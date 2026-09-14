using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMaterialCostLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectMaterialCostEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostingState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AffectsActualCost = table.Column<bool>(type: "bit", nullable: false),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceTransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InventoryItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HasMissingSourceLink = table.Column<bool>(type: "bit", nullable: false),
                    HasReversalGap = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectMaterialCostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMaterialCostEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMaterialCostEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMaterialCostEntries_ProjectId_EntryDate",
                table: "ProjectMaterialCostEntries",
                columns: new[] { "ProjectId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMaterialCostEntries_ProjectId_EntryType_PostingState",
                table: "ProjectMaterialCostEntries",
                columns: new[] { "ProjectId", "EntryType", "PostingState" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMaterialCostEntries_TenantId_SourceTransactionType_SourceTransactionId",
                table: "ProjectMaterialCostEntries",
                columns: new[] { "TenantId", "SourceTransactionType", "SourceTransactionId" },
                unique: true,
                filter: "[SourceTransactionType] IS NOT NULL AND [SourceTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectMaterialCostEntries");
        }
    }
}
