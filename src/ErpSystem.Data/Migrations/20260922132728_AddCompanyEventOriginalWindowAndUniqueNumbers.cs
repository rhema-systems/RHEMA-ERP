using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane D-2 (D7) — company-schedule defects C-2 and C-6.
    /// </summary>
    /// <remarks>
    /// <para><b>C-2:</b> four nullable columns on <c>CompanyEvents</c> holding the window an event
    /// was originally scheduled for. The reschedule used to overwrite <c>StartDate</c>/<c>EndDate</c>
    /// and keep nothing — <c>RescheduledDate</c> records <i>when somebody pressed the button</i>, not
    /// what the event moved from — while the dialog told the user the original was retained.</para>
    ///
    /// <para><b>C-6:</b> <c>EventNumber</c>, <c>RoomCode</c> and <c>BookingNumber</c> become UNIQUE
    /// per tenant. They were plain indexes, and the generators issued <c>COUNT(*) + 1</c> over live
    /// rows — so a soft-deleted row freed its number, the next create took it, and nothing objected.
    /// The generators now go through the shared sequence; this is the guard that makes any future
    /// regression fail loudly rather than duplicate silently.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has the new unique indexes
    /// and does NOT have the old plain ones — and a bare <c>DropIndex</c> on an index that is not
    /// there stops the chain.</para>
    ///
    /// <para><b>⚠ A unique index cannot be built over existing duplicates</b>, and duplicates are
    /// exactly what the old generator could produce. <c>ErpSystemDB_UAT</c> has none — measured
    /// before this was written — but a tenant that deleted an event and created another might, and
    /// SQL Server's own failure names the index rather than the cause. The <c>Up</c> therefore
    /// checks first and stops with a sentence naming the table, the column and the shared values.
    /// It deliberately does <b>not</b> repair them: these references are printed on agendas and
    /// quoted in emails, and which row keeps a shared one is a decision for whoever knows what the
    /// rows are.</para>
    ///
    /// <para>⚠ The standalone <c>TenantId</c> indexes are dropped deliberately — the composite
    /// <c>(TenantId, Number)</c> serves a TenantId-only lookup as a prefix, so keeping both would be
    /// paying for the same index twice. That was EF's own decision, kept.</para>
    /// </remarks>
    public partial class AddCompanyEventOriginalWindowAndUniqueNumbers : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{column} sysname;
    SELECT @df_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string CreateIndex(string table, string index, string columns, bool unique = false) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [{index}] ON [dbo].[{table}] ({columns});";

        /// <summary>
        /// Stops the migration with a sentence somebody can act on, if a number is already shared.
        /// </summary>
        /// <remarks>
        /// <para>A unique index cannot be built over a column that already contains duplicates, and
        /// duplicates are what the old <c>COUNT(*) + 1</c> generator could produce. Without this
        /// guard the failure is SQL Server's own — <i>"CREATE UNIQUE INDEX terminated because a
        /// duplicate key was found"</i> — which names the index and not the cause, and leaves the
        /// reader to work out which rows and what to do.</para>
        ///
        /// <para><b>⚠ It REFUSES; it does not repair.</b> An earlier draft renumbered the duplicates
        /// automatically, oldest row keeping the number. That was the wrong trade: these references
        /// are printed on agendas, quoted in emails and written in people's notes, and a migration
        /// that silently rewrites one is worse than a migration that stops. Which row keeps a shared
        /// reference is a decision for whoever knows what the rows are, taken with their eyes open.
        /// <c>ErpSystemDB_UAT</c> has none of these, measured before this was written.</para>
        /// </remarks>
        private static string RefuseOnDuplicates(string table, string column) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND EXISTS (
        SELECT 1 FROM [dbo].[{table}]
         GROUP BY [TenantId], [{column}]
        HAVING COUNT(*) > 1)
