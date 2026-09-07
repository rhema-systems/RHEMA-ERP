namespace ErpSystem.Core.Services.Procurement;

/// <summary>Calculates expiry from an explicitly recorded term, never from an assumed policy default.</summary>
public static class ProcurementBidValidity
{
    public static DateTime Calculate(DateTime closingUtc, int days)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days), "Bid validity must be a positive number of calendar days.");
        // DateTime arithmetic is intentionally UTC; crossing local DST must not shorten a calendar-day term.
        return DateTime.SpecifyKind(closingUtc, DateTimeKind.Utc).AddDays(days);
    }
}
