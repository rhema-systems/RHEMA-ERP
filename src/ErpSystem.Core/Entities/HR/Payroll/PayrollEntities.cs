using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.HR.Payroll;

public enum PayrollComponentType
{
    Allowance = 1,
    Deduction = 2,
    Benefit = 3,
    EmployeeContribution = 4,
    EmployerContribution = 5
}

public enum PayrollCalculationType
{
    FixedAmount = 1,
    PercentageOfBasic = 2
}

public enum PayrollRunStatus
{
    Draft = 1,
    Calculated = 2,
    InReview = 3,
    Approved = 4,
    Closed = 5,
    RolledBack = 6
}

public enum PayrollImportRowStatus
{
    Matched = 1,
    MissingEmployee = 2,
    DuplicateEmployeeNumber = 3
}

public enum PayrollReportType
{
    RunSummary = 1,
    Payslip = 2,
    BankSchedule = 3,
    TaxSchedule = 4,
    PensionSchedule = 5,
    JournalSummary = 6
}

public class PayrollBudgetAnalysisRow : TenantEntity
{
    public int OrderField { get; set; }

    public int PayPeriod { get; set; }

    public DateTime PayPeriodFrom { get; set; }

    public DateTime PayPeriodTo { get; set; }

    [Required, MaxLength(5)]
    public string TransactionType { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string ActualTransaction { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public bool Percentage { get; set; }

    public decimal BaseAmount { get; set; }

    public decimal Amount1 { get; set; }

    public bool Include1 { get; set; } = true;

    public decimal NewAmount1 { get; set; }

    public decimal Amount2 { get; set; }

    public bool Include2 { get; set; } = true;

    public decimal NewAmount2 { get; set; }

    public decimal Amount3 { get; set; }

    public bool Include3 { get; set; } = true;

    public decimal NewAmount3 { get; set; }

    [Required, MaxLength(5)]
    public string CompanyCode { get; set; } = "001";
}

public class PayrollParameterSet : TenantEntity
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(8)]
    public string BaseCurrency { get; set; } = "GHS";

    [MaxLength(15)]
    public string DateFormat { get; set; } = "dd-MM-yyyy";

    public bool MultiLoginEnabled { get; set; }

    public int? MaxLoginCount { get; set; }

    public int CurrentPayPeriod { get; set; }

    public DateTime? CurrentPeriodFrom { get; set; }

    public DateTime? CurrentPeriodTo { get; set; }

    public int MonthDays { get; set; } = 30;

    public int PayFrequencyMonths { get; set; } = 1;

    [MaxLength(1)]
    public string? PayMode { get; set; }

    [MaxLength(1)]
    public string? TimeSheetMode { get; set; }

    [MaxLength(5)]
    public string? LeaveClassification { get; set; }

    public bool MultiplePayBasisEnabled { get; set; }

    [MaxLength(12)]
    public string? DefaultPayBasis { get; set; }

    public bool AllowEmployeePaymentMethod { get; set; }

    [MaxLength(15)]
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

    [MaxLength(8)]
    public string? ReportingCurrency { get; set; }

    [MaxLength(200)]
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

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollComponent : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public PayrollComponentType ComponentType { get; set; }

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public decimal Rate { get; set; }

    public decimal? TaxFreeCeiling { get; set; }

    public decimal? SeparateTaxPercent { get; set; }

    public decimal? MaxBenefitToTax { get; set; }

    public decimal? EmployerAmount { get; set; }

    [MaxLength(30)]
    public string? Category { get; set; }

    [MaxLength(20)]
    public string? Cycle { get; set; }

    public int? NextPayPeriod { get; set; }

    public DateTime? NextPayPeriodDate { get; set; }

    public DateTime? LastPayDate { get; set; }

    public bool Taxable { get; set; }

    public bool EmployerTaxable { get; set; }

    public bool SeparateTax { get; set; }

    public bool ApplyToBenefit { get; set; }

    public bool AfterTaxContribution { get; set; }

    public bool IncludeInGross { get; set; } = true;

    public bool GrossUp { get; set; }

