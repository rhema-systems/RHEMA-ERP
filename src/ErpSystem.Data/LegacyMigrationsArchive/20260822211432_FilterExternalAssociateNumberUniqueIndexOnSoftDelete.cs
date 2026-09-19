using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Makes <c>(TenantId, AssociateNumber)</c> unique among live rows:
    /// <c>IX_ExternalAssociates_Tenant_Number … WHERE IsDeleted = 0</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slice 8, and D-10 — the defect slice 0 found by execution back on 2026-08-22 and named as
    /// slice 8's to own. <c>AssociateNumber</c> was indexed but <b>not</b> unique, and
    /// <c>ExternalAssociateService.GenerateAssociateNumberAsync</c> took the highest <c>EXT-nnnn</c>
    /// through the soft-delete filter, so a removed associate's number was invisible to the
    /// generator and was minted again. Measured on DEFAULT before the fix: <b>38 rows carried
    /// EXT-0008</b>, every one of them deleted, and <c>GET number/{n}</c> resolved them with a
    /// <c>FirstOrDefault</c> — so the lookup answered with whichever row the engine reached first.
    /// </para>
    /// <para>
    /// <b>Third occurrence of one trap in this bundle</b>, after D-29 (<c>IX_Team_Tenant_Code</c>,
    /// slice 4b) and D-9 (<c>IX_EmployeeRelievers_EmployeeId_Priority</c>, slice 7). Whenever a store
    /// soft-deletes, every uniqueness claim over it — index <i>or</i> generator — is wrong until
    /// proven otherwise. This one had both halves wrong at once.
    /// </para>
    /// <para>
    /// ⚠ <b>The filter is not optional, and the data is what settles it.</b> A plain unique index
    /// cannot be built here: those 38 duplicate rows are real, and dropping them is not this
    /// migration's business. Uniqueness among <i>live</i> rows is the claim the register actually
    /// makes; the generator no longer reissuing is what keeps a retired number out of circulation.
    /// Checked immediately before writing this: 48 rows, 41 of them deleted, and <b>no two live rows
    /// share a number</b> — so the index builds.
    /// </para>
    /// <para>
    /// ⚠ <b><c>IX_ExternalAssociates_TenantId</c> goes with it</b>, and that is EF's diff rather than
    /// a choice made here: an index whose leading column is <c>TenantId</c> makes the convention's
    /// single-column one redundant, so the model no longer declares it. Keeping it by hand would
    /// leave the database permanently ahead of the model. The one read it used to serve and the new
    /// index cannot — the generator's include-deleted sweep, which the filter excludes by
    /// definition — now scans, and at 48 rows that is a page. Recorded here so it is a known trade
    /// rather than a later discovery.
    /// </para>
    /// <para>
    /// Written as guarded SQL rather than the scaffolded <c>DropIndex</c>/<c>CreateIndex</c> pair so
    /// it is safe to re-run against a database at either state, matching slices 4b and 7 and the
    /// surrounding HR migrations.
    /// </para>
    /// </remarks>
    public partial class FilterExternalAssociateNumberUniqueIndexOnSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalAssociates_TenantId' AND object_id = OBJECT_ID('dbo.ExternalAssociates'))
    DROP INDEX [IX_ExternalAssociates_TenantId] ON [dbo].[ExternalAssociates];");

            // Drop first, unconditionally-if-present. The name is new today, but slice 4b paid for
            // the general lesson: an "IF NOT EXISTS … CREATE" on its own finds an index already
            // sitting under the same name — whatever its shape — skips, and records the migration as
            // applied, leaving the defect in place and looking fixed.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalAssociates_Tenant_Number' AND object_id = OBJECT_ID('dbo.ExternalAssociates'))
    DROP INDEX [IX_ExternalAssociates_Tenant_Number] ON [dbo].[ExternalAssociates];");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalAssociates_Tenant_Number' AND object_id = OBJECT_ID('dbo.ExternalAssociates'))
    CREATE UNIQUE INDEX [IX_ExternalAssociates_Tenant_Number] ON [dbo].[ExternalAssociates] ([TenantId], [AssociateNumber]) WHERE [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalAssociates_Tenant_Number' AND object_id = OBJECT_ID('dbo.ExternalAssociates'))
    DROP INDEX [IX_ExternalAssociates_Tenant_Number] ON [dbo].[ExternalAssociates];");

            // ⚠ Unlike slice 7's Down, this one cannot fail: the index it restores is not unique.
            // The uniqueness simply goes away, which is the state this migration found.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalAssociates_TenantId' AND object_id = OBJECT_ID('dbo.ExternalAssociates'))
    CREATE INDEX [IX_ExternalAssociates_TenantId] ON [dbo].[ExternalAssociates] ([TenantId]);");
        }
    }
}
