using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// The tenant's <b>legal-employer master record</b> — the company identity, statutory numbers,
/// registered address, contacts and document-presentation details reused across generated documents
/// (offer letters, employment contracts, payslips, certificates, statutory reports) and email
/// letterheads.
///
/// <para><b>Cardinality:</b> exactly one active record per tenant. <c>ICompanyProfileProvider</c>
/// loads it (read-only) and, when no row exists yet, returns a transient instance populated from the
/// <c>Tenant</c> record and configuration so a fresh tenant still prints sensible defaults.</para>
///
/// <para><b>Scope discipline:</b> this holds company <i>identity / master data</i>. It deliberately
/// does NOT duplicate policy or locale knobs owned elsewhere — currency and fiscal-year live on
/// <c>CompanyHrPolicySettings</c>; brand colours / favicon live on <c>Tenant</c>.</para>
/// </summary>
public class CompanyProfile : TenantEntity
{
    // ═══════════════════════════════════════════
    //  LEGAL IDENTITY
    // ═══════════════════════════════════════════

    /// <summary>Registered legal name of the employer (appears on all formal documents).</summary>
    [MaxLength(200)]
    public string LegalName { get; set; } = string.Empty;

    /// <summary>Trading / brand name ("doing business as"), when different from the legal name.</summary>
    [MaxLength(200)]
    public string? TradingName { get; set; }

    public CompanyLegalForm LegalForm { get; set; } = CompanyLegalForm.LimitedCompany;

    /// <summary>Company registration / incorporation number.</summary>
    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    public DateTime? DateOfIncorporation { get; set; }

    /// <summary>Country where the company is legally incorporated (FK to the Country master).</summary>
    public Guid? CountryOfIncorporationId { get; set; }

    [ForeignKey(nameof(CountryOfIncorporationId))]
    public virtual Country? CountryOfIncorporation { get; set; }

    // ═══════════════════════════════════════════
    //  STATUTORY / TAX
    // ═══════════════════════════════════════════

    /// <summary>Tax Identification Number (TIN).</summary>
    [MaxLength(50)]
    public string? TaxIdentificationNumber { get; set; }

    [MaxLength(50)]
    public string? VatNumber { get; set; }

    /// <summary>Employer pension registration (e.g. Ghana SSNIT employer number).</summary>
    [MaxLength(50)]
    public string? SsnitEmployerNumber { get; set; }

    /// <summary>Any additional statutory registrations (GRA, NHIL, sector regulators…), free text.</summary>
    [MaxLength(500)]
    public string? OtherStatutoryRegistrations { get; set; }

    // ═══════════════════════════════════════════
    //  REGISTERED ADDRESS & CONTACT
    // ═══════════════════════════════════════════

    [MaxLength(500)]
    public string? RegisteredAddress { get; set; }

    /// <summary>Digital address (e.g. Ghana GhanaPostGPS code).</summary>
    [MaxLength(100)]
    public string? DigitalAddress { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    /// <summary>Country of the registered/duty address (FK to the Country master).</summary>
    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    public string? PhonePrimary { get; set; }

    [MaxLength(200)]
    public string? HrEmail { get; set; }

    [MaxLength(200)]
    public string? GeneralEmail { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    // ═══════════════════════════════════════════
    //  EMPLOYER / DOCUMENT PRESENTATION
    // ═══════════════════════════════════════════

    /// <summary>Default signatory printed on generated letters (overridable per document later).</summary>
    [MaxLength(200)]
    public string? DefaultSignatoryName { get; set; }

    [MaxLength(200)]
    public string? DefaultSignatoryTitle { get; set; }

    /// <summary>URL of a signature image to embed on generated letters (optional).</summary>
    [MaxLength(500)]
    public string? SignatureImageUrl { get; set; }

    /// <summary>URL of a company seal / stamp image (optional).</summary>
    [MaxLength(500)]
    public string? CompanySealImageUrl { get; set; }

    /// <summary>Logo used on document letterheads. Falls back to <c>Tenant.LogoUrl</c> when unset.</summary>
    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    /// <summary>Standard "how to accept" instructions merged into offer letters.</summary>
    [MaxLength(2000)]
    public string? OfferAcceptanceInstructions { get; set; }

    /// <summary>Footer line printed on generated documents (e.g. registered office + reg. number).</summary>
    [MaxLength(1000)]
    public string? DocumentFooterText { get; set; }
}
