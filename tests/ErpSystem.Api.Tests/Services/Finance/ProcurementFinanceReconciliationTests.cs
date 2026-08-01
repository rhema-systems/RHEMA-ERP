using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ErpSystem.Shared;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ProcurementFinanceReconciliationTests
{
    [Fact]
    [Trait("Batch", "TDC-0508")]
    public void ReportEndpoints_ShouldRequireExistingFinanceReportPermissions()
    {
        var read = typeof(ApReportsController).GetMethod(
            nameof(ApReportsController.GetProcurementFinanceReconciliation));
        var export = typeof(ApReportsController).GetMethod(
            nameof(ApReportsController.ExportProcurementFinanceReconciliation));

        read.Should().NotBeNull();
        export.Should().NotBeNull();
        read!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Policy.Should().Be(FinancePermissions.RunFinanceReports);
        export!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Policy.Should().Be(FinancePermissions.ExportFinanceReports);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldComposeExistingCommitmentReceiptApGlRetentionAndMilestones()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        report.RuleCode.Should().Be("AP-005");
        report.TaskCode.Should().Be("TDC-0508");
        report.DecisionKeys.Should().Equal(Enumerable.Range(1, 14).Select(index => $"DEC-{index:000}"));
        report.IsReconciled.Should().BeTrue();
        report.UnbalancedPostingCount.Should().Be(0);
        report.Rows.Should().ContainSingle();
        var row = report.Rows.Single();
        row.IsReconciled.Should().BeTrue();
        row.Issues.Should().BeEmpty();
        row.PurchaseOrderAmount.Should().Be(100m);
        row.CommitmentAmount.Should().Be(100m);
        row.AcceptedReceiptAmount.Should().Be(100m);
        row.InvoiceAmount.Should().Be(100m);
        row.SettledAmount.Should().Be(100m);
        row.InvoicePostedAmount.Should().Be(100m);
        row.PaymentPostedAmount.Should().Be(100m);
        row.RetentionHeldAmount.Should().Be(10m);
        row.RetentionReleasedAmount.Should().Be(0m);
        row.PaidMilestoneAmount.Should().Be(100m);
        report.CurrencySummaries.Should().ContainSingle(summary =>
            summary.CurrencyCode == "GHS" &&
            summary.PurchaseOrderAmount == 100m &&
            summary.CommitmentAmount == 100m &&
            summary.RetentionHeldAmount == 10m &&
            summary.MilestoneAmount == 100m);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldExplainUnbalancedAndUncontrolledReversalGaps()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        fixture.Commitment.ReservedAmount = 80m;
        fixture.Payment.Status = VendorPaymentStatus.Voided;
        fixture.PaymentPosting.TotalCreditAmount = 90m;
        fixture.Certificate.RetentionReleasedAmount = 15m;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        report.IsReconciled.Should().BeFalse();
        report.UnbalancedPostingCount.Should().Be(1);
        report.Rows.Single().Issues.Select(issue => issue.Code).Should().Contain(new[]
        {
            "COMMITMENT_ORDER_VARIANCE",
            "VOIDED_PAYMENT_REVERSAL_MISSING",
            "VOIDED_PAYMENT_ALLOCATION_REVERSAL_MISSING",
            "UNBALANCED_FINANCE_POSTING",
            "RETENTION_RELEASE_EXCEEDS_HELD"
        });
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldNotCountAcceptancePostedAfterCutoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        var receipt = fixture.PurchaseOrder.Receipts.Single();
        var receiptLine = receipt.Items.Single();
        var inspection = new ProcurementReceiptInspectionCase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderReceiptId = receipt.Id,
            Sequence = 1,
            Status = ProcurementReceiptInspectionStatus.Closed,
            ReceivedQuantity = 10m,
            AcceptedQuantity = 10m,
            PendingQuantity = 0m,
            StockEligibleQuantity = 10m,
            StockPostedQuantity = 10m,
            StockPostedAtUtc = new DateTime(2026, 9, 2),
            ApEligibleQuantity = 10m,
            ConfigurationProfileId = Guid.NewGuid(),
            PolicySetId = Guid.NewGuid(),
            AuthorityRuleId = Guid.NewGuid(),
            AuthorityName = "Receipt approval",
            WorkflowDefinitionId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedByName = "Inspector",
            IdempotencyKey = "inspection-after-cutoff",
            CorrelationId = "inspection-after-cutoff",
            SourceSnapshotHash = new string('a', 64),
            IntegrityHash = new string('b', 64)
        };
        inspection.Lines.Add(new ProcurementReceiptInspectionLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InspectionCaseId = inspection.Id,
            PurchaseOrderReceiptItemId = receiptLine.Id,
            ReceivedQuantity = 10m,
            AcceptedQuantity = 10m,
            PendingQuantity = 0m,
            Disposition = ProcurementReceiptDisposition.Accepted,
            IntegrityHash = new string('c', 64)
        });
        db.ProcurementReceiptInspectionCases.Add(inspection);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        report.Rows.Single().AcceptedReceiptAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldNotSettleAllocationUntilItsPostingExistsAtCutoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        fixture.PaymentPosting.PostingDate = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.SettledAmount.Should().Be(0m);
        row.PaymentPostedAmount.Should().Be(0m);
        row.PaymentCount.Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldKeepSettlementActiveBeforeLaterControlledReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "AP",
            SourceDocumentType = "VendorPayment",
            SourceDocumentId = fixture.Payment.Id,
            PostingAction = "Reverse",
            PostingStatus = "Posted",
            PostingDate = new DateTime(2026, 9, 2),
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS"
        });
        await db.SaveChangesAsync();
        fixture.Payment.Status = VendorPaymentStatus.Voided;
        fixture.Payment.UpdatedAt = new DateTime(2026, 9, 2);
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.SettledAmount.Should().Be(100m);
        row.PaymentPostedAmount.Should().Be(100m);
        row.PaymentCount.Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldApplyCutoffToMilestoneCreationAndLifecycleDates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        var milestone = await db.ContractMilestones.SingleAsync();
        milestone.CompletedAt = new DateTime(2026, 9, 2);
        milestone.InvoicedAt = new DateTime(2026, 9, 3);
        milestone.PaidAt = new DateTime(2026, 9, 4);
        milestone.Status = "Paid";
        var futureMilestone = new ContractMilestone
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = milestone.ContractId,
            MilestoneName = "Future milestone",
            PaymentAmount = 25m,
            PaymentPercentage = 25m,
            Status = "Pending"
        };
        db.ContractMilestones.Add(futureMilestone);
        await db.SaveChangesAsync();
        futureMilestone.CreatedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.MilestoneAmount.Should().Be(100m);
        row.CompletedMilestoneAmount.Should().Be(0m);
        row.InvoicedMilestoneAmount.Should().Be(0m);
        row.PaidMilestoneAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldReconstructCertificateBeforeLaterCancellationAndRelease()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        var before = new
        {
            fixture.Certificate.Id,
            fixture.Certificate.ContractId,
            Status = ProjectPaymentCertificateStatuses.Approved,
            fixture.Certificate.Currency,
            fixture.Certificate.RetentionHeldAmount,
            RetentionReleasedAmount = 0m,
            fixture.Certificate.IssueDate,
            fixture.Certificate.CreatedAt,
            UpdatedAt = (DateTime?)null,
            IsDeleted = false,
            DeletedAt = (DateTime?)null
        };
        var after = new
        {
            fixture.Certificate.Id,
            fixture.Certificate.ContractId,
            Status = ProjectPaymentCertificateStatuses.Cancelled,
            fixture.Certificate.Currency,
            fixture.Certificate.RetentionHeldAmount,
            RetentionReleasedAmount = 10m,
            fixture.Certificate.IssueDate,
            fixture.Certificate.CreatedAt,
            UpdatedAt = (DateTime?)new DateTime(2026, 9, 2),
            IsDeleted = false,
            DeletedAt = (DateTime?)null
        };
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            Username = "project.controller",
            Action = ProjectPaymentCertificateAuditEvents.Snapshot,
            Resource = ProjectPaymentCertificateAuditEvents.Resource,
            ResourceId = fixture.Certificate.Id.ToString(),
            OldValues = System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = System.Text.Json.JsonSerializer.Serialize(after),
            IpAddress = "127.0.0.1",
            Timestamp = new DateTime(2026, 9, 2)
        });
        db.ProcurementWorksCloseoutActions.Add(new ProcurementWorksCloseoutAction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = fixture.Certificate.ContractId!.Value,
            ProjectId = fixture.Certificate.ProjectId,
            Sequence = 1,
            ActionType = ProcurementWorksCloseoutActionType.RetentionRelease,
            Status = ProcurementWorksCloseoutActionStatus.Approved,
            ConfigurationProfileId = Guid.NewGuid(),
            PolicySetId = Guid.NewGuid(),
            AuthorityRuleId = Guid.NewGuid(),
            AuthorityName = "Retention authority",
            WorkflowDefinitionId = Guid.NewGuid(),
            ProjectPaymentCertificateId = fixture.Certificate.Id,
            EffectiveAtUtc = new DateTime(2026, 9, 2),
            Amount = 10m,
            Currency = "GHS",
            SubmittedById = Guid.NewGuid(),
            SubmittedByName = "Project Controller",
            SubmittedAtUtc = new DateTime(2026, 9, 1),
            DecidedById = Guid.NewGuid(),
            DecidedByName = "Finance Controller",
            DecidedAtUtc = new DateTime(2026, 9, 2),
            Reason = "Release after reporting cutoff",
            IdempotencyKey = "retention-after-cutoff",
            CorrelationId = "retention-after-cutoff",
            SourceSnapshotHash = new string('a', 64),
            IntegrityHash = new string('b', 64)
        });
        await db.SaveChangesAsync();
        fixture.Certificate.Status = ProjectPaymentCertificateStatuses.Cancelled;
        fixture.Certificate.RetentionReleasedAmount = 10m;
        fixture.Certificate.UpdatedAt = new DateTime(2026, 9, 2);
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.RetentionHeldAmount.Should().Be(10m);
        row.RetentionReleasedAmount.Should().Be(0m);
    }

    private static ApReportsService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("reconciliation.tester");
        currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());

        var settlement = new Mock<ISubledgerSettlementReadModelService>();
        settlement.Setup(item => item.GetControlReconciliationAsync(
                SubledgerSettlementModules.AccountsPayable,
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, DateTime? date, CancellationToken _) => new SubledgerControlReconciliationDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = date ?? DateTime.UtcNow,
                ReadModelOutstanding = 0m,
                PostedGlControlBalance = 0m,
                Variance = 0m
            });

        return new ApReportsService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<ITenantSettingsService>(),
            Mock.Of<ILogger<ApReportsService>>(),
            settlement.Object);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tdc0508-reconciliation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ScenarioFixture SeedBalancedScenario(ApplicationDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "TDC Test Tenant",
            Code = "TDC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });

        var requisitionId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "PO-0508-001",
            BusinessPartnerId = Guid.NewGuid(),
            OrderDate = new DateTime(2026, 7, 1),
            Status = "Approved",
            TotalAmount = 100m,
            Currency = "GHS",
            SourceRequisitionId = requisitionId,
            ContractId = contractId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var poLine = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = po.Id,
            ItemDescription = "Controlled item",
            OrderedQuantity = 10m,
            RemainingQuantity = 0m,
            UnitOfMeasure = "EA",
            UnitPrice = 10m,
            LineTotal = 100m
        };
        po.Items.Add(poLine);
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = po.Id,
            ReceiptNumber = "GR-0508-001",
            ReceiptDate = new DateTime(2026, 7, 2),
            InspectionDate = new DateTime(2026, 7, 2),
            Status = "Accepted"
        };
        receipt.Items.Add(new PurchaseOrderReceiptItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receipt.Id,
            PurchaseOrderItemId = poLine.Id,
            ReceivedQuantity = 10m,
            AcceptedQuantity = 10m,
            OrderedQuantitySnapshot = 10m,
            UnitOfMeasure = "EA"
        });
        po.Receipts.Add(receipt);
        db.PurchaseOrders.Add(po);

        var commitment = new ProcurementBudgetCommitment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProcurementBudgetId = Guid.NewGuid(),
            PurchaseRequisitionId = requisitionId,
            ReservationReference = "COM-0508-001",
            Status = ProcurementBudgetCommitmentStatus.Reserved,
            ReservedAmount = 100m,
            Currency = "GHS",
            ReservedAtUtc = new DateTime(2026, 7, 1),
            ReservedById = Guid.NewGuid(),
            ReservedByName = "Budget Controller",
            CorrelationId = "tdc0508-test"
        };
        db.ProcurementBudgetCommitments.Add(commitment);

        var invoiceJournal = Journal(tenantId, "JE-INV-0508", "VendorInvoice", 100m);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "VI-0508-001",
            SupplierId = Guid.NewGuid(),
            SupplierName = "TDC Supplier",
            PurchaseOrderId = po.Id,
            InvoiceDate = new DateTime(2026, 7, 3),
            TotalAmount = 100m,
            PaidAmount = 100m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 100m,
            Status = VendorInvoiceStatus.Paid,
            ApprovalStatus = "Approved",
            SubmittedDate = new DateTime(2026, 7, 3),
            ApprovedDate = new DateTime(2026, 7, 3),
            JournalEntryId = invoiceJournal.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var invoicePosting = Posting(tenantId, invoice.Id, "VendorInvoice", invoiceJournal.Id, 100m);
        db.JournalEntries.Add(invoiceJournal);
        db.FinancePostingEvents.Add(invoicePosting);
        db.VendorInvoices.Add(invoice);

        var paymentJournal = Journal(tenantId, "JE-PAY-0508", "VendorPayment", 100m);
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-0508-001",
            SupplierId = invoice.SupplierId,
            PaymentDate = new DateTime(2026, 7, 4),
            TotalAmount = 100m,
            AllocatedAmount = 100m,
            CurrencyCode = "GHS",
            Status = VendorPaymentStatus.Processed,
            JournalEntryId = paymentJournal.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = payment.Id,
            VendorInvoiceId = invoice.Id,
            AllocatedAmount = 100m,
            AllocationDate = payment.PaymentDate,
            ApplicationJournalEntryId = paymentJournal.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        payment.Allocations.Add(allocation);
        var paymentPosting = Posting(tenantId, payment.Id, "VendorPayment", paymentJournal.Id, 100m);
        db.JournalEntries.Add(paymentJournal);
        db.FinancePostingEvents.Add(paymentPosting);
        db.Set<VendorPayment>().Add(payment);

        db.Contracts.Add(new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            ContractNumber = "CON-0508-001",
            ContractTitle = "Controlled contract",
            Status = "Active",
            TenderAwardId = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = po.BusinessPartnerId,
            ContractValue = 100m,
            Currency = "GHS"
        });
        db.ContractMilestones.Add(new ContractMilestone
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = contractId,
            MilestoneName = "Delivery",
            PaymentAmount = 100m,
            PaymentPercentage = 100m,
            Status = "Paid",
            CompletedAt = new DateTime(2026, 7, 2),
            InvoicedAt = new DateTime(2026, 7, 3),
            PaidAt = new DateTime(2026, 7, 4),
            InvoiceNumber = invoice.InvoiceNumber
        });
        var certificate = new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = Guid.NewGuid(),
            ContractId = contractId,
            CertificateNumber = "CERT-0508-001",
            Title = "Delivery certificate",
            Status = ProjectPaymentCertificateStatuses.Approved,
            IssueDate = new DateTime(2026, 7, 3),
            GrossCertifiedAmount = 100m,
            RetentionHeldAmount = 10m,
            RetentionReleasedAmount = 0m,
            NetCertifiedAmount = 90m,
            Currency = "GHS"
        };
        db.ProjectPaymentCertificates.Add(certificate);

        return new ScenarioFixture(po, commitment, payment, paymentPosting, certificate);
    }

    private static JournalEntry Journal(
        Guid tenantId,
        string number,
        string sourceDocumentType,
        decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        JournalEntryNumber = number,
        JournalType = "System Generated",
        EntryDate = new DateTime(2026, 7, 4),
        Description = number,
        SourceModule = "AP",
        SourceDocumentType = sourceDocumentType,
        TotalDebitAmount = amount,
        TotalCreditAmount = amount,
        IsBalanced = true,
        PostingStatus = "Posted",
        PostingDate = new DateTime(2026, 7, 4),
        BookClassification = "IFRS"
    };

    private static FinancePostingEvent Posting(
        Guid tenantId,
        Guid sourceId,
        string sourceDocumentType,
        Guid journalId,
        decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SourceModule = "AP",
        SourceDocumentType = sourceDocumentType,
        SourceDocumentId = sourceId,
        PostingAction = "Post",
        JournalEntryId = journalId,
        PostingStatus = "Posted",
        PostingDate = new DateTime(2026, 7, 4),
        TotalDebitAmount = amount,
        TotalCreditAmount = amount,
        FunctionalCurrencyCode = "GHS",
        BookClassification = "IFRS"
    };

    private sealed record ScenarioFixture(
        PurchaseOrder PurchaseOrder,
        ProcurementBudgetCommitment Commitment,
        VendorPayment Payment,
        FinancePostingEvent PaymentPosting,
        ProjectPaymentCertificate Certificate);
}
