'use client';

import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ArrowRightLeft, Flame, Loader2, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { leaveYearEndService } from '@/services/hr/leave.service';
import type { LeaveYearEndResult } from '@/types/hr/leave-request';

function ResultPanel({ title, result }: { title: string; result: LeaveYearEndResult }) {
  return (
    <div
      className={
        result.isDryRun
          ? 'space-y-3 rounded-md border border-amber-300/60 bg-amber-50 p-4 dark:border-amber-900/60 dark:bg-amber-950/40'
          : 'space-y-3 rounded-md border p-4'
      }
    >
      <p className="text-sm font-medium">
        {title}
        {result.isDryRun && (
          <span className="ml-2 font-semibold uppercase tracking-wide">
            &mdash; preview only, nothing was written
          </span>
        )}
      </p>
      <div className="flex flex-wrap gap-2">
        {/*
          ⚠ "Examined", not "processed". The old label counted every balance the loop LOOKED at,
          including the ones it skipped, so a run that examined 900 and changed 12 reported "900
          processed" (finding L-26). The skipped count sits beside it so the two cannot be read as
          the same number.
        */}
        <Badge variant="secondary">{result.balancesProcessed} examined</Badge>
        <Badge variant="secondary">{result.balancesAffected} changed</Badge>
        <Badge variant="outline">{result.balancesSkipped} left alone</Badge>
        {result.totalDaysCarriedOver > 0 && (
          <Badge variant="outline">{result.totalDaysCarriedOver} days carried over</Badge>
        )}
        {result.totalDaysExpired > 0 && (
          <Badge variant="outline">{result.totalDaysExpired} carried days expired</Badge>
        )}
        {result.totalDaysForfeited > 0 && (
          <Badge variant="outline">{result.totalDaysForfeited} days forfeited</Badge>
        )}
      </div>
      {result.notes?.length > 0 && (
        <ul className="max-h-48 space-y-1 overflow-y-auto text-xs text-muted-foreground">
          {result.notes.map((n, i) => (
            <li key={i}>• {n}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * Year-end batches. Both runs write to every matching balance, so each is behind a
 * confirmation and can be scoped to a single employee first as a dry run.
 */
export default function LeaveYearEndPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // The year being closed is the LEAVE year before the one we are in (round 5, C4) — a choice the
  // user types wins.
  const { currentYear } = useLeaveYear();

  const [chosenCarryFromYear, setCarryFromYear] = useState<string | null>(null);
  const carryFromYear = chosenCarryFromYear ?? String(currentYear - 1);
  const [carryEmployeeId, setCarryEmployeeId] = useState<string | null>(null);
  const [chosenForfeitYear, setForfeitYear] = useState<string | null>(null);
  const forfeitYear = chosenForfeitYear ?? String(currentYear - 1);
  const [forfeitAsOf, setForfeitAsOf] = useState('');
  const [forfeitEmployeeId, setForfeitEmployeeId] = useState<string | null>(null);

  const [pending, setPending] = useState<null | 'carry' | 'forfeit'>(null);
  // Which job a preview is for. Separate from `pending`, which drives the confirm dialog — a
  // preview needs no confirming, because it changes nothing.
  const [previewing, setPreviewing] = useState<null | 'carry' | 'forfeit'>(null);
  const [busy, setBusy] = useState(false);
  const [carryResult, setCarryResult] = useState<LeaveYearEndResult | null>(null);
  const [forfeitResult, setForfeitResult] = useState<LeaveYearEndResult | null>(null);

  /**
   * ⚠ `dryRun` is why this screen exists in its current shape (finding L-24).
   *
   * Both jobs move people's balances in bulk and neither has an undo. A preview is the difference
   * between catching a misconfigured leave type before the run and catching it in nine hundred
   * balances afterwards. It goes through the SAME path with the same guards — a preview that took
   * a shortcut would not be a preview of anything.
   */
  const run = async (dryRun = false) => {
    setBusy(true);
    try {
      if ((dryRun ? previewing : pending) === 'carry') {
        const res = await leaveYearEndService.processCarryOver(
          Number(carryFromYear),
          carryEmployeeId ?? undefined,
          dryRun,
        );
        setCarryResult(res);
      } else {
        const res = await leaveYearEndService.processForfeiture(
          Number(forfeitYear),
          forfeitAsOf || undefined,
          forfeitEmployeeId ?? undefined,
          dryRun,
        );
        setForfeitResult(res);
      }
      // Nothing moved on a preview, so nothing needs refetching.
      if (!dryRun) await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({
        title: dryRun ? 'Preview only' : 'Completed',
        description: dryRun
          ? 'Nothing was written. Read the figures, then run it for real.'
          : 'Year-end run finished.',
      });
      setPending(null);
      setPreviewing(null);
      return true;
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'The run failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Year-End"
        description="Carry unused entitlement into the new year, then forfeit what expires."
      />

      <div className="flex items-start gap-2 rounded-md border border-amber-500/40 bg-amber-500/5 p-3 text-sm">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
        <p className="text-muted-foreground">
          Both runs update leave balances in bulk and are not automatically reversible. Scope a run
          to one employee first to confirm the numbers before running it for everyone.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ArrowRightLeft className="h-4 w-4" /> Carry-over
            </CardTitle>
            <CardDescription>
              Moves unused days from the closing year into the next, capped by each leave
              type&apos;s carry-over limit. It runs only once that year has ended; a preview runs any
              time.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="carryFromYear">From year</Label>
              <Input
                id="carryFromYear"
                type="number"
                value={carryFromYear}
                onChange={(e) => setCarryFromYear(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Employee (optional)</Label>
              <EmployeePicker value={carryEmployeeId} onChange={setCarryEmployeeId} />
              <p className="text-xs text-muted-foreground">
                Leave blank to run for every employee.
              </p>
            </div>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => {
                setPreviewing('carry');
                void run(true);
              }}
            >
              Preview
            </Button>
            <Button onClick={() => setPending('carry')} disabled={busy}>
              {busy && pending === 'carry' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Run carry-over
            </Button>
            {carryResult && <ResultPanel title="Last carry-over run" result={carryResult} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Flame className="h-4 w-4" /> Forfeiture
            </CardTitle>
            <CardDescription>
              Expires the carried-over days not taken before their window closed — days taken in
              time are kept — and, for a leave type with a cut-off, forfeits what is still unused
              after it.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="forfeitYear">Year</Label>
              <Input
                id="forfeitYear"
                type="number"
                value={forfeitYear}
                onChange={(e) => setForfeitYear(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="forfeitAsOf">As of date (optional)</Label>
              <Input
                id="forfeitAsOf"
                type="date"
                value={forfeitAsOf}
                onChange={(e) => setForfeitAsOf(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">Defaults to today.</p>
            </div>
            <div className="space-y-2">
              <Label>Employee (optional)</Label>
              <EmployeePicker value={forfeitEmployeeId} onChange={setForfeitEmployeeId} />
            </div>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => {
                setPreviewing('forfeit');
                void run(true);
              }}
            >
              Preview
            </Button>
            <Button variant="destructive" onClick={() => setPending('forfeit')} disabled={busy}>
              {busy && pending === 'forfeit' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Run forfeiture
            </Button>
            {forfeitResult && <ResultPanel title="Last forfeiture run" result={forfeitResult} />}
          </CardContent>
        </Card>
      </div>

      <ConfirmationDialog
        open={pending !== null}
        onOpenChange={(open) => !open && setPending(null)}
        title={pending === 'carry' ? 'Run carry-over?' : 'Run forfeiture?'}
        description={
          pending === 'carry'
            ? `Unused days from ${carryFromYear} will be carried into ${Number(carryFromYear) + 1}${
                carryEmployeeId ? ' for the selected employee' : ' for every employee'
              }.`
            : `Carried-over days not taken before their window closed, and unused days past a cut-off, will be removed from ${forfeitYear}${
                forfeitEmployeeId ? ' for the selected employee' : ' for every employee'
              }. This cannot be undone automatically.`
        }
        confirmText={pending === 'carry' ? 'Run carry-over' : 'Run forfeiture'}
        variant={pending === 'forfeit' ? 'destructive' : 'default'}
        isLoading={busy}
        // ⚠ Explicitly false, not bare `run`. ConfirmationDialog calls onConfirm() with no
        // arguments today, so this is equivalent — but if that ever changed, the first argument
        // would land on `dryRun` and a real year-end run would silently become a preview, with
        // the operator believing it had run.
        onConfirm={() => run(false)}
      />
    </div>
  );
}
