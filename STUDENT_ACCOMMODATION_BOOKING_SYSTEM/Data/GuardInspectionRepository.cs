
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public class GuardInspectionRepository
{
    private readonly string _connectionString;

    public GuardInspectionRepository(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("AccommodationDb")
            ?? throw new InvalidOperationException("Missing connection string.");
    }

    // Lấy các booking đã check-in để bảo vệ kiểm tra
    public async Task<List<GuardInspectionBooking>> GetBookingsAsync()
    {
        var result = new List<GuardInspectionBooking>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                b.booking_id,
                a.full_name,
                r.room_code,
                b.booking_status,
                CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.INSPECTION i
                    WHERE i.booking_id = b.booking_id AND i.phase = 'IN'
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.INSPECTION i
                    WHERE i.booking_id = b.booking_id AND i.phase = 'OUT'
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
            FROM dbo.BOOKING b
            JOIN dbo.ACCOUNT a ON a.account_id = b.student_id
            JOIN dbo.ROOM r ON r.room_id = b.room_id
            WHERE b.booking_status IN ('APPROVED', 'COMPLETED')
              AND EXISTS (
                  SELECT 1 FROM dbo.ATTENDANCE att
                  WHERE att.booking_id = b.booking_id
                    AND att.outcome IN ('ON_TIME', 'LATE')
              )
            ORDER BY b.booking_id DESC
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new GuardInspectionBooking
            {
                BookingId = reader.GetInt64(0),
                StudentName = reader.GetString(1),
                RoomCode = reader.GetString(2),
                BookingStatus = reader.GetString(3),
                HasIn = reader.GetBoolean(4),
                HasOut = reader.GetBoolean(5)
            });
        }

        return result;
    }

    // Lấy thông tin phòng và các thiết bị cần kiểm tra
    public async Task<GuardInspectionForm?> GetFormAsync(
        long bookingId, string phase)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT b.room_id, a.full_name, r.room_code
            FROM dbo.BOOKING b
            JOIN dbo.ACCOUNT a ON a.account_id = b.student_id
            JOIN dbo.ROOM r ON r.room_id = b.room_id
            WHERE b.booking_id = @bookingId
              AND b.booking_status IN ('APPROVED', 'COMPLETED')
              AND (@phase = 'OUT' OR b.booking_status = 'APPROVED')
              AND EXISTS (
                  SELECT 1 FROM dbo.ATTENDANCE att
                  WHERE att.booking_id = b.booking_id
                    AND att.outcome IN ('ON_TIME', 'LATE')
              )
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.INSPECTION i
                  WHERE i.booking_id = b.booking_id
                    AND i.phase = @phase
              )
            """;

        var model = new GuardInspectionForm
        {
            BookingId = bookingId,
            Phase = phase
        };

        int roomId;

        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            command.Parameters.AddWithValue("@phase", phase);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            roomId = reader.GetInt32(0);
            model.StudentName = reader.GetString(1);
            model.RoomCode = reader.GetString(2);
        }

        const string assetSql = """
            SELECT asset_id, asset_tag, asset_name, registered_condition
            FROM dbo.ROOM_ASSET
            WHERE room_id = @roomId
            ORDER BY asset_id
            """;

        await using var assetCommand = new SqlCommand(assetSql, connection);
        assetCommand.Parameters.AddWithValue("@roomId", roomId);

        await using var assetReader = await assetCommand.ExecuteReaderAsync();

        while (await assetReader.ReadAsync())
        {
            model.Items.Add(new GuardInspectionAsset
            {
                AssetId = assetReader.GetInt64(0),
                AssetTag = assetReader.GetString(1),
                AssetName = assetReader.GetString(2),
                RegisteredCondition = assetReader.GetString(3)
            });
        }

        return model;
    }

    // Lưu biên bản, từng thiết bị và sự cố trong cùng transaction
    public async Task<bool> SaveAsync(GuardInspectionForm model, int guardId)
    {
        if (model.Phase is not ("IN" or "OUT"))
            throw new ArgumentException("Loại kiểm tra không hợp lệ.");

        if (model.InspectionNotes?.Length > 300)
            throw new ArgumentException("Ghi chú tối đa 300 ký tự.");

        if (model.Items == null ||
            model.Items.Any(x =>
                x.ObservedState is not ("OK" or "DAMAGED" or "MISSING") ||
                x.ObservationNotes?.Length > 300))
        {
            throw new ArgumentException("Thông tin thiết bị không hợp lệ.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction =
            (SqlTransaction)await connection.BeginTransactionAsync();

        // Kiểm tra booking và phòng trước khi lưu
        const string bookingSql = """
            SELECT b.room_id
            FROM dbo.BOOKING b
            WHERE b.booking_id = @bookingId
              AND b.booking_status IN ('APPROVED', 'COMPLETED')
              AND (@phase = 'OUT' OR b.booking_status = 'APPROVED')
              AND EXISTS (
                  SELECT 1 FROM dbo.ATTENDANCE a
                  WHERE a.booking_id = b.booking_id
                    AND a.outcome IN ('ON_TIME', 'LATE')
              )
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.INSPECTION i
                  WHERE i.booking_id = b.booking_id
                    AND i.phase = @phase
              )
            """;

        int roomId;

        await using (var command =
            new SqlCommand(bookingSql, connection, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", model.BookingId);
            command.Parameters.AddWithValue("@phase", model.Phase);

            var value = await command.ExecuteScalarAsync();

            if (value == null)
                return false;

            roomId = Convert.ToInt32(value);
        }

        // Xác minh thiết bị gửi lên thuộc đúng phòng
        var validIds = new HashSet<long>();

        await using (var command = new SqlCommand(
            "SELECT asset_id FROM dbo.ROOM_ASSET WHERE room_id = @roomId",
            connection, transaction))
        {
            command.Parameters.AddWithValue("@roomId", roomId);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                validIds.Add(reader.GetInt64(0));
        }

        if (model.Items.Count != validIds.Count ||
            model.Items.Select(x => x.AssetId).Distinct().Count()
                != model.Items.Count ||
            model.Items.Any(x => !validIds.Contains(x.AssetId)))
        {
            throw new ArgumentException(
                "Danh sách thiết bị không khớp với phòng.");
        }

        string overallCondition =
            model.Items.Any(x => x.ObservedState != "OK")
                ? "ISSUES"
                : "OK";

        // Tạo biên bản kiểm tra
        const string inspectionSql = """
            INSERT INTO dbo.INSPECTION
            (booking_id, guard_id, phase, inspected_at,
             overall_condition, inspection_notes)
            OUTPUT INSERTED.inspection_id
            VALUES
            (@bookingId, @guardId, @phase, SYSUTCDATETIME(),
             @condition, @notes)
            """;

        long inspectionId;

        await using (var command =
            new SqlCommand(inspectionSql, connection, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", model.BookingId);
            command.Parameters.AddWithValue("@guardId", guardId);
            command.Parameters.AddWithValue("@phase", model.Phase);
            command.Parameters.AddWithValue("@condition", overallCondition);
            command.Parameters.AddWithValue(
                "@notes", (object?)model.InspectionNotes ?? DBNull.Value);

            inspectionId =
                Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        // Lưu kết quả của từng thiết bị
        foreach (var item in model.Items)
        {
            const string itemSql = """
                INSERT INTO dbo.INSPECTION_ITEM
                (inspection_id, asset_id, observed_state,
                 observation_notes, confirmed_at)
                OUTPUT INSERTED.inspection_item_id
                VALUES
                (@inspectionId, @assetId, @state, @notes, SYSUTCDATETIME())
                """;

            long inspectionItemId;

            await using (var command =
                new SqlCommand(itemSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@inspectionId", inspectionId);
                command.Parameters.AddWithValue("@assetId", item.AssetId);
                command.Parameters.AddWithValue("@state", item.ObservedState);
                command.Parameters.AddWithValue(
                    "@notes", (object?)item.ObservationNotes ?? DBNull.Value);

                inspectionItemId =
                    Convert.ToInt64(await command.ExecuteScalarAsync());
            }

            // Thiết bị hỏng hoặc mất: tạo báo cáo chờ xử lý
            if (item.ObservedState is "DAMAGED" or "MISSING")
            {
                const string incidentSql = """
                    INSERT INTO dbo.ASSET_INCIDENT
                    (inspection_item_id, incident_type, reported_at,
                     review_status, verified_missing, incident_notes)
                    VALUES
                    (@itemId, @type, SYSUTCDATETIME(),
                     'PENDING', 0, @notes)
                    """;

                await using var command =
                    new SqlCommand(incidentSql, connection, transaction);

                command.Parameters.AddWithValue("@itemId", inspectionItemId);
                command.Parameters.AddWithValue("@type", item.ObservedState);
                command.Parameters.AddWithValue(
                    "@notes", (object?)item.ObservationNotes ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }
        }

        await transaction.CommitAsync();
        return true;
    }
}
