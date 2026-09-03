using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountClassificationService : IAccountClassificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountClassificationService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AccountClassificationDto>> GetAsync(Guid? accountingBookId = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.AccountClassifications.AsNoTracking()
            .Include(item => item.AccountingBook)
            .Where(item => item.TenantId == TenantId && !item.IsDeleted);
        if (accountingBookId.HasValue) query = query.Where(item => item.AccountingBookId == accountingBookId);
        if (!includeInactive) query = query.Where(item => item.Status == AccountClassificationStatus.Active);
        return (await query.OrderBy(item => item.AccountingBook.SortOrder).ThenBy(item => item.DisplayOrder).ThenBy(item => item.Code)
            .ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<AccountClassificationDto> CreateAsync(SaveAccountClassificationDto request, CancellationToken cancellationToken = default)
    {
        var parsed = await ValidateAsync(request, null, cancellationToken);
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
        await _db.Entry(entity).Reference(item => item.AccountingBook).LoadAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<AccountClassificationDto> UpdateAsync(Guid id, SaveAccountClassificationDto request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AccountClassifications.Include(item => item.AccountingBook)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Account classification was not found.");
        ApplyRowVersion(entity, request.RowVersion);
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
        return Map(entity);
    }

    public async Task<AccountClassificationDto> RetireAsync(Guid id, RetireAccountClassificationDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A retirement reason is required.");
        var entity = await _db.AccountClassifications.Include(item => item.AccountingBook)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Account classification was not found.");
        ApplyRowVersion(entity, request.RowVersion);
        if (await _db.AccountAccountingBooks.AnyAsync(item =>
                item.TenantId == TenantId
                && item.AccountClassificationId == id
                && item.IsEnabled
                && !item.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException(
                "A classification used by enabled account-book assignments cannot be retired.");
        entity.Status = AccountClassificationStatus.Retired;
        entity.RetirementReason = request.Reason.Trim();
        entity.RetiredAtUtc = DateTime.UtcNow;
        entity.RetiredByUserId = Guid.TryParse(_currentUser.UserId, out var actor) ? actor : null;
        entity.UpdatedAt = entity.RetiredAtUtc;
        entity.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
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
        var book = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.AccountingBookId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Accounting book is invalid for this tenant.");
        var code = NormalizeCode(request.Code);
        if (await _db.AccountClassifications.AnyAsync(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && item.Code == code && !item.IsDeleted && item.Id != currentId, cancellationToken))
            throw new InvalidOperationException("Classification code already exists in this accounting book.");
        if (request.ParentClassificationId.HasValue)
        {
            if (request.ParentClassificationId == currentId) throw new InvalidOperationException("A classification cannot be its own parent.");
            var parent = await _db.AccountClassifications.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ParentClassificationId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Parent classification is invalid for this tenant.");
            if (parent.AccountingBookId != book.Id || parent.CoreAccountType != accountType) throw new InvalidOperationException("Parent and child must share accounting book and core account type.");
            if (parent.IsPostingClassification) throw new InvalidOperationException("A posting classification cannot have children.");
            if (currentId.HasValue && await IsDescendantAsync(request.ParentClassificationId.Value, currentId.Value, cancellationToken)) throw new InvalidOperationException("Classification hierarchy cycles are prohibited.");
        }
        if (request.IsPostingClassification && currentId.HasValue
            && await _db.AccountClassifications.AnyAsync(item => item.ParentClassificationId == currentId.Value && !item.IsDeleted, cancellationToken))
            throw new InvalidOperationException("A classification with children cannot be a posting classification.");
        return (accountType, treatment, role, status);
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
    private static AccountClassificationDto Map(AccountClassification item) => new()
    {
        Id = item.Id, AccountingBookId = item.AccountingBookId, AccountingBookCode = item.AccountingBook.Code,
        ParentClassificationId = item.ParentClassificationId, Code = item.Code, Name = item.Name,
        Description = item.Description, CoreAccountType = item.CoreAccountType.ToString(),
        DefaultRevaluationTreatment = item.DefaultRevaluationTreatment.ToString(), SystemRole = item.SystemRole?.ToString(),
        IsPostingClassification = item.IsPostingClassification, Status = item.Status.ToString(), DisplayOrder = item.DisplayOrder,
        RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion)
    };
}
