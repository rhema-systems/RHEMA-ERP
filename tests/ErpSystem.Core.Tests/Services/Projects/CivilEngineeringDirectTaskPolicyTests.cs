using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDirectTaskPolicyTests
{
    [Fact]
    public void Assignment_requires_retry_controlled_assignee_urgency_due_date_and_paired_dms_reference()
    {
        var policy = new CivilEngineeringTaskAssignmentValue
        {
            AllowedUrgencies = [CivilEngineeringUrgency.Routine],
            RequireDueDate = true
        };

        var errors = CivilEngineeringDirectTaskPolicy.ValidateCreate(new CreateCivilEngineeringDirectTaskRequest
        {
            Title = "x", Instructions = "x", Urgency = CivilEngineeringUrgency.Emergency, CentralDocumentRecordId = Guid.NewGuid()
        }, policy, DateTime.UtcNow);

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("assignee", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("urgency", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("due date", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("document and its current Published version", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(CivilEngineeringUrgency.Routine, "Normal")]
    [InlineData(CivilEngineeringUrgency.Priority, "High")]
    [InlineData(CivilEngineeringUrgency.Urgent, "High")]
    [InlineData(CivilEngineeringUrgency.Emergency, "Critical")]
    public void Work_item_priority_is_derived_from_the_controlled_urgency(CivilEngineeringUrgency urgency, string expected)
    {
        CivilEngineeringDirectTaskPolicy.WorkItemPriority(urgency).Should().Be(expected);
    }

    [Fact]
    public void Feedback_requires_an_idempotency_key_row_version_valid_action_and_paired_dms_reference()
    {
        var errors = CivilEngineeringDirectTaskPolicy.ValidateFeedback(new ProcessCivilEngineeringDirectTaskFeedbackRequest
        {
            Action = (CivilEngineeringDirectTaskFeedbackAction)999,
            CentralDocumentRecordId = Guid.NewGuid()
        });

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("row version", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("valid Civil task feedback action", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("document and its current Published version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Feedback_requires_a_paired_active_measurement_unit_and_a_plausible_offline_capture_time()
    {
        var errors = CivilEngineeringDirectTaskPolicy.ValidateFeedback(new ProcessCivilEngineeringDirectTaskFeedbackRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "row-version", Action = CivilEngineeringDirectTaskFeedbackAction.UpdateProgress,
            MeasurementValue = 12.5m, CapturedOfflineAtUtc = DateTime.UtcNow.AddDays(-32)
        });

        errors.Should().Contain(error => error.Contains("measurement value and an active unit", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("older than 31 days", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Urgent_assignment_requires_reason_sla_deadline_and_configured_escalation_roles()
    {
        var now = DateTime.UtcNow;
        var policy = new CivilEngineeringTaskAssignmentValue
        {
            AllowedUrgencies = [CivilEngineeringUrgency.Urgent],
            UrgentResponseHours = 4
        };
        var request = new CreateCivilEngineeringDirectTaskRequest
        {
            ClientRequestId = Guid.NewGuid(), Title = "Urgent site response", Instructions = "Inspect the reported structural safety issue and record the condition.",
            AssignedToUserId = Guid.NewGuid(), AssignedRoleId = Guid.NewGuid(), Urgency = CivilEngineeringUrgency.Urgent,
            UrgencyReason = "Immediate site safety review is required.", DueDate = now.AddHours(5)
        };

        var errors = CivilEngineeringDirectTaskPolicy.ValidateCreate(request, policy, now);

        errors.Should().Contain(error => error.Contains("SLA", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("escalation roles", StringComparison.OrdinalIgnoreCase));
    }
}
