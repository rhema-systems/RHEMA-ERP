using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable CS0618 // Regression tests intentionally assert that obsolete legacy posting paths are not used.

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetCapitalizationFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task CreateDraftFixedAsset_ShouldBeTenantScopedAndAudited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var result = await services.FixedAssets.CreateAsync(new CreateFixedAssetDto
        {
            AssetCode = "FA-NEW-001",
            Name = "New laptop",
            FixedAssetCategoryId = fixture.Category.Id,
            PurchaseDate = new DateTime(2026, 7, 5),
            PurchasePrice = 250m,
            UsefulLifeMonths = 36,
            ResidualValue = 10m
        });

        result.Id.Should().NotBeEmpty();
        result.Status.Should().Be(FixedAssetStatus.Draft);
        (await db.FixedAssets.SingleAsync(a => a.Id == result.Id)).TenantId.Should().Be(tenantId);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.FixedAssetCreated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task ApInvoiceCapitalizableLine_ShouldPostAssetCostThroughPostingEngineAndNotExpense()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        var result = await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        result.JournalEntryId.Should().NotBeNull();
        services.SubledgerPosting.Verify(x => x.PostApInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AP" &&
            e.SourceDocumentType == "VendorInvoice" &&
            e.SourceDocumentId == fixture.Invoice.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.Transactions.Single(t => t.AccountId == fixture.AssetCostAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).CreditAmount.Should().Be(100m);
        journal.Transactions.Should().NotContain(t => t.AccountId == fixture.ExpenseAccount.Id && t.DebitAmount > 0m);

        var asset = await db.FixedAssets
            .Include(a => a.BookValues)
            .SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.Status.Should().Be(FixedAssetStatus.Capitalized);
        asset.AcquisitionCost.Should().Be(100m);
        asset.NetBookValue.Should().Be(100m);
        asset.SourceDocumentType.Should().Be("VendorInvoice");
        asset.SourceDocumentId.Should().Be(fixture.Invoice.Id);
        asset.SourceDocumentLineId.Should().Be(fixture.Invoice.LineItems.Single().Id);
        asset.JournalEntryId.Should().Be(result.JournalEntryId);
        asset.PostingEventId.Should().Be(postingEvent.Id);
        asset.BookValues.Should().ContainSingle().Which.AcquisitionCost.Should().Be(100m);

        var line = await db.Set<VendorInvoiceLineItem>().SingleAsync(l => l.Id == fixture.Invoice.LineItems.Single().Id);
        line.CapitalizationJournalEntryId.Should().Be(result.JournalEntryId);
        line.CapitalizationPostingEventId.Should().Be(postingEvent.Id);
        line.CapitalizedAt.Should().NotBeNull();

        (await db.AssetTransactions.CountAsync(t =>
            t.TenantId == tenantId &&
            t.FixedAssetId == fixture.Asset.Id &&
            t.TransactionType == "Capitalization")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.FixedAssetCapitalized)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task DuplicateApInvoiceCapitalizationRetry_ShouldNotDuplicateAssetRegisterTransactions()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);
        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == fixture.Invoice.Id)).Should().Be(1);
        (await db.AssetTransactions.CountAsync(t =>
            t.TenantId == tenantId &&
            t.FixedAssetId == fixture.Asset.Id &&
            t.TransactionType == "Capitalization")).Should().Be(1);
        (await db.FixedAssetBookValues.CountAsync(v =>
            v.TenantId == tenantId &&
            v.FixedAssetId == fixture.Asset.Id &&
            v.CapitalizationPostingEventId != null)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantFixedAssetReference_ShouldBeRejectedBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var otherFixture = await SeedFixedAssetFoundationAsync(db, otherTenantId, tenantCode: "OTH", invoiceNumber: "VI-OTH-001");
        fixture.Invoice.LineItems.Single().FixedAssetId = otherFixture.Asset.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        var act = () => services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*references an asset that was not found for this tenant*");
        (await db.FinancePostingEvents.CountAsync(e => e.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantAssetCategoryAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAssetAccount = SeedAccount(db, otherTenantId, "1500", AccountType.Asset);
        fixture.Category.AssetAccountId = otherAssetAccount.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        var act = () => services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fixed asset cost account was not found for this tenant*");
        (await db.FinancePostingEvents.CountAsync(e => e.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task CategoryMissingCostAccount_ShouldBlockDirectCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        fixture.Category.AssetAccountId = Guid.Empty;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            CreditAccountId = fixture.AucAccount.Id,
            Reason = "Test capitalization"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fixed asset cost account is required*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Contract", "FIN-INT-007")]
    public async Task ApprovedProcurementHandoff_ShouldReclassInventoryWithoutDuplicatingAssetCost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var inventoryControl = SeedAccount(
            db,
            tenantId,
            "1300",
            AccountType.Asset,
            isControlAccount: true,
            allowDirectPosting: false);
        var settings = await db.FinanceSettings.SingleAsync(value => value.TenantId == tenantId);
        settings.ControlAccountInventoryId = inventoryControl.Id;
        fixture.Asset.Status = FixedAssetStatus.Acquired;
        var handoff = new ProcurementFixedAssetCapitalization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = fixture.Asset.Id,
            AcceptedSupplyKind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection,
            AcceptedSupplySourceId = Guid.NewGuid(),
            AcceptedSupplyReference = "PO-ASSET-001",
            PurchaseOrderId = Guid.NewGuid(),
            PurchaseOrderItemId = Guid.NewGuid(),
            InventoryItemId = Guid.NewGuid(),
            CapitalizedQuantity = 1m,
            SourceCurrencyCode = "GHS",
            SourceTransactionAmount = 100m,
            FunctionalCurrencyCode = "GHS",
            FunctionalAmount = 100m,
            SourceIntegrityHash = new string('a', 64),
            SourceSnapshotJson = "{}",
            ReceiptPostingEvidenceJson = "{}",
            IdempotencyKey = "fa-procurement-test-001",
            CapitalizationDate = new DateTime(2026, 7, 5)
        };
        db.ProcurementFixedAssetCapitalizations.Add(handoff);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var result = await services.FixedAssets.CapitalizeFromProcurementAsync(
            fixture.Asset.Id,
            new ProcurementFixedAssetPostingInstructionDto
            {
                CapitalizationId = handoff.Id,
                PurchaseOrderItemId = handoff.PurchaseOrderItemId,
                CapitalizationDate = new DateTime(2026, 7, 5),
                InventoryControlAccountId = inventoryControl.Id,
                FunctionalAmount = 100m,
                FunctionalCurrencyCode = "GHS",
                SourceReference = handoff.AcceptedSupplyReference,
                Reason = "Accepted procured laptop approved for capitalization."
            });

        result.SourceDocumentType.Should().Be("ProcurementFixedAssetCapitalization");
        result.SourceDocumentId.Should().Be(handoff.Id);
        result.SourceDocumentLineId.Should().Be(handoff.PurchaseOrderItemId);
        var journal = await db.JournalEntries.Include(value => value.Transactions)
            .SingleAsync(value => value.Id == result.JournalEntryId);
        journal.Transactions.Should().ContainSingle(value =>
            value.AccountId == fixture.AssetCostAccount.Id && value.DebitAmount == 100m);
        journal.Transactions.Should().ContainSingle(value =>
            value.AccountId == inventoryControl.Id && value.CreditAmount == 100m);
        journal.Transactions.Sum(value => value.DebitAmount).Should().Be(100m);
        journal.Transactions.Sum(value => value.CreditAmount).Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Contract", "FIN-INT-007")]
    public async Task ProcurementAcceptedSupplyInvoice_ShouldClearGrvAndNotCapitalizeAssetAgain()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var grvControl = SeedAccount(
            db,
            tenantId,
            "2100",
            AccountType.Liability,
            isControlAccount: true,
            allowDirectPosting: false);
        var purchaseOrderId = Guid.NewGuid();
        fixture.Invoice.PurchaseOrderId = purchaseOrderId;
        fixture.Invoice.AcceptedSupplyKind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection;
        fixture.Invoice.AcceptedSupplySourceId = purchaseOrderId;
        (await db.FinanceSettings.SingleAsync(value => value.TenantId == tenantId)).ControlAccountGRVAccrualId = grvControl.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        var posted = await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries.Include(value => value.Transactions)
            .SingleAsync(value => value.Id == posted.JournalEntryId);
        journal.Transactions.Should().ContainSingle(value => value.AccountId == grvControl.Id && value.DebitAmount == 100m);
        journal.Transactions.Should().NotContain(value => value.AccountId == fixture.AssetCostAccount.Id && value.DebitAmount > 0m);
        (await db.FixedAssets.AsNoTracking().SingleAsync(value => value.Id == fixture.Asset.Id)).Status
            .Should().Be(FixedAssetStatus.Draft,
                "the accepted-receipt adapter, not the supplier invoice, owns Procurement asset capitalization");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task RecoverableTaxOnApAssetLine_ShouldNotBeCapitalized()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(
            db,
            tenantId,
            lineTaxAmount: 20m,
            totalAmount: 120m);
        var taxEngine = CreateTaxEngine(fixture.TaxAccount.Id, isInputTaxDeductible: true);
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true, taxEngine: taxEngine.Object);

        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(100m);
        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync();
        journal.Transactions.Single(t => t.AccountId == fixture.AssetCostAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.TaxAccount.Id).DebitAmount.Should().Be(20m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task NonRecoverableTaxOnApAssetLine_ShouldBeCapitalizedWhenTaxConfigMarksItNonRecoverable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(
            db,
            tenantId,
            lineTaxAmount: 20m,
            totalAmount: 120m);
        var taxEngine = CreateTaxEngine(fixture.TaxAccount.Id, isInputTaxDeductible: false);
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true, taxEngine: taxEngine.Object);

        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(120m);
        var assetLines = await db.AccountTransactions
            .Where(t => t.AccountId == fixture.AssetCostAccount.Id && t.DebitAmount > 0m)
            .ToListAsync();
        assetLines.Sum(t => t.DebitAmount).Should().Be(120m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyApAssetAcquisition_ShouldPreserveCurrencyAndRateSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var rate = SeedExchangeRate(db, tenantId, "GHS", "USD", 12m, new DateTime(2026, 7, 1));
        var fixture = await SeedFixedAssetFoundationAsync(
            db,
            tenantId,
            currencyCode: "USD",
            exchangeRate: 12m,
            makePostingAccountsMultiCurrency: true);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.FunctionalCurrencyCode.Should().Be("GHS");
        asset.TransactionCurrencyCode.Should().Be("USD");
        asset.ExchangeRate.Should().Be(12m);
        asset.ExchangeRateId.Should().Be(rate.Id);
        asset.AcquisitionCost.Should().Be(1200m);

        var assetLine = await db.AccountTransactions.SingleAsync(t =>
            t.AccountId == fixture.AssetCostAccount.Id &&
            t.TransactionTag == "AP-FixedAsset");
        assetLine.TransactionCurrency.Should().Be("USD");
        assetLine.TransactionDebitAmount.Should().Be(100m);
        assetLine.ExchangeRate.Should().Be(12m);
        assetLine.ExchangeRateId.Should().Be(rate.Id);

        rate.Rate = 13m;
        rate.InverseRate = Math.Round(1m / 13m, 6);
        await db.SaveChangesAsync();

        var unchangedAsset = await db.FixedAssets.AsNoTracking().SingleAsync(a => a.Id == fixture.Asset.Id);
        var unchangedLine = await db.AccountTransactions.AsNoTracking().SingleAsync(t => t.Id == assetLine.Id);
        unchangedAsset.AcquisitionCost.Should().Be(1200m);
        unchangedAsset.ExchangeRate.Should().Be(12m);
        unchangedLine.DebitAmount.Should().Be(1200m);
        unchangedLine.ExchangeRate.Should().Be(12m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task DirectCapitalizationIntoClosedPeriod_ShouldBeRejectedByPostingEngineAndAudited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var services = CreateServices(db, tenantId);

        var act = () => services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            CreditAccountId = fixture.AucAccount.Id,
            Reason = "Closed period test"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetCapitalizationBlockedClosedPeriod)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task DirectCapitalizationWithoutClearingAccountOrCreditAccount_ShouldBeGuarded()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        fixture.Category.AucAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            Reason = "No clearing account"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a category AUC/CIP clearing account or an explicit credit account*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task DirectCapitalizationRequiresWorkflowApprovalBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("FixedAsset", fixture.Asset.Id))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var services = CreateServices(db, tenantId, workflowService: workflow.Object);

        var blocked = () => services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            Reason = "Attempt before approval"
        });

        await blocked.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*submitted and approved*");

        var submitted = await services.FixedAssets.SubmitCapitalizationForApprovalAsync(
            fixture.Asset.Id,
            new SubmitFixedAssetCapitalizationDto
            {
                CapitalizationDate = new DateTime(2026, 7, 5),
                CreditAccountId = fixture.AucAccount.Id,
                Amount = 100m,
                Reference = "FA-APPROVED-001",
                Reason = "Approved direct capitalization",
                Comments = "Approve the exact capitalization journal proposal."
            });
        submitted.Status.Should().Be(FixedAssetStatus.PendingApproval);
        submitted.CapitalizationApprovalSnapshot.Should().NotBeNull();
        submitted.CapitalizationApprovalSnapshot!.TransactionAmount.Should().Be(100m);
        submitted.CapitalizationApprovalSnapshot.CreditAccountId.Should().Be(fixture.AucAccount.Id);
        submitted.CapitalizationApprovalSnapshotHash.Should().HaveLength(64);
        workflow.Verify(x => x.StartApprovalWorkflowAsync("FixedAsset", fixture.Asset.Id), Times.Once);

        var approval = await db.FixedAssets.SingleAsync(item => item.Id == fixture.Asset.Id);
        approval.Status = FixedAssetStatus.Acquired;
        approval.CapitalizationApprovalApprovedByUserId = Guid.NewGuid();
        approval.CapitalizationApprovalApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var capitalized = await services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 20),
            CreditAccountId = fixture.AssetCostAccount.Id,
            Amount = 999m,
            Reference = "CALLER-REPLACEMENT",
            Reason = "Caller values must not replace approved evidence"
        });

        capitalized.Status.Should().Be(FixedAssetStatus.Capitalized);
        capitalized.PostingEventId.Should().NotBeNull();
        capitalized.CapitalizationDate.Should().Be(new DateTime(2026, 7, 5));
        capitalized.AcquisitionCost.Should().Be(100m);
        var journal = await db.JournalEntries
            .Include(item => item.Transactions)
            .SingleAsync(item => item.Id == capitalized.JournalEntryId);
        journal.Transactions.Single(item => item.DebitAmount > 0m).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(item => item.CreditAmount > 0m).AccountId.Should().Be(fixture.AucAccount.Id);
        var postingEvent = await db.FinancePostingEvents.SingleAsync(item => item.Id == capitalized.PostingEventId);
        postingEvent.SourceDocumentReference.Should().Be("FA-APPROVED-001");
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task DirectCapitalization_ShouldFailClosedWhenApprovalSnapshotIsTampered()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("FixedAsset", fixture.Asset.Id))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var services = CreateServices(db, tenantId, workflowService: workflow.Object);

        await services.FixedAssets.SubmitCapitalizationForApprovalAsync(
            fixture.Asset.Id,
            new SubmitFixedAssetCapitalizationDto
            {
                CapitalizationDate = new DateTime(2026, 7, 5),
                CreditAccountId = fixture.AucAccount.Id,
                Amount = 100m,
                Reason = "Approve capitalization evidence"
            });

        var approval = await db.FixedAssets.SingleAsync(item => item.Id == fixture.Asset.Id);
        approval.Status = FixedAssetStatus.Acquired;
        approval.CapitalizationApprovalApprovedByUserId = Guid.NewGuid();
        approval.CapitalizationApprovalApprovedAt = DateTime.UtcNow;
        approval.CapitalizationApprovalSnapshotJson += " ";
        await db.SaveChangesAsync();

        var act = () => services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            Reason = "Attempt to post tampered evidence"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*approval evidence failed its integrity check*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task DirectCapitalization_ShouldRetireApprovalWhenPostingConfigurationDrifts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("FixedAsset", fixture.Asset.Id))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var services = CreateServices(db, tenantId, workflowService: workflow.Object);

        await services.FixedAssets.SubmitCapitalizationForApprovalAsync(
            fixture.Asset.Id,
            new SubmitFixedAssetCapitalizationDto
            {
                CapitalizationDate = new DateTime(2026, 7, 5),
                CreditAccountId = fixture.AucAccount.Id,
                Amount = 100m,
                Reason = "Approve current category accounts"
            });

        var approval = await db.FixedAssets.SingleAsync(item => item.Id == fixture.Asset.Id);
        approval.Status = FixedAssetStatus.Acquired;
        approval.CapitalizationApprovalApprovedByUserId = Guid.NewGuid();
        approval.CapitalizationApprovalApprovedAt = DateTime.UtcNow;
        var replacementAssetAccount = SeedAccount(db, tenantId, "1601", AccountType.Asset);
        fixture.Category.AssetAccountId = replacementAssetAccount.Id;
        await db.SaveChangesAsync();

        var act = () => services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            Reason = "Attempt after account configuration drift"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*posting-account configuration changed after capitalization approval*");
        var retired = await db.FixedAssets.AsNoTracking().SingleAsync(item => item.Id == fixture.Asset.Id);
        retired.Status.Should().Be(FixedAssetStatus.Draft);
        retired.CapitalizationApprovalInvalidatedAt.Should().NotBeNull();
        retired.CapitalizationApprovalInvalidationReason.Should().Contain("posting-account configuration changed");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task CapitalizedAsset_ShouldRejectDestructiveCostEdit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);
        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        var act = () => services.FixedAssets.UpdateAsync(fixture.Asset.Id, new UpdateFixedAssetDto
        {
            AssetCode = fixture.Asset.AssetCode,
            Name = fixture.Asset.Name,
            FixedAssetCategoryId = fixture.Category.Id,
            PurchaseDate = fixture.Asset.PurchaseDate,
            PurchasePrice = 101m,
            InstallationCost = 0m,
            TaxAmount = 0m,
            AcquisitionCost = 101m,
            UsefulLifeMonths = fixture.Asset.UsefulLifeMonths,
            ResidualValue = fixture.Asset.ResidualValue,
            Status = FixedAssetStatus.Capitalized
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Capitalized fixed asset accounting fields cannot be edited destructively.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalization")]
    [Trait("Category", "FixedAssets")]
    public async Task AssetCannotActivateBeforeCapitalization_ButActivationAfterCapitalizationPreservesPlacedInServiceDate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);

        await services.FixedAssets.Invoking(s => s.ActivateAsync(fixture.Asset.Id, new DateTime(2026, 7, 10)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only acquired or capitalized assets can be activated*");

        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);
        var activated = await services.FixedAssets.ActivateAsync(fixture.Asset.Id, new DateTime(2026, 7, 10));

        activated.Status.Should().Be(FixedAssetStatus.Active);
        activated.PlacedInServiceDate.Should().Be(new DateTime(2026, 7, 10));
        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.PostingEventId.Should().NotBeNull();
        asset.PlacedInServiceDate.Should().Be(new DateTime(2026, 7, 10));
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetActivated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalizationReversal")]
    [Trait("Requirement", "FR-GL-008;FR-GL-010;FIN-LIM-0030")]
    public async Task ApprovedDirectCapitalizationReversal_ShouldPostLinkedJournalAndZeroCurrentRegisterCost()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var maker = CreateServices(db, tenantId, userId: makerId, userName: "fa.maker");

        var capitalized = await maker.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            CreditAccountId = fixture.AucAccount.Id,
            Reason = "Approved original capitalization"
        });
        capitalized.Status.Should().Be(FixedAssetStatus.Capitalized);
        capitalized.PostingEventId.Should().NotBeNull("the reversal workflow requires immutable original posting lineage");
        capitalized.JournalEntryId.Should().NotBeNull("the reversal must link back to the posted capitalization journal");
        capitalized.CapitalizationReversalPostingEventId.Should().BeNull();
        var postedAsset = await db.FixedAssets.SingleAsync(item => item.Id == fixture.Asset.Id);
        postedAsset.CapitalizationReversalPostingEventId.Should().BeNull();
        var request = await maker.FixedAssets.RequestCapitalizationReversalAsync(
            fixture.Asset.Id,
            new RequestFixedAssetCapitalizationReversalDto
            {
                ReversalDate = new DateTime(2026, 7, 20),
                Reason = "Incorrect asset cost classification requires correction.",
                ImpactAssessment = "The asset cost and AUC clearing entries will be fully reversed before corrected reposting."
            });

        // Use a separately resolved user to prove maker-checker separation is enforced by the
        // service and not merely presented as a UI convention.
        var reviewer = CreateServices(db, tenantId, userId: Guid.NewGuid(), userName: "fa.reviewer");
        var approved = await reviewer.FixedAssets.ReviewCapitalizationReversalAsync(
            fixture.Asset.Id,
            request.Id,
            new ReviewFixedAssetCapitalizationReversalDto
            {
                Approved = true,
                ReviewComment = "Evidence reviewed; full reversal is appropriate before corrected capitalization."
            });
        approved.Status.Should().Be(FixedAssetCapitalizationReversalStatuses.Approved);

        var posted = await maker.FixedAssets.PostCapitalizationReversalAsync(fixture.Asset.Id, request.Id);

        posted.Status.Should().Be(FixedAssetCapitalizationReversalStatuses.Posted);
        posted.ReversalJournalEntryId.Should().NotBeNull();
        posted.ReversalPostingEventId.Should().NotBeNull();
        var originalJournal = await db.JournalEntries.SingleAsync(item => item.Id == capitalized.JournalEntryId);
        originalJournal.IsReversed.Should().BeTrue();
        originalJournal.ReversalJournalEntryId.Should().Be(posted.ReversalJournalEntryId);
        var asset = await db.FixedAssets.Include(item => item.BookValues).SingleAsync(item => item.Id == fixture.Asset.Id);
        asset.Status.Should().Be(FixedAssetStatus.Draft);
        asset.AcquisitionCost.Should().Be(0m);
        asset.NetBookValue.Should().Be(0m);
        asset.PostingEventId.Should().Be(capitalized.PostingEventId, "original lineage must not be destroyed");
        asset.CapitalizationReversalPostingEventId.Should().Be(posted.ReversalPostingEventId);
        asset.BookValues.Should().OnlyContain(value => value.AcquisitionCost == 0m && value.NetBookValue == 0m);
        (await db.AssetTransactions.CountAsync(item =>
            item.FixedAssetId == fixture.Asset.Id && item.TransactionType == "CapitalizationReversal")).Should().Be(1);
        (await db.AuditLogs.CountAsync(item =>
            item.TenantId == tenantId && item.Action == FinanceAuditEvents.FixedAssetCapitalizationReversed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalizationReversal")]
    [Trait("Requirement", "FR-GL-008;FIN-LIM-0030")]
    public async Task CapitalizationReversalRequester_ShouldNotApproveOwnRequest()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId, userId: Guid.NewGuid(), userName: "same.actor");
        await services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            CreditAccountId = fixture.AucAccount.Id,
            Reason = "Approved original capitalization"
        });
        var request = await services.FixedAssets.RequestCapitalizationReversalAsync(
            fixture.Asset.Id,
            new RequestFixedAssetCapitalizationReversalDto
            {
                ReversalDate = new DateTime(2026, 7, 20),
                Reason = "Incorrect asset cost classification requires correction.",
                ImpactAssessment = "The original cost will be removed and a corrected capitalization submitted separately."
            });

        var act = () => services.FixedAssets.ReviewCapitalizationReversalAsync(
            fixture.Asset.Id,
            request.Id,
            new ReviewFixedAssetCapitalizationReversalDto
            {
                Approved = true,
                ReviewComment = "Attempting to approve the request created by this same authenticated user."
            });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requester cannot review*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalizationReversal")]
    [Trait("Requirement", "FR-GL-010;FIN-LIM-0030")]
    public async Task DownstreamAssetValueMovement_ShouldBlockCapitalizationReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        await services.FixedAssets.CapitalizeAsync(fixture.Asset.Id, new CapitalizeFixedAssetDto
        {
            CapitalizationDate = new DateTime(2026, 7, 5),
            CreditAccountId = fixture.AucAccount.Id,
            Reason = "Approved original capitalization"
        });
        db.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = tenantId,
            FixedAssetId = fixture.Asset.Id,
            TransactionDate = new DateTime(2026, 7, 15),
            TransactionType = "Depreciation",
            Description = "Posted downstream depreciation evidence",
            Amount = -10m,
            ResultingBookValue = 90m,
            PerformedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var act = () => services.FixedAssets.RequestCapitalizationReversalAsync(
            fixture.Asset.Id,
            new RequestFixedAssetCapitalizationReversalDto
            {
                ReversalDate = new DateTime(2026, 7, 20),
                Reason = "Incorrect asset cost classification requires correction.",
                ImpactAssessment = "This request must be blocked because later asset value movements already exist."
            });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Reverse the later lifecycle entries in order*");
        (await db.FixedAssetCapitalizationReversals.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetCapitalizationReversal")]
    [Trait("Requirement", "FR-GL-008;FR-GL-010;FIN-LIM-0030")]
    public async Task ApInvoiceVoid_ShouldUseSharedReversalJournalAndSynchronizeAssetRegister()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedFixedAssetFoundationAsync(db, tenantId);
        // AP void uses the current accounting date for the linked reversal. Seed that period as
        // well as the invoice's July period so the test proves shared-journal behavior rather than
        // being stopped earlier by the independent period-control safeguard.
        SeedOpenPeriod(db, tenantId, startDate: new DateTime(2026, 8, 1));
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId, fixedAssetServiceRequired: true);
        await services.VendorInvoices.PostAsync(fixture.Invoice.Id);

        await services.VendorInvoices.VoidAsync(
            fixture.Invoice.Id,
            "Supplier invoice contained an incorrect fixed asset and must be fully reversed.");

        var events = await db.FinancePostingEvents
            .Where(item => item.TenantId == tenantId && item.SourceDocumentId == fixture.Invoice.Id)
            .OrderBy(item => item.PostedAt)
            .ToListAsync();
        events.Should().HaveCount(2, "AP owns one posting event and one reversal event; FA must not post a duplicate journal");
        var asset = await db.FixedAssets.SingleAsync(item => item.Id == fixture.Asset.Id);
        asset.Status.Should().Be(FixedAssetStatus.Draft);
        asset.AcquisitionCost.Should().Be(0m);
        asset.CapitalizationReversalPostingEventId.Should().Be(events.Single(item => item.PostingAction == "Reverse").Id);
        var line = await db.Set<VendorInvoiceLineItem>().SingleAsync(item => item.VendorInvoiceId == fixture.Invoice.Id);
        line.CapitalizationReversalPostingEventId.Should().Be(asset.CapitalizationReversalPostingEventId);
        (await db.AssetTransactions.CountAsync(item =>
            item.FixedAssetId == fixture.Asset.Id && item.TransactionType == "CapitalizationReversal")).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-capitalization-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ServiceFixture CreateServices(
        ApplicationDbContext db,
        Guid tenantId,
        bool fixedAssetServiceRequired = false,
        ITaxCalculationEngine? taxEngine = null,
        IWorkflowService? workflowService = null,
        Guid? userId = null,
        string userName = "fa.poster")
    {
        var currentUser = CreateCurrentUser(tenantId, userId, userName);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-capitalization" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var reversalPolicy = new FinanceReversalPolicyService(db, currentUser.Object);
        var fixedAssetService = new FixedAssetService(
            db,
            currentUser.Object,
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService,
            workflowService: workflowService,
            financeReversalPolicyService: reversalPolicy);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var vendorInvoiceService = new VendorInvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<VendorInvoiceService>>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IWorkflowService>(),
            postingEngine,
            auditService,
            taxEngine,
            fixedAssetServiceRequired ? fixedAssetService : null);

        return new ServiceFixture(vendorInvoiceService, fixedAssetService, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(
        Guid tenantId,
        Guid? userId = null,
        string userName = "fa.poster")
    {
        var resolvedUserId = (userId ?? Guid.NewGuid()).ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(resolvedUserId);
        currentUser.SetupGet(x => x.UserName).Returns(userName);
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-capitalization-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<FixedAssetCapitalizationFixture> SeedFixedAssetFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string tenantCode = "TEN",
        string invoiceNumber = "VI-FA-001",
        decimal lineTaxAmount = 0m,
        decimal totalAmount = 100m,
        string currencyCode = "GHS",
        decimal exchangeRate = 1m,
        bool makePostingAccountsMultiCurrency = false,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId, tenantCode, currencyCode == "GHS" ? "GHS" : "GHS");
        SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var assetCostAccount = SeedAccount(db, tenantId, "1600", AccountType.Asset, isMultiCurrency: makePostingAccountsMultiCurrency);
        var aucAccount = SeedAccount(db, tenantId, "1690", AccountType.Asset, isMultiCurrency: makePostingAccountsMultiCurrency);
        var accumAccount = SeedAccount(db, tenantId, "1699", AccountType.Asset, isControlAccount: true, allowDirectPosting: true);
        var depreciationExpenseAccount = SeedAccount(db, tenantId, "6700", AccountType.Expense);
        var expenseAccount = SeedAccount(db, tenantId, "6000", AccountType.Expense);
        var apAccount = SeedAccount(
            db,
            tenantId,
            "2000",
            AccountType.Liability,
            isControlAccount: true,
            allowDirectPosting: false,
            isMultiCurrency: makePostingAccountsMultiCurrency);
        var taxAccount = SeedAccount(db, tenantId, "1400", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var supplier = SeedSupplier(db, tenantId, apAccount.Id, expenseAccount.Id);

        if (makePostingAccountsMultiCurrency && !string.Equals(currencyCode, "GHS", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var account in new[] { assetCostAccount, aucAccount, expenseAccount, apAccount, taxAccount })
            {
                // The posting engine requires explicit account/currency authorization; marking an
                // account multi-currency is not enough. Seed the link so this fixture tests fixed-
                // asset currency snapshots rather than failing the independent GL configuration
                // safeguard introduced by the FX foundation.
                db.AccountCurrencyLinks.Add(new AccountCurrencyLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AccountId = account.Id,
                    LinkedCurrencyCode = currencyCode,
                    TransactionRateType = "Daily",
                    RevaluationRateType = "Month-End",
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "seed"
                });
            }
        }

        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apAccount.Id,
            ControlAccountTaxId = taxAccount.Id
        });

        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"COMP-{tenantCode}",
            Name = "Computer Hardware",
            AssetAccountId = assetCostAccount.Id,
            AccumulatedDepreciationAccountId = accumAccount.Id,
            DepreciationExpenseAccountId = depreciationExpenseAccount.Id,
            AucAccountId = aucAccount.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = 36,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-{tenantCode}-001",
            Name = "Laptop",
            FixedAssetCategoryId = category.Id,
            PurchaseDate = new DateTime(2026, 7, 5),
            PurchasePrice = 100m,
            AcquisitionCost = 100m,
            NetBookValue = 100m,
            UsefulLifeMonths = 36,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            Status = FixedAssetStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            SupplierInvoiceNumber = $"{invoiceNumber}-SUP",
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            InvoiceDate = new DateTime(2026, 7, 5),
            ReceivedDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            SubTotal = 100m,
            TaxAmount = lineTaxAmount,
            DiscountAmount = 0m,
            TotalAmount = totalAmount,
            PaidAmount = 0m,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            BaseCurrencyAmount = Math.Round(totalAmount * exchangeRate, 2),
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedDate = DateTime.UtcNow,
            ApAccountId = apAccount.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.LineItems.Add(new VendorInvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorInvoiceId = invoice.Id,
            LineItemType = "FixedAsset",
            GLAccountId = expenseAccount.Id,
            FixedAssetId = asset.Id,
            Description = "Laptop acquisition",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxRate = lineTaxAmount > 0m ? 20m : 0m,
            TaxAmount = lineTaxAmount,
            TaxTreatment = TaxTreatment.Standard,
            DiscountPercentage = 0m,
            DiscountAmount = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        db.FixedAssetCategories.Add(category);
        db.FixedAssets.Add(asset);
        db.VendorInvoices.Add(invoice);
        await db.SaveChangesAsync();

        return new FixedAssetCapitalizationFixture(
            invoice,
            supplier,
            asset,
            category,
            assetCostAccount,
            aucAccount,
            expenseAccount,
            apAccount,
            taxAccount);
    }

    private static Mock<ITaxCalculationEngine> CreateTaxEngine(Guid taxAccountId, bool isInputTaxDeductible)
    {
        var taxId = Guid.NewGuid();
        var taxGroupId = Guid.NewGuid();
        var taxEngine = new Mock<ITaxCalculationEngine>();
        taxEngine
            .Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxCalculationRequestDto request, CancellationToken _) => new TaxCalculationResultDto
            {
                BaseAmount = request.BaseAmount,
                TotalTaxAmount = 20m,
                GrandTotal = request.BaseAmount + 20m,
                EffectiveTaxRate = 20m,
                TaxGroupId = taxGroupId,
                TaxGroupName = "Ghana Standard VAT",
                TaxBreakdowns = new List<TaxBreakdownDto>
                {
                    new()
                    {
                        TaxId = taxId,
                        TaxCode = "VAT",
                        TaxName = "VAT",
                        TaxCategory = TaxCategory.Standard,
                        TaxReceivableAccountId = taxAccountId,
                        EffectiveFrom = new DateTime(2026, 1, 1),
                        TaxableAmount = request.BaseAmount,
                        TaxRate = 20m,
                        TaxAmount = 20m,
                        CompoundBasis = CompoundBasis.BaseOnly,
                        CalculationOrder = 1,
                        IsInputTaxDeductible = isInputTaxDeductible
                    }
                }
            });
        return taxEngine;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN", string baseCurrency = "GHS")
    {
        if (db.Tenants.Any(t => t.Id == tenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = baseCurrency
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false,
        DateTime? startDate = null)
    {
        var periodStart = (startDate ?? new DateTime(2026, 7, 1)).Date;
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = periodStart.ToString("MMMM yyyy"),
            PeriodCode = $"{periodStart:yyyy-MM}-{tenantId.ToString("N")[..4]}",
            PeriodNumber = periodStart.Month,
            PeriodType = PeriodType.Monthly,
            StartDate = periodStart,
            EndDate = periodEnd,
            PeriodDays = (periodEnd - periodStart).Days + 1,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        bool isControlAccount = false,
        bool allowDirectPosting = true,
        bool isMultiCurrency = false)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = $"{accountNumber}-{tenantId.ToString("N")[..4]}",
            AccountNumber = $"{accountNumber}-{tenantId.ToString("N")[..4]}",
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = status,
            CurrencyCode = "GHS",
            IsControlAccount = isControlAccount,
            AllowDirectPosting = allowDirectPosting,
            IsMultiCurrency = isMultiCurrency
        };

        db.Accounts.Add(account);
        return account;
    }

    private static Supplier SeedSupplier(
        ApplicationDbContext db,
        Guid tenantId,
        Guid apAccountId,
        Guid expenseAccountId)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = $"SUP-{tenantId.ToString("N")[..6]}",
            Name = "Test Supplier",
            SupplierType = "Vendor",
            IsActive = true,
            Status = "Active",
            DefaultApAccountId = apAccountId,
            DefaultExpenseAccountId = expenseAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.Suppliers.Add(supplier);
        return supplier;
    }

    private static ExchangeRate SeedExchangeRate(
        ApplicationDbContext db,
        Guid tenantId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        DateTime effectiveDate)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = baseCurrency,
            TargetCurrencyCode = targetCurrency,
            Rate = rate,
            InverseRate = Math.Round(1m / rate, 6),
            EffectiveDate = effectiveDate.Date,
            RateType = ExchangeRateType.Daily,
            RateSource = "Manual",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private sealed record ServiceFixture(
        VendorInvoiceService VendorInvoices,
        FixedAssetService FixedAssets,
        Mock<ISubledgerPostingService> SubledgerPosting);

    private sealed record FixedAssetCapitalizationFixture(
        VendorInvoice Invoice,
        Supplier Supplier,
        FixedAsset Asset,
        FixedAssetCategory Category,
        Account AssetCostAccount,
        Account AucAccount,
        Account ExpenseAccount,
        Account ApAccount,
        Account TaxAccount);
}
