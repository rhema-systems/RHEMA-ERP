using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    // ── Lease Contract ───────────────────────────────────────────────────

    /// <summary>
    /// IFRS 16 / ASC 842 lease contract with ROU asset recognition.
    /// </summary>
    public class LeaseContract : TenantEntity
    {
        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        // ── Lessor (BusinessPartner) ──
        [Required]
        public Guid LessorId { get; set; }
        public virtual BusinessPartner Lessor { get; set; } = null!;

        // ── Contract dates ──
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // ── Payment terms ──
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyPaymentAmount { get; set; }

        [Required]
        public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;

        /// <summary>
        /// Incremental borrowing rate (annual, e.g. 0.08 = 8%)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(8,6)")]
        public decimal AnnualDiscountRate { get; set; }

        // ── Calculated values (set on creation / activation) ──

        /// <summary>
        /// Total number of payment periods
        /// </summary>
        public int TotalPeriods { get; set; }

        /// <summary>
        /// Present Value of the lease liability at inception
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PresentValue { get; set; }

        // ── Status ──
        public LeaseStatus Status { get; set; } = LeaseStatus.Draft;

        // ── ROU Asset link (set on activation) ──
        public Guid? RouAssetId { get; set; }
        public virtual FixedAsset? RouAsset { get; set; }

        // ── Concurrency ──
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        // ── Navigation ──
        public virtual ICollection<LeaseScheduleLine> ScheduleLines { get; set; } = new List<LeaseScheduleLine>();
    }

    // ── Lease Schedule Line ──────────────────────────────────────────────

    /// <summary>
    /// Single period in the lease amortization schedule.
    /// </summary>
    public class LeaseScheduleLine : TenantEntity
    {
        [Required]
        public Guid LeaseContractId { get; set; }
        public virtual LeaseContract LeaseContract { get; set; } = null!;

        public int PeriodNumber { get; set; }
        public DateTime PeriodDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaymentAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InterestExpense { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrincipalReduction { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingLiability { get; set; }

        /// <summary>
        /// Whether the period journal has been posted
        /// </summary>
        public bool IsPosted { get; set; }
    }
}
