'use client';

/**
 * The employee's Salary tab — round-2 lane E1 (docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 6.5).
 *
 * Four sections, in the order § 6.5.3 gives them:
 *   1. Pay basis — scale or negotiated, the note, and the figure HR would quote with its source.
 *   2. Grade placement — the existing salary-assignment history; hidden while negotiated, because
 *      a placement contradicts a negotiated amount and the server refuses one.
 *   3. Payroll profile — payroll's own window (basic salary, currency, switches, payment split),
 *      hosted here as the SAME component payroll's page uses. Read through HR's door; saved
 *      through payroll's upsert. Read-only for somebody not on payroll.
 *   4. Payroll items — loans, advances, tax reliefs and component exceptions for this staff number,
 *      read-only from payroll's own lists. Becomes the components editor when payroll builds one
 *      (plan § 7.1, item 2).
 *
 * ⚠ HR's write gate is on this tab (HR.Compensation.Write). The payroll routes the editor saves
 * through are open to any internal user until payroll gates them (cross-module defect #11 —
 * recorded, not HR's to fix). The gate here is what stops an HR read-only user from reaching them
 * through this screen; it does not pretend to close the route.
 */

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { employeeService } from '@/services/hr/employee.service';
import { payrollService } from '@/services/payrollService';
import {
  PAY_BASIS_OPTIONS,
  PAYROLL_ISSUE_LABELS,
  type EmployeeDetail,
  type PayBasis,
} from '@/types/hr/employee';
import { PayrollEmployeeProfileEditor, money } from '@/components/hr/payroll/PayrollEmployeeProfileEditor';
import { SalaryAssignmentsTab } from './SalaryAssignmentsTab';
import { SalaryChangesCard } from './SalaryChangesCard';
import { PayrollComponentExceptionsCard } from './PayrollComponentExceptionsCard';
import { policySettingsService } from '@/services/hr/policy-settings.service';

const COMPENSATION_WRITE = 'HR.Compensation.Write';

