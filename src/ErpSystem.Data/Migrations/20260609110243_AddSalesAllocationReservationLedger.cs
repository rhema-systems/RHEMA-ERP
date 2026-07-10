using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesAllocationReservationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleableSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AdapterKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceItemId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SourceItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceItemName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SourceItemType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SalesAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReservedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AgreedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAllocations_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAllocations_SalesAgreements_SalesAgreementId",
                        column: x => x.SalesAgreementId,
                        principalTable: "SalesAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesAllocations_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesAllocations_SalesSaleableSources_SaleableSourceId",
                        column: x => x.SaleableSourceId,
                        principalTable: "SalesSaleableSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesAllocationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PerformedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAllocationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAllocationHistories_SalesAllocations_SalesAllocationId",
                        column: x => x.SalesAllocationId,
                        principalTable: "SalesAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesAllocationHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocationHistories_SalesAllocationId",
                table: "SalesAllocationHistories",
                column: "SalesAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocationHistories_TenantId_SalesAllocationId_PerformedAt",
                table: "SalesAllocationHistories",
                columns: new[] { "TenantId", "SalesAllocationId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_BusinessPartnerId",
                table: "SalesAllocations",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_SaleableSourceId",
                table: "SalesAllocations",
                column: "SaleableSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_SalesAgreementId",
                table: "SalesAllocations",
                column: "SalesAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_SalesOrderId",
                table: "SalesAllocations",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_TenantId_BusinessPartnerId",
                table: "SalesAllocations",
                columns: new[] { "TenantId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_TenantId_SaleableSourceId_SourceItemId",
                table: "SalesAllocations",
                columns: new[] { "TenantId", "SaleableSourceId", "SourceItemId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('Reserved','Allocated','Sold','Leased','PendingApproval','Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_TenantId_SalesAgreementId",
                table: "SalesAllocations",
                columns: new[] { "TenantId", "SalesAgreementId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_TenantId_SalesOrderId",
                table: "SalesAllocations",
                columns: new[] { "TenantId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAllocations_TenantId_Status_CreatedAt",
                table: "SalesAllocations",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesAllocationHistories");

            migrationBuilder.DropTable(
                name: "SalesAllocations");
        }
    }
}
