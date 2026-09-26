using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924210000_LandedCostReceiptWeights")]
public sealed class LandedCostReceiptWeights : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("LandedCostReceiptWeights", columns: table => new
        {
            Id = table.Column<Guid>(nullable: false), TenantId = table.Column<Guid>(nullable: false),
            LandedCostId = table.Column<Guid>(nullable: false), GoodsReceiptNoteItemId = table.Column<Guid>(nullable: false),
            UnitWeightKg = table.Column<decimal>(type: "decimal(22,6)", nullable: false),
            StockUom = table.Column<string>(maxLength: 20, nullable: false), Reason = table.Column<string>(maxLength: 500, nullable: false),
            CreatedAt = table.Column<DateTime>(nullable: false), UpdatedAt = table.Column<DateTime>(nullable: true),
            CreatedBy = table.Column<string>(nullable: true), UpdatedBy = table.Column<string>(nullable: true),
            CreatedById = table.Column<Guid>(nullable: true), LastModifiedById = table.Column<Guid>(nullable: true),
            IsDeleted = table.Column<bool>(nullable: false), DeletedAt = table.Column<DateTime>(nullable: true), DeletedBy = table.Column<string>(nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_LandedCostReceiptWeights", x => x.Id);
            table.ForeignKey("FK_LandedCostReceiptWeights_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_LandedCostReceiptWeights_LandedCosts_LandedCostId", x => x.LandedCostId, "LandedCosts", "Id");
            table.ForeignKey("FK_LandedCostReceiptWeights_GoodsReceiptNoteItems_GoodsReceiptNoteItemId", x => x.GoodsReceiptNoteItemId, "GoodsReceiptNoteItems", "Id");
        });
        migrationBuilder.CreateIndex("IX_LandedCostReceiptWeights_TenantId_LandedCostId_GoodsReceiptNoteItemId", "LandedCostReceiptWeights", new[] { "TenantId", "LandedCostId", "GoodsReceiptNoteItemId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LandedCostReceiptWeights_LandedCostId", "LandedCostReceiptWeights", "LandedCostId");
        migrationBuilder.CreateIndex("IX_LandedCostReceiptWeights_GoodsReceiptNoteItemId", "LandedCostReceiptWeights", "GoodsReceiptNoteItemId");
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_LandedCostReceiptWeights_Source ON dbo.LandedCostReceiptWeights
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i
                    LEFT JOIN dbo.LandedCosts c WITH (UPDLOCK, HOLDLOCK) ON c.Id = i.LandedCostId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                    LEFT JOIN dbo.GoodsReceiptNoteItems r ON r.Id = i.GoodsReceiptNoteItemId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                    WHERE c.Id IS NULL OR c.Status <> 'Draft' OR r.Id IS NULL OR r.GoodsReceiptNoteId <> c.GoodsReceiptNoteId
                       OR i.StockUom <> r.UnitOfMeasure OR LEN(LTRIM(RTRIM(i.StockUom))) = 0
                       OR i.UnitWeightKg <= 0 OR LEN(LTRIM(RTRIM(i.Reason))) = 0 OR i.IsDeleted = 1)
                    THROW 51711, 'Weight declarations require a draft voucher and its own tenant receipt stock line.', 1;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id
                    WHERE i.Id IS NULL OR i.TenantId <> d.TenantId OR i.LandedCostId <> d.LandedCostId OR i.GoodsReceiptNoteItemId <> d.GoodsReceiptNoteItemId)
                    THROW 51712, 'Weight declaration source links are immutable; retain the declaration history.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("LandedCostReceiptWeights");
}
