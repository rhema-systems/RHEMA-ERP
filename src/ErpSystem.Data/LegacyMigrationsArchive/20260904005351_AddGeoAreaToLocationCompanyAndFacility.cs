using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Puts the company's own places on the administrative-geography tree: one nullable
    /// <c>GeoAreaId</c> on <c>Locations</c> (sites), <c>CompanyProfiles</c> (the registered
    /// address) and <c>HealthcareFacilities</c> (hospitals and clinics).
    /// Phase 4 of <c>docs/GEOGRAPHY-REFERENCE-DESIGN.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why these three and not everything with a City column.</b> A <c>GeoAreaId</c> belongs on a
    /// record whose address is a property of a PLACE. It does not belong on one that merely mentions
    /// a city — a hotel booking, a per-diem rate, a travel alert — because those cities are usually
    /// foreign ones the scheme cannot hold, and keying a per-diem rate by area is a rate-model
    /// change rather than an address. Columns that would stay permanently null are a false promise
    /// of coverage.
    /// </para>
    /// <para>
    /// <b>⚠ Each column needs an <c>IGeoAreaConsumer</c> probe, and the database cannot supply it.</b>
    /// Geography deletes are SOFT, so these foreign keys are never consulted: without a probe an
    /// area someone still uses deletes cleanly, vanishes from every read, and takes the address with
    /// it. All three probes are registered in <c>GeoAreaConsumers.cs</c>. A future consumer that
    /// adds the column and forgets the probe gets no protection at all.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b> — the same rewrite as
    /// <c>20260903232742_AddEmployeeGeoArea</c>. <c>rebuild-db</c> builds from the EF model rather
    /// than the migration chain, so a rebuilt database already has all three columns, both indexes
    /// and the foreign keys, and a bare <c>AddColumn</c> fails on it.
    /// </para>
    /// <para>
    /// <b>No data guard.</b> Three nullable columns with no default: nothing dropped, nothing
    /// altered, no existing value read or rewritten. Every existing row gets NULL. And no backfill
    /// from the existing <c>City</c> text — matching a name to an area needs parent scoping and the
    /// alias table, and a wrong match is worse than a null because it looks answered.
    /// </para>
    /// </remarks>
    public partial class AddGeoAreaToLocationCompanyAndFacility : Migration
    {
        private static readonly string[] Tables =
        {
            "Locations", "CompanyProfiles", "HealthcareFacilities",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each column in its OWN batch: the statements below reference these by name, and SQL
            // Server compiles a batch before running it — adding and then indexing in one batch
            // fails with "Invalid column name".
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'GeoAreaId') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [GeoAreaId] uniqueidentifier NULL;");
            }

            // The composite is the reporting index — "every site in the Ashanti Region". The
            // single-column ones are what each foreign key's own delete-check uses, and are what
            // the EF model declares; dropping them would make the next scaffold produce a diff.
            // CompanyProfiles gets no composite: there is at most one row per tenant.
            foreach (var (name, table, columns) in new[]
            {
                ("IX_Location_Tenant_GeoArea", "Locations", "[TenantId], [GeoAreaId]"),
                ("IX_Locations_GeoAreaId", "Locations", "[GeoAreaId]"),
                ("IX_HealthcareFacility_Tenant_GeoArea", "HealthcareFacilities", "[TenantId], [GeoAreaId]"),
                ("IX_HealthcareFacilities_GeoAreaId", "HealthcareFacilities", "[GeoAreaId]"),
                ("IX_CompanyProfiles_GeoAreaId", "CompanyProfiles", "[GeoAreaId]"),
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'GeoAreaId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ({columns});");
            }

            // ⚠ NO ACTION on all three. Deleting an area out from under the places that stand in it
            // must fail loudly; the service refuses it first with a message that points at
            // end-dating, and this is the backstop for anything bypassing the service.
            foreach (var (name, table) in new[]
            {
                ("FK_Locations_GeoAreas_GeoAreaId", "Locations"),
                ("FK_CompanyProfiles_GeoAreas_GeoAreaId", "CompanyProfiles"),
                ("FK_HealthcareFacilities_GeoAreas_GeoAreaId", "HealthcareFacilities"),
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'GeoAreaId') IS NOT NULL
   AND OBJECT_ID('dbo.GeoAreas', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE name = '{name}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{name}]
        FOREIGN KEY ([GeoAreaId]) REFERENCES [dbo].[GeoAreas] ([Id]) ON DELETE NO ACTION;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible without losing anything a person typed: City and Region still hold each
            // address, because this phase never stopped writing them. What is lost is which area
            // each place stood in.
            foreach (var (name, table) in new[]
            {
                ("FK_Locations_GeoAreas_GeoAreaId", "Locations"),
                ("FK_CompanyProfiles_GeoAreas_GeoAreaId", "CompanyProfiles"),
                ("FK_HealthcareFacilities_GeoAreas_GeoAreaId", "HealthcareFacilities"),
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = '{name}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{name}];");
            }

            foreach (var (name, table) in new[]
            {
                ("IX_Location_Tenant_GeoArea", "Locations"),
                ("IX_Locations_GeoAreaId", "Locations"),
                ("IX_HealthcareFacility_Tenant_GeoArea", "HealthcareFacilities"),
                ("IX_HealthcareFacilities_GeoAreaId", "HealthcareFacilities"),
                ("IX_CompanyProfiles_GeoAreaId", "CompanyProfiles"),
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{name}] ON [dbo].[{table}];");
            }

            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'GeoAreaId') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [GeoAreaId];");
            }
        }
    }
}
