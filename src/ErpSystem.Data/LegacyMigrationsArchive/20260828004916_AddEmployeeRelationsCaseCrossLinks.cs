using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 9 — an employee-relations case can be cross-referenced to the SHE incident,
    /// performance improvement plan or disciplinary case it arose from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three real foreign keys, not a polymorphic link table.</b> A (type-name, id) pair cannot
    /// be enforced by the database, so it rots the first time a source row is deleted and nothing
    /// complains. A case has at most one origin of each kind, so a link table would buy nothing but
    /// a join — and would give up the one thing worth having here, which is the database refusing a
    /// dangling reference.
    /// </para>
    /// <para>
    /// <b>⚠ NO ACTION on all three, and it is the point rather than a default.</b> The delete
    /// behaviour is what makes the link read-only in both directions: retiring an incident must not
    /// delete the employee-relations case that referenced it, and it must not silently blank the
    /// reference either. Deleting a linked source is refused at the database — the only place that
    /// refusal cannot be forgotten by a future service method.
    /// </para>
    /// <para>
    /// <b>The indexes are FILTERED to non-null.</b> Almost every case links to nothing, so an
    /// unfiltered index would be three index rows per case to serve a lookup — "which cases point at
    /// this incident" — that only ever asks for the rows that DO link.
    /// </para>
    /// <para>
    /// <b>Guarded throughout</b>, per the module convention: <c>rebuild-db</c> builds from the EF
    /// model rather than from the migration chain, so every statement here has to be safe to re-run
    /// against a database that already has the columns.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeRelationsCaseCrossLinks : Migration
    {
        /// <summary>(column, principal table, foreign-key name, index name) for the three links.</summary>
        private static readonly (string Column, string PrincipalTable, string ForeignKey, string Index)[] Links =
        {
            ("SafetyIncidentId", "SafetyIncidents",
                "FK_StaffGrievances_SafetyIncidents_SafetyIncidentId",
                "IX_StaffGrievances_SafetyIncidentId"),
            ("PerformanceImprovementPlanId", "PerformanceImprovementPlans",
                "FK_StaffGrievances_PerformanceImprovementPlans_PerformanceImprovementPlanId",
                "IX_StaffGrievances_PerformanceImprovementPlanId"),
            ("StaffDisciplinaryActionId", "StaffDisciplinaryActions",
                "FK_StaffGrievances_StaffDisciplinaryActions_StaffDisciplinaryActionId",
                "IX_StaffGrievances_StaffDisciplinaryActionId"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (column, principalTable, foreignKey, index) in Links)
            {
                // Nullable with no default: a case that arose from nothing links to nothing, and
                // every one of the 87 rows that predates this slice is correct as NULL.
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.StaffGrievances', '{column}') IS NULL
    ALTER TABLE [dbo].[StaffGrievances] ADD [{column}] uniqueidentifier NULL;");

                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.StaffGrievances'))
    CREATE INDEX [{index}] ON [dbo].[StaffGrievances] ([{column}]) WHERE [{column}] IS NOT NULL;");

                // ⚠ NO ACTION, not CASCADE and not SET NULL. See the remarks.
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{foreignKey}' AND parent_object_id = OBJECT_ID('dbo.StaffGrievances'))
   AND OBJECT_ID('dbo.{principalTable}') IS NOT NULL
    ALTER TABLE [dbo].[StaffGrievances]
        ADD CONSTRAINT [{foreignKey}] FOREIGN KEY ([{column}])
        REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE NO ACTION;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the columns discards which record each case arose from. There is nowhere
            // else that fact is written down, so this is a real loss rather than a rollback of a
            // derived value.
            foreach (var (column, _, foreignKey, index) in Links)
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{foreignKey}' AND parent_object_id = OBJECT_ID('dbo.StaffGrievances'))
    ALTER TABLE [dbo].[StaffGrievances] DROP CONSTRAINT [{foreignKey}];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.StaffGrievances'))
    DROP INDEX [{index}] ON [dbo].[StaffGrievances];

IF COL_LENGTH('dbo.StaffGrievances', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[StaffGrievances] DROP COLUMN [{column}];");
            }
        }
    }
}
