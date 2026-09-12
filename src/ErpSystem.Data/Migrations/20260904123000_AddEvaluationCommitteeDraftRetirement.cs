using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds a governed, audit-preserving retirement transition for committee
/// controls that never progressed beyond a pristine Draft snapshot.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260904123000_AddEvaluationCommitteeDraftRetirement")]
public sealed class AddEvaluationCommitteeDraftRetirement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "RetiredAtUtc",
            table: "ProcurementEvaluationCommitteeControls",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "RetiredByUserId",
            table: "ProcurementEvaluationCommitteeControls",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RetirementReason",
            table: "ProcurementEvaluationCommitteeControls",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RetirementEvidenceReference",
            table: "ProcurementEvaluationCommitteeControls",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RetirementIdempotencyKey",
            table: "ProcurementEvaluationCommitteeControls",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[ProcurementEvaluationCommitteeControls]
                DROP CONSTRAINT [CK_ProcurementEvaluationCommitteeControls_State];

            ALTER TABLE [dbo].[ProcurementEvaluationCommitteeControls]
                ADD CONSTRAINT [CK_ProcurementEvaluationCommitteeControls_State] CHECK (
                    [SourceType] BETWEEN 0 AND 1 AND [Version] >= 1 AND [Status] BETWEEN 0 AND 3
                    AND [RequiredQuorum] BETWEEN 1 AND 50 AND [PolicyVersion] >= 1
                    AND ([ConfigurationProfileVersion] IS NULL OR [ConfigurationProfileVersion] >= 1)
                    AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc])
                    AND LEN([CompositionIntegrityHash]) = 64 AND ISJSON([CompositionSnapshotJson]) = 1
                    AND (
                        ([Status] = 0
                         AND [ActivatedAtUtc] IS NULL AND [ActivatedByUserId] IS NULL
                         AND [ActivationEvidenceReference] IS NULL
                         AND [RetiredAtUtc] IS NULL AND [RetiredByUserId] IS NULL
                         AND [RetirementReason] IS NULL AND [RetirementEvidenceReference] IS NULL
                         AND [RetirementIdempotencyKey] IS NULL)
                        OR
                        ([Status] IN (1, 2)
                         AND [ActivatedAtUtc] IS NOT NULL AND [ActivatedByUserId] IS NOT NULL
                         AND LEN(LTRIM(RTRIM(ISNULL([ActivationEvidenceReference], '')))) > 0
                         AND [RetiredAtUtc] IS NULL AND [RetiredByUserId] IS NULL
                         AND [RetirementReason] IS NULL AND [RetirementEvidenceReference] IS NULL
                         AND [RetirementIdempotencyKey] IS NULL)
                        OR
                        ([Status] = 3
                         AND [ActivatedAtUtc] IS NULL AND [ActivatedByUserId] IS NULL
                         AND [ActivationEvidenceReference] IS NULL AND [ActivationIdempotencyKey] IS NULL
                         AND [RetiredAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL
                         AND LEN(LTRIM(RTRIM(ISNULL([RetirementReason], '')))) >= 10
                         AND LEN(LTRIM(RTRIM(ISNULL([RetirementEvidenceReference], '')))) > 0
                         AND LEN(LTRIM(RTRIM(ISNULL([RetirementIdempotencyKey], '')))) > 0)
                    ));
            """);

        AllowDraftToRetiredTransition(migrationBuilder, allow: true);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementEvaluationCommitteeControls_DraftRetirement]
            ON [dbo].[ProcurementEvaluationCommitteeControls]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE
                        (d.Id IS NULL AND i.Status = 3)
                        OR
                        ((ISNULL(i.RetiredAtUtc, '19000101') <> ISNULL(d.RetiredAtUtc, '19000101')
                          OR ISNULL(i.RetiredByUserId, '00000000-0000-0000-0000-000000000000')
                             <> ISNULL(d.RetiredByUserId, '00000000-0000-0000-0000-000000000000')
                          OR ISNULL(i.RetirementReason, '') <> ISNULL(d.RetirementReason, '')
                          OR ISNULL(i.RetirementEvidenceReference, '') <> ISNULL(d.RetirementEvidenceReference, '')
                          OR ISNULL(i.RetirementIdempotencyKey, '') <> ISNULL(d.RetirementIdempotencyKey, ''))
                         AND NOT (d.Status = 0 AND i.Status = 3
                                  AND d.RetiredAtUtc IS NULL AND d.RetiredByUserId IS NULL
                                  AND d.RetirementReason IS NULL
                                  AND d.RetirementEvidenceReference IS NULL
                                  AND d.RetirementIdempotencyKey IS NULL))
                        OR
                        (d.Status = 0 AND i.Status = 3 AND (
                            i.WorkflowInstanceId IS NOT NULL
                            OR EXISTS (
                                SELECT 1
                                FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId
                                  AND (a.IsDeleted = 1 OR a.Status <> 0 OR a.AcceptedAtUtc IS NOT NULL
                                       OR a.AcceptanceSignatureReference IS NOT NULL
                                       OR a.AcceptanceEvidenceReference IS NOT NULL
                                       OR a.AcceptanceIdempotencyKey IS NOT NULL
                                       OR a.StatusReason IS NOT NULL))
                            OR EXISTS (
                                SELECT 1
                                FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                                JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                  ON a.Id = cd.AppointmentId AND a.TenantId = cd.TenantId
                                WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId
                                  )
                            OR EXISTS (
                                SELECT 1 FROM [dbo].[ProcurementEvaluationMeetings] m
                                WHERE m.CommitteeControlId = i.Id AND m.TenantId = i.TenantId
                                  )
                            OR EXISTS (
                                SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] s
                                WHERE s.CommitteeControlId = i.Id AND s.TenantId = i.TenantId
                                  )
                        )))
                    THROW 51304, 'Only a pristine, unactivated Draft evaluation committee control can be retired; retirement metadata is immutable.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationCommitteeControls_DraftRetirement];");
        AllowDraftToRetiredTransition(migrationBuilder, allow: false);
        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[ProcurementEvaluationCommitteeControls]
                DROP CONSTRAINT [CK_ProcurementEvaluationCommitteeControls_State];

            ALTER TABLE [dbo].[ProcurementEvaluationCommitteeControls]
                ADD CONSTRAINT [CK_ProcurementEvaluationCommitteeControls_State] CHECK (
                    [SourceType] BETWEEN 0 AND 1 AND [Version] >= 1 AND [Status] BETWEEN 0 AND 2
                    AND [RequiredQuorum] BETWEEN 1 AND 50 AND [PolicyVersion] >= 1
                    AND ([ConfigurationProfileVersion] IS NULL OR [ConfigurationProfileVersion] >= 1)
                    AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc])
                    AND LEN([CompositionIntegrityHash]) = 64 AND ISJSON([CompositionSnapshotJson]) = 1
                    AND (([Status] = 0 AND [ActivatedAtUtc] IS NULL
                          AND [ActivatedByUserId] IS NULL AND [ActivationEvidenceReference] IS NULL)
                         OR ([Status] IN (1, 2) AND [ActivatedAtUtc] IS NOT NULL
                             AND [ActivatedByUserId] IS NOT NULL
                             AND LEN(LTRIM(RTRIM(ISNULL([ActivationEvidenceReference], '')))) > 0)));
            """);
        migrationBuilder.DropColumn("RetiredAtUtc", "ProcurementEvaluationCommitteeControls");
        migrationBuilder.DropColumn("RetiredByUserId", "ProcurementEvaluationCommitteeControls");
        migrationBuilder.DropColumn("RetirementReason", "ProcurementEvaluationCommitteeControls");
        migrationBuilder.DropColumn("RetirementEvidenceReference", "ProcurementEvaluationCommitteeControls");
        migrationBuilder.DropColumn("RetirementIdempotencyKey", "ProcurementEvaluationCommitteeControls");
    }

    private static void AllowDraftToRetiredTransition(
        MigrationBuilder migrationBuilder,
        bool allow)
    {
        var original = allow
            ? "OR (d.Status = 1 AND i.Status = 2))"
            : "OR (d.Status = 1 AND i.Status = 2) OR (d.Status = 0 AND i.Status = 3))";
        var replacement = allow
            ? "OR (d.Status = 1 AND i.Status = 2) OR (d.Status = 0 AND i.Status = 3))"
            : "OR (d.Status = 1 AND i.Status = 2))";
        migrationBuilder.Sql($$"""
            DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(
                OBJECT_ID(N'[dbo].[TR_ProcurementEvaluationCommitteeControls_Lifecycle]'));
            IF @definition IS NULL
                THROW 51305, 'Evaluation committee lifecycle trigger is missing.', 1;
            IF CHARINDEX(N'{{replacement}}', @definition) = 0
            BEGIN
                DECLARE @updated nvarchar(max) = REPLACE(@definition, N'{{original}}', N'{{replacement}}');
                IF @updated = @definition
                    THROW 51306, 'Evaluation committee lifecycle trigger transition guard could not be upgraded.', 1;

                DECLARE @triggerIndex int = CHARINDEX(N'TRIGGER', UPPER(@updated));
                DECLARE @createIndex int = CHARINDEX(N'CREATE', UPPER(@updated));
                DECLARE @alterIndex int = CHARINDEX(N'ALTER', UPPER(@updated));
                IF @triggerIndex > 0 AND @createIndex > 0 AND @createIndex < @triggerIndex
                    SET @updated = STUFF(
                        @updated,
                        @createIndex,
                        @triggerIndex - @createIndex,
                        N'ALTER ');
                ELSE IF @triggerIndex = 0 OR @alterIndex = 0 OR @alterIndex > @triggerIndex
                    THROW 51307, 'Evaluation committee lifecycle trigger definition cannot be altered safely.', 1;

                EXEC sys.sp_executesql @updated;
            END;
            """);
    }
}
