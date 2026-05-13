const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

type QueryValue = string | number | boolean | null | undefined;

function getToken() {
  if (typeof window === 'undefined') {
    return null;
  }

  return localStorage.getItem('token') || localStorage.getItem('authToken');
}

function buildQuery(params: Record<string, QueryValue>) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      query.set(key, String(value));
    }
  });

  const value = query.toString();
  return value ? `?${value}` : '';
}

async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers = new Headers(options.headers);
  headers.set('Content-Type', 'application/json');

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(payload?.message || payload?.error || `Payroll API request failed with ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export type PayrollComponentType =
  | 'Allowance'
  | 'Deduction'
  | 'Benefit'
  | 'EmployeeContribution'
  | 'EmployerContribution';

export type PayrollCalculationType = 'FixedAmount' | 'PercentageOfBasic';
export type PayrollRunStatus = 'Draft' | 'Calculated' | 'InReview' | 'Approved' | 'Closed' | 'RolledBack';

export interface PayrollParameterSet {
  id?: string;
  code: string;
  name: string;
  baseCurrency: string;
  dateFormat: string;
  multiLoginEnabled: boolean;
  maxLoginCount?: number | null;
  currentPayPeriod: number;
  currentPeriodFrom?: string | null;
  currentPeriodTo?: string | null;
  monthDays: number;
  payFrequencyMonths: number;
  payMode?: string | null;
  timeSheetMode?: string | null;
  leaveClassification?: string | null;
  multiplePayBasisEnabled: boolean;
  defaultPayBasis?: string | null;
  allowEmployeePaymentMethod: boolean;
  defaultEmployeePaymentMethod?: string | null;
  employeeSsfRate: number;
  employerSsfRate: number;
  ssfLimit: number;
  bonusTaxRate: number;
  minimumBonusToTax: number;
  withholdingTaxRate: number;
  taxRate?: number | null;
  minimumTaxableIncome?: number | null;
  debitRatio?: number | null;
  minimumHireAge?: number | null;
  maleRetireAge?: number | null;
  femaleRetireAge?: number | null;
  totalAllowanceTaxCeiling?: number | null;
  exchangeRate?: number | null;
  reportingCurrency?: string | null;
  pictureDirectory?: string | null;
  nightAllowanceAmount?: number | null;
  nightAllowancePercent?: number | null;
  separateBonusTax: boolean;
  multiCurrencyEnabled: boolean;
  timesheetEnabled: boolean;
  minimumPasswordLength?: number | null;
  passwordExpirationDays?: number | null;
  passwordReuseCount?: number | null;
  minimumPasswordNumbers?: number | null;
  minimumPasswordSpecialCharacters?: number | null;
  minimumPasswordUppercase?: number | null;
  minimumPasswordLowercase?: number | null;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollComponent {
  id?: string;
  code: string;
  name: string;
  componentType: PayrollComponentType;
  calculationType: PayrollCalculationType;
  amount: number;
  rate: number;
  taxFreeCeiling?: number | null;
  separateTaxPercent?: number | null;
  maxBenefitToTax?: number | null;
  employerAmount?: number | null;
  category?: string | null;
  cycle?: string | null;
  nextPayPeriod?: number | null;
  nextPayPeriodDate?: string | null;
  lastPayDate?: string | null;
  taxable: boolean;
  employerTaxable: boolean;
  separateTax: boolean;
  applyToBenefit: boolean;
  afterTaxContribution: boolean;
  includeInGross: boolean;
  grossUp: boolean;
  prorate: boolean;
  appliesByDefault: boolean;
  currencyCode: string;
  isActive: boolean;
}

export interface PayrollComponentRule {
  id?: string;
  componentType: PayrollComponentType;
  componentCode: string;
  category: string;
  calculationType: PayrollCalculationType;
  amount: number;
  taxFreeCeiling?: number | null;
  employerAmount?: number | null;
  afterTax: boolean;
  employerTaxable: boolean;
  applicable: boolean;
  legacyCompanyCode?: string | null;
}

export interface PayrollTaxBand {
  id?: string;
  taxType: string;
  serialNo: number;
  description?: string | null;
  lowerBound: number;
  upperBound?: number | null;
  taxableIncome?: number | null;
  ratePercent: number;
  fixedAmount: number;
  perMonthAmount?: number | null;
  cumulativeTax?: number | null;
  cumulativeSalary?: number | null;
  payPeriod?: number | null;
  payPeriodFrom?: string | null;
  payPeriodTo?: string | null;
  legacyCompanyCode?: string | null;
  isAnnual: boolean;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface PayrollTaxRelief {
  id?: string;
  code: string;
  name: string;
  calculationType: PayrollCalculationType;
  amount: number;
  factor: number;
  appliesByDefault: boolean;
  isActive: boolean;
}

export interface PayrollEmployeeTaxRelief {
  id?: string;
  employeeProfileId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  reliefCode: string;
  reliefName: string;
  calculationType: PayrollCalculationType;
  amount: number;
  factor: number;
  totalRelief: number;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollEmployeeTaxReliefBulkSave {
  reliefCode: string;
  reliefName: string;
  calculationType: PayrollCalculationType;
  amount: number;
  legacyCompanyCode?: string | null;
  entries: Array<{
    id?: string;
    employeeProfileId?: string;
    employeeNumber: string;
    amount?: number | null;
    factor?: number | null;
    isSelected: boolean;
  }>;
}

export interface PayrollEmployeeComponentException {
  id?: string;
  employeeProfileId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  monthlyBasicSalary: number;
  payrollComponentId: string;
  componentCode: string;
  componentName: string;
  componentType: PayrollComponentType;
  calculationType: PayrollCalculationType;
  amount: number;
  rate: number;
  taxable: boolean;
  taxFreeCeiling?: number | null;
  employerAmount?: number | null;
  employerTaxable: boolean;
  grossUp: boolean;
  currencyCode: string;
  applicable: boolean;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

export interface PayrollEmployeeComponentExceptionBulkSave {
  payrollComponentId?: string | null;
  componentCode?: string | null;
  componentType?: PayrollComponentType | null;
  entries: Array<{
    id?: string;
    employeeProfileId?: string;
    employeeNumber: string;
    calculationTypeOverride?: PayrollCalculationType | null;
    amountOverride?: number | null;
    rateOverride?: number | null;
    taxableOverride?: boolean | null;
    taxFreeCeilingOverride?: number | null;
    employerAmountOverride?: number | null;
    employerTaxableOverride?: boolean | null;
    grossUpOverride?: boolean | null;
    currencyCodeOverride?: string | null;
    applicable: boolean;
    effectiveFrom?: string | null;
    effectiveTo?: string | null;
    isSelected: boolean;
  }>;
}

export interface PayrollPromotionArrearsEntry {
  id?: string;
  employeeProfileId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  legacyEmployeeId?: string | null;
  effectiveDate: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  legacyCompanyCode?: string | null;
  workingDays?: number | null;
  basicSalary?: number | null;
  isActive: boolean;
}

export interface PayrollPromotionArrearsBulkSave {
  legacyCompanyCode?: string | null;
  entries: Array<{
    id?: string;
    employeeProfileId?: string;
    employeeNumber: string;
    effectiveDate?: string | null;
    isSelected: boolean;
  }>;
}

export interface PayrollOvertimeSummaryEntry {
  id?: string;
  employeeProfileId: string;
  employeeId?: string | null;
  employeeNumber: string;
  employeeName: string;
  legacyEmployeeId?: string | null;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  absentDays: number;
  normalDays: number;
  weekdayDays: number;
  holidayDays: number;
  saturdayDays: number;
  sundayDays: number;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollOvertimeSummaryBulkSave {
  legacyCompanyCode?: string | null;
  entries: Array<{
    id?: string;
    employeeProfileId?: string;
    employeeNumber: string;
    absentDays?: number | null;
    normalDays?: number | null;
    weekdayDays?: number | null;
    holidayDays?: number | null;
    saturdayDays?: number | null;
    sundayDays?: number | null;
    isSelected: boolean;
  }>;
}

export interface PayrollContributionOpeningBalance {
  id?: string;
  employeeProfileId: string;
  employeeId?: string | null;
  employeeNumber: string;
  employeeName: string;
  legacyEmployeeId?: string | null;
  contributionCodeType: string;
  contributionCode: string;
  contributionName: string;
  balanceAsAt: string;
  openingBalance: number;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollContributionOpeningBalanceBulkSave {
  contributionCodeType: string;
  contributionCode: string;
  contributionName: string;
  legacyCompanyCode?: string | null;
  entries: Array<{
    id?: string;
    employeeProfileId?: string;
    employeeNumber: string;
    balanceAsAt?: string | null;
    openingBalance?: number | null;
    isSelected: boolean;
  }>;
}

export interface PayrollContributionTransaction {
  id?: string;
  employeeProfileId: string;
  employeeId?: string | null;
  employeeNumber: string;
  employeeName: string;
  legacyEmployeeId?: string | null;
  contributionCodeType: string;
  contributionCode: string;
  contributionName: string;
  transactionType: 'Withdrawal' | 'Interest';
  effectiveDate: string;
  amount: number;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollBonusPolicy {
  id?: string;
  code: string;
  name: string;
  calculationType: PayrollCalculationType;
  amount: number;
  taxable: boolean;
  separateTax: boolean;
  taxFreeCeiling?: number | null;
  minimumToTax?: number | null;
  annualSalaryPercentToTax?: number | null;
  taxRate?: number | null;
  nextPayPeriod?: number | null;
  lastPayPeriod?: number | null;
  nextPayPeriodDate?: string | null;
  lastPayPeriodDate?: string | null;
  category?: string | null;
  cycle?: string | null;
  paySeparate: boolean;
  perAnnual: boolean;
  minimumMonths?: number | null;
  prorate: boolean;
  isActive: boolean;
}

export interface PayrollBonusRule {
  id?: string;
  bonusCode: string;
  groupCode: string;
  calculationType: PayrollCalculationType;
  amount: number;
  applicable: boolean;
  legacyCompanyCode?: string | null;
}

export interface PayrollBonusException {
  id?: string;
  bonusCode: string;
  employeeProfileId?: string | null;
  employeeNumber: string;
  employeeName: string;
  calculationType: PayrollCalculationType;
  amount: number;
  taxable: boolean;
  applicable: boolean;
  currencyCode?: string | null;
  legacyCompanyCode?: string | null;
}

export interface PayrollBonusExceptionBulkSave {
  bonusCode: string;
  legacyCompanyCode?: string | null;
  entries: Array<{
    id?: string;
    employeeProfileId?: string | null;
    employeeNumber: string;
    calculationType: PayrollCalculationType;
    amount: number;
    taxable?: boolean | null;
    applicable: boolean;
    currencyCode?: string | null;
    legacyCompanyCode?: string | null;
    isSelected: boolean;
  }>;
}

export interface PayrollBackpayPolicy {
  id?: string;
  operationType: 'IncreaseSalary' | 'SalaryArrears';
  calculationType: PayrollCalculationType;
  amount: number;
  numberOfMonths?: number | null;
  effectiveDate?: string | null;
  applyTax: boolean;
  applySsf: boolean;
  minimumServiceMode?: string | null;
  minimumServiceValue?: number | null;
  minimumServiceDate?: string | null;
  categoryType: string;
  legacyCompanyCode?: string | null;
}

export interface PayrollBackpayRule {
  id?: string;
  operationType: 'IncreaseSalary' | 'SalaryArrears';
  categoryType: string;
  categoryCode: string;
  categoryName?: string | null;
  calculationType: PayrollCalculationType;
  amount: number;
  applicable: boolean;
  legacyCompanyCode?: string | null;
}

export interface PayrollBackpayException {
  id?: string;
  operationType: 'SalaryArrears';
  employeeProfileId?: string | null;
  employeeNumber: string;
  employeeName: string;
  calculationType: PayrollCalculationType;
  amount: number;
  applicable: boolean;
  legacyCompanyCode?: string | null;
}

export interface PayrollPensionScheme {
  id?: string;
  code: string;
  name: string;
  employeeRatePercent: number;
  employerRatePercent: number;
  contributionLimit?: number | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface PayrollOvertimeRange {
  id?: string;
  payrollOvertimePolicyId: string;
  minRange: number;
  maxRange: number;
  rate: number;
  legacyCompanyCode?: string | null;
}

export interface PayrollOvertimePolicy {
  id?: string;
  code: string;
  name: string;
  normalWorkingHours: number;
  normalHours?: number | null;
  weekdayRate: number;
  holidayRate: number;
  specialDutyRate: number;
  taxable: boolean;
  taxCeiling?: number | null;
  separateOvertimeTax: boolean;
  maxOvertimeAmount?: number | null;
  maxOvertimeIsPercent: boolean;
  maxOvertimeType?: string | null;
  leaveRecallRate?: number | null;
  payPeriod?: number | null;
  payPeriodFrom?: string | null;
  payPeriodTo?: string | null;
  maxOvertimeSeparateTax?: number | null;
  minimumBasicForSeparateTax?: number | null;
  minimumSeparateOvertimePercent?: number | null;
  legacyCompanyCode?: string | null;
  isDefault: boolean;
  isActive: boolean;
  ranges?: PayrollOvertimeRange[];
}

export interface PayrollOvertimeSetup {
  overtimePolicies: PayrollOvertimePolicy[];
  overtimeRanges: PayrollOvertimeRange[];
}

export interface PayrollLoanPolicy {
  id?: string;
  code: string;
  name: string;
  maxLoanAmount?: number | null;
  interestRatePercent?: number | null;
  applyInterest: boolean;
  maxPaybackPeriods?: number | null;
  maxDebitRatioPercent?: number | null;
  interestType: string;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollJournalMapping {
  id?: string;
  transactionType: string;
  componentCode?: string | null;
  description: string;
  debitCredit: 'DR' | 'CR';
  accountCode: string;
  accountType?: string | null;
  isActive: boolean;
}

export interface PayrollLegacyMenuItem {
  menuId: string;
  parentMenuId?: string | null;
  caption: string;
  path: string;
  itemType: 'Folder' | 'Form' | 'Report' | string;
  target?: string | null;
  companyScope?: string | null;
  sequenceNo: number;
  implemented: boolean;
}

export interface PayrollCodeType {
  id?: string;
  codeType: string;
  description: string;
  accountCode?: string | null;
  blocked: boolean;
  dependent: boolean;
  dependentOnCodeType?: string | null;
  legacyCompanyCode?: string | null;
  valueCount?: number;
}

export interface PayrollCodeValue {
  id?: string;
  payrollCodeTypeId?: string;
  codeType: string;
  actualCode: string;
  description: string;
  additionalDescription?: string | null;
  accountCode?: string | null;
  blocked: boolean;
  dependentCodeType?: string | null;
  dependentActualCode?: string | null;
  legacyCompanyCode?: string | null;
}

export interface PayrollCodeSetup {
  selectedCodeType?: string | null;
  codeTypes: PayrollCodeType[];
  codeValues: PayrollCodeValue[];
}

export interface PayrollHoliday {
  id?: string;
  holidayDate: string;
  description: string;
  legacyCompanyCode?: string | null;
}

export interface PayrollNonWorkingDay {
  id?: string;
  dayCode: string;
  description: string;
  overtimeRate?: number | null;
  legacyCompanyCode?: string | null;
}

export interface PayrollHolidaySetup {
  holidays: PayrollHoliday[];
  nonWorkingDays: PayrollNonWorkingDay[];
}

export interface PayrollExchangeRate {
  id?: string;
  currencyCode: string;
  rate: number;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  legacyCompanyCode?: string | null;
}

export interface PayrollBankBranch {
  id?: string;
  branchSetupCode?: string | null;
  bankCode: string;
  branchCode: string;
  branchDescription: string;
  region?: string | null;
  sortCode?: string | null;
  accountCode?: string | null;
  legacyCompanyCode?: string | null;
}

export interface PayrollLeaveSetupDetail {
  id?: string;
  payrollLeaveSetupId?: string;
  category: string;
  categoryDetail: string;
  days?: number | null;
  serviceFrom?: number | null;
  serviceTo?: number | null;
  sequenceNo?: number | null;
  legacyCompanyCode?: string | null;
}

export interface PayrollLeaveSetup {
  id?: string;
  category: string;
  categoryDetail: string;
  legacyCompanyCode?: string | null;
  detailCount?: number;
  details?: PayrollLeaveSetupDetail[];
}

export interface PayrollLeaveSetupCollection {
  leaveSetups: PayrollLeaveSetup[];
  leaveSetupDetails: PayrollLeaveSetupDetail[];
}

export interface PayrollLegacyMenuUser {
  id?: string;
  userName: string;
  userGroup: string;
  userNo: string;
  legacyPasswordHash?: string | null;
  loginEnabled: boolean;
  locked: boolean;
  passwordChangeRequired: boolean;
  passwordExpiryDate?: string | null;
  passwordFailureCount: number;
  companyId?: string | null;
  businessUnitId?: string | null;
  legacyCompanyCode?: string | null;
  canViewSalary: boolean;
  canApprove: boolean;
  refreshToken?: string | null;
  lastPasswordChange?: string | null;
}

export interface PayrollLegacyMenuSecurity {
  id?: string;
  formCode: string;
  formName: string;
  userName: string;
  allowed: boolean;
  groupName: string;
  legacyCompanyCode?: string | null;
  topMenu?: string | null;
  subMenu?: string | null;
  canEdit: boolean;
}

export interface PayrollLegacyPasswordChange {
  userName: string;
  legacyCompanyCode?: string | null;
  oldPasswordReference?: string | null;
  newPasswordReference: string;
  forceChange: boolean;
  passwordExpiryDate?: string | null;
}

export interface PayrollCompanyProfile {
  id?: string;
  companyId: string;
  companyName: string;
  companySystemName?: string | null;
  companyCode?: string | null;
  multipleBusinessUnits: boolean;
  businessNumber?: number | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  addressLine3?: string | null;
  cityOrTown?: string | null;
  regionOrState?: string | null;
  country?: string | null;
  licenceType?: string | null;
  licenceNo?: number | null;
  socialSecurityNo?: string | null;
  taxpayerIdNo?: string | null;
  pictureName?: string | null;
  logoName?: string | null;
  splitLicenceOnBusinessUnits: boolean;
  licencePercentOrNumber?: string | null;
  makeCompanyABusinessUnit: boolean;
  multiplePayBasis: boolean;
  companyPayBasis?: string | null;
  multipleEmployeePaymentMethods: boolean;
  defaultEmployeePaymentMethod?: string | null;
  legacyCompanyCode?: string | null;
  isActive: boolean;
  businessUnits?: PayrollBusinessUnit[];
  bankers?: PayrollCompanyBanker[];
}

export interface PayrollBusinessUnit {
  id?: string;
  payrollCompanyProfileId?: string | null;
  businessUnitId: string;
  businessUnitCode?: string | null;
  businessUnitName?: string | null;
  location?: string | null;
  country?: string | null;
  distributeLicence: boolean;
  licencePercentOrNumber?: string | null;
  licenceValue?: number | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  addressLine3?: string | null;
  regionOrState?: string | null;
  companyId?: string | null;
  companyCode?: string | null;
  accountsCode?: string | null;
  multiplePayBasis: boolean;
  companyPayBasis?: string | null;
  multipleEmployeePaymentMethods: boolean;
  defaultEmployeePaymentMethod?: string | null;
  legacyCompanyCode?: string | null;
}

export interface PayrollCompanyBanker {
  id?: string;
  payrollCompanyProfileId?: string | null;
  bankCode: string;
  bankBranch?: string | null;
  address1?: string | null;
  address2?: string | null;
  address3?: string | null;
  accountNumber1?: string | null;
  accountNumber2?: string | null;
  accountNumber3?: string | null;
  companyCode: string;
  companyId: string;
  signatory1?: string | null;
  signatory2?: string | null;
  signatory3?: string | null;
  signatoryPosition1?: string | null;
  signatoryPosition2?: string | null;
  signatoryPosition3?: string | null;
  bankRegion?: string | null;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollCompanySetup {
  companyProfiles: PayrollCompanyProfile[];
  businessUnits: PayrollBusinessUnit[];
  companyBankers: PayrollCompanyBanker[];
}

export interface PayrollGrade {
  id?: string;
  gradeId?: string | null;
  gradeType?: string | null;
  gradeName: string;
  systemGradeName?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  midPoint?: number | null;
  currencyCode: string;
  startPoint?: string | null;
  incrementStep?: number | null;
  ceiling?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  businessUnitId?: string | null;
  companyId?: string | null;
  orderField?: number | null;
  reportingName?: string | null;
  enforceNotchConsistency: boolean;
  legacyCompanyCode?: string | null;
  isActive: boolean;
  notches?: PayrollGradeNotch[];
}

export interface PayrollGradeNotch {
  id?: string;
  payrollGradeId: string;
  gradeId?: string | null;
  systemGradeName?: string | null;
  notch: string;
  value?: number | null;
  currencyCode: string;
  startDate?: string | null;
  endDate?: string | null;
  orderField?: number | null;
  businessUnitId?: string | null;
  companyId?: string | null;
  annualisedValue?: number | null;
  gradeName?: string | null;
  reportingName?: string | null;
  legacyCompanyCode?: string | null;
  hourlyRate?: number | null;
}

export interface PayrollGradeSetup {
  grades: PayrollGrade[];
  notches: PayrollGradeNotch[];
}

export interface PayrollTaxTable {
  taxBands: PayrollTaxBand[];
}

export interface PayrollSetupSummary {
  activeParameters?: PayrollParameterSet | null;
  componentCount: number;
  componentRuleCount: number;
  codeTypeCount: number;
  holidayCount: number;
  nonWorkingDayCount: number;
  exchangeRateCount: number;
  bankBranchCount: number;
  leaveSetupCount: number;
  overtimeRangeCount: number;
  legacyMenuUserCount: number;
  menuSecurityCount: number;
  companyProfileCount: number;
  businessUnitCount: number;
  companyBankerCount: number;
  gradeCount: number;
  gradeNotchCount: number;
  taxBandCount: number;
  taxReliefCount: number;
  pensionSchemeCount: number;
  overtimePolicyCount: number;
  loanPolicyCount: number;
  bonusPolicyCount: number;
  bonusRuleCount: number;
  bonusExceptionCount: number;
  backpayPolicyCount: number;
  backpayRuleCount: number;
  backpayExceptionCount: number;
  journalMappingCount: number;
  components: PayrollComponent[];
  componentRules: PayrollComponentRule[];
  taxBands: PayrollTaxBand[];
  taxReliefs: PayrollTaxRelief[];
  pensionSchemes: PayrollPensionScheme[];
  overtimePolicies: PayrollOvertimePolicy[];
  loanPolicies: PayrollLoanPolicy[];
  bonusPolicies: PayrollBonusPolicy[];
  bonusRules: PayrollBonusRule[];
  bonusExceptions: PayrollBonusException[];
  backpayPolicies: PayrollBackpayPolicy[];
  backpayRules: PayrollBackpayRule[];
  backpayExceptions: PayrollBackpayException[];
  journalMappings: PayrollJournalMapping[];
  codeTypes: PayrollCodeType[];
  bankBranches: PayrollBankBranch[];
  leaveSetups: PayrollLeaveSetup[];
  legacyMenuUsers: PayrollLegacyMenuUser[];
  menuSecurity: PayrollLegacyMenuSecurity[];
  companyProfiles: PayrollCompanyProfile[];
  grades: PayrollGrade[];
}

export interface PayrollSalaryBasis {
  id?: string;
  monthlyBasicSalary: number;
  annualBasicSalary?: number | null;
  hourlyRate?: number | null;
  currencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface PayrollLoanSchedule {
  id: string;
  payrollLoanId: string;
  sequenceNo: number;
  repaymentDate: string;
  principalAmount: number;
  interestAmount: number;
  amountPaid: number;
  interestPaid: number;
  actualRepaymentDate?: string | null;
  posted: boolean;
}

export interface PayrollLoan {
  id?: string;
  employeeProfileId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  loanPolicyId?: string | null;
  loanPolicyCode?: string | null;
  loanPolicyName?: string | null;
  loanTypeCode?: string | null;
  facilityNumber: string;
  dateGranted: string;
  amountGranted: number;
  basicSalary: number;
  monthlyRepaymentAmount: number;
  outstandingBalance: number;
  interestRatePercent: number;
  totalInterest: number;
  interestRepaymentAmount: number;
  repaymentMode: 'Amount' | 'NoOfRepayments' | 'PercentOfBasic' | string;
  totalRepaymentAmount: number;
  employeeDebtRatio: number;
  debtServiceRatio: number;
  paymentStartDate: string;
  paymentEndDate?: string | null;
  numberOfRepayments: number;
  status: 'Active' | 'Suspended' | 'Closed' | string;
  periodOfSuspension?: number | null;
  suspensionStartDate?: string | null;
  suspensionEndDate?: string | null;
  suspensionNarration?: string | null;
  generalRemarks?: string | null;
  isActive: boolean;
  schedules: PayrollLoanSchedule[];
}

export interface PayrollLoanRepayment {
  payrollLoanId: string;
  payrollLoanScheduleId: string;
  loanSequenceNo: number;
  facilityNumber: string;
  employeeNumber: string;
  repaymentAmount: number;
  principalPaid: number;
  interestPaid: number;
  loanBalance: number;
  actualRepaymentDate: string;
  loan: PayrollLoan;
}

export interface PayrollLoanRepaymentRequest {
  payrollLoanId?: string;
  employeeNumber: string;
  facilityNumber: string;
  repaymentAmount: number;
  actualRepaymentDate?: string | null;
}

export interface PayrollEmployeeProfile {
  id?: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentName?: string | null;
  sectionName?: string | null;
  positionTitle?: string | null;
  staffCategory?: string | null;
  jobLocation?: string | null;
  legacyEmployeeId?: string | null;
  legacyEmployeeNumber?: string | null;
  payrollActive: boolean;
  payTax: boolean;
  ssfApplicable: boolean;
  grossUp: boolean;
  tier2Only: boolean;
  overtimeEligible: boolean;
  ssfNumber?: string | null;
  tinNumber?: string | null;
  currencyCode: string;
  salaryBasis?: PayrollSalaryBasis | null;
  paymentMethods: any[];
  employeeComponents: any[];
  loans: PayrollLoan[];
}

export interface PayrollSalaryAdvance {
  id?: string;
  employeeProfileId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  advanceDate: string;
  advanceAmount: number;
  description?: string | null;
  legacyCompanyCode?: string | null;
  isActive: boolean;
}

export interface PayrollRunEmployee {
  id: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  basicSalary: number;
  grossIncome: number;
  taxableIncome: number;
  incomeTax: number;
  employeeContribution: number;
  employerContribution: number;
  netIncome: number;
  currencyCode: string;
}

export interface PayrollTransaction {
  id: string;
  employeeNumber: string;
  transactionType: string;
  componentCode?: string | null;
  description?: string | null;
  amount: number;
  employerAmount?: number | null;
  currencyCode: string;
}

export interface PayrollJournalLine {
  id: string;
  sequenceNo: number;
  transactionType: string;
  debitCredit: 'DR' | 'CR';
  accountCode: string;
  description: string;
  amount: number;
  posted: boolean;
  journalEntryId?: string | null;
}

export interface PayrollRun {
  id: string;
  runNumber: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  runDate: string;
  currencyCode: string;
  isSeparateBonusRun: boolean;
  separateBonusCode?: string | null;
  status: PayrollRunStatus;
  employeeCount: number;
  grossAmount: number;
  netAmount: number;
  taxAmount: number;
  employeeContributionAmount: number;
  employerContributionAmount: number;
  calculatedAt?: string | null;
  reviewedAt?: string | null;
  approvedAt?: string | null;
  closedAt?: string | null;
  notes?: string | null;
  employees: PayrollRunEmployee[];
  transactions: PayrollTransaction[];
  journalLines: PayrollJournalLine[];
}

export interface PayrollSummaryReport {
  payrollRunId: string;
  runNumber: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  currencyCode: string;
  employeeCount: number;
  grossAmount: number;
  netAmount: number;
  taxAmount: number;
  employeeContributionAmount: number;
  employerContributionAmount: number;
  components: Array<{
    transactionType: string;
    componentCode?: string | null;
    description: string;
    amount: number;
    employerAmount: number;
  }>;
  employees: PayrollRunEmployee[];
  snapshot?: PayrollReportSnapshot | null;
}

export interface PayrollReportSnapshot {
  id: string;
  payrollRunId: string;
  reportType: string;
  reportName: string;
  snapshotNumber: string;
  generatedAt: string;
  generatedByUserId?: string | null;
  notes?: string | null;
}

export interface PayrollPayslipSnapshot {
  id: string;
  payrollRunId: string;
  payrollRunEmployeeId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  payslipNumber: string;
  generatedAt: string;
  grossIncome: number;
  netIncome: number;
  taxAmount: number;
  employeeContribution: number;
}

export interface PayrollPayslipContribution {
  item: string;
  employeeContribution: number;
  employerContribution: number;
  totalContribution: number;
  openingBalance: number;
  totalWithdrawal: number;
  grandTotal: number;
  firstTier: number;
  secondTier: number;
  isProvidentFund: boolean;
}

export interface PayrollPayslipBankDetail {
  bankName: string;
  accountNumber?: string | null;
  currencyCode: string;
  exchangeRate?: number | null;
  amount: number;
}

export interface PayrollPayslipOvertime {
  workingHours: number;
  overtimeHours: number;
  holidayHours: number;
  saturdayHours: number;
  sundayHours: number;
  daySixAndSevenHours: number;
  totalHours: number;
}

export interface PayrollPayslip {
  payrollRunId: string;
  payrollRunEmployeeId: string;
  employeeId: string;
  runNumber: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  isSeparateBonusRun: boolean;
  separateBonusCode?: string | null;
  companyName: string;
  companyAddress?: string | null;
  companyPhone?: string | null;
  employeeNumber: string;
  employeeName: string;
  employeeEmail?: string | null;
  departmentName?: string | null;
  sectionName?: string | null;
  positionTitle?: string | null;
  staffCategory?: string | null;
  jobLocation?: string | null;
  ssfNumber?: string | null;
  staffTin?: string | null;
  currencyCode: string;
  basicSalary: number;
  grossIncome: number;
  taxableIncome: number;
  taxRelief: number;
  incomeTax: number;
  normalIncomeTax: number;
  bonusIncomeTax: number;
  overtimeIncomeTax: number;
  employeeContribution: number;
  employerContribution: number;
  netIncome: number;
  overtime?: PayrollPayslipOvertime | null;
  earnings: PayrollTransaction[];
  deductions: PayrollTransaction[];
  contributions: PayrollPayslipContribution[];
  bankDetails: PayrollPayslipBankDetail[];
  snapshot?: PayrollPayslipSnapshot | null;
}

export interface PayrollPayslipFilters {
  employeeId?: string;
  categoryType?: string;
  categoryValue?: string;
}

export interface PayrollJournalPosting {
  payrollRunId: string;
  runNumber: string;
  journalEntryId: string;
  journalNumber: string;
  totalDebit: number;
  totalCredit: number;
  postedAt: string;
  lines: PayrollJournalLine[];
}

export interface PayrollPayslipEmailResult {
  payrollRunId: string;
  runNumber: string;
  requestedCount: number;
  sentCount: number;
  failedCount: number;
  recipients: Array<{
    employeeId: string;
    employeeNumber: string;
    employeeName: string;
    emailAddress?: string | null;
    sent: boolean;
    message: string;
  }>;
}

export interface PayrollImportBatch {
  id: string;
  sourceName: string;
  totalRows: number;
  matchedRows: number;
  missingRows: number;
  duplicateRows: number;
  importedAt: string;
  rows: Array<{
    id: string;
    rowNumber: number;
    legacyEmployeeNumber: string;
    legacyFullName?: string | null;
    department?: string | null;
    matchedEmployeeNumber?: string | null;
    status: string;
    message?: string | null;
  }>;
}

export interface HrEmployee {
  id: string;
  employeeNumber: string;
  fullName: string;
  displayName?: string | null;
  departmentName?: string | null;
  positionTitle?: string | null;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const payrollService = {
  getLegacyMenu: () => apiRequest<PayrollLegacyMenuItem[]>('/hr/payroll/legacy-menu'),
  getSetupSummary: () => apiRequest<PayrollSetupSummary>('/hr/payroll/setup-summary'),
  getCodeSetup: (codeType?: string) =>
    apiRequest<PayrollCodeSetup>(`/hr/payroll/setup/codes${buildQuery({ codeType })}`),
  upsertCodeType: (payload: PayrollCodeType) =>
    apiRequest<PayrollCodeType>('/hr/payroll/setup/code-types', { method: 'POST', body: JSON.stringify(payload) }),
  upsertCodeValue: (payload: PayrollCodeValue) =>
    apiRequest<PayrollCodeValue>('/hr/payroll/setup/code-values', { method: 'POST', body: JSON.stringify(payload) }),
  getHolidaySetup: () => apiRequest<PayrollHolidaySetup>('/hr/payroll/setup/holidays'),
  upsertHoliday: (payload: PayrollHoliday) =>
    apiRequest<PayrollHoliday>('/hr/payroll/setup/holidays', { method: 'POST', body: JSON.stringify(payload) }),
  upsertNonWorkingDay: (payload: PayrollNonWorkingDay) =>
    apiRequest<PayrollNonWorkingDay>('/hr/payroll/setup/non-working-days', { method: 'POST', body: JSON.stringify(payload) }),
  getExchangeRates: () => apiRequest<PayrollExchangeRate[]>('/hr/payroll/setup/exchange-rates'),
  upsertExchangeRate: (payload: PayrollExchangeRate) =>
    apiRequest<PayrollExchangeRate>('/hr/payroll/setup/exchange-rates', { method: 'POST', body: JSON.stringify(payload) }),
  getBankBranches: () => apiRequest<PayrollBankBranch[]>('/hr/payroll/setup/bank-branches'),
  upsertBankBranch: (payload: PayrollBankBranch) =>
    apiRequest<PayrollBankBranch>('/hr/payroll/setup/bank-branches', { method: 'POST', body: JSON.stringify(payload) }),
  getLeaveSetup: () => apiRequest<PayrollLeaveSetupCollection>('/hr/payroll/setup/leave'),
  upsertLeaveSetup: (payload: PayrollLeaveSetup) =>
    apiRequest<PayrollLeaveSetup>('/hr/payroll/setup/leave', { method: 'POST', body: JSON.stringify(payload) }),
  upsertLeaveSetupDetail: (payload: PayrollLeaveSetupDetail) =>
    apiRequest<PayrollLeaveSetupDetail>('/hr/payroll/setup/leave-details', { method: 'POST', body: JSON.stringify(payload) }),
  getOvertimeSetup: () => apiRequest<PayrollOvertimeSetup>('/hr/payroll/setup/overtime'),
  upsertOvertimePolicy: (payload: PayrollOvertimePolicy) =>
    apiRequest<PayrollOvertimePolicy>('/hr/payroll/setup/overtime-policies', { method: 'POST', body: JSON.stringify(payload) }),
  upsertOvertimeRange: (payload: PayrollOvertimeRange) =>
    apiRequest<PayrollOvertimeRange>('/hr/payroll/setup/overtime-ranges', { method: 'POST', body: JSON.stringify(payload) }),
  getLegacyMenuUsers: () => apiRequest<PayrollLegacyMenuUser[]>('/hr/payroll/setup/menu-users'),
  upsertLegacyMenuUser: (payload: PayrollLegacyMenuUser) =>
    apiRequest<PayrollLegacyMenuUser>('/hr/payroll/setup/menu-users', { method: 'POST', body: JSON.stringify(payload) }),
  getMenuSecurity: () => apiRequest<PayrollLegacyMenuSecurity[]>('/hr/payroll/setup/menu-security'),
  upsertMenuSecurity: (payload: PayrollLegacyMenuSecurity) =>
    apiRequest<PayrollLegacyMenuSecurity>('/hr/payroll/setup/menu-security', { method: 'POST', body: JSON.stringify(payload) }),
  changeLegacyMenuUserPassword: (payload: PayrollLegacyPasswordChange) =>
    apiRequest<PayrollLegacyMenuUser>('/hr/payroll/setup/menu-users/change-password', { method: 'POST', body: JSON.stringify(payload) }),
  getCompanySetup: () => apiRequest<PayrollCompanySetup>('/hr/payroll/setup/company'),
  upsertCompanyProfile: (payload: PayrollCompanyProfile) =>
    apiRequest<PayrollCompanyProfile>('/hr/payroll/setup/company-profiles', { method: 'POST', body: JSON.stringify(payload) }),
  upsertBusinessUnit: (payload: PayrollBusinessUnit) =>
    apiRequest<PayrollBusinessUnit>('/hr/payroll/setup/business-units', { method: 'POST', body: JSON.stringify(payload) }),
  upsertCompanyBanker: (payload: PayrollCompanyBanker) =>
    apiRequest<PayrollCompanyBanker>('/hr/payroll/setup/company-bankers', { method: 'POST', body: JSON.stringify(payload) }),
  getGradeSetup: () => apiRequest<PayrollGradeSetup>('/hr/payroll/setup/grades'),
  upsertGrade: (payload: PayrollGrade) =>
    apiRequest<PayrollGrade>('/hr/payroll/setup/grades', { method: 'POST', body: JSON.stringify(payload) }),
  upsertGradeNotch: (payload: PayrollGradeNotch) =>
    apiRequest<PayrollGradeNotch>('/hr/payroll/setup/grade-notches', { method: 'POST', body: JSON.stringify(payload) }),
  getTaxTable: () => apiRequest<PayrollTaxTable>('/hr/payroll/setup/tax-table'),
  upsertParameters: (payload: PayrollParameterSet) =>
    apiRequest<PayrollParameterSet>('/hr/payroll/setup/parameters', { method: 'POST', body: JSON.stringify(payload) }),
  upsertComponent: (payload: PayrollComponent) =>
    apiRequest<PayrollComponent>('/hr/payroll/setup/components', { method: 'POST', body: JSON.stringify(payload) }),
  upsertComponentRule: (payload: PayrollComponentRule) =>
    apiRequest<PayrollComponentRule>('/hr/payroll/setup/component-rules', { method: 'POST', body: JSON.stringify(payload) }),
  upsertTaxBand: (payload: PayrollTaxBand) =>
    apiRequest<PayrollTaxBand>('/hr/payroll/setup/tax-bands', { method: 'POST', body: JSON.stringify(payload) }),
  upsertTaxRelief: (payload: PayrollTaxRelief) =>
    apiRequest<PayrollTaxRelief>('/hr/payroll/setup/tax-reliefs', { method: 'POST', body: JSON.stringify(payload) }),
  upsertPensionScheme: (payload: PayrollPensionScheme) =>
    apiRequest<PayrollPensionScheme>('/hr/payroll/setup/pension-schemes', { method: 'POST', body: JSON.stringify(payload) }),
  upsertLoanPolicy: (payload: PayrollLoanPolicy) =>
    apiRequest<PayrollLoanPolicy>('/hr/payroll/setup/loan-policies', { method: 'POST', body: JSON.stringify(payload) }),
  upsertBonusPolicy: (payload: PayrollBonusPolicy) =>
    apiRequest<PayrollBonusPolicy>('/hr/payroll/setup/bonus-policies', { method: 'POST', body: JSON.stringify(payload) }),
  upsertBonusRule: (payload: PayrollBonusRule) =>
    apiRequest<PayrollBonusRule>('/hr/payroll/setup/bonus-rules', { method: 'POST', body: JSON.stringify(payload) }),
  getBonusExceptions: (params: { bonusCode?: string } = {}) =>
    apiRequest<PayrollBonusException[]>(`/hr/payroll/setup/bonus-exceptions${buildQuery(params)}`),
  saveBonusExceptions: (payload: PayrollBonusExceptionBulkSave) =>
    apiRequest<PayrollBonusException[]>('/hr/payroll/setup/bonus-exceptions/bulk', { method: 'POST', body: JSON.stringify(payload) }),
  upsertBackpayPolicy: (payload: PayrollBackpayPolicy) =>
    apiRequest<PayrollBackpayPolicy>('/hr/payroll/setup/backpay-policies', { method: 'POST', body: JSON.stringify(payload) }),
  upsertBackpayRule: (payload: PayrollBackpayRule) =>
    apiRequest<PayrollBackpayRule>('/hr/payroll/setup/backpay-rules', { method: 'POST', body: JSON.stringify(payload) }),
  upsertBackpayException: (payload: PayrollBackpayException) =>
    apiRequest<PayrollBackpayException>('/hr/payroll/setup/backpay-exceptions', { method: 'POST', body: JSON.stringify(payload) }),
  upsertJournalMapping: (payload: PayrollJournalMapping) =>
    apiRequest<PayrollJournalMapping>('/hr/payroll/setup/journal-mappings', { method: 'POST', body: JSON.stringify(payload) }),
  getEmployeeProfiles: (searchTerm?: string) =>
    apiRequest<PayrollEmployeeProfile[]>(`/hr/payroll/employee-profiles${buildQuery({ searchTerm })}`),
  upsertEmployeeProfile: (payload: Partial<PayrollEmployeeProfile> & { employeeId: string; employeeNumber: string }) =>
    apiRequest<PayrollEmployeeProfile>('/hr/payroll/employee-profiles', { method: 'POST', body: JSON.stringify(payload) }),
  getLoans: (params: { employeeNumber?: string; includeInactive?: boolean } = {}) =>
    apiRequest<PayrollLoan[]>(`/hr/payroll/loans${buildQuery(params)}`),
  upsertLoan: (payload: Partial<PayrollLoan> & { employeeProfileId?: string; employeeNumber: string; facilityNumber?: string; dateGranted: string; amountGranted: number; monthlyRepaymentAmount: number }) =>
    apiRequest<PayrollLoan>('/hr/payroll/loans', { method: 'POST', body: JSON.stringify(payload) }),
  postLoanRepayment: (payload: PayrollLoanRepaymentRequest) =>
    apiRequest<PayrollLoanRepayment>('/hr/payroll/loans/repayments', { method: 'POST', body: JSON.stringify(payload) }),
  getSalaryAdvances: (params: { fromDate?: string; toDate?: string; employeeNumber?: string } = {}) =>
    apiRequest<PayrollSalaryAdvance[]>(`/hr/payroll/salary-advances${buildQuery(params)}`),
  upsertSalaryAdvance: (payload: Partial<PayrollSalaryAdvance> & { employeeProfileId?: string; employeeNumber: string; advanceDate: string; advanceAmount: number }) =>
    apiRequest<PayrollSalaryAdvance>('/hr/payroll/salary-advances', { method: 'POST', body: JSON.stringify(payload) }),
  getEmployeeTaxReliefs: (params: { reliefCode?: string } = {}) =>
    apiRequest<PayrollEmployeeTaxRelief[]>(`/hr/payroll/tax-reliefs${buildQuery(params)}`),
  saveEmployeeTaxReliefs: (payload: PayrollEmployeeTaxReliefBulkSave) =>
    apiRequest<PayrollEmployeeTaxRelief[]>('/hr/payroll/tax-reliefs/bulk', { method: 'POST', body: JSON.stringify(payload) }),
  getEmployeeComponentExceptions: (params: { payrollComponentId?: string; componentCode?: string; componentType?: PayrollComponentType } = {}) =>
    apiRequest<PayrollEmployeeComponentException[]>(`/hr/payroll/component-exceptions${buildQuery(params)}`),
  saveEmployeeComponentExceptions: (payload: PayrollEmployeeComponentExceptionBulkSave) =>
    apiRequest<PayrollEmployeeComponentException[]>('/hr/payroll/component-exceptions/bulk', { method: 'POST', body: JSON.stringify(payload) }),
  getPromotionArrears: (params: { employeeNumber?: string; includeInactive?: boolean } = {}) =>
    apiRequest<PayrollPromotionArrearsEntry[]>(`/hr/payroll/promotion-arrears${buildQuery(params)}`),
  savePromotionArrears: (payload: PayrollPromotionArrearsBulkSave) =>
    apiRequest<PayrollPromotionArrearsEntry[]>('/hr/payroll/promotion-arrears/bulk', { method: 'POST', body: JSON.stringify(payload) }),
  getOvertimeSummaries: (params: { employeeNumber?: string; includeInactive?: boolean } = {}) =>
    apiRequest<PayrollOvertimeSummaryEntry[]>(`/hr/payroll/overtime-summaries${buildQuery(params)}`),
  saveOvertimeSummaries: (payload: PayrollOvertimeSummaryBulkSave) =>
    apiRequest<PayrollOvertimeSummaryEntry[]>('/hr/payroll/overtime-summaries/bulk', { method: 'POST', body: JSON.stringify(payload) }),
  getContributionOpeningBalances: (params: { contributionCode?: string; contributionCodeType?: string; includeInactive?: boolean } = {}) =>
    apiRequest<PayrollContributionOpeningBalance[]>(`/hr/payroll/contribution-opening-balances${buildQuery(params)}`),
  saveContributionOpeningBalances: (payload: PayrollContributionOpeningBalanceBulkSave) =>
    apiRequest<PayrollContributionOpeningBalance[]>('/hr/payroll/contribution-opening-balances/bulk', { method: 'POST', body: JSON.stringify(payload) }),
  getContributionTransactions: (params: { employeeNumber?: string; contributionCode?: string; contributionCodeType?: string; includeInactive?: boolean } = {}) =>
    apiRequest<PayrollContributionTransaction[]>(`/hr/payroll/contribution-transactions${buildQuery(params)}`),
  upsertContributionTransaction: (payload: Partial<PayrollContributionTransaction> & { employeeProfileId?: string; employeeNumber: string; contributionCodeType: string; contributionCode: string; contributionName: string; transactionType: 'Withdrawal' | 'Interest'; effectiveDate: string; amount: number }) =>
    apiRequest<PayrollContributionTransaction>('/hr/payroll/contribution-transactions', { method: 'POST', body: JSON.stringify(payload) }),
  importEmployeeReconciliation: (payload: {
    sourceName: string;
    rows: Array<{
      rowNumber: number;
      legacyEmployeeNumber: string;
      legacyEmployeeId?: string | null;
      legacyFullName?: string | null;
      department?: string | null;
    }>;
  }) => apiRequest<PayrollImportBatch>('/hr/payroll/imports/employee-reconciliation', { method: 'POST', body: JSON.stringify(payload) }),
  getRuns: () => apiRequest<PayrollRun[]>('/hr/payroll/runs'),
  getRun: (runId: string) => apiRequest<PayrollRun>(`/hr/payroll/runs/${runId}`),
  createRun: (payload: {
    payPeriod: number;
    payPeriodFrom: string;
    payPeriodTo: string;
    runDate?: string | null;
    currencyCode: string;
    isSeparateBonusRun?: boolean;
    separateBonusCode?: string | null;
    notes?: string | null;
  }) =>
    apiRequest<PayrollRun>('/hr/payroll/runs', { method: 'POST', body: JSON.stringify(payload) }),
  calculateRun: (runId: string) => apiRequest<PayrollRun>(`/hr/payroll/runs/${runId}/calculate`, { method: 'POST', body: JSON.stringify({}) }),
  submitRun: (runId: string, notes?: string) =>
    apiRequest<PayrollRun>(`/hr/payroll/runs/${runId}/submit-review`, { method: 'POST', body: JSON.stringify({ notes }) }),
  approveRun: (runId: string, notes?: string) =>
    apiRequest<PayrollRun>(`/hr/payroll/runs/${runId}/approve`, { method: 'POST', body: JSON.stringify({ notes }) }),
  closeRun: (runId: string, notes?: string) =>
    apiRequest<PayrollRun>(`/hr/payroll/runs/${runId}/close`, { method: 'POST', body: JSON.stringify({ notes }) }),
  rollbackRun: (runId: string, notes?: string) =>
    apiRequest<PayrollRun>(`/hr/payroll/runs/${runId}/rollback`, { method: 'POST', body: JSON.stringify({ notes }) }),
  getRunSummary: (runId: string, createSnapshot = false) =>
    apiRequest<PayrollSummaryReport>(`/hr/payroll/runs/${runId}/summary${buildQuery({ createSnapshot })}`),
  getPayslips: (runId: string, filters?: string | PayrollPayslipFilters) => {
    const query = typeof filters === 'string'
      ? { employeeId: filters }
      : {
          employeeId: filters?.employeeId,
          categoryType: filters?.categoryType,
          categoryValue: filters?.categoryValue,
        };
    return apiRequest<PayrollPayslip[]>(`/hr/payroll/runs/${runId}/payslips${buildQuery(query)}`);
  },
  generatePayslipSnapshots: (runId: string) =>
    apiRequest<PayrollPayslipSnapshot[]>(`/hr/payroll/runs/${runId}/payslips/snapshots`, { method: 'POST', body: JSON.stringify({}) }),
  emailPayslips: (runId: string, payload: { employeeIds?: string[]; subject?: string | null; message?: string | null; attachHtmlCopy?: boolean } = {}) =>
    apiRequest<PayrollPayslipEmailResult>(`/hr/payroll/runs/${runId}/payslips/email`, { method: 'POST', body: JSON.stringify(payload) }),
  postJournal: (runId: string, notes?: string) =>
    apiRequest<PayrollJournalPosting>(`/hr/payroll/runs/${runId}/post-journal`, { method: 'POST', body: JSON.stringify({ notes }) }),
  searchEmployees: (searchTerm: string) =>
    apiRequest<PagedResult<HrEmployee>>(`/hr/employees/paged?page=1&pageSize=20`, {
      method: 'POST',
      body: JSON.stringify({ searchTerm, isActive: true }),
    }),
};
