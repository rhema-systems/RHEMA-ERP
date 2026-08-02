using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds controlled AP/AR advance application references and an immutable Sales credit-note
/// reversal trail. The unapplied balance table is rebuildable reporting state, not source GL.
/// </summary>
public partial class AddAdvanceSettlementAndCreditNoteReversalFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "SupplierAdvanceAccountId", table: "FinanceSettings", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "CustomerAdvanceAccountId", table: "FinanceSettings", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsSupplierAdvance", table: "VendorPayment", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "IsCustomerAdvance", table: "CustomerPayment", type: "bit", nullable: false, defaultValue: false);

        migrationBuilder.AddColumn<Guid>(name: "ApplicationJournalEntryId", table: "VendorPaymentAllocation", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ApplicationPostingEventId", table: "VendorPaymentAllocation", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ApplicationJournalEntryId", table: "PaymentAllocation", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ApplicationPostingEventId", table: "PaymentAllocation", type: "uniqueidentifier", nullable: true);

        migrationBuilder.AddColumn<Guid>(name: "ReversalJournalEntryId", table: "CreditNotes", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ReversalPostingEventId", table: "CreditNotes", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "ReversedAt", table: "CreditNotes", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReversalReason", table: "CreditNotes", type: "nvarchar(500)", maxLength: 500, nullable: true);

        migrationBuilder.CreateTable(
            name: "SubledgerUnappliedSettlementBalances",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceModule = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                CounterpartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SettlementSourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SettlementSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SettlementSourceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Classification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SettlementPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SettlementJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                DocumentCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                UnappliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RebuildBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LastRebuiltAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                HasDiagnostics = table.Column<bool>(type: "bit", nullable: false),
                DiagnosticFlags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Priority = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_SubledgerUnappliedSettlementBalances", x => x.Id);
                table.ForeignKey("FK_SubledgerUnappliedSettlementBalances_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SubledgerUnappliedSettlementBalances_FinancePostingEvents_SettlementPostingEventId", x => x.SettlementPostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SubledgerUnappliedSettlementBalances_JournalEntries_SettlementJournalEntryId", x => x.SettlementJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_SettlementSourceType_SettlementSourceId", table: "SubledgerUnappliedSettlementBalances", columns: new[] { "TenantId", "SourceModule", "SettlementSourceType", "SettlementSourceId" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(name: "IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_CounterpartyId_SettlementDate", table: "SubledgerUnappliedSettlementBalances", columns: new[] { "TenantId", "SourceModule", "CounterpartyId", "SettlementDate" });
        migrationBuilder.CreateIndex(name: "IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementPostingEventId", table: "SubledgerUnappliedSettlementBalances", columns: new[] { "TenantId", "SettlementPostingEventId" });
        migrationBuilder.CreateIndex(name: "IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementJournalEntryId", table: "SubledgerUnappliedSettlementBalances", columns: new[] { "TenantId", "SettlementJournalEntryId" });
        migrationBuilder.CreateIndex(name: "IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId", table: "VendorPaymentAllocation", columns: new[] { "TenantId", "ApplicationPostingEventId" });
        migrationBuilder.CreateIndex(name: "IX_PaymentAllocation_TenantId_ApplicationPostingEventId", table: "PaymentAllocation", columns: new[] { "TenantId", "ApplicationPostingEventId" });
        migrationBuilder.CreateIndex(name: "IX_CreditNotes_TenantId_ReversalJournalEntryId", table: "CreditNotes", columns: new[] { "TenantId", "ReversalJournalEntryId" });
        migrationBuilder.CreateIndex(name: "IX_CreditNotes_TenantId_ReversalPostingEventId", table: "CreditNotes", columns: new[] { "TenantId", "ReversalPostingEventId" });

        migrationBuilder.AddForeignKey(name: "FK_FinanceSettings_Accounts_SupplierAdvanceAccountId", table: "FinanceSettings", column: "SupplierAdvanceAccountId", principalTable: "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_FinanceSettings_Accounts_CustomerAdvanceAccountId", table: "FinanceSettings", column: "CustomerAdvanceAccountId", principalTable: "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId", table: "VendorPaymentAllocation", column: "ApplicationJournalEntryId", principalTable: "JournalEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId", table: "VendorPaymentAllocation", column: "ApplicationPostingEventId", principalTable: "FinancePostingEvents", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId", table: "PaymentAllocation", column: "ApplicationJournalEntryId", principalTable: "JournalEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId", table: "PaymentAllocation", column: "ApplicationPostingEventId", principalTable: "FinancePostingEvents", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_CreditNotes_JournalEntries_ReversalJournalEntryId", table: "CreditNotes", column: "ReversalJournalEntryId", principalTable: "JournalEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId", table: "CreditNotes", column: "ReversalPostingEventId", principalTable: "FinancePostingEvents", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SubledgerUnappliedSettlementBalances");
        migrationBuilder.DropForeignKey(name: "FK_FinanceSettings_Accounts_SupplierAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropForeignKey(name: "FK_FinanceSettings_Accounts_CustomerAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropForeignKey(name: "FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId", table: "VendorPaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId", table: "PaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId", table: "PaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_CreditNotes_JournalEntries_ReversalJournalEntryId", table: "CreditNotes");
        migrationBuilder.DropForeignKey(name: "FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId", table: "CreditNotes");
        migrationBuilder.DropIndex(name: "IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropIndex(name: "IX_PaymentAllocation_TenantId_ApplicationPostingEventId", table: "PaymentAllocation");
        migrationBuilder.DropIndex(name: "IX_CreditNotes_TenantId_ReversalJournalEntryId", table: "CreditNotes");
        migrationBuilder.DropIndex(name: "IX_CreditNotes_TenantId_ReversalPostingEventId", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "SupplierAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "CustomerAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "IsSupplierAdvance", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "IsCustomerAdvance", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ApplicationJournalEntryId", table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "ApplicationPostingEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "ApplicationJournalEntryId", table: "PaymentAllocation");
        migrationBuilder.DropColumn(name: "ApplicationPostingEventId", table: "PaymentAllocation");
        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "ReversedAt", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "ReversalReason", table: "CreditNotes");
    }
}
