'use client';

import { AlertTriangle } from 'lucide-react';

/**
 * What a staff-travel screen shows when a read fails.
 *
 * ⚠ Before this (travel final closure, lane 0 — finding F4) no travel query looked at `isError`, so
 * a 403 or a 500 rendered as the screen's empty state: "Nothing has been raised yet", "No budget
 * set", "No passport is on file". That tells the user something false about the data. This says the
 * read failed, and why when the server said why.
 *
 * `what` names the thing that failed to load, in lower case: "the travel requests".
 *
 * ⚠ Render it only when NOTHING is loaded: `isError && !data`. TanStack Query 5 sets `isError` on a
 * failed background refetch too, and keeps the last good data — replacing a page the user is reading
 * with an error box because a focus refetch failed would be worse than the old empty state.
 */
export function TravelQueryError({ error, what }: { error: unknown; what: string }) {
  const status = (error as { status?: number } | null | undefined)?.status;
  const message = (error as Error | null | undefined)?.message;
  const subject = what.charAt(0).toUpperCase() + what.slice(1);

  const headline =
    status === 403
      ? `You do not have access to ${what}.`
      : status === 404
        ? `${subject} could not be found.`
        : `${subject} could not be loaded.`;

  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm"
    >
      <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
      <div className="space-y-1">
        <p className="font-medium text-destructive">{headline}</p>
        {message && status !== 403 && status !== 404 && (
          <p className="text-muted-foreground">{message}</p>
        )}
      </div>
    </div>
  );
}
