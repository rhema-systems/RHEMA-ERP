namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Finance-owned compatibility boundary for legacy subledger single-book settings.
/// It deliberately resolves one concrete book and never expands pseudo-book selectors.
/// </summary>
public static class FinanceAccountingBookCodeResolver
{
    public static string ResolveLegacySingleBook(string? configuredValue, bool settingsExist)
    {
        if (!settingsExist)
            return "IFRS";

        var value = configuredValue?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A concrete subledger accounting book is required.", nameof(configuredValue));

        return value.ToUpperInvariant() switch
        {
            "IFRS" => "IFRS",
            "LOCAL" or "LOCAL_STATUTORY" => "LOCAL_STATUTORY",
            "MANAGEMENT" => "MANAGEMENT",
            "ALLCLASSIFIEDBOOKS" or "ALL_ACTIVE_BOOKS" => throw new ArgumentException(
                "A multi-book selector cannot be submitted to the single-book posting executor.", nameof(configuredValue)),
            _ => throw new ArgumentException(
                $"Unknown subledger accounting-book value '{value}'.", nameof(configuredValue))
        };
    }
}
