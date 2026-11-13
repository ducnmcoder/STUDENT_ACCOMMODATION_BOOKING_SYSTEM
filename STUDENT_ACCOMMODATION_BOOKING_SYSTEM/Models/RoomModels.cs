using System.ComponentModel.DataAnnotations;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

public sealed class RoomViewModel
{
    public int RoomId { get; set; }
    
    [Required(ErrorMessage = "Vui lòng nhập mã phòng.")]
    [StringLength(30, ErrorMessage = "Mã phòng tối đa 30 ký tự.")]
    [Display(Name = "Mã phòng")]
    public string RoomCode { get; set; } = "";
    
    [Required(ErrorMessage = "Vui lòng nhập tòa nhà.")]
    [StringLength(100, ErrorMessage = "Tên tòa nhà tối đa 100 ký tự.")]
    [Display(Name = "Tòa nhà")]
    public string Building { get; set; } = "";
    
    [Required(ErrorMessage = "Vui lòng nhập tầng.")]
    [Range(1, 50, ErrorMessage = "Tầng phải từ 1 đến 50.")]
    [Display(Name = "Tầng")]
    public short Floor { get; set; }
    
    [Required(ErrorMessage = "Vui lòng nhập sức chứa.")]
    [Range(1, 20, ErrorMessage = "Sức chứa phải từ 1 đến 20.")]
    [Display(Name = "Sức chứa")]
    public short Capacity { get; set; }
    
    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [Display(Name = "Trạng thái")]
    public string RoomStatus { get; set; } = "AVAILABLE";
    
    public int? LastConfiguredByAdminId { get; set; }
    public string? LastConfiguredByAdminName { get; set; }
}

public sealed class RoomListViewModel
{
    public List<RoomViewModel> Rooms { get; set; } = [];
    public string? Filter { get; set; }
    public string? StatusFilter { get; set; }
}

public sealed class RoomUsageStatistics
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = "";
    public string Building { get; set; } = "";
    public int TotalBookings { get; set; }
    public int CompletedCheckouts { get; set; }
    public int CancelledBookings { get; set; }
    public decimal OccupancyRate { get; set; }
    public DateTime? LastUsed { get; set; }
}

public sealed class UsageStatisticsViewModel
{
    public List<RoomUsageStatistics> Statistics { get; set; } = [];
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? BuildingFilter { get; set; }
}

public sealed class AssetIncidentViewModel
{
    public long IncidentId { get; set; }
    public long InspectionItemId { get; set; }
    public string IncidentType { get; set; } = "";
    public DateTime ReportedAt { get; set; }
    public string ReviewStatus { get; set; } = "";
    public bool VerifiedMissing { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? IncidentNotes { get; set; }
    
    // Thông tin bổ sung từ join
    public string? AssetName { get; set; }
    public string? RoomCode { get; set; }
    public string? ReporterName { get; set; }
    public int? BookingId { get; set; }
}

public sealed class ReviewIncidentInput
{
    [Required]
    public long IncidentId { get; set; }
    
    [Required]
    public bool VerifiedMissing { get; set; }
    
    [StringLength(300)]
    public string? IncidentNotes { get; set; }
}

public sealed class OccupiedRoomInfo
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = "";
    public string Building { get; set; } = "";
    public short Floor { get; set; }
    public int BookingId { get; set; }
    public string StudentName { get; set; } = "";
    public DateTime CheckInTime { get; set; }
    public DateTime ExpectedCheckout { get; set; }
    public string Status { get; set; } = ""; // IN_USE, OVERDUE
}
