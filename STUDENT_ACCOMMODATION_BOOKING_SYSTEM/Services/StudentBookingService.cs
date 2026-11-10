using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Services;

public sealed class StudentBookingService(BookingDbContext db, IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:AccommodationDb.");

    public async Task<IReadOnlyList<RoomItem>> GetRoomsAsync()
    {
        var rooms = await db.Rooms.AsNoTracking().OrderBy(r => r.Building).ThenBy(r => r.Floor)
            .ThenBy(r => r.Code).ToListAsync();
        return await WithAssetsAsync(rooms);
    }

    public static DateTime VietnamDayStartUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);

    public async Task<IReadOnlyList<AvailableRoom>> GetAvailableRoomsAsync(AvailabilityQuery filter)
    {
        var query = db.Rooms.AsNoTracking().Where(r => r.Status == "AVAILABLE");
        if (!string.IsNullOrWhiteSpace(filter.Building)) query = query.Where(r => r.Building == filter.Building);
        if (filter.Floor.HasValue) query = query.Where(r => r.Floor == filter.Floor.Value);
        if (filter.Capacity.HasValue) query = query.Where(r => r.Capacity >= filter.Capacity.Value);
        var rooms = await query.OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.Code).ToListAsync();
        var roomItems = await WithAssetsAsync(rooms);
        var ids = rooms.Select(r => r.Id).ToArray();
        if (ids.Length == 0) return [];

        var dayStart = VietnamDayStartUtc(filter.Date!.Value);
        var dayEnd = dayStart.AddDays(1);
        var now = DateTime.UtcNow;
        var roundedNow = new DateTime((now.Ticks + TimeSpan.TicksPerMinute - 1) /
            TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var firstAvailable = dayStart > roundedNow ? dayStart : roundedNow;
        if (firstAvailable >= dayEnd) return [];
        var occupied = await db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.RoomId) && (b.Status == "PENDING" || b.Status == "APPROVED") &&
                b.StartAt < dayEnd && dayStart < b.EndAt)
            .OrderBy(b => b.StartAt).Select(b => new { b.RoomId, b.StartAt, b.EndAt }).ToListAsync();
        var byRoom = occupied.GroupBy(b => b.RoomId).ToDictionary(g => g.Key, g => g.ToArray());
        var result = new List<AvailableRoom>();
        foreach (var room in roomItems)
        {
            var slots = new List<AvailableSlot>();
            var cursor = firstAvailable;
            if (byRoom.TryGetValue(room.Id, out var reservations))
            {
                foreach (var reservation in reservations)
                {
                    var blockedStart = reservation.StartAt < dayStart ? dayStart : reservation.StartAt;
                    var blockedEnd = reservation.EndAt > dayEnd ? dayEnd : reservation.EndAt;
                    if (blockedStart > cursor)
                        slots.Add(new AvailableSlot(cursor, DateTime.SpecifyKind(blockedStart, DateTimeKind.Utc)));
                    if (blockedEnd > cursor) cursor = DateTime.SpecifyKind(blockedEnd, DateTimeKind.Utc);
                    if (cursor >= dayEnd) break;
                }
            }
            if (cursor < dayEnd) slots.Add(new AvailableSlot(cursor, dayEnd));
            if (slots.Count > 0) result.Add(new AvailableRoom(room, slots));
        }
        return result;
    }

    private async Task<IReadOnlyList<RoomItem>> WithAssetsAsync(List<Room> rooms)
    {
        var ids = rooms.Select(r => r.Id).ToArray();
        if (ids.Length == 0) return [];
        var assets = await db.RoomAssets.AsNoTracking()
            .Where(a => ids.Contains(a.RoomId) && a.Status == "ACTIVE")
            .OrderBy(a => a.Name).Select(a => new { a.RoomId, a.Name }).ToListAsync();
        var assetNames = assets.GroupBy(a => a.RoomId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(a => a.Name).ToArray());
        return rooms.Select(r => new RoomItem(r.Id, r.Code, r.Building, r.Floor,
            r.Capacity, r.Status, assetNames.GetValueOrDefault(r.Id) ?? [])).ToArray();
    }

    public Task<List<string>> GetBuildingsAsync() => db.Rooms.AsNoTracking()
        .Select(r => r.Building).Distinct().OrderBy(b => b).ToListAsync();

    public async Task<BookingEligibility> GetEligibilityAsync(int studentId)
    {
        var now = DateTime.UtcNow;
        var localToday = DateOnly.FromDateTime(now.AddHours(7));
        var daysSinceMonday = ((int)localToday.DayOfWeek + 6) % 7;
        var weekStart = VietnamDayStartUtc(localToday.AddDays(-daysSinceMonday));
        var monthStartLocal = new DateOnly(localToday.Year, localToday.Month, 1);
        var monthStart = VietnamDayStartUtc(monthStartLocal);
        var monthEnd = VietnamDayStartUtc(monthStartLocal.AddMonths(1));
        var enrollment = await db.Students.AsNoTracking().Where(s => s.AccountId == studentId)
            .Select(s => s.EnrollmentStatus).SingleOrDefaultAsync() ?? "";
        var policy = await db.Policies.AsNoTracking().Where(p => p.EffectiveFrom <= now &&
            (p.EffectiveTo == null || p.EffectiveTo > now))
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Id).FirstOrDefaultAsync();
        var delta = await (from e in db.ReputationEvents.AsNoTracking()
                           join w in db.WeeklyReputations.AsNoTracking() on e.WeeklyReputationId equals w.Id
                           where w.StudentId == studentId && e.OccurredAt >= monthStart && e.OccurredAt < monthEnd
                           select (int?)e.ScoreDelta).SumAsync() ?? 0;
        var used = await db.Bookings.AsNoTracking().CountAsync(b => b.StudentId == studentId &&
            b.CreatedAt >= weekStart && b.CreatedAt < weekStart.AddDays(7) && b.Status != "REJECTED");
        return new BookingEligibility(enrollment,
            policy is null ? null : policy.MonthlyInitialScore + delta,
            policy?.BookingBlockBelow, policy?.WeeklyBookingLimit, used, policy is not null);
    }

    public async Task<IReadOnlyList<BookingItem>> GetBookingsAsync(int studentId)
    {
        var now = DateTime.UtcNow;
        return await (from booking in db.Bookings.AsNoTracking()
                      join room in db.Rooms.AsNoTracking() on booking.RoomId equals room.Id
                      where booking.StudentId == studentId
                      orderby booking.StartAt descending, booking.Id descending
                      select new BookingItem(booking.Id, room.Code, room.Building, room.Floor,
                          booking.StartAt, booking.EndAt, booking.Status, booking.Purpose,
                          booking.CreatedAt,
                          (booking.Status == "PENDING" || booking.Status == "APPROVED") && booking.StartAt > now))
            .ToListAsync();
    }

    public async Task<long> BookAsync(int studentId, BookRoomInput input)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("dbo.usp_BookRoom", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@student_id", SqlDbType.Int).Value = studentId;
        command.Parameters.Add("@room_id", SqlDbType.Int).Value = input.RoomId;
        command.Parameters.Add("@start_at", SqlDbType.DateTime2).Value = input.StartAt!.Value.UtcDateTime;
        command.Parameters.Add("@end_at", SqlDbType.DateTime2).Value = input.EndAt!.Value.UtcDateTime;
        command.Parameters.Add("@purpose", SqlDbType.NVarChar, 300).Value =
            string.IsNullOrWhiteSpace(input.Purpose) ? DBNull.Value : input.Purpose.Trim();
        var bookingId = command.Parameters.Add("@booking_id", SqlDbType.BigInt);
        bookingId.Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync();
        return (long)bookingId.Value;
    }

    public async Task CancelAsync(int studentId, CancelBookingInput input)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("dbo.usp_CancelBooking", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@booking_id", SqlDbType.BigInt).Value = input.BookingId;
        command.Parameters.Add("@student_id", SqlDbType.Int).Value = studentId;
        command.Parameters.Add("@reason", SqlDbType.NVarChar, 300).Value =
            string.IsNullOrWhiteSpace(input.Reason) ? DBNull.Value : input.Reason.Trim();
        await command.ExecuteNonQueryAsync();
    }
}
