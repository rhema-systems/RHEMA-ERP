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

        public Fixture()
        {
            RequisitionId = Guid.NewGuid();
            EstimatedValue = 125000m;
            Currency = "GHS";

            var tenantId = Guid.NewGuid();
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
                    SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
                }
            };
            sourcingCases.Setup(repository => repository.GetQueryable(
                    It.IsAny<System.Linq.Expressions.Expression<Func<ProcurementSourcingCase, bool>>>()))
                .Returns((System.Linq.Expressions.Expression<Func<ProcurementSourcingCase, bool>> predicate) =>
                    sourcingCaseRows.Where(predicate.Compile()).AsAsyncQueryable());

            var unitOfWork = new Mock<IUnitOfWork>();
            UnitOfWork = unitOfWork;
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
                    SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering,
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
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<ITenderNotificationService>(),
                Workflow.Object,
                StatusAdapters.Object,
                UnitOfWork.Object,
                userManager.Object,
                roleManager.Object,
                currentUser.Object,
                eventBus.Object,
                SourcingCases.Object,
                Mock.Of<IProcurementTenderControlService>(),
                Mock.Of<IProcurementTenderDocumentControlService>(),
                Mock.Of<IProcurementExceptionalSourcingControlService>(),
                Mock.Of<IProcurementEvaluationCommitteeControlService>(),
                NullLogger<TenderService>.Instance);
        }

        public TenderService Service { get; }
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
                TenantId = Guid.NewGuid(),
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
            Category = "Goods",
            TenderType = "ITB"
        };
    }
}
