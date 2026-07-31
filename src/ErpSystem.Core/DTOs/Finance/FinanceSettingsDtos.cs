using System;
using ErpSystem.Core.Enums;

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
        public bool FunctionalCurrencyLocked { get; set; }
        public DateTime? FunctionalCurrencyLockedAt { get; set; }
        public string? FunctionalCurrencyLockedReason { get; set; }
        public string AccountSeparator { get; set; } = "-";
        public Guid? RetainedEarningsAccountId { get; set; }
        public Guid? UnrealizedGainLossAccountId { get; set; }
        public Guid? UnrealizedFxGainAccountId { get; set; }
        public Guid? UnrealizedFxLossAccountId { get; set; }
        public Guid? RealizedGainLossAccountId { get; set; }
        public Guid? RealizedFxGainAccountId { get; set; }
        public Guid? RealizedFxLossAccountId { get; set; }
        public Guid? SuspenseAccountId { get; set; }
        public Guid? ControlAccountArId { get; set; }
        public Guid? ControlAccountApId { get; set; }
        public Guid? SupplierAdvanceAccountId { get; set; }
        public Guid? CustomerAdvanceAccountId { get; set; }
        public Guid? ControlAccountInventoryId { get; set; }
        public Guid? ControlAccountPayrollId { get; set; }
        public Guid? ControlAccountTaxId { get; set; }
        public Guid? ControlAccountGRVAccrualId { get; set; }
        public Guid? DiscountAllowedAccountId { get; set; }
        public Guid? DiscountReceivedAccountId { get; set; }
        
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
        public DepositPolicy BankDepositPolicy { get; set; } = DepositPolicy.DepositIntact;
        public bool RequireBankDepositPrimaryEvidence { get; set; } = true;
        public bool AutoPostBankDepositAfterApproval { get; set; } = true;
        public decimal? MaximumDepositDeductionAmount { get; set; }
        public decimal? MaximumDepositDeductionPercentage { get; set; }
        public int BankStatementMatchDateToleranceDays { get; set; } = 3;
        public int ChequeClearingPeriodDays { get; set; } = 3;
        public Guid? ReturnedChequeBankChargeAccountId { get; set; }
        public ReturnedChequeChargeTreatment DefaultReturnedChequeChargeTreatment { get; set; } =
            ReturnedChequeChargeTreatment.CustomerRecoverable;
        public bool DirectionalExchangeRatePolicyEnabled { get; set; }
        public string DefaultTransactionQuoteSide { get; set; } = "Mid";
        public string ArInvoiceQuoteSide { get; set; } = "Mid";
        public string ArSettlementQuoteSide { get; set; } = "Buying";
        public string ApInvoiceQuoteSide { get; set; } = "Mid";
        public string ApSettlementQuoteSide { get; set; } = "Selling";
        public string ClosingQuoteSide { get; set; } = "Mid";
        public bool RequireExchangeRateOverrideApproval { get; set; } = true;
    }

    public class UpdateFinanceSettingsDto
    {
        public string? CoaType { get; set; }
        public string? BaseCurrency { get; set; }
        public string? AccountSeparator { get; set; }
        public Guid? RetainedEarningsAccountId { get; set; }
        public Guid? UnrealizedGainLossAccountId { get; set; }
        public Guid? UnrealizedFxGainAccountId { get; set; }
        public Guid? UnrealizedFxLossAccountId { get; set; }
        public Guid? RealizedGainLossAccountId { get; set; }
        public Guid? RealizedFxGainAccountId { get; set; }
        public Guid? RealizedFxLossAccountId { get; set; }
        public Guid? SuspenseAccountId { get; set; }
        public Guid? ControlAccountArId { get; set; }
        public Guid? ControlAccountApId { get; set; }
        public Guid? SupplierAdvanceAccountId { get; set; }
        public Guid? CustomerAdvanceAccountId { get; set; }
        public Guid? ControlAccountInventoryId { get; set; }
        public Guid? ControlAccountPayrollId { get; set; }
        public Guid? ControlAccountTaxId { get; set; }
        public Guid? ControlAccountGRVAccrualId { get; set; }
        public Guid? DiscountAllowedAccountId { get; set; }
        public Guid? DiscountReceivedAccountId { get; set; }
        public Guid? MigrationClearingAccountId { get; set; }
        public bool? OpeningBalanceAutoRoutingEnabled { get; set; }
        
        // Subledger Settings
        public Guid? WriteOffExpenseAccountId { get; set; }
        public Guid? WriteOffRecoveryAccountId { get; set; }
        public bool? RequireSubledgerJournalApproval { get; set; }
        public DepositPolicy? BankDepositPolicy { get; set; }
        public bool? RequireBankDepositPrimaryEvidence { get; set; }
        public bool? AutoPostBankDepositAfterApproval { get; set; }
        public decimal? MaximumDepositDeductionAmount { get; set; }
        public decimal? MaximumDepositDeductionPercentage { get; set; }
        public int? BankStatementMatchDateToleranceDays { get; set; }
        public int? ChequeClearingPeriodDays { get; set; }
        public Guid? ReturnedChequeBankChargeAccountId { get; set; }
        public ReturnedChequeChargeTreatment? DefaultReturnedChequeChargeTreatment { get; set; }
        public bool? DirectionalExchangeRatePolicyEnabled { get; set; }
        public string? DefaultTransactionQuoteSide { get; set; }
        public string? ArInvoiceQuoteSide { get; set; }
        public string? ArSettlementQuoteSide { get; set; }
        public string? ApInvoiceQuoteSide { get; set; }
        public string? ApSettlementQuoteSide { get; set; }
        public string? ClosingQuoteSide { get; set; }
        public bool? RequireExchangeRateOverrideApproval { get; set; }
    }
}
