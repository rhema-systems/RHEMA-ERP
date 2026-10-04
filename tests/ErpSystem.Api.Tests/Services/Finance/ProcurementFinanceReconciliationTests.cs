using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using ErpSystem.Shared;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ProcurementFinanceReconciliationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConsolidatedInvoiceAndPaymentAreAttributedOnceAcrossTheirPurchaseOrders(bool filterFirstOrder)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenant);
        var invoice = db.ChangeTracker.Entries<VendorInvoice>().Single().Entity;
        invoice.PurchaseOrderId = null;
        invoice.AutoInvoiceRequestId = Guid.NewGuid();
        invoice.AutoInvoiceRequestHash = new string('a', 64);
        var other = new PurchaseOrder { TenantId = tenant, OrderNumber = "PO-CONSOLIDATED", BusinessPartnerId = fixture.PurchaseOrder.BusinessPartnerId,
            OrderDate = new DateTime(2026, 7, 1), CreatedAt = new DateTime(2026, 7, 1), Status = "Approved", ApprovedAt = new DateTime(2026, 7, 1), TotalAmount = 40m, Currency = "GHS" };
        var sourceLine = fixture.PurchaseOrder.Items.Single();
        var otherItem = new PurchaseOrderItem
        {
            TenantId = tenant,
            PurchaseOrderId = other.Id,
            ItemDescription = "Second PO",
            OrderedQuantity = 4,
            UnitOfMeasure = "EA",
            UnitOfMeasureId = sourceLine.UnitOfMeasureId,
            UnitOfMeasureCodeSnapshot = sourceLine.UnitOfMeasureCodeSnapshot,
            UnitOfMeasureDecimalPlacesSnapshot = sourceLine.UnitOfMeasureDecimalPlacesSnapshot,
            UnitOfMeasureRoundingIncrementSnapshot = sourceLine.UnitOfMeasureRoundingIncrementSnapshot,
            UnitPrice = 10
        };
        other.Items.Add(otherItem); db.PurchaseOrders.Add(other);
        invoice.LineItems.Add(new VendorInvoiceLineItem { TenantId = tenant, VendorInvoiceId = invoice.Id,
            PurchaseOrderItemId = fixture.PurchaseOrder.Items.Single().Id, Quantity = 6, UnitPrice = 10 });
        invoice.LineItems.Add(new VendorInvoiceLineItem { TenantId = tenant, VendorInvoiceId = invoice.Id,
            PurchaseOrderItemId = otherItem.Id, Quantity = 4, UnitPrice = 10 });
        db.Set<VendorInvoiceLineItem>().AddRange(invoice.LineItems);
        await db.SaveChangesAsync();
        // Restore the historical creation date after the context's insert audit stamp.
        other.CreatedAt = new DateTime(2026, 7, 1);
        await db.SaveChangesAsync();
        var report = await CreateService(db, tenant).GetProcurementFinanceReconciliationAsync(new DateTime(2026, 8, 31), filterFirstOrder ? fixture.PurchaseOrder.Id : null);
        var first = report.Rows.Single(row => row.PurchaseOrderId == fixture.PurchaseOrder.Id);
        first.InvoiceAmount.Should().Be(60); first.SettledAmount.Should().Be(60);
        first.InvoicePostedAmount.Should().Be(60); first.PaymentPostedAmount.Should().Be(60);
        if (!filterFirstOrder)
        {
            report.Rows.Sum(row => row.InvoiceAmount).Should().Be(100);
            report.Rows.Sum(row => row.SettledAmount).Should().Be(100);
            report.Rows.Sum(row => row.InvoicePostedAmount).Should().Be(100);
            report.Rows.Sum(row => row.PaymentPostedAmount).Should().Be(100);
        }
    }

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
    public async Task Reconciliation_ShouldWeightPaymentPostingByAllEffectiveAllocations()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        var selectedAllocation = fixture.Payment.Allocations.Single();
        selectedAllocation.AllocatedAmount = 50m;
        selectedAllocation.WithholdingTaxAmount = 10m;
        fixture.Payment.TotalAmount = 90m;
        fixture.Payment.AllocatedAmount = 90m;
        fixture.Payment.WithholdingTaxAmount = 0m;

        var otherPurchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "PO-0508-OTHER",
            BusinessPartnerId = fixture.PurchaseOrder.BusinessPartnerId,
            OrderDate = new DateTime(2026, 7, 1),
            Status = "Approved",
            ApprovedAt = new DateTime(2026, 7, 1),
            TotalAmount = 40m,
            Currency = "GHS",
            CreatedAt = new DateTime(2026, 7, 1),
            CreatedBy = "seed"
        };
        var otherInvoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "VI-0508-OTHER",
            BusinessPartnerId = fixture.Payment.BusinessPartnerId,
            SupplierName = "TDC Supplier",
            PurchaseOrderId = otherPurchaseOrder.Id,
            InvoiceDate = new DateTime(2026, 7, 3),
            TotalAmount = 40m,
            PaidAmount = 40m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 40m,
            Status = VendorInvoiceStatus.Paid,
            ApprovalStatus = "Approved",
            SubmittedDate = new DateTime(2026, 7, 3),
            ApprovedDate = new DateTime(2026, 7, 3),
            CreatedAt = new DateTime(2026, 7, 3),
            CreatedBy = "seed"
        };
        fixture.Payment.Allocations.Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = fixture.Payment.Id,
            VendorInvoiceId = otherInvoice.Id,
            AllocatedAmount = 40m,
            AllocationDate = fixture.Payment.PaymentDate,
            CreatedAt = fixture.Payment.PaymentDate,
            CreatedBy = "seed"
        });
        db.AddRange(otherPurchaseOrder, otherInvoice);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        report.Rows.Single().PaymentPostedAmount.Should().Be(60m);
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
    public async Task Reconciliation_ShouldExcludePurchaseOrderApprovedAfterCutoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        fixture.PurchaseOrder.CreatedAt = new DateTime(2026, 7, 1);
        fixture.PurchaseOrder.ApprovedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldReconstructPurchaseOrderBeforeLaterCancellation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        fixture.PurchaseOrder.CreatedAt = new DateTime(2026, 7, 1);
        fixture.PurchaseOrder.ApprovedAt = new DateTime(2026, 7, 2);
        fixture.PurchaseOrder.Status = "Cancelled";
        fixture.PurchaseOrder.CancelledAtUtc = new DateTime(2026, 9, 2);
        fixture.PurchaseOrder.UpdatedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        report.Rows.Should().ContainSingle();
        report.Rows.Single().PurchaseOrderStatus.Should().Be("Approved");
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldUseCommitmentReservationIntervalAtCutoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        fixture.PurchaseOrder.CreatedAt = new DateTime(2026, 7, 1);
        fixture.Commitment.CreatedAt = new DateTime(2026, 7, 1);
        fixture.Commitment.Status = ProcurementBudgetCommitmentStatus.Released;
        fixture.Commitment.ReleasedAtUtc = new DateTime(2026, 9, 2);
        fixture.Commitment.UpdatedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var beforeRelease = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);
        beforeRelease.Rows.Single().CommitmentAmount.Should().Be(100m);

        fixture.Commitment.ReleasedAtUtc = new DateTime(2026, 8, 30);
        fixture.Commitment.UpdatedAt = new DateTime(2026, 8, 30);
        await db.SaveChangesAsync();

        var afterRelease = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);
        afterRelease.Rows.Single().CommitmentAmount.Should().Be(0m);

        fixture.Commitment.Status = ProcurementBudgetCommitmentStatus.Consumed;
        fixture.Commitment.ReleasedAtUtc = null;
        fixture.Commitment.ConsumedAtUtc = new DateTime(2026, 9, 2);
        fixture.Commitment.UpdatedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();

        var beforeConsumption = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);
        beforeConsumption.Rows.Single().CommitmentAmount.Should().Be(100m);

        fixture.Commitment.ConsumedAtUtc = new DateTime(2026, 8, 30);
        fixture.Commitment.UpdatedAt = new DateTime(2026, 8, 30);
        await db.SaveChangesAsync();

        var afterConsumption = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);
        afterConsumption.Rows.Single().CommitmentAmount.Should().Be(0m);

        fixture.Commitment.Status = ProcurementBudgetCommitmentStatus.Reserved;
        fixture.Commitment.ReservedAtUtc = new DateTime(2026, 9, 2);
        fixture.Commitment.UpdatedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();

        var beforeReservation = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);
        beforeReservation.Rows.Single().CommitmentAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldUseCamelCaseControlEventSnapshotAtCutoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        fixture.Commitment.Status = ProcurementBudgetCommitmentStatus.Released;
        fixture.Commitment.ReservedAmount = 20m;
        fixture.Commitment.ReleasedAtUtc = new DateTime(2026, 9, 2);
        fixture.Commitment.UpdatedAt = new DateTime(2026, 9, 2);

        var historicalSnapshot = JsonSerializer.Serialize(new
        {
            fixture.Commitment.Id,
            fixture.Commitment.ProcurementBudgetId,
            fixture.Commitment.PurchaseRequisitionId,
            fixture.Commitment.ReservationReference,
            fixture.Commitment.ReservationSequence,
            Status = (int)ProcurementBudgetCommitmentStatus.Reserved,
            ReservedAmount = 100m,
            fixture.Commitment.Currency,
            fixture.Commitment.BudgetCommittedBefore,
            fixture.Commitment.BudgetAvailableBefore,
            fixture.Commitment.BudgetCommittedAfter,
            fixture.Commitment.BudgetAvailableAfter,
            fixture.Commitment.IsOverride,
            fixture.Commitment.OverrideRuleCode,
            fixture.Commitment.OverrideApprovalReference,
            ReservedAtUtc = new DateTime(2026, 7, 1),
            ReleasedAtUtc = (DateTime?)null,
            ConsumedAtUtc = (DateTime?)null,
            ReleaseReason = (string?)null
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.Set<ProcurementControlEvent>().Add(new ProcurementControlEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventKey = $"test:{Guid.NewGuid():N}",
            EventType = "PurchaseRequisitionBudgetControl",
            Action = "BudgetCommitmentReleased",
            Result = ProcurementControlEventResult.Succeeded,
            SourceType = "PurchaseRequisition",
            SourceId = fixture.Commitment.PurchaseRequisitionId,
            SourceReference = "PR-0508-001",
            ActorUserId = Guid.NewGuid(),
            ActorName = "Budget Controller",
            BeforeJson = historicalSnapshot,
            CorrelationId = "tdc0508-snapshot-test",
            OccurredAtUtc = new DateTime(2026, 9, 2),
            IntegrityHash = new string('a', 64),
            CreatedAt = new DateTime(2026, 9, 2),
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        report.Rows.Single().CommitmentAmount.Should().Be(100m);
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
    public async Task Reconciliation_ShouldReconstructMilestoneAmountBeforeLaterContractValueAmendment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        var contract = await db.Contracts.SingleAsync(item =>
            item.Id == fixture.PurchaseOrder.ContractId);
        var milestone = await db.ContractMilestones.SingleAsync(item =>
            item.ContractId == contract.Id);
        contract.ContractValue = 200m;
        milestone.PaymentAmount = 200m;
        db.ContractAmendments.Add(new ContractAmendment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = contract.Id,
            AmendmentNumber = "AMD-0508-FUTURE",
            SequenceNumber = 1,
            AmendmentType = "ValueChange",
            PreviousValue = 100m,
            NewValue = 200m,
            ValueChange = 100m,
            Status = "Approved",
            RequestedDate = new DateTime(2026, 9, 1),
            ApprovedDate = new DateTime(2026, 9, 2),
            CreatedAt = new DateTime(2026, 9, 1),
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.MilestoneAmount.Should().Be(100m);
        row.CompletedMilestoneAmount.Should().Be(100m);
        row.InvoicedMilestoneAmount.Should().Be(100m);
        row.PaidMilestoneAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldReconstructMilestoneAmountBeforeLaterPercentageEdit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        var milestone = await db.ContractMilestones.SingleAsync(item =>
            item.ContractId == fixture.PurchaseOrder.ContractId);
        var before = JsonSerializer.Serialize(new
        {
            PaymentPercentage = 100m,
            PaymentAmount = 100m
        });
        var after = JsonSerializer.Serialize(new
        {
            PaymentPercentage = 50m,
            PaymentAmount = 50m
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            Username = "contract.controller",
            Action = "Update",
            Resource = nameof(ContractMilestone),
            ResourceId = milestone.Id.ToString(),
            OldValues = before,
            NewValues = after,
            IpAddress = "127.0.0.1",
            Timestamp = new DateTime(2026, 9, 2)
        });
        milestone.PaymentPercentage = 50m;
        milestone.PaymentAmount = 50m;
        milestone.UpdatedAt = new DateTime(2026, 9, 2);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.MilestoneAmount.Should().Be(100m);
        row.CompletedMilestoneAmount.Should().Be(100m);
        row.InvoicedMilestoneAmount.Should().Be(100m);
        row.PaidMilestoneAmount.Should().Be(100m);
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

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldUseCommercialSnapshotBeforeLaterPoAmendment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        await db.SaveChangesAsync();
        var line = fixture.PurchaseOrder.Items.Single();
        var beforeSnapshot = JsonSerializer.Serialize(new
        {
            fixture.PurchaseOrder.SourceRequisitionId,
            Currency = "GHS",
            TotalAmount = 100m,
            Items = new[]
            {
                new { PurchaseOrderItemId = (Guid?)line.Id, UnitPrice = 10m }
            }
        });
        var proposedSnapshot = JsonSerializer.Serialize(new
        {
            fixture.PurchaseOrder.SourceRequisitionId,
            Currency = "GHS",
            TotalAmount = 200m,
            Items = new[]
            {
                new { PurchaseOrderItemId = (Guid?)line.Id, UnitPrice = 20m }
            }
        });
        db.ProcurementPurchaseOrderAmendments.Add(
            new ProcurementPurchaseOrderAmendment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PurchaseOrderId = fixture.PurchaseOrder.Id,
                AmendmentNumber = "POA-0508-001",
                AmendmentSequence = 1,
                BaseRevisionNumber = 0,
                ProposedRevisionNumber = 1,
                Status = ProcurementPurchaseOrderAmendmentStatus.Applied,
                Reason = "Price amendment after reporting cutoff",
                ChangeScope = "Commercial",
                BeforeSnapshotJson = beforeSnapshot,
                BeforeIntegrityHash = Hash(beforeSnapshot),
                ProposedSnapshotJson = proposedSnapshot,
                ProposedIntegrityHash = Hash(proposedSnapshot),
                DiffJson = "[]",
                DiffIntegrityHash = Hash("[]"),
                ProposedSourceReference = "TEST-SOURCE",
                Currency = "GHS",
                BeforeTotalAmount = 100m,
                ProposedTotalAmount = 200m,
                CommitmentDelta = 100m,
                AppliedAtUtc = new DateTime(2026, 9, 2),
                IdempotencyKey = "po-amendment-after-cutoff",
                CorrelationId = "po-amendment-after-cutoff",
                CreatedAt = new DateTime(2026, 9, 1)
            });
        fixture.PurchaseOrder.TotalAmount = 200m;
        fixture.PurchaseOrder.SubTotal = 200m;
        fixture.PurchaseOrder.RevisionNumber = 1;
        fixture.PurchaseOrder.UpdatedAt = new DateTime(2026, 9, 2);
        line.UnitPrice = 20m;
        line.LineTotal = 200m;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.PurchaseOrderAmount.Should().Be(100m);
        row.AcceptedReceiptAmount.Should().Be(100m);
        row.CommitmentGroupOrderAmount.Should().Be(100m);
        row.Issues.Should().NotContain(item =>
            item.Code == "RECEIPT_EXCEEDS_ORDER" ||
            item.Code == "COMMITMENT_ORDER_VARIANCE");
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    public async Task Reconciliation_ShouldExcludeReversedAllocationOriginalAtCutoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBalancedScenario(db, tenantId);
        var original = fixture.Payment.Allocations.Single();
        fixture.Payment.Allocations.Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = fixture.Payment.Id,
            VendorInvoiceId = original.VendorInvoiceId,
            AllocatedAmount = -original.AllocatedAmount,
            DiscountAmount = -original.DiscountAmount,
            WithholdingTaxAmount = -original.WithholdingTaxAmount,
            AllocationDate = new DateTime(2026, 7, 5),
            IsReversal = true,
            OriginalAllocationId = original.Id,
            CreatedAt = new DateTime(2026, 7, 5),
            CreatedBy = "seed"
        });
        fixture.Payment.Allocations.Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = fixture.Payment.Id,
            VendorInvoiceId = original.VendorInvoiceId,
            AllocatedAmount = original.AllocatedAmount,
            DiscountAmount = original.DiscountAmount,
            WithholdingTaxAmount = original.WithholdingTaxAmount,
            AllocationDate = new DateTime(2026, 7, 5),
            CreatedAt = new DateTime(2026, 7, 5),
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var report = await service.GetProcurementFinanceReconciliationAsync(
            new DateTime(2026, 8, 31), fixture.PurchaseOrder.Id);

        var row = report.Rows.Single();
        row.SettledAmount.Should().Be(100m);
        row.PaymentPostedAmount.Should().Be(100m);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

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
        var accountingBookId = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        db.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Id = unitOfMeasureId,
            TenantId = tenantId,
            Code = "EA",
            Name = "Each",
            Category = "Quantity",
            IsBaseUnit = true,
            IsActive = true,
            DecimalPlaces = 0,
            RoundingIncrement = 1m,
            CreatedBy = "seed"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = accountingBookId,
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS",
            IsDefault = true
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
            ApprovedAt = new DateTime(2026, 7, 1),
            TotalAmount = 100m,
            Currency = "GHS",
            SourceRequisitionId = requisitionId,
            ContractId = contractId,
            CreatedAt = new DateTime(2026, 7, 1),
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
            UnitOfMeasureId = unitOfMeasureId,
            UnitOfMeasureCodeSnapshot = "EA",
            UnitOfMeasureDecimalPlacesSnapshot = 0,
            UnitOfMeasureRoundingIncrementSnapshot = 1m,
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
            UnitOfMeasure = "EA",
            UnitOfMeasureId = unitOfMeasureId,
            UnitOfMeasureCodeSnapshot = "EA",
            UnitOfMeasureDecimalPlacesSnapshot = 0,
            UnitOfMeasureRoundingIncrementSnapshot = 1m
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

        var invoiceJournal = Journal(tenantId, accountingBookId, "JE-INV-0508", "VendorInvoice", 100m);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "VI-0508-001",
            BusinessPartnerId = Guid.NewGuid(),
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
            CreatedAt = new DateTime(2026, 7, 3),
            CreatedBy = "seed"
        };
        var invoicePosting = Posting(tenantId, accountingBookId, invoice.Id, "VendorInvoice", invoiceJournal.Id, 100m);
        db.JournalEntries.Add(invoiceJournal);
        db.FinancePostingEvents.Add(invoicePosting);
        db.VendorInvoices.Add(invoice);

        var paymentJournal = Journal(tenantId, accountingBookId, "JE-PAY-0508", "VendorPayment", 100m);
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-0508-001",
            BusinessPartnerId = invoice.BusinessPartnerId,
            PaymentDate = new DateTime(2026, 7, 4),
            TotalAmount = 100m,
            AllocatedAmount = 100m,
            CurrencyCode = "GHS",
            Status = VendorPaymentStatus.Processed,
            JournalEntryId = paymentJournal.Id,
            CreatedAt = new DateTime(2026, 7, 4),
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
            CreatedAt = new DateTime(2026, 7, 4),
            CreatedBy = "seed"
        };
        payment.Allocations.Add(allocation);
        var paymentPosting = Posting(tenantId, accountingBookId, payment.Id, "VendorPayment", paymentJournal.Id, 100m);
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

        // ApplicationDbContext stamps Added entities with the current UTC time.
        // Persist first, then restore the scenario's historical creation dates so
        // fixed as-of reporting tests do not change behavior after 31 Aug 2026.
        db.SaveChanges();
        foreach (var entry in db.ChangeTracker.Entries<BaseEntity>())
            entry.Entity.CreatedAt = new DateTime(2026, 7, 1);

        return new ScenarioFixture(po, commitment, payment, paymentPosting, certificate);
    }

    private static JournalEntry Journal(
        Guid tenantId,
        Guid accountingBookId,
        string number,
        string sourceDocumentType,
        decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AccountingBookId = accountingBookId,
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
        Guid accountingBookId,
        Guid sourceId,
        string sourceDocumentType,
        Guid journalId,
        decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AccountingBookId = accountingBookId,
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
