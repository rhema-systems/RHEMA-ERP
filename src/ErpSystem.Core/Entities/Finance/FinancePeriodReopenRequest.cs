using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A controlled request to reopen one certified accounting period. The request deliberately
/// retains both the business justification and the exact affected-period validation seen by the
/// maker. Approval re-runs that validation so a reviewer cannot act on a stale downstream-period
/// picture after another close, lock, or reopen has occurred.
/// </summary>
public class FinancePeriodReopenRequest : TenantEntity
{
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// The completed numbered cycle whose certificate will be superseded if approval is granted.
    /// Requiring this relationship avoids an ungoverned reopen of a period with no signed close
    /// evidence; the application is pre-production, so no legacy bypass is necessary.
    /// </summary>
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FinancePeriodReopenStatuses.PendingApproval;

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The maker's explanation of the expected accounting/reporting impact and the correction
    /// that will be posted. TDC reviewers need this separately from the short reopen reason.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string AffectedPeriodAssessment { get; set; } = string.Empty;

    /// <summary>
    /// Immutable JSON snapshot of the fiscal-year state and every later active period at request
    /// time. It is evidence, not a live cache; the service builds a fresh snapshot at approval.
    /// </summary>
    [Required]
    public string ImpactSnapshotJson { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string ImpactFingerprint { get; set; } = string.Empty;

    public int AffectedPeriodCount { get; set; }

    public Guid RequestedByUserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequestedByUserName { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReviewedByUserId { get; set; }

    [MaxLength(200)]
    public string? ReviewedByUserName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(2000)]
    public string? ReviewComment { get; set; }

    /// <summary>
    /// Cycle N+1 is created in the same transaction as an approved reopen. Retaining its ID makes
    /// the request itself prove that the old certificate was not reused for the reopened books.
    /// </summary>
    public Guid? ResultingFinanceCloseCycleId { get; set; }

    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;
    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
    public virtual FinanceCloseCycle? ResultingFinanceCloseCycle { get; set; }
    public virtual ICollection<FinanceCloseAlertDelivery> AlertDeliveries { get; set; } = new List<FinanceCloseAlertDelivery>();
}

/// <summary>
/// Persisted decision vocabulary for period reopening. Approved means the period was reopened in
/// the same database transaction; there is intentionally no ambiguous "approved but not applied"
/// state that operations would need to reconcile later.
/// </summary>
public static class FinancePeriodReopenStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
