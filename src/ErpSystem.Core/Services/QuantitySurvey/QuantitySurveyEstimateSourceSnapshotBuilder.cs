using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyEstimateSourceSnapshot(
    string? FundingSource,
    string? PropertyReference);

/// <summary>
/// Captures estimate source metadata from authoritative project and Estate records.
/// Browser requests never provide these values.
/// </summary>
public static class QuantitySurveyEstimateSourceSnapshotBuilder
{
    public const int CurrentSchemaVersion = 1;

    public static QuantitySurveyEstimateSourceSnapshot Capture(
        Project project,
        IEnumerable<EstateManagedAsset> projectAssets)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projectAssets);

        var fundingSource = Normalize(project.FundingSource, 500);
        var propertyReferences = projectAssets
            .Where(asset => asset.TenantId == project.TenantId
                            && asset.ProjectId == project.Id
                            && !asset.IsDeleted)
            .OrderBy(asset => asset.AssetCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.PropertyFileReference, StringComparer.OrdinalIgnoreCase)
            .Select(asset => Normalize(asset.PropertyFileReference, 120)
                             ?? Normalize(asset.AssetCode, 80))
            .Where(reference => reference is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new QuantitySurveyEstimateSourceSnapshot(
            fundingSource,
            Normalize(string.Join("; ", propertyReferences), 2000));
    }

    private static string? Normalize(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized[..Math.Min(normalized.Length, maximumLength)];
    }
}
