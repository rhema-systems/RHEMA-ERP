using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane K-II-b: a medical board case's injury, its incapacity and compensation (PNDCL 187).
    /// </summary>
    /// <remarks>
    /// <para><b>New tables:</b> <c>IncapacityScheduleItems</c> (a tenant's compensation schedule; unique per
    /// tenant, schedule and injury among live rows) and <c>MedicalBoardCaseInjuries</c> (the injuries on
    /// a case, cascading from it; the schedule row is Restrict — rows are retired, never deleted).
    /// ⚠ <b>No schedule rows are inserted here</b>: a Medical administrator loads PNDCL 187's First and
    /// Third Schedules from the schedule page (<c>IncapacityScheduleDefaults</c>), which adds only rows a
    /// tenant lacks. A statute's rates are the tenant's to adopt, not something a migration writes into
    /// every database.</para>
    ///
    /// <para><b>New columns on <c>MedicalBoardCases</c></b>, all nullable: the SHE incident (a bare Guid —
    /// the SHE↔Medical boundary), the assessment, the indicative figure and its working, and the
    /// labour officer's and agreed amounts.</para>
    ///
    /// <para><b>Three settings on <c>CompanyHrPolicySettings</c></b>, with the Act's figures:
    /// <c>PermanentTotalIncapacityMonths</c> (nullable; <b>96</b> written onto every existing tenant row in
    /// the same step that adds the column, so a re-run never overwrites a tenant that has since emptied
    /// it), <c>TemporaryIncapacityMaxMonths</c> (<b>24</b>, never the scaffold's 0), and
    /// <c>CompensationEarningsCeiling</c> (nullable, <b>no default</b> — s.36's 25,000 cedis predates the
    /// redenomination; R5-Q5). ⚠ The scaffold's <c>UpdateData</c> of the seeded tenant is replaced by that
    /// backfill: it cannot run under the fast EF build, and it would have covered one tenant only.</para>
    ///
    /// <para>⚠ Every decimal is <c>decimal(18,4)</c>: <c>ConfigureDecimalPrecision</c> sets them all
    /// (see the entity). Guarded SQL throughout, as on every HR migration.</para>
    /// </remarks>
    public partial class AddIncapacityAssessment : Migration
    {
        private const string Cases = "MedicalBoardCases";
        private const string Settings = "CompanyHrPolicySettings";
        private const string Schedule = "IncapacityScheduleItems";
        private const string Injuries = "MedicalBoardCaseInjuries";

        /// <summary>The case's new columns, in the scaffold's order.</summary>
        private static readonly (string Name, string Type)[] CaseColumns =
        {
            ("AgreedCompensation", "decimal(18,4) NULL"),
            ("CompensationAgreedOn", "date NULL"),
            ("CompensationCurrency", "nvarchar(3) NULL"),
            ("CompensationDueOn", "date NULL"),
            ("CompensationNotPayableReason", "int NULL"),
            ("CompensationNotifiedOn", "date NULL"),
            ("IncapacityAssessedBy", "nvarchar(200) NULL"),
            ("IncapacityAssessedOn", "date NULL"),
            ("IncapacityKind", "int NULL"),
            ("IncapacityNotes", "nvarchar(2000) NULL"),
            ("IncapacityPercentage", "decimal(18,4) NULL"),
            ("IndicativeCompensation", "decimal(18,4) NULL"),
            ("IndicativeCompensationBasis", "nvarchar(1000) NULL"),
            ("NotifiedCompensation", "decimal(18,4) NULL"),
            ("SafetyIncidentId", "uniqueidentifier NULL"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── The case's columns ─────────────────────────────────────────────────────────────────
            foreach (var (name, type) in CaseColumns)
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Cases}', '{name}') IS NULL
    ALTER TABLE [dbo].[{Cases}] ADD [{name}] {type};");

            // ── The settings, with the Act's figures ───────────────────────────────────────────────
            // 96 is written only in the step that adds the column: dynamic, because the column does not
            // exist when this batch compiles, and inside the IF so a second run changes nothing.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', 'PermanentTotalIncapacityMonths') IS NULL
BEGIN
    ALTER TABLE [dbo].[{Settings}] ADD [PermanentTotalIncapacityMonths] int NULL;
    EXEC sp_executesql N'UPDATE [dbo].[{Settings}] SET [PermanentTotalIncapacityMonths] = 96;';
END
IF COL_LENGTH('dbo.{Settings}', 'TemporaryIncapacityMaxMonths') IS NULL
    ALTER TABLE [dbo].[{Settings}] ADD [TemporaryIncapacityMaxMonths] int NOT NULL DEFAULT (24);
IF COL_LENGTH('dbo.{Settings}', 'CompensationEarningsCeiling') IS NULL
    ALTER TABLE [dbo].[{Settings}] ADD [CompensationEarningsCeiling] decimal(18,4) NULL;");

            // ── The schedule, and the injuries assessed against it ─────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Schedule}', 'U') IS NULL
CREATE TABLE [dbo].[{Schedule}] (
    [Id]                 uniqueidentifier NOT NULL,
    [Kind]               int              NOT NULL,
    [Injury]             nvarchar(300)    NOT NULL,
    [Percentage]         decimal(18,4)    NOT NULL,
    [Source]             nvarchar(200)    NOT NULL,
    [AppliesToArmOrHand] bit              NOT NULL,
    [IsActive]           bit              NOT NULL,
    [SortOrder]          int              NOT NULL,
    [CreatedAt]          datetime2        NOT NULL,
    [UpdatedAt]          datetime2        NULL,
    [CreatedBy]          nvarchar(max)    NULL,
    [UpdatedBy]          nvarchar(max)    NULL,
    [CreatedById]        uniqueidentifier NULL,
    [LastModifiedById]   uniqueidentifier NULL,
    [IsDeleted]          bit              NOT NULL,
    [DeletedAt]          datetime2        NULL,
    [DeletedBy]          nvarchar(max)    NULL,
    [TenantId]           uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Schedule}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Schedule}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Injuries}', 'U') IS NULL
