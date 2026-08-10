using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
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

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetDisposalFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalCannotRunBeforeCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, isCapitalized: false, status: FixedAssetStatus.Draft);
        var services = CreateServices(db, tenantId);

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalized*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task AlreadyDisposedAssetCannotBeDisposedAgain()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*again*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposedAssetCannotBeTransferredDepreciatedOrRevalued()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var transferAct = () => services.Transfers.RequestTransferAsync(new RequestAssetTransferDto
        {
            FixedAssetId = fixture.Asset.Id,
            TransferDate = new DateTime(2026, 7, 15),
            TransferType = AssetTransferType.Custodial,
            ToLocation = "Warehouse B",
            Reason = "Should fail"
        }, fixture.RequestedBy.Id);
        await transferAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transferable*");

        var depreciationAct = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.OpenPeriod.Id,
            FixedAssetId = fixture.Asset.Id
        });
        await depreciationAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not eligible*");

        var valuationAct = () => services.Valuations.CreateValuationAsync(new CreateAssetValuationDto
        {
            FixedAssetId = fixture.Asset.Id,
            ValuationDate = new DateTime(2026, 7, 20),
            ValuationType = ValuationType.Impairment,
            FairValue = 800m,
            Reason = "Should fail"
        }, fixture.RequestedBy.Id);
        await valuationAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revalued or impaired*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalBeforePlacedInServiceOrCapitalizationDateRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var dto = RequestWriteOff(fixture.Asset.Id);
        dto.DisposalDate = new DateTime(2026, 6, 15);

        var act = () => services.Disposals.RequestDisposalAsync(dto, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalization date*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalIntoClosedPeriodRejectedThroughPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, closeDisposalPeriod: true);
        var services = CreateServices(db, tenantId);
        var requested = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.CompleteDisposalAsync(requested.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*period*");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalBlockedClosedPeriod)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task WriteOffNoProceedsPostsCorrectDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var lines = await PostedLinesAsync(db);
        completed.FinalDepreciationAmount.Should().Be(32.26m);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.DepreciationExpense.Id, DebitAmount = 32.26m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 0m, CreditAmount = 32.26m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 232.26m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.LossOnDisposal.Id, DebitAmount = 967.74m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1200m });
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task SaleDisposalWithGainPostsCorrectDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture.Asset.Id, 1100m));

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 232.26m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.ProceedsClearing.Id, DebitAmount = 1100m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1200m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.GainOnDisposal.Id, DebitAmount = 0m, CreditAmount = 132.26m });
        completed.GainOrLoss.Should().Be(132.26m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task SaleDisposalWithLossPostsCorrectDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture.Asset.Id, 900m));

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 232.26m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.ProceedsClearing.Id, DebitAmount = 900m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.LossOnDisposal.Id, DebitAmount = 67.74m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1200m });
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task AccumulatedDepreciationIsClearedAndCostIsNotMutated()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var asset = await db.FixedAssets.Include(a => a.BookValues).SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(1200m);
        asset.NetBookValue.Should().Be(0m);
        asset.Status.Should().Be(FixedAssetStatus.WrittenOff);
        asset.BookValues.Single().AcquisitionCost.Should().Be(1200m);
        asset.BookValues.Single().AccumulatedDepreciation.Should().Be(0m);
        asset.BookValues.Single().NetBookValue.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task AccumulatedImpairmentIsClearedThroughDerecognitionLines()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 900m);
        SeedPostedImpairment(db, fixture, 100m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        completed.AccumulatedImpairmentAtDisposal.Should().Be(100m);
        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedImpairment.Id, DebitAmount = 100m, CreditAmount = 0m });
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task PriorDepreciationSchedulesAndValuationsAreNotRewritten()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var schedule = SeedPostedDepreciationSchedule(db, fixture, 100m);
        var valuation = SeedPostedRevaluation(db, fixture, surplus: 300m);
        fixture.Asset.BookValues.Single().NetBookValue = 1300m;
        fixture.Asset.NetBookValue = 1300m;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var unchangedSchedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.Id == schedule.Id);
        unchangedSchedule.DepreciationAmount.Should().Be(100m);
        unchangedSchedule.NetBookValue.Should().Be(1100m);
        unchangedSchedule.IsPosted.Should().BeTrue();

        var unchangedValuation = await db.AssetValuations.SingleAsync(v => v.Id == valuation.Id);
        unchangedValuation.RevaluationSurplus.Should().Be(300m);
        unchangedValuation.Status.Should().Be("Posted");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task MissingDisposalGainLossOrProceedsMappingBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        fixture.Category.LossOnDisposalAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var requested = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.CompleteDisposalAsync(requested.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*loss on disposal account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantDisposalAccountRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var other = await SeedDisposalFoundationAsync(db, otherTenantId, codePrefix: "OTH");
        fixture.Category.LossOnDisposalAccountId = other.Accounts.LossOnDisposal.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var requested = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.CompleteDisposalAsync(requested.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found for this tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DuplicateDisposalIsIdempotentOrSafelyRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestWriteOff(fixture.Asset.Id);
        dto.IdempotencyKey = "dispose-once";

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, dto);
        var again = await services.Disposals.CompleteDisposalAsync(completed.Id);

        again.Id.Should().Be(completed.Id);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "FixedAssetDisposal")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalWithMissingRequiredDepreciationIsRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, lastDepreciationDate: null);
        fixture.Asset.BookValues.Single().LastDepreciationDate = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depreciation must be posted*");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalPostsActualDaysDepreciationAndCreatesReportableScheduleAtomically()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(
            db,
            tenantId,
            acquisitionCost: 1200m,
            accumulatedDepreciation: 200m,
            netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var requested = await services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        // July has 31 days and the disposal date is inclusive, so 10/31 of the normal GHS 100
        // monthly straight-line charge is the immutable maker-checker snapshot.
        requested.FinalDepreciationAmount.Should().Be(32.26m);
        requested.FinalDepreciationFromDate.Should().Be(new DateTime(2026, 7, 1));
        requested.FinalDepreciationToDate.Should().Be(new DateTime(2026, 7, 10));
        requested.FinalDepreciationEligibleDays.Should().Be(10);
        requested.FinalDepreciationPeriodDays.Should().Be(31);
        requested.FinalDepreciationProrationBasis.Should().Be("ActualDaysInclusive");

        var approved = await services.Disposals.ApproveDisposalAsync(
            requested.Id,
            fixture.Approver.Id,
            new ApproveAssetDisposalDto { Comments = "Approved final depreciation and disposal." });
        var completed = await services.Disposals.CompleteDisposalAsync(approved.Id);

        var schedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.AssetDisposalId == completed.Id);
        schedule.Id.Should().Be(completed.FinalDepreciationScheduleId!.Value);
        schedule.DepreciationAmount.Should().Be(32.26m);
        schedule.IsPosted.Should().BeTrue();
        schedule.JournalEntryId.Should().Be(completed.JournalEntryId);
        schedule.PostingEventId.Should().Be(completed.PostingEventId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task CurrentPeriodFullDepreciationMustBeReversedBeforeMidPeriodDisposal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var currentSchedule = SeedPostedDepreciationSchedule(db, fixture, 100m);
        currentSchedule.FiscalPeriodId = fixture.OpenPeriod.Id;
        currentSchedule.PostingDate = new DateTime(2026, 7, 31);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current-period depreciation schedule*Reverse*");
        (await db.AssetDisposals.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task UnitsOfProductionDisposalRequiresAndPersistsVerifiedUsageEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(
            db,
            tenantId,
            acquisitionCost: 1200m,
            accumulatedDepreciation: 200m,
            netBookValue: 1000m,
            depreciationMethod: DepreciationMethod.UnitsOfProduction,
            lifetimeProductionCapacity: 10000m);
        var services = CreateServices(db, tenantId);
        var missingEvidence = RequestWriteOff(fixture.Asset.Id);

        var missingAct = () => services.Disposals.RequestDisposalAsync(missingEvidence, fixture.RequestedBy.Id);
        await missingAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*production usage*");

        var dto = RequestWriteOff(fixture.Asset.Id);
        dto.FinalDepreciationProductionUnits = 500m;
        dto.FinalDepreciationEvidenceReference = "METER-2026-07-10";
        dto.FinalDepreciationEvidenceNotes = "Signed disposal meter reading.";
        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, dto);

        completed.FinalDepreciationAmount.Should().Be(50m);
        completed.FinalDepreciationProrationBasis.Should().Be("ProductionUsage");
        var schedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.AssetDisposalId == completed.Id);
        schedule.PeriodProductionUnits.Should().Be(500m);
        schedule.ProductionEvidenceReference.Should().Be("METER-2026-07-10");
        schedule.ProductionEvidenceNotes.Should().Be("Signed disposal meter reading.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationSurplusTransfersDirectlyToRetainedEarningsWithoutAffectingProfitAndLoss()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.RevaluationSurplus.Id, DebitAmount = 300m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.RetainedEarnings.Id, DebitAmount = 0m, CreditAmount = 300m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1500m });
        completed.FinalDepreciationAmount.Should().Be(41.94m);
        completed.GainOrLoss.Should().Be(-1258.06m);
        completed.RevaluationSurplusTransferAmount.Should().Be(300m);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalRevaluationSurplusTransferred)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluedAssetDisposalRequiresConfiguredRetainedEarningsAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        (await db.FinanceSettings.SingleAsync()).RetainedEarningsAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*retained earnings disposal-transfer account is required*");
        (await db.AssetDisposals.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedSurplusPolicyAccountCancelsStaleDisposalApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var approved = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        // Finance configuration can change legitimately, but an old checker decision must never
        // be silently rebuilt with the replacement retained-earnings account.
        var replacementRetainedEarnings = SeedAccount(db, tenantId, "3199", AccountType.Equity);
        (await db.FinanceSettings.SingleAsync()).RetainedEarningsAccountId = replacementRetainedEarnings.Id;
        await db.SaveChangesAsync();

        var act = () => services.Disposals.CompleteDisposalAsync(approved.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*policy accounts changed*");
        var cancelled = await db.AssetDisposals.SingleAsync(d => d.Id == approved.Id);
        cancelled.Status.Should().Be(AssetDisposalStatus.Cancelled);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedRevaluationSurplusBalanceCancelsStaleDisposalApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var approved = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        // A posted valuation after checker approval changes the equity reserve that the disposal
        // would derecognise. The service must invalidate the stale approval instead of silently
        // transferring an amount that the checker never reviewed.
        SeedPostedRevaluation(db, fixture, surplus: 50m);
        await db.SaveChangesAsync();

        var act = () => services.Disposals.CompleteDisposalAsync(approved.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*revaluation surplus changed*");
        var cancelled = await db.AssetDisposals.SingleAsync(d => d.Id == approved.Id);
        cancelled.Status.Should().Be(AssetDisposalStatus.Cancelled);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyProceedsRejectedClearly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestSale(fixture.Asset.Id, 900m);
        dto.ProceedsCurrencyCode = "USD";

        var act = () => services.Disposals.RequestDisposalAsync(dto, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Foreign-currency disposal proceeds*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalAuditEventsAreEmitted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture.Asset.Id, 1100m));

        var actions = await db.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action)
            .ToListAsync();

        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalRequested);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalApproved);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalCalculated);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalConfigurationUsed);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalPosted);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalSaleProceedsRecorded);
    }

    private static RequestAssetDisposalDto RequestWriteOff(Guid assetId) => new()
    {
        FixedAssetId = assetId,
        DisposalDate = new DateTime(2026, 7, 10),
        DisposalType = DisposalType.Scrap,
        Reason = "Asset has no future economic benefit.",
        SaleProceeds = 0m,
        DisposalCost = 0m,
        ProceedsCurrencyCode = "GHS"
    };

    private static RequestAssetDisposalDto RequestSale(Guid assetId, decimal proceeds) => new()
    {
        FixedAssetId = assetId,
        DisposalDate = new DateTime(2026, 7, 10),
        DisposalType = DisposalType.Sale,
        Reason = "Approved sale disposal.",
        BuyerName = "Buyer Ltd",
        SaleProceeds = proceeds,
        DisposalCost = 0m,
        ProceedsCurrencyCode = "GHS"
    };

    private static async Task<AssetDisposalDto> RequestAndApproveAsync(
        AssetDisposalService service,
        DisposalFixture fixture,
        RequestAssetDisposalDto dto)
    {
        var requested = await service.RequestDisposalAsync(dto, fixture.RequestedBy.Id);
        return await service.ApproveDisposalAsync(requested.Id, fixture.Approver.Id, new ApproveAssetDisposalDto { Comments = "Approved" });
    }

    private static async Task<AssetDisposalDto> RequestApproveAndCompleteAsync(
        AssetDisposalService service,
        DisposalFixture fixture,
        RequestAssetDisposalDto dto)
    {
        var approved = await RequestAndApproveAsync(service, fixture, dto);
        return await service.CompleteDisposalAsync(approved.Id);
    }

    private static async Task<List<AccountTransaction>> PostedLinesAsync(ApplicationDbContext db)
    {
        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync();
        return journal.Transactions.OrderBy(t => t.LineNumber).ToList();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-disposal-foundation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ServiceFixture CreateServices(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-disposal" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"DSP-{Guid.NewGuid():N}"[..20]);

        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("AssetDisposal", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(x => x.CanUserApproveAsync("AssetDisposal", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(x => x.ProcessApprovalStepAsync("AssetDisposal", It.IsAny<Guid>(), It.IsAny<Guid>(), "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(x => x.ProcessApprovalStepAsync("AssetDisposal", It.IsAny<Guid>(), It.IsAny<Guid>(), "Reject", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Cancelled,
                WorkflowInstanceId = Guid.NewGuid()
            });

        var disposalService = new AssetDisposalService(
            db,
            currentUser.Object,
            numbering.Object,
            workflow.Object,
            postingEngine,
            auditService);

        var transferService = new AssetTransferService(
            db,
            currentUser.Object,
            numbering.Object,
            workflow.Object,
            auditService);

        var depreciationService = new FixedAssetDepreciationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService);

        var valuationService = new AssetValuationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService);

        return new ServiceFixture(disposalService, transferService, depreciationService, valuationService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fa.disposal");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-disposal-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        currentUser.SetupGet(x => x.EmployeeId).Returns(Guid.NewGuid());
        return currentUser;
    }

    private static async Task<DisposalFixture> SeedDisposalFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string codePrefix = "TEN",
        bool isCapitalized = true,
        FixedAssetStatus status = FixedAssetStatus.Active,
        decimal acquisitionCost = 1200m,
        decimal accumulatedDepreciation = 200m,
        decimal? netBookValue = null,
        DateTime? lastDepreciationDate = null,
        bool closeDisposalPeriod = false,
        DepreciationMethod depreciationMethod = DepreciationMethod.StraightLine,
        decimal lifetimeProductionCapacity = 0m)
    {
        SeedTenant(db, tenantId, codePrefix);
        var previousPeriod = SeedPeriod(db, tenantId, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "2026-06", isOpen: true);
        var openPeriod = SeedPeriod(db, tenantId, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), "2026-07", isOpen: !closeDisposalPeriod);
        var book = SeedBook(db, tenantId);
        var accounts = SeedAccounts(db, tenantId, codePrefix);

        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            RetainedEarningsAccountId = accounts.RetainedEarnings.Id
        });

        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"DSP-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Disposal Assets",
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

        var requestedBy = SeedEmployee(db, tenantId, $"{codePrefix}-REQ", "Request", "User");
        var approver = SeedEmployee(db, tenantId, $"{codePrefix}-APR", "Approve", "User");
        var capitalizedAt = isCapitalized ? new DateTime(2026, 6, 30) : (DateTime?)null;
        var journalEntryId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var postingEventId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var nbv = netBookValue ?? acquisitionCost - accumulatedDepreciation;
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-DSP-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Disposal asset",
            Location = "Warehouse A",
            CurrentSegmentString = "OPS",
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 6, 1),
            PlacedInServiceDate = status == FixedAssetStatus.Active ? new DateTime(2026, 6, 1) : null,
            PurchasePrice = acquisitionCost,
            InstallationCost = 0m,
            TaxAmount = 0m,
            AcquisitionCost = acquisitionCost,
            NetBookValue = nbv,
            UsefulLifeMonths = 12,
            ResidualValue = 0m,
            DepreciationMethod = depreciationMethod,
            DepreciationConvention = DepreciationConvention.FullMonth,
            LifetimeProductionCapacity = lifetimeProductionCapacity,
            Status = status,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            CapitalizationDate = isCapitalized ? new DateTime(2026, 6, 30) : null,
            CapitalizedAt = capitalizedAt,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEventId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var bookValue = new FixedAssetBookValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = asset.Id,
            FixedAsset = asset,
            AccountingBookId = book.Id,
            AccountingBook = book,
            BookClassification = "IFRS",
            AcquisitionCost = acquisitionCost,
            AccumulatedDepreciation = accumulatedDepreciation,
            NetBookValue = nbv,
            ResidualValue = 0m,
            UsefulLifeMonths = 12,
            RemainingUsefulLifeMonths = 10,
            DepreciationMethod = depreciationMethod,
            DepreciationConvention = DepreciationConvention.FullMonth,
            LifetimeProductionCapacity = lifetimeProductionCapacity,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            CapitalizationDate = asset.CapitalizationDate,
            CapitalizationJournalEntryId = journalEntryId,
            CapitalizationPostingEventId = postingEventId,
            LastDepreciationDate = lastDepreciationDate ?? previousPeriod.EndDate,
            OpeningSource = "Capitalization",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        asset.BookValues.Add(bookValue);
        db.FixedAssetCategories.Add(category);
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();

        return new DisposalFixture(openPeriod, previousPeriod, book, asset, category, accounts, requestedBy, approver);
    }

    private static void SeedPostedImpairment(ApplicationDbContext db, DisposalFixture fixture, decimal amount)
    {
        db.AssetValuations.Add(new AssetValuation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Asset.TenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.PreviousPeriod.Id,
            ValuationDate = new DateTime(2026, 6, 30),
            AccountingDate = new DateTime(2026, 6, 30),
            ValuationType = ValuationType.Impairment,
            CarryingAmountBefore = fixture.Asset.NetBookValue + amount,
            AccumulatedDepreciationBefore = fixture.Asset.BookValues.Single().AccumulatedDepreciation,
            NetBookValueBefore = fixture.Asset.NetBookValue + amount,
            FairValue = fixture.Asset.NetBookValue,
            CarryingAmountAfter = fixture.Asset.NetBookValue,
            ImpairmentLoss = amount,
            AdjustmentAmount = amount,
            IsPostedToGL = true,
            Status = "Posted",
            Reason = "Impairment before disposal",
            PerformedByUserId = fixture.RequestedBy.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static AssetValuation SeedPostedRevaluation(ApplicationDbContext db, DisposalFixture fixture, decimal surplus)
    {
        var valuation = new AssetValuation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Asset.TenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.PreviousPeriod.Id,
            ValuationDate = new DateTime(2026, 6, 30),
            AccountingDate = new DateTime(2026, 6, 30),
            ValuationType = ValuationType.Revaluation,
            CarryingAmountBefore = 1200m,
            AccumulatedDepreciationBefore = 200m,
            NetBookValueBefore = 1000m,
            FairValue = 1300m,
            CarryingAmountAfter = 1300m,
            RevaluationSurplus = surplus,
            AdjustmentAmount = surplus,
            IsPostedToGL = true,
            Status = "Posted",
            Reason = "Market valuation before disposal",
            PerformedByUserId = fixture.RequestedBy.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AssetValuations.Add(valuation);
        return valuation;
    }

    private static AssetDepreciationSchedule SeedPostedDepreciationSchedule(ApplicationDbContext db, DisposalFixture fixture, decimal amount)
    {
        var schedule = new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Asset.TenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.PreviousPeriod.Id,
            DepreciationAmount = amount,
            AccumulatedDepreciationBefore = 0m,
            AccumulatedDepreciation = amount,
            NetBookValueBefore = 1200m,
            NetBookValue = 1100m,
            DepreciableAmount = 1200m,
            ResidualValueSnapshot = 0m,
            UsefulLifeMonthsSnapshot = 12,
            DepreciationMethodSnapshot = DepreciationMethod.StraightLine,
            PlacedInServiceDateSnapshot = fixture.Asset.PlacedInServiceDate,
            IsPosted = true,
            PostedDate = new DateTime(2026, 6, 30),
            PostingDate = new DateTime(2026, 6, 30),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AssetDepreciationSchedules.Add(schedule);
        return schedule;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code)
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
            BaseCurrency = "GHS"
        });
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId, DateTime start, DateTime end, string code, bool isOpen)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = code,
            PeriodCode = $"{code}-{tenantId.ToString("N")[..4]}",
            PeriodNumber = start.Month,
            PeriodType = PeriodType.Monthly,
            StartDate = start,
            EndDate = end,
            PeriodDays = (end - start).Days + 1,
            PeriodStatus = isOpen ? "Open" : "Closed",
            IsOpen = isOpen,
            IsClosed = !isOpen,
            IsLocked = !isOpen
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static AccountingBook SeedBook(ApplicationDbContext db, Guid tenantId)
    {
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"IFRS-{tenantId.ToString("N")[..4]}",
            Name = "IFRS",
            Purpose = "Primary",
            IsActive = true,
            IsDefault = true,
            AllowsPosting = true,
            IsSystemDefined = true,
            SortOrder = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AccountingBooks.Add(book);
        return book;
    }

    private static Employee SeedEmployee(ApplicationDbContext db, Guid tenantId, string number, string firstName, string lastName)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = number,
            FirstName = firstName,
            LastName = lastName,
            EmailAddress = $"{number.ToLowerInvariant()}@example.test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Employees.Add(employee);
        return employee;
    }

    private static DisposalAccounts SeedAccounts(ApplicationDbContext db, Guid tenantId, string codePrefix)
    {
        return new DisposalAccounts(
            SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Asset),
            SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}9", AccountType.Asset),
            SeedAccount(db, tenantId, $"67{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Expense),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}1", AccountType.Expense),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}2", AccountType.Expense),
            SeedAccount(db, tenantId, $"11{codePrefix[..Math.Min(2, codePrefix.Length)]}5", AccountType.Asset),
            SeedAccount(db, tenantId, $"32{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Equity),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}3", AccountType.Expense),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}4", AccountType.Expense),
            SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}8", AccountType.Asset),
            SeedAccount(db, tenantId, $"31{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Equity));
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = $"{accountNumber}-{tenantId.ToString("N")[..4]}",
            AccountNumber = $"{accountNumber}-{tenantId.ToString("N")[..4]}",
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        return account;
    }

    private sealed record ServiceFixture(
        AssetDisposalService Disposals,
        AssetTransferService Transfers,
        FixedAssetDepreciationService Depreciation,
        AssetValuationService Valuations);

    private sealed record DisposalFixture(
        FiscalPeriod OpenPeriod,
        FiscalPeriod PreviousPeriod,
        AccountingBook Book,
        FixedAsset Asset,
        FixedAssetCategory Category,
        DisposalAccounts Accounts,
        Employee RequestedBy,
        Employee Approver);

    private sealed record DisposalAccounts(
        Account Asset,
        Account AccumulatedDepreciation,
        Account DepreciationExpense,
        Account GainOnDisposal,
        Account LossOnDisposal,
        Account ProceedsClearing,
        Account RevaluationSurplus,
        Account RevaluationLoss,
        Account ImpairmentLoss,
        Account AccumulatedImpairment,
        Account RetainedEarnings);
}
