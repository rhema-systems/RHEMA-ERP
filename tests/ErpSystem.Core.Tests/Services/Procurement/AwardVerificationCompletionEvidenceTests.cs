using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class AwardVerificationCompletionEvidenceTests
{
    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, "", true)]
    [InlineData(false, "   ", true)]
    [InlineData(false, "Reviewed existing records", true)]
    [InlineData(true, null, false)]
    [InlineData(true, "Reviewed existing records", false)]
    public async Task BothCompletionBoundariesApplyTheSameEvidenceRule(bool requiresDocument, string? comments, bool allowed)
    {
        var tenant = Guid.NewGuid();
        var bidder = new TenderAwardVerificationBidder { Id = Guid.NewGuid(), TenantId = tenant, Status = "Passed" };
        bidder.ItemResults.Add(new() {
            Id = Guid.NewGuid(), TenantId = tenant, BidderId = bidder.Id, IsVerified = true, Status = "Passed",
            VerifiedById = Guid.NewGuid(), VerifiedDate = DateTime.UtcNow,
            Comments = comments,
            ChecklistItem = new() { IsActive = true, IsRequired = true, RequiresDocument = requiresDocument, ItemText = "Review" }
        });
        var verification = new TenderAwardVerification { Id = Guid.NewGuid(), Status = "InProgress", TenantId = tenant, Bidders = [bidder] };
        var bidders = new Mock<ITenderAwardVerificationBidderRepository>();
        bidders.Setup(repository => repository.GetByIdWithItemsAsync(bidder.Id)).ReturnsAsync(bidder);
        bidders.Setup(repository => repository.UpdateAsync(bidder)).ReturnsAsync(bidder);
        var verifications = new Mock<ITenderAwardVerificationRepository>();
        verifications.Setup(repository => repository.GetByIdWithDetailsAsync(verification.Id)).ReturnsAsync(verification);
        verifications.Setup(repository => repository.UpdateAsync(verification)).ReturnsAsync(verification);
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new AwardVerificationService(Mock.Of<IAwardVerificationChecklistTemplateRepository>(),
            Mock.Of<IAwardVerificationChecklistItemRepository>(), verifications.Object, bidders.Object,
            Mock.Of<ITenderAwardVerificationItemResultRepository>(), Mock.Of<ITenderAwardVerificationItemDocumentRepository>(),
            Mock.Of<ITenderRepository>(), Mock.Of<ITenderBidRepository>(), unitOfWork.Object,
            Mock.Of<ICurrentUserProvider>(), NullLogger<AwardVerificationService>.Instance);

        if (allowed)
        {
            (await service.CompleteBidderVerificationAsync(new CompleteBidderVerificationDto { BidderId = bidder.Id })).Status.Should().Be("Passed");
            (await service.CompleteVerificationAsync(verification.Id, new CompleteVerificationDto())).Status.Should().Be("Completed");
        }
        else
        {
            await service.Invoking(value => value.CompleteBidderVerificationAsync(new CompleteBidderVerificationDto { BidderId = bidder.Id }))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("AWARD_VERIFICATION_EVIDENCE_REQUIRED*");
            await service.Invoking(value => value.CompleteVerificationAsync(verification.Id, new CompleteVerificationDto()))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("AWARD_VERIFICATION_EVIDENCE_REQUIRED*");
            verification.Status.Should().Be("InProgress");
            verification.CompletedDate.Should().BeNull();
            unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
