using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane G: the tenant's salary-structure policy — how many tiers the scale has, and who
    /// maintains it (Payroll, mirrored into HR; or HR itself).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as for every migration in this
    /// round: <c>rebuild-db</c> builds from the EF model, so a rebuilt database already has the
    /// columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ EF scaffolded <c>defaultValue: 0</c> for BOTH columns, and neither enum has a zero
    /// member</b> — the third time this session (<c>IsActive</c> in D1, <c>PayBasis</c> in E1).
    /// It reads the CLR default of a non-nullable int, not the property initialiser. Left as
    /// scaffolded, every existing settings row would have carried a tier count and a source that
    /// nothing names, and the first read would have deserialised an enum value with no meaning.
    /// The defaults below are the entity's own: <c>2</c> = two-tier (grade and notch), <c>1</c> =
    /// Payroll — which is what every existing tenant was implicitly, since payroll's structure is
    /// two-tier and HR has mirrored it since lane 3a.
    /// </para>
    ///
    /// <para>
    /// The scaffold also emitted an <c>UpdateData</c> for the model-seeded settings row (fixed id
    /// <c>b2c3d4e5-…-0001</c>), because that seed now states the two properties. With the column
    /// defaults above it is redundant; it is kept as a guarded repair over <i>every</i> row instead
    /// — any settings row that somehow holds a zero in either column is set to the default, which
    /// covers the seeded row and anything else alike.
    /// </para>
    /// </remarks>
    public partial class AddSalaryStructurePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SalaryStructureTiers') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings]
        ADD [SalaryStructureTiers] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_SalaryStructureTiers] DEFAULT (2);");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SalaryStructureSource') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings]
        ADD [SalaryStructureSource] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_SalaryStructureSource] DEFAULT (1);");

            // A zero in either column is a value no enum member names. Only a database whose column
            // arrived without the defaults above could hold one; repaired rather than assumed away.
            migrationBuilder.Sql(@"
UPDATE [dbo].[CompanyHrPolicySettings] SET [SalaryStructureTiers] = 2 WHERE [SalaryStructureTiers] = 0;
UPDATE [dbo].[CompanyHrPolicySettings] SET [SalaryStructureSource] = 1 WHERE [SalaryStructureSource] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CompanyHrPolicySettings_SalaryStructureSource')
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP CONSTRAINT [DF_CompanyHrPolicySettings_SalaryStructureSource];
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SalaryStructureSource') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [SalaryStructureSource];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CompanyHrPolicySettings_SalaryStructureTiers')
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP CONSTRAINT [DF_CompanyHrPolicySettings_SalaryStructureTiers];
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SalaryStructureTiers') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [SalaryStructureTiers];");
        }
    }
}
