'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  CheckSquare,
  Clock,
  Loader2,
  RefreshCw,
  Save,
  Search,
  Trash2,
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
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollEmployeeProfile,
  PayrollOvertimeSummaryEntry,
  PayrollParameterSet,
  payrollService,
} from '@/services/payrollService';

type OvertimeRow = {
  id?: string;
  employeeProfileId: string;
  employeeNumber: string;
  employeeName: string;
  absentDays: number;
  normalDays: number;
  weekdayDays: number;
  holidayDays: number;
  saturdayDays: number;
  sundayDays: number;
  selected: boolean;
};

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function formatPayrollPeriodLabel(parameters: PayrollParameterSet | null) {
  const source = dateValue(parameters?.currentPeriodTo) || dateValue(parameters?.currentPeriodFrom);
  if (!source) {
    return 'Not configured';
  }

  const [year, month] = source.split('-').map(Number);
  return year && month
    ? new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1))
    : 'Not configured';
}

function numberValue(value: number | null | undefined) {
  return Number.isFinite(value ?? 0) ? Number(value ?? 0) : 0;
}

function sortProfiles(first: PayrollEmployeeProfile, second: PayrollEmployeeProfile) {
  return first.employeeNumber.localeCompare(second.employeeNumber);
}

function buildRows(entries: PayrollOvertimeSummaryEntry[]) {
  return entries
    .slice()
    .sort((first, second) => first.employeeNumber.localeCompare(second.employeeNumber))
    .map((entry) => ({
      id: entry.id,
      employeeProfileId: entry.employeeProfileId,
      employeeNumber: entry.employeeNumber,
      employeeName: entry.employeeName,
      absentDays: numberValue(entry.absentDays),
      normalDays: numberValue(entry.normalDays),
      weekdayDays: numberValue(entry.weekdayDays),
      holidayDays: numberValue(entry.holidayDays),
      saturdayDays: numberValue(entry.saturdayDays),
      sundayDays: numberValue(entry.sundayDays),
      selected: true,
    }));
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

export default function OvertimeSummaryPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [rows, setRows] = useState<OvertimeRow[]>([]);
  const [removedRows, setRemovedRows] = useState<OvertimeRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeePickerOpen, setEmployeePickerOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<OvertimeRow | null>(null);
  const [gridDirty, setGridDirty] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

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
        payrollService.getOvertimeSummaries(),
      ]);
      setActiveParameters(setup.activeParameters ?? null);
      setProfiles(profileList);
      setRows(buildRows(savedEntries));
      setRemovedRows([]);
      setGridDirty(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load overtime summary.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const createRowFromProfile = useCallback((profile: PayrollEmployeeProfile): OvertimeRow => ({
    employeeProfileId: profile.id || '',
    employeeNumber: profile.employeeNumber,
    employeeName: profile.employeeName,
    absentDays: 0,
    normalDays: 0,
    weekdayDays: 0,
    holidayDays: 0,
    saturdayDays: 0,
    sundayDays: 0,
    selected: true,
  }), []);

  const updateRow = (employeeProfileId: string, patch: Partial<OvertimeRow>) => {
    setGridDirty(true);
    setRows((current) =>
      current.map((row) =>
        row.employeeProfileId === employeeProfileId ? { ...row, ...patch } : row,
      ),
    );
  };

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

  const removeRow = (row: OvertimeRow) => {
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
    setBusy('save');
    setError(null);
    try {
      const saved = await payrollService.saveOvertimeSummaries({
        legacyCompanyCode: activeParameters?.legacyCompanyCode ?? null,
        entries: [
          ...rows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            absentDays: row.absentDays,
            normalDays: row.normalDays,
            weekdayDays: row.weekdayDays,
            holidayDays: row.holidayDays,
            saturdayDays: row.saturdayDays,
            sundayDays: row.sundayDays,
            isSelected: true,
          })),
          ...removedRows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            absentDays: row.absentDays,
            normalDays: row.normalDays,
            weekdayDays: row.weekdayDays,
            holidayDays: row.holidayDays,
            saturdayDays: row.saturdayDays,
            sundayDays: row.sundayDays,
            isSelected: false,
          })),
        ],
      });

      setRows(buildRows(saved));
      setRemovedRows([]);
      setGridDirty(false);
      toast({ title: 'Overtime summary saved', description: `${saved.length} employee record${saved.length === 1 ? '' : 's'} active.` });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save overtime summary.';
      setError(message);
      toast({ title: 'Overtime summary error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading overtime summary
      </main>
    );
  }

  return (
    <main className="space-y-4 p-6">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div className="flex flex-wrap items-center gap-2">
          <Button asChild variant="outline" size="sm">
            <Link href="/hr/payroll">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Payroll
            </Link>
          </Button>
          <h1 className="text-2xl font-semibold tracking-normal">Overtime Summary</h1>
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
              <div className="grid flex-1 gap-3 md:grid-cols-[minmax(260px,1fr)_120px]">
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
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => void loadWorkspace()} disabled={Boolean(busy)}>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Refresh
                </Button>
                <PayrollGridExportButton
                  rows={filteredRows}
                  fileName="overtime-summary"
                  columns={[
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Absent Hrs', value: (row) => row.absentDays },
                    { header: 'Normal Hrs', value: (row) => row.normalDays },
                    { header: 'Week Day Hrs', value: (row) => row.weekdayDays },
                    { header: 'Holiday Hrs', value: (row) => row.holidayDays },
                    { header: 'Saturday Hrs', value: (row) => row.saturdayDays },
                    { header: 'Sunday Hrs', value: (row) => row.sundayDays },
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
              <table className="min-w-[1020px] w-full text-sm">
                <thead className="bg-muted/60 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="w-10 px-3 py-2 text-left">
                      <Clock className="h-4 w-4" />
                    </th>
                    <th className="px-3 py-2 text-left">Employee No</th>
                    <th className="px-3 py-2 text-left">Employee Name</th>
                    <th className="w-24 px-3 py-2 text-right">Absent Hrs</th>
                    <th className="w-24 px-3 py-2 text-right">Normal Hrs</th>
                    <th className="w-24 px-3 py-2 text-right">Week Day Hrs</th>
                    <th className="w-24 px-3 py-2 text-right">Holiday Hrs</th>
                    <th className="w-24 px-3 py-2 text-right">Saturday Hrs</th>
                    <th className="w-24 px-3 py-2 text-right">Sunday Hrs</th>
                    <th className="w-12 px-3 py-2" aria-label="Remove" />
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.length === 0 ? (
                    <tr>
                      <td colSpan={10} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No overtime employees selected.
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
                        {(['absentDays', 'normalDays', 'weekdayDays', 'holidayDays', 'saturdayDays', 'sundayDays'] as const).map((field) => (
                          <td key={field} className="px-3 py-2">
                            <Input
                              type="number"
                              min="0"
                              step="0.01"
                              className="h-8 text-right"
                              value={row[field]}
                              onChange={(event) => updateRow(row.employeeProfileId, { [field]: Number(event.target.value) })}
                            />
                          </td>
                        ))}
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
            ? `Remove ${deleteTarget.employeeNumber} - ${deleteTarget.employeeName} from this overtime grid? Save the page to persist the removal.`
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
                    <th className="px-3 py-2 text-left">Employee No</th>
                    <th className="px-3 py-2 text-left">Employee Name</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredPickerProfiles.length === 0 ? (
                    <tr>
                      <td colSpan={3} className="px-3 py-8 text-center text-sm text-muted-foreground">
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
