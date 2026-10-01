using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookPeriodInitializationC4Tests
{
    [Fact]
    public void InitializationFingerprint_DoesNotTreatPrimaryDesignationAsFinancialEvidence()
    {
        var tenantId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var cutoff = new DateTime(2025, 12, 31);

        string Current(AccountingBookType bookType, AccountingBookType sourceType) =>
            AccountingBookInitializationFingerprint.Evidence(
                tenantId, bookId, "MANAGEMENT", bookType, "GHS",
                AccountingBookInitializationMode.BaseBookCopyAtCutoff, cutoff, periodId,
                "2025-12", new DateTime(2025, 12, 1), cutoff,
                sourceId, "IFRS", sourceType, "GHS", "INIT-1", "Opening authority", "lines");
        string Legacy(AccountingBookType bookType, AccountingBookType sourceType) =>
            AccountingBookInitializationFingerprint.LegacyEvidenceV2(
                tenantId, bookId, "MANAGEMENT", bookType, "GHS",
                AccountingBookInitializationMode.BaseBookCopyAtCutoff, cutoff, periodId,
                "2025-12", new DateTime(2025, 12, 1), cutoff,
                sourceId, "IFRS", sourceType, "GHS", "INIT-1", "Opening authority", "lines");

        Current(AccountingBookType.ParallelFull, AccountingBookType.PrimaryFull)
            .Should().Be(Current(AccountingBookType.PrimaryFull, AccountingBookType.ParallelFull));
        Legacy(AccountingBookType.ParallelFull, AccountingBookType.PrimaryFull)
            .Should().NotBe(Legacy(AccountingBookType.PrimaryFull, AccountingBookType.ParallelFull));
    }

    [Fact]
    public async Task ApprovedLegacyInitialization_RemainsValidAfterPrimaryDesignationChanges()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());
        await service.ConfigureAsync(state.Book.Id, Independent(state, "legacy-primary-replacement", 0m));
        await service.SubmitAsync(state.Book.Id);
        var initialization = db.AccountingBookInitializations.Include(item => item.Lines).Single();
        initialization.RowVersion = [11];
        await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto
            {
                Reason = "Approved before primary replacement",
                RowVersion = Convert.ToBase64String([11])
            });

        var lineEvidence = string.Join('|', initialization.Lines.OrderBy(item => item.AccountId)
            .Select(item => $"{item.AccountId:N}:{item.CurrencyCode}:{AccountingBookInitializationFingerprint.Decimal(item.OpeningDebit)}:{AccountingBookInitializationFingerprint.Decimal(item.OpeningCredit)}:{AccountingBookInitializationFingerprint.Decimal(item.BaseBookSignedBalance)}:{AccountingBookInitializationFingerprint.Decimal(item.OpeningAdjustment)}"));
        var legacyEvidence = AccountingBookInitializationFingerprint.LegacyEvidenceV2(
            state.TenantId, state.Book.Id, state.Book.Code, AccountingBookType.PrimaryFull,
            state.Book.FunctionalCurrencyCode, initialization.Mode, initialization.CutoffDate,
            state.CutoffPeriod.Id, state.CutoffPeriod.PeriodCode, state.CutoffPeriod.StartDate,
            state.CutoffPeriod.EndDate, null, null, null, null, initialization.IdempotencyKey,
            initialization.Reason, lineEvidence);
        var mappings = db.AccountAccountingBooks.Include(item => item.Account)
            .Include(item => item.AccountClassification).OrderBy(item => item.AccountId).ToList();
        var authority = string.Join('|', mappings.Select(item =>
            $"{item.AccountId:N}:{item.Account.AccountNumber}:{item.Account.AccountType}:{item.Id:N}:{item.AccountClassificationId:N}:{item.AccountClassification.Code}:{item.AccountClassification.Status}:{item.AccountClassification.IsPostingClassification}"));
        initialization.EvidenceFingerprint = legacyEvidence;
        initialization.ReconciliationFingerprint = AccountingBookInitializationFingerprint.Reconciliation(
            legacyEvidence, authority, string.Empty, string.Empty,
            initialization.TotalDebits, initialization.TotalCredits);

        state.Book.BookType = AccountingBookType.ParallelFull;
        await db.SaveChangesAsync();

        var validation = await service.ValidateCurrentApprovedEvidenceAsync(state.Book.Id);
        validation.IsValid.Should().BeTrue(validation.Blocker);
    }

    [Fact]
    public void Controllers_UseCanonicalReadManageAndCheckerPermissions_AndExposeNoDelete()
    {
        var periods = typeof(AccountingBookPeriodsController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Get))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ViewFinance);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Create))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBookPeriods);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.RequestTransition))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBookPeriods);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Approve))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookPeriods);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Reject))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookPeriods);
        periods.Should().NotContain(item => item.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        var initialization = typeof(AccountingBookInitializationController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (var read in new[] { nameof(AccountingBookInitializationController.Get), nameof(AccountingBookInitializationController.GetReadiness), nameof(AccountingBookInitializationController.Prepare) })
            initialization.Single(item => item.Name == read).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ViewFinance);
        foreach (var manage in new[] { nameof(AccountingBookInitializationController.PrepareDeltaStructure), nameof(AccountingBookInitializationController.Configure), nameof(AccountingBookInitializationController.Submit) })
            initialization.Single(item => item.Name == manage).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBookInitialization);
        foreach (var decide in new[] { nameof(AccountingBookInitializationController.Approve), nameof(AccountingBookInitializationController.Reject) })
            initialization.Single(item => item.Name == decide).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookInitialization);
        initialization.Should().NotContain(item => item.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Period_ReadDoesNotCreate_AndWrongTenantFails()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = PeriodService(db, state.TenantId, Guid.NewGuid());

        (await service.GetAsync(state.Book.Id)).Should().BeEmpty();
        await FluentActions.Awaiting(() => service.CreateAsync(state.Book.Id,
            new CreateAccountingBookPeriodDto { FiscalPeriodId = Guid.NewGuid() }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*current tenant*");
        db.AccountingBookPeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task Period_OpenRequiresOuterOpenPeriod_AndMakerChecker()
    {
        await using var db = Context();
        var state = Seed(db);
        state.Period.IsOpen = false;
        await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var service = PeriodService(db, state.TenantId, maker);
        var created = await service.CreateAsync(state.Book.Id, new CreateAccountingBookPeriodDto { FiscalPeriodId = state.Period.Id });
        var entity = db.AccountingBookPeriods.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => service.RequestTransitionAsync(state.Book.Id, created.Id, new RequestAccountingBookPeriodTransitionDto
        { TargetStatus = "Open", Reason = "Open first book period", RowVersion = Convert.ToBase64String([1]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant fiscal period must be open*");
    }

    [Fact]
    public async Task Period_TransitionRequiresExactRouteBook_AndDifferentChecker()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var created = await PeriodService(db, state.TenantId, maker).CreateAsync(state.Book.Id,
            new CreateAccountingBookPeriodDto { FiscalPeriodId = state.Period.Id });
        var entity = db.AccountingBookPeriods.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();
        var request = new RequestAccountingBookPeriodTransitionDto { TargetStatus = "Open", Reason = "Open", RowVersion = Convert.ToBase64String([1]) };

        await FluentActions.Awaiting(() => PeriodService(db, state.TenantId, maker).RequestTransitionAsync(Guid.NewGuid(), created.Id, request))
            .Should().ThrowAsync<KeyNotFoundException>();
        entity.PendingStatus.Should().BeNull();

        await PeriodService(db, state.TenantId, maker).RequestTransitionAsync(state.Book.Id, created.Id, request);
        entity.RowVersion = [2]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => PeriodService(db, state.TenantId, maker).ApproveAsync(state.Book.Id, created.Id,
            new DecideAccountingBookPeriodTransitionDto { Reason = "self", RowVersion = Convert.ToBase64String([2]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*checker must differ*");
        entity.RowVersion = [3]; await db.SaveChangesAsync();
        var approved = await PeriodService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id, created.Id,
            new DecideAccountingBookPeriodTransitionDto { Reason = "approved", RowVersion = Convert.ToBase64String([3]) });
        approved.Status.Should().Be("Open");
        approved.PendingStatus.Should().BeNull();
    }

    [Fact]
    public async Task Period_StaleRowVersionRejectsWithoutPendingMutation()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var created = await PeriodService(db, state.TenantId, Guid.NewGuid()).CreateAsync(state.Book.Id,
            new CreateAccountingBookPeriodDto { FiscalPeriodId = state.Period.Id });
        var entity = db.AccountingBookPeriods.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => PeriodService(db, state.TenantId, Guid.NewGuid()).RequestTransitionAsync(state.Book.Id, created.Id,
            new RequestAccountingBookPeriodTransitionDto { TargetStatus = "Open", Reason = "stale", RowVersion = Convert.ToBase64String([9]) }))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        entity.PendingStatus.Should().BeNull();
        entity.WorkflowInstanceId.Should().BeNull();
    }

    [Fact]
    public async Task Initialization_IsBalancedCompleteAndIdempotent_ButChangedEvidenceConflicts()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());
        var request = Independent(state, "init-1", 0m);

        var first = await service.ConfigureAsync(state.Book.Id, request);
        var retry = await service.ConfigureAsync(state.Book.Id, request);
        retry.Id.Should().Be(first.Id);
        retry.IsBalanced.Should().BeTrue();
        retry.IsCoverageComplete.Should().BeTrue();

        var changed = Independent(state, "init-1", 0m);
        changed.Reason = "Conflicting governed reason";
        await FluentActions.Awaiting(() => service.ConfigureAsync(state.Book.Id, changed))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("INITIALIZATION_IDEMPOTENCY_CONFLICT:*");
    }

    [Fact]
    public async Task Initialization_StaleConfigureAndDecisionRejectWithoutMutation()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var maker = Guid.NewGuid(); var service = InitializationService(db, state.TenantId, maker);
        await service.ConfigureAsync(state.Book.Id, Independent(state, "stale-original", 0m));
        var entity = db.AccountingBookInitializations.Single(); entity.RowVersion = [2]; await db.SaveChangesAsync();
        var replacement = Independent(state, "stale-replacement", 0m); replacement.RowVersion = Convert.ToBase64String([9]);

        await FluentActions.Awaiting(() => service.ConfigureAsync(state.Book.Id, replacement))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        entity.IdempotencyKey.Should().Be("stale-original");
        entity.InitializationStatus.Should().Be(AccountingBookInitializationStatus.Draft);

        await service.SubmitAsync(state.Book.Id);
        entity.RowVersion = [3]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "stale checker", RowVersion = Convert.ToBase64String([8]) }))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        entity.InitializationStatus.Should().Be(AccountingBookInitializationStatus.PendingApproval);
        entity.ApprovedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Initialization_RejectsSubmittedAmountsThatDoNotReconcileToPostedBookEvidence()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());

        await FluentActions.Awaiting(() => service.ConfigureAsync(state.Book.Id, Independent(state, "init-2", 5m)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*posted exact-book balances*");
    }

    [Theory]
    [InlineData("BaseBookCopyAtCutoff")]
    [InlineData("BaseBalancesWithOpeningAdjustments")]
    public async Task Initialization_BaseModesUseExactActiveSourceBookEvidence(string mode)
    {
        await using var db = Context();
        var state = Seed(db);
        var source = new AccountingBook { TenantId = state.TenantId, Code = "SOURCE", Name = "Source", Purpose = "Reporting",
            BookType = AccountingBookType.ParallelFull, LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS", IsActive = true, AllowsPosting = true };
        db.AccountingBooks.Add(source);
        var cutoff = db.FiscalPeriods.Local.Single(item => item.PeriodCode == "2025-12");
        foreach (var account in new[] { state.Debit, state.Credit })
            db.AccountBalances.Add(new AccountBalance { TenantId = state.TenantId, AccountId = account.Id, AccountingBookId = source.Id,
                FiscalPeriodId = cutoff.Id, BookClassification = source.Code, Currency = "GHS", ClosingBalance = 0m });
        await db.SaveChangesAsync();
        var request = Independent(state, $"base-{mode}", 0m); request.Mode = mode; request.SourceAccountingBookId = source.Id;
        request.SourceAccountingBookCode = source.Code;

        var result = await InitializationService(db, state.TenantId, Guid.NewGuid()).ConfigureAsync(state.Book.Id, request);
        result.SourceAccountingBookId.Should().Be(source.Id);
        result.IsBalanced.Should().BeTrue();
        result.Lines.Should().Contain(line => line.AccountNumber == "1000" && line.AccountName == "1000" && line.AccountType == "Asset");
    }

    [Fact]
    public async Task Initialization_PureBaseCopyRejectsOpeningAdjustment()
    {
        await using var db = Context(); var state = Seed(db);
        var source = new AccountingBook { TenantId = state.TenantId, Code = "SOURCE", Name = "Source", Purpose = "Reporting",
            BookType = AccountingBookType.ParallelFull, LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS", IsActive = true, AllowsPosting = true };
        db.AccountingBooks.Add(source); await db.SaveChangesAsync();
        var request = Independent(state, "copy-adjustment", 0m); request.Mode = "BaseBookCopyAtCutoff";
        request.SourceAccountingBookId = source.Id; request.SourceAccountingBookCode = source.Code;
        request.Lines.First().OpeningAdjustment = 1m;

        await FluentActions.Awaiting(() => InitializationService(db, state.TenantId, Guid.NewGuid()).ConfigureAsync(state.Book.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot contain opening adjustments*");
    }

    [Fact]
    public async Task Initialization_LastDraftEditorBecomesMakerAndPriorDecisionEvidenceIsCleared()
    {
        await using var db = Context(); var state = Seed(db); await db.SaveChangesAsync();
        var firstMaker = Guid.NewGuid();
        await InitializationService(db, state.TenantId, firstMaker).ConfigureAsync(state.Book.Id, Independent(state, "draft-v1", 0m));
        var entity = db.AccountingBookInitializations.Single();
        entity.DecidedByUserId = Guid.NewGuid(); entity.DecidedAtUtc = DateTime.UtcNow; entity.DecisionReason = "obsolete";
        entity.RowVersion = [7]; await db.SaveChangesAsync();
        var secondMaker = Guid.NewGuid(); var revised = Independent(state, "draft-v2", 0m); revised.RowVersion = Convert.ToBase64String([7]);

        var result = await InitializationService(db, state.TenantId, secondMaker).ConfigureAsync(state.Book.Id, revised);
        result.PreparedByUserId.Should().Be(secondMaker);
        result.DecidedByUserId.Should().BeNull(); result.DecisionReason.Should().BeNull();
    }

    [Fact]
    public async Task Initialization_RejectionPreservesEvidenceAndCreatesVersionedSuccessor()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var maker = Guid.NewGuid(); var service = InitializationService(db, state.TenantId, maker);
        await service.ConfigureAsync(state.Book.Id, Independent(state, "rejected-v1", 0m));
        await service.SubmitAsync(state.Book.Id);
        var first = db.AccountingBookInitializations.Single(); first.RowVersion = [4]; await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).RejectAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Evidence incomplete", RowVersion = Convert.ToBase64String([4]) });

        var second = await service.ConfigureAsync(state.Book.Id, Independent(state, "replacement-v2", 0m));
        second.Version.Should().Be(2);
        second.SupersedesInitializationId.Should().Be(first.Id);
        db.AccountingBookInitializations.Single(item => item.Id == first.Id).InitializationStatus.Should().Be(AccountingBookInitializationStatus.Rejected);
        first.ApprovedByUserId.Should().BeNull(); first.ApprovedAtUtc.Should().BeNull();
        first.RejectedByUserId.Should().NotBeNull(); first.DecidedByUserId.Should().Be(first.RejectedByUserId);
    }

    [Fact]
    public async Task DeltaStructure_IsPreparedFromBase_AndInitializationRemainsZeroOnly()
    {
        await using var db = Context();
        var state = Seed(db);
        var delta = new AccountingBook
        {
            TenantId = state.TenantId, Code = "IFRS_CONSOL_ADJ", Name = "IFRS consolidation adjustments",
            Purpose = "Consolidation adjustments", BookType = AccountingBookType.Delta,
            LifecycleStatus = AccountingBookLifecycleStatus.Initializing,
            BaseAccountingBookId = state.Book.Id, IsActive = false, AllowsPosting = false
        };
        db.AccountingBooks.Add(delta);
        await db.SaveChangesAsync();
        var initialization = InitializationService(db, state.TenantId, Guid.NewGuid());

        await FluentActions.Awaiting(() => initialization.PrepareAsync(delta.Id, "IndependentOpeningBalances",
                new DateTime(2025, 12, 31), null))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*DELTA_ACCOUNT_MAPPINGS_REQUIRED*");

        var preparedStructure = await initialization.EnsureDeltaStructureAsync(delta.Id);
        preparedStructure.AccountMappingCount.Should().Be(2);
        preparedStructure.ClassificationCount.Should().Be(2);
        db.AccountAccountingBooks.Count(item => item.AccountingBookId == delta.Id && item.IsEnabled).Should().Be(2);

        var preparation = await initialization.PrepareAsync(delta.Id, "IndependentOpeningBalances",
            new DateTime(2025, 12, 31), null);
        preparation.Accounts.Should().HaveCount(2);
        preparation.Accounts.Should().OnlyContain(item => item.AuthoritativeSignedBalance == 0m);

        var baseAssetClassification = await db.AccountClassifications.SingleAsync(item =>
            item.AccountingBookId == state.Book.Id && item.Code == "ASSET");
        baseAssetClassification.Name = "Assets governed by IFRS";
        baseAssetClassification.DisplayOrder = 25;
        var baseDebitMapping = await db.AccountAccountingBooks.SingleAsync(item =>
            item.AccountingBookId == state.Book.Id && item.AccountId == state.Debit.Id);
        baseDebitMapping.IsEnabled = false;
        await db.SaveChangesAsync();

        var refreshedStructure = await initialization.EnsureDeltaStructureAsync(delta.Id);
        refreshedStructure.AccountMappingCount.Should().Be(1);
        var deltaAssetClassification = await db.AccountClassifications.SingleAsync(item =>
            item.AccountingBookId == delta.Id && item.Code == "ASSET");
        deltaAssetClassification.Name.Should().Be("Assets governed by IFRS");
        deltaAssetClassification.DisplayOrder.Should().Be(25);
        (await db.AccountAccountingBooks.SingleAsync(item =>
            item.AccountingBookId == delta.Id && item.AccountId == state.Debit.Id)).IsEnabled.Should().BeFalse();

        baseDebitMapping.IsEnabled = true;
        await db.SaveChangesAsync();
        await initialization.EnsureDeltaStructureAsync(delta.Id);
        (await db.AccountAccountingBooks.SingleAsync(item =>
            item.AccountingBookId == delta.Id && item.AccountId == state.Debit.Id)).IsEnabled.Should().BeTrue();

        await FluentActions.Awaiting(() => initialization.PrepareAsync(delta.Id, "BaseBookCopyAtCutoff",
                new DateTime(2025, 12, 31), state.Book.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*DELTA_INITIALIZATION_MODE_INVALID*");

        state.Book.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        state.Book.IsActive = true; state.Book.AllowsPosting = true;
        delta.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        delta.IsActive = true; delta.AllowsPosting = true;
        await db.SaveChangesAsync();
        var report = await new AccountingBookService(db, User(state.TenantId, Guid.NewGuid()).Object)
            .GetDeltaCombinedReportAsync(delta.Id, new DateTime(2026, 1, 31));
        report.BaseAccountingBookCode.Should().Be("IFRS");
        report.DeltaAccountingBookCode.Should().Be("IFRS_CONSOL_ADJ");
        report.Lines.Should().HaveCount(2);
        report.Lines.Should().OnlyContain(item => item.BaseSignedBalance == 0m
            && item.DeltaSignedBalance == 0m && item.CombinedSignedBalance == 0m);
    }

    [Fact]
    public async Task DeltaCombinedReport_AggregatesPostedHistoryAndRetainsDisabledMappedAccounts()
    {
        await using var db = Context();
        var state = Seed(db);
        var delta = new AccountingBook
        {
            TenantId = state.TenantId, Code = "IFRS_CONSOL_ADJ", Name = "IFRS consolidation adjustments",
            Purpose = "Consolidation", BookType = AccountingBookType.Delta,
            BaseAccountingBookId = state.Book.Id, FunctionalCurrencyCode = "GHS",
            LifecycleStatus = AccountingBookLifecycleStatus.Active, IsActive = true, AllowsPosting = true
        };
        db.AccountingBooks.Add(delta);
        var deltaAsset = Classification(state.TenantId, delta.Id, "ASSET", AccountType.Asset);
        var deltaEquity = Classification(state.TenantId, delta.Id, "EQUITY", AccountType.Equity);
        db.AccountClassifications.AddRange(deltaAsset, deltaEquity);
        var deltaDebitMapping = Mapping(state.TenantId, delta.Id, state.Debit.Id, deltaAsset.Id);
        db.AccountAccountingBooks.AddRange(deltaDebitMapping,
            Mapping(state.TenantId, delta.Id, state.Credit.Id, deltaEquity.Id));
        var journalId = Guid.NewGuid();
        db.AccountTransactions.AddRange(
            Transaction(state, state.Book.Id, journalId, state.Debit.Id, new DateTime(2026, 1, 10), 1_000m, 0m, "Posted"),
            Transaction(state, state.Book.Id, journalId, state.Credit.Id, new DateTime(2026, 1, 10), 0m, 1_000m, "Posted"),
            Transaction(state, delta.Id, journalId, state.Debit.Id, new DateTime(2026, 1, 20), 125m, 0m, "Posted"),
            Transaction(state, delta.Id, journalId, state.Credit.Id, new DateTime(2026, 1, 20), 0m, 125m, "Posted"),
            Transaction(state, delta.Id, journalId, state.Debit.Id, new DateTime(2026, 1, 21), 900m, 0m, "Draft"),
            Transaction(state, delta.Id, journalId, state.Debit.Id, new DateTime(2026, 2, 1), 700m, 0m, "Posted"));
        await db.SaveChangesAsync();

        // Historical posted balances must remain reportable if a mapping is later disabled.
        deltaDebitMapping.IsEnabled = false;
        await db.SaveChangesAsync();

        var report = await new AccountingBookService(db, User(state.TenantId, Guid.NewGuid()).Object)
            .GetDeltaCombinedReportAsync(delta.Id, new DateTime(2026, 1, 31));

        report.Lines.Should().HaveCount(2);
        report.Lines.Single(item => item.AccountId == state.Debit.Id).Should().BeEquivalentTo(new
        {
            BaseSignedBalance = 1_000m,
            DeltaSignedBalance = 125m,
            CombinedSignedBalance = 1_125m
        });
        report.Lines.Single(item => item.AccountId == state.Credit.Id).Should().BeEquivalentTo(new
        {
            BaseSignedBalance = -1_000m,
            DeltaSignedBalance = -125m,
            CombinedSignedBalance = -1_125m
        });
        report.BaseTotal.Should().Be(0m);
        report.DeltaTotal.Should().Be(0m);
        report.CombinedTotal.Should().Be(0m);
    }

    [Fact]
    public async Task Initialization_DecisionDtoResolvesTenantUserDisplayNames()
    {
        await using var db = Context();
        var state = Seed(db);
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        db.Users.AddRange(
            new ApplicationUser { Id = maker, TenantId = state.TenantId, UserName = "opening.maker", NormalizedUserName = "OPENING.MAKER", FirstName = "Kwame", LastName = "Mensah" },
            new ApplicationUser { Id = checker, TenantId = state.TenantId, UserName = "financial.controller", NormalizedUserName = "FINANCIAL.CONTROLLER", FirstName = "Abena", LastName = "Dapaah" });
        await db.SaveChangesAsync();

        var makerService = InitializationService(db, state.TenantId, maker);
        await makerService.ConfigureAsync(state.Book.Id, Independent(state, "named-decision", 0m));
        await makerService.SubmitAsync(state.Book.Id);
        var entity = db.AccountingBookInitializations.Single();
        entity.RowVersion = [8];
        await db.SaveChangesAsync();

        var checkerService = InitializationService(db, state.TenantId, checker);
        var approved = await checkerService.ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Evidence independently reviewed", RowVersion = Convert.ToBase64String([8]) });

        approved.DecidedByName.Should().Be("Abena Dapaah");
        approved.ApprovedByName.Should().Be("Abena Dapaah");
        (await checkerService.GetAsync(state.Book.Id))!.DecidedByName.Should().Be("Abena Dapaah");
    }

    [Fact]
    public async Task ActivationReadiness_RequiresApprovedInitializationAndExactFirstPostingPeriod()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var service = InitializationService(db, state.TenantId, maker);
        var draft = await service.ConfigureAsync(state.Book.Id, Independent(state, "init-3", 0m));
        (await service.GetReadinessAsync(state.Book.Id)).IsReady.Should().BeFalse();

        await service.SubmitAsync(state.Book.Id);
        var entity = db.AccountingBookInitializations.Single(); entity.RowVersion = [3]; await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Independent review complete", RowVersion = Convert.ToBase64String([3]) });
        db.AccountingBookPeriods.Add(new AccountingBookPeriod { TenantId = state.TenantId, AccountingBookId = state.Book.Id,
            FiscalPeriodId = state.Period.Id, PeriodStatus = AccountingBookPeriodStatus.Open });
        await db.SaveChangesAsync();

        var readiness = await service.GetReadinessAsync(state.Book.Id);
        readiness.IsReady.Should().BeTrue();
        readiness.RequiredPeriodCount.Should().Be(1);
        readiness.ReadyPeriodCount.Should().Be(1);
    }

    [Fact]
    public async Task ApprovedInitialization_IgnoresNewMappingsEffectiveAfterCutoff_ButRejectsRetroactiveMappings()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var service = InitializationService(db, state.TenantId, maker);
        await service.ConfigureAsync(state.Book.Id, Independent(state, "future-account-mapping", 0m));
        await service.SubmitAsync(state.Book.Id);
        var evidence = db.AccountingBookInitializations.Single();
        evidence.RowVersion = [9];
        await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Approved cutoff evidence", RowVersion = Convert.ToBase64String([9]) });

        var newAccount = Account(state.TenantId, "1050", AccountType.Asset);
        newAccount.EffectiveDate = new DateTime(2026, 1, 1);
        db.Accounts.Add(newAccount);
        var assetClassification = db.AccountClassifications.Single(item => item.AccountingBookId == state.Book.Id
            && item.CoreAccountType == AccountType.Asset);
        db.AccountAccountingBooks.Add(Mapping(state.TenantId, state.Book.Id, newAccount.Id, assetClassification.Id));
        await db.SaveChangesAsync();

        (await service.ValidateCurrentApprovedEvidenceAsync(state.Book.Id)).IsValid.Should().BeTrue();

        newAccount.EffectiveDate = new DateTime(2025, 12, 31);
        await db.SaveChangesAsync();
        var retroactive = await service.ValidateCurrentApprovedEvidenceAsync(state.Book.Id);
        retroactive.IsValid.Should().BeFalse();
        retroactive.Blocker.Should().Contain("exactly one opening line");
    }

    [Fact]
    public async Task ApprovedInitialization_PreservesZeroHistoricalLine_WhenAccountBecomesFutureEffective()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var service = InitializationService(db, state.TenantId, maker);
        await service.ConfigureAsync(state.Book.Id, Independent(state, "future-dated-existing-line", 0m));
        await service.SubmitAsync(state.Book.Id);
        var evidence = db.AccountingBookInitializations.Single();
        evidence.RowVersion = [10];
        await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Approved zero opening evidence", RowVersion = Convert.ToBase64String([10]) });

        state.Debit.EffectiveDate = new DateTime(2026, 1, 1);
        await db.SaveChangesAsync();

        var retained = await service.ValidateCurrentApprovedEvidenceAsync(state.Book.Id);
        retained.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ManifestPreparedMappings_InitializeAndBecomeExecutableOnlyAfterApprovedActivation()
    {
        await using var db = Context();
        var state = Seed(db);
        foreach (var mapping in db.AccountAccountingBooks.Local)
        {
            mapping.IsEnabled = false;
            mapping.CreatedBy = $"System ({FinanceClassificationManifestSeeder.ManifestVersion})";
        }
        await db.SaveChangesAsync();

        var maker = Guid.NewGuid();
        var initialization = InitializationService(db, state.TenantId, maker);
        var preparation = await initialization.PrepareAsync(state.Book.Id, "IndependentOpeningBalances", new DateTime(2025, 12, 31), null);
        preparation.Accounts.Should().HaveCount(2);
        await initialization.ConfigureAsync(state.Book.Id, Independent(state, "manifest-prepared", 0m));
        await initialization.SubmitAsync(state.Book.Id);
        var evidence = db.AccountingBookInitializations.Single();
        evidence.RowVersion = [7];
        await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Approved opening authority", RowVersion = Convert.ToBase64String([7]) });
        db.AccountingBookPeriods.Add(new AccountingBookPeriod { TenantId = state.TenantId, AccountingBookId = state.Book.Id,
            FiscalPeriodId = state.Period.Id, PeriodStatus = AccountingBookPeriodStatus.Open });
        state.Book.RowVersion = [8];
        await db.SaveChangesAsync();
        db.AccountAccountingBooks.Should().OnlyContain(mapping => !mapping.IsEnabled);

        var lifecycle = new AccountingBookService(db, User(state.TenantId, maker).Object, Workflow().Object, Audit().Object);
        await lifecycle.RequestTransitionAsync(state.Book.Id, new RequestAccountingBookTransitionDto
            { TargetStatus = "Active", Reason = "Opening and period approved", RowVersion = Convert.ToBase64String([8]) });
        db.AccountAccountingBooks.Should().OnlyContain(mapping => !mapping.IsEnabled);
        await new AccountingBookService(db, User(state.TenantId, Guid.NewGuid()).Object, Workflow().Object, Audit().Object)
            .ApproveTransitionAsync(state.Book.Id, new DecideAccountingBookTransitionDto
                { Reason = "Independent activation approval", RowVersion = Convert.ToBase64String([8]) });

        state.Book.LifecycleStatus.Should().Be(AccountingBookLifecycleStatus.Active);
        db.AccountAccountingBooks.Should().OnlyContain(mapping => mapping.IsEnabled);
        (await initialization.ValidateCurrentApprovedEvidenceAsync(state.Book.Id)).IsValid.Should().BeTrue();

        await lifecycle.RequestTransitionAsync(state.Book.Id, new RequestAccountingBookTransitionDto
            { TargetStatus = "Suspended", Reason = "Pause test posting", RowVersion = Convert.ToBase64String([8]) });
        await new AccountingBookService(db, User(state.TenantId, Guid.NewGuid()).Object, Workflow().Object, Audit().Object)
            .ApproveTransitionAsync(state.Book.Id, new DecideAccountingBookTransitionDto
                { Reason = "Independent suspension approval", RowVersion = Convert.ToBase64String([8]) });
        db.AccountAccountingBooks.Should().OnlyContain(mapping => !mapping.IsEnabled);

        (await initialization.GetReadinessAsync(state.Book.Id)).IsReady.Should().BeTrue();
        await lifecycle.RequestTransitionAsync(state.Book.Id, new RequestAccountingBookTransitionDto
            { TargetStatus = "Active", Reason = "Resume test posting", RowVersion = Convert.ToBase64String([8]) });
        await new AccountingBookService(db, User(state.TenantId, Guid.NewGuid()).Object, Workflow().Object, Audit().Object)
            .ApproveTransitionAsync(state.Book.Id, new DecideAccountingBookTransitionDto
                { Reason = "Independent resumption approval", RowVersion = Convert.ToBase64String([8]) });
        db.AccountAccountingBooks.Should().OnlyContain(mapping => mapping.IsEnabled);
        (await initialization.ValidateCurrentApprovedEvidenceAsync(state.Book.Id)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ActivationReadiness_ReDerivesLatestEvidenceAndRejectsPendingBookClose()
    {
        await using var db = Context(); var state = Seed(db); await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());
        await service.ConfigureAsync(state.Book.Id, Independent(state, "readiness-fresh", 0m)); await service.SubmitAsync(state.Book.Id);
        var initialization = db.AccountingBookInitializations.Single(); initialization.RowVersion = [5]; await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "approved", RowVersion = Convert.ToBase64String([5]) });
        var bookPeriod = new AccountingBookPeriod { TenantId = state.TenantId, AccountingBookId = state.Book.Id,
            FiscalPeriodId = state.Period.Id, PeriodStatus = AccountingBookPeriodStatus.Open, PendingStatus = AccountingBookPeriodStatus.Closed };
        db.AccountingBookPeriods.Add(bookPeriod); await db.SaveChangesAsync();

        (await service.GetReadinessAsync(state.Book.Id)).IsReady.Should().BeFalse();
        bookPeriod.PendingStatus = null;
        db.AccountAccountingBooks.First().IsEnabled = false; await db.SaveChangesAsync();
        var stale = await service.GetReadinessAsync(state.Book.Id);
        stale.IsReady.Should().BeFalse(); stale.Blockers.Should().Contain(item => item.Contains("mapped", StringComparison.OrdinalIgnoreCase));
    }


    private static ConfigureAccountingBookInitializationDto Independent(State state, string key, decimal firstDebit) => new()
    {
        Mode = "IndependentOpeningBalances", CutoffDate = new DateTime(2025, 12, 31),
        CutoffFiscalPeriodId = state.CutoffPeriod.Id, CutoffFiscalPeriodCode = state.CutoffPeriod.PeriodCode,
        IdempotencyKey = key, Reason = "Governed test opening",
        Lines = new[]
        {
            new AccountingBookInitializationLineDto { AccountId = state.Debit.Id, CurrencyCode = "GHS", OpeningDebit = firstDebit },
            new AccountingBookInitializationLineDto { AccountId = state.Credit.Id, CurrencyCode = "GHS", OpeningCredit = firstDebit }
        }
    };

    [Theory]
    [InlineData(AccountingBookLifecycleStatus.Configuring)]
    [InlineData(AccountingBookLifecycleStatus.Initializing)]
    public async Task ParallelStructure_CopyPreparesMappingsWithoutActivatingOrPosting(AccountingBookLifecycleStatus status)
    {
        await using var db = Context();
        var state = Seed(db);
        db.AccountClassifications.Add(Classification(state.TenantId, state.Book.Id, "OTHER_EXPENSE", AccountType.Expense));
        var equityLeaf = db.AccountClassifications.Local.Single(item => item.Code == "EQUITY");
        equityLeaf.Code = "RETAINED_EARNINGS";
        var equityRoot = Classification(state.TenantId, state.Book.Id, "EQUITY", AccountType.Equity);
        equityRoot.IsPostingClassification = false;
        equityLeaf.ParentClassificationId = equityRoot.Id;
        db.AccountClassifications.Add(equityRoot);
        var parallel = new AccountingBook { TenantId = state.TenantId, Code = "USD_CUSTOM", Name = "Custom USD",
            Purpose = "Parallel reporting", BookType = AccountingBookType.ParallelFull, LifecycleStatus = status,
            BaseAccountingBookId = state.Book.Id, FunctionalCurrencyCode = "USD", IsActive = false, AllowsPosting = false,
            ParallelOpeningMode = ParallelBookOpeningMode.ZeroOpening, ReplicationStartDate = new DateTime(2026, 1, 1) };
        db.AccountingBooks.Add(parallel);
        await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());
        await service.EnsureDeltaStructureAsync(parallel.Id);
        await service.EnsureDeltaStructureAsync(parallel.Id);
        db.AccountAccountingBooks.Count(item => item.AccountingBookId == parallel.Id).Should().Be(4);
        db.AccountClassifications.Count(item => item.AccountingBookId == parallel.Id).Should().Be(4);
        parallel.LifecycleStatus.Should().Be(status);
        parallel.IsActive.Should().BeFalse();
        parallel.AllowsPosting.Should().BeFalse();
        db.JournalEntries.Should().BeEmpty();
        db.AccountTransactions.Should().BeEmpty();
        var preparation = await service.PrepareAsync(parallel.Id, "IndependentOpeningBalances", new DateTime(2025, 12, 31), null);
        preparation.Accounts.Should().HaveCount(4);
        preparation.Accounts.Should().OnlyContain(item => item.AuthoritativeSignedBalance == 0);
    }

    [Theory]
    [InlineData(AccountingBookInitializationStatus.PendingApproval)]
    [InlineData(AccountingBookInitializationStatus.Approved)]
    public async Task StructureCopy_RejectsFrozenInitialization(AccountingBookInitializationStatus status)
    {
        await using var db = Context();
        var state = Seed(db);
        var derived = new AccountingBook { TenantId = state.TenantId, Code = "DELTA_TEST", Name = "Delta", Purpose = "Test",
            BookType = AccountingBookType.Delta, LifecycleStatus = AccountingBookLifecycleStatus.Initializing,
            BaseAccountingBookId = state.Book.Id, IsActive = false, AllowsPosting = false };
        db.AccountingBooks.Add(derived);
        db.AccountingBookInitializations.Add(new AccountingBookInitialization { TenantId = state.TenantId,
            AccountingBookId = derived.Id, InitializationStatus = status, IdempotencyKey = "frozen" });
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => InitializationService(db, state.TenantId, Guid.NewGuid()).EnsureDeltaStructureAsync(derived.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*pending approval or approved*");
        db.AccountAccountingBooks.Count(item => item.AccountingBookId == derived.Id).Should().Be(0);
    }

    private static State Seed(ApplicationDbContext db)
    {
        var tenant = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenant, Code = "C4", Name = "C4", Status = TenantStatus.Active, BaseCurrency = "GHS" });
        db.FinanceSettings.Add(new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", CoaType = "Segmented" });
        var book = new AccountingBook { TenantId = tenant, Code = "IFRS", Name = "IFRS", Purpose = "Reporting",
            BookType = AccountingBookType.PrimaryFull, LifecycleStatus = AccountingBookLifecycleStatus.Initializing,
            FunctionalCurrencyCode = "GHS", IsDefault = true, IsActive = false, AllowsPosting = false };
        db.AccountingBooks.Add(book);
        var year2026 = FiscalYear(tenant, 2026); var year2025 = FiscalYear(tenant, 2025); db.FiscalYears.AddRange(year2025, year2026);
        var period = new FiscalPeriod { TenantId = tenant, FiscalYearId = year2026.Id, PeriodName = "January 2026", PeriodCode = "2026-01",
            PeriodNumber = 1, PeriodType = PeriodType.Monthly, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31),
            PeriodDays = 31, PeriodStatus = "Open", IsOpen = true };
        db.FiscalPeriods.Add(period);
        var cutoffPeriod = new FiscalPeriod { TenantId = tenant, FiscalYearId = year2025.Id, PeriodName = "December 2025", PeriodCode = "2025-12",
            PeriodNumber = 12, PeriodType = PeriodType.Monthly, StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsOpen = false, IsClosed = true };
        db.FiscalPeriods.Add(cutoffPeriod);
        var debit = Account(tenant, "1000", AccountType.Asset); var credit = Account(tenant, "3000", AccountType.Equity);
        db.Accounts.AddRange(debit, credit);
        var assetClass = Classification(tenant, book.Id, "ASSET", AccountType.Asset); var equityClass = Classification(tenant, book.Id, "EQUITY", AccountType.Equity);
        db.AccountClassifications.AddRange(assetClass, equityClass);
        db.AccountAccountingBooks.AddRange(Mapping(tenant, book.Id, debit.Id, assetClass.Id), Mapping(tenant, book.Id, credit.Id, equityClass.Id));
        return new State(tenant, book, period, cutoffPeriod, debit, credit);
    }
    private static Account Account(Guid tenant, string code, AccountType type) => new() { TenantId = tenant, AccountCode = code, AccountNumber = code,
        AccountName = code, AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active };
    private static FiscalYear FiscalYear(Guid tenant, int year) => new() { TenantId = tenant, FiscalYearName = $"FY {year}", FiscalYearCode = year.ToString(),
        Year = year, FiscalYearType = "Calendar", StartDate = new DateTime(year, 1, 1), EndDate = new DateTime(year, 12, 31),
        TotalDays = DateTime.IsLeapYear(year) ? 366 : 365, NumberOfPeriods = 12, Status = "Open", IsActive = true };
    private static AccountClassification Classification(Guid tenant, Guid book, string code, AccountType type) => new() { TenantId = tenant,
        AccountingBookId = book, Code = code, Name = code, CoreAccountType = type, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
    private static AccountAccountingBook Mapping(Guid tenant, Guid book, Guid account, Guid classification) => new() { TenantId = tenant,
        AccountingBookId = book, AccountId = account, AccountClassificationId = classification, IsEnabled = true };
    private static AccountTransaction Transaction(State state, Guid bookId, Guid journalId, Guid accountId,
        DateTime date, decimal debit, decimal credit, string status) => new()
    {
        TenantId = state.TenantId, AccountingBookId = bookId, BookClassification = "IFRS",
        JournalEntryId = journalId, AccountId = accountId, FiscalPeriodId = state.Period.Id,
        TransactionDate = date, DebitAmount = debit, CreditAmount = credit,
        FunctionalCurrencyCode = "GHS", PostingStatus = status
    };
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"c4-{Guid.NewGuid():N}").Options);
    private static IAccountingBookPeriodService PeriodService(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? audit = null) =>
        new AccountingBookPeriodService(db, User(tenant, actor).Object, Workflow().Object, audit ?? Audit().Object);
    private static IAccountingBookInitializationService InitializationService(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? audit = null) =>
        new AccountingBookInitializationService(db, User(tenant, actor).Object, Workflow().Object, audit ?? Audit().Object);
    private static Mock<ICurrentUserService> User(Guid tenant, Guid actor)
    { var mock = new Mock<ICurrentUserService>(); mock.SetupGet(item => item.TenantId).Returns(tenant); mock.SetupGet(item => item.UserId).Returns(actor.ToString()); mock.SetupGet(item => item.UserName).Returns("c4.test"); return mock; }
    private static Mock<IWorkflowService> Workflow()
    { var mock = new Mock<IWorkflowService>(); mock.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
      mock.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
      mock.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
      mock.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
          .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed }); return mock; }
    private static Mock<IFinanceAuditService> Audit()
    { var mock = new Mock<IFinanceAuditService>(); mock.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog()); return mock; }
    private sealed record State(Guid TenantId, AccountingBook Book, FiscalPeriod Period, FiscalPeriod CutoffPeriod, Account Debit, Account Credit);
}
