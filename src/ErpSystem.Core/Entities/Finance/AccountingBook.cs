using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Tenant-level accounting book used for parallel reporting and postings.
    /// Examples: IFRS, Local Statutory, Management.
    /// </summary>
    public class AccountingBook : TenantEntity
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string Purpose { get; set; } = "Reporting";

        // PrimaryFull is the safe CLR compatibility default for legacy fixtures/rows; the C3
        // migration explicitly classifies every retained non-primary instance before enforcing it.
        public AccountingBookType BookType { get; set; } = AccountingBookType.PrimaryFull;

        public AccountingBookLifecycleStatus LifecycleStatus { get; set; } = AccountingBookLifecycleStatus.Draft;

        [MaxLength(3)]
        public string? FunctionalCurrencyCode { get; set; }

        public DateTime? EffectiveFromUtc { get; set; }

        public DateTime? EffectiveToUtc { get; set; }

        public Guid? BaseAccountingBookId { get; set; }

        public DateTime? InitializationStartedAtUtc { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; }

        public bool AllowsPosting { get; set; } = true;

        public bool IsSystemDefined { get; set; } = true;

        public int SortOrder { get; set; }

        public AccountingBookLifecycleStatus? PendingLifecycleStatus { get; set; }

        [MaxLength(500)]
        public string? PendingTransitionReason { get; set; }

        public Guid? TransitionRequestedByUserId { get; set; }

        public DateTime? TransitionRequestedAtUtc { get; set; }

        public Guid? TransitionWorkflowInstanceId { get; set; }

        public Guid? TransitionDecidedByUserId { get; set; }

        public DateTime? TransitionDecidedAtUtc { get; set; }

        [MaxLength(500)]
        public string? TransitionDecisionReason { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual AccountingBook? BaseAccountingBook { get; set; }
        public virtual ICollection<AccountingBook> DerivedBooks { get; set; } = new List<AccountingBook>();

        public virtual ICollection<AccountAccountingBook> AccountMappings { get; set; } = new List<AccountAccountingBook>();
        public virtual ICollection<AccountClassification> AccountClassifications { get; set; } = new List<AccountClassification>();
    }
}
