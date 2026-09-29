using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane H1: a post says which pre-employment checks an offer for it starts from.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> <c>AcceptConditionallyAsync</c> refuses an offer that has no check set —
    /// correctly, because the condition in <i>"conditionally accepted"</i> <b>is</b> the check set.
    /// But nothing ever created one, so HR had to know to open a tab, build a set by hand and add
    /// every item before a conditional acceptance would be taken at all. The refusal was honest;
    /// the absence of a set was the defect. This column is what lets an offer arrive with the right
    /// checks already on it.</para>
    ///
    /// <para><b>⚠ Nullable, and null does NOT mean "no checks".</b> A post that names no template
    /// falls back to the tenant's <b>single</b> active one — and to nothing at all where there are
    /// several. That refusal to guess is deliberate: an offer letter tells a candidate what they
    /// must produce, so seeding a set nobody chose would commit the company, in writing, to checks
    /// it never decided on, and HR would only find out after the letter was sent.</para>
    ///
    /// <para><b>The foreign key is NO ACTION</b>, the EF default for an optional reference.
    /// Retiring a check template must not cascade into the establishment; a post left pointing at a
    /// deleted template falls through to the tenant rule, which the resolver handles explicitly.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has this column, index
    /// and constraint, and a bare <c>AddColumn</c> stops the chain for everyone.</para>
    /// </remarks>
    public partial class AddPositionPreEmploymentCheckTemplate : Migration
    {
        private const string Positions = "EmployeePositions";
        private const string Column = "PreEmploymentCheckTemplateId";
        private const string IndexName = "IX_EmployeePositions_PreEmploymentCheckTemplateId";
        private const string FkName =
            "FK_EmployeePositions_PreEmploymentCheckTemplates_PreEmploymentCheckTemplateId";
        private const string PrincipalTable = "PreEmploymentCheckTemplates";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Positions}', '{Column}') IS NULL
    ALTER TABLE [dbo].[{Positions}] ADD [{Column}] uniqueidentifier NULL;");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Positions}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{IndexName}' AND object_id = OBJECT_ID('dbo.{Positions}'))
    CREATE INDEX [{IndexName}] ON [dbo].[{Positions}] ([{Column}]);");

            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Positions}', '{Column}') IS NOT NULL
   AND OBJECT_ID('dbo.{PrincipalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{FkName}' AND parent_object_id = OBJECT_ID('dbo.{Positions}'))
    ALTER TABLE [dbo].[{Positions}] WITH CHECK
        ADD CONSTRAINT [{FkName}] FOREIGN KEY ([{Column}])
        REFERENCES [dbo].[{PrincipalTable}] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{FkName}' AND parent_object_id = OBJECT_ID('dbo.{Positions}'))
    ALTER TABLE [dbo].[{Positions}] DROP CONSTRAINT [{FkName}];");

            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{IndexName}' AND object_id = OBJECT_ID('dbo.{Positions}'))
    DROP INDEX [{IndexName}] ON [dbo].[{Positions}];");

            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Positions}', '{Column}') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{Positions}') AND c.name = '{Column}';
    IF @df IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{Positions}] DROP CONSTRAINT [' + @df + ']');
    ALTER TABLE [dbo].[{Positions}] DROP COLUMN [{Column}];
END");
        }
    }
}
