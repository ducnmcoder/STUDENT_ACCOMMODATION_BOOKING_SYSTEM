using Microsoft.Data.SqlClient;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class NoShowService(IConfiguration configuration, ILogger<NoShowService> logger)
{
    private readonly string _connectionString = configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:AccommodationDb.");

    public async Task ProcessExpiredBookingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Lock each eligible booking while inserting its unique attendance row.
            await using var command = new SqlCommand("""
                DECLARE @now datetime2(0)=SYSUTCDATETIME(), @guard int;
                SELECT TOP (1) @guard=account_id FROM dbo.SECURITY_GUARD ORDER BY account_id;
                IF @guard IS NULL THROW 51100, 'No security guard account is available for automatic attendance.', 1;
                DECLARE @created TABLE(attendance_id bigint);
                INSERT dbo.ATTENDANCE(booking_id,guard_id,outcome,actual_checkin_at,recorded_at,late_minutes)
                    OUTPUT inserted.attendance_id INTO @created
                SELECT b.booking_id,@guard,'NO_SHOW',NULL,@now,NULL
                FROM dbo.BOOKING b WITH (UPDLOCK,HOLDLOCK)
                JOIN dbo.POLICY p ON p.policy_id=(SELECT TOP (1) p2.policy_id FROM dbo.POLICY p2
                    WHERE p2.effective_from<=@now AND (p2.effective_to IS NULL OR p2.effective_to>@now)
                    ORDER BY p2.effective_from DESC,p2.policy_id DESC)
                WHERE b.booking_status='APPROVED' AND p.no_show_cutoff_min IS NOT NULL
                  AND @now>=DATEADD(MINUTE,p.no_show_cutoff_min,b.start_at)
                  AND NOT EXISTS(SELECT 1 FROM dbo.ATTENDANCE a WITH (UPDLOCK,HOLDLOCK) WHERE a.booking_id=b.booking_id)
                  AND NOT EXISTS(SELECT 1 FROM dbo.CHECKOUT c WHERE c.booking_id=b.booking_id);
                SELECT attendance_id FROM @created;
                """, connection, transaction);
            var ids = new List<long>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetInt64(0));
            foreach (var id in ids)
            {
                await using var penalty = new SqlCommand("dbo.usp_RecordPenalty", connection, transaction) { CommandType = System.Data.CommandType.StoredProcedure };
                penalty.Parameters.AddWithValue("@source_type", "ATTENDANCE");
                penalty.Parameters.AddWithValue("@source_id", id);
                penalty.Parameters.AddWithValue("@actor_account_id", DBNull.Value);
                await penalty.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            if (ids.Count > 0) logger.LogInformation("Đã ghi nhận {Count} lượt vắng mặt quá hạn", ids.Count);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

public sealed class NoShowBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NoShowBackgroundService> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = Math.Clamp(configuration.GetValue<int?>("Reputation:NoShowScanSeconds") ?? 60, 15, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NoShowService>().ProcessExpiredBookingsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Tác vụ ghi nhận lượt vắng mặt thất bại"); }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
