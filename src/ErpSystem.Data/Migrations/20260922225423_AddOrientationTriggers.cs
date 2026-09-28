using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane I — orientation triggers that fire, and onboarding templates that know who they
    /// are for.
    /// </summary>
    /// <remarks>
    /// <para><b>Four things.</b></para>
    /// <list type="bullet">
    ///   <item><c>OrientationAudienceRules.Population</c> — the new-hires / management / contractors
    ///   narrowing, layered on a target that now uses the shared <c>HrAudienceTargetType</c>.</item>
    ///   <item><c>EmployeeOrientations.AudienceRuleId / TriggerEvent / TriggerDate</c> — what created
    ///   an automatic enrollment. No FK on the rule id, on purpose: a rule may be deleted and the
    ///   enrollment must keep saying what created it.</item>
    ///   <item><c>OnboardingPlans.TemplateSelectionReason</c> — why the system chose a template when
    ///   it created a plan on hire confirmation.</item>
    ///   <item><c>OnboardingPlanTemplateAudiences</c> — who a template is for.</item>
    /// </list>
    ///
    /// <para><b>⚠ The rule targets are remapped, ONCE.</b> <c>OrientationAudienceRules.TargetType</c>
    /// held <c>OrientationAudienceScope</c> values; it now holds <c>HrAudienceTargetType</c>. Two
    /// values mean the same in both (1 AllEmployees, 5 Location); the rest move:</para>
    /// <list type="table">
    ///   <item><term>2 NewHires</term><description>1 AllEmployees + Population 1 NewHires</description></item>
    ///   <item><term>3 OrganizationUnit</term><description>2 OrganizationUnit</description></item>
    ///   <item><term>6 Role</term><description>4 Position</description></item>
    ///   <item><term>7 Management</term><description>1 AllEmployees + Population 2 Management</description></item>
    ///   <item><term>8 Contractors</term><description>1 AllEmployees + Population 3 Contractors</description></item>
    ///   <item><term>4 JobGrade, 99 Custom</term><description>no evaluable equivalent — the rule is
    ///   DEACTIVATED and its description says why, rather than silently re-pointed at an axis its id
    ///   does not belong to</description></item>
    /// </list>
    /// <para>The remap runs inside the same guard that adds <c>Population</c>, so it runs exactly
    /// once: on a database this migration has already reached, or one built from the model (which
    /// has the column and the new meaning already), it does nothing. It is a single UPDATE keyed on
    /// the OLD value, so no row is mapped twice by passing through an intermediate value.</para>
    ///
    /// <para><b>⚠ Guarded SQL throughout</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has all of this and a bare
    /// <c>AddColumn</c>/<c>CreateTable</c> stops the chain for everyone. Data work is raw SQL because
    /// the fast EF build strips the target models <c>UpdateData</c> needs.</para>
    /// </remarks>
    public partial class AddOrientationTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 — Population, and the one-time remap of the rule targets.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrientationAudienceRules', 'Population') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrientationAudienceRules] ADD [Population] int NOT NULL
        CONSTRAINT [DF_OrientationAudienceRules_Population] DEFAULT (0);

    EXEC(N'
UPDATE [dbo].[OrientationAudienceRules] SET
    [Population] = CASE [TargetType] WHEN 2 THEN 1 WHEN 7 THEN 2 WHEN 8 THEN 3 ELSE 0 END,
    [IsActive]   = CASE WHEN [TargetType] IN (4, 99) THEN 0 ELSE [IsActive] END,
    [Description] = CASE WHEN [TargetType] IN (4, 99)
        THEN LEFT(CONCAT(N''[Switched off during a system upgrade: rules can no longer '',
                         CASE [TargetType] WHEN 4 THEN N''target a job grade'' ELSE N''use a custom target'' END,
                         N''. Choose a unit, level, position or location instead, then switch the rule back on.] '',
                         ISNULL([Description], N'''')), 1000)
        ELSE [Description] END,
    [TargetType] = CASE [TargetType]
        WHEN 2 THEN 1   -- NewHires         -> AllEmployees + NewHires
        WHEN 3 THEN 2   -- OrganizationUnit -> OrganizationUnit
        WHEN 4 THEN 3   -- JobGrade         -> OrganizationLevel (deactivated above)
        WHEN 6 THEN 4   -- Role             -> Position
        WHEN 7 THEN 1   -- Management       -> AllEmployees + Management
        WHEN 8 THEN 1   -- Contractors      -> AllEmployees + Contractors
        WHEN 99 THEN 1  -- Custom           -> AllEmployees (deactivated above)
        ELSE [TargetType] END,  -- 1 AllEmployees and 5 Location mean the same in both
    [TargetEntityId] = CASE WHEN [TargetType] IN (2, 7, 8, 99) THEN NULL ELSE [TargetEntityId] END;');
END");

            // 2 — what created an automatic enrollment.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeOrientations', 'AudienceRuleId') IS NULL
    ALTER TABLE [dbo].[EmployeeOrientations] ADD [AudienceRuleId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.EmployeeOrientations', 'TriggerEvent') IS NULL
    ALTER TABLE [dbo].[EmployeeOrientations] ADD [TriggerEvent] int NULL;
IF COL_LENGTH('dbo.EmployeeOrientations', 'TriggerDate') IS NULL
    ALTER TABLE [dbo].[EmployeeOrientations] ADD [TriggerDate] date NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeOrientations_AudienceRuleId'
               AND object_id = OBJECT_ID('dbo.EmployeeOrientations'))
    CREATE INDEX [IX_EmployeeOrientations_AudienceRuleId] ON [dbo].[EmployeeOrientations] ([AudienceRuleId]);");

            // 3 — why the system chose an onboarding template.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OnboardingPlans', 'TemplateSelectionReason') IS NULL
    ALTER TABLE [dbo].[OnboardingPlans] ADD [TemplateSelectionReason] nvarchar(500) NULL;");

            // 4 — who an onboarding template is for.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.OnboardingPlanTemplateAudiences', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OnboardingPlanTemplateAudiences] (
        [Id] uniqueidentifier NOT NULL,
        [PlanTemplateId] uniqueidentifier NOT NULL,
        [TargetType] int NOT NULL,
        [TargetEntityId] uniqueidentifier NULL,
        [IsInclusive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_OnboardingPlanTemplateAudiences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingPlanTemplateAudiences_OnboardingPlanTemplates_PlanTemplateId]
            FOREIGN KEY ([PlanTemplateId]) REFERENCES [dbo].[OnboardingPlanTemplates] ([Id]),
        CONSTRAINT [FK_OnboardingPlanTemplateAudiences_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingPlanTemplateAudiences_TenantId'
               AND object_id = OBJECT_ID('dbo.OnboardingPlanTemplateAudiences'))
    CREATE INDEX [IX_OnboardingPlanTemplateAudiences_TenantId] ON [dbo].[OnboardingPlanTemplateAudiences] ([TenantId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingTemplateAudience_TemplateId'
               AND object_id = OBJECT_ID('dbo.OnboardingPlanTemplateAudiences'))
    CREATE INDEX [IX_OnboardingTemplateAudience_TemplateId] ON [dbo].[OnboardingPlanTemplateAudiences] ([PlanTemplateId]);");
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>⚠ <b>Down maps the rule targets BACK before dropping Population.</b> It has to: Up's
        /// remap is keyed on the old values, and several numbers mean different things before and
        /// after (2 is NewHires before, OrganizationUnit after). A Down that only dropped the column
        /// would leave the new meanings behind, and the next Up would remap them a second time —
        /// found by running Down then Up on a scratch database, where a unit rule came back as
        /// "everyone".</para>
        /// <para>The reverse is lossy where the old enum could not say the thing: a population on a
        /// narrower target keeps the population and loses the target (it widens), and a
        /// single-employee rule — which had no old equivalent — becomes Custom and is
        /// deactivated.</para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.OnboardingPlanTemplateAudiences', 'U') IS NOT NULL
    DROP TABLE [dbo].[OnboardingPlanTemplateAudiences];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeOrientations_AudienceRuleId'
           AND object_id = OBJECT_ID('dbo.EmployeeOrientations'))
    DROP INDEX [IX_EmployeeOrientations_AudienceRuleId] ON [dbo].[EmployeeOrientations];

IF COL_LENGTH('dbo.EmployeeOrientations', 'AudienceRuleId') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeOrientations] DROP COLUMN [AudienceRuleId];
IF COL_LENGTH('dbo.EmployeeOrientations', 'TriggerEvent') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeOrientations] DROP COLUMN [TriggerEvent];
IF COL_LENGTH('dbo.EmployeeOrientations', 'TriggerDate') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeOrientations] DROP COLUMN [TriggerDate];
IF COL_LENGTH('dbo.OnboardingPlans', 'TemplateSelectionReason') IS NOT NULL
    ALTER TABLE [dbo].[OnboardingPlans] DROP COLUMN [TemplateSelectionReason];

IF COL_LENGTH('dbo.OrientationAudienceRules', 'Population') IS NOT NULL
BEGIN
    EXEC(N'
UPDATE [dbo].[OrientationAudienceRules] SET
    [IsActive] = CASE WHEN [Population] = 0 AND [TargetType] = 6 THEN 0 ELSE [IsActive] END,
    [TargetEntityId] = CASE WHEN [Population] <> 0 THEN NULL ELSE [TargetEntityId] END,
    [TargetType] = CASE
        WHEN [Population] = 1 THEN 2    -- NewHires
        WHEN [Population] = 2 THEN 7    -- Management
        WHEN [Population] = 3 THEN 8    -- Contractors
        WHEN [TargetType] = 2 THEN 3    -- OrganizationUnit
        WHEN [TargetType] = 3 THEN 4    -- OrganizationLevel -> JobGrade (the nearest the old enum had)
        WHEN [TargetType] = 4 THEN 6    -- Position -> Role
        WHEN [TargetType] = 6 THEN 99   -- Employee -> Custom (deactivated above)
        ELSE [TargetType] END;');

    DECLARE @df sysname;
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.OrientationAudienceRules') AND c.name = 'Population';
    IF @df IS NOT NULL
        EXEC('ALTER TABLE [dbo].[OrientationAudienceRules] DROP CONSTRAINT [' + @df + ']');
    ALTER TABLE [dbo].[OrientationAudienceRules] DROP COLUMN [Population];
END");
        }
    }
}
