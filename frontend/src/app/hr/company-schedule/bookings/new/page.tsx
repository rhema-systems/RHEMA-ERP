'use client';

import { useState } from 'react';
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
import { companyEventService, meetingRoomService, roomBookingService } from '@/services/hr/company-schedule.service';
import type { MeetingRoomSummary } from '@/types/hr/company-schedule';

/**
 * Booking a room.
 *
 * ⚠ **The room list is the availability list, not the room register.** Picking a start and end
 * first and then asking the API which rooms are free is the whole point — offering every room and
 * letting the create fail on a clash would be a worse version of the same screen. The picker stays
 * empty until both times are set.
 *
 * ⚠ **No booked-by field** — the API takes the booker from the token.
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

export default function NewRoomBookingPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  const [searched, setSearched] = useState(false);

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

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      const created = await roomBookingService.create({
        roomId: values.roomId,
        eventId: orNull(values.eventId),
        startDateTime: toIsoInstant(values.startDateTime) ?? '',
        endDateTime: toIsoInstant(values.endDateTime) ?? '',
        purpose: values.purpose.trim(),
        expectedAttendees: values.expectedAttendees,
        specialRequirements: orNull(values.specialRequirements),
        cateringRequirements: orNull(values.cateringRequirements),
        notes: orNull(values.notes),
      });
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
          Book room
        </Button>
      </div>
    </form>
  );
}
