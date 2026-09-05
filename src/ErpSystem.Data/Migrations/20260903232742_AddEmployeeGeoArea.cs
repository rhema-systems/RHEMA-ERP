using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Puts the employee address on the administrative-geography tree: one nullable
    /// <c>Employees.GeoAreaId</c> pointing at the deepest tier known.
    /// Phase 2 of <c>docs/GEOGRAPHY-REFERENCE-DESIGN.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One column, not one per tier.</b> Not <c>RegionId</c> + <c>DistrictId</c> + <c>TownId</c>:
    /// the ancestors come from <c>GeoArea.Path</c>, so Ghana's scheme can gain a fifth tier, or a
    /// second country arrive with a different depth, without ever migrating this table again. The
    /// three-column shape would need a migration here the first time either happened.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, the same rewrite as
    /// <c>20260903223232_AddAdministrativeGeography</c> and <c>20260901001749_AddEmployeeDocuments</c>.
    /// <c>rebuild-db</c> builds from the EF model rather than the migration chain, so a database
    /// rebuilt after the entity change already has the column, both indexes and the foreign key,
    /// and a bare <c>AddColumn</c> fails on it. Every step is guarded, so this is a no-op against a
    /// database already in the target shape and still does the work on one that is not.
    /// </para>
    /// <para>
    /// <b>No data guard, and here is why it is safe not to have one</b> — <c>Employees</c> is
    /// populated on every real database, so the question is fair. The column is <b>nullable with no
    /// default</b>: nothing is dropped, nothing is altered, no existing value is read or rewritten,
    /// and every existing row simply gets NULL. That is the whole change. The halt guards the
    /// destructive HR migrations carry protect against redesigned tables and required columns
    /// added over data that cannot satisfy them; neither applies.
    /// </para>
    /// <para>
    /// <b>⚠ This does NOT backfill from <c>State</c> / <c>City</c>, on purpose.</b> Matching the
    /// free text an employee already carries to an area means scoping the name to its parent and
    /// falling back to the alias table — "Tema" is a town under one district and a metropolis under
    /// another, and "Brong Ahafo" resolves only through an alias to a region that no longer exists.
    /// SQL here cannot do that safely, and a wrong match is worse than a null because it looks
    /// answered. The backfill is phase 3, through the geography service's resolver, with the
    /// unmatched rows reported rather than guessed.
    /// </para>
    /// <para>
    /// <b>⚠ <c>NO ACTION</c>, not <c>SET NULL</c>.</b> Deleting an area out from under the people
    /// who live in it must fail loudly. The service refuses it already with a message pointing at
    /// end-dating; this is the backstop for anything that bypasses the service.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeGeoArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Its own batch: the statements below reference this column by name, and SQL Server
            // compiles a batch before running it — adding and then indexing in one batch fails with
            // "Invalid column name".
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'GeoAreaId') IS NULL
    ALTER TABLE [dbo].[Employees] ADD [GeoAreaId] uniqueidentifier NULL;");

            // The reporting index this column was added for: "everyone in the Ashanti Region".
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'GeoAreaId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_Employee_Tenant_GeoArea'
                     AND object_id = OBJECT_ID('dbo.Employees'))
    CREATE INDEX [IX_Employee_Tenant_GeoArea] ON [dbo].[Employees] ([TenantId], [GeoAreaId]);");

            // Kept alongside the composite even though the composite serves most reads: this is the
            // index the foreign key's own delete-check uses, and it is what the EF model declares.
            // Dropping it here would make the next scaffold produce a spurious diff.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'GeoAreaId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_Employees_GeoAreaId'
                     AND object_id = OBJECT_ID('dbo.Employees'))
    CREATE INDEX [IX_Employees_GeoAreaId] ON [dbo].[Employees] ([GeoAreaId]);");

            // Guarded on GeoAreas existing too, so running this out of order reports nothing rather
            // than half-applying.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'GeoAreaId') IS NOT NULL
   AND OBJECT_ID('dbo.GeoAreas', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE name = 'FK_Employees_GeoAreas_GeoAreaId'
                     AND parent_object_id = OBJECT_ID('dbo.Employees'))
    ALTER TABLE [dbo].[Employees] WITH CHECK
        ADD CONSTRAINT [FK_Employees_GeoAreas_GeoAreaId]
        FOREIGN KEY ([GeoAreaId]) REFERENCES [dbo].[GeoAreas] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible without loss of anything a person typed: State and City still hold the
            // address, because this phase never stopped writing them. What is lost is which area
            // each employee was linked to.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_Employees_GeoAreas_GeoAreaId'
             AND parent_object_id = OBJECT_ID('dbo.Employees'))
    ALTER TABLE [dbo].[Employees] DROP CONSTRAINT [FK_Employees_GeoAreas_GeoAreaId];

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_Employee_Tenant_GeoArea' AND object_id = OBJECT_ID('dbo.Employees'))
    DROP INDEX [IX_Employee_Tenant_GeoArea] ON [dbo].[Employees];

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_Employees_GeoAreaId' AND object_id = OBJECT_ID('dbo.Employees'))
    DROP INDEX [IX_Employees_GeoAreaId] ON [dbo].[Employees];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'GeoAreaId') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [GeoAreaId];");
        }
    }
}
