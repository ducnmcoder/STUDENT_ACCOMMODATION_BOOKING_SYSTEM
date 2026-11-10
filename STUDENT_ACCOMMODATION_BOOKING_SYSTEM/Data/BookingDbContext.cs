using Microsoft.EntityFrameworkCore;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomAsset> RoomAssets => Set<RoomAsset>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<BookingPolicy> Policies => Set<BookingPolicy>();
    public DbSet<WeeklyReputation> WeeklyReputations => Set<WeeklyReputation>();
    public DbSet<ReputationEvent> ReputationEvents => Set<ReputationEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Room>(e => {
            e.ToTable("ROOM", "dbo"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("room_id");
            e.Property(x => x.Code).HasColumnName("room_code");
            e.Property(x => x.Building).HasColumnName("building");
            e.Property(x => x.Floor).HasColumnName("floor");
            e.Property(x => x.Capacity).HasColumnName("capacity");
            e.Property(x => x.Status).HasColumnName("room_status");
        });
        model.Entity<RoomAsset>(e => {
            e.ToTable("ROOM_ASSET", "dbo"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("asset_id");
            e.Property(x => x.RoomId).HasColumnName("room_id");
            e.Property(x => x.Name).HasColumnName("asset_name");
            e.Property(x => x.Status).HasColumnName("asset_status");
        });
        model.Entity<Booking>(e => {
            e.ToTable("BOOKING", "dbo"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("booking_id");
            e.Property(x => x.StudentId).HasColumnName("student_id");
            e.Property(x => x.RoomId).HasColumnName("room_id");
            e.Property(x => x.StartAt).HasColumnName("start_at");
            e.Property(x => x.EndAt).HasColumnName("end_at");
            e.Property(x => x.Status).HasColumnName("booking_status");
            e.Property(x => x.Purpose).HasColumnName("purpose");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
        model.Entity<Student>(e => {
            e.ToTable("STUDENT", "dbo"); e.HasKey(x => x.AccountId);
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.EnrollmentStatus).HasColumnName("enrollment_status");
        });
        model.Entity<BookingPolicy>(e => {
            e.ToTable("POLICY", "dbo"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("policy_id");
            e.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
            e.Property(x => x.EffectiveTo).HasColumnName("effective_to");
            e.Property(x => x.BookingBlockBelow).HasColumnName("booking_block_below");
            e.Property(x => x.MonthlyInitialScore).HasColumnName("monthly_initial_score");
            e.Property(x => x.WeeklyBookingLimit).HasColumnName("weekly_booking_limit");
        });
        model.Entity<WeeklyReputation>(e => {
            e.ToTable("WEEKLY_REPUTATION", "dbo"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("weekly_reputation_id");
            e.Property(x => x.StudentId).HasColumnName("student_id");
            e.Property(x => x.WeekStart).HasColumnName("week_start");
            e.Property(x => x.CurrentScore).HasColumnName("current_score");
        });
        model.Entity<ReputationEvent>(e => {
            e.ToTable("REPUTATION_EVENT", "dbo"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("event_id");
            e.Property(x => x.WeeklyReputationId).HasColumnName("weekly_reputation_id");
            e.Property(x => x.ScoreDelta).HasColumnName("score_delta");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        });
    }
}

public sealed class Room
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Building { get; set; } = "";
    public short Floor { get; set; }
    public short Capacity { get; set; }
    public string Status { get; set; } = "";
}

public sealed class RoomAsset
{
    public long Id { get; set; }
    public int RoomId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class Booking
{
    public long Id { get; set; }
    public int StudentId { get; set; }
    public int RoomId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "";
    public string? Purpose { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Student
{
    public int AccountId { get; set; }
    public string EnrollmentStatus { get; set; } = "";
}

public sealed class BookingPolicy
{
    public int Id { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int BookingBlockBelow { get; set; }
    public int MonthlyInitialScore { get; set; }
    public int WeeklyBookingLimit { get; set; }
}

public sealed class WeeklyReputation
{
    public long Id { get; set; }
    public int StudentId { get; set; }
    public DateTime WeekStart { get; set; }
    public int CurrentScore { get; set; }
}

public sealed class ReputationEvent
{
    public long Id { get; set; }
    public long WeeklyReputationId { get; set; }
    public int ScoreDelta { get; set; }
    public DateTime OccurredAt { get; set; }
}
