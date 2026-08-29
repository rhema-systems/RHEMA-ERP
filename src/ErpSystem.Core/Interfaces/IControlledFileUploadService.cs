using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Stable category names for modules that use the shared controlled-upload
/// boundary. Categories in <see cref="SystemCleanScanRequired"/> cannot opt out
/// of a clean malware scan through tenant policy.
/// </summary>
public static class ControlledFileUploadCategories
{
    public const string SupplierRegistrationEvidence =
        "supplier-registration-evidence";

    public const string DocumentManagement =
        "document-management";

    public const string FinanceCloseEvidence =
        "finance-close-evidence";

    public const string ProcurementReceiptSourceEvidence =
        "procurement-receipt-source-evidence";

    public const string QuantitySurveyBoqImport =
        "quantity-survey-boq-import";

    public const string QuantitySurveyTenderBoqSubmission =
        "quantity-survey-tender-boq-submission";

    public const string QuantitySurveyPriceIndexImport =
        "quantity-survey-price-index-import";

    public const string QuantitySurveyEscalationDisputeEvidence =
        "quantity-survey-escalation-dispute-evidence";

    public const string QuantitySurveyMeasurementEvidence =
        "quantity-survey-measurement-evidence";

    public const string QuantitySurveyValuationEvidence =
        "quantity-survey-valuation-evidence";

    public const string QuantitySurveyPaymentCertificate =
        "quantity-survey-payment-certificate";

    public const string QuantitySurveyVariationEvidence =
        "quantity-survey-variation-evidence";

    public const string QuantitySurveyClaimEvidence =
        "quantity-survey-claim-evidence";

    public const string QuantitySurveyDayworkEvidence =
        "quantity-survey-daywork-evidence";

    public const string QuantitySurveySubcontractEvidence =
        "quantity-survey-subcontract-evidence";

    // HR document families. Every one of these is personal data — CVs, identity
    // documents, sick-note certificates, disciplinary evidence, medical exam
    // results — so they are all private (see LocalFileStorageService's "hr-"
    // prefix rule) and all scan-mandatory below.
    public const string HrCandidateCv = "hr-candidate-cv";
    public const string HrCandidateDocuments = "hr-candidate-documents";
    public const string HrCandidatePhotos = "hr-candidate-photos";
    public const string HrLeaveAttachments = "hr-leave-attachments";
    public const string HrPipAttachments = "hr-pip-attachments";
    public const string HrDisciplineDocuments = "hr-discipline-documents";
    public const string HrStaffMovementAttachments = "hr-staff-movement-attachments";
    public const string HrOfferLetters = "hr-offer-letters";
    public const string HrMedicalExamDocuments = "hr-medical-exam-documents";
    public const string HrMedicalClaimDocuments = "hr-medical-claim-documents";
    public const string HrMedicalInsuranceProviderDocuments = "hr-medical-insurance-provider-documents";
    public const string HrAppraisalAttachments = "hr-appraisal-attachments";
    public const string HrStaffTravelAttachments = "hr-staff-travel-attachments";

    /// <summary>
    /// Documents on a succession plan, a plan candidate or a talent-pool member — assessment
    /// reports, development plans, signed readiness reviews.
    /// </summary>
    /// <remarks>
    /// One category for all three owners because one table and one DTO serve all three, and
    /// because they are read by one permission family (<c>HrPermissions.Succession*</c>) rather
    /// than by the medical or discipline desks. A succession document names a person as a
    /// candidate to replace someone — frequently someone still in the post — so it is exactly the
    /// kind of HR file that must not be servable from a path a caller chose.
    /// </remarks>
    public const string HrSuccessionDocuments = "hr-succession-documents";

    /// <summary>
    /// Paperwork on an employee-relations case — the grievance statement as filed on paper, evidence
    /// gathered during an investigation, the investigation report itself, and FR-HR-181's final
    /// signed agreement.
    /// </summary>
    /// <remarks>
    /// Its own category rather than a share of the discipline pile, even though the two halves sit
    /// behind one permission family. A grievance is raised BY an employee and is usually ABOUT a
    /// colleague, so its documents name people who are not the subject of any case — and retention
    /// and access rules for "somebody's complaint about their manager" should not have to be
    /// expressed as "some of the documents in the disciplinary pile".
    /// </remarks>
    public const string HrGrievanceDocuments = "hr-grievance-documents";

    /// <summary>
    /// Signed oaths of secrecy (FR-HR-030). Its own category rather than a general HR bucket: an
    /// oath is a legal instrument naming one person, kept for the life of their employment, and
    /// retention and access rules that apply to it should not have to be expressed as "some of the
    /// documents in the HR pile".
    /// </summary>
    public const string HrOathOfSecrecyDocuments = "hr-oath-of-secrecy-documents";

