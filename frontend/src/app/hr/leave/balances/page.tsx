'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, RefreshCw, Scale, Wallet, Wrench } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { leaveService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { LeaveBalanceDetailDialog } from '@/components/hr/leave/LeaveBalanceDetailDialog';
import { fmtDay } from '@/components/hr/leave/AccrualStatementPanel';
import type { LeaveBalance, LeaveEntitlementRepairResult } from '@/types/hr/leave-request';

/** Saves a blob the browser already has, rather than navigating to a URL that carries no token. */
function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

const ALL = '__all__';
const PAGE_SIZE = 50;

/**
 * Round 5, lane J: the page opens on ANNUAL leave for every employee still serving — the balance the
 * stakeholders asked to see first — and the Overview is the page as it was: every leave type, every
 * record that exists.
 */
type View = 'annual' | 'overview';

export default function LeaveBalancesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // Round 5, C4: opens on the leave year we are in, which is not the calendar year from January to
  // the start month of a leave year that does not start in January. A choice the user makes wins.
  const { currentYear } = useLeaveYear();
  const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [leaveTypeId, setLeaveTypeId] = useState<string>(ALL);
  const [chosenYear, setChosenYear] = useState<string | null>(null);
  const year = chosenYear ?? String(currentYear);
  const [openBalanceId, setOpenBalanceId] = useState<string | null>(null);
  const [recalculating, setRecalculating] = useState(false);
  const [recalculatingAll, setRecalculatingAll] = useState(false);
  const [confirmAll, setConfirmAll] = useState(false);
  const [exporting, setExporting] = useState(false);
  // The repair pass is always previewed before it is applied, so it needs somewhere to hold the
  // preview between the two calls.
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [repairPreview, setRepairPreview] = useState<LeaveEntitlementRepairResult | null>(null);
  const [view, setView] = useState<View>('annual');
  const [unitId, setUnitId] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  // Both bulk passes are Admin, which the HR role does not hold. Hidden rather than offered and
  // refused — the rule the closure build applied to the rulebook's delete controls (L-11).
  const { canAdminister } = useLeavePermissions();

  // The CSV comes from the SAME server read the table uses, so it carries the live accrued and
  // "can take now" figures rather than a second derivation that would quietly disagree (L-18).
  const exportCsv = async () => {
    setExporting(true);
    try {
      // The file follows the view: the annual list, or every record.
      const blob =
        view === 'annual'
          ? await leaveService.exportAnnualBalances(Number(year), employeeId ?? undefined, unitId || undefined)
          : await leaveService.exportBalances(
              Number(year),
              employeeId ?? undefined,
              leaveTypeId === ALL ? undefined : leaveTypeId,
            );
      saveBlob(blob, view === 'annual' ? `annual-leave-${year}.csv` : `leave-balances-${year}.csv`);
    } catch (e: any) {
      toast({
        title: 'Export failed',
        description: e?.message || 'The balances could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setExporting(false);
    }
  };

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data: overview, isLoading: overviewLoading } = useQuery({
    queryKey: ['hr', 'leave-balances', year, employeeId, leaveTypeId],
    queryFn: () =>
      leaveService.getBalances(
        Number(year),
        employeeId ?? undefined,
        leaveTypeId === ALL ? undefined : leaveTypeId,
      ),
    enabled: view === 'overview',
  });

  // Round 5, lane J: everybody still serving, including the many no request has opened an annual
  // record for yet — their figures are worked out live, and nothing is created by reading them.
  const {
    data: annual,
    isLoading: annualLoading,
    isError: annualFailed,
    error: annualError,
  } = useQuery({
    queryKey: ['hr', 'leave-balances', 'annual', year, employeeId, unitId],
    queryFn: () =>
      leaveService.getAnnualBalances(Number(year), employeeId ?? undefined, unitId || undefined),
    enabled: view === 'annual',
  });

  const recalculate = async () => {
    if (!employeeId) {
      toast({
        title: 'Choose an employee',
        description: 'Recalculation runs for one employee at a time.',
        variant: 'destructive',
      });
      return;
    }
    setRecalculating(true);
    try {
      await leaveService.recalculateBalance({
        employeeId,
        year: Number(year),
        leaveTypeId: leaveTypeId === ALL ? null : leaveTypeId,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({ title: 'Recalculated', description: 'Balances rebuilt from entitlement and usage.' });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to recalculate.',
        variant: 'destructive',
      });
    } finally {
      setRecalculating(false);
    }
  };

  /**
   * ⚠ Every balance in the tenant for the chosen year (finding L-19).
   *
   * The per-employee call above is right for the ordinary case — it runs after every approval,
   * cancellation and adjustment. But a policy correction reaches everybody, and L-19's complaint
   * was precisely that such a correction "has NO ROUTE THROUGH THE UI". An endpoint alone did not
   * answer that; this button is the answer.
   *
   * Safe to run and safe to re-run: it DERIVES the counters from requests and adjustments that
   * already exist, never invents a figure, and never touches entitled or carried-over days. That
   * is why it needs no preview, unlike the year-end jobs, which MOVE balances.
   */
  const recalculateAll = async () => {
    setRecalculatingAll(true);
    try {
      const res = await leaveService.recalculateAllBalances(
        Number(year),
        leaveTypeId === ALL ? undefined : leaveTypeId,
      );
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });

      // ⚠ A failure count is a BUG SIGNAL, not routine — the whole point of a derived figure is
      // that deriving it cannot fail. Said loudly rather than folded into a success message.
      toast({
        title: res.employeesFailed > 0 ? 'Finished with failures' : 'Recalculated',
        description:
          `${res.employeesProcessed} employee(s) rebuilt for ${year}.` +
          (res.employeesFailed > 0
            ? ` ⚠ ${res.employeesFailed} could not be finished — see the server log.`
            : ''),
        variant: res.employeesFailed > 0 ? 'destructive' : undefined,
      });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'The tenant-wide recalculation failed.',
        variant: 'destructive',
      });
    } finally {
      setRecalculatingAll(false);
      setConfirmAll(false);
    }
  };

  /**
   * ⚠ Entitlement repair (plan A3), and it is deliberately TWO calls.
   *
   * `EntitledDays` is written once, when a balance row is created, and no path has ever refreshed
   * it — so an allocation corrected mid-year never reaches the rows that already exist, and a
   * balance opened by posting an adjustment used to record the leave type's DEFAULT days rather
   * than the employee's staff-level allocation (A1).
   *
   * Unlike "Recalculate everybody", this OVERWRITES a stored figure from configuration that may
   * have moved since. So the button previews, the dialog shows every row and both figures, and a
   * person presses Apply. Nothing about this pass is safe to run blind.
   */
  const previewRepair = async () => {
    setPreviewing(true);
    try {
      const res = await leaveService.repairEntitlements(Number(year), {
        leaveTypeId: leaveTypeId === ALL ? undefined : leaveTypeId,
        employeeId: employeeId ?? undefined,
        dryRun: true,
      });
      setRepairPreview(res);
    } catch (e: any) {
      toast({
        title: 'Could not preview',
        description: e?.message || 'The entitlement repair could not be previewed.',
        variant: 'destructive',
      });
    } finally {
      setPreviewing(false);
    }
  };

  const applyRepair = async () => {
    setApplying(true);
    try {
      const res = await leaveService.repairEntitlements(Number(year), {
        leaveTypeId: leaveTypeId === ALL ? undefined : leaveTypeId,
        employeeId: employeeId ?? undefined,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({
        title: 'Entitlements repaired',
        description:
          `${res.balancesChanged} of ${res.balancesExamined} balance(s) brought into line for ${year}.` +
          (res.changedWithCarryOverAlreadyRun > 0
            ? ` ⚠ ${res.changedWithCarryOverAlreadyRun} already carried days into ${Number(year) + 1}` +
              ' from the old figure — that carry-over was not revisited.'
            : ''),
      });
      setRepairPreview(null);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'The entitlement repair failed.',
        variant: 'destructive',
      });
    } finally {
      setApplying(false);
    }
  };

  const annualRows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = annual ?? [];
    if (!term) return all;
    return all.filter((b) =>
      [b.employeeName, b.employeeNumber, b.organizationUnitName]
        .filter((v): v is string => !!v)
        .some((v) => v.toLowerCase().includes(term)),
    );
  }, [annual, search]);
  const pages = Math.max(1, Math.ceil(annualRows.length / PAGE_SIZE));
  const current = Math.min(page, pages);

  const rows: LeaveBalance[] =
    view === 'annual'
      ? annualRows.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE)
      : overview ?? [];
  const isLoading = view === 'annual' ? annualLoading : overviewLoading;
  const columns = view === 'annual' ? 10 : 11;

  // Round 5, C2: "Accrued" says as at when. The server says, per row — a leaver's figure stops at
  // their last day — so the header carries the date the rows share and a row that differs says its own.
  const asOfDates = [...new Set(rows.map((b) => b.accruedAsOf).filter(Boolean))] as string[];
  const sharedAsOf = asOfDates.length === 1 ? asOfDates[0] : null;

  return (
    <div className="space-y-6 p-6">
      <LeaveBalanceDetailDialog balanceId={openBalanceId} onClose={() => setOpenBalanceId(null)} />
      <PageHeader
        title="Leave Balances"
        description="Entitlement, accrual, usage and what remains."
        actions={
          <>
          {/* Round 5, C6: annual leave built up and not yet taken, as at a date, for Finance. */}
          <Button variant="outline" asChild>
            <Link href="/hr/leave/balances/owed">
              <Wallet className="mr-2 h-4 w-4" />
              Leave owed
            </Link>
          </Button>
          <Button variant="outline" onClick={exportCsv} disabled={exporting}>
            {exporting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Download className="mr-2 h-4 w-4" />
            )}
            Export CSV
          </Button>
          <Button variant="outline" onClick={recalculate} disabled={recalculating}>
            {recalculating ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="mr-2 h-4 w-4" />
            )}
            Recalculate
          </Button>
          {canAdminister && (
            <Button
              variant="outline"
              onClick={() => setConfirmAll(true)}
              disabled={recalculatingAll}
            >
              {recalculatingAll ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Recalculate everybody
            </Button>
          )}
          {canAdminister && (
            <Button variant="outline" onClick={previewRepair} disabled={previewing}>
              {previewing ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Wrench className="mr-2 h-4 w-4" />
              )}
              Repair entitlements
            </Button>
          )}
          </>
        }
      />

      {/*
        The preview IS the decision. It names every row and both figures, because the alternative —
        a confirmation that says "this will correct some entitlements" — asks somebody to authorise
        a change they cannot see.
      */}
      <ConfirmationDialog
        open={!!repairPreview}
        onOpenChange={(open) => !open && setRepairPreview(null)}
        title={`Repair entitlements for ${year}?`}
        maxWidth="sm:max-w-2xl"
        description={
          repairPreview ? (
            <span className="block space-y-3">
              <span className="block">{repairPreview.notes[0]}</span>

              {repairPreview.balancesChanged === 0 ? (
                <span className="block font-medium">
                  Every entitlement already matches the rulebook. There is nothing to apply.
                </span>
              ) : (
                <>
                  <span className="block">
                    Each line is a stored entitlement that disagrees with what the rulebook resolves
                    today — the sub-type cap, then the effective-dated allocation for that
                    employee&apos;s staff level, then the leave type&apos;s default.
                  </span>
                  <span className="block max-h-64 overflow-y-auto rounded-md border p-3 text-xs">
                    {repairPreview.notes.slice(1).map((n, i) => (
                      <span key={i} className="block py-0.5">
                        {n}
                      </span>
                    ))}
                  </span>
                </>
              )}

              {repairPreview.changedWithCarryOverAlreadyRun > 0 && (
                <span className="block font-medium text-amber-700 dark:text-amber-300">
                  ⚠ {repairPreview.changedWithCarryOverAlreadyRun} of these already carried days into{' '}
                  {Number(year) + 1}, computed from the figure being replaced. This pass does not
                  revisit a carry-over — re-running it is a decision of its own.
                </span>
              )}

              {repairPreview.balancesFailed > 0 && (
                <span className="block font-medium text-destructive">
                  ⚠ {repairPreview.balancesFailed} balance(s) could not be resolved at all — usually
                  an employee with no position, and therefore no staff level. They are listed above
                  and will be skipped.
                </span>
              )}
            </span>
          ) : null
        }
        confirmText={`Apply to ${repairPreview?.balancesChanged ?? 0} balance(s)`}
        confirmDisabled={!repairPreview || repairPreview.balancesChanged === 0}
        isLoading={applying}
        onConfirm={() => applyRepair()}
      />

      {/* Heavy, and it touches everybody — so it asks first. */}
      <ConfirmationDialog
        open={confirmAll}
        onOpenChange={setConfirmAll}
        title={`Recalculate every balance for ${year}?`}
        description={
          'This rebuilds UsedDays, PendingDays and AdjustmentDays for every employee who has a '
          + 'balance in this year, from the requests and adjustments already on record. It never '
          + 'touches entitled or carried-over days, and running it twice gives the same answer as '
          + 'running it once — so there is nothing to undo. It is simply heavy.'
        }
        confirmText="Recalculate everybody"
        isLoading={recalculatingAll}
        onConfirm={() => recalculateAll()}
      />

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
          <CardTitle>Filters</CardTitle>
          <Tabs
            value={view}
            onValueChange={(v) => {
              setView(v as View);
              setPage(1);
            }}
          >
            <TabsList>
              <TabsTrigger value="annual">Annual leave</TabsTrigger>
              <TabsTrigger value="overview">Overview — every type</TabsTrigger>
            </TabsList>
          </Tabs>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker
                value={employeeId}
                onChange={(v) => {
                  setEmployeeId(v);
                  setPage(1);
                }}
              />
            </div>
            {view === 'overview' && (
              <div className="space-y-2">
                <label className="text-sm font-medium">Leave type</label>
                <Select value={leaveTypeId} onValueChange={setLeaveTypeId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL}>All leave types</SelectItem>
                    {(leaveTypes ?? []).map((t) => (
                      <SelectItem key={t.id} value={t.id}>
                        {t.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="space-y-2">
              <label className="text-sm font-medium">Year</label>
              <Select value={year} onValueChange={setChosenYear}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {years.map((y) => (
                    <SelectItem key={y} value={String(y)}>
                      {y}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {view === 'annual' && (
            <OrganizationUnitPicker
              idPrefix="balances-unit"
              value={unitId}
              onChange={(id) => {
                setUnitId(id);
                setPage(1);
              }}
              allowNone="Any unit"
              levelLabel="Level"
              unitLabel="Unit — and every unit beneath it"
              className="grid gap-4 md:grid-cols-3"
            />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
          <div className="space-y-1">
            <CardTitle>{view === 'annual' ? 'Annual leave' : 'Balances'}</CardTitle>
            {view === 'annual' && (
              <p className="text-sm text-muted-foreground">
                Everybody still serving
                {annual ? ` — ${annual.length.toLocaleString()} ${annual.length === 1 ? 'person' : 'people'}` : ''}. Where
                no request has opened a record yet, the figures are worked out live, exactly as the
                record will hold them. Leavers are not listed; the Overview shows every record.
              </p>
            )}
          </div>
          {view === 'annual' && (
            <Input
              placeholder="Search name, staff number or unit"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="w-[280px]"
            />
          )}
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  {view === 'overview' && <TableHead>Leave type</TableHead>}
                  <TableHead className="text-right">Entitled</TableHead>
                  <TableHead className="text-right">
                    Accrued
                    {sharedAsOf && (
                      <span className="block text-xs font-normal">as at {fmtDay(sharedAsOf)}</span>
                    )}
                  </TableHead>
                  <TableHead className="text-right">Carried over</TableHead>
                  <TableHead className="text-right">Adjustments</TableHead>
                  <TableHead className="text-right">Used</TableHead>
                  <TableHead className="text-right">Pending</TableHead>
                  <TableHead className="text-right">Encashed</TableHead>
                  <TableHead className="text-right">Available</TableHead>
                  <TableHead className="text-right">Can take now</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(columns)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[60px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : view === 'annual' && annualFailed ? (
                  <TableRow>
                    <TableCell colSpan={columns}>
                      <EmptyState
                        icon={Scale}
                        title="No annual leave to show"
                        description={
                          (annualError as any)?.message ||
                          'Set the kind of the annual leave type to Annual, and it appears here.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={columns}>
                      <EmptyState
                        icon={Scale}
                        title={view === 'annual' && search ? 'Nobody matches' : 'No balances'}
                        description={
                          view === 'annual'
                            ? search
                              ? 'Try a shorter search.'
                              : 'Nobody still serving matches these filters.'
                            : 'Balances appear once leave types have allocations and employees are entitled.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((b) => {
                    // A row worked out live has no record to open (round 5, lane J).
                    const opens = b.hasRecord !== false;
                    return (
                    <TableRow
                      key={opens ? b.id : `live-${b.employeeId}`}
                      className={opens ? 'cursor-pointer hover:bg-muted/50' : undefined}
                      onClick={opens ? () => setOpenBalanceId(b.id) : undefined}
                      title={
                        opens
                          ? 'Open the balance and how it built up'
                          : 'No request has opened a record yet; these figures are worked out live'
                      }
                    >
                      <TableCell className="font-medium">
                        {b.employeeName}
                        {(b.employeeNumber || b.organizationUnitName) && (
                          <span className="block text-xs text-muted-foreground">
                            {[view === 'annual' ? b.employeeNumber : null, b.organizationUnitName]
                              .filter(Boolean)
                              .join(' · ')}
                          </span>
                        )}
                        {!opens && (
                          <span className="block text-xs font-normal italic text-muted-foreground">
                            no record yet — worked out live
                          </span>
                        )}
                        {b.accessibleFrom && (
                          <span className="block text-xs font-normal text-amber-700 dark:text-amber-400">
                            may take it from {fmtDay(b.accessibleFrom)}
                          </span>
                        )}
                      </TableCell>
                      {view === 'overview' && (
                        <TableCell>
                          {b.leaveTypeName}
                          {b.leaveSubTypeName ? ` · ${b.leaveSubTypeName}` : ''}
                        </TableCell>
                      )}
                      <TableCell className="text-right">{b.entitledDays}</TableCell>
                      <TableCell className="text-right">
                        {b.accruedToDateDays}
                        {b.accruedAsOf && b.accruedAsOf !== sharedAsOf && (
                          <span className="block text-xs text-muted-foreground">
                            to {fmtDay(b.accruedAsOf)}
                          </span>
                        )}
                      </TableCell>
                      <TableCell className="text-right">{b.carriedOverDays}</TableCell>
                      <TableCell className="text-right">{b.adjustmentDays}</TableCell>
                      <TableCell className="text-right">{b.usedDays}</TableCell>
                      <TableCell className="text-right">{b.pendingDays}</TableCell>
                      <TableCell className="text-right">{b.encashedDays}</TableCell>
                      <TableCell className="text-right">{b.availableDays}</TableCell>
                      {/*
                        The figure the create check enforces. On an accruing type it is below
                        Available until the year is fully accrued — that difference is the single
                        most common reason a request is refused (closure plan L-3 / L-14).
                      */}
                      <TableCell className="text-right font-medium">
                        {b.accruedAvailableDays}
                      </TableCell>
                    </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
          {view === 'annual' && pages > 1 && (
            <div className="mt-3 flex items-center justify-end gap-2 text-sm">
              <span className="text-muted-foreground">
                {(current - 1) * PAGE_SIZE + 1}–{Math.min(current * PAGE_SIZE, annualRows.length)} of{' '}
                {annualRows.length.toLocaleString()}
              </span>
              <Button variant="outline" size="sm" disabled={current <= 1} onClick={() => setPage(current - 1)}>
                Previous
              </Button>
              <Button variant="outline" size="sm" disabled={current >= pages} onClick={() => setPage(current + 1)}>
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