BEGIN
    DECLARE @dupes_{column} nvarchar(max);
    SELECT @dupes_{column} = STRING_AGG(CONVERT(nvarchar(max), [{column}]), ', ')
      FROM (SELECT DISTINCT [{column}] FROM [dbo].[{table}]
             GROUP BY [TenantId], [{column}] HAVING COUNT(*) > 1) d;

    RAISERROR (
        N'{table}.{column} already contains values shared by more than one row (%s), so a unique index cannot be created. These came from the old count-based generator, which reissued a number after a soft delete. Decide which row keeps each reference and renumber the others, then run this migration again.',
        16, 1, @dupes_{column});
END";

        private const string Events = "CompanyEvents";
        private const string Rooms = "MeetingRooms";
        private const string Bookings = "RoomBookings";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── C-2 ──────────────────────────────────────────────────────────────
            migrationBuilder.Sql(AddColumn(Events, "OriginalStartDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Events, "OriginalStartTime", "time NULL"));
            migrationBuilder.Sql(AddColumn(Events, "OriginalEndDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Events, "OriginalEndTime", "time NULL"));

            // ── C-6: check, then drop the old, then build the unique ────────────
            //
            // ⚠ The check comes FIRST and stops the migration, rather than letting CREATE UNIQUE
            // INDEX fail with an error that names the index instead of the problem. It does not
            // repair: see RefuseOnDuplicates for why a migration must not quietly renumber a
            // reference somebody may have printed.
            migrationBuilder.Sql(RefuseOnDuplicates(Events, "EventNumber"));
            migrationBuilder.Sql(RefuseOnDuplicates(Rooms, "RoomCode"));
            migrationBuilder.Sql(RefuseOnDuplicates(Bookings, "BookingNumber"));

            migrationBuilder.Sql(DropIndex(Events, "IX_CompanyEvents_EventNumber"));
            migrationBuilder.Sql(DropIndex(Events, "IX_CompanyEvents_TenantId"));
            migrationBuilder.Sql(DropIndex(Rooms, "IX_MeetingRooms_RoomCode"));
            migrationBuilder.Sql(DropIndex(Rooms, "IX_MeetingRooms_TenantId"));
            migrationBuilder.Sql(DropIndex(Bookings, "IX_RoomBookings_BookingNumber"));
            migrationBuilder.Sql(DropIndex(Bookings, "IX_RoomBookings_TenantId"));

            migrationBuilder.Sql(CreateIndex(Events, "IX_CompanyEvent_Tenant_EventNumber",
                "[TenantId], [EventNumber]", unique: true));
            migrationBuilder.Sql(CreateIndex(Rooms, "IX_MeetingRoom_Tenant_RoomCode",
                "[TenantId], [RoomCode]", unique: true));
            migrationBuilder.Sql(CreateIndex(Bookings, "IX_RoomBooking_Tenant_BookingNumber",
                "[TenantId], [BookingNumber]", unique: true));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropIndex(Events, "IX_CompanyEvent_Tenant_EventNumber"));
            migrationBuilder.Sql(DropIndex(Rooms, "IX_MeetingRoom_Tenant_RoomCode"));
            migrationBuilder.Sql(DropIndex(Bookings, "IX_RoomBooking_Tenant_BookingNumber"));

            migrationBuilder.Sql(CreateIndex(Events, "IX_CompanyEvents_EventNumber", "[EventNumber]"));
            migrationBuilder.Sql(CreateIndex(Events, "IX_CompanyEvents_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Rooms, "IX_MeetingRooms_RoomCode", "[RoomCode]"));
            migrationBuilder.Sql(CreateIndex(Rooms, "IX_MeetingRooms_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Bookings, "IX_RoomBookings_BookingNumber", "[BookingNumber]"));
            migrationBuilder.Sql(CreateIndex(Bookings, "IX_RoomBookings_TenantId", "[TenantId]"));

            migrationBuilder.Sql(DropColumn(Events, "OriginalEndTime"));
            migrationBuilder.Sql(DropColumn(Events, "OriginalEndDate"));
            migrationBuilder.Sql(DropColumn(Events, "OriginalStartTime"));
            migrationBuilder.Sql(DropColumn(Events, "OriginalStartDate"));
        }
    }
}
