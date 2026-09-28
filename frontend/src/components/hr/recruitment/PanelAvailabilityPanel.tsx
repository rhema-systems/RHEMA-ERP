'use client';

import { useState } from 'react';
import {
  AlertTriangle,
  Building2,
  CalendarCheck,
  CalendarClock,
  CheckCircle2,
  DoorClosed,
  GraduationCap,
  Info,
  Loader2,
  Plane,
  Search,
  Users,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatTime } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import type {
  CommitmentKind,
  PanelistAvailabilityCheck,
  PanelistCommitment,
  PanelSlotSuggestion,
} from '@/types/hr/interviews';

const KIND_ICON: Record<CommitmentKind, typeof CalendarCheck> = {
  Interview: CalendarCheck,
  Leave: CalendarClock,
  Travel: Plane,
  Event: Users,
  RoomBooking: DoorClosed,
  Training: GraduationCap,
  Closure: Building2,
  Holiday: Building2,
};

/**
 * One commitment.
 *
 * ⚠ A day-granular commitment is never printed with times. Leave, travel, closures and all-day
 * events are recorded by the DAY, so the start and end the server sends are the day's bounds, not
 * a window — rendering "09:00–11:00" there would invent a precision the record does not have, and
 * that missing precision is exactly why those clashes are soft.
 */
function CommitmentRow({ c }: { c: PanelistCommitment }) {
  const Icon = KIND_ICON[c.kind] ?? CalendarCheck;
  return (
    <div className="flex items-start gap-2 text-sm">
      <Icon className="mt-0.5 h-3.5 w-3.5 shrink-0" />
      <span>
        {c.label}
        {c.isDayGranular ? (
          <span className="text-xs opacity-80">
            {' '}
            — {formatDate(c.start)}
            {c.start.slice(0, 10) !== c.end.slice(0, 10) && ` to ${formatDate(c.end)}`}
          </span>
        ) : (
          <span className="text-xs opacity-80">
            {' '}
            — {formatTime(c.start.slice(11, 19))}–{formatTime(c.end.slice(11, 19))}
          </span>
        )}
        {c.reference && <span className="ml-1 text-xs opacity-70">({c.reference})</span>}
        {c.hardness === 'Soft' && (
          <Badge variant="outline" className="ml-2 text-[10px]">
            warns only
          </Badge>
        )}
      </span>
    </div>
  );
}

/**
 * What the panel is already committed to during a proposed slot — and, when they are not free,
 * when they would be.
 *
 * ⚠ **Round 4, lane D: this stopped being advisory.** It used to check three things (other
 * interviews, leave, travel) and the server never refused a booking. It now fans out over every
 * registered commitment source — meetings the panelist is a participant of, room bookings, training
 * nominations, closures and public holidays as well — and a HARD clash REFUSES the save unless a
 * reason is supplied, which is recorded on the interview.
 *
 * ⚠ Hard and soft are shown differently on purpose. Soft clashes are day-granular or unconfirmed
 * evidence and never block: somebody on annual leave may well come in for an hour, and the system
 * should say so rather than decide for them.
 */
