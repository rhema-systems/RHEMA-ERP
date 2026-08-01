using System.Security.Claims;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class VendorInvoiceMatchExceptionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewExposesRequestCapabilityOnlyToApInvoiceManagers(bool canManage)
    {
        await using var fixture = new Fixture(canManage);
        var invoice = fixture.NewInvoice();
        fixture.Context.VendorInvoices.Add(invoice);
        await fixture.Context.SaveChangesAsync();
        fixture.InvoiceService
            .Setup(service => service.GetThreeWayMatchReadinessAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExceptionableReadiness(invoice.Id));

        var overview = await fixture.Service.GetOverviewAsync(invoice.Id);

        overview.CanRequest.Should().Be(canManage);
        fixture.Authorization.Verify(service => service.AuthorizeAsync(
            It.IsAny<ClaimsPrincipal>(),
            null,
            FinancePermissions.ManageApInvoices), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewExposesDecisionCapabilityOnlyToApInvoiceApprovers(bool canApprove)
    {
        await using var fixture = new Fixture(canManage: false, canApprove);
        var invoice = fixture.NewInvoice();
        invoice.SubmittedById = Guid.NewGuid();
        var purchaseOrder = fixture.NewPurchaseOrder();
        invoice.PurchaseOrderId = purchaseOrder.Id;
        var exception = fixture.NewException(invoice, purchaseOrder, "decision-capability");
        exception.Status = VendorInvoiceMatchExceptionStatus.PendingApproval;
        exception.RequestedById = Guid.NewGuid();
        exception.WorkflowInstanceId = Guid.NewGuid();
        fixture.Context.AddRange(purchaseOrder, invoice, exception);
        await fixture.Context.SaveChangesAsync();
        fixture.InvoiceService
            .Setup(service => service.GetThreeWayMatchReadinessAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExceptionableReadiness(invoice.Id));
        fixture.Workflow
            .Setup(service => service.CanUserApproveAsync(
                VendorInvoiceMatchExceptionRules.WorkflowEntityType,
                exception.Id,
                fixture.UserId))
            .ReturnsAsync(true);

        var overview = await fixture.Service.GetOverviewAsync(invoice.Id);

        overview.CanDecide.Should().Be(canApprove);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewExposesCancellationCapabilityOnlyToApInvoiceManagers(bool canManage)
    {
        await using var fixture = new Fixture(canManage, canApprove: false);
        var invoice = fixture.NewInvoice();
        var purchaseOrder = fixture.NewPurchaseOrder();
        invoice.PurchaseOrderId = purchaseOrder.Id;
        var exception = fixture.NewException(invoice, purchaseOrder, "cancel-capability");
        exception.Status = VendorInvoiceMatchExceptionStatus.PendingApproval;
        fixture.Context.AddRange(purchaseOrder, invoice, exception);
        await fixture.Context.SaveChangesAsync();
        fixture.InvoiceService
            .Setup(service => service.GetThreeWayMatchReadinessAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExceptionableReadiness(invoice.Id));

        var overview = await fixture.Service.GetOverviewAsync(invoice.Id);

        overview.CanCancel.Should().Be(canManage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewExposesCorrectiveCompletionOnlyToApInvoiceManagers(bool canManage)
    {
        await using var fixture = new Fixture(canManage, canApprove: false);
        var invoice = fixture.NewInvoice();
        var purchaseOrder = fixture.NewPurchaseOrder();
        invoice.PurchaseOrderId = purchaseOrder.Id;
        var exception = fixture.NewException(invoice, purchaseOrder, "corrective-capability");
        exception.Status = VendorInvoiceMatchExceptionStatus.Approved;
        exception.CorrectiveActionStatus = VendorInvoiceMatchCorrectiveActionStatus.Planned;
        exception.CorrectiveActionOwnerId = fixture.UserId;
        fixture.Context.AddRange(purchaseOrder, invoice, exception);
        await fixture.Context.SaveChangesAsync();
        fixture.InvoiceService
            .Setup(service => service.GetThreeWayMatchReadinessAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExceptionableReadiness(invoice.Id));

        var overview = await fixture.Service.GetOverviewAsync(invoice.Id);

        overview.CanCompleteCorrectiveAction.Should().Be(canManage);
    }

    [Fact]
    public async Task IdempotencyReplayForSameInvoiceReturnsTheExistingException()
    {
        await using var fixture = new Fixture(canManage: true);
        var invoice = fixture.NewInvoice();
        var purchaseOrder = fixture.NewPurchaseOrder();
        invoice.PurchaseOrderId = purchaseOrder.Id;
        var request = ValidRequest("replay-key");
        var existing = fixture.NewException(invoice, purchaseOrder, request);
        fixture.Context.AddRange(purchaseOrder, invoice, existing);
        await fixture.Context.SaveChangesAsync();

        var replay = await fixture.Service.RequestAsync(
            invoice.Id,
            request,
            "correlation-replay");

        replay.Id.Should().Be(existing.Id);
        replay.VendorInvoiceId.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task IdempotencyReplayForSameInvoiceRejectsChangedRequestPayload()
    {
        await using var fixture = new Fixture(canManage: true);
        var invoice = fixture.NewInvoice();
        var purchaseOrder = fixture.NewPurchaseOrder();
        invoice.PurchaseOrderId = purchaseOrder.Id;
        var original = ValidRequest("same-invoice-changed-payload");
        fixture.Context.AddRange(
            purchaseOrder,
            invoice,
            fixture.NewException(invoice, purchaseOrder, original));
        await fixture.Context.SaveChangesAsync();
        var replay = ValidRequest(original.IdempotencyKey);
        replay.CorrectiveActionOwnerId = original.CorrectiveActionOwnerId;
        replay.CorrectiveActionDueAtUtc = original.CorrectiveActionDueAtUtc;
        replay.ExpiresAtUtc = original.ExpiresAtUtc;
        replay.Justification = "Changed justification that was never submitted.";

        var action = () => fixture.Service.RequestAsync(
            invoice.Id,
            replay,
            "correlation-changed-payload");

        var exception = await action.Should()
            .ThrowAsync<VendorInvoiceMatchExceptionControlException>();
        exception.Which.Code.Should().Be(
            "AP_MATCH_EXCEPTION_IDEMPOTENCY_PAYLOAD_MISMATCH");
        exception.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task IdempotencyKeyBoundToAnotherInvoiceIsRejected()
    {
        await using var fixture = new Fixture(canManage: true);
        var boundInvoice = fixture.NewInvoice();
        var purchaseOrder = fixture.NewPurchaseOrder();
        boundInvoice.PurchaseOrderId = purchaseOrder.Id;
        fixture.Context.AddRange(
            purchaseOrder,
            boundInvoice,
            fixture.NewException(boundInvoice, purchaseOrder, "tenant-wide-key"));
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.RequestAsync(
            Guid.NewGuid(),
            ValidRequest("tenant-wide-key"),
            "correlation-mismatch");

        var exception = await action.Should().ThrowAsync<VendorInvoiceMatchExceptionControlException>();
        exception.Which.Code.Should().Be("AP_MATCH_EXCEPTION_IDEMPOTENCY_MISMATCH");
        exception.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task InitialRequestRejectsReservedCorrectiveCompletionEvidence()
    {
        await using var fixture = new Fixture(canManage: true);
        var request = ValidRequest("reserved-completion-evidence");
        request.Evidence.Add(new VendorInvoiceMatchExceptionEvidenceRequestDto
        {
            RequirementKey = VendorInvoiceMatchExceptionRules.CorrectiveCompletionEvidenceKey,
            ReferenceKind = VendorInvoiceMatchExceptionEvidenceKind.CentralDocument,
            FileUploadRecordId = Guid.NewGuid(),
            EvidenceReference = "Completion evidence submitted before approval"
        });

        var action = () => fixture.Service.RequestAsync(
            Guid.NewGuid(),
            request,
            "reserved-completion-correlation");

        var exception = await action.Should()
            .ThrowAsync<VendorInvoiceMatchExceptionControlException>();
        exception.Which.Code.Should().Be(
            "AP_MATCH_EXCEPTION_COMPLETION_EVIDENCE_RESERVED");
        exception.Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    private static InvoiceMatchingResultDto ExceptionableReadiness(Guid invoiceId) => new()
    {
        VendorInvoiceId = invoiceId,
        IsRequired = true,
        IsMatched = false,
        Discrepancies =
        [
            new MatchingDiscrepancyDto
            {
                DiscrepancyType = "Price",
                ItemDescription = "Test line",
                ExceptionEligible = true,
                Variance = 5m
            }
        ],
        Checks =
        [
            new InvoiceMatchingCheckDto
            {
                CheckKey = "price-tolerance",
                Passed = false,
                ExceptionEligible = true
            }
        ]
    };

    private static CreateVendorInvoiceMatchExceptionDto ValidRequest(string idempotencyKey) => new()
    {
        RootCauseCategory = "Supplier pricing",
        RootCauseDescription = "Supplier price differs from the purchase order.",
        Justification = "Controlled exception replay test.",
        CorrectiveAction = "Reconcile the supplier price.",
        CorrectiveActionOwnerId = Guid.NewGuid(),
        CorrectiveActionDueAtUtc = DateTime.UtcNow.AddDays(2),
        ExpiresAtUtc = DateTime.UtcNow.AddDays(5),
        IdempotencyKey = idempotencyKey
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(bool canManage, bool canApprove = false)
        {
            Context = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"tdc0507-service-{Guid.NewGuid():N}")
                    // ApplicationDbContext's tenant filter captures the constructor
                    // tenant in its model. A fresh provider prevents a model cached
                    // by a parallel fixture from applying another tenant's filter.
                    .EnableServiceProviderCaching(false)
                    .Options,
                TenantId);

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(service => service.UserId).Returns(UserId.ToString());
            currentUser.SetupGet(service => service.UserName).Returns("AP test user");
            currentUser.SetupGet(service => service.TenantId).Returns(TenantId);
            currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
            currentUser.Setup(service => service.IsInRole(It.IsAny<string>())).Returns(false);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Name, "AP test user"),
                new Claim("tenant_id", TenantId.ToString())
            ], "test"));
            var httpContextAccessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            Authorization.Setup(service => service.AuthorizeAsync(
                    principal,
                    null,
                    FinancePermissions.ManageApInvoices))
                .ReturnsAsync(canManage ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            Authorization.Setup(service => service.AuthorizeAsync(
                    principal,
                    null,
                    FinancePermissions.ApproveApInvoices))
                .ReturnsAsync(canApprove ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            InvoiceService = new Mock<IVendorInvoiceService>();
            Workflow = new Mock<IWorkflowService>();

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.SetupGet(service => service.HasActiveTransaction).Returns(true);
            unitOfWork.Setup(service => service.AcquireTransactionLockAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Service = new VendorInvoiceMatchExceptionService(
                Context,
                unitOfWork.Object,
                currentUser.Object,
                InvoiceService.Object,
                Workflow.Object,
                Mock.Of<IProcurementConfigurationService>(),
                Mock.Of<IProcurementControlEventService>(),
                Mock.Of<INotificationTopicPublisher>(),
                Authorization.Object,
                httpContextAccessor,
                NullLogger<VendorInvoiceMatchExceptionService>.Instance);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public Mock<IVendorInvoiceService> InvoiceService { get; }
        public Mock<IWorkflowService> Workflow { get; }
        public Mock<IAuthorizationService> Authorization { get; } = new();
        public VendorInvoiceMatchExceptionService Service { get; }

        public VendorInvoice NewInvoice() => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            SupplierId = Guid.NewGuid(),
            SupplierName = "Test supplier",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        public PurchaseOrder NewPurchaseOrder() => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            OrderNumber = $"PO-{Guid.NewGuid():N}",
            BusinessPartnerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        public VendorInvoiceMatchException NewException(
            VendorInvoice invoice,
            PurchaseOrder purchaseOrder,
            string idempotencyKey) =>
            NewException(invoice, purchaseOrder, ValidRequest(idempotencyKey));

        public VendorInvoiceMatchException NewException(
            VendorInvoice invoice,
            PurchaseOrder purchaseOrder,
            CreateVendorInvoiceMatchExceptionDto request) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            VendorInvoiceId = invoice.Id,
            VendorInvoice = invoice,
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            Sequence = 1,
            Status = VendorInvoiceMatchExceptionStatus.Rejected,
            VarianceType = "Price",
            RootCauseCategory = request.RootCauseCategory,
            RootCauseDescription = request.RootCauseDescription,
            Justification = request.Justification,
            CorrectiveAction = request.CorrectiveAction,
            CorrectiveActionOwnerId = request.CorrectiveActionOwnerId,
            CorrectiveActionOwnerName = "AP test user",
            CorrectiveActionDueAtUtc = request.CorrectiveActionDueAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            InvoiceSnapshotHash = new string('a', 64),
            RequestedById = UserId,
            RequestedByName = "AP test user",
            RequestedAtUtc = DateTime.UtcNow,
            IdempotencyKey = request.IdempotencyKey,
            CorrelationId = "existing-correlation",
            IntegrityHash = new string('b', 64),
            CreatedAt = DateTime.UtcNow
        };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
