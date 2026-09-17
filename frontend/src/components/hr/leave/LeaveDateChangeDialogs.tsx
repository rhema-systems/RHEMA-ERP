'use client';

/**
 * The three date-change conversations a leave request can now have, plus the two panels that show
 * what happened. Shared by the desk (`/hr/leave/requests/[id]`) and the portal (`/me/leave/[id]`)
 * so the same act reads the same way on both sides.
 *
 * Closure plan wave C:
 *  · C2 — an approver sends a request back with dates of their own, and the employee accepts them
 *    or counters. Leave PLANS have had this since the port; the request, which is the record that
 *    actually books the days, could only be approved or rejected outright (R-3).
 *  · C4 — an approved request moves to different dates, keeping its number, its history and its
 *    reason. The alternative was cancel-and-re-key, which loses all three (R-8).
 *
 * ⚠ Rescheduling RE-OPENS the approval (decision D-5), and the dialog says so before you confirm.
 * An approval is an approval of dates; carrying it silently across to different ones would let the
 * record claim an authority nobody gave.
 */

import { useState } from 'react';
import { CalendarClock, CornerUpLeft, Info } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type { LeaveRequest } from '@/types/hr/leave-request';

const day = (v?: string | null) => v?.slice(0, 10) ?? '';

/** Inclusive calendar-day span, for the "that is N days" hint. The server counts chargeable days. */
function spanDays(start: string, end: string): number | null {
  if (!start || !end || end < start) return null;
  return Math.round((new Date(end).getTime() - new Date(start).getTime()) / 86_400_000) + 1;
}

function DateRangeFields({
  start,
  end,
  onStart,
  onEnd,
}: {
  start: string;
  end: string;
  onStart: (v: string) => void;
  onEnd: (v: string) => void;
}) {
  const span = spanDays(start, end);
  return (
    <>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-2">
          <Label htmlFor="range-start">Start date</Label>
          <Input id="range-start" type="date" value={start} onChange={(e) => onStart(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="range-end">End date</Label>
          <Input id="range-end" type="date" value={end} onChange={(e) => onEnd(e.target.value)} />
        </div>
      </div>
      {end && start && end < start && (
        <p className="text-sm text-red-500">The end date cannot be before the start date.</p>
      )}
      {span !== null && (
        <p className="text-xs text-muted-foreground">
          {span} calendar day{span === 1 ? '' : 's'}. The chargeable total is worked out on save —
          weekends and public holidays depend on the leave type.
        </p>
      )}
    </>
  );
}

// ── Approver: send it back with different dates ────────────────────────────────────────────────

export function SuggestDatesDialog({
  request,
  open,
  onOpenChange,
  busy,
  onConfirm,
}: {
  request: LeaveRequest;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  busy: boolean;
  onConfirm: (values: { suggestedStartDate: string; suggestedEndDate: string; notes: string }) => void;
}) {
  const [start, setStart] = useState(day(request.startDate));
  const [end, setEnd] = useState(day(request.endDate));
  const [notes, setNotes] = useState('');

  const invalid = !start || !end || end < start;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        // Reopening starts from the request's own dates again, not from the last edit.
        if (next) {
          setStart(day(request.startDate));
          setEnd(day(request.endDate));
          setNotes('');
        }
        onOpenChange(next);
      }}
    >
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>Send back with different dates</DialogTitle>
          <DialogDescription>
            {request.employeeName} asked for {day(request.startDate)} to {day(request.endDate)}.
            Propose dates that work and the request goes back to them to accept or counter — it is
            not rejected, and it keeps its number.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <DateRangeFields start={start} end={end} onStart={setStart} onEnd={setEnd} />
          <div className="space-y-2">
            <Label htmlFor="suggest-notes">Why these dates</Label>
            <Textarea
              id="suggest-notes"
              rows={3}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="e.g. the year-end valuation runs that week — the following week is clear."
            />
            <p className="text-xs text-muted-foreground">
              Optional, but the employee sees only the dates without it.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button
            disabled={invalid || busy}
            onClick={() =>
              onConfirm({ suggestedStartDate: start, suggestedEndDate: end, notes: notes.trim() })
            }
          >
            <CornerUpLeft className="mr-2 h-4 w-4" /> Send back
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Employee: accept the suggestion, or counter it ─────────────────────────────────────────────

export function RespondToSuggestionDialog({
  request,
  open,
  onOpenChange,
  busy,
  onConfirm,
}: {
  request: LeaveRequest;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  busy: boolean;
  onConfirm: (values: {
    accept: boolean;
    startDate: string | null;
    endDate: string | null;
    notes: string | null;
  }) => void;
}) {
  const suggestedStart = day(request.suggestedStartDate);
  const suggestedEnd = day(request.suggestedEndDate);

  const [mode, setMode] = useState<'accept' | 'counter'>('accept');
  const [start, setStart] = useState(suggestedStart);
  const [end, setEnd] = useState(suggestedEnd);
  const [notes, setNotes] = useState('');

  const counterInvalid = mode === 'counter' && (!start || !end || end < start);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next) {
          setMode('accept');
          setStart(suggestedStart);
          setEnd(suggestedEnd);
          setNotes('');
        }
        onOpenChange(next);
      }}
    >
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>Answer the suggested dates</DialogTitle>
          <DialogDescription>
            You asked for {day(request.startDate)} to {day(request.endDate)}. The approver suggested{' '}
            <span className="font-medium text-foreground">
              {suggestedStart} to {suggestedEnd}
            </span>
            . Either way this goes back for approval on the dates you settle on.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {request.managerSuggestionNotes && (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              <p className="mb-1 text-xs text-muted-foreground">What they said</p>
              <p className="whitespace-pre-wrap">{request.managerSuggestionNotes}</p>
            </div>
          )}

          <div className="flex gap-2">
            <Button
              type="button"
              variant={mode === 'accept' ? 'default' : 'outline'}
              size="sm"
              onClick={() => setMode('accept')}
            >
              Accept their dates
            </Button>
            <Button
              type="button"
              variant={mode === 'counter' ? 'default' : 'outline'}
              size="sm"
              onClick={() => setMode('counter')}
            >
              Propose different ones
            </Button>
          </div>

          {mode === 'counter' ? (
            <DateRangeFields start={start} end={end} onStart={setStart} onEnd={setEnd} />
          ) : (
            <p className="text-sm text-muted-foreground">
              Your request moves to {suggestedStart} – {suggestedEnd} and returns for approval.
            </p>
          )}

          <div className="space-y-2">
            <Label htmlFor="respond-notes">Anything to add</Label>
            <Textarea
              id="respond-notes"
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Optional."
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button
            disabled={counterInvalid || busy}
            onClick={() =>
              onConfirm({
                accept: mode === 'accept',
                startDate: mode === 'counter' ? start : null,
                endDate: mode === 'counter' ? end : null,
                notes: notes.trim() || null,
              })
            }
          >
            {mode === 'accept' ? 'Accept and resubmit' : 'Send my dates'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Move an approved request ───────────────────────────────────────────────────────────────────

export function RescheduleDialog({
  request,
  open,
  onOpenChange,
  busy,
  onConfirm,
}: {
  request: LeaveRequest;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  busy: boolean;
  onConfirm: (values: { startDate: string; endDate: string; reason: string }) => void;
}) {
  const [start, setStart] = useState(day(request.startDate));
  const [end, setEnd] = useState(day(request.endDate));
  const [reason, setReason] = useState('');

  const unchanged = start === day(request.startDate) && end === day(request.endDate);
  const invalid = !start || !end || end < start || !reason.trim() || unchanged;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next) {
          setStart(day(request.startDate));
          setEnd(day(request.endDate));
          setReason('');
        }
        onOpenChange(next);
      }}
    >
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>Move this leave</DialogTitle>
          <DialogDescription>
            {request.requestNumber} is approved for {day(request.startDate)} to {day(request.endDate)}.
            Moving it keeps the request, its number and its history — you do not have to cancel and
            key it again.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <DateRangeFields start={start} end={end} onStart={setStart} onEnd={setEnd} />

          <div className="space-y-2">
            <Label htmlFor="reschedule-reason">
              Why it is moving <span className="text-red-500">*</span>
            </Label>
            <Textarea
              id="reschedule-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. the client audit was brought forward a week."
            />
            <p className="text-xs text-muted-foreground">
              Required. A move that does not say why is what this exists to prevent.
            </p>
          </div>

          {/* Said before you confirm, not discovered afterwards. */}
          <div className="flex gap-2 rounded-md border border-amber-300/60 bg-amber-50 p-3 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
            <Info className="mt-0.5 h-4 w-4 shrink-0 text-amber-700 dark:text-amber-300" />
            <p>
              This <span className="font-medium">re-opens the approval</span>. The leave is approved
              for its current dates, not for any dates — so it goes back through approval on the new
              ones.
              {unchanged && start && end && (
                <span className="mt-1 block text-muted-foreground">
                  Those are the dates it already has — change one to continue.
                </span>
              )}
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button
            disabled={invalid || busy}
            onClick={() => onConfirm({ startDate: start, endDate: end, reason: reason.trim() })}
          >
            <CalendarClock className="mr-2 h-4 w-4" /> Move and resubmit
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── The two panels that show what happened ─────────────────────────────────────────────────────

