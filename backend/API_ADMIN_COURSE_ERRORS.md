# Báo cáo lỗi API Admin Course

**Phạm vi:** các endpoint `/api/admin/courses` và lỗi runtime được cung cấp.  
**Kết luận:** hiện tại cả 7 endpoint đều không chạy được vì ASP.NET Core không tạo được `AdminCourseController`.

## 1. Lỗi chặn toàn bộ endpoint: thiếu đăng ký Dependency Injection

Log báo:

```text
Unable to resolve service for type
'Aives.Application.Interfaces.Services.ICourseService'
while attempting to activate 'Aives.Api.Controllers.AdminCourseController'
```

`AdminCourseController` nhận `ICourseService` trong constructor, nhưng `Program.cs` chưa đăng ký ánh xạ `ICourseService` sang `CourseService`. Do đó lỗi xảy ra khi framework tạo controller, trước khi chạy bất kỳ action nào. Cả GET, POST, PUT và DELETE đều có thể trả HTTP 500.

### Các endpoint bị ảnh hưởng

| Method | Endpoint | Ảnh hưởng hiện tại |
|---|---|---|
| GET | `/api/admin/courses` | HTTP 500 khi khởi tạo controller |
| POST | `/api/admin/courses` | HTTP 500 khi khởi tạo controller |
| GET | `/api/admin/courses/{id}` | HTTP 500 khi khởi tạo controller |
| PUT | `/api/admin/courses/{id}` | HTTP 500 khi khởi tạo controller |
| DELETE | `/api/admin/courses/{id}` | HTTP 500 khi khởi tạo controller |
| GET | `/api/admin/courses/teacher/{teacherId}` | HTTP 500 khi khởi tạo controller |
| PUT | `/api/admin/courses/{id}/assign-teacher` | HTTP 500 khi khởi tạo controller |

### Hướng khắc phục

Đăng ký service trong `Aives.Api/Program.cs`:

```csharp
builder.Services.AddScoped<ICourseService, CourseService>();
```

## 2. Lỗi DI kế tiếp: chưa đăng ký đúng IUserRepository cho CourseService

`CourseService` yêu cầu `Aives.Application.Interfaces.Repositories.IUserRepository`. Trong `Program.cs` hiện chỉ có đăng ký cho `Aives.Application.Auth.Interfaces.IUserRepository`; đây là **interface khác**, không thỏa dependency của `CourseService`.

Sau khi thêm đăng ký `ICourseService`, ứng dụng vẫn không tạo được `CourseService` nếu chưa đăng ký repository đúng interface. Thêm:

```csharp
builder.Services.AddScoped<
    Aives.Application.Interfaces.Repositories.IUserRepository,
    Aives.Infrastructure.Repositories.UserRepository>();
```

`ICourseRepository` đã được đăng ký, nên không phải dependency DI còn thiếu trong constructor này.

## 3. Lỗi xử lý 403 trong POST tạo course

Trong `AdminCourseController.Create`, nhánh bắt `UnauthorizedAccessException` gọi:

```csharp
return Forbid(ex.Message);
```

`Forbid(string)` nhận tên authentication scheme, không phải nội dung thông báo. Khi nhánh này được chạy (ví dụ `CreatedBy` không phải tài khoản ADMIN), thông báo lỗi có thể bị dùng làm scheme và tiếp tục gây lỗi runtime thay vì trả 403 như dự định.

Nên trả 403 cùng payload lỗi, ví dụ:

```csharp
return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
```

Lỗi này chỉ có thể quan sát sau khi sửa các đăng ký DI ở trên.

## 4. Ưu tiên khắc phục

1. Đăng ký `ICourseService` → `CourseService`.
2. Đăng ký đúng `Aives.Application.Interfaces.Repositories.IUserRepository` → `Aives.Infrastructure.Repositories.UserRepository`.
3. Sửa cách trả kết quả cho `UnauthorizedAccessException` trong endpoint POST.
4. Khởi động lại API và kiểm tra lần lượt 7 endpoint, bao gồm các nhánh 404, 409 và 403.

## Lưu ý thông tin nhạy cảm

Trace được gửi kèm có chứa giá trị `Authorization: Bearer ...`. Không đưa giá trị đó vào tài liệu, log công khai hoặc commit; hãy thu hồi/đổi token nếu token còn hiệu lực.