export function SalaryTab({ employee }: { employee: EmployeeDetail }) {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission(COMPENSATION_WRITE);
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const employeeId = employee.id;
  const employeeNumber = employee.employeeNumber;
  const negotiated = employee.payBasis === 'Negotiated';

  // Round 3, lane S. Until the policy is known the direct doors stay locked — the safe reading.
  const policy = useQuery({ queryKey: ['hr', 'policy-settings'], queryFn: () => policySettingsService.get(), staleTime: 60_000 });
  const requiresApproval = policy.data ? policy.data.salaryChangeRequiresApproval : true;
  const directWrite = canWrite && !requiresApproval;

  // Both sides in one read: HR's basis and resolved figure, payroll's profile state, the issue.
  const status = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'payroll-status'],
    queryFn: () => employeeService.getPayrollStatus(employeeId),
  });

  // Payroll's profile through HR's door. Null is a normal answer — nobody has set them up yet.
  const profile = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'payroll-profile'],
    queryFn: () => employeeService.getPayrollProfile(employeeId),
  });

  const refreshAll = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['hr', 'employees', employeeId] }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'employees', 'payroll-reconciliation'] }),
      queryClient.invalidateQueries({ queryKey: ['payroll'] }),
    ]);
  };

  return (
    <div className="space-y-4">
      <SalaryChangesCard
        employee={employee}
        canWrite={canWrite}
        requiresApproval={requiresApproval}
        onApplied={refreshAll}
      />

      <PayBasisCard
        employee={employee}
        status={status.data}
        canWrite={directWrite}
        onChanged={refreshAll}
      />

      {/* A placement contradicts a negotiated amount, and the server refuses one — so the section
          is not offered rather than offering a form that always fails. The history is still there
          the day they return to the scale. */}
      {!negotiated && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Grade placement</CardTitle>
            <CardDescription>
              Where this person sits on the salary scale. The notch amount is their basic pay.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <SalaryAssignmentsTab employeeId={employeeId} isOnPayroll={employee.isOnPayroll} lockedReason={requiresApproval ? 'On this tenant a placement is changed through an approved salary change request — raise one in the card above.' : undefined} />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Payroll profile</CardTitle>
          <CardDescription>
            Payroll&apos;s own record for this person — the basic salary it pays, the switches, and how
            the net is split. This is the same window as Payroll → Employee Profiles.
            {negotiated && ' The monthly basic salary here IS the negotiated amount.'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {!employee.isOnPayroll && (
            <div className="mb-3 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100">
              This employee is not on payroll, so the profile is shown read-only. Put them on payroll
              (Edit → Compensation &amp; Tax) to maintain it.
            </div>
          )}
          {profile.isLoading ? (
            <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Reading payroll&apos;s profile…
            </div>
          ) : (
            <>
              {!profile.data && (
                <p className="mb-3 text-sm text-muted-foreground">
                  Payroll has no profile for this person yet.
                  {employee.isOnPayroll && canWrite
                    ? ' Fill in the salary and a payment method below and save to create one.'
                    : ''}
                </p>
              )}
              <PayrollEmployeeProfileEditor
                employeeId={employeeId}
                employeeNumber={employeeNumber}
                initialProfile={profile.data ?? null}
                reload={() => employeeService.getPayrollProfile(employeeId)}
                readOnly={!employee.isOnPayroll}
                canSave={canWrite}
                basicSalaryLocked={requiresApproval}
                onSaved={() => {
                  toast({ title: 'Payroll profile saved' });
                  void refreshAll();
                }}
              />
            </>
          )}
        </CardContent>
      </Card>

      {/* Round 3, lane X (D-3): allowances and deductions, employee-first, saved one row at a time
          through payroll's own bulk endpoint — the probe proved it touches only the lines it is sent. */}
      <PayrollComponentExceptionsCard employee={employee} canWrite={canWrite} />

      <PayrollItems employeeNumber={employeeNumber} />
    </div>
  );
}

// ── 1. Pay basis ─────────────────────────────────────────────────────────────

function PayBasisCard({
  employee,
  status,
  canWrite,
  onChanged,
}: {
  employee: EmployeeDetail;
  status?: { hrMonthlyBasicPay?: number | null; hrBasicPaySource?: string | null; issue?: string | null; payrollCurrencyCode?: string | null };
  canWrite: boolean;
  onChanged: () => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [basis, setBasis] = useState<PayBasis>(employee.payBasis);
  const [note, setNote] = useState(employee.payBasisNote ?? '');
  const { toast } = useToast();

  const save = useMutation({
    mutationFn: () => employeeService.setPayBasis(employee.id, { payBasis: basis, note: note.trim() || null }),
    onSuccess: async () => {
      toast({ title: 'Pay basis updated' });
      setOpen(false);
      await onChanged();
    },
    onError: (error: any) => {
      toast({
        title: 'Could not change the pay basis',
        description: error?.message ?? 'The change was refused.',
        variant: 'destructive',
      });
    },
  });

  const label = PAY_BASIS_OPTIONS.find((o) => o.value === employee.payBasis)?.label ?? employee.payBasis;
  const switchingToNegotiated = employee.payBasis === 'SalaryScale' && basis === 'Negotiated';

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-3">
          <div>
            <CardTitle className="text-base">Pay basis</CardTitle>
            <CardDescription>How this person&apos;s basic pay is arrived at.</CardDescription>
          </div>
          {canWrite && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setBasis(employee.payBasis);
                setNote(employee.payBasisNote ?? '');
                setOpen(true);
              }}
            >
              <Pencil className="mr-2 h-3.5 w-3.5" /> Change
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="grid gap-3 sm:grid-cols-3">
        <div>
          <div className="text-xs text-muted-foreground">Basis</div>
          <div className="mt-0.5 flex items-center gap-2 text-sm">
            <Badge variant={employee.payBasis === 'Negotiated' ? 'default' : 'secondary'}>{label}</Badge>
          </div>
          {employee.payBasisNote && (
            <p className="mt-1 text-xs text-muted-foreground">{employee.payBasisNote}</p>
          )}
        </div>
        <div>
          <div className="text-xs text-muted-foreground">Monthly basic pay</div>
          <div className="mt-0.5 text-sm font-medium tabular-nums">
            {status?.hrMonthlyBasicPay != null ? money(status.hrMonthlyBasicPay, status.payrollCurrencyCode ?? 'GHS') : '—'}
          </div>
          {/* The source, in words: which notch, or payroll's basis, or the record figure. A number
              with no provenance is the thing the demo feedback kept asking about. */}
          {status?.hrBasicPaySource && (
            <p className="mt-1 text-xs text-muted-foreground">{status.hrBasicPaySource}</p>
          )}
        </div>
        <div>
          <div className="text-xs text-muted-foreground">Payroll reconciliation</div>
          <div className="mt-0.5 text-sm">
            {status?.issue ? (
              <span className="text-amber-700 dark:text-amber-300">
                {PAYROLL_ISSUE_LABELS[status.issue as keyof typeof PAYROLL_ISSUE_LABELS] ?? status.issue}
              </span>
            ) : (
              <span className="text-emerald-700 dark:text-emerald-300">HR and Payroll agree</span>
            )}
          </div>
        </div>
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Change pay basis</DialogTitle>
            <DialogDescription>
              Scale or negotiated. Independent of employment type and of payroll membership.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              {PAY_BASIS_OPTIONS.map((o) => (
                <label
                  key={o.value}
                  className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 ${basis === o.value ? 'border-primary bg-muted/40' : ''}`}
                >
                  <input
                    type="radio"
                    name="payBasis"
                    className="mt-1"
                    checked={basis === o.value}
                    onChange={() => setBasis(o.value)}
                  />
                  <span>
                    <span className="block text-sm font-medium">{o.label}</span>
                    <span className="block text-xs text-muted-foreground">{o.description}</span>
                  </span>
                </label>
              ))}
            </div>
            {basis === 'Negotiated' && (
              <div className="space-y-1.5">
                <Label htmlFor="payBasisNote">Why negotiated — who agreed what, and when</Label>
                <Textarea
                  id="payBasisNote"
                  rows={3}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="Contract engagement, rate per agreement of 2026-07-01."
                  maxLength={500}
                />
                <p className="text-xs text-muted-foreground">
                  Required. A negotiated figure with nothing behind it cannot be defended when questioned.
                </p>
              </div>
            )}
            {switchingToNegotiated && (
              <p className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100">
                Any open grade placement is closed as of yesterday: the notch stops being this person&apos;s
                pay. The amount on the payroll profile becomes the basis.
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={save.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => save.mutate()}
              disabled={save.isPending || (basis === 'Negotiated' && !note.trim())}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

// ── 4. Payroll items (read-only) ─────────────────────────────────────────────

function PayrollItems({ employeeNumber }: { employeeNumber: string }) {
  // Loans and advances filter by staff number on payroll's side. Tax reliefs have no employee
  // filter on payroll's routes, so that one is the tenant's whole list narrowed here — recorded in
  // the round-2 plan's § 7.1 as a by-employee read payroll still owes. ⚠ Component exceptions
  // USED to be read the same way and never showed a row: payroll's read answers an EMPTY list when
  // no component is named. Round 3, lane X replaced it with HR's employee-first door (the card above).
  const loans = useQuery({
    queryKey: ['payroll', 'loans', employeeNumber],
    queryFn: () => payrollService.getLoans({ employeeNumber, includeInactive: false }),
  });
  const advances = useQuery({
    queryKey: ['payroll', 'salary-advances', employeeNumber],
    queryFn: () => payrollService.getSalaryAdvances({ employeeNumber }),
  });
  const reliefs = useQuery({
    queryKey: ['payroll', 'employee-tax-reliefs'],
    queryFn: () => payrollService.getEmployeeTaxReliefs(),
  });

  const myReliefs = useMemo(
    () => (reliefs.data ?? []).filter((r) => r.employeeNumber === employeeNumber),
    [reliefs.data, employeeNumber],
  );

  const loading = loans.isLoading || advances.isLoading || reliefs.isLoading;
  const empty =
    !loading &&
    (loans.data?.length ?? 0) === 0 &&
    (advances.data?.length ?? 0) === 0 &&
    myReliefs.length === 0;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Payroll items</CardTitle>
        <CardDescription>
          Loans, advances and tax reliefs payroll holds for this staff number. Maintained in
          Payroll; shown here so the whole picture is on one screen.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading && (
          <div className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Reading payroll…
          </div>
        )}
        {empty && <p className="text-sm text-muted-foreground">Payroll holds no items for this person.</p>}

        {(loans.data?.length ?? 0) > 0 && (
          <ItemTable
            title="Loans"
            headers={['Facility', 'Granted', 'Amount', 'Monthly', 'Outstanding']}
            rows={(loans.data ?? []).map((l) => [
              l.facilityNumber,
              l.dateGranted?.slice(0, 10) ?? '—',
              money(l.amountGranted),
              money(l.monthlyRepaymentAmount),
              money(l.outstandingBalance),
            ])}
          />
        )}
        {(advances.data?.length ?? 0) > 0 && (
          <ItemTable
            title="Salary advances"
            headers={['Date', 'Amount', 'Description', 'Active']}
            rows={(advances.data ?? []).map((a) => [
              a.advanceDate?.slice(0, 10) ?? '—',
              money(a.advanceAmount),
              a.description ?? '—',
              a.isActive ? 'Yes' : 'No',
            ])}
          />
        )}
        {myReliefs.length > 0 && (
          <ItemTable
            title="Tax reliefs"
            headers={['Relief', 'Amount', 'Factor', 'Total', 'Active']}
            rows={myReliefs.map((r) => [
              `${r.reliefCode} — ${r.reliefName}`,
              money(r.amount),
              String(r.factor),
              money(r.totalRelief),
              r.isActive ? 'Yes' : 'No',
            ])}
          />
        )}
      </CardContent>
    </Card>
  );
}

function ItemTable({ title, headers, rows }: { title: string; headers: string[]; rows: string[][] }) {
  return (
    <div>
      <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</div>
      <div className="overflow-x-auto rounded-md border">
        <Table className="text-xs">
          <TableHeader>
            <TableRow>
              {headers.map((h) => (
                <TableHead key={h} className="h-8">{h}</TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((r, i) => (
              <TableRow key={i}>
                {r.map((c, j) => (
                  <TableCell key={j} className="py-1.5">{c}</TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
