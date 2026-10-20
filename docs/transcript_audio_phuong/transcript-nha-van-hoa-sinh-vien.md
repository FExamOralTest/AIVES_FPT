# BẢN TRANSCRIPT & NỘI DUNG CHI TIẾT 2 FILE AUDIO MỚI

## 1. File Audio: Nhà Văn Hóa Sinh Viên Thành Phố Hồ Chí Minh.m4a

### A. Cấu hình Đề thi và Đợt thi
- **Quản lý Đề thi:** Mỗi đề thi gồm mã đề và danh sách câu hỏi. Ứng với mỗi câu hỏi sẽ có nội dung cụ thể cùng bộ **tiêu chí chấm điểm (Rubric)** (ví dụ: nội dung bám sát chủ đề hay không, các ý cộng điểm hoặc trừ điểm) [1].
- **Cấu hình Đợt thi & Ca thi:** Một đợt thi chia thành nhiều ca thi [1]. Hệ thống cho phép cấu hình trộn ngẫu nhiên nhiều đề thi cho sinh viên (ví dụ: ca thi 50 sinh viên sử dụng 5 bộ đề khác nhau, thuật toán random phân bổ 10 sinh viên/đề) [2].

### B. Quy trình Thi trên Desktop App
- **Giao diện Client:** Thí sinh sử dụng phần mềm dạng Desktop App (viết bằng Flutter) [2]. App tải đề thi từ server về [2].
- **Thực hiện bài thi:** Thí sinh đeo tai nghe, kiểm tra micro, thực hiện trả lời lần lượt các câu hỏi 1, 2, 3... Thí sinh có thể chọn phiên trả lời cuối cùng để lưu lại trước khi nộp bài [2, 3].
- **Gửi dữ liệu:** File ghi âm (audio) của thí sinh được đẩy trực tiếp lên Server [3].

### C. Xử lý Chuyển đổi & Chấm điểm trên Server
- **Speech-to-Text (Server-side):** Việc chuyển âm thanh thành văn bản (transcript) được thực hiện tập trung trên Server thông qua thư viện Speech-to-Text để tránh quá tải cho các máy client [3].
- **AI Chấm điểm theo Rubric:** Server kết hợp bản transcript + file audio truyền qua mô hình AI [3]. AI dựa vào tiêu chí Rubric của từng câu hỏi để đưa ra mức độ hoàn thành và quy đổi ra điểm số [3].
- **Chấm điểm & Duyệt điểm (Giám khảo & Moderator):** Giám khảo chấm bài sẽ nghe lại file audio/đánh giá kết quả [3]. Cuối cùng, **Moderator** sẽ duyệt điểm (finalize) và công bố kết quả [3].

### D. Phân biệt Giám thị (Proctor) và Giám khảo (Examiner)
- **Giám thị:** Quản lý phòng thi (Check-in/Check-out, nhấn nút kích hoạt ca thi sau khi kiểm tra camera/micro) [3]. Có thể thiết kế riêng Mobile App cho giám thị [3, 4].
- **Giám khảo:** Người chịu trách nhiệm nghe và chấm điểm bài thi của sinh viên trên hệ thống [3].

---

## 2. File Audio: Nhà Văn Hóa Sinh Viên (Đại học QG).m4a

### A. Định hướng Dự án & Quyền lợi Sinh viên
- **Đánh giá từ Giảng viên:** Hướng đi của nhóm bám sát đúng định hướng của giảng viên [5]. Giảng viên khuyến khích nhóm tăng tốc hoàn thành dự án trong khoảng 3–4 tuần tới [5].
- **Lợi ích & Quyền lợi:** Nếu sản phẩm được triển khai thử nghiệm thành công tại trường, sinh viên sẽ nhận được điểm bonus/điểm thưởng, đồng thời đây là một case study thực tế tốt có thể phát triển thành bài báo nghiên cứu [5, 6].

### B. Hỗ trợ Tài nguyên từ Giảng viên
- Giảng viên cung cấp một **GitHub Repository (mẫu code cơ bản)** để sinh viên tham khảo cấu trúc và luồng làm việc [6].

### C. Trọng tâm Xây dựng Hệ thống
1. **Luồng trải nghiệm Thí sinh (Onboarding & Thi):**
   - Thiết kế giao diện cho 50 sinh viên thi đồng thời [6].
   - Thí sinh nhập code, đeo tai nghe, thực hiện test âm thanh/micro kỹ càng trước khi bấm nút "Start" để bắt đầu làm bài [6].
2. **Luồng Chấm điểm trên Server (Grading):**
   - Áp dụng bộ tiêu chí Rubric kết hợp mô hình AI để xử lý bài thi audio và đưa ra điểm số tự động [6].
