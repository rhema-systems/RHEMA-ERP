using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane C3: named sets — a bundle of benefits, of skills, of credentials, attached to a post in
    /// one move instead of row by row (plan § 1.5, § 6.4; register rows P-3, S-3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these tables and a bare <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap this time</b>, and it is worth saying why, because the last three
    /// migrations in this round all had one. EF scaffolds <c>defaultValue: 0</c> for a non-nullable
    /// enum or bool column added to an EXISTING table, ignoring the property initialiser. Every
    /// column here but one is in a NEW table, where there are no existing rows to default, and the
    /// one added to an existing table (<c>SourceBenefitGroupId</c>) is nullable. Nothing to repair.
    /// </para>
    ///
    /// <para>
    /// <b>Every unique index carries <c>WHERE [IsDeleted] = 0</c></b>, following C2 rather than the
    /// older <c>PositionSkillRequirements</c> / <c>EmployeePositionBenefits</c> indexes, which lack
    /// the filter and are the reason those two syncs have to find a soft-deleted row and revive it
    /// instead of inserting. Detaching a set and re-attaching it here is an ordinary insert.
    /// </para>
    ///
    /// <para>
    /// <b>Delete behaviour.</b> A member or attachment dies with the set or the post that owns it
    /// (<c>CASCADE</c>); every reference to a catalogue row — a benefit policy, a skill, a
    /// credential — and every reference from a post to a SET is <c>NO ACTION</c>, so a set a post is
    /// standing on cannot be deleted out from under it. The service turns that into a sentence
    /// naming how many posts hold it, rather than letting the database raise a constraint error.
    /// </para>
    /// </remarks>
    public partial class AddNamedSets : Migration
    {
        private const string AuditColumns = @"
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,";

        /// <summary>The four columns every one of the three masters carries, identically.</summary>
        private const string SetHeaderColumns = @"
    [Name] nvarchar(150) NOT NULL,
    [Code] nvarchar(50) NULL,
    [Description] nvarchar(1000) NULL,
    [IsActive] bit NOT NULL,";

        private static string CreateTable(string table, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NULL
CREATE TABLE [dbo].[{table}] (
    [Id] uniqueidentifier NOT NULL,{columns}{AuditColumns}
    CONSTRAINT [PK_{table}] PRIMARY KEY ([Id])
);";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}) WHERE {filter};";

        private static string AddForeignKey(string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropTable(string table) =>
            $"IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL DROP TABLE [dbo].[{table}];";

        /// <remarks>
        /// ⚠ The default constraint is looked up rather than named: on a model-built database
        /// (<c>rebuild-db</c>) the name is server-generated, so guessing it would leave the column
        /// undroppable and the <c>Down</c> broken.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{column} sysname;
    SELECT @df_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{column} IS NOT NULL EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        /// <summary>The two unique indexes every master carries: name, and code where one is given.</summary>
        private static void SetHeaderIndexes(MigrationBuilder b, string table, string singular)
        {
            b.Sql(CreateUniqueIndex(table, $"UX_{singular}_Tenant_Name", "[TenantId], [Name]", "[IsDeleted] = 0"));
            b.Sql(CreateUniqueIndex(table, $"UX_{singular}_Tenant_Code", "[TenantId], [Code]", "[IsDeleted] = 0 AND [Code] IS NOT NULL"));
            b.Sql(AddForeignKey(table, $"FK_{table}_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The three masters ─────────────────────────────────────────────────
            migrationBuilder.Sql(CreateTable("BenefitGroups", SetHeaderColumns));
            SetHeaderIndexes(migrationBuilder, "BenefitGroups", "BenefitGroup");

            migrationBuilder.Sql(CreateTable("SkillSets", SetHeaderColumns));
            SetHeaderIndexes(migrationBuilder, "SkillSets", "SkillSet");

            migrationBuilder.Sql(CreateTable("CertificationSets", SetHeaderColumns));
            SetHeaderIndexes(migrationBuilder, "CertificationSets", "CertificationSet");

            // ── 2. What is in each set ───────────────────────────────────────────────
            // ⚠ A benefit member carries the policy and NOTHING else — no amount, no expiry. A post
            // needing its own figure for a benefit takes that benefit individually (plan Q-5).
            migrationBuilder.Sql(CreateTable("BenefitGroupMembers", @"
    [BenefitGroupId] uniqueidentifier NOT NULL,
    [PolicyId] uniqueidentifier NOT NULL,"));
            migrationBuilder.Sql(CreateIndex("BenefitGroupMembers", "IX_BenefitGroupMembers_BenefitGroupId", "[BenefitGroupId]"));
            migrationBuilder.Sql(CreateIndex("BenefitGroupMembers", "IX_BenefitGroupMembers_PolicyId", "[PolicyId]"));
            migrationBuilder.Sql(CreateUniqueIndex("BenefitGroupMembers", "UX_BenefitGroupMember_Tenant_Group_Policy",
                "[TenantId], [BenefitGroupId], [PolicyId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey("BenefitGroupMembers", "FK_BenefitGroupMembers_BenefitGroups_BenefitGroupId", "BenefitGroupId", "BenefitGroups", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("BenefitGroupMembers", "FK_BenefitGroupMembers_BenefitPolicies_PolicyId", "PolicyId", "BenefitPolicies"));
            migrationBuilder.Sql(AddForeignKey("BenefitGroupMembers", "FK_BenefitGroupMembers_Tenants_TenantId", "TenantId", "Tenants"));

            // A skill member carries the same three attributes the individual position row carries,
            // which is what makes attaching the set equivalent to attaching its rows.
            migrationBuilder.Sql(CreateTable("SkillSetMembers", @"
    [SkillSetId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [RequiredLevel] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [Priority] int NOT NULL,"));
            migrationBuilder.Sql(CreateIndex("SkillSetMembers", "IX_SkillSetMembers_SkillSetId", "[SkillSetId]"));
            migrationBuilder.Sql(CreateIndex("SkillSetMembers", "IX_SkillSetMembers_SkillId", "[SkillId]"));
            migrationBuilder.Sql(CreateUniqueIndex("SkillSetMembers", "UX_SkillSetMember_Tenant_Set_Skill",
                "[TenantId], [SkillSetId], [SkillId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey("SkillSetMembers", "FK_SkillSetMembers_SkillSets_SkillSetId", "SkillSetId", "SkillSets", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("SkillSetMembers", "FK_SkillSetMembers_Skills_SkillId", "SkillId", "Skills"));
            migrationBuilder.Sql(AddForeignKey("SkillSetMembers", "FK_SkillSetMembers_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(CreateTable("CertificationSetMembers", @"
    [CertificationSetId] uniqueidentifier NOT NULL,
    [CertificationId] uniqueidentifier NOT NULL,
    [IsMandatory] bit NOT NULL,"));
            migrationBuilder.Sql(CreateIndex("CertificationSetMembers", "IX_CertificationSetMembers_CertificationSetId", "[CertificationSetId]"));
            migrationBuilder.Sql(CreateIndex("CertificationSetMembers", "IX_CertificationSetMembers_CertificationId", "[CertificationId]"));
            migrationBuilder.Sql(CreateUniqueIndex("CertificationSetMembers", "UX_CertificationSetMember_Tenant_Set_Certification",
                "[TenantId], [CertificationSetId], [CertificationId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey("CertificationSetMembers", "FK_CertificationSetMembers_CertificationSets_CertificationSetId", "CertificationSetId", "CertificationSets", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("CertificationSetMembers", "FK_CertificationSetMembers_Certifications_CertificationId", "CertificationId", "Certifications"));
            migrationBuilder.Sql(AddForeignKey("CertificationSetMembers", "FK_CertificationSetMembers_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 3. Which sets a post holds ───────────────────────────────────────────
            // Attachments, nothing more. What the post actually requires is these unioned with its
            // individual rows, computed on read by PositionNamedSetService — deliberately NOT
            // materialised here, so that editing a set's membership is at once true of every post.
            migrationBuilder.Sql(CreateTable("EmployeePositionBenefitGroups", @"
    [PositionId] uniqueidentifier NOT NULL,
    [BenefitGroupId] uniqueidentifier NOT NULL,"));
            migrationBuilder.Sql(CreateIndex("EmployeePositionBenefitGroups", "IX_EmployeePositionBenefitGroups_PositionId", "[PositionId]"));
            migrationBuilder.Sql(CreateIndex("EmployeePositionBenefitGroups", "IX_EmployeePositionBenefitGroups_BenefitGroupId", "[BenefitGroupId]"));
            migrationBuilder.Sql(CreateUniqueIndex("EmployeePositionBenefitGroups", "UX_EmployeePositionBenefitGroup_Tenant_Position_Group",
                "[TenantId], [PositionId], [BenefitGroupId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey("EmployeePositionBenefitGroups", "FK_EmployeePositionBenefitGroups_EmployeePositions_PositionId", "PositionId", "EmployeePositions", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("EmployeePositionBenefitGroups", "FK_EmployeePositionBenefitGroups_BenefitGroups_BenefitGroupId", "BenefitGroupId", "BenefitGroups"));
            migrationBuilder.Sql(AddForeignKey("EmployeePositionBenefitGroups", "FK_EmployeePositionBenefitGroups_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(CreateTable("PositionSkillSets", @"
    [PositionId] uniqueidentifier NOT NULL,
    [SkillSetId] uniqueidentifier NOT NULL,"));
            migrationBuilder.Sql(CreateIndex("PositionSkillSets", "IX_PositionSkillSets_PositionId", "[PositionId]"));
            migrationBuilder.Sql(CreateIndex("PositionSkillSets", "IX_PositionSkillSets_SkillSetId", "[SkillSetId]"));
            migrationBuilder.Sql(CreateUniqueIndex("PositionSkillSets", "UX_PositionSkillSet_Tenant_Position_Set",
                "[TenantId], [PositionId], [SkillSetId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey("PositionSkillSets", "FK_PositionSkillSets_EmployeePositions_PositionId", "PositionId", "EmployeePositions", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("PositionSkillSets", "FK_PositionSkillSets_SkillSets_SkillSetId", "SkillSetId", "SkillSets"));
            migrationBuilder.Sql(AddForeignKey("PositionSkillSets", "FK_PositionSkillSets_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(CreateTable("PositionCertificationSets", @"
    [PositionId] uniqueidentifier NOT NULL,
    [CertificationSetId] uniqueidentifier NOT NULL,"));
            migrationBuilder.Sql(CreateIndex("PositionCertificationSets", "IX_PositionCertificationSets_PositionId", "[PositionId]"));
            migrationBuilder.Sql(CreateIndex("PositionCertificationSets", "IX_PositionCertificationSets_CertificationSetId", "[CertificationSetId]"));
            migrationBuilder.Sql(CreateUniqueIndex("PositionCertificationSets", "UX_PositionCertificationSet_Tenant_Position_Set",
                "[TenantId], [PositionId], [CertificationSetId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey("PositionCertificationSets", "FK_PositionCertificationSets_EmployeePositions_PositionId", "PositionId", "EmployeePositions", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("PositionCertificationSets", "FK_PositionCertificationSets_CertificationSets_CertificationSetId", "CertificationSetId", "CertificationSets"));
            migrationBuilder.Sql(AddForeignKey("PositionCertificationSets", "FK_PositionCertificationSets_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 4. Where a group-provided enrolment came from ────────────────────────
            // ⚠ Nullable, and deliberately NO foreign key and no navigation. The existing
            // SourcePositionBenefitId points at an individual position-benefit row; a benefit that
            // reached the post through a GROUP has no such row, so without this the provenance
            // would simply have gone null and said nothing. It is a record of where the enrollment
            // came from, not a live reference — retiring a group, or changing what is in it, must
            // not be blocked by an enrollment already made.
            migrationBuilder.Sql(AddColumn("EmployeeBenefitEnrollments", "SourceBenefitGroupId", "uniqueidentifier NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn("EmployeeBenefitEnrollments", "SourceBenefitGroupId"));

            // Reverse dependency order: attachments and members before the sets they point at.
            migrationBuilder.Sql(DropTable("PositionCertificationSets"));
            migrationBuilder.Sql(DropTable("PositionSkillSets"));
            migrationBuilder.Sql(DropTable("EmployeePositionBenefitGroups"));
            migrationBuilder.Sql(DropTable("CertificationSetMembers"));
            migrationBuilder.Sql(DropTable("SkillSetMembers"));
            migrationBuilder.Sql(DropTable("BenefitGroupMembers"));
            migrationBuilder.Sql(DropTable("CertificationSets"));
            migrationBuilder.Sql(DropTable("SkillSets"));
            migrationBuilder.Sql(DropTable("BenefitGroups"));
        }
    }
}
