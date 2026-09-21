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
            TotalYearsExperience = candidate.TotalYearsExperience,
            Skills               = skills.AsReadOnly(),
            Qualifications       = qualifications.AsReadOnly(),
            Languages            = languages.AsReadOnly(),
            WorkHistories        = workHistories.AsReadOnly(),
        };
    }
}
