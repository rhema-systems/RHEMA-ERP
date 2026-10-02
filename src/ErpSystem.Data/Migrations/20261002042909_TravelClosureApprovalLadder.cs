using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Staff travel final closure, lane 2 (decision D-7): the staff travel approval route in two stages — the
    /// traveller's line authority, then HR — on a database whose route was seeded as one stage.
    /// </summary>
    /// <remarks>
    /// <para><b>Data only</b> — the model does not change. Each tenant's seeded one-step route ("Staff Travel
    /// Approval", created by the system, one approval step on which a Manager, HR or a TenantAdmin signed once) gets a
    /// version 2 — same definition key and name, superseding it, published and active, as the workflow designer's own
    /// "new version, then publish" leaves it — and the one-step version is retired:</para>
    /// <list type="bullet">
    /// <item><b>Line manager approval</b> is addressed BY NAME: two Dynamic approver rules read the logins of the
    /// traveller's two nearest line authorities, which the engine's context for a travel request now carries
    /// (<c>SimpleWorkflowService</c>, <c>StaffTravelApprovalLadder</c>). Its required role is HR — the engine's
    /// fallback when no rule resolves, which is the decision's "a traveller with no line authority who has a login
    /// goes to HR". So the task, its notification and the inbox row reach the traveller's own supervisor or head of
    /// unit, not every holder of the Manager role.</item>
    /// <item><b>HR approval</b> — HR, with TenantAdmin as the backstop every HR route carries.</item>
    /// </list>
    /// <para>Both stages keep the superseded step's own approval settings; only who approves changes. A request already
    /// out for approval keeps the route it was submitted on — the engine reads an open instance's own definition,
    /// active or not — and is decided as that route said.</para>
    /// <para><b>Why a migration:</b> the seeder only adds. On a database built from empty the workflow tables are
    /// empty when this runs, and <c>seed-workflows</c> then creates the two-stage route directly
    /// (<c>EnsureStaffTravelWorkflowSeededAsync</c>). A tenant that authored its own travel route, or already has a
    /// two-stage one, is left alone.</para>
    /// <para>⚠ <b>Refused, not guessed at:</b> a tenant with two seeded one-step routes active at once stops the
    /// migration with the count, and nothing is changed. Running it twice changes nothing the second time.</para>
    /// <para>Down changes nothing: the one-step version is kept, retired, as the route's version 1 — republishing
    /// it is a workflow-designer action, and the code before this migration decided its requests the same way.</para>
    /// <para>Proven on a COPY_ONLY restore of UAT before it was applied (27 checks): the route's shape, rules and
    /// transitions; the two requests out for approval left on version 1; Up twice (no change); two active seeded
    /// routes refused with nothing changed; a tenant-authored route left alone.</para>
    /// </remarks>
    public partial class TravelClosureApprovalLadder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.WorkflowDefinitions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.WorkflowSteps', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.WorkflowTransitions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.WorkflowEntityTypes', 'U') IS NOT NULL
