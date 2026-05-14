'use client';

import type { ReactNode } from 'react';
import { useEffect, useMemo, useState } from 'react';
import * as XLSX from 'xlsx';
import {
  ArrowLeft,
  ChevronDown,
  ChevronRight,
  Download,
  FileSpreadsheet,
  FileText,
  Loader2,
  Play,
  Printer,
  Search,
} from 'lucide-react';

import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollOracleReport,
  PayrollOracleReportColumn,
  PayrollOracleReportRequest,
  PayrollOracleReportRow,
  PayrollRun,
  payrollService,
} from '@/services/payrollService';

type HrReportDefinition = {
  code: string;
  menuId: string;
  label: string;
  sourceForm: string;
  categoryId: string;
  subgroupId?: string;
  variantLabel: string;
  variants: Array<{
    value: string;
    label: string;
    sourceReport: string;
  }>;
  showDepartmentEmployeeRange: boolean;
  showBankBranchRange: boolean;
  showLocationRange?: boolean;
  showAnalysisCriteria?: boolean;
  showComponentRange?: boolean;
  showEmployerBankCriteria?: boolean;
  showBonusType?: boolean;
  showLoanCriteria?: boolean;
};

type HrReportCategory = {
  id: string;
  label: string;
};

type HrReportSubgroup = {
  id: string;
  categoryId: string;
  label: string;
};

const categories: HrReportCategory[] = [
  { id: 'payroll', label: 'Payroll Reports' },
];

const reportSubgroups: HrReportSubgroup[] = [
  { id: 'bonus', categoryId: 'payroll', label: 'Bonus Reports' },
];

