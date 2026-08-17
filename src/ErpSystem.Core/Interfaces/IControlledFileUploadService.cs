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
    public const string HrAppraisalAttachments = "hr-appraisal-attachments";
    public const string HrStaffTravelAttachments = "hr-staff-travel-attachments";

    /// <summary>
    /// Recruitment paperwork attached to a requisition, vacancy or advert — org charts, budget
    /// approvals, signed job descriptions, agency terms. Kept apart from
    /// <see cref="HrCandidateDocuments"/>: that is a named person's file, this is a role's, and the
    /// two have different audiences.
    /// </summary>
    public const string HrRecruitmentAttachments = "hr-recruitment-attachments";

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
                HrCandidateCv,
                HrCandidateDocuments,
                HrCandidatePhotos,
                HrLeaveAttachments,
                HrPipAttachments,
                HrDisciplineDocuments,
                HrStaffMovementAttachments,
                HrOfferLetters,
                HrMedicalExamDocuments,
                HrMedicalClaimDocuments,
                HrAppraisalAttachments,
                // A travel attachment is a passport scan, a visa letter or an invitation carrying
                // a name, a number and an address. A tenant policy should not be able to permit
                // one of those unscanned.
                HrStaffTravelAttachments,
                HrRecruitmentAttachments,
                HrPreEmploymentDocuments,
                HrSheControlledDocuments
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
