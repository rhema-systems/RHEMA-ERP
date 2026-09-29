using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// Which applications a test assignment reaches — the one rule, in one place.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane E6.</b> Extracted because a second reader arrived: the printed paper prints
/// one named paper per candidate an assignment reaches, and the online path decides who may start it.
/// Two private copies of "which statuses are live" is how the two would come to disagree — a paper
/// printed for a withdrawn candidate, or a live one left without a paper. Lane B moved the scoring
/// engine out of the services for exactly this reason.</para>
/// </remarks>
public static class RecruitmentTestReach
{
    /// <summary>
    /// The application statuses a VACANCY-WIDE test reaches.
    /// </summary>
    /// <remarks>
    /// ⚠ Not the withdrawn, rejected, hired or declined. Inviting a rejected candidate to sit an
    /// aptitude test — or printing them a paper — is the kind of mistake that ends up on social media.
    /// A per-application assignment is HR naming one candidate deliberately and is not filtered.
    /// </remarks>
    public static bool IsLive(ApplicationStatus status)
        => status is ApplicationStatus.New
                  or ApplicationStatus.Submitted
                  or ApplicationStatus.UnderReview
                  or ApplicationStatus.Shortlisted
                  or ApplicationStatus.AssessmentPending
                  or ApplicationStatus.InterviewScheduled
                  or ApplicationStatus.InterviewCompleted
                  or ApplicationStatus.Waitlisted;

    /// <summary>Whether this assignment reaches this application.</summary>
    public static bool Reaches(RecruitmentTestAssignment assignment, JobApplication application)
        => assignment.JobApplicationId == application.Id
        || (assignment.JobVacancyId is { } vacancyId
            && application.JobVacancyId == vacancyId
            && IsLive(application.Status));
}
