using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831231434_AddFinanceSettlementDimensions")]
public partial class AddFinanceSettlementDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinanceSettlementDimensionComponents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RouteId = table.Column<int>(type: "int", nullable: false),
                ProducerModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceRoute = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SettlementSourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SettlementAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OriginatingDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OriginatingSourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ComponentType = table.Column<int>(type: "int", nullable: false),
                FinanceDimensionSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinanceDimensionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TransactionCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                TransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                FunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                EvidenceVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                IsFinalResidualRecipient = table.Column<bool>(type: "bit", nullable: false),
                RoundingResidualTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RoundingResidualFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                EvidenceFrozenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_FinanceSettlementDimensionComponents", x => x.Id);
                table.CheckConstraint("CK_FinanceSettlementDimensionComponents_ComponentType", "[ComponentType] IN (0,1,2,3,4,5,6)");
                table.ForeignKey(
                    name: "FK_FinanceSettlementDimensionComponents_FinanceDimensionSets_FinanceDimensionSetId",
                    column: x => x.FinanceDimensionSetId,
                    principalTable: "FinanceDimensionSets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceSettlementDimensionComponents_FinanceDimensionSnapshots_FinanceDimensionSnapshotId",
                    column: x => x.FinanceDimensionSnapshotId,
                    principalTable: "FinanceDimensionSnapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceSettlementDimensionComponents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceSettlementDimensionComponents_EvidenceKey",
            table: "FinanceSettlementDimensionComponents",
            columns: new[] { "TenantId", "RouteId", "SourceDocumentId", "SettlementSourceLineId", "OriginatingSourceLineId", "ComponentType" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSettlementDimensionComponents_FinanceDimensionSetId",
            table: "FinanceSettlementDimensionComponents",
            column: "FinanceDimensionSetId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSettlementDimensionComponents_FinanceDimensionSnapshotId",
            table: "FinanceSettlementDimensionComponents",
            column: "FinanceDimensionSnapshotId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSettlementDimensionComponents_TenantId_RouteId_SourceDocumentId",
            table: "FinanceSettlementDimensionComponents",
            columns: new[] { "TenantId", "RouteId", "SourceDocumentId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "FinanceSettlementDimensionComponents");
}
