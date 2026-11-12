
namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

public class GuardInspectionBooking
{
    public long BookingId { get; set; }
    public string StudentName { get; set; } = "";
    public string RoomCode { get; set; } = "";
    public string BookingStatus { get; set; } = "";
    public bool HasIn { get; set; }
    public bool HasOut { get; set; }
}

public class GuardInspectionAsset
{
    public long AssetId { get; set; }
    public string AssetTag { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string RegisteredCondition { get; set; } = "";
    public string ObservedState { get; set; } = "OK";
    public string? ObservationNotes { get; set; }
}

public class GuardInspectionForm
{
    public long BookingId { get; set; }
    public string Phase { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string RoomCode { get; set; } = "";
    public string? InspectionNotes { get; set; }
    public List<GuardInspectionAsset> Items { get; set; } = new();
}