const reports: HrReportDefinition[] = [
  {
    code: 'REP3_001',
    menuId: 'A0000401',
    label: 'Payroll Register Report',
    sourceForm: 'REP3_001.fmb',
    categoryId: 'payroll',
    variantLabel: 'By',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    variants: [
      { value: 'A', label: 'All', sourceReport: 'REP3_001_ALL.RDF' },
    ],
  },
  {
    code: 'REP3_035',
    menuId: 'A0000402',
    label: 'Bank Advice',
    sourceForm: 'BANK_ADV.fmb',
    categoryId: 'payroll',
    variantLabel: 'By',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    variants: [
      { value: 'BNK', label: 'Employee Bank', sourceReport: 'REP3_004.RDF' },
      { value: 'BR1', label: 'Employee Bank Branch', sourceReport: 'REP3_005_GBC1.RDF' },
      { value: 'BSU', label: 'Bank Summary', sourceReport: 'BANK_SUM.RDF' },
      { value: 'BRA', label: 'Branch Summary', sourceReport: 'REP3_005_GBC.RDF' },
      { value: 'REG', label: 'Regional Summary', sourceReport: 'REP3_020.RDF' },
      { value: 'ARR', label: 'Arreas Advice', sourceReport: 'REP3_005_ARR.RDF' },
    ],
  },
  {
    code: 'REP3_006',
    menuId: 'A0000405',
    label: 'Monthly PAYE Report',
    sourceForm: 'REP3_006.fmb',
    categoryId: 'payroll',
    variantLabel: 'REPTYPE',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    variants: [
      { value: 'MPR', label: 'Monthly Payee', sourceReport: 'REP3_006_PWC.rdf' },
      { value: 'GRA', label: 'GRA Monthly Payee', sourceReport: 'REP3_006_GRA.RDF' },
    ],
  },
  {
    code: 'REP3_002',
    menuId: 'A0000406',
    label: 'Payroll Analysis - Month',
    sourceForm: 'REP3_002.fmb',
    categoryId: 'payroll',
    variantLabel: 'BY_TYPE',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    showAnalysisCriteria: true,
    variants: [
      { value: 'DEP', label: 'Department', sourceReport: 'ANALYSIS_DEPT.rdf' },
      { value: 'REG', label: 'Region', sourceReport: 'ANALYSIS_REG.RDF' },
      { value: 'IND', label: 'Individual', sourceReport: 'ANALYSIS_INDV.RDF' },
    ],
  },
  {
    code: 'REP3_009',
    menuId: 'A0000407',
    label: 'Bank Advice (Employer Bank)',
    sourceForm: 'REP3_009.fmb',
    categoryId: 'payroll',
    variantLabel: 'BANK_REP_TYPE',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    showEmployerBankCriteria: true,
    variants: [
      { value: 'CC', label: 'Bank Advice(Cedi to Cedi)', sourceReport: 'REP3_009_PHC.RDF' },
      { value: 'CD', label: 'Bank Advice(Cedi to Dollar)', sourceReport: 'REP3_009_GSLTF_USD.RDF' },
      { value: 'DC', label: 'Bank Advice(Dollar to Cedi)', sourceReport: 'REP3_009_USD_CEDI.RDF' },
      { value: 'DD', label: 'Bank Advice(Dollar to Dollar)', sourceReport: 'REP3_009_DOL_DOL.RDF' },
    ],
  },
  {
    code: 'REP3_010',
    menuId: 'A0000408',
    label: 'Allowances & Deductions Sch',
    sourceForm: 'REP3_010.fmb',
    categoryId: 'payroll',
    variantLabel: 'ALL_DED',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    showComponentRange: true,
    variants: [
      { value: 'ALL', label: 'All', sourceReport: 'REP3_010_SLTF.rdf' },
      { value: 'ALW', label: 'Allowances', sourceReport: 'REP3_010_SLTF.rdf' },
      { value: 'DED', label: 'Deductions', sourceReport: 'REP3_010_SLTF.rdf' },
      { value: 'ADV', label: 'Advance', sourceReport: 'REP3_010_SLTF.rdf' },
      { value: 'REP', label: 'Loan Repayment', sourceReport: 'REP3_010_SLTF.rdf' },
      { value: 'INT', label: 'Loan Interest', sourceReport: 'REP3_010_SLTF.rdf' },
    ],
  },
  {
    code: 'REP3_011',
    menuId: 'A0000410',
    label: 'Cash List',
    sourceForm: 'REP3_011.fmb',
    categoryId: 'payroll',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    variants: [
      { value: 'CASH', label: 'Cash List', sourceReport: 'CASHLIST.RDF' },
    ],
  },
  {
    code: 'REP3_007',
    menuId: 'A0000404',
    label: 'SSF Report',
    sourceForm: 'REP3_007.fmb',
    categoryId: 'payroll',
    variantLabel: 'SSF Report',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    variants: [
      { value: 'REP', label: 'SSF Report', sourceReport: 'REP3_007.rdf' },
      { value: 'REP1', label: '1st Tier', sourceReport: 'REP3_007_1.rdf' },
      { value: 'REP2', label: '2nd Tier', sourceReport: 'REP3_007_2.rdf' },
      { value: 'REP1A', label: '1st Tier New', sourceReport: 'REP3_007_1A.rdf' },
      { value: 'REP3', label: 'Tier3 Contribution', sourceReport: 'REP3_007_3.RDF' },
      { value: 'FB', label: 'Format B', sourceReport: 'REP3_001_SSF.RDF' },
    ],
  },
  {
    code: 'REP3_019',
    menuId: 'A0000412',
    label: 'Overtime Reports',
    sourceForm: 'REP3_019.fmb',
    categoryId: 'payroll',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    variants: [
      { value: 'OT', label: 'Overtime Reports', sourceReport: 'REP3_019.RDF' },
    ],
  },
  {
    code: 'REP3_034',
    menuId: 'A0000418',
    label: 'Annual Tax Returns',
    sourceForm: 'REP3_034.fmb',
    categoryId: 'payroll',
    variantLabel: 'Report Type',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    variants: [
      { value: 'O', label: 'Income Tax Deduction Form', sourceReport: 'REP3_034.RDF' },
      { value: 'A', label: 'Annual Employee Return', sourceReport: 'REP3_034_A.RDF' },
      { value: 'R', label: 'Annual Tax Register', sourceReport: 'REP3_034_R.RDF' },
      { value: 'P', label: 'Employee Tax Certificate', sourceReport: 'REP3_034_P.RDF' },
      { value: 'L', label: 'Annual Tax by Location', sourceReport: 'REP3_034_LOC.rdf' },
    ],
  },
  {
    code: 'REP3_305',
    menuId: 'A0000420',
    label: 'Staff Lists',
    sourceForm: 'REP3_305.fmb',
    categoryId: 'payroll',
    variantLabel: 'Staff List',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    variants: [
      { value: 'DEP', label: 'Department', sourceReport: 'REP3_305.RDF' },
      { value: 'CAT', label: 'Category', sourceReport: 'REP3_305_A.RDF' },
      { value: 'GEN', label: 'Gender', sourceReport: 'REP3_305_B.RDF' },
    ],
  },
  {
    code: 'REP3_036',
    menuId: 'A0000423',
    label: 'Loan Statement',
    sourceForm: 'REP3_036.fmb',
    categoryId: 'payroll',
    variantLabel: 'Category',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    showLoanCriteria: true,
    variants: [
      { value: 'I', label: 'Individual Statement', sourceReport: 'REP3_036.RDF' },
      { value: 'F', label: 'Facility Statement', sourceReport: 'REP3_036_A.RDF' },
      { value: 'T', label: 'Loan Type Statement', sourceReport: 'REP3_036_B.RDF' },
      { value: 'D', label: 'Employees Loan Report', sourceReport: 'REP3_036_DETAILS.RDF' },
    ],
  },
  {
    code: 'REP3_029',
    menuId: 'A0000415',
    label: 'Bonus Register',
    sourceForm: 'REP3_029.fmb',
    categoryId: 'payroll',
    subgroupId: 'bonus',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    showBonusType: true,
    variants: [
      { value: 'REG', label: 'Bonus Register', sourceReport: 'BONUS_REGISTER.rdf' },
    ],
  },
  {
    code: 'REP3_028',
    menuId: 'A000041402',
    label: 'Bonus Slip',
    sourceForm: 'REP3_028.fmb',
    categoryId: 'payroll',
    subgroupId: 'bonus',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    showBonusType: true,
    variants: [
      { value: 'SLIP', label: 'Bonus Slip', sourceReport: 'REP3_028.RDF' },
    ],
  },
  {
    code: 'REP3_030',
    menuId: 'A000041403',
    label: 'Bonus Tax Report',
    sourceForm: 'REP3_030.fmb',
    categoryId: 'payroll',
    subgroupId: 'bonus',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    showBonusType: true,
    variants: [
      { value: 'TAX', label: 'Bonus Tax Report', sourceReport: 'REP3_030.RDF' },
    ],
  },
  {
    code: 'REP3_031',
    menuId: 'A000041404',
    label: 'Bonus Bank Advice',
    sourceForm: 'REP3_031.fmb',
    categoryId: 'payroll',
    subgroupId: 'bonus',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: false,
    showBankBranchRange: false,
    showEmployerBankCriteria: true,
    showBonusType: true,
    variants: [
      { value: 'BANK', label: 'Bonus Bank Advice', sourceReport: 'REP3_031.RDF' },
    ],
  },
  {
    code: 'REP3_032',
    menuId: 'A000041405',
    label: 'Bonus Cash List',
    sourceForm: 'REP3_032.fmb',
    categoryId: 'payroll',
    subgroupId: 'bonus',
    variantLabel: 'Report',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    showLocationRange: true,
    showBonusType: true,
    variants: [
      { value: 'CASH', label: 'Bonus Cash List', sourceReport: 'REP3_032.RDF' },
    ],
  },
  {
    code: 'REP3_016',
    menuId: 'A0000411',
    label: 'Journal Reports',
    sourceForm: 'REP3_016.fmb',
    categoryId: 'payroll',
    variantLabel: 'Select',
    showDepartmentEmployeeRange: true,
    showBankBranchRange: false,
    variants: [
      { value: 'S', label: 'Summary Journal Report', sourceReport: 'JOUNAL_REP.RDF' },
      { value: 'D', label: 'Detail Journal Report', sourceReport: 'JOUNAL_DETAIL.rdf' },
    ],
  },
];

