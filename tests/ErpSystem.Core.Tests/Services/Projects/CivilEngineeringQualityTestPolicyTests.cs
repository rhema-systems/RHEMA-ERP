using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringQualityTestPolicyTests
{
    private static CivilEngineeringQualityTestValue Policy => new()
    {
        TestCategories = [CivilEngineeringQualityTestCategory.Concrete],
        ReviewerRoleIds = [Guid.NewGuid()],
        RequireEndorsementEvidence = true
    };

    [Fact]
    public void Create_requires_a_controlled_category_external_source_partner_reviewer_and_dms_evidence()
    {
        var request = new CreateCivilEngineeringQualityTestReportRequest
        {
            ClientRequestId = Guid.NewGuid(), ReportReference = "LAB-100", TestCategory = CivilEngineeringQualityTestCategory.Concrete,
            SourceType = "Laboratory", TestedAt = DateTime.UtcNow, ResultStatus = "Pass", ResultSummary = "Compressive strength passed.", ReviewerUserId = Guid.NewGuid(),
            CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid()
        };
        CivilEngineeringQualityTestPolicy.ValidateCreate(request, Policy, DateTime.UtcNow).Should().Contain(value => value.Contains("source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_rejects_categories_not_selected_by_the_effective_policy()
    {
        var request = new CreateCivilEngineeringQualityTestReportRequest { ClientRequestId = Guid.NewGuid(), ReportReference = "LAB-100", TestCategory = CivilEngineeringQualityTestCategory.Soil, SourceType = "Internal", TestedAt = DateTime.UtcNow, ResultStatus = "Pass", ResultSummary = "Result", ReviewerUserId = Guid.NewGuid(), CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid() };
        CivilEngineeringQualityTestPolicy.ValidateCreate(request, Policy, DateTime.UtcNow).Should().Contain(value => value.Contains("permitted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_accepts_a_fully_controlled_internal_report()
    {
        var request = new CreateCivilEngineeringQualityTestReportRequest { ClientRequestId = Guid.NewGuid(), ReportReference = "FIELD-100", TestCategory = CivilEngineeringQualityTestCategory.Concrete, SourceType = "Internal", TestedAt = DateTime.UtcNow, ResultStatus = "Pass", ResultSummary = "Field compaction result passed.", ReviewerUserId = Guid.NewGuid(), CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid() };
        CivilEngineeringQualityTestPolicy.ValidateCreate(request, Policy, DateTime.UtcNow).Should().BeEmpty();
    }

    [Fact]
    public void Create_rejects_an_external_partner_on_an_internal_report()
    {
        var request = new CreateCivilEngineeringQualityTestReportRequest { ClientRequestId = Guid.NewGuid(), ReportReference = "FIELD-100", TestCategory = CivilEngineeringQualityTestCategory.Concrete, SourceType = "Internal", SourceBusinessPartnerId = Guid.NewGuid(), TestedAt = DateTime.UtcNow, ResultStatus = "Pass", ResultSummary = "Field compaction result passed.", ReviewerUserId = Guid.NewGuid(), CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid() };
        CivilEngineeringQualityTestPolicy.ValidateCreate(request, Policy, DateTime.UtcNow).Should().Contain(value => value.Contains("internal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Approval_requires_endorsement_evidence_when_the_effective_policy_requires_it()
    {
        var request = new ProcessCivilEngineeringQualityTestReportRequest { ClientRequestId = Guid.NewGuid(), RowVersion = "AQID", Approve = true, Reason = "Independent review passed." };
        CivilEngineeringQualityTestPolicy.ValidateDecision(request, true).Should().Contain(value => value.Contains("endorsement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejection_does_not_require_endorsement_evidence()
    {
        var request = new ProcessCivilEngineeringQualityTestReportRequest { ClientRequestId = Guid.NewGuid(), RowVersion = "AQID", Approve = false, Reason = "Evidence does not support the reported result." };
        CivilEngineeringQualityTestPolicy.ValidateDecision(request, true).Should().BeEmpty();
    }
}
