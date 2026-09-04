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

namespace ErpSystem.Api.Services.Finance.Segments;

/// <summary>Governed definition service for GL account-number identity segments.</summary>
public sealed class AccountSegmentStructureService : IAccountSegmentStructureService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _audit;
    private readonly ILogger<AccountSegmentStructureService> _logger;

    public AccountSegmentStructureService(ApplicationDbContext db, ICurrentUserService currentUser,
        IFinanceAuditService audit, ILogger<AccountSegmentStructureService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<IReadOnlyList<AccountSegmentStructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await Query().OrderBy(item => item.SegmentPosition).ToListAsync(cancellationToken);
        return await MapAsync(items, cancellationToken);
    }

    public async Task<AccountSegmentStructureDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await Query().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item == null ? null : (await MapAsync(new[] { item }, cancellationToken)).Single();
    }

    public Task<AccountSegmentStructureDto> CreateAsync(AccountSegmentStructureCreateDto dto, CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(async () =>
        {
            ValidateDefinition(dto.SegmentName, dto.SegmentCode, dto.SegmentPosition, dto.SegmentLength, dto.DataType);
            var code = NormalizeCode(dto.SegmentCode);
            await EnsureUniqueAsync(null, code, dto.SegmentPosition, dto.IsNaturalAccount, cancellationToken);
            var now = DateTime.UtcNow;
            var item = new AccountSegmentStructure
            {
                Id = Guid.NewGuid(), TenantId = TenantId, SegmentName = dto.SegmentName.Trim(), SegmentCode = code,
                SegmentPosition = dto.SegmentPosition, SegmentLength = dto.SegmentLength, DataType = dto.DataType,
                SeparatorCharacter = NormalizeOptional(dto.SeparatorCharacter), LookupTableRequired = dto.LookupTableRequired,
                IsReportingDimension = false, IsNaturalAccount = dto.IsNaturalAccount,
                LifecycleStatus = AccountSegmentLifecycleStatus.Draft, IsActive = false,
                Description = NormalizeOptional(dto.Description), CreatedAt = now, CreatedBy = UserName
            };
            _db.AccountSegmentStructures.Add(item);
            await _db.SaveChangesAsync(cancellationToken);
            await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureCreated, item, null, Snapshot(item), null, cancellationToken);
            _logger.LogInformation("Created draft GL identity segment {SegmentCode}", code);
            return (await GetByIdAsync(item.Id, cancellationToken))!;
        }, cancellationToken);

    public Task<AccountSegmentStructureDto> UpdateAsync(AccountSegmentStructureUpdateDto dto, CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(async () =>
        {
            var item = await LoadAsync(dto.Id, cancellationToken);
            ApplyRowVersion(item, dto.RowVersion);
            if (item.LifecycleStatus is AccountSegmentLifecycleStatus.Frozen or AccountSegmentLifecycleStatus.Retired)
                throw new InvalidOperationException("Frozen or retired account-number segments cannot be changed.");
            var usage = await UsageCountAsync(item.Id, cancellationToken);
            var structuralChange = item.SegmentCode != NormalizeCode(dto.SegmentCode)
                || item.SegmentPosition != dto.SegmentPosition || item.SegmentLength != dto.SegmentLength
                || !string.Equals(item.DataType, dto.DataType, StringComparison.OrdinalIgnoreCase)
                || item.IsNaturalAccount != dto.IsNaturalAccount || item.LookupTableRequired != dto.LookupTableRequired
                || !string.Equals(item.SeparatorCharacter, NormalizeOptional(dto.SeparatorCharacter), StringComparison.Ordinal);
            if (structuralChange && (item.LifecycleStatus != AccountSegmentLifecycleStatus.Draft || usage > 0))
                throw new InvalidOperationException("A used or active account-number segment cannot be structurally changed.");
            ValidateDefinition(dto.SegmentName, dto.SegmentCode, dto.SegmentPosition, dto.SegmentLength, dto.DataType);
            var before = Snapshot(item);
            var code = NormalizeCode(dto.SegmentCode);
            await EnsureUniqueAsync(item.Id, code, dto.SegmentPosition, dto.IsNaturalAccount, cancellationToken);
            item.SegmentName = dto.SegmentName.Trim(); item.SegmentCode = code; item.SegmentPosition = dto.SegmentPosition;
            item.SegmentLength = dto.SegmentLength; item.DataType = dto.DataType.Trim();
            item.SeparatorCharacter = NormalizeOptional(dto.SeparatorCharacter); item.LookupTableRequired = dto.LookupTableRequired;
            item.IsNaturalAccount = dto.IsNaturalAccount; item.IsReportingDimension = false;
            item.Description = NormalizeOptional(dto.Description); item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = UserName;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureUpdated, item, before, Snapshot(item), null, cancellationToken);
            return (await GetByIdAsync(item.Id, cancellationToken))!;
        }, cancellationToken);

    public Task<AccountSegmentStructureDto> ActivateAsync(Guid id, AccountSegmentLifecycleTransitionDto dto, CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(async () =>
        {
            var item = await LoadAsync(id, cancellationToken);
            ApplyRowVersion(item, dto.RowVersion);
            if (item.LifecycleStatus != AccountSegmentLifecycleStatus.Draft)
                throw new InvalidOperationException("Only a draft account-number segment can be activated.");
            ValidateDefinition(item.SegmentName, item.SegmentCode, item.SegmentPosition, item.SegmentLength, item.DataType);
            await EnsureUniqueAsync(item.Id, item.SegmentCode, item.SegmentPosition, item.IsNaturalAccount, cancellationToken);
            await EnsureActivationReadyAsync(item, cancellationToken);
            var before = Snapshot(item);
            item.LifecycleStatus = AccountSegmentLifecycleStatus.Active; item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = UserName;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureActivated, item, before, Snapshot(item), dto.Reason, cancellationToken);
            return (await GetByIdAsync(item.Id, cancellationToken))!;
        }, cancellationToken);

    public Task<AccountSegmentStructureDto> FreezeAsync(Guid id, AccountSegmentLifecycleTransitionDto dto, CancellationToken cancellationToken = default) =>
        ExecuteAtomicAsync(async () =>
        {
            var item = await LoadAsync(id, cancellationToken);
            ApplyRowVersion(item, dto.RowVersion);
            if (item.LifecycleStatus != AccountSegmentLifecycleStatus.Active)
                throw new InvalidOperationException("Only an active account-number segment can be frozen.");
            await EnsureActiveStructureValidAsync(cancellationToken);
            await EnsureAllAccountsReadyAsync(cancellationToken);
            var before = Snapshot(item); var now = DateTime.UtcNow;
            item.LifecycleStatus = AccountSegmentLifecycleStatus.Frozen; item.IsActive = true;
            item.FrozenAtUtc = now; item.FrozenByUserId = CurrentUserId(); item.UpdatedAt = now; item.UpdatedBy = UserName;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureFrozen, item, before, Snapshot(item), dto.Reason, cancellationToken);
            return (await GetByIdAsync(item.Id, cancellationToken))!;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => ExecuteAtomicAsync(async () =>
    {
        var item = await LoadAsync(id, cancellationToken);
        if (item.LifecycleStatus != AccountSegmentLifecycleStatus.Draft || await UsageCountAsync(id, cancellationToken) > 0)
            throw new InvalidOperationException("Only an unused draft account-number segment can be deleted.");
        var before = Snapshot(item); item.IsDeleted = true; item.IsActive = false; item.DeletedAt = DateTime.UtcNow; item.DeletedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureDeleted, item, before, Snapshot(item), null, cancellationToken);
        return true;
    }, cancellationToken);

    public Task ReorderSegmentsAsync(List<ReorderSegmentDto> reorderList, CancellationToken cancellationToken = default) => ExecuteAtomicAsync(async () =>
    {
        var items = await _db.AccountSegmentStructures.Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        if (items.Count == 0) throw new InvalidOperationException("No account-number segments are configured.");
        if (items.Any(item => item.LifecycleStatus != AccountSegmentLifecycleStatus.Draft) ||
            await _db.AccountSegmentValues.AnyAsync(value => value.TenantId == TenantId && !value.IsDeleted, cancellationToken))
            throw new InvalidOperationException("Account-number segments may only be reordered while every segment is draft and no account identity exists.");
        if (reorderList.Count != items.Count || reorderList.Select(item => item.SegmentId).Distinct().Count() != items.Count ||
            !reorderList.Select(item => item.NewPosition).OrderBy(value => value).SequenceEqual(Enumerable.Range(1, items.Count)))
            throw new InvalidOperationException("Reorder must contain every segment exactly once with sequential positions.");
        var before = items.OrderBy(item => item.SegmentPosition).Select(Snapshot).ToArray();
        foreach (var change in reorderList) items.Single(item => item.Id == change.SegmentId).SegmentPosition = change.NewPosition;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureReordered, items[0], before,
            items.OrderBy(item => item.SegmentPosition).Select(Snapshot).ToArray(), null, cancellationToken);
        return true;
    }, cancellationToken);

    public async Task RegenerateAccountNumbersAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.AccountSegmentValues.AnyAsync(value => value.TenantId == TenantId && !value.IsDeleted, cancellationToken))
            throw new InvalidOperationException("Bulk identity regeneration is prohibited after account identities exist; use a governed remediation workflow.");
    }

    private IQueryable<AccountSegmentStructure> Query() => _db.AccountSegmentStructures.AsNoTracking()
        .AsSplitQuery().Include(item => item.LookupValues.Where(value => !value.IsDeleted))
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);

    private async Task<IReadOnlyList<AccountSegmentStructureDto>> MapAsync(IEnumerable<AccountSegmentStructure> items, CancellationToken ct)
    {
        var materialized = items.ToList(); var ids = materialized.Select(item => item.Id).ToArray();
        var counts = await _db.AccountSegmentValues.AsNoTracking().Where(value => ids.Contains(value.SegmentStructureId) && !value.IsDeleted)
            .GroupBy(value => value.SegmentStructureId).Select(group => new { Id = group.Key, Count = group.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return materialized.Select(item => Map(item, counts.GetValueOrDefault(item.Id))).ToList();
    }

    private static AccountSegmentStructureDto Map(AccountSegmentStructure item, int usage) => new()
    {
        Id = item.Id, TenantId = item.TenantId, SegmentName = item.SegmentName, SegmentCode = item.SegmentCode,
        SegmentPosition = item.SegmentPosition, SegmentLength = item.SegmentLength, DataType = item.DataType,
        SeparatorCharacter = item.SeparatorCharacter, LookupTableRequired = item.LookupTableRequired,
        IsReportingDimension = false, IsNaturalAccount = item.IsNaturalAccount, IsActive = item.IsActive,
        LifecycleStatus = item.LifecycleStatus.ToString(), IsSystemDefined = item.IsSystemDefined,
        RowVersion = Convert.ToBase64String(item.RowVersion ?? Array.Empty<byte>()), Description = item.Description,
        LookupValuesCount = item.LookupValues.Count(value => !value.IsDeleted), AccountUsageCount = usage,
        CanBeModified = item.LifecycleStatus == AccountSegmentLifecycleStatus.Draft && usage == 0,
        CanActivate = item.LifecycleStatus == AccountSegmentLifecycleStatus.Draft,
        CanFreeze = item.LifecycleStatus == AccountSegmentLifecycleStatus.Active,
        RestrictionWarning = usage > 0 ? "This segment forms part of an existing account identity." : null,
        LookupValues = item.LookupValues.Where(value => !value.IsDeleted).OrderBy(value => value.DisplayOrder).Select(value => new SegmentLookupValueSummaryDto
        { Id = value.Id, SegmentValue = value.SegmentValue, Description = value.Description, IsActive = value.IsActive, DisplayOrder = value.DisplayOrder }).ToList(),
        CreatedBy = item.CreatedBy ?? "system", CreatedAt = item.CreatedAt, UpdatedBy = item.UpdatedBy, UpdatedAt = item.UpdatedAt
    };

    private async Task<AccountSegmentStructure> LoadAsync(Guid id, CancellationToken ct) =>
        await _db.AccountSegmentStructures.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, ct)
        ?? throw new KeyNotFoundException("Account-number segment was not found.");

    private async Task EnsureUniqueAsync(Guid? id, string code, int position, bool natural, CancellationToken ct)
    {
        if (await _db.AccountSegmentStructures.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Id != id && item.SegmentCode == code, ct))
            throw new InvalidOperationException($"Account-number segment code '{code}' already exists.");
        if (await _db.AccountSegmentStructures.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Id != id
            && item.LifecycleStatus != AccountSegmentLifecycleStatus.Retired && item.SegmentPosition == position, ct))
            throw new InvalidOperationException($"Account-number segment position {position} already exists.");
        if (natural && await _db.AccountSegmentStructures.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Id != id
            && item.LifecycleStatus != AccountSegmentLifecycleStatus.Retired && item.IsNaturalAccount, ct))
            throw new InvalidOperationException("Only one Natural Account segment is allowed.");
    }

    private async Task EnsureAllAccountsReadyAsync(CancellationToken ct)
    {
        var activeIds = await _db.AccountSegmentStructures.AsNoTracking().Where(item => item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .Select(item => item.Id).ToArrayAsync(ct);
        var accounts = await _db.Accounts.AsNoTracking().Include(item => item.SegmentValues.Where(value => !value.IsDeleted))
            .Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(ct);
        if (accounts.Any(account => !account.SegmentValues.Select(value => value.SegmentStructureId).OrderBy(id => id).SequenceEqual(activeIds.OrderBy(id => id))))
            throw new InvalidOperationException("The structure cannot be frozen while existing accounts have incomplete or extra identity segments.");
    }

    private async Task EnsureActiveStructureValidAsync(CancellationToken ct)
    {
        var definitions = await _db.AccountSegmentStructures.AsNoTracking().Where(item =>
                item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.SegmentPosition).ToListAsync(ct);
        if (definitions.Count == 0 || definitions.Count(item => item.IsNaturalAccount) != 1)
            throw new InvalidOperationException("The active account-number structure must contain exactly one Natural Account segment before freezing.");
        if (!definitions.Select(item => item.SegmentPosition).SequenceEqual(Enumerable.Range(1, definitions.Count)))
            throw new InvalidOperationException("Active account-number segment positions must be contiguous from 1 before freezing.");
        foreach (var definition in definitions)
            ValidateDefinition(definition.SegmentName, definition.SegmentCode, definition.SegmentPosition,
                definition.SegmentLength, definition.DataType);
    }

    private async Task EnsureActivationReadyAsync(AccountSegmentStructure candidate, CancellationToken ct)
    {
        if (candidate.LookupTableRequired)
        {
            var values = await _db.SegmentLookupValues.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.SegmentStructureId == candidate.Id
                && value.IsActive && !value.IsDeleted).Select(value => value.SegmentValue).ToListAsync(ct);
            if (values.Count == 0)
                throw new InvalidOperationException("A lookup-backed account-number segment requires at least one active value before activation.");
            foreach (var value in values)
            {
                if (value.Length != candidate.SegmentLength || !MatchesDataType(candidate.DataType, value))
                    throw new InvalidOperationException($"Lookup value '{value}' does not satisfy the segment length or data type.");
            }
        }

        var futureIds = await _db.AccountSegmentStructures.AsNoTracking().Where(item =>
                item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .Select(item => item.Id).ToListAsync(ct);
        futureIds.Add(candidate.Id);
        var futureSet = futureIds.ToHashSet();
        var accountSets = await _db.Accounts.AsNoTracking().Where(account => account.TenantId == TenantId && !account.IsDeleted)
            .Select(account => account.SegmentValues.Where(value => !value.IsDeleted).Select(value => value.SegmentStructureId).ToList())
            .ToListAsync(ct);
        if (accountSets.Any(values => values.Count != futureSet.Count || !values.All(futureSet.Contains)))
            throw new InvalidOperationException(
                "The segment cannot be activated until every existing GL account has exactly the resulting active identity set.");
    }

    private async Task<int> UsageCountAsync(Guid id, CancellationToken ct) =>
        await _db.AccountSegmentValues.CountAsync(item => item.TenantId == TenantId && item.SegmentStructureId == id && !item.IsDeleted, ct);

    private async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return await action();
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try { var result = await action(); await transaction.CommitAsync(ct); return result; }
            catch { await transaction.RollbackAsync(ct); _db.ChangeTracker.Clear(); throw; }
        });
    }

    private void ApplyRowVersion(AccountSegmentStructure item, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Row version is required.");
        try { _db.Entry(item).Property(value => value.RowVersion).OriginalValue = Convert.FromBase64String(value); }
        catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
    }

    private async Task RecordAuditAsync(string eventType, AccountSegmentStructure item, object? before, object after, string? reason, CancellationToken ct) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            TenantId = TenantId, EventType = eventType, SourceModule = "GL", SourceDocumentType = "AccountSegmentStructure",
            SourceDocumentId = item.Id, Resource = "Finance.AccountSegmentStructure", ResourceId = item.Id.ToString(),
            BeforeValues = before, AfterValues = after, Reason = reason
        }, ct);

    private static object Snapshot(AccountSegmentStructure item) => new
    {
        item.SegmentCode, item.SegmentName, item.SegmentPosition, item.SegmentLength, item.DataType,
        item.SeparatorCharacter, item.LookupTableRequired, item.IsNaturalAccount, LifecycleStatus = item.LifecycleStatus.ToString()
    };

    private Guid? CurrentUserId() => Guid.TryParse(_currentUser.UserId, out var id) ? id : null;
    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidateDefinition(string name, string code, int position, int length, string dataType)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Segment name and stable code are required.");
        if (position < 1 || length < 1 || length > 50) throw new InvalidOperationException("Segment position and length are invalid.");
        if (dataType is not ("Numeric" or "Alphanumeric" or "Alpha")) throw new InvalidOperationException("Segment data type must be Numeric, Alpha, or Alphanumeric.");
    }

    private static bool MatchesDataType(string dataType, string value) => dataType switch
    {
        "Numeric" => value.All(char.IsDigit),
        "Alpha" => value.All(char.IsLetter),
        "Alphanumeric" => value.All(char.IsLetterOrDigit),
        _ => false
    };
}
