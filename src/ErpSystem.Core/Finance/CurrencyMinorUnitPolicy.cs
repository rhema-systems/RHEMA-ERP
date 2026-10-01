namespace ErpSystem.Core.Finance;

/// <summary>
/// Authoritative Finance accounting-boundary policy for ISO 4217 minor units.
/// Monetary amounts are rounded only when they enter a currency-denominated ledger;
/// exchange rates and percentages keep their separate domain precision.
/// </summary>
public static class CurrencyMinorUnitPolicy
{
    public const int MaximumDecimalPlaces = 4;

    public static int? ExpectedDecimalPlaces(string? currencyCode) =>
        currencyCode?.Trim().ToUpperInvariant() switch
        {
            "JPY" or "KRW" or "VND" or "UGX" or "RWF" or "XAF" or "XOF" or "CLP" => 0,
            "BHD" or "KWD" or "OMR" => 3,
            "AED" or "ARS" or "AUD" or "BDT" or "BRL" or "CAD" or "CHF" or "CNY"
                or "COP" or "CZK" or "DKK" or "EGP" or "ETB" or "EUR" or "GBP" or "GHS"
                or "HKD" or "HUF" or "IDR" or "ILS" or "INR" or "KES" or "LKR" or "MAD"
                or "MXN" or "MYR" or "NGN" or "NOK" or "NZD" or "PEN" or "PHP" or "PKR"
                or "PLN" or "QAR" or "RON" or "RUB" or "SAR" or "SEK" or "SGD" or "THB"
                or "TRY" or "TZS" or "USD" or "ZAR" => 2,
            _ => null
        };

    public static void Validate(string currencyCode, int decimalPlaces)
    {
        if (decimalPlaces is < 0 or > MaximumDecimalPlaces)
            throw new InvalidOperationException($"Currency decimal places must be between 0 and {MaximumDecimalPlaces}.");

        var expected = ExpectedDecimalPlaces(currencyCode);
        if (expected.HasValue && decimalPlaces != expected.Value)
            throw new InvalidOperationException(
                $"{currencyCode.Trim().ToUpperInvariant()} uses {expected.Value} decimal place(s) under ISO 4217; the currency precision cannot be overridden.");
    }

    public static decimal MinorUnit(int decimalPlaces) => decimalPlaces switch
    {
        0 => 1m,
        1 => 0.1m,
        2 => 0.01m,
        3 => 0.001m,
        4 => 0.0001m,
        _ => throw new InvalidOperationException($"Currency decimal places must be between 0 and {MaximumDecimalPlaces}.")
    };

    public static decimal Round(decimal amount, int decimalPlaces)
    {
        _ = MinorUnit(decimalPlaces);
        return decimal.Round(amount, decimalPlaces, MidpointRounding.AwayFromZero);
    }
}
