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
3. Chạy `sql/001_account_profile_item.sql` để thêm bảng lưu các mục thông tin cá nhân tự tạo. Ứng dụng cũng tự tạo bảng này nếu tài khoản kết nối có quyền `CREATE TABLE`.
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
- Sinh viên xem điểm theo tuần và lịch sử sự kiện tại `/student/reputation`; admin xem tổng hợp điểm và các sự kiện tại `/admin/reputation`.
- Tác vụ nền định kỳ tìm booking `APPROVED` đã qua `POLICY.no_show_cutoff_min`, ghi `ATTENDANCE.NO_SHOW` và gọi `dbo.usp_RecordPenalty` để áp dụng mức phạt theo policy.
- Tác vụ nền mặc định quét mỗi 60 giây. Có thể điều chỉnh bằng `Reputation:NoShowScanSeconds` (giới hạn 15–3600 giây). Cần có ít nhất một tài khoản trong `SECURITY_GUARD` để tạo attendance tự động.
- Tác vụ chỉ hoạt động khi cơ sở dữ liệu có các bảng, ràng buộc và stored procedure `dbo.usp_RecordPenalty` từ file SQL gốc. Mỗi booking chỉ tạo một attendance; ghi attendance và sự kiện phạt được thực hiện an toàn khi chạy lặp.
- Không có chức năng vô hiệu hóa hay xóa tài khoản trên giao diện.