    /// <summary>
    /// Paperwork attached to a separation — the resignation letter, the acceptance, the signed
    /// clearance form, the settlement statement, a medical report supporting a medical retirement,
    /// a death certificate.
    /// </summary>
    /// <remarks>
    /// Its own category rather than part of the HR pile, for the same reason as the oath: these
    /// are the evidence that someone's employment ended and that what they were owed was paid.
    /// They outlive the employment and are the first things asked for in a dispute, so retention
    /// and access rules for them should be stateable on their own.
    /// </remarks>
    public const string HrSeparationDocuments = "hr-separation-documents";

    /// <summary>
    /// Recruitment paperwork attached to a requisition, vacancy or advert — org charts, budget
    /// approvals, signed job descriptions, agency terms. Kept apart from
    /// <see cref="HrCandidateDocuments"/>: that is a named person's file, this is a role's, and the
    /// two have different audiences.
    /// </summary>
    public const string HrRecruitmentAttachments = "hr-recruitment-attachments";

    /// <summary>
    /// Paperwork filed against a company asset — the purchase invoice, the warranty certificate,
    /// the user manual, and photographs of its condition when issued or taken back (area 16).
    /// </summary>
    /// <remarks>
    /// Its own category rather than part of the HR pile because most of it is not personal data at
    /// all — an invoice for a laptop is a procurement record — while a photograph of damage taken
    /// at a return is evidence in a charge against a named employee. Keeping them together under
    /// one asset-scoped category lets retention follow the ASSET, which is how anybody looking for
    /// them would ask. The <c>hr-</c> prefix keeps the store private either way.
    /// </remarks>
    public const string HrAssetDocuments = "hr-asset-documents";

    /// <summary>
    /// Evidence behind a pre-employment check on a conditional offer — police clearance
    /// certificates, medical reports, academic transcripts, written references.
    ///
    /// <para>Deliberately its own category rather than sharing
    /// <see cref="HrCandidateDocuments"/>: these are third-party verification results about a named
    /// person, frequently the most sensitive documents recruitment ever holds, and keeping them
    /// separate means a retention or access policy can be set for them alone.</para>
    /// </summary>
    public const string HrPreEmploymentDocuments = "hr-pre-employment-documents";

    /// <summary>
    /// Versions of SHE controlled documents (policies, procedures, emergency
    /// plans, …) on the area's register. Not personal data, but the register is
    /// HR-role-gated and the <c>hr-</c> prefix keeps the files on the private
    /// storage tree rather than the public web root.
    /// </summary>
    public const string HrSheControlledDocuments = "hr-she-controlled-documents";

    /// <summary>
    /// Evidence an employee attaches to a personal-data change request — a marriage
    /// certificate behind a name change, a bank letter behind an account change, a Ghana Card
    /// behind a date-of-birth correction (area 25 slice 12).
    /// </summary>
    public const string HrProfileChangeEvidence = "hr-profile-change-evidence";

    /// <summary>
    /// A signed HR letter uploaded to fulfil an employee's letter request, where a generated
    /// document will not do — a wet signature, a stamp, or an embassy's own form
    /// (area 25 slice 12b).
    /// </summary>
    public const string HrLetterDocuments = "hr-letter-documents";

    /// <summary>
    /// A file attached to a staff announcement — the notice, form or circular everybody is
    /// being pointed at (area 25 slice 12c).
    /// </summary>
    public const string HrAnnouncementDocuments = "hr-announcement-documents";

    /// <summary>
    /// A company policy in the staff library — the document people are asked to read and, for
    /// some of them, to sign (area 25 slice 12d).
    /// </summary>
    public const string HrPolicyDocuments = "hr-policy-documents";

