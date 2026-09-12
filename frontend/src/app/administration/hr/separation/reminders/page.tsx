'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlarmClock,
  CalendarSearch,
  FileWarning,
  Loader2,
  Play,
  TriangleAlert,
  UserRoundX,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { separationService } from '@/services/hr/separation.service';
import type {
  SeparationReminderKind,
  SweepResult,
  UpcomingContractExpiry,
  UpcomingRetirement,
} from '@/types/hr/separation';
import { toast } from 'sonner';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const KIND_LABEL: Record<SeparationReminderKind, string> = {
  RetirementApproaching: 'Retirement approaching',
  ContractExpiring: 'Contract expiring',
  ClearanceOutstanding: 'Clearance outstanding',
  SettlementAwaitingReview: 'Settlement awaiting review',
  SettlementApprovedNotCompleted: 'Approved, not completed',
};

/** Tier 0 is a countdown; 1 and above are overruns. Colour follows meaning, not order. */
const tierTone = (tier: number) =>
  tier === 0
    ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200'
    : tier === 1
      ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200'
      : 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200';

/** The horizon the two sweeps and the two lists share. Days, because the API takes days. */
const HORIZONS = [
  { label: '90 days', value: 90 },
  { label: '6 months', value: 182 },
  { label: '1 year', value: 365 },
  { label: '2 years', value: 730 },
];

/**
 * Separation reminders, retirements and contract expiries (FR-HR-093, FR-HR-111).
 *
 * ⚠ **The two sweeps are not previews — they RAISE separations**, which is why each sits behind a
 * confirmation naming the horizon and the count. The lists above them are the dry run: every row
 * already shows whether a separation exists for that person, so "how many are actually new" is
 * answerable before the button is pressed rather than after.
 *
 * ⚠ **The horizon is the whole screen.** At the default 90 days both lists are usually empty on
 * this data — nobody retires that soon — and an empty list with no horizon control reads as "the
 * feature is broken" rather than "nothing is due yet". Proven while building: at 3,650 days the
 * same endpoints return 12 retirements and 10 expiries.
 *
 * Both sweeps also run nightly on `SeparationReminderBackgroundService` at a 17-minute stagger, so
 * this screen is the manual run — for seeing the effect of a policy change today rather than
 * tomorrow — not the only thing that fires them.
 */
