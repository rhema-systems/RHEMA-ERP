using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderEvaluatorLineageTests
{
    [Fact]
    public async Task ReleaseOnlyTenderAllowsStandaloneEvaluatorsWithoutAdvancedCommittee()
    {
        var releaseId = Guid.NewGuid();
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-RELEASE-ONLY",
            TenderType = "NCT",
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = releaseId
        });
        fixture.SourceGate = new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = releaseId,
            SourcingCaseId = null
        };

        await fixture.Service.AssignEvaluatorsAsync(fixture.Tender.Id, fixture.Assignment());

        fixture.EvaluationCommittee.Verify(service => service.GetReadinessAsync(
            It.IsAny<ProcurementEvaluationSourceType>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Evaluators.Verify(repository => repository.CreateAsync(
            It.Is<TenderEvaluator>(item => item.TenderId == fixture.Tender.Id)), Times.Once);
    }

    [Fact]
    public async Task ExistingTenderBackfillsCurrentAdvancedCaseBeforeCheckingCommittee()
    {
        var releaseId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-CASE-BACKFILL",
            TenderType = "NCT",
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = releaseId,
            SourcingCaseId = null
        });
        fixture.SourceGate = new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = releaseId,
            SourcingCaseId = caseId
        };
        fixture.EvaluationCommittee.Setup(service => service.GetReadinessAsync(
                ProcurementEvaluationSourceType.Tender, fixture.Tender.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeReadinessDto { HasControl = false });

        await fixture.Service.AssignEvaluatorsAsync(fixture.Tender.Id, fixture.Assignment());

        fixture.Tender.SourcingCaseId.Should().Be(caseId);
        fixture.Tenders.Verify(repository => repository.UpdateAsync(
            It.Is<Tender>(item => item.SourcingCaseId == caseId)), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.EvaluationCommittee.Verify(service => service.GetReadinessAsync(
            ProcurementEvaluationSourceType.Tender, fixture.Tender.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdvancedTenderWithControlledCommitteeRejectsStandaloneEvaluatorChanges()
    {
        var releaseId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-CONTROLLED-COMMITTEE",
            TenderType = "NCT",
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = releaseId,
            SourcingCaseId = caseId
        });
        fixture.SourceGate = new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = releaseId,
            SourcingCaseId = caseId
        };
        fixture.EvaluationCommittee.Setup(service => service.GetReadinessAsync(
                ProcurementEvaluationSourceType.Tender, fixture.Tender.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeReadinessDto { HasControl = true });

        var action = () => fixture.Service.AssignEvaluatorsAsync(
            fixture.Tender.Id, fixture.Assignment());

        await action.Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_COMMITTEE_MEMBERSHIP_REQUIRED");
        fixture.Evaluators.Verify(repository => repository.CreateAsync(
            It.IsAny<TenderEvaluator>()), Times.Never);
    }

    private sealed class Fixture
    {
        internal static readonly Guid TenantId = Guid.NewGuid();
        private static readonly Guid UserId = Guid.NewGuid();

        internal Fixture(Tender tender)
        {
            Tender = tender;
            Tenders.Setup(repository => repository.GetByIdAsync(tender.Id)).ReturnsAsync(tender);
            Tenders.Setup(repository => repository.UpdateAsync(It.IsAny<Tender>()))
                .ReturnsAsync((Tender value) => value);
            Evaluators.Setup(repository => repository.CreateAsync(It.IsAny<TenderEvaluator>()))
                .ReturnsAsync((TenderEvaluator value) => value);
            SourcingCases.Setup(service => service.EnforceSourceEntryAsync(
                    It.IsAny<Guid>(), It.IsAny<ErpSystem.Core.Enums.ProcurementMethodType?>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => SourceGate);
            Notifications.Setup(service => service.SendEvaluationAssignedNotificationAsync(
                    It.IsAny<Guid>(), It.IsAny<List<Guid>>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            CurrentUser.SetupGet(user => user.TenantId).Returns(TenantId);
            CurrentUser.SetupGet(user => user.UserId).Returns(UserId);

            var userStore = new Mock<IUserStore<ApplicationUser>>();
            var userManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            Service = new TenderService(
                Tenders.Object,
                Mock.Of<ITenderItemRepository>(),
                Mock.Of<ITenderDocumentRepository>(),
                Mock.Of<ITenderInvitationRepository>(),
                Mock.Of<ITenderFeeRepository>(),
                Evaluators.Object,
                Mock.Of<ITenderClarificationRepository>(),
                Mock.Of<ITenderRevisionRepository>(),
                Mock.Of<ITenderViewLogRepository>(),
                Mock.Of<ITenderLotRepository>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Notifications.Object,
                Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(),
                UnitOfWork.Object,
                userManager.Object,
                CurrentUser.Object,
                Mock.Of<IAppEventBus>(),
                SourcingCases.Object,
                Mock.Of<IProcurementTenderControlService>(),
                Mock.Of<IProcurementTenderDocumentControlService>(),
                Mock.Of<IProcurementExceptionalSourcingControlService>(),
                EvaluationCommittee.Object,
                Mock.Of<ILogger<TenderService>>());
        }

        internal Tender Tender { get; }
        internal TenderService Service { get; }
        internal Mock<ITenderRepository> Tenders { get; } = new();
        internal Mock<ITenderEvaluatorRepository> Evaluators { get; } = new();
        internal Mock<ITenderNotificationService> Notifications { get; } = new();
        internal Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        internal Mock<IProcurementEvaluationCommitteeControlService> EvaluationCommittee { get; } = new();
        internal Mock<IUnitOfWork> UnitOfWork { get; } = new();
        internal Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        internal ProcurementSourcingCaseEntryGateDto SourceGate { get; set; } = new();

        internal AssignEvaluatorsDto Assignment() => new()
        {
            Evaluators =
            [
                new EvaluatorAssignmentDto
                {
                    UserId = Guid.NewGuid(),
                    Role = "Technical Evaluator",
                    WeightagePercentage = 100m
                }
            ]
        };
    }
}
