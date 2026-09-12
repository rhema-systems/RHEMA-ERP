using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The FR-HR-092 decision on a separation — who signed it, who refused it and why, and how the
    /// unserved notice was settled (area 9b slice 3).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-092: the Managing Director signs all terminations except procedural ones, which
    /// HR approves per policy — the FRD's only example being absence beyond ten days. Nothing in
    /// the existing model could distinguish such a case, so it is now stated rather than inferred:
    /// <c>EmployeeSeparations.AbsenceDays</c> against
    /// <c>CompanyHrPolicySettings.ProceduralAbsenceDays</c>.</para>
    ///
    /// <para><b>ProceduralAbsenceDays defaults to 10 here, not to 0.</b> The scaffold wrote
    /// <c>defaultValue: 0</c> — that is the CLR default for <c>int</c>, not a decision, and it would
    /// have silently set every already-provisioned tenant to "the MD signs everything". 10 is the
    /// entity's default and the FRD's stated policy, so an existing settings row gets the rule the
    /// specification describes. A tenant that genuinely wants the MD on every termination sets it
    /// to 0 deliberately.</para>
    ///
    /// <para>Rejection gets its own fields rather than reusing the approval ones: an approval and a
    /// refusal are different acts, and a record that cannot tell them apart cannot answer "was this
    /// person refused, and on what grounds".</para>
    ///
    /// <para>Scaffolded by the user, then rewritten here into guarded SQL. Discovery attributes stay
    /// in the generated <c>.Designer.cs</c>; listed in <c>FastBuildMigrationMetadata.cs</c> because
    /// fast Debug builds strip that designer.</para>
    /// </remarks>
    public partial class AddSeparationApprovalDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AbsenceDays' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [AbsenceDays] int NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsNoticeWaived' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [IsNoticeWaived] bit NOT NULL CONSTRAINT [DF_EmployeeSeparations_IsNoticeWaived] DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsNoticePaidInLieu' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [IsNoticePaidInLieu] bit NOT NULL CONSTRAINT [DF_EmployeeSeparations_IsNoticePaidInLieu] DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NoticeWaiverReason' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [NoticeWaiverReason] nvarchar(1000) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [RejectedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectedOn' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [RejectedOn] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectionReason' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [RejectionReason] nvarchar(1000) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_RejectedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparations_RejectedById] ON [dbo].[EmployeeSeparations] ([RejectedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_RejectedById')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_RejectedById]
        FOREIGN KEY ([RejectedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            // Default 10 — FR-HR-092's stated threshold, matching the entity. See the remarks above
            // for why this is not the scaffold's 0.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ProceduralAbsenceDays' AND object_id = OBJECT_ID('dbo.CompanyHrPolicySettings'))
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [ProceduralAbsenceDays] int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_ProceduralAbsenceDays] DEFAULT (10);");

            // Keep the seeded DEFAULT-tenant row identical to what the model snapshot says it holds.
            migrationBuilder.Sql(@"
UPDATE [dbo].[CompanyHrPolicySettings]
   SET [ProceduralAbsenceDays] = 10
 WHERE [Id] = 'b2c3d4e5-0000-0000-0000-000000000001'
   AND [ProceduralAbsenceDays] <> 10;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_RejectedById')
    ALTER TABLE [dbo].[EmployeeSeparations] DROP CONSTRAINT [FK_EmployeeSeparations_Employees_RejectedById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_RejectedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    DROP INDEX [IX_EmployeeSeparations_RejectedById] ON [dbo].[EmployeeSeparations];");

            // Named default constraints have to go before their columns.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CompanyHrPolicySettings_ProceduralAbsenceDays')
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP CONSTRAINT [DF_CompanyHrPolicySettings_ProceduralAbsenceDays];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ProceduralAbsenceDays' AND object_id = OBJECT_ID('dbo.CompanyHrPolicySettings'))
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [ProceduralAbsenceDays];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_EmployeeSeparations_IsNoticeWaived')
    ALTER TABLE [dbo].[EmployeeSeparations] DROP CONSTRAINT [DF_EmployeeSeparations_IsNoticeWaived];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_EmployeeSeparations_IsNoticePaidInLieu')
    ALTER TABLE [dbo].[EmployeeSeparations] DROP CONSTRAINT [DF_EmployeeSeparations_IsNoticePaidInLieu];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectionReason' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [RejectionReason];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectedOn' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [RejectedOn];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [RejectedById];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NoticeWaiverReason' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [NoticeWaiverReason];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsNoticeWaived' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [IsNoticeWaived];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsNoticePaidInLieu' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [IsNoticePaidInLieu];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AbsenceDays' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [AbsenceDays];");
        }
    }
}
