'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  Database,
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
  PayrollCodeSetup,
  PayrollContributionOpeningBalance,
  PayrollEmployeeProfile,
  PayrollParameterSet,
  payrollService,
} from '@/services/payrollService';

type ContributionOption = {
  key: string;
  codeType: string;
  code: string;
  name: string;
};

type OpeningBalanceRow = {
  id?: string;
  employeeProfileId: string;
  employeeNumber: string;
  employeeName: string;
  openingBalance: number;
  balanceAsAt: string;
  selected: boolean;
};

const CONTRIBUTION_CODE_TYPE_ALIASES = ['CON', 'CONT', 'CONTR', 'CONTRIB'];

function normalize(value?: string | null) {
  return (value || '').trim().toUpperCase();
}

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function dateBeforeCurrentPeriod(parameters: PayrollParameterSet | null) {
  const periodFrom = dateValue(parameters?.currentPeriodFrom);
  if (!periodFrom) {
    return new Date().toISOString().slice(0, 10);
  }

  const [year, month, day] = periodFrom.split('-').map(Number);
  const date = new Date(year, (month || 1) - 1, day || 1);
  date.setDate(date.getDate() - 1);
  return date.toISOString().slice(0, 10);
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

function money(value: number | null | undefined, currency = 'GHS') {
  return new Intl.NumberFormat('en-GH', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value ?? 0);
}

function sortProfiles(first: PayrollEmployeeProfile, second: PayrollEmployeeProfile) {
  return first.employeeNumber.localeCompare(second.employeeNumber);
}

function contributionKey(option: Pick<ContributionOption, 'codeType' | 'code'>) {
  return `${option.codeType}::${option.code}`;
}

function findContributionCodeType(setup: PayrollCodeSetup) {
  return setup.codeTypes.find((type) => CONTRIBUTION_CODE_TYPE_ALIASES.includes(normalize(type.codeType))) ??
    setup.codeTypes.find((type) => normalize(type.description).includes('CONTRIBUTION'));
}

function contributionOptionsFromSetup(setup: PayrollCodeSetup, preferredCodeType?: string | null) {
  const targetCodeType = normalize(preferredCodeType) || normalize(findContributionCodeType(setup)?.codeType);
  return setup.codeValues
    .filter((value) => !value.blocked && (!targetCodeType || normalize(value.codeType) === targetCodeType))
    .map((value) => ({
      key: contributionKey({ codeType: value.codeType, code: value.actualCode }),
      codeType: value.codeType,
      code: value.actualCode,
      name: value.description,
    }))
    .sort((first, second) => first.name.localeCompare(second.name));
}

function buildRows(entries: PayrollContributionOpeningBalance[]) {
  return entries
    .slice()
    .sort((first, second) => first.employeeNumber.localeCompare(second.employeeNumber))
    .map((entry) => ({
      id: entry.id,
      employeeProfileId: entry.employeeProfileId,
      employeeNumber: entry.employeeNumber,
      employeeName: entry.employeeName,
      openingBalance: entry.openingBalance ?? 0,
      balanceAsAt: dateValue(entry.balanceAsAt),
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

export default function OpeningBalancePage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [codeSetup, setCodeSetup] = useState<PayrollCodeSetup>({ codeTypes: [], codeValues: [] });
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [selectedContributionKey, setSelectedContributionKey] = useState('');
  const [rows, setRows] = useState<OpeningBalanceRow[]>([]);
  const [removedRows, setRemovedRows] = useState<OpeningBalanceRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeePickerOpen, setEmployeePickerOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<OpeningBalanceRow | null>(null);
  const [gridDirty, setGridDirty] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const contributionOptions = useMemo(() => contributionOptionsFromSetup(codeSetup), [codeSetup]);
  const selectedContribution = useMemo(
    () => contributionOptions.find((option) => option.key === selectedContributionKey) ?? null,
    [contributionOptions, selectedContributionKey],
  );
  const activeProfiles = useMemo(
    () => profiles.filter((profile) => profile.payrollActive).sort(sortProfiles),
    [profiles],
  );
  const pickerSelectedSet = useMemo(() => new Set(pickerSelectedIds), [pickerSelectedIds]);
  const defaultBalanceAsAt = useMemo(() => dateBeforeCurrentPeriod(activeParameters), [activeParameters]);
  const currency = activeParameters?.baseCurrency || 'GHS';

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

  const loadBalances = useCallback(async (contribution: ContributionOption | null) => {
    if (!contribution) {
      setRows([]);
      setRemovedRows([]);
      setGridDirty(false);
      return;
    }

    setBusy('load-balances');
    setError(null);
    try {
      const saved = await payrollService.getContributionOpeningBalances({
        contributionCodeType: contribution.codeType,
        contributionCode: contribution.code,
      });
      setRows(buildRows(saved));
      setRemovedRows([]);
      setGridDirty(false);
      setEmployeeSearch('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load opening balances.');
    } finally {
      setBusy(null);
    }
  }, []);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, profileList, allCodes] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getEmployeeProfiles(),
        payrollService.getCodeSetup(),
      ]);
      const contributionCodeType = findContributionCodeType(allCodes);
      const scopedCodes = contributionCodeType
        ? await payrollService.getCodeSetup(contributionCodeType.codeType)
        : allCodes;
      const options = contributionOptionsFromSetup(scopedCodes, contributionCodeType?.codeType);
      const nextContribution = options[0] ?? null;

      setActiveParameters(setup.activeParameters ?? null);
      setProfiles(profileList);
      setCodeSetup(scopedCodes);
      setSelectedContributionKey(nextContribution?.key ?? '');

      if (nextContribution) {
        const saved = await payrollService.getContributionOpeningBalances({
          contributionCodeType: nextContribution.codeType,
          contributionCode: nextContribution.code,
        });
        setRows(buildRows(saved));
      } else {
        setRows([]);
      }
      setRemovedRows([]);
      setGridDirty(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load opening balance.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const selectContribution = (key: string) => {
    setSelectedContributionKey(key);
    const contribution = contributionOptions.find((option) => option.key === key) ?? null;
    void loadBalances(contribution);
  };

  const createRowFromProfile = useCallback((profile: PayrollEmployeeProfile): OpeningBalanceRow => ({
    employeeProfileId: profile.id || '',
    employeeNumber: profile.employeeNumber,
    employeeName: profile.employeeName,
    openingBalance: 0,
    balanceAsAt: defaultBalanceAsAt,
    selected: true,
  }), [defaultBalanceAsAt]);

  const updateRow = (employeeProfileId: string, patch: Partial<OpeningBalanceRow>) => {
    setGridDirty(true);
    setRows((current) =>
      current.map((row) => row.employeeProfileId === employeeProfileId ? { ...row, ...patch } : row),
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

  const removeRow = (row: OpeningBalanceRow) => {
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
    if (!selectedContribution) {
      setError('Contribution is required.');
      return;
    }

    const missingDate = rows.find((row) => !row.balanceAsAt);
    if (missingDate) {
      setError(`Balance As At is required for ${missingDate.employeeNumber}.`);
      return;
    }

    setBusy('save');
    setError(null);
    try {
      const saved = await payrollService.saveContributionOpeningBalances({
        contributionCodeType: selectedContribution.codeType,
        contributionCode: selectedContribution.code,
        contributionName: selectedContribution.name,
        legacyCompanyCode: activeParameters?.legacyCompanyCode ?? null,
        entries: [
          ...rows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            openingBalance: row.openingBalance,
            balanceAsAt: row.balanceAsAt,
            isSelected: true,
          })),
          ...removedRows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            openingBalance: row.openingBalance,
            balanceAsAt: row.balanceAsAt,
            isSelected: false,
          })),
        ],
      });
      setRows(buildRows(saved));
      setRemovedRows([]);
      setGridDirty(false);
      toast({ title: 'Opening balance saved', description: `${saved.length} employee record${saved.length === 1 ? '' : 's'} active.` });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save opening balance.';
      setError(message);
      toast({ title: 'Opening balance error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading opening balance
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
          <h1 className="text-2xl font-semibold tracking-normal">Opening Balance</h1>
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
              <div className="grid flex-1 gap-3 md:grid-cols-[minmax(220px,320px)_minmax(260px,1fr)_120px]">
                <Field label="Contribution">
                  <select
                    className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                    value={selectedContributionKey}
                    onChange={(event) => selectContribution(event.target.value)}
                  >
                    <option value="">Select contribution</option>
                    {contributionOptions.map((option) => (
                      <option key={option.key} value={option.key}>{option.name}</option>
                    ))}
                  </select>
                </Field>
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
                  fileName={`opening-balance-${selectedContribution?.code || 'employees'}`}
                  columns={[
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Contribution', value: () => selectedContribution?.name },
                    { header: 'Opening Balance', value: (row) => row.openingBalance },
                    { header: 'Balance As At', value: (row) => row.balanceAsAt },
                  ]}
                  disabled={!selectedContribution}
                />
                <Button type="button" size="sm" onClick={openEmployeePicker} disabled={Boolean(busy) || !selectedContribution}>
                  <UserPlus className="mr-2 h-4 w-4" />
                  Add Employee
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  variant={gridDirty ? 'warning' : 'default'}
                  disabled={busy === 'save' || !selectedContribution}
                >
                  {busy === 'save' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto rounded-md border">
              <table className="min-w-[840px] w-full text-sm">
                <thead className="bg-muted/60 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="w-10 px-3 py-2 text-left">
                      <Database className="h-4 w-4" />
                    </th>
                    <th className="px-3 py-2 text-left">Employee No</th>
                    <th className="px-3 py-2 text-left">Employee Name</th>
                    <th className="w-44 px-3 py-2 text-right">Opening Balance</th>
                    <th className="w-44 px-3 py-2 text-left">Balance As At</th>
                    <th className="w-12 px-3 py-2" aria-label="Remove" />
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No employees selected.
                      </td>
                    </tr>
                  ) : (
                    filteredRows.map((row) => (
                      <tr key={row.employeeProfileId} className="border-t">
                        <td className="px-3 py-2">
                          <Checkbox checked disabled />
                        </td>
                        <td className="px-3 py-2 font-medium">{row.employeeNumber}</td>
                        <td className="px-3 py-2">{row.employeeName}</td>
                        <td className="px-3 py-2">
                          <Input
                            type="number"
                            min="0"
                            step="0.01"
                            className="h-8 text-right"
                            value={row.openingBalance}
                            onChange={(event) => updateRow(row.employeeProfileId, { openingBalance: Number(event.target.value) })}
                          />
                        </td>
                        <td className="px-3 py-2">
                          <Input
                            type="date"
                            className="h-8"
                            value={row.balanceAsAt}
                            onChange={(event) => updateRow(row.employeeProfileId, { balanceAsAt: event.target.value })}
                          />
                        </td>
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
            <div className="mt-3 text-right text-sm font-medium">
              Total {money(rows.reduce((sum, row) => sum + Number(row.openingBalance || 0), 0), currency)}
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
            ? `Remove ${deleteTarget.employeeNumber} - ${deleteTarget.employeeName} from this opening balance grid? Save the page to persist the removal.`
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
