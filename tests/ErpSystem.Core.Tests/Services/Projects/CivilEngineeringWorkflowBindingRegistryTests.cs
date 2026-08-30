using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringWorkflowBindingRegistryTests
{
    [Fact]
    public void Every_required_Civil_workflow_field_is_bound_to_one_shared_entity_type()
    {
        var bindings = CivilEngineeringWorkflowBindingRegistry.Bindings;
        var entityCodes = CivilEngineeringWorkflowBindingRegistry.EntityTypes
            .Select(value => value.Code)
            .Append(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate)
            .ToHashSet(StringComparer.Ordinal);

        bindings.Should().HaveCount(14);
        bindings.Select(value => $"{value.DecisionKey}:{value.ConfigurationField}")
            .Should().OnlyHaveUniqueItems();
        bindings.Select(value => value.EntityTypeCode)
            .Should().OnlyContain(value => entityCodes.Contains(value));
        bindings.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value.AuthoritativeOwner));

        var workflowFields = CivilEngineeringConfigurationDecisionRegistry.Definitions
            .SelectMany(decision => decision.Fields
                .Where(field => field.LookupSource == "workflows")
                .Select(field => new { decision.ConfigurationKey, Field = field }))
            .ToList();
        workflowFields.Should().HaveCount(bindings.Count);
        foreach (var item in workflowFields)
        {
            var binding = CivilEngineeringWorkflowBindingRegistry.GetRequired(
                item.ConfigurationKey,
                item.Field.Name);
            item.Field.LookupGroup.Should().Be(binding.EntityTypeCode);
            item.Field.Required.Should().BeTrue();
        }
    }

    [Fact]
    public void Interim_certificate_binding_reuses_the_authoritative_QS_workflow_owner()
    {
        var binding = CivilEngineeringWorkflowBindingRegistry.GetRequired(
            "CIV-CFG-005",
            "interimCertificateWorkflowDefinitionId");

        binding.EntityTypeCode.Should().Be(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate);
        binding.AuthoritativeOwner.Should().Be("Quantity Survey");
        CivilEngineeringWorkflowBindingRegistry.EntityTypes.Select(value => value.Code)
            .Should().NotContain(binding.EntityTypeCode);
    }

    [Fact]
    public void Shared_registry_resolves_every_Civil_alias_to_one_adapter()
    {
        var adapter = new CivilEngineeringWorkflowStatusAdapter();
        var registry = new WorkflowStatusAdapterRegistry([adapter]);

        foreach (var entityType in CivilEngineeringWorkflowBindingRegistry.EntityTypes)
        {
            registry.GetAdapter(entityType.Code).Should().BeSameAs(adapter);
            registry.GetAdapter(entityType.Name).Should().BeSameAs(adapter);
        }
    }

    [Fact]
    public void Adapter_projects_submit_approve_reject_and_recall_without_parallel_workflow_state()
    {
        var adapter = new CivilEngineeringWorkflowStatusAdapter();
        var record = new StubRecord();
        var approverId = Guid.NewGuid();

        adapter.ApplySubmitOutcome(record, WorkflowOutcome.Pending, approverId);
        record.Status.Should().Be("PendingApproval");
        record.ApprovalStatus.Should().Be("Pending");

        adapter.ApplyApprovalOutcome(record, WorkflowOutcome.Approved, approverId);
        record.Status.Should().Be("Approved");
        record.ApprovedById.Should().Be(approverId);
        record.ApprovedAt.Should().NotBeNull();

        adapter.ApplyApprovalOutcome(record, WorkflowOutcome.Rejected, approverId, "Independent reviewer rejected");
        record.Status.Should().Be("Rejected");
        record.ApprovedById.Should().BeNull();
        record.RejectionReason.Should().Be("Independent reviewer rejected");

        adapter.ApplyRecallOutcome(record, approverId, "Correct the evidence");
        record.Status.Should().Be("Draft");
        record.ApprovalStatus.Should().Be("Draft");
        record.RejectionReason.Should().Be("Correct the evidence");
    }

    private sealed class StubRecord : ICivilEngineeringWorkflowRecord
    {
        public string Status { get; set; } = "Draft";
        public string ApprovalStatus { get; set; } = "Draft";
        public Guid? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
    }
}
