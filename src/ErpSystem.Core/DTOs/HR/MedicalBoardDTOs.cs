using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// A medical board and its recommendation (residue plan G4 / R-15b).
/// </summary>
/// <remarks>
/// ⚠ <b>Medical-grade content.</b> <see cref="Findings"/>, <see cref="Recommendation"/> and the
/// sittings' notes describe somebody's health. Every endpoint returning this is gated on
/// <c>HR.Medical.*</c>, not on the HR role — the same treatment the SHE↔Medical boundary gave
/// occupational-health surveillance and return-to-work plans.
/// </remarks>
public class MedicalBoardDto
{
    public Guid Id { get; set; }
    public string BoardNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public MedicalBoardStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;

    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateOnly RequestedOn { get; set; }
    public DateOnly? ConvenedOn { get; set; }

    public Guid? HealthProfileId { get; set; }
    public Guid? BasedOnExamId { get; set; }
    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }

    public MedicalExamResult? Outcome { get; set; }
    public string? Findings { get; set; }
    public string? Recommendation { get; set; }
    public string? Restrictions { get; set; }
    public DateOnly? ReviewDueDate { get; set; }

    /// <summary>⚠ A recommendation, not an act — separation decides, this only says what was advised.</summary>
    public bool RecommendsMedicalRetirement { get; set; }

    public DateOnly? ConcludedOn { get; set; }
    public Guid? ConcludedById { get; set; }
    public string? ConcludedByName { get; set; }
    public string? CancellationReason { get; set; }

    public List<MedicalBoardMemberDto> Members { get; set; } = new();
    public List<MedicalBoardSittingDto> Sittings { get; set; } = new();
}

public class MedicalBoardMemberDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid? PhysicianId { get; set; }

    /// <summary>Set when the member works here — HR, a staff or union representative, a nurse.</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>The registered name when there is one, otherwise the name typed in.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Which of the three a member is, so a screen can show it without guessing.</summary>
    public string MemberKind { get; set; } = string.Empty;

    public string? Institution { get; set; }
    public MedicalBoardMemberRole Role { get; set; }
}

public class MedicalBoardSittingDto
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public DateOnly SittingDate { get; set; }
    public string? Venue { get; set; }
    public string? Notes { get; set; }
}

public class RequestMedicalBoardDto
{
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid? HealthProfileId { get; set; }
    public Guid? BasedOnExamId { get; set; }
    public Guid? FacilityId { get; set; }
}

/// <summary>
/// Appointing somebody to a board.
/// </summary>
/// <remarks>
/// <para>⚠ Supply <b>one</b> of <see cref="PhysicianId"/>, <see cref="EmployeeId"/> or
/// <see cref="MemberName"/>. A board is not only doctors — it carries HR as secretary, a union or
/// staff representative, and often a clinician from outside who is in nobody's register.</para>
///
/// <para>⚠ The subject of the board cannot be seated on it.</para>
/// </remarks>
public class AddMedicalBoardMemberDto
{
    public Guid? PhysicianId { get; set; }

    /// <summary>A member who works here.</summary>
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? MemberName { get; set; }

    [MaxLength(200)]
    public string? Institution { get; set; }

    public MedicalBoardMemberRole Role { get; set; } = MedicalBoardMemberRole.Member;
}

public class RecordMedicalBoardSittingDto
{
    public DateOnly SittingDate { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }
}

/// <summary>
/// The board reporting. This is the write that makes it count for anything.
/// </summary>
public class ConcludeMedicalBoardDto
{
    /// <summary>Required — a board that concludes without a finding has not concluded.</summary>
    public MedicalExamResult Outcome { get; set; }

    [MaxLength(4000)]
    public string? Findings { get; set; }

    [Required, MaxLength(4000)]
    public string Recommendation { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Restrictions { get; set; }

    public DateOnly? ReviewDueDate { get; set; }
    public bool RecommendsMedicalRetirement { get; set; }
}

public class MedicalBoardFilterDto
{
    public Guid? EmployeeId { get; set; }
    public MedicalBoardStatus? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Search { get; set; }
}
