'use client';

import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { companyEventService } from '@/services/hr/company-schedule.service';
import { EVENT_CATEGORIES, EVENT_STATUSES } from '@/types/hr/company-schedule';
import type { CompanyEvent } from '@/types/hr/company-schedule';

const ALL = '__all__';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/** `"09:00:00"` → `"09:00"`. Empty for an all-day event. */
const hhmm = (t?: string | null) => (t ? t.slice(0, 5) : '');

export default function CompanyEventsPage() {
  const router = useRouter();
  // Lane 2f-1: the event page's "all occurrences" link opens the register on one series.
  const series = useSearchParams().get('series');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ALL);
  const [category, setCategory] = useState<string>(ALL);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events'],
    queryFn: () => companyEventService.getAll(),
  });

  const events = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (data ?? [])
      .filter((e) => !series || e.recurrenceSeriesId === series)
      .filter((e) => (status === ALL ? true : e.status === status))
      .filter((e) => (category === ALL ? true : e.category === category))
      .filter(
        (e) =>
          !term ||
          e.eventName.toLowerCase().includes(term) ||
          e.eventNumber.toLowerCase().includes(term) ||
          (e.venueName ?? '').toLowerCase().includes(term) ||
          e.organizerName.toLowerCase().includes(term),
      )
      .sort((a, b) =>
        series ? (a.occurrenceNumber ?? 0) - (b.occurrenceNumber ?? 0) : b.startDate.localeCompare(a.startDate));
  }, [data, search, status, category, series]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company events"
        description="Meetings, training days, conferences and company-wide occasions."
        backHref="/hr/company-schedule"
        actions={
          <Button onClick={() => router.push('/hr/company-schedule/events/new')}>
            <Plus className="mr-2 h-4 w-4" /> New event
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>
              {series ? 'One series' : 'Events'}
              {series && (
                <Button variant="link" size="sm" onClick={() => router.push('/hr/company-schedule/events')}>
                  Show all events
                </Button>
              )}
            </CardTitle>
            <div className="flex flex-wrap items-center gap-2">
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {EVENT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>{spaced(s)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Select value={category} onValueChange={setCategory}>
                <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All categories</SelectItem>
                  {EVENT_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>{spaced(c)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <div className="relative w-64">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search events…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Event</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>When</TableHead>
                  <TableHead>Where</TableHead>
                  <TableHead>Organiser</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : events.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={CalendarDays}
                        title={data?.length ? 'No matching events' : 'No events yet'}
                        description={
                          data?.length
                            ? 'Try a different search or filter.'
                            : 'Schedule the first company event.'
                        }
                        action={
                          !data?.length ? (
                            <Button size="sm" onClick={() => router.push('/hr/company-schedule/events/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New event
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  events.map((e: CompanyEvent) => (
                    <TableRow
                      key={e.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/company-schedule/events/${e.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{e.eventNumber}</TableCell>
                      <TableCell className="font-medium">
                        {e.eventName}
                        {e.occurrenceNumber && e.occurrenceCount ? (
                          <span className="ml-2 text-xs font-normal text-muted-foreground">
                            {e.occurrenceNumber} of {e.occurrenceCount}
                          </span>
                        ) : null}
                      </TableCell>
                      <TableCell>{spaced(e.category)}</TableCell>
                      <TableCell className="whitespace-nowrap">
                        {e.startDate.slice(0, 10)}
                        {e.isAllDayEvent ? (
                          <span className="text-muted-foreground"> · all day</span>
                        ) : (
                          hhmm(e.startTime) && <span className="text-muted-foreground"> · {hhmm(e.startTime)}</span>
                        )}
                      </TableCell>
                      <TableCell>{e.venueName || e.locationName || spaced(e.locationType)}</TableCell>
                      <TableCell>{e.organizerName}</TableCell>
                      <TableCell><StatusBadge status={spaced(e.status)} /></TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
