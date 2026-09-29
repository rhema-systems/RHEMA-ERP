-- Reviewed upgrade checks for AddMedicalBoardCases. Its explicit THROW protects
-- Down only; multiple legitimate cases must NOT block an upgrade. Up preserves
-- legacy board findings and creates filtered unique indexes on cases/attendance.
-- Only table variables are written. No business or migration data is changed.
SET NOCOUNT ON;
DECLARE @Checks TABLE (CheckName nvarchar(240), AffectedRows bigint);
DECLARE @Applied TABLE (MigrationId nvarchar(150));
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    INSERT @Applied EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory';
IF EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260926143546_AddMedicalBoardCases')
BEGIN
    SELECT CheckName,AffectedRows FROM @Checks;
    RETURN;
END;

IF COL_LENGTH(N'dbo.MedicalBoards',N'EmployeeId') IS NOT NULL
BEGIN
    INSERT @Checks EXEC sys.sp_executesql N'
        SELECT N''HrMedicalBoards:LegacyEmployeeMissing'',COUNT_BIG(*)
        FROM dbo.MedicalBoards b LEFT JOIN dbo.Employees e ON e.Id=b.EmployeeId
        WHERE e.Id IS NULL;';
    IF OBJECT_ID(N'dbo.MedicalBoardCases',N'U') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''HrMedicalBoards:ConflictingLegacyCase'',COUNT_BIG(*)
            FROM dbo.MedicalBoards b
            WHERE EXISTS(SELECT 1 FROM dbo.MedicalBoardCases c WHERE c.BoardId=b.Id)
              AND NOT EXISTS(SELECT 1 FROM dbo.MedicalBoardCases c WHERE c.BoardId=b.Id AND c.EmployeeId=b.EmployeeId);';
END;
IF OBJECT_ID(N'dbo.MedicalBoardCases',N'U') IS NOT NULL
    INSERT @Checks EXEC sys.sp_executesql N'
        SELECT N''HrMedicalBoards:DuplicateLiveBoardEmployee'',COUNT_BIG(*) FROM (
            SELECT BoardId,EmployeeId FROM dbo.MedicalBoardCases WHERE IsDeleted=0
            GROUP BY BoardId,EmployeeId HAVING COUNT_BIG(*)>1) duplicates;';
IF OBJECT_ID(N'dbo.MedicalBoardSittingAttendances',N'U') IS NOT NULL
    INSERT @Checks EXEC sys.sp_executesql N'
        SELECT N''HrMedicalBoards:DuplicateLiveSittingMember'',COUNT_BIG(*) FROM (
            SELECT SittingId,MemberId FROM dbo.MedicalBoardSittingAttendances WHERE IsDeleted=0
            GROUP BY SittingId,MemberId HAVING COUNT_BIG(*)>1) duplicates;';
SELECT CheckName,AffectedRows FROM @Checks WHERE AffectedRows>0 ORDER BY CheckName;
