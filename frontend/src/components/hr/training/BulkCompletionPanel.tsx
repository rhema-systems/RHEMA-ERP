'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/components/ui/use-toast';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { trainingCompletionService } from '@/services/hr/training-completion.service';
import type { BulkCompletionResult } from '@/types/hr/training-delivery';

/**
 * Record completion for a whole run at once.
 *
 * ⚠ **TDC asked for this and it was built and never wired** — "bulk completion has no UI and no
 * client method". Recording a twenty-person course one nomination at a time is the kind of thing
 * that quietly does not get done.
 *
 * ⚠ **The server trusts its input.** `BulkRecordCompletionAsync` checks only for a completion that
 * already exists; it does not verify that the nomination belongs to this schedule, that the employee
 * matches it, or that the nomination was ever confirmed. So the screen is the constraint: the list
 * is built from this schedule's own nominations, and each row carries its own nomination id rather
 * than anything typed. Established by reading the service after the probe recorded a completion
 * against a DRAFT nomination without complaint.
 *
 * ⚠ **Skips are shown, never swallowed.** A bulk call that silently drops rows is worse than one
 * that refuses: the result names every skipped nomination and why.
 */
export function BulkCompletionPanel({
  scheduleId,
  readOnly = false,
}: {
  scheduleId: string;
  readOnly?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [completionDate, setCompletionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [status, setStatus] = useState('Completed');
  const [result, setResult] = useState<BulkCompletionResult | null>(null);

  const { data: nominees, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'schedules', scheduleId, 'nominees'],
    queryFn: () => trainingNominationService.getBySchedule(scheduleId),
  });

  const { data: existing } = useQuery({
    queryKey: ['hr', 'training', 'schedules', scheduleId, 'completions'],
    queryFn: () => trainingCompletionService.getBySchedule(scheduleId),
  });

  // Nominations that already have a completion are shown as done rather than offered again — the
  // server would skip them, and a checkbox that always fails is a trap.
  const done = useMemo(
    () => new Set((existing ?? []).map((c) => c.nominationId)),
    [existing],
  );

  const rows = (nominees ?? []).filter((n) => n.status !== 'Withdrawn' && n.status !== 'Rejected');
  const selectable = rows.filter((n) => !done.has(n.id));
  const chosen = selectable.filter((n) => selected[n.id]);

  const record = useMutation({
    mutationFn: () =>
      trainingCompletionService.bulkRecord({
        scheduleId,
        items: chosen.map((n) => ({
          nominationId: n.id,
          employeeId: n.employeeId,
          completionDate: new Date(completionDate).toISOString(),
          status,
          isPassed: status === 'Completed',
        })),
      }),
    onSuccess: async (r) => {
      setResult(r);
      setSelected({});
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'training', 'schedules', scheduleId, 'completions'],
      });
      toast({
        title: `${r.createdCount} of ${r.requestedCount} recorded`,
        description: r.skipped.length > 0 ? `${r.skipped.length} skipped — see below.` : undefined,
      });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Nothing was recorded',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Record completion</CardTitle>
        <CardDescription>
          Mark everyone who finished this run in one pass. Anyone already recorded is shown as done.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {result && (
          <Alert>
            <AlertTitle>
              {result.createdCount} of {result.requestedCount} recorded
            </AlertTitle>
            <AlertDescription>
              {result.skipped.length === 0 ? (
                'Every row was accepted.'
              ) : (
                <ul className="mt-1 space-y-1 text-sm">
                  {result.skipped.map((s) => {
                    const who = rows.find((n) => n.id === s.nominationId);
                    return (
                      <li key={s.nominationId}>
                        <span className="font-medium">{who?.employeeName ?? 'A nominee'}:</span>{' '}
                        {s.reason}
                      </li>
                    );
                  })}
                </ul>
              )}
            </AlertDescription>
          </Alert>
        )}

        <div className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-2">
            <Label htmlFor="bulkDate">Completed on</Label>
            <Input
              id="bulkDate"
              type="date"
              value={completionDate}
              onChange={(e) => setCompletionDate(e.target.value)}
              disabled={readOnly}
            />
          </div>
          <div className="space-y-2">
            <Label>Outcome</Label>
            <Select value={status} onValueChange={setStatus} disabled={readOnly}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Failed">Failed</SelectItem>
                <SelectItem value="Incomplete">Incomplete</SelectItem>
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Applies to everyone ticked. Record a different outcome in a second pass.
            </p>
          </div>
          <div className="flex items-end">
            <Button
              className="w-full"
              disabled={readOnly || chosen.length === 0 || record.isPending}
              onClick={() => record.mutate()}
            >
              {record.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record {chosen.length > 0 ? `${chosen.length} ` : ''}completion
              {chosen.length === 1 ? '' : 's'}
            </Button>
          </div>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            title="Nobody is booked on this run"
            description="Nominate people first; completion follows attendance."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">
                  <Checkbox
                    checked={chosen.length > 0 && chosen.length === selectable.length}
                    disabled={readOnly || selectable.length === 0}
                    onCheckedChange={(v) =>
                      setSelected(
                        v === true
                          ? Object.fromEntries(selectable.map((n) => [n.id, true]))
                          : {},
                      )
                    }
                    aria-label="Select everyone"
                  />
                </TableHead>
                <TableHead>Nominee</TableHead>
                <TableHead>Nomination</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((n) => {
                const already = done.has(n.id);
                return (
                  <TableRow key={n.id}>
                    <TableCell>
                      <Checkbox
                        checked={Boolean(selected[n.id])}
                        disabled={readOnly || already}
                        onCheckedChange={(v) =>
                          setSelected((s) => ({ ...s, [n.id]: v === true }))
                        }
                        aria-label={`Select ${n.employeeName}`}
                      />
                    </TableCell>
                    <TableCell className="font-medium">{n.employeeName}</TableCell>
                    <TableCell className="text-muted-foreground">{n.nominationNumber}</TableCell>
                    <TableCell>
                      {already ? (
                        <Badge variant="secondary">
                          <CheckCircle2 className="mr-1 h-3 w-3" />
                          Recorded
                        </Badge>
                      ) : (
                        <Badge variant="outline">{n.status}</Badge>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
