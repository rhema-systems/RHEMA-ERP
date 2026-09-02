using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Claims and posts only exact reversals already authorised with a posted
/// occurrence. Template lifecycle state is intentionally irrelevant after the
/// occurrence has posted.
/// </summary>
public sealed class RecurringJournalReversalProcessor
{
    public const string SystemActor = "RecurringJournalScheduler";
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(30);
    private readonly ApplicationDbContext _db;
    private readonly IFinanceSystemPostingEngine _posting;
    private readonly ILogger<RecurringJournalReversalProcessor> _logger;

    public RecurringJournalReversalProcessor(ApplicationDbContext db, IFinanceSystemPostingEngine posting,
        ILogger<RecurringJournalReversalProcessor> logger)
    {
        _db = db;
        _posting = posting;
        _logger = logger;
    }

    public async Task<RecurringJournalReversalProcessingResultDto> ProcessAllAsync(
        DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var tenantIds = await Eligible().Where(item => item.ReversalDueDate <= asOfDate)
            .Select(item => item.TenantId).Distinct().ToListAsync(cancellationToken);
        var total = new RecurringJournalReversalProcessingResultDto();
        foreach (var tenantId in tenantIds)
            Add(total, await ProcessTenantAsync(tenantId, asOfDate, null, cancellationToken));
        return total;
    }

    public async Task<RecurringJournalReversalProcessingResultDto> ProcessTenantAsync(
        Guid tenantId, DateOnly asOfDate, Guid? occurrenceId = null, CancellationToken cancellationToken = default)
    {
        if (occurrenceId.HasValue)
        {
            var dueDate = await _db.RecurringJournalOccurrences.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == occurrenceId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => item.ReversalDueDate).SingleOrDefaultAsync(cancellationToken);
            if (!dueDate.HasValue)
                throw new InvalidOperationException("The occurrence has no scheduled automatic reversal.");
            if (dueDate.Value > asOfDate)
                throw new InvalidOperationException("A future recurring-journal reversal cannot be processed early.");
        }

        var result = new RecurringJournalReversalProcessingResultDto();
        var candidateIds = await Eligible()
            .Where(item => item.TenantId == tenantId && item.ReversalDueDate <= asOfDate &&
                (!occurrenceId.HasValue || item.Id == occurrenceId.Value))
            .OrderBy(item => item.ReversalDueDate).ThenBy(item => item.Id)
            .Select(item => item.Id).Take(250).ToListAsync(cancellationToken);
        result.CandidateCount = candidateIds.Count;

