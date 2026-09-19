using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountingBookPeriodService : IAccountingBookPeriodService
{
    public const string WorkflowEntityType = "AccountingBookPeriodLifecycle";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;
    private readonly IFinanceAuditService _audit;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountingBookPeriodService(ApplicationDbContext db, ICurrentUserService currentUser, IWorkflowService workflow, IFinanceAuditService audit)
        => (_db, _currentUser, _workflow, _audit) = (db, currentUser, workflow, audit);

    public async Task<IReadOnlyList<AccountingBookPeriodDto>> GetAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        await RequireBookAsync(accountingBookId, cancellationToken);
        return (await Query().AsNoTracking().Where(item => item.AccountingBookId == accountingBookId)
            .OrderBy(item => item.FiscalPeriod.StartDate).ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public Task<AccountingBookPeriodDto> CreateAsync(Guid accountingBookId, CreateAccountingBookPeriodDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(async () =>
        {
            var book = await RequireBookAsync(accountingBookId, cancellationToken);
            if (book.LifecycleStatus == AccountingBookLifecycleStatus.Retired)
                throw new InvalidOperationException("A retired accounting book cannot receive period authority.");
            if (!Enum.TryParse<AccountingBookPeriodStatus>(request.InitialStatus, true, out var status) || status != AccountingBookPeriodStatus.Future)
                throw new InvalidOperationException("A new accounting-book period must begin in Future status and be opened through approval.");
            var fiscal = await _db.FiscalPeriods.SingleOrDefaultAsync(item => item.Id == request.FiscalPeriodId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("The fiscal period does not belong to the current tenant.");
            if (await _db.AccountingBookPeriods.AnyAsync(item => item.TenantId == TenantId && item.AccountingBookId == accountingBookId && item.FiscalPeriodId == fiscal.Id && !item.IsDeleted, cancellationToken))
                throw new InvalidOperationException("Accounting-book period authority already exists for this fiscal period.");
            var entity = new AccountingBookPeriod { TenantId = TenantId, AccountingBookId = book.Id, FiscalPeriodId = fiscal.Id,
                PeriodStatus = status, CreatedAt = DateTime.UtcNow, CreatedBy = ActorName() };
            _db.AccountingBookPeriods.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(FinanceAuditEvents.AccountingBookPeriodCreated, entity, "Accounting-book period authority created.", cancellationToken);
            return Map(await Query().AsNoTracking().SingleAsync(item => item.Id == entity.Id, cancellationToken));
        }, cancellationToken);

    public Task<AccountingBookPeriodDto> RequestTransitionAsync(Guid accountingBookId, Guid id, RequestAccountingBookPeriodTransitionDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(async () =>
        {
            var entity = await Query().SingleOrDefaultAsync(item => item.Id == id && item.AccountingBookId == accountingBookId, cancellationToken) ?? throw new KeyNotFoundException("Accounting-book period was not found for the selected accounting book.");
            ApplyRowVersion(entity, request.RowVersion);
            if (!Enum.TryParse<AccountingBookPeriodStatus>(request.TargetStatus, true, out var target)) throw new InvalidOperationException("Accounting-book period target status is invalid.");
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A period-transition reason is required.");
            if (entity.PendingStatus.HasValue) throw new InvalidOperationException("The accounting-book period already has a pending transition.");
            ValidateTransition(entity.PeriodStatus, target);
            ValidateOuterPeriodAllows(entity.FiscalPeriod, target);
            if (!await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType)) throw new InvalidOperationException("A published AccountingBookPeriodLifecycle approval workflow is required.");
            entity.PendingStatus = target; entity.PendingReason = request.Reason.Trim(); entity.RequestedByUserId = Actor(); entity.RequestedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            var workflow = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, entity.Id);
            if (!workflow.Success) throw new InvalidOperationException(workflow.Message ?? "Accounting-book period workflow could not be started.");
            entity.WorkflowInstanceId = workflow.WorkflowInstanceId;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(FinanceAuditEvents.AccountingBookPeriodTransitionRequested, entity, request.Reason, cancellationToken);
            return Map(entity);
        }, cancellationToken);

    public Task<AccountingBookPeriodDto> ApproveAsync(Guid accountingBookId, Guid id, DecideAccountingBookPeriodTransitionDto request, CancellationToken cancellationToken = default) => DecideAsync(accountingBookId, id, request, true, cancellationToken);
    public Task<AccountingBookPeriodDto> RejectAsync(Guid accountingBookId, Guid id, DecideAccountingBookPeriodTransitionDto request, CancellationToken cancellationToken = default) => DecideAsync(accountingBookId, id, request, false, cancellationToken);

    private Task<AccountingBookPeriodDto> DecideAsync(Guid accountingBookId, Guid id, DecideAccountingBookPeriodTransitionDto request, bool approve, CancellationToken ct) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A period-transition decision reason is required.");
        var entity = await Query().SingleOrDefaultAsync(item => item.Id == id && item.AccountingBookId == accountingBookId, ct) ?? throw new KeyNotFoundException("Accounting-book period was not found for the selected accounting book.");
        ApplyRowVersion(entity, request.RowVersion);
        if (!entity.PendingStatus.HasValue || !entity.WorkflowInstanceId.HasValue) throw new InvalidOperationException("No accounting-book period transition is pending.");
        var actor = Actor();
        if (actor == entity.RequestedByUserId) throw new InvalidOperationException("The transition checker must differ from the maker.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, entity.Id, actor)) throw new UnauthorizedAccessException("The current user cannot decide this accounting-book period transition.");
        var action = approve ? "Approve" : "Reject";
        var outcome = await _workflow.ProcessApprovalStepAsync(WorkflowEntityType, entity.Id, actor, action, request.Reason.Trim());
        if (!outcome.Success) throw new InvalidOperationException(outcome.Message ?? "Accounting-book period workflow decision failed.");
        var completed = outcome.Status == WorkflowInstanceStatus.Completed;
        if (approve && completed)
        {
            ValidateTransition(entity.PeriodStatus, entity.PendingStatus.Value);
            ValidateOuterPeriodAllows(entity.FiscalPeriod, entity.PendingStatus.Value);
            entity.PeriodStatus = entity.PendingStatus.Value;
        }
        entity.DecidedByUserId = actor; entity.DecidedAtUtc = DateTime.UtcNow; entity.DecisionReason = request.Reason.Trim();
        if (completed || !approve) { entity.PendingStatus = null; entity.PendingReason = null; }
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        await AuditAsync(!completed && approve ? FinanceAuditEvents.AccountingBookPeriodTransitionApprovalStepCompleted
            : approve ? FinanceAuditEvents.AccountingBookPeriodTransitionApproved : FinanceAuditEvents.AccountingBookPeriodTransitionRejected,
            entity, request.Reason, ct);
        return Map(entity);
    }, ct);

    private IQueryable<AccountingBookPeriod> Query() => _db.AccountingBookPeriods.Include(item => item.AccountingBook).Include(item => item.FiscalPeriod)
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);
    private async Task<AccountingBook> RequireBookAsync(Guid id, CancellationToken ct) =>
        await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, ct)
        ?? throw new KeyNotFoundException("Accounting book was not found.");
    private static void ValidateTransition(AccountingBookPeriodStatus from, AccountingBookPeriodStatus to)
    {
        var valid = (from, to) switch { (AccountingBookPeriodStatus.Future, AccountingBookPeriodStatus.Open) => true,
            (AccountingBookPeriodStatus.Open, AccountingBookPeriodStatus.Closed) => true,
            (AccountingBookPeriodStatus.Open, AccountingBookPeriodStatus.Locked) => true,
            (AccountingBookPeriodStatus.Closed, AccountingBookPeriodStatus.Open) => true,
            (AccountingBookPeriodStatus.Closed, AccountingBookPeriodStatus.Locked) => true,
            (AccountingBookPeriodStatus.Locked, AccountingBookPeriodStatus.Open) => true, _ => false };
        if (!valid) throw new InvalidOperationException($"Accounting-book period transition {from} to {to} is not allowed.");
    }
    private static void ValidateOuterPeriodAllows(FiscalPeriod period, AccountingBookPeriodStatus target)
    {
        // Book authority is an inner gate: it can restrict a tenant period but never reopen an outer closed/locked period.
        if (target == AccountingBookPeriodStatus.Open && (!period.IsOpen || period.IsClosed || period.IsLocked))
            throw new InvalidOperationException("The tenant fiscal period must be open and unlocked before this accounting-book period can open.");
    }
    private async Task AuditAsync(string type, AccountingBookPeriod entity, string reason, CancellationToken ct) => await _audit.RecordAsync(new FinanceAuditEventDto
    { TenantId = TenantId, EventType = type, SourceModule = "GL", SourceDocumentType = WorkflowEntityType, SourceDocumentId = entity.Id,
        WorkflowInstanceId = entity.WorkflowInstanceId, Resource = "Finance.AccountingBookPeriod", ResourceId = entity.Id.ToString(), AfterValues = new
        { entity.AccountingBookId, entity.FiscalPeriodId, Status = entity.PeriodStatus.ToString(), Pending = entity.PendingStatus?.ToString(), entity.RequestedByUserId, entity.DecidedByUserId }, Reason = reason.Trim() }, ct);
    private void ApplyRowVersion(AccountingBookPeriod entity, string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required.");
        byte[] value; try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
        if (value.Length == 0 || entity.RowVersion.Length > 0 && !entity.RowVersion.SequenceEqual(value)) throw new DbUpdateConcurrencyException("The accounting-book period changed after it was loaded.");
        _db.Entry(entity).Property(item => item.RowVersion).OriginalValue = value;
    }
    private static AccountingBookPeriodDto Map(AccountingBookPeriod item) => new() { Id = item.Id, AccountingBookId = item.AccountingBookId,
        AccountingBookCode = item.AccountingBook.Code, FiscalPeriodId = item.FiscalPeriodId, FiscalPeriodCode = item.FiscalPeriod.PeriodCode,
        StartDate = item.FiscalPeriod.StartDate, EndDate = item.FiscalPeriod.EndDate, Status = item.PeriodStatus.ToString(), PendingStatus = item.PendingStatus?.ToString(),
        PendingReason = item.PendingReason, RequestedByUserId = item.RequestedByUserId, RequestedAtUtc = item.RequestedAtUtc,
        RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion) };
    private Guid Actor() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private string ActorName() => _currentUser.UserName ?? "system";
    private Task<T> AtomicAsync<T>(Func<Task<T>> action, CancellationToken ct) => !_db.Database.IsRelational() ? action() : _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    { await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); try { var result = await action(); await tx.CommitAsync(ct); return result; }
      catch { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); throw; } });
}
