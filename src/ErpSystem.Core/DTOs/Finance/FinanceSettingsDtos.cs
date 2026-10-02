using System;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;

namespace ErpSystem.Core.DTOs.Finance
{
    public class FinanceSettingsDto
    {
        public Guid? ReturnToVendorClearingAccountId { get; set; }
        public Guid? PurchaseReturnVarianceAccountId { get; set; }
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CoaType { get; set; } = "Standard";
        public bool CoaConfigurationLocked { get; set; }
        public string BaseCurrency { get; set; } = "GHS";
        public int WhtStatutoryYearStartMonth { get; set; } = 1;
        public int WhtStatutoryYearStartDay { get; set; } = 1;
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
        public Guid? ControlAccountCOGSId { get; set; }
        public Guid? ControlAccountPayrollId { get; set; }
        public Guid? ControlAccountTaxId { get; set; }
        public Guid? ControlAccountGRVAccrualId { get; set; }
        public Guid? DiscountAllowedAccountId { get; set; }
        public Guid? DiscountReceivedAccountId { get; set; }
        
        // Subledger Settings
        public Guid? WriteOffExpenseAccountId { get; set; }
        public Guid? WriteOffRecoveryAccountId { get; set; }
        public bool RequireSubledgerJournalApproval { get; set; }
        public decimal ApInvoicePriceTolerancePercent { get; set; } = 1m;
        public decimal ApInvoiceQuantityTolerancePercent { get; set; } = 1m;

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
        public decimal CashTillVarianceApprovalThreshold { get; set; } = 100m;
        public bool RequireIndependentCashTillClosure { get; set; } = true;
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
        public FinanceReversalDatePolicy ReversalDatePolicy { get; set; } =
            FinanceReversalDatePolicy.CurrentOpenPeriod;
        public int MinimumReversalReasonLength { get; set; } = 20;
        public bool EnforceFinanceAccessScopes { get; set; }
        public bool RequireDepreciationBeforePeriodClose { get; set; } = true;
        public int UnitPriceDecimalPlaces { get; set; } = 4;
        public int ExchangeRateInputDecimalPlaces { get; set; } = 10;
        public int ExchangeRateDisplayDecimalPlaces { get; set; } = 6;
        public int TaxPercentageDecimalPlaces { get; set; } = 4;
        public GovernedRoundingMethod TaxRoundingMethod { get; set; } = GovernedRoundingMethod.Nearest;
        public TaxRoundingScope TaxRoundingScope { get; set; } = TaxRoundingScope.Line;
        public decimal? TaxRoundingIncrement { get; set; }
        public bool InvoiceRoundingEnabled { get; set; }
        public decimal? InvoiceRoundingIncrement { get; set; }
        public GovernedRoundingMethod InvoiceRoundingMethod { get; set; } = GovernedRoundingMethod.Nearest;
        public Guid? InvoiceRoundingGainAccountId { get; set; }
        public Guid? InvoiceRoundingLossAccountId { get; set; }
        public decimal SettlementToleranceAmount { get; set; }
        public decimal SettlementTolerancePercentage { get; set; }
        public int ReportDisplayDecimalPlaces { get; set; } = 2;
        public bool PrecisionAccountingPolicyLocked { get; set; }
    }

    public class UpdateFinanceSettingsDto
    {
        public Guid? ReturnToVendorClearingAccountId { get; set; }
        public Guid? PurchaseReturnVarianceAccountId { get; set; }
        public string? CoaType { get; set; }
        public string? BaseCurrency { get; set; }
        public int? WhtStatutoryYearStartMonth { get; set; }
        public int? WhtStatutoryYearStartDay { get; set; }
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
        public decimal? CashTillVarianceApprovalThreshold { get; set; }
        public bool? RequireIndependentCashTillClosure { get; set; }
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
        public FinanceReversalDatePolicy? ReversalDatePolicy { get; set; }
        public int? MinimumReversalReasonLength { get; set; }
        public bool? EnforceFinanceAccessScopes { get; set; }
        public bool? RequireDepreciationBeforePeriodClose { get; set; }
        public decimal? ApInvoicePriceTolerancePercent { get; set; }
        public decimal? ApInvoiceQuantityTolerancePercent { get; set; }
        public int? UnitPriceDecimalPlaces { get; set; }
        public int? ExchangeRateInputDecimalPlaces { get; set; }
        public int? ExchangeRateDisplayDecimalPlaces { get; set; }
        public int? TaxPercentageDecimalPlaces { get; set; }
        public GovernedRoundingMethod? TaxRoundingMethod { get; set; }
        public TaxRoundingScope? TaxRoundingScope { get; set; }
        public decimal? TaxRoundingIncrement { get; set; }
        public bool? InvoiceRoundingEnabled { get; set; }
        public decimal? InvoiceRoundingIncrement { get; set; }
        public GovernedRoundingMethod? InvoiceRoundingMethod { get; set; }
        public Guid? InvoiceRoundingGainAccountId { get; set; }
        public Guid? InvoiceRoundingLossAccountId { get; set; }
        public decimal? SettlementToleranceAmount { get; set; }
        public decimal? SettlementTolerancePercentage { get; set; }
        public int? ReportDisplayDecimalPlaces { get; set; }
    }
}
