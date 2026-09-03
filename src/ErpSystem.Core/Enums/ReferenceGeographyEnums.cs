namespace ErpSystem.Core.Enums;

/// <summary>
/// Why an alternate name for a <c>GeoArea</c> exists. The kind matters because the three cases
/// deserve different treatment on screen: a former name is history and should be shown as such,
/// while a spelling or an abbreviation is just another way of writing the current name.
/// </summary>
public enum GeoAreaAliasKind
{
    /// <summary>
    /// The name the area carried before a boundary or naming change — "Brong Ahafo" for the areas
    /// that became Bono, Bono East and Ahafo in 2019. The single most important kind: it is what
    /// lets an old spreadsheet still resolve.
    /// </summary>
    FormerName = 0,

    /// <summary>An alternate spelling or transliteration — "Sekondi-Takoradi" / "Sekondi/Takoradi".</summary>
    Spelling = 1,

    /// <summary>A short form in common use — "GAR" for Greater Accra Region.</summary>
    Abbreviation = 2,

    /// <summary>A local-language or colloquial name that appears in submitted documents.</summary>
    Vernacular = 3,

    /// <summary>Anything else worth resolving on.</summary>
    Other = 99,
}
