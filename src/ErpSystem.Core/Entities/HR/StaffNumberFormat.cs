using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// How staff numbers are composed for one register of employees, for one tenant.
/// </summary>
/// <remarks>
/// <para><b>Why a table and not settings columns.</b> Staff numbering is per-REGISTER, not
/// per-company. One organisation numbers permanent staff as bare digits (<c>10482</c>) and contract
/// staff with a prefix (<c>ABC123</c>); the next uses <c>EMP/26/0417</c> for everyone; a third
/// numbers site labour separately again. Four flat columns on <c>CompanyHrPolicySettings</c> were
/// written first and could not express the first case, and adding a
/// <c>ContractStaffNumberPrefix</c> beside them would have hardcoded a two-register assumption into
/// a multi-tenant product.</para>
///
/// <para><b>Selection.</b> <see cref="AppliesToEmploymentType"/> chooses the rule;
/// <c>null</c> is the tenant's DEFAULT and catches everything without a rule of its own. A tenant
/// with one convention therefore needs exactly one row, not nine — the enum has nine members and
/// nobody wants to configure all of them to express "we do the same thing for everyone".</para>
///
/// <para>⚠ <b>The number does not follow a conversion.</b> A contract employee made permanent keeps
/// the number their register issued. It is an identity, referenced ~750 times across HR, Payroll,
/// Maintenance and Appraisal, and renumbering would orphan every one of those references. The
/// consequence, and it must be said plainly rather than discovered: <b>a staff number records which
/// register somebody ENTERED by, not what they are today.</b> Read employment type from
/// <c>Employee.EmploymentType</c>, never from the shape of the number.</para>
/// </remarks>
public class StaffNumberFormat : TenantEntity
{
    /// <summary>Human label for the register, e.g. "Permanent staff", "Contract staff".</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Which employment type this rule numbers. <c>null</c> is the tenant's default rule.
    /// </summary>
    /// <remarks>
    /// Exactly one row per tenant may have <c>null</c>, and exactly one row may claim any given
    /// employment type — enforced by <c>StaffNumberFormatService</c>, because a second rule for the
    /// same register makes "which number does this person get" unanswerable.
    /// </remarks>
    public EmploymentType? AppliesToEmploymentType { get; set; }

    // ── the format ──────────────────────────────────────────────────────────
    // Structured rather than a template string. A template ("{PREFIX}{YEAR}{SEQ:D4}") is more
    // flexible and needs a parser, a validator and an error message for every way it can be
    // malformed; these five fields cover 10482, ABC123, ABC-2026-0001 and EMP/26/0417, which is the
    // range actually asked for.

    /// <summary>Printed before everything else. Empty for a bare-digits register.</summary>
    [MaxLength(10)]
    public string Prefix { get; set; } = "";

    /// <summary>Placed between the parts, e.g. "-" or "/". Empty joins them directly.</summary>
    [MaxLength(3)]
    public string Separator { get; set; } = "";

    /// <summary>Whether the year is printed, and therefore whether the sequence resets annually.</summary>
    /// <remarks>
    /// ⚠ One switch, not two. A number that prints the year must reset annually or it climbs for
    /// ever; a number that does not print it must NOT reset, or January reissues last year's
    /// numbers. Separate switches would let a tenant choose the combination that collides.
    /// </remarks>
    public bool IncludeYear { get; set; }

    /// <summary>2 for "26", 4 for "2026". Ignored when <see cref="IncludeYear"/> is false.</summary>
    [Range(2, 4)]
    public int YearDigits { get; set; } = 4;

    /// <summary>Zero-padding width for the counter.</summary>
    [Range(1, 12)]
    public int SequenceDigits { get; set; } = 4;

    /// <summary>Printed after the counter. Rare, but some registers carry a trailing marker.</summary>
    [MaxLength(10)]
    public string Suffix { get; set; } = "";

