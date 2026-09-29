using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Who may read what the employee has entered on their self-evaluation (performance closure P12).
/// </summary>
/// <remarks>
/// The self-evaluation context is the employee's own form, and it carries whatever they have
/// saved — a draft included. It was readable in full by their manager and the HR desk at any
/// time, so a manager could watch a self-assessment being written, and read it where the profile's
/// <c>ShowSelfScoreToManager</c> says the manager does not see self scores at all.
/// <list type="bullet">
///   <item>The employee sees everything on their own form.</item>
///   <item>Nobody else sees a draft: the entries are withheld until the self-evaluation is
///         submitted.</item>
///   <item>Once it is submitted, HR sees it; the manager sees it only when the profile shows self
///         scores to the manager (the same rule lane B2 puts on the manager's own form).</item>
/// </list>
/// The form's structure — sections, items, targets, bands — is never withheld: it is the
/// template, not the employee's answer.
/// </remarks>
public static class SelfEvaluationView
{
    /// <param name="viewerEmployeeId">The caller's employee id; null when the account has none.</param>
    /// <param name="viewerIsDesk">The caller holds the performance Read policy (the HR desk).</param>
    public static SelfEvaluationContextDto ForViewer(SelfEvaluationContextDto dto, Guid? viewerEmployeeId, bool viewerIsDesk)
    {
        if (viewerEmployeeId is Guid me && me == dto.EmployeeId)
            return dto;

        var visible = dto.IsSelfEvaluationSubmitted
                      && (viewerIsDesk || dto.Settings?.ShowSelfScoreToManager != false);
        if (visible)
            return dto;

        foreach (var section in dto.Sections)
        {
            foreach (var item in section.Items)
            {
                item.ExistingCriterionScoreId = null;
                item.ExistingNumericScore = null;
                item.ExistingActualValue = null;
                item.ExistingNotes = null;
                item.ExistingEvidenceLinks = null;
                item.AchievementPercent = null;
                item.AchievedGrade = null;
            }

            foreach (var question in section.CustomQuestions)
                question.ExistingResponse = null;
        }

        // Nobody but the employee edits their own form.
        dto.IsEditable = false;
        return dto;
    }
}
