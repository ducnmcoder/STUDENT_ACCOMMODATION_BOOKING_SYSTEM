USE [StudentAccommodationBookingDB];
GO

-- New policy values are applied to existing policies without changing historical events.
IF COL_LENGTH('dbo.POLICY', 'monthly_initial_score') IS NULL
    ALTER TABLE dbo.POLICY ADD monthly_initial_score INT NOT NULL
        CONSTRAINT DF_POLICY_monthly_initial_score DEFAULT (10);
IF COL_LENGTH('dbo.POLICY', 'weekly_booking_limit') IS NULL
    ALTER TABLE dbo.POLICY ADD weekly_booking_limit INT NOT NULL
        CONSTRAINT DF_POLICY_weekly_booking_limit DEFAULT (5);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.POLICY') AND name='CK_POLICY_student_booking_rules')
    ALTER TABLE dbo.POLICY ADD CONSTRAINT CK_POLICY_student_booking_rules
        CHECK (monthly_initial_score >= 0 AND weekly_booking_limit > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.BOOKING') AND name = 'IX_BOOKING_student_created')
    CREATE INDEX IX_BOOKING_student_created ON dbo.BOOKING(student_id, created_at) INCLUDE (booking_status);
GO

-- Calendar months and weeks use Vietnam time (UTC+7); BOOKING timestamps remain UTC.
CREATE OR ALTER PROCEDURE dbo.usp_BookRoom
    @student_id INT, @room_id INT, @start_at DATETIME2(0), @end_at DATETIME2(0),
    @purpose NVARCHAR(300)=NULL, @booking_id BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @now DATETIME2(0) = SYSUTCDATETIME();
    IF @start_at >= @end_at THROW 51003, 'End time must be after start time.', 1;
    IF @start_at <= @now THROW 51019, 'Booking must start in the future.', 1;
    IF CONVERT(date, DATEADD(HOUR, 7, @start_at)) <> CONVERT(date, DATEADD(HOUR, 7, DATEADD(SECOND, -1, @end_at)))
        THROW 51020, 'Booking must fit within one Vietnam calendar day.', 1;

    DECLARE @localToday DATE = CONVERT(date, DATEADD(HOUR, 7, @now));
    DECLARE @monthStartLocal DATE = DATEFROMPARTS(YEAR(@localToday), MONTH(@localToday), 1);
    DECLARE @monthStartUtc DATETIME2(0) = DATEADD(HOUR, -7, CONVERT(DATETIME2(0), @monthStartLocal));
    DECLARE @monthEndUtc DATETIME2(0) = DATEADD(HOUR, -7,
        CONVERT(DATETIME2(0), DATEADD(MONTH, 1, @monthStartLocal)));
    DECLARE @weekStartLocal DATE = DATEADD(DAY,
        -(DATEDIFF(DAY, CONVERT(date, '19000101', 112), @localToday) % 7), @localToday);
    DECLARE @weekStartUtc DATETIME2(0) = DATEADD(HOUR, -7, CONVERT(DATETIME2(0), @weekStartLocal));
    DECLARE @weekEndUtc DATETIME2(0) = DATEADD(DAY, 7, @weekStartUtc);
    DECLARE @initial INT, @threshold INT, @weeklyLimit INT, @monthlyScore INT;
    DECLARE @used BIGINT;

    BEGIN TRANSACTION;
        SELECT TOP (1) @initial=p.monthly_initial_score,
            @threshold=p.booking_block_below, @weeklyLimit=p.weekly_booking_limit
        FROM dbo.POLICY p
        WHERE p.effective_from <= @now AND (p.effective_to IS NULL OR p.effective_to > @now)
        ORDER BY p.effective_from DESC, p.policy_id DESC;
        IF @initial IS NULL THROW 51004, 'No active booking policy.', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.STUDENT s WITH (UPDLOCK, HOLDLOCK)
            JOIN dbo.ACCOUNT a ON a.account_id=s.account_id
            WHERE s.account_id=@student_id AND s.enrollment_status='ENROLLED' AND a.account_status='ACTIVE')
            THROW 51005, 'Student is not active.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.ROOM WHERE room_id=@room_id AND room_status='AVAILABLE')
            THROW 51006, 'Room is unavailable.', 1;

        SELECT @monthlyScore = @initial + COALESCE(SUM(COALESCE(e.score_delta, 0)), 0)
        FROM dbo.WEEKLY_REPUTATION w WITH (UPDLOCK, HOLDLOCK)
        LEFT JOIN dbo.REPUTATION_EVENT e ON e.weekly_reputation_id=w.weekly_reputation_id
            AND e.occurred_at >= @monthStartUtc AND e.occurred_at < @monthEndUtc
        WHERE w.student_id=@student_id;
        IF @monthlyScore < @threshold
            THROW 51007, 'Monthly reputation is below booking threshold.', 1;

        SELECT @used=COUNT_BIG(*)
        FROM dbo.BOOKING WITH (UPDLOCK, HOLDLOCK, INDEX(IX_BOOKING_student_created))
        WHERE student_id=@student_id AND created_at >= @weekStartUtc AND created_at < @weekEndUtc
            AND booking_status <> 'REJECTED';
        IF @used >= @weeklyLimit THROW 51021, 'Weekly booking limit reached.', 1;

        IF EXISTS (SELECT 1 FROM dbo.BOOKING WITH (UPDLOCK, HOLDLOCK, INDEX(IX_BOOKING_room_time))
            WHERE room_id=@room_id AND booking_status IN ('PENDING', 'APPROVED')
                AND start_at < @end_at AND @start_at < end_at)
            THROW 51008, 'Time slot is occupied.', 1;

        INSERT dbo.BOOKING(student_id, room_id, start_at, end_at, booking_status, purpose, created_at)
            VALUES(@student_id, @room_id, @start_at, @end_at, 'APPROVED', @purpose, @now);
        SET @booking_id=CONVERT(BIGINT, SCOPE_IDENTITY());
    COMMIT TRANSACTION;
END;
GO
