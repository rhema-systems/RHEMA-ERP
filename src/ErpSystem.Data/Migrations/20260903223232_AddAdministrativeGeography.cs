using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Administrative geography as shared reference data: a per-country division scheme
    /// (<c>GeoSchemes</c>), its tiers (<c>GeoLevels</c>), the areas themselves (<c>GeoAreas</c>) and
    /// the alternate names they answer to (<c>GeoAreaAliases</c>).
    /// Phase 1 of <c>docs/GEOGRAPHY-REFERENCE-DESIGN.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The same idea was spelled four different ways as free text and shared by nobody —
    /// <c>Employee.State</c>, <c>EstateManagedAsset.Region/District/Town</c>,
    /// <c>CompanyProfile.Region</c>, plus <c>State</c> on Sales, Procurement and Inventory. None of
    /// them could answer "how many staff live in the Tema Metropolitan area", survive a spelling
    /// variant, or survive a boundary change.
    /// </para>
    /// <para>
    /// <b>⚠ Not the Location tree.</b> <c>Locations</c> answers "which of OUR sites?" and is the
    /// target of roughly thirty foreign keys — incident sites, asset custody, attendance devices,
    /// company schedules, geofence zones. <c>GeoAreas</c> answers "where is this on the map of the
    /// country?", which is true whether or not the company operates there. They are separate tables
    /// on purpose; seeding regions into <c>Locations</c> would put "Greater Accra Region" in the
    /// incident-site picker.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, the same rewrite as
    /// <c>20260901001749_AddEmployeeDocuments</c> and <c>20260827223812_AddEmployeeRelationsConcerns</c>.
    /// <c>rebuild-db</c> builds from the EF model rather than the migration chain, so a database
    /// rebuilt after the entity change already has all four tables and a bare <c>CreateTable</c>
    /// fails. Every step is guarded, so this is a no-op against a database already in the target
    /// shape and still does the work on one that is not.
    /// </para>
    /// <para>
    /// <b>No data operation, and nothing to halt-guard.</b> This migration is purely additive — four
    /// new tables, no column altered, no table redesigned, no foreign key retargeted — so the
    /// data-safety guards the destructive HR migrations carry have nothing to protect here. The
    /// Ghana scheme is loaded by a seeder keyed on official GSS codes, not by <c>HasData</c>: the
    /// area list is a tenant's editable data, and a migration seed would re-assert rows a tenant had
    /// deliberately pruned.
    /// </para>
    /// <para>
    /// <b>⚠ All four unique indexes are FILTERED on <c>IsDeleted = 0</c>.</b> A soft delete does not
    /// release a unique index — this codebase has met that repeatedly — so an unfiltered index would
    /// make a removed region permanently un-re-addable under its own official code.
    /// </para>
    /// <para>
    /// <b>⚠ <c>SupersededByGeoAreaId</c> is a second self-reference and is deliberate.</b> A boundary
    /// change is never a rename: the old area is end-dated and points at its successor, so a record
    /// created in 2018 still resolves to the region that existed in 2018. Both self-references are
    /// NO ACTION, which is what stops a delete quietly cutting a branch or a succession trail.
    /// </para>
    /// </remarks>
    public partial class AddAdministrativeGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.GeoSchemes', 'U') IS NULL
CREATE TABLE [dbo].[GeoSchemes] (
    [Id]               uniqueidentifier NOT NULL,
    -- The country this scheme divides. Restrict, not Cascade: deleting a country must not silently
    -- take its whole division scheme, and every address that resolved through it, with it.
    [CountryId]        uniqueidentifier NOT NULL,
    [Name]             nvarchar(150)    NOT NULL,
    [Code]             nvarchar(50)     NOT NULL,
    [Description]      nvarchar(1000)   NULL,
    -- The scheme an address form reaches for when a country is chosen. At most one per country per
    -- tenant; the service unsets the previous holder rather than refusing the change.
    [IsDefault]        bit              NOT NULL,
    [IsActive]         bit              NOT NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_GeoSchemes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GeoSchemes_Countries_CountryId] FOREIGN KEY ([CountryId])
        REFERENCES [dbo].[Countries] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeoSchemes_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.GeoLevels', 'U') IS NULL
