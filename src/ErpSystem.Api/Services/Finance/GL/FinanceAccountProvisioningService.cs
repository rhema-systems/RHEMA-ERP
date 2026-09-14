using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinanceAccountProvisioningService : IFinanceAccountProvisioningService
{
    private const string LegacyProcurementSeederActor = "Development supplier-onboarding seeder";
    private static readonly HashSet<string> LegacyProcurementAccountCodes =
        new(StringComparer.OrdinalIgnoreCase) { "1040", "4930", "2210" };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FinanceAccountProvisioningService> _logger;
    private readonly IAccountSegmentIdentityService _segmentIdentity;

    public FinanceAccountProvisioningService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<FinanceAccountProvisioningService> logger,
        IAccountSegmentIdentityService? segmentIdentity = null)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
        _segmentIdentity = segmentIdentity ?? new ErpSystem.Api.Services.Finance.Segments.AccountSegmentIdentityService(db);
    }

    public async Task<ProvisionedFinanceAccountDto> ProvisionAsync(
        ProvisionFinanceAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        if (request.TenantId == Guid.Empty || request.TenantId != tenantId)
            throw new InvalidOperationException("Finance account provisioning tenant context is invalid.");
        var accountCode = NormalizeRequired(request.AccountCode, "Account code", 50).ToUpperInvariant();
        var accountName = NormalizeRequired(request.AccountName, "Account name", 200);
        var currencyCode = NormalizeRequired(request.CurrencyCode, "Currency code", 3).ToUpperInvariant();
        var classificationCode = FinanceClassificationManifestSeeder.ResolveReviewedClassificationCode(
            accountCode, request.CoreAccountType)
            ?? throw new InvalidOperationException(
                "The requested account code and core type are not present in the reviewed Finance classification manifest.");

        var provisionedAt = DateTime.UtcNow;
        var hasCallerTransaction = _db.Database.IsRelational()
            && (_db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null);
        if (!_db.Database.IsRelational() || hasCallerTransaction)
        {
            await AcquireProvisioningLocksAsync(tenantId, accountCode, cancellationToken);
            return await ProvisionCoreAsync(
                request, tenantId, accountCode, accountName, currencyCode, classificationCode,
                provisionedAt, cancellationToken);
        }

        // SQL Server's retrying execution strategy must own the complete transaction unit.
        // A caller-owned transaction takes the branch above, so this boundary never nests or
        // independently commits work that belongs to its caller.
        var strategy = _db.Database.CreateExecutionStrategy();
        var initialTrackedState = CaptureRetryableTrackedState();
        var provisioned = await strategy.ExecuteAsync(async () =>
        {
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                await AcquireProvisioningLocksAsync(tenantId, accountCode, cancellationToken);
                var result = await ProvisionCoreAsync(
                    request, tenantId, accountCode, accountName, currencyCode, classificationCode,
                    provisionedAt, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                // A failed transaction can leave successfully written-but-rolled-back entities
                // as Unchanged in this scoped context. Restore the exact pre-attempt tracker so
                // the execution strategy can repeat the whole idempotent unit without duplicate
                // identity instances, while retaining caller-owned pending state.
                RestoreTrackedState(initialTrackedState);
                throw;
            }
        });
        DetachAttemptTrackedEntities(initialTrackedState);
        return provisioned;
    }

    private IReadOnlyList<TrackedEntrySnapshot> CaptureRetryableTrackedState()
    {
        _db.ChangeTracker.DetectChanges();
        var unsafeEntry = _db.ChangeTracker.Entries().FirstOrDefault(entry =>
            entry.State is EntityState.Added or EntityState.Deleted
            || entry.Properties.Any(property => property.IsTemporary
                || (property.IsModified && property.Metadata.GetContainingForeignKeys().Any()))
            || entry.Navigations.Any(navigation => navigation.IsLoaded || navigation.IsModified
                || HasMaterializedRelationship(navigation.CurrentValue)));
        if (unsafeEntry is not null)
        {
            throw new InvalidOperationException(
                "FINANCE_ACCOUNT_PROVISIONING_TRACKER_NOT_RETRY_SAFE: service-owned retry requires no added, deleted, " +
                "temporary-key, relationship-modified, or loaded-navigation state. Start a caller-owned transaction " +
                "inside its execution strategy when provisioning must share that state.");
        }

        return _db.ChangeTracker.Entries()
            .Select(entry => new TrackedEntrySnapshot(
                entry.Entity,
                entry.State,
                entry.CurrentValues.Clone(),
                entry.OriginalValues.Clone(),
                entry.Properties.Where(property => property.IsModified)
                    .Select(property => property.Metadata.Name)
                    .ToHashSet(StringComparer.Ordinal)))
            .ToList();
    }

    private static bool HasMaterializedRelationship(object? value) => value switch
    {
        null => false,
        System.Collections.IEnumerable collection => collection.Cast<object>().Any(),
        _ => true
    };

    private async Task AcquireProvisioningLocksAsync(
        Guid tenantId,
        string accountCode,
        CancellationToken cancellationToken)
    {
        if (!_db.Database.IsSqlServer())
            return;

        // The tenant lock protects deterministic segment, dimension, classification and book
        // manifests even when different account codes are provisioned concurrently. The second
        // canonical account lock documents and protects the exact read/insert convergence key.
        // Every caller takes them in this fixed order and both are transaction-owned.
        var resources = BuildProvisioningLockResources(tenantId, accountCode);
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @tenantResult int, @accountResult int;
EXEC @tenantResult = sys.sp_getapplock
    @Resource = {resources.Manifest}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
IF @tenantResult < 0
    THROW 51000, 'FINANCE_ACCOUNT_PROVISIONING_MANIFEST_LOCK_FAILED: tenant Finance manifest could not be serialized.', 1;
EXEC @accountResult = sys.sp_getapplock
    @Resource = {resources.Account}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
IF @accountResult < 0
    THROW 51000, 'FINANCE_ACCOUNT_PROVISIONING_ACCOUNT_LOCK_FAILED: canonical account identity could not be serialized.', 1;",
            cancellationToken);
    }

    internal static (string Manifest, string Account) BuildProvisioningLockResources(
        Guid tenantId,
        string accountCode)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant ID is required for Finance provisioning serialization.", nameof(tenantId));
        var canonicalAccountCode = NormalizeRequired(accountCode, "Account code", 50).ToUpperInvariant();
        return (
            $"RHEMA:FIN:PROVISION:MANIFEST:{tenantId:N}",
            $"RHEMA:FIN:PROVISION:ACCOUNT:{tenantId:N}:{canonicalAccountCode}");
    }

    private void RestoreTrackedState(IReadOnlyList<TrackedEntrySnapshot> snapshots)
    {
        DetachAttemptTrackedEntities(snapshots);
        foreach (var snapshot in snapshots)
        {
            var entry = _db.Entry(snapshot.Entity);
            entry.CurrentValues.SetValues(snapshot.CurrentValues);
            entry.OriginalValues.SetValues(snapshot.OriginalValues);
            entry.State = snapshot.State;
            if (snapshot.State == EntityState.Modified)
            {
                foreach (var property in entry.Properties)
                    property.IsModified = snapshot.ModifiedProperties.Contains(property.Metadata.Name);
            }
        }
    }

    private void DetachAttemptTrackedEntities(IReadOnlyList<TrackedEntrySnapshot> snapshots)
    {
        var originalEntities = snapshots.Select(snapshot => snapshot.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(entry => !originalEntities.Contains(entry.Entity)).ToList())
            entry.State = EntityState.Detached;
    }

    private sealed record TrackedEntrySnapshot(
        object Entity,
        EntityState State,
        PropertyValues CurrentValues,
        PropertyValues OriginalValues,
        IReadOnlySet<string> ModifiedProperties);

    private async Task<ProvisionedFinanceAccountDto> ProvisionCoreAsync(
        ProvisionFinanceAccountDto request,
        Guid tenantId,
        string accountCode,
        string accountName,
        string currencyCode,
        string classificationCode,
        DateTime provisionedAt,
        CancellationToken cancellationToken)
    {
        await new FinanceSegmentDimensionManifestSeeder(_db, _logger)
            .SeedAsync(tenantId, provisionedAt, cancellationToken);
        var stableCodeMatches = await _db.Accounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.AccountCode == accountCode)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (stableCodeMatches.Count > 1)
            throw new InvalidOperationException("Finance account provisioning found an ambiguous stable account code.");
        var existingAccountId = stableCodeMatches.Count == 1 ? stableCodeMatches[0] : (Guid?)null;
        var identity = await _segmentIdentity.ResolveProvisioningIdentityAsync(
            tenantId, accountCode, existingAccountId, cancellationToken);
        var accountNumber = identity.AccountNumber;

        var matches = await _db.Accounts.Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.AccountCode == accountCode || item.AccountNumber == accountNumber))
            .ToListAsync(cancellationToken);
        if (matches.Select(item => item.Id).Distinct().Count() > 1)
            throw new InvalidOperationException("Finance account provisioning found an ambiguous stable account code or number.");

        var account = matches.SingleOrDefault();
        var wasCreated = account == null;
        if (account == null)
        {
            account = new Account
            {
                TenantId = tenantId,
                AccountCode = accountCode,
                AccountNumber = accountNumber,
                AccountName = accountName,
                AccountType = request.CoreAccountType,
                CurrencyCode = currencyCode,
                Description = string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description.Trim(),
                IsSegmented = true,
                AllowDirectPosting = true,
                IsSystemAccount = true,
                Status = AccountStatus.Active,
                ReferenceNumber = accountCode,
                EffectiveDate = provisionedAt,
                CreatedAt = provisionedAt,
                CreatedBy = _currentUser.UserName ?? "system"
            };
            foreach (var value in identity.Values)
            {
                account.SegmentValues.Add(new AccountSegmentValue
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                    SegmentStructureId = value.SegmentStructureId, SegmentPosition = value.SegmentPosition,
                    SegmentValue = value.SegmentValue, SegmentLookupValueId = value.SegmentLookupValueId,
                    EffectiveDate = provisionedAt, CreatedAt = provisionedAt,
                    CreatedBy = _currentUser.UserName ?? "system"
                });
            }
            _db.Accounts.Add(account);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (account.AccountType != request.CoreAccountType)
                throw new InvalidOperationException("The existing Finance account has a different core account type.");
            var readiness = await _segmentIdentity.GetReadinessAsync(tenantId, account.Id, cancellationToken);
            if (!readiness.IsReady)
            {
                if (await CanAdoptLegacyProcurementSeederAccountAsync(account, accountCode, cancellationToken))
                {
                    // This narrow bridge adopts only the three identities created by the former
                    // executable Procurement seeder. Finance composes their canonical identity
                    // while preserving the account ID and all downstream references.
                    account.AccountNumber = accountNumber;
                    account.IsSegmented = true;
                    account.UpdatedAt = provisionedAt;
                    account.UpdatedBy = _currentUser.UserName ?? "system";
                    foreach (var value in identity.Values)
                    {
                        _db.AccountSegmentValues.Add(new AccountSegmentValue
                        {
                            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                            SegmentStructureId = value.SegmentStructureId, SegmentPosition = value.SegmentPosition,
                            SegmentValue = value.SegmentValue, SegmentLookupValueId = value.SegmentLookupValueId,
                            EffectiveDate = provisionedAt, CreatedAt = provisionedAt,
                            CreatedBy = _currentUser.UserName ?? "system"
                        });
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Existing Finance account '{account.AccountCode}' is not ready for the active account-number structure: {string.Join("; ", readiness.Issues)}");
                }
            }
        }

        await new FinanceClassificationManifestSeeder(_db, _logger)
            .SeedAsync(tenantId, provisionedAt, cancellationToken);
        var bookCodes = await _db.AccountAccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.AccountId == account.Id && item.IsEnabled && !item.IsDeleted)
            .Join(_db.AccountingBooks.AsNoTracking(), mapping => mapping.AccountingBookId, book => book.Id,
                (_, book) => book.Code)
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        var result = new ProvisionedFinanceAccountDto
        {
            AccountId = account.Id,
            AccountCode = account.AccountCode,
            ClassificationCode = classificationCode,
            AccountingBookCodes = bookCodes,
            WasCreated = wasCreated
        };
        return result;
    }

    private async Task<bool> CanAdoptLegacyProcurementSeederAccountAsync(
        Account account,
        string accountCode,
        CancellationToken cancellationToken)
    {
        if (!LegacyProcurementAccountCodes.Contains(accountCode)
            || !string.Equals(account.AccountCode, accountCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(account.AccountNumber, accountCode, StringComparison.OrdinalIgnoreCase)
            || account.IsSegmented
            || !string.Equals(account.CreatedBy, LegacyProcurementSeederActor, StringComparison.Ordinal)
            || (account.UpdatedBy is not null
                && !string.Equals(account.UpdatedBy, LegacyProcurementSeederActor, StringComparison.Ordinal)))
            return false;

        var hasSegmentEvidence = await _db.AccountSegmentValues
            .AnyAsync(item => item.AccountId == account.Id, cancellationToken);
        var hasBookEvidence = await _db.AccountAccountingBooks
            .AnyAsync(item => item.AccountId == account.Id, cancellationToken);
        return !hasSegmentEvidence && !hasBookEvidence;
    }

    private static string NormalizeRequired(string? value, string label, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
            throw new InvalidOperationException($"{label} is required and cannot exceed {maxLength} characters.");
        return normalized;
    }
}
