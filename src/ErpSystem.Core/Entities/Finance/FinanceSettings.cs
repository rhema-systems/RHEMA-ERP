using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Finance module configuration settings at tenant level
    /// </summary>
    public class FinanceSettings : BusinessEntity
    {
        public Guid? ReturnToVendorClearingAccountId { get; set; }
        public Guid? PurchaseReturnVarianceAccountId { get; set; }
        /// <summary>
        /// Tenant ID (one settings record per tenant)
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Chart of Accounts type: Standard or Segmented
        /// Once accounts are created, this becomes locked
        /// </summary>
        public string CoaType { get; set; } = "Segmented"; // Segmented (Standard is deprecated)

        /// <summary>
        /// Indicates if COA type is locked (accounts exist)
        /// </summary>
        public bool CoaConfigurationLocked { get; set; } = false;

        /// <summary>
        /// Base currency code for the tenant (e.g., GHS, USD)
        /// </summary>
        public string BaseCurrency { get; set; } = "GHS";

        /// <summary>
        /// Tenant statutory WHT accumulation-year boundary. January 1 is the
        /// backward-compatible default and may differ from the accounting fiscal year.
        /// </summary>
        [Range(1, 12)]
        public int WhtStatutoryYearStartMonth { get; set; } = 1;

        [Range(1, 31)]
        public int WhtStatutoryYearStartDay { get; set; } = 1;

        /// <summary>
        /// Indicates the tenant functional currency is locked because accounting activity exists.
        /// Functional currency changes after this point require a controlled migration process.
        /// </summary>
        public bool FunctionalCurrencyLocked { get; set; } = false;

        /// <summary>
        /// Timestamp when the functional currency was locked.
        /// </summary>
        public DateTime? FunctionalCurrencyLockedAt { get; set; }

        /// <summary>
        /// Operational reason why the functional currency was locked.
        /// </summary>
        [MaxLength(500)]
        public string? FunctionalCurrencyLockedReason { get; set; }

        /// <summary>
        /// Character used to separate segments in the account number (e.g. "-", ".", "/")
        /// </summary>
        public string AccountSeparator { get; set; } = "-";

        /// <summary>
        /// Default Retained Earnings account for year-end close
        /// </summary>
        public Guid? RetainedEarningsAccountId { get; set; }

        /// <summary>
        /// Default Unrealized Gain/Loss account for currency revaluation
        /// </summary>
        public Guid? UnrealizedGainLossAccountId { get; set; }

        /// <summary>
        /// Default Unrealized FX Gain account for period-end revaluation postings.
        /// </summary>
        public Guid? UnrealizedFxGainAccountId { get; set; }

        /// <summary>
        /// Default Unrealized FX Loss account for period-end revaluation postings.
        /// </summary>
        public Guid? UnrealizedFxLossAccountId { get; set; }
        
        /// <summary>
        /// Account for realized FX gain/loss postings.
        /// </summary>
        public Guid? RealizedGainLossAccountId { get; set; }

        /// <summary>
        /// Default Realized FX Gain account for settled transactions
        /// </summary>
        public Guid? RealizedFxGainAccountId { get; set; }

        /// <summary>
        /// Default Realized FX Loss account for settled transactions
        /// </summary>
        public Guid? RealizedFxLossAccountId { get; set; }

        /// <summary>
        /// Default Suspense account for unbalanced entries
        /// </summary>
        public Guid? SuspenseAccountId { get; set; }

        /// <summary>
        /// Default Zero-Balance Clearing Account / Inter-segment Due-To/Due-From Account
        /// Used by the Document Splitting Engine to balance trial balances per segment.
        /// </summary>
        public Guid? SegmentClearingAccountId { get; set; }

        /// <summary>
        /// Controls which books automated subledger postings target.
        /// Values: "IFRS", "Management", "Local", "AllClassifiedBooks"
        /// Default: "IFRS"
        /// </summary>
        [MaxLength(30)]
        public string SubledgerPostingMode { get; set; } = "IFRS";

        // Navigation properties
        public virtual Account? RetainedEarningsAccount { get; set; }
        public virtual Account? UnrealizedGainLossAccount { get; set; }
        public virtual Account? UnrealizedFxGainAccount { get; set; }
        public virtual Account? UnrealizedFxLossAccount { get; set; }
        public virtual Account? RealizedFxGainAccount { get; set; }
        public virtual Account? RealizedFxLossAccount { get; set; }
        public virtual Account? SuspenseAccount { get; set; }

        /// <summary>
        /// Default Control Account for Accounts Receivable (AR)
        /// </summary>
        public Guid? ControlAccountArId { get; set; }
        public virtual Account? ControlAccountAr { get; set; }

        /// <summary>
        /// Default Control Account for Accounts Payable (AP)
        /// </summary>
        public Guid? ControlAccountApId { get; set; }
        public virtual Account? ControlAccountAp { get; set; }

        /// <summary>
        /// Asset account used when a posted vendor payment is not yet applied to a supplier invoice.
        /// Applying the advance later reclassifies it to AP control through the Finance posting engine.
        /// </summary>
        public Guid? SupplierAdvanceAccountId { get; set; }
        public virtual Account? SupplierAdvanceAccount { get; set; }

        /// <summary>
        /// Liability account used when a posted customer receipt is not yet applied to an AR invoice.
        /// Applying the advance later reclassifies it to AR control through the Finance posting engine.
        /// </summary>
        public Guid? CustomerAdvanceAccountId { get; set; }
        public virtual Account? CustomerAdvanceAccount { get; set; }

        /// <summary>
        /// Default Control Account for Inventory
        /// </summary>
        public Guid? ControlAccountInventoryId { get; set; }
        public virtual Account? ControlAccountInventory { get; set; }

        /// <summary>
        /// Default Control Account for Payroll/Salaries Payable
        /// </summary>
        public Guid? ControlAccountPayrollId { get; set; }
        public virtual Account? ControlAccountPayroll { get; set; }

        /// <summary>
        /// Default Control Account for Tax (VAT/GST/Sales Tax)
        /// </summary>
        public Guid? ControlAccountTaxId { get; set; }
        public virtual Account? ControlAccountTax { get; set; }

        public Guid? ControlAccountCOGSId { get; set; }
        public virtual Account? ControlAccountCOGS { get; set; }

        public Guid? ControlAccountGRVAccrualId { get; set; }
        public virtual Account? ControlAccountGRVAccrual { get; set; }

        public Guid? DefaultBankAccountId { get; set; }

        /// <summary>
        /// Enables directional Buy/Sell selection. Kept off during migration so
        /// tenants can load and approve directional rates before enforcing them.
        /// </summary>
        public bool DirectionalExchangeRatePolicyEnabled { get; set; }
        public ExchangeRateQuoteSide DefaultTransactionQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;
        public ExchangeRateQuoteSide ArInvoiceQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;
        public ExchangeRateQuoteSide ArSettlementQuoteSide { get; set; } = ExchangeRateQuoteSide.Buying;
        public ExchangeRateQuoteSide ApInvoiceQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;
        public ExchangeRateQuoteSide ApSettlementQuoteSide { get; set; } = ExchangeRateQuoteSide.Selling;
        public ExchangeRateQuoteSide ClosingQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;
        public bool RequireExchangeRateOverrideApproval { get; set; } = true;

        /// <summary>
        /// Banking and settlement controls. DepositIntact is the safe tenant default.
        /// </summary>
        public DepositPolicy BankDepositPolicy { get; set; } = DepositPolicy.DepositIntact;
        public bool RequireBankDepositPrimaryEvidence { get; set; } = true;
        public bool AutoPostBankDepositAfterConfirmation { get; set; } = true;
        public decimal? MaximumDepositDeductionAmount { get; set; }
        public decimal? MaximumDepositDeductionPercentage { get; set; }
        public int BankStatementMatchDateToleranceDays { get; set; } = 3;
        public int ChequeClearingPeriodDays { get; set; } = 3;

        /// <summary>
        /// Absolute physical-count variance above which the reviewer must explicitly confirm the
        /// exception. GHS 100 is the conservative TDC development baseline and remains configurable.
        /// </summary>
        public decimal CashTillVarianceApprovalThreshold { get; set; } = 100m;

        /// <summary>
        /// TDC's maker-checker baseline requires a user other than the cashier to close every till,
        /// including a zero-variance count. This avoids treating a clean count as self-certified.
        /// </summary>
        public bool RequireIndependentCashTillClosure { get; set; } = true;
        public Guid? ReturnedChequeBankChargeAccountId { get; set; }
        public virtual Account? ReturnedChequeBankChargeAccount { get; set; }
        public ReturnedChequeChargeTreatment DefaultReturnedChequeChargeTreatment { get; set; } =
            ReturnedChequeChargeTreatment.CustomerRecoverable;

        /// <summary>
        /// Contra-revenue / expense account debited when customer settlement discounts are allowed.
        /// </summary>
        public Guid? DiscountAllowedAccountId { get; set; }
        public virtual Account? DiscountAllowedAccount { get; set; }

        /// <summary>
        /// Other-income / contra-expense account credited when supplier settlement discounts are taken.
        /// </summary>
        public Guid? DiscountReceivedAccountId { get; set; }
        public virtual Account? DiscountReceivedAccount { get; set; }

        /// <summary>
        /// Migration/Opening Balance Clearing Account.
        /// Used during go-live to offset subledger opening balance entries.
        /// The balance of this account should be zero after migration is complete.
        /// This is distinct from SuspenseAccountId, which handles operational exceptions.
        /// </summary>
        public Guid? MigrationClearingAccountId { get; set; }
        public virtual Account? MigrationClearingAccount { get; set; }

        /// <summary>
        /// Retained for settings compatibility. The legacy opening-balance journal route remains
        /// retired; governed opening balances use their dedicated Finance workflow.
        /// </summary>
        public bool OpeningBalanceAutoRoutingEnabled { get; set; } = true;

        // ── Lease Accounting (IFRS 16) GL Defaults ──────────────────────

        /// <summary>
        /// Default ROU Asset account for lease activation journals
        /// </summary>
        public Guid? LeaseRouAssetAccountId { get; set; }
        public virtual Account? LeaseRouAssetAccount { get; set; }

        /// <summary>
        /// Default Lease Liability account for lease activation / period journals
        /// </summary>
        public Guid? LeaseLiabilityAccountId { get; set; }
        public virtual Account? LeaseLiabilityAccount { get; set; }

        /// <summary>
        /// Default Interest Expense account for lease period journals
        /// </summary>
        public Guid? LeaseInterestExpenseAccountId { get; set; }
        public virtual Account? LeaseInterestExpenseAccount { get; set; }

        // ── Subledger Journals Defaults ─────────────────────────────────
        
        /// <summary>
        /// Default expense account for bad debt / write-offs (AR)
        /// </summary>
        public Guid? WriteOffExpenseAccountId { get; set; }
        public virtual Account? WriteOffExpenseAccount { get; set; }

        /// <summary>
        /// Default income/recovery account for vendor write-offs (AP)
        /// </summary>
        public Guid? WriteOffRecoveryAccountId { get; set; }
        public virtual Account? WriteOffRecoveryAccount { get; set; }

        /// <summary>
        /// If true, subledger journals must go through the approval workflow before posting
        /// </summary>
        public bool RequireSubledgerJournalApproval { get; set; } = false;

        // ── Finance correction and data-scope controls ─────────────────────────────

        /// <summary>
        /// Governs which fiscal date is used by source-document reversals. TDC's safe default is
        /// the current open period so a correction does not rewrite a previously reported period.
        /// </summary>
        public FinanceReversalDatePolicy ReversalDatePolicy { get; set; } =
            FinanceReversalDatePolicy.CurrentOpenPeriod;

        /// <summary>
        /// Minimum narrative required for a posted Finance reversal. Twenty characters is long
        /// enough to discourage labels such as "error" while remaining practical for operations.
        /// </summary>
        public int MinimumReversalReasonLength { get; set; } = 20;

        /// <summary>
        /// Activates Finance data-scope enforcement after administrators have prepared grants.
        /// This is intentionally an explicit switch: it lets TDC configure and review assignments
        /// before fail-closed enforcement is enabled for non-administrators.
        /// </summary>
        public bool EnforceFinanceAccessScopes { get; set; } = false;

        /// <summary>
        /// Makes due or incomplete fixed-asset depreciation a mandatory period-close blocker.
        /// TDC defaults this to true for stronger month-end control, while the explicit setting
        /// resolves FIN-LIM-0034 by allowing an authorised tenant policy decision when needed.
        /// </summary>
        public bool RequireDepreciationBeforePeriodClose { get; set; } = true;

        // Precision and rounding domains are deliberately independent. Currency
        // minor units remain authoritative at the ledger boundary.
        [Range(0, PrecisionRoundingPolicy.MaximumUnitPriceDecimalPlaces)]
        public int UnitPriceDecimalPlaces { get; set; } = 4;

        [Range(PrecisionRoundingPolicy.MinimumExchangeRateDecimalPlaces, PrecisionRoundingPolicy.MaximumExchangeRateDecimalPlaces)]
        public int ExchangeRateInputDecimalPlaces { get; set; } = 10;

        [Range(PrecisionRoundingPolicy.MinimumExchangeRateDecimalPlaces, PrecisionRoundingPolicy.MaximumExchangeRateDecimalPlaces)]
        public int ExchangeRateDisplayDecimalPlaces { get; set; } = 6;

        [Range(0, PrecisionRoundingPolicy.MaximumPercentageDecimalPlaces)]
        public int TaxPercentageDecimalPlaces { get; set; } = 4;

        public GovernedRoundingMethod TaxRoundingMethod { get; set; } = GovernedRoundingMethod.Nearest;
        public TaxRoundingScope TaxRoundingScope { get; set; } = TaxRoundingScope.Line;

        [Column(TypeName = "decimal(18,6)")]
        public decimal? TaxRoundingIncrement { get; set; }

        public bool InvoiceRoundingEnabled { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? InvoiceRoundingIncrement { get; set; }

        public GovernedRoundingMethod InvoiceRoundingMethod { get; set; } = GovernedRoundingMethod.Nearest;
        public Guid? InvoiceRoundingGainAccountId { get; set; }
        public virtual Account? InvoiceRoundingGainAccount { get; set; }
        public Guid? InvoiceRoundingLossAccountId { get; set; }
        public virtual Account? InvoiceRoundingLossAccount { get; set; }

        [Column(TypeName = "decimal(20,4)")]
        public decimal SettlementToleranceAmount { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal SettlementTolerancePercentage { get; set; }

        [Range(0, CurrencyMinorUnitPolicy.MaximumDecimalPlaces)]
        public int ReportDisplayDecimalPlaces { get; set; } = 2;

        // Procurement/Finance invoice-match tolerances belong to the same tenant Finance policy.
        // Retaining them alongside the correction controls lets the incoming three-way-match
        // workflow and the Finance close/payment controls share one authoritative settings row.
        /// <summary>
        /// Maximum unit-price variance allowed for PO-linked AP invoice matching.
        /// </summary>
        [Range(typeof(decimal), "0", "100")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ApInvoicePriceTolerancePercent { get; set; } = 1m;

        /// <summary>
        /// Maximum cumulative invoice quantity over the accepted receipt quantity.
        /// </summary>
        [Range(typeof(decimal), "0", "100")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ApInvoiceQuantityTolerancePercent { get; set; } = 1m;
    }
}
