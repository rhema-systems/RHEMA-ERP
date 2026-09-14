using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class PurchaseReceiptDistributionTests
{
    [Fact]
    public async Task Saved_splits_drive_posting_and_do_not_change_item_defaults()
    {
        await using var db = Context();
        var (receipt, item, original, _) = await SeedAsync(db);
        var alternate = Account(receipt.TenantId, "ALT", AccountType.Asset); db.Accounts.Add(alternate);
        await db.SaveChangesAsync();
        var service = new ProcurementReceiptDistributionService(db);
        var before = await service.GetAsync(receipt.TenantId, receipt.Id);
        var request = Edit(before);
        var inventory = request.Lines.Single(value => value.Purpose == "Inventory");
        inventory.Debit = 40m;
        request.Lines.Add(new() { LineId = Guid.NewGuid(), InventoryItemId = item.Id, Purpose = "Inventory", AccountId = alternate.Id, Debit = 80m });
        var audited = 0;
        var saved = await service.SaveAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), (_, after) =>
        { after.Should().NotBeNull(); audited++; return Task.CompletedTask; });
        saved.HasOverrides.Should().BeTrue(); saved.Version.Should().NotBe(before.Version); audited.Should().Be(1);
        var movement = await db.InventoryMovements.SingleAsync();
        movement.TotalValue = 60m; await db.SaveChangesAsync();
        FinancePostingRequestDto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((value, _) => captured = value)
            .ReturnsAsync(new FinancePostingResultDto { JournalEntryId = Guid.NewGuid(), PostingEventId = Guid.NewGuid() });
        await new InventoryReceiptFinancePostingService(db, posting.Object).PostAcceptedReceiptAsync(receipt.Id);
        captured!.Lines.Should().Contain(value => value.AccountId == original.Id && value.DebitAmount == 20m);
        captured.Lines.Should().Contain(value => value.AccountId == alternate.Id && value.DebitAmount == 40m);
        captured.Lines.Should().OnlyContain(value => value.SourceDocumentLineId == item.Id);
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(captured.Lines.Sum(value => value.CreditAmount));
        item.InventoryAccountId.Should().Be(original.Id);
    }

    [Fact]
    public async Task Stale_save_and_reset_cannot_overwrite_newer_distribution()
    {
        await using var db = Context();
        var (receipt, _, _, _) = await SeedAsync(db);
        var service = new ProcurementReceiptDistributionService(db);
        var request = Edit(await service.GetAsync(receipt.TenantId, receipt.Id));
        var saved = await service.SaveAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), NoAudit);
        Func<Task> stale = () => service.ResetAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), NoAudit);
        await stale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed*");
        var reset = await service.ResetAsync(receipt.TenantId, receipt.Id, Edit(saved), Guid.NewGuid(), NoAudit);
        reset.HasOverrides.Should().BeFalse();
    }

    [Fact]
    public async Task Valid_saved_replacement_survives_original_default_becoming_inactive()
    {
        await using var db = Context();
        var (receipt, _, original, _) = await SeedAsync(db);
        var replacement = Account(receipt.TenantId, "REPLACEMENT", AccountType.Asset);
        db.Accounts.Add(replacement); await db.SaveChangesAsync();
        var service = new ProcurementReceiptDistributionService(db);
        var request = Edit(await service.GetAsync(receipt.TenantId, receipt.Id));
        request.Lines.Single(value => value.Purpose == "Inventory").AccountId = replacement.Id;
        await service.SaveAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), NoAudit);
        original.Status = AccountStatus.Inactive; await db.SaveChangesAsync();
        var view = await service.GetAsync(receipt.TenantId, receipt.Id);
        view.Lines.Should().Contain(value => value.AccountId == replacement.Id && value.Debit == 120m);
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancePostingResultDto { JournalEntryId = Guid.NewGuid(), PostingEventId = Guid.NewGuid() });
        await new InventoryReceiptFinancePostingService(db, posting.Object).PostAcceptedReceiptAsync(receipt.Id);
        posting.Verify(value => value.PostAsync(It.Is<FinancePostingRequestDto>(request =>
            request.Lines.Any(line => line.AccountId == replacement.Id && line.DebitAmount == 120m)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("nonposting")]
    [InlineData("wrongtype")]
    [InlineData("unbalanced")]
    [InlineData("source")]
    [InlineData("twosided")]
    [InlineData("nullrow")]
    public async Task Invalid_override_is_rejected_without_saving(string issue)
    {
        await using var db = Context();
        var (receipt, _, _, _) = await SeedAsync(db);
        var account = Account(issue == "foreign" ? Guid.NewGuid() : receipt.TenantId, "OTHER",
            issue == "wrongtype" ? AccountType.Expense : AccountType.Asset);
        if (issue == "inactive") account.Status = AccountStatus.Inactive;
        if (issue == "nonposting") { account.IsControlAccount = false; account.AllowDirectPosting = false; }
        db.Accounts.Add(account); await db.SaveChangesAsync();
        var service = new ProcurementReceiptDistributionService(db);
        var request = Edit(await service.GetAsync(receipt.TenantId, receipt.Id));
        var line = request.Lines.Single(value => value.Purpose == "Inventory");
        line.AccountId = account.Id;
        if (issue == "unbalanced") line.Debit = 119m;
        if (issue == "source") line.InventoryItemId = Guid.NewGuid();
        if (issue == "twosided") line.Credit = 1m;
        if (issue == "nullrow") request.Lines.Add(null!);
        Func<Task> save = () => service.SaveAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), NoAudit);
        await save.Should().ThrowAsync<InvalidOperationException>();
        (await db.PurchaseOrderReceipts.AsNoTracking().SingleAsync()).DistributionDraftJson.Should().BeNull();
    }

    [Fact]
    public async Task Posted_journal_cannot_be_overridden_or_reset()
    {
        await using var db = Context();
        var (receipt, _, _, _) = await SeedAsync(db);
        var service = new ProcurementReceiptDistributionService(db);
        var request = Edit(await service.GetAsync(receipt.TenantId, receipt.Id));
        db.FinancePostingEvents.Add(new FinancePostingEvent { TenantId = receipt.TenantId,
            SourceDocumentType = "ProcurementPurchaseOrderReceipt", SourceDocumentId = receipt.Id,
            PostingAction = "PostAcceptedInventoryReceipt", PostingStatus = "Posted", JournalEntryId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        Func<Task> save = () => service.SaveAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), NoAudit);
        Func<Task> reset = () => service.ResetAsync(receipt.TenantId, receipt.Id, request, Guid.NewGuid(), NoAudit);
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be changed*");
        await reset.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be changed*");
    }

    private static Task NoAudit(string? before, string? after) => Task.CompletedTask;
    private static SavePurchaseReceiptDistributionRequest Edit(PurchaseReceiptDistributionDto view) => new()
    {
        Version = view.Version, BasisVersion = view.BasisVersion,
        Lines = view.Lines.Select(value => new SavePurchaseReceiptDistributionLine
        {
            LineId = value.LineId, InventoryItemId = value.InventoryItemId!.Value, Purpose = value.Purpose,
            AccountId = value.AccountId, Debit = value.Debit, Credit = value.Credit
        }).ToList()
    };

    [Fact]
    public void Cent_allocation_preserves_positive_signs_total_and_zero_basis()
    {
        var amounts = new[] { 0.006m, 0.006m, 0.001m, 0m };
        var allocated = MonetaryAllocation.Allocate(amounts, 0.01m);
        allocated.Should().Equal(0.01m, 0m, 0m, 0m);
        allocated.Sum().Should().Be(0.01m);
        MonetaryAllocation.Allocate(amounts, 0.02m).Should().Equal(0.01m, 0.01m, 0m, 0m);
        MonetaryAllocation.Allocate(Array.Empty<decimal>(), 0m).Should().BeEmpty();
    }

    [Fact]
    public async Task Tiny_positive_receipt_items_never_generate_negative_inventory_or_accrual()
    {
        await using var db = Context();
        var (receipt, _, inventory, _) = await SeedAsync(db, movement: false);
        var variance = Account(receipt.TenantId, "PPV", AccountType.Expense);
        db.Accounts.Add(variance);
        var values = new[] { 0.006m, 0.006m, 0.001m };
        for (var index = 0; index < values.Length; index++)
        {
            var item = new InventoryItem { Id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
                TenantId = receipt.TenantId, ItemCode = $"TINY-{index}", Name = $"Tiny {index}",
                InventoryAccountId = inventory.Id, PurchasePriceVarianceAccountId = variance.Id };
            db.InventoryItems.Add(item);
            db.InventoryMovements.Add(new InventoryMovement { TenantId = receipt.TenantId, InventoryItemId = item.Id,
                WarehouseId = Guid.NewGuid(), MovementNumber = $"TINY-{index}", ReferenceId = receipt.Id,
                MovementType = InventoryMovementType.PurchaseReceipt, Direction = MovementDirection.In,
                Quantity = 1m, UnitCost = values[index], TotalValue = values[index], VarianceAmount = 0m,
                IsPosted = true, ReferenceType = ReferenceType.PO });
        }
        await db.SaveChangesAsync();
        var view = await new ProcurementReceiptDistributionService(db).GetAsync(receipt.TenantId, receipt.Id);
        view.TotalDebit.Should().Be(0.01m); view.TotalCredit.Should().Be(0.01m);
        view.Lines.Where(line => line.Type == "Inventory").Should().OnlyContain(line => line.Credit == 0m);
        view.Lines.Where(line => line.Type == "Accrued purchases").Should().OnlyContain(line => line.Debit == 0m);
    }

    [Fact]
    public async Task Preview_and_post_use_the_same_item_and_supplier_accounts()
    {
        await using var db = Context();
        var (receipt, item, inventory, accrued) = await SeedAsync(db);
        var view = await new ProcurementReceiptDistributionService(db).GetAsync(receipt.TenantId, receipt.Id);
        view.TotalDebit.Should().Be(120m); view.TotalCredit.Should().Be(120m);
        view.Lines.Should().Contain(line => line.AccountId == inventory.Id && line.Debit == 120m && line.Source == "Item default");
        view.Lines.Should().Contain(line => line.AccountId == accrued.Id && line.Credit == 120m && line.Source == "PO supplier default");
        FinancePostingRequestDto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new FinancePostingResultDto { JournalEntryId = Guid.NewGuid(), PostingEventId = Guid.NewGuid() });
        await new InventoryReceiptFinancePostingService(db, posting.Object).PostAcceptedReceiptAsync(receipt.Id);
        captured!.Lines.Should().OnlyContain(line => line.SourceDocumentLineId == item.Id);
        captured.Lines.Select(line => (line.AccountId, line.DebitAmount, line.CreditAmount))
            .Should().BeEquivalentTo(view.Lines.Select(line => (line.AccountId, line.Debit, line.Credit)));
    }

    [Fact]
    public async Task Posted_distribution_uses_journal_even_when_current_defaults_are_invalid()
    {
        await using var db = Context();
        var (receipt, item, inventory, accrued) = await SeedAsync(db);
        var journal = new JournalEntry { TenantId = receipt.TenantId, JournalEntryNumber = "JE-HISTORY", Description = "Receipt" };
        db.JournalEntries.Add(journal);
        db.FinancePostingEvents.Add(new FinancePostingEvent { TenantId = receipt.TenantId, SourceModule = "Inventory",
            SourceDocumentType = "ProcurementPurchaseOrderReceipt", SourceDocumentId = receipt.Id,
            PostingAction = "PostAcceptedInventoryReceipt", PostingStatus = "Posted", JournalEntryId = journal.Id,
            FunctionalCurrencyCode = "GHS" });
        db.AccountTransactions.AddRange(
            new AccountTransaction { TenantId = receipt.TenantId, JournalEntryId = journal.Id, AccountId = inventory.Id,
                DebitAmount = 120m, LineNumber = 1, TransactionTag = "INV-RECEIPT-CONTROL" },
            new AccountTransaction { TenantId = receipt.TenantId, JournalEntryId = journal.Id, AccountId = accrued.Id,
                CreditAmount = 120m, LineNumber = 2, TransactionTag = "INV-RECEIPT-GRV-ACCRUAL" });
        item.InventoryAccountId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var view = await new ProcurementReceiptDistributionService(db).GetAsync(receipt.TenantId, receipt.Id);
        view.Status.Should().Be("Posted"); view.JournalEntryNumber.Should().Be("JE-HISTORY");
        view.Lines.Should().OnlyContain(line => line.Source == "Posted journal");
        view.Lines.Should().Contain(line => line.AccountId == inventory.Id && line.Debit == 120m);
    }

    [Fact]
    public async Task Wrong_tenant_cannot_read_a_distribution()
    {
        await using var db = Context();
        var (receipt, _, _, _) = await SeedAsync(db);
        var act = () => new ProcurementReceiptDistributionService(db).GetAsync(Guid.NewGuid(), receipt.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Draft_preview_excludes_rejected_units_and_uses_standard_cost_variance()
    {
        await using var db = Context();
        var (receipt, item, _, _) = await SeedAsync(db, movement: false);
        receipt.Status = "Accepted"; item.ValuationMethod = ValuationMethod.StandardCost; item.StandardCost = 10m;
        var variance = Account(receipt.TenantId, "PPV", AccountType.Expense); db.Accounts.Add(variance);
        item.PurchasePriceVarianceAccountId = variance.Id;
        var poItem = new PurchaseOrderItem { TenantId = receipt.TenantId, PurchaseOrderId = receipt.PurchaseOrderId,
            InventoryItemId = item.Id, UnitPrice = 12m, OrderedQuantity = 10m, LineType = ItemType.StockItem };
        db.PurchaseOrderItems.Add(poItem);
        db.PurchaseOrderReceiptItems.Add(new PurchaseOrderReceiptItem { TenantId = receipt.TenantId, ReceiptId = receipt.Id,
            PurchaseOrderItemId = poItem.Id, ReceivedQuantity = 10m, AcceptedQuantity = 8m, RejectedQuantity = 2m });
        await db.SaveChangesAsync();
        var view = await new ProcurementReceiptDistributionService(db).GetAsync(receipt.TenantId, receipt.Id);
        view.TotalDebit.Should().Be(96m); view.TotalCredit.Should().Be(96m);
        view.Lines.Should().Contain(line => line.Type == "Inventory" && line.Debit == 80m);
        view.Lines.Should().Contain(line => line.AccountId == variance.Id && line.Debit == 16m);
    }

    private static async Task<(PurchaseOrderReceipt, InventoryItem, Account, Account)> SeedAsync(ApplicationDbContext db, bool movement = true)
    {
        var tenant = Guid.NewGuid();
        var inventory = Account(tenant, "INV", AccountType.Asset); var accrued = Account(tenant, "GRV", AccountType.Liability);
        db.Accounts.AddRange(inventory, accrued);
        var partner = new BusinessPartner { TenantId = tenant, PartnerName = "Supplier", PartnerCode = "SUP",
            DefaultAccruedPurchasesAccountId = accrued.Id };
        db.BusinessPartners.Add(partner);
        var po = new PurchaseOrder { TenantId = tenant, OrderNumber = "PO-TEST", BusinessPartner = partner,
            BusinessPartnerId = partner.Id, SupplierDefaultsSnapshotJson = BusinessPartnerPostingDefaults.SerializeSnapshot(partner) };
        db.PurchaseOrders.Add(po);
        var receipt = new PurchaseOrderReceipt { TenantId = tenant, PurchaseOrderId = po.Id, PurchaseOrder = po, ReceiptNumber = "REC-TEST" };
        db.PurchaseOrderReceipts.Add(receipt);
        var item = new InventoryItem { TenantId = tenant, ItemCode = "ITEM", Name = "Item", InventoryAccountId = inventory.Id };
        db.InventoryItems.Add(item);
        db.FinanceSettings.Add(new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", ControlAccountInventoryId = Guid.NewGuid(), ControlAccountGRVAccrualId = Guid.NewGuid() });
        if (movement) db.InventoryMovements.Add(new InventoryMovement { TenantId = tenant, InventoryItemId = item.Id,
            WarehouseId = Guid.NewGuid(), MovementNumber = "MOV-TEST", ReferenceId = receipt.Id,
            MovementType = InventoryMovementType.PurchaseReceipt, Direction = MovementDirection.In,
            Quantity = 10m, UnitCost = 12m, TotalValue = 120m, IsPosted = true, ReferenceType = ReferenceType.PO });
        await db.SaveChangesAsync();
        return (receipt, item, inventory, accrued);
    }
    private static Account Account(Guid tenant, string code, AccountType type) => new() { TenantId = tenant,
        AccountCode = code, AccountNumber = code, AccountName = code, AccountType = type, Status = AccountStatus.Active,
        IsControlAccount = type != AccountType.Expense, AllowDirectPosting = type == AccountType.Expense };
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
}
