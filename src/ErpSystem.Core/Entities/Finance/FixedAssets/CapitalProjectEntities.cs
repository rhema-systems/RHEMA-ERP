using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    // ── Enums ────────────────────────────────────────────────────────────

    public enum ProjectStatus
    {
        [Display(Name = "Planning")]
        Planning = 1,

        [Display(Name = "In Progress")]
        InProgress = 2,

        [Display(Name = "On Hold")]
        OnHold = 3,

        [Display(Name = "Completed")]
        Completed = 4,

        [Display(Name = "Cancelled")]
        Cancelled = 5,

        [Display(Name = "Pending Approval")]
        PendingApproval = 6,

        [Display(Name = "Approved")]
        Approved = 7,

        [Display(Name = "Rejected")]
        Rejected = 8
    }

    public enum ProjectCostSourceType
    {
        [Display(Name = "Vendor Invoice")]
        VendorInvoice = 1,

        [Display(Name = "Expense Record")]
        ExpenseRecord = 2,

        [Display(Name = "Inventory Dispatch")]
        InventoryDispatch = 3,

        [Display(Name = "Manual Journal")]
        ManualJournal = 4
    }

    // ── Capital Project ──────────────────────────────────────────────────

    /// <summary>
    /// Assets Under Construction (AUC / CIP) — tracks costs accumulated
    /// during a capital project until capitalization into fixed assets.
    /// </summary>
    public class CapitalProject : TenantEntity
    {
        [Required]
        [MaxLength(50)]
        public string ProjectCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? TargetCompletionDate { get; set; }
        public DateTime? ActualCompletionDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalBudgetAmount { get; set; }

        /// <summary>
        /// Sum of all ProjectCostLine amounts — updated on PostCost / RemoveCost
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAccumulatedCost { get; set; }

        /// <summary>
        /// Total value transferred to fixed assets on capitalization
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CapitalizedAmount { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        // Exact, server-owned approval and posting authority for the current settlement cycle.
        public Guid? CapitalizationWorkflowInstanceId { get; set; }
        public Guid? CapitalizationSourceBookAuthorityId { get; set; }
        [MaxLength(64)] public string? CapitalizationEvidenceHash { get; set; }
        public virtual WorkflowInstance? CapitalizationWorkflowInstance { get; set; }
        public virtual FinanceSourceBookAuthority? CapitalizationSourceBookAuthority { get; set; }

        // ── Concurrency ──
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        // ── Navigation ──
        public virtual ICollection<ProjectCostLine> CostLines { get; set; } = new List<ProjectCostLine>();
        public virtual ICollection<ProjectSettlementRule> SettlementRules { get; set; } = new List<ProjectSettlementRule>();
    }

    // ── Project Cost Line ────────────────────────────────────────────────

    /// <summary>
    /// Individual cost accumulation entry — tracking only, no GL posting.
    /// The source document (AP invoice, expense record, etc.) handles its own GL.
    /// </summary>
    public class ProjectCostLine : TenantEntity
    {
        [Required]
        public Guid CapitalProjectId { get; set; }
        public virtual CapitalProject CapitalProject { get; set; } = null!;

        public ProjectCostSourceType SourceDocumentType { get; set; }

        /// <summary>
        /// FK to the originating document (e.g. VendorInvoice.Id) — nullable for manual entries
        /// </summary>
        public Guid? SourceDocumentId { get; set; }
        public Guid? SourceFinancePostingEventId { get; set; }
        public Guid? SourceBookAuthorityId { get; set; }
        public virtual FinancePostingEvent? SourceFinancePostingEvent { get; set; }
        public virtual FinanceSourceBookAuthority? SourceBookAuthority { get; set; }

        [MaxLength(100)]
        public string? SourceDocumentReference { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    // ── Project Settlement Rule ──────────────────────────────────────────

    /// <summary>
    /// Defines how accumulated project costs are allocated to fixed asset
    /// categories when the project is capitalized. Total allocation % must = 100.
    /// </summary>
    public class ProjectSettlementRule : TenantEntity
    {
        [Required]
        public Guid CapitalProjectId { get; set; }
        public virtual CapitalProject CapitalProject { get; set; } = null!;

        [Required]
        public Guid TargetFixedAssetCategoryId { get; set; }
        public virtual FixedAssetCategory TargetFixedAssetCategory { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string ProposedAssetName { get; set; } = string.Empty;

        /// <summary>
        /// Allocation percentage (0–100). All rules for a project must sum to 100.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal AllocationPercentage { get; set; }

        /// <summary>
        /// Populated after capitalization: the actual amount allocated
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal AllocatedAmount { get; set; }

        /// <summary>
        /// The FixedAsset created during capitalization
        /// </summary>
        public Guid? ResultingFixedAssetId { get; set; }
        public virtual FixedAsset? ResultingFixedAsset { get; set; }
    }

    /// <summary>
    /// Durable evidence for one direct fixed-asset capitalization attempt. The cycle id is the
    /// workflow and posting source id; this prevents a later correction from reusing prior approval.
    /// </summary>
    public class FixedAssetCapitalizationCycle : TenantEntity
    {
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;
        public int CycleNumber { get; set; }
        public Guid? WorkflowInstanceId { get; set; }
        public virtual WorkflowInstance? WorkflowInstance { get; set; }
        public Guid? SourceBookAuthorityId { get; set; }
        public virtual FinanceSourceBookAuthority? SourceBookAuthority { get; set; }
        [MaxLength(64)] public string ApprovalEvidenceHash { get; set; } = string.Empty;
        [MaxLength(30)] public string Status { get; set; } = "Submitted";
        public DateTime EffectiveDate { get; set; }
        [MaxLength(3)] public string FunctionalCurrencyCode { get; set; } = string.Empty;
        [MaxLength(3)] public string TransactionCurrencyCode { get; set; } = string.Empty;
        public Guid DebitAccountId { get; set; }
        public Guid CreditAccountId { get; set; }
        [MaxLength(64)] public string DimensionEvidenceHash { get; set; } = string.Empty;
       public Guid? OriginalFinancePostingEventId { get; set; }
       public Guid? OriginalJournalEntryId { get; set; }
       public Guid? ReversalFinancePostingEventId { get; set; }
       public Guid? ReversalJournalEntryId { get; set; }
        public FinancePostingEvent? OriginalFinancePostingEvent { get; set; }
        public JournalEntry? OriginalJournalEntry { get; set; }
        public FinancePostingEvent? ReversalFinancePostingEvent { get; set; }
        public JournalEntry? ReversalJournalEntry { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
