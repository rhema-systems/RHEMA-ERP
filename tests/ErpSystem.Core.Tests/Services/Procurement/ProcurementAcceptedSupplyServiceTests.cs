using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAcceptedSupplyServiceTests
{
    [Theory]
    [InlineData(ProcurementCategoryClass.Goods, "No governed goods receipt")]
    [InlineData(ProcurementCategoryClass.TechnicalServices, "No approved, documented Project deliverable")]
    [InlineData(ProcurementCategoryClass.Works, "Works invoices are created only through")]
    public async Task OptionsRouteEachCategoryToItsExistingAuthoritativeOwner(
        ProcurementCategoryClass category,
        string expectedReason)
    {
        var tenantId = Guid.NewGuid();
        var order = Order(tenantId, category);
        await using var context = Context(tenantId);
        context.Add(order);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var result = await Service(unitOfWork, tenantId).GetOptionsAsync(order.Id);

        result.Category.Should().Be(category);
        result.Ready.Should().BeFalse();
        result.BlockedReasons.Should().ContainSingle()
            .Which.Should().Contain(expectedReason);
        if (category == ProcurementCategoryClass.Works)
            result.WorksHandoffRoute.Should().Be("/quantity-survey/payment-certificates");
    }

    [Fact]
    public async Task OptionsFailClosedForAnotherTenantsPurchaseOrder()
    {
        var ownerTenantId = Guid.NewGuid();
        var requesterTenantId = Guid.NewGuid();
        var order = Order(ownerTenantId, ProcurementCategoryClass.Goods);
        await using var context = Context(requesterTenantId);
        context.Add(order);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var action = () => Service(unitOfWork, requesterTenantId).GetOptionsAsync(order.Id);

        var exception = await action.Should()
            .ThrowAsync<ProcurementAcceptedSupplyValidationException>();
        exception.Which.Code.Should().Be("ACCEPTED_SUPPLY_PO_NOT_FOUND");
    }

    [Fact]
    public async Task OptionsFailClosedWhenGovernedCategorySnapshotIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var order = Order(tenantId, null);
        await using var context = Context(tenantId);
        context.Add(order);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var action = () => Service(unitOfWork, tenantId).GetOptionsAsync(order.Id);

        var exception = await action.Should()
            .ThrowAsync<ProcurementAcceptedSupplyValidationException>();
        exception.Which.Code.Should().Be("PURCHASE_ORDER_CATEGORY_REQUIRED");
    }

    private static PurchaseOrder Order(Guid tenantId, ProcurementCategoryClass? category) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OrderNumber = $"PO-{Guid.NewGuid():N}",
        BusinessPartnerId = Guid.NewGuid(),
        ProcurementCategory = category,
        Currency = "GHS"
    };

    private static ApplicationDbContext Context(Guid tenantId) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options,
        tenantId);

    private static ProcurementAcceptedSupplyService Service(
        IUnitOfWork unitOfWork,
        Guid tenantId)
    {
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.IsAuthenticated).Returns(true);
        return new ProcurementAcceptedSupplyService(unitOfWork, current.Object);
    }
}