        foreach (var id in candidateIds)
        {
            var now = DateTime.UtcNow;
            var staleBefore = now.Subtract(StaleClaimAge);
            var claimQuery = _db.RecurringJournalOccurrences.IgnoreQueryFilters()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted &&
                    item.Status == RecurringJournalOccurrenceStatus.Posted && item.JournalEntryId.HasValue &&
                    item.ReversalDueDate.HasValue && item.ReversalDueDate.Value <= asOfDate &&
                    item.ReversalAuthorizedAt.HasValue && item.ReversalAuthorizedByUserId.HasValue &&
                    !item.ReversalJournalEntryId.HasValue && !item.ReversedAt.HasValue &&
                    (item.ReversalStatus == RecurringJournalReversalStatus.Scheduled ||
                     item.ReversalStatus == RecurringJournalReversalStatus.Failed ||
                     (item.ReversalStatus == RecurringJournalReversalStatus.Processing &&
                      item.ReversalLastAttemptAt < staleBefore)));
            var claimed = _db.Database.IsRelational()
                ? await claimQuery.ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.ReversalStatus, RecurringJournalReversalStatus.Processing)
                    .SetProperty(item => item.ReversalAttemptCount, item => item.ReversalAttemptCount + 1)
                    .SetProperty(item => item.ReversalLastAttemptAt, now)
                    .SetProperty(item => item.ReversalError, (string?)null)
                    .SetProperty(item => item.ReversalProcessedBy, SystemActor)
                    .SetProperty(item => item.UpdatedAt, now)
                    .SetProperty(item => item.UpdatedBy, SystemActor), cancellationToken)
                : await ClaimForNonRelationalTestsAsync(claimQuery, now, cancellationToken);
            if (claimed == 0)
            {
                result.ExistingCount++;
                continue;
            }

            try
            {
                _db.ChangeTracker.Clear();
                var occurrence = await _db.RecurringJournalOccurrences.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken);
                var originalEvent = await _db.FinancePostingEvents.IgnoreQueryFilters().AsNoTracking()
                    .Where(item => item.TenantId == tenantId && item.JournalEntryId == occurrence.JournalEntryId &&
                        item.PostingStatus == "Posted" && item.SourceDocumentId == occurrence.Id &&
                        item.SourceDocumentType == nameof(RecurringJournalOccurrence))
                    .OrderByDescending(item => item.PostedAt).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The posted recurring occurrence has no authoritative Finance posting event.");

                var reversalDate = occurrence.ReversalDueDate!.Value.ToDateTime(TimeOnly.MinValue);
                const string reason = "Exact automatic reversal authorised with the recurring-journal occurrence.";
                var plan = await _posting.GetReversalPlanAsync(
                    tenantId, originalEvent.Id, reason, reversalDate, SystemActor, cancellationToken);
                if (!plan.IsDefined || plan.OriginalJournalEntryId != occurrence.JournalEntryId)
                    throw new InvalidOperationException("The central posting engine did not return the expected immutable reversal plan.");

                var posted = await _posting.PostAsync(tenantId, new FinancePostingRequestDto
                {
                    SourceModule = "GL",
                    OriginModuleCode = "FIN",
                    SourceDocumentType = nameof(RecurringJournalOccurrence),
                    SourceDocumentId = occurrence.Id,
                    SourceDocumentTenantId = tenantId,
                    ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                    ReversalReason = plan.Reason,
                    ReversalType = "Automatic",
                    PostingAction = "AutoReverseRecurringJournal",
                    SourceDocumentReference = $"{originalEvent.SourceDocumentReference}-REV",
                    Description = $"Automatic reversal of recurring journal occurrence {occurrence.Id}",
                    PostingDate = reversalDate,
                    JournalType = "Recurring Reversal",
                    BookClassification = originalEvent.BookClassification,
                    FunctionalCurrencyCode = originalEvent.FunctionalCurrencyCode,
                    IdempotencyKey = $"RecurringJournal:{tenantId:N}:{occurrence.Id:N}:AutoReverse",
                    ReturnExistingOnDuplicate = true,
                    PreserveHistoricalExchangeRateSnapshot = true,
                    Lines = plan.ReversalLines
                }, SystemActor, cancellationToken);

                _db.ChangeTracker.Clear();
                var completedAt = DateTime.UtcNow;
                var linkQuery = _db.RecurringJournalOccurrences.IgnoreQueryFilters()
                    .Where(item => item.Id == id && item.TenantId == tenantId &&
                        !item.ReversalJournalEntryId.HasValue && !item.ReversedAt.HasValue);
                var linked = _db.Database.IsRelational()
                    ? await linkQuery.ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.ReversalStatus, RecurringJournalReversalStatus.Posted)
                        .SetProperty(item => item.ReversalJournalEntryId, posted.JournalEntryId)
                        .SetProperty(item => item.ReversalPostingEventId, posted.PostingEventId)
                        .SetProperty(item => item.ReversedAt, completedAt)
                        .SetProperty(item => item.ReversalError, (string?)null)
                        .SetProperty(item => item.ReversalProcessedBy, SystemActor)
                        .SetProperty(item => item.UpdatedAt, completedAt)
                        .SetProperty(item => item.UpdatedBy, SystemActor), cancellationToken)
                    : await LinkForNonRelationalTestsAsync(linkQuery, posted, completedAt, cancellationToken);
                if (linked == 0)
                    result.ExistingCount++;
                else if (posted.WasDuplicate)
                    result.ExistingCount++;
                else
                    result.PostedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _db.ChangeTracker.Clear();
                result.FailedCount++;
                var error = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
                var failureQuery = _db.RecurringJournalOccurrences.IgnoreQueryFilters()
                    .Where(item => item.Id == id && item.TenantId == tenantId &&
                        !item.ReversalJournalEntryId.HasValue && !item.ReversedAt.HasValue);
                if (_db.Database.IsRelational())
                    await failureQuery.ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.ReversalStatus, RecurringJournalReversalStatus.Failed)
                        .SetProperty(item => item.ReversalError, error)
                        .SetProperty(item => item.ReversalProcessedBy, SystemActor)
                        .SetProperty(item => item.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(item => item.UpdatedBy, SystemActor), cancellationToken);
                else
                    await FailForNonRelationalTestsAsync(failureQuery, error, cancellationToken);
                _logger.LogError(exception, "Automatic recurring-journal reversal failed for tenant {TenantId}, occurrence {OccurrenceId}.", tenantId, id);
            }
        }

        return result;
    }

    private IQueryable<RecurringJournalOccurrence> Eligible()
    {
        var staleBefore = DateTime.UtcNow.Subtract(StaleClaimAge);
        return _db.RecurringJournalOccurrences.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status == RecurringJournalOccurrenceStatus.Posted &&
                item.JournalEntryId.HasValue && item.ReversalDueDate.HasValue &&
                item.ReversalAuthorizedAt.HasValue && item.ReversalAuthorizedByUserId.HasValue &&
                !item.ReversalJournalEntryId.HasValue && !item.ReversedAt.HasValue &&
                (item.ReversalStatus == RecurringJournalReversalStatus.Scheduled ||
                 item.ReversalStatus == RecurringJournalReversalStatus.Failed ||
                 (item.ReversalStatus == RecurringJournalReversalStatus.Processing && item.ReversalLastAttemptAt < staleBefore)));
    }

    private async Task<int> ClaimForNonRelationalTestsAsync(
        IQueryable<RecurringJournalOccurrence> query, DateTime now, CancellationToken cancellationToken)
    {
        var item = await query.SingleOrDefaultAsync(cancellationToken);
        if (item is null) return 0;
        item.ReversalStatus = RecurringJournalReversalStatus.Processing;
        item.ReversalAttemptCount++;
        item.ReversalLastAttemptAt = now;
        item.ReversalError = null;
        item.ReversalProcessedBy = SystemActor;
        item.UpdatedAt = now;
        item.UpdatedBy = SystemActor;
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task<int> LinkForNonRelationalTestsAsync(IQueryable<RecurringJournalOccurrence> query,
        FinancePostingResultDto posted, DateTime completedAt, CancellationToken cancellationToken)
    {
        var item = await query.SingleOrDefaultAsync(cancellationToken);
        if (item is null) return 0;
        item.ReversalStatus = RecurringJournalReversalStatus.Posted;
        item.ReversalJournalEntryId = posted.JournalEntryId;
        item.ReversalPostingEventId = posted.PostingEventId;
        item.ReversedAt = completedAt;
        item.ReversalError = null;
        item.ReversalProcessedBy = SystemActor;
        item.UpdatedAt = completedAt;
        item.UpdatedBy = SystemActor;
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task FailForNonRelationalTestsAsync(IQueryable<RecurringJournalOccurrence> query,
        string error, CancellationToken cancellationToken)
    {
        var item = await query.SingleOrDefaultAsync(cancellationToken);
        if (item is null) return;
        item.ReversalStatus = RecurringJournalReversalStatus.Failed;
        item.ReversalError = error;
        item.ReversalProcessedBy = SystemActor;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = SystemActor;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void Add(RecurringJournalReversalProcessingResultDto total, RecurringJournalReversalProcessingResultDto item)
    {
        total.CandidateCount += item.CandidateCount;
        total.PostedCount += item.PostedCount;
        total.ExistingCount += item.ExistingCount;
        total.FailedCount += item.FailedCount;
    }
}
