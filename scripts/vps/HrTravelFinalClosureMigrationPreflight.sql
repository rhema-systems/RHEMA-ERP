-- Read-only Up preconditions for the HR staff travel final closure:
--   20261002000637_TravelClosureBatch1           (data-precondition)
--   20261002042909_TravelClosureApprovalLadder   (data-precondition)
--   20261003002540_TravelClosureHealthClearance  (down-only)
--   20261003212653_TravelClosureAttendanceLink   (down-only)
-- Only table variables are written. Each check mirrors one THROW in a migration's
-- Up, under the same conditions, and counts what that THROW counts. The last two
-- migrations throw only in Down, when a rollback would drop rows the previous
-- shape cannot hold; their Up has no data precondition, so nothing is probed.
SET NOCOUNT ON;

DECLARE @Checks TABLE (CheckName nvarchar(300) NOT NULL, AffectedRows bigint NOT NULL);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'HrTravelFinalClosure.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @Batch1Applied bit = CASE WHEN EXISTS
    (SELECT 1 FROM dbo.__EFMigrationsHistory
     WHERE MigrationId = N'20261002000637_TravelClosureBatch1') THEN 1 ELSE 0 END;
DECLARE @LadderApplied bit = CASE WHEN EXISTS
    (SELECT 1 FROM dbo.__EFMigrationsHistory
     WHERE MigrationId = N'20261002042909_TravelClosureApprovalLadder') THEN 1 ELSE 0 END;

-- Batch 1 creates three unique indexes filtered to live rows and refuses when live
-- rows already hold the key twice. The counts run in dynamic SQL so a missing
-- column is reported instead of failing this probe's compile.
IF @Batch1Applied = 0
BEGIN
    IF OBJECT_ID(N'dbo.StaffTravelPolicies', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.StaffTravelPolicies')
                         AND name = N'IX_StaffTravelPolicies_TenantId_PolicyName_VersionNumber')
    BEGIN
        IF COL_LENGTH(N'dbo.StaffTravelPolicies', N'TenantId') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelPolicies', N'PolicyName') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelPolicies', N'VersionNumber') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelPolicies', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrTravelFinalClosure.RequiredColumnMissing:StaffTravelPolicies', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrTravelFinalClosure.DuplicatePolicyVersions'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.StaffTravelPolicies
                      WHERE IsDeleted = 0
                      GROUP BY TenantId, PolicyName, VersionNumber
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;

    -- The claim and advance number indexes are dropped only while still unfiltered,
    -- then rebuilt filtered to live rows: the duplicates are counted whenever no
    -- filtered index of that name exists yet.
    IF OBJECT_ID(N'dbo.StaffTravelExpenseClaims', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.StaffTravelExpenseClaims')
                         AND name = N'IX_StaffTravelExpenseClaims_TenantId_ClaimNumber'
                         AND has_filter = 1)
    BEGIN
        IF COL_LENGTH(N'dbo.StaffTravelExpenseClaims', N'TenantId') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelExpenseClaims', N'ClaimNumber') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelExpenseClaims', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrTravelFinalClosure.RequiredColumnMissing:StaffTravelExpenseClaims', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrTravelFinalClosure.DuplicateClaimNumbers'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.StaffTravelExpenseClaims
                      WHERE IsDeleted = 0
                      GROUP BY TenantId, ClaimNumber
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;

    IF OBJECT_ID(N'dbo.StaffTravelAdvances', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.StaffTravelAdvances')
                         AND name = N'IX_StaffTravelAdvances_TenantId_AdvanceNumber'
                         AND has_filter = 1)
    BEGIN
        IF COL_LENGTH(N'dbo.StaffTravelAdvances', N'TenantId') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelAdvances', N'AdvanceNumber') IS NULL
           OR COL_LENGTH(N'dbo.StaffTravelAdvances', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrTravelFinalClosure.RequiredColumnMissing:StaffTravelAdvances', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrTravelFinalClosure.DuplicateAdvanceNumbers'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.StaffTravelAdvances
                      WHERE IsDeleted = 0
                      GROUP BY TenantId, AdvanceNumber
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;
END;

-- The approval ladder turns each tenant's seeded one-step travel route into a
-- two-stage one, and refuses when a tenant holds more than one such route. This is
-- the migration's own selection of those routes, counted per tenant.
IF @LadderApplied = 0
   AND OBJECT_ID(N'dbo.WorkflowDefinitions', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkflowSteps', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkflowTransitions', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkflowEntityTypes', N'U') IS NOT NULL
    INSERT @Checks EXEC sys.sp_executesql N'
        SELECT N''HrTravelFinalClosure.TenantsWithSeveralSeededTravelRoutes'', COUNT_BIG(*)
        FROM (SELECT d.TenantId
              FROM dbo.WorkflowDefinitions d
              JOIN dbo.WorkflowEntityTypes et ON et.Id = d.EntityTypeId
              WHERE d.IsDeleted = 0 AND d.IsActive = 1 AND d.LifecycleStatus = 1
                AND (et.Code = N''STAFF_TRAVEL_REQUEST'' OR et.Name = N''Staff Travel Request'')
                AND (d.Name = N''Staff Travel Approval'' OR d.Name LIKE N''Staff Travel Approval %'')
                AND d.CreatedBy = N''System''
                AND (SELECT COUNT(*) FROM dbo.WorkflowSteps s
                      WHERE s.WorkflowDefinitionId = d.Id AND s.IsDeleted = 0 AND s.StepType = 2) = 1
                AND NOT EXISTS (
                    SELECT 1 FROM dbo.WorkflowDefinitions d2
                    JOIN dbo.WorkflowSteps s2 ON s2.WorkflowDefinitionId = d2.Id
                    WHERE d2.TenantId = d.TenantId AND d2.EntityTypeId = d.EntityTypeId
                      AND d2.IsDeleted = 0 AND d2.IsActive = 1 AND s2.IsDeleted = 0
                      AND s2.Name = N''Line manager approval'')
              GROUP BY d.TenantId
              HAVING COUNT_BIG(*) > 1) ambiguous;';

SELECT CheckName, AffectedRows FROM @Checks WHERE AffectedRows > 0 ORDER BY CheckName;
