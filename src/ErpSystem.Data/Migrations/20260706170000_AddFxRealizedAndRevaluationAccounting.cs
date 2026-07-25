using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706170000_AddFxRealizedAndRevaluationAccounting")]
    public partial class AddFxRealizedAndRevaluationAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UnrealizedFxGainAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnrealizedFxLossAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FxRealizedSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SettlementDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SettlementDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SettledForeignAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HistoricalExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    HistoricalExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SettlementExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    SettlementExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HistoricalFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SettlementFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GainLossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GainLossType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GainLossAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_FxRealizedSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FxRealizedSettlements_Accounts_ControlAccountId",
                        column: x => x.ControlAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRealizedSettlements_Accounts_GainLossAccountId",
                        column: x => x.GainLossAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRealizedSettlements_FinancePostingEvents_PostingEventId",
                        column: x => x.PostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRealizedSettlements_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRealizedSettlements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FxRevaluationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalGainAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalLossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetGainLossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AutoReverseNextPeriod = table.Column<bool>(type: "bit", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_FxRevaluationBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FxRevaluationBatches_FinancePostingEvents_PostingEventId",
                        column: x => x.PostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationBatches_FinancePostingEvents_ReversalPostingEventId",
                        column: x => x.ReversalPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationBatches_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationBatches_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationBatches_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FxRevaluationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FxRevaluationBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ForeignCurrencyBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CarryingFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosingExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    RevaluedFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GainLossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GainLossType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GainLossAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FxRevaluationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_Accounts_GainLossAccountId",
                        column: x => x.GainLossAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_ExchangeRates_ClosingExchangeRateId",
                        column: x => x.ClosingExchangeRateId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_FinancePostingEvents_PostingEventId",
                        column: x => x.PostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_FxRevaluationBatches_FxRevaluationBatchId",
                        column: x => x.FxRevaluationBatchId,
                        principalTable: "FxRevaluationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxRevaluationLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_UnrealizedFxGainAccountId",
                table: "FinanceSettings",
                column: "UnrealizedFxGainAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_UnrealizedFxLossAccountId",
                table: "FinanceSettings",
                column: "UnrealizedFxLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_ControlAccountId",
                table: "FxRealizedSettlements",
                column: "ControlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_GainLossAccountId",
                table: "FxRealizedSettlements",
                column: "GainLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_JournalEntryId",
                table: "FxRealizedSettlements",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_PostingEventId",
                table: "FxRealizedSettlements",
                column: "PostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_TenantId",
                table: "FxRealizedSettlements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_TenantId_IdempotencyKey",
                table: "FxRealizedSettlements",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FxRealizedSettlements_TenantId_SettlementDocumentType_SettlementDocumentId_SettlementAllocationId",
                table: "FxRealizedSettlements",
                columns: new[] { "TenantId", "SettlementDocumentType", "SettlementDocumentId", "SettlementAllocationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_FiscalPeriodId",
                table: "FxRevaluationBatches",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_JournalEntryId",
                table: "FxRevaluationBatches",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_PostingEventId",
                table: "FxRevaluationBatches",
                column: "PostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_ReversalJournalEntryId",
                table: "FxRevaluationBatches",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_ReversalPostingEventId",
                table: "FxRevaluationBatches",
                column: "ReversalPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_TenantId",
                table: "FxRevaluationBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_TenantId_IdempotencyKey",
                table: "FxRevaluationBatches",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_TenantId_Scope_RevaluationDate_FiscalPeriodId",
                table: "FxRevaluationBatches",
                columns: new[] { "TenantId", "Scope", "RevaluationDate", "FiscalPeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_AccountId",
                table: "FxRevaluationLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_ClosingExchangeRateId",
                table: "FxRevaluationLines",
                column: "ClosingExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_FxRevaluationBatchId",
                table: "FxRevaluationLines",
                column: "FxRevaluationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_GainLossAccountId",
                table: "FxRevaluationLines",
                column: "GainLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_JournalEntryId",
                table: "FxRevaluationLines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_PostingEventId",
                table: "FxRevaluationLines",
                column: "PostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_TenantId",
                table: "FxRevaluationLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_TenantId_AccountId_TransactionCurrency",
                table: "FxRevaluationLines",
                columns: new[] { "TenantId", "AccountId", "TransactionCurrency" });

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_UnrealizedFxGainAccountId",
                table: "FinanceSettings",
                column: "UnrealizedFxGainAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_UnrealizedFxLossAccountId",
                table: "FinanceSettings",
                column: "UnrealizedFxLossAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_UnrealizedFxGainAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_UnrealizedFxLossAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropTable(
                name: "FxRealizedSettlements");

            migrationBuilder.DropTable(
                name: "FxRevaluationLines");

            migrationBuilder.DropTable(
                name: "FxRevaluationBatches");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_UnrealizedFxGainAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_UnrealizedFxLossAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "UnrealizedFxGainAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "UnrealizedFxLossAccountId",
                table: "FinanceSettings");
        }
    }
}