    /// <summary>
    /// Categories that cannot opt out of a clean malware scan through tenant policy.
    /// </summary>
    /// <remarks>
    /// Membership here is the ONLY thing that turns scanning on by default:
    /// <c>GetEffectivePolicyAsync</c> falls back to <c>RequireVirusScan = false</c>.
    /// It is also a hard prerequisite for central-DMS registration, because
    /// <c>CentralDocumentRepositoryFileService.RegisterAsync</c> rejects
    /// <c>Skipped</c> exactly as firmly as <c>Infected</c> — a category omitted here
    /// would pass the upload gate, fail DMS registration, and surface as a 500.
    /// </remarks>
    public static IReadOnlySet<string> SystemCleanScanRequired { get; } =
        new HashSet<string>(
            [
                SupplierRegistrationEvidence,
                DocumentManagement,
                FinanceCloseEvidence,
                ProcurementReceiptSourceEvidence,
                QuantitySurveyBoqImport,
                QuantitySurveyTenderBoqSubmission,
                QuantitySurveyPriceIndexImport,
                QuantitySurveyEscalationDisputeEvidence,
                QuantitySurveyMeasurementEvidence,
                QuantitySurveyValuationEvidence,
                QuantitySurveyPaymentCertificate,
                QuantitySurveyVariationEvidence,
                QuantitySurveyClaimEvidence,
                QuantitySurveyDayworkEvidence,
                QuantitySurveySubcontractEvidence,
                HrCandidateCv,
                HrCandidateDocuments,
                HrCandidatePhotos,
                HrLeaveAttachments,
                HrPipAttachments,
                HrDisciplineDocuments,
                // An employee-relations file holds a complaint about a named colleague, the
                // evidence gathered about them, and a signed agreement between the two. A tenant
                // policy should not be able to admit any of that unscanned.
                HrGrievanceDocuments,
                HrStaffMovementAttachments,
                HrOfferLetters,
                HrMedicalExamDocuments,
                HrMedicalClaimDocuments,
                // Registered here, not only declared above: a category absent from this set is
                // SKIPPED by the scanner, and the DMS then refuses to register a non-clean upload
                // — so the upload fails with an InvalidOperationException that names neither the
                // category nor the scan. Declaring the constant is half the job.
                HrMedicalInsuranceProviderDocuments,
                // Same reason again, one family further on: succession documents had the identical
                // caller-supplied-path defect (D-14) and are registered here in the same commit
                // that gives them an upload route, so the D-11 half-job cannot recur.
                HrSuccessionDocuments,
                HrAppraisalAttachments,
                // A travel attachment is a passport scan, a visa letter or an invitation carrying
                // a name, a number and an address. A tenant policy should not be able to permit
                // one of those unscanned.
                HrStaffTravelAttachments,
                HrOathOfSecrecyDocuments,
                // A separation file holds a resignation letter, a medical report or a death
                // certificate. No tenant policy should be able to let one of those in unscanned.
                HrSeparationDocuments,
                HrRecruitmentAttachments,
                // ⚠ Scan-mandatory although an asset invoice is mundane. The same category carries
                // damage photographs taken at a return, which are evidence in a money claim against
                // an employee, and a tenant policy should not be able to let one of those in
                // unscanned just because most of its neighbours are receipts.
                HrAssetDocuments,
                HrPreEmploymentDocuments,
                HrSheControlledDocuments,
                // Identity documents by definition — this is the category whose whole purpose is
                // proving who someone is and where their money goes.
                HrProfileChangeEvidence,
                // A signed letter on company letterhead, handed to a bank or an embassy. If any
                // category should not be scan-optional by tenant policy, it is this one.
                HrLetterDocuments,
                // An announcement attachment is the one file in HR deliberately pushed at EVERY
                // employee at once, which makes it the worst possible thing to leave unscanned.
                HrAnnouncementDocuments,
                // A policy document is pushed at everyone AND signed for. A signature against an
                // unscanned file is the last thing anybody wants to have to explain.
                HrPolicyDocuments
            ],
            StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Well-known synthetic actors for controlled uploads that have no authenticated
/// user behind them.
/// </summary>
/// <remarks>
/// <see cref="IControlledFileUploadService.UploadAsync"/> rejects an empty
/// <c>ActorUserId</c>, and these flows genuinely have no user. Persisting a
/// synthetic id is safe because <c>FileUploadRecord.UploadedByUserId</c> and
/// <c>BaseEntity.CreatedById</c> carry no foreign key to the users table — the
/// DbContext configures indexes only. Pair each with a descriptive
/// <c>ActorName</c> so audit exports stay readable.
/// </remarks>
public static class ControlledFileUploadActors
{
    /// <summary>Unauthenticated caller on the public careers portal.</summary>
    public static readonly Guid PublicPortalAnonymous =
        new("00000000-0000-0000-0000-0000000000a1");

    /// <summary>The one-off HR legacy-file migration utility.</summary>
    public static readonly Guid LegacyFileMigration =
        new("00000000-0000-0000-0000-0000000000a2");

    /// <summary>Background sweeper reclaiming unclaimed public uploads.</summary>
    public static readonly Guid PublicUploadSweeper =
        new("00000000-0000-0000-0000-0000000000a3");
}

public interface IControlledFileUploadService
{
    Task<ControlledFileUploadResult> UploadAsync(
        ControlledFileUploadRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        Guid fileUploadRecordId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class ControlledFileUploadRequest
{
    public required Guid TenantId { get; init; }
    public required Guid ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public required string Category { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required Func<Stream> OpenReadStream { get; init; }
}

public sealed class ControlledFileUploadResult
{
    public required FileUploadRecord Record { get; init; }
    public required string ChecksumSha256 { get; init; }
    public required string PublicUrl { get; init; }
}

public sealed class ControlledFileUploadException(
    string code,
    string message,
    int statusCode = 422) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
