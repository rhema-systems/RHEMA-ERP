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
  UserPlus,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
  PayrollCalculationType,
  PayrollCodeType,
  PayrollCodeValue,
  PayrollEmployeeProfile,
  PayrollEmployeeTaxRelief,
  PayrollParameterSet,
  PayrollTaxRelief,
  payrollService,
} from '@/services/payrollService';

type ReliefForm = {
  code: string;
  name: string;
  amount: number;
  calculationType: PayrollCalculationType;
};

type ReliefRow = {
  id?: string;
  employeeProfileId: string;
  employeeNumber: string;
  employeeName: string;
  monthlyBasicSalary: number;
  amount: number;
  factor: number;
  selected: boolean;
};

const defaultReliefForm: ReliefForm = {
  code: '',
  name: '',
  amount: 0,
  calculationType: 'FixedAmount',
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
  if (!year || !month) {
    return 'Not configured';
  }

  return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
}

function amount(value: number | null | undefined) {
  return new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value ?? 0);
}

function numberValue(value: string) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function reliefTotal(calculationType: PayrollCalculationType, row: Pick<ReliefRow, 'monthlyBasicSalary' | 'amount' | 'factor'>) {
  const baseAmount = calculationType === 'PercentageOfBasic'
    ? row.monthlyBasicSalary * row.amount / 100
    : row.amount;
  return Math.round(baseAmount * (row.factor || 1) * 100) / 100;
}

function findTaxReliefCodeType(codeTypes: PayrollCodeType[]) {
  const aliases = new Set(['REL', 'REF', 'TRL', 'TXR', 'TAXREL', 'TREL']);

  return codeTypes.find((item) => {
    const code = item.codeType.trim().toUpperCase();
    const description = item.description.trim().toUpperCase();
    return aliases.has(code) || description.includes('TAX RELIEF') || description.includes('RELIEF');
  });
}

function buildReliefOptions(codeValues: PayrollCodeValue[], setupReliefs: PayrollTaxRelief[]) {
  const options = new Map<string, PayrollCodeValue>();

  codeValues
    .filter((item) => !item.blocked)
    .forEach((item) => {
      options.set(item.actualCode, item);
    });

  setupReliefs.forEach((item) => {
    if (!options.has(item.code)) {
      options.set(item.code, {
        id: item.id,
        codeType: 'REL',
        actualCode: item.code,
        description: item.name || item.code,
        blocked: !item.isActive,
        additionalDescription: null,
        accountCode: null,
        dependentActualCode: null,
        dependentCodeType: null,
        legacyCompanyCode: null,
      });
    }
  });

  return Array.from(options.values()).sort((first, second) =>
    first.description.localeCompare(second.description),
  );
}

function sortProfiles(first: PayrollEmployeeProfile, second: PayrollEmployeeProfile) {
  return first.employeeNumber.localeCompare(second.employeeNumber);
}

