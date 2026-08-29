using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class RfqQuoteRevisionTests
{
    [Fact]
    public void RevisionIsAllowedBeforeSubmissionDeadline()
    {
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

        var action = () => RfqService.EnsureQuoteRevisionOpen(now.AddMinutes(1), now);

        action.Should().NotThrow();
    }

    [Fact]
    public void RevisionIsBlockedAtSubmissionDeadline()
    {
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

        var action = () => RfqService.EnsureQuoteRevisionOpen(now, now);

        var error = action.Should().Throw<ProcurementRfqControlConflictException>().Which;
        error.Code.Should().Be("RFQ_QUOTE_REVISION_CLOSED");
    }

    [Fact]
    public void RevisionIsBlockedWhenSubmissionDeadlineIsMissing()
    {
        var action = () => RfqService.EnsureQuoteRevisionOpen(null, DateTime.UtcNow);

        action.Should().Throw<ProcurementRfqControlConflictException>()
            .Which.Code.Should().Be("RFQ_QUOTE_REVISION_CLOSED");
    }
}