BEGIN
    DECLARE @now datetime2 = SYSUTCDATETIME();
    DECLARE @by nvarchar(100) = N'System (travel closure lane 2)';

    -- The approver rules of each new stage. Stage 1 is addressed BY NAME: the engine's context for a travel request
    -- carries the logins of the traveller's two nearest line authorities under these two keys. With neither, the
    -- engine falls back to the step's required role, HR.
    DECLARE @lineRules nvarchar(max) = N'[{""approvalGroup"":1,""condition"":null,""assignmentType"":""Dynamic"",""userId"":null,""role"":null,""dynamicExpression"":""lineApproverUserId"",""priority"":0},{""approvalGroup"":1,""condition"":null,""assignmentType"":""Dynamic"",""userId"":null,""role"":null,""dynamicExpression"":""lineApproverUserId2"",""priority"":0}]';
    DECLARE @deskRules nvarchar(max) = N'[{""approvalGroup"":1,""condition"":null,""assignmentType"":""Role"",""userId"":null,""role"":""HR"",""dynamicExpression"":null,""priority"":0},{""approvalGroup"":1,""condition"":null,""assignmentType"":""Role"",""userId"":null,""role"":""TenantAdmin"",""dynamicExpression"":null,""priority"":0}]';
    -- What the seeder writes, for a superseded step whose own configuration cannot be read.
    DECLARE @configShape nvarchar(max) = N'{""assignmentRules"":null,""approvalConfig"":{""approvalType"":""Single"",""activationMode"":""Parallel"",""approverRules"":[],""minApprovalsRequired"":1,""autoApprovalCondition"":null,""rejectionHandling"":""StopWorkflow"",""preventInitiatorApproval"":false,""requireDistinctApprovers"":false,""conflictRules"":[],""signaturePolicy"":null,""evidenceRequirements"":[],""allowEvidenceException"":false,""evidenceExceptionApproverRole"":""Managing Director"",""minimumExceptionReasonLength"":30,""requiresManagingDirectorApproval"":false,""managingDirectorApproverRole"":""Managing Director""},""qualityConfig"":null,""notificationConfig"":null,""taskConfig"":null,""escalationRules"":null,""formFields"":null,""skipCondition"":null}';

    -- Each tenant's seeded one-step travel route: the travel entity type's active, published definition, named as the
    -- seeder named it, created by the system, with exactly one approval step — and no two-stage route beside it.
    DECLARE @old TABLE (
        DefinitionId uniqueidentifier NOT NULL PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        DefinitionKey uniqueidentifier NOT NULL,
        EntityTypeId uniqueidentifier NOT NULL,
        Name nvarchar(200) NOT NULL,
        Configuration nvarchar(max) NULL,
        NextVersion int NOT NULL,
        StepConfiguration nvarchar(max) NULL);

    INSERT INTO @old (DefinitionId, TenantId, DefinitionKey, EntityTypeId, Name, Configuration, NextVersion, StepConfiguration)
    SELECT d.Id, d.TenantId, d.DefinitionKey, d.EntityTypeId, d.Name, d.Configuration,
           (SELECT MAX(f.Version) FROM dbo.WorkflowDefinitions f
             WHERE f.TenantId = d.TenantId AND f.DefinitionKey = d.DefinitionKey) + 1,
           (SELECT TOP (1) s.Configuration FROM dbo.WorkflowSteps s
             WHERE s.WorkflowDefinitionId = d.Id AND s.IsDeleted = 0 AND s.StepType = 2)
    FROM dbo.WorkflowDefinitions d
    JOIN dbo.WorkflowEntityTypes et ON et.Id = d.EntityTypeId
    WHERE d.IsDeleted = 0 AND d.IsActive = 1 AND d.LifecycleStatus = 1
      AND (et.Code = N'STAFF_TRAVEL_REQUEST' OR et.Name = N'Staff Travel Request')
      AND (d.Name = N'Staff Travel Approval' OR d.Name LIKE N'Staff Travel Approval %')
      AND d.CreatedBy = N'System'
      AND (SELECT COUNT(*) FROM dbo.WorkflowSteps s
            WHERE s.WorkflowDefinitionId = d.Id AND s.IsDeleted = 0 AND s.StepType = 2) = 1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.WorkflowDefinitions d2
          JOIN dbo.WorkflowSteps s2 ON s2.WorkflowDefinitionId = d2.Id
          WHERE d2.TenantId = d.TenantId AND d2.EntityTypeId = d.EntityTypeId
            AND d2.IsDeleted = 0 AND d2.IsActive = 1 AND s2.IsDeleted = 0
            AND s2.Name = N'Line manager approval');

    -- Refused rather than guessed at: two seeded routes active in one tenant would each become a two-stage route.
    DECLARE @ambiguous int = (SELECT COUNT(*) FROM (SELECT TenantId FROM @old GROUP BY TenantId HAVING COUNT(*) > 1) t);
    IF @ambiguous > 0
    BEGIN
        DECLARE @ambiguousMessage nvarchar(400) = CONCAT(N'Travel closure lane 2 refused: ', @ambiguous,
            N' tenant(s) hold more than one active seeded staff travel route. Retire all but one in the workflow designer first.');
        THROW 50000, @ambiguousMessage, 1;
    END

    DECLARE @map TABLE (
        OldDefinitionId uniqueidentifier NOT NULL PRIMARY KEY,
        NewDefinitionId uniqueidentifier NOT NULL,
        DraftId uniqueidentifier NOT NULL,
        LineId uniqueidentifier NOT NULL,
        DeskId uniqueidentifier NOT NULL,
        ApprovedId uniqueidentifier NOT NULL);
    INSERT INTO @map (OldDefinitionId, NewDefinitionId, DraftId, LineId, DeskId, ApprovedId)
    SELECT DefinitionId, NEWID(), NEWID(), NEWID(), NEWID(), NEWID() FROM @old;

    -- 1. The next version of the same route — same key and name, superseding the one-step version — as the workflow
    --    designer's own ""new version, then publish"" leaves it.
    INSERT INTO dbo.WorkflowDefinitions (Id, DefinitionKey, Name, Description, EntityTypeId, Version, LifecycleStatus,
        IsActive, ChangeSummary, SupersedesDefinitionId, PublishedAt, Configuration, CreatedAt, CreatedBy, IsDeleted,
        TenantId)
    SELECT m.NewDefinitionId, o.DefinitionKey, o.Name,
           N'Official travel: Draft -> line manager approval -> HR approval -> Approved.',
           o.EntityTypeId, o.NextVersion, 1, 1,
           N'Travel closure lane 2 (D-7): two stages — the traveller''s line authority, addressed by name, then HR.',
           o.DefinitionId, @now, o.Configuration, @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId;

    -- 2. Its four steps. Both approval stages keep the superseded step's own approval settings; only who approves changes.
    INSERT INTO dbo.WorkflowSteps (Id, WorkflowDefinitionId, Name, Description, StepType, [Order], IsStartStep,
        IsEndStep, IsRequired, RequiredRole, Configuration, CreatedAt, CreatedBy, IsDeleted, TenantId)
    SELECT m.DraftId, m.NewDefinitionId, N'Draft', NULL, 0, 1, 1, 0, 1, NULL, NULL, @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId
    UNION ALL
    SELECT m.LineId, m.NewDefinitionId, N'Line manager approval',
           N'The traveller''s supervisor, or the head of their unit or of a unit above it, approves the trip, rejects it or returns it for revision. A traveller with no line manager who can sign in goes to HR.',
           2, 2, 0, 0, 1, N'HR',
           JSON_MODIFY(CASE WHEN ISJSON(o.StepConfiguration) = 1 AND JSON_QUERY(o.StepConfiguration, '$.approvalConfig') IS NOT NULL
                            THEN o.StepConfiguration ELSE @configShape END,
                       '$.approvalConfig.approverRules', JSON_QUERY(@lineRules)),
           @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId
    UNION ALL
    SELECT m.DeskId, m.NewDefinitionId, N'HR approval', N'HR approves the trip and the budget it may spend.',
           2, 3, 0, 0, 1, NULL,
           JSON_MODIFY(CASE WHEN ISJSON(o.StepConfiguration) = 1 AND JSON_QUERY(o.StepConfiguration, '$.approvalConfig') IS NOT NULL
                            THEN o.StepConfiguration ELSE @configShape END,
                       '$.approvalConfig.approverRules', JSON_QUERY(@deskRules)),
           @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId
    UNION ALL
    SELECT m.ApprovedId, m.NewDefinitionId, N'Approved', NULL, 0, 4, 0, 1, 1, NULL, NULL, @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId;

    -- 3. Its transitions, in order, as the seeder names them.
    INSERT INTO dbo.WorkflowTransitions (Id, WorkflowDefinitionId, FromStepId, ToStepId, Name, IsDefault, Priority,
        CreatedAt, CreatedBy, IsDeleted, TenantId)
    SELECT NEWID(), m.NewDefinitionId, m.DraftId, m.LineId, N'Submit', 1, 0, @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId
    UNION ALL
    SELECT NEWID(), m.NewDefinitionId, m.LineId, m.DeskId, N'Approve', 1, 0, @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId
    UNION ALL
    SELECT NEWID(), m.NewDefinitionId, m.DeskId, m.ApprovedId, N'Final Approve', 1, 0, @now, N'System', 0, o.TenantId
    FROM @old o JOIN @map m ON m.OldDefinitionId = o.DefinitionId;

    -- 4. The one-step version retired, as publishing a new version retires the old. A request already out for approval
    --    keeps the route it was submitted on: the engine reads an open instance's own definition, active or not.
    UPDATE d
    SET d.IsActive = 0,
        d.LifecycleStatus = 2,
        d.RetiredAt = @now,
        d.UpdatedAt = @now,
        d.UpdatedBy = @by
    FROM dbo.WorkflowDefinitions d
    JOIN @old o ON o.DefinitionId = d.Id;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: see the remarks.
        }
    }
}
