using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyWorkflowBindingRegistryTests
{
    [Fact]
    public void Every_required_QS_workflow_field_is_bound_to_one_shared_entity_type()
    {
        var bindings = QuantitySurveyWorkflowBindingRegistry.Bindings;
        var entityCodes = QuantitySurveyWorkflowBindingRegistry.EntityTypes
            .Select(value => value.Code)
            .ToHashSet(StringComparer.Ordinal);

        bindings.Should().HaveCount(11);
        bindings.Select(value => $"{value.DecisionKey}:{value.ConfigurationField}")
            .Should().OnlyHaveUniqueItems();
        bindings.Select(value => value.EntityTypeCode)
            .Should().OnlyContain(value => entityCodes.Contains(value));

        var workflowFields = QuantitySurveyConfigurationDecisionRegistry.Definitions
            .SelectMany(decision => decision.Fields
                .Where(field => field.LookupSource == "workflows")
                .Select(field => new { decision.DecisionKey, Field = field }))
            .ToList();
        workflowFields.Should().HaveCount(bindings.Count);
        foreach (var item in workflowFields)
        {
            var binding = QuantitySurveyWorkflowBindingRegistry.GetRequired(
                item.DecisionKey,
                item.Field.Name);
            item.Field.LookupGroup.Should().Be(binding.EntityTypeCode);
            item.Field.Required.Should().BeTrue();
        }
    }

    [Fact]
    public void Shared_registry_resolves_all_QS_aliases_to_the_single_QS_adapter()
    {
        var adapter = new QuantitySurveyWorkflowStatusAdapter();
        var registry = new WorkflowStatusAdapterRegistry([adapter]);

        foreach (var entityType in QuantitySurveyWorkflowBindingRegistry.EntityTypes)
        {
            registry.GetAdapter(entityType.Code).Should().BeSameAs(adapter);
            registry.GetAdapter(entityType.Name).Should().BeSameAs(adapter);
        }
    }

    [Fact]
    public void Adapter_applies_submit_approve_reject_and_recall_without_a_parallel_workflow_state()
    {
        var adapter = new QuantitySurveyWorkflowStatusAdapter();
        var record = new StubRecord();
        var approverId = Guid.NewGuid();

        adapter.ApplySubmitOutcome(record, WorkflowOutcome.Pending, approverId);
        record.Status.Should().Be("PendingApproval");
        record.ApprovalStatus.Should().Be("Pending");

        adapter.ApplyApprovalOutcome(record, WorkflowOutcome.Approved, approverId);
        record.Status.Should().Be("Approved");
        record.ApprovedById.Should().Be(approverId);
        record.ApprovedAt.Should().NotBeNull();

        adapter.ApplyApprovalOutcome(record, WorkflowOutcome.Rejected, approverId, "Authority rejected");
        record.Status.Should().Be("Rejected");
        record.ApprovedById.Should().BeNull();
        record.RejectionReason.Should().Be("Authority rejected");

        adapter.ApplyRecallOutcome(record, approverId, "Correct the submission");
        record.Status.Should().Be("Draft");
        record.ApprovalStatus.Should().Be("Draft");
        record.RejectionReason.Should().Be("Correct the submission");
    }

    private sealed class StubRecord : IQuantitySurveyWorkflowRecord
    {
        public string Status { get; set; } = "Draft";
        public string ApprovalStatus { get; set; } = "Draft";
        public Guid? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
    }
}
