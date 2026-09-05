using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Keeps evaluation committee binding anchored to the exact sourcing-case
/// snapshot. A configuration version that was Published and was retained by
/// the exact source policy remains valid after it is superseded and retired;
/// Draft, cross-tenant, mismatched, deleted, or unregistered lineage remains
/// rejected by the database boundary.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902110000_AllowHistoricalEvaluationCommitteeSourceLineage")]
public sealed class AllowHistoricalEvaluationCommitteeSourceLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateLifecycleTrigger(migrationBuilder, allowHistoricalSourceLineage: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        CreateLifecycleTrigger(migrationBuilder, allowHistoricalSourceLineage: false);

    private static void CreateLifecycleTrigger(
        MigrationBuilder migrationBuilder,
        bool allowHistoricalSourceLineage)
    {
        var sourcingCaseJoin = allowHistoricalSourceLineage
            ? """
              LEFT JOIN [dbo].[ProcurementSourcingCases] sc
                ON sc.Id = CASE
                    WHEN i.SourceType = 0 THEN t.SourcingCaseId
                    WHEN i.SourceType = 1 THEN r.SourcingCaseId
                    ELSE NULL
                END
               AND sc.TenantId = i.TenantId
               AND sc.IsDeleted = 0
              """
            : string.Empty;

        var sourcingCaseValidation = allowHistoricalSourceLineage
            ? """
                 OR i.SourceType NOT IN (0, 1)
                 OR sc.Id IS NULL
                 OR sc.PolicySetId <> i.PolicySetId
                 OR sc.PolicyCode <> i.PolicyCode OR sc.PolicyVersion <> i.PolicyVersion
                 OR sc.MethodRuleId <> i.MethodRuleId OR sc.MethodRuleCode <> i.MethodRuleCode
              """
            : string.Empty;

        var insertionLifecycleValidation = allowHistoricalSourceLineage
            ? """
              c.Status <> 1
              OR c.EffectiveFrom > SYSUTCDATETIME()
              OR (c.EffectiveTo IS NOT NULL AND c.EffectiveTo < SYSUTCDATETIME())
              OR p.LifecycleStatus NOT IN (1, 2)
              OR p.PublishedAt IS NULL
              OR p.PublishedAt > sc.CreatedAt
              OR p.EffectiveFrom > sc.CreatedAt
              OR (p.EffectiveTo IS NOT NULL AND
                  p.EffectiveTo < sc.CreatedAt)
              OR (p.RetiredAt IS NOT NULL AND
                  p.RetiredAt < sc.CreatedAt)
              OR cp.LifecycleStatus NOT IN (1, 2)
              OR cp.PublishedAt IS NULL
              OR mr.EffectiveFrom > sc.CreatedAt
              OR (mr.EffectiveTo IS NOT NULL AND
                  mr.EffectiveTo < sc.CreatedAt)
              """
            : """
              c.Status <> 1 OR p.LifecycleStatus <> 1
              OR mr.IsAllowed = 0 OR mr.IsEnabled = 0
              OR c.EffectiveFrom > SYSUTCDATETIME()
              OR (c.EffectiveTo IS NOT NULL AND c.EffectiveTo < SYSUTCDATETIME())
              OR (cp.Id IS NOT NULL AND cp.LifecycleStatus <> 1)
              """;

        migrationBuilder.Sql($$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementEvaluationCommitteeControls_Lifecycle]
            ON [dbo].[ProcurementEvaluationCommitteeControls]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51300, 'Evaluation committee controls cannot be deleted.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [dbo].[Tenders] t
                      ON i.SourceType = 0 AND t.Id = i.SourceId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                    LEFT JOIN [dbo].[RequestForQuotations] r
                      ON i.SourceType = 1 AND r.Id = i.SourceId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                    {{sourcingCaseJoin}}
                    LEFT JOIN [dbo].[ProcurementCommittees] c
                      ON c.Id = i.CommitteeTemplateId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                    LEFT JOIN [dbo].[ProcurementPolicySets] p
                      ON p.Id = i.PolicySetId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                    LEFT JOIN [dbo].[ProcurementConfigurationProfiles] cp
                      ON cp.Id = i.ConfigurationProfileId AND cp.TenantId = i.TenantId AND cp.IsDeleted = 0
                    LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr
                      ON mr.Id = i.MethodRuleId AND mr.TenantId = i.TenantId AND mr.IsDeleted = 0
                    LEFT JOIN [dbo].[WorkflowDefinitions] wd
                      ON wd.Id = i.WorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                    LEFT JOIN [dbo].[WorkflowInstances] wi
                      ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId AND wi.IsDeleted = 0
                    WHERE i.IsDeleted = 1
                       OR (i.SourceType = 0 AND t.Id IS NULL)
                       OR (i.SourceType = 1 AND r.Id IS NULL)
                       OR (i.SourceType = 0 AND t.TenderNumber <> i.SourceReference)
                       OR (i.SourceType = 1 AND r.RfqNumber <> i.SourceReference)
                       {{sourcingCaseValidation}}
                       OR c.Id IS NULL OR c.CommitteeType <> 2
                       OR c.Code <> i.CommitteeCode OR c.Name <> i.CommitteeName
                       OR c.RequiredQuorum <> i.RequiredQuorum
                       OR p.Id IS NULL
                       OR p.Code <> i.PolicyCode OR p.Version <> i.PolicyVersion
                       OR mr.Id IS NULL OR mr.PolicySetId <> i.PolicySetId
                       OR mr.RuleCode <> i.MethodRuleCode
                       OR ISNULL(mr.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                       OR (i.ConfigurationProfileId IS NOT NULL AND
                           (cp.Id IS NULL OR p.SourceConfigurationProfileId <> i.ConfigurationProfileId
                            OR cp.ProfileCode <> i.ConfigurationProfileCode
                            OR cp.Version <> i.ConfigurationProfileVersion))
                       OR (i.ConfigurationProfileId IS NULL AND
                           (i.ConfigurationProfileCode IS NOT NULL OR i.ConfigurationProfileVersion IS NOT NULL))
                       OR (i.WorkflowDefinitionId IS NOT NULL AND wd.Id IS NULL)
                       OR (i.WorkflowInstanceId IS NOT NULL AND
                           (wi.Id IS NULL OR i.WorkflowDefinitionId IS NULL
                            OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId))
                       OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id)
                           AND ({{insertionLifecycleValidation}})))
                    THROW 51301, 'Evaluation committee source, template, policy, method, configuration, workflow, or tenant lineage is invalid.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId
                       OR i.SourceType <> d.SourceType OR i.SourceId <> d.SourceId OR i.Version <> d.Version
                       OR i.SourceReference <> d.SourceReference OR i.Purpose <> d.Purpose
                       OR i.CommitteeTemplateId <> d.CommitteeTemplateId OR i.CommitteeCode <> d.CommitteeCode
                       OR i.CommitteeName <> d.CommitteeName OR i.RequiredQuorum <> d.RequiredQuorum
                       OR i.PolicySetId <> d.PolicySetId OR i.PolicyCode <> d.PolicyCode OR i.PolicyVersion <> d.PolicyVersion
                       OR ISNULL(i.ConfigurationProfileId, '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.ConfigurationProfileId, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.ConfigurationProfileCode, '') <> ISNULL(d.ConfigurationProfileCode, '')
                       OR ISNULL(i.ConfigurationProfileVersion, 0) <> ISNULL(d.ConfigurationProfileVersion, 0)
                       OR i.MethodRuleId <> d.MethodRuleId OR i.MethodRuleCode <> d.MethodRuleCode
                       OR ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                       OR i.EffectiveFromUtc <> d.EffectiveFromUtc
                       OR ISNULL(i.EffectiveToUtc, '99991231') <> ISNULL(d.EffectiveToUtc, '99991231')
                       OR i.CompositionSnapshotJson <> d.CompositionSnapshotJson
                       OR i.CompositionIntegrityHash <> d.CompositionIntegrityHash
                       OR i.CreationIdempotencyKey <> d.CreationIdempotencyKey
                       OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                       OR NOT (i.Status = d.Status
                           OR (d.Status = 0 AND i.Status = 1)
                           OR (d.Status = 1 AND i.Status = 2))
                       OR (d.Status <> 0 AND
                           (ISNULL(i.ActivatedAtUtc, '19000101') <> ISNULL(d.ActivatedAtUtc, '19000101')
                            OR ISNULL(i.ActivatedByUserId, '00000000-0000-0000-0000-000000000000')
                               <> ISNULL(d.ActivatedByUserId, '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(i.ActivationEvidenceReference, '') <> ISNULL(d.ActivationEvidenceReference, '')
                            OR ISNULL(i.ActivationIdempotencyKey, '') <> ISNULL(d.ActivationIdempotencyKey, ''))))
                    THROW 51302, 'Evaluation committee immutable lineage or lifecycle transition is invalid.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status = 0 AND i.Status = 1
                      AND (
                          (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                           WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0 AND a.MemberKind = 0) <> 1
                          OR
                          (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                           WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0 AND a.MemberKind = 4) <> 1
                          OR
                          (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                           WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0 AND a.IsVoting = 1) < i.RequiredQuorum
                          OR EXISTS (
                              SELECT 1
                              FROM [dbo].[ProcurementEvaluationCommitteeRoleRequirements] rr
                              WHERE rr.CommitteeControlId = i.Id AND rr.TenantId = i.TenantId AND rr.IsDeleted = 0
                                AND (SELECT COUNT(*)
                                     FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                     WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                                       AND a.MemberKind = rr.MemberKind
                                       AND UPPER(LTRIM(RTRIM(a.RoleName))) = UPPER(LTRIM(RTRIM(rr.RoleName)))
                                       AND (rr.IsVoting = 0 OR a.IsVoting = 1)) < rr.MinimumCount)))
                    THROW 51303, 'Evaluation committee activation requires the exact configured Chair, Secretary, voting quorum, and role composition.', 1;
            END
            """);
    }
}
