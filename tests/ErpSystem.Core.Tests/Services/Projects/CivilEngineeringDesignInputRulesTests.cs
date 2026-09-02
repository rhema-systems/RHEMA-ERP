using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDesignInputRulesTests
{
    [Fact]
    public void Request_creation_is_limited_to_information_gathering_with_a_future_due_date()
    {
        var now = DateTime.UtcNow;

        Action valid = () => CivilEngineeringDesignInputRules.EnsureCanCreate(
            CivilEngineeringDesignStages.SceInformationGathering,
            now.AddDays(1),
            now);
        Action wrongStage = () => CivilEngineeringDesignInputRules.EnsureCanCreate(
            CivilEngineeringDesignStages.CivilEngineerDesign,
            now.AddDays(1),
            now);
        Action pastDue = () => CivilEngineeringDesignInputRules.EnsureCanCreate(
            CivilEngineeringDesignStages.SceInformationGathering,
            now.AddSeconds(-1),
            now);

        valid.Should().NotThrow();
        wrongStage.Should().Throw<InvalidOperationException>().WithMessage("*information gathering*");
        pastDue.Should().Throw<InvalidOperationException>().WithMessage("*future*");
    }

    [Fact]
    public void Response_and_review_follow_the_existing_project_rfi_lifecycle()
    {
        Action respondSubmitted = () => CivilEngineeringDesignInputRules.EnsureCanRespond(ProjectRfiStatuses.Submitted);
        Action respondClosed = () => CivilEngineeringDesignInputRules.EnsureCanRespond(ProjectRfiStatuses.Closed);

        respondSubmitted.Should().NotThrow();
        respondClosed.Should().Throw<InvalidOperationException>();
        CivilEngineeringDesignInputRules.ReviewStatus(
                ProjectRfiStatuses.Answered,
                CivilEngineeringDesignInputReviewAction.Accept)
            .Should().Be(ProjectRfiStatuses.Closed);
        CivilEngineeringDesignInputRules.ReviewStatus(
                ProjectRfiStatuses.Answered,
                CivilEngineeringDesignInputReviewAction.Return)
            .Should().Be(ProjectRfiStatuses.Submitted);
    }

    [Theory]
    [InlineData(false, ProjectRfiStatuses.Submitted, true)]
    [InlineData(true, ProjectRfiStatuses.Submitted, false)]
    [InlineData(true, ProjectRfiStatuses.Answered, false)]
    [InlineData(true, ProjectRfiStatuses.Closed, true)]
    public void Required_inputs_block_design_readiness_until_accepted(
        bool blocksReadiness,
        string status,
        bool expected)
    {
        CivilEngineeringDesignInputRules.IsReady(blocksReadiness, status).Should().Be(expected);
    }

    [Fact]
    public void Request_priority_is_a_closed_controlled_set()
    {
        CivilEngineeringDesignInputRules.Priorities.Should().BeEquivalentTo([
            ProjectRfiPriorities.Low,
            ProjectRfiPriorities.Medium,
            ProjectRfiPriorities.High,
            ProjectRfiPriorities.Critical]);
    }
}
