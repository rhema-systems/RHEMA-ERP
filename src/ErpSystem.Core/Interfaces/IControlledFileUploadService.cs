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

    public const string QuantitySurveyBoqImport =
        "quantity-survey-boq-import";

    public const string QuantitySurveyTenderBoqSubmission =
        "quantity-survey-tender-boq-submission";

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
                QuantitySurveyBoqImport,
                QuantitySurveyTenderBoqSubmission,
                HrCandidateCv,
                HrCandidateDocuments,
                HrCandidatePhotos,
                HrLeaveAttachments,
                HrPipAttachments,
                HrDisciplineDocuments,
                HrStaffMovementAttachments,
                HrOfferLetters,
                HrMedicalExamDocuments
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
