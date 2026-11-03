# Transcript & Nội dung chi tiết: Nhà Văn Hóa Sinh Viên Thành Phố Hồ Chí Minh.m4a

---

### **1. Cấu hình Đề thi và Đợt thi**
* **Quản lý Đề thi:** Mỗi đề thi gồm mã đề và danh sách các câu hỏi cụ thể. Đi kèm với từng câu hỏi là bộ **tiêu chí chấm điểm (Rubric)** (chẳng hạn: mức độ bám sát chủ đề, các ý được cộng điểm hoặc trừ điểm).
* **Cấu hình Đợt thi & Ca thi:** Một đợt thi được chia thành nhiều ca thi nhỏ. Hệ thống cho phép cấu hình và trộn ngẫu nhiên nhiều bộ đề thi cho sinh viên (ví dụ: ca thi 50 sinh viên sử dụng 5 bộ đề, hệ thống sẽ dùng thuật toán để random phân bổ 10 sinh viên/đề).

---

### **2. Quy trình Thi trên Desktop App (Flutter)**
* **Giao diện Client:** Thí sinh sử dụng ứng dụng **Desktop App (viết bằng Flutter)**. Ứng dụng sẽ tải đề thi trực tiếp từ Server về máy.
* **Thực hiện bài thi:** Thí sinh đeo tai nghe, kiểm tra micro, thực hiện trả lời lần lượt các câu hỏi 1, 2, 3... Thí sinh có thể chọn lưu lại phiên trả lời cuối cùng trước khi hoàn tất nộp bài.
* **Gửi dữ liệu:** File audio ghi âm phần trả lời của thí sinh được đẩy thẳng lên Server.

---

### **3. Xử lý Chuyển đổi & Chấm điểm trên Server**
* **Speech-to-Text (Server-side):** Quá trình chuyển audio thành văn bản (transcript) được thực hiện tập trung trên Server bằng thư viện Speech-to-Text để tránh gây tốn tài nguyên trên 50 máy client.
* **AI Chấm điểm theo Rubric:** Server truyền dữ liệu (transcript + file audio) qua mô hình AI. AI dựa vào tiêu chí Rubric của từng câu hỏi để phân tích mức độ hoàn thành và quy đổi thành điểm số.
* **Chấm điểm & Duyệt điểm (Giám khảo & Moderator):** Giám khảo sẽ nghe lại audio/đối chiếu kết quả chấm. Sau đó, **Moderator** sẽ duyệt điểm số cuối cùng (finalize) và công bố kết quả.

---

### **4. Phân biệt Vai trò: Giám thị (Proctor) và Giám khảo (Examiner)**
* **Giám thị (Proctor):** Quản lý phòng thi (Check-in/Check-out, kiểm tra camera/micro và nhấn nút kích hoạt ca thi). Có thể xây dựng một Mobile App riêng dành cho giám thị.
* **Giám khảo (Examiner):** Người chịu trách nhiệm nghe lại bài thi và chấm/đánh giá điểm số của sinh viên.
