using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The exit register (area 9b) — separations of every type, from resignation to summary dismissal.
/// FRD §A1.10 and §3.A.2.
/// </summary>
/// <remarks>
/// Slice 1 covers the record itself: raise, amend while draft, read, cancel. Approval (FR-HR-092),
/// clearance (FR-HR-091/183), settlement (FR-HR-184/185) and the effect on the employee master
/// record each arrive as their own slice with their own rule, deliberately rather than as a status
/// field a client can set.
/// </remarks>
public interface ISeparationService
{
    Task<PagedResult<EmployeeSeparationListDto>> GetPagedAsync(
        EmployeeSeparationQueryDto query, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every separation on record for one employee, newest first. Usually zero or one.</summary>
    Task<IEnumerable<EmployeeSeparationListDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> CreateAsync(
        CreateEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> UpdateAsync(
        Guid id, UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> CancelAsync(
        Guid id, CancelEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move a draft separation into the approval queue, deriving whatever notice dates follow from
    /// what is already recorded. After this the notice facts are fixed, because the FR-HR-184
    /// settlement is computed from them.
    /// </summary>
    Task<EmployeeSeparationDetailDto> SubmitAsync(
        Guid id, SubmitEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<IEnumerable<EmployeeSeparationDocumentDto>> GetDocumentsAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a file already stored and scanned by the controlled-upload gate against a
    /// separation. The caller owns the upload; this owns the domain row.
    /// </summary>
    Task<EmployeeSeparationDocumentDto> AttachDocumentAsync(
        Guid separationId,
        SeparationDocumentCategory category,
        string fileName,
        string filePath,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        string? description,
        Guid? uploadedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>The document row, for serving the file back. Null when it is not this tenant's.</summary>
    Task<EmployeeSeparationDocument?> GetDocumentEntityAsync(
        Guid documentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
