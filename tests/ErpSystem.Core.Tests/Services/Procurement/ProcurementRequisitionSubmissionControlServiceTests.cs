using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementRequisitionSubmissionControlServiceTests
{
    [Fact]
    public async Task LatestAcknowledgedAppAttemptAllowsSubmissionAndRecordsIntegrityProtectedEvidence()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedAppPath();
        var requisition = fixture.NewRequisition(references.Plan, references.PlanItem);
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.EnforceAsync(requisition, "trace-app-allowed");
        var history = await fixture.Service.GetHistoryAsync(requisition.Id);
        var audit = await fixture.Context.ProcurementControlEvents
            .Include(item => item.EvidenceLinks)
            .SingleAsync(item => item.SourceId == requisition.Id);

        readiness.CanSubmit.Should().BeTrue();
        readiness.Basis.Should().Be("AcknowledgedAPP");
        readiness.DecisionCode.Should().Be("PR_APP_ACKNOWLEDGED");
        readiness.AppAcknowledgementReference.Should().Be(references.Submission.AcknowledgementReference);
        history.Should().ContainSingle(item => item.Action == "SubmissionGateAllowed" && item.IntegrityHash.Length == 64);
        audit.DecisionKeysJson.Should().Contain("DEC-009");
        audit.EvidenceLinks.Should().ContainSingle(item => item.Reference == references.Submission.AcknowledgementReference);
    }

    [Fact]
    public async Task MissingAppAndExceptionAllowsConfiguredWorkflowAndRecordsTraceabilityDecision()
    {
        await using var fixture = new Fixture();
        var requisition = fixture.NewRequisition();
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.EnforceAsync(requisition, "trace-ready");

        readiness.CanSubmit.Should().BeTrue();
        readiness.IsCompliant.Should().BeTrue();
        readiness.DecisionCode.Should().Be("PR_SUBMISSION_READY");
        readiness.Basis.Should().Be("ConfiguredApprovalWorkflow");
        readiness.RequiredActions.Should().BeEmpty();
        var history = await fixture.Service.GetHistoryAsync(requisition.Id);
        history.Should().ContainSingle(item => item.Action == "SubmissionGateAllowed" && item.Result == "Allowed" && item.IntegrityHash.Length == 64);
    }

    [Fact]
    public async Task NewerRejectedAppAttemptIsRetainedForTraceabilityWithoutBlockingSubmission()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedAppPath();
        fixture.Context.Add(new ProcurementAppSubmission
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProcurementPlanId = references.Plan.Id,
            SubmissionNumber = references.Submission.SubmissionNumber, AttemptNumber = 2,
            Status = ProcurementAppSubmissionStatus.Rejected, TimelineCorrelationId = "timeline-app",
            SupersedesSubmissionId = references.Submission.Id, ExportFileName = "app-attempt-2.xlsx",
            ExportFormat = "XLSX", ExportTemplateVersion = "1", ExportChecksumSha256 = new string('b', 64),
            ExportedAtUtc = DateTime.UtcNow.AddMinutes(-20), ExportedById = fixture.UserId,
            ExportedByName = "Submission Manager", RejectionReference = "APP-REJ-2",
            RejectionReason = "Correction required", RejectedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });
        var requisition = fixture.NewRequisition(references.Plan, references.PlanItem);
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanSubmit.Should().BeTrue();
        readiness.DecisionCode.Should().Be("PR_SUBMISSION_READY");
        readiness.Basis.Should().Be("ConfiguredApprovalWorkflow");
        readiness.AppSubmissionAttemptNumber.Should().Be(2);
        readiness.AppSubmissionStatus.Should().Be("Rejected");
    }

    [Fact]
    public async Task CompletedExceptionWorkflowForTheSameRequisitionAllowsSubmission()
    {
        await using var fixture = new Fixture();
        var requisition = fixture.NewRequisition();
        var references = fixture.SeedExceptionPath(requisition.Id);
        requisition.ProcurementCategory = ProcurementCategoryClass.Goods;
        requisition.Justification = "Emergency continuity purchase";
        requisition.ApprovedExceptionRuleId = references.Rule.Id;
        requisition.ApprovedExceptionRuleCode = references.Rule.RuleCode;
        requisition.ExceptionWorkflowInstanceId = references.Workflow.Id;
        requisition.ExceptionApprovalReference = "EX-APPROVAL-104";
        requisition.ExceptionEvidenceReference = "EVIDENCE-104";
        requisition.ExceptionApprovedAtUtc = references.Workflow.CompletedDate;
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.EnforceAsync(requisition, "trace-exception-allowed");
        var audit = await fixture.Context.ProcurementControlEvents
            .Include(item => item.EvidenceLinks)
            .SingleAsync(item => item.SourceId == requisition.Id);

        readiness.CanSubmit.Should().BeTrue();
        readiness.Basis.Should().Be("ApprovedException");
        readiness.DecisionCode.Should().Be("PR_APPROVED_EXCEPTION");
        audit.RuleCode.Should().Be(references.Rule.RuleCode);
        audit.DecisionKeysJson.Should().Contain("DEC-006");
        audit.EvidenceLinks.Select(item => item.Reference).Should().Contain([
            $"workflow:{references.Workflow.Id:N}", "EX-APPROVAL-104", "EVIDENCE-104"
        ]);
    }

    [Fact]
    public async Task CompletedExceptionWorkflowForAnotherRequisitionIsNotReusable()
    {
        await using var fixture = new Fixture();
        var requisition = fixture.NewRequisition();
        var references = fixture.SeedExceptionPath(Guid.NewGuid());
        requisition.ProcurementCategory = ProcurementCategoryClass.Goods;
        requisition.Justification = "Emergency continuity purchase";
        requisition.ApprovedExceptionRuleId = references.Rule.Id;
        requisition.ApprovedExceptionRuleCode = references.Rule.RuleCode;
        requisition.ExceptionWorkflowInstanceId = references.Workflow.Id;
        requisition.ExceptionApprovalReference = "EX-APPROVAL-OTHER";
        requisition.ExceptionEvidenceReference = "EVIDENCE-OTHER";
        requisition.ExceptionApprovedAtUtc = references.Workflow.CompletedDate;
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanSubmit.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_EXCEPTION_WORKFLOW_SUBJECT_MISMATCH");
    }

    [Fact]
    public async Task CrossTenantRequisitionIsNotDiscoverable()
    {
        await using var fixture = new Fixture();
        var requisition = fixture.NewRequisition();
        requisition.TenantId = fixture.ForeignTenantId;
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.GetReadinessAsync(requisition.Id);

        await action.Should().ThrowAsync<ProcurementRequisitionSubmissionNotFoundException>()
            .Where(exception => exception.Code == "PR_NOT_FOUND");
    }

    [Fact]
    public async Task NonAdministratorSubmissionRequiresRequisitionCreateCapability()
    {
        await using var fixture = new Fixture();
        fixture.SwitchRoles("TDC_REQUISITIONER");
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false, Code = "ACCESS_PERMISSION_DENIED", Message = "No active requisition responsibility."
            });
        var requisition = fixture.NewRequisition();
        fixture.Context.Add(requisition);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.EnforceAsync(requisition, "trace-capability-denied");

        await action.Should().ThrowAsync<ProcurementRequisitionSubmissionAuthorizationException>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.PermissionCode == "procurement.requisition.create"),
            "trace-capability-denied", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser;
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" };

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.SaveChanges();

            _currentUser = new Mock<ICurrentUserProvider>();
            _currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("submission.manager@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Submission Manager");
            _currentUser.SetupGet(item => item.Roles).Returns(_roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));

            _unitOfWork = new UnitOfWork(Context);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED", Message = "Allowed" });
            var controlEvents = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementRequisitionSubmissionControlService(_unitOfWork, _currentUser.Object, Access.Object, controlEvents);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementRequisitionSubmissionControlService Service { get; }

        public PurchaseRequisition NewRequisition(ProcurementPlan? plan = null, ProcurementPlanItem? item = null)
        {
            var requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                RequisitionNumber = $"PR-2026-{Random.Shared.Next(1000, 9999)}",
                RequestedById = UserId, Status = "Draft", ProcurementCategory = ProcurementCategoryClass.Goods,
                Department = "Information Technology", RequiredDate = DateTime.UtcNow.Date.AddDays(30),
                Justification = "Approved operational requirement", BudgetId = Guid.NewGuid(),
                Currency = "GHS", TotalAmount = 500m,
                SourcePlanId = plan?.Id, SourcePlanItemId = item?.Id,
                SourcePlanNumber = plan?.PlanNumber, SourcePlanItemDescription = item?.ItemDescription,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            requisition.Items.Add(new PurchaseRequisitionItem
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RequisitionId = requisition.Id,
                ItemDescription = item?.ItemDescription ?? "Operational equipment",
                Quantity = 1m, UnitOfMeasure = "EA", EstimatedUnitPrice = 500m,
                LineTotal = 500m, Specifications = "Business-approved minimum specification",
                Status = "Pending", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            return requisition;
        }

        public AppReferences SeedAppPath()
        {
            var plan = new ProcurementPlan
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PlanNumber = "APP-2026-104", Title = "Annual procurement plan",
                DepartmentId = Guid.NewGuid(), FiscalYear = 2026, PlanStartDate = DateTime.UtcNow.Date,
                PlanEndDate = DateTime.UtcNow.Date.AddYears(1), Status = "Active", PublishedDate = DateTime.UtcNow.AddDays(-1)
            };
            var item = new ProcurementPlanItem
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProcurementPlanId = plan.Id,
                ItemDescription = "Operational equipment", ItemCategory = "Goods", EstimatedQuantity = 5,
                EstimatedUnitPrice = 1000, EstimatedTotalCost = 5000, Status = "Approved", Currency = "GHS"
            };
            var submission = new ProcurementAppSubmission
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProcurementPlanId = plan.Id,
                SubmissionNumber = "APP-SUB-2026-104", AttemptNumber = 1,
                Status = ProcurementAppSubmissionStatus.Acknowledged, TimelineCorrelationId = "timeline-app",
                ExportFileName = "app.xlsx", ExportFormat = "XLSX", ExportTemplateVersion = "1",
                ExportChecksumSha256 = new string('a', 64), ExportedAtUtc = DateTime.UtcNow.AddHours(-2),
                ExportedById = UserId, ExportedByName = "Submission Manager",
                AcknowledgementReference = "PPA-ACK-104", AcknowledgedAtUtc = DateTime.UtcNow.AddHours(-1),
                AcknowledgedById = UserId, AcknowledgedByName = "Submission Manager"
            };
            Context.AddRange(plan, item, submission);
            Context.SaveChanges();
            return new AppReferences(plan, item, submission);
        }

        public ExceptionReferences SeedExceptionPath(Guid requisitionId)
        {
            var policy = new ProcurementPolicySet
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicyKey = Guid.NewGuid(), Code = "TDC-POLICY-104",
                Name = "TDC submission policy", Version = 1,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                SourceConfigurationProfileId = Guid.NewGuid(), EffectiveFrom = DateTime.UtcNow.AddDays(-1)
            };
            var rule = new ProcurementPolicyExceptionRule
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "EX-PR-104",
                ExceptionName = "Emergency requisition", ExceptionType = "Emergency",
                Category = ProcurementCategoryClass.Goods, ApproverRole = "TDC_MANAGING_DIRECTOR",
                JustificationRequired = true, EvidenceRequired = true, MaximumDurationDays = 30,
                IsEnabled = true, EffectiveFrom = DateTime.UtcNow.AddDays(-1), SourceDecisionKey = "DEC-006"
            };
            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Code = "PROCUREMENT_EXCEPTION", Name = "Procurement Exception"
            };
            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Name = "Procurement Exception Approval",
                EntityTypeId = entityType.Id, Version = 1, IsActive = true
            };
            rule.WorkflowDefinitionId = definition.Id;
            var workflow = new WorkflowInstance
            {
                Id = Guid.NewGuid(), TenantId = TenantId, WorkflowDefinitionId = definition.Id,
                EntityTypeId = entityType.Id, EntityId = requisitionId, InitiatedById = UserId,
                Status = WorkflowInstanceStatus.Completed, CompletedDate = DateTime.UtcNow.AddHours(-1)
            };
            Context.AddRange(policy, rule, entityType, definition, workflow);
            Context.SaveChanges();
            return new ExceptionReferences(rule, workflow);
        }

        public void SwitchRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }

    private sealed record AppReferences(
        ProcurementPlan Plan,
        ProcurementPlanItem PlanItem,
        ProcurementAppSubmission Submission);

    private sealed record ExceptionReferences(
        ProcurementPolicyExceptionRule Rule,
        WorkflowInstance Workflow);
}
