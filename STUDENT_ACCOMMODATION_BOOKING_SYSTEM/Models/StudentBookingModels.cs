using System.ComponentModel.DataAnnotations;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

public sealed record RoomItem(int Id, string Code, string Building, short Floor, short Capacity,
    string Status, IReadOnlyList<string> Assets);
public sealed record AvailableSlot(DateTime StartAt, DateTime EndAt);
public sealed record AvailableRoom(RoomItem Room, IReadOnlyList<AvailableSlot> Slots);

public sealed record BookingItem(long Id, string RoomCode, string Building, short Floor,
    DateTime StartAt, DateTime EndAt, string Status, string? Purpose, DateTime CreatedAt,
    bool CanCancel);

public sealed record BookingEligibility(string EnrollmentStatus, int? Score,
    int? Threshold, int? WeeklyLimit, int BookingsThisWeek, bool HasActivePolicy);

public sealed class RoomsViewModel
{
    public IReadOnlyList<RoomItem> Rooms { get; init; } = [];
    public IReadOnlyList<string> Buildings { get; init; } = [];
    public BookingEligibility Eligibility { get; init; } = new("", null, null, null, 0, false);
}

public sealed class AvailabilityQuery
{
    public string? Building { get; set; }
    public short? Floor { get; set; }
    public short? Capacity { get; set; }
    public DateOnly? Date { get; set; }
}

public sealed class BookRoomInput
{
    [Range(1, int.MaxValue)] public int RoomId { get; set; }
    [Required] public DateTimeOffset? StartAt { get; set; }
    [Required] public DateTimeOffset? EndAt { get; set; }
    [StringLength(300)] public string? Purpose { get; set; }
}

public sealed class CancelBookingInput
{
    [Range(1, long.MaxValue)] public long BookingId { get; set; }
    [StringLength(300)] public string? Reason { get; set; }
}
