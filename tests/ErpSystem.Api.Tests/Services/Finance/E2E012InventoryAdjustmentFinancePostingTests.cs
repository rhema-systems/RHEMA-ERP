using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Finance;
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

        FinancePostingRequestDto? captured = null;
        var engine = new Mock<IFinancePostingEngine>();
        engine.Setup(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new FinancePostingResultDto
            {
                PostingEventId = postingEventId,
                JournalEntryId = journalEntryId,
                JournalEntryNumber = "JE-E2E-012",
                PostingStatus = "Posted",
                FunctionalCurrencyCode = "GHS"
            });

        var result = await new InventoryAdjustmentFinancePostingService(context, engine.Object)
            .PostAsync(adjustment);

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
}
