namespace ErpSystem.Core.Services.Estate;

public static class EstateLandDemarcationReference
{
    private const string PortionSeparator = "-PORTION-";

    public static string Build(string assetCode, int demarcationNumber)
        => $"{assetCode.Trim()}{PortionSeparator}{demarcationNumber:D3}";

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
        if (separatorIndex <= 0)
        {
            return false;
        }

        var numberText = normalized[(separatorIndex + PortionSeparator.Length)..];
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
