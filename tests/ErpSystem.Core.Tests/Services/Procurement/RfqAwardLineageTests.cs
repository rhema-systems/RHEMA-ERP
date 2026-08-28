using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class RfqAwardLineageTests
{
    [Fact]
    public void WinnerTakesAllPersistsAwardTimestampAndSupplier()
    {
        var supplierId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var awardedAtUtc = new DateTime(2026, 8, 14, 12, 30, 0, DateTimeKind.Utc);
        var rfq = new RequestForQuotation();

        RfqService.ApplyAwardLineage(rfq, new[] { supplierId }, awardedAtUtc, actorId);

        rfq.Status.Should().Be("Awarded");
        rfq.AwardedAt.Should().Be(awardedAtUtc);
        rfq.AwardedBusinessPartnerId.Should().Be(supplierId);
        rfq.UpdatedAt.Should().Be(awardedAtUtc);
        rfq.LastModifiedById.Should().Be(actorId);
    }

    [Fact]
    public void SplitAwardPersistsTimestampWithoutMisrepresentingOneSupplierAsWinner()
    {
        var awardedAtUtc = new DateTime(2026, 8, 14, 12, 30, 0, DateTimeKind.Utc);
        var rfq = new RequestForQuotation();

        RfqService.ApplyAwardLineage(
            rfq,
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            awardedAtUtc,
            Guid.NewGuid());

        rfq.Status.Should().Be("Awarded");
        rfq.AwardedAt.Should().Be(awardedAtUtc);
        rfq.AwardedBusinessPartnerId.Should().BeNull();
    }
}
