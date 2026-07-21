using ErpSystem.Core.Entities.HR.Recruitment;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Builds an immutable <see cref="ApplicationCandidateSnapshot"/> from a live
/// <see cref="JobCandidate"/> and the application-form data at submission time.
///
/// The snapshot is a pure value computation — no I/O, no side effects.  Register
/// as a singleton.
/// </summary>
public interface IApplicationSnapshotService
{
    /// <summary>
    /// Produces a snapshot from the candidate's current in-memory state.
    /// The caller is responsible for ensuring <paramref name="candidate"/> has its
    /// navigation collections loaded (Skills, Qualifications, Languages, WorkHistories).
    /// </summary>
    /// <param name="candidate">Live candidate entity with collections populated.</param>
    /// <param name="applicationYearsOfExperience">
    /// YearsOfExperience submitted on the application form (may differ from the
    /// profile's <see cref="JobCandidate.TotalYearsExperience"/>).
    /// </param>
    ApplicationCandidateSnapshot BuildSnapshot(
        JobCandidate candidate,
        int?         applicationYearsOfExperience);
}
