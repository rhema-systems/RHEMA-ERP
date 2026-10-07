'use client';

import { use, useEffect, useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateTimeField,
  FieldRow,
  NumberField,
  TextField,
  TextareaField,
  fromIsoInstant,
  toIsoInstant,
} from '@/components/hr/employee/tabs/fields';
import {
  RoomDayBoard,
  RoomRules,
  bookingStatusWord,
  localDay,
  shiftDay,
} from '@/components/hr/company-schedule/RoomDayBoard';
import { myRoomBookingService } from '@/services/hr/company-schedule.service';

const when = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—';

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <div className="text-sm">{children ?? '—'}</div>
    </div>
  );
}

/**
 * One of the caller's own room bookings, from the portal (company schedule lane 3c, D-13): its detail, a change while it
 * holds its room, and cancelling it with a reason. Somebody else's booking answers 404, and reads here as not found.
 *
 * ⚠ **The room cannot be changed** — `UpdateRoomBookingDto` has no `roomId`; cancel and book again. A changed start may
 * not be in the past; an unchanged one may, so a booking under way can be extended. Approving, not approving and no-show
 * are the desk's and are not offered here.
 */

const schema = z
  .object({
    startDateTime: z.string().min(1, 'Pick a start'),
    endDateTime: z.string().min(1, 'Pick an end'),
    purpose: z.string().min(1, 'Say what the room is for').max(500),
    expectedAttendees: z.coerce.number().int().min(1),
    specialRequirements: z.string().max(1000).optional().or(z.literal('')),
    cateringRequirements: z.string().max(1000).optional().or(z.literal('')),
    notes: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => v.endDateTime > v.startDateTime, {
    message: 'The end must be after the start',
    path: ['endDateTime'],
  });

type FormValues = z.infer<typeof schema>;
const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);

