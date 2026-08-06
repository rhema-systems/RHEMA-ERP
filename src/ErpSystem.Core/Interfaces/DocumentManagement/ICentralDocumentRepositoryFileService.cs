using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces.DocumentManagement;

/// <summary>
/// Shared module boundary for registering clean uploads in the central DMS and
/// opening their content through an authorized application endpoint.
/// </summary>
public interface ICentralDocumentRepositoryFileService
{
    Task<CentralDocumentRepositoryLink> RegisterAsync(
        CentralDocumentRepositoryRegistration request,
        CancellationToken cancellationToken = default);

    Task<CentralDocumentRepositoryContent?> OpenAsync(
        Guid tenantId,
        Guid documentRecordId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        Guid documentRecordId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class CentralDocumentRepositoryRegistration
{
    public required Guid TenantId { get; init; }
    public required Guid ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public required Guid FileUploadRecordId { get; init; }
    public required string SourceModule { get; init; }
    public required string SourceLabel { get; init; }
    public required string SourceEntityType { get; init; }
    public required Guid SourceRecordId { get; init; }
    public string? SourceRecordReference { get; init; }
    public required string Title { get; init; }
    public required string DocumentType { get; init; }
    public string? MetadataTemplateCode { get; init; }
    public string AccessProfile { get; init; } = "Module restricted";
    public string VersionNumber { get; init; } = "v1.0";
    public string VersionStatus { get; init; } = "Submitted";
    public string? ChangeSummary { get; init; }
    public string? Notes { get; init; }
    public bool RequirePublishedGovernance { get; init; }
    public IReadOnlyList<CentralDocumentMetadataRegistrationValue> MetadataValues { get; init; } =
        Array.Empty<CentralDocumentMetadataRegistrationValue>();
}

public sealed record CentralDocumentMetadataRegistrationValue(
    string FieldKey,
    string FieldLabel,
    string? FieldValue,
    string ValueType = "text");

public sealed class CentralDocumentRepositoryLink
{
    public required Guid DocumentRecordId { get; init; }
    public required Guid DocumentVersionId { get; init; }
    public required Guid FileUploadRecordId { get; init; }
    public required string DocumentReference { get; init; }
    public required string VersionNumber { get; init; }
}

public sealed class CentralDocumentRepositoryContent : IAsyncDisposable
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required FileUploadRecord UploadRecord { get; init; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
