'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  CheckSquare,
  Loader2,
  RefreshCw,
  Save,
  Search,
  Trash2,
  TrendingUp,
  UserPlus,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollEmployeeProfile,
  PayrollParameterSet,
  PayrollPromotionArrearsEntry,
  payrollService,
} from '@/services/payrollService';

type PromotionArrearsRow = {
  id?: string;
  employeeProfileId: string;
  employeeNumber: string;
  employeeName: string;
  effectiveDate: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  legacyCompanyCode?: string | null;
  basicSalary?: number | null;
  workingDays?: number | null;
  selected: boolean;
};

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function parseDateInput(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, (month || 1) - 1, day || 1);
}

function toDateInputValue(value: Date) {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function defaultEffectiveDate(parameters: PayrollParameterSet | null) {
  const periodFrom = dateValue(parameters?.currentPeriodFrom);
  if (!periodFrom) {
    return toDateInputValue(new Date());
  }

  const from = parseDateInput(periodFrom);
  return toDateInputValue(new Date(from.getFullYear(), from.getMonth() - 1, 1));
}

function maxEffectiveDate(parameters: PayrollParameterSet | null) {
  const periodFrom = dateValue(parameters?.currentPeriodFrom);
  if (!periodFrom) {
    return undefined;
  }

  const maxDate = parseDateInput(periodFrom);
  maxDate.setDate(maxDate.getDate() - 1);
  return toDateInputValue(maxDate);
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

function sortProfiles(first: PayrollEmployeeProfile, second: PayrollEmployeeProfile) {
  return first.employeeNumber.localeCompare(second.employeeNumber);
}

function buildRowsFromAssignments(
  assignments: PayrollPromotionArrearsEntry[],
  profiles: PayrollEmployeeProfile[],
) {
  const profilesById = new Map(profiles.map((profile) => [profile.id || '', profile]));

  return assignments
    .slice()
    .sort((first, second) => first.employeeNumber.localeCompare(second.employeeNumber))
    .map((existing) => {
      const profile = profilesById.get(existing.employeeProfileId);
      return {
        id: existing.id,
        employeeProfileId: existing.employeeProfileId,
        employeeNumber: existing.employeeNumber || profile?.employeeNumber || '',
        employeeName: existing.employeeName || profile?.employeeName || '',
        effectiveDate: dateValue(existing.effectiveDate),
        payPeriod: existing.payPeriod,
        payPeriodFrom: dateValue(existing.payPeriodFrom),
        payPeriodTo: dateValue(existing.payPeriodTo),
        legacyCompanyCode: existing.legacyCompanyCode,
        basicSalary: existing.basicSalary ?? profile?.salaryBasis?.monthlyBasicSalary ?? 0,
        workingDays: existing.workingDays,
        selected: true,
      };
    });
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
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

export default function PromotionArrearsPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [rows, setRows] = useState<PromotionArrearsRow[]>([]);
  const [removedRows, setRemovedRows] = useState<PromotionArrearsRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeePickerOpen, setEmployeePickerOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<PromotionArrearsRow | null>(null);
  const [gridDirty, setGridDirty] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const effectiveDateMax = useMemo(() => maxEffectiveDate(activeParameters), [activeParameters]);

  const activeProfiles = useMemo(
    () => profiles.filter((profile) => profile.payrollActive).sort(sortProfiles),
    [profiles],
  );

  const pickerSelectedSet = useMemo(() => new Set(pickerSelectedIds), [pickerSelectedIds]);

  const filteredRows = useMemo(() => {
    const term = employeeSearch.trim().toLowerCase();
    if (!term) {
      return rows;
    }

    return rows.filter((row) =>
      row.employeeNumber.toLowerCase().includes(term) ||
      row.employeeName.toLowerCase().includes(term),
    );
  }, [employeeSearch, rows]);

  const filteredPickerProfiles = useMemo(() => {
    const term = pickerSearch.trim().toLowerCase();
    if (!term) {
      return activeProfiles;
    }

    return activeProfiles.filter((profile) =>
      profile.employeeNumber.toLowerCase().includes(term) ||
      profile.employeeName.toLowerCase().includes(term),
    );
  }, [activeProfiles, pickerSearch]);

  const selectablePickerIds = useMemo(
    () => activeProfiles.map((profile) => profile.id || '').filter(Boolean),
    [activeProfiles],
  );
  const allPickerProfilesSelected = selectablePickerIds.length > 0 &&
    selectablePickerIds.every((id) => pickerSelectedSet.has(id));
  const pickerSelectAllState = allPickerProfilesSelected
    ? true
    : pickerSelectedIds.length > 0
      ? 'indeterminate'
      : false;

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, profileList, savedEntries] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getEmployeeProfiles(),
        payrollService.getPromotionArrears(),
      ]);

      setActiveParameters(setup.activeParameters ?? null);
      setProfiles(profileList);
      setRows(buildRowsFromAssignments(savedEntries, profileList));
      setRemovedRows([]);
      setGridDirty(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load promotion arrears.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const updateRow = (employeeProfileId: string, patch: Partial<PromotionArrearsRow>) => {
    setGridDirty(true);
    setRows((current) =>
      current.map((row) =>
        row.employeeProfileId === employeeProfileId
          ? { ...row, ...patch }
          : row,
      ),
    );
  };

  const createRowFromProfile = useCallback((profile: PayrollEmployeeProfile): PromotionArrearsRow => ({
    employeeProfileId: profile.id || '',
    employeeNumber: profile.employeeNumber,
    employeeName: profile.employeeName,
    effectiveDate: defaultEffectiveDate(activeParameters),
    payPeriod: activeParameters?.currentPayPeriod ?? 0,
    payPeriodFrom: dateValue(activeParameters?.currentPeriodFrom),
    payPeriodTo: dateValue(activeParameters?.currentPeriodTo),
    legacyCompanyCode: activeParameters?.legacyCompanyCode ?? null,
    basicSalary: profile.salaryBasis?.monthlyBasicSalary ?? 0,
    workingDays: null,
    selected: true,
  }), [activeParameters]);

  const openEmployeePicker = () => {
    setPickerSearch('');
    setPickerSelectedIds(rows.map((row) => row.employeeProfileId));
    setEmployeePickerOpen(true);
  };

  const togglePickerProfile = (employeeProfileId: string, selected: boolean) => {
    setPickerSelectedIds((current) => {
      if (selected) {
        return current.includes(employeeProfileId) ? current : [...current, employeeProfileId];
      }

      return current.filter((id) => id !== employeeProfileId);
    });
  };

  const setVisiblePickerSelection = (selected: boolean) => {
    const visibleIds = new Set(filteredPickerProfiles.map((profile) => profile.id || '').filter(Boolean));
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
    setGridDirty(true);
    const selectedIds = new Set(pickerSelectedIds);
    const existingRowsByProfile = new Map(rows.map((row) => [row.employeeProfileId, row]));
    const activeProfileIds = new Set(activeProfiles.map((profile) => profile.id || '').filter(Boolean));
    const retainedRows = rows.filter((row) => selectedIds.has(row.employeeProfileId) && !activeProfileIds.has(row.employeeProfileId));
    const rowsToRemove = rows.filter((row) => !selectedIds.has(row.employeeProfileId));
    const nextRows = activeProfiles
      .filter((profile) => selectedIds.has(profile.id || ''))
      .map((profile) => existingRowsByProfile.get(profile.id || '') ?? createRowFromProfile(profile));

    setRows([...retainedRows, ...nextRows]);
    setRemovedRows((current) => {
      const removedByProfile = new Map(current.map((row) => [row.employeeProfileId, row]));
      rowsToRemove
        .filter((row) => row.id)
        .forEach((row) => removedByProfile.set(row.employeeProfileId, { ...row, selected: false }));
      selectedIds.forEach((employeeProfileId) => removedByProfile.delete(employeeProfileId));
      return Array.from(removedByProfile.values());
    });
    setEmployeeSearch('');
    setEmployeePickerOpen(false);
  };

  const removeRow = (row: PromotionArrearsRow) => {
    setGridDirty(true);
    setRows((current) => current.filter((item) => item.employeeProfileId !== row.employeeProfileId));
    if (row.id) {
      setRemovedRows((current) =>
        current.some((item) => item.employeeProfileId === row.employeeProfileId)
          ? current
          : [...current, { ...row, selected: false }],
      );
    }
  };

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const missingDate = rows.find((row) => !row.effectiveDate);
    if (missingDate) {
      setError(`Effective date is required for ${missingDate.employeeNumber}.`);
      return;
    }

    if (effectiveDateMax) {
      const invalidDate = rows.find((row) => row.effectiveDate > effectiveDateMax);
      if (invalidDate) {
        setError(`Effective date for ${invalidDate.employeeNumber} must be before the current payroll period starts.`);
        return;
      }
    }

    setBusy('save');
    setError(null);
    try {
      const saved = await payrollService.savePromotionArrears({
        legacyCompanyCode: activeParameters?.legacyCompanyCode ?? null,
        entries: [
          ...rows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            effectiveDate: row.effectiveDate,
            workingDays: row.workingDays ?? null,
            basicSalary: row.basicSalary ?? null,
            isSelected: true,
          })),
          ...removedRows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            effectiveDate: row.effectiveDate,
            workingDays: row.workingDays ?? null,
            basicSalary: row.basicSalary ?? null,
            isSelected: false,
          })),
        ],
      });

      setRows(buildRowsFromAssignments(saved, profiles));
      setRemovedRows([]);
      setGridDirty(false);
      toast({
        title: 'Promotion arrears saved',
        description: `${saved.length} employee record${saved.length === 1 ? '' : 's'} active.`,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save promotion arrears.';
      setError(message);
      toast({ title: 'Promotion arrears error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading promotion arrears
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
            <h1 className="text-2xl font-semibold tracking-normal">Promotion Arrears</h1>
          </div>
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

      <form className="space-y-4" onSubmit={save}>
        <Card className="rounded-md">
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
              <div className="grid flex-1 gap-3 md:grid-cols-[minmax(260px,1fr)_130px_160px]">
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
                    {rows.length}
                  </div>
                </Field>
                <Field label="Effective Date Cutoff">
                  <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm">
                    {effectiveDateMax || '-'}
                  </div>
                </Field>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => void loadWorkspace()} disabled={Boolean(busy)}>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Refresh
                </Button>
                <PayrollGridExportButton
                  rows={filteredRows}
                  fileName="promotion-arrears"
                  columns={[
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Effective Date', value: (row) => row.effectiveDate },
                    { header: 'Basic Salary', value: (row) => row.basicSalary },
                    { header: 'Working Days', value: (row) => row.workingDays },
                    { header: 'Pay Period', value: (row) => row.payPeriod },
                    { header: 'Period From', value: (row) => row.payPeriodFrom },
                    { header: 'Period To', value: (row) => row.payPeriodTo },
                  ]}
                />
                <Button type="button" size="sm" onClick={openEmployeePicker} disabled={Boolean(busy)}>
                  <UserPlus className="mr-2 h-4 w-4" />
                  Add Employee
                </Button>
                <Button type="submit" size="sm" variant={gridDirty ? 'warning' : 'default'} disabled={busy === 'save'}>
                  {busy === 'save' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto rounded-md border">
              <table className="min-w-[980px] w-full text-sm">
                <thead className="bg-muted/60 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="w-10 px-3 py-2 text-left">
                      <TrendingUp className="h-4 w-4" />
                    </th>
                    <th className="w-[120px] px-3 py-2 text-left">Employee No</th>
                    <th className="px-3 py-2 text-left">Employee Name</th>
                    <th className="w-44 px-3 py-2 text-left">Effective Date</th>
                    <th className="w-36 whitespace-nowrap px-3 py-2 text-right">Basic Salary</th>
                    <th className="w-32 whitespace-nowrap px-3 py-2 text-right">Working Days</th>
                    <th className="w-28 px-3 py-2 text-right">Pay Period</th>
                    <th className="w-12 px-3 py-2" aria-label="Remove" />
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No promotion arrears employees selected.
                      </td>
                    </tr>
                  ) : (
                    filteredRows.map((row) => (
                      <tr key={row.employeeProfileId} className="border-t">
                        <td className="px-3 py-2">
                          <CheckSquare className="h-4 w-4 text-emerald-600" />
                        </td>
                        <td className="px-3 py-2 font-medium">{row.employeeNumber}</td>
                        <td className="px-3 py-2">{row.employeeName}</td>
                        <td className="px-3 py-2">
                          <Input
                            type="date"
                            className="h-8"
                            value={row.effectiveDate}
                            max={effectiveDateMax}
                            onChange={(event) => updateRow(row.employeeProfileId, { effectiveDate: event.target.value })}
                          />
                        </td>
                        <td className="px-3 py-2">
                          <Input
                            type="number"
                            min="0"
                            step="0.01"
                            className="h-8 text-right"
                            value={row.basicSalary ?? 0}
                            onChange={(event) => updateRow(row.employeeProfileId, { basicSalary: Number(event.target.value) || 0 })}
                          />
                        </td>
                        <td className="px-3 py-2">
                          <Input
                            type="number"
                            min="0"
                            step="0.01"
                            className="h-8 text-right"
                            value={row.workingDays ?? ''}
                            onChange={(event) => updateRow(row.employeeProfileId, { workingDays: event.target.value === '' ? null : Number(event.target.value) || 0 })}
                          />
                        </td>
                        <td className="px-3 py-2 text-right tabular-nums">{row.payPeriod || activeParameters?.currentPayPeriod || '-'}</td>
                        <td className="px-3 py-2 text-center">
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8 text-destructive hover:text-destructive"
                            aria-label={`Remove ${row.employeeName}`}
                            onClick={() => setDeleteTarget(row)}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      </form>

      <ConfirmationDialog
        open={Boolean(deleteTarget)}
        onOpenChange={(open) => {
          if (!open) {
            setDeleteTarget(null);
          }
        }}
        title="Remove Employee?"
        description={
          deleteTarget
            ? `Remove ${deleteTarget.employeeNumber} - ${deleteTarget.employeeName} from this promotion arrears grid? Save the page to persist the removal.`
            : undefined
        }
        confirmText="Remove"
        variant="destructive"
        onConfirm={() => {
          if (deleteTarget) {
            removeRow(deleteTarget);
          }
          setDeleteTarget(null);
        }}
      />

      <Dialog open={employeePickerOpen} onOpenChange={setEmployeePickerOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Add Employee</DialogTitle>
            <DialogDescription>Select active payroll employees for promotion arrears.</DialogDescription>
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
                <span className="text-sm text-muted-foreground">
                  {pickerSelectedIds.length} selected
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setAllPickerSelection(true)}
                  disabled={selectablePickerIds.length === 0 || allPickerProfilesSelected}
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
                <Button type="button" variant="outline" size="sm" onClick={() => setVisiblePickerSelection(true)} disabled={filteredPickerProfiles.length === 0}>
                  Select Visible
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setVisiblePickerSelection(false)} disabled={filteredPickerProfiles.length === 0}>
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
                  {filteredPickerProfiles.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No employees found.
                      </td>
                    </tr>
                  ) : (
                    filteredPickerProfiles.map((profile) => {
                      const profileId = profile.id || '';
                      return (
                        <tr key={profileId || profile.employeeNumber} className="border-t">
                          <td className="px-3 py-2">
                            <Checkbox
                              checked={pickerSelectedSet.has(profileId)}
                              onCheckedChange={(value) => togglePickerProfile(profileId, value === true)}
                              disabled={!profileId}
                            />
                          </td>
                          <td className="px-3 py-2 font-medium">{profile.employeeNumber}</td>
                          <td className="px-3 py-2">{profile.employeeName}</td>
                          <td className="px-3 py-2 text-right tabular-nums">{amount(profile.salaryBasis?.monthlyBasicSalary)}</td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setEmployeePickerOpen(false)}>
              Cancel
            </Button>
            <Button type="button" onClick={addPickerEmployees}>
              <UserPlus className="mr-2 h-4 w-4" />
              Add Selected
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </main>
  );
}
