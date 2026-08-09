using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public partial class FixedAssetDepreciationService : IFixedAssetDepreciationService
{
    private const string SourceModule = "FixedAssets";
    private const string SourceDocumentType = "FixedAssetDepreciationRun";
    private const string PostingAction = "Depreciation";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountingBookService? _accountingBookService;
    private readonly IFinancePostingEngine? _financePostingEngine;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IWorkflowService? _workflowService;
    private readonly IFinanceReversalPolicyService? _financeReversalPolicyService;

    public FixedAssetDepreciationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IJournalEntryService? journalEntryService = null,
        IAccountingBookService? accountingBookService = null,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowService? workflowService = null,
        IFinanceReversalPolicyService? financeReversalPolicyService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _accountingBookService = accountingBookService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _workflowService = workflowService;
        _financeReversalPolicyService = financeReversalPolicyService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid? UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : null;

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> RunDepreciationAsync(
        RunDepreciationDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var fiscalPeriod = await _context.FiscalPeriods
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == dto.FiscalPeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal period not found.");

        var postingDate = (dto.PostingDate ?? fiscalPeriod.EndDate).Date;
        if (fiscalPeriod.IsLocked || !fiscalPeriod.IsOpen)
        {
            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationBlockedClosedPeriod,
                fiscalPeriod.Id,
                afterValues: new { fiscalPeriod.PeriodCode, fiscalPeriod.IsOpen, fiscalPeriod.IsLocked, postingDate },
                reason: "Fiscal period is locked or not open.",
                comment: "Fixed asset depreciation posting blocked by period status.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException("Fiscal period is locked or not open.");
        }

        if (postingDate < fiscalPeriod.StartDate.Date || postingDate > fiscalPeriod.EndDate.Date)
        {
            throw new InvalidOperationException("Depreciation posting date must fall within the selected fiscal period.");
        }

        if (dto.PostToGl && _financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for fixed asset depreciation.");
        }

        var requestedBook = NormalizeBookClassification(dto.BookClassification);
        var activeBooks = await GetActivePostingBooksAsync(cancellationToken);
        var defaultBook = GetDefaultBook(activeBooks);
        var postAllBooks = requestedBook == "ALL_ACTIVE_BOOKS";
        var runBookClassification = postAllBooks
            ? "ALL_ACTIVE_BOOKS"
            : requestedBook ?? defaultBook.Code;
        var baseIdempotencyKey = BuildRunIdempotencyKey(
            tenantId,
            fiscalPeriod.Id,
            runBookClassification,
            dto.FixedAssetId,
            dto.PostToGl,
            correctionSequence: 0);
        var scopeRuns = await _context.FixedAssetDepreciationRuns
            .Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId &&
                (r.IdempotencyKey == baseIdempotencyKey || r.IdempotencyKey.StartsWith(baseIdempotencyKey + ":C")))
            .OrderByDescending(r => r.CorrectionSequence)
            .ToListAsync(cancellationToken);

        // A posted revision must be reversed before another run for the same scope is allowed.
        // Reversed revisions remain in scopeRuns so the next correction sequence is monotonic and
        // every idempotency key stays unique without rewriting the historical run.
        var existingRun = scopeRuns.FirstOrDefault(r =>
            !string.Equals(r.Status, "Reversed", StringComparison.OrdinalIgnoreCase));
        if (existingRun != null)
        {
            if (string.Equals(existingRun.Status, "Posted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingRun.Status, "Calculated", StringComparison.OrdinalIgnoreCase))
            {
                return existingRun.Lines
                    .OrderBy(l => l.FixedAssetId)
                    .ThenBy(l => l.BookClassification)
                    .Select(MapSchedule)
                    .ToList();
            }

            throw new InvalidOperationException("A depreciation run already exists for this tenant, period, and scope and must be reviewed before retry.");
        }

        var correctionSequence = scopeRuns.Count == 0
            ? 0
            : scopeRuns.Max(r => r.CorrectionSequence) + 1;
        var idempotencyKey = BuildRunIdempotencyKey(
            tenantId,
            fiscalPeriod.Id,
            runBookClassification,
            dto.FixedAssetId,
            dto.PostToGl,
            correctionSequence);

        var assetsQuery = _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(value => value.AccountingBook)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

        if (dto.FixedAssetId.HasValue)
        {
            assetsQuery = assetsQuery.Where(a => a.Id == dto.FixedAssetId.Value);
        }

        var assets = await assetsQuery.ToListAsync(cancellationToken);
        if (dto.FixedAssetId.HasValue && assets.Count == 0)
        {
            throw new KeyNotFoundException("Fixed asset not found.");
        }

        if (assets.Count == 0)
        {
            return Array.Empty<AssetDepreciationScheduleDto>();
        }

        var existingScheduleKeys = await _context.AssetDepreciationSchedules
            .Where(s => s.TenantId == tenantId && s.FiscalPeriodId == fiscalPeriod.Id && !s.IsDeleted && !s.IsReversed)
            .Select(s => new { s.FixedAssetId, s.BookClassification })
            .ToListAsync(cancellationToken);

        var existingSet = existingScheduleKeys
            .Select(s => BuildScheduleKey(s.FixedAssetId, s.BookClassification))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var depreciationLines = new List<DepreciationLineWorkItem>();
        foreach (var asset in assets)
        {
            // A bulk run must omit ineligible assets rather than allowing a validator return to
            // fall through into schedule creation. A single-asset request still fails explicitly.
            if (!EnsureAssetEligibleForDepreciation(asset, fiscalPeriod, postingDate, dto.FixedAssetId.HasValue))
            {
                continue;
            }

            EnsureLegacyBookValues(asset, activeBooks, defaultBook);
            var targetBookValues = SelectTargetBookValues(asset, requestedBook, postAllBooks, defaultBook);
            if (!postAllBooks && !string.IsNullOrWhiteSpace(requestedBook) && targetBookValues.Count == 0)
            {
                throw new InvalidOperationException("Requested accounting book was not found for this fixed asset.");
            }

            var expenseAccount = await ResolveDepreciationAccountAsync(
                asset.Category.DepreciationExpenseAccountId,
                "depreciation expense account",
                cancellationToken,
                AccountType.Expense);
            var accumulatedAccount = await ResolveDepreciationAccountAsync(
                asset.Category.AccumulatedDepreciationAccountId,
                "accumulated depreciation account",
                cancellationToken,
                AccountType.Asset);

            foreach (var bookValue in targetBookValues)
            {
                var scheduleKey = BuildScheduleKey(asset.Id, bookValue.BookClassification);
                if (existingSet.Contains(scheduleKey))
                {
                    continue;
                }

                ValidateBookValueForDepreciation(asset, bookValue, fiscalPeriod, postingDate);
                var depreciationAmount = CalculateStraightLineDepreciation(bookValue);
                if (depreciationAmount <= 0m)
                {
                    UpdateAssetDepreciationStatus(asset);
                    continue;
                }

                var accumulatedBefore = RoundMoney(bookValue.AccumulatedDepreciation);
                var netBookValueBefore = RoundMoney(bookValue.NetBookValue);
                var accumulatedAfter = RoundMoney(accumulatedBefore + depreciationAmount);
                var netBookValueAfter = RoundMoney(netBookValueBefore - depreciationAmount);
                if (netBookValueAfter < bookValue.ResidualValue)
                {
                    netBookValueAfter = RoundMoney(bookValue.ResidualValue);
                    depreciationAmount = RoundMoney(netBookValueBefore - bookValue.ResidualValue);
                    accumulatedAfter = RoundMoney(accumulatedBefore + depreciationAmount);
                }

                if (depreciationAmount <= 0m)
                {
                    UpdateAssetDepreciationStatus(asset);
                    continue;
                }

                var schedule = new AssetDepreciationSchedule
                {
                    TenantId = tenantId,
                    FixedAssetId = asset.Id,
                    AccountingBookId = bookValue.AccountingBookId,
                    BookClassification = bookValue.BookClassification,
                    FiscalPeriodId = fiscalPeriod.Id,
                    DepreciationAmount = depreciationAmount,
                    AccumulatedDepreciationBefore = accumulatedBefore,
                    AccumulatedDepreciation = accumulatedAfter,
                    NetBookValueBefore = netBookValueBefore,
                    NetBookValue = netBookValueAfter,
                    DepreciableAmount = RoundMoney(netBookValueBefore - bookValue.ResidualValue),
                    ResidualValueSnapshot = bookValue.ResidualValue,
                    UsefulLifeMonthsSnapshot = bookValue.UsefulLifeMonths,
                    DepreciationMethodSnapshot = bookValue.DepreciationMethod,
                    CorrectionSequence = correctionSequence,
                    PlacedInServiceDateSnapshot = bookValue.PlacedInServiceDate ?? asset.PlacedInServiceDate,
                    IsPosted = false,
                    IsProjected = !dto.PostToGl,
                    PostingDate = postingDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserName
                };

                depreciationLines.Add(new DepreciationLineWorkItem(
                    asset,
                    bookValue,
                    schedule,
                    expenseAccount.Id,
                    accumulatedAccount.Id));
                existingSet.Add(scheduleKey);
            }
        }

        if (depreciationLines.Count == 0)
        {
            return Array.Empty<AssetDepreciationScheduleDto>();
        }

        var run = new FixedAssetDepreciationRun
        {
            TenantId = tenantId,
            FiscalPeriodId = fiscalPeriod.Id,
            FixedAssetId = dto.FixedAssetId,
            BookClassification = runBookClassification,
            PostingDate = postingDate,
            Status = dto.PostToGl ? "Calculated" : "Calculated",
            TotalDepreciationAmount = RoundMoney(depreciationLines.Sum(line => line.Schedule.DepreciationAmount)),
            CorrectionSequence = correctionSequence,
            IdempotencyKey = idempotencyKey,
            CalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName
        };

        foreach (var line in depreciationLines)
        {
            line.Schedule.FixedAssetDepreciationRunId = run.Id;
        }

        _context.FixedAssetDepreciationRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordDepreciationAuditAsync(
            FinanceAuditEvents.FixedAssetDepreciationRunCreated,
            run.Id,
            afterValues: new { run.FiscalPeriodId, run.FixedAssetId, run.BookClassification, run.TotalDepreciationAmount },
            comment: "Fixed asset depreciation run created.",
            cancellationToken: cancellationToken);
        await RecordDepreciationAuditAsync(
            FinanceAuditEvents.FixedAssetDepreciationCalculated,
            run.Id,
            afterValues: new { LineCount = depreciationLines.Count, run.TotalDepreciationAmount },
            comment: "Fixed asset depreciation calculated.",
            cancellationToken: cancellationToken);

        if (dto.PostToGl && _workflowService != null)
        {
            run.Status = "PendingApproval";
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = _currentUser.UserName;
            foreach (var line in depreciationLines)
            {
                line.Schedule.IsProjected = true;
                _context.AssetDepreciationSchedules.Add(line.Schedule);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("FixedAssetDepreciationRun", run.Id);
            if (!workflowResult.Success)
            {
                run.Status = "Failed";
                run.FailedAt = DateTime.UtcNow;
                run.FailureReason = workflowResult.Message;
                await _context.SaveChangesAsync(cancellationToken);
                await RecordDepreciationAuditAsync(
                    FinanceAuditEvents.FinanceWorkflowApprovalFailed,
                    run.Id,
                    afterValues: new { workflowResult.Status, workflowResult.Message },
                    reason: workflowResult.Message,
                    comment: "Fixed asset depreciation workflow submission failed.",
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException(workflowResult.Message ?? "Fixed asset depreciation workflow could not be started.");
            }

            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FinanceWorkflowSubmitted,
                run.Id,
                afterValues: new
                {
                    run.Status,
                    workflowResult.WorkflowInstanceId,
                    LineCount = depreciationLines.Count,
                    run.TotalDepreciationAmount
                },
                comment: "Fixed asset depreciation run submitted for workflow approval.",
                cancellationToken: cancellationToken);

            return depreciationLines.Select(line => MapSchedule(line.Schedule)).ToList();
        }

        if (!dto.PostToGl)
        {
            foreach (var line in depreciationLines)
            {
                _context.AssetDepreciationSchedules.Add(line.Schedule);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationScheduleGenerated,
                run.Id,
                afterValues: new { LineCount = depreciationLines.Count, run.TotalDepreciationAmount, IsProjection = true },
                comment: "Projected fixed asset depreciation schedule generated.",
                cancellationToken: cancellationToken);
            return depreciationLines.Select(line => MapSchedule(line.Schedule)).ToList();
        }

        try
        {
            var functionalCurrency = await GetFunctionalCurrencyAsync(cancellationToken);
            var request = BuildPostingRequest(run, fiscalPeriod, depreciationLines, functionalCurrency);
            var postingResult = await _financePostingEngine!.PostAsync(request, cancellationToken);

            run.Status = "Posted";
            run.JournalEntryId = postingResult.JournalEntryId;
            run.PostingEventId = postingResult.PostingEventId;
            run.PostedAt = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = _currentUser.UserName;

            foreach (var item in depreciationLines)
            {
                ApplyPostedDepreciation(item, postingResult, postingDate);
            }

            if (!dto.FixedAssetId.HasValue)
            {
                fiscalPeriod.DepreciationComplete = true;
                fiscalPeriod.DepreciationCompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationPosted,
                run.Id,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                afterValues: new
                {
                    run.TotalDepreciationAmount,
                    postingResult.JournalEntryId,
                    postingResult.PostingEventId,
                    LineCount = depreciationLines.Count
                },
                comment: "Fixed asset depreciation posted through the central posting engine.",
                cancellationToken: cancellationToken);

            return depreciationLines.Select(line => MapSchedule(line.Schedule)).ToList();
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.FailedAt = DateTime.UtcNow;
            run.FailureReason = ex.Message;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = _currentUser.UserName;
            await _context.SaveChangesAsync(cancellationToken);

            var eventType = ex.Message.Contains("period is not open", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetDepreciationBlockedClosedPeriod
                    : ex.Message.Contains("account", StringComparison.OrdinalIgnoreCase)
                        ? FinanceAuditEvents.FixedAssetDepreciationAccountMappingInvalid
                        : FinanceAuditEvents.FixedAssetDepreciationPostingFailed;

            await RecordDepreciationAuditAsync(
                eventType,
                run.Id,
                afterValues: new { error = ex.Message },
                reason: ex.Message,
                comment: "Fixed asset depreciation posting failed.",
                cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> GetSchedulesForAssetAsync(
        Guid fixedAssetId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await _context.AssetDepreciationSchedules
            .Where(s => s.TenantId == TenantId && s.FixedAssetId == fixedAssetId && !s.IsDeleted)
            .OrderBy(s => s.FiscalPeriodId)
            .ThenBy(s => s.BookClassification)
            .Select(s => MapSchedule(s))
            .ToListAsync(cancellationToken);

        return schedules;
    }

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> PostApprovedRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for fixed asset depreciation.");
        }

        var tenantId = TenantId;
        var run = await _context.FixedAssetDepreciationRuns
            .Include(r => r.FiscalPeriod)
            .Include(r => r.Lines)
                .ThenInclude(line => line.AccountingBook)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset depreciation run not found.");

        if (string.Equals(run.Status, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            return run.Lines.OrderBy(l => l.FixedAssetId).ThenBy(l => l.BookClassification).Select(MapSchedule).ToList();
        }

        if (_workflowService != null && !string.Equals(run.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var rejected = string.Equals(run.Status, "Rejected", StringComparison.OrdinalIgnoreCase);
            await RecordDepreciationAuditAsync(
                rejected
                    ? FinanceAuditEvents.FinancePostingBlockedAfterRejection
                    : FinanceAuditEvents.FinancePostingBlockedPendingApproval,
                run.Id,
                afterValues: new { run.Status },
                reason: rejected
                    ? "Fixed asset depreciation run was rejected by workflow."
                    : "Fixed asset depreciation run has not been approved.",
                comment: "Fixed asset depreciation posting blocked by workflow status.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException(rejected
                ? "Rejected depreciation runs cannot be posted."
                : "Depreciation run must be approved before posting.");
        }

        if (run.FiscalPeriod.IsLocked || !run.FiscalPeriod.IsOpen)
        {
            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationBlockedClosedPeriod,
                run.Id,
                afterValues: new { run.FiscalPeriod.PeriodCode, run.FiscalPeriod.IsOpen, run.FiscalPeriod.IsLocked, run.PostingDate },
                reason: "Fiscal period is locked or not open.",
                comment: "Fixed asset depreciation posting blocked by period status.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException("Fiscal period is locked or not open.");
        }

        var pendingLines = run.Lines
            .Where(line => !line.IsPosted && !line.IsDeleted)
            .OrderBy(line => line.FixedAssetId)
            .ThenBy(line => line.BookClassification)
            .ToList();
        if (pendingLines.Count == 0)
        {
            return run.Lines.Select(MapSchedule).ToList();
        }

        var assetIds = pendingLines.Select(line => line.FixedAssetId).Distinct().ToList();
        var assets = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(value => value.AccountingBook)
            .Where(a => a.TenantId == tenantId && assetIds.Contains(a.Id) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var depreciationLines = new List<DepreciationLineWorkItem>();
        foreach (var schedule in pendingLines)
        {
            if (!assets.TryGetValue(schedule.FixedAssetId, out var asset))
            {
                throw new InvalidOperationException("Depreciation run references a fixed asset that was not found for this tenant.");
            }

            // A run can wait for workflow approval. Revalidate each persisted schedule before
            // posting so an asset disposed or put on hold in the interim cannot reach the GL.
            EnsureAssetEligibleForDepreciation(asset, run.FiscalPeriod, run.PostingDate, isSingleAssetRun: true);
            var bookValue = asset.BookValues.FirstOrDefault(value =>
                    !value.IsDeleted &&
                    ((schedule.AccountingBookId.HasValue && value.AccountingBookId == schedule.AccountingBookId) ||
                     value.BookClassification.Equals(schedule.BookClassification, StringComparison.OrdinalIgnoreCase)))
                ?? throw new InvalidOperationException("Depreciation run references a fixed asset book value that was not found for this tenant.");

            var expenseAccount = await ResolveDepreciationAccountAsync(
                asset.Category.DepreciationExpenseAccountId,
                "depreciation expense account",
                cancellationToken,
                AccountType.Expense);
            var accumulatedAccount = await ResolveDepreciationAccountAsync(
                asset.Category.AccumulatedDepreciationAccountId,
                "accumulated depreciation account",
                cancellationToken,
                AccountType.Asset);

            depreciationLines.Add(new DepreciationLineWorkItem(asset, bookValue, schedule, expenseAccount.Id, accumulatedAccount.Id));
        }

        try
        {
            var functionalCurrency = await GetFunctionalCurrencyAsync(cancellationToken);
            var request = BuildPostingRequest(run, run.FiscalPeriod, depreciationLines, functionalCurrency);
            var postingResult = await _financePostingEngine.PostAsync(request, cancellationToken);

            run.Status = "Posted";
            run.JournalEntryId = postingResult.JournalEntryId;
            run.PostingEventId = postingResult.PostingEventId;
            run.PostedAt = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = _currentUser.UserName;

            foreach (var item in depreciationLines)
            {
                ApplyPostedDepreciation(item, postingResult, run.PostingDate);
            }

            if (!run.FixedAssetId.HasValue)
            {
                run.FiscalPeriod.DepreciationComplete = true;
                run.FiscalPeriod.DepreciationCompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationPosted,
                run.Id,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                afterValues: new
                {
                    run.TotalDepreciationAmount,
                    postingResult.JournalEntryId,
                    postingResult.PostingEventId,
                    LineCount = depreciationLines.Count
                },
                comment: "Workflow-approved fixed asset depreciation posted through the central posting engine.",
                cancellationToken: cancellationToken);

            return depreciationLines.Select(line => MapSchedule(line.Schedule)).ToList();
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.FailedAt = DateTime.UtcNow;
            run.FailureReason = ex.Message;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = _currentUser.UserName;
            await _context.SaveChangesAsync(cancellationToken);

            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationPostingFailed,
                run.Id,
                afterValues: new { error = ex.Message },
                reason: ex.Message,
                comment: "Workflow-approved fixed asset depreciation posting failed.",
                cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> GetSchedulesForPeriodAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await _context.AssetDepreciationSchedules
            .Where(s => s.TenantId == TenantId && s.FiscalPeriodId == fiscalPeriodId && !s.IsDeleted)
            .OrderBy(s => s.FixedAssetId)
            .ThenBy(s => s.BookClassification)
            .Select(s => MapSchedule(s))
            .ToListAsync(cancellationToken);

        return schedules;
    }

    private async Task<List<AccountingBook>> GetActivePostingBooksAsync(CancellationToken cancellationToken)
    {
        if (_accountingBookService != null)
        {
            await _accountingBookService.EnsureTenantDefaultsAsync(cancellationToken);
        }

        var books = await _context.AccountingBooks
            .Where(book => book.TenantId == TenantId && !book.IsDeleted && book.IsActive && book.AllowsPosting)
            .OrderBy(book => book.SortOrder)
            .ThenBy(book => book.Name)
            .ToListAsync(cancellationToken);

        if (books.Count > 0)
        {
            return books;
        }

        var fallbackBook = new AccountingBook
        {
            TenantId = TenantId,
            Code = "IFRS",
            Name = "IFRS",
            Description = "Primary corporate reporting book for IFRS financial statements.",
            Purpose = "Primary",
            IsActive = true,
            IsDefault = true,
            AllowsPosting = true,
            IsSystemDefined = true,
            SortOrder = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system"
        };

        _context.AccountingBooks.Add(fallbackBook);
        await _context.SaveChangesAsync(cancellationToken);
        return new List<AccountingBook> { fallbackBook };
    }

    private async Task<Account> ResolveDepreciationAccountAsync(
        Guid accountId,
        string label,
        CancellationToken cancellationToken,
        params AccountType[] allowedTypes)
    {
        if (accountId == Guid.Empty)
        {
            await RecordDepreciationAuditAsync(
                FinanceAuditEvents.FixedAssetDepreciationAccountMappingInvalid,
                Guid.Empty,
                afterValues: new { label, accountId },
                reason: $"{label} is required.",
                comment: "Fixed asset depreciation account mapping is missing.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException($"Fixed asset {label} is required.");
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == accountId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"Fixed asset {label} was not found for this tenant.");

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Fixed asset {label} must be active.");
        }

        if (!account.AllowDirectPosting)
        {
            throw new InvalidOperationException($"Fixed asset {label} must allow direct posting.");
        }

        if (!allowedTypes.Contains(account.AccountType))
        {
            throw new InvalidOperationException($"Fixed asset {label} must be a valid {string.Join(" or ", allowedTypes)} account.");
        }

        return account;
    }

    private FinancePostingRequestDto BuildPostingRequest(
        FixedAssetDepreciationRun run,
        FiscalPeriod fiscalPeriod,
        IReadOnlyList<DepreciationLineWorkItem> lines,
        string functionalCurrency)
    {
        var postingLines = new List<FinancePostingLineDto>();
        var lineNumber = 1;
        foreach (var item in lines)
        {
            var reference = $"DEP-{fiscalPeriod.PeriodCode}-{item.Asset.AssetCode}";
            var notes = $"FixedAssetId={item.Asset.Id:N};DepreciationRunId={run.Id:N};ScheduleId={item.Schedule.Id:N};Book={item.Schedule.BookClassification}";
            postingLines.Add(new FinancePostingLineDto
            {
                AccountId = item.ExpenseAccountId,
                DebitAmount = item.Schedule.DepreciationAmount,
                CreditAmount = 0m,
                TransactionCurrency = functionalCurrency,
                TransactionDebitAmount = item.Schedule.DepreciationAmount,
                TransactionCreditAmount = 0m,
                Description = $"Depreciation expense - {item.Asset.AssetCode}",
                SourceReferenceNumber = reference,
                LineNumber = lineNumber++,
                SegmentString = item.Asset.CurrentSegmentString,
                Notes = notes,
                TransactionTag = "FA-Depreciation"
            });
            postingLines.Add(new FinancePostingLineDto
            {
                AccountId = item.AccumulatedDepreciationAccountId,
                DebitAmount = 0m,
                CreditAmount = item.Schedule.DepreciationAmount,
                TransactionCurrency = functionalCurrency,
                TransactionDebitAmount = 0m,
                TransactionCreditAmount = item.Schedule.DepreciationAmount,
                Description = $"Accumulated depreciation - {item.Asset.AssetCode}",
                SourceReferenceNumber = reference,
                LineNumber = lineNumber++,
                SegmentString = item.Asset.CurrentSegmentString,
                Notes = notes,
                TransactionTag = "FA-AccumulatedDepreciation"
            });
        }

        return new FinancePostingRequestDto
        {
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = run.Id,
            SourceDocumentTenantId = run.TenantId,
            PostingAction = PostingAction,
            SourceDocumentReference = $"DEP-{fiscalPeriod.PeriodCode}-{run.BookClassification}",
            Description = $"Fixed asset depreciation - {fiscalPeriod.PeriodCode} - {run.BookClassification}",
            PostingDate = run.PostingDate,
            FiscalPeriodId = fiscalPeriod.Id,
            JournalType = "Fixed Asset Depreciation",
            BookClassification = run.BookClassification == "ALL_ACTIVE_BOOKS" ? "IFRS" : run.BookClassification,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = run.IdempotencyKey,
            ReturnExistingOnDuplicate = true,
            Lines = postingLines
        };
    }

    private void ApplyPostedDepreciation(
        DepreciationLineWorkItem item,
        FinancePostingResultDto postingResult,
        DateTime postingDate)
    {
        item.Schedule.IsPosted = true;
        item.Schedule.IsProjected = false;
        item.Schedule.PostedDate = DateTime.UtcNow;
        item.Schedule.PostingDate = postingDate;
        item.Schedule.JournalEntryId = postingResult.JournalEntryId;
        item.Schedule.PostingEventId = postingResult.PostingEventId;
        if (_context.Entry(item.Schedule).State == EntityState.Detached)
        {
            _context.AssetDepreciationSchedules.Add(item.Schedule);
        }

        item.BookValue.AccumulatedDepreciation = item.Schedule.AccumulatedDepreciation;
        item.BookValue.NetBookValue = item.Schedule.NetBookValue;
        item.BookValue.LastDepreciationDate = postingDate;
        if (item.BookValue.RemainingUsefulLifeMonths.HasValue && item.BookValue.RemainingUsefulLifeMonths.Value > 0)
        {
            item.BookValue.RemainingUsefulLifeMonths -= 1;
        }
        item.BookValue.UpdatedAt = DateTime.UtcNow;
        item.BookValue.UpdatedBy = _currentUser.UserName;

        if (item.BookValue.AccountingBook?.IsDefault == true || item.BookValue.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
        {
            item.Asset.NetBookValue = item.Schedule.NetBookValue;
            item.Asset.ResidualValue = item.BookValue.ResidualValue;
            item.Asset.UsefulLifeMonths = item.BookValue.UsefulLifeMonths;
        }

        UpdateAssetDepreciationStatus(item.Asset);
        item.Asset.UpdatedAt = DateTime.UtcNow;
        item.Asset.UpdatedBy = _currentUser.UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = item.Asset.Id,
            AccountingBookId = item.BookValue.AccountingBookId,
            BookClassification = item.BookValue.BookClassification,
            TransactionDate = postingDate,
            TransactionType = "Depreciation",
            Description = $"Depreciation for {item.Schedule.BookClassification}",
            Amount = item.Schedule.DepreciationAmount,
            ResultingBookValue = item.Schedule.NetBookValue,
            RelatedEntityId = postingResult.PostingEventId,
            PerformedByUserId = UserId ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName
        });
    }

    private static AccountingBook GetDefaultBook(IReadOnlyList<AccountingBook> books)
        => books.FirstOrDefault(book => book.IsDefault) ?? books.First();

    private static string? NormalizeBookClassification(string? bookClassification)
    {
        var normalized = bookClassification?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToUpperInvariant();
    }

    private static string BuildScheduleKey(Guid assetId, string? bookClassification)
        => $"{assetId:N}|{NormalizeBookClassification(bookClassification) ?? "IFRS"}";

    private static string BuildRunIdempotencyKey(
        Guid tenantId,
        Guid fiscalPeriodId,
        string bookClassification,
        Guid? fixedAssetId,
        bool postToGl,
        int correctionSequence)
    {
        var baseKey = $"FA:Depreciation:{tenantId:N}:{fiscalPeriodId:N}:{bookClassification}:{fixedAssetId?.ToString("N") ?? "ALL"}:{(postToGl ? "Post" : "Calculate")}";
        return correctionSequence == 0 ? baseKey : $"{baseKey}:C{correctionSequence}";
    }

    private bool EnsureAssetEligibleForDepreciation(
        FixedAsset asset,
        FiscalPeriod fiscalPeriod,
        DateTime postingDate,
        bool isSingleAssetRun)
    {
        if (asset.TenantId != TenantId || asset.Category?.TenantId != TenantId)
        {
            throw new InvalidOperationException("Fixed asset or category belongs to another tenant.");
        }

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff or FixedAssetStatus.HeldForSale or FixedAssetStatus.OnHold)
        {
            return HandleIneligibleAsset("Fixed asset status is not eligible for depreciation.", isSingleAssetRun);
        }

        if (!IsCapitalized(asset))
        {
            return HandleIneligibleAsset("Fixed asset must be capitalized before depreciation can run.", isSingleAssetRun);
        }

        if (asset.Status != FixedAssetStatus.Active)
        {
            return HandleIneligibleAsset("Fixed asset must be active before depreciation can run.", isSingleAssetRun);
        }

        if (!asset.PlacedInServiceDate.HasValue)
        {
            return HandleIneligibleAsset("Fixed asset must have a placed-in-service date before depreciation can run.", isSingleAssetRun);
        }

        if (asset.CapitalizationDate.HasValue && postingDate < asset.CapitalizationDate.Value.Date)
        {
            return HandleIneligibleAsset("Depreciation cannot post before the capitalization date.", isSingleAssetRun);
        }

        if (postingDate < asset.PlacedInServiceDate.Value.Date || fiscalPeriod.EndDate.Date < asset.PlacedInServiceDate.Value.Date)
        {
            return HandleIneligibleAsset("Depreciation cannot post before the placed-in-service date.", isSingleAssetRun);
        }

        return true;
    }

    private static bool HandleIneligibleAsset(string message, bool isSingleAssetRun)
    {
        if (isSingleAssetRun)
        {
            throw new InvalidOperationException(message);
        }

        return false;
    }

    private static void ValidateBookValueForDepreciation(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        FiscalPeriod fiscalPeriod,
        DateTime postingDate)
    {
        if (bookValue.TenantId != asset.TenantId)
        {
            throw new InvalidOperationException("Fixed asset book value belongs to another tenant.");
        }

        if (bookValue.DepreciationMethod != DepreciationMethod.StraightLine)
        {
            throw new InvalidOperationException("Only straight-line depreciation is supported in the Batch 20 depreciation foundation.");
        }

        if (bookValue.UsefulLifeMonths <= 0)
        {
            throw new InvalidOperationException("Fixed asset useful life must be greater than zero.");
        }

        if (bookValue.ResidualValue < 0m)
        {
            throw new InvalidOperationException("Fixed asset residual value cannot be negative.");
        }

        if (bookValue.ResidualValue > bookValue.AcquisitionCost)
        {
            throw new InvalidOperationException("Fixed asset residual value cannot exceed capitalized cost.");
        }

        var placedInServiceDate = bookValue.PlacedInServiceDate ?? asset.PlacedInServiceDate;
        if (!placedInServiceDate.HasValue)
        {
            throw new InvalidOperationException("Fixed asset book value must have a placed-in-service date before depreciation can run.");
        }

        if (bookValue.CapitalizationDate.HasValue && postingDate < bookValue.CapitalizationDate.Value.Date)
        {
            throw new InvalidOperationException("Depreciation cannot post before the book capitalization date.");
        }

        if (postingDate < placedInServiceDate.Value.Date || fiscalPeriod.EndDate.Date < placedInServiceDate.Value.Date)
        {
            throw new InvalidOperationException("Depreciation cannot post before the book placed-in-service date.");
        }
    }

    private static decimal CalculateStraightLineDepreciation(FixedAssetBookValue bookValue)
    {
        var remaining = RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue);
        if (remaining <= 0m)
        {
            return 0m;
        }

        var remainingUsefulLife = bookValue.RemainingUsefulLifeMonths.GetValueOrDefault(bookValue.UsefulLifeMonths);
        if (remainingUsefulLife <= 0)
        {
            return remaining;
        }

        var unadjustedNetBookValue = RoundMoney(bookValue.AcquisitionCost - bookValue.AccumulatedDepreciation);
        var hasValuationAdjustment = Math.Abs(unadjustedNetBookValue - RoundMoney(bookValue.NetBookValue)) >= 0.01m;
        var depreciationBase = hasValuationAdjustment
            ? remaining
            : RoundMoney(bookValue.AcquisitionCost - bookValue.ResidualValue);
        var divisor = hasValuationAdjustment
            ? remainingUsefulLife
            : bookValue.UsefulLifeMonths;

        if (divisor <= 0)
        {
            return remaining;
        }

        var monthlyCharge = RoundMoney(depreciationBase / divisor);
        return monthlyCharge > remaining ? remaining : monthlyCharge;
    }

    private void EnsureLegacyBookValues(
        FixedAsset asset,
        IReadOnlyList<AccountingBook> activeBooks,
        AccountingBook defaultBook)
    {
        if (asset.BookValues.Any(value => !value.IsDeleted))
        {
            return;
        }

        var accumulatedDepreciation = Math.Max(asset.AcquisitionCost - asset.NetBookValue, 0);
        foreach (var book in activeBooks)
        {
            asset.BookValues.Add(new FixedAssetBookValue
            {
                TenantId = asset.TenantId,
                FixedAssetId = asset.Id,
                AccountingBookId = book.Id,
                AccountingBook = book,
                BookClassification = book.Code,
                AcquisitionCost = asset.AcquisitionCost,
                AccumulatedDepreciation = accumulatedDepreciation,
                NetBookValue = asset.NetBookValue,
                ResidualValue = asset.ResidualValue,
                UsefulLifeMonths = asset.UsefulLifeMonths,
                RemainingUsefulLifeMonths = asset.UsefulLifeMonths,
                DepreciationMethod = asset.DepreciationMethod,
                DepreciationConvention = asset.DepreciationConvention,
                PlacedInServiceDate = asset.PlacedInServiceDate,
                CapitalizationDate = asset.CapitalizationDate,
                CapitalizationJournalEntryId = asset.JournalEntryId,
                CapitalizationPostingEventId = asset.PostingEventId,
                SourceDocumentType = asset.SourceDocumentType,
                SourceDocumentId = asset.SourceDocumentId,
                SourceDocumentLineId = asset.SourceDocumentLineId,
                OpeningAsOfDate = asset.PlacedInServiceDate,
                OpeningYtdDepreciation = 0,
                OpeningSource = book.Id == defaultBook.Id ? "LegacyDefaultBookBackfill" : "LegacyBookBackfill",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName ?? "system"
            });
        }
    }

    private static IReadOnlyList<FixedAssetBookValue> SelectTargetBookValues(
        FixedAsset asset,
        string? requestedBook,
        bool postAllBooks,
        AccountingBook defaultBook)
    {
        var values = asset.BookValues
            .Where(value => !value.IsDeleted)
            .ToList();

        if (postAllBooks)
        {
            return values;
        }

        if (!string.IsNullOrWhiteSpace(requestedBook))
        {
            return values
                .Where(value => NormalizeBookClassification(value.BookClassification) == requestedBook)
                .ToList();
        }

        var defaultValue = values.FirstOrDefault(value => value.AccountingBookId == defaultBook.Id)
            ?? values
                .OrderBy(value => value.AccountingBook?.IsDefault == true ? 0 : 1)
                .ThenBy(value => value.AccountingBook?.SortOrder ?? int.MaxValue)
                .FirstOrDefault();

        return defaultValue == null ? Array.Empty<FixedAssetBookValue>() : new[] { defaultValue };
    }

    private static void UpdateAssetDepreciationStatus(FixedAsset asset)
    {
        var values = asset.BookValues
            .Where(value => !value.IsDeleted)
            .ToList();

        if (values.Count == 0)
        {
            return;
        }

        if (values.All(value => value.NetBookValue <= value.ResidualValue))
        {
            asset.Status = FixedAssetStatus.FullyDepreciated;
        }
    }

    private async Task<string> GetFunctionalCurrencyAsync(CancellationToken cancellationToken)
    {
        var settingsCurrency = await _context.FinanceSettings
            .Where(s => s.TenantId == TenantId)
            .Select(s => s.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(settingsCurrency))
        {
            return settingsCurrency.Trim().ToUpperInvariant();
        }

        var tenantCurrency = await _context.Tenants
            .Where(t => t.Id == TenantId)
            .Select(t => t.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(tenantCurrency)
            ? "GHS"
            : tenantCurrency.Trim().ToUpperInvariant();
    }

    private async Task RecordDepreciationAuditAsync(
        string eventType,
        Guid sourceDocumentId,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        object? beforeValues = null,
        object? afterValues = null,
        string? reason = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = sourceDocumentId == Guid.Empty ? null : sourceDocumentId,
            PostingEventId = postingEventId,
            JournalEntryId = journalEntryId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Resource = SourceDocumentType,
            ResourceId = sourceDocumentId == Guid.Empty ? null : sourceDocumentId.ToString()
        }, cancellationToken);
    }

    private static AssetDepreciationScheduleDto MapSchedule(AssetDepreciationSchedule schedule)
    {
        return new AssetDepreciationScheduleDto
        {
            Id = schedule.Id,
            FixedAssetId = schedule.FixedAssetId,
            FixedAssetDepreciationRunId = schedule.FixedAssetDepreciationRunId,
            AccountingBookId = schedule.AccountingBookId,
            BookClassification = schedule.BookClassification,
            FiscalPeriodId = schedule.FiscalPeriodId,
            DepreciationAmount = schedule.DepreciationAmount,
            AccumulatedDepreciationBefore = schedule.AccumulatedDepreciationBefore,
            AccumulatedDepreciation = schedule.AccumulatedDepreciation,
            NetBookValueBefore = schedule.NetBookValueBefore,
            NetBookValue = schedule.NetBookValue,
            DepreciableAmount = schedule.DepreciableAmount,
            ResidualValueSnapshot = schedule.ResidualValueSnapshot,
            UsefulLifeMonthsSnapshot = schedule.UsefulLifeMonthsSnapshot,
            DepreciationMethodSnapshot = schedule.DepreciationMethodSnapshot,
            PlacedInServiceDateSnapshot = schedule.PlacedInServiceDateSnapshot,
            IsPosted = schedule.IsPosted,
            PostedDate = schedule.PostedDate,
            PostingDate = schedule.PostingDate,
            JournalEntryId = schedule.JournalEntryId,
            PostingEventId = schedule.PostingEventId,
            IsProjected = schedule.IsProjected,
            CorrectionSequence = schedule.CorrectionSequence,
            IsReversed = schedule.IsReversed,
            ReversedAt = schedule.ReversedAt,
            ReversalJournalEntryId = schedule.ReversalJournalEntryId,
            ReversalPostingEventId = schedule.ReversalPostingEventId,
            DepreciationReversalId = schedule.DepreciationReversalId
        };
    }

    private static bool IsCapitalized(FixedAsset asset)
        => asset.PostingEventId.HasValue
           || asset.JournalEntryId.HasValue
           || asset.CapitalizedAt.HasValue
           || asset.BookValues.Any(value =>
               !value.IsDeleted &&
               (value.CapitalizationPostingEventId.HasValue ||
                value.CapitalizationJournalEntryId.HasValue ||
                value.OpeningPostedToGl ||
                value.OpeningSource.Contains("Opening", StringComparison.OrdinalIgnoreCase)));

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record DepreciationLineWorkItem(
        FixedAsset Asset,
        FixedAssetBookValue BookValue,
        AssetDepreciationSchedule Schedule,
        Guid ExpenseAccountId,
        Guid AccumulatedDepreciationAccountId);
}
