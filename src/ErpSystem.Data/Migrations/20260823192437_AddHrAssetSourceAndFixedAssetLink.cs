using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 2b. Gives <c>CompanyAssets</c> its provenance (<c>Source</c>), its link into the
    /// Finance fixed-asset register (<c>FixedAssetId</c>), and the two fields the TDC change document
    /// asks for: <c>AdditionalRemarks</c> (AST-7) and <c>InsuranceExpiryDate</c> (AST-4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Source</c> defaults to 1 — <c>AssetSource.HrCreated</c> — and that is a statement of
    /// fact, not a placeholder.</b> Every row that exists before this column did was registered by HR
    /// directly, because until now there was no other way in. Without the default those rows would
    /// read as source <c>0</c>, a value the enum does not define, which no screen could render and no
    /// rule could branch on. The flag is load-bearing: <c>CompanyAssetService</c> reads it to refuse
    /// edits to Finance-owned figures and to refuse disposal of a capitalised asset.
    /// </para>
    /// <para>
    /// ⚠ <b><c>IX_CompanyAssets_FixedAssetId</c> is deliberately NOT unique</b>, although the rule is
    /// one HR entry per fixed asset. Every delete in this area is a <b>soft</b> delete, so a unique
    /// index would keep the slot after an HR entry was removed and would then refuse the re-link
    /// forever, with a constraint violation no user could read and no screen could explain. The rule
    /// lives in <c>CompanyAssetService.CreateFromFixedAssetAsync</c>, where it can see
    /// <c>IsDeleted</c> and can name the asset already holding the link. Area 13 lost five separate
    /// faces to a unique index over a soft-deleted store; this is that lesson applied in advance
    /// rather than after the fact.
    /// </para>
    /// <para>
    /// <b>The <c>UnitId</c> foreign key is dropped and re-added</b> to move it to
    /// <c>ON DELETE NO ACTION</c>. It existed already — minted by the <c>[ForeignKey]</c> annotation
    /// on the navigation — but the navigation itself was configured nowhere, so it carried the
    /// convention's default cascade. That column is also the reason this slice exists at all:
    /// <c>UnitId</c> was mapped into <c>CompanyAssetSummaryDto</c> and was absent from the create
    /// payload, the update payload and the read DTO, so <c>unitId</c> and <c>unitName</c> on every
    /// list row were permanently null. Defect D-i(b).
    /// </para>
    /// <para>
    /// <b>The FK into <c>FixedAssets</c> is HR depending on Finance</b>, which is a new direction in
    /// this solution but not a new shape: <c>FixedAsset</c> itself carries exactly this kind of
    /// nullable FK into <c>MaintenanceAssets</c>. <c>Restrict</c> rather than cascade, because a
    /// fixed asset disappearing must not silently take the HR custody record with it — and in
    /// practice both stores soft-delete, so the constraint guards inserts rather than deletes.
    /// </para>
    /// <para>
    /// Written as guarded SQL rather than the scaffolded <c>AddColumn</c>/<c>AddForeignKey</c> calls
    /// so it is safe to re-run against a database at either state, matching the surrounding HR
    /// migrations.
    /// </para>
    /// </remarks>
    public partial class AddHrAssetSourceAndFixedAssetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── columns ────────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AdditionalRemarks' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [AdditionalRemarks] nvarchar(1000) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'InsuranceExpiryDate' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [InsuranceExpiryDate] date NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'FixedAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [FixedAssetId] uniqueidentifier NULL;");

            // The default is named so the Down can drop it: an unnamed SQL Server default constraint
            // gets a generated name that Down cannot predict, which is how a reversible migration
            // quietly stops being reversible.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Source' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [Source] int NOT NULL
        CONSTRAINT [DF_CompanyAssets_Source] DEFAULT 1;");

            // ── indexes ────────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_FixedAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    CREATE INDEX [IX_CompanyAssets_FixedAssetId] ON [dbo].[CompanyAssets] ([FixedAssetId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_InsuranceExpiryDate' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    CREATE INDEX [IX_CompanyAssets_InsuranceExpiryDate] ON [dbo].[CompanyAssets] ([InsuranceExpiryDate]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_Source' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    CREATE INDEX [IX_CompanyAssets_Source] ON [dbo].[CompanyAssets] ([Source]);");

            // ── foreign keys ───────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_FixedAssets_FixedAssetId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD CONSTRAINT [FK_CompanyAssets_FixedAssets_FixedAssetId]
        FOREIGN KEY ([FixedAssetId]) REFERENCES [dbo].[FixedAssets] ([Id]) ON DELETE NO ACTION;");

            // Drop-then-add, because the constraint already exists with the convention's cascade and
            // the model now says Restrict. An "IF NOT EXISTS … ADD" alone would find the old one,
            // skip, and record the migration as applied with the wrong delete behaviour still in
            // place — the shape slice 4b of areas 19-23 paid for.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_OrganizationUnits_UnitId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP CONSTRAINT [FK_CompanyAssets_OrganizationUnits_UnitId];");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_OrganizationUnits_UnitId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD CONSTRAINT [FK_CompanyAssets_OrganizationUnits_UnitId]
        FOREIGN KEY ([UnitId]) REFERENCES [dbo].[OrganizationUnits] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_FixedAssets_FixedAssetId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP CONSTRAINT [FK_CompanyAssets_FixedAssets_FixedAssetId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_OrganizationUnits_UnitId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP CONSTRAINT [FK_CompanyAssets_OrganizationUnits_UnitId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_FixedAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    DROP INDEX [IX_CompanyAssets_FixedAssetId] ON [dbo].[CompanyAssets];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_InsuranceExpiryDate' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    DROP INDEX [IX_CompanyAssets_InsuranceExpiryDate] ON [dbo].[CompanyAssets];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_Source' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    DROP INDEX [IX_CompanyAssets_Source] ON [dbo].[CompanyAssets];");

            // The named default has to go before its column can.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CompanyAssets_Source' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP CONSTRAINT [DF_CompanyAssets_Source];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Source' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP COLUMN [Source];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'FixedAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP COLUMN [FixedAssetId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'InsuranceExpiryDate' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP COLUMN [InsuranceExpiryDate];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AdditionalRemarks' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP COLUMN [AdditionalRemarks];");

            // Restore the UnitId FK at the convention's default, which is what a database rolled
            // back to before this migration actually had.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_OrganizationUnits_UnitId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD CONSTRAINT [FK_CompanyAssets_OrganizationUnits_UnitId]
        FOREIGN KEY ([UnitId]) REFERENCES [dbo].[OrganizationUnits] ([Id]);");
        }
    }
}
