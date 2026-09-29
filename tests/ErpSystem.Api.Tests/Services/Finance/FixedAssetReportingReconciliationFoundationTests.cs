using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetReportingReconciliationFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task FixedAssetRegisterIsTenantScopedAndAudited()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        await SeedReportingFoundationAsync(db, otherTenantId, "OTH");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetAssetRegisterAsync(DefaultQuery());

        report.Items.Should().OnlyContain(i => i.AssetCode.StartsWith("TEN-"));
        report.Items.Should().HaveCount(2);
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.FixedAssetRegisterReportGenerated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task FixedAssetRegisterUsesRequestedBookValuesAndExcludesMasterOnlyAssets()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var masterOnly = BuildAsset(
            tenantId,
            "TEN-MASTER-ONLY",
            "Master without accounting-book lineage",
            fixture.Category,
            1_710_000m,
            204_333.33m,
            1_505_666.67m);
        var localOnly = BuildAsset(
            tenantId,
            "TEN-LOCAL-ONLY",
            "Asset carried only in the local statutory book",
            fixture.Category,
            500_000m,
            50_000m,
            450_000m);
        var localBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "LOCAL_STATUTORY",
            Name = "Local Statutory",
            IsActive = true,
            AllowsPosting = true,
            SortOrder = 2,
            CreatedAt = DateTime.UtcNow
        };
        var localBookValue = BuildBookValue(
            tenantId,
            localBook,
            localOnly,
            500_000m,
            50_000m,
            450_000m,
            "FixedAsset",
            localOnly.Id,
            null);
        localBookValue.BookClassification = "LOCAL_STATUTORY";
        var apLocalBookValue = BuildBookValue(
            tenantId,
            localBook,
            fixture.ApAsset,
            9_000m,
            800m,
            8_200m,
            "FixedAsset",
            fixture.ApAsset.Id,
            null);
        apLocalBookValue.BookClassification = "LOCAL_STATUTORY";
        db.FixedAssets.AddRange(masterOnly, localOnly);
        db.AccountingBooks.Add(localBook);
        db.FixedAssetBookValues.AddRange(localBookValue, apLocalBookValue);
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetAssetRegisterAsync(DefaultQuery());
        var additions = await service.GetAdditionsReportAsync(CapitalizationQuery());
        var accumulated = await service.GetAccumulatedDepreciationReportAsync(DefaultQuery());
        var rollForward = await service.GetRollForwardReportAsync(DefaultQuery());
        var reconciliation = await service.GetGlReconciliationReportAsync(DefaultQuery());

        report.Items.Should().HaveCount(2);
        report.Items.Should().NotContain(item => item.AssetCode == masterOnly.AssetCode);
        report.Items.Should().NotContain(item => item.AssetCode == localOnly.AssetCode);
        report.Items.Should().OnlyContain(item => item.BookValueId.HasValue && item.BookClassification == "IFRS");
        report.TotalCost.Should().Be(2_000m);
        report.TotalAccumulatedDepreciation.Should().Be(100m);
        report.TotalNetBookValue.Should().Be(1_300m);
        additions.Items.Should().HaveCount(2);
        additions.Items.Should().NotContain(item => item.AssetCode == masterOnly.AssetCode || item.AssetCode == localOnly.AssetCode);
        accumulated.Items.Should().HaveCount(2);
        accumulated.Items.Should().NotContain(item => item.AssetCode == masterOnly.AssetCode || item.AssetCode == localOnly.AssetCode);
        rollForward.Rows.Should().ContainSingle();
        rollForward.Totals.OpeningCost.Should().Be(2_000m);
        reconciliation.Rows.Should().OnlyContain(row => row.Variance == 0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantAssetCategoryAndAccountFiltersAreRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var other = await SeedReportingFoundationAsync(db, otherTenantId, "OTH");
        var service = CreateReportsService(db, tenantId);

        var categoryAct = () => service.GetAssetRegisterAsync(Query(categoryId: other.Category.Id));
        await categoryAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*category*tenant*");
        var accountAct = () => service.GetAssetRegisterAsync(Query(accountId: other.Accounts.Asset.Id));
        await accountAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*account*tenant*");
        var assetAct = () => service.GetAssetRegisterAsync(Query(assetId: other.ApAsset.Id));
        await assetAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*asset*tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task AdditionsReportIncludesApAndDirectCapitalizationAndExcludesExpenseLines()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetAdditionsReportAsync(CapitalizationQuery());

        report.Items.Should().HaveCount(2);
        report.Items.Should().Contain(i => i.IsApSourced && i.CapitalizedCost == 1200m && i.PostedGlCost == 1200m);
        report.Items.Should().Contain(i => i.IsDirectCapitalization && i.CapitalizedCost == 800m && i.PostedGlCost == 800m);
        report.Items.Should().NotContain(i => string.Equals(i.SourceDocumentType, "Expense", StringComparison.OrdinalIgnoreCase));
        report.TotalVariance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationAndAccumulatedDepreciationReportsTiePostedSchedulesToGl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var depreciation = await service.GetDepreciationReportAsync(DefaultQuery());
        var accumulated = await service.GetAccumulatedDepreciationReportAsync(Query(assetId: fixture.ApAsset.Id));

        depreciation.Items.Should().ContainSingle(i =>
            i.FixedAssetId == fixture.ApAsset.Id &&
            i.DepreciationAmount == 100m &&
            i.PostedExpenseDebit == 100m &&
            i.PostedAccumulatedDepreciationCredit == 100m &&
            i.Variance == 0m);
        accumulated.Items.Single().SubledgerAccumulatedDepreciation.Should().Be(100m);
        accumulated.Items.Single().PostedGlAccumulatedDepreciation.Should().Be(100m);
        accumulated.TotalVariance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationAndImpairmentMovementReportTiesRecordsToGl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetValuationMovementReportAsync(DefaultQuery());

        report.Items.Should().Contain(i =>
            i.FixedAssetId == fixture.ApAsset.Id &&
            i.ValuationType == ValuationType.Revaluation &&
            i.RevaluationSurplus == 200m &&
            i.PostedGlMovement == 200m);
        report.Items.Should().Contain(i =>
            i.FixedAssetId == fixture.DirectAsset.Id &&
            i.ValuationType == ValuationType.Impairment &&
            i.ImpairmentLoss == 50m &&
            i.PostedGlMovement == 50m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task TransferReportShowsCustodySegmentHistoryWithoutGlMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetTransferReportAsync(DefaultQuery());

        report.Should().ContainSingle(i =>
            i.FixedAssetId == fixture.ApAsset.Id &&
            i.FromLocation == "Warehouse A" &&
            i.ToLocation == "Branch B" &&
            i.FromSegmentString == "OPS" &&
            i.ToSegmentString == "ADMIN" &&
            !i.HasGlImpact &&
            !i.JournalEntryId.HasValue &&
            !i.PostingEventId.HasValue);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalReportShowsProceedsNbvGainAndPresentationWarning()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetDisposalReportAsync(DefaultQuery());
        var disposal = report.Single(i => i.FixedAssetId == fixture.DirectAsset.Id);

        disposal.NetProceeds.Should().Be(900m);
        disposal.NetBookValue.Should().Be(750m);
        disposal.GainLoss.Should().Be(150m);
        disposal.HasPostedGlReference.Should().BeTrue();
        disposal.GainLossPresentation.Should().Contain("non-operating");
        disposal.PresentationWarning.Should().Contain("expense-class");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task RegisterShowsDisposedAssetWithZeroCurrentNbv()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetAssetRegisterAsync(Query(assetId: fixture.DirectAsset.Id));

        report.Items.Single().Status.Should().Be(FixedAssetStatus.Disposed);
        report.Items.Single().NetBookValue.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task RollForwardTiesOpeningPlusMovementsToClosingAndCategoryTotals()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetRollForwardReportAsync(DefaultQuery());
        var row = report.Rows.Single();

        row.OpeningCost.Should().Be(2000m);
        row.RevaluationIncrease.Should().Be(200m);
        row.DepreciationCharge.Should().Be(100m);
        row.ImpairmentAdditions.Should().Be(50m);
        row.Disposals.Should().Be(800m);
        row.ClosingCost.Should().Be(row.OpeningCost + row.Additions + row.RevaluationIncrease - row.RevaluationDecrease - row.Disposals);
        row.ClosingNetBookValue.Should().Be(1300m);
        report.Totals.ClosingNetBookValue.Should().Be(row.ClosingNetBookValue);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetRollForwardReportGenerated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task GlReconciliationShowsZeroVarianceForCleanData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetGlReconciliationReportAsync(DefaultQuery());

        report.Rows.Should().OnlyContain(r => r.Variance == 0m);
        report.Diagnostics.Should().NotContain(d => d.Severity == "Critical");
        report.Diagnostics.Should().Contain(d => d.Code == "FA-REPORT-DISPOSAL-GAIN-PRESENTATION");
        report.IsReconciled.Should().BeTrue();
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetGlReconciliationReportGenerated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationSurplusReconciliationSubtractsCompletedDisposalEquityTransfer()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var retainedEarnings = SeedAccount(db, tenantId, "TEN-3100", "Retained Earnings", AccountType.Equity, "Equity");
        var disposalId = Guid.NewGuid();
        var (journalId, postingEventId) = SeedJournal(
            db,
            tenantId,
            fixture.Period.Id,
            new DateTime(2026, 7, 28),
            "FixedAssets",
            "FixedAssetDisposal",
            disposalId,
            "Disposal",
            ("Transfer asset reserve", fixture.Accounts.RevaluationSurplus.Id, 200m, 0m, $"FixedAssetId={fixture.ApAsset.Id:N};AssetDisposalId={disposalId:N};Book=IFRS", "FA-DisposalRevaluationSurplus", fixture.ApAsset.AssetCode),
            ("Transfer to retained earnings", retainedEarnings.Id, 0m, 200m, $"FixedAssetId={fixture.ApAsset.Id:N};AssetDisposalId={disposalId:N};Book=IFRS", "FA-DisposalRetainedEarnings", fixture.ApAsset.AssetCode));

        // This focused disposal record represents the asset-specific equity movement. The asset
        // register status is intentionally irrelevant here: the reconciliation must compare the
        // reserve GL after the transfer with the reserve subledger after the same transfer.
        db.AssetDisposals.Add(new AssetDisposal
        {
            Id = disposalId,
            TenantId = tenantId,
            FixedAssetId = fixture.ApAsset.Id,
            DisposalDate = new DateTime(2026, 7, 28),
            AccountingDate = new DateTime(2026, 7, 28),
            FiscalPeriodId = fixture.Period.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            DisposalType = DisposalType.Scrap,
            Status = AssetDisposalStatus.Completed,
            Reason = "Focused reserve reconciliation fixture",
            ProceedsCurrencyCode = "GHS",
            RevaluationSurplusAtDisposal = 200m,
            RevaluationSurplusAccountId = fixture.Accounts.RevaluationSurplus.Id,
            RetainedEarningsAccountId = retainedEarnings.Id,
            RevaluationSurplusTransferAmount = 200m,
            JournalEntryId = journalId,
            PostingEventId = postingEventId,
            PostedAt = new DateTime(2026, 7, 28),
            CompletedAt = new DateTime(2026, 7, 28),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetGlReconciliationReportAsync(Query(accountId: fixture.Accounts.RevaluationSurplus.Id));
        var reserve = report.Rows.Single(r => r.AccountId == fixture.Accounts.RevaluationSurplus.Id);

        reserve.SubledgerBalance.Should().Be(0m);
        reserve.GlBalance.Should().Be(0m);
        reserve.Variance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task ReconciliationDetectsMissingPostingReferencesAndSubledgerWithoutGl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        await SeedUnpostedSubledgerAssetAsync(db, fixture, "TEN-MISSING");
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetGlReconciliationReportAsync(DefaultQuery());

        report.Diagnostics.Should().Contain(d => d.Code == "FA-REPORT-MISSING-POSTING-REFERENCE");
        report.Diagnostics.Should().Contain(d => d.Code == "FA-REPORT-SUBLEDGER-WITHOUT-GL");
        report.Diagnostics.Should().Contain(d => d.Code == "FA-REPORT-GL-VARIANCE");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetVarianceDiagnosticGenerated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task ReconciliationDetectsGlMovementWithoutFixedAssetSourceReference()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        SeedJournal(
            db,
            tenantId,
            fixture.Period.Id,
            new DateTime(2026, 7, 28),
            "FixedAssets",
            sourceDocumentType: null,
            sourceDocumentId: null,
            postingAction: "ManualAdjustment",
            ("Unreferenced FA debit", fixture.Accounts.Asset.Id, 25m, 0m, null, "FA-Unreferenced", null),
            ("Offset", fixture.Accounts.Clearing.Id, 0m, 25m, null, "FA-Unreferenced", null));
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetGlReconciliationReportAsync(DefaultQuery());

        report.Rows.Single(r => r.Area == "Asset Cost/Carrying").GlWithoutSourceReferenceCount.Should().BeGreaterThan(0);
        report.Diagnostics.Should().Contain(d => d.Code == "FA-REPORT-GL-WITHOUT-ASSET-SOURCE");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task ReportsDoNotRelySolelyOnMutableBookValueFields()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var bookValue = await db.FixedAssetBookValues.SingleAsync(b => b.FixedAssetId == fixture.ApAsset.Id);
        bookValue.AccumulatedDepreciation = 999m;
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        var accumulated = await service.GetAccumulatedDepreciationReportAsync(Query(assetId: fixture.ApAsset.Id));
        var reconciliation = await service.GetGlReconciliationReportAsync(DefaultQuery());

        accumulated.Items.Single().PostedGlAccumulatedDepreciation.Should().Be(100m);
        accumulated.Items.Single().SubledgerAccumulatedDepreciation.Should().Be(999m);
        accumulated.TotalVariance.Should().Be(-899m);
        reconciliation.Diagnostics.Should().Contain(d => d.Code == "FA-REPORT-GL-VARIANCE");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task LaterPeriodRollForwardCarriesOpeningReservesAndReconciliationRemainsCumulative()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);
        var query = new FixedAssetReportQueryDto
        {
            FromDate = new DateTime(2026, 8, 1),
            ToDate = new DateTime(2026, 8, 31),
            BookClassification = "IFRS"
        };

        var rollForward = await service.GetRollForwardReportAsync(query);
        var reconciliation = await service.GetGlReconciliationReportAsync(query);

        var row = rollForward.Rows.Single();
        row.OpeningCost.Should().Be(1_400m);
        row.OpeningAccumulatedDepreciation.Should().Be(100m);
        row.OpeningAccumulatedImpairment.Should().Be(0m);
        row.Additions.Should().Be(0m);
        row.DepreciationCharge.Should().Be(0m);
        row.Disposals.Should().Be(0m);
        row.ClosingCost.Should().Be(1_400m);
        row.ClosingAccumulatedDepreciation.Should().Be(100m);
        row.ClosingNetBookValue.Should().Be(1_300m);
        reconciliation.Rows.Should().OnlyContain(item => item.Variance == 0m);
        reconciliation.IsReconciled.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task SoftDeletedTransactionsAndCorrectedValuationsAreExcludedFromReports()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        foreach (var transfer in db.AssetTransfers)
            transfer.IsDeleted = true;
        foreach (var disposal in db.AssetDisposals)
            disposal.IsDeleted = true;
        foreach (var valuation in db.AssetValuations)
            valuation.IsCorrected = true;
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        (await service.GetTransferReportAsync(DefaultQuery())).Should().BeEmpty();
        (await service.GetDisposalReportAsync(DefaultQuery())).Should().BeEmpty();
        (await service.GetValuationMovementReportAsync(DefaultQuery())).Items.Should().BeEmpty();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task InvalidDateFiscalPeriodAndAccountingBookFiltersAreRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var other = await SeedReportingFoundationAsync(db, otherTenantId, "OTH");
        var otherOnlyBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Code = "OTH_ONLY",
            Name = "Other Tenant Only",
            IsActive = true,
            AllowsPosting = true,
            CreatedAt = DateTime.UtcNow
        };
        db.AccountingBooks.Add(otherOnlyBook);
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        var dateAct = () => service.GetAssetRegisterAsync(new FixedAssetReportQueryDto
        {
            FromDate = new DateTime(2026, 8, 2),
            ToDate = new DateTime(2026, 8, 1),
            BookClassification = "IFRS"
        });
        await dateAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*start date*");

        var periodAct = () => service.GetAssetRegisterAsync(new FixedAssetReportQueryDto
        {
            FiscalPeriodId = other.Period.Id,
            BookClassification = "IFRS"
        });
        await periodAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*fiscal period*tenant*");

        var bookAct = () => service.GetAssetRegisterAsync(new FixedAssetReportQueryDto
        {
            BookClassification = otherOnlyBook.Code
        });
        await bookAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*accounting book*tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task GlEvidenceRequiresPostedParentJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var journal = await db.JournalEntries.SingleAsync(entry => entry.Id == fixture.ApAsset.JournalEntryId);
        journal.PostingStatus = "Draft";
        await db.SaveChangesAsync();
        var service = CreateReportsService(db, tenantId);

        var report = await service.GetGlReconciliationReportAsync(DefaultQuery());

        report.Diagnostics.Should().Contain(item => item.Code == "FA-REPORT-GL-VARIANCE");
        report.IsReconciled.Should().BeFalse();
    }

    [Theory]
    [InlineData("AssetRegister")]
    [InlineData("AdditionsReport")]
    [InlineData("DepreciationReport")]
    [InlineData("AccumulatedDepreciationReport")]
    [InlineData("ValuationReport")]
    [InlineData("DisposalReport")]
    [InlineData("TransferReport")]
    [InlineData("RollForwardReport")]
    [InlineData("GlReconciliationReport")]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task EveryFixedAssetReportSupportsExcelExport(string reportType)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var bytes = await service.ExportToExcelAsync(reportType, DefaultQuery());

        bytes.Should().NotBeEmpty();
        bytes.Take(2).Should().Equal((byte)'P', (byte)'K');
    }

    [Theory]
    [InlineData("DisposalReport")]
    [InlineData("TransferReport")]
    [InlineData("GlReconciliationReport")]
    [Trait("Batch", "FinanceGoLive-FixedAssetReporting")]
    [Trait("Category", "FixedAssets")]
    public async Task PreviouslyUnavailableReportsSupportPdfExport(string reportType)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedReportingFoundationAsync(db, tenantId, "TEN");
        var service = CreateReportsService(db, tenantId);

        var bytes = await service.ExportToPdfAsync(reportType, DefaultQuery());

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray()).Should().Be("%PDF");
    }

    private static FixedAssetReportQueryDto DefaultQuery() => new()
    {
        FromDate = new DateTime(2026, 7, 1),
        ToDate = new DateTime(2026, 7, 31),
        BookClassification = "IFRS"
    };

    private static FixedAssetReportQueryDto CapitalizationQuery() => new()
    {
        FromDate = new DateTime(2026, 6, 1),
        ToDate = new DateTime(2026, 7, 31),
        BookClassification = "IFRS"
    };

    private static FixedAssetReportQueryDto Query(Guid? assetId = null, Guid? categoryId = null, Guid? accountId = null)
    {
        var query = DefaultQuery();
        query.AssetId = assetId;
        query.CategoryId = categoryId;
        query.AccountId = accountId;
        return query;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-reporting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FixedAssetReportsService CreateReportsService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-reporting" }
            });

        return new FixedAssetReportsService(db, currentUser.Object, auditService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fa.reporting");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-reporting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        currentUser.SetupGet(x => x.EmployeeId).Returns(Guid.NewGuid());
        return currentUser;
    }

    private static async Task<ReportingFixture> SeedReportingFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string prefix)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"{prefix} Tenant",
            Code = prefix,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS",
            CreatedAt = DateTime.UtcNow
        });

        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        };
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS",
            IsActive = true,
            IsDefault = true,
            AllowsPosting = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        var accounts = SeedAccounts(db, tenantId, prefix);
        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"{prefix}-CAT",
            Name = $"{prefix} Plant",
            AssetAccountId = accounts.Asset.Id,
            AccumulatedDepreciationAccountId = accounts.AccumulatedDepreciation.Id,
            DepreciationExpenseAccountId = accounts.DepreciationExpense.Id,
            GainOnDisposalAccountId = accounts.GainOnDisposal.Id,
            LossOnDisposalAccountId = accounts.LossOnDisposal.Id,
            DisposalProceedsClearingAccountId = accounts.ProceedsClearing.Id,
            RevaluationSurplusAccountId = accounts.RevaluationSurplus.Id,
            RevaluationLossAccountId = accounts.RevaluationLoss.Id,
            ImpairmentLossAccountId = accounts.ImpairmentLoss.Id,
            AccumulatedImpairmentAccountId = accounts.AccumulatedImpairment.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var apInvoiceId = Guid.NewGuid();
        var apInvoiceLineId = Guid.NewGuid();
        var apAsset = BuildAsset(tenantId, $"{prefix}-AP-001", "AP acquired asset", category, 1200m, 100m, 1300m);
        apAsset.SourceDocumentType = "VendorInvoice";
        apAsset.SourceDocumentId = apInvoiceId;
        apAsset.SourceDocumentLineId = apInvoiceLineId;

        var directAsset = BuildAsset(tenantId, $"{prefix}-DIR-001", "Direct capitalized asset", category, 800m, 0m, 0m);
        directAsset.Status = FixedAssetStatus.Disposed;
        directAsset.DisposalDate = new DateTime(2026, 7, 25);
        directAsset.SourceDocumentType = "FixedAsset";
        directAsset.SourceDocumentId = directAsset.Id;

        var fixture = new ReportingFixture(period, book, category, accounts, apAsset, directAsset);
        db.FiscalPeriods.Add(period);
        db.AccountingBooks.Add(book);
        db.FixedAssetCategories.Add(category);
        db.FixedAssets.AddRange(apAsset, directAsset);
        db.FixedAssetBookValues.AddRange(
            BuildBookValue(tenantId, book, apAsset, 1200m, 100m, 1300m, "VendorInvoice", apInvoiceId, apInvoiceLineId),
            BuildBookValue(tenantId, book, directAsset, 800m, 0m, 0m, "FixedAsset", directAsset.Id, null));

        SeedCapitalization(db, fixture, apAsset, apInvoiceId, "VendorInvoice", 1200m, $"FixedAssetId={apAsset.Id:N};VendorInvoiceLineId={apInvoiceLineId:N};Book=IFRS");
        SeedCapitalization(db, fixture, directAsset, directAsset.Id, "FixedAsset", 800m, $"FixedAssetId={directAsset.Id:N};Book=IFRS");
        SeedDepreciation(db, fixture, apAsset, 100m);
        SeedRevaluation(db, fixture, apAsset, 200m);
        SeedImpairment(db, fixture, directAsset, 50m);
        SeedTransfer(db, fixture, apAsset);
        SeedDisposal(db, fixture, directAsset);

        await db.SaveChangesAsync();

        return fixture;
    }

    private static FixedAsset BuildAsset(Guid tenantId, string code, string name, FixedAssetCategory category, decimal cost, decimal accumulatedDepreciation, decimal netBookValue)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = code,
            Name = name,
            Location = "Warehouse A",
            CurrentSegmentString = "OPS",
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 6, 15),
            PlacedInServiceDate = new DateTime(2026, 7, 1),
            PurchasePrice = cost,
            AcquisitionCost = cost,
            NetBookValue = netBookValue,
            UsefulLifeMonths = 12,
            ResidualValue = 0m,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            Status = FixedAssetStatus.Active,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            CapitalizationDate = new DateTime(2026, 6, 30),
            CapitalizedAt = new DateTime(2026, 6, 30),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

    private static FixedAssetBookValue BuildBookValue(
        Guid tenantId,
        AccountingBook book,
        FixedAsset asset,
        decimal acquisitionCost,
        decimal accumulatedDepreciation,
        decimal netBookValue,
        string sourceDocumentType,
        Guid? sourceDocumentId,
        Guid? sourceDocumentLineId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = book.Id,
            BookClassification = "IFRS",
            AcquisitionCost = acquisitionCost,
            AccumulatedDepreciation = accumulatedDepreciation,
            NetBookValue = netBookValue,
            ResidualValue = 0m,
            UsefulLifeMonths = 12,
            RemainingUsefulLifeMonths = 10,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            CapitalizationDate = asset.CapitalizationDate,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentLineId = sourceDocumentLineId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

    private static void SeedCapitalization(
        ApplicationDbContext db,
        ReportingFixture fixture,
        FixedAsset asset,
        Guid sourceDocumentId,
        string sourceDocumentType,
        decimal amount,
        string notes)
    {
        var (journalId, postingEventId) = SeedJournal(
            db,
            asset.TenantId,
            fixture.Period.Id,
            new DateTime(2026, 6, 30),
            sourceDocumentType == "VendorInvoice" ? "AP" : "FA",
            sourceDocumentType,
            sourceDocumentId,
            "Capitalize",
            ("Asset capitalization", fixture.Accounts.Asset.Id, amount, 0m, notes, "FA-Capitalization", asset.AssetCode),
            ("Capitalization clearing", fixture.Accounts.Clearing.Id, 0m, amount, null, "FA-Capitalization-Clearing", asset.AssetCode));

        asset.JournalEntryId = journalId;
        asset.PostingEventId = postingEventId;
    }

    private static void SeedDepreciation(ApplicationDbContext db, ReportingFixture fixture, FixedAsset asset, decimal amount)
    {
        var runId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (journalId, postingEventId) = SeedJournal(
            db,
            asset.TenantId,
            fixture.Period.Id,
            new DateTime(2026, 7, 31),
            "FixedAssets",
            "FixedAssetDepreciationRun",
            runId,
            "Depreciation",
            ("Depreciation expense", fixture.Accounts.DepreciationExpense.Id, amount, 0m, $"FixedAssetId={asset.Id:N};DepreciationRunId={runId:N};ScheduleId={scheduleId:N};Book=IFRS", "FA-Depreciation", asset.AssetCode),
            ("Accumulated depreciation", fixture.Accounts.AccumulatedDepreciation.Id, 0m, amount, $"FixedAssetId={asset.Id:N};DepreciationRunId={runId:N};ScheduleId={scheduleId:N};Book=IFRS", "FA-AccumulatedDepreciation", asset.AssetCode));

        db.FixedAssetDepreciationRuns.Add(new FixedAssetDepreciationRun
        {
            Id = runId,
            TenantId = asset.TenantId,
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            PostingDate = new DateTime(2026, 7, 31),
            Status = "Posted",
            TotalDepreciationAmount = amount,
            JournalEntryId = journalId,
            PostingEventId = postingEventId,
            IdempotencyKey = $"DEP-{asset.Id:N}",
            CreatedAt = DateTime.UtcNow
        });
        db.AssetDepreciationSchedules.Add(new AssetDepreciationSchedule
        {
            Id = scheduleId,
            TenantId = asset.TenantId,
            FixedAssetId = asset.Id,
            FixedAssetDepreciationRunId = runId,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.Period.Id,
            DepreciationAmount = amount,
            AccumulatedDepreciationBefore = 0m,
            AccumulatedDepreciation = amount,
            NetBookValueBefore = 1200m,
            NetBookValue = 1100m,
            DepreciableAmount = 1200m,
            UsefulLifeMonthsSnapshot = 12,
            DepreciationMethodSnapshot = DepreciationMethod.StraightLine,
            IsPosted = true,
            PostingDate = new DateTime(2026, 7, 31),
            PostedDate = new DateTime(2026, 7, 31),
            JournalEntryId = journalId,
            PostingEventId = postingEventId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedRevaluation(ApplicationDbContext db, ReportingFixture fixture, FixedAsset asset, decimal amount)
    {
        var valuationId = Guid.NewGuid();
        var (journalId, postingEventId) = SeedJournal(
            db,
            asset.TenantId,
            fixture.Period.Id,
            new DateTime(2026, 7, 15),
            "FixedAssets",
            "FixedAssetValuation",
            valuationId,
            "Revaluation",
            ("Revaluation increase", fixture.Accounts.Asset.Id, amount, 0m, $"FixedAssetId={asset.Id:N};ValuationId={valuationId:N};Book=IFRS", "FA-RevaluationAsset", asset.AssetCode),
            ("Revaluation surplus", fixture.Accounts.RevaluationSurplus.Id, 0m, amount, $"FixedAssetId={asset.Id:N};ValuationId={valuationId:N};Book=IFRS", "FA-RevaluationSurplus", asset.AssetCode));

        db.AssetValuations.Add(new AssetValuation
        {
            Id = valuationId,
            TenantId = asset.TenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.Period.Id,
            ValuationDate = new DateTime(2026, 7, 15),
            AccountingDate = new DateTime(2026, 7, 15),
            ValuationType = ValuationType.Revaluation,
            CarryingAmountBefore = 1100m,
            AccumulatedDepreciationBefore = 100m,
            NetBookValueBefore = 1100m,
            FairValue = 1300m,
            CarryingAmountAfter = 1300m,
            RevaluationSurplus = amount,
            AdjustmentAmount = amount,
            IsPostedToGL = true,
            Status = "Posted",
            JournalEntryId = journalId,
            PostingEventId = postingEventId,
            PerformedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedImpairment(ApplicationDbContext db, ReportingFixture fixture, FixedAsset asset, decimal amount)
    {
        var valuationId = Guid.NewGuid();
        var (journalId, postingEventId) = SeedJournal(
            db,
            asset.TenantId,
            fixture.Period.Id,
            new DateTime(2026, 7, 18),
            "FixedAssets",
            "FixedAssetValuation",
            valuationId,
            "Impairment",
            ("Impairment loss", fixture.Accounts.ImpairmentLoss.Id, amount, 0m, $"FixedAssetId={asset.Id:N};ValuationId={valuationId:N};Book=IFRS", "FA-ImpairmentLoss", asset.AssetCode),
            ("Accumulated impairment", fixture.Accounts.AccumulatedImpairment.Id, 0m, amount, $"FixedAssetId={asset.Id:N};ValuationId={valuationId:N};Book=IFRS", "FA-AccumulatedImpairment", asset.AssetCode));

        db.AssetValuations.Add(new AssetValuation
        {
            Id = valuationId,
            TenantId = asset.TenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.Period.Id,
            ValuationDate = new DateTime(2026, 7, 18),
            AccountingDate = new DateTime(2026, 7, 18),
            ValuationType = ValuationType.Impairment,
            CarryingAmountBefore = 800m,
            NetBookValueBefore = 800m,
            FairValue = 750m,
            CarryingAmountAfter = 750m,
            ImpairmentLoss = amount,
            AdjustmentAmount = amount,
            IsPostedToGL = true,
            Status = "Posted",
            JournalEntryId = journalId,
            PostingEventId = postingEventId,
            PerformedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedTransfer(ApplicationDbContext db, ReportingFixture fixture, FixedAsset asset)
    {
        db.AssetTransfers.Add(new AssetTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = asset.TenantId,
            FixedAssetId = asset.Id,
            TransferDate = new DateTime(2026, 7, 20),
            TransferType = AssetTransferType.SegmentMovement,
            Status = AssetTransferStatus.Completed,
            FromLocation = "Warehouse A",
            ToLocation = "Branch B",
            FromSegmentString = "OPS",
            ToSegmentString = "ADMIN",
            Reason = "Custody move",
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedDisposal(ApplicationDbContext db, ReportingFixture fixture, FixedAsset asset)
    {
        var disposalId = Guid.NewGuid();
        var (journalId, postingEventId) = SeedJournal(
            db,
            asset.TenantId,
            fixture.Period.Id,
            new DateTime(2026, 7, 25),
            "FixedAssets",
            "FixedAssetDisposal",
            disposalId,
            "Disposal",
            ("Proceeds clearing", fixture.Accounts.ProceedsClearing.Id, 900m, 0m, $"FixedAssetId={asset.Id:N};AssetDisposalId={disposalId:N};Book=IFRS", "FA-DisposalProceeds", asset.AssetCode),
            ("Accumulated impairment clear", fixture.Accounts.AccumulatedImpairment.Id, 50m, 0m, $"FixedAssetId={asset.Id:N};AssetDisposalId={disposalId:N};Book=IFRS", "FA-DisposalAccumulatedImpairment", asset.AssetCode),
            ("Asset derecognition", fixture.Accounts.Asset.Id, 0m, 800m, $"FixedAssetId={asset.Id:N};AssetDisposalId={disposalId:N};Book=IFRS", "FA-DisposalAsset", asset.AssetCode),
            ("Disposal gain", fixture.Accounts.GainOnDisposal.Id, 0m, 150m, $"FixedAssetId={asset.Id:N};AssetDisposalId={disposalId:N};Book=IFRS", "FA-DisposalGain", asset.AssetCode));

        db.AssetDisposals.Add(new AssetDisposal
        {
            Id = disposalId,
            TenantId = asset.TenantId,
            FixedAssetId = asset.Id,
            DisposalDate = new DateTime(2026, 7, 25),
            AccountingDate = new DateTime(2026, 7, 25),
            FiscalPeriodId = fixture.Period.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            DisposalType = DisposalType.Sale,
            Status = AssetDisposalStatus.Completed,
            Reason = "Sale",
            SaleProceeds = 900m,
            NetProceeds = 900m,
            ProceedsCurrencyCode = "GHS",
            ProceedsFunctionalAmount = 900m,
            ProceedsAccountId = fixture.Accounts.ProceedsClearing.Id,
            CostAtDisposal = 800m,
            AccumulatedDepreciationAtDisposal = 0m,
            AccumulatedImpairmentAtDisposal = 50m,
            NetBookValueAtDisposal = 750m,
            GainOrLoss = 150m,
            JournalEntryId = journalId,
            PostingEventId = postingEventId,
            PostedAt = new DateTime(2026, 7, 25),
            CompletedAt = new DateTime(2026, 7, 25),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static async Task SeedUnpostedSubledgerAssetAsync(ApplicationDbContext db, ReportingFixture fixture, string code)
    {
        var asset = BuildAsset(fixture.Category.TenantId, code, "Missing GL asset", fixture.Category, 300m, 0m, 300m);
        asset.JournalEntryId = null;
        asset.PostingEventId = null;
        var book = BuildBookValue(fixture.Category.TenantId, fixture.Book, asset, 300m, 0m, 300m, "FixedAsset", asset.Id, null);
        db.FixedAssets.Add(asset);
        db.FixedAssetBookValues.Add(book);
        await db.SaveChangesAsync();
    }

    private static (Guid JournalEntryId, Guid PostingEventId) SeedJournal(
        ApplicationDbContext db,
        Guid tenantId,
        Guid fiscalPeriodId,
        DateTime postingDate,
        string sourceModule,
        string? sourceDocumentType,
        Guid? sourceDocumentId,
        string postingAction,
        params (string Description, Guid AccountId, decimal Debit, decimal Credit, string? Notes, string? Tag, string? Reference)[] lines)
    {
        var journalId = Guid.NewGuid();
        var postingEventId = Guid.NewGuid();
        var accountingBookId = db.AccountingBooks.Local
            .Single(book => book.TenantId == tenantId && book.Code == "IFRS")
            .Id;
        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);

        db.JournalEntries.Add(new JournalEntry
        {
            Id = journalId,
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{journalId:N}"[..16],
            JournalType = "Fixed Asset Reporting Seed",
            EntryDate = postingDate,
            PostingDate = postingDate,
            Description = "Fixed asset reporting seed journal",
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            FiscalPeriodId = fiscalPeriodId,
            TotalDebitAmount = totalDebit,
            TotalCreditAmount = totalCredit,
            IsBalanced = totalDebit == totalCredit,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            AccountingBookId = accountingBookId,
            CreatedAt = DateTime.UtcNow
        });

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            db.AccountTransactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = line.AccountId,
                JournalEntryId = journalId,
                TransactionDate = postingDate,
                DebitAmount = line.Debit,
                CreditAmount = line.Credit,
                FunctionalCurrencyCode = "GHS",
                TransactionCurrency = "GHS",
                TransactionDebitAmount = line.Debit,
                TransactionCreditAmount = line.Credit,
                Description = line.Description,
                FiscalPeriodId = fiscalPeriodId,
                PostedDate = postingDate,
                PostingStatus = "Posted",
                SourceModule = sourceModule,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = sourceDocumentId,
                SourceReferenceNumber = line.Reference,
                BookClassification = "IFRS",
                AccountingBookId = accountingBookId,
                LineNumber = i + 1,
                TransactionTag = line.Tag,
                Notes = line.Notes,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (sourceDocumentId.HasValue && !string.IsNullOrWhiteSpace(sourceDocumentType))
        {
            db.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = postingEventId,
                TenantId = tenantId,
                SourceModule = sourceModule,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = sourceDocumentId.Value,
                PostingAction = postingAction,
                JournalEntryId = journalId,
                PostingStatus = "Posted",
                PostingDate = postingDate,
                PostedAt = postingDate,
                TotalDebitAmount = totalDebit,
                TotalCreditAmount = totalCredit,
                FunctionalCurrencyCode = "GHS",
                BookClassification = "IFRS",
                AccountingBookId = accountingBookId,
                CreatedAt = DateTime.UtcNow
            });
        }

        return (journalId, postingEventId);
    }

    private static ReportingAccounts SeedAccounts(ApplicationDbContext db, Guid tenantId, string prefix)
    {
        var asset = SeedAccount(db, tenantId, $"{prefix}-1600", "Fixed Asset Cost", AccountType.Asset, "Fixed Assets");
        var accumulatedDepreciation = SeedAccount(db, tenantId, $"{prefix}-1699", "Accumulated Depreciation", AccountType.Asset, "Fixed Assets");
        var depreciationExpense = SeedAccount(db, tenantId, $"{prefix}-6700", "Depreciation Expense", AccountType.Expense, "Depreciation");
        var revaluationSurplus = SeedAccount(db, tenantId, $"{prefix}-3300", "Revaluation Surplus", AccountType.Equity, "Revaluation");
        var revaluationLoss = SeedAccount(db, tenantId, $"{prefix}-6710", "Revaluation Loss", AccountType.Expense, "Revaluation");
        var impairmentLoss = SeedAccount(db, tenantId, $"{prefix}-6720", "Impairment Loss", AccountType.Expense, "Impairment");
        var accumulatedImpairment = SeedAccount(db, tenantId, $"{prefix}-1698", "Accumulated Impairment", AccountType.Asset, "Fixed Assets");
        var proceedsClearing = SeedAccount(db, tenantId, $"{prefix}-1300", "Disposal Proceeds Clearing", AccountType.Asset, "Fixed Assets");
        var gainOnDisposal = SeedAccount(db, tenantId, $"{prefix}-6790", "Disposal Gain Presentation", AccountType.Expense, "Disposal Gain Contra Presentation");
        var lossOnDisposal = SeedAccount(db, tenantId, $"{prefix}-6730", "Disposal Loss", AccountType.Expense, "Disposal");
        var clearing = SeedAccount(db, tenantId, $"{prefix}-2999", "Capitalization Clearing", AccountType.Liability, "Clearing");
        return new ReportingAccounts(asset, accumulatedDepreciation, depreciationExpense, revaluationSurplus, revaluationLoss, impairmentLoss, accumulatedImpairment, proceedsClearing, gainOnDisposal, lossOnDisposal, clearing);
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string number, string name, AccountType type, string category)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = type,
            AccountCategory = category,
            Status = AccountStatus.Active,
            AllowDirectPosting = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Accounts.Add(account);
        return account;
    }

    private sealed record ReportingFixture(
        FiscalPeriod Period,
        AccountingBook Book,
        FixedAssetCategory Category,
        ReportingAccounts Accounts,
        FixedAsset ApAsset,
        FixedAsset DirectAsset);

    private sealed record ReportingAccounts(
        Account Asset,
        Account AccumulatedDepreciation,
        Account DepreciationExpense,
        Account RevaluationSurplus,
        Account RevaluationLoss,
        Account ImpairmentLoss,
        Account AccumulatedImpairment,
        Account ProceedsClearing,
        Account GainOnDisposal,
        Account LossOnDisposal,
        Account Clearing);
}
