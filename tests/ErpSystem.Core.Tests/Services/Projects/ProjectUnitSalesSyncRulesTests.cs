using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public class ProjectUnitSalesSyncRulesTests
{
    [Fact]
    public void ResolveStatusFromAgreement_ShouldMarkActiveLeaseAgreementAsLeased()
    {
        var result = ProjectUnitSalesSyncRules.ResolveStatusFromAgreement(
            ProjectUnitStatuses.Reserved,
            isReleasedForMarket: true,
            SalesAgreementType.LeaseAgreement,
            SalesAgreementStatus.Active);

        result.Should().Be(ProjectUnitStatuses.Leased);
    }

    [Fact]
    public void ResolveStatusFromAgreement_ShouldReturnAvailableForReleasedTerminatedAgreement()
    {
        var result = ProjectUnitSalesSyncRules.ResolveStatusFromAgreement(
            ProjectUnitStatuses.Reserved,
            isReleasedForMarket: true,
            SalesAgreementType.General,
            SalesAgreementStatus.Terminated);

        result.Should().Be(ProjectUnitStatuses.Available);
    }

    [Fact]
    public void ResolveStatusFromOrder_ShouldMarkConfirmedPropertySaleAsSold()
    {
        var result = ProjectUnitSalesSyncRules.ResolveStatusFromOrder(
            ProjectUnitStatuses.Reserved,
            isReleasedForMarket: true,
            SalesOrderType.PropertySale,
            SalesOrderStatus.Confirmed);

        result.Should().Be(ProjectUnitStatuses.Sold);
    }

    [Fact]
    public void ResolveStatusFromOrder_ShouldFallBackToLeaseAgreementWhenOrderIsCancelled()
    {
        var result = ProjectUnitSalesSyncRules.ResolveStatusFromOrder(
            ProjectUnitStatuses.Reserved,
            isReleasedForMarket: true,
            SalesOrderType.PropertySale,
            SalesOrderStatus.Cancelled,
            SalesAgreementType.TenancyAgreement,
            SalesAgreementStatus.Active);

        result.Should().Be(ProjectUnitStatuses.Leased);
    }

    [Fact]
    public void ResolveStatusFromOrder_ShouldPreserveOccupiedUnit()
    {
        var result = ProjectUnitSalesSyncRules.ResolveStatusFromOrder(
            ProjectUnitStatuses.Occupied,
            isReleasedForMarket: true,
            SalesOrderType.PropertySale,
            SalesOrderStatus.Cancelled);

        result.Should().Be(ProjectUnitStatuses.Occupied);
    }
}
