'use client';

import Link from 'next/link';
import {
  FormEvent,
  ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react';
import {
  ArrowLeft,
  Calculator,
  FileText,
  Landmark,
  Loader2,
  PauseCircle,
  RefreshCw,
  Save,
  Search,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollLoan,
  PayrollLoanPolicy,
  PayrollParameterSet,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);

const defaultForm = {
  id: '',
  employeeProfileId: '',
  employeeNumber: '',
  employeeName: '',
  loanPolicyId: '',
  loanTypeCode: '',
  facilityNumber: '',
  dateGranted: today,
  amountGranted: 0,
  loanBalance: 0,
  monthlyRepaymentAmount: 0,
  interestRatePercent: 0,
  totalInterest: 0,
  interestRepaymentAmount: 0,
  repaymentMode: 'Amount',
  paymentStartDate: today,
  paymentEndDate: '',
  numberOfRepayments: 0,
  status: 'Active',
  periodOfSuspension: 0,
  suspensionStartDate: '',
  suspensionEndDate: '',
  suspensionNarration: '',
  generalRemarks: '',
};

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function money(value: number | null | undefined, currency = 'GHS') {
  return new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(value ?? 0);
}

function formatPayrollPeriodLabel(parameters: PayrollParameterSet | null) {
  const source =
    dateValue(parameters?.currentPeriodTo) ||
    dateValue(parameters?.currentPeriodFrom);
  if (!source) {
    return 'Not configured';
  }

  const [year, month] = source.split('-').map(Number);
  if (!year || !month) {
    return 'Not configured';
  }

  return new Intl.DateTimeFormat('en-US', {
    month: 'long',
    year: 'numeric',
  }).format(new Date(year, month - 1, 1));
}

function addMonthsValue(value: string, months: number) {
  if (!value || months <= 0) {
    return value;
  }

  const date = new Date(`${value}T00:00:00`);
  date.setMonth(date.getMonth() + months - 1);
  return date.toISOString().slice(0, 10);
}

function addSuspensionPeriod(value: string, months: number) {
  if (!value || months <= 0) {
    return value;
  }

  const date = new Date(`${value}T00:00:00`);
  date.setMonth(date.getMonth() + months);
  date.setDate(date.getDate() - 1);
  return date.toISOString().slice(0, 10);
}

function monthsBetweenInclusive(startValue: string, endValue: string) {
  if (!startValue || !endValue) {
    return 0;
  }

  const start = new Date(`${startValue}T00:00:00`);
  const end = new Date(`${endValue}T00:00:00`);
  if (
    Number.isNaN(start.getTime()) ||
    Number.isNaN(end.getTime()) ||
    end < start
  ) {
    return 0;
  }

  return (
    (end.getFullYear() - start.getFullYear()) * 12 +
    end.getMonth() -
    start.getMonth() +
    1
  );
}

function loanInterestType(policy?: PayrollLoanPolicy) {
  const value = policy?.interestType?.replace(/\s+/g, '').toUpperCase();
  if (value === 'S' || value === 'SIMPLE') return 'S';
  if (value === 'F' || value === 'FLAT') return 'F';
  return 'R';
}

function buildPrincipalSchedule(
  amountGranted: number,
  repaymentAmount: number,
  numberOfRepayments: number
) {
  const rows: number[] = [];
  let remaining = Number(amountGranted.toFixed(2));
  for (
    let index = 1;
    index <= numberOfRepayments && remaining > 0;
    index += 1
  ) {
    const principal =
      index === numberOfRepayments
        ? remaining
        : Math.min(repaymentAmount, remaining);
    rows.push(Number(principal.toFixed(2)));
    remaining = Number(Math.max(0, remaining - principal).toFixed(2));
  }

  return rows;
}

function calculateLoanForm(
  current: typeof defaultForm,
  policy: PayrollLoanPolicy | undefined,
  basicSalary: number,
  patch: Partial<typeof defaultForm> = {}
) {
  const next = { ...current, ...patch };
  const amountGranted = Number(next.amountGranted) || 0;
  let numberOfRepayments = Number(next.numberOfRepayments) || 0;
  if (patch.paymentEndDate && next.paymentStartDate) {
    numberOfRepayments = monthsBetweenInclusive(
      next.paymentStartDate,
      next.paymentEndDate
    );
  }

  let repaymentAmount = Number(next.monthlyRepaymentAmount) || 0;
  if (next.repaymentMode === 'PercentOfBasic') {
    repaymentAmount =
      basicSalary > 0
        ? Number(((basicSalary * repaymentAmount) / 100).toFixed(2))
        : 0;
  } else if (
    next.repaymentMode === 'NoOfRepayments' &&
    numberOfRepayments > 0
  ) {
    repaymentAmount = Number((amountGranted / numberOfRepayments).toFixed(2));
    next.monthlyRepaymentAmount = repaymentAmount;
  }

  if (numberOfRepayments <= 0 && amountGranted > 0 && repaymentAmount > 0) {
    numberOfRepayments = Math.ceil(amountGranted / repaymentAmount);
  }

  const paymentEndDate =
    numberOfRepayments > 0
      ? addMonthsValue(next.paymentStartDate, numberOfRepayments)
      : next.paymentEndDate;
  const interestRate = Number(next.interestRatePercent) || 0;
  const applyInterest = policy ? policy.applyInterest : interestRate > 0;
  let totalInterest = 0;
  if (
    applyInterest &&
    amountGranted > 0 &&
    interestRate > 0 &&
    numberOfRepayments > 0
  ) {
    const interestType = loanInterestType(policy);
    if (interestType === 'S') {
      totalInterest =
        (amountGranted * interestRate * numberOfRepayments) / 1200;
    } else if (interestType === 'F') {
      totalInterest = (amountGranted * interestRate) / 100;
    } else {
      let remaining = amountGranted;
      for (const principal of buildPrincipalSchedule(
        amountGranted,
        repaymentAmount,
        numberOfRepayments
      )) {
        totalInterest += (remaining * interestRate) / 1200;
        remaining = Math.max(0, remaining - principal);
      }
    }
  }

  const roundedInterest = Number(totalInterest.toFixed(2));
  return {
    ...next,
    numberOfRepayments,
    paymentEndDate,
    totalInterest: roundedInterest,
    interestRepaymentAmount:
      numberOfRepayments > 0
        ? Number((roundedInterest / numberOfRepayments).toFixed(2))
        : roundedInterest,
    loanBalance:
      next.loanBalance > 0 && current.id ? next.loanBalance : amountGranted,
  };
}

function formFromLoan(loan: PayrollLoan) {
  return {
    id: loan.id || '',
    employeeProfileId: loan.employeeProfileId,
    employeeNumber: loan.employeeNumber,
    employeeName: loan.employeeName,
    loanPolicyId: loan.loanPolicyId || '',
    loanTypeCode: loan.loanTypeCode || loan.loanPolicyCode || '',
    facilityNumber: loan.facilityNumber,
    dateGranted: dateValue(loan.dateGranted) || today,
    amountGranted: loan.amountGranted,
    loanBalance: loan.outstandingBalance,
    monthlyRepaymentAmount: loan.monthlyRepaymentAmount,
    interestRatePercent: loan.interestRatePercent,
    totalInterest: loan.totalInterest,
    interestRepaymentAmount: loan.interestRepaymentAmount,
    repaymentMode: loan.repaymentMode || 'Amount',
    paymentStartDate: dateValue(loan.paymentStartDate) || today,
    paymentEndDate: dateValue(loan.paymentEndDate),
    numberOfRepayments: loan.numberOfRepayments,
    status: loan.status || 'Active',
    periodOfSuspension: loan.periodOfSuspension ?? 0,
    suspensionStartDate: dateValue(loan.suspensionStartDate),
    suspensionEndDate: dateValue(loan.suspensionEndDate),
    suspensionNarration: loan.suspensionNarration || '',
    generalRemarks: loan.generalRemarks || '',
  };
}

function loanTypeKey(loan: PayrollLoan) {
  return (
    loan.loanPolicyId ||
    loan.loanTypeCode ||
    loan.loanPolicyCode ||
    loan.loanPolicyName ||
    ''
  );
}

function loanTypeLabel(loan: PayrollLoan | null) {
  return (
    loan?.loanPolicyName || loan?.loanTypeCode || loan?.loanPolicyCode || ''
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

export default function LoanTransactionAmendmentPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] =
    useState<PayrollParameterSet | null>(null);
  const [loanPolicies, setLoanPolicies] = useState<PayrollLoanPolicy[]>([]);
  const [loans, setLoans] = useState<PayrollLoan[]>([]);
  const [selectedLoanId, setSelectedLoanId] = useState('');
  const [form, setForm] = useState(defaultForm);
  const [loanTypeSearch, setLoanTypeSearch] = useState('');
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [selectedEmployeeNumber, setSelectedEmployeeNumber] = useState('');
  const [selectedFacilityNumber, setSelectedFacilityNumber] = useState('');
  const [reportType, setReportType] = useState('balances');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, loanList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getLoans({ includeInactive: true }),
      ]);
      setActiveParameters(setup.activeParameters ?? null);
      setLoanPolicies(setup.loanPolicies ?? []);
      setLoans(loanList);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to load loan amendments.'
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const loanTypeOptions = useMemo(() => {
    const map = new Map<string, string>();
    loans.forEach((loan) => {
      const key = loanTypeKey(loan);
      const label = loanTypeLabel(loan);
      if (key && label && !map.has(key)) {
        map.set(key, label);
      }
    });
    return Array.from(map.entries())
      .map(([value, label]) => ({ value, label }))
      .sort((first, second) => first.label.localeCompare(second.label));
  }, [loans]);

  const loansForSelectedType = useMemo(
    () =>
      loanTypeSearch
        ? loans.filter((loan) => loanTypeKey(loan) === loanTypeSearch)
        : [],
    [loanTypeSearch, loans]
  );

  const employeeOptions = useMemo(() => {
    const term = employeeSearch.trim().toLowerCase();
    const map = new Map<string, PayrollLoan>();
    loansForSelectedType.forEach((loan) => {
      const matches =
        !term ||
        loan.employeeNumber.toLowerCase().includes(term) ||
        loan.employeeName.toLowerCase().includes(term) ||
        loan.facilityNumber.toLowerCase().includes(term);
      if (matches && !map.has(loan.employeeNumber)) {
        map.set(loan.employeeNumber, loan);
      }
    });

    return Array.from(map.values()).sort((first, second) =>
      first.employeeNumber.localeCompare(second.employeeNumber)
    );
  }, [employeeSearch, loansForSelectedType]);

  const employeeLoans = useMemo(
    () =>
      loansForSelectedType
        .filter((loan) => loan.employeeNumber === selectedEmployeeNumber)
        .sort((first, second) =>
          first.facilityNumber.localeCompare(second.facilityNumber)
        ),
    [loansForSelectedType, selectedEmployeeNumber]
  );

  const filteredLoans = selectedEmployeeNumber
    ? employeeLoans
    : loansForSelectedType;

  const selectLoan = useCallback((loan: PayrollLoan) => {
    const key = loanTypeKey(loan);
    if (key) {
      setLoanTypeSearch(key);
    }
    setSelectedEmployeeNumber(loan.employeeNumber);
    setSelectedFacilityNumber(loan.facilityNumber);
    setSelectedLoanId(loan.id || '');
    setForm(formFromLoan(loan));
  }, []);

  const selectedLoan = loans.find((loan) => loan.id === selectedLoanId) ?? null;
  const selectedLoanPolicy = loanPolicies.find(
    (policy) => policy.id === form.loanPolicyId
  );
  const currency = activeParameters?.baseCurrency || 'GHS';
  const basicSalary = selectedLoan?.basicSalary ?? 0;
  const principalRepayment =
    form.repaymentMode === 'PercentOfBasic'
      ? (basicSalary * form.monthlyRepaymentAmount) / 100
      : form.monthlyRepaymentAmount;
  const totalRepayment = principalRepayment + form.interestRepaymentAmount;
  const debtRatio = basicSalary > 0 ? (totalRepayment / basicSalary) * 100 : 0;

  const updateForm = <K extends keyof typeof defaultForm>(
    key: K,
    value: (typeof defaultForm)[K]
  ) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const updateCalculatedForm = (patch: Partial<typeof defaultForm>) => {
    setForm((current) =>
      calculateLoanForm(current, selectedLoanPolicy, basicSalary, patch)
    );
  };

  const chooseLoanType = (value: string) => {
    setLoanTypeSearch(value);
    setEmployeeSearch('');
    setSelectedEmployeeNumber('');
    setSelectedFacilityNumber('');
    setSelectedLoanId('');
    setForm(defaultForm);
  };

  const chooseEmployee = (employeeNumber: string) => {
    setSelectedEmployeeNumber(employeeNumber);
    const loan = loansForSelectedType.find(
      (item) => item.employeeNumber === employeeNumber
    );
    if (loan) {
      selectLoan(loan);
    } else {
      setSelectedFacilityNumber('');
      setSelectedLoanId('');
      setForm(defaultForm);
    }
  };

  const chooseFacility = (facilityNumber: string) => {
    setSelectedFacilityNumber(facilityNumber);
    const loan = employeeLoans.find(
      (item) => item.facilityNumber === facilityNumber
    );
    if (loan) {
      selectLoan(loan);
    }
  };

  const recalculateInterest = () => {
    setForm((current) =>
      calculateLoanForm(current, selectedLoanPolicy, basicSalary)
    );
  };

  const markSuspended = () => {
    setForm((current) => ({
      ...current,
      status: 'Suspended',
      periodOfSuspension: current.periodOfSuspension || 1,
      suspensionStartDate: current.suspensionStartDate || today,
      suspensionEndDate:
        current.suspensionEndDate ||
        addSuspensionPeriod(
          current.suspensionStartDate || today,
          current.periodOfSuspension || 1
        ),
    }));
  };

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy('Save');
    setError(null);
    try {
      const saved = await payrollService.upsertLoan({
        id: form.id || undefined,
        employeeProfileId: form.employeeProfileId,
        employeeNumber: form.employeeNumber,
        loanPolicyId: form.loanPolicyId || null,
        loanTypeCode: form.loanTypeCode || null,
        facilityNumber: form.facilityNumber,
        dateGranted: form.dateGranted,
        amountGranted: form.amountGranted,
        outstandingBalance: form.loanBalance,
        monthlyRepaymentAmount: form.monthlyRepaymentAmount,
        interestRatePercent: form.interestRatePercent,
        totalInterest: form.totalInterest,
        interestRepaymentAmount: form.interestRepaymentAmount,
        repaymentMode: form.repaymentMode,
        paymentStartDate: form.paymentStartDate,
        paymentEndDate: form.paymentEndDate || null,
        numberOfRepayments: form.numberOfRepayments,
        status: form.status,
        periodOfSuspension: form.periodOfSuspension || null,
        suspensionStartDate: form.suspensionStartDate || null,
        suspensionEndDate: form.suspensionEndDate || null,
        suspensionNarration: form.suspensionNarration || null,
        generalRemarks: form.generalRemarks || null,
        isActive: form.status !== 'Closed',
      });
      const refreshed = await payrollService.getLoans({
        includeInactive: true,
      });
      setLoans(refreshed);
      selectLoan(saved);
      toast({
        title: 'Loan amendment saved',
        description: `${saved.facilityNumber} updated.`,
      });
    } catch (err) {
      const message =
        err instanceof Error ? err.message : 'Unable to save loan amendment.';
      setError(message);
      toast({
        title: 'Loan amendment error',
        description: message,
        variant: 'destructive',
      });
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading loan transaction amendment
      </main>
    );
  }

  return (
    <main className="space-y-4 p-6">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="outline" size="sm">
              <Link href="/hr/payroll">
                <ArrowLeft className="mr-2 h-4 w-4" />
                Payroll
              </Link>
            </Button>
            <h1 className="text-2xl font-semibold tracking-normal">
              Loan Transaction Amendment
            </h1>
          </div>
        </div>
        <div className="grid min-w-[280px] gap-3 rounded-md border bg-muted/40 p-3 text-sm sm:grid-cols-[1fr_auto] xl:min-w-[420px]">
          <div>
            <div className="text-xs text-muted-foreground">Payroll Period</div>
            <div className="font-semibold">
              {formatPayrollPeriodLabel(activeParameters)}
            </div>
          </div>
          <div className="sm:text-right">
            <div className="text-xs text-muted-foreground">Period No</div>
            <div className="font-semibold">
              {activeParameters?.currentPayPeriod || '-'}
            </div>
          </div>
        </div>
      </div>

      {error && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <Card className="rounded-md">
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Search className="h-4 w-4" />
            Search Existing Loan
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-3 lg:grid-cols-[minmax(220px,300px)_minmax(240px,1fr)_minmax(220px,320px)_minmax(160px,220px)_auto] lg:items-end">
            <Field label="Loan Type">
              <select
                className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                value={loanTypeSearch}
                onChange={(event) => chooseLoanType(event.target.value)}
              >
                <option value="">Select loan type</option>
                {loanTypeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Find Employee">
              <Input
                value={employeeSearch}
                onChange={(event) => setEmployeeSearch(event.target.value)}
                placeholder="Search employee or employee no"
                disabled={!loanTypeSearch}
              />
            </Field>
            <Field label="Employee">
              <select
                className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                value={selectedEmployeeNumber}
                onChange={(event) => chooseEmployee(event.target.value)}
                disabled={!loanTypeSearch}
              >
                <option value="">
                  {loanTypeSearch
                    ? 'Select employee'
                    : 'Select loan type first'}
                </option>
                {employeeOptions.map((loan) => (
                  <option key={loan.employeeNumber} value={loan.employeeNumber}>
                    {loan.employeeNumber} - {loan.employeeName}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Facility No">
              <select
                className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                value={selectedFacilityNumber}
                onChange={(event) => chooseFacility(event.target.value)}
                disabled={!selectedEmployeeNumber}
              >
                <option value="">
                  {selectedEmployeeNumber
                    ? 'Select facility'
                    : 'Select employee first'}
                </option>
                {employeeLoans.map((loan) => (
                  <option key={loan.id} value={loan.facilityNumber}>
                    {loan.facilityNumber}
                  </option>
                ))}
              </select>
            </Field>
            <Button
              type="button"
              variant="outline"
              onClick={() => void loadWorkspace()}
            >
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh
            </Button>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 2xl:grid-cols-[minmax(0,1fr)_360px]">
        <Card className="min-w-0 rounded-md">
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Landmark className="h-4 w-4" />
                Loan Transaction Amendment
              </CardTitle>
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={markSuspended}
                  disabled={!form.id}
                >
                  <PauseCircle className="mr-2 h-4 w-4" />
                  Loan Suspension
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={recalculateInterest}
                  disabled={!form.id}
                >
                  <Calculator className="mr-2 h-4 w-4" />
                  Recalculate Interest
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <form className="grid gap-3" onSubmit={save}>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Field label="Loan Type">
                  <Input readOnly value={loanTypeLabel(selectedLoan)} />
                </Field>
                <Field label="Loan Reference">
                  <Input readOnly value={form.facilityNumber} />
                </Field>
                <Field label="Employee No">
                  <Input readOnly value={form.employeeNumber} />
                </Field>
                <Field label="Employee Name">
                  <Input readOnly value={form.employeeName} />
                </Field>
              </div>

              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Field label="Loan Balance">
                  <Input
                    type="number"
                    step="0.01"
                    value={form.loanBalance}
                    onChange={(event) =>
                      updateForm('loanBalance', Number(event.target.value))
                    }
                  />
                </Field>
                <Field label="Amount Granted">
                  <Input
                    type="number"
                    step="0.01"
                    value={form.amountGranted}
                    onChange={(event) =>
                      updateCalculatedForm({
                        amountGranted: Number(event.target.value),
                      })
                    }
                  />
                </Field>
                <Field label="Date Granted">
                  <Input
                    type="date"
                    value={form.dateGranted}
                    onChange={(event) =>
                      updateForm('dateGranted', event.target.value)
                    }
                  />
                </Field>
                <Field label="Payment Start">
                  <Input
                    type="date"
                    value={form.paymentStartDate}
                    onChange={(event) =>
                      updateCalculatedForm({
                        paymentStartDate: event.target.value,
                      })
                    }
                  />
                </Field>
              </div>

              <div className="rounded-md border p-3">
                <Label className="text-xs text-muted-foreground">
                  Repayment Mode
                </Label>
                <div className="mt-2 grid gap-2 sm:grid-cols-3">
                  {[
                    { value: 'Amount', label: 'Amount' },
                    { value: 'NoOfRepayments', label: 'No of Repayments' },
                    { value: 'PercentOfBasic', label: '% of Basic' },
                  ].map((option) => (
                    <label
                      key={option.value}
                      className="flex items-center gap-2 text-sm"
                    >
                      <input
                        type="radio"
                        checked={form.repaymentMode === option.value}
                        onChange={() =>
                          updateCalculatedForm({ repaymentMode: option.value })
                        }
                      />
                      <span>{option.label}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Field
                  label={
                    form.repaymentMode === 'PercentOfBasic'
                      ? '% of Basic'
                      : 'Repayment Amount'
                  }
                >
                  <Input
                    readOnly={form.repaymentMode === 'NoOfRepayments'}
                    type="number"
                    step="0.01"
                    value={form.monthlyRepaymentAmount}
                    onChange={(event) =>
                      updateCalculatedForm({
                        monthlyRepaymentAmount: Number(event.target.value),
                      })
                    }
                  />
                </Field>
                <Field label="Basic Salary">
                  <Input readOnly value={money(basicSalary, currency)} />
                </Field>
                <Field label="No Repayments">
                  <Input
                    type="number"
                    readOnly={form.repaymentMode !== 'NoOfRepayments'}
                    value={form.numberOfRepayments}
                    onChange={(event) =>
                      updateCalculatedForm({
                        numberOfRepayments: Number(event.target.value),
                      })
                    }
                  />
                </Field>
                <Field label="Payment End">
                  <Input
                    type="date"
                    value={form.paymentEndDate}
                    onChange={(event) =>
                      updateCalculatedForm({
                        paymentEndDate: event.target.value,
                      })
                    }
                  />
                </Field>
              </div>

              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Field label="Interest Rate">
                  <Input
                    type="number"
                    step="0.01"
                    value={form.interestRatePercent}
                    onChange={(event) =>
                      updateCalculatedForm({
                        interestRatePercent: Number(event.target.value),
                      })
                    }
                  />
                </Field>
                <Field label="Total Interest">
                  <Input
                    readOnly
                    type="number"
                    step="0.01"
                    value={form.totalInterest}
                  />
                </Field>
                <Field label="Interest Repayment">
                  <Input
                    readOnly
                    type="number"
                    step="0.01"
                    value={form.interestRepaymentAmount}
                  />
                </Field>
                <Field label="Status">
                  <select
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={form.status}
                    onChange={(event) =>
                      updateForm('status', event.target.value)
                    }
                  >
                    <option value="Active">Active</option>
                    <option value="Suspended">Suspended</option>
                    <option value="Closed">Closed</option>
                  </select>
                </Field>
              </div>

              <div className="grid gap-3 md:grid-cols-3">
                <Field label="Period Of Suspension">
                  <Input
                    type="number"
                    value={form.periodOfSuspension}
                    onChange={(event) => {
                      const periodOfSuspension = Number(event.target.value);
                      setForm((current) => ({
                        ...current,
                        periodOfSuspension,
                        suspensionEndDate: addSuspensionPeriod(
                          current.suspensionStartDate,
                          periodOfSuspension
                        ),
                      }));
                    }}
                  />
                </Field>
                <Field label="Suspension Start Date">
                  <Input
                    type="date"
                    value={form.suspensionStartDate}
                    onChange={(event) => {
                      const suspensionStartDate = event.target.value;
                      setForm((current) => ({
                        ...current,
                        suspensionStartDate,
                        suspensionEndDate: addSuspensionPeriod(
                          suspensionStartDate,
                          current.periodOfSuspension
                        ),
                      }));
                    }}
                  />
                </Field>
                <Field label="Suspension End Date">
                  <Input
                    type="date"
                    value={form.suspensionEndDate}
                    onChange={(event) =>
                      updateForm('suspensionEndDate', event.target.value)
                    }
                  />
                </Field>
              </div>

              <div className="grid gap-3 lg:grid-cols-2">
                <Field label="Suspension Narration">
                  <Textarea
                    className="min-h-20"
                    value={form.suspensionNarration}
                    onChange={(event) =>
                      updateForm('suspensionNarration', event.target.value)
                    }
                  />
                </Field>
                <Field label="General Remarks">
                  <Textarea
                    className="min-h-20"
                    value={form.generalRemarks}
                    onChange={(event) =>
                      updateForm('generalRemarks', event.target.value)
                    }
                  />
                </Field>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="text-sm text-muted-foreground">
                  {selectedLoan
                    ? `${selectedLoan.employeeNumber} / ${selectedLoan.facilityNumber}`
                    : 'No loan selected'}
                </div>
                <Button
                  type="submit"
                  disabled={
                    !form.id ||
                    !form.facilityNumber ||
                    form.amountGranted <= 0 ||
                    busy === 'Save'
                  }
                >
                  {busy === 'Save' ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="mr-2 h-4 w-4" />
                  )}
                  Save Amendment
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <div className="space-y-4">
          <div className="grid gap-2 sm:grid-cols-2 2xl:grid-cols-1">
            <div className="rounded-md border bg-lime-100 p-2.5 text-slate-900">
              <div className="text-xs">Loan Balance</div>
              <div className="text-base font-semibold">
                {money(form.loanBalance, currency)}
              </div>
            </div>
            <div className="rounded-md border bg-lime-100 p-2.5 text-slate-900">
              <div className="text-xs">Total Repayment Amount</div>
              <div className="text-base font-semibold">
                {money(totalRepayment, currency)}
              </div>
            </div>
            <div className="rounded-md border bg-lime-100 p-2.5 text-slate-900">
              <div className="text-xs">Employee Debt Ratio</div>
              <div className="text-base font-semibold">
                {debtRatio.toFixed(2)}
              </div>
            </div>
            <div className="rounded-md border bg-lime-100 p-2.5 text-slate-900">
              <div className="text-xs">Debt Service Ratio</div>
              <div className="text-base font-semibold">
                {(
                  selectedLoanPolicy?.maxDebitRatioPercent ??
                  selectedLoan?.debtServiceRatio ??
                  0
                ).toFixed(2)}
              </div>
            </div>
            <div className="rounded-md border bg-lime-100 p-2.5 text-slate-900">
              <div className="text-xs">Max Loan Amount</div>
              <div className="text-base font-semibold">
                {selectedLoanPolicy?.maxLoanAmount
                  ? money(selectedLoanPolicy.maxLoanAmount, currency)
                  : '-'}
              </div>
            </div>
            <div className="rounded-md border bg-lime-100 p-2.5 text-slate-900">
              <div className="text-xs">Max Payback Period</div>
              <div className="text-base font-semibold">
                {selectedLoanPolicy?.maxPaybackPeriods ?? '-'}
              </div>
            </div>
          </div>

          <Card className="rounded-md">
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 text-base">
                <FileText className="h-4 w-4" />
                Loan Balances Reports
              </CardTitle>
            </CardHeader>
            <CardContent>
              <select
                className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                value={reportType}
                onChange={(event) => setReportType(event.target.value)}
              >
                <option value="balances">Loan Balances Reports</option>
                <option value="schedule">Repayment Schedule</option>
                <option value="suspension">Suspended Loans</option>
              </select>
            </CardContent>
          </Card>

          <Card className="rounded-md">
            <CardHeader className="pb-3">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <CardTitle className="text-base">Matching Loans</CardTitle>
                <PayrollGridExportButton
                  rows={filteredLoans}
                  fileName="loan-amendment-matching-loans"
                  columns={[
                    {
                      header: 'Employee No',
                      value: (row) => row.employeeNumber,
                    },
                    {
                      header: 'Employee Name',
                      value: (row) => row.employeeName,
                    },
                    { header: 'Loan Type', value: (row) => loanTypeLabel(row) },
                    {
                      header: 'Facility Number',
                      value: (row) => row.facilityNumber,
                    },
                    {
                      header: 'Date Granted',
                      value: (row) => dateValue(row.dateGranted),
                    },
                    {
                      header: 'Amount Granted',
                      value: (row) => row.amountGranted,
                    },
                    {
                      header: 'Outstanding Balance',
                      value: (row) => row.outstandingBalance,
                    },
                    { header: 'Status', value: (row) => row.status },
                  ]}
                />
              </div>
            </CardHeader>
            <CardContent className="max-h-[430px] space-y-2 overflow-auto">
              {filteredLoans.length === 0 && (
                <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                  No loans match the search.
                </div>
              )}
              {filteredLoans.map((loan) => (
                <button
                  key={loan.id}
                  type="button"
                  className={`w-full rounded-md border p-3 text-left transition-colors hover:bg-muted/60 ${selectedLoanId === loan.id ? 'border-primary bg-primary/5' : ''}`}
                  onClick={() => selectLoan(loan)}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-medium">
                        {loan.employeeNumber} - {loan.employeeName}
                      </div>
                      <div className="truncate text-xs text-muted-foreground">
                        {loan.loanPolicyName || loan.loanTypeCode || 'Loan'} /{' '}
                        {loan.facilityNumber}
                      </div>
                    </div>
                    <Badge
                      variant={
                        loan.status === 'Closed'
                          ? 'secondary'
                          : loan.status === 'Suspended'
                            ? 'outline'
                            : 'default'
                      }
                    >
                      {loan.status}
                    </Badge>
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-muted-foreground">
                    <span>
                      Balance {money(loan.outstandingBalance, currency)}
                    </span>
                    <span className="text-right">
                      {dateValue(loan.paymentStartDate) || '-'}
                    </span>
                  </div>
                </button>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </main>
  );
}
