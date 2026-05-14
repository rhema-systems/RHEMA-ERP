'use client';

import { ClipboardEvent, FormEvent, Fragment, KeyboardEvent, ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ArrowDown,
  ArrowUp,
  Award,
  Banknote,
  Boxes,
  Building2,
  Calculator,
  CalendarCheck,
  CalendarDays,
  Check,
  ChevronRight,
  Circle,
  CreditCard,
  FileSpreadsheet,
  FileText,
  GraduationCap,
  HandCoins,
  Landmark,
  Loader2,
  Printer,
  Save,
  Search,
  Settings,
  Timer,
  Trash2,
  UserPlus,
  type LucideIcon,
} from 'lucide-react';
import * as XLSX from 'xlsx';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { currencyService, CurrencyListDto } from '@/services/financeCommonService';
import {
  PayrollBankBranch,
  PayrollBackpayException,
  PayrollBackpayPolicy,
  PayrollBackpayRule,
  PayrollBonusPolicy,
  PayrollBonusException,
  PayrollBudgetAnalysis,
  PayrollBudgetAnalysisRow,
  PayrollCodeSetup,
  PayrollCodeType,
  PayrollCodeValue,
  PayrollComponent,
  PayrollComponentRule,
  PayrollEmployeeProfile,
  PayrollGrade,
  PayrollGradeNotch,
  PayrollGradeSetup,
  PayrollHoliday,
  PayrollHolidaySetup,
  PayrollLeaveSetup,
  PayrollLeaveSetupCollection,
  PayrollLeaveSetupDetail,
  PayrollLegacyMenuItem,
  PayrollLoanPolicy,
  PayrollNonWorkingDay,
  PayrollOvertimePolicy,
  PayrollParameterSet,
  PayrollRun,
  PayrollSetupSummary,
  PayrollTaxBand,
  PayrollTaxRelief,
  PayrollTaxTable,
  buildPayrollBudgetAnalysis,
  payrollService,
  savePayrollBudgetAnalysis,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);
const currentPayPeriod = Number(new Date().toISOString().slice(0, 7).replace('-', ''));

const defaultParameters: PayrollParameterSet = {
  code: 'DEFAULT',
  name: 'Default payroll parameters',
  baseCurrency: 'GHC',
  dateFormat: 'DD-MM-YYYY',
  multiLoginEnabled: true,
  maxLoginCount: 3,
  currentPayPeriod: 7,
  currentPeriodFrom: today,
  currentPeriodTo: today,
  monthDays: 22,
  payFrequencyMonths: 1,
  payMode: 'M',
  timeSheetMode: 'D',
  leaveClassification: 'CAT',
  multiplePayBasisEnabled: true,
  defaultPayBasis: 'COMBINED',
  allowEmployeePaymentMethod: true,
  defaultEmployeePaymentMethod: 'Bank',
  employeeSsfRate: 5,
  employerSsfRate: 12.5,
  ssfLimit: 0,
  bonusTaxRate: 0,
  minimumBonusToTax: 0,
  withholdingTaxRate: 0,
  taxRate: 0,
  minimumTaxableIncome: 0,
  debitRatio: 25,
  minimumHireAge: 18,
  maleRetireAge: 60,
  femaleRetireAge: 55,
  totalAllowanceTaxCeiling: 0,
  exchangeRate: 1,
  reportingCurrency: 'GHS',
  pictureDirectory: 'C:\\PAYROLL_FORMS_CUR',
  nightAllowanceAmount: 0,
  nightAllowancePercent: 0,
  separateBonusTax: false,
  multiCurrencyEnabled: false,
  timesheetEnabled: false,
  minimumPasswordLength: 8,
  passwordExpirationDays: 90,
  passwordReuseCount: 3,
  minimumPasswordNumbers: 1,
  minimumPasswordSpecialCharacters: 1,
  minimumPasswordUppercase: 1,
  minimumPasswordLowercase: 1,
  legacyCompanyCode: '001',
  isActive: true,
};

const defaultComponent: PayrollComponent = {
  code: '',
  name: '',
  componentType: 'Allowance',
  calculationType: 'FixedAmount',
  amount: 0,
  rate: 0,
  taxFreeCeiling: 0,
  separateTaxPercent: 0,
  maxBenefitToTax: 0,
  employerAmount: 0,
  category: 'All',
  cycle: 'Recurring',
  nextPayPeriod: currentPayPeriod,
  nextPayPeriodDate: today,
  lastPayDate: null,
  taxable: false,
  employerTaxable: false,
  separateTax: false,
  applyToBenefit: false,
  afterTaxContribution: false,
  includeInGross: true,
  grossUp: false,
  prorate: false,
  appliesByDefault: true,
  currencyCode: 'GHS',
  isActive: true,
};

const defaultComponentRule: PayrollComponentRule = {
  componentType: 'Deduction',
  componentCode: '',
  category: '',
  calculationType: 'FixedAmount',
  amount: 0,
  taxFreeCeiling: 0,
  employerAmount: 0,
  afterTax: false,
  employerTaxable: false,
  applicable: true,
  legacyCompanyCode: '001',
};

const defaultCodeType: PayrollCodeType = {
  codeType: '',
  description: '',
  accountCode: '',
  blocked: false,
  dependent: false,
  dependentOnCodeType: '',
  legacyCompanyCode: '001',
};

const defaultCodeValue: PayrollCodeValue = {
  codeType: '',
  actualCode: '',
  description: '',
  additionalDescription: '',
  accountCode: '',
  blocked: false,
  dependentCodeType: '',
  dependentActualCode: '',
  legacyCompanyCode: '001',
};

const defaultHoliday: PayrollHoliday = {
  holidayDate: today,
  description: '',
  legacyCompanyCode: '001',
};

const defaultNonWorkingDay: PayrollNonWorkingDay = {
  dayCode: '',
  description: '',
  overtimeRate: 0,
  legacyCompanyCode: '001',
};

const defaultBankBranch: PayrollBankBranch = {
  branchSetupCode: '',
  bankCode: '',
  branchCode: '',
  branchDescription: '',
  region: '',
  sortCode: '',
  accountCode: '',
  legacyCompanyCode: '001',
};

type PayrollBankMaster = {
  id?: string;
  branchSetupCode?: string | null;
  bankCode: string;
  bankName: string;
  legacyCompanyCode?: string | null;
  branchCount?: number;
};

const bankMasterBranchCode = '__BANK__';

type GradeSetupDraft = {
  gradeType: string;
  currencyCode: string;
  firstGrade: string;
  lastGrade: string;
  unitStep: string;
  gradePrefix: string;
  gradeSuffix: string;
  prefixSeparator: string;
  suffixSeparator: string;
  firstNotch: string;
  lastNotch: string;
};

type GradeSetupRow = {
  order: number;
  gradeName: string;
  reportingName: string;
  firstNotch: string;
  unitStep: string;
  lastNotch: string;
  grade?: PayrollGrade;
};

type TaxTableGridRow = PayrollTaxBand & {
  label: string;
  taxManuallyEdited?: boolean;
};

const defaultGradeSetupDraft: GradeSetupDraft = {
  gradeType: 'COMBINED',
  currencyCode: 'GHC',
  firstGrade: '1',
  lastGrade: '12',
  unitStep: '1',
  gradePrefix: 'GRADE-',
  gradeSuffix: '',
  prefixSeparator: 'None',
  suffixSeparator: 'Space',
  firstNotch: '1',
  lastNotch: '10',
};

const defaultLoanPolicy: PayrollLoanPolicy = {
  code: '',
  name: '',
  maxLoanAmount: 0,
  interestRatePercent: 0,
  applyInterest: false,
  maxPaybackPeriods: 0,
  maxDebitRatioPercent: 0,
  interestType: 'Flat Rate',
  legacyCompanyCode: '001',
  isActive: true,
};

const defaultLeaveSetup: PayrollLeaveSetup = {
  category: '',
  categoryDetail: '',
  legacyCompanyCode: '001',
};

const defaultLeaveDetail: PayrollLeaveSetupDetail = {
  category: '',
  categoryDetail: '',
  days: 0,
  serviceFrom: 0,
  serviceTo: 0,
  sequenceNo: 1,
  legacyCompanyCode: '001',
};

const defaultOvertimePolicy: PayrollOvertimePolicy = {
  code: 'DEFAULT',
  name: 'Default overtime setup',
  normalWorkingHours: 8,
  normalHours: 0,
  weekdayRate: 1.5,
  holidayRate: 2,
  specialDutyRate: 0,
  taxable: true,
  taxCeiling: 0,
  separateOvertimeTax: false,
  maxOvertimeAmount: 0,
  maxOvertimeIsPercent: false,
  maxOvertimeType: '',
  leaveRecallRate: 0,
  payPeriod: currentPayPeriod,
  payPeriodFrom: today,
  payPeriodTo: today,
  maxOvertimeSeparateTax: 0,
  minimumBasicForSeparateTax: 0,
  minimumSeparateOvertimePercent: 0,
  legacyCompanyCode: '001',
  isDefault: true,
  isActive: true,
  ranges: [],
};

const defaultGrade: PayrollGrade = {
  gradeId: '',
  gradeType: '',
  gradeName: '',
  systemGradeName: '',
  minValue: 0,
  maxValue: 0,
  midPoint: 0,
  currencyCode: 'GHS',
  startPoint: '',
  incrementStep: 0,
  ceiling: '',
  startDate: today,
  endDate: '',
  businessUnitId: '',
  companyId: '',
  orderField: 1,
  reportingName: '',
  enforceNotchConsistency: false,
  legacyCompanyCode: '001',
  isActive: true,
};

const defaultGradeNotch: PayrollGradeNotch = {
  payrollGradeId: '',
  gradeId: '',
  systemGradeName: '',
  notch: '',
  value: 0,
  currencyCode: 'GHS',
  startDate: today,
  endDate: '',
  orderField: 1,
  businessUnitId: '',
  companyId: '',
  annualisedValue: 0,
  gradeName: '',
  reportingName: '',
  legacyCompanyCode: '001',
  hourlyRate: 0,
};

const defaultTaxBand: PayrollTaxBand = {
  taxType: 'P',
  serialNo: 1,
  description: '',
  lowerBound: 0,
  upperBound: null,
  taxableIncome: 0,
  ratePercent: 0,
  fixedAmount: 0,
  perMonthAmount: 0,
  cumulativeTax: 0,
  cumulativeSalary: 0,
  payPeriod: currentPayPeriod,
  payPeriodFrom: today,
  payPeriodTo: today,
  legacyCompanyCode: '001',
  isAnnual: false,
  effectiveFrom: today,
  effectiveTo: null,
  isActive: true,
};

const defaultTaxRelief: PayrollTaxRelief = {
  code: '',
  name: '',
  calculationType: 'FixedAmount',
  amount: 0,
  factor: 1,
  appliesByDefault: true,
  isActive: true,
};

const defaultBonusPolicy: PayrollBonusPolicy = {
  code: '',
  name: '',
  calculationType: 'FixedAmount',
  amount: 0,
  taxable: true,
  separateTax: false,
  taxFreeCeiling: 0,
  minimumToTax: 0,
  annualSalaryPercentToTax: 0,
  taxRate: 0,
  nextPayPeriod: currentPayPeriod,
  lastPayPeriod: null,
  nextPayPeriodDate: today,
  lastPayPeriodDate: null,
  category: 'All',
  cycle: 'Recurring',
  paySeparate: false,
  perAnnual: false,
  minimumMonths: 0,
  prorate: false,
  isActive: true,
};

const defaultBonusException: PayrollBonusException = {
  bonusCode: '',
  employeeProfileId: null,
  employeeNumber: '',
  employeeName: '',
  calculationType: 'FixedAmount',
  amount: 0,
  taxable: true,
  applicable: true,
  currencyCode: 'GHS',
  legacyCompanyCode: '001',
};

type BackpayOperationType = PayrollBackpayPolicy['operationType'];

const defaultBackpayPolicy: PayrollBackpayPolicy = {
  operationType: 'IncreaseSalary',
  calculationType: 'PercentageOfBasic',
  amount: 10,
  numberOfMonths: null,
  effectiveDate: today,
  applyTax: false,
  applySsf: false,
  minimumServiceMode: 'In Service Since',
  minimumServiceValue: null,
  minimumServiceDate: null,
  categoryType: 'All Staff',
  legacyCompanyCode: '001',
};

const defaultBackpayRule: PayrollBackpayRule = {
  operationType: 'IncreaseSalary',
  categoryType: 'Staff Category',
  categoryCode: '',
  categoryName: '',
  calculationType: 'PercentageOfBasic',
  amount: 10,
  applicable: true,
  legacyCompanyCode: '001',
};

const defaultBackpayException: PayrollBackpayException = {
  operationType: 'SalaryArrears',
  employeeProfileId: null,
  employeeNumber: '',
  employeeName: '',
  calculationType: 'PercentageOfBasic',
  amount: 10,
  applicable: true,
  legacyCompanyCode: '001',
};

const implementedMenuIds = new Set([
  'A0000102',
  'A0000103',
  'A0000104',
  'A0000106',
  'A0000107',
  'A0000108',
  'A0000109',
  'A0000114',
  'A0000115',
  'A0000116',
  'A0000117',
  'A0000118',
  'A0000119',
  'A0000131',
]);

const payrollAdministrationMenuItems: PayrollLegacyMenuItem[] = [
  { menuId: 'A0000102', parentMenuId: 'A00001', caption: 'Codes Description', path: 'Main Menu > Setup > Codes Description', itemType: 'Form', target: 'GE3_004.fmb', companyScope: '001', sequenceNo: 2, implemented: true },
  { menuId: 'A0000103', parentMenuId: 'A00001', caption: 'Parameters', path: 'Main Menu > Setup > Parameters', itemType: 'Form', target: 'GE3_007.fmb', companyScope: '001', sequenceNo: 3, implemented: true },
  { menuId: 'A0000104', parentMenuId: 'A00001', caption: 'Holiday Controls', path: 'Main Menu > Setup > Holiday Controls', itemType: 'Form', target: 'GE3_005.fmb', companyScope: '001', sequenceNo: 4, implemented: true },
  { menuId: 'A0000106', parentMenuId: 'A00001', caption: 'Bank Branch Setup', path: 'Main Menu > Setup > Bank Branch Setup', itemType: 'Form', target: 'GE3_010.fmb', companyScope: '001', sequenceNo: 6, implemented: true },
  { menuId: 'A0000107', parentMenuId: 'A00001', caption: 'Loan Setup', path: 'Main Menu > Setup > Loan Setup', itemType: 'Form', target: 'PR3_003.fmb', companyScope: '001', sequenceNo: 7, implemented: true },
  { menuId: 'A0000109', parentMenuId: 'A00001', caption: 'Overtime Setup', path: 'Main Menu > Setup > Overtime Setup', itemType: 'Form', target: 'PR3_002.fmb', companyScope: '001', sequenceNo: 9, implemented: true },
  { menuId: 'A0000114', parentMenuId: 'A00001', caption: 'Grades Setup', path: 'Main Menu > Setup > Grades Setup', itemType: 'Form', target: 'PR3_007.fmb', companyScope: '001', sequenceNo: 14, implemented: true },
  { menuId: 'A0000115', parentMenuId: 'A00001', caption: 'Tax Table', path: 'Main Menu > Setup > Tax Table', itemType: 'Form', target: 'PR3_004.fmb', companyScope: '001', sequenceNo: 15, implemented: true },
  { menuId: 'A0000116', parentMenuId: 'A00001', caption: 'Tax Relief Setup', path: 'Main Menu > Setup > Tax Relief Setup', itemType: 'Form', target: 'PR3_017.fmb', companyScope: '001', sequenceNo: 16, implemented: true },
  { menuId: 'A0000117', parentMenuId: 'A00001', caption: 'Allowances & Deductions Setup', path: 'Main Menu > Setup > Allowances & Deductions Setup', itemType: 'Form', target: 'PR3_009.fmb', companyScope: '001', sequenceNo: 17, implemented: true },
  { menuId: 'A0000118', parentMenuId: 'A00001', caption: 'Bonus Setup', path: 'Main Menu > Setup > Bonus Setup', itemType: 'Form', target: 'PR3_022.fmb', companyScope: '001', sequenceNo: 18, implemented: true },
  { menuId: 'A0000119', parentMenuId: 'A00001', caption: 'Backpay / Salary Increase', path: 'Main Menu > Setup > Backpay / Salary Increase', itemType: 'Form', target: 'PR3_023.fmb', companyScope: '001', sequenceNo: 19, implemented: true },
  { menuId: 'A0000131', parentMenuId: 'A00001', caption: 'Budget Analysis', path: 'Main Menu > Setup > Budget Analysis', itemType: 'Form', target: 'PR3_032.fmb', companyScope: 'DEMO', sequenceNo: 31, implemented: true },
];

const completedMenuIcons: Record<string, LucideIcon> = {
  A0000102: Boxes,
  A0000103: Settings,
  A0000104: CalendarCheck,
  A0000106: Landmark,
  A0000107: Banknote,
  A0000108: CalendarDays,
  A0000109: Timer,
  A0000114: GraduationCap,
  A0000115: Calculator,
  A0000116: HandCoins,
  A0000117: CreditCard,
  A0000118: Award,
  A0000119: ArrowUp,
  A0000131: Calculator,
};

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function nullableDateValue(value?: string | null) {
  const date = dateValue(value);
  return date || null;
}

function monthValue(value?: string | null) {
  return dateValue(value).slice(0, 7);
}

function formatPayrollPeriodLabel(from?: string | null, to?: string | null, period?: number) {
  const source = dateValue(to) || dateValue(from);
  if (source) {
    const [year, month] = source.split('-').map(Number);
    if (year && month) {
      return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
    }
  }

  const periodText = String(period || '');
  if (/^\d{6}$/.test(periodText)) {
    const year = Number(periodText.slice(0, 4));
    const month = Number(periodText.slice(4, 6));
    if (year && month >= 1 && month <= 12) {
      return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
    }
  }

  return 'Current payroll period';
}

function nullableMonthValue(value?: string | null) {
  return value ? `${value}-01` : null;
}

function numberOrNull(value: string) {
  return value === '' ? null : Number(value);
}

function formatAmount(value: number | null | undefined) {
  return Number(value || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

const numericNavigationKeys = new Set(['Backspace', 'Delete', 'Tab', 'Enter', 'Escape', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End']);

function isNumericInput(target: EventTarget | null): target is HTMLInputElement {
  return target instanceof HTMLInputElement && target.type === 'number';
}

function handlePayrollNumericKeyDown(event: KeyboardEvent<HTMLElement>) {
  if (!isNumericInput(event.target)) {
    return;
  }

  const input = event.target;
  if (event.ctrlKey || event.metaKey || event.altKey || numericNavigationKeys.has(event.key)) {
    return;
  }

  if (/^\d$/.test(event.key)) {
    return;
  }

  if (event.key === '.' && !input.value.includes('.')) {
    return;
  }

  event.preventDefault();
}

function handlePayrollNumericPaste(event: ClipboardEvent<HTMLElement>) {
  if (!isNumericInput(event.target)) {
    return;
  }

  const pastedText = event.clipboardData.getData('text');
  if (!/^\d*\.?\d*$/.test(pastedText)) {
    event.preventDefault();
  }
}

function findBankCodeType(codeTypes: PayrollCodeType[]) {
  return (
    codeTypes.find((item) => item.codeType.toUpperCase() === 'BANK') ??
    codeTypes.find((item) => item.codeType.toUpperCase().includes('BANK')) ??
    codeTypes.find((item) => item.description.toUpperCase().includes('BANK'))
  );
}

function findLoanCodeType(codeTypes: PayrollCodeType[]) {
  const loanCodeAliases = new Set(['LOA', 'LOAN', 'LON', 'LNTYP', 'LTYPE']);
  const isLoanCodeType = (item: PayrollCodeType) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();

    return (
      loanCodeAliases.has(code) ||
      code.includes('LOAN') ||
      description.includes('LOAN') ||
      description.includes('ADVANCE')
    );
  };

  return (
    codeTypes.find((item) => item.codeType.trim().toUpperCase() === 'LOAN') ??
    codeTypes.find((item) => item.codeType.trim().toUpperCase() === 'LOA') ??
    codeTypes.find(isLoanCodeType)
  );
}

function findTaxReliefCodeType(codeTypes: PayrollCodeType[]) {
  const taxReliefAliases = new Set(['REL', 'REF', 'TRL', 'TXR', 'TAXREL', 'TREL']);

  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return taxReliefAliases.has(code) || description.includes('TAX RELIEF') || description.includes('RELIEF');
  });
}

function findBonusCodeType(codeTypes: PayrollCodeType[]) {
  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return ['BON', 'BONUS', 'BNS'].includes(code) || description.includes('BONUS');
  });
}

function findStaffCategoryCodeType(codeTypes: PayrollCodeType[]) {
  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return ['CAT', 'SCAT', 'STAFF'].includes(code) || description.includes('STAFF CATEGORY') || description.includes('STAFF CATEG');
  });
}

function findDepartmentCodeType(codeTypes: PayrollCodeType[]) {
  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return ['DEP', 'DEPT'].includes(code) || description.includes('DEPARTMENT');
  });
}

function findPositionCodeType(codeTypes: PayrollCodeType[]) {
  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return ['POS', 'POST', 'JOB', 'RANK'].includes(code) || description.includes('POSITION') || description.includes('JOB') || description.includes('RANK');
  });
}

function validCodeTypeCandidates(values: Array<string | undefined>) {
  return values
    .map((value) => value?.trim().toUpperCase())
    .filter((value): value is string => typeof value === 'string' && value.length > 0 && value.length <= 5)
    .filter((value, index, allValues) => allValues.indexOf(value) === index);
}

const componentCodeTypeMap = {
  Allowance: ['ALW'],
  EmployeeContribution: ['CON', 'CONT'],
  Benefit: ['BEN'],
  Deduction: ['DED'],
} satisfies Record<string, string[]>;

const componentDescriptionAliases = {
  Allowance: ['ALLOWANCE', 'ALLOWANCES'],
  EmployeeContribution: ['CONTRIBUTION', 'CONTRIBUTIONS'],
  Benefit: ['BENEFIT', 'BENEFITS'],
  Deduction: ['DEDUCTION', 'DEDUCTIONS'],
} satisfies Record<keyof typeof componentCodeTypeMap, string[]>;

function findComponentCodeType(codeTypes: PayrollCodeType[], componentType: keyof typeof componentCodeTypeMap) {
  const aliasValues = componentCodeTypeMap[componentType];
  const aliases = new Set(aliasValues);
  const descriptionAliases = componentDescriptionAliases[componentType];
  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return aliases.has(code) || descriptionAliases.some((alias) => description.includes(alias));
  });
}

function buildBankMasters(bankCodes: PayrollCodeValue[], branches: PayrollBankBranch[]): PayrollBankMaster[] {
  const branchCounts = branches
    .filter((branch) => branch.branchCode !== bankMasterBranchCode)
    .reduce<Record<string, number>>((counts, branch) => {
      counts[branch.bankCode] = (counts[branch.bankCode] ?? 0) + 1;
      return counts;
    }, {});

  return bankCodes
    .filter((item) => !item.blocked)
    .map((item) => ({
      id: item.id,
      branchSetupCode: item.codeType,
      bankCode: item.actualCode,
      bankName: item.description,
      legacyCompanyCode: item.legacyCompanyCode || '001',
      branchCount: branchCounts[item.actualCode] ?? 0,
    }))
    .sort((left, right) => left.bankCode.localeCompare(right.bankCode));
}

function separatorValue(value: string) {
  if (value === 'Space') {
    return ' ';
  }
  if (value === 'Dash') {
    return '-';
  }
  return '';
}

function composeGradeName(draft: GradeSetupDraft, gradeNo: number) {
  const prefixSeparator = draft.gradePrefix && !draft.gradePrefix.endsWith('-') && !draft.gradePrefix.endsWith('_') ? separatorValue(draft.prefixSeparator) : '';
  const suffixSeparator = draft.gradeSuffix ? separatorValue(draft.suffixSeparator) : '';
  return `${draft.gradePrefix}${prefixSeparator}${gradeNo}${suffixSeparator}${draft.gradeSuffix}`.trim();
}

function generateGradeSetupRows(draft: GradeSetupDraft): GradeSetupRow[] {
  const firstGrade = Number(draft.firstGrade) || 1;
  const lastGrade = Math.max(firstGrade, Number(draft.lastGrade) || firstGrade);
  const unitStep = draft.unitStep || '1';

  return Array.from({ length: lastGrade - firstGrade + 1 }, (_, index) => {
    const order = index + 1;
    const gradeName = composeGradeName(draft, firstGrade + index);
    return {
      order,
      gradeName,
      reportingName: gradeName,
      firstNotch: draft.firstNotch || '1',
      unitStep,
      lastNotch: draft.lastNotch || '10',
    };
  });
}

function gradeSetupRowsFromExisting(grades: PayrollGrade[]): GradeSetupRow[] {
  return [...grades]
    .sort((left, right) => (left.orderField ?? 0) - (right.orderField ?? 0) || left.gradeName.localeCompare(right.gradeName))
    .map((grade, index) => ({
      order: grade.orderField ?? index + 1,
      gradeName: grade.gradeName,
      reportingName: grade.reportingName || grade.gradeName,
      firstNotch: grade.startPoint || '1',
      unitStep: String(grade.incrementStep ?? 1),
      lastNotch: grade.ceiling || '10',
      grade,
    }));
}

const taxTableTypeOptions = ['Tax', 'Overtime'];
const defaultTaxTableRows = [
  { label: 'First', taxableIncome: 20, ratePercent: 0, perMonthAmount: 0, cumulativeTax: 0, cumulativeSalary: 20 },
  { label: 'Next', taxableIncome: 20, ratePercent: 5, perMonthAmount: 1, cumulativeTax: 1, cumulativeSalary: 40 },
  { label: 'Next', taxableIncome: 100, ratePercent: 10, perMonthAmount: 10, cumulativeTax: 11, cumulativeSalary: 140 },
  { label: 'Next', taxableIncome: 660, ratePercent: 17.5, perMonthAmount: 115.5, cumulativeTax: 126.5, cumulativeSalary: 800 },
  { label: 'Exceeding', taxableIncome: 800, ratePercent: 25, perMonthAmount: 200, cumulativeTax: 326.5, cumulativeSalary: 1600 },
];
const defaultOvertimeTaxTableRows = [
  { label: 'First', taxableIncome: 40, ratePercent: 0, perMonthAmount: 0, cumulativeTax: 0, cumulativeSalary: 40 },
  { label: 'Next', taxableIncome: 100, ratePercent: 2.5, perMonthAmount: 2.5, cumulativeTax: 2.5, cumulativeSalary: 140 },
  { label: 'Next', taxableIncome: 140, ratePercent: 5, perMonthAmount: 7, cumulativeTax: 9.5, cumulativeSalary: 280 },
  { label: 'Exceeding', taxableIncome: 560, ratePercent: 9, perMonthAmount: 50.4, cumulativeTax: 59.9, cumulativeSalary: 840 },
];

