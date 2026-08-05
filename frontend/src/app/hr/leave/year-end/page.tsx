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
import { leaveYearEndService } from '@/services/hr/leave.service';
import type { LeaveYearEndResult } from '@/types/hr/leave-request';

const currentYear = new Date().getFullYear();

function ResultPanel({ title, result }: { title: string; result: LeaveYearEndResult }) {
  return (
    <div className="space-y-3 rounded-md border p-4">
      <p className="text-sm font-medium">{title}</p>
      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{result.balancesProcessed} processed</Badge>
        <Badge variant="secondary">{result.balancesAffected} affected</Badge>
        {result.totalDaysCarriedOver > 0 && (
          <Badge variant="outline">{result.totalDaysCarriedOver} days carried over</Badge>
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

  const [carryFromYear, setCarryFromYear] = useState(String(currentYear - 1));
  const [carryEmployeeId, setCarryEmployeeId] = useState<string | null>(null);
  const [forfeitYear, setForfeitYear] = useState(String(currentYear - 1));
  const [forfeitAsOf, setForfeitAsOf] = useState('');
  const [forfeitEmployeeId, setForfeitEmployeeId] = useState<string | null>(null);

  const [pending, setPending] = useState<null | 'carry' | 'forfeit'>(null);
  const [busy, setBusy] = useState(false);
  const [carryResult, setCarryResult] = useState<LeaveYearEndResult | null>(null);
  const [forfeitResult, setForfeitResult] = useState<LeaveYearEndResult | null>(null);

  const run = async () => {
    setBusy(true);
    try {
      if (pending === 'carry') {
        const res = await leaveYearEndService.processCarryOver(
          Number(carryFromYear),
          carryEmployeeId ?? undefined,
        );
        setCarryResult(res);
      } else {
        const res = await leaveYearEndService.processForfeiture(
          Number(forfeitYear),
          forfeitAsOf || undefined,
          forfeitEmployeeId ?? undefined,
        );
        setForfeitResult(res);
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({ title: 'Completed', description: 'Year-end run finished.' });
      setPending(null);
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
              type&apos;s carry-over limit.
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
              Removes carried-over or unused days that have passed their expiry window.
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
            : `Expired days in ${forfeitYear} will be removed${
                forfeitEmployeeId ? ' for the selected employee' : ' for every employee'
              }. This cannot be undone automatically.`
        }
        confirmText={pending === 'carry' ? 'Run carry-over' : 'Run forfeiture'}
        variant={pending === 'forfeit' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={run}
      />
    </div>
  );
}
