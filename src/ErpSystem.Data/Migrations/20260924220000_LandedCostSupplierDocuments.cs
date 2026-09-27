using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924220000_LandedCostSupplierDocuments")]
public sealed class LandedCostSupplierDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("LandedCostSupplierDocuments", columns: table => new
        {
            Id = table.Column<Guid>(nullable: false), TenantId = table.Column<Guid>(nullable: false),
            DocumentNumber = table.Column<string>(type: "nvarchar(37)", maxLength: 37, nullable: false,
                computedColumnSql: "'LCSD-' + LOWER(REPLACE(CONVERT(varchar(36), [Id]), '-', ''))", stored: true),
            LandedCostItemId = table.Column<Guid>(nullable: false), LandedCostId = table.Column<Guid>(nullable: false),
            GoodsReceiptNoteId = table.Column<Guid>(nullable: false), PurchaseOrderId = table.Column<Guid>(nullable: true),
            BusinessPartnerId = table.Column<Guid>(nullable: false), CostType = table.Column<int>(nullable: false),
            Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false), Currency = table.Column<string>(maxLength: 10, nullable: false),
            CreatedAt = table.Column<DateTime>(nullable: false), UpdatedAt = table.Column<DateTime>(nullable: true),
            CreatedBy = table.Column<string>(nullable: true), UpdatedBy = table.Column<string>(nullable: true),
            CreatedById = table.Column<Guid>(nullable: true), LastModifiedById = table.Column<Guid>(nullable: true),
            IsDeleted = table.Column<bool>(nullable: false), DeletedAt = table.Column<DateTime>(nullable: true), DeletedBy = table.Column<string>(nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_LandedCostSupplierDocuments", x => x.Id);
            table.ForeignKey("FK_LandedCostSupplierDocuments_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_LandedCostSupplierDocuments_LandedCostItems_LandedCostItemId", x => x.LandedCostItemId, "LandedCostItems", "Id");
            table.ForeignKey("FK_LandedCostSupplierDocuments_LandedCosts_LandedCostId", x => x.LandedCostId, "LandedCosts", "Id");
            table.ForeignKey("FK_LandedCostSupplierDocuments_GoodsReceiptNotes_GoodsReceiptNoteId", x => x.GoodsReceiptNoteId, "GoodsReceiptNotes", "Id");
            table.ForeignKey("FK_LandedCostSupplierDocuments_PurchaseOrders_PurchaseOrderId", x => x.PurchaseOrderId, "PurchaseOrders", "Id");
            table.ForeignKey("FK_LandedCostSupplierDocuments_BusinessPartners_BusinessPartnerId", x => x.BusinessPartnerId, "BusinessPartners", "Id");
        });
        foreach (var column in new[] { "LandedCostItemId", "LandedCostId", "GoodsReceiptNoteId", "PurchaseOrderId", "BusinessPartnerId" })
            migrationBuilder.CreateIndex($"IX_LandedCostSupplierDocuments_{column}", "LandedCostSupplierDocuments", column);
        migrationBuilder.CreateIndex("IX_LandedCostSupplierDocuments_TenantId_LandedCostItemId", "LandedCostSupplierDocuments", new[] { "TenantId", "LandedCostItemId" }, unique: true);
        // A computed column cannot participate in a SQL Server filtered index.
        migrationBuilder.Sql("CREATE UNIQUE INDEX IX_LandedCostSupplierDocuments_TenantId_DocumentNumber ON dbo.LandedCostSupplierDocuments(TenantId, DocumentNumber);");
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_LandedCostSupplierDocuments_Immutable ON dbo.LandedCostSupplierDocuments
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51713, 'Landed-cost supplier documents are immutable source evidence.', 1;
                IF EXISTS (SELECT 1 FROM inserted d
                    LEFT JOIN dbo.LandedCostItems i ON i.Id = d.LandedCostItemId AND i.TenantId = d.TenantId AND i.IsDeleted = 0
                    LEFT JOIN dbo.LandedCosts c ON c.Id = d.LandedCostId AND c.TenantId = d.TenantId AND c.IsDeleted = 0
                    LEFT JOIN dbo.GoodsReceiptNotes r ON r.Id = d.GoodsReceiptNoteId AND r.TenantId = d.TenantId AND r.IsDeleted = 0
                    LEFT JOIN dbo.BusinessPartners p ON p.Id = d.BusinessPartnerId AND p.TenantId = d.TenantId AND p.IsDeleted = 0
                    LEFT JOIN dbo.PurchaseOrders po ON po.Id = d.PurchaseOrderId AND po.TenantId = d.TenantId AND po.IsDeleted = 0
                    WHERE i.Id IS NULL OR c.Id IS NULL OR r.Id IS NULL OR p.Id IS NULL OR i.SupplierId IS NULL
                       OR (d.PurchaseOrderId IS NOT NULL AND po.Id IS NULL)
                       OR i.LandedCostId <> c.Id OR c.GoodsReceiptNoteId <> r.Id OR i.SupplierId <> d.BusinessPartnerId
                       OR (r.PurchaseOrderId IS NULL AND d.PurchaseOrderId IS NOT NULL)
                       OR (r.PurchaseOrderId IS NOT NULL AND d.PurchaseOrderId IS NULL) OR r.PurchaseOrderId <> d.PurchaseOrderId
                       OR i.CostType <> d.CostType OR i.Amount <> d.Amount OR i.Currency <> d.Currency
                       OR d.Amount <= 0 OR d.IsDeleted = 1 OR c.Status NOT IN ('Allocated','Approved','Posted'))
                    THROW 51714, 'Supplier document must match the allocated charge, supplier and original receipt in the same tenant.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("LandedCostSupplierDocuments");
}
