'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  Loader2,
  Mail,
  Printer,
  RefreshCw,
  Search,
  UserPlus,
  Users,
} from 'lucide-react';

// ⚠ Round-2 lane E1: the profile editor that lived inline in this page's dialog is now a
// component with two hosts — this dialog and HR's employee Salary tab. This is the single permitted
// edit to app/hr/payroll/** under the plan's § 1.2, and it REMOVES code from here.
import { PayrollEmployeeProfileEditor, dateValue, money } from '@/components/hr/payroll/PayrollEmployeeProfileEditor';
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
  PayrollEmployeeProfile,
  PayrollParameterSet,
  PayrollPayslip,
  PayrollPayslipEmailResult,
  PayrollRun,
  payrollService,
} from '@/services/payrollService';


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


export default function PayrollEmployeeProfilesPage() {
  const { toast } = useToast();
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [profilePayslipRunId, setProfilePayslipRunId] = useState('');
  const [profilePayslipSearch, setProfilePayslipSearch] = useState('');
  const [profilePage, setProfilePage] = useState(1);
  const [profilePageSize, setProfilePageSize] = useState(25);
  // Who the dialog is for: an existing profile picked from the list, or an HR employee chosen
  // through the search for a new one. The editor owns everything else about the form.
  const [editingProfile, setEditingProfile] = useState<PayrollEmployeeProfile | null>(null);
  const [selectedEmployee, setSelectedEmployee] = useState<{ id: string; employeeNumber: string } | null>(null);
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
    setEditingProfile(null);
    setSelectedEmployee({ id: employee.id, employeeNumber: employee.employeeNumber });
    setEmployeeResults([]);
  };

  const selectProfileForEdit = (profile: PayrollEmployeeProfile) => {
    setEditingProfile(profile);
    setSelectedEmployee({ id: profile.employeeId, employeeNumber: profile.employeeNumber });
    setProfileDialogOpen(true);
    setEmployeeResults([]);
    setEmployeeSearch('');
  };

  const clearProfileForm = () => {
    setEditingProfile(null);
    setSelectedEmployee(null);
    setEmployeeResults([]);
    setEmployeeSearch('');
  };

  // The editor re-reads before every save (the replace-set guard). Payroll has no by-employee read,
  // so this is its own list filtered by number — the same stopgap HR's door uses server-side.
  const reloadEditingProfile = async () => {
    if (!selectedEmployee) return null;
    const matches = await payrollService.getEmployeeProfiles(selectedEmployee.employeeNumber);
    return matches.find((p) => p.employeeId === selectedEmployee.id) ?? null;
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

      <Dialog open={profileDialogOpen} onOpenChange={setProfileDialogOpen}>
        <DialogContent className="max-h-[92vh] max-w-[1180px] overflow-y-auto p-5">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-base">
              <UserPlus className="h-4 w-4" />
              {editingProfile?.id ? 'Edit Payroll Profile' : 'Payroll Profile'}
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
            {selectedEmployee ? (
              <PayrollEmployeeProfileEditor
                employeeId={selectedEmployee.id}
                employeeNumber={selectedEmployee.employeeNumber}
                initialProfile={editingProfile}
                reload={reloadEditingProfile}
                onSaved={(saved) => {
                  setEditingProfile(saved);
                  void loadWorkspace();
                }}
              />
            ) : (
              <p className="text-sm text-muted-foreground">Search for an HR employee above to create their payroll profile.</p>
            )}
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
                      className={`group cursor-pointer hover:bg-muted/60 ${editingProfile?.id && editingProfile.id === profile.id ? 'bg-muted/60' : ''}`}
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
