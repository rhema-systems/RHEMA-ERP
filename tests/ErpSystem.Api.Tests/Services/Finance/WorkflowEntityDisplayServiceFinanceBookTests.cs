using System.Linq.Expressions;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class WorkflowEntityDisplayServiceFinanceBookTests
{
    [Fact]
    public async Task AccountingBookInitialization_ShouldResolveReadinessDeepLink()
    {
        var book = new AccountingBook { Id = Guid.NewGuid(), Code = "IFRS", Name = "IFRS Primary" };
        var initialization = new AccountingBookInitialization
        {
            Id = Guid.NewGuid(), AccountingBookId = book.Id, AccountingBook = book, Version = 3
        };
        var repository = new Mock<IGenericRepository<AccountingBookInitialization>>();
        repository.Setup(value => value.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AccountingBookInitialization, bool>>>(),
                It.IsAny<Expression<Func<AccountingBookInitialization, object>>[]>()))
            .ReturnsAsync(initialization);
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(value => value.Repository<AccountingBookInitialization>()).Returns(repository.Object);

        var result = await CreateService(unit.Object)
            .GetEntityDisplayInfoAsync("AccountingBookInitialization", initialization.Id);

        result.EntityNumber.Should().Be("IFRS/V3");
        result.EntityName.Should().Be("IFRS Primary");
        result.ActionUrl.Should().Be($"/finance/settings/accounting-books/{book.Id:D}/readiness");
    }

    [Fact]
    public async Task HistoricalAccountingBookApplicabilityPolicy_ShouldResolveBookRegister()
    {
        var policy = new AccountingBookApplicabilityPolicy
        {
            Id = Guid.NewGuid(), PolicyCode = "FIN_DEFAULT", Version = 2, Name = "Finance defaults"
        };
        var repository = new Mock<IGenericRepository<AccountingBookApplicabilityPolicy>>();
        repository.Setup(value => value.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AccountingBookApplicabilityPolicy, bool>>>()))
            .ReturnsAsync(policy);
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(value => value.Repository<AccountingBookApplicabilityPolicy>()).Returns(repository.Object);

        var result = await CreateService(unit.Object)
            .GetEntityDisplayInfoAsync("AccountingBookApplicabilityPolicy", policy.Id);

        result.EntityNumber.Should().Be("FIN_DEFAULT/V2");
        result.EntityName.Should().Be("Finance defaults");
        result.ActionUrl.Should().Be("/finance/settings/accounting-books");
    }

    private static WorkflowEntityDisplayService CreateService(IUnitOfWork unitOfWork) => new(
        unitOfWork,
        Mock.Of<IPurchaseRequisitionRepository>(),
        Mock.Of<IPurchaseOrderRepository>(),
        Mock.Of<ITenderRepository>(),
        Mock.Of<IProcurementPlanRepository>(),
        Mock.Of<IProjectRepository>(),
        Mock.Of<IBusinessPartnerRepository>(),
        Mock.Of<IJobCardRepository>(),
        Mock.Of<IInventoryTransferRepository>(),
        Mock.Of<IInventoryRequisitionRepository>(),
        NullLogger<WorkflowEntityDisplayService>.Instance);
}
