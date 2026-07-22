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
  PayrollComponent,
  PayrollComponentType,
  PayrollEmployeeComponentException,
  PayrollEmployeeProfile,
  PayrollParameterSet,
  payrollService,
} from '@/services/payrollService';

type ComponentGroup = 'Allowance' | 'Contribution' | 'Benefit' | 'Deduction';

type ExceptionRow = {
  id?: string;
  employeeProfileId: string;
  employeeNumber: string;
  employeeName: string;
  monthlyBasicSalary: number;
  calculationType: PayrollCalculationType;
  amount: number;
  rate: number;
  taxable: boolean;
  taxFreeCeiling: number;
  employerAmount: number;
  employerTaxable: boolean;
  grossUp: boolean;
  currencyCode: string;
  applicable: boolean;
  selected: boolean;
};

const componentGroups: Array<{ value: ComponentGroup; label: string; types: PayrollComponentType[] }> = [
  { value: 'Allowance', label: 'Allowances', types: ['Allowance'] },
  { value: 'Contribution', label: 'Contributions', types: ['EmployeeContribution', 'EmployerContribution'] },
  { value: 'Benefit', label: 'Benefits', types: ['Benefit'] },
  { value: 'Deduction', label: 'Deductions', types: ['Deduction'] },
];

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

function componentMatchesGroup(component: PayrollComponent, group: ComponentGroup) {
  return componentGroups.find((item) => item.value === group)?.types.includes(component.componentType) ?? false;
}

function sortProfiles(first: PayrollEmployeeProfile, second: PayrollEmployeeProfile) {
  return first.employeeNumber.localeCompare(second.employeeNumber);
}

