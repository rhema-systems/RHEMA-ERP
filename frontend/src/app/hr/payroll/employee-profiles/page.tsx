'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  Loader2,
  Mail,
  Plus,
  Printer,
  RefreshCw,
  Save,
  Search,
  Trash2,
  UserPlus,
  Users,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { PayrollPayslipPreviewDialog } from '@/components/hr/payroll/PayrollPayslipPreviewDialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import {
  HrEmployee,
  PayrollBankBranch,
  PayrollCodeValue,
  PayrollEmployeeProfile,
  PayrollExchangeRate,
  PayrollParameterSet,
  PayrollPayslip,
  PayrollPayslipEmailResult,
  PayrollPaymentMethod,
  PayrollRun,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);
const paymentMethodOptions = ['Bank', 'Cash', 'Cheque'] as const;

type PaymentMethodForm = {
  id?: string;
  paymentType: string;
  paymentMode: 'Percentage' | 'FixedAmount';
  paymentPercent: number;
  amount: number;
  bankCode: string;
  bankBranchCode: string;
  accountNumber: string;
  chequeNumber: string;
  chequeBankCode: string;
  currencyCode: string;
  exchangeRate: number;
  sequenceNo: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
};

const createDefaultPaymentMethod = (currencyCode = 'GHS', sequenceNo = 1): PaymentMethodForm => ({
  paymentType: 'Bank',
  paymentMode: 'Percentage',
  paymentPercent: 100,
  amount: 0,
  bankCode: '',
  bankBranchCode: '',
  accountNumber: '',
  chequeNumber: '',
  chequeBankCode: '',
  currencyCode,
  exchangeRate: 1,
  sequenceNo,
  startDate: '',
  endDate: '',
  isActive: true,
});

const createDefaultProfileForm = (currencyCode = 'GHS') => ({
  id: '',
  employeeId: '',
  employeeNumber: '',
  legacyEmployeeId: '',
  legacyEmployeeNumber: '',
  ssfNumber: '',
  monthlyBasicSalary: 0,
  currencyCode,
  payrollActive: true,
  payTax: true,
  ssfApplicable: true,
  overtimeEligible: false,
  grossUp: false,
  tier2Only: false,
  paymentMethods: [createDefaultPaymentMethod(currencyCode)],
});

const money = (value: number | null | undefined, currency = 'GHS') => {
  try {
    return new Intl.NumberFormat('en-GH', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value ?? 0);
  } catch {
    return `${currency} ${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value ?? 0)}`;
  }
};

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function normalizePaymentMode(method: Partial<PayrollPaymentMethod>): 'Percentage' | 'FixedAmount' {
  const mode = String(method.paymentMode || '').replace(/\s+/g, '').toLowerCase();
  if (mode === 'fixedamount' || mode === 'amount' || mode === 'a') {
    return 'FixedAmount';
  }

  if (!method.paymentPercent && Number(method.amount || 0) > 0) {
    return 'FixedAmount';
  }

  return 'Percentage';
}

function toPaymentMethodForm(method: PayrollPaymentMethod, index: number, fallbackCurrency: string): PaymentMethodForm {
  const paymentMode = normalizePaymentMode(method);
  return {
    id: method.id,
    paymentType: method.paymentType || 'Bank',
    paymentMode,
    paymentPercent: paymentMode === 'Percentage' ? Number(method.paymentPercent ?? 0) : 0,
    amount: paymentMode === 'FixedAmount' ? Number(method.amount ?? 0) : 0,
    bankCode: method.bankCode || '',
    bankBranchCode: method.bankBranchCode || '',
    accountNumber: method.accountNumber || '',
    chequeNumber: method.chequeNumber || '',
    chequeBankCode: method.chequeBankCode || '',
    currencyCode: method.currencyCode || fallbackCurrency,
    exchangeRate: Number(method.exchangeRate ?? 1) || 1,
    sequenceNo: method.sequenceNo || index + 1,
    startDate: dateValue(method.startDate),
    endDate: dateValue(method.endDate),
    isActive: method.isActive !== false,
  };
}

