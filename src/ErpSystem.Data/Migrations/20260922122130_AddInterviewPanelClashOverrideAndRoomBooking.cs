using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane D — the panel clash check made binding, and the room actually held.
    /// </summary>
    /// <remarks>
    /// <para>Five nullable columns on <c>JobInterviews</c>. Four record that a hard clash was
    /// scheduled over — the reason, what was overridden, who decided and when — because the check
    /// now REFUSES a create, update or reschedule that double-books a panelist unless a reason is
    /// supplied (decision D-5). The fifth, <c>RoomBookingId</c>, lets an interview hold a room
    /// through the meeting-room register instead of naming one in the free-text
    /// <c>LocationOrLink</c>, which books nothing and is invisible to the room's own
    /// double-booking check.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns, this
    /// index and this foreign key, and the bare scaffolded statements stop the chain.</para>
    ///
    /// <para><b>No default-value trap here, for once.</b> The recurring fault this module keeps
    /// recording — EF scaffolding <c>defaultValue: 0</c> for a column whose real default is not zero
    /// — cannot arise: all five columns are nullable and null is the correct state for every
    /// interview that exists. An interview with no override reason was not forced, and one with no
    /// booking holds no room. Both are the ordinary case, not a missing value.</para>
    ///
    /// <para>⚠ The foreign key is <c>ON DELETE NO ACTION</c>. A cascade would mean deleting a room
    /// booking silently deleted the interviews held in it, which is the opposite of what holding a
    /// room is for. Same choice as lane H's template FK, and for the same reason.</para>
    /// </remarks>
    public partial class AddInterviewPanelClashOverrideAndRoomBooking : Migration
    {
        private const string Interviews = "JobInterviews";
        private const string RoomColumn = "RoomBookingId";
        private const string IndexName = "IX_JobInterviews_RoomBookingId";
        private const string FkName = "FK_JobInterviews_RoomBookings_RoomBookingId";
        private const string PrincipalTable = "RoomBookings";

        private static string AddColumn(string column, string definition) => $@"
IF COL_LENGTH('dbo.{Interviews}', '{column}') IS NULL
    ALTER TABLE [dbo].[{Interviews}] ADD [{column}] {definition};";

        /// <remarks>
        /// The default constraint is looked up rather than named, so this works whether the column
        /// was created by this migration or by the EF model on a rebuilt database, where the
        /// constraint name is server-generated. Guessing it would leave the column undroppable.
        /// </remarks>
        private static string DropColumn(string column) => $@"
IF COL_LENGTH('dbo.{Interviews}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{column} sysname;
    SELECT @df_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{Interviews}') AND c.name = '{column}';
    IF @df_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{Interviews}] DROP CONSTRAINT [' + @df_{column} + ']');
    ALTER TABLE [dbo].[{Interviews}] DROP COLUMN [{column}];
END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn("PanelClashOverrideReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumn("PanelClashOverrideDetail", "nvarchar(2000) NULL"));
            migrationBuilder.Sql(AddColumn("PanelClashOverriddenById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("PanelClashOverriddenAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(RoomColumn, "uniqueidentifier NULL"));

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Interviews}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{IndexName}' AND object_id = OBJECT_ID('dbo.{Interviews}'))
    CREATE INDEX [{IndexName}] ON [dbo].[{Interviews}] ([{RoomColumn}]);");

            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Interviews}', '{RoomColumn}') IS NOT NULL
   AND OBJECT_ID('dbo.{PrincipalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{FkName}' AND parent_object_id = OBJECT_ID('dbo.{Interviews}'))
    ALTER TABLE [dbo].[{Interviews}] WITH CHECK
        ADD CONSTRAINT [{FkName}] FOREIGN KEY ([{RoomColumn}])
        REFERENCES [dbo].[{PrincipalTable}] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{FkName}' AND parent_object_id = OBJECT_ID('dbo.{Interviews}'))
    ALTER TABLE [dbo].[{Interviews}] DROP CONSTRAINT [{FkName}];");

            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{IndexName}' AND object_id = OBJECT_ID('dbo.{Interviews}'))
    DROP INDEX [{IndexName}] ON [dbo].[{Interviews}];");

            migrationBuilder.Sql(DropColumn(RoomColumn));
            migrationBuilder.Sql(DropColumn("PanelClashOverriddenAt"));
            migrationBuilder.Sql(DropColumn("PanelClashOverriddenById"));
            migrationBuilder.Sql(DropColumn("PanelClashOverrideDetail"));
            migrationBuilder.Sql(DropColumn("PanelClashOverrideReason"));
        }
    }
}