CREATE TABLE [dbo].[{Injuries}] (
    [Id]               uniqueidentifier NOT NULL,
    [CaseId]           uniqueidentifier NOT NULL,
    [ScheduleItemId]   uniqueidentifier NULL,
    [Description]      nvarchar(300)    NOT NULL,
    [BasePercentage]   decimal(18,4)    NOT NULL,
    [LossOfUse]        int              NOT NULL,
    [NonDominantSide]  bit              NOT NULL,
    [Percentage]       decimal(18,4)    NOT NULL,
    [SortOrder]        int              NOT NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Injuries}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Injuries}_{Schedule}_ScheduleItemId] FOREIGN KEY ([ScheduleItemId])
        REFERENCES [dbo].[{Schedule}] ([Id]),
    CONSTRAINT [FK_{Injuries}_{Cases}_CaseId] FOREIGN KEY ([CaseId])
        REFERENCES [dbo].[{Cases}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Injuries}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql(CreateIndex(Schedule, $"UX_{Schedule}_Tenant_Kind_Injury", "[TenantId], [Kind], [Injury]",
                unique: true, filter: "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateIndex(Injuries, $"IX_{Injuries}_CaseId", "[CaseId]"));
            migrationBuilder.Sql(CreateIndex(Injuries, $"IX_{Injuries}_ScheduleItemId", "[ScheduleItemId]"));
            migrationBuilder.Sql(CreateIndex(Injuries, $"IX_{Injuries}_TenantId", "[TenantId]"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ The injuries go with their table, and the schedule with its — assessments recorded since
            // are lost with them, as any Down loses what only the new shape could hold.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Injuries}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{Injuries}];
IF OBJECT_ID('dbo.{Schedule}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{Schedule}];");

            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "CompensationEarningsCeiling"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "TemporaryIncapacityMaxMonths"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "PermanentTotalIncapacityMonths"));

            foreach (var (name, _) in CaseColumns)
                migrationBuilder.Sql(DropColumnWithDefaultSql(Cases, name));
        }

        private static string CreateIndex(string table, string index, string columns, bool unique = false, string filter = null)
        {
            var uniqueness = unique ? "UNIQUE " : string.Empty;
            var where = filter is null ? string.Empty : " WHERE " + filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {uniqueness}INDEX [{index}] ON [dbo].[{table}] ({columns}){where};";
        }

        /// <summary>
        /// Drops a column that may carry an unnamed default: the default constraint first, then the column.
        /// </summary>
        private static string DropColumnWithDefaultSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";
    }
}
