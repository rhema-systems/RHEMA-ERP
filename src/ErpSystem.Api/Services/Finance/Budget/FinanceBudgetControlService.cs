using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Budget;

/// <summary>
/// Finance-owned budget control for manual journals. Procurement commitments remain owned by
/// Procurement; future adapters must call this contract with canonical Finance budget cells.
/// </summary>
public sealed class FinanceBudgetControlService : IFinanceBudgetControlService
{
    internal const string ManualJournalSource = "ManualJournalEntry";
    internal const string OverrideWorkflowType = "FinanceBudgetOverride";
    private const string ReservedStatus = "Reserved";
    private const string ConsumedStatus = "Consumed";
    private const string ReleasedStatus = "Released";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;

    public FinanceBudgetControlService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IWorkflowService workflow)
    {
        _db = db;
        _currentUser = currentUser;
        _workflow = workflow;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public Task<FinanceBudgetControlEvaluationDto> EvaluateManualJournalAsync(
        Guid journalEntryId,
        CancellationToken cancellationToken = default) =>
        EvaluateManualJournalCoreAsync(TenantId, journalEntryId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ReserveManualJournalAsync(
        Guid journalEntryId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        await EnsureDraftManualJournalSourceAsync(tenantId, journalEntryId, cancellationToken);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                await AcquireReservationLockAsync(tenantId, cancellationToken);
                var existing = await _db.FinanceBudgetReservations
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted
                        && x.SourceDocumentType == ManualJournalSource
                        && x.SourceDocumentId == journalEntryId
                        && x.Status == ReservedStatus)
                    .ToListAsync(cancellationToken);
                if (existing.Count > 0)
                {
                    var current = await EvaluateManualJournalCoreAsync(tenantId, journalEntryId, cancellationToken);
                    EnsureAllowed(current);
                    EnsureReservationsMatchEvaluation(existing, current);
                    return (IReadOnlyList<Guid>)existing.Select(x => x.Id).ToList();
                }

                var evaluation = await EvaluateManualJournalCoreAsync(tenantId, journalEntryId, cancellationToken);
                EnsureAllowed(evaluation);
                if (!evaluation.HasTrackedExpenseLines)
                    return Array.Empty<Guid>();

                var overrideRequest = evaluation.HasApprovedOverride
                    ? await ApprovedOverrideAsync(tenantId, journalEntryId, evaluation.EvaluationHash, cancellationToken)
                    : null;
                var now = DateTime.UtcNow;
                var reservations = evaluation.Lines
                    .Where(x => x.BudgetEntryId.HasValue)
                    .Select(line => new FinanceBudgetReservation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BudgetScenarioId = line.BudgetScenarioId!.Value,
                        BudgetReturnId = line.BudgetReturnId!.Value,
                        BudgetEntryId = line.BudgetEntryId!.Value,
                        AccountId = line.AccountId,
                        FiscalPeriodId = line.FiscalPeriodId,
                        SegmentValueId = line.SegmentValueId,
                        FinanceDimensionSetId = line.FinanceDimensionSetId,
                        DimensionCombinationHashSnapshot = line.DimensionCombinationHash,
                        CurrencyCode = evaluation.CurrencyCode,
                        SourceDocumentType = ManualJournalSource,
                        SourceDocumentId = journalEntryId,
                        SourceVersion = evaluation.EvaluationHash,
                        BudgetDate = evaluation.EntryDate.Date,
                        SourceLineIdsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                        {
                            $"{line.AccountId:N}:{line.FiscalPeriodId:N}:{line.DimensionCombinationHash ?? "LEGACY"}"
                        }),
                        TransactionCurrencyCode = evaluation.CurrencyCode,
                        TransactionAmount = line.RequestedAmount,
                        ExchangeRate = 1m,
                        ReservationVersion = 1,
                        ReservedAmount = line.RequestedAmount,
                        BudgetAmountSnapshot = line.BudgetAmount,
                        PostedActualSnapshot = line.PostedActualAmount,
                        OtherReservationsSnapshot = line.ReservedAmount,
                        AvailableBeforeReservationSnapshot = line.AvailableAmount,
                        Status = ReservedStatus,
                        EvaluationHash = evaluation.EvaluationHash,
                        OverrideRequestId = overrideRequest?.Id,
                        ReservedByUserId = UserId,
                        ReservedAt = now,
                        CreatedAt = now,
                        CreatedById = UserId == Guid.Empty ? null : UserId,
                        CreatedBy = _currentUser.UserName
                    })
                    .ToList();
                _db.FinanceBudgetReservations.AddRange(reservations);
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return (IReadOnlyList<Guid>)reservations.Select(x => x.Id).ToList();
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task ReleaseManualJournalAsync(Guid journalEntryId, string reason, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var rows = await _db.FinanceBudgetReservations
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == ManualJournalSource
                && x.SourceDocumentId == journalEntryId
                && x.Status == ReservedStatus)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return;
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.Status = ReleasedStatus;
            row.ReleasedAt = now;
            row.ReleasedByUserId = UserId;
            row.ReleaseReason = string.IsNullOrWhiteSpace(reason) ? "Journal approval lifecycle ended." : reason.Trim();
            row.UpdatedAt = now;
            row.LastModifiedById = UserId == Guid.Empty ? null : UserId;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ValidateManualJournalForPostingAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var evaluation = await EvaluateManualJournalCoreAsync(tenantId, journalEntryId, cancellationToken);
        EnsureAllowed(evaluation);
        if (!evaluation.HasTrackedExpenseLines)
            return Array.Empty<Guid>();

        var reservations = await _db.FinanceBudgetReservations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == ManualJournalSource
                && x.SourceDocumentId == journalEntryId
                && x.Status == ReservedStatus)
            .ToListAsync(cancellationToken);
        if (reservations.Count == 0)
            throw new InvalidOperationException("The journal does not have Finance budget reservation evidence. Withdraw and resubmit it for approval.");
        EnsureReservationsMatchEvaluation(reservations, evaluation);
        return reservations.Select(x => x.Id).ToList();
    }

    public async Task ConsumeReservationsAsync(
        Guid tenantId,
        Guid sourceDocumentId,
        IReadOnlyList<Guid> reservationIds,
        Guid journalEntryId,
        Guid postingEventId,
        CancellationToken cancellationToken = default)
    {
        if (reservationIds.Count == 0)
            return;
        var rows = await _db.FinanceBudgetReservations
            .Where(x => x.TenantId == tenantId && reservationIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        if (rows.Count != reservationIds.Distinct().Count()
            || rows.Any(x => x.SourceDocumentType != ManualJournalSource
                || x.SourceDocumentId != sourceDocumentId
                || x.Status != ReservedStatus))
            throw new InvalidOperationException("Finance budget reservation evidence is missing, stale, or belongs to another source document.");
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.Status = ConsumedStatus;
            row.ConsumedAt = now;
            row.ConsumedByUserId = UserId;
            row.JournalEntryId = journalEntryId;
            row.PostingEventId = postingEventId;
            row.UpdatedAt = now;
            row.LastModifiedById = UserId == Guid.Empty ? null : UserId;
        }
    }

    public async Task<FinanceBudgetOverrideRequestDto> RequestManualJournalOverrideAsync(
        Guid journalEntryId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw new ArgumentException("A budget override reason of at least 10 characters is required.", nameof(reason));
        var tenantId = TenantId;
        await EnsureDraftManualJournalSourceAsync(tenantId, journalEntryId, cancellationToken);
        var evaluation = await EvaluateManualJournalCoreAsync(tenantId, journalEntryId, cancellationToken);
        if (!evaluation.RequiresOverride || evaluation.Lines.Any(x => x.DecisionCode != "INSUFFICIENT_BUDGET" && x.DecisionCode != "AVAILABLE"))
            throw new InvalidOperationException("An override can only be requested for a journal whose sole budget failure is insufficient available budget.");

        var existing = await _db.FinanceBudgetOverrideRequests
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == ManualJournalSource
                && x.SourceDocumentId == journalEntryId
                && x.EvaluationHash == evaluation.EvaluationHash
                && (x.Status == "PendingApproval" || x.Status == "Approved"), cancellationToken);
        if (existing != null)
            return MapOverride(existing);

        var now = DateTime.UtcNow;
        var request = new FinanceBudgetOverrideRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceDocumentType = ManualJournalSource,
            SourceDocumentId = journalEntryId,
            CurrencyCode = evaluation.CurrencyCode,
            EvaluationHash = evaluation.EvaluationHash,
            Reason = reason.Trim(),
            RequestedAmount = evaluation.TotalRequestedAmount,
            ShortfallAmount = evaluation.TotalShortfallAmount,
            Status = "PendingApproval",
            RequestedByUserId = UserId,
            RequestedAt = now,
            CreatedAt = now,
            CreatedById = UserId == Guid.Empty ? null : UserId,
            CreatedBy = _currentUser.UserName
        };
        _db.FinanceBudgetOverrideRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            var workflow = await _workflow.StartApprovalWorkflowAsync(OverrideWorkflowType, request.Id);
            if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue || workflow.WorkflowInstanceId == Guid.Empty)
                throw new InvalidOperationException(workflow.Message ?? "Finance budget override workflow could not be started.");
            request.WorkflowInstanceId = workflow.WorkflowInstanceId.Value;
            await _db.SaveChangesAsync(cancellationToken);
            return MapOverride(request);
        }
        catch
        {
            _db.FinanceBudgetOverrideRequests.Remove(request);
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task ApplyOverrideOutcomeAsync(Guid overrideRequestId, bool approved, Guid actorUserId, string? reason, CancellationToken cancellationToken = default)
    {
        var request = await _db.FinanceBudgetOverrideRequests.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == overrideRequestId && !x.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("Finance budget override request was not found.");
        if (request.Status != "PendingApproval")
            return;
        if (!request.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Finance budget override workflow evidence was not linked.");
        var workflow = await _db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == request.WorkflowInstanceId.Value && !x.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("Finance budget override workflow evidence was not found.");
        var now = DateTime.UtcNow;
        if (approved)
        {
            if (workflow.Status != WorkflowInstanceStatus.Completed)
                throw new InvalidOperationException("The shared Finance budget override workflow is not complete.");
            request.Status = "Approved";
            request.ApprovedByUserId = actorUserId;
            request.ApprovedAt = now;
        }
        else
        {
            request.Status = "Rejected";
            request.RejectedByUserId = actorUserId;
            request.RejectedAt = now;
            request.RejectionReason = reason;
        }
        request.UpdatedAt = now;
        request.LastModifiedById = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateManualJournalOverridesAsync(
        Guid journalEntryId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var requests = await _db.FinanceBudgetOverrideRequests
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == ManualJournalSource
                && x.SourceDocumentId == journalEntryId
                && (x.Status == "PendingApproval" || x.Status == "Approved"))
            .ToListAsync(cancellationToken);
        if (requests.Count == 0)
            return;

        var invalidationReason = string.IsNullOrWhiteSpace(reason)
            ? "The source journal changed after the budget override was evaluated."
            : reason.Trim();
        foreach (var request in requests.Where(x => x.Status == "PendingApproval"))
        {
            var cancelled = await _workflow.CancelWorkflowAsync(OverrideWorkflowType, request.Id, invalidationReason);
            if (!cancelled.Success)
                throw new InvalidOperationException(cancelled.Message ?? "The stale Finance budget override workflow could not be cancelled.");
        }

        var now = DateTime.UtcNow;
        foreach (var request in requests)
        {
            request.Status = "Superseded";
            request.RejectionReason = invalidationReason;
            request.UpdatedAt = now;
            request.LastModifiedById = UserId == Guid.Empty ? null : UserId;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<FinanceBudgetControlEvaluationDto> EvaluateManualJournalCoreAsync(
        Guid tenantId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        var journal = await _db.JournalEntries.AsNoTracking()
            .Include(x => x.FiscalPeriod)
            .Include(x => x.Transactions).ThenInclude(x => x.Account)
            .Include(x => x.Transactions).ThenInclude(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionDefinition)
            .Include(x => x.Transactions).ThenInclude(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionValue)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == journalEntryId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Manual journal was not found for this tenant.");
        if (journal.FiscalPeriod == null)
            throw new InvalidOperationException("The manual journal has no fiscal period.");

        var requested = journal.Transactions
            .Where(x => x.Account.AccountType == AccountType.Expense && x.Account.BudgetTrackingEnabled)
            .GroupBy(x => new { x.AccountId, x.FinanceDimensionSetId })
            .Select(group => new
            {
                AccountId = group.Key.AccountId,
                Account = group.First().Account,
                DimensionSet = group.First().FinanceDimensionSet,
                Amount = Math.Max(0m, group.Sum(x => x.DebitAmount - x.CreditAmount))
            })
            .Where(x => x.Amount > 0)
            .OrderBy(x => x.AccountId)
            .ToList();

        var result = new FinanceBudgetControlEvaluationDto
        {
            SourceDocumentId = journal.Id,
            EntryDate = journal.EntryDate.Date,
            HasTrackedExpenseLines = requested.Count > 0,
            IsAllowed = true
        };
        if (requested.Count == 0)
        {
            result.EvaluationHash = Hash($"{tenantId:N}|{journal.Id:N}|NO_TRACKED_EXPENSE");
            return result;
        }

        // Once a journal has posted, its consumed reservations are the immutable budget
        // decision evidence. Re-evaluating it against today's ledger would count the journal
        // as both an actual and a request, and later activity could rewrite its audit history.
        var postingSnapshot = await TryBuildPostingSnapshotAsync(
            tenantId,
            journal,
            cancellationToken);
        if (postingSnapshot != null)
            return postingSnapshot;

        result.CurrencyCode = await _db.FinanceSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => x.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance functional-currency settings are missing for this tenant.");
        result.CurrencyCode = result.CurrencyCode.Trim().ToUpperInvariant();
        if (result.CurrencyCode.Length != 3 || result.CurrencyCode.Any(x => x is < 'A' or > 'Z'))
            throw new InvalidOperationException("Finance functional-currency settings contain an invalid ISO currency code.");

        var scenario = await _db.BudgetScenarios.AsNoTracking()
            .Include(x => x.ControlDimensions)
                .ThenInclude(x => x.FinanceDimensionDefinition)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.FiscalYearId == journal.FiscalPeriod.FiscalYearId
                && x.AdoptedAt != null && x.AdoptionEffectiveDate != null
                && x.AdoptionEffectiveDate.Value.Date <= journal.EntryDate.Date
                && (x.Status == "Approved" || x.Status == "Superseded"))
            .OrderByDescending(x => x.AdoptionEffectiveDate)
            .ThenByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var accountIds = requested.Select(x => x.AccountId).ToList();
        var segmentRows = await _db.AccountSegmentValues.AsNoTracking()
            .Include(x => x.SegmentStructure)
            .Include(x => x.SegmentLookupValue)
            .Where(x => x.TenantId == tenantId && accountIds.Contains(x.AccountId) && !x.IsDeleted
                && x.EffectiveDate.Date <= journal.EntryDate.Date
                && (!x.EndDate.HasValue || x.EndDate.Value.Date >= journal.EntryDate.Date))
            .ToListAsync(cancellationToken);
        var entries = scenario == null
            ? new List<BudgetEntry>()
            : await _db.BudgetEntries.AsNoTracking()
                .Include(x => x.BudgetReturn).ThenInclude(x => x!.SegmentValue)
                .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                    .ThenInclude(x => x.FinanceDimensionDefinition)
                .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                    .ThenInclude(x => x.FinanceDimensionValue)
                .Where(x => x.TenantId == tenantId && !x.IsDeleted
                    && accountIds.Contains(x.AccountId)
                    && x.FiscalPeriodId == journal.FiscalPeriodId
                    && x.BudgetReturn!.BudgetScenarioId == scenario.Id
                    && x.BudgetReturn.Status == "Approved" && !x.BudgetReturn.IsDeleted)
                .ToListAsync(cancellationToken);

        foreach (var request in requested)
        {
            var controlSegments = segmentRows.Where(x => x.AccountId == request.AccountId && IsControllingSegment(x.SegmentStructure)).ToList();
            var matches = entries.Where(x => x.AccountId == request.AccountId)
                .Where(x => x.BudgetReturn!.SegmentValueId.HasValue
                    ? controlSegments.Any(s => s.SegmentLookupValueId == x.BudgetReturn.SegmentValueId)
                    : controlSegments.Count == 0)
                .Where(x => BudgetEntryMatchesTransactionSet(x, request.DimensionSet))
                .ToList();
            var line = new FinanceBudgetControlLineDto
            {
                AccountId = request.AccountId,
                AccountNumber = request.Account.AccountNumber,
                AccountName = request.Account.AccountName,
                FiscalPeriodId = journal.FiscalPeriodId,
                FiscalPeriodCode = journal.FiscalPeriod.PeriodCode,
                BudgetScenarioId = scenario?.Id,
                BudgetScenarioName = scenario?.Name,
                RequestedAmount = request.Amount
            };
            if (scenario == null)
            {
                line.DecisionCode = "NO_ADOPTED_BUDGET";
                line.Message = "No adopted Finance budget is effective for this journal date.";
            }
            else if (matches.Count == 0)
            {
                line.DecisionCode = "NO_MATCHING_BUDGET_LINE";
                line.Message = NoMatchingBudgetLineMessage(scenario, controlSegments);
            }
            else if (matches.Count > 1)
            {
                line.DecisionCode = "AMBIGUOUS_BUDGET_LINE";
                line.Message = "More than one approved budget line matches this account combination and period.";
            }
            else
            {
                var entry = matches[0];
                line.BudgetReturnId = entry.BudgetReturnId;
                line.BudgetEntryId = entry.Id;
                line.SegmentValueId = entry.BudgetReturn!.SegmentValueId;
                line.SegmentValue = entry.BudgetReturn.SegmentValue?.SegmentValue;
                line.FinanceDimensionSetId = entry.FinanceDimensionSetId;
                line.DimensionCombinationHash = entry.FinanceDimensionSet?.CombinationHash;
                line.DimensionAssignments = MapAssignments(entry);
                line.BudgetAmount = entry.AmountBase;
                line.PostedActualAmount = await PostedActualAsync(entry, tenantId, cancellationToken);
                line.ReservedAmount = await _db.FinanceBudgetReservations.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.BudgetEntryId == entry.Id
                        && x.Status == ReservedStatus
                        // The current document's own reservation is evidence, not another
                        // consumer. Excluding it keeps revalidation hash-stable through approval.
                        && x.SourceDocumentId != journalEntryId)
                    .SumAsync(x => x.ReservedAmount, cancellationToken);
                line.AvailableAmount = line.BudgetAmount - line.PostedActualAmount - line.ReservedAmount;
                line.ShortfallAmount = Math.Max(0m, line.RequestedAmount - line.AvailableAmount);
                line.DecisionCode = line.ShortfallAmount > 0 ? "INSUFFICIENT_BUDGET" : "AVAILABLE";
                line.Message = line.ShortfallAmount > 0
                    ? $"Available budget is short by {line.ShortfallAmount.ToString("0.00", CultureInfo.InvariantCulture)}."
                    : "Budget is available.";
            }
            result.Lines = result.Lines.Append(line).ToList();
        }

        result.TotalRequestedAmount = result.Lines.Sum(x => x.RequestedAmount);
        result.TotalShortfallAmount = result.Lines.Sum(x => x.ShortfallAmount);
        result.RequiresOverride = result.TotalShortfallAmount > 0;
        result.EvaluationHash = EvaluationHash(tenantId, journal, result.CurrencyCode, result.Lines);
        var matchingOverride = result.RequiresOverride
            ? await _db.FinanceBudgetOverrideRequests.AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted
                    && x.SourceDocumentType == ManualJournalSource && x.SourceDocumentId == journalEntryId
                    && x.EvaluationHash == result.EvaluationHash
                    && (x.Status == "PendingApproval" || x.Status == "Approved"))
                .OrderByDescending(x => x.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        result.OverrideStatus = matchingOverride?.Status;
        result.HasApprovedOverride = matchingOverride?.Status == "Approved" && matchingOverride.ApprovedAt != null;
        result.IsAllowed = result.Lines.All(x => x.DecisionCode == "AVAILABLE" || x.DecisionCode == "INSUFFICIENT_BUDGET")
            && (!result.RequiresOverride || result.HasApprovedOverride);
        return result;
    }

    private async Task<FinanceBudgetControlEvaluationDto?> TryBuildPostingSnapshotAsync(
        Guid tenantId,
        JournalEntry journal,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(journal.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase))
            return null;

        var reservations = await _db.FinanceBudgetReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == ManualJournalSource
                && x.SourceDocumentId == journal.Id
                && x.Status == ConsumedStatus)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        if (reservations.Count == 0)
            return null;

        var evaluationHashes = reservations.Select(x => x.EvaluationHash).Distinct(StringComparer.Ordinal).ToList();
        var currencyCodes = reservations.Select(x => x.CurrencyCode.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
        if (evaluationHashes.Count != 1
            || currencyCodes.Count != 1
            || reservations.Any(x => x.JournalEntryId != journal.Id
                || !x.PostingEventId.HasValue
                || !x.ConsumedAt.HasValue))
        {
            throw new InvalidOperationException("The posted journal's Finance budget evidence is incomplete or inconsistent.");
        }

        var entryIds = reservations.Select(x => x.BudgetEntryId).Distinct().ToList();
        var entries = await _db.BudgetEntries.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.FiscalPeriod)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.BudgetScenario)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.SegmentValue)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionDefinition)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionValue)
            .Where(x => x.TenantId == tenantId && entryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (entries.Count != entryIds.Count)
            throw new InvalidOperationException("The posted journal's Finance budget cell evidence could not be resolved.");

        var lines = reservations.Select(reservation =>
        {
            var entry = entries[reservation.BudgetEntryId];
            if (entry.Account == null || entry.FiscalPeriod == null || entry.BudgetReturn?.BudgetScenario == null)
                throw new InvalidOperationException("The posted journal's Finance budget cell evidence is incomplete.");
            var shortfall = Math.Max(0m, reservation.ReservedAmount - reservation.AvailableBeforeReservationSnapshot);
            return new FinanceBudgetControlLineDto
            {
                AccountId = reservation.AccountId,
                AccountNumber = entry.Account.AccountNumber,
                AccountName = entry.Account.AccountName,
                FiscalPeriodId = reservation.FiscalPeriodId,
                FiscalPeriodCode = entry.FiscalPeriod.PeriodCode,
                BudgetScenarioId = reservation.BudgetScenarioId,
                BudgetScenarioName = entry.BudgetReturn.BudgetScenario.Name,
                BudgetReturnId = reservation.BudgetReturnId,
                BudgetEntryId = reservation.BudgetEntryId,
                SegmentValueId = reservation.SegmentValueId,
                SegmentValue = entry.BudgetReturn.SegmentValue?.SegmentValue,
                FinanceDimensionSetId = reservation.FinanceDimensionSetId,
                DimensionCombinationHash = reservation.DimensionCombinationHashSnapshot,
                DimensionAssignments = MapAssignments(entry),
                RequestedAmount = reservation.ReservedAmount,
                BudgetAmount = reservation.BudgetAmountSnapshot,
                PostedActualAmount = reservation.PostedActualSnapshot,
                ReservedAmount = reservation.OtherReservationsSnapshot,
                AvailableAmount = reservation.AvailableBeforeReservationSnapshot,
                ShortfallAmount = shortfall,
                DecisionCode = shortfall > 0 ? "INSUFFICIENT_BUDGET" : "AVAILABLE",
                Message = shortfall > 0
                    ? $"Available budget was short by {shortfall.ToString("0.00", CultureInfo.InvariantCulture)} at approval."
                    : "Budget was available at approval."
            };
        }).ToList();

        var evaluationHash = evaluationHashes[0];
        var matchingOverride = await _db.FinanceBudgetOverrideRequests.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == ManualJournalSource
                && x.SourceDocumentId == journal.Id
                && x.EvaluationHash == evaluationHash
                && (x.Status == "PendingApproval" || x.Status == "Approved"))
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var requiresOverride = lines.Sum(x => x.ShortfallAmount) > 0;
        var hasApprovedOverride = matchingOverride?.Status == "Approved" && matchingOverride.ApprovedAt.HasValue;

        return new FinanceBudgetControlEvaluationDto
        {
            SourceDocumentId = journal.Id,
            EntryDate = journal.EntryDate.Date,
            CurrencyCode = currencyCodes[0],
            EvaluationHash = evaluationHash,
            HasTrackedExpenseLines = true,
            IsPostingSnapshot = true,
            IsAllowed = !requiresOverride || hasApprovedOverride,
            RequiresOverride = requiresOverride,
            HasApprovedOverride = hasApprovedOverride,
            OverrideStatus = matchingOverride?.Status,
            TotalRequestedAmount = lines.Sum(x => x.RequestedAmount),
            TotalShortfallAmount = lines.Sum(x => x.ShortfallAmount),
            Lines = lines
        };
    }

    private async Task<FinanceBudgetOverrideRequest?> ApprovedOverrideAsync(Guid tenantId, Guid journalEntryId, string hash, CancellationToken cancellationToken) =>
        await _db.FinanceBudgetOverrideRequests.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted
            && x.SourceDocumentType == ManualJournalSource && x.SourceDocumentId == journalEntryId
            && x.EvaluationHash == hash && x.Status == "Approved" && x.ApprovedAt != null, cancellationToken);

    private async Task EnsureDraftManualJournalSourceAsync(
        Guid tenantId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        var status = await _db.JournalEntries.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == journalEntryId && !x.IsDeleted)
            .Select(x => x.PostingStatus)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Manual journal was not found for this tenant.");
        if (!string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Finance budget reservation and override requests can only be created for Draft manual journals.");
        if (await _db.JournalBatchItems.AsNoTracking().AnyAsync(
                x => x.TenantId == tenantId && x.JournalEntryId == journalEntryId && !x.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException("Journal-batch items use the governed batch approval lifecycle and cannot request a manual-journal budget override.");
    }

    private static bool IsControllingSegment(AccountSegmentStructure structure)
    {
        if (structure.IsNaturalAccount || !structure.IsReportingDimension)
            return false;
        var key = $"{structure.SegmentCode} {structure.SegmentName}".Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return key.Contains("DEPARTMENT", StringComparison.OrdinalIgnoreCase)
            || key.Contains("DEPT", StringComparison.OrdinalIgnoreCase)
            || key.Contains("COSTCENTRE", StringComparison.OrdinalIgnoreCase)
            || key.Contains("COSTCENTER", StringComparison.OrdinalIgnoreCase);
    }

    private static bool BudgetEntryMatchesTransactionSet(
        BudgetEntry entry,
        FinanceDimensionSet? transactionSet)
    {
        if (entry.FinanceDimensionSet is null)
            return true;
        if (transactionSet is null)
            return false;
        return entry.FinanceDimensionSet.Items.All(required => transactionSet.Items.Any(actual =>
            actual.FinanceDimensionDefinitionId == required.FinanceDimensionDefinitionId
            && actual.FinanceDimensionValueId == required.FinanceDimensionValueId));
    }

    private async Task<decimal> PostedActualAsync(
        BudgetEntry entry,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var primaryBook = await BudgetPrimaryBookResolver.ResolveAsync(
            _db, tenantId, cancellationToken);
        var query = _db.AccountTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.AccountingBookId == primaryBook.Id
                && x.AccountId == entry.AccountId && x.FiscalPeriodId == entry.FiscalPeriodId
                && x.JournalEntry.PostingStatus == "Posted" && !x.JournalEntry.IsDeleted);
        if (entry.FinanceDimensionSet is not null)
        {
            var requiredValueIds = entry.FinanceDimensionSet.Items
                .Select(item => item.FinanceDimensionValueId).ToArray();
            var matchingSetIds = _db.FinanceDimensionSetItems.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && requiredValueIds.Contains(item.FinanceDimensionValueId))
                .GroupBy(item => item.FinanceDimensionSetId)
                .Where(group => group.Count() == requiredValueIds.Length)
                .Select(group => group.Key);
            query = query.Where(transaction => transaction.FinanceDimensionSetId.HasValue
                && matchingSetIds.Contains(transaction.FinanceDimensionSetId.Value));
        }
        return await query.SumAsync(
            transaction => transaction.DebitAmount - transaction.CreditAmount,
            cancellationToken);
    }

    private static IReadOnlyList<BudgetDimensionAssignmentDto> MapAssignments(BudgetEntry entry) =>
        entry.FinanceDimensionSet?.Items
            .OrderBy(item => item.FinanceDimensionDefinition.DisplayOrder)
            .ThenBy(item => item.DimensionCodeSnapshot)
            .Select(item => new BudgetDimensionAssignmentDto
            {
                FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                FinanceDimensionValueId = item.FinanceDimensionValueId,
                DimensionCode = item.DimensionCodeSnapshot,
                DimensionName = item.FinanceDimensionDefinition.Name,
                ValueCode = item.DimensionValueCodeSnapshot,
                ValueName = item.DimensionValueNameSnapshot
            }).ToList() ?? new List<BudgetDimensionAssignmentDto>();

    /// <summary>
    /// Names the adopted scenario's actual controlled grain in operator-facing diagnostics.
    /// Legacy segmented-account budgets fall back to their configured segment names; this
    /// avoids implying that every tenant controls only Department and Cost Centre.
    /// </summary>
    private static string NoMatchingBudgetLineMessage(
        BudgetScenario scenario,
        IReadOnlyCollection<AccountSegmentValue> controlSegments)
    {
        var dimensionNames = scenario.ControlDimensions
            .Where(item => !item.IsDeleted
                && item.FinanceDimensionDefinition is { IsDeleted: false })
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.FinanceDimensionDefinition.Code)
            .Select(item => item.FinanceDimensionDefinition.Name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dimensionNames.Count == 0)
        {
            dimensionNames = controlSegments
                .Where(segment => segment.SegmentStructure is not null)
                .OrderBy(segment => segment.SegmentStructure.SegmentPosition)
                .Select(segment => segment.SegmentStructure.SegmentName.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return dimensionNames.Count > 0
            ? $"No approved budget line matches this account's {string.Join(" + ", dimensionNames)} budget combination for this fiscal period."
            : "No approved budget line exists for this account and fiscal period.";
    }

    private static string EvaluationHash(
        Guid tenantId,
        JournalEntry journal,
        string currencyCode,
        IEnumerable<FinanceBudgetControlLineDto> lines)
    {
        var cells = lines
            .OrderBy(x => x.AccountId)
            .ThenBy(x => x.DimensionCombinationHash, StringComparer.Ordinal)
            .Select(x => string.Join('|',
            x.AccountId.ToString("N"), x.FiscalPeriodId.ToString("N"), x.BudgetEntryId?.ToString("N") ?? "NONE",
            x.DimensionCombinationHash ?? "NONE",
            x.RequestedAmount.ToString("0.00", CultureInfo.InvariantCulture), x.BudgetAmount.ToString("0.00", CultureInfo.InvariantCulture),
            x.PostedActualAmount.ToString("0.00", CultureInfo.InvariantCulture), x.ReservedAmount.ToString("0.00", CultureInfo.InvariantCulture), x.DecisionCode));
        return Hash($"{tenantId:N}|{journal.Id:N}|{journal.EntryDate:yyyyMMdd}|{currencyCode}|{string.Join(";", cells)}");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void EnsureAllowed(FinanceBudgetControlEvaluationDto evaluation)
    {
        if (evaluation.IsAllowed)
            return;
        var reasons = string.Join(" ", evaluation.Lines.Where(x => x.DecisionCode != "AVAILABLE").Select(x => $"{x.AccountNumber}: {x.Message}"));
        throw new InvalidOperationException($"Finance budget control blocked this journal. {reasons}".Trim());
    }

    private static void EnsureReservationsMatchEvaluation(
        IReadOnlyCollection<FinanceBudgetReservation> reservations,
        FinanceBudgetControlEvaluationDto evaluation)
    {
        var controlledLines = evaluation.Lines.Where(x => x.BudgetEntryId.HasValue).ToList();
        if (reservations.Count != controlledLines.Count)
            throw new InvalidOperationException("The journal changed after its Finance budget was reserved. Withdraw and resubmit it.");
        foreach (var line in controlledLines)
        {
            var reservation = reservations.SingleOrDefault(x => x.BudgetEntryId == line.BudgetEntryId);
            if (reservation == null
                || reservation.AccountId != line.AccountId
                || reservation.FiscalPeriodId != line.FiscalPeriodId
                || reservation.BudgetScenarioId != line.BudgetScenarioId
                || reservation.BudgetReturnId != line.BudgetReturnId
                || reservation.FinanceDimensionSetId != line.FinanceDimensionSetId
                || reservation.DimensionCombinationHashSnapshot != line.DimensionCombinationHash
                || reservation.ReservedAmount != line.RequestedAmount)
                throw new InvalidOperationException("The journal or adopted Finance budget changed after reservation. Withdraw and resubmit it.");
        }
    }

    private async Task AcquireReservationLockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsSqlServer())
            return;
        // Tenant-wide serialization is deliberately conservative for the foundation slice:
        // every evaluation can span several budget cells, so locking only one journal key
        // would still let two documents reserve the same cell concurrently.
        var resource = $"FINANCE:BUDGET:RESERVATION:{tenantId:N}";
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
IF @result < 0 THROW 51000, 'Unable to acquire Finance budget reservation lock.', 1;", cancellationToken);
    }

    private static FinanceBudgetOverrideRequestDto MapOverride(FinanceBudgetOverrideRequest request) => new()
    {
        Id = request.Id,
        SourceDocumentId = request.SourceDocumentId,
        EvaluationHash = request.EvaluationHash,
        Reason = request.Reason,
        RequestedAmount = request.RequestedAmount,
        CurrencyCode = request.CurrencyCode,
        ShortfallAmount = request.ShortfallAmount,
        Status = request.Status,
        WorkflowInstanceId = request.WorkflowInstanceId,
        RequestedByUserId = request.RequestedByUserId,
        RequestedAt = request.RequestedAt,
        ApprovedByUserId = request.ApprovedByUserId,
        ApprovedAt = request.ApprovedAt
    };
}
