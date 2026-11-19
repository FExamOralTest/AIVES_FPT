# Nginx & Load Balancing — Hướng dẫn chi tiết

> **Thành viên phụ trách:** Thịnh (Member 4)
> **Phạm vi:** Cấu hình Nginx reverse proxy, thiết lập upstream, load balancing
> Round Robin, kiểm chứng phân phối request, kiểm chứng failover khi replica
> ngừng hoạt động.
> **Deliverable chính:** [`monitoring/nginx/nginx.conf`](../../monitoring/nginx/nginx.conf)
>
> Mọi số liệu trong tài liệu này đến từ **lượt chạy thật** trên Docker Desktop
> (Windows 11, Docker 29.7.2, Nginx 1.27.5-alpine), không phải số liệu ước lượng.

---

## Mục lục

1. [Khái niệm nền](#1-khái-niệm-nền)
2. [Kiến trúc tổng thể](#2-kiến-trúc-tổng-thể)
3. [Cấu hình Nginx Reverse Proxy](#3-cấu-hình-nginx-reverse-proxy)
4. [Thiết lập Upstream](#4-thiết-lập-upstream)
5. [Cấu hình Load Balancing (Round Robin)](#5-cấu-hình-load-balancing-round-robin)
6. [Chuẩn bị môi trường chạy thử](#6-chuẩn-bị-môi-trường-chạy-thử)
7. [Kiểm tra request được phân phối đến các Backend replicas](#7-kiểm-tra-request-được-phân-phối-đến-các-backend-replicas)
8. [Kiểm tra lỗi khi một replica ngừng hoạt động](#8-kiểm-tra-lỗi-khi-một-replica-ngừng-hoạt-động)
9. [Giám sát trên Grafana](#9-giám-sát-trên-grafana)
10. [Bảng tham chiếu nhanh](#10-bảng-tham-chiếu-nhanh)
11. [Xử lý sự cố](#11-xử-lý-sự-cố)

---

## 1. Khái niệm nền

### 1.1. Reverse Proxy là gì?

**Reverse proxy** (proxy đảo chiều) là một server đứng **phía trước** nhóm
backend server, nhận request từ client và chuyển tiếp (proxy) xuống backend.

```text
Client ──► Reverse Proxy ──┬──► Backend 1
                            ├──► Backend 2
                            └──► Backend 3
```

Khác biệt với **forward proxy** (proxy thuận):

| | Forward proxy | Reverse proxy |
|---|---|---|
| Hướng | Client → Proxy → Internet | Internet → Proxy → Backend |
| Ai chủ động kết nối | Client | Server |
| Mục đích | Ẩn IP client, vượt firewall | Ẩn backend, chia tải, cache, TLS |
| Ai cấu hình | Người dùng | Quản trị viên server |

**Vì sao cần reverse proxy cho AIVES?**

| Lợi ích | Giải thích trong dự án |
|---|---|
| **Cân bằng tải** | 3 backend replica chia sẻ request → không điểm chết |
| **Ẩn cấu trúc nội bộ** | Client không biết backend thật sự nằm ở đâu, cổng nào |
| **Điểm vào duy nhất** | Rate limit, timeout, logging, TLS chỉ cấu hình 1 lần ở Nginx |
| **Dễ scale** | Thêm backend = scale Docker, không sửa code client |
| **Health check & failover** | Tự loại replica chết khỏi vòng phân phối |

### 1.2. Load Balancing là gì?

**Load balancing** (cân bằng tải) là kỹ thuật phân tán request đến nhiều server
backend để:

- Tăng **throughput** tổng thể (phục vụ được nhiều request/s hơn)
- Giảm **latency** (mỗi request được xử lý bởi máy ít tải hơn)
- Tăng **khả dụng** (một server chết, dịch vụ vẫn chạy)

### 1.3. Các thuật toán phân phối

| Thuật toán | Cách hoạt động | Khi nào dùng |
|---|---|---|
| **Round Robin** ⭐ | Xoay vòng: req1→A, req2→B, req3→C, req4→A… | Backend có sức mạnh & tải giống nhau |
| Weighted Round Robin | Như RR nhưng mỗi backend có trọng số riêng | Backend không đồng đều (server mạnh hơn) |
| Least Connections | Chọn backend có **ít connection nhất** hiện tại | Thời gian xử lý chênh nhau nhiều |
| IP Hash | Cùng IP client → cùng backend | Cần session affinity (sticky session) |
| Random | Chọn ngẫu nhiên | Khi không cần đảm bảo phân bố |

> **Đề bài yêu cầu Round Robin** → cấu hình hiện tại dùng Round Robin (mặc định
> của Nginx, không cần khai báo thêm directive nào).

### 1.4. Các khái niệm Nginx quan trọng

**`upstream`** — khối cấu hình định nghĩa một "nhóm server" mà Nginx có thể
`proxy_pass` tới. Tách riêng upstream giúp:
- Quản lý cân bằng tải tập trung 1 chỗ
- `proxy_pass` chỉ cần ghi tên (`http://aives_backend`) thay vì IP/port

**`resolver`** — chỉ định DNS server để Nginx dùng khi cần phân giải hostname
**tại runtime** (không phải lúc khởi động). Bắt buộc cho `resolve` trong upstream.

**`zone`** — vùng bộ nhớ dùng chung để lưu trạng thái runtime của upstream
(bảng địa chỉ, cờ lỗi). Bắt buộc khi dùng `resolve`.

**`stub_status`** — module Nginx built-in cung cấp số liệu cơ bản
(số connection, số request) qua HTTP để exporter đọc.

**`max_fails` / `fail_timeout`** — cơ chế **health check ngầm** của Nginx:
- `max_fails=3`: sau 3 lần lỗi liên tiếp → tạm loại server khỏi vòng phân phối
- `fail_timeout=10s`: sau 10 giây, thử lại server đó

**`keepalive`** — giữ connection HTTP tới backend mở để tái sử dụng, giảm
overhead của TCP handshake + TLS handshake. Phải đi kèm
`proxy_http_version 1.1` + `proxy_set_header Connection ""`.

**`$upstream_addr`** — biến Nginx chứa địa chỉ backend thực sự đã phục vụ
request. **Đây là biến then chốt** để chứng minh request được phân phối.

---

## 2. Kiến trúc tổng thể

```text
                    ┌──────────────────────────────────────────┐
                    │  aives-dev_default (Docker network)      │
                    │                                          │
  k6 / client ────► │  ┌──────────────────────────────┐      │
  :8088             │  │  nginx:8088                  │      │
                    │  │  ─ resolver 127.0.0.11        │      │
                    │  │  ─ upstream aives_backend     │      │
                    │  │      ├─► backend-1 :8080      │      │
                    │  │      ├─► backend-2 :8080      │      │
                    │  │      └─► backend-3 :8080      │      │
                    │  │  ─ log: aives-access.log      │      │
                    │  └──────────┬───────────────────┘      │
                    │             │                          │
                    │             ▼                          │
                    │  ┌──────────────────────────────┐      │
                    │  │  postgres:5432                │      │
                    │  │  (max_connections=600)        │      │
                    │  └──────────────────────────────┘      │
                    │                                          │
                    │  ┌──────────────────────────────┐      │
                    │  │  nginx-prometheus-exporter    │◄─────┼──► prometheus → grafana
                    │  │  :9113 → /nginx_status        │      │      (network aives-monitoring)
                    │  └──────────────────────────────┘      │
                    └──────────────────────────────────────────┘
```

### 2.1. Các thành phần liên quan

| Thành phần | File | Vai trò |
|---|---|---|
| Nginx reverse proxy | `monitoring/nginx/nginx.conf` | **Deliverable chính** |
| Override khi scale | `docker-compose.scaling.yml` | Bỏ publish port backend, tune pool, định nghĩa service nginx |
| Base compose | `docker-compose.yml` | Định nghĩa service `backend`, `postgres` |
| Monitoring stack | `monitoring/compose.yaml` | Prometheus, Grafana, cAdvisor, 2 exporter |
| Dashboard | `monitoring/nginx-dashboard.json` | 12 panel trực quan hoá |
| Script k6 | `monitoring/k6/nginx-submit.js` | Kịch bản nộp bài đồng thời |
| Fixture | `monitoring/k6/prepare-demo-fixture.ps1` | Sinh học sinh/course/exam/session ảo |
| Runner | `monitoring/k6/run-nginx-demo.ps1` | Chạy k6, lưu summary |

### 2.2. Vì sao phải scale backend?

Khi chạy `docker compose up` thông thường, service `backend` chỉ có **1 container**.
Muốn có N replica, Compose cần cơ chế **scale**:

```powershell
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile local-db --profile scaling `
    up -d --scale backend=3 nginx
```

**Vấn đề phải giải quyết:** file `docker-compose.yml` gốc khai báo
`ports: 127.0.0.1:5000:8080` cho backend. Khi scale 3 replica, container thứ 2 và 3
sẽ fail với `port is already allocated` — vì 1 port host chỉ bind được 1 container.

**Cách giải quyết trong `docker-compose.scaling.yml`:**

```yaml
services:
  backend:
    ports: !reset []      # ← xoá toàn bộ khai báo ports của base compose
```

`!reset []` là directive mới của Compose Spec (v2.24+) — cho phép **xoá** giá trị
đã khai ở file override trước đó. Sau đó backend chỉ được truy cập qua:
- Nginx (`http://nginx:8088`) — đường chính
- Mạng nội bộ Docker (`http://backend:8080`) — dùng cho baseline so sánh

### 2.3. Docker DNS và vấn đề định tuyến

Khi scale, Docker gán IP cho từng container **động** (ví dụ `172.18.0.6`,
`172.18.0.7`, `172.18.0.8`). Service name `backend` trong Docker network được
Embedded DNS (`127.0.0.11`) phân giải ra **nhiều bản ghi A** — mỗi replica một IP.

```bash
# Trong container nginx:
nslookup backend
# → Address: 172.18.0.8
# → Address: 172.18.0.6
# → Address: 172.18.0.7
```

Nếu Nginx chỉ resolve 1 lần lúc khởi động, khi một replica restart (đổi IP) thì
Nginx vẫn giữ IP cũ → `Host is unreachable` → mất một phần lớn request.
→ Bắt buộc dùng `resolve` (xem mục 4.3).

---

## 3. Cấu hình Nginx Reverse Proxy

### 3.1. File cấu hình đầy đủ

**Đường dẫn:** `monitoring/nginx/nginx.conf`
**Mount vào container:** `/etc/nginx/conf.d/default.conf` (read-only)

```nginx
# log_format phải nằm ở http context.
log_format aives_upstream '$remote_addr "$request" $status '
                         'upstream=$upstream_addr rt=$request_time '
                         'urt=$upstream_response_time';

# Docker embedded DNS — BẮT BUỘC cho `server ... resolve` trong upstream.
resolver 127.0.0.11 valid=10s ipv6=off;

upstream aives_backend {
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
    keepalive 64;
}

server {
    listen 8088;
    server_name _;

    client_max_body_size 50m;

    location = /health {
        access_log off;
        default_type text/plain;
        return 200 "ok\n";
    }

    location = /nginx_status {
        stub_status;
        access_log off;
        allow 127.0.0.1;
        allow ::1;
        allow 172.16.0.0/12;
        deny all;
    }

    location / {
        proxy_pass http://aives_backend;
        proxy_http_version 1.1;
        proxy_set_header Connection "";

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_connect_timeout 10s;
        proxy_send_timeout 360s;
        proxy_read_timeout 360s;

        access_log /var/log/nginx/aives-access.log aives_upstream;
    }
}
```

### 3.2. Giải thích từng khối

#### Khối `log_format` — định dạng log có thông tin upstream

```nginx
log_format aives_upstream '$remote_addr "$request" $status '
                         'upstream=$upstream_addr rt=$request_time '
                         'urt=$upstream_response_time';
```

| Biến | Ý nghĩa | Dùng để làm gì |
|---|---|---|
| `$remote_addr` | IP client | Truy vết nguồn request |
| `$request` | Method + URI + HTTP version | Xác định endpoint |
| `$status` | HTTP status code | Đếm lỗi (502/504/499) |
| **`$upstream_addr`** | **IP:port backend đã phục vụ** | **Chứng minh phân phối Round Robin** |
| `$request_time` | Tổng thời gian xử lý (client → Nginx → backend) | Latency tổng |
| `$upstream_response_time` | Thời gian backend trả lời | Tách latency của app vs network |

> **Điểm mấu chốt:** `$upstream_addr` là nền tảng của phần kiểm chứng ở mục 7.
> Nếu không có nó, ta không biết request thực sự đi tới replica nào.

`log_format` **phải** khai ở `http` context — file này được `include` trong
`http {}` của `/etc/nginx/nginx.conf` nên thỏa điều kiện.

#### Khối `server` — cấu hình reverse proxy

```nginx
server {
    listen 8088;
    server_name _;
```

| Chỉ thị | Giá trị | Giải thích |
|---|---|---|
| `listen 8088` | port 8088 | Cổng nội bộ của Nginx, publish ra host qua `docker-compose.scaling.yml` |
| `server_name _` | wildcard | Không có domain cụ thể, nhận mọi Host header |

**Vì sao `listen 8088` mà không phải `80`?**
- Cổng 80 thường đã bị chiếm (hoặc cần quyền admin trên Windows).
- Cổng không mặc định giúp tránh xung đột với service khác trong Docker network.
- Không mất gì về mặt hiệu năng.

##### `client_max_body_size 50m`

```nginx
client_max_body_size 50m;
```

Mặc định Nginx chỉ cho phép body **1 MB**. Trong AIVES, endpoint nộp bài nhận
multipart kèm file audio → vượt 1 MB. Nếu không nới, client sẽ nhận
`413 Request Entity Too Large` trước khi request tới backend.

##### Các `proxy_set_header` — truyền metadata về client

```nginx
proxy_set_header Host              $host;
proxy_set_header X-Real-IP         $remote_addr;
proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
```

| Header | Biến | Tác dụng |
|---|---|---|
| `Host` | `$host` | Backend nhận đúng domain client dùng (quan trọng cho redirect/OAuth) |
| `X-Real-IP` | `$remote_addr` | IP client thật (backend không thấy IP Nginx) |
| `X-Forwarded-For` | `$proxy_add_x_forwarded_for` | Chuỗi IP đầy đủ, **nối thêm** vào header đã có |
| `X-Forwarded-Proto` | `$scheme` | `http` hay `https` — backend dùng để tạo link đúng |

> `$proxy_add_x_forwarded_for` vs `$proxy_set_header X-Forwarded-For $remote_addr`:
> cái **đầu** nối chuỗi và giữ lại lịch sử; cái **sau** ghi đè. Khi có nhiều tầng
> proxy, dùng cái đầu để không mất thông tin.

##### Timeout

```nginx
proxy_connect_timeout 10s;    # thiết lập TCP connection tới backend
proxy_send_timeout    360s;   # gửi request body lên backend
proxy_read_timeout    360s;   # chờ response từ backend
```

Mặc định Nginx chỉ chờ **60 giây**. Trong AIVES, thao tác chấm bài bằng AI
(STT + chấm điểm) có thể mất thời gian → cần nới lên 360s (6 phút).
`proxy_connect_timeout` giữ ở 10s vì backend local không bao giờ cần lâu để
connect — timeout ngắn ở đây giúp phát hiện replica chết nhanh hơn.

##### `location = /health` — endpoint cho Docker healthcheck

```nginx
location = /health {
    access_log off;
    default_type text/plain;
    return 200 "ok\n";
}
```

Trả về `200` ngay lập tức, **không** chạm vào backend → healthcheck phản ánh
đúng trạng thái Nginx (không phải trạng thái app). Định nghĩa trong
`docker-compose.scaling.yml`:

```yaml
healthcheck:
  test: ["CMD", "wget", "--quiet", "--spider", "http://127.0.0.1:8088/health"]
  interval: 10s
  timeout: 5s
  retries: 6
```

##### `location = /nginx_status` — chỉ dành cho exporter

```nginx
location = /nginx_status {
    stub_status;
    access_log off;
    allow 127.0.0.1;
    allow ::1;
    allow 172.16.0.0/12;
    deny all;
}
```

`stub_status` trả về 4 số liệu: `Active connections`, `accepts`, `handled`,
`requests`, và trạng thái `Reading/Writing/Waiting`.

**Vì sao phải `allow`/`deny`?**
Endpoint này tiết lộ thông tin hệ thống (số lượng connection, lưu lượng).
Nếu để mở toàn Internet → thông tin nội bộ bị lộ. Ở đây chỉ cho phép:
- `127.0.0.1` / `::1` — loopback
- `172.16.0.0/12` — **toàn bộ dải private Docker network** (172.16 – 172.31)

> **Lưu ý thực tế:** Docker mặc định dùng `172.17.0.0/16`, `172.18.0.0/16`… —
> đều nằm trong `172.16.0.0/12`. Nếu chỉ `allow 172.18.0.0/16` thì khi đổi
> network hoặc dùng mạng `192.168.x.x` (Docker Desktop trên Windows) sẽ bị 403.

##### `location /` — proxy chính

```nginx
location / {
    proxy_pass http://aives_backend;
    proxy_http_version 1.1;
    proxy_set_header Connection "";
    ...
    access_log /var/log/nginx/aives-access.log aives_upstream;
}
```

| Chỉ thị | Giá trị | Giải thích |
|---|---|---|
| `proxy_pass` | `http://aives_backend` | Trỏ tới **tên upstream**, không ghi IP/port → Nginx tự cân bằng |
| `proxy_http_version` | `1.1` | Bắt buộc cho `keepalive` |
| `proxy_set_header Connection ""` | rỗng | Xoá header `Connection: keep-alive` mặc định → cho phép tái sử dụng connection |
| `access_log` | `/var/log/nginx/aives-access.log` | Ghi vào volume `nginx-logs` để đọc được từ host |

> **Lưu ý về `proxy_pass` và path:** ở đây `proxy_pass` **không** có phần path
> (`http://aives_backend` chứ không phải `http://aives_backend/`), nên URI được
> chuyển nguyên vẹn. Nếu có path, Nginx sẽ thay thế prefix tương ứng — dễ gây
> bug 404 khi ứng dụng có route `/api/...`.

### 3.3. Kiểm tra cấu hình hợp lệ

```powershell
# Test cấu hình (không reload)
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile scaling exec nginx nginx -t
```

Kết quả mong đợi:

```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

**Reload** (áp dụng thay đổi không gián đoạn):

```powershell
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile scaling exec nginx nginx -s reload
```

> **Luôn `nginx -t` trước khi reload.** Nếu cấu hình sai, `reload` sẽ fail và
> Nginx giữ nguyên cấu hình cũ — nhưng nếu restart container với cấu hình sai
> thì Nginx **không start được** → mất toàn bộ dịch vụ.

### 3.4. Các lỗi cấu hình thường gặp

| Lỗi | Nguyên nhân | Cách sửa |
|---|---|---|
| `no resolver defined to resolve names at run time in upstream` | Khai `resolver` sai context | `resolver` phải ở `http` context, **không** trong `server {}` |
| `host not found in upstream "backend:8080"` | Backend chưa sẵn sàng lúc Nginx start | Dùng `resolve` để không cần resolve lúc start |
| `invalid number of arguments in "server" directive` | Sai cú pháp `server backend:8080 resolve ...` | Kiểm tra thứ tự: `server <addr> [resolve] [max_fails=N] [fail_timeout=T]` |
| `unknown directive "least_conn"` | Thiếu `ngx_http_upstream_module` (bản build không có) | Kiểm tra `nginx -V` |
| `duplicate "resolver"` | Khai `resolver` ở cả `http` và `server` | Bỏ bản trùng |

---

## 4. Thiết lập Upstream

### 4.1. Khai báo upstream

```nginx
upstream aives_backend {
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
    keepalive 64;
}
```

| Chỉ thị | Giá trị | Ý nghĩa |
|---|---|---|
| `upstream aives_backend` | tên nhóm | Tên này dùng trong `proxy_pass` |
| `server backend:8080` | 1 dòng cho **service name**, không lặp lại | Xem giải thích bên dưới |
| `resolve` | — | Tự làm mới danh sách IP theo TTL DNS |
| `max_fails=3` | 3 lần lỗi | Ngưỡng loại tạm khỏi vòng phân phối |
| `fail_timeout=10s` | 10 giây | Thời gian bị loại trước khi thử lại |
| `zone aives_backend 64k` | 64 KB | Vùng nhớ chia sẻ lưu trạng thái runtime |
| `keepalive 64` | 64 connection | Số connection tối đa giữ mở tới upstream |

### 4.2. Vì sao chỉ cần **1 dòng** `server` cho N replica?

Đây là điểm gây nhầm lẫn nhiều nhất.

Docker Embedded DNS trả về **nhiều bản ghi A** cho service đã scale:

```bash
$ nslookup backend
Address: 172.18.0.6    ← backend-1
Address: 172.18.0.7    ← backend-2
Address: 172.18.0.8    ← backend-3
```

Khi Nginx resolve `backend`, nó nhận cả 3 địa chỉ và **tự tạo server entry cho
mỗi địa chỉ** → 3 backend trong vòng phân phối → Round Robin hoạt động.

❌ **Cách làm SAI** (một số bài hướng dẫn trên mạng dùng):

```nginx
upstream aives_backend {
    server backend:8080;   # ← sai: chỉ tạo 1 entry
    server backend:8080;   # ← sai: trùng, Nginx cảnh báo "conflicting server"
    server backend:8080;
}
```

Nginx sẽ cảnh báo:

```
nginx: [emerg] conflicting server name "backend:8080" on 0.0.0.0:8080
```

hoặc chỉ giữ 1 entry → **không có load balancing**.

✅ **Cách đúng** (cấu hình hiện tại) — 1 dòng, Nginx tự mở rộng:

```nginx
upstream aives_backend {
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
}
```

### 4.3. `resolve` — trái tim của cấu hình scale được

**Vấn đề:** Nginx mặc định resolve hostname **1 lần duy nhất lúc khởi động**.

Kịch bản hỏng:

```text
T=0s   Nginx start → resolve "backend" → nhận IP 172.18.0.6, .7, .8
T=60s  backend-2 restart → Docker cấp IP MỚI 172.18.0.9
T=60s+ Nginx vẫn dùng IP CŨ 172.18.0.8 cho backend-2
        → "Host is unreachable" → 502 Bad Gateway
```

Đây không phải lý thuyết — trong quá trình làm đồ án, chính xác lỗi này xảy ra
và làm **mất 17–98 % request** trong các lượt đo có restart replica.

**Giải pháp:** `resolve` (yêu cầu **Nginx ≥ 1.27.3**, đang dùng **1.27.5**)

```nginx
resolver 127.0.0.11 valid=10s ipv6=off;

upstream aives_backend {
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
}
```

| Tham số | Ý nghĩa |
|---|---|
| `resolver 127.0.0.11` | Địa chỉ Docker Embedded DNS — **bắt buộc** |
| `valid=10s` | TTL: hỏi lại DNS mỗi 10 giây → replica mới vào vòng trong ≤10s |
| `ipv6=off` | Tắt AAAA lookup — Docker network không dùng IPv6, tránh chậm |

**Điều kiện bắt buộc khi dùng `resolve`:**

1. `resolver` phải ở `http` context → file `default.conf` được include trong
   `http {}` nên khai ở đầu file (ngoài `server {}`) là đúng.
2. Bắt buộc có `zone` → nếu thiếu, Nginx báo:
   ```
   nginx: [emerg] "resolve" parameter is only available with "zone" directive
   ```

### 4.4. `zone` — vùng bộ nhớ dùng chung

```nginx
zone aives_backend 64k;
```

Cho phép nhiều worker process của Nginx chia sẻ trạng thái của upstream
(danh sách IP đã resolve, cờ lỗi, số request đến từng server).

- **Bắt buộc** khi dùng `resolve` hoặc `sticky`.
- 64 KB là mức tối thiểu thực tế — đủ cho hàng trăm server.
- Không có `zone`, Nginx sẽ lưu trạng thái riêng trong mỗi worker → mỗi worker
  có danh sách IP khác nhau → phân phối không đều.

### 4.5. `max_fails` / `fail_timeout` — cơ chế loại replica chết

```nginx
server backend:8080 resolve max_fails=3 fail_timeout=10s;
```

**Cơ chế (health check ngầm — passive health check):**

```
Backend-2 bị kill
   ↓
Request thứ 1 tới backend-2 → lỗi connect → đếm fails = 1
Request thứ 2 tới backend-2 → lỗi connect → đếm fails = 2
Request thứ 3 tới backend-2 → lỗi connect → đếm fails = 3
   ↓
≥ max_fails (3) → backend-2 bị ĐÁNH DẤU unavailable trong fail_timeout (10s)
   ↓
Trong 10 giây, Nginx KHÔNG gửi request tới backend-2
   ↓
Sau 10 giây → Nginx thử lại backend-2 (fail_timeout tính từ lần thử cuối)
```

**Ý nghĩa thực tế:** trong khoảng 10 giây đầu, có tối đa 3 request có thể rơi.
Đó là đánh đổi giữa "phát hiện nhanh" và "mất ít request". Giảm `fail_timeout`
→ phát hiện nhanh hơn nhưng request bị rơi nhiều hơn; tăng → ngược lại.

### 4.6. `keepalive` — tái sử dụng connection

```nginx
upstream aives_backend {
    ...
    keepalive 64;
}

server {
    location / {
        proxy_http_version 1.1;
        proxy_set_header Connection "";
        ...
    }
}
```

**Vấn đề:** mặc định `proxy_http_version 1.0` và header `Connection: close`
→ **mỗi request tạo 1 TCP connection mới** tới backend. Ở tải cao, đây là
nguồn lãng phí lớn (TCP handshake + TLS handshake nếu có).

**Giải pháp:** 3 thay đổi phải đi cùng nhau:

| Thay đổi | Bắt buộc vì |
|---|---|
| `proxy_http_version 1.1` | HTTP/1.0 không hỗ trợ keep-alive |
| `proxy_set_header Connection ""` | Xoá `Connection: close` mặc định |
| `keepalive 64` (trong upstream) | Cho phép giữ tối đa 64 connection mở |

**Lưu ý:** `keepalive` trong upstream **không phải** là số request đồng thời —
đó là giới hạn connection *cache* của Nginx tới upstream.

### 4.7. Kiểm tra upstream đã nhận đủ replica

Docker Embedded DNS trả về **nhiều bản ghi A** cho service `backend`. Dùng
`nslookup` để xem đủ (lưu ý: `getent hosts` chỉ trả về 1 dòng đầu):

```powershell
docker exec aives-dev-nginx-1 sh -c "nslookup backend 2>&1"
```

Kết quả mong đợi với 3 replica:

```text
Non-authoritative answer:
Name:	backend
Address: 172.18.0.8
Name:	backend
Address: 172.18.0.6
Name:	backend
Address: 172.18.0.7
```

Nếu chỉ thấy **1 IP** → Nginx sẽ chỉ có 1 backend trong vòng phân phối
(cấu hình `resolve` sẽ tự mở rộng khi IP mới xuất hiện, nhưng nếu service
chưa scale thì đây là dấu hiệu scale chưa áp dụng).

```powershell
# Xem cấu hình upstream đang được Nginx nạp
docker exec aives-dev-nginx-1 sh -c "nginx -T 2>/dev/null | grep -A8 'upstream aives_backend'"
```

> **`nslookup` chạy được trong container nginx** vì image `nginx:1.27-alpine`
> có sẵn `busybox nslookup`. Trên host Windows, dùng lệnh tương đương:
> ```powershell
> docker run --rm --network aives-dev_default busybox nslookup backend
> ```

---

## 5. Cấu hình Load Balancing (Round Robin)

### 5.1. Thuật toán mặc định

**Round Robin là thuật toán mặc định của Nginx** — không cần khai directive nào.
Với 3 backend, chuỗi phân phối là:

```text
Request  1 → backend-1 (172.18.0.6)
Request  2 → backend-2 (172.18.0.7)
Request  3 → backend-3 (172.18.0.8)
Request  4 → backend-1 (172.18.0.6)   ← vòng lại
Request  5 → backend-2 (172.18.0.7)
Request  6 → backend-3 (172.18.0.8)
...
```

### 5.2. Cách bật/thay đổi thuật toán

#### Cách 1 — dùng file include riêng (cách đang dùng trong dự án)

Tách phần cấu hình thuật toán ra file riêng, mount vào `conf.d` cùng file chính:

```nginx
# monitoring/nginx/nginx-upstream-least-conn.conf
upstream aives_backend {
    least_conn;
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
    keepalive 64;
}
```

Khi đó file chính **không** khai `upstream` nữa (tránh trùng tên → Nginx báo
`duplicate upstream "aives_backend"`).

#### Cách 2 — chỉnh trực tiếp trong `nginx.conf`

```nginx
upstream aives_backend {
    least_conn;                          # ← thêm dòng này
    server backend:8080 resolve max_fails=3 fail_timeout=10s;
    zone aives_backend 64k;
    keepalive 64;
}
```

### 5.3. So sánh các thuật toán

| Thuật toán | Directive | Phân phối | Phù hợp khi |
|---|---|---|---|
| **Round Robin** ⭐ | *(mặc định)* | Luân phiên tuyệt đối | Backend đồng đều, request giống nhau |
| Weighted RR | `server backend:8080 weight=3;` | Theo trọng số | Backend khác cấu hình |
| Least Connections | `least_conn;` | Theo số connection đang mở | Thời gian xử lý chênh nhau |
| IP Hash | `ip_hash;` | Theo IP client | Cần sticky session |
| Random | `random;` (module) | Ngẫu nhiên | Không cần đảm bảo |

### 5.4. Tại sao chọn Round Robin cho AIVES?

| Lý do | Chi tiết |
|---|---|
| Đề bài yêu cầu | "Cấu hình Load Balancing (Round Robin)" |
| Backend đồng đều | Cùng image, cùng config, cùng pool → sức mạnh tương đương |
| Request giống nhau | Kịch bản k6: tất cả học sinh nộp bài cùng lúc |
| Dễ kiểm chứng | Phân phối phải ra **tỉ lệ 1:1:1** — dễ so với log |
| Không cần state | Không phụ thuộc IP hay số connection |

### 5.5. Ảnh hưởng của Round Robin đến hiệu năng

Đo thực tế trên 3 replica (mục 7):

| Mức tải | p95 (1 backend) | p95 (Nginx + 3 replica) | Cải thiện |
|---|---|---|---|
| 50 VUs | 3529 ms | **1512 ms** | −57.1 % |
| 100 VUs | 4133 ms | **3086 ms** | −25.3 % |
| 200 VUs | 9681 ms | **6485 ms** | −33.0 % |

Ở mức 50 VUs, **avg latency giảm từ 1130 ms → 619 ms (−45 %)** — đây là hiệu
ứng rõ nhất của việc mỗi backend chỉ phải xử lý 1/3 lượng request.

> **Lưu ý:** từ 200 VUs trở lên, cải thiện p95 bão hoà dần vì nút thắt chuyển
> sang **database** (connection pool PostgreSQL), không phải Nginx. Chi tiết ở
> [`docs/nginx-handover-thinh.md`](./nginx-handover-thinh.md) mục 9.

---

## 6. Chuẩn bị môi trường chạy thử

### 6.1. Yêu cầu

| Thành phần | Phiên bản |
|---|---|
| Docker Desktop | 29.7.2 (Windows) |
| Nginx | 1.27.5-alpine (**cần ≥ 1.27.3** cho `resolve`) |
| PostgreSQL | 16 (image `postgres:16-alpine`) |
| k6 | 0.53.0 (container `grafana/k6:0.53.0`) |

### 6.2. Khởi động stack

```powershell
Set-Location c:\Users\thinh\Documents\GitHub\AIVES_FPT

# 1) Scale backend lên 3 replica + bật Nginx
$env:POSTGRES_MAX_CONNECTIONS = "600"
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile local-db --profile scaling `
    up -d --scale backend=3 nginx

# 2) Chờ các container healthy
Start-Sleep -Seconds 50
docker ps --filter "name=aives-dev" --format "{{.Names}}`t{{.Status}}"
```

Kết quả mong đợi:

```
aives-dev-nginx-1        Up 50 seconds (healthy)
aives-dev-backend-1      Up 50 seconds (healthy)
aives-dev-backend-2      Up 50 seconds (healthy)
aives-dev-backend-3      Up 50 seconds (healthy)
aives-dev-postgres-1     Up 50 seconds (healthy)
```

### 6.3. ⚠️ Bẫy quan trọng: race condition khi seed

Nếu bật `Database__SeedOnStartup=true` cho **cả 3 replica cùng lúc**, cả 3 sẽ
chạy seeder song song → cùng INSERT `admin@aives.edu.vn` → vi phạm unique
constraint → app crash:

```
ERROR:  duplicate key value violates unique constraint "users_email_key"
DETAIL:  Key (email)=(admin@aives.edu.vn) already exists.
```

→ container exit code **139** (SIGSEGV do exception lúc khởi động).

**Cách xử lý đúng (đã tích hợp trong `run-matrix.ps1`):**

```powershell
# Bước 1: seed bằng DUY NHẤT 1 replica
$env:Database__SeedOnStartup = "true"
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile local-db --profile scaling up -d --scale backend=1 --force-recreate backend
Start-Sleep -Seconds 45

# Bước 2: scale lên N replica, TẮT seed
$env:Database__SeedOnStartup = "false"
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile local-db --profile scaling up -d --scale backend=3 --force-recreate backend nginx
```

### 6.4. Chuẩn bị dữ liệu test

```powershell
$env:LOADTEST_TEST_PASSWORD = "Dev@12345"

# Tạo 30 học sinh ảo + course + question + exam + session
.\monitoring\k6\prepare-demo-fixture.ps1 -Students 30 -Graders 3
```

Kết quả:

```
== AIVES demo fixture ==
  [ok] dang nhap admin + staff
  [ok] tao 30 hoc sinh ao (demo-20261106-135810-)
  [ok] course=1 question=1 exam=1
  [ok] session=9 da ghi danh 30 sinh vien
```

Fixture được ghi ra `monitoring/results/2026-11-06/nginx-demo-<runId>/fixture.json`
để k6 đọc vào.

> **Mỗi học sinh chỉ nộp được 1 lần/ca thi.** Nếu chạy lại k6 trên cùng
> session → tất cả request sẽ thất bại (checks = 0 %). Phải tạo session mới
> cho mỗi lượt chạy.

### 6.5. Chạy k6 qua Nginx

```powershell
.\monitoring\k6\run-nginx-demo.ps1 -Vus 30 -Label nginx -BaseUrl "http://nginx:8088"
```

Kết quả:

```
== k6 summary ==
  requests    : 90 (4.49 req/s)
  http failed : 0%
  checks      : 100%
  latency avg : 545.15 ms
  latency p95 : 1141.57 ms
```

---

## 7. Kiểm tra request được phân phối đến các Backend replicas

### 7.1. Cách 1 — đọc log Nginx (cách chính xác nhất)

`nginx.conf` đã cấu hình `log_format aives_upstream` có `$upstream_addr`, nên
log cho biết **chính xác** replica nào đã phục vụ mỗi request.

```powershell
# Xoá log cũ để chỉ đo lượt chạy hiện tại
docker exec aives-dev-nginx-1 sh -c ": > /var/log/nginx/aives-access.log"

# Chạy test
.\monitoring\k6\run-nginx-demo.ps1 -Vus 50 -Label roundrobin -BaseUrl "http://nginx:8088"

# Đếm số request theo từng backend
docker exec aives-dev-nginx-1 sh -c `
  "grep -o 'upstream=[0-9.]*:[0-9]*' /var/log/nginx/aives-access.log | sort | uniq -c | sort -rn"
```

### 7.2. Kết quả đo thực tế

**Lượt 50 VUs, 150 request, log sạch:**

```
50 upstream=172.18.0.6:8080
50 upstream=172.18.0.7:8080
50 upstream=172.18.0.8:8080
```

| Chỉ số | Giá trị |
|---|---|
| Tổng request | 150 |
| Phân phối | 50 / 50 / 50 |
| **Sai lệch** | **0 %** — chia đều tuyệt đối |
| Tỉ lệ lý thuyết | 150 ÷ 3 = 50 ✔ |

**Lượt 30 VUs sau khi phục hồi 1 replica (90 request):**

```
33 upstream=172.18.0.8:8080
33 upstream=172.18.0.7:8080
32 upstream=172.18.0.6:8080
```

→ sai lệch ≤ 1 request (1.1 %) — do request không đều (thời gian xử lý khác
nhau giữa các backend), **không phải** lỗi phân phối.

### 7.3. Cách 2 — đối chiếu IP với container

```powershell
# Map IP → tên container
docker inspect -f "{{.Name}} {{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" `
  aives-dev-backend-1 aives-dev-backend-2 aives-dev-backend-3
```

Kết quả:

```
/aives-dev-backend-1 172.18.0.6
/aives-dev-backend-3 172.18.0.7
/aives-dev-backend-2 172.18.0.8
```

→ đối chiếu với log ở mục 7.2 → xác nhận request được chia đều cho cả 3
container thật.

### 7.4. Cách 3 — theo dõi bằng `stub_status`

```powershell
docker exec aives-dev-nginx-1 sh -c "wget -qO- http://127.0.0.1:8088/nginx_status"
```

Kết quả:

```
Active connections: 2
server accepts handled requests
 604 604 1691
Reading: 0 Writing: 1 Waiting: 1
```

| Dòng | Ý nghĩa |
|---|---|
| `Active connections` | Số connection đang mở (đang đọc / đang ghi / đang chờ) |
| `accepts` | Tổng connection đã nhận |
| `handled` | Tổng connection đã xử lý (bằng `accepts` nghĩa là không bị drop) |
| `requests` | Tổng HTTP request đã xử lý |

> `stub_status` cho biết **tổng** của Nginx, không tách theo backend. Muốn
> tách theo từng replica thì dùng `$upstream_addr` trong log (cách 1) hoặc
> metric `upstream_addr` của Nginx Plus.

### 7.5. Cách 4 — Grafana (quan sát theo thời gian thực)

```powershell
docker compose -f monitoring/compose.yaml up -d prometheus grafana
```

Mở `http://localhost:3000/d/aives-nginx-lb` → panel
**"Request Rate qua Nginx"** và **"CPU của từng backend replica"**.

> **Lưu ý:** panel `CPU của từng backend replica` dùng metric của **cAdvisor**
> (`container_cpu_usage_seconds_total`) chứ không phải metric Nginx. Vì vậy nó
> phản ánh tải thực tế của container, độc lập với việc Nginx có phân phối hay
> không — dùng để **đối chiếu chéo** với log `$upstream_addr`.

### 7.6. Cách 5 — kiểm tra ngay từ host (không cần k6)

```powershell
# Gửi 9 request tuần tự → 3 request tới mỗi replica
1..9 | ForEach-Object { Invoke-WebRequest http://localhost:8088/api/health/db -UseBasicParsing | Out-Null }

docker exec aives-dev-nginx-1 sh -c `
  "grep -o 'upstream=[0-9.]*:[0-9]*' /var/log/nginx/aives-access.log | sort | uniq -c | sort -rn"
```

Kết quả mong đợi: `3 / 3 / 3`.

---

## 8. Kiểm tra lỗi khi một replica ngừng hoạt động

### 8.1. Mục tiêu kiểm chứng

Chứng minh rằng khi 1 trong 3 backend chết:
1. Request vẫn được phục vụ (không gián đoạn dịch vụ)
2. Nginx tự chuyển traffic sang các replica còn lại
3. Khi replica hồi phục, Nginx tự đưa nó trở lại vòng phân phối

### 8.2. Kịch bản thực hiện

```powershell
# Bước 0 — chuẩn bị
$env:LOADTEST_TEST_PASSWORD = "Dev@12345"
.\monitoring\k6\prepare-demo-fixture.ps1 -Students 30 -Graders 3
docker exec aives-dev-nginx-1 sh -c ": > /var/log/nginx/aives-access.log"

# Bước 1 — chạy k6, ĐỒNG THỜI dừng 1 replica giữa chừng
$job = Start-Job -ScriptBlock {
    Set-Location "c:\Users\thinh\Documents\GitHub\AIVES_FPT"
    $env:LOADTEST_TEST_PASSWORD = "Dev@12345"
    .\monitoring\k6\run-nginx-demo.ps1 -Vus 30 -Label failover -BaseUrl "http://nginx:8088"
}

Start-Sleep -Seconds 6          # ← đợi k6 đã gửi được vài request
docker stop aives-dev-backend-2 # ← GIỮA LÚC tải, dừng 1 replica

Wait-Job $job -Timeout 200 | Out-Null
Receive-Job $job
Remove-Job $job -Force

# Bước 2 — kiểm tra log
docker exec aives-dev-nginx-1 sh -c `
  "grep -o 'upstream=[0-9.]*:[0-9]*' /var/log/nginx/aives-access.log | sort | uniq -c | sort -rn"
docker exec aives-dev-nginx-1 sh -c `
  "grep -c ' 502 \| 504 \| 499 ' /var/log/nginx/aives-access.log"   # đếm lỗi

# Bước 3 — phục hồi
docker start aives-dev-backend-2
```

### 8.3. Kết quả đo thực tế

**Trong lúc 1 replica bị dừng (90 request tổng):**

```
43 upstream=172.18.0.7:8080    ← backend-3
42 upstream=172.18.0.6:8080    ← backend-1
 5 upstream=172.18.0.8:8080    ← backend-2 (chỉ nhận request trước lúc bị dừng)
```

| Chỉ số | Giá trị | Đánh giá |
|---|---|---|
| Tổng request | 90 | — |
| HTTP failed | **0 %** | ✅ Không mất request |
| Checks | **100 %** | ✅ Mọi request thành công |
| Số lỗi 502/504/499 trong log | **0** | ✅ Không có request nào rơi |
| Traffic sau khi dừng | 43 + 42 = 85 request chia đều 2 replica | ✅ Tự động chuyển hướng |

**Sau khi phục hồi (lượt chạy mới, 90 request):**

```
33 upstream=172.18.0.8:8080    ← backend-2 đã trở lại
33 upstream=172.18.0.7:8080
32 upstream=172.18.0.6:8080
```

→ backend-2 được đưa trở lại vòng phân phối **mà không cần restart Nginx**.

### 8.4. Phân tích cơ chế xử lý lỗi

```
T=0s    30 VUs bắt đầu nộp bài → phân phối 10 / 10 / 10

T=6s    docker stop aives-dev-backend-2
        ↓
        Backend-2 ngắt kết nối đột ngột
        ↓
        Request tới backend-2 bị lỗi connect
        ↓
        max_fails=3 → sau 3 lỗi, backend-2 bị đánh dấu unavailable
        ↓
        fail_timeout=10s → trong 10s, Nginx KHÔNG gửi request tới backend-2
        ↓
        Traffic tự động chia đều cho backend-1 và backend-3
        ↓
        0 request bị rơi (0 % lỗi, 100 % checks)

T=45s   docker start aives-dev-backend-2
        ↓
        Backend-2 khởi động lại, cùng IP 172.18.0.8 (Docker tái cấp trong
        dải đã cấp) — nhưng kể cả khi IP đổi, `resolve` sẽ tự cập nhật
        ↓
        Sau fail_timeout, Nginx thử lại backend-2 → thành công
        ↓
        Trong lượt chạy kế tiếp: 33 / 33 / 32 — backend-2 đã trở lại
```

### 8.5. ⚠️ Nếu KHÔNG có `resolve` — điều gì xảy ra?

Đây là lỗi đã gặp thực tế trong quá trình làm đồ án:

```text
Bước 1: Nginx start, resolve "backend" → 172.18.0.6, .7, .8
Bước 2: docker stop aives-dev-backend-2
Bước 3: docker start aives-dev-backend-2
        → Docker cấp IP MỚI, ví dụ 172.18.0.11
Bước 4: Nginx vẫn giữ 172.18.0.8 cho backend-2 (đã bị đổi)
        → mọi request tới "backend-2" → "Host is unreachable"
        → 502 Bad Gateway
```

**Kết quả quan sát được:** mất **17–98 %** request tùy lúc restart.

**Với `resolve` + `valid=10s`:** IP mới được cập nhật trong ≤ 10 giây, không
cần restart Nginx.

### 8.6. Bảng tổng hợp cơ chế bảo vệ

| Cơ chế | Chống lỗi gì | Tham số | Bật/tắt |
|---|---|---|---|
| `resolve` | Replica đổi IP sau restart | `valid=10s` | ✅ Bật |
| `max_fails` | Replica chết / quá tải | `max_fails=3` | ✅ Bật |
| `fail_timeout` | Thời gian loại replica chết | `fail_timeout=10s` | ✅ Bật |
| `keepalive` | Tránh mở connection mới liên tục | `keepalive 64` | ✅ Bật |
| `proxy_connect_timeout` | Treo khi connect backend chết | `10s` | ✅ Bật |
| Healthcheck container | Backend không còn phản hồi health | `interval=10s, retries=6` | ✅ Bật |

### 8.7. Kiểm tra sau khi phục hồi

```powershell
# Replica đã healthy lại chưa?
docker ps --filter "name=aives-dev-backend" --format "{{.Names}}`t{{.Status}}"

# Nginx đã cập nhật IP mới chưa?
docker exec aives-dev-nginx-1 sh -c "nslookup backend 2>&1"

# Health tổng thể qua Nginx
Invoke-RestMethod http://localhost:8088/api/health/db
```

---

## 9. Giám sát trên Grafana

### 9.1. Thành phần

| Thành phần | File | Vai trò |
|---|---|---|
| Nginx exporter | `monitoring/compose.yaml` | Đọc `/nginx_status` → xuất metric Prometheus |
| Prometheus | `monitoring/prometheus.yml` | Scrape exporter, cAdvisor, postgres-exporter |
| Dashboard | `monitoring/nginx-dashboard.json` | 12 panel trực quan hoá |
| Script provision | `monitoring/grafana/setup-dashboard.py` | Tự đăng ký datasource + dashboard |

### 9.2. Chuỗi scrape

```
nginx:8088/nginx_status
   ↓ (nginx-prometheus-exporter --nginx.scrape-uri)
nginx-prometheus-exporter:9113/metrics
   ↓ (Prometheus scrape, mỗi 15s)
prometheus:9090
   ↓ (datasource cg393kpfzb2f4b)
grafana:3000
```

Cấu hình exporter:

```yaml
nginx-prometheus-exporter:
  image: nginx/nginx-prometheus-exporter:1.4.1
  command:
    - --nginx.scrape-uri=http://nginx:8088/nginx_status
  networks:
    - aives      # external: aives-dev_default — dùng chung với project aives-dev
```

### 9.3. Các panel trong dashboard

| # | Panel | Loại | Metric |
|---|---|---|---|
| 1 | Nginx Up | stat | `up{job="nginx"}` |
| 2 | Active Connections | stat | `nginx_connections_active` |
| 3 | Total Requests | stat | `nginx_http_requests_total` |
| 4 | Request Rate (req/s) | stat | `rate(nginx_http_requests_total[5m])` |
| 5 | Backend Replicas | stat | `count(container_last_seen{name=~"aives-dev-backend.*"})` |
| 6 | Postgres Active Connections | stat | `pg_stat_activity_count` |
| 7 | Request Rate qua Nginx | timeseries | `rate(nginx_http_requests_total[1m])` |
| 8 | Nginx Connections theo trạng thái | timeseries | `nginx_connections_reading/writing/waiting` |
| 9 | CPU của từng backend replica | timeseries | cAdvisor `container_cpu_usage_seconds_total` |
| 10 | Memory của từng backend replica | timeseries | cAdvisor `container_memory_working_set_bytes` |
| 11 | Network I/O của Nginx | timeseries | cAdvisor network metrics |
| 12 | Postgres — số kết nối đang hoạt động | timeseries | `pg_stat_activity_count` |

### 9.4. Truy cập

| Thành phần | URL | Tài khoản |
|---|---|---|
| Grafana | http://localhost:3000/d/aives-nginx-lb | `admin` / `admin` |
| Prometheus | http://localhost:9090 | — |
| Nginx (API) | http://localhost:8088 | — |
| Nginx status | http://localhost:8088/nginx_status | chỉ nội bộ Docker |

### 9.5. Đối chiếu Grafana ↔ log

| Câu hỏi | Xem ở đâu |
|---|---|
| Nginx có sống không? | Panel 1 (Nginx Up) |
| Tổng request đã xử lý? | Panel 3 (Total Requests) |
| Phân phối có đều không? | Log `$upstream_addr` (mục 7.1) + panel 9/10 |
| Có replica nào quá tải? | Panel 9 (CPU) |
| Database có bị nghẽn? | Panel 12 (Postgres connections) |
| Số connection đang chờ? | Panel 8 |

---

## 10. Bảng tham chiếu nhanh

### 10.1. Lệnh thường dùng

```powershell
# ── Khởi động ──────────────────────────────────────────────
$env:POSTGRES_MAX_CONNECTIONS = "600"
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile local-db --profile scaling up -d --scale backend=3 nginx

# ── Kiểm tra ───────────────────────────────────────────────
docker ps --filter "name=aives-dev" --format "{{.Names}}`t{{.Status}}"
docker exec aives-dev-nginx-1 nginx -t
docker exec aives-dev-nginx-1 sh -c "nslookup backend 2>&1"
Invoke-RestMethod http://localhost:8088/api/health/db

# ── Test cấu hình ──────────────────────────────────────────
docker exec aives-dev-nginx-1 sh -c `
  "grep -o 'upstream=[0-9.]*:[0-9]*' /var/log/nginx/aives-access.log | sort | uniq -c | sort -rn"
docker exec aives-dev-nginx-1 sh -c `
  "grep -c ' 502 \| 504 \| 499 ' /var/log/nginx/aives-access.log"

# ── Failover ───────────────────────────────────────────────
docker stop aives-dev-backend-2
docker start aives-dev-backend-2

# ── Reload ─────────────────────────────────────────────────
docker compose -f docker-compose.yml -f docker-compose.scaling.yml `
    --profile scaling exec nginx nginx -s reload

# ── Dừng ───────────────────────────────────────────────────
docker compose -f docker-compose.yml -f docker-compose.scaling.yml --profile scaling down
docker compose up -d
```

### 10.2. Đối chiếu IP ↔ container

```powershell
docker inspect -f "{{.Name}} {{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" `
  aives-dev-backend-1 aives-dev-backend-2 aives-dev-backend-3
```

### 10.3. Tổng hợp kết quả kiểm chứng

| Hạng mục | Kết quả | Nguồn |
|---|---|---|
| Phân phối Round Robin (50 VU) | 50 / 50 / 50 — sai lệch **0 %** | log `$upstream_addr` |
| Phân phối sau phục hồi (30 VU) | 33 / 33 / 32 — sai lệch ≤ 1.1 % | log `$upstream_addr` |
| Failover — request thất bại | **0 / 90** (0 %) | k6 `http_req_failed` |
| Failover — checks | **100 %** | k6 `checks` |
| Failover — lỗi 502/504/499 | **0** | grep log |
| Failover — traffic sau khi dừng | 43 + 42 trên 2 replica còn lại | log |
| Phục hồi — backend-2 trở lại | 33 request, **không cần restart Nginx** | log |
| Cải thiện p95 (50 VU) | 3529 ms → 1512 ms (**−57.1 %**) | `docs/nginx-loadtest-results.md` |
| Cải thiện p95 (100 VU) | 4133 ms → 3086 ms (**−25.3 %**) | `docs/nginx-loadtest-results.md` |
| Cải thiện p95 (200 VU) | 9681 ms → 6485 ms (**−33.0 %**) | `docs/nginx-loadtest-results.md` |

### 10.4. Script tự động hoá

```powershell
# Chạy đầy đủ 1 lệnh: reset DB → seed → scale → ma trận → báo cáo
$env:LOADTEST_TEST_PASSWORD = "Dev@12345"
.\monitoring\k6\run-matrix.ps1 -VusList @(50,100,200) -Replicas 3 -ResetDb
```

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `-VusList` | `50,100,200` | Các mức tải cần đo — `@(50)`, `@(100,200)`, `@(50,100,200,500)` |
| `-Replicas` | `3` | Số backend replica — `@(2)`, `@(5)`… |
| `-Variants` | `direct,nginx` | `-Variants nginx` để chỉ đo qua Nginx |
| `-ResetDb` | tắt | Xoá & tạo lại DB (để số liệu so sánh được) |
| `-SkipFixture` | tắt | Dùng lại dữ liệu ảo đã có |
| `-SkipScale` | tắt | Không scale lại replica |

### 10.5. Tuning connection pool (khi đổi số replica)

**Quy tắc:** `Số replica × Maximum Pool Size ≤ max_connections của PostgreSQL`

Hiện tại trong `docker-compose.scaling.yml`:

```yaml
ConnectionStrings__DefaultConnection: >-
  Host=postgres;Database=aives_dev;Username=aives;Password=aives-local-development;
  Maximum Pool Size=190;Minimum Pool Size=5;Timeout=60;Command Timeout=120
POSTGRES_MAX_CONNECTIONS: "600"
```

| Số replica | Maximum Pool Size nên đặt | Tổng | max_connections |
|---|---|---|---|
| 1 | 100 | 100 | 250 |
| 3 | 190 | 570 | 600 ✅ (hiện tại) |
| 5 | 110 | 550 | 600 |
| 10 | 55 | 550 | 600 |

> Nếu đổi `-Replicas`, **phải sửa lại cả 2 giá trị** trên, nếu không sẽ gặp
> `Npgsql.NpgsqlException: The connection pool has been exhausted`.

---

## 11. Xử lý sự cố

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `502 Bad Gateway` khi truy cập | Không có backend nào healthy | `docker ps` → xem backend; `docker logs aives-dev-backend-1` |
| `413 Request Entity Too Large` | Body > 1 MB | Đã nới `client_max_body_size 50m` |
| `504 Gateway Timeout` | Backend phản hồi > `proxy_read_timeout` | Đã nới 360s |
| `no resolver defined...` | `resolver` khai sai context | Đặt ở `http` context (đầu file) |
| `"resolve" parameter is only available with "zone"` | Thiếu `zone` | Thêm `zone aives_backend 64k;` |
| `conflicting server name "backend:8080"` | Lặp dòng `server` trong upstream | Chỉ giữ 1 dòng `server backend:8080 resolve ...` |
| Mất một phần request sau khi restart replica | Thiếu `resolve` | Bổ sung `resolve` + `zone` + `resolver` |
| `The connection pool has been exhausted` | Thiếu connection khi scale | Tăng `Maximum Pool Size` / `max_connections` (mục 10.5) |
| `duplicate key users_email_key` | Nhiều replica seed đồng thời | Seed bằng 1 replica trước (mục 6.3) |
| `403 Forbidden` khi gọi `/nginx_status` | IP exporter ngoài `172.16.0.0/12` | Thêm `allow` cho dải IP Docker |
| Log không có trường `upstream=` | Đang xem log mặc định của Nginx | Xem `/var/log/nginx/aives-access.log` (có format riêng) |

---

## Phụ lục — Tài liệu liên quan

| Tài liệu | Nội dung |
|---|---|
| [`monitoring/nginx/nginx.conf`](../../monitoring/nginx/nginx.conf) | **Deliverable** — cấu hình Nginx đầy đủ |
| [`docker-compose.scaling.yml`](../../docker-compose.scaling.yml) | Override cho chế độ scale nhiều replica |
| [`monitoring/nginx-dashboard.json`](../../monitoring/nginx-dashboard.json) | Grafana dashboard 12 panel |
| [`monitoring/k6/nginx-submit.js`](../../monitoring/k6/nginx-submit.js) | Kịch bản k6 |
| [`monitoring/k6/run-matrix.ps1`](../../monitoring/k6/run-matrix.ps1) | Script chạy ma trận load test |
| [`docs/nginx-loadtest-results.md`](./nginx-loadtest-results.md) | Báo cáo kết quả load test |
| [`docs/nginx-demo-quickstart.md`](./nginx-demo-quickstart.md) | Hướng dẫn dùng nhanh |
| [`docs/nginx-handover-thinh.md`](./nginx-handover-thinh.md) | Bàn giao toàn bộ + phân tích nút thắt database |
