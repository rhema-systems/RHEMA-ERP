using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptDocumentRulesTests
{
    [Theory]
    [InlineData(ProcurementReceiptDocumentType.Grn, ProcurementReceiptDocumentKind.Grn)]
    [InlineData(ProcurementReceiptDocumentType.Mrn, ProcurementReceiptDocumentKind.Mrn)]
    public void SingleDocumentConfigurationProducesOnlyItsConfiguredKind(
        ProcurementReceiptDocumentType type,
        ProcurementReceiptDocumentKind expected)
    {
        ProcurementReceiptDocumentRules.RequiredKinds(type).Should().Equal(expected);
    }

    [Fact]
    public void CombinedConfigurationResolvesDistinctNumbersAndDmsTemplates()
    {
        var decision = ValidDecision();

        decision.ResolveNumberFormat(ProcurementReceiptDocumentType.Grn).Should().Be("GRN-{YYYY}-{######}");
        decision.ResolveNumberFormat(ProcurementReceiptDocumentType.Mrn).Should().Be("MRN-{YYYY}-{######}");
        decision.ResolveTemplateReference(ProcurementReceiptDocumentType.Grn).Should().Be("TDC-GRN");
        decision.ResolveTemplateReference(ProcurementReceiptDocumentType.Mrn).Should().Be("TDC-MRN");
        Validate(decision).Should().BeEmpty();
    }

    [Fact]
    public void CombinedConfigurationFailsClosedWhenNumbersOrTemplatesCollide()
    {
        var decision = ValidDecision();
        decision.NumberFormat = "RCV-{YYYY}-{######}";
        decision.TemplateReference = "TDC-RECEIPT";

        var errors = Validate(decision);

        errors.Should().Contain(item => item.ErrorMessage!.Contains("number formats must resolve to distinct"));
        errors.Should().Contain(item => item.ErrorMessage!.Contains("template references must resolve to distinct"));
    }

    [Fact]
    public void CoexistenceAndSignatoryRulesFailClosed()
    {
        var decision = ValidDecision();
        decision.CoexistenceRule = ProcurementReceiptCoexistenceRule.MutuallyExclusive;
        decision.SignatureRequirements = ["Stores", "stores"];

        var errors = Validate(decision);

        errors.Should().Contain(item => item.ErrorMessage!.Contains("cannot be mutually exclusive"));
        errors.Should().Contain(item => item.ErrorMessage!.Contains("must be unique"));
    }

    [Theory]
    [InlineData(ProcurementReceiptDocumentStatus.Draft, ProcurementReceiptDocumentStatus.Issued, true)]
    [InlineData(ProcurementReceiptDocumentStatus.PendingSignatures, ProcurementReceiptDocumentStatus.Issued, true)]
    [InlineData(ProcurementReceiptDocumentStatus.Draft, ProcurementReceiptDocumentStatus.Cancelled, false)]
    [InlineData(ProcurementReceiptDocumentStatus.PendingSignatures, ProcurementReceiptDocumentStatus.Cancelled, false)]
    [InlineData(ProcurementReceiptDocumentStatus.Issued, ProcurementReceiptDocumentStatus.Cancelled, true)]
    [InlineData(ProcurementReceiptDocumentStatus.Cancelled, ProcurementReceiptDocumentStatus.Issued, false)]
    [InlineData(ProcurementReceiptDocumentStatus.Issued, ProcurementReceiptDocumentStatus.Draft, false)]
    public void LifecycleTransitionsAreExplicit(
        ProcurementReceiptDocumentStatus from,
        ProcurementReceiptDocumentStatus to,
        bool expected)
    {
        ProcurementReceiptDocumentRules.CanTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void ReadinessHelpersEnforceEvidenceInspectionAndSequentialMrn()
    {
        ProcurementReceiptDocumentRules.MissingRequirements(
            ["Delivery note", "Inspection report"],
            ["delivery NOTE"])
            .Should().Equal("Inspection report");
        ProcurementReceiptDocumentRules.IsInspectionApproved(ProcurementReceiptInspectionStatus.Approved).Should().BeTrue();
        ProcurementReceiptDocumentRules.IsInspectionApproved(ProcurementReceiptInspectionStatus.QualityHold).Should().BeFalse();
        ProcurementReceiptDocumentRules.RequiresPriorGrnIssue(
            ProcurementReceiptDocumentKind.Mrn,
            ProcurementReceiptCoexistenceRule.SequentialDocuments).Should().BeTrue();
    }

    private static ProcurementReceiptDocumentDecisionValueDto ValidDecision() => new()
    {
        EffectiveFrom = DateTime.UtcNow.Date,
        DocumentType = ProcurementReceiptDocumentType.GrnAndMrn,
        ApplicabilityRule = "Both documents are produced for a governed receipt.",
        CoexistenceRule = ProcurementReceiptCoexistenceRule.BothFromSingleReceipt,
        NumberFormat = "{TYPE}-{YYYY}-{######}",
        TemplateReference = "TDC-{TYPE}",
        SignatureRequirements = ["Stores", "Internal Audit"],
        EvidenceRequirements = ["Delivery note"]
    };

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
