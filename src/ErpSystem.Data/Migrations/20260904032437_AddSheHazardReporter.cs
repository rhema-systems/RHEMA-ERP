using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Gives a hazard a reporter: nullable <c>ReportedById</c> (an Employee) and
    /// <c>ReportedDate</c> on <c>SheHazards</c>. Every self-service hazard report is now stamped
    /// with the token's employee, and the SHE desk may record a hazard in the name of the person
    /// who reported it — the same shape incidents and stop-work orders already had.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b> — the same rewrite as
    /// <c>20260904005351_AddGeoAreaToLocationCompanyAndFacility</c>. <c>rebuild-db</c> builds from
    /// the EF model rather than the migration chain, so a rebuilt database already has both
    /// columns, the index and the foreign key, and a bare <c>AddColumn</c> fails on it.
    /// </para>
    /// <para>
    /// <b>No data guard.</b> Two nullable columns with no default: nothing dropped, nothing
    /// altered, no existing value read or rewritten. Every hazard recorded before this migration
    /// keeps NULL — there is no reliable source to backfill the reporter from (the row's
    /// <c>CreatedBy</c> is a user name, and until now the desk filed hazards as itself).
    /// </para>
    /// <para>
    /// <b>NO ACTION on the foreign key</b>, as the scaffold chose (Restrict): an employee who
    /// reported a hazard cannot be hard-deleted out from under it, and employee deletes are soft
    /// anyway.
    /// </para>
    /// </remarks>
    public partial class AddSheHazardReporter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each column in its OWN batch: the statements below reference these by name, and SQL
            // Server compiles a batch before running it — adding and then indexing in one batch
            // fails with "Invalid column name".
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheHazards', 'ReportedById') IS NULL
    ALTER TABLE [dbo].[SheHazards] ADD [ReportedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheHazards', 'ReportedDate') IS NULL
    ALTER TABLE [dbo].[SheHazards] ADD [ReportedDate] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheHazards', 'ReportedById') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_SheHazards_ReportedById' AND object_id = OBJECT_ID('dbo.SheHazards'))
    CREATE INDEX [IX_SheHazards_ReportedById] ON [dbo].[SheHazards] ([ReportedById]);");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheHazards', 'ReportedById') IS NOT NULL
   AND OBJECT_ID('dbo.Employees', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE name = 'FK_SheHazards_Employees_ReportedById' AND parent_object_id = OBJECT_ID('dbo.SheHazards'))
    ALTER TABLE [dbo].[SheHazards] WITH CHECK
        ADD CONSTRAINT [FK_SheHazards_Employees_ReportedById]
        FOREIGN KEY ([ReportedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible: what is lost is who reported each hazard recorded after the migration.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_SheHazards_Employees_ReportedById' AND parent_object_id = OBJECT_ID('dbo.SheHazards'))
    ALTER TABLE [dbo].[SheHazards] DROP CONSTRAINT [FK_SheHazards_Employees_ReportedById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_SheHazards_ReportedById' AND object_id = OBJECT_ID('dbo.SheHazards'))
    DROP INDEX [IX_SheHazards_ReportedById] ON [dbo].[SheHazards];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheHazards', 'ReportedById') IS NOT NULL
    ALTER TABLE [dbo].[SheHazards] DROP COLUMN [ReportedById];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheHazards', 'ReportedDate') IS NOT NULL
    ALTER TABLE [dbo].[SheHazards] DROP COLUMN [ReportedDate];");
        }
    }
}
