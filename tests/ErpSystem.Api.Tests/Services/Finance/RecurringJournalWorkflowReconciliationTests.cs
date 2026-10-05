using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class RecurringJournalWorkflowReconciliationTests
{
    [Fact]
    public async Task ReconciliationStartsOnlyPendingTemplatesWithoutWorkflowHistory()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = nameof(RecurringJournalTemplate),
            Name = "Recurring Journal Template",
            IsActive = true
        };
        var orphan = CreateTemplate(tenantId, initiatorId, RecurringJournalStatus.PendingApproval, "RJ-001");
        var historical = CreateTemplate(tenantId, initiatorId, RecurringJournalStatus.PendingApproval, "RJ-002");
        var active = CreateTemplate(tenantId, initiatorId, RecurringJournalStatus.Active, "RJ-003");
        db.WorkflowEntityTypes.Add(entityType);
        db.RecurringJournalTemplates.AddRange(orphan, historical, active);
        db.WorkflowInstances.Add(new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = Guid.NewGuid(),
            EntityTypeId = entityType.Id,
            EntityId = historical.Id,
            InitiatedById = initiatorId,
            Status = WorkflowInstanceStatus.Cancelled
        });
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.StartApprovalWorkflowAsAsync(
                nameof(RecurringJournalTemplate), orphan.Id, initiatorId, tenantId))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = new RecurringJournalWorkflowReconciliationService(
            db,
            workflow.Object,
            NullLogger<RecurringJournalWorkflowReconciliationService>.Instance);

        var result = await service.ReconcileAsync();

        result.Should().Be(new RecurringJournalWorkflowReconciliationResult(
            PendingCount: 2,
            OrphanCount: 1,
            RecoveredCount: 1,
            FailedCount: 0,
            SkippedWithoutInitiatorCount: 0));
        workflow.Verify(item => item.StartApprovalWorkflowAsAsync(
            nameof(RecurringJournalTemplate), orphan.Id, initiatorId, tenantId), Times.Once);
        workflow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReconciliationDoesNotStartWorkflowWithoutOriginalInitiator()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.WorkflowEntityTypes.Add(new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = nameof(RecurringJournalTemplate),
            Name = "Recurring Journal Template",
            IsActive = true
        });
        db.RecurringJournalTemplates.Add(CreateTemplate(
            tenantId,
            null,
            RecurringJournalStatus.PendingApproval,
            "RJ-001"));
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        var service = new RecurringJournalWorkflowReconciliationService(
            db,
            workflow.Object,
            NullLogger<RecurringJournalWorkflowReconciliationService>.Instance);

        var result = await service.ReconcileAsync();

        result.OrphanCount.Should().Be(1);
        result.SkippedWithoutInitiatorCount.Should().Be(1);
        result.RecoveredCount.Should().Be(0);
        workflow.VerifyNoOtherCalls();
    }

    private static RecurringJournalTemplate CreateTemplate(
        Guid tenantId,
        Guid? initiatorId,
        RecurringJournalStatus status,
        string number)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateNumber = number,
            Name = number,
            Status = status,
            EffectiveFrom = new DateOnly(2026, 10, 1),
            Frequency = RecurrenceFrequency.Monthly,
            RecurrenceRuleJson = "{}",
            SubmittedAt = status == RecurringJournalStatus.PendingApproval ? DateTime.UtcNow : null,
            SubmittedByUserId = initiatorId,
            CreatedById = initiatorId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Finance maker"
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recurring-journal-workflow-reconciliation-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
