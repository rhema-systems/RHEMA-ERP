using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDevelopmentApprovalFilePolicyTests
{
    private static readonly DateTime Today = new(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_requires_controlled_targets_safe_retry_and_current_published_evidence()
    {
        var request = new CreateCivilEngineeringDevelopmentApprovalFileRequest
        {
            ClientRequestId = Guid.Empty,
            ProjectId = Guid.Empty,
            EstateManagedAssetId = Guid.Empty,
            ApplicationReference = "A",
            DueDate = Today.AddDays(-1),
            ApplicationEvidence = []
        };

        var errors = CivilEngineeringDevelopmentApprovalFilePolicy.ValidateCreate(request, Today);

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("project", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("property", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("applicant", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Site_inspection_requires_row_version_and_deduplicated_dms_evidence()
    {
        var request = new RecordCivilEngineeringSiteInspectionRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SiteInspectedAt = Today,
            RowVersion = string.Empty,
            Evidence =
            [
                new() { CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.Empty },
                new() { CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.Empty }
            ]
        };

        var errors = CivilEngineeringDevelopmentApprovalFilePolicy.ValidateInspection(request, Today);

        errors.Should().Contain(error => error.Contains("row version", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("DMS", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("once", StringComparison.OrdinalIgnoreCase));
    }
}
