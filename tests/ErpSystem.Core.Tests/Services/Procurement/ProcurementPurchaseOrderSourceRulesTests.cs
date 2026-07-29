using System.Text.Json;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceRulesTests
{
    [Theory]
    [InlineData(ProcurementPurchaseOrderSourceType.RfqAward)]
    [InlineData(ProcurementPurchaseOrderSourceType.TenderAward)]
    [InlineData(ProcurementPurchaseOrderSourceType.Contract)]
    [InlineData(ProcurementPurchaseOrderSourceType.ApprovedException)]
    public void OrdinaryPurchaseOrderSourcesAreExplicit(
        ProcurementPurchaseOrderSourceType sourceType)
    {
        ProcurementPurchaseOrderSourceRules.CanCreate(sourceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(ProcurementPurchaseOrderSourceType.FrameworkCallOff)]
    [InlineData(ProcurementPurchaseOrderSourceType.HistoricalMigration)]
    public void DedicatedAndMigrationSourcesCannotUseOrdinaryCreation(
        ProcurementPurchaseOrderSourceType sourceType)
    {
        ProcurementPurchaseOrderSourceRules.CanCreate(sourceType).Should().BeFalse();
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Pending Approval")]
    [InlineData("Approved")]
    [InlineData("Sent")]
    [InlineData("Acknowledged")]
    public void ApprovalAndIssueBoundariesRequireRevalidation(string status)
    {
        ProcurementPurchaseOrderSourceRules.RequiresRevalidation(status)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("PartiallyReceived")]
    [InlineData("Received")]
    [InlineData("Cancelled")]
    public void ReceivingAndTerminalStatusAreOutsideThisSourceGate(string status)
    {
        ProcurementPurchaseOrderSourceRules.RequiresRevalidation(status)
            .Should().BeFalse();
    }

    [Fact]
    public void AwardReadinessSupplierMustBeAnExactGuidMember()
    {
        var supplier = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new[] { supplier, Guid.NewGuid() });

        ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(json, supplier)
            .Should().BeTrue();
        ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(
                json, Guid.NewGuid())
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not-json")]
    public void MalformedSupplierLineageFailsClosed(string json)
    {
        ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(
                json, Guid.NewGuid())
            .Should().BeFalse();
    }
}
