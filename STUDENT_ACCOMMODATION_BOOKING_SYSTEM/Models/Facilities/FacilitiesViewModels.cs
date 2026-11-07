using System.ComponentModel.DataAnnotations;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Facilities;

/// <summary>
/// Thống kê tổng quan tình trạng thiết bị hiển thị ở Right Sidebar.
/// </summary>
public class FacilitiesOverviewStats
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int MaintenanceAssets { get; set; }
    public int DamagedAssets { get; set; }
    public int MissingAssets { get; set; }
    public int RetiredAssets { get; set; }
    public int PendingIncidents { get; set; }
    public int TotalIncidents { get; set; }
}

/// <summary>
/// DTO chứa thông tin tóm tắt của phòng phục vụ chọn lọc (dropdown / sidebar).
/// </summary>
public class RoomLookupDto
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public short Floor { get; set; }
    public short Capacity { get; set; }
    public string RoomStatus { get; set; } = string.Empty;
    public int AssetCount { get; set; }

    public string DisplayText => $"{RoomCode} - {Building} (Tầng {Floor})";
}

/// <summary>
/// DTO hiển thị từng dòng thiết bị trong danh sách.
/// </summary>
public class RoomAssetDto
{
    public long AssetId { get; set; }
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public short Floor { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string RegisteredCondition { get; set; } = "GOOD"; // GOOD, WORN, DAMAGED
    public string AssetStatus { get; set; } = "ACTIVE"; // ACTIVE, MAINTENANCE, RETIRED, MISSING
    public string CreatedByStaffName { get; set; } = string.Empty;
    public string? UpdatedByStaffName { get; set; }
    public int IncidentCount { get; set; }

    public string ConditionLabel => RegisteredCondition switch
    {
        "GOOD" => "Tốt",
        "WORN" => "Hao mòn",
        "DAMAGED" => "Hư hỏng",
        _ => RegisteredCondition
    };

    public string StatusLabel => AssetStatus switch
    {
        "ACTIVE" => "Đang hoạt động",
        "MAINTENANCE" => "Đang bảo trì",
        "RETIRED" => "Đã thanh lý",
        "MISSING" => "Thất lạc",
        _ => AssetStatus
    };
}

/// <summary>
/// Input Model khi Thêm mới thiết bị vào phòng.
/// </summary>
public class AssetCreateInput
{
    [Required(ErrorMessage = "Vui lòng chọn phòng trang bị thiết bị.")]
    [Display(Name = "Phòng")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã tem thiết bị.")]
    [StringLength(50, ErrorMessage = "Mã tem tối đa 50 ký tự.")]
    [Display(Name = "Mã tem (Asset Tag)")]
    public string AssetTag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên thiết bị.")]
    [StringLength(150, ErrorMessage = "Tên thiết bị tối đa 150 ký tự.")]
    [Display(Name = "Tên thiết bị")]
    public string AssetName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn tình trạng thiết bị.")]
    [Display(Name = "Tình trạng ban đầu")]
    public string RegisteredCondition { get; set; } = "GOOD"; // GOOD, WORN, DAMAGED

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [Display(Name = "Trạng thái hoạt động")]
    public string AssetStatus { get; set; } = "ACTIVE"; // ACTIVE, MAINTENANCE, RETIRED, MISSING
}

/// <summary>
/// Input Model khi Chỉnh sửa thông tin thiết bị.
/// </summary>
public class AssetEditInput
{
    public long AssetId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng.")]
    [Display(Name = "Phòng")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã tem thiết bị.")]
    [StringLength(50, ErrorMessage = "Mã tem tối đa 50 ký tự.")]
    [Display(Name = "Mã tem")]
    public string AssetTag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên thiết bị.")]
    [StringLength(150, ErrorMessage = "Tên thiết bị tối đa 150 ký tự.")]
    [Display(Name = "Tên thiết bị")]
    public string AssetName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Tình trạng")]
    public string RegisteredCondition { get; set; } = "GOOD";

    [Required]
    [Display(Name = "Trạng thái")]
    public string AssetStatus { get; set; } = "ACTIVE";
}

/// <summary>
/// Input Model khi Cập nhật nhanh tình trạng / trạng thái thiết bị.
/// </summary>
public class AssetStatusUpdateInput
{
    public long AssetId { get; set; }

    [Required]
    public string RegisteredCondition { get; set; } = "GOOD";

    [Required]
    public string AssetStatus { get; set; } = "ACTIVE";

