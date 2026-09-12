using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed partial class ProcurementAwardReadinessServiceTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task ReadinessHonoursTheExplicitDocumentRule(bool requiresDocument, bool withDocument, bool expectedReady)
    {
        await using var fixture = new Fixture();
        fixture.Evaluation.Status = "Submitted";
        fixture.Evaluation.SubmittedDate = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();
        await fixture.AddLockedScoreAttemptAsync();
        await fixture.AddFailedVerificationAsync();
        var item = await fixture.Context.Set<TenderAwardVerificationItemResult>().SingleAsync();
        var bidder = await fixture.Context.Set<TenderAwardVerificationBidder>().SingleAsync();
        var checklist = await fixture.Context.Set<AwardVerificationChecklistItem>().SingleAsync();
        checklist.RequiresDocument = requiresDocument;
        bidder.Status = "Passed";
        item.Status = "Passed";
        item.VerifiedById = Guid.NewGuid();
        item.Comments = "Reviewed existing controlled supplier and bid evidence.";
        if (withDocument) fixture.Context.Add(new TenderAwardVerificationItemDocument {
            Id = Guid.NewGuid(), TenantId = item.TenantId, ItemResultId = item.Id,
            FileName = "existing-evidence.pdf", FilePath = "dms://existing-version"
        });
        await fixture.Context.SaveChangesAsync();
        // Review follows the configured rule; changing a rule after completion must remain stale.
        var verification = await fixture.Context.Set<TenderAwardVerification>().SingleAsync();
        verification.CompletedDate = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();

        var decision = await fixture.Service.EvaluateAsync(ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id, fixture.Request("evidence-rule"), "evidence-rule");
        decision.IsReady.Should().Be(expectedReady, string.Join("; ", decision.BlockedReasons));
        decision.PrerequisiteGroups.SelectMany(group => group.Items)
            .Single(check => check.Code == "AWARD_VERIFICATION_PASSED").Status.Should().Be(expectedReady
                ? ProcurementAwardReadinessPrerequisiteStatus.Passed : ProcurementAwardReadinessPrerequisiteStatus.Failed);
    }
}
