using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class PhysicalCountAdjustmentTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Adjustment_submission_joins_count_transaction_without_closing_it(bool replay)
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        using var unit = new UnitOfWork(context);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var adjustment = new StockAdjustment {
            TenantId=tenant, WarehouseId=Guid.NewGuid(), AdjustmentNumber="REVIEW-TEST", RequestedById=actor,
            Status=replay ? "PendingApproval" : "Draft", RowVersion=[1,2,3]
        };
        context.Add(new Warehouse { Id=adjustment.WarehouseId,TenantId=tenant,Code="REVIEW",Name="Review fixture",IsActive=true });
        context.Add(adjustment);
        if (replay) context.Add(new StockAdjustmentAction {
            TenantId=tenant, StockAdjustmentId=adjustment.Id, Sequence=1, ActionType="Submitted", ActorUserId=actor,
            IdempotencyKey="submit-test", OccurredAtUtc=DateTime.UtcNow, CorrelationId="test", SnapshotJson="{}", IntegrityHash=new string('0',64)
        });
        await context.SaveChangesAsync();
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(x=>x.UserId).Returns(actor);
        user.SetupGet(x=>x.TenantId).Returns(tenant);
        user.SetupGet(x=>x.IsAuthenticated).Returns(true);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(x=>x.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),It.IsAny<string>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed=true });
        var service = new StockAdjustmentService(
            Mock.Of<IStockAdjustmentRepository>(),Mock.Of<IInventoryItemRepository>(),Mock.Of<IStockMovementRepository>(),
            Mock.Of<IWarehouseQuantityRepository>(),Mock.Of<IWarehouseLocationRepository>(),Mock.Of<IWarehouseRepository>(),
            Mock.Of<IConsignmentSettlementService>(),user.Object,unit,Mock.Of<IInventoryTrackingControlService>(),
            Mock.Of<IInventoryNegativeStockControlService>(),access.Object,Mock.Of<IProcurementSodGuardService>(),
            Mock.Of<IWorkflowIntegrationService>(),Mock.Of<IProcurementControlEventService>(),
            Mock.Of<IInventoryAdjustmentFinancePostingService>(),Mock.Of<IInventoryValuationService>(),NullLogger<StockAdjustmentService>.Instance);
        await unit.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var request = new StockAdjustmentActionRequest { IdempotencyKey="submit-test",RowVersion=Convert.ToBase64String(adjustment.RowVersion) };
        if (replay) (await service.SubmitAsync(adjustment.Id,actor,request)).Status.Should().Be("PendingApproval");
        else {
            var action = () => service.SubmitAsync(adjustment.Id,actor,request);
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("The adjustment has no lines.");
        }
        unit.HasActiveTransaction.Should().BeTrue("only the enclosing count operation may commit or roll back");
        await unit.RollbackAsync();
    }
}
