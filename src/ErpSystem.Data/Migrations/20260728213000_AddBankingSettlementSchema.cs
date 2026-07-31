using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Creates the operational liquidity, bank-deposit, and returned-cheque schema.
///
/// This migration is intentionally dated after the budgeting/account-balance migrations.
/// The banking model was previously captured by the model snapshot without a corresponding
/// executable migration, so a later forward migration is required for databases which have
/// already applied those migrations.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260728213000_AddBankingSettlementSchema")]
public partial class AddBankingSettlementSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BankDepositPolicy",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "RequireBankDepositPrimaryEvidence",
            table: "FinanceSettings",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "AutoPostBankDepositAfterApproval",
            table: "FinanceSettings",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MaximumDepositDeductionAmount",
            table: "FinanceSettings",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MaximumDepositDeductionPercentage",
            table: "FinanceSettings",
            type: "decimal(9,4)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BankStatementMatchDateToleranceDays",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<int>(
            name: "ChequeClearingPeriodDays",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<Guid>(
            name: "ReturnedChequeBankChargeAccountId",
            table: "FinanceSettings",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DefaultReturnedChequeChargeTreatment",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "LiquidityAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AccountType = table.Column<int>(type: "int", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProviderAccountReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                AllowsNegativeBalance = table.Column<bool>(type: "bit", nullable: false),
                AllowsManualAllocations = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_LiquidityAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_LiquidityAccounts_Accounts_GLAccountId",
                    column: x => x.GLAccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LiquidityAccounts_BankAccounts_BankAccountId",
                    column: x => x.BankAccountId,
                    principalTable: "BankAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LiquidityAccounts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LiquidityAccountEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                EntryType = table.Column<int>(type: "int", nullable: false),
                Direction = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                SourceDocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CounterpartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsReversed = table.Column<bool>(type: "bit", nullable: false),
                ReversalOfEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_LiquidityAccountEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_LiquidityAccountEntries_LiquidityAccountEntries_ReversalOfEntryId",
                    column: x => x.ReversalOfEntryId,
                    principalTable: "LiquidityAccountEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LiquidityAccountEntries_LiquidityAccounts_LiquidityAccountId",
                    column: x => x.LiquidityAccountId,
                    principalTable: "LiquidityAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LiquidityAccountEntries_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "LiquidityAccountId",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LiquidityAccountEntryId",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ChequeDrawerBank",
            table: "CustomerPayment",
            type: "nvarchar(150)",
            maxLength: 150,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "BankDepositBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DepositNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DepositDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                DepositReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                PolicySnapshot = table.Column<int>(type: "int", nullable: false),
                TotalReceipts = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CashTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReversedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_BankDepositBatches", x => x.Id);
                table.ForeignKey(
                    name: "FK_BankDepositBatches_BankAccounts_BankAccountId",
                    column: x => x.BankAccountId,
                    principalTable: "BankAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BankDepositBatches_CashTransaction_CashTransactionId",
                    column: x => x.CashTransactionId,
                    principalTable: "CashTransaction",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BankDepositBatches_JournalEntries_JournalEntryId",
                    column: x => x.JournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BankDepositBatches_JournalEntries_ReversalJournalEntryId",
                    column: x => x.ReversalJournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BankDepositBatches_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "BankDepositAllocations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BankDepositBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LiquidityAccountEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AllocationType = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                table.PrimaryKey("PK_BankDepositAllocations", x => x.Id);
                table.ForeignKey(
                    name: "FK_BankDepositAllocations_BankDepositBatches_BankDepositBatchId",
                    column: x => x.BankDepositBatchId,
                    principalTable: "BankDepositBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BankDepositAllocations_LiquidityAccountEntries_LiquidityAccountEntryId",
                    column: x => x.LiquidityAccountEntryId,
                    principalTable: "LiquidityAccountEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BankDepositAllocations_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "BankDepositAttachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BankDepositBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                IsPrimaryEvidence = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_BankDepositAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_BankDepositAttachments_BankDepositBatches_BankDepositBatchId",
                    column: x => x.BankDepositBatchId,
                    principalTable: "BankDepositBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BankDepositAttachments_FileUploadRecords_FileUploadRecordId",
                    column: x => x.FileUploadRecordId,
                    principalTable: "FileUploadRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BankDepositAttachments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReturnedChequeCases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CaseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                CustomerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BankDepositBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChequeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DrawerBank = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ReturnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ReturnedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                BankChargeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ChargeTreatment = table.Column<int>(type: "int", nullable: false),
                CustomerRecoverableChargeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ExpenseChargeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReturnCashTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ChargeCashTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_ReturnedChequeCases", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_BankAccounts_BankAccountId",
                    column: x => x.BankAccountId,
                    principalTable: "BankAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_BankDepositBatches_BankDepositBatchId",
                    column: x => x.BankDepositBatchId,
                    principalTable: "BankDepositBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_CashTransaction_ChargeCashTransactionId",
                    column: x => x.ChargeCashTransactionId,
                    principalTable: "CashTransaction",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_CashTransaction_ReturnCashTransactionId",
                    column: x => x.ReturnCashTransactionId,
                    principalTable: "CashTransaction",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_CustomerPayment_CustomerPaymentId",
                    column: x => x.CustomerPaymentId,
                    principalTable: "CustomerPayment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_JournalEntries_JournalEntryId",
                    column: x => x.JournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeCases_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReturnedChequeAttachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReturnedChequeCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                IsPrimaryEvidence = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_ReturnedChequeAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReturnedChequeAttachments_FileUploadRecords_FileUploadRecordId",
                    column: x => x.FileUploadRecordId,
                    principalTable: "FileUploadRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnedChequeAttachments_ReturnedChequeCases_ReturnedChequeCaseId",
                    column: x => x.ReturnedChequeCaseId,
                    principalTable: "ReturnedChequeCases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ReturnedChequeAttachments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceSettings_ReturnedChequeBankChargeAccountId",
            table: "FinanceSettings",
            column: "ReturnedChequeBankChargeAccountId");

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceSettings_Accounts_ReturnedChequeBankChargeAccountId",
            table: "FinanceSettings",
            column: "ReturnedChequeBankChargeAccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(name: "IX_LiquidityAccounts_BankAccountId", table: "LiquidityAccounts", column: "BankAccountId");
        migrationBuilder.CreateIndex(name: "IX_LiquidityAccounts_GLAccountId", table: "LiquidityAccounts", column: "GLAccountId");
        migrationBuilder.CreateIndex(
            name: "IX_LiquidityAccounts_TenantId_AccountType_Currency_IsActive",
            table: "LiquidityAccounts",
            columns: new[] { "TenantId", "AccountType", "Currency", "IsActive" });
        migrationBuilder.CreateIndex(
            name: "IX_LiquidityAccounts_TenantId_BankAccountId",
            table: "LiquidityAccounts",
            columns: new[] { "TenantId", "BankAccountId" },
            unique: true,
            filter: "[BankAccountId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_LiquidityAccounts_TenantId_Code",
            table: "LiquidityAccounts",
            columns: new[] { "TenantId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(name: "IX_LiquidityAccountEntries_LiquidityAccountId", table: "LiquidityAccountEntries", column: "LiquidityAccountId");
        migrationBuilder.CreateIndex(name: "IX_LiquidityAccountEntries_ReversalOfEntryId", table: "LiquidityAccountEntries", column: "ReversalOfEntryId");
        migrationBuilder.CreateIndex(
            name: "IX_LiquidityAccountEntries_TenantId_EntryNumber",
            table: "LiquidityAccountEntries",
            columns: new[] { "TenantId", "EntryNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_LiquidityAccountEntries_TenantId_LiquidityAccountId_Currency_EntryDate_IsReversed",
            table: "LiquidityAccountEntries",
            columns: new[] { "TenantId", "LiquidityAccountId", "Currency", "EntryDate", "IsReversed" });
        migrationBuilder.CreateIndex(
            name: "IX_LiquidityAccountEntries_TenantId_SourceDocumentType_SourceDocumentId",
            table: "LiquidityAccountEntries",
            columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(name: "IX_CustomerPayment_LiquidityAccountId", table: "CustomerPayment", column: "LiquidityAccountId");
        migrationBuilder.CreateIndex(name: "IX_CustomerPayment_LiquidityAccountEntryId", table: "CustomerPayment", column: "LiquidityAccountEntryId");
        migrationBuilder.CreateIndex(
            name: "IX_CustomerPayment_TenantId_LiquidityAccountId_PaymentDate",
            table: "CustomerPayment",
            columns: new[] { "TenantId", "LiquidityAccountId", "PaymentDate" });
        migrationBuilder.AddForeignKey(
            name: "FK_CustomerPayment_LiquidityAccounts_LiquidityAccountId",
            table: "CustomerPayment",
            column: "LiquidityAccountId",
            principalTable: "LiquidityAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_CustomerPayment_LiquidityAccountEntries_LiquidityAccountEntryId",
            table: "CustomerPayment",
            column: "LiquidityAccountEntryId",
            principalTable: "LiquidityAccountEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(name: "IX_BankDepositBatches_BankAccountId", table: "BankDepositBatches", column: "BankAccountId");
        migrationBuilder.CreateIndex(name: "IX_BankDepositBatches_CashTransactionId", table: "BankDepositBatches", column: "CashTransactionId");
        migrationBuilder.CreateIndex(name: "IX_BankDepositBatches_JournalEntryId", table: "BankDepositBatches", column: "JournalEntryId");
        migrationBuilder.CreateIndex(name: "IX_BankDepositBatches_ReversalJournalEntryId", table: "BankDepositBatches", column: "ReversalJournalEntryId");
        migrationBuilder.CreateIndex(
            name: "IX_BankDepositBatches_TenantId_BankAccountId_DepositReference",
            table: "BankDepositBatches",
            columns: new[] { "TenantId", "BankAccountId", "DepositReference" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_BankDepositBatches_TenantId_DepositNumber",
            table: "BankDepositBatches",
            columns: new[] { "TenantId", "DepositNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_BankDepositBatches_TenantId_Status_DepositDate",
            table: "BankDepositBatches",
            columns: new[] { "TenantId", "Status", "DepositDate" });
        migrationBuilder.CreateIndex(
            name: "IX_BankDepositBatches_TenantId_WorkflowInstanceId",
            table: "BankDepositBatches",
            columns: new[] { "TenantId", "WorkflowInstanceId" });

        migrationBuilder.CreateIndex(name: "IX_BankDepositAllocations_BankDepositBatchId", table: "BankDepositAllocations", column: "BankDepositBatchId");
        migrationBuilder.CreateIndex(name: "IX_BankDepositAllocations_LiquidityAccountEntryId", table: "BankDepositAllocations", column: "LiquidityAccountEntryId");
        migrationBuilder.CreateIndex(
            name: "IX_BankDepositAllocations_TenantId_BankDepositBatchId_LiquidityAccountEntryId",
            table: "BankDepositAllocations",
            columns: new[] { "TenantId", "BankDepositBatchId", "LiquidityAccountEntryId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(name: "IX_BankDepositAttachments_BankDepositBatchId", table: "BankDepositAttachments", column: "BankDepositBatchId");
        migrationBuilder.CreateIndex(name: "IX_BankDepositAttachments_FileUploadRecordId", table: "BankDepositAttachments", column: "FileUploadRecordId");
        migrationBuilder.CreateIndex(
            name: "IX_BankDepositAttachments_TenantId_BankDepositBatchId_FileUploadRecordId",
            table: "BankDepositAttachments",
            columns: new[] { "TenantId", "BankDepositBatchId", "FileUploadRecordId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeCases_BankAccountId", table: "ReturnedChequeCases", column: "BankAccountId");
        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeCases_BankDepositBatchId", table: "ReturnedChequeCases", column: "BankDepositBatchId");
        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeCases_ChargeCashTransactionId", table: "ReturnedChequeCases", column: "ChargeCashTransactionId");
        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeCases_CustomerPaymentId", table: "ReturnedChequeCases", column: "CustomerPaymentId");
        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeCases_JournalEntryId", table: "ReturnedChequeCases", column: "JournalEntryId");
        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeCases_ReturnCashTransactionId", table: "ReturnedChequeCases", column: "ReturnCashTransactionId");
        migrationBuilder.CreateIndex(
            name: "IX_ReturnedChequeCases_TenantId_CaseNumber",
            table: "ReturnedChequeCases",
            columns: new[] { "TenantId", "CaseNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_ReturnedChequeCases_TenantId_CustomerPaymentId_Status",
            table: "ReturnedChequeCases",
            columns: new[] { "TenantId", "CustomerPaymentId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnedChequeCases_TenantId_WorkflowInstanceId",
            table: "ReturnedChequeCases",
            columns: new[] { "TenantId", "WorkflowInstanceId" });

        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeAttachments_FileUploadRecordId", table: "ReturnedChequeAttachments", column: "FileUploadRecordId");
        migrationBuilder.CreateIndex(name: "IX_ReturnedChequeAttachments_ReturnedChequeCaseId", table: "ReturnedChequeAttachments", column: "ReturnedChequeCaseId");
        migrationBuilder.CreateIndex(
            name: "IX_ReturnedChequeAttachments_TenantId_ReturnedChequeCaseId_FileUploadRecordId",
            table: "ReturnedChequeAttachments",
            columns: new[] { "TenantId", "ReturnedChequeCaseId", "FileUploadRecordId" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BankDepositAllocations");
        migrationBuilder.DropTable(name: "BankDepositAttachments");
        migrationBuilder.DropTable(name: "ReturnedChequeAttachments");
        migrationBuilder.DropTable(name: "ReturnedChequeCases");
        migrationBuilder.DropTable(name: "BankDepositBatches");

        migrationBuilder.DropForeignKey(
            name: "FK_CustomerPayment_LiquidityAccountEntries_LiquidityAccountEntryId",
            table: "CustomerPayment");
        migrationBuilder.DropForeignKey(
            name: "FK_CustomerPayment_LiquidityAccounts_LiquidityAccountId",
            table: "CustomerPayment");
        migrationBuilder.DropIndex(name: "IX_CustomerPayment_LiquidityAccountEntryId", table: "CustomerPayment");
        migrationBuilder.DropIndex(name: "IX_CustomerPayment_LiquidityAccountId", table: "CustomerPayment");
        migrationBuilder.DropIndex(name: "IX_CustomerPayment_TenantId_LiquidityAccountId_PaymentDate", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ChequeDrawerBank", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "LiquidityAccountEntryId", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "LiquidityAccountId", table: "CustomerPayment");

        migrationBuilder.DropTable(name: "LiquidityAccountEntries");
        migrationBuilder.DropTable(name: "LiquidityAccounts");

        migrationBuilder.DropForeignKey(
            name: "FK_FinanceSettings_Accounts_ReturnedChequeBankChargeAccountId",
            table: "FinanceSettings");
        migrationBuilder.DropIndex(
            name: "IX_FinanceSettings_ReturnedChequeBankChargeAccountId",
            table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "AutoPostBankDepositAfterApproval", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "BankDepositPolicy", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "BankStatementMatchDateToleranceDays", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ChequeClearingPeriodDays", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "DefaultReturnedChequeChargeTreatment", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "MaximumDepositDeductionAmount", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "MaximumDepositDeductionPercentage", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "RequireBankDepositPrimaryEvidence", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ReturnedChequeBankChargeAccountId", table: "FinanceSettings");
    }
}
