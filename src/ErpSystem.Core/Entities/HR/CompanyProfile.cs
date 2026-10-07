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

    /// <summary>
    /// The administrative area of the registered address. <see cref="Region"/> and
    /// <see cref="City"/> become display snapshots the service rewrites from the tree once this is
    /// set. See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

    [ForeignKey(nameof(GeoAreaId))]
    public virtual ErpSystem.Core.Entities.Reference.GeoArea? GeoArea { get; set; }

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

    /// <summary>
    /// ⚠ Retired (company-schedule final closure lane 4c, F-55): neither written nor read. The letterhead logo is an
    /// uploaded, versioned image (<c>CompanySealAssetKind.Logo</c>), else <c>Tenant.LogoUrl</c> —
    /// <c>ICompanyProfileProvider.GetLogoAsync</c>. The column stays until a later migration drops it.
    /// </summary>
    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    /// <summary>Standard "how to accept" instructions merged into offer letters.</summary>
    [MaxLength(2000)]
    public string? OfferAcceptanceInstructions { get; set; }

    /// <summary>Footer line printed on generated documents (e.g. registered office + reg. number).</summary>
    [MaxLength(1000)]
    public string? DocumentFooterText { get; set; }
}

/// <summary>
/// One seal or signature image the company has used, and the window it was current for.
/// </summary>
/// <remarks>
/// <para><b>Why a history table and not two columns.</b> A company seal is an instrument of
/// authority: whoever controls it can make a document look authentic. Overwriting one destroys the
/// answer to the question that matters after a compromise — <i>which documents carry the seal that
/// leaked?</i> Every image ever used is kept, with who uploaded it and when it was current, so that
/// question stays answerable.</para>
///
/// <para><b>Currency is derived, not stored.</b> The current asset of a kind is the one with no
/// <see cref="RetiredOn"/>. This follows the temporal idiom already in this module —
/// <c>EmployeeSalaryAssignment</c> closes the open row and inserts a new one rather than keeping an
/// <c>IsCurrent</c> flag — because a stored flag is a second source of truth that drifts from the
/// window it describes. Exactly one row per tenant per kind may be open; the service enforces that
/// with close-then-insert rather than a unique index, because the delete here is soft.</para>
///
/// <para>⚠ <b>The image is private.</b> It goes through the controlled upload gate and is registered
/// in the central DMS — unlike an avatar, a seal has real retention value. There is deliberately no
/// public URL: the legacy <c>CompanyProfile.SignatureImageUrl</c> and <c>CompanySealImageUrl</c>
/// were caller-supplied strings substituted straight into rendered letters, which made an
/// attacker-controlled value an image source in a document sent to a candidate. Those two survive
/// read-only so ported tenants keep rendering until they upload a real one.</para>
/// </remarks>
public class CompanySealAsset : TenantEntity
{
    /// <summary>Seal or signature. See <see cref="CompanySealAssetKind"/> for why one table.</summary>
    public CompanySealAssetKind Kind { get; set; }

    /// <summary>Scanned controlled upload holding the image.</summary>
    public Guid FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, because an instrument of authority is a retained document.</summary>
    public Guid? DocumentRecordId { get; set; }

    public Guid? DocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(150)]
    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>When this image became the one in use.</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// When it stopped being used. <c>null</c> means it is the current one.
    /// </summary>
    /// <remarks>
    /// ⚠ This is the currency test. Do not add an <c>IsCurrent</c> flag beside it — the module has
    /// already met the cost of that shape on <c>EmployeeContractDetail</c>, where the flag is
    /// hardcoded true at creation and updated by nothing.
    /// </remarks>
    public DateTime? RetiredOn { get; set; }

    /// <summary>Why it was retired — routine replacement, or a compromise worth recording.</summary>
    [MaxLength(500)]
    public string? RetiredReason { get; set; }
}
