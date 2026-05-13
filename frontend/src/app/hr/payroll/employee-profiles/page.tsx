'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  Loader2,
  Mail,
  Pencil,
  Printer,
  RefreshCw,
  Save,
  Search,
  UserPlus,
  Users,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { PayrollPayslipPreviewDialog } from '@/components/hr/payroll/PayrollPayslipPreviewDialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import {
  HrEmployee,
  PayrollEmployeeProfile,
  PayrollParameterSet,
  PayrollPayslip,
  PayrollPayslipEmailResult,
  PayrollRun,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);

const defaultProfileForm = {
  id: '',
  employeeId: '',
  employeeNumber: '',
  legacyEmployeeId: '',
  legacyEmployeeNumber: '',
  monthlyBasicSalary: 0,
  currencyCode: 'GHS',
  payrollActive: true,
  payTax: true,
  ssfApplicable: true,
  overtimeEligible: false,
  grossUp: false,
  tier2Only: false,
};

const money = (value: number | null | undefined, currency = 'GHS') =>
  new Intl.NumberFormat('en-GH', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value ?? 0);

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
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
  const [profilePayslipRunId, setProfilePayslipRunId] = useState('');
  const [profilePayslipSearch, setProfilePayslipSearch] = useState('');
  const [profileForm, setProfileForm] = useState(defaultProfileForm);
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
      const [setup, runList, profileList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getRuns(),
        payrollService.getEmployeeProfiles(),
      ]);

      setActiveParameters(setup.activeParameters ?? null);
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

      const [setup, runList, profileList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getRuns(),
        payrollService.getEmployeeProfiles(),
      ]);
      setActiveParameters(setup.activeParameters ?? null);
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

  const filteredProfiles = useMemo(() => {
    const term = profilePayslipSearch.trim().toLowerCase();
    if (!term) {
      return profiles;
    }

    return profiles.filter((profile) =>
      profile.employeeNumber.toLowerCase().includes(term) ||
      profile.employeeName.toLowerCase().includes(term) ||
      (profile.legacyEmployeeNumber || '').toLowerCase().includes(term));
  }, [profiles, profilePayslipSearch]);

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
    setProfileForm((current) => ({
      ...current,
      id: '',
      employeeId: employee.id,
      employeeNumber: employee.employeeNumber,
    }));
  };

  const selectProfileForEdit = (profile: PayrollEmployeeProfile) => {
    setProfileForm({
      id: profile.id || '',
      employeeId: profile.employeeId,
      employeeNumber: profile.employeeNumber,
      legacyEmployeeId: profile.legacyEmployeeId || '',
      legacyEmployeeNumber: profile.legacyEmployeeNumber || '',
      monthlyBasicSalary: profile.salaryBasis?.monthlyBasicSalary ?? 0,
      currencyCode: profile.currencyCode || 'GHS',
      payrollActive: profile.payrollActive,
      payTax: profile.payTax,
      ssfApplicable: profile.ssfApplicable,
      overtimeEligible: profile.overtimeEligible,
      grossUp: profile.grossUp,
      tier2Only: profile.tier2Only,
    });
    setEmployeeResults([]);
    setEmployeeSearch('');
  };

  const clearProfileForm = () => {
    setProfileForm(defaultProfileForm);
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

      <div className="grid gap-4 xl:grid-cols-[minmax(0,420px)_1fr]">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <UserPlus className="h-4 w-4" />
              {profileForm.id ? 'Edit Payroll Profile' : 'Payroll Profile'}
            </CardTitle>
            <CardDescription>Connect an ERP HR employee to payroll and assign the payroll salary basis.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
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
                    paymentMethods: [],
                    employeeComponents: [],
                  }),
                );
              }}
            >
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Employee Number">
                  <Input value={profileForm.employeeNumber} onChange={(event) => setProfileForm((current) => ({ ...current, employeeNumber: event.target.value }))} />
                </Field>
                <Field label="Monthly Basic Salary">
                  <Input type="number" step="0.01" value={profileForm.monthlyBasicSalary} onChange={(event) => setProfileForm((current) => ({ ...current, monthlyBasicSalary: Number(event.target.value) }))} />
                </Field>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Legacy No">
                  <Input value={profileForm.legacyEmployeeNumber} onChange={(event) => setProfileForm((current) => ({ ...current, legacyEmployeeNumber: event.target.value }))} />
                </Field>
                <Field label="Currency">
                  <Input value={profileForm.currencyCode} onChange={(event) => setProfileForm((current) => ({ ...current, currencyCode: event.target.value }))} />
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
              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={!profileForm.employeeId || busy === 'Save profile'}>
                  {busy === 'Save profile' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  {profileForm.id ? 'Save Changes' : 'Save Profile'}
                </Button>
                {profileForm.id && (
                  <Button type="button" variant="outline" onClick={clearProfileForm} disabled={busy === 'Save profile'}>
                    New Profile
                  </Button>
                )}
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Users className="h-4 w-4" />
                Active Payroll Profiles
              </CardTitle>
              <div className="flex flex-wrap items-center gap-2">
                <select
                  className="h-9 min-w-[220px] rounded-md border bg-background px-3 text-sm"
                  value={selectedProfilePayslipRunId}
                  onChange={(event) => setProfilePayslipRunId(event.target.value)}
                >
                  <option value="">Select payroll run</option>
                  {runs.map((run) => (
                    <option key={run.id} value={run.id}>{run.runNumber} / {formatPayrollPeriodLabel(run.payPeriodFrom, run.payPeriodTo)}</option>
                  ))}
                </select>
                <Input
                  className="h-9 w-[240px]"
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
                    { header: 'Legacy No', value: (row) => row.legacyEmployeeNumber },
                    { header: 'Basic', value: (row) => row.salaryBasis?.monthlyBasicSalary },
                    { header: 'Currency', value: (row) => row.currencyCode },
                    { header: 'PAYE', value: (row) => row.payTax ? 'Yes' : 'No' },
                    { header: 'SSF', value: (row) => row.ssfApplicable ? 'Yes' : 'No' },
                    { header: 'Overtime', value: (row) => row.overtimeEligible ? 'Yes' : 'No' },
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
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Legacy</TableHead>
                  <TableHead className="text-right">Basic</TableHead>
                  <TableHead>Flags</TableHead>
                  <TableHead className="w-[360px] text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredProfiles.map((profile) => (
                  <TableRow
                    key={profile.id}
                    className={`cursor-pointer hover:bg-muted/60 ${profileForm.id && profileForm.id === profile.id ? 'bg-muted/60' : ''}`}
                    onClick={() => selectProfileForEdit(profile)}
                  >
                    <TableCell>
                      <div className="font-medium">{profile.employeeNumber}</div>
                      <div className="text-xs text-muted-foreground">{profile.employeeName}</div>
                    </TableCell>
                    <TableCell>{profile.legacyEmployeeNumber || '-'}</TableCell>
                    <TableCell className="text-right">{money(profile.salaryBasis?.monthlyBasicSalary, profile.currencyCode)}</TableCell>
                    <TableCell className="space-x-1">
                      {profile.payTax && <Badge variant="secondary">PAYE</Badge>}
                      {profile.ssfApplicable && <Badge variant="secondary">SSF</Badge>}
                      {profile.overtimeEligible && <Badge variant="outline">OT</Badge>}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex flex-wrap justify-end gap-2">
                        <Button type="button" size="sm" variant="outline" onClick={(event) => { event.stopPropagation(); selectProfileForEdit(profile); }}>
                          <Pencil className="mr-2 h-4 w-4" />
                          Edit
                        </Button>
                        <Button type="button" size="sm" variant="outline" disabled={!selectedProfilePayslipRunId || Boolean(busy)} onClick={(event) => { event.stopPropagation(); void handleProfilePayslipAction(profile, 'print'); }}>
                          <Printer className="mr-2 h-4 w-4" />
                          Print
                        </Button>
                        <Button type="button" size="sm" variant="outline" disabled={!selectedProfilePayslipRunId || Boolean(busy)} onClick={(event) => { event.stopPropagation(); void handleProfilePayslipAction(profile, 'email'); }}>
                          <Mail className="mr-2 h-4 w-4" />
                          Email
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

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
