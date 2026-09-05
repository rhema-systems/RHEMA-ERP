using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// HR letters an employee can ask for, and how HR issues them (area 25 slice 12b, D7).
/// </summary>
/// <remarks>
/// Two fulfilment routes on purpose: <see cref="IssueGeneratedAsync"/> renders the letter from
/// the HR-editable template and FREEZES it on the record, and the upload route
/// (<see cref="AttachIssuedFileAsync"/>) takes a signed scan for the letters that need one.
/// <see cref="PreviewAsync"/> lets HR read the letter before committing to it — issuing a
/// document nobody looked at first is how a wrong salary reaches a bank.
/// </remarks>
public interface IHrLetterRequestService
{
    Task<HrLetterRequestDto> CreateAsync(
        Guid employeeId, CreateHrLetterRequestDto dto, CancellationToken cancellationToken = default);

    Task<IEnumerable<HrLetterRequestDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Someone else's request is a lookup miss, not a refusal.</summary>
    Task<HrLetterRequestDto?> GetByIdAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken cancellationToken = default);

    Task<HrLetterRequestDto> CancelAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default);

    Task<IEnumerable<HrLetterRequestDto>> GetQueueAsync(
        HrLetterRequestStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Renders the letter WITHOUT issuing it, so HR can read it first.</summary>
    Task<HrLetterDocumentDto> PreviewAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renders the letter, freezes it on the request, and marks it issued.</summary>
    Task<HrLetterRequestDto> IssueGeneratedAsync(
        Guid id, Guid issuerEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Records an uploaded signed letter against the request and marks it issued.</summary>
    Task AttachIssuedFileAsync(
        Guid id, Guid issuerEmployeeId, Guid fileUploadRecordId, Guid? documentRecordId,
        Guid? documentVersionId, string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default);

    Task<HrLetterRequestDto> RejectAsync(
        Guid id, Guid issuerEmployeeId, RejectHrLetterRequestDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The frozen document, for the employee to read or print (or for HR to re-read). Returns
    /// null when the request is not the caller's, was fulfilled by upload, or is not issued.
    /// </summary>
    Task<HrLetterDocumentDto?> GetIssuedDocumentAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken cancellationToken = default);
}
