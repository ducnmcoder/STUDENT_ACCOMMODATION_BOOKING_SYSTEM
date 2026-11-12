
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class GuardBookingRepository(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Missing connection string.");

    public async Task<List<GuardBookingViewModel>> GetApprovedBookingsAsync()
    {
        var bookings = new List<GuardBookingViewModel>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        string sql = """
            SELECT
                b.booking_id,
                a.full_name,
                s.student_code,
                r.room_code,
                b.start_at,
                b.end_at,
                b.booking_status,
                att.outcome
            FROM dbo.BOOKING b
            INNER JOIN dbo.STUDENT s ON b.student_id = s.account_id
            INNER JOIN dbo.ACCOUNT a ON s.account_id = a.account_id
            INNER JOIN dbo.ROOM r ON b.room_id = r.room_id
            LEFT JOIN dbo.ATTENDANCE att ON b.booking_id = att.booking_id
            WHERE b.booking_status = 'APPROVED'
            ORDER BY b.start_at, b.booking_id
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            bookings.Add(new GuardBookingViewModel
            {
                BookingId = reader.GetInt64(0),
                StudentName = reader.GetString(1),
                StudentCode = reader.GetString(2),
                RoomCode = reader.GetString(3),
                StartAt = reader.GetDateTime(4),
                EndAt = reader.GetDateTime(5),
                BookingStatus = reader.GetString(6),
                CheckInOutcome = reader.IsDBNull(7)
                    ? null
                    : reader.GetString(7)
            });
        }

        return bookings;
    }

    public async Task<bool> CheckInAsync(long bookingId, int guardId)
    {
        const string sql = """
        DECLARE @now datetime2(0) = SYSUTCDATETIME();

        INSERT INTO dbo.ATTENDANCE
        (
            booking_id,
            guard_id,
            outcome,
            actual_checkin_at,
            recorded_at,
            late_minutes
        )
        SELECT
            b.booking_id,
            @guardId,
            CASE
                WHEN @now <= b.start_at THEN 'ON_TIME'
                ELSE 'LATE'
            END,
            @now,
            @now,
            CASE
                WHEN @now <= b.start_at THEN 0
                ELSE (DATEDIFF(SECOND, b.start_at, @now) + 59) / 60
            END
        FROM dbo.BOOKING b
        INNER JOIN dbo.SECURITY_GUARD g
            ON g.account_id = @guardId
        WHERE b.booking_id = @bookingId
          AND b.booking_status = 'APPROVED'
          AND @now < b.end_at
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ATTENDANCE a
              WHERE a.booking_id = b.booking_id
          );

        SELECT @@ROWCOUNT;
        """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@guardId", guardId);

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    public async Task<bool> CheckOutAsync(long bookingId, int guardId)
    {
        const string sql = """
        SET XACT_ABORT ON;

        DECLARE @now datetime2(0) = SYSUTCDATETIME();

        BEGIN TRANSACTION;

        UPDATE dbo.BOOKING
        SET booking_status = 'COMPLETED'
        WHERE booking_id = @bookingId
          AND booking_status = 'APPROVED'
          AND EXISTS (
              SELECT 1
              FROM dbo.SECURITY_GUARD
              WHERE account_id = @guardId
          )
          AND EXISTS (
              SELECT 1
              FROM dbo.ATTENDANCE
              WHERE booking_id = @bookingId
                AND outcome IN ('ON_TIME', 'LATE')
          )
          AND NOT EXISTS (
              SELECT 1
              FROM dbo.CHECKOUT
              WHERE booking_id = @bookingId
          );

        DECLARE @updated int = @@ROWCOUNT;

        IF @updated = 1
        BEGIN
            INSERT INTO dbo.CHECKOUT
            (
                booking_id,
                guard_id,
                actual_checkout_at,
                recorded_at,
                late_minutes
            )
            SELECT
                @bookingId,
                @guardId,
                @now,
                @now,
                CASE
                    WHEN @now <= end_at THEN 0
                    ELSE (DATEDIFF(SECOND, end_at, @now) + 59) / 60
                END
            FROM dbo.BOOKING
            WHERE booking_id = @bookingId;
        END

        COMMIT TRANSACTION;

        SELECT @updated;
        """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@guardId", guardId);

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }
}
