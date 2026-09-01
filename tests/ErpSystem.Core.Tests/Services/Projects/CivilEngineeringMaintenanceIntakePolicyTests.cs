using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceIntakePolicyTests
{
    [Fact]
    public void Scheduled_maintenance_requires_a_controlled_schedule_and_maintenance_source()
    {
        var request = ValidRequest(CivilEngineeringWorkClassification.ScheduledMaintenance);
        request.Source = CivilEngineeringRequestSource.Project;
        request.MaintenanceScheduleId = null;

        CivilEngineeringMaintenanceIntakePolicy.ValidateCreate(request, Policy())
            .Should().Contain(value => value.Contains("Scheduled", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("schedule", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Complaint_requires_an_authoritative_helpdesk_ticket_and_permitted_source()
    {
        var request = ValidRequest(CivilEngineeringWorkClassification.AssetComplaintResolution);
        request.Source = CivilEngineeringRequestSource.Project;
        request.HelpdeskTicketId = null;

        CivilEngineeringMaintenanceIntakePolicy.ValidateCreate(request, Policy())
            .Should().Contain(value => value.Contains("TenantComplaint", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("Helpdesk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Breakdown_cannot_bypass_controlled_target_or_attach_a_complaint_ticket()
    {
        var request = ValidRequest(CivilEngineeringWorkClassification.BreakdownMaintenance);
        request.MaintenanceAssetId = null;
        request.EstateManagedAssetId = null;
        request.HelpdeskTicketId = Guid.NewGuid();

        CivilEngineeringMaintenanceIntakePolicy.ValidateCreate(request, Policy())
            .Should().Contain(value => value.Contains("exactly one", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Frozen_configuration_rejects_unconfigured_source_and_urgency()
    {
        var request = ValidRequest(CivilEngineeringWorkClassification.BreakdownMaintenance);
        request.Source = CivilEngineeringRequestSource.ManagementDirective;
        request.Urgency = CivilEngineeringUrgency.Emergency;
        var policy = Policy();
        policy.AllowedRequestSources = [CivilEngineeringRequestSource.Maintenance];
        policy.AllowedUrgencies = [CivilEngineeringUrgency.Routine];

        CivilEngineeringMaintenanceIntakePolicy.ValidateCreate(request, policy)
            .Should().Contain(value => value.Contains("CIV-CFG-007", StringComparison.Ordinal));
    }

    private static CreateCivilEngineeringMaintenanceIntakeRequest ValidRequest(CivilEngineeringWorkClassification classification) => new()
    {
        ClientRequestId = Guid.NewGuid(), WorkClassification = classification,
        Source = classification == CivilEngineeringWorkClassification.ScheduledMaintenance ? CivilEngineeringRequestSource.Maintenance : CivilEngineeringRequestSource.Project,
        Urgency = CivilEngineeringUrgency.Priority, Title = "Controlled maintenance intake", Description = "Evidence-backed maintenance assessment intake.",
        MaintenanceAssetId = Guid.NewGuid(), RequesterUserId = Guid.NewGuid(), PriorityLevelId = Guid.NewGuid(),
        CentralDocumentRecordId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid(),
        MaintenanceScheduleId = classification == CivilEngineeringWorkClassification.ScheduledMaintenance ? Guid.NewGuid() : null,
        HelpdeskTicketId = classification == CivilEngineeringWorkClassification.AssetComplaintResolution ? Guid.NewGuid() : null
    };

    private static CivilEngineeringMaintenanceAssessmentValue Policy() => new()
    {
        AllowedRequestSources = [CivilEngineeringRequestSource.Project, CivilEngineeringRequestSource.Maintenance, CivilEngineeringRequestSource.TenantComplaint, CivilEngineeringRequestSource.Estate, CivilEngineeringRequestSource.ManagementDirective],
        AllowedUrgencies = [CivilEngineeringUrgency.Routine, CivilEngineeringUrgency.Priority, CivilEngineeringUrgency.Urgent, CivilEngineeringUrgency.Emergency],
        RequireAssetOrProperty = true
    };
}
