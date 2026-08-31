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

    [Fact]
    public async Task EvaluatorCandidatesContainOnlyActiveSameTenantUsersWithEvaluatePermission()
    {
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-CANDIDATES",
            TenderType = "ITB"
        });

        var candidates = (await fixture.Service.GetEvaluatorCandidatesAsync(fixture.Tender.Id)).ToList();

        candidates.Should().ContainSingle();
        candidates[0].UserId.Should().Be(fixture.EligibleEvaluatorId);
        candidates[0].RoleNames.Should().ContainSingle().Which.Should().Be("TDC_EVALUATOR");
    }

    [Fact]
    public async Task StandaloneAssignmentRejectsUserWithoutTenderEvaluatePermission()
    {
        var releaseId = Guid.NewGuid();
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-INELIGIBLE-EVALUATOR",
            TenderType = "ITB",
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = releaseId
        });
        fixture.SourceGate = new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = releaseId
        };
        var request = fixture.Assignment();
        request.Evaluators[0].UserId = Guid.NewGuid();

        var action = () => fixture.Service.AssignEvaluatorsAsync(fixture.Tender.Id, request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*procurement.tender.evaluate*");
        fixture.Evaluators.Verify(repository => repository.CreateAsync(
            It.IsAny<TenderEvaluator>()), Times.Never);
    }

    [Fact]
    public async Task PublishedControlledTenderAllowsImmutableAddendumRecord()
    {
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-CONTROLLED-ADDENDUM",
            TenderType = "NCT",
            Status = "Published",
            SubmissionDeadline = DateTime.UtcNow.AddDays(3)
        });
        fixture.TenderControls.Setup(service => service.IsControlledTenderMethodAsync(
                fixture.Tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await fixture.Service.CreateRevisionAsync(
            fixture.Tender.Id,
            new CreateRevisionDto
            {
                RevisionType = "Addendum",
                Description = "Clarifies the published delivery schedule.",
                Changes = "Delivery schedule clarification only",
                SendNotifications = false
            });

        result.RevisionType.Should().Be("Addendum");
        fixture.Revisions.Verify(repository => repository.CreateAsync(
            It.Is<TenderRevision>(revision =>
                revision.TenderId == fixture.Tender.Id &&
                revision.RevisionType == "Addendum")), Times.Once);
        fixture.TenderControls.Verify(service => service.IsControlledTenderMethodAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeadlineExtensionRevisionRequiresNewDeadline()
    {
        var fixture = new Fixture(new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId,
            TenderNumber = "TND-DEADLINE-ADDENDUM",
            Status = "Published",
            SubmissionDeadline = DateTime.UtcNow.AddDays(3)
        });

        await fixture.Service.Invoking(service => service.CreateRevisionAsync(
                fixture.Tender.Id,
                new CreateRevisionDto
                {
                    RevisionType = "DeadlineExtension",
                    Description = "Extends the published submission deadline.",
                    SendNotifications = false
                }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a new submission deadline*");
        fixture.Revisions.Verify(repository => repository.CreateAsync(
            It.IsAny<TenderRevision>()), Times.Never);
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
            Revisions.Setup(repository => repository.GetByTenderIdAsync(tender.Id))
                .ReturnsAsync(Array.Empty<TenderRevision>());
            Revisions.Setup(repository => repository.CreateAsync(It.IsAny<TenderRevision>()))
                .ReturnsAsync((TenderRevision value) => value);
            Evaluators.Setup(repository => repository.CreateAsync(It.IsAny<TenderEvaluator>()))
                .ReturnsAsync((TenderEvaluator value) => value);
            Evaluators.Setup(repository => repository.GetByTenderIdAsync(tender.Id))
                .ReturnsAsync(Array.Empty<TenderEvaluator>());
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
            UserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            UserManager.Setup(manager => manager.GetUsersInRoleAsync("TDC_EVALUATOR"))
                .ReturnsAsync(new List<ApplicationUser>
                {
                    new()
                    {
                        Id = EligibleEvaluatorId,
                        TenantId = TenantId,
                        UserName = "tender.evaluator",
                        Email = "tender.evaluator@example.test",
                        FirstName = "Tender",
                        LastName = "Evaluator",
                        IsActive = true
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        TenantId = Guid.NewGuid(),
                        UserName = "other.tenant",
                        FirstName = "Other",
                        LastName = "Tenant",
                        IsActive = true
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        UserName = "inactive.evaluator",
                        FirstName = "Inactive",
                        LastName = "Evaluator",
                        IsActive = false
                    }
                });

            RoleManager = new Mock<RoleManager<ApplicationRole>>(
                Mock.Of<IRoleStore<ApplicationRole>>(),
                Array.Empty<IRoleValidator<ApplicationRole>>(),
                null!, null!, null!);
            RoleManager.Setup(manager => manager.FindByIdAsync(EvaluatorRoleId.ToString()))
                .ReturnsAsync(new ApplicationRole("TDC_EVALUATOR") { Id = EvaluatorRoleId });

            Permissions.Setup(repository => repository.FirstOrDefaultAsync(
                    It.IsAny<System.Linq.Expressions.Expression<Func<Permission, bool>>>(),
                    It.IsAny<System.Linq.Expressions.Expression<Func<Permission, object>>[]>()))
                .ReturnsAsync(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = "procurement.tender.evaluate",
                    DisplayName = "Evaluate tenders",
                    Category = "Procurement",
                    RolePermissions =
                    [
                        new RolePermission
                        {
                            RoleId = EvaluatorRoleId,
                            PermissionId = Guid.NewGuid()
                        }
                    ]
                });
            UnitOfWork.Setup(unit => unit.Repository<Permission>()).Returns(Permissions.Object);

            Service = new TenderService(
                Tenders.Object,
                Mock.Of<ITenderItemRepository>(),
                Mock.Of<ITenderDocumentRepository>(),
                Mock.Of<ITenderInvitationRepository>(),
                Mock.Of<ITenderFeeRepository>(),
                Evaluators.Object,
                Mock.Of<ITenderClarificationRepository>(),
                Revisions.Object,
                Mock.Of<ITenderViewLogRepository>(),
                Mock.Of<ITenderLotRepository>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Notifications.Object,
                Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(),
                UnitOfWork.Object,
                UserManager.Object,
                RoleManager.Object,
                CurrentUser.Object,
                Mock.Of<IAppEventBus>(),
                SourcingCases.Object,
                TenderControls.Object,
                Mock.Of<IProcurementTenderDocumentControlService>(),
                Mock.Of<IProcurementExceptionalSourcingControlService>(),
                EvaluationCommittee.Object,
                Mock.Of<ILogger<TenderService>>());
        }

        internal Tender Tender { get; }
        internal Guid EligibleEvaluatorId { get; } = Guid.NewGuid();
        internal Guid EvaluatorRoleId { get; } = Guid.NewGuid();
        internal TenderService Service { get; }
        internal Mock<ITenderRepository> Tenders { get; } = new();
        internal Mock<ITenderEvaluatorRepository> Evaluators { get; } = new();
        internal Mock<ITenderRevisionRepository> Revisions { get; } = new();
        internal Mock<ITenderNotificationService> Notifications { get; } = new();
        internal Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        internal Mock<IProcurementEvaluationCommitteeControlService> EvaluationCommittee { get; } = new();
        internal Mock<IProcurementTenderControlService> TenderControls { get; } = new();
        internal Mock<IUnitOfWork> UnitOfWork { get; } = new();
        internal Mock<IGenericRepository<Permission>> Permissions { get; } = new();
        internal Mock<UserManager<ApplicationUser>> UserManager { get; }
        internal Mock<RoleManager<ApplicationRole>> RoleManager { get; }
        internal Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        internal ProcurementSourcingCaseEntryGateDto SourceGate { get; set; } = new();

        internal AssignEvaluatorsDto Assignment() => new()
        {
            Evaluators =
            [
                new EvaluatorAssignmentDto
                {
                    UserId = EligibleEvaluatorId,
                    Role = "Technical Evaluator",
                    WeightagePercentage = 100m
                }
            ]
        };
    }
}
