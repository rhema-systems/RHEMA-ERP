using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Employee documents, their shared vocabulary, and the position requirements that make the file
/// answerable rather than merely present.
/// </summary>
public interface IEmployeeDocumentService
{
    // Types — the vocabulary an employee's document and a position's requirement both speak.
    Task<IEnumerable<EmployeeDocumentTypeDto>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default);
    /// <summary>Seeds a starting vocabulary, skipping names the tenant already has. Safe to re-run.</summary>
    Task<int> SeedDefaultTypesAsync(CancellationToken ct = default);

    Task<EmployeeDocumentTypeDto> CreateTypeAsync(CreateEmployeeDocumentTypeDto dto, CancellationToken ct = default);
    Task<EmployeeDocumentTypeDto> UpdateTypeAsync(Guid id, UpdateEmployeeDocumentTypeDto dto, CancellationToken ct = default);
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // Documents.
    Task<IEnumerable<EmployeeDocumentDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<EmployeeDocumentDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The raw row, for the download path's entitlement check before anything is streamed.</summary>
    Task<EmployeeDocument?> GetEntityAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Records a document AFTER the controlled gate has scanned and registered its file.
    /// </summary>
    /// <remarks>
    /// ⚠ There is deliberately no JSON create. The gate's facts — the three DMS ids, the stored file
    /// name, the measured size — arrive as PARAMETERS, so a caller may describe what a file is and
    /// may never say where it lives.
    /// </remarks>
    Task<EmployeeDocumentDto> AttachAsync(
        Guid employeeId,
        Guid documentTypeId,
        string? title,
        string? description,
        DateOnly? issuedOn,
        DateOnly? expiresOn,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        string? fileName,
        string? mimeType,
        long? fileSizeBytes,
        Guid? uploadedByEmployeeId,
        CancellationToken ct = default);

    Task<EmployeeDocumentDto> UpdateAsync(Guid id, UpdateEmployeeDocumentDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Files that belong to a ROW rather than to the employee's general file: a guarantor's
    // photograph and a referee's written reference. One file per row, the oath-of-secrecy shape.
    Task<Employee> AttachEmployeePhotoAsync(Guid employeeId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default);
    Task<Employee?> GetEmployeeForPhotoAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDependent> AttachDependentPhotoAsync(Guid dependentId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default);
    Task<EmployeeDependent?> GetDependentForPhotoAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeGuarantor> AttachGuarantorPhotoAsync(Guid guarantorId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default);
    Task<EmployeeGuarantor?> GetGuarantorAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeReferee> AttachRefereeLetterAsync(Guid refereeId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default);
    Task<EmployeeReferee?> GetRefereeAsync(Guid id, CancellationToken ct = default);

    // Position requirements.
    Task<IEnumerable<PositionDocumentRequirementDto>> GetRequirementsAsync(Guid positionId, CancellationToken ct = default);
    Task<PositionDocumentRequirementDto> AddRequirementAsync(CreatePositionDocumentRequirementDto dto, CancellationToken ct = default);
    Task<PositionDocumentRequirementDto> UpdateRequirementAsync(Guid id, UpdatePositionDocumentRequirementDto dto, CancellationToken ct = default);
    Task DeleteRequirementAsync(Guid id, CancellationToken ct = default);

    /// <summary>What the employee's position requires, and whether they hold it.</summary>
    Task<EmployeeDocumentComplianceDto> GetComplianceAsync(Guid employeeId, CancellationToken ct = default);
}
