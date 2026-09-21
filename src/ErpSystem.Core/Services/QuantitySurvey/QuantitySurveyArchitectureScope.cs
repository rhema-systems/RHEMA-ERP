using System.Text.Json;

namespace ErpSystem.Core.Services.QuantitySurvey;

/// <summary>Default QS scope from architecture sections 16.4 and 16.4.1.</summary>
public static class QuantitySurveyArchitectureScope
{
    // These questionnaire extensions are retained for existing record lineage,
    // but do not belong to the default operating or configuration screens.
    public static bool IsExtensionDecision(string key) => key is
        "QS-DEC-006" or "QS-DEC-013" or "QS-DEC-014" or "QS-DEC-017";

    public static bool HasConfiguration(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) return false;
        try
        {
            using var value = JsonDocument.Parse(valueJson);
            return value.RootElement.ValueKind != JsonValueKind.Object ||
                value.RootElement.EnumerateObject().Any();
        }
        catch (JsonException)
        {
            // Malformed saved data is configured-but-invalid, never silently skipped.
            return true;
        }
    }
}
