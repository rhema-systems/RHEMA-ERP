using ErpSystem.Core.DTOs.Documents;

namespace ErpSystem.Core.Interfaces.Documents;

/// <summary>
/// Coordinates the one-original/many-replacement issuance rule shared by Finance transaction
/// documents. Builders remain responsible for validating and rendering their canonical source;
/// this service owns copy sequencing, retained byte hashes, and the issuance audit.
/// </summary>
public interface IFinanceControlledDocumentIssueService
{
    Task<ControlledDocumentIssuePreparationDto> PrepareAsync(
        string documentType,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string documentNumber,
        string? requestedCopyType,
        string? replacementReason,
        CancellationToken cancellationToken = default);

    Task RecordIssuedAsync(
        ControlledDocumentIssuePreparationDto preparation,
        string fileName,
        string contentType,
        string contentSha256,
        string auditEventType,
        string sourceModule,
        string auditResource,
        Guid? journalEntryId,
        CancellationToken cancellationToken = default);

    Task<ControlledDocumentIssueSummaryDto> GetSummaryAsync(
        string documentType,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);
}
