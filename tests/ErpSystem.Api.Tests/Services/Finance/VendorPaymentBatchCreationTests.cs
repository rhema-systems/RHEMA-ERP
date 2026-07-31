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
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class VendorPaymentBatchCreationTests
{
    [Fact]
    public async Task CreatePaymentBatchAsync_AppendsReadinessBeforeAttachingCompleteAggregate()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = "SUP-TDC0505",
            Name = "TDC-0505 Supplier",
            SupplierType = "Vendor",
            Status = "Active",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "TDC0505-BATCH-INV",
            SupplierInvoiceNumber = "TDC0505-EXT",
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            SubTotal = 50m,
            TotalAmount = 50m,
            PaidAmount = 0m,
            CurrencyCode = "GHS",
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
        db.Suppliers.Add(supplier);
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
        workflow.Setup(item => item.StartApprovalWorkflowAsync("PaymentBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true });

        var invoiceService = new Mock<IVendorInvoiceService>();
        invoiceService.Setup(item => item.GetThreeWayMatchReadinessAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoice.Id,
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
        var service = new VendorPaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<VendorPaymentService>>(),
            numbering.Object,
            workflow.Object,
            vendorInvoiceService: invoiceService.Object,
            procurementControlEvents: controlEvents);

        var result = await service.CreatePaymentBatchAsync(new PaymentBatchCreateDto
        {
            Description = "TDC-0505 aggregate-order regression",
            BatchDate = DateTime.UtcNow.Date,
            InvoiceIds = new List<Guid> { invoice.Id }
        });

        result.Status.Should().Be(PaymentBatchStatus.PendingApproval);
        result.Items.Should().ContainSingle();
        result.Items.Single().Invoices.Should().ContainSingle();
        (await db.Set<PaymentBatch>().CountAsync()).Should().Be(1);
        (await db.Set<VendorPayment>().CountAsync()).Should().Be(1);
        var selection = await db.Set<PaymentBatchInvoice>().SingleAsync();
        selection.PaymentReadinessControlEventId.Should().NotBeEmpty();
        (await db.ProcurementControlEvents.AnyAsync(item =>
            item.Id == selection.PaymentReadinessControlEventId &&
            item.RuleCode == ProcurementPaymentReadinessRules.RuleCode)).Should().BeTrue();
        workflow.Verify(item => item.StartApprovalWorkflowAsync("PaymentBatch", result.Id), Times.Once);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tdc0505-batch-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
