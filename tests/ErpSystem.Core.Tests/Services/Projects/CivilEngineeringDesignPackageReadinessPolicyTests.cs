using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDesignPackageReadinessPolicyTests
{
    private readonly DateTime _approvedAt = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _design = Guid.NewGuid();
    private readonly Guid _drawing = Guid.NewGuid();
    private readonly Guid _package = Guid.NewGuid();

    [Fact]
    public void Complete_current_governed_package_is_ready_and_returns_latest_approval_time()
    {
        var result = CivilEngineeringDesignPackageReadinessPolicy.Validate(
            [CivilEngineeringDesignDiscipline.Civil, CivilEngineeringDesignDiscipline.Structural],
            Documents(),
            Evidence());

        result.IsReady.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.LatestGovernedApprovalAt.Should().Be(_approvedAt.AddMinutes(3));
    }

    [Fact]
    public void Missing_required_discipline_blocks_hod_submission()
    {
        var result = CivilEngineeringDesignPackageReadinessPolicy.Validate(
            [CivilEngineeringDesignDiscipline.Civil, CivilEngineeringDesignDiscipline.Electrical],
            Documents(),
            Evidence());

        result.Errors.Should().ContainSingle(error => error.Contains("Electrical"));
    }

    [Fact]
    public void Ungoverned_or_stale_evidence_and_missing_package_are_rejected()
    {
        var staleDocuments = Documents()
            .Select(item => item.CentralDocumentVersionId == _drawing ? item with { IsCurrentPublished = false } : item)
            .ToList();
        var evidence = Evidence().Where(item => item.EvidenceType != CivilEngineeringDesignEvidenceTypes.SubmissionPackage).ToList();

        var result = CivilEngineeringDesignPackageReadinessPolicy.Validate(
            [CivilEngineeringDesignDiscipline.Civil],
            staleDocuments,
            evidence);

        result.Errors.Should().Contain(error => error.Contains("Drawing") && error.Contains("governed engineering document register"));
        result.Errors.Should().Contain(error => error.Contains("SubmissionPackage") && error.Contains("required"));
    }

    private List<CivilEngineeringGovernedPackageDocument> Documents() =>
    [
        new(CivilEngineeringDesignDiscipline.Civil, CivilEngineeringDocumentStatus.Approved,
            _design, true, _approvedAt.AddMinutes(1)),
        new(CivilEngineeringDesignDiscipline.Structural, CivilEngineeringDocumentStatus.Approved,
            _drawing, true, _approvedAt.AddMinutes(2)),
        new(CivilEngineeringDesignDiscipline.Civil, CivilEngineeringDocumentStatus.Approved,
            _package, true, _approvedAt.AddMinutes(3))
    ];

    private List<CivilEngineeringPackageEvidence> Evidence() =>
    [
        new(CivilEngineeringDesignEvidenceTypes.Design, _design),
        new(CivilEngineeringDesignEvidenceTypes.Drawing, _drawing),
        new(CivilEngineeringDesignEvidenceTypes.SubmissionPackage, _package)
    ];
}
