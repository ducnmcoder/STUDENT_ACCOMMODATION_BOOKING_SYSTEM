using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Entities;

/// <summary>
/// Đại diện cho tài khoản người dùng trong hệ thống (ACCOUNT).
/// </summary>
[Table("ACCOUNT")]
public class Account
{
    [Key]
    [Column("account_id")]
    public int AccountId { get; set; }

    [Column("last_updated_by_admin_id")]
    public int? LastUpdatedByAdminId { get; set; }

    [Column("full_name")]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("password_hash")]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("account_status")]
    [MaxLength(20)]
    public string AccountStatus { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Đại diện cho thông tin hồ sơ nhân viên cơ sở vật chất (FACILITIES_STAFF).
/// Khóa ngoại liên kết 1-1 với ACCOUNT.
/// </summary>
[Table("FACILITIES_STAFF")]
public class FacilitiesStaff
{
    [Key]
    [Column("account_id")]
    public int AccountId { get; set; }

    [Column("employee_code")]
    [MaxLength(30)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Column("department")]
    [MaxLength(100)]
    public string? Department { get; set; }

    [Column("assigned_area")]
    [MaxLength(100)]
    public string? AssignedArea { get; set; }

    // Navigation property đến tài khoản
    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }
}

/// <summary>
/// Đại diện cho thông tin phòng trong hệ thống (ROOM).
/// Người 3 (Nhân viên CSVC) chỉ có quyền ĐỌC (read-only) để hiển thị và chọn phòng cho thiết bị.
/// </summary>
[Table("ROOM")]
public class Room
{
    [Key]
    [Column("room_id")]
    public int RoomId { get; set; }

    [Column("last_configured_by_admin_id")]
    public int? LastConfiguredByAdminId { get; set; }

    [Column("room_code")]
    [MaxLength(30)]
    public string RoomCode { get; set; } = string.Empty;

    [Column("building")]
    [MaxLength(100)]
    public string Building { get; set; } = string.Empty;

    [Column("floor")]
    public short Floor { get; set; }

    [Column("capacity")]
    public short Capacity { get; set; }

    [Column("room_status")]
    [MaxLength(20)]
    public string RoomStatus { get; set; } = "AVAILABLE"; // AVAILABLE, MAINTENANCE, INACTIVE

    // Danh sách thiết bị có trong phòng
    public virtual ICollection<RoomAsset> Assets { get; set; } = new List<RoomAsset>();
}

