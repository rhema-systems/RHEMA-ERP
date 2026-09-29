using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InventorySupplierReturnGroupReadTests
{
    [Fact]
    public void Full_migration_SQL_generation_retains_goods_receipt_columns_inside_one_trigger_batch()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var operation = new ErpSystem.Data.Migrations.InventoryControlledWorkflowsAndAccounting()
            .UpOperations.OfType<SqlOperation>().Single(x => x.Sql.Contains(
                "CREATE OR ALTER TRIGGER dbo.TR_VendorInvoiceReceiptCostAllocations_Guard", StringComparison.Ordinal));
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(new[] { operation });
        commands.Should().ContainSingle("the EF GO parser must not interpret a GoodsReceipt column as a batch separator");
        commands[0].CommandText.Split("[GoodsReceiptNoteItemId]", StringSplitOptions.None).Should().HaveCount(3);
        commands[0].CommandText.Should().Contain("SourceFingerprint,IsDeleted FROM deleted")
            .And.Contain("SourceFingerprint,IsDeleted FROM inserted");
    }

    [Theory]
    [InlineData("ErpSystem.Core.Entities.Procurement.ProcurementReceiptCostBasis", "ConversionToBase", "decimal(18,8)")]
    [InlineData("ErpSystem.Core.Entities.Procurement.ProcurementReceiptCostBasis", "ExchangeRateToFunctional", "decimal(18,6)")]
    [InlineData("ErpSystem.Core.Entities.Procurement.ProcurementReceiptCostBasis", "PurchaseUnitCost", "decimal(18,6)")]
    [InlineData("ErpSystem.Core.Entities.Finance.InventorySupplierReturnAllocation", "ConversionToBase", "decimal(18,8)")]
    [InlineData("ErpSystem.Core.Entities.Finance.VendorInvoiceReceiptCostAllocation", "InvoiceExchangeRateToFunctional", "decimal(18,6)")]
    [InlineData("ErpSystem.Core.Entities.Finance.VendorInvoiceReceiptCostAllocation", "RevaluedReceiptBaseQuantity", "decimal(28,12)")]
    [InlineData("ErpSystem.Core.Entities.Finance.VendorInvoiceReceiptCostValuation", "AttributedReceiptBaseQuantity", "decimal(28,12)")]
    [InlineData("ErpSystem.Core.Entities.Finance.VendorInvoiceReceiptCostValuation", "ValueChange", "decimal(18,2)")]
    public void Sql_model_preserves_immutable_receipt_evidence_precision(string entityName, string property, string columnType)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        db.Model.FindEntityType(entityName)!.FindProperty(property)!.GetColumnType().Should().Be(columnType);
    }

    [Fact]
    public async Task Crediting_one_invoice_group_preserves_the_other_group_as_a_read_only_candidate()
    {
        using var db = Context();
        var (tenant, source, groups) = Seed(db);
        db.SupplierDebitNotes.Add(new SupplierDebitNote { TenantId = tenant, InventoryPurchaseReturnId = source.Id,
            InventorySupplierReturnAccountingGroupId = groups[0].Id, OriginalVendorInvoiceId = groups[0].OriginalVendorInvoiceId });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = Service(db, tenant);
        var remaining = await service.GetInventoryReturnCreditSourcesAsync(source.Id);
        remaining.Should().ContainSingle();
        remaining[0].AccountingGroupId.Should().Be(groups[1].Id);
        remaining[0].InvoiceId.Should().Be(groups[1].OriginalVendorInvoiceId!.Value);
        remaining[0].ReturnBaseQuantity.Should().Be(1);
        (await service.GetInventoryReturnCreditCandidatesAsync()).Should().ContainSingle().Which.ReturnId.Should().Be(source.Id);
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData("reversed-journal")]
    [InlineData("foreign-event")]
    [InlineData("wrong-source")]
    public async Task Invalid_dispatch_evidence_cannot_expose_invoice_groups(string corruption)
    {
        using var db = Context();
        var (tenant, source, groups) = Seed(db);
        var dispatch = db.JournalEntries.Local.Single(x => x.Id == groups[0].DispatchJournalEntryId);
        var posting = db.Set<FinancePostingEvent>().Local.Single();
        if (corruption == "reversed-journal") dispatch.IsReversed = true;
        if (corruption == "foreign-event") posting.TenantId = Guid.NewGuid();
        if (corruption == "wrong-source") posting.SourceDocumentId = Guid.NewGuid();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = Service(db, tenant);
        Func<Task> read = () => service.GetInventoryReturnCreditSourcesAsync(source.Id);
        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("RTV_ALLOCATED_DISPATCH_INVALID*");
        (await service.GetInventoryReturnCreditCandidatesAsync()).Should().BeEmpty();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Fully_linked_groups_disappear_from_candidates_without_deleting_their_history()
    {
        using var db = Context();
        var (tenant, source, groups) = Seed(db);
        foreach (var group in groups)
            db.SupplierDebitNotes.Add(new SupplierDebitNote { TenantId = tenant, InventoryPurchaseReturnId = source.Id,
                InventorySupplierReturnAccountingGroupId = group.Id, OriginalVendorInvoiceId = group.OriginalVendorInvoiceId });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await Service(db, tenant).GetInventoryReturnCreditSourcesAsync(source.Id)).Should().BeEmpty();
        (await Service(db, tenant).GetInventoryReturnCreditCandidatesAsync()).Should().BeEmpty();
        (await db.Set<InventorySupplierReturnAccountingGroup>().CountAsync()).Should().Be(2);
        (await db.Set<InventorySupplierReturnAllocation>().CountAsync()).Should().Be(2);
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"return-group-reads-{Guid.NewGuid():N}", options => options.EnableNullChecks(false)).Options);

    private static SupplierDebitNoteService Service(ApplicationDbContext db, Guid tenant)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.TenantId).Returns(tenant);
        current.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        current.SetupGet(x => x.IsAuthenticated).Returns(true);
        return new SupplierDebitNoteService(db, null!, current.Object, null!, null!, null!, null!, null!,
            NullLogger<SupplierDebitNoteService>.Instance);
    }

    private static (Guid Tenant, PurchaseReturn Return, InventorySupplierReturnAccountingGroup[] Groups) Seed(ApplicationDbContext db)
    {
        var tenant = Guid.NewGuid(); var supplier = Guid.NewGuid(); var po = Guid.NewGuid(); var item = Guid.NewGuid(); var book = Guid.NewGuid();
        var receipt = new GoodsReceiptNote { TenantId = tenant, SupplierId = supplier, PurchaseOrderId = po };
        var receiptLine = new GoodsReceiptNoteItem { TenantId = tenant, GoodsReceiptNote = receipt,
            GoodsReceiptNoteId = receipt.Id, PurchaseOrderItemId = Guid.NewGuid(), InventoryItemId = item };
        var source = new PurchaseReturn { TenantId = tenant, ReturnNumber = "RTV-TWO-INVOICES", SupplierId = supplier,
            SupplierName = "Canonical supplier", PurchaseOrderId = po, GoodsReceiptNoteId = receipt.Id,
            Status = "Shipped", ApprovalRequired = false, ShippedDate = DateTime.UtcNow, AccountingAllocationVersion = 1 };
        var line = new PurchaseReturnItem { TenantId = tenant, InventoryItemId = item, GoodsReceiptNoteItem = receiptLine,
            GoodsReceiptNoteItemId = receiptLine.Id, ReturnQuantity = 2, StockReversed = true, StockReversedAt = DateTime.UtcNow };
        source.Items.Add(line);
        var dispatch = new JournalEntry { TenantId = tenant, AccountingBookId = book, SourceDocumentType = "SupplierReturnDispatch", SourceDocumentId = source.Id,
            PostingStatus = "Posted" };
        var posting = new FinancePostingEvent { TenantId = tenant, AccountingBookId = book, SourceDocumentType = "SupplierReturnDispatch", SourceDocumentId = source.Id,
            PostingStatus = "Posted", JournalEntryId = dispatch.Id };
        db.AddRange(source, dispatch, posting, new InventoryMovement { TenantId = tenant, InventoryItemId = item,
            WarehouseId = source.WarehouseId, ReferenceType = ReferenceType.Return, ReferenceId = source.Id,
            MovementType = InventoryMovementType.SupplierReturn, Direction = MovementDirection.Out, Quantity = 2,
            TotalValue = 200, IsPosted = true, PostedAt = DateTime.UtcNow });
        var groups = new List<InventorySupplierReturnAccountingGroup>();
        for (var index = 0; index < 2; index++)
        {
            var invoice = new VendorInvoice { TenantId = tenant, BusinessPartnerId = supplier, InvoiceNumber = $"VI-GROUP-{index}",
                PurchaseOrderId = po, TotalAmount = 100, CurrencyCode = "GHS", Status = VendorInvoiceStatus.Approved };
            var journal = new JournalEntry { TenantId = tenant, AccountingBookId = book, SourceDocumentType = "VendorInvoice", SourceDocumentId = invoice.Id, PostingStatus = "Posted" };
            invoice.JournalEntryId = journal.Id;
            var group = new InventorySupplierReturnAccountingGroup { TenantId = tenant, InventoryPurchaseReturnId = source.Id,
                OriginalVendorInvoiceId = invoice.Id, DispatchJournalEntryId = dispatch.Id, DispatchPostingEventId = posting.Id,
                CarryingAmount = 100, FunctionalCurrency = "GHS" };
            groups.Add(group);
            db.AddRange(invoice, journal, group, new InventorySupplierReturnAllocation { TenantId = tenant,
                InventoryPurchaseReturnId = source.Id, InventoryPurchaseReturnItemId = line.Id, GoodsReceiptNoteItemId = receiptLine.Id,
                AccountingGroupId = group.Id, OriginalVendorInvoiceId = invoice.Id, BaseQuantity = 1, CarryingAmount = 100 });
        }
        return (tenant, source, groups.ToArray());
    }
}
