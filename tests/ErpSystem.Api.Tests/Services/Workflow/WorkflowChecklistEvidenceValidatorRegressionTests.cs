using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Workflow;

public sealed class WorkflowChecklistEvidenceValidatorRegressionTests
{
    [Fact]
    public void Validate_AcceptsConfiguredDocumentName_WhenLegacyRequirementKeysDiffer()
    {
        var checklist = new[]
        {
            new WorkflowQualityCheckDto
            {
                Id = "legacy-document-check",
                Name = "Attach Zoning / Planning Clearance",
                IsRequired = true,
                RequiresDocument = true,
                DocumentName = "Zoning / Planning Clearance"
            }
        };
        var responses = new[]
        {
            new WorkflowApprovalChecklistResponseDto
            {
                Id = "legacy-document-check",
                Name = "Attach Zoning / Planning Clearance",
                IsSatisfied = true
            }
        };
        var attachments = new[]
        {
            new WorkflowTaskAttachmentDto
            {
                RequirementKey = "zoning-planning-clearance",
                DocumentName = "Zoning / Planning Clearance",
                FileName = "Zoning Planning Clearance.txt"
            }
        };

        var errors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, attachments);

        errors.Should().BeEmpty();
    }
}
