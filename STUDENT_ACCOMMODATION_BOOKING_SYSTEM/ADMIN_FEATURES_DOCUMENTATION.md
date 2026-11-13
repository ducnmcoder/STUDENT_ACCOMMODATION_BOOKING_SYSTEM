# Tài liệu Chức năng Admin (Người 4 - Tiến Thành)

## Tổng quan
Đã implement đầy đủ 4 chức năng chính của Admin Dashboard theo yêu cầu:

1. ✅ **Quản lý phòng (CRUD ROOM)**
2. ✅ **Thống kê lịch sử sử dụng**
3. ✅ **Duyệt báo cáo thất lạc (ASSET_INCIDENT)**
4. ✅ **Hiển thị phòng đang sử dụng Real-time (SignalR)**

---

## 1. Quản lý phòng

### Routes:
- `GET /admin/rooms` - Danh sách phòng (có filter và search)
- `GET /admin/rooms/create` - Form tạo phòng mới
- `POST /admin/rooms/create` - Xử lý tạo phòng
- `GET /admin/rooms/edit/{roomId}` - Form chỉnh sửa phòng
- `POST /admin/rooms/edit/{roomId}` - Xử lý cập nhật phòng
- `POST /admin/rooms/delete/{roomId}` - Xóa phòng

### Features:
- **CRUD đầy đủ** trên bảng ROOM
- **Filter theo trạng thái**: AVAILABLE, MAINTENANCE, INACTIVE
- **Search** theo mã phòng và tòa nhà
- **Validation**: Mã phòng unique, capacity 1-20, floor 1-50
- **Audit log**: Ghi lại mọi thao tác CREATE/UPDATE/DELETE
- **last_configured_by_admin_id**: Tracking admin nào cập nhật cuối

### Database:
```sql
-- Bảng ROOM có sẵn với các cột:
room_id, room_code, building, floor, capacity, room_status, last_configured_by_admin_id

-- Bảng ADMIN (chỉ có 2 cột):
account_id (PK), admin_scope

-- last_configured_by_admin_id trong ROOM = account_id trong ADMIN
```

### Notes:
- `room_status` chỉ có 3 giá trị: AVAILABLE, MAINTENANCE, INACTIVE
- Không dùng `room_status` làm cờ "đang sử dụng" (theo yêu cầu spec)
- Trạng thái "đang sử dụng" được tính từ BOOKING + ATTENDANCE + CHECKOUT

---

## 2. Thống kê lịch sử sử dụng

### Routes:
- `GET /admin/statistics` - Trang thống kê với filter

### Features:
- **Filter theo thời gian**: startDate, endDate (mặc định 30 ngày gần nhất)
- **Filter theo tòa nhà**: buildingFilter
- **LINQ GroupBy**: Tính toán trên SQL Server
  ```csharp
  COUNT(DISTINCT b.booking_id) AS total_bookings
  COUNT(DISTINCT c.checkout_id) AS completed_checkouts
  COUNT(DISTINCT cn.cancellation_id) AS cancelled_bookings
  CAST(COUNT(DISTINCT c.checkout_id) AS DECIMAL(5,2)) / COUNT(DISTINCT b.booking_id) * 100 AS occupancy_rate
  ```
- **Visualization**: Progress bar cho tỷ lệ hoàn thành
- **Summary cards**: Tổng phòng, tổng booking, hoàn thành, hủy

### Database:
Đọc từ: BOOKING, ATTENDANCE, CHECKOUT, CANCELLATION, ROOM

---

## 3. Duyệt báo cáo thất lạc (ASSET_INCIDENT)

### Routes:
- `GET /admin/incidents` - Danh sách sự cố chờ duyệt (review_status = PENDING)
- `GET /admin/incidents/{incidentId}` - Chi tiết sự cố
- `POST /admin/incidents/review` - Duyệt sự cố (approve/reject)

### Features:
- **Review workflow**:
  1. Admin xem danh sách sự cố PENDING
  2. Chọn XÁC NHẬN (verified_missing=true) hoặc TỪ CHỐI (verified_missing=false)
  3. Nhập ghi chú (tối đa 300 ký tự)
  4. Lưu → update ASSET_INCIDENT
- **Trừ điểm tự động**:
  - Chỉ sự cố **MISSING** (thất lạc) được admin xác nhận mới gọi `ReputationService.DeductPointsForMissingAssetAsync()`
  - Trừ **10 điểm uy tín** cho sinh viên
  - Ghi vào bảng REPUTATION_EVENT với event_type = 'MISSING_ASSET'
