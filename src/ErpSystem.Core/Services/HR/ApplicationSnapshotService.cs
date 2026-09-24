using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Pure, I/O-free builder that freezes a candidate's scoreable profile into an
/// <see cref="ApplicationCandidateSnapshot"/> at the moment of application submission.
///
/// Register as a singleton — the service holds no state.
/// </summary>
public sealed class ApplicationSnapshotService : IApplicationSnapshotService
{
    /// <inheritdoc />
    public ApplicationCandidateSnapshot BuildSnapshot(
        JobCandidate candidate,
        int?         applicationYearsOfExperience)
    {
        var skills = candidate.Skills
            .Select(s => new SnapshotSkill
            {
                SkillName         = s.SkillName.Trim(),
                IsCertified       = s.IsCertified,
                CertificationName = s.CertificationName?.Trim(),
                YearsOfExperience = s.YearsOfExperience,
                Proficiency       = s.Proficiency.HasValue ? (int)s.Proficiency.Value : (int?)null,
                SkillId           = s.SkillId,
            })
            .ToList();

        var qualifications = candidate.Qualifications
            .Select(q =>
            {
                var display = (q.Qualification?.Name ?? q.QualificationFreeText ?? string.Empty).Trim();
                return new SnapshotQualification
                {
                    NormalisedName = display.ToLowerInvariant(),
                    DisplayName    = display,
                    Institution    = q.Institution.Trim(),
                    QualificationId = q.QualificationId,
                    // Round 4, lane Q: the EFFECTIVE rung, frozen with the rest — its own, or else
                    // the catalogue entry's (loaded with the name above).
                    QualificationLevelId = q.QualificationLevelId ?? q.Qualification?.QualificationLevelId,
                };
            })
            .Where(q => q.NormalisedName.Length > 0)
            .ToList();

        var languages = candidate.Languages
            .Select(l => new SnapshotLanguage
            {
                NormalisedName = l.LanguageName.Trim().ToLowerInvariant(),
                DisplayName    = l.LanguageName.Trim(),
                Proficiency    = (int)l.Proficiency,
                LanguageId     = l.LanguageId,
            })
            .Where(l => l.NormalisedName.Length > 0)
            .ToList();

        var workHistories = candidate.WorkHistories
            .Select(w => new SnapshotWorkHistory
            {
                InstitutionName = w.InstitutionName.Trim(),
                PositionHeld    = w.PositionHeld.Trim(),
                StartDate       = w.StartDate,
                EndDate         = w.EndDate,
            })
            .ToList();

        return new ApplicationCandidateSnapshot
        {
            SnapshotTakenAt      = DateTime.UtcNow,
            YearsOfExperience    = applicationYearsOfExperience,
            DateOfBirth          = candidate.DateOfBirth,
            Gender               = candidate.Gender,
            City                 = candidate.City?.Trim(),
            // Round 4, lane A. The path is taken from the navigation and is therefore null unless
            // the caller Included GeoArea — which JobCandidateRepository.GetWithFullDetailsAsync
            // now does. A null path is not a failure: the scorer resolves it from GeoAreaId against
            // the live tree instead, and only falls back to the free-text city when both are absent.
            GeoAreaId            = candidate.GeoAreaId,
            GeoAreaPath          = string.IsNullOrWhiteSpace(candidate.GeoArea?.Path)
                                       ? null
                                       : candidate.GeoArea!.Path,
            TotalYearsExperience = candidate.TotalYearsExperience,
            QualificationLevelsRecorded = true,   // round 4, lane Q
            Skills               = skills.AsReadOnly(),
            Qualifications       = qualifications.AsReadOnly(),
            Languages            = languages.AsReadOnly(),
            WorkHistories        = workHistories.AsReadOnly(),
        };
    }
}
