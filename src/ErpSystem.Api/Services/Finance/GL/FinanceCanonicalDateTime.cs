using System.Globalization;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Canonical Finance timestamp representation used by immutable request fingerprints. SQL/EF
/// materializes datetime2 values as Unspecified, so those values retain their UTC wall-clock ticks;
/// Local values represent instants and are converted to the equivalent UTC instant.
/// </summary>
internal static class FinanceCanonicalDateTime
{
    public static string? Format(DateTime? value)
    {
        if (!value.HasValue) return null;
        var utc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
        return utc.ToString("O", CultureInfo.InvariantCulture);
    }
}
