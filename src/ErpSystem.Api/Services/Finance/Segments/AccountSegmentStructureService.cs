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
    private readonly IAccountSegmentIdentityService _identity;
    private readonly ILogger<AccountSegmentStructureService> _logger;

    public AccountSegmentStructureService(ApplicationDbContext db, ICurrentUserService currentUser,
        IFinanceAuditService audit, IAccountSegmentIdentityService identity,
        ILogger<AccountSegmentStructureService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _identity = identity;
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
            var activation = await PrepareActivationAsync(item, dto, cancellationToken);
            var before = Snapshot(item);
            item.LifecycleStatus = AccountSegmentLifecycleStatus.Active; item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = UserName;
            await _db.SaveChangesAsync(cancellationToken);
            await BackfillActivatedSegmentAsync(item, activation, cancellationToken);
            await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureActivated, item, before, new
            {
                Segment = Snapshot(item),
                ExistingAccountsBackfilled = activation.Accounts.Count,
                DefaultSegmentValue = activation.DefaultValue
            }, dto.Reason, cancellationToken);
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

    public Task DeleteAsync(Guid id, AccountSegmentDeleteDto dto, CancellationToken cancellationToken = default) => ExecuteAtomicAsync(async () =>
    {
        var item = await LoadAsync(id, cancellationToken);
        ApplyRowVersion(item, dto.RowVersion);
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
        foreach (var change in reorderList)
            ApplyRowVersion(items.Single(item => item.Id == change.SegmentId), change.RowVersion);
        var before = items.OrderBy(item => item.SegmentPosition).Select(Snapshot).ToArray();
        foreach (var change in reorderList) items.Single(item => item.Id == change.SegmentId).SegmentPosition = change.NewPosition;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.AccountSegmentStructureReordered, items[0], before,
            items.OrderBy(item => item.SegmentPosition).Select(Snapshot).ToArray(), null, cancellationToken);
        return true;
    }, cancellationToken);

    private IQueryable<AccountSegmentStructure> Query() => _db.AccountSegmentStructures.AsNoTracking()
        .AsSplitQuery().Include(item => item.LookupValues.Where(value => !value.IsDeleted))
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);

    private async Task<IReadOnlyList<AccountSegmentStructureDto>> MapAsync(IEnumerable<AccountSegmentStructure> items, CancellationToken ct)
    {
        var materialized = items.ToList(); var ids = materialized.Select(item => item.Id).ToArray();
        var counts = await _db.AccountSegmentValues.AsNoTracking().Where(value => value.TenantId == TenantId
                && ids.Contains(value.SegmentStructureId) && !value.IsDeleted)
            .GroupBy(value => value.SegmentStructureId)
            .Select(group => new { Id = group.Key, Count = group.Select(value => value.AccountId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var totalAccounts = await _db.Accounts.AsNoTracking()
            .CountAsync(account => account.TenantId == TenantId && !account.IsDeleted, ct);
        return materialized.Select(item => Map(item, counts.GetValueOrDefault(item.Id), totalAccounts)).ToList();
    }

    private static AccountSegmentStructureDto Map(AccountSegmentStructure item, int usage, int totalAccounts) => new()
    {
        Id = item.Id, TenantId = item.TenantId, SegmentName = item.SegmentName, SegmentCode = item.SegmentCode,
        SegmentPosition = item.SegmentPosition, SegmentLength = item.SegmentLength, DataType = item.DataType,
        SeparatorCharacter = item.SeparatorCharacter, LookupTableRequired = item.LookupTableRequired,
        IsReportingDimension = false, IsNaturalAccount = item.IsNaturalAccount, IsActive = item.IsActive,
        LifecycleStatus = item.LifecycleStatus.ToString(), IsSystemDefined = item.IsSystemDefined,
        RowVersion = Convert.ToBase64String(item.RowVersion ?? Array.Empty<byte>()), Description = item.Description,
        LookupValuesCount = item.LookupValues.Count(value => !value.IsDeleted), AccountUsageCount = usage,
        TotalAccountCount = totalAccounts,
        CanBeModified = item.LifecycleStatus == AccountSegmentLifecycleStatus.Draft && usage == 0,
        CanActivate = item.LifecycleStatus == AccountSegmentLifecycleStatus.Draft,
        CanFreeze = item.LifecycleStatus == AccountSegmentLifecycleStatus.Active,
        RestrictionWarning = usage < totalAccounts
            ? $"Structured identity coverage is incomplete: {usage} of {totalAccounts} GL accounts are assigned."
            : usage > 0 ? "This segment forms part of every existing account identity." : null,
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
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .OrderBy(item => item.AccountNumber)
            .Select(item => new { item.Id, item.AccountNumber })
            .ToListAsync(ct);
        var affected = new List<string>();
        foreach (var account in accounts)
        {
            var readiness = await _identity.GetReadinessAsync(TenantId, account.Id, ct);
            if (!readiness.IsReady)
                affected.Add($"{account.AccountNumber} ({account.Id}): {string.Join("; ", readiness.Issues)}");
        }
        if (affected.Count > 0)
            throw new InvalidOperationException(
                $"The structure cannot be frozen because {affected.Count} account identity record(s) require reconciliation: {string.Join(" | ", affected.Take(20))}");
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

    private async Task<ActivationPreparation> PrepareActivationAsync(
        AccountSegmentStructure candidate,
        AccountSegmentLifecycleTransitionDto dto,
        CancellationToken ct)
    {
        SegmentLookupValue? defaultLookup = null;
        if (candidate.LookupTableRequired)
        {
            var values = await _db.SegmentLookupValues.Where(value =>
                value.TenantId == TenantId && value.SegmentStructureId == candidate.Id
                && value.IsActive && !value.IsDeleted).ToListAsync(ct);
            if (values.Count == 0)
                throw new InvalidOperationException("A lookup-backed account-number segment requires at least one active value before activation.");
            foreach (var value in values)
            {
                if (value.SegmentValue.Length != candidate.SegmentLength || !MatchesDataType(candidate.DataType, value.SegmentValue))
                    throw new InvalidOperationException($"Lookup value '{value.SegmentValue}' does not satisfy the segment length or data type.");
            }

            if (dto.DefaultSegmentLookupValueId.HasValue)
                defaultLookup = values.SingleOrDefault(value => value.Id == dto.DefaultSegmentLookupValueId.Value)
                    ?? throw new InvalidOperationException("The selected activation default is not an active lookup value for this segment.");
        }

        var activeDefinitions = await _db.AccountSegmentStructures.AsNoTracking().Where(item =>
                item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.SegmentPosition).ToListAsync(ct);
        var futurePositions = activeDefinitions.Select(item => item.SegmentPosition).Append(candidate.SegmentPosition).OrderBy(value => value).ToList();
        if (!futurePositions.SequenceEqual(Enumerable.Range(1, futurePositions.Count)))
            throw new InvalidOperationException("Activating this segment would leave a gap in the mandatory account-number structure positions.");

        var accounts = await _db.Accounts
            .Include(account => account.SegmentValues.Where(value => !value.IsDeleted))
            .Where(account => account.TenantId == TenantId && !account.IsDeleted)
            .OrderBy(account => account.AccountNumber)
            .ToListAsync(ct);
        if (accounts.Count == 0)
            return new ActivationPreparation(accounts, null, null);

        if (!dto.ConfirmExistingAccountBackfill)
            throw new InvalidOperationException(
                $"Activation makes this segment mandatory for {accounts.Count} existing GL accounts. Confirm the governed backfill and supply one default value.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reason is required when activation changes existing GL account identities.");
        var defaultValue = (dto.DefaultSegmentValue ?? defaultLookup?.SegmentValue ?? string.Empty).Trim().ToUpperInvariant();
        if (defaultValue.Length != candidate.SegmentLength || !MatchesDataType(candidate.DataType, defaultValue))
            throw new InvalidOperationException($"The activation default must contain exactly {candidate.SegmentLength} valid {candidate.DataType} characters.");
        if (candidate.LookupTableRequired)
        {
            defaultLookup ??= await _db.SegmentLookupValues.SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.SegmentStructureId == candidate.Id && value.IsActive && !value.IsDeleted
                && value.SegmentValue == defaultValue, ct);
            if (defaultLookup == null || !string.Equals(defaultLookup.SegmentValue, defaultValue, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The activation default must be an active configured lookup value for this segment.");
        }
        else if (dto.DefaultSegmentLookupValueId.HasValue)
            throw new InvalidOperationException("A lookup value ID cannot be supplied for a free-form segment.");

        if (await _db.AccountTransactions.AsNoTracking().AnyAsync(transaction =>
                transaction.TenantId == TenantId && !transaction.IsDeleted, ct))
            throw new InvalidOperationException(
                "Account-number structure activation is blocked after accounting transactions exist. Use a separately approved historical identity migration.");

        var activeIds = activeDefinitions.Select(definition => definition.Id).ToHashSet();
        var incomplete = accounts.Where(account =>
        {
            var assigned = account.SegmentValues.Select(value => value.SegmentStructureId).ToList();
            return assigned.Count != activeIds.Count || assigned.Distinct().Count() != activeIds.Count || assigned.Any(id => !activeIds.Contains(id));
        }).Select(account => $"{account.AccountNumber} ({account.Id})").Take(20).ToList();
        if (incomplete.Count > 0)
            throw new InvalidOperationException(
                $"Activation is blocked because existing GL accounts do not have the current active identity set: {string.Join(", ", incomplete)}. Reconcile them before extending the structure.");

        return new ActivationPreparation(accounts, defaultValue, defaultLookup);
    }

    private async Task BackfillActivatedSegmentAsync(
        AccountSegmentStructure candidate,
        ActivationPreparation preparation,
        CancellationToken ct)
    {
        if (preparation.Accounts.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var account in preparation.Accounts)
        {
            var submitted = account.SegmentValues.Select(value => new AccountSegmentValueCreateDto
            {
                AccountId = account.Id,
                SegmentStructureId = value.SegmentStructureId,
                SegmentPosition = value.SegmentPosition,
                SegmentValue = value.SegmentValue,
                SegmentLookupValueId = value.SegmentLookupValueId,
                IsLocked = value.IsLocked,
                EffectiveDate = value.EffectiveDate,
                EndDate = value.EndDate
            }).ToList();
            submitted.Add(new AccountSegmentValueCreateDto
            {
                AccountId = account.Id,
                SegmentStructureId = candidate.Id,
                SegmentPosition = candidate.SegmentPosition,
                SegmentValue = preparation.DefaultValue!,
                SegmentLookupValueId = preparation.DefaultLookup?.Id,
                EffectiveDate = now
            });
            var identity = await _identity.ValidateAndComposeAsync(TenantId, submitted,
                existingAccountId: account.Id, cancellationToken: ct);
            var normalized = identity.Values.Single(value => value.SegmentStructureId == candidate.Id);
            account.AccountNumber = identity.AccountNumber;
            account.UpdatedAt = now;
            account.UpdatedBy = UserName;
            _db.AccountSegmentValues.Add(new AccountSegmentValue
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountId = account.Id,
                SegmentStructureId = candidate.Id,
                SegmentValue = normalized.SegmentValue,
                SegmentLookupValueId = normalized.SegmentLookupValueId,
                SegmentValueDescription = preparation.DefaultLookup?.Description,
                SegmentPosition = candidate.SegmentPosition,
                EffectiveDate = now,
                CreatedAt = now,
                CreatedBy = UserName
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private sealed record ActivationPreparation(
        IReadOnlyList<Account> Accounts,
        string? DefaultValue,
        SegmentLookupValue? DefaultLookup);

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
