using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly IWorkflowService _workflow;
    private readonly RecurringJournalGenerationProcessor _generator;
    private readonly RecurringJournalReversalProcessor _reversals;

    public RecurringJournalService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IDocumentNumberingService numbering,
        IFinancePostingEngine postingEngine,
        IFinanceAuditService audit,
        IWorkflowService workflow,
        RecurringJournalGenerationProcessor generator,
        RecurringJournalReversalProcessor reversals)
    {
        _db = db;
        _currentUser = currentUser;
        _numbering = numbering;
        _postingEngine = postingEngine;
        _audit = audit;
        _workflow = workflow;
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
        RecurringJournalLineDefinitionEditor.Apply(template, request.Lines, now, userId, template.CreatedBy,
            allowExistingLineIds: false);
        _db.RecurringJournalTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateCreated, template.Id, null,
            new { template.TemplateNumber, template.Name, template.Frequency, lineCount = template.Lines.Count(line => !line.IsDeleted) }, null,
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
        var before = new { template.Name, template.Frequency, template.EffectiveFrom, template.EndDate, lineCount = template.Lines.Count(line => !line.IsDeleted) };
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
        var lineEdit = RecurringJournalLineDefinitionEditor.Apply(template, request.Lines, now, userId, template.UpdatedBy,
            allowExistingLineIds: true);
        _db.RecurringJournalTemplateLines.AddRange(lineEdit.AddedLines);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateUpdated, template.Id, before,
            new { template.Name, template.Frequency, template.EffectiveFrom, template.EndDate, lineCount = template.Lines.Count(line => !line.IsDeleted) }, null,
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
        var workflowResult = await _workflow.StartApprovalWorkflowAsync(nameof(RecurringJournalTemplate), template.Id);
        if (!workflowResult.Success)
        {
            template.Status = RecurringJournalStatus.Draft;
            template.SubmittedAt = null;
            template.SubmittedByUserId = null;
            Touch(template);
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(workflowResult.Message ?? "The recurring-journal approval workflow could not be started.");
        }
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateSubmitted, template.Id, null,
            new { template.Status, template.SubmittedAt, template.SubmittedByUserId, workflowResult.WorkflowInstanceId }, comment, cancellationToken);
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
        if (!await _workflow.CanUserApproveAsync(nameof(RecurringJournalTemplate), template.Id, reviewerId))
            throw new UnauthorizedAccessException("This recurring-journal approval is not assigned to the current user or their roles.");
        var workflowResult = await _workflow.ProcessApprovalStepAsync(
            nameof(RecurringJournalTemplate), template.Id, reviewerId, "Approve", comment.Trim());
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "The recurring-journal approval step failed.");
        if (workflowResult.Status == WorkflowInstanceStatus.Completed)
            await ApplyApprovedTemplateOutcomeAsync(template, reviewerId, comment, cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    public Task<RecurringJournalTemplateDto> RejectAsync(Guid id, string comment, CancellationToken cancellationToken = default) =>
        DecideTemplateAsync(id, comment, cancellationToken);

    public Task<RecurringJournalTemplateDto> PauseAsync(Guid id, string comment, CancellationToken cancellationToken = default) =>
        ChangeOperationalStatusAsync(id, comment, RecurringJournalStatus.Active, RecurringJournalStatus.Paused,
            FinanceAuditEvents.RecurringJournalTemplatePaused, cancellationToken);

    public Task<RecurringJournalTemplateDto> ResumeAsync(Guid id, string comment, CancellationToken cancellationToken = default) =>
        ChangeOperationalStatusAsync(id, comment, RecurringJournalStatus.Paused, RecurringJournalStatus.Active,
            FinanceAuditEvents.RecurringJournalTemplateResumed, cancellationToken);

    public async Task<RecurringJournalTemplateDto> CancelAsync(
        Guid id, string comment, CancellationToken cancellationToken = default)
    {
        RequireComment(comment, "Cancellation reason");
        var template = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (template.Status is not (RecurringJournalStatus.Draft or RecurringJournalStatus.Rejected or
            RecurringJournalStatus.Active or RecurringJournalStatus.Paused))
            throw new InvalidOperationException("Only Draft, Rejected, Active, or Paused templates can be cancelled.");
        var previous = template.Status;
        template.Status = RecurringJournalStatus.Cancelled;
        template.NextDueDate = null;
        template.LastFailure = comment.Trim();
        Touch(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateCancelled, template.Id,
            new { Status = previous }, new { template.Status }, comment, cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    public async Task<RecurringJournalTemplateDto> CreateNewVersionAsync(
        Guid id, string comment, CancellationToken cancellationToken = default)
    {
        RequireComment(comment, "Version reason");
        var source = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (source.Status is not (RecurringJournalStatus.Active or RecurringJournalStatus.Paused or RecurringJournalStatus.Completed))
            throw new InvalidOperationException("A new version can be created only from an Active, Paused, or Completed template.");
        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        var userId = CurrentUserIdRequired();
        var nextVersion = await _db.RecurringJournalTemplates.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.DefinitionKey == source.DefinitionKey && !item.IsDeleted)
            .MaxAsync(item => (int?)item.Version, cancellationToken) + 1 ?? source.Version + 1;
        var version = new RecurringJournalTemplate
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            TemplateNumber = await _numbering.GenerateAsync(DocumentNumberingModules.Finance,
                FinanceDocumentTypes.RecurringJournal, tenantId, DateTime.UtcNow,
                nameof(RecurringJournalTemplate), cancellationToken: cancellationToken),
            DefinitionKey = source.DefinitionKey, Version = nextVersion,
            SupersedesTemplateId = source.Id, Status = RecurringJournalStatus.Draft,
            CreatedAt = now, CreatedBy = _currentUser.UserName ?? "Finance user", CreatedById = userId
        };
        ApplyDefinition(version, new CreateRecurringJournalTemplateDto
        {
            Name = source.Name, Description = source.Description, JournalType = source.JournalType,
            BookClassification = source.BookClassification, CurrencyCode = source.CurrencyCode,
            ReferencePattern = source.ReferencePattern, Notes = source.Notes, OwnerUserId = source.OwnerUserId,
            EffectiveFrom = source.EffectiveFrom, EndDate = source.EndDate, MaximumOccurrences = source.MaximumOccurrences,
            TimeZoneId = source.TimeZoneId, Frequency = source.Frequency, Interval = source.Interval,
            RecurrenceRuleJson = source.RecurrenceRuleJson, BusinessDayConvention = source.BusinessDayConvention,
            AutoReverse = source.AutoReverse, ReversalRule = source.ReversalRule,
            ReversalDayOffset = source.ReversalDayOffset
        });
        RecurringJournalLineDefinitionEditor.Apply(version, source.Lines.Where(line => !line.IsDeleted)
            .OrderBy(line => line.LineNumber).Select(line => new RecurringJournalTemplateLineInputDto
            {
                AccountId = line.AccountId, IsDebit = line.IsDebit, FixedAmount = line.FixedAmount,
                Description = line.Description, DimensionValuesJson = line.DimensionValuesJson
            }).ToList(), now, userId, version.CreatedBy, allowExistingLineIds: false);
        _db.RecurringJournalTemplates.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateVersionCreated, version.Id,
            null, new { version.TemplateNumber, version.Version, version.SupersedesTemplateId, version.DefinitionKey },
            comment, cancellationToken);
        return await RequireMappedAsync(version.Id, cancellationToken);
    }

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
        if (!await _workflow.CanUserApproveAsync(nameof(RecurringJournalOccurrence), occurrence.Id, reviewerId))
            throw new UnauthorizedAccessException("This occurrence approval is not assigned to the current user or their roles.");
        var workflowResult = await _workflow.ProcessApprovalStepAsync(
            nameof(RecurringJournalOccurrence), occurrence.Id, reviewerId, "Approve", comment.Trim());
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "The occurrence approval step failed.");
        if (workflowResult.Status == WorkflowInstanceStatus.Completed)
            await ApplyApprovedOccurrenceOutcomeAsync(occurrence, reviewerId, comment, cancellationToken);
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
        if (!await _workflow.CanUserApproveAsync(nameof(RecurringJournalOccurrence), occurrence.Id, reviewerId))
            throw new UnauthorizedAccessException("This occurrence decision is not assigned to the current user or their roles.");
        var workflowResult = await _workflow.ProcessApprovalStepAsync(
            nameof(RecurringJournalOccurrence), occurrence.Id, reviewerId, "Reject", comment.Trim());
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "The occurrence rejection step failed.");
        if (workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
            await ApplyRejectedOccurrenceOutcomeAsync(occurrence, reviewerId, comment, cancellationToken);
        return MapOccurrence(occurrence);
    }

    public async Task<RecurringJournalOccurrenceDto> RequestOccurrenceWaiverAsync(
        Guid occurrenceId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        RequireComment(comment, "Waiver reason");
        var occurrence = await LoadOccurrenceAsync(occurrenceId, cancellationToken);
        if (occurrence.JournalEntryId.HasValue || occurrence.Status == RecurringJournalOccurrenceStatus.Posted)
            throw new InvalidOperationException("A posted occurrence cannot be waived; use the controlled reversal process.");
        if (occurrence.Status is not (RecurringJournalOccurrenceStatus.PendingApproval or
            RecurringJournalOccurrenceStatus.Approved or RecurringJournalOccurrenceStatus.Failed or
            RecurringJournalOccurrenceStatus.SubmissionFailed))
            throw new InvalidOperationException("This occurrence is not eligible for a waiver request.");
        var previous = occurrence.Status;
        var workflowResult = await _workflow.StartApprovalWorkflowAsync(
            "RecurringJournalOccurrenceWaiver", occurrence.Id);
        if (!workflowResult.Success || !workflowResult.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException(workflowResult.Message ?? "The occurrence waiver workflow could not be started.");

        if (previous == RecurringJournalOccurrenceStatus.PendingApproval)
        {
            var cancellation = await _workflow.CancelWorkflowAsync(nameof(RecurringJournalOccurrence), occurrence.Id,
                "Superseded by a controlled waiver request.");
            if (!cancellation.Success)
            {
                await _workflow.CancelWorkflowAsync("RecurringJournalOccurrenceWaiver", occurrence.Id,
                    "The original occurrence approval could not be cancelled.");
                throw new InvalidOperationException(cancellation.Message ??
                    "The original occurrence approval could not be replaced by a waiver workflow.");
            }
        }

        occurrence.Status = RecurringJournalOccurrenceStatus.WaiverPending;
        occurrence.WaiverReason = comment.Trim();
        occurrence.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
        occurrence.UpdatedAt = DateTime.UtcNow;
        occurrence.UpdatedBy = _currentUser.UserName;
        occurrence.LastModifiedById = CurrentUserIdRequired();
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalOccurrenceWaiverRequested, occurrence.Id,
            new { Status = previous }, new { occurrence.Status, occurrence.WaiverReason, occurrence.WorkflowInstanceId },
            comment, cancellationToken);
        return MapOccurrence(occurrence);
    }

    public async Task<RecurringJournalOccurrenceDto> PostOccurrenceAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await LoadOccurrenceAsync(occurrenceId, cancellationToken);
        if (occurrence.Status is not (RecurringJournalOccurrenceStatus.Approved or RecurringJournalOccurrenceStatus.SubmissionFailed))
            throw new InvalidOperationException("Only an approved recurring-journal occurrence can be posted.");
        if (!occurrence.ReviewedAt.HasValue || !occurrence.ReviewedByUserId.HasValue)
            throw new InvalidOperationException("The occurrence has not completed its independent approval workflow.");
        var posterId = CurrentUserIdRequired();
        if (posterId == occurrence.ReviewedByUserId)
            throw new InvalidOperationException("Segregation of duties requires a different user to post the approved occurrence.");

        var snapshot = DeserializeSnapshot(occurrence.TemplateSnapshotJson);
        try
        {
            var result = await _postingEngine.PostAsync(new FinancePostingRequestV2Dto
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
                AccountingBookCode = snapshot.BookClassification,
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
        Guid id, string comment, CancellationToken cancellationToken)
    {
        RequireComment(comment, "Decision reason");
        var template = await LoadAsync(id, includeOccurrences: false, cancellationToken);
        if (template.Status != RecurringJournalStatus.PendingApproval)
            throw new InvalidOperationException("Only a Pending Approval template can be rejected.");
        var reviewerId = CurrentUserIdRequired();
        if (reviewerId == template.CreatedById || reviewerId == template.SubmittedByUserId)
            throw new InvalidOperationException("Maker-checker control requires a different user to decide the template.");
        if (!await _workflow.CanUserApproveAsync(nameof(RecurringJournalTemplate), template.Id, reviewerId))
            throw new UnauthorizedAccessException("This recurring-journal decision is not assigned to the current user or their roles.");
        var workflowResult = await _workflow.ProcessApprovalStepAsync(
            nameof(RecurringJournalTemplate), template.Id, reviewerId, "Reject", comment.Trim());
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "The recurring-journal rejection step failed.");
        if (workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
            await ApplyRejectedTemplateOutcomeAsync(template, reviewerId, comment, cancellationToken);
        return await RequireMappedAsync(template.Id, cancellationToken);
    }

    private async Task ApplyApprovedTemplateOutcomeAsync(
        RecurringJournalTemplate template,
        Guid reviewerId,
        string comment,
        CancellationToken cancellationToken)
    {
        await ValidatePersistedDefinitionAsync(template, cancellationToken);
        template.Status = RecurringJournalStatus.Active;
        template.ReviewedAt = DateTime.UtcNow;
        template.ReviewedByUserId = reviewerId;
        template.ReviewComment = comment.Trim();
        template.ActivatedAt = template.ReviewedAt;
        template.ActivatedByUserId = reviewerId;
        template.NextDueDate = RecurringJournalRecurrenceCalculator.NextScheduledDate(
            template, template.EffectiveFrom.AddDays(-1));
        if (!template.NextDueDate.HasValue)
            throw new InvalidOperationException("The approved recurrence rule does not produce an occurrence within its configured limits.");
        Touch(template);
        if (template.SupersedesTemplateId.HasValue)
        {
            var superseded = await _db.RecurringJournalTemplates.FirstOrDefaultAsync(item =>
                item.Id == template.SupersedesTemplateId.Value && item.TenantId == template.TenantId && !item.IsDeleted,
                cancellationToken);
            if (superseded != null && superseded.Status is RecurringJournalStatus.Active or RecurringJournalStatus.Paused)
            {
                superseded.Status = RecurringJournalStatus.Completed;
                superseded.NextDueDate = null;
                superseded.UpdatedAt = DateTime.UtcNow;
                superseded.UpdatedBy = _currentUser.UserName;
                superseded.LastModifiedById = reviewerId;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateApproved, template.Id, null,
            new { template.Status, template.ActivatedAt, template.ActivatedByUserId, template.NextDueDate }, comment,
            cancellationToken);
    }

    private async Task ApplyRejectedTemplateOutcomeAsync(
        RecurringJournalTemplate template,
        Guid reviewerId,
        string comment,
        CancellationToken cancellationToken)
    {
        template.Status = RecurringJournalStatus.Rejected;
        template.ReviewedAt = DateTime.UtcNow;
        template.ReviewedByUserId = reviewerId;
        template.ReviewComment = comment.Trim();
        template.LastFailure = comment.Trim();
        Touch(template);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.RecurringJournalTemplateRejected, template.Id, null,
            new { template.Status, template.ReviewedAt, template.ReviewedByUserId }, comment, cancellationToken);
    }

    private async Task ApplyApprovedOccurrenceOutcomeAsync(
        RecurringJournalOccurrence occurrence,
        Guid reviewerId,
        string comment,
        CancellationToken cancellationToken)
    {
        occurrence.Status = RecurringJournalOccurrenceStatus.Approved;
        occurrence.ReviewedAt = DateTime.UtcNow;
        occurrence.ReviewedByUserId = reviewerId;
        occurrence.ReviewComment = comment.Trim();
        occurrence.ErrorMessage = null;
        if (occurrence.ReversalDueDate.HasValue)
        {
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
            new { occurrence.Status, occurrence.ReviewedAt, occurrence.ReviewedByUserId,
                occurrence.ReversalDueDate, occurrence.ReversalStatus, occurrence.ReversalAuthorizedAt,
                occurrence.ReversalAuthorizedByUserId }, comment, cancellationToken);
    }

    private async Task ApplyRejectedOccurrenceOutcomeAsync(
        RecurringJournalOccurrence occurrence,
        Guid reviewerId,
        string comment,
        CancellationToken cancellationToken)
    {
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
        if (request.MaximumOccurrences is > 100000)
            throw new InvalidOperationException("Maximum occurrences cannot exceed 100,000.");
        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
            throw new InvalidOperationException("A schedule time zone is required.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId.Trim()); }
        catch (TimeZoneNotFoundException) { throw new InvalidOperationException("The schedule time zone is not recognized by the server."); }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("The schedule time zone is invalid."); }
        if (!string.IsNullOrWhiteSpace(request.ReferencePattern))
        {
            if (request.ReferencePattern.Trim().Length > 200)
                throw new InvalidOperationException("Reference pattern cannot exceed 200 characters.");
            var allowedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "TemplateNumber", "Period", "ScheduledDate", "Sequence" };
            var tokens = Regex.Matches(request.ReferencePattern, "\\{([^{}]+)\\}")
                .Select(match => match.Groups[1].Value);
            var unknown = tokens.FirstOrDefault(token => !allowedTokens.Contains(token));
            if (unknown != null)
                throw new InvalidOperationException($"Reference pattern token '{{{unknown}}}' is not supported.");
            var withoutTokens = Regex.Replace(request.ReferencePattern, "\\{[^{}]+\\}", string.Empty);
            if (withoutTokens.Contains('{') || withoutTokens.Contains('}'))
                throw new InvalidOperationException("Reference pattern contains an unmatched brace.");
        }
        if (request.Lines.Count < 2)
            throw new InvalidOperationException("A recurring journal requires at least two balanced lines.");
        if (request.Lines.Where(line => line.Id.HasValue).Select(line => line.Id!.Value).Distinct().Count() !=
            request.Lines.Count(line => line.Id.HasValue))
            throw new InvalidOperationException("Recurring-journal line identities must be unique.");
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

        var requestedBookCode = NormalizeBook(request.BookClassification);
        var requestedBook = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(book =>
            book.TenantId == tenantId && !book.IsDeleted && book.Code == requestedBookCode,
            cancellationToken);
        if (requestedBook == null)
            throw new InvalidOperationException($"Accounting book '{requestedBookCode}' does not belong to the current tenant.");
        if (!requestedBook.IsActive || !requestedBook.AllowsPosting)
            throw new InvalidOperationException($"Accounting book '{requestedBookCode}' must be active and allow posting.");
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
        var result = new RecurringJournalTemplateDto
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
            TotalDebit = RoundMoney(template.Lines.Where(line => !line.IsDeleted && line.IsDebit).Sum(line => line.FixedAmount)),
            TotalCredit = RoundMoney(template.Lines.Where(line => !line.IsDeleted && !line.IsDebit).Sum(line => line.FixedAmount)),
            RowVersion = Convert.ToBase64String(template.RowVersion),
            Lines = template.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.LineNumber).Select(line => new RecurringJournalTemplateLineDto
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
        var userId = CurrentUserId();
        if (userId.HasValue && template.Status == RecurringJournalStatus.PendingApproval)
        {
            var makerBlocked = userId == template.CreatedById || userId == template.SubmittedByUserId;
            result.CanApprove = !makerBlocked &&
                await _workflow.CanUserApproveAsync(nameof(RecurringJournalTemplate), template.Id, userId.Value);
            result.CanReject = result.CanApprove;
            if (makerBlocked)
                result.ActionDisabledReason = "Maker-checker control prevents the template creator or submitter from deciding this request.";
            else if (!result.CanApprove)
                result.ActionDisabledReason = "This approval is assigned to another workflow user or role.";
        }
        if (includeOccurrences && userId.HasValue)
        {
            foreach (var occurrence in result.Occurrences)
            {
                if (occurrence.Status == RecurringJournalOccurrenceStatus.PendingApproval)
                {
                    var makerBlocked = userId == template.CreatedById || userId == template.SubmittedByUserId;
                    occurrence.CanApprove = !makerBlocked &&
                        await _workflow.CanUserApproveAsync(nameof(RecurringJournalOccurrence), occurrence.Id, userId.Value);
                    occurrence.CanReject = occurrence.CanApprove;
                    occurrence.ActionDisabledReason = makerBlocked
                        ? "The standing-instruction maker cannot decide its generated accounting occurrence."
                        : occurrence.CanApprove ? null : "This approval is assigned to another workflow user or role.";
                }
                occurrence.CanPost = (occurrence.Status is RecurringJournalOccurrenceStatus.Approved or RecurringJournalOccurrenceStatus.SubmissionFailed)
                    && occurrence.ReviewedAt.HasValue && occurrence.ReviewedByUserId.HasValue
                    && occurrence.ReviewedByUserId != userId;
                occurrence.CanRequestWaiver = !occurrence.JournalEntryId.HasValue &&
                    occurrence.Status is RecurringJournalOccurrenceStatus.PendingApproval or
                        RecurringJournalOccurrenceStatus.Approved or RecurringJournalOccurrenceStatus.Failed or
                        RecurringJournalOccurrenceStatus.SubmissionFailed;
            }
        }
        return result;
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
        ErrorMessage = item.ErrorMessage, AdjustmentExplanation = item.AdjustmentExplanation,
        WaivedAt = item.WaivedAt, WaivedByUserId = item.WaivedByUserId, WaiverReason = item.WaiverReason,
        WorkflowInstanceId = item.WorkflowInstanceId
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
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20)
            throw new InvalidOperationException("A valid accounting book code is required.");
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
    private readonly IWorkflowService _workflow;
    private readonly ILogger<RecurringJournalGenerationProcessor> _logger;

    public RecurringJournalGenerationProcessor(ApplicationDbContext db, IBusinessCalendarProvider calendar,
        IFinanceAuditService audit, IWorkflowService workflow, ILogger<RecurringJournalGenerationProcessor> logger)
    {
        _db = db;
        _calendar = calendar;
        _audit = audit;
        _workflow = workflow;
        _logger = logger;
    }

    public async Task<RecurringJournalGenerationResultDto> ProcessAllAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var dueTenants = _db.RecurringJournalTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status == RecurringJournalStatus.Active && item.NextDueDate <= asOfDate)
            .Select(item => item.TenantId);
        var retryTenants = _db.RecurringJournalOccurrences.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status == RecurringJournalOccurrenceStatus.SubmissionFailed &&
                item.WorkflowInstanceId == null && item.JournalEntryId == null)
            .Select(item => item.TenantId);
        var tenants = await dueTenants.Union(retryTenants).Distinct().ToListAsync(cancellationToken);
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
        await RecoverWorkflowSubmissionFailuresAsync(tenantId, actor, result, cancellationToken);
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
                    if (await TryStartOccurrenceWorkflowAsync(occurrence, template, actor, cancellationToken))
                        result.GeneratedCount++;
                    else
                        result.FailedCount++;
                    await RecordGenerationAuditAsync(tenantId, occurrence, template, actor, cancellationToken);
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

    private async Task RecoverWorkflowSubmissionFailuresAsync(
        Guid tenantId,
        string actor,
        RecurringJournalGenerationResultDto result,
        CancellationToken cancellationToken)
    {
        var failed = await _db.RecurringJournalOccurrences.IgnoreQueryFilters()
            .Include(item => item.Template)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == RecurringJournalOccurrenceStatus.SubmissionFailed &&
                item.WorkflowInstanceId == null && item.JournalEntryId == null)
            .OrderBy(item => item.ScheduledDate)
            .Take(250)
            .ToListAsync(cancellationToken);
        foreach (var occurrence in failed)
        {
            if (await TryStartOccurrenceWorkflowAsync(occurrence, occurrence.Template, actor, cancellationToken))
                result.ExistingCount++;
            else
                result.FailedCount++;
        }
    }

    private async Task<bool> TryStartOccurrenceWorkflowAsync(
        RecurringJournalOccurrence occurrence,
        RecurringJournalTemplate template,
        string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var initiatorId = template.SubmittedByUserId ?? template.CreatedById
                ?? throw new InvalidOperationException("The recurring template has no durable maker identity.");
            var workflowResult = await _workflow.StartApprovalWorkflowAsAsync(
                nameof(RecurringJournalOccurrence), occurrence.Id, initiatorId, occurrence.TenantId);
            if (!workflowResult.Success || !workflowResult.WorkflowInstanceId.HasValue)
                throw new InvalidOperationException(workflowResult.Message ?? "The occurrence approval workflow could not be started.");
            occurrence.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
            occurrence.Status = RecurringJournalOccurrenceStatus.PendingApproval;
            occurrence.ErrorMessage = null;
            occurrence.UpdatedAt = DateTime.UtcNow;
            occurrence.UpdatedBy = actor;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            occurrence.Status = RecurringJournalOccurrenceStatus.SubmissionFailed;
            occurrence.ErrorMessage = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
            occurrence.AttemptCount++;
            occurrence.UpdatedAt = DateTime.UtcNow;
            occurrence.UpdatedBy = actor;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogError(exception,
                "Recurring occurrence {OccurrenceId} could not enter its approval workflow; it remains a close-blocking exception.",
                occurrence.Id);
            return false;
        }
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

    private async Task RecordGenerationAuditAsync(Guid tenantId, RecurringJournalOccurrence occurrence,
        RecurringJournalTemplate template, string actor,
        CancellationToken cancellationToken)
    {
        var auditEvent = new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.RecurringJournalOccurrenceGenerated, TenantId = tenantId,
            SourceModule = "GL", SourceDocumentType = nameof(RecurringJournalOccurrence), SourceDocumentId = occurrence.Id,
            WorkflowInstanceId = occurrence.WorkflowInstanceId,
            IdempotencyKey = $"RecurringJournal:{tenantId:N}:{occurrence.Id:N}:Generated",
            AfterValues = new { occurrence.TemplateId, occurrence.TemplateVersion, occurrence.SequenceNumber,
                occurrence.ScheduledDate, occurrence.EffectiveDate, occurrence.Status, occurrence.ReversalDueDate,
                occurrence.WorkflowInstanceId },
            Resource = "Finance.RecurringJournalOccurrence", ResourceId = occurrence.Id.ToString()
        };
        if (actor == "RecurringJournalScheduler")
        {
            var initiatorId = template.SubmittedByUserId ?? template.CreatedById
                ?? throw new InvalidOperationException("The recurring template has no durable maker identity for system audit.");
            await _audit.RecordSystemAsync(auditEvent, initiatorId, actor, cancellationToken);
            return;
        }
        await _audit.RecordAsync(auditEvent, cancellationToken);
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
        template.ReversalDayOffset, template.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.LineNumber).Select(line =>
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
