namespace ErpSystem.Core.Services.Estate;

public static class EstateLandDemarcationReference
{
    public static string Build(string assetCode, int demarcationNumber)
        => $"{assetCode.Trim()}-PORTION-{demarcationNumber:D3}";
}
