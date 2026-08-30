using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringWeeklySupervisionPolicyTests
{
    private static readonly CivilEngineeringWeeklyReportValue Policy = new()
    {
        ActivityCategoryIds = [Guid.NewGuid()],
        EscalationRoleIds = [Guid.NewGuid()],
        RequireProgressMeasurement = true,
        RequireMaterialUsage = true,
        RequireSafetyNotes = true,
        RequireTestSummary = true,
        MinimumPhotoCount = 1,
        DueDay = CivilEngineeringReportDueDay.Monday
    };

    [Fact]
    public void Create_rejects_non_Monday_week_and_uncontrolled_activity_category()
    {
        var request = Valid(Policy);
        request.WeekStart = new DateTime(2026, 8, 19);
        request.Activities[0].ActivityCategoryId = Guid.NewGuid();

        CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, Policy, DateTime.UtcNow)
            .Should().Contain(value => value.Contains("Monday", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("permitted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_requires_controlled_contractor_and_minimum_photo_evidence()
    {
        var request = Valid(Policy);
        request.Activities[0].ContractorBusinessPartnerId = null;
        request.Evidence[1].EvidenceRole = CivilEngineeringWeeklySupervisionEvidenceRoles.Report;

        CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, Policy, DateTime.UtcNow)
            .Should().Contain(value => value.Contains("Business Partner", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("photo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_accepts_a_governed_contractor_report()
    {
        CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(Valid(Policy), Policy, DateTime.UtcNow).Should().BeEmpty();
    }

    [Fact]
    public void Labour_gang_cannot_carry_an_external_contractor()
    {
        var request = Valid(Policy);
        request.Activities[0].ActorType = CivilEngineeringWeeklySupervisionActorTypes.LabourGang;

        CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, Policy, DateTime.UtcNow)
            .Should().Contain(value => value.Contains("labour-gang", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Delayed_progress_requires_a_controlled_recovery_action_owner_and_due_date()
    {
        var request = Valid(Policy);
        request.SiteStatus = CivilEngineeringWeeklySupervisionSiteStatuses.Delayed;

        CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, Policy, DateTime.UtcNow)
            .Should().Contain(value => value.Contains("delay", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("recovery action", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("owner", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("due date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_requires_a_controlled_milestone_and_site_status()
    {
        var request = Valid(Policy);
        request.ProjectMilestoneId = Guid.Empty;
        request.SiteStatus = "Uncontrolled";

        CivilEngineeringWeeklySupervisionPolicy.ValidateCreate(request, Policy, DateTime.UtcNow)
            .Should().Contain(value => value.Contains("milestone", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("site status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Review_requires_comment_and_paired_dms_evidence()
    {
        var request = new ProcessCivilEngineeringWeeklySupervisionReportRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "AQID", Comment = "ok", CentralDocumentRecordId = Guid.NewGuid()
        };

        CivilEngineeringWeeklySupervisionPolicy.ValidateReview(request).Should()
            .Contain(value => value.Contains("comment", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("both", StringComparison.OrdinalIgnoreCase));
    }

    private static CreateCivilEngineeringWeeklySupervisionReportRequest Valid(CivilEngineeringWeeklyReportValue policy)
    {
        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);
        return new CreateCivilEngineeringWeeklySupervisionReportRequest
        {
            ClientRequestId = Guid.NewGuid(), WeekStart = monday, ProjectMilestoneId = Guid.NewGuid(), OverallProgressPercent = 35, MaterialUsageSummary = "Controlled material usage summary.",
            SafetyNotes = "Controlled safety notes.", TestSummary = "Controlled test observation.",
            Activities = [new CivilEngineeringWeeklySupervisionActivityRequest { ActivityCategoryId = policy.ActivityCategoryIds[0], ActorType = CivilEngineeringWeeklySupervisionActorTypes.Contractor, ContractorBusinessPartnerId = Guid.NewGuid(), Description = "Foundation work activity.", ProgressPercent = 35 }],
            Evidence = [new CivilEngineeringWeeklySupervisionEvidenceRequest { CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid(), EvidenceRole = CivilEngineeringWeeklySupervisionEvidenceRoles.Report }, new CivilEngineeringWeeklySupervisionEvidenceRequest { CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid(), EvidenceRole = CivilEngineeringWeeklySupervisionEvidenceRoles.Photo }]
        };
    }
}
