namespace ErpSystem.Core.Services.Estate;

public static class EstateLandDemarcationReference
{
    private const string PortionSeparator = "-PORTION-";

    public static string Build(string assetCode, int demarcationNumber)
        => $"{assetCode.Trim()}{PortionSeparator}{demarcationNumber:D3}";

    public static string DisplayReference(
        string? childFixedAssetReference,
        string assetCode,
        int demarcationNumber)
        => string.IsNullOrWhiteSpace(childFixedAssetReference)
            ? $"{assetCode.Trim()}-D{demarcationNumber:000}"
            : childFixedAssetReference.Trim();

    public static bool TryParse(
        string? landReference,
        out string assetCode,
        out int demarcationNumber)
    {
        assetCode = string.Empty;
        demarcationNumber = 0;
        if (string.IsNullOrWhiteSpace(landReference))
        {
            return false;
        }

        var normalized = landReference.Trim();
        var separatorIndex = normalized.LastIndexOf(
            PortionSeparator,
            StringComparison.OrdinalIgnoreCase);
        var separatorLength = PortionSeparator.Length;
        if (separatorIndex <= 0)
        {
            separatorIndex = normalized.LastIndexOf("-D", StringComparison.OrdinalIgnoreCase);
            separatorLength = 2;
            if (separatorIndex <= 0)
            {
                return false;
            }
        }

        var numberText = normalized[(separatorIndex + separatorLength)..];
        if (!int.TryParse(numberText, out demarcationNumber)
            || demarcationNumber <= 0)
        {
            demarcationNumber = 0;
            return false;
        }

        assetCode = normalized[..separatorIndex];
        return true;
    }
}
