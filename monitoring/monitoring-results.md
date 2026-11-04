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

## 5. Kiểm thử tải – chờ Thành viên 3

| Người dùng ảo | Thời gian đáp ứng trung bình | P95 | Tỷ lệ lỗi | Thông lượng | CPU/RAM/mạng dưới tải |
|---:|---|---|---|---|---|
| 50 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 |
| 100 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 |
| 200 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 |
| 500 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 | Pending – Member 3 |

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
- `monitoring/monitoring-results.md`: cấu hình giám sát, phương pháp đo, kết quả baseline thực tế và các bảng chờ kiểm thử tải/mở rộng.
- Dashboard đang chạy: [AIVES – Infrastructure Monitoring](http://localhost:3000/d/adncptn/aives-e28093-infrastructure-monitoring).
- Ảnh chụp màn hình: chưa tạo; không có file ảnh nào được liệt kê là sản phẩm bàn giao.

## 8. Kết luận

Phần Observability của Thành viên 2 đã có stack cAdvisor–Prometheus–Grafana hoạt động, target cAdvisor `UP`, dashboard bốn panel được xuất thành JSON và baseline tám phút được tính từ dữ liệu Prometheus thật. Các kết quả 50/100/200/500 người dùng ảo còn chờ Thành viên 3; so sánh trước/sau mở rộng backend còn chờ Thành viên 4.
