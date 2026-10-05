'use client';

import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { CalendarDays, Download, Loader2, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { RegisterPager } from '@/components/hr/company-schedule/RegisterPager';
import { useDebounce } from '@/hooks/use-debounce';
import { companyEventService } from '@/services/hr/company-schedule.service';
import { EVENT_CATEGORIES, EVENT_STATUSES } from '@/types/hr/company-schedule';
import type { CompanyEvent, CompanyEventSearch, EventCategory, EventStatus } from '@/types/hr/company-schedule';

const ALL = '__all__';
const PAGE_SIZE = 25;

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/** `"09:00:00"` → `"09:00"`. Empty for an all-day event. */
const hhmm = (t?: string | null) => (t ? t.slice(0, 5) : '');

/**
 * The events register. Since lane 2g-1 (D-9; C-10…C-13) it is searched, filtered by date, sorted and paged on the
 * server — it loaded every event and filtered in the browser — and the same filters export a CSV.
 */
export default function CompanyEventsPage() {
  const router = useRouter();
  const { toast } = useToast();
  // Lane 2f-1: the event page's "Open in the register" opens the register on one series.
  const series = useSearchParams().get('series');
  const [text, setText] = useState('');
  const [status, setStatus] = useState<string>(ALL);
  const [category, setCategory] = useState<string>(ALL);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);
  const term = useDebounce(text.trim(), 300);

  // A new filter starts again at the first page.
  useEffect(() => setPage(1), [term, status, category, from, to, series]);

  const filters: CompanyEventSearch = {
    text: term || undefined,
    status: status === ALL ? undefined : (status as EventStatus),
    category: category === ALL ? undefined : (category as EventCategory),
    from: from || undefined,
    to: to || undefined,
    seriesId: series ?? undefined,
  };

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events', 'search', filters, page],
    queryFn: () => companyEventService.search({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const events = data?.items ?? [];
  const filtered = !!(term || status !== ALL || category !== ALL || from || to);

  const exportCsv = async () => {
    setExporting(true);
    try {
      await companyEventService.exportCsv(filters);
    } catch (error: any) {
      toast({
        title: 'Could not export the events',
        description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company events"
        description="Meetings, training days, conferences and company-wide occasions."
        backHref="/hr/company-schedule"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={exportCsv} disabled={exporting || !data?.totalCount}>
              {exporting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
              Export CSV
            </Button>
            <Button onClick={() => router.push('/hr/company-schedule/events/new')}>
              <Plus className="mr-2 h-4 w-4" /> New event
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>
              {series ? 'One series' : 'Events'}
              {series && (
                <Button variant="link" size="sm" onClick={() => router.push('/hr/company-schedule/events')}>
                  Show all events
                </Button>
              )}
            </CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Name, number, venue or organiser…"
                className="pl-8"
                value={text}
                onChange={(e) => setText(e.target.value)}
              />
            </div>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <Label className="text-xs text-muted-foreground">Status</Label>
              <Select value={status} onValueChange={(v) => v && setStatus(v)}>
                <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {EVENT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>{spaced(s)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label className="text-xs text-muted-foreground">Category</Label>
              <Select value={category} onValueChange={(v) => v && setCategory(v)}>
                <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All categories</SelectItem>
                  {EVENT_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>{spaced(c)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="eventsFrom" className="text-xs text-muted-foreground">From</Label>
              <Input id="eventsFrom" type="date" className="w-40" value={from} onChange={(e) => setFrom(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="eventsTo" className="text-xs text-muted-foreground">To</Label>
              <Input id="eventsTo" type="date" className="w-40" value={to} onChange={(e) => setTo(e.target.value)} />
            </div>
            {isFetching && !isLoading && <Loader2 className="mb-2 h-4 w-4 animate-spin text-muted-foreground" />}
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
                        title={filtered || series ? 'No matching events' : 'No events yet'}
                        description={filtered || series ? 'Try a different search, filter or dates.' : 'Schedule the first company event.'}
                        action={
                          !filtered && !series ? (
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
          <RegisterPager data={data} noun="events" onPage={setPage} />
        </CardContent>
      </Card>
    </div>
  );
}
