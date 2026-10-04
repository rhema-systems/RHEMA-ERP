using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261003090000_AddSalesCommercialQuantityAuthorities")]
public partial class AddSalesCommercialQuantityAuthorities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddCommercialQuantityEvidence(migrationBuilder, "QuoteLineItems");
        AddCommercialQuantityEvidence(migrationBuilder, "SalesAgreementLines");

        migrationBuilder.CreateTable(
            name: "SalesForecasts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Method = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                TotalForecastAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                TotalActualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                OwnerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalesForecasts", x => x.Id);
                table.ForeignKey("FK_SalesForecasts_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SalesForecastLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SalesForecastId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SalesRepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SalesRepId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ForecastQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UnitOfMeasureCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                UnitOfMeasureDecimalPlacesSnapshot = table.Column<int>(type: "int", nullable: true),
                UnitOfMeasureRoundingIncrementSnapshot = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                ForecastAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ActualQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                ActualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalesForecastLines", x => x.Id);
                table.ForeignKey("FK_SalesForecastLines_SalesForecasts_SalesForecastId", x => x.SalesForecastId, "SalesForecasts", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SalesForecastLines_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SalesForecastLines_UnitsOfMeasure_UnitOfMeasureId", x => x.UnitOfMeasureId, "UnitsOfMeasure", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_SalesForecasts_TenantId", "SalesForecasts", "TenantId");
        migrationBuilder.CreateIndex("IX_SalesForecastLines_SalesForecastId", "SalesForecastLines", "SalesForecastId");
        migrationBuilder.CreateIndex("IX_SalesForecastLines_TenantId", "SalesForecastLines", "TenantId");
        migrationBuilder.CreateIndex("IX_SalesForecastLines_UnitOfMeasureId", "SalesForecastLines", "UnitOfMeasureId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SalesForecastLines");
        migrationBuilder.DropTable(name: "SalesForecasts");
        DropCommercialQuantityEvidence(migrationBuilder, "QuoteLineItems");
        DropCommercialQuantityEvidence(migrationBuilder, "SalesAgreementLines");
    }

    private static void AddCommercialQuantityEvidence(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<string>(name: "UnitOfMeasureCodeSnapshot", table: table, type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<int>(name: "UnitOfMeasureDecimalPlacesSnapshot", table: table, type: "int", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "UnitOfMeasureId", table: table, type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UnitOfMeasureRoundingIncrementSnapshot", table: table, type: "decimal(18,6)", nullable: true);
        migrationBuilder.CreateIndex(name: $"IX_{table}_UnitOfMeasureId", table: table, column: "UnitOfMeasureId");
        migrationBuilder.AddForeignKey(name: $"FK_{table}_UnitsOfMeasure_UnitOfMeasureId", table: table, column: "UnitOfMeasureId", principalTable: "UnitsOfMeasure", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    private static void DropCommercialQuantityEvidence(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropForeignKey(name: $"FK_{table}_UnitsOfMeasure_UnitOfMeasureId", table: table);
        migrationBuilder.DropIndex(name: $"IX_{table}_UnitOfMeasureId", table: table);
        migrationBuilder.DropColumn(name: "UnitOfMeasureCodeSnapshot", table: table);
        migrationBuilder.DropColumn(name: "UnitOfMeasureDecimalPlacesSnapshot", table: table);
        migrationBuilder.DropColumn(name: "UnitOfMeasureId", table: table);
        migrationBuilder.DropColumn(name: "UnitOfMeasureRoundingIncrementSnapshot", table: table);
    }
}
