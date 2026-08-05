using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpeningBalanceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PeriodDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PeriodCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PeriodNetMovement = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalanceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    YearToDateDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    YearToDateCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    YearToDateNetMovement = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SegmentString = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CostCenterSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    BaseCurrencyEquivalent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnrealizedGainLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTransactionUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    LastReconciledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciliationDiscrepancy = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasActivity = table.Column<bool>(type: "bit", nullable: false),
                    IsZeroBalance = table.Column<bool>(type: "bit", nullable: false),
                    IsNegativeBalance = table.Column<bool>(type: "bit", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBalances_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_AccountId",
                table: "AccountBalances",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_CostCenterSegment_FiscalPeriodId",
                table: "AccountBalances",
                columns: new[] { "CostCenterSegment", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_DepartmentSegment_FiscalPeriodId",
                table: "AccountBalances",
                columns: new[] { "DepartmentSegment", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_ProjectSegment_FiscalPeriodId",
                table: "AccountBalances",
                columns: new[] { "ProjectSegment", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_AccountId_BookClassification",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountId", "BookClassification" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_AccountId_FiscalPeriodId_BookClassification_Currency",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountId", "FiscalPeriodId", "BookClassification", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_FiscalPeriodId_BookClassification",
                table: "AccountBalances",
                columns: new[] { "TenantId", "FiscalPeriodId", "BookClassification" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_IsReconciled",
                table: "AccountBalances",
                columns: new[] { "TenantId", "IsReconciled" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_LastUpdated",
                table: "AccountBalances",
                columns: new[] { "TenantId", "LastUpdated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBalances");
        }
    }
}
