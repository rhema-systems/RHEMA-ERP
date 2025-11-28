using System;
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
        public string CoaType { get; set; } = "Standard"; // Standard | Segmented

        /// <summary>
        /// Indicates if COA type is locked (accounts exist)
        /// </summary>
        public bool CoaConfigurationLocked { get; set; } = false;

        /// <summary>
        /// Base currency code for the tenant (e.g., GHS, USD)
        /// </summary>
        public string BaseCurrency { get; set; } = "GHS";

        /// <summary>
        /// Default Retained Earnings account for year-end close
        /// </summary>
        public Guid? RetainedEarningsAccountId { get; set; }

        /// <summary>
        /// Default Unrealized Gain/Loss account for currency revaluation
        /// </summary>
        public Guid? UnrealizedGainLossAccountId { get; set; }

        /// <summary>
        /// Default Realized Gain/Loss account for settled transactions
        /// </summary>
        public Guid? RealizedGainLossAccountId { get; set; }

        /// <summary>
        /// Default Suspense account for unbalanced entries
        /// </summary>
        public Guid? SuspenseAccountId { get; set; }

        // Navigation properties
        public virtual Account? RetainedEarningsAccount { get; set; }
        public virtual Account? UnrealizedGainLossAccount { get; set; }
        public virtual Account? RealizedGainLossAccount { get; set; }
        public virtual Account? SuspenseAccount { get; set; }
    }
}
