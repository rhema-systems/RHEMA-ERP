using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Who may read what the employee has entered on their self-evaluation (performance closure P12,
/// re-based on <see cref="AppraisalVisibility"/> in B2).
/// </summary>
/// <remarks>
/// The self-evaluation context is the employee's own form, and it carries whatever they have
/// saved — a draft included. It was readable in full by their manager and the HR desk at any
/// time, so a manager could watch a self-assessment being written.
/// <list type="bullet">
///   <item>The employee sees everything on their own form.</item>
///   <item>Nobody else sees a draft: the entries are withheld until the self-evaluation is
///         submitted.</item>
///   <item>Once it is submitted, HR sees it; the line manager sees it when the profile shows self
///         scores to the manager, or once they have submitted their own evaluation — the rule on the
///         manager's own form. (P12 first hid it from the manager for good when the switch was off;
///         the switch's contract is "until after they submit".)</item>
/// </list>
/// The form's structure — sections, items, targets, bands — is never withheld: it is the
/// template, not the employee's answer.
/// </remarks>
public static class SelfEvaluationView
{
    public static SelfEvaluationContextDto ForViewer(SelfEvaluationContextDto dto, AppraisalView view)
    {
        if (view.Reader == AppraisalReader.Appraisee)
            return dto;

        // Nobody but the employee edits their own form.
        dto.IsEditable = false;

        if (view.SelfEntries)
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

        // Submitted, but not this reader's to see yet — the screen says so rather than show it blank.
        dto.SelfEntriesWithheld = dto.IsSelfEvaluationSubmitted;
        return dto;
    }
}
