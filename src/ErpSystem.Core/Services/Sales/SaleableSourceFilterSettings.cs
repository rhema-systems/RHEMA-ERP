using System.Text.Json;

namespace ErpSystem.Core.Services.Sales;

internal sealed record SaleableSourceFilterValue(string Field, string Value);

internal static class SaleableSourceFilterSettings
{
    public static IReadOnlyCollection<SaleableSourceFilterValue> Parse(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            var root = document.RootElement;
            var filters = new List<SaleableSourceFilterValue>();

            if (root.TryGetProperty("filters", out var configuredFilters)
                && configuredFilters.ValueKind == JsonValueKind.Array)
            {
                foreach (var filter in configuredFilters.EnumerateArray())
                {
                    if (!TryReadString(filter, "field", out var field)
                        || !TryReadString(filter, "value", out var value))
                    {
                        continue;
                    }

                    AddDistinct(filters, field, value);
                }
            }

            if (TryReadString(root, "filterField", out var legacyField)
                && TryReadString(root, "filterValue", out var legacyValue))
            {
                AddDistinct(filters, legacyField, legacyValue);
            }

            return filters;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Normalize(string value)
        => value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Trim()
            .ToLowerInvariant();

    public static bool TextMatches(string? actual, string expected)
        => !string.IsNullOrWhiteSpace(actual)
            && actual.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool BoolMatches(bool actual, string expected)
        => bool.TryParse(expected, out var parsed)
            ? actual == parsed
            : (actual ? "yes" : "no").Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static void AddDistinct(
        ICollection<SaleableSourceFilterValue> filters,
        string field,
        string value)
    {
        if (filters.Any(filter => filter.Field.Equals(field, StringComparison.OrdinalIgnoreCase)
            && filter.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        filters.Add(new SaleableSourceFilterValue(field.Trim(), value.Trim()));
    }
}
