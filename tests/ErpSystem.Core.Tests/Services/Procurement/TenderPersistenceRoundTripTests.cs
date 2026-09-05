using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderPersistenceRoundTripTests
{
    [Theory]
    [InlineData("create", "Draft")]
    [InlineData("update", "Draft")]
    [InlineData("submit", "Draft")]
    [InlineData("approve", "Submitted")]
    [InlineData("publish", "Approved")]
    public async Task ConfigurationMismatchBlocksEachLifecycleBoundaryBeforeWrites(string stage, string status)
    {
        var fixture = new Fixture();
        var tender = fixture.SeedDraftTender();
        tender.Status = status;
        tender.EvaluationTemplateId = Guid.NewGuid(); // Fixture returns a QCBS template.
        tender.UseQCBSEvaluation = false;
        fixture.Workflow.Setup(service => service.CanUserApproveAsync("Tender", tender.Id, fixture.UserId))
            .ReturnsAsync(true);
        Func<Task> action = stage switch
        {
            "create" => () => fixture.Service.CreateTenderAsync(new CreateTenderDto
                { EvaluationTemplateId = tender.EvaluationTemplateId, UseQCBSEvaluation = false }),
            "update" => () => fixture.Service.UpdateTenderAsync(tender.Id, new UpdateTenderDto
                { EvaluationTemplateId = tender.EvaluationTemplateId, UseQCBSEvaluation = false }),
            "submit" => () => fixture.Service.SubmitTenderForApprovalAsync(tender.Id, fixture.UserId),
            "approve" => () => fixture.Service.ApproveTenderAsync(tender.Id, fixture.UserId),
            _ => () => fixture.Service.PublishTenderAsync(tender.Id, new PublishTenderDto())
        };
        (await action.Should().ThrowAsync<TenderEvaluationConfigurationException>())
            .Which.Code.Should().Be("TENDER_EVALUATION_METHOD_MISMATCH");
        tender.Status.Should().Be(status);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationRoutesStandardAndAdvancedCasesWithoutDroppingDocumentChecks(bool advanced)
    {
        var fixture = new Fixture(advanced);
        var tender = fixture.SeedDraftTender();
        tender.Status = "Approved";
        tender.SourcePurchaseRequisitionId = fixture.RequisitionId;
        tender.SourcingReleaseId = fixture.ReleaseId;
        tender.SourcingCaseId = fixture.SourcingCaseId;
        tender.EstimatedValue = fixture.EstimatedValue;
        tender.Currency = fixture.Currency;
        var result = await fixture.Service.PublishTenderAsync(tender.Id, new PublishTenderDto
        {
            SubmissionDeadline = DateTime.UtcNow.AddDays(1),
            OpeningDate = DateTime.UtcNow.AddDays(1).AddMinutes(5)
        });

        result.Status.Should().Be("Published");
        result.SourcingCaseId.Should().Be(fixture.SourcingCaseId);
        fixture.Documents.Verify(service => service.EnsurePublicationReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, tender.Id, It.IsAny<DateTime>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Controls.Verify(service => service.PublishAsync(tender.Id,
            It.IsAny<PublishProcurementTenderRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            advanced ? Times.Once() : Times.Never());
        fixture.Documents.Verify(service => service.EnsureDispatchReadyAsync(
            It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(),
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationNoticeDoesNotRequireDocumentReceiptEvenForPrequalifiedTender(bool prequalified)
    {
        var fixture = new Fixture();
        var tender = fixture.SeedDraftTender();
        tender.Status = "Approved";
        tender.SourcePurchaseRequisitionId = fixture.RequisitionId;
        tender.SourcingReleaseId = fixture.ReleaseId;
        tender.SourcingCaseId = fixture.SourcingCaseId;
        tender.EstimatedValue = fixture.EstimatedValue;
        tender.Currency = fixture.Currency;
        tender.RequiresPrequalification = prequalified;
        var recipient = new BusinessPartner { Id = Guid.NewGuid(), TenantId = tender.TenantId, IsActive = true, PartnerType = "Supplier" };
        fixture.Partners.Setup(repository => repository.GetByIdAsync(recipient.Id)).ReturnsAsync(recipient);
        fixture.Documents.Setup(service => service.EnsureDispatchReadyAsync(
            It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(),
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No document receipt exists yet."));

        var result = await fixture.Service.PublishTenderAsync(tender.Id, new PublishTenderDto
        {
            SubmissionDeadline = DateTime.UtcNow.AddDays(1), OpeningDate = DateTime.UtcNow.AddDays(1).AddMinutes(5),
            InvitedBusinessPartnerIds = [recipient.Id]
        });

        result.Status.Should().Be("Published");
        fixture.Notifications.Verify(service => service.SendTenderPublishedNotificationAsync(tender.Id,
            It.IsAny<List<Guid>>(), It.IsAny<List<string>>()), Times.Once);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("blacklisted")]
    [InlineData("deleted")]
    [InlineData("customer")]
    public async Task InvalidNoticeRecipientIsRejectedBeforePublicationOrNotification(string invalid)
    {
        var fixture = new Fixture();
        var tender = fixture.SeedDraftTender();
        tender.Status = "Approved";
        var recipient = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = invalid == "foreign" ? Guid.NewGuid() : tender.TenantId,
            IsActive = invalid != "inactive", IsBlacklisted = invalid == "blacklisted",
            IsDeleted = invalid == "deleted", PartnerType = invalid == "customer" ? "Customer" : "Supplier"
        };
        fixture.Partners.Setup(repository => repository.GetByIdAsync(recipient.Id)).ReturnsAsync(recipient);
        var action = () => fixture.Service.PublishTenderAsync(tender.Id, new PublishTenderDto { InvitedBusinessPartnerIds = [recipient.Id] });

        (await action.Should().ThrowAsync<ProcurementRequisitionSourcingValidationException>()).Which.Code
            .Should().Be("TENDER_PUBLICATION_RECIPIENT_INVALID");
        fixture.Tenders.Verify(repository => repository.UpdateAsync(It.IsAny<Tender>()), Times.Never);
        fixture.Notifications.Verify(service => service.SendTenderPublishedNotificationAsync(It.IsAny<Guid>(),
            It.IsAny<List<Guid>>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task LegacyRfqPublicationStillRequiresAccessBeforeSendingDocumentAttachments()
    {
        var fixture = new Fixture(method: ProcurementMethodType.RequestForQuotation);
        var tender = fixture.SeedDraftTender();
        tender.Status = "Approved";
        tender.TenderType = "RFQ";
        tender.SourcePurchaseRequisitionId = fixture.RequisitionId;
        tender.SourcingReleaseId = fixture.ReleaseId;
        tender.SourcingCaseId = fixture.SourcingCaseId;
        tender.EstimatedValue = fixture.EstimatedValue;
        tender.Currency = fixture.Currency;
        fixture.Documents.Setup(service => service.EnsureDispatchReadyAsync(
            It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(),
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlConflictException("TENDER_DOCUMENT_DISPATCH_RECIPIENTS_NOT_ISSUED", "Record controlled RFQ access first."));

        var action = () => fixture.Service.PublishTenderAsync(tender.Id, new PublishTenderDto
        {
            SubmissionDeadline = DateTime.UtcNow.AddDays(1), OpeningDate = DateTime.UtcNow.AddDays(1).AddMinutes(5),
            ExternalRecipientEmails = ["rfq@example.test"]
        });
        (await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()).Which.Code
            .Should().Be("TENDER_DOCUMENT_DISPATCH_RECIPIENTS_NOT_ISSUED");
        fixture.Tenders.Verify(repository => repository.UpdateAsync(It.IsAny<Tender>()), Times.Never);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("Published")]
    public async Task PublicationRequiresApprovedStatusAndRejectsReplay(string status)
    {
        var fixture = new Fixture();
        var tender = fixture.SeedDraftTender();
        tender.Status = status;
        await fixture.Service.Invoking(service => service.PublishTenderAsync(tender.Id, new PublishTenderDto()))
            .Should().ThrowAsync<InvalidOperationException>();
        fixture.Tenders.Verify(repository => repository.UpdateAsync(It.IsAny<Tender>()), Times.Never);
    }

    [Fact]
    public async Task SubmitWithoutActiveWorkflowFailsClosedAndLeavesDraftUnchanged()
    {
        var fixture = new Fixture();
        var tender = fixture.SeedDraftTender();
        fixture.Workflow.Setup(service => service.SubmitAsync("Tender", tender.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Completed,
                    Message = "No active approval workflow is configured; approval is not required."
                },
                WorkflowOutcome.Approved,
                approvalRequired: false));

        var action = () => fixture.Service.SubmitTenderForApprovalAsync(tender.Id, fixture.UserId);

        var exception = await action.Should().ThrowAsync<ProcurementTenderWorkflowValidationException>();
        exception.Which.Code.Should().Be("TENDER_WORKFLOW_NOT_CONFIGURED");
        tender.Status.Should().Be("Draft");
        fixture.Tenders.Verify(repository => repository.UpdateAsync(It.IsAny<Tender>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.StatusAdapters.Verify(registry => registry.GetAdapter(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitWithActiveWorkflowAppliesPendingOutcomeAndPersistsTender()
    {
        var fixture = new Fixture();
        var tender = fixture.SeedDraftTender();
        fixture.Workflow.Setup(service => service.SubmitAsync("Tender", tender.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress
                },
                WorkflowOutcome.Pending,
                approvalRequired: true));

        await fixture.Service.SubmitTenderForApprovalAsync(tender.Id, fixture.UserId);

        tender.Status.Should().Be("Submitted");
        fixture.Tenders.Verify(repository => repository.UpdateAsync(tender), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.StatusAdapters.Verify(registry => registry.GetAdapter("Tender"), Times.Once);
    }

    [Fact]
    public async Task CreateUpdateAndGetRetainEvaluationTemplateAndEvaluationSettings()
    {
        var fixture = new Fixture();
        var firstTemplateId = Guid.NewGuid();

        var created = await fixture.Service.CreateTenderAsync(new CreateTenderDto
        {
            SourcePurchaseRequisitionId = fixture.RequisitionId,
            Title = "Initial tender",
            Description = "Initial description",
            TenderType = "ITB",
            SubmissionDeadline = DateTime.UtcNow.AddDays(20),
            OpeningDate = DateTime.UtcNow.AddDays(21),
            EstimatedValue = fixture.EstimatedValue,
            Currency = fixture.Currency,
            MinimumPerformanceRating = 4,
            RequiresPrequalification = true,
            AllowPartialBids = true,
            PriceWeightage = 40,
            QualityWeightage = 30,
            DeliveryWeightage = 20,
            ExperienceWeightage = 10,
            EvaluationCriteriaJson = "{\"mandatory\":true}",
            UseQCBSEvaluation = true,
            MinimumTechnicalScore = 75,
            TechnicalWeight = 70,
            FinancialWeight = 30,
            TermsAndConditions = "Initial terms",
            RequiredDocuments = "[{\"documentType\":\"TaxClearance\"}]",
            RequiresAcceptanceDeclaration = true,
            EvaluationTemplateId = firstTemplateId,
            Notes = "Initial notes"
        });

        created.EvaluationTemplateId.Should().Be(firstTemplateId);
        created.UseQCBSEvaluation.Should().BeTrue();
        created.TechnicalWeight.Should().Be(70);
        created.FinancialWeight.Should().Be(30);
        created.MinimumTechnicalScore.Should().Be(75);

        var reopenedAfterCreate = await fixture.Service.GetTenderByIdAsync(created.Id);
        reopenedAfterCreate.Should().NotBeNull();
        reopenedAfterCreate!.EvaluationTemplateId.Should().Be(firstTemplateId);
        reopenedAfterCreate.EvaluationTemplateName.Should().Be("Template A");
        reopenedAfterCreate.RequiredDocuments.Should().Contain("TaxClearance");
        reopenedAfterCreate.RequiresAcceptanceDeclaration.Should().BeTrue();

        // Reproduce an existing draft saved by the former update path, which retained
        // the PR/release but failed to persist the case link.
        fixture.RemoveSourcingCaseLink();

        var secondTemplateId = Guid.NewGuid();
        fixture.RegisterTemplate(secondTemplateId, "Template B");
        await fixture.Service.UpdateTenderAsync(created.Id, new UpdateTenderDto
        {
            Title = "Updated tender",
            Description = "Updated description",
            SubmissionDeadline = DateTime.UtcNow.AddDays(30),
            OpeningDate = DateTime.UtcNow.AddDays(31),
            EstimatedValue = fixture.EstimatedValue,
            Currency = fixture.Currency,
            MinimumPerformanceRating = 5,
            RequiresPrequalification = false,
            AllowPartialBids = false,
            PriceWeightage = 50,
            QualityWeightage = 25,
            DeliveryWeightage = 15,
            ExperienceWeightage = 10,
            EvaluationCriteriaJson = "{\"updated\":true}",
            UseQCBSEvaluation = true,
            MinimumTechnicalScore = 80,
            TechnicalWeight = 65,
            FinancialWeight = 35,
            TermsAndConditions = "Updated terms",
            RequiredDocuments = "[{\"documentType\":\"SSNIT\"}]",
            RequiresAcceptanceDeclaration = false,
            EvaluationTemplateId = secondTemplateId,
            Notes = "Updated notes"
        });

        var reopenedAfterUpdate = await fixture.Service.GetTenderByIdAsync(created.Id);
        reopenedAfterUpdate.Should().NotBeNull();
        reopenedAfterUpdate!.EvaluationTemplateId.Should().Be(secondTemplateId);
        reopenedAfterUpdate.EvaluationTemplateName.Should().Be("Template B");
        reopenedAfterUpdate.Title.Should().Be("Updated tender");
        reopenedAfterUpdate.MinimumPerformanceRating.Should().Be(5);
        reopenedAfterUpdate.PriceWeightage.Should().Be(50);
        reopenedAfterUpdate.QualityWeightage.Should().Be(25);
        reopenedAfterUpdate.DeliveryWeightage.Should().Be(15);
        reopenedAfterUpdate.ExperienceWeightage.Should().Be(10);
        reopenedAfterUpdate.TechnicalWeight.Should().Be(65);
        reopenedAfterUpdate.FinancialWeight.Should().Be(35);
        reopenedAfterUpdate.MinimumTechnicalScore.Should().Be(80);
        reopenedAfterUpdate.RequiredDocuments.Should().Contain("SSNIT");
        reopenedAfterUpdate.Notes.Should().Be("Updated notes");
        reopenedAfterUpdate.SourcingReleaseId.Should().Be(fixture.ReleaseId);
        reopenedAfterUpdate.SourcingCaseId.Should().Be(fixture.SourcingCaseId);
        fixture.SourcingCases.Verify(service => service.RegisterSourceRequestAsync(
            fixture.SourcingCaseId,
            "Tender",
            created.Id,
            created.TenderNumber,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private sealed class Fixture
    {
        private readonly Mock<ITenderRepository> _tenders = new();
        private readonly Dictionary<Guid, string> _templates = new();
        private Tender? _storedTender;

        public Fixture(bool advanced = false, ProcurementMethodType method = ProcurementMethodType.NationalCompetitiveTendering)
        {
            RequisitionId = Guid.NewGuid();
            EstimatedValue = 125000m;
            Currency = "GHS";

            var tenantId = Guid.NewGuid();
            TenantId = tenantId;
            var userId = Guid.NewGuid();
            ReleaseId = Guid.NewGuid();
            SourcingCaseId = Guid.NewGuid();

            _tenders.Setup(repository => repository.GenerateTenderNumberAsync())
                .ReturnsAsync("TND-2026-9001");
            _tenders.Setup(repository => repository.CreateAsync(It.IsAny<Tender>()))
                .Callback<Tender>(tender => _storedTender = tender)
                .ReturnsAsync((Tender tender) => tender);
            _tenders.Setup(repository => repository.UpdateAsync(It.IsAny<Tender>()))
                .Callback<Tender>(tender => _storedTender = tender)
                .ReturnsAsync((Tender tender) => tender);
            _tenders.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(() => HydrateTemplate(_storedTender));

            var items = new Mock<ITenderItemRepository>();
            items.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderItem>());
            var documents = new Mock<ITenderDocumentRepository>();
            documents.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderDocument>());
            var invitations = new Mock<ITenderInvitationRepository>();
            invitations.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderInvitation>());
            var fees = new Mock<ITenderFeeRepository>();
            fees.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderFee>());
            var evaluators = new Mock<ITenderEvaluatorRepository>();
            evaluators.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderEvaluator>());
            var clarifications = new Mock<ITenderClarificationRepository>();
            clarifications.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderClarification>());
            var lots = new Mock<ITenderLotRepository>();
            lots.Setup(repository => repository.GetByTenderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Array.Empty<TenderLot>());

            var bids = new Mock<IGenericRepository<TenderBid>>();
            bids.Setup(repository => repository.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TenderBid, bool>>>()))
                .ReturnsAsync(Array.Empty<TenderBid>());
            var evaluations = new Mock<IGenericRepository<TenderEvaluation>>();
            var sourcingCases = new Mock<IGenericRepository<ProcurementSourcingCase>>();
            var sourcingCaseRows = new[]
            {
                new ProcurementSourcingCase
                {
                    Id = SourcingCaseId,
                    TenantId = tenantId,
                    SelectedMethod = method
                }
            };
            sourcingCases.Setup(repository => repository.GetQueryable(
                    It.IsAny<System.Linq.Expressions.Expression<Func<ProcurementSourcingCase, bool>>>()))
                .Returns((System.Linq.Expressions.Expression<Func<ProcurementSourcingCase, bool>> predicate) =>
                    sourcingCaseRows.Where(predicate.Compile()).AsAsyncQueryable());

            var unitOfWork = new Mock<IUnitOfWork>();
            UnitOfWork = unitOfWork;
            var templates = new Mock<IGenericRepository<EvaluationTemplate>>();
            templates.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Template(id, _templates.GetValueOrDefault(id, "Template A"), tenantId));
            UnitOfWork.Setup(repository => repository.Repository<EvaluationTemplate>()).Returns(templates.Object);
            UnitOfWork.Setup(repository => repository.Repository<TenderBid>()).Returns(bids.Object);
            UnitOfWork.Setup(repository => repository.Repository<TenderEvaluation>()).Returns(evaluations.Object);
            UnitOfWork.Setup(repository => repository.Repository<ProcurementSourcingCase>()).Returns(sourcingCases.Object);
            UnitOfWork.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(provider => provider.TenantId).Returns(tenantId);
            currentUser.SetupGet(provider => provider.UserId).Returns(userId);
            UserId = userId;

            StatusAdapters.Setup(registry => registry.GetAdapter("Tender"))
                .Returns(new TenderWorkflowStatusAdapter());

            SourcingCases.Setup(service => service.EnforceSourceEntryAsync(
                    RequisitionId,
                    It.IsAny<ProcurementMethodType?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
                {
                    SourcingReleaseId = ReleaseId,
                    SourcingCaseId = SourcingCaseId,
                    HasAdvancedAuthorityRoute = advanced,
                    SelectedMethod = method,
                    EstimatedValue = EstimatedValue,
                    CurrencyCode = Currency
                });
            SourcingCases.Setup(service => service.RegisterSourceRequestAsync(
                    SourcingCaseId,
                    "Tender",
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var eventBus = new Mock<IAppEventBus>();
            eventBus.Setup(bus => bus.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!,
                null!,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                null!,
                null!,
                null!,
                null!);
            var roleManager = new Mock<RoleManager<ApplicationRole>>(
                Mock.Of<IRoleStore<ApplicationRole>>(),
                Array.Empty<IRoleValidator<ApplicationRole>>(),
                null!,
                null!,
                null!);

            RegisterTemplate(Guid.Empty, "Template A");

            Service = new TenderService(
                _tenders.Object,
                items.Object,
                documents.Object,
                invitations.Object,
                fees.Object,
                evaluators.Object,
                clarifications.Object,
                Mock.Of<ITenderRevisionRepository>(),
                Mock.Of<ITenderViewLogRepository>(),
                lots.Object,
                Partners.Object,
                Notifications.Object,
                Workflow.Object,
                StatusAdapters.Object,
                UnitOfWork.Object,
                userManager.Object,
                roleManager.Object,
                currentUser.Object,
                eventBus.Object,
                SourcingCases.Object,
                Controls.Object,
                Documents.Object,
                Mock.Of<IProcurementExceptionalSourcingControlService>(),
                Mock.Of<IProcurementEvaluationCommitteeControlService>(),
                NullLogger<TenderService>.Instance);
        }

        public TenderService Service { get; }
        public Guid TenantId { get; }
        public Mock<IBusinessPartnerRepository> Partners { get; } = new();
        public Mock<ITenderNotificationService> Notifications { get; } = new();
        public Mock<IProcurementTenderControlService> Controls { get; } = new();
        public Mock<IProcurementTenderDocumentControlService> Documents { get; } = new();
        public Guid UserId { get; }
        public Guid RequisitionId { get; }
        public Guid ReleaseId { get; }
        public Guid SourcingCaseId { get; }
        public decimal EstimatedValue { get; }
        public string Currency { get; }
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        public Mock<IWorkflowStatusAdapterRegistry> StatusAdapters { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; }
        public Mock<ITenderRepository> Tenders => _tenders;

        public Tender SeedDraftTender()
        {
            _storedTender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-2026-9002",
                Title = "Governed tender",
                TenderType = "ITB",
                Status = "Draft",
                CreatedById = UserId,
                CreatedAt = DateTime.UtcNow
            };
            return _storedTender;
        }

        public void RemoveSourcingCaseLink()
        {
            if (_storedTender is not null)
                _storedTender.SourcingCaseId = null;
        }

        public void RegisterTemplate(Guid id, string name)
        {
            _templates[id] = name;
            if (_storedTender?.EvaluationTemplateId == id)
                _storedTender.EvaluationTemplate = Template(id, name, _storedTender.TenantId);
        }

        private Tender? HydrateTemplate(Tender? tender)
        {
            if (tender?.EvaluationTemplateId is not Guid templateId)
                return tender;

            var templateName = _templates.TryGetValue(templateId, out var registeredName)
                ? registeredName
                : _templates[Guid.Empty];
            tender.EvaluationTemplate = Template(templateId, templateName, tender.TenantId);
            return tender;
        }

        private static EvaluationTemplate Template(Guid id, string name, Guid tenantId) => new()
        {
            Id = id,
            TenantId = tenantId,
            TemplateName = name,
            TemplateCode = name.Replace(" ", "-").ToUpperInvariant(),
            ScoringMethod = "QCBS",
            TechnicalWeight = name == "Template B" ? 65 : 70,
            FinancialWeight = name == "Template B" ? 35 : 30,
            MinimumTechnicalScore = name == "Template B" ? 80 : 75,
            Category = "Goods",
            TenderType = "ITB"
        };
    }
}
