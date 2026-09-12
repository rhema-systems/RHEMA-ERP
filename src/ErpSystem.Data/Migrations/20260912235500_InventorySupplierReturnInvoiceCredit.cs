using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912235500_InventorySupplierReturnInvoiceCredit")]
public sealed class InventorySupplierReturnInvoiceCredit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("InventoryPurchaseReturnId", "SupplierDebitNotes", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReturnDispatchPostingEventId", "SupplierDebitNotes", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReturnDispatchJournalEntryId", "SupplierDebitNotes", nullable: true);
        migrationBuilder.AddColumn<decimal>("DirectInvoiceAppliedAmount", "SupplierDebitNotes", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<DateTime>("DirectInvoiceAppliedAt", "SupplierDebitNotes", nullable: true);
        migrationBuilder.AddColumn<Guid>("DirectInvoiceAppliedById", "SupplierDebitNotes", nullable: true);
        migrationBuilder.AddColumn<Guid>("InventoryPurchaseReturnItemId", "SupplierDebitNoteLineItems", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReturnToVendorClearingAccountId", "FinanceSettings", nullable: true);
        migrationBuilder.AddColumn<Guid>("PurchaseReturnVarianceAccountId", "FinanceSettings", nullable: true);
        migrationBuilder.CreateIndex("UX_SupplierDebitNotes_Tenant_InventoryReturn", "SupplierDebitNotes",
            new[] { "TenantId", "InventoryPurchaseReturnId" }, unique: true,
            filter: "[InventoryPurchaseReturnId] IS NOT NULL AND [IsDeleted] = 0");
        AddLink(migrationBuilder, "SupplierDebitNotes", "InventoryPurchaseReturnId", "PurchaseReturns");
        AddLink(migrationBuilder, "SupplierDebitNotes", "ReturnDispatchPostingEventId", "FinancePostingEvents");
        AddLink(migrationBuilder, "SupplierDebitNotes", "ReturnDispatchJournalEntryId", "JournalEntries");
        AddLink(migrationBuilder, "SupplierDebitNoteLineItems", "InventoryPurchaseReturnItemId", "PurchaseReturnItems");
        AddLink(migrationBuilder, "FinanceSettings", "ReturnToVendorClearingAccountId", "Accounts");
        AddLink(migrationBuilder, "FinanceSettings", "PurchaseReturnVarianceAccountId", "Accounts");
        migrationBuilder.CreateTable("InventorySupplierReturnPostings", columns: table => new
        {
            Id = table.Column<Guid>(nullable: false), TenantId = table.Column<Guid>(nullable: false),
            InventoryPurchaseReturnId = table.Column<Guid>(nullable: false), OriginalVendorInvoiceId = table.Column<Guid>(nullable: false),
            PostingEventId = table.Column<Guid>(nullable: false), JournalEntryId = table.Column<Guid>(nullable: false),
            ClearingAccountId = table.Column<Guid>(nullable: false), InventoryAccountId = table.Column<Guid>(nullable: false),
            CarryingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false), PostingDate = table.Column<DateTime>(nullable: false),
            CreatedAt = table.Column<DateTime>(nullable: false), UpdatedAt = table.Column<DateTime>(nullable: true),
            CreatedBy = table.Column<string>(nullable: true), UpdatedBy = table.Column<string>(nullable: true),
            CreatedById = table.Column<Guid>(nullable: true), LastModifiedById = table.Column<Guid>(nullable: true),
            IsDeleted = table.Column<bool>(nullable: false), DeletedAt = table.Column<DateTime>(nullable: true), DeletedBy = table.Column<string>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_InventorySupplierReturnPostings", x => x.Id));
        migrationBuilder.CreateIndex("IX_InventorySupplierReturnPostings_TenantId_InventoryPurchaseReturnId", "InventorySupplierReturnPostings",
            new[] { "TenantId", "InventoryPurchaseReturnId" }, unique: true);
        AddLink(migrationBuilder, "InventorySupplierReturnPostings", "InventoryPurchaseReturnId", "PurchaseReturns");
        AddLink(migrationBuilder, "InventorySupplierReturnPostings", "OriginalVendorInvoiceId", "VendorInvoice");
        AddLink(migrationBuilder, "InventorySupplierReturnPostings", "PostingEventId", "FinancePostingEvents");
        AddLink(migrationBuilder, "InventorySupplierReturnPostings", "JournalEntryId", "JournalEntries");
        AddLink(migrationBuilder, "InventorySupplierReturnPostings", "ClearingAccountId", "Accounts");
        AddLink(migrationBuilder, "InventorySupplierReturnPostings", "InventoryAccountId", "Accounts");
        migrationBuilder.AddForeignKey("FK_InventorySupplierReturnPostings_Tenants_TenantId", "InventorySupplierReturnPostings", "TenantId", "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql(DispatchGuard);
        migrationBuilder.Sql(CreditGuard);
    }

    private static void AddLink(MigrationBuilder migrationBuilder, string table, string column, string principal)
    {
        migrationBuilder.CreateIndex($"IX_{table}_{column}", table, column);
        migrationBuilder.AddForeignKey($"FK_{table}_{principal}_{column}", table, column, principal, principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("THROW 51998, 'Supplier return Finance postings and invoice applications require a reviewed forward correction, not an automatic downgrade.', 1;");

    public const string DispatchGuard = """
CREATE OR ALTER TRIGGER [dbo].[TR_InventorySupplierReturnPostings_Immutable]
ON [dbo].[InventorySupplierReturnPostings] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted) THROW 51980, 'RTV_FINANCE_DISPATCH_IMMUTABLE', 1;
    IF EXISTS (SELECT 1 FROM inserted i
        LEFT JOIN dbo.PurchaseReturns r ON r.Id=i.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
        LEFT JOIN dbo.VendorInvoice v ON v.Id=i.OriginalVendorInvoiceId AND v.TenantId=i.TenantId
        LEFT JOIN dbo.ApSupplierIdentityLinks s ON s.TenantId=i.TenantId AND s.BusinessPartnerId=r.SupplierId AND s.SupplierId=v.SupplierId AND s.IsDeleted=0
        LEFT JOIN dbo.FinancePostingEvents p ON p.Id=i.PostingEventId AND p.TenantId=i.TenantId
        LEFT JOIN dbo.JournalEntries j ON j.Id=i.JournalEntryId AND j.TenantId=i.TenantId
        LEFT JOIN dbo.Accounts a ON a.Id=i.ClearingAccountId AND a.TenantId=i.TenantId
        LEFT JOIN dbo.Accounts b ON b.Id=i.InventoryAccountId AND b.TenantId=i.TenantId
        WHERE r.Id IS NULL OR r.Status NOT IN ('Shipped','Acknowledged') OR r.ShippedDate IS NULL OR r.IsDeleted=1
          OR v.Id IS NULL OR v.IsDeleted=1 OR v.JournalEntryId IS NULL OR v.PurchaseOrderId<>r.PurchaseOrderId
          OR s.Id IS NULL OR p.Id IS NULL OR j.Id IS NULL OR a.Id IS NULL OR b.Id IS NULL
          OR p.PostingStatus<>'Posted' OR p.PostedAt IS NULL OR p.JournalEntryId IS NULL OR p.JournalEntryId<>i.JournalEntryId
          OR p.SourceDocumentId<>r.Id OR p.SourceDocumentType<>'InventorySupplierReturnDispatch'
          OR j.PostingStatus<>'Posted' OR j.IsReversed=1 OR j.SourceDocumentId IS NULL OR j.SourceDocumentId<>r.Id
          OR j.SourceDocumentType IS NULL OR j.SourceDocumentType<>'InventorySupplierReturnDispatch'
          OR i.CarryingAmount<=0 OR i.ClearingAccountId=i.InventoryAccountId OR i.IsDeleted=1)
        THROW 51981, 'RTV_FINANCE_DISPATCH_LINEAGE_INVALID', 1;
END;
""";

    public const string CreditGuard = """
CREATE OR ALTER TRIGGER [dbo].[TR_SupplierDebitNotes_InventoryReturnCreditGuard]
ON [dbo].[SupplierDebitNotes] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id
        WHERE d.InventoryPurchaseReturnId IS NOT NULL AND (i.Id IS NULL OR i.InventoryPurchaseReturnId IS NULL OR
            i.InventoryPurchaseReturnId<>d.InventoryPurchaseReturnId OR i.TenantId<>d.TenantId OR i.VendorId<>d.VendorId OR
            ISNULL(i.OriginalVendorInvoiceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.OriginalVendorInvoiceId,'00000000-0000-0000-0000-000000000000') OR
            (d.DirectInvoiceAppliedAt IS NOT NULL AND (i.DirectInvoiceAppliedAmount<>d.DirectInvoiceAppliedAmount OR
                i.DirectInvoiceAppliedAt IS NULL OR i.DirectInvoiceAppliedAt<>d.DirectInvoiceAppliedAt OR i.IsDeleted<>d.IsDeleted))))
        THROW 51982, 'RTV_CREDIT_LINK_OR_APPLICATION_IMMUTABLE', 1;
    IF EXISTS (SELECT 1 FROM inserted i
        LEFT JOIN dbo.PurchaseReturns r ON r.Id=i.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
        LEFT JOIN dbo.VendorInvoice v ON v.Id=i.OriginalVendorInvoiceId AND v.TenantId=i.TenantId
        WHERE i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL OR r.IsDeleted=1 OR r.SupplierId<>i.VendorId OR
            r.Status NOT IN ('Shipped','Acknowledged') OR v.Id IS NULL OR v.IsDeleted=1 OR v.SupplierId<>i.SupplierId OR
            v.PurchaseOrderId<>r.PurchaseOrderId OR i.SupplierReturnId IS NOT NULL))
        THROW 51983, 'RTV_CREDIT_SOURCE_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted i WHERE i.DirectInvoiceAppliedAmount<0 OR
        (i.DirectInvoiceAppliedAmount<>0 AND (i.InventoryPurchaseReturnId IS NULL OR i.OriginalVendorInvoiceId IS NULL OR
            i.Status<>2 OR i.PostingEventId IS NULL OR i.JournalEntryId IS NULL OR i.ReturnDispatchPostingEventId IS NULL OR
            i.ReturnDispatchJournalEntryId IS NULL OR i.DirectInvoiceAppliedAt IS NULL OR i.DirectInvoiceAppliedAmount<>i.TotalAmount)))
        THROW 51984, 'RTV_CREDIT_APPLICATION_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted i
        LEFT JOIN dbo.FinancePostingEvents p ON p.Id=i.PostingEventId AND p.TenantId=i.TenantId AND p.IsDeleted=0
        LEFT JOIN dbo.JournalEntries j ON j.Id=i.JournalEntryId AND j.TenantId=i.TenantId AND j.IsDeleted=0
        LEFT JOIN dbo.InventorySupplierReturnPostings d ON d.TenantId=i.TenantId AND d.InventoryPurchaseReturnId=i.InventoryPurchaseReturnId
        WHERE i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL OR j.Id IS NULL OR d.Id IS NULL OR
            p.PostingStatus<>'Posted' OR p.SourceDocumentType<>'SupplierDebitNote' OR p.SourceDocumentId<>i.Id OR
            p.JournalEntryId IS NULL OR p.JournalEntryId<>j.Id OR j.PostingStatus<>'Posted' OR j.IsReversed=1 OR
            d.PostingEventId<>i.ReturnDispatchPostingEventId OR d.JournalEntryId<>i.ReturnDispatchJournalEntryId))
        THROW 51985, 'RTV_CREDIT_POSTING_EVIDENCE_INVALID', 1;
END;
""";
}
