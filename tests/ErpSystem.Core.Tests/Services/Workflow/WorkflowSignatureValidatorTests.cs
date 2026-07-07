using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class WorkflowSignatureValidatorTests
{
    [Fact]
    public void Validate_RequiresConfiguredAttestationAndSigningRole()
    {
        var now = DateTime.UtcNow;
        var policy = new WorkflowSignaturePolicyDto
        { IsRequired = true, Method = WorkflowSignatureMethod.Attestation, RequiredSigningRole = "FinanceApprover", AttestationText = "Approved." };
        var payload = new { signature = new WorkflowSignatureSubmissionDto
        { Method = WorkflowSignatureMethod.Attestation, Attestation = "Approved.", SignedAt = now } };

        WorkflowSignatureValidator.Validate(policy, ["FinanceApprover"], payload, now).Should().BeEmpty();
        WorkflowSignatureValidator.Validate(policy, ["Requester"], payload, now).Should().ContainSingle(message => message.Contains("FinanceApprover"));
    }

    [Fact]
    public void Validate_RejectsMissingSignature()
    {
        var policy = new WorkflowSignaturePolicyDto { IsRequired = true };
        WorkflowSignatureValidator.Validate(policy, [], null, DateTime.UtcNow).Should().ContainSingle();
    }
}
