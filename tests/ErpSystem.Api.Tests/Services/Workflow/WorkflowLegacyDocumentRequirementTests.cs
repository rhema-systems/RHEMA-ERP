using System.Reflection;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Workflow;

public sealed class WorkflowLegacyDocumentRequirementTests
{
    [Fact]
    public void LegacySingleDocumentTask_WithoutARequirementKey_RemainsWildcard()
    {
        var requirements = GetRequirements(new WorkflowTaskConfigDto
        {
            RequiresDocument = true,
            DocumentName = "Legacy stage evidence",
            DocumentRequirementKey = null
        });

        requirements.Should().ContainSingle();
        requirements[0].RequirementKey.Should().BeEmpty();
        requirements[0].IsRequired.Should().BeTrue();
    }

    [Fact]
    public void SingleDocumentTask_WithAnExplicitRequirementKey_RemainsKeyed()
    {
        var requirements = GetRequirements(new WorkflowTaskConfigDto
        {
            RequiresDocument = true,
            DocumentName = "Survey plan",
            DocumentRequirementKey = "survey-plan"
        });

        requirements.Should().ContainSingle();
        requirements[0].RequirementKey.Should().Be("survey-plan");
    }

    private static List<WorkflowDocumentRequirementDto> GetRequirements(WorkflowTaskConfigDto config)
    {
        var method = typeof(WorkflowEngine).GetMethod(
            "GetTaskDocumentRequirements",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        return method!.Invoke(null, [config]).Should()
            .BeOfType<List<WorkflowDocumentRequirementDto>>()
            .Subject;
    }
}
