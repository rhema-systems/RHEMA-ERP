using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
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
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPrequalificationServiceTests
{
    [Fact]
    public async Task CreateLocksTenantScopeCriteriaAndAllDecisionKeys()
    {
        await using var fixture = new Fixture();

        var result = await fixture.CreateAsync();

        result.Status.Should().Be(ProcurementPrequalificationStatus.Draft);
        result.Criteria.Should().HaveCount(2);
        result.Criteria.Sum(item => item.Weight).Should().Be(100m);
        result.IntegrityHash.Should().HaveLength(64);
        result.Milestones.Should().HaveCount(7);
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.Action == "PrequalificationDraftCreated" &&
                request.DecisionKeys.Count == 14 &&
                request.DecisionKeys[0] == "DEC-001" &&
                request.DecisionKeys[13] == "DEC-014"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRejectsIncompleteCriteriaAndForeignTenantCategory()
    {
        await using var fixture = new Fixture();
        var request = fixture.CreateRequest();
        request.Criteria[1].Weight = 30m;

        await fixture.Service.Invoking(service => service.CreateAsync(request, "weights"))
            .Should().ThrowAsync<ProcurementPrequalificationValidationException>()
            .Where(exception => exception.Code == "PREQUAL_CRITERIA_WEIGHT_INVALID");

        request = fixture.CreateRequest();
        request.CategoryIds = [Guid.NewGuid()];
        await fixture.Service.Invoking(service => service.CreateAsync(request, "tenant"))
            .Should().ThrowAsync<ProcurementPrequalificationValidationException>()
            .Where(exception => exception.Code == "PREQUAL_CATEGORY_INVALID");
    }

    [Fact]
    public async Task ApprovedLifecycleCreatesAuditableReusableCategoryEligibility()
    {
        await using var fixture = new Fixture();

        var approved = await fixture.CompleteApprovedLifecycleAsync();

        approved.Status.Should().Be(ProcurementPrequalificationStatus.Approved);
        approved.QualifiedEntries.Should().ContainSingle(item =>
            item.BusinessPartnerId == fixture.Supplier.Id &&
            item.CategoryId == fixture.Category.Id &&
            item.Status == ProcurementQualifiedListEntryStatus.Active &&
            item.IntegrityHash.Length == 64);
        var eligible = await fixture.Service.CheckEligibilityAsync(
            fixture.Supplier.Id, fixture.Category.Id, DateTime.UtcNow);
        eligible.Eligible.Should().BeTrue();
        eligible.Code.Should().Be("PREQUALIFIED");
        (await fixture.Service.CheckEligibilityAsync(
            fixture.Supplier.Id, fixture.OtherCategory.Id, DateTime.UtcNow)).Eligible.Should().BeFalse();
        fixture.Notifications.Verify(service => service.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(request =>
                request.RecipientId == fixture.SupplierPortalUserId &&
                request.Type == "ProcurementPrequalification"),
            It.IsAny<Guid>(), fixture.TenantId), Times.Once);
    }

    [Fact]
    public async Task EvaluatorAndApproverSeparationAndFinalWorkflowOutcomeAreEnforced()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.CreateAsync();
        var advertised = await fixture.AdvertiseAsync(draft);
        var application = await fixture.SubmitAsync(advertised);
        await fixture.MoveDeadlineToPastAsync();
        var closed = await fixture.Service.CloseAsync(advertised.Id,
            new CloseProcurementPrequalificationRequest { RowVersion = advertised.RowVersion }, "close");

        await fixture.Service.Invoking(service => service.EvaluateAsync(closed.Id, application.Id,
                fixture.EvaluationRequest(application), "same-actor"))
            .Should().ThrowAsync<ProcurementPrequalificationAuthorizationException>();

        fixture.CurrentUserId = Guid.NewGuid();
        var evaluated = await fixture.Service.EvaluateAsync(closed.Id, application.Id,
            fixture.EvaluationRequest(application), "evaluate");
        var pending = await fixture.Service.SubmitDecisionAsync(closed.Id,
            new SubmitProcurementPrequalificationDecisionRequest { RowVersion = closed.RowVersion }, "submit");
        fixture.CurrentUserId = Guid.NewGuid();
        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "ProcurementSourcing", closed.Id, fixture.CurrentUserId, "reject", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = fixture.WorkflowInstanceId
            });

        await fixture.Service.Invoking(service => service.DecideAsync(closed.Id,
                new DecideProcurementPrequalificationRequest
                {
                    Action = "Reject", DecisionReference = "DEC-REJECT-001",
                    DecisionEvidenceReference = "evidence://decision",
                    Reason = "The shared workflow did not reject this application.",
                    RowVersion = pending.RowVersion
                }, "reject"))
            .Should().ThrowAsync<ProcurementPrequalificationConflictException>()
            .Where(exception => exception.Code == "PREQUAL_REJECTION_NOT_FINAL");
        evaluated.Status.Should().Be(ProcurementPrequalificationApplicationStatus.EvaluatedQualified);
    }

    [Fact]
    public async Task StaleConcurrencyAndDuplicateSupplierApplicationFailClosed()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.CreateAsync();

        await fixture.Service.Invoking(service => service.AdvertiseAsync(draft.Id,
                new AdvertiseProcurementPrequalificationRequest
                {
                    AdvertisementReference = "ADV-PQ-2026-001",
                    AdvertisementEvidenceReference = "evidence://advertisement",
                    RowVersion = "AQ=="
                }, "stale"))
            .Should().ThrowAsync<ProcurementPrequalificationConflictException>()
            .Where(exception => exception.Code == "PREQUAL_VERSION_CONFLICT");

        var advertised = await fixture.AdvertiseAsync(draft);
        await fixture.SubmitAsync(advertised);
        await fixture.Service.Invoking(service => service.SubmitApplicationAsync(
                advertised.Id, fixture.ApplicationRequest(), "duplicate"))
            .Should().ThrowAsync<ProcurementPrequalificationConflictException>()
            .Where(exception => exception.Code == "PREQUAL_APPLICATION_EXISTS");
    }

    [Fact]
    public async Task DueExpiryImmediatelyRemovesSourcingEligibility()
    {
        await using var fixture = new Fixture();
        var approved = await fixture.CompleteApprovedLifecycleAsync();
        var entry = await fixture.Context.ProcurementQualifiedListEntries.SingleAsync();
        entry.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Context.SaveChangesAsync();
        var current = await fixture.Service.GetAsync(approved.Id);

        var expired = await fixture.Service.ExpireAsync(approved.Id,
            new ExpireProcurementPrequalificationRequest { RowVersion = current.RowVersion }, "expire");
        var eligibility = await fixture.Service.CheckEligibilityAsync(
            fixture.Supplier.Id, fixture.Category.Id, DateTime.UtcNow);

        expired.Status.Should().Be(ProcurementPrequalificationStatus.Expired);
        expired.QualifiedEntries.Should().ContainSingle(item =>
            item.Status == ProcurementQualifiedListEntryStatus.Expired);
        eligibility.Eligible.Should().BeFalse();
        eligibility.Code.Should().Be("NOT_PREQUALIFIED");
    }

    [Fact]
    public async Task TenantAndExternalSupplierIsolationAreEnforced()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.CreateAsync();
        var advertised = await fixture.AdvertiseAsync(draft);
        var unlinkedSupplier = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, PartnerCode = "SUP-PQ-UNLINKED",
            PartnerName = "Unlinked Supplier Limited", PartnerType = "Supplier",
            RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true
        };
        fixture.Context.Add(unlinkedSupplier);
        await fixture.Context.SaveChangesAsync();
        fixture.IsExternalUser = true;
        fixture.CurrentUserId = fixture.SupplierPortalUserId;

        var readiness = await fixture.Service.GetReadinessAsync();
        readiness.Suppliers.Should().ContainSingle(item => item.Id == fixture.Supplier.Id);
        readiness.Suppliers.Should().NotContain(item => item.Id == unlinkedSupplier.Id);

        fixture.CurrentUserId = Guid.NewGuid();

        await fixture.Service.Invoking(service => service.SubmitApplicationAsync(advertised.Id,
                fixture.ApplicationRequest(), "unlinked"))
            .Should().ThrowAsync<ProcurementPrequalificationAuthorizationException>();

        fixture.IsExternalUser = false;
        fixture.TenantId = Guid.NewGuid();
        await fixture.Service.Invoking(service => service.GetAsync(advertised.Id))
            .Should().ThrowAsync<ProcurementPrequalificationNotFoundException>()
            .Where(exception => exception.Code == "PREQUAL_EXERCISE_NOT_FOUND");
    }

    [Fact]
    public async Task AdvertisementFailsWhenCapturedPolicyIsNoLongerCurrent()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.CreateAsync();
        fixture.PolicySet.LifecycleStatus = ProcurementPolicyLifecycleStatus.Retired;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.AdvertiseAsync(
                draft.Id,
                new AdvertiseProcurementPrequalificationRequest
                {
                    AdvertisementReference = "ADVERT-PQ-001",
                    AdvertisementEvidenceReference = "evidence://approved-advertisement",
                    RowVersion = draft.RowVersion
                },
                "stale-policy"))
            .Should().ThrowAsync<ProcurementPrequalificationConflictException>()
            .Where(exception => exception.Code == "PREQUAL_POLICY_STALE");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            CurrentUserId = Guid.NewGuid();
            WorkflowInstanceId = Guid.NewGuid();
            SupplierPortalUserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _currentUser.SetupGet(item => item.UserId).Returns(() => CurrentUserId);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => TenantId);
            _currentUser.SetupGet(item => item.Username).Returns("prequalification@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Prequalification Controller");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.IsExternalUser).Returns(() => IsExternalUser);
            _currentUser.SetupGet(item => item.Roles).Returns(["SuperAdmin"]);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => role == "SuperAdmin");

            Category = new PartnerCategory
            {
                Id = Guid.NewGuid(), TenantId = TenantId, CategoryCode = "WORKS",
                CategoryName = "Civil works", CategoryType = "Contractor", IsActive = true
            };
            OtherCategory = new PartnerCategory
            {
                Id = Guid.NewGuid(), TenantId = TenantId, CategoryCode = "GOODS",
                CategoryName = "Goods", CategoryType = "Supplier", IsActive = true
            };
            Supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PartnerCode = "SUP-PQ-001",
                PartnerName = "Qualified Works Limited", PartnerType = "Contractor",
                RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true
            };
            Supplier.Categories.Add(new BusinessPartnerCategory
            {
                Id = Guid.NewGuid(), BusinessPartnerId = Supplier.Id, CategoryId = Category.Id,
                BusinessPartner = Supplier, Category = Category, IsPrimary = true
            });
            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Code = "PROCUREMENT_SOURCING",
                Name = "Procurement Sourcing", IsActive = true
            };
            WorkflowDefinition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(), TenantId = TenantId, DefinitionKey = Guid.NewGuid(),
                Name = "Prequalification approval", EntityTypeId = entityType.Id,
                EntityType = entityType, Version = 1, IsActive = true,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                PublishedAt = DateTime.UtcNow.AddDays(-1)
            };
            PolicySet = new ProcurementPolicySet
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicyKey = Guid.NewGuid(),
                Code = "TDC-POLICY", Name = "TDC current procurement policy", Version = 3,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                SourceConfigurationProfileId = Guid.NewGuid(),
                DefaultCurrencyCode = "GHS", EffectiveFrom = DateTime.UtcNow.AddDays(-5),
                PublishedAt = DateTime.UtcNow.AddDays(-5)
            };
            var tenant = new Tenant
                { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active };
            Context.AddRange(tenant, Category, OtherCategory, Supplier, entityType, WorkflowDefinition, PolicySet);
            Context.AddRange(Supplier.Categories);
            Context.Add(new BusinessPartnerUser
            {
                Id = Guid.NewGuid(), TenantId = TenantId, BusinessPartnerId = Supplier.Id,
                UserId = SupplierPortalUserId, Role = "Admin", IsActive = true
            });
            Context.SaveChanges();

            _unitOfWork = new UnitOfWork(Context);
            SupplierValidation.Setup(service => service.ValidateForPurchaseOrderAsync(Supplier.Id))
                .ReturnsAsync(SupplierValidationResult.Success());
            ControlEvents.Setup(service => service.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            SodGuard.Setup(service => service.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true });
            Workflow.Setup(service => service.StartApprovalWorkflowAsync(
                    "ProcurementSourcing", It.IsAny<Guid>(), WorkflowDefinition.Id))
                .ReturnsAsync(new WorkflowExecutionResult
                {
                    Success = true, Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = WorkflowInstanceId
                });
            Workflow.Setup(service => service.CanUserApproveAsync(
                    "ProcurementSourcing", It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(true);
            Notifications.Setup(service => service.CreateNotificationAsync(
                    It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new NotificationDto());
            Service = new ProcurementPrequalificationService(
                _unitOfWork, _currentUser.Object, AccessControl.Object, SodGuard.Object,
                ControlEvents.Object, Workflow.Object, SupplierValidation.Object, Notifications.Object);
        }

        public Guid TenantId { get; set; }
        public Guid CurrentUserId { get; set; }
        public bool IsExternalUser { get; set; }
        public Guid WorkflowInstanceId { get; }
        public Guid SupplierPortalUserId { get; }
        public ApplicationDbContext Context { get; }
        public PartnerCategory Category { get; }
        public PartnerCategory OtherCategory { get; }
        public BusinessPartner Supplier { get; }
        public ProcurementPolicySet PolicySet { get; }
        public WorkflowDefinition WorkflowDefinition { get; }
        public ProcurementPrequalificationService Service { get; }
        public Mock<IProcurementAccessControlService> AccessControl { get; } = new();
        public Mock<IProcurementSodGuardService> SodGuard { get; } = new();
        public Mock<IProcurementControlEventService> ControlEvents { get; } = new();
        public Mock<IWorkflowService> Workflow { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();

        public CreateProcurementPrequalificationExerciseRequest CreateRequest() => new()
        {
            Reference = "PQ-2026-001",
            Title = "Civil works supplier prequalification",
            Description = "Controlled annual prequalification of civil works contractors.",
            CategoryIds = [Category.Id],
            OpensAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ClosesAtUtc = DateTime.UtcNow.AddDays(7),
            ValidityMonths = 12,
            PassingScore = 70m,
            PolicySetId = PolicySet.Id,
            WorkflowDefinitionId = WorkflowDefinition.Id,
            Criteria =
            [
                new CreateProcurementPrequalificationCriterionRequest
                {
                    Code = "TECH", Name = "Technical capacity", Weight = 60m,
                    MinimumScore = 60m, IsMandatory = true, RequiresEvidence = true, SortOrder = 1
                },
                new CreateProcurementPrequalificationCriterionRequest
                {
                    Code = "FIN", Name = "Financial capacity", Weight = 40m,
                    MinimumScore = 50m, IsMandatory = true, RequiresEvidence = true, SortOrder = 2
                }
            ]
        };

        public SubmitProcurementPrequalificationApplicationRequest ApplicationRequest() => new()
        {
            BusinessPartnerId = Supplier.Id,
            CategoryIds = [Category.Id],
            Evidence =
            [
                new ProcurementPrequalificationEvidenceRequest
                {
                    CriterionCode = "TECH", EvidenceReference = "evidence://technical",
                    VerificationReference = "verify://technical"
                },
                new ProcurementPrequalificationEvidenceRequest
                {
                    CriterionCode = "FIN", EvidenceReference = "evidence://financial",
                    VerificationReference = "verify://financial"
                }
            ]
        };

        public EvaluateProcurementPrequalificationApplicationRequest EvaluationRequest(
            ProcurementPrequalificationApplicationDto application) => new()
        {
            Remarks = "The independently verified scorecard meets all mandatory thresholds.",
            RecommendationEvidenceReference = "evidence://signed-recommendation",
            RowVersion = application.RowVersion,
            Scores = Context.ProcurementPrequalificationCriteria
                .OrderBy(item => item.SortOrder)
                .Select(item => new ProcurementPrequalificationCriterionScoreRequest
                {
                    CriterionId = item.Id, Score = 80m, MeetsRequirement = true,
                    Reason = "Verified evidence meets the published criterion.",
                    EvidenceReference = $"evidence://evaluation-{item.Code.ToLowerInvariant()}"
                }).ToList()
        };

        public Task<ProcurementPrequalificationExerciseDto> CreateAsync() =>
            Service.CreateAsync(CreateRequest(), "create");

        public Task<ProcurementPrequalificationExerciseDto> AdvertiseAsync(
            ProcurementPrequalificationExerciseDto draft) =>
            Service.AdvertiseAsync(draft.Id, new AdvertiseProcurementPrequalificationRequest
            {
                AdvertisementReference = "ADV-PQ-2026-001",
                AdvertisementEvidenceReference = "evidence://advertisement",
                RowVersion = draft.RowVersion
            }, "advertise");

        public Task<ProcurementPrequalificationApplicationDto> SubmitAsync(
            ProcurementPrequalificationExerciseDto advertised) =>
            Service.SubmitApplicationAsync(advertised.Id, ApplicationRequest(), "application");

        public async Task MoveDeadlineToPastAsync()
        {
            var exercise = await Context.ProcurementPrequalificationExercises.SingleAsync();
            exercise.ClosesAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await Context.SaveChangesAsync();
        }

        public async Task<ProcurementPrequalificationExerciseDto> CompleteApprovedLifecycleAsync()
        {
            var draft = await CreateAsync();
            var advertised = await AdvertiseAsync(draft);
            var application = await SubmitAsync(advertised);
            await MoveDeadlineToPastAsync();
            var closed = await Service.CloseAsync(advertised.Id,
                new CloseProcurementPrequalificationRequest { RowVersion = advertised.RowVersion }, "close");
            CurrentUserId = Guid.NewGuid();
            await Service.EvaluateAsync(closed.Id, application.Id, EvaluationRequest(application), "evaluate");
            var pending = await Service.SubmitDecisionAsync(closed.Id,
                new SubmitProcurementPrequalificationDecisionRequest { RowVersion = closed.RowVersion }, "submit");
            CurrentUserId = Guid.NewGuid();
            Workflow.Setup(service => service.ProcessApprovalStepAsync(
                    "ProcurementSourcing", closed.Id, CurrentUserId, "approve", It.IsAny<string?>()))
                .ReturnsAsync(new WorkflowExecutionResult
                {
                    Success = true, Status = WorkflowInstanceStatus.Completed,
                    WorkflowInstanceId = WorkflowInstanceId
                });
            return await Service.DecideAsync(closed.Id, new DecideProcurementPrequalificationRequest
            {
                Action = "Approve", DecisionReference = "PQ-BOARD-2026-001",
                DecisionEvidenceReference = "evidence://signed-board-decision",
                Reason = "The shared workflow approved the evaluated qualified list.",
                RowVersion = pending.RowVersion
            }, "approve");
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
