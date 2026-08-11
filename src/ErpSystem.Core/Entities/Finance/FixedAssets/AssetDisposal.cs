using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    public class AssetDisposal : TenantEntity
    {
        [Required]
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;

        [Required]
        public DateTime DisposalDate { get; set; }

        public DateTime? AccountingDate { get; set; }

        public Guid? FiscalPeriodId { get; set; }
        public virtual FiscalPeriod? FiscalPeriod { get; set; }

        public Guid? AccountingBookId { get; set; }
        public virtual AccountingBook? AccountingBook { get; set; }

        [MaxLength(20)]
        public string BookClassification { get; set; } = "IFRS";

        [Required]
        public DisposalType DisposalType { get; set; } = DisposalType.Sale;

        [Required]
        public AssetDisposalStatus Status { get; set; } = AssetDisposalStatus.Draft;

        // FIN-LIM-0042: the approved scope and allocation evidence must remain immutable because
        // they determine both the amount derecognised and the carrying amount left in service.
        public AssetDisposalScope DisposalScope { get; set; } = AssetDisposalScope.WholeAsset;

        [Column(TypeName = "decimal(18,4)")]
        public decimal DisposedPortionPercent { get; set; } = 100m;

        [MaxLength(100)]
        public string? ComponentReference { get; set; }

        [MaxLength(500)]
        public string? ComponentDescription { get; set; }

        [MaxLength(200)]
        public string? AllocationEvidenceReference { get; set; }

        [MaxLength(1000)]
        public string? AllocationEvidenceNotes { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        // --- Financial Impact ---

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaleProceeds { get; set; } = 0; // Amount received (Cash/AR)

        [Column(TypeName = "decimal(18,2)")]
        public decimal DisposalCost { get; set; } = 0; // Costs to sell/remove

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetProceeds { get; set; } = 0;

        [MaxLength(3)]
        public string ProceedsCurrencyCode { get; set; } = "GHS";

        public Guid? ProceedsAccountId { get; set; }
        public virtual Account? ProceedsAccount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostAtDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AcquisitionCostAllocated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationAdjustmentAllocated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ResidualValueAllocated { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ProductionCapacityAllocated { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal AccumulatedProductionUnitsAllocated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciationAtDisposal { get; set; }

        // FIN-LIM-0039 evidence: disposal can occur between normal month-end runs, so the maker
        // approves the final charge and its proration basis together with the derecognition.
        // Persisting these values prevents a later policy or book change from silently altering the
        // accounting decision seen by the checker.
        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalDepreciationAmount { get; set; }

        public DateTime? FinalDepreciationFromDate { get; set; }
        public DateTime? FinalDepreciationToDate { get; set; }
        public int FinalDepreciationPeriodDays { get; set; }
        public int FinalDepreciationEligibleDays { get; set; }

        [MaxLength(30)]
        public string? FinalDepreciationProrationBasis { get; set; }

        public DepreciationMethod? FinalDepreciationMethodSnapshot { get; set; }
        public Guid? FinalDepreciationScheduleId { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal FinalDepreciationProductionUnits { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal FinalDepreciationDiminishingRatePercent { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal FinalDepreciationLifetimeProductionCapacity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal FinalDepreciationCumulativeProductionUnitsBefore { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal FinalDepreciationCumulativeProductionUnitsAfter { get; set; }

        [MaxLength(200)]
        public string? FinalDepreciationEvidenceReference { get; set; }

        [MaxLength(1000)]
        public string? FinalDepreciationEvidenceNotes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedImpairmentAtDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationSurplusAtDisposal { get; set; }

        // IAS 16 permits the asset-specific reserve remaining on derecognition to move directly
        // within equity. These account and amount snapshots preserve the exact TDC policy evidence
        // approved by the checker; the transfer is part of the disposal journal and never P&L.
        public Guid? RevaluationSurplusAccountId { get; set; }
        public virtual Account? RevaluationSurplusAccount { get; set; }

        public Guid? RetainedEarningsAccountId { get; set; }
        public virtual Account? RetainedEarningsAccount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationSurplusTransferAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBookValueAtDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GainOrLoss { get; set; } 
        // Calculated: (SaleProceeds - DisposalCost) - NetBookValueAtDisposal

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAcquisitionCostAfterDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAccumulatedDepreciationAfterDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingNetBookValueAfterDisposal { get; set; }

        [MaxLength(100)]
        public string? BuyerName { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        public Guid? RequestedById { get; set; }
        public virtual Employee? RequestedBy { get; set; }

        public Guid? ApprovedById { get; set; }
        public virtual Employee? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? PostedAt { get; set; }

        public DateTime? FailedAt { get; set; }

        [MaxLength(1000)]
        public string? Comments { get; set; }

        [MaxLength(1000)]
        public string? FailureReason { get; set; }

        public Guid? JournalEntryId { get; set; }
        public virtual JournalEntry? JournalEntry { get; set; }

        public Guid? PostingEventId { get; set; }
        public virtual FinancePostingEvent? PostingEvent { get; set; }

        public Guid? WorkflowInstanceId { get; set; }

        [MaxLength(150)]
        public string? IdempotencyKey { get; set; }
    }
}
