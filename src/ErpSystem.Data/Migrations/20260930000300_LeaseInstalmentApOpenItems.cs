using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260930000300_LeaseInstalmentApOpenItems")]
public sealed class LeaseInstalmentApOpenItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("AccountingBookId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("AccountingBookCode", "LeaseContracts", "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>("FunctionalCurrencyCode", "LeaseContracts", "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<Guid>("InterestExpenseAccountId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("LeaseLiabilityAccountId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("RecognitionJournalEntryId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("RecognitionPostingEventId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("RouAssetAccountId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ActivationWorkflowInstanceId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ActivationSubmittedByUserId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("ActivationSubmittedAtUtc", "LeaseContracts", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("ActivationApprovedByUserId", "LeaseContracts", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("ActivationApprovedAtUtc", "LeaseContracts", "datetime2", nullable: true);

        migrationBuilder.AddColumn<Guid>("LeaseScheduleLineId", "VendorInvoice", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReplacesLeaseVendorInvoiceId", "VendorInvoice", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("LeaseAccountingBookId", "VendorInvoice", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("LeaseAccountingBookCode", "VendorInvoice", "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>("LeaseFunctionalCurrencyCode", "VendorInvoice", "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<int>("LeaseComponent", "VendorInvoiceLineItem", "int", nullable: true);
        migrationBuilder.AddColumn<Guid>("AccountingBookId", "VendorPayment", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("AccountingBookCode", "VendorPayment", "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>("FunctionalCurrencyCode", "VendorPayment", "nvarchar(3)", maxLength: 3, nullable: true);

        migrationBuilder.AddUniqueConstraint(
            "AK_LeaseScheduleLines_TenantId_Id", "LeaseScheduleLines", new[] { "TenantId", "Id" });
        migrationBuilder.AddUniqueConstraint(
            "AK_VendorInvoice_TenantId_Id", "VendorInvoice", new[] { "TenantId", "Id" });
        migrationBuilder.AddUniqueConstraint(
            "AK_FinancePostingEvents_TenantId_Id_AccountingBookId", "FinancePostingEvents",
            new[] { "TenantId", "Id", "AccountingBookId" });
        migrationBuilder.AddUniqueConstraint(
            "AK_WorkflowInstances_TenantId_Id", "WorkflowInstances", new[] { "TenantId", "Id" });

        migrationBuilder.AddCheckConstraint(
            "CK_VendorInvoice_LeaseSourceCoherent", "VendorInvoice",
            "([LeaseScheduleLineId] IS NULL AND [ReplacesLeaseVendorInvoiceId] IS NULL AND [LeaseAccountingBookId] IS NULL AND [LeaseAccountingBookCode] IS NULL AND [LeaseFunctionalCurrencyCode] IS NULL) OR ([LeaseScheduleLineId] IS NOT NULL AND [LeaseAccountingBookId] IS NOT NULL AND LEN([LeaseAccountingBookCode]) BETWEEN 1 AND 20 AND LEN([LeaseFunctionalCurrencyCode]) = 3 AND [IsOpeningBalance] = 0 AND [PurchaseOrderId] IS NULL AND [AcceptedSupplyKind] IS NULL AND [AutoInvoiceRequestId] IS NULL AND [EstateAcquisitionId] IS NULL)");
        migrationBuilder.AddCheckConstraint(
            "CK_VendorInvoiceLineItem_LeaseComponent", "VendorInvoiceLineItem",
            "[LeaseComponent] IS NULL OR [LeaseComponent] BETWEEN 1 AND 2");

        migrationBuilder.CreateIndex(
            "IX_LeaseContracts_TenantId_AccountingBookId", "LeaseContracts", new[] { "TenantId", "AccountingBookId" });
        migrationBuilder.CreateIndex(
            "IX_LeaseContracts_TenantId_ActivationWorkflowInstanceId", "LeaseContracts",
            new[] { "TenantId", "ActivationWorkflowInstanceId" });
        migrationBuilder.CreateIndex("IX_LeaseContracts_InterestExpenseAccountId", "LeaseContracts", "InterestExpenseAccountId");
        migrationBuilder.CreateIndex("IX_LeaseContracts_LeaseLiabilityAccountId", "LeaseContracts", "LeaseLiabilityAccountId");
        migrationBuilder.CreateIndex(
            "IX_LeaseContracts_TenantId_RecognitionJournalEntryId_AccountingBookId", "LeaseContracts",
            new[] { "TenantId", "RecognitionJournalEntryId", "AccountingBookId" });
        migrationBuilder.CreateIndex(
            "IX_LeaseContracts_TenantId_RecognitionPostingEventId_AccountingBookId", "LeaseContracts",
            new[] { "TenantId", "RecognitionPostingEventId", "AccountingBookId" });
        migrationBuilder.CreateIndex("IX_LeaseContracts_RouAssetAccountId", "LeaseContracts", "RouAssetAccountId");
        migrationBuilder.CreateIndex(
            "IX_VendorInvoice_TenantId_LeaseAccountingBookId", "VendorInvoice", new[] { "TenantId", "LeaseAccountingBookId" });
        migrationBuilder.CreateIndex(
            "IX_VendorInvoice_TenantId_LeaseScheduleLineId", "VendorInvoice", new[] { "TenantId", "LeaseScheduleLineId" },
            unique: true, filter: "[LeaseScheduleLineId] IS NOT NULL AND [IsDeleted] = 0 AND [Status] <> 7");
        migrationBuilder.CreateIndex(
            "IX_VendorInvoice_TenantId_ReplacesLeaseVendorInvoiceId", "VendorInvoice",
            new[] { "TenantId", "ReplacesLeaseVendorInvoiceId" }, unique: true,
            filter: "[ReplacesLeaseVendorInvoiceId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            "IX_VendorPayment_TenantId_AccountingBookId", "VendorPayment", new[] { "TenantId", "AccountingBookId" });

        migrationBuilder.AddForeignKey(
            name: "FK_LeaseContracts_AccountingBooks_TenantId_AccountingBookId",
            table: "LeaseContracts", columns: new[] { "TenantId", "AccountingBookId" },
            principalTable: "AccountingBooks", principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_LeaseContracts_WorkflowInstances_TenantId_ActivationWorkflowInstanceId",
            table: "LeaseContracts", columns: new[] { "TenantId", "ActivationWorkflowInstanceId" },
            principalTable: "WorkflowInstances", principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            "FK_LeaseContracts_Accounts_InterestExpenseAccountId", "LeaseContracts", "InterestExpenseAccountId",
            "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            "FK_LeaseContracts_Accounts_LeaseLiabilityAccountId", "LeaseContracts", "LeaseLiabilityAccountId",
            "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            "FK_LeaseContracts_Accounts_RouAssetAccountId", "LeaseContracts", "RouAssetAccountId",
            "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_LeaseContracts_FinancePostingEvents_TenantId_RecognitionPostingEventId_AccountingBookId",
            table: "LeaseContracts", columns: new[] { "TenantId", "RecognitionPostingEventId", "AccountingBookId" },
            principalTable: "FinancePostingEvents", principalColumns: new[] { "TenantId", "Id", "AccountingBookId" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_LeaseContracts_JournalEntries_TenantId_RecognitionJournalEntryId_AccountingBookId",
            table: "LeaseContracts", columns: new[] { "TenantId", "RecognitionJournalEntryId", "AccountingBookId" },
            principalTable: "JournalEntries", principalColumns: new[] { "TenantId", "Id", "AccountingBookId" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoice_AccountingBooks_TenantId_LeaseAccountingBookId",
            table: "VendorInvoice", columns: new[] { "TenantId", "LeaseAccountingBookId" },
            principalTable: "AccountingBooks", principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoice_LeaseScheduleLines_TenantId_LeaseScheduleLineId",
            table: "VendorInvoice", columns: new[] { "TenantId", "LeaseScheduleLineId" },
            principalTable: "LeaseScheduleLines", principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoice_VendorInvoice_TenantId_ReplacesLeaseVendorInvoiceId",
            table: "VendorInvoice", columns: new[] { "TenantId", "ReplacesLeaseVendorInvoiceId" },
            principalTable: "VendorInvoice", principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_VendorPayment_AccountingBooks_TenantId_AccountingBookId",
            table: "VendorPayment", columns: new[] { "TenantId", "AccountingBookId" },
            principalTable: "AccountingBooks", principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM [LeaseContracts]
                WHERE [AccountingBookId] IS NOT NULL OR [AccountingBookCode] IS NOT NULL OR
                      [FunctionalCurrencyCode] IS NOT NULL OR [InterestExpenseAccountId] IS NOT NULL OR
                      [LeaseLiabilityAccountId] IS NOT NULL OR [RouAssetAccountId] IS NOT NULL OR
                      [RecognitionJournalEntryId] IS NOT NULL OR [RecognitionPostingEventId] IS NOT NULL OR
                      [ActivationWorkflowInstanceId] IS NOT NULL OR [ActivationSubmittedByUserId] IS NOT NULL OR
                      [ActivationSubmittedAtUtc] IS NOT NULL OR [ActivationApprovedByUserId] IS NOT NULL OR
                      [ActivationApprovedAtUtc] IS NOT NULL
            ) OR EXISTS (
                SELECT 1 FROM [VendorInvoice]
                WHERE [LeaseScheduleLineId] IS NOT NULL OR [ReplacesLeaseVendorInvoiceId] IS NOT NULL OR
                      [LeaseAccountingBookId] IS NOT NULL OR [LeaseAccountingBookCode] IS NOT NULL OR
                      [LeaseFunctionalCurrencyCode] IS NOT NULL
            ) OR EXISTS (
                SELECT 1 FROM [VendorInvoiceLineItem] WHERE [LeaseComponent] IS NOT NULL
            ) OR EXISTS (
                SELECT 1 FROM [VendorPayment]
                WHERE [AccountingBookId] IS NOT NULL OR [AccountingBookCode] IS NOT NULL OR
                      [FunctionalCurrencyCode] IS NOT NULL
            ) OR EXISTS (
                SELECT 1 FROM [FixedAssets]
                WHERE [SourceDocumentType] = N'LeaseRecognition' OR
                      [PostingEventId] IS NOT NULL AND [SourceDocumentType] = N'LeaseRecognition' OR
                      [JournalEntryId] IS NOT NULL AND [SourceDocumentType] = N'LeaseRecognition'
            ) OR EXISTS (
                SELECT 1 FROM [FixedAssetBookValues] WHERE [SourceDocumentType] = N'LeaseRecognition'
            ) OR EXISTS (
                SELECT 1 FROM [AssetTransactions] WHERE [TransactionType] = N'LeaseRecognition'
            )
                THROW 51000, 'Cannot roll back lease/AP governance while retained source, authority, component, replacement, workflow, posting, journal, or ROU evidence exists (including soft-deleted rows).', 1;
            """);
        migrationBuilder.DropForeignKey("FK_LeaseContracts_WorkflowInstances_TenantId_ActivationWorkflowInstanceId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_LeaseContracts_AccountingBooks_TenantId_AccountingBookId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_LeaseContracts_Accounts_InterestExpenseAccountId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_LeaseContracts_Accounts_LeaseLiabilityAccountId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_LeaseContracts_Accounts_RouAssetAccountId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_LeaseContracts_FinancePostingEvents_TenantId_RecognitionPostingEventId_AccountingBookId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_LeaseContracts_JournalEntries_TenantId_RecognitionJournalEntryId_AccountingBookId", "LeaseContracts");
        migrationBuilder.DropForeignKey("FK_VendorInvoice_AccountingBooks_TenantId_LeaseAccountingBookId", "VendorInvoice");
        migrationBuilder.DropForeignKey("FK_VendorInvoice_LeaseScheduleLines_TenantId_LeaseScheduleLineId", "VendorInvoice");
        migrationBuilder.DropForeignKey("FK_VendorInvoice_VendorInvoice_TenantId_ReplacesLeaseVendorInvoiceId", "VendorInvoice");
        migrationBuilder.DropForeignKey("FK_VendorPayment_AccountingBooks_TenantId_AccountingBookId", "VendorPayment");
        migrationBuilder.DropIndex("IX_LeaseContracts_TenantId_AccountingBookId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_LeaseContracts_TenantId_ActivationWorkflowInstanceId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_LeaseContracts_InterestExpenseAccountId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_LeaseContracts_LeaseLiabilityAccountId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_LeaseContracts_TenantId_RecognitionJournalEntryId_AccountingBookId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_LeaseContracts_TenantId_RecognitionPostingEventId_AccountingBookId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_LeaseContracts_RouAssetAccountId", "LeaseContracts");
        migrationBuilder.DropIndex("IX_VendorInvoice_TenantId_LeaseAccountingBookId", "VendorInvoice");
        migrationBuilder.DropIndex("IX_VendorInvoice_TenantId_LeaseScheduleLineId", "VendorInvoice");
        migrationBuilder.DropIndex("IX_VendorInvoice_TenantId_ReplacesLeaseVendorInvoiceId", "VendorInvoice");
        migrationBuilder.DropIndex("IX_VendorPayment_TenantId_AccountingBookId", "VendorPayment");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoice_LeaseSourceCoherent", "VendorInvoice");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoiceLineItem_LeaseComponent", "VendorInvoiceLineItem");
        migrationBuilder.DropUniqueConstraint("AK_LeaseScheduleLines_TenantId_Id", "LeaseScheduleLines");
        migrationBuilder.DropUniqueConstraint("AK_VendorInvoice_TenantId_Id", "VendorInvoice");
        migrationBuilder.DropUniqueConstraint("AK_FinancePostingEvents_TenantId_Id_AccountingBookId", "FinancePostingEvents");
        migrationBuilder.DropUniqueConstraint("AK_WorkflowInstances_TenantId_Id", "WorkflowInstances");
        migrationBuilder.DropColumn("AccountingBookId", "LeaseContracts");
        migrationBuilder.DropColumn("AccountingBookCode", "LeaseContracts");
        migrationBuilder.DropColumn("FunctionalCurrencyCode", "LeaseContracts");
        migrationBuilder.DropColumn("InterestExpenseAccountId", "LeaseContracts");
        migrationBuilder.DropColumn("LeaseLiabilityAccountId", "LeaseContracts");
        migrationBuilder.DropColumn("RecognitionJournalEntryId", "LeaseContracts");
        migrationBuilder.DropColumn("RecognitionPostingEventId", "LeaseContracts");
        migrationBuilder.DropColumn("RouAssetAccountId", "LeaseContracts");
        migrationBuilder.DropColumn("ActivationWorkflowInstanceId", "LeaseContracts");
        migrationBuilder.DropColumn("ActivationSubmittedByUserId", "LeaseContracts");
        migrationBuilder.DropColumn("ActivationSubmittedAtUtc", "LeaseContracts");
        migrationBuilder.DropColumn("ActivationApprovedByUserId", "LeaseContracts");
        migrationBuilder.DropColumn("ActivationApprovedAtUtc", "LeaseContracts");
        migrationBuilder.DropColumn("LeaseScheduleLineId", "VendorInvoice");
        migrationBuilder.DropColumn("ReplacesLeaseVendorInvoiceId", "VendorInvoice");
        migrationBuilder.DropColumn("LeaseAccountingBookId", "VendorInvoice");
        migrationBuilder.DropColumn("LeaseAccountingBookCode", "VendorInvoice");
        migrationBuilder.DropColumn("LeaseFunctionalCurrencyCode", "VendorInvoice");
        migrationBuilder.DropColumn("LeaseComponent", "VendorInvoiceLineItem");
        migrationBuilder.DropColumn("AccountingBookId", "VendorPayment");
        migrationBuilder.DropColumn("AccountingBookCode", "VendorPayment");
        migrationBuilder.DropColumn("FunctionalCurrencyCode", "VendorPayment");
    }
}
