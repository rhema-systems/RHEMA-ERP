using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowChecklistEvidenceValidatorTests
{
    [Fact]
    public void Validate_RequiresSatisfiedResponse_ForRequiredItem()
    {
        var checklist = new[] { ChecklistItem(isRequired: true, requiresDocument: false) };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, Array.Empty<WorkflowApprovalChecklistResponseDto>(), Array.Empty<WorkflowTaskAttachmentDto>());

        errors.Should().ContainSingle().Which.Should().Contain("must be satisfied");
    }

    [Fact]
    public void Validate_RequiresMatchingDocument_ForRequiredDocumentItem()
    {
        var checklist = new[] { ChecklistItem(isRequired: true, requiresDocument: true) };
        var responses = new[] { SatisfiedResponse() };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, Array.Empty<WorkflowTaskAttachmentDto>());

        errors.Should().ContainSingle().Which.Should().Contain("Attach 'Current Tax Clearance'");
    }

    [Fact]
    public void Validate_AcceptsEvidenceLinkedToChecklistItem()
    {
        var checklist = new[] { ChecklistItem(isRequired: true, requiresDocument: true) };
        var responses = new[] { SatisfiedResponse() };
        var attachments = new[]
        {
            new WorkflowTaskAttachmentDto
            {
                ChecklistItemId = "tax-clearance",
                RequirementKey = "tax-clearance",
                FileName = "clearance.pdf"
            }
        };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, attachments);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AcceptsEvidenceWithTheConfiguredDocumentName_WhenLegacyKeysDiffer()
    {
        var checklist = new[] { ChecklistItem(isRequired: true, requiresDocument: true) };
        var responses = new[] { SatisfiedResponse() };
        var attachments = new[]
        {
            new WorkflowTaskAttachmentDto
            {
                ChecklistItemId = "legacy-task-requirement",
                RequirementKey = "legacy-task-requirement",
                DocumentName = "Current Tax Clearance",
                FileName = "clearance.pdf"
            }
        };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, attachments);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DoesNotRequireEvidence_ForUncheckedOptionalItem()
    {
        var checklist = new[] { ChecklistItem(isRequired: false, requiresDocument: true) };
        var responses = new[] { SatisfiedResponse(isSatisfied: false) };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, Array.Empty<WorkflowTaskAttachmentDto>());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequiresEvidence_WhenOptionalDocumentItemIsChecked()
    {
        var checklist = new[] { ChecklistItem(isRequired: false, requiresDocument: true) };
        var responses = new[] { SatisfiedResponse() };
        var wrongItemAttachment = new[]
        {
            new WorkflowTaskAttachmentDto
            {
                ChecklistItemId = "different-item",
                RequirementKey = "different-item",
                FileName = "unrelated.pdf"
            }
        };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, wrongItemAttachment);

        errors.Should().ContainSingle().Which.Should().Contain("before this step can continue");
    }

    private static WorkflowQualityCheckDto ChecklistItem(bool isRequired, bool requiresDocument)
    {
        return new WorkflowQualityCheckDto
        {
            Id = "tax-clearance",
            Name = "Verify tax clearance",
            IsRequired = isRequired,
            RequiresDocument = requiresDocument,
            DocumentType = "Tax certificate",
            DocumentName = "Current Tax Clearance"
        };
    }

    private static WorkflowApprovalChecklistResponseDto SatisfiedResponse(bool isSatisfied = true)
    {
        return new WorkflowApprovalChecklistResponseDto
        {
            Id = "tax-clearance",
            Name = "Verify tax clearance",
            IsSatisfied = isSatisfied
        };
    }
}
