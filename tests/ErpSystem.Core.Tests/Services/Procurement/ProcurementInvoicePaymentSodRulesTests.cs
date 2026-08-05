using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementInvoicePaymentSodRulesTests
{
    [Fact]
    public void SameInvoiceProcessorAndPaymentApproverConflicts()
    {
        var actor = Guid.NewGuid();

        ProcurementInvoicePaymentSodRules.HasConflict(actor, actor).Should().BeTrue();
        ProcurementInvoicePaymentSodRules.HasConflict(actor, Guid.NewGuid()).Should().BeFalse();
        ProcurementInvoicePaymentSodRules.HasConflict(null, actor).Should().BeFalse();
    }

    [Fact]
    public void RuleUsesExistingRequiredControlAndCompleteDecisionRegister()
    {
        ProcurementInvoicePaymentSodRules.ControlCode.Should().Be("SOD-INVOICE-PROCESSOR-PAYMENT");
        ProcurementInvoicePaymentSodRules.RequirementCode.Should().Be("AP-004");
        ProcurementInvoicePaymentSodRules.RuleVersion.Should().Be("TDC-0506");
        ProcurementInvoicePaymentSodRules.DecisionKeys.Should()
            .Equal(Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}"));
    }
}
