using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class AwardVerificationEvidencePolicyTests
{
    private static TenderAwardVerificationItemResult Review(bool requiresDocument = false)
    {
        var item = new TenderAwardVerificationItemResult
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), IsVerified = true, Status = "Passed",
            VerifiedById = Guid.NewGuid(), VerifiedDate = DateTime.UtcNow,
            Comments = "Reviewed the existing supplier and controlled bid records.",
            ChecklistItem = new() { ItemText = "Evidence check", IsRequired = true, IsActive = true, RequiresDocument = requiresDocument }
        };
        return item;
    }

    [Fact]
    public void AttributableReviewDoesNotRequireDuplicateUpload()
    {
        var item = Review();
        AwardVerificationEvidencePolicy.HasEvidence(item).Should().BeTrue();
        var validate = () => AwardVerificationEvidencePolicy.EnsureComplete([item]);
        validate.Should().NotThrow();
    }

    [Fact]
    public void ExplicitDocumentRequirementCannotBeReplacedWithNotes()
    {
        var item = Review(true);
        AwardVerificationEvidencePolicy.HasEvidence(item).Should().BeFalse();
        var validate = () => AwardVerificationEvidencePolicy.EnsureComplete([item]);
        validate.Should().Throw<InvalidOperationException>().WithMessage("AWARD_VERIFICATION_EVIDENCE_REQUIRED*attach the required document*");
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void OnlyRetainedSameTenantItemDocumentsSatisfyTheRule(bool deleted, bool foreignTenant, bool differentItem, bool expected)
    {
        var item = Review(true);
        item.Documents.Add(new() { Id = Guid.NewGuid(), TenantId = foreignTenant ? Guid.NewGuid() : item.TenantId,
            ItemResultId = differentItem ? Guid.NewGuid() : item.Id, FilePath = "dms://existing-controlled-version",
            IsDeleted = deleted });
        AwardVerificationEvidencePolicy.HasEvidence(item).Should().Be(expected);
    }

    [Theory]
    [InlineData("notes")]
    [InlineData("reviewer")]
    [InlineData("time")]
    [InlineData("verified")]
    public void MissingReviewEvidenceOrAttributionDoesNotPass(string missing)
    {
        var item = Review();
        if (missing == "notes") item.Comments = " ";
        if (missing == "reviewer") item.VerifiedById = null;
        if (missing == "time") item.VerifiedDate = null;
        if (missing == "verified") item.IsVerified = false;
        AwardVerificationEvidencePolicy.HasEvidence(item).Should().BeFalse();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("NotApplicable")]
    [InlineData("Unknown")]
    public void MandatoryChecksCannotBeSkipped(string status)
    {
        var item = Review(); item.Status = status;
        var validate = () => AwardVerificationEvidencePolicy.EnsureComplete([item]);
        validate.Should().Throw<InvalidOperationException>().WithMessage("AWARD_VERIFICATION_REQUIRED_CHECKS*");
    }
}
