# Student Accommodation Booking System

Ứng dụng ASP.NET Core MVC (.NET 8) kết nối SQL Server, dùng cookie authentication và phân quyền `STUDENT`, `ADMIN`, `FACILITIES_STAFF`, `SECURITY_GUARD`. Giao diện dùng Razor, Bootstrap 5 và CSS/JavaScript thuần. Mỗi tài khoản chỉ truy cập dashboard của vai trò mình; form hồ sơ lưu vào database bằng transaction.

## Chạy trên máy này

SQL Server mặc định (`Server=.`) đã được nạp từ file `C:\Users\vuan1\Downloads\Student_Accommodation_Booking_System_SQLServer.sql`. Dự án dùng database `StudentAccommodationBookingDB`. Nếu dùng server hoặc instance khác, sửa `ConnectionStrings:AccommodationDb` trong `STUDENT_ACCOMMODATION_BOOKING_SYSTEM/appsettings.json` hoặc đặt biến môi trường `ConnectionStrings__AccommodationDb`. Với SQL authentication, nên dùng biến môi trường hoặc .NET user secrets thay vì ghi mật khẩu trong repo.

```powershell
cd C:\Semester7\PRN222\STUDENT_ACCOMMODATION_BOOKING_SYSTEM\STUDENT_ACCOMMODATION_BOOKING_SYSTEM
dotnet run --launch-profile http
```

Mở `http://localhost:5118/`. Trang đầu tiên là đăng nhập. Tài khoản mẫu:

| Vai trò | Email |
| --- | --- |
| Student | `an.student@example.test` |
| Admin | `admin@example.test` |
| Facilities staff | `facilities@example.test` |
| Security guard | `guard@example.test` |

Các tài khoản mẫu khác là `binh.student@example.test`, `cuong.student@example.test`, `duong.student@example.test`, `ha.student@example.test`. Tất cả tài khoản mẫu dùng mật khẩu `123456`.

## Nếu dùng database khác

1. Chạy toàn bộ file SQL gốc trong SSMS. Nếu chạy bằng `sqlcmd`, dùng tùy chọn `-I` để bật `QUOTED_IDENTIFIER` cho các filtered index.
2. Cập nhật connection string cho đúng SQL Server và database.
3. Chạy `sql/002_student_booking_rules.sql` sau file SQL gốc để thêm cấu hình điểm tháng, giới hạn lượt tuần và cập nhật `dbo.usp_BookRoom`. Ví dụ: `sqlcmd -S . -d StudentAccommodationBookingDB -E -I -b -i sql\002_student_booking_rules.sql`.
4. File SQL gốc dùng `DEMO_HASH_NOT_FOR_LOGIN`, không phải mật khẩu hợp lệ. Chỉ trong môi trường `Development`, đặt mật khẩu mẫu riêng rồi khởi tạo hash một lần:

```powershell
$env:SABS_DEMO_PASSWORD = 'MatKhauMauDaiItNhat12KyTu!'
dotnet run -- --init-demo-passwords
Remove-Item Env:SABS_DEMO_PASSWORD
```

Lệnh chỉ thay các dòng còn chứa placeholder; các hash đã tạo không bị ghi đè. Không có tài khoản đăng ký công khai. Nếu cần cấp tài khoản thật, thêm bản ghi vào `ACCOUNT` và bảng vai trò tương ứng, lưu `password_hash` bằng ASP.NET Core `PasswordHasher<AccountRecord>`.

## Hành vi chính

- Đăng nhập kiểm tra trường trống, định dạng email, tài khoản và mật khẩu. Server kiểm tra trạng thái `ACTIVE`, vai trò và password hash trước khi phát cookie.
- Cookie có `HttpOnly`, `SameSite=Lax`, thời hạn 8 giờ, trượt theo phiên. Form thay đổi dữ liệu và đăng xuất dùng anti-forgery token.
- Dashboard có thông tin cơ bản và thông tin theo vai trò. Các trường không bắt buộc có thể xóa nội dung rồi lưu. Email, tên, mã sinh viên/mã nhân viên là bắt buộc theo schema hiện tại.
- Mục thông tin bổ sung hỗ trợ thêm, sửa, xóa. Tất cả thay đổi trong một lần lưu được ghi trong transaction và giới hạn theo tài khoản đang đăng nhập.
- Không có chức năng vô hiệu hóa hay xóa tài khoản trên giao diện.

## Chức năng sinh viên: phòng và lịch đặt

- `STUDENT DASHBOARD` có **Danh sách phòng** (`/student/rooms`) và **Đặt phòng của tôi** (`/student/bookings`). Chỉ tài khoản `STUDENT` được truy cập.
- Danh sách phòng đọc bằng EF Core/LINQ từ `ROOM` và thiết bị đang hoạt động trong `ROOM_ASSET`. Mỗi phòng khả dụng có nút đặt ngay trên danh sách ban đầu.
- Điểm uy tín tháng = `monthly_initial_score` (mặc định 10) cộng tổng `REPUTATION_EVENT.score_delta` phát sinh trong tháng theo giờ Việt Nam. Đầu tháng mới, điểm tự bắt đầu lại từ 10; không cần tác vụ chạy nền. Chính sách vẫn giữ `booking_block_below` để chặn đặt khi điểm thấp hơn ngưỡng.
- Giới hạn đặt phòng là `weekly_booking_limit` (mặc định 5). Hệ thống đếm các lượt đã tạo trong tuần hiện tại theo giờ Việt Nam, kể cả lượt đã hủy; đầu tuần mới số đã dùng trở về 0. Procedure kiểm tra giới hạn trong transaction khi đặt.
- Bộ lọc tòa, tầng, sức chứa tối thiểu và **Ngày đặt** gọi AJAX tới `GET /api/student/rooms/available?date=yyyy-MM-dd`. API trả các khung giờ còn trống trong ngày của phòng `AVAILABLE` sau khi loại khoảng thời gian giao với `BOOKING` trạng thái `PENDING` hoặc `APPROVED`. Người dùng chọn một phần của khung giờ trước khi đặt. Ngày và giờ hiển thị theo UTC+7; database lưu UTC.
- Đặt phòng gọi `dbo.usp_BookRoom`, hủy gọi `dbo.usp_CancelBooking`. Hai procedure kiểm tra điều kiện và thực hiện transaction; kết quả tìm kiếm chỉ để tham khảo. Server luôn lấy `student_id` từ cookie đăng nhập, không nhận ID sinh viên từ biểu mẫu. Mọi form ghi dữ liệu đều có anti-forgery token.
- Trang **Đặt phòng của tôi** chỉ truy vấn các lịch đặt thuộc tài khoản đăng nhập. Lịch được hiển thị theo giờ Việt Nam (UTC+7), trong database vẫn lưu UTC.