/** Shown while a request is sitting at ChangesSuggested, on both sides of the conversation. */
export function SuggestedDatesPanel({ request }: { request: LeaveRequest }) {
  if (request.status !== 'ChangesSuggested' || !request.suggestedStartDate) return null;

  return (
    <div className="rounded-md border border-blue-300/60 bg-blue-50 p-4 text-sm dark:border-blue-900/60 dark:bg-blue-950/40">
      <p className="font-medium">Sent back with different dates</p>
      <p className="mt-1">
        Asked for {day(request.startDate)} – {day(request.endDate)}; suggested{' '}
        <span className="font-medium">
          {day(request.suggestedStartDate)} – {day(request.suggestedEndDate)}
        </span>
        .
      </p>
      {request.managerSuggestionNotes && (
        <p className="mt-2 whitespace-pre-wrap text-muted-foreground">
          {request.managerSuggestionNotes}
        </p>
      )}
    </div>
  );
}

/** Shown once a request has been moved, so the record says what it used to be. */
export function RescheduleTrailPanel({ request }: { request: LeaveRequest }) {
  if (!request.rescheduleCount) return null;

  return (
    <div className="rounded-md border bg-muted/40 p-4 text-sm">
      <p className="font-medium">
        Moved {request.rescheduleCount === 1 ? 'once' : `${request.rescheduleCount} times`}
      </p>
      <p className="mt-1">
        Originally approved for {day(request.originalStartDate)} – {day(request.originalEndDate)};
        now {day(request.startDate)} – {day(request.endDate)}.
      </p>
      {request.rescheduleReason && (
        <p className="mt-2 whitespace-pre-wrap text-muted-foreground">{request.rescheduleReason}</p>
      )}
      <p className="mt-2 text-xs text-muted-foreground">
        Last moved {day(request.rescheduledDate)}
        {request.rescheduledByName ? ` by ${request.rescheduledByName}` : ''}.
      </p>
    </div>
  );
}
