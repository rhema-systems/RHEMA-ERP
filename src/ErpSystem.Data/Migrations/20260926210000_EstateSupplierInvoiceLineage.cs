using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260926210000_EstateSupplierInvoiceLineage")]
public sealed class EstateSupplierInvoiceLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("EstateAcquisitionId", "VendorInvoice", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>("EstatePayableKind", "VendorInvoice", "int", nullable: true);
        migrationBuilder.AddUniqueConstraint("AK_LandAcquisitions_TenantId_Id", "LandAcquisitions", new[] { "TenantId", "Id" });
        migrationBuilder.AddForeignKey("FK_VendorInvoice_LandAcquisitions_TenantId_EstateAcquisitionId", "VendorInvoice",
            new[] { "TenantId", "EstateAcquisitionId" }, "LandAcquisitions", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.CreateIndex("IX_VendorInvoice_TenantId_EstateAcquisitionId_EstatePayableKind", "VendorInvoice",
            new[] { "TenantId", "EstateAcquisitionId", "EstatePayableKind" }, unique: true, filter: "[EstateAcquisitionId] IS NOT NULL");
        migrationBuilder.AddCheckConstraint("CK_VendorInvoice_EstateSource", "VendorInvoice",
            "([EstateAcquisitionId] IS NULL AND [EstatePayableKind] IS NULL) OR ([EstateAcquisitionId] IS NOT NULL AND [EstatePayableKind] IS NOT NULL AND [EstatePayableKind] BETWEEN 1 AND 4 AND [IsOpeningBalance] = 0 AND [PurchaseOrderId] IS NULL AND [AcceptedSupplyKind] IS NULL AND [AutoInvoiceRequestId] IS NULL)");
        // Existing reference-only invoices require explicit reconciliation; never adopt client supplied reference text.
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_VendorInvoice_EstateSource ON dbo.VendorInvoice AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE
                    ISNULL(i.EstateAcquisitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.EstateAcquisitionId, '00000000-0000-0000-0000-000000000000') OR
                    ISNULL(i.EstatePayableKind, 0) <> ISNULL(d.EstatePayableKind, 0))
                    THROW 51732, 'Estate supplier invoice source identity is immutable.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.VendorInvoice WHERE EstateAcquisitionId IS NOT NULL) THROW 51733, 'Preserve Estate supplier invoice source records before rollback.', 1;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_VendorInvoice_EstateSource;");
        migrationBuilder.DropForeignKey("FK_VendorInvoice_LandAcquisitions_TenantId_EstateAcquisitionId", "VendorInvoice");
        migrationBuilder.DropIndex("IX_VendorInvoice_TenantId_EstateAcquisitionId_EstatePayableKind", "VendorInvoice");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoice_EstateSource", "VendorInvoice");
        migrationBuilder.DropUniqueConstraint("AK_LandAcquisitions_TenantId_Id", "LandAcquisitions");
        migrationBuilder.DropColumn("EstatePayableKind", "VendorInvoice");
        migrationBuilder.DropColumn("EstateAcquisitionId", "VendorInvoice");
    }
}