export default function MyRoomBookingPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [saving, setSaving] = useState(false);

  const key = ['me', 'room-bookings', id];
  const { data: booking, isLoading } = useQuery({
    queryKey: key,
    queryFn: () => myRoomBookingService.getById(id),
    retry: false,
  });
  const { data: rooms } = useQuery({
    queryKey: ['me', 'room-bookings', 'rooms'],
    queryFn: () => myRoomBookingService.getRooms(),
  });

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      startDateTime: '',
      endDateTime: '',
      purpose: '',
      expectedAttendees: 1,
      specialRequirements: '',
      cateringRequirements: '',
      notes: '',
    },
  });

  useEffect(() => {
    if (!booking) return;
    form.reset({
      startDateTime: fromIsoInstant(booking.startDateTime),
      endDateTime: fromIsoInstant(booking.endDateTime),
      purpose: booking.purpose,
      expectedAttendees: booking.expectedAttendees,
      specialRequirements: booking.specialRequirements ?? '',
      cateringRequirements: booking.cateringRequirements ?? '',
      notes: booking.notes ?? '',
    });
  }, [booking]);

  const start = form.watch('startDateTime');
  const end = form.watch('endDateTime');
  const day = start ? start.slice(0, 10) : booking ? fromIsoInstant(booking.startDateTime).slice(0, 10) : localDay();
  const { data: busy } = useQuery({
    queryKey: ['me', 'room-bookings', 'busy', day],
    queryFn: () => myRoomBookingService.getBusy(shiftDay(day, -1), shiftDay(day, 1)),
    enabled: !!booking,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['me', 'room-bookings'] });

  const fail = (title: string) => (error: any) =>
    toast({
      title,
      description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const cancel = useMutation({
    mutationFn: () => myRoomBookingService.cancel(id, cancelReason.trim()),
    onSuccess: async () => {
      await refresh();
      setCancelOpen(false);
      setCancelReason('');
      toast({ title: 'Booking cancelled', description: 'The room is free for someone else.' });
    },
    onError: fail('Could not cancel the booking'),
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      const saved = await myRoomBookingService.update(id, {
        id,
        startDateTime: toIsoInstant(values.startDateTime) ?? '',
        endDateTime: toIsoInstant(values.endDateTime) ?? '',
        purpose: values.purpose.trim(),
        expectedAttendees: values.expectedAttendees,
        specialRequirements: orNull(values.specialRequirements),
        cateringRequirements: orNull(values.cateringRequirements),
        notes: orNull(values.notes),
      });
      await refresh();
      toast({
        title: 'Booking updated',
        description:
          saved.status === 'Tentative' && booking?.status === 'Confirmed'
            ? 'The new time waits for approval again; you will be told either way.'
            : undefined,
      });
    } catch (e) {
      fail('Could not update the booking')(e);
    } finally {
      setSaving(false);
    }
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!booking) {
    return (
      <div className="space-y-6">
        <PageHeader title="Room booking" backHref="/me/room-bookings" />
        <p className="text-muted-foreground">That booking could not be found among yours.</p>
      </div>
    );
  }

  const open = !booking.isCancelled && (booking.status === 'Tentative' || booking.status === 'Confirmed');
  const room = rooms?.find((r) => r.id === booking.roomId);
  const windowReady = !!start && !!end && end > start;
  // The booking itself is on the board; drawn as held, it would look like a clash with its own new time.
  const others = (busy ?? []).filter((b) => b.bookingId !== booking.id);

  return (
    <div className="space-y-6">
      <PageHeader
        title={booking.roomName}
        description={`${booking.bookingNumber} · ${booking.purpose}`}
        backHref="/me/room-bookings"
        actions={
          open ? (
            <Button variant="outline" onClick={() => setCancelOpen(true)}>
              <XCircle className="mr-2 h-4 w-4" /> Cancel booking
            </Button>
          ) : undefined
        }
      />
      {booking.status === 'Tentative' && open && (
        <p className="text-sm text-muted-foreground">
          This booking waits for the HR desk to approve it. You will be told either way; one still waiting when its time
          comes is cancelled.
        </p>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base">Booking</CardTitle></CardHeader>
        <CardContent className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <Detail label="Status"><StatusBadge status={bookingStatusWord(booking.status, booking.isCancelled)} /></Detail>
          <Detail label="From">{when(booking.startDateTime)}</Detail>
          <Detail label="To">{when(booking.endDateTime)}</Detail>
          <Detail label="Booked on">{when(booking.bookingDate)}</Detail>
          {booking.approvedByName && (
            <Detail label="Approved by">{`${booking.approvedByName} · ${when(booking.approvalDate)}`}</Detail>
          )}
          {booking.eventName && <Detail label="For the event">{booking.eventName}</Detail>}
          {booking.isCancelled && (
            <Detail label="Cancelled">
              {when(booking.cancellationDate)} — {booking.cancellationReason}
            </Detail>
          )}
        </CardContent>
      </Card>

      {open ? (
        <form onSubmit={onSubmit} className="space-y-6">
          <Card>
            <CardHeader><CardTitle className="text-base">Change it</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <p className="text-xs text-muted-foreground">
                To use another room, cancel this booking and book again.
                {room?.requiresApproval && booking.status === 'Confirmed'
                  ? ' This room needs approval: a new time waits for approval again.'
                  : ''}
              </p>
              <FieldRow>
                <DateTimeField form={form} name="startDateTime" label="From" required />
                <DateTimeField form={form} name="endDateTime" label="To" required />
              </FieldRow>
              <FieldRow>
                <TextField form={form} name="purpose" label="Purpose" required />
                <NumberField form={form} name="expectedAttendees" label="Expected attendees" required />
              </FieldRow>
              <FieldRow>
                <TextareaField form={form} name="specialRequirements" label="Special requirements" />
                <TextareaField form={form} name="cateringRequirements" label="Catering" />
              </FieldRow>
              <TextareaField form={form} name="notes" label="Notes" />
              <div className="flex justify-end">
                <Button type="submit" disabled={saving}>
                  {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Save changes
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle className="text-base">The room that day</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <RoomDayBoard
                day={day}
                rooms={room ? [room] : [{ id: booking.roomId, roomName: booking.roomName, capacity: 0, locationName: '' }]}
                busy={others}
                proposed={
                  windowReady
                    ? { start: toIsoInstant(start) ?? '', end: toIsoInstant(end) ?? '', roomId: booking.roomId }
                    : null
                }
              />
              {room && <RoomRules room={room} />}
            </CardContent>
          </Card>
        </form>
      ) : (
        <Card>
          <CardHeader><CardTitle className="text-base">Details</CardTitle></CardHeader>
          <CardContent className="grid gap-6 sm:grid-cols-2">
            <Detail label="Purpose">{booking.purpose}</Detail>
            <Detail label="Expected attendees">{booking.expectedAttendees}</Detail>
            <Detail label="Special requirements">{booking.specialRequirements || '—'}</Detail>
            <Detail label="Catering">{booking.cateringRequirements || '—'}</Detail>
            <Detail label="Notes">{booking.notes || '—'}</Detail>
          </CardContent>
        </Card>
      )}

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this booking</DialogTitle>
            <DialogDescription>
              The room is released for someone else.
              {booking.status === 'Tentative' ? ' Its approval is withdrawn.' : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="reason">Why</Label>
            <Textarea id="reason" rows={3} value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>Keep it</Button>
            <Button
              variant="destructive"
              disabled={!cancelReason.trim() || cancel.isPending}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Cancel booking
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
