using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
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

public sealed class FixedAssetRevaluationImpairmentFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationCannotRunBeforeCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId, isCapitalized: false, status: FixedAssetStatus.Draft);
        var services = CreateServices(db, tenantId);

        var act = () => services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalized*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ImpairmentCannotRunBeforeCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId, isCapitalized: false, status: FixedAssetStatus.Draft);
        var services = CreateServices(db, tenantId);

        var act = () => services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Impairment, 800m), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalized*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposedAssetCannotBeRevaluedOrImpaired()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId, status: FixedAssetStatus.Disposed);
        var services = CreateServices(db, tenantId);

        var act = () => services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Disposed*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationIncreasePostsThroughPostingEngine_WithExpectedDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var journal = await LoadValuationJournalAsync(db, valuation.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.AssetAccount.Id).DebitAmount.Should().Be(500m);
        journal.Transactions.Single(t => t.AccountId == fixture.RevaluationSurplusAccount.Id).CreditAmount.Should().Be(500m);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "FixedAssetValuation" && e.SourceDocumentId == valuation.Id)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationDecreasePostsThroughPostingEngine_WithExpectedDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 800m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var journal = await LoadValuationJournalAsync(db, valuation.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.AssetAccount.Id).CreditAmount.Should().Be(200m);
        journal.Transactions.Single(t => t.AccountId == fixture.RevaluationLossAccount.Id).DebitAmount.Should().Be(200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationDecreaseUsesExistingSurplusBeforeLoss()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var increase = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1300m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(increase.Id);
        var decrease = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 900m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(decrease.Id);

        var postedDecrease = await db.AssetValuations.SingleAsync(v => v.Id == decrease.Id);
        postedDecrease.RevaluationSurplusApplied.Should().Be(300m);
        postedDecrease.RevaluationLossRecognized.Should().Be(100m);

        var journal = await LoadValuationJournalAsync(db, decrease.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.AssetAccount.Id).CreditAmount.Should().Be(400m);
        journal.Transactions.Single(t => t.AccountId == fixture.RevaluationSurplusAccount.Id).DebitAmount.Should().Be(300m);
        journal.Transactions.Single(t => t.AccountId == fixture.RevaluationLossAccount.Id).DebitAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ImpairmentLossPostsThroughPostingEngine_WithExpectedDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Impairment, 700m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var journal = await LoadValuationJournalAsync(db, valuation.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.ImpairmentLossAccount.Id).DebitAmount.Should().Be(300m);
        journal.Transactions.Single(t => t.AccountId == fixture.AccumulatedImpairmentAccount.Id).CreditAmount.Should().Be(300m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task MissingRevaluationSurplusAccountBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        fixture.Category.RevaluationSurplusAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        var act = () => services.Valuations.PostValuationToGLAsync(valuation.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revaluation surplus account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task MissingImpairmentLossAccountBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        fixture.Category.ImpairmentLossAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Impairment, 700m), Guid.NewGuid());
        var act = () => services.Valuations.PostValuationToGLAsync(valuation.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*impairment loss account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantRevaluationAccountRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, "3999", AccountType.Equity);
        fixture.Category.RevaluationSurplusAccountId = otherAccount.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        var act = () => services.Valuations.PostValuationToGLAsync(valuation.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found for this tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantAssetRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, otherTenantId);

        var act = () => services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Theory]
    [InlineData(ValuationType.Revaluation, 1500)]
    [InlineData(ValuationType.Impairment, 700)]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ClosedPeriodValuationRejectedThroughPostingEngine(ValuationType valuationType, decimal fairValue)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, valuationType, fairValue), Guid.NewGuid());
        var act = () => services.Valuations.PostValuationToGLAsync(valuation.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*period is not open*");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetValuationBlockedClosedPeriod)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task DuplicateValuationPostIsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        var first = await services.Valuations.PostValuationToGLAsync(valuation.Id);
        var second = await services.Valuations.PostValuationToGLAsync(valuation.Id);

        second.PostingEventId.Should().Be(first.PostingEventId);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "FixedAssetValuation" && e.SourceDocumentId == valuation.Id)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task AcquisitionCostIsNotMutatedByValuationAndBookValueUpdates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Impairment, 700m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var asset = await db.FixedAssets.Include(a => a.BookValues).SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(1200m);
        asset.PurchasePrice.Should().Be(1200m);
        asset.NetBookValue.Should().Be(700m);
        asset.BookValues.Single().NetBookValue.Should().Be(700m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task LaterDepreciationUsesAdjustedCarryingAmountProspectively()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId, accumulatedDepreciation: 200m, remainingUsefulLifeMonths: 10);
        var services = CreateServices(db, tenantId);

        var valuationDto = CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m);
        valuationDto.RevisedUsefulLifeMonths = 5;
        var valuation = await services.Valuations.CreateValuationAsync(valuationDto, Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var depreciation = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        depreciation.Single().DepreciationAmount.Should().Be(300m);
        depreciation.Single().NetBookValueBefore.Should().Be(1500m);
        depreciation.Single().NetBookValue.Should().Be(1200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task PriorPostedDepreciationSchedulesAreNotRewritten()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        db.AssetDepreciationSchedules.Add(new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.Period.Id,
            DepreciationAmount = 50m,
            AccumulatedDepreciationBefore = 150m,
            AccumulatedDepreciation = 200m,
            NetBookValueBefore = 1050m,
            NetBookValue = 1000m,
            DepreciableAmount = 1000m,
            ResidualValueSnapshot = 0m,
            UsefulLifeMonthsSnapshot = 10,
            DepreciationMethodSnapshot = DepreciationMethod.StraightLine,
            IsPosted = true,
            PostingDate = new DateTime(2026, 7, 1),
            PostedDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var schedule = await db.AssetDepreciationSchedules.SingleAsync();
        schedule.DepreciationAmount.Should().Be(50m);
        schedule.NetBookValue.Should().Be(1000m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyAcquisitionSnapshotsRemainUnchanged()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId, transactionCurrency: "USD", exchangeRate: 12m);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);
        fixture.ExchangeRate!.Rate = 15m;
        await db.SaveChangesAsync();

        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.TransactionCurrencyCode.Should().Be("USD");
        asset.ExchangeRate.Should().Be(12m);
        var journal = await LoadValuationJournalAsync(db, valuation.Id);
        journal.Transactions.Should().OnlyContain(t => t.TransactionCurrency == "GHS");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ValuationAuditEventsAreEmitted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var valuation = await services.Valuations.CreateValuationAsync(CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(valuation.Id);

        var actions = await db.AuditLogs.Where(a => a.TenantId == tenantId).Select(a => a.Action).ToListAsync();
        actions.Should().Contain(FinanceAuditEvents.FixedAssetRevaluationCalculated);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetValuationConfigurationUsed);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetRevaluationPosted);
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task ValuationRequiresWorkflowApprovalBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("AssetValuation", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var services = CreateServices(db, tenantId, workflow.Object);

        var valuation = await services.Valuations.CreateValuationAsync(
            CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m),
            Guid.NewGuid());

        valuation.Status.Should().Be("PendingApproval");
        var unapprovedPost = () => services.Valuations.PostValuationToGLAsync(valuation.Id);
        await unapprovedPost.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved*");

        var stored = await db.AssetValuations.SingleAsync(v => v.Id == valuation.Id);
        stored.Status = "Approved";
        await db.SaveChangesAsync();

        var posted = await services.Valuations.PostValuationToGLAsync(valuation.Id);

        posted.IsPostedToGL.Should().BeTrue();
        posted.Status.Should().Be("Posted");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "FixedAssetValuation")).Should().Be(1);
        workflow.Verify(x => x.StartApprovalWorkflowAsync("AssetValuation", valuation.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ImpairmentReversalPostsAgainstItsSource_WithinBothIas36Ceilings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var impairment = await services.Valuations.CreateValuationAsync(
            CreateValuationDto(fixture.Asset.Id, ValuationType.Impairment, 700m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(impairment.Id);

        var request = CreateValuationDto(fixture.Asset.Id, ValuationType.ImpairmentReversal, 850m);
        request.SourceImpairmentValuationId = impairment.Id;
        request.UnimpairedCarryingAmountCap = 900m;
        var reversal = await services.Valuations.CreateValuationAsync(request, Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(reversal.Id);

        reversal.ImpairmentReversal.Should().Be(150m);
        reversal.OutstandingImpairmentBefore.Should().Be(300m);
        reversal.SourceImpairmentValuationId.Should().Be(impairment.Id);
        var journal = await LoadValuationJournalAsync(db, reversal.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.AccumulatedImpairmentAccount.Id)
            .DebitAmount.Should().Be(150m);
        journal.Transactions.Single(t => t.AccountId == fixture.Category.ImpairmentReversalAccountId)
            .CreditAmount.Should().Be(150m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ImpairmentReversalCannotExceedNoImpairmentCarryingAmount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var impairment = await services.Valuations.CreateValuationAsync(
            CreateValuationDto(fixture.Asset.Id, ValuationType.Impairment, 700m), Guid.NewGuid());
        await services.Valuations.PostValuationToGLAsync(impairment.Id);

        var request = CreateValuationDto(fixture.Asset.Id, ValuationType.ImpairmentReversal, 950m);
        request.SourceImpairmentValuationId = impairment.Id;
        request.UnimpairedCarryingAmountCap = 900m;
        var act = () => services.Valuations.CreateValuationAsync(request, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*above 900.00*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetRevaluationImpairment")]
    [Trait("Category", "FixedAssets")]
    public async Task ApprovedValuationCorrectionPostsLinkedCompensatingJournal_AndRestoresBookSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedValuationFoundationAsync(db, tenantId);
        var maker = CreateServices(db, tenantId, userId: Guid.NewGuid());
        var valuation = await maker.Valuations.CreateValuationAsync(
            CreateValuationDto(fixture.Asset.Id, ValuationType.Revaluation, 1500m), Guid.NewGuid());
        await maker.Valuations.PostValuationToGLAsync(valuation.Id);

        var request = await maker.Valuations.RequestCorrectionAsync(valuation.Id, new()
        {
            Reason = "The valuer report was assigned to the wrong fixed asset record.",
            ImpactAssessment = "Restore the pre-valuation NBV before posting the corrected valuation report."
        });

        // A separate resolved identity is essential evidence: the maker cannot approve the same
        // correction request even when both users operate through the same Finance workspace.
        var checker = CreateServices(db, tenantId, userId: Guid.NewGuid());
        var approved = await checker.Valuations.ReviewCorrectionAsync(valuation.Id, request.Id, new()
        {
            Approved = true,
            ReviewComment = "Source report and asset register were independently checked and agree."
        });
        var posted = await checker.Valuations.PostCorrectionAsync(valuation.Id, approved.Id);

        posted.Status.Should().Be(AssetValuationCorrectionStatuses.Posted);
        posted.ReversalJournalEntryId.Should().NotBeNull();
        var storedValuation = await db.AssetValuations.SingleAsync(v => v.Id == valuation.Id);
        storedValuation.IsCorrected.Should().BeTrue();
        storedValuation.CorrectionId.Should().Be(request.Id);
        var book = await db.FixedAssetBookValues.SingleAsync(b => b.FixedAssetId == fixture.Asset.Id);
        book.NetBookValue.Should().Be(1000m);
        (await db.AssetTransactions.SingleAsync(t => t.TransactionType == "ValuationCorrection"))
            .ResultingBookValue.Should().Be(1000m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-revaluation-impairment-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ServiceFixture CreateServices(
        ApplicationDbContext db,
        Guid tenantId,
        IWorkflowService? workflowService = null,
        Guid? userId = null)
    {
        var currentUser = CreateCurrentUser(tenantId, userId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-valuation" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var valuationService = new AssetValuationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService,
            workflowService: workflowService,
            financeReversalPolicyService: new FinanceReversalPolicyService(db, currentUser.Object));
        var depreciationService = new FixedAssetDepreciationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService);

        return new ServiceFixture(valuationService, depreciationService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId, Guid? userId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns((userId ?? Guid.NewGuid()).ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fa.valuation");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-valuation-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ValuationFixture> SeedValuationFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        decimal acquisitionCost = 1200m,
        decimal accumulatedDepreciation = 200m,
        decimal? netBookValue = null,
        int usefulLifeMonths = 10,
        int? remainingUsefulLifeMonths = 10,
        bool isCapitalized = true,
        FixedAssetStatus status = FixedAssetStatus.Active,
        bool periodIsOpen = true,
        bool periodIsClosed = false,
        string transactionCurrency = "GHS",
        decimal exchangeRate = 1m)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var book = SeedBook(db, tenantId);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, book.Code);
        var assetAccount = SeedAccount(db, tenantId, "1600", AccountType.Asset);
        var accumulatedDepreciationAccount = SeedAccount(db, tenantId, "1699", AccountType.Asset);
        var depreciationExpenseAccount = SeedAccount(db, tenantId, "6700", AccountType.Expense);
        var revaluationSurplusAccount = SeedAccount(db, tenantId, "3300", AccountType.Equity);
        var revaluationLossAccount = SeedAccount(db, tenantId, "6710", AccountType.Expense);
        var impairmentLossAccount = SeedAccount(db, tenantId, "6720", AccountType.Expense);
        var accumulatedImpairmentAccount = SeedAccount(db, tenantId, "1698", AccountType.Asset);
        var impairmentReversalAccount = SeedAccount(db, tenantId, "4800", AccountType.Revenue);
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(
            db,
            tenantId,
            book,
            assetAccount,
            accumulatedDepreciationAccount,
            depreciationExpenseAccount,
            revaluationSurplusAccount,
            revaluationLossAccount,
            impairmentLossAccount,
            accumulatedImpairmentAccount,
            impairmentReversalAccount);

        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS"
        });

        ExchangeRate? rate = null;
        if (!transactionCurrency.Equals("GHS", StringComparison.OrdinalIgnoreCase))
        {
            rate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = transactionCurrency,
                Rate = exchangeRate,
                InverseRate = Math.Round(1m / exchangeRate, 6),
                EffectiveDate = new DateTime(2026, 7, 1),
                RateType = ExchangeRateType.Daily,
                RateSource = "Manual",
                IsActive = true,
                ApprovalStatus = RateApprovalStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            };
            db.ExchangeRates.Add(rate);
        }

        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"VAL-{tenantId.ToString("N")[..4]}",
            Name = "Valuation Assets",
            AssetAccountId = assetAccount.Id,
            AccumulatedDepreciationAccountId = accumulatedDepreciationAccount.Id,
            DepreciationExpenseAccountId = depreciationExpenseAccount.Id,
            RevaluationSurplusAccountId = revaluationSurplusAccount.Id,
            RevaluationLossAccountId = revaluationLossAccount.Id,
            ImpairmentLossAccountId = impairmentLossAccount.Id,
            AccumulatedImpairmentAccountId = accumulatedImpairmentAccount.Id,
            ImpairmentReversalAccountId = impairmentReversalAccount.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = usefulLifeMonths,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var capitalizationDate = isCapitalized ? new DateTime(2026, 6, 30) : (DateTime?)null;
        var journalEntryId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var postingEventId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var nbv = netBookValue ?? acquisitionCost - accumulatedDepreciation;
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-VAL-{tenantId.ToString("N")[..4]}",
            Name = "Revaluable asset",
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 6, 30),
            PlacedInServiceDate = status == FixedAssetStatus.Active ? new DateTime(2026, 7, 1) : null,
            PurchasePrice = acquisitionCost,
            AcquisitionCost = acquisitionCost,
            NetBookValue = nbv,
            UsefulLifeMonths = usefulLifeMonths,
            ResidualValue = 0m,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            Status = status,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrencyCode = transactionCurrency,
            ExchangeRate = transactionCurrency == "GHS" ? null : exchangeRate,
            ExchangeRateId = rate?.Id,
            ExchangeRateDate = rate?.EffectiveDate,
            CapitalizationDate = capitalizationDate,
            CapitalizedAt = capitalizationDate,
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
            UsefulLifeMonths = usefulLifeMonths,
            RemainingUsefulLifeMonths = remainingUsefulLifeMonths,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            CapitalizationDate = capitalizationDate,
            CapitalizationJournalEntryId = journalEntryId,
            CapitalizationPostingEventId = postingEventId,
            OpeningSource = "Capitalization",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        asset.BookValues.Add(bookValue);
        db.FixedAssetCategories.Add(category);
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();

        return new ValuationFixture(
            period,
            book,
            asset,
            category,
            assetAccount,
            revaluationSurplusAccount,
            revaluationLossAccount,
            impairmentLossAccount,
            accumulatedImpairmentAccount,
            rate);
    }

    private static CreateAssetValuationDto CreateValuationDto(Guid assetId, ValuationType valuationType, decimal fairValue)
        => new()
        {
            FixedAssetId = assetId,
            ValuationDate = new DateTime(2026, 7, 15),
            ValuationType = valuationType,
            FairValue = fairValue,
            ValuerName = "Independent valuer",
            ValuationMethod = valuationType == ValuationType.Impairment ? "Recoverable amount" : "Market comparison",
            ValuationReportReference = "VAL-REP-001",
            Reason = $"{valuationType} test reason"
        };

    private static async Task<JournalEntry> LoadValuationJournalAsync(ApplicationDbContext db, Guid valuationId)
        => await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "FixedAssetValuation" && j.SourceDocumentId == valuationId);

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
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

    private static AccountingBook SeedBook(ApplicationDbContext db, Guid tenantId)
    {
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
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

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen,
        bool isClosed)
    {
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = $"FY26-{tenantId.ToString("N")[..4]}", Year = 2026,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365, NumberOfPeriods = 12, Status = "Open", IsActive = true
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            FiscalYear = fiscalYear,
            PeriodName = "July 2026",
            PeriodCode = $"2026-07-{tenantId.ToString("N")[..4]}",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };

        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        bool allowDirectPosting = true)
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
            AllowDirectPosting = allowDirectPosting
        };

        db.Accounts.Add(account);
        return account;
    }

    private sealed record ServiceFixture(
        AssetValuationService Valuations,
        FixedAssetDepreciationService Depreciation);

    private sealed record ValuationFixture(
        FiscalPeriod Period,
        AccountingBook Book,
        FixedAsset Asset,
        FixedAssetCategory Category,
        Account AssetAccount,
        Account RevaluationSurplusAccount,
        Account RevaluationLossAccount,
        Account ImpairmentLossAccount,
        Account AccumulatedImpairmentAccount,
        ExchangeRate? ExchangeRate);
}
