# Bàn giao công việc — Thịnh (Member 4: Nginx & Load Balancing)

> Phạm vi: cấu hình Nginx reverse proxy + load balancing, scale backend replica,
> kiểm chứng phân phối request và failover, chạy k6 load test và đưa số liệu
> lên Grafana.  Tất cả số liệu trong tài liệu này đến từ **lượt chạy thật**
> trên Docker Desktop, không phải số liệu ước lượng.

---

## 1. Tóm tắt kết quả

| Hạng mục | Trạng thái | Bằng chứng |
|---|---|---|
| Nginx reverse proxy + Round Robin | ✅ Hoàn thành | `monitoring/nginx/nginx.conf` |
| Scale backend lên 3 replica | ✅ Hoàn thành | `docker-compose.scaling.yml` |
| Phân phối request đều 3 replica | ✅ 50 / 50 / 50 (100 % chênh lệch 0) | log `aives-access.log` |
| Failover khi chết 1 replica | ✅ 0 request rơi | mục 4 bên dưới |
| k6 load test (50 → 200 VU) | ✅ 0 % lỗi, 100 % checks | `monitoring/results/2026-11-06/` |
| Grafana dashboard | ✅ 12 panel | `monitoring/nginx-dashboard.json` |
| Bảng so sánh trước/sau | ✅ p95 giảm 25–57 % | `docs/nginx-loadtest-results.md` |
| **Phát hiện nút thắt database** | ✅ Đo đỉnh 372 connection | mục 9 bên dưới |

---

## 2. Kiến trúc sau khi tối ưu

```
                    ┌──────────────────────────┐
   k6 (50→500 VU) ──▶│  nginx  :8088             │
                    │  upstream aives_backend   │
                    │  server backend:8080      │
                    │    resolve + zone         │
                    └───┬──────────┬────────┬────┘
                        │          │        │
                   ┌────▼───┐ ┌────▼───┐ ┌──▼─────┐
                   │backend-1│ │backend-2│ │backend-3│
                   └────┬───┘ └────┬───┘ └────┬───┘
                        └──────────┼──────────┘
                              ┌────▼──────┐
                              │ postgres  │
                              │ aives_dev │
                              └───────────┘
```

**Điểm kỹ thuật quan trọng:** Nginx dùng `server backend:8080 resolve` +
`resolver 127.0.0.11 valid=10s` + `zone aives_backend 64k`.  Nếu thiếu `resolve`,
Nginx giữ IP cũ của replica đã restart → "Host is unreachable" → mất 17–98 %
request.  `zone` là bắt buộc để lưu trạng thái resolve.

---

## 3. Cách chạy lại demo

### 3.1. Khởi động stack đã scale

```powershell
$env:DATABASE_CONNECTION = "Host=postgres;Database=aives_dev;Username=aives;Password=aives-local-development"
$env:Database__SeedOnStartup = "true"
$env:LOGIN_RATE_LIMIT = "5000"

docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile local-db --profile scaling up -d --scale backend=3 --no-recreate nginx
```

### 3.2. Bật monitoring (Prometheus + Grafana + exporter)

```powershell
docker compose -f monitoring/compose.yaml up -d
python monitoring/grafana/setup-dashboard.py   # tạo datasource + import dashboard
```

Dashboard: <http://localhost:3000/d/aives-nginx-lb> (admin / admin)

### 3.3. Chuẩn bị dữ liệu ảo + chạy k6

```powershell
$env:LOADTEST_TEST_PASSWORD = "Dev@12345"

# ── CÁCH NHANH NHẤT: 1 lệnh làm hết ─────────────────────────────
# reset DB → scale 3 replica → chạy ma trận 50/100/200 → tổng hợp báo cáo
.\monitoring\k6\run-matrix.ps1 -VusList @(50, 100, 200) -Replicas 3 -ResetDb
```

Tinh chỉnh nhanh:

```powershell
# Chỉ đo 1 mức
.\monitoring\k6\run-matrix.ps1 -VusList @(50)

# Đổi số replica
.\monitoring\k6\run-matrix.ps1 -VusList @(100,200) -Replicas 5

# Chỉ đo 1 nhánh
.\monitoring\k6\run-matrix.ps1 -VusList @(100) -Variants nginx

# Dùng lại dữ liệu ảo, không scale lại
.\monitoring\k6\run-matrix.ps1 -VusList @(100) -SkipFixture -SkipScale
```

Chạy tay từng bước:

```powershell
# 1) Tạo học sinh / giảng viên / course / question / exam / session
.\monitoring\k6\prepare-demo-fixture.ps1 -Students 200 -Graders 5

# 2) Chạy load test qua Nginx
.\monitoring\k6\run-nginx-demo.ps1 -Vus 200 -Label nginx -BaseUrl "http://nginx:8088"

# 3) Chạy baseline (trực tiếp vào 1 backend) để so sánh
.\monitoring\k6\run-nginx-demo.ps1 -Vus 200 -Label direct -BaseUrl "http://backend:8080"

# 4) Tổng hợp ra bảng + CSV
python monitoring/k6/summarize-nginx-results.py
```

