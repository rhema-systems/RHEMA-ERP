using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringDocumentRules
{
    public static CivilEngineeringFileCategory FileCategory(string extension) => NormalizeExtension(extension) switch
    {
        ".dwg" or ".dxf" => CivilEngineeringFileCategory.AutoCad,
        ".pro" or ".prc" => CivilEngineeringFileCategory.ProtaStructure,
        ".rvt" or ".rfa" or ".rte" => CivilEngineeringFileCategory.Revit,
        ".std" => CivilEngineeringFileCategory.StaadPro,
        ".pdf" => CivilEngineeringFileCategory.Pdf,
        ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => CivilEngineeringFileCategory.Office,
        ".jpg" or ".jpeg" or ".png" => CivilEngineeringFileCategory.Image,
        _ => CivilEngineeringFileCategory.Other
    };

    public static string ExpectedReference(
        string projectCode,
        CivilEngineeringDesignDiscipline discipline,
        string? packageCode,
        int sequenceNumber,
        int revisionNumber,
        CivilEngineeringDocumentNamingPolicy policy)
    {
        if (sequenceNumber is < 1 or > 999999)
            throw new InvalidOperationException("The engineering document sequence must be between 1 and 999999.");
        if (revisionNumber is < 0 or > 9999)
            throw new InvalidOperationException("The engineering document revision must be between 0 and 9999.");
        var project = Code(projectCode, "project");
        var middle = policy == CivilEngineeringDocumentNamingPolicy.ProjectWorkPackageSequenceRevision
            ? Code(packageCode, "work package")
            : DisciplineCode(discipline);
        return $"{project}-{middle}-{sequenceNumber:000}-R{revisionNumber:00}";
    }

    public static void EnsureCanSubmit(CivilEngineeringDocumentStatus status)
    {
        if (status is not (CivilEngineeringDocumentStatus.Draft or CivilEngineeringDocumentStatus.Returned))
            throw new InvalidOperationException("Only a Draft or Returned engineering document can be submitted for review.");
    }

    public static void EnsureCanReview(CivilEngineeringDocumentStatus status)
    {
        if (status != CivilEngineeringDocumentStatus.ForReview)
            throw new InvalidOperationException("Only an engineering document awaiting review can be approved or returned.");
    }

    public static string NormalizeExtension(string? value)
    {
        var clean = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (clean.Length == 0) return string.Empty;
        return clean.StartsWith('.') ? clean : $".{clean}";
    }

    private static string DisciplineCode(CivilEngineeringDesignDiscipline discipline) => discipline switch
    {
        CivilEngineeringDesignDiscipline.Civil => "CIV",
        CivilEngineeringDesignDiscipline.Structural => "STR",
        CivilEngineeringDesignDiscipline.Architecture => "ARC",
        CivilEngineeringDesignDiscipline.Geodetic => "GEO",
        CivilEngineeringDesignDiscipline.TownPlanning => "TWP",
        CivilEngineeringDesignDiscipline.Mechanical => "MEC",
        CivilEngineeringDesignDiscipline.Electrical => "ELE",
        _ => throw new InvalidOperationException("Select a supported engineering discipline.")
    };

    private static string Code(string? value, string label)
    {
        var normalized = string.Concat((value ?? string.Empty).Trim().ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-");
        normalized = normalized.Trim('-');
        if (normalized.Length is < 1 or > 50)
            throw new InvalidOperationException($"A valid {label} code is required by the engineering document naming policy.");
        return normalized;
    }
}
