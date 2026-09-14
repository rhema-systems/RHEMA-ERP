namespace ErpSystem.Core.Finance;

/// <summary>One canonical grammar shared by C5 policy preparation and C6 event preparation.</summary>
public static class FinancePreparedIdentityNormalizer
{
    private static readonly HashSet<string> PseudoSelectors = new(StringComparer.Ordinal)
        { "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS" };

    public sealed record Identity(string OriginatingModuleCode, string SourceDocumentType, string PostingAction);

    public static Identity Normalize(string module, string documentType, string postingAction) =>
        new(NormalizeModule(module), NormalizeValue(documentType, "Source document type"),
            NormalizeValue(postingAction, "Posting action"));

    public static string NormalizeModule(string value)
    {
        var module = NormalizeValue(value, "Originating module code");
        if (!FinanceModuleLockCatalog.Definitions.Any(item => item.Code == module))
            throw new InvalidOperationException("ORIGIN_MODULE_NOT_REGISTERED: originating module code is not in the canonical Finance period-lock catalog.");
        return module;
    }

    public static string NormalizeValue(string value, string label)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!IsAsciiIdentity(normalized) || PseudoSelectors.Contains(normalized))
            throw new InvalidOperationException($"{label} must be a canonical ASCII stable identity and cannot be a pseudo selector.");
        return normalized;
    }

    private static bool IsAsciiIdentity(string value)
    {
        if (value.Length == 0 || value[0] is < 'A' or > 'Z') return false;
        return value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '.' or '-');
    }
}
