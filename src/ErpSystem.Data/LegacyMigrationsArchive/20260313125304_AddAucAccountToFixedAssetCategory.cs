using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAucAccountToFixedAssetCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AucAccountId",
                table: "FixedAssetCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseInterestExpenseAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseLiabilityAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseRouAssetAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CapitalProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalBudgetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAccumulatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CapitalizedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_CapitalProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapitalProjects_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaseContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MonthlyPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentFrequency = table.Column<int>(type: "int", nullable: false),
                    AnnualDiscountRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalPeriods = table.Column<int>(type: "int", nullable: false),
                    PresentValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RouAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_LeaseContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseContracts_BusinessPartners_LessorId",
                        column: x => x.LessorId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaseContracts_FixedAssets_RouAssetId",
                        column: x => x.RouAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaseContracts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCostLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapitalProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentType = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ProjectCostLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCostLines_CapitalProjects_CapitalProjectId",
                        column: x => x.CapitalProjectId,
                        principalTable: "CapitalProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCostLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSettlementRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapitalProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetFixedAssetCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedAssetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllocationPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ResultingFixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProjectSettlementRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSettlementRules_CapitalProjects_CapitalProjectId",
                        column: x => x.CapitalProjectId,
                        principalTable: "CapitalProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSettlementRules_FixedAssetCategories_TargetFixedAssetCategoryId",
                        column: x => x.TargetFixedAssetCategoryId,
                        principalTable: "FixedAssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSettlementRules_FixedAssets_ResultingFixedAssetId",
                        column: x => x.ResultingFixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectSettlementRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaseScheduleLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    PeriodDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestExpense = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PrincipalReduction = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingLiability = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_LeaseScheduleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseScheduleLines_LeaseContracts_LeaseContractId",
                        column: x => x.LeaseContractId,
                        principalTable: "LeaseContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaseScheduleLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AucAccountId",
                table: "FixedAssetCategories",
                column: "AucAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_LeaseInterestExpenseAccountId",
                table: "FinanceSettings",
                column: "LeaseInterestExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_LeaseLiabilityAccountId",
                table: "FinanceSettings",
                column: "LeaseLiabilityAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_LeaseRouAssetAccountId",
                table: "FinanceSettings",
                column: "LeaseRouAssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitalProjects_TenantId",
                table: "CapitalProjects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContracts_LessorId",
                table: "LeaseContracts",
                column: "LessorId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContracts_RouAssetId",
                table: "LeaseContracts",
                column: "RouAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContracts_TenantId",
                table: "LeaseContracts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseScheduleLines_LeaseContractId",
                table: "LeaseScheduleLines",
                column: "LeaseContractId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseScheduleLines_TenantId",
                table: "LeaseScheduleLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCostLines_CapitalProjectId",
                table: "ProjectCostLines",
                column: "CapitalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCostLines_TenantId",
                table: "ProjectCostLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettlementRules_CapitalProjectId",
                table: "ProjectSettlementRules",
                column: "CapitalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettlementRules_ResultingFixedAssetId",
                table: "ProjectSettlementRules",
                column: "ResultingFixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettlementRules_TargetFixedAssetCategoryId",
                table: "ProjectSettlementRules",
                column: "TargetFixedAssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettlementRules_TenantId",
                table: "ProjectSettlementRules",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_LeaseInterestExpenseAccountId",
                table: "FinanceSettings",
                column: "LeaseInterestExpenseAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_LeaseLiabilityAccountId",
                table: "FinanceSettings",
                column: "LeaseLiabilityAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_LeaseRouAssetAccountId",
                table: "FinanceSettings",
                column: "LeaseRouAssetAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAssetCategories_Accounts_AucAccountId",
                table: "FixedAssetCategories",
                column: "AucAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_LeaseInterestExpenseAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_LeaseLiabilityAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_LeaseRouAssetAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FixedAssetCategories_Accounts_AucAccountId",
                table: "FixedAssetCategories");

            migrationBuilder.DropTable(
                name: "LeaseScheduleLines");

            migrationBuilder.DropTable(
                name: "ProjectCostLines");

            migrationBuilder.DropTable(
                name: "ProjectSettlementRules");

            migrationBuilder.DropTable(
                name: "LeaseContracts");

            migrationBuilder.DropTable(
                name: "CapitalProjects");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetCategories_AucAccountId",
                table: "FixedAssetCategories");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_LeaseInterestExpenseAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_LeaseLiabilityAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_LeaseRouAssetAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "AucAccountId",
                table: "FixedAssetCategories");

            migrationBuilder.DropColumn(
                name: "LeaseInterestExpenseAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "LeaseLiabilityAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "LeaseRouAssetAccountId",
                table: "FinanceSettings");
        }
    }
}
