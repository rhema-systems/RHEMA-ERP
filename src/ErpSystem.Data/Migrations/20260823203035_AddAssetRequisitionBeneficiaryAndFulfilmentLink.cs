using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 3. Gives <c>AssetRequisitions</c> a <c>BeneficiaryEmployeeId</c> (AST-6b — who
    /// the asset is <i>for</i>, beside who asked), gives <c>AssetAssignments</c> a
    /// <c>RequisitionId</c> (D-e — what a requisition actually produced), and removes
    /// <c>AssetRequisitions.AssignedAssetId</c>, which could only ever remember one asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>The scaffold wrote this as a RENAME of <c>AssignedAssetId</c> to
    /// <c>BeneficiaryEmployeeId</c>, and that was rewritten by hand into a DROP and an ADD.</b> EF's
    /// model differ saw one nullable <c>Guid</c> column leave and another arrive on the same table
    /// and inferred the cheaper operation. It is the wrong one, and not merely stylistically:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>A rename <b>keeps the data</b>. The old column held a
    ///   <c>CompanyAssets.Id</c>; the new one holds an <c>Employees.Id</c>. Any populated row would
    ///   end up with an asset's key sitting in an employee foreign key — and the
    ///   <c>FK_AssetRequisitions_Employees_BeneficiaryEmployeeId</c> added three lines later would
    ///   then fail with error 547 on a database where fulfilment had ever run.</description></item>
    ///   <item><description>It also renames the <b>index</b>, so a database restored from that
    ///   migration would carry an index whose name says one thing and whose column means
    ///   another.</description></item>
    /// </list>
    /// <para>
    /// Measured before rewriting, on DEFAULT: <c>AssetRequisitions</c> holds 14 rows and
    /// <b>0 of them carry an <c>AssignedAssetId</c></b> — because defect D-k meant no requisition
    /// could ever be approved, and so none was ever fulfilled. The rename would therefore have been
    /// harmless <i>here</i>. It is rewritten anyway: the next database this runs against is not
    /// promised to be this one, and a migration that silently mistypes a foreign key is not
    /// something to leave loaded because today's data happens to be empty.
    /// </para>
    /// <para>
    /// The dropped column is genuinely gone rather than deprecated in place. What a requisition
    /// produced is now recorded on the assignments that cite it, so a fulfilment of three assets is
    /// three facts instead of one fact and two silences.
    /// </para>
    /// <para>
    /// Guarded SQL throughout, matching the surrounding HR migrations, so it is safe to re-run
    /// against a database at either state.
    /// </para>
    /// </remarks>
    public partial class AddAssetRequisitionBeneficiaryAndFulfilmentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── out with the single-asset column, FK and index first ───────────────────────────
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetRequisitions_CompanyAssets_AssignedAssetId' AND parent_object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] DROP CONSTRAINT [FK_AssetRequisitions_CompanyAssets_AssignedAssetId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetRequisitions_AssignedAssetId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    DROP INDEX [IX_AssetRequisitions_AssignedAssetId] ON [dbo].[AssetRequisitions];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AssignedAssetId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] DROP COLUMN [AssignedAssetId];");

            // ── AST-6b: who the asset is for ──────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'BeneficiaryEmployeeId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] ADD [BeneficiaryEmployeeId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetRequisitions_BeneficiaryEmployeeId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    CREATE INDEX [IX_AssetRequisitions_BeneficiaryEmployeeId] ON [dbo].[AssetRequisitions] ([BeneficiaryEmployeeId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetRequisitions_Employees_BeneficiaryEmployeeId' AND parent_object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] ADD CONSTRAINT [FK_AssetRequisitions_Employees_BeneficiaryEmployeeId]
        FOREIGN KEY ([BeneficiaryEmployeeId]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            // ── D-e: what a requisition produced ──────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RequisitionId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [RequisitionId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_RequisitionId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    CREATE INDEX [IX_AssetAssignments_RequisitionId] ON [dbo].[AssetAssignments] ([RequisitionId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetAssignments_AssetRequisitions_RequisitionId' AND parent_object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD CONSTRAINT [FK_AssetAssignments_AssetRequisitions_RequisitionId]
        FOREIGN KEY ([RequisitionId]) REFERENCES [dbo].[AssetRequisitions] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetAssignments_AssetRequisitions_RequisitionId' AND parent_object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP CONSTRAINT [FK_AssetAssignments_AssetRequisitions_RequisitionId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_RequisitionId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    DROP INDEX [IX_AssetAssignments_RequisitionId] ON [dbo].[AssetAssignments];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RequisitionId' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP COLUMN [RequisitionId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetRequisitions_Employees_BeneficiaryEmployeeId' AND parent_object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] DROP CONSTRAINT [FK_AssetRequisitions_Employees_BeneficiaryEmployeeId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetRequisitions_BeneficiaryEmployeeId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    DROP INDEX [IX_AssetRequisitions_BeneficiaryEmployeeId] ON [dbo].[AssetRequisitions];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'BeneficiaryEmployeeId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] DROP COLUMN [BeneficiaryEmployeeId];");

            // ⚠ The old column comes back EMPTY, and that is the honest outcome. Down cannot recover
            // which asset fulfilled a requisition, because after this migration that fact lives on
            // the assignments — and it lives there more completely than it ever did here. Rolling
            // back loses the pointer, not the history.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AssignedAssetId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] ADD [AssignedAssetId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetRequisitions_AssignedAssetId' AND object_id = OBJECT_ID('dbo.AssetRequisitions'))
    CREATE INDEX [IX_AssetRequisitions_AssignedAssetId] ON [dbo].[AssetRequisitions] ([AssignedAssetId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetRequisitions_CompanyAssets_AssignedAssetId' AND parent_object_id = OBJECT_ID('dbo.AssetRequisitions'))
    ALTER TABLE [dbo].[AssetRequisitions] ADD CONSTRAINT [FK_AssetRequisitions_CompanyAssets_AssignedAssetId]
        FOREIGN KEY ([AssignedAssetId]) REFERENCES [dbo].[CompanyAssets] ([Id]) ON DELETE NO ACTION;");
        }
    }
}
