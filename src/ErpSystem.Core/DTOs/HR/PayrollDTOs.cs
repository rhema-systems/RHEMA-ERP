using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.DTOs.HR.Payroll;

public class PayrollSetupSummaryDto
{
    public PayrollParameterSetDto? ActiveParameters { get; set; }
    public int ComponentCount { get; set; }
    public int ComponentRuleCount { get; set; }
    public int CodeTypeCount { get; set; }
    public int HolidayCount { get; set; }
    public int NonWorkingDayCount { get; set; }
    public int ExchangeRateCount { get; set; }
    public int BankBranchCount { get; set; }
    public int LeaveSetupCount { get; set; }
    public int OvertimeRangeCount { get; set; }
    public int LegacyMenuUserCount { get; set; }
    public int MenuSecurityCount { get; set; }
    public int CompanyProfileCount { get; set; }
    public int BusinessUnitCount { get; set; }
    public int CompanyBankerCount { get; set; }
    public int GradeCount { get; set; }
    public int GradeNotchCount { get; set; }
    public int TaxBandCount { get; set; }
    public int TaxReliefCount { get; set; }
    public int PensionSchemeCount { get; set; }
    public int OvertimePolicyCount { get; set; }
    public int LoanPolicyCount { get; set; }
    public int BonusPolicyCount { get; set; }
    public int BonusRuleCount { get; set; }
    public int BonusExceptionCount { get; set; }
    public int BackpayPolicyCount { get; set; }
    public int BackpayRuleCount { get; set; }
    public int BackpayExceptionCount { get; set; }
    public int JournalMappingCount { get; set; }
    public IReadOnlyList<PayrollComponentDto> Components { get; set; } = [];
    public IReadOnlyList<PayrollComponentRuleDto> ComponentRules { get; set; } = [];
    public IReadOnlyList<PayrollTaxBandDto> TaxBands { get; set; } = [];
    public IReadOnlyList<PayrollTaxReliefDto> TaxReliefs { get; set; } = [];
    public IReadOnlyList<PayrollPensionSchemeDto> PensionSchemes { get; set; } = [];
    public IReadOnlyList<PayrollOvertimePolicyDto> OvertimePolicies { get; set; } = [];
    public IReadOnlyList<PayrollLoanPolicyDto> LoanPolicies { get; set; } = [];
    public IReadOnlyList<PayrollBonusPolicyDto> BonusPolicies { get; set; } = [];
    public IReadOnlyList<PayrollBonusRuleDto> BonusRules { get; set; } = [];
    public IReadOnlyList<PayrollBonusExceptionDto> BonusExceptions { get; set; } = [];
    public IReadOnlyList<PayrollBackpayPolicyDto> BackpayPolicies { get; set; } = [];
    public IReadOnlyList<PayrollBackpayRuleDto> BackpayRules { get; set; } = [];
    public IReadOnlyList<PayrollBackpayExceptionDto> BackpayExceptions { get; set; } = [];
    public IReadOnlyList<PayrollJournalMappingDto> JournalMappings { get; set; } = [];
    public IReadOnlyList<PayrollCodeTypeDto> CodeTypes { get; set; } = [];
    public IReadOnlyList<PayrollBankBranchDto> BankBranches { get; set; } = [];
    public IReadOnlyList<PayrollLeaveSetupDto> LeaveSetups { get; set; } = [];
    public IReadOnlyList<PayrollLegacyMenuUserDto> LegacyMenuUsers { get; set; } = [];
    public IReadOnlyList<PayrollLegacyMenuSecurityDto> MenuSecurity { get; set; } = [];
    public IReadOnlyList<PayrollCompanyProfileDto> CompanyProfiles { get; set; } = [];
    public IReadOnlyList<PayrollGradeDto> Grades { get; set; } = [];
}