const defaultCriteria = {
  departmentFrom: '0',
  departmentTo: 'ZZZZZ',
  employeeNumberFrom: '0',
  employeeNumberTo: 'ZZZZZ',
  bankFrom: '0',
  bankTo: 'ZZZZZ',
  branchFrom: '0',
  branchTo: 'ZZZZZ',
  locationFrom: '0',
  locationTo: 'ZZZZZ',
  regionCode: 'ALL',
  departmentCode: 'ALL',
  componentCodeFrom: '0',
  componentCodeTo: 'ZZZZZ',
  accountNumber: '',
  signer1: '',
  signer2: '',
  signer3: '',
  position1: '',
  position2: '',
  position3: '',
  reportingCurrency: '',
  loanType: '',
  facilityNumber: '',
};

type ReportCriteriaState = typeof defaultCriteria & { variantCode: string };

const reportLookup = new Map(reports.map((report) => [report.code, report]));

function formatDate(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

function formatPeriodMonthYear(run?: PayrollRun | null) {
  if (!run) {
    return '-';
  }

  const date = new Date(run.payPeriodTo || run.payPeriodFrom);
  if (Number.isNaN(date.getTime())) {
    return String(run.payPeriod);
  }

  return date.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
}

function formatPeriodYear(run?: PayrollRun | null) {
  if (!run) {
    return '-';
  }

  const date = new Date(run.payPeriodTo || run.payPeriodFrom);
  if (Number.isNaN(date.getTime())) {
    return String(run.payPeriod);
  }

  return String(date.getFullYear());
}

function getReportTitle(report: PayrollOracleReport) {
  if (report.reportCode === 'REP3_001') {
    return `PAYROLL REGISTER FOR : ${report.periodLabel}`;
  }

  if (report.reportCode === 'REP3_006' && report.variantCode === 'MPR') {
    return 'MONTHLY PAYE DEDUCTIONS';
  }

  if (report.reportCode === 'REP3_006' && report.variantCode === 'GRA') {
    return `STAFF INCOME TAX CONTRIBUTION FOR : ${report.periodLabel.toUpperCase()}`;
  }

  if (report.reportCode === 'REP3_007') {
    return `EMPLOYEE SSF CONTRIBUTION FOR : ${report.periodLabel.toUpperCase()}`;
  }

  if (report.reportCode === 'REP3_002') {
    if (report.variantCode === 'IND') {
      return 'Current Vrs Previous Month Analysis - Payroll';
    }

    return report.variantCode === 'REG' ? 'Analysis By Region' : 'Analysis By Department';
  }

  if (report.reportCode === 'REP3_010') {
    return 'List of Allowances / Deductions';
  }

  if (report.reportCode === 'REP3_011') {
    return 'CASH LIST';
  }

  if (report.reportCode === 'REP3_019') {
    return 'OVERTIME REPORTS';
  }

  if (report.reportCode === 'REP3_034') {
    return `ANNUAL TAX RETURNS FOR : ${report.periodLabel}`;
  }

  if (report.reportCode === 'REP3_305') {
    return 'STAFF ON ROLL';
  }

  if (report.reportCode === 'REP3_036') {
    return report.variantCode === 'D' ? 'EMPLOYEES LOAN REPORT' : 'LOAN STATEMENT';
  }

  if (report.reportCode === 'REP3_029') {
    return 'BONUS REGISTER';
  }

  if (report.reportCode === 'REP3_028') {
    return 'BONUS PAYSLIPS';
  }

  if (report.reportCode === 'REP3_030') {
    return 'BONUS TAX REPORT';
  }

  if (report.reportCode === 'REP3_031') {
    return 'Bank Advice';
  }

  if (report.reportCode === 'REP3_032') {
    return 'CASH LIST';
  }

  if (report.reportCode === 'REP3_016') {
    return report.variantName;
  }

  return report.reportName;
}

function getReportPageCount(report: PayrollOracleReport) {
  return Math.max(1, Math.ceil(report.totalRows / 28));
}

function showReportVariant(report: PayrollOracleReport) {
  return !['REP3_001', 'REP3_006', 'REP3_007', 'REP3_016'].includes(report.reportCode) &&
    report.variantName &&
    report.variantName !== report.reportName;
}

function usesOracleTwoFieldCriteria(reportCode: string) {
  return reportCode === 'REP3_001' || reportCode === 'REP3_035';
}

function usesOracleRangeCriteria(reportCode: string) {
  return ['REP3_006', 'REP3_007', 'REP3_011', 'REP3_016', 'REP3_019', 'REP3_028', 'REP3_029', 'REP3_032', 'REP3_034', 'REP3_036'].includes(reportCode);
}

function getPeriodCriteriaLabel(reportCode: string) {
  if (reportCode === 'REP3_034') {
    return 'Tax Year';
  }

  return reportCode === 'REP3_001' ? 'Period' : 'Pay Period';
}

function usesReportingCurrencyCriteria(reportCode: string) {
  return ['REP3_007', 'REP3_009', 'REP3_010', 'REP3_031'].includes(reportCode);
}

function getReportPeriodLine(report: PayrollOracleReport) {
  if (report.reportCode === 'REP3_016') {
    return `FOR THE MONTH OF: ${report.periodLabel}`;
  }

  if (report.reportCode === 'REP3_034') {
    return '';
  }

  if (report.reportCode === 'REP3_006' && report.variantCode === 'GRA') {
    return '';
  }

  if (report.reportCode === 'REP3_007') {
    return '';
  }

  return report.reportCode !== 'REP3_001' ? report.periodLabel : '';
}

function getReportNote(report: PayrollOracleReport) {
  if (report.reportCode === 'REP3_006' && report.variantCode === 'GRA') {
    return 'NOTE: THE OVERTIME (COLUMN 22) HAS BEEN INCLUDED IN THE CASH ALLOWANCE SINCE ALL STAFF WHO EARN THE OVERTIME AMOUNT ARE ABOVE THE THRESHHOLD ELIGIBLE FOR OVERTIME FINAL TAX. THE AMOUNT HAS APPEARED ON THE TEMPLATE UNDER COLUMN 22 FOR PRESENTATION PURPOSES.';
  }

  return '';
}

function isBankAdviceLetterReport(report: PayrollOracleReport) {
  return report.reportCode === 'REP3_035' && ['BNK', 'BR1', 'BRA', 'REG', 'ARR'].includes(report.variantCode);
}

function formatAmount(value: number) {
  return new Intl.NumberFormat('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
}

function formatWholeNumber(value: number) {
  return new Intl.NumberFormat('en-GH', { maximumFractionDigits: 0 }).format(value);
}

function reportCellText(value: string | number | boolean | null | undefined, column?: PayrollOracleReportColumn) {
  if (typeof value === 'number') {
    if (column?.valueType === 'currency') {
      return formatAmount(value);
    }

    if (column?.valueType === 'number') {
      return formatWholeNumber(value);
    }

    return String(value);
  }

  if (typeof value === 'boolean') {
    return value ? 'Yes' : 'No';
  }

  return value == null || value === '' ? '' : String(value);
}

function rowText(row: PayrollOracleReportRow, key: string) {
  const value = row.values[key];
  return value == null || value === '' ? '' : String(value);
}

function rowAmount(row: PayrollOracleReportRow, key: string) {
  const value = row.values[key];
  return typeof value === 'number' ? value : Number(value || 0);
}

function formatBankAdviceDate(value?: string | null) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const month = date.toLocaleDateString('en-US', { month: 'short' }).toUpperCase();
  return `${month}-${String(date.getDate()).padStart(2, '0')}-${date.getFullYear()}`;
}

function downloadText(content: string, fileName: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

function safeFileName(value: string) {
  return value.replace(/[^a-z0-9_.-]+/gi, '-').replace(/^-+|-+$/g, '') || 'report';
}

function escapeCsv(value: string) {
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}

function buildCsv(report: PayrollOracleReport) {
  const rows = [
    report.columns.map((column) => escapeCsv(column.header)).join(','),
    ...report.rows.map((row) =>
      report.columns
        .map((column) => escapeCsv(reportCellText(row.values[column.key], column)))
        .join(',')),
  ];

  return rows.join('\r\n');
}

function getVariant(report: HrReportDefinition, value: string) {
  return report.variants.find((variant) => variant.value === value) ?? report.variants[0];
}

function buildExcelWorkbook(report: PayrollOracleReport) {
  const data = [
    report.columns.map((column) => column.header),
    ...report.rows.map((row) =>
      report.columns.map((column) => {
        const value = row.values[column.key];
        return typeof value === 'number' ? value : reportCellText(value, column);
      })),
  ];
  const worksheet = XLSX.utils.aoa_to_sheet(data);

  worksheet['!cols'] = report.columns.map((column) => ({
    wch: Math.min(Math.max(column.header.length + 2, 12), column.valueType === 'currency' ? 18 : 36),
  }));

  report.rows.forEach((row, rowIndex) => {
    report.columns.forEach((column, columnIndex) => {
      const value = row.values[column.key];
      if (typeof value !== 'number') {
        return;
      }

      const cellAddress = XLSX.utils.encode_cell({ r: rowIndex + 1, c: columnIndex });
      const cell = worksheet[cellAddress];
      if (cell) {
        cell.z = column.valueType === 'number' ? '#,##0' : '#,##0.00';
      }
    });
  });

  const workbook = XLSX.utils.book_new();
  const sheetName = safeFileName(report.sourceReport.replace(/\.[^.]+$/, '')).slice(0, 31) || 'Report';
  XLSX.utils.book_append_sheet(workbook, worksheet, sheetName);
  return workbook;
}

function CriteriaField({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1">
      <Label className="text-[11px] font-medium uppercase tracking-normal text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

function OracleRangeCriteriaFields({
  criteria,
  inputClass,
  setCriteria,
  showLocationRange = false,
}: {
  criteria: ReportCriteriaState;
  inputClass: string;
  setCriteria: (updater: (current: ReportCriteriaState) => ReportCriteriaState) => void;
  showLocationRange?: boolean;
}) {
  return (
    <>
      <div className="md:col-span-4 grid gap-3 md:grid-cols-4">
        <CriteriaField label="Department Code">
          <Input
            className={inputClass}
            value={criteria.departmentFrom}
            onChange={(event) => setCriteria((current) => ({ ...current, departmentFrom: event.target.value }))}
          />
        </CriteriaField>
        <CriteriaField label="Department Description">
          <Input className={inputClass} value="" disabled />
        </CriteriaField>
        <CriteriaField label="Department Code">
          <Input
            className={inputClass}
            value={criteria.departmentTo}
            onChange={(event) => setCriteria((current) => ({ ...current, departmentTo: event.target.value }))}
          />
        </CriteriaField>
        <CriteriaField label="Department Description">
          <Input className={inputClass} value="" disabled />
        </CriteriaField>
      </div>

      {showLocationRange && (
        <div className="md:col-span-4 grid gap-3 md:grid-cols-4">
          <CriteriaField label="Location Code">
            <Input
              className={inputClass}
              value={criteria.locationFrom}
              onChange={(event) => setCriteria((current) => ({ ...current, locationFrom: event.target.value }))}
            />
          </CriteriaField>
          <CriteriaField label="Location Description">
            <Input className={inputClass} value="" disabled />
          </CriteriaField>
          <CriteriaField label="Location Code">
            <Input
              className={inputClass}
              value={criteria.locationTo}
              onChange={(event) => setCriteria((current) => ({ ...current, locationTo: event.target.value }))}
            />
          </CriteriaField>
          <CriteriaField label="Location Description">
            <Input className={inputClass} value="" disabled />
          </CriteriaField>
        </div>
      )}

      <div className="md:col-span-4 grid gap-3 md:grid-cols-4">
        <CriteriaField label="Employee Number">
          <Input
            className={inputClass}
            value={criteria.employeeNumberFrom}
            onChange={(event) => setCriteria((current) => ({ ...current, employeeNumberFrom: event.target.value }))}
          />
        </CriteriaField>
        <CriteriaField label="Employee Name">
          <Input className={inputClass} value="" disabled />
        </CriteriaField>
        <CriteriaField label="Employee Number">
          <Input
            className={inputClass}
            value={criteria.employeeNumberTo}
            onChange={(event) => setCriteria((current) => ({ ...current, employeeNumberTo: event.target.value }))}
          />
        </CriteriaField>
        <CriteriaField label="Employee Name">
          <Input className={inputClass} value="" disabled />
        </CriteriaField>
      </div>
    </>
  );
}

function ReportTable({ report }: { report: PayrollOracleReport }) {
  return (
    <div className="overflow-auto border border-slate-400">
      <table className="w-full border-collapse bg-white text-[11px] leading-tight text-slate-950">
        <thead>
          <tr>
            {report.columns.map((column) => (
              <th
                key={column.key}
                className={`whitespace-normal border border-slate-400 bg-slate-100 px-1.5 py-1 align-bottom font-semibold leading-tight ${column.alignment === 'right' ? 'text-right' : 'text-left'}`}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {report.rows.map((row: PayrollOracleReportRow, rowIndex) => {
            const isTotalRow = rowText(row, 'oracleRowType') === 'TOTAL';

            return (
              <tr key={`${report.sourceReport}-${rowIndex}`} className={isTotalRow ? 'font-semibold' : undefined}>
                {report.columns.map((column) => (
                  <td
                    key={column.key}
                    className={`whitespace-nowrap border border-slate-300 px-1.5 py-0.5 ${isTotalRow ? 'border-t-slate-950' : ''} ${column.alignment === 'right' ? 'text-right tabular-nums' : 'text-left'}`}
                  >
                    {reportCellText(row.values[column.key], column)}
                  </td>
                ))}
              </tr>
            );
          })}
          {report.rows.length === 0 && (
            <tr>
              <td className="border border-slate-300 px-2 py-8 text-center text-slate-500" colSpan={Math.max(report.columns.length, 1)}>
                No rows
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

function BankAdviceLetterReport({ report }: { report: PayrollOracleReport }) {
  if (report.rows.length === 0) {
    return (
      <div className="border border-slate-300 px-2 py-8 text-center text-slate-500">
        No rows
      </div>
    );
  }

  const groups = Array.from(
    report.rows.reduce((map, row) => {
      const bankName = rowText(row, 'bankName') || 'BANK';
      const bankBranch = rowText(row, 'bankBranch');
      const key = `${bankName}||${bankBranch}`;
      const existing = map.get(key);
      if (existing) {
        existing.rows.push(row);
      } else {
        map.set(key, { bankName, bankBranch, rows: [row] });
      }

      return map;
    }, new Map<string, { bankName: string; bankBranch: string; rows: PayrollOracleReportRow[] }>()),
  ).map(([, group]) => group);
  const companyName = rowText(report.rows[0], 'companyName') || report.companyCode;
  const companyAddress = rowText(report.rows[0], 'companyAddress');
  const companyPhone = rowText(report.rows[0], 'companyPhone');
  const dateLabel = formatBankAdviceDate(report.generatedAt);
  const netColumnLabel = report.variantCode === 'BNK' ? 'Net' : 'Net PAY';
  const accountColumnLabel = report.variantCode === 'BNK' ? 'Acct No' : 'Account No';

  return (
    <div className="space-y-8 font-mono text-[12px] leading-6 text-slate-950">
      {groups.map((group) => {
        const total = group.rows.reduce((sum, row) => sum + rowAmount(row, 'net'), 0);

        return (
          <section key={`${group.bankName}-${group.bankBranch}`} className="mx-auto max-w-[760px] break-inside-avoid">
            <div className="font-semibold uppercase">{companyName}</div>
            {(companyAddress || companyPhone) && (
              <div>{companyAddress}{companyPhone ? `, TEL:${companyPhone}` : ''}</div>
            )}

            <div className="mt-8">{dateLabel}</div>
            <div>The Manager</div>
            <div className="font-semibold uppercase">{group.bankName}</div>
            {group.bankBranch && <div className="font-semibold uppercase">{group.bankBranch}</div>}

            <div className="mt-8">Dear Sir,</div>
            <div className="mt-2 text-center font-semibold underline">Staff Salary Payment</div>

            <div className="mt-4">
              <p>We shall be grateful if you could pay salaries of the underlisted staff members</p>
              <p>of our company into their individual accounts with your bank from account number</p>
              <p>.................... We attach cheque number ............ for the total amount of</p>
              <p>....................</p>
              <p>Counting on your usual cooperation.</p>
            </div>

            <div className="mt-2">
              <div className="grid grid-cols-[110px_minmax(220px,1fr)_160px_110px] gap-3 font-semibold">
                <div className="underline">Staff ID</div>
                <div className="underline">Name</div>
                <div className="underline">{accountColumnLabel}</div>
                <div className="text-right underline">{netColumnLabel}</div>
              </div>
              {group.rows.map((row, index) => (
                <div key={`${rowText(row, 'staffId')}-${index}`} className="grid grid-cols-[110px_minmax(220px,1fr)_160px_110px] gap-3">
                  <div>{rowText(row, 'staffId')}</div>
                  <div>{rowText(row, 'name')}</div>
                  <div>{rowText(row, 'acctNo')}</div>
                  <div className="text-right tabular-nums">{formatAmount(rowAmount(row, 'net'))}</div>
                </div>
              ))}
              <div className="mt-1 grid grid-cols-[110px_minmax(220px,1fr)_160px_110px] gap-3 font-semibold">
                <div />
                <div>TOTAL</div>
                <div />
                <div className="border-y border-slate-950 py-0.5 text-right tabular-nums">{formatAmount(total)}</div>
              </div>
            </div>

            <div className="mt-5">Yours Faithfully</div>
          </section>
        );
      })}
    </div>
  );
}

export default function HrReportsPage() {
  const { toast } = useToast();
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(
    () => new Set(categories.map((category) => category.id)),
  );
  const [expandedSubgroups, setExpandedSubgroups] = useState<Set<string>>(
    () => new Set(reportSubgroups.map((group) => group.id)),
  );
  const [selectedReportCode, setSelectedReportCode] = useState(reports[0].code);
  const [selectedRunId, setSelectedRunId] = useState('');
  const [criteria, setCriteria] = useState({
    ...defaultCriteria,
    variantCode: reports[0].variants[0].value,
  });
  const [report, setReport] = useState<PayrollOracleReport | null>(null);

  const selectedReport = reportLookup.get(selectedReportCode) ?? reports[0];
  const selectedVariant = getVariant(selectedReport, criteria.variantCode);
  const selectedRun = runs.find((run) => run.id === selectedRunId) ?? null;
  const selectedCurrency = selectedRun?.currencyCode || 'GHS';
  const selectedCategory = categories.find((category) => category.id === selectedReport.categoryId);
  const selectedSubgroup = selectedReport.subgroupId
    ? reportSubgroups.find((group) => group.id === selectedReport.subgroupId)
    : null;

  useEffect(() => {
    let mounted = true;

    async function loadRuns() {
      setLoading(true);
      setError(null);
      try {
        const runList = await payrollService.getRuns();
        if (!mounted) {
          return;
        }

        setRuns(runList);
        setSelectedRunId((current) => current || runList.find((run) => run.status !== 'Draft')?.id || runList[0]?.id || '');
      } catch (err) {
        if (!mounted) {
          return;
        }

        const message = err instanceof Error ? err.message : 'Unable to load payroll runs.';
        setError(message);
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    }

    void loadRuns();
    return () => {
      mounted = false;
    };
  }, []);

  const filteredCategories = useMemo(() => {
    const term = search.trim().toLowerCase();

    return categories
      .map((category) => {
        const categoryReports = reports.filter((item) => {
          if (item.categoryId !== category.id) {
            return false;
          }

          if (!term) {
            return true;
          }

          const subgroup = item.subgroupId
            ? reportSubgroups.find((group) => group.id === item.subgroupId)
            : null;

          return [
            item.label,
            item.code,
            item.menuId,
            item.sourceForm,
            subgroup?.label ?? '',
            ...item.variants.map((variant) => variant.sourceReport),
          ].some((value) => value.toLowerCase().includes(term));
        });
        const directReports = categoryReports.filter((item) => !item.subgroupId);
        const subgroups = reportSubgroups
          .filter((group) => group.categoryId === category.id)
          .map((group) => ({
            ...group,
            reports: categoryReports.filter((item) => item.subgroupId === group.id),
          }))
          .filter((group) => group.reports.length > 0);

        return {
          ...category,
          directReports,
          subgroups,
          reportCount: directReports.length + subgroups.reduce((sum, group) => sum + group.reports.length, 0),
        };
      })
      .filter((category) => category.reportCount > 0);
  }, [search]);

  const selectReport = (nextReport: HrReportDefinition) => {
    setSelectedReportCode(nextReport.code);
    setCriteria({
      ...defaultCriteria,
      componentCodeFrom: nextReport.showBonusType ? '' : defaultCriteria.componentCodeFrom,
      variantCode: nextReport.variants[0].value,
    });
    setReport(null);
  };

  const toggleCategory = (categoryId: string) => {
    setExpandedCategories((current) => {
      const next = new Set(current);
      if (next.has(categoryId)) {
        next.delete(categoryId);
      } else {
        next.add(categoryId);
      }

      return next;
    });
  };

  const toggleSubgroup = (subgroupId: string) => {
    setExpandedSubgroups((current) => {
      const next = new Set(current);
      if (next.has(subgroupId)) {
        next.delete(subgroupId);
      } else {
        next.add(subgroupId);
      }

      return next;
    });
  };

  const runReport = async () => {
    if (!selectedRunId) {
      toast({ title: 'Select payroll run', description: 'Choose PAY_PERIOD before running the report.' });
      return;
    }

    setRunning(true);
    setError(null);
    try {
      const payload: PayrollOracleReportRequest = {
        reportCode: selectedReport.code,
        variantCode: selectedVariant.value,
        departmentFrom: selectedReport.showDepartmentEmployeeRange ? criteria.departmentFrom : undefined,
        departmentTo: selectedReport.showDepartmentEmployeeRange ? criteria.departmentTo : undefined,
        employeeNumberFrom: selectedReport.showDepartmentEmployeeRange ? criteria.employeeNumberFrom : undefined,
        employeeNumberTo: selectedReport.showDepartmentEmployeeRange ? criteria.employeeNumberTo : undefined,
        locationFrom: selectedReport.showLocationRange ? criteria.locationFrom : undefined,
        locationTo: selectedReport.showLocationRange ? criteria.locationTo : undefined,
        regionCode: selectedReport.showAnalysisCriteria ? criteria.regionCode : undefined,
        departmentCode: selectedReport.showAnalysisCriteria ? criteria.departmentCode : undefined,
        componentCodeFrom: selectedReport.showComponentRange || selectedReport.showBonusType ? criteria.componentCodeFrom : undefined,
        componentCodeTo: selectedReport.showComponentRange ? criteria.componentCodeTo : undefined,
        bankFrom: selectedReport.showBankBranchRange ? criteria.bankFrom : undefined,
        bankTo: selectedReport.showBankBranchRange ? criteria.bankTo : undefined,
        branchFrom: selectedReport.showBankBranchRange ? criteria.branchFrom : undefined,
        branchTo: selectedReport.showBankBranchRange ? criteria.branchTo : undefined,
        accountNumber: selectedReport.showEmployerBankCriteria ? criteria.accountNumber : undefined,
        signer1: selectedReport.showEmployerBankCriteria ? criteria.signer1 : undefined,
        signer2: selectedReport.showEmployerBankCriteria ? criteria.signer2 : undefined,
        signer3: selectedReport.showEmployerBankCriteria ? criteria.signer3 : undefined,
        position1: selectedReport.showEmployerBankCriteria ? criteria.position1 : undefined,
        position2: selectedReport.showEmployerBankCriteria ? criteria.position2 : undefined,
        position3: selectedReport.showEmployerBankCriteria ? criteria.position3 : undefined,
        loanType: selectedReport.showLoanCriteria ? criteria.loanType : undefined,
        facilityNumber: selectedReport.showLoanCriteria ? criteria.facilityNumber : undefined,
        loanReportCategory: selectedReport.showLoanCriteria ? selectedVariant.value : undefined,
        reportingCurrency: criteria.reportingCurrency || selectedCurrency,
      };
      if (selectedReport.showEmployerBankCriteria) {
        payload.bankFrom = criteria.bankFrom;
        payload.bankTo = criteria.bankFrom;
        payload.branchFrom = criteria.branchFrom;
        payload.branchTo = criteria.branchFrom;
      }

      const result = await payrollService.getOracleRunReport(selectedRunId, payload);
      setReport(result);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to run HR report.';
      setError(message);
      toast({ title: 'Report failed', description: message, variant: 'destructive' });
    } finally {
      setRunning(false);
    }
  };

  const exportCsv = () => {
    if (!report) {
      return;
    }

    downloadText(buildCsv(report), `${safeFileName(`${report.sourceReport}-${report.runNumber}`)}.csv`, 'text/csv;charset=utf-8;');
  };

  const exportExcel = () => {
    if (!report) {
      return;
    }

    XLSX.writeFile(buildExcelWorkbook(report), `${safeFileName(`${report.sourceReport}-${report.runNumber}`)}.xlsx`);
  };

  const printReport = () => {
    window.print();
  };

  const criteriaInputClass = 'h-8 rounded-sm text-xs';
  const periodCriteria = (
    <CriteriaField label={getPeriodCriteriaLabel(selectedReport.code)}>
      <select
        className="h-8 w-full rounded-sm border bg-background px-2 text-xs"
        value={selectedRunId}
        disabled={loading}
        onChange={(event) => {
          setSelectedRunId(event.target.value);
          setReport(null);
        }}
      >
        {runs.map((run) => (
          <option key={run.id} value={run.id}>
            {selectedReport.code === 'REP3_034' ? formatPeriodYear(run) : formatPeriodMonthYear(run)}
          </option>
        ))}
      </select>
    </CriteriaField>
  );
  const variantCriteria = (
    <CriteriaField label={selectedReport.variantLabel}>
      <select
        className="h-8 w-full rounded-sm border bg-background px-2 text-xs"
        value={selectedVariant.value}
        onChange={(event) => {
          setCriteria((current) => ({ ...current, variantCode: event.target.value }));
          setReport(null);
        }}
      >
        {selectedReport.variants.map((variant) => (
          <option key={variant.value} value={variant.value}>
            {variant.label}
          </option>
        ))}
      </select>
    </CriteriaField>
  );

  const content = report ? (
    <div className="space-y-3">
      <style>{`
        @media print {
          @page {
            size: landscape;
            margin: 8mm;
          }
          body * {
            visibility: hidden !important;
          }
          .hr-report-print-area,
          .hr-report-print-area * {
            visibility: visible !important;
          }
          .hr-report-print-area {
            position: absolute !important;
            left: 0 !important;
            top: 0 !important;
            width: 100% !important;
            background: #fff !important;
          }
          .hr-report-no-print {
            display: none !important;
          }
        }
      `}</style>
      <div className="hr-report-no-print flex flex-wrap items-center justify-between gap-2 border-b bg-background pb-3">
        <div className="flex flex-wrap items-center gap-2">
          <Button size="sm" variant="outline" onClick={() => setReport(null)}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Criteria
          </Button>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button size="sm" variant="outline" onClick={exportCsv}>
            <Download className="mr-2 h-4 w-4" />
            CSV
          </Button>
          <Button size="sm" variant="outline" onClick={exportExcel}>
            <FileSpreadsheet className="mr-2 h-4 w-4" />
            Excel
          </Button>
          <Button size="sm" onClick={printReport}>
            <Printer className="mr-2 h-4 w-4" />
            Print
          </Button>
        </div>
      </div>

      <section className="hr-report-print-area bg-white p-3 text-slate-950">
        {!isBankAdviceLetterReport(report) && (
          <div className="mb-3 grid grid-cols-[1fr_auto_1fr] items-start gap-3 text-[11px]">
            <div />
            <div className="text-center">
              <h1 className="text-base font-bold uppercase tracking-normal">{getReportTitle(report)}</h1>
              {showReportVariant(report) && <div className="font-semibold">{report.variantName}</div>}
              {getReportPeriodLine(report) && <div>{getReportPeriodLine(report)}</div>}
            </div>
            <div className="space-y-1 text-right">
              <div>Run Date : {formatDate(report.generatedAt)}</div>
              <div>Page No&nbsp;&nbsp; : 1 of {getReportPageCount(report)}</div>
            </div>
          </div>
        )}

        {isBankAdviceLetterReport(report) ? (
          <BankAdviceLetterReport report={report} />
        ) : (
          <>
            <ReportTable report={report} />
            {getReportNote(report) && <div className="mt-3 text-[11px] font-semibold">{getReportNote(report)}</div>}
          </>
        )}
      </section>
    </div>
  ) : (
    <div className="grid min-h-[calc(100vh-150px)] gap-4 lg:grid-cols-[280px_minmax(0,1fr)]">
      <aside className="space-y-4 border-r pr-4">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="h-9 rounded-sm pl-9 text-sm"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="search..."
          />
        </div>

        <div className="space-y-3 text-sm">
          {filteredCategories.map((category) => {
            const expanded = expandedCategories.has(category.id);

            return (
              <div key={category.id}>
                <button
                  type="button"
                  className="flex w-full items-center gap-1.5 text-left font-medium"
                  onClick={() => toggleCategory(category.id)}
                >
                  {expanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
                  {category.label}
                </button>
                {expanded && (
                  <div className="mt-2 space-y-1 pl-8">
                    {category.directReports.map((item) => (
                      <button
                        key={item.code}
                        type="button"
                        className={`flex w-full items-center gap-2 rounded-sm px-1 py-1 text-left text-sm text-primary hover:bg-muted ${item.code === selectedReport.code ? 'bg-muted font-medium' : ''}`}
                        onClick={() => selectReport(item)}
                      >
                        <FileText className="h-4 w-4 text-foreground" />
                        <span>{item.label}</span>
                      </button>
                    ))}
                    {category.subgroups.map((group) => {
                      const subgroupExpanded = expandedSubgroups.has(group.id);

                      return (
                        <div key={group.id}>
                          <button
                            type="button"
                            className="flex w-full items-center gap-1.5 rounded-sm px-1 py-1 text-left text-sm font-medium text-foreground hover:bg-muted"
                            onClick={() => toggleSubgroup(group.id)}
                          >
                            {subgroupExpanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
                            {group.label}
                          </button>
                          {subgroupExpanded && (
                            <div className="mt-1 space-y-1 pl-6">
                              {group.reports.map((item) => (
                                <button
                                  key={item.code}
                                  type="button"
                                  className={`flex w-full items-center gap-2 rounded-sm px-1 py-1 text-left text-sm text-primary hover:bg-muted ${item.code === selectedReport.code ? 'bg-muted font-medium' : ''}`}
                                  onClick={() => selectReport(item)}
                                >
                                  <FileText className="h-4 w-4 text-foreground" />
                                  <span>{item.label}</span>
                                </button>
                              ))}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </aside>

      <section className="space-y-4">
        <div className="border-b pb-3">
          <h1 className="text-xl font-semibold tracking-normal">HR Reports</h1>
          <div className="mt-1 text-sm text-muted-foreground">
            Reports / HR Reports / {selectedCategory?.label ?? 'Payroll Reports'}{selectedSubgroup ? ` / ${selectedSubgroup.label}` : ''} / {selectedReport.label}
          </div>
        </div>

        {error && <div className="rounded-sm border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</div>}

        <div className={`${usesOracleTwoFieldCriteria(selectedReport.code) ? 'max-w-lg' : 'max-w-5xl'} border bg-background p-4`}>
          <div className="mb-4 border-b pb-3">
            <div>
              <h2 className="text-base font-semibold">{selectedReport.label}</h2>
            </div>
          </div>

          <div className={`grid gap-3 ${usesOracleTwoFieldCriteria(selectedReport.code) ? 'md:grid-cols-2' : 'md:grid-cols-4'}`}>
            {usesOracleRangeCriteria(selectedReport.code) ? (
              <>
                <OracleRangeCriteriaFields
                  criteria={criteria}
                  inputClass={criteriaInputClass}
                  setCriteria={setCriteria}
                  showLocationRange={selectedReport.showLocationRange}
                />
                {periodCriteria}
                {variantCriteria}
              </>
            ) : usesOracleTwoFieldCriteria(selectedReport.code) ? (
              <>
                {variantCriteria}
                {periodCriteria}
              </>
            ) : (
              <>
                {periodCriteria}
                {variantCriteria}
              </>
            )}

            {selectedReport.showAnalysisCriteria && (
              <>
                <CriteriaField label="P_REGION">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.regionCode}
                    onChange={(event) => setCriteria((current) => ({ ...current, regionCode: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_DEPARTMENT">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.departmentCode}
                    onChange={(event) => setCriteria((current) => ({ ...current, departmentCode: event.target.value }))}
                  />
                </CriteriaField>
              </>
            )}

            {selectedReport.showDepartmentEmployeeRange && !usesOracleRangeCriteria(selectedReport.code) && (
              <>
                <CriteriaField label="Department From">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.departmentFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, departmentFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="Department To">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.departmentTo}
                    onChange={(event) => setCriteria((current) => ({ ...current, departmentTo: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="Employee No From">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.employeeNumberFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, employeeNumberFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="Employee No To">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.employeeNumberTo}
                    onChange={(event) => setCriteria((current) => ({ ...current, employeeNumberTo: event.target.value }))}
                  />
                </CriteriaField>
              </>
            )}

            {selectedReport.showComponentRange && (
              <>
                <CriteriaField label="P_ALW_FM">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.componentCodeFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, componentCodeFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_ALW_TO">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.componentCodeTo}
                    onChange={(event) => setCriteria((current) => ({ ...current, componentCodeTo: event.target.value }))}
                  />
                </CriteriaField>
              </>
            )}

            {selectedReport.showBonusType && (
              <CriteriaField label="P_BONUS_TYPE">
                <Input
                  className={criteriaInputClass}
                  value={criteria.componentCodeFrom}
                  onChange={(event) => setCriteria((current) => ({ ...current, componentCodeFrom: event.target.value }))}
                />
              </CriteriaField>
            )}

            {selectedReport.showLoanCriteria && (
              <>
                <CriteriaField label="P_LOANTYPE">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.loanType}
                    onChange={(event) => setCriteria((current) => ({ ...current, loanType: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_FACILITYNO">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.facilityNumber}
                    onChange={(event) => setCriteria((current) => ({ ...current, facilityNumber: event.target.value }))}
                  />
                </CriteriaField>
              </>
            )}

            {selectedReport.showEmployerBankCriteria && (
              <>
                <CriteriaField label="P_BANK_CODE">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.bankFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, bankFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_BRANCH_CODE">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.branchFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, branchFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_ACCT_NO">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.accountNumber}
                    onChange={(event) => setCriteria((current) => ({ ...current, accountNumber: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_SIG1">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.signer1}
                    onChange={(event) => setCriteria((current) => ({ ...current, signer1: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_PSN1">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.position1}
                    onChange={(event) => setCriteria((current) => ({ ...current, position1: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_SIG2">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.signer2}
                    onChange={(event) => setCriteria((current) => ({ ...current, signer2: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_PSN2">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.position2}
                    onChange={(event) => setCriteria((current) => ({ ...current, position2: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_SIG3">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.signer3}
                    onChange={(event) => setCriteria((current) => ({ ...current, signer3: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="P_PSN3">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.position3}
                    onChange={(event) => setCriteria((current) => ({ ...current, position3: event.target.value }))}
                  />
                </CriteriaField>
              </>
            )}

            {selectedReport.showBankBranchRange && (
              <>
                <CriteriaField label="Bank From">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.bankFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, bankFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="Bank To">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.bankTo}
                    onChange={(event) => setCriteria((current) => ({ ...current, bankTo: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="Branch From">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.branchFrom}
                    onChange={(event) => setCriteria((current) => ({ ...current, branchFrom: event.target.value }))}
                  />
                </CriteriaField>
                <CriteriaField label="Branch To">
                  <Input
                    className={criteriaInputClass}
                    value={criteria.branchTo}
                    onChange={(event) => setCriteria((current) => ({ ...current, branchTo: event.target.value }))}
                  />
                </CriteriaField>
              </>
            )}

            {usesReportingCurrencyCriteria(selectedReport.code) && (
              <CriteriaField label="P_REP_CURRENCY">
                <Input
                  className={criteriaInputClass}
                  value={criteria.reportingCurrency}
                  onChange={(event) => setCriteria((current) => ({ ...current, reportingCurrency: event.target.value }))}
                  placeholder={selectedCurrency}
                />
              </CriteriaField>
            )}
          </div>
          <div className={`mt-4 flex ${usesOracleTwoFieldCriteria(selectedReport.code) ? 'justify-center' : 'justify-start'}`}>
            <Button size="sm" disabled={running || loading || !selectedRunId} onClick={() => void runReport()}>
              {running ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Play className="mr-2 h-4 w-4" />}
              Run Report
            </Button>
          </div>
        </div>
      </section>
    </div>
  );

  return (
    <TenantGuard>
      <DashboardLayout>{content}</DashboardLayout>
    </TenantGuard>
  );
}
