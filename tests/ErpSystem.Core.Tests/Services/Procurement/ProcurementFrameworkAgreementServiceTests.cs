using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
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

public sealed class ProcurementFrameworkAgreementServiceTests
{
    [Fact]
    public async Task CreateCapturesExactReadySourceEligibilityPricesAndAllDecisionKeys()
    {
        await using var fixture = new Fixture();

        var created = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-create");

        created.Status.Should().Be(ProcurementFrameworkAgreementStatus.Draft);
        created.SourceId.Should().Be(fixture.Rfq.Id);
        created.BusinessPartnerId.Should().Be(fixture.Supplier.Id);
        created.SourceIntegrityHash.Should().Be(fixture.Readiness.IntegrityHash);
        created.SupplierEligibilityDecisionHash.Should().Be(Fixture.EligibilityHash);
        created.PriceLines.Should().ContainSingle(line =>
            line.InventoryItemId == fixture.Item.Id &&
            line.UnitPrice == 25m);
        created.Categories.Should().ContainSingle(category =>
            category.PartnerCategoryId == fixture.Category.Id);
        created.CallOffAuthorities.Should().ContainSingle(authority =>
            authority.AuthorityKind == ProcurementFrameworkAuthorityKind.Role);
        created.AvailableCeiling.Should().Be(created.CeilingAmount);
        created.IntegrityHash.Should().HaveLength(64);

        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.Action == "Created" &&
                request.DecisionKeys.Count == 14 &&
                request.DecisionKeys.Contains("DEC-001") &&
                request.DecisionKeys.Contains("DEC-014")),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.SupplierValidation.Verify(service =>
            service.EnforceEligibilityAsync(
                It.Is<SupplierEligibilityEvaluationRequest>(request =>
                    request.Boundary == SupplierEligibilityBoundary.Contract &&
                    request.BusinessPartnerId == fixture.Supplier.Id &&
                    request.CategoryIds.SequenceEqual(new[] { fixture.Category.Id })),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewerBlockedReadinessRevokesOlderReadyDecision()
    {
        await using var fixture = new Fixture();
        fixture.Context.Add(new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            SourceType = ProcurementAwardReadinessSourceType.RequestForQuotation,
            SourceId = fixture.Rfq.Id,
            SourceReference = fixture.Rfq.RfqNumber,
            Method = ProcurementMethodType.RequestForQuotation,
            DecisionSequence = fixture.Readiness.DecisionSequence + 1,
            Status = ProcurementAwardReadinessDecisionStatus.Blocked,
            RecommendationSubjectType = "RequestForQuotationQuote",
            RecommendedBusinessPartnerIdsJson =
                JsonSerializer.Serialize(new[] { fixture.Supplier.Id }),
            SourceIntegrityHash = new string('d', 64),
            IntegrityHash = new string('e', 64),
            IdempotencyKey = "framework-readiness-blocked",
            CorrelationId = "framework-readiness-blocked",
            EvaluatedAtUtc = DateTime.UtcNow,
            EvaluatedByUserId = Guid.NewGuid(),
            EvaluatedByName = "Award control reviewer"
        });
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-blocked-readiness");

        await action.Should()
            .ThrowAsync<ProcurementFrameworkAgreementConflictException>()
            .Where(exception =>
                exception.Code == "FRAMEWORK_AGREEMENT_AWARD_READINESS_REQUIRED");
        fixture.Context.ProcurementFrameworkAgreements.Should().BeEmpty();
        (await fixture.Service.GetSourceOptionsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SourceFamilyUniquenessIsRecheckedInsideSerializableTransaction()
    {
        await using var fixture = new Fixture();
        var injected = false;
        fixture.AfterTransactionStarted = async () =>
        {
            if (injected)
                return;
            injected = true;
            fixture.AfterTransactionStarted = null;
            await fixture.InsertCompetingAgreementAsync();
        };

        var action = () => fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-concurrent-source");

        await action.Should()
            .ThrowAsync<ProcurementFrameworkAgreementConflictException>()
            .Where(exception =>
                exception.Code == "FRAMEWORK_AGREEMENT_SOURCE_ALREADY_REGISTERED");
        fixture.LastTransactionIsolationLevel.Should().Be(IsolationLevel.Serializable);
        fixture.Context.ProcurementFrameworkAgreements.Count(item =>
                item.TenantId == fixture.TenantId &&
                item.SourceType ==
                ProcurementAwardReadinessSourceType.RequestForQuotation &&
                item.SourceId == fixture.Rfq.Id &&
                item.BusinessPartnerId == fixture.Supplier.Id)
            .Should().Be(1);
    }

    [Fact]
    public async Task FuturePublishedRevisionKeepsCurrentAgreementUntilEffectiveLifecycleProcessing()
    {
        await using var fixture = new Fixture();
        var firstDraft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(60)),
            "framework-v1-create");
        await fixture.AttachRequiredDocumentAsync(firstDraft);
        var firstSubmitted = await fixture.Service.SubmitAsync(
            firstDraft.Id,
            Lifecycle(firstDraft.RowVersion, "Submit first agreement."),
            "framework-v1-submit");
        fixture.CompleteLatestWorkflow();
        fixture.SetUser(Guid.NewGuid());
        var firstPublished = await fixture.Service.ApproveAsync(
            firstDraft.Id,
            Lifecycle(firstSubmitted.RowVersion, "Approve first agreement."),
            "framework-v1-approve");

        var futureStart = DateTime.UtcNow.AddDays(20);
        var secondDraft = await fixture.Service.CloneAsync(
            firstPublished.Id,
            new CloneProcurementFrameworkAgreementRequest
            {
                RowVersion = firstPublished.RowVersion,
                EffectiveFromUtc = futureStart,
                EffectiveToUtc = futureStart.AddMonths(12),
                WorkflowDefinitionId = fixture.Workflow.Id,
                ChangeSummary = "Scheduled annual commercial revision."
            },
            "framework-v2-clone");
        await fixture.AttachRequiredDocumentAsync(secondDraft);
        var secondSubmitted = await fixture.Service.SubmitAsync(
            secondDraft.Id,
            Lifecycle(secondDraft.RowVersion, "Submit scheduled revision."),
            "framework-v2-submit");
        fixture.CompleteLatestWorkflow();
        fixture.SetUser(Guid.NewGuid());
        var secondPublished = await fixture.Service.ApproveAsync(
            secondDraft.Id,
            Lifecycle(secondSubmitted.RowVersion, "Approve scheduled revision."),
            "framework-v2-approve");

        (await fixture.Service.GetAsync(firstPublished.Id)).Status.Should()
            .Be(ProcurementFrameworkAgreementStatus.Published);
        secondPublished.Status.Should()
            .Be(ProcurementFrameworkAgreementStatus.Published);

        var beforeEffective = await fixture.Service.ProcessLifecycleAsync(
            atUtc: futureStart.AddMinutes(-1));
        beforeEffective.Should().Be(0);
        (await fixture.Service.GetAsync(firstPublished.Id)).Status.Should()
            .Be(ProcurementFrameworkAgreementStatus.Published);

        var atEffective = await fixture.Service.ProcessLifecycleAsync(
            atUtc: futureStart.AddMinutes(1));
        atEffective.Should().Be(1);
        var superseded = await fixture.Service.GetAsync(firstPublished.Id);
        superseded.Status.Should()
            .Be(ProcurementFrameworkAgreementStatus.Superseded);
        superseded.SupersededByAgreementId.Should().Be(secondPublished.Id);
    }

    [Fact]
    public async Task CompletedWorkflowCannotBeRecordedAsRejection()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-reject-create");
        await fixture.AttachRequiredDocumentAsync(draft);
        var submitted = await fixture.Service.SubmitAsync(
            draft.Id,
            Lifecycle(draft.RowVersion, "Submit agreement."),
            "framework-reject-submit");
        fixture.CompleteLatestWorkflow();
        fixture.SetUser(Guid.NewGuid());

        var action = () => fixture.Service.RejectAsync(
            draft.Id,
            Lifecycle(submitted.RowVersion, "Contradict approved workflow."),
            "framework-reject-attempt");

        await action.Should()
            .ThrowAsync<ProcurementFrameworkAgreementConflictException>()
            .Where(exception =>
                exception.Code == "FRAMEWORK_AGREEMENT_WORKFLOW_NOT_REJECTED");
    }

