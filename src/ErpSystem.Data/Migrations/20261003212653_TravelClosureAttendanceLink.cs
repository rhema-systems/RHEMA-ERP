using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Staff travel final closure, lane 9, slice 9a (<c>docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md</c>
    /// § 1 D-53, finding V1): the trip that posted an attendance day as <c>OnDuty</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Added:</b> <c>StaffDailyAttendances.StaffTravelRequestId</c> — nullable, indexed, a foreign key to the trip
    /// that sets itself null if the trip is deleted. Leave's posting owns its days by <c>LeaveRequestId</c>; travel's owns
    /// its days by this, so a reversal touches only the rows a trip wrote, never a clerk's own <c>OnDuty</c> day.</para>
    ///
    /// <para><b>No data step.</b> No code has ever written <c>OnDuty</c> (V2), so there is nothing to link; the posting
    /// service writes the column from this slice on, and the nightly sweep posts the trips already approved.</para>
    ///
    /// <para><b>Down</b> refuses while any attendance day carries a trip's link — without the column those days would be
    /// <c>OnDuty</c> rows nobody owns, which the previous code cannot tell from a clerk's — then drops the key, the index
    /// and the column.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: each statement checks for the object it changes, so a database built
    /// from the model skips what it already has.</para>
    /// </remarks>
    public partial class TravelClosureAttendanceLink : Migration
    {
        private const string Attendance = "StaffDailyAttendances";
        private const string Column = "StaffTravelRequestId";
        private const string Index = "IX_StaffDailyAttendances_StaffTravelRequestId";
        private const string ForeignKey = "FK_StaffDailyAttendances_StaffTravelRequests_StaffTravelRequestId";

        // Dynamic SQL: a batch naming the column does not compile where the column is gone, whatever its IF says — so a
        // second Down, or a Down where Up never added it, would fail on the name before the guard ran.
        private static readonly string RefuseDownWithLinksSql = $@"
IF COL_LENGTH('dbo.{Attendance}', '{Column}') IS NOT NULL
    EXEC(N'IF EXISTS (SELECT 1 FROM [dbo].[{Attendance}] WHERE [{Column}] IS NOT NULL)
        THROW 50001, N''Attendance days posted by staff travel exist (StaffDailyAttendances.StaffTravelRequestId). Without the column they would be OnDuty days nobody owns, which the previous code cannot tell from a clerk entry; remove or unlink them deliberately before rolling this migration back.'', 1;');";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Attendance}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{Attendance}', '{Column}') IS NULL
    ALTER TABLE [dbo].[{Attendance}] ADD [{Column}] uniqueidentifier NULL;");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Attendance}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{Index}' AND object_id = OBJECT_ID('dbo.{Attendance}'))
    CREATE INDEX [{Index}] ON [dbo].[{Attendance}] ([{Column}]);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Attendance}', 'U') IS NOT NULL AND OBJECT_ID('dbo.StaffTravelRequests', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.{ForeignKey}', 'F') IS NULL
    ALTER TABLE [dbo].[{Attendance}] ADD CONSTRAINT [{ForeignKey}]
        FOREIGN KEY ([{Column}]) REFERENCES [dbo].[StaffTravelRequests] ([Id]) ON DELETE SET NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseDownWithLinksSql);

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{ForeignKey}', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{Attendance}] DROP CONSTRAINT [{ForeignKey}];");

            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{Index}' AND object_id = OBJECT_ID('dbo.{Attendance}'))
    DROP INDEX [{Index}] ON [dbo].[{Attendance}];");

            // The column carries no default, so it goes on its own.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Attendance}', '{Column}') IS NOT NULL
    ALTER TABLE [dbo].[{Attendance}] DROP COLUMN [{Column}];");
        }
    }
}
