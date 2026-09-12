using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 9 slice 2 — <c>MinimumAuthority</c> on the disciplinary action-type catalog.
    ///
    /// This is where FR-HR-080 (heads of department are limited to verbal warnings) and FR-HR-092
    /// (the MD signs terminations) hang: who may issue a given sanction is a policy each tenant sets
    /// in its own catalog, so the column carries it rather than the code inferring it from the
    /// action's name. It also feeds the workflow routing context, so a definition can send
    /// management-level sanctions to a different approver.
    ///
    /// The scaffolded AddColumn body is replaced with guarded SQL (repo convention): local dev DBs
    /// are built from the EF model by rebuild-db, so a DB can already carry this column without the
    /// migration being stamped — the operation checks before it acts. The generated Designer and the
    /// regenerated snapshot are kept as scaffolded.
    ///
    /// ⚠ Two things the scaffold got wrong for this column, both corrected here:
    ///
    /// The default is <b>2 (Hr)</b>, not 0. <c>DisciplinaryActionAuthority</c> starts at 1 and has no
    /// member for 0, so the scaffolded <c>defaultValue: 0</c> would have written a value that maps to
    /// no enum member onto every existing catalog row. 2 matches the entity's own default and is the
    /// restrictive choice: an unmaintained row requires HR, rather than silently becoming issuable by
    /// anyone.
    ///
    /// The default constraint is <b>named</b>. A NOT NULL column added with an anonymous default gets
    /// a system-generated constraint name, which <c>Down</c> then cannot drop deterministically — so
    /// the column could not be removed. Naming it lets Down drop the constraint before the column.
    /// </summary>
    public partial class AddDisciplineMinimumAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffDisciplinaryActionTypes]', N'MinimumAuthority') IS NULL
    ALTER TABLE [StaffDisciplinaryActionTypes] ADD [MinimumAuthority] int NOT NULL
        CONSTRAINT [DF_StaffDisciplinaryActionTypes_MinimumAuthority] DEFAULT 2;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DF_StaffDisciplinaryActionTypes_MinimumAuthority]', N'D') IS NOT NULL
    ALTER TABLE [StaffDisciplinaryActionTypes] DROP CONSTRAINT [DF_StaffDisciplinaryActionTypes_MinimumAuthority];
IF COL_LENGTH(N'[StaffDisciplinaryActionTypes]', N'MinimumAuthority') IS NOT NULL
    ALTER TABLE [StaffDisciplinaryActionTypes] DROP COLUMN [MinimumAuthority];
");
        }
    }
}