/// <summary>
/// Đại diện cho tài sản / thiết bị trang bị trong phòng (ROOM_ASSET).
/// Người 3 (Nhân viên cơ sở vật chất) sở hữu toàn quyền CRUD.
/// </summary>
[Table("ROOM_ASSET")]
public class RoomAsset
{
    [Key]
    [Column("asset_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long AssetId { get; set; }

    [Column("room_id")]
    public int RoomId { get; set; }

    [Column("created_by_staff_id")]
    public int CreatedByStaffId { get; set; }

    [Column("updated_by_staff_id")]
    public int? UpdatedByStaffId { get; set; }

    [Column("last_configured_by_admin_id")]
    public int? LastConfiguredByAdminId { get; set; }

    [Column("asset_tag")]
    [MaxLength(50)]
    public string AssetTag { get; set; } = string.Empty; // Mã tem thiết bị, DUY NHẤT (Unique)

    [Column("asset_name")]
    [MaxLength(150)]
    public string AssetName { get; set; } = string.Empty; // Tên thiết bị (bàn, ghế, điều hòa...)

    /// <summary>
    /// Tình trạng kỹ thuật: 'GOOD' (Tốt), 'WORN' (Hao mòn), 'DAMAGED' (Hư hỏng)
    /// </summary>
    [Column("registered_condition")]
    [MaxLength(20)]
    public string RegisteredCondition { get; set; } = "GOOD";

    /// <summary>
    /// Trạng thái hoạt động: 'ACTIVE' (Đang dùng), 'MAINTENANCE' (Bảo dưỡng), 'RETIRED' (Thanh lý), 'MISSING' (Thất lạc)
    /// </summary>
    [Column("asset_status")]
    [MaxLength(20)]
    public string AssetStatus { get; set; } = "ACTIVE";

    // Navigation properties
    [ForeignKey(nameof(RoomId))]
    public virtual Room? Room { get; set; }

    [ForeignKey(nameof(CreatedByStaffId))]
    public virtual FacilitiesStaff? CreatedByStaff { get; set; }

    [ForeignKey(nameof(UpdatedByStaffId))]
    public virtual FacilitiesStaff? UpdatedByStaff { get; set; }

    public virtual ICollection<InspectionItem> InspectionItems { get; set; } = new List<InspectionItem>();
}

/// <summary>
/// Đại diện cho biên bản kiểm tra nhận/trả phòng của bảo vệ (INSPECTION).
/// </summary>
[Table("INSPECTION")]
public class Inspection
{
    [Key]
    [Column("inspection_id")]
    public long InspectionId { get; set; }

    [Column("booking_id")]
    public long BookingId { get; set; }

    [Column("guard_id")]
    public int GuardId { get; set; }

    [Column("phase")]
    [MaxLength(10)]
    public string Phase { get; set; } = "IN"; // 'IN' hoặc 'OUT'

    [Column("inspected_at")]
    public DateTime InspectedAt { get; set; }

    [Column("overall_condition")]
    [MaxLength(20)]
    public string OverallCondition { get; set; } = "OK"; // 'OK', 'ISSUES', 'PENDING'

    [Column("inspection_notes")]
    [MaxLength(300)]
    public string? InspectionNotes { get; set; }

    [ForeignKey(nameof(GuardId))]
    public virtual Account? GuardAccount { get; set; }

    public virtual ICollection<InspectionItem> Items { get; set; } = new List<InspectionItem>();
}

/// <summary>
/// Đại diện cho kết quả kiểm tra từng thiết bị trong biên bản (INSPECTION_ITEM).
/// </summary>
[Table("INSPECTION_ITEM")]
public class InspectionItem
{
    [Key]
    [Column("inspection_item_id")]
    public long InspectionItemId { get; set; }

    [Column("inspection_id")]
    public long InspectionId { get; set; }

    [Column("asset_id")]
    public long AssetId { get; set; }

    [Column("observed_state")]
    [MaxLength(20)]
    public string ObservedState { get; set; } = "OK"; // 'OK', 'DAMAGED', 'MISSING'

    [Column("observation_notes")]
    [MaxLength(300)]
    public string? ObservationNotes { get; set; }

    [Column("confirmed_at")]
    public DateTime ConfirmedAt { get; set; }

    [ForeignKey(nameof(InspectionId))]
    public virtual Inspection? Inspection { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual RoomAsset? Asset { get; set; }

    public virtual AssetIncident? Incident { get; set; }
}

/// <summary>
/// Đại diện cho sự cố thiết bị được ghi nhận (ASSET_INCIDENT).
/// Tạo ra khi bảo vệ kiểm tra phát hiện thiết bị hư hỏng hoặc thất lạc.
/// Lưu ý: Cột reviewed_by_admin_id do Admin (Người 4) duyệt, Người 3 chỉ xem và cập nhật xử lý bảo dưỡng/sửa chữa.
/// </summary>
[Table("ASSET_INCIDENT")]
public class AssetIncident
{
    [Key]
    [Column("incident_id")]
    public long IncidentId { get; set; }

    [Column("inspection_item_id")]
    public long InspectionItemId { get; set; }

    [Column("incident_type")]
    [MaxLength(30)]
    public string IncidentType { get; set; } = "DAMAGED"; // 'DAMAGED' hoặc 'MISSING'

    [Column("reported_at")]
    public DateTime ReportedAt { get; set; }

    [Column("review_status")]
    [MaxLength(20)]
    public string ReviewStatus { get; set; } = "PENDING"; // 'PENDING', 'CONFIRMED', 'REJECTED'

    [Column("verified_missing")]
    public bool VerifiedMissing { get; set; } = false;

    [Column("reviewed_by_admin_id")]
    public int? ReviewedByAdminId { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("incident_notes")]
    [MaxLength(300)]
    public string? IncidentNotes { get; set; }

    [ForeignKey(nameof(InspectionItemId))]
    public virtual InspectionItem? InspectionItem { get; set; }

    [ForeignKey(nameof(ReviewedByAdminId))]
    public virtual Account? ReviewedByAdmin { get; set; }
}

/// <summary>
/// Đại diện cho bảng nhật ký kiểm toán hệ thống (AUDIT_LOG).
/// Ghi lại toàn bộ thao tác thêm, sửa, đổi trạng thái và bảo trì thiết bị.
/// </summary>
[Table("AUDIT_LOG")]
public class AuditLog
{
    [Key]
    [Column("audit_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long AuditId { get; set; }

    [Column("performed_by_account_id")]
    public int PerformedByAccountId { get; set; }

    [Column("action_type")]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty; // 'CREATE_ASSET', 'UPDATE_ASSET', 'UPDATE_STATUS', 'MAINTENANCE_DONE', 'RETIRE_ASSET'

    [Column("target_entity")]
    [MaxLength(50)]
    public string TargetEntity { get; set; } = "ROOM_ASSET";

    [Column("target_key")]
    [MaxLength(100)]
    public string TargetKey { get; set; } = string.Empty;

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    [Column("before_summary")]
    public string? BeforeSummary { get; set; }

    [Column("after_summary")]
    public string? AfterSummary { get; set; }

    [ForeignKey(nameof(PerformedByAccountId))]
    public virtual Account? PerformedByAccount { get; set; }
}