public class PayrollParameterSetDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "GHS";
    public string DateFormat { get; set; } = "dd-MM-yyyy";
    public bool MultiLoginEnabled { get; set; }
    public int? MaxLoginCount { get; set; }
    public int CurrentPayPeriod { get; set; }
    public DateTime? CurrentPeriodFrom { get; set; }
    public DateTime? CurrentPeriodTo { get; set; }
    public int MonthDays { get; set; }
    public int PayFrequencyMonths { get; set; }
    public string? PayMode { get; set; }
    public string? TimeSheetMode { get; set; }
    public string? LeaveClassification { get; set; }
    public bool MultiplePayBasisEnabled { get; set; }
    public string? DefaultPayBasis { get; set; }
    public bool AllowEmployeePaymentMethod { get; set; }
    public string? DefaultEmployeePaymentMethod { get; set; }
    public decimal EmployeeSsfRate { get; set; }
    public decimal EmployerSsfRate { get; set; }
    public decimal SsfLimit { get; set; }
    public decimal BonusTaxRate { get; set; }
    public decimal MinimumBonusToTax { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? MinimumTaxableIncome { get; set; }
    public decimal? DebitRatio { get; set; }
    public int? MinimumHireAge { get; set; }
    public int? MaleRetireAge { get; set; }
    public int? FemaleRetireAge { get; set; }
    public decimal? TotalAllowanceTaxCeiling { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? ReportingCurrency { get; set; }
    public string? PictureDirectory { get; set; }
    public decimal? NightAllowanceAmount { get; set; }
    public decimal? NightAllowancePercent { get; set; }
    public bool SeparateBonusTax { get; set; }
    public bool MultiCurrencyEnabled { get; set; }
    public bool TimesheetEnabled { get; set; }
    public int? MinimumPasswordLength { get; set; }
    public int? PasswordExpirationDays { get; set; }
    public int? PasswordReuseCount { get; set; }
    public int? MinimumPasswordNumbers { get; set; }
    public int? MinimumPasswordSpecialCharacters { get; set; }
    public int? MinimumPasswordUppercase { get; set; }
    public int? MinimumPasswordLowercase { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollParameterSetDto
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "GHS";
    public string DateFormat { get; set; } = "dd-MM-yyyy";
    public bool MultiLoginEnabled { get; set; }
    public int? MaxLoginCount { get; set; }
    public int CurrentPayPeriod { get; set; }
    public DateTime? CurrentPeriodFrom { get; set; }
    public DateTime? CurrentPeriodTo { get; set; }
    public int MonthDays { get; set; } = 30;
    public int PayFrequencyMonths { get; set; } = 1;
    public string? PayMode { get; set; }
    public string? TimeSheetMode { get; set; }
    public string? LeaveClassification { get; set; }
    public bool MultiplePayBasisEnabled { get; set; }
    public string? DefaultPayBasis { get; set; }
    public bool AllowEmployeePaymentMethod { get; set; }
    public string? DefaultEmployeePaymentMethod { get; set; }
    public decimal EmployeeSsfRate { get; set; }
    public decimal EmployerSsfRate { get; set; }
    public decimal SsfLimit { get; set; }
    public decimal BonusTaxRate { get; set; }
    public decimal MinimumBonusToTax { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? MinimumTaxableIncome { get; set; }
    public decimal? DebitRatio { get; set; }
    public int? MinimumHireAge { get; set; }
    public int? MaleRetireAge { get; set; }
    public int? FemaleRetireAge { get; set; }
    public decimal? TotalAllowanceTaxCeiling { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? ReportingCurrency { get; set; }
    public string? PictureDirectory { get; set; }
    public decimal? NightAllowanceAmount { get; set; }
    public decimal? NightAllowancePercent { get; set; }
    public bool SeparateBonusTax { get; set; }
    public bool MultiCurrencyEnabled { get; set; }
    public bool TimesheetEnabled { get; set; }
    public int? MinimumPasswordLength { get; set; }
    public int? PasswordExpirationDays { get; set; }
    public int? PasswordReuseCount { get; set; }
    public int? MinimumPasswordNumbers { get; set; }
    public int? MinimumPasswordSpecialCharacters { get; set; }
    public int? MinimumPasswordUppercase { get; set; }
    public int? MinimumPasswordLowercase { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayrollComponentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollComponentType ComponentType { get; set; }
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public decimal Rate { get; set; }
    public decimal? TaxFreeCeiling { get; set; }
    public decimal? SeparateTaxPercent { get; set; }
    public decimal? MaxBenefitToTax { get; set; }
    public decimal? EmployerAmount { get; set; }
    public string? Category { get; set; }
    public string? Cycle { get; set; }
    public int? NextPayPeriod { get; set; }
    public DateTime? NextPayPeriodDate { get; set; }
    public DateTime? LastPayDate { get; set; }
    public bool Taxable { get; set; }
    public bool EmployerTaxable { get; set; }
    public bool SeparateTax { get; set; }
    public bool ApplyToBenefit { get; set; }
    public bool AfterTaxContribution { get; set; }
    public bool IncludeInGross { get; set; }
    public bool GrossUp { get; set; }
    public bool Prorate { get; set; }
    public bool AppliesByDefault { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool IsActive { get; set; }
}

public class UpsertPayrollComponentDto : PayrollComponentDto
{
}

public class PayrollComponentRuleDto
{
    public Guid Id { get; set; }
    public PayrollComponentType ComponentType { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public decimal? TaxFreeCeiling { get; set; }
    public decimal? EmployerAmount { get; set; }
    public bool AfterTax { get; set; }
    public bool EmployerTaxable { get; set; }
    public bool Applicable { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollComponentRuleDto : PayrollComponentRuleDto
{
}

public class PayrollTaxBandDto
{
    public Guid Id { get; set; }
    public string TaxType { get; set; } = "PAYE";
    public int SerialNo { get; set; }
    public string? Description { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal? TaxableIncome { get; set; }
    public decimal RatePercent { get; set; }
    public decimal FixedAmount { get; set; }
    public decimal? PerMonthAmount { get; set; }
    public decimal? CumulativeTax { get; set; }
    public decimal? CumulativeSalary { get; set; }
    public int? PayPeriod { get; set; }
    public DateTime? PayPeriodFrom { get; set; }
    public DateTime? PayPeriodTo { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsAnnual { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollTaxBandDto : PayrollTaxBandDto
{
}

public class PayrollTaxReliefDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public decimal Factor { get; set; }
    public bool AppliesByDefault { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollTaxReliefDto : PayrollTaxReliefDto
{
}

public class PayrollEmployeeTaxReliefDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string ReliefCode { get; set; } = string.Empty;
    public string ReliefName { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public decimal Factor { get; set; }
    public decimal TotalRelief { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class PayrollEmployeeTaxReliefBulkSaveDto
{
    public string ReliefCode { get; set; } = string.Empty;
    public string ReliefName { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;
    public decimal Amount { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public IReadOnlyList<PayrollEmployeeTaxReliefLineDto> Entries { get; set; } = [];
}

public class PayrollEmployeeTaxReliefLineDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? Factor { get; set; }
    public bool IsSelected { get; set; }
}

public class PayrollEmployeeComponentExceptionDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal MonthlyBasicSalary { get; set; }
    public Guid PayrollComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public PayrollComponentType ComponentType { get; set; }
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public decimal Rate { get; set; }
    public bool Taxable { get; set; }
    public decimal? TaxFreeCeiling { get; set; }
    public decimal? EmployerAmount { get; set; }
    public bool EmployerTaxable { get; set; }
    public bool GrossUp { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool Applicable { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class PayrollEmployeeComponentExceptionBulkSaveDto
{
    public Guid? PayrollComponentId { get; set; }
    public string? ComponentCode { get; set; }
    public PayrollComponentType? ComponentType { get; set; }
    public IReadOnlyList<PayrollEmployeeComponentExceptionLineDto> Entries { get; set; } = [];
}

public class PayrollEmployeeComponentExceptionLineDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public PayrollCalculationType? CalculationTypeOverride { get; set; }
    public decimal? AmountOverride { get; set; }
    public decimal? RateOverride { get; set; }
    public bool? TaxableOverride { get; set; }
    public decimal? TaxFreeCeilingOverride { get; set; }
    public decimal? EmployerAmountOverride { get; set; }
    public bool? EmployerTaxableOverride { get; set; }
    public bool? GrossUpOverride { get; set; }
    public string? CurrencyCodeOverride { get; set; }
    public bool Applicable { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsSelected { get; set; }
}

public class PayrollPromotionArrearsEntryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? LegacyEmployeeId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public decimal? WorkingDays { get; set; }
    public decimal? BasicSalary { get; set; }
    public bool IsActive { get; set; }
}

public class PayrollPromotionArrearsBulkSaveDto
{
    public string? LegacyCompanyCode { get; set; }
    public IReadOnlyList<PayrollPromotionArrearsLineDto> Entries { get; set; } = [];
}

public class PayrollPromotionArrearsLineDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
    public bool IsSelected { get; set; }
}

public class PayrollOvertimeSummaryEntryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? LegacyEmployeeId { get; set; }
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public decimal AbsentDays { get; set; }
    public decimal NormalDays { get; set; }
    public decimal WeekdayDays { get; set; }
    public decimal HolidayDays { get; set; }
    public decimal SaturdayDays { get; set; }
    public decimal SundayDays { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class PayrollOvertimeSummaryBulkSaveDto
{
    public string? LegacyCompanyCode { get; set; }
    public IReadOnlyList<PayrollOvertimeSummaryLineDto> Entries { get; set; } = [];
}

public class PayrollOvertimeSummaryLineDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public decimal? AbsentDays { get; set; }
    public decimal? NormalDays { get; set; }
    public decimal? WeekdayDays { get; set; }
    public decimal? HolidayDays { get; set; }
    public decimal? SaturdayDays { get; set; }
    public decimal? SundayDays { get; set; }
    public bool IsSelected { get; set; }
}

public class PayrollContributionOpeningBalanceDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? LegacyEmployeeId { get; set; }
    public string ContributionCodeType { get; set; } = "CON";
    public string ContributionCode { get; set; } = string.Empty;
    public string ContributionName { get; set; } = string.Empty;
    public DateTime BalanceAsAt { get; set; }
    public decimal OpeningBalance { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class PayrollContributionOpeningBalanceBulkSaveDto
{
    public string ContributionCodeType { get; set; } = "CON";
    public string ContributionCode { get; set; } = string.Empty;
    public string ContributionName { get; set; } = string.Empty;
    public string? LegacyCompanyCode { get; set; }
    public IReadOnlyList<PayrollContributionOpeningBalanceLineDto> Entries { get; set; } = [];
}

public class PayrollContributionOpeningBalanceLineDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateTime? BalanceAsAt { get; set; }
    public decimal? OpeningBalance { get; set; }
    public bool IsSelected { get; set; }
}

public class PayrollContributionTransactionDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? LegacyEmployeeId { get; set; }
    public string ContributionCodeType { get; set; } = "CON";
    public string ContributionCode { get; set; } = string.Empty;
    public string ContributionName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public decimal Amount { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollContributionTransactionDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string ContributionCodeType { get; set; } = "CON";
    public string ContributionCode { get; set; } = string.Empty;
    public string ContributionName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "Withdrawal";
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.Date;
    public decimal Amount { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayrollPensionSchemeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal EmployeeRatePercent { get; set; }
    public decimal EmployerRatePercent { get; set; }
    public decimal? ContributionLimit { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollPensionSchemeDto : PayrollPensionSchemeDto
{
}

public class PayrollOvertimePolicyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal NormalWorkingHours { get; set; }
    public decimal? NormalHours { get; set; }
    public decimal WeekdayRate { get; set; }
    public decimal HolidayRate { get; set; }
    public decimal SpecialDutyRate { get; set; }
    public bool Taxable { get; set; }
    public decimal? TaxCeiling { get; set; }
    public bool SeparateOvertimeTax { get; set; }
    public decimal? MaxOvertimeAmount { get; set; }
    public bool MaxOvertimeIsPercent { get; set; }
    public string? MaxOvertimeType { get; set; }
    public decimal? LeaveRecallRate { get; set; }
    public int? PayPeriod { get; set; }
    public DateTime? PayPeriodFrom { get; set; }
    public DateTime? PayPeriodTo { get; set; }
    public decimal? MaxOvertimeSeparateTax { get; set; }
    public decimal? MinimumBasicForSeparateTax { get; set; }
    public decimal? MinimumSeparateOvertimePercent { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<PayrollOvertimeRangeDto> Ranges { get; set; } = [];
}

public class UpsertPayrollOvertimePolicyDto : PayrollOvertimePolicyDto
{
}

public class PayrollLoanPolicyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? MaxLoanAmount { get; set; }
    public decimal? InterestRatePercent { get; set; }
    public bool ApplyInterest { get; set; }
    public int? MaxPaybackPeriods { get; set; }
    public decimal? MaxDebitRatioPercent { get; set; }
    public string InterestType { get; set; } = "Flat";
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollLoanPolicyDto : PayrollLoanPolicyDto
{
}

public class PayrollBonusPolicyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public bool Taxable { get; set; }
    public bool SeparateTax { get; set; }
    public decimal? TaxFreeCeiling { get; set; }
    public decimal? MinimumToTax { get; set; }
    public decimal? AnnualSalaryPercentToTax { get; set; }
    public decimal? TaxRate { get; set; }
    public int? NextPayPeriod { get; set; }
    public int? LastPayPeriod { get; set; }
    public DateTime? NextPayPeriodDate { get; set; }
    public DateTime? LastPayPeriodDate { get; set; }
    public string? Category { get; set; }
    public string? Cycle { get; set; }
    public bool PaySeparate { get; set; }
    public bool PerAnnual { get; set; }
    public int? MinimumMonths { get; set; }
    public bool Prorate { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollBonusPolicyDto : PayrollBonusPolicyDto
{
}

public class PayrollBonusRuleDto
{
    public Guid Id { get; set; }
    public string BonusCode { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public bool Applicable { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollBonusRuleDto : PayrollBonusRuleDto
{
}

public class PayrollBonusExceptionDto
{
    public Guid Id { get; set; }
    public string BonusCode { get; set; } = string.Empty;
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;
    public decimal Amount { get; set; }
    public bool Taxable { get; set; } = true;
    public bool Applicable { get; set; } = true;
    public string? CurrencyCode { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollBonusExceptionBulkSaveDto
{
    public string BonusCode { get; set; } = string.Empty;
    public string? LegacyCompanyCode { get; set; }
    public IReadOnlyList<PayrollBonusExceptionLineDto> Entries { get; set; } = [];
}

public class PayrollBonusExceptionLineDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;
    public decimal Amount { get; set; }
    public bool? Taxable { get; set; }
    public bool Applicable { get; set; } = true;
    public string? CurrencyCode { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class PayrollBackpayPolicyDto
{
    public Guid Id { get; set; }
    public string OperationType { get; set; } = "IncreaseSalary";
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public int? NumberOfMonths { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public bool ApplyTax { get; set; }
    public bool ApplySsf { get; set; }
    public string? MinimumServiceMode { get; set; }
    public decimal? MinimumServiceValue { get; set; }
    public DateTime? MinimumServiceDate { get; set; }
    public string CategoryType { get; set; } = "All Staff";
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollBackpayPolicyDto : PayrollBackpayPolicyDto
{
}

public class PayrollBackpayRuleDto
{
    public Guid Id { get; set; }
    public string OperationType { get; set; } = "IncreaseSalary";
    public string CategoryType { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public bool Applicable { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollBackpayRuleDto : PayrollBackpayRuleDto
{
}

public class PayrollBackpayExceptionDto
{
    public Guid Id { get; set; }
    public string OperationType { get; set; } = "SalaryArrears";
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public PayrollCalculationType CalculationType { get; set; }
    public decimal Amount { get; set; }
    public bool Applicable { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollBackpayExceptionDto : PayrollBackpayExceptionDto
{
}

public class PayrollJournalMappingDto
{
    public Guid Id { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? ComponentCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DebitCredit { get; set; } = "DR";
    public string AccountCode { get; set; } = string.Empty;
    public string? AccountType { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollJournalMappingDto : PayrollJournalMappingDto
{
}

public class PayrollLegacyMenuItemDto
{
    public string MenuId { get; set; } = string.Empty;
    public string? ParentMenuId { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Form";
    public string? Target { get; set; }
    public string? CompanyScope { get; set; }
    public int SequenceNo { get; set; }
    public bool Implemented { get; set; }
}

public class PayrollCodeSetupDto
{
    public string? SelectedCodeType { get; set; }
    public IReadOnlyList<PayrollCodeTypeDto> CodeTypes { get; set; } = [];
    public IReadOnlyList<PayrollCodeValueDto> CodeValues { get; set; } = [];
}

public class PayrollCodeTypeDto
{
    public Guid Id { get; set; }
    public string CodeType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public bool Blocked { get; set; }
    public bool Dependent { get; set; }
    public string? DependentOnCodeType { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public int ValueCount { get; set; }
}

public class UpsertPayrollCodeTypeDto : PayrollCodeTypeDto
{
}

public class PayrollCodeValueDto
{
    public Guid Id { get; set; }
    public Guid PayrollCodeTypeId { get; set; }
    public string CodeType { get; set; } = string.Empty;
    public string ActualCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AdditionalDescription { get; set; }
    public string? AccountCode { get; set; }
    public bool Blocked { get; set; }
    public string? DependentCodeType { get; set; }
    public string? DependentActualCode { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollCodeValueDto : PayrollCodeValueDto
{
}

public class PayrollHolidaySetupDto
{
    public IReadOnlyList<PayrollHolidayDto> Holidays { get; set; } = [];
    public IReadOnlyList<PayrollNonWorkingDayDto> NonWorkingDays { get; set; } = [];
}

public class PayrollHolidayDto
{
    public Guid Id { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollHolidayDto : PayrollHolidayDto
{
}

public class PayrollNonWorkingDayDto
{
    public Guid Id { get; set; }
    public string DayCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? OvertimeRate { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollNonWorkingDayDto : PayrollNonWorkingDayDto
{
}

public class PayrollExchangeRateDto
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollExchangeRateDto : PayrollExchangeRateDto
{
}

public class PayrollBankBranchDto
{
    public Guid Id { get; set; }
    public string? BranchSetupCode { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string BranchDescription { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? SortCode { get; set; }
    public string? AccountCode { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollBankBranchDto : PayrollBankBranchDto
{
}

public class PayrollLeaveSetupCollectionDto
{
    public IReadOnlyList<PayrollLeaveSetupDto> LeaveSetups { get; set; } = [];
    public IReadOnlyList<PayrollLeaveSetupDetailDto> LeaveSetupDetails { get; set; } = [];
}

public class PayrollLeaveSetupDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string CategoryDetail { get; set; } = string.Empty;
    public string? LegacyCompanyCode { get; set; }
    public int DetailCount { get; set; }
    public IReadOnlyList<PayrollLeaveSetupDetailDto> Details { get; set; } = [];
}

public class UpsertPayrollLeaveSetupDto : PayrollLeaveSetupDto
{
}

public class PayrollLeaveSetupDetailDto
{
    public Guid Id { get; set; }
    public Guid PayrollLeaveSetupId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string CategoryDetail { get; set; } = string.Empty;
    public int? Days { get; set; }
    public int? ServiceFrom { get; set; }
    public int? ServiceTo { get; set; }
    public int? SequenceNo { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollLeaveSetupDetailDto : PayrollLeaveSetupDetailDto
{
}

public class PayrollOvertimeSetupDto
{
    public IReadOnlyList<PayrollOvertimePolicyDto> OvertimePolicies { get; set; } = [];
    public IReadOnlyList<PayrollOvertimeRangeDto> OvertimeRanges { get; set; } = [];
}

public class PayrollOvertimeRangeDto
{
    public Guid Id { get; set; }
    public Guid PayrollOvertimePolicyId { get; set; }
    public decimal MinRange { get; set; }
    public decimal MaxRange { get; set; }
    public decimal Rate { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollOvertimeRangeDto : PayrollOvertimeRangeDto
{
}

public class PayrollLegacyMenuUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserGroup { get; set; } = string.Empty;
    public string UserNo { get; set; } = string.Empty;
    public string? LegacyPasswordHash { get; set; }
    public bool LoginEnabled { get; set; }
    public bool Locked { get; set; }
    public bool PasswordChangeRequired { get; set; }
    public DateTime? PasswordExpiryDate { get; set; }
    public int PasswordFailureCount { get; set; }
    public string? CompanyId { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool CanViewSalary { get; set; }
    public bool CanApprove { get; set; }
    public string? RefreshToken { get; set; }
    public string? LastPasswordChange { get; set; }
}

public class UpsertPayrollLegacyMenuUserDto : PayrollLegacyMenuUserDto
{
}

public class PayrollLegacyMenuSecurityDto
{
    public Guid Id { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool Allowed { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? LegacyCompanyCode { get; set; }
    public string? TopMenu { get; set; }
    public string? SubMenu { get; set; }
    public bool CanEdit { get; set; }
}

public class UpsertPayrollLegacyMenuSecurityDto : PayrollLegacyMenuSecurityDto
{
}

public class PayrollLegacyPasswordChangeDto
{
    public string UserName { get; set; } = string.Empty;
    public string? LegacyCompanyCode { get; set; }
    public string? OldPasswordReference { get; set; }
    public string NewPasswordReference { get; set; } = string.Empty;
    public bool ForceChange { get; set; }
    public DateTime? PasswordExpiryDate { get; set; }
}

public class PayrollCompanySetupDto
{
    public IReadOnlyList<PayrollCompanyProfileDto> CompanyProfiles { get; set; } = [];
    public IReadOnlyList<PayrollBusinessUnitDto> BusinessUnits { get; set; } = [];
    public IReadOnlyList<PayrollCompanyBankerDto> CompanyBankers { get; set; } = [];
}

public class PayrollCompanyProfileDto
{
    public Guid Id { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanySystemName { get; set; }
    public string? CompanyCode { get; set; }
    public bool MultipleBusinessUnits { get; set; }
    public int? BusinessNumber { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? CityOrTown { get; set; }
    public string? RegionOrState { get; set; }
    public string? Country { get; set; }
    public string? LicenceType { get; set; }
    public int? LicenceNo { get; set; }
    public string? SocialSecurityNo { get; set; }
    public string? TaxpayerIdNo { get; set; }
    public string? PictureName { get; set; }
    public string? LogoName { get; set; }
    public bool SplitLicenceOnBusinessUnits { get; set; }
    public string? LicencePercentOrNumber { get; set; }
    public bool MakeCompanyABusinessUnit { get; set; }
    public bool MultiplePayBasis { get; set; }
    public string? CompanyPayBasis { get; set; }
    public bool MultipleEmployeePaymentMethods { get; set; }
    public string? DefaultEmployeePaymentMethod { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<PayrollBusinessUnitDto> BusinessUnits { get; set; } = [];
    public IReadOnlyList<PayrollCompanyBankerDto> Bankers { get; set; } = [];
}

public class UpsertPayrollCompanyProfileDto : PayrollCompanyProfileDto
{
}

public class PayrollBusinessUnitDto
{
    public Guid Id { get; set; }
    public Guid? PayrollCompanyProfileId { get; set; }
    public string BusinessUnitId { get; set; } = string.Empty;
    public string? BusinessUnitCode { get; set; }
    public string? BusinessUnitName { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public bool DistributeLicence { get; set; }
    public string? LicencePercentOrNumber { get; set; }
    public int? LicenceValue { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? RegionOrState { get; set; }
    public string? CompanyId { get; set; }
    public string? CompanyCode { get; set; }
    public string? AccountsCode { get; set; }
    public bool MultiplePayBasis { get; set; }
    public string? CompanyPayBasis { get; set; }
    public bool MultipleEmployeePaymentMethods { get; set; }
    public string? DefaultEmployeePaymentMethod { get; set; }
    public string? LegacyCompanyCode { get; set; }
}

public class UpsertPayrollBusinessUnitDto : PayrollBusinessUnitDto
{
}

public class PayrollCompanyBankerDto
{
    public Guid Id { get; set; }
    public Guid? PayrollCompanyProfileId { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? AccountNumber1 { get; set; }
    public string? AccountNumber2 { get; set; }
    public string? AccountNumber3 { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? Signatory1 { get; set; }
    public string? Signatory2 { get; set; }
    public string? Signatory3 { get; set; }
    public string? SignatoryPosition1 { get; set; }
    public string? SignatoryPosition2 { get; set; }
    public string? SignatoryPosition3 { get; set; }
    public string? BankRegion { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollCompanyBankerDto : PayrollCompanyBankerDto
{
}

public class PayrollGradeSetupDto
{
    public IReadOnlyList<PayrollGradeDto> Grades { get; set; } = [];
    public IReadOnlyList<PayrollGradeNotchDto> Notches { get; set; } = [];
}

public class PayrollGradeDto
{
    public Guid Id { get; set; }
    public string? GradeId { get; set; }
    public string? GradeType { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string? SystemGradeName { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? MidPoint { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string? StartPoint { get; set; }
    public decimal? IncrementStep { get; set; }
    public string? Ceiling { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? CompanyId { get; set; }
    public int? OrderField { get; set; }
    public string? ReportingName { get; set; }
    public bool EnforceNotchConsistency { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<PayrollGradeNotchDto> Notches { get; set; } = [];
}

public class UpsertPayrollGradeDto : PayrollGradeDto
{
}

public class PayrollGradeNotchDto
{
    public Guid Id { get; set; }
    public Guid PayrollGradeId { get; set; }
    public string? GradeId { get; set; }
    public string? SystemGradeName { get; set; }
    public string Notch { get; set; } = string.Empty;
    public decimal? Value { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? OrderField { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? CompanyId { get; set; }
    public decimal? AnnualisedValue { get; set; }
    public string? GradeName { get; set; }
    public string? ReportingName { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public decimal? HourlyRate { get; set; }
}

public class UpsertPayrollGradeNotchDto : PayrollGradeNotchDto
{
}

public class PayrollTaxTableDto
{
    public IReadOnlyList<PayrollTaxBandDto> TaxBands { get; set; } = [];
}

public class PayrollEmployeeProfileDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? SectionName { get; set; }
    public string? PositionTitle { get; set; }
    public string? StaffCategory { get; set; }
    public string? JobLocation { get; set; }
    public string? LegacyEmployeeId { get; set; }
    public string? LegacyEmployeeNumber { get; set; }
    public bool PayrollActive { get; set; }
    public bool PayTax { get; set; }
    public bool SsfApplicable { get; set; }
    public bool GrossUp { get; set; }
    public bool Tier2Only { get; set; }
    public bool OvertimeEligible { get; set; }
    public string? SsfNumber { get; set; }
    public string? TinNumber { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public PayrollSalaryBasisDto? SalaryBasis { get; set; }
    public IReadOnlyList<PayrollPaymentMethodDto> PaymentMethods { get; set; } = [];
    public IReadOnlyList<PayrollEmployeeComponentDto> EmployeeComponents { get; set; } = [];
    public IReadOnlyList<PayrollLoanDto> Loans { get; set; } = [];
}

public class UpsertPayrollEmployeeProfileDto
{
    public Guid? Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? LegacyEmployeeId { get; set; }
    public string? LegacyEmployeeNumber { get; set; }
    public bool PayrollActive { get; set; } = true;
    public bool PayTax { get; set; } = true;
    public bool SsfApplicable { get; set; } = true;
    public bool GrossUp { get; set; }
    public bool Tier2Only { get; set; }
    public bool OvertimeEligible { get; set; }
    public string? SsfNumber { get; set; }
    public string? TinNumber { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public UpsertPayrollSalaryBasisDto? SalaryBasis { get; set; }
    public IReadOnlyList<UpsertPayrollPaymentMethodDto> PaymentMethods { get; set; } = [];
    public IReadOnlyList<UpsertPayrollEmployeeComponentDto> EmployeeComponents { get; set; } = [];
}

public class PayrollSalaryBasisDto
{
    public Guid Id { get; set; }
    public decimal MonthlyBasicSalary { get; set; }
    public decimal? AnnualBasicSalary { get; set; }
    public decimal? HourlyRate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollSalaryBasisDto
{
    public Guid? Id { get; set; }
    public decimal MonthlyBasicSalary { get; set; }
    public decimal? AnnualBasicSalary { get; set; }
    public decimal? HourlyRate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayrollPaymentMethodDto
{
    public Guid Id { get; set; }
    public string PaymentType { get; set; } = "Bank";
    public decimal? PaymentPercent { get; set; }
    public decimal? Amount { get; set; }
    public string? BankCode { get; set; }
    public string? BankBranchCode { get; set; }
    public string? AccountNumber { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public int SequenceNo { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollPaymentMethodDto : PayrollPaymentMethodDto
{
}

public class PayrollEmployeeComponentDto
{
    public Guid Id { get; set; }
    public Guid PayrollComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public PayrollCalculationType? CalculationTypeOverride { get; set; }
    public decimal? AmountOverride { get; set; }
    public decimal? RateOverride { get; set; }
    public bool? TaxableOverride { get; set; }
    public decimal? TaxFreeCeilingOverride { get; set; }
    public decimal? EmployerAmountOverride { get; set; }
    public bool? EmployerTaxableOverride { get; set; }
    public bool? GrossUpOverride { get; set; }
    public string? CurrencyCodeOverride { get; set; }
    public bool Applicable { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class UpsertPayrollEmployeeComponentDto
{
    public Guid? Id { get; set; }
    public Guid PayrollComponentId { get; set; }
    public PayrollCalculationType? CalculationTypeOverride { get; set; }
    public decimal? AmountOverride { get; set; }
    public decimal? RateOverride { get; set; }
    public bool? TaxableOverride { get; set; }
    public decimal? TaxFreeCeilingOverride { get; set; }
    public decimal? EmployerAmountOverride { get; set; }
    public bool? EmployerTaxableOverride { get; set; }
    public bool? GrossUpOverride { get; set; }
    public string? CurrencyCodeOverride { get; set; }
    public bool Applicable { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class PayrollLoanDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? LoanPolicyId { get; set; }
    public string? LoanPolicyCode { get; set; }
    public string? LoanPolicyName { get; set; }
    public string? LoanTypeCode { get; set; }
    public string FacilityNumber { get; set; } = string.Empty;
    public DateTime DateGranted { get; set; }
    public decimal AmountGranted { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal MonthlyRepaymentAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal InterestRatePercent { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal InterestRepaymentAmount { get; set; }
    public string RepaymentMode { get; set; } = "Amount";
    public decimal TotalRepaymentAmount { get; set; }
    public decimal EmployeeDebtRatio { get; set; }
    public decimal DebtServiceRatio { get; set; }
    public DateTime PaymentStartDate { get; set; }
    public DateTime? PaymentEndDate { get; set; }
    public int NumberOfRepayments { get; set; }
    public string Status { get; set; } = "Active";
    public int? PeriodOfSuspension { get; set; }
    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }
    public string? SuspensionNarration { get; set; }
    public string? GeneralRemarks { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<PayrollLoanScheduleDto> Schedules { get; set; } = [];
}

public class PayrollLoanScheduleDto
{
    public Guid Id { get; set; }
    public Guid PayrollLoanId { get; set; }
    public int SequenceNo { get; set; }
    public DateTime RepaymentDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public DateTime? ActualRepaymentDate { get; set; }
    public bool Posted { get; set; }
}

public class PayrollLoanRepaymentDto
{
    public Guid PayrollLoanId { get; set; }
    public Guid PayrollLoanScheduleId { get; set; }
    public int LoanSequenceNo { get; set; }
    public string FacilityNumber { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public decimal RepaymentAmount { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal LoanBalance { get; set; }
    public DateTime ActualRepaymentDate { get; set; }
    public PayrollLoanDto Loan { get; set; } = new();
}

public class PayrollLoanRepaymentRequestDto
{
    public Guid? PayrollLoanId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FacilityNumber { get; set; } = string.Empty;
    public decimal RepaymentAmount { get; set; }
    public DateTime? ActualRepaymentDate { get; set; }
}

public class UpsertPayrollLoanDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid? LoanPolicyId { get; set; }
    public string? LoanTypeCode { get; set; }
    public string FacilityNumber { get; set; } = string.Empty;
    public DateTime DateGranted { get; set; } = DateTime.UtcNow.Date;
    public decimal AmountGranted { get; set; }
    public decimal MonthlyRepaymentAmount { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public decimal? InterestRatePercent { get; set; }
    public decimal? TotalInterest { get; set; }
    public decimal? InterestRepaymentAmount { get; set; }
    public string RepaymentMode { get; set; } = "Amount";
    public DateTime? PaymentStartDate { get; set; }
    public DateTime? PaymentEndDate { get; set; }
    public int NumberOfRepayments { get; set; }
    public string Status { get; set; } = "Active";
    public int? PeriodOfSuspension { get; set; }
    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }
    public string? SuspensionNarration { get; set; }
    public string? GeneralRemarks { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayrollSalaryAdvanceDto
{
    public Guid Id { get; set; }
    public Guid EmployeeProfileId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime AdvanceDate { get; set; }
    public decimal AdvanceAmount { get; set; }
    public string? Description { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPayrollSalaryAdvanceDto
{
    public Guid? Id { get; set; }
    public Guid? EmployeeProfileId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateTime AdvanceDate { get; set; } = DateTime.UtcNow.Date;
    public decimal AdvanceAmount { get; set; }
    public string? Description { get; set; }
    public string? LegacyCompanyCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayrollImportBatchDto
{
    public Guid Id { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string ImportType { get; set; } = "EmployeeReconciliation";
    public int TotalRows { get; set; }
    public int MatchedRows { get; set; }
    public int MissingRows { get; set; }
    public int DuplicateRows { get; set; }
    public DateTime ImportedAt { get; set; }
    public Guid? ImportedByUserId { get; set; }
    public IReadOnlyList<PayrollImportRowDto> Rows { get; set; } = [];
}

public class PayrollImportRowDto
{
    public Guid Id { get; set; }
    public int RowNumber { get; set; }
    public string LegacyEmployeeNumber { get; set; } = string.Empty;
    public string? LegacyFullName { get; set; }
    public string? Department { get; set; }
    public Guid? MatchedEmployeeId { get; set; }
    public string? MatchedEmployeeNumber { get; set; }
    public PayrollImportRowStatus Status { get; set; }
    public string? Message { get; set; }
}

public class PayrollReconciliationImportDto
{
    public string SourceName { get; set; } = "Oracle Forms payroll import";
    public IReadOnlyList<PayrollReconciliationImportRowDto> Rows { get; set; } = [];
}

public class PayrollReconciliationImportRowDto
{
    public int RowNumber { get; set; }
    public string LegacyEmployeeNumber { get; set; } = string.Empty;
    public string? LegacyEmployeeId { get; set; }
    public string? LegacyFullName { get; set; }
    public string? Department { get; set; }
}

public class PayrollRunDto
{
    public Guid Id { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public DateTime RunDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool IsSeparateBonusRun { get; set; }
    public string? SeparateBonusCode { get; set; }
    public PayrollRunStatus Status { get; set; }
    public int EmployeeCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal EmployeeContributionAmount { get; set; }
    public decimal EmployerContributionAmount { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<PayrollRunEmployeeDto> Employees { get; set; } = [];
    public IReadOnlyList<PayrollTransactionDto> Transactions { get; set; } = [];
    public IReadOnlyList<PayrollJournalLineDto> JournalLines { get; set; } = [];
}

public class CreatePayrollRunDto
{
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public DateTime? RunDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool IsSeparateBonusRun { get; set; }
    public string? SeparateBonusCode { get; set; }
    public string? Notes { get; set; }
}

public class PayrollRunActionDto
{
    public string? Notes { get; set; }
}

public class PayrollRunEmployeeDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal TaxableAllowances { get; set; }
    public decimal NonTaxableAllowances { get; set; }
    public decimal TaxableDeductions { get; set; }
    public decimal NonTaxableDeductions { get; set; }
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal TaxRelief { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal NetIncome { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
}

public class PayrollTransactionDto
{
    public Guid Id { get; set; }
    public Guid PayrollRunEmployeeId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public Guid? PayrollComponentId { get; set; }
    public string? ComponentCode { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? EmployerAmount { get; set; }
    public bool Taxable { get; set; }
    public bool EmployerTaxable { get; set; }
    public bool SeparateTax { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
}

public class PayrollJournalLineDto
{
    public Guid Id { get; set; }
    public int SequenceNo { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string DebitCredit { get; set; } = "DR";
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool Posted { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public class PayrollSummaryReportDto
{
    public Guid PayrollRunId { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public int EmployeeCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal EmployeeContributionAmount { get; set; }
    public decimal EmployerContributionAmount { get; set; }
    public IReadOnlyList<PayrollSummaryComponentDto> Components { get; set; } = [];
    public IReadOnlyList<PayrollSummaryEmployeeDto> Employees { get; set; } = [];
    public PayrollReportSnapshotDto? Snapshot { get; set; }
}

public class PayrollSummaryComponentDto
{
    public string TransactionType { get; set; } = string.Empty;
    public string? ComponentCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal EmployerAmount { get; set; }
}

public class PayrollSummaryEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal TaxRelief { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal NormalIncomeTax { get; set; }
    public decimal BonusIncomeTax { get; set; }
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal NetIncome { get; set; }
}

public class PayrollPayslipDto
{
    public Guid PayrollRunId { get; set; }
    public Guid PayrollRunEmployeeId { get; set; }
    public Guid EmployeeId { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public int PayPeriod { get; set; }
    public DateTime PayPeriodFrom { get; set; }
    public DateTime PayPeriodTo { get; set; }
    public bool IsSeparateBonusRun { get; set; }
    public string? SeparateBonusCode { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeEmail { get; set; }
    public string? DepartmentName { get; set; }
    public string? SectionName { get; set; }
    public string? PositionTitle { get; set; }
    public string? StaffCategory { get; set; }
    public string? JobLocation { get; set; }
    public string? SsfNumber { get; set; }
    public string? StaffTin { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal BasicSalary { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal TaxRelief { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal NormalIncomeTax { get; set; }
    public decimal BonusIncomeTax { get; set; }
    public decimal OvertimeIncomeTax { get; set; }
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal NetIncome { get; set; }
    public PayrollPayslipOvertimeDto? Overtime { get; set; }
    public IReadOnlyList<PayrollTransactionDto> Earnings { get; set; } = [];
    public IReadOnlyList<PayrollTransactionDto> Deductions { get; set; } = [];
    public IReadOnlyList<PayrollPayslipContributionDto> Contributions { get; set; } = [];
    public IReadOnlyList<PayrollPayslipBankDetailDto> BankDetails { get; set; } = [];
    public PayrollPayslipSnapshotDto? Snapshot { get; set; }
}

public class PayrollPayslipOvertimeDto
{
    public decimal WorkingHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal HolidayHours { get; set; }
    public decimal SaturdayHours { get; set; }
    public decimal SundayHours { get; set; }
    public decimal DaySixAndSevenHours { get; set; }
    public decimal TotalHours { get; set; }
}

public class PayrollPayslipContributionDto
{
    public string Item { get; set; } = string.Empty;
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal TotalContribution { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalWithdrawal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal FirstTier { get; set; }
    public decimal SecondTier { get; set; }
    public bool IsProvidentFund { get; set; }
}

public class PayrollPayslipBankDetailDto
{
    public string BankName { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal? ExchangeRate { get; set; }
    public decimal Amount { get; set; }
}

public class PayrollReportSnapshotDto
{
    public Guid Id { get; set; }
    public Guid PayrollRunId { get; set; }
    public PayrollReportType ReportType { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string SnapshotNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedByUserId { get; set; }
    public string? Notes { get; set; }
}

public class PayrollPayslipSnapshotDto
{
    public Guid Id { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid PayrollRunEmployeeId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string PayslipNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedByUserId { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal NetIncome { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal EmployeeContribution { get; set; }
}

public class PayrollJournalPostingDto
{
    public Guid PayrollRunId { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public Guid JournalEntryId { get; set; }
    public string JournalNumber { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateTime PostedAt { get; set; }
    public JournalEntryDto? JournalEntry { get; set; }
    public IReadOnlyList<PayrollJournalLineDto> Lines { get; set; } = [];
}

public class PayrollPayslipEmailRequestDto
{
    public IReadOnlyList<Guid> EmployeeIds { get; set; } = [];
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public bool AttachHtmlCopy { get; set; } = true;
}

public class PayrollPayslipEmailResultDto
{
    public Guid PayrollRunId { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<PayrollPayslipEmailRecipientDto> Recipients { get; set; } = [];
}

public class PayrollPayslipEmailRecipientDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public bool Sent { get; set; }
    public string Message { get; set; } = string.Empty;
}
