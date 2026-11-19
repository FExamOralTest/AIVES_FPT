# Hướng dẫn nhanh — Công cụ demo Nginx Load Balancing (Thịnh)

Mọi thứ đã chạy sẵn. Bạn chỉ cần **1 lệnh** để đo, rồi tinh chỉnh tham số.

---

## 1. Chạy nhanh (30 giây)

```powershell
$env:LOADTEST_TEST_PASSWORD = "Dev@12345"
.\monitoring\k6\run-matrix.ps1 -VusList @(50)
```

Kết quả in ra:
- Số liệu `direct` (1 backend) vs `nginx` (3 replica)
- Bảng so sánh ở `docs/nginx-loadtest-results.md`
- CSV ở `monitoring/results/nginx-summary.csv`

---

## 2. Tinh chỉnh — các tham số hay dùng

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `-VusList` | `50, 100, 200` | Các mức tải cần đo. Ví dụ `@(50)`, `@(100,200)`, `@(50,100,200,500)` |
| `-Replicas` | `3` | Số backend replica khi chạy qua Nginx. `@(2)`, `@(5)`… |
| `-Variants` | `direct, nginx` | Chỉ đo 1 nhánh: `-Variants nginx` hoặc `-Variants direct` |
| `-ResetDb` | tắt | Xoá & tạo lại DB local trước khi đo (dùng khi muốn số liệu so sánh được với lần trước) |
| `-SkipFixture` | tắt | Dùng lại dữ liệu ảo đã có (bỏ bước tạo học sinh) |
| `-SkipScale` | tắt | Không scale lại replica (stack đã đúng sẵn) |

### Ví dụ thực tế

```powershell
# Chỉ đo qua Nginx, 5 replica, mức 100 và 200
.\monitoring\k6\run-matrix.ps1 -VusList @(100,200) -Replicas 5 -Variants nginx

# So sánh 2 replica vs 3 replica ở mức 100
.\monitoring\k6\run-matrix.ps1 -VusList @(100) -Replicas 2 -Variants nginx -ResetDb
.\monitoring\k6\run-matrix.ps1 -VusList @(100) -Replicas 3 -Variants nginx -ResetDb

# Thử tải nặng (chậm, ~10 phút)
.\monitoring\k6\run-matrix.ps1 -VusList @(500) -ResetDb
```

---

## 3. Trạng thái hiện tại của môi trường

| Thành phần | Trạng thái | Ghi chú |
|---|---|---|
| 3 backend replica | ✅ healthy | scale bằng `-Replicas` |
| Nginx (round robin) | ✅ healthy | `http://localhost:8088` |
| PostgreSQL local | ✅ `max_connections=600` | pool 190/replica |
| Prometheus + Grafana | ✅ | `http://localhost:3000/d/aives-nginx-lb` (admin/admin) |
| Nginx exporter | ✅ | metric `nginx_http_requests_total` |
| k6 script | ✅ | `monitoring/k6/nginx-submit.js` |

---

## 4. Kiểm chứng nhanh (không cần k6)

```powershell
# Phân phối round robin hiện tại
docker exec aives-dev-nginx-1 sh -c "grep -o 'upstream=[0-9.]*:[0-9]*' /var/log/nginx/aives-access.log | sort | uniq -c | sort -rn"

# Số connection đang dùng
docker exec aives-dev-postgres-1 psql -U aives -d aives_dev -At -c "SELECT count(*) FROM pg_stat_activity WHERE datname='aives_dev';"

# Health qua Nginx
Invoke-RestMethod http://localhost:8088/api/health/db
```

---

## 5. Kết quả đã verify (mức an toàn, 0 % lỗi)

| Profile | p95 trước (1 backend) | p95 sau (3 replica) | Cải thiện | Error rate |
|---|---|---|---|---|
| 50 users | 3529 ms | 1512 ms | **−57.1 %** | 0 % |
| 100 users | 4133 ms | 3086 ms | **−25.3 %** | 0 % |
| 200 users | 9681 ms | 6485 ms | **−33.0 %** | 0 % |

Tất cả đều chạy trên **cùng DB sạch**, cùng payload, cùng executor → so sánh được.

> Ở 500 VUs, hệ thống đạt giới hạn connection của PostgreSQL local (đo được đỉnh
> **372 connection** khi 500 request chạy song song).  Đây là nút thắt **database**,
> không phải Nginx — xem mục 9 trong `docs/nginx-handover-thinh.md`.

---

## 6. Cấu trúc kết quả

```
monitoring/results/2026-11-06/
├── nginx-demo-<timestamp>/
│   ├── fixture.json              ← danh sách user ảo + session id
│   └── <vUs>/
│       ├── k6-console-<label>-<time>.log
│       └── k6-summary-<label>-<time>.json   ← k6 ghi ra, dùng để tổng hợp
└── nginx-summary.csv
```

`label` là `direct` (gọi thẳng backend) hoặc `nginx` (qua load balancer).

---

## 7. Cấu hình có thể tinh chỉnh thêm

### `docker-compose.scaling.yml` — connection pool

```yaml
ConnectionStrings__DefaultConnection: >-
  ...;Maximum Pool Size=190;Minimum Pool Size=5;Timeout=60;Command Timeout=120
POSTGRES_MAX_CONNECTIONS: "600"
```

**Quy tắc:** `Số replica × Maximum Pool Size ≤ max_connections`

Đổi `-Replicas` thì nhớ sửa lại 2 con số này.

### `monitoring/nginx/nginx.conf` — load balancing

```nginx
upstream aives_backend {
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
    keepalive 64;
}
```

Muốn đổi sang `least_conn`: thêm `least_conn;` vào khối `upstream`.

### `monitoring/k6/nginx-submit.js` — kịch bản test

| Biến môi trường | Mặc định |
|---|---|
| `VUS` | 50 |
| `SESSION_ID` | bắt buộc |
| `BASE_URL` | `http://nginx:8088` |
| `USER_PREFIX` | `demo-` |
| `TEST_PASSWORD` | bắt buộc |

Threshold hiện tại: `submit_answer_failures < 5%`, `checks > 95%`.
