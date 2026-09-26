using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
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

public sealed class ProcurementInvoicePaymentSodServiceTests
{
    [Fact]
    public async Task DisabledProcurementSeparationStillProducesApprovalAuditAndMatchingQueueState()
    {
        await using var fixture = await Fixture.CreateAsync(sameActor: true, enforceSeparation: false);
        var readiness = await fixture.Service.EnforcePaymentApprovalAsync(fixture.PaymentId, "disabled-procurement-sod");
        readiness.CanApprove.Should().BeTrue();
        readiness.Code.Should().Be("SOD_DISABLED");
        readiness.ControlEventId.Should().Be(fixture.EventId);
        readiness.Invoices.Should().OnlyContain(item => !item.ConflictsWithCurrentActor);
        fixture.Events.Should().ContainSingle(item => item.Result == ProcurementControlEventResult.Allowed);
        var queue = await fixture.Service.GetQueueReadinessAsync(new[] { fixture.PaymentId }, Array.Empty<Guid>(), "disabled-procurement-queue");
        queue.Payments[fixture.PaymentId].CanApprove.Should().BeTrue();
        queue.Payments[fixture.PaymentId].Code.Should().Be("SOD_DISABLED");
    }

    [Fact]
    public async Task DisabledProcurementSeparationDoesNotPermitMissingProcessorLineage()
    {
        await using var fixture = await Fixture.CreateAsync(sameActor: true, processorMissing: true, enforceSeparation: false);
        var action = () => fixture.Service.EnforcePaymentApprovalAsync(fixture.PaymentId, "disabled-missing-lineage");
        var blocked = await action.Should().ThrowAsync<ProcurementInvoicePaymentSodBlockedException>();
        blocked.Which.Code.Should().Be(ProcurementInvoicePaymentSodRules.LineageCode);
    }

    [Fact]
    public async Task SameInvoiceProcessorCannotApproveManualPaymentAndDenialIsAudited()
    {
        await using var fixture = await Fixture.CreateAsync(sameActor: true);

        var action = () => fixture.Service.EnforcePaymentApprovalAsync(
            fixture.PaymentId, "tdc0506-manual-conflict");

        var blocked = await action.Should().ThrowAsync<ProcurementInvoicePaymentSodBlockedException>();
        blocked.Which.Code.Should().Be(ProcurementInvoicePaymentSodRules.ConflictCode);
        blocked.Which.Readiness.CanApprove.Should().BeFalse();
        blocked.Which.Readiness.Invoices.Should().ContainSingle(item => item.ConflictsWithCurrentActor);
        fixture.Events.Should().ContainSingle(item =>
            item.RuleCode == "AP-004" &&
            item.RuleVersion == "TDC-0506" &&
            item.Result == ProcurementControlEventResult.Denied &&
            item.DecisionKeys.Count == 14);
    }

    [Fact]
    public async Task IndependentActorCanApproveManualPaymentAndReceivesImmutableEventId()
    {
        await using var fixture = await Fixture.CreateAsync(sameActor: false);

        var readiness = await fixture.Service.EnforcePaymentApprovalAsync(
            fixture.PaymentId, "tdc0506-manual-independent");

        readiness.CanApprove.Should().BeTrue();
        readiness.ControlEventId.Should().Be(fixture.EventId);
        fixture.EnforcedRequests.Should().ContainSingle(item =>
            item.ControlCode == ProcurementInvoicePaymentSodRules.ControlCode &&
            item.SourceType == ProcurementInvoicePaymentSodRules.PaymentSourceType);
    }

