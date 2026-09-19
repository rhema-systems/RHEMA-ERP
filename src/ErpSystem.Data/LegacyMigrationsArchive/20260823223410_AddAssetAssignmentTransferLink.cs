using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 4. Gives <c>AssetAssignments</c> a <c>TransferId</c> — the twin of the
    /// <c>RequisitionId</c> added in slice 3 — so that an assignment created by completing an
    /// employee-to-employee transfer can say what authorised it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without it, closing the transfer-completion gap would have produced an assignment that
    /// appears from nowhere: the recipient holds the asset, and nothing on their record points back
    /// at the transfer that moved it. Same reasoning as D-e, one surface along.
    /// </para>
    /// <para>
    /// ⚠ <b>The scaffold also dropped and re-added <c>FK_AssetTransfer_Tenants_TenantId</c>,
    /// changing it from CASCADE to RESTRICT, and that is kept deliberately.</b> It is not drift
    /// introduced here — <c>ConfigureGlobalTenantRelationships</c> has always set every tenant
    /// foreign key to Restrict "to avoid multiple cascade paths in SQL Server", so the model has
    /// always said Restrict and this one table's constraint was left on Cascade by an earlier
    /// migration. Adding <c>AssetAssignments → AssetTransfer</c> is what made it matter: with
    /// <c>Tenants → AssetTransfer → AssetAssignments</c> alongside the existing
    /// <c>Tenants → AssetAssignments</c>, SQL Server refuses the new foreign key outright while the
    /// old one still cascades. So the correction is a precondition of the column, not a side
    /// effect of it, and it moves the database towards what the model has always described.
    /// </para>
    /// <para>
    /// Guarded SQL throughout, matching the surrounding HR migrations, so it is safe to re-run
    /// against a database at either state — including one built from the EF model directly, where
    /// the tenant constraint is already Restrict and there is nothing to correct.
    /// </para>
    /// </remarks>
    public partial class AddAssetAssignmentTransferLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── the tenant constraint first: the new FK below cannot be created while this one
            //    still cascades, because that would give Tenants two cascade paths into
            //    AssetAssignments ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetTransfer_Tenants_TenantId' AND parent_object_id = OBJECT_ID('dbo.AssetTransfer') AND delete_referential_action <> 0)
    ALTER TABLE [dbo].[AssetTransfer] DROP CONSTRAINT [FK_AssetTransfer_Tenants_TenantId];");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetTransfer_Tenants_TenantId' AND parent_object_id = OBJECT_ID('dbo.AssetTransfer'))
    ALTER TABLE [dbo].[AssetTransfer] ADD CONSTRAINT [FK_AssetTransfer_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            // ── where a custody came from, when a transfer produced it ────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TransferId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [TransferId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_TransferId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    CREATE INDEX [IX_AssetAssignments_TransferId] ON [dbo].[AssetAssignments] ([TransferId]);");

            // ⚠ Singular [AssetTransfer], not [AssetTransfers]. HR's transfer table is the one place
            // in this module that is not pluralised — the same collision the build plan's §3.3
            // records at the entity, interface and workflow-key levels.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetAssignments_AssetTransfer_TransferId' AND parent_object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD CONSTRAINT [FK_AssetAssignments_AssetTransfer_TransferId]
        FOREIGN KEY ([TransferId]) REFERENCES [dbo].[AssetTransfer] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetAssignments_AssetTransfer_TransferId' AND parent_object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP CONSTRAINT [FK_AssetAssignments_AssetTransfer_TransferId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_TransferId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    DROP INDEX [IX_AssetAssignments_TransferId] ON [dbo].[AssetAssignments];");

            // The provenance of any assignment created by a completed transfer is lost here, and
            // nothing else records it. The assignment itself survives — the asset is still in the
            // right hands — but which transfer put it there is gone.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TransferId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP COLUMN [TransferId];");

            // ⚠ The tenant constraint is deliberately NOT put back on CASCADE. The scaffold's Down
            // restored it, because a differ only knows what it changed; but Cascade contradicts
            // ConfigureGlobalTenantRelationships, which every other table in this module already
            // follows, and putting it back would leave the database in a state the model has never
            // described. Rolling this migration back removes the column, not the correction.
        }
    }
}
