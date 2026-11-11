using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class ReputationRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:AccommodationDb.");

    public async Task<ReputationViewModel> GetStudentHistoryAsync(int studentId)
    {
        var model = new ReputationViewModel();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using (var command = new SqlCommand("""
            SELECT week_start,opening_score,current_score,booking_blocked,updated_at
            FROM dbo.WEEKLY_REPUTATION WHERE student_id=@id ORDER BY week_start DESC
            """, connection))
        {
            command.Parameters.AddWithValue("@id", studentId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                model.Weeks.Add(new(reader.GetDateTime(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetBoolean(3), reader.GetDateTime(4)));
        }
        await using (var command = new SqlCommand("""
            SELECT wr.week_start,e.event_type,e.score_delta,e.occurred_at,e.reason_code,e.event_notes,COALESCE(b.booking_id,ib.booking_id)
            FROM dbo.REPUTATION_EVENT e JOIN dbo.WEEKLY_REPUTATION wr ON wr.weekly_reputation_id=e.weekly_reputation_id
            LEFT JOIN dbo.ATTENDANCE a ON a.attendance_id=e.attendance_id
            LEFT JOIN dbo.CHECKOUT c ON c.checkout_id=e.checkout_id
            LEFT JOIN dbo.BOOKING b ON b.booking_id=COALESCE(a.booking_id,c.booking_id)
            LEFT JOIN dbo.ASSET_INCIDENT i ON i.incident_id=e.incident_id
            LEFT JOIN dbo.INSPECTION_ITEM it ON it.inspection_item_id=i.inspection_item_id
            LEFT JOIN dbo.INSPECTION p ON p.inspection_id=it.inspection_id
            LEFT JOIN dbo.BOOKING ib ON ib.booking_id=p.booking_id
            WHERE wr.student_id=@id ORDER BY e.occurred_at DESC,e.event_id DESC
            """, connection))
        {
            command.Parameters.AddWithValue("@id", studentId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                model.Events.Add(new(reader.GetDateTime(0), reader.GetString(1), reader.GetInt32(2), reader.GetDateTime(3),
                    reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetInt64(6)));
        }
        return model;
    }

    public async Task<AdminReputationViewModel> GetAdminHistoryAsync()
    {
        var model = new AdminReputationViewModel();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using (var command = new SqlCommand("""
            SELECT wr.student_id,s.student_code,a.full_name,wr.week_start,wr.current_score,wr.booking_blocked
            FROM dbo.WEEKLY_REPUTATION wr JOIN dbo.STUDENT s ON s.account_id=wr.student_id
            JOIN dbo.ACCOUNT a ON a.account_id=s.account_id ORDER BY wr.week_start DESC,a.full_name
            """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                model.Weeks.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3), reader.GetInt32(4), reader.GetBoolean(5)));
        }
        await using (var command = new SqlCommand("""
            SELECT s.student_code,a.full_name,wr.week_start,e.event_type,e.score_delta,e.occurred_at,e.reason_code,e.event_notes
            FROM dbo.REPUTATION_EVENT e JOIN dbo.WEEKLY_REPUTATION wr ON wr.weekly_reputation_id=e.weekly_reputation_id
            JOIN dbo.STUDENT s ON s.account_id=wr.student_id JOIN dbo.ACCOUNT a ON a.account_id=s.account_id
            ORDER BY e.occurred_at DESC,e.event_id DESC
            """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                model.Events.Add(new(reader.GetString(0), reader.GetString(1), reader.GetDateTime(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7)));
        }
        return model;
    }
}