    [Fact]
    public async Task AgreementReadsFailClosedAcrossTenants()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-tenant-create");
        fixture.SetTenant(Guid.NewGuid());

        var action = () => fixture.Service.GetAsync(created.Id);

        await action.Should()
            .ThrowAsync<ProcurementFrameworkAgreementNotFoundException>();
    }

    [Fact]
    public async Task StaleRowVersionCannotMutateDraftOrAppendAnUpdateEvent()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-concurrency-create");
        var request = fixture.UpdateRequest(
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        request.Title = "Stale client overwrite";

        var action = () => fixture.Service.UpdateAsync(
            created.Id, request, "framework-concurrency-stale");

        await action.Should()
            .ThrowAsync<ProcurementFrameworkAgreementConflictException>()
            .Where(exception =>
                exception.Code == "FRAMEWORK_AGREEMENT_CONCURRENCY_CONFLICT");
        (await fixture.Service.GetAsync(created.Id)).Title.Should()
            .Be("TDC governed goods framework");
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(eventRequest =>
                eventRequest.Action == "Updated"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExternalApplicantCannotCreateFrameworkAgreement()
    {
        await using var fixture = new Fixture();
        fixture.SetExternal(true);

        var action = () => fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "framework-external-create");

        await action.Should()
            .ThrowAsync<ProcurementFrameworkAgreementAuthorizationException>()
            .WithMessage("*External portal users*");
        fixture.Context.ProcurementFrameworkAgreements.Should().BeEmpty();
        fixture.AccessControl.Verify(service =>
            service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtensionRequiresIndependentWorkflowOutcomeAndChangesOnlyEffectiveEnd()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(60)),
            "framework-extension-create");
        await fixture.AttachRequiredDocumentAsync(draft);
        var submitted = await fixture.Service.SubmitAsync(
            draft.Id,
            Lifecycle(draft.RowVersion, "Submit agreement."),
            "framework-extension-submit");
        fixture.CompleteLatestWorkflow();
        fixture.SetUser(Guid.NewGuid());
        var published = await fixture.Service.ApproveAsync(
            draft.Id,
            Lifecycle(submitted.RowVersion, "Approve agreement."),
            "framework-extension-publish");
        var proposedEnd = published.EffectiveEndUtc.AddMonths(6);
        var extension = await fixture.Service.RequestExtensionAsync(
            published.Id,
            new RequestProcurementFrameworkAgreementExtension
            {
                AgreementRowVersion = published.RowVersion,
                ProposedEndUtc = proposedEnd,
                WorkflowDefinitionId = fixture.Workflow.Id,
                Reason = "Retain pricing while the replacement tender completes.",
                Evidence = Evidence("EXTENSION-MINUTE-001")
            },
            "framework-extension-request");
        fixture.CompleteLatestWorkflow();
        fixture.SetUser(Guid.NewGuid());

        var approved = await fixture.Service.DecideExtensionAsync(
            published.Id,
            extension.Id,
            new DecideProcurementFrameworkAgreementExtension
            {
                RowVersion = extension.RowVersion,
                Approve = true,
                Comment = "Extension workflow completed.",
                Evidence = Evidence("EXTENSION-APPROVAL-001")
            },
            "framework-extension-approve");
        var current = await fixture.Service.GetAsync(published.Id);

        approved.Status.Should().Be(ProcurementFrameworkExtensionStatus.Approved);
        current.EffectiveToUtc.Should().Be(published.EffectiveToUtc);
        current.EffectiveEndUtc.Should().BeCloseTo(proposedEnd, TimeSpan.FromSeconds(1));
    }

    private static ProcurementFrameworkAgreementLifecycleRequest Lifecycle(
        string rowVersion,
        string comment) =>
        new()
        {
            RowVersion = rowVersion,
            Comment = comment,
            Evidence = Evidence($"MINUTE-{Guid.NewGuid():N}")
        };

    private static List<ProcurementControlEventEvidenceReference> Evidence(
        string reference) =>
        [
            new ProcurementControlEventEvidenceReference
            {
                ReferenceKind =
                    ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = reference,
                Label = "Retained lifecycle evidence",
                RequirementKey = "FrameworkAgreement.Lifecycle"
            }
        ];

    private sealed class Fixture : IAsyncDisposable
    {
        public const string EligibilityHash =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private Guid _tenantId;
        private Guid _userId;
        private bool _isExternal;
        private readonly RecordingUnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly List<WorkflowInstance> _workflowInstances = new();

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            _tenantId = TenantId;
            _userId = UserId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _unitOfWork = new RecordingUnitOfWork(new UnitOfWork(Context));

            Supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "SUP-FRAME-001",
                PartnerName = "Framework Supplier",
                PartnerType = "Supplier",
                ApprovalStatus = "Approved",
                RegistrationStatus = "Active",
                IsActive = true
            };
            Category = new PartnerCategory
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CategoryCode = "GOODS",
                CategoryName = "Goods",
                CategoryType = "Supplier",
                IsActive = true
            };
            Item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ItemCode = "ITM-001",
                Name = "Governed item",
                CategoryId = Guid.NewGuid(),
                UnitOfMeasure = "EA",
                Status = ItemStatus.Active
            };
            Workflow = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "Framework approval",
                EntityTypeId = Guid.NewGuid(),
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true
            };
            Rfq = new RequestForQuotation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RfqNumber = "RFQ-FRAME-001",
                Title = "Framework source",
                Status = "Awarded",
                Currency = "GHS",
                AwardedBusinessPartnerId = Supplier.Id,
                AwardedAt = DateTime.UtcNow.AddDays(-2)
            };
            Rfq.AwardLines.Add(new RequestForQuotationAwardLine
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RfqId = Rfq.Id,
                RfqItemId = Guid.NewGuid(),
                BusinessPartnerId = Supplier.Id,
                QuoteId = Guid.NewGuid(),
                UnitPrice = 25m,
                LineTotal = 1000m
            });
            Readiness = new ProcurementAwardReadinessDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType = ProcurementAwardReadinessSourceType.RequestForQuotation,
                SourceId = Rfq.Id,
                SourceReference = Rfq.RfqNumber,
                Method = ProcurementMethodType.RequestForQuotation,
                DecisionSequence = 1,
                Status = ProcurementAwardReadinessDecisionStatus.Ready,
                RecommendationSubjectType = "RequestForQuotationQuote",
                RecommendedBusinessPartnerIdsJson =
                    JsonSerializer.Serialize(new[] { Supplier.Id }),
                SourceIntegrityHash = new string('a', 64),
                IntegrityHash = new string('c', 64),
                IdempotencyKey = "framework-readiness",
                CorrelationId = "framework-readiness",
                EvaluatedAtUtc = DateTime.UtcNow.AddDays(-1),
                EvaluatedByUserId = Guid.NewGuid(),
                EvaluatedByName = "Award approver"
            };
            Context.AddRange(
                new Tenant
                {
                    Id = TenantId,
                    Code = "TDC",
                    Name = "TDC",
                    Status = TenantStatus.Active
                },
                Supplier,
                Category,
                Item,
                Workflow,
                Rfq,
                Readiness,
                new Currency
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CurrencyCode = "GHS",
                    CurrencyName = "Ghana cedi",
                    IsActive = true
                });
            Context.SaveChanges();

            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.IsExternalUser)
                .Returns(() => _isExternal);
            _currentUser.SetupGet(item => item.UserId).Returns(() => _userId);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _currentUser.SetupGet(item => item.Username).Returns("procurement@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("TDC Procurement User");
            _currentUser.SetupGet(item => item.Roles)
                .Returns(new[] { "TDC_PROCUREMENT_OFFICER" });
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns(false);

            AccessControl
                .Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ALLOWED",
                    Message = "Allowed"
                });
            SodGuard
                .Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = true,
                    Code = "ALLOWED",
                    Message = "Independent actor"
                });
            SupplierValidation
                .Setup(item => item.EnforceEligibilityAsync(
                    It.IsAny<SupplierEligibilityEvaluationRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((SupplierEligibilityEvaluationRequest request,
                    CancellationToken _) => new SupplierValidationResult
                    {
                        IsValid = true,
                        BusinessPartnerId = request.BusinessPartnerId,
                        TenantId = TenantId,
                        Boundary = request.Boundary,
                        DecisionHash = EligibilityHash,
                        DecisionKeys = Enumerable.Range(1, 14)
                            .Select(number => $"DEC-{number:000}")
                            .ToList()
                    });
            ControlEvents
                .Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementControlEventWriteRequest request,
                    CancellationToken _) => new ProcurementControlEventDto
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        EventKey = request.EventKey,
                        Action = request.Action,
                        DecisionKeys = request.DecisionKeys
                    });
            WorkflowInstances
                .Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<object?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Guid definitionId, Guid entityTypeId,
                    string entityId, Guid startedById, object? _,
                    CancellationToken cancellationToken) =>
                {
                    var instance = new WorkflowInstance
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        WorkflowDefinitionId = definitionId,
                        EntityTypeId = entityTypeId,
                        EntityId = Guid.Parse(entityId),
                        InitiatedById = startedById,
                        Status = WorkflowInstanceStatus.InProgress
                    };
                    _workflowInstances.Add(instance);
                    Context.Add(instance);
                    await Context.SaveChangesAsync(cancellationToken);
                    return instance;
                });
            NotificationTopics
                .Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Documents
                .Setup(item => item.RegisterAsync(
                    It.IsAny<CentralDocumentRepositoryRegistration>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    CentralDocumentRepositoryRegistration request,
                    CancellationToken _) => new CentralDocumentRepositoryLink
                    {
                        DocumentRecordId = Guid.NewGuid(),
                        DocumentVersionId = Guid.NewGuid(),
                        FileUploadRecordId = request.FileUploadRecordId,
                        DocumentReference = $"DMS-{request.SourceRecordId:N}",
                        VersionNumber = "v1.0"
                    });

            Service = new ProcurementFrameworkAgreementService(
                _unitOfWork,
                _currentUser.Object,
                AccessControl.Object,
                SodGuard.Object,
                ControlEvents.Object,
                WorkflowInstances.Object,
                SupplierValidation.Object,
                Documents.Object,
                NotificationTopics.Object,
                NullLogger<ProcurementFrameworkAgreementService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public BusinessPartner Supplier { get; }
        public PartnerCategory Category { get; }
        public InventoryItem Item { get; }
        public WorkflowDefinition Workflow { get; }
        public RequestForQuotation Rfq { get; }
        public ProcurementAwardReadinessDecision Readiness { get; }
        public ProcurementFrameworkAgreementService Service { get; }
        public Mock<IProcurementAccessControlService> AccessControl { get; } = new();
        public Mock<IProcurementSodGuardService> SodGuard { get; } = new();
        public Mock<IProcurementControlEventService> ControlEvents { get; } = new();
        public Mock<IWorkflowInstanceService> WorkflowInstances { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<ICentralDocumentRepositoryFileService> Documents { get; } = new();
        public Mock<INotificationTopicPublisher> NotificationTopics { get; } = new();
        public IsolationLevel? LastTransactionIsolationLevel =>
            _unitOfWork.LastIsolationLevel;
        public Func<Task>? AfterTransactionStarted
        {
            get => _unitOfWork.AfterTransactionStarted;
            set => _unitOfWork.AfterTransactionStarted = value;
        }

        public CreateProcurementFrameworkAgreementRequest CreateRequest(
            DateTime? effectiveFrom = null,
            DateTime? effectiveTo = null)
        {
            var from = effectiveFrom ?? DateTime.UtcNow.AddDays(1);
            var to = effectiveTo ?? from.AddYears(1);
            return new CreateProcurementFrameworkAgreementRequest
            {
                SourceType = ProcurementAwardReadinessSourceType.RequestForQuotation,
                SourceId = Rfq.Id,
                BusinessPartnerId = Supplier.Id,
                Title = "TDC governed goods framework",
                CeilingAmount = 1000m,
                CurrencyCode = "GHS",
                EffectiveFromUtc = from,
                EffectiveToUtc = to,
                WorkflowDefinitionId = Workflow.Id,
                Description = "Framework test fixture",
                TermsSummary = "Call-offs remain outside TDC-0401.",
                CategoryIds = [Category.Id],
                PriceLines =
                [
                    new SaveProcurementFrameworkPriceListLineRequest
                    {
                        InventoryItemId = Item.Id,
                        UnitPrice = 25m,
                        MinimumQuantity = 1m,
                        MaximumQuantity = 100m,
                        LeadTimeDays = 5
                    }
                ],
                CallOffAuthorities =
                [
                    new SaveProcurementFrameworkCallOffAuthorityRequest
                    {
                        AuthorityKind = ProcurementFrameworkAuthorityKind.Role,
                        AuthorityValue = "Procurement Officer",
                        DisplayName = "Procurement Officer",
                        MaximumCallOffAmount = 500m,
                        ValidFromUtc = from,
                        ValidToUtc = to,
                        IsActive = true
                    }
                ]
            };
        }

        public UpdateProcurementFrameworkAgreementRequest UpdateRequest(
            string rowVersion)
        {
            var request = CreateRequest();
            return new UpdateProcurementFrameworkAgreementRequest
            {
                RowVersion = rowVersion,
                Title = request.Title,
                CeilingAmount = request.CeilingAmount,
                CurrencyCode = request.CurrencyCode,
                EffectiveFromUtc = request.EffectiveFromUtc,
                EffectiveToUtc = request.EffectiveToUtc,
                WorkflowDefinitionId = request.WorkflowDefinitionId,
                Description = request.Description,
                TermsSummary = request.TermsSummary,
                CategoryIds = request.CategoryIds,
                PriceLines = request.PriceLines,
                CallOffAuthorities = request.CallOffAuthorities
            };
        }

        public void SetUser(Guid userId) => _userId = userId;
        public void SetTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetExternal(bool isExternal) => _isExternal = isExternal;

        public void CompleteLatestWorkflow()
        {
            var instance = _workflowInstances.Last();
            instance.Status = WorkflowInstanceStatus.Completed;
            instance.CompletedDate = DateTime.UtcNow;
            Context.SaveChanges();
        }

        public async Task InsertCompetingAgreementAsync()
        {
            var now = DateTime.UtcNow;
            Context.Add(new ProcurementFrameworkAgreement
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AgreementKey = Guid.NewGuid(),
                AgreementNumber = $"FA-RACE-{Guid.NewGuid():N}"[..20],
                Title = "Concurrent framework",
                Version = 1,
                Status = ProcurementFrameworkAgreementStatus.Draft,
                BusinessPartnerId = Supplier.Id,
                SourceType =
                    ProcurementAwardReadinessSourceType.RequestForQuotation,
                SourceId = Rfq.Id,
                SourceReference = Rfq.RfqNumber,
                AwardReadinessDecisionId = Readiness.Id,
                SourceIntegrityHash = Readiness.IntegrityHash,
                SupplierEligibilityDecisionHash = EligibilityHash,
                PriceListReference = $"FPL-RACE-{Guid.NewGuid():N}"[..21],
                PriceListVersion = 1,
                CeilingAmount = 1000m,
                CurrencyCode = "GHS",
                EffectiveFromUtc = now.AddDays(1),
                EffectiveToUtc = now.AddYears(1),
                WorkflowDefinitionId = Workflow.Id,
                CreationCorrelationId = "framework-concurrent-winner",
                LastOperationCorrelationId = "framework-concurrent-winner",
                LastOperation = "Created",
                SnapshotJson = "{}",
                IntegrityHash = new string('f', 64),
                RowVersion = Guid.NewGuid().ToByteArray(),
                CreatedAt = now,
                CreatedBy = "Concurrent request",
                CreatedById = Guid.NewGuid()
            });
            await Context.SaveChangesAsync();
        }

        public async Task<ProcurementFrameworkAgreementDocumentDto>
            AttachRequiredDocumentAsync(ProcurementFrameworkAgreementDto agreement)
        {
            var upload = new FileUploadRecord
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Category = ControlledFileUploadCategories.DocumentManagement,
                FilePath = $"secure/{Guid.NewGuid():N}.pdf",
                StoredFileName = $"{Guid.NewGuid():N}.pdf",
                OriginalFileName = "signed-framework.pdf",
                ContentType = "application/pdf",
                FileSize = 128,
                StorageProvider = "test",
                UploadedByUserId = _userId,
                VirusScanStatus = FileVirusScanStatus.Clean,
                ScannedAtUtc = DateTime.UtcNow,
                CreatedById = _userId
            };
            Context.Add(upload);
            await Context.SaveChangesAsync();
            return await Service.AddDocumentAsync(
                agreement.Id,
                new AddProcurementFrameworkAgreementDocumentRequest
                {
                    FileUploadRecordId = upload.Id,
                    DocumentType = "Signed agreement",
                    Title = "Executed framework agreement",
                    IsRequired = true
                },
                $"framework-document-{agreement.Id:N}");
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public IsolationLevel? LastIsolationLevel { get; private set; }
        public Func<Task>? AfterTransactionStarted { get; set; }

        public IAccountRepository Accounts => inner.Accounts;
        public IAccountSegmentStructureRepository AccountSegmentStructures =>
            inner.AccountSegmentStructures;
        public IAccountSegmentValueRepository AccountSegmentValues =>
            inner.AccountSegmentValues;
        public ISegmentLookupValueRepository SegmentLookupValues =>
            inner.SegmentLookupValues;
        public bool HasActiveTransaction => inner.HasActiveTransaction;

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);

        public int SaveChanges() => inner.SaveChanges();

        public async Task BeginTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            LastIsolationLevel = IsolationLevel.ReadCommitted;
            await inner.BeginTransactionAsync(cancellationToken);
            if (AfterTransactionStarted is not null)
                await AfterTransactionStarted();
        }

        public async Task BeginTransactionAsync(
            IsolationLevel isolationLevel,
            CancellationToken cancellationToken = default)
        {
            LastIsolationLevel = isolationLevel;
            await inner.BeginTransactionAsync(isolationLevel, cancellationToken);
            if (AfterTransactionStarted is not null)
                await AfterTransactionStarted();
        }

        public Task AcquireTransactionLockAsync(
            string resource,
            CancellationToken cancellationToken = default) =>
            inner.AcquireTransactionLockAsync(resource, cancellationToken);

        public Task CommitAsync(
            CancellationToken cancellationToken = default) =>
            inner.CommitAsync(cancellationToken);

        public Task RollbackAsync(
            CancellationToken cancellationToken = default) =>
            inner.RollbackAsync(cancellationToken);

        public void ClearTrackedChanges() => inner.ClearTrackedChanges();

        public IGenericRepository<T> Repository<T>() where T : BaseEntity =>
            inner.Repository<T>();

        public Task ExecuteInStrategyAsync(
            Func<Task> operation,
            CancellationToken cancellationToken = default) =>
            inner.ExecuteInStrategyAsync(operation, cancellationToken);

        public Task<T> ExecuteInStrategyAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            inner.ExecuteInStrategyAsync(operation, cancellationToken);

        public void Dispose() => inner.Dispose();
    }
}
