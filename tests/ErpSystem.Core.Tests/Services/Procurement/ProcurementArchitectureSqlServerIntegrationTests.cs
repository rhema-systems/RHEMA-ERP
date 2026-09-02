using System.Reflection;
using System.Data;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
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
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        await context.Database.EnsureCreatedAsync();

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
        context.AddRange(budget, requisition, foreignBudget, foreignRequisition,
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

        // FR-PR-005: PR submission protects exposure as a reservation, not a
        // formal Finance commitment. Replaying submission must not reserve twice.
        var reservation = await reservationService.ReserveAsync(requisition, "sql-pr-reserve");
        reservation.CanReserve.Should().BeTrue();
        await unitOfWork.SaveChangesAsync();
        var reservationReplay = await reservationService.ReserveAsync(requisition, "sql-pr-reserve-retry");
        reservationReplay.CommitmentId.Should().Be(reservation.CommitmentId);
        await unitOfWork.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var afterReservation = await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId);
        afterReservation.ReservedAmount.Should().Be(1000m);
        afterReservation.CommittedAmount.Should().Be(0m);
        afterReservation.UtilizedAmount.Should().Be(0m);
        afterReservation.RemainingAmount.Should().Be(0m);
        (await context.ProcurementBudgetCommitments.IgnoreQueryFilters()
            .CountAsync(item => item.PurchaseRequisitionId == requisitionId)).Should().Be(1);
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
        foreach (var purchaseOrder in new[] { poOne, poTwo, overCommittedPo, contractPo, contractPoOver })
            purchaseOrder.BusinessPartnerId = partnerId;
        var receiptOne = Guid.NewGuid();
        var receiptTwo = Guid.NewGuid();
        var contractPoReceipt = Guid.NewGuid();
        var contractPoExcessReceipt = Guid.NewGuid();
        context.AddRange(contract, project, certificate, poOne, poTwo, overCommittedPo, contractPo, contractPoOver,
            NewReceipt(receiptOne, tenantId, poOne.Id, "REC-SQL-001", actorId),
            NewReceipt(receiptTwo, tenantId, poTwo.Id, "REC-SQL-002", actorId),
            NewReceipt(contractPoReceipt, tenantId, contractPo.Id, "REC-SQL-CONTRACT", actorId),
            NewReceipt(contractPoExcessReceipt, tenantId, contractPo.Id, "REC-SQL-CONTRACT-OVER", actorId));
        await unitOfWork.SaveChangesAsync();

        var formalOne = await lifecycle.CommitPurchaseOrderAsync(poOne, "sql-po-1");
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
        await lifecycle.CommitPurchaseOrderAsync(poTwo, "sql-po-2");
        await unitOfWork.SaveChangesAsync();

        var contractFormal = await lifecycle.CommitContractAsync(contract, "sql-contract");
        await unitOfWork.SaveChangesAsync();
        var contractFormalReplay = await lifecycle.CommitContractAsync(contract, "sql-contract-retry");
        contractFormalReplay.Id.Should().Be(contractFormal.Id);
        await unitOfWork.SaveChangesAsync();

        var overCommit = async () => await lifecycle.CommitPurchaseOrderAsync(overCommittedPo, "sql-over");
        (await overCommit.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_RESERVATION_EXCEEDED");

        // A child PO under an already committed contract is an allocation only;
        // it must not commit the same GHS exposure a second time.
        var allocation = await lifecycle.CommitPurchaseOrderAsync(contractPo, "sql-contract-po");
        await unitOfWork.SaveChangesAsync();
        var allocationReplay = await lifecycle.CommitPurchaseOrderAsync(contractPo, "sql-contract-po-retry");
        allocationReplay.Id.Should().Be(allocation.Id);
        await unitOfWork.SaveChangesAsync();
        var overAllocation = async () => await lifecycle.CommitPurchaseOrderAsync(contractPoOver, "sql-contract-po-over");
        (await overAllocation.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("PO_CONTRACT_COMMITMENT_EXCEEDED");

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
            contractPo.Id, contractPoReceipt, "REC-SQL-CONTRACT", 250m, "sql-contract-receipt");
        await unitOfWork.SaveChangesAsync();
        var overChildAllocation = async () => await lifecycle.UtilizePurchaseOrderAsync(
            contractPo.Id, contractPoExcessReceipt, "REC-SQL-CONTRACT-OVER", 1m,
            "sql-contract-receipt-over");
        (await overChildAllocation.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("PO_CONTRACT_ALLOCATION_EXCEEDED");
        var certificateUtilization = await lifecycle.UtilizeContractCertificateAsync(
            contractId, certificateId, "CERT-SQL-001", 50m, "sql-certificate");
        await unitOfWork.SaveChangesAsync();
        var certificateReplay = await lifecycle.UtilizeContractCertificateAsync(
            contractId, certificateId, "CERT-SQL-001", 50m, "sql-certificate-retry");
        certificateReplay.Id.Should().Be(certificateUtilization.Id);
        await unitOfWork.SaveChangesAsync();
        var conflictingCertificate = async () => await lifecycle.UtilizeContractCertificateAsync(
            contractId, certificateId, "CERT-SQL-001", 49m, "sql-certificate-conflict");
        (await conflictingCertificate.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("BUDGET_UTILIZATION_IDEMPOTENCY_CONFLICT");

        var foreignUtilization = async () => await lifecycle.UtilizePurchaseOrderAsync(
            foreignFormalPoId, Guid.NewGuid(), "REC-SQL-FOREIGN", 100m, "sql-foreign-receipt");
        (await foreignUtilization.Should().ThrowAsync<ProcurementBudgetCommitmentLifecycleException>())
            .Which.Code.Should().Be("FORMAL_BUDGET_COMMITMENT_REQUIRED");

        await unitOfWork.CommitAsync();
        context.ChangeTracker.Clear();

        var finalBudget = await context.ProcurementBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == budgetId);
        finalBudget.ReservedAmount.Should().Be(0m);
        finalBudget.CommittedAmount.Should().Be(0m);
        finalBudget.UtilizedAmount.Should().Be(1000m);
        finalBudget.RemainingAmount.Should().Be(0m);
        var finalCommitment = await context.ProcurementBudgetCommitments.IgnoreQueryFilters()
            .SingleAsync(item => item.PurchaseRequisitionId == requisitionId);
        finalCommitment.FormallyCommittedAmount.Should().Be(1000m);
        finalCommitment.UtilizedAmount.Should().Be(1000m);
        finalCommitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Consumed);
        var ledger = await context.ProcurementBudgetCommitmentLedgerEntries.IgnoreQueryFilters()
            .Where(item => item.PurchaseRequisitionId == requisitionId).ToListAsync();
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment)
            .Should().Be(3);
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation)
            .Should().Be(1);
        ledger.Count(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization)
            .Should().Be(4);
        ledger.Where(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment)
            .Sum(item => item.Amount).Should().Be(1000m);
        ledger.Where(item => item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization)
            .Sum(item => item.Amount).Should().Be(1000m);
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
