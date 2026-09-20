using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Persists exact settlement-to-origin-line evidence and owns the deterministic proportional
/// allocation algorithm shared by AP and AR. Consumers can only pass contexts assembled from
/// tenant-scoped Finance entities; route identity remains compiled and server-controlled.
/// </summary>
public sealed class FinanceSettlementDimensionService : IFinanceSettlementDimensionService
{
    private const string EvidenceVersion = "1.0";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly FinanceDimensionAdministrationService _dimensions;

    public FinanceSettlementDimensionService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        FinanceDimensionAdministrationService dimensions)
    {
        _db = db;
        _currentUser = currentUser;
        _dimensions = dimensions;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeDraftAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        IReadOnlyList<FinanceSettlementAllocationInput> allocations,
        CancellationToken cancellationToken = default)
    {
        var route = RequireSettlementRoute(producer);
        if (sourceDocumentId == Guid.Empty)
            throw new ArgumentException("A persisted settlement document id is required.", nameof(sourceDocumentId));

        allocations ??= Array.Empty<FinanceSettlementAllocationInput>();
        if (allocations.GroupBy(item => item.SettlementSourceLineId).Any(group => group.Count() > 1))
            throw new InvalidOperationException("A settlement source line may be synchronized only once per request.");

        var desired = new List<DesiredComponent>();
        foreach (var allocation in allocations.OrderBy(item => item.SettlementSourceLineId))
            desired.AddRange(BuildDesiredComponents(route, sourceDocumentId, allocation));

        await ValidateCanonicalEvidenceAsync(desired, cancellationToken);

        var existing = await _db.FinanceSettlementDimensionComponents
            .Where(item => item.TenantId == TenantId
                && item.RouteId == route.Id
                && item.SourceDocumentId == sourceDocumentId
                && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var desiredKeys = desired.Select(item => item.Key).ToHashSet();
        // A route service calls this method only for a document that has legitimately returned to
        // Draft. Preserve prior frozen rows as superseded audit evidence; never rewrite them.
        foreach (var stale in existing.Where(item =>
                     item.EvidenceFrozenAt.HasValue || !desiredKeys.Contains(Key(item))))
        {
            stale.IsDeleted = true;
            stale.DeletedAt = DateTime.UtcNow;
            stale.DeletedBy = UserName;
            stale.UpdatedAt = DateTime.UtcNow;
            stale.UpdatedBy = UserName;
            stale.LastModifiedById = UserId;
        }

        var existingByKey = existing.Where(item => !item.IsDeleted).ToDictionary(Key);
        foreach (var item in desired)
        {
            if (!existingByKey.TryGetValue(item.Key, out var entity))
            {
                entity = new FinanceSettlementDimensionComponent
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    RouteId = route.Id,
                    ProducerModule = route.ProducerModule,
                    SourceRoute = route.SourceRoute,
                    SourceDocumentType = route.DocumentType,
                    ContractVersion = route.ContractVersion,
                    SourceDocumentId = sourceDocumentId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName,
                    CreatedById = UserId
                };
                _db.FinanceSettlementDimensionComponents.Add(entity);
            }

            entity.SettlementSourceLineId = item.SettlementSourceLineId;
            entity.SettlementAllocationId = item.SettlementAllocationId;
            entity.OriginatingDocumentId = item.OriginatingDocumentId;
            entity.OriginatingSourceLineId = item.OriginatingSourceLineId;
            entity.ComponentType = item.ComponentType;
            entity.FinanceDimensionSetId = item.FinanceDimensionSetId;
            entity.FinanceDimensionSnapshotId = item.FinanceDimensionSnapshotId;
            entity.TransactionCurrencyCode = item.TransactionCurrencyCode;
            entity.TransactionAmount = item.TransactionAmount;
            entity.FunctionalAmount = item.FunctionalAmount;
            entity.ExchangeRateId = item.ExchangeRateId;
            entity.ExchangeRate = item.ExchangeRate;
            entity.ComparisonExchangeRateId = item.ComparisonExchangeRateId;
            entity.ComparisonExchangeRate = item.ComparisonExchangeRate;
            entity.EvidenceVersion = EvidenceVersion;
            entity.IsFinalResidualRecipient = item.IsFinalResidualRecipient;
            entity.RoundingResidualTransactionAmount = item.RoundingResidualTransactionAmount;
            entity.RoundingResidualFunctionalAmount = item.RoundingResidualFunctionalAmount;
            entity.EvidenceHash = item.EvidenceHash;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName;
            entity.LastModifiedById = UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(producer, sourceDocumentId, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancePostingDimensionValueDto>> ResolvePostingDimensionsAsync(
        FinancePostingProducerContext producer,
        Guid componentEvidenceId,
        Guid postingAccountId,
        DateTime postingDate,
        CancellationToken cancellationToken = default)
    {
        var route = RequireSettlementRoute(producer);
        var evidence = await _db.FinanceSettlementDimensionComponents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == componentEvidenceId
                && item.TenantId == TenantId
                && item.RouteId == route.Id
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Settlement dimension component evidence was not found for this tenant and route.");
        var inherited = evidence.FinanceDimensionSnapshotId.HasValue
            ? await _db.FinanceDimensionSnapshotItems.AsNoTracking()
                .Where(item => item.TenantId == TenantId
                    && item.FinanceDimensionSnapshotId == evidence.FinanceDimensionSnapshotId.Value
                    && !item.IsDeleted)
                .OrderBy(item => item.DimensionCodeSnapshot)
                .Select(item => new FinancePostingDimensionValueDto
                {
                    DimensionCode = item.DimensionCodeSnapshot,
                    ValueCode = item.DimensionValueCodeSnapshot
                })
                .ToArrayAsync(cancellationToken)
            : evidence.FinanceDimensionSetId.HasValue
                ? await _db.FinanceDimensionSetItems.AsNoTracking()
                    .Where(item => item.TenantId == TenantId
                        && item.FinanceDimensionSetId == evidence.FinanceDimensionSetId.Value
                        && !item.IsDeleted)
                    .OrderBy(item => item.DimensionCodeSnapshot)
                    .Select(item => new FinancePostingDimensionValueDto
                    {
                        DimensionCode = item.DimensionCodeSnapshot,
                        ValueCode = item.DimensionValueCodeSnapshot
                    })
                    .ToArrayAsync(cancellationToken)
                : Array.Empty<FinancePostingDimensionValueDto>();
        var rules = await _dimensions.GetSourceLineRulesAsync(
            producer, postingAccountId, postingDate, cancellationToken);
        var serverOwnedCodes = rules.Where(item => item.RuleType is "Fixed" or "Prohibited")
            .Select(item => item.FinanceDimensionDefinition.Code)
            .ToHashSet(StringComparer.Ordinal);
        var eligible = inherited.Where(item => !serverOwnedCodes.Contains(item.DimensionCode)).ToArray();
        var state = await GetCertificationStateAsync(route, cancellationToken);
        var resolved = await _dimensions.ResolveSourceLineAsync(
            producer,
            postingAccountId,
            postingDate,
            eligible,
            state,
            refreshPersistedFixedValues: true,
            cancellationToken);
        if (resolved.ReadinessWarnings.Any(message =>
                message.Contains(" is required ", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Required Finance dimensions are missing from a derived settlement posting line.");
        if (resolved.DimensionSet is null)
            return Array.Empty<FinancePostingDimensionValueDto>();
        await _db.SaveChangesAsync(cancellationToken);
        return resolved.DimensionSet.Items.OrderBy(item => item.DimensionCodeSnapshot)
            .Select(item => new FinancePostingDimensionValueDto
            {
                DimensionCode = item.DimensionCodeSnapshot,
                ValueCode = item.DimensionValueCodeSnapshot
            }).ToArray();
    }

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var route = RequireSettlementRoute(producer);
        var rows = await _db.FinanceSettlementDimensionComponents.AsNoTracking()
            .Include(item => item.FinanceDimensionSet)
                .ThenInclude(item => item!.Items)
            .Include(item => item.FinanceDimensionSnapshot)
                .ThenInclude(item => item!.Items)
            .Where(item => item.TenantId == TenantId
                && item.RouteId == route.Id
                && item.SourceDocumentId == sourceDocumentId
                && !item.IsDeleted)
            .OrderBy(item => item.SettlementSourceLineId)
            .ThenBy(item => item.OriginatingSourceLineId)
            .ThenBy(item => item.ComponentType)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        IReadOnlyCollection<Guid> authoritativeSettlementSourceLineIds,
        CancellationToken cancellationToken = default)
    {
        var route = RequireSettlementRoute(producer);
        var expectedIds = (authoritativeSettlementSourceLineIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var rows = await _db.FinanceSettlementDimensionComponents
            .Where(item => item.TenantId == TenantId
                && item.RouteId == route.Id
                && item.SourceDocumentId == sourceDocumentId
                && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var actualIds = rows.Select(item => item.SettlementSourceLineId).Distinct().ToHashSet();
        var missing = expectedIds.Where(id => !actualIds.Contains(id)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Settlement dimension evidence is missing for {missing.Length} authoritative allocation line(s).");
        var unexpected = actualIds.Where(id => !expectedIds.Contains(id)).ToArray();
        if (unexpected.Length > 0)
            throw new InvalidOperationException("Settlement dimension evidence contains stale allocation lines. Refresh the draft before submission.");

        var state = await GetCertificationStateAsync(route, cancellationToken);
        if (state == FinanceDimensionCertificationState.LegacyReadOnly && expectedIds.Count > 0)
            throw new InvalidOperationException($"Finance dimension route {route.SourceRoute} is not certified for new settlement documents.");
        if (state == FinanceDimensionCertificationState.Enforced
            && rows.Any(item => !item.FinanceDimensionSetId.HasValue || !item.FinanceDimensionSnapshotId.HasValue))
            throw new InvalidOperationException("Required inherited Finance dimension evidence is missing for one or more settlement allocation lines.");

        var frozenAt = DateTime.UtcNow;
        foreach (var row in rows.Where(item => !item.EvidenceFrozenAt.HasValue))
        {
            row.EvidenceFrozenAt = frozenAt;
            row.UpdatedAt = frozenAt;
            row.UpdatedBy = UserName;
            row.LastModifiedById = UserId;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(producer, sourceDocumentId, cancellationToken);
    }

    private static FinanceDimensionRouteDefinition RequireSettlementRoute(FinancePostingProducerContext producer)
    {
        var route = producer?.Definition ?? throw new ArgumentNullException(nameof(producer));
        if (route.Grain != FinanceDimensionGrain.SettlementAllocationLine)
            throw new InvalidOperationException($"Route {route.SourceRoute} does not support settlement-allocation dimension evidence.");
        return route;
    }

    private IEnumerable<DesiredComponent> BuildDesiredComponents(
        FinanceDimensionRouteDefinition route,
        Guid sourceDocumentId,
        FinanceSettlementAllocationInput allocation)
    {
        if (allocation.SettlementSourceLineId == Guid.Empty)
            throw new InvalidOperationException("Every settlement allocation requires a stable source-line id.");
        var origins = allocation.OriginatingLines
            .OrderBy(item => item.OriginatingSourceLineId)
            .ToArray();
        if (origins.Length == 0 || origins.Any(item => item.OriginatingSourceLineId == Guid.Empty || item.AllocationWeight <= 0m))
            throw new InvalidOperationException("Settlement allocations require positive weights for persisted originating economic lines.");
        if (origins.GroupBy(item => item.OriginatingSourceLineId).Any(group => group.Count() > 1))
            throw new InvalidOperationException("An originating economic line may appear only once in a settlement allocation.");
        var components = allocation.Components.Where(item => item.TransactionAmount != 0m || item.FunctionalAmount != 0m).ToArray();
        if (components.GroupBy(item => item.ComponentType).Any(group => group.Count() > 1))
            throw new InvalidOperationException("A settlement component type may appear only once per allocation.");

        foreach (var component in components.OrderBy(item => item.ComponentType))
        {
            var currency = component.TransactionCurrencyCode?.Trim().ToUpperInvariant();
            if (currency?.Length != 3)
                throw new InvalidOperationException("Settlement component currency must be a three-character code.");
            if (component.ExchangeRate <= 0m || component.ComparisonExchangeRate is <= 0m)
                throw new InvalidOperationException("Settlement component exchange-rate evidence must be greater than zero.");
            var transactionShares = Allocate(component.TransactionAmount, origins);
            var functionalShares = Allocate(component.FunctionalAmount, origins);
            for (var index = 0; index < origins.Length; index++)
            {
                var origin = origins[index];
                var evidenceHash = Hash(string.Join("|",
                    EvidenceVersion,
                    TenantId.ToString("N"),
                    (int)route.Id,
                    sourceDocumentId.ToString("N"),
                    allocation.SettlementSourceLineId.ToString("N"),
                    allocation.SettlementAllocationId?.ToString("N") ?? "NONE",
                    allocation.OriginatingDocumentId?.ToString("N") ?? "NONE",
                    origin.OriginatingSourceLineId.ToString("N"),
                    (int)component.ComponentType,
                    origin.FinanceDimensionSetId?.ToString("N") ?? "NONE",
                    origin.FinanceDimensionSnapshotId?.ToString("N") ?? "NONE",
                    currency,
                    transactionShares[index].Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    functionalShares[index].Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    component.ExchangeRateId?.ToString("N") ?? "NONE",
                    component.ExchangeRate.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture),
                    component.ComparisonExchangeRateId?.ToString("N") ?? "NONE",
                    component.ComparisonExchangeRate?.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture) ?? "NONE",
                    transactionShares[index].Residual.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    functionalShares[index].Residual.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
                yield return new DesiredComponent(
                    allocation.SettlementSourceLineId,
                    allocation.SettlementAllocationId,
                    allocation.OriginatingDocumentId,
                    origin.OriginatingSourceLineId,
                    component.ComponentType,
                    origin.FinanceDimensionSetId,
                    origin.FinanceDimensionSnapshotId,
                    currency,
                    transactionShares[index].Amount,
                    functionalShares[index].Amount,
                    component.ExchangeRateId,
                    component.ExchangeRate,
                    component.ComparisonExchangeRateId,
                    component.ComparisonExchangeRate,
                    index == origins.Length - 1,
                    transactionShares[index].Residual,
                    functionalShares[index].Residual,
                    evidenceHash);
            }
        }
    }

    private async Task ValidateCanonicalEvidenceAsync(
        IReadOnlyCollection<DesiredComponent> desired,
        CancellationToken cancellationToken)
    {
        var setIds = desired.Where(item => item.FinanceDimensionSetId.HasValue)
            .Select(item => item.FinanceDimensionSetId!.Value).Distinct().ToArray();
        var snapshotIds = desired.Where(item => item.FinanceDimensionSnapshotId.HasValue)
            .Select(item => item.FinanceDimensionSnapshotId!.Value).Distinct().ToArray();
        var sets = setIds.Length == 0
            ? new Dictionary<Guid, FinanceDimensionSet>()
            : await _db.FinanceDimensionSets.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted && setIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (sets.Count != setIds.Length)
            throw new InvalidOperationException("Settlement evidence references an invalid or cross-tenant Finance dimension set.");
        var snapshots = snapshotIds.Length == 0
            ? new Dictionary<Guid, FinanceDimensionSnapshot>()
            : await _db.FinanceDimensionSnapshots.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted && snapshotIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (snapshots.Count != snapshotIds.Length)
            throw new InvalidOperationException("Settlement evidence references an invalid or cross-tenant Finance dimension snapshot.");
        foreach (var item in desired.Where(item => item.FinanceDimensionSnapshotId.HasValue))
        {
            var snapshot = snapshots[item.FinanceDimensionSnapshotId!.Value];
            if (!item.FinanceDimensionSetId.HasValue || snapshot.FinanceDimensionSetId != item.FinanceDimensionSetId.Value)
                throw new InvalidOperationException("Settlement dimension snapshot and canonical set evidence are inconsistent.");
        }
    }

    private async Task<FinanceDimensionCertificationState> GetCertificationStateAsync(
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken)
    {
        var state = await _db.FinanceDimensionRouteCertifications.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.RouteId == route.Id && !item.IsDeleted)
            .Select(item => (FinanceDimensionCertificationState?)item.State)
            .SingleOrDefaultAsync(cancellationToken);
        return state ?? route.DefaultState;
    }

    private static Share[] Allocate(decimal total, IReadOnlyList<FinanceSettlementOriginLineInput> origins)
    {
        var result = new Share[origins.Count];
        var totalWeight = origins.Sum(item => item.AllocationWeight);
        decimal allocated = 0m;
        for (var index = 0; index < origins.Count; index++)
        {
            var independentlyRounded = RoundMoney(total * origins[index].AllocationWeight / totalWeight);
            var amount = index == origins.Count - 1 ? RoundMoney(total - allocated) : independentlyRounded;
            result[index] = new Share(amount, index == origins.Count - 1 ? RoundMoney(amount - independentlyRounded) : 0m);
            allocated += amount;
        }
        if (result.Sum(item => item.Amount) != RoundMoney(total))
            throw new InvalidOperationException("Deterministic settlement allocation did not reconcile to its component total.");
        return result;
    }

    private static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ComponentKey Key(FinanceSettlementDimensionComponent item) =>
        new(item.SettlementSourceLineId, item.OriginatingSourceLineId, item.ComponentType);

    private static FinanceSettlementDimensionComponentDto Map(FinanceSettlementDimensionComponent item) => new()
    {
        Id = item.Id,
        SettlementSourceLineId = item.SettlementSourceLineId,
        SettlementAllocationId = item.SettlementAllocationId,
        OriginatingDocumentId = item.OriginatingDocumentId,
        OriginatingSourceLineId = item.OriginatingSourceLineId,
        ComponentType = item.ComponentType,
        FinanceDimensionSetId = item.FinanceDimensionSetId,
        FinanceDimensionSnapshotId = item.FinanceDimensionSnapshotId,
        DimensionCombination = item.FinanceDimensionSnapshot?.DisplayValueSnapshot
            ?? item.FinanceDimensionSet?.DisplayValue,
        DimensionHash = item.FinanceDimensionSnapshot?.CombinationHashSnapshot
            ?? item.FinanceDimensionSet?.CombinationHash,
        DimensionValues = item.FinanceDimensionSnapshot?.Items
            .Where(value => !value.IsDeleted)
            .OrderBy(value => value.DimensionCodeSnapshot)
            .Select(value => new FinanceSourceDimensionValueDto
            {
                DimensionCode = value.DimensionCodeSnapshot,
                DimensionName = value.DimensionNameSnapshot,
                ValueCode = value.DimensionValueCodeSnapshot,
                ValueName = value.DimensionValueNameSnapshot,
                RuleType = value.RuleTypeSnapshot,
                IsReadOnly = true
            }).ToArray()
            ?? item.FinanceDimensionSet?.Items
                .Where(value => !value.IsDeleted)
                .OrderBy(value => value.DimensionCodeSnapshot)
                .Select(value => new FinanceSourceDimensionValueDto
                {
                    DimensionCode = value.DimensionCodeSnapshot,
                    DimensionName = value.DimensionNameSnapshot,
                    ValueCode = value.DimensionValueCodeSnapshot,
                    ValueName = value.DimensionValueNameSnapshot,
                    IsReadOnly = true
                }).ToArray()
            ?? Array.Empty<FinanceSourceDimensionValueDto>(),
        TransactionCurrencyCode = item.TransactionCurrencyCode,
        TransactionAmount = item.TransactionAmount,
        FunctionalAmount = item.FunctionalAmount,
        ExchangeRateId = item.ExchangeRateId,
        ExchangeRate = item.ExchangeRate,
        ComparisonExchangeRateId = item.ComparisonExchangeRateId,
        ComparisonExchangeRate = item.ComparisonExchangeRate,
        IsFinalResidualRecipient = item.IsFinalResidualRecipient,
        RoundingResidualTransactionAmount = item.RoundingResidualTransactionAmount,
        RoundingResidualFunctionalAmount = item.RoundingResidualFunctionalAmount,
        EvidenceHash = item.EvidenceHash,
        EvidenceFrozenAt = item.EvidenceFrozenAt
    };

    private readonly record struct Share(decimal Amount, decimal Residual);
    private readonly record struct ComponentKey(
        Guid SettlementSourceLineId,
        Guid? OriginatingSourceLineId,
        FinanceSettlementComponentType ComponentType);

    private sealed record DesiredComponent(
        Guid SettlementSourceLineId,
        Guid? SettlementAllocationId,
        Guid? OriginatingDocumentId,
        Guid OriginatingSourceLineId,
        FinanceSettlementComponentType ComponentType,
        Guid? FinanceDimensionSetId,
        Guid? FinanceDimensionSnapshotId,
        string TransactionCurrencyCode,
        decimal TransactionAmount,
        decimal FunctionalAmount,
        Guid? ExchangeRateId,
        decimal ExchangeRate,
        Guid? ComparisonExchangeRateId,
        decimal? ComparisonExchangeRate,
        bool IsFinalResidualRecipient,
        decimal RoundingResidualTransactionAmount,
        decimal RoundingResidualFunctionalAmount,
        string EvidenceHash)
    {
        public ComponentKey Key => new(SettlementSourceLineId, OriginatingSourceLineId, ComponentType);
    }
}
