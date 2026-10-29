# Transcript & Tổng Hợp Nội Dung Chi Tiết
**File ghi âm:** `Phuong_record_27_10_2026.m4a`  
**Ngày ghi âm / Cập nhật:** 27/10/2026  

---

### **1. Quản lý File Ghi âm & Cơ chế Tối ưu Upload Server**
* **Vị trí lưu file cục bộ (Client App):** 
  * Thay vì lưu file ghi âm trực tiếp ở thư mục cài đặt ứng dụng, hệ thống chuyển sang lưu file trong thư mục `AppData/[Mã_Đợt_Thi]`. Mỗi đợt thi có một folder mã hóa riêng chứa toàn bộ dữ liệu ghi âm của ca thi đó.
* **Cơ chế nộp file audio lên Server:** 
  * Vì số lượng thí sinh thi đồng thời lớn (30–50 sinh viên cùng nộp bài), dung lượng file ghi âm audio có thể rất lớn. 
  * **Cơ chế tải lên theo từng câu (Per-Question Upload):** Khi thí sinh hoàn thành trả lời câu nào, file audio của câu đó lập tức được gửi lên Server. Thí sinh không thể làm lại câu đã kết thúc. Phương pháp này giúp phân tán lưu lượng đường truyền, tránh tình trạng Server bị nghẽn mạng hoặc quá tải ở thời điểm kết thúc ca thi.
  * **Kiến trúc lưu trữ Server:** Server không nên lưu trực tiếp dữ liệu nhị phân (binary/blob) của file ghi âm vào Database chính ngay thời điểm nhận file, mà cần lưu vào bộ nhớ đệm/object storage riêng để tránh bị nghẽn Database.

---

### **2. Quy tắc Dịch vụ & Giao diện Thí sinh (Student UI Onboarding)**
* **Chuẩn hóa Quy tắc Dịch vụ (Service Rules):** Cần thể hiện rõ quy định thi và vẽ sơ đồ luồng (Diagram) chi tiết minh họa toàn bộ quá trình tương tác.
* **Tối giản Giao diện Thí sinh (Minimalist Student UI):** 
  * Loại bỏ các văn bản hướng dẫn rườm rà trên màn hình thi của thí sinh.
  * Luồng làm bài tinh gọn: Đăng nhập tài khoản $\rightarrow$ Nhập mã kích hoạt ca thi (Activation Code) $\rightarrow$ Màn hình Kiểm tra thiết bị (Microphone & Tai nghe) $\rightarrow$ Nhấn Bắt đầu thi.

---

### **3. Giao diện Quản lý Đợt thi dành cho Giảng viên/Quản trị (Admin Dashboard)**
* **Màn hình Chi tiết Đợt thi (Exam Session Detail):** 
  * Cần cung cấp bức tranh toàn cảnh về đợt thi thay vì hiển thị dữ liệu rời rạc.
* **Thiết kế dạng Tab (Tabbed Interface):**
  * **Tab Tổng quan & Báo cáo:** Theo dõi trạng thái đợt thi (Đang diễn ra, Đã đóng ca thi, Đang chấm điểm, Đã xuất báo cáo...).
  * **Tab Ca thi & Thí sinh:** Quản lý danh sách ca thi (Sections/Sessions) và danh sách sinh viên tham gia.
  * **Tab Hội đồng & Cấp quyền:** Quản lý danh sách Giám thị (Proctor) phòng thi, Giám khảo (Examiner) chấm điểm và phân quyền tương ứng.
  * **Tab Xuất dữ liệu:** Cho phép báo cáo, kết xuất điểm số và tổng hợp dữ liệu đợt thi.

---

### **4. Điều chỉnh Kỹ thuật & Cắt giảm Tính năng Không Cần Thiết**
* **Xóa bỏ Real-time Speech-to-Text trên App Thí sinh:** 
  * Việc chạy mô hình chuyển giọng nói thành văn bản thời gian thực trên máy thí sinh vừa tốn chi phí Token AI, vừa ngốn tài nguyên máy tính và dễ gây phân tâm cho thí sinh.
  * Thí sinh chỉ cần chức năng ghi âm và nghe lại file thoại của chính mình trước khi chuyển câu.
* **Chuyển tính năng Transcript sang Giao diện Giám khảo:** 
  * Quá trình chuyển giọng nói thành văn bản (Speech-to-Text) sẽ do Server xử lý tập trung 1 lần.
  * Màn hình của Giám khảo (Examiner) sẽ hiển thị bản Transcript đi kèm trình phát Audio có các phím điều khiển (tăng/giảm tốc độ phát - speed) để hỗ trợ quá trình nghe và chấm điểm thuận tiện.