function formatPayrollPeriodLabel(from?: string | null, to?: string | null) {
  const source = dateValue(to) || dateValue(from);
  if (!source) {
    return 'Not configured';
  }

  const [year, month] = source.split('-').map(Number);
  if (!year || !month) {
    return 'Not configured';
  }

  return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
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
    <label className="flex items-center gap-2 text-sm">
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

export default function PayrollEmployeeProfilesPage() {
  const { toast } = useToast();
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [bankBranches, setBankBranches] = useState<PayrollBankBranch[]>([]);
  const [bankCodeValues, setBankCodeValues] = useState<PayrollCodeValue[]>([]);
  const [exchangeRates, setExchangeRates] = useState<PayrollExchangeRate[]>([]);
  const [profilePayslipRunId, setProfilePayslipRunId] = useState('');
  const [profilePayslipSearch, setProfilePayslipSearch] = useState('');
  const [profilePage, setProfilePage] = useState(1);
  const [profilePageSize, setProfilePageSize] = useState(25);
  const [profileForm, setProfileForm] = useState(() => createDefaultProfileForm());
  const [selectedPaymentMethodIndex, setSelectedPaymentMethodIndex] = useState(0);
  const [profileDialogOpen, setProfileDialogOpen] = useState(false);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeeResults, setEmployeeResults] = useState<HrEmployee[]>([]);
  const [previewPayslip, setPreviewPayslip] = useState<PayrollPayslip | null>(null);
  const [payslipEmailResult, setPayslipEmailResult] = useState<PayrollPayslipEmailResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, bankSetup, runList, profileList, rateList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getCodeSetup('BNK'),
        payrollService.getRuns(),
        payrollService.getEmployeeProfiles(),
        payrollService.getExchangeRates(),
      ]);

      setActiveParameters(setup.activeParameters ?? null);
      setBankBranches(setup.bankBranches ?? []);
      setBankCodeValues(bankSetup.codeValues ?? []);
      setExchangeRates(rateList);
      setRuns(runList);
      setProfiles(profileList);
      setProfilePayslipRunId((current) => current || runList[0]?.id || '');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load payroll employee profiles.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const runOperation = async (
    label: string,
    action: () => Promise<PayrollEmployeeProfile | PayrollPayslipEmailResult | unknown>,
  ) => {
    setBusy(label);
    setError(null);
    try {
      const result = await action();
      toast({ title: 'Payroll updated', description: `${label} completed.` });

      const [setup, bankSetup, runList, profileList, rateList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getCodeSetup('BNK'),
        payrollService.getRuns(),
        payrollService.getEmployeeProfiles(),
        payrollService.getExchangeRates(),
      ]);
      setActiveParameters(setup.activeParameters ?? null);
      setBankBranches(setup.bankBranches ?? []);
      setBankCodeValues(bankSetup.codeValues ?? []);
      setExchangeRates(rateList);
      setRuns(runList);
      setProfiles(profileList);
      setProfilePayslipRunId((current) => current || runList[0]?.id || '');
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Payroll operation failed.';
      setError(message);
      toast({ title: 'Payroll error', description: message, variant: 'destructive' });
      return null;
    } finally {
      setBusy(null);
    }
  };

  const selectedProfilePayslipRunId = profilePayslipRunId || runs[0]?.id || '';
  const profilePayslipRun = runs.find((run) => run.id === selectedProfilePayslipRunId) ?? null;
  const activeProfileCount = profiles.filter((profile) => profile.payrollActive).length;
  const baseCurrency = activeParameters?.baseCurrency || profileForm.currencyCode || 'GHS';

  const currencyOptions = useMemo(() => {
    const values = new Set<string>(['GHS', 'USD', 'EUR', 'GBP', baseCurrency, activeParameters?.reportingCurrency || '', profileForm.currencyCode]);
    exchangeRates.forEach((rate) => values.add(rate.currencyCode));
    profileForm.paymentMethods.forEach((method) => values.add(method.currencyCode));
    return Array.from(values).filter(Boolean).map((value) => value.toUpperCase()).sort();
  }, [activeParameters?.reportingCurrency, baseCurrency, exchangeRates, profileForm.currencyCode, profileForm.paymentMethods]);

  const bankCodeOptions = useMemo(() => {
    const values = new Set<string>();
    bankCodeValues.forEach((value) => {
      if (value.actualCode) {
        values.add(value.actualCode);
      }
    });
    bankBranches.forEach((branch) => {
      if (branch.bankCode) {
        values.add(branch.bankCode);
      }
    });
    return Array.from(values).sort();
  }, [bankBranches, bankCodeValues]);

  const bankLabelByCode = useMemo(() => {
    const labels = new Map<string, string>();
    bankCodeValues.forEach((value) => {
      const code = value.actualCode?.trim();
      if (!code) {
        return;
      }

      const description = value.description?.trim();
      labels.set(code, description ? `${code} - ${description}` : code);
    });

    bankBranches.forEach((branch) => {
      if (!branch.bankCode || labels.has(branch.bankCode)) {
        return;
      }

      labels.set(branch.bankCode, branch.bankCode);
    });

    return labels;
  }, [bankBranches, bankCodeValues]);

  const activePaymentMethods = profileForm.paymentMethods.filter((method) => method.isActive);
  const percentageTotal = activePaymentMethods
    .filter((method) => method.paymentMode === 'Percentage')
    .reduce((total, method) => total + Number(method.paymentPercent || 0), 0);
  const fixedBaseEquivalent = activePaymentMethods
    .filter((method) => method.paymentMode === 'FixedAmount')
    .reduce((total, method) => total + Number(method.amount || 0) * (Number(method.exchangeRate || 1) || 1), 0);

  const resolveExchangeRate = (currencyCode: string) => {
    const currency = currencyCode.trim().toUpperCase();
    if (!currency || currency === baseCurrency.toUpperCase()) {
      return 1;
    }

    const match = exchangeRates
      .filter((rate) => rate.currencyCode.toUpperCase() === currency)
      .sort((a, b) => (b.payPeriod || 0) - (a.payPeriod || 0))[0];

    return match?.rate && match.rate > 0 ? match.rate : 1;
  };

  const filteredProfiles = useMemo(() => {
    const term = profilePayslipSearch.trim().toLowerCase();
    if (!term) {
      return profiles;
    }

    return profiles.filter((profile) =>
      profile.employeeNumber.toLowerCase().includes(term) ||
      profile.employeeName.toLowerCase().includes(term) ||
      (profile.departmentName || '').toLowerCase().includes(term) ||
      (profile.sectionName || '').toLowerCase().includes(term) ||
      (profile.positionTitle || '').toLowerCase().includes(term) ||
      (profile.staffCategory || '').toLowerCase().includes(term) ||
      (profile.ssfNumber || '').toLowerCase().includes(term) ||
      (profile.tinNumber || '').toLowerCase().includes(term));
  }, [profiles, profilePayslipSearch]);

  const profileTotalPages = Math.max(1, Math.ceil(filteredProfiles.length / profilePageSize));
  const pagedProfiles = useMemo(() => {
    const start = (profilePage - 1) * profilePageSize;
    return filteredProfiles.slice(start, start + profilePageSize);
  }, [filteredProfiles, profilePage, profilePageSize]);

  useEffect(() => {
    setProfilePage(1);
  }, [profilePayslipSearch, profilePageSize]);

  useEffect(() => {
    setProfilePage((current) => Math.min(Math.max(current, 1), profileTotalPages));
  }, [profileTotalPages]);

  const searchEmployees = async () => {
    if (!employeeSearch.trim()) {
      return;
    }

    setBusy('Search Employees');
    setError(null);
    try {
      const result = await payrollService.searchEmployees(employeeSearch.trim());
      setEmployeeResults(result.items);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Employee search failed.';
      setError(message);
      toast({ title: 'Employee search error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  const chooseEmployee = (employee: HrEmployee) => {
    setSelectedPaymentMethodIndex(0);
    setProfileForm((current) => ({
      ...current,
      id: '',
      employeeId: employee.id,
      employeeNumber: employee.employeeNumber,
      paymentMethods: current.paymentMethods.length > 0 ? current.paymentMethods : [createDefaultPaymentMethod(current.currencyCode || baseCurrency)],
    }));
  };

  const updatePaymentMethod = (index: number, patch: Partial<PaymentMethodForm>) => {
    setProfileForm((current) => ({
      ...current,
      paymentMethods: current.paymentMethods.map((method, methodIndex) => {
        if (methodIndex !== index) {
          return method;
        }

        const next = { ...method, ...patch };
        if (patch.currencyCode) {
          next.exchangeRate = resolveExchangeRate(patch.currencyCode);
        }

        if (patch.paymentMode === 'Percentage') {
          next.amount = 0;
          next.exchangeRate = next.currencyCode.toUpperCase() === baseCurrency.toUpperCase() ? 1 : next.exchangeRate;
        }

        if (patch.paymentMode === 'FixedAmount') {
          next.paymentPercent = 0;
          next.exchangeRate = next.exchangeRate || resolveExchangeRate(next.currencyCode);
        }

        return next;
      }),
    }));
  };

  const addPaymentMethod = () => {
    const nextIndex = profileForm.paymentMethods.length;
    setSelectedPaymentMethodIndex(nextIndex);
    setProfileForm((current) => ({
      ...current,
      paymentMethods: [
        ...current.paymentMethods,
        {
          ...createDefaultPaymentMethod(current.currencyCode || baseCurrency, current.paymentMethods.length + 1),
          paymentPercent: 0,
        },
      ],
    }));
  };

  const removePaymentMethod = (index: number) => {
    const nextCount = Math.max(profileForm.paymentMethods.length - 1, 1);
    setSelectedPaymentMethodIndex((current) => {
      if (current > index) {
        return current - 1;
      }

      return Math.min(current, nextCount - 1);
    });
    setProfileForm((current) => {
      const nextMethods = current.paymentMethods
        .filter((_, methodIndex) => methodIndex !== index)
        .map((method, methodIndex) => ({ ...method, sequenceNo: methodIndex + 1 }));

      return {
        ...current,
        paymentMethods: nextMethods.length > 0 ? nextMethods : [createDefaultPaymentMethod(current.currencyCode || baseCurrency)],
      };
    });
  };

  const selectProfileForEdit = (profile: PayrollEmployeeProfile) => {
    const currencyCode = profile.currencyCode || activeParameters?.baseCurrency || 'GHS';
    const paymentMethods = profile.paymentMethods?.length
      ? profile.paymentMethods.map((method, index) => toPaymentMethodForm(method, index, currencyCode))
      : [createDefaultPaymentMethod(currencyCode)];

    setProfileForm({
      id: profile.id || '',
      employeeId: profile.employeeId,
      employeeNumber: profile.employeeNumber,
      legacyEmployeeId: profile.legacyEmployeeId || '',
      legacyEmployeeNumber: profile.legacyEmployeeNumber || '',
      ssfNumber: profile.ssfNumber || '',
      monthlyBasicSalary: profile.salaryBasis?.monthlyBasicSalary ?? 0,
      currencyCode,
      payrollActive: profile.payrollActive,
      payTax: profile.payTax,
      ssfApplicable: profile.ssfApplicable,
      overtimeEligible: profile.overtimeEligible,
      grossUp: profile.grossUp,
      tier2Only: profile.tier2Only,
      paymentMethods,
    });
    setSelectedPaymentMethodIndex(0);
    setProfileDialogOpen(true);
    setEmployeeResults([]);
    setEmployeeSearch('');
  };

  const clearProfileForm = () => {
    setProfileForm(createDefaultProfileForm(activeParameters?.baseCurrency || 'GHS'));
    setSelectedPaymentMethodIndex(0);
    setEmployeeResults([]);
    setEmployeeSearch('');
  };

  const loadEmployeePayslip = async (profile: PayrollEmployeeProfile) => {
    if (!selectedProfilePayslipRunId) {
      throw new Error('Select a payroll run before opening a payslip.');
    }

    const slips = await payrollService.getPayslips(selectedProfilePayslipRunId, profile.employeeId);
    const slip = slips[0];
    if (!slip) {
      throw new Error(`No payslip found for ${profile.employeeNumber} in the selected run.`);
    }

    return slip;
  };

  const handleProfilePayslipAction = async (profile: PayrollEmployeeProfile, action: 'print' | 'email') => {
    const label = action === 'print' ? 'Print payslip' : 'Email payslip';
    setBusy(label);
    setError(null);
    try {
      if (action === 'email') {
        if (!selectedProfilePayslipRunId) {
          throw new Error('Select a payroll run before emailing a payslip.');
        }

        const result = await payrollService.emailPayslips(selectedProfilePayslipRunId, { employeeIds: [profile.employeeId] });
        setPayslipEmailResult(result);
        toast({ title: 'Payslip email queued', description: `${result.sentCount} sent, ${result.failedCount} failed.` });
        return;
      }

      const slip = await loadEmployeePayslip(profile);
      setPreviewPayslip(slip);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Payslip action failed.';
      setError(message);
      toast({ title: 'Payslip error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  const currentPayrollPeriodLabel = activeParameters
    ? formatPayrollPeriodLabel(activeParameters.currentPeriodFrom, activeParameters.currentPeriodTo)
    : 'Not configured';

  const validatePaymentMethods = () => {
    const activeRows = profileForm.paymentMethods.filter((method) => method.isActive);
    if (activeRows.length === 0) {
      return 'Add at least one active payment method.';
    }

    const percentageRows = activeRows.filter((method) => method.paymentMode === 'Percentage');
    if (percentageRows.length === 0) {
      return 'Add one percentage row for the remaining net salary.';
    }

    const percentTotal = percentageRows.reduce((total, method) => total + Number(method.paymentPercent || 0), 0);
    if (Math.round(percentTotal * 10000) / 10000 !== 100) {
      return 'Percentage payment rows must total 100%. Fixed amounts are deducted before this split.';
    }

    const invalidFixedRow = activeRows.find((method) => method.paymentMode === 'FixedAmount' && Number(method.amount || 0) <= 0);
    if (invalidFixedRow) {
      return 'Fixed amount rows must have an amount greater than zero.';
    }

    return null;
  };

  const buildPaymentMethodPayload = () =>
    profileForm.paymentMethods.map((method, index) => ({
      id: method.id || undefined,
      paymentType: method.paymentType || 'Bank',
      paymentMode: method.paymentMode,
      paymentPercent: method.paymentMode === 'Percentage' ? Number(method.paymentPercent || 0) : null,
      amount: method.paymentMode === 'FixedAmount' ? Number(method.amount || 0) : null,
      bankCode: method.paymentType === 'Bank' ? method.bankCode || null : null,
      bankBranchCode: method.paymentType === 'Bank' ? method.bankBranchCode || null : null,
      accountNumber: method.paymentType === 'Bank' ? method.accountNumber || null : null,
      chequeNumber: method.paymentType === 'Cheque' ? method.chequeNumber || null : null,
      chequeBankCode: method.paymentType === 'Cheque' ? method.chequeBankCode || null : null,
      currencyCode: method.currencyCode || profileForm.currencyCode || baseCurrency,
      exchangeRate: Number(method.exchangeRate || 1) || 1,
      sequenceNo: index + 1,
      startDate: method.startDate || null,
      endDate: method.endDate || null,
      isActive: method.isActive,
    }));

  return (
    <main className="space-y-5 p-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="ghost" size="sm" className="-ml-2">
              <Link href="/hr/payroll">
                <ArrowLeft className="mr-2 h-4 w-4" />
                Payroll
              </Link>
            </Button>
            <Badge variant="outline">Human Resources</Badge>
          </div>
          <h1 className="text-2xl font-semibold tracking-normal">Employee Profiles</h1>
          <p className="max-w-3xl text-sm text-muted-foreground">
            Maintain the payroll-ready employee records used by payroll runs, payslips, salary advances, loans, and employee-specific payroll entries.
          </p>
        </div>
        <div className="flex flex-col gap-2 lg:items-end">
          <div className="grid min-w-[280px] gap-3 rounded-md border bg-muted/40 p-3 text-sm sm:grid-cols-[1fr_auto] lg:min-w-[420px]">
            <div>
              <div className="text-xs text-muted-foreground">Payroll Period</div>
              <div className="font-semibold">{currentPayrollPeriodLabel}</div>
            </div>
            <div className="sm:text-right">
              <div className="text-xs text-muted-foreground">Profiles</div>
              <div className="font-semibold">{profiles.length}</div>
            </div>
          </div>
          <Button variant="outline" onClick={() => void loadWorkspace()} disabled={loading}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</div>}

      <section className="grid gap-3 md:grid-cols-3">
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Total Profiles</div>
          <div className="mt-1 font-medium">{profiles.length}</div>
        </div>
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Active Payroll</div>
          <div className="mt-1 font-medium">{activeProfileCount}</div>
        </div>
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Payslip Run</div>
          <div className="mt-1 truncate font-medium">{profilePayslipRun?.runNumber || 'Select run'}</div>
        </div>
      </section>

      <Dialog open={profileDialogOpen} onOpenChange={setProfileDialogOpen}>
        <DialogContent className="max-h-[92vh] max-w-[1180px] overflow-y-auto p-5">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-base">
              <UserPlus className="h-4 w-4" />
              {profileForm.id ? 'Edit Payroll Profile' : 'Payroll Profile'}
            </DialogTitle>
            <DialogDescription>Connect an ERP HR employee to payroll, salary basis, and payment split.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="flex gap-2">
              <Input placeholder="Search HR employees" value={employeeSearch} onChange={(event) => setEmployeeSearch(event.target.value)} />
              <Button type="button" variant="outline" onClick={() => void searchEmployees()} disabled={busy === 'Search Employees'}>
                {busy === 'Search Employees' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
              </Button>
            </div>
            {employeeResults.length > 0 && (
              <div className="max-h-48 overflow-auto rounded-md border">
                {employeeResults.map((employee) => (
                  <button key={employee.id} type="button" className="flex w-full items-center justify-between gap-3 border-b px-3 py-2 text-left text-sm last:border-b-0 hover:bg-muted" onClick={() => chooseEmployee(employee)}>
                    <span>
                      <span className="font-medium">{employee.employeeNumber}</span>
                      <span className="ml-2 text-muted-foreground">{employee.fullName || employee.displayName}</span>
                    </span>
                    <span className="text-xs text-muted-foreground">{employee.departmentName}</span>
                  </button>
                ))}
              </div>
            )}
            <form
              className="grid gap-3"
              onSubmit={(event: FormEvent) => {
                event.preventDefault();
                const paymentError = validatePaymentMethods();
                if (paymentError) {
                  setError(paymentError);
                  toast({ title: 'Payment method error', description: paymentError, variant: 'destructive' });
                  return;
                }

                void runOperation('Save profile', () =>
                  payrollService.upsertEmployeeProfile({
                    id: profileForm.id || undefined,
                    employeeId: profileForm.employeeId,
                    employeeNumber: profileForm.employeeNumber,
                    legacyEmployeeId: profileForm.legacyEmployeeId || null,
                    legacyEmployeeNumber: profileForm.legacyEmployeeNumber || null,
                    payrollActive: profileForm.payrollActive,
                    payTax: profileForm.payTax,
                    ssfApplicable: profileForm.ssfApplicable,
                    ssfNumber: profileForm.ssfNumber || null,
                    grossUp: profileForm.grossUp,
                    tier2Only: profileForm.tier2Only,
                    overtimeEligible: profileForm.overtimeEligible,
                    currencyCode: profileForm.currencyCode,
                    salaryBasis: {
                      monthlyBasicSalary: profileForm.monthlyBasicSalary,
                      currencyCode: profileForm.currencyCode,
                      effectiveFrom: today,
                      isActive: true,
                    },
                    paymentMethods: buildPaymentMethodPayload(),
                    employeeComponents: [],
                  }),
                );
              }}
            >
              <Tabs defaultValue="profile" className="space-y-3">
                <TabsList className="grid w-full grid-cols-2">
                  <TabsTrigger value="profile">Profile</TabsTrigger>
                  <TabsTrigger value="payments">Payment Methods</TabsTrigger>
                </TabsList>

                <TabsContent value="profile" className="mt-0 space-y-3">
                  <div className="grid gap-3 md:grid-cols-2">
                    <Field label="Employee Number">
                      <Input value={profileForm.employeeNumber} onChange={(event) => setProfileForm((current) => ({ ...current, employeeNumber: event.target.value }))} />
                    </Field>
                    <Field label="SSF Number">
                      <Input value={profileForm.ssfNumber} onChange={(event) => setProfileForm((current) => ({ ...current, ssfNumber: event.target.value }))} />
                    </Field>
                  </div>
                  <div className="grid gap-3 md:grid-cols-2">
                    <Field label="Monthly Basic Salary">
                      <Input type="number" step="0.01" value={profileForm.monthlyBasicSalary} onChange={(event) => setProfileForm((current) => ({ ...current, monthlyBasicSalary: Number(event.target.value) }))} />
                    </Field>
                    <Field label="Currency">
                      <select
                        className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                        value={profileForm.currencyCode}
                        onChange={(event) => setProfileForm((current) => ({ ...current, currencyCode: event.target.value }))}
                      >
                        {currencyOptions.map((currency) => (
                          <option key={currency} value={currency}>{currency}</option>
                        ))}
                      </select>
                    </Field>
                  </div>
                  <div className="grid gap-2 md:grid-cols-2">
                    <BooleanField label="Payroll active" checked={profileForm.payrollActive} onChange={(checked) => setProfileForm((current) => ({ ...current, payrollActive: checked }))} />
                    <BooleanField label="PAYE" checked={profileForm.payTax} onChange={(checked) => setProfileForm((current) => ({ ...current, payTax: checked }))} />
                    <BooleanField label="SSF" checked={profileForm.ssfApplicable} onChange={(checked) => setProfileForm((current) => ({ ...current, ssfApplicable: checked }))} />
                    <BooleanField label="Overtime" checked={profileForm.overtimeEligible} onChange={(checked) => setProfileForm((current) => ({ ...current, overtimeEligible: checked }))} />
                    <BooleanField label="Gross up" checked={profileForm.grossUp} onChange={(checked) => setProfileForm((current) => ({ ...current, grossUp: checked }))} />
                    <BooleanField label="Tier 2 only" checked={profileForm.tier2Only} onChange={(checked) => setProfileForm((current) => ({ ...current, tier2Only: checked }))} />
                  </div>
                </TabsContent>

                <TabsContent value="payments" className="mt-0 space-y-3">
                  <div className="grid gap-2 text-xs sm:grid-cols-2">
                    <div className={`rounded-md border px-3 py-2 ${Math.round(percentageTotal * 10000) / 10000 === 100 ? 'bg-muted/30' : 'border-destructive/40 bg-destructive/10 text-destructive'}`}>
                      <div className="text-muted-foreground">Percentage total</div>
                      <div className="font-semibold">{percentageTotal.toFixed(2)}%</div>
                    </div>
                    <div className="rounded-md border bg-muted/30 px-3 py-2">
                      <div className="text-muted-foreground">Fixed base equivalent</div>
                      <div className="font-semibold">{money(fixedBaseEquivalent, baseCurrency)}</div>
                    </div>
                  </div>

                  <div className="overflow-x-auto rounded-md border">
                    <Table className="min-w-[1060px] text-xs">
                      <TableHeader>
                        <TableRow>
                          <TableHead className="h-7 w-[52px] py-1">Row</TableHead>
                          <TableHead className="h-7 w-[88px] py-1">Type</TableHead>
                          <TableHead className="h-7 w-[116px] py-1">Split</TableHead>
                          <TableHead className="h-7 w-[94px] py-1">Value</TableHead>
                          <TableHead className="h-7 w-[72px] py-1">Currency</TableHead>
                          <TableHead className="h-7 w-[72px] py-1">Rate</TableHead>
                          <TableHead className="h-7 w-[180px] py-1">Bank</TableHead>
                          <TableHead className="h-7 w-[150px] py-1">Branch</TableHead>
                          <TableHead className="h-7 w-[136px] py-1">Account/Cheque</TableHead>
                          <TableHead className="h-7 w-[36px] py-1 text-right"></TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {profileForm.paymentMethods.map((method, index) => {
                          const branchOptions = bankBranches.filter((branch) => !method.bankCode || branch.bankCode === method.bankCode);
                          const isSelected = selectedPaymentMethodIndex === index;

                          return (
                            <TableRow
                              key={method.id || index}
                              className={`cursor-pointer whitespace-nowrap hover:bg-muted/50 ${isSelected ? 'bg-blue-50/70' : ''}`}
                              onClick={() => setSelectedPaymentMethodIndex(index)}
                            >
                              <TableCell className="py-1">
                                <div className="flex items-center gap-2">
                                  <Checkbox checked={method.isActive} onCheckedChange={(value) => updatePaymentMethod(index, { isActive: value === true })} />
                                  <span className="font-medium">{index + 1}</span>
                                </div>
                              </TableCell>
                              <TableCell className="py-1">
                                <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" value={method.paymentType} onChange={(event) => updatePaymentMethod(index, { paymentType: event.target.value })}>
                                  {paymentMethodOptions.map((option) => (
                                    <option key={option} value={option}>{option}</option>
                                  ))}
                                </select>
                              </TableCell>
                              <TableCell className="py-1">
                                <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" value={method.paymentMode} onChange={(event) => updatePaymentMethod(index, { paymentMode: event.target.value as PaymentMethodForm['paymentMode'] })}>
                                  <option value="Percentage">Percentage</option>
                                  <option value="FixedAmount">Fixed Amount</option>
                                </select>
                              </TableCell>
                              <TableCell className="py-1">
                                <Input
                                  className="h-7 px-1.5 text-xs"
                                  type="number"
                                  step={method.paymentMode === 'FixedAmount' ? '0.01' : '0.0001'}
                                  value={method.paymentMode === 'FixedAmount' ? method.amount : method.paymentPercent}
                                  onChange={(event) => updatePaymentMethod(index, method.paymentMode === 'FixedAmount'
                                    ? { amount: Number(event.target.value) }
                                    : { paymentPercent: Number(event.target.value) })}
                                />
                              </TableCell>
                              <TableCell className="py-1">
                                <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" value={method.currencyCode} onChange={(event) => updatePaymentMethod(index, { currencyCode: event.target.value })}>
                                  {currencyOptions.map((currency) => (
                                    <option key={currency} value={currency}>{currency}</option>
                                  ))}
                                </select>
                              </TableCell>
                              <TableCell className="py-1">
                                <Input className="h-7 px-1.5 text-xs" type="number" step="0.0001" value={method.exchangeRate} onChange={(event) => updatePaymentMethod(index, { exchangeRate: Number(event.target.value) })} />
                              </TableCell>
                              <TableCell className="py-1">
                                {method.paymentType === 'Bank' && (
                                  bankCodeOptions.length > 0 ? (
                                    <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" value={method.bankCode} onChange={(event) => updatePaymentMethod(index, { bankCode: event.target.value, bankBranchCode: '' })}>
                                      <option value="">Select bank</option>
                                      {bankCodeOptions.map((bankCode) => (
                                        <option key={bankCode} value={bankCode}>{bankLabelByCode.get(bankCode) ?? bankCode}</option>
                                      ))}
                                    </select>
                                  ) : (
                                    <Input className="h-7 px-1.5 text-xs" placeholder="Bank" value={method.bankCode} onChange={(event) => updatePaymentMethod(index, { bankCode: event.target.value })} />
                                  )
                                )}
                                {method.paymentType === 'Cheque' && (
                                  bankCodeOptions.length > 0 ? (
                                    <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" value={method.chequeBankCode} onChange={(event) => updatePaymentMethod(index, { chequeBankCode: event.target.value })}>
                                      <option value="">Cheque bank</option>
                                      {bankCodeOptions.map((bankCode) => (
                                        <option key={bankCode} value={bankCode}>{bankLabelByCode.get(bankCode) ?? bankCode}</option>
                                      ))}
                                    </select>
                                  ) : (
                                    <Input className="h-7 px-1.5 text-xs" placeholder="Cheque bank" value={method.chequeBankCode} onChange={(event) => updatePaymentMethod(index, { chequeBankCode: event.target.value })} />
                                  )
                                )}
                                {method.paymentType === 'Cash' && (
                                  <span className="inline-flex h-7 w-full items-center rounded-md border border-dashed px-2 text-muted-foreground">Cash</span>
                                )}
                              </TableCell>
                              <TableCell className="py-1">
                                {method.paymentType === 'Bank' && (
                                  branchOptions.length > 0 ? (
                                    <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" value={method.bankBranchCode} onChange={(event) => updatePaymentMethod(index, { bankBranchCode: event.target.value })}>
                                      <option value="">Branch</option>
                                      {branchOptions.map((branch) => (
                                        <option key={`${branch.bankCode}-${branch.branchCode}`} value={branch.branchCode}>
                                          {branch.branchCode} {branch.branchDescription ? `- ${branch.branchDescription}` : ''}
                                        </option>
                                      ))}
                                    </select>
                                  ) : (
                                    <Input className="h-7 px-1.5 text-xs" placeholder="Branch" value={method.bankBranchCode} onChange={(event) => updatePaymentMethod(index, { bankBranchCode: event.target.value })} />
                                  )
                                )}
                                {method.paymentType !== 'Bank' && (
                                  <span className="inline-flex h-7 w-full items-center rounded-md border border-dashed px-2 text-muted-foreground">-</span>
                                )}
                              </TableCell>
                              <TableCell className="py-1">
                                {method.paymentType === 'Bank' && (
                                  <Input className="h-7 px-1.5 text-xs" placeholder="Account No" value={method.accountNumber} onChange={(event) => updatePaymentMethod(index, { accountNumber: event.target.value })} />
                                )}
                                {method.paymentType === 'Cheque' && (
                                  <Input className="h-7 px-1.5 text-xs" placeholder="Cheque No" value={method.chequeNumber} onChange={(event) => updatePaymentMethod(index, { chequeNumber: event.target.value })} />
                                )}
                                {method.paymentType === 'Cash' && (
                                  <span className="inline-flex h-7 w-full items-center rounded-md border border-dashed px-2 text-muted-foreground">-</span>
                                )}
                              </TableCell>
                              <TableCell className="py-1 text-right">
                                <Button type="button" variant="ghost" size="icon" className="h-7 w-7" title="Delete row" aria-label="Delete row" onClick={(event) => { event.stopPropagation(); removePaymentMethod(index); }} disabled={profileForm.paymentMethods.length === 1}>
                                  <Trash2 className="h-3.5 w-3.5" />
                                </Button>
                              </TableCell>
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                  </div>

                  <Button type="button" variant="outline" size="sm" onClick={addPaymentMethod}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add Row
                  </Button>
                </TabsContent>
              </Tabs>
              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={!profileForm.employeeId || busy === 'Save profile'}>
                  {busy === 'Save profile' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  {profileForm.id ? 'Save Changes' : 'Save Profile'}
                </Button>
              </div>
            </form>
          </div>
        </DialogContent>
      </Dialog>

      <Card>
          <CardHeader className="pb-2">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Users className="h-4 w-4" />
                Active Payroll Profiles
              </CardTitle>
              <div className="flex flex-wrap items-center gap-2">
                <Button
                  type="button"
                  size="sm"
                  className="h-8"
                  onClick={() => {
                    clearProfileForm();
                    setProfileDialogOpen(true);
                  }}
                >
                  <UserPlus className="mr-2 h-3.5 w-3.5" />
                  New Profile
                </Button>
                <select
                  className="h-8 min-w-[190px] rounded-md border bg-background px-2 text-xs"
                  value={selectedProfilePayslipRunId}
                  onChange={(event) => setProfilePayslipRunId(event.target.value)}
                >
                  <option value="">Select payroll run</option>
                  {runs.map((run) => (
                    <option key={run.id} value={run.id}>{run.runNumber} / {formatPayrollPeriodLabel(run.payPeriodFrom, run.payPeriodTo)}</option>
                  ))}
                </select>
                <Input
                  className="h-8 w-[180px] text-xs"
                  placeholder="Search employee"
                  value={profilePayslipSearch}
                  onChange={(event) => setProfilePayslipSearch(event.target.value)}
                />
                <PayrollGridExportButton
                  rows={filteredProfiles}
                  fileName="active-payroll-profiles"
                  columns={[
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Department', value: (row) => row.departmentName },
                    { header: 'Section', value: (row) => row.sectionName },
                    { header: 'Position', value: (row) => row.positionTitle },
                    { header: 'Staff Category', value: (row) => row.staffCategory },
                    { header: 'SSF Number', value: (row) => row.ssfNumber },
                    { header: 'TIN Number', value: (row) => row.tinNumber },
                    { header: 'Basic', value: (row) => row.salaryBasis?.monthlyBasicSalary },
                    { header: 'Currency', value: (row) => row.currencyCode },
                    { header: 'Payroll Active', value: (row) => row.payrollActive ? 'Yes' : 'No' },
                    { header: 'PAYE', value: (row) => row.payTax ? 'Yes' : 'No' },
                    { header: 'SSF', value: (row) => row.ssfApplicable ? 'Yes' : 'No' },
                    { header: 'Overtime', value: (row) => row.overtimeEligible ? 'Yes' : 'No' },
                    { header: 'Gross Up', value: (row) => row.grossUp ? 'Yes' : 'No' },
                    { header: 'Tier 2 Only', value: (row) => row.tier2Only ? 'Yes' : 'No' },
                  ]}
                />
              </div>
            </div>
            {profilePayslipRun && (
              <CardDescription>Payslip actions use {profilePayslipRun.runNumber} / {formatPayrollPeriodLabel(profilePayslipRun.payPeriodFrom, profilePayslipRun.payPeriodTo)}.</CardDescription>
            )}
            {payslipEmailResult && (
              <CardDescription>
                {payslipEmailResult.sentCount} payslip email sent, {payslipEmailResult.failedCount} failed.
              </CardDescription>
            )}
          </CardHeader>
          <CardContent className="px-3 pb-3">
            <Table className="w-full table-fixed text-xs">
              <colgroup>
                <col className="w-[12%]" />
                <col className="w-[16%]" />
                <col className="w-[16%]" />
                <col className="w-[16%]" />
                <col className="w-[12%]" />
                <col className="w-[18%]" />
                <col className="w-[10%]" />
              </colgroup>
              <TableHeader>
                <TableRow>
                  <TableHead className="h-8 px-2 py-1">Employee</TableHead>
                  <TableHead className="h-8 px-2 py-1">Department / Section</TableHead>
                  <TableHead className="h-8 px-2 py-1">Position / Category</TableHead>
                  <TableHead className="h-8 px-2 py-1">SSF / Tax ID</TableHead>
                  <TableHead className="h-8 px-2 py-1 text-right">Basic / Currency</TableHead>
                  <TableHead className="h-8 px-2 py-1">Flags</TableHead>
                  <TableHead className="h-8 px-2 py-1 text-right">Payslip</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pagedProfiles.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} className="h-20 text-center text-sm text-muted-foreground">
                      No payroll employee profiles found.
                    </TableCell>
                  </TableRow>
                )}
                {pagedProfiles.map((profile) => (
                    <TableRow
                      key={profile.id}
                      className={`group cursor-pointer hover:bg-muted/60 ${profileForm.id && profileForm.id === profile.id ? 'bg-muted/60' : ''}`}
                      onClick={() => selectProfileForEdit(profile)}
                    >
                      <TableCell className="overflow-hidden px-2 py-1.5">
                        <button
                          type="button"
                          className="block max-w-full truncate font-medium text-blue-600 underline-offset-2 hover:underline"
                          onClick={(event) => {
                            event.stopPropagation();
                            selectProfileForEdit(profile);
                          }}
                        >
                          {profile.employeeNumber}
                        </button>
                        <div className="truncate text-xs text-muted-foreground">{profile.employeeName}</div>
                      </TableCell>
                      <TableCell className="overflow-hidden px-2 py-1.5">
                        <div className="truncate font-medium">{profile.departmentName || '-'}</div>
                        <div className="truncate text-xs text-muted-foreground">{profile.sectionName || '-'}</div>
                      </TableCell>
                      <TableCell className="overflow-hidden px-2 py-1.5">
                        <div className="truncate font-medium">{profile.positionTitle || '-'}</div>
                        <div className="truncate text-xs text-muted-foreground">{profile.staffCategory || profile.jobLocation || '-'}</div>
                      </TableCell>
                      <TableCell className="overflow-hidden px-2 py-1.5 font-mono">
                        <div className="truncate">SSF: {profile.ssfNumber || '-'}</div>
                        <div className="truncate text-xs text-muted-foreground">TIN: {profile.tinNumber || '-'}</div>
                      </TableCell>
                      <TableCell className="overflow-hidden px-2 py-1.5 text-right">
                        <div className="font-medium">{money(profile.salaryBasis?.monthlyBasicSalary, profile.currencyCode)}</div>
                        <div className="text-xs text-muted-foreground">{profile.currencyCode}</div>
                      </TableCell>
                      <TableCell className="overflow-hidden px-2 py-1.5">
                        <div className="flex max-w-full flex-wrap gap-1">
                          <Badge variant={profile.payrollActive ? 'secondary' : 'outline'} className="px-1.5 py-0 text-[10px]">{profile.payrollActive ? 'Active' : 'Inactive'}</Badge>
                          {profile.payTax && <Badge variant="secondary" className="px-1.5 py-0 text-[10px]">PAYE</Badge>}
                          {profile.ssfApplicable && <Badge variant="secondary" className="px-1.5 py-0 text-[10px]">Pays SSF</Badge>}
                          {profile.overtimeEligible && <Badge variant="outline" className="px-1.5 py-0 text-[10px]">Overtime</Badge>}
                          {profile.grossUp && <Badge variant="outline" className="px-1.5 py-0 text-[10px]">Gross Up</Badge>}
                          {profile.tier2Only && <Badge variant="outline" className="px-1.5 py-0 text-[10px]">Tier 2</Badge>}
                        </div>
                      </TableCell>
                      <TableCell className="px-2 py-1.5 text-right">
                        <div className="flex justify-end gap-1">
                          <Button type="button" size="icon" variant="ghost" className="h-7 w-7" title="Print payslip" aria-label="Print payslip" disabled={!selectedProfilePayslipRunId || Boolean(busy)} onClick={(event) => { event.stopPropagation(); void handleProfilePayslipAction(profile, 'print'); }}>
                            <Printer className="h-3.5 w-3.5" />
                          </Button>
                          <Button type="button" size="icon" variant="ghost" className="h-7 w-7" title="Email payslip" aria-label="Email payslip" disabled={!selectedProfilePayslipRunId || Boolean(busy)} onClick={(event) => { event.stopPropagation(); void handleProfilePayslipAction(profile, 'email'); }}>
                            <Mail className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                ))}
              </TableBody>
            </Table>
            {filteredProfiles.length > 0 && (
              <Pagination
                currentPage={profilePage}
                totalPages={profileTotalPages}
                totalItems={filteredProfiles.length}
                pageSize={profilePageSize}
                onPageChange={setProfilePage}
                onPageSizeChange={(nextPageSize) => {
                  setProfilePageSize(nextPageSize);
                  setProfilePage(1);
                }}
              />
            )}
          </CardContent>
      </Card>

      {loading && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading payroll employee profiles
        </div>
      )}

      <PayrollPayslipPreviewDialog
        open={Boolean(previewPayslip)}
        payslip={previewPayslip}
        onOpenChange={(open) => {
          if (!open) {
            setPreviewPayslip(null);
          }
        }}
      />
    </main>
  );
}
