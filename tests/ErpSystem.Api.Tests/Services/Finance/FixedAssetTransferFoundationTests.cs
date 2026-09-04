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

public sealed class FixedAssetTransferFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task TransferCannotRunBeforeCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId, isCapitalized: false, status: FixedAssetStatus.Draft);
        var services = CreateServices(db, tenantId);

        var act = () => services.Transfers.RequestTransferAsync(RequestDto(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalized*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposedAssetCannotBeTransferred()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId, status: FixedAssetStatus.Disposed);
        var services = CreateServices(db, tenantId);

        var act = () => services.Transfers.RequestTransferAsync(RequestDto(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transferable status*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantAssetTransferRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var other = await SeedTransferFoundationAsync(db, otherTenantId, codePrefix: "OTH");
        var services = CreateServices(db, tenantId);

        var act = () => services.Transfers.RequestTransferAsync(RequestDto(other.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantSegmentLookupRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var other = await SeedTransferFoundationAsync(db, otherTenantId, codePrefix: "OTH");
        var services = CreateServices(db, tenantId);

        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.SegmentMovement;
        dto.ToSegmentLookupValueId = other.TargetSegment.Id;

        var act = () => services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*segment belongs to another tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantCustodianRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var other = await SeedTransferFoundationAsync(db, otherTenantId, codePrefix: "OTH");
        var services = CreateServices(db, tenantId);

        var dto = RequestDto(fixture.Asset.Id);
        dto.ToCustodianId = other.TargetCustodian.Id;

        var act = () => services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*custodian belongs to another tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantReclassificationCategoryRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var other = await SeedTransferFoundationAsync(db, otherTenantId, codePrefix: "OTH");
        var services = CreateServices(db, tenantId);

        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.GlReclassification;
        dto.ToLocation = string.Empty;
        dto.ToFixedAssetCategoryId = other.TargetCategory.Id;
        dto.AccountingDate = new DateTime(2026, 7, 10);
        dto.Reason = "Move the asset to the reviewed accounting classification.";

        var act = () => services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*category belongs to another tenant*");
        (await db.AssetTransfers.CountAsync(t => t.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task CustodyLocationOnlyTransferCreatesHistoryButNoGlJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var dto = RequestDto(fixture.Asset.Id);
        dto.ToCustodianId = fixture.TargetCustodian.Id;
        var requested = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);
        var completed = await services.Transfers.ApproveTransferAsync(requested.Id, fixture.Approver.Id, new ApproveAssetTransferDto { Comments = "Approved" });

        completed.Status.Should().Be(AssetTransferStatus.Completed);
        completed.JournalEntryId.Should().BeNull();
        completed.PostingEventId.Should().BeNull();
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.Location.Should().Be("Warehouse B");
        asset.CurrentCustodianId.Should().Be(fixture.TargetCustodian.Id);
        (await db.AssetTransactions.CountAsync(t => t.FixedAssetId == fixture.Asset.Id && t.TransactionType == "Transfer")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task GlReclassificationMovesCurrentBalancesAndPreservesMeasurementHistory()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.GlReclassification;
        dto.ToFixedAssetCategoryId = fixture.TargetCategory.Id;
        dto.ToSegmentLookupValueId = fixture.TargetSegment.Id;
        dto.AccountingDate = new DateTime(2026, 7, 10);
        dto.ToLocation = string.Empty;
        dto.Reason = "Reclassify the asset to its approved operational category.";

        var requested = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);
        var completed = await services.Transfers.ApproveTransferAsync(
            requested.Id,
            fixture.Approver.Id,
            new ApproveAssetTransferDto { Comments = "Independent accounting review completed." });

        completed.Status.Should().Be(AssetTransferStatus.Completed);
        completed.JournalEntryId.Should().NotBeNull();
        completed.PostingEventId.Should().NotBeNull();
        completed.ReclassificationAssetCarryingAmount.Should().Be(1200m);
        completed.ReclassificationAccumulatedDepreciation.Should().Be(200m);

        var asset = await db.FixedAssets.Include(a => a.BookValues).SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.FixedAssetCategoryId.Should().Be(fixture.TargetCategory.Id);
        asset.CurrentSegmentLookupValueId.Should().Be(fixture.TargetSegment.Id);
        asset.AcquisitionCost.Should().Be(1200m);
        asset.NetBookValue.Should().Be(1000m);
        asset.BookValues.Single().AccumulatedDepreciation.Should().Be(200m);

        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync(j => j.Id == completed.JournalEntryId);
        journal.Transactions.Should().ContainSingle(t =>
            t.AccountId == fixture.TargetCategory.AssetAccountId && t.DebitAmount == 1200m && t.SegmentString == fixture.TargetSegment.SegmentValue);
        journal.Transactions.Should().ContainSingle(t =>
            t.AccountId == fixture.Category.AssetAccountId && t.CreditAmount == 1200m && t.SegmentString == fixture.SourceSegment.SegmentValue);
        journal.Transactions.Should().ContainSingle(t =>
            t.AccountId == fixture.Category.AccumulatedDepreciationAccountId && t.DebitAmount == 200m);
        journal.Transactions.Should().ContainSingle(t =>
            t.AccountId == fixture.TargetCategory.AccumulatedDepreciationAccountId && t.CreditAmount == 200m);
        journal.Transactions.Sum(t => t.DebitAmount).Should().Be(1400m);
        journal.Transactions.Sum(t => t.CreditAmount).Should().Be(1400m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task GlReclassificationRequiresIndependentChecker()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.GlReclassification;
        dto.ToFixedAssetCategoryId = fixture.TargetCategory.Id;
        dto.AccountingDate = new DateTime(2026, 7, 10);
        dto.Reason = "Correct the account classification after independent review.";
        var requested = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        var act = () => services.Transfers.ApproveTransferAsync(
            requested.Id,
            fixture.RequestedBy.Id,
            new ApproveAssetTransferDto { Comments = "Self approval attempt." });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot approve*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.AssetTransfers.SingleAsync(t => t.Id == requested.Id)).Status.Should().Be(AssetTransferStatus.PendingApproval);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedBalanceInvalidatesApprovedReclassificationSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId, accumulatedDepreciation: 100m, netBookValue: 1100m);
        var services = CreateServices(db, tenantId);
        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.GlReclassification;
        dto.ToFixedAssetCategoryId = fixture.TargetCategory.Id;
        dto.AccountingDate = new DateTime(2026, 7, 10);
        dto.Reason = "Move current balances to the corrected asset category accounts.";
        var requested = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        // Simulate a depreciation/valuation change after maker submission. The checker-approved
        // request must not post stale amounts; a new request is required instead.
        var book = await db.FixedAssetBookValues.SingleAsync(b => b.FixedAssetId == fixture.Asset.Id);
        book.AccumulatedDepreciation += 50m;
        book.NetBookValue -= 50m;
        await db.SaveChangesAsync();

        var act = () => services.Transfers.ApproveTransferAsync(
            requested.Id,
            fixture.Approver.Id,
            new ApproveAssetTransferDto { Comments = "Approved based on submitted evidence." });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*balances changed*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id)).FixedAssetCategoryId.Should().Be(fixture.Category.Id);
        var failed = await db.AssetTransfers.SingleAsync(t => t.Id == requested.Id);
        // The checker approved an earlier balance snapshot. That approval is terminal once the
        // evidence drifts, allowing the maker to raise a new request instead of endlessly retrying.
        failed.Status.Should().Be(AssetTransferStatus.Cancelled);
        failed.FailureReason.Should().Contain("balances changed");

        var replacement = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);
        replacement.Id.Should().NotBe(requested.Id);
        replacement.Status.Should().Be(AssetTransferStatus.PendingApproval);
        replacement.ReclassificationAccumulatedDepreciation.Should().Be(150m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedCategoryMappingInvalidatesApprovedReclassificationSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.GlReclassification;
        dto.ToFixedAssetCategoryId = fixture.TargetCategory.Id;
        dto.AccountingDate = new DateTime(2026, 7, 10);
        dto.Reason = "Move current balances to the corrected asset category accounts.";
        var requested = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        // Administrators may legitimately correct a category mapping while a request waits for
        // review. The checker must approve a newly calculated request, not stale account evidence.
        fixture.TargetCategory.AssetAccountId = fixture.Category.AssetAccountId;
        await db.SaveChangesAsync();

        var act = () => services.Transfers.ApproveTransferAsync(
            requested.Id,
            fixture.Approver.Id,
            new ApproveAssetTransferDto { Comments = "Approved based on submitted evidence." });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*account mapping changed*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id)).FixedAssetCategoryId.Should().Be(fixture.Category.Id);
        var failed = await db.AssetTransfers.SingleAsync(t => t.Id == requested.Id);
        // Mutable category setup cannot silently replace the account evidence approved by the
        // checker. Cancelling the stale request permits a fresh controlled approval cycle.
        failed.Status.Should().Be(AssetTransferStatus.Cancelled);
        failed.FailureReason.Should().Contain("account mapping changed");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedPostingBookConfigurationCancelsApprovedReclassification()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.GlReclassification;
        dto.ToFixedAssetCategoryId = fixture.TargetCategory.Id;
        dto.AccountingDate = new DateTime(2026, 7, 10);
        dto.Reason = "Move current balances to the corrected asset category accounts.";
        var requested = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        // Posting-book authority is mutable configuration. Disabling the book after submission
        // invalidates the checker's evidence just as surely as changing a category account.
        fixture.Book.AllowsPosting = false;
        await db.SaveChangesAsync();

        var act = () => services.Transfers.ApproveTransferAsync(
            requested.Id,
            fixture.Approver.Id,
            new ApproveAssetTransferDto { Comments = "Approved based on submitted evidence." });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*posting-book configuration changed*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        var failed = await db.AssetTransfers.SingleAsync(t => t.Id == requested.Id);
        failed.Status.Should().Be(AssetTransferStatus.Cancelled);
        failed.FailureReason.Should().Contain("posting-book configuration changed");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task DuplicateTransferRequestReturnsExistingTransfer()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestDto(fixture.Asset.Id);
        dto.IdempotencyKey = "same-request";

        var first = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);
        var second = await services.Transfers.RequestTransferAsync(dto, fixture.RequestedBy.Id);

        second.Id.Should().Be(first.Id);
        (await db.AssetTransfers.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task CompletedTransferCannotBeCompletedAgainWithMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var transfer = await RequestAndApproveAsync(services.Transfers, fixture);
        var again = await services.Transfers.CompleteTransferAsync(transfer.Id);

        again.Id.Should().Be(transfer.Id);
        (await db.AssetTransactions.CountAsync(t => t.FixedAssetId == fixture.Asset.Id && t.TransactionType == "Transfer")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task TransferPreservesAcquisitionCostAccumulatedDepreciationAndNbv()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        await RequestAndApproveAsync(services.Transfers, fixture);

        var asset = await db.FixedAssets.Include(a => a.BookValues).SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(1200m);
        asset.NetBookValue.Should().Be(1000m);
        asset.BookValues.Single().AccumulatedDepreciation.Should().Be(200m);
        asset.BookValues.Single().NetBookValue.Should().Be(1000m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task TransferPreservesImpairmentAndRevaluationHistory()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        db.AssetValuations.Add(new AssetValuation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = fixture.Asset.Id,
            ValuationType = ValuationType.Revaluation,
            ValuationDate = new DateTime(2026, 7, 5),
            AccountingDate = new DateTime(2026, 7, 5),
            BookClassification = "IFRS",
            CarryingAmountBefore = 1200m,
            NetBookValueBefore = 1200m,
            FairValue = 1400m,
            CarryingAmountAfter = 1400m,
            AdjustmentAmount = 200m,
            Status = "Posted",
            ValuationReportReference = "VAL-001",
            Reason = "Market appraisal",
            PerformedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        await RequestAndApproveAsync(services.Transfers, fixture);

        var valuation = await db.AssetValuations.SingleAsync(v => v.FixedAssetId == fixture.Asset.Id);
        valuation.Status.Should().Be("Posted");
        valuation.AdjustmentAmount.Should().Be(200m);
        valuation.Reason.Should().Be("Market appraisal");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task FutureDepreciationUsesNewSegmentAfterTransfer()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestDto(fixture.Asset.Id);
        dto.TransferType = AssetTransferType.SegmentMovement;
        dto.ToSegmentLookupValueId = fixture.TargetSegment.Id;

        await RequestAndApproveAsync(services.Transfers, fixture, dto);
        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync();
        journal.Transactions.Should().OnlyContain(t => t.SegmentString == fixture.TargetSegment.SegmentValue);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task PriorDepreciationSchedulesAreNotRewritten()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var postedSchedule = new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = fixture.Asset.Id,
            FiscalPeriodId = Guid.NewGuid(),
            BookClassification = "IFRS",
            DepreciationAmount = 100m,
            AccumulatedDepreciationBefore = 0m,
            AccumulatedDepreciation = 100m,
            NetBookValueBefore = 1200m,
            NetBookValue = 1100m,
            DepreciableAmount = 1200m,
            ResidualValueSnapshot = 0m,
            UsefulLifeMonthsSnapshot = 12,
            DepreciationMethodSnapshot = DepreciationMethod.StraightLine,
            PlacedInServiceDateSnapshot = fixture.Asset.PlacedInServiceDate,
            IsPosted = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AssetDepreciationSchedules.Add(postedSchedule);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        await RequestAndApproveAsync(services.Transfers, fixture);

        var schedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.Id == postedSchedule.Id);
        schedule.DepreciationAmount.Should().Be(100m);
        schedule.NetBookValue.Should().Be(1100m);
        schedule.IsPosted.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetTransfers")]
    [Trait("Category", "FixedAssets")]
    public async Task TransferAuditEventsAreEmitted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedTransferFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestAndApproveAsync(services.Transfers, fixture);

        var auditActions = await db.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action)
            .ToListAsync();
        auditActions.Should().Contain(FinanceAuditEvents.FixedAssetTransferRequested);
        auditActions.Should().Contain(FinanceAuditEvents.FixedAssetTransferApproved);
        auditActions.Should().Contain(FinanceAuditEvents.FixedAssetTransferred);
    }

    private static RequestAssetTransferDto RequestDto(Guid assetId) => new()
    {
        FixedAssetId = assetId,
        TransferDate = new DateTime(2026, 7, 10),
        TransferType = AssetTransferType.Custodial,
        ToLocation = "Warehouse B",
        Reason = "Operational reassignment"
    };

    private static async Task<AssetTransferDto> RequestAndApproveAsync(
        AssetTransferService service,
        TransferFixture fixture,
        RequestAssetTransferDto? dto = null)
    {
        var request = dto ?? RequestDto(fixture.Asset.Id);
        if (request.TransferType != AssetTransferType.GlReclassification)
        {
            request.ToCustodianId ??= fixture.TargetCustodian.Id;
        }
        var requested = await service.RequestTransferAsync(request, fixture.RequestedBy.Id);
        return await service.ApproveTransferAsync(requested.Id, fixture.Approver.Id, new ApproveAssetTransferDto { Comments = "Approved" });
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-transfer-foundation-{Guid.NewGuid()}")
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
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-transfer" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var depreciationService = new FixedAssetDepreciationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService);
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"TRF-{Guid.NewGuid():N}"[..20]);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("AssetTransfer", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(x => x.CanUserApproveAsync("AssetTransfer", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(x => x.ProcessApprovalStepAsync("AssetTransfer", It.IsAny<Guid>(), It.IsAny<Guid>(), "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(x => x.ProcessApprovalStepAsync("AssetTransfer", It.IsAny<Guid>(), It.IsAny<Guid>(), "Reject", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Cancelled,
                WorkflowInstanceId = Guid.NewGuid()
            });

        var transferService = new AssetTransferService(
            db,
            currentUser.Object,
            numbering.Object,
            workflow.Object,
            auditService,
            postingEngine);

        return new ServiceFixture(transferService, depreciationService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fa.transfer");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-transfer-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        currentUser.SetupGet(x => x.EmployeeId).Returns(Guid.NewGuid());
        return currentUser;
    }

    private static async Task<TransferFixture> SeedTransferFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string codePrefix = "TEN",
        bool isCapitalized = true,
        FixedAssetStatus status = FixedAssetStatus.Active,
        decimal acquisitionCost = 1200m,
        decimal accumulatedDepreciation = 0m,
        decimal? netBookValue = null)
    {
        SeedTenant(db, tenantId, codePrefix);
        var period = SeedOpenPeriod(db, tenantId);
        var book = SeedBook(db, tenantId);
        var assetAccount = SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Asset);
        var accumulatedAccount = SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}9", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, $"67{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Expense);
        var targetAssetAccount = SeedAccount(db, tenantId, $"17{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Asset);
        var targetAccumulatedAccount = SeedAccount(db, tenantId, $"17{codePrefix[..Math.Min(2, codePrefix.Length)]}9", AccountType.Asset);
        var targetExpenseAccount = SeedAccount(db, tenantId, $"68{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Expense);

        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS"
        });

        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"TRN-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Transfer Assets",
            AssetAccountId = assetAccount.Id,
            AccumulatedDepreciationAccountId = accumulatedAccount.Id,
            DepreciationExpenseAccountId = expenseAccount.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var targetCategory = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"TRG-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Target Transfer Assets",
            AssetAccountId = targetAssetAccount.Id,
            AccumulatedDepreciationAccountId = targetAccumulatedAccount.Id,
            DepreciationExpenseAccountId = targetExpenseAccount.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var sourceCustodian = SeedEmployee(db, tenantId, $"{codePrefix}-001", "Source", "Custodian");
        var targetCustodian = SeedEmployee(db, tenantId, $"{codePrefix}-002", "Target", "Custodian");
        var requestedBy = SeedEmployee(db, tenantId, $"{codePrefix}-003", "Request", "User");
        var approver = SeedEmployee(db, tenantId, $"{codePrefix}-004", "Approve", "User");
        var sourceSegment = SeedSegment(db, tenantId, $"S{codePrefix[..1]}1", "Source Segment");
        var targetSegment = SeedSegment(db, tenantId, $"T{codePrefix[..1]}2", "Target Segment");

        var capitalizedAt = isCapitalized ? new DateTime(2026, 6, 30) : (DateTime?)null;
        var journalEntryId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var postingEventId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var nbv = netBookValue ?? acquisitionCost - accumulatedDepreciation;
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-TRN-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Transfer laptop",
            Location = "Warehouse A",
            CurrentCustodianId = sourceCustodian.Id,
            CurrentCustodian = sourceCustodian,
            CurrentSegmentString = sourceSegment.SegmentValue,
            CurrentSegmentLookupValueId = sourceSegment.Id,
            CurrentSegmentLookupValue = sourceSegment,
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 6, 30),
            PlacedInServiceDate = status == FixedAssetStatus.Active ? new DateTime(2026, 7, 1) : null,
            PurchasePrice = acquisitionCost,
            InstallationCost = 0m,
            TaxAmount = 0m,
            AcquisitionCost = acquisitionCost,
            NetBookValue = nbv,
            UsefulLifeMonths = 12,
            ResidualValue = 0m,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
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
            RemainingUsefulLifeMonths = 12,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            CapitalizationDate = asset.CapitalizationDate,
            CapitalizationJournalEntryId = journalEntryId,
            CapitalizationPostingEventId = postingEventId,
            OpeningSource = "Capitalization",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        asset.BookValues.Add(bookValue);
        db.FixedAssetCategories.Add(category);
        db.FixedAssetCategories.Add(targetCategory);
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();

        return new TransferFixture(
            period,
            book,
            asset,
            category,
            targetCategory,
            sourceCustodian,
            targetCustodian,
            requestedBy,
            approver,
            sourceSegment,
            targetSegment);
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

    private static SegmentLookupValue SeedSegment(ApplicationDbContext db, Guid tenantId, string value, string description)
    {
        var structure = new AccountSegmentStructure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentName = $"Department {value}",
            SegmentCode = value,
            SegmentPosition = 1,
            SegmentLength = Math.Min(10, value.Length),
            LookupTableRequired = true,
            IsReportingDimension = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var lookup = new SegmentLookupValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentStructureId = structure.Id,
            SegmentStructure = structure,
            SegmentValue = value,
            Description = description,
            EffectiveDate = new DateTime(2026, 1, 1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AccountSegmentStructures.Add(structure);
        db.SegmentLookupValues.Add(lookup);
        return lookup;
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

    private static FiscalPeriod SeedOpenPeriod(ApplicationDbContext db, Guid tenantId)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = $"2026-07-{tenantId.ToString("N")[..4]}",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        return period;
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
        AssetTransferService Transfers,
        FixedAssetDepreciationService Depreciation);

    private sealed record TransferFixture(
        FiscalPeriod Period,
        AccountingBook Book,
        FixedAsset Asset,
        FixedAssetCategory Category,
        FixedAssetCategory TargetCategory,
        Employee SourceCustodian,
        Employee TargetCustodian,
        Employee RequestedBy,
        Employee Approver,
        SegmentLookupValue SourceSegment,
        SegmentLookupValue TargetSegment);
}
