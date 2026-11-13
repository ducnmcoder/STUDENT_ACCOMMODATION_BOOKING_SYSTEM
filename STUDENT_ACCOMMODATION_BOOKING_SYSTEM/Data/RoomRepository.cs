/*  */using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class RoomRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:AccommodationDb.");

    private SqlConnection Connection() => new(_connectionString);

    // CRUD ROOM
    public async Task<List<RoomViewModel>> GetAllRoomsAsync(string? filter = null, string? statusFilter = null)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        var sql = """
            SELECT r.room_id, r.room_code, r.building, r.floor, r.capacity, r.room_status, 
                   r.last_configured_by_admin_id, a.full_name
            FROM dbo.ROOM r
            LEFT JOIN dbo.ACCOUNT a ON a.account_id = r.last_configured_by_admin_id
            WHERE 1=1
            """;
        
        if (!string.IsNullOrWhiteSpace(filter))
            sql += " AND (r.room_code LIKE @filter OR r.building LIKE @filter)";
        if (!string.IsNullOrWhiteSpace(statusFilter))
            sql += " AND r.room_status = @statusFilter";
        
        sql += " ORDER BY r.building, r.floor, r.room_code";
        
        Console.WriteLine($"Filter: '{filter}', StatusFilter: '{statusFilter}'");
        Console.WriteLine($"SQL: {sql}");
        
        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(filter))
            command.Parameters.AddWithValue("@filter", $"%{filter}%");
        if (!string.IsNullOrWhiteSpace(statusFilter))
            command.Parameters.AddWithValue("@statusFilter", statusFilter);
        
        var rooms = new List<RoomViewModel>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rooms.Add(new RoomViewModel
            {
                RoomId = reader.GetInt32(0),           // room_id
                RoomCode = reader.GetString(1),        // room_code
                Building = reader.GetString(2),        // building
                Floor = reader.GetInt16(3),            // floor
                Capacity = reader.GetInt16(4),         // capacity
                RoomStatus = reader.GetString(5),      // room_status
                LastConfiguredByAdminId = reader.IsDBNull(6) ? null : reader.GetInt32(6), // last_configured_by_admin_id
                LastConfiguredByAdminName = reader.IsDBNull(7) ? null : reader.GetString(7) // full_name
            });
        }
        
        Console.WriteLine($"Returned {rooms.Count} rooms");
        return rooms;
    }

    public async Task<RoomViewModel?> GetRoomByIdAsync(int roomId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            SELECT r.room_id, r.room_code, r.building, r.floor, r.capacity, r.room_status, 
                   r.last_configured_by_admin_id, a.full_name
            FROM dbo.ROOM r
            LEFT JOIN dbo.ACCOUNT a ON a.account_id = r.last_configured_by_admin_id
            WHERE r.room_id = @roomId
            """, connection);
        command.Parameters.AddWithValue("@roomId", roomId);
        
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        
        return new RoomViewModel
        {
            RoomId = reader.GetInt32(0),           // room_id
            RoomCode = reader.GetString(1),        // room_code
            Building = reader.GetString(2),        // building
            Floor = reader.GetInt16(3),            // floor
            Capacity = reader.GetInt16(4),         // capacity
            RoomStatus = reader.GetString(5),      // room_status
            LastConfiguredByAdminId = reader.IsDBNull(6) ? null : reader.GetInt32(6), // last_configured_by_admin_id
            LastConfiguredByAdminName = reader.IsDBNull(7) ? null : reader.GetString(7) // full_name
        };
    }

    public async Task<int> CreateRoomAsync(RoomViewModel room, int adminAccountId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            INSERT INTO dbo.ROOM (room_code, building, floor, capacity, room_status, last_configured_by_admin_id)
            VALUES (@roomCode, @building, @floor, @capacity, @roomStatus, @adminAccountId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, connection);
        
        command.Parameters.AddWithValue("@roomCode", room.RoomCode.Trim());
        command.Parameters.AddWithValue("@building", room.Building.Trim());
        command.Parameters.AddWithValue("@floor", room.Floor);
        command.Parameters.AddWithValue("@capacity", room.Capacity);
        command.Parameters.AddWithValue("@roomStatus", room.RoomStatus);
        command.Parameters.AddWithValue("@adminAccountId", adminAccountId);
        
        var roomId = await command.ExecuteScalarAsync();
        return (int)roomId!;
    }

    public async Task UpdateRoomAsync(RoomViewModel room, int adminAccountId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            UPDATE dbo.ROOM
            SET room_code = @roomCode,
                building = @building,
                floor = @floor,
                capacity = @capacity,
                room_status = @roomStatus,
                last_configured_by_admin_id = @adminAccountId
            WHERE room_id = @roomId
            """, connection);
        
        command.Parameters.AddWithValue("@roomId", room.RoomId);
        command.Parameters.AddWithValue("@roomCode", room.RoomCode.Trim());
        command.Parameters.AddWithValue("@building", room.Building.Trim());
        command.Parameters.AddWithValue("@floor", room.Floor);
        command.Parameters.AddWithValue("@capacity", room.Capacity);
        command.Parameters.AddWithValue("@roomStatus", room.RoomStatus);
        command.Parameters.AddWithValue("@adminAccountId", adminAccountId);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0) throw new InvalidOperationException("Phòng không tồn tại.");
    }

    public async Task DeleteRoomAsync(int roomId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            DELETE FROM dbo.ROOM WHERE room_id = @roomId
            """, connection);
        command.Parameters.AddWithValue("@roomId", roomId);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0) throw new InvalidOperationException("Phòng không tồn tại.");
    }

    // Thống kê sử dụng phòng
    public async Task<List<dynamic>> GetUsageStatisticsAsync(DateTime? startDate, DateTime? endDate)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        var sql = """
            SELECT 
                r.room_code AS RoomNumber,
                COUNT(DISTINCT b.booking_id) AS BookingCount,
                ISNULL(SUM(DATEDIFF(DAY, b.start_at, b.end_at)), 0) AS TotalDays
            FROM dbo.ROOM r
            LEFT JOIN dbo.BOOKING b ON b.room_id = r.room_id
            """;
        
        var hasWhere = false;
        if (startDate.HasValue)
        {
            sql += " AND b.start_at >= @startDate ";
            hasWhere = true;
        }
        if (endDate.HasValue)
        {
            sql += " AND b.end_at <= @endDate ";
            hasWhere = true;
        }
        
        sql += " GROUP BY r.room_code HAVING COUNT(DISTINCT b.booking_id) > 0 ORDER BY r.room_code";
        
        await using var command = new SqlCommand(sql, connection);
        if (startDate.HasValue)
            command.Parameters.AddWithValue("@startDate", startDate.Value);
        if (endDate.HasValue)
            command.Parameters.AddWithValue("@endDate", endDate.Value);
        
        var statistics = new List<dynamic>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            statistics.Add(new
            {
                RoomNumber = reader.GetString(0),
                BookingCount = reader.GetInt32(1),
                TotalDays = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
            });
        }
        return statistics;
    }

    // Lấy danh sách phòng đang được sử dụng
    public async Task<List<dynamic>> GetOccupiedRoomsAsync()
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            SELECT 
                r.room_code AS RoomNumber,
                r.floor AS Floor,
                'Standard' AS RoomType,
                acc.full_name AS StudentName,
                acc.email AS StudentEmail,
                att.actual_checkin_at AS CheckInDate,
                DATEDIFF(DAY, att.actual_checkin_at, GETDATE()) AS DaysOccupied
            FROM dbo.ROOM r
            INNER JOIN dbo.BOOKING b ON b.room_id = r.room_id
            INNER JOIN dbo.ATTENDANCE att ON att.booking_id = b.booking_id
            INNER JOIN dbo.STUDENT s ON s.account_id = b.student_id
            INNER JOIN dbo.ACCOUNT acc ON acc.account_id = s.account_id
            LEFT JOIN dbo.CHECKOUT c ON c.booking_id = b.booking_id
            WHERE c.checkout_id IS NULL
              AND att.actual_checkin_at IS NOT NULL
            ORDER BY r.building, r.floor, r.room_code
            """, connection);
        
        var occupiedRooms = new List<dynamic>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            occupiedRooms.Add(new
            {
                RoomNumber = reader.GetString(0),
                Floor = reader.GetInt16(1),
                RoomType = reader.GetString(2),
                StudentName = reader.GetString(3),
                StudentEmail = reader.GetString(4),
                CheckInDate = reader.GetDateTime(5),
                DaysOccupied = reader.GetInt32(6)
            });
        }
        return occupiedRooms;
    }

    // Lấy danh sách asset incident chờ duyệt
    public async Task<List<dynamic>> GetPendingIncidentsAsync()
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            SELECT 
                ai.incident_id AS IncidentId,
                r.room_code AS RoomNumber,
                acc.full_name AS StudentName,
                ra.asset_name AS AssetType,
                ai.incident_notes AS Description,
                ai.reported_at AS ReportedAt,
                ai.review_status AS ReviewStatus,
                ai.incident_type AS Severity
            FROM dbo.ASSET_INCIDENT ai
            INNER JOIN dbo.INSPECTION_ITEM ii ON ii.inspection_item_id = ai.inspection_item_id
            INNER JOIN dbo.INSPECTION i ON i.inspection_id = ii.inspection_id
            INNER JOIN dbo.BOOKING b ON b.booking_id = i.booking_id
            INNER JOIN dbo.ROOM r ON r.room_id = b.room_id
            INNER JOIN dbo.ROOM_ASSET ra ON ra.asset_id = ii.asset_id
            INNER JOIN dbo.STUDENT s ON s.account_id = b.student_id
            INNER JOIN dbo.ACCOUNT acc ON acc.account_id = s.account_id
            ORDER BY ai.reported_at DESC
            """, connection);
        
        var incidents = new List<dynamic>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            incidents.Add(new
            {
                IncidentId = reader.GetInt64(0),
                RoomNumber = reader.GetString(1),
                StudentName = reader.GetString(2),
                AssetType = reader.GetString(3),
                Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ReportedAt = reader.GetDateTime(5),
                ReviewStatus = reader.GetString(6),
                Severity = reader.GetString(7)
            });
        }
        return incidents;
    }

    public async Task<dynamic?> GetIncidentByIdAsync(long incidentId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            SELECT 
                ai.incident_id AS IncidentId,
                r.room_code AS RoomNumber,
                acc.full_name AS StudentName,
                ai.reported_at AS ReportedAt,
                ra.asset_name AS AssetType,
                ai.incident_type AS Severity,
                ai.incident_notes AS Description,
                ai.review_status AS ReviewStatus,
                ai.reviewed_by_admin_id AS ReviewedByAdminId,
                ai.reviewed_at AS ReviewedAt,
                0 AS PointsDeducted,
                '' AS AdminNotes,
                b.booking_id AS BookingId
            FROM dbo.ASSET_INCIDENT ai
            INNER JOIN dbo.INSPECTION_ITEM ii ON ii.inspection_item_id = ai.inspection_item_id
            INNER JOIN dbo.INSPECTION i ON i.inspection_id = ii.inspection_id
            INNER JOIN dbo.BOOKING b ON b.booking_id = i.booking_id
            INNER JOIN dbo.ROOM r ON r.room_id = b.room_id
            INNER JOIN dbo.ROOM_ASSET ra ON ra.asset_id = ii.asset_id
            INNER JOIN dbo.STUDENT s ON s.account_id = b.student_id
            INNER JOIN dbo.ACCOUNT acc ON acc.account_id = s.account_id
            WHERE ai.incident_id = @incidentId
            """, connection);
        command.Parameters.AddWithValue("@incidentId", incidentId);
        
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        
        return new
        {
            IncidentId = reader.GetInt64(0),
            RoomNumber = reader.GetString(1),
            StudentName = reader.GetString(2),
            ReportedAt = reader.GetDateTime(3),
            AssetType = reader.GetString(4),
            Severity = reader.GetString(5),
            Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
            ReviewStatus = reader.GetString(7),
            ReviewedByAdminId = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
            ReviewedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
            PointsDeducted = reader.GetInt32(10),
            AdminNotes = reader.GetString(11),
            BookingId = (int)reader.GetInt64(12)
        };
    }

    public async Task ReviewIncidentAsync(long incidentId, int adminAccountId, bool verifiedMissing, string? notes)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            UPDATE dbo.ASSET_INCIDENT
            SET review_status = @reviewStatus,
                verified_missing = @verifiedMissing,
                reviewed_by_admin_id = @adminAccountId,
                reviewed_at = GETDATE(),
                incident_notes = @notes
            WHERE incident_id = @incidentId
            """, connection);
        
        command.Parameters.AddWithValue("@incidentId", incidentId);
        command.Parameters.AddWithValue("@reviewStatus", verifiedMissing ? "APPROVED" : "REJECTED");
        command.Parameters.AddWithValue("@verifiedMissing", verifiedMissing);
        command.Parameters.AddWithValue("@adminAccountId", adminAccountId);
        command.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0) throw new InvalidOperationException("Sự cố không tồn tại.");
    }

    public async Task UpdateIncidentStatusAsync(long incidentId, string decision, int adminAccountId, string? adminNotes, int pointsDeducted)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        await using var command = new SqlCommand("""
            UPDATE dbo.ASSET_INCIDENT
            SET review_status = @decision,
                reviewed_by_admin_id = @adminAccountId,
                reviewed_at = GETDATE(),
                incident_notes = @notes,
                verified_missing = CASE 
                    WHEN @decision = 'CONFIRMED' AND incident_type = 'MISSING' THEN 1 
                    ELSE 0 
                END
            WHERE incident_id = @incidentId
            """, connection);
        
        command.Parameters.AddWithValue("@incidentId", incidentId);
        command.Parameters.AddWithValue("@decision", decision);
        command.Parameters.AddWithValue("@adminAccountId", adminAccountId);
        command.Parameters.AddWithValue("@notes", (object?)adminNotes ?? DBNull.Value);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0) throw new InvalidOperationException("Sự cố không tồn tại.");
    }

    public async Task<List<dynamic>> GetFloorStatisticsAsync(DateTime? startDate, DateTime? endDate)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        
        var sql = """
            SELECT 
                r.floor,
                COUNT(DISTINCT b.booking_id) AS booking_count,
                ISNULL(SUM(DATEDIFF(DAY, b.start_at, b.end_at)), 0) AS total_days
            FROM dbo.ROOM r
            LEFT JOIN dbo.BOOKING b ON b.room_id = r.room_id
            """;
        
        if (startDate.HasValue)
            sql += " AND b.start_at >= @startDate ";
        if (endDate.HasValue)
            sql += " AND b.end_at <= @endDate ";
        
        sql += " GROUP BY r.floor HAVING COUNT(DISTINCT b.booking_id) > 0 ORDER BY r.floor";
        
        await using var command = new SqlCommand(sql, connection);
        if (startDate.HasValue)
            command.Parameters.AddWithValue("@startDate", startDate.Value);
        if (endDate.HasValue)
            command.Parameters.AddWithValue("@endDate", endDate.Value);
        
        var stats = new List<dynamic>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stats.Add(new
            {
                Floor = reader.GetInt16(0),
                BookingCount = reader.GetInt32(1),
                TotalDays = reader.GetInt32(2)
            });
        }
        return stats;
    }
}