function rowAmount(row: Pick<ExceptionRow, 'calculationType' | 'amount' | 'rate' | 'monthlyBasicSalary'>) {
  return row.calculationType === 'PercentageOfBasic'
    ? Math.round(row.monthlyBasicSalary * row.rate) / 100
    : row.amount;
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

function BooleanCell({
  checked,
  onChange,
  label,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
}) {
  return (
    <label className="flex items-center justify-center" aria-label={label}>
      <Checkbox checked={checked} onCheckedChange={(value) => onChange(value === true)} />
    </label>
  );
}

export default function AllowancesDeductionsExceptionPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] = useState<PayrollParameterSet | null>(null);
  const [components, setComponents] = useState<PayrollComponent[]>([]);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [componentGroup, setComponentGroup] = useState<ComponentGroup>('Allowance');
  const [componentId, setComponentId] = useState('');
  const [rows, setRows] = useState<ExceptionRow[]>([]);
  const [removedRows, setRemovedRows] = useState<ExceptionRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeePickerOpen, setEmployeePickerOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ExceptionRow | null>(null);
  const [gridDirty, setGridDirty] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const selectedComponent = useMemo(
    () => components.find((component) => component.id === componentId) ?? null,
    [componentId, components],
  );

  const availableComponents = useMemo(
    () => components
      .filter((component) => component.isActive && componentMatchesGroup(component, componentGroup))
      .sort((first, second) => first.name.localeCompare(second.name)),
    [componentGroup, components],
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

  const selectedTotal = rows.reduce((total, row) => total + (row.applicable ? rowAmount(row) : 0), 0);

  const buildRows = useCallback((assignments: PayrollEmployeeComponentException[], component = selectedComponent) => {
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
            monthlyBasicSalary: existing.monthlyBasicSalary || profile?.salaryBasis?.monthlyBasicSalary || 0,
            calculationType: existing.calculationType || component?.calculationType || 'FixedAmount',
            amount: existing.amount ?? component?.amount ?? 0,
            rate: existing.rate ?? component?.rate ?? 0,
            taxable: existing.taxable ?? component?.taxable ?? false,
            taxFreeCeiling: existing.taxFreeCeiling ?? component?.taxFreeCeiling ?? 0,
            employerAmount: existing.employerAmount ?? component?.employerAmount ?? 0,
            employerTaxable: existing.employerTaxable ?? component?.employerTaxable ?? false,
            grossUp: existing.grossUp ?? component?.grossUp ?? false,
            currencyCode: existing.currencyCode || component?.currencyCode || activeParameters?.baseCurrency || 'GHS',
            applicable: existing.applicable ?? true,
            selected: true,
          };
        }),
    );
    setRemovedRows([]);
    setGridDirty(false);
  }, [activeParameters?.baseCurrency, profiles, selectedComponent]);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, profileList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getEmployeeProfiles(),
      ]);

      setActiveParameters(setup.activeParameters ?? null);
      setComponents(setup.components ?? []);
      setProfiles(profileList);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load allowances and deductions.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const selectGroup = (value: ComponentGroup) => {
    setComponentGroup(value);
    setComponentId('');
    setRows([]);
    setRemovedRows([]);
    setGridDirty(false);
    setEmployeeSearch('');
    setPickerSearch('');
    setPickerSelectedIds([]);
  };

  const selectComponent = async (nextComponentId: string) => {
    const component = components.find((item) => item.id === nextComponentId) ?? null;
    setComponentId(nextComponentId);
    setRows([]);
    setRemovedRows([]);
    setGridDirty(false);
    setEmployeeSearch('');
    setPickerSearch('');
    setPickerSelectedIds([]);

    if (!component?.id) {
      return;
    }

    setBusy('search');
    setError(null);
    try {
      const assignments = await payrollService.getEmployeeComponentExceptions({ payrollComponentId: component.id });
      buildRows(assignments, component);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load saved component exceptions.');
    } finally {
      setBusy(null);
    }
  };

  const updateRow = (employeeProfileId: string, patch: Partial<ExceptionRow>) => {
    setGridDirty(true);
    setRows((current) =>
      current.map((row) =>
        row.employeeProfileId === employeeProfileId
          ? { ...row, ...patch }
          : row,
      ),
    );
  };

  const createRowFromProfile = useCallback((profile: PayrollEmployeeProfile): ExceptionRow => ({
    employeeProfileId: profile.id || '',
    employeeNumber: profile.employeeNumber,
    employeeName: profile.employeeName,
    monthlyBasicSalary: profile.salaryBasis?.monthlyBasicSalary ?? 0,
    calculationType: selectedComponent?.calculationType ?? 'FixedAmount',
    amount: selectedComponent?.amount ?? 0,
    rate: selectedComponent?.rate ?? 0,
    taxable: selectedComponent?.taxable ?? false,
    taxFreeCeiling: selectedComponent?.taxFreeCeiling ?? 0,
    employerAmount: selectedComponent?.employerAmount ?? 0,
    employerTaxable: selectedComponent?.employerTaxable ?? false,
    grossUp: selectedComponent?.grossUp ?? false,
    currencyCode: selectedComponent?.currencyCode || activeParameters?.baseCurrency || 'GHS',
    applicable: true,
    selected: true,
  }), [activeParameters?.baseCurrency, selectedComponent]);

  const openEmployeePicker = () => {
    if (!selectedComponent?.id) {
      setError('Select an allowance or deduction before adding employees.');
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

  const removeRow = (row: ExceptionRow) => {
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
    if (!selectedComponent?.id) {
      setError('Select an allowance or deduction before saving.');
      return;
    }

    setBusy('save');
    setError(null);
    try {
      const saved = await payrollService.saveEmployeeComponentExceptions({
        payrollComponentId: selectedComponent.id,
        componentCode: selectedComponent.code,
        componentType: selectedComponent.componentType,
        entries: [
          ...rows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            calculationTypeOverride: row.calculationType,
            amountOverride: row.amount,
            rateOverride: row.rate,
            taxableOverride: row.taxable,
            taxFreeCeilingOverride: row.taxFreeCeiling,
            employerAmountOverride: row.employerAmount,
            employerTaxableOverride: row.employerTaxable,
            grossUpOverride: row.grossUp,
            currencyCodeOverride: row.currencyCode,
            applicable: row.applicable,
            isSelected: true,
          })),
          ...removedRows.map((row) => ({
            id: row.id,
            employeeProfileId: row.employeeProfileId,
            employeeNumber: row.employeeNumber,
            calculationTypeOverride: row.calculationType,
            amountOverride: row.amount,
            rateOverride: row.rate,
            taxableOverride: row.taxable,
            taxFreeCeilingOverride: row.taxFreeCeiling,
            employerAmountOverride: row.employerAmount,
            employerTaxableOverride: row.employerTaxable,
            grossUpOverride: row.grossUp,
            currencyCodeOverride: row.currencyCode,
            applicable: row.applicable,
            isSelected: false,
          })),
        ],
      });

      buildRows(saved, selectedComponent);
      toast({
        title: 'Exceptions saved',
        description: `${saved.length} employee record${saved.length === 1 ? '' : 's'} active for ${selectedComponent.name}.`,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to save component exceptions.';
      setError(message);
      toast({ title: 'Exception error', description: message, variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading allowances and deductions
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
            <h1 className="text-2xl font-semibold tracking-normal">Allowances & Ded Exception</h1>
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
              <div className="grid flex-1 gap-3 md:grid-cols-[180px_minmax(280px,1fr)_130px_120px]">
                <Field label="Type">
                  <select
                    className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                    value={componentGroup}
                    onChange={(event) => selectGroup(event.target.value as ComponentGroup)}
                  >
                    {componentGroups.map((item) => (
                      <option key={item.value} value={item.value}>
                        {item.label}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Allowance / Deduction">
                  <select
                    className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                    value={componentId}
                    onChange={(event) => void selectComponent(event.target.value)}
                  >
                    <option value="">Select option</option>
                    {availableComponents.map((component) => (
                      <option key={component.id || component.code} value={component.id}>
                        {component.name || component.code}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Default Value">
                  <div className="flex h-9 items-center justify-end rounded-md border bg-muted/30 px-3 text-sm font-medium">
                    {amount(selectedComponent?.calculationType === 'PercentageOfBasic' ? selectedComponent?.rate : selectedComponent?.amount)}
                  </div>
                </Field>
                <Field label="Percentage?">
                  <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm">
                    {selectedComponent?.calculationType === 'PercentageOfBasic' ? 'Yes' : 'No'}
                  </div>
                </Field>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => void loadWorkspace()} disabled={Boolean(busy)}>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Refresh
                </Button>
                <Button type="button" size="sm" variant="outline" onClick={openEmployeePicker} disabled={!selectedComponent}>
                  <UserPlus className="mr-2 h-4 w-4" />
                  Add Employees
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  variant={gridDirty ? 'warning' : 'default'}
                  disabled={busy === 'save' || !selectedComponent || (rows.length === 0 && removedRows.length === 0)}
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
                  Employees: <span className="font-medium text-foreground">{rows.length}</span>
                  <span className="mx-2">|</span>
                  Total: <span className="font-medium text-foreground">{amount(selectedTotal)}</span>
                </div>
                <PayrollGridExportButton
                  rows={filteredRows}
                  fileName={`allowance-deduction-exception-${selectedComponent?.code || 'employees'}`}
                  columns={[
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Percentage', value: (row) => row.calculationType === 'PercentageOfBasic' ? 'Yes' : 'No' },
                    { header: 'Amount', value: (row) => row.calculationType === 'PercentageOfBasic' ? row.rate : row.amount },
                    { header: 'Taxable', value: (row) => row.taxable ? 'Yes' : 'No' },
                    { header: 'Tax Free Ceiling', value: (row) => row.taxFreeCeiling },
                    { header: 'Employer Amount', value: (row) => row.employerAmount },
                    { header: 'Employer Taxable', value: (row) => row.employerTaxable ? 'Yes' : 'No' },
                    { header: 'Applicable', value: (row) => row.applicable ? 'Yes' : 'No' },
                    { header: 'Total', value: (row) => row.applicable ? rowAmount(row) : 0 },
                  ]}
                  disabled={!selectedComponent}
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
              <table className="w-full min-w-[1180px] text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-left text-xs text-muted-foreground">
                    <th className="w-[140px] px-2 py-2 font-medium">Employee No</th>
                    <th className="min-w-[210px] px-2 py-2 font-medium">Employee Name</th>
                    <th className="w-[64px] px-2 py-2 text-center font-medium">%</th>
                    <th className="w-[130px] px-2 py-2 text-right font-medium">Amount</th>
                    <th className="w-[86px] px-2 py-2 text-center font-medium">Taxable</th>
                    <th className="w-[150px] px-2 py-2 text-right font-medium">Tax Free Ceiling</th>
                    <th className="w-[150px] px-2 py-2 text-right font-medium">Employer Amount</th>
                    <th className="w-[92px] px-2 py-2 text-center font-medium">Employer Tax</th>
                    <th className="w-[92px] px-2 py-2 text-center font-medium">Applicable</th>
                    <th className="w-12 px-2 py-2 text-right font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.map((row) => (
                    <tr key={row.employeeProfileId} className="border-b last:border-b-0">
                      <td className="px-2 py-1.5 align-middle font-medium">{row.employeeNumber}</td>
                      <td className="px-2 py-1.5 align-middle">{row.employeeName}</td>
                      <td className="px-2 py-1.5 align-middle">
                        <BooleanCell
                          label={`Toggle percentage for ${row.employeeName}`}
                          checked={row.calculationType === 'PercentageOfBasic'}
                          onChange={(checked) => updateRow(row.employeeProfileId, { calculationType: checked ? 'PercentageOfBasic' : 'FixedAmount' })}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <Input
                          className="h-8 text-right"
                          type="number"
                          step="0.01"
                          value={row.calculationType === 'PercentageOfBasic' ? row.rate : row.amount}
                          onChange={(event) => {
                            const value = numberValue(event.target.value);
                            updateRow(row.employeeProfileId, row.calculationType === 'PercentageOfBasic' ? { rate: value } : { amount: value });
                          }}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <BooleanCell
                          label={`Toggle taxable for ${row.employeeName}`}
                          checked={row.taxable}
                          onChange={(checked) => updateRow(row.employeeProfileId, { taxable: checked })}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <Input
                          className="h-8 text-right"
                          type="number"
                          step="0.01"
                          value={row.taxFreeCeiling}
                          onChange={(event) => updateRow(row.employeeProfileId, { taxFreeCeiling: numberValue(event.target.value) })}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <Input
                          className="h-8 text-right"
                          type="number"
                          step="0.01"
                          value={row.employerAmount}
                          onChange={(event) => updateRow(row.employeeProfileId, { employerAmount: numberValue(event.target.value) })}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <BooleanCell
                          label={`Toggle employer taxable for ${row.employeeName}`}
                          checked={row.employerTaxable}
                          onChange={(checked) => updateRow(row.employeeProfileId, { employerTaxable: checked })}
                        />
                      </td>
                      <td className="px-2 py-1.5 align-middle">
                        <BooleanCell
                          label={`Toggle applicable for ${row.employeeName}`}
                          checked={row.applicable}
                          onChange={(checked) => updateRow(row.employeeProfileId, { applicable: checked })}
                        />
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
                      <td colSpan={10} className="px-2 py-8 text-center text-sm text-muted-foreground">
                        {selectedComponent ? 'No employee exceptions found for this option.' : 'Select an option to begin.'}
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
            ? `Remove ${deleteTarget.employeeNumber} - ${deleteTarget.employeeName} from this exception grid? Save the page to persist the removal.`
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
              Active payroll employees for {selectedComponent?.name || selectedComponent?.code}.
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
