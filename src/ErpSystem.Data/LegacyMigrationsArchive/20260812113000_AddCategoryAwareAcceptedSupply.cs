using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260812113000_AddCategoryAwareAcceptedSupply")]
public partial class AddCategoryAwareAcceptedSupply : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ProcurementCategory",
            table: "PurchaseOrders",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AcceptedSupplyKind",
            table: "VendorInvoice",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "AcceptedSupplySourceId",
            table: "VendorInvoice",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "AcceptedSupplySourceReference",
            table: "VendorInvoice",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "AcceptedSupplySnapshotHash",
            table: "VendorInvoice",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "AcceptedSupplyValidatedAtUtc",
            table: "VendorInvoice",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE po
               SET ProcurementCategory = pr.ProcurementCategory
              FROM dbo.PurchaseOrders po
              JOIN dbo.PurchaseRequisitions pr
                ON pr.Id = po.SourceRequisitionId
               AND pr.TenantId = po.TenantId
               AND pr.IsDeleted = 0
             WHERE po.ProcurementCategory IS NULL
               AND pr.ProcurementCategory IS NOT NULL;

            UPDATE vi
               SET AcceptedSupplyKind = 3,
                   AcceptedSupplySourceId = certificate.Id,
                   AcceptedSupplySourceReference = LEFT(
                       COALESCE(NULLIF(certificate.CertificateNumber, ''),
                           CONVERT(nvarchar(36), certificate.Id)), 100),
                   AcceptedSupplySnapshotHash = COALESCE(
                       NULLIF(certificate.GeneratedDocumentHash, ''),
                       NULLIF(certificate.PolicyHash, ''),
                       certificate.RequestHash),
                   AcceptedSupplyValidatedAtUtc = certificate.ApprovedAt
              FROM dbo.VendorInvoice vi
              JOIN dbo.ProjectPaymentCertificates certificate
                ON certificate.VendorInvoiceId = vi.Id
               AND certificate.TenantId = vi.TenantId
               AND certificate.IsDeleted = 0
             WHERE vi.IsDeleted = 0
               AND vi.AcceptedSupplyKind IS NULL
               AND certificate.Status IN ('Approved', 'Paid')
               AND certificate.ApprovalStatus = 'Approved'
               AND certificate.ApprovedAt IS NOT NULL
               AND LEN(COALESCE(
                       NULLIF(certificate.GeneratedDocumentHash, ''),
                       NULLIF(certificate.PolicyHash, ''),
                       certificate.RequestHash)) = 64;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseOrders_ProcurementCategory",
            table: "PurchaseOrders",
            sql: "[ProcurementCategory] IS NULL OR [ProcurementCategory] BETWEEN 0 AND 4");
        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseOrders_GovernedCategoryRequired",
            table: "PurchaseOrders",
            sql: "[ProcurementSourceType] IS NULL OR [ProcurementSourceType] = 5 OR [ProcurementCategory] IS NOT NULL");
        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorInvoice_AcceptedSupplyCoherent",
            table: "VendorInvoice",
            sql: "([AcceptedSupplyKind] IS NULL AND [AcceptedSupplySourceId] IS NULL AND [AcceptedSupplySourceReference] IS NULL AND [AcceptedSupplySnapshotHash] IS NULL AND [AcceptedSupplyValidatedAtUtc] IS NULL) OR ([AcceptedSupplyKind] BETWEEN 1 AND 3 AND [AcceptedSupplySourceId] IS NOT NULL AND LEN([AcceptedSupplySourceReference]) BETWEEN 1 AND 100 AND LEN([AcceptedSupplySnapshotHash]) = 64 AND [AcceptedSupplyValidatedAtUtc] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorInvoice_AcceptedSupplyPurchaseOrder",
            table: "VendorInvoice",
            sql: "[AcceptedSupplyKind] IS NULL OR [AcceptedSupplyKind] = 3 OR [PurchaseOrderId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "UX_VendorInvoice_AcceptedCertificate",
            table: "VendorInvoice",
            columns: new[] { "TenantId", "AcceptedSupplyKind", "AcceptedSupplySourceId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [AcceptedSupplyKind] IN (2, 3) AND [AcceptedSupplySourceId] IS NOT NULL");

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoice_AcceptedSupplyProtected]
            ON [dbo].[VendorInvoice]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status NOT IN (1, 8)
                      AND (
                           ISNULL(i.AcceptedSupplyKind, -1) <> ISNULL(d.AcceptedSupplyKind, -1)
                        OR ISNULL(i.AcceptedSupplySourceId, '00000000-0000-0000-0000-000000000000') <>
                           ISNULL(d.AcceptedSupplySourceId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.AcceptedSupplySourceReference, '') <> ISNULL(d.AcceptedSupplySourceReference, '')
                        OR ISNULL(i.AcceptedSupplySnapshotHash, '') <> ISNULL(d.AcceptedSupplySnapshotHash, '')
                        OR ISNULL(i.AcceptedSupplyValidatedAtUtc, '19000101') <>
                           ISNULL(d.AcceptedSupplyValidatedAtUtc, '19000101')
                      ))
                    THROW 52051, 'Accepted-supply lineage is immutable after invoice submission.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN dbo.PurchaseOrders po
                      ON po.Id = i.PurchaseOrderId
                     AND po.TenantId = i.TenantId
                     AND po.IsDeleted = 0
                    WHERE (d.Id IS NULL OR d.Status <> i.Status)
                      AND i.Status IN (2, 3, 4, 5, 6, 9)
                      AND (
                           (po.ProcurementCategory = 0 AND
                              (ISNULL(i.AcceptedSupplyKind, -1) <> 1 OR
                               ISNULL(i.AcceptedSupplySourceId, '00000000-0000-0000-0000-000000000000') <> po.Id))
                        OR (po.ProcurementCategory IN (2, 3, 4) AND
                            ISNULL(i.AcceptedSupplyKind, -1) <> 2)
                        OR (po.ProcurementCategory = 1)
                        OR (i.PurchaseOrderId IS NULL AND i.Reference LIKE 'QS-%' AND
                            ISNULL(i.AcceptedSupplyKind, -1) <> 3)
                      ))
                    THROW 52052, 'Invoice approval requires the authoritative category-specific accepted-supply lineage.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoice_AcceptedSupplyProtected];");
        migrationBuilder.DropIndex(
            name: "UX_VendorInvoice_AcceptedCertificate",
            table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(
            name: "CK_VendorInvoice_AcceptedSupplyPurchaseOrder",
            table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(
            name: "CK_VendorInvoice_AcceptedSupplyCoherent",
            table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseOrders_GovernedCategoryRequired",
            table: "PurchaseOrders");
        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseOrders_ProcurementCategory",
            table: "PurchaseOrders");
        migrationBuilder.DropColumn("AcceptedSupplyKind", "VendorInvoice");
        migrationBuilder.DropColumn("AcceptedSupplySourceId", "VendorInvoice");
        migrationBuilder.DropColumn("AcceptedSupplySourceReference", "VendorInvoice");
        migrationBuilder.DropColumn("AcceptedSupplySnapshotHash", "VendorInvoice");
        migrationBuilder.DropColumn("AcceptedSupplyValidatedAtUtc", "VendorInvoice");
        migrationBuilder.DropColumn("ProcurementCategory", "PurchaseOrders");
    }
}
