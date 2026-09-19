using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260905193000_AllowUnpublishedTenderScheduleReschedule")]
public sealed class AllowUnpublishedTenderScheduleReschedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("PreviousOpeningScheduledAtUtc", "ProcurementTenderDocumentChanges", "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>("NewOpeningScheduledAtUtc", "ProcurementTenderDocumentChanges", "datetime2", nullable: true);
        migrationBuilder.DropCheckConstraint("CK_ProcurementTenderDocumentChanges_Kind", "ProcurementTenderDocumentChanges");
        migrationBuilder.DropCheckConstraint("CK_ProcurementTenderDocumentChanges_State", "ProcurementTenderDocumentChanges");
        migrationBuilder.AddCheckConstraint("CK_ProcurementTenderDocumentChanges_Kind", "ProcurementTenderDocumentChanges", KindConstraint);
        migrationBuilder.AddCheckConstraint("CK_ProcurementTenderDocumentChanges_State", "ProcurementTenderDocumentChanges", StateConstraint);
        migrationBuilder.Sql(UpgradeTriggers);
        migrationBuilder.Sql(PublicationGuard);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverting the binary while retaining an approved schedule would change effective dates.
        // Fail visibly rather than silently deleting or reinterpreting immutable audit history.
        migrationBuilder.Sql("THROW 51212, 'Unpublished tender schedule history is forward-only; rollback requires an explicitly reviewed recovery migration.', 1;");
    }

    internal const string KindConstraint =
        "([ChangeType] = 0 AND [PreviousTemplateVersionId] IS NOT NULL AND [NewTemplateVersionId] IS NOT NULL " +
        "AND [PreviousTemplateVersionId] <> [NewTemplateVersionId] AND [PreviousValueUtc] IS NULL AND [NewValueUtc] IS NULL " +
        "AND [PreviousOpeningScheduledAtUtc] IS NULL AND [NewOpeningScheduledAtUtc] IS NULL) OR " +
        "([ChangeType] IN (1, 2) AND [PreviousTemplateVersionId] IS NULL AND [NewTemplateVersionId] IS NULL " +
        "AND [PreviousValueUtc] IS NOT NULL AND [NewValueUtc] IS NOT NULL AND [NewValueUtc] > [PreviousValueUtc] " +
        "AND [PreviousOpeningScheduledAtUtc] IS NULL AND [NewOpeningScheduledAtUtc] IS NULL) OR " +
        "([ChangeType] = 3 AND [PreviousTemplateVersionId] IS NULL AND [NewTemplateVersionId] IS NULL " +
        "AND [PreviousValueUtc] IS NOT NULL AND [NewValueUtc] IS NOT NULL AND [NewValueUtc] > [PreviousValueUtc] " +
        "AND [NewOpeningScheduledAtUtc] IS NOT NULL AND [NewOpeningScheduledAtUtc] > [NewValueUtc] " +
        "AND ([PreviousOpeningScheduledAtUtc] IS NULL OR [NewOpeningScheduledAtUtc] > [PreviousOpeningScheduledAtUtc]) " +
        "AND [RequiresAcknowledgement] = 0)";

    internal const string StateConstraint = "[Sequence] >= 1 AND [ChangeType] BETWEEN 0 AND 3 AND [Status] BETWEEN 0 AND 2 " +
        "AND (([Status] = 0 AND [DecidedAtUtc] IS NULL AND [DecidedByUserId] IS NULL " +
        "AND [WorkflowOutcome] IS NULL AND [ApprovalReference] IS NULL " +
        "AND [DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL) OR " +
        "([Status] = 1 AND [DecidedAtUtc] IS NOT NULL AND [DecidedByUserId] IS NOT NULL " +
        "AND LEN(LTRIM(RTRIM(ISNULL([WorkflowOutcome], '')))) > 0 " +
        "AND LEN(LTRIM(RTRIM(ISNULL([ApprovalReference], '')))) > 0 " +
        "AND (([DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL) OR " +
        "([DispatchedAtUtc] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL " +
        "AND LEN(LTRIM(RTRIM(ISNULL([DispatchEvidenceReference], '')))) > 0))) OR " +
        "([Status] = 2 AND [DecidedAtUtc] IS NOT NULL AND [DecidedByUserId] IS NOT NULL " +
        "AND LEN(LTRIM(RTRIM(ISNULL([WorkflowOutcome], '')))) > 0 " +
        "AND LEN(LTRIM(RTRIM(ISNULL([ApprovalReference], '')))) > 0 " +
        "AND [DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL)) " +
        "AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1 " +
        "AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)";

    private const string PublicationGuard = """
        CREATE OR ALTER TRIGGER [dbo].[TR_Tenders_ControlledDocumentPublicationGuard]
        ON [dbo].[Tenders]
        AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (
                SELECT 1 FROM inserted i
                JOIN deleted d ON d.Id = i.Id
                JOIN ProcurementTenderDocumentRegisters r ON r.TenderId = i.Id
                    AND r.TenantId = i.TenantId AND r.SourceType = 0 AND r.IsDeleted = 0
                OUTER APPLY (
                    SELECT TOP (1) c.NewValueUtc FROM ProcurementTenderDocumentChanges c
                    WHERE c.RegisterId = r.Id AND c.TenantId = r.TenantId
                      AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType IN (1, 3)
                    ORDER BY c.Sequence DESC) dl
                OUTER APPLY (
                    SELECT TOP (1) c.NewOpeningScheduledAtUtc FROM ProcurementTenderDocumentChanges c
                    WHERE c.RegisterId = r.Id AND c.TenantId = r.TenantId
                      AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 3
                    ORDER BY c.Sequence DESC) op
                WHERE i.Status = 'Published'
                  AND (d.Status <> 'Published'
                       OR ISNULL(i.PublishDate, '19000101') <> ISNULL(d.PublishDate, '19000101')
                       OR ISNULL(i.PublishedById, '00000000-0000-0000-0000-000000000000') <>
                          ISNULL(d.PublishedById, '00000000-0000-0000-0000-000000000000'))
                  AND (i.SubmissionDeadline IS NULL
                       OR i.SubmissionDeadline <> COALESCE(dl.NewValueUtc, r.OriginalSubmissionDeadlineUtc)
                       OR ISNULL(i.OpeningDate, '9999-12-31') <>
                          ISNULL(COALESCE(op.NewOpeningScheduledAtUtc, r.OpeningScheduledAtUtc), '9999-12-31')
                       OR EXISTS (SELECT 1 FROM ProcurementTenderDocumentChanges pending
                           WHERE pending.RegisterId = r.Id AND pending.TenantId = r.TenantId
                             AND pending.IsDeleted = 0 AND pending.Status = 0)))
                THROW 51212, 'Tender publication requires the current approved document schedule and no pending controlled change.', 1;
        END
        """;

    private const string UpgradeTriggers = """
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderDocumentChanges_Lifecycle]'));
        IF @definition IS NULL
            THROW 51212, 'Tender-document change lifecycle trigger is missing.', 1;
        IF CHARINDEX(N'TDC-UNPUBLISHED-SCHEDULE-GUARD', @definition) > 0
            THROW 51212, 'Tender schedule trigger was already modified; review migration lineage.', 1;
        DECLARE @lockD nvarchar(max) = N'd.PreviousValueUtc, d.NewValueUtc, d.RequiresAcknowledgement';
        DECLARE @lockI nvarchar(max) = N'i.PreviousValueUtc, i.NewValueUtc, i.RequiresAcknowledgement';
        DECLARE @opening nvarchar(max) = N'r.OpeningScheduledAtUtc IS NOT NULL AND i.NewValueUtc > r.OpeningScheduledAtUtc';
        DECLARE @anchor nvarchar(max) = N'THROW 51211, ''Tender-document change outcome contradicts the exact shared-workflow outcome.'', 1;';
        IF CHARINDEX(@lockD, @definition) = 0 OR CHARINDEX(@lockI, @definition) = 0
           OR CHARINDEX(@opening, @definition) = 0 OR CHARINDEX(@anchor, @definition) = 0
           OR CHARINDEX(N'c.ChangeType = 1', @definition) = 0
            THROW 51212, 'Tender-document change trigger is not a recognized governed source variant.', 1;
        SET @definition = REPLACE(@definition, @lockD, N'd.PreviousValueUtc, d.NewValueUtc, d.PreviousOpeningScheduledAtUtc, d.NewOpeningScheduledAtUtc, d.RequiresAcknowledgement');
        SET @definition = REPLACE(@definition, @lockI, N'i.PreviousValueUtc, i.NewValueUtc, i.PreviousOpeningScheduledAtUtc, i.NewOpeningScheduledAtUtc, i.RequiresAcknowledgement');
        SET @definition = REPLACE(@definition, N'c.ChangeType = 1', N'c.ChangeType IN (1, 3)');
        DECLARE @effectiveOpening nvarchar(max) = N'COALESCE((SELECT TOP (1) os.NewOpeningScheduledAtUtc FROM ProcurementTenderDocumentChanges os WHERE os.RegisterId = i.RegisterId AND os.TenantId = i.TenantId AND os.Id <> i.Id AND os.IsDeleted = 0 AND os.Status = 1 AND os.ChangeType = 3 ORDER BY os.Sequence DESC), r.OpeningScheduledAtUtc)';
        SET @definition = REPLACE(@definition, @opening, @effectiveOpening + N' IS NOT NULL AND i.NewValueUtc > ' + @effectiveOpening);
        DECLARE @guard nvarchar(max) = N'
        /* TDC-UNPUBLISHED-SCHEDULE-GUARD */
        IF EXISTS (
            SELECT 1 FROM inserted i
            JOIN ProcurementTenderDocumentRegisters r ON r.Id = i.RegisterId AND r.TenantId = i.TenantId
            LEFT JOIN Tenders t ON t.Id = r.TenderId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
            OUTER APPLY (SELECT TOP (1) c.NewValueUtc FROM ProcurementTenderDocumentChanges c
                WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId AND c.Id <> i.Id
                  AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType IN (1, 3) ORDER BY c.Sequence DESC) dl
            OUTER APPLY (SELECT TOP (1) c.NewOpeningScheduledAtUtc FROM ProcurementTenderDocumentChanges c
                WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId AND c.Id <> i.Id
                  AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 3 ORDER BY c.Sequence DESC) op
            OUTER APPLY (SELECT TOP (1) c.NewValueUtc FROM ProcurementTenderDocumentChanges c
                WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId AND c.Id <> i.Id
                  AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 2 ORDER BY c.Sequence DESC) bv
            WHERE i.ChangeType = 3 AND i.Status <> 2 AND
                (r.SourceType <> 0 OR r.Method <> 1 OR t.Id IS NULL OR t.Status <> ''Approved''
                 OR t.PublishDate IS NOT NULL OR t.PublishedById IS NOT NULL
                 OR t.RequiresPrequalification = 1 OR t.UseQCBSEvaluation = 1
                 OR EXISTS (SELECT 1 FROM TenderBids b WHERE b.TenderId = t.Id AND b.TenantId = i.TenantId AND b.IsDeleted = 0)
                 OR EXISTS (SELECT 1 FROM ProcurementTenderControls tc WHERE tc.TenderId = t.Id AND tc.TenantId = i.TenantId AND tc.IsDeleted = 0)
                 OR EXISTS (SELECT 1 FROM ProcurementTenderDocumentIssuances di WHERE di.RegisterId = r.Id AND di.TenantId = i.TenantId AND di.IsDeleted = 0)
                 OR i.PreviousValueUtc <> COALESCE(dl.NewValueUtc, r.OriginalSubmissionDeadlineUtc)
                 OR ISNULL(i.PreviousOpeningScheduledAtUtc, ''9999-12-31'') <> ISNULL(COALESCE(op.NewOpeningScheduledAtUtc, r.OpeningScheduledAtUtc), ''9999-12-31'')
                 OR i.NewValueUtc <= COALESCE(i.DecidedAtUtc, i.RequestedAtUtc)
                 OR i.NewValueUtc >= COALESCE(bv.NewValueUtc, r.OriginalBidValidityUntilUtc)))
            THROW 51212, ''Unpublished tender reschedule requires untouched external access and a valid forward schedule.'', 1;
        ';
        SET @definition = REPLACE(@definition, @anchor, @anchor + @guard);
        SET @definition = LTRIM(@definition);
        WHILE UNICODE(LEFT(@definition, 1)) IN (9, 10, 13, 32) SET @definition = SUBSTRING(@definition, 2, LEN(@definition));
        -- SQL Server retains CREATE OR ALTER as CREATE followed by extra spaces.
        DECLARE @triggerPosition int = CHARINDEX(N'trigger', LOWER(@definition));
        DECLARE @headerToken nvarchar(100) = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(
            LEFT(@definition, CASE WHEN @triggerPosition > 0 THEN @triggerPosition - 1 ELSE 0 END),
            CHAR(9), N''), CHAR(10), N''), CHAR(13), N''), N' ', N''));
        IF @triggerPosition = 0 OR @headerToken NOT IN (N'create', N'createoralter', N'alter')
            THROW 51212, 'Unsupported change trigger statement header.', 1;
        SET @definition = N'ALTER ' + SUBSTRING(@definition, @triggerPosition, LEN(@definition));
        EXEC sys.sp_executesql @definition;

        SET @definition = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderDocumentIssuances_Immutable]'));
        DECLARE @publication nvarchar(max) = N't.Status NOT IN (''Closed'', ''Awarded'', ''Cancelled'')';
        IF @definition IS NULL OR CHARINDEX(N'c.ChangeType = 1', @definition) = 0 OR CHARINDEX(@publication, @definition) = 0
            THROW 51209, 'Tender-document issuance trigger is not a recognized governed source variant.', 1;
        SET @definition = REPLACE(@definition, N'c.ChangeType = 1', N'c.ChangeType IN (1, 3)');
        SET @definition = REPLACE(@definition, @publication, N'((r.Method = 0 AND t.Status NOT IN (''Closed'', ''Awarded'', ''Cancelled'')) OR (r.Method <> 0 AND t.Status = ''Published'' AND t.PublishDate IS NOT NULL AND t.PublishDate <= i.IssuedAtUtc))');
        SET @definition = LTRIM(@definition);
        WHILE UNICODE(LEFT(@definition, 1)) IN (9, 10, 13, 32) SET @definition = SUBSTRING(@definition, 2, LEN(@definition));
        SET @triggerPosition = CHARINDEX(N'trigger', LOWER(@definition));
        SET @headerToken = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(
            LEFT(@definition, CASE WHEN @triggerPosition > 0 THEN @triggerPosition - 1 ELSE 0 END),
            CHAR(9), N''), CHAR(10), N''), CHAR(13), N''), N' ', N''));
        IF @triggerPosition = 0 OR @headerToken NOT IN (N'create', N'createoralter', N'alter')
            THROW 51209, 'Unsupported issuance trigger statement header.', 1;
        SET @definition = N'ALTER ' + SUBSTRING(@definition, @triggerPosition, LEN(@definition));
        EXEC sys.sp_executesql @definition;
        """;
}
