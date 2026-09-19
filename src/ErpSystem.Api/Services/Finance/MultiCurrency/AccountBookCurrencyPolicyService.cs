using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.MultiCurrency;

public sealed class AccountBookCurrencyPolicyService : IAccountBookCurrencyPolicyService
{
    public const string WorkflowEntityType = "AccountBookCurrencyPolicy";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;
    private readonly IFinanceAuditService _audit;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountBookCurrencyPolicyService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IWorkflowService workflow,
        IFinanceAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _workflow = workflow;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AccountBookCurrencyPolicyDto>> GetForAccountAsync(
        Guid accountId, CancellationToken cancellationToken = default)
    {
        var mappings = await AuthorityQuery(accountId)
            .AsNoTracking()
            .OrderBy(item => item.AccountingBook.SortOrder)
            .ThenBy(item => item.AccountingBook.Code)
            .ToListAsync(cancellationToken);
        var links = await _db.AccountCurrencyLinks.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.AccountId == accountId && !item.IsDeleted)
            .OrderBy(item => item.LinkedCurrencyCode)
            .ToListAsync(cancellationToken);
        var policies = await _db.AccountBookCurrencyPolicies.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted
                && mappings.Select(mapping => mapping.Id).Contains(item.AccountAccountingBookId)
                && links.Select(link => link.Id).Contains(item.AccountCurrencyLinkId))
            .ToDictionaryAsync(item => (item.AccountAccountingBookId, item.AccountCurrencyLinkId), cancellationToken);

        return (from mapping in mappings
                from link in links
                let policy = policies.GetValueOrDefault((mapping.Id, link.Id))
                select Map(mapping, link, policy)).ToList();
    }

    public Task<AccountBookCurrencyPolicyDto> SaveAsync(
        Guid accountId,
        Guid accountCurrencyLinkId,
        Guid accountingBookId,
        SaveAccountBookCurrencyPolicyDto request,
        CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new InvalidOperationException("A reason is required for every revaluation-policy override decision.");
            var actor = RequiredActor();
            var mapping = await AuthorityQuery(accountId)
                .SingleOrDefaultAsync(item => item.AccountingBookId == accountingBookId, cancellationToken)
                ?? throw new InvalidOperationException("The account is not enabled with a valid posting classification in the selected accounting book.");
            var link = await _db.AccountCurrencyLinks.SingleOrDefaultAsync(item =>
                item.Id == accountCurrencyLinkId && item.TenantId == TenantId && item.AccountId == accountId
                && item.IsActive && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("The selected account currency link is inactive or invalid for this tenant.");
            var nonstandard = IsNonstandardInclusion(mapping, request.RevaluationOverride);
            if (nonstandard && !request.ConfirmNonstandardInclusion)
                throw new InvalidOperationException("Explicit confirmation is required because this Equity, Revenue or Expense account will enter closing revaluation.");
            if (nonstandard && !await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType))
                throw new InvalidOperationException("A published AccountBookCurrencyPolicy approval workflow is required for non-standard inclusion.");

            var entity = await _db.AccountBookCurrencyPolicies.SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.AccountAccountingBookId == mapping.Id
                && item.AccountCurrencyLinkId == link.Id && !item.IsDeleted, cancellationToken);
            var before = entity == null ? null : Snapshot(entity, mapping, link);
            if (entity == null)
            {
                if (!string.IsNullOrWhiteSpace(request.RowVersion))
                    throw new InvalidOperationException("Row version must be omitted when creating a policy.");
                entity = new AccountBookCurrencyPolicy
                {
                    TenantId = TenantId,
                    AccountAccountingBookId = mapping.Id,
                    AccountCurrencyLinkId = link.Id,
                    CreatedBy = _currentUser.UserName ?? "system",
                    CreatedById = actor
                };
                _db.AccountBookCurrencyPolicies.Add(entity);
            }
            else
            {
                ApplyRowVersion(entity, request.RowVersion);
                if (entity.LifecycleStatus == "PendingApproval")
                    throw new InvalidOperationException("This policy already has a pending maker-checker request.");
            }

            var now = DateTime.UtcNow;
            if (nonstandard)
            {
                entity.PendingRevaluationOverride = true;
                entity.PendingReason = request.Reason.Trim();
                entity.LifecycleStatus = "PendingApproval";
                entity.RequestedByUserId = actor;
                entity.RequestedAtUtc = now;
                entity.DecidedByUserId = null;
                entity.DecidedAtUtc = null;
                entity.DecisionReason = null;
                await _db.SaveChangesAsync(cancellationToken);
                var result = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, entity.Id);
                if (!result.Success)
                    throw new InvalidOperationException(result.Message ?? "The revaluation-policy approval workflow could not be started.");
                entity.WorkflowInstanceId = result.WorkflowInstanceId;
            }
            else
            {
                entity.RevaluationOverride = request.RevaluationOverride;
                entity.OverrideReason = request.Reason.Trim();
                entity.PendingRevaluationOverride = null;
                entity.PendingReason = null;
                entity.LifecycleStatus = "Active";
                entity.WorkflowInstanceId = null;
                entity.RequestedByUserId = actor;
                entity.RequestedAtUtc = now;
                entity.DecidedByUserId = actor;
                entity.DecidedAtUtc = now;
                entity.DecisionReason = request.Reason.Trim();
            }
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.UserName ?? "system";
            entity.LastModifiedById = actor;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(nonstandard ? FinanceAuditEvents.FxPolicyOverrideRequested : FinanceAuditEvents.FxPolicyOverrideChanged,
                entity, mapping, link, before, request.Reason, cancellationToken);
            return Map(mapping, link, entity);
        }, cancellationToken);

    public Task<AccountBookCurrencyPolicyDto> ApproveAsync(
        Guid accountId, Guid policyId, DecideAccountBookCurrencyPolicyDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(accountId, policyId, request, "Approve", cancellationToken);

    public Task<AccountBookCurrencyPolicyDto> RejectAsync(
        Guid accountId, Guid policyId, DecideAccountBookCurrencyPolicyDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(accountId, policyId, request, "Reject", cancellationToken);

    private Task<AccountBookCurrencyPolicyDto> DecideAsync(
        Guid accountId, Guid policyId, DecideAccountBookCurrencyPolicyDto request, string action, CancellationToken cancellationToken) =>
        ExecuteAtomicAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A decision reason is required.");
            var actor = RequiredActor();
            var entity = await _db.AccountBookCurrencyPolicies
                .Include(item => item.AccountAccountingBook).ThenInclude(item => item.Account)
                .Include(item => item.AccountAccountingBook).ThenInclude(item => item.AccountingBook)
                .Include(item => item.AccountAccountingBook).ThenInclude(item => item.AccountClassification)
                .Include(item => item.AccountCurrencyLink)
                .SingleOrDefaultAsync(item => item.Id == policyId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Revaluation policy was not found.");
            ValidateAuthority(entity.AccountAccountingBook, entity.AccountCurrencyLink);
            if (entity.AccountAccountingBook.AccountId != accountId)
                throw new KeyNotFoundException("Revaluation policy was not found for this account.");
            ApplyRowVersion(entity, request.RowVersion);
            if (entity.LifecycleStatus != "PendingApproval" || entity.PendingRevaluationOverride != true)
                throw new InvalidOperationException("The policy has no pending non-standard inclusion request.");
            if (entity.RequestedByUserId == actor)
                throw new InvalidOperationException("Maker-checker control prohibits the requester from deciding this policy.");
            if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, entity.Id, actor))
                throw new UnauthorizedAccessException("The current user is not an assigned approver for this policy.");
            var before = Snapshot(entity, entity.AccountAccountingBook, entity.AccountCurrencyLink);
            var result = await _workflow.ProcessApprovalStepAsync(WorkflowEntityType, entity.Id, actor, action, request.Reason.Trim());
            if (!result.Success) throw new InvalidOperationException(result.Message ?? $"The policy {action.ToLowerInvariant()} action failed.");

            entity.DecidedByUserId = actor;
            entity.DecidedAtUtc = DateTime.UtcNow;
            entity.DecisionReason = request.Reason.Trim();
            if (action == "Reject")
            {
                entity.PendingRevaluationOverride = null;
                entity.PendingReason = null;
                entity.LifecycleStatus = "Rejected";
            }
            else if (result.Status == WorkflowInstanceStatus.Completed)
            {
                entity.RevaluationOverride = entity.PendingRevaluationOverride;
                entity.OverrideReason = entity.PendingReason;
                entity.PendingRevaluationOverride = null;
                entity.PendingReason = null;
                entity.LifecycleStatus = "Active";
            }
            else
            {
                entity.LifecycleStatus = "PendingApproval";
            }
            await _db.SaveChangesAsync(cancellationToken);
            var auditEvent = action == "Reject"
                ? FinanceAuditEvents.FxPolicyOverrideRejected
                : result.Status == WorkflowInstanceStatus.Completed
                    ? FinanceAuditEvents.FxPolicyOverrideApproved
                    : FinanceAuditEvents.FxPolicyOverrideApprovalStepCompleted;
            await AuditAsync(auditEvent,
                entity, entity.AccountAccountingBook, entity.AccountCurrencyLink, before, request.Reason, cancellationToken);
            return Map(entity.AccountAccountingBook, entity.AccountCurrencyLink, entity);
        }, cancellationToken);

    private IQueryable<AccountAccountingBook> AuthorityQuery(Guid accountId) =>
        _db.AccountAccountingBooks
            .Include(item => item.Account)
            .Include(item => item.AccountingBook)
            .Include(item => item.AccountClassification)
            .Where(item => item.TenantId == TenantId && item.AccountId == accountId && item.IsEnabled && !item.IsDeleted
                && !item.Account.IsDeleted && item.Account.Status == AccountStatus.Active
                && item.AccountingBook.TenantId == TenantId && item.AccountingBook.IsActive && item.AccountingBook.AllowsPosting && !item.AccountingBook.IsDeleted
                && item.AccountClassificationId.HasValue
                && item.AccountClassification!.TenantId == TenantId
                && item.AccountClassification.AccountingBookId == item.AccountingBookId
                && item.AccountClassification.Status == AccountClassificationStatus.Active
                && item.AccountClassification.IsPostingClassification && !item.AccountClassification.IsDeleted);

    private void ValidateAuthority(AccountAccountingBook mapping, AccountCurrencyLink link)
    {
        if (mapping.TenantId != TenantId || link.TenantId != TenantId || mapping.AccountId != link.AccountId
            || !mapping.IsEnabled || mapping.IsDeleted || mapping.Account.Status != AccountStatus.Active
            || !link.IsActive || link.IsDeleted
            || mapping.AccountingBook.TenantId != TenantId || !mapping.AccountingBook.IsActive || !mapping.AccountingBook.AllowsPosting
            || mapping.AccountClassification is null || mapping.AccountClassification.TenantId != TenantId
            || mapping.AccountClassification.AccountingBookId != mapping.AccountingBookId
            || mapping.AccountClassification.Status != AccountClassificationStatus.Active
            || !mapping.AccountClassification.IsPostingClassification)
            throw new InvalidOperationException("The policy's account, currency link, book or classification authority is no longer valid.");
    }

    private static bool IsNonstandardInclusion(AccountAccountingBook mapping, bool? value) =>
        value == true && mapping.AccountClassification!.CoreAccountType is AccountType.Equity or AccountType.Revenue or AccountType.Expense;

    public static AccountBookCurrencyPolicyDto Map(
        AccountAccountingBook mapping, AccountCurrencyLink link, AccountBookCurrencyPolicy? policy)
    {
        var classification = mapping.AccountClassification!;
        var inherited = classification.DefaultRevaluationTreatment == RevaluationTreatment.Include;
        var effective = policy?.RevaluationOverride ?? inherited;
        var nonstandard = effective && classification.CoreAccountType is AccountType.Equity or AccountType.Revenue or AccountType.Expense;
        return new AccountBookCurrencyPolicyDto
        {
            Id = policy?.Id,
            AccountId = mapping.AccountId,
            AccountAccountingBookId = mapping.Id,
            AccountCurrencyLinkId = link.Id,
            AccountingBookId = mapping.AccountingBookId,
            AccountingBookCode = mapping.AccountingBook.Code,
            AccountingBookName = mapping.AccountingBook.Name,
            CurrencyCode = link.LinkedCurrencyCode,
            AccountClassificationId = classification.Id,
            AccountClassificationCode = classification.Code,
            AccountClassificationName = classification.Name,
            CoreAccountType = classification.CoreAccountType.ToString(),
            ClassificationDefault = classification.DefaultRevaluationTreatment.ToString(),
            RevaluationOverride = policy?.RevaluationOverride,
            EffectiveRevaluationRequired = effective,
            EffectiveSource = policy?.RevaluationOverride.HasValue == true ? "CurrencyOverride" : "Classification",
            IsNonstandardInclusion = nonstandard,
            Warning = nonstandard
                ? "Non-standard revaluation policy: this Equity, Revenue or Expense exposure is included in closing revaluation."
                : null,
            LifecycleStatus = policy?.LifecycleStatus ?? "Inherited",
            PendingRevaluationOverride = policy?.PendingRevaluationOverride,
            PendingReason = policy?.PendingReason,
            WorkflowInstanceId = policy?.WorkflowInstanceId,
            RequestedByUserId = policy?.RequestedByUserId,
            RequestedAtUtc = policy?.RequestedAtUtc,
            DecidedByUserId = policy?.DecidedByUserId,
            DecidedAtUtc = policy?.DecidedAtUtc,
            DecisionReason = policy?.DecisionReason,
            RowVersion = policy == null || policy.RowVersion.Length == 0 ? null : Convert.ToBase64String(policy.RowVersion)
        };
    }

    private Guid RequiredActor() => Guid.TryParse(_currentUser.UserId, out var actor) && actor != Guid.Empty
        ? actor : throw new UnauthorizedAccessException("An authenticated Finance user is required.");

    private void ApplyRowVersion(AccountBookCurrencyPolicy entity, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) throw new InvalidOperationException("Row version is required for an existing policy.");
        try { _db.Entry(entity).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
    }

    private async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            try { return await action(); }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        }
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task AuditAsync(string eventType, AccountBookCurrencyPolicy entity,
        AccountAccountingBook mapping, AccountCurrencyLink link, object? before, string reason,
        CancellationToken cancellationToken) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = "FX",
            SourceDocumentType = WorkflowEntityType,
            SourceDocumentId = entity.Id,
            WorkflowInstanceId = entity.WorkflowInstanceId,
            Resource = "Finance.AccountBookCurrencyPolicy",
            ResourceId = entity.Id.ToString(),
            BeforeValues = before,
            AfterValues = Snapshot(entity, mapping, link),
            Reason = reason.Trim(),
            Context = new
            {
                mapping.AccountId,
                mapping.AccountingBookId,
                AccountingBookCode = mapping.AccountingBook.Code,
                CurrencyCode = link.LinkedCurrencyCode,
                AccountClassificationId = mapping.AccountClassificationId,
                ClassificationCode = mapping.AccountClassification?.Code,
                ActorUserId = RequiredActor(),
                TimestampUtc = DateTime.UtcNow
            }
        }, cancellationToken);

    private static object Snapshot(AccountBookCurrencyPolicy entity, AccountAccountingBook mapping, AccountCurrencyLink link) => new
    {
        entity.RevaluationOverride,
        entity.OverrideReason,
        entity.PendingRevaluationOverride,
        entity.PendingReason,
        entity.LifecycleStatus,
        entity.WorkflowInstanceId,
        entity.RequestedByUserId,
        entity.RequestedAtUtc,
        entity.DecidedByUserId,
        entity.DecidedAtUtc,
        entity.DecisionReason,
        mapping.AccountId,
        mapping.AccountingBookId,
        AccountingBookCode = mapping.AccountingBook.Code,
        CurrencyCode = link.LinkedCurrencyCode,
        AccountClassificationId = mapping.AccountClassificationId,
        ClassificationCode = mapping.AccountClassification?.Code,
        ClassificationDefault = mapping.AccountClassification?.DefaultRevaluationTreatment.ToString()
    };
}