export default function SeparationRemindersPage() {
  const qc = useQueryClient();
  const [withinDays, setWithinDays] = useState(365);
  const [confirming, setConfirming] = useState<'retirement' | 'contract' | null>(null);

  const { data: preview, isFetching: previewing } = useQuery({
    queryKey: ['separation-reminder-preview'],
    queryFn: () => separationService.previewReminders(),
  });

  const { data: retirements, isFetching: loadingRetirements } = useQuery({
    queryKey: ['separation-upcoming-retirements', withinDays],
    queryFn: () => separationService.getUpcomingRetirements(withinDays, true),
  });

  const { data: expiries, isFetching: loadingExpiries } = useQuery({
    queryKey: ['separation-upcoming-expiries', withinDays],
    queryFn: () => separationService.getUpcomingContractExpiries(withinDays, true),
  });

  const refreshAll = () => {
    void qc.invalidateQueries({ queryKey: ['separation-reminder-preview'] });
    void qc.invalidateQueries({ queryKey: ['separation-upcoming-retirements'] });
    void qc.invalidateQueries({ queryKey: ['separation-upcoming-expiries'] });
    void qc.invalidateQueries({ queryKey: ['separations'] });
  };

  // A sweep that raises nothing is a healthy outcome, and a silent success would look identical to
  // a sweep that never ran — so every count is reported, including the zeroes.
  const describeSweep = (r: SweepResult) => {
    const parts = [`${r.dueCount} due`, `${r.raisedCount} raised`];
    if (r.skippedExistingCount > 0) parts.push(`${r.skippedExistingCount} already had one`);
    if (r.failures.length > 0) parts.push(`${r.failures.length} failed`);
    return parts.join(' · ');
  };

  const retirementSweep = useMutation({
    mutationFn: () => separationService.runRetirementSweep(withinDays),
    onSuccess: (r) => {
      if (r.failures.length > 0) toast.warning(`Retirement sweep — ${describeSweep(r)}`);
      else toast.success(`Retirement sweep — ${describeSweep(r)}`);
      refreshAll();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  const contractSweep = useMutation({
    mutationFn: () => separationService.runContractExpirySweep(withinDays),
    onSuccess: (r) => {
      if (r.failures.length > 0) toast.warning(`Contract-expiry sweep — ${describeSweep(r)}`);
      else toast.success(`Contract-expiry sweep — ${describeSweep(r)}`);
      refreshAll();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  const reminderRun = useMutation({
    mutationFn: () => separationService.runReminderSweep(),
    onSuccess: (r) => {
      const kinds = Object.entries(r.byKind)
        .map(([k, n]) => `${n} ${KIND_LABEL[k as SeparationReminderKind] ?? k}`)
        .join(', ');
      toast.success(
        r.remindersQueued === 0
          ? `Sweep ran — nothing new to send (${r.suppressedAsDuplicate} already raised)`
          : `Sweep ran — ${r.remindersQueued} queued (${kinds})`,
      );
      refreshAll();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  // What the sweep would actually create, as opposed to what is merely due. The API skips anyone
  // who already has a separation, and every row carries that fact, so the honest number is here.
  const newRetirements = (retirements ?? []).filter((r) => !r.existingSeparationId);
  const newExpiries = (expiries ?? []).filter((r) => !r.existingSeparationId);

  const sweepBusy = retirementSweep.isPending || contractSweep.isPending;
  const pendingKind = confirming === 'retirement' ? 'retirement' : 'contract expiry';
  const pendingCount = confirming === 'retirement' ? newRetirements.length : newExpiries.length;

  const personCell = (
    name: string,
    number?: string | null,
    position?: string | null,
    unit?: string | null,
  ) => (
    <div>
      <p className="font-medium">{name}</p>
      <p className="text-xs text-muted-foreground">
        {[number, position, unit].filter(Boolean).join(' · ') || '—'}
      </p>
    </div>
  );

  const existingCell = (row: UpcomingRetirement | UpcomingContractExpiry) =>
    row.existingSeparationId ? (
      <Link
        href={`/hr/separations/${row.existingSeparationId}`}
        className="text-primary hover:underline"
      >
        {row.existingSeparationNumber ?? 'View'}
        {row.existingSeparationStatus ? ` (${row.existingSeparationStatus})` : ''}
      </Link>
    ) : (
      <Badge variant="secondary">Not raised</Badge>
    );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Separation reminders & sweeps"
        description="Retirements, contract expiries, and the chase queue for clearance and settlement."
        backHref="/administration/hr"
        actions={
          <Button onClick={() => reminderRun.mutate()} disabled={reminderRun.isPending}>
            {reminderRun.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Play className="mr-2 h-4 w-4" />
            )}
            Run the reminder sweep
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 p-4">
          <div className="space-y-2">
            <Label htmlFor="horizon">Look ahead</Label>
            <Input
              id="horizon"
              type="number"
              min={1}
              max={7300}
              value={withinDays}
              onChange={(e) => setWithinDays(Math.max(1, Number(e.target.value) || 1))}
              className="w-32"
            />
          </div>
          <div className="flex flex-wrap gap-2 pb-1">
            {HORIZONS.map((h) => (
              <Button
                key={h.value}
                type="button"
                size="sm"
                variant={withinDays === h.value ? 'default' : 'outline'}
                onClick={() => setWithinDays(h.value)}
              >
                {h.label}
              </Button>
            ))}
          </div>
          <p className="pb-2 text-sm text-muted-foreground">
            Days ahead for both lists below. Overdue people are always included.
          </p>
        </CardContent>
      </Card>

      <Tabs defaultValue="reminders">
        <TabsList>
          <TabsTrigger value="reminders">
            Chase queue{preview?.length ? ` (${preview.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="retirements">
            Retirements{retirements?.length ? ` (${retirements.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="contracts">
            Contract expiries{expiries?.length ? ` (${expiries.length})` : ''}
          </TabsTrigger>
        </TabsList>

        {/* ── Chase queue ─────────────────────────────────────────────────── */}
        <TabsContent value="reminders" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">What would fire</CardTitle>
              <CardDescription>
                Nothing is sent by opening this. A row marked already raised has been chased before
                and the sweep will suppress it as a duplicate rather than send it twice.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {previewing ? (
                <div className="flex justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !preview || preview.length === 0 ? (
                <EmptyState
                  icon={CalendarSearch}
                  title="Nothing due"
                  description="No separation needs chasing today."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>What</TableHead>
                      <TableHead>Who</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead className="text-right">Days</TableHead>
                      <TableHead>State</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {preview.map((p, i) => (
                      <TableRow key={`${p.kind}-${p.employeeId}-${p.separationId ?? i}`}>
                        <TableCell>
                          <Badge className={tierTone(p.escalationTier)} variant="secondary">
                            {KIND_LABEL[p.kind] ?? p.kind}
                          </Badge>
                          <p className="mt-1 text-xs text-muted-foreground">{p.message}</p>
                        </TableCell>
                        <TableCell>{personCell(p.employeeName, p.employeeNumber)}</TableCell>
                        <TableCell>
                          {/* ⚠ separationId is genuinely nullable here — a contract expiring for
                              somebody with no separation raised yet has nothing to link to. */}
                          {p.separationId ? (
                            <Link
                              href={`/hr/separations/${p.separationId}`}
                              className="text-primary hover:underline"
                            >
                              {p.reference ?? 'View'}
                            </Link>
                          ) : (
                            <span className="text-muted-foreground">{p.reference ?? '—'}</span>
                          )}
                        </TableCell>
                        <TableCell>{fmtDate(p.dueDate)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {p.daysRemaining}
                        </TableCell>
                        <TableCell>
                          {p.alreadyRaised ? (
                            <Badge variant="secondary">Already raised</Badge>
                          ) : (
                            <Badge>New</Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Retirements ─────────────────────────────────────────────────── */}
        <TabsContent value="retirements" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Retiring within {withinDays} days</CardTitle>
              <CardDescription>
                The sweep raises a separation for everyone here who does not already have one —
                {' '}
                <strong>{newRetirements.length}</strong> of {retirements?.length ?? 0} at this
                horizon.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <Button
                onClick={() => setConfirming('retirement')}
                disabled={sweepBusy || newRetirements.length === 0}
              >
                {retirementSweep.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <UserRoundX className="mr-2 h-4 w-4" />
                )}
                Raise {newRetirements.length} retirement
                {newRetirements.length === 1 ? '' : 's'}
              </Button>

              {loadingRetirements ? (
                <div className="flex justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !retirements || retirements.length === 0 ? (
                <EmptyState
                  icon={AlarmClock}
                  title="Nobody retiring in this window"
                  description="Widen the horizon above — at 90 days this is usually empty."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Age</TableHead>
                      <TableHead>Retires</TableHead>
                      <TableHead className="text-right">Days</TableHead>
                      <TableHead>Basis</TableHead>
                      <TableHead>Separation</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {retirements.map((r) => (
                      <TableRow key={r.employeeId}>
                        <TableCell>
                          {personCell(
                            r.employeeName,
                            r.employeeNumber,
                            r.positionTitle,
                            r.organizationUnitName,
                          )}
                        </TableCell>
                        <TableCell className="tabular-nums">
                          {r.currentAge ?? '—'}
                          <span className="text-xs text-muted-foreground"> / {r.retirementAge}</span>
                        </TableCell>
                        <TableCell>{fmtDate(r.retirementDate)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {r.isOverdue ? (
                            <Badge variant="destructive">{Math.abs(r.daysUntilRetirement)} over</Badge>
                          ) : (
                            r.daysUntilRetirement
                          )}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {/* An explicit date on the record beats one derived from the birthday. */}
                          {r.isExplicitDate ? 'Recorded date' : 'From date of birth'}
                        </TableCell>
                        <TableCell>{existingCell(r)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Contract expiries ───────────────────────────────────────────── */}
        <TabsContent value="contracts" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Contracts ending within {withinDays} days</CardTitle>
              <CardDescription>
                The sweep raises a separation for everyone here who does not already have one —
                {' '}
                <strong>{newExpiries.length}</strong> of {expiries?.length ?? 0} at this horizon.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <Button
                onClick={() => setConfirming('contract')}
                disabled={sweepBusy || newExpiries.length === 0}
              >
                {contractSweep.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <FileWarning className="mr-2 h-4 w-4" />
                )}
                Raise {newExpiries.length} separation{newExpiries.length === 1 ? '' : 's'}
              </Button>

              {loadingExpiries ? (
                <div className="flex justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !expiries || expiries.length === 0 ? (
                <EmptyState
                  icon={CalendarSearch}
                  title="No contracts ending in this window"
                  description="Widen the horizon above, or the contracts on file carry no end date."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Contract</TableHead>
                      <TableHead>Ends</TableHead>
                      <TableHead className="text-right">Days</TableHead>
                      <TableHead>Separation</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {expiries.map((r) => (
                      <TableRow key={r.contractId}>
                        <TableCell>
                          {personCell(
                            r.employeeName,
                            r.employeeNumber,
                            r.positionTitle,
                            r.organizationUnitName,
                          )}
                        </TableCell>
                        <TableCell>
                          <p>{r.contractNumber ?? '—'}</p>
                          <p className="text-xs text-muted-foreground">
                            from {fmtDate(r.contractStartDate)}
                          </p>
                        </TableCell>
                        <TableCell>{fmtDate(r.contractEndDate)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {r.isOverdue ? (
                            <Badge variant="destructive">{Math.abs(r.daysUntilExpiry)} over</Badge>
                          ) : (
                            r.daysUntilExpiry
                          )}
                        </TableCell>
                        <TableCell>{existingCell(r)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* ⚠ A sweep writes separation records against named people. It is confirmed, and the
          confirmation states the horizon — a wide one raises people who are years away. */}
      <AlertDialog open={confirming !== null} onOpenChange={(o) => !o && setConfirming(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <TriangleAlert className="h-5 w-5 text-amber-600" />
              Raise {pendingCount} {pendingKind} separation{pendingCount === 1 ? '' : 's'}?
            </AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-2">
                <p>
                  This creates a separation record for each person in the list who does not already
                  have one, using a horizon of <strong>{withinDays} days</strong>. Anyone who
                  already has one is skipped.
                </p>
                <p>
                  It is not a preview. Widen the horizon and you raise people who are years from
                  leaving.
                </p>
                <p className="font-medium text-destructive">
                  There is no way to remove a separation from the application once it is raised —
                  the API has no delete. Check the list before running this.
                </p>
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirming === 'retirement') retirementSweep.mutate();
                else contractSweep.mutate();
                setConfirming(null);
              }}
            >
              Run the sweep
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
