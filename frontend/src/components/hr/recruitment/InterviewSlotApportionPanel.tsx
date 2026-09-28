'use client';

/**
 * Spreads a day's candidates across the interview window — round 4, lane C.
 *
 * ⚠ **Preview is a dry run and Apply is the only write.** A recruiter changes the interval three
 * times before they like the shape of the day, and each attempt must not rewrite nine candidates'
 * times — nor send anybody anything. The two buttons are deliberately not one.
 *
 * ⚠ **The feasibility line is the point of the screen, not decoration.** "Nine of eleven fit" has
 * to be readable before the invitations go out, because the alternative is discovering it when two
 * candidates are standing in reception.
 */

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CalendarClock, CheckCircle2, Loader2, Plus, X } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';
import { jobInterviewService } from '@/services/hr/interviews.service';
import type { InterviewBreak, InterviewSlotPlan, JobInterviewDetail } from '@/types/hr/interviews';

/** `<input type="time">` gives `HH:mm`; the API takes a TimeSpan. */
const toApiTime = (hhmm: string) => (hhmm.length === 5 ? `${hhmm}:00` : hhmm);
/** …and back, for display. */
const toClock = (t?: string | null) => (t ? t.slice(0, 5) : '');

export function InterviewSlotApportionPanel({
  interview,
  canManage,
}: {
  interview: JobInterviewDetail;
  canManage: boolean;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [slotMinutes, setSlotMinutes] = useState(30);
  const [bufferMinutes, setBufferMinutes] = useState(0);
  const [breaks, setBreaks] = useState<InterviewBreak[]>([]);
  const [plan, setPlan] = useState<InterviewSlotPlan | null>(null);

  const request = () => ({
    slotMinutes,
    bufferMinutes,
    breaks: breaks
      // A half-typed break row is ignored rather than sent — the server would refuse the whole
      // request over a row the user is still filling in.
      .filter((b) => b.start && b.end)
      .map((b) => ({ start: toApiTime(b.start), end: toApiTime(b.end), label: b.label || null })),
  });

  const preview = useMutation({
    mutationFn: () => jobInterviewService.previewSlots(interview.id, request()),
    onSuccess: setPlan,
    onError: (e: any) =>
      toast({ title: 'Could not work out a timetable', description: e?.message, variant: 'destructive' }),
  });

  const apply = useMutation({
    mutationFn: () => jobInterviewService.applySlots(interview.id, request()),
    onSuccess: async (result) => {
      setPlan(result);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'interview', interview.id] });
      toast({
        title: 'Timetable applied',
        description: result.allFit
          ? `${result.slots.length} candidates have a time.`
          : `${result.slots.length} placed; ${result.unplaced.length} still need a session.`,
      });
    },
    onError: (e: any) =>
      toast({ title: 'Could not apply the timetable', description: e?.message, variant: 'destructive' }),
  });

  const candidateCount = interview.interviewees?.length ?? 0;
  const busy = preview.isPending || apply.isPending;

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <CalendarClock className="h-4 w-4" />
          Apportion slots
        </CardTitle>
        <CardDescription>
          Spread {candidateCount} candidate{candidateCount === 1 ? '' : 's'} across{' '}
          {toClock(interview.startTime)}–{toClock(interview.endTime)}. Preview first — nothing is
          written until you apply.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="slotMinutes">Minutes per candidate</Label>
            <Input
              id="slotMinutes"
              type="number"
              min={5}
              max={480}
              value={slotMinutes}
              disabled={!canManage}
              onChange={(e) => setSlotMinutes(Number(e.target.value) || 0)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="bufferMinutes">Gap between candidates</Label>
            <Input
              id="bufferMinutes"
              type="number"
              min={0}
              max={120}
              value={bufferMinutes}
              disabled={!canManage}
              onChange={(e) => setBufferMinutes(Number(e.target.value) || 0)}
            />
            <p className="text-xs text-muted-foreground">
              Turnaround for notes and fetching the next person. Never added after the last slot.
            </p>
          </div>
        </div>

        {/* ── breaks ─────────────────────────────────────────────────────── */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label>Breaks</Label>
            {canManage && (
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => setBreaks((b) => [...b, { start: '', end: '', label: '' }])}
              >
                <Plus className="mr-1.5 h-3.5 w-3.5" />
                Add a break
              </Button>
            )}
          </div>

          {breaks.length === 0 ? (
            <p className="text-xs text-muted-foreground">
              No breaks. Candidates will be placed back to back across the whole window.
            </p>
          ) : (
            breaks.map((b, i) => (
              <div key={i} className="flex flex-wrap items-end gap-2">
                <div className="space-y-1">
                  <Label className="text-xs" htmlFor={`breakStart${i}`}>From</Label>
                  <Input
                    id={`breakStart${i}`}
                    type="time"
                    className="w-32"
                    value={toClock(b.start)}
                    disabled={!canManage}
                    onChange={(e) =>
                      setBreaks((rows) => rows.map((r, j) => (j === i ? { ...r, start: e.target.value } : r)))
                    }
                  />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs" htmlFor={`breakEnd${i}`}>To</Label>
                  <Input
                    id={`breakEnd${i}`}
                    type="time"
                    className="w-32"
                    value={toClock(b.end)}
                    disabled={!canManage}
                    onChange={(e) =>
                      setBreaks((rows) => rows.map((r, j) => (j === i ? { ...r, end: e.target.value } : r)))
                    }
                  />
                </div>
                <div className="space-y-1 flex-1 min-w-[10rem]">
                  <Label className="text-xs" htmlFor={`breakLabel${i}`}>What for</Label>
                  <Input
                    id={`breakLabel${i}`}
                    placeholder="Lunch"
                    value={b.label ?? ''}
                    disabled={!canManage}
                    onChange={(e) =>
                      setBreaks((rows) => rows.map((r, j) => (j === i ? { ...r, label: e.target.value } : r)))
                    }
                  />
                </div>
                {canManage && (
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    aria-label="Remove this break"
                    onClick={() => setBreaks((rows) => rows.filter((_, j) => j !== i))}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                )}
              </div>
            ))
          )}
        </div>

        {canManage && (
          <div className="flex gap-2">
            <Button variant="outline" disabled={busy || candidateCount === 0} onClick={() => preview.mutate()}>
              {preview.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Preview
            </Button>
            <Button disabled={busy || !plan} onClick={() => apply.mutate()}>
              {apply.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Apply to {plan?.slots.length ?? 0} candidate{plan?.slots.length === 1 ? '' : 's'}
            </Button>
          </div>
        )}

        {candidateCount === 0 && (
          <p className="text-xs text-muted-foreground">
            Book candidates into this interview first — there is nothing to lay out yet.
          </p>
        )}

        {/* ── the verdict, then the timetable ────────────────────────────── */}
        {plan && (
          <div className="space-y-3">
            <Alert variant={plan.allFit ? 'default' : 'destructive'}>
              {plan.allFit ? (
                <CheckCircle2 className="h-4 w-4" />
              ) : (
                <AlertTriangle className="h-4 w-4" />
              )}
              <AlertDescription>
                {plan.summary}
                {!plan.allFit && plan.firstFreeAfterWindow && (
                  <>
                    {' '}
                    The window is free again from{' '}
                    <strong>{toClock(plan.firstFreeAfterWindow)}</strong> — pick a date for the rest.
                  </>
                )}
              </AlertDescription>
            </Alert>

            {plan.slots.length > 0 && (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10">#</TableHead>
                    <TableHead>Candidate</TableHead>
                    <TableHead className="w-40">Time</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {plan.slots.map((s) => (
                    <TableRow key={s.intervieweeId}>
                      <TableCell className="text-muted-foreground">{s.ordinal}</TableCell>
                      <TableCell>
                        <div>{s.candidateName}</div>
                        <div className="text-xs text-muted-foreground">{s.applicationNumber}</div>
                      </TableCell>
                      <TableCell className="font-medium">
                        {toClock(s.slotStartTime)} – {toClock(s.slotEndTime)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}

            {plan.unplaced.length > 0 && (
              <div className="space-y-1.5 rounded-md border border-destructive/30 p-3">
                <div className="text-sm font-medium">
                  Will not fit in this window ({plan.unplaced.length})
                </div>
                {/* Named, not counted. The recruiter decides who moves — being told only that
                    somebody must is not something anyone can act on. */}
                <div className="flex flex-wrap gap-1.5">
                  {plan.unplaced.map((u) => (
                    <Badge key={u.intervieweeId} variant="outline">
                      {u.candidateName}
                    </Badge>
                  ))}
                </div>
                <p className="text-xs text-muted-foreground">
                  Applying will clear any time these candidates already had, so nobody is left
                  holding an appointment the timetable no longer keeps.
                </p>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
