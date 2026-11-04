# AIVES – Observability & Monitoring Results

## 1. Kiến trúc giám sát

Ba dịch vụ ứng dụng `frontend`, `backend` và `ai-service` chạy trong Docker Compose project `aives-dev`. cAdvisor đọc số liệu tài nguyên của các container Docker và cung cấp endpoint `/metrics`. Prometheus thu thập endpoint này; Grafana dùng Prometheus làm nguồn dữ liệu để hiển thị bốn chuỗi thời gian CPU, RAM và lưu lượng mạng. Các phép đo trong báo cáo là số liệu container, không phải phép đo thời gian đáp ứng HTTP của người dùng.

## 2. Cấu hình giám sát

| Thành phần | Cấu hình đã kiểm tra |
|---|---|
| Ứng dụng | `aives-dev-frontend-1`, `aives-dev-backend-1`, `aives-dev-ai-service-1`; cả ba ở trạng thái `healthy` khi kiểm tra |
| Monitoring Compose | `monitoring/compose.yaml`; project `aives-monitoring`; cAdvisor, Prometheus và Grafana cùng tham gia Docker network ngoài `aives-dev_default` |
| cAdvisor | `cadvisor:8080`, công bố tại `http://localhost:8081`; container `healthy` |
| Prometheus | `prometheus:9090`, công bố tại `http://localhost:9090`; `scrape_interval: 5s`; job `aives-containers` scrape `cadvisor:8080`, job `prometheus` scrape `prometheus:9090` |
| Trạng thái target | API `/api/v1/targets` trả về `health: up` cho cả `aives-containers` và `prometheus` vào ngày quan sát |
| Grafana | `http://localhost:3000`; dashboard [AIVES – Infrastructure Monitoring](http://localhost:3000/d/adncptn/aives-e28093-infrastructure-monitoring), UID `adncptn`, phiên bản nguồn 9; Prometheus datasource UID `cg393kpfzb2f4b` |

Dashboard hiện có bốn panel time series: `AIVES – Memory Usage`, `AIVES – CPU Usage.`, `AIVES – Network Receive` và `Network Transmit`. Mỗi panel lọc `container_label_com_docker_compose_project="aives-dev"` cùng `container_label_com_docker_compose_service=~"frontend|backend|ai-service"`, gom theo nhãn service và dùng legend `{{container_label_com_docker_compose_service}}`. Cả ba service xuất hiện ở cả bốn truy vấn.

JSON nguồn được đọc từ Grafana API `/api/dashboards/uid/adncptn` trong phiên trình duyệt đã đăng nhập. File xuất giữ nguyên cấu trúc dashboard, truy vấn, tiêu đề, legend, loại biểu đồ và datasource. Các khác biệt so với yêu cầu đơn vị hiển thị: panel Network Receive chưa khai báo `unit`; Network Transmit dùng `bytes` mặc dù truy vấn trả về bytes/giây. Chỉ trong file JSON xuất, hai panel mạng được đặt `Bps` (Grafana bytes/sec theo IEC). Dashboard đang chạy không bị sửa. Trường `id` được đặt `null` để có thể nhập dashboard vào Grafana; UID nguồn được giữ lại.

## 3. Giám sát baseline

- Ngày: **04/11/2026**, múi giờ **Asia/Ho_Chi_Minh (UTC+07:00)**.
- Cửa sổ quan sát: **13:52:37–14:00:37**, đúng **8 phút** (tương ứng 06:52:37–07:00:37 UTC; Unix `1793775157`–`1793775637`).
- Môi trường: Docker Desktop trên Windows; ba container ứng dụng và ba container monitoring đang chạy. Không chủ động chạy bài kiểm thử tải trong cửa sổ này; các truy cập thông thường/health check có thể vẫn xảy ra.
- Phương pháp: Prometheus HTTP API `GET /api/v1/query_range`, `start=1793775157`, `end=1793775637`, `step=15s`. Mỗi ô là trung bình cộng của **33 mẫu** từ truy vấn đã `sum by (container_label_com_docker_compose_service)`. CPU và mạng dùng `rate(...[1m])` tại từng mẫu; RAM dùng `container_memory_working_set_bytes` tại từng mẫu. Không lấy trung bình trực tiếp từ counter tích lũy.

Selector chung:

```promql
{container_label_com_docker_compose_project="aives-dev",container_label_com_docker_compose_service=~"frontend|backend|ai-service"}
```

Các biểu thức truy vấn lần lượt là:

```promql
sum by (container_label_com_docker_compose_service) (rate(container_cpu_usage_seconds_total{container_label_com_docker_compose_project="aives-dev",container_label_com_docker_compose_service=~"frontend|backend|ai-service"}[1m]))
sum by (container_label_com_docker_compose_service) (container_memory_working_set_bytes{container_label_com_docker_compose_project="aives-dev",container_label_com_docker_compose_service=~"frontend|backend|ai-service"})
sum by (container_label_com_docker_compose_service) (rate(container_network_receive_bytes_total{container_label_com_docker_compose_project="aives-dev",container_label_com_docker_compose_service=~"frontend|backend|ai-service"}[1m]))
sum by (container_label_com_docker_compose_service) (rate(container_network_transmit_bytes_total{container_label_com_docker_compose_project="aives-dev",container_label_com_docker_compose_service=~"frontend|backend|ai-service"}[1m]))
```

Quy đổi: MiB = bytes / 1.048.576; KiB/s = bytes/giây / 1.024. Giá trị được làm tròn khi trình bày; các giá trị nhận trực tiếp từ Prometheus trước quy đổi lần lượt theo thứ tự Frontend / Backend / AI Service là CPU `0.0023172085 / 0.0159603301 / 0.0110321520` cores; RAM `13661649.4545 / 349725789.0909 / 49197924.8485` bytes; Receive `0.0917141878 / 2837.8724569757 / 0.0929687794` bytes/s; Transmit `0 / 3558.3711741324 / 0` bytes/s.

| Chỉ số trung bình | Frontend | Backend | AI Service |
|---|---:|---:|---:|
| CPU (cores) | 0,00232 | 0,01596 | 0,01103 |
| RAM (MiB) | 13,03 | 333,52 | 46,92 |
| Network Receive (KiB/s) | 0,00009 | 2,77136 | 0,00009 |
| Network Transmit (KiB/s) | 0,00000 | 3,47497 | 0,00000 |

## 4. Nhận xét

Trong cửa sổ quan sát, backend dùng RAM và CPU trung bình cao nhất trong ba dịch vụ: khoảng 333,52 MiB và 0,01596 core. AI Service đứng thứ hai ở cả hai chỉ số, còn frontend thấp nhất. Lưu lượng mạng đo được tập trung ở backend (nhận 2,77136 KiB/s; truyền 3,47497 KiB/s). Lưu lượng của frontend và AI Service rất thấp ở baseline này; Transmit trung bình của hai dịch vụ bằng 0 trong các mẫu Prometheus trả về. Những số liệu này mô tả trạng thái chạy bình thường của cửa sổ đã chọn, chưa đủ cơ sở kết luận nghẽn hiệu năng hoặc khả năng chịu tải.

## 5. Kiểm thử tải

- Ngày chạy: **04/11/2026**, múi giờ **Asia/Ho_Chi_Minh (UTC+07:00)**.
- Môi trường: Docker Compose `aives-dev`, backend/API, AI service, frontend và PostgreSQL 16 đều chạy local. Backend connection string trỏ tới `Host=postgres;Database=aives_dev`; **không sử dụng Neon**.
- Công cụ và kịch bản: k6, `monitoring/k6/health-db.js`, mỗi mức chạy 60 giây danh định với các mức 50/100/200/500 VUs. Mỗi VU gọi `GET /api/health/db`, kiểm tra HTTP 200 và `databaseConnected=true`, sau đó nghỉ một giây.
- CPU, RAM working set và Network Receive/Transmit được lấy từ Prometheus query range trong cửa sổ từng lần chạy, trung bình mẫu 5 giây; bảng dưới tóm tắt tài nguyên của **backend**. Đơn vị mạng là KiB/s.
- Tỷ lệ HTTP lỗi và tỷ lệ kiểm tra database được tách riêng: endpoint có thể trả HTTP 200 dù `databaseConnected` là `false`.

| VUs | Thời gian chạy (local) | Avg latency | P95 | HTTP lỗi | `databaseConnected` fail | Throughput | Backend CPU avg | Backend RAM avg | Backend RX/TX avg |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 50 | 15:04:09–15:05:09 | 25,57 ms | 51,61 ms | 0% | 0 / 2.950 (0%) | 48,44 req/s | 0,364 core | 262,20 MiB | 46,71 / 51,12 KiB/s |
| 100 | 15:05:47–15:06:49 | 31,33 ms | 43,52 ms | 0% | 0 / 5.844 (0%) | 95,78 req/s | 0,611 core | 225,27 MiB | 86,37 / 93,90 KiB/s |
| 200 | 15:07:24–15:08:27 | 65,32 ms | 309,21 ms | 0% | 0 / 11.332 (0%) | 185,73 req/s | 1,285 core | 166,22 MiB | 178,23 / 191,52 KiB/s |
| 500 | 15:09:13–15:10:18 | 908,88 ms | 1.873,85 ms | 0% | **504 / 16.034 (3,14%)** | 256,24 req/s | 1,778 core | 424,77 MiB | 246,60 / 251,69 KiB/s |

Ở mức 500 VUs, 504 lượt kiểm tra `databaseConnected` thất bại; hai check HTTP 200 vẫn thành công và tỷ lệ HTTP lỗi của k6 bằng 0%. Vì vậy chỉ số HTTP lỗi không phản ánh các lần database check này. Đây là dấu hiệu nghẽn/gián đoạn kết nối database dưới tải trong lần chạy này, dù health endpoint vẫn trả HTTP 200. Backend và PostgreSQL đều healthy lại sau khi bài test kết thúc; health check cuối cùng xác nhận `databaseConnected=true`.

Mỗi mức tải có k6 summary JSON, log đầy đủ và Prometheus metrics JSON trong thư mục tương ứng. Ảnh dashboard được chụp trong lúc chạy tải:

| VUs | Grafana |
|---:|---|
| 50 | [grafana.png](./results/2026-11-04/50/grafana.png) |
| 100 | [grafana.png](./results/2026-11-04/100/grafana.png) |
| 200 | [grafana.png](./results/2026-11-04/200/grafana.png) |
| 500 | [grafana.png](./results/2026-11-04/500/grafana.png) |

Tóm tắt máy đọc được: [summary.csv](./results/2026-11-04/summary.csv), [summary.json](./results/2026-11-04/summary.json). Các giá trị theo từng mức: `k6-summary.json`, `k6-output.txt`, `prometheus-metrics.json`, `start-epoch.txt`, `end-epoch.txt`.

### 5.1 Kiểm thử Member 3 – đăng nhập và nộp câu trả lời Viva

- Ngày chạy: **04/11/2026**, múi giờ **Asia/Ho_Chi_Minh (UTC+07:00)**.
- Database: PostgreSQL local trong Docker, database riêng `aives_loadtest`; đã xác minh backend kết nối `Host=postgres;Database=aives_loadtest`. Không dùng Neon.
- Tạo 850 tài khoản test mới theo từng lần chạy, chia các nhóm không trùng nhau cho 50/100/200/500 VUs. Mỗi VU được đăng nhập và bắt đầu phiên thi tuần tự trong `setup()`; phần chuẩn bị này không nằm trong khoảng submit đồng thời được đo. Mỗi VU gửi một câu trả lời text tới endpoint submit.
- Để bài 500 VUs không vượt giới hạn kết nối PostgreSQL mặc định, cấu hình riêng khi chạy fixture local: `max_connections=250`, Npgsql pool tối đa 200 và login limit 2000. Mặc định PostgreSQL và login limiter không đổi nếu không bật các biến cấu hình này.
- K6 đo thời gian trực tiếp của request `submit-answer`; Prometheus lấy trung bình các mẫu backend quanh cửa sổ submit. Scrape interval là 5 giây nên số mẫu tài nguyên trong mỗi cửa sổ ngắn chỉ 3–4; dùng để đối chiếu xu hướng, không coi là phép đo tải kéo dài.

| VUs | Thời điểm submit (local) | Submit trung bình | Submit P95 | Được chấp nhận | HTTP lỗi | Backend CPU trung bình | Backend RAM trung bình | RX / TX trung bình |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 50 | 15:53:01–15:53:01 | 1.820,09 ms | 1.922,96 ms | 50/50 | 0% | 0,4702 core | 949,32 MiB | 91,47 / 25,36 KiB/s |
| 100 | 15:53:44–15:53:47 | 2.206,06 ms | 2.736,66 ms | 100/100 | 0% | 1,0432 core | 1.161,79 MiB | 450,07 / 83,94 KiB/s |
| 200 | 15:55:03–15:55:07 | 3.357,09 ms | 4.122,93 ms | 200/200 | 0% | 0,9149 core | 1.170,79 MiB | 729,71 / 67,31 KiB/s |
| 500 | 15:58:31–15:58:37 | 20.038,22 ms | 21.823,20 ms | 500/500 | 0% | 1,5773 core | 886,76 MiB | 5.144,56 / 196,62 KiB/s |

Một lần chạy chẩn đoán trước đó chạm giới hạn 100 kết nối của PostgreSQL local và không được tính vào bảng. Sau khi tăng giới hạn riêng cho profile load-test, đã tạo fixture/tài khoản mới và chạy lại cả bốn mức; toàn bộ câu trả lời đều được API chấp nhận.

Kết quả cuối cùng, log k6, raw JSONL, số liệu Prometheus và ảnh dashboard theo từng profile:

| VUs | Kết quả | Grafana |
|---:|---|---|
| 50 | [k6-summary.json](./results/2026-11-04/member3-submit-20261104-155042/50/k6-summary.json) · [k6-raw.jsonl](./results/2026-11-04/member3-submit-20261104-155042/50/k6-raw.jsonl) · [prometheus-range.json](./results/2026-11-04/member3-submit-20261104-155042/50/prometheus-range.json) | [grafana-dashboard.png](./results/2026-11-04/member3-submit-20261104-155042/50/grafana-dashboard.png) |
| 100 | [k6-summary.json](./results/2026-11-04/member3-submit-20261104-155042/100/k6-summary.json) · [k6-raw.jsonl](./results/2026-11-04/member3-submit-20261104-155042/100/k6-raw.jsonl) · [prometheus-range.json](./results/2026-11-04/member3-submit-20261104-155042/100/prometheus-range.json) | [grafana-dashboard.png](./results/2026-11-04/member3-submit-20261104-155042/100/grafana-dashboard.png) |
| 200 | [k6-summary.json](./results/2026-11-04/member3-submit-20261104-155042/200/k6-summary.json) · [k6-raw.jsonl](./results/2026-11-04/member3-submit-20261104-155042/200/k6-raw.jsonl) · [prometheus-range.json](./results/2026-11-04/member3-submit-20261104-155042/200/prometheus-range.json) | [grafana-dashboard.png](./results/2026-11-04/member3-submit-20261104-155042/200/grafana-dashboard.png) |
| 500 | [k6-summary.json](./results/2026-11-04/member3-submit-20261104-155042/500/k6-summary.json) · [k6-raw.jsonl](./results/2026-11-04/member3-submit-20261104-155042/500/k6-raw.jsonl) · [prometheus-range.json](./results/2026-11-04/member3-submit-20261104-155042/500/prometheus-range.json) | [grafana-dashboard.png](./results/2026-11-04/member3-submit-20261104-155042/500/grafana-dashboard.png) |

Bảng tổng hợp máy đọc được: [summary.csv](./results/2026-11-04/member3-submit-20261104-155042/summary.csv) · [summary.json](./results/2026-11-04/member3-submit-20261104-155042/summary.json). Fixture và thời điểm đo chi tiết được lưu tại `fixture.json` và `run-metadata.json` trong từng thư mục profile.

## 6. So sánh trước/sau mở rộng – chờ Thành viên 4

| Chỉ số | Trước mở rộng | Sau mở rộng |
|---|---|---|
| Số replica backend | Pending – Member 4 | Pending – Member 4 |
| CPU và RAM | Pending – Member 4 | Pending – Member 4 |
| Lưu lượng mạng | Pending – Member 4 | Pending – Member 4 |
| Thời gian đáp ứng trung bình | Pending – Member 4 | Pending – Member 4 |
| Độ trễ P95 | Pending – Member 4 | Pending – Member 4 |
| Tỷ lệ lỗi | Pending – Member 4 | Pending – Member 4 |
| Thông lượng | Pending – Member 4 | Pending – Member 4 |

## 7. Sản phẩm bàn giao

- `monitoring/aives-dashboard.json`: cấu hình xuất từ dashboard Grafana hiện có, bốn panel, datasource Prometheus UID `cg393kpfzb2f4b`; đơn vị hai panel mạng đã chỉnh trong artifact.
- `monitoring/monitoring-results.md`: cấu hình giám sát, baseline và kết quả kiểm thử tải 50/100/200/500 VUs.
- Dashboard đang chạy: [AIVES – Infrastructure Monitoring](http://localhost:3000/d/adncptn/aives-e28093-infrastructure-monitoring).
- Kết quả k6, Prometheus và ảnh dashboard theo mức tải: `monitoring/results/2026-11-04/`.

## 8. Kết luận

Phần Observability có stack cAdvisor–Prometheus–Grafana hoạt động, hai scrape targets `UP`, dashboard bốn panel và baseline tám phút từ dữ liệu Prometheus. Đã chạy k6 50/100/200/500 VUs với PostgreSQL local và lưu kết quả cùng ảnh Grafana. So sánh trước/sau mở rộng backend vẫn chờ số liệu sau scaling.
