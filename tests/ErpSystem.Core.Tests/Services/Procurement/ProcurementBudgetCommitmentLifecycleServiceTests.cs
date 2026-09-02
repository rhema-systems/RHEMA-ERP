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
