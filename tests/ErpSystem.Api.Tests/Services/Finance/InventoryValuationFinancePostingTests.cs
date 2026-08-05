using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InventoryValuationFinancePostingTests
{
    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Accepted_receipt_posts_exact_valuation_to_inventory_and_grv_accrual()
    {
        var tenant = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var accrualAccount = Guid.NewGuid();
        await using var db = Context();
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenant, BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryAccount, ControlAccountGRVAccrualId = accrualAccount
        });
        var po = new PurchaseOrder { Id = Guid.NewGuid(), TenantId = tenant, OrderNumber = "PO-001" };
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenant, PurchaseOrderId = po.Id,
            PurchaseOrder = po, ReceiptNumber = "REC-001", ReceiptDate = new DateTime(2026, 8, 1)
        };
        db.PurchaseOrders.Add(po); db.PurchaseOrderReceipts.Add(receipt);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), TenantId = tenant, MovementNumber = "IMV-001",
            InventoryItemId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(),
            MovementType = InventoryMovementType.PurchaseReceipt, Direction = MovementDirection.In,
            Quantity = 10m, UnitCost = 12m, TotalValue = 120m, IsPosted = true,
            PostingDate = receipt.ReceiptDate, ReferenceType = ReferenceType.PO,
            ReferenceId = receipt.Id, ReferenceNumber = receipt.ReceiptNumber
        });
        await db.SaveChangesAsync();
        FinancePostingRequestDto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(Result());

        await new InventoryReceiptFinancePostingService(db, posting.Object)
            .PostAcceptedReceiptAsync(receipt.Id);

        captured.Should().NotBeNull();
        captured!.SourceDocumentType.Should().Be("ProcurementPurchaseOrderReceipt");
        captured.IdempotencyKey.Should().Contain(receipt.Id.ToString("N"));
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(120m);
        captured.Lines.Sum(value => value.CreditAmount).Should().Be(120m);
        captured.Lines.Should().Contain(value => value.AccountId == inventoryAccount && value.DebitAmount == 120m);
        captured.Lines.Should().Contain(value => value.AccountId == accrualAccount && value.CreditAmount == 120m);
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
        FinancePostingRequestDto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(Result());

        await new InventoryLandedCostFinancePostingService(db, posting.Object).PostLandedCostAsync(landed.Id);

        captured!.SourceDocumentType.Should().Be("InventoryLandedCost");
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(50m);
        captured.Lines.Sum(value => value.CreditAmount).Should().Be(50m);
        captured.Lines.Should().Contain(value => value.AccountId == inventoryAccount && value.DebitAmount == 40m);
        captured.Lines.Should().Contain(value => value.AccountId == varianceAccount && value.DebitAmount == 10m);
        captured.Lines.Should().Contain(value => value.AccountId == accrualAccount && value.CreditAmount == 50m);
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

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        return new ApplicationDbContext(options);
    }
}
