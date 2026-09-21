using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractActivationGatePolicyTests
{
    [Fact]
    public async Task EmptyEvidenceReachesTheConfiguredReadinessChecks()
    {
        // This validator checks supplied references; EvaluateAsync determines which
        // references the effective policy requires. No repository is needed for none.
        var constructor = typeof(ProcurementContractActivationService).GetConstructors().Single();
        var service = constructor.Invoke(new object?[constructor.GetParameters().Length]);
        var validate = typeof(ProcurementContractActivationService).GetMethod(
            "ValidateEvidenceAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = await (Task<List<ProcurementContractActivationEvidence>>)validate.Invoke(
            service, new object[] { new Contract(), Array.Empty<ProcurementContractActivationEvidenceRequest>(), CancellationToken.None })!;

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcurementContractActivationCheckStatus.Passed, true)]
    [InlineData(ProcurementContractActivationCheckStatus.NotRequired, true)]
    [InlineData(ProcurementContractActivationCheckStatus.Pending, false)]
    [InlineData(ProcurementContractActivationCheckStatus.Failed, false)]
    public void MissingOrInvalidConfiguredEvidenceStillBlocksReadiness(
        ProcurementContractActivationCheckStatus status, bool expected)
    {
        var ready = typeof(ProcurementContractActivationService).GetMethod(
            "IsReady", BindingFlags.NonPublic | BindingFlags.Static)!;
        var checks = new[] { new ProcurementContractActivationCheckDto {
            Key = "signed-contract", Label = "Signed contract", Status = status,
            Code = "CONTRACT_ACTIVATION_EVIDENCE_REQUIRED", IsRequired = true,
            Message = "Configured evidence check"
        } };

        ((bool)ready.Invoke(null, new object[] { checks })!).Should().Be(expected);
    }

    [Theory]
    [InlineData("PR_AUTHORITY_POLICY_NOT_EFFECTIVE")]
    [InlineData("PR_AUTHORITY_NOT_CONFIGURED")]
    public void AbsentOptionalAuthorityMetadataUsesContractWorkflow(string code)
    {
        ProcurementContractActivationService.AuthorityMetadataIsAbsent(code)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("PR_AUTHORITY_COVERAGE_GAP")]
    [InlineData("PR_AUTHORITY_CURRENCY_MISMATCH")]
    [InlineData("PR_AUTHORITY_ROUTE_AMBIGUOUS")]
    public void InvalidConfiguredAuthorityStillBlocks(string code)
    {
        ProcurementContractActivationService.AuthorityMetadataIsAbsent(code)
            .Should().BeFalse();
    }

    [Fact]
    public void ContractEvidenceIsConditionalOnConfiguredRequirements()
    {
        var evidenceProperty = typeof(SubmitProcurementContractActivationRequest)
            .GetProperty(nameof(SubmitProcurementContractActivationRequest.Evidence));

        evidenceProperty.Should().NotBeNull();
        evidenceProperty!.GetCustomAttributes(typeof(MinLengthAttribute), true)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("PROCUREMENT_CONTRACT", "Procurement Contract", true)]
    [InlineData("ProcurementContract", "Procurement Contract", true)]
    [InlineData("PURCHASE_REQUISITION", "Purchase Requisition", false)]
    public void OnlyContractEntityWorkflowCanExecuteContractActivation(
        string code,
        string name,
        bool expected)
    {
        var workflow = ValidWorkflow(code, name);

        ProcurementContractActivationService.IsContractWorkflowEntityType(workflow)
            .Should().Be(expected);
    }

    [Fact]
    public void AuthorityWorkflowMetadataMustIdentifyAConcretePublishedVersion()
    {
        var valid = ValidWorkflow("PURCHASE_REQUISITION", "Purchase Requisition");
        var missingEntityType = new ProcurementAuthorityWorkflowSelectionDto
        {
            WorkflowDefinitionId = valid.WorkflowDefinitionId,
            DefinitionKey = valid.DefinitionKey,
            Name = valid.Name,
            Version = valid.Version,
            PublishedAt = valid.PublishedAt
        };
        var unpublished = new ProcurementAuthorityWorkflowSelectionDto
        {
            WorkflowDefinitionId = valid.WorkflowDefinitionId,
            DefinitionKey = valid.DefinitionKey,
            Name = valid.Name,
            Version = valid.Version,
            EntityTypeCode = valid.EntityTypeCode,
            EntityTypeName = valid.EntityTypeName
        };

        ProcurementContractActivationService.HasValidAuthorityWorkflowMetadata(valid)
            .Should().BeTrue();
        ProcurementContractActivationService.HasValidAuthorityWorkflowMetadata(missingEntityType)
            .Should().BeFalse();
        ProcurementContractActivationService.HasValidAuthorityWorkflowMetadata(unpublished)
            .Should().BeFalse();
    }

    private static ProcurementAuthorityWorkflowSelectionDto ValidWorkflow(
        string entityTypeCode,
        string entityTypeName) => new()
    {
        WorkflowDefinitionId = Guid.NewGuid(),
        DefinitionKey = Guid.NewGuid(),
        Name = "Authority approval",
        Version = 2,
        EntityTypeCode = entityTypeCode,
        EntityTypeName = entityTypeName,
        PublishedAt = DateTime.UtcNow.AddMinutes(-1)
    };
}
