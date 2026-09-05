using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>Records oaths of secrecy (FRD FR-HR-030).</summary>
public interface IEmployeeOathOfSecrecyService
{
    Task<IEnumerable<EmployeeOathOfSecrecyDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Employees with no oath on record — what makes this a feature rather than a table.</summary>
    Task<IEnumerable<OathOutstandingEmployeeDto>> GetOutstandingAsync(CancellationToken cancellationToken = default);

    /// <summary>The employee's own affirmation. The actor comes from the token; the date from the server.</summary>
    Task<EmployeeOathOfSecrecyDto> AffirmAsync(AffirmOathOfSecrecyDto dto, Guid actorEmployeeId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>HR recording an oath sworn on paper, before a named witness.</summary>
    Task<EmployeeOathOfSecrecyDto> RecordAdministeredAsync(RecordAdministeredOathDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the scanned signed copy against an oath, after the controlled upload gate has stored
    /// it and the central DMS has registered it.
    /// </summary>
    /// <remarks>
    /// Called only by the upload endpoint, which is what guarantees the ids came from the gate.
    /// There is no path here and no way for a caller to supply an id of their own.
    /// </remarks>
    Task<EmployeeOathOfSecrecyDto> AttachScanAsync(
        Guid oathId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string fileName, string mimeType, long fileSizeBytes, CancellationToken cancellationToken = default);
}
