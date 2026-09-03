using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountClassificationService : IAccountClassificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _audit;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountClassificationService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AccountClassificationDto>> GetAsync(Guid? accountingBookId = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.AccountClassifications.AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.AccountingBook)
            .Include(item => item.ParentClassification)
            .Include(item => item.Children.Where(child => !child.IsDeleted))
            .Include(item => item.AccountMappings.Where(mapping => !mapping.IsDeleted))
            .Where(item => item.TenantId == TenantId && !item.IsDeleted);
        if (accountingBookId.HasValue) query = query.Where(item => item.AccountingBookId == accountingBookId);
        if (!includeInactive) query = query.Where(item => item.Status == AccountClassificationStatus.Active);
        return (await query.OrderBy(item => item.AccountingBook.SortOrder).ThenBy(item => item.DisplayOrder).ThenBy(item => item.Code)
            .ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<AccountClassificationWhereUsedDto> GetWhereUsedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var classification = await _db.AccountClassifications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Account classification was not found.");
        var mappings = await _db.AccountAccountingBooks.AsNoTracking()
            .Include(item => item.Account)
            .Include(item => item.AccountingBook)
            .Where(item => item.TenantId == TenantId && item.AccountClassificationId == id && !item.IsDeleted)
            .OrderBy(item => item.Account.AccountCode)
            .Select(item => new AccountClassificationUsageDto
            {
                AccountAccountingBookId = item.Id,
                AccountId = item.AccountId,
                AccountCode = item.Account.AccountCode,
                AccountName = item.Account.AccountName,
                AccountingBookId = item.AccountingBookId,
                AccountingBookCode = item.AccountingBook.Code,
                IsEnabled = item.IsEnabled
            }).ToListAsync(cancellationToken);
        return new AccountClassificationWhereUsedDto
        {
            ClassificationId = classification.Id,
            ClassificationCode = classification.Code,
            TotalMappings = mappings.Count,
            EnabledMappings = mappings.Count(item => item.IsEnabled),
            Mappings = mappings
        };
    }

    public async Task<AccountClassificationDto> CreateAsync(SaveAccountClassificationDto request, CancellationToken cancellationToken = default)
        => await ExecuteAtomicAsync(() => CreateCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<AccountClassificationDto> CreateCoreAsync(SaveAccountClassificationDto request, CancellationToken cancellationToken)
    {
        var parsed = await ValidateAsync(request, null, cancellationToken);
        if (parsed.Status == AccountClassificationStatus.Retired)
            throw new InvalidOperationException("A classification cannot be created in the retired state.");
        var now = DateTime.UtcNow;
        var entity = new AccountClassification
        {
            TenantId = TenantId, AccountingBookId = request.AccountingBookId,
            ParentClassificationId = request.ParentClassificationId, Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(), Description = NormalizeOptional(request.Description),
            CoreAccountType = parsed.AccountType, DefaultRevaluationTreatment = parsed.Treatment,
            SystemRole = parsed.Role, IsPostingClassification = request.IsPostingClassification,
            Status = parsed.Status, DisplayOrder = request.DisplayOrder, CreatedAt = now,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        _db.AccountClassifications.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.AccountClassificationCreated, entity, null, Snapshot(entity), null, cancellationToken);
        var result = Map(await LoadForDtoAsync(entity.Id, cancellationToken));
        return result;
    }

    public async Task<AccountClassificationDto> UpdateAsync(Guid id, SaveAccountClassificationDto request, CancellationToken cancellationToken = default)
        => await ExecuteAtomicAsync(() => UpdateCoreAsync(id, request, cancellationToken), cancellationToken);

    private async Task<AccountClassificationDto> UpdateCoreAsync(Guid id, SaveAccountClassificationDto request, CancellationToken cancellationToken)
    {
        var entity = await _db.AccountClassifications.Include(item => item.AccountingBook)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Account classification was not found.");
        if (entity.Status == AccountClassificationStatus.Retired)
            throw new InvalidOperationException("A retired classification cannot be edited.");
        ApplyRowVersion(entity, request.RowVersion);
        var before = Snapshot(entity);
        var parsed = await ValidateAsync(request, id, cancellationToken);
        var used = await _db.AccountAccountingBooks.AnyAsync(item => item.TenantId == TenantId && item.AccountClassificationId == id && !item.IsDeleted, cancellationToken);
        var usedByEnabledMapping = await _db.AccountAccountingBooks.AnyAsync(item =>
            item.TenantId == TenantId
            && item.AccountClassificationId == id
            && item.IsEnabled
            && !item.IsDeleted,
            cancellationToken);
        if (used && (entity.AccountingBookId != request.AccountingBookId
            || !string.Equals(entity.Code, NormalizeCode(request.Code), StringComparison.Ordinal)
            || entity.CoreAccountType != parsed.AccountType))
            throw new InvalidOperationException("A used classification's accounting book, code and core account type are immutable.");
        if (usedByEnabledMapping
            && (parsed.Status != AccountClassificationStatus.Active || !request.IsPostingClassification))
            throw new InvalidOperationException(
                "A classification used by enabled account-book assignments must remain active and posting-enabled.");
        if (parsed.Status == AccountClassificationStatus.Retired)
            throw new InvalidOperationException("Use the governed retirement operation to retire a classification.");
        entity.AccountingBookId = request.AccountingBookId;
        entity.ParentClassificationId = request.ParentClassificationId;
        entity.Code = NormalizeCode(request.Code);
        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.CoreAccountType = parsed.AccountType;
        entity.DefaultRevaluationTreatment = parsed.Treatment;
        entity.SystemRole = parsed.Role;
        entity.IsPostingClassification = request.IsPostingClassification;
        entity.Status = parsed.Status;
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.AccountClassificationUpdated, entity, before, Snapshot(entity), null, cancellationToken);
        var result = Map(await LoadForDtoAsync(entity.Id, cancellationToken));
        return result;
    }

    public async Task<AccountClassificationDto> RetireAsync(Guid id, RetireAccountClassificationDto request, CancellationToken cancellationToken = default)
        => await ExecuteAtomicAsync(() => RetireCoreAsync(id, request, cancellationToken), cancellationToken);

    private async Task<AccountClassificationDto> RetireCoreAsync(Guid id, RetireAccountClassificationDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A retirement reason is required.");
        var entity = await _db.AccountClassifications.Include(item => item.AccountingBook)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Account classification was not found.");
        ApplyRowVersion(entity, request.RowVersion);
        var before = Snapshot(entity);
        if (await _db.AccountAccountingBooks.AnyAsync(item =>
                item.TenantId == TenantId
                && item.AccountClassificationId == id
                && item.IsEnabled
                && !item.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException(
                "A classification used by enabled account-book assignments cannot be retired.");
        if (await _db.AccountClassifications.AnyAsync(item =>
                item.TenantId == TenantId
                && item.ParentClassificationId == id
                && item.Status != AccountClassificationStatus.Retired
                && !item.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException("A classification with non-retired children cannot be retired.");
        entity.Status = AccountClassificationStatus.Retired;
        entity.RetirementReason = request.Reason.Trim();
        entity.RetiredAtUtc = DateTime.UtcNow;
        entity.RetiredByUserId = Guid.TryParse(_currentUser.UserId, out var actor) ? actor : null;
        entity.UpdatedAt = entity.RetiredAtUtc;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.AccountClassificationRetired, entity, before, Snapshot(entity), request.Reason.Trim(), cancellationToken);
        var result = Map(await LoadForDtoAsync(entity.Id, cancellationToken));
        return result;
    }

    private async Task<(AccountType AccountType, RevaluationTreatment Treatment, AccountClassificationSystemRole? Role, AccountClassificationStatus Status)> ValidateAsync(SaveAccountClassificationDto request, Guid? currentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Classification code and name are required.");
        if (!Enum.TryParse<AccountType>(request.CoreAccountType, true, out var accountType)) throw new InvalidOperationException("Core account type is invalid.");
        if (!Enum.TryParse<RevaluationTreatment>(request.DefaultRevaluationTreatment, true, out var treatment)) throw new InvalidOperationException("Default revaluation treatment is invalid.");
        if (!Enum.TryParse<AccountClassificationStatus>(request.Status, true, out var status)) throw new InvalidOperationException("Classification status is invalid.");
        AccountClassificationSystemRole? role = null;
        if (!string.IsNullOrWhiteSpace(request.SystemRole))
        {
            if (!Enum.TryParse<AccountClassificationSystemRole>(request.SystemRole, true, out var parsedRole)) throw new InvalidOperationException("Classification system role is invalid.");
            role = parsedRole;
        }
        ValidateSystemRole(accountType, role);
        var book = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.AccountingBookId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Accounting book is invalid for this tenant.");
        var code = NormalizeCode(request.Code);
        if (await _db.AccountClassifications.AnyAsync(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && item.Code == code && !item.IsDeleted && item.Id != currentId, cancellationToken))
            throw new InvalidOperationException("Classification code already exists in this accounting book.");
        if (role.HasValue && !IsRepeatableSystemRole(role.Value)
            && await _db.AccountClassifications.AnyAsync(item => item.TenantId == TenantId
                && item.AccountingBookId == book.Id && item.SystemRole == role && !item.IsDeleted
                && item.Id != currentId, cancellationToken))
            throw new InvalidOperationException($"System role {role} may be assigned only once in an accounting book.");
        if (request.ParentClassificationId.HasValue)
        {
            if (request.ParentClassificationId == currentId) throw new InvalidOperationException("A classification cannot be its own parent.");
            var parent = await _db.AccountClassifications.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ParentClassificationId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Parent classification is invalid for this tenant.");
            if (parent.AccountingBookId != book.Id || parent.CoreAccountType != accountType) throw new InvalidOperationException("Parent and child must share accounting book and core account type.");
            if (parent.IsPostingClassification) throw new InvalidOperationException("A posting classification cannot have children.");
            if (status == AccountClassificationStatus.Active && parent.Status != AccountClassificationStatus.Active)
                throw new InvalidOperationException("An active classification requires an active parent.");
            if (currentId.HasValue && await IsDescendantAsync(request.ParentClassificationId.Value, currentId.Value, cancellationToken)) throw new InvalidOperationException("Classification hierarchy cycles are prohibited.");
        }
        if (request.IsPostingClassification && currentId.HasValue
            && await _db.AccountClassifications.AnyAsync(item => item.ParentClassificationId == currentId.Value && !item.IsDeleted, cancellationToken))
            throw new InvalidOperationException("A classification with children cannot be a posting classification.");
        if (role.HasValue && !request.IsPostingClassification)
            throw new InvalidOperationException("A system role may only be assigned to a posting classification.");
        if (currentId.HasValue && status != AccountClassificationStatus.Active
            && await _db.AccountClassifications.AnyAsync(item =>
                item.TenantId == TenantId
                && item.ParentClassificationId == currentId.Value
                && item.Status == AccountClassificationStatus.Active
                && !item.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException("A classification with active children must remain active.");
        return (accountType, treatment, role, status);
    }

    private static void ValidateSystemRole(AccountType accountType, AccountClassificationSystemRole? role)
    {
        if (!role.HasValue) return;
        var valid = role.Value switch
        {
            AccountClassificationSystemRole.PayableControl or AccountClassificationSystemRole.OutputTax or AccountClassificationSystemRole.WhtPayable
                => accountType == AccountType.Liability,
            _ => accountType == AccountType.Asset
        };
        if (!valid) throw new InvalidOperationException($"System role {role} is incompatible with core account type {accountType}.");
    }

    private static bool IsRepeatableSystemRole(AccountClassificationSystemRole role) =>
        role is AccountClassificationSystemRole.Cash or AccountClassificationSystemRole.Bank;

    private async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational()) return await operation();
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation();
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

    private async Task<bool> IsDescendantAsync(Guid candidateId, Guid ancestorId, CancellationToken cancellationToken)
    {
        var seen = new HashSet<Guid>();
        Guid? cursor = candidateId;
        while (cursor.HasValue && seen.Add(cursor.Value))
        {
            if (cursor.Value == ancestorId) return true;
            cursor = await _db.AccountClassifications.AsNoTracking().Where(item => item.Id == cursor.Value && item.TenantId == TenantId && !item.IsDeleted).Select(item => item.ParentClassificationId).SingleOrDefaultAsync(cancellationToken);
        }
        return false;
    }

    private void ApplyRowVersion(AccountClassification entity, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Row version is required.");
        try { _db.Entry(entity).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(value); }
        catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task<AccountClassification> LoadForDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.AccountClassifications.AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.AccountingBook)
            .Include(item => item.ParentClassification)
            .Include(item => item.Children.Where(child => !child.IsDeleted))
            .Include(item => item.AccountMappings.Where(mapping => !mapping.IsDeleted))
            .SingleAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);

    private async Task RecordAuditAsync(string eventType, AccountClassification item, object? before, object after, string? reason, CancellationToken cancellationToken) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            TenantId = TenantId,
            EventType = eventType,
            SourceModule = "GL",
            SourceDocumentType = "AccountClassification",
            SourceDocumentId = item.Id,
            Resource = "Finance.AccountClassification",
            ResourceId = item.Id.ToString(),
            BeforeValues = before,
            AfterValues = after,
            Reason = reason
        }, cancellationToken);

    private static object Snapshot(AccountClassification item) => new
    {
        item.AccountingBookId,
        item.ParentClassificationId,
        item.Code,
        item.Name,
        item.Description,
        CoreAccountType = item.CoreAccountType.ToString(),
        DefaultRevaluationTreatment = item.DefaultRevaluationTreatment.ToString(),
        SystemRole = item.SystemRole?.ToString(),
        item.IsPostingClassification,
        Status = item.Status.ToString(),
        item.DisplayOrder,
        item.RetirementReason,
        item.RetiredAtUtc,
        item.RetiredByUserId
    };

    private static AccountClassificationDto Map(AccountClassification item) => new()
    {
        Id = item.Id, AccountingBookId = item.AccountingBookId, AccountingBookCode = item.AccountingBook.Code,
        ParentClassificationId = item.ParentClassificationId,
        ParentClassificationCode = item.ParentClassification?.Code,
        ParentClassificationName = item.ParentClassification?.Name,
        Code = item.Code, Name = item.Name,
        Description = item.Description, CoreAccountType = item.CoreAccountType.ToString(),
        DefaultRevaluationTreatment = item.DefaultRevaluationTreatment.ToString(), SystemRole = item.SystemRole?.ToString(),
        IsPostingClassification = item.IsPostingClassification, Status = item.Status.ToString(), DisplayOrder = item.DisplayOrder,
        ChildCount = item.Children.Count(child => !child.IsDeleted),
        NonRetiredChildCount = item.Children.Count(child => !child.IsDeleted && child.Status != AccountClassificationStatus.Retired),
        TotalAccountCount = item.AccountMappings.Count(mapping => !mapping.IsDeleted),
        EnabledAccountCount = item.AccountMappings.Count(mapping => !mapping.IsDeleted && mapping.IsEnabled),
        RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion)
    };
}
