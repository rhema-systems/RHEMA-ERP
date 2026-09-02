using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Owns the authenticated recurring-journal lifecycle. Due-date generation is
/// delegated to <see cref="RecurringJournalGenerationProcessor"/> so the same
/// idempotent logic can be invoked by the API and the background scheduler.
/// </summary>
public sealed class RecurringJournalService : IRecurringJournalService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _numbering;
    private readonly IFinancePostingEngine _postingEngine;
    private readonly IFinanceAuditService _audit;
    private readonly RecurringJournalGenerationProcessor _generator;
    private readonly RecurringJournalReversalProcessor _reversals;

    public RecurringJournalService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IDocumentNumberingService numbering,
        IFinancePostingEngine postingEngine,
        IFinanceAuditService audit,
        RecurringJournalGenerationProcessor generator,
        RecurringJournalReversalProcessor reversals)
    {
        _db = db;
        _currentUser = currentUser;
        _numbering = numbering;
        _postingEngine = postingEngine;
        _audit = audit;
        _generator = generator;
        _reversals = reversals;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<IReadOnlyList<RecurringJournalTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var ids = await _db.RecurringJournalTemplates.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        // The list does not load full occurrence histories. Aggregate exception
        // counts once so the workspace metric remains truthful without creating
        // an unbounded payload as recurring schedules accumulate over the years.
        var exceptionCounts = await _db.RecurringJournalOccurrences.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && ids.Contains(item.TemplateId) &&
                (item.Status == RecurringJournalOccurrenceStatus.Failed ||
                 item.Status == RecurringJournalOccurrenceStatus.SubmissionFailed ||
                 item.ReversalStatus == RecurringJournalReversalStatus.Failed))
            .GroupBy(item => item.TemplateId)
            .Select(group => new { TemplateId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TemplateId, item => item.Count, cancellationToken);

        var result = new List<RecurringJournalTemplateDto>(ids.Count);
        foreach (var id in ids)
        {
            var item = await MapAsync(id, includeOccurrences: false, cancellationToken);
            if (item != null)
            {
                item.ExceptionCount = exceptionCounts.GetValueOrDefault(id);
                result.Add(item);
            }
        }
        return result;
    }

    public Task<RecurringJournalTemplateDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        MapAsync(id, includeOccurrences: true, cancellationToken);

    public async Task<RecurringJournalTemplateDto> CreateAsync(
        CreateRecurringJournalTemplateDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        await ValidateDefinitionAsync(request, tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        // A durable maker identity is mandatory because every later activation
        // and occurrence decision compares against this user for segregation.
        var userId = CurrentUserIdRequired();
        var template = new RecurringJournalTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateNumber = await _numbering.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.RecurringJournal,
                tenantId,
                request.EffectiveFrom.ToDateTime(TimeOnly.MinValue),
                nameof(RecurringJournalTemplate),
                cancellationToken: cancellationToken),
            DefinitionKey = Guid.NewGuid(),
            Version = 1,
            Status = RecurringJournalStatus.Draft,
            CreatedAt = now,
            CreatedBy = _currentUser.UserName ?? "Finance user",
            CreatedById = userId
        };

        ApplyDefinition(template, request);
        ReplaceLines(template, request.Lines, now, userId, template.CreatedBy);
        _db.RecurringJournalTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateCreated, template.Id, null,
            new { template.TemplateNumber, template.Name, template.Frequency, lineCount = template.Lines.Count }, null,
            cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    public async Task<RecurringJournalTemplateDto> UpdateAsync(
        Guid id,
        UpdateRecurringJournalTemplateDto request,
        CancellationToken cancellationToken = default)
    {
        var template = await LoadAsync(id, includeOccurrences: true, cancellationToken);
        if (template.Status is not (RecurringJournalStatus.Draft or RecurringJournalStatus.Rejected))
            throw new InvalidOperationException("Only Draft or Rejected recurring-journal templates can be edited.");
        if (template.Occurrences.Any(item => !item.IsDeleted))
            throw new InvalidOperationException("A template with generated occurrences must be cloned into a new version instead of edited.");

        SetRowVersion(template, request.RowVersion);
        await ValidateDefinitionAsync(request, TenantId, cancellationToken);
        var before = new { template.Name, template.Frequency, template.EffectiveFrom, template.EndDate, lineCount = template.Lines.Count };
        var now = DateTime.UtcNow;
        var userId = CurrentUserId();
        ApplyDefinition(template, request);
        template.Status = RecurringJournalStatus.Draft;
        template.SubmittedAt = null;
        template.SubmittedByUserId = null;
        template.ReviewedAt = null;
        template.ReviewedByUserId = null;
        template.ReviewComment = null;
        template.ActivatedAt = null;
        template.ActivatedByUserId = null;
        template.NextDueDate = null;
        template.LastFailure = null;
        template.UpdatedAt = now;
        template.UpdatedBy = _currentUser.UserName;
        template.LastModifiedById = userId;
        _db.RecurringJournalTemplateLines.RemoveRange(template.Lines);
        template.Lines = [];
        ReplaceLines(template, request.Lines, now, userId, template.UpdatedBy);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateUpdated, template.Id, before,
            new { template.Name, template.Frequency, template.EffectiveFrom, template.EndDate, lineCount = template.Lines.Count }, null,
            cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    public async Task<RecurringJournalTemplateDto> SubmitAsync(Guid id, string comment, CancellationToken cancellationToken = default)
    {
        var template = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (template.Status is not (RecurringJournalStatus.Draft or RecurringJournalStatus.Rejected))
            throw new InvalidOperationException("Only Draft or Rejected templates can be submitted for activation.");
        RequireComment(comment, "Submission comment");
        await ValidatePersistedDefinitionAsync(template, cancellationToken);
        template.Status = RecurringJournalStatus.PendingApproval;
        template.SubmittedAt = DateTime.UtcNow;
        template.SubmittedByUserId = CurrentUserIdRequired();
        template.ReviewedAt = null;
        template.ReviewedByUserId = null;
        template.ReviewComment = null;
        template.LastFailure = null;
        Touch(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateSubmitted, template.Id, null,
            new { template.Status, template.SubmittedAt, template.SubmittedByUserId }, comment, cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    public async Task<RecurringJournalTemplateDto> ApproveAsync(Guid id, string comment, CancellationToken cancellationToken = default)
    {
        var template = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (template.Status != RecurringJournalStatus.PendingApproval)
            throw new InvalidOperationException("Only a Pending Approval template can be activated.");
        RequireComment(comment, "Approval comment");
        var reviewerId = CurrentUserIdRequired();
        if (reviewerId == template.CreatedById || reviewerId == template.SubmittedByUserId)
            throw new InvalidOperationException("Maker-checker control requires a different user to approve the recurring-journal template.");
        await ValidatePersistedDefinitionAsync(template, cancellationToken);

        template.Status = RecurringJournalStatus.Active;
        template.ReviewedAt = DateTime.UtcNow;
        template.ReviewedByUserId = reviewerId;
        template.ReviewComment = comment.Trim();
        template.ActivatedAt = template.ReviewedAt;
        template.ActivatedByUserId = reviewerId;
        template.NextDueDate = RecurringJournalRecurrenceCalculator.NextScheduledDate(
            template,
            template.EffectiveFrom.AddDays(-1));
        if (!template.NextDueDate.HasValue)
            throw new InvalidOperationException("The approved recurrence rule does not produce an occurrence within its configured limits.");
        Touch(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateApproved, template.Id, null,
            new { template.Status, template.ActivatedAt, template.ActivatedByUserId, template.NextDueDate }, comment,
            cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    public Task<RecurringJournalTemplateDto> RejectAsync(Guid id, string comment, CancellationToken cancellationToken = default) =>
        DecideTemplateAsync(id, comment, RecurringJournalStatus.Rejected, FinanceAuditEvents.RecurringJournalTemplateRejected, cancellationToken);

    public Task<RecurringJournalTemplateDto> PauseAsync(Guid id, string comment, CancellationToken cancellationToken = default) =>
        ChangeOperationalStatusAsync(id, comment, RecurringJournalStatus.Active, RecurringJournalStatus.Paused,
            FinanceAuditEvents.RecurringJournalTemplatePaused, cancellationToken);

    public Task<RecurringJournalTemplateDto> ResumeAsync(Guid id, string comment, CancellationToken cancellationToken = default) =>
        ChangeOperationalStatusAsync(id, comment, RecurringJournalStatus.Paused, RecurringJournalStatus.Active,
            FinanceAuditEvents.RecurringJournalTemplateResumed, cancellationToken);

    public Task<RecurringJournalGenerationResultDto> GenerateDueAsync(DateOnly asOfDate, CancellationToken cancellationToken = default) =>
        _generator.ProcessTenantAsync(TenantId, asOfDate, _currentUser.UserName ?? "Finance user", cancellationToken);

    public Task<RecurringJournalReversalProcessingResultDto> ProcessDueReversalsAsync(
        DateOnly asOfDate, Guid? occurrenceId = null, CancellationToken cancellationToken = default) =>
        _reversals.ProcessTenantAsync(TenantId, asOfDate, occurrenceId, cancellationToken);

    public async Task<RecurringJournalOccurrenceDto> ApproveOccurrenceAsync(
        Guid occurrenceId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        RequireComment(comment, "Occurrence approval comment");
        var occurrence = await LoadOccurrenceAsync(occurrenceId, cancellationToken);
        if (occurrence.Status != RecurringJournalOccurrenceStatus.PendingApproval)
            throw new InvalidOperationException("Only a Pending Approval occurrence can be approved.");
        var reviewerId = CurrentUserIdRequired();
        if (reviewerId == occurrence.Template.CreatedById || reviewerId == occurrence.Template.SubmittedByUserId)
            throw new InvalidOperationException("The template maker cannot approve a generated accounting occurrence.");
        occurrence.Status = RecurringJournalOccurrenceStatus.Approved;
        occurrence.ReviewedAt = DateTime.UtcNow;
        occurrence.ReviewedByUserId = reviewerId;
        occurrence.ReviewComment = comment.Trim();
        if (occurrence.ReversalDueDate.HasValue)
        {
            // The approval screen explicitly tells the checker that this decision
            // authorises both this occurrence and its exact mechanical reversal.
            occurrence.ReversalStatus = RecurringJournalReversalStatus.Scheduled;
            occurrence.ReversalAuthorizedAt = occurrence.ReviewedAt;
            occurrence.ReversalAuthorizedByUserId = reviewerId;
            occurrence.ReversalError = null;
        }
        occurrence.UpdatedAt = DateTime.UtcNow;
        occurrence.UpdatedBy = _currentUser.UserName;
        occurrence.LastModifiedById = reviewerId;
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalOccurrenceApproved, occurrence.Id, null,
            new
            {
                occurrence.Status,
                occurrence.ReviewedAt,
                occurrence.ReviewedByUserId,
                occurrence.ReversalDueDate,
                occurrence.ReversalStatus,
                occurrence.ReversalAuthorizedAt,
                occurrence.ReversalAuthorizedByUserId
            }, comment, cancellationToken);
        return MapOccurrence(occurrence);
    }

    public async Task<RecurringJournalOccurrenceDto> RejectOccurrenceAsync(
        Guid occurrenceId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        RequireComment(comment, "Occurrence rejection reason");
        var occurrence = await LoadOccurrenceAsync(occurrenceId, cancellationToken);
        if (occurrence.Status != RecurringJournalOccurrenceStatus.PendingApproval)
            throw new InvalidOperationException("Only a Pending Approval occurrence can be rejected.");
        var reviewerId = CurrentUserIdRequired();
        if (reviewerId == occurrence.Template.CreatedById || reviewerId == occurrence.Template.SubmittedByUserId)
            throw new InvalidOperationException("The template maker cannot reject a generated accounting occurrence.");
        occurrence.Status = RecurringJournalOccurrenceStatus.Failed;
        occurrence.ReviewedAt = DateTime.UtcNow;
        occurrence.ReviewedByUserId = reviewerId;
        occurrence.ReviewComment = comment.Trim();
        occurrence.ErrorMessage = comment.Trim();
        occurrence.UpdatedAt = DateTime.UtcNow;
        occurrence.UpdatedBy = _currentUser.UserName;
        occurrence.LastModifiedById = reviewerId;
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalOccurrenceRejected, occurrence.Id, null,
            new { occurrence.Status, occurrence.ReviewedAt, occurrence.ReviewedByUserId }, comment, cancellationToken);
        return MapOccurrence(occurrence);
    }

    public async Task<RecurringJournalOccurrenceDto> PostOccurrenceAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await LoadOccurrenceAsync(occurrenceId, cancellationToken);
        if (occurrence.Status is not (RecurringJournalOccurrenceStatus.Approved or RecurringJournalOccurrenceStatus.SubmissionFailed))
            throw new InvalidOperationException("Only an approved recurring-journal occurrence can be posted.");
        var posterId = CurrentUserIdRequired();
        if (posterId == occurrence.ReviewedByUserId)
            throw new InvalidOperationException("Segregation of duties requires a different user to post the approved occurrence.");

        var snapshot = DeserializeSnapshot(occurrence.TemplateSnapshotJson);
        try
        {
            var result = await _postingEngine.PostAsync(new FinancePostingRequestDto
            {
                SourceModule = "GL",
                OriginModuleCode = "FIN",
                SourceDocumentType = nameof(RecurringJournalOccurrence),
                SourceDocumentId = occurrence.Id,
                SourceDocumentTenantId = TenantId,
                PostingAction = "PostRecurringJournalOccurrence",
                SourceDocumentReference = ExpandReference(snapshot.ReferencePattern, snapshot.TemplateNumber, occurrence),
                Description = $"Recurring journal {snapshot.TemplateNumber}: {snapshot.Name}",
                PostingDate = occurrence.EffectiveDate.ToDateTime(TimeOnly.MinValue),
                JournalType = "Recurring",
                BookClassification = snapshot.BookClassification,
                FunctionalCurrencyCode = snapshot.CurrencyCode,
                IdempotencyKey = $"RecurringJournal:{TenantId:N}:{occurrence.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = snapshot.Lines.OrderBy(item => item.LineNumber).Select(line => new FinancePostingLineDto
                {
                    AccountId = line.AccountId,
                    SourceDocumentLineId = line.SourceLineId == Guid.Empty ? null : line.SourceLineId,
                    Description = line.Description ?? snapshot.Name,
                    DebitAmount = line.IsDebit ? line.FixedAmount : 0m,
                    CreditAmount = line.IsDebit ? 0m : line.FixedAmount,
                    TransactionCurrency = snapshot.CurrencyCode,
                    TransactionDebitAmount = line.IsDebit ? line.FixedAmount : 0m,
                    TransactionCreditAmount = line.IsDebit ? 0m : line.FixedAmount,
                    SourceReferenceNumber = ExpandReference(snapshot.ReferencePattern, snapshot.TemplateNumber, occurrence),
                    LineNumber = line.LineNumber,
                    SegmentString = ExtractSegmentString(line.DimensionValuesJson),
                    Notes = line.DimensionValuesJson == "{}" ? null : $"Recurring template dimensions: {line.DimensionValuesJson}"
                }).ToList()
            }, cancellationToken);

            occurrence.JournalEntryId = result.JournalEntryId;
            occurrence.Status = RecurringJournalOccurrenceStatus.Posted;
            occurrence.PostedAt = DateTime.UtcNow;
            occurrence.PostedByUserId = posterId;
            occurrence.ErrorMessage = null;
            occurrence.AttemptCount++;
            occurrence.UpdatedAt = DateTime.UtcNow;
            occurrence.UpdatedBy = _currentUser.UserName;
            occurrence.LastModifiedById = posterId;

            var journal = await _db.JournalEntries.FirstAsync(item => item.Id == result.JournalEntryId && item.TenantId == TenantId, cancellationToken);
            journal.IsRecurring = true;
            journal.RecurringTemplateId = occurrence.TemplateId;
            journal.RecurrenceFrequency = snapshot.Frequency.ToString();
            journal.NextRecurrenceDate = occurrence.Template.NextDueDate?.ToDateTime(TimeOnly.MinValue);
            journal.EntryTag = "Recurring Journal";
            await _db.SaveChangesAsync(cancellationToken);

            await AuditAsync(FinanceAuditEvents.RecurringJournalOccurrencePosted, occurrence.Id, null,
                new { occurrence.Status, occurrence.JournalEntryId, result.PostingEventId, occurrence.PostedAt }, null,
                cancellationToken, result.JournalEntryId, result.PostingEventId);
            return MapOccurrence(occurrence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (occurrence.Status != RecurringJournalOccurrenceStatus.Posted && !occurrence.JournalEntryId.HasValue)
        {
            // The posting engine owns its transaction and may leave tracked
            // entities in Modified state after a rollback. Clear that state and
            // reload only the occurrence before retaining retry diagnostics; this
            // prevents a failed attempt from accidentally saving partial ledger
            // mutations on the error-reporting SaveChanges call.
            _db.ChangeTracker.Clear();
            var failedOccurrence = await LoadOccurrenceAsync(occurrenceId, cancellationToken);
            failedOccurrence.Status = RecurringJournalOccurrenceStatus.SubmissionFailed;
            failedOccurrence.AttemptCount++;
            failedOccurrence.ErrorMessage = Limit(exception.Message, 2000);
            failedOccurrence.UpdatedAt = DateTime.UtcNow;
            failedOccurrence.UpdatedBy = _currentUser.UserName;
            failedOccurrence.LastModifiedById = posterId;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(FinanceAuditEvents.RecurringJournalGenerationFailed, failedOccurrence.Id, null,
                new { failedOccurrence.Status, failedOccurrence.ErrorMessage, failedOccurrence.AttemptCount }, exception.Message, cancellationToken);
            throw;
        }
    }

    private async Task<RecurringJournalTemplateDto> DecideTemplateAsync(
        Guid id, string comment, RecurringJournalStatus targetStatus, string auditEvent,
        CancellationToken cancellationToken)
    {
        RequireComment(comment, "Decision reason");
        var template = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (template.Status != RecurringJournalStatus.PendingApproval)
            throw new InvalidOperationException("Only a Pending Approval template can be rejected.");
        var reviewerId = CurrentUserIdRequired();
        if (reviewerId == template.CreatedById || reviewerId == template.SubmittedByUserId)
            throw new InvalidOperationException("Maker-checker control requires a different user to decide the template.");
        template.Status = targetStatus;
        template.ReviewedAt = DateTime.UtcNow;
        template.ReviewedByUserId = reviewerId;
        template.ReviewComment = comment.Trim();
        template.LastFailure = comment.Trim();
        Touch(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(auditEvent, template.Id, null,
            new { template.Status, template.ReviewedAt, template.ReviewedByUserId }, comment, cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    private async Task<RecurringJournalTemplateDto> ChangeOperationalStatusAsync(
        Guid id, string comment, RecurringJournalStatus expected, RecurringJournalStatus target,
        string auditEvent, CancellationToken cancellationToken)
    {
        RequireComment(comment, "Operational reason");
        var template = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (template.Status != expected)
            throw new InvalidOperationException($"Only an {expected} recurring-journal template can transition to {target}.");
        template.Status = target;
        Touch(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(auditEvent, template.Id, new { Status = expected }, new { Status = target }, comment, cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    private async Task ValidateDefinitionAsync(
        CreateRecurringJournalTemplateDto request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new InvalidOperationException("Recurring-journal name is required and cannot exceed 200 characters.");
        if (request.EffectiveFrom == default)
            throw new InvalidOperationException("An effective start date is required.");
        if (request.EndDate.HasValue && request.EndDate < request.EffectiveFrom)
            throw new InvalidOperationException("End date cannot precede the effective start date.");
        if (request.Interval < 1 || request.Interval > 366)
            throw new InvalidOperationException("Recurrence interval must be between 1 and 366.");
        if (request.MaximumOccurrences is <= 0)
            throw new InvalidOperationException("Maximum occurrences must be positive when supplied.");
        if (request.Lines.Count < 2)
            throw new InvalidOperationException("A recurring journal requires at least two balanced lines.");
        if (request.Lines.Any(line => line.AccountId == Guid.Empty || line.FixedAmount <= 0))
            throw new InvalidOperationException("Every recurring-journal line requires an account and a positive fixed amount.");

        var debit = RoundMoney(request.Lines.Where(line => line.IsDebit).Sum(line => line.FixedAmount));
        var credit = RoundMoney(request.Lines.Where(line => !line.IsDebit).Sum(line => line.FixedAmount));
        if (debit <= 0 || debit != credit)
            throw new InvalidOperationException($"Recurring-journal lines must balance. Debit {debit:N2}; credit {credit:N2}.");

        var accountIds = request.Lines.Select(line => line.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(account => account.TenantId == tenantId && accountIds.Contains(account.Id) && !account.IsDeleted)
            .ToListAsync(cancellationToken);
        if (accounts.Count != accountIds.Count)
            throw new InvalidOperationException("One or more recurring-journal accounts do not belong to the current tenant.");
        var invalid = accounts.FirstOrDefault(account =>
            account.Status != AccountStatus.Active || !account.AllowDirectPosting || account.IsControlAccount);
        if (invalid != null)
            throw new InvalidOperationException($"Account {invalid.AccountCode} must be active, directly postable, and not a subledger control account.");

        var functionalCurrency = await ResolveFunctionalCurrencyAsync(tenantId, cancellationToken);
        var requestedCurrency = NormalizeCurrency(request.CurrencyCode);
        if (!string.Equals(functionalCurrency, requestedCurrency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This recurring-journal slice supports the tenant functional currency only. Foreign-currency templates require rate-snapshot rules.");

        _ = NormalizeBook(request.BookClassification);
        _ = RecurringJournalRecurrenceCalculator.ParseRule(request.RecurrenceRuleJson);
        foreach (var line in request.Lines) ValidateDimensionJson(line.DimensionValuesJson);
        if (request.AutoReverse && request.ReversalRule == RecurringJournalReversalRule.None)
            throw new InvalidOperationException("Automatic reversal requires an explicit reversal rule.");
        if (request.AutoReverse && request.ReversalRule == RecurringJournalReversalRule.DayOffset && request.ReversalDayOffset is not (> 0 and <= 366))
            throw new InvalidOperationException("Reversal day offset must be between 1 and 366.");

        var probe = new RecurringJournalTemplate
        {
            TenantId = tenantId,
            EffectiveFrom = request.EffectiveFrom,
            EndDate = request.EndDate,
            MaximumOccurrences = request.MaximumOccurrences,
            Frequency = request.Frequency,
            Interval = request.Interval,
            RecurrenceRuleJson = request.RecurrenceRuleJson
        };
        if (!RecurringJournalRecurrenceCalculator.NextScheduledDate(probe, request.EffectiveFrom.AddDays(-1)).HasValue)
            throw new InvalidOperationException("The recurrence rule does not produce a date within the configured limits.");
    }

    private Task ValidatePersistedDefinitionAsync(RecurringJournalTemplate template, CancellationToken cancellationToken) =>
        ValidateDefinitionAsync(new CreateRecurringJournalTemplateDto
        {
            Name = template.Name,
            Description = template.Description,
            JournalType = template.JournalType,
            BookClassification = template.BookClassification,
            CurrencyCode = template.CurrencyCode,
            ReferencePattern = template.ReferencePattern,
            Notes = template.Notes,
            OwnerUserId = template.OwnerUserId,
            EffectiveFrom = template.EffectiveFrom,
            EndDate = template.EndDate,
            MaximumOccurrences = template.MaximumOccurrences,
            TimeZoneId = template.TimeZoneId,
            Frequency = template.Frequency,
            Interval = template.Interval,
            RecurrenceRuleJson = template.RecurrenceRuleJson,
            BusinessDayConvention = template.BusinessDayConvention,
            AutoReverse = template.AutoReverse,
            ReversalRule = template.ReversalRule,
            ReversalDayOffset = template.ReversalDayOffset,
            Lines = template.Lines.OrderBy(line => line.LineNumber).Select(line => new RecurringJournalTemplateLineInputDto
            {
                AccountId = line.AccountId,
                IsDebit = line.IsDebit,
                FixedAmount = line.FixedAmount,
                Description = line.Description,
                DimensionValuesJson = line.DimensionValuesJson
            }).ToList()
        }, template.TenantId, cancellationToken);

    private async Task<RecurringJournalTemplate> LoadAsync(Guid id, bool includeOccurrences, CancellationToken cancellationToken)
    {
        IQueryable<RecurringJournalTemplate> query = _db.RecurringJournalTemplates.Include(item => item.Lines);
        if (includeOccurrences) query = query.Include(item => item.Occurrences);
        return await query.FirstOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Recurring-journal template was not found for the current tenant.");
    }

    private async Task<RecurringJournalOccurrence> LoadOccurrenceAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.RecurringJournalOccurrences.Include(item => item.Template)
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
        ?? throw new KeyNotFoundException("Recurring-journal occurrence was not found for the current tenant.");

    private async Task<RecurringJournalTemplateDto?> MapAsync(Guid id, bool includeOccurrences, CancellationToken cancellationToken)
    {
        IQueryable<RecurringJournalTemplate> query = _db.RecurringJournalTemplates.AsNoTracking().Include(item => item.Lines);
        if (includeOccurrences) query = query.Include(item => item.Occurrences);
        var template = await query.FirstOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (template == null) return null;
        var accountIds = template.Lines.Select(line => line.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking().Where(account => account.TenantId == TenantId && accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);
        return new RecurringJournalTemplateDto
        {
            Id = template.Id, TemplateNumber = template.TemplateNumber, Name = template.Name, Description = template.Description,
            JournalType = template.JournalType, BookClassification = template.BookClassification, CurrencyCode = template.CurrencyCode,
            ReferencePattern = template.ReferencePattern, Notes = template.Notes, Status = template.Status, Version = template.Version,
            EffectiveFrom = template.EffectiveFrom, EndDate = template.EndDate, MaximumOccurrences = template.MaximumOccurrences,
            GeneratedOccurrenceCount = template.GeneratedOccurrenceCount, TimeZoneId = template.TimeZoneId, Frequency = template.Frequency,
            Interval = template.Interval, RecurrenceRuleJson = template.RecurrenceRuleJson, BusinessDayConvention = template.BusinessDayConvention,
            NextDueDate = template.NextDueDate, LastGeneratedDueDate = template.LastGeneratedDueDate, AutoReverse = template.AutoReverse,
            ReversalRule = template.ReversalRule, ReversalDayOffset = template.ReversalDayOffset, OwnerUserId = template.OwnerUserId,
            SubmittedAt = template.SubmittedAt, SubmittedByUserId = template.SubmittedByUserId, ReviewedAt = template.ReviewedAt,
            ReviewedByUserId = template.ReviewedByUserId, ReviewComment = template.ReviewComment, ActivatedAt = template.ActivatedAt,
            ActivatedByUserId = template.ActivatedByUserId, ConsecutiveFailureCount = template.ConsecutiveFailureCount,
            LastFailure = template.LastFailure,
            ExceptionCount = includeOccurrences ? template.Occurrences.Count(item => !item.IsDeleted &&
                item.Status is RecurringJournalOccurrenceStatus.Failed or RecurringJournalOccurrenceStatus.SubmissionFailed) : 0,
            TotalDebit = RoundMoney(template.Lines.Where(line => line.IsDebit).Sum(line => line.FixedAmount)),
            TotalCredit = RoundMoney(template.Lines.Where(line => !line.IsDebit).Sum(line => line.FixedAmount)),
            RowVersion = Convert.ToBase64String(template.RowVersion),
            Lines = template.Lines.OrderBy(line => line.LineNumber).Select(line => new RecurringJournalTemplateLineDto
            {
                Id = line.Id, LineNumber = line.LineNumber, AccountId = line.AccountId,
                AccountCode = accounts.GetValueOrDefault(line.AccountId)?.AccountCode ?? string.Empty,
                AccountName = accounts.GetValueOrDefault(line.AccountId)?.AccountName ?? string.Empty,
                IsDebit = line.IsDebit, FixedAmount = line.FixedAmount, Description = line.Description,
                DimensionValuesJson = line.DimensionValuesJson
            }).ToList(),
            Occurrences = includeOccurrences
                ? template.Occurrences.Where(item => !item.IsDeleted).OrderByDescending(item => item.ScheduledDate).Select(MapOccurrence).ToList()
                : []
        };
    }

    private async Task<RecurringJournalTemplateDto> RequireMappedAsync(Guid id, CancellationToken cancellationToken) =>
        await MapAsync(id, includeOccurrences: true, cancellationToken)
        ?? throw new InvalidOperationException("Recurring-journal template could not be reloaded.");

    private static RecurringJournalOccurrenceDto MapOccurrence(RecurringJournalOccurrence item) => new()
    {
        Id = item.Id, TemplateVersion = item.TemplateVersion, SequenceNumber = item.SequenceNumber,
        ScheduledDate = item.ScheduledDate, EffectiveDate = item.EffectiveDate, Status = item.Status,
        JournalEntryId = item.JournalEntryId, ReversalJournalEntryId = item.ReversalJournalEntryId,
        ReversalDueDate = item.ReversalDueDate, AttemptCount = item.AttemptCount, GeneratedAt = item.GeneratedAt,
        ReversalStatus = item.ReversalStatus, ReversalAuthorizedAt = item.ReversalAuthorizedAt,
        ReversalAuthorizedByUserId = item.ReversalAuthorizedByUserId, ReversalAttemptCount = item.ReversalAttemptCount,
        ReversalLastAttemptAt = item.ReversalLastAttemptAt, ReversalError = item.ReversalError,
        ReversalPostingEventId = item.ReversalPostingEventId, ReversalProcessedBy = item.ReversalProcessedBy,
        ReviewedAt = item.ReviewedAt, ReviewedByUserId = item.ReviewedByUserId, ReviewComment = item.ReviewComment,
        PostedAt = item.PostedAt, PostedByUserId = item.PostedByUserId, ReversedAt = item.ReversedAt,
        ErrorMessage = item.ErrorMessage, AdjustmentExplanation = item.AdjustmentExplanation
    };

    private static void ApplyDefinition(RecurringJournalTemplate template, CreateRecurringJournalTemplateDto request)
    {
        template.Name = request.Name.Trim();
        template.Description = NormalizeOptional(request.Description, 1000);
        template.JournalType = string.IsNullOrWhiteSpace(request.JournalType) ? "Recurring" : Limit(request.JournalType.Trim(), 50);
        template.BookClassification = NormalizeBook(request.BookClassification);
        template.CurrencyCode = NormalizeCurrency(request.CurrencyCode);
        template.ReferencePattern = NormalizeOptional(request.ReferencePattern, 200);
        template.Notes = NormalizeOptional(request.Notes, 2000);
        template.OwnerUserId = request.OwnerUserId;
        template.EffectiveFrom = request.EffectiveFrom;
        template.EndDate = request.EndDate;
        template.MaximumOccurrences = request.MaximumOccurrences;
        template.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Africa/Accra" : Limit(request.TimeZoneId.Trim(), 100);
        template.Frequency = request.Frequency;
        template.Interval = request.Interval;
        template.RecurrenceRuleJson = string.IsNullOrWhiteSpace(request.RecurrenceRuleJson) ? "{}" : request.RecurrenceRuleJson.Trim();
        template.BusinessDayConvention = request.BusinessDayConvention;
        template.AutoReverse = request.AutoReverse;
        template.ReversalRule = request.AutoReverse ? request.ReversalRule : RecurringJournalReversalRule.None;
        template.ReversalDayOffset = request.AutoReverse ? request.ReversalDayOffset : null;
    }

    private static void ReplaceLines(RecurringJournalTemplate template, IReadOnlyList<RecurringJournalTemplateLineInputDto> lines,
        DateTime now, Guid? userId, string? userName)
    {
        var number = 1;
        foreach (var input in lines)
        {
            template.Lines.Add(new RecurringJournalTemplateLine
            {
                Id = Guid.NewGuid(), TenantId = template.TenantId, TemplateId = template.Id, LineNumber = number++,
                AccountId = input.AccountId, IsDebit = input.IsDebit, FixedAmount = RoundMoney(input.FixedAmount),
                Description = NormalizeOptional(input.Description, 500),
                DimensionValuesJson = string.IsNullOrWhiteSpace(input.DimensionValuesJson) ? "{}" : input.DimensionValuesJson.Trim(),
                CreatedAt = now, CreatedBy = userName, CreatedById = userId
            });
        }
    }

    private async Task<string> ResolveFunctionalCurrencyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var configured = await _db.FinanceSettings.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(configured)) return NormalizeCurrency(configured);
        var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == tenantId)
            .Select(item => item.BaseCurrency).FirstOrDefaultAsync(cancellationToken);
        return NormalizeCurrency(tenantCurrency ?? "GHS");
    }

    private void Touch(RecurringJournalTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = _currentUser.UserName;
        template.LastModifiedById = CurrentUserId();
    }

    private void SetRowVersion(RecurringJournalTemplate template, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Row version is required.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(token); }
        catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
        if (bytes.Length != 8) throw new InvalidOperationException("Row version is invalid.");
        // RowVersion is an optimistic-concurrency token. The value supplied by
        // the editor must be EF's original value; assigning it as the current
        // value can hide an intervening approval or edit made by another user.
        _db.Entry(template).Property(item => item.RowVersion).OriginalValue = bytes;
    }

    private Guid? CurrentUserId() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : null;
    private Guid CurrentUserIdRequired() => CurrentUserId() ?? throw new InvalidOperationException("An authenticated user is required.");

    private async Task AuditAsync(string eventType, Guid sourceId, object? before, object? after, string? comment,
        CancellationToken cancellationToken, Guid? journalEntryId = null, Guid? postingEventId = null) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType, TenantId = TenantId, SourceModule = "GL", SourceDocumentType = "RecurringJournal",
            SourceDocumentId = sourceId, JournalEntryId = journalEntryId, PostingEventId = postingEventId,
            BeforeValues = before, AfterValues = after, Comment = comment, Reason = comment,
            Resource = "Finance.RecurringJournal", ResourceId = sourceId.ToString()
        }, cancellationToken);

    private static RecurringJournalSnapshot DeserializeSnapshot(string json) =>
        JsonSerializer.Deserialize<RecurringJournalSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("The recurring-journal occurrence snapshot is missing or invalid.");

    private static string ExpandReference(string? pattern, string templateNumber, RecurringJournalOccurrence occurrence)
    {
        var value = string.IsNullOrWhiteSpace(pattern) ? $"{templateNumber}-{{Sequence}}" : pattern;
        return Limit(value.Replace("{TemplateNumber}", templateNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{Period}", occurrence.EffectiveDate.ToString("yyyyMM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{ScheduledDate}", occurrence.ScheduledDate.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Sequence}", occurrence.SequenceNumber.ToString("D4"), StringComparison.OrdinalIgnoreCase), 100);
    }

    private static string? ExtractSegmentString(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("segmentString", out var value) ? value.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static void ValidateDimensionJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Line dimension values must be a JSON object.");
        }
        catch (JsonException exception) { throw new InvalidOperationException("Line dimension values contain invalid JSON.", exception); }
    }

    private static void RequireComment(string? comment, string label)
    {
        if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length < 10)
            throw new InvalidOperationException($"{label} must contain at least 10 characters.");
        if (comment.Trim().Length > 1000) throw new InvalidOperationException($"{label} cannot exceed 1000 characters.");
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            throw new InvalidOperationException("A valid three-letter currency code is required.");
        return normalized;
    }

    private static string NormalizeBook(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "IFRS" : value.Trim().ToUpperInvariant();
        if (normalized is not ("IFRS" or "LOCAL_STATUTORY" or "MANAGEMENT"))
            throw new InvalidOperationException("Recurring journals require one explicit IFRS, LOCAL_STATUTORY, or MANAGEMENT book.");
        return normalized;
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
    private static string? NormalizeOptional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Limit(value.Trim(), max);
}

/// <summary>
/// Idempotently materializes due schedule occurrences without posting money. This
/// processor is safe for request and background execution because every query uses
/// an explicit tenant predicate and the database enforces one occurrence per
/// template/scheduled date. Generated occurrences still require human approval.
/// </summary>
public sealed class RecurringJournalGenerationProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendarProvider _calendar;
    private readonly IFinanceAuditService _audit;
    private readonly ILogger<RecurringJournalGenerationProcessor> _logger;

    public RecurringJournalGenerationProcessor(ApplicationDbContext db, IBusinessCalendarProvider calendar,
        IFinanceAuditService audit, ILogger<RecurringJournalGenerationProcessor> logger)
    {
        _db = db;
        _calendar = calendar;
        _audit = audit;
        _logger = logger;
    }

    public async Task<RecurringJournalGenerationResultDto> ProcessAllAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var tenants = await _db.RecurringJournalTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status == RecurringJournalStatus.Active && item.NextDueDate <= asOfDate)
            .Select(item => item.TenantId).Distinct().ToListAsync(cancellationToken);
        var total = new RecurringJournalGenerationResultDto();
        foreach (var tenantId in tenants)
        {
            var result = await ProcessTenantAsync(tenantId, asOfDate, "RecurringJournalScheduler", cancellationToken);
            total.TemplateCount += result.TemplateCount;
            total.GeneratedCount += result.GeneratedCount;
            total.ExistingCount += result.ExistingCount;
            total.FailedCount += result.FailedCount;
        }
        return total;
    }

    public async Task<RecurringJournalGenerationResultDto> ProcessTenantAsync(
        Guid tenantId, DateOnly asOfDate, string actor, CancellationToken cancellationToken = default)
    {
        var result = new RecurringJournalGenerationResultDto();
        var templateIds = await _db.RecurringJournalTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Status == RecurringJournalStatus.Active && item.NextDueDate <= asOfDate)
            .OrderBy(item => item.NextDueDate).Select(item => item.Id).Take(250).ToListAsync(cancellationToken);
        result.TemplateCount = templateIds.Count;

        foreach (var templateId in templateIds)
        {
            try
            {
                var template = await _db.RecurringJournalTemplates.IgnoreQueryFilters().Include(item => item.Lines)
                    .FirstAsync(item => item.Id == templateId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
                var safety = 0;
                while (template.Status == RecurringJournalStatus.Active && template.NextDueDate is { } scheduled && scheduled <= asOfDate)
                {
                    if (++safety > 120) throw new InvalidOperationException("Recurring-journal catch-up exceeded 120 occurrences in one run.");
                    var existing = await _db.RecurringJournalOccurrences.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(item =>
                        item.TenantId == tenantId && item.TemplateId == template.Id && item.ScheduledDate == scheduled && !item.IsDeleted,
                        cancellationToken);
                    if (existing != null)
                    {
                        // Heal a rare partial/concurrent run where the occurrence
                        // committed but this template cursor was not yet observed.
                        // The unique key remains the authority; advancing from the
                        // retained date stops the scheduler looping forever.
                        result.ExistingCount++;
                        template.LastGeneratedDueDate = scheduled;
                        template.GeneratedOccurrenceCount = Math.Max(template.GeneratedOccurrenceCount, existing.SequenceNumber);
                        template.NextDueDate = RecurringJournalRecurrenceCalculator.NextScheduledDate(template, scheduled);
                        if (!template.NextDueDate.HasValue) template.Status = RecurringJournalStatus.Completed;
                        await _db.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    var effective = await RecurringJournalRecurrenceCalculator.AdjustBusinessDayAsync(
                        tenantId, scheduled, template.BusinessDayConvention, _calendar, cancellationToken);
                    var occurrence = new RecurringJournalOccurrence
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, TemplateId = template.Id, TemplateVersion = template.Version,
                        SequenceNumber = template.GeneratedOccurrenceCount + 1, ScheduledDate = scheduled, EffectiveDate = effective,
                        Status = RecurringJournalOccurrenceStatus.PendingApproval, AttemptCount = 0, GeneratedAt = DateTime.UtcNow,
                        AdjustmentExplanation = effective == scheduled ? null :
                            $"{scheduled:yyyy-MM-dd} adjusted to {effective:yyyy-MM-dd} using {template.BusinessDayConvention}.",
                        TemplateSnapshotJson = JsonSerializer.Serialize(RecurringJournalSnapshot.From(template), JsonOptions),
                        ReversalDueDate = await ResolveReversalDueDateAsync(template, effective, cancellationToken),
                        CreatedAt = DateTime.UtcNow, CreatedBy = actor
                    };
                    occurrence.ReversalStatus = occurrence.ReversalDueDate.HasValue
                        ? RecurringJournalReversalStatus.PendingAuthorization
                        : RecurringJournalReversalStatus.NotApplicable;
                    _db.RecurringJournalOccurrences.Add(occurrence);
                    template.GeneratedOccurrenceCount++;
                    template.LastGeneratedDueDate = scheduled;
                    template.NextDueDate = RecurringJournalRecurrenceCalculator.NextScheduledDate(template, scheduled);
                    template.ConsecutiveFailureCount = 0;
                    template.LastFailure = null;
                    template.UpdatedAt = DateTime.UtcNow;
                    template.UpdatedBy = actor;
                    if (!template.NextDueDate.HasValue) template.Status = RecurringJournalStatus.Completed;
                    await _db.SaveChangesAsync(cancellationToken);
                    result.GeneratedCount++;
                    await RecordGenerationAuditAsync(tenantId, occurrence, actor, cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _db.ChangeTracker.Clear();
                result.ExistingCount++;
                _logger.LogInformation(exception, "A concurrent scheduler already advanced recurring template {TemplateId}.", templateId);
            }
            catch (DbUpdateException exception)
            {
                _db.ChangeTracker.Clear();
                result.ExistingCount++;
                _logger.LogInformation(exception, "A recurring occurrence already exists for template {TemplateId}.", templateId);
            }
            catch (Exception exception)
            {
                _db.ChangeTracker.Clear();
                result.FailedCount++;
                _logger.LogError(exception, "Recurring-journal generation failed for template {TemplateId}.", templateId);
                var failed = await _db.RecurringJournalTemplates.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(item => item.Id == templateId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
                if (failed != null)
                {
                    failed.ConsecutiveFailureCount++;
                    failed.LastFailure = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
                    failed.UpdatedAt = DateTime.UtcNow;
                    failed.UpdatedBy = actor;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        return result;
    }

    private async Task<DateOnly?> ResolveReversalDueDateAsync(RecurringJournalTemplate template, DateOnly effective,
        CancellationToken cancellationToken)
    {
        if (!template.AutoReverse || template.ReversalRule == RecurringJournalReversalRule.None) return null;
        if (template.ReversalRule == RecurringJournalReversalRule.NextCalendarDay) return effective.AddDays(1);
        if (template.ReversalRule == RecurringJournalReversalRule.DayOffset) return effective.AddDays(template.ReversalDayOffset ?? 1);
        var effectiveDate = effective.ToDateTime(TimeOnly.MinValue);
        var nextPeriod = await _db.FiscalPeriods.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == template.TenantId && !item.IsDeleted && item.StartDate > effectiveDate)
            .OrderBy(item => item.StartDate).Select(item => (DateTime?)item.StartDate).FirstOrDefaultAsync(cancellationToken);
        return nextPeriod.HasValue ? DateOnly.FromDateTime(nextPeriod.Value) : null;
    }

    private async Task RecordGenerationAuditAsync(Guid tenantId, RecurringJournalOccurrence occurrence, string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            await _audit.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.RecurringJournalOccurrenceGenerated, TenantId = tenantId,
                SourceModule = "GL", SourceDocumentType = nameof(RecurringJournalOccurrence), SourceDocumentId = occurrence.Id,
                AfterValues = new { occurrence.TemplateId, occurrence.TemplateVersion, occurrence.SequenceNumber,
                    occurrence.ScheduledDate, occurrence.EffectiveDate, occurrence.Status, occurrence.ReversalDueDate },
                Resource = "Finance.RecurringJournalOccurrence", ResourceId = occurrence.Id.ToString()
            }, cancellationToken);
        }
        catch (InvalidOperationException exception) when (actor == "RecurringJournalScheduler")
        {
            // FinanceAuditService intentionally requires an authenticated user,
            // while a host worker has no request principal to impersonate. The
            // occurrence row is itself durable system evidence (actor, snapshot,
            // timestamps and dates); do not falsely attribute it to a human or
            // roll it back. Manual catch-up runs still require the normal audit.
            _logger.LogWarning(exception,
                "Recurring occurrence {OccurrenceId} was generated by the system worker; the occurrence record is the durable generation evidence.",
                occurrence.Id);
        }
    }
}

internal sealed record RecurringJournalSnapshot(
    string TemplateNumber,
    string Name,
    string BookClassification,
    string CurrencyCode,
    string? ReferencePattern,
    RecurrenceFrequency Frequency,
    bool AutoReverse,
    RecurringJournalReversalRule ReversalRule,
    int? ReversalDayOffset,
    IReadOnlyList<RecurringJournalSnapshotLine> Lines)
{
    public static RecurringJournalSnapshot From(RecurringJournalTemplate template) => new(
        template.TemplateNumber, template.Name, template.BookClassification, template.CurrencyCode,
        template.ReferencePattern, template.Frequency, template.AutoReverse, template.ReversalRule,
        template.ReversalDayOffset, template.Lines.OrderBy(line => line.LineNumber).Select(line =>
            new RecurringJournalSnapshotLine(line.Id, line.LineNumber, line.AccountId, line.IsDebit, line.FixedAmount,
                line.Description, line.DimensionValuesJson)).ToList());
}

internal sealed record RecurringJournalSnapshotLine(
    Guid SourceLineId,
    int LineNumber,
    Guid AccountId,
    bool IsDebit,
    decimal FixedAmount,
    string? Description,
    string DimensionValuesJson);
