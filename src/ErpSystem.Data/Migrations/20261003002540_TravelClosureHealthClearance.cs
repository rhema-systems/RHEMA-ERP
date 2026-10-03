using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Staff travel final closure, lane 7, slice 7b (<c>docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md</c>
    /// § 1 D-36, D-39): the health clearance a trip records per destination requirement, and the reason a trip's visa
    /// flag stands against the visa register.
    /// </summary>
    /// <remarks>
    /// <para><b>Added:</b> <c>StaffTravelHealthClearances</c> — one live row per trip and health requirement: the officer
    /// who checked it (an Employee), when, and a note; unique on (tenant, trip, requirement) among live rows, so an
    /// untick (a soft delete) leaves the requirement free to be ticked again (D-36, T-25). And
    /// <c>StaffTravelRequests.VisaOverrideReason</c>: null, the server derives <c>RequiresVisa</c> from the register for
    /// the traveller's passport; set, the flag stands as the requester left it (D-39).</para>
    ///
    /// <para><b>No data step.</b> Nothing exists to backfill: no trip has been cleared or overridden before this.</para>
    ///
    /// <para><b>Down</b> refuses while a live clearance exists — it is a duty-of-care record that someone checked a
    /// traveller's certificate, which the previous code has nowhere to keep — then drops the table and the column (an
    /// override's reason is also on the trip as an internal note).</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: each statement checks for the object it changes, so a database built
    /// from the model skips what it already has.</para>
    /// </remarks>
    public partial class TravelClosureHealthClearance : Migration
    {
        private const string Requests = "StaffTravelRequests";
        private const string Clearances = "StaffTravelHealthClearances";

        private static readonly string CreateTableSql = $@"
IF OBJECT_ID('dbo.{Clearances}', 'U') IS NULL
    CREATE TABLE [dbo].[{Clearances}] (
        [Id] uniqueidentifier NOT NULL,
        [StaffTravelRequestId] uniqueidentifier NOT NULL,
        [HealthRequirementId] uniqueidentifier NOT NULL,
        [ClearedById] uniqueidentifier NOT NULL,
        [ClearedAt] datetime2 NOT NULL,
        [Note] nvarchar(1000) NULL,
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
        CONSTRAINT [PK_{Clearances}] PRIMARY KEY ([Id])
    );";

        private static readonly string RefuseDownWithClearancesSql = $@"
IF OBJECT_ID('dbo.{Clearances}', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[{Clearances}] WHERE [IsDeleted] = 0)
    THROW 50001, 'Live health clearances exist (StaffTravelHealthClearances). They record that a traveller''s health requirement was checked, which the previous code cannot keep; remove them deliberately before rolling this migration back.', 1;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The trip: why its visa flag stands against the register (D-39) ───────────────────────
            migrationBuilder.Sql(AddColumnSql(Requests, "VisaOverrideReason", "nvarchar(1000) NULL"));

            // ── 2. The clearances (D-36) ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateTableSql);
            migrationBuilder.Sql(AddForeignKeySql(Clearances, $"FK_{Clearances}_Employees_ClearedById", "ClearedById", "Employees"));
            migrationBuilder.Sql(AddForeignKeySql(Clearances,
                $"FK_{Clearances}_StaffTravelHealthRequirements_HealthRequirementId", "HealthRequirementId", "StaffTravelHealthRequirements"));
            migrationBuilder.Sql(AddForeignKeySql(Clearances,
                $"FK_{Clearances}_StaffTravelRequests_StaffTravelRequestId", "StaffTravelRequestId", Requests));
            migrationBuilder.Sql(AddForeignKeySql(Clearances, $"FK_{Clearances}_Tenants_TenantId", "TenantId", "Tenants"));
            migrationBuilder.Sql(CreateIndexSql(Clearances, $"IX_{Clearances}_ClearedById", "[ClearedById]"));
            migrationBuilder.Sql(CreateIndexSql(Clearances, $"IX_{Clearances}_HealthRequirementId", "[HealthRequirementId]"));
            migrationBuilder.Sql(CreateIndexSql(Clearances, $"IX_{Clearances}_StaffTravelRequestId", "[StaffTravelRequestId]"));
            migrationBuilder.Sql(CreateIndexSql(Clearances,
                $"IX_{Clearances}_TenantId_StaffTravelRequestId_HealthRequirementId",
                "[TenantId], [StaffTravelRequestId], [HealthRequirementId]", unique: true, filter: "[IsDeleted] = 0"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseDownWithClearancesSql);
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Clearances}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{Clearances}];");
            migrationBuilder.Sql(DropColumnSql(Requests, "VisaOverrideReason"));
        }

        private static string AddColumnSql(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndexSql(string table, string index, string columns, bool unique = false, string filter = null) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [{index}] ON [dbo].[{table}] ({columns}){(filter is null ? string.Empty : $" WHERE {filter}")};";

        private static string AddForeignKeySql(string table, string name, string column, string principal) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{name}', 'F') IS NULL
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{name}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principal}] ([Id]);";

        /// <summary>The column carries no default, so it goes on its own.</summary>
        private static string DropColumnSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";
    }
}
