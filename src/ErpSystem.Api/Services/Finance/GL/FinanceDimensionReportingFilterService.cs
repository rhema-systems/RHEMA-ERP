using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Resolves transaction-dimension report filters against Finance-owned tenant data and
/// applies them to posted-ledger queries. One filter may select several values (OR), while
/// separate dimensions are combined with AND semantics.
/// </summary>
public sealed class FinanceDimensionReportingFilterService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FinanceDimensionReportingFilterService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ResolvedFinanceDimensionFilter>> ResolveAsync(
        IEnumerable<FinanceDimensionFilterDto>? filters,
        CancellationToken cancellationToken = default)
    {
        var requested = filters?
            .Where(filter => filter != null)
            .ToList() ?? [];
        if (requested.Count == 0)
        {
            return [];
        }

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var resolved = new List<ResolvedFinanceDimensionFilter>();
        foreach (var filter in requested)
        {
            var valueCodes = filter.ValueCodes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (valueCodes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Transaction-dimension report filters require at least one value code.");
            }

            IQueryable<FinanceDimensionDefinition> definitionQuery = _context.FinanceDimensionDefinitions
                .AsNoTracking()
                .Where(definition => definition.TenantId == tenantId && !definition.IsDeleted);
            if (filter.FinanceDimensionDefinitionId.HasValue &&
                filter.FinanceDimensionDefinitionId.Value != Guid.Empty)
            {
                var definitionId = filter.FinanceDimensionDefinitionId.Value;
                definitionQuery = definitionQuery.Where(definition => definition.Id == definitionId);
            }
            else if (!string.IsNullOrWhiteSpace(filter.DimensionCode))
            {
                var dimensionCode = filter.DimensionCode.Trim().ToUpperInvariant();
                definitionQuery = definitionQuery.Where(definition => definition.Code == dimensionCode);
            }
            else
            {
                throw new InvalidOperationException(
                    "Transaction-dimension report filters require a tenant-owned definition id or code.");
            }

            var definition = await definitionQuery.SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "One or more transaction-dimension report filters do not belong to the current tenant.");
            if (!string.IsNullOrWhiteSpace(filter.DimensionCode)
                && !definition.Code.Equals(
                    filter.DimensionCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The transaction-dimension definition id and code do not identify the same tenant-owned dimension.");
            }
            var values = await _context.FinanceDimensionValues
                .AsNoTracking()
                .Where(value =>
                    value.TenantId == tenantId &&
                    !value.IsDeleted &&
                    value.FinanceDimensionDefinitionId == definition.Id &&
                    valueCodes.Contains(value.Code))
                .Select(value => new { value.Id, value.Code, value.Name })
                .ToListAsync(cancellationToken);
            if (values.Count != valueCodes.Length)
            {
                throw new InvalidOperationException(
                    $"One or more values are invalid for transaction dimension '{definition.Code}'.");
            }

            resolved.Add(new ResolvedFinanceDimensionFilter(
                definition.Id,
                definition.Code,
                definition.Name,
                values.Select(value => value.Id).ToArray(),
                values.Select(value => value.Code).OrderBy(value => value).ToArray(),
                values.Select(value => value.Name).OrderBy(value => value).ToArray()));
        }

        return resolved
            .GroupBy(filter => filter.DefinitionId)
            .Select(group => new ResolvedFinanceDimensionFilter(
                group.Key,
                group.First().DimensionCode,
                group.First().DimensionName,
                group.SelectMany(filter => filter.ValueIds).Distinct().ToArray(),
                group.SelectMany(filter => filter.ValueCodes).Distinct().OrderBy(value => value).ToArray(),
                group.SelectMany(filter => filter.ValueNames).Distinct().OrderBy(value => value).ToArray()))
            .OrderBy(filter => filter.DimensionCode)
            .ToList();
    }

    public IQueryable<AccountTransaction> Apply(
        IQueryable<AccountTransaction> query,
        IReadOnlyCollection<ResolvedFinanceDimensionFilter>? filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return query;
        }

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        foreach (var filter in filters)
        {
            var definitionId = filter.DefinitionId;
            var valueIds = filter.ValueIds.ToArray();
            query = query.Where(transaction =>
                transaction.FinanceDimensionSetId.HasValue &&
                transaction.FinanceDimensionSet!.TenantId == tenantId &&
                !transaction.FinanceDimensionSet.IsDeleted &&
                transaction.FinanceDimensionSet.Items.Any(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.FinanceDimensionDefinitionId == definitionId &&
                    valueIds.Contains(item.FinanceDimensionValueId)));
        }

        return query;
    }
}

public sealed record ResolvedFinanceDimensionFilter(
    Guid DefinitionId,
    string DimensionCode,
    string DimensionName,
    IReadOnlyList<Guid> ValueIds,
    IReadOnlyList<string> ValueCodes,
    IReadOnlyList<string> ValueNames);
