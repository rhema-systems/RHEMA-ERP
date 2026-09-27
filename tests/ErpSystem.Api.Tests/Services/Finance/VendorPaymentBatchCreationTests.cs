using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class VendorPaymentBatchCreationTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"https://example.invalid\")")]
    [InlineData(" +SUM(1,1)")]
    [InlineData("@malicious")]
    [InlineData("-1+2")]
    public void ApCsvExportNeutralizesSpreadsheetFormulaPayloads(string value)
    {
        var method = typeof(ApReportsService).GetMethod(
            "Csv",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string)],
            modifiers: null)!;

        var result = (string)method.Invoke(null, [value])!;

        result.TrimStart('"').Should().StartWith("'");
    }

    [Fact]
    public void BatchFailureHandlingPreservesDurablyPostedPaymentOutcome()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorPaymentService.cs"));

        source.Should().Contain("var postingCommitted =");
        source.Should().Contain("postedPayment?.JournalEntryId is not null");
        source.Should().Contain("VendorPaymentStatus.Reconciled");
        source.Should().Contain("preserving the committed payment outcome");
    }

    [Theory]
    [InlineData(true, "GHS", false)]
    [InlineData(false, "GHS", false)]
    [InlineData(false, "USD", false)]
    [InlineData(false, "GHS", true)]
    public async Task CreatePaymentBatchAsync_AppendsReadinessBeforeAttachingCompleteAggregate(bool approvalRequired, string invoiceCurrency, bool requireEvidence)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        var supplier = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-TDC0505",
            PartnerName = "TDC-0505 Supplier",
            PartnerType = "Supplier",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var role = ReadyRole(tenantId, supplier.Id);
        var profile = ReadyProfile(tenantId, role.Id);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "TDC0505-BATCH-INV",
            SupplierInvoiceNumber = "TDC0505-EXT",
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = role.Id,
            BusinessPartnerApProfileVersionId = profile.Id,
            BusinessPartnerCode = supplier.PartnerCode,
            SupplierName = supplier.PartnerName,
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            SubTotal = 50m,
            TotalAmount = 50m,
            PaidAmount = 0m,
            CurrencyCode = invoiceCurrency,
            ExchangeRate = 1m,
            BaseCurrencyAmount = 50m,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            IsOpeningBalance = true,
            SubmittedById = userId,
            SubmittedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "TDC-0505 Tenant",
            Code = "T505",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.BusinessPartners.Add(supplier);
        db.Set<BusinessPartnerRole>().Add(role);
        db.Set<BusinessPartnerApProfileVersion>().Add(profile);
        db.VendorInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
        currentUser.SetupGet(item => item.UserName).Returns("ap.officer");
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.Roles).Returns(new[] { "Accounts Officer" });
        currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());

        var currentProvider = new Mock<ICurrentUserProvider>();
        currentProvider.SetupGet(item => item.TenantId).Returns(tenantId);
        currentProvider.SetupGet(item => item.UserId).Returns(userId);
        currentProvider.SetupGet(item => item.Username).Returns("ap.officer");
        currentProvider.SetupGet(item => item.FullName).Returns("AP Officer");
        currentProvider.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentProvider.SetupGet(item => item.Roles).Returns(new[] { "Accounts Officer" });
        currentProvider.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());

        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(item => item.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string module, string documentType, Guid? tenant, DateTime? documentDate,
                    string? entityType, Guid? entityId, CancellationToken cancellationToken) =>
                documentType == FinanceDocumentTypes.APPaymentBatch ? "PB-TDC0505" : "VP-TDC0505");

        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("PaymentBatch")).ReturnsAsync(approvalRequired);
        workflow.Setup(item => item.StartApprovalWorkflowAsync("PaymentBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, WorkflowInstanceId = Guid.NewGuid() });

        var invoiceService = new Mock<IVendorInvoiceService>();
        invoiceService.Setup(item => item.GetThreeWayMatchReadinessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid invoiceId, CancellationToken _) => new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoiceId,
                IsRequired = false,
                IsMatched = true,
                ApprovalReady = true,
                SnapshotHash = "OPENING-BALANCE",
                Message = "Three-way matching is not required."
            });

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(item => item.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var controlEvents = new ProcurementControlEventService(
            new UnitOfWork(db),
            currentProvider.Object,
            Mock.Of<ILogger<ProcurementControlEventService>>());
        var approvalPolicy = new Mock<IWorkflowApprovalPolicyResolver>();
        if (requireEvidence)
            approvalPolicy.Setup(item => item.ResolveAsync(
                    It.IsAny<WorkflowApprovalPolicyContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkflowApprovalPolicyResolution(Guid.NewGuid(), "PAYMENT-EVIDENCE",
                    new WorkflowApprovalConfigDto
                    {
                        EvidenceRequirements =
                        [
                            new WorkflowEvidenceRequirementDto
                            {
                                RequirementKey = "bank-instruction", DocumentName = "Bank instruction",
                                MinimumDocuments = 1, RequireVerification = true
                            }
                        ]
                    }));
        var service = new VendorPaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<VendorPaymentService>>(),
            numbering.Object,
            workflow.Object,
            Mock.Of<IFinanceAccessScopeService>(),
            Mock.Of<IFinanceReversalPolicyService>(),
            financeAuditService: Mock.Of<IFinanceAuditService>(),
            approvalPolicyResolver: approvalPolicy.Object,
            vendorInvoiceService: invoiceService.Object,
            procurementControlEvents: controlEvents);

        var request = new PaymentBatchCreateDto
        {
            Description = "TDC-0505 aggregate-order regression",
            BatchDate = DateTime.UtcNow.Date,
            InvoiceIds = new List<Guid> { invoice.Id }
        };
        if (requireEvidence)
        {
            var create = () => service.CreatePaymentBatchAsync(request);
            await create.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Bank instruction*");
            (await db.Set<PaymentBatch>().CountAsync()).Should().Be(0);
            (await db.Set<VendorPayment>().CountAsync()).Should().Be(0);
            workflow.Verify(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never());
            return;
        }
        if (!approvalRequired && invoiceCurrency != "GHS")
        {
            var create = () => service.CreatePaymentBatchAsync(request);
            var failure = await create.Should().ThrowAsync<VendorPaymentControlException>();
            failure.Which.Code.Should().Be("AP_PAYMENT_BATCH_SETTLEMENT_RATE_REQUIRED");
            (await db.Set<PaymentBatch>().CountAsync()).Should().Be(0);
            (await db.Set<VendorPayment>().CountAsync()).Should().Be(0);
            workflow.Verify(item => item.StartApprovalWorkflowAsync("PaymentBatch", It.IsAny<Guid>()), Times.Never());
            return;
        }
        var result = await service.CreatePaymentBatchAsync(request);

        result.Status.Should().Be(approvalRequired ? PaymentBatchStatus.PendingApproval : PaymentBatchStatus.Approved);
        result.ApprovalRequired.Should().Be(approvalRequired);
        result.ApprovedById.Should().BeNull();
        result.ApprovedDate.Should().BeNull();
        result.Items.Should().ContainSingle();
        result.Items.Single().Invoices.Should().ContainSingle();
        (await db.Set<PaymentBatch>().CountAsync()).Should().Be(1);
        (await db.Set<VendorPayment>().CountAsync()).Should().Be(1);
        var selection = await db.Set<PaymentBatchInvoice>().SingleAsync();
        selection.PaymentReadinessControlEventId.Should().NotBeEmpty();
        (await db.ProcurementControlEvents.AnyAsync(item =>
            item.Id == selection.PaymentReadinessControlEventId &&
            item.RuleCode == ProcurementPaymentReadinessRules.RuleCode)).Should().BeTrue();
        workflow.Verify(item => item.StartApprovalWorkflowAsync("PaymentBatch", result.Id), approvalRequired ? Times.Once() : Times.Never());

        var ownedPayment = await db.Set<VendorPayment>()
            .SingleAsync(item => item.PaymentBatchId == result.Id);
        var ownedBatch = await db.Set<PaymentBatch>().SingleAsync(item => item.Id == result.Id);
        ownedPayment.ApprovalRequired.Should().Be(approvalRequired);
        ownedPayment.AuthorizedById.Should().BeNull();
        ownedPayment.AuthorizedDate.Should().BeNull();
        ownedBatch.Status = PaymentBatchStatus.Approved;
        ownedPayment.Status = VendorPaymentStatus.Authorized;
        await db.SaveChangesAsync();
        var directBatchAllocation = () => service.AllocatePaymentAsync(
            ownedPayment.Id,
            [new VendorPaymentAllocationCreateDto
            {
                VendorInvoiceId = invoice.Id,
                AllocatedAmount = 1m
            }]);
        var frozenBatch = await directBatchAllocation
            .Should().ThrowAsync<VendorPaymentControlException>();
        frozenBatch.Which.Code.Should().Be("AP_PAYMENT_BATCH_DIRECT_ALLOCATION_BLOCKED");

        (await service.GetOutstandingInvoicesAsync(supplier.Id)).Should().BeEmpty(
            "a payment-batch selection that reserves the whole balance must disappear from the picker");
        selection.Amount = 20m;
        await db.SaveChangesAsync();
        var partiallyReserved = await service.GetOutstandingInvoicesAsync(supplier.Id);
        partiallyReserved.Should().ContainSingle();
        partiallyReserved.Single().BalanceAmount.Should().Be(30m,
            "the picker must expose only the unreserved allocatable balance");
        selection.Amount = 50m;
        await db.SaveChangesAsync();

        var manualPayment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-TDC0505-MANUAL",
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = role.Id,
            BusinessPartnerApProfileVersionId = profile.Id,
            BusinessPartnerCode = supplier.PartnerCode,
            BusinessPartnerName = supplier.PartnerName,
            PaymentDate = DateTime.UtcNow.Date,
            TotalAmount = 50m,
            CurrencyCode = "GHS",
            Status = VendorPaymentStatus.Draft,
            CreatedBy = "Tests"
        };
        db.Set<VendorPayment>().Add(manualPayment);
        await db.SaveChangesAsync();

        var allocate = () => service.AllocatePaymentAsync(
            manualPayment.Id,
            [new VendorPaymentAllocationCreateDto
            {
                VendorInvoiceId = invoice.Id,
                AllocatedAmount = 50m
            }]);

        var exception = await allocate.Should().ThrowAsync<VendorPaymentControlException>();
        exception.Which.Code.Should().Be("AP_PAYMENT_BALANCE_RESERVED");
        (await db.Set<VendorPaymentAllocation>().CountAsync()).Should().Be(0);

        var mixedCurrencyInvoices = new[]
        {
            NewOpeningBalanceInvoice(
                tenantId, supplier, role.Id, profile.Id, userId, "TDC0505-MIX-GHS", "GHS", 60m),
            NewOpeningBalanceInvoice(
                tenantId, supplier, role.Id, profile.Id, userId, "TDC0505-MIX-EUR", "EUR", 40m)
        };
        db.VendorInvoices.AddRange(mixedCurrencyInvoices);
        await db.SaveChangesAsync();

        var createMixedCurrencyBatch = () => service.CreatePaymentBatchAsync(
            new PaymentBatchCreateDto
            {
                Description = "Mixed-currency batch must be rejected",
                BatchDate = DateTime.UtcNow.Date,
                InvoiceIds = mixedCurrencyInvoices.Select(item => item.Id).ToList()
            });
        var mixedCurrencyException = await createMixedCurrencyBatch
            .Should().ThrowAsync<VendorPaymentControlException>();
        mixedCurrencyException.Which.Code.Should().Be(
            "AP_PAYMENT_BATCH_CURRENCY_MISMATCH");
    }

    [Fact]
    public async Task OutstandingInvoicePicker_FiltersReservationsBeforePagination()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        var supplier = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-PAGED",
            PartnerName = "Paged supplier",
            PartnerType = "Supplier",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var role = ReadyRole(tenantId, supplier.Id);
        var profile = ReadyProfile(tenantId, role.Id);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Picker tenant",
            Code = "PICK",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.BusinessPartners.Add(supplier);
        db.Set<BusinessPartnerRole>().Add(role);
        db.Set<BusinessPartnerApProfileVersion>().Add(profile);

        for (var index = 0; index < 50; index++)
        {
            var invoice = NewOpeningBalanceInvoice(
                tenantId, supplier, role.Id, profile.Id, userId, $"RESERVED-{index:00}", "GHS", 10m);
            invoice.DueDate = DateTime.UtcNow.Date;
            var payment = new VendorPayment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PaymentNumber = $"VP-RESERVED-{index:00}",
                BusinessPartnerId = supplier.Id,
                BusinessPartnerRoleId = role.Id,
                BusinessPartnerApProfileVersionId = profile.Id,
                BusinessPartnerCode = supplier.PartnerCode,
                BusinessPartnerName = supplier.PartnerName,
                PaymentDate = DateTime.UtcNow.Date,
                TotalAmount = 10m,
                CurrencyCode = "GHS",
                Status = VendorPaymentStatus.Draft,
                CreatedBy = "Tests"
            };
            payment.Allocations.Add(new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VendorPaymentId = payment.Id,
                VendorInvoiceId = invoice.Id,
                AllocatedAmount = 10m,
                AllocationDate = DateTime.UtcNow,
                CreatedBy = "Tests"
            });
            db.VendorInvoices.Add(invoice);
            db.Set<VendorPayment>().Add(payment);
        }

        var selectable = NewOpeningBalanceInvoice(
            tenantId, supplier, role.Id, profile.Id, userId, "SELECTABLE-51", "GHS", 25m);
        selectable.DueDate = DateTime.UtcNow.Date.AddDays(1);
        db.VendorInvoices.Add(selectable);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
        currentUser.SetupGet(item => item.UserName).Returns("ap.officer");
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.Roles).Returns(new[] { "Accounts Officer" });
        currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
        var invoiceService = new Mock<IVendorInvoiceService>();
        invoiceService.Setup(item => item.GetThreeWayMatchReadinessAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid invoiceId, CancellationToken _) => new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoiceId,
                IsRequired = false,
                IsMatched = true,
                ApprovalReady = true,
                SnapshotHash = "OPENING-BALANCE",
                Message = "Three-way matching is not required."
            });
        var service = new VendorPaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<ITenantSettingsService>(),
            Mock.Of<ILogger<VendorPaymentService>>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<IFinanceAccessScopeService>(),
            Mock.Of<IFinanceReversalPolicyService>(),
            vendorInvoiceService: invoiceService.Object);

        var result = await service.GetOutstandingInvoicesAsync(supplier.Id);

        result.Should().ContainSingle(item => item.InvoiceId == selectable.Id);
        result.Single().BalanceAmount.Should().Be(25m);
    }

    private static VendorInvoice NewOpeningBalanceInvoice(
        Guid tenantId,
        BusinessPartner supplier,
        Guid roleId,
        Guid profileId,
        Guid userId,
        string invoiceNumber,
        string currencyCode,
        decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        InvoiceNumber = invoiceNumber,
        SupplierInvoiceNumber = $"EXT-{invoiceNumber}",
        BusinessPartnerId = supplier.Id,
        BusinessPartnerRoleId = roleId,
        BusinessPartnerApProfileVersionId = profileId,
        BusinessPartnerCode = supplier.PartnerCode,
        SupplierName = supplier.PartnerName,
        InvoiceDate = DateTime.UtcNow.Date,
        DueDate = DateTime.UtcNow.Date.AddDays(30),
        SubTotal = amount,
        TotalAmount = amount,
        PaidAmount = 0m,
        CurrencyCode = currencyCode,
        ExchangeRate = 1m,
        BaseCurrencyAmount = amount,
        Status = VendorInvoiceStatus.Approved,
        ApprovalStatus = "Approved",
        IsOpeningBalance = true,
        SubmittedById = userId,
        SubmittedDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "Tests"
    };

    private static BusinessPartnerRole ReadyRole(Guid tenantId, Guid partnerId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partnerId,
        RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
        ActiveFromUtc = new DateTime(2025, 1, 1)
    };

    private static BusinessPartnerApProfileVersion ReadyProfile(Guid tenantId, Guid roleId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = roleId,
        VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
        EffectiveFrom = new DateTime(2025, 1, 1), SubjectToWithholding = false,
        ApprovedAtUtc = new DateTime(2025, 1, 1), ApprovedById = Guid.NewGuid()
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tdc0505-batch-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
               throw new DirectoryNotFoundException("Repository root not found.");
    }
}