    // ── behaviour ───────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the system issues the number, or HR types one in.
    /// </summary>
    /// <remarks>
    /// <para>Per register, not per tenant: an organisation can auto-number its permanent staff while
    /// accepting externally-issued numbers for contract labour that arrives with a number already.
    /// When true, a caller-supplied number is REFUSED rather than honoured — otherwise a hand-typed
    /// value can occupy a number the sequence is about to issue, and the collision surfaces later as
    /// a unique-index violation on somebody else's create.</para>
    ///
    /// <para>⚠ <b>There is deliberately NO global auto/manual switch.</b> The ABSENCE of a rule for
    /// a register means manual — HR types the number, and nothing needs configuring. A company-level
    /// toggle beside this flag would be a second source of truth for one fact: "global says auto but
    /// no rule exists" and "global says manual but the rule says auto" both have to resolve to
    /// something, and whatever we chose would be a rule nobody could predict. The same shape as a
    /// stored currency flag drifting from the window it describes.</para>
    ///
    /// <para>So the model is: <b>configure a format for the registers you want auto-numbered.</b> A
    /// tenant that has configured nothing gets manual entry everywhere, which is the right default —
    /// safer to ask for a number than to invent one in a format nobody chose.</para>
    /// </remarks>
    public bool AutoGenerate { get; set; } = true;

    /// <summary>
    /// The <c>INumberSequenceService</c> key this register counts on. Independent per register.
    /// </summary>
    /// <remarks>
    /// ⚠ Independent counters mean two registers can reach the same value. That is safe only while
    /// their FORMATS differ, because the uniqueness that actually matters is the filtered unique
    /// index on <c>(TenantId, EmployeeNumber)</c> across all registers. The service refuses two
    /// active rules that would compose identical output for the same counter value.
    /// </remarks>
    [Required]
    [MaxLength(30)]
    public string SequenceKey { get; set; } = "EMP";

    public bool IsActive { get; set; } = true;

    /// <summary>Composes a number for this register from a raw counter value.</summary>
    /// <remarks>
    /// Lives on the entity so the settings screen can show a live example of what the rule
    /// produces. A format nobody can preview is a format that gets discovered in production.
    /// </remarks>
    public string Compose(long sequence, int year)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);
        if (IncludeYear) parts.Add(YearDigits == 2 ? (year % 100).ToString("D2") : year.ToString("D4"));
        parts.Add(sequence.ToString("D" + SequenceDigits));
        if (!string.IsNullOrEmpty(Suffix)) parts.Add(Suffix);
        return string.Join(Separator, parts);
    }

    /// <summary>What this rule produces for counter 1, for a settings screen to display.</summary>
    public string Example(int year) => Compose(1, year);

    /// <summary>
    /// Reads the counter back out of a number this rule could have produced, for the given year.
    /// </summary>
    /// <remarks>
    /// <para>The exact inverse of <see cref="Compose"/>, and deliberately so: an import advances the
    /// counter past what it just loaded, and a loose "take the trailing digits" reader would learn
    /// the wrong lesson from a number some other system issued. Anything that does not fit the rule
    /// returns <c>false</c>, which means "nothing to learn from this one" — not an error. A data
    /// load must not fail because one legacy number was formatted differently.</para>
    ///
    /// <para>⚠ <b>The year is part of the match, not decoration.</b> Counters for a rule that prints
    /// the year are per-year buckets, so <c>EMP/25/0417</c> says nothing at all about where the 2026
    /// counter should stand — reading 417 out of it would push next January's numbering forward for
    /// no reason.</para>
    ///
    /// <para>Padding width is NOT checked. Registers loaded from elsewhere are full of numbers that
    /// are right but unpadded (<c>8072</c> where the rule prints <c>08072</c>), and refusing those
    /// would leave the counter behind precisely on the register that has the most numbers in it.</para>
    /// </remarks>
    public bool TryReadSequence(string? number, int year, out long sequence)
    {
        sequence = 0;
        var text = number?.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        if (!string.IsNullOrEmpty(Prefix))
        {
            if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
            text = text[Prefix.Length..];
            text = TrimLeadingSeparator(text);
        }

        if (IncludeYear)
        {
            var token = YearDigits == 2 ? (year % 100).ToString("D2") : year.ToString("D4");
            if (!text.StartsWith(token, StringComparison.Ordinal)) return false;
            text = text[token.Length..];
            text = TrimLeadingSeparator(text);
        }

        if (!string.IsNullOrEmpty(Suffix))
        {
            if (!text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)) return false;
            text = text[..^Suffix.Length];
            if (!string.IsNullOrEmpty(Separator) && text.EndsWith(Separator, StringComparison.Ordinal))
                text = text[..^Separator.Length];
        }

        if (text.Length == 0 || !text.All(char.IsAsciiDigit)) return false;
        return long.TryParse(text, out sequence);
    }

    private string TrimLeadingSeparator(string text) =>
        !string.IsNullOrEmpty(Separator) && text.StartsWith(Separator, StringComparison.Ordinal)
            ? text[Separator.Length..]
            : text;
}
