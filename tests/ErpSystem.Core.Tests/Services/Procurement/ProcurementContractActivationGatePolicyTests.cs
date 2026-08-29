using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractActivationGatePolicyTests
{
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
}