- **Audit log**: Ghi lại mọi quyết định duyệt

### Database:
```sql
-- ASSET_INCIDENT
incident_id, inspection_item_id, incident_type, reported_at, review_status,
verified_missing, reviewed_by_admin_id, reviewed_at, incident_notes
-- reviewed_by_admin_id = account_id của admin (không phải admin_id riêng)

-- REPUTATION_EVENT (người 5 quản lý, nhưng admin gọi service trừ điểm)
event_id, student_id, event_type, point_change, event_date, event_description

-- AUDIT_LOG (cấu trúc thực tế):
audit_id, performed_by_account_id, action_type, target_entity, target_key,
occurred_at, before_summary, after_summary
```

### Logic:
```csharp
if (input.VerifiedMissing && incident.IncidentType == "MISSING")
{
    await reputationService.DeductPointsForMissingAssetAsync(
        incident.BookingId!.Value,
        incident.AssetName ?? "tài sản",
        "Admin xác nhận thất lạc tài sản");
}
```

---

## 4. Phòng đang sử dụng Real-time (SignalR)

### Routes:
- `GET /admin/occupied-rooms` - Trang hiển thị phòng đang sử dụng
- `GET /admin/api/occupied-rooms` - API endpoint cho AJAX
- `WS /hubs/occupancy` - SignalR WebSocket endpoint

### Features:
- **SignalR Hub**: `OccupancyHub` (chỉ ADMIN mới connect được)
- **Real-time updates**:
  - Auto-refresh mỗi 30 giây
  - Manual refresh bằng button
  - SignalR push khi có room update
- **Status calculation**:
  ```sql
  CASE 
    WHEN b.end_time < GETDATE() THEN 'OVERDUE'
    ELSE 'IN_USE'
  END AS status
  ```
- **Hiển thị**:
  - Room code, building, floor
  - Sinh viên đang sử dụng
  - Check-in time
  - Expected checkout time
  - Status badge (IN_USE, OVERDUE)
- **Visual indicators**:
  - Animated pulse dot
  - Red highlight cho phòng quá hạn
  - Live badge với animation

### Database:
Đọc từ: ROOM, BOOKING, ATTENDANCE, CHECKOUT, STUDENT, ACCOUNT

### SignalR Setup:
```csharp
// Program.cs
builder.Services.AddSignalR();
app.MapHub<OccupancyHub>("/hubs/occupancy");

// Client (JavaScript)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/occupancy")
    .withAutomaticReconnect()
    .build();
```

---

## Files Created

### Backend:
1. `Data/RoomRepository.cs` - Repository cho ROOM và ASSET_INCIDENT
2. `Data/ReputationService.cs` - Service trừ điểm uy tín (gọi từ người 4, dữ liệu người 5 quản lý)
3. `Controllers/AdminController.cs` - Controller xử lý tất cả routes admin
4. `Hubs/OccupancyHub.cs` - SignalR Hub cho real-time updates
5. `Models/RoomModels.cs` - ViewModels cho Room, Statistics, Incidents, Occupancy

### Frontend:
1. `Views/Admin/ManageRooms.cshtml` - Danh sách phòng
2. `Views/Admin/RoomForm.cshtml` - Form thêm/sửa phòng
3. `Views/Admin/UsageStatistics.cshtml` - Trang thống kê
4. `Views/Admin/ManageIncidents.cshtml` - Danh sách sự cố
5. `Views/Admin/ReviewIncident.cshtml` - Form duyệt sự cố
6. `Views/Admin/OccupiedRooms.cshtml` - Trang phòng đang sử dụng (SignalR)

### Configuration:
1. `libman.json` - Cài đặt SignalR client library
2. Updated `Program.cs` - Đăng ký services và SignalR
3. Updated `Views/Dashboard/Index.cshtml` - Thêm menu Admin

---

## Database Dependencies

### Ghi (WRITE):
- ROOM (INSERT, UPDATE, DELETE)
- ASSET_INCIDENT (UPDATE review_status, verified_missing, reviewed_by_admin_id, reviewed_at)
- AUDIT_LOG (INSERT mọi thao tác admin)
- REPUTATION_EVENT (INSERT khi trừ điểm)

### Đọc (READ):
- BOOKING
- ATTENDANCE
- CHECKOUT
- CANCELLATION
- INSPECTION
- INSPECTION_ITEM
- ROOM_ASSET
- ADMIN
- STUDENT
- ACCOUNT

---

## Security

### Authorization:
- Tất cả routes yêu cầu `[Authorize(Roles = "ADMIN")]`
- SignalR Hub cũng yêu cầu role ADMIN

