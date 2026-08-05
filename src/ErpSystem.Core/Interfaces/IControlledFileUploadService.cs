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

    public static IReadOnlySet<string> SystemCleanScanRequired { get; } =
        new HashSet<string>(
            [
                SupplierRegistrationEvidence,
                DocumentManagement,
                FinanceCloseEvidence
            ],
            StringComparer.OrdinalIgnoreCase);
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
