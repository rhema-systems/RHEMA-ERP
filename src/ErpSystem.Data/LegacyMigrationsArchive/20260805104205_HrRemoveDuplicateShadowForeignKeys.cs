using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Removes five shadow foreign-key columns EF had been generating for HR relationships, and
    /// relaxes the accidental unique index on <c>ProbationPeriods.EmployeeId</c>.
    /// </summary>
    /// <remarks>
    /// <para>Each shadow column came from the same mistake: a navigation with no inverse to pair with,
    /// so EF quietly built a <em>second</em> relationship next to the real one and had to invent a
    /// column for it. <c>Employee.CurrentTerms</c>, <c>Employee.Probation</c> and
    /// <c>Employee.OnboardingPlan</c> were reference navigations whose one-to-many was already owned by
    /// <c>ContractDetails</c> / <c>ProbationPeriod.Employee</c> / <c>OnboardingPlan.Employee</c>, and
    /// <c>JobDescription.Competencies</c> / <c>.Qualifications</c> were collections left unpaired by a
    /// <c>HasOne(x =&gt; x.JobDescription).WithMany()</c> that should have named them. The result was
    /// <c>EmployeeId1</c> on three tables and <c>JobDescriptionId1</c> on two, each duplicating a column
    /// the table already carried.</para>
    ///
    /// <para>No data is lost. The shadow columns were unreachable: nothing in the codebase read or
    /// assigned any of the five navigations, and a write through the two <c>JobDescription</c>
    /// collections would have failed its foreign key anyway, since the required <c>JobDescriptionId</c>
    /// would have been left empty. The fix is in the model — the navigations are now
    /// <c>[NotMapped]</c> accessors over properly paired collections — so this migration only clears the
    /// columns away.</para>
    ///
    /// <para>The unique index is a separate, real bug that the same tangle produced.
    /// <c>20260720181131_AddHRModule</c> shipped <c>IX_ProbationPeriod_EmployeeId</c> as
    /// <c>unique: true</c>, which contradicts <c>ProbationPeriod</c>'s own documentation — "a new
    /// probation record is created if employment terms are restarted (e.g. after rehire or contract
    /// renewal with a probation clause)". Any employee's second probation period would have hit a
    /// unique-constraint violation. It is recreated here as a plain non-unique index; the equivalent
    /// indexes on <c>OnboardingPlans</c> and <c>EmployeeContractDetails</c> were already non-unique and
    /// are untouched.</para>
    ///
    /// <para>Every statement is guarded so the migration is safe both on databases built by the
    /// migration chain (columns present) and on databases built by <c>rebuild-db</c>/EnsureCreated from
    /// the current model (columns never created), matching the defensive style of the other HR
    /// migrations.</para>
    /// </remarks>
    public partial class HrRemoveDuplicateShadowForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropForeignKeyIfExists(migrationBuilder, "EmployeeContractDetails", "FK_EmployeeContractDetails_Employees_EmployeeId1");
            DropForeignKeyIfExists(migrationBuilder, "OnboardingPlans", "FK_OnboardingPlans_Employees_EmployeeId1");
            DropForeignKeyIfExists(migrationBuilder, "ProbationPeriods", "FK_ProbationPeriods_Employees_EmployeeId1");
            DropForeignKeyIfExists(migrationBuilder, "JobCompetencies", "FK_JobCompetencies_JobDescriptions_JobDescriptionId1");
            DropForeignKeyIfExists(migrationBuilder, "JobQualifications", "FK_JobQualifications_JobDescriptions_JobDescriptionId1");

            DropIndexIfExists(migrationBuilder, "EmployeeContractDetails", "IX_EmployeeContractDetails_EmployeeId1");
            DropIndexIfExists(migrationBuilder, "OnboardingPlans", "IX_OnboardingPlans_EmployeeId1");
            DropIndexIfExists(migrationBuilder, "ProbationPeriods", "IX_ProbationPeriods_EmployeeId1");
            DropIndexIfExists(migrationBuilder, "JobCompetencies", "IX_JobCompetencies_JobDescriptionId1");
            DropIndexIfExists(migrationBuilder, "JobQualifications", "IX_JobQualifications_JobDescriptionId1");

            DropColumnIfExists(migrationBuilder, "EmployeeContractDetails", "EmployeeId1");
            DropColumnIfExists(migrationBuilder, "OnboardingPlans", "EmployeeId1");
            DropColumnIfExists(migrationBuilder, "ProbationPeriods", "EmployeeId1");
            DropColumnIfExists(migrationBuilder, "JobCompetencies", "JobDescriptionId1");
            DropColumnIfExists(migrationBuilder, "JobQualifications", "JobDescriptionId1");

            // Rebuild as non-unique: an employee may hold several probation periods over a career.
            DropIndexIfExists(migrationBuilder, "ProbationPeriods", "IX_ProbationPeriod_EmployeeId");
            CreateIndexIfMissing(migrationBuilder, "ProbationPeriods", "IX_ProbationPeriod_EmployeeId", "[EmployeeId]", unique: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "ProbationPeriods", "IX_ProbationPeriod_EmployeeId");
            CreateIndexIfMissing(migrationBuilder, "ProbationPeriods", "IX_ProbationPeriod_EmployeeId", "[EmployeeId]", unique: true);

            AddNullableGuidColumnIfMissing(migrationBuilder, "EmployeeContractDetails", "EmployeeId1");
            AddNullableGuidColumnIfMissing(migrationBuilder, "OnboardingPlans", "EmployeeId1");
            AddNullableGuidColumnIfMissing(migrationBuilder, "ProbationPeriods", "EmployeeId1");
            AddNullableGuidColumnIfMissing(migrationBuilder, "JobCompetencies", "JobDescriptionId1");
            AddNullableGuidColumnIfMissing(migrationBuilder, "JobQualifications", "JobDescriptionId1");

            CreateIndexIfMissing(migrationBuilder, "EmployeeContractDetails", "IX_EmployeeContractDetails_EmployeeId1", "[EmployeeId1]", unique: true, filter: "[EmployeeId1] IS NOT NULL");
            CreateIndexIfMissing(migrationBuilder, "OnboardingPlans", "IX_OnboardingPlans_EmployeeId1", "[EmployeeId1]", unique: true, filter: "[EmployeeId1] IS NOT NULL");
            CreateIndexIfMissing(migrationBuilder, "ProbationPeriods", "IX_ProbationPeriods_EmployeeId1", "[EmployeeId1]", unique: true, filter: "[EmployeeId1] IS NOT NULL");
            CreateIndexIfMissing(migrationBuilder, "JobCompetencies", "IX_JobCompetencies_JobDescriptionId1", "[JobDescriptionId1]", unique: false);
            CreateIndexIfMissing(migrationBuilder, "JobQualifications", "IX_JobQualifications_JobDescriptionId1", "[JobDescriptionId1]", unique: false);

            AddForeignKeyIfMissing(migrationBuilder, "EmployeeContractDetails", "FK_EmployeeContractDetails_Employees_EmployeeId1", "EmployeeId1", "Employees");
            AddForeignKeyIfMissing(migrationBuilder, "OnboardingPlans", "FK_OnboardingPlans_Employees_EmployeeId1", "EmployeeId1", "Employees");
            AddForeignKeyIfMissing(migrationBuilder, "ProbationPeriods", "FK_ProbationPeriods_Employees_EmployeeId1", "EmployeeId1", "Employees");
            AddForeignKeyIfMissing(migrationBuilder, "JobCompetencies", "FK_JobCompetencies_JobDescriptions_JobDescriptionId1", "JobDescriptionId1", "JobDescriptions");
            AddForeignKeyIfMissing(migrationBuilder, "JobQualifications", "FK_JobQualifications_JobDescriptions_JobDescriptionId1", "JobDescriptionId1", "JobDescriptions");
        }

        private static void DropForeignKeyIfExists(MigrationBuilder migrationBuilder, string table, string foreignKey)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{foreignKey}]', N'F') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{foreignKey}];");
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string index)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' AND object_id = OBJECT_ID(N'[dbo].[{table}]'))
    DROP INDEX [{index}] ON [dbo].[{table}];");
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];");
        }

        private static void AddNullableGuidColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] uniqueidentifier NULL;");
        }

        private static void CreateIndexIfMissing(
            MigrationBuilder migrationBuilder,
            string table,
            string index,
            string columns,
            bool unique,
            string filter = null)
        {
            var uniqueClause = unique ? "UNIQUE " : string.Empty;
            var whereClause = filter is null ? string.Empty : $" WHERE {filter}";

            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' AND object_id = OBJECT_ID(N'[dbo].[{table}]'))
    CREATE {uniqueClause}INDEX [{index}] ON [dbo].[{table}] ({columns}){whereClause};");
        }

        private static void AddForeignKeyIfMissing(
            MigrationBuilder migrationBuilder,
            string table,
            string foreignKey,
            string column,
            string principalTable)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{foreignKey}]', N'F') IS NULL
   AND COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{foreignKey}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]);");
        }
    }
}
