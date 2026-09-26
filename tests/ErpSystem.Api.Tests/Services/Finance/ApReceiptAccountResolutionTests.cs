using System.Reflection;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApReceiptAccountResolutionTests
{
    [Fact]
    public async Task Mixed_service_line_does_not_guess_a_stock_receipt_account()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, _, _) = await fixture.SeedAsync(false);
        var invoiceLine = invoice.LineItems.Single(value => value.Id == firstLine);
        var poLine = await fixture.Context.Set<PurchaseOrderItem>().SingleAsync(value => value.Id == invoiceLine.PurchaseOrderItemId);
        poLine.LineType = ItemType.Service;
        poLine.InventoryItemId = null;
        await fixture.Context.SaveChangesAsync();

        var act = () => fixture.ContextsAsync(invoice);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a stock item*no account has been guessed*");
    }

    [Fact]
    public async Task Each_invoice_item_uses_its_original_receipt_accrual_not_current_global()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, secondLine, firstAccount, secondAccount) = await fixture.SeedAsync(false);
        var contexts = await fixture.ContextsAsync(invoice);
        contexts.Should().Contain(value => value.SourceLineId == firstLine && value.AccountId == firstAccount);
        contexts.Should().Contain(value => value.SourceLineId == secondLine && value.AccountId == secondAccount);
    }

    [Fact]
    public async Task Legacy_receipt_without_item_lineage_uses_its_recorded_single_account()
    {
        await using var fixture = new Fixture();
        var (invoice, _, _, firstAccount, _) = await fixture.SeedAsync(true);
        var contexts = await fixture.ContextsAsync(invoice);
        contexts.Should().HaveCount(2).And.OnlyContain(value => value.AccountId == firstAccount);
    }

    [Fact]
    public async Task Foreign_receipt_journal_cannot_supply_an_invoice_account()
    {
        await using var fixture = new Fixture();
        var (invoice, _, _, firstAccount, _) = await fixture.SeedAsync(true);
        fixture.Context.FinancePostingEvents.Add(new FinancePostingEvent { TenantId = Guid.NewGuid(),
            SourceDocumentType = "ProcurementPurchaseOrderReceipt", SourceDocumentId = fixture.ReceiptId,
            PostingAction = "PostAcceptedInventoryReceipt", PostingStatus = "Posted", JournalEntryId = Guid.NewGuid() });
        await fixture.Context.SaveChangesAsync();
        (await fixture.ContextsAsync(invoice)).Should().OnlyContain(value => value.AccountId == firstAccount);
    }

    [Fact]
    public async Task Legacy_and_new_receipts_both_contribute_their_original_item_accounts()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, oldAccount, _) = await fixture.SeedAsync(true, movements: true);
        var item = invoice.LineItems.Single(value => value.Id == firstLine).InventoryItemId!.Value;
        var valuation = await fixture.Context.InventoryMovements.SingleAsync(value => value.InventoryItemId == item);
        valuation.TotalValue = 100m;
        valuation.VarianceAmount = 20m;
        var newAccount = Guid.NewGuid();
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, item, newAccount, 100m);
        var shares = (await fixture.SharesAsync(invoice))[firstLine];
        shares.Should().Contain(value => value.Account == oldAccount && value.Weight == 120m);
        shares.Should().Contain(value => value.Account == newAccount && value.Weight == 100m);
    }

    [Fact]
    public async Task A_second_partial_invoice_does_not_reclear_the_first_receipt()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, oldAccount, _) = await fixture.SeedAsync(false);
        var sourceLine = invoice.LineItems.Single(value => value.Id == firstLine);
        await fixture.AddPostedInvoiceAsync(invoice, sourceLine, oldAccount, 120m);
        var newAccount = Guid.NewGuid();
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, sourceLine.InventoryItemId!.Value, newAccount, 100m);
        (await fixture.SharesAsync(invoice))[firstLine].Should().ContainSingle(value => value.Account == newAccount && value.Weight == 100m);
    }

    [Fact]
    public async Task Partially_consumed_receipt_weights_use_only_the_remaining_balance()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, oldAccount, _) = await fixture.SeedAsync(false);
        var sourceLine = invoice.LineItems.Single(value => value.Id == firstLine);
        await fixture.AddPostedInvoiceAsync(invoice, sourceLine, oldAccount, 60m);
        var newAccount = Guid.NewGuid();
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, sourceLine.InventoryItemId!.Value, newAccount, 100m);
        var shares = (await fixture.SharesAsync(invoice))[firstLine];
        shares.Should().Contain(value => value.Account == oldAccount && value.Weight == 60m);
        shares.Should().Contain(value => value.Account == newAccount && value.Weight == 100m);
    }

    [Fact]
    public async Task Zero_net_lines_do_not_invent_receipt_accounts_or_dimension_contexts()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, secondLine, _, _) = await fixture.SeedAsync(false);
        invoice.LineItems.Single(value => value.Id == secondLine).UnitPrice = 0m;
        (await fixture.ContextsAsync(invoice)).Should().ContainSingle(value => value.SourceLineId == firstLine);
    }

    [Fact]
    public async Task Multiple_account_targets_remain_on_one_dimension_source_identity()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, original, _) = await fixture.SeedAsync(false);
        var additional = Guid.NewGuid();
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, invoice.LineItems.Single(value => value.Id == firstLine).InventoryItemId!.Value, additional, 100m);
        var context = (await fixture.ContextsAsync(invoice)).Single(value => value.SourceLineId == firstLine);
        new[] { context.AccountId }.Concat(context.AdditionalAccountIds ?? Array.Empty<Guid>()).Should().BeEquivalentTo(new[] { original, additional });
    }

    [Theory]
    [InlineData("reversed")]
    [InlineData("voided")]
    [InlineData("foreign")]
    public async Task Noncurrent_clearings_do_not_consume_this_tenant_receipt(string state)
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, account, _) = await fixture.SeedAsync(false);
        await fixture.AddPostedInvoiceAsync(invoice, invoice.LineItems.Single(value => value.Id == firstLine), account, 120m, state);
        (await fixture.SharesAsync(invoice))[firstLine].Should().ContainSingle(value => value.Account == account && value.Weight == 120m);
    }

    [Fact]
    public async Task Posted_invoice_history_uses_its_own_journal_after_further_receipts()
    {
        await using var fixture = new Fixture();
        var (draft, lineId, _, account, _) = await fixture.SeedAsync(false);
        var sourceLine = draft.LineItems.Single(value => value.Id == lineId);
        var posted = await fixture.AddPostedInvoiceAsync(draft, sourceLine, account, 120m);
        await fixture.AddReceiptAsync(draft.PurchaseOrderId!.Value, sourceLine.InventoryItemId!.Value, Guid.NewGuid(), 100m);
        (await fixture.SharesAsync(posted))[posted.LineItems.Single().Id].Should().ContainSingle(value => value.Account == account && value.Weight == 120m);
    }

    [Fact]
    public async Task Mixed_legacy_accounts_without_movements_are_not_guessed()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, _, _) = await fixture.SeedAsync(true);
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, invoice.LineItems.Single(value => value.Id == firstLine).InventoryItemId!.Value, Guid.NewGuid(), 100m);
        var action = () => fixture.SharesAsync(invoice);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Legacy receipt movements*");
    }

    [Fact]
    public async Task Mixed_receipt_invoice_request_balances_and_clears_both_original_accounts()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, original, _) = await fixture.SeedAsync(true, movements: true);
        var line = invoice.LineItems.Single(value => value.Id == firstLine);
        var additional = Guid.NewGuid();
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, line.InventoryItemId!.Value, additional, 100m);
        line.UnitPrice = 220m;
        await fixture.PreparePostingAsync(invoice);
        var request = await fixture.PostingRequestAsync(invoice);
        var clearing = request.Lines.Where(value => value.TransactionTag == "AP-GRV").ToList();
        clearing.Where(value => value.AccountId == original).Sum(value => value.DebitAmount).Should().Be(200m);
        clearing.Where(value => value.AccountId == additional).Sum(value => value.DebitAmount).Should().Be(100m);
        request.Lines.Sum(value => value.DebitAmount).Should().Be(request.Lines.Sum(value => value.CreditAmount));
        request.Lines.Should().OnlyContain(value => value.DebitAmount >= 0m && value.CreditAmount >= 0m);
    }

    [Fact]
    public async Task Tiny_invoice_split_over_four_accounts_never_produces_negative_debits()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, _, _) = await fixture.SeedAsync(false);
        var line = invoice.LineItems.Single(value => value.Id == firstLine);
        invoice.LineItems = new List<VendorInvoiceLineItem> { line };
        line.UnitPrice = 0.02m;
        for (var index = 0; index < 3; index++)
            await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, line.InventoryItemId!.Value, Guid.NewGuid(), 120m);
        await fixture.PreparePostingAsync(invoice);
        var request = await fixture.PostingRequestAsync(invoice);
        request.Lines.Where(value => value.TransactionTag == "AP-GRV").Sum(value => value.DebitAmount).Should().Be(0.02m);
        request.Lines.Should().OnlyContain(value => value.DebitAmount >= 0m && value.CreditAmount >= 0m);
    }

    [Fact]
    public async Task Foreign_currency_split_difference_is_explicit_and_does_not_return_invalid_posting()
    {
        await using var fixture = new Fixture();
        var (invoice, firstLine, _, _, _) = await fixture.SeedAsync(false);
        var line = invoice.LineItems.Single(value => value.Id == firstLine);
        invoice.LineItems = new List<VendorInvoiceLineItem> { line };
        line.UnitPrice = 0.02m;
        await fixture.AddReceiptAsync(invoice.PurchaseOrderId!.Value, line.InventoryItemId!.Value, Guid.NewGuid(), 120m);
        await fixture.PreparePostingAsync(invoice);
        invoice.CurrencyCode = "USD";
        invoice.ExchangeRate = 1.5m;
        var action = () => fixture.PostingRequestAsync(invoice);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*FX rounding difference of 0.01 GHS*reviewed FX rounding policy*");
        (await fixture.Context.JournalEntries.CountAsync()).Should().Be(2, "a rejected request must not post a journal");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid ReceiptId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        private readonly VendorInvoiceService service;
        public Fixture()
        {
            var current = new Mock<ICurrentUserService>(); current.SetupGet(value => value.TenantId).Returns(Tenant);
            service = new VendorInvoiceService(new UnitOfWork(Context), current.Object, Mock.Of<IInventoryValuationService>(),
                NullLogger<VendorInvoiceService>.Instance, Mock.Of<IDocumentNumberingService>(), Mock.Of<IWorkflowService>(), taxEngine: Mock.Of<ITaxCalculationEngine>());
        }
        public async Task<(VendorInvoice, Guid, Guid, Guid, Guid)> SeedAsync(bool legacy, bool movements = false)
        {
            var order = Guid.NewGuid(); var firstItem = Guid.NewGuid(); var secondItem = Guid.NewGuid();
            var firstAccount = Guid.NewGuid(); var secondAccount = legacy ? firstAccount : Guid.NewGuid();
            var poLine1 = new PurchaseOrderItem { TenantId = Tenant, PurchaseOrderId = order, InventoryItemId = firstItem };
            var poLine2 = new PurchaseOrderItem { TenantId = Tenant, PurchaseOrderId = order, InventoryItemId = secondItem };
            Context.PurchaseOrderItems.AddRange(poLine1, poLine2);
            Context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt { Id = ReceiptId, TenantId = Tenant, PurchaseOrderId = order, ReceiptNumber = "REC-ORIGINAL" });
            var journal = Guid.NewGuid();
            Context.JournalEntries.Add(new JournalEntry { Id = journal, TenantId = Tenant, JournalEntryNumber = "JE-RECEIPT", PostingStatus = "Posted" });
            Context.FinancePostingEvents.Add(new FinancePostingEvent { TenantId = Tenant,
                SourceDocumentType = "ProcurementPurchaseOrderReceipt", SourceDocumentId = ReceiptId,
                PostingAction = "PostAcceptedInventoryReceipt", PostingStatus = "Posted", JournalEntryId = journal });
            Context.AccountTransactions.AddRange(
                new AccountTransaction { TenantId = Tenant, JournalEntryId = journal, AccountId = firstAccount,
                    SourceDocumentLineId = legacy ? null : firstItem, CreditAmount = 120m, TransactionTag = "INV-RECEIPT-GRV-ACCRUAL" },
                new AccountTransaction { TenantId = Tenant, JournalEntryId = journal, AccountId = secondAccount,
                    SourceDocumentLineId = legacy ? null : secondItem, CreditAmount = 80m, TransactionTag = "INV-RECEIPT-GRV-ACCRUAL" });
            if (movements)
                Context.InventoryMovements.AddRange(
                    new InventoryMovement { TenantId = Tenant, ReferenceId = ReceiptId, InventoryItemId = firstItem, MovementType = InventoryMovementType.PurchaseReceipt, IsPosted = true, Quantity = 1, TotalValue = 120m },
                    new InventoryMovement { TenantId = Tenant, ReferenceId = ReceiptId, InventoryItemId = secondItem, MovementType = InventoryMovementType.PurchaseReceipt, IsPosted = true, Quantity = 1, TotalValue = 80m });
            Context.FinanceSettings.Add(new FinanceSettings { TenantId = Tenant, ControlAccountGRVAccrualId = Guid.NewGuid() });
            await Context.SaveChangesAsync();
            var line1 = new VendorInvoiceLineItem { TenantId = Tenant, PurchaseOrderItemId = poLine1.Id, InventoryItemId = firstItem, Quantity = 1, UnitPrice = 120 };
            var line2 = new VendorInvoiceLineItem { TenantId = Tenant, PurchaseOrderItemId = poLine2.Id, InventoryItemId = secondItem, Quantity = 1, UnitPrice = 80 };
            var invoice = new VendorInvoice { TenantId = Tenant, PurchaseOrderId = order, AcceptedSupplyKind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection,
                AcceptedSupplySourceId = order, LineItems = new List<VendorInvoiceLineItem> { line1, line2 } };
            return (invoice, line1.Id, line2.Id, firstAccount, secondAccount);
        }
        public async Task AddReceiptAsync(Guid order, Guid item, Guid account, decimal amount)
        {
            var receipt = new PurchaseOrderReceipt { TenantId = Tenant, PurchaseOrderId = order, ReceiptNumber = "REC-NEXT" };
            var journal = new JournalEntry { TenantId = Tenant, JournalEntryNumber = "JE-NEXT", PostingStatus = "Posted" };
            Context.PurchaseOrderReceipts.Add(receipt);
            Context.JournalEntries.Add(journal);
            Context.FinancePostingEvents.Add(new FinancePostingEvent { TenantId = Tenant, SourceDocumentType = "ProcurementPurchaseOrderReceipt",
                SourceDocumentId = receipt.Id, PostingAction = "PostAcceptedInventoryReceipt", PostingStatus = "Posted", JournalEntryId = journal.Id });
            Context.AccountTransactions.Add(new AccountTransaction { TenantId = Tenant, JournalEntryId = journal.Id, AccountId = account,
                SourceDocumentLineId = item, CreditAmount = amount, TransactionTag = "INV-RECEIPT-GRV-ACCRUAL" });
            await Context.SaveChangesAsync();
        }
        public async Task<VendorInvoice> AddPostedInvoiceAsync(VendorInvoice source, VendorInvoiceLineItem sourceLine, Guid account, decimal amount, string state = "posted")
        {
            var tenant = state == "foreign" ? Guid.NewGuid() : Tenant;
            var journal = new JournalEntry { TenantId = tenant, JournalEntryNumber = "JE-AP", PostingStatus = "Posted", IsReversed = state == "reversed" };
            var line = new VendorInvoiceLineItem { TenantId = tenant, PurchaseOrderItemId = sourceLine.PurchaseOrderItemId,
                InventoryItemId = sourceLine.InventoryItemId, Quantity = 1, UnitPrice = amount };
            var invoice = new VendorInvoice { TenantId = tenant, PurchaseOrderId = source.PurchaseOrderId, JournalEntryId = journal.Id,
                InvoiceNumber = "AP-PREVIOUS", Status = state == "voided" ? VendorInvoiceStatus.Voided : VendorInvoiceStatus.Approved,
                AcceptedSupplyKind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection, AcceptedSupplySourceId = source.PurchaseOrderId,
                LineItems = new List<VendorInvoiceLineItem> { line } };
            line.VendorInvoiceId = invoice.Id;
            Context.JournalEntries.Add(journal);
            Context.VendorInvoices.Add(invoice);
            Context.FinancePostingEvents.Add(new FinancePostingEvent { TenantId = tenant, SourceModule = "AP", SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id, PostingAction = "Post", PostingStatus = "Posted", JournalEntryId = journal.Id });
            Context.AccountTransactions.Add(new AccountTransaction { TenantId = tenant, JournalEntryId = journal.Id, AccountId = account,
                SourceDocumentLineId = line.Id, DebitAmount = amount, TransactionTag = "AP-GRV" });
            await Context.SaveChangesAsync();
            return invoice;
        }
        public async Task<Dictionary<Guid, List<(Guid Account, decimal Weight)>>> SharesAsync(VendorInvoice invoice)
        {
            var task = (Task)typeof(VendorInvoiceService).GetMethod("ResolveProcurementAccrualAccountsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(service, new object[] { invoice, CancellationToken.None })!;
            await task;
            var values = (System.Collections.IDictionary)task.GetType().GetProperty("Result")!.GetValue(task)!;
            var output = new Dictionary<Guid, List<(Guid, decimal)>>();
            foreach (System.Collections.DictionaryEntry entry in values)
                output[(Guid)entry.Key] = ((System.Collections.IEnumerable)entry.Value!).Cast<object>()
                    .Select(value => ((Guid)value.GetType().GetProperty("AccountId")!.GetValue(value)!, (decimal)value.GetType().GetProperty("Weight")!.GetValue(value)!)).ToList();
            return output;
        }
        public async Task PreparePostingAsync(VendorInvoice invoice)
        {
            var settings = await Context.FinanceSettings.SingleAsync();
            var ap = Guid.NewGuid();
            settings.ControlAccountApId = ap;
            settings.BaseCurrency = "GHS";
            var accounts = await Context.AccountTransactions.Select(value => value.AccountId).Distinct().ToListAsync();
            foreach (var account in accounts.Append(ap))
                Context.Accounts.Add(new Account { Id = account, TenantId = Tenant, AccountNumber = account.ToString("N"),
                    AccountName = "Posting account", AccountType = AccountType.Liability, Status = AccountStatus.Active, IsControlAccount = true });
            var supplier = new BusinessPartner
            {
                TenantId = Tenant,
                PartnerName = "Invoice supplier",
                PartnerCode = "SUP-POST",
                PartnerType = "Supplier",
                IsActive = true
            };
            Context.BusinessPartners.Add(supplier);
            invoice.BusinessPartnerId = supplier.Id;
            invoice.BusinessPartner = supplier;
            invoice.Status = VendorInvoiceStatus.Approved;
            invoice.ApprovalRequired = false;
            invoice.ApprovalStatus = "NotRequired";
            invoice.CurrencyCode = "GHS";
            invoice.ExchangeRate = 1m;
            invoice.InvoiceNumber = "AP-NEW";
            invoice.SubTotal = invoice.LineItems.Sum(value => value.Quantity * value.UnitPrice - value.DiscountAmount);
            invoice.TotalAmount = invoice.SubTotal;
            foreach (var line in invoice.LineItems) line.VendorInvoiceId = invoice.Id;
            await Context.SaveChangesAsync();
        }
        public Task<FinancePostingRequestV2Dto> PostingRequestAsync(VendorInvoice invoice) =>
            (Task<FinancePostingRequestV2Dto>)typeof(VendorInvoiceService).GetMethod("BuildApInvoicePostingRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(service, new object?[] { invoice, Array.Empty<Guid>(), null, CancellationToken.None })!;
        public Task<IReadOnlyList<FinanceSourceDocumentLineContext>> ContextsAsync(VendorInvoice invoice) =>
            (Task<IReadOnlyList<FinanceSourceDocumentLineContext>>)typeof(VendorInvoiceService).GetMethod("BuildDimensionLineContextsAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, new object[] { invoice, CancellationToken.None })!;
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