    [Fact]
    public async Task MissingInvoiceProcessorLineageFailsClosedBeforeSharedGuard()
    {
        await using var fixture = await Fixture.CreateAsync(sameActor: false, processorMissing: true);

        var action = () => fixture.Service.EnforcePaymentApprovalAsync(
            fixture.PaymentId, "tdc0506-missing-lineage");

        var blocked = await action.Should().ThrowAsync<ProcurementInvoicePaymentSodBlockedException>();
        blocked.Which.Code.Should().Be(ProcurementInvoicePaymentSodRules.LineageCode);
        fixture.EnforcedRequests.Should().BeEmpty();
        fixture.Events.Should().ContainSingle(item => item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task ReversedOriginalInvoiceProcessorDoesNotParticipateInPaymentSod()
    {
        await using var fixture = await Fixture.CreateAsync(
            sameActor: true,
            reversedOriginalWithIndependentReplacement: true);

        var readiness = await fixture.Service.EnforcePaymentApprovalAsync(
            fixture.PaymentId, "tdc0506-reversed-original");

        readiness.CanApprove.Should().BeTrue();
        readiness.Invoices.Should().ContainSingle();
        readiness.Invoices.Single().ConflictsWithCurrentActor.Should().BeFalse();
        fixture.EnforcedRequests.Should().ContainSingle(item =>
            !item.ProhibitedActorUserIds.Contains(readiness.CurrentActorUserId));
    }

    [Fact]
    public async Task QueueReadinessUsesOneCoverageEvaluationForTheRequestedPage()
    {
        await using var fixture = await Fixture.CreateAsync(sameActor: false);

        var readiness = await fixture.Service.GetQueueReadinessAsync(
            new[] { fixture.PaymentId, fixture.PaymentId },
            Array.Empty<Guid>(),
            "tdc0506-paged-queue");

        readiness.Payments.Should().ContainSingle();
        readiness.Payments[fixture.PaymentId].CanApprove.Should().BeTrue();
        fixture.SodGuard.Verify(service => service.GetCoverageAsync(
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.SodGuard.Verify(service => service.CheckAsync(
            It.IsAny<ProcurementSodGuardRequest>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        private Fixture(
            ApplicationDbContext context,
            UnitOfWork unitOfWork,
            ProcurementInvoicePaymentSodService service,
            Guid paymentId,
            Guid eventId,
            Mock<IProcurementSodGuardService> sodGuard,
            List<ProcurementSodGuardRequest> enforcedRequests,
            List<ProcurementControlEventWriteRequest> events)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            Service = service;
            PaymentId = paymentId;
            EventId = eventId;
            SodGuard = sodGuard;
            EnforcedRequests = enforcedRequests;
            Events = events;
        }

        public ProcurementInvoicePaymentSodService Service { get; }
        public Guid PaymentId { get; }
        public Guid EventId { get; }
        public Mock<IProcurementSodGuardService> SodGuard { get; }
        public List<ProcurementSodGuardRequest> EnforcedRequests { get; }
        public List<ProcurementControlEventWriteRequest> Events { get; }

        public static async Task<Fixture> CreateAsync(
            bool sameActor,
            bool processorMissing = false,
            bool reversedOriginalWithIndependentReplacement = false,
            bool enforceSeparation = true)
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var processorId = processorMissing ? (Guid?)null : sameActor ? actorId : Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var context = new ApplicationDbContext(options);
            var unitOfWork = new UnitOfWork(context);

            var originalInvoice = new VendorInvoice
            {
                Id = invoiceId,
                TenantId = tenantId,
                InvoiceNumber = "INV-0506",
                SupplierId = Guid.NewGuid(),
                SupplierName = "TDC supplier",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 100m,
                CurrencyCode = "GHS",
                Status = VendorInvoiceStatus.Approved,
                SubmittedById = processorId,
                SubmittedDate = processorId.HasValue ? DateTime.UtcNow : null
            };
            context.VendorInvoices.Add(originalInvoice);
            context.Set<VendorPayment>().Add(new VendorPayment
            {
                Id = paymentId,
                TenantId = tenantId,
                PaymentNumber = "VP-0506",
                SupplierId = Guid.NewGuid(),
                TotalAmount = 100m,
                CurrencyCode = "GHS",
                Status = VendorPaymentStatus.PendingAuthorization
            });
            var originalAllocation = new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VendorPaymentId = paymentId,
                VendorInvoiceId = invoiceId,
                AllocatedAmount = 100m,
                AllocationDate = DateTime.UtcNow
            };
            context.Set<VendorPaymentAllocation>().Add(originalAllocation);
            if (reversedOriginalWithIndependentReplacement)
            {
                var replacementInvoice = new VendorInvoice
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InvoiceNumber = "INV-0506-REPLACEMENT",
                    SupplierId = Guid.NewGuid(),
                    SupplierName = "TDC supplier",
                    InvoiceDate = DateTime.UtcNow,
                    TotalAmount = 100m,
                    CurrencyCode = "GHS",
                    Status = VendorInvoiceStatus.Approved,
                    SubmittedById = Guid.NewGuid(),
                    SubmittedDate = DateTime.UtcNow
                };
                context.VendorInvoices.Add(replacementInvoice);
                context.Set<VendorPaymentAllocation>().AddRange(
                    new VendorPaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        VendorPaymentId = paymentId,
                        VendorInvoiceId = invoiceId,
                        AllocatedAmount = -100m,
                        AllocationDate = DateTime.UtcNow.AddSeconds(1),
                        IsReversal = true,
                        OriginalAllocationId = originalAllocation.Id
                    },
                    new VendorPaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        VendorPaymentId = paymentId,
                        VendorInvoiceId = replacementInvoice.Id,
                        AllocatedAmount = 100m,
                        AllocationDate = DateTime.UtcNow.AddSeconds(2)
                    });
            }
            await context.SaveChangesAsync();

            var current = new Mock<ICurrentUserService>();
            current.SetupGet(item => item.TenantId).Returns(tenantId);
            current.SetupGet(item => item.UserId).Returns(actorId.ToString());

            var enforcedRequests = new List<ProcurementSodGuardRequest>();
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.GetCoverageAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodCoverageDto
                {
                    EvaluatedAtUtc = DateTime.UtcNow,
                    CurrentActorUserId = actorId,
                    IsComplete = true,
                    PolicySetId = Guid.NewGuid(),
                    PolicyCode = "TDC-PROCUREMENT",
                    PolicyVersion = 1,
                    Controls =
                    [
                        new ProcurementSodRequiredControlDto
                        {
                            Code = ProcurementInvoicePaymentSodRules.ControlCode,
                            IsConfigured = true,
                            IsEffective = true,
                            IsHardStop = true,
                            RuleId = Guid.NewGuid(),
                            RuleCode = ProcurementInvoicePaymentSodRules.ControlCode
                        }
                    ]
                });
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementSodGuardRequest, string, CancellationToken>((request, _, _) => enforcedRequests.Add(request))
                .ReturnsAsync((ProcurementSodGuardRequest request, string _, CancellationToken _) =>
                {
                    var allowed = !request.ProhibitedActorUserIds.Contains(actorId);
                    return new ProcurementSodGuardDecisionDto
                    {
                        Allowed = allowed,
                        Code = allowed ? "SOD_ALLOWED" : "SOD_CONFLICT",
                        Message = allowed ? "Independent actor." : "Same-user conflict.",
                        ActorUserId = actorId,
                        ControlCode = request.ControlCode,
                        SourceType = request.SourceType,
                        SourceReference = request.SourceReference,
                        PolicySetId = Guid.NewGuid(),
                        PolicyCode = "TDC-PROCUREMENT",
                        PolicyVersion = 1,
                        RuleId = Guid.NewGuid(),
                        RuleCode = request.ControlCode,
                        EvaluatedAtUtc = DateTime.UtcNow
                    };
                });

            var events = new List<ProcurementControlEventWriteRequest>();
            var controlEvents = new Mock<IProcurementControlEventService>();
            controlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest, CancellationToken>((request, _) => events.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto { Id = eventId, TenantId = tenantId });

            var policy = new Mock<IProcurementSodPolicy>();
            policy.Setup(item => item.IsRequiredForSourceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(enforceSeparation);
            var service = new ProcurementInvoicePaymentSodService(
                unitOfWork,
                current.Object,
                sod.Object,
                controlEvents.Object,
                NullLogger<ProcurementInvoicePaymentSodService>.Instance, policy.Object);
            return new Fixture(context, unitOfWork, service, paymentId, eventId, sod, enforcedRequests, events);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