const taxTableColumnLabels = {
  Tax: {
    baseAmount: 'Taxable Income',
    calculatedTax: 'Tax',
    cumulativeBase: 'Cumulative Salary',
  },
  Overtime: {
    baseAmount: 'Overtime Amount',
    calculatedTax: 'Overtime Tax',
    cumulativeBase: 'Cumulative Overtime',
  },
};

function displayTaxType(value?: string | null) {
  const normalized = (value || '').trim().toUpperCase();
  return normalized === 'OVERTIME' || normalized === 'OT' ? 'Overtime' : 'Tax';
}

function roundMoney(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

type BudgetScenario = 1 | 2 | 3;

function calculateBudgetAnalysisNewAmount(baseAmount: number, amount: number) {
  return roundMoney((Number(baseAmount) || 0) + ((Number(baseAmount) || 0) * (Number(amount) || 0)) / 100);
}

function calculateBudgetAnalysisPercent(variance: number, baseAmount: number) {
  const base = Number(baseAmount) || 0;
  return base === 0 ? 0 : roundMoney(((Number(variance) || 0) / base) * 100);
}

function recalculateBudgetAnalysisRow(row: PayrollBudgetAnalysisRow): PayrollBudgetAnalysisRow {
  const baseAmount = Number(row.baseAmount) || 0;
  const amount1 = Number(row.amount1) || 0;
  const amount2 = Number(row.amount2) || 0;
  const amount3 = Number(row.amount3) || 0;
  const newAmount1 = calculateBudgetAnalysisNewAmount(baseAmount, amount1);
  const newAmount2 = calculateBudgetAnalysisNewAmount(baseAmount, amount2);
  const newAmount3 = calculateBudgetAnalysisNewAmount(baseAmount, amount3);
  const variance1 = roundMoney(newAmount1 - baseAmount);
  const variance2 = roundMoney(newAmount2 - baseAmount);
  const variance3 = roundMoney(newAmount3 - baseAmount);

  return {
    ...row,
    baseAmount: roundMoney(baseAmount),
    amount1: roundMoney(amount1),
    newAmount1,
    variance1,
    percent1: calculateBudgetAnalysisPercent(variance1, baseAmount),
    amount2: roundMoney(amount2),
    newAmount2,
    variance2,
    percent2: calculateBudgetAnalysisPercent(variance2, baseAmount),
    amount3: roundMoney(amount3),
    newAmount3,
    variance3,
    percent3: calculateBudgetAnalysisPercent(variance3, baseAmount),
  };
}

function applyBudgetBasicPercent(row: PayrollBudgetAnalysisRow, scenario: BudgetScenario, value: number): PayrollBudgetAnalysisRow {
  const amount = row.percentage ? value : 0;
  if (scenario === 1) {
    return recalculateBudgetAnalysisRow({ ...row, amount1: amount });
  }

  if (scenario === 2) {
    return recalculateBudgetAnalysisRow({ ...row, amount2: amount });
  }

  return recalculateBudgetAnalysisRow({ ...row, amount3: amount });
}

function calculateBudgetAnalysisTotals(rows: PayrollBudgetAnalysisRow[]) {
  return {
    totalBaseAmount: roundMoney(rows.reduce((sum, row) => sum + (Number(row.baseAmount) || 0), 0)),
    totalNewAmount1: roundMoney(rows.filter((row) => row.include1).reduce((sum, row) => sum + (Number(row.newAmount1) || 0), 0)),
    totalNewAmount2: roundMoney(rows.filter((row) => row.include2).reduce((sum, row) => sum + (Number(row.newAmount2) || 0), 0)),
    totalNewAmount3: roundMoney(rows.filter((row) => row.include3).reduce((sum, row) => sum + (Number(row.newAmount3) || 0), 0)),
    totalVariance1: roundMoney(rows.filter((row) => row.include1).reduce((sum, row) => sum + (Number(row.variance1) || 0), 0)),
    totalVariance2: roundMoney(rows.filter((row) => row.include2).reduce((sum, row) => sum + (Number(row.variance2) || 0), 0)),
    totalVariance3: roundMoney(rows.filter((row) => row.include3).reduce((sum, row) => sum + (Number(row.variance3) || 0), 0)),
  };
}

function budgetAnalysisRowKey(row: PayrollBudgetAnalysisRow, index: number) {
  return `${row.orderField}-${row.transactionType}-${row.actualTransaction || 'BASE'}-${index}`;
}

function recalculateTaxTableRows(rows: TaxTableGridRow[], selectedTaxType: string) {
  return rows.map((row, index) => {
    const taxableIncome = Number(row.taxableIncome ?? row.lowerBound ?? 0) || 0;
    const tax = Number(row.perMonthAmount ?? row.fixedAmount ?? 0) || 0;

    return {
      ...row,
      taxType: selectedTaxType,
      serialNo: index + 1,
      lowerBound: taxableIncome,
      taxableIncome,
      fixedAmount: tax,
      perMonthAmount: tax,
      cumulativeTax: row.cumulativeTax ?? null,
      cumulativeSalary: row.cumulativeSalary ?? null,
      payPeriod: row.payPeriod ?? currentPayPeriod,
      payPeriodFrom: dateValue(row.payPeriodFrom) || today,
      payPeriodTo: dateValue(row.payPeriodTo) || today,
      effectiveFrom: dateValue(row.effectiveFrom) || today,
      effectiveTo: nullableDateValue(row.effectiveTo),
      legacyCompanyCode: row.legacyCompanyCode || '001',
      isAnnual: false,
      isActive: true,
    };
  });
}

function buildTaxTableRows(taxBands: PayrollTaxBand[], selectedTaxType: string): TaxTableGridRow[] {
  const defaultRows = selectedTaxType === 'Overtime' ? defaultOvertimeTaxTableRows : defaultTaxTableRows;
  const selectedRows = taxBands
    .filter((row) => displayTaxType(row.taxType) === selectedTaxType)
    .sort((left, right) => left.serialNo - right.serialNo);

  if (selectedRows.length > 0) {
    return recalculateTaxTableRows(
      selectedRows.map((row, index) => ({
        ...defaultTaxBand,
        ...row,
        label: row.description || (index === 0 ? 'First' : index === selectedRows.length - 1 ? 'Exceeding' : 'Next'),
        taxManuallyEdited: true,
      })),
      selectedTaxType,
    );
  }

  return recalculateTaxTableRows(
    defaultRows.map((row, index) => ({
      ...defaultTaxBand,
      taxType: selectedTaxType,
      serialNo: index + 1,
      description: row.label,
      label: row.label,
      taxableIncome: row.taxableIncome,
      lowerBound: row.taxableIncome,
      ratePercent: row.ratePercent,
      fixedAmount: row.perMonthAmount,
      perMonthAmount: row.perMonthAmount,
      cumulativeTax: row.cumulativeTax,
      cumulativeSalary: row.cumulativeSalary,
      effectiveFrom: today,
      effectiveTo: null,
    })),
    selectedTaxType,
  );
}

function isExceedingTaxRow(row: TaxTableGridRow) {
  return (row.label || row.description || '').trim().toUpperCase() === 'EXCEEDING';
}

function calculateTaxFromRows(rows: TaxTableGridRow[], taxableIncome: number) {
  const income = Math.max(0, taxableIncome);
  if (income === 0) {
    return 0;
  }

  let previousCumulativeBase = 0;
  let previousCumulativeTax = 0;
  let lastBand: { cumulativeBase: number; cumulativeTax: number; ratePercent: number } | null = null;

  for (const row of rows) {
    const bandAmount = Number(row.taxableIncome ?? row.lowerBound ?? 0) || 0;
    const ratePercent = Number(row.ratePercent) || 0;
    const rowTax = Number(row.perMonthAmount ?? row.fixedAmount ?? 0) || 0;
    const cumulativeBase = Number(row.cumulativeSalary ?? 0) || roundMoney(previousCumulativeBase + bandAmount);
    const cumulativeTax = Number(row.cumulativeTax ?? 0) || roundMoney(previousCumulativeTax + rowTax);

    if (isExceedingTaxRow(row)) {
      return roundMoney(previousCumulativeTax + (Math.max(0, income - previousCumulativeBase) * ratePercent) / 100);
    }

    if (income <= cumulativeBase) {
      return roundMoney(previousCumulativeTax + ((income - previousCumulativeBase) * ratePercent) / 100);
    }

    previousCumulativeBase = cumulativeBase;
    previousCumulativeTax = cumulativeTax;
    lastBand = { cumulativeBase, cumulativeTax, ratePercent };
  }

  if (!lastBand) {
    return 0;
  }

  return roundMoney(lastBand.cumulativeTax + ((income - lastBand.cumulativeBase) * lastBand.ratePercent) / 100);
}

function estimateIncomeFromTaxRows(rows: TaxTableGridRow[], targetTax: number) {
  const safeTargetTax = Math.max(0, targetTax);
  if (safeTargetTax === 0) {
    return 0;
  }

  let previousCumulativeBase = 0;
  let previousCumulativeTax = 0;
  let lastBand: { cumulativeBase: number; cumulativeTax: number; ratePercent: number } | null = null;

  for (const row of rows) {
    const bandAmount = Number(row.taxableIncome ?? row.lowerBound ?? 0) || 0;
    const ratePercent = Number(row.ratePercent) || 0;
    const rowTax = Number(row.perMonthAmount ?? row.fixedAmount ?? 0) || 0;
    const cumulativeBase = Number(row.cumulativeSalary ?? 0) || roundMoney(previousCumulativeBase + bandAmount);
    const cumulativeTax = Number(row.cumulativeTax ?? 0) || roundMoney(previousCumulativeTax + rowTax);

    if (isExceedingTaxRow(row)) {
      if (ratePercent === 0) {
        return roundMoney(previousCumulativeBase);
      }

      return roundMoney(previousCumulativeBase + ((safeTargetTax - previousCumulativeTax) * 100) / ratePercent);
    }

    if (safeTargetTax <= cumulativeTax) {
      if (ratePercent === 0) {
        return roundMoney(cumulativeBase);
      }

      return roundMoney(previousCumulativeBase + ((safeTargetTax - previousCumulativeTax) * 100) / ratePercent);
    }

    previousCumulativeBase = cumulativeBase;
    previousCumulativeTax = cumulativeTax;
    lastBand = { cumulativeBase, cumulativeTax, ratePercent };
  }

  if (!lastBand || lastBand.ratePercent === 0) {
    return roundMoney(previousCumulativeBase);
  }

  return roundMoney(lastBand.cumulativeBase + ((safeTargetTax - lastBand.cumulativeTax) * 100) / lastBand.ratePercent);
}

function BooleanField({
  checked,
  label,
  onChange,
}: {
  checked: boolean;
  label: string;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex min-h-9 items-center gap-2 text-sm">
      <Checkbox checked={checked} onCheckedChange={(value) => onChange(value === true)} />
      <span>{label}</span>
    </label>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

function CompactParameterField({
  label,
  children,
  wide = false,
}: {
  label: string;
  children: ReactNode;
  wide?: boolean;
}) {
  return (
    <div className={`grid gap-1 sm:grid-cols-[150px_minmax(0,1fr)] sm:items-center ${wide ? 'lg:col-span-2' : ''}`}>
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

function getDepth(item: PayrollLegacyMenuItem) {
  return Math.max(0, displayMenuPath(item).split('>').length - 1);
}

function displayMenuPath(item: PayrollLegacyMenuItem) {
  return item.path
    .split('>')
    .map((part) => part.trim())
    .filter((part) => part !== 'Main Menu' && part !== 'Setup')
    .join(' > ');
}

function PayrollFormTitle({
  menuId,
  children,
}: {
  menuId: string;
  children: ReactNode;
}) {
  const Icon = completedMenuIcons[menuId] ?? FileText;

  return (
    <CardTitle className="flex items-center gap-2 text-base">
      <Icon className="h-4 w-4" />
      {children}
    </CardTitle>
  );
}

function LegacyMenu({
  items,
  selectedId,
  onSelect,
}: {
  items: PayrollLegacyMenuItem[];
  selectedId: string;
  onSelect: (id: string) => void;
}) {
  return (
    <aside className="rounded-md border bg-background">
      <div className="border-b px-3 py-2">
        <h2 className="text-sm font-semibold text-foreground">Payroll Setup</h2>
      </div>
      <div className="max-h-[calc(100vh-190px)] overflow-auto p-2">
        {items.map((item) => {
          const isSelected = selectedId === item.menuId;
          const isFolder = item.itemType === 'Folder';
          const Icon = completedMenuIcons[item.menuId] ?? (isFolder ? ChevronRight : item.implemented ? Check : Circle);
          return (
            <button
              key={item.menuId}
              type="button"
              onClick={() => onSelect(item.menuId)}
              className={`flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm hover:bg-muted ${
                isSelected ? 'bg-primary text-primary-foreground hover:bg-primary' : ''
              }`}
              style={{ paddingLeft: `${8 + getDepth(item) * 14}px` }}
            >
              <Icon className="h-3.5 w-3.5 shrink-0" />
              <span className="truncate">{item.caption}</span>
              {item.implemented && !isFolder ? <span className="ml-auto h-1.5 w-1.5 rounded-full bg-emerald-500" /> : null}
            </button>
          );
        })}
      </div>
    </aside>
  );
}

export default function PayrollAdministrationPage() {
  const { toast } = useToast();
  const [selectedMenuId, setSelectedMenuId] = useState('A0000102');
  const [setup, setSetup] = useState<PayrollSetupSummary | null>(null);
  const [codeSetup, setCodeSetup] = useState<PayrollCodeSetup>({ codeTypes: [], codeValues: [] });
  const [bankCodeValues, setBankCodeValues] = useState<PayrollCodeValue[]>([]);
  const [loanCodeValues, setLoanCodeValues] = useState<PayrollCodeValue[]>([]);
  const [taxReliefCodeValues, setTaxReliefCodeValues] = useState<PayrollCodeValue[]>([]);
  const [bonusCodeValues, setBonusCodeValues] = useState<PayrollCodeValue[]>([]);
  const [staffCategoryCodeValues, setStaffCategoryCodeValues] = useState<PayrollCodeValue[]>([]);
  const [departmentCodeValues, setDepartmentCodeValues] = useState<PayrollCodeValue[]>([]);
  const [positionCodeValues, setPositionCodeValues] = useState<PayrollCodeValue[]>([]);
  const [payrollEmployees, setPayrollEmployees] = useState<PayrollEmployeeProfile[]>([]);
  const [componentCodeValues, setComponentCodeValues] = useState<Record<string, PayrollCodeValue[]>>({
    Allowance: [],
    EmployeeContribution: [],
    Benefit: [],
    Deduction: [],
  });
  const [holidaySetup, setHolidaySetup] = useState<PayrollHolidaySetup>({ holidays: [], nonWorkingDays: [] });
  const [bankBranches, setBankBranches] = useState<PayrollBankBranch[]>([]);
  const [leaveSetup, setLeaveSetup] = useState<PayrollLeaveSetupCollection>({ leaveSetups: [], leaveSetupDetails: [] });
  const [gradeSetup, setGradeSetup] = useState<PayrollGradeSetup>({ grades: [], notches: [] });
  const [taxTable, setTaxTable] = useState<PayrollTaxTable>({ taxBands: [] });
  const [financeCurrencies, setFinanceCurrencies] = useState<CurrencyListDto[]>([]);
  const [parameters, setParameters] = useState<PayrollParameterSet>(defaultParameters);
  const [codeType, setCodeType] = useState<PayrollCodeType>(defaultCodeType);
  const [codeValue, setCodeValue] = useState<PayrollCodeValue>(defaultCodeValue);
  const [holiday, setHoliday] = useState<PayrollHoliday>(defaultHoliday);
  const [nonWorkingDay, setNonWorkingDay] = useState<PayrollNonWorkingDay>(defaultNonWorkingDay);
  const [bankBranch, setBankBranch] = useState<PayrollBankBranch>(defaultBankBranch);
  const [loanPolicy, setLoanPolicy] = useState<PayrollLoanPolicy>(defaultLoanPolicy);
  const [taxRelief, setTaxRelief] = useState<PayrollTaxRelief>(defaultTaxRelief);
  const [component, setComponent] = useState<PayrollComponent>(defaultComponent);
  const [bonusPolicy, setBonusPolicy] = useState<PayrollBonusPolicy>(defaultBonusPolicy);
  const [backpayPolicy, setBackpayPolicy] = useState<PayrollBackpayPolicy>(defaultBackpayPolicy);
  const [leaveHeader, setLeaveHeader] = useState<PayrollLeaveSetup>(defaultLeaveSetup);
  const [leaveDetail, setLeaveDetail] = useState<PayrollLeaveSetupDetail>(defaultLeaveDetail);
  const [overtimePolicy, setOvertimePolicy] = useState<PayrollOvertimePolicy>(defaultOvertimePolicy);
  const [grade, setGrade] = useState<PayrollGrade>(defaultGrade);
  const [gradeNotch, setGradeNotch] = useState<PayrollGradeNotch>(defaultGradeNotch);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const selectedMenu = useMemo(
    () => payrollAdministrationMenuItems.find((item) => item.menuId === selectedMenuId) ?? payrollAdministrationMenuItems.find((item) => item.menuId === 'A0000102') ?? null,
    [selectedMenuId],
  );
  const bankMasters = useMemo(() => buildBankMasters(bankCodeValues, bankBranches), [bankCodeValues, bankBranches]);

  const loadCodes = useCallback(async (nextCodeType?: string) => {
    const result = await payrollService.getCodeSetup(nextCodeType);
    setCodeSetup(result);
    const selectedType = result.selectedCodeType || result.codeTypes[0]?.codeType || '';
    setCodeValue((current) => ({ ...current, codeType: selectedType, payrollCodeTypeId: result.codeTypes.find((item) => item.codeType === selectedType)?.id }));
    return result;
  }, []);

  const loadBankCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const bankCodeType = findBankCodeType(codeTypes);
    if (!bankCodeType) {
      setBankCodeValues([]);
      return;
    }

    const result = await payrollService.getCodeSetup(bankCodeType.codeType);
    setBankCodeValues(result.codeValues);
  }, []);

  const loadLoanCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const loanCodeType = findLoanCodeType(codeTypes);
    const candidates = validCodeTypeCandidates([
      loanCodeType?.codeType,
      'LOA',
      'LOAN',
      'LON',
      'LNTYP',
      'LTYPE',
    ]);

    for (const candidate of candidates) {
      const result = await payrollService.getCodeSetup(candidate);
      if (result.codeValues.length > 0 || result.selectedCodeType === 'LOA') {
        setLoanCodeValues(result.codeValues);
        return;
      }
    }

    setLoanCodeValues([]);
  }, []);

  const loadTaxReliefCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const taxReliefCodeType = findTaxReliefCodeType(codeTypes);
    const candidates = validCodeTypeCandidates([
      taxReliefCodeType?.codeType,
      'REL',
      'REF',
      'TXR',
      'TRL',
      'TREL',
      'TAXREL',
    ]);

    for (const candidate of candidates) {
      const result = await payrollService.getCodeSetup(candidate);
      if (result.codeValues.length > 0) {
        setTaxReliefCodeValues(result.codeValues);
        return;
      }
    }

    setTaxReliefCodeValues([]);
  }, []);

  const loadBonusCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const bonusCodeType = findBonusCodeType(codeTypes);
    const candidates = validCodeTypeCandidates([
      bonusCodeType?.codeType,
      'BON',
      'BONUS',
      'BNS',
    ]);

    for (const candidate of candidates) {
      const result = await payrollService.getCodeSetup(candidate);
      if (result.codeValues.length > 0) {
        setBonusCodeValues(result.codeValues);
        return;
      }
    }

    setBonusCodeValues([]);
  }, []);

  const loadStaffCategoryCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const staffCategoryCodeType = findStaffCategoryCodeType(codeTypes);
    const candidates = validCodeTypeCandidates([
      staffCategoryCodeType?.codeType,
      'CAT',
      'SCAT',
      'STCAT',
      'STAFF',
    ]);

    for (const candidate of candidates) {
      const result = await payrollService.getCodeSetup(candidate);
      if (result.codeValues.length > 0) {
        setStaffCategoryCodeValues(result.codeValues);
        return;
      }
    }

    setStaffCategoryCodeValues([]);
  }, []);

  const loadDepartmentCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const departmentCodeType = findDepartmentCodeType(codeTypes);
    const candidates = validCodeTypeCandidates([
      departmentCodeType?.codeType,
      'DEP',
      'DEPT',
    ]);

    for (const candidate of candidates) {
      const result = await payrollService.getCodeSetup(candidate);
      if (result.codeValues.length > 0) {
        setDepartmentCodeValues(result.codeValues);
        return;
      }
    }

    setDepartmentCodeValues([]);
  }, []);

  const loadPositionCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const positionCodeType = findPositionCodeType(codeTypes);
    const candidates = validCodeTypeCandidates([
      positionCodeType?.codeType,
      'POS',
      'JOB',
      'RANK',
    ]);

    for (const candidate of candidates) {
      const result = await payrollService.getCodeSetup(candidate);
      if (result.codeValues.length > 0) {
        setPositionCodeValues(result.codeValues);
        return;
      }
    }

    setPositionCodeValues([]);
  }, []);

  const loadComponentCodes = useCallback(async (codeTypes: PayrollCodeType[]) => {
    const nextValues: Record<string, PayrollCodeValue[]> = {
      Allowance: [],
      EmployeeContribution: [],
      Benefit: [],
      Deduction: [],
    };

    for (const componentType of Object.keys(componentCodeTypeMap) as Array<keyof typeof componentCodeTypeMap>) {
      const codeType = findComponentCodeType(codeTypes, componentType);
      const candidates = validCodeTypeCandidates([
        codeType?.codeType,
        ...componentCodeTypeMap[componentType],
      ]);

      for (const candidate of candidates) {
        const result = await payrollService.getCodeSetup(candidate);
        if (result.codeValues.length > 0) {
          nextValues[componentType] = result.codeValues;
          break;
        }
      }
    }

    setComponentCodeValues(nextValues);
  }, []);

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setupSummary, holidays, branches, leaves, overtime, grades, taxes, currencies, baseCurrency, employees] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getHolidaySetup(),
        payrollService.getBankBranches(),
        payrollService.getLeaveSetup(),
        payrollService.getOvertimeSetup(),
        payrollService.getGradeSetup(),
        payrollService.getTaxTable(),
        currencyService.getActive().catch(() => []),
        currencyService.getBaseCurrency().catch(() => null),
        payrollService.getEmployeeProfiles().catch(() => []),
      ]);

      setSetup(setupSummary);
      setHolidaySetup(holidays);
      setBankBranches(branches);
      setLeaveSetup(leaves);
      setGradeSetup(grades);
      setTaxTable(taxes);
      setFinanceCurrencies(currencies);
      setPayrollEmployees(employees);
      if (overtime.overtimePolicies[0]) {
        const activeOvertimePolicy = overtime.overtimePolicies[0];
        setOvertimePolicy({
          ...defaultOvertimePolicy,
          ...activeOvertimePolicy,
          normalHours: activeOvertimePolicy.normalHours ?? defaultOvertimePolicy.normalHours,
          taxCeiling: activeOvertimePolicy.taxCeiling ?? defaultOvertimePolicy.taxCeiling,
          maxOvertimeType: activeOvertimePolicy.maxOvertimeType ?? '',
          leaveRecallRate: activeOvertimePolicy.leaveRecallRate ?? defaultOvertimePolicy.leaveRecallRate,
          payPeriod: activeOvertimePolicy.payPeriod ?? defaultOvertimePolicy.payPeriod,
          payPeriodFrom: dateValue(activeOvertimePolicy.payPeriodFrom) || today,
          payPeriodTo: dateValue(activeOvertimePolicy.payPeriodTo) || today,
          ranges: activeOvertimePolicy.ranges ?? [],
        });
      } else {
        setOvertimePolicy(defaultOvertimePolicy);
      }
      setGradeNotch((current) => ({
        ...current,
        payrollGradeId: current.payrollGradeId || grades.grades[0]?.id || '',
        gradeName: current.gradeName || grades.grades[0]?.gradeName || '',
        gradeId: current.gradeId || grades.grades[0]?.gradeId || '',
        currencyCode: current.currencyCode || grades.grades[0]?.currencyCode || 'GHS',
        legacyCompanyCode: current.legacyCompanyCode || grades.grades[0]?.legacyCompanyCode || '001',
      }));
      if (setupSummary.activeParameters) {
        setParameters({
          ...defaultParameters,
          ...setupSummary.activeParameters,
          baseCurrency: setupSummary.activeParameters.baseCurrency || baseCurrency?.code || currencies[0]?.code || defaultParameters.baseCurrency,
          currentPeriodFrom: dateValue(setupSummary.activeParameters.currentPeriodFrom),
          currentPeriodTo: dateValue(setupSummary.activeParameters.currentPeriodTo),
          payMode: setupSummary.activeParameters.payMode || defaultParameters.payMode,
          timeSheetMode: setupSummary.activeParameters.timeSheetMode || (setupSummary.activeParameters.timesheetEnabled ? 'D' : defaultParameters.timeSheetMode),
          leaveClassification: setupSummary.activeParameters.leaveClassification || defaultParameters.leaveClassification,
          defaultEmployeePaymentMethod: setupSummary.activeParameters.defaultEmployeePaymentMethod || defaultParameters.defaultEmployeePaymentMethod,
          defaultPayBasis: setupSummary.activeParameters.defaultPayBasis || defaultParameters.defaultPayBasis,
        });
      } else if (baseCurrency?.code || currencies[0]?.code) {
        setParameters((current) => ({ ...current, baseCurrency: baseCurrency?.code || currencies[0]?.code || current.baseCurrency }));
      }
      const activeBackpayPolicy = setupSummary.backpayPolicies.find((item) => item.operationType === 'IncreaseSalary');
      setBackpayPolicy(activeBackpayPolicy ? { ...defaultBackpayPolicy, ...activeBackpayPolicy, effectiveDate: nullableDateValue(activeBackpayPolicy.effectiveDate), minimumServiceDate: nullableDateValue(activeBackpayPolicy.minimumServiceDate) } : defaultBackpayPolicy);
      const loadedCodes = await loadCodes();
      await loadBankCodes(loadedCodes.codeTypes);
      await loadLoanCodes(loadedCodes.codeTypes);
      await loadTaxReliefCodes(loadedCodes.codeTypes);
      await loadBonusCodes(loadedCodes.codeTypes);
      await loadStaffCategoryCodes(loadedCodes.codeTypes);
      await loadDepartmentCodes(loadedCodes.codeTypes);
      await loadPositionCodes(loadedCodes.codeTypes);
      await loadComponentCodes(loadedCodes.codeTypes);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load payroll setup.');
    } finally {
      setLoading(false);
    }
  }, [loadBankCodes, loadBonusCodes, loadCodes, loadComponentCodes, loadDepartmentCodes, loadLoanCodes, loadPositionCodes, loadStaffCategoryCodes, loadTaxReliefCodes]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const save = async (label: string, action: () => Promise<unknown>, after?: () => Promise<void>) => {
    setBusy(label);
    setError(null);
    try {
      await action();
      toast({ title: 'Payroll setup saved', description: `${label} updated.` });
      if (after) {
        await after();
      } else {
        await loadAll();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Payroll setup save failed.';
      setError(message);
      toast({ title: 'Payroll setup error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  const updateParameters = <K extends keyof PayrollParameterSet>(key: K, value: PayrollParameterSet[K]) =>
    setParameters((current) => ({ ...current, [key]: value }));

  const selectCodeType = async (value: string) => {
    const selected = codeSetup.codeTypes.find((item) => item.codeType === value);
    setCodeValue((current) => ({ ...current, codeType: value, payrollCodeTypeId: selected?.id }));
    const result = await loadCodes(value);
    await loadBankCodes(result.codeTypes);
    await loadLoanCodes(result.codeTypes);
    await loadTaxReliefCodes(result.codeTypes);
    await loadBonusCodes(result.codeTypes);
    await loadComponentCodes(result.codeTypes);
  };

  return (
    <main
      className="space-y-4 px-4 pb-6 pt-4 [&_table]:text-xs [&_td]:px-2 [&_td]:py-1 [&_th]:h-8 [&_th]:px-2 [&_th]:py-1 [&_th]:text-xs"
      onKeyDownCapture={handlePayrollNumericKeyDown}
      onPasteCapture={handlePayrollNumericPaste}
    >
      {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</div>}

      <div className="grid gap-4 xl:grid-cols-[340px_minmax(0,1fr)]">
        <LegacyMenu items={payrollAdministrationMenuItems} selectedId={selectedMenuId} onSelect={setSelectedMenuId} />

        <section className="min-w-0">
          {loading ? (
            <div className="flex h-56 items-center justify-center rounded-md border">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : selectedMenuId === 'A0000102' ? (
            <CodesDescriptionForm
              codeSetup={codeSetup}
              codeType={codeType}
              codeValue={codeValue}
              busy={busy}
              onCodeTypeChange={setCodeType}
              onCodeValueChange={setCodeValue}
              onSelectCodeType={selectCodeType}
              onSaveCodeType={() =>
                save(
                  'Code type',
                  async () => {
                    const saved = await payrollService.upsertCodeType(codeType);
                    setCodeType(defaultCodeType);
                    setCodeValue((current) => ({ ...current, codeType: saved.codeType, payrollCodeTypeId: saved.id }));
                    const loadedCodes = await loadCodes(saved.codeType);
                    await loadBankCodes(loadedCodes.codeTypes);
                    await loadLoanCodes(loadedCodes.codeTypes);
                    await loadTaxReliefCodes(loadedCodes.codeTypes);
                    await loadBonusCodes(loadedCodes.codeTypes);
                    await loadComponentCodes(loadedCodes.codeTypes);
                  },
                  async () => undefined,
                )
              }
              onSaveCodeValue={() =>
                save(
                  'Actual code',
                  async () => {
                    await payrollService.upsertCodeValue(codeValue);
                    setCodeValue((current) => ({ ...defaultCodeValue, codeType: current.codeType, payrollCodeTypeId: current.payrollCodeTypeId, legacyCompanyCode: current.legacyCompanyCode || '001' }));
                    const loadedCodes = await loadCodes(codeValue.codeType);
                    await loadBankCodes(loadedCodes.codeTypes);
                    await loadLoanCodes(loadedCodes.codeTypes);
                    await loadTaxReliefCodes(loadedCodes.codeTypes);
                    await loadBonusCodes(loadedCodes.codeTypes);
                    await loadComponentCodes(loadedCodes.codeTypes);
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000103' ? (
            <ParametersForm
              parameters={parameters}
              currencies={financeCurrencies}
              busy={busy}
              onChange={updateParameters}
              onSave={() => save('Parameters', () => payrollService.upsertParameters(parameters))}
            />
          ) : selectedMenuId === 'A0000104' ? (
            <HolidayControlsForm
              setup={holidaySetup}
              holiday={holiday}
              nonWorkingDay={nonWorkingDay}
              busy={busy}
              onHolidayChange={setHoliday}
              onNonWorkingDayChange={setNonWorkingDay}
              onSaveHoliday={() =>
                save(
                  'Holiday',
                  async () => {
                    await payrollService.upsertHoliday(holiday);
                    setHoliday(defaultHoliday);
                    setHolidaySetup(await payrollService.getHolidaySetup());
                  },
                  async () => undefined,
                )
              }
              onSaveNonWorkingDay={() =>
                save(
                  'Non-working day',
                  async () => {
                    await payrollService.upsertNonWorkingDay(nonWorkingDay);
                    setNonWorkingDay(defaultNonWorkingDay);
                    setHolidaySetup(await payrollService.getHolidaySetup());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000106' ? (
            <BankBranchForm
              banks={bankMasters}
              branches={bankBranches}
              branch={bankBranch}
              busy={busy}
              onBranchChange={setBankBranch}
              onSaveBranch={() =>
                save(
                  'Bank branch',
                  async () => {
                    await payrollService.upsertBankBranch(bankBranch);
                    setBankBranch((current) => ({
                      ...defaultBankBranch,
                      branchSetupCode: current.branchSetupCode,
                      bankCode: current.bankCode,
                      legacyCompanyCode: current.legacyCompanyCode || '001',
                    }));
                    setBankBranches(await payrollService.getBankBranches());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000107' ? (
            <LoanSetupForm
              loans={setup?.loanPolicies ?? []}
              loanTypes={loanCodeValues}
              loan={loanPolicy}
              busy={busy}
              onChange={setLoanPolicy}
              onSave={() =>
                save('Loan setup', async () => {
                  await payrollService.upsertLoanPolicy(loanPolicy);
                  setLoanPolicy(defaultLoanPolicy);
                })
              }
            />
          ) : selectedMenuId === 'A0000108' ? (
            <LeaveSetupForm
              setup={leaveSetup}
              leaveHeader={leaveHeader}
              leaveDetail={leaveDetail}
              busy={busy}
              onHeaderChange={setLeaveHeader}
              onDetailChange={setLeaveDetail}
              onSaveHeader={() =>
                save(
                  'Leave setup',
                  async () => {
                    const saved = await payrollService.upsertLeaveSetup(leaveHeader);
                    setLeaveHeader(defaultLeaveSetup);
                    setLeaveDetail((current) => ({ ...current, payrollLeaveSetupId: saved.id, category: saved.category, categoryDetail: saved.categoryDetail, legacyCompanyCode: saved.legacyCompanyCode || '001' }));
                    setLeaveSetup(await payrollService.getLeaveSetup());
                  },
                  async () => undefined,
                )
              }
              onSaveDetail={() =>
                save(
                  'Leave detail',
                  async () => {
                    await payrollService.upsertLeaveSetupDetail(leaveDetail);
                    setLeaveDetail((current) => ({ ...defaultLeaveDetail, category: current.category, categoryDetail: current.categoryDetail, payrollLeaveSetupId: current.payrollLeaveSetupId, legacyCompanyCode: current.legacyCompanyCode || '001' }));
                    setLeaveSetup(await payrollService.getLeaveSetup());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000109' ? (
            <OvertimeSetupForm
              policy={overtimePolicy}
              busy={busy}
              onPolicyChange={setOvertimePolicy}
              onSavePolicy={() =>
                save(
                  'Overtime setup',
                  async () => {
                    const saved = await payrollService.upsertOvertimePolicy({
                      ...defaultOvertimePolicy,
                      ...overtimePolicy,
                      code: overtimePolicy.code || defaultOvertimePolicy.code,
                      name: overtimePolicy.name || defaultOvertimePolicy.name,
                      legacyCompanyCode: overtimePolicy.legacyCompanyCode || defaultOvertimePolicy.legacyCompanyCode,
                    });
                    setOvertimePolicy({
                      ...defaultOvertimePolicy,
                      ...saved,
                      normalHours: saved.normalHours ?? defaultOvertimePolicy.normalHours,
                      taxCeiling: saved.taxCeiling ?? defaultOvertimePolicy.taxCeiling,
                      maxOvertimeType: saved.maxOvertimeType ?? '',
                      leaveRecallRate: saved.leaveRecallRate ?? defaultOvertimePolicy.leaveRecallRate,
                      payPeriod: saved.payPeriod ?? defaultOvertimePolicy.payPeriod,
                      payPeriodFrom: dateValue(saved.payPeriodFrom) || today,
                      payPeriodTo: dateValue(saved.payPeriodTo) || today,
                      ranges: saved.ranges ?? [],
                    });
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000114' ? (
            <GradesSetupForm
              setup={gradeSetup}
              grade={grade}
              notch={gradeNotch}
              busy={busy}
              onGradeChange={setGrade}
              onNotchChange={setGradeNotch}
              onSaveGrade={() =>
                save(
                  'Grade',
                  async () => {
                    const saved = await payrollService.upsertGrade(grade);
                    setGrade(defaultGrade);
                    setGradeNotch((current) => ({
                      ...current,
                      payrollGradeId: saved.id || '',
                      gradeId: saved.gradeId || '',
                      gradeName: saved.gradeName,
                      systemGradeName: saved.systemGradeName || '',
                      currencyCode: saved.currencyCode,
                      legacyCompanyCode: saved.legacyCompanyCode || '001',
                    }));
                    setGradeSetup(await payrollService.getGradeSetup());
                  },
                  async () => undefined,
                )
              }
              onSaveGeneratedGrades={(rows, draft) =>
                save(
                  'Grade setup',
                  async () => {
                    for (const row of rows) {
                      const savedGrade = await payrollService.upsertGrade({
                        ...defaultGrade,
                        gradeType: draft.gradeType,
                        gradeName: row.gradeName,
                        systemGradeName: row.gradeName,
                        reportingName: row.reportingName,
                        currencyCode: draft.currencyCode,
                        startPoint: row.firstNotch,
                        incrementStep: Number(row.unitStep) || null,
                        ceiling: row.lastNotch,
                        orderField: row.order,
                        enforceNotchConsistency: grade.enforceNotchConsistency,
                        legacyCompanyCode: grade.legacyCompanyCode || '001',
                      });
                      const firstNotch = Number(row.firstNotch) || 1;
                      const lastNotch = Math.max(firstNotch, Number(row.lastNotch) || firstNotch);
                      const unitStep = Math.max(1, Number(row.unitStep) || 1);

                      for (let notchNo = firstNotch; notchNo <= lastNotch; notchNo += unitStep) {
                        await payrollService.upsertGradeNotch({
                          ...defaultGradeNotch,
                          payrollGradeId: savedGrade.id || '',
                          gradeId: savedGrade.gradeId || '',
                          gradeName: savedGrade.gradeName,
                          systemGradeName: savedGrade.systemGradeName || savedGrade.gradeName,
                          reportingName: savedGrade.reportingName || savedGrade.gradeName,
                          notch: String(notchNo),
                          currencyCode: savedGrade.currencyCode,
                          orderField: notchNo,
                          legacyCompanyCode: savedGrade.legacyCompanyCode || '001',
                        });
                      }
                    }
                    setGradeSetup(await payrollService.getGradeSetup());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000115' ? (
            <TaxTableForm
              table={taxTable}
              busy={busy}
              onSaveRows={(rows) =>
                save(
                  'Tax table',
                  async () => {
                    for (const row of rows) {
                      await payrollService.upsertTaxBand(row);
                    }
                    setTaxTable(await payrollService.getTaxTable());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000116' ? (
            <TaxReliefSetupForm
              reliefs={setup?.taxReliefs ?? []}
              reliefTypes={taxReliefCodeValues}
              relief={taxRelief}
              busy={busy}
              onChange={setTaxRelief}
              onSave={() =>
                save(
                  'Tax relief',
                  async () => {
                    await payrollService.upsertTaxRelief({ ...taxRelief, appliesByDefault: true, isActive: true });
                    setTaxRelief(defaultTaxRelief);
                    setSetup(await payrollService.getSetupSummary());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000117' ? (
            <AllowancesDeductionsSetupForm
              components={setup?.components ?? []}
              componentRules={setup?.componentRules ?? []}
              componentCodeValues={componentCodeValues}
              staffCategories={staffCategoryCodeValues}
              component={component}
              busy={busy}
              onChange={setComponent}
              onSave={(rules) =>
                save(
                  'Allowance/deduction setup',
                  async () => {
                    const savedComponentType = component.componentType;
                    await payrollService.upsertComponent(component);
                    if (savedComponentType === 'Deduction' && rules) {
                      for (const rule of rules) {
                        if (!rule.category || !component.code) {
                          continue;
                        }

                        await payrollService.upsertComponentRule({
                          ...defaultComponentRule,
                          ...rule,
                          componentType: 'Deduction',
                          componentCode: component.code,
                          legacyCompanyCode: rule.legacyCompanyCode || '001',
                        });
                      }
                    }
                    setComponent({
                      ...defaultComponent,
                      componentType: savedComponentType,
                      taxable: savedComponentType === 'Allowance' || savedComponentType === 'Benefit',
                      includeInGross: savedComponentType !== 'Deduction',
                      currencyCode: component.currencyCode || defaultComponent.currencyCode,
                    });
                    setSetup(await payrollService.getSetupSummary());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000118' ? (
            <BonusSetupForm
              bonuses={setup?.bonusPolicies ?? []}
              bonusExceptions={setup?.bonusExceptions ?? []}
              bonusTypes={bonusCodeValues}
              staffCategories={staffCategoryCodeValues}
              employees={payrollEmployees}
              bonus={bonusPolicy}
              busy={busy}
              onChange={setBonusPolicy}
              onSave={(exceptionRows) =>
                save(
                  'Bonus setup',
                  async () => {
                    const savedBonus = await payrollService.upsertBonusPolicy(bonusPolicy);
                    await payrollService.saveBonusExceptions({
                      bonusCode: savedBonus.code,
                      legacyCompanyCode: setup?.activeParameters?.legacyCompanyCode ?? null,
                      entries: exceptionRows.map((row) => ({
                        id: row.id,
                        employeeProfileId: row.employeeProfileId,
                        employeeNumber: row.employeeNumber,
                        calculationType: row.calculationType,
                        amount: row.amount,
                        taxable: row.taxable,
                        applicable: row.applicable,
                        currencyCode: row.currencyCode,
                        legacyCompanyCode: row.legacyCompanyCode || setup?.activeParameters?.legacyCompanyCode || '001',
                        isSelected: row.isSelected !== false,
                      })),
                    });
                    setBonusPolicy(defaultBonusPolicy);
                    setSetup(await payrollService.getSetupSummary());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000119' ? (
            <BackpaySalaryIncreaseForm
              policies={setup?.backpayPolicies ?? []}
              rules={setup?.backpayRules ?? []}
              exceptions={setup?.backpayExceptions ?? []}
              staffCategories={staffCategoryCodeValues}
              departments={departmentCodeValues}
              positions={positionCodeValues}
              grades={gradeSetup.grades}
              employees={payrollEmployees}
              policy={backpayPolicy}
              busy={busy}
              onChange={setBackpayPolicy}
              onSave={(policy, rules, exceptions) =>
                save(
                  'Backpay / salary increase',
                  async () => {
                    const savedPolicy = await payrollService.upsertBackpayPolicy(policy);
                    if (policy.categoryType !== 'All Staff') {
                      for (const rule of rules) {
                        if (!rule.categoryCode) {
                          continue;
                        }

                        await payrollService.upsertBackpayRule({
                          ...defaultBackpayRule,
                          ...rule,
                          operationType: policy.operationType,
                          categoryType: policy.categoryType,
                          legacyCompanyCode: rule.legacyCompanyCode || '001',
                        });
                      }
                    }
                    if (policy.operationType === 'SalaryArrears') {
                      for (const exception of exceptions) {
                        if (!exception.employeeNumber || !exception.employeeName) {
                          continue;
                        }

                        await payrollService.upsertBackpayException({
                          ...defaultBackpayException,
                          ...exception,
                          operationType: 'SalaryArrears',
                          legacyCompanyCode: exception.legacyCompanyCode || '001',
                        });
                      }
                    }
                    setBackpayPolicy({ ...savedPolicy, effectiveDate: nullableDateValue(savedPolicy.effectiveDate), minimumServiceDate: nullableDateValue(savedPolicy.minimumServiceDate) });
                    setSetup(await payrollService.getSetupSummary());
                  },
                  async () => undefined,
                )
              }
            />
          ) : selectedMenuId === 'A0000131' ? (
            <BudgetAnalysisForm
              defaultPayPeriod={parameters.currentPayPeriod || currentPayPeriod}
              defaultPayPeriodFrom={parameters.currentPeriodFrom}
              defaultPayPeriodTo={parameters.currentPeriodTo}
              companyCode={parameters.legacyCompanyCode || '001'}
            />
          ) : (
            <MenuTracker selectedMenu={selectedMenu} />
          )}
        </section>
      </div>
    </main>
  );
}

function CodesDescriptionForm({
  codeSetup,
  codeType,
  codeValue,
  busy,
  onCodeTypeChange,
  onCodeValueChange,
  onSelectCodeType,
  onSaveCodeType,
  onSaveCodeValue,
}: {
  codeSetup: PayrollCodeSetup;
  codeType: PayrollCodeType;
  codeValue: PayrollCodeValue;
  busy: string | null;
  onCodeTypeChange: (value: PayrollCodeType) => void;
  onCodeValueChange: (value: PayrollCodeValue) => void;
  onSelectCodeType: (value: string) => Promise<void>;
  onSaveCodeType: () => void;
  onSaveCodeValue: () => void;
}) {
  const compactHeadClassName = 'h-8 px-2 py-1 text-xs';
  const compactCellClassName = 'px-2 py-1 text-xs';

  const selectCodeTypeRow = (item: PayrollCodeType) => {
    onCodeTypeChange({ ...defaultCodeType, ...item });
    onCodeValueChange({
      ...defaultCodeValue,
      codeType: item.codeType,
      payrollCodeTypeId: item.id,
      legacyCompanyCode: item.legacyCompanyCode || '001',
    });
    void onSelectCodeType(item.codeType);
  };

  const selectCodeValueRow = (item: PayrollCodeValue) => {
    const parent = codeSetup.codeTypes.find((codeTypeItem) => codeTypeItem.codeType === item.codeType);
    onCodeValueChange({
      ...defaultCodeValue,
      ...item,
      payrollCodeTypeId: item.payrollCodeTypeId || parent?.id,
      legacyCompanyCode: item.legacyCompanyCode || parent?.legacyCompanyCode || '001',
    });
  };

  return (
    <Tabs defaultValue="code-types" className="space-y-4">
      <TabsList className="grid w-full max-w-md grid-cols-2">
        <TabsTrigger value="code-types">Code Types</TabsTrigger>
        <TabsTrigger value="actual-codes">Actual Codes</TabsTrigger>
      </TabsList>

      <TabsContent value="code-types" className="space-y-4">
        <Card>
          <CardHeader className="pb-3">
            <PayrollFormTitle menuId="A0000102">Code Type Entry</PayrollFormTitle>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-3 lg:grid-cols-4"
              onSubmit={(event: FormEvent) => {
                event.preventDefault();
                onSaveCodeType();
              }}
            >
              <Field label="Code Type">
                <Input maxLength={5} value={codeType.codeType} onChange={(event) => onCodeTypeChange({ ...codeType, codeType: event.target.value.toUpperCase() })} />
              </Field>
              <Field label="Code Description">
                <Input value={codeType.description} onChange={(event) => onCodeTypeChange({ ...codeType, description: event.target.value })} />
              </Field>
              <Field label="Account Code">
                <Input value={codeType.accountCode ?? ''} onChange={(event) => onCodeTypeChange({ ...codeType, accountCode: event.target.value })} />
              </Field>
              <div className="flex items-end gap-3 lg:col-span-4">
                <BooleanField checked={!codeType.blocked} label="Active" onChange={(checked) => onCodeTypeChange({ ...codeType, blocked: !checked })} />
                <Button type="submit" disabled={busy === 'Code type'}>
                  {busy === 'Code type' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save Code Type
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Code Types</CardTitle>
          </CardHeader>
          <CardContent>
            <Table className="text-xs">
              <TableHeader>
                <TableRow>
                  <TableHead className={compactHeadClassName}>Type</TableHead>
                  <TableHead className={compactHeadClassName}>Description</TableHead>
                  <TableHead className={compactHeadClassName}>Account</TableHead>
                  <TableHead className={compactHeadClassName}>Values</TableHead>
                  <TableHead className={compactHeadClassName}>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {codeSetup.codeTypes.map((item) => (
                  <TableRow
                    key={item.id || item.codeType}
                    className={`cursor-pointer hover:bg-muted/60 ${codeType.id === item.id || (!codeType.id && codeType.codeType === item.codeType) ? 'bg-muted' : ''}`}
                    onClick={() => selectCodeTypeRow(item)}
                  >
                    <TableCell className={`${compactCellClassName} font-medium`}>{item.codeType}</TableCell>
                    <TableCell className={compactCellClassName}>{item.description}</TableCell>
                    <TableCell className={compactCellClassName}>{item.accountCode || '-'}</TableCell>
                    <TableCell className={compactCellClassName}>{item.valueCount ?? 0}</TableCell>
                    <TableCell className={compactCellClassName}>
                      <Badge variant="outline" className={`h-5 px-1.5 text-[11px] ${item.blocked ? 'text-muted-foreground' : 'border-emerald-200 bg-emerald-50 text-emerald-700'}`}>
                        {item.blocked ? 'Inactive' : 'Active'}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </TabsContent>

      <TabsContent value="actual-codes" className="space-y-4">
        <Card>
          <CardHeader className="pb-3">
            <PayrollFormTitle menuId="A0000102">Actual Code Entry</PayrollFormTitle>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-3 lg:grid-cols-4"
              onSubmit={(event) => {
                event.preventDefault();
                onSaveCodeValue();
              }}
            >
              <Field label="Currently Processing">
                <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={codeValue.codeType} onChange={(event) => void onSelectCodeType(event.target.value)}>
                  <option value="">Select code type</option>
                  {codeSetup.codeTypes.map((item) => (
                    <option key={item.id || item.codeType} value={item.codeType}>
                      {item.codeType} - {item.description}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Actual Code">
                <Input maxLength={15} value={codeValue.actualCode} onChange={(event) => onCodeValueChange({ ...codeValue, actualCode: event.target.value.toUpperCase() })} />
              </Field>
              <Field label="Code Description">
                <Input value={codeValue.description} onChange={(event) => onCodeValueChange({ ...codeValue, description: event.target.value })} />
              </Field>
              <Field label="Additional Description">
                <Input value={codeValue.additionalDescription ?? ''} onChange={(event) => onCodeValueChange({ ...codeValue, additionalDescription: event.target.value })} />
              </Field>
              <Field label="Account Code">
                <Input value={codeValue.accountCode ?? ''} onChange={(event) => onCodeValueChange({ ...codeValue, accountCode: event.target.value })} />
              </Field>
              <div className="flex items-end gap-3 lg:col-span-3">
                <BooleanField checked={!codeValue.blocked} label="Active" onChange={(checked) => onCodeValueChange({ ...codeValue, blocked: !checked })} />
                <Button type="submit" disabled={busy === 'Actual code' || !codeValue.codeType}>
                  {busy === 'Actual code' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save Actual Code
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Actual Codes</CardTitle>
          </CardHeader>
          <CardContent>
            <Table className="text-xs">
              <TableHeader>
                <TableRow>
                  <TableHead className={compactHeadClassName}>Actual Code</TableHead>
                  <TableHead className={compactHeadClassName}>Description</TableHead>
                  <TableHead className={compactHeadClassName}>Additional</TableHead>
                  <TableHead className={compactHeadClassName}>Account</TableHead>
                  <TableHead className={compactHeadClassName}>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {codeSetup.codeValues.map((item) => (
                  <TableRow
                    key={item.id || `${item.codeType}-${item.actualCode}`}
                    className={`cursor-pointer hover:bg-muted/60 ${codeValue.id === item.id || (!codeValue.id && codeValue.actualCode === item.actualCode) ? 'bg-muted' : ''}`}
                    onClick={() => selectCodeValueRow(item)}
                  >
                    <TableCell className={`${compactCellClassName} font-medium`}>{item.actualCode}</TableCell>
                    <TableCell className={compactCellClassName}>{item.description}</TableCell>
                    <TableCell className={compactCellClassName}>{item.additionalDescription || '-'}</TableCell>
                    <TableCell className={compactCellClassName}>{item.accountCode || '-'}</TableCell>
                    <TableCell className={compactCellClassName}>
                      <Badge variant="outline" className={`h-5 px-1.5 text-[11px] ${item.blocked ? 'text-muted-foreground' : 'border-emerald-200 bg-emerald-50 text-emerald-700'}`}>
                        {item.blocked ? 'Inactive' : 'Active'}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </TabsContent>
    </Tabs>
  );
}

function ParametersForm({
  parameters,
  currencies,
  busy,
  onChange,
  onSave,
}: {
  parameters: PayrollParameterSet;
  currencies: CurrencyListDto[];
  busy: string | null;
  onChange: <K extends keyof PayrollParameterSet>(key: K, value: PayrollParameterSet[K]) => void;
  onSave: () => void;
}) {
  const currencyOptions = currencies.some((currency) => currency.code === parameters.baseCurrency)
    ? currencies
    : [
        ...currencies,
        {
          id: parameters.baseCurrency,
          code: parameters.baseCurrency,
          name: parameters.baseCurrency,
          symbol: '',
          decimalPlaces: 2,
          exchangeRate: 1,
          isBaseCurrency: false,
          isActive: true,
          displayOrder: 0,
        },
      ].filter((currency) => currency.code);
  const inputClassName = 'h-8 w-full text-sm';
  const compactSelectClassName = 'h-8 w-full rounded-md border bg-background px-2 text-sm';

  return (
    <Card>
      <CardHeader className="pb-3">
        <PayrollFormTitle menuId="A0000103">Parameters</PayrollFormTitle>
      </CardHeader>
      <CardContent>
        <form
          className="space-y-3"
          onSubmit={(event: FormEvent) => {
            event.preventDefault();
            onSave();
          }}
        >
          <div className="grid gap-x-5 gap-y-2 lg:grid-cols-2 2xl:grid-cols-3">
            <CompactParameterField label="Base Currency" wide>
              <select className={compactSelectClassName} value={parameters.baseCurrency} onChange={(event) => onChange('baseCurrency', event.target.value)}>
              {currencyOptions.map((currency) => (
                <option key={currency.id || currency.code} value={currency.code}>
                  {currency.code} {currency.name ? `- ${currency.name}` : ''}{currency.isBaseCurrency ? ' (Base)' : ''}
                </option>
              ))}
              </select>
            </CompactParameterField>

            <CompactParameterField label="Allow Currency Multiple?">
              <div className="flex h-8 items-center">
                <Checkbox checked={parameters.multiCurrencyEnabled} onCheckedChange={(value) => onChange('multiCurrencyEnabled', value === true)} />
              </div>
            </CompactParameterField>

            <CompactParameterField label="Pay Period">
              <Input className={inputClassName} type="number" value={parameters.currentPayPeriod} onChange={(event) => onChange('currentPayPeriod', Number(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Payment Frequency">
              <Input className={inputClassName} type="number" value={parameters.payFrequencyMonths} onChange={(event) => onChange('payFrequencyMonths', Number(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Frequency Mode">
              <select className={compactSelectClassName} value={parameters.payMode ?? 'M'} onChange={(event) => onChange('payMode', event.target.value)}>
                <option value="M">Month</option>
                <option value="D">Days</option>
              </select>
            </CompactParameterField>

            <CompactParameterField label="Pay Period From">
              <Input className={inputClassName} type="date" value={dateValue(parameters.currentPeriodFrom)} onChange={(event) => onChange('currentPeriodFrom', event.target.value || null)} />
            </CompactParameterField>

            <CompactParameterField label="Pay Period To">
              <Input className={inputClassName} type="date" value={dateValue(parameters.currentPeriodTo)} onChange={(event) => onChange('currentPeriodTo', event.target.value || null)} />
            </CompactParameterField>

            <CompactParameterField label="Days in Month">
              <Input className={inputClassName} type="number" value={parameters.monthDays} onChange={(event) => onChange('monthDays', Number(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Date Format">
              <select className={compactSelectClassName} value={parameters.dateFormat} onChange={(event) => onChange('dateFormat', event.target.value)}>
                <option value="MM-DD-YY">MM-DD-YY</option>
                <option value="MM-DD-YYYY">MM-DD-YYYY</option>
                <option value="DD-MM-YY">DD-MM-YY</option>
                <option value="DD-MM-YYYY">DD-MM-YYYY</option>
                <option value="DD-MON-YY">DD-MON-YY</option>
                <option value="DD-MON-YYYY">DD-MON-YYYY</option>
              </select>
            </CompactParameterField>

            <CompactParameterField label="Tax Bonus Separately">
              <div className="flex h-8 items-center">
                <Checkbox checked={parameters.separateBonusTax} onCheckedChange={(value) => onChange('separateBonusTax', value === true)} />
              </div>
            </CompactParameterField>

            <CompactParameterField label="Time Sheet">
              <select className={compactSelectClassName} value={parameters.timeSheetMode ?? 'D'} onChange={(event) => onChange('timeSheetMode', event.target.value)}>
                <option value="D">Detail</option>
                <option value="S">Summary</option>
              </select>
            </CompactParameterField>

            <CompactParameterField label="Leave Classification">
              <select className={compactSelectClassName} value={parameters.leaveClassification ?? 'CAT'} onChange={(event) => onChange('leaveClassification', event.target.value)}>
                <option value="CAT">Staff Categories</option>
                <option value="DEP">Departments</option>
                <option value="JOB">Jobs</option>
                <option value="POS">Positions</option>
                <option value="RAN">Ranks</option>
                <option value="ALL">All</option>
              </select>
            </CompactParameterField>

            <CompactParameterField label="Employer SSF">
              <Input className={inputClassName} type="number" step="0.01" value={parameters.employerSsfRate} onChange={(event) => onChange('employerSsfRate', Number(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Employee SSF">
              <Input className={inputClassName} type="number" step="0.01" value={parameters.employeeSsfRate} onChange={(event) => onChange('employeeSsfRate', Number(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Picture Directory" wide>
              <Input className={inputClassName} value={parameters.pictureDirectory ?? ''} onChange={(event) => onChange('pictureDirectory', event.target.value)} />
            </CompactParameterField>

            <CompactParameterField label="Multiple Payments?">
              <div className="flex h-8 items-center">
                <Checkbox checked={parameters.allowEmployeePaymentMethod} onCheckedChange={(value) => onChange('allowEmployeePaymentMethod', value === true)} />
              </div>
            </CompactParameterField>

            <CompactParameterField label="Default Payment">
              <select className={compactSelectClassName} value={parameters.defaultEmployeePaymentMethod ?? 'Bank'} onChange={(event) => onChange('defaultEmployeePaymentMethod', event.target.value)}>
                <option value="Bank">Bank</option>
                <option value="Cash">Cash</option>
                <option value="Cheque">Cheque</option>
              </select>
            </CompactParameterField>

            <CompactParameterField label="Multiple Pay Basis?">
              <div className="flex h-8 items-center">
                <Checkbox checked={parameters.multiplePayBasisEnabled} onCheckedChange={(value) => onChange('multiplePayBasisEnabled', value === true)} />
              </div>
            </CompactParameterField>

            <CompactParameterField label="Default Pay Basis">
              <select className={compactSelectClassName} value={parameters.defaultPayBasis ?? 'COMBINED'} onChange={(event) => onChange('defaultPayBasis', event.target.value)}>
                <option value="GRADED">Graded</option>
                <option value="NEGOTIATED">Negotiated</option>
                <option value="COMBINED">Combined</option>
              </select>
            </CompactParameterField>

            <CompactParameterField label="Male Retire Age">
              <Input className={inputClassName} type="number" value={parameters.maleRetireAge ?? ''} onChange={(event) => onChange('maleRetireAge', numberOrNull(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Female Retire Age">
              <Input className={inputClassName} type="number" value={parameters.femaleRetireAge ?? ''} onChange={(event) => onChange('femaleRetireAge', numberOrNull(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Min Hire Date">
              <Input className={inputClassName} type="number" value={parameters.minimumHireAge ?? ''} onChange={(event) => onChange('minimumHireAge', numberOrNull(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Allowance Ceiling">
              <Input className={inputClassName} type="number" step="0.01" value={parameters.totalAllowanceTaxCeiling ?? ''} onChange={(event) => onChange('totalAllowanceTaxCeiling', numberOrNull(event.target.value))} />
            </CompactParameterField>

            <CompactParameterField label="Debt Service Ratio">
              <Input className={inputClassName} type="number" step="0.01" value={parameters.debitRatio ?? ''} onChange={(event) => onChange('debitRatio', numberOrNull(event.target.value))} />
            </CompactParameterField>
          </div>

          <div className="flex justify-end border-t pt-3">
            <Button type="submit" disabled={busy === 'Parameters'}>
              {busy === 'Parameters' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save Parameters
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function HolidayControlsForm({
  setup,
  holiday,
  nonWorkingDay,
  busy,
  onHolidayChange,
  onNonWorkingDayChange,
  onSaveHoliday,
  onSaveNonWorkingDay,
}: {
  setup: PayrollHolidaySetup;
  holiday: PayrollHoliday;
  nonWorkingDay: PayrollNonWorkingDay;
  busy: string | null;
  onHolidayChange: (value: PayrollHoliday) => void;
  onNonWorkingDayChange: (value: PayrollNonWorkingDay) => void;
  onSaveHoliday: () => void;
  onSaveNonWorkingDay: () => void;
}) {
  return (
    <Tabs defaultValue="holidays" className="space-y-4">
      <TabsList className="grid w-full max-w-md grid-cols-2">
        <TabsTrigger value="holidays">Holidays</TabsTrigger>
        <TabsTrigger value="non-working-days">Non-working Days</TabsTrigger>
      </TabsList>

      <TabsContent value="holidays" className="space-y-4">
        <Card>
          <CardHeader className="pb-3">
            <PayrollFormTitle menuId="A0000104">Holiday Entry</PayrollFormTitle>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-3 lg:grid-cols-4"
              onSubmit={(event) => {
                event.preventDefault();
                onSaveHoliday();
              }}
            >
              <Field label="Holiday Date">
                <Input type="date" value={dateValue(holiday.holidayDate)} onChange={(event) => onHolidayChange({ ...holiday, holidayDate: event.target.value })} />
              </Field>
              <Field label="Description">
                <Input value={holiday.description} onChange={(event) => onHolidayChange({ ...holiday, description: event.target.value })} />
              </Field>
              <div className="flex items-end">
                <Button type="submit" disabled={busy === 'Holiday'}>
                  {busy === 'Holiday' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save Holiday
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Holidays</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Description</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {setup.holidays.map((item) => (
                  <TableRow
                    key={item.id || `${item.holidayDate}-${item.legacyCompanyCode}`}
                    className={`cursor-pointer hover:bg-muted/60 ${holiday.id === item.id || (!holiday.id && holiday.holidayDate === item.holidayDate) ? 'bg-muted' : ''}`}
                    onClick={() =>
                      onHolidayChange({
                        ...defaultHoliday,
                        ...item,
                        holidayDate: dateValue(item.holidayDate),
                        legacyCompanyCode: item.legacyCompanyCode || '001',
                      })
                    }
                  >
                    <TableCell>{dateValue(item.holidayDate)}</TableCell>
                    <TableCell>{item.description}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </TabsContent>

      <TabsContent value="non-working-days" className="space-y-4">
        <Card>
          <CardHeader className="pb-3">
            <PayrollFormTitle menuId="A0000104">Non-working Day Entry</PayrollFormTitle>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-3 lg:grid-cols-5"
              onSubmit={(event) => {
                event.preventDefault();
                onSaveNonWorkingDay();
              }}
            >
              <Field label="Day Code">
                <Input maxLength={1} value={nonWorkingDay.dayCode} onChange={(event) => onNonWorkingDayChange({ ...nonWorkingDay, dayCode: event.target.value.toUpperCase() })} />
              </Field>
              <Field label="Description">
                <Input value={nonWorkingDay.description} onChange={(event) => onNonWorkingDayChange({ ...nonWorkingDay, description: event.target.value })} />
              </Field>
              <Field label="Overtime Rate">
                <Input type="number" step="0.01" value={nonWorkingDay.overtimeRate ?? ''} onChange={(event) => onNonWorkingDayChange({ ...nonWorkingDay, overtimeRate: numberOrNull(event.target.value) })} />
              </Field>
              <div className="flex items-end">
                <Button type="submit" disabled={busy === 'Non-working day'}>
                  {busy === 'Non-working day' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save Non-working Day
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Non-working Days</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Day</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Overtime Rate</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {setup.nonWorkingDays.map((item) => (
                  <TableRow
                    key={item.id || `${item.dayCode}-${item.legacyCompanyCode}`}
                    className={`cursor-pointer hover:bg-muted/60 ${nonWorkingDay.id === item.id || (!nonWorkingDay.id && nonWorkingDay.dayCode === item.dayCode) ? 'bg-muted' : ''}`}
                    onClick={() =>
                      onNonWorkingDayChange({
                        ...defaultNonWorkingDay,
                        ...item,
                        legacyCompanyCode: item.legacyCompanyCode || '001',
                      })
                    }
                  >
                    <TableCell className="font-medium">{item.dayCode}</TableCell>
                    <TableCell>{item.description}</TableCell>
                    <TableCell>{item.overtimeRate ?? '-'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </TabsContent>
    </Tabs>
  );
}

function BankBranchForm({
  banks,
  branches,
  branch,
  busy,
  onBranchChange,
  onSaveBranch,
}: {
  banks: PayrollBankMaster[];
  branches: PayrollBankBranch[];
  branch: PayrollBankBranch;
  busy: string | null;
  onBranchChange: (value: PayrollBankBranch) => void;
  onSaveBranch: () => void;
}) {
  const branchRows = branches.filter((item) => item.branchCode !== bankMasterBranchCode);

  return (
    <div className="space-y-4">
        <Card>
          <CardHeader className="pb-3">
            <PayrollFormTitle menuId="A0000106">Branch Setup</PayrollFormTitle>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-3 lg:grid-cols-3"
              onSubmit={(event) => {
                event.preventDefault();
                onSaveBranch();
              }}
            >
              <Field label="Bank">
                <select
                  className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                  value={branch.bankCode}
                  onChange={(event) => {
                    const selected = banks.find((item) => item.bankCode === event.target.value);
                    onBranchChange({
                      ...branch,
                      branchSetupCode: selected?.branchSetupCode || branch.branchSetupCode,
                      bankCode: event.target.value,
                      legacyCompanyCode: selected?.legacyCompanyCode || branch.legacyCompanyCode || '001',
                    });
                  }}
                >
                  <option value="">Select bank</option>
                  {banks.map((item) => (
                    <option key={item.id || item.bankCode} value={item.bankCode}>
                      {item.bankCode} - {item.bankName}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Setup Code">
                <Input maxLength={5} value={branch.branchSetupCode ?? ''} onChange={(event) => onBranchChange({ ...branch, branchSetupCode: event.target.value.toUpperCase() })} />
              </Field>
              <Field label="Branch Code">
                <Input maxLength={8} value={branch.branchCode} onChange={(event) => onBranchChange({ ...branch, branchCode: event.target.value.toUpperCase() })} />
              </Field>
              <Field label="Branch Description">
                <Input value={branch.branchDescription} onChange={(event) => onBranchChange({ ...branch, branchDescription: event.target.value })} />
              </Field>
              <Field label="Region">
                <Input value={branch.region ?? ''} onChange={(event) => onBranchChange({ ...branch, region: event.target.value })} />
              </Field>
              <Field label="Sort Code">
                <Input value={branch.sortCode ?? ''} onChange={(event) => onBranchChange({ ...branch, sortCode: event.target.value })} />
              </Field>
              <Field label="Account Code">
                <Input value={branch.accountCode ?? ''} onChange={(event) => onBranchChange({ ...branch, accountCode: event.target.value })} />
              </Field>
              <div className="flex items-end lg:col-span-3">
                <Button type="submit" disabled={busy === 'Bank branch' || !branch.bankCode || !branch.branchCode || !branch.branchDescription}>
                  {busy === 'Bank branch' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save Branch
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Bank Branches</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Bank</TableHead>
                  <TableHead>Setup Code</TableHead>
                  <TableHead>Branch</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Region</TableHead>
                  <TableHead>Sort</TableHead>
                  <TableHead>Account</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {branchRows.map((item) => (
                  <TableRow key={item.id || `${item.bankCode}-${item.branchCode}-${item.legacyCompanyCode}`} className="cursor-pointer" onClick={() => onBranchChange(item)}>
                    <TableCell className="font-medium">{item.bankCode}</TableCell>
                    <TableCell>{item.branchSetupCode || '-'}</TableCell>
                    <TableCell>{item.branchCode}</TableCell>
                    <TableCell>{item.branchDescription}</TableCell>
                    <TableCell>{item.region || '-'}</TableCell>
                    <TableCell>{item.sortCode || '-'}</TableCell>
                    <TableCell>{item.accountCode || '-'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
    </div>
  );
}

function LoanSetupForm({
  loans,
  loanTypes,
  loan,
  busy,
  onChange,
  onSave,
}: {
  loans: PayrollLoanPolicy[];
  loanTypes: PayrollCodeValue[];
  loan: PayrollLoanPolicy;
  busy: string | null;
  onChange: (value: PayrollLoanPolicy) => void;
  onSave: () => void;
}) {
  const loanTypeOptions: PayrollCodeValue[] =
    loanTypes.length > 0
      ? loanTypes
      : loans.map((item) => ({
          id: item.id,
          codeType: 'LOAN',
          actualCode: item.code,
          description: item.name,
          blocked: !item.isActive,
          accountCode: null,
          additionalDescription: null,
          dependentActualCode: null,
          dependentCodeType: null,
          legacyCompanyCode: item.legacyCompanyCode || '001',
        }));
  const selectedInterestType = loan.interestType === 'Flat' ? 'Flat Rate' : loan.interestType || 'Flat Rate';

  return (
    <div className="grid gap-4 2xl:grid-cols-[minmax(0,520px)_1fr]">
      <Card>
        <CardHeader className="pb-3">
          <PayrollFormTitle menuId="A0000107">Loan Setup</PayrollFormTitle>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              onSave();
            }}
          >
            <LoanSetupField label="Loan Type">
              <select
                className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                value={loan.code}
                onChange={(event) => {
                  const selected = loanTypeOptions.find((item) => item.actualCode === event.target.value);
                  onChange({
                    ...loan,
                    code: event.target.value,
                    name: selected?.description || loan.name,
                    legacyCompanyCode: selected?.legacyCompanyCode || loan.legacyCompanyCode || '001',
                  });
                }}
              >
                <option value="">Select loan type</option>
                {loanTypeOptions.map((item) => (
                  <option key={item.id || `${item.codeType}-${item.actualCode}`} value={item.actualCode}>
                    {item.actualCode} - {item.description}
                  </option>
                ))}
              </select>
              {loanTypes.length === 0 && loans.length === 0 ? (
                <p className="text-xs text-muted-foreground">No LOA code values found in Codes Description.</p>
              ) : null}
            </LoanSetupField>
            <LoanSetupField label="Description">
              <Input className="h-9" value={loan.name} onChange={(event) => onChange({ ...loan, name: event.target.value })} />
            </LoanSetupField>
            <div className="grid gap-3">
              <LoanSetupField label="Max Loan Amount (Months)">
                <Input type="number" step="0.01" value={loan.maxLoanAmount ?? ''} onChange={(event) => onChange({ ...loan, maxLoanAmount: numberOrNull(event.target.value) })} />
              </LoanSetupField>
              <LoanSetupField label="Interest %">
                <Input type="number" step="0.01" value={loan.interestRatePercent ?? ''} onChange={(event) => onChange({ ...loan, interestRatePercent: numberOrNull(event.target.value) })} />
              </LoanSetupField>
            </div>
            <LoanSetupField label="Interest Type">
              <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={selectedInterestType} onChange={(event) => onChange({ ...loan, interestType: event.target.value })}>
                <option value="Reducing Balance">Reducing Balance</option>
                <option value="Flat Rate">Flat Rate</option>
                <option value="Simple Interest">Simple Interest</option>
              </select>
            </LoanSetupField>
            <div className="grid gap-2 md:grid-cols-2">
              <BooleanField checked={loan.applyInterest} label="Apply Interest" onChange={(checked) => onChange({ ...loan, applyInterest: checked })} />
              <BooleanField checked={loan.isActive} label="Active" onChange={(checked) => onChange({ ...loan, isActive: checked })} />
            </div>
            <Button type="submit" disabled={busy === 'Loan setup'}>
              {busy === 'Loan setup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save Loan Setup
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Loan Types</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Type</TableHead>
                <TableHead>Description</TableHead>
                <TableHead>Max Loan Amount (Months)</TableHead>
                <TableHead>Interest</TableHead>
                <TableHead>Interest Type</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loans.map((item) => (
                <TableRow key={item.id || `${item.code}-${item.legacyCompanyCode}`} className="cursor-pointer hover:bg-muted/60" onClick={() => onChange({ ...defaultLoanPolicy, ...item, interestType: item.interestType === 'Flat' ? 'Flat Rate' : item.interestType || 'Flat Rate' })}>
                  <TableCell className="font-medium">{item.code}</TableCell>
                  <TableCell>{item.name}</TableCell>
                  <TableCell>{item.maxLoanAmount ?? '-'}</TableCell>
                  <TableCell>{item.applyInterest ? item.interestRatePercent ?? 0 : 'N'}</TableCell>
                  <TableCell>{item.interestType === 'Flat' ? 'Flat Rate' : item.interestType || '-'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

    </div>
  );
}

function LoanSetupField({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="grid gap-2 sm:grid-cols-[180px_minmax(0,1fr)] sm:items-center">
      <Label className="text-sm font-medium text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

function LeaveSetupForm({
  setup,
  leaveHeader,
  leaveDetail,
  busy,
  onHeaderChange,
  onDetailChange,
  onSaveHeader,
  onSaveDetail,
}: {
  setup: PayrollLeaveSetupCollection;
  leaveHeader: PayrollLeaveSetup;
  leaveDetail: PayrollLeaveSetupDetail;
  busy: string | null;
  onHeaderChange: (value: PayrollLeaveSetup) => void;
  onDetailChange: (value: PayrollLeaveSetupDetail) => void;
  onSaveHeader: () => void;
  onSaveDetail: () => void;
}) {
  return (
    <div className="grid gap-4 2xl:grid-cols-[minmax(0,420px)_1fr]">
      <Card>
        <CardHeader className="pb-3">
          <PayrollFormTitle menuId="A0000108">Leave Setup</PayrollFormTitle>
        </CardHeader>
        <CardContent className="space-y-5">
          <form
            className="grid gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              onSaveHeader();
            }}
          >
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Category">
                <Input maxLength={5} value={leaveHeader.category} onChange={(event) => onHeaderChange({ ...leaveHeader, category: event.target.value.toUpperCase() })} />
              </Field>
              <Field label="Category Detail">
                <Input maxLength={5} value={leaveHeader.categoryDetail} onChange={(event) => onHeaderChange({ ...leaveHeader, categoryDetail: event.target.value.toUpperCase() })} />
              </Field>
            </div>
            <Button type="submit" disabled={busy === 'Leave setup'}>
              {busy === 'Leave setup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save Leave Setup
            </Button>
          </form>

          <form
            className="grid gap-3 border-t pt-5"
            onSubmit={(event) => {
              event.preventDefault();
              onSaveDetail();
            }}
          >
            <Field label="Setup Row">
              <select
                className="h-9 rounded-md border bg-background px-3 text-sm"
                value={leaveDetail.payrollLeaveSetupId ?? ''}
                onChange={(event) => {
                  const selected = setup.leaveSetups.find((item) => item.id === event.target.value);
                  onDetailChange({
                    ...leaveDetail,
                    payrollLeaveSetupId: event.target.value,
                    category: selected?.category || '',
                    categoryDetail: selected?.categoryDetail || '',
                    legacyCompanyCode: selected?.legacyCompanyCode || '001',
                  });
                }}
              >
                <option value="">Select leave setup</option>
                {setup.leaveSetups.map((item) => (
                  <option key={item.id || `${item.category}-${item.categoryDetail}`} value={item.id}>
                    {item.category}/{item.categoryDetail}
                  </option>
                ))}
              </select>
            </Field>
            <div className="grid gap-3 md:grid-cols-4">
              <Field label="Days">
                <Input type="number" value={leaveDetail.days ?? ''} onChange={(event) => onDetailChange({ ...leaveDetail, days: numberOrNull(event.target.value) })} />
              </Field>
              <Field label="Service From">
                <Input type="number" value={leaveDetail.serviceFrom ?? ''} onChange={(event) => onDetailChange({ ...leaveDetail, serviceFrom: numberOrNull(event.target.value) })} />
              </Field>
              <Field label="Service To">
                <Input type="number" value={leaveDetail.serviceTo ?? ''} onChange={(event) => onDetailChange({ ...leaveDetail, serviceTo: numberOrNull(event.target.value) })} />
              </Field>
              <Field label="No.">
                <Input type="number" value={leaveDetail.sequenceNo ?? ''} onChange={(event) => onDetailChange({ ...leaveDetail, sequenceNo: numberOrNull(event.target.value) })} />
              </Field>
            </div>
            <Button type="submit" disabled={busy === 'Leave detail' || !leaveDetail.category}>
              {busy === 'Leave detail' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save Leave Detail
            </Button>
          </form>
        </CardContent>
      </Card>

      <div className="space-y-4">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Leave Setup Rows</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Category</TableHead>
                  <TableHead>Detail</TableHead>
                  <TableHead>Rules</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {setup.leaveSetups.map((item) => (
                  <TableRow key={item.id || `${item.category}-${item.categoryDetail}`}>
                    <TableCell className="font-medium">{item.category}</TableCell>
                    <TableCell>{item.categoryDetail}</TableCell>
                    <TableCell>{item.detailCount ?? item.details?.length ?? 0}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Leave Detail Rows</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Category</TableHead>
                  <TableHead>Days</TableHead>
                  <TableHead>Service From</TableHead>
                  <TableHead>Service To</TableHead>
                  <TableHead>No.</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {setup.leaveSetupDetails.map((item) => (
                  <TableRow key={item.id || `${item.category}-${item.categoryDetail}-${item.sequenceNo}`}>
                    <TableCell className="font-medium">{item.category}/{item.categoryDetail}</TableCell>
                    <TableCell>{item.days ?? '-'}</TableCell>
                    <TableCell>{item.serviceFrom ?? '-'}</TableCell>
                    <TableCell>{item.serviceTo ?? '-'}</TableCell>
                    <TableCell>{item.sequenceNo ?? '-'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function OvertimeSetupForm({
  policy,
  busy,
  onPolicyChange,
  onSavePolicy,
}: {
  policy: PayrollOvertimePolicy;
  busy: string | null;
  onPolicyChange: (value: PayrollOvertimePolicy) => void;
  onSavePolicy: () => void;
}) {
  const updatePolicy = <K extends keyof PayrollOvertimePolicy>(key: K, value: PayrollOvertimePolicy[K]) => {
    onPolicyChange({ ...policy, [key]: value });
  };
  const inputClassName = 'h-8 rounded-sm';
  const selectClassName = 'h-8 rounded-sm border bg-background px-2 text-sm';

  return (
    <Card className="max-w-4xl">
      <CardHeader className="pb-2">
        <PayrollFormTitle menuId="A0000109">Overtime Setup</PayrollFormTitle>
      </CardHeader>
      <CardContent>
        <form
          className="space-y-5"
          onSubmit={(event) => {
            event.preventDefault();
            onSavePolicy();
          }}
        >
          <div className="grid gap-x-10 gap-y-3 lg:grid-cols-2">
            <div className="space-y-3">
              <OracleFormRow label="Overtime Period From">
                <div className="flex gap-2">
                  <Input className={inputClassName} type="date" value={dateValue(policy.payPeriodFrom)} onChange={(event) => updatePolicy('payPeriodFrom', event.target.value || null)} />
                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-sm border bg-background">
                    <CalendarDays className="h-4 w-4" />
                  </span>
                </div>
              </OracleFormRow>
              <OracleFormRow label="Normal Hour Rate">
                <Input className={inputClassName} type="number" step="0.01" value={policy.normalHours ?? ''} onChange={(event) => updatePolicy('normalHours', numberOrNull(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Week Day OT Rate">
                <Input className={inputClassName} type="number" step="0.01" value={policy.weekdayRate} onChange={(event) => updatePolicy('weekdayRate', Number(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Recall From Leave">
                <Input className={inputClassName} type="number" step="0.01" value={policy.leaveRecallRate ?? ''} onChange={(event) => updatePolicy('leaveRecallRate', numberOrNull(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Tax Overtime Separately?">
                <div className="flex h-8 items-center">
                  <Checkbox checked={policy.separateOvertimeTax} onCheckedChange={(value) => updatePolicy('separateOvertimeTax', value === true)} />
                </div>
              </OracleFormRow>
              <OracleFormRow label="Maximum Overtime">
                <div className="grid grid-cols-[110px_minmax(0,1fr)_auto] gap-2">
                  <select
                    className={selectClassName}
                    value={policy.maxOvertimeType ?? ''}
                    onChange={(event) =>
                      onPolicyChange({
                        ...policy,
                        maxOvertimeType: event.target.value,
                        maxOvertimeIsPercent: false,
                      })
                    }
                  >
                    <option value=""></option>
                    <option value="A">Amount</option>
                    <option value="H">Hours</option>
                  </select>
                  <Input
                    className={inputClassName}
                    type="number"
                    step="0.01"
                    value={policy.maxOvertimeAmount ?? ''}
                    onChange={(event) => updatePolicy('maxOvertimeAmount', numberOrNull(event.target.value))}
                    disabled={!policy.maxOvertimeType}
                  />
                  <label className="flex h-8 items-center gap-2 text-xs">
                    <Checkbox
                      checked={policy.maxOvertimeIsPercent}
                      disabled={policy.maxOvertimeType !== 'A'}
                      onCheckedChange={(value) => updatePolicy('maxOvertimeIsPercent', value === true)}
                    />
                    %
                  </label>
                </div>
              </OracleFormRow>
              <OracleFormRow label="Min Basic To Attract Normal Tax">
                <Input className={inputClassName} type="number" step="0.01" value={policy.minimumBasicForSeparateTax ?? ''} onChange={(event) => updatePolicy('minimumBasicForSeparateTax', numberOrNull(event.target.value))} />
              </OracleFormRow>
            </div>

            <div className="space-y-3">
              <OracleFormRow label="Overtime Period To">
                <div className="flex gap-2">
                  <Input className={inputClassName} type="date" value={dateValue(policy.payPeriodTo)} onChange={(event) => updatePolicy('payPeriodTo', event.target.value || null)} />
                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-sm border bg-background">
                    <CalendarDays className="h-4 w-4" />
                  </span>
                </div>
              </OracleFormRow>
              <OracleFormRow label="Normal Working Hrs">
                <Input className={inputClassName} type="number" step="0.01" value={policy.normalWorkingHours} onChange={(event) => updatePolicy('normalWorkingHours', Number(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Holiday OT Rate">
                <Input className={inputClassName} type="number" step="0.01" value={policy.holidayRate} onChange={(event) => updatePolicy('holidayRate', Number(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Tax Overtime?">
                <div className="flex h-8 items-center">
                  <Checkbox checked={policy.taxable} onCheckedChange={(value) => updatePolicy('taxable', value === true)} />
                </div>
              </OracleFormRow>
              <OracleFormRow label="Overtime Tax Ceiling">
                <Input className={inputClassName} type="number" step="0.01" value={policy.taxCeiling ?? ''} onChange={(event) => updatePolicy('taxCeiling', numberOrNull(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Max Separate Taxable OT Amt">
                <Input className={inputClassName} type="number" step="0.01" value={policy.maxOvertimeSeparateTax ?? ''} onChange={(event) => updatePolicy('maxOvertimeSeparateTax', numberOrNull(event.target.value))} />
              </OracleFormRow>
              <OracleFormRow label="Min Per To Qualify For Sep OT Tax">
                <Input className={inputClassName} type="number" step="0.01" value={policy.minimumSeparateOvertimePercent ?? ''} onChange={(event) => updatePolicy('minimumSeparateOvertimePercent', numberOrNull(event.target.value))} />
              </OracleFormRow>
            </div>
          </div>

          <Button type="submit" disabled={busy === 'Overtime setup'}>
            {busy === 'Overtime setup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save Overtime Setup
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function OracleFormRow({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="grid gap-1.5 sm:grid-cols-[150px_minmax(0,1fr)] sm:items-center">
      <Label className="text-sm font-normal text-foreground">{label}</Label>
      {children}
    </div>
  );
}

function GradesSetupForm({
  setup,
  grade,
  notch,
  busy,
  onGradeChange,
  onNotchChange,
  onSaveGrade,
  onSaveGeneratedGrades,
}: {
  setup: PayrollGradeSetup;
  grade: PayrollGrade;
  notch: PayrollGradeNotch;
  busy: string | null;
  onGradeChange: (value: PayrollGrade) => void;
  onNotchChange: (value: PayrollGradeNotch) => void;
  onSaveGrade: () => void;
  onSaveGeneratedGrades: (rows: GradeSetupRow[], draft: GradeSetupDraft) => void;
}) {
  const [rearrangeOpen, setRearrangeOpen] = useState(false);
  const [gradeSetupOpen, setGradeSetupOpen] = useState(false);
  const [enforceInGrades, setEnforceInGrades] = useState(false);
  const [setupDraft, setSetupDraft] = useState<GradeSetupDraft>({
    ...defaultGradeSetupDraft,
    gradeType: grade.gradeType || defaultGradeSetupDraft.gradeType,
    currencyCode: grade.currencyCode || defaultGradeSetupDraft.currencyCode,
  });
  const [generatedRows, setGeneratedRows] = useState<GradeSetupRow[]>([]);
  const existingGradeRows = useMemo(() => gradeSetupRowsFromExisting(setup.grades), [setup.grades]);
  const setupRows = generatedRows.length > 0 ? generatedRows : existingGradeRows.length > 0 ? existingGradeRows : generateGradeSetupRows(setupDraft);
  const rearrangeRows = existingGradeRows.length > 0 ? existingGradeRows : generateGradeSetupRows(setupDraft);
  const editableGridInputClassName = 'h-8 rounded-none border-0 bg-transparent px-1 shadow-none focus-visible:ring-1 focus-visible:ring-ring focus-visible:ring-offset-0';

  const selectGradeRow = (row: GradeSetupRow) => {
    if (row.grade) {
      onGradeChange({
        ...grade,
        ...row.grade,
        startDate: dateValue(row.grade.startDate),
        endDate: dateValue(row.grade.endDate),
      });
      onNotchChange({
        ...notch,
        payrollGradeId: row.grade.id || notch.payrollGradeId,
        gradeId: row.grade.gradeId || notch.gradeId,
        gradeName: row.grade.gradeName,
        systemGradeName: row.grade.systemGradeName || notch.systemGradeName,
        currencyCode: row.grade.currencyCode,
        notch: row.firstNotch,
        legacyCompanyCode: row.grade.legacyCompanyCode || notch.legacyCompanyCode || '001',
      });
      return;
    }

    onGradeChange({
      ...grade,
      gradeType: setupDraft.gradeType,
      gradeName: row.gradeName,
      reportingName: row.reportingName,
      systemGradeName: row.gradeName,
      currencyCode: setupDraft.currencyCode,
      startPoint: row.firstNotch,
      incrementStep: Number(row.unitStep) || null,
      ceiling: row.lastNotch,
      orderField: row.order,
    });
    onNotchChange({
      ...notch,
      gradeName: row.gradeName,
      systemGradeName: row.gradeName,
      currencyCode: setupDraft.currencyCode,
      notch: row.firstNotch,
      orderField: row.order,
    });
  };

  const applyGradeSetup = () => {
    const rows = generateGradeSetupRows(setupDraft);
    setGeneratedRows(rows);
    if (rows[0]) {
      selectGradeRow(rows[0]);
    }
  };

  const updateSetupRow = (index: number, updates: Partial<GradeSetupRow>) => {
    setGeneratedRows((current) => {
      const rows = (current.length > 0 ? current : setupRows).map((row, rowIndex) => (rowIndex === index ? { ...row, ...updates } : row));
      const selected = rows[index];
      if (selected) {
        selectGradeRow(selected);
      }
      return rows;
    });
  };

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <PayrollFormTitle menuId="A0000114">Grades Setup</PayrollFormTitle>
        </CardHeader>
        <CardContent>
          <form
            className="space-y-3"
            onSubmit={(event) => {
              event.preventDefault();
              onSaveGrade();
            }}
          >
            <fieldset className="rounded-md border p-3">
              <legend className="px-1 text-xs font-medium">Grades</legend>
              <div className="grid gap-3 lg:grid-cols-2">
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Grade Type</Label>
                  <select className="h-9 rounded-md border bg-background px-3 text-sm" value={grade.gradeType || 'GRADED'} onChange={(event) => onGradeChange({ ...grade, gradeType: event.target.value })}>
                    <option value="GRADED">GRADED</option>
                    <option value="COMBINED">COMBINED</option>
                  </select>
                </div>
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Reporting Name</Label>
                  <Input value={grade.reportingName ?? ''} onChange={(event) => onGradeChange({ ...grade, reportingName: event.target.value })} />
                </div>
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Grade Name</Label>
                  <Input value={grade.gradeName} onChange={(event) => onGradeChange({ ...grade, gradeName: event.target.value, systemGradeName: event.target.value })} />
                </div>
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Max Value</Label>
                  <Input disabled type="number" step="0.01" value={grade.maxValue ?? ''} onChange={(event) => onGradeChange({ ...grade, maxValue: numberOrNull(event.target.value) })} />
                </div>
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Min Value</Label>
                  <Input disabled type="number" step="0.01" value={grade.minValue ?? ''} onChange={(event) => onGradeChange({ ...grade, minValue: numberOrNull(event.target.value) })} />
                </div>
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Currency</Label>
                  <div className="flex gap-2">
                    <Input maxLength={5} value={grade.currencyCode} onChange={(event) => onGradeChange({ ...grade, currencyCode: event.target.value.toUpperCase() })} />
                    <Button type="button" variant="outline" size="icon" aria-label="Currency lookup">
                      <CreditCard className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)] sm:items-center">
                  <Label className="text-xs text-muted-foreground">Mid Point</Label>
                  <Input type="number" step="0.01" value={grade.midPoint ?? ''} onChange={(event) => onGradeChange({ ...grade, midPoint: numberOrNull(event.target.value) })} />
                </div>
              </div>
            </fieldset>

            <fieldset className="rounded-md border p-3">
              <legend className="px-1 text-xs font-medium">Grade Actions</legend>
              <div className="grid gap-3 md:grid-cols-3">
                <Button type="button" variant="outline" onClick={() => setRearrangeOpen(true)}>
                  Rearrange Grades ...
                </Button>
                <Button type="button" variant="outline" onClick={() => setGradeSetupOpen(true)}>
                  Grade Setup
                </Button>
                <Button type="submit" disabled={busy === 'Grade' || !grade.gradeName}>
                  {busy === 'Grade' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save Grade
                </Button>
              </div>
            </fieldset>

            <fieldset className="rounded-md border p-3">
              <legend className="px-1 text-xs font-medium">Enforce Consistency</legend>
              <div className="grid gap-3 md:grid-cols-2">
                <BooleanField checked={enforceInGrades} label="In Grades" onChange={setEnforceInGrades} />
                <BooleanField checked={grade.enforceNotchConsistency} label="In Notches" onChange={(checked) => onGradeChange({ ...grade, enforceNotchConsistency: checked })} />
              </div>
            </fieldset>
          </form>
        </CardContent>
      </Card>

      <Dialog open={rearrangeOpen} onOpenChange={setRearrangeOpen}>
        <DialogContent className="max-h-[90vh] max-w-[680px] overflow-y-auto p-4">
          <DialogHeader>
            <DialogTitle>HR_GRADES_REARRANGE</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4" style={{ gridTemplateColumns: '220px minmax(0,1fr) 44px' }}>
            <div className="space-y-3">
              <p className="text-sm text-muted-foreground">Arrange the Grades in ascending order, starting with the grade for lower salaried employees to higher salaried employees.</p>
              <div className="h-64 rounded-md border bg-muted" />
            </div>
            <div className="space-y-3">
              <Field label="Currency">
                <div className="flex gap-2">
                  <Input maxLength={5} value={grade.currencyCode} onChange={(event) => onGradeChange({ ...grade, currencyCode: event.target.value.toUpperCase() })} />
                  <Button type="button" variant="outline" size="icon" aria-label="Currency lookup">
                    <CreditCard className="h-4 w-4" />
                  </Button>
                </div>
              </Field>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Order</TableHead>
                    <TableHead>Grade</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rearrangeRows.map((item) => (
                    <TableRow key={`${item.order}-${item.gradeName}`} className="cursor-pointer" onClick={() => selectGradeRow(item)}>
                      <TableCell className="font-medium">{item.order}</TableCell>
                      <TableCell>{item.gradeName}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <div className="flex flex-col justify-center gap-2">
              <Button type="button" variant="outline" size="icon" aria-label="Move grade up">
                <ArrowUp className="h-4 w-4" />
              </Button>
              <Button type="button" variant="outline" size="icon" aria-label="Move grade down">
                <ArrowDown className="h-4 w-4" />
              </Button>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" onClick={() => setRearrangeOpen(false)}>Apply</Button>
            <Button type="button" variant="outline" onClick={() => setRearrangeOpen(false)}>Cancel</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={gradeSetupOpen} onOpenChange={setGradeSetupOpen}>
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Grades Setup</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_220px]">
            <div className="grid gap-3 md:grid-cols-3">
              <Field label="Grade Type">
                <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={setupDraft.gradeType} onChange={(event) => setSetupDraft((current) => ({ ...current, gradeType: event.target.value }))}>
                  <option value="COMBINED">COMBINED</option>
                  <option value="GRADED">GRADED</option>
                </select>
              </Field>
              <Field label="Currency">
                <Input maxLength={5} value={setupDraft.currencyCode} onChange={(event) => setSetupDraft((current) => ({ ...current, currencyCode: event.target.value.toUpperCase() }))} />
              </Field>
              <Field label="Unit Step">
                <Input value={setupDraft.unitStep} onChange={(event) => setSetupDraft((current) => ({ ...current, unitStep: event.target.value }))} />
              </Field>
              <Field label="First Grade">
                <Input value={setupDraft.firstGrade} onChange={(event) => setSetupDraft((current) => ({ ...current, firstGrade: event.target.value }))} />
              </Field>
              <Field label="Last Grade">
                <Input value={setupDraft.lastGrade} onChange={(event) => setSetupDraft((current) => ({ ...current, lastGrade: event.target.value }))} />
              </Field>
              <Field label="Grade Prefix">
                <Input value={setupDraft.gradePrefix} onChange={(event) => setSetupDraft((current) => ({ ...current, gradePrefix: event.target.value }))} />
              </Field>
              <Field label="Grade Suffix">
                <Input value={setupDraft.gradeSuffix} onChange={(event) => setSetupDraft((current) => ({ ...current, gradeSuffix: event.target.value }))} />
              </Field>
              <Field label="Prefix Separator">
                <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={setupDraft.prefixSeparator} onChange={(event) => setSetupDraft((current) => ({ ...current, prefixSeparator: event.target.value }))}>
                  <option>Space</option>
                  <option>Dash</option>
                  <option>None</option>
                </select>
              </Field>
              <Field label="Suffix Separator">
                <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={setupDraft.suffixSeparator} onChange={(event) => setSetupDraft((current) => ({ ...current, suffixSeparator: event.target.value }))}>
                  <option>Space</option>
                  <option>Dash</option>
                  <option>None</option>
                </select>
              </Field>
            </div>
            <div className="flex items-end gap-2">
              <Button type="button" onClick={applyGradeSetup}>Apply</Button>
              <Button type="button" variant="outline" onClick={() => { setSetupDraft(defaultGradeSetupDraft); setGeneratedRows([]); }}>Clear</Button>
            </div>
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Order</TableHead>
                <TableHead>Grade Name</TableHead>
                <TableHead>Reporting Name</TableHead>
                <TableHead>First Notch</TableHead>
                <TableHead>Unit Step</TableHead>
                <TableHead>Last Notch</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {setupRows.map((item, index) => (
                <TableRow key={`grade-setup-row-${index}`}>
                  <TableCell className="w-20">
                    <Input
                      className={editableGridInputClassName}
                      type="number"
                      value={item.order}
                      onChange={(event) => updateSetupRow(index, { order: Number(event.target.value) || 0 })}
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      className={editableGridInputClassName}
                      value={item.gradeName}
                      onChange={(event) => updateSetupRow(index, { gradeName: event.target.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      className={editableGridInputClassName}
                      value={item.reportingName}
                      onChange={(event) => updateSetupRow(index, { reportingName: event.target.value })}
                    />
                  </TableCell>
                  <TableCell className="w-32">
                    <Input
                      className={editableGridInputClassName}
                      value={item.firstNotch}
                      onChange={(event) => updateSetupRow(index, { firstNotch: event.target.value })}
                    />
                  </TableCell>
                  <TableCell className="w-32">
                    <Input
                      className={editableGridInputClassName}
                      value={item.unitStep}
                      onChange={(event) => updateSetupRow(index, { unitStep: event.target.value })}
                    />
                  </TableCell>
                  <TableCell className="w-32">
                    <Input
                      className={editableGridInputClassName}
                      value={item.lastNotch}
                      onChange={(event) => updateSetupRow(index, { lastNotch: event.target.value })}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setGeneratedRows((current) => (current.length > 0 ? current : setupRows).slice(0, -1))}>Delete Record</Button>
            <Button
              type="button"
              disabled={busy === 'Grade setup'}
              onClick={() => {
                onSaveGeneratedGrades(setupRows, setupDraft);
                setGradeSetupOpen(false);
              }}
            >
              {busy === 'Grade setup' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              OK
            </Button>
            <Button type="button" variant="outline" onClick={() => setGradeSetupOpen(false)}>Cancel</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function TaxReliefSetupForm({
  reliefs,
  reliefTypes,
  relief,
  busy,
  onChange,
  onSave,
}: {
  reliefs: PayrollTaxRelief[];
  reliefTypes: PayrollCodeValue[];
  relief: PayrollTaxRelief;
  busy: string | null;
  onChange: (value: PayrollTaxRelief) => void;
  onSave: () => void;
}) {
  const reliefOptions: PayrollCodeValue[] =
    reliefTypes.length > 0
      ? reliefTypes
      : reliefs.map((item) => ({
          id: item.id,
          codeType: 'REL',
          actualCode: item.code,
          description: item.name,
          blocked: !item.isActive,
          accountCode: null,
          additionalDescription: null,
          dependentActualCode: null,
          dependentCodeType: null,
          legacyCompanyCode: null,
        }));
  const annualRelief = roundMoney((Number(relief.amount) || 0) * 12);
  const isPercentage = relief.calculationType === 'PercentageOfBasic';

  return (
    <div className="grid gap-4 2xl:grid-cols-[minmax(0,520px)_1fr]">
      <Card>
        <CardHeader className="pb-3">
          <PayrollFormTitle menuId="A0000116">Tax Relief Setup</PayrollFormTitle>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              onSave();
            }}
          >
            <Field label="Tax Relief">
              <select
                className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                value={relief.code}
                onChange={(event) => {
                  const selected = reliefOptions.find((item) => item.actualCode === event.target.value);
                  onChange({
                    ...relief,
                    code: event.target.value,
                    name: selected?.description || relief.name,
                  });
                }}
              >
                <option value="">Select tax relief</option>
                {reliefOptions.map((item) => (
                  <option key={item.id || `${item.codeType}-${item.actualCode}`} value={item.actualCode}>
                    {item.description}
                  </option>
                ))}
              </select>
            </Field>
            {reliefTypes.length === 0 && reliefs.length === 0 ? (
              <p className="text-xs text-muted-foreground">No tax relief code values found in Codes Description.</p>
            ) : null}
            <Field label="Monthly Relief">
              <Input type="number" step="0.01" value={relief.amount ?? ''} onChange={(event) => onChange({ ...relief, amount: Number(event.target.value) || 0 })} />
            </Field>
            <Field label="Annual Relief">
              <Input
                type="number"
                step="0.01"
                value={annualRelief}
                onChange={(event) => onChange({ ...relief, amount: roundMoney((Number(event.target.value) || 0) / 12) })}
              />
            </Field>
            <BooleanField
              checked={isPercentage}
              label="Percentage?"
              onChange={(checked) => onChange({ ...relief, calculationType: checked ? 'PercentageOfBasic' : 'FixedAmount' })}
            />
            <Button type="submit" disabled={busy === 'Tax relief' || !relief.code}>
              {busy === 'Tax relief' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save Tax Relief
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Tax Reliefs</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Relief</TableHead>
                <TableHead>Monthly</TableHead>
                <TableHead>Annual</TableHead>
                <TableHead>Percentage?</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {reliefs.map((item) => (
                <TableRow key={item.id || item.code} className="cursor-pointer hover:bg-muted/60" onClick={() => onChange({ ...defaultTaxRelief, ...item })}>
                  <TableCell className="font-medium">{item.name || item.code}</TableCell>
                  <TableCell>{item.amount}</TableCell>
                  <TableCell>{roundMoney((Number(item.amount) || 0) * 12)}</TableCell>
                  <TableCell>{item.calculationType === 'PercentageOfBasic' ? 'Yes' : 'No'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

const componentTabs: Array<{ value: PayrollComponent['componentType']; label: string; fieldLabel: string }> = [
  { value: 'Allowance', label: 'Allowances', fieldLabel: 'Allowance' },
  { value: 'EmployeeContribution', label: 'Contributions', fieldLabel: 'Contribution' },
  { value: 'Benefit', label: 'Benefits', fieldLabel: 'Benefit' },
  { value: 'Deduction', label: 'Deductions', fieldLabel: 'Deduction' },
];

const componentCycleOptions = ['Recurring', 'Quarterly', 'Semi Annually', 'Annually', 'One Off'];
const componentCategoryOptions = ['All', 'Department', 'Staff Category', 'Job', 'Position', 'Ranks'];

function componentTypeLabel(value: PayrollComponent['componentType']) {
  return componentTabs.find((item) => item.value === value)?.label ?? value;
}

function AllowancesDeductionsSetupForm({
  components,
  componentRules,
  componentCodeValues,
  staffCategories,
  component,
  busy,
  onChange,
  onSave,
}: {
  components: PayrollComponent[];
  componentRules: PayrollComponentRule[];
  componentCodeValues: Record<string, PayrollCodeValue[]>;
  staffCategories: PayrollCodeValue[];
  component: PayrollComponent;
  busy: string | null;
  onChange: (value: PayrollComponent) => void;
  onSave: (rules?: PayrollComponentRule[]) => void;
}) {
  const activeTab = componentTabs.some((item) => item.value === component.componentType) ? component.componentType : 'Allowance';
  const [deductionGridRows, setDeductionGridRows] = useState<PayrollComponentRule[]>([]);
  const staffCategoryOptions = staffCategories.map((item) => ({ value: item.actualCode, label: item.description || item.actualCode }));

  useEffect(() => {
    if (activeTab !== 'Deduction' || !component.code) {
      setDeductionGridRows([]);
      return;
    }

    const existingRows = componentRules.filter((item) => item.componentType === 'Deduction' && item.componentCode === component.code);
    setDeductionGridRows(existingRows.length > 0 ? existingRows : [{ ...defaultComponentRule, componentCode: component.code }]);
  }, [activeTab, component.code, componentRules]);

  const switchTab = (value: string) => {
    const componentType = value as PayrollComponent['componentType'];
    onChange({
      ...defaultComponent,
      componentType,
      taxable: componentType !== 'Deduction',
      includeInGross: componentType !== 'Deduction',
      currencyCode: component.currencyCode || defaultComponent.currencyCode,
    });
  };

  const updateDeductionGridRow = (index: number, patch: Partial<PayrollComponentRule>) => {
    setDeductionGridRows((current) => current.map((row, rowIndex) => (rowIndex === index ? { ...row, ...patch } : row)));
  };

  const addDeductionGridRow = () => {
    setDeductionGridRows((current) => [...current, { ...defaultComponentRule, componentCode: component.code }]);
  };

  const renderTab = (tab: (typeof componentTabs)[number]) => {
    const options = componentCodeValues[tab.value] ?? [];
    const rows = components.filter((item) => item.componentType === tab.value);
    const selectedIsPercentage = component.calculationType === 'PercentageOfBasic';
    const isContribution = tab.value === 'EmployeeContribution';
    const isDeduction = tab.value === 'Deduction';
    const deductionStaffCategoryOptions = [
      ...staffCategoryOptions,
      ...deductionGridRows
        .filter((row) => row.category && !staffCategoryOptions.some((option) => option.value === row.category))
        .map((row) => ({ value: row.category, label: row.category })),
    ];
    const deductionGridInputClassName = 'h-8 border-0 bg-transparent px-1 text-sm shadow-none focus-visible:ring-1';
    const showEmployeeTaxable = tab.value === 'Allowance' || tab.value === 'Benefit';
    const afterTaxLabel = isContribution ? 'After Tax Contribution?' : isDeduction ? 'After Tax Deduction?' : null;
    const checkboxFields = [
      showEmployeeTaxable ? <BooleanField key="taxable" checked={component.taxable} label="Employee Taxable?" onChange={(checked) => onChange({ ...component, taxable: checked })} /> : null,
      afterTaxLabel ? <BooleanField key="afterTax" checked={component.afterTaxContribution} label={afterTaxLabel} onChange={(checked) => onChange({ ...component, afterTaxContribution: checked })} /> : null,
      <BooleanField key="percentage" checked={selectedIsPercentage} label="Percentage?" onChange={(checked) => onChange({ ...component, calculationType: checked ? 'PercentageOfBasic' : 'FixedAmount' })} />,
      <BooleanField key="separateTax" checked={component.separateTax} label="Tax Separately?" onChange={(checked) => onChange({ ...component, separateTax: checked })} />,
      <BooleanField key="applyBenefit" checked={component.applyToBenefit} label="Apply To Benefit?" onChange={(checked) => onChange({ ...component, applyToBenefit: checked })} />,
      <BooleanField key="employerTaxable" checked={component.employerTaxable} label="Employer Taxable?" onChange={(checked) => onChange({ ...component, employerTaxable: checked })} />,
      <BooleanField key="prorate" checked={component.prorate} label="Prorate?" onChange={(checked) => onChange({ ...component, prorate: checked })} />,
      <BooleanField key="applicable" checked={component.appliesByDefault} label="Applicable?" onChange={(checked) => onChange({ ...component, appliesByDefault: checked, isActive: checked })} />,
    ].filter(Boolean);

    return (
      <TabsContent key={tab.value} value={tab.value} className="mt-0">
        <div className="grid gap-4 2xl:grid-cols-[minmax(0,680px)_1fr]">
          <Card>
            <CardHeader className="pb-3">
              <PayrollFormTitle menuId="A0000117">{tab.label} Setup</PayrollFormTitle>
            </CardHeader>
            <CardContent>
              <form
                className="grid gap-3 lg:grid-cols-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  onSave(tab.value === 'Deduction' ? deductionGridRows : undefined);
                }}
              >
                <Field label={tab.fieldLabel}>
                  <select
                    className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                    value={component.code}
                    onChange={(event) => {
                      const selected = options.find((item) => item.actualCode === event.target.value);
                      onChange({
                        ...component,
                        componentType: tab.value,
                        code: event.target.value,
                        name: selected?.description || component.name,
                      });
                    }}
                  >
                    <option value="">Select {tab.fieldLabel.toLowerCase()}</option>
                    {options.map((item) => (
                      <option key={item.id || `${item.codeType}-${item.actualCode}`} value={item.actualCode}>
                        {item.description}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Amount">
                  <Input type="number" step="0.01" value={component.amount ?? ''} onChange={(event) => onChange({ ...component, amount: Number(event.target.value) || 0 })} />
                </Field>
                <Field label="Tax Free Ceiling">
                  <Input type="number" step="0.01" value={component.taxFreeCeiling ?? ''} onChange={(event) => onChange({ ...component, taxFreeCeiling: numberOrNull(event.target.value) })} />
                </Field>
                <Field label="Max Benefit To Tax">
                  <Input type="number" step="0.01" value={component.maxBenefitToTax ?? ''} onChange={(event) => onChange({ ...component, maxBenefitToTax: numberOrNull(event.target.value) })} />
                </Field>
                <Field label="Separate Tax (%)">
                  <Input type="number" step="0.01" value={component.separateTaxPercent ?? ''} onChange={(event) => onChange({ ...component, separateTaxPercent: numberOrNull(event.target.value) })} />
                </Field>
                <Field label="Employer Amount">
                  <Input type="number" step="0.01" value={component.employerAmount ?? ''} onChange={(event) => onChange({ ...component, employerAmount: numberOrNull(event.target.value) })} />
                </Field>
                <Field label="Category">
                  <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={component.category || 'All'} onChange={(event) => onChange({ ...component, category: event.target.value })}>
                    {componentCategoryOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                  </select>
                </Field>
                <Field label="Next Pay Period">
                  <Input type="date" value={dateValue(component.nextPayPeriodDate)} onChange={(event) => onChange({ ...component, nextPayPeriodDate: nullableDateValue(event.target.value) })} />
                </Field>
                <Field label="Cycle">
                  <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={component.cycle || 'Recurring'} onChange={(event) => onChange({ ...component, cycle: event.target.value })}>
                    {componentCycleOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                  </select>
                </Field>
                <Field label="Last Pay Date">
                  <Input type="date" value={dateValue(component.lastPayDate)} onChange={(event) => onChange({ ...component, lastPayDate: nullableDateValue(event.target.value) })} />
                </Field>
                <div className="rounded-md border bg-muted/20 p-3 lg:col-span-2">
                  <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                    {checkboxFields}
                  </div>
                </div>
                <div className="flex items-end lg:col-span-2">
                  <Button type="submit" disabled={busy === 'Allowance/deduction setup' || !component.code}>
                    {busy === 'Allowance/deduction setup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                    Save {tab.fieldLabel}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">{tab.label}</CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Code</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>Amount</TableHead>
                    <TableHead>Cycle</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>Applicable</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((item) => (
                    <TableRow
                      key={item.id || `${item.componentType}-${item.code}`}
                      className="cursor-pointer hover:bg-muted/60"
                      onClick={() => onChange({ ...defaultComponent, ...item, componentType: tab.value, nextPayPeriodDate: nullableDateValue(item.nextPayPeriodDate), lastPayDate: nullableDateValue(item.lastPayDate) })}
                    >
                      <TableCell className="font-medium">{item.code}</TableCell>
                      <TableCell>{item.name}</TableCell>
                      <TableCell>{item.amount}</TableCell>
                      <TableCell>{item.cycle || '-'}</TableCell>
                      <TableCell>{item.category || '-'}</TableCell>
                      <TableCell>{item.appliesByDefault ? 'Yes' : 'No'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          {tab.value === 'Deduction' ? (
            <Card className="2xl:col-span-2">
              <CardHeader className="pb-3">
                <CardTitle className="text-base">Deduction Staff Category Details</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="min-w-48">Staff Category</TableHead>
                        <TableHead className="w-32">Amount</TableHead>
                        <TableHead className="w-32">Tax Free Ceiling</TableHead>
                        <TableHead className="w-32">Employer Amount</TableHead>
                        <TableHead className="w-28">Percentage?</TableHead>
                        <TableHead className="w-36">After Tax Deduction</TableHead>
                        <TableHead className="w-36">Employer Taxable?</TableHead>
                        <TableHead className="w-28">Applicable?</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {deductionGridRows.map((row, index) => (
                        <TableRow key={row.id || `${row.category || 'new'}-${index}`}>
                          <TableCell>
                            <select
                              className="h-8 w-full rounded-sm border bg-background px-2 text-sm"
                              value={row.category}
                              onChange={(event) => updateDeductionGridRow(index, { category: event.target.value })}
                            >
                              <option value="">Select category</option>
                              {deductionStaffCategoryOptions.map((option) => (
                                <option key={option.value} value={option.value}>{option.label}</option>
                              ))}
                            </select>
                          </TableCell>
                          <TableCell>
                            <Input className={deductionGridInputClassName} type="number" step="0.01" value={row.amount ?? ''} onChange={(event) => updateDeductionGridRow(index, { amount: Number(event.target.value) || 0 })} />
                          </TableCell>
                          <TableCell>
                            <Input className={deductionGridInputClassName} type="number" step="0.01" value={row.taxFreeCeiling ?? ''} onChange={(event) => updateDeductionGridRow(index, { taxFreeCeiling: numberOrNull(event.target.value) })} />
                          </TableCell>
                          <TableCell>
                            <Input className={deductionGridInputClassName} type="number" step="0.01" value={row.employerAmount ?? ''} onChange={(event) => updateDeductionGridRow(index, { employerAmount: numberOrNull(event.target.value) })} />
                          </TableCell>
                          <TableCell>
                            <Checkbox checked={row.calculationType === 'PercentageOfBasic'} onCheckedChange={(checked) => updateDeductionGridRow(index, { calculationType: checked === true ? 'PercentageOfBasic' : 'FixedAmount' })} />
                          </TableCell>
                          <TableCell>
                            <Checkbox checked={row.afterTax} onCheckedChange={(checked) => updateDeductionGridRow(index, { afterTax: checked === true })} />
                          </TableCell>
                          <TableCell>
                            <Checkbox checked={row.employerTaxable} onCheckedChange={(checked) => updateDeductionGridRow(index, { employerTaxable: checked === true })} />
                          </TableCell>
                          <TableCell>
                            <Checkbox checked={row.applicable} onCheckedChange={(checked) => updateDeductionGridRow(index, { applicable: checked === true })} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
                <Button type="button" variant="outline" disabled={!component.code} onClick={addDeductionGridRow}>
                  Add Row
                </Button>
              </CardContent>
            </Card>
          ) : null}
        </div>
      </TabsContent>
    );
  };

  return (
    <Tabs value={activeTab} onValueChange={switchTab} className="space-y-4">
      <TabsList className="grid h-auto grid-cols-2 lg:grid-cols-4">
        {componentTabs.map((tab) => (
          <TabsTrigger key={tab.value} value={tab.value}>
            {tab.label}
          </TabsTrigger>
        ))}
      </TabsList>
      {componentTabs.map(renderTab)}
    </Tabs>
  );
}

type BonusExceptionGridRow = PayrollBonusException & { isSelected?: boolean };

function BonusSetupForm({
  bonuses,
  bonusExceptions,
  bonusTypes,
  staffCategories,
  employees,
  bonus,
  busy,
  onChange,
  onSave,
}: {
  bonuses: PayrollBonusPolicy[];
  bonusExceptions: PayrollBonusException[];
  bonusTypes: PayrollCodeValue[];
  staffCategories: PayrollCodeValue[];
  employees: PayrollEmployeeProfile[];
  bonus: PayrollBonusPolicy;
  busy: string | null;
  onChange: (value: PayrollBonusPolicy) => void;
  onSave: (exceptions: BonusExceptionGridRow[]) => void;
}) {
  const bonusOptions: PayrollCodeValue[] =
    bonusTypes.length > 0
      ? bonusTypes
      : bonuses.map((item) => ({
          id: item.id,
          codeType: 'BON',
          actualCode: item.code,
          description: item.name,
          blocked: !item.isActive,
          accountCode: null,
          additionalDescription: null,
          dependentActualCode: null,
          dependentCodeType: null,
          legacyCompanyCode: null,
        }));
  const isPercentage = bonus.calculationType === 'PercentageOfBasic';
  const [exceptionRows, setExceptionRows] = useState<BonusExceptionGridRow[]>([]);
  const [removedExceptionRows, setRemovedExceptionRows] = useState<BonusExceptionGridRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeePickerOpen, setEmployeePickerOpen] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const bonusExceptionInputClassName = 'h-8 border-0 bg-transparent px-1 text-right text-sm shadow-none focus-visible:ring-1';
  const staffCategoryOptions = staffCategories.map((item) => ({ value: item.actualCode, label: item.description || item.actualCode }));
  const bonusCategoryOptions = [{ value: 'All', label: 'All' }, ...staffCategoryOptions];
  const activeEmployees = useMemo(
    () => employees.filter((employee) => employee.payrollActive).sort((left, right) => left.employeeNumber.localeCompare(right.employeeNumber)),
    [employees],
  );
  const selectedExceptionProfileIds = useMemo(
    () => new Set(exceptionRows.map((row) => row.employeeProfileId).filter(Boolean) as string[]),
    [exceptionRows],
  );
  const filteredExceptionRows = useMemo(() => {
    const term = employeeSearch.trim().toLowerCase();
    if (!term) {
      return exceptionRows;
    }

    return exceptionRows.filter((row) =>
      row.employeeNumber.toLowerCase().includes(term) ||
      row.employeeName.toLowerCase().includes(term),
    );
  }, [employeeSearch, exceptionRows]);
  const filteredPickerEmployees = useMemo(() => {
    const term = pickerSearch.trim().toLowerCase();
    const candidates = activeEmployees.filter((employee) => !selectedExceptionProfileIds.has(employee.id || ''));
    if (!term) {
      return candidates;
    }

    return candidates.filter((employee) =>
      employee.employeeNumber.toLowerCase().includes(term) ||
      employee.employeeName.toLowerCase().includes(term),
    );
  }, [activeEmployees, pickerSearch, selectedExceptionProfileIds]);
  const pickerSelectedSet = useMemo(() => new Set(pickerSelectedIds), [pickerSelectedIds]);
  const selectablePickerIds = useMemo(
    () => activeEmployees
      .filter((employee) => !selectedExceptionProfileIds.has(employee.id || ''))
      .map((employee) => employee.id || '')
      .filter(Boolean),
    [activeEmployees, selectedExceptionProfileIds],
  );
  const allPickerEmployeesSelected = selectablePickerIds.length > 0 &&
    selectablePickerIds.every((id) => pickerSelectedSet.has(id));
  const pickerSelectAllState = allPickerEmployeesSelected
    ? true
    : pickerSelectedIds.length > 0
      ? 'indeterminate'
      : false;

  useEffect(() => {
    if (!bonus.code) {
      setExceptionRows([]);
      setRemovedExceptionRows([]);
      return;
    }

    const selectedBonusCode = bonus.code.trim().toUpperCase();
    setExceptionRows(bonusExceptions.filter((item) => item.bonusCode.trim().toUpperCase() === selectedBonusCode));
    setRemovedExceptionRows([]);
    setEmployeeSearch('');
  }, [bonus.code, bonusExceptions]);

  const updateExceptionRow = (employeeNumber: string, patch: Partial<PayrollBonusException>) => {
    setExceptionRows((current) => current.map((row) => (row.employeeNumber === employeeNumber ? { ...row, ...patch } : row)));
  };

  const openEmployeePicker = () => {
    setPickerSearch('');
    setPickerSelectedIds([]);
    setEmployeePickerOpen(true);
  };

  const togglePickerEmployee = (employeeProfileId: string, selected: boolean) => {
    setPickerSelectedIds((current) => {
      if (selected) {
        return current.includes(employeeProfileId) ? current : [...current, employeeProfileId];
      }

      return current.filter((id) => id !== employeeProfileId);
    });
  };

  const setVisiblePickerSelection = (selected: boolean) => {
    const visibleIds = new Set(filteredPickerEmployees.map((employee) => employee.id || '').filter(Boolean));
    setPickerSelectedIds((current) => {
      if (selected) {
        return Array.from(new Set([...current, ...visibleIds]));
      }

      return current.filter((id) => !visibleIds.has(id));
    });
  };

  const setAllPickerSelection = (selected: boolean) => {
    setPickerSelectedIds((current) =>
      selected
        ? Array.from(new Set([...current, ...selectablePickerIds]))
        : [],
    );
  };

  const addPickerEmployees = () => {
    const selectedIds = new Set(pickerSelectedIds);
    const selectedEmployees = activeEmployees.filter((employee) => selectedIds.has(employee.id || ''));
    setExceptionRows((current) => [
      ...current,
      ...selectedEmployees
        .filter((employee) => !current.some((row) => row.employeeProfileId === employee.id || row.employeeNumber === employee.employeeNumber))
        .map((employee) => ({
          ...defaultBonusException,
          bonusCode: bonus.code,
          employeeProfileId: employee.id,
          employeeNumber: employee.employeeNumber,
          employeeName: employee.employeeName,
          calculationType: bonus.calculationType,
          amount: 0,
          taxable: bonus.taxable,
        })),
    ]);
    setRemovedExceptionRows((current) => current.filter((row) => !selectedIds.has(row.employeeProfileId || '')));
    setEmployeePickerOpen(false);
  };

  const removeExceptionRow = (row: BonusExceptionGridRow) => {
    setExceptionRows((current) => current.filter((item) => item.employeeNumber !== row.employeeNumber));
    if (row.id) {
      setRemovedExceptionRows((current) =>
        current.some((item) => item.id === row.id)
          ? current
          : [...current, { ...row, isSelected: false }],
      );
    }
  };

  const saveBonus = () => {
    onSave([
      ...exceptionRows.map((row) => ({ ...row, isSelected: true })),
      ...removedExceptionRows.map((row) => ({ ...row, isSelected: false })),
    ]);
  };

  return (
    <div className="grid gap-4 2xl:grid-cols-[minmax(0,680px)_1fr]">
      <Card>
        <CardHeader className="pb-3">
          <PayrollFormTitle menuId="A0000118">Bonus Setup</PayrollFormTitle>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-3 lg:grid-cols-2"
            onSubmit={(event) => {
              event.preventDefault();
              saveBonus();
            }}
          >
            <Field label="Bonus Type">
              <select
                className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                value={bonus.code}
                onChange={(event) => {
                  const selected = bonusOptions.find((item) => item.actualCode === event.target.value);
                  onChange({ ...bonus, code: event.target.value, name: selected?.description || bonus.name });
                }}
              >
                <option value="">Select bonus</option>
                {bonusOptions.map((item) => (
                  <option key={item.id || `${item.codeType}-${item.actualCode}`} value={item.actualCode}>
                    {item.description}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Amount">
              <Input type="number" step="0.01" value={bonus.amount ?? ''} onChange={(event) => onChange({ ...bonus, amount: Number(event.target.value) || 0 })} />
            </Field>
            <Field label="Tax Free Ceiling">
              <Input type="number" step="0.01" value={bonus.taxFreeCeiling ?? ''} onChange={(event) => onChange({ ...bonus, taxFreeCeiling: numberOrNull(event.target.value) })} />
            </Field>
            <Field label="Annual Salary To Tax">
              <Input type="number" step="0.01" value={bonus.annualSalaryPercentToTax ?? ''} onChange={(event) => onChange({ ...bonus, annualSalaryPercentToTax: numberOrNull(event.target.value) })} />
            </Field>
            <Field label="Tax Rate">
              <Input type="number" step="0.01" value={bonus.taxRate ?? ''} onChange={(event) => onChange({ ...bonus, taxRate: numberOrNull(event.target.value) })} />
            </Field>
            <Field label="Minimum Months">
              <Input type="number" value={bonus.minimumMonths ?? ''} onChange={(event) => onChange({ ...bonus, minimumMonths: numberOrNull(event.target.value) })} />
            </Field>
            <Field label="Category">
              <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={bonus.category || 'All'} onChange={(event) => onChange({ ...bonus, category: event.target.value })}>
                {bonusCategoryOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </Field>
            <Field label="Cycle">
              <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={bonus.cycle || 'Recurring'} onChange={(event) => onChange({ ...bonus, cycle: event.target.value })}>
                {componentCycleOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            </Field>
            <Field label="Pay Period">
              <Input
                type="month"
                value={monthValue(bonus.nextPayPeriodDate)}
                onChange={(event) => onChange({
                  ...bonus,
                  nextPayPeriodDate: nullableMonthValue(event.target.value),
                  nextPayPeriod: event.target.value ? Number(event.target.value.replace('-', '')) : null,
                })}
              />
            </Field>
            <Field label="Last Pay Period">
              <Input
                type="month"
                value={monthValue(bonus.lastPayPeriodDate)}
                onChange={(event) => onChange({
                  ...bonus,
                  lastPayPeriodDate: nullableMonthValue(event.target.value),
                  lastPayPeriod: event.target.value ? Number(event.target.value.replace('-', '')) : null,
                })}
              />
            </Field>
            <Field label="Prorate?">
              <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={bonus.prorate ? 'Prorate' : 'Straight Bonus'} onChange={(event) => onChange({ ...bonus, prorate: event.target.value === 'Prorate' })}>
                <option value="Prorate">Prorate</option>
                <option value="Straight Bonus">Straight Bonus</option>
              </select>
            </Field>
            <div className="rounded-md border bg-muted/20 p-3 lg:col-span-2">
              <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                <BooleanField checked={isPercentage} label="Percentage?" onChange={(checked) => onChange({ ...bonus, calculationType: checked ? 'PercentageOfBasic' : 'FixedAmount' })} />
                <BooleanField checked={bonus.taxable} label="Taxable?" onChange={(checked) => onChange({ ...bonus, taxable: checked })} />
                <BooleanField checked={bonus.separateTax} label="Tax Separately?" onChange={(checked) => onChange({ ...bonus, separateTax: checked })} />
                <BooleanField checked={bonus.paySeparate} label="Pay Separate?" onChange={(checked) => onChange({ ...bonus, paySeparate: checked })} />
                <BooleanField checked={bonus.isActive} label="Applicable?" onChange={(checked) => onChange({ ...bonus, isActive: checked })} />
              </div>
            </div>
            <div className="flex items-end lg:col-span-2">
              <Button type="submit" disabled={busy === 'Bonus setup' || !bonus.code}>
                {busy === 'Bonus setup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Save Bonus
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Bonuses</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Description</TableHead>
                <TableHead>Amount</TableHead>
                <TableHead>Cycle</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Applicable</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {bonuses.map((item) => (
                <TableRow key={item.id || item.code} className="cursor-pointer hover:bg-muted/60" onClick={() => onChange({ ...defaultBonusPolicy, ...item, nextPayPeriodDate: nullableDateValue(item.nextPayPeriodDate), lastPayPeriodDate: nullableDateValue(item.lastPayPeriodDate) })}>
                  <TableCell className="font-medium">{item.code}</TableCell>
                  <TableCell>{item.name}</TableCell>
                  <TableCell>{item.amount}</TableCell>
                  <TableCell>{item.cycle || '-'}</TableCell>
                  <TableCell>{item.category || '-'}</TableCell>
                  <TableCell>{item.isActive ? 'Yes' : 'No'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card className="2xl:col-span-2">
        <CardHeader className="pb-3">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div className="grid flex-1 gap-3 sm:grid-cols-[minmax(240px,1fr)_120px]">
              <Field label="Find Employee">
                <div className="relative">
                  <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    className="h-9 pl-9"
                    value={employeeSearch}
                    onChange={(event) => setEmployeeSearch(event.target.value)}
                    placeholder="Employee no or name"
                  />
                </div>
              </Field>
              <Field label="Selected">
                <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm font-medium">
                  {exceptionRows.length}
                </div>
              </Field>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button type="button" size="sm" variant="outline" onClick={openEmployeePicker} disabled={!bonus.code}>
                <UserPlus className="mr-2 h-4 w-4" />
                Add Employee
              </Button>
              <Button type="button" size="sm" onClick={saveBonus} disabled={busy === 'Bonus setup' || !bonus.code}>
                {busy === 'Bonus setup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Save Bonus
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="min-w-32">Employee No</TableHead>
                  <TableHead className="min-w-64">Employee Name</TableHead>
                  <TableHead className="w-36 text-right">Amount</TableHead>
                  <TableHead className="w-28">Percentage?</TableHead>
                  <TableHead className="w-24">Taxable?</TableHead>
                  <TableHead className="w-28">Applicable?</TableHead>
                  <TableHead className="w-12" aria-label="Remove" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredExceptionRows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-sm text-muted-foreground">
                      No employees selected.
                    </TableCell>
                  </TableRow>
                ) : filteredExceptionRows.map((row) => (
                  <TableRow key={row.id || row.employeeProfileId || row.employeeNumber}>
                    <TableCell className="font-medium">{row.employeeNumber}</TableCell>
                    <TableCell>{row.employeeName}</TableCell>
                    <TableCell>
                      <Input className={bonusExceptionInputClassName} type="number" step="0.01" value={row.amount ?? ''} onChange={(event) => updateExceptionRow(row.employeeNumber, { amount: Number(event.target.value) || 0 })} />
                    </TableCell>
                    <TableCell>
                      <Checkbox checked={row.calculationType === 'PercentageOfBasic'} onCheckedChange={(checked) => updateExceptionRow(row.employeeNumber, { calculationType: checked === true ? 'PercentageOfBasic' : 'FixedAmount' })} />
                    </TableCell>
                    <TableCell>
                      <Checkbox checked={row.taxable} onCheckedChange={(checked) => updateExceptionRow(row.employeeNumber, { taxable: checked === true })} />
                    </TableCell>
                    <TableCell>
                      <Checkbox checked={row.applicable} onCheckedChange={(checked) => updateExceptionRow(row.employeeNumber, { applicable: checked === true })} />
                    </TableCell>
                    <TableCell className="text-center">
                      <Button type="button" variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" onClick={() => removeExceptionRow(row)} aria-label={`Remove ${row.employeeName}`}>
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={employeePickerOpen} onOpenChange={setEmployeePickerOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Add Employee</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
              <div className="relative md:w-80">
                <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  className="h-9 pl-9"
                  value={pickerSearch}
                  onChange={(event) => setPickerSearch(event.target.value)}
                  placeholder="Search employees"
                />
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm text-muted-foreground">{pickerSelectedIds.length} selected</span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setAllPickerSelection(true)}
                  disabled={selectablePickerIds.length === 0 || allPickerEmployeesSelected}
                >
                  Select All
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setAllPickerSelection(false)}
                  disabled={pickerSelectedIds.length === 0}
                >
                  Clear All
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setVisiblePickerSelection(true)} disabled={filteredPickerEmployees.length === 0}>
                  Select Visible
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setVisiblePickerSelection(false)} disabled={filteredPickerEmployees.length === 0}>
                  Clear Visible
                </Button>
              </div>
            </div>
            <div className="max-h-[420px] overflow-auto rounded-md border">
              <table className="w-full min-w-[620px] text-sm">
                <thead className="sticky top-0 bg-muted text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="w-12 px-3 py-2 text-left">
                      <Checkbox
                        aria-label="Select all employees"
                        checked={pickerSelectAllState}
                        onCheckedChange={(checked) => setAllPickerSelection(checked === true)}
                        disabled={selectablePickerIds.length === 0}
                      />
                    </th>
                    <th className="w-[120px] px-3 py-2 text-left">Employee No</th>
                    <th className="px-3 py-2 text-left">Employee Name</th>
                    <th className="w-36 whitespace-nowrap px-3 py-2 text-right">Basic Salary</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredPickerEmployees.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No employees found.
                      </td>
                    </tr>
                  ) : filteredPickerEmployees.map((employee) => {
                    const employeeProfileId = employee.id || '';
                    return (
                      <tr key={employeeProfileId || employee.employeeNumber} className="border-t">
                        <td className="px-3 py-2">
                          <Checkbox
                            checked={pickerSelectedSet.has(employeeProfileId)}
                            disabled={!employeeProfileId}
                            onCheckedChange={(checked) => togglePickerEmployee(employeeProfileId, checked === true)}
                          />
                        </td>
                        <td className="px-3 py-2 font-medium">{employee.employeeNumber}</td>
                        <td className="px-3 py-2">{employee.employeeName}</td>
                        <td className="px-3 py-2 text-right tabular-nums">{formatAmount(employee.salaryBasis?.monthlyBasicSalary)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setEmployeePickerOpen(false)}>
              Cancel
            </Button>
            <Button type="button" onClick={addPickerEmployees} disabled={pickerSelectedIds.length === 0}>
              <UserPlus className="mr-2 h-4 w-4" />
              Add Selected
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

const backpayTabs: Array<{ value: BackpayOperationType; label: string }> = [
  { value: 'IncreaseSalary', label: 'Increase Salary' },
  { value: 'SalaryArrears', label: 'Salary Arrears' },
];

const backpayCategoryOptions = [
  { value: 'All Staff', label: 'All Staff', oracleCode: 'ALL' },
  { value: 'Grade', label: 'Grade', oracleCode: 'GRA' },
  { value: 'Position', label: 'Position', oracleCode: 'POS' },
  { value: 'Department', label: 'Department', oracleCode: 'DEP' },
  { value: 'Staff Category', label: 'Staff Category', oracleCode: 'CAT' },
];
const backpayMinimumServiceOptions = ['', 'Length of Service', 'In Service Since'];

function normalizeBackpayCategoryValue(categoryType?: string | null) {
  const value = categoryType?.trim().toUpperCase().replace(/\s+/g, '');

  if (value === 'ALL' || value === 'ALLSTAFF') return 'All Staff';
  if (value === 'GRA' || value === 'GRADE') return 'Grade';
  if (value === 'POS' || value === 'POSITION') return 'Position';
  if (value === 'DEP' || value === 'DEPARTMENT') return 'Department';
  if (value === 'CAT' || value === 'STAFFCATEGORY') return 'Staff Category';

  return categoryType || 'All Staff';
}

function defaultPolicyForBackpayOperation(operationType: BackpayOperationType): PayrollBackpayPolicy {
  return {
    ...defaultBackpayPolicy,
    operationType,
    categoryType: operationType === 'SalaryArrears' ? 'Staff Category' : 'All Staff',
    numberOfMonths: operationType === 'SalaryArrears' ? 3 : null,
    applyTax: operationType === 'SalaryArrears',
    applySsf: operationType === 'SalaryArrears',
    minimumServiceMode: operationType === 'SalaryArrears' ? '' : 'In Service Since',
  };
}

function backpayCategoryColumnLabel(categoryType: string) {
  return categoryType === 'Grade' ? 'Grade Name' : categoryType;
}

function codeValueOptions(values: PayrollCodeValue[]) {
  return values.map((item) => ({ value: item.actualCode, label: item.description || item.actualCode }));
}

function backpayCategoryItems(categoryType: string, staffCategories: PayrollCodeValue[], departments: PayrollCodeValue[], positions: PayrollCodeValue[], grades: PayrollGrade[]) {
  const normalizedCategory = normalizeBackpayCategoryValue(categoryType);

  if (normalizedCategory === 'Grade') {
    return grades.map((item) => ({ value: item.gradeName, label: item.reportingName || item.gradeName }));
  }

  if (normalizedCategory === 'Position') {
    return codeValueOptions(positions);
  }

  if (normalizedCategory === 'Department') {
    return codeValueOptions(departments);
  }

  if (normalizedCategory === 'Staff Category') {
    return codeValueOptions(staffCategories);
  }

  return [];
}

function buildBackpayRowsFromCategory(
  operationType: BackpayOperationType,
  categoryType: string,
  amount: number,
  calculationType: PayrollBackpayRule['calculationType'],
  categoryItems: Array<{ value: string; label: string }>,
) {
  const normalizedCategory = normalizeBackpayCategoryValue(categoryType);

  if (normalizedCategory === 'All Staff') {
    return [];
  }

  return categoryItems.map((item) => ({
    ...defaultBackpayRule,
    operationType,
    categoryType: normalizedCategory,
    categoryCode: item.value,
    categoryName: item.label,
    calculationType,
    amount,
  }));
}

function BackpaySalaryIncreaseForm({
  policies,
  rules,
  exceptions,
  staffCategories,
  departments,
  positions,
  grades,
  employees,
  policy,
  busy,
  onChange,
  onSave,
}: {
  policies: PayrollBackpayPolicy[];
  rules: PayrollBackpayRule[];
  exceptions: PayrollBackpayException[];
  staffCategories: PayrollCodeValue[];
  departments: PayrollCodeValue[];
  positions: PayrollCodeValue[];
  grades: PayrollGrade[];
  employees: PayrollEmployeeProfile[];
  policy: PayrollBackpayPolicy;
  busy: string | null;
  onChange: (value: PayrollBackpayPolicy) => void;
  onSave: (policy: PayrollBackpayPolicy, rules: PayrollBackpayRule[], exceptions: PayrollBackpayException[]) => void;
}) {
  const [gridRows, setGridRows] = useState<PayrollBackpayRule[]>([]);
  const [exceptionRows, setExceptionRows] = useState<PayrollBackpayException[]>([]);
  const [exceptionOpen, setExceptionOpen] = useState(false);
  const [selectedEmployeeId, setSelectedEmployeeId] = useState('');
  const [employeeSearch, setEmployeeSearch] = useState('');
  const policyAmountRef = useRef(policy.amount);
  const policyCalculationTypeRef = useRef(policy.calculationType);
  const activeOperation = backpayTabs.some((item) => item.value === policy.operationType) ? policy.operationType : 'IncreaseSalary';
  const activeCategoryType = normalizeBackpayCategoryValue(policy.categoryType);
  const isPercentage = policy.calculationType === 'PercentageOfBasic';
  const showCategoryGrid = activeCategoryType !== 'All Staff';
  const categoryItems = useMemo(() => backpayCategoryItems(activeCategoryType, staffCategories, departments, positions, grades), [activeCategoryType, departments, grades, positions, staffCategories]);
  const categoryOptionsWithExistingRows = [
    ...categoryItems,
    ...gridRows
      .filter((row) => row.categoryCode && !categoryItems.some((item) => item.value === row.categoryCode))
      .map((row) => ({ value: row.categoryCode, label: row.categoryName || row.categoryCode })),
  ];
  const filteredEmployees = employees.filter((employee) => {
    const term = employeeSearch.trim().toLowerCase();
    return !term || employee.employeeNumber.toLowerCase().includes(term) || employee.employeeName.toLowerCase().includes(term);
  });

  useEffect(() => {
    policyAmountRef.current = policy.amount;
    policyCalculationTypeRef.current = policy.calculationType;
  }, [policy.amount, policy.calculationType]);

  useEffect(() => {
    if (!showCategoryGrid) {
      setGridRows([]);
      return;
    }

    const existingRows = rules.filter((item) => item.operationType === activeOperation && normalizeBackpayCategoryValue(item.categoryType) === activeCategoryType);
    if (existingRows.length > 0) {
      setGridRows(existingRows.map((row) => ({ ...row, categoryType: activeCategoryType })));
      return;
    }

    setGridRows(buildBackpayRowsFromCategory(activeOperation, activeCategoryType, policyAmountRef.current, policyCalculationTypeRef.current, categoryItems));
  }, [activeCategoryType, activeOperation, categoryItems, rules, showCategoryGrid]);

  useEffect(() => {
    setExceptionRows(exceptions.filter((item) => item.operationType === 'SalaryArrears'));
  }, [exceptions]);

  const switchOperation = (value: string) => {
    const operationType = value as BackpayOperationType;
    const existingPolicy = policies.find((item) => item.operationType === operationType);
    onChange(
      existingPolicy
        ? { ...defaultPolicyForBackpayOperation(operationType), ...existingPolicy, effectiveDate: nullableDateValue(existingPolicy.effectiveDate), minimumServiceDate: nullableDateValue(existingPolicy.minimumServiceDate) }
        : defaultPolicyForBackpayOperation(operationType),
    );
  };

  const updateGridRow = (index: number, patch: Partial<PayrollBackpayRule>) => {
    setGridRows((current) => current.map((row, rowIndex) => (rowIndex === index ? { ...row, ...patch } : row)));
  };

  const changeCategory = (categoryType: string) => {
    const normalizedCategory = normalizeBackpayCategoryValue(categoryType);
    const nextItems = backpayCategoryItems(normalizedCategory, staffCategories, departments, positions, grades);

    onChange({ ...policy, categoryType: normalizedCategory });
    setGridRows(buildBackpayRowsFromCategory(activeOperation, normalizedCategory, policy.amount, policy.calculationType, nextItems));
  };

  const addGridRow = () => {
    setGridRows((current) => [...current, { ...defaultBackpayRule, operationType: activeOperation, categoryType: activeCategoryType, calculationType: policy.calculationType, amount: policy.amount }]);
  };

  const updateExceptionRow = (index: number, patch: Partial<PayrollBackpayException>) => {
    setExceptionRows((current) => current.map((row, rowIndex) => (rowIndex === index ? { ...row, ...patch } : row)));
  };

  const addExceptionEmployee = () => {
    const employee = employees.find((item) => item.id === selectedEmployeeId);
    if (!employee || exceptionRows.some((item) => item.employeeNumber === employee.employeeNumber)) {
      return;
    }

    setExceptionRows((current) => [
      ...current,
      {
        ...defaultBackpayException,
        employeeProfileId: employee.id,
        employeeNumber: employee.employeeNumber,
        employeeName: employee.employeeName,
        calculationType: policy.calculationType,
        amount: policy.amount,
      },
    ]);
    setSelectedEmployeeId('');
  };

  const gridInputClassName = 'h-8 border-0 bg-transparent px-1 text-right text-sm shadow-none focus-visible:ring-1';

  return (
    <Tabs value={activeOperation} onValueChange={switchOperation} className="space-y-4">
      <TabsList className="grid h-auto w-full max-w-md grid-cols-2">
        {backpayTabs.map((tab) => (
          <TabsTrigger key={tab.value} value={tab.value}>
            {tab.label}
          </TabsTrigger>
        ))}
      </TabsList>

      {backpayTabs.map((tab) => (
        <TabsContent key={tab.value} value={tab.value} className="mt-0">
          <div className="grid gap-4">
            <Card>
              <CardHeader className="pb-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <PayrollFormTitle menuId="A0000119">Backpay / Salary Increase</PayrollFormTitle>
                  {activeOperation === 'SalaryArrears' ? (
                    <Button type="button" variant="outline" onClick={() => setExceptionOpen(true)}>
                      Exceptions
                    </Button>
                  ) : null}
                </div>
              </CardHeader>
              <CardContent>
                <form
                  className="space-y-4"
                  onSubmit={(event) => {
                    event.preventDefault();
                    onSave(policy, gridRows, exceptionRows);
                  }}
                >
                  <div className="grid gap-3 lg:grid-cols-3">
                    <Field label="Amount">
                      <Input type="number" step="0.01" value={policy.amount ?? ''} onChange={(event) => onChange({ ...policy, amount: Number(event.target.value) || 0 })} />
                    </Field>
                    <Field label="No of Months">
                      <Input type="number" value={policy.numberOfMonths ?? ''} onChange={(event) => onChange({ ...policy, numberOfMonths: numberOrNull(event.target.value) })} />
                    </Field>
                    <Field label="Effective Date">
                      <Input type="date" value={dateValue(policy.effectiveDate)} onChange={(event) => onChange({ ...policy, effectiveDate: nullableDateValue(event.target.value) })} />
                    </Field>
                    <Field label="Minimum Service">
                      <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_150px]">
                        <select className="h-9 rounded-md border bg-background px-3 text-sm" value={policy.minimumServiceMode || ''} onChange={(event) => onChange({ ...policy, minimumServiceMode: event.target.value })}>
                          {backpayMinimumServiceOptions.map((option) => (
                            <option key={option || 'blank'} value={option}>{option === 'Length of Service' ? 'Length of Service (Years)' : option || ''}</option>
                          ))}
                        </select>
                        {policy.minimumServiceMode === 'In Service Since' ? (
                          <Input type="date" value={dateValue(policy.minimumServiceDate)} onChange={(event) => onChange({ ...policy, minimumServiceDate: nullableDateValue(event.target.value) })} />
                        ) : policy.minimumServiceMode === 'Length of Service' ? (
                          <div className="relative">
                            <Input className="pr-14" type="number" min={0} step="1" value={policy.minimumServiceValue ?? ''} onChange={(event) => onChange({ ...policy, minimumServiceValue: numberOrNull(event.target.value) })} />
                            <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-xs text-muted-foreground">Years</span>
                          </div>
                        ) : (
                          <Input type="number" value={policy.minimumServiceValue ?? ''} onChange={(event) => onChange({ ...policy, minimumServiceValue: numberOrNull(event.target.value) })} />
                        )}
                      </div>
                    </Field>
                    <Field label="Category">
                      <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={activeCategoryType} onChange={(event) => changeCategory(event.target.value)}>
                        {backpayCategoryOptions.map((option) => (
                          <option key={option.oracleCode} value={option.value}>{option.label}</option>
                        ))}
                      </select>
                    </Field>
                    <div className="grid gap-2 rounded-md border bg-muted/20 p-3 sm:grid-cols-3">
                      <BooleanField checked={isPercentage} label="Percentage?" onChange={(checked) => onChange({ ...policy, calculationType: checked ? 'PercentageOfBasic' : 'FixedAmount' })} />
                      <BooleanField checked={policy.applyTax} label="Apply Tax?" onChange={(checked) => onChange({ ...policy, applyTax: checked })} />
                      <BooleanField checked={policy.applySsf} label="Apply SSF?" onChange={(checked) => onChange({ ...policy, applySsf: checked })} />
                    </div>
                  </div>

                  {showCategoryGrid ? (
                    <div className="overflow-x-auto rounded-md border">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead className="min-w-52">{backpayCategoryColumnLabel(activeCategoryType)}</TableHead>
                            <TableHead className="w-36 text-right">Amount</TableHead>
                            <TableHead className="w-28">Percentage?</TableHead>
                            <TableHead className="w-28">Applicable?</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {gridRows.map((row, index) => (
                            <TableRow key={row.id || `${row.categoryCode || 'new'}-${index}`}>
                              <TableCell>
                                <select
                                  className="h-8 w-full rounded-sm border bg-background px-2 text-sm"
                                  value={row.categoryCode}
                                  onChange={(event) => {
                                    const selected = categoryOptionsWithExistingRows.find((item) => item.value === event.target.value);
                                    updateGridRow(index, { categoryCode: event.target.value, categoryName: selected?.label || event.target.value });
                                  }}
                                >
                                  <option value="">Select {activeCategoryType.toLowerCase()}</option>
                                  {categoryOptionsWithExistingRows.map((option) => (
                                    <option key={option.value} value={option.value}>{option.label}</option>
                                  ))}
                                </select>
                              </TableCell>
                              <TableCell>
                                <Input className={gridInputClassName} type="number" step="0.01" value={row.amount ?? ''} onChange={(event) => updateGridRow(index, { amount: Number(event.target.value) || 0 })} />
                              </TableCell>
                              <TableCell>
                                <Checkbox checked={row.calculationType === 'PercentageOfBasic'} onCheckedChange={(checked) => updateGridRow(index, { calculationType: checked === true ? 'PercentageOfBasic' : 'FixedAmount' })} />
                              </TableCell>
                              <TableCell>
                                <Checkbox checked={row.applicable} onCheckedChange={(checked) => updateGridRow(index, { applicable: checked === true })} />
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  ) : null}

                  {showCategoryGrid ? (
                    <Button type="button" variant="outline" onClick={addGridRow}>
                      Add Row
                    </Button>
                  ) : null}

                  <div className="flex flex-wrap gap-3">
                    <Button type="submit" disabled={busy === 'Backpay / salary increase'}>
                      {busy === 'Backpay / salary increase' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                      Process
                    </Button>
                    <Button type="button" variant="outline" onClick={() => onChange(defaultPolicyForBackpayOperation(activeOperation))}>
                      Revert
                    </Button>
                  </div>
                </form>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      ))}

      <Dialog open={exceptionOpen} onOpenChange={setExceptionOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Salary Arrears Exceptions</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-3 lg:grid-cols-[220px_minmax(0,1fr)_auto]">
              <Input placeholder="Find employee" value={employeeSearch} onChange={(event) => setEmployeeSearch(event.target.value)} />
              <select className="h-9 w-full rounded-md border bg-background px-3 text-sm" value={selectedEmployeeId} onChange={(event) => setSelectedEmployeeId(event.target.value)}>
                <option value="">Select employee</option>
                {filteredEmployees.map((employee) => (
                  <option key={employee.id} value={employee.id}>
                    {employee.employeeNumber} - {employee.employeeName}
                  </option>
                ))}
              </select>
              <Button type="button" onClick={addExceptionEmployee} disabled={!selectedEmployeeId}>
                Add Employee
              </Button>
            </div>
            <div className="max-h-[420px] overflow-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="min-w-32">Employee No</TableHead>
                    <TableHead className="min-w-64">Employee Name</TableHead>
                    <TableHead className="w-32 text-right">Amount</TableHead>
                    <TableHead className="w-28">Percentage?</TableHead>
                    <TableHead className="w-28">Applicable?</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {exceptionRows.map((row, index) => (
                    <TableRow key={row.id || `${row.employeeNumber || 'new'}-${index}`}>
                      <TableCell>{row.employeeNumber}</TableCell>
                      <TableCell>{row.employeeName}</TableCell>
                      <TableCell>
                        <Input className={gridInputClassName} type="number" step="0.01" value={row.amount ?? ''} onChange={(event) => updateExceptionRow(index, { amount: Number(event.target.value) || 0 })} />
                      </TableCell>
                      <TableCell>
                        <Checkbox checked={row.calculationType === 'PercentageOfBasic'} onCheckedChange={(checked) => updateExceptionRow(index, { calculationType: checked === true ? 'PercentageOfBasic' : 'FixedAmount' })} />
                      </TableCell>
                      <TableCell>
                        <Checkbox checked={row.applicable} onCheckedChange={(checked) => updateExceptionRow(index, { applicable: checked === true })} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" onClick={() => setExceptionOpen(false)}>OK</Button>
            <Button type="button" variant="outline" onClick={() => setExceptionOpen(false)}>Cancel</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Tabs>
  );
}

function BudgetAnalysisForm({
  defaultPayPeriod,
  defaultPayPeriodFrom,
  defaultPayPeriodTo,
  companyCode,
}: {
  defaultPayPeriod: number;
  defaultPayPeriodFrom?: string | null;
  defaultPayPeriodTo?: string | null;
  companyCode: string;
}) {
  const { toast } = useToast();
  const [payPeriod, setPayPeriod] = useState(defaultPayPeriod > 0 ? defaultPayPeriod : currentPayPeriod);
  const [basicPercent1, setBasicPercent1] = useState(0);
  const [basicPercent2, setBasicPercent2] = useState(0);
  const [basicPercent3, setBasicPercent3] = useState(0);
  const [analysis, setAnalysis] = useState<PayrollBudgetAnalysis | null>(null);
  const [rows, setRows] = useState<PayrollBudgetAnalysisRow[]>([]);
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loadingPeriodRows, setLoadingPeriodRows] = useState(false);
  const [loadingRuns, setLoadingRuns] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [analysisOpen, setAnalysisOpen] = useState(false);
  const periodLoadRef = useRef(0);

  useEffect(() => {
    let active = true;
    setLoadingRuns(true);
    payrollService.getRuns()
      .then((items) => {
        if (active) {
          setRuns(items);
        }
      })
      .catch(() => {
        if (active) {
          setRuns([]);
        }
      })
      .finally(() => {
        if (active) {
          setLoadingRuns(false);
        }
      });

    return () => {
      active = false;
    };
  }, []);

  const payPeriodOptions = useMemo(
    () => Array.from(new Set(runs.map((run) => run.payPeriod).filter((value) => value > 0))).sort((left, right) => right - left),
    [runs],
  );
  const payPeriodSelectOptions = useMemo(() => {
    const options = new Set(payPeriodOptions);
    if (payPeriod > 0) {
      options.add(payPeriod);
    }

    return Array.from(options).sort((left, right) => right - left);
  }, [payPeriod, payPeriodOptions]);

  const totals = useMemo(() => calculateBudgetAnalysisTotals(rows), [rows]);

  const buildAdjustments = useCallback((period: number) => rows
    .filter((row) => row.payPeriod === period)
    .map((row) => ({
      orderField: row.orderField,
      transactionType: row.transactionType,
      actualTransaction: row.actualTransaction,
      amount1: row.amount1,
      include1: row.include1,
      amount2: row.amount2,
      include2: row.include2,
      amount3: row.amount3,
      include3: row.include3,
    })), [rows]);

  const loadPeriodDetails = useCallback(async (selectedPeriod: number) => {
    if (selectedPeriod <= 0) {
      setAnalysis(null);
      setRows([]);
      return;
    }

    const requestId = periodLoadRef.current + 1;
    periodLoadRef.current = requestId;
    setLoadingPeriodRows(true);
    setError(null);

    try {
      const result = await buildPayrollBudgetAnalysis({
        payPeriod: selectedPeriod,
        basicPercent1: 0,
        basicPercent2: 0,
        basicPercent3: 0,
        adjustments: [],
      });
      if (periodLoadRef.current !== requestId) {
        return;
      }

      setAnalysis(result);
      setPayPeriod(result.payPeriod || selectedPeriod);
      setBasicPercent1(result.basicPercent1);
      setBasicPercent2(result.basicPercent2);
      setBasicPercent3(result.basicPercent3);
      setRows(result.rows.map(recalculateBudgetAnalysisRow));
    } catch (err) {
      if (periodLoadRef.current !== requestId) {
        return;
      }

      const message = err instanceof Error ? err.message : 'Unable to load budget analysis details.';
      setAnalysis(null);
      setRows([]);
      setError(message);
      toast({ title: 'Budget Analysis', description: message, variant: 'destructive' });
    } finally {
      if (periodLoadRef.current === requestId) {
        setLoadingPeriodRows(false);
      }
    }
  }, [toast]);

  useEffect(() => {
    const initialPayPeriod = defaultPayPeriod > 0 ? defaultPayPeriod : currentPayPeriod;
    if (initialPayPeriod > 0) {
      setPayPeriod(initialPayPeriod);
      void loadPeriodDetails(initialPayPeriod);
    }
  }, [defaultPayPeriod, loadPeriodDetails]);

  const handlePayPeriodChange = (value: number) => {
    periodLoadRef.current += 1;
    setPayPeriod(value);
    setBasicPercent1(0);
    setBasicPercent2(0);
    setBasicPercent3(0);
    setAnalysis(null);
    setRows([]);
    setAnalysisOpen(false);
    void loadPeriodDetails(value);
  };

  const generateAnalysis = useCallback(async (preserveAdjustments = true) => {
    const selectedPeriod = Number(payPeriod) || 0;
    periodLoadRef.current += 1;
    setLoading(true);
    setError(null);

    try {
      const result = await buildPayrollBudgetAnalysis({
        payPeriod: selectedPeriod,
        basicPercent1,
        basicPercent2,
        basicPercent3,
        adjustments: preserveAdjustments ? buildAdjustments(selectedPeriod) : [],
      });
      setAnalysis(result);
      setPayPeriod(result.payPeriod || selectedPeriod);
      setBasicPercent1(result.basicPercent1);
      setBasicPercent2(result.basicPercent2);
      setBasicPercent3(result.basicPercent3);
      setRows(result.rows.map(recalculateBudgetAnalysisRow));
      setAnalysisOpen(true);
      const resultPeriodLabel = formatPayrollPeriodLabel(result.payPeriodFrom, result.payPeriodTo, result.payPeriod || selectedPeriod);
      toast({
        title: 'Budget Analysis',
        description: `${result.rows.length} row${result.rows.length === 1 ? '' : 's'} generated for ${resultPeriodLabel}.`,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to build budget analysis.';
      setError(message);
      toast({ title: 'Budget Analysis', description: message, variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [basicPercent1, basicPercent2, basicPercent3, buildAdjustments, payPeriod, toast]);

  const saveAnalysis = useCallback(async () => {
    const selectedPeriod = Number(analysis?.payPeriod || payPeriod) || 0;
    periodLoadRef.current += 1;
    setSaving(true);
    setError(null);

    try {
      const normalizedRows = rows.map(recalculateBudgetAnalysisRow);
      const result = await savePayrollBudgetAnalysis({
        payPeriod: selectedPeriod,
        basicPercent1,
        basicPercent2,
        basicPercent3,
        adjustments: buildAdjustments(selectedPeriod),
        rows: normalizedRows,
      });
      setAnalysis(result);
      setPayPeriod(result.payPeriod || selectedPeriod);
      setBasicPercent1(result.basicPercent1);
      setBasicPercent2(result.basicPercent2);
      setBasicPercent3(result.basicPercent3);
      setRows(result.rows.map(recalculateBudgetAnalysisRow));
      const resultPeriodLabel = formatPayrollPeriodLabel(result.payPeriodFrom, result.payPeriodTo, result.payPeriod || selectedPeriod);
      toast({
        title: 'Budget Analysis',
        description: `${result.rows.length} row${result.rows.length === 1 ? '' : 's'} saved for ${resultPeriodLabel}.`,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save budget analysis.';
      setError(message);
      toast({ title: 'Budget Analysis', description: message, variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  }, [analysis?.payPeriod, basicPercent1, basicPercent2, basicPercent3, buildAdjustments, payPeriod, rows, toast]);

  const updateBasicPercent = (scenario: BudgetScenario, value: number) => {
    if (scenario === 1) {
      setBasicPercent1(value);
    } else if (scenario === 2) {
      setBasicPercent2(value);
    } else {
      setBasicPercent3(value);
    }

    setRows((current) => current.map((row) => applyBudgetBasicPercent(row, scenario, value)));
  };

  const updateScenarioAmount = (index: number, scenario: BudgetScenario, value: number) => {
    setRows((current) => current.map((row, rowIndex) => {
      if (rowIndex !== index) {
        return row;
      }

      if (scenario === 1) {
        return recalculateBudgetAnalysisRow({ ...row, amount1: value });
      }

      if (scenario === 2) {
        return recalculateBudgetAnalysisRow({ ...row, amount2: value });
      }

      return recalculateBudgetAnalysisRow({ ...row, amount3: value });
    }));
  };

  const updateScenarioInclude = (index: number, scenario: BudgetScenario, checked: boolean) => {
    setRows((current) => current.map((row, rowIndex) => {
      if (rowIndex !== index) {
        return row;
      }

      if (scenario === 1) {
        return { ...row, include1: checked };
      }

      if (scenario === 2) {
        return { ...row, include2: checked };
      }

      return { ...row, include3: checked };
    }));
  };

  const periodLine = analysis?.payPeriodFrom || analysis?.payPeriodTo
    ? `${dateValue(analysis?.payPeriodFrom) || '-'} to ${dateValue(analysis?.payPeriodTo) || '-'}`
    : '';
  const criteriaRows = () => [
    ['Report', analysis?.reportName ?? 'Budget Analysis Report'],
    ['Pay Period', selectedPeriodLabel],
    ['Period From', dateValue(analysis?.payPeriodFrom)],
    ['Period To', dateValue(analysis?.payPeriodTo)],
    ['Basic (%) 1', basicPercent1],
    ['Basic (%) 2', basicPercent2],
    ['Basic (%) 3', basicPercent3],
  ];
  const detailHeaders = [
    'Description',
    'Type',
    'Actual',
    'Percentage?',
    'Base Amount',
    'Scenario 1 Amount',
    'Scenario 1 Include?',
    'Scenario 1 New Amount',
    'Scenario 1 Variance',
    'Scenario 2 Amount',
    'Scenario 2 Include?',
    'Scenario 2 New Amount',
    'Scenario 2 Variance',
    'Scenario 3 Amount',
    'Scenario 3 Include?',
    'Scenario 3 New Amount',
    'Scenario 3 Variance',
  ];
  const detailRows = () => rows.map((row) => [
    row.description,
    row.transactionType,
    row.actualTransaction ?? '',
    row.percentage ? 'Yes' : 'No',
    row.baseAmount,
    row.amount1,
    row.include1 ? 'Yes' : 'No',
    row.newAmount1,
    row.variance1,
    row.amount2,
    row.include2 ? 'Yes' : 'No',
    row.newAmount2,
    row.variance2,
    row.amount3,
    row.include3 ? 'Yes' : 'No',
    row.newAmount3,
    row.variance3,
  ]);
  const exportExcel = () => {
    if (rows.length === 0) {
      return;
    }

    const workbook = XLSX.utils.book_new();
    const detailSheet = XLSX.utils.aoa_to_sheet([detailHeaders, ...detailRows()]);
    detailSheet['!cols'] = [
      { wch: 32 },
      { wch: 8 },
      { wch: 8 },
      { wch: 11 },
      { wch: 14 },
      { wch: 18 },
      { wch: 18 },
      { wch: 20 },
      { wch: 19 },
      { wch: 18 },
      { wch: 18 },
      { wch: 20 },
      { wch: 19 },
      { wch: 18 },
      { wch: 18 },
      { wch: 20 },
      { wch: 19 },
    ];
    const criteriaSheet = XLSX.utils.aoa_to_sheet(criteriaRows());
    criteriaSheet['!cols'] = [{ wch: 18 }, { wch: 24 }];

    XLSX.utils.book_append_sheet(workbook, detailSheet, 'Budget Analysis');
    XLSX.utils.book_append_sheet(workbook, criteriaSheet, 'Criteria');
    XLSX.writeFile(workbook, `REP3_033-${analysis?.payPeriod ?? payPeriod}.xlsx`);
  };
  const escapeHtml = (value: string | number | boolean | null | undefined) => String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
  const printAnalysis = () => {
    if (rows.length === 0) {
      return;
    }

    const printWindow = window.open('', '_blank', 'width=1200,height=800');
    if (!printWindow) {
      toast({ title: 'Budget Analysis', description: 'Allow pop-ups to print the budget analysis report.', variant: 'destructive' });
      return;
    }

    const rowHtml = detailRows()
      .map((row) => `<tr>${row.map((cell, index) => `<td class="${index >= 4 ? 'num' : ''}">${escapeHtml(cell)}</td>`).join('')}</tr>`)
      .join('');
    const criteriaHtml = criteriaRows()
      .map(([label, value]) => `<span><strong>${escapeHtml(label)}</strong>: ${escapeHtml(value)}</span>`)
      .join('');

    printWindow.document.open();
    printWindow.document.write(`<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(analysis?.reportName ?? 'Budget Analysis Report')}</title>
  <style>
    @page { size: A4 landscape; margin: 8mm; }
    * { box-sizing: border-box; }
    body { font-family: Arial, sans-serif; margin: 0; color: #111827; font-size: 7pt; }
    h1 { margin: 0 0 4px; font-size: 13pt; }
    .criteria { display: flex; flex-wrap: wrap; gap: 4px 12px; margin-bottom: 8px; font-size: 8pt; }
    table { width: 100%; border-collapse: collapse; table-layout: fixed; }
    th, td { border: 1px solid #9ca3af; padding: 2px 3px; vertical-align: middle; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    th { background: #f3f4f6; font-weight: 700; white-space: normal; line-height: 1.1; }
    th:nth-child(6), td:nth-child(6), th:nth-child(10), td:nth-child(10), th:nth-child(14), td:nth-child(14) { border-left: 2px solid #374151; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
  </style>
</head>
<body>
  <h1>${escapeHtml(analysis?.reportName ?? 'Budget Analysis Report')}</h1>
  <div class="criteria">${criteriaHtml}</div>
  <table>
    <thead><tr>${detailHeaders.map((header, index) => `<th class="${index >= 4 ? 'num' : ''}">${escapeHtml(header)}</th>`).join('')}</tr></thead>
    <tbody>${rowHtml}</tbody>
  </table>
</body>
</html>`);
    printWindow.document.close();
    printWindow.focus();
    window.setTimeout(() => {
      printWindow.print();
    }, 250);
  };
  const periodOptionLabel = (period: number) => {
    const periodRun = runs.find((run) => run.payPeriod === period);
    if (periodRun) {
      return formatPayrollPeriodLabel(periodRun.payPeriodFrom, periodRun.payPeriodTo, period);
    }

    if (period === defaultPayPeriod) {
      return formatPayrollPeriodLabel(defaultPayPeriodFrom, defaultPayPeriodTo, period);
    }

    return formatPayrollPeriodLabel(null, null, period);
  };
  const selectedPeriodLabel = analysis
    ? formatPayrollPeriodLabel(analysis.payPeriodFrom, analysis.payPeriodTo, analysis.payPeriod || payPeriod)
    : periodOptionLabel(payPeriod);
  const gridInputClassName = 'h-6 w-full min-w-0 rounded-sm border-0 bg-transparent px-1 text-right text-[11px] shadow-none focus-visible:ring-1';
  const renderAnalysisSummary = (compact = false) => analysis ? (
    <div className={`grid gap-2 ${compact ? 'md:grid-cols-5' : 'md:grid-cols-5'}`}>
      <div className="rounded-md border bg-muted/20 p-2">
        <div className="text-[11px] text-muted-foreground">Period</div>
        <div className="text-xs font-medium">{selectedPeriodLabel}</div>
        {periodLine ? <div className="truncate text-[11px] text-muted-foreground">{periodLine}</div> : null}
      </div>
      <div className="rounded-md border bg-muted/20 p-2">
        <div className="text-[11px] text-muted-foreground">Base Amount</div>
        <div className="truncate text-xs font-medium tabular-nums" title={formatAmount(totals.totalBaseAmount)}>{formatAmount(totals.totalBaseAmount)}</div>
      </div>
      {[1, 2, 3].map((scenario) => {
        const totalNew = scenario === 1 ? totals.totalNewAmount1 : scenario === 2 ? totals.totalNewAmount2 : totals.totalNewAmount3;
        const totalVariance = scenario === 1 ? totals.totalVariance1 : scenario === 2 ? totals.totalVariance2 : totals.totalVariance3;

        return (
          <div key={scenario} className="rounded-md border bg-muted/20 p-2">
            <div className="text-[11px] text-muted-foreground">Scenario {scenario}</div>
            <div className="truncate text-xs font-medium tabular-nums" title={formatAmount(totalNew)}>{formatAmount(totalNew)}</div>
            <div className="truncate text-[11px] text-muted-foreground" title={formatAmount(totalVariance)}>Variance {formatAmount(totalVariance)}</div>
          </div>
        );
      })}
    </div>
  ) : null;

  const renderAnalysisTable = () => (
    <div className="h-full min-h-0 overflow-y-auto overflow-x-hidden rounded-md border">
      <Table className="w-full table-fixed text-[11px]">
        <colgroup>
          <col className="w-[15%]" />
          <col className="w-[5%]" />
          <col className="w-[8%]" />
          <col className="w-[5%]" />
          <col className="w-[5%]" />
          <col className="w-[7%]" />
          <col className="w-[7%]" />
          <col className="w-[5%]" />
          <col className="w-[5%]" />
          <col className="w-[7%]" />
          <col className="w-[7%]" />
          <col className="w-[5%]" />
          <col className="w-[5%]" />
          <col className="w-[7%]" />
          <col className="w-[7%]" />
        </colgroup>
        <TableHeader>
          <TableRow>
            <TableHead className="sticky top-0 z-10 h-8 whitespace-normal bg-background px-2 py-1 text-[10px] font-bold leading-tight text-foreground">Description</TableHead>
            <TableHead className="sticky top-0 z-10 h-8 whitespace-normal bg-background px-1 py-1 text-center text-[10px] font-bold leading-tight text-foreground">Percentage?</TableHead>
            <TableHead className="sticky top-0 z-10 h-8 whitespace-normal bg-background px-1 py-1 text-right text-[10px] font-bold leading-tight text-foreground">Base Amount</TableHead>
            {[1, 2, 3].map((scenario) => (
              <Fragment key={scenario}>
                <TableHead className="sticky top-0 z-10 h-8 whitespace-normal border-l-2 border-slate-500 bg-background px-1 py-1 text-right text-[10px] font-bold leading-tight text-foreground">Scenario {scenario} Amount</TableHead>
                <TableHead className="sticky top-0 z-10 h-8 whitespace-normal bg-background px-0.5 py-1 text-center text-[10px] font-bold leading-tight text-foreground">Include?</TableHead>
                <TableHead className="sticky top-0 z-10 h-8 whitespace-normal bg-background px-1 py-1 text-right text-[10px] font-bold leading-tight text-foreground">New Amount</TableHead>
                <TableHead className="sticky top-0 z-10 h-8 whitespace-normal bg-background px-1 py-1 text-right text-[10px] font-bold leading-tight text-foreground">Variance</TableHead>
              </Fragment>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={15} className="py-8 text-center text-sm text-muted-foreground">
                No budget rows for this period.
              </TableCell>
            </TableRow>
          ) : rows.map((row, index) => (
            <TableRow key={budgetAnalysisRowKey(row, index)}>
              <TableCell className="px-2 py-1 align-middle">
                <div className="truncate font-medium" title={row.description}>{row.description}</div>
                <div className="truncate text-[10px] text-muted-foreground" title={`${row.transactionType}${row.actualTransaction ? ` / ${row.actualTransaction}` : ''}`}>
                  {row.transactionType}{row.actualTransaction ? ` / ${row.actualTransaction}` : ''}
                </div>
              </TableCell>
              <TableCell className="px-1 py-1 text-center">{row.percentage ? 'Yes' : 'No'}</TableCell>
              <TableCell className="truncate px-1 py-1 text-right tabular-nums" title={formatAmount(row.baseAmount)}>{formatAmount(row.baseAmount)}</TableCell>
              {[1, 2, 3].map((scenario) => {
                const amount = scenario === 1 ? row.amount1 : scenario === 2 ? row.amount2 : row.amount3;
                const include = scenario === 1 ? row.include1 : scenario === 2 ? row.include2 : row.include3;
                const newAmount = scenario === 1 ? row.newAmount1 : scenario === 2 ? row.newAmount2 : row.newAmount3;
                const variance = scenario === 1 ? row.variance1 : scenario === 2 ? row.variance2 : row.variance3;

                return (
                  <Fragment key={scenario}>
                    <TableCell className="border-l-2 border-slate-500 px-1 py-1">
                      <Input className={gridInputClassName} type="number" step="0.01" value={amount} onChange={(event) => updateScenarioAmount(index, scenario as BudgetScenario, Number(event.target.value) || 0)} />
                    </TableCell>
                    <TableCell className="px-0.5 py-1 text-center">
                      <Checkbox className="h-3.5 w-3.5" checked={include} onCheckedChange={(checked) => updateScenarioInclude(index, scenario as BudgetScenario, checked === true)} />
                    </TableCell>
                    <TableCell className="truncate px-1 py-1 text-right tabular-nums" title={formatAmount(newAmount)}>{formatAmount(newAmount)}</TableCell>
                    <TableCell className="truncate px-1 py-1 text-right tabular-nums" title={formatAmount(variance)}>{formatAmount(variance)}</TableCell>
                  </Fragment>
                );
              })}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );

  return (
    <>
    <Card className="max-w-full">
      <CardHeader className="border-b px-4 py-3">
        <PayrollFormTitle menuId="A0000131">Budget Analysis</PayrollFormTitle>
      </CardHeader>
      <CardContent className="p-4">
        <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); void generateAnalysis(true); }}>
          <div className="grid gap-3 md:grid-cols-[minmax(220px,280px)_repeat(3,120px)_auto] md:items-end">
            <Field label="Pay Period">
              <select
                className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                value={payPeriod || ''}
                onChange={(event) => handlePayPeriodChange(Number(event.target.value) || 0)}
                disabled={loading || loadingPeriodRows}
              >
                {loadingRuns && payPeriodSelectOptions.length === 0 ? <option value="">Loading periods</option> : null}
                {payPeriodSelectOptions.map((option) => (
                  <option key={option} value={option}>
                    {periodOptionLabel(option)}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Basic (%) 1">
              <Input type="number" step="0.01" value={basicPercent1} onChange={(event) => updateBasicPercent(1, Number(event.target.value) || 0)} disabled={loadingPeriodRows} />
            </Field>
            <Field label="Basic (%) 2">
              <Input type="number" step="0.01" value={basicPercent2} onChange={(event) => updateBasicPercent(2, Number(event.target.value) || 0)} disabled={loadingPeriodRows} />
            </Field>
            <Field label="Basic (%) 3">
              <Input type="number" step="0.01" value={basicPercent3} onChange={(event) => updateBasicPercent(3, Number(event.target.value) || 0)} disabled={loadingPeriodRows} />
            </Field>
            <div className="flex items-end gap-2">
              <Button type="submit" disabled={loading || loadingPeriodRows}>
                {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Calculator className="mr-2 h-4 w-4" />}
                Generate
              </Button>
              {rows.length > 0 ? (
                <Button type="button" variant="outline" onClick={() => void loadPeriodDetails(payPeriod)} disabled={loading || loadingPeriodRows}>
                  {loadingPeriodRows ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Search className="mr-2 h-4 w-4" />}
                  Refresh
                </Button>
              ) : null}
            </div>
          </div>

          {error ? <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</div> : null}

          {analysis ? renderAnalysisSummary(true) : null}

          {analysis ? <div className="flex flex-wrap gap-3">
            <Button type="button" onClick={() => setAnalysisOpen(true)}>
              Open Full Page
            </Button>
            <Button type="button" onClick={() => void saveAnalysis()} disabled={rows.length === 0 || saving || loadingPeriodRows}>
              {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save
            </Button>
            <Button type="button" variant="outline" onClick={printAnalysis} disabled={rows.length === 0}>
              <Printer className="mr-2 h-4 w-4" />
              Print
            </Button>
            <Button type="button" variant="outline" onClick={exportExcel} disabled={rows.length === 0}>
              <FileSpreadsheet className="mr-2 h-4 w-4" />
              Excel
            </Button>
          </div> : null}

          {loadingPeriodRows ? (
            <div className="rounded-md border bg-muted/20 px-3 py-8 text-center text-sm text-muted-foreground">
              Loading budget analysis details for {periodOptionLabel(payPeriod)}...
            </div>
          ) : analysis ? (
            <div className="h-[52vh] min-h-[340px]">
              {renderAnalysisTable()}
            </div>
          ) : null}
        </form>
      </CardContent>
    </Card>

    <Dialog open={analysisOpen} onOpenChange={setAnalysisOpen}>
      <DialogContent className="flex h-screen w-screen max-w-none flex-col gap-0 overflow-hidden rounded-none p-0">
        <DialogHeader className="border-b px-4 py-2">
          <div className="flex flex-wrap items-center justify-between gap-3 pr-8">
            <div>
              <DialogTitle>Budget Analysis</DialogTitle>
              {periodLine ? <div className="mt-1 text-xs text-muted-foreground">{periodLine}</div> : null}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button type="button" size="sm" onClick={() => void saveAnalysis()} disabled={rows.length === 0 || saving || loadingPeriodRows}>
                {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Save
              </Button>
              <Button type="button" variant="outline" size="sm" onClick={printAnalysis} disabled={rows.length === 0}>
                <Printer className="mr-2 h-4 w-4" />
                Print
              </Button>
              <Button type="button" variant="outline" size="sm" onClick={exportExcel} disabled={rows.length === 0}>
                <FileSpreadsheet className="mr-2 h-4 w-4" />
                Excel
              </Button>
            </div>
          </div>
        </DialogHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
          {analysis ? renderAnalysisSummary(false) : null}
          <div className="min-h-0 flex-1">
            {analysis ? renderAnalysisTable() : null}
          </div>
        </div>
      </DialogContent>
    </Dialog>
    </>
  );
}

function TaxTableForm({
  table,
  busy,
  onSaveRows,
}: {
  table: PayrollTaxTable;
  busy: string | null;
  onSaveRows: (rows: PayrollTaxBand[]) => void;
}) {
  const [selectedTaxType, setSelectedTaxType] = useState(taxTableTypeOptions[0]);
  const [rows, setRows] = useState<TaxTableGridRow[]>(() => buildTaxTableRows(table.taxBands, taxTableTypeOptions[0]));
  const [taxableIncome, setTaxableIncome] = useState('');
  const [taxAmount, setTaxAmount] = useState('');
  const [netSalary, setNetSalary] = useState('');
  const [basicSalary, setBasicSalary] = useState('');

  useEffect(() => {
    setRows(buildTaxTableRows(table.taxBands, selectedTaxType));
  }, [table.taxBands, selectedTaxType]);

  const updateRow = (index: number, patch: Partial<TaxTableGridRow>, taxManuallyEdited = false) => {
    setRows((current) =>
      recalculateTaxTableRows(
        current.map((row, rowIndex) => (rowIndex === index ? { ...row, ...patch, taxManuallyEdited } : row)),
        selectedTaxType,
      ),
    );
  };

  const saveRows = () => {
    onSaveRows(
      rows.map((row, index) => {
        const { label, taxManuallyEdited, ...taxBandRow } = row;

        return {
          ...defaultTaxBand,
          ...taxBandRow,
          taxType: selectedTaxType,
          serialNo: index + 1,
          description: label,
          lowerBound: Number(row.taxableIncome ?? row.lowerBound ?? 0) || 0,
          taxableIncome: Number(row.taxableIncome ?? row.lowerBound ?? 0) || 0,
          fixedAmount: Number(row.perMonthAmount ?? row.fixedAmount ?? 0) || 0,
          perMonthAmount: Number(row.perMonthAmount ?? row.fixedAmount ?? 0) || 0,
          cumulativeTax: Number(row.cumulativeTax ?? 0) || 0,
          cumulativeSalary: Number(row.cumulativeSalary ?? 0) || 0,
          upperBound: null,
          payPeriod: row.payPeriod ?? currentPayPeriod,
          payPeriodFrom: dateValue(row.payPeriodFrom) || today,
          payPeriodTo: dateValue(row.payPeriodTo) || today,
          effectiveFrom: dateValue(row.effectiveFrom) || today,
          effectiveTo: nullableDateValue(row.effectiveTo),
          legacyCompanyCode: row.legacyCompanyCode || '001',
          isAnnual: false,
          isActive: true,
        };
      }),
    );
  };

  const runCalcTax = () => {
    const income = Number(taxableIncome) || 0;
    setTaxAmount(calculateTaxFromRows(rows, income).toFixed(2));
  };

  const runCalcIncome = () => {
    const income = estimateIncomeFromTaxRows(rows, Number(taxAmount) || 0);
    setTaxableIncome(income.toFixed(2));
  };

  const runCalcNet = () => {
    const basic = Number(basicSalary || taxableIncome) || 0;
    const tax = calculateTaxFromRows(rows, basic);
    setBasicSalary(basic.toFixed(2));
    setTaxableIncome(basic.toFixed(2));
    setTaxAmount(tax.toFixed(2));
    setNetSalary(roundMoney(basic - tax).toFixed(2));
  };

  const runCalcBasic = () => {
    const targetNet = Number(netSalary) || 0;
    if (targetNet <= 0) {
      setBasicSalary('0.00');
      return;
    }

    let low = targetNet;
    let high = targetNet * 2;
    while (roundMoney(high - calculateTaxFromRows(rows, high)) < targetNet) {
      high *= 2;
    }

    for (let index = 0; index < 40; index += 1) {
      const mid = (low + high) / 2;
      if (roundMoney(mid - calculateTaxFromRows(rows, mid)) < targetNet) {
        low = mid;
      } else {
        high = mid;
      }
    }

    const basic = roundMoney(high);
    const tax = calculateTaxFromRows(rows, basic);
    setBasicSalary(basic.toFixed(2));
    setTaxableIncome(basic.toFixed(2));
    setTaxAmount(tax.toFixed(2));
  };

  const numberInputNoSpinnerClassName = '[appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none';
  const gridInputClassName = `h-7 rounded-none border-0 bg-transparent px-2 text-right shadow-none focus-visible:ring-1 ${numberInputNoSpinnerClassName}`;
  const calculatorInputClassName = `h-8 rounded-sm ${numberInputNoSpinnerClassName}`;
  const columnLabels = selectedTaxType === 'Overtime' ? taxTableColumnLabels.Overtime : taxTableColumnLabels.Tax;

  return (
    <Card className="max-w-6xl">
      <CardHeader className="pb-2">
        <PayrollFormTitle menuId="A0000115">Tax Table</PayrollFormTitle>
      </CardHeader>
      <CardContent>
        <form
          className="space-y-5"
          onSubmit={(event) => {
            event.preventDefault();
            saveRows();
          }}
        >
          <Tabs value={selectedTaxType} onValueChange={setSelectedTaxType} className="space-y-4">
            <div className="flex justify-center">
              <TabsList className="h-8">
                {taxTableTypeOptions.map((option) => (
                  <TabsTrigger key={option} value={option} className="h-7 min-w-28">
                    {option}
                  </TabsTrigger>
                ))}
              </TabsList>
            </div>

            {taxTableTypeOptions.map((option) => (
              <TabsContent key={option} value={option} className="mt-0">
                <div className="overflow-x-auto">
                  <Table className="min-w-[760px] border-separate border-spacing-0">
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-20 border-0 bg-transparent"></TableHead>
                        <TableHead className="border px-2 text-center">{columnLabels.baseAmount}</TableHead>
                        <TableHead className="border px-2 text-center">Percentage</TableHead>
                        <TableHead className="border px-2 text-center">{columnLabels.calculatedTax}</TableHead>
                        <TableHead className="border px-2 text-center">Cumulative Tax</TableHead>
                        <TableHead className="border px-2 text-center">{columnLabels.cumulativeBase}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rows.map((row, index) => (
                        <TableRow key={row.id || `${selectedTaxType}-${index}`}>
                          <TableCell className="border-0 bg-transparent px-2 py-1 text-sm">{row.label}</TableCell>
                          <TableCell className="border p-0">
                            <Input
                              className={gridInputClassName}
                              type="number"
                              step="0.01"
                              value={row.taxableIncome ?? ''}
                              onChange={(event) => {
                                const value = numberOrNull(event.target.value);
                                updateRow(index, { taxableIncome: value, lowerBound: value ?? 0 }, false);
                              }}
                            />
                          </TableCell>
                          <TableCell className="border p-0">
                            <Input
                              className={gridInputClassName}
                              type="number"
                              step="0.01"
                              value={row.ratePercent}
                              onChange={(event) => updateRow(index, { ratePercent: Number(event.target.value) }, false)}
                            />
                          </TableCell>
                          <TableCell className="border p-0">
                            <Input
                              className={gridInputClassName}
                              type="number"
                              step="0.01"
                              value={row.perMonthAmount ?? ''}
                              onChange={(event) => {
                                const value = numberOrNull(event.target.value);
                                updateRow(index, { perMonthAmount: value, fixedAmount: value ?? 0 }, true);
                              }}
                            />
                          </TableCell>
                          <TableCell className="border p-0">
                            <Input
                              className={gridInputClassName}
                              type="number"
                              step="0.01"
                              value={row.cumulativeTax ?? ''}
                              onChange={(event) => updateRow(index, { cumulativeTax: numberOrNull(event.target.value) }, true)}
                            />
                          </TableCell>
                          <TableCell className="border p-0">
                            <Input
                              className={gridInputClassName}
                              type="number"
                              step="0.01"
                              value={row.cumulativeSalary ?? ''}
                              onChange={(event) => updateRow(index, { cumulativeSalary: numberOrNull(event.target.value) }, true)}
                            />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </TabsContent>
            ))}
          </Tabs>

          <div className="grid gap-6 lg:grid-cols-[170px_1fr_170px_1fr] lg:items-start">
            <div className="space-y-2">
              <Button type="button" variant="outline" className="w-full rounded-sm" onClick={runCalcTax}>
                Calculate Tax
              </Button>
              <Button type="button" variant="outline" className="w-full rounded-sm" onClick={runCalcIncome}>
                Calculate Income
              </Button>
            </div>
            <div className="space-y-2">
              <OracleFormRow label="Taxable Income">
                <Input className={calculatorInputClassName} type="number" step="0.01" value={taxableIncome} onChange={(event) => setTaxableIncome(event.target.value)} />
              </OracleFormRow>
              <OracleFormRow label="Tax">
                <Input className={calculatorInputClassName} type="number" step="0.01" value={taxAmount} onChange={(event) => setTaxAmount(event.target.value)} />
              </OracleFormRow>
            </div>
            <div className="space-y-2 lg:row-start-2">
              <Button type="button" variant="outline" className="w-full rounded-sm" onClick={runCalcNet}>
                Calculate Net
              </Button>
              <Button type="button" variant="outline" className="w-full rounded-sm" onClick={runCalcBasic}>
                Calculate Basic
              </Button>
            </div>
            <div className="space-y-2 lg:row-start-2">
              <OracleFormRow label="Net Salary">
                <Input className={calculatorInputClassName} type="number" step="0.01" value={netSalary} onChange={(event) => setNetSalary(event.target.value)} />
              </OracleFormRow>
              <OracleFormRow label="Basic Salary">
                <Input className={calculatorInputClassName} type="number" step="0.01" value={basicSalary} onChange={(event) => setBasicSalary(event.target.value)} />
              </OracleFormRow>
            </div>
          </div>

          <Button type="submit" disabled={busy === 'Tax table'}>
            {busy === 'Tax table' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save Tax Table
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function MenuTracker({ selectedMenu }: { selectedMenu: PayrollLegacyMenuItem | null }) {
  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <FileText className="h-4 w-4" />
          {selectedMenu?.caption || 'Menu Item'}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Table>
          <TableBody>
            <TableRow>
              <TableCell className="w-40 text-muted-foreground">Menu Id</TableCell>
              <TableCell>{selectedMenu?.menuId || '-'}</TableCell>
            </TableRow>
            <TableRow>
              <TableCell className="text-muted-foreground">Target</TableCell>
              <TableCell>{selectedMenu?.target || selectedMenu?.itemType || '-'}</TableCell>
            </TableRow>
            <TableRow>
              <TableCell className="text-muted-foreground">Status</TableCell>
              <TableCell>
                {selectedMenu && implementedMenuIds.has(selectedMenu.menuId) ? (
                  <Badge>Implemented</Badge>
                ) : (
                  <Badge variant="outline">Mapped</Badge>
                )}
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
