using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Effective-dated Finance data scope assigned to one user inside one tenant.
///
/// Role/permission grants answer "may this user perform the action?". This record answers the
/// separate control question "on which Finance data may the user perform it?". Keeping those two
/// concerns separate prevents a broad global role grant from silently exposing every bank account
/// or Finance dimension in a tenant (the weakness recorded as FIN-LIM-0014).
/// </summary>
public class FinanceAccessScopeGrant : TenantEntity
{
    [Required]
    public Guid UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public FinanceAccessScopeType ScopeType { get; set; } = FinanceAccessScopeType.Tenant;

    /// <summary>
    /// Null only for a tenant-wide grant. GUID-backed dimensions are stored in canonical "N"
    /// format so comparisons are stable and case-independent; coded dimensions are normalized by
    /// the access-scope service before persistence.
    /// </summary>
    [MaxLength(100)]
    public string? ScopeValue { get; set; }

    public FinanceAccessLevel AccessLevel { get; set; } = FinanceAccessLevel.Read;

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Prevents one administrator from silently overwriting another administrator's scope change.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