CREATE TABLE [dbo].[GeoLevels] (
    [Id]                      uniqueidentifier NOT NULL,
    [SchemeId]                uniqueidentifier NOT NULL,
    -- ⚠ This is the LABEL the address form prints, not just a description. It is the whole
    -- mechanism by which one React component serves every country: Region / District / Town in
    -- Ghana, State / LGA / Ward in Nigeria, with no difference in frontend code.
    [Name]                    nvarchar(100)    NOT NULL,
    [Code]                    nvarchar(50)     NOT NULL,
    [Description]             nvarchar(1000)   NULL,
    -- 1 = broadest. Mirrors LocationLevel.LevelNumber and OrganizationLevel.LevelNumber so the
    -- three hierarchies in this codebase are read the same way.
    [LevelNumber]             int              NOT NULL,
    [IsRequiredInAddress]     bit              NOT NULL,
    -- False for a tier that exists only to group — one that is never itself an answer to
    -- ""where do you live?"".
    [AllowsAddressAssignment] bit              NOT NULL,
    [IsActive]                bit              NOT NULL,
    [CreatedAt]               datetime2        NOT NULL,
    [UpdatedAt]               datetime2        NULL,
    [CreatedBy]               nvarchar(max)    NULL,
    [UpdatedBy]               nvarchar(max)    NULL,
    [CreatedById]             uniqueidentifier NULL,
    [LastModifiedById]        uniqueidentifier NULL,
    [IsDeleted]               bit              NOT NULL,
    [DeletedAt]               datetime2        NULL,
    [DeletedBy]               nvarchar(max)    NULL,
    [TenantId]                uniqueidentifier NOT NULL,
    CONSTRAINT [PK_GeoLevels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GeoLevels_GeoSchemes_SchemeId] FOREIGN KEY ([SchemeId])
        REFERENCES [dbo].[GeoSchemes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeoLevels_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.GeoAreas', 'U') IS NULL
CREATE TABLE [dbo].[GeoAreas] (
    [Id]                      uniqueidentifier NOT NULL,
    [SchemeId]                uniqueidentifier NOT NULL,
    [GeoLevelId]              uniqueidentifier NOT NULL,
    -- Null only at the broadest tier of the scheme.
    [ParentAreaId]            uniqueidentifier NULL,
    [Name]                    nvarchar(200)    NOT NULL,
    -- ⚠ The OFFICIAL statutory code — a Ghana Statistical Service district code, an ISO 3166-2
    -- subdivision code — never an invented one. It is what makes re-running the seeder a no-op
    -- instead of a duplicate; invent a code and the next seed creates a second Tema Metropolitan.
    [Code]                    nvarchar(50)     NOT NULL,
    -- Materialised ancestor path, matching the Location.Path convention. Roll-up to any tier reads
    -- this, which is why consumers need only ONE GeoAreaId rather than a column per tier.
    [Path]                    nvarchar(1000)   NOT NULL,
    [Latitude]                float            NULL,
    [Longitude]               float            NULL,
    -- Same storage shape as GeofenceZone.PolygonCoordinatesJson, so the Leaflet picker built for
    -- geofencing reads it without translation. Expected to stay NULL until someone has shapefiles.
    [PolygonCoordinatesJson]  nvarchar(max)    NULL,
    [EffectiveFrom]           date             NULL,
    -- ⚠ Non-null means the area is historical: it must still resolve for records that reference it,
    -- but it is never offered for new ones. Ghana went from 10 regions to 16 in 2019 and districts
    -- split most election cycles — end-dating is how that is recorded, never a rename.
    [EffectiveTo]             date             NULL,
    [SupersededByGeoAreaId]   uniqueidentifier NULL,
    [IsActive]                bit              NOT NULL,
    [Notes]                   nvarchar(1000)   NULL,
    [CreatedAt]               datetime2        NOT NULL,
    [UpdatedAt]               datetime2        NULL,
    [CreatedBy]               nvarchar(max)    NULL,
    [UpdatedBy]               nvarchar(max)    NULL,
    [CreatedById]             uniqueidentifier NULL,
    [LastModifiedById]        uniqueidentifier NULL,
    [IsDeleted]               bit              NOT NULL,
    [DeletedAt]               datetime2        NULL,
    [DeletedBy]               nvarchar(max)    NULL,
    [TenantId]                uniqueidentifier NOT NULL,
    CONSTRAINT [PK_GeoAreas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GeoAreas_GeoAreas_ParentAreaId] FOREIGN KEY ([ParentAreaId])
        REFERENCES [dbo].[GeoAreas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeoAreas_GeoAreas_SupersededByGeoAreaId] FOREIGN KEY ([SupersededByGeoAreaId])
        REFERENCES [dbo].[GeoAreas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeoAreas_GeoLevels_GeoLevelId] FOREIGN KEY ([GeoLevelId])
        REFERENCES [dbo].[GeoLevels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeoAreas_GeoSchemes_SchemeId] FOREIGN KEY ([SchemeId])
        REFERENCES [dbo].[GeoSchemes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeoAreas_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.GeoAreaAliases', 'U') IS NULL
CREATE TABLE [dbo].[GeoAreaAliases] (
    [Id]               uniqueidentifier NOT NULL,
    [GeoAreaId]        uniqueidentifier NOT NULL,
    [Alias]            nvarchar(200)    NOT NULL,
    -- GeoAreaAliasKind: 0 FormerName, 1 Spelling, 2 Abbreviation, 3 Vernacular, 99 Other.
    [Kind]             int              NOT NULL,
    [Notes]            nvarchar(500)    NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_GeoAreaAliases] PRIMARY KEY ([Id]),
    -- The one CASCADE in this migration, and it is safe: an alias is meaningless without its area.
    CONSTRAINT [FK_GeoAreaAliases_GeoAreas_GeoAreaId] FOREIGN KEY ([GeoAreaId])
        REFERENCES [dbo].[GeoAreas] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GeoAreaAliases_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            foreach (var (name, table, columns, unique, filter) in new (string, string, string, bool, string)[]
            {
                // ── GeoSchemes ──────────────────────────────────────────────────────────────
                ("IX_GeoScheme_Tenant_Code", "GeoSchemes", "[TenantId], [Code]", true, "[IsDeleted] = 0"),
                ("IX_GeoScheme_Tenant_Country", "GeoSchemes", "[TenantId], [CountryId]", false, null),
                ("IX_GeoSchemes_CountryId", "GeoSchemes", "[CountryId]", false, null),

                // ── GeoLevels ───────────────────────────────────────────────────────────────
                // Two tiers cannot share a depth: the depth is what tells the address form which
                // dropdown comes first.
                ("IX_GeoLevel_Tenant_Scheme_LevelNum", "GeoLevels",
                    "[TenantId], [SchemeId], [LevelNumber]", true, "[IsDeleted] = 0"),
                ("IX_GeoLevel_Tenant_Scheme_Code", "GeoLevels",
                    "[TenantId], [SchemeId], [Code]", true, "[IsDeleted] = 0"),
                ("IX_GeoLevels_SchemeId", "GeoLevels", "[SchemeId]", false, null),

                // ── GeoAreas ────────────────────────────────────────────────────────────────
                // Unique per SCHEME, not per level — the seeder's idempotency key has to be
                // scheme-wide or two tiers sharing a code would break re-seeding.
                ("IX_GeoArea_Tenant_Scheme_Code", "GeoAreas",
                    "[TenantId], [SchemeId], [Code]", true, "[IsDeleted] = 0"),
                // The address widget's cascade query: children of this parent, at this tier, active.
                ("IX_GeoArea_Tenant_Parent_Active", "GeoAreas",
                    "[TenantId], [ParentAreaId], [IsActive]", false, null),
                // How the import resolver and the backfill pass find an area.
                ("IX_GeoArea_Tenant_Scheme_Name", "GeoAreas",
                    "[TenantId], [SchemeId], [Name]", false, null),
                ("IX_GeoArea_ParentId", "GeoAreas", "[ParentAreaId]", false, null),
                ("IX_GeoArea_LevelId", "GeoAreas", "[GeoLevelId]", false, null),
                ("IX_GeoAreas_SchemeId", "GeoAreas", "[SchemeId]", false, null),
                ("IX_GeoAreas_SupersededByGeoAreaId", "GeoAreas", "[SupersededByGeoAreaId]", false, null),

                // ── GeoAreaAliases ──────────────────────────────────────────────────────────
                ("IX_GeoAreaAlias_Tenant_Area_Alias", "GeoAreaAliases",
                    "[TenantId], [GeoAreaId], [Alias]", true, "[IsDeleted] = 0"),
                // The resolver's lookup: "does any area answer to this name?"
                ("IX_GeoAreaAlias_Tenant_Alias", "GeoAreaAliases", "[TenantId], [Alias]", false, null),
                ("IX_GeoAreaAliases_GeoAreaId", "GeoAreaAliases", "[GeoAreaId]", false, null),
            })
            {
                var kind = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
                var where = filter is null ? string.Empty : $" WHERE {filter}";
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    {kind} [{name}] ON [dbo].[{table}] ({columns}){where};");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reference data only — no module's records are lost by this, because nothing points at
            // a GeoArea yet. The consumer foreign keys arrive in phase 4 (Location, CompanyProfile,
            // Employee); once they exist, this Down needs a halt guard like the destructive HR
            // migrations carry, or dropping these tables silently strips resolved addresses.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.GeoAreaAliases', 'U') IS NOT NULL
    DROP TABLE [dbo].[GeoAreaAliases];

IF OBJECT_ID('dbo.GeoAreas', 'U') IS NOT NULL
    DROP TABLE [dbo].[GeoAreas];

IF OBJECT_ID('dbo.GeoLevels', 'U') IS NOT NULL
    DROP TABLE [dbo].[GeoLevels];

IF OBJECT_ID('dbo.GeoSchemes', 'U') IS NOT NULL
    DROP TABLE [dbo].[GeoSchemes];");
        }
    }
}
