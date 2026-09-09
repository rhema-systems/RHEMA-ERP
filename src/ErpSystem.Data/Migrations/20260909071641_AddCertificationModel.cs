using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Demo feedback round 2, lane C2 (docs/HR/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 1.3, § 6.3): the
    /// certification model — a credential catalogue under the certifying body, the skill and
    /// position links that consume it, what an employee holds with its evidence, and the pair the
    /// expiry sweep writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>. <c>rebuild-db</c> builds from the EF model
    /// rather than the migration chain, so a rebuilt database already has these tables, columns,
    /// indexes and foreign keys, and a bare <c>CreateTable</c> / <c>AddColumn</c> fails on it.
    /// </para>
    /// <para>
    /// <b>⚠ <c>CertificationExpiryLeadDays</c> is added with a REAL default of 60, not the
    /// scaffold's 0.</b> The generated form writes <c>defaultValue: 0</c> and then repairs the one
    /// SEEDED row by id — so every other tenant's row, and any row a service created, would keep
    /// the zero. A zero lead time is not a harmless default here: the sweep only warns while
    /// <c>daysRemaining &lt;= lead</c>, so a tenant left at zero would be told about a lapsed
    /// licence on the day it lapsed and never before. The <c>DEFAULT</c> constraint populates every
    /// existing row at ALTER time, which is why the scaffold's <c>UpdateData</c> is gone — it is
    /// redundant, and a data operation cannot run on the fast EF build at all (it resolves column
    /// types from a target model that build strips).
    /// </para>
    /// <para>
    /// <b>Delete behaviour follows the model.</b> Cascade where the child has no meaning without
    /// its parent (a skill's accepted credentials, a position's requirements, a person's
    /// credentials); NO ACTION everywhere else, so a catalogue row that anything cites cannot be
    /// deleted out from under it — the service refuses that with a sentence naming who cites it.
    /// </para>
    /// </remarks>
    public partial class AddCertificationModel : Migration
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

        private static string CreateTable(string table, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NULL
CREATE TABLE [dbo].[{table}] (
    [Id] uniqueidentifier NOT NULL,{columns}{AuditColumns}
    CONSTRAINT [PK_{table}] PRIMARY KEY ([Id])
);";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns, string filter = null) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns}){(filter is null ? "" : $" WHERE {filter}")};";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}) WHERE {filter};";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The catalogue ─────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateTable("Certifications", @"
    [CertifyingBodyId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Code] nvarchar(50) NULL,
    [Kind] int NOT NULL,
    [Description] nvarchar(1000) NULL,
    [ValidityMonths] int NULL,
    [RenewalRequired] bit NOT NULL,
    [ExpiryNotificationLeadDays] int NULL,
    [IsActive] bit NOT NULL,"));

            migrationBuilder.Sql(CreateIndex("Certifications", "IX_Certifications_CertifyingBodyId", "[CertifyingBodyId]"));
            migrationBuilder.Sql(CreateIndex("Certifications", "IX_Certifications_TenantId_IsActive", "[TenantId], [IsActive]"));
            // Name unique per body, code unique per body — both filtered, so a retired (soft-deleted)
            // row does not block its replacement and the code index ignores rows without a code.
            migrationBuilder.Sql(CreateUniqueIndex("Certifications", "UX_Certification_Tenant_Body_Name",
                "[TenantId], [CertifyingBodyId], [Name]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateUniqueIndex("Certifications", "UX_Certification_Tenant_Body_Code",
                "[TenantId], [CertifyingBodyId], [Code]", "[IsDeleted] = 0 AND [Code] IS NOT NULL"));

            migrationBuilder.Sql(AddForeignKey("Certifications", "FK_Certifications_CertifyingBodies_CertifyingBodyId", "CertifyingBodyId", "CertifyingBodies"));
            migrationBuilder.Sql(AddForeignKey("Certifications", "FK_Certifications_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 2. Which credential evidences a skill ────────────────────────────────
            migrationBuilder.Sql(CreateTable("SkillCertifications", @"
    [SkillId] uniqueidentifier NOT NULL,
    [CertificationId] uniqueidentifier NOT NULL,
    [IsMandatory] bit NOT NULL,
    [Notes] nvarchar(500) NULL,"));

            migrationBuilder.Sql(CreateIndex("SkillCertifications", "IX_SkillCertifications_SkillId", "[SkillId]"));
            migrationBuilder.Sql(CreateIndex("SkillCertifications", "IX_SkillCertifications_CertificationId", "[CertificationId]"));
            migrationBuilder.Sql(CreateUniqueIndex("SkillCertifications", "UX_SkillCertification_Tenant_Skill_Certification",
                "[TenantId], [SkillId], [CertificationId]", "[IsDeleted] = 0"));

            migrationBuilder.Sql(AddForeignKey("SkillCertifications", "FK_SkillCertifications_Skills_SkillId", "SkillId", "Skills", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("SkillCertifications", "FK_SkillCertifications_Certifications_CertificationId", "CertificationId", "Certifications"));
            migrationBuilder.Sql(AddForeignKey("SkillCertifications", "FK_SkillCertifications_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 3. What a post must hold ─────────────────────────────────────────────
            migrationBuilder.Sql(CreateTable("PositionCertificationRequirements", @"
    [PositionId] uniqueidentifier NOT NULL,
    [CertificationId] uniqueidentifier NOT NULL,
    [IsMandatory] bit NOT NULL,
    [Notes] nvarchar(500) NULL,"));

            migrationBuilder.Sql(CreateIndex("PositionCertificationRequirements", "IX_PositionCertificationRequirements_PositionId", "[PositionId]"));
            migrationBuilder.Sql(CreateIndex("PositionCertificationRequirements", "IX_PositionCertificationRequirements_CertificationId", "[CertificationId]"));
            migrationBuilder.Sql(CreateUniqueIndex("PositionCertificationRequirements", "UX_PositionCertificationRequirement_Tenant_Position_Certification",
                "[TenantId], [PositionId], [CertificationId]", "[IsDeleted] = 0"));

            migrationBuilder.Sql(AddForeignKey("PositionCertificationRequirements", "FK_PositionCertificationRequirements_EmployeePositions_PositionId", "PositionId", "EmployeePositions", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("PositionCertificationRequirements", "FK_PositionCertificationRequirements_Certifications_CertificationId", "CertificationId", "Certifications"));
            migrationBuilder.Sql(AddForeignKey("PositionCertificationRequirements", "FK_PositionCertificationRequirements_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 4. What a person holds, with its evidence ────────────────────────────
            migrationBuilder.Sql(CreateTable("EmployeeCertifications", @"
    [EmployeeId] uniqueidentifier NOT NULL,
    [CertificationId] uniqueidentifier NOT NULL,
    [CertificateNumber] nvarchar(100) NULL,
    [IssuedOn] date NULL,
    [ExpiresOn] date NULL,
    [IsRevoked] bit NOT NULL,
    [RevokedOn] date NULL,
    [RevocationReason] nvarchar(500) NULL,
    [EvidenceFileUploadRecordId] uniqueidentifier NULL,
    [EvidenceDocumentRecordId] uniqueidentifier NULL,
    [EvidenceDocumentVersionId] uniqueidentifier NULL,
    [EvidenceFileName] nvarchar(255) NULL,
    [EvidenceMimeType] nvarchar(150) NULL,
    [EvidenceFileSizeBytes] bigint NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedById] uniqueidentifier NULL,
    [VerifiedOn] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,"));

            migrationBuilder.Sql(CreateIndex("EmployeeCertifications", "IX_EmployeeCertifications_EmployeeId", "[EmployeeId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeCertifications", "IX_EmployeeCertifications_CertificationId", "[CertificationId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeCertifications", "IX_EmployeeCertifications_VerifiedById", "[VerifiedById]"));
            migrationBuilder.Sql(CreateIndex("EmployeeCertifications", "IX_EmployeeCertifications_TenantId_EmployeeId", "[TenantId], [EmployeeId]"));
            // The sweep's own read: every live credential due inside its lead window.
            migrationBuilder.Sql(CreateIndex("EmployeeCertifications", "IX_EmployeeCertifications_TenantId_ExpiresOn", "[TenantId], [ExpiresOn]"));
            // The same credential twice for one person is a renewal, and a renewal carries a new
            // number; the same number twice is a duplicate entry.
            migrationBuilder.Sql(CreateUniqueIndex("EmployeeCertifications", "UX_EmployeeCertification_Tenant_Employee_Certification_Number",
                "[TenantId], [EmployeeId], [CertificationId], [CertificateNumber]",
                "[IsDeleted] = 0 AND [CertificateNumber] IS NOT NULL"));

            migrationBuilder.Sql(AddForeignKey("EmployeeCertifications", "FK_EmployeeCertifications_Employees_EmployeeId", "EmployeeId", "Employees", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("EmployeeCertifications", "FK_EmployeeCertifications_Certifications_CertificationId", "CertificationId", "Certifications"));
            migrationBuilder.Sql(AddForeignKey("EmployeeCertifications", "FK_EmployeeCertifications_Employees_VerifiedById", "VerifiedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey("EmployeeCertifications", "FK_EmployeeCertifications_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 5. The expiry sweep's run header and dispatch rows ───────────────────
            migrationBuilder.Sql(CreateTable("CertificationExpiryReminderRuns", @"
    [StartedAt] datetime2 NOT NULL,
    [CompletedAt] datetime2 NULL,
    [Trigger] nvarchar(30) NOT NULL,
    [TriggeredByUserId] uniqueidentifier NULL,
    [RemindersQueued] int NOT NULL,"));

            migrationBuilder.Sql(CreateIndex("CertificationExpiryReminderRuns", "IX_CertificationExpiryRun_Tenant_StartedAt", "[TenantId], [StartedAt]"));
            migrationBuilder.Sql(AddForeignKey("CertificationExpiryReminderRuns", "FK_CertificationExpiryReminderRuns_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(CreateTable("CertificationExpiryDispatchLogs", @"
    [RunId] uniqueidentifier NOT NULL,
    [Kind] nvarchar(60) NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeCertificationId] uniqueidentifier NOT NULL,
    [CertificationId] uniqueidentifier NOT NULL,
    [Reference] nvarchar(300) NULL,
    [DueDate] date NULL,
    [DaysRemaining] int NOT NULL,
    [EscalationTier] int NOT NULL,
    [RoutedToEmployeeId] uniqueidentifier NULL,
    [DedupeKey] nvarchar(200) NOT NULL,"));

            migrationBuilder.Sql(CreateIndex("CertificationExpiryDispatchLogs", "IX_CertificationExpiryDispatch_RunId", "[RunId]"));
            migrationBuilder.Sql(CreateIndex("CertificationExpiryDispatchLogs", "IX_CertificationExpiryDispatch_EmployeeId", "[EmployeeId]"));
            // Looked up on every sweep for every candidate — the one index that decides whether a
            // daily pass over a whole workforce is cheap.
            migrationBuilder.Sql(CreateIndex("CertificationExpiryDispatchLogs", "IX_CertificationExpiryDispatch_Tenant_DedupeKey", "[TenantId], [DedupeKey]"));

            migrationBuilder.Sql(AddForeignKey("CertificationExpiryDispatchLogs", "FK_CertificationExpiryDispatchLogs_CertificationExpiryReminderRuns_RunId", "RunId", "CertificationExpiryReminderRuns"));
            migrationBuilder.Sql(AddForeignKey("CertificationExpiryDispatchLogs", "FK_CertificationExpiryDispatchLogs_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 6. The three columns on existing tables ──────────────────────────────
            //
            // ⚠ A REAL default, not the scaffold's 0 — see the remarks. This populates every
            // existing tenant's settings row at ALTER time, so the scaffold's UpdateData (which
            // repaired only the seeded row, by id, and cannot run on the fast EF build at all) is
            // deliberately gone.
            migrationBuilder.Sql(AddColumn("CompanyHrPolicySettings", "CertificationExpiryLeadDays",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_CertificationExpiryLeadDays] DEFAULT (60)"));

            migrationBuilder.Sql(AddColumn("EmployeeSkills", "EmployeeCertificationId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndex("EmployeeSkills", "IX_EmployeeSkills_EmployeeCertificationId", "[EmployeeCertificationId]"));
            migrationBuilder.Sql(AddForeignKey("EmployeeSkills", "FK_EmployeeSkills_EmployeeCertifications_EmployeeCertificationId", "EmployeeCertificationId", "EmployeeCertifications"));

            migrationBuilder.Sql(AddColumn("JobQualifications", "CertificationId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndex("JobQualifications", "IX_JobQualifications_CertificationId", "[CertificationId]"));
            migrationBuilder.Sql(AddForeignKey("JobQualifications", "FK_JobQualifications_Certifications_CertificationId", "CertificationId", "Certifications"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible for the schema; what is lost is every catalogue row, every skill and
            // position link, and every credential a person holds (the evidence files themselves
            // stay in the document store).
            migrationBuilder.Sql(DropForeignKey("EmployeeSkills", "FK_EmployeeSkills_EmployeeCertifications_EmployeeCertificationId"));
            migrationBuilder.Sql(DropIndex("EmployeeSkills", "IX_EmployeeSkills_EmployeeCertificationId"));
            migrationBuilder.Sql(DropColumn("EmployeeSkills", "EmployeeCertificationId"));

            migrationBuilder.Sql(DropForeignKey("JobQualifications", "FK_JobQualifications_Certifications_CertificationId"));
            migrationBuilder.Sql(DropIndex("JobQualifications", "IX_JobQualifications_CertificationId"));
            migrationBuilder.Sql(DropColumn("JobQualifications", "CertificationId"));

            migrationBuilder.Sql(DropColumn("CompanyHrPolicySettings", "CertificationExpiryLeadDays"));

            migrationBuilder.Sql(DropTable("CertificationExpiryDispatchLogs"));
            migrationBuilder.Sql(DropTable("CertificationExpiryReminderRuns"));
            migrationBuilder.Sql(DropTable("EmployeeCertifications"));
            migrationBuilder.Sql(DropTable("PositionCertificationRequirements"));
            migrationBuilder.Sql(DropTable("SkillCertifications"));
            migrationBuilder.Sql(DropTable("Certifications"));
        }
    }
}
