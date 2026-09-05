using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementBudgetCommitmentLifecycleServiceTests
{
    [Fact]
    public async Task PurchaseOrderApprovalPromotesReservationOnceAndAcceptedReceiptUtilizesOnlyAcceptedValue()
    {
        await using var fixture = new Fixture(1_000m, 400m);
        var purchaseOrder = fixture.NewPurchaseOrder(300m);
        fixture.Context.PurchaseOrders.Add(purchaseOrder);
        await fixture.Context.SaveChangesAsync();

        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "po-approval");
            await fixture.Context.SaveChangesAsync();
        });

        fixture.Budget.ReservedAmount.Should().Be(100m);
        fixture.Budget.CommittedAmount.Should().Be(300m);
        fixture.Budget.UtilizedAmount.Should().Be(0m);

        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "po-approval-retry");
            await fixture.Context.SaveChangesAsync();
        });
        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries
            .CountAsync(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment))
            .Should().Be(1);

        var receiptId = Guid.NewGuid();
        fixture.Context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = receiptId,
            TenantId = fixture.TenantId,
            PurchaseOrderId = purchaseOrder.Id,
            ReceiptNumber = "REC-001"
        });
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.UtilizePurchaseOrderAsync(
                purchaseOrder.Id, receiptId, "REC-001", 120m, "receipt-approval");
            await fixture.Context.SaveChangesAsync();
        });

        fixture.Budget.ReservedAmount.Should().Be(100m);
        fixture.Budget.CommittedAmount.Should().Be(180m);
        fixture.Budget.UtilizedAmount.Should().Be(120m);
        fixture.Commitment.FormallyCommittedAmount.Should().Be(300m);
        fixture.Commitment.UtilizedAmount.Should().Be(120m);
    }

    [Fact]
    public async Task SuccessfulPurchaseOrderApprovalReplayDoesNotRevalidateLaterReservationState()
    {
        await using var fixture = new Fixture(1_000m, 400m);
        var purchaseOrder = fixture.NewPurchaseOrder(300m);
        fixture.Context.PurchaseOrders.Add(purchaseOrder);
        await fixture.Context.SaveChangesAsync();

        ProcurementBudgetCommitmentLedgerEntry original = null!;
        await fixture.InTransaction(async () =>
        {
            original = await fixture.Service.CommitPurchaseOrderAsync(
                purchaseOrder,
                "po-approval");
            await fixture.Context.SaveChangesAsync();
        });

        fixture.Commitment.Status = ProcurementBudgetCommitmentStatus.Consumed;
        fixture.Budget.Status = "Closed";
        fixture.Budget.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        await fixture.Context.SaveChangesAsync();

        ProcurementBudgetCommitmentLedgerEntry replay = null!;
        await fixture.InTransaction(async () =>
        {
            replay = await fixture.Service.CommitPurchaseOrderAsync(
                purchaseOrder,
                "po-approval-retry-after-expiry");
            await fixture.Context.SaveChangesAsync();
        });

        replay.Id.Should().Be(original.Id);
        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                item.SourceType == "PurchaseOrder" &&
                item.SourceId == purchaseOrder.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task AmendedPurchaseOrderUsesEffectiveExposureForReplayAndReceiptCapacity()
    {
        await using var fixture = new Fixture(1_000m, 500m);
        var purchaseOrder = fixture.NewPurchaseOrder(300m);
        fixture.Context.PurchaseOrders.Add(purchaseOrder);
        await fixture.Context.SaveChangesAsync();
        ProcurementBudgetCommitmentLedgerEntry formal = null!;
        await fixture.InTransaction(async () =>
        {
            formal = await fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "po-approval");
            await fixture.Context.SaveChangesAsync();
        });

        fixture.Context.ProcurementPurchaseOrderCommitmentAdjustments.Add(
            NewAdjustment(fixture, purchaseOrder, formal, 1, 300m, 400m, 100m));
        purchaseOrder.TotalAmount = 400m;
        fixture.Budget.CommittedAmount = 400m;
        fixture.Commitment.ReservedAmount = 400m;
        fixture.Commitment.FormallyCommittedAmount = 400m;
        await fixture.Context.SaveChangesAsync();

        await fixture.InTransaction(async () =>
        {
            var replay = await fixture.Service.CommitPurchaseOrderAsync(
                purchaseOrder,
                "po-amendment-replay");
            replay.Id.Should().Be(formal.Id);
        });

        var receiptId = Guid.NewGuid();
        fixture.Context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = receiptId,
            TenantId = fixture.TenantId,
            PurchaseOrderId = purchaseOrder.Id,
            ReceiptNumber = "REC-AMENDED"
        });
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.UtilizePurchaseOrderAsync(
                purchaseOrder.Id,
                receiptId,
                "REC-AMENDED",
                350m,
                "receipt-amended");
            await fixture.Context.SaveChangesAsync();
        });
        fixture.Budget.CommittedAmount.Should().Be(50m);

        await using var decreased = new Fixture(1_000m, 500m);
        var decreasedPo = decreased.NewPurchaseOrder(300m);
        decreased.Context.PurchaseOrders.Add(decreasedPo);
        await decreased.Context.SaveChangesAsync();
        ProcurementBudgetCommitmentLedgerEntry decreasedFormal = null!;
        await decreased.InTransaction(async () =>
        {
            decreasedFormal = await decreased.Service.CommitPurchaseOrderAsync(decreasedPo, "po-approval");
            await decreased.Context.SaveChangesAsync();
        });
        decreased.Context.ProcurementPurchaseOrderCommitmentAdjustments.Add(
            NewAdjustment(decreased, decreasedPo, decreasedFormal, 1, 300m, 200m, -100m));
        decreasedPo.TotalAmount = 200m;
        decreased.Budget.CommittedAmount = 200m;
        decreased.Commitment.ReservedAmount = 200m;
        decreased.Commitment.FormallyCommittedAmount = 200m;
        var decreasedReceiptId = Guid.NewGuid();
        decreased.Context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = decreasedReceiptId,
            TenantId = decreased.TenantId,
            PurchaseOrderId = decreasedPo.Id,
            ReceiptNumber = "REC-DECREASED"
        });
        await decreased.Context.SaveChangesAsync();
        var exceed = () => decreased.InTransaction(() =>
            decreased.Service.UtilizePurchaseOrderAsync(
                decreasedPo.Id,
                decreasedReceiptId,
                "REC-DECREASED",
                201m,
                "receipt-decreased"));
        (await exceed.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("FORMAL_BUDGET_COMMITMENT_EXCEEDED");
    }

    [Fact]
    public async Task ChildPurchaseOrderAllocatesAgainstFormalContractWithoutDoubleCommittingBudget()
    {
        await using var fixture = new Fixture(1_000m, 500m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-001", Title = "Works tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-001", ContractTitle = "Works contract",
            ContractValue = 450m, Currency = "GHS"
        };
        var purchaseOrder = fixture.NewPurchaseOrder(200m);
        purchaseOrder.ContractId = contract.Id;
        fixture.Context.AddRange(tender, contract, purchaseOrder);
        await fixture.Context.SaveChangesAsync();

        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
        });
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "child-po-approval");
            await fixture.Context.SaveChangesAsync();
        });

        fixture.Budget.CommittedAmount.Should().Be(450m);
        fixture.Budget.ReservedAmount.Should().Be(50m);
        var entries = await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.ToListAsync();
        entries.Should().ContainSingle(item =>
            item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
            item.SourceType == "Contract" && item.Amount == 450m);
        entries.Should().ContainSingle(item =>
            item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation &&
            item.SourceType == "PurchaseOrder" && item.Amount == 200m);
    }

    [Fact]
    public async Task ChildPurchaseOrderCannotAllocateCapacityReleasedFromItsParentContract()
    {
        await using var fixture = new Fixture(1_000m, 500m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-RELEASED-PARENT", Title = "Released parent tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-RELEASED-PARENT", ContractTitle = "Released parent contract",
            ContractValue = 450m, Currency = "GHS", Status = "Active"
        };
        var child = fixture.NewPurchaseOrder(1m);
        child.ContractId = contract.Id;
        fixture.Context.AddRange(tender, contract, child);
        await fixture.Context.SaveChangesAsync();

        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "released-parent-commit");
            await fixture.Context.SaveChangesAsync();
        });
        await fixture.InTransaction(async () =>
        {
            (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "released-parent-close"))!.Amount.Should().Be(450m);
            await fixture.Context.SaveChangesAsync();
        });

        var allocate = () => fixture.InTransaction(() =>
            fixture.Service.CommitPurchaseOrderAsync(child, "released-parent-child"));
        (await allocate.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("PO_CONTRACT_COMMITMENT_EXCEEDED");
    }

    [Fact]
    public async Task ForeignTenantSourceObjectsFailClosedBeforeChangingTheCurrentTenantReservation()
    {
        await using var fixture = new Fixture(1_000m, 400m);
        var purchaseOrder = fixture.NewPurchaseOrder(300m);
        purchaseOrder.TenantId = Guid.NewGuid();

        var action = () => fixture.InTransaction(() =>
            fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "foreign-po"));

        var error = await action.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>();
        error.Which.Code.Should().Be("BUDGET_LIFECYCLE_SOURCE_TENANT_MISMATCH");
        fixture.Budget.ReservedAmount.Should().Be(400m);
        fixture.Budget.CommittedAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ChildPurchaseOrderMustShareTheContractsPurchaseRequisitionLineage()
    {
        await using var fixture = new Fixture(1_000m, 500m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-002", Title = "Goods tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-002", ContractTitle = "Goods contract",
            ContractValue = 450m, Currency = "GHS"
        };
        fixture.Context.AddRange(tender, contract);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
        });

        var child = fixture.NewPurchaseOrder(200m);
        child.ContractId = contract.Id;
        child.SourceRequisitionId = Guid.NewGuid();
        fixture.Context.PurchaseOrders.Add(child);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.InTransaction(() =>
            fixture.Service.CommitPurchaseOrderAsync(child, "invalid-child-po"));
        var error = await action.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>();
        error.Which.Code.Should().Be("PO_CONTRACT_REQUISITION_LINEAGE_MISMATCH");
        fixture.Budget.CommittedAmount.Should().Be(450m);
    }

    [Fact]
    public async Task PaymentCertificateMustBelongToTheContractBeingUtilized()
    {
        await using var fixture = new Fixture(1_000m, 500m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-003", Title = "Works tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-003", ContractTitle = "Works contract",
            ContractValue = 450m, Currency = "GHS"
        };
        var certificate = new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProjectId = Guid.NewGuid(),
            ContractId = Guid.NewGuid(), ClientRequestId = Guid.NewGuid(),
            Title = "Certificate 1", Currency = "GHS", GrossCertifiedAmount = 100m
        };
        fixture.Context.AddRange(tender, contract, certificate);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
        });

        var action = () => fixture.InTransaction(() =>
            fixture.Service.UtilizeContractCertificateAsync(
                contract.Id, certificate.Id, "CERT-001", 100m, "certificate-approval"));
        var error = await action.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>();
        error.Which.Code.Should().Be("BUDGET_UTILIZATION_SOURCE_LINEAGE_INVALID");
        fixture.Budget.CommittedAmount.Should().Be(450m);
        fixture.Budget.UtilizedAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ContractCloseReleasesOnlyUnusedFormalExposureAndReplayIsIdempotent()
    {
        await using var fixture = new Fixture(1_000m, 450m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-CLOSE-001", Title = "Works closeout tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-CLOSE-001", ContractTitle = "Works closeout contract",
            ContractValue = 450m, Currency = "GHS", Status = "Active"
        };
        var certificate = new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProjectId = Guid.NewGuid(),
            ContractId = contract.Id, ClientRequestId = Guid.NewGuid(),
            CertificateNumber = "CERT-CLOSE-001", Title = "Partial utilization",
            Currency = "GHS", GrossCertifiedAmount = 150m
        };
        fixture.Context.AddRange(tender, contract, certificate);
        await fixture.Context.SaveChangesAsync();

        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
        });
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.UtilizeContractCertificateAsync(
                contract.Id, certificate.Id, certificate.CertificateNumber, 150m,
                "certificate-approval");
            await fixture.Context.SaveChangesAsync();
        });

        ProcurementBudgetCommitmentLedgerEntry release = null!;
        await fixture.InTransaction(async () =>
        {
            release = (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout"))!;
            await fixture.Context.SaveChangesAsync();
        });

        release.Amount.Should().Be(300m);
        release.EntryType.Should().Be(ProcurementBudgetCommitmentLedgerEntryType.Release);
        fixture.Budget.CommittedAmount.Should().Be(0m);
        fixture.Budget.UtilizedAmount.Should().Be(150m);
        fixture.Budget.RemainingAmount.Should().Be(850m);
        fixture.Commitment.ReservedAmount.Should().Be(150m);
        fixture.Commitment.FormallyCommittedAmount.Should().Be(150m);
        fixture.Commitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Consumed);
        fixture.Commitment.ConsumedAtUtc.Should().NotBeNull();

        ProcurementBudgetCommitmentLedgerEntry replay = null!;
        await fixture.InTransaction(async () =>
        {
            replay = (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout-replay"))!;
            await fixture.Context.SaveChangesAsync();
        });
        replay.Id.Should().Be(release.Id);
        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceType == "Contract" &&
                item.SourceId == contract.Id))
            .Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task ContractCloseRejectsUnutilizedChildPurchaseOrderAllocation(
        decimal utilizedAmount)
    {
        await using var fixture = new Fixture(1_000m, 100m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-CHILD-CLOSE-001", Title = "Child allocation close guard",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-CHILD-CLOSE-001", ContractTitle = "Child allocation close guard",
            ContractValue = 100m, Currency = "GHS", Status = "Active"
        };
        var purchaseOrder = fixture.NewPurchaseOrder(60m);
        purchaseOrder.ContractId = contract.Id;
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            PurchaseOrderId = purchaseOrder.Id, ReceiptNumber = "REC-CHILD-CLOSE-001"
        };
        fixture.Context.AddRange(tender, contract, purchaseOrder, receipt);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
            await fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "child-po-approval");
            await fixture.Context.SaveChangesAsync();
            if (utilizedAmount > 0m)
            {
                await fixture.Service.UtilizePurchaseOrderAsync(
                    purchaseOrder.Id, receipt.Id, receipt.ReceiptNumber,
                    utilizedAmount, "child-po-receipt");
                await fixture.Context.SaveChangesAsync();
            }
        });

        var close = () => fixture.InTransaction(() =>
            fixture.Service.ReleaseUnusedContractAsync(contract.Id, "works-closeout"));

        var error = await close.Should()
            .ThrowAsync<ProcurementBudgetCommitmentLifecycleException>();
        error.Which.Code.Should().Be("CONTRACT_CHILD_PO_ALLOCATION_OUTSTANDING");
        error.Which.Message.Should().Contain(purchaseOrder.OrderNumber)
            .And.Contain("receipt/utilization lifecycle");
        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceId == contract.Id))
            .Should().Be(0);
    }

    [Fact]
    public async Task ContractCloseAllowsFullyUtilizedChildAllocationAndReplayIsIdempotent()
    {
        await using var fixture = new Fixture(1_000m, 100m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-CHILD-CLOSE-002", Title = "Completed child allocation",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-CHILD-CLOSE-002", ContractTitle = "Completed child allocation",
            ContractValue = 100m, Currency = "GHS", Status = "Active"
        };
        var purchaseOrder = fixture.NewPurchaseOrder(60m);
        purchaseOrder.ContractId = contract.Id;
        var receipt = new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            PurchaseOrderId = purchaseOrder.Id, ReceiptNumber = "REC-CHILD-CLOSE-002"
        };
        fixture.Context.AddRange(tender, contract, purchaseOrder, receipt);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
            await fixture.Service.CommitPurchaseOrderAsync(purchaseOrder, "child-po-approval");
            await fixture.Context.SaveChangesAsync();
            await fixture.Service.UtilizePurchaseOrderAsync(
                purchaseOrder.Id, receipt.Id, receipt.ReceiptNumber, 60m, "child-po-receipt");
            await fixture.Context.SaveChangesAsync();
        });

        ProcurementBudgetCommitmentLedgerEntry release = null!;
        ProcurementBudgetCommitmentLedgerEntry replay = null!;
        await fixture.InTransaction(async () =>
        {
            release = (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout"))!;
            await fixture.Context.SaveChangesAsync();
            replay = (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout-replay"))!;
        });

        release.Amount.Should().Be(40m);
        replay.Id.Should().Be(release.Id);
        fixture.Commitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Consumed);
        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceId == contract.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task FullyUtilizedContractCloseAndReplayDoNotCreateZeroValueRelease()
    {
        await using var fixture = new Fixture(1_000m, 300m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-CLOSE-002", Title = "Fully utilized Works tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-CLOSE-002", ContractTitle = "Fully utilized Works contract",
            ContractValue = 300m, Currency = "GHS", Status = "Active"
        };
        var certificate = new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProjectId = Guid.NewGuid(),
            ContractId = contract.Id, ClientRequestId = Guid.NewGuid(),
            CertificateNumber = "CERT-CLOSE-002", Title = "Full utilization",
            Currency = "GHS", GrossCertifiedAmount = 300m
        };
        fixture.Context.AddRange(tender, contract, certificate);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
            await fixture.Service.UtilizeContractCertificateAsync(
                contract.Id, certificate.Id, certificate.CertificateNumber, 300m,
                "certificate-approval");
            await fixture.Context.SaveChangesAsync();
        });

        await fixture.InTransaction(async () =>
        {
            (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout")).Should().BeNull();
            (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout-replay")).Should().BeNull();
        });

        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceId == contract.Id))
            .Should().Be(0);
        fixture.Budget.CommittedAmount.Should().Be(0m);
        fixture.Budget.UtilizedAmount.Should().Be(300m);
        fixture.Commitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Consumed);
        fixture.Commitment.ConsumedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task FullyUtilizedContractCloseKeepsSharedEnvelopeReservedForOutstandingSiblingPo()
    {
        await using var fixture = new Fixture(1_000m, 400m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-CLOSE-SHARED", Title = "Shared envelope contract",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-CLOSE-SHARED", ContractTitle = "Shared envelope contract",
            ContractValue = 300m, Currency = "GHS", Status = "Active"
        };
        var siblingPurchaseOrder = fixture.NewPurchaseOrder(100m);
        var certificate = new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProjectId = Guid.NewGuid(),
            ContractId = contract.Id, ClientRequestId = Guid.NewGuid(),
            CertificateNumber = "CERT-CLOSE-SHARED", Title = "Contract fully utilized",
            Currency = "GHS", GrossCertifiedAmount = 300m
        };
        fixture.Context.AddRange(
            tender, contract, siblingPurchaseOrder, certificate);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
            await fixture.Service.CommitPurchaseOrderAsync(
                siblingPurchaseOrder, "sibling-po-approval");
            await fixture.Context.SaveChangesAsync();
            await fixture.Service.UtilizeContractCertificateAsync(
                contract.Id, certificate.Id, certificate.CertificateNumber,
                300m, "certificate-approval");
            await fixture.Context.SaveChangesAsync();
        });

        await fixture.InTransaction(async () =>
        {
            (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout")).Should().BeNull();
            (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout-replay")).Should().BeNull();
        });

        fixture.Commitment.ReservedAmount.Should().Be(400m);
        fixture.Commitment.UtilizedAmount.Should().Be(300m);
        fixture.Commitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Reserved);
        fixture.Commitment.ConsumedAtUtc.Should().BeNull();
        fixture.Budget.CommittedAmount.Should().Be(100m);
        (await fixture.Context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceId == contract.Id))
            .Should().Be(0);
    }

    [Fact]
    public async Task FullyUnusedSoleContractReleaseClosesTheAggregateEnvelope()
    {
        await using var fixture = new Fixture(1_000m, 300m);
        var tender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderNumber = "TND-CLOSE-003", Title = "Unused Works tender",
            SourcePurchaseRequisitionId = fixture.Requisition.Id
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = tender.Id,
            ContractNumber = "CTR-CLOSE-003", ContractTitle = "Unused Works contract",
            ContractValue = 300m, Currency = "GHS", Status = "Active"
        };
        fixture.Context.AddRange(tender, contract);
        await fixture.Context.SaveChangesAsync();
        await fixture.InTransaction(async () =>
        {
            await fixture.Service.CommitContractAsync(contract, "contract-activation");
            await fixture.Context.SaveChangesAsync();
        });

        await fixture.InTransaction(async () =>
        {
            (await fixture.Service.ReleaseUnusedContractAsync(
                contract.Id, "works-closeout"))!.Amount.Should().Be(300m);
            await fixture.Context.SaveChangesAsync();
        });

        fixture.Commitment.ReservedAmount.Should().Be(0m);
        fixture.Commitment.FormallyCommittedAmount.Should().Be(0m);
        fixture.Commitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Released);
        fixture.Commitment.ReleasedAtUtc.Should().NotBeNull();
        fixture.Commitment.ReleasedById.Should().Be(fixture.UserId);
        fixture.Budget.CommittedAmount.Should().Be(0m);
        fixture.Budget.RemainingAmount.Should().Be(1_000m);
    }

    private static ProcurementPurchaseOrderCommitmentAdjustment NewAdjustment(
        Fixture fixture,
        PurchaseOrder purchaseOrder,
        ProcurementBudgetCommitmentLedgerEntry formal,
        int sequence,
        decimal before,
        decimal after,
        decimal delta) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = fixture.TenantId,
        AmendmentId = Guid.NewGuid(),
        PurchaseOrderId = purchaseOrder.Id,
        PurchaseRequisitionId = fixture.Requisition.Id,
        ProcurementBudgetId = fixture.Budget.Id,
        BudgetCommitmentId = formal.ProcurementBudgetCommitmentId,
        Sequence = sequence,
        PurchaseOrderAmountBefore = before,
        PurchaseOrderAmountAfter = after,
        RequisitionExposureBefore = before,
        RequisitionExposureAfter = after,
        CommitmentAmountBefore = before,
        CommitmentAmountAfter = after,
        DeltaAmount = delta,
        BudgetCommittedBefore = before,
        BudgetCommittedAfter = after,
        BudgetAvailableBefore = fixture.Budget.AllocatedAmount - before,
        BudgetAvailableAfter = fixture.Budget.AllocatedAmount - after,
        Currency = "GHS",
        AppliedAtUtc = DateTime.UtcNow,
        AppliedById = fixture.UserId,
        AppliedByName = "Procurement Approver",
        IntegrityHash = new string('a', 64),
        CorrelationId = $"amendment-{sequence}"
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture(decimal allocated, decimal reserved)
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
                { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active });
            Requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RequisitionNumber = "PR-001",
                Status = "Approved", RequestedById = UserId, Currency = "GHS",
                TotalAmount = reserved
            };
            Budget = new ProcurementBudget
            {
                Id = Guid.NewGuid(), TenantId = TenantId, BudgetCode = "PB-001",
                Title = "Approved budget", DepartmentId = Guid.NewGuid(), FiscalYear = 2026,
                AllocatedAmount = allocated, ReservedAmount = reserved,
                RemainingAmount = allocated - reserved, Currency = "GHS", Status = "Approved"
            };
            Requisition.BudgetId = Budget.Id;
            Commitment = new ProcurementBudgetCommitment
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProcurementBudgetId = Budget.Id,
                PurchaseRequisitionId = Requisition.Id, ReservationReference = "PR-001/BUDGET/A1",
                Status = ProcurementBudgetCommitmentStatus.Reserved, ReservedAmount = reserved,
                Currency = "GHS", ReservedAtUtc = DateTime.UtcNow, ReservedById = UserId,
                ReservedByName = "Procurement Approver", CorrelationId = "reserve",
                BudgetAllocatedSnapshot = allocated, BudgetReservedAfter = reserved,
                BudgetAvailableAfter = allocated - reserved
            };
            Context.AddRange(Budget, Requisition, Commitment);
            Context.SaveChanges();

            var user = new Mock<ICurrentUserProvider>();
            user.SetupGet(value => value.TenantId).Returns(TenantId);
            user.SetupGet(value => value.UserId).Returns(UserId);
            user.SetupGet(value => value.IsAuthenticated).Returns(true);
            user.SetupGet(value => value.Username).Returns("procurement.approver");
            user.SetupGet(value => value.FullName).Returns("Procurement Approver");
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementBudgetCommitmentLifecycleService(
                _unitOfWork, user.Object, new ProcurementBudgetReservationStore(Context));
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementBudget Budget { get; }
        public PurchaseRequisition Requisition { get; }
        public ProcurementBudgetCommitment Commitment { get; }
        public ProcurementBudgetCommitmentLifecycleService Service { get; }

        public PurchaseOrder NewPurchaseOrder(decimal total) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, OrderNumber = $"PO-{Guid.NewGuid():N}"[..15],
            SourceRequisitionId = Requisition.Id, TotalAmount = total, Currency = "GHS",
            Status = "Approved", OrderDate = DateTime.UtcNow
        };

        public async Task InTransaction(Func<Task> action)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await action();
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
