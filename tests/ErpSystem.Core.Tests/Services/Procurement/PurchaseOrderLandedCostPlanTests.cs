using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;
public class PurchaseOrderLandedCostPlanTests
{
    private readonly PurchaseOrder po = new() { TenantId = Guid.NewGuid(), Status = "Draft", Currency = "GHS" };
    private PurchaseOrderItem Line() => new() { PurchaseOrderId = po.Id, TenantId = po.TenantId };
    private static UpsertPurchaseOrderLandedCostPlanItemDto Cost() => new()
        { CostType = ErpSystem.Core.Enums.LandedCostType.Freight, Description = "Freight", Amount = 100, Currency = "GHS" };
    [Fact]
    public void SharedCostHasNoTarget() =>
        Assert.Null(PurchaseOrderLandedCostPlanService.ResolveTarget(Cost(), new[] { Line() }, po.Id, po.TenantId, true));
    [Fact]
    public void NewLineIndexResolvesToStableIdentity()
    {
        var lines = new[] { Line(), Line() };
        var cost = Cost(); cost.PurchaseOrderLineIndex = 1;
        Assert.Equal(lines[1].Id, PurchaseOrderLandedCostPlanService.ResolveTarget(cost, lines, po.Id, po.TenantId, true));
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void InvalidIndexIsRejected(int index)
    {
        var cost = Cost(); cost.PurchaseOrderLineIndex = index;
        Assert.Throws<ArgumentException>(() => PurchaseOrderLandedCostPlanService.ResolveTarget(cost, new[] { Line() }, po.Id, po.TenantId, true));
    }
    [Fact]
    public void StandalonePlanDoesNotAcceptTransientIndexes()
    {
        var cost = Cost(); cost.PurchaseOrderLineIndex = 0;
        Assert.Throws<ArgumentException>(() => PurchaseOrderLandedCostPlanService.ResolveTarget(cost, new[] { Line() }, po.Id, po.TenantId, false));
    }
    [Theory]
    [InlineData("foreign-po")]
    [InlineData("foreign-tenant")]
    [InlineData("deleted")]
    [InlineData("missing")]
    public void InvalidLineIdentityIsRejected(string scenario)
    {
        var line = Line();
        if (scenario == "foreign-po") line.PurchaseOrderId = Guid.NewGuid();
        if (scenario == "foreign-tenant") line.TenantId = Guid.NewGuid();
        if (scenario == "deleted") line.IsDeleted = true;
        var cost = Cost(); cost.PurchaseOrderItemId = scenario == "missing" ? Guid.NewGuid() : line.Id;
        Assert.Throws<ArgumentException>(() => PurchaseOrderLandedCostPlanService.ResolveTarget(cost, new[] { line }, po.Id, po.TenantId, true));
    }
    [Fact]
    public async Task StagesMixedScopesWithoutChangingCommercialTotal()
    {
        var plans = new Mock<IPurchaseOrderLandedCostPlanRepository>();
        var costs = new Mock<IPurchaseOrderLandedCostPlanItemRepository>();
        var partners = new Mock<IBusinessPartnerRepository>();
        var saved = new List<PurchaseOrderLandedCostPlanItem>();
        costs.Setup(r => r.AddAsync(It.IsAny<PurchaseOrderLandedCostPlanItem>()))
            .Callback<PurchaseOrderLandedCostPlanItem>(saved.Add).ReturnsAsync((PurchaseOrderLandedCostPlanItem c) => c);
        var shared = Cost(); var target = Cost(); target.PurchaseOrderLineIndex = 1;
        po.TotalAmount = 52000;
        var lines = new[] { Line(), Line() };
        await new PurchaseOrderLandedCostPlanService(plans.Object, costs.Object, partners.Object)
            .StageAsync(po, lines, new() { Currency = "GHS", Items = new() { shared, target } }, true);
        Assert.Null(saved[0].PurchaseOrderItemId);
        Assert.Equal(lines[1].Id, saved[1].PurchaseOrderItemId);
        Assert.Equal(52000, po.TotalAmount);
        Assert.Equal(200, saved.Sum(c => c.AmountInPlanCurrency));
    }
    [Fact]
    public async Task OmittedPlanDoesNotOrphanExistingTargetedCosts()
    {
        var plans = new Mock<IPurchaseOrderLandedCostPlanRepository>();
        plans.Setup(r => r.GetWithItemsByPurchaseOrderIdAsync(po.Id)).ReturnsAsync(new PurchaseOrderLandedCostPlan {
            TenantId = po.TenantId, Items = new List<PurchaseOrderLandedCostPlanItem> { new() { PurchaseOrderItemId = Guid.NewGuid() } } });
        var service = new PurchaseOrderLandedCostPlanService(plans.Object, Mock.Of<IPurchaseOrderLandedCostPlanItemRepository>(), Mock.Of<IBusinessPartnerRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.StageAsync(po, new[] { Line() }, null));
    }

    [Fact]
    public async Task EmptyReplacementClearsCostsButOmittedPlanDoesNot()
    {
        var plans = new Mock<IPurchaseOrderLandedCostPlanRepository>();
        var costs = new Mock<IPurchaseOrderLandedCostPlanItemRepository>();
        var plan = new PurchaseOrderLandedCostPlan { TenantId = po.TenantId, TotalPlannedCost = 100,
            Items = new List<PurchaseOrderLandedCostPlanItem> { new() { AmountInPlanCurrency = 100 } } };
        plans.Setup(r => r.GetWithItemsByPurchaseOrderIdAsync(po.Id)).ReturnsAsync(plan);
        var service = new PurchaseOrderLandedCostPlanService(plans.Object, costs.Object, Mock.Of<IBusinessPartnerRepository>());
        await service.StageAsync(po, new[] { Line() }, null);
        costs.Verify(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<PurchaseOrderLandedCostPlanItem>>()), Times.Never);
        await service.StageAsync(po, new[] { Line() }, new() { Currency = "GHS", Items = new() });
        costs.Verify(r => r.DeleteRangeAsync(It.Is<IEnumerable<PurchaseOrderLandedCostPlanItem>>(items => items.Count() == 1)), Times.Once);
        Assert.Equal(0, plan.TotalPlannedCost);
    }

    [Fact]
    public async Task InvalidReplacementDoesNotMutateExistingPlan()
    {
        var plans = new Mock<IPurchaseOrderLandedCostPlanRepository>();
        var costs = new Mock<IPurchaseOrderLandedCostPlanItemRepository>();
        var plan = new PurchaseOrderLandedCostPlan { TenantId = po.TenantId, Currency = "GHS", TotalPlannedCost = 100 };
        plans.Setup(r => r.GetWithItemsByPurchaseOrderIdAsync(po.Id)).ReturnsAsync(plan);
        var invalid = Cost(); invalid.PurchaseOrderItemId = Guid.NewGuid();
        var service = new PurchaseOrderLandedCostPlanService(plans.Object, costs.Object, Mock.Of<IBusinessPartnerRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.StageAsync(po, new[] { Line() },
            new() { Currency = "USD", Items = new() { Cost(), invalid } }));
        Assert.Equal("GHS", plan.Currency);
        Assert.Equal(100, plan.TotalPlannedCost);
        costs.Verify(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<PurchaseOrderLandedCostPlanItem>>()), Times.Never);
        costs.Verify(r => r.AddAsync(It.IsAny<PurchaseOrderLandedCostPlanItem>()), Times.Never);
    }
}
