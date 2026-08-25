using System.Text.Json;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceFrameworkTests
{
    [Fact]
    public async Task ExceptionalFrameworkLineageResolvesControlByTenderId()
    {
        await using var fixture = new Fixture();

        var resolved = await fixture.Service.ResolveFrameworkCallOffAsync(
            fixture.CallOff,
            fixture.Agreement,
            "framework-exception-source");

        resolved.SourcingCaseId.Should().Be(fixture.SourcingCase.Id);
        resolved.SourcingReleaseId.Should().Be(fixture.Release.Id);
        resolved.AwardReadinessDecisionId.Should().Be(fixture.Readiness.Id);
    }

    [Fact]
    public async Task NewerBlockedReadinessRevokesFrameworkCallOffSource()
    {
        await using var fixture = new Fixture();
        fixture.Context.Add(new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            SourceType =
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            SourceId = fixture.Tender.Id,
            SourceReference = fixture.Tender.TenderNumber,
            DecisionSequence = fixture.Readiness.DecisionSequence + 1,
            Status = ProcurementAwardReadinessDecisionStatus.Blocked,
            RecommendedBusinessPartnerIdsJson =
                JsonSerializer.Serialize(new[] { fixture.SupplierId }),
            EvaluatedAtUtc = DateTime.UtcNow,
            EvaluatedByUserId = Guid.NewGuid(),
            EvaluatedByName = "Readiness reviewer"
        });
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.ResolveFrameworkCallOffAsync(
            fixture.CallOff,
            fixture.Agreement,
            "framework-revoked-source");

        await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderSourceValidationException>()
            .Where(exception =>
                exception.Code ==
                "PO_FRAMEWORK_SOURCE_READINESS_SUPERSEDED");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            SupplierId = Guid.NewGuid();
            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .Options;
            Context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(Context);

            var requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RequisitionNumber = "PR-FRAME-EX-001",
                RequestedById = Guid.NewGuid(),
                Status = "Approved",
                ProcurementCategory = ProcurementCategoryClass.Goods,
                Currency = "GHS"
            };
            Release = new ProcurementRequisitionSourcingRelease
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseRequisitionId = requisition.Id,
                AttemptNumber = 1,
                ReleaseReference = "SRC-REL-FRAME-EX-001"
            };
            SourcingCase = new ProcurementSourcingCase
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseRequisitionId = requisition.Id,
                SourcingReleaseId = Release.Id,
                CaseSequence = 1,
                CaseNumber = "SC-FRAME-EX-001",
                Status = ProcurementSourcingCaseStatus.Ready
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "EX-FRAME-001",
                Title = "Exceptional framework source",
                Status = "Awarded",
                SourcePurchaseRequisitionId = requisition.Id,
                SourcingReleaseId = Release.Id,
                SourcingCaseId = SourcingCase.Id,
                Currency = "GHS"
            };
            var exceptionalControl =
                new ProcurementExceptionalSourcingControl
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    TenderId = Tender.Id,
                    SourcingCaseId = SourcingCase.Id,
                    Status =
                        ProcurementExceptionalSourcingControlStatus.Awarded
                };
            exceptionalControl.Id.Should().NotBe(Tender.Id);

            Readiness = new ProcurementAwardReadinessDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType =
                    ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                SourceId = Tender.Id,
                SourceReference = Tender.TenderNumber,
                DecisionSequence = 1,
                Status = ProcurementAwardReadinessDecisionStatus.Ready,
                RecommendedBusinessPartnerIdsJson =
                    JsonSerializer.Serialize(new[] { SupplierId }),
                SourceIntegrityHash = new string('a', 64),
                IntegrityHash = new string('b', 64),
                EvaluatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                EvaluatedByUserId = Guid.NewGuid(),
                EvaluatedByName = "Readiness reviewer"
            };
            Agreement = new ProcurementFrameworkAgreement
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AgreementKey = Guid.NewGuid(),
                AgreementNumber = "FA-EX-001",
                BusinessPartnerId = SupplierId,
                SourceType =
                    ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                SourceId = Tender.Id,
                SourceReference = Tender.TenderNumber,
                AwardReadinessDecisionId = Readiness.Id,
                SourceIntegrityHash = Readiness.IntegrityHash,
                CurrencyCode = "GHS",
                IntegrityHash = new string('c', 64)
            };
            CallOff = new ProcurementFrameworkCallOff
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CallOffNumber = "CO-EX-001",
                AgreementId = Agreement.Id,
                PurchaseOrderId = Guid.NewGuid(),
                SourceRequisitionId = requisition.Id,
                BusinessPartnerId = SupplierId,
                CurrencyCode = "GHS"
            };
            Context.AddRange(
                requisition,
                Release,
                SourcingCase,
                Tender,
                exceptionalControl,
                Readiness,
                Agreement,
                CallOff);
            Context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            Service = new ProcurementPurchaseOrderSourceService(
                _unitOfWork,
                current.Object,
                new Mock<IProcurementAccessControlService>().Object,
                new Mock<IProcurementControlEventService>().Object,
                new Mock<IProcurementRequisitionBudgetControlService>().Object,
                new Mock<INotificationTopicPublisher>().Object,
                NullLogger<ProcurementPurchaseOrderSourceService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid SupplierId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementRequisitionSourcingRelease Release { get; }
        public ProcurementSourcingCase SourcingCase { get; }
        public Tender Tender { get; }
        public ProcurementAwardReadinessDecision Readiness { get; }
        public ProcurementFrameworkAgreement Agreement { get; }
        public ProcurementFrameworkCallOff CallOff { get; }
        public ProcurementPurchaseOrderSourceService Service { get; }

        public ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
