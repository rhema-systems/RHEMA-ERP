using System.Reflection;
using System.Data;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Repositories.Procurement;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

/// <summary>
/// Executes the production procurement SQL triggers against a disposable SQL
/// Server database. These tests deliberately do not use EF InMemory or SQLite:
/// the SQL Server trigger numbers and transactional hard stops are part of the
/// acceptance contract.
/// </summary>
public sealed class ProcurementArchitectureSqlServerIntegrationTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ReservationPromotesAndUtilizesExactlyOnceWithoutCrossTenantOrContractDoubleCount()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync(string.Empty);
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var foreignActorId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var foreignDepartmentId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var foreignRequisitionId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var foreignBudgetId = Guid.NewGuid();
        var foreignFormalPoId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var tenderBidId = Guid.NewGuid();
        var tenderAwardId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();
        var postReleaseCertificateId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        await context.Database.EnsureCreatedAsync();
        await database.ApplySqlOperationsAsync(
            new EnforceAtomicPurchaseOrderBudgetCommitment());

        var now = DateTime.UtcNow;
        var tenant = NewTenant(tenantId, "PROC-SQL-PRIMARY");
        var foreignTenant = NewTenant(foreignTenantId, "PROC-SQL-FOREIGN");
        var actor = NewUser(actorId, tenantId, "proc.sql.actor");
        var foreignActor = NewUser(foreignActorId, foreignTenantId, "proc.sql.foreign");
        context.AddRange(
            tenant, foreignTenant, actor, foreignActor,
            new Department
            {
                Id = departmentId, TenantId = tenantId, Name = "Procurement SQL",
                Code = "PROC-SQL", AccountCode = "PROC-SQL", CreatedAt = now
            },
            new Department
            {
                Id = foreignDepartmentId, TenantId = foreignTenantId, Name = "Foreign SQL",
                Code = "FOREIGN-SQL", AccountCode = "FOREIGN-SQL", CreatedAt = now
            });
        await context.SaveChangesAsync();

        var budget = NewBudget(budgetId, tenantId, departmentId, actorId, "PB-SQL-001", 1000m);
        var requisition = NewRequisition(requisitionId, tenantId, actorId, budgetId, "PB-SQL-001", "PR-SQL-001", 1000m);
        requisition.Status = "Approved";
        var sourcingRelease = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PurchaseRequisitionId = requisitionId, AttemptNumber = 1,
            ReleaseReference = "PR-SQL-001/REL/A1",
            ReleasedAtUtc = now, ReleasedById = actorId,
            ReleasedByName = "Procurement SQL Actor",
            ReleaseReason = "SQL lifecycle verification",
            CorrelationId = "sql-release",
            ControlFingerprint = new string('a', 64),
            SnapshotJson = "{}", IntegrityHash = new string('b', 64),
            CreatedAt = now, CreatedById = actorId
        };
        var foreignBudget = NewBudget(foreignBudgetId, foreignTenantId, foreignDepartmentId,
            foreignActorId, "PB-SQL-FOREIGN", 100m);
        var foreignRequisition = NewRequisition(foreignRequisitionId, foreignTenantId, foreignActorId,
            foreignBudgetId, "PB-SQL-FOREIGN", "PR-SQL-FOREIGN", 100m);
        var foreignCommitment = NewCommitment(Guid.NewGuid(), foreignTenantId, foreignBudget,
            foreignRequisition, foreignActorId, 100m, now);
        foreignCommitment.FormallyCommittedAmount = 100m;
        foreignCommitment.BudgetReservedAfter = 0m;
        foreignCommitment.BudgetCommittedAfter = 100m;
        foreignBudget.ReservedAmount = 0m;
        foreignBudget.CommittedAmount = 100m;
        var foreignFormalEntry = new ProcurementBudgetCommitmentLedgerEntry
        {
            Id = Guid.NewGuid(), TenantId = foreignTenantId,
            ProcurementBudgetCommitmentId = foreignCommitment.Id,
            ProcurementBudgetId = foreignBudgetId,
            PurchaseRequisitionId = foreignRequisitionId,
            EntryType = ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            SourceType = "PurchaseOrder", SourceId = foreignFormalPoId,
            SourceReference = "PO-SQL-FOREIGN-STORED", Amount = 100m,
            Currency = "GHS", OccurredAtUtc = now, ActorUserId = foreignActorId,
            ActorName = "Foreign SQL Actor", CorrelationId = "sql-foreign-formal",
            CreatedAt = now, CreatedById = foreignActorId
        };
        var partner = new BusinessPartner
        {
            Id = partnerId, TenantId = tenantId, PartnerCode = "SUP-SQL-001",
            PartnerName = "SQL Supplier", PartnerType = "Supplier",
            RegistrationStatus = "Approved", ApprovalStatus = "Approved",
            ApprovedById = actorId, ApprovedDate = now, Currency = "GHS",
            IsActive = true, CreatedAt = now
        };
        var tender = new Tender
        {
            Id = tenderId, TenantId = tenantId, TenderNumber = "TND-SQL-001",
            Title = "SQL commitment contract", TenderType = "RFQ", Status = "Awarded",
            Currency = "GHS", SourcePurchaseRequisitionId = requisitionId,
            CreatedAt = now, CreatedById = actorId
        };
        var tenderBid = new TenderBid
        {
            Id = tenderBidId, TenantId = tenantId, TenderId = tenderId,
            BusinessPartnerId = partnerId, BidNumber = "BID-SQL-001",
            Status = "Accepted", TotalBidAmount = 300m, Currency = "GHS",
            CreatedAt = now
        };
        var tenderAward = new TenderAward
        {
            Id = tenderAwardId, TenantId = tenantId, TenderId = tenderId,
            TenderBidId = tenderBidId, BusinessPartnerId = partnerId,
            OriginalBidAmount = 300m, AwardedAmount = 300m, Currency = "GHS",
            AwardedById = actorId, Status = "ContractSigned", CreatedAt = now
        };
        context.AddRange(budget, requisition, sourcingRelease, foreignBudget, foreignRequisition,
            foreignCommitment, foreignFormalEntry, partner, tender, tenderBid, tenderAward);
        await context.SaveChangesAsync();

        var currentUser = NewCurrentUser(actorId, tenantId);
        using var unitOfWork = new UnitOfWork(context);
        var reservationStore = new ProcurementBudgetReservationStore(context);
        var access = new Mock<IProcurementAccessControlService>();
        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(item => item.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventDto());
        var reservationService = new ProcurementRequisitionBudgetControlService(
            unitOfWork, currentUser.Object, access.Object, controlEvents.Object, reservationStore);
        var lifecycle = new ProcurementBudgetCommitmentLifecycleService(
            unitOfWork, currentUser.Object, reservationStore);

        await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

        // Submission/approval readiness is read-only. The reservation begins
        // only inside the final downstream approval transaction.
        var readiness = await reservationService.GetDownstreamReadinessAsync(
            requisition.Id, 300m, "GHS");
        readiness.CanReserve.Should().BeTrue();

        context.ChangeTracker.Clear();
        var afterReservation = await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId);
        afterReservation.ReservedAmount.Should().Be(0m);
        afterReservation.CommittedAmount.Should().Be(0m);
        afterReservation.UtilizedAmount.Should().Be(0m);
        afterReservation.RemainingAmount.Should().Be(1000m);
        (await context.ProcurementBudgetCommitments.IgnoreQueryFilters()
            .CountAsync(item => item.PurchaseRequisitionId == requisitionId)).Should().Be(0);
        (await context.ProcurementBudgetCommitmentLedgerEntries.IgnoreQueryFilters()
            .CountAsync(item => item.PurchaseRequisitionId == requisitionId)).Should().Be(0);

        var poOne = NewPurchaseOrder(Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-001", 300m);
        var poTwo = NewPurchaseOrder(Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-002", 400m);
        var overCommittedPo = NewPurchaseOrder(Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-OVER", 1m);
        var contract = new Contract
        {
            Id = contractId, TenantId = tenantId, TenderId = tenderId,
            TenderAwardId = tenderAwardId, BusinessPartnerId = partnerId,
            ContractNumber = "CON-SQL-001", ContractTitle = "SQL commitment contract",
            ContractValue = 300m, Currency = "GHS", Status = "Active",
            CreatedAt = now, CreatedById = actorId
        };
        var contractPo = NewPurchaseOrder(Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-CONTRACT", 250m);
        contractPo.ContractId = contractId;
        var contractPoOver = NewPurchaseOrder(Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-CONTRACT-OVER", 51m);
        contractPoOver.ContractId = contractId;
        var project = new Project
        {
            Id = projectId, TenantId = tenantId, ProjectCode = "PRJ-SQL-001",
            Title = "SQL commitment project", Status = ProjectStatuses.InProgress,
            ContractId = contractId, TenderId = tenderId, DepartmentId = departmentId,
            BaseCurrencyCode = "GHS", CreatedAt = now, CreatedById = actorId
        };
        var certificate = new ProjectPaymentCertificate
        {
            Id = certificateId, TenantId = tenantId, ProjectId = projectId,
            ContractId = contractId, ClientRequestId = Guid.NewGuid(),
            CertificateNumber = "CERT-SQL-001", Title = "SQL contract certificate",
            Status = ProjectPaymentCertificateStatuses.Approved,
            ApprovalStatus = ProjectPaymentCertificateStatuses.Approved,
            Currency = "GHS", GrossCertifiedAmount = 50m, NetCertifiedAmount = 50m,
            IssueDate = now, PreparedAt = now, CreatedAt = now, CreatedById = actorId
        };
        var postReleaseCertificate = new ProjectPaymentCertificate
        {
            Id = postReleaseCertificateId, TenantId = tenantId, ProjectId = projectId,
            ContractId = contractId, ClientRequestId = Guid.NewGuid(),
            CertificateNumber = "CERT-SQL-POST-RELEASE",
            Title = "SQL post-release certificate probe",
            Status = ProjectPaymentCertificateStatuses.Approved,
            ApprovalStatus = ProjectPaymentCertificateStatuses.Approved,
            Currency = "GHS", GrossCertifiedAmount = 1m, NetCertifiedAmount = 1m,
            IssueDate = now, PreparedAt = now, CreatedAt = now, CreatedById = actorId
        };
        foreach (var purchaseOrder in new[] { poOne, poTwo, overCommittedPo, contractPo, contractPoOver })
        {
            purchaseOrder.BusinessPartnerId = partnerId;
            MakeGovernedDraft(purchaseOrder, sourcingRelease.Id);
        }
        var receiptOne = Guid.NewGuid();
        var receiptTwo = Guid.NewGuid();
        var contractPoReceipt = Guid.NewGuid();
        var contractPoFinalReceipt = Guid.NewGuid();
        var contractPoExcessReceipt = Guid.NewGuid();
        context.AddRange(contract, project, certificate, postReleaseCertificate,
            poOne, poTwo, overCommittedPo, contractPo, contractPoOver,
            NewReceipt(receiptOne, tenantId, poOne.Id, "REC-SQL-001", actorId),
            NewReceipt(receiptTwo, tenantId, poTwo.Id, "REC-SQL-002", actorId),
            NewReceipt(contractPoReceipt, tenantId, contractPo.Id, "REC-SQL-CONTRACT", actorId),
            NewReceipt(contractPoFinalReceipt, tenantId, contractPo.Id, "REC-SQL-CONTRACT-FINAL", actorId),
            NewReceipt(contractPoExcessReceipt, tenantId, contractPo.Id, "REC-SQL-CONTRACT-OVER", actorId));
        await unitOfWork.SaveChangesAsync();

        var poOneReservation = await reservationService.ReserveForDownstreamAsync(
            requisition, 300m, "GHS", "procurement.purchase-order.approve", "sql-po-1-reserve");
        poOneReservation.CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        var formalOne = await lifecycle.CommitPurchaseOrderAsync(poOne, "sql-po-1");
        await unitOfWork.SaveChangesAsync();
        poOne.Status = "Approved";
        await unitOfWork.SaveChangesAsync();
        var formalOneReplay = await lifecycle.CommitPurchaseOrderAsync(poOne, "sql-po-1-retry");
        formalOneReplay.Id.Should().Be(formalOne.Id);
        await unitOfWork.SaveChangesAsync();
        var conflictingPoReplay = NewPurchaseOrder(
            poOne.Id, tenantId, requisitionId, poOne.OrderNumber, 301m);
        conflictingPoReplay.BusinessPartnerId = partnerId;
        var conflictingPo = async () => await lifecycle.CommitPurchaseOrderAsync(
            conflictingPoReplay, "sql-po-1-conflict");
        (await conflictingPo.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_COMMITMENT_IDEMPOTENCY_CONFLICT");
        var poTwoReservation = await reservationService.ReserveForDownstreamAsync(
            requisition, 700m, "GHS", "procurement.purchase-order.approve", "sql-po-2-reserve");
        poTwoReservation.CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        await lifecycle.CommitPurchaseOrderAsync(poTwo, "sql-po-2");
        await unitOfWork.SaveChangesAsync();
        poTwo.Status = "Approved";
        await unitOfWork.SaveChangesAsync();

        var contractReservation = await reservationService.ReserveForDownstreamAsync(
            requisition, 1000m, "GHS", "procurement.purchase-order.approve", "sql-contract-reserve");
        contractReservation.CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        var contractFormal = await lifecycle.CommitContractAsync(contract, "sql-contract");
        await unitOfWork.SaveChangesAsync();
        var contractFormalReplay = await lifecycle.CommitContractAsync(contract, "sql-contract-retry");
        contractFormalReplay.Id.Should().Be(contractFormal.Id);
        await unitOfWork.SaveChangesAsync();

        var overCommitReadiness = await reservationService.ReserveForDownstreamAsync(
            requisition, 1001m, "GHS", "procurement.purchase-order.approve", "sql-over-reserve");
        overCommitReadiness.CanReserve.Should().BeFalse();
        var overCommit = async () => await lifecycle.CommitPurchaseOrderAsync(overCommittedPo, "sql-over");
        (await overCommit.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_RESERVATION_EXCEEDED");

        // A child PO under an already committed contract is an allocation only;
        // it must not commit the same GHS exposure a second time. The parent
        // formal commitment remains authoritative after the original budget
        // period closes, so the allocation trigger must not revalidate current
        // budget eligibility.
        var expiredBudget = await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId);
        expiredBudget.Status = "Closed";
        expiredBudget.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        await unitOfWork.SaveChangesAsync();
        var allocation = await lifecycle.CommitPurchaseOrderAsync(contractPo, "sql-contract-po");
        await unitOfWork.SaveChangesAsync();
        contractPo.Status = "Approved";
        await unitOfWork.SaveChangesAsync();
        var allocationReplay = await lifecycle.CommitPurchaseOrderAsync(contractPo, "sql-contract-po-retry");
        allocationReplay.Id.Should().Be(allocation.Id);
        await unitOfWork.SaveChangesAsync();
        var overAllocation = async () => await lifecycle.CommitPurchaseOrderAsync(contractPoOver, "sql-contract-po-over");
        (await overAllocation.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("PO_CONTRACT_COMMITMENT_EXCEEDED");
        var zeroUtilizationClose = async () => await lifecycle.ReleaseUnusedContractAsync(
            contractId, "sql-contract-po-zero-close");
        (await zeroUtilizationClose.Should()
                .ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("CONTRACT_CHILD_PO_ALLOCATION_OUTSTANDING");

        // A source belonging to another tenant must not resolve through the
        // current tenant's lifecycle service.
        var foreignPo = NewPurchaseOrder(Guid.NewGuid(), foreignTenantId,
            requisitionId, "PO-SQL-FOREIGN", 100m);
        var crossTenantCommit = async () => await lifecycle.CommitPurchaseOrderAsync(foreignPo, "sql-foreign-po");
        (await crossTenantCommit.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_LIFECYCLE_SOURCE_TENANT_MISMATCH");
        var foreignContract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = foreignTenantId, TenderId = tenderId,
            ContractNumber = "CON-SQL-FOREIGN", ContractTitle = "Foreign SQL contract",
            ContractValue = 10m, Currency = "GHS", Status = "Active"
        };
        var crossTenantContract = async () => await lifecycle.CommitContractAsync(
            foreignContract, "sql-foreign-contract");
        (await crossTenantContract.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_LIFECYCLE_SOURCE_TENANT_MISMATCH");

        var wrongReceiptLineage = async () => await lifecycle.UtilizePurchaseOrderAsync(
            poOne.Id, receiptTwo, "REC-SQL-002", 1m, "sql-wrong-receipt-lineage");
        (await wrongReceiptLineage.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_UTILIZATION_SOURCE_LINEAGE_INVALID");
        var receiptUtilization = await lifecycle.UtilizePurchaseOrderAsync(
            poOne.Id, receiptOne, "REC-SQL-001", 300m, "sql-receipt-1");
        await unitOfWork.SaveChangesAsync();
        var receiptReplay = await lifecycle.UtilizePurchaseOrderAsync(
            poOne.Id, receiptOne, "REC-SQL-001", 300m, "sql-receipt-1-retry");
        receiptReplay.Id.Should().Be(receiptUtilization.Id);
        await unitOfWork.SaveChangesAsync();
        var conflictingReceipt = async () => await lifecycle.UtilizePurchaseOrderAsync(
            poOne.Id, receiptOne, "REC-SQL-001", 299m, "sql-receipt-1-conflict");
        (await conflictingReceipt.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_UTILIZATION_IDEMPOTENCY_CONFLICT");
        await lifecycle.UtilizePurchaseOrderAsync(
            poTwo.Id, receiptTwo, "REC-SQL-002", 400m, "sql-receipt-2");
        await unitOfWork.SaveChangesAsync();
        await lifecycle.UtilizePurchaseOrderAsync(
            contractPo.Id, contractPoReceipt, "REC-SQL-CONTRACT", 100m, "sql-contract-receipt");
        await unitOfWork.SaveChangesAsync();
        var partialUtilizationClose = async () => await lifecycle.ReleaseUnusedContractAsync(
            contractId, "sql-contract-po-partial-close");
        (await partialUtilizationClose.Should()
                .ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("CONTRACT_CHILD_PO_ALLOCATION_OUTSTANDING");
        await lifecycle.UtilizePurchaseOrderAsync(
            contractPo.Id, contractPoFinalReceipt, "REC-SQL-CONTRACT-FINAL", 150m,
            "sql-contract-receipt-final");
        await unitOfWork.SaveChangesAsync();
        var overChildAllocation = async () => await lifecycle.UtilizePurchaseOrderAsync(
            contractPo.Id, contractPoExcessReceipt, "REC-SQL-CONTRACT-OVER", 1m,
            "sql-contract-receipt-over");
        (await overChildAllocation.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("PO_CONTRACT_ALLOCATION_EXCEEDED");
        var certificateUtilization = await lifecycle.UtilizeContractCertificateAsync(
            contractId, certificateId, "CERT-SQL-001", 25m, "sql-certificate");
        await unitOfWork.SaveChangesAsync();
        var certificateReplay = await lifecycle.UtilizeContractCertificateAsync(
            contractId, certificateId, "CERT-SQL-001", 25m, "sql-certificate-retry");
        certificateReplay.Id.Should().Be(certificateUtilization.Id);
        await unitOfWork.SaveChangesAsync();
        var conflictingCertificate = async () => await lifecycle.UtilizeContractCertificateAsync(
            contractId, certificateId, "CERT-SQL-001", 49m, "sql-certificate-conflict");
        (await conflictingCertificate.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_UTILIZATION_IDEMPOTENCY_CONFLICT");

        // Works completion releases only the unused GHS 25 contract balance.
        // Prove that a later terminal-mutation failure rolls back both Finance
        // and its immutable release evidence before applying the successful
        // completion and replaying it exactly once.
        var transaction = context.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync("BeforeWorksCloseout");
        var rolledBackRelease = await lifecycle.ReleaseUnusedContractAsync(
            contractId, "sql-works-closeout-rollback");
        rolledBackRelease.Should().NotBeNull();
        rolledBackRelease!.Amount.Should().Be(25m);
        await unitOfWork.SaveChangesAsync();
        var rolledBackContract = await context.Contracts.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == contractId);
        rolledBackContract.Status = "Completed";
        await unitOfWork.SaveChangesAsync();
        await transaction.RollbackToSavepointAsync("BeforeWorksCloseout");
        context.ChangeTracker.Clear();
        (await context.ProcurementBudgetCommitmentLedgerEntries.IgnoreQueryFilters()
            .CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceType == "Contract" &&
                item.SourceId == contractId)).Should().Be(0);
        (await context.Contracts.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == contractId)).Status.Should().Be("Active");
        (await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId)).CommittedAmount.Should().Be(25m);

        var contractRelease = await lifecycle.ReleaseUnusedContractAsync(
            contractId, "sql-works-closeout");
        contractRelease.Should().NotBeNull();
        contractRelease!.Amount.Should().Be(25m);
        await unitOfWork.SaveChangesAsync();
        var completedContract = await context.Contracts.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == contractId);
        completedContract.Status = "Completed";
        await unitOfWork.SaveChangesAsync();
        var contractReleaseReplay = await lifecycle.ReleaseUnusedContractAsync(
            contractId, "sql-works-closeout-replay");
        contractReleaseReplay!.Id.Should().Be(contractRelease.Id);
        await unitOfWork.SaveChangesAsync();
        var postReleaseAllocationPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, requisitionId,
            "PO-SQL-CONTRACT-POST-RELEASE", 26m);
        postReleaseAllocationPo.ContractId = contractId;
        postReleaseAllocationPo.BusinessPartnerId = partnerId;
        MakeGovernedDraft(postReleaseAllocationPo, sourcingRelease.Id);
        context.Add(postReleaseAllocationPo);
        await unitOfWork.SaveChangesAsync();
        var allocateReleasedParentCapacity = async () =>
            await lifecycle.CommitPurchaseOrderAsync(
                postReleaseAllocationPo, "sql-contract-post-release-allocation");
        (await allocateReleasedParentCapacity.Should()
                .ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("PO_CONTRACT_COMMITMENT_EXCEEDED");
        var utilizeReleasedContractCapacity = async () =>
            await lifecycle.UtilizeContractCertificateAsync(
                contractId,
                postReleaseCertificateId,
                postReleaseCertificate.CertificateNumber,
                1m,
                "sql-post-release-certificate");
        (await utilizeReleasedContractCapacity.Should()
                .ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("FORMAL_BUDGET_COMMITMENT_EXCEEDED");

        var foreignUtilization = async () => await lifecycle.UtilizePurchaseOrderAsync(
            foreignFormalPoId, Guid.NewGuid(), "REC-SQL-FOREIGN", 100m, "sql-foreign-receipt");
        (await foreignUtilization.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("FORMAL_BUDGET_COMMITMENT_REQUIRED");

        // A fully unused closeout returns Finance capacity but terminalizes
        // this PR envelope. Replacement procurement must start from a newly
        // approved or formally amended requisition; advisory and final paths
        // must agree and the SQL trigger must not permit a free reopen.
        var releasedBudget = NewBudget(
            Guid.NewGuid(), tenantId, departmentId, actorId, "PB-SQL-RELEASED", 100m);
        var releasedRequisition = NewRequisition(
            Guid.NewGuid(), tenantId, actorId, releasedBudget.Id,
            releasedBudget.BudgetCode, "PR-SQL-RELEASED", 100m);
        releasedRequisition.Status = "Approved";
        var releasedTender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            TenderNumber = "TND-SQL-RELEASED", Title = "Unused replacement guard",
            SourcePurchaseRequisitionId = releasedRequisition.Id,
            CreatedAt = now, CreatedById = actorId
        };
        var releasedTenderBid = new TenderBid
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = releasedTender.Id,
            BusinessPartnerId = partnerId, BidNumber = "BID-SQL-RELEASED",
            Status = "Accepted", TotalBidAmount = 100m, Currency = "GHS",
            CreatedAt = now
        };
        var releasedTenderAward = new TenderAward
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = releasedTender.Id,
            TenderBidId = releasedTenderBid.Id, BusinessPartnerId = partnerId,
            OriginalBidAmount = 100m, AwardedAmount = 100m, Currency = "GHS",
            AwardedById = actorId, Status = "ContractSigned", CreatedAt = now
        };
        var fullyUnusedContract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = releasedTender.Id,
            TenderAwardId = releasedTenderAward.Id, BusinessPartnerId = partnerId,
            ContractNumber = "CON-SQL-FULLY-UNUSED",
            ContractTitle = "Fully unused replacement guard",
            ContractValue = 100m, Currency = "GHS", Status = "Active",
            CreatedAt = now, CreatedById = actorId
        };
        context.AddRange(
            releasedBudget, releasedRequisition, releasedTender, releasedTenderBid,
            releasedTenderAward, fullyUnusedContract);
        await unitOfWork.SaveChangesAsync();
        (await reservationService.ReserveForDownstreamAsync(
            releasedRequisition, 100m, "GHS",
            "procurement.contract.approve", "sql-released-reserve"))
            .CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        await lifecycle.CommitContractAsync(
            fullyUnusedContract, "sql-released-contract-commit");
        await unitOfWork.SaveChangesAsync();
        (await lifecycle.ReleaseUnusedContractAsync(
            fullyUnusedContract.Id, "sql-released-contract-close"))!
            .Amount.Should().Be(100m);
        await unitOfWork.SaveChangesAsync();
        var releasedAdvisory = await reservationService.GetDownstreamReadinessAsync(
            releasedRequisition.Id, 50m, "GHS");
        var releasedFinal = await reservationService.ReserveForDownstreamAsync(
            releasedRequisition, 50m, "GHS",
            "procurement.contract.approve", "sql-released-replacement");
        releasedAdvisory.CanReserve.Should().BeFalse();
        releasedAdvisory.DecisionCode.Should().Be("PR_BUDGET_COMMITMENT_RELEASED");
        releasedFinal.CanReserve.Should().BeFalse();
        releasedFinal.DecisionCode.Should().Be("PR_BUDGET_COMMITMENT_RELEASED");
        (await context.ProcurementBudgetCommitments.IgnoreQueryFilters()
            .SingleAsync(item =>
                item.PurchaseRequisitionId == releasedRequisition.Id))
            .Status.Should().Be(ProcurementBudgetCommitmentStatus.Released);

        // A generic no-exposure PR release must zero its aggregate under the
        // exact release session context; the lifecycle trigger rejects an
        // unauthenticated Reserved -> Released amount mutation.
        var prReleaseBudget = NewBudget(
            Guid.NewGuid(), tenantId, departmentId, actorId, "PB-SQL-PR-RELEASE", 80m);
        var prReleaseRequisition = NewRequisition(
            Guid.NewGuid(), tenantId, actorId, prReleaseBudget.Id,
            prReleaseBudget.BudgetCode, "PR-SQL-GENERIC-RELEASE", 80m);
        prReleaseRequisition.Status = "Approved";
        context.AddRange(prReleaseBudget, prReleaseRequisition);
        await unitOfWork.SaveChangesAsync();
        (await reservationService.ReserveForDownstreamAsync(
            prReleaseRequisition, 80m, "GHS",
            "procurement.purchase-order.approve", "sql-pr-release-reserve"))
            .CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        (await reservationService.ReleaseAsync(
            prReleaseRequisition,
            "Approved requisition withdrawn before downstream exposure.",
            "procurement.requisition.approve",
            "sql-pr-release"))
            .Released.Should().BeTrue();
        var prReleasedCommitment = await context.ProcurementBudgetCommitments
            .SingleAsync(item => item.PurchaseRequisitionId == prReleaseRequisition.Id);
        prReleasedCommitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Released);
        prReleasedCommitment.ReservedAmount.Should().Be(0m);
        prReleasedCommitment.FormallyCommittedAmount.Should().Be(0m);
        prReleasedCommitment.BudgetReservedAfter.Should().Be(0m);
        prReleaseBudget.ReservedAmount.Should().Be(0m);

        // Retain one ordinary reservation through commit so each direct SQL
        // lifecycle-guard probe can run in its own autocommit statement. A
        // trigger THROW may abort its ambient transaction, so running these
        // probes inside the lifecycle setup transaction would invalidate all
        // later assertions instead of independently certifying each guard.
        var mutationProbeBudget = NewBudget(
            Guid.NewGuid(), tenantId, departmentId, actorId, "PB-SQL-MUTATION-PROBE", 80m);
        var mutationProbeRequisition = NewRequisition(
            Guid.NewGuid(), tenantId, actorId, mutationProbeBudget.Id,
            mutationProbeBudget.BudgetCode, "PR-SQL-MUTATION-PROBE", 80m);
        mutationProbeRequisition.Status = "Approved";
        var mutationProbeRelease = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PurchaseRequisitionId = mutationProbeRequisition.Id, AttemptNumber = 1,
            ReleaseReference = "PR-SQL-MUTATION-PROBE/REL/A1",
            ReleasedAtUtc = now, ReleasedById = actorId,
            ReleasedByName = "Procurement SQL Actor",
            ReleaseReason = "SQL lifecycle mutation guard verification",
            CorrelationId = "sql-mutation-probe-release",
            ControlFingerprint = new string('c', 64),
            SnapshotJson = "{}", IntegrityHash = new string('d', 64),
            CreatedAt = now, CreatedById = actorId
        };
        context.AddRange(mutationProbeBudget, mutationProbeRequisition);
        await unitOfWork.SaveChangesAsync();
        (await reservationService.ReserveForDownstreamAsync(
            mutationProbeRequisition, 80m, "GHS",
            "procurement.purchase-order.approve", "sql-mutation-probe-reserve"))
            .CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        var mutationProbeCommitment = await context.ProcurementBudgetCommitments
            .SingleAsync(item =>
                item.PurchaseRequisitionId == mutationProbeRequisition.Id);
        var mutationProbeCommitmentId = mutationProbeCommitment.Id;
        mutationProbeRelease.BudgetCommitmentId = mutationProbeCommitment.Id;
        mutationProbeRelease.BudgetCommitmentReference =
            mutationProbeCommitment.ReservationReference;
        context.Add(mutationProbeRelease);
        await unitOfWork.SaveChangesAsync();

        var frameworkProfile = new ProcurementConfigurationProfile
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            ProfileCode = "PROC-SQL-FRAMEWORK", Name = "SQL framework probe profile",
            Version = 1, EffectiveFrom = now.AddDays(-1), CreatedAt = now
        };
        var frameworkPolicy = new ProcurementPolicySet
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Code = "PROC-SQL-FRAMEWORK", Name = "SQL framework probe policy",
            Version = 1, SourceConfigurationProfileId = frameworkProfile.Id,
            DefaultCurrencyCode = "GHS", EffectiveFrom = now.AddDays(-1),
            CreatedAt = now
        };
        var frameworkMethodRule = new ProcurementPolicyMethodRule
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PolicySetId = frameworkPolicy.Id,
            RuleCode = "METHOD-RFQ-SQL", Name = "SQL framework RFQ method",
            Category = ProcurementCategoryClass.Goods,
            Method = ProcurementMethodType.RequestForQuotation,
            IsAllowed = true, RequiresCompetition = true, MinimumQuotationCount = 1,
            SourceDecisionKey = "DEC-001", IsEnabled = true,
            EffectiveFrom = now.AddDays(-1), CreatedAt = now
        };
        var frameworkThresholdRule = new ProcurementPolicyThresholdRule
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PolicySetId = frameworkPolicy.Id,
            RuleCode = "THRESHOLD-RFQ-SQL", Name = "SQL framework threshold",
            Category = ProcurementCategoryClass.Goods,
            Method = ProcurementMethodType.RequestForQuotation,
            CurrencyCode = "GHS", LowerBound = 0m, UpperBound = 80m,
            StatutoryReference = "SQL framework trigger certification",
            SourceDecisionKey = "DEC-001", IsEnabled = true,
            EffectiveFrom = now.AddDays(-1), CreatedAt = now
        };
        var frameworkSourcingCase = new ProcurementSourcingCase
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PurchaseRequisitionId = mutationProbeRequisition.Id,
            SourcingReleaseId = mutationProbeRelease.Id,
            CaseSequence = 1, CaseNumber = "CASE-SQL-FRAMEWORK",
            Category = ProcurementCategoryClass.Goods,
            RecommendedMethod = ProcurementMethodType.RequestForQuotation,
            SelectedMethod = ProcurementMethodType.RequestForQuotation,
            MethodSelectionBasis = ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation,
            EstimatedValue = 80m, CurrencyCode = "GHS",
            PolicySetId = frameworkPolicy.Id, PolicyCode = frameworkPolicy.Code,
            PolicyVersion = frameworkPolicy.Version,
            MethodRuleId = frameworkMethodRule.Id,
            MethodRuleCode = frameworkMethodRule.RuleCode,
            ThresholdRuleId = frameworkThresholdRule.Id,
            ThresholdRuleCode = frameworkThresholdRule.RuleCode,
            Justification = "SQL Server framework commitment trigger certification.",
            Status = ProcurementSourcingCaseStatus.Ready,
            CreatedByName = "Procurement SQL Actor",
            SourceControlFingerprint = new string('e', 64),
            CaseFingerprint = new string('f', 64), SnapshotJson = "{}",
            IntegrityHash = new string('1', 64), CreatedAt = now, CreatedById = actorId
        };
        var frameworkReadiness = new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            SourceType = ProcurementAwardReadinessSourceType.RequestForQuotation,
            SourceId = frameworkSourcingCase.Id,
            SourceReference = frameworkSourcingCase.CaseNumber,
            Method = ProcurementMethodType.RequestForQuotation,
            DecisionSequence = 1, Status = ProcurementAwardReadinessDecisionStatus.Ready,
            RecommendationSubjectType = "BusinessPartner",
            RecommendedBusinessPartnerIdsJson = $"[\"{partnerId}\"]",
            SourceIntegrityHash = new string('2', 64),
            IntegrityHash = new string('3', 64),
            IdempotencyKey = "sql-framework-readiness",
            CorrelationId = "sql-framework-readiness",
            EvaluatedAtUtc = now, EvaluatedByUserId = actorId,
            EvaluatedByName = "Procurement SQL Actor",
            CreatedAt = now, CreatedById = actorId
        };
        context.AddRange(
            frameworkProfile, frameworkPolicy, frameworkMethodRule,
            frameworkThresholdRule, frameworkSourcingCase, frameworkReadiness);
        await unitOfWork.SaveChangesAsync();

        var wrongAmountPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id,
            "PO-SQL-WRONG-AMOUNT", 2m);
        var wrongCurrencyPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id,
            "PO-SQL-WRONG-CURRENCY", 2m);
        var frameworkFinalPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id,
            "PO-SQL-FRAMEWORK-FINAL", 2m);
        foreach (var governedProbe in new[]
                 {
                     wrongAmountPo, wrongCurrencyPo, frameworkFinalPo
                 })
        {
            governedProbe.BusinessPartnerId = partnerId;
            MakeGovernedDraft(governedProbe, mutationProbeRelease.Id);
        }
        frameworkFinalPo.ProcurementSourceType =
            ProcurementPurchaseOrderSourceType.FrameworkCallOff;
        frameworkFinalPo.SourcingCaseId = frameworkSourcingCase.Id;
        frameworkFinalPo.AwardReadinessDecisionId = frameworkReadiness.Id;
        context.AddRange(wrongAmountPo, wrongCurrencyPo, frameworkFinalPo);
        await unitOfWork.SaveChangesAsync();
        await lifecycle.CommitPurchaseOrderAsync(
            wrongAmountPo, "sql-wrong-amount-formal");
        await unitOfWork.SaveChangesAsync();
        await lifecycle.CommitPurchaseOrderAsync(
            wrongCurrencyPo, "sql-wrong-currency-formal");
        await unitOfWork.SaveChangesAsync();
        await lifecycle.CommitPurchaseOrderAsync(
            frameworkFinalPo, "sql-framework-formal");
        await unitOfWork.SaveChangesAsync();
        frameworkFinalPo.Status = "Approved";
        await unitOfWork.SaveChangesAsync();

        // Full utilization alone keeps the shared envelope active. The
        // explicit governed close is the terminal event and remains replay-safe
        // without inventing a zero-value Release ledger row.
        var fullCloseBudget = NewBudget(
            Guid.NewGuid(), tenantId, departmentId, actorId, "PB-SQL-FULL-CLOSE", 100m);
        var fullCloseRequisition = NewRequisition(
            Guid.NewGuid(), tenantId, actorId, fullCloseBudget.Id,
            fullCloseBudget.BudgetCode, "PR-SQL-FULL-CLOSE", 100m);
        fullCloseRequisition.Status = "Approved";
        var fullCloseTender = new Tender
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            TenderNumber = "TND-SQL-FULL-CLOSE", Title = "Fully utilized close proof",
            SourcePurchaseRequisitionId = fullCloseRequisition.Id,
            CreatedAt = now, CreatedById = actorId
        };
        var fullCloseTenderBid = new TenderBid
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = fullCloseTender.Id,
            BusinessPartnerId = partnerId, BidNumber = "BID-SQL-FULL-CLOSE",
            Status = "Accepted", TotalBidAmount = 100m, Currency = "GHS",
            CreatedAt = now
        };
        var fullCloseTenderAward = new TenderAward
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = fullCloseTender.Id,
            TenderBidId = fullCloseTenderBid.Id, BusinessPartnerId = partnerId,
            OriginalBidAmount = 100m, AwardedAmount = 100m, Currency = "GHS",
            AwardedById = actorId, Status = "ContractSigned", CreatedAt = now
        };
        var fullCloseContract = new Contract
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TenderId = fullCloseTender.Id,
            TenderAwardId = fullCloseTenderAward.Id, BusinessPartnerId = partnerId,
            ContractNumber = "CON-SQL-FULL-CLOSE",
            ContractTitle = "Fully utilized close proof",
            ContractValue = 100m, Currency = "GHS", Status = "Active",
            CreatedAt = now, CreatedById = actorId
        };
        var fullCloseProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            ProjectCode = "PRJ-SQL-FULL-CLOSE", Title = "Fully utilized close proof",
            Status = ProjectStatuses.InProgress, ContractId = fullCloseContract.Id,
            TenderId = fullCloseTender.Id, DepartmentId = departmentId,
            BaseCurrencyCode = "GHS", CreatedAt = now, CreatedById = actorId
        };
        var fullCloseCertificate = new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = fullCloseProject.Id,
            ContractId = fullCloseContract.Id, ClientRequestId = Guid.NewGuid(),
            CertificateNumber = "CERT-SQL-FULL-CLOSE", Title = "Full utilization",
            Currency = "GHS", GrossCertifiedAmount = 100m,
            CreatedAt = now, CreatedById = actorId
        };
        context.AddRange(
            fullCloseBudget, fullCloseRequisition, fullCloseTender,
            fullCloseTenderBid, fullCloseTenderAward, fullCloseContract,
            fullCloseProject, fullCloseCertificate);
        await unitOfWork.SaveChangesAsync();
        (await reservationService.ReserveForDownstreamAsync(
            fullCloseRequisition, 100m, "GHS",
            "procurement.contract.approve", "sql-full-close-reserve"))
            .CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        await lifecycle.CommitContractAsync(fullCloseContract, "sql-full-close-commit");
        await unitOfWork.SaveChangesAsync();
        await lifecycle.UtilizeContractCertificateAsync(
            fullCloseContract.Id, fullCloseCertificate.Id,
            fullCloseCertificate.CertificateNumber, 100m,
            "sql-full-close-utilize");
        await unitOfWork.SaveChangesAsync();
        var fullCloseCommitment = await context.ProcurementBudgetCommitments
            .SingleAsync(item => item.PurchaseRequisitionId == fullCloseRequisition.Id);
        fullCloseCommitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Reserved);
        (await lifecycle.ReleaseUnusedContractAsync(
            fullCloseContract.Id, "sql-full-close")).Should().BeNull();
        fullCloseCommitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Consumed);
        fullCloseCommitment.ConsumedAtUtc.Should().NotBeNull();
        (await lifecycle.ReleaseUnusedContractAsync(
            fullCloseContract.Id, "sql-full-close-replay")).Should().BeNull();
        (await context.ProcurementBudgetCommitmentLedgerEntries.CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release &&
                item.SourceId == fullCloseContract.Id))
            .Should().Be(0);

        await unitOfWork.CommitAsync();
        context.ChangeTracker.Clear();

        var finalBudget = await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId);
        finalBudget.ReservedAmount.Should().Be(0m);
        finalBudget.CommittedAmount.Should().Be(0m);
        finalBudget.UtilizedAmount.Should().Be(975m);
        finalBudget.RemainingAmount.Should().Be(25m);
        var finalCommitment = await context.ProcurementBudgetCommitments.IgnoreQueryFilters()
            .SingleAsync(item => item.PurchaseRequisitionId == requisitionId);
        finalCommitment.ReservedAmount.Should().Be(975m);
        finalCommitment.FormallyCommittedAmount.Should().Be(975m);
        finalCommitment.UtilizedAmount.Should().Be(975m);
        finalCommitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Consumed);
        finalCommitment.ConsumedAtUtc.Should().NotBeNull();
        var ledger = await context.ProcurementBudgetCommitmentLedgerEntries.IgnoreQueryFilters()
            .Where(item => item.PurchaseRequisitionId == requisitionId).ToListAsync();
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment)
            .Should().Be(3);
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation)
            .Should().Be(1);
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization)
            .Should().Be(5);
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release)
            .Should().Be(1);
        ledger.Where(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment)
            .Sum(item => item.Amount).Should().Be(1000m);
        ledger.Where(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization)
            .Sum(item => item.Amount).Should().Be(975m);

        async Task AssertLifecycleGuardAsync(string sql, params SqlParameter[] parameters)
        {
            var mutation = async () => await database.ExecuteAsync(sql, parameters);
            (await mutation.Should().ThrowAsync<SqlException>())
                .Which.Number.Should().Be(51022);
        }

        await AssertLifecycleGuardAsync(
            "UPDATE dbo.ProcurementBudgetCommitments SET Status=2 WHERE Id=@id;",
            new SqlParameter("@id", mutationProbeCommitmentId));
        await AssertLifecycleGuardAsync(
            "UPDATE dbo.ProcurementBudgetCommitments SET Status=3 WHERE Id=@id;",
            new SqlParameter("@id", mutationProbeCommitmentId));
        await AssertLifecycleGuardAsync(
            "UPDATE dbo.ProcurementBudgetCommitments SET FormallyCommittedAmount=FormallyCommittedAmount+1 WHERE Id=@id;",
            new SqlParameter("@id", mutationProbeCommitmentId));
        await AssertLifecycleGuardAsync(
            "UPDATE dbo.ProcurementBudgetCommitments SET UtilizedAmount=UtilizedAmount+1 WHERE Id=@id;",
            new SqlParameter("@id", mutationProbeCommitmentId));

        const string reservationPiggybackSql = """
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_TENANT_ID', @value=@tenant;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_REQUISITION_ID', @value=@requisition;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_COMMITMENT_ID', @value=@commitment;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_BEFORE', @value=80;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_AFTER', @value=81;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_BEFORE', @value=1;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_AFTER', @value=2;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_DOWNSTREAM_RESERVATION_CORRELATION_ID', @value=N'sql-reservation-aggregate-piggyback';
            UPDATE dbo.ProcurementBudgetCommitments
            SET ReservedAmount=81, FormallyCommittedAmount=1, UtilizedAmount=1,
                ReservationSequence=2, CorrelationId=N'sql-reservation-aggregate-piggyback'
            WHERE Id=@commitment;
            """;
        await AssertLifecycleGuardAsync(
            reservationPiggybackSql,
            new SqlParameter("@tenant", tenantId),
            new SqlParameter("@requisition", mutationProbeRequisition.Id),
            new SqlParameter("@commitment", mutationProbeCommitmentId));

        const string releasePiggybackSql = """
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_TENANT_ID', @value=@tenant;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_REQUISITION_ID', @value=@requisition;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_COMMITMENT_ID', @value=@commitment;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_AMOUNT_BEFORE', @value=80;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_SEQUENCE', @value=1;
            EXEC sys.sp_set_session_context @key=N'PROCUREMENT_REQUISITION_RELEASE_CORRELATION_ID', @value=N'sql-release-aggregate-piggyback';
            UPDATE dbo.ProcurementBudgetCommitments
            SET ReservedAmount=0, FormallyCommittedAmount=1, UtilizedAmount=1,
                Status=2, CorrelationId=N'sql-release-aggregate-piggyback'
            WHERE Id=@commitment;
            """;
        await AssertLifecycleGuardAsync(
            releasePiggybackSql,
            new SqlParameter("@tenant", tenantId),
            new SqlParameter("@requisition", mutationProbeRequisition.Id),
            new SqlParameter("@commitment", mutationProbeCommitmentId));

        // Certify the production SQL hard stop itself. Each probe begins in
        // Draft so only the transition into final exposure invokes 52041.
        var missingLedgerPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id, "PO-SQL-NO-LEDGER", 2m);
        var nullSourcePo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id, "PO-SQL-NULL-SOURCE", 2m);
        var foreignCommitmentPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id, "PO-SQL-FOREIGN-COMMIT", 2m);
        var frameworkMissingLedgerPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id,
            "PO-SQL-FRAMEWORK-NO-LEDGER", 2m);
        var overParentPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-CONTRACT-OVER-TRIGGER", 51m);
        overParentPo.ContractId = contractId;
        foreach (var probe in new[]
                 {
                      missingLedgerPo, nullSourcePo, foreignCommitmentPo,
                      frameworkMissingLedgerPo
                  })
        {
            probe.BusinessPartnerId = partnerId;
            MakeGovernedDraft(probe, mutationProbeRelease.Id);
        }
        overParentPo.BusinessPartnerId = partnerId;
        MakeGovernedDraft(overParentPo, sourcingRelease.Id);
        foreach (var frameworkProbe in new[]
                 {
                     frameworkMissingLedgerPo
                 })
        {
            frameworkProbe.ProcurementSourceType =
                ProcurementPurchaseOrderSourceType.FrameworkCallOff;
            frameworkProbe.SourcingCaseId = frameworkSourcingCase.Id;
            frameworkProbe.AwardReadinessDecisionId = frameworkReadiness.Id;
        }
        nullSourcePo.ProcurementSourceType = null;

        ProcurementBudgetCommitmentLedgerEntry Exposure(
            PurchaseOrder po,
            ProcurementBudgetCommitment owner,
            Guid ownerBudgetId,
            Guid ownerRequisitionId,
            decimal amount,
            string currency,
            ProcurementBudgetCommitmentLedgerEntryType entryType,
            Guid? parentId = null) => new()
        {
            Id = Guid.NewGuid(), TenantId = owner.TenantId,
            ProcurementBudgetCommitmentId = owner.Id,
            ProcurementBudgetId = ownerBudgetId,
            PurchaseRequisitionId = ownerRequisitionId,
            EntryType = entryType,
            SourceType = "PurchaseOrder", SourceId = po.Id,
            SourceReference = po.OrderNumber,
            Amount = amount, Currency = currency,
            FormalCommitmentEntryId = parentId,
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = owner.TenantId == tenantId ? actorId : foreignActorId,
            ActorName = "SQL trigger probe",
            CorrelationId = $"sql-trigger-{po.Id:N}",
            CreatedAt = DateTime.UtcNow
        };

        context.AddRange(
            missingLedgerPo, nullSourcePo, foreignCommitmentPo,
            frameworkMissingLedgerPo,
            overParentPo,
            Exposure(foreignCommitmentPo, foreignCommitment, foreignBudgetId, foreignRequisitionId,
                2m, "GHS", ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment),
            Exposure(overParentPo, finalCommitment, budgetId, requisitionId,
                51m, "GHS", ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation,
                contractFormal.Id));
        await context.SaveChangesAsync();

        // The valid 250 allocation above succeeded while its budget was closed
        // and expired. This additional 51 allocation must still fail because
        // effective children exceed the parent's net 275 after its immutable
        // 25 release, even if a stale external update leaves the contract Active.
        await database.ExecuteAsync(
            "UPDATE dbo.Contracts SET Status=N'Active' WHERE Id=@id;",
            new SqlParameter("@id", contractId));
        var overParentTransition = async () => await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Approved' WHERE Id=@id;",
            new SqlParameter("@id", overParentPo.Id));
        (await overParentTransition.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(52041);
        await database.ExecuteAsync(
            "UPDATE dbo.Contracts SET Status=N'Completed' WHERE Id=@id;",
            new SqlParameter("@id", contractId));

        // Each draft was formally projected through the real lifecycle. These
        // post-projection mutations isolate the per-PO amount/currency checks
        // without corrupting the aggregate projection itself.
        await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET TotalAmount=3 WHERE Id=@id;",
            new SqlParameter("@id", wrongAmountPo.Id));
        await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Currency=N'USD' WHERE Id=@id;",
            new SqlParameter("@id", wrongCurrencyPo.Id));

        foreach (var invalidPo in new[]
                 {
                      missingLedgerPo, nullSourcePo, wrongAmountPo, wrongCurrencyPo,
                      foreignCommitmentPo, frameworkMissingLedgerPo
                  })
        {
            var transition = async () => await database.ExecuteAsync(
                "UPDATE dbo.PurchaseOrders SET Status=N'Approved' WHERE Id=@id;",
                new SqlParameter("@id", invalidPo.Id));
            (await transition.Should().ThrowAsync<SqlException>())
                .Which.Number.Should().Be(52041);
        }

        // The dedicated source-4 route used the real lifecycle projection and
        // protects that final exposure from a generic cancellation.
        var finalExitWithoutReversal = async () => await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Cancelled' WHERE Id=@id;",
            new SqlParameter("@id", frameworkFinalPo.Id));
        (await finalExitWithoutReversal.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(52041);

        // Non-exposure workflow exits remain ordinary status transitions.
        await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Cancelled' WHERE Id=@id;",
            new SqlParameter("@id", missingLedgerPo.Id));
        await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Pending Approval' WHERE Id=@id;",
            new SqlParameter("@id", wrongAmountPo.Id));
        await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Rejected' WHERE Id=@id;",
            new SqlParameter("@id", wrongAmountPo.Id));

        // A different PR under the same Finance budget cannot borrow the
        // already-projected committed balance of the first PR. The second
        // aggregate and immutable Formal row are internally exact, but the
        // omitted budget increment must still block final exposure.
        var unprojectedSiblingRequisition = NewRequisition(
            Guid.NewGuid(), tenantId, actorId, mutationProbeBudget.Id,
            mutationProbeBudget.BudgetCode,
            "PR-SQL-UNPROJECTED-SIBLING", 2m);
        unprojectedSiblingRequisition.Status = "Approved";
        var unprojectedSiblingCommitment = NewCommitment(
            Guid.NewGuid(), tenantId, mutationProbeBudget,
            unprojectedSiblingRequisition, actorId, 2m, DateTime.UtcNow);
        unprojectedSiblingCommitment.FormallyCommittedAmount = 2m;
        var unprojectedSiblingRelease = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PurchaseRequisitionId = unprojectedSiblingRequisition.Id,
            AttemptNumber = 1,
            ReleaseReference = "PR-SQL-UNPROJECTED-SIBLING/REL/A1",
            ReleasedAtUtc = DateTime.UtcNow, ReleasedById = actorId,
            ReleasedByName = "Procurement SQL Actor",
            ReleaseReason = "SQL budget-wide projection trigger verification",
            CorrelationId = "sql-unprojected-sibling-release",
            ControlFingerprint = new string('6', 64),
            SnapshotJson = "{}", IntegrityHash = new string('7', 64),
            BudgetCommitmentId = unprojectedSiblingCommitment.Id,
            BudgetCommitmentReference = unprojectedSiblingCommitment.ReservationReference,
            CreatedAt = DateTime.UtcNow, CreatedById = actorId
        };
        var unprojectedSiblingPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, unprojectedSiblingRequisition.Id,
            "PO-SQL-UNPROJECTED-SIBLING", 2m);
        unprojectedSiblingPo.BusinessPartnerId = partnerId;
        MakeGovernedDraft(unprojectedSiblingPo, unprojectedSiblingRelease.Id);
        context.AddRange(
            unprojectedSiblingRequisition, unprojectedSiblingCommitment,
            unprojectedSiblingRelease, unprojectedSiblingPo,
            Exposure(unprojectedSiblingPo, unprojectedSiblingCommitment,
                mutationProbeBudget.Id, unprojectedSiblingRequisition.Id,
                2m, "GHS",
                ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment));
        await context.SaveChangesAsync();
        var unprojectedSiblingTransition = async () => await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Approved' WHERE Id=@id;",
            new SqlParameter("@id", unprojectedSiblingPo.Id));
        (await unprojectedSiblingTransition.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(52041);

        // A per-PO formal row that was never projected into the aggregate and
        // Finance budget cannot authorize final exposure.
        var rogueProjectionPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, mutationProbeRequisition.Id,
            "PO-SQL-ROGUE-PROJECTION", 2m);
        rogueProjectionPo.BusinessPartnerId = partnerId;
        MakeGovernedDraft(rogueProjectionPo, mutationProbeRelease.Id);
        context.AddRange(
            rogueProjectionPo,
            Exposure(rogueProjectionPo, mutationProbeCommitment,
                mutationProbeBudget.Id, mutationProbeRequisition.Id,
                2m, "GHS", ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment));
        await context.SaveChangesAsync();
        var rogueProjectionTransition = async () => await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Approved' WHERE Id=@id;",
            new SqlParameter("@id", rogueProjectionPo.Id));
        (await rogueProjectionTransition.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(52041);

        // Amendment re-entry must also require a live aggregate projection.
        // A soft-deleted commitment remains a valid FK target for immutable
        // release lineage, but it cannot authorize a final PO exposure.
        var deletedProjectionBudget = NewBudget(
            Guid.NewGuid(), tenantId, departmentId, actorId,
            "PB-SQL-DELETED-PROJECTION", 10m);
        var deletedProjectionRequisition = NewRequisition(
            Guid.NewGuid(), tenantId, actorId, deletedProjectionBudget.Id,
            deletedProjectionBudget.BudgetCode,
            "PR-SQL-DELETED-PROJECTION", 10m);
        deletedProjectionRequisition.Status = "Approved";
        var deletedProjectionCommitment = NewCommitment(
            Guid.NewGuid(), tenantId, deletedProjectionBudget,
            deletedProjectionRequisition, actorId, 10m, DateTime.UtcNow);
        deletedProjectionCommitment.IsDeleted = true;
        deletedProjectionCommitment.DeletedAt = DateTime.UtcNow;
        var deletedProjectionRelease = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PurchaseRequisitionId = deletedProjectionRequisition.Id,
            AttemptNumber = 1,
            ReleaseReference = "PR-SQL-DELETED-PROJECTION/REL/A1",
            ReleasedAtUtc = DateTime.UtcNow, ReleasedById = actorId,
            ReleasedByName = "Procurement SQL Actor",
            ReleaseReason = "SQL deleted projection trigger verification",
            CorrelationId = "sql-deleted-projection-release",
            ControlFingerprint = new string('4', 64),
            SnapshotJson = "{}", IntegrityHash = new string('5', 64),
            BudgetCommitmentId = deletedProjectionCommitment.Id,
            BudgetCommitmentReference = deletedProjectionCommitment.ReservationReference,
            CreatedAt = DateTime.UtcNow, CreatedById = actorId
        };
        var amendmentReentryPo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, deletedProjectionRequisition.Id,
            "PO-SQL-AMENDMENT-DELETED-PROJECTION", 10m);
        amendmentReentryPo.BusinessPartnerId = partnerId;
        MakeGovernedDraft(amendmentReentryPo, deletedProjectionRelease.Id);
        amendmentReentryPo.Status = "Amendment Pending Approval";
        context.AddRange(
            deletedProjectionBudget, deletedProjectionRequisition,
            deletedProjectionCommitment, deletedProjectionRelease,
            amendmentReentryPo,
            Exposure(amendmentReentryPo, deletedProjectionCommitment,
                deletedProjectionBudget.Id, deletedProjectionRequisition.Id,
                10m, "GHS",
                ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment));
        await context.SaveChangesAsync();
        var amendmentReentryWithoutProjection = async () => await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrders SET Status=N'Approved' WHERE Id=@id;",
            new SqlParameter("@id", amendmentReentryPo.Id));
        (await amendmentReentryWithoutProjection.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(52041);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task AppliedPoAmendmentUsesExactContextAndNetReleasedExposureWhilePreservingSiblingReservation()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync(string.Empty);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var commitmentId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var sourcingCaseId = Guid.NewGuid();
        var readinessId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var inventoryItemId = Guid.NewGuid();
        var workflowEntityTypeId = Guid.NewGuid();
        var workflowDefinitionId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        await context.Database.EnsureCreatedAsync();
        await database.ApplySqlOperationsAsync(
            new EnforceAtomicPurchaseOrderBudgetCommitment());

        var budget = NewBudget(
            budgetId, tenantId, departmentId, actorId, "PB-SQL-AMEND", 200m);
        budget.CommittedAmount = 100m;
        budget.ReservedAmount = 0m;
        budget.RemainingAmount = 100m;
        var requisition = NewRequisition(
            requisitionId, tenantId, actorId, budgetId,
            budget.BudgetCode, "PR-SQL-AMEND", 150m);
        requisition.Status = "Approved";
        var commitment = new ProcurementBudgetCommitment
        {
            Id = commitmentId,
            TenantId = tenantId,
            ProcurementBudgetId = budgetId,
            PurchaseRequisitionId = requisitionId,
            ReservationReference = "BCR-PR-SQL-AMEND",
            ReservationSequence = 1,
            Status = ProcurementBudgetCommitmentStatus.Reserved,
            ReservedAmount = 100m,
            FormallyCommittedAmount = 100m,
            Currency = "GHS",
            BudgetAllocatedSnapshot = 200m,
            BudgetCommittedAfter = 100m,
            BudgetReservedAfter = 0m,
            BudgetAvailableAfter = 100m,
            ReservedAtUtc = now,
            ReservedById = actorId,
            ReservedByName = "SQL amendment actor",
            CorrelationId = "sql-amend-reservation",
            CreatedAt = now,
            CreatedById = actorId
        };
        var sourcingRelease = new ProcurementRequisitionSourcingRelease
        {
            Id = releaseId,
            TenantId = tenantId,
            PurchaseRequisitionId = requisitionId,
            AttemptNumber = 1,
            ReleaseReference = "PR-SQL-AMEND/REL/A1",
            ReleasedAtUtc = now,
            ReleasedById = actorId,
            ReleasedByName = "SQL amendment actor",
            ReleaseReason = "SQL amendment lifecycle proof",
            BudgetCommitmentId = commitmentId,
            BudgetCommitmentReference = commitment.ReservationReference,
            CorrelationId = "sql-amend-release",
            ControlFingerprint = new string('a', 64),
            SnapshotJson = "{}",
            IntegrityHash = new string('b', 64),
            CreatedAt = now,
            CreatedById = actorId
        };
        var supplier = new BusinessPartner
        {
            Id = supplierId,
            TenantId = tenantId,
            PartnerCode = "SUP-SQL-AMEND",
            PartnerName = "SQL Amendment Supplier",
            PartnerType = "Supplier",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            ApprovedById = actorId,
            ApprovedDate = now,
            Currency = "GHS",
            IsActive = true,
            CreatedAt = now
        };
        var inventoryCategory = new InventoryCategory
        {
            Id = categoryId,
            TenantId = tenantId,
            Code = "SQL-AMEND",
            Name = "SQL Amendment Items",
            CreatedAt = now
        };
        var inventoryItem = new InventoryItem
        {
            Id = inventoryItemId,
            TenantId = tenantId,
            ItemCode = "SQL-AMEND-ITEM",
            Name = "SQL amendment item",
            CategoryId = categoryId,
            UnitOfMeasure = "EA",
            Status = ItemStatus.Active,
            CreatedAt = now
        };
        var poOne = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-AMEND-1", 100m);
        var poTwo = NewPurchaseOrder(
            Guid.NewGuid(), tenantId, requisitionId, "PO-SQL-AMEND-2", 30m);
        foreach (var po in new[] { poOne, poTwo })
        {
            MakeGovernedDraft(po, releaseId);
            po.BusinessPartnerId = supplierId;
            po.ProcurementSourceId = sourceId;
            po.ProcurementSourceReference = "RFQ-SQL-AMEND";
            // Direct RFQ award lineage is represented by source/release; the
            // schema requires both case and readiness to be null for type 0.
            po.SourcingCaseId = null;
            po.AwardReadinessDecisionId = null;
            po.RequiredDate = now.Date.AddDays(5);
            po.PromisedDate = now.Date.AddDays(7);
        }
        poOne.RevisionNumber = 1;
        poTwo.ProcurementSourceId = Guid.NewGuid();
        poTwo.ProcurementSourceReference = "RFQ-SQL-AMEND-2";
        var poOneItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = poOne.Id,
            InventoryItemId = inventoryItemId,
            ItemDescription = "SQL amendment item",
            OrderedQuantity = 10m,
            RemainingQuantity = 10m,
            UnitOfMeasure = "EA",
            UnitPrice = 10m,
            LineTotal = 100m,
            CreatedAt = now
        };
        var directFormal = new ProcurementBudgetCommitmentLedgerEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            ProcurementBudgetCommitmentId = commitmentId,
            ProcurementBudgetId = budgetId,
            PurchaseRequisitionId = requisitionId,
            EntryType = ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            SourceType = "PurchaseOrder", SourceId = poOne.Id,
            SourceReference = poOne.OrderNumber, Amount = 100m,
            Currency = "GHS", OccurredAtUtc = now, ActorUserId = actorId,
            ActorName = "SQL amendment actor", CorrelationId = "sql-amend-po1",
            CreatedAt = now, CreatedById = actorId
        };
        var releasedContractId = Guid.NewGuid();
        var contractFormal = new ProcurementBudgetCommitmentLedgerEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            ProcurementBudgetCommitmentId = commitmentId,
            ProcurementBudgetId = budgetId,
            PurchaseRequisitionId = requisitionId,
            EntryType = ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment,
            SourceType = "Contract", SourceId = releasedContractId,
            SourceReference = "CON-SQL-RELEASED", Amount = 40m,
            Currency = "GHS", OccurredAtUtc = now, ActorUserId = actorId,
            ActorName = "SQL amendment actor", CorrelationId = "sql-contract-formal",
            CreatedAt = now, CreatedById = actorId
        };
        var contractRelease = new ProcurementBudgetCommitmentLedgerEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            ProcurementBudgetCommitmentId = commitmentId,
            ProcurementBudgetId = budgetId,
            PurchaseRequisitionId = requisitionId,
            EntryType = ProcurementBudgetCommitmentLedgerEntryType.Release,
            SourceType = "Contract", SourceId = releasedContractId,
            SourceReference = "CON-SQL-RELEASED", Amount = 40m,
            Currency = "GHS", FormalCommitmentEntryId = contractFormal.Id,
            OccurredAtUtc = now, ActorUserId = actorId,
            ActorName = "SQL amendment actor", CorrelationId = "sql-contract-release",
            CreatedAt = now, CreatedById = actorId
        };
        var workflowEntityType = new WorkflowEntityType
        {
            Id = workflowEntityTypeId, TenantId = tenantId,
            Code = "PO_SQL_AMEND", Name = "SQL PO Amendment", CreatedAt = now
        };
        var workflowDefinition = new WorkflowDefinition
        {
            Id = workflowDefinitionId, TenantId = tenantId,
            DefinitionKey = Guid.NewGuid(), Name = "SQL PO Amendment Approval",
            EntityTypeId = workflowEntityTypeId, Version = 1,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            IsActive = true, CreatedAt = now
        };
        var workflowInstance = new WorkflowInstance
        {
            Id = workflowInstanceId, TenantId = tenantId,
            WorkflowDefinitionId = workflowDefinitionId,
            EntityId = poOne.Id, EntityTypeId = workflowEntityTypeId,
            Status = WorkflowInstanceStatus.InProgress,
            InitiatedById = actorId, CreatedAt = now
        };
        context.AddRange(
            NewTenant(tenantId, "PROC-SQL-AMEND"),
            NewUser(actorId, tenantId, "proc.sql.amend"),
            new Department
            {
                Id = departmentId, TenantId = tenantId, Name = "SQL Amendment",
                Code = "SQL-AMEND", AccountCode = "SQL-AMEND", CreatedAt = now
            },
            budget, requisition, sourcingRelease, supplier,
            inventoryCategory, inventoryItem, poOne, poTwo, poOneItem,
            commitment, directFormal, contractFormal, contractRelease,
            workflowEntityType, workflowDefinition, workflowInstance);
        await context.SaveChangesAsync();
        poOne.Status = "Approved";
        await context.SaveChangesAsync();

        using var unitOfWork = new UnitOfWork(context);
        var currentUser = NewCurrentUser(actorId, tenantId);
        var source = new ProcurementPurchaseOrderSourceResolution
        {
            SourceType = ProcurementPurchaseOrderSourceType.RfqAward,
            SourceId = sourceId,
            SourceReference = "RFQ-SQL-AMEND",
            PurchaseRequisitionId = requisitionId,
            PurchaseRequisitionNumber = requisition.RequisitionNumber,
            SourcingReleaseId = releaseId,
            SourcingCaseId = Guid.Empty,
            AwardReadinessDecisionId = Guid.Empty,
            BusinessPartnerId = supplierId,
            CurrencyCode = "GHS",
            SourceSnapshotJson = poOne.SourceSnapshotJson,
            SourceIntegrityHash = poOne.SourceIntegrityHash,
            ValidatedAtUtc = now
        };
        var access = new Mock<IProcurementAccessControlService>();
        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(item => item.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventDto());
        var sources = new Mock<IProcurementPurchaseOrderSourceService>();
        sources.Setup(item => item.RevalidateAsync(
                It.IsAny<PurchaseOrder>(), "Amend", It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        sources.Setup(item => item.ValidateOrderAsync(
                It.IsAny<ProcurementPurchaseOrderSourceResolution>(),
                It.IsAny<IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine>>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sod = new Mock<IProcurementPurchaseOrderSodService>();
        sod.Setup(item => item.EnforceApprovalAsync(
                It.IsAny<PurchaseOrder>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementPurchaseOrderSodReadinessDto
            {
                CanApprove = true, Code = "PO_SOD_ALLOWED"
            });
        var compliance = new Mock<IProcurementPurchaseOrderComplianceService>();
        compliance.Setup(item => item.EnforceAsync(
                It.IsAny<PurchaseOrder>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementPurchaseOrderComplianceDto
            {
                IsCompliant = true, Code = "PO_COMPLIANT"
            });
        var framework = new Mock<IProcurementFrameworkCallOffService>();
        framework.Setup(item => item.IsFrameworkCallOffPurchaseOrderAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(item => item.SubmitAsync("PurchaseOrder", poOne.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = workflowInstanceId
                },
                WorkflowOutcome.Pending));
        workflow.Setup(item => item.CanUserApproveAsync(
                "PurchaseOrder", poOne.Id, actorId))
            .ReturnsAsync(true);
        workflow.Setup(item => item.ProcessApprovalAsync(
                "PurchaseOrder", poOne.Id, actorId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Completed,
                    WorkflowInstanceId = workflowInstanceId
                },
                WorkflowOutcome.Approved));
        var notifications = new Mock<INotificationTopicPublisher>();
        notifications.Setup(item => item.PublishAsync(
                It.IsAny<NotificationTopicEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var reservationStore = new ProcurementBudgetReservationStore(context);
        var amendmentStore = new ProcurementPurchaseOrderAmendmentStore(context);
        var amendmentService = new ProcurementPurchaseOrderAmendmentService(
            unitOfWork, currentUser.Object, access.Object, sources.Object,
            sod.Object, compliance.Object, framework.Object, workflow.Object,
            reservationStore, amendmentStore, controlEvents.Object,
            notifications.Object,
            NullLogger<ProcurementPurchaseOrderAmendmentService>.Instance);

        var created = await amendmentService.CreateAsync(
            poOne.Id,
            new CreateProcurementPurchaseOrderAmendmentRequest
            {
                Reason = "Prove exact SQL amendment budget lifecycle authorization.",
                ChangeScope = "Quantity",
                RequiredDate = poOne.RequiredDate,
                PromisedDate = poOne.PromisedDate,
                EvidenceReference = "DMS-SQL-AMEND-CREATE",
                IdempotencyKey = "sql-amend-create",
                Items =
                [
                    new ProcurementPurchaseOrderAmendmentItemRequest
                    {
                        PurchaseOrderItemId = poOneItem.Id,
                        InventoryItemId = inventoryItemId,
                        ItemDescription = poOneItem.ItemDescription,
                        OrderedQuantity = 12m,
                        UnitOfMeasure = "EA",
                        UnitPrice = 10m
                    }
                ]
            },
            "sql-amend-create");
        var submitted = await amendmentService.SubmitAsync(
            created.Id,
            new ProcurementPurchaseOrderAmendmentLifecycleRequest
            {
                Comment = "Submit exact SQL amendment proof.",
                RowVersion = created.RowVersion,
                EvidenceReference = "DMS-SQL-AMEND-SUBMIT"
            },
            "sql-amend-submit");
        var applied = await amendmentService.DecideAsync(
            submitted.Id,
            new DecideProcurementPurchaseOrderAmendmentRequest
            {
                Approved = true,
                Comment = "Approve exact SQL amendment proof.",
                RowVersion = submitted.RowVersion,
                EvidenceReference = "DMS-SQL-AMEND-APPROVE"
            },
            "sql-amend-approve");

        applied.CommitmentAdjustments.Should().ContainSingle()
            .Which.DeltaAmount.Should().Be(20m);
        context.ChangeTracker.Clear();
        var amendedCommitment = await context.ProcurementBudgetCommitments
            .IgnoreQueryFilters().SingleAsync(item => item.Id == commitmentId);
        amendedCommitment.ReservationSequence.Should().Be(2);
        amendedCommitment.ReservedAmount.Should().Be(120m);
        amendedCommitment.FormallyCommittedAmount.Should().Be(120m);
        (amendedCommitment.ReservedAmount - amendedCommitment.FormallyCommittedAmount)
            .Should().Be(0m);
        var amendedBudget = await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId);
        amendedBudget.CommittedAmount.Should().Be(120m);
        amendedBudget.ReservedAmount.Should().Be(0m);

        var budgetControl = new ProcurementRequisitionBudgetControlService(
            unitOfWork, currentUser.Object, access.Object, controlEvents.Object,
            reservationStore);
        var sourceService = new ProcurementPurchaseOrderSourceService(
            unitOfWork, currentUser.Object, access.Object, controlEvents.Object,
            budgetControl, reservationStore, notifications.Object,
            NullLogger<ProcurementPurchaseOrderSourceService>.Instance);
        var ensureBudgetCommitment = typeof(ProcurementPurchaseOrderSourceService)
            .GetMethod("EnsureBudgetCommitmentAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

        await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var ensureTask = (Task)ensureBudgetCommitment.Invoke(
            sourceService,
            [source, poTwo.TotalAmount, poTwo.Currency, poTwo.Id,
                "sql-amend-po2-reserve", CancellationToken.None])!;
        await ensureTask;
        var expandedCommitment = await context.ProcurementBudgetCommitments
            .IgnoreQueryFilters().SingleAsync(item => item.Id == commitmentId);
        expandedCommitment.ReservedAmount.Should().Be(150m);
        expandedCommitment.FormallyCommittedAmount.Should().Be(120m);
        expandedCommitment.ReservationSequence.Should().Be(3);

        var lifecycle = new ProcurementBudgetCommitmentLifecycleService(
            unitOfWork, currentUser.Object, reservationStore);
        var poTwoFormal = await lifecycle.CommitPurchaseOrderAsync(
            poTwo, "sql-amend-po2-commit");
        await unitOfWork.SaveChangesAsync();
        poTwo.Status = "Approved";
        await unitOfWork.SaveChangesAsync();
        var poTwoReplay = await lifecycle.CommitPurchaseOrderAsync(
            poTwo, "sql-amend-po2-replay");
        poTwoReplay.Id.Should().Be(poTwoFormal.Id);
        await unitOfWork.SaveChangesAsync();
        await unitOfWork.CommitAsync();

        context.ChangeTracker.Clear();
        var finalCommitment = await context.ProcurementBudgetCommitments
            .IgnoreQueryFilters().SingleAsync(item => item.Id == commitmentId);
        finalCommitment.ReservedAmount.Should().Be(150m);
        finalCommitment.FormallyCommittedAmount.Should().Be(150m);
        (await context.ProcurementBudgetCommitmentLedgerEntries.IgnoreQueryFilters()
            .CountAsync(item =>
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                item.SourceType == "PurchaseOrder")).Should().Be(2);

        await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await reservationStore.SetDownstreamReservationContextAsync(
            new ProcurementDownstreamReservationMutationContext(
                tenantId, requisitionId, Guid.NewGuid(),
                150m, 151m, 3, 4, "sql-wrong-downstream-context"),
            CancellationToken.None);
        var wrongContextMutation = async () =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE dbo.ProcurementBudgetCommitments
                 SET ReservedAmount = ReservedAmount + 1,
                     ReservationSequence = ReservationSequence + 1,
                     CorrelationId = {"sql-wrong-downstream-context"}
                 WHERE Id = {commitmentId};
                 """);
        (await wrongContextMutation.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(51022);
        if (unitOfWork.HasActiveTransaction)
            await unitOfWork.RollbackAsync();
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task GoodsVatInvoiceRequiresAcceptedReceiptMatchAndRejectsForeignOrStaleEvidence()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync(ThreeWayMatchSchemaSql);
        await database.ApplySqlOperationsAsync(new TDC0504MandatoryThreeWayMatch());

        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var matchingEventId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var purchaseOrderItemId = Guid.NewGuid();
        var receiptItemId = Guid.NewGuid();
        var inspectionId = Guid.NewGuid();
        var inspectionLineId = Guid.NewGuid();
        var invoiceLineId = Guid.NewGuid();
        var evaluatedAt = DateTime.UtcNow.AddMinutes(2);
        var snapshotHash = new string('a', 64);

        await database.ExecuteAsync(
            """
            INSERT dbo.PurchaseOrders (Id,TenantId,CreatedAt,UpdatedAt)
            VALUES (@po,@tenant,DATEADD(minute,-5,@evaluated),DATEADD(minute,-5,@evaluated));
            INSERT dbo.PurchaseOrderItems
                (Id,PurchaseOrderId,TenantId,OrderedQuantity,UnitPrice,CreatedAt,UpdatedAt)
            VALUES (@poItem,@po,@tenant,10,10,DATEADD(minute,-5,@evaluated),DATEADD(minute,-5,@evaluated));
            INSERT dbo.PurchaseOrderReceipts (Id,PurchaseOrderId,TenantId,CreatedAt,UpdatedAt)
            VALUES (@receipt,@po,@tenant,DATEADD(minute,-4,@evaluated),DATEADD(minute,-4,@evaluated));
            INSERT dbo.PurchaseOrderReceiptItems
                (Id,ReceiptId,PurchaseOrderItemId,TenantId,ReceivedQuantity,AcceptedQuantity,
                 RejectedQuantity,CreatedAt,UpdatedAt)
            VALUES (@receiptItem,@receipt,@poItem,@tenant,10,10,0,
                    DATEADD(minute,-4,@evaluated),DATEADD(minute,-4,@evaluated));
            INSERT dbo.ProcurementReceiptInspectionCases
                (Id,PurchaseOrderReceiptId,TenantId,Status,ReceivedQuantity,AcceptedQuantity,
                 RejectedQuantity,PendingQuantity,ApEligibleQuantity,CreatedAt,UpdatedAt)
            VALUES (@inspection,@receipt,@tenant,9,10,10,0,0,10,
                    DATEADD(minute,-3,@evaluated),DATEADD(minute,-3,@evaluated));
            INSERT dbo.ProcurementReceiptInspectionLines
                (Id,InspectionCaseId,PurchaseOrderReceiptItemId,TenantId,ReceivedQuantity,
                 AcceptedQuantity,RejectedQuantity,PendingQuantity,CreatedAt,UpdatedAt)
            VALUES (@inspectionLine,@inspection,@receiptItem,@tenant,10,10,0,0,
                    DATEADD(minute,-3,@evaluated),DATEADD(minute,-3,@evaluated));
            INSERT dbo.VendorInvoice
                (Id,TenantId,IsDeleted,Status,PurchaseOrderId,SupplierId,CurrencyCode,
                 SubTotal,TaxAmount,DiscountAmount,TotalAmount,IsOpeningBalance,
                 MatchingStatus,MatchingNotes,MatchingPriceTolerancePercent,
                 MatchingQuantityTolerancePercent)
            VALUES (@invoice,@tenant,0,1,@po,@supplier,N'GHS',100,0,0,100,0,0,N'',0,0);
            INSERT dbo.VendorInvoiceLineItem
                (Id,VendorInvoiceId,PurchaseOrderItemId,Quantity,UnitPrice)
            VALUES (@invoiceLine,@invoice,@poItem,10,10);
            """,
            new SqlParameter("@po", purchaseOrderId),
            new SqlParameter("@tenant", tenantId),
            new SqlParameter("@evaluated", evaluatedAt),
            new SqlParameter("@receipt", receiptId),
            new SqlParameter("@poItem", purchaseOrderItemId),
            new SqlParameter("@receiptItem", receiptItemId),
            new SqlParameter("@inspection", inspectionId),
            new SqlParameter("@inspectionLine", inspectionLineId),
            new SqlParameter("@invoice", invoiceId),
            new SqlParameter("@invoiceLine", invoiceLineId),
            new SqlParameter("@supplier", supplierId));

        // This is the architecture's goods three-way match: PO price/quantity,
        // independently accepted receipt quantity, and the supplier VAT invoice.
        // Exercise the shared production rules with values read back from SQL
        // Server before testing the database approval hard stop.
        var values = await database.QuerySingleAsync(
            """
            SELECT poi.OrderedQuantity, poi.UnitPrice, ril.AcceptedQuantity,
                   vil.Quantity, vil.UnitPrice
              FROM dbo.PurchaseOrderItems poi
              JOIN dbo.PurchaseOrderReceiptItems pori
                ON pori.PurchaseOrderItemId=poi.Id AND pori.TenantId=poi.TenantId
              JOIN dbo.ProcurementReceiptInspectionLines ril
                ON ril.PurchaseOrderReceiptItemId=pori.Id AND ril.TenantId=poi.TenantId
              JOIN dbo.ProcurementReceiptInspectionCases ric
                ON ric.Id=ril.InspectionCaseId AND ric.TenantId=poi.TenantId
               AND ric.PendingQuantity=0 AND ric.ApEligibleQuantity>0
              JOIN dbo.VendorInvoiceLineItem vil ON vil.PurchaseOrderItemId=poi.Id
             WHERE poi.Id=@poItem;
            """,
            new SqlParameter("@poItem", purchaseOrderItemId));
        ProcurementInvoiceThreeWayMatchRules.IsPriceWithinTolerance(
                values[4], values[1], 0m)
            .Should().BeTrue();
        ProcurementInvoiceThreeWayMatchRules.IsCumulativeQuantityWithinTolerance(
                values[3], values[2], 0m)
            .Should().BeTrue();

        var missingMatch = async () => await database.ExecuteAsync(
            "UPDATE dbo.VendorInvoice SET Status=2 WHERE Id=@invoice;",
            new SqlParameter("@invoice", invoiceId));
        (await missingMatch.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(51601);

        // A matching event owned by another tenant must not authorize this
        // invoice, even when every other value looks valid.
        await database.ExecuteAsync(
            """
            INSERT dbo.ProcurementControlEvents
                (Id,TenantId,SourceId,SourceType,EventType,Action,RuleCode,RuleVersion,
                 Result,IsDeleted,OccurredAtUtc,ResultValuesJson,DecisionKeysJson,ActorUserId)
            VALUES (@event,@foreignTenant,@invoice,N'VendorInvoice',N'InvoiceThreeWayMatching',
                    N'InvoiceThreeWayMatchEvaluated',N'AP-002',N'TDC-0504',2,0,@evaluated,
                    N'{"approvalReady":true,"snapshotHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}',
                    N'["DEC-001","DEC-002","DEC-003","DEC-004","DEC-005","DEC-006","DEC-007","DEC-008","DEC-009","DEC-010","DEC-011","DEC-012","DEC-013","DEC-014"]',NEWID());
            UPDATE dbo.VendorInvoice
               SET MatchingType=2,MatchingStatus=2,MatchingControlEventId=@event,
                   MatchingSnapshotHash=@hash,MatchingEvaluatedAtUtc=@evaluated
             WHERE Id=@invoice;
            """,
            new SqlParameter("@event", matchingEventId),
            new SqlParameter("@foreignTenant", foreignTenantId),
            new SqlParameter("@invoice", invoiceId),
            new SqlParameter("@evaluated", evaluatedAt),
            new SqlParameter("@hash", snapshotHash));
        var foreignMatch = async () => await database.ExecuteAsync(
            "UPDATE dbo.VendorInvoice SET Status=2 WHERE Id=@invoice;",
            new SqlParameter("@invoice", invoiceId));
        (await foreignMatch.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(51601);

        await database.ExecuteAsync(
            "UPDATE dbo.ProcurementControlEvents SET TenantId=@tenant WHERE Id=@event;",
            new SqlParameter("@tenant", tenantId),
            new SqlParameter("@event", matchingEventId));
        await database.ExecuteAsync(
            "UPDATE dbo.VendorInvoice SET Status=2 WHERE Id=@invoice;",
            new SqlParameter("@invoice", invoiceId));

        // A receipt change after evaluation invalidates the previous match.
        await database.ExecuteAsync(
            "UPDATE dbo.PurchaseOrderReceipts SET UpdatedAt=DATEADD(minute,1,@evaluated) WHERE Id=@receipt;",
            new SqlParameter("@evaluated", evaluatedAt),
            new SqlParameter("@receipt", receiptId));
        var staleMatch = async () => await database.ExecuteAsync(
            "UPDATE dbo.VendorInvoice SET Status=3 WHERE Id=@invoice;",
            new SqlParameter("@invoice", invoiceId));
        (await staleMatch.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().Be(51603);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task TenantFilterUsesCurrentContextAcrossSharedProviderModelAndPreservesSoftDelete()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync(string.Empty);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var partnerAId = Guid.NewGuid();
        var partnerBId = Guid.NewGuid();
        var deletedPartnerBId = Guid.NewGuid();

        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.AddRange(
                NewTenant(tenantAId, "FILTER-SQL-A"),
                NewTenant(tenantBId, "FILTER-SQL-B"),
                new BusinessPartner
                {
                    Id = partnerAId, TenantId = tenantAId,
                    PartnerCode = "FILTER-A", PartnerName = "Tenant A Supplier",
                    PartnerType = "Supplier", RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved", Currency = "GHS", IsActive = true
                },
                new BusinessPartner
                {
                    Id = partnerBId, TenantId = tenantBId,
                    PartnerCode = "FILTER-B", PartnerName = "Tenant B Supplier",
                    PartnerType = "Supplier", RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved", Currency = "GHS", IsActive = true
                },
                new BusinessPartner
                {
                    Id = deletedPartnerBId, TenantId = tenantBId,
                    PartnerCode = "FILTER-B-DELETED", PartnerName = "Deleted Tenant B Supplier",
                    PartnerType = "Supplier", RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved", Currency = "GHS", IsActive = true,
                    IsDeleted = true, DeletedAt = DateTime.UtcNow
                });
            await setup.SaveChangesAsync();
        }

        await using (var tenantA = new ApplicationDbContext(options, tenantAId))
        {
            (await tenantA.BusinessPartners.Select(item => item.Id).ToListAsync())
                .Should().Equal(partnerAId);
        }

        await using (var tenantB = new ApplicationDbContext(options, tenantBId))
        {
            (await tenantB.BusinessPartners.Select(item => item.Id).ToListAsync())
                .Should().Equal(partnerBId);
        }

        await using (var system = new ApplicationDbContext(options))
        {
            (await system.BusinessPartners.Select(item => item.Id).ToListAsync())
                .Should().BeEquivalentTo(new[] { partnerAId, partnerBId });
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task StatutoryReportSeederIsIdempotentAndNewArchitectureQueriesTranslateOnSqlServer()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync(string.Empty);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Procurement SQL Architecture Tenant",
            Code = $"PROC-SQL-{tenantId:N}"[..32],
            BaseCurrency = "GHS"
        });
        await context.SaveChangesAsync();

        var seeder = new ProcurementStatutoryReportSeeder(
            context, NullLogger<ProcurementStatutoryReportSeeder>.Instance);
        (await seeder.SeedTenantAsync(tenantId)).Should().Be(13);
        (await seeder.SeedTenantAsync(tenantId)).Should().Be(0);

        var reportQueries = await context.Reports.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           item.Type == ProcurementStatutoryReportCatalogue.ReportType)
            .Select(item => item.Query)
            .ToListAsync();
        reportQueries.Should().HaveCount(13).And.OnlyHaveUniqueItems();

        using var unitOfWork = new UnitOfWork(context);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(item => item.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = true,
                Code = "ACCESS_ALLOWED"
            });
        var service = new ProcurementStatutoryReportService(
            unitOfWork, access.Object, currentUser.Object);

        foreach (var code in new[]
                 {
                     ProcurementStatutoryReportCatalogue.ExceptionRegisterCode,
                     ProcurementStatutoryReportCatalogue.ProcurementToPaymentCode
                 })
        {
            var query = ProcurementStatutoryReportCatalogue.QueryPrefix + code;
            reportQueries.Should().Contain(query);
            var result = await service.ExecuteAsync(query,
                new ExecuteReportDto { Page = 1, PageSize = 25 }, isAdministrator: true);
            result.TotalRows.Should().Be(0);
            result.Columns.Should().NotBeEmpty();
        }
    }

    private static Tenant NewTenant(Guid id, string code) => new()
    {
        Id = id,
        Name = code.Replace('-', ' '),
        Code = code,
        BaseCurrency = "GHS",
        CreatedAt = DateTime.UtcNow
    };

    private static ApplicationUser NewUser(Guid id, Guid tenantId, string username) => new()
    {
        Id = id,
        TenantId = tenantId,
        UserName = username,
        NormalizedUserName = username.ToUpperInvariant(),
        Email = $"{username}@example.invalid",
        NormalizedEmail = $"{username}@example.invalid".ToUpperInvariant(),
        FirstName = "Procurement",
        LastName = "SQL Actor",
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static ProcurementBudget NewBudget(
        Guid id,
        Guid tenantId,
        Guid departmentId,
        Guid approverId,
        string code,
        decimal amount) => new()
    {
        Id = id,
        TenantId = tenantId,
        DepartmentId = departmentId,
        BudgetCode = code,
        Title = code,
        FiscalYear = DateTime.UtcNow.Year,
        AllocatedAmount = amount,
        UtilizedAmount = 0m,
        CommittedAmount = 0m,
        ReservedAmount = 0m,
        RemainingAmount = amount,
        Currency = "GHS",
        Status = "Approved",
        ControlLevel = "Strict",
        EffectiveDate = DateTime.UtcNow.AddDays(-1),
        ExpiryDate = DateTime.UtcNow.AddYears(1),
        ApprovedById = approverId,
        ApprovedDate = DateTime.UtcNow.AddMinutes(-1),
        CreatedAt = DateTime.UtcNow
    };

    private static PurchaseRequisition NewRequisition(
        Guid id,
        Guid tenantId,
        Guid requestedById,
        Guid budgetId,
        string budgetCode,
        string requisitionNumber,
        decimal amount) => new()
    {
        Id = id,
        TenantId = tenantId,
        RequisitionNumber = requisitionNumber,
        RequisitionDate = DateTime.UtcNow,
        RequestedById = requestedById,
        Status = "Draft",
        Priority = "Normal",
        Department = "Procurement SQL",
        Justification = "Real SQL Server FR-PR-005 verification",
        BudgetId = budgetId,
        BudgetCode = budgetCode,
        Currency = "GHS",
        TotalAmount = amount,
        ProcurementCategory = ProcurementCategoryClass.Goods,
        CreatedAt = DateTime.UtcNow
    };

    private static ProcurementBudgetCommitment NewCommitment(
        Guid id,
        Guid tenantId,
        ProcurementBudget budget,
        PurchaseRequisition requisition,
        Guid actorId,
        decimal amount,
        DateTime now)
    {
        budget.ReservedAmount += amount;
        budget.RemainingAmount = budget.AllocatedAmount - budget.ReservedAmount;
        return new ProcurementBudgetCommitment
        {
            Id = id,
            TenantId = tenantId,
            ProcurementBudgetId = budget.Id,
            PurchaseRequisitionId = requisition.Id,
            ReservationReference = $"BCR-{requisition.RequisitionNumber}",
            ReservationSequence = 1,
            Status = ProcurementBudgetCommitmentStatus.Reserved,
            ReservedAmount = amount,
            Currency = "GHS",
            BudgetAllocatedSnapshot = budget.AllocatedAmount,
            BudgetUtilizedSnapshot = 0m,
            BudgetCommittedBefore = 0m,
            BudgetReservedBefore = 0m,
            BudgetAvailableBefore = budget.AllocatedAmount,
            BudgetCommittedAfter = 0m,
            BudgetReservedAfter = amount,
            BudgetAvailableAfter = budget.RemainingAmount,
            ReservedAtUtc = now,
            ReservedById = actorId,
            ReservedByName = "Foreign SQL Actor",
            CorrelationId = "sql-foreign-reservation",
            CreatedAt = now,
            CreatedById = actorId
        };
    }

    private static PurchaseOrder NewPurchaseOrder(
        Guid id,
        Guid tenantId,
        Guid requisitionId,
        string orderNumber,
        decimal amount) => new()
    {
        Id = id,
        TenantId = tenantId,
        OrderNumber = orderNumber,
        SourceRequisitionId = requisitionId,
        ProcurementSourceType = ProcurementPurchaseOrderSourceType.HistoricalMigration,
        ProcurementSourceId = id,
        ProcurementSourceReference = orderNumber,
        SourceSnapshotJson = "{}",
        SourceIntegrityHash = new string('b', 64),
        SourceValidatedAtUtc = DateTime.UtcNow,
        TotalAmount = amount,
        Currency = "GHS",
        Status = "Approved",
        CreatedAt = DateTime.UtcNow
    };

    private static void MakeGovernedDraft(
        PurchaseOrder purchaseOrder,
        Guid sourcingReleaseId)
    {
        purchaseOrder.ProcurementSourceType = ProcurementPurchaseOrderSourceType.RfqAward;
        purchaseOrder.ProcurementSourceId = purchaseOrder.Id;
        purchaseOrder.ProcurementSourceReference = purchaseOrder.OrderNumber;
        purchaseOrder.SourcingReleaseId = sourcingReleaseId;
        purchaseOrder.SourcingCaseId = null;
        purchaseOrder.AwardReadinessDecisionId = null;
        purchaseOrder.ProcurementCategory = ProcurementCategoryClass.Goods;
        purchaseOrder.Status = "Draft";
    }

    private static PurchaseOrderReceipt NewReceipt(
        Guid id,
        Guid tenantId,
        Guid purchaseOrderId,
        string receiptNumber,
        Guid receivedById) => new()
    {
        Id = id,
        TenantId = tenantId,
        PurchaseOrderId = purchaseOrderId,
        ReceiptNumber = receiptNumber,
        ReceiptDate = DateTime.UtcNow,
        ReceivedById = receivedById,
        Status = "Accepted",
        CreatedAt = DateTime.UtcNow
    };

    private static Mock<ICurrentUserProvider> NewCurrentUser(Guid userId, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.Username).Returns("proc.sql.actor");
        currentUser.SetupGet(item => item.FullName).Returns("Procurement SQL Actor");
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.Roles).Returns(new[] { "TenantAdmin" });
        currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
        currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
            .Returns((string role) => string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase));
        return currentUser;
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable procurement architecture SQL gates.";
        }
    }

    private sealed class DisposableSqlDatabase : IAsyncDisposable
    {
        private readonly string _databaseName;
        private readonly string _connectionString;
        private readonly string _masterConnectionString;

        private DisposableSqlDatabase(string databaseName, string connectionString, string masterConnectionString)
        {
            _databaseName = databaseName;
            _connectionString = connectionString;
            _masterConnectionString = masterConnectionString;
        }

        public string ConnectionString => _connectionString;

        public static async Task<DisposableSqlDatabase> CreateAsync(string schemaSql)
        {
            var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var databaseName = $"RhemaERP_ProcArchitecture_{Guid.NewGuid():N}";
            var masterBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = "master",
                TrustServerCertificate = true
            };
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = databaseName,
                TrustServerCertificate = true
            };
            var result = new DisposableSqlDatabase(
                databaseName,
                databaseBuilder.ConnectionString,
                masterBuilder.ConnectionString);
            await using (var master = new SqlConnection(result._masterConnectionString))
            {
                await master.OpenAsync();
                await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(schemaSql))
                    await result.ExecuteAsync(schemaSql);
                return result;
            }
            catch
            {
                await result.DisposeAsync();
                throw;
            }
        }

        public async Task ApplySqlOperationsAsync(Migration migration)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(migration, [builder]);
            foreach (var operation in builder.Operations.OfType<SqlOperation>())
                await ExecuteAsync(operation.Sql);
        }

        public async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters.Length > 0) command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<decimal[]> QuerySingleAsync(string sql, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters.Length > 0) command.Parameters.AddRange(parameters);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("The expected SQL Server integration row was not returned.");
            return Enumerable.Range(0, reader.FieldCount).Select(reader.GetDecimal).ToArray();
        }

        private static async Task ExecuteAsync(SqlConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var master = new SqlConnection(_masterConnectionString);
            await master.OpenAsync();
            await ExecuteAsync(master,
                $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;");
        }
    }

    private const string ThreeWayMatchSchemaSql =
        """
        CREATE TABLE dbo.ProcurementControlEvents
        (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,SourceId uniqueidentifier NOT NULL,
         SourceType nvarchar(100) NOT NULL,EventType nvarchar(100) NOT NULL,Action nvarchar(100) NOT NULL,
         RuleCode nvarchar(50) NULL,RuleVersion nvarchar(50) NULL,Result int NOT NULL,IsDeleted bit NOT NULL,
         OccurredAtUtc datetime2 NOT NULL,ResultValuesJson nvarchar(max) NULL,DecisionKeysJson nvarchar(max) NULL,
         ActorUserId uniqueidentifier NOT NULL);
        CREATE TABLE dbo.WorkflowInstances
        (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,Status int NOT NULL,
         IsDeleted bit NOT NULL,InitiatedById uniqueidentifier NOT NULL);
        CREATE TABLE dbo.ProcurementControlEventEvidenceLinks
        (Id uniqueidentifier NOT NULL DEFAULT NEWID(),ControlEventId uniqueidentifier NOT NULL,TenantId uniqueidentifier NOT NULL,
         RequirementKey nvarchar(100) NULL,Reference nvarchar(100) NULL,IsDeleted bit NOT NULL);
        CREATE TABLE dbo.PurchaseOrders
        (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,DeletedAt datetime2 NULL,
         UpdatedAt datetime2 NULL,CreatedAt datetime2 NOT NULL);
        CREATE TABLE dbo.PurchaseOrderItems
        (Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),PurchaseOrderId uniqueidentifier NOT NULL,TenantId uniqueidentifier NOT NULL,
         OrderedQuantity decimal(18,4) NOT NULL,UnitPrice decimal(18,4) NOT NULL,
         DeletedAt datetime2 NULL,UpdatedAt datetime2 NULL,CreatedAt datetime2 NOT NULL);
        CREATE TABLE dbo.PurchaseOrderReceipts
        (Id uniqueidentifier PRIMARY KEY,PurchaseOrderId uniqueidentifier NOT NULL,TenantId uniqueidentifier NOT NULL,
         DeletedAt datetime2 NULL,UpdatedAt datetime2 NULL,CreatedAt datetime2 NOT NULL);
        CREATE TABLE dbo.PurchaseOrderReceiptItems
        (Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),ReceiptId uniqueidentifier NOT NULL,TenantId uniqueidentifier NOT NULL,
         PurchaseOrderItemId uniqueidentifier NOT NULL,ReceivedQuantity decimal(18,4) NOT NULL,
         AcceptedQuantity decimal(18,4) NOT NULL,RejectedQuantity decimal(18,4) NOT NULL,
         DeletedAt datetime2 NULL,UpdatedAt datetime2 NULL,CreatedAt datetime2 NOT NULL);
        CREATE TABLE dbo.ProcurementReceiptInspectionCases
        (Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),PurchaseOrderReceiptId uniqueidentifier NOT NULL,TenantId uniqueidentifier NOT NULL,
         Status int NOT NULL,ReceivedQuantity decimal(18,4) NOT NULL,AcceptedQuantity decimal(18,4) NOT NULL,
         RejectedQuantity decimal(18,4) NOT NULL,PendingQuantity decimal(18,4) NOT NULL,
         ApEligibleQuantity decimal(18,4) NOT NULL,
         DeletedAt datetime2 NULL,UpdatedAt datetime2 NULL,CreatedAt datetime2 NOT NULL);
        CREATE TABLE dbo.ProcurementReceiptInspectionLines
        (Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),InspectionCaseId uniqueidentifier NOT NULL,TenantId uniqueidentifier NOT NULL,
         PurchaseOrderReceiptItemId uniqueidentifier NOT NULL,ReceivedQuantity decimal(18,4) NOT NULL,
         AcceptedQuantity decimal(18,4) NOT NULL,RejectedQuantity decimal(18,4) NOT NULL,
         PendingQuantity decimal(18,4) NOT NULL,
         DeletedAt datetime2 NULL,UpdatedAt datetime2 NULL,CreatedAt datetime2 NOT NULL);
        CREATE TABLE dbo.VendorInvoice
        (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,IsDeleted bit NOT NULL,Status int NOT NULL,
         PurchaseOrderId uniqueidentifier NULL,SupplierId uniqueidentifier NOT NULL,CurrencyCode nvarchar(10) NOT NULL,
         SubTotal decimal(18,2) NOT NULL,TaxAmount decimal(18,2) NOT NULL,DiscountAmount decimal(18,2) NOT NULL,
         TotalAmount decimal(18,2) NOT NULL,IsOpeningBalance bit NOT NULL,SubmittedById uniqueidentifier NULL,
         MatchingType int NOT NULL DEFAULT 0,MatchingStatus int NOT NULL,MatchingNotes nvarchar(max) NULL,
         MatchingControlEventId uniqueidentifier NULL,MatchingSnapshotHash nvarchar(64) NULL,
         MatchingEvaluatedAtUtc datetime2 NULL,MatchingPriceTolerancePercent decimal(5,2) NOT NULL,
         MatchingQuantityTolerancePercent decimal(5,2) NOT NULL,MatchExceptionControlEventId uniqueidentifier NULL);
        CREATE TABLE dbo.VendorInvoiceLineItem
        (Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),VendorInvoiceId uniqueidentifier NOT NULL,
         PurchaseOrderItemId uniqueidentifier NOT NULL,Quantity decimal(18,4) NOT NULL,
         UnitPrice decimal(18,4) NOT NULL);
        """;
}
