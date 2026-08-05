using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Security;

/// <summary>
/// Persists and evaluates tenant-specific Finance data scopes.
///
/// This service intentionally complements, rather than replaces, ASP.NET permission policies.
/// Controllers first prove that the actor may perform an action; this service then constrains the
/// data on which that action can operate. SuperAdmin and TenantAdmin retain tenant-wide recovery
/// access so a bad scope configuration cannot permanently lock administrators out of Finance.
/// </summary>
public sealed class FinanceAccessScopeService : IFinanceAccessScopeService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly ILogger<FinanceAccessScopeService> _logger;

    public FinanceAccessScopeService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<FinanceAccessScopeService> logger,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var userId)
        ? userId
        : throw new UnauthorizedAccessException("An authenticated Finance user is required.");

    public async Task<IReadOnlyList<FinanceAccessScopeGrantDto>> GetGrantsAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();

        var grants = await _context.Set<FinanceAccessScopeGrant>()
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item =>
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                (!userId.HasValue || item.UserId == userId.Value))
            .OrderBy(item => item.User.FirstName)
            .ThenBy(item => item.User.LastName)
            .ThenBy(item => item.ScopeType)
            .ThenBy(item => item.ScopeValue)
            .ToListAsync(cancellationToken);

        var bankIds = grants
            .Where(item => item.ScopeType == FinanceAccessScopeType.BankAccount)
            .Select(item => ParseGuidScopeValue(item.ScopeValue))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var banks = await _context.BankAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && bankIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return grants.Select(item => MapGrant(item, banks)).ToList();
    }

    public async Task<IReadOnlyList<FinanceAccessUserOptionDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();

        // Some development tenants still use ApplicationUser.TenantId without an explicit
        // UserTenant row. Supporting both active relationships avoids hiding legitimate users
        // while the access-scope feature is being configured; enforcement still uses the current
        // authenticated tenant and never crosses tenant boundaries.
        return await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.IsActive &&
                (user.TenantId == TenantId || user.UserTenants.Any(link =>
                    link.TenantId == TenantId &&
                    !link.IsDeleted &&
                    link.Status == UserTenantStatus.Active &&
                    (!link.ExpiresAt.HasValue || link.ExpiresAt > DateTime.UtcNow))))
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Select(user => new FinanceAccessUserOptionDto
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                DisplayName = (user.FirstName + " " + user.LastName).Trim()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceAccessBankAccountOptionDto>> GetBankAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await _context.BankAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .OrderBy(item => item.BankName)
            .ThenBy(item => item.AccountName)
            .Select(item => new FinanceAccessBankAccountOptionDto
            {
                BankAccountId = item.Id,
                AccountNumber = item.AccountNumber,
                AccountName = item.AccountName,
                BankName = item.BankName,
                Currency = item.Currency,
                IsActive = item.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<FinanceAccessScopeGrantDto> SaveGrantAsync(
        Guid? id,
        SaveFinanceAccessScopeGrantDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureAuthenticatedTenant();
        ValidateGrant(dto);

        var userExists = await _context.Users.AnyAsync(user =>
            user.Id == dto.UserId &&
            user.IsActive &&
            (user.TenantId == TenantId || user.UserTenants.Any(link =>
                link.TenantId == TenantId &&
                !link.IsDeleted &&
                link.Status == UserTenantStatus.Active &&
                (!link.ExpiresAt.HasValue || link.ExpiresAt > DateTime.UtcNow))), cancellationToken);
        if (!userExists)
            throw new KeyNotFoundException("The selected user is not active in the current tenant.");

        var normalizedValue = await NormalizeAndValidateScopeValueAsync(
            dto.ScopeType,
            dto.ScopeValue,
            cancellationToken);

        var duplicateExists = await _context.Set<FinanceAccessScopeGrant>().AnyAsync(item =>
            item.TenantId == TenantId &&
            !item.IsDeleted &&
            item.IsActive &&
            item.UserId == dto.UserId &&
            item.ScopeType == dto.ScopeType &&
            item.ScopeValue == normalizedValue &&
            (!id.HasValue || item.Id != id.Value), cancellationToken);
        if (duplicateExists)
            throw new InvalidOperationException("An active Finance scope grant already exists for this user and scope.");

        FinanceAccessScopeGrant grant;
        object? beforeValues = null;
        if (id.HasValue)
        {
            grant = await _context.Set<FinanceAccessScopeGrant>()
                .Include(item => item.User)
                .SingleOrDefaultAsync(item =>
                    item.TenantId == TenantId && item.Id == id.Value && !item.IsDeleted,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Finance access-scope grant was not found.");

            ApplyOriginalRowVersion(grant, dto.RowVersion);
            beforeValues = AuditShape(grant);
        }
        else
        {
            grant = new FinanceAccessScopeGrant
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName,
                CreatedById = CurrentUserId
            };
            _context.Set<FinanceAccessScopeGrant>().Add(grant);
        }

        grant.UserId = dto.UserId;
        grant.ScopeType = dto.ScopeType;
        grant.ScopeValue = normalizedValue;
        grant.AccessLevel = dto.AccessLevel;
        grant.EffectiveFrom = EnsureUtc(dto.EffectiveFrom);
        grant.EffectiveTo = dto.EffectiveTo.HasValue ? EnsureUtc(dto.EffectiveTo.Value) : null;
        grant.IsActive = dto.IsActive;
        grant.Reason = dto.Reason.Trim();
        grant.UpdatedAt = DateTime.UtcNow;
        grant.UpdatedBy = _currentUser.UserName;
        grant.LastModifiedById = CurrentUserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(grant).Reference(item => item.User).LoadAsync(cancellationToken);

        await RecordAuditAsync(
            id.HasValue ? FinanceAuditEvents.FinanceAccessScopeUpdated : FinanceAuditEvents.FinanceAccessScopeGranted,
            grant,
            beforeValues,
            AuditShape(grant),
            grant.Reason,
            cancellationToken);

        var banks = await LoadBankDictionaryAsync(grant, cancellationToken);
        return MapGrant(grant, banks);
    }

    public async Task DeactivateGrantAsync(
        Guid id,
        string reason,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw new ArgumentException("A deactivation reason of at least 10 characters is required.", nameof(reason));

        var grant = await _context.Set<FinanceAccessScopeGrant>()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Finance access-scope grant was not found.");
        ApplyOriginalRowVersion(grant, rowVersion);

        var beforeValues = AuditShape(grant);
        grant.IsActive = false;
        grant.EffectiveTo = DateTime.UtcNow;
        grant.Reason = reason.Trim();
        grant.UpdatedAt = DateTime.UtcNow;
        grant.UpdatedBy = _currentUser.UserName;
        grant.LastModifiedById = CurrentUserId;
        await _context.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.FinanceAccessScopeDeactivated,
            grant,
            beforeValues,
            AuditShape(grant),
            reason,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>?> GetPermittedBankAccountIdsAsync(
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        ValidateAccessLevel(requiredLevel);

        if (IsRecoveryAdministrator() || !await IsScopeEnforcementEnabledAsync(cancellationToken))
            return null;

        var now = DateTime.UtcNow;
        var grants = await _context.Set<FinanceAccessScopeGrant>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                item.UserId == CurrentUserId &&
                !item.IsDeleted &&
                item.IsActive &&
                item.AccessLevel >= requiredLevel &&
                item.EffectiveFrom <= now &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo >= now) &&
                (item.ScopeType == FinanceAccessScopeType.Tenant ||
                 item.ScopeType == FinanceAccessScopeType.BankAccount))
            .Select(item => new { item.ScopeType, item.ScopeValue })
            .ToListAsync(cancellationToken);

        if (grants.Any(item => item.ScopeType == FinanceAccessScopeType.Tenant))
            return null;

        return grants
            .Where(item => item.ScopeType == FinanceAccessScopeType.BankAccount)
            .Select(item => ParseGuidScopeValue(item.ScopeValue))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
    }

    public async Task EnsureBankAccountAccessAsync(
        Guid? bankAccountId,
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken = default)
    {
        var permittedIds = await GetPermittedBankAccountIdsAsync(requiredLevel, cancellationToken);
        if (permittedIds == null)
            return;

        if (bankAccountId.HasValue && permittedIds.Contains(bankAccountId.Value))
            return;

        await RecordDeniedAuditAsync(bankAccountId, requiredLevel, cancellationToken);
        throw new UnauthorizedAccessException(
            "The current user does not have the required Finance scope for this bank account.");
    }

    private async Task<string?> NormalizeAndValidateScopeValueAsync(
        FinanceAccessScopeType scopeType,
        string? scopeValue,
        CancellationToken cancellationToken)
    {
        if (scopeType == FinanceAccessScopeType.Tenant)
        {
            if (!string.IsNullOrWhiteSpace(scopeValue))
                throw new InvalidOperationException("A tenant-wide Finance scope must not specify a scope value.");
            return null;
        }

        if (scopeType != FinanceAccessScopeType.BankAccount)
        {
            // The enum reserves stable values for the next Finance work packages, but accepting
            // unvalidated grants now would create a misleading sense of enforcement.
            throw new InvalidOperationException(
                $"Finance scope type '{scopeType}' is reserved but is not yet enforceable in this delivery slice.");
        }

        if (!Guid.TryParse(scopeValue, out var bankAccountId) || bankAccountId == Guid.Empty)
            throw new InvalidOperationException("A valid bank-account ID is required for a bank-account Finance scope.");

        var exists = await _context.BankAccounts.AsNoTracking().AnyAsync(item =>
            item.TenantId == TenantId && item.Id == bankAccountId && !item.IsDeleted && item.IsActive,
            cancellationToken);
        if (!exists)
            throw new KeyNotFoundException("The selected bank account is not active in the current tenant.");

        return bankAccountId.ToString("N");
    }

    private async Task<bool> IsScopeEnforcementEnabledAsync(CancellationToken cancellationToken) =>
        await _context.FinanceSettings
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .Select(item => item.EnforceFinanceAccessScopes)
            .FirstOrDefaultAsync(cancellationToken);

    private bool IsRecoveryAdministrator() => _currentUser.Roles.Any(role =>
        string.Equals(role, Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, Constants.Roles.TenantAdmin, StringComparison.OrdinalIgnoreCase));

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || CurrentUserId == Guid.Empty || TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated tenant context is required for Finance access scopes.");
    }

    private static void ValidateGrant(SaveFinanceAccessScopeGrantDto dto)
    {
        if (dto.UserId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(dto));
        ValidateAccessLevel(dto.AccessLevel);
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length < 10)
            throw new ArgumentException("A grant reason of at least 10 characters is required.", nameof(dto));
        if (dto.EffectiveTo.HasValue && EnsureUtc(dto.EffectiveTo.Value) < EnsureUtc(dto.EffectiveFrom))
            throw new ArgumentException("EffectiveTo cannot precede EffectiveFrom.", nameof(dto));
    }

    private static void ValidateAccessLevel(FinanceAccessLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level), "A valid Finance access level is required.");
    }

    private void ApplyOriginalRowVersion(FinanceAccessScopeGrant grant, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw new InvalidOperationException("RowVersion is required when changing an existing Finance scope grant.");

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(supplied);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("RowVersion is not valid base64.", ex);
        }

        _context.Entry(grant).Property(item => item.RowVersion).OriginalValue = expected;
    }

    private async Task<IReadOnlyDictionary<Guid, BankAccount>> LoadBankDictionaryAsync(
        FinanceAccessScopeGrant grant,
        CancellationToken cancellationToken)
    {
        var bankId = grant.ScopeType == FinanceAccessScopeType.BankAccount
            ? ParseGuidScopeValue(grant.ScopeValue)
            : null;
        if (!bankId.HasValue)
            return new Dictionary<Guid, BankAccount>();

        return await _context.BankAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.Id == bankId.Value)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private static FinanceAccessScopeGrantDto MapGrant(
        FinanceAccessScopeGrant grant,
        IReadOnlyDictionary<Guid, BankAccount> banks)
    {
        var bankId = grant.ScopeType == FinanceAccessScopeType.BankAccount
            ? ParseGuidScopeValue(grant.ScopeValue)
            : null;
        var displayName = grant.ScopeType == FinanceAccessScopeType.Tenant
            ? "All Finance data in tenant"
            : bankId.HasValue && banks.TryGetValue(bankId.Value, out var bank)
                ? $"{bank.BankName} — {bank.AccountName} ({bank.AccountNumber})"
                : grant.ScopeValue;

        return new FinanceAccessScopeGrantDto
        {
            Id = grant.Id,
            UserId = grant.UserId,
            Username = grant.User?.UserName ?? string.Empty,
            UserDisplayName = grant.User == null
                ? string.Empty
                : (grant.User.FirstName + " " + grant.User.LastName).Trim(),
            ScopeType = grant.ScopeType,
            // Persist GUID dimensions in canonical N form for stable comparisons, but expose the
            // conventional dashed representation expected by HTML controls and API consumers.
            ScopeValue = bankId?.ToString() ?? grant.ScopeValue,
            ScopeDisplayName = displayName,
            AccessLevel = grant.AccessLevel,
            EffectiveFrom = grant.EffectiveFrom,
            EffectiveTo = grant.EffectiveTo,
            IsActive = grant.IsActive,
            Reason = grant.Reason,
            RowVersion = Convert.ToBase64String(grant.RowVersion ?? Array.Empty<byte>())
        };
    }

    private async Task RecordAuditAsync(
        string eventType,
        FinanceAccessScopeGrant grant,
        object? beforeValues,
        object? afterValues,
        string reason,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = grant.TenantId,
            SourceModule = "Finance",
            SourceDocumentType = nameof(FinanceAccessScopeGrant),
            SourceDocumentId = grant.Id,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Resource = "Finance.AccessScope",
            ResourceId = grant.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordDeniedAuditAsync(
        Guid? bankAccountId,
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Finance scope denied user {UserId} tenant {TenantId} bank account {BankAccountId} at level {RequiredLevel}.",
            CurrentUserId,
            TenantId,
            bankAccountId,
            requiredLevel);

        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.FinanceAccessScopeDenied,
            TenantId = TenantId,
            SourceModule = "Finance",
            SourceDocumentType = "BankAccount",
            SourceDocumentId = bankAccountId,
            AfterValues = new { BankAccountId = bankAccountId, RequiredLevel = requiredLevel.ToString() },
            Reason = "No effective Finance scope grant satisfied the requested bank-account access.",
            Resource = "Finance.AccessDecision",
            ResourceId = bankAccountId?.ToString() ?? CurrentUserId.ToString()
        }, cancellationToken);
    }

    private static object AuditShape(FinanceAccessScopeGrant grant) => new
    {
        grant.UserId,
        grant.ScopeType,
        grant.ScopeValue,
        grant.AccessLevel,
        grant.EffectiveFrom,
        grant.EffectiveTo,
        grant.IsActive,
        grant.Reason
    };

    private static Guid? ParseGuidScopeValue(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
