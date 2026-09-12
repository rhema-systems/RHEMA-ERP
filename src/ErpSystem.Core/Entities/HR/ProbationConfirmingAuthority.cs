using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR.Recruitment;

/// <summary>
/// Who confirms probation for a given part of the organisation (FR-HR-032's "the head").
/// </summary>
/// <remarks>
/// <para><b>Why this exists as a table rather than a derivation.</b> FR-HR-032 routes the month-5
/// confirmation form to the head of the employee's unit, and FR-HR-140 notifies their supervisor.
/// Measured on the reference tenant 2026-08-18: <b>0 of 41</b> organisation units carry a
/// <c>HeadEmployeeId</c>, and <b>175 of 2,351</b> employees a <c>ManagerId</c> — 7%, and the count
/// has not moved while the workforce nearly doubled. Deriving authority from org structure would
/// therefore resolve to nobody for 93% of staff, which is the same wall that forced area 8's
/// establishment rule down to advisory and left area 9's <c>MinimumAuthority</c> rule correct and
/// unreachable. This is the fourth requirement to hit it; decision D-2 (2026-08-18) is to state the
/// authority explicitly instead of inferring it a fourth time.</para>
///
/// <para><b>Resolution is most-specific-wins</b>, so a tenant can start with one default row and
/// refine later without re-keying anything:</para>
/// <list type="number">
///   <item>this unit and this staff level</item>
///   <item>this unit, any level</item>
///   <item>any unit, this staff level</item>
///   <item>the tenant-wide default (both null)</item>
/// </list>
///
/// <para>⚠ If TDC later maintains <c>OrganizationUnit.HeadEmployeeId</c>, this table becomes the
/// override rather than the source, and resolution gains a step between (2) and (4). Nothing here
/// prevents that; it stops the feature waiting on data that may never arrive.</para>
/// </remarks>
public class ProbationConfirmingAuthority : TenantEntity
{
    /// <summary>The unit this rule covers. Null means every unit.</summary>
    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>The staff category this rule covers. Null means every category.</summary>
    public Guid? StaffLevelId { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel? StaffLevel { get; set; }

    /// <summary>The employee who confirms probations matching this rule.</summary>
    public Guid AuthorityEmployeeId { get; set; }

    [ForeignKey(nameof(AuthorityEmployeeId))]
    public virtual Employee AuthorityEmployee { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