export function PanelAvailabilityPanel({
  panelistIds,
  externalPanelistIds,
  date,
  start,
  end,
  excludeInterviewId,
}: {
  panelistIds: string[];
  externalPanelistIds: string[];
  date?: string;
  start?: string;
  end?: string;
  excludeInterviewId?: string;
}) {
  const { toast } = useToast();
  const [result, setResult] = useState<PanelistAvailabilityCheck | null>(null);
  const [checking, setChecking] = useState(false);
  const [suggestions, setSuggestions] = useState<PanelSlotSuggestion[] | null>(null);
  const [suggesting, setSuggesting] = useState(false);

  const ready = !!date && !!start && !!end && (panelistIds.length > 0 || externalPanelistIds.length > 0);

  const run = async () => {
    if (!date || !start || !end) return;
    setChecking(true);
    setSuggestions(null);
    try {
      setResult(
        await jobInterviewService.checkAvailability({
          panelistIds,
          externalPanelistIds,
          date,
          start,
          end,
          excludeInterviewId,
        }),
      );
    } catch (error: any) {
      toast({
        title: 'Could not check availability',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setChecking(false);
    }
  };

  /** D4 — "then when?". Searches the fortnight from the proposed date at the same duration. */
  const suggest = async () => {
    if (!date || !start || !end) return;
    setSuggesting(true);
    try {
      const minutes = Math.max(
        15,
        Math.round(
          (new Date(`1970-01-01T${end}`).getTime() - new Date(`1970-01-01T${start}`).getTime()) / 60000,
        ),
      );
      const to = new Date(`${date}T00:00:00`);
      to.setDate(to.getDate() + 14);
      setSuggestions(
        await jobInterviewService.suggestSlots({
          panelistIds,
          externalPanelistIds,
          from: date,
          to: to.toISOString().slice(0, 10),
          durationMinutes: minutes,
          excludeInterviewId,
          maxSuggestions: 8,
        }),
      );
    } catch (error: any) {
      toast({
        title: 'Could not suggest slots',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setSuggesting(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium">Panel availability</p>
          <p className="text-sm text-muted-foreground">
            Checks the panel against their other interviews, meetings, room bookings, training, leave,
            travel and any closure that day. A confirmed clash blocks the save unless you give a reason.
          </p>
        </div>
        <Button type="button" variant="outline" size="sm" disabled={!ready || checking} onClick={run}>
          {checking ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Search className="mr-1.5 h-4 w-4" />}
          Check
        </Button>
      </div>

      {!ready && (
        <p className="text-sm text-muted-foreground">
          Choose a date, a time and at least one panel member to check.
        </p>
      )}

      {result && !result.hasConflicts && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>No clashes found</AlertTitle>
          <AlertDescription>
            Everyone on the panel is free for this slot.
            {result.sourcesConsulted?.length > 0 && (
              <span className="mt-1 block text-xs opacity-80">
                Checked against: {result.sourcesConsulted.join(', ')}.
              </span>
            )}
          </AlertDescription>
        </Alert>
      )}

      {result?.hasConflicts && (
        <div className="space-y-2">
          {result.panelists
            .filter((p) => p.hasConflicts)
            .map((p) => (
              <Alert
                key={`${p.employeeId}-${p.isExternal}`}
                // ⚠ Destructive ONLY for a hard clash. Painting a soft one red says "you cannot do
                // this" about something the server will happily accept.
                variant={p.hasHardConflicts ? 'destructive' : 'default'}
              >
                {p.hasHardConflicts ? <AlertTriangle className="h-4 w-4" /> : <Info className="h-4 w-4" />}
                <AlertTitle>
                  {p.employeeName}
                  {p.isExternal && <span className="ml-2 text-xs font-normal">(external)</span>}
                  {!p.hasHardConflicts && (
                    <span className="ml-2 text-xs font-normal">— worth knowing, does not block</span>
                  )}
                </AlertTitle>
                <AlertDescription className="space-y-1">
                  {p.commitments.map((c, i) => (
                    <CommitmentRow key={`${c.kind}-${c.reference ?? i}`} c={c} />
                  ))}
                </AlertDescription>
              </Alert>
            ))}

          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" size="sm" disabled={suggesting} onClick={suggest}>
              {suggesting ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <CalendarClock className="mr-1.5 h-4 w-4" />
              )}
              When is everyone free?
            </Button>
            {result.sourcesConsulted?.length > 0 && (
              <span className="text-xs text-muted-foreground">
                Checked against: {result.sourcesConsulted.join(', ')}.
              </span>
            )}
          </div>
        </div>
      )}

      {suggestions && (
        <div className="rounded-md border p-3">
          <p className="mb-2 text-sm font-medium">
            Free slots in the next fortnight
            <span className="ml-2 text-xs font-normal text-muted-foreground">
              (same length as the one you proposed)
            </span>
          </p>
          {suggestions.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nothing in the next fortnight where the whole panel is free. Try a shorter interview, a
              smaller panel, or a wider range.
            </p>
          ) : (
            <ul className="space-y-1 text-sm">
              {suggestions.map((s) => (
                <li key={`${s.date}-${s.startTime}`} className="flex items-baseline gap-2">
                  <span className="font-medium">{formatDate(s.date)}</span>
                  <span>
                    {formatTime(s.startTime)}–{formatTime(s.endTime)}
                  </span>
                  {/* ⚠ Offered, not hidden. A slot where somebody is nominally on leave is one HR
                      may well want, and withholding it on day-granular evidence would be the
                      system making that call. */}
                  {s.hasSoftConflicts && (
                    <span className="text-xs text-muted-foreground">— {s.softConflictSummary}</span>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
