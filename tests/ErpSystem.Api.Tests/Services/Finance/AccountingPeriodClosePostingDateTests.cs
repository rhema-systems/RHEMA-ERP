using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Fiscal;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingPeriodClosePostingDateTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task OpenPeriod_ShouldAllowAdjacentOpenPeriodsButRejectFutureGapsAndAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var july = SeedPeriod(db, tenantId);
        var august = CreateSiblingPeriod(july, 8, "August 2026", new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
        var september = CreateSiblingPeriod(july, 9, "September 2026", new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
        db.FiscalPeriods.AddRange(august, september);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var gapAttempt = () => service.OpenPeriodAsync(new PeriodOpenRequestDto
        {
            FiscalPeriodId = september.Id,
            Reason = "Begin September operational posting"
        });
        await gapAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Open earlier period '2026-08 - August 2026'*");

        var opened = await service.OpenPeriodAsync(new PeriodOpenRequestDto
        {
            FiscalPeriodId = august.Id,
            Reason = "Begin August operational posting"
        });

        opened.PeriodStatus.Should().Be("Open");
        // The rejected gap attempt rolls back the controlled unit and clears EF's change tracker.
        // Reload persisted state instead of inspecting the now-detached setup objects; this also
        // mirrors what the frontend receives from its post-operation refresh.
        var persistedAugust = await db.FiscalPeriods.AsNoTracking()
            .SingleAsync(item => item.Id == august.Id);
        var persistedJuly = await db.FiscalPeriods.AsNoTracking()
            .SingleAsync(item => item.Id == july.Id);
        persistedAugust.IsOpen.Should().BeTrue();
        persistedAugust.IsClosed.Should().BeFalse();
        persistedJuly.IsOpen.Should().BeTrue("the prior period can remain open while Finance completes its close");
        (await db.AuditLogs.CountAsync(item =>
            item.TenantId == tenantId &&
            item.Action == FinanceAuditEvents.AccountingPeriodOpened)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task OpenPeriod_ShouldRejectClosedPeriodBecauseItRequiresControlledReopen()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId, isOpen: false, isClosed: true);
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId).OpenPeriodAsync(new PeriodOpenRequestDto
        {
            FiscalPeriodId = period.Id,
            Reason = "Attempt direct opening after close"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A closed period must use the controlled reopen request and approval workflow.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task PostingDatePolicy_ShouldExposePersistAndAuditChange_ButProtectClosedPeriods()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var openPeriod = SeedPeriod(db, tenantId);
        var closedPeriod = CreateSiblingPeriod(
            openPeriod,
            6,
            "June 2026",
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 30));
        closedPeriod.PeriodStatus = "Closed";
        closedPeriod.IsOpen = false;
        closedPeriod.IsClosed = true;
        db.FiscalPeriods.Add(closedPeriod);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var updated = await service.UpdatePostingDatePolicyAsync(openPeriod.Id, new PeriodPostingDatePolicyRequestDto
        {
            AllowFutureDating = true,
            Reason = "Permit controlled September preparation"
        });

        updated.AllowFutureDating.Should().BeTrue();
        (await db.FiscalPeriods.AsNoTracking().SingleAsync(period => period.Id == openPeriod.Id))
            .AllowFutureDating.Should().BeTrue();
        (await db.AuditLogs.CountAsync(log =>
            log.TenantId == tenantId
            && log.Action == FinanceAuditEvents.AccountingPeriodPostingDatePolicyUpdated)).Should().Be(1);

        var closedChange = () => service.UpdatePostingDatePolicyAsync(closedPeriod.Id, new PeriodPostingDatePolicyRequestDto
        {
            AllowFutureDating = true,
            Reason = "Attempt change after certified close"
        });
        await closedChange.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closing, closed, or locked*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ClosePeriod_ShouldCloseOnlyCurrentTenantPeriodAndAudit()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var period = SeedPeriod(db, tenantId);
        var otherPeriod = SeedPeriod(db, otherTenantId);
        await db.SaveChangesAsync();

        var preparerId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var preparer = CreateService(db, tenantId, preparerId, "finance.preparer");
        var service = CreateService(db, tenantId, reviewerId, "finance.reviewer");

        var prepared = await preparer.PreparePeriodCloseAsync(period.Id, new PeriodClosePreparationRequestDto
        {
            Declaration = "I reviewed every close check and confirm the evidence is complete."
        });
        prepared.Status.Should().Be(FinanceCloseStatuses.Prepared);
        prepared.Checks.Should().HaveCount(11);

        var result = await service.ClosePeriodAsync(new PeriodCloseRequestDto
        {
            FiscalPeriodId = period.Id,
            ClosingNotes = "Month-end close complete",
            ReviewerDeclaration = "I independently reviewed the evidence and approve this period close."
        });

        result.Success.Should().BeTrue();
        period.IsClosed.Should().BeTrue();
        period.IsOpen.Should().BeFalse();
        period.PeriodStatus.Should().Be("Closed");
        otherPeriod.IsOpen.Should().BeTrue();
        otherPeriod.IsClosed.Should().BeFalse();

        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodCloseRequested)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodClosed)).Should().Be(1);
        (await db.AuditLogs.AnyAsync(a => a.TenantId == otherTenantId)).Should().BeFalse();
        var certificate = await db.FinanceCloseCertifications.SingleAsync();
        certificate.PreparedByUserId.Should().Be(preparerId);
        certificate.ApprovedByUserId.Should().Be(reviewerId);
        // Preparation and final approval each append the complete eleven-provider evidence set.
        (await db.FinanceCloseCheckSnapshots.CountAsync()).Should().Be(22);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ClosePeriod_ShouldFailValidation_WhenApprovedApInvoiceIsUnposted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.VendorInvoices.Add(new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "AP-001",
            SupplierId = Guid.NewGuid(),
            SupplierName = "Supplier",
            InvoiceDate = new DateTime(2026, 7, 10),
            CurrencyCode = "GHS",
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            SubTotal = 100m,
            TotalAmount = 100m
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var result = await service.ClosePeriodAsync(new PeriodCloseRequestDto
        {
            FiscalPeriodId = period.Id,
            ClosingNotes = "Attempt close with unposted AP",
            ReviewerDeclaration = "I independently reviewed this attempted period close evidence."
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("approved Finance source documents", StringComparison.OrdinalIgnoreCase));
        period.IsOpen.Should().BeTrue();
        period.IsClosed.Should().BeFalse();
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodCloseValidationFailed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ReopenPeriod_ShouldRequireIndependentApprovalAndAuditSuccessfulReopen()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId, isOpen: false, isClosed: true);
        SeedCertifiedClosedCycle(db, tenantId, period);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, requesterId, "finance.manager");

        var missingReason = () => service.RequestPeriodReopenAsync(new PeriodReopenRequestDto
        {
            FiscalPeriodId = period.Id,
            Reason = " ",
            AffectedPeriodAssessment = "The correction affects the July management accounts only."
        });

        await missingReason.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least 20 characters*");

        var reopenRequest = await service.RequestPeriodReopenAsync(new PeriodReopenRequestDto
        {
            FiscalPeriodId = period.Id,
            Reason = "External audit adjustment required",
            AffectedPeriodAssessment = "The correction affects the July management accounts and will be recertified."
        });

        reopenRequest.Status.Should().Be(FinancePeriodReopenStatuses.PendingApproval);
        (await db.FiscalPeriods.SingleAsync(item => item.Id == period.Id)).IsClosed.Should().BeTrue();
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodReopenRequested)).Should().Be(1);

        var selfReview = () => service.ReviewPeriodReopenAsync(period.Id, reopenRequest.Id, new PeriodReopenReviewDto
        {
            Approved = true,
            ReviewComment = "I independently approve this controlled accounting correction."
        });
        await selfReview.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requester cannot review*");

        var approver = CreateService(db, tenantId, approverId, "financial.controller");
        var approved = await approver.ReviewPeriodReopenAsync(period.Id, reopenRequest.Id, new PeriodReopenReviewDto
        {
            Approved = true,
            ReviewComment = "I independently reviewed the affected periods and approve reopening."
        });

        approved.Status.Should().Be(FinancePeriodReopenStatuses.Approved);
        approved.ResultingFinanceCloseCycleId.Should().NotBeNull();
        var reopened = await db.FiscalPeriods.SingleAsync(item => item.Id == period.Id);
        reopened.IsOpen.Should().BeTrue();
        reopened.IsClosed.Should().BeFalse();
        reopened.ReopenCount.Should().Be(1);
        reopened.ReopenReason.Should().Be("External audit adjustment required");
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodReopened)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task RequestPeriodReopen_ShouldBlockWhileALaterPeriodRemainsClosed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var target = SeedPeriod(db, tenantId, isOpen: false, isClosed: true);
        SeedCertifiedClosedCycle(db, tenantId, target);
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = target.FiscalYearId,
            PeriodName = "August 2026",
            PeriodCode = "2026-08",
            PeriodNumber = 8,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 31),
            PeriodDays = 31,
            PeriodStatus = "Closed",
            IsClosed = true,
            IsOpen = false
        });
        await db.SaveChangesAsync();

        var act = () => CreateService(db, tenantId).RequestPeriodReopenAsync(new PeriodReopenRequestDto
        {
            FiscalPeriodId = target.Id,
            Reason = "External audit correction is required in July",
            AffectedPeriodAssessment = "The correction changes carried balances reported in the August close."
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*working backwards*");
        (await db.FinancePeriodReopenRequests.CountAsync()).Should().Be(0);
        (await db.FiscalPeriods.SingleAsync(item => item.Id == target.Id)).IsClosed.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ClosePeriod_ShouldEnforceMakerCheckerAndStartNewCycleAfterReopen()
    {
        var tenantId = Guid.NewGuid();
        var preparerId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        await db.SaveChangesAsync();

        var preparer = CreateService(db, tenantId, preparerId, "finance.preparer");
        await preparer.PreparePeriodCloseAsync(period.Id, new PeriodClosePreparationRequestDto
        {
            Declaration = "I reviewed the complete period-close evidence as the preparer."
        });

        var selfApproval = () => preparer.ClosePeriodAsync(new PeriodCloseRequestDto
        {
            FiscalPeriodId = period.Id,
            ReviewerDeclaration = "I also approve the same period-close evidence as reviewer."
        });
        await selfApproval.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*second authorised user*");

        var reviewer = CreateService(db, tenantId, reviewerId, "finance.reviewer");
        (await reviewer.ClosePeriodAsync(new PeriodCloseRequestDto
        {
            FiscalPeriodId = period.Id,
            ReviewerDeclaration = "I independently reviewed the close evidence and approve closure."
        })).Success.Should().BeTrue();

        var reopenRequest = await reviewer.RequestPeriodReopenAsync(new PeriodReopenRequestDto
        {
            FiscalPeriodId = period.Id,
            Reason = "External audit adjustment requires a controlled reopen",
            AffectedPeriodAssessment = "The adjustment affects this period and every later open reporting period."
        });

        var reopenApprover = CreateService(db, tenantId, Guid.NewGuid(), "financial.controller");
        var approvedReopen = await reopenApprover.ReviewPeriodReopenAsync(
            period.Id,
            reopenRequest.Id,
            new PeriodReopenReviewDto
            {
                Approved = true,
                ReviewComment = "I verified the period impact and approve a controlled reopen and re-close."
            });

        var firstCycle = await db.FinanceCloseCycles.SingleAsync(item => item.CycleNumber == 1);
        firstCycle.Status.Should().Be(FinanceCloseStatuses.Reopened);
        (await db.FinanceCloseCertifications.SingleAsync()).IsSuperseded.Should().BeTrue();
        approvedReopen.ResultingFinanceCloseCycleId.Should().NotBeNull();

        var secondCycle = await preparer.EvaluatePeriodCloseWorkspaceAsync(period.Id);
        secondCycle.CycleNumber.Should().Be(2);
        secondCycle.History.Should().Contain(item => item.CycleNumber == 1 && item.Status == FinanceCloseStatuses.Reopened);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ValidatePeriodClose_ShouldApplyConfigurableDepreciationBlockerWithTdcDefaultOn()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var assetId = Guid.NewGuid();
        db.FixedAssets.Add(new FixedAsset
        {
            Id = assetId,
            TenantId = tenantId,
            AssetCode = "FA-CLOSE-001",
            Name = "Depreciable test asset",
            FixedAssetCategoryId = Guid.NewGuid(),
            PurchaseDate = new DateTime(2026, 1, 1),
            PlacedInServiceDate = new DateTime(2026, 1, 1),
            CapitalizedAt = new DateTime(2026, 1, 1),
            PurchasePrice = 12_000m,
            AcquisitionCost = 12_000m,
            NetBookValue = 10_000m,
            ResidualValue = 0m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            Status = FixedAssetStatus.Active
        });
        // A posted flag without source-to-ledger evidence must not satisfy the close check.
        db.AssetDepreciationSchedules.Add(new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = assetId,
            FiscalPeriodId = period.Id,
            DepreciationAmount = 200m,
            IsPosted = true,
            IsProjected = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var defaultValidation = await service.ValidatePeriodCloseAsync(period.Id);
        defaultValidation.CanClose.Should().BeFalse();
        defaultValidation.ValidationErrors.Should().Contain(message =>
            message.Contains("invalid journal/posting-event evidence", StringComparison.OrdinalIgnoreCase));

        var settings = await db.FinanceSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.RequireDepreciationBeforePeriodClose = false;
        await db.SaveChangesAsync();

        var policyExceptionValidation = await service.ValidatePeriodCloseAsync(period.Id);
        policyExceptionValidation.ValidationErrors.Should().NotContain(message =>
            message.Contains("depreciation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ValidatePeriodClose_ShouldDetectOrphanedPostingEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "TEST",
            SourceDocumentType = "TestDocument",
            SourceDocumentId = Guid.NewGuid(),
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = new DateTime(2026, 7, 10),
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var validation = await service.ValidatePeriodCloseAsync(period.Id);

        validation.CanClose.Should().BeFalse();
        validation.ValidationErrors.Should().Contain(e => e.Contains("posting events", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ValidatePeriodClose_ShouldDetectPostedJournalMissingPostingEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = "JE-001",
            Description = "Posted journal without posting event",
            EntryDate = new DateTime(2026, 7, 10),
            FiscalPeriodId = period.Id,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var validation = await service.ValidatePeriodCloseAsync(period.Id);

        validation.CanClose.Should().BeFalse();
        validation.ValidationErrors.Should().Contain(e => e.Contains("posted journal entries are missing posting events", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task EvaluateCloseWorkspace_ShouldSeedTdcTemplatesAndCopyTheMatchingApprovedVersion()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var workspace = await service.EvaluatePeriodCloseWorkspaceAsync(period.Id);

        workspace.TemplateCode.Should().Be("TDC-MONTH-END");
        workspace.CloseType.Should().Be(FinanceCloseTemplateTypes.MonthEnd);
        workspace.TemplateVersion.Should().Be(1);
        workspace.Checks.Single(check => check.CheckCode == "AP_CONTROL_RECONCILIATION").Status
            .Should().Be(FinanceCloseCheckStatuses.Passed);
        workspace.Checks.Single(check => check.CheckCode == "AR_CONTROL_RECONCILIATION").Status
            .Should().Be(FinanceCloseCheckStatuses.Passed);
        workspace.Checks.Single(check => check.CheckCode == "AP_UNAPPLIED_BALANCES").Status
            .Should().Be(FinanceCloseCheckStatuses.Passed);
        workspace.Checks.Single(check => check.CheckCode == "AR_UNAPPLIED_BALANCES").Status
            .Should().Be(FinanceCloseCheckStatuses.Passed);
        workspace.Checks.Single(check => check.CheckCode == "RECURRING_JOURNAL_EXCEPTIONS").Status
            .Should().Be(FinanceCloseCheckStatuses.Passed);
        workspace.Checks.Single(check => check.CheckCode == "BUDGET_ADOPTION_REVIEW").Status
            .Should().Be(FinanceCloseCheckStatuses.Passed);
        (await db.FinanceCloseTemplates.CountAsync(item => item.IsActive)).Should().Be(3);
        (await db.FinanceCloseTemplates.AllAsync(item =>
            item.Status == FinanceCloseTemplateStatuses.Approved && item.IsSystemDefault)).Should().BeTrue();

        var cycle = await db.FinanceCloseCycles.Include(item => item.Tasks).SingleAsync();
        cycle.FinanceCloseTemplateId.Should().NotBeNull();
        cycle.Tasks.Should().HaveCount(12);
        cycle.Tasks.Should().Contain(task =>
            task.CheckCode == "RECURRING_JOURNAL_EXCEPTIONS" && task.IsMandatory);
        cycle.Tasks.Should().Contain(task => task.CheckCode == "AP_CONTROL_RECONCILIATION" && task.IsMandatory);
        cycle.Tasks.Should().Contain(task => task.CheckCode == "AR_CONTROL_RECONCILIATION" && task.IsMandatory);
        cycle.Tasks.Should().Contain(task => task.CheckCode == "AP_UNAPPLIED_BALANCES" && !task.IsMandatory);
        cycle.Tasks.Should().Contain(task => task.CheckCode == "AR_UNAPPLIED_BALANCES" && !task.IsMandatory);
        cycle.Tasks.Should().Contain(task =>
            task.CheckCode == "BUDGET_ADOPTION_REVIEW" && !task.IsMandatory);
        cycle.Tasks.Should().OnlyContain(task => task.DueAt == period.EndDate.Date.AddDays(5));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task EvaluateCloseWorkspace_ShouldBlockUnresolvedRecurringJournalGenerationException()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var template = new RecurringJournalTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateNumber = "RJ-ACCRUAL-001",
            Name = "Monthly utilities accrual",
            Status = RecurringJournalStatus.Active,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            Frequency = RecurrenceFrequency.Monthly,
            RecurrenceRuleJson = "{}"
        };
        db.RecurringJournalTemplates.Add(template);
        db.RecurringJournalOccurrences.Add(new RecurringJournalOccurrence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateId = template.Id,
            TemplateVersion = 1,
            SequenceNumber = 7,
            ScheduledDate = new DateOnly(2026, 7, 31),
            EffectiveDate = new DateOnly(2026, 7, 31),
            Status = RecurringJournalOccurrenceStatus.SubmissionFailed,
            AttemptCount = 2,
            ErrorMessage = "Journal workflow submission could not be completed.",
            TemplateSnapshotJson = "{}"
        });
        await db.SaveChangesAsync();

        var workspace = await CreateService(db, tenantId)
            .EvaluatePeriodCloseWorkspaceAsync(period.Id);

        var recurringCheck = workspace.Checks.Single(check =>
            check.CheckCode == "RECURRING_JOURNAL_EXCEPTIONS");
        recurringCheck.Severity.Should().Be(FinanceCloseCheckSeverities.Mandatory);
        recurringCheck.Status.Should().Be(FinanceCloseCheckStatuses.Failed);
        recurringCheck.ExceptionCount.Should().Be(1);
        recurringCheck.ResultSummary.Should().Contain("require resolution before close");
        workspace.MandatoryBlockerCount.Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task EvaluateCloseWorkspace_ShouldWarnForApprovedBudgetAwaitingAdoption()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.BudgetScenarios.Add(new BudgetScenario
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = period.FiscalYearId,
            Name = "FY2026 approved revision 1",
            BaseCurrencyCode = "GHS",
            Status = "Approved",
            IsActive = false,
            LockedDate = new DateTime(2026, 7, 20),
            LockedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var workspace = await CreateService(db, tenantId)
            .EvaluatePeriodCloseWorkspaceAsync(period.Id);

        var budgetCheck = workspace.Checks.Single(check =>
            check.CheckCode == "BUDGET_ADOPTION_REVIEW");
        budgetCheck.Severity.Should().Be(FinanceCloseCheckSeverities.Warning);
        budgetCheck.Status.Should().Be(FinanceCloseCheckStatuses.Warning);
        budgetCheck.ExceptionCount.Should().Be(1);
        budgetCheck.ResultSummary.Should().Contain("not adopted as the official reporting baseline");
        workspace.WarningCount.Should().Be(1);
        workspace.MandatoryBlockerCount.Should().Be(0);
        workspace.CanPrepare.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task CloseTemplate_ShouldRequireIndependentApprovalAndBlockForManualEvidenceTask()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        await db.SaveChangesAsync();

        var author = CreateService(db, tenantId, authorId, "template.author");
        var baseline = (await author.GetFinanceCloseTemplatesAsync())
            .Single(item => item.CloseType == FinanceCloseTemplateTypes.MonthEnd && item.IsActive);
        var request = BuildTemplateVersionRequest(baseline);
        request.Tasks.Add(new SaveFinanceCloseTemplateTaskDto
        {
            TaskCode = "MANUAL_CONTROL_REVIEW",
            Title = "Manual control-account review evidence",
            Category = "Finance Review",
            DependsOnTaskCode = "TRIAL_BALANCE",
            Sequence = 130,
            IsMandatory = true,
            IsAutomated = false,
            DueDaysAfterPeriodEnd = 5,
            Instructions = "Retain the reviewed Finance reconciliation or exception reference."
        });

        var draft = await author.CreateFinanceCloseTemplateVersionAsync(request);
        draft.Version.Should().Be(2);
        draft.Status.Should().Be(FinanceCloseTemplateStatuses.Draft);

        var selfApproval = () => author.ApproveFinanceCloseTemplateAsync(draft.Id, new ApproveFinanceCloseTemplateDto
        {
            Declaration = "I independently approve this close template control design."
        });
        await selfApproval.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*author cannot approve*");

        var approver = CreateService(db, tenantId, approverId, "template.approver");
        var approved = await approver.ApproveFinanceCloseTemplateAsync(draft.Id, new ApproveFinanceCloseTemplateDto
        {
            Declaration = "I independently reviewed every task and approve this TDC close template."
        });
        approved.IsActive.Should().BeTrue();
        (await db.FinanceCloseTemplates.SingleAsync(item => item.Id == baseline.Id)).Status
            .Should().Be(FinanceCloseTemplateStatuses.Superseded);

        var workspace = await approver.EvaluatePeriodCloseWorkspaceAsync(period.Id);
        workspace.TemplateVersion.Should().Be(2);
        workspace.MandatoryBlockerCount.Should().Be(1);
        workspace.CanPrepare.Should().BeFalse();
        var manualTask = workspace.Tasks.Single(item => item.TaskCode == "MANUAL_CONTROL_REVIEW");

        var completed = await approver.UpdateFinanceCloseTaskAsync(period.Id, manualTask.Id, new UpdateFinanceCloseTaskDto
        {
            AssignToCurrentUser = true,
            MarkCompleted = true,
            EvidenceSummary = "Reviewed reconciliation FIN-REC-2026-07; no unresolved difference remains."
        });
        completed.MandatoryBlockerCount.Should().Be(0);
        completed.Tasks.Single(item => item.Id == manualTask.Id).Status
            .Should().Be(FinanceCloseTaskStatuses.Completed);

        (await approver.PreparePeriodCloseAsync(period.Id, new PeriodClosePreparationRequestDto
        {
            Declaration = "I reviewed automated checks and the retained manual task evidence."
        })).Status.Should().Be(FinanceCloseStatuses.Prepared);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task CloseTemplate_ShouldRejectDowngradeOfNonWaivableDepreciationControl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var baseline = (await service.GetFinanceCloseTemplatesAsync())
            .Single(item => item.CloseType == FinanceCloseTemplateTypes.MonthEnd && item.IsActive);
        var request = BuildTemplateVersionRequest(baseline);
        request.Tasks.Single(item => item.CheckCode == "FIXED_ASSET_DEPRECIATION").IsMandatory = false;

        var action = () => service.CreateFinanceCloseTemplateVersionAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-waivable mandatory control*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task CloseWaiver_ShouldRequireEvidenceAndMakerChecker_ThenExpireWhenExceptionChanges()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.Set<CashTransaction>().Add(BuildUnreconciledCashTransaction(tenantId, "CB-UNREC-001"));
        await db.SaveChangesAsync();

        var requester = CreateService(db, tenantId, requesterId, "senior.accountant");
        var initial = await requester.EvaluatePeriodCloseWorkspaceAsync(period.Id);
        var bankCheck = initial.Checks.Single(item => item.CheckCode == "BANK_RECONCILIATION");
        bankCheck.Status.Should().Be(FinanceCloseCheckStatuses.Failed);
        bankCheck.IsWaivable.Should().BeTrue();
        bankCheck.EvidenceFingerprint.Should().HaveLength(64);
        var bankTask = initial.Tasks.Single(item => item.CheckCode == bankCheck.CheckCode);

        var file = BuildCloseEvidenceFile(tenantId, requesterId, "July-bank-reconciliation.xlsx");
        db.FileUploadRecords.Add(file);
        await db.SaveChangesAsync();
        var withEvidence = await requester.LinkFinanceCloseEvidenceAsync(period.Id, bankTask.Id, new LinkFinanceCloseEvidenceDto
        {
            FileUploadRecordId = file.Id,
            EvidenceType = FinanceCloseEvidenceTypes.Reconciliation,
            Description = "July bank reconciliation and aged reconciling-items schedule."
        });
        var attachment = withEvidence.Tasks.Single(item => item.Id == bankTask.Id).EvidenceAttachments.Single();

        var requested = await requester.RequestFinanceCloseExceptionWaiverAsync(period.Id, bankCheck.Id, new RequestFinanceCloseWaiverDto
        {
            FinanceCloseEvidenceAttachmentId = attachment.Id,
            Justification = "The item is a confirmed bank timing difference; daily treasury review and the attached reconciliation mitigate the risk."
        });
        var waiver = requested.ExceptionWaivers.Single();
        waiver.Status.Should().Be(FinanceCloseWaiverStatuses.Requested);

        var selfReview = () => requester.ReviewFinanceCloseExceptionWaiverAsync(period.Id, waiver.Id, new ReviewFinanceCloseWaiverDto
        {
            Approve = true,
            Comment = "I independently approve this temporary timing difference."
        });
        await selfReview.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requester cannot review*");

        var reviewer = CreateService(db, tenantId, reviewerId, "finance.manager");
        var approved = await reviewer.ReviewFinanceCloseExceptionWaiverAsync(period.Id, waiver.Id, new ReviewFinanceCloseWaiverDto
        {
            Approve = true,
            Comment = "I verified the bank timing item and approve the documented mitigating control."
        });
        var waivedCheck = approved.Checks.Single(item => item.CheckCode == "BANK_RECONCILIATION");
        waivedCheck.Status.Should().Be(FinanceCloseCheckStatuses.Waived);
        waivedCheck.AppliedWaiverId.Should().Be(waiver.Id);
        approved.MandatoryBlockerCount.Should().Be(0);

        // Adding a second exception changes the provider evidence and therefore the SHA-256
        // fingerprint. The old approval remains auditable but cannot authorize the new state.
        db.Set<CashTransaction>().Add(BuildUnreconciledCashTransaction(tenantId, "CB-UNREC-002"));
        await db.SaveChangesAsync();
        var changed = await reviewer.EvaluatePeriodCloseWorkspaceAsync(period.Id);
        var changedBankCheck = changed.Checks.Single(item => item.CheckCode == "BANK_RECONCILIATION");
        changedBankCheck.Status.Should().Be(FinanceCloseCheckStatuses.Failed);
        changedBankCheck.AppliedWaiverId.Should().BeNull();
        changedBankCheck.EvidenceFingerprint.Should().NotBe(bankCheck.EvidenceFingerprint);
        changed.MandatoryBlockerCount.Should().Be(1);
        changed.ExceptionWaivers.Single().MatchesLatestEvidence.Should().BeFalse();

        (await db.AuditLogs.AnyAsync(item => item.Action == FinanceAuditEvents.AccountingPeriodCloseWaiverApproved))
            .Should().BeTrue();
        (await db.AuditLogs.AnyAsync(item => item.Action == FinanceAuditEvents.AccountingPeriodCloseWaiverApplied))
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task CloseWaiver_ShouldRejectNonWaivableControlAndCrossTenantEvidence()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var period = SeedPeriod(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, userId, "senior.accountant");
        var workspace = await service.EvaluatePeriodCloseWorkspaceAsync(period.Id);
        var trialCheck = workspace.Checks.Single(item => item.CheckCode == "TRIAL_BALANCE");
        var trialTask = workspace.Tasks.Single(item => item.CheckCode == trialCheck.CheckCode);
        var otherTenantFile = BuildCloseEvidenceFile(otherTenantId, Guid.NewGuid(), "foreign-tenant.pdf");
        db.FileUploadRecords.Add(otherTenantFile);
        await db.SaveChangesAsync();

        var crossTenantLink = () => service.LinkFinanceCloseEvidenceAsync(period.Id, trialTask.Id, new LinkFinanceCloseEvidenceDto
        {
            FileUploadRecordId = otherTenantFile.Id,
            EvidenceType = FinanceCloseEvidenceTypes.SupportingDocument
        });
        await crossTenantLink.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tenant file record was not found*");

        var ownFile = BuildCloseEvidenceFile(tenantId, userId, "trial-balance.pdf");
        db.FileUploadRecords.Add(ownFile);
        await db.SaveChangesAsync();
        var linked = await service.LinkFinanceCloseEvidenceAsync(period.Id, trialTask.Id, new LinkFinanceCloseEvidenceDto
        {
            FileUploadRecordId = ownFile.Id,
            EvidenceType = FinanceCloseEvidenceTypes.SupportingDocument
        });
        var attachment = linked.Tasks.Single(item => item.Id == trialTask.Id).EvidenceAttachments.Single();

        // Force a current failed snapshot to isolate the immutable catalogue rule: even with a
        // valid fingerprint and evidence, TRIAL_BALANCE is never eligible for a waiver.
        var persistedSnapshot = await db.FinanceCloseCheckSnapshots.SingleAsync(item => item.Id == trialCheck.Id);
        persistedSnapshot.Status = FinanceCloseCheckStatuses.Failed;
        persistedSnapshot.EvidenceFingerprint = new string('a', 64);
        await db.SaveChangesAsync();
        var nonWaivable = () => service.RequestFinanceCloseExceptionWaiverAsync(period.Id, trialCheck.Id, new RequestFinanceCloseWaiverDto
        {
            FinanceCloseEvidenceAttachmentId = attachment.Id,
            Justification = "This deliberately long test justification must still be rejected because the ledger control is absolute."
        });
        await nonWaivable.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-waivable ledger control*");
    }

    private static CashTransaction BuildUnreconciledCashTransaction(Guid tenantId, string transactionNumber)
    {
        return new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = transactionNumber,
            TransactionDate = new DateTime(2026, 7, 15),
            TransactionType = CashTransactionType.Payment,
            BankAccountId = Guid.NewGuid(),
            Amount = 250m,
            Currency = "GHS",
            BaseAmount = 250m,
            IsPosted = true,
            IsReconciled = false
        };
    }

    private static FileUploadRecord BuildCloseEvidenceFile(Guid tenantId, Guid userId, string fileName)
    {
        return new FileUploadRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = ControlledFileUploadCategories.FinanceCloseEvidence,
            FilePath = $"/uploads/finance-close-evidence/{fileName}",
            StoredFileName = fileName,
            OriginalFileName = fileName,
            ContentType = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "application/pdf",
            FileSize = 2048,
            StorageProvider = "Test",
            UploadedByUserId = userId,
            VirusScanStatus = FileVirusScanStatus.Clean,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test.user"
        };
    }

    private static SaveFinanceCloseTemplateVersionDto BuildTemplateVersionRequest(
        FinanceCloseTemplateDto template)
    {
        return new SaveFinanceCloseTemplateVersionDto
        {
            TemplateCode = template.TemplateCode,
            Name = template.Name,
            CloseType = template.CloseType,
            Description = template.Description,
            Tasks = template.Tasks.Select(task => new SaveFinanceCloseTemplateTaskDto
            {
                TaskCode = task.TaskCode,
                Title = task.Title,
                Category = task.Category,
                DependsOnTaskCode = task.DependsOnTaskCode,
                CheckCode = task.CheckCode,
                Sequence = task.Sequence,
                IsMandatory = task.IsMandatory,
                IsAutomated = task.IsAutomated,
                DueDaysAfterPeriodEnd = task.DueDaysAfterPeriodEnd,
                DefaultAssigneeUserId = task.DefaultAssigneeUserId,
                Instructions = task.Instructions
            }).ToList()
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"accounting-period-close-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FiscalPeriodService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid? userId = null,
        string userName = "finance.close")
    {
        var currentUser = CreateCurrentUser(tenantId, userId, userName);
        var auditService = new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor());
        var settlementReadModelService = new SubledgerSettlementReadModelService(
            db,
            currentUser.Object,
            Mock.Of<ILogger<SubledgerSettlementReadModelService>>(),
            auditService);
        return new FiscalPeriodService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<ILogger<FiscalPeriodService>>(),
            settlementReadModelService,
            auditService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(
        Guid tenantId,
        Guid? userId = null,
        string userName = "finance.close")
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns((userId ?? Guid.NewGuid()).ToString());
        currentUser.SetupGet(x => x.UserName).Returns(userName);
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("period-close-tests");
        return currentUser;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });

        // Period-close tests need configured Finance control accounts even when the period has no
        // AP/AR documents. A zero-activity reconciliation should pass at 0.00, not bypass the new
        // mandatory provider because the tenant fixture is incomplete.
        var apControl = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = $"21{code}0",
            AccountNumber = $"21{code}0",
            AccountName = $"{code} AP Control",
            AccountType = AccountType.Liability,
            AccountCategory = "Current Liabilities",
            IFRSLineItem = "Trade and other payables",
            BaseLineItem = "Trade and other payables",
            LocalLineItem = "Trade and other payables",
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        var arControl = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = $"11{code}0",
            AccountNumber = $"11{code}0",
            AccountName = $"{code} AR Control",
            AccountType = AccountType.Asset,
            AccountCategory = "Current Assets",
            IFRSLineItem = "Trade and other receivables",
            BaseLineItem = "Trade and other receivables",
            LocalLineItem = "Trade and other receivables",
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        db.Accounts.AddRange(apControl, arControl);
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apControl.Id,
            ControlAccountArId = arControl.Id
        });
    }

    private static FiscalPeriod SeedPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false,
        bool isLocked = false)
    {
        var fiscalYearId = Guid.NewGuid();
        db.FiscalYears.Add(new FiscalYear
        {
            Id = fiscalYearId,
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = ("FY26-" + fiscalYearId.ToString("N"))[..20],
            Year = 2026,
            FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            NumberOfPeriods = 12,
            Status = "Open",
            IsActive = true
        });

        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYearId,
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = isLocked ? "Locked" : isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = isLocked
        };

        db.FiscalPeriods.Add(period);
        return period;
    }

    private static FiscalPeriod CreateSiblingPeriod(
        FiscalPeriod anchor,
        int periodNumber,
        string periodName,
        DateTime startDate,
        DateTime endDate)
    {
        return new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = anchor.TenantId,
            FiscalYearId = anchor.FiscalYearId,
            PeriodName = periodName,
            PeriodCode = $"{startDate:yyyy-MM}",
            PeriodNumber = periodNumber,
            PeriodType = PeriodType.Monthly,
            StartDate = startDate,
            EndDate = endDate,
            PeriodDays = (endDate - startDate).Days + 1,
            Status = "Future",
            PeriodStatus = "Future",
            IsOpen = false,
            IsClosed = false,
            IsLocked = false
        };
    }

    private static FinanceCloseCycle SeedCertifiedClosedCycle(
        ApplicationDbContext db,
        Guid tenantId,
        FiscalPeriod period)
    {
        var preparerId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var cycle = new FinanceCloseCycle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            CycleNumber = 1,
            TemplateCode = "TDC-MONTH-END",
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            TemplateVersion = 1,
            Status = FinanceCloseStatuses.Closed,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            PreparedAt = DateTime.UtcNow.AddDays(-1),
            ClosedAt = DateTime.UtcNow.AddHours(-12)
        };
        db.FinanceCloseCycles.Add(cycle);
        db.FinanceCloseCertifications.Add(new FinanceCloseCertification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            PreparedByUserId = preparerId,
            PreparedByUserName = "finance.preparer",
            PreparedAt = cycle.PreparedAt,
            PreparerDeclaration = "I completed and certified the retained close evidence.",
            ReviewedByUserId = approverId,
            ReviewedByUserName = "finance.reviewer",
            ReviewedAt = cycle.ClosedAt,
            ReviewerDeclaration = "I independently reviewed and approved the retained close evidence.",
            ApprovedByUserId = approverId,
            ApprovedByUserName = "finance.reviewer",
            ApprovedAt = cycle.ClosedAt
        });
        return cycle;
    }
}
