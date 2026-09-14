using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Keep discovery metadata on the executable migration because routine Debug builds omit EF's
    // multi-megabyte generated designer history. Without these attributes, deployment could
    // compile successfully while silently overlooking the cashier/till control schema.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803163336_AddCashierTillControlWorkspace")]
    /// <inheritdoc />
    public partial class AddCashierTillControlWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashTillVarianceApprovalThreshold",
                table: "FinanceSettings",
                type: "decimal(18,4)",
                nullable: false,
                // TDC development baseline: variances above GHS 100 require explicit reviewer
                // comments. The value remains tenant-configurable after migration.
                defaultValue: 100m);

            migrationBuilder.AddColumn<bool>(
                name: "RequireIndependentCashTillClosure",
                table: "FinanceSettings",
                type: "bit",
                nullable: false,
                // All TDC till closures start with maker-checker enabled, including zero variance.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "CashierTillSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpeningFloatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpeningNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OpeningEvidenceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityCutoffAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionMovementAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedClosingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CountedClosingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VarianceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VarianceApprovalThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustodyEntryCount = table.Column<int>(type: "int", nullable: false),
                    VarianceReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClosingEvidenceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrectsSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CashierTillSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierTillSessions_CashierTillSessions_CorrectsSessionId",
                        column: x => x.CorrectsSessionId,
                        principalTable: "CashierTillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierTillSessions_FileUploadRecords_ClosingEvidenceFileId",
                        column: x => x.ClosingEvidenceFileId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierTillSessions_FileUploadRecords_OpeningEvidenceFileId",
                        column: x => x.OpeningEvidenceFileId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierTillSessions_LiquidityAccounts_LiquidityAccountId",
                        column: x => x.LiquidityAccountId,
                        principalTable: "LiquidityAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierTillSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierTillCountLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierTillSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Denomination = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_CashierTillCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierTillCountLines_CashierTillSessions_CashierTillSessionId",
                        column: x => x.CashierTillSessionId,
                        principalTable: "CashierTillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashierTillCountLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillCountLines_CashierTillSessionId",
                table: "CashierTillCountLines",
                column: "CashierTillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillCountLines_TenantId_CashierTillSessionId_Denomination",
                table: "CashierTillCountLines",
                columns: new[] { "TenantId", "CashierTillSessionId", "Denomination" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_ClosingEvidenceFileId",
                table: "CashierTillSessions",
                column: "ClosingEvidenceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_CorrectsSessionId",
                table: "CashierTillSessions",
                column: "CorrectsSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_LiquidityAccountId",
                table: "CashierTillSessions",
                column: "LiquidityAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_OpeningEvidenceFileId",
                table: "CashierTillSessions",
                column: "OpeningEvidenceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_TenantId_CashierUserId_Status",
                table: "CashierTillSessions",
                columns: new[] { "TenantId", "CashierUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_TenantId_LiquidityAccountId_Status_OpenedAt",
                table: "CashierTillSessions",
                columns: new[] { "TenantId", "LiquidityAccountId", "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierTillSessions_TenantId_SessionNumber",
                table: "CashierTillSessions",
                columns: new[] { "TenantId", "SessionNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_CashierTillSessions_ActiveTill",
                table: "CashierTillSessions",
                columns: new[] { "TenantId", "LiquidityAccountId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashierTillCountLines");

            migrationBuilder.DropTable(
                name: "CashierTillSessions");

            migrationBuilder.DropColumn(
                name: "CashTillVarianceApprovalThreshold",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "RequireIndependentCashTillClosure",
                table: "FinanceSettings");
        }
    }
}
