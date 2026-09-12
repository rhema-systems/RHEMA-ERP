using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Middleware;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class JournalBatchService : IJournalBatchService
{
    public const string WorkflowEntityType = "JournalBatch";

    private static readonly JsonSerializerOptions FingerprintJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IJournalEntryService _journalEntries;
    private readonly IDocumentNumberingService _numbering;
    private readonly IWorkflowService _workflow;
    private readonly IWorkflowIntegrationService _workflowIntegration;
    private readonly IFinanceAuditService? _audit;
    private readonly ILogger<JournalBatchService> _logger;

    public JournalBatchService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IJournalEntryService journalEntries,
        IDocumentNumberingService numbering,
        IWorkflowService workflow,
        ILogger<JournalBatchService> logger,
        IFinanceAuditService? audit = null,
        IWorkflowIntegrationService? workflowIntegration = null)
    {
        _context = context;
        _currentUser = currentUser;
        _journalEntries = journalEntries;
        _numbering = numbering;
        _workflow = workflow;
        _logger = logger;
        _audit = audit;
        _workflowIntegration = workflowIntegration ?? new ErpSystem.Core.Services.Workflow.WorkflowIntegrationService(
            workflow, Microsoft.Extensions.Logging.Abstractions.NullLogger<ErpSystem.Core.Services.Workflow.WorkflowIntegrationService>.Instance);
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    private Guid UserId
    {
        get
        {
            if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId == Guid.Empty)
                throw new InvalidOperationException("An authenticated user is required.");
            return userId;
        }
    }

    public async Task<JournalBatchListResultDto> GetAsync(
        JournalBatchQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var source = _context.JournalBatches
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (query.ApprovalStatus.HasValue)
            source = source.Where(x => x.ApprovalStatus == query.ApprovalStatus.Value);
        if (query.PostingStatus.HasValue)
            source = source.Where(x => x.PostingStatus == query.PostingStatus.Value);
        if (query.FiscalPeriodId.HasValue)
            source = source.Where(x => x.FiscalPeriodId == query.FiscalPeriodId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.BatchNumber.Contains(search) || x.Description.Contains(search));
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var batches = await source
            .OrderByDescending(x => x.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(x => x.FiscalPeriod)
            .Include(x => x.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.JournalEntry)
                    .ThenInclude(journal => journal.Transactions)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new JournalBatchListResultDto
        {
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            Items = batches.Select(MapListItem).ToList()
        };
    }

    public async Task<JournalBatchDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(id, asTracking: false, cancellationToken);
        return batch == null ? null : MapDetail(batch);
    }

    public async Task<JournalBatchDetailDto> CreateAsync(
        CreateJournalBatchDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        await EnsureOpenPeriodAsync(dto.FiscalPeriodId, tenantId, cancellationToken);
        await EnsureControlCurrencyAsync(dto.ControlCurrencyCode, tenantId, cancellationToken);

        var now = DateTime.UtcNow;
        var batch = new JournalBatch
        {
            TenantId = tenantId,
            BatchNumber = await _numbering.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.JournalBatch,
                tenantId,
                now,
                nameof(JournalBatch),
                cancellationToken: cancellationToken),
            Description = dto.Description.Trim(),
            FiscalPeriodId = dto.FiscalPeriodId,
            BookClassification = dto.BookClassification.Trim().ToUpperInvariant(),
            ControlCurrencyCode = dto.ControlCurrencyCode.Trim().ToUpperInvariant(),
            ExpectedDebitTotal = decimal.Round(dto.ExpectedDebitTotal, 2),
            ExpectedJournalCount = dto.ExpectedJournalCount,
            Notes = dto.Notes?.Trim(),
            CreatedAt = now,
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId
        };

        _context.JournalBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("JournalBatchCreated", batch, cancellationToken);
        return await RequireDetailAsync(batch.Id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> UpdateAsync(
        Guid id,
        UpdateJournalBatchDto dto,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        ApplyRowVersion(batch, dto.RowVersion);

        batch.Description = dto.Description.Trim();
        batch.ExpectedDebitTotal = decimal.Round(dto.ExpectedDebitTotal, 2);
        batch.ExpectedJournalCount = dto.ExpectedJournalCount;
        batch.Notes = dto.Notes?.Trim();
        StampModified(batch);

        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("JournalBatchUpdated", batch, cancellationToken);
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        if (await _context.JournalBatchItems.AnyAsync(
                x => x.TenantId == TenantId && x.JournalBatchId == id && !x.IsDeleted,
                cancellationToken))
        {
            throw new InvalidOperationException("Only an empty Draft journal batch can be deleted.");
        }

        batch.IsDeleted = true;
        batch.DeletedAt = DateTime.UtcNow;
        batch.DeletedBy = _currentUser.UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("JournalBatchDeleted", batch, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> AddExistingJournalAsync(
        Guid id,
        Guid journalEntryId,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        var journal = await _context.JournalEntries
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(
                x => x.TenantId == TenantId && x.Id == journalEntryId && !x.IsDeleted,
                cancellationToken)
            ?? throw new ArgumentException("Journal entry was not found for this tenant.");

        await EnsureEligibleJournalAsync(batch, journal, cancellationToken);
        var nextSequence = await NextSequenceAsync(batch.Id, cancellationToken);
        var item = new JournalBatchItem
        {
            TenantId = TenantId,
            JournalBatchId = batch.Id,
            JournalEntryId = journal.Id,
            SequenceNumber = nextSequence,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId
        };
        _context.JournalBatchItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            "JournalBatchEntryAdded",
            batch,
            cancellationToken,
            new
            {
                ItemId = item.Id,
                JournalEntryId = journal.Id,
                journal.JournalEntryNumber
            });
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> CreateJournalAsync(
        Guid id,
        CreateJournalEntryDto dto,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        dto.FiscalPeriodId = batch.FiscalPeriodId;
        dto.BookClassification = batch.BookClassification;
        dto.SourceModule = "GL";

        var journal = await _journalEntries.CreateJournalEntryAsync(dto, cancellationToken);
        return await AddExistingJournalAsync(id, journal.Id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> UpdateJournalAsync(
        Guid id,
        Guid journalEntryId,
        UpdateJournalEntryDto dto,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        await RequireBatchItemAsync(id, journalEntryId, cancellationToken);
        await _journalEntries.UpdateJournalEntryAsync(journalEntryId, dto, cancellationToken);
        await AuditAsync("JournalBatchEntryUpdated", batch, cancellationToken, new { journalEntryId });
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> RemoveJournalAsync(
        Guid id,
        Guid journalEntryId,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        var item = await RequireBatchItemAsync(id, journalEntryId, cancellationToken);
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = _currentUser.UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("JournalBatchEntryRemoved", batch, cancellationToken, new { item.Id, journalEntryId });
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task<JournalBatchValidationResultDto> ValidateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(id, asTracking: false, cancellationToken)
            ?? throw new ArgumentException("Journal batch was not found for this tenant.");
        var issues = new List<JournalBatchValidationIssueDto>();
        var items = ActiveItems(batch);
        var debit = Money(items.Sum(x => x.JournalEntry.TotalDebitAmount));
        var credit = Money(items.Sum(x => x.JournalEntry.TotalCreditAmount));
        var lineCount = items.Sum(x => x.JournalEntry.Transactions.Count(t => !t.IsDeleted));

        if (items.Count == 0)
            issues.Add(Issue("EMPTY_BATCH", "A journal batch must contain at least one journal entry."));
        if (debit != credit)
            issues.Add(Issue("BATCH_UNBALANCED", $"Batch debit {debit:N2} does not equal credit {credit:N2}."));
        if (debit != Money(batch.ExpectedDebitTotal))
            issues.Add(Issue("CONTROL_TOTAL_MISMATCH", $"Expected debit {batch.ExpectedDebitTotal:N2} does not equal actual debit {debit:N2}."));
        if (batch.ExpectedJournalCount.HasValue && batch.ExpectedJournalCount.Value != items.Count)
            issues.Add(Issue("EXPECTED_COUNT_MISMATCH", $"Expected {batch.ExpectedJournalCount.Value} journal entries but found {items.Count}."));

        var period = await _context.FiscalPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == batch.FiscalPeriodId && !x.IsDeleted, cancellationToken);
        if (period == null || !period.IsOpen || period.IsClosed || period.IsLocked)
            issues.Add(Issue("PERIOD_NOT_OPEN", "The batch fiscal period is not open and unlocked."));

        var eligibleReversalSourceIds = new HashSet<Guid>();
        if (batch.BatchType == JournalBatchType.Reversal && batch.ReversalOfJournalBatchId.HasValue)
        {
            eligibleReversalSourceIds = (await _context.JournalBatchItems
                    .AsNoTracking()
                    .Where(x => x.TenantId == TenantId &&
                                x.JournalBatchId == batch.ReversalOfJournalBatchId.Value &&
                                x.PostingStatus == JournalBatchItemPostingStatus.Posted &&
                                !x.IsDeleted)
                    .Select(x => x.JournalEntryId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        foreach (var item in items)
        {
            var journal = item.JournalEntry;
            var isEligible = batch.BatchType == JournalBatchType.Reversal
                ? IsGeneratedBatchReversalJournal(batch, journal, eligibleReversalSourceIds)
                : IsManualJournal(journal);
            if (!isEligible)
            {
                var message = batch.BatchType == JournalBatchType.Reversal
                    ? $"{journal.JournalEntryNumber} is not a valid generated reversal of a posted entry in the source batch."
                    : $"{journal.JournalEntryNumber} is not an eligible manual GL journal.";
                issues.Add(Issue(
                    batch.BatchType == JournalBatchType.Reversal ? "INVALID_BATCH_REVERSAL" : "NOT_MANUAL_GL",
                    message,
                    item));
                continue;
            }

            if (journal.FiscalPeriodId != batch.FiscalPeriodId)
                issues.Add(Issue("PERIOD_MISMATCH", $"{journal.JournalEntryNumber} belongs to another fiscal period.", item));
            if (!string.Equals(journal.BookClassification, batch.BookClassification, StringComparison.OrdinalIgnoreCase))
                issues.Add(Issue("BOOK_MISMATCH", $"{journal.JournalEntryNumber} belongs to another accounting book.", item));

            try
            {
                await _journalEntries.ValidateJournalEntryReadyForSubmissionAsync(journal.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                issues.Add(Issue("JOURNAL_INVALID", ex.Message, item));
            }
        }

        return new JournalBatchValidationResultDto
        {
            IsValid = issues.All(x => !string.Equals(x.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            ExpectedDebitTotal = batch.ExpectedDebitTotal,
            ActualDebitTotal = debit,
            ActualCreditTotal = credit,
            Variance = debit - batch.ExpectedDebitTotal,
            EntryCount = items.Count,
            ExpectedJournalCount = batch.ExpectedJournalCount,
            LineCount = lineCount,
            Issues = issues
        };
    }

    public async Task<JournalBatchDetailDto> SubmitAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        var validation = await ValidateAsync(id, cancellationToken);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(" ", validation.Issues.Select(x => x.Message)));

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                batch = await RequireBatchAsync(id, cancellationToken);
                EnsureDraft(batch);
                validation = await ValidateAsync(id, cancellationToken);
                if (!validation.IsValid)
                    throw new InvalidOperationException(string.Join(" ", validation.Issues.Select(x => x.Message)));
                var requiresApproval = await _workflowIntegration.HasActiveApprovalInstanceAsync(WorkflowEntityType, id) ||
                    await _workflowIntegration.HasActiveApprovalWorkflowAsync(WorkflowEntityType);
                var items = await _context.JournalBatchItems
                    .Include(x => x.JournalEntry)
                        .ThenInclude(x => x.Transactions)
                    .Where(x => x.TenantId == TenantId && x.JournalBatchId == id && !x.IsDeleted)
                    .OrderBy(x => x.SequenceNumber)
                    .ToListAsync(cancellationToken);

                var now = DateTime.UtcNow;
                foreach (var item in items)
                {
                    item.SubmittedContentFingerprint = ComputeItemFingerprint(item);
                    if (requiresApproval)
                    {
                        item.ReviewStatus = JournalBatchItemReviewStatus.Pending;
                        item.PostingStatus = JournalBatchItemPostingStatus.NotEligible;
                        item.JournalEntry.PostingStatus = "Pending Approval";
                        item.JournalEntry.ApprovalStatus = "Pending";
                        item.JournalEntry.UpdatedAt = now;
                    }
                }

                batch.ContentFingerprint = ComputeBatchFingerprint(batch, items);
                batch.SubmittedDebitTotal = validation.ActualDebitTotal;
                batch.SubmittedCreditTotal = validation.ActualCreditTotal;
                batch.SubmittedJournalCount = validation.EntryCount;
                batch.SubmittedLineCount = validation.LineCount;
                batch.SubmittedByUserId = UserId;
                batch.SubmittedAt = now;
                batch.ApprovalStatus = requiresApproval ? JournalBatchApprovalStatus.PendingApproval : JournalBatchApprovalStatus.Draft;
                batch.PostingStatus = JournalBatchPostingStatus.NotReady;
                StampModified(batch);
                if (requiresApproval) await _context.SaveChangesAsync(cancellationToken);

                var outcome = await _workflowIntegration.SubmitAsync(WorkflowEntityType, id);
                var workflowResult = outcome.ExecutionResult;
                if (outcome.ApprovalRequired != requiresApproval)
                    throw new InvalidOperationException("Approval configuration changed during submission. Refresh the batch and retry.");
                if (!workflowResult.Success ||
                    (outcome.ApprovalRequired && !workflowResult.WorkflowInstanceId.HasValue) ||
                    (!outcome.ApprovalRequired && (outcome.Outcome != WorkflowOutcome.Approved || workflowResult.WorkflowInstanceId.HasValue)))
                    throw new InvalidOperationException(workflowResult.Message ?? "Approval configuration changed or journal batch submission failed. Refresh and retry.");

                batch.ApprovalRequired = outcome.ApprovalRequired;
                batch.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
                if (!outcome.ApprovalRequired)
                {
                    if (batch.ApprovedByUserId.HasValue || batch.ApprovedAt.HasValue || batch.ReviewCompletedAt.HasValue ||
                        items.Any(x => x.FinalReviewedByUserId.HasValue || x.FinalReviewedAt.HasValue ||
                            x.JournalEntry.ApprovedByUserId.HasValue || x.JournalEntry.ApprovedDate.HasValue ||
                            !string.IsNullOrWhiteSpace(x.JournalEntry.ApprovalWorkflowId)))
                        throw new InvalidOperationException("A directly prepared batch cannot replace retained approval history.");
                    batch.ApprovalStatus = JournalBatchApprovalStatus.ReadyToPost;
                    batch.PostingStatus = JournalBatchPostingStatus.Ready;
                    // Persist the mode before child journal guards verify their owning batch.
                    await _context.SaveChangesAsync(cancellationToken);
                    foreach (var item in items)
                    {
                        item.ReviewStatus = JournalBatchItemReviewStatus.NotRequired;
                        item.PostingStatus = JournalBatchItemPostingStatus.Ready;
                        item.JournalEntry.PostingStatus = "Approved"; // Canonical journal posting-ready status, not a human decision.
                        item.JournalEntry.ApprovalStatus = "Not Required";
                        item.JournalEntry.RequiresApproval = false;
                        item.JournalEntry.UpdatedAt = now;
                    }
                }
                else if (outcome.Outcome == WorkflowOutcome.Approved)
                {
                    var completed = await _context.WorkflowInstances.AsNoTracking().Include(x => x.EntityType)
                        .FirstOrDefaultAsync(x => x.Id == batch.WorkflowInstanceId &&
                        x.TenantId == TenantId && x.EntityId == id && !x.IsDeleted &&
                        x.Status == WorkflowInstanceStatus.Completed && x.CompletedDate != null &&
                        x.EntityType.TenantId == TenantId, cancellationToken);
                    static bool IsBatchEntity(string? value) => string.Equals(
                        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()), WorkflowEntityType, StringComparison.OrdinalIgnoreCase);
                    if (completed == null || (!IsBatchEntity(completed.EntityType.Code) && !IsBatchEntity(completed.EntityType.Name)))
                        throw new InvalidOperationException("Completed journal batch approval requires its retained completed workflow.");
                    batch.ApprovalStatus = JournalBatchApprovalStatus.Approved;
                    batch.PostingStatus = JournalBatchPostingStatus.Ready;
                    batch.ReviewCompletedAt = now;
                    foreach (var item in items)
                    {
                        item.ReviewStatus = JournalBatchItemReviewStatus.Approved;
                        item.PostingStatus = JournalBatchItemPostingStatus.Ready;
                        item.JournalEntry.PostingStatus = "Approved";
                        item.JournalEntry.ApprovalStatus = "Approved";
                    }
                }
                else if (outcome.Outcome != WorkflowOutcome.Pending)
                    throw new InvalidOperationException("The journal batch approval workflow did not accept this submission.");
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        });

        batch = await RequireBatchAsync(id, cancellationToken);
        await AuditAsync(batch.ApprovalRequired ? "JournalBatchSubmitted" : "JournalBatchPreparedForPosting", batch, cancellationToken);
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> WithdrawAsync(
        Guid id,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        if (batch.ApprovalStatus != JournalBatchApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Only a batch pending approval can be withdrawn.");
        if (await _context.JournalBatchItemReviews.AnyAsync(
                x => x.TenantId == TenantId && x.JournalBatchItem.JournalBatchId == id && !x.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException("The batch cannot be withdrawn after entry-level review has begun.");

        var result = await _workflow.CancelWorkflowAsync(
            WorkflowEntityType,
            id,
            string.IsNullOrWhiteSpace(reason) ? "Journal batch withdrawn." : reason.Trim());
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? "Unable to cancel the journal batch workflow.");

        var items = await _context.JournalBatchItems
            .Include(x => x.JournalEntry)
            .Where(x => x.TenantId == TenantId && x.JournalBatchId == id && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.ReviewStatus = JournalBatchItemReviewStatus.Pending;
            item.PostingStatus = JournalBatchItemPostingStatus.NotEligible;
            item.SubmittedContentFingerprint = null;
            item.JournalEntry.PostingStatus = batch.BatchType == JournalBatchType.Reversal
                ? "Cancelled"
                : "Draft";
            item.JournalEntry.ApprovalStatus = batch.BatchType == JournalBatchType.Reversal
                ? "Cancelled"
                : "Withdrawn";
        }

        batch.ApprovalStatus = batch.BatchType == JournalBatchType.Reversal
            ? JournalBatchApprovalStatus.Cancelled
            : JournalBatchApprovalStatus.Draft;
        batch.PostingStatus = JournalBatchPostingStatus.NotReady;
        batch.WorkflowInstanceId = null;
        batch.ContentFingerprint = null;
        batch.SubmittedAt = null;
        batch.SubmittedByUserId = null;
        batch.SubmittedDebitTotal = null;
        batch.SubmittedCreditTotal = null;
        batch.SubmittedJournalCount = null;
        batch.SubmittedLineCount = null;
        if (batch.BatchType == JournalBatchType.Reversal)
        {
            batch.IsVoided = true;
            batch.VoidedByUserId = UserId;
            batch.VoidedAt = DateTime.UtcNow;
            batch.VoidReason = string.IsNullOrWhiteSpace(reason)
                ? "Reversal workflow withdrawn."
                : reason.Trim();
            await ReleaseSourceReversalClaimAsync(batch, cancellationToken);
        }
        StampModified(batch);
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("JournalBatchWithdrawn", batch, cancellationToken, reason: reason);
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> ReviewStageAsync(
        Guid id,
        JournalBatchReviewStageDto dto,
        CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(id, asTracking: true, cancellationToken)
            ?? throw new ArgumentException("Journal batch was not found for this tenant.");
        if (batch.ApprovalStatus != JournalBatchApprovalStatus.PendingApproval || !batch.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("The journal batch is not pending approval.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, id, UserId))
            throw new UnauthorizedAccessException("This journal batch is assigned to another workflow approver.");

        var currentStep = await _workflow.GetCurrentWorkflowStepAsync(WorkflowEntityType, id)
            ?? throw new InvalidOperationException("The workflow has no active review stage.");
        var stageKey = $"{currentStep.StepOrder}:{currentStep.StepName}";
        var eligible = ActiveItems(batch)
            .Where(x => x.ReviewStatus == JournalBatchItemReviewStatus.Pending)
            .ToList();
        if (eligible.Count == 0)
            throw new InvalidOperationException("There are no entries awaiting a review decision.");

        var supplied = dto.Decisions
            .GroupBy(x => x.JournalBatchItemId)
            .ToDictionary(x => x.Key, x => x.Single());
        if (supplied.Count != eligible.Count || eligible.Any(x => !supplied.ContainsKey(x.Id)))
            throw new InvalidOperationException("A decision is required for every entry eligible at this review stage.");
        foreach (var decision in supplied.Values)
        {
            if (decision.Decision == JournalBatchReviewDecision.Rejected && string.IsNullOrWhiteSpace(decision.Comment))
                throw new InvalidOperationException("A reason is required for each rejected journal entry.");
        }

        if (batch.BatchType == JournalBatchType.Reversal &&
            supplied.Values.Select(x => x.Decision).Distinct().Count() != 1)
            throw new InvalidOperationException("A full reversal batch must be approved or rejected as a complete unit.");

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                foreach (var item in eligible)
                {
                    var decision = supplied[item.Id];
                    var review = new JournalBatchItemReview
                    {
                        TenantId = TenantId,
                        WorkflowInstanceId = batch.WorkflowInstanceId.Value,
                        WorkflowStepInstanceId = currentStep.Id,
                        WorkflowStageKey = stageKey,
                        Decision = decision.Decision,
                        Comment = decision.Comment?.Trim(),
                        DecidedByUserId = UserId,
                        DecidedAt = now,
                        CreatedAt = now,
                        CreatedBy = _currentUser.UserName,
                        CreatedById = UserId
                    };
                    item.Reviews.Add(review);
                    _context.JournalBatchItemReviews.Add(review);

                    if (decision.Decision == JournalBatchReviewDecision.Rejected)
                    {
                        item.ReviewStatus = JournalBatchItemReviewStatus.Rejected;
                        item.FinalReviewedByUserId = UserId;
                        item.FinalReviewedAt = now;
                        item.FinalRejectionReason = decision.Comment!.Trim();
                        item.PostingStatus = JournalBatchItemPostingStatus.NotEligible;
                        item.JournalEntry.PostingStatus = "Rejected";
                        item.JournalEntry.ApprovalStatus = "Rejected";
                        item.JournalEntry.RejectionReason = decision.Comment.Trim();
                    }
                }
                await _context.SaveChangesAsync(cancellationToken);

                var allRejected = ActiveItems(batch).All(x => x.ReviewStatus == JournalBatchItemReviewStatus.Rejected);
                var action = allRejected ? "Reject" : "Approve";
                var workflowResult = await _workflow.ProcessApprovalStepAsync(
                    WorkflowEntityType,
                    id,
                    UserId,
                    action,
                    dto.StageComment);
                if (!workflowResult.Success)
                    throw new InvalidOperationException(workflowResult.Message ?? "Unable to finalize the review stage.");

                if (allRejected ||
                    workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                {
                    batch.ApprovalStatus = JournalBatchApprovalStatus.Rejected;
                    batch.PostingStatus = JournalBatchPostingStatus.NotReady;
                    batch.ReviewCompletedAt = now;
                    if (batch.BatchType == JournalBatchType.Reversal)
                    {
                        batch.IsVoided = true;
                        batch.VoidedByUserId = UserId;
                        batch.VoidedAt = now;
                        batch.VoidReason = "The reversal workflow was rejected or cancelled.";
                        await ReleaseSourceReversalClaimAsync(batch, cancellationToken);
                    }
                }
                else if (workflowResult.Status == WorkflowInstanceStatus.Completed)
                {
                    foreach (var item in ActiveItems(batch).Where(x => x.ReviewStatus == JournalBatchItemReviewStatus.Pending))
                    {
                        item.ReviewStatus = JournalBatchItemReviewStatus.Approved;
                        item.FinalReviewedByUserId = UserId;
                        item.FinalReviewedAt = now;
                        item.PostingStatus = JournalBatchItemPostingStatus.Ready;
                        item.JournalEntry.PostingStatus = "Approved";
                        item.JournalEntry.ApprovalStatus = "Approved";
                        item.JournalEntry.ApprovedByUserId = UserId;
                        item.JournalEntry.ApprovedDate = now;
                    }

                    batch.ApprovalStatus = ActiveItems(batch).Any(x => x.ReviewStatus == JournalBatchItemReviewStatus.Rejected)
                        ? JournalBatchApprovalStatus.PartiallyApproved
                        : JournalBatchApprovalStatus.Approved;
                    batch.PostingStatus = JournalBatchPostingStatus.Ready;
                    batch.ReviewCompletedAt = now;
                }

                StampModified(batch);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        await AuditAsync("JournalBatchReviewStageFinalized", batch, cancellationToken, new
        {
            stageKey,
            decisions = dto.Decisions.Select(x => new { x.JournalBatchItemId, x.Decision })
        });
        return await RequireDetailAsync(id, cancellationToken);
    }

    public async Task<JournalBatchPostingRunDto> PostAsync(
        Guid id,
        CreateJournalBatchPostingRunDto dto,
        CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(id, asTracking: true, cancellationToken)
            ?? throw new ArgumentException("Journal batch was not found for this tenant.");
        EnsurePostingEligibility(batch);

        var requestedIds = dto.JournalBatchItemIds.Distinct().ToList();
        if (requestedIds.Count == 0)
            throw new InvalidOperationException("Select at least one posting-ready journal entry to post.");

        var existingRun = batch.PostingRuns.FirstOrDefault(x =>
            !x.IsDeleted && string.Equals(x.IdempotencyKey, dto.IdempotencyKey.Trim(), StringComparison.Ordinal));
        if (existingRun != null)
        {
            var existingIds = existingRun.Items.Where(x => !x.IsDeleted).Select(x => x.JournalBatchItemId).OrderBy(x => x).ToList();
            if (!existingIds.SequenceEqual(requestedIds.OrderBy(x => x)))
                throw new InvalidOperationException("The idempotency key was already used with a different posting selection.");
            return MapPostingRun(existingRun);
        }

        var selected = ActiveItems(batch).Where(x => requestedIds.Contains(x.Id)).OrderBy(x => x.SequenceNumber).ToList();
        if (selected.Count != requestedIds.Count)
            throw new InvalidOperationException("One or more selected entries do not belong to this journal batch.");
        if (selected.Any(x => !IsPostingEligibleReview(batch, x) ||
                              x.PostingStatus != JournalBatchItemPostingStatus.Ready))
            throw new InvalidOperationException("Only posting-ready, unposted entries can be selected.");
        if (batch.BatchType == JournalBatchType.Reversal &&
            selected.Count != ActiveItems(batch).Count(x => IsPostingEligibleReview(batch, x)))
            throw new InvalidOperationException("A full reversal batch must be posted in one complete run.");

        foreach (var item in selected)
        {
            if (!string.Equals(item.SubmittedContentFingerprint, ComputeItemFingerprint(item), StringComparison.Ordinal))
                throw new InvalidOperationException($"{item.JournalEntry.JournalEntryNumber} changed after submission.");
        }

        var run = new JournalBatchPostingRun
        {
            TenantId = TenantId,
            JournalBatchId = batch.Id,
            RunNumber = batch.PostingRuns.Where(x => !x.IsDeleted).Select(x => x.RunNumber).DefaultIfEmpty().Max() + 1,
            IdempotencyKey = dto.IdempotencyKey.Trim(),
            Status = JournalBatchPostingRunStatus.Pending,
            RequestedByUserId = UserId,
            RequestedAt = DateTime.UtcNow,
            SelectedDebitTotal = Money(selected.Sum(x => x.JournalEntry.TotalDebitAmount)),
            SelectedEntryCount = selected.Count,
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId,
            Items = selected.Select(x => new JournalBatchPostingRunItem
            {
                TenantId = TenantId,
                JournalBatchItemId = x.Id,
                CreatedBy = _currentUser.UserName,
                CreatedById = UserId
            }).ToList()
        };
        _context.JournalBatchPostingRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var trackedRun = await _context.JournalBatchPostingRuns
                        .Include(x => x.Items)
                        .FirstAsync(x => x.TenantId == TenantId && x.Id == run.Id, cancellationToken);
                    var trackedBatch = await RequireBatchAsync(id, cancellationToken);
                    EnsurePostingEligibility(trackedBatch);

                    var trackedItems = await ClaimPostingItemsAsync(
                        id,
                        trackedRun.Id,
                        requestedIds,
                        cancellationToken);

                    trackedRun.Status = JournalBatchPostingRunStatus.Posting;
                    trackedRun.StartedAt = DateTime.UtcNow;
                    trackedBatch.PostingStatus = JournalBatchPostingStatus.Posting;
                    await _context.SaveChangesAsync(cancellationToken);

                    foreach (var item in trackedItems)
                    {
                        await _journalEntries.PostJournalEntryForBatchAsync(item.JournalEntryId, cancellationToken);
                        var postingEvent = await _context.FinancePostingEvents
                            .Where(x => x.TenantId == TenantId &&
                                        x.SourceDocumentId == item.JournalEntryId &&
                                        x.PostingAction == "Post" &&
                                        !x.IsDeleted)
                            .OrderByDescending(x => x.CreatedAt)
                            .FirstOrDefaultAsync(cancellationToken);

                        item.PostingStatus = JournalBatchItemPostingStatus.Posted;
                        item.PostingClaimRunId = null;
                        item.PostingClaimedAt = null;
                        item.PostedInRunId = trackedRun.Id;
                        item.PostedAt = DateTime.UtcNow;
                        var runItem = trackedRun.Items.Single(x => x.JournalBatchItemId == item.Id);
                        runItem.FinancePostingEventId = postingEvent?.Id;
                    }

                    trackedRun.Status = JournalBatchPostingRunStatus.Posted;
                    trackedRun.CompletedAt = DateTime.UtcNow;
                    var remaining = await _context.JournalBatchItems.AnyAsync(
                        x => x.TenantId == TenantId &&
                             x.JournalBatchId == id &&
                             !x.IsDeleted &&
                             !requestedIds.Contains(x.Id) &&
                             (x.ReviewStatus == JournalBatchItemReviewStatus.Approved ||
                              (!x.JournalBatch.ApprovalRequired && x.ReviewStatus == JournalBatchItemReviewStatus.NotRequired)) &&
                             x.PostingStatus != JournalBatchItemPostingStatus.Posted,
                        cancellationToken);
                    trackedBatch.PostingStatus = remaining
                        ? JournalBatchPostingStatus.PartiallyPosted
                        : JournalBatchPostingStatus.Posted;
                    if (!remaining)
                    {
                        trackedBatch.PostingCompletedAt = DateTime.UtcNow;
                        if (trackedBatch.BatchType == JournalBatchType.Reversal)
                            await CompleteFullReversalAsync(trackedBatch, cancellationToken);
                    }
                    StampModified(trackedBatch);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            var failedRun = await _context.JournalBatchPostingRuns
                .FirstAsync(x => x.TenantId == TenantId && x.Id == run.Id, cancellationToken);
            failedRun.Status = JournalBatchPostingRunStatus.Failed;
            failedRun.CompletedAt = DateTime.UtcNow;
            failedRun.ErrorMessage = Sanitize(ex.Message);
            await ReleasePostingClaimsAsync(run.Id, cancellationToken);
            var failedBatch = await RequireBatchAsync(id, cancellationToken);
            failedBatch.PostingStatus = await DeterminePostingProgressAsync(id, cancellationToken);
            if (failedBatch.PostingStatus != JournalBatchPostingStatus.Posted)
                failedBatch.PostingCompletedAt = null;
            await _context.SaveChangesAsync(cancellationToken);
            await AuditAsync("JournalBatchPostingFailed", failedBatch, cancellationToken, new { run.Id, failedRun.ErrorMessage });
            throw;
        }

        _context.ChangeTracker.Clear();
        var completed = await _context.JournalBatchPostingRuns
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstAsync(x => x.TenantId == TenantId && x.Id == run.Id, cancellationToken);
        var completedBatch = await RequireBatchAsync(id, cancellationToken);
        foreach (var item in selected)
        {
            try
            {
                await _journalEntries.NotifyJournalPostedAsync(
                    item.JournalEntryId,
                    CancellationToken.None);
            }
            catch (Exception notificationException)
            {
                _logger.LogError(
                    notificationException,
                    "Journal {JournalEntryId} was committed in batch {JournalBatchId}, but its owner notification failed.",
                    item.JournalEntryId,
                    id);
            }
        }
        await AuditAsync(
            completedBatch.PostingStatus == JournalBatchPostingStatus.Posted
                ? "JournalBatchPosted"
                : "JournalBatchPartiallyPosted",
            completedBatch,
            cancellationToken,
            new { run.Id, run.RunNumber, run.SelectedEntryCount, run.SelectedDebitTotal });
        return MapPostingRun(completed);
    }

    public async Task<JournalBatchDetailDto> CopyAsync(
        Guid id,
        CopyJournalBatchDto dto,
        bool rejectedOnly,
        CancellationToken cancellationToken = default)
    {
        var source = await LoadBatchAsync(id, asTracking: false, cancellationToken)
            ?? throw new ArgumentException("Journal batch was not found for this tenant.");
        var selected = ActiveItems(source)
            .Where(x => !rejectedOnly || x.ReviewStatus == JournalBatchItemReviewStatus.Rejected)
            .OrderBy(x => x.SequenceNumber)
            .ToList();
        if (selected.Count == 0)
            throw new InvalidOperationException(rejectedOnly ? "The batch has no rejected entries to copy." : "The batch has no entries to copy.");

        var fiscalPeriodId = dto.FiscalPeriodId ?? source.FiscalPeriodId;
        await EnsureOpenPeriodAsync(fiscalPeriodId, TenantId, cancellationToken);
        var expectedTotal = Money(selected.Sum(x => x.JournalEntry.TotalDebitAmount));
        var created = await CreateAsync(new CreateJournalBatchDto
        {
            Description = rejectedOnly ? $"Correction of rejected entries from {source.BatchNumber}" : $"Copy of {source.BatchNumber}",
            FiscalPeriodId = fiscalPeriodId,
            BookClassification = source.BookClassification,
            ControlCurrencyCode = source.ControlCurrencyCode,
            ExpectedDebitTotal = expectedTotal,
            ExpectedJournalCount = selected.Count,
            Notes = $"Copied from journal batch {source.BatchNumber}."
        }, cancellationToken);

        foreach (var item in selected)
        {
            var original = item.JournalEntry;
            await CreateJournalAsync(created.Id, new CreateJournalEntryDto
            {
                TransactionDate = dto.EntryDate?.Date ?? original.EntryDate,
                JournalType = original.JournalType,
                Description = original.Description,
                Reference = original.ReferenceNumber,
                BookClassification = source.BookClassification,
                SourceModule = "GL",
                Notes = $"Copied from {original.JournalEntryNumber} in {source.BatchNumber}.",
                FiscalPeriodId = fiscalPeriodId,
                Transactions = original.Transactions
                    .Where(x => !x.IsDeleted)
                    .OrderBy(x => x.LineNumber)
                    .Select(x => new CreateAccountTransactionDto
                    {
                        AccountId = x.AccountId,
                        Amount = x.DebitAmount > 0 ? x.DebitAmount : x.CreditAmount,
                        TransactionType = x.DebitAmount > 0 ? "Debit" : "Credit",
                        Description = x.Description,
                        Reference = x.SourceReferenceNumber ?? original.ReferenceNumber ?? string.Empty,
                        CurrencyCode = x.TransactionCurrency,
                        ForeignAmount = x.ForeignCurrencyAmount,
                        ExchangeRate = x.ExchangeRate,
                        LineNumber = x.LineNumber
                    })
                    .ToList()
            }, cancellationToken);
        }

        if (dto.IncludeAttachments)
        {
            var attachments = await _context.JournalBatchAttachments
                .AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.JournalBatchId == id && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var attachment in attachments)
                await LinkAttachmentAsync(created.Id, attachment.FileUploadRecordId, cancellationToken);
        }

        var copiedBatch = await RequireBatchAsync(created.Id, cancellationToken);
        await AuditAsync(
            rejectedOnly ? "JournalBatchRejectedItemsCopied" : "JournalBatchCopied",
            copiedBatch,
            cancellationToken,
            new { sourceBatchId = source.Id, source.BatchNumber });
        return await RequireDetailAsync(created.Id, cancellationToken);
    }

    public async Task<JournalBatchDetailDto> CreateReversalBatchAsync(
        Guid id,
        CreateJournalBatchReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var source = await LoadBatchAsync(id, asTracking: true, cancellationToken)
                    ?? throw new ArgumentException("Journal batch was not found for this tenant.");
                if (source.PostingStatus != JournalBatchPostingStatus.Posted)
                    throw new InvalidOperationException("The entire approved batch must be posted before it can be reversed.");
                if (source.ReversalStatus != JournalBatchReversalStatus.NotReversed)
                    throw new InvalidOperationException("The journal batch is already reversed or has a reversal in progress.");

                var sourceItems = ActiveItems(source)
                    .Where(x => x.PostingStatus == JournalBatchItemPostingStatus.Posted)
                    .OrderBy(x => x.SequenceNumber)
                    .ToList();
                if (sourceItems.Count == 0 ||
                    sourceItems.Any(x => x.JournalEntry.IsReversed || x.JournalEntry.ReversalJournalEntryId.HasValue))
                {
                    throw new InvalidOperationException(
                        "Every posted journal must be unreversed before a full batch reversal can be created.");
                }

                var reversalPeriod = await FindOpenPeriodAsync(dto.ReversalDate, TenantId, cancellationToken);
                var reversal = await CreateAsync(new CreateJournalBatchDto
                {
                    Description = $"Full reversal of {source.BatchNumber}",
                    FiscalPeriodId = reversalPeriod.Id,
                    BookClassification = source.BookClassification,
                    ControlCurrencyCode = source.ControlCurrencyCode,
                    ExpectedDebitTotal = Money(sourceItems.Sum(x => x.JournalEntry.TotalDebitAmount)),
                    ExpectedJournalCount = sourceItems.Count,
                    Notes = dto.Reason.Trim()
                }, cancellationToken);
                var reversalEntity = await RequireBatchAsync(reversal.Id, cancellationToken);
                reversalEntity.BatchType = JournalBatchType.Reversal;
                reversalEntity.ReversalOfJournalBatchId = source.Id;
                reversalEntity.ReversalReason = dto.Reason.Trim();
                source.ReversalStatus = JournalBatchReversalStatus.ReversalPending;
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var sourceItem in sourceItems)
                {
                    var original = sourceItem.JournalEntry;
                    var detail = await CreateJournalAsync(reversal.Id, new CreateJournalEntryDto
                    {
                        TransactionDate = dto.ReversalDate.Date,
                        JournalType = "Reversing",
                        Description = $"Reversal of {original.JournalEntryNumber}: {original.Description}",
                        Reference = original.JournalEntryNumber,
                        BookClassification = source.BookClassification,
                        SourceModule = "GL",
                        SourceDocumentId = original.Id,
                        SourceDocumentType = "JournalBatchReversal",
                        Notes = dto.Reason.Trim(),
                        FiscalPeriodId = reversalPeriod.Id,
                        Transactions = original.Transactions
                            .Where(x => !x.IsDeleted)
                            .OrderBy(x => x.LineNumber)
                            .Select(x => new CreateAccountTransactionDto
                            {
                                AccountId = x.AccountId,
                                Amount = x.DebitAmount > 0 ? x.DebitAmount : x.CreditAmount,
                                TransactionType = x.DebitAmount > 0 ? "Credit" : "Debit",
                                Description = $"Reversal: {x.Description}",
                                Reference = original.JournalEntryNumber,
                                CurrencyCode = x.TransactionCurrency,
                                ForeignAmount = x.ForeignCurrencyAmount,
                                ExchangeRate = x.ExchangeRate,
                                LineNumber = x.LineNumber
                            })
                            .ToList()
                    }, cancellationToken);

                    var reversalItem = detail.Items.OrderByDescending(x => x.SequenceNumber).First();
                    var reversalJournal = await _context.JournalEntries.FirstAsync(
                        x => x.TenantId == TenantId && x.Id == reversalItem.JournalEntryId,
                        cancellationToken);
                    reversalJournal.OriginalJournalEntryId = original.Id;
                    reversalJournal.ReversalType = "Batch";
                    reversalJournal.ReversalReason = dto.Reason.Trim();
                    sourceItem.ReversalJournalBatchItemId = reversalItem.Id;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                await AuditAsync("JournalBatchReversalCreated", reversalEntity, cancellationToken, new
                {
                    sourceBatchId = source.Id,
                    source.BatchNumber,
                    dto.ReversalDate,
                    dto.Reason
                });
                await SubmitAsync(reversal.Id, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                return await RequireDetailAsync(reversal.Id, cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task LinkAttachmentAsync(Guid id, Guid fileUploadRecordId, CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        var fileExists = await _context.FileUploadRecords.AnyAsync(
            x => x.TenantId == TenantId && x.Id == fileUploadRecordId && !x.IsDeleted,
            cancellationToken);
        if (!fileExists)
            throw new ArgumentException("Attachment was not found for this tenant.");
        if (await _context.JournalBatchAttachments.AnyAsync(
                x => x.TenantId == TenantId &&
                     x.JournalBatchId == id &&
                     x.FileUploadRecordId == fileUploadRecordId &&
                     !x.IsDeleted,
                cancellationToken))
            return;
        _context.JournalBatchAttachments.Add(new JournalBatchAttachment
        {
            TenantId = TenantId,
            JournalBatchId = id,
            FileUploadRecordId = fileUploadRecordId,
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlinkAttachmentAsync(Guid id, Guid fileUploadRecordId, CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(id, cancellationToken);
        EnsureDraft(batch);
        var link = await _context.JournalBatchAttachments.FirstOrDefaultAsync(
            x => x.TenantId == TenantId &&
                 x.JournalBatchId == id &&
                 x.FileUploadRecordId == fileUploadRecordId &&
                 !x.IsDeleted,
            cancellationToken);
        if (link == null)
            return;
        link.IsDeleted = true;
        link.DeletedAt = DateTime.UtcNow;
        link.DeletedBy = _currentUser.UserName;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<JournalBatchItem>> ClaimPostingItemsAsync(
        Guid batchId,
        Guid postingRunId,
        IReadOnlyCollection<Guid> requestedIds,
        CancellationToken cancellationToken)
    {
        var claimable = _context.JournalBatchItems.Where(item =>
            item.TenantId == TenantId &&
            item.JournalBatchId == batchId &&
            requestedIds.Contains(item.Id) &&
            !item.IsDeleted &&
            (item.ReviewStatus == JournalBatchItemReviewStatus.Approved ||
             (!item.JournalBatch.ApprovalRequired && item.ReviewStatus == JournalBatchItemReviewStatus.NotRequired)) &&
            item.PostingStatus == JournalBatchItemPostingStatus.Ready &&
            item.PostingClaimRunId == null);

        if (_context.Database.IsRelational())
        {
            var claimedAt = DateTime.UtcNow;
            var affected = await claimable.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.PostingStatus, JournalBatchItemPostingStatus.Posting)
                    .SetProperty(item => item.PostingClaimRunId, postingRunId)
                    .SetProperty(item => item.PostingClaimedAt, claimedAt),
                cancellationToken);
            if (affected != requestedIds.Count)
            {
                throw new ConflictException(
                    "One or more journal entries were claimed by another posting run. Refresh the batch and retry.");
            }
        }
        else
        {
            var claimed = await claimable
                .Include(item => item.JournalEntry)
                    .ThenInclude(journal => journal.Transactions)
                .OrderBy(item => item.SequenceNumber)
                .ToListAsync(cancellationToken);
            if (claimed.Count != requestedIds.Count)
            {
                throw new ConflictException(
                    "One or more journal entries were claimed by another posting run. Refresh the batch and retry.");
            }

            var claimedAt = DateTime.UtcNow;
            foreach (var item in claimed)
            {
                item.PostingStatus = JournalBatchItemPostingStatus.Posting;
                item.PostingClaimRunId = postingRunId;
                item.PostingClaimedAt = claimedAt;
            }
            return claimed;
        }

        return await _context.JournalBatchItems
            .Include(item => item.JournalEntry)
                .ThenInclude(journal => journal.Transactions)
            .Where(item =>
                item.TenantId == TenantId &&
                item.JournalBatchId == batchId &&
                requestedIds.Contains(item.Id) &&
                !item.IsDeleted &&
                item.PostingClaimRunId == postingRunId &&
                item.PostingStatus == JournalBatchItemPostingStatus.Posting)
            .OrderBy(item => item.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    private async Task ReleasePostingClaimsAsync(Guid postingRunId, CancellationToken cancellationToken)
    {
        var claims = _context.JournalBatchItems.Where(item =>
            item.TenantId == TenantId &&
            item.PostingClaimRunId == postingRunId &&
            item.PostingStatus == JournalBatchItemPostingStatus.Posting &&
            !item.IsDeleted);

        if (_context.Database.IsRelational())
        {
            await claims.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.PostingStatus, JournalBatchItemPostingStatus.Ready)
                    .SetProperty(item => item.PostingClaimRunId, (Guid?)null)
                    .SetProperty(item => item.PostingClaimedAt, (DateTime?)null),
                cancellationToken);
            return;
        }

        foreach (var item in await claims.ToListAsync(cancellationToken))
        {
            item.PostingStatus = JournalBatchItemPostingStatus.Ready;
            item.PostingClaimRunId = null;
            item.PostingClaimedAt = null;
        }
    }

    private async Task<JournalBatchPostingStatus> DeterminePostingProgressAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var approvedStates = await _context.JournalBatchItems
            .AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                item.JournalBatchId == batchId &&
                (item.ReviewStatus == JournalBatchItemReviewStatus.Approved ||
                 (!item.JournalBatch.ApprovalRequired && item.ReviewStatus == JournalBatchItemReviewStatus.NotRequired)) &&
                !item.IsDeleted)
            .Select(item => item.PostingStatus)
            .ToListAsync(cancellationToken);
        var postedCount = approvedStates.Count(status => status == JournalBatchItemPostingStatus.Posted);
        if (approvedStates.Count > 0 && postedCount == approvedStates.Count)
            return JournalBatchPostingStatus.Posted;
        return postedCount > 0
            ? JournalBatchPostingStatus.PartiallyPosted
            : JournalBatchPostingStatus.Ready;
    }

    private async Task<JournalBatch?> LoadBatchAsync(Guid id, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.JournalBatches
            .Where(x => x.TenantId == TenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.FiscalPeriod)
            .Include(x => x.ReversalAttempts)
            .Include(x => x.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.JournalEntry)
                    .ThenInclude(journal => journal.Transactions)
            .Include(x => x.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.Reviews.Where(review => !review.IsDeleted))
            .Include(x => x.PostingRuns.Where(run => !run.IsDeleted))
                .ThenInclude(run => run.Items.Where(item => !item.IsDeleted))
            .Include(x => x.Attachments.Where(attachment => !attachment.IsDeleted))
            .AsSplitQuery();
        if (!asTracking)
            query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<JournalBatch> RequireBatchAsync(Guid id, CancellationToken cancellationToken)
        => await _context.JournalBatches.FirstOrDefaultAsync(
               x => x.TenantId == TenantId && x.Id == id && !x.IsDeleted,
               cancellationToken)
           ?? throw new ArgumentException("Journal batch was not found for this tenant.");

    private async Task<JournalBatchDetailDto> RequireDetailAsync(Guid id, CancellationToken cancellationToken)
        => await GetByIdAsync(id, cancellationToken)
           ?? throw new InvalidOperationException("Journal batch could not be reloaded.");

    private async Task<JournalBatchItem> RequireBatchItemAsync(
        Guid batchId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
        => await _context.JournalBatchItems.FirstOrDefaultAsync(
               x => x.TenantId == TenantId &&
                    x.JournalBatchId == batchId &&
                    x.JournalEntryId == journalEntryId &&
                    !x.IsDeleted,
               cancellationToken)
           ?? throw new ArgumentException("Journal entry is not a member of this batch.");

    private async Task EnsureEligibleJournalAsync(
        JournalBatch batch,
        JournalEntry journal,
        CancellationToken cancellationToken)
    {
        if (journal.PostingStatus != "Draft")
            throw new InvalidOperationException("Only Draft journal entries can be added to a batch.");
        if (batch.BatchType == JournalBatchType.Reversal)
        {
            var eligibleSourceIds = batch.ReversalOfJournalBatchId.HasValue
                ? (await _context.JournalBatchItems
                    .AsNoTracking()
                    .Where(x => x.TenantId == TenantId &&
                                x.JournalBatchId == batch.ReversalOfJournalBatchId.Value &&
                                x.PostingStatus == JournalBatchItemPostingStatus.Posted &&
                                !x.IsDeleted)
                    .Select(x => x.JournalEntryId)
                    .ToListAsync(cancellationToken)).ToHashSet()
                : new HashSet<Guid>();
            if (!IsGeneratedBatchReversalJournal(batch, journal, eligibleSourceIds))
                throw new InvalidOperationException("Only generated reversals of posted entries in the source batch can be added to a reversal batch.");
        }
        else if (!IsManualJournal(journal))
        {
            throw new InvalidOperationException("Only manual General Ledger journals can be added to a batch.");
        }
        if (journal.FiscalPeriodId != batch.FiscalPeriodId)
            throw new InvalidOperationException("Journal entry and batch must use the same fiscal period.");
        if (!string.Equals(journal.BookClassification, batch.BookClassification, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Journal entry and batch must use the same accounting book.");
        if (await _context.JournalBatchItems.AnyAsync(
                x => x.TenantId == TenantId && x.JournalEntryId == journal.Id && !x.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException("Journal entry already belongs to a journal batch.");
        if (await _context.WorkflowInstances.AnyAsync(
                x => x.TenantId == TenantId &&
                     x.EntityId == journal.Id &&
                     x.EntityType.Code == "JournalEntry" &&
                     x.Status != WorkflowInstanceStatus.Completed &&
                     x.Status != WorkflowInstanceStatus.Cancelled &&
                     x.Status != WorkflowInstanceStatus.Failed,
                cancellationToken))
            throw new InvalidOperationException("Journal entry has an active standalone approval workflow.");
    }

    private static bool IsManualJournal(JournalEntry journal)
    {
        var source = journal.SourceModule?.Trim();
        return (string.IsNullOrWhiteSpace(source) ||
                source.Equals("GL", StringComparison.OrdinalIgnoreCase) ||
                source.Equals("Manual", StringComparison.OrdinalIgnoreCase)) &&
               !journal.IsRecurring &&
               !journal.IsRevaluationEntry &&
               !journal.IsAutoReversalEntry &&
               !journal.OriginalJournalEntryId.HasValue;
    }

    private static bool IsGeneratedBatchReversalJournal(
        JournalBatch batch,
        JournalEntry journal,
        IReadOnlySet<Guid> eligibleSourceJournalIds)
    {
        var sourceJournalId = journal.OriginalJournalEntryId ?? journal.SourceDocumentId;
        return batch.ReversalOfJournalBatchId.HasValue &&
               sourceJournalId.HasValue &&
               eligibleSourceJournalIds.Contains(sourceJournalId.Value) &&
               string.Equals(journal.SourceModule, "GL", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(journal.SourceDocumentType, "JournalBatchReversal", StringComparison.OrdinalIgnoreCase) &&
               journal.SourceDocumentId == sourceJournalId;
    }

    private async Task<int> NextSequenceAsync(Guid batchId, CancellationToken cancellationToken)
        => await _context.JournalBatchItems
               .Where(x => x.TenantId == TenantId && x.JournalBatchId == batchId && !x.IsDeleted)
               .Select(x => (int?)x.SequenceNumber)
               .MaxAsync(cancellationToken) is { } maximum
            ? maximum + 1
            : 1;

    private async Task EnsureOpenPeriodAsync(Guid fiscalPeriodId, Guid tenantId, CancellationToken cancellationToken)
    {
        var period = await _context.FiscalPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == fiscalPeriodId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Fiscal period was not found for this tenant.");
        if (!period.IsOpen || period.IsClosed || period.IsLocked)
            throw new InvalidOperationException($"Fiscal period '{period.PeriodName}' is not open and unlocked.");
    }

    private async Task EnsureControlCurrencyAsync(string currencyCode, Guid tenantId, CancellationToken cancellationToken)
    {
        var baseCurrency = await _context.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseCurrency))
            throw new InvalidOperationException("The tenant base currency is not configured.");
        if (!string.Equals(currencyCode.Trim(), baseCurrency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Journal batch control totals are measured in the tenant base currency {baseCurrency.Trim().ToUpperInvariant()}.");
        }
    }

    private async Task<FiscalPeriod> FindOpenPeriodAsync(DateTime date, Guid tenantId, CancellationToken cancellationToken)
    {
        var target = date.Date;
        return await _context.FiscalPeriods
                   .FirstOrDefaultAsync(
                       x => x.TenantId == tenantId &&
                            !x.IsDeleted &&
                            x.StartDate <= target &&
                            x.EndDate >= target &&
                            x.IsOpen &&
                            !x.IsClosed &&
                            !x.IsLocked,
                       cancellationToken)
               ?? throw new InvalidOperationException($"No open fiscal period contains reversal date {target:yyyy-MM-dd}.");
    }

    private static void EnsureDraft(JournalBatch batch)
    {
        if (batch.IsVoided || batch.ApprovalStatus != JournalBatchApprovalStatus.Draft)
            throw new InvalidOperationException("Only a Draft journal batch can be changed.");
    }

    private void StampModified(JournalBatch batch)
    {
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = _currentUser.UserName;
        batch.LastModifiedById = UserId;
    }

    private void ApplyRowVersion(JournalBatch batch, string rowVersion)
    {
        try
        {
            _context.Entry(batch).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The supplied row version is invalid.");
        }
    }

    private async Task ReleaseSourceReversalClaimAsync(
        JournalBatch batch,
        CancellationToken cancellationToken)
    {
        if (batch.BatchType != JournalBatchType.Reversal || !batch.ReversalOfJournalBatchId.HasValue)
            return;
        var source = await _context.JournalBatches
            .Include(item => item.Items.Where(batchItem => !batchItem.IsDeleted))
            .FirstOrDefaultAsync(
                x => x.TenantId == TenantId && x.Id == batch.ReversalOfJournalBatchId.Value && !x.IsDeleted,
                cancellationToken);
        if (source == null)
            return;

        source.ReversalStatus = JournalBatchReversalStatus.NotReversed;
        foreach (var sourceItem in source.Items)
            sourceItem.ReversalJournalBatchItemId = null;
    }

    private async Task CompleteFullReversalAsync(JournalBatch reversalBatch, CancellationToken cancellationToken)
    {
        if (!reversalBatch.ReversalOfJournalBatchId.HasValue)
            throw new InvalidOperationException("The reversal batch has no source batch.");
        var source = await _context.JournalBatches
            .Include(x => x.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.JournalEntry)
            .FirstAsync(
                x => x.TenantId == TenantId && x.Id == reversalBatch.ReversalOfJournalBatchId.Value && !x.IsDeleted,
                cancellationToken);
        var reversalItems = await _context.JournalBatchItems
            .Include(x => x.JournalEntry)
            .Where(x => x.TenantId == TenantId && x.JournalBatchId == reversalBatch.Id && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var sourceItem in source.Items.Where(x => !x.IsDeleted))
        {
            if (!sourceItem.ReversalJournalBatchItemId.HasValue)
                throw new InvalidOperationException("A source batch item is missing its reversal link.");
            var reversalItem = reversalItems.Single(x => x.Id == sourceItem.ReversalJournalBatchItemId.Value);
            var original = sourceItem.JournalEntry;
            original.IsReversed = true;
            original.ReversalDate = reversalItem.JournalEntry.PostingDate ?? DateTime.UtcNow;
            original.ReversalJournalEntryId = reversalItem.JournalEntryId;
            original.ReversalType = "Batch";
            original.ReversalReason = reversalBatch.ReversalReason;
        }

        source.ReversalStatus = JournalBatchReversalStatus.Reversed;
        reversalBatch.ReversalStatus = JournalBatchReversalStatus.Reversed;
    }

    private static List<JournalBatchItem> ActiveItems(JournalBatch batch)
        => batch.Items.Where(x => !x.IsDeleted).OrderBy(x => x.SequenceNumber).ToList();

    private static bool IsPostingEligibleReview(JournalBatch batch, JournalBatchItem item)
        => batch.ApprovalRequired
            ? item.ReviewStatus == JournalBatchItemReviewStatus.Approved
            : item.ReviewStatus == JournalBatchItemReviewStatus.NotRequired;

    private static void EnsurePostingEligibility(JournalBatch batch)
    {
        if (batch.IsVoided || (batch.ApprovalRequired
            ? batch.ApprovalStatus is not (JournalBatchApprovalStatus.Approved or JournalBatchApprovalStatus.PartiallyApproved)
            : batch.ApprovalStatus != JournalBatchApprovalStatus.ReadyToPost))
            throw new InvalidOperationException("The journal batch is not ready for posting.");
        if (!batch.ApprovalRequired && (batch.WorkflowInstanceId.HasValue || batch.ApprovedByUserId.HasValue ||
            batch.ApprovedAt.HasValue || batch.ReviewCompletedAt.HasValue || ActiveItems(batch).Any(x =>
                x.Reviews.Any(r => !r.IsDeleted) || x.FinalReviewedByUserId.HasValue || x.FinalReviewedAt.HasValue ||
                x.JournalEntry.RequiresApproval || x.JournalEntry.ApprovedByUserId.HasValue ||
                x.JournalEntry.ApprovedDate.HasValue || !string.IsNullOrWhiteSpace(x.JournalEntry.ApprovalWorkflowId) ||
                x.JournalEntry.ApprovalStatus != "Not Required")))
            throw new InvalidOperationException("A directly prepared batch cannot claim a workflow or human approval.");
    }

    private static decimal Money(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static JournalBatchValidationIssueDto Issue(
        string code,
        string message,
        JournalBatchItem? item = null)
        => new()
        {
            Code = code,
            Message = message,
            JournalBatchItemId = item?.Id,
            JournalEntryId = item?.JournalEntryId
        };

    private static string ComputeItemFingerprint(JournalBatchItem item)
    {
        var journal = item.JournalEntry;
        var payload = new
        {
            item.SequenceNumber,
            item.JournalEntryId,
            journal.JournalEntryNumber,
            date = journal.EntryDate.ToUniversalTime().ToString("O"),
            journal.JournalType,
            journal.Description,
            journal.ReferenceNumber,
            journal.BookClassification,
            totalDebit = Money(journal.TotalDebitAmount),
            totalCredit = Money(journal.TotalCreditAmount),
            lines = journal.Transactions
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.LineNumber)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.LineNumber,
                    x.AccountId,
                    debit = Money(x.DebitAmount),
                    credit = Money(x.CreditAmount),
                    x.TransactionCurrency,
                    x.ForeignCurrencyAmount,
                    x.ExchangeRate,
                    x.SegmentString,
                    x.SourceReferenceNumber,
                    x.Description
                })
        };
        return Sha256(JsonSerializer.Serialize(payload, FingerprintJsonOptions));
    }

    private static string ComputeBatchFingerprint(JournalBatch batch, IEnumerable<JournalBatchItem> items)
    {
        var payload = new
        {
            batch.BatchNumber,
            batch.FiscalPeriodId,
            batch.BookClassification,
            batch.ControlCurrencyCode,
            expected = Money(batch.ExpectedDebitTotal),
            batch.ExpectedJournalCount,
            items = items.OrderBy(x => x.SequenceNumber).Select(x => new { x.Id, x.SequenceNumber, x.SubmittedContentFingerprint })
        };
        return Sha256(JsonSerializer.Serialize(payload, FingerprintJsonOptions));
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Sanitize(string value)
        => value.Length <= 2000 ? value : value[..2000];

    private static JournalBatchListItemDto MapListItem(JournalBatch batch)
    {
        var items = ActiveItems(batch);
        var approved = items.Where(x => x.ReviewStatus == JournalBatchItemReviewStatus.Approved).ToList();
        var rejected = items.Where(x => x.ReviewStatus == JournalBatchItemReviewStatus.Rejected).ToList();
        var posted = items.Where(x => x.PostingStatus == JournalBatchItemPostingStatus.Posted).ToList();
        var debit = Money(items.Sum(x => x.JournalEntry.TotalDebitAmount));
        return new JournalBatchListItemDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            Description = batch.Description,
            FiscalPeriodId = batch.FiscalPeriodId,
            FiscalPeriodName = batch.FiscalPeriod?.PeriodName,
            BookClassification = batch.BookClassification,
            ControlCurrencyCode = batch.ControlCurrencyCode,
            BatchType = batch.BatchType,
            ApprovalStatus = batch.ApprovalStatus,
            ApprovalRequired = batch.ApprovalRequired,
            PostingStatus = batch.PostingStatus,
            ReversalStatus = batch.ReversalStatus,
            IsVoided = batch.IsVoided,
            DisplayStatus = DisplayStatus(batch),
            ExpectedDebitTotal = batch.ExpectedDebitTotal,
            ActualDebitTotal = debit,
            ActualCreditTotal = Money(items.Sum(x => x.JournalEntry.TotalCreditAmount)),
            Variance = debit - batch.ExpectedDebitTotal,
            EntryCount = items.Count,
            ExpectedJournalCount = batch.ExpectedJournalCount,
            ApprovedEntryCount = approved.Count,
            RejectedEntryCount = rejected.Count,
            PostedEntryCount = posted.Count,
            CreatedAt = batch.CreatedAt,
            SubmittedAt = batch.SubmittedAt,
            PostingCompletedAt = batch.PostingCompletedAt
        };
    }

    private static JournalBatchDetailDto MapDetail(JournalBatch batch)
    {
        var list = MapListItem(batch);
        var items = ActiveItems(batch);
        var approved = items.Where(x => x.ReviewStatus == JournalBatchItemReviewStatus.Approved).ToList();
        var rejected = items.Where(x => x.ReviewStatus == JournalBatchItemReviewStatus.Rejected).ToList();
        var posted = items.Where(x => x.PostingStatus == JournalBatchItemPostingStatus.Posted).ToList();
        return new JournalBatchDetailDto
        {
            Id = list.Id,
            BatchNumber = list.BatchNumber,
            Description = list.Description,
            FiscalPeriodId = list.FiscalPeriodId,
            FiscalPeriodName = list.FiscalPeriodName,
            BookClassification = list.BookClassification,
            ControlCurrencyCode = list.ControlCurrencyCode,
            BatchType = list.BatchType,
            ApprovalStatus = list.ApprovalStatus,
            ApprovalRequired = list.ApprovalRequired,
            PostingStatus = list.PostingStatus,
            ReversalStatus = list.ReversalStatus,
            IsVoided = list.IsVoided,
            DisplayStatus = list.DisplayStatus,
            ExpectedDebitTotal = list.ExpectedDebitTotal,
            ActualDebitTotal = list.ActualDebitTotal,
            ActualCreditTotal = list.ActualCreditTotal,
            Variance = list.Variance,
            EntryCount = list.EntryCount,
            ExpectedJournalCount = list.ExpectedJournalCount,
            ApprovedEntryCount = list.ApprovedEntryCount,
            RejectedEntryCount = list.RejectedEntryCount,
            PostedEntryCount = list.PostedEntryCount,
            CreatedAt = list.CreatedAt,
            SubmittedAt = list.SubmittedAt,
            PostingCompletedAt = list.PostingCompletedAt,
            ApprovedDebitTotal = Money(approved.Sum(x => x.JournalEntry.TotalDebitAmount)),
            RejectedDebitTotal = Money(rejected.Sum(x => x.JournalEntry.TotalDebitAmount)),
            PostedDebitTotal = Money(posted.Sum(x => x.JournalEntry.TotalDebitAmount)),
            RemainingApprovedDebitTotal = Money(approved.Where(x => x.PostingStatus != JournalBatchItemPostingStatus.Posted).Sum(x => x.JournalEntry.TotalDebitAmount)),
            PendingReviewCount = items.Count(x => x.ReviewStatus == JournalBatchItemReviewStatus.Pending),
            RemainingApprovedEntryCount = approved.Count(x => x.PostingStatus != JournalBatchItemPostingStatus.Posted),
            LineCount = items.Sum(x => x.JournalEntry.Transactions.Count(t => !t.IsDeleted)),
            ContentFingerprint = batch.ContentFingerprint,
            Notes = batch.Notes,
            SubmittedByUserId = batch.SubmittedByUserId,
            ApprovedByUserId = batch.ApprovedByUserId,
            WorkflowInstanceId = batch.WorkflowInstanceId,
            ApprovedAt = batch.ApprovedAt,
            ReviewCompletedAt = batch.ReviewCompletedAt,
            ReversalOfJournalBatchId = batch.ReversalOfJournalBatchId,
            ReversalBatchId = batch.ReversalAttempts
                .Where(attempt => !attempt.IsDeleted && !attempt.IsVoided)
                .OrderByDescending(attempt => attempt.CreatedAt)
                .Select(attempt => (Guid?)attempt.Id)
                .FirstOrDefault(),
            ReversalReason = batch.ReversalReason,
            VoidedByUserId = batch.VoidedByUserId,
            VoidedAt = batch.VoidedAt,
            VoidReason = batch.VoidReason,
            RowVersion = batch.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(batch.RowVersion),
            CanEdit = !batch.IsVoided && batch.ApprovalStatus == JournalBatchApprovalStatus.Draft,
            CanSubmit = !batch.IsVoided && batch.ApprovalStatus == JournalBatchApprovalStatus.Draft && items.Count > 0,
            CanReview = !batch.IsVoided && batch.ApprovalRequired && batch.ApprovalStatus == JournalBatchApprovalStatus.PendingApproval,
            CanPostAny = !batch.IsVoided &&
                         (batch.ApprovalRequired
                             ? batch.ApprovalStatus is JournalBatchApprovalStatus.Approved or JournalBatchApprovalStatus.PartiallyApproved
                             : batch.ApprovalStatus == JournalBatchApprovalStatus.ReadyToPost) &&
                         items.Any(x => IsPostingEligibleReview(batch, x) && x.PostingStatus == JournalBatchItemPostingStatus.Ready),
            CanReverseBatch = !batch.IsVoided &&
                              batch.PostingStatus == JournalBatchPostingStatus.Posted &&
                              batch.ReversalStatus == JournalBatchReversalStatus.NotReversed &&
                              items.Where(x => x.PostingStatus == JournalBatchItemPostingStatus.Posted)
                                  .All(x => !x.JournalEntry.IsReversed && !x.JournalEntry.ReversalJournalEntryId.HasValue),
            Items = items.Select(MapItem).ToList(),
            PostingRuns = batch.PostingRuns.Where(x => !x.IsDeleted).OrderBy(x => x.RunNumber).Select(MapPostingRun).ToList(),
            AttachmentIds = batch.Attachments.Where(x => !x.IsDeleted).Select(x => x.FileUploadRecordId).ToList()
        };
    }

    private static JournalBatchItemDto MapItem(JournalBatchItem item)
        => new()
        {
            Id = item.Id,
            SequenceNumber = item.SequenceNumber,
            JournalEntryId = item.JournalEntryId,
            JournalEntryNumber = item.JournalEntry.JournalEntryNumber,
            EntryDate = item.JournalEntry.EntryDate,
            JournalType = item.JournalEntry.JournalType,
            Description = item.JournalEntry.Description,
            ReferenceNumber = item.JournalEntry.ReferenceNumber,
            TotalDebit = item.JournalEntry.TotalDebitAmount,
            TotalCredit = item.JournalEntry.TotalCreditAmount,
            LineCount = item.JournalEntry.Transactions.Count(x => !x.IsDeleted),
            ReviewStatus = item.ReviewStatus,
            PostingStatus = item.PostingStatus,
            FinalReviewedByUserId = item.FinalReviewedByUserId,
            FinalReviewedAt = item.FinalReviewedAt,
            FinalRejectionReason = item.FinalRejectionReason,
            PostingClaimRunId = item.PostingClaimRunId,
            PostingClaimedAt = item.PostingClaimedAt,
            PostedInRunId = item.PostedInRunId,
            PostedAt = item.PostedAt,
            ReversalJournalBatchItemId = item.ReversalJournalBatchItemId,
            Reviews = item.Reviews
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DecidedAt)
                .Select(x => new JournalBatchItemReviewDto
                {
                    Id = x.Id,
                    WorkflowStageKey = x.WorkflowStageKey,
                    Decision = x.Decision,
                    Comment = x.Comment,
                    DecidedByUserId = x.DecidedByUserId,
                    DecidedAt = x.DecidedAt
                })
                .ToList()
        };

    private static JournalBatchPostingRunDto MapPostingRun(JournalBatchPostingRun run)
        => new()
        {
            Id = run.Id,
            RunNumber = run.RunNumber,
            IdempotencyKey = run.IdempotencyKey,
            Status = run.Status,
            RequestedByUserId = run.RequestedByUserId,
            RequestedAt = run.RequestedAt,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            SelectedDebitTotal = run.SelectedDebitTotal,
            SelectedEntryCount = run.SelectedEntryCount,
            ErrorMessage = run.ErrorMessage,
            JournalBatchItemIds = run.Items.Where(x => !x.IsDeleted).Select(x => x.JournalBatchItemId).ToList()
        };

    private static string DisplayStatus(JournalBatch batch)
    {
        if (batch.IsVoided) return "Voided";
        if (batch.ReversalStatus == JournalBatchReversalStatus.Reversed) return "Reversed";
        if (batch.ReversalStatus == JournalBatchReversalStatus.ReversalPending) return "Reversal Pending";
        if (batch.PostingStatus == JournalBatchPostingStatus.Posted) return "Posted";
        if (batch.PostingStatus == JournalBatchPostingStatus.PartiallyPosted) return "Partially Posted";
        if (batch.PostingStatus == JournalBatchPostingStatus.Posting) return "Posting";
        return batch.ApprovalStatus switch
        {
            JournalBatchApprovalStatus.PartiallyApproved => "Partially Approved",
            JournalBatchApprovalStatus.PendingApproval => "Pending Approval",
            JournalBatchApprovalStatus.ReadyToPost => "Ready to Post",
            _ => batch.ApprovalStatus.ToString()
        };
    }

    private async Task AuditAsync(
        string eventType,
        JournalBatch batch,
        CancellationToken cancellationToken,
        object? context = null,
        string? reason = null)
    {
        if (_audit == null)
            return;
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = "GL",
            SourceDocumentType = nameof(JournalBatch),
            SourceDocumentId = batch.Id,
            WorkflowInstanceId = batch.WorkflowInstanceId,
            Resource = "Finance.JournalBatch",
            ResourceId = batch.Id.ToString(),
            Reason = reason,
            AfterValues = new
            {
                batch.BatchNumber,
                batch.ApprovalStatus,
                batch.PostingStatus,
                batch.ReversalStatus,
                batch.ExpectedDebitTotal,
                batch.ExpectedJournalCount
            },
            Context = context
        }, cancellationToken);
    }
}
