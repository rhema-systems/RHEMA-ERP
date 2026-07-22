'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  Award,
  Loader2,
  RefreshCw,
  Save,
  Search,
  Trash2,
  UserPlus,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  PayrollBonusException,
  PayrollBonusPolicy,
  PayrollEmployeeProfile,
  PayrollSetupSummary,
  payrollService,
} from '@/services/payrollService';

type BonusExceptionRow = PayrollBonusException & { isSelected?: boolean };

function normalizeCode(value?: string | null) {
  return (value || '').trim().toUpperCase();
}

function formatAmount(value?: number | null) {
  return new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value ?? 0);
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

function bonusLabel(policy: PayrollBonusPolicy) {
  return policy.name ? `${policy.code} - ${policy.name}` : policy.code;
}

function calculationLabel(value?: PayrollBonusPolicy['calculationType']) {
  return value === 'PercentageOfBasic' ? 'Percentage' : 'Amount';
}

export default function BonusExceptionsPage() {
  const [setup, setSetup] = useState<PayrollSetupSummary | null>(null);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [selectedBonusCode, setSelectedBonusCode] = useState('');
  const [rows, setRows] = useState<BonusExceptionRow[]>([]);
  const [removedRows, setRemovedRows] = useState<BonusExceptionRow[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pickerSearch, setPickerSearch] = useState('');
  const [pickerSelectedIds, setPickerSelectedIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const bonusPolicies = setup?.bonusPolicies ?? [];
  const companyCode = setup?.activeParameters?.legacyCompanyCode || '001';
  const selectedBonus = useMemo(
    () => bonusPolicies.find((policy) => normalizeCode(policy.code) === normalizeCode(selectedBonusCode)) ?? null,
    [bonusPolicies, selectedBonusCode],
  );

  const activeProfiles = useMemo(
    () => profiles
      .filter((profile) => profile.payrollActive)
      .sort((left, right) => (left.employeeNumber || '').localeCompare(right.employeeNumber || '')),
    [profiles],
  );

  const selectedProfileIds = useMemo(
    () => new Set(rows.map((row) => row.employeeProfileId).filter(Boolean) as string[]),
    [rows],
  );

  const selectedEmployeeNumbers = useMemo(
    () => new Set(rows.map((row) => normalizeCode(row.employeeNumber)).filter(Boolean)),
    [rows],
  );

  const filteredRows = useMemo(() => {
    const term = employeeSearch.trim().toLowerCase();
    if (!term) {
      return rows;
    }

    return rows.filter((row) =>
      (row.employeeNumber || '').toLowerCase().includes(term) ||
      (row.employeeName || '').toLowerCase().includes(term),
    );
  }, [employeeSearch, rows]);

  const filteredPickerEmployees = useMemo(() => {
    const term = pickerSearch.trim().toLowerCase();
    const candidates = activeProfiles.filter((profile) =>
      !selectedProfileIds.has(profile.id || '') &&
      !selectedEmployeeNumbers.has(normalizeCode(profile.employeeNumber)),
    );

    if (!term) {
      return candidates;
    }

    return candidates.filter((profile) =>
      (profile.employeeNumber || '').toLowerCase().includes(term) ||
      (profile.employeeName || '').toLowerCase().includes(term),
    );
  }, [activeProfiles, pickerSearch, selectedEmployeeNumbers, selectedProfileIds]);

  const selectablePickerIds = useMemo(
    () => filteredPickerEmployees.map((profile) => profile.id || '').filter(Boolean),
    [filteredPickerEmployees],
  );
  const pickerSelectedSet = useMemo(() => new Set(pickerSelectedIds), [pickerSelectedIds]);
  const allVisibleSelected = selectablePickerIds.length > 0 && selectablePickerIds.every((id) => pickerSelectedSet.has(id));
  const selectAllState = allVisibleSelected ? true : pickerSelectedIds.length > 0 ? 'indeterminate' : false;

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [summary, employees] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getEmployeeProfiles(),
      ]);

      setSetup(summary);
      setProfiles(employees);
      setSelectedBonusCode((current) => {
        if (current && summary.bonusPolicies.some((policy) => normalizeCode(policy.code) === normalizeCode(current))) {
          return current;
        }

        return summary.bonusPolicies[0]?.code ?? '';
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load bonus exceptions.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  useEffect(() => {
    if (!selectedBonusCode) {
      setRows([]);
      setRemovedRows([]);
      return;
    }

    const selectedCode = normalizeCode(selectedBonusCode);
    setRows((setup?.bonusExceptions ?? [])
      .filter((row) => normalizeCode(row.bonusCode) === selectedCode)
      .map((row) => ({ ...row, isSelected: true })));
    setRemovedRows([]);
    setEmployeeSearch('');
  }, [selectedBonusCode, setup?.bonusExceptions]);

  const updateRow = (employeeNumber: string, patch: Partial<PayrollBonusException>) => {
    setRows((current) => current.map((row) => (
      row.employeeNumber === employeeNumber ? { ...row, ...patch } : row
    )));
  };

  const removeRow = (row: BonusExceptionRow) => {
    setRows((current) => current.filter((item) => item.employeeNumber !== row.employeeNumber));
    if (row.id) {
      setRemovedRows((current) =>
        current.some((item) => item.id === row.id)
          ? current
          : [...current, { ...row, isSelected: false }],
      );
    }
  };

  const openPicker = () => {
    setPickerSearch('');
    setPickerSelectedIds([]);
    setPickerOpen(true);
  };

  const setVisibleSelection = (selected: boolean) => {
    setPickerSelectedIds((current) => {
      if (selected) {
        return Array.from(new Set([...current, ...selectablePickerIds]));
      }

      const visible = new Set(selectablePickerIds);
      return current.filter((id) => !visible.has(id));
    });
  };

  const togglePickerEmployee = (employeeProfileId: string, selected: boolean) => {
    setPickerSelectedIds((current) => {
      if (selected) {
        return current.includes(employeeProfileId) ? current : [...current, employeeProfileId];
      }

      return current.filter((id) => id !== employeeProfileId);
    });
  };

  const addPickerEmployees = () => {
    if (!selectedBonus) {
      return;
    }

    const selectedIds = new Set(pickerSelectedIds);
    const nextRows = activeProfiles
      .filter((profile) => selectedIds.has(profile.id || ''))
      .filter((profile) => !rows.some((row) => row.employeeProfileId === profile.id || normalizeCode(row.employeeNumber) === normalizeCode(profile.employeeNumber)))
      .map((profile) => ({
        bonusCode: selectedBonus.code,
        employeeProfileId: profile.id,
        employeeNumber: profile.employeeNumber,
        employeeName: profile.employeeName,
        calculationType: selectedBonus.calculationType,
        amount: selectedBonus.amount,
        taxable: selectedBonus.taxable,
        applicable: true,
        currencyCode: profile.currencyCode || 'GHS',
        legacyCompanyCode: companyCode,
        isSelected: true,
      }));

    setRows((current) => [...current, ...nextRows]);
    setRemovedRows((current) => current.filter((row) => !selectedIds.has(row.employeeProfileId || '')));
    setPickerOpen(false);
  };

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedBonus) {
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const savedRows = await payrollService.saveBonusExceptions({
        bonusCode: selectedBonus.code,
        legacyCompanyCode: companyCode,
        entries: [
          ...rows.map((row) => ({ ...row, isSelected: true })),
          ...removedRows.map((row) => ({ ...row, isSelected: false })),
        ].map((row) => ({
          id: row.id,
          employeeProfileId: row.employeeProfileId,
          employeeNumber: row.employeeNumber,
          calculationType: row.calculationType,
          amount: row.amount,
          taxable: row.taxable,
          applicable: row.applicable,
          currencyCode: row.currencyCode,
          legacyCompanyCode: row.legacyCompanyCode || companyCode,
          isSelected: row.isSelected !== false,
        })),
      });

      setRows(savedRows.map((row) => ({ ...row, isSelected: true })));
      setRemovedRows([]);
      setSetup((current) => current
        ? {
            ...current,
            bonusExceptions: [
              ...current.bonusExceptions.filter((row) => normalizeCode(row.bonusCode) !== normalizeCode(selectedBonus.code)),
              ...savedRows,
            ],
          }
        : current);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save bonus exceptions.');
    } finally {
      setBusy(false);
    }
  };

  const inputClassName = 'h-8 border-0 bg-transparent px-1 text-right text-sm shadow-none focus-visible:ring-1';

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Button asChild variant="ghost" size="icon" className="h-8 w-8">
              <Link href="/hr/payroll" aria-label="Back to payroll">
                <ArrowLeft className="h-4 w-4" />
              </Link>
            </Button>
            <Badge variant="outline">Human Resources</Badge>
          </div>
          <h1 className="flex items-center gap-2 text-xl font-semibold">
            <Award className="h-5 w-5" />
            Bonus Exceptions
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" onClick={() => void loadWorkspace()} disabled={loading || busy}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      {error ? (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      ) : null}

      <form className="space-y-4" onSubmit={save}>
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Bonus</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 md:grid-cols-[minmax(260px,1fr)_150px_150px_150px] md:items-end">
              <Field label="Bonus">
                <select
                  className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                  value={selectedBonusCode}
                  onChange={(event) => setSelectedBonusCode(event.target.value)}
                  disabled={loading || busy || bonusPolicies.length === 0}
                >
                  {bonusPolicies.length === 0 ? <option value="">No bonuses</option> : null}
                  {bonusPolicies.map((policy) => (
                    <option key={policy.id || policy.code} value={policy.code}>
                      {bonusLabel(policy)}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Calculation">
                <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm">
                  {calculationLabel(selectedBonus?.calculationType)}
                </div>
              </Field>
              <Field label="Policy Value">
                <div className="flex h-9 items-center justify-end rounded-md border bg-muted/30 px-3 text-sm tabular-nums">
                  {formatAmount(selectedBonus?.amount)}
                </div>
              </Field>
              <Field label="Selected">
                <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm font-medium">
                  {rows.length}
                </div>
              </Field>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
              <div className="grid flex-1 gap-3 sm:grid-cols-[minmax(240px,1fr)_120px]">
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
                <Field label="Rows">
                  <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm font-medium">
                    {filteredRows.length}
                  </div>
                </Field>
              </div>
              <div className="flex flex-wrap gap-2">
                <PayrollGridExportButton
                  rows={filteredRows}
                  fileName={`bonus-exceptions-${selectedBonusCode || 'bonus'}`}
                  columns={[
                    { header: 'Bonus Code', value: (row) => row.bonusCode },
                    { header: 'Employee No', value: (row) => row.employeeNumber },
                    { header: 'Employee Name', value: (row) => row.employeeName },
                    { header: 'Amount', value: (row) => row.amount },
                    { header: 'Calculation', value: (row) => calculationLabel(row.calculationType) },
                    { header: 'Taxable', value: (row) => row.taxable ? 'Yes' : 'No' },
                    { header: 'Applicable', value: (row) => row.applicable ? 'Yes' : 'No' },
                  ]}
                />
                <Button type="button" size="sm" variant="outline" onClick={openPicker} disabled={!selectedBonus || busy}>
                  <UserPlus className="mr-2 h-4 w-4" />
                  Add Employee
                </Button>
                <Button type="submit" size="sm" disabled={!selectedBonus || busy}>
                  {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[760px] text-sm">
                <thead className="bg-muted/60 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="w-[120px] px-3 py-2 text-left">Employee No</th>
                    <th className="min-w-64 px-3 py-2 text-left">Employee Name</th>
                    <th className="w-36 px-3 py-2 text-right">Amount</th>
                    <th className="w-28 px-3 py-2">Percentage?</th>
                    <th className="w-24 px-3 py-2">Taxable?</th>
                    <th className="w-28 px-3 py-2">Applicable?</th>
                    <th className="w-12 px-3 py-2" aria-label="Remove" />
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No employees selected.
                      </td>
                    </tr>
                  ) : filteredRows.map((row) => (
                    <tr key={row.id || row.employeeProfileId || row.employeeNumber} className="border-t">
                      <td className="px-3 py-2 font-medium">{row.employeeNumber}</td>
                      <td className="px-3 py-2">{row.employeeName}</td>
                      <td className="px-3 py-2">
                        <Input className={inputClassName} type="number" step="0.01" value={row.amount ?? ''} onChange={(event) => updateRow(row.employeeNumber, { amount: Number(event.target.value) || 0 })} />
                      </td>
                      <td className="px-3 py-2 text-center">
                        <Checkbox checked={row.calculationType === 'PercentageOfBasic'} onCheckedChange={(checked) => updateRow(row.employeeNumber, { calculationType: checked === true ? 'PercentageOfBasic' : 'FixedAmount' })} />
                      </td>
                      <td className="px-3 py-2 text-center">
                        <Checkbox checked={row.taxable} onCheckedChange={(checked) => updateRow(row.employeeNumber, { taxable: checked === true })} />
                      </td>
                      <td className="px-3 py-2 text-center">
                        <Checkbox checked={row.applicable} onCheckedChange={(checked) => updateRow(row.employeeNumber, { applicable: checked === true })} />
                      </td>
                      <td className="px-3 py-2 text-center">
                        <Button type="button" variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" onClick={() => removeRow(row)} aria-label={`Remove ${row.employeeName}`}>
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      </form>

      <Dialog open={pickerOpen} onOpenChange={setPickerOpen}>
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
                <span className="text-sm text-muted-foreground">{pickerSelectedIds.length} selected</span>
                <Button type="button" variant="outline" size="sm" onClick={() => setVisibleSelection(true)} disabled={selectablePickerIds.length === 0 || allVisibleSelected}>
                  Select Visible
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setVisibleSelection(false)} disabled={pickerSelectedIds.length === 0}>
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
                        aria-label="Select visible employees"
                        checked={selectAllState}
                        onCheckedChange={(checked) => setVisibleSelection(checked === true)}
                        disabled={selectablePickerIds.length === 0}
                      />
                    </th>
                    <th className="w-[120px] px-3 py-2 text-left">Employee No</th>
                    <th className="px-3 py-2 text-left">Employee Name</th>
                    <th className="w-36 whitespace-nowrap px-3 py-2 text-right">Basic Salary</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredPickerEmployees.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-3 py-8 text-center text-sm text-muted-foreground">
                        No employees found.
                      </td>
                    </tr>
                  ) : filteredPickerEmployees.map((profile) => {
                    const employeeProfileId = profile.id || '';
                    return (
                      <tr key={employeeProfileId || profile.employeeNumber} className="border-t">
                        <td className="px-3 py-2">
                          <Checkbox
                            checked={pickerSelectedSet.has(employeeProfileId)}
                            disabled={!employeeProfileId}
                            onCheckedChange={(checked) => togglePickerEmployee(employeeProfileId, checked === true)}
                          />
                        </td>
                        <td className="px-3 py-2 font-medium">{profile.employeeNumber}</td>
                        <td className="px-3 py-2">{profile.employeeName}</td>
                        <td className="px-3 py-2 text-right tabular-nums">{formatAmount(profile.salaryBasis?.monthlyBasicSalary)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setPickerOpen(false)}>
              Cancel
            </Button>
            <Button type="button" onClick={addPickerEmployees} disabled={pickerSelectedIds.length === 0}>
              <UserPlus className="mr-2 h-4 w-4" />
              Add Selected
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
