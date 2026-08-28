SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS
(
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806010613_TDC0706SharedAuditGovernance'
)
    THROW 52100, 'TDC-0706 migration history is missing.', 1;

IF EXISTS
(
    SELECT 1 FROM [dbo].[DataRetentionPolicies]
    WHERE [AuditLogRetentionDays] < 2555 OR [WorkflowAuditRetentionDays] < 2555
)
    THROW 52101, 'Existing retention policy values were not normalized to seven years.', 1;

IF EXISTS
(
    SELECT expected.[Id], expected.[Operation]
    FROM (VALUES
        ('50000000-0000-0000-0000-000000000001', 1),
        ('50000000-0000-0000-0000-000000000002', 2),
        ('50000000-0000-0000-0000-000000000003', 3),
        ('50000000-0000-0000-0000-000000000004', 4),
        ('50000000-0000-0000-0000-000000000005', 5),
        ('50000000-0000-0000-0000-000000000006', 6),
        ('50000000-0000-0000-0000-000000000007', 7),
        ('50000000-0000-0000-0000-000000000008', 8),
        ('50000000-0000-0000-0000-000000000009', 9)
    ) expected([Id], [Operation])
    LEFT JOIN [dbo].[ProcurementControlEvents] actual
      ON actual.[Id] = CONVERT(uniqueidentifier, expected.[Id])
     AND actual.[Operation] = expected.[Operation]
    WHERE actual.[Id] IS NULL
)
    THROW 52102, 'Semantic operation backfill did not cover all required actions.', 1;

DECLARE @tenantId uniqueidentifier = '11111111-1111-1111-1111-111111111111';
DECLARE @userId uniqueidentifier = '22222222-2222-2222-2222-222222222222';
DECLARE @recordId uniqueidentifier = '33333333-3333-3333-3333-333333333333';
DECLARE @sourceAt datetime2 = '2026-08-06T00:00:00Z';
DECLARE @retainUntil datetime2 = DATEADD(day, 2555, @sourceAt);

INSERT INTO [dbo].[AuditRecordLifecycleEvents]
(
    [Id], [StoreKey], [RecordId], [SequenceNumber], [Action], [Reason], [RequestKey],
    [CorrelationId], [ArchiveReference], [SourceOccurredAtUtc], [RetainUntilUtc],
    [ActorUserId], [ActorName], [ActorRolesJson], [RecordSnapshotJson],
    [PreviousIntegrityHash], [IntegrityHash], [CreatedAt], [IsDeleted], [TenantId]
)
VALUES
    ('60000000-0000-0000-0000-000000000001', 'platform-audit-log', @recordId, 1, 0,
     N'Litigation hold requested', 'hold-1', 'correlation-hold-1', NULL,
     @sourceAt, @retainUntil, @userId, N'Internal Auditor', N'["TDC_INTERNAL_AUDIT"]', N'{}',
     NULL, REPLICATE('A', 64), '2026-08-06T01:00:00Z', 0, @tenantId),
    ('60000000-0000-0000-0000-000000000002', 'platform-audit-log', @recordId, 2, 2,
     N'Move to statutory archive', 'archive-1', 'correlation-archive-1', N'audit-archive://test',
     @sourceAt, @retainUntil, @userId, N'Internal Auditor', N'["TDC_INTERNAL_AUDIT"]', N'{}',
     REPLICATE('A', 64), REPLICATE('B', 64), '2026-08-06T01:01:00Z', 0, @tenantId),
    ('60000000-0000-0000-0000-000000000003', 'platform-audit-log', @recordId, 3, 3,
     N'Restore for audit inspection', 'restore-1', 'correlation-restore-1', N'audit-archive://test',
     @sourceAt, @retainUntil, @userId, N'Internal Auditor', N'["TDC_INTERNAL_AUDIT"]', N'{}',
     REPLICATE('B', 64), REPLICATE('C', 64), '2026-08-06T01:02:00Z', 0, @tenantId);

IF (SELECT COUNT(*) FROM [dbo].[AuditRecordLifecycleEvents]
    WHERE [TenantId] = @tenantId AND [RecordId] = @recordId) <> 3
    THROW 52103, 'Legal-hold, archive, and restore history was not persisted.', 1;

BEGIN TRY
    UPDATE [dbo].[AuditLogs]
    SET [Action] = N'Tampered'
    WHERE [Id] = @recordId;
    THROW 52104, 'Audit log update unexpectedly succeeded.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51470 THROW;
END CATCH;

BEGIN TRY
    DELETE FROM [dbo].[AuditRecordLifecycleEvents]
    WHERE [Id] = '60000000-0000-0000-0000-000000000001';
    THROW 52105, 'Audit lifecycle delete unexpectedly succeeded.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51471 THROW;
END CATCH;

BEGIN TRY
    UPDATE [dbo].[ProcurementControlEvents]
    SET [Operation] = 0
    WHERE [Id] = '50000000-0000-0000-0000-000000000001';
    THROW 52106, 'Control-event update unexpectedly succeeded.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51000 THROW;
END CATCH;

BEGIN TRY
    UPDATE [dbo].[DataRetentionPolicies]
    SET [AuditLogRetentionDays] = 365;
    THROW 52107, 'Retention minimum unexpectedly accepted less than seven years.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;

PRINT 'TDC0706_SQL_SMOKE_OK';
