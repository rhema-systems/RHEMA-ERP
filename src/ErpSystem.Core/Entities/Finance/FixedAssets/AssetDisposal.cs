using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;

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

        // FIN-LIM-0043: SaleProceeds, DisposalCost and NetProceeds retain the commercial
        // transaction currency. These separate fields freeze the approved translation into the
        // tenant's functional currency so the GL, gain/loss calculation and audit trail never
        // reinterpret a foreign amount after the checker has approved it.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProceedsFunctionalAmount { get; set; }

        public Guid? ProceedsExchangeRateId { get; set; }
        public virtual ExchangeRate? ProceedsExchangeRate { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ProceedsExchangeRateValue { get; set; } = 1m;

        [MaxLength(100)]
        public string ProceedsExchangeRateSource { get; set; } = "Functional currency";

        public DateTime ProceedsExchangeRateDate { get; set; }
        public ExchangeRateType ProceedsExchangeRateType { get; set; } = ExchangeRateType.Daily;
        public ExchangeRateQuoteSide ProceedsExchangeRateQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;

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
        // Calculated in functional currency: ProceedsFunctionalAmount - NetBookValueAtDisposal

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAcquisitionCostAfterDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAccumulatedDepreciationAfterDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingNetBookValueAfterDisposal { get; set; }

        [MaxLength(100)]
        public string? BuyerName { get; set; }

        // FIN-LIM-0040: a sale must identify the canonical customer/business partner used by AR.
        // BuyerName remains a frozen display snapshot; this foreign key supplies durable master-
        // data identity and prevents a free-text buyer from becoming an untraceable receivable.
        public Guid? BuyerBusinessPartnerId { get; set; }
        public virtual BusinessPartner? BuyerBusinessPartner { get; set; }

        public AssetDisposalSettlementMode SettlementMode { get; set; } = AssetDisposalSettlementMode.NotApplicable;
        public AssetDisposalSettlementStatus SettlementStatus { get; set; } = AssetDisposalSettlementStatus.NotApplicable;

        // Tax configuration and treatment are frozen with the maker-checker request. Rates are
        // still resolved by the AR tax engine at completion so withdrawn or ineffective tax
        // configuration invalidates stale approval instead of silently using an old percentage.
        public Guid? SaleTaxGroupId { get; set; }
        public virtual TaxGroup? SaleTaxGroup { get; set; }
        public TaxTreatment SaleTaxTreatment { get; set; } = TaxTreatment.Standard;

        public Guid? SettlementPaymentTermId { get; set; }
        public virtual PaymentTerm? SettlementPaymentTerm { get; set; }
        public Guid? SettlementPaymentMethodId { get; set; }
        public virtual PaymentMethod? SettlementPaymentMethod { get; set; }
        public Guid? SettlementBankAccountId { get; set; }
        public virtual BankAccount? SettlementBankAccount { get; set; }
        public Guid? SettlementLiquidityAccountId { get; set; }
        public virtual LiquidityAccount? SettlementLiquidityAccount { get; set; }

        [MaxLength(100)]
        public string? SettlementReference { get; set; }

        // The linked AR documents are the statutory/customer-facing evidence. Their own journals
        // remain authoritative; these references let a reviewer traverse the complete asset-sale
        // chain without copying AR or Cash/Bank state into Fixed Assets.
        public Guid? CustomerInvoiceId { get; set; }
        public virtual Invoice? CustomerInvoice { get; set; }
        public Guid? CustomerPaymentId { get; set; }
        public virtual CustomerPayment? CustomerPayment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SettlementInvoiceAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SettlementTaxAmount { get; set; }

        public DateTime? SettlementCompletedAt { get; set; }

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
