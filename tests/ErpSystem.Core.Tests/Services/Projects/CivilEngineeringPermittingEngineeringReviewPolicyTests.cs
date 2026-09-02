using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringPermittingEngineeringReviewPolicyTests
{
    [Fact]
    public void Review_requires_controlled_retry_category_comment_and_current_dms_evidence()
    {
        var errors = CivilEngineeringPermittingEngineeringReviewPolicy.ValidateSubmit(new SubmitCivilEngineeringPermittingEngineeringReviewRequest());

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("comment category", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("review comment", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("Published central-DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Review_requires_hod_decision_except_for_a_correction_request()
    {
        CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(CivilEngineeringPermittingOutcome.ReturnForCorrection).Should().BeFalse();
        CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(CivilEngineeringPermittingOutcome.RecommendApproval).Should().BeTrue();
        CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(CivilEngineeringPermittingOutcome.RecommendApprovalWithConditions).Should().BeTrue();
        CivilEngineeringPermittingEngineeringReviewPolicy.NeedsHodDecision(CivilEngineeringPermittingOutcome.RecommendRejection).Should().BeTrue();
    }
}
