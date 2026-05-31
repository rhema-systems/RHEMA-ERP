using System;

namespace ErpSystem.Core.DTOs.Finance
{
    public class FinanceSettingsDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CoaType { get; set; } = "Standard";
        public bool CoaConfigurationLocked { get; set; }
        public string BaseCurrency { get; set; } = "GHS";
        public string BaseCurrencyName { get; set; } = "Ghana Cedi";
        public string BaseCurrencySymbol { get; set; } = "₵";
        public int BaseCurrencyDecimalPlaces { get; set; } = 2;
        public string AccountSeparator { get; set; } = "-";
        public Guid? RetainedEarningsAccountId { get; set; }
        public Guid? UnrealizedGainLossAccountId { get; set; }
        public Guid? RealizedGainLossAccountId { get; set; }
        public Guid? SuspenseAccountId { get; set; }
        public Guid? ControlAccountArId { get; set; }
        public Guid? ControlAccountApId { get; set; }
        public Guid? ControlAccountInventoryId { get; set; }
        public Guid? ControlAccountPayrollId { get; set; }
        public Guid? ControlAccountTaxId { get; set; }
        
        // Subledger Settings
        public Guid? WriteOffExpenseAccountId { get; set; }
        public Guid? WriteOffRecoveryAccountId { get; set; }
        public bool RequireSubledgerJournalApproval { get; set; }

        /// <summary>
        /// True when posted transactions exist for the tenant.
        /// When true, base currency and default control accounts cannot be changed.
        /// </summary>
        public bool TransactionsExist { get; set; }
        public Guid? MigrationClearingAccountId { get; set; }
        public bool OpeningBalanceAutoRoutingEnabled { get; set; } = true;
    }

    public class UpdateFinanceSettingsDto
    {
        public string? CoaType { get; set; }
        public string? BaseCurrency { get; set; }
        public string? AccountSeparator { get; set; }
        public Guid? RetainedEarningsAccountId { get; set; }
        public Guid? UnrealizedGainLossAccountId { get; set; }
        public Guid? RealizedGainLossAccountId { get; set; }
        public Guid? SuspenseAccountId { get; set; }
        public Guid? ControlAccountArId { get; set; }
        public Guid? ControlAccountApId { get; set; }
        public Guid? ControlAccountInventoryId { get; set; }
        public Guid? ControlAccountPayrollId { get; set; }
        public Guid? ControlAccountTaxId { get; set; }
        public Guid? MigrationClearingAccountId { get; set; }
        public bool? OpeningBalanceAutoRoutingEnabled { get; set; }
        
        // Subledger Settings
        public Guid? WriteOffExpenseAccountId { get; set; }
        public Guid? WriteOffRecoveryAccountId { get; set; }
        public bool? RequireSubledgerJournalApproval { get; set; }
    }
}