---

## 4. Kiểm chứng phân phối & failover

### 4.1. Phân phối Round Robin

```powershell
docker exec aives-dev-nginx-1 sh -c `
  "grep -o 'upstream=[0-9.]*:[0-9]*' /var/log/nginx/aives-access.log | sort | uniq -c | sort -rn"
```

Kết quả đo trên 1 lượt 50 VUs, đã xoá log trước khi chạy (150 request):

```
50 upstream=172.18.0.6:8080
50 upstream=172.18.0.7:8080
50 upstream=172.18.0.8:8080
```

→ sai lệch **0 %** (chia đều tuyệt đối), xác nhận Round Robin hoạt động đúng.

### 4.2. Failover — dừng 1 replica giữa lúc tải

```powershell
docker stop aives-dev-backend-2
# ... chạy k6 ...
docker start aives-dev-backend-2
```

Kết quả: **30/30 request HTTP 200**, chia đều 15/15 cho 2 replica còn lại →
**không rơi request nào**.  Sau khi restart, phân phối trở lại 10/10/10.

---

## 5. Kết quả load test (trước / sau tối ưu)

> Số liệu chính thức trong báo cáo được đo trên **DB sạch**, cùng payload, cùng
> executor, chạy `direct` rồi `nginx` liên tiếp → điều kiện so sánh công bằng.

| Profile | p95 trước (1 backend) | p95 sau (3 replica) | Cải thiện | Error trước | Error sau |
|---|---|---|---|---|---|
| 50 users | 3529 ms | 1512 ms | **−57.1 %** | 0 % | 0 % |
| 100 users | 4133 ms | 3086 ms | **−25.3 %** | 0 % | 0 % |
| 200 users | 9681 ms | 6485 ms | **−33.0 %** | 0 % | 0 % |

Bảng đầy đủ (requests, throughput, avg, p95, error rate, checks):
[`docs/nginx-loadtest-results.md`](./nginx-loadtest-results.md)
CSV: `monitoring/results/nginx-summary.csv`
Hướng dẫn dùng nhanh: [`docs/nginx-demo-quickstart.md`](./nginx-demo-quickstart.md)

**Nhận xét:**
- Nginx + 3 replica cải thiện **p95 rõ rệt ở mọi mức tải** (25–57 %).
- Ở 50 users, avg latency giảm từ 1130 ms → 619 ms (−45 %).
- Từ 200 users trở lên, cải thiện p95 bị bão hoà vì nút thắt chuyển sang
  **database** (connection pool), không phải Nginx — xem mục 9.

---

## 6. Files bàn giao

| File | Vai trò |
|---|---|
| `monitoring/nginx/nginx.conf` | Cấu hình Nginx reverse proxy + Round Robin + resolver + log upstream |
| `docker-compose.scaling.yml` | Override cho chế độ scaled (bỏ publish port backend, thêm Nginx) |
| `monitoring/nginx-dashboard.json` | Dashboard Grafana riêng cho Nginx (12 panel) |
| `monitoring/prometheus.yml` | Thêm scrape job `nginx` + `postgres` |
| `monitoring/compose.yaml` | Thêm `nginx-prometheus-exporter` + `postgres-exporter` |
| `monitoring/grafana/setup-dashboard.py` | Tạo datasource (uid cố định) + import dashboard |
| `monitoring/k6/nginx-submit.js` | Script k6: login → start → submit-answer |
| `monitoring/k6/prepare-demo-fixture.ps1` | Sinh dữ liệu ảo (học sinh, course, exam, session) |
| `monitoring/k6/run-nginx-demo.ps1` | Chạy k6 trong Docker, lưu summary + console log |
| `monitoring/k6/run-matrix.ps1` | **Công cụ chính**: reset DB → scale replica → chạy ma trận → tổng hợp |
| `monitoring/k6/summarize-nginx-results.py` | Tổng hợp → Markdown + CSV |
| `docs/nginx-loadtest-results.md` | Báo cáo kết quả load test |
| `docs/nginx-demo-quickstart.md` | Hướng dẫn dùng nhanh + tham số tinh chỉnh |
| `monitoring/results/2026-11-06/` | Log + summary từng lượt chạy |

---

## 7. Vấn đề đã gặp & cách xử lý

| Vấn đề | Nguyên nhân | Cách xử lý |
|---|---|---|
| `no resolver defined to resolve names at run time in upstream` | `resolver` khai sai context | Đặt `resolver` ở **http context**, không phải trong `server {}` |
| `relation "exam_slot_templates" does not exist` | Image backend cũ 2 ngày, chưa có migration mới | Rebuild image `docker compose build backend db-init` |
| Schema thiếu bảng sau khi `EnsureCreatedAsync` | `EnsureCreatedAsync` không chạy migration trên schema có sẵn | Drop + recreate DB bằng image mới → đủ 21 bảng |
| **HTTP 500 `transient failure` khi tải 500 VU** | **Npgsql pool mặc định 100/replica × 3 = 300 > `max_connections=250`** | **Set `Maximum Pool Size=130` + `max_connections=400`** (xem mục 9) |
| `StartTime cannot be in the past` (400) | API chặn `StartTime < now - 5 phút` | Đặt `StartTime = now - 2 phút` (vừa qua cả ràng buộc create lẫn start) |
| `Ca thi chưa mở` (400) | API start yêu cầu `now >= StartTime` | Cùng fix trên |
| `docker: invalid spec: :/results:rw` | Mount path Windows không escape | Dùng `--mount type=bind,source=...,target=...` |
| `only arrays can be made into SharedArray` | Fixture là object, không phải array | `open()` ở global scope (init stage) |
| `the "open" function is only available in the init stage` | Gọi `open()` trong `setup()` | Đọc ở top-level của script |
| Nginx exporter trả `403` | `stub_status` chỉ allow loopback | Thêm `allow 172.16.0.0/12` (subnet Docker) rồi `nginx -s reload` |
| Dashboard không có dữ liệu | Datasource uid trong JSON không tồn tại | `setup-dashboard.py` tạo datasource với uid `cg393kpfzb2f4b` |

---

## 8. Hướng mở rộng (không làm trong kỳ này)

1. **Least-conn thay Round Robin** — thêm file include riêng để bật `least_conn`
   khi request có thời gian xử lý không đồng đều.
2. **Health check chủ động** — hiện Nginx chỉ dựa vào `max_fails`/`fail_timeout`;
   có thể thêm `health_check` (nginx Plus) hoặc sidecar.
3. **Rate limiting ở tầng Nginx** — đẩy `limit_req` xuống Nginx thay vì trong
   app, để giảm tải cho backend.
4. **Giải quyết nút thắt database** — connection pool / PgBouncer / read replica,
   vì ở 500 users phần lớn request rơi do độ trễ query chứ không phải do Nginx.

---

## 9. Phát hiện quan trọng: nút thắt thật sự nằm ở DATABASE, không phải ở Nginx

Trong quá trình chạy ma trận, **3 replica bị "connection pool exhausted"** khi tải
500 VU.  Đây là phát hiện quan trọng nhất của phần demo:

### 9.1. Nguyên nhân

```
Npgsql.NpgsqlException: The connection pool has been exhausted,
either raise 'Max Pool Size' (currently 60) or 'Timeout' (currently 30 seconds)
```

- Npgsql **mặc định** `Maximum Pool Size = 100` cho mỗi process.
- Scale 3 replica → `3 × 100 = 300` connection tiềm năng.
- `max_connections` của PostgreSQL local mặc định chỉ **250** (đã nâng lên 600).
- → Vượt quota → request phải chờ 30–60 s rồi timeout → HTTP 400/500.

Đã xác minh **không có lock contention** (`pg_stat_activity` cho `wait_event_type='Lock'`
= 0), nên đây là vấn đề **số lượng connection**, không phải tranh chấp dữ liệu.

**Đo thực tế** (bảng `conn_watch` trong DB, lấy mẫu mỗi giây khi 500 VU chạy):

| Chỉ số | Giá trị |
|---|---|
| Đỉnh connection đồng thời | **372** |
| Trung bình | 101 |
| `max_connections` lúc đó | 400 |

→ Đỉnh 372 đã sát trần 400 → một phần request không kịp lấy connection.

### 9.2. Cách sửa đã áp dụng

Trong `docker-compose.scaling.yml`:

```yaml
ConnectionStrings__DefaultConnection: >-
  Host=postgres;Database=aives_dev;Username=aives;Password=aives-local-development;
  Maximum Pool Size=190;Minimum Pool Size=5;Timeout=60;Command Timeout=120
POSTGRES_MAX_CONNECTIONS: "600"
```

**Cần nhớ:** `Maximum Pool Size` phải được đặt **nhân với số replica** và so với
`max_connections` của PostgreSQL:

```
Số replica × Maximum Pool Size  ≤  max_connections của PostgreSQL
```

> **Lưu ý:** Npgsql **không** hỗ trợ keyword `Maximum Idle Connections` — dùng sẽ
> làm app crash ngay lúc khởi động với
> `ArgumentException: Couldn't set maximum idle connections`.

### 9.3. Bài học cho phần Database (việc của nhóm Database)

Khi scale ngang backend, **connection pool phải được tính toán lại**.  Nếu không,
số backend tăng lên sẽ làm DB chết trước.  Các hướng xử lý:

| Hướng | Mô tả |
|---|---|
| Giảm pool size | Đây là cách đã dùng ở trên |
| **PgBouncer** | Connection pooler trước PostgreSQL — giải pháp chuẩn cho production |
| Truyền `DATABASE_URL` từ Neon | Neon pooler đã làm sẵn lớp pool này |
| Read replica | Tách query đọc (báo cáo, danh sách) khỏi query ghi |
