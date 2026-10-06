'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CalendarSearch, Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  DateTimeField,
  FieldRow,
  NumberField,
  SelectField,
  TextField,
  TextareaField,
  toIsoInstant,
} from '@/components/hr/employee/tabs/fields';
import { SeriesScopeField } from '@/components/hr/company-schedule/SeriesScopeField';
import { companyEventService, meetingRoomService, roomBookingService } from '@/services/hr/company-schedule.service';
import type { MeetingRoomSummary, RoomBookingSeriesResult, SeriesScope } from '@/types/hr/company-schedule';

/**
 * Booking a room.
 *
 * ⚠ **The room list is the availability list, not the room register.** Picking a start and end
 * first and then asking the API which rooms are free is the whole point — offering every room and
 * letting the create fail on a clash would be a worse version of the same screen. The picker stays
 * empty until both times are set.
 *
 * ⚠ **No booked-by field** — the API takes the booker from the token.
 *
 * **A series (lane 3d-1, D-12).** When the linked event is a date of a recurring event, "Which dates" offers this date
 * and following, or every date: the window is this date's, and each other date is booked at the same distance from its
 * own start. The room is checked here for this date only; the server checks each other date and lists those it cannot
 * take, booking the rest — so the answer is a list, shown in place of moving to one booking's page.
 */

const schema = z
  .object({
    startDateTime: z.string().min(1, 'Pick a start'),
    endDateTime: z.string().min(1, 'Pick an end'),
    roomId: z.string().min(1, 'Pick a room'),
    eventId: z.string().optional().or(z.literal('')),
    purpose: z.string().min(1, 'Say what the room is for').max(500),
    expectedAttendees: z.coerce.number().int().min(1, 'At least one person'),
    specialRequirements: z.string().max(1000).optional().or(z.literal('')),
    cateringRequirements: z.string().max(1000).optional().or(z.literal('')),
    notes: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => !v.startDateTime || !v.endDateTime || v.endDateTime > v.startDateTime, {
    message: 'The end must be after the start',
    path: ['endDateTime'],
  });

type FormValues = z.infer<typeof schema>;

const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);

/** "Tue 14 Oct 2026, 09:00–11:00" — a booking's window as the series result lists it. */
const span = (startIso: string, endIso: string) => {
  const s = new Date(startIso);
  const e = new Date(endIso);
  const t = (d: Date) => d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  return `${s.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })}, ${t(s)}–${t(e)}`;
};

