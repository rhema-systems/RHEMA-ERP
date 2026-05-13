'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  CreditCard,
  Landmark,
  Loader2,
  RefreshCw,
  Save,
  Search,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollLoan,
  PayrollLoanSchedule,
  PayrollParameterSet,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);

const defaultForm = {
  payrollLoanId: '',
  employeeNumber: '',
  employeeName: '',
  facilityNumber: '',
  repaymentAmount: 0,
  actualRepaymentDate: today,
};

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function dateInRange(value: string, from?: string | null, to?: string | null) {
  return Boolean(value && (!from || value >= from) && (!to || value <= to));
}

function transactionDateForPeriod(from?: string | null, to?: string | null, currentValue = today) {
  const start = dateValue(from);
  const end = dateValue(to);
  return dateInRange(currentValue, start, end) ? currentValue : start || end || currentValue;
}

function formatPayrollPeriodLabel(parameters: PayrollParameterSet | null) {
  const source = dateValue(parameters?.currentPeriodTo) || dateValue(parameters?.currentPeriodFrom);
  if (!source) {
    return 'Not configured';
  }

  const [year, month] = source.split('-').map(Number);
  if (!year || !month) {
    return 'Not configured';
  }

  return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
}

function amount(value: number | null | undefined) {
  return new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value ?? 0);
}

function scheduleBalance(schedule: PayrollLoanSchedule) {
  return Math.max(
    0,
    Number((schedule.principalAmount + schedule.interestAmount - schedule.amountPaid - (schedule.interestPaid ?? 0)).toFixed(2)),
  );
}

function nextUnpaidSchedule(loan: PayrollLoan | null, periodTo?: string) {
  return loan?.schedules
    ?.slice()
    .sort((first, second) => first.sequenceNo - second.sequenceNo)
    .find((schedule) => scheduleBalance(schedule) > 0 && (!periodTo || dateValue(schedule.repaymentDate) <= periodTo)) ?? null;
}

function loanType(loan: PayrollLoan | null) {
  return loan?.loanPolicyName || loan?.loanTypeCode || loan?.loanPolicyCode || '';
}

function isActiveRepayableLoan(loan: PayrollLoan, periodTo?: string) {
  return loan.isActive &&
    loan.status?.toLowerCase() === 'active' &&
    loan.outstandingBalance > 0 &&
    nextUnpaidSchedule(loan, periodTo) !== null;
}

function formFromLoan(loan: PayrollLoan | null, parameters?: PayrollParameterSet | null) {
  const schedule = nextUnpaidSchedule(loan, dateValue(parameters?.currentPeriodTo));
  const amount = schedule
    ? scheduleBalance(schedule)
    : (loan?.totalRepaymentAmount || ((loan?.monthlyRepaymentAmount ?? 0) + (loan?.interestRepaymentAmount ?? 0)));

  return {
    payrollLoanId: loan?.id || '',
    employeeNumber: loan?.employeeNumber || '',
    employeeName: loan?.employeeName || '',
    facilityNumber: loan?.facilityNumber || '',
    repaymentAmount: Number(amount.toFixed(2)),
    actualRepaymentDate: transactionDateForPeriod(parameters?.currentPeriodFrom, parameters?.currentPeriodTo),
  };
}

function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1">
      <Label className="text-[11px] text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

function ReadonlyInput({ value, className = '' }: { value: string | number; className?: string }) {
  const heightClass = /\bh-\d/.test(className) ? '' : 'h-9';

  return (
    <Input
      className={`${heightClass} bg-muted/40 ${className}`}
      value={value}
      readOnly
    />
  );
}

function CompactInfoField({
  label,
  value,
  align = 'left',
}: {
  label: string;
  value: string | number;
  align?: 'left' | 'right';
}) {
  return (
    <div className="grid grid-cols-[128px_minmax(0,1fr)] items-center gap-3">
      <Label className="truncate text-xs leading-4 text-muted-foreground">{label}</Label>
      <ReadonlyInput
        value={value}
        className={`h-9 px-3 text-sm ${align === 'right' ? 'text-right' : ''}`}
      />
    </div>
  );
}

