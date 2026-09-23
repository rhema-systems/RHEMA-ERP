using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// MY ONBOARDING — the self-service view (round 4, lane K-b2)
// ============================================================================
//
// Three readers, one page: the new hire (their own plan), anybody given an onboarding task (the tasks
// they owe, with a way to mark them done), and a buddy (whom they are helping). Deliberately narrower
// than HR's plan read — no comments, no evidence, nobody else's completion notes: those are HR's
// working record, and a task's notes can be about the new hire.

/// <summary>Everything onboarding asks of, or tells, the signed-in employee.</summary>
public class MyOnboardingDto
{
    /// <summary>Their own onboarding, as the new hire — the latest plan not cancelled. Null when none.</summary>
    public MyOnboardingPlanDto? MyPlan { get; set; }

    /// <summary>Tasks given to them on anybody's plan: open ones, and those done in the last 30 days.</summary>
    public List<MyOnboardingTaskDto> TasksAssignedToMe { get; set; } = new();

    /// <summary>The new hires they are onboarding buddy to, while those plans are open.</summary>
    public List<MyOnboardingBuddyDto> BuddyFor { get; set; } = new();
}

public class MyOnboardingPlanDto
{
    public Guid PlanId { get; set; }
    public OnboardingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly StartDate { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public string? CoordinatorName { get; set; }
    public string? BuddyName { get; set; }
    public int TasksTotal { get; set; }
    public int TasksDone { get; set; }
    public List<MyOnboardingPlanTaskDto> Tasks { get; set; } = new();
}

/// <summary>A task on the new hire's own plan — what is being done, by whom, and whether it is theirs.</summary>
public class MyOnboardingPlanTaskDto
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public OnboardingTaskCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public OnboardingTaskStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly DueDate { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>Who owes it: the person, else the post it belongs to, else nobody yet.</summary>
    public string? Who { get; set; }

    /// <summary>Given to the new hire themselves — it is also in their own task list.</summary>
    public bool IsMine { get; set; }
}

/// <summary>A task given to the signed-in employee.</summary>
public class MyOnboardingTaskDto
{
    public Guid TaskId { get; set; }
    public Guid PlanId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OnboardingTaskCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public OnboardingTaskStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly DueDate { get; set; }
    public bool IsMandatory { get; set; }
    public bool RequiresVerification { get; set; }
    public bool IsOverdue { get; set; }

    /// <summary>Their own onboarding (a task given to the new hire), or somebody else's.</summary>
    public bool ForMyOwnOnboarding { get; set; }
    public string? NewHireName { get; set; }
    public DateOnly NewHireStartDate { get; set; }
    public string? CoordinatorName { get; set; }
    public DateOnly? CompletedDate { get; set; }

    /// <summary>What they wrote when they marked it done — their own note, nobody else's.</summary>
    public string? CompletionNotes { get; set; }

    /// <summary>Whether Mark done is open to them now — and if not, why not.</summary>
    public bool CanMarkDone { get; set; }
    public string? CannotMarkDoneBecause { get; set; }
}

public class MyOnboardingBuddyDto
{
    public Guid PlanId { get; set; }
    public string? NewHireName { get; set; }
    public DateOnly StartDate { get; set; }
    public OnboardingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? CoordinatorName { get; set; }
}

public class CompleteMyOnboardingTaskDto
{
    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }
}
