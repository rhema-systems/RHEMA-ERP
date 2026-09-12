'use client';

import { useState } from 'react';
import { AlertTriangle, CalendarCheck, CheckCircle2, Loader2, Plane, Search } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatTime } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import type { PanelistAvailabilityCheck } from '@/types/hr/interviews';

/**
 * Advisory clash check for a proposed slot.
 *
 * ⚠ **Non-blocking by design, on both sides.** The server never refuses a booking because of a
 * clash — a chair may well be double-booked and sort it out themselves — so this is a warning the
 * scheduler reads, not a gate. It is checked on demand rather than on every keystroke because it
 * reads other people's leave and travel, and because the slot is usually edited several times
 * before it settles.
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

  const ready = !!date && !!start && !!end && (panelistIds.length > 0 || externalPanelistIds.length > 0);

  const run = async () => {
    if (!date || !start || !end) return;
    setChecking(true);
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

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm font-medium">Panel availability</p>
          <p className="text-sm text-muted-foreground">
            Checks the panel against their other interviews, approved leave and travel. Advisory only —
            a clash never blocks the booking.
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
          <AlertDescription>Everyone on the panel is free for this slot.</AlertDescription>
        </Alert>
      )}

      {result?.hasConflicts && (
        <div className="space-y-2">
          {result.panelists
            .filter((p) => p.hasConflicts)
            .map((p) => (
              <Alert key={`${p.employeeId}-${p.isExternal}`} variant="destructive">
                <AlertTriangle className="h-4 w-4" />
                <AlertTitle>
                  {p.employeeName}
                  {p.isExternal && <span className="ml-2 text-xs font-normal">(external)</span>}
                </AlertTitle>
                <AlertDescription className="space-y-1">
                  {p.interviewConflicts.map((c) => (
                    <div key={c.interviewId} className="flex items-center gap-2 text-sm">
                      <CalendarCheck className="h-3.5 w-3.5 shrink-0" />
                      <span>
                        {c.interviewNumber} — {c.jobTitle || 'interview'} at {formatTime(c.startTime)}–
                        {formatTime(c.endTime)}
                      </span>
                    </div>
                  ))}
                  {p.leaveConflicts.map((c, i) => (
                    <div key={`leave-${i}`} className="flex items-center gap-2 text-sm">
                      <CalendarCheck className="h-3.5 w-3.5 shrink-0" />
                      <span>
                        {c.status} leave, {formatDate(c.startDate)} – {formatDate(c.endDate)}
                      </span>
                    </div>
                  ))}
                  {p.travelConflicts.map((c, i) => (
                    <div key={`travel-${i}`} className="flex items-center gap-2 text-sm">
                      <Plane className="h-3.5 w-3.5 shrink-0" />
                      <span>
                        Travel {c.requestNumber} ({c.status}), {formatDate(c.startDate)} – {formatDate(c.endDate)}
                      </span>
                    </div>
                  ))}
                </AlertDescription>
              </Alert>
            ))}
        </div>
      )}
    </div>
  );
}