### Validation:
- AntiForgeryToken trên mọi POST request
- Input validation với Data Annotations
- SQL parameters để tránh injection

### Audit Trail:
- Mọi thao tác quan trọng được ghi vào AUDIT_LOG
- Tracking admin_id thực hiện hành động

---

## Testing Guide

### 1. Test CRUD Room:
```
1. Login với tài khoản admin@accommodation.edu.vn
2. Vào /admin/rooms
3. Click "Thêm phòng mới"
4. Nhập: Room code = A101, Building = Tòa A, Floor = 1, Capacity = 4
5. Click "Tạo phòng"
6. Verify: Phòng xuất hiện trong danh sách
7. Click "Sửa" → thay đổi capacity → Lưu
8. Click "Xóa" (chỉ xóa được nếu không có booking)
```

### 2. Test Thống kê:
```
1. Vào /admin/statistics
2. Chọn khoảng thời gian (ví dụ 30 ngày gần nhất)
3. Filter theo tòa nhà nếu cần
4. Click "Xem thống kê"
5. Verify: Hiển thị danh sách phòng với số liệu booking, checkout, cancel, occupancy rate
```

### 3. Test Duyệt sự cố:
```
1. Vào /admin/incidents
2. Click "Duyệt" trên một sự cố PENDING
3. Chọn "Xác nhận" hoặc "Từ chối"
4. Nhập ghi chú
5. Click "Xác nhận duyệt"
6. Verify: 
   - Sự cố biến mất khỏi danh sách PENDING
   - Nếu là MISSING và xác nhận → sinh viên bị trừ 10 điểm
```

### 4. Test Phòng đang sử dụng:
```
1. Vào /admin/occupied-rooms
2. Verify: 
   - Hiển thị "✅ Kết nối thành công — Dữ liệu real-time"
   - Danh sách phòng có sinh viên đã check-in nhưng chưa checkout
   - Status OVERDUE nếu quá end_time
3. Mở tab khác → tạo/xóa booking → check-in
4. Quay lại tab real-time → verify auto update (hoặc click "Làm mới")
```

---

## Integration với Người khác

### Với Người 5 (Điểm uy tín):
- Admin gọi `ReputationService.DeductPointsForMissingAssetAsync()`
- Service này ghi vào bảng REPUTATION_EVENT
- Người 5 sẽ đọc bảng này để tính tổng điểm, hiển thị lịch sử

### Với Người 1, 2, 3:
- Admin quản lý ROOM → ảnh hưởng đến booking (người 1)
- Admin đọc BOOKING, ATTENDANCE, CHECKOUT để thống kê và hiển thị occupied rooms

---

## Known Limitations

1. **Delete Room**: Chỉ xóa được phòng không có booking/data liên quan (foreign key constraint)
2. **SignalR reconnect**: Nếu mất kết nối lâu, cần refresh trang
3. **Real-time push**: Hiện tại chỉ push khi RoomUpdated, chưa push khi có check-in/checkout mới (cần người 2, 3 gọi hub)

## Important Notes

### Database Schema Clarifications:
- **ADMIN table**: Chỉ có `account_id` (PK) và `admin_scope`. Không có cột `admin_id` riêng.
- **ROOM.last_configured_by_admin_id**: Thực chất là `account_id` của admin, không phải `admin_id` riêng.
- **AUDIT_LOG**: Dùng `performed_by_account_id` (account_id của admin), không phải `admin_id`.
- **ASSET_INCIDENT.reviewed_by_admin_id**: Cũng là `account_id` của admin.

### Code đã được sửa:
- Bỏ tất cả logic "lấy admin_id từ account_id"
- Dùng trực tiếp `account_id` làm foreign key
- JOIN trực tiếp `ROOM -> ACCOUNT` thay vì `ROOM -> ADMIN -> ACCOUNT`

---

## Future Enhancements

1. **Export statistics** sang Excel/CSV
2. **Bulk operations**: Tạo nhiều phòng cùng lúc, bulk update status
3. **Room history**: Lịch sử thay đổi phòng
4. **Incident attachments**: Cho phép upload ảnh khi báo cáo sự cố
5. **Dashboard widgets**: Charts, graphs cho statistics
6. **Real-time notifications**: Toast khi có sự cố mới, phòng quá hạn

---

## Liên hệ

- **Developer**: Người 4 - Tiến Thành
- **Module**: Admin Dashboard
- **Tables**: ROOM, ASSET_INCIDENT, AUDIT_LOG (write) + nhiều bảng khác (read)
- **SignalR Hub**: /hubs/occupancy
