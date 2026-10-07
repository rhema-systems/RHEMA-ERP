-- Read-only Up preconditions for the HR company-schedule final closure:
--   20261004224953_CompanyScheduleFinalReview   (data-precondition)
-- Only table variables are written. Each check mirrors one THROW in the migration's
-- Up, under the same conditions, and counts what that THROW counts. The migration
-- also throws in Down (51522) when a rollback would drop data the old shape cannot
-- hold; that is not an Up precondition, so nothing is probed for it.
SET NOCOUNT ON;

DECLARE @Checks TABLE (CheckName nvarchar(300) NOT NULL, AffectedRows bigint NOT NULL);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'HrCompanyScheduleFinalClosure.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @ReviewApplied bit = CASE WHEN EXISTS
    (SELECT 1 FROM dbo.__EFMigrationsHistory
     WHERE MigrationId = N'20261004224953_CompanyScheduleFinalReview') THEN 1 ELSE 0 END;

-- The review builds two unique indexes filtered to live rows - one employee once per
-- event as a guest, once in the attendance register - and refuses first (THROW 51520,
-- 51521) when live rows already hold a pair twice. Its Up checks whenever the table
-- exists, with no index guard, so this does too. The counts are duplicate (tenant,
-- event, employee) groups, run in dynamic SQL so a missing column is reported instead
-- of failing this probe's compile.
IF @ReviewApplied = 0
BEGIN
    -- THROW 51520: the unique index's own filter - internal guests (EmployeeId set), live rows.
    IF OBJECT_ID(N'dbo.EventParticipants', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.EventParticipants', N'TenantId') IS NULL
           OR COL_LENGTH(N'dbo.EventParticipants', N'EventId') IS NULL
           OR COL_LENGTH(N'dbo.EventParticipants', N'EmployeeId') IS NULL
           OR COL_LENGTH(N'dbo.EventParticipants', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrCompanyScheduleFinalClosure.RequiredColumnMissing:EventParticipants', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrCompanyScheduleFinalClosure.DuplicateEventGuests'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.EventParticipants
                      WHERE EmployeeId IS NOT NULL AND IsDeleted = 0
                      GROUP BY TenantId, EventId, EmployeeId
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;

    -- THROW 51521: the unique index's own filter - live attendance marks.
    IF OBJECT_ID(N'dbo.EventAttendances', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.EventAttendances', N'TenantId') IS NULL
           OR COL_LENGTH(N'dbo.EventAttendances', N'EventId') IS NULL
           OR COL_LENGTH(N'dbo.EventAttendances', N'EmployeeId') IS NULL
           OR COL_LENGTH(N'dbo.EventAttendances', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrCompanyScheduleFinalClosure.RequiredColumnMissing:EventAttendances', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrCompanyScheduleFinalClosure.DuplicateAttendanceMarks'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.EventAttendances
                      WHERE IsDeleted = 0
                      GROUP BY TenantId, EventId, EmployeeId
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;
END;

SELECT CheckName, AffectedRows FROM @Checks WHERE AffectedRows > 0 ORDER BY CheckName;
