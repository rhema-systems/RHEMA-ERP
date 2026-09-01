using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDesignWorkflowRulesTests
{
    [Fact]
    public void Route_is_complete_ordered_and_has_no_duplicate_direct_mutation_path()
    {
        var transitions = CivilEngineeringDesignWorkflowRules.Transitions;

        transitions.Should().HaveCount(11);
        transitions.Select(value => $"{value.FromStage}:{value.Action}")
            .Should().OnlyHaveUniqueItems();
        transitions.Should().Contain(value =>
            value.FromStage == CivilEngineeringDesignStages.DraftDirective
            && value.Action == CivilEngineeringDesignAction.DirectToSce
            && value.ToStage == CivilEngineeringDesignStages.SceInformationGathering);
        transitions.Should().Contain(value =>
            value.FromStage == CivilEngineeringDesignStages.SceInformationGathering
            && value.Action == CivilEngineeringDesignAction.AssignCivilEngineer
            && value.ToStage == CivilEngineeringDesignStages.CivilEngineerDesign);
        transitions.Should().Contain(value =>
            value.FromStage == CivilEngineeringDesignStages.CivilEngineerDesign
            && value.Action == CivilEngineeringDesignAction.SubmitDesign
            && value.ToStage == CivilEngineeringDesignStages.SceDesignReview);
        transitions.Should().Contain(value =>
            value.FromStage == CivilEngineeringDesignStages.SceDesignReview
            && value.Action == CivilEngineeringDesignAction.AssignDraftsman
            && value.ToStage == CivilEngineeringDesignStages.Drafting);
        transitions.Should().Contain(value =>
            value.FromStage == CivilEngineeringDesignStages.Drafting
            && value.Action == CivilEngineeringDesignAction.SubmitDrawings
            && value.ToStage == CivilEngineeringDesignStages.SceDrawingReview);
        transitions.Should().Contain(value =>
            value.FromStage == CivilEngineeringDesignStages.SceDrawingReview
            && value.Action == CivilEngineeringDesignAction.SubmitPackage
            && value.ToStage == CivilEngineeringDesignStages.HodFinalReview);
    }

    [Fact]
    public void Assignments_are_controlled_and_review_returns_require_reasons()
    {
        var civil = CivilEngineeringDesignWorkflowRules.GetRequired(
            CivilEngineeringDesignStages.SceInformationGathering,
            CivilEngineeringDesignAction.AssignCivilEngineer);
        var drafting = CivilEngineeringDesignWorkflowRules.GetRequired(
            CivilEngineeringDesignStages.SceDesignReview,
            CivilEngineeringDesignAction.AssignDraftsman);
        var returnDesign = CivilEngineeringDesignWorkflowRules.GetRequired(
            CivilEngineeringDesignStages.SceDesignReview,
            CivilEngineeringDesignAction.ReturnDesign);
        var returnDrawing = CivilEngineeringDesignWorkflowRules.GetRequired(
            CivilEngineeringDesignStages.SceDrawingReview,
            CivilEngineeringDesignAction.ReturnDrawings);

        civil.AssigneeRole.Should().Be(CivilEngineeringAccessControlRegistry.CivilEngineerRole);
        civil.RequiresReason.Should().BeTrue();
        drafting.AssigneeRole.Should().Be(CivilEngineeringAccessControlRegistry.DraftsmanRole);
        drafting.RequiresReason.Should().BeTrue();
        returnDesign.RequiresReason.Should().BeTrue();
        returnDrawing.RequiresReason.Should().BeTrue();
    }

    [Fact]
    public void Design_submission_starts_package_submission_advances_and_hod_decides_shared_workflow()
    {
        var starts = CivilEngineeringDesignWorkflowRules.Transitions
            .Where(value => value.StartsSharedWorkflow).ToList();
        var advances = CivilEngineeringDesignWorkflowRules.Transitions
            .Where(value => value.AdvancesSharedWorkflow).ToList();
        var completes = CivilEngineeringDesignWorkflowRules.Transitions
            .Where(value => value.CompletesSharedWorkflow).ToList();

        starts.Should().ContainSingle().Which.Action.Should().Be(CivilEngineeringDesignAction.SubmitDesign);
        advances.Should().ContainSingle().Which.Action.Should().Be(CivilEngineeringDesignAction.SubmitPackage);
        advances.Should().OnlyContain(value => value.Actor == CivilEngineeringDesignActor.SupervisingCivilEngineer);
        completes.Select(value => value.Action).Should().BeEquivalentTo([
            CivilEngineeringDesignAction.Approve,
            CivilEngineeringDesignAction.Reject]);
        completes.Should().OnlyContain(value => value.Actor == CivilEngineeringDesignActor.Hod);
    }

    [Fact]
    public void Configured_shared_workflow_requires_all_sce_checks_and_hod_authority()
    {
        var definition = Workflow(
            CivilEngineeringAccessControlRegistry.SupervisingEngineerRole,
            CivilEngineeringDesignReviewChecklistPolicy.RequiredChecks
                .Select(item => new WorkflowQualityCheckDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    IsRequired = true
                })
                .ToList(),
            CivilEngineeringAccessControlRegistry.HeadRole);

        CivilEngineeringDesignReviewChecklistPolicy.Validate(definition, requireHodApproval: true)
            .Should().BeEmpty();
    }

    [Fact]
    public void Configured_shared_workflow_rejects_optional_missing_or_wrong_authority_controls()
    {
        var checks = CivilEngineeringDesignReviewChecklistPolicy.RequiredChecks
            .Where(item => item.Id != "civil-calculations")
            .Select(item => new WorkflowQualityCheckDto
            {
                Id = item.Id,
                Name = item.Name,
                IsRequired = item.Id != "civil-specifications"
            })
            .ToList();
        var definition = Workflow("WRONG_SCE_ROLE", checks, "WRONG_HOD_ROLE");

        var errors = CivilEngineeringDesignReviewChecklistPolicy.Validate(
            definition,
            requireHodApproval: true);

        errors.Should().Contain(message => message.Contains("TDC_SUPERVISING_CIVIL_ENGINEER"));
        errors.Should().Contain(message => message.Contains("civil-calculations"));
        errors.Should().Contain(message => message.Contains("civil-specifications") && message.Contains("required"));
        errors.Should().Contain(message => message.Contains("TDC_HEAD_OF_CIVIL_ENGINEERING"));
    }

    [Fact]
    public void Sce_checklist_responses_must_belong_to_current_reviewer_and_current_evidence()
    {
        var reviewer = Guid.NewGuid();
        var evidencePublishedAt = DateTime.UtcNow.AddMinutes(-2);
        var responses = CivilEngineeringDesignReviewChecklistPolicy.RequiredChecks
            .Select(item => new WorkflowApprovalChecklistResponseDto
            {
                Id = item.Id,
                Name = item.Name,
                IsSatisfied = true,
                CompletedById = reviewer,
                CompletedAt = evidencePublishedAt.AddMinutes(1)
            })
            .ToList();

        CivilEngineeringDesignReviewChecklistPolicy.ValidateCurrentResponses(
                responses,
                reviewer,
                evidencePublishedAt)
            .Should().BeEmpty();

        responses.Single(item => item.Id == "civil-drawings").CompletedAt =
            evidencePublishedAt.AddSeconds(-1);
        CivilEngineeringDesignReviewChecklistPolicy.ValidateCurrentResponses(
                responses,
                reviewer,
                evidencePublishedAt)
            .Should().ContainSingle(message => message.Contains("Drawings reviewed"));

        responses.Single(item => item.Id == "civil-drawings").CompletedAt =
            evidencePublishedAt.AddMinutes(1);
        responses.Single(item => item.Id == "civil-design").CompletedById = Guid.NewGuid();
        CivilEngineeringDesignReviewChecklistPolicy.ValidateCurrentResponses(
                responses,
                reviewer,
                evidencePublishedAt)
            .Should().ContainSingle(message => message.Contains("Design reviewed"));
    }

    [Theory]
    [InlineData(CivilEngineeringDesignStages.DraftDirective, CivilEngineeringDesignAction.Approve)]
    [InlineData(CivilEngineeringDesignStages.CivilEngineerDesign, CivilEngineeringDesignAction.SubmitDrawings)]
    [InlineData(CivilEngineeringDesignStages.HodFinalReview, CivilEngineeringDesignAction.ReturnPackage)]
    [InlineData(CivilEngineeringDesignStages.Approved, CivilEngineeringDesignAction.SubmitPackage)]
    public void Direct_out_of_sequence_actions_are_rejected(string stage, CivilEngineeringDesignAction action)
    {
        var act = () => CivilEngineeringDesignWorkflowRules.GetRequired(stage, action);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{action}*{stage}*");
    }

    [Fact]
    public void Maker_reviewer_assignments_must_be_distinct()
    {
        var hod = Guid.NewGuid();
        var sce = Guid.NewGuid();
        var civil = Guid.NewGuid();
        var draftsman = Guid.NewGuid();

        Action valid = () => CivilEngineeringDesignWorkflowRules.EnsureDistinctAssignments(
            hod, sce, civil, draftsman);
        Action invalid = () => CivilEngineeringDesignWorkflowRules.EnsureDistinctAssignments(
            hod, sce, civil, civil);

        valid.Should().NotThrow();
        invalid.Should().Throw<InvalidOperationException>()
            .WithMessage("*different users*");
    }

    [Fact]
    public void Evidence_types_are_closed_controlled_values()
    {
        CivilEngineeringDesignEvidenceTypes.All.Should().BeEquivalentTo([
            CivilEngineeringDesignEvidenceTypes.Directive,
            CivilEngineeringDesignEvidenceTypes.Reconnaissance,
            CivilEngineeringDesignEvidenceTypes.Design,
            CivilEngineeringDesignEvidenceTypes.Drawing,
            CivilEngineeringDesignEvidenceTypes.SubmissionPackage]);
        CivilEngineeringDesignWorkflowRules.Transitions
            .Where(value => value.RequiredEvidenceType is not null)
            .Select(value => value.RequiredEvidenceType!)
            .Should().OnlyContain(value => CivilEngineeringDesignEvidenceTypes.All.Contains(value));
    }

    private static WorkflowDefinition Workflow(
        string sceRole,
        List<WorkflowQualityCheckDto> checks,
        string hodRole)
    {
        var definition = new WorkflowDefinition();
        definition.Steps.Add(new WorkflowStep
        {
            Name = "SCE design review",
            Order = 10,
            StepType = WorkflowStepType.Approval,
            RequiredRole = sceRole,
            Configuration = JsonSerializer.Serialize(new WorkflowStepConfigurationDto
            {
                QualityConfig = new WorkflowQualityConfigDto { QualityChecks = checks }
            })
        });
        definition.Steps.Add(new WorkflowStep
        {
            Name = "HOD final review",
            Order = 20,
            StepType = WorkflowStepType.Approval,
            RequiredRole = hodRole
        });
        return definition;
    }
}
