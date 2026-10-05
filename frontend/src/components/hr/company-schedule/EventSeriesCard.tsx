'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CalendarPlus, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/components/ui/use-toast';
import { companyEventService } from '@/services/hr/company-schedule.service';
import { RECURRENCE_PATTERN_LABELS } from '@/types/hr/company-schedule';
import type { CompanyEventDetail } from '@/types/hr/company-schedule';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/**
 * A recurring event's series (lane 2f-1, D-12): its occurrences, each a full event of its own, with any that falls
 * on a public holiday or a company-wide closure flagged — and "Extend the series", on its rule. The occurrences ARE
 * the series: there is nothing else to open.
 */
export function EventSeriesCard({ event, open }: { event: CompanyEventDetail; open: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [extendOpen, setExtendOpen] = useState(false);
  const [count, setCount] = useState('');
  const [until, setUntil] = useState('');

  const occurrences = event.seriesOccurrences ?? [];
  const total = occurrences.length;
  const room = 52 - Math.max(0, ...occurrences.map((o) => o.occurrenceNumber));

  const extend = useMutation({
    mutationFn: () =>
      companyEventService.extendSeries(event.id, {
        count: count ? Number(count) : null,
        until: until || null,
      }),
    onSuccess: async (r) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      setExtendOpen(false);
      setCount('');
      setUntil('');
      toast({
        title: `Series extended by ${r.occurrences.length}`,
        description: [
          r.occurrences.map((o) => o.eventNumber).join(', ') + '.',
          ...r.warnings.map((w) => `⚠ ${w}`),
        ].join(' '),
      });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not extend the series',
        description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  // A row saved as "repeating" before series existed: nothing was ever made from it (C-14).
  if (!event.recurrenceSeriesId) {
    return event.isRecurring ? (
      <Card>
        <CardHeader><CardTitle>Repeats</CardTitle></CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          This event was recorded as repeating ({spaced(event.recurrencePattern)}) before series existed, and no other
          occurrences were made from it. To repeat it now, schedule a new recurring event.
        </CardContent>
      </Card>
    ) : null;
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle>
            Series — occurrence {event.occurrenceNumber} of {total}
          </CardTitle>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" asChild>
              <Link href={`/hr/company-schedule/events?series=${event.recurrenceSeriesId}`}>Open in the register</Link>
            </Button>
            {open && room > 0 && (
              <Button variant="outline" size="sm" onClick={() => setExtendOpen(true)}>
                <CalendarPlus className="mr-2 h-4 w-4" /> Extend the series
              </Button>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p className="text-muted-foreground">
          {event.recurrencePattern ? RECURRENCE_PATTERN_LABELS[event.recurrencePattern] : 'Repeats'}, counted from the
          first date. Each occurrence is its own event — its guests, replies, register and tasks — and is edited on its
          own page.
        </p>
        <ul className="divide-y rounded-md border">
          {occurrences.map((o) => (
            <li key={o.id} className={`flex flex-wrap items-center gap-3 px-3 py-2 ${o.id === event.id ? 'bg-muted/50' : ''}`}>
              <span className="w-8 text-right text-xs text-muted-foreground">{o.occurrenceNumber}</span>
              {o.id === event.id ? (
                <span className="font-mono text-xs">{o.eventNumber}</span>
              ) : (
                <Link className="font-mono text-xs text-primary underline underline-offset-2" href={`/hr/company-schedule/events/${o.id}`}>
                  {o.eventNumber}
                </Link>
              )}
              <span className="whitespace-nowrap">
                {new Date(`${o.startDate.slice(0, 10)}T00:00:00Z`).toLocaleDateString(undefined, {
                  timeZone: 'UTC', weekday: 'short', day: 'numeric', month: 'short', year: 'numeric',
                })}
                {o.startTime ? ` · ${o.startTime.slice(0, 5)}` : ''}
              </span>
              <StatusBadge status={spaced(o.status)} />
              {o.dayOffNote && <span className="text-xs text-amber-700 dark:text-amber-300">⚠ {o.dayOffNote}</span>}
            </li>
          ))}
        </ul>
      </CardContent>

      <Dialog open={extendOpen} onOpenChange={setExtendOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Extend the series</DialogTitle>
            <DialogDescription>
              More occurrences after the last, on the same rule, copied from the latest occurrence. Give how many more,
              or the date to run until — one, not both. The series can hold {room} more (52 in all). New dates start
              with no guests.
            </DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="extendCount">How many more</Label>
              <Input id="extendCount" type="number" min={1} max={room} value={count} onChange={(e) => setCount(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="extendUntil">…or until</Label>
              <Input id="extendUntil" type="date" value={until} onChange={(e) => setUntil(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setExtendOpen(false)}>Cancel</Button>
            <Button disabled={(!count && !until) || (!!count && !!until) || extend.isPending} onClick={() => extend.mutate()}>
              {extend.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Extend
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
