using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class E2E012InventoryAdjustmentFinancePostingTests
{
    [Fact]
    public async Task Opening_stock_posts_approved_inventory_evidence_to_control_and_migration_clearing_in_one_book()
    {
        var tenantId = Guid.NewGuid();
        var inventoryAccountId = Guid.NewGuid();
        var migrationClearingAccountId = Guid.NewGuid();
        var firstLineId = Guid.NewGuid();
        var secondLineId = Guid.NewGuid();
        var openingDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"opening-stock-finance-{Guid.NewGuid():N}")
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        context.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryAccountId,
            MigrationClearingAccountId = migrationClearingAccountId
            // Write-off mappings are intentionally absent: opening stock must never use P&L.
        });
        await context.SaveChangesAsync();

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdjustmentNumber = "FINDEMO-INV-OB-001",
            AdjustmentDate = openingDate,
            BookClassification = "LOCAL_GAAP",
            WarehouseId = Guid.NewGuid(),
            Reference = "FINDEMO-INV-SCHEDULE-001",
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Status = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow,
            PayloadHash = new string('A', 64),
            IntegrityHash = new string('B', 64),
            Items =
            [
                new StockAdjustmentItem
                {
                    Id = firstLineId,
                    TenantId = tenantId,
                    InventoryItemId = Guid.NewGuid(),
                    AdjustmentQuantity = 10m,
                    UnitCost = 10_000m,
                    AdjustmentValue = 100_000m,
                    InventoryItem = new InventoryItem { TenantId = tenantId, ItemCode = "OPEN-001", Name = "Opening item one" }
                },
                new StockAdjustmentItem
                {
                    Id = secondLineId,
                    TenantId = tenantId,
                    InventoryItemId = Guid.NewGuid(),
                    AdjustmentQuantity = 8m,
                    UnitCost = 10_000m,
                    AdjustmentValue = 80_000m,
                    InventoryItem = new InventoryItem { TenantId = tenantId, ItemCode = "OPEN-002", Name = "Opening item two" }
                }
            ]
        };

        FinancePostingRequestV2Dto? captured = null;
        var engine = new Mock<IFinancePostingEngine>();
        engine.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => captured = request)
            .ReturnsAsync(new FinancePostingResultDto
            {
                PostingEventId = Guid.NewGuid(),
                JournalEntryId = Guid.NewGuid(),
                JournalEntryNumber = "JE-OPEN-001",
                PostingStatus = "Posted",
                FunctionalCurrencyCode = "GHS"
            });

        await Service(context, engine.Object).PostAsync(adjustment);

        captured.Should().NotBeNull();
        captured!.PostingAction.Should().Be("PostOpeningStock");
        captured.AccountingBookCode.Should().Be("LOCAL_GAAP");
        captured.PostingDate.Should().Be(openingDate);
        captured.SourceDocumentId.Should().Be(adjustment.Id);
        captured.Lines.Should().HaveCount(4);
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(180_000m);
        captured.Lines.Sum(value => value.CreditAmount).Should().Be(180_000m);
        captured.Lines.Where(value => value.AccountId == inventoryAccountId)
            .Should().OnlyContain(value => value.DebitAmount > 0m && value.CreditAmount == 0m &&
                                          value.TransactionTag == "INV-OPEN-CONTROL");
        captured.Lines.Where(value => value.AccountId == migrationClearingAccountId)
            .Should().OnlyContain(value => value.CreditAmount > 0m && value.DebitAmount == 0m &&
                                          value.TransactionTag == "INV-OPEN-MIGRATION");
        captured.Lines.Should().OnlyContain(value =>
            value.ExchangeRateDate == openingDate &&
            value.SourceReferenceNumber == adjustment.AdjustmentNumber);
        captured.Lines.Should().NotContain(value =>
            value.TransactionTag == "INV-ADJ-EXPENSE" || value.TransactionTag == "INV-ADJ-RECOVERY");
        engine.Verify(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Opening_stock_fails_closed_when_migration_clearing_is_not_configured()
    {
        var tenantId = Guid.NewGuid();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"opening-stock-no-clearing-{Guid.NewGuid():N}")
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        context.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountInventoryId = Guid.NewGuid(),
            WriteOffExpenseAccountId = Guid.NewGuid(),
            WriteOffRecoveryAccountId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdjustmentNumber = "FINDEMO-INV-OB-002",
            AdjustmentDate = DateTime.UtcNow.Date,
            BookClassification = "IFRS",
            WarehouseId = Guid.NewGuid(),
            Reference = "FINDEMO-INV-SCHEDULE-002",
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Status = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow,
            PayloadHash = new string('C', 64),
            IntegrityHash = new string('D', 64),
            Items =
            [
                new StockAdjustmentItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InventoryItemId = Guid.NewGuid(),
                    AdjustmentQuantity = 1m,
                    UnitCost = 1m,
                    AdjustmentValue = 1m
                }
            ]
        };
        var engine = new Mock<IFinancePostingEngine>(MockBehavior.Strict);

        var action = () => Service(context, engine.Object).PostAsync(adjustment);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Migration Clearing Account*");
        engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Opening_stock_adapter_rejects_unapproved_or_mutable_inventory_evidence()
    {
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AdjustmentNumber = "FINDEMO-INV-OB-DRAFT",
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Status = "Draft",
            BookClassification = "IFRS",
            Reference = "FINDEMO-INV-SCHEDULE-DRAFT"
        };
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"opening-stock-unapproved-{Guid.NewGuid():N}")
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var engine = new Mock<IFinancePostingEngine>(MockBehavior.Strict);

        var action = () => Service(context, engine.Object).PostAsync(adjustment);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable INITIAL_STOCK evidence*approved*");
        engine.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Cycle_count_shortage_posts_balanced_expense_and_inventory_control_lines_through_finance_engine()
    {
        var tenantId = Guid.NewGuid();
        var inventoryAccountId = Guid.NewGuid();
        var expenseAccountId = Guid.NewGuid();
        var recoveryAccountId = Guid.NewGuid();
        var postingEventId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"e2e-012-finance-{Guid.NewGuid():N}")
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        context.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryAccountId,
            WriteOffExpenseAccountId = expenseAccountId,
            WriteOffRecoveryAccountId = recoveryAccountId
        });
        await context.SaveChangesAsync();

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdjustmentNumber = "ADJ-E2E-012",
            AdjustmentDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            WarehouseId = Guid.NewGuid(),
            ReasonCode = "CycleCount",
            Status = "Approved",
            Items =
            [
                new StockAdjustmentItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InventoryItemId = Guid.NewGuid(),
                    AdjustmentQuantity = -2m,
                    UnitCost = 10m,
                    AdjustmentValue = -20m,
                    InventoryItem = new InventoryItem { TenantId = tenantId, ItemCode = "ABC-A-001", Name = "ABC item" }
                }
            ]
        };

        FinancePostingRequestV2Dto? captured = null;
        var engine = new Mock<IFinancePostingEngine>();
        engine.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, FinancePostingProducerContext, CancellationToken>((request, _, _) => captured = request)
            .ReturnsAsync(new FinancePostingResultDto
            {
                PostingEventId = postingEventId,
                JournalEntryId = journalEntryId,
                JournalEntryNumber = "JE-E2E-012",
                PostingStatus = "Posted",
                FunctionalCurrencyCode = "GHS"
            });

        var result = await Service(context, engine.Object).PostAsync(adjustment);

        result.PostingEventId.Should().Be(postingEventId);
        result.JournalEntryId.Should().Be(journalEntryId);
        captured.Should().NotBeNull();
        captured!.SourceDocumentType.Should().Be("StockAdjustment");
        captured.SourceDocumentId.Should().Be(adjustment.Id);
        captured.SourceDocumentTenantId.Should().Be(tenantId);
        captured.IdempotencyKey.Should().Contain(adjustment.Id.ToString("N"));
        captured.Lines.Sum(value => value.DebitAmount).Should().Be(20m);
        captured.Lines.Sum(value => value.CreditAmount).Should().Be(20m);
        captured.Lines.Should().ContainSingle(value => value.AccountId == expenseAccountId && value.DebitAmount == 20m);
        captured.Lines.Should().ContainSingle(value => value.AccountId == inventoryAccountId && value.CreditAmount == 20m);
    }

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

    private static InventoryAdjustmentFinancePostingService Service(
        ApplicationDbContext context,
        IFinancePostingEngine engine,
        IFinanceSourceDimensionService? dimensions = null) =>
        new(context, engine, dimensions ?? Dimensions().Object, new StockAdjustmentValuationIntentBuilder(context));
}
