using Microsoft.Data.SqlClient;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class ReputationService(IConfiguration configuration, ILogger<ReputationService> logger)
{
    private readonly string _connectionString = configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:AccommodationDb.");

    private SqlConnection Connection() => new(_connectionString);

    // Trừ điểm khi có tài sản thất lạc được admin xác nhận
    public async Task DeductPointsForMissingAssetAsync(int bookingId, string assetName, string reason)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        
        try
        {
            // Lấy student_id từ booking
            await using var getStudent = new SqlCommand(
                "SELECT student_id FROM dbo.BOOKING WHERE booking_id = @bookingId", connection, transaction);
            getStudent.Parameters.AddWithValue("@bookingId", bookingId);
            var studentIdObj = await getStudent.ExecuteScalarAsync();
            
            if (studentIdObj == null)
                throw new InvalidOperationException($"Không tìm thấy booking {bookingId}");
            
            var studentId = (int)studentIdObj;
            
            // Trừ điểm uy tín (ví dụ: -10 điểm cho tài sản thất lạc)
            const int deductionPoints = -10;
            const string eventType = "MISSING_ASSET";
            
            await using var insertEvent = new SqlCommand("""
                INSERT INTO dbo.REPUTATION_EVENT (student_id, event_type, point_change, event_date, event_description)
                VALUES (@studentId, @eventType, @pointChange, GETDATE(), @description)
                """, connection, transaction);
            
            insertEvent.Parameters.AddWithValue("@studentId", studentId);
            insertEvent.Parameters.AddWithValue("@eventType", eventType);
            insertEvent.Parameters.AddWithValue("@pointChange", deductionPoints);
            insertEvent.Parameters.AddWithValue("@description", 
                $"Thất lạc tài sản: {assetName}. {reason}");
            
            await insertEvent.ExecuteNonQueryAsync();
            
            logger.LogInformation(
                "Trừ {Points} điểm uy tín cho sinh viên {StudentId} do thất lạc {Asset}",
                deductionPoints, studentId, assetName);
            
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Không thể trừ điểm uy tín cho booking {BookingId}", bookingId);
            throw;
        }
    }

    // Lấy tổng điểm uy tín của sinh viên
    public async Task<int> GetStudentReputationScoreAsync(int studentId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            SELECT ISNULL(SUM(point_change), 0)
            FROM dbo.REPUTATION_EVENT
            WHERE student_id = @studentId
            """, connection);
        
        command.Parameters.AddWithValue("@studentId", studentId);
        
        var result = await command.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // Lấy điểm uy tín tuần hiện tại
    public async Task<int> GetWeeklyReputationScoreAsync(int studentId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        // Lấy tuần hiện tại (ISO week)
        var today = DateTime.Today;
        var year = today.Year;
        var weekNumber = GetIso8601WeekOfYear(today);
        
        await using var command = new SqlCommand("""
            SELECT total_points
            FROM dbo.WEEKLY_REPUTATION
            WHERE student_id = @studentId AND year = @year AND week_number = @weekNumber
            """, connection);
        
        command.Parameters.AddWithValue("@studentId", studentId);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@weekNumber", weekNumber);
        
        var result = await command.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // Helper: Tính ISO 8601 week number
    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= System.DayOfWeek.Monday && day <= System.DayOfWeek.Wednesday)
            date = date.AddDays(3);
        
        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
    }
}
