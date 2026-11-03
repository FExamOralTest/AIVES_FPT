# Nội dung Transcript & Tổng hợp Chi tiết từ Audio File: Phuong_record_03_11.m4a

---

### **1. Tiêu chí Đánh giá & Định hướng Chấm điểm Môn Kiến trúc Phần mềm**
* **Tập trung vào Kiến trúc & Ghép nối Thành phần:** Giảng viên nhấn mạnh môn Kiến trúc phần mềm không chấm theo kiểu kiểm tra (validate) tỉ mỉ từng màn hình hay thẩm mỹ giao diện, mà tập trung vào **cách thiết kế hệ thống thô và sự ghép nối giữa các thành phần (components)**.
* **Đạt mục tiêu cốt lõi:** Khi nhóm đã phân chia hệ thống thành các module/phòng chức năng rõ ràng và kết nối chúng vận hành thông suốt với nhau là đã đạt yêu cầu cơ bản của môn học.

---

### **2. Cấu hình Giám sát Hạ tầng Hệ thống (System Monitoring with Grafana)**
* **Yêu cầu đo đạc trực quan:** Giảng viên yêu cầu nhóm cài đặt và cấu hình công cụ giám sát như **Grafana** để theo dõi hạ tầng hệ thống khi vận hành.
* **Đo đạc trước khi tối ưu:** Hệ thống cần hiển thị trực quan lưu lượng dữ liệu trao đổi, tài nguyên tiêu thụ (CPU, RAM, Bandwidth). Giảng viên đưa ra nguyên tắc: *"Không đo đạc được thì không có căn cứ để tối ưu hệ thống"*.

---

### **3. Thử nghiệm Khả năng Chịu tải (Stress Testing & Load Simulation)**
* **Bài toán 500 Thí sinh nộp bài đồng thời:** Đặt ra kịch bản thực tế khi tổ chức thi diện rộng: 500 sinh viên cùng nhấn nút nộp bài ở cuối ca thi khiến Server bị nghẽn mạng hoặc đơ/sập (crash).
* **Giả lập hành vi người dùng (Player/User Simulation):** Nhóm không cần phải huy động 500 người thật, mà cần tạo các script giả lập (simulate) hành vi của 50, 100 đến 500 user đồng thời gửi dữ liệu submit lên Server.
* **Đề xuất Giải pháp Kiến trúc Xử lý Quá tải:**
  * **Cơ chế Hàng đợi (Message Queue / Rate Limiting):** Thay vì cho phép 500 request dồn dập đánh thẳng vào Server, hệ thống sẽ đưa request vào hàng đợi (Queue), xử lý theo kiểu "gọi số thứ tự" (polling/ack).
  * **Cân bằng tải & Mở rộng Container (Load Balancing & Docker Scaling):** Sử dụng Nginx/Load Balancer kết hợp chạy nhiều Docker container để chứng minh hệ thống có thể mở rộng (scale) linh hoạt và nâng cao khả năng chịu tải khi cấp thêm tài nguyên hạ tầng.

---

### **4. Tổng kết Yêu cầu Kỳ vọng để Đạt Điểm Cao (High Grade Checklist)**
1. **Cấu hình thành công Grafana** (hoặc công cụ Monitoring tương đương) để hiển thị thông số hạ tầng theo thời gian thực.
2. **Giả lập được tải của hàng chục/hàng trăm user đồng thời** gửi request submit bài thi.
3. **Chứng minh được giải pháp giải quyết nghẽn mạng** (bằng Message Queue hoặc Scale Docker Container).
