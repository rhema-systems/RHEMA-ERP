'use client';

import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CalendarSearch, Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { RoomDayBoard, RoomRules, localDay, shiftDay } from '@/components/hr/company-schedule/RoomDayBoard';
import { myRoomBookingService } from '@/services/hr/company-schedule.service';

/**
 * Booking a room from the portal (company schedule lane 3c, D-13).
 *
 * The desk's form (`/hr/company-schedule/bookings/new`) without the event link — a room for a company event is the HR
 * desk's (the user's ruling) — and with the day's board, so the rooms' busy times are in view while choosing. As there,
 * the room list is the availability list: rooms are offered once there is a window to check them against.
 *
 * ⚠ No booked-by field — the booker is the token. A start that has passed is refused by the server.
 */

const schema = z
  .object({
    startDateTime: z.string().min(1, 'Pick a start'),
    endDateTime: z.string().min(1, 'Pick an end'),
    roomId: z.string().min(1, 'Pick a room'),
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
const isDay = (s: string | null): s is string => !!s && /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s));
const isId = (s: string | null): s is string => !!s && /^[0-9a-f-]{36}$/i.test(s);

export default function NewMyRoomBookingPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // Company-schedule lane 7b (D-13): the calendar's "Book this room" opens here for a day (`?date=`) and a room (`?room=`)
  // — 09:00 to 10:00 that day, the rooms searched at once; a room that is not free then drops out as before.
  const params = useSearchParams();
  const preDay = params.get('date');
  const preRoom = params.get('room');
  const [saving, setSaving] = useState(false);
  const [searched, setSearched] = useState(isDay(preDay));

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      startDateTime: isDay(preDay) ? `${preDay}T09:00` : '',
      endDateTime: isDay(preDay) ? `${preDay}T10:00` : '',
      roomId: isId(preRoom) ? preRoom : '',
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
  const roomId = form.watch('roomId');
  const windowReady = !!start && !!end && end > start;
  const day = start ? start.slice(0, 10) : localDay();

  const { data: rooms } = useQuery({
    queryKey: ['me', 'room-bookings', 'rooms'],
    queryFn: () => myRoomBookingService.getRooms(),
  });
  const { data: busy } = useQuery({
    queryKey: ['me', 'room-bookings', 'busy', day],
    queryFn: () => myRoomBookingService.getBusy(shiftDay(day, -1), shiftDay(day, 1)),
  });
  const { data: available, isFetching } = useQuery({
    queryKey: ['me', 'room-bookings', 'available', start, end, attendees],
    queryFn: () =>
      myRoomBookingService.getAvailable(
        toIsoInstant(start) ?? '',
        toIsoInstant(end) ?? '',
        attendees > 0 ? attendees : undefined,
      ),
    enabled: windowReady && searched,
  });

  // A room chosen for an earlier window may not be free for this one: let it go rather than book it blind.
  useEffect(() => {
    if (roomId && available && !available.some((r) => r.id === roomId)) form.setValue('roomId', '');
  }, [available, roomId]);

  const chosen = rooms?.find((r) => r.id === roomId);

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      const created = await myRoomBookingService.create({
        roomId: values.roomId,
        startDateTime: toIsoInstant(values.startDateTime) ?? '',
        endDateTime: toIsoInstant(values.endDateTime) ?? '',
        purpose: values.purpose.trim(),
        expectedAttendees: values.expectedAttendees,
        specialRequirements: orNull(values.specialRequirements),
        cateringRequirements: orNull(values.cateringRequirements),
        notes: orNull(values.notes),
      });
      await queryClient.invalidateQueries({ queryKey: ['me', 'room-bookings'] });
      toast({
        title: 'Room booked',
        description: `${created.bookingNumber} — ${created.roomName}.${
          created.status === 'Tentative' ? ' It waits for approval; you will be told either way.' : ''
        }`,
      });
      router.push(`/me/room-bookings/${created.id}`);
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
    <form onSubmit={onSubmit} className="space-y-6">
      <PageHeader
        title="Book a room"
        description="You are recorded as the person booking. A room that needs approval waits until the HR desk approves it."
        backHref="/me/room-bookings"
      />

      <Card>
        <CardHeader><CardTitle className="text-base">When and how many</CardTitle></CardHeader>
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
        <CardHeader>
          <CardTitle className="text-base">
            When the rooms are held —{' '}
            {new Date(`${day}T00:00`).toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {!rooms ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Loading the rooms…
            </div>
          ) : (
            <RoomDayBoard
              day={day}
              rooms={rooms}
              busy={busy ?? []}
              proposed={
                windowReady
                  ? { start: toIsoInstant(start) ?? '', end: toIsoInstant(end) ?? '', roomId: roomId || null }
                  : null
              }
            />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Which room</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {!searched || !windowReady ? (
            <EmptyState
              icon={CalendarSearch}
              title="Set the time first"
              description="Rooms are offered once there is a start and an end to check them against."
            />
          ) : isFetching ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Checking what is free…
            </div>
          ) : (available ?? []).length === 0 ? (
            <EmptyState
              icon={CalendarSearch}
              title="Nothing free then"
              description="Try another time or fewer people — or a shorter booking, or one nearer today, as some rooms limit both."
            />
          ) : (
            <SelectField
              form={form}
              name="roomId"
              label="Room"
              required
              options={(available ?? []).map((r) => ({
                value: r.id,
                label: `${r.roomName} · ${r.locationName} · seats ${r.capacity}${r.requiresApproval ? ' · needs approval' : ''}`,
              }))}
            />
          )}
          {chosen && <RoomRules room={chosen} />}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">What for</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="purpose" label="Purpose" required />
          <FieldRow>
            <TextareaField form={form} name="specialRequirements" label="Special requirements" />
            <TextareaField form={form} name="cateringRequirements" label="Catering" />
          </FieldRow>
          <TextareaField form={form} name="notes" label="Notes" />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={() => router.push('/me/room-bookings')} disabled={saving}>
          Cancel
        </Button>
        <Button type="submit" disabled={saving}>
          {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Book room
        </Button>
      </div>
    </form>
  );
}
