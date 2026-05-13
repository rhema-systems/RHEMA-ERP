'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  CreditCard,
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollEmployeeProfile,
  PayrollParameterSet,
  PayrollSalaryAdvance,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);

const defaultForm = {
  id: '',
  employeeProfileId: '',
  employeeNumber: '',
  employeeName: '',
  advanceDate: today,
  advanceAmount: 0,
  description: '',
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

function money(value: number | null | undefined, currency = 'GHS') {
  return new Intl.NumberFormat('en-GH', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value ?? 0);
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

export default function SalaryAdvancePage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [salaryAdvances, setSalaryAdvances] = useState<PayrollSalaryAdvance[]>([]);
  const [form, setForm] = useState(defaultForm);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const applyActivePeriod = useCallback((parameters?: PayrollParameterSet | null) => {
    setActiveParameters(parameters ?? null);
    if (!parameters) {
      return;
    }

    const periodDate = transactionDateForPeriod(parameters.currentPeriodFrom, parameters.currentPeriodTo);
    const periodFrom = dateValue(parameters.currentPeriodFrom);
    const periodTo = dateValue(parameters.currentPeriodTo);

    setForm((current) => ({
      ...current,
      advanceDate: dateInRange(current.advanceDate, periodFrom, periodTo) ? current.advanceDate : periodDate,
    }));
  }, []);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, profileList, advanceList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getEmployeeProfiles(),
        payrollService.getSalaryAdvances(),
      ]);
      applyActivePeriod(setup.activeParameters ?? null);
      setProfiles(profileList);
      setSalaryAdvances(advanceList);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load salary advance.');
    } finally {
      setLoading(false);
    }
  }, [applyActivePeriod]);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const filteredProfiles = useMemo(() => {
    const term = employeeSearch.trim().toLowerCase();
    if (!term) {
      return profiles;
    }

    return profiles.filter((profile) =>
      profile.employeeNumber.toLowerCase().includes(term) ||
      profile.employeeName.toLowerCase().includes(term),
    );
  }, [employeeSearch, profiles]);

  const currentPeriodFrom = dateValue(activeParameters?.currentPeriodFrom);
  const currentPeriodTo = dateValue(activeParameters?.currentPeriodTo);
  const currency = activeParameters?.baseCurrency || 'GHS';

  const chooseProfile = (profile: PayrollEmployeeProfile) => {
    setForm((current) => ({
      ...current,
      employeeProfileId: profile.id || '',
      employeeNumber: profile.employeeNumber,
      employeeName: profile.employeeName,
    }));
  };

  const editAdvance = (advance: PayrollSalaryAdvance) => {
    setForm({
      id: advance.id || '',
      employeeProfileId: advance.employeeProfileId,
      employeeNumber: advance.employeeNumber,
      employeeName: advance.employeeName,
      advanceDate: dateValue(advance.advanceDate) || transactionDateForPeriod(activeParameters?.currentPeriodFrom, activeParameters?.currentPeriodTo),
      advanceAmount: advance.advanceAmount,
      description: advance.description || '',
    });
  };

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await payrollService.upsertSalaryAdvance({
        id: form.id || undefined,
        employeeProfileId: form.employeeProfileId,
        employeeNumber: form.employeeNumber,
        advanceDate: form.advanceDate,
        advanceAmount: form.advanceAmount,
        description: form.description || null,
        isActive: true,
      });
      const [setup, advanceList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getSalaryAdvances(),
      ]);
      applyActivePeriod(setup.activeParameters ?? null);
      setSalaryAdvances(advanceList);
      setForm((current) => ({
        ...defaultForm,
        advanceDate: transactionDateForPeriod(setup.activeParameters?.currentPeriodFrom, setup.activeParameters?.currentPeriodTo, current.advanceDate),
      }));
      toast({ title: 'Salary advance saved', description: `${form.employeeNumber} updated.` });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save salary advance.';
      setError(message);
      toast({ title: 'Salary advance error', description: message, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading salary advance
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
            <h1 className="text-2xl font-semibold tracking-normal">Salary Advance</h1>
          </div>
          <p className="max-w-3xl text-sm text-muted-foreground">
            Enter current-period salary advances for the active payroll month.
          </p>
        </div>
        <div className="grid min-w-[280px] gap-3 rounded-md border bg-muted/40 p-3 text-sm sm:grid-cols-[1fr_auto] xl:min-w-[420px]">
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

      <div className="grid gap-4 xl:grid-cols-[minmax(0,420px)_1fr]">
        <Card className="rounded-md">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <CreditCard className="h-4 w-4" />
              Salary Advance
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search employee no or name"
                value={employeeSearch}
                onChange={(event) => setEmployeeSearch(event.target.value)}
              />
              <Button type="button" variant="outline" onClick={() => void loadWorkspace()}>
                <RefreshCw className="h-4 w-4" />
              </Button>
            </div>
            <form className="grid gap-3" onSubmit={save}>
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Employee No">
                  <select
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={form.employeeProfileId}
                    onChange={(event) => {
                      const profile = profiles.find((item) => item.id === event.target.value);
                      if (profile) {
                        chooseProfile(profile);
                      }
                    }}
                  >
                    <option value="">Select employee</option>
                    {filteredProfiles.map((profile) => (
                      <option key={profile.id || profile.employeeNumber} value={profile.id}>{profile.employeeNumber}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Employee Name">
                  <Input readOnly value={form.employeeName} />
                </Field>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Advance Date">
                  <Input
                    type="date"
                    min={currentPeriodFrom || undefined}
                    max={currentPeriodTo || undefined}
                    value={form.advanceDate}
                    onChange={(event) => setForm((current) => ({ ...current, advanceDate: event.target.value }))}
                  />
                </Field>
                <Field label="Advance Amount">
                  <Input
                    type="number"
                    min="0"
                    step="0.01"
                    value={form.advanceAmount}
                    onChange={(event) => setForm((current) => ({ ...current, advanceAmount: Number(event.target.value) }))}
                  />
                </Field>
              </div>
              <Field label="Remarks">
                <Textarea
                  className="min-h-20"
                  value={form.description}
                  onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                />
              </Field>
              <Button type="submit" disabled={!form.employeeProfileId || form.advanceAmount <= 0 || busy}>
                {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Save Salary Advance
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card className="rounded-md">
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Search className="h-4 w-4" />
                Current Salary Advance Entries
              </CardTitle>
              <PayrollGridExportButton
                rows={salaryAdvances}
                fileName="salary-advance-entries"
                columns={[
                  { header: 'Employee No', value: (row) => row.employeeNumber },
                  { header: 'Employee Name', value: (row) => row.employeeName },
                  { header: 'Advance Date', value: (row) => dateValue(row.advanceDate) },
                  { header: 'Amount', value: (row) => row.advanceAmount },
                  { header: 'Remarks', value: (row) => row.description },
                ]}
              />
            </div>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee No</TableHead>
                  <TableHead>Employee Name</TableHead>
                  <TableHead>Advance Date</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead>Remarks</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {salaryAdvances.map((advance) => (
                  <TableRow key={advance.id} className="cursor-pointer" onClick={() => editAdvance(advance)}>
                    <TableCell className="font-medium">{advance.employeeNumber}</TableCell>
                    <TableCell>{advance.employeeName}</TableCell>
                    <TableCell>{dateValue(advance.advanceDate)}</TableCell>
                    <TableCell className="text-right">{money(advance.advanceAmount, currency)}</TableCell>
                    <TableCell className="max-w-60 truncate">{advance.description || '-'}</TableCell>
                  </TableRow>
                ))}
                {salaryAdvances.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="py-6 text-center text-sm text-muted-foreground">
                      No salary advances saved for this current period.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
