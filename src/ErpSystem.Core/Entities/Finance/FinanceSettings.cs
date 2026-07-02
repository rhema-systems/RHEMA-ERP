using System;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Finance module configuration settings at tenant level
    /// </summary>
    public class FinanceSettings : BusinessEntity
    {
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
        /// When enabled, opening balance postings auto-route balancing/offset lines
        /// to the Migration Clearing Account.
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
    }
}
