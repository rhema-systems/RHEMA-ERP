using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Api.Services.Finance.Reporting;

internal static class FinancialStatementRowOrdering
{
    public static IReadOnlyList<FinancialStatementRowInputDto> Order(
        IReadOnlyCollection<FinancialStatementRowInputDto> rows)
        => OrderCore(
            rows,
            row => row.RowCode,
            row => row.ParentRowCode,
            row => row.DisplayOrder);

    public static IReadOnlyList<FinancialStatementRow> Order(
        IReadOnlyCollection<FinancialStatementRow> rows)
    {
        var rowCodesById = rows.ToDictionary(row => row.Id, row => row.RowCode);
        return OrderCore(
            rows,
            row => row.RowCode,
            row => row.ParentRowId.HasValue
                && rowCodesById.TryGetValue(row.ParentRowId.Value, out var parentCode)
                    ? parentCode
                    : null,
            row => row.DisplayOrder);
    }

    private static IReadOnlyList<T> OrderCore<T>(
        IReadOnlyCollection<T> rows,
        Func<T, string> codeSelector,
        Func<T, string?> parentCodeSelector,
        Func<T, int> displayOrderSelector)
    {
        var children = rows
            .GroupBy(
                row => NormalizeParent(parentCodeSelector(row)),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(displayOrderSelector)
                    .ThenBy(codeSelector, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var ordered = new List<T>(rows.Count);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(T row)
        {
            var code = codeSelector(row);
            if (!visited.Add(code))
            {
                return;
            }

            ordered.Add(row);
            if (children.TryGetValue(code, out var childRows))
            {
                foreach (var child in childRows)
                {
                    Visit(child);
                }
            }
        }

        if (children.TryGetValue(string.Empty, out var roots))
        {
            foreach (var root in roots)
            {
                Visit(root);
            }
        }

        foreach (var row in rows
                     .OrderBy(displayOrderSelector)
                     .ThenBy(codeSelector, StringComparer.OrdinalIgnoreCase))
        {
            Visit(row);
        }

        return ordered;
    }

    private static string NormalizeParent(string? parentCode)
        => string.IsNullOrWhiteSpace(parentCode) ? string.Empty : parentCode;
}
