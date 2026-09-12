using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed partial class ProcurementAwardReadinessServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FormalTenderWorkflowIsOptionalOnlyWhenSourcePersistedThatDecision(bool approvalRequired)
    {
        await using var fixture = new Fixture();
        await fixture.AddFormalControlledEvaluationAsync();
        var control = await fixture.Context.ProcurementTenderControls.SingleAsync();
        control.ApprovalRequired = approvalRequired;
        control.WorkflowInstanceId = null;
        control.ApprovedById = null;
        control.ApprovedAtUtc = null;
        control.AuthorityApprovalReference = null;
        control.ApprovalActorsJson = "[]";
        control.SubmittedForApprovalAtUtc = DateTime.UtcNow;
        control.SubmittedForApprovalById = Guid.NewGuid();
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAsync(ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id, fixture.Request($"optional-{approvalRequired}"), $"optional-{approvalRequired}");

        result.Authority.ApprovalRequired.Should().Be(approvalRequired);
        result.Authority.ApprovedByUserId.Should().BeNull();
        result.Authority.ApprovedAtUtc.Should().BeNull();
        var workflow = result.PrerequisiteGroups.SelectMany(group => group.Items)
            .Single(item => item.Code.StartsWith("AUTHORITY_WORKFLOW_"));
        workflow.Status.Should().Be(approvalRequired
            ? ProcurementAwardReadinessPrerequisiteStatus.Failed
            : ProcurementAwardReadinessPrerequisiteStatus.NotApplicable);
        result.PrerequisiteGroups.SelectMany(group => group.Items)
            .Should().Contain(item => item.Code == "CURRENT_TECHNICAL_SCORES")
            .And.Contain(item => item.Code == "CURRENT_FINANCIAL_SCORES");
        if (!approvalRequired)
            result.Evidence.Should().NotContain(item => item.RequirementKey == "TENDER_AUTHORITY_APPROVAL");
    }
}
