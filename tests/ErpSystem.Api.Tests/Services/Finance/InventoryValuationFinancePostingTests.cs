using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Mapping;
using AutoMapper;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InventoryValuationFinancePostingTests
{
    [Theory]
    [InlineData(0.006, 0.006, -0.001)]
    [InlineData(-0.006, -0.006, -0.001)]
    [InlineData(10.006, -10.002, 0)]
    public void Signed_landed_cost_allocations_preserve_net_total_and_each_source_sign(decimal first, decimal second, decimal third)
    {
        var values = new[] { first, second, third };
        var total = decimal.Round(values.Sum(), 2, MidpointRounding.AwayFromZero);
        var result = InventoryLandedCostFinancePostingService.AllocateSignedValues(values, total);
        result.Sum().Should().Be(total);
        for (var index = 0; index < values.Length; index++)
        {
            (result[index] * values[index]).Should().BeGreaterThanOrEqualTo(0m);
            if (values[index] == 0m) result[index].Should().Be(0m);
        }
    }

    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Accepted_receipt_posts_exact_valuation_to_inventory_and_grv_accrual()
    {
        var tenant = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var accrualAccount = Guid.NewGuid();
        await using var db = Context();
        db.Accounts.AddRange(
            new Account { Id = inventoryAccount, TenantId = tenant, AccountCode = "INV", AccountNumber = "INV",
                AccountName = "Inventory", AccountType = AccountType.Asset, Status = AccountStatus.Active, IsControlAccount = true },
            new Account { Id = accrualAccount, TenantId = tenant, AccountCode = "GRV", AccountNumber = "GRV",
                AccountName = "Receipt accrual", AccountType = AccountType.Liability, Status = AccountStatus.Active, IsControlAccount = true });
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenant, BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryAccount, ControlAccountGRVAccrualId = accrualAccount
        });
        var partner = new BusinessPartner { TenantId = tenant, PartnerCode = "BP-001", PartnerName = "Supplier" };
        var item = new InventoryItem { TenantId = tenant, ItemCode = "ITEM-001", Name = "Item" };
        db.BusinessPartners.Add(partner); db.InventoryItems.Add(item);
        var po = new PurchaseOrder { Id = Guid.NewGuid(), TenantId = tenant, OrderNumber = "PO-001", BusinessPartnerId = partner.Id, BusinessPartner = partner };
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenant, PurchaseOrderId = po.Id,
            PurchaseOrder = po, ReceiptNumber = "REC-001", ReceiptDate = new DateTime(2026, 8, 1)
        };
        db.PurchaseOrders.Add(po); db.PurchaseOrderReceipts.Add(receipt);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), TenantId = tenant, MovementNumber = "IMV-001",
            InventoryItemId = item.Id, WarehouseId = Guid.NewGuid(),
            MovementType = InventoryMovementType.PurchaseReceipt, Direction = MovementDirection.In,
            Quantity = 10m, UnitCost = 12m, TotalValue = 120m, IsPosted = true,
            PostingDate = receipt.ReceiptDate, ReferenceType = ReferenceType.PO,
            ReferenceId = receipt.Id, ReferenceNumber = receipt.ReceiptNumber
        });
        await db.SaveChangesAsync();
        FinancePostingRequestV2Dto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => captured = request)
            .ReturnsAsync(Result());

        var dimensions = Dimensions();
        await new InventoryReceiptFinancePostingService(db, posting.Object, dimensions.Object)
            .PostAcceptedReceiptAsync(receipt.Id);

        captured.Should().NotBeNull();
        captured!.SourceDocumentType.Should().Be("ProcurementPurchaseOrderReceipt");
        captured.IdempotencyKey.Should().Contain(receipt.Id.ToString("N"));
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(120m);
        captured.Lines.Sum(value => value.CreditAmount).Should().Be(120m);
        captured.Lines.Should().Contain(value => value.AccountId == inventoryAccount && value.DebitAmount == 120m);
        captured.Lines.Should().Contain(value => value.AccountId == accrualAccount && value.CreditAmount == 120m);
        captured.Lines.Should().OnlyContain(value => value.SourceDocumentLineId == item.Id &&
            value.ExchangeRateDate == receipt.ReceiptDate);
        dimensions.Verify(value => value.SynchronizeDraftAsync(
            It.IsAny<FinancePostingProducerContext>(), receipt.Id, receipt.ReceiptDate,
            It.Is<IReadOnlyList<FinanceSourceDocumentLineContext>>(contexts => contexts.Count == 1 &&
                contexts[0].SourceLineId == item.Id && contexts[0].AdditionalAccountIds != null &&
                contexts[0].AdditionalAccountIds.Contains(accrualAccount)),
            It.IsAny<FinanceSourceDocumentDimensionInputDto?>(), false, null,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Landed_cost_posts_inventory_and_consumed_variance_without_parallel_journal_owner()
    {
        var tenant = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var accrualAccount = Guid.NewGuid();
        var varianceAccount = Guid.NewGuid();
        await using var db = Context();
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenant, BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryAccount, ControlAccountGRVAccrualId = accrualAccount,
            WriteOffExpenseAccountId = varianceAccount
        });
        var landed = new LandedCost
        {
            Id = Guid.NewGuid(), TenantId = tenant, LandedCostNumber = "LC-001", Status = "Posted",
            TotalCost = 50m, AllocatedAmount = 50m, UnallocatedAmount = 0m,
            PostedDate = new DateTime(2026, 8, 1), CostDate = new DateTime(2026, 8, 1)
        };
        db.LandedCosts.Add(landed);
        db.InventoryMovements.AddRange(
            Movement(tenant, landed.Id, "IMV-LC-1", 40m, null),
            Movement(tenant, landed.Id, "IMV-LC-2", 0m, 10m));
        await db.SaveChangesAsync();
        FinancePostingRequestV2Dto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => captured = request)
            .ReturnsAsync(Result());

        await new InventoryLandedCostFinancePostingService(db, posting.Object, Dimensions().Object).PostLandedCostAsync(landed.Id);

        captured!.SourceDocumentType.Should().Be("InventoryLandedCost");
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(50m);
        captured.Lines.Sum(value => value.CreditAmount).Should().Be(50m);
        captured.Lines.Should().Contain(value => value.AccountId == inventoryAccount && value.DebitAmount == 40m);
        captured.Lines.Should().Contain(value => value.AccountId == varianceAccount && value.DebitAmount == 10m);
        captured.Lines.Should().Contain(value => value.AccountId == accrualAccount && value.CreditAmount == 50m);
        captured.Lines.Select(value => value.SourceDocumentLineId).Should().OnlyHaveUniqueItems();
        captured.Lines.Should().OnlyContain(value => value.ExchangeRateDate == landed.PostedDate);
    }

    [Fact]
    public async Task Landed_cost_uses_item_account_overrides_and_preserves_balanced_rounding()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context();
        var inventory = new Account { TenantId = tenant, AccountCode = "ITEM-INV", AccountNumber = "1301", AccountName = "Item inventory", AccountType = AccountType.Asset, Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false };
        var variance = new Account { TenantId = tenant, AccountCode = "ITEM-PPV", AccountNumber = "5101", AccountName = "Item PPV", AccountType = AccountType.Expense, Status = AccountStatus.Active, AllowDirectPosting = true };
        db.Accounts.AddRange(inventory, variance);
        var item = new InventoryItem { TenantId = tenant, ItemCode = "ITEM", Name = "Item", InventoryAccountId = inventory.Id, PurchasePriceVarianceAccountId = variance.Id };
        db.InventoryItems.Add(item);
        var accrual = Guid.NewGuid();
        db.FinanceSettings.Add(new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", ControlAccountInventoryId = Guid.NewGuid(), ControlAccountGRVAccrualId = accrual, WriteOffExpenseAccountId = Guid.NewGuid() });
        var landed = new LandedCost { TenantId = tenant, LandedCostNumber = "LC-ITEM", Status = "Posted", TotalCost = 50m, AllocatedAmount = 50m, CostDate = DateTime.UtcNow, PostedDate = DateTime.UtcNow };
        db.LandedCosts.Add(landed);
        var movement = Movement(tenant, landed.Id, "LC-ITEM-MOV", 40m, 10m);
        movement.InventoryItemId = item.Id;
        db.InventoryMovements.Add(movement);
        await db.SaveChangesAsync();
        FinancePostingRequestV2Dto? captured = null;
        var engine = new Mock<IFinancePostingEngine>();
        engine.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(),
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => captured = request)
            .ReturnsAsync(Result());
        await new InventoryLandedCostFinancePostingService(db, engine.Object, Dimensions().Object).PostLandedCostAsync(landed.Id);
        captured!.Lines.Should().Contain(line => line.AccountId == inventory.Id && line.DebitAmount == 40m);
        captured.Lines.Should().Contain(line => line.AccountId == variance.Id && line.DebitAmount == 10m);
        captured.Lines.Should().Contain(line => line.AccountId == accrual && line.CreditAmount == 50m);
        captured.Lines.Sum(line => line.DebitAmount - line.CreditAmount).Should().Be(0m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Landed_cost_tiny_item_values_never_assign_a_negative_rounding_residual(bool consumedVariance)
    {
        var tenant = Guid.NewGuid();
        var inventory = Guid.NewGuid(); var variance = Guid.NewGuid(); var accrual = Guid.NewGuid();
        await using var db = Context();
        db.FinanceSettings.Add(new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", ControlAccountInventoryId = inventory,
            ControlAccountGRVAccrualId = accrual, WriteOffExpenseAccountId = variance });
        var landed = new LandedCost { TenantId = tenant, LandedCostNumber = "LC-TINY", Status = "Posted",
            TotalCost = 0.01m, AllocatedAmount = 0.01m, CostDate = DateTime.UtcNow, PostedDate = DateTime.UtcNow };
        db.LandedCosts.Add(landed);
        var values = new[] { 0.006m, 0.006m, 0.001m };
        for (var index = 0; index < values.Length; index++)
        {
            var movement = Movement(tenant, landed.Id, $"LC-TINY-{index}", consumedVariance ? 0m : values[index], consumedVariance ? values[index] : null);
            movement.InventoryItemId = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");
            db.InventoryMovements.Add(movement);
        }
        await db.SaveChangesAsync();
        FinancePostingRequestV2Dto? captured = null;
        var engine = new Mock<IFinancePostingEngine>();
        engine.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(),
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => captured = request)
            .ReturnsAsync(Result());

        await new InventoryLandedCostFinancePostingService(db, engine.Object, Dimensions().Object).PostLandedCostAsync(landed.Id);

        captured!.Lines.Should().HaveCount(2);
        captured.Lines.Single(line => line.AccountId == (consumedVariance ? variance : inventory)).DebitAmount.Should().Be(0.01m);
        captured.Lines.Where(line => line.AccountId != accrual).Should().OnlyContain(line => line.CreditAmount == 0m);
        captured.Lines.Sum(line => line.DebitAmount - line.CreditAmount).Should().Be(0m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Store_return_uses_original_issue_accounts_for_legacy_and_item_specific_postings(bool legacy)
    {
        var journal = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var expenseAccount = Guid.NewGuid();
        var entries = new[]
        {
            new AccountTransaction { JournalEntryId = journal, AccountId = expenseAccount, TransactionTag = $"INV-ISSUE-EXP-{lineId:N}" },
            new AccountTransaction { JournalEntryId = journal, AccountId = inventoryAccount, TransactionTag = legacy ? "INV-ISSUE-CONTROL" : $"INV-ISSUE-CTL-{lineId:N}" },
            new AccountTransaction { JournalEntryId = Guid.NewGuid(), AccountId = Guid.NewGuid(), TransactionTag = $"INV-ISSUE-EXP-{lineId:N}" }
        };
        var resolved = InventoryIssueFinanceAssetPostingService.ResolveOriginalIssueAccounts(entries, journal, lineId);
        resolved.DebitAccountId.Should().Be(expenseAccount);
        resolved.InventoryAccountId.Should().Be(inventoryAccount);
    }

    [Fact]
    public void Item_master_mapping_roundtrips_accounts_without_clearing_omitted_values()
    {
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<InventoryMappingProfile>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        var inventory = Guid.NewGuid();
        var sales = Guid.NewGuid();
        var item = new InventoryItem { InventoryAccountId = inventory, SalesAccountId = sales };
        mapper.Map<InventoryItemDto>(item).PostingAccounts.InventoryAccountId.Should().Be(inventory);
        mapper.Map(new UpdateInventoryItemDto(), item);
        item.InventoryAccountId.Should().Be(inventory);
        mapper.Map(new UpdateInventoryItemDto { PostingAccounts = new InventoryItemPostingAccountsDto { InventoryAccountId = null } }, item);
        item.InventoryAccountId.Should().BeNull();
        item.SalesAccountId.Should().Be(sales);
    }

    private static InventoryMovement Movement(Guid tenant, Guid landedCostId, string number,
        decimal value, decimal? variance) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, MovementNumber = number,
        InventoryItemId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(),
        MovementType = InventoryMovementType.LandedCostRevaluation, Direction = MovementDirection.In,
        Quantity = 0m, UnitCost = 0m, TotalValue = value, VarianceAmount = variance, IsPosted = true,
        PostingDate = new DateTime(2026, 8, 1), ReferenceType = ReferenceType.Adjustment,
        ReferenceId = landedCostId
    };

    private static FinancePostingResultDto Result() => new()
    {
        PostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid(), JournalEntryNumber = "JE-TEST",
        PostingStatus = "Posted", FunctionalCurrencyCode = "GHS"
    };

    private static Mock<IFinanceSourceDimensionService> Dimensions()
    {
        var dimensions = new Mock<IFinanceSourceDimensionService>();
        dimensions.Setup(value => value.SynchronizeDraftAsync(
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(), It.IsAny<FinanceSourceDocumentDimensionInputDto?>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
        dimensions.Setup(value => value.ValidateAndFreezeAsync(
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
        dimensions.Setup(value => value.ResolvePostingDimensionsAsync(
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FinancePostingDimensionValueDto>());
        return dimensions;
    }

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        return new ApplicationDbContext(options);
    }
}