function normalizeReliefCode(code: string) {
  return code.trim().toUpperCase();
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

export default function TaxReliefPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [setupReliefs, setSetupReliefs] = useState<PayrollTaxRelief[]>([]);
  const [reliefCodeValues, setReliefCodeValues] = useState<PayrollCodeValue[]>([]);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [relief, setRelief] = useState<ReliefForm>(defaultReliefForm);
  const [rows, setRows] = useState<ReliefRow[]>([]);
  const [removedRows, setRemovedRows] = useState<ReliefRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeePickerOpen, setEmployeePickerOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ReliefRow | null>(null);
  const [gridDirty, setGridDirty] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reliefOptions = useMemo(
    () => buildReliefOptions(reliefCodeValues, setupReliefs),
    [reliefCodeValues, setupReliefs],
  );

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

  const selectedCount = rows.length;
  const selectedTotal = rows
    .reduce((total, row) => total + reliefTotal(relief.calculationType, row), 0);

  const buildRows = useCallback((assignments: PayrollEmployeeTaxRelief[], selectedRelief = relief) => {
    const profilesById = new Map(profiles.map((profile) => [profile.id || '', profile]));

    setRows(
      assignments
        .slice()
        .sort((first, second) => first.employeeNumber.localeCompare(second.employeeNumber))
        .map((existing) => {
          const profile = profilesById.get(existing.employeeProfileId);
          return {
            id: existing.id,
            employeeProfileId: existing.employeeProfileId,
            employeeNumber: existing.employeeNumber || profile?.employeeNumber || '',
            employeeName: existing.employeeName || profile?.employeeName || '',
            monthlyBasicSalary: profile?.salaryBasis?.monthlyBasicSalary ?? 0,
            amount: existing.amount ?? selectedRelief.amount,
            factor: existing.factor || 1,
            selected: Boolean(existing),
          };
        }),
    );
    setRemovedRows([]);
    setGridDirty(false);
  }, [profiles, relief]);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, codeSetup, profileList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getCodeSetup(),
        payrollService.getEmployeeProfiles(),
      ]);

      const taxReliefCodeType = findTaxReliefCodeType(codeSetup.codeTypes);
      const taxReliefCodes = taxReliefCodeType
        ? await payrollService.getCodeSetup(taxReliefCodeType.codeType)
        : { codeValues: [] };

      setActiveParameters(setup.activeParameters ?? null);
      setSetupReliefs(setup.taxReliefs ?? []);
      setReliefCodeValues(taxReliefCodes.codeValues ?? []);
      setProfiles(profileList);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load tax relief.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const selectRelief = async (code: string) => {
    const normalizedCode = normalizeReliefCode(code);
    const option = reliefOptions.find((item) => normalizeReliefCode(item.actualCode) === normalizedCode);
    const setupRelief = setupReliefs.find((item) => normalizeReliefCode(item.code) === normalizedCode);
    const selectedRelief = {
      code: normalizedCode,
      name: option?.description || setupRelief?.name || code,
      amount: setupRelief?.amount ?? 0,
      calculationType: setupRelief?.calculationType ?? 'FixedAmount',
    };

    setRelief(selectedRelief);
    setRows([]);
    setRemovedRows([]);
    setGridDirty(false);
    setEmployeeSearch('');
    setPickerSearch('');
    setPickerSelectedIds([]);

    if (!normalizedCode) {
      return;
    }

    setBusy('search');
    setError(null);
    try {
      const assignments = await payrollService.getEmployeeTaxReliefs({ reliefCode: normalizedCode });
      buildRows(assignments, selectedRelief);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load saved tax relief employees.');
    } finally {
      setBusy(null);
    }
  };

  const updateRow = (employeeProfileId: string, patch: Partial<ReliefRow>) => {
    setGridDirty(true);
    setRows((current) =>
      current.map((row) =>
        row.employeeProfileId === employeeProfileId
          ? { ...row, ...patch }
          : row,
      ),
    );
  };

  const createRowFromProfile = useCallback((profile: PayrollEmployeeProfile): ReliefRow => ({
    employeeProfileId: profile.id || '',
    employeeNumber: profile.employeeNumber,
    employeeName: profile.employeeName,
    monthlyBasicSalary: profile.salaryBasis?.monthlyBasicSalary ?? 0,
    amount: relief.amount,
    factor: 1,
    selected: true,
  }), [relief.amount]);

  const openEmployeePicker = () => {
    if (!relief.code) {
      setError('Select a tax relief before adding employees.');
      return;
    }

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

  const removeRow = (row: ReliefRow) => {
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
    if (!relief.code) {
      setError('Select a tax relief before saving.');
      return;
    }

    setBusy('save');
    setError(null);
    try {
      const saved = await payrollService.saveEmployeeTaxReliefs({
        reliefCode: relief.code,
        reliefName: relief.name || relief.code,
        calculationType: relief.calculationType,
        amount: relief.amount,
        legacyCompanyCode: activeParameters?.legacyCompanyCode ?? null,
        entries: [
          ...rows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            amount: row.amount,
            factor: row.factor,
            isSelected: true,
          })),
          ...removedRows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            amount: row.amount,
            factor: row.factor,
            isSelected: false,
          })),
        ],
      });

      buildRows(saved);
      toast({
        title: 'Tax relief saved',
        description: `${saved.length} employee relief record${saved.length === 1 ? '' : 's'} active for ${relief.name || relief.code}.`,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save tax relief.';
      setError(message);
      toast({ title: 'Tax relief error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading tax relief
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
            <h1 className="text-2xl font-semibold tracking-normal">Tax Relief</h1>
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
              <div className="grid flex-1 gap-3 md:grid-cols-[minmax(260px,1fr)_160px_130px]">
                <Field label="Tax Relief">
                  <select
                    className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                    value={relief.code}
                    onChange={(event) => void selectRelief(event.target.value)}
                  >
                    <option value="">Select tax relief</option>
                    {reliefOptions.map((item) => (
                      <option key={item.id || `${item.codeType}-${item.actualCode}`} value={normalizeReliefCode(item.actualCode)}>
                        {item.description}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Relief Amount">
                  <Input
                    className="h-9 text-right"
                    type="number"
                    step="0.01"
                    value={relief.amount}
                    onChange={(event) => setRelief((current) => ({ ...current, amount: numberValue(event.target.value) }))}
                  />
                </Field>
                <Field label="Percentage?">
                  <label className="flex h-9 items-center gap-2 rounded-md border px-3 text-sm">
                    <Checkbox
                      checked={relief.calculationType === 'PercentageOfBasic'}
                      onCheckedChange={(checked) =>
                        setRelief((current) => ({
                          ...current,
                          calculationType: checked === true ? 'PercentageOfBasic' : 'FixedAmount',
                        }))
                      }
                    />
                    <span>{relief.calculationType === 'PercentageOfBasic' ? 'Yes' : 'No'}</span>
                  </label>
                </Field>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => void loadWorkspace()} disabled={Boolean(busy)}>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Refresh
                </Button>
                <Button type="button" size="sm" variant="outline" onClick={openEmployeePicker} disabled={!relief.code}>
                  <UserPlus className="mr-2 h-4 w-4" />
                  Add Employees
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  variant={gridDirty ? 'warning' : 'default'}
                  disabled={busy === 'save' || !relief.code || (rows.length === 0 && removedRows.length === 0)}
                >
                  {busy === 'save' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save
                </Button>
              </div>
            </div>
          </CardHeader>
        </Card>

        <Card className="rounded-md">
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <CheckSquare className="h-4 w-4" />
                Employees
              </CardTitle>
              <div className="flex flex-wrap items-center gap-2">
                <div className="text-sm text-muted-foreground">
                  Employees: <span className="font-medium text-foreground">{selectedCount}</span>
                  <span className="mx-2">|</span>
                  Total Relief: <span className="font-medium text-foreground">{amount(selectedTotal)}</span>
                </div>
                <PayrollGridExportButton
                  rows={filteredRows}
                  fileName={`tax-relief-${relief.code || 'employees'}`}
                  columns={[
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Amount', value: (row) => row.amount },
                    { header: 'Factor', value: (row) => row.factor },
                    { header: 'Total Relief', value: (row) => reliefTotal(relief.calculationType, row) },
                  ]}
                  disabled={!relief.code}
                />
                <div className="relative">
                  <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    className="h-9 w-[260px] pl-9"
                    value={employeeSearch}
                    onChange={(event) => setEmployeeSearch(event.target.value)}
                    placeholder="Search employees"
                  />
                </div>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[880px] text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-left text-xs text-muted-foreground">
                    <th className="w-[150px] px-2 py-2 font-medium">Employee No</th>
                    <th className="px-2 py-2 font-medium">Employee Name</th>
                    <th className="w-[150px] px-2 py-2 text-right font-medium">Amount</th>
                    <th className="w-[110px] px-2 py-2 text-right font-medium">Factor</th>
                    <th className="w-[150px] px-2 py-2 text-right font-medium">Total Relief</th>
                    <th className="w-12 px-2 py-2 text-right font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.map((row) => (
                    <tr key={row.employeeProfileId} className="border-b last:border-b-0">
                      <td className="px-2 py-1.5 align-middle font-medium">{row.employeeNumber}</td>
                      <td className="px-2 py-1.5 align-middle">{row.employeeName}</td>
                      <td className="px-2 py-1.5 align-middle">
                        <Input
                          className="h-8 text-right"
                          type="number"
                          step="0.01"
                          value={row.amount}
                          onChange={(event) => updateRow(row.employeeProfileId, { amount: numberValue(event.target.value) })}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <Input
                          className="h-8 text-right"
                          type="number"
                          step="0.0001"
                          value={row.factor}
                          onChange={(event) => updateRow(row.employeeProfileId, { factor: numberValue(event.target.value) })}
                        />
                      </td>
                      <td className="px-2 py-1.5 text-right align-middle font-medium">
                        {amount(reliefTotal(relief.calculationType, row))}
                      </td>
                      <td className="px-2 py-1.5 text-right align-middle">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="text-destructive hover:text-destructive"
                          aria-label={`Remove ${row.employeeName}`}
                          onClick={() => setDeleteTarget(row)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                  {filteredRows.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-2 py-8 text-center text-sm text-muted-foreground">
                        {relief.code ? 'Click Search to load saved entries or Add Employees to build this tax relief list.' : 'Select a tax relief to begin.'}
                      </td>
                    </tr>
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
            ? `Remove ${deleteTarget.employeeNumber} - ${deleteTarget.employeeName} from this tax relief grid? Save the page to persist the removal.`
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
        <DialogContent className="flex max-h-[88vh] w-[95vw] max-w-5xl flex-col gap-3 overflow-hidden p-4 sm:rounded-lg">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-lg">
              <UserPlus className="h-5 w-5" />
              Add Employees
            </DialogTitle>
            <DialogDescription>
              Active payroll employees for {relief.name || relief.code}.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div className="relative md:w-[360px]">
              <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="h-9 pl-9"
                value={pickerSearch}
                onChange={(event) => setPickerSearch(event.target.value)}
                placeholder="Search employee no or name"
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

          <div className="min-h-0 flex-1 overflow-auto rounded-md border">
            <table className="w-full min-w-[760px] text-sm">
              <thead className="sticky top-0 z-10 bg-muted/80 backdrop-blur">
                <tr className="border-b text-left text-xs text-muted-foreground">
                  <th className="w-10 px-2 py-2 font-medium">
                    <Checkbox
                      aria-label="Select all employees"
                      checked={pickerSelectAllState}
                      onCheckedChange={(checked) => setAllPickerSelection(checked === true)}
                      disabled={selectablePickerIds.length === 0}
                    />
                  </th>
                  <th className="w-[120px] px-2 py-2 font-medium">Employee No</th>
                  <th className="px-2 py-2 font-medium">Employee Name</th>
                  <th className="w-[180px] whitespace-nowrap px-2 py-2 text-right font-medium">Basic Salary</th>
                </tr>
              </thead>
              <tbody>
                {filteredPickerProfiles.map((profile) => {
                  const profileId = profile.id || '';
                  return (
                    <tr key={profileId || profile.employeeNumber} className="border-b last:border-b-0 hover:bg-muted/40">
                      <td className="px-2 py-1.5 align-middle">
                        <Checkbox
                          checked={pickerSelectedSet.has(profileId)}
                          onCheckedChange={(checked) => togglePickerProfile(profileId, checked === true)}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle font-medium">{profile.employeeNumber}</td>
                      <td className="px-2 py-1.5 align-middle">{profile.employeeName}</td>
                      <td className="px-2 py-1.5 text-right align-middle">{amount(profile.salaryBasis?.monthlyBasicSalary ?? 0)}</td>
                    </tr>
                  );
                })}
                {filteredPickerProfiles.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-2 py-8 text-center text-sm text-muted-foreground">
                      No active payroll employees match this search.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <DialogFooter className="gap-2 sm:space-x-0">
            <Button type="button" variant="outline" onClick={() => setEmployeePickerOpen(false)}>
              Cancel
            </Button>
            <Button type="button" onClick={addPickerEmployees}>
              <UserPlus className="mr-2 h-4 w-4" />
              Apply Selection
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </main>
  );
}
