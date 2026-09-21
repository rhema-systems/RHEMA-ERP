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

        /// <summary>
        /// Optional Delta posting-window start. Primary and Parallel books never
        /// use lifecycle effective dates.
        /// </summary>
        public DateTime? EffectiveFromUtc { get; set; }

        /// <summary>Optional Delta posting-window end.</summary>
        public DateTime? EffectiveToUtc { get; set; }

        public Guid? BaseAccountingBookId { get; set; }

        /// <summary>
        /// First accounting date replicated into a Parallel book. This is
        /// operational cutoff evidence, not a lifecycle effective date.
        /// </summary>
        public DateTime? ReplicationStartDate { get; set; }

        public ParallelBookOpeningMode? ParallelOpeningMode { get; set; }

        public ParallelBookTranslationMethod? ParallelTranslationMethod { get; set; }

        /// <summary>Protected Parallel-only equity account for translation differences.</summary>
        public Guid? CurrencyTranslationReserveAccountId { get; set; }

        /// <summary>Protected Parallel-only account for immaterial precision residuals.</summary>
        public Guid? CurrencyRoundingAccountId { get; set; }

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

        public Guid? PrimaryReplacementFromBookId { get; set; }

        public DateTime? PrimaryReplacementEffectiveDate { get; set; }

        [MaxLength(500)]
        public string? PrimaryReplacementReason { get; set; }

        public Guid? PrimaryReplacementRequestedByUserId { get; set; }

        public DateTime? PrimaryReplacementRequestedAtUtc { get; set; }

        public Guid? PrimaryReplacementWorkflowInstanceId { get; set; }

        public Guid? TransitionDecidedByUserId { get; set; }

        public DateTime? TransitionDecidedAtUtc { get; set; }

        [MaxLength(500)]
        public string? TransitionDecisionReason { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual AccountingBook? BaseAccountingBook { get; set; }
        public virtual ICollection<AccountingBook> DerivedBooks { get; set; } = new List<AccountingBook>();

        public virtual Account? CurrencyTranslationReserveAccount { get; set; }
        public virtual Account? CurrencyRoundingAccount { get; set; }

        public virtual ICollection<AccountAccountingBook> AccountMappings { get; set; } = new List<AccountAccountingBook>();
        public virtual ICollection<AccountClassification> AccountClassifications { get; set; } = new List<AccountClassification>();
        public virtual ICollection<AccountingBookPeriod> BookPeriods { get; set; } = new List<AccountingBookPeriod>();
        public virtual ICollection<AccountingBookInitialization> Initializations { get; set; } = new List<AccountingBookInitialization>();
    }

    /// <summary>Immutable evidence of an approved, effective-dated primary-book replacement.</summary>
    public sealed class AccountingBookPrimaryDesignation : TenantEntity
    {
        public Guid PreviousPrimaryBookId { get; set; }
        public Guid NewPrimaryBookId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        [MaxLength(500)] public string RequestReason { get; set; } = string.Empty;
        public Guid RequestedByUserId { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public Guid ApprovedByUserId { get; set; }
        public DateTime ApprovedAtUtc { get; set; }
        [MaxLength(500)] public string DecisionReason { get; set; } = string.Empty;
        public Guid? WorkflowInstanceId { get; set; }
        [MaxLength(500)] public string? ReversalReason { get; set; }
        public Guid? ReversalRequestedByUserId { get; set; }
        public DateTime? ReversalRequestedAtUtc { get; set; }
        public Guid? ReversalWorkflowInstanceId { get; set; }
        public Guid? ReversedByUserId { get; set; }
        public DateTime? ReversedAtUtc { get; set; }
        [MaxLength(500)] public string? ReversalDecisionReason { get; set; }

        public AccountingBook PreviousPrimaryBook { get; set; } = null!;
        public AccountingBook NewPrimaryBook { get; set; } = null!;
    }
}
