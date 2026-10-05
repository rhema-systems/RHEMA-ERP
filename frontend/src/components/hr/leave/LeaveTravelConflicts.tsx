'use client';

import { Plane } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';

/**
 * The same warning as a badge on the approver's list (travel final closure, lane 9, D-55) — an approver deciding in bulk
 * does not open each request, so the row says it; the tooltip carries each trip's sentence.
 */
export function LeaveTravelConflictBadge({ conflicts }: { conflicts?: string[] | null }) {
  if (!conflicts?.length) return null;
  return (
    <Badge
      variant="outline"
      className="gap-1 border-amber-300 text-amber-700 dark:border-amber-500/50 dark:text-amber-400"
      title={conflicts.join('\n')}
    >
      <Plane className="h-3 w-3" />
      {conflicts.length === 1 ? 'Staff travel over these days' : `${conflicts.length} trips over these days`}
    </Badge>
  );
}

/**
 * The employee's staff travel over a leave request's days (travel final closure, lane 9, D-55) — computed by the server
 * on every read of the request, so it shows however the dates were set: raised, suggested by the approver, rescheduled.
 * Advisory, in the shape of the leave plan's reliever clashes: nothing refuses the leave.
 */
export function LeaveTravelConflicts({ conflicts }: { conflicts?: string[] | null }) {
  if (!conflicts?.length) return null;
  return (
    <Alert>
      <Plane className="h-4 w-4" />
      <AlertTitle>Staff travel over these days</AlertTitle>
      <AlertDescription>
        <ul className="mt-1 list-disc space-y-1 pl-4 text-sm">
          {conflicts.map((line) => (
            <li key={line}>{line}</li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-muted-foreground">
          The leave can still be decided — this is a warning, not a rule. The travel desk can change or cancel the trip.
        </p>
      </AlertDescription>
    </Alert>
  );
}