export default function LoanRepaymentPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [loans, setLoans] = useState<PayrollLoan[]>([]);
  const [form, setForm] = useState(defaultForm);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedLoan = loans.find((loan) => loan.id === form.payrollLoanId) ?? null;
  const periodTo = dateValue(activeParameters?.currentPeriodTo);
  const nextSchedule = nextUnpaidSchedule(selectedLoan, periodTo);

  const repayableLoans = useMemo(() => loans.filter((loan) => isActiveRepayableLoan(loan, periodTo)), [loans, periodTo]);

  const employeeOptions = useMemo(() => {
    const byEmployee = new Map<string, PayrollLoan>();
    repayableLoans.forEach((loan) => {
      if (!byEmployee.has(loan.employeeNumber)) {
        byEmployee.set(loan.employeeNumber, loan);
      }
    });

    return Array.from(byEmployee.values()).sort((first, second) =>
      first.employeeNumber.localeCompare(second.employeeNumber),
    );
  }, [repayableLoans]);

  const filteredEmployeeOptions = useMemo(() => {
    const term = employeeSearch.trim().toLowerCase();
    if (!term) {
      return employeeOptions;
    }

    return employeeOptions.filter((loan) =>
      loan.employeeNumber.toLowerCase().includes(term) ||
      loan.employeeName.toLowerCase().includes(term) ||
      loan.facilityNumber.toLowerCase().includes(term) ||
      loanType(loan).toLowerCase().includes(term),
    );
  }, [employeeOptions, employeeSearch]);

  const employeeLoans = useMemo(() => repayableLoans
    .filter((loan) => loan.employeeNumber === form.employeeNumber)
    .sort((first, second) => first.facilityNumber.localeCompare(second.facilityNumber)),
  [form.employeeNumber, repayableLoans]);

  const selectLoan = useCallback((loan: PayrollLoan | null, parameters = activeParameters) => {
    setForm(formFromLoan(loan, parameters));
  }, [activeParameters]);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, loanList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getLoans({ includeInactive: true }),
      ]);
      const parameters = setup.activeParameters ?? null;
      setActiveParameters(parameters);
      setLoans(loanList);
      setForm((current) => {
        if (current.payrollLoanId) {
          const refreshedSelectedLoan = loanList.find((loan) => loan.id === current.payrollLoanId) ?? null;
          return refreshedSelectedLoan ? formFromLoan(refreshedSelectedLoan, parameters) : current;
        }

        const firstLoan = loanList.find((loan) =>
          isActiveRepayableLoan(loan, dateValue(parameters?.currentPeriodTo)),
        ) ?? null;

        return formFromLoan(firstLoan, parameters);
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load loan repayment.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const chooseEmployee = (employeeNumber: string) => {
    const loan = repayableLoans.find((item) => item.employeeNumber === employeeNumber) ?? null;
    selectLoan(loan);
    setEmployeeSearch('');
  };

  const chooseFacility = (facilityNumber: string) => {
    const loan = repayableLoans.find((item) =>
      item.employeeNumber === form.employeeNumber &&
      item.facilityNumber === facilityNumber,
    ) ?? null;
    selectLoan(loan);
  };

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (!selectedLoan?.id) {
        throw new Error('Select a loan facility before saving.');
      }

      const result = await payrollService.postLoanRepayment({
        payrollLoanId: selectedLoan.id,
        employeeNumber: form.employeeNumber,
        facilityNumber: form.facilityNumber,
        repaymentAmount: form.repaymentAmount,
        actualRepaymentDate: form.actualRepaymentDate,
      });

      const refreshedLoans = await payrollService.getLoans({ includeInactive: true });
      setLoans(refreshedLoans);
      selectLoan(result.loan, activeParameters);
      toast({
        title: 'Loan repayment saved',
        description: `${result.facilityNumber} balance is now ${amount(result.loanBalance)}.`,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save loan repayment.';
      setError(message);
      toast({ title: 'Loan repayment error', description: message, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading loan repayment
      </main>
    );
  }

  return (
    <main className="space-y-3 p-4">
      <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="outline" size="sm">
              <Link href="/hr/payroll">
                <ArrowLeft className="mr-2 h-4 w-4" />
                Payroll
              </Link>
            </Button>
            <h1 className="text-2xl font-semibold tracking-normal">Loan Repayment</h1>
          </div>
        </div>
        <div className="grid min-w-[280px] gap-2 rounded-md border bg-muted/40 p-2.5 text-sm sm:grid-cols-[1fr_auto] xl:min-w-[420px]">
          <div>
            <div className="text-xs text-muted-foreground">Payroll Period</div>
            <div className="font-semibold">{formatPayrollPeriodLabel(activeParameters)}</div>
          </div>
          <div className="sm:text-right">
            <div className="text-xs text-muted-foreground">Period No</div>
            <div className="font-semibold">{activeParameters?.currentPayPeriod || '-'}</div>
          </div>
        </div>
      </div>

      {error && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="grid gap-4 xl:grid-cols-[minmax(500px,620px)_minmax(0,1fr)]">
        <form onSubmit={save}>
          <Card className="rounded-md">
            <CardHeader className="p-3">
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Landmark className="h-4 w-4" />
                  Loan Repayment
                </CardTitle>
                <div className="flex flex-wrap gap-2">
                  <Button type="button" size="sm" variant="outline" onClick={() => void loadWorkspace()} disabled={busy}>
                    <RefreshCw className="mr-2 h-4 w-4" />
                    Refresh
                  </Button>
                  <Button type="submit" size="sm" disabled={busy || !selectedLoan}>
                    {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                    Save
                  </Button>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-3 p-3 pt-0">
              <div className="grid gap-2.5 md:grid-cols-2">
                <Field label="Employee No">
                  <div className="relative mb-1.5">
                    <Search className="pointer-events-none absolute left-3 top-2 h-4 w-4 text-muted-foreground" />
                    <Input
                      className="h-8 pl-9"
                      value={employeeSearch}
                      onChange={(event) => setEmployeeSearch(event.target.value)}
                      placeholder="Search active loan employee"
                    />
                  </div>
                  <select
                    className="h-8 w-full rounded-md border bg-background px-3 text-sm"
                    value={form.employeeNumber}
                    onChange={(event) => chooseEmployee(event.target.value)}
                  >
                    <option value="">{filteredEmployeeOptions.length ? 'Select employee' : 'No active loan employee found'}</option>
                    {filteredEmployeeOptions.map((loan) => (
                      <option key={loan.employeeNumber} value={loan.employeeNumber}>
                        {loan.employeeNumber} - {loan.employeeName}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Employee Name">
                  <ReadonlyInput value={form.employeeName} />
                </Field>
                <Field label="Facility No">
                  <select
                    className="h-8 w-full rounded-md border bg-background px-3 text-sm"
                    value={form.facilityNumber}
                    onChange={(event) => chooseFacility(event.target.value)}
                    disabled={!form.employeeNumber}
                  >
                    <option value="">Select facility</option>
                    {employeeLoans.map((loan) => (
                      <option key={loan.id} value={loan.facilityNumber}>
                        {loan.facilityNumber}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Repayment Amount">
                  <Input
                    className="h-8"
                    type="number"
                    min="0"
                    step="0.01"
                    value={form.repaymentAmount}
                    onChange={(event) => setForm((current) => ({ ...current, repaymentAmount: Number(event.target.value) }))}
                  />
                </Field>
              </div>

              <div className="rounded-md border p-4">
                <div className="mb-4 flex items-center gap-2 text-sm font-semibold">
                  <CreditCard className="h-3.5 w-3.5" />
                  Loan Information
                </div>
                <div className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
                  <CompactInfoField label="Type of Loan" value={loanType(selectedLoan)} />
                  <CompactInfoField label="Granted Date" value={dateValue(selectedLoan?.dateGranted)} />
                  <CompactInfoField label="Start Date" value={dateValue(selectedLoan?.paymentStartDate)} />
                  <CompactInfoField label="End Date" value={dateValue(selectedLoan?.paymentEndDate)} />
                  <CompactInfoField label="Amount Granted" value={amount(selectedLoan?.amountGranted)} align="right" />
                  <CompactInfoField label="Monthly Repay." value={amount(selectedLoan?.monthlyRepaymentAmount)} align="right" />
                  <CompactInfoField label="Loan Balance" value={amount(selectedLoan?.outstandingBalance)} align="right" />
                  <CompactInfoField label="No Repayments" value={selectedLoan?.numberOfRepayments ?? ''} align="right" />
                  <CompactInfoField label="Interest Rate" value={selectedLoan ? `${selectedLoan.interestRatePercent.toFixed(2)}%` : ''} align="right" />
                  <CompactInfoField label="Total Interest" value={amount(selectedLoan?.totalInterest)} align="right" />
                  <CompactInfoField label="Interest Repay." value={amount(selectedLoan?.interestRepaymentAmount)} align="right" />
                  <CompactInfoField label="Loan Sequence" value={nextSchedule?.sequenceNo ?? ''} align="right" />
                  <CompactInfoField label="Schedule Date" value={dateValue(nextSchedule?.repaymentDate)} />
                </div>
              </div>
            </CardContent>
          </Card>
        </form>

        <Card className="rounded-md">
          <CardHeader className="p-3">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <CreditCard className="h-4 w-4" />
                Repayment History
              </CardTitle>
              <PayrollGridExportButton
                rows={selectedLoan?.schedules ?? []}
                fileName={`loan-repayment-history-${selectedLoan?.facilityNumber || 'facility'}`}
                columns={[
                  { header: 'Sequence', value: (row) => row.sequenceNo },
                  { header: 'Repayment Date', value: (row) => dateValue(row.repaymentDate) },
                  { header: 'Principal', value: (row) => row.principalAmount },
                  { header: 'Interest', value: (row) => row.interestAmount },
                  { header: 'Amount Paid', value: (row) => row.amountPaid },
                  { header: 'Interest Paid', value: (row) => row.interestPaid },
                  { header: 'Actual Date', value: (row) => dateValue(row.actualRepaymentDate) },
                  { header: 'Balance', value: (row) => scheduleBalance(row) },
                  { header: 'Posted', value: (row) => row.posted ? 'Yes' : 'No' },
                ]}
                disabled={!selectedLoan}
              />
            </div>
          </CardHeader>
          <CardContent className="p-3 pt-0">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] text-sm">
                <thead>
                  <tr className="border-b text-left text-xs text-muted-foreground">
                    <th className="px-2 py-2 font-medium">Seq</th>
                    <th className="px-2 py-2 font-medium">Repayment Date</th>
                    <th className="px-2 py-2 text-right font-medium">Principal</th>
                    <th className="px-2 py-2 text-right font-medium">Interest</th>
                    <th className="px-2 py-2 text-right font-medium">Amount Paid</th>
                    <th className="px-2 py-2 text-right font-medium">Interest Paid</th>
                    <th className="px-2 py-2 font-medium">Actual Date</th>
                    <th className="px-2 py-2 text-right font-medium">Balance</th>
                    <th className="px-2 py-2 font-medium">Posted</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedLoan?.schedules?.map((schedule) => (
                    <tr key={schedule.id} className="border-b last:border-b-0">
                      <td className="px-2 py-2">{schedule.sequenceNo}</td>
                      <td className="px-2 py-2">{dateValue(schedule.repaymentDate) || '-'}</td>
                      <td className="px-2 py-2 text-right">{amount(schedule.principalAmount)}</td>
                      <td className="px-2 py-2 text-right">{amount(schedule.interestAmount)}</td>
                      <td className="px-2 py-2 text-right">{amount(schedule.amountPaid)}</td>
                      <td className="px-2 py-2 text-right">{amount(schedule.interestPaid)}</td>
                      <td className="px-2 py-2">{dateValue(schedule.actualRepaymentDate) || '-'}</td>
                      <td className="px-2 py-2 text-right">{amount(scheduleBalance(schedule))}</td>
                      <td className="px-2 py-2">{schedule.posted ? 'Yes' : 'No'}</td>
                    </tr>
                  ))}
                  {(!selectedLoan?.schedules || selectedLoan.schedules.length === 0) && (
                    <tr>
                      <td colSpan={9} className="px-2 py-6 text-center text-sm text-muted-foreground">
                        No repayment history for the selected loan.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
