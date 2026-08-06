using System.Text;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Canonical contact handling for the public TDC supplier-applicant journey.
/// Ghana national numbers and their international equivalents resolve to the
/// same E.164 value so OTP delivery, active-application uniqueness and recovery
/// cannot disagree about the applicant contact.
/// </summary>
public static class SupplierApplicantContactNormalizer
{
    public const string DefaultCountryCallingCode = "233";

    public static string Normalize(
        ProcurementSupplierApplicantVerificationChannel channel,
        string? contact) =>
        channel == ProcurementSupplierApplicantVerificationChannel.Email
            ? (contact ?? string.Empty).Trim().ToLowerInvariant()
            : NormalizePhone(contact);

    public static IReadOnlyCollection<string> GetLookupAliases(
        ProcurementSupplierApplicantVerificationChannel channel,
        string normalizedContact)
    {
        if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
            return [normalizedContact];

        var aliases = new HashSet<string>(StringComparer.Ordinal)
        {
            normalizedContact
        };
        if (!normalizedContact.StartsWith('+'))
            return aliases;

        var internationalDigits = normalizedContact[1..];
        aliases.Add(internationalDigits);
        aliases.Add($"00{internationalDigits}");

        if (internationalDigits.StartsWith(DefaultCountryCallingCode,
                StringComparison.Ordinal))
        {
            var national = internationalDigits[DefaultCountryCallingCode.Length..];
            aliases.Add(national);
            aliases.Add($"0{national}");
            aliases.Add($"+{DefaultCountryCallingCode}0{national}");
        }

        return aliases;
    }

    private static string NormalizePhone(string? contact)
    {
        var value = (contact ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        var digitsBuilder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsDigit(character))
                digitsBuilder.Append(character);
        }

        var digits = digitsBuilder.ToString();
        if (digits.Length == 0)
            return string.Empty;

        if (value.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];

        var explicitlyInternational = value.StartsWith('+') ||
            value.StartsWith("00", StringComparison.Ordinal) ||
            digits.StartsWith(DefaultCountryCallingCode, StringComparison.Ordinal);

        if (explicitlyInternational)
        {
            if (digits.StartsWith($"{DefaultCountryCallingCode}0",
                    StringComparison.Ordinal))
            {
                digits = DefaultCountryCallingCode +
                    digits[(DefaultCountryCallingCode.Length + 1)..];
            }
            return $"+{digits}";
        }

        if (digits.StartsWith('0'))
            digits = digits[1..];
        return $"+{DefaultCountryCallingCode}{digits}";
    }
}