    public bool Prorate { get; set; }

    public bool AppliesByDefault { get; set; }

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public bool IsActive { get; set; } = true;

    public virtual ICollection<PayrollEmployeeComponent> EmployeeComponents { get; set; } = new List<PayrollEmployeeComponent>();
    public virtual ICollection<PayrollTransaction> Transactions { get; set; } = new List<PayrollTransaction>();
}

public class PayrollComponentRule : TenantEntity
{
    public PayrollComponentType ComponentType { get; set; }

    [Required, MaxLength(20)]
    public string ComponentCode { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Category { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public decimal? TaxFreeCeiling { get; set; }

    public decimal? EmployerAmount { get; set; }

    public bool AfterTax { get; set; }

    public bool EmployerTaxable { get; set; }

    public bool Applicable { get; set; } = true;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollTaxBand : TenantEntity
{
    [Required, MaxLength(20)]
    public string TaxType { get; set; } = "PAYE";

    public int SerialNo { get; set; }

    [MaxLength(30)]
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

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsAnnual { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollTaxRelief : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public decimal Factor { get; set; } = 1;

    public bool AppliesByDefault { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollEmployeeTaxRelief : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ReliefCode { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ReliefName { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public decimal Factor { get; set; } = 1;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
}

public class PayrollPensionScheme : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public decimal EmployeeRatePercent { get; set; }

    public decimal EmployerRatePercent { get; set; }

    public decimal? ContributionLimit { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollOvertimePolicy : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
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

    [MaxLength(1)]
    public string? MaxOvertimeType { get; set; }

    public decimal? LeaveRecallRate { get; set; }

    public int? PayPeriod { get; set; }

    public DateTime? PayPeriodFrom { get; set; }

    public DateTime? PayPeriodTo { get; set; }

    public decimal? MaxOvertimeSeparateTax { get; set; }

    public decimal? MinimumBasicForSeparateTax { get; set; }

    public decimal? MinimumSeparateOvertimePercent { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<PayrollOvertimeRange> Ranges { get; set; } = new List<PayrollOvertimeRange>();
}

public class PayrollLoanPolicy : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public decimal? MaxLoanAmount { get; set; }

    public decimal? InterestRatePercent { get; set; }

    public bool ApplyInterest { get; set; }

    public int? MaxPaybackPeriods { get; set; }

    public decimal? MaxDebitRatioPercent { get; set; }

    [MaxLength(20)]
    public string InterestType { get; set; } = "Flat";

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollBonusPolicy : TenantEntity
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public bool Taxable { get; set; } = true;

    public bool SeparateTax { get; set; }

    public decimal? TaxFreeCeiling { get; set; }

    public decimal? MinimumToTax { get; set; }

    public decimal? AnnualSalaryPercentToTax { get; set; }

    public decimal? TaxRate { get; set; }

    public int? NextPayPeriod { get; set; }

    public int? LastPayPeriod { get; set; }

    public DateTime? NextPayPeriodDate { get; set; }

    public DateTime? LastPayPeriodDate { get; set; }

    [MaxLength(30)]
    public string? Category { get; set; }

    [MaxLength(20)]
    public string? Cycle { get; set; }

    public bool PaySeparate { get; set; }

    public bool PerAnnual { get; set; }

    public int? MinimumMonths { get; set; }

    public bool Prorate { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollBonusRule : TenantEntity
{
    [Required, MaxLength(20)]
    public string BonusCode { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string GroupCode { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public bool Applicable { get; set; } = true;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollBonusException : TenantEntity
{
    [Required, MaxLength(20)]
    public string BonusCode { get; set; } = string.Empty;

    public Guid? EmployeeProfileId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string EmployeeName { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public bool Taxable { get; set; } = true;

    public bool Applicable { get; set; } = true;

    [MaxLength(8)]
    public string? CurrencyCode { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual PayrollEmployeeProfile? EmployeeProfile { get; set; }
}

public class PayrollBackpayPolicy : TenantEntity
{
    [Required, MaxLength(30)]
    public string OperationType { get; set; } = "IncreaseSalary";

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public int? NumberOfMonths { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public bool ApplyTax { get; set; }

    public bool ApplySsf { get; set; }

    [MaxLength(30)]
    public string? MinimumServiceMode { get; set; }

    public decimal? MinimumServiceValue { get; set; }

    public DateTime? MinimumServiceDate { get; set; }

    [MaxLength(30)]
    public string CategoryType { get; set; } = "All Staff";

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollBackpayRule : TenantEntity
{
    [Required, MaxLength(30)]
    public string OperationType { get; set; } = "IncreaseSalary";

    [Required, MaxLength(30)]
    public string CategoryType { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? CategoryName { get; set; }

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public bool Applicable { get; set; } = true;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollBackpayException : TenantEntity
{
    [Required, MaxLength(30)]
    public string OperationType { get; set; } = "SalaryArrears";

    public Guid? EmployeeProfileId { get; set; }

    [Required, MaxLength(30)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string EmployeeName { get; set; } = string.Empty;

    public PayrollCalculationType CalculationType { get; set; } = PayrollCalculationType.FixedAmount;

    public decimal Amount { get; set; }

    public bool Applicable { get; set; } = true;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollJournalMapping : TenantEntity
{
    public int SequenceNo { get; set; }

    [Required, MaxLength(40)]
    public string TransactionType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ComponentCode { get; set; }

    [MaxLength(30)]
    public string? ShortDescription { get; set; }

    [Required, MaxLength(120)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(2)]
    public string DebitCredit { get; set; } = "DR";

    [Required, MaxLength(100)]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? AccountType { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PayrollCodeType : TenantEntity
{
    [Required, MaxLength(5)]
    public string CodeType { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? AccountCode { get; set; }

    public bool Blocked { get; set; }

    public bool Dependent { get; set; }

    [MaxLength(5)]
    public string? DependentOnCodeType { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual ICollection<PayrollCodeValue> Values { get; set; } = new List<PayrollCodeValue>();
}

public class PayrollCodeValue : TenantEntity
{
    public Guid PayrollCodeTypeId { get; set; }

    [Required, MaxLength(5)]
    public string CodeType { get; set; } = string.Empty;

    [Required, MaxLength(15)]
    public string ActualCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? AdditionalDescription { get; set; }

    [MaxLength(30)]
    public string? AccountCode { get; set; }

    public bool Blocked { get; set; }

    [MaxLength(5)]
    public string? DependentCodeType { get; set; }

    [MaxLength(5)]
    public string? DependentActualCode { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual PayrollCodeType PayrollCodeType { get; set; } = null!;
}

public class PayrollHoliday : TenantEntity
{
    public DateTime HolidayDate { get; set; }

    [Required, MaxLength(100)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollNonWorkingDay : TenantEntity
{
    [Required, MaxLength(1)]
    public string DayCode { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Description { get; set; } = string.Empty;

    public decimal? OvertimeRate { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollExchangeRate : TenantEntity
{
    [Required, MaxLength(5)]
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public int PayPeriod { get; set; }

    public DateTime PayPeriodFrom { get; set; }

    public DateTime PayPeriodTo { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollBankBranch : TenantEntity
{
    [MaxLength(5)]
    public string? BranchSetupCode { get; set; }

    [Required, MaxLength(8)]
    public string BankCode { get; set; } = string.Empty;

    [Required, MaxLength(8)]
    public string BranchCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string BranchDescription { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(30)]
    public string? SortCode { get; set; }

    [MaxLength(30)]
    public string? AccountCode { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }
}

public class PayrollLeaveSetup : TenantEntity
{
    [Required, MaxLength(5)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string CategoryDetail { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual ICollection<PayrollLeaveSetupDetail> Details { get; set; } = new List<PayrollLeaveSetupDetail>();
}

public class PayrollLeaveSetupDetail : TenantEntity
{
    public Guid PayrollLeaveSetupId { get; set; }

    [Required, MaxLength(5)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string CategoryDetail { get; set; } = string.Empty;

    public int? Days { get; set; }

    public int? ServiceFrom { get; set; }

    public int? ServiceTo { get; set; }

    public int? SequenceNo { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual PayrollLeaveSetup PayrollLeaveSetup { get; set; } = null!;
}

public class PayrollOvertimeRange : TenantEntity
{
    public Guid PayrollOvertimePolicyId { get; set; }

    public decimal MinRange { get; set; }

    public decimal MaxRange { get; set; }

    public decimal Rate { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual PayrollOvertimePolicy PayrollOvertimePolicy { get; set; } = null!;
}

public class PayrollLegacyMenuUser : TenantEntity
{
    [Required, MaxLength(10)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string UserGroup { get; set; } = string.Empty;

    [Required, MaxLength(6)]
    public string UserNo { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? LegacyPasswordHash { get; set; }

    public bool LoginEnabled { get; set; } = true;

    public bool Locked { get; set; }

    public bool PasswordChangeRequired { get; set; } = true;

    public DateTime? PasswordExpiryDate { get; set; }

    public int PasswordFailureCount { get; set; }

    [MaxLength(5)]
    public string? CompanyId { get; set; }

    [MaxLength(5)]
    public string? BusinessUnitId { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool CanViewSalary { get; set; }

    public bool CanApprove { get; set; }

    [MaxLength(50)]
    public string? RefreshToken { get; set; }

    [MaxLength(50)]
    public string? LastPasswordChange { get; set; }
}

public class PayrollLegacyMenuSecurity : TenantEntity
{
    [Required, MaxLength(10)]
    public string FormCode { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string FormName { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string UserName { get; set; } = string.Empty;

    public bool Allowed { get; set; } = true;

    [Required, MaxLength(5)]
    public string GroupName { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    [MaxLength(100)]
    public string? TopMenu { get; set; }

    [MaxLength(100)]
    public string? SubMenu { get; set; }

    public bool CanEdit { get; set; }
}

public class PayrollCompanyProfile : TenantEntity
{
    [Required, MaxLength(5)]
    public string CompanyId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? CompanySystemName { get; set; }

    [MaxLength(5)]
    public string? CompanyCode { get; set; }

    public bool MultipleBusinessUnits { get; set; }

    public int? BusinessNumber { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(200)]
    public string? AddressLine3 { get; set; }

    [MaxLength(60)]
    public string? CityOrTown { get; set; }

    [MaxLength(60)]
    public string? RegionOrState { get; set; }

    [MaxLength(60)]
    public string? Country { get; set; }

    [MaxLength(60)]
    public string? LicenceType { get; set; }

    public int? LicenceNo { get; set; }

    [MaxLength(30)]
    public string? SocialSecurityNo { get; set; }

    [MaxLength(30)]
    public string? TaxpayerIdNo { get; set; }

    [MaxLength(30)]
    public string? PictureName { get; set; }

    [MaxLength(100)]
    public string? LogoName { get; set; }

    public bool SplitLicenceOnBusinessUnits { get; set; }

    [MaxLength(1)]
    public string? LicencePercentOrNumber { get; set; }

    public bool MakeCompanyABusinessUnit { get; set; }

    public bool MultiplePayBasis { get; set; }

    [MaxLength(15)]
    public string? CompanyPayBasis { get; set; }

    public bool MultipleEmployeePaymentMethods { get; set; }

    [MaxLength(15)]
    public string? DefaultEmployeePaymentMethod { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<PayrollBusinessUnit> BusinessUnits { get; set; } = new List<PayrollBusinessUnit>();
    public virtual ICollection<PayrollCompanyBanker> Bankers { get; set; } = new List<PayrollCompanyBanker>();
}

public class PayrollBusinessUnit : TenantEntity
{
    public Guid? PayrollCompanyProfileId { get; set; }

    [Required, MaxLength(5)]
    public string BusinessUnitId { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? BusinessUnitCode { get; set; }

    [MaxLength(200)]
    public string? BusinessUnitName { get; set; }

    [MaxLength(60)]
    public string? Location { get; set; }

    [MaxLength(60)]
    public string? Country { get; set; }

    public bool DistributeLicence { get; set; }

    [MaxLength(1)]
    public string? LicencePercentOrNumber { get; set; }

    public int? LicenceValue { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(200)]
    public string? AddressLine3 { get; set; }

    [MaxLength(60)]
    public string? RegionOrState { get; set; }

    [MaxLength(5)]
    public string? CompanyId { get; set; }

    [MaxLength(5)]
    public string? CompanyCode { get; set; }

    [MaxLength(30)]
    public string? AccountsCode { get; set; }

    public bool MultiplePayBasis { get; set; }

    [MaxLength(15)]
    public string? CompanyPayBasis { get; set; }

    public bool MultipleEmployeePaymentMethods { get; set; }

    [MaxLength(15)]
    public string? DefaultEmployeePaymentMethod { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public virtual PayrollCompanyProfile? PayrollCompanyProfile { get; set; }
}

public class PayrollCompanyBanker : TenantEntity
{
    public Guid? PayrollCompanyProfileId { get; set; }

    [Required, MaxLength(5)]
    public string BankCode { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? BankBranch { get; set; }

    [MaxLength(100)]
    public string? Address1 { get; set; }

    [MaxLength(100)]
    public string? Address2 { get; set; }

    [MaxLength(100)]
    public string? Address3 { get; set; }

    [MaxLength(30)]
    public string? AccountNumber1 { get; set; }

    [MaxLength(30)]
    public string? AccountNumber2 { get; set; }

    [MaxLength(30)]
    public string? AccountNumber3 { get; set; }

    [Required, MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string CompanyId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Signatory1 { get; set; }

    [MaxLength(100)]
    public string? Signatory2 { get; set; }

    [MaxLength(100)]
    public string? Signatory3 { get; set; }

    [MaxLength(100)]
    public string? SignatoryPosition1 { get; set; }

    [MaxLength(100)]
    public string? SignatoryPosition2 { get; set; }

    [MaxLength(100)]
    public string? SignatoryPosition3 { get; set; }

    [MaxLength(5)]
    public string? BankRegion { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollCompanyProfile? PayrollCompanyProfile { get; set; }
}

public class PayrollGrade : TenantEntity
{
    [MaxLength(5)]
    public string? GradeId { get; set; }

    [MaxLength(15)]
    public string? GradeType { get; set; }

    [Required, MaxLength(60)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? SystemGradeName { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public decimal? MidPoint { get; set; }

    [Required, MaxLength(5)]
    public string CurrencyCode { get; set; } = "GHS";

    [MaxLength(5)]
    public string? StartPoint { get; set; }

    public decimal? IncrementStep { get; set; }

    [MaxLength(5)]
    public string? Ceiling { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(5)]
    public string? BusinessUnitId { get; set; }

    [MaxLength(5)]
    public string? CompanyId { get; set; }

    public int? OrderField { get; set; }

    [MaxLength(100)]
    public string? ReportingName { get; set; }

    public bool EnforceNotchConsistency { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<PayrollGradeNotch> Notches { get; set; } = new List<PayrollGradeNotch>();
}

public class PayrollGradeNotch : TenantEntity
{
    public Guid PayrollGradeId { get; set; }

    [MaxLength(5)]
    public string? GradeId { get; set; }

    [MaxLength(15)]
    public string? SystemGradeName { get; set; }

    [Required, MaxLength(5)]
    public string Notch { get; set; } = string.Empty;

    public decimal? Value { get; set; }

    [Required, MaxLength(5)]
    public string CurrencyCode { get; set; } = "GHS";

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int? OrderField { get; set; }

    [MaxLength(5)]
    public string? BusinessUnitId { get; set; }

    [MaxLength(5)]
    public string? CompanyId { get; set; }

    public decimal? AnnualisedValue { get; set; }

    [MaxLength(60)]
    public string? GradeName { get; set; }

    [MaxLength(100)]
    public string? ReportingName { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public decimal? HourlyRate { get; set; }

    public virtual PayrollGrade PayrollGrade { get; set; } = null!;
}

public class PayrollEmployeeProfile : TenantEntity
{
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LegacyEmployeeId { get; set; }

    [MaxLength(50)]
    public string? LegacyEmployeeNumber { get; set; }

    public bool PayrollActive { get; set; } = true;

    public bool PayTax { get; set; } = true;

    public bool SsfApplicable { get; set; } = true;

    public bool GrossUp { get; set; }

    public bool Tier2Only { get; set; }

    public bool OvertimeEligible { get; set; }

    [MaxLength(50)]
    public string? SsfNumber { get; set; }

    [MaxLength(50)]
    public string? TinNumber { get; set; }

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public Guid? DefaultPaymentMethodId { get; set; }

    public virtual Employee Employee { get; set; } = null!;
    public virtual PayrollPaymentMethod? DefaultPaymentMethod { get; set; }
    public virtual PayrollSalaryBasis? SalaryBasis { get; set; }
    public virtual ICollection<PayrollPaymentMethod> PaymentMethods { get; set; } = new List<PayrollPaymentMethod>();
    public virtual ICollection<PayrollEmployeeComponent> EmployeeComponents { get; set; } = new List<PayrollEmployeeComponent>();
    public virtual ICollection<PayrollLoan> Loans { get; set; } = new List<PayrollLoan>();
    public virtual ICollection<PayrollSalaryAdvance> SalaryAdvances { get; set; } = new List<PayrollSalaryAdvance>();
}

public class PayrollSalaryBasis : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public decimal MonthlyBasicSalary { get; set; }

    public decimal? AnnualBasicSalary { get; set; }

    public decimal? HourlyRate { get; set; }

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
}

public class PayrollPaymentMethod : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    [Required, MaxLength(40)]
    public string PaymentType { get; set; } = "Bank";

    [Required, MaxLength(20)]
    public string PaymentMode { get; set; } = "Percentage";

    public decimal? PaymentPercent { get; set; }

    public decimal? Amount { get; set; }

    [MaxLength(20)]
    public string? BankCode { get; set; }

    [MaxLength(20)]
    public string? BankBranchCode { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [MaxLength(30)]
    public string? ChequeNumber { get; set; }

    [MaxLength(20)]
    public string? ChequeBankCode { get; set; }

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public decimal? ExchangeRate { get; set; }

    public int SequenceNo { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
}

public class PayrollEmployeeComponent : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid PayrollComponentId { get; set; }

    public PayrollCalculationType? CalculationTypeOverride { get; set; }

    public decimal? AmountOverride { get; set; }

    public decimal? RateOverride { get; set; }

    public bool? TaxableOverride { get; set; }

    public decimal? TaxFreeCeilingOverride { get; set; }

    public decimal? EmployerAmountOverride { get; set; }

    public bool? EmployerTaxableOverride { get; set; }

    public bool? GrossUpOverride { get; set; }

    [MaxLength(8)]
    public string? CurrencyCodeOverride { get; set; }

    public bool Applicable { get; set; } = true;

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual PayrollComponent PayrollComponent { get; set; } = null!;
}

public class PayrollLoan : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid? LoanPolicyId { get; set; }

    [MaxLength(40)]
    public string? LoanTypeCode { get; set; }

    [Required, MaxLength(40)]
    public string FacilityNumber { get; set; } = string.Empty;

    public DateTime DateGranted { get; set; }

    public decimal AmountGranted { get; set; }

    public decimal MonthlyRepaymentAmount { get; set; }

    public decimal OutstandingBalance { get; set; }

    public decimal InterestRatePercent { get; set; }

    public decimal TotalInterest { get; set; }

    public decimal InterestRepaymentAmount { get; set; }

    [Required, MaxLength(30)]
    public string RepaymentMode { get; set; } = "Amount";

    public DateTime PaymentStartDate { get; set; }

    public DateTime? PaymentEndDate { get; set; }

    public int NumberOfRepayments { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Active";

    public int? PeriodOfSuspension { get; set; }

    public DateTime? SuspensionStartDate { get; set; }

    public DateTime? SuspensionEndDate { get; set; }

    [MaxLength(500)]
    public string? SuspensionNarration { get; set; }

    [MaxLength(500)]
    public string? GeneralRemarks { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual PayrollLoanPolicy? LoanPolicy { get; set; }
    public virtual ICollection<PayrollLoanSchedule> Schedules { get; set; } = new List<PayrollLoanSchedule>();
}

public class PayrollLoanSchedule : TenantEntity
{
    public Guid PayrollLoanId { get; set; }

    public int SequenceNo { get; set; }

    public DateTime RepaymentDate { get; set; }

    public decimal PrincipalAmount { get; set; }

    public decimal InterestAmount { get; set; }

    public decimal AmountPaid { get; set; }

    public decimal InterestPaid { get; set; }

    public DateTime? ActualRepaymentDate { get; set; }

    public bool Posted { get; set; }

    public virtual PayrollLoan PayrollLoan { get; set; } = null!;
}

public class PayrollSalaryAdvance : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    public DateTime AdvanceDate { get; set; }

    public decimal AdvanceAmount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
}

public class PayrollPromotionArrearsEntry : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LegacyEmployeeId { get; set; }

    public DateTime EffectiveDate { get; set; }

    public int PayPeriod { get; set; }

    public DateTime PayPeriodFrom { get; set; }

    public DateTime PayPeriodTo { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public decimal? WorkingDays { get; set; }

    public decimal? BasicSalary { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
}

public class PayrollTimesheetSummary : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LegacyEmployeeId { get; set; }

    public int PayPeriod { get; set; }

    public DateTime PayPeriodFrom { get; set; }

    public DateTime PayPeriodTo { get; set; }

    public decimal NormalHours { get; set; }

    public decimal WeekdayHours { get; set; }

    public decimal HolidayHours { get; set; }

    public decimal SaturdayHours { get; set; }

    public decimal SundayHours { get; set; }

    public decimal AbsentHours { get; set; }

    public decimal NightShiftCount { get; set; }

    public decimal AttendanceCount { get; set; }

    public decimal OvertimeAmount { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    [MaxLength(10)]
    public string RecordSource { get; set; } = "S";

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual Employee? Employee { get; set; }
}

public class PayrollContributionOpeningBalance : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LegacyEmployeeId { get; set; }

    [Required, MaxLength(20)]
    public string ContributionCodeType { get; set; } = "CON";

    [Required, MaxLength(50)]
    public string ContributionCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContributionName { get; set; } = string.Empty;

    public DateTime BalanceAsAt { get; set; }

    public decimal OpeningBalance { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual Employee? Employee { get; set; }
}

public class PayrollContributionTransaction : TenantEntity
{
    public Guid EmployeeProfileId { get; set; }

    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LegacyEmployeeId { get; set; }

    [Required, MaxLength(20)]
    public string ContributionCodeType { get; set; } = "CON";

    [Required, MaxLength(50)]
    public string ContributionCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContributionName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string TransactionType { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(5)]
    public string? LegacyCompanyCode { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PayrollEmployeeProfile EmployeeProfile { get; set; } = null!;
    public virtual Employee? Employee { get; set; }
}

public class PayrollImportBatch : TenantEntity
{
    [Required, MaxLength(80)]
    public string SourceName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string ImportType { get; set; } = "EmployeeReconciliation";

    public int TotalRows { get; set; }

    public int MatchedRows { get; set; }

    public int MissingRows { get; set; }

    public int DuplicateRows { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public Guid? ImportedByUserId { get; set; }

    public virtual ICollection<PayrollImportRow> Rows { get; set; } = new List<PayrollImportRow>();
}

public class PayrollImportRow : TenantEntity
{
    public Guid PayrollImportBatchId { get; set; }

    public int RowNumber { get; set; }

    [Required, MaxLength(50)]
    public string LegacyEmployeeNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LegacyFullName { get; set; }

    [MaxLength(120)]
    public string? Department { get; set; }

    public Guid? MatchedEmployeeId { get; set; }

    [MaxLength(50)]
    public string? MatchedEmployeeNumber { get; set; }

    public PayrollImportRowStatus Status { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    public virtual PayrollImportBatch PayrollImportBatch { get; set; } = null!;
    public virtual Employee? MatchedEmployee { get; set; }
}

public class PayrollRun : TenantEntity
{
    [Required, MaxLength(40)]
    public string RunNumber { get; set; } = string.Empty;

    public int PayPeriod { get; set; }

    public DateTime PayPeriodFrom { get; set; }

    public DateTime PayPeriodTo { get; set; }

    public DateTime RunDate { get; set; } = DateTime.UtcNow;

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public bool IsSeparateBonusRun { get; set; }

    [MaxLength(20)]
    public string? SeparateBonusCode { get; set; }

    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    public int EmployeeCount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal EmployeeContributionAmount { get; set; }

    public decimal EmployerContributionAmount { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public Guid? CalculatedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedByUserId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual ICollection<PayrollRunEmployee> Employees { get; set; } = new List<PayrollRunEmployee>();
    public virtual ICollection<PayrollTransaction> Transactions { get; set; } = new List<PayrollTransaction>();
    public virtual ICollection<PayrollJournalLine> JournalLines { get; set; } = new List<PayrollJournalLine>();
    public virtual ICollection<PayrollReportSnapshot> ReportSnapshots { get; set; } = new List<PayrollReportSnapshot>();
    public virtual ICollection<PayrollPayslipSnapshot> PayslipSnapshots { get; set; } = new List<PayrollPayslipSnapshot>();
}

public class PayrollRunEmployee : TenantEntity
{
    public Guid PayrollRunId { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
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

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public virtual PayrollRun PayrollRun { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
    public virtual ICollection<PayrollTransaction> Transactions { get; set; } = new List<PayrollTransaction>();
}

public class PayrollTransaction : TenantEntity
{
    public Guid PayrollRunId { get; set; }

    public Guid PayrollRunEmployeeId { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string TransactionType { get; set; } = string.Empty;

    public Guid? PayrollComponentId { get; set; }

    [MaxLength(20)]
    public string? ComponentCode { get; set; }

    [MaxLength(160)]
    public string? Description { get; set; }

    public decimal Amount { get; set; }

    public decimal? EmployerAmount { get; set; }

    public bool Taxable { get; set; }

    public bool EmployerTaxable { get; set; }

    public bool SeparateTax { get; set; }

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public virtual PayrollRun PayrollRun { get; set; } = null!;
    public virtual PayrollRunEmployee PayrollRunEmployee { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
    public virtual PayrollComponent? PayrollComponent { get; set; }
}

public class PayrollJournalLine : TenantEntity
{
    public Guid PayrollRunId { get; set; }

    public int SequenceNo { get; set; }

    [Required, MaxLength(40)]
    public string TransactionType { get; set; } = string.Empty;

    [Required, MaxLength(2)]
    public string DebitCredit { get; set; } = "DR";

    [Required, MaxLength(100)]
    public string AccountCode { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public bool Posted { get; set; }

    public Guid? JournalEntryId { get; set; }

    public virtual PayrollRun PayrollRun { get; set; } = null!;
}

public class PayrollReportSnapshot : TenantEntity
{
    public Guid PayrollRunId { get; set; }

    public PayrollReportType ReportType { get; set; }

    [Required, MaxLength(160)]
    public string ReportName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string SnapshotNumber { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public Guid? GeneratedByUserId { get; set; }

    [Required]
    public string SnapshotJson { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual PayrollRun PayrollRun { get; set; } = null!;
}

public class PayrollPayslipSnapshot : TenantEntity
{
    public Guid PayrollRunId { get; set; }

    public Guid PayrollRunEmployeeId { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string PayslipNumber { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public Guid? GeneratedByUserId { get; set; }

    public decimal GrossIncome { get; set; }

    public decimal NetIncome { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal EmployeeContribution { get; set; }

    [Required]
    public string SnapshotJson { get; set; } = string.Empty;

    public virtual PayrollRun PayrollRun { get; set; } = null!;
    public virtual PayrollRunEmployee PayrollRunEmployee { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
}
