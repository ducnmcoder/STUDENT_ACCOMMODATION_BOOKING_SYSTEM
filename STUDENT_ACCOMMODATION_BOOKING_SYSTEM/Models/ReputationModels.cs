namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

public sealed class ReputationViewModel
{
    public List<WeeklyReputationItem> Weeks { get; } = [];
    public List<ReputationEventItem> Events { get; } = [];
}

public sealed class AdminReputationViewModel
{
    public List<StudentReputationItem> Weeks { get; } = [];
    public List<AdminReputationEventItem> Events { get; } = [];
}

public sealed record WeeklyReputationItem(DateTime WeekStart, int OpeningScore, int CurrentScore, bool BookingBlocked, DateTime UpdatedAt);
public sealed record ReputationEventItem(DateTime WeekStart, string EventType, int ScoreDelta, DateTime OccurredAt, string ReasonCode, string? Notes, long? BookingId);
public sealed record StudentReputationItem(int StudentId, string StudentCode, string FullName, DateTime WeekStart, int CurrentScore, bool BookingBlocked);
public sealed record AdminReputationEventItem(string StudentCode, string FullName, DateTime WeekStart, string EventType, int ScoreDelta, DateTime OccurredAt, string ReasonCode, string? Notes);
