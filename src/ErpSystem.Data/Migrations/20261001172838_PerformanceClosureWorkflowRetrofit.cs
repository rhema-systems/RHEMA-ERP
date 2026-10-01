using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Performance closure F-b (D-94, D-101, D-104): the four seeded performance approval routes, retrofitted on a database
    /// whose routes were seeded before the rules — the person who submits a record does not approve it, and a pay or
    /// employment proposal is the Managing Director's to decide.
    /// </summary>
    /// <remarks>
    /// <para><b>Data only</b> — the model does not change. It edits the JSON configuration of the approval steps of
    /// <i>PIP Approval</i>, <i>Salary Review Approval</i>, <i>Employment Action Approval</i> and <i>Appraisal Template
    /// Approval</i>, each found by its seeded name <b>and</b> its entity type, so a route a tenant authored under another
    /// name, and every other module's route, is left alone:</para>
    /// <list type="bullet">
    /// <item>on the two proposal routes, the HR and TenantAdmin approver rules come off: HR prepares a proposal, and an
    /// administrator does not make a pay or employment decision (D-12, D-104);</item>
    /// <item>on all four, <c>preventInitiatorApproval</c> and <c>requireDistinctApprovers</c> are set, the pair the
    /// seeder's maker-checker repair sets — the engine then refuses the submitter on every path, the generic approval
    /// routes that bypass the HR services included.</item>
    /// </list>
    /// <para>The services enforce the same rules on both of their paths whether or not this has run; this makes the engine
    /// stop asking people who may not decide — and stop showing them an Approve button that would fail.</para>
    /// <para><b>Why a migration:</b> the seeder only adds, so a route seeded before this change is never reached by it.
    /// On a database built from empty the workflow tables are empty when this runs, and <c>seed-workflows</c> then creates
    /// the routes with these settings already (<c>EnsureHrWorkflowsSeededAsync</c>).</para>
    /// <para>⚠ <b>Refused, not half-done:</b> a proposal step left with no approver once HR and TenantAdmin are taken off
    /// stops the migration with the count, and nothing is changed. Running it twice changes nothing the second time.</para>
    /// <para>Down changes nothing: which rules a tenant's routes held before was not kept, and the code before this
    /// migration refused the same people in its services.</para>
    /// <para>Proven on a COPY_ONLY restore of UAT before it was applied: Up, Up again (no change), a planted HR-only
    /// proposal step refused and rolled back.</para>
    /// </remarks>
    public partial class PerformanceClosureWorkflowRetrofit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.WorkflowSteps', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.WorkflowDefinitions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.WorkflowEntityTypes', 'U') IS NOT NULL
BEGIN
    -- The four seeded performance routes: each definition by its seeded name AND its entity type.
    DECLARE @routes TABLE (DefinitionName nvarchar(200) NOT NULL, EntityCode nvarchar(100) NOT NULL, EntityName nvarchar(200) NOT NULL, IsProposal bit NOT NULL);
    INSERT INTO @routes VALUES
        (N'PIP Approval', N'PERFORMANCE_IMPROVEMENT_PLAN', N'Performance Improvement Plan', 0),
        (N'Salary Review Approval', N'SALARY_REVIEW_PROPOSAL', N'Salary Review Proposal', 1),
        (N'Employment Action Approval', N'EMPLOYMENT_ACTION_PROPOSAL', N'Employment Action Proposal', 1),
        (N'Appraisal Template Approval', N'APPRAISAL_TEMPLATE', N'Appraisal Template', 0);

    DECLARE @steps TABLE (StepId uniqueidentifier NOT NULL PRIMARY KEY, IsProposal bit NOT NULL);
    INSERT INTO @steps (StepId, IsProposal)
    SELECT s.Id, r.IsProposal
    FROM dbo.WorkflowSteps s
    JOIN dbo.WorkflowDefinitions d ON d.Id = s.WorkflowDefinitionId
    JOIN dbo.WorkflowEntityTypes et ON et.Id = d.EntityTypeId
    JOIN @routes r ON r.DefinitionName = d.Name AND (et.Code = r.EntityCode OR et.Name = r.EntityName)
    WHERE s.IsDeleted = 0 AND d.IsDeleted = 0 AND s.StepType = 2
      AND ISJSON(s.Configuration) = 1
      AND JSON_QUERY(s.Configuration, '$.approvalConfig') IS NOT NULL;

    -- Refused rather than leaving a proposal step with nobody to decide it.
    DECLARE @stranded int = (
        SELECT COUNT(*) FROM @steps st JOIN dbo.WorkflowSteps s ON s.Id = st.StepId
        WHERE st.IsProposal = 1
          AND NOT EXISTS (
              SELECT 1 FROM OPENJSON(s.Configuration, '$.approvalConfig.approverRules') ar
              WHERE NOT (JSON_VALUE(ar.[value], '$.assignmentType') = N'Role'
                         AND JSON_VALUE(ar.[value], '$.role') IN (N'HR', N'TenantAdmin'))));
    IF @stranded > 0
    BEGIN
        DECLARE @strandedMessage nvarchar(400) = CONCAT(N'Performance closure retrofit refused: ', @stranded,
            N' proposal approval step(s) name only HR or TenantAdmin, and taking them off would leave no approver. Add the Managing Director to them first.');
        THROW 50000, @strandedMessage, 1;
    END

    -- 1. HR and TenantAdmin off the two proposal routes (D-12, D-101, D-104). The other rules keep their order.
    UPDATE s
    SET s.Configuration = JSON_MODIFY(s.Configuration, '$.approvalConfig.approverRules', JSON_QUERY(
            (SELECT N'[' + ISNULL(STRING_AGG(CAST(ar.[value] AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY CAST(ar.[key] AS int)), N'') + N']'
             FROM OPENJSON(s.Configuration, '$.approvalConfig.approverRules') ar
             WHERE NOT (JSON_VALUE(ar.[value], '$.assignmentType') = N'Role'
                        AND JSON_VALUE(ar.[value], '$.role') IN (N'HR', N'TenantAdmin'))))),
        s.UpdatedAt = SYSUTCDATETIME(),
        s.UpdatedBy = N'System (performance closure retrofit)'
    FROM dbo.WorkflowSteps s
    JOIN @steps st ON st.StepId = s.Id
    WHERE st.IsProposal = 1
      AND EXISTS (
          SELECT 1 FROM OPENJSON(s.Configuration, '$.approvalConfig.approverRules') ar
          WHERE JSON_VALUE(ar.[value], '$.assignmentType') = N'Role'
            AND JSON_VALUE(ar.[value], '$.role') IN (N'HR', N'TenantAdmin'));

    -- 2. The submitter barred on all four, with distinct approvers (D-94, D-101).
    UPDATE s
    SET s.Configuration = JSON_MODIFY(JSON_MODIFY(s.Configuration,
                '$.approvalConfig.preventInitiatorApproval', CAST(1 AS bit)),
                '$.approvalConfig.requireDistinctApprovers', CAST(1 AS bit)),
        s.UpdatedAt = SYSUTCDATETIME(),
        s.UpdatedBy = N'System (performance closure retrofit)'
    FROM dbo.WorkflowSteps s
    JOIN @steps st ON st.StepId = s.Id
    WHERE ISNULL(JSON_VALUE(s.Configuration, '$.approvalConfig.preventInitiatorApproval'), N'false') <> N'true'
       OR ISNULL(JSON_VALUE(s.Configuration, '$.approvalConfig.requireDistinctApprovers'), N'false') <> N'true';
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: see the remarks.
        }
    }
}
