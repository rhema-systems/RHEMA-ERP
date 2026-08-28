using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
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

public sealed class ProcurementRequisitionLinkageServiceTests
{
    [Fact]
    public async Task CompleteLinkagePersistsSnapshotsAndImmutableHistory()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedReferences();
        var requisition = fixture.NewRequisition();
        var request = fixture.CompleteRequest(references);

        await fixture.Service.PrepareAsync(requisition, request, "trace-create");
        await fixture.Context.PurchaseRequisitions.AddAsync(requisition);
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.RecordMutationAsync(requisition, "Created", null, "trace-create");

        var saved = await fixture.Context.PurchaseRequisitions.SingleAsync(item => item.Id == requisition.Id);
        var history = await fixture.Service.GetHistoryAsync(requisition.Id);
        saved.SourcePlanItemId.Should().Be(references.PlanItem.Id);
        saved.SourcePlanNumber.Should().Be(references.Plan.PlanNumber);
        saved.BudgetId.Should().Be(references.Budget.Id);
        saved.ProcurementCategory.Should().Be(ProcurementCategoryClass.Goods);
        saved.CostCenter.Should().Be("CC-010");
        saved.ProjectCode.Should().Be(references.Project.ProjectCode);
        saved.RequisitionType.Should().Be(PurchaseRequisitionType.ProjectPurchase);
        saved.SpecificationTemplateCode.Should().Be(references.Template.TemplateCode);
        saved.ApprovedExceptionRuleCode.Should().Be(references.ExceptionRule.RuleCode);
        saved.ExceptionWorkflowInstanceId.Should().Be(references.ExceptionWorkflow.Id);
        saved.ExceptionApprovedAtUtc.Should().Be(references.ExceptionWorkflow.CompletedDate);
        saved.LinkageRevision.Should().Be(1);
        history.Should().ContainSingle(item => item.Action == "Created" && item.IntegrityHash.Length == 64);
    }

    [Fact]
    public async Task PlanItemAutomaticallyCarriesItsBudgetWithoutMakingLinkageMandatory()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedReferences();
        var requisition = fixture.NewRequisition();

        await fixture.Service.PrepareAsync(requisition, new SavePurchaseRequisitionLinkageRequest
        {
            SourcePlanItemId = references.PlanItem.Id,
            RequisitionType = PurchaseRequisitionType.StockReplenishment
        }, "trace-plan");

        requisition.BudgetId.Should().Be(references.Budget.Id);
        requisition.Currency.Should().Be(references.Budget.Currency);
        requisition.SpecificationTemplateId.Should().BeNull();
        requisition.ApprovedExceptionRuleId.Should().BeNull();
    }

    [Fact]
    public async Task MismatchedPlanItemAndBudgetAreRejectedBeforeMutation()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedReferences();
        var otherBudget = new ProcurementBudget
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, BudgetCode = "BUD-OTHER", Title = "Other",
            DepartmentId = Guid.NewGuid(), FiscalYear = 2026, Status = "Active"
        };
        fixture.Context.ProcurementBudgets.Add(otherBudget);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.PrepareAsync(fixture.NewRequisition(), new SavePurchaseRequisitionLinkageRequest
        {
            SourcePlanItemId = references.PlanItem.Id,
            BudgetId = otherBudget.Id,
            RequisitionType = PurchaseRequisitionType.StockReplenishment
        }, "trace-mismatch");

        await action.Should().ThrowAsync<ProcurementRequisitionLinkageValidationException>()
            .Where(exception => exception.Code == "PLAN_BUDGET_MISMATCH");
    }

    [Fact]
    public async Task CrossTenantReferenceIsNotDiscoverable()
    {
        await using var fixture = new Fixture();
        var foreignProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = fixture.ForeignTenantId, ProjectCode = "FOREIGN", Title = "Foreign", Status = "Active"
        };
        fixture.Context.Projects.Add(foreignProject);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.PrepareAsync(fixture.NewRequisition(), new SavePurchaseRequisitionLinkageRequest
        {
            ProjectId = foreignProject.Id,
            RequisitionType = PurchaseRequisitionType.ProjectPurchase
        }, "trace-tenant");

        await action.Should().ThrowAsync<ProcurementRequisitionLinkageNotFoundException>()
            .Where(exception => exception.Code == "PROJECT_NOT_FOUND");
    }

    [Fact]
    public async Task ApprovedExceptionRequiresCompletedSharedExceptionWorkflow()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedReferences();

        var action = () => fixture.Service.PrepareAsync(fixture.NewRequisition(), new SavePurchaseRequisitionLinkageRequest
        {
            ApprovedExceptionRuleId = references.ExceptionRule.Id,
            RequisitionType = PurchaseRequisitionType.EmergencyPurchase
        }, "trace-exception");

        await action.Should().ThrowAsync<ProcurementRequisitionLinkageValidationException>()
            .Where(exception => exception.Code == "EXCEPTION_WORKFLOW_REQUIRED");
    }

    [Fact]
    public async Task CompletedExceptionWorkflowWithoutCompletionTimestampIsNotAcceptedOrOffered()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedReferences();
        references.ExceptionWorkflow.CompletedDate = null;
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.PrepareAsync(
            fixture.NewRequisition(), fixture.CompleteRequest(references), "trace-missing-completion");

        await action.Should().ThrowAsync<ProcurementRequisitionLinkageConflictException>()
            .Where(exception => exception.Code == "EXCEPTION_WORKFLOW_COMPLETION_DATE_MISSING");
        var options = await fixture.Service.GetOptionsAsync();
        options.ApprovedExceptionWorkflows.Should().NotContain(item => item.Id == references.ExceptionWorkflow.Id);
    }

    [Fact]
    public async Task NonAdministratorMutationRequiresRequisitionCreateCapability()
    {
        await using var fixture = new Fixture();
        fixture.SwitchRoles("TDC_REQUISITIONER");
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false, Code = "ACCESS_PERMISSION_DENIED", Message = "No active requisition responsibility."
            });

        var action = () => fixture.Service.PrepareAsync(fixture.NewRequisition(), new SavePurchaseRequisitionLinkageRequest(), "trace-denied");

        await action.Should().ThrowAsync<ProcurementRequisitionLinkageAuthorizationException>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.PermissionCode == "procurement.requisition.create"),
            "trace-denied", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OptionsExposeOnlyEffectivePublishedTemplatesAndCompletedExceptionWorkflows()
    {
        await using var fixture = new Fixture();
        var references = fixture.SeedReferences();
        fixture.Context.ProcurementSpecificationTemplates.Add(new ProcurementSpecificationTemplate
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TemplateKey = Guid.NewGuid(), TemplateCode = "DRAFT",
            Name = "Draft", Kind = ProcurementSpecificationTemplateKind.Goods,
            Status = ProcurementSpecificationTemplateStatus.Draft, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1)
        });
        await fixture.Context.SaveChangesAsync();

        var options = await fixture.Service.GetOptionsAsync();

        options.SpecificationTemplates.Should().ContainSingle(item => item.Id == references.Template.Id);
        options.SpecificationTemplates.Should().NotContain(item => item.Code == "DRAFT");
        options.ApprovedExceptionWorkflows.Should().ContainSingle(item => item.Id == references.ExceptionWorkflow.Id);
        options.PlanItems.Should().ContainSingle(item =>
            item.Id == references.PlanItem.Id && item.LinkedBudgetId == references.Budget.Id);
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
            _currentUser.SetupGet(item => item.Username).Returns("linkage.manager@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Linkage Manager");
            _currentUser.SetupGet(item => item.Roles).Returns(_roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));

            _unitOfWork = new UnitOfWork(Context);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED", Message = "Allowed" });
            var controlEvents = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementRequisitionLinkageService(_unitOfWork, _currentUser.Object, Access.Object, controlEvents);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementRequisitionLinkageService Service { get; }

        public PurchaseRequisition NewRequisition() => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RequisitionNumber = $"PR-2026-{Random.Shared.Next(1000, 9999)}",
            RequestedById = UserId,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public SeededReferences SeedReferences()
        {
            var plan = new ProcurementPlan
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PlanNumber = "APP-2026-01", Title = "Annual plan",
                DepartmentId = Guid.NewGuid(), FiscalYear = 2026, PlanStartDate = DateTime.UtcNow.Date,
                PlanEndDate = DateTime.UtcNow.Date.AddYears(1), Status = "Active"
            };
            var budget = new ProcurementBudget
            {
                Id = Guid.NewGuid(), TenantId = TenantId, BudgetCode = "BUD-2026-01", Title = "Goods budget",
                DepartmentId = plan.DepartmentId, ProcurementPlanId = plan.Id, FiscalYear = 2026,
                AllocatedAmount = 100000, RemainingAmount = 90000, Currency = "GHS", Status = "Active"
            };
            var planItem = new ProcurementPlanItem
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProcurementPlanId = plan.Id, ProcurementBudgetId = budget.Id,
                ItemDescription = "Enterprise laptops", ItemCategory = "Goods", EstimatedQuantity = 10,
                EstimatedUnitPrice = 5000, EstimatedTotalCost = 50000, Status = "Approved", Currency = "GHS"
            };
            var project = new Project
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectCode = "PRJ-010", Title = "Digital workplace", Status = "Active"
            };
            var template = new ProcurementSpecificationTemplate
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TemplateKey = Guid.NewGuid(), TemplateCode = "SPEC-GOODS",
                Name = "Goods standard", Kind = ProcurementSpecificationTemplateKind.Goods, Version = 2,
                Status = ProcurementSpecificationTemplateStatus.Published, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1)
            };
            var policy = new ProcurementPolicySet
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicyKey = Guid.NewGuid(), Code = "TDC-POLICY", Name = "TDC policy",
                Version = 1, LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                SourceConfigurationProfileId = Guid.NewGuid(), EffectiveFrom = DateTime.UtcNow.AddDays(-1)
            };
            var exceptionRule = new ProcurementPolicyExceptionRule
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "EX-EMERGENCY",
                ExceptionName = "Emergency procurement", ExceptionType = "Emergency", ApproverRole = "TDC_MANAGING_DIRECTOR",
                IsEnabled = true, EffectiveFrom = DateTime.UtcNow.AddDays(-1)
            };
            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Code = "PROCUREMENT_EXCEPTION", Name = "Procurement Exception"
            };
            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Name = "TDC Procurement Exception Approval",
                Description = "Shared exception approval", EntityTypeId = entityType.Id, Version = 1, IsActive = true
            };
            var workflow = new WorkflowInstance
            {
                Id = Guid.NewGuid(), TenantId = TenantId, WorkflowDefinitionId = definition.Id,
                EntityTypeId = entityType.Id, EntityId = Guid.NewGuid(), InitiatedById = UserId,
                Status = WorkflowInstanceStatus.Completed, CompletedDate = DateTime.UtcNow.AddHours(-1)
            };
            Context.AddRange(plan, budget, planItem, project, template, policy, exceptionRule, entityType, definition, workflow);
            Context.SaveChanges();
            return new SeededReferences(plan, planItem, budget, project, template, exceptionRule, workflow);
        }

        public SavePurchaseRequisitionLinkageRequest CompleteRequest(SeededReferences references) => new()
        {
            SourcePlanItemId = references.PlanItem.Id,
            BudgetId = references.Budget.Id,
            ProcurementCategory = ProcurementCategoryClass.Goods,
            CostCenter = " CC-010 ",
            ProjectId = references.Project.Id,
            RequisitionType = PurchaseRequisitionType.ProjectPurchase,
            SpecificationTemplateId = references.Template.Id,
            ApprovedExceptionRuleId = references.ExceptionRule.Id,
            ExceptionWorkflowInstanceId = references.ExceptionWorkflow.Id,
            ExceptionApprovalReference = "MIN-EX-010",
            ExceptionEvidenceReference = "EVID-EX-010"
        };

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

    private sealed record SeededReferences(
        ProcurementPlan Plan,
        ProcurementPlanItem PlanItem,
        ProcurementBudget Budget,
        Project Project,
        ProcurementSpecificationTemplate Template,
        ProcurementPolicyExceptionRule ExceptionRule,
        WorkflowInstance ExceptionWorkflow);
}
