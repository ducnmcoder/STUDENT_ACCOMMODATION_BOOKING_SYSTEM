
namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

public class GuardBookingViewModel
{
    public long BookingId { get; set; }

    public string StudentName { get; set; } = "";

    public string StudentCode { get; set; } = "";

    public string RoomCode { get; set; } = "";

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string BookingStatus { get; set; } = "";

    public string? CheckInOutcome { get; set; }
}
