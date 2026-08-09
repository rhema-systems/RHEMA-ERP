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

public sealed class FixedAssetDepreciationFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationCannotRunBeforeCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, isCapitalized: false, status: FixedAssetStatus.Draft);
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be capitalized before depreciation*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task BulkScheduleGeneration_ShouldSkipActiveAssetsThatAreNotCapitalized()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var ineligibleAsset = AddActiveUncapitalizedAsset(db, fixture, tenantId);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var schedules = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            PostToGl = false
        });

        schedules.Should().ContainSingle(schedule => schedule.FixedAssetId == fixture.Asset.Id);
        schedules.Should().NotContain(schedule => schedule.FixedAssetId == ineligibleAsset.Id);
        (await db.AssetDepreciationSchedules.AnyAsync(schedule => schedule.FixedAssetId == ineligibleAsset.Id)).Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "Workflow")]
    public async Task ApprovedBulkRun_ShouldNotPostWhenAnAssetBecomesIneligible()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(service => service.StartApprovalWorkflowAsync("FixedAssetDepreciationRun", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var services = CreateServices(db, tenantId, workflow.Object);

        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            PostToGl = true
        });

        fixture.Asset.Status = FixedAssetStatus.Disposed;
        var run = await db.FixedAssetDepreciationRuns.SingleAsync();
        run.Status = "Approved";
        await db.SaveChangesAsync();

        var post = () => services.Depreciation.PostApprovedRunAsync(run.Id);

        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status is not eligible for depreciation*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.AssetDepreciationSchedules.SingleAsync()).IsPosted.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationCannotRunBeforeActivationOrPlacedInServiceDate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, status: FixedAssetStatus.Capitalized);
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be active before depreciation*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task StraightLineScheduleGeneration_ShouldBeDeterministicAndNotUpdateBookValueUntilPosted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, acquisitionCost: 1200m, usefulLifeMonths: 12);
        var services = CreateServices(db, tenantId);

        var result = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id,
            PostToGl = false
        });

        result.Should().ContainSingle();
        result.Single().DepreciationAmount.Should().Be(100m);
        result.Single().IsProjected.Should().BeTrue();
        var book = await db.FixedAssetBookValues.SingleAsync(v => v.FixedAssetId == fixture.Asset.Id);
        book.AccumulatedDepreciation.Should().Be(0m);
        book.NetBookValue.Should().Be(1200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationAmountRoundingIsDeterministic()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, acquisitionCost: 1000m, usefulLifeMonths: 3);
        var services = CreateServices(db, tenantId);

        var result = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id,
            PostToGl = false
        });

        result.Single().DepreciationAmount.Should().Be(333.33m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task ResidualValueCannotExceedAssetCost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, acquisitionCost: 100m, residualValue: 101m);
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*residual value cannot exceed*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task UsefulLifeMustBePositive()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, usefulLifeMonths: 0);
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*useful life must be greater than zero*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task ScheduleTotalDoesNotExceedDepreciableAmount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(
            db,
            tenantId,
            acquisitionCost: 1000m,
            accumulatedDepreciation: 895m,
            netBookValue: 105m,
            residualValue: 100m,
            usefulLifeMonths: 12);
        var services = CreateServices(db, tenantId);

        var result = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        result.Single().DepreciationAmount.Should().Be(5m);
        result.Single().NetBookValue.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationPostsThroughPostingEngine_WithExpectedDebitAndCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var result = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        result.Single().IsPosted.Should().BeTrue();
        result.Single().PostingEventId.Should().NotBeNull();
        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.SourceDocumentType == "FixedAssetDepreciationRun" &&
            e.PostingAction == "Depreciation");
        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync(j => j.Id == postingEvent.JournalEntryId);
        journal.Transactions.Single(t => t.AccountId == fixture.DepreciationExpenseAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.AccumulatedDepreciationAccount.Id).CreditAmount.Should().Be(100m);
        journal.Transactions.Should().OnlyContain(t =>
            t.TransactionTag == "FA-Depreciation" ||
            t.TransactionTag == "FA-AccumulatedDepreciation");
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task DepreciationRunRequiresWorkflowApprovalBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var workflowInstanceId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("FixedAssetDepreciationRun", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = workflowInstanceId
            });
        var services = CreateServices(db, tenantId, workflow.Object);

        var pending = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        pending.Single().IsPosted.Should().BeFalse();
        var run = await db.FixedAssetDepreciationRuns.SingleAsync();
        run.Status.Should().Be("PendingApproval");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);

        var unapprovedPost = () => services.Depreciation.PostApprovedRunAsync(run.Id);
        await unapprovedPost.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved*");

        run.Status = "Approved";
        await db.SaveChangesAsync();
        var posted = await services.Depreciation.PostApprovedRunAsync(run.Id);

        posted.Single().IsPosted.Should().BeTrue();
        posted.Single().PostingEventId.Should().NotBeNull();
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "FixedAssetDepreciationRun")).Should().Be(1);
        workflow.Verify(x => x.StartApprovalWorkflowAsync("FixedAssetDepreciationRun", run.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task MissingDepreciationExpenseAccountBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        fixture.Category.DepreciationExpenseAccountId = Guid.Empty;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depreciation expense account is required*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task MissingAccumulatedDepreciationAccountBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        fixture.Category.AccumulatedDepreciationAccountId = Guid.Empty;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*accumulated depreciation account is required*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantDepreciationAccountRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherExpense = SeedAccount(db, otherTenantId, "6700", AccountType.Expense);
        fixture.Category.DepreciationExpenseAccountId = otherExpense.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depreciation expense account was not found for this tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task ClosedPeriodDepreciationPostingRejectedAndAudited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var services = CreateServices(db, tenantId);

        var act = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*locked or not open*");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDepreciationBlockedClosedPeriod)).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DuplicateDepreciationPeriodReturnsExistingRunWithoutDuplicatePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var first = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });
        var second = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        second.Single().Id.Should().Be(first.Single().Id);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.AssetDepreciationSchedules.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task AccumulatedDepreciationAndNbvUpdateAfterPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        var book = await db.FixedAssetBookValues.SingleAsync(v => v.FixedAssetId == fixture.Asset.Id);
        book.AccumulatedDepreciation.Should().Be(100m);
        book.NetBookValue.Should().Be(1100m);
        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.NetBookValue.Should().Be(1100m);
        asset.AcquisitionCost.Should().Be(1200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task LaterDepreciationAssumptionChangeDoesNotRewritePostedDepreciation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        var act = () => services.FixedAssets.UpdateAsync(fixture.Asset.Id, new UpdateFixedAssetDto
        {
            AssetCode = fixture.Asset.AssetCode,
            Name = fixture.Asset.Name,
            FixedAssetCategoryId = fixture.Category.Id,
            PurchaseDate = fixture.Asset.PurchaseDate,
            PlacedInServiceDate = fixture.Asset.PlacedInServiceDate,
            PurchasePrice = fixture.Asset.PurchasePrice,
            InstallationCost = fixture.Asset.InstallationCost,
            TaxAmount = fixture.Asset.TaxAmount,
            AcquisitionCost = fixture.Asset.AcquisitionCost,
            DepreciationMethod = fixture.Asset.DepreciationMethod,
            DepreciationConvention = fixture.Asset.DepreciationConvention,
            UsefulLifeMonths = 24,
            ResidualValue = fixture.Asset.ResidualValue,
            Status = fixture.Asset.Status
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depreciation assumptions cannot be edited*");
        var postedSchedule = await db.AssetDepreciationSchedules.SingleAsync();
        postedSchedule.DepreciationAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationRunCreatesAuditEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        var actions = await db.AuditLogs.Where(a => a.TenantId == tenantId).Select(a => a.Action).ToListAsync();
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDepreciationRunCreated);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDepreciationCalculated);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDepreciationPosted);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyAssetDepreciationUsesFunctionalCapitalizedCostNotCurrentExchangeRate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(
            db,
            tenantId,
            acquisitionCost: 1200m,
            transactionCurrency: "USD",
            exchangeRate: 12m);
        var services = CreateServices(db, tenantId);

        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        fixture.ExchangeRate!.Rate = 13m;
        await db.SaveChangesAsync();

        var schedule = await db.AssetDepreciationSchedules.SingleAsync();
        schedule.DepreciationAmount.Should().Be(100m);
        var journalLine = await db.AccountTransactions.SingleAsync(t => t.TransactionTag == "FA-Depreciation");
        journalLine.DebitAmount.Should().Be(100m);
        journalLine.TransactionCurrency.Should().Be("GHS");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciation")]
    [Trait("Category", "FixedAssets")]
    public async Task DepreciationDoesNotMutateAssetAcquisitionCost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });

        var asset = await db.FixedAssets.SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(1200m);
        asset.PurchasePrice.Should().Be(1200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciationReversal")]
    [Trait("Requirement", "FR-GL-008;FR-GL-010;FIN-LIM-0033")]
    public async Task ApprovedDepreciationReversal_ShouldPostCompensatingJournalRestoreRegisterAndAllowCorrectionRevision()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var maker = CreateServices(db, tenantId, userId: makerId, userName: "fa.depreciation.maker");

        var originalSchedules = await maker.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });
        var originalSchedule = originalSchedules.Single();
        var originalRun = await db.FixedAssetDepreciationRuns.SingleAsync();
        var request = await maker.Depreciation.RequestReversalAsync(
            originalRun.Id,
            new RequestFixedAssetDepreciationReversalDto
            {
                ReversalDate = new DateTime(2026, 7, 25),
                Reason = "The approved useful-life evidence was entered incorrectly and requires a controlled correction.",
                ImpactAssessment = "The July depreciation expense and reserve will be reversed before a corrected July run is posted."
            });

        // Resolve the service again with a different authenticated identity. This proves the
        // maker-checker rule is enforced in the domain service rather than only by the page.
        var reviewer = CreateServices(
            db,
            tenantId,
            userId: Guid.NewGuid(),
            userName: "fa.depreciation.reviewer");
        var approved = await reviewer.Depreciation.ReviewReversalAsync(
            originalRun.Id,
            request.Id,
            new ReviewFixedAssetDepreciationReversalDto
            {
                Approved = true,
                ReviewComment = "The source evidence and downstream impact were independently checked and the reversal is approved."
            });
        approved.Status.Should().Be(FixedAssetDepreciationReversalStatuses.Approved);

        var posted = await maker.Depreciation.PostReversalAsync(originalRun.Id, request.Id);

        posted.Status.Should().Be(FixedAssetDepreciationReversalStatuses.Posted);
        posted.ReversalPostingEventId.Should().NotBeNull();
        posted.ReversalJournalEntryId.Should().NotBeNull();
        var reversedSchedule = await db.AssetDepreciationSchedules.SingleAsync(item => item.Id == originalSchedule.Id);
        reversedSchedule.IsPosted.Should().BeTrue("the immutable original posting remains accounting evidence");
        reversedSchedule.IsReversed.Should().BeTrue();
        reversedSchedule.ReversalPostingEventId.Should().Be(posted.ReversalPostingEventId);
        var restoredBook = await db.FixedAssetBookValues.SingleAsync(item => item.FixedAssetId == fixture.Asset.Id);
        restoredBook.AccumulatedDepreciation.Should().Be(0m);
        restoredBook.NetBookValue.Should().Be(1200m);
        (await db.AssetTransactions.CountAsync(item =>
            item.FixedAssetId == fixture.Asset.Id && item.TransactionType == "DepreciationReversal")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "FixedAssetDepreciationReversal" && item.PostingAction == "Reverse")).Should().Be(1);

        // A corrected same-period run receives revision one instead of deleting or colliding
        // with revision zero. This is the core evidence-retention behavior for FIN-LIM-0033.
        var corrected = await maker.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });
        corrected.Single().CorrectionSequence.Should().Be(1);
        (await db.FixedAssetDepreciationRuns.CountAsync()).Should().Be(2);
        (await db.AssetDepreciationSchedules.CountAsync()).Should().Be(2);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciationReversal")]
    [Trait("Requirement", "FR-GL-008;FIN-LIM-0033")]
    public async Task DepreciationReversalRequester_ShouldNotApproveOwnRequest()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId, userId: actorId, userName: "same.fa.actor");
        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });
        var run = await db.FixedAssetDepreciationRuns.SingleAsync();
        var request = await services.Depreciation.RequestReversalAsync(
            run.Id,
            new RequestFixedAssetDepreciationReversalDto
            {
                ReversalDate = new DateTime(2026, 7, 25),
                Reason = "The posted depreciation used an incorrect approved assumption and must be corrected.",
                ImpactAssessment = "The original expense and accumulated depreciation will be reversed before reposting."
            });

        var act = () => services.Depreciation.ReviewReversalAsync(
            run.Id,
            request.Id,
            new ReviewFixedAssetDepreciationReversalDto
            {
                Approved = true,
                ReviewComment = "This attempted self-approval must be rejected by the service-level maker-checker guard."
            });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requester cannot review*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDepreciationReversal")]
    [Trait("Requirement", "FR-GL-010;FIN-LIM-0033")]
    public async Task LaterPostedDepreciation_ShouldBlockEarlierRunReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDepreciationFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetId = fixture.Asset.Id
        });
        var run = await db.FixedAssetDepreciationRuns.SingleAsync();
        var original = await db.AssetDepreciationSchedules.SingleAsync();

        // Seed a later posted schedule directly so the test isolates reversal sequencing without
        // requiring another fiscal-period fixture. The service must force reverse-order unwind.
        db.AssetDepreciationSchedules.Add(new AssetDepreciationSchedule
        {
            TenantId = tenantId,
            FixedAssetId = fixture.Asset.Id,
            FiscalPeriodId = fixture.Period.Id,
            AccountingBookId = original.AccountingBookId,
            BookClassification = original.BookClassification,
            CorrectionSequence = 1,
            DepreciationAmount = 100m,
            AccumulatedDepreciationBefore = 100m,
            AccumulatedDepreciation = 200m,
            NetBookValueBefore = 1100m,
            NetBookValue = 1000m,
            DepreciableAmount = 1200m,
            PostingDate = original.PostingDate!.Value.AddDays(1),
            IsPosted = true,
            IsProjected = false,
            PostingEventId = Guid.NewGuid(),
            JournalEntryId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var act = () => services.Depreciation.RequestReversalAsync(
            run.Id,
            new RequestFixedAssetDepreciationReversalDto
            {
                ReversalDate = new DateTime(2026, 7, 25),
                Reason = "The earlier posted depreciation requires correction after later accounting was recorded.",
                ImpactAssessment = "The request must be blocked until dependent later depreciation is reversed first."
            });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Reverse later runs before reversing this run*");
        (await db.FixedAssetDepreciationReversals.CountAsync()).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-depreciation-foundation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ServiceFixture CreateServices(
        ApplicationDbContext db,
        Guid tenantId,
        IWorkflowService? workflowService = null,
        Guid? userId = null,
        string userName = "fa.depreciation")
    {
        var currentUser = CreateCurrentUser(tenantId, userId, userName);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-depreciation" }
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
            financeAuditService: auditService);
        var depreciationService = new FixedAssetDepreciationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService,
            workflowService: workflowService,
            financeReversalPolicyService: reversalPolicy);

        return new ServiceFixture(depreciationService, fixedAssetService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(
        Guid tenantId,
        Guid? userId = null,
        string userName = "fa.depreciation")
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns((userId ?? Guid.NewGuid()).ToString());
        currentUser.SetupGet(x => x.UserName).Returns(userName);
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-depreciation-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<DepreciationFixture> SeedDepreciationFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        decimal acquisitionCost = 1200m,
        decimal accumulatedDepreciation = 0m,
        decimal? netBookValue = null,
        decimal residualValue = 0m,
        int usefulLifeMonths = 12,
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
        var assetAccount = SeedAccount(db, tenantId, "1600", AccountType.Asset);
        var accumulatedAccount = SeedAccount(db, tenantId, "1699", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, "6700", AccountType.Expense);

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
            Code = $"DEP-{tenantId.ToString("N")[..4]}",
            Name = "Depreciable Assets",
            AssetAccountId = assetAccount.Id,
            AccumulatedDepreciationAccountId = accumulatedAccount.Id,
            DepreciationExpenseAccountId = expenseAccount.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = usefulLifeMonths,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var capitalizedAt = isCapitalized ? new DateTime(2026, 6, 30) : (DateTime?)null;
        var journalEntryId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var postingEventId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-DEP-{tenantId.ToString("N")[..4]}",
            Name = "Depreciable laptop",
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 6, 30),
            PlacedInServiceDate = status == FixedAssetStatus.Active ? new DateTime(2026, 7, 1) : null,
            PurchasePrice = acquisitionCost,
            InstallationCost = 0m,
            TaxAmount = 0m,
            AcquisitionCost = acquisitionCost,
            NetBookValue = netBookValue ?? acquisitionCost - accumulatedDepreciation,
            UsefulLifeMonths = usefulLifeMonths,
            ResidualValue = residualValue,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            Status = status,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrencyCode = transactionCurrency,
            ExchangeRate = transactionCurrency == "GHS" ? null : exchangeRate,
            ExchangeRateId = rate?.Id,
            ExchangeRateDate = rate?.EffectiveDate,
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
            NetBookValue = netBookValue ?? acquisitionCost - accumulatedDepreciation,
            ResidualValue = residualValue,
            UsefulLifeMonths = usefulLifeMonths,
            RemainingUsefulLifeMonths = usefulLifeMonths,
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
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();

        return new DepreciationFixture(
            period,
            book,
            asset,
            category,
            assetAccount,
            accumulatedAccount,
            expenseAccount,
            rate);
    }

    private static FixedAsset AddActiveUncapitalizedAsset(
        ApplicationDbContext db,
        DepreciationFixture fixture,
        Guid tenantId)
    {
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-UNCAP-{Guid.NewGuid():N}"[..18],
            Name = "Uncapitalized imported asset",
            FixedAssetCategoryId = fixture.Category.Id,
            PurchaseDate = new DateTime(2026, 6, 30),
            PlacedInServiceDate = new DateTime(2026, 7, 1),
            PurchasePrice = 1200m,
            AcquisitionCost = 1200m,
            NetBookValue = 1200m,
            UsefulLifeMonths = 12,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            Status = FixedAssetStatus.Active,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        asset.BookValues.Add(new FixedAssetBookValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            AcquisitionCost = 1200m,
            NetBookValue = 1200m,
            UsefulLifeMonths = 12,
            RemainingUsefulLifeMonths = 12,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        db.FixedAssets.Add(asset);
        return asset;
    }

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
        FixedAssetDepreciationService Depreciation,
        FixedAssetService FixedAssets);

    private sealed record DepreciationFixture(
        FiscalPeriod Period,
        AccountingBook Book,
        FixedAsset Asset,
        FixedAssetCategory Category,
        Account AssetAccount,
        Account AccumulatedDepreciationAccount,
        Account DepreciationExpenseAccount,
        ExchangeRate? ExchangeRate);
}