export default function NewRoomBookingPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  const [searched, setSearched] = useState(false);
  const [seriesScope, setSeriesScope] = useState<SeriesScope>('ThisOccurrence');
  const [seriesResult, setSeriesResult] = useState<RoomBookingSeriesResult | null>(null);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      startDateTime: '',
      endDateTime: '',
      roomId: '',
      eventId: '',
      purpose: '',
      expectedAttendees: 1,
      specialRequirements: '',
      cateringRequirements: '',
      notes: '',
    },
  });

  const start = form.watch('startDateTime');
  const end = form.watch('endDateTime');
  const attendees = form.watch('expectedAttendees');
  const windowReady = !!start && !!end && end > start;

  const { data: available, isFetching } = useQuery({
    queryKey: ['hr', 'company-schedule', 'rooms', 'available', start, end, attendees],
    queryFn: () =>
      meetingRoomService.getAvailable(
        toIsoInstant(start) ?? '',
        toIsoInstant(end) ?? '',
        attendees > 0 ? attendees : undefined,
      ),
    enabled: windowReady && searched,
  });

  const { data: events } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events', 'upcoming'],
    queryFn: () => companyEventService.getUpcoming(120),
  });

  const eventId = form.watch('eventId');
  const linkedEvent = (events ?? []).find((e) => e.id === eventId);
  const inSeries = !!linkedEvent?.recurrenceSeriesId;
  const bookSeries = inSeries && seriesScope !== 'ThisOccurrence';

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      const body = {
        roomId: values.roomId,
        eventId: orNull(values.eventId),
        startDateTime: toIsoInstant(values.startDateTime) ?? '',
        endDateTime: toIsoInstant(values.endDateTime) ?? '',
        purpose: values.purpose.trim(),
        expectedAttendees: values.expectedAttendees,
        specialRequirements: orNull(values.specialRequirements),
        cateringRequirements: orNull(values.cateringRequirements),
        notes: orNull(values.notes),
      };
      if (bookSeries && body.eventId) {
        const result = await roomBookingService.createForSeries({
          ...body,
          eventId: body.eventId,
          seriesScope: seriesScope as Exclude<SeriesScope, 'ThisOccurrence'>,
        });
        await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'bookings'] });
        setSeriesResult(result);
        window.scrollTo({ top: 0, behavior: 'smooth' });
        toast({
          title: `Room booked for ${result.booked.length} date${result.booked.length === 1 ? '' : 's'}`,
          description: result.notBooked.length
            ? `${result.notBooked.length} date${result.notBooked.length === 1 ? '' : 's'} could not be booked — listed below.`
            : undefined,
        });
        return;
      }
      const created = await roomBookingService.create(body);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'bookings'] });
      toast({
        title: 'Room booked',
        description: `${created.bookingNumber} — ${created.roomName}. ${
          created.status === 'Tentative' ? 'It needs approval before it is confirmed.' : ''
        }`,
      });
      router.push(`/hr/company-schedule/bookings/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Could not book the room',
        description:
          error?.response?.data?.detail ??
          error?.message ??
          'The room may have been taken while you were filling this in.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-6 p-6">
      <PageHeader
        title="Book a room"
        description="You are recorded as the person booking."
        backHref="/hr/company-schedule/bookings"
      />

      {seriesResult && (
        <Card>
          <CardHeader>
            <CardTitle>
              Booked for {seriesResult.booked.length} date{seriesResult.booked.length === 1 ? '' : 's'}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            {seriesResult.approvalCarriedBy && (
              <p className="text-muted-foreground">
                The room needs approval: {seriesResult.approvalCarriedBy} asks for all of them, and its decision covers the
                rest.
              </p>
            )}
            <ul className="space-y-1">
              {seriesResult.booked.map((b) => (
                <li key={b.id}>
                  <Link href={`/hr/company-schedule/bookings/${b.id}`} className="font-medium hover:underline">
                    {b.bookingNumber}
                  </Link>{' '}
                  — {span(b.startDateTime, b.endDateTime)} · {b.status === 'Tentative' ? 'awaiting approval' : b.statusName}
                </li>
              ))}
            </ul>
            {seriesResult.notBooked.length > 0 && (
              <div className="space-y-1">
                <p className="font-medium">Not booked</p>
                <ul className="space-y-1">
                  {seriesResult.notBooked.map((n) => (
                    <li key={n.eventId}>
                      {n.eventNumber} — {span(n.startDateTime, n.endDateTime)}:{' '}
                      <span className="text-muted-foreground">{n.reason}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {seriesResult.closed > 0 && (
              <p className="text-muted-foreground">
                {seriesResult.closed} date{seriesResult.closed === 1 ? '' : 's'} already started, completed or cancelled
                {seriesResult.closed === 1 ? ' was' : ' were'} left alone.
              </p>
            )}
            <Button asChild variant="outline">
              <Link href="/hr/company-schedule/bookings">Go to the bookings</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle>When and how many</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <DateTimeField form={form} name="startDateTime" label="From" required />
            <DateTimeField form={form} name="endDateTime" label="To" required />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="expectedAttendees" label="Expected attendees" required />
            <div className="flex items-end">
              <Button
                type="button"
                variant="outline"
                className="w-full"
                disabled={!windowReady}
                onClick={() => setSearched(true)}
              >
                <CalendarSearch className="mr-2 h-4 w-4" />
                Find free rooms
              </Button>
            </div>
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Which room</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {!searched || !windowReady ? (
            <EmptyState
              icon={CalendarSearch}
              title="Set the window first"
              description="Rooms are offered once there is a start and an end to check them against."
            />
          ) : isFetching ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Checking what is free…
            </div>
          ) : (available ?? []).length === 0 ? (
            <EmptyState
              icon={CalendarSearch}
              title="Nothing free in that window"
              description="Try a different time, or a smaller number of attendees."
            />
          ) : (
            <>
              <SelectField
                form={form}
                name="roomId"
                label="Room"
                required
                options={(available ?? []).map((r: MeetingRoomSummary) => ({
                  value: r.id,
                  label: `${r.roomName} · ${r.locationName} · seats ${r.capacity}`,
                }))}
              />
              <div className="flex flex-wrap gap-2">
                {(available ?? []).map((r) => (
                  <Badge key={r.id} variant="secondary">
                    {r.roomName} ({r.capacity})
                  </Badge>
                ))}
              </div>
            </>
          )}
          {bookSeries && (
            <p className="text-xs text-muted-foreground">
              Rooms are checked here for this date only. Each other date is checked when you book, and any the room cannot
              take is listed.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>What for</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="purpose" label="Purpose" required />
          <SelectField
            form={form}
            name="eventId"
            label="Linked event"
            allowEmpty
            emptyLabel="Not linked to an event"
            options={(events ?? []).map((e) => ({ value: e.id, label: `${e.eventNumber} — ${e.eventName}` }))}
          />
          {inSeries && (
            <SeriesScopeField
              value={seriesScope}
              onChange={setSeriesScope}
              hint={`${linkedEvent?.eventNumber} is date ${linkedEvent?.occurrenceNumber ?? ''} of a recurring event. Book the room for its other dates too, each at the same time relative to its own start; the dates the room cannot take are listed and the rest booked.`}
              tail="on a room needing approval, the first date asks and its decision covers the rest."
            />
          )}
          <FieldRow>
            <TextareaField form={form} name="specialRequirements" label="Special requirements" />
            <TextareaField form={form} name="cateringRequirements" label="Catering" />
          </FieldRow>
          <TextareaField form={form} name="notes" label="Notes" />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          onClick={() => router.push('/hr/company-schedule/bookings')}
          disabled={saving}
        >
          Cancel
        </Button>
        <Button type="submit" disabled={saving}>
          {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          {bookSeries ? 'Book the dates' : 'Book room'}
        </Button>
      </div>
    </form>
  );
}
