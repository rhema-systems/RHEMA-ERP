using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 9b. HR sends an asset to the workshop — <b>AST-1</b>'s other half, decision
    /// <b>D10</b>. Three columns on <c>AssetMaintenances</c> tying an HR maintenance record to the
    /// admission it was raised on in the Maintenance module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Deliberately no foreign keys.</b> These name rows in another module's <i>transaction</i>
    /// tables — <c>AssetAdmissions</c> and <c>AssetDischarges</c> — and an FK from here would block
    /// that team's deletes and tie an HR migration to the shape of their tables. The register link
    /// added in slice 9 (<c>CompanyAssets.MaintenanceAssetId</c>) <b>is</b> an FK, because it names
    /// a <i>master</i>. That is the distinction, and it is the same one the reminder dispatch log
    /// draws with its bare <c>EntityId</c>. EF scaffolded this correctly — three columns, no
    /// relationship inferred from the property names.
    /// </para>
    /// <para>
    /// <b><c>MaintenanceAdmissionNumber</c> is a copy, on purpose.</b> The id is the join; the
    /// number is what somebody quotes on the phone to the workshop, and it has to still read on an
    /// HR screen when the other module is unreachable or the row has been archived. Same reasoning
    /// as the identity fields copied from Finance at the moment of linking in slice 2b.
    /// </para>
    /// <para>
    /// <b>The pair of them is the state.</b> Admission set and discharge null means the asset is
    /// physically somewhere else — the one question a maintenance record could not answer before
    /// this slice. Both null means HR kept the job in-house; a battery changed at the desk needs no
    /// workshop.
    /// </para>
    /// <para>
    /// ⚠ <b>Every add is guarded and every drop is conditional</b>, so this is safe to re-run against
    /// a database at either state — including one built from the EF model rather than from the
    /// chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddAssetMaintenanceAdmissionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MaintenanceAdmissionId' AND object_id = OBJECT_ID('dbo.AssetMaintenances'))
    ALTER TABLE [dbo].[AssetMaintenances] ADD [MaintenanceAdmissionId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MaintenanceAdmissionNumber' AND object_id = OBJECT_ID('dbo.AssetMaintenances'))
    ALTER TABLE [dbo].[AssetMaintenances] ADD [MaintenanceAdmissionNumber] nvarchar(50) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MaintenanceDischargeId' AND object_id = OBJECT_ID('dbo.AssetMaintenances'))
    ALTER TABLE [dbo].[AssetMaintenances] ADD [MaintenanceDischargeId] uniqueidentifier NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Reversing this severs HR's record of what is at the workshop, while the admissions
            // themselves stay open in the Maintenance module. Nothing else on the HR side carries the
            // link, so an asset out for repair becomes an asset HR believes is in maintenance with no
            // way to say where — and completing its record would no longer discharge anything.
            foreach (var column in new[]
            {
                "MaintenanceDischargeId",
                "MaintenanceAdmissionNumber",
                "MaintenanceAdmissionId",
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = '{column}' AND object_id = OBJECT_ID('dbo.AssetMaintenances'))
    ALTER TABLE [dbo].[AssetMaintenances] DROP COLUMN [{column}];");
            }
        }
    }
}
