using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The employee's own profile, and the approval path for changing the parts of it that carry
/// identity or payment consequences (area 25 slice 12, decision D6).
/// </summary>
/// <remarks>
/// <para><b>The split this service exists to enforce.</b> Contact details are the employee's
/// own to correct; a name, a date of birth, a statutory number or a bank account is not.
/// <see cref="UpdateMyContactDetailsAsync"/> writes the first set immediately;
/// everything in <see cref="EmployeeProfileField"/> can only be reached through a request an
/// HR officer approves, and approval applies it.</para>
///
/// <para><b>Every method takes the acting employee explicitly</b> rather than reaching for the
/// token inside the service — the controller resolves the actor once, which is what makes the
/// self-or-HR boundary readable at the call site and testable without a request context.</para>
/// </remarks>
public interface IEmployeeProfileChangeService
{
    /// <summary>The employee's own profile — an explicit projection, never the desk's DTO.</summary>
    Task<MyProfileDto> GetMyProfileAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the low-risk contact fields directly. Null means "leave alone"; an empty string
    /// clears the field.
    /// </summary>
    Task<MyProfileDto> UpdateMyContactDetailsAsync(
        Guid employeeId, UpdateMyContactDetailsDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Files a change request. Refuses a value that cannot be parsed for its field, a bank
    /// field without an account of the employee's own, and a field that already has a pending
    /// request (a second one would race the first).
    /// </summary>
    Task<ProfileChangeRequestDto> CreateRequestAsync(
        Guid employeeId, CreateProfileChangeRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>The employee's own requests, newest first.</summary>
    Task<IEnumerable<ProfileChangeRequestDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One request. <paramref name="requestingEmployeeId"/> is the caller; a request belonging
    /// to somebody else is a lookup miss unless the caller holds the desk permission
    /// (<paramref name="isHrDesk"/>) — never a 403, which would confirm it exists.
    /// </summary>
    Task<ProfileChangeRequestDto?> GetByIdAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken cancellationToken = default);

    /// <summary>The employee withdraws their own request while it is still pending.</summary>
    Task<ProfileChangeRequestDto> CancelAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>The HR queue: pending first, then the answered history.</summary>
    Task<IEnumerable<ProfileChangeRequestDto>> GetQueueAsync(
        ProfileChangeRequestStatus? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the request AND writes the values onto the record in one transaction,
    /// re-running the same tenant-uniqueness rules the desk update enforces.
    /// </summary>
    Task<ProfileChangeRequestDto> ApproveAsync(
        Guid id, Guid reviewerEmployeeId, ReviewProfileChangeRequestDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Refuses it. A comment is required — the employee reads it back.</summary>
    Task<ProfileChangeRequestDto> RejectAsync(
        Guid id, Guid reviewerEmployeeId, ReviewProfileChangeRequestDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Records an uploaded evidence file against a pending request of the caller's.</summary>
    Task AttachEvidenceAsync(
        Guid id, Guid employeeId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default);
}
