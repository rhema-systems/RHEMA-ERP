'use client';

import { AlertCircle, CheckCircle2, Info, Loader2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

/**
 * The landing card an interview confirmation link arrives at.
 *
 * Shared by the candidate's "confirm attendance" page and the panelist's "confirm my assignment"
 * page so the two look like one product. Purely presentational — the pages own the call.
 *
 * ⚠ `already` is a SUCCESS tone, not an error. Somebody who clicks the link twice, or whose mail
 * scanner followed it first, has still confirmed; telling them something went wrong would send
 * them to the recruitment team over a non-event.
 */
export type ConfirmationTone = 'success' | 'already' | 'error';

export interface ConfirmationDetail {
  label: string;
  value: string;
}

const TONE = {
  success: {
    Icon: CheckCircle2,
    iconClass: 'text-green-600',
    ringClass: 'bg-green-50 dark:bg-green-950/40',
  },
  already: {
    Icon: Info,
    iconClass: 'text-blue-600',
    ringClass: 'bg-blue-50 dark:bg-blue-950/40',
  },
  error: {
    Icon: AlertCircle,
    iconClass: 'text-destructive',
    ringClass: 'bg-destructive/10',
  },
} as const;

/**
 * Formats a `DateOnly` the server sent as "2026-07-20".
 *
 * ⚠ Parsed part by part, NOT with `new Date(value)`. That treats a bare date as UTC midnight, so
 * every reader west of Greenwich would be shown the day before their interview.
 */
export function formatInterviewDate(value?: string | null): string | null {
  if (!value) return null;

  const [year, month, day] = value.split('T')[0].split('-').map(Number);
  if (!year || !month || !day) return null;

  // DateOnly.MinValue ("0001-01-01") is what the server returns when it could not resolve the
  // interview. Rendering "1 January 1" would be worse than saying nothing.
  if (year < 1900) return null;

  return new Date(year, month - 1, day).toLocaleDateString(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}

export function InterviewConfirmationCard({
  tone,
  heading,
  message,
  details = [],
  footer,
}: {
  tone: ConfirmationTone;
  heading: string;
  message: string;
  details?: ConfirmationDetail[];
  footer?: React.ReactNode;
}) {
  const { Icon, iconClass, ringClass } = TONE[tone];
  const rows = details.filter((detail) => !!detail.value);

  return (
    <div className="mx-auto w-full max-w-lg py-10">
      <Card>
        <CardHeader className="items-center text-center">
          <div className={`mb-2 rounded-full p-3 ${ringClass}`}>
            <Icon className={`h-8 w-8 ${iconClass}`} />
          </div>
          <CardTitle className="text-xl">{heading}</CardTitle>
          <CardDescription className="text-base">{message}</CardDescription>
        </CardHeader>

        {(rows.length > 0 || footer) && (
          <CardContent className="space-y-4">
            {rows.length > 0 && (
              <dl className="divide-y rounded-lg border text-sm">
                {rows.map((detail) => (
                  <div key={detail.label} className="flex justify-between gap-4 px-4 py-3">
                    <dt className="text-muted-foreground">{detail.label}</dt>
                    <dd className="text-right font-medium">{detail.value}</dd>
                  </div>
                ))}
              </dl>
            )}
            {footer}
          </CardContent>
        )}
      </Card>
    </div>
  );
}

export function InterviewConfirmationPending({ message }: { message: string }) {
  return (
    <div className="mx-auto w-full max-w-lg py-10">
      <Card>
        <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          <p className="text-sm text-muted-foreground">{message}</p>
        </CardContent>
      </Card>
    </div>
  );
}
