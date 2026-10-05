using ErpSystem.Api.Services.Finance.MultiCurrency;
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

public sealed class ExchangeRateWorkflowReconciliationTests
{
    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    public async Task ReconciliationStartsOnlyPendingRatesWithoutWorkflowHistory()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "ExchangeRate",
            Name = "Exchange Rate",
            IsActive = true
        };
        var orphan = CreateRate(tenantId, initiatorId, RateApprovalStatus.Pending, "USD");
        var historical = CreateRate(tenantId, initiatorId, RateApprovalStatus.Pending, "EUR");
        var approved = CreateRate(tenantId, initiatorId, RateApprovalStatus.Approved, "GBP");
        db.WorkflowEntityTypes.Add(entityType);
        db.ExchangeRates.AddRange(orphan, historical, approved);
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
                "ExchangeRate", orphan.Id, initiatorId, tenantId))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = new ExchangeRateWorkflowReconciliationService(
            db,
            workflow.Object,
            NullLogger<ExchangeRateWorkflowReconciliationService>.Instance);

        var result = await service.ReconcileAsync();

        result.Should().Be(new ExchangeRateWorkflowReconciliationResult(
            PendingCount: 2,
            OrphanCount: 1,
            RecoveredCount: 1,
            FailedCount: 0,
            SkippedWithoutInitiatorCount: 0));
        workflow.Verify(item => item.StartApprovalWorkflowAsAsync(
            "ExchangeRate", orphan.Id, initiatorId, tenantId), Times.Once);
        workflow.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    public async Task ReconciliationDoesNotStartWorkflowWithoutOriginalInitiator()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.WorkflowEntityTypes.Add(new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "ExchangeRate",
            Name = "Exchange Rate",
            IsActive = true
        });
        db.ExchangeRates.Add(CreateRate(tenantId, Guid.Empty, RateApprovalStatus.Pending, "USD"));
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        var service = new ExchangeRateWorkflowReconciliationService(
            db,
            workflow.Object,
            NullLogger<ExchangeRateWorkflowReconciliationService>.Instance);

        var result = await service.ReconcileAsync();

        result.OrphanCount.Should().Be(1);
        result.SkippedWithoutInitiatorCount.Should().Be(1);
        result.RecoveredCount.Should().Be(0);
        workflow.VerifyNoOtherCalls();
    }

    private static ExchangeRate CreateRate(
        Guid tenantId,
        Guid initiatorId,
        RateApprovalStatus approvalStatus,
        string targetCurrency)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = targetCurrency,
            Rate = 15m,
            InverseRate = 1m / 15m,
            EffectiveDate = new DateTime(2026, 10, 5),
            RateType = ExchangeRateType.Daily,
            QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana",
            IsActive = true,
            ApprovalStatus = approvalStatus,
            CreatedByUserId = initiatorId,
            CreatedDate = DateTime.UtcNow
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"exchange-rate-workflow-reconciliation-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
