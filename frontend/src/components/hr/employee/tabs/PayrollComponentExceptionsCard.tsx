'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, Loader2, Pencil, PowerOff } from 'lucide-react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { employeeService } from '@/services/hr/employee.service';
import { payrollService } from '@/services/payrollService';
import type { EmployeeDetail, EmployeePayrollComponentRow } from '@/types/hr/employee';

const money = (v?: number | null, currency = 'GHS') =>
  v == null ? '—' : new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(v);

/**
 * Allowances and deductions for ONE employee (round 3, lane X; decision D-3).
 *
 * Payroll's own screen is component-first: pick an allowance, see every employee. HR needs the
 * other axis — this person, every component — and that is what this card is. The READ is HR's door
 * (`payroll-component-exceptions`, every active component with the person's exception beside it).
 * The WRITE is payroll's own `component-exceptions/bulk`, one row at a time: the lane's probe proved
 * that endpoint upserts only the lines it is sent and removes only a line marked deselected, so a
 * save from here can never disturb another employee's row.
 *
 * ⚠ Payroll owns the components and their defaults; nothing here edits those. What is edited is
 * the exception — this person's amount, rate, taxability, whether it applies, and the dates.
 */
export function PayrollComponentExceptionsCard({ employee, canWrite }: { employee: EmployeeDetail; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const queryKey = ['hr', 'employees', employee.id, 'payroll-component-exceptions'] as const;
  const { data, isLoading, isError, error } = useQuery({
    queryKey,
    queryFn: () => employeeService.getPayrollComponentExceptions(employee.id),
  });
  const [editing, setEditing] = useState<EmployeePayrollComponentRow | null>(null);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      queryClient.invalidateQueries({ queryKey: ['payroll', 'component-exceptions'] }),
    ]);

  // ⚠ "Remove" deliberately does NOT deselect the row. Payroll soft-deletes a deselected exception
  // and keeps a unique index on (tenant, profile, component) that is not filtered on IsDeleted, so
  // the person could never be given that component again without a 500 (cross-module defect #27).
  // Switching the row OFF (applicable: false) keeps the row and stops payroll paying or deducting
  // it, which is what the user meant.
  const switchOff = useMutation({
    mutationFn: (row: EmployeePayrollComponentRow) =>
      payrollService.saveEmployeeComponentExceptions({
        payrollComponentId: row.payrollComponentId,
        componentCode: row.code,
        componentType: row.componentType as any,
        entries: [
          {
            id: row.exceptionId ?? undefined,
            employeeProfileId: data?.payrollProfileId ?? undefined,
            employeeNumber: employee.employeeNumber,
            calculationTypeOverride: row.calculationType ?? row.defaultCalculationType,
            amountOverride: row.amount ?? row.defaultAmount,
            rateOverride: row.rate ?? row.defaultRate,
            taxableOverride: row.taxable ?? row.defaultTaxable,
            applicable: false,
            effectiveFrom: row.effectiveFrom ?? null,
            effectiveTo: row.effectiveTo ?? null,
            isSelected: true,
          },
        ],
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Switched off', description: 'The row stays on payroll but no longer applies to this employee.' });
    },
    onError: (e: unknown) =>
      toast({ variant: 'destructive', title: 'Could not switch it off', description: e instanceof Error ? e.message : 'Payroll refused the change.' }),
  });

  const rows = data?.rows ?? [];
  const withException = rows.filter((r) => r.hasException);
  const canEdit = canWrite && !!data?.hasPayrollProfile;

  return (
    <Card data-testid="payroll-component-exceptions-card">
      <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
        <div className="space-y-1.5">
          <CardTitle className="text-base">Allowances &amp; deductions</CardTitle>
          <CardDescription>
            Every payroll component, with this employee&rsquo;s own figure where one is set. Defaults are
            payroll&rsquo;s; a change here is saved to payroll for this person only.
          </CardDescription>
        </div>
        <Button variant="outline" size="sm" asChild>
          <Link href="/hr/payroll/allowances-deductions-exception">
            <ExternalLink className="mr-2 h-4 w-4" />
            Open in Payroll
          </Link>
        </Button>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : isError ? (
          <p className="text-sm text-destructive">{(error as Error)?.message || 'Payroll could not be read.'}</p>
        ) : (
          <>
            {data && !data.hasPayrollProfile && (
              <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900" data-testid="no-payroll-profile">
                Payroll has no profile for this employee yet, so nothing can be saved here. The defaults are shown
                for reference; create the profile on this tab first.
              </p>
            )}
            {rows.length === 0 ? (
              <p className="text-sm text-muted-foreground">Payroll has no active allowance or deduction components set up.</p>
            ) : (
              <div className="overflow-x-auto rounded-md border">
                <Table className="text-sm">
                  <TableHeader>
                    <TableRow>
                      <TableHead>Component</TableHead>
                      <TableHead className="w-[110px]">Type</TableHead>
                      <TableHead className="w-[150px]">Default</TableHead>
                      <TableHead className="w-[170px]">This employee</TableHead>
                      <TableHead className="w-[90px]">Applies</TableHead>
                      <TableHead className="w-[100px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((r) => (
                      <TableRow key={r.payrollComponentId} data-testid="payroll-component-row">
                        <TableCell>
                          <span className="font-medium">{r.code}</span>
                          <span className="text-muted-foreground"> — {r.name}</span>
                        </TableCell>
                        <TableCell>{r.componentTypeName ?? r.componentType}</TableCell>
                        <TableCell className="text-muted-foreground">
                          {r.defaultCalculationType === 'PercentageOfBasic' ? `${r.defaultRate}% of basic` : money(r.defaultAmount, r.currencyCode)}
                        </TableCell>
                        <TableCell>
                          {r.hasException ? (
                            <span className="font-medium">
                              {(r.calculationType ?? r.defaultCalculationType) === 'PercentageOfBasic'
                                ? `${r.rate ?? r.defaultRate}% of basic`
                                : money(r.amount ?? r.defaultAmount, r.currencyCode)}
                              {r.effectiveFrom && (
                                <span className="block text-xs font-normal text-muted-foreground">
                                  from {r.effectiveFrom.slice(0, 10)}{r.effectiveTo ? ` to ${r.effectiveTo.slice(0, 10)}` : ''}
                                </span>
                              )}
                            </span>
                          ) : (
                            <span className="text-muted-foreground">{r.appliesByDefault ? 'Default' : 'Not applied'}</span>
                          )}
                        </TableCell>
                        <TableCell>
                          {r.hasException ? (
                            <Badge variant={r.applicable ? 'default' : 'secondary'}>{r.applicable ? 'Yes' : 'No'}</Badge>
                          ) : (
                            <span className="text-muted-foreground">{r.appliesByDefault ? 'Yes' : 'No'}</span>
                          )}
                        </TableCell>
                        <TableCell>
                          {canEdit && (
                            <div className="flex justify-end gap-1">
                              <Button variant="ghost" size="sm" onClick={() => setEditing(r)} aria-label={`Edit ${r.code}`}>
                                <Pencil className="h-4 w-4" />
                              </Button>
                              {r.hasException && r.applicable !== false && (
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  className="text-destructive hover:text-destructive"
                                  onClick={() => switchOff.mutate(r)}
                                  disabled={switchOff.isPending}
                                  aria-label={`Switch ${r.code} off for this employee`}
                                  title="Switch off for this employee"
                                >
                                  <PowerOff className="h-4 w-4" />
                                </Button>
                              )}
                            </div>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
            {withException.length > 0 && (
              <p className="text-xs text-muted-foreground">
                {withException.length} of {rows.length} component{rows.length === 1 ? '' : 's'} carry a figure set for this employee.
              </p>
            )}
          </>
        )}
      </CardContent>

      {editing && data && (
        <ExceptionDialog
          row={editing}
          employeeNumber={employee.employeeNumber}
          payrollProfileId={data.payrollProfileId ?? null}
          open={!!editing}
          onOpenChange={(o) => !o && setEditing(null)}
          onSaved={refresh}
        />
      )}
    </Card>
  );
}

function ExceptionDialog({
  row,
  employeeNumber,
  payrollProfileId,
  open,
  onOpenChange,
  onSaved,
}: {
  row: EmployeePayrollComponentRow;
  employeeNumber: string;
  payrollProfileId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<unknown> | void;
}) {
  const { toast } = useToast();
  const [calc, setCalc] = useState<'FixedAmount' | 'PercentageOfBasic'>(row.calculationType ?? row.defaultCalculationType);
  const [amount, setAmount] = useState(String(row.amount ?? row.defaultAmount));
  const [rate, setRate] = useState(String(row.rate ?? row.defaultRate));
  const [taxable, setTaxable] = useState(row.taxable ?? row.defaultTaxable);
  const [applicable, setApplicable] = useState(row.applicable ?? true);
  const [from, setFrom] = useState(row.effectiveFrom?.slice(0, 10) ?? '');
  const [to, setTo] = useState(row.effectiveTo?.slice(0, 10) ?? '');
  useEffect(() => {
    setCalc(row.calculationType ?? row.defaultCalculationType);
    setAmount(String(row.amount ?? row.defaultAmount));
    setRate(String(row.rate ?? row.defaultRate));
    setTaxable(row.taxable ?? row.defaultTaxable);
    setApplicable(row.applicable ?? true);
    setFrom(row.effectiveFrom?.slice(0, 10) ?? '');
    setTo(row.effectiveTo?.slice(0, 10) ?? '');
  }, [row]);

  const datesInverted = Boolean(from && to && to < from);

  const save = useMutation({
    mutationFn: () =>
      payrollService.saveEmployeeComponentExceptions({
        payrollComponentId: row.payrollComponentId,
        componentCode: row.code,
        componentType: row.componentType as any,
        entries: [
          {
            id: row.exceptionId ?? undefined,
            employeeProfileId: payrollProfileId ?? undefined,
            employeeNumber,
            calculationTypeOverride: calc,
            amountOverride: calc === 'FixedAmount' ? Number(amount) || 0 : null,
            rateOverride: calc === 'PercentageOfBasic' ? Number(rate) || 0 : null,
            taxableOverride: taxable,
            applicable,
            effectiveFrom: from ? `${from}T00:00:00` : null,
            effectiveTo: to ? `${to}T00:00:00` : null,
            isSelected: true,
          },
        ],
      }),
    onSuccess: async () => {
      await onSaved();
      toast({ title: 'Saved to payroll', description: `${row.code} now carries this employee's own figure.` });
      onOpenChange(false);
    },
    onError: (e: unknown) =>
      toast({ variant: 'destructive', title: 'Payroll refused the change', description: e instanceof Error ? e.message : 'Please try again.' }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>
            {row.code} — {row.name}
          </DialogTitle>
          <DialogDescription>
            This employee&rsquo;s own figure for the component. Payroll&rsquo;s default is{' '}
            {row.defaultCalculationType === 'PercentageOfBasic' ? `${row.defaultRate}% of basic` : money(row.defaultAmount, row.currencyCode)}.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="exc-calc">Worked out as</Label>
            <Select value={calc} onValueChange={(v) => setCalc(v as 'FixedAmount' | 'PercentageOfBasic')}>
              <SelectTrigger id="exc-calc">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="FixedAmount">A fixed amount</SelectItem>
                <SelectItem value="PercentageOfBasic">A percentage of basic</SelectItem>
              </SelectContent>
            </Select>
          </div>
          {calc === 'FixedAmount' ? (
            <div className="space-y-2">
              <Label htmlFor="exc-amount">Amount ({row.currencyCode})</Label>
              <Input id="exc-amount" type="number" min={0} step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
            </div>
          ) : (
            <div className="space-y-2">
              <Label htmlFor="exc-rate">Rate (% of basic)</Label>
              <Input id="exc-rate" type="number" min={0} step="0.01" value={rate} onChange={(e) => setRate(e.target.value)} />
            </div>
          )}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="exc-from">From</Label>
              <Input id="exc-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="exc-to">To</Label>
              <Input id="exc-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              <p className="text-xs text-muted-foreground">Blank = open-ended.</p>
            </div>
          </div>
          {datesInverted && <p className="text-sm text-destructive">The end date cannot be before the start date.</p>}
          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="exc-taxable">Taxable</Label>
              <p className="text-xs text-muted-foreground">Overrides the component&rsquo;s default for this person.</p>
            </div>
            <Switch id="exc-taxable" checked={taxable} onCheckedChange={setTaxable} />
          </div>
          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="exc-applicable">Applies to this employee</Label>
              <p className="text-xs text-muted-foreground">Off keeps the row but stops payroll paying or deducting it.</p>
            </div>
            <Switch id="exc-applicable" checked={applicable} onCheckedChange={setApplicable} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={save.isPending}>
            Cancel
          </Button>
          <Button onClick={() => save.mutate()} disabled={save.isPending || datesInverted}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save to payroll
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
