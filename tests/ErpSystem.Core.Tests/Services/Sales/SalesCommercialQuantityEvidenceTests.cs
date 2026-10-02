using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Sales;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Sales;

public sealed class SalesCommercialQuantityEvidenceTests
{
    public static TheoryData<ICommercialQuantityEvidenceLine> SalesLineTypes => new()
    {
        new SalesOrderLine(),
        new DeliveryNoteLine(),
        new ReturnOrderLine()
    };

    [Theory]
    [MemberData(nameof(SalesLineTypes))]
    public async Task ValidateAndFreezeAsync_FreezesResolvedInventoryUomEvidence(
        ICommercialQuantityEvidenceLine line)
    {
        var uomId = Guid.NewGuid();
        var validator = new Mock<ICommercialQuantityPolicyValidator>();
        validator.Setup(value => value.ResolveAndValidateAsync(
                null, "ea", 2.5m, "Sales boundary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialQuantityEvidence(uomId, "EA", 1, 0.5m, 2.5m));

        await SalesCommercialQuantityEvidence.ValidateAndFreezeAsync(
            validator.Object, line, "ea", 2.5m, "Sales boundary");

        line.UnitOfMeasureId.Should().Be(uomId);
        line.UnitOfMeasureCodeSnapshot.Should().Be("EA");
        line.UnitOfMeasureDecimalPlacesSnapshot.Should().Be(1);
        line.UnitOfMeasureRoundingIncrementSnapshot.Should().Be(0.5m);
    }

    [Fact]
    public async Task ValidateAndFreezeAsync_PrefersStableIdAndRejectsSnapshotDrift()
    {
        var uomId = Guid.NewGuid();
        var line = new SalesOrderLine
        {
            UnitOfMeasureId = uomId,
            UnitOfMeasureCodeSnapshot = "EA",
            UnitOfMeasureDecimalPlacesSnapshot = 0,
            UnitOfMeasureRoundingIncrementSnapshot = 1m
        };
        var validator = new Mock<ICommercialQuantityPolicyValidator>();
        validator.Setup(value => value.ResolveAndValidateAsync(
                uomId, "EA", 3m, "Sales invoice generation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialQuantityEvidence(uomId, "EA", 2, 0.01m, 3m));

        var action = () => SalesCommercialQuantityEvidence.ValidateAndFreezeAsync(
            validator.Object, line, "legacy-code-is-not-used", 3m, "Sales invoice generation");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retained unit-of-measure evidence*");
        validator.Verify(value => value.ResolveAndValidateAsync(
            uomId, "EA", 3m, "Sales invoice generation", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndFreezeAsync_PropagatesCommercialPrecisionFailure()
    {
        var line = new DeliveryNoteLine { Unit = "BOX" };
        var validator = new Mock<ICommercialQuantityPolicyValidator>();
        validator.Setup(value => value.ResolveAndValidateAsync(
                null, "BOX", 1.25m, "Delivery confirm", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Quantity must be a whole multiple of 0.5."));

        var action = () => SalesCommercialQuantityEvidence.ValidateAndFreezeAsync(
            validator.Object, line, line.Unit, 1.25m, "Delivery confirm");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*whole multiple*");
    }
}