    [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
    public string? Note { get; set; }
}

/// <summary>
/// DTO hiển thị danh sách sự cố thiết bị do bảo vệ báo cáo.
/// </summary>
public class IncidentItemDto
{
    public long IncidentId { get; set; }
    public long InspectionItemId { get; set; }
    public long InspectionId { get; set; }
    public long BookingId { get; set; }
    public long AssetId { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string CurrentAssetStatus { get; set; } = string.Empty;
    public string CurrentAssetCondition { get; set; } = string.Empty;
    public string IncidentType { get; set; } = "DAMAGED"; // DAMAGED, MISSING
    public DateTime ReportedAt { get; set; }
    public string ReviewStatus { get; set; } = "PENDING"; // PENDING, CONFIRMED, REJECTED
    public bool VerifiedMissing { get; set; }
    public string? IncidentNotes { get; set; }
    public string Phase { get; set; } = "OUT"; // IN, OUT
    public string? GuardName { get; set; }
    public string? AdminReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public string IncidentTypeLabel => IncidentType switch
    {
        "DAMAGED" => "Hư hỏng",
        "MISSING" => "Thất lạc / Mất",
        _ => IncidentType
    };

    public string ReviewStatusLabel => ReviewStatus switch
    {
        "PENDING" => "Chờ Admin duyệt",
        "CONFIRMED" => "Admin đã xác nhận",
        "REJECTED" => "Admin từ chối",
        _ => ReviewStatus
    };
}

/// <summary>
/// Input Model khi Nhân viên CSVC ghi nhận xử lý sự cố / bảo dưỡng thiết bị.
/// </summary>
public class ProcessIncidentInput
{
    public long IncidentId { get; set; }
    public long AssetId { get; set; }

    /// <summary>
    /// Hành động kỹ thuật: 'MAINTENANCE' (Chuyển đi bảo dưỡng), 'RESTORE' (Đã sửa xong - phục hồi ACTIVE), 'RETIRE' (Hỏng nát - thanh lý)
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chọn hình thức xử lý.")]
    public string Action { get; set; } = "MAINTENANCE";

    [Required]
    public string TargetCondition { get; set; } = "GOOD"; // GOOD, WORN, DAMAGED

    [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
    public string? MaintenanceNotes { get; set; }
}

/// <summary>
/// Tiêu chí lọc thiết bị.
/// </summary>
public class AssetFilterCriteria
{
    public string? Search { get; set; }
    public string? Building { get; set; }
    public int? RoomId { get; set; }
    public string? Condition { get; set; }
    public string? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Tiêu chí lọc sự cố.
/// </summary>
public class IncidentFilterCriteria
{
    public string? Search { get; set; }
    public string? IncidentType { get; set; }
    public string? ReviewStatus { get; set; }
    public int? RoomId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Kết quả phân trang chung.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 10));
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
}

/// <summary>
/// ViewModel tổng thể cung cấp cho view chính theo cấu trúc wireframe sketch:
/// - Left rail (Navigation icons)
/// - Sidebar (Danh sách phòng / lọc nhanh)
/// - Tabs (Thiết bị phòng, Sự cố thiết bị, Nhật ký bảo dưỡng)
/// - Main content (Nội dung danh sách, chi tiết)
/// - Right side bar (Thống kê tình trạng, sự cố khẩn, thao tác nhanh)
/// - Bottom bar (Thông tin hệ thống / phiên làm việc)
/// </summary>
public class FacilitiesDashboardViewModel
{
    public string ActiveTab { get; set; } = "assets"; // 'assets', 'incidents', 'logs'
    public FacilitiesOverviewStats Stats { get; set; } = new();
    public List<RoomLookupDto> Rooms { get; set; } = new();
    public List<string> Buildings { get; set; } = new();

    // Dữ liệu danh sách thiết bị
    public PagedResult<RoomAssetDto> AssetsPaged { get; set; } = new();
    public AssetFilterCriteria AssetFilter { get; set; } = new();

    // Dữ liệu danh sách sự cố
    public PagedResult<IncidentItemDto> IncidentsPaged { get; set; } = new();
    public IncidentFilterCriteria IncidentFilter { get; set; } = new();

    // Dữ liệu lịch sử Audit Log
    public List<AuditLogItemDto> RecentAuditLogs { get; set; } = new();

    // Form inputs cho modal popup
    public AssetCreateInput CreateInput { get; set; } = new();
    public AssetEditInput EditInput { get; set; } = new();
    public ProcessIncidentInput ProcessInput { get; set; } = new();

    // Thông tin nhân viên CSVC đang đăng nhập
    public string StaffName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? AssignedArea { get; set; }
}

/// <summary>
/// DTO hiển thị dòng lịch sử Audit Log.
/// </summary>
public class AuditLogItemDto
{
    public long AuditId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? BeforeSummary { get; set; }
    public string? AfterSummary { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
}

