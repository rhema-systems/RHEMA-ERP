using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountingBookApplicabilityService : IAccountingBookApplicabilityService
{
    public const string WorkflowEntityType = "AccountingBookApplicabilityPolicy";
    private static readonly Regex CanonicalCode = new("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalIdentity = new("^[A-Z][A-Z0-9_.-]*$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> PseudoSelectors = new(StringComparer.Ordinal)
        { "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS" };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;
    private readonly IFinanceAuditService _audit;
    private readonly IAccountingBookInitializationService _initialization;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountingBookApplicabilityService(ApplicationDbContext db, ICurrentUserService currentUser, IWorkflowService workflow, IFinanceAuditService audit,
        IAccountingBookInitializationService initialization)
        => (_db, _currentUser, _workflow, _audit, _initialization) = (db, currentUser, workflow, audit, initialization);

    public async Task<IReadOnlyList<AccountingBookApplicabilityPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default) =>
        (await PolicyQuery().AsNoTracking().OrderBy(item => item.PolicyCode).ThenByDescending(item => item.Version).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<AccountingBookApplicabilityEligibleBookDto>> GetEligibleBooksAsync(CancellationToken cancellationToken = default)
    {
        // Configuration lookup is deliberately scoped to eligible full books and never depends on report/book-read access.
        var books = await _db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted
            && (item.BookType == AccountingBookType.PrimaryFull || item.BookType == AccountingBookType.ParallelFull))
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code).ToListAsync(cancellationToken);
        var result = new List<AccountingBookApplicabilityEligibleBookDto>();
        foreach (var book in books)
        {
            var initialized = await _initialization.ValidateCurrentApprovedEvidenceAsync(book.Id, cancellationToken);
            var mappings = await _db.AccountAccountingBooks.AsNoTracking().Include(item => item.Account).Include(item => item.AccountClassification)
                .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted && item.IsEnabled).ToListAsync(cancellationToken);
            var mappingReady = mappings.Count > 0 && mappings.All(item => item.AccountClassificationId != null && item.AccountClassification != null
                && !item.AccountClassification.IsDeleted && item.AccountClassification.Status == AccountClassificationStatus.Active
                && item.AccountClassification.IsPostingClassification && item.Account != null && !item.Account.IsDeleted
                && item.Account.TenantId == TenantId && item.Account.AccountType == item.AccountClassification.CoreAccountType);
            result.Add(new() { AccountingBookId = book.Id, Code = book.Code, Name = book.Name, BookType = book.BookType.ToString(),
                LifecycleStatus = book.LifecycleStatus.ToString(), IsDefault = book.IsDefault, IsActive = book.IsActive,
                AllowsPosting = book.AllowsPosting, InitializationReconciled = initialized.IsValid, MappingClassificationReady = mappingReady });
        }
        return result;
    }

    public Task<AccountingBookApplicabilityPolicyDto> CreateDraftAsync(SaveAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        var code = NormalizeCode(request.PolicyCode, "Policy code");
        var previous = await _db.AccountingBookApplicabilityPolicies.Where(item => item.TenantId == TenantId && item.PolicyCode == code && !item.IsDeleted)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        var entity = new AccountingBookApplicabilityPolicy
        {
            TenantId = TenantId, PolicyCode = code, Version = (previous?.Version ?? 0) + 1, SupersedesPolicyId = previous?.Id,
            CreatedByUserId = Actor(), PreparedByUserId = Actor(), PreparedAtUtc = DateTime.UtcNow, CreatedBy = ActorName(), CreatedAt = DateTime.UtcNow
        };
        ApplyDraft(entity, request);
        entity.PreparedByUserId = Actor(); entity.PreparedAtUtc = DateTime.UtcNow;
        _db.AccountingBookApplicabilityPolicies.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AccountingBookApplicabilityPolicyDrafted, entity, request.Reason, cancellationToken);
        return Map(entity);
    }, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> UpdateDraftAsync(Guid id, SaveAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        var entity = await PolicyQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw new KeyNotFoundException("Applicability policy was not found.");
        ApplyRowVersion(entity, request.RowVersion);
        if (entity.PolicyStatus != AccountingBookApplicabilityPolicyStatus.Draft)
            throw new InvalidOperationException("Only a Draft applicability-policy version can be edited; approved-use evidence is structurally immutable.");
        if (await _db.AccountingBookSelectionEvidence.AnyAsync(item => item.TenantId == TenantId && item.AccountingBookApplicabilityPolicyId == id, cancellationToken))
            throw new InvalidOperationException("Applicability policy structure is immutable after approved-use evidence exists.");
        if (NormalizeCode(request.PolicyCode, "Policy code") != entity.PolicyCode) throw new InvalidOperationException("Policy code is immutable within a version lineage.");
        UpdateDraftRulesInPlace(entity, request);
        ApplyDraft(entity, request);
        // Maker-checker authority follows the last substantive editor, not the original creator.
        // Keep this assignment inside the same serializable/audited mutation as the draft evidence.
        entity.PreparedByUserId = Actor(); entity.PreparedAtUtc = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AccountingBookApplicabilityPolicyDrafted, entity, request.Reason, cancellationToken);
        return Map(entity);
    }, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> SubmitAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        var entity = await RequirePolicyAsync(id, cancellationToken);
        ApplyRowVersion(entity, request.RowVersion);
        RequireReason(request.Reason);
        if (entity.PolicyStatus != AccountingBookApplicabilityPolicyStatus.Draft) throw new InvalidOperationException("Only a Draft applicability policy can be submitted.");
        await ValidatePolicyAsync(entity, includeOtherApprovedPolicies: false, cancellationToken);
        if (!await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType)) throw new InvalidOperationException("A published AccountingBookApplicabilityPolicy approval workflow is required.");
        var result = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, entity.Id);
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "Applicability-policy workflow could not be started.");
        entity.WorkflowInstanceId = result.WorkflowInstanceId; entity.PolicyStatus = AccountingBookApplicabilityPolicyStatus.PendingApproval;
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AccountingBookApplicabilityPolicySubmitted, entity, request.Reason, cancellationToken);
        return Map(entity);
    }, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> ApproveAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(id, request, true, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> RejectAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(id, request, false, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> RetireAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        var entity = await RequirePolicyAsync(id, cancellationToken);
        ApplyRowVersion(entity, request.RowVersion); RequireReason(request.Reason);
        if (entity.PolicyStatus != AccountingBookApplicabilityPolicyStatus.Approved) throw new InvalidOperationException("Only an Approved applicability policy can be retired.");
        if (DateTime.UtcNow.Date < entity.EffectiveFrom)
            throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_FUTURE_RETIREMENT_FORBIDDEN: a future-effective policy cannot be retired before its governed interval begins; create a corrected successor instead.");
        if (entity.RetirementDecisionStatus == "Pending") throw new InvalidOperationException("Policy retirement is already pending independent approval.");
        if (!await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType)) throw new InvalidOperationException("A published AccountingBookApplicabilityPolicy approval workflow is required.");
        var result = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, entity.Id);
        if (!result.Success || !result.WorkflowInstanceId.HasValue) throw new InvalidOperationException(result.Message ?? "Policy-retirement workflow could not be started.");
        entity.RetirementRequestedByUserId = Actor(); entity.RetirementRequestedAtUtc = DateTime.UtcNow; entity.RetirementReason = request.Reason.Trim();
        entity.RetirementWorkflowInstanceId = result.WorkflowInstanceId; entity.RetirementDecisionStatus = "Pending";
        entity.RetirementDecidedByUserId = null; entity.RetirementDecidedAtUtc = null; entity.RetirementDecisionReason = null;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AccountingBookApplicabilityPolicyRetirementRequested, entity, request.Reason, cancellationToken);
        return Map(entity);
    }, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> ApproveRetirementAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) =>
        DecideRetirementAsync(id, request, true, cancellationToken);

    public Task<AccountingBookApplicabilityPolicyDto> RejectRetirementAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, CancellationToken cancellationToken = default) =>
        DecideRetirementAsync(id, request, false, cancellationToken);

    private Task<AccountingBookApplicabilityPolicyDto> DecideRetirementAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, bool approve, CancellationToken cancellationToken) => AtomicAsync(async () =>
    {
        var entity = await RequirePolicyAsync(id, cancellationToken); ApplyRowVersion(entity, request.RowVersion); RequireReason(request.Reason);
        if (entity.PolicyStatus != AccountingBookApplicabilityPolicyStatus.Approved || entity.RetirementDecisionStatus != "Pending" || !entity.RetirementRequestedByUserId.HasValue || !entity.RetirementWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Policy retirement is not pending approval.");
        var actor = Actor(); if (actor == entity.RetirementRequestedByUserId) throw new InvalidOperationException("The retirement checker must differ from the retirement maker.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, entity.Id, actor)) throw new UnauthorizedAccessException("The current user cannot approve this policy retirement.");
        // Revalidate the still-approved version while holding the serializable writer boundary before a retirement decision.
        await ValidatePolicyAsync(entity, includeOtherApprovedPolicies: false, cancellationToken);
        if (approve && DateTime.UtcNow.Date < entity.EffectiveFrom)
            throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_FUTURE_RETIREMENT_FORBIDDEN: retirement cannot precede the governed effective interval.");
        var result = await _workflow.ProcessApprovalStepAsync(WorkflowEntityType, entity.Id, actor, approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "Policy-retirement workflow decision failed.");
        if (approve && result.Status == WorkflowInstanceStatus.Completed)
        {
            entity.PolicyStatus = AccountingBookApplicabilityPolicyStatus.Retired; entity.RetiredByUserId = actor; entity.RetiredAtUtc = DateTime.UtcNow;
            var retirementDate = entity.RetiredAtUtc.Value.Date;
            if (retirementDate < entity.EffectiveFrom)
                throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_FUTURE_RETIREMENT_FORBIDDEN: retirement cannot precede the governed effective interval.");
            // Retirement may close an open interval or shorten a later bound, but it must never expand an
            // already-ended historical interval. Resolution remains inclusive at the preserved minimum bound.
            entity.EffectiveTo = entity.EffectiveTo.HasValue && entity.EffectiveTo.Value.Date < retirementDate
                ? entity.EffectiveTo.Value.Date : retirementDate;
        }
        if (!approve || result.Status == WorkflowInstanceStatus.Completed)
        {
            entity.RetirementDecisionStatus = approve ? "Approved" : "Rejected"; entity.RetirementDecidedByUserId = actor;
            entity.RetirementDecidedAtUtc = DateTime.UtcNow; entity.RetirementDecisionReason = request.Reason.Trim();
        }
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(!approve ? FinanceAuditEvents.AccountingBookApplicabilityPolicyRetirementRejected
            : result.Status == WorkflowInstanceStatus.Completed ? FinanceAuditEvents.AccountingBookApplicabilityPolicyRetired
            : FinanceAuditEvents.AccountingBookApplicabilityPolicyApprovalStepCompleted, entity, request.Reason, cancellationToken);
        return Map(entity);
    }, cancellationToken);

    public async Task<AccountingBookSelectionDto> ResolveAsync(ResolveAccountingBookApplicabilityDto request, CancellationToken cancellationToken = default)
    {
        // Producer modules supply stable evidence only. Finance remains the sole book-enumeration authority.
        var input = Normalize(request);
        var date = request.EffectiveDate.Date;
        // An approved successor replaces its policy lineage from its inclusive EffectiveFrom date without
        // rewriting predecessor rows or frozen selections. This keeps before/at/after resolution gap-free.
        var startedPolicies = await _db.AccountingBookApplicabilityPolicies.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted
                && (item.PolicyStatus == AccountingBookApplicabilityPolicyStatus.Approved || item.PolicyStatus == AccountingBookApplicabilityPolicyStatus.Retired)
                && item.EffectiveFrom <= date)
            .OrderBy(item => item.PolicyCode).ThenByDescending(item => item.Version).ToListAsync(cancellationToken);
        foreach (var policy in startedPolicies)
            await ValidateExactVersionLineageAsync(policy, cancellationToken);
        var effectivePolicyIds = startedPolicies.GroupBy(item => item.PolicyCode, StringComparer.Ordinal)
            .Select(group => group.First()).Where(item => item.EffectiveTo == null || item.EffectiveTo >= date).Select(item => item.Id).ToList();
        var candidates = await _db.AccountingBookApplicabilityRules.AsNoTracking()
            .Include(item => item.Policy).Include(item => item.SelectedBooks).ThenInclude(item => item.AccountingBook)
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && effectivePolicyIds.Contains(item.AccountingBookApplicabilityPolicyId)
                && item.OriginatingModuleCode == input.Module && item.SourceDocumentType == input.Document && item.PostingAction == input.Action)
            .OrderByDescending(item => item.Priority).ThenBy(item => item.Policy.PolicyCode).ThenByDescending(item => item.Policy.Version).ThenBy(item => item.RuleCode)
            .ToListAsync(cancellationToken);
        AccountingBookApplicabilityRule? rule = null;
        if (candidates.Count > 0)
        {
            var top = candidates[0].Priority;
            var winners = candidates.Where(item => item.Priority == top).ToList();
            if (winners.Count != 1) throw new InvalidOperationException("AMBIGUOUS_ACCOUNTING_BOOK_APPLICABILITY: multiple effective rules have equal highest priority.");
            rule = winners[0];
        }

        IReadOnlyList<AccountingBookSelectionBookDto> selected;
        var fallback = rule == null;
        if (fallback)
        {
            // No explicit policy means exactly the one primary/default full book, never every active book.
            var primary = await _db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted && item.IsDefault && item.BookType == AccountingBookType.PrimaryFull).ToListAsync(cancellationToken);
            if (primary.Count != 1) throw new InvalidOperationException("PRIMARY_ACCOUNTING_BOOK_REQUIRED: exactly one canonical primary/default full book is required.");
            selected = [new AccountingBookSelectionBookDto { AccountingBookId = primary[0].Id, AccountingBookCode = primary[0].Code, SelectionOrder = 0 }];
        }
        else
        {
            // Delta books are not ordinary automatic representations; only explicit full-book IDs survive validation.
            selected = rule!.SelectedBooks.OrderBy(item => item.SelectionOrder).Select(item => new AccountingBookSelectionBookDto
            { AccountingBookId = item.AccountingBookId, AccountingBookCode = item.AccountingBookCodeSnapshot, SelectionOrder = item.SelectionOrder }).ToList();
            // Corrupt/imported explicit authority must never become a successful empty selection or fallback.
            if (selected.Count == 0) throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_EMPTY_SELECTION: an explicit rule must select at least one governed full book.");
        }

        var validation = await ValidateSelectedBooksAsync(selected, date, input.Module, cancellationToken);
        var blockers = validation.Blockers;
        var inputHash = Hash($"BOOK-APPLICABILITY-INPUT-V1|{TenantId:D}|{date:yyyy-MM-dd}|{input.Module}|{input.Document}|{input.Action}");
        var fingerprint = Hash($"BOOK-APPLICABILITY-SELECTION-V1|{inputHash}|{rule?.Policy.Id:D}|{rule?.Id:D}|{rule?.Policy.Version}|{string.Join("|", selected.OrderBy(item => item.SelectionOrder).Select(item => $"{item.SelectionOrder}:{item.AccountingBookId:D}:{item.AccountingBookCode}:{item.AuthorityFingerprint}"))}");
        if (!string.IsNullOrWhiteSpace(request.ExpectedCalculationInputHash) && !string.Equals(request.ExpectedCalculationInputHash, inputHash, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_BOOK_SELECTION_INPUT_CONFLICT: normalized selection inputs changed.");
        if (!string.IsNullOrWhiteSpace(request.ExpectedSelectionFingerprint) && !string.Equals(request.ExpectedSelectionFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_BOOK_SELECTION_EVIDENCE_CONFLICT: policy, rule, or selected-book evidence changed.");
        return new AccountingBookSelectionDto { PolicyId = rule?.Policy.Id, RuleId = rule?.Id, PolicyVersion = rule?.Policy.Version,
            EffectiveDate = date, OriginatingModuleCode = input.Module, SourceDocumentType = input.Document, PostingAction = input.Action,
            UsedPrimaryOnlyFallback = fallback, Books = selected, Blockers = blockers, CalculationInputHash = inputHash, SelectionFingerprint = fingerprint };
    }

    public Task<AccountingBookSelectionDto> FreezeAsync(FreezeAccountingBookSelectionDto request, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new InvalidOperationException("A selection idempotency key is required.");
        var key = request.IdempotencyKey.Trim().ToUpperInvariant();
        if (key.Length > 100) throw new InvalidOperationException("Selection idempotency key cannot exceed 100 characters.");
        // SQL Server's unique-key equality is ordinarily case-insensitive. A transaction-owned application lock
        // over the canonical key prevents identical or conflicting concurrent freezes from racing the evidence insert.
        if (_db.Database.IsSqlServer())
        {
            var resource = $"FIN:C5:FREEZE:{Hash($"{TenantId:D}|{key}")}";
            await _db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000;
IF @result < 0 THROW 51000, 'ACCOUNTING_BOOK_SELECTION_LOCK_FAILED: selection evidence could not be serialized.', 1;", cancellationToken);
        }
        var existing = await _db.AccountingBookSelectionEvidence.AsNoTracking().Include(item => item.Books)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.IdempotencyKey == key && !item.IsDeleted, cancellationToken);
        if (existing != null)
        {
            RequirePreviewHash(request.ExpectedCalculationInputHash, "calculation input hash"); RequirePreviewHash(request.ExpectedSelectionFingerprint, "selection fingerprint");
            var normalized = Normalize(request);
            if (existing.EffectiveDate != request.EffectiveDate.Date || existing.OriginatingModuleCode != normalized.Module
                || existing.SourceDocumentType != normalized.Document || existing.PostingAction != normalized.Action)
                throw new InvalidOperationException("ACCOUNTING_BOOK_SELECTION_IDEMPOTENCY_CONFLICT: the key already freezes different normalized source evidence.");
            if (existing.CalculationInputHash != request.ExpectedCalculationInputHash || existing.SelectionFingerprint != request.ExpectedSelectionFingerprint)
                throw new InvalidOperationException("ACCOUNTING_BOOK_SELECTION_IDEMPOTENCY_CONFLICT: the key already freezes different evidence.");
            return MapEvidence(existing);
        }
        RequirePreviewHash(request.ExpectedCalculationInputHash, "calculation input hash"); RequirePreviewHash(request.ExpectedSelectionFingerprint, "selection fingerprint");
        var resolved = await ResolveAsync(request, cancellationToken);
        if (resolved.Blockers.Count > 0) throw new InvalidOperationException("ACCOUNTING_BOOK_SELECTION_BLOCKED: resolve every selected-book readiness blocker before freezing evidence.");
        // This explicit write freezes portable C6 evidence; it creates no economic event or journal representation.
        var evidence = new AccountingBookSelectionEvidence { TenantId = TenantId, AccountingBookApplicabilityPolicyId = resolved.PolicyId,
            AccountingBookApplicabilityRuleId = resolved.RuleId, PolicyVersion = resolved.PolicyVersion, EffectiveDate = resolved.EffectiveDate,
            OriginatingModuleCode = resolved.OriginatingModuleCode, SourceDocumentType = resolved.SourceDocumentType, PostingAction = resolved.PostingAction,
            IdempotencyKey = key, CalculationInputHash = resolved.CalculationInputHash, SelectionFingerprint = resolved.SelectionFingerprint,
            FrozenByUserId = Actor(), FrozenAtUtc = DateTime.UtcNow, CreatedBy = ActorName(),
            Books = resolved.Books.Select(item => new AccountingBookSelectionEvidenceBook { TenantId = TenantId, AccountingBookId = item.AccountingBookId,
                SelectionOrder = item.SelectionOrder, AccountingBookCodeSnapshot = item.AccountingBookCode, AuthorityFingerprint = item.AuthorityFingerprint }).ToList() };
        _db.AccountingBookSelectionEvidence.Add(evidence);
        await _db.SaveChangesAsync(cancellationToken);
        resolved.SelectionEvidenceId = evidence.Id;
        await _audit.RecordAsync(new FinanceAuditEventDto { TenantId = TenantId, EventType = FinanceAuditEvents.AccountingBookSelectionFrozen,
            SourceModule = resolved.OriginatingModuleCode, SourceDocumentType = resolved.SourceDocumentType, SourceDocumentId = evidence.Id,
            Resource = "Finance.AccountingBookSelectionEvidence", ResourceId = evidence.Id.ToString(), IdempotencyKey = evidence.IdempotencyKey,
            AfterValues = new { evidence.Id, evidence.IdempotencyKey, evidence.FrozenByUserId, evidence.FrozenAtUtc,
              evidence.AccountingBookApplicabilityPolicyId, evidence.AccountingBookApplicabilityRuleId, evidence.PolicyVersion,
              evidence.EffectiveDate, evidence.OriginatingModuleCode, evidence.SourceDocumentType, evidence.PostingAction,
              evidence.CalculationInputHash, evidence.SelectionFingerprint,
              Books = evidence.Books.OrderBy(item => item.SelectionOrder).Select(item => new
              { item.AccountingBookId, item.AccountingBookCodeSnapshot, item.SelectionOrder, item.AuthorityFingerprint }) },
            Reason = "Approved applicability selection evidence frozen." }, cancellationToken);
        return resolved;
    }, cancellationToken);

    private Task<AccountingBookApplicabilityPolicyDto> DecideAsync(Guid id, DecideAccountingBookApplicabilityPolicyDto request, bool approve, CancellationToken ct) => AtomicAsync(async () =>
    {
        var entity = await RequirePolicyAsync(id, ct); ApplyRowVersion(entity, request.RowVersion); RequireReason(request.Reason);
        if (entity.PolicyStatus != AccountingBookApplicabilityPolicyStatus.PendingApproval || !entity.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("The applicability policy is not pending approval.");
        var actor = Actor(); if (actor == entity.PreparedByUserId) throw new InvalidOperationException("The applicability-policy checker must differ from the last substantive Draft editor.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, entity.Id, actor)) throw new UnauthorizedAccessException("The current user cannot decide this applicability policy.");
        if (approve) await ValidatePolicyAsync(entity, includeOtherApprovedPolicies: true, ct);
        var result = await _workflow.ProcessApprovalStepAsync(WorkflowEntityType, entity.Id, actor, approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "Applicability-policy workflow decision failed.");
        if (result.Status == WorkflowInstanceStatus.Completed)
        {
            entity.PolicyStatus = approve ? AccountingBookApplicabilityPolicyStatus.Approved : AccountingBookApplicabilityPolicyStatus.Rejected;
            if (approve) { entity.ApprovedByUserId = actor; entity.ApprovedAtUtc = DateTime.UtcNow; }
        }
        entity.DecidedByUserId = actor; entity.DecidedAtUtc = DateTime.UtcNow; entity.DecisionReason = request.Reason.Trim();
        await _db.SaveChangesAsync(ct);
        await AuditAsync(!approve ? FinanceAuditEvents.AccountingBookApplicabilityPolicyRejected
            : result.Status == WorkflowInstanceStatus.Completed ? FinanceAuditEvents.AccountingBookApplicabilityPolicyApproved
            : FinanceAuditEvents.AccountingBookApplicabilityPolicyApprovalStepCompleted, entity, request.Reason, ct);
        return Map(entity);
    }, ct);

    private void ApplyDraft(AccountingBookApplicabilityPolicy entity, SaveAccountingBookApplicabilityPolicyDto request)
    {
        RequireReason(request.Reason);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Policy name is required.");
        var from = request.EffectiveFrom.Date; var to = request.EffectiveTo?.Date;
        if (to < from) throw new InvalidOperationException("Policy effective-to date cannot precede effective-from date.");
        if (request.Rules.Count == 0) throw new InvalidOperationException("An explicit applicability policy requires at least one rule.");
        var ruleCodes = new HashSet<string>(StringComparer.Ordinal);
        var matchKeys = new HashSet<string>(StringComparer.Ordinal);
        entity.Name = request.Name.Trim(); entity.Description = request.Description?.Trim(); entity.EffectiveFrom = from; entity.EffectiveTo = to; entity.Reason = request.Reason.Trim();
        if (entity.Rules.Count > 0) return;
        foreach (var input in request.Rules)
        {
            var ruleCode = NormalizeCode(input.RuleCode, "Rule code");
            var module = NormalizeModule(input.OriginatingModuleCode);
            var document = NormalizeIdentity(input.SourceDocumentType, "Source document type");
            var action = NormalizeIdentity(input.PostingAction, "Posting action");
            if (!ruleCodes.Add(ruleCode)) throw new InvalidOperationException("Rule codes must be unique within a policy version.");
            if (!matchKeys.Add($"{module}|{document}|{action}|{input.Priority}")) throw new InvalidOperationException("AMBIGUOUS_ACCOUNTING_BOOK_APPLICABILITY: equal-priority overlapping rules are forbidden.");
            if (input.Priority < 0) throw new InvalidOperationException("Rule priority cannot be negative.");
            if (input.AccountingBookIds.Count == 0 || input.AccountingBookIds.Distinct().Count() != input.AccountingBookIds.Count) throw new InvalidOperationException("A rule requires a unique ordered set of accounting-book IDs.");
            var books = _db.AccountingBooks.Where(item => item.TenantId == TenantId && input.AccountingBookIds.Contains(item.Id) && !item.IsDeleted).ToDictionary(item => item.Id);
            if (books.Count != input.AccountingBookIds.Count) throw new InvalidOperationException("Every selected accounting-book ID must belong to the current tenant.");
            var rule = new AccountingBookApplicabilityRule { TenantId = TenantId, RuleCode = ruleCode, Priority = input.Priority,
                OriginatingModuleCode = module, SourceDocumentType = document, PostingAction = action, SortOrder = input.SortOrder };
            var order = 0;
            foreach (var bookId in input.AccountingBookIds)
            {
                var book = books[bookId];
                if (book.BookType == AccountingBookType.Delta) throw new InvalidOperationException("DELTA_BOOK_AUTOMATIC_APPLICABILITY_FORBIDDEN: Delta books cannot be selected by ordinary applicability rules.");
                if (book.BookType is not (AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull)) throw new InvalidOperationException("Applicability rules may select only full accounting books.");
                rule.SelectedBooks.Add(new AccountingBookApplicabilityRuleBook { TenantId = TenantId, AccountingBookId = book.Id,
                    AccountingBookCodeSnapshot = book.Code, SelectionOrder = order++ });
            }
            entity.Rules.Add(rule);
        }
    }

    private void UpdateDraftRulesInPlace(AccountingBookApplicabilityPolicy entity, SaveAccountingBookApplicabilityPolicyDto request)
    {
        if (entity.Rules.Count != request.Rules.Count) throw new InvalidOperationException("Draft rule count cannot change without physical deletion; create a new policy version instead.");
        var existing = entity.Rules.OrderBy(item => item.SortOrder).ThenBy(item => item.RuleCode).ToList();
        var proposed = request.Rules.OrderBy(item => item.SortOrder).ThenBy(item => item.RuleCode, StringComparer.OrdinalIgnoreCase).ToList();
        var codes = new HashSet<string>(StringComparer.Ordinal); var matches = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < existing.Count; index++)
        {
            var left = existing[index]; var right = proposed[index];
            if (left.SelectedBooks.Count != right.AccountingBookIds.Count) throw new InvalidOperationException("Draft selected-book count cannot change without physical deletion; create a new policy version instead.");
            var code = NormalizeCode(right.RuleCode, "Rule code"); var module = NormalizeModule(right.OriginatingModuleCode);
            var document = NormalizeIdentity(right.SourceDocumentType, "Source document type"); var action = NormalizeIdentity(right.PostingAction, "Posting action");
            if (!codes.Add(code) || !matches.Add($"{module}|{document}|{action}|{right.Priority}")) throw new InvalidOperationException("AMBIGUOUS_ACCOUNTING_BOOK_APPLICABILITY: duplicate rule code or equal-priority overlap.");
            if (right.Priority < 0 || right.AccountingBookIds.Distinct().Count() != right.AccountingBookIds.Count) throw new InvalidOperationException("Rule priority and selected books are invalid.");
            var books = _db.AccountingBooks.Where(item => item.TenantId == TenantId && right.AccountingBookIds.Contains(item.Id) && !item.IsDeleted).ToDictionary(item => item.Id);
            if (books.Count != right.AccountingBookIds.Count || books.Values.Any(book => book.BookType is not (AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull)))
                throw new InvalidOperationException("Draft rules may select only same-tenant full accounting books; Delta is forbidden.");
            left.RuleCode = code; left.Priority = right.Priority; left.OriginatingModuleCode = module; left.SourceDocumentType = document; left.PostingAction = action; left.SortOrder = right.SortOrder;
            var targets = left.SelectedBooks.OrderBy(item => item.SelectionOrder).ToList();
            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                var book = books[right.AccountingBookIds[targetIndex]]; targets[targetIndex].AccountingBookId = book.Id;
                targets[targetIndex].AccountingBookCodeSnapshot = book.Code; targets[targetIndex].SelectionOrder = targetIndex;
            }
        }
    }

    private async Task ValidatePolicyAsync(AccountingBookApplicabilityPolicy entity, bool includeOtherApprovedPolicies, CancellationToken ct)
    {
        await ValidateExactVersionLineageAsync(entity, ct);
        if (entity.Rules.Count == 0 || entity.Rules.Any(item => item.SelectedBooks.Count == 0)) throw new InvalidOperationException("Every applicability rule must select at least one full book.");
        var selected = entity.Rules.SelectMany(item => item.SelectedBooks).ToList();
        var selectedIds = selected.Select(item => item.AccountingBookId).Distinct().ToList();
        var books = await _db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId && selectedIds.Contains(item.Id) && !item.IsDeleted).ToDictionaryAsync(item => item.Id, ct);
        if (books.Count != selectedIds.Count || selected.Any(item => !books.TryGetValue(item.AccountingBookId, out var book)
                || !string.Equals(item.AccountingBookCodeSnapshot, book.Code, StringComparison.Ordinal)
                || !CanonicalCode.IsMatch(book.Code) || PseudoSelectors.Contains(book.Code)
                || book.BookType is not (AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull)))
            throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_SELECTED_BOOK_INVALID: every selected ID/code must be a canonical same-tenant eligible full book.");
        if (!includeOtherApprovedPolicies) return;
        // Approval runs inside the serializable tenant writer boundary. The range reads below are
        // the concurrency authority paired with migration indexes/triggers; do not weaken them.
        var predecessor = entity.SupersedesPolicyId.HasValue
            ? await _db.AccountingBookApplicabilityPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == entity.SupersedesPolicyId && item.PolicyCode == entity.PolicyCode && !item.IsDeleted, ct) : null;
        if (predecessor != null && predecessor.PolicyStatus == AccountingBookApplicabilityPolicyStatus.Approved
            && entity.EffectiveFrom <= predecessor.EffectiveFrom)
            throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_REPLACEMENT_ORDER: a successor must start after its approved predecessor.");
        foreach (var rule in entity.Rules)
        {
            var overlap = await _db.AccountingBookApplicabilityRules.AsNoTracking().Include(item => item.Policy).AnyAsync(item =>
                item.TenantId == TenantId && item.AccountingBookApplicabilityPolicyId != entity.Id && !item.IsDeleted && !item.Policy.IsDeleted
                && item.Policy.PolicyStatus == AccountingBookApplicabilityPolicyStatus.Approved
                && item.Policy.PolicyCode != entity.PolicyCode
                && item.Priority == rule.Priority && item.OriginatingModuleCode == rule.OriginatingModuleCode
                && item.SourceDocumentType == rule.SourceDocumentType && item.PostingAction == rule.PostingAction
                && item.Policy.EffectiveFrom <= (entity.EffectiveTo ?? DateTime.MaxValue)
                && (item.Policy.EffectiveTo == null || item.Policy.EffectiveTo >= entity.EffectiveFrom), ct);
            if (overlap) throw new InvalidOperationException("AMBIGUOUS_ACCOUNTING_BOOK_APPLICABILITY: an approved equal-priority rule overlaps this effective range.");
        }
    }

    private async Task ValidateExactVersionLineageAsync(AccountingBookApplicabilityPolicy entity, CancellationToken ct)
    {
        // Version numbers are accounting authority, not display sequencing. Walk the complete predecessor chain
        // so a malformed high version can neither be approved nor win an effective-date resolution.
        var visited = new HashSet<Guid>();
        var current = entity;
        while (true)
        {
            if (!visited.Add(current.Id))
                throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_VERSION_LINEAGE_INVALID: policy-version lineage contains a cycle.");
            if (current.Version == 1)
            {
                if (current.SupersedesPolicyId.HasValue)
                    throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_VERSION_LINEAGE_INVALID: version 1 cannot supersede another policy.");
                return;
            }
            if (current.Version < 1 || !current.SupersedesPolicyId.HasValue)
                throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_VERSION_LINEAGE_INVALID: every version after 1 must identify its immediate predecessor.");
            var predecessor = await _db.AccountingBookApplicabilityPolicies.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == current.SupersedesPolicyId.Value && item.TenantId == TenantId && !item.IsDeleted, ct);
            if (predecessor == null || !string.Equals(predecessor.PolicyCode, current.PolicyCode, StringComparison.Ordinal)
                || predecessor.Version != current.Version - 1)
                throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_VERSION_LINEAGE_INVALID: successor version must equal its same-tenant, same-code predecessor version plus one.");
            current = predecessor;
        }
    }

    private async Task<SelectionValidation> ValidateSelectedBooksAsync(IReadOnlyList<AccountingBookSelectionBookDto> selected, DateTime date, string moduleCode, CancellationToken ct)
    {
        if (selected.Count == 0) throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_EMPTY_SELECTION: selection authority cannot be empty.");
        var ids = selected.Select(item => item.AccountingBookId).ToList();
        var books = await _db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId && ids.Contains(item.Id) && !item.IsDeleted).ToDictionaryAsync(item => item.Id, ct);
        var fiscalPeriods = await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted && item.StartDate <= date && item.EndDate >= date).ToListAsync(ct);
        var blockers = new List<AccountingBookSelectionBlockerDto>();
        foreach (var selectedBook in selected.OrderBy(item => item.SelectionOrder))
        {
            void Block(string code, string message) => blockers.Add(new() { Code = code, AccountingBookId = selectedBook.AccountingBookId, Message = message });
            if (!books.TryGetValue(selectedBook.AccountingBookId, out var book)) { Block("BOOK_NOT_CANONICAL_FOR_TENANT", "Selected book is missing, deleted, or belongs to another tenant."); continue; }
            if (!string.Equals(book.Code, selectedBook.AccountingBookCode, StringComparison.Ordinal) || !CanonicalCode.IsMatch(book.Code) || PseudoSelectors.Contains(book.Code)) Block("BOOK_CODE_SNAPSHOT_MISMATCH", "Selected book code is noncanonical or differs from frozen rule evidence.");
            if (book.BookType == AccountingBookType.Delta) Block("DELTA_BOOK_EXCLUDED", "Delta books are excluded from ordinary automatic applicability.");
            else if (book.BookType is not (AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull)) Block("BOOK_TYPE_NOT_ELIGIBLE", "Selected book is not an eligible full book.");
            if (book.LifecycleStatus != AccountingBookLifecycleStatus.Active || !book.IsActive || !book.AllowsPosting) Block("BOOK_NOT_ACTIVE_POSTABLE", "Selected book must be Active and posting-enabled.");
            if (book.EffectiveFromUtc?.Date > date || book.EffectiveToUtc?.Date < date) Block("BOOK_NOT_EFFECTIVE", "Selected book is not effective on the event date.");
            var initialization = await _initialization.ValidateCurrentApprovedEvidenceAsync(book.Id, ct);
            if (!initialization.IsValid) Block("BOOK_INITIALIZATION_NOT_RECONCILED", initialization.Blocker ?? "Selected book lacks current approved, reconciled initialization evidence.");
            var mappings = await _db.AccountAccountingBooks.AsNoTracking().Include(item => item.Account).Include(item => item.AccountClassification)
                .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted && item.IsEnabled).ToListAsync(ct);
            if (mappings.Count == 0 || mappings.Any(item => item.Account == null || item.Account.IsDeleted || item.Account.TenantId != TenantId || item.Account.Status != AccountStatus.Active
                || item.AccountClassificationId == null || item.AccountClassification == null || item.AccountClassification.TenantId != TenantId
                || item.AccountClassification.AccountingBookId != book.Id || item.AccountClassification.IsDeleted
                || item.AccountClassification.Status != AccountClassificationStatus.Active || !item.AccountClassification.IsPostingClassification
                || item.AccountClassification.CoreAccountType != item.Account.AccountType))
                Block("BOOK_MAPPING_CLASSIFICATION_NOT_READY", "Selected book requires enabled mappings whose active posting classification core type matches the account type.");
            Guid? fiscalYearId = null; Guid? fiscalPeriodId = null; Guid? bookPeriodId = null; Guid? moduleDefinitionId = null; Guid? moduleLockId = null;
            string fiscalYearState = "MISSING"; string periodState = "MISSING"; string bookPeriodState = "MISSING"; string moduleState = "MISSING";
            if (fiscalPeriods.Count != 1) Block("EXACT_FISCAL_PERIOD_NOT_READY", "Event date must resolve to exactly one open, unlocked tenant fiscal period.");
            else
            {
                var period = fiscalPeriods[0]; fiscalPeriodId = period.Id; fiscalYearId = period.FiscalYearId;
                if (!period.IsOpen || period.IsClosed || period.IsLocked) Block("OUTER_FISCAL_PERIOD_NOT_OPEN", "Tenant fiscal period is not open and unlocked.");
                periodState = $"{period.PeriodStatus}:{period.IsOpen}:{period.IsClosed}:{period.IsLocked}:{period.IsGlobalLockSuspended}";
                var year = await _db.FiscalYears.AsNoTracking().SingleOrDefaultAsync(item => item.Id == period.FiscalYearId && item.TenantId == TenantId && !item.IsDeleted, ct);
                if (year == null || !year.IsActive || year.IsClosed || year.IsLocked) Block("FISCAL_YEAR_NOT_OPEN", "Same-tenant fiscal year is not active, open, and unlocked.");
                fiscalYearState = year == null ? "MISSING" : $"{year.Status}:{year.IsActive}:{year.IsClosed}:{year.IsLocked}";
                var modules = await _db.ModuleDefinitions.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted && item.IsActive && item.ModuleCode == moduleCode).ToListAsync(ct);
                if (modules.Count != 1) Block("ORIGIN_MODULE_AUTHORITY_REQUIRED", "Originating module must have exactly one active same-tenant period-lock definition.");
                else
                {
                    moduleDefinitionId = modules[0].Id;
                    var locks = await _db.PeriodModuleLocks.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted && item.FiscalPeriodId == period.Id && item.ModuleDefinitionId == modules[0].Id).ToListAsync(ct);
                    if (locks.Count > 1) Block("ORIGIN_MODULE_LOCK_AMBIGUOUS", "Originating module period-lock authority is ambiguous.");
                    var moduleLock = locks.SingleOrDefault(); moduleLockId = moduleLock?.Id;
                    var expired = moduleLock is { IsLocked: false, ReopenExpiresAtUtc: not null } && moduleLock.ReopenExpiresAtUtc <= DateTime.UtcNow;
                    var locked = period.IsGlobalLockSuspended ? moduleLock == null || moduleLock.IsLocked || expired : moduleLock?.IsLocked == true || expired;
                    moduleState = $"{modules[0].Id:D}:{modules[0].ModuleCode}:{modules[0].IsActive}:" + (moduleLock == null ? "OPEN:NO_ROW" : $"{moduleLock.Id:D}:{moduleLock.IsLocked}:{moduleLock.ReopenExpiresAtUtc:O}:{moduleLock.AutoRelockedDate:O}");
                    if (locked) Block("ORIGIN_MODULE_PERIOD_LOCKED", "Originating module is locked for the exact fiscal period.");
                }
                var bookPeriod = await _db.AccountingBookPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && item.FiscalPeriodId == period.Id && !item.IsDeleted, ct);
                if (bookPeriod == null) Block("ACCOUNTING_BOOK_PERIOD_REQUIRED", "Exact accounting-book period authority is missing for the event date.");
                else if (bookPeriod.PeriodStatus != AccountingBookPeriodStatus.Open || bookPeriod.PendingStatus is AccountingBookPeriodStatus.Closed or AccountingBookPeriodStatus.Locked)
                    Block("ACCOUNTING_BOOK_PERIOD_NOT_OPEN", "Exact accounting-book period authority is not open.");
                bookPeriodId = bookPeriod?.Id;
                bookPeriodState = bookPeriod == null ? "MISSING" : $"{bookPeriod.PeriodStatus}:{bookPeriod.PendingStatus}";
            }
            // Both economic type authorities are decision inputs; binding them prevents stale previews from
            // surviving an account or classification type mutation.
            var mappingEvidence = string.Join("|", mappings.OrderBy(item => item.Id).Select(item => $"{item.Id:D}:{item.TenantId:D}:{item.AccountingBookId:D}:{item.AccountId:D}:{item.Account?.TenantId:D}:{item.Account?.Status}:{item.Account?.AccountType}:{item.AccountClassificationId:D}:{item.AccountClassification?.TenantId:D}:{item.AccountClassification?.AccountingBookId:D}:{item.AccountClassification?.Code}:{item.AccountClassification?.CoreAccountType}:{item.AccountClassification?.Status}:{item.AccountClassification?.IsPostingClassification}:{item.IsEnabled}"));
            selectedBook.AuthorityFingerprint = Hash($"BOOK-APPLICABILITY-AUTHORITY-V1|{TenantId:D}|{book.Id:D}|{book.Code}|{book.BookType}|{book.LifecycleStatus}|{book.IsActive}|{book.AllowsPosting}|{book.EffectiveFromUtc:O}|{book.EffectiveToUtc:O}|INIT:{initialization.InitializationId:D}:{initialization.Version}:{initialization.EvidenceFingerprint}:{initialization.ReconciliationFingerprint}|FY:{fiscalYearId:D}|FP:{fiscalPeriodId:D}:{periodState}|BP:{bookPeriodId:D}|MOD:{moduleDefinitionId:D}:{moduleLockId:D}:{moduleState}|MAP:{mappingEvidence}");
            selectedBook.AuthorityFingerprint = Hash($"{selectedBook.AuthorityFingerprint}|FYSTATE:{fiscalYearState}|BPSTATE:{bookPeriodState}");
        }
        return new(blockers);
    }

    private IQueryable<AccountingBookApplicabilityPolicy> PolicyQuery() => _db.AccountingBookApplicabilityPolicies
        .Include(item => item.Rules).ThenInclude(item => item.SelectedBooks).ThenInclude(item => item.AccountingBook)
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);
    private async Task<AccountingBookApplicabilityPolicy> RequirePolicyAsync(Guid id, CancellationToken ct) =>
        await PolicyQuery().SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw new KeyNotFoundException("Applicability policy was not found.");
    private static AccountingBookApplicabilityPolicyDto Map(AccountingBookApplicabilityPolicy item) => new() { Id = item.Id, PolicyCode = item.PolicyCode,
        Version = item.Version, SupersedesPolicyId = item.SupersedesPolicyId, Name = item.Name, Description = item.Description,
        EffectiveFrom = item.EffectiveFrom, EffectiveTo = item.EffectiveTo, Status = item.PolicyStatus.ToString(), Reason = item.Reason,
        PreparedByUserId = item.PreparedByUserId, PreparedAtUtc = item.PreparedAtUtc,
        WorkflowInstanceId = item.WorkflowInstanceId, RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion),
        RetirementRequestedByUserId = item.RetirementRequestedByUserId, RetirementRequestedAtUtc = item.RetirementRequestedAtUtc,
        RetirementReason = item.RetirementReason, RetirementWorkflowInstanceId = item.RetirementWorkflowInstanceId,
        RetirementDecisionStatus = item.RetirementDecisionStatus, RetirementDecidedByUserId = item.RetirementDecidedByUserId,
        RetirementDecidedAtUtc = item.RetirementDecidedAtUtc, RetirementDecisionReason = item.RetirementDecisionReason,
        Rules = item.Rules.OrderBy(rule => rule.SortOrder).ThenBy(rule => rule.RuleCode).Select(rule => new AccountingBookApplicabilityRuleDto { Id = rule.Id,
            RuleCode = rule.RuleCode, Priority = rule.Priority, OriginatingModuleCode = rule.OriginatingModuleCode, SourceDocumentType = rule.SourceDocumentType,
            PostingAction = rule.PostingAction, SortOrder = rule.SortOrder, SelectedBooks = rule.SelectedBooks.OrderBy(book => book.SelectionOrder)
                .Select(book => new AccountingBookApplicabilityRuleBookDto { AccountingBookId = book.AccountingBookId, AccountingBookCode = book.AccountingBookCodeSnapshot, SelectionOrder = book.SelectionOrder }).ToList() }).ToList() };
    private static (string Module, string Document, string Action) Normalize(ResolveAccountingBookApplicabilityDto request)
    {
        var module = NormalizeModule(request.OriginatingModuleCode);
        return (module, NormalizeIdentity(request.SourceDocumentType, "Source document type"), NormalizeIdentity(request.PostingAction, "Posting action"));
    }
    private static string NormalizeCode(string value, string label) { var normalized = value?.Trim().ToUpperInvariant() ?? ""; if (!CanonicalCode.IsMatch(normalized) || PseudoSelectors.Contains(normalized)) throw new InvalidOperationException($"{label} must be canonical and cannot be a pseudo selector."); return normalized; }
    private static string NormalizeIdentity(string value, string label) { var normalized = value?.Trim().ToUpperInvariant() ?? ""; if (!CanonicalIdentity.IsMatch(normalized) || PseudoSelectors.Contains(normalized)) throw new InvalidOperationException($"{label} must be a canonical stable identity and cannot be a pseudo selector."); return normalized; }
    private static string NormalizeModule(string value) { var module = NormalizeIdentity(value, "Originating module code"); if (!FinanceModuleLockCatalog.Definitions.Any(item => item.Code == module)) throw new InvalidOperationException("ORIGIN_MODULE_NOT_REGISTERED: originating module code is not in the canonical Finance period-lock catalog."); return module; }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void RequirePreviewHash(string? value, string label) { if (value is null || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)) || value != value.ToUpperInvariant()) throw new InvalidOperationException($"A canonical 64-character preview {label} is required before freezing selection evidence."); }
    private static AccountingBookSelectionDto MapEvidence(AccountingBookSelectionEvidence item) => new() { SelectionEvidenceId = item.Id, PolicyId = item.AccountingBookApplicabilityPolicyId,
        RuleId = item.AccountingBookApplicabilityRuleId, PolicyVersion = item.PolicyVersion, EffectiveDate = item.EffectiveDate,
        OriginatingModuleCode = item.OriginatingModuleCode, SourceDocumentType = item.SourceDocumentType, PostingAction = item.PostingAction,
        UsedPrimaryOnlyFallback = !item.AccountingBookApplicabilityPolicyId.HasValue, CalculationInputHash = item.CalculationInputHash,
        SelectionFingerprint = item.SelectionFingerprint, Books = item.Books.OrderBy(book => book.SelectionOrder).Select(book => new AccountingBookSelectionBookDto
        { AccountingBookId = book.AccountingBookId, AccountingBookCode = book.AccountingBookCodeSnapshot, SelectionOrder = book.SelectionOrder, AuthorityFingerprint = book.AuthorityFingerprint }).ToList() };
    private static void RequireReason(string value) { if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("A governed reason is required."); }
    private void ApplyRowVersion(AccountingBookApplicabilityPolicy entity, string? encoded) { if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required."); byte[] value; try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); } if (value.Length == 0 || entity.RowVersion.Length > 0 && !entity.RowVersion.SequenceEqual(value)) throw new DbUpdateConcurrencyException("The applicability policy changed after it was loaded."); _db.Entry(entity).Property(item => item.RowVersion).OriginalValue = value; }
    private async Task AuditAsync(string type, AccountingBookApplicabilityPolicy entity, string reason, CancellationToken ct) => await _audit.RecordAsync(new FinanceAuditEventDto
    { TenantId = TenantId, EventType = type, SourceModule = "GL", SourceDocumentType = WorkflowEntityType, SourceDocumentId = entity.Id,
        WorkflowInstanceId = entity.WorkflowInstanceId, Resource = "Finance.AccountingBookApplicabilityPolicy", ResourceId = entity.Id.ToString(),
        // The audit snapshot is deliberately reconstructible without rereading mutable policy tables.
        AfterValues = new { entity.Id, entity.PolicyCode, entity.Version, entity.SupersedesPolicyId, entity.Name, entity.Description,
            Status = entity.PolicyStatus.ToString(), entity.EffectiveFrom, entity.EffectiveTo, entity.Reason,
            entity.CreatedByUserId, entity.CreatedAt, entity.PreparedByUserId, entity.PreparedAtUtc,
            entity.WorkflowInstanceId, entity.ApprovedByUserId, entity.ApprovedAtUtc,
            entity.DecidedByUserId, entity.DecidedAtUtc, entity.DecisionReason,
            entity.RetiredByUserId, entity.RetiredAtUtc, entity.RetirementRequestedByUserId,
            entity.RetirementRequestedAtUtc, entity.RetirementReason, entity.RetirementWorkflowInstanceId,
            entity.RetirementDecisionStatus, entity.RetirementDecidedByUserId, entity.RetirementDecidedAtUtc,
            entity.RetirementDecisionReason, entity.UpdatedBy, entity.UpdatedAt,
            RowVersion = entity.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(entity.RowVersion),
            Rules = entity.Rules.OrderBy(item => item.SortOrder).ThenBy(item => item.RuleCode).Select(item => new
            { item.Id, item.RuleCode, item.Priority, item.OriginatingModuleCode, item.SourceDocumentType, item.PostingAction, item.SortOrder,
              Books = item.SelectedBooks.OrderBy(book => book.SelectionOrder).Select(book => new { book.AccountingBookId, book.AccountingBookCodeSnapshot, book.SelectionOrder }) }) }, Reason = reason.Trim() }, ct);
    private Guid Actor() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private string ActorName() => _currentUser.UserName ?? "system";
    private sealed record SelectionValidation(IReadOnlyList<AccountingBookSelectionBlockerDto> Blockers);
    private Task<T> AtomicAsync<T>(Func<Task<T>> action, CancellationToken ct) =>
        !_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null
            ? action()
            : _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    { await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); try { var result = await action(); await tx.CommitAsync(ct); return result; } catch { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); throw; } });
}
