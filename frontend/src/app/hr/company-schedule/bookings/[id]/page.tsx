'use client';

import { use, useEffect, useState, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CheckCircle2, Loader2, Save, Trash2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useAuth } from '@/hooks/use-auth';
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
import { roomBookingService } from '@/services/hr/company-schedule.service';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
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
 * One room booking: its detail, an in-place edit, and approve/cancel.
 *
 * ⚠ **The room cannot be changed here.** `UpdateRoomBookingDto` has no `roomId` — moving a booking
 * to another room means cancelling and re-booking, which is also the only way the availability
 * check gets re-run. The field is therefore not rendered rather than rendered and ignored.
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

export default function RoomBookingDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // Lane 3a (C-32): Delete here too, beside the register's — Admin only, hidden otherwise. And the booker is never offered
  // Approve on their own booking: the server refuses it (D-10's guard).
  const { user, hasPermission } = useAuth();
  const canDelete = hasPermission('HR.Company.Admin');
  const myEmployeeId = (user?.employeeId as string | undefined) ?? null;
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const key = ['hr', 'company-schedule', 'bookings', id];
  const { data: booking, isLoading } = useQuery({
    queryKey: key,
    queryFn: () => roomBookingService.getById(id),
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

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: key }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'bookings'] }),
    ]);

  const fail = (title: string) => (error: any) =>
    toast({
      title,
      description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const approve = useMutation({
    mutationFn: () => roomBookingService.approve(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Booking approved' });
    },
    onError: fail('Could not approve the booking'),
  });

  const cancel = useMutation({
    mutationFn: () => roomBookingService.cancel(id, cancelReason.trim()),
    onSuccess: async () => {
      await refresh();
      setCancelOpen(false);
      setCancelReason('');
      toast({ title: 'Booking cancelled' });
    },
    onError: fail('Could not cancel the booking'),
  });

  const remove = async () => {
    try {
      await roomBookingService.remove(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'bookings'] });
      toast({ title: 'Booking deleted' });
      router.push('/hr/company-schedule/bookings');
      return true;
    } catch (error: any) {
      fail('Could not delete the booking')(error);
      return false;
    }
  };

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      await roomBookingService.update(id, {
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
      toast({ title: 'Booking updated' });
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
    return <div className="p-6 text-muted-foreground">That booking could not be found.</div>;
  }

  // Lane 3a (F-8): a booking may be changed or cancelled while it holds its room — not once cancelled, completed or
  // marked a no-show — as the server now rules.
  const open = !booking.isCancelled && (booking.status === 'Tentative' || booking.status === 'Confirmed');
  const mine = !!myEmployeeId && booking.bookedById?.toLowerCase() === myEmployeeId.toLowerCase();

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={booking.roomName}
        description={`${booking.bookingNumber} · booked by ${booking.bookedByName}`}
        backHref="/hr/company-schedule/bookings"
        actions={
          <div className="flex items-center gap-2">
            {booking.status === 'Tentative' && open && !mine && (
              <Button variant="outline" onClick={() => approve.mutate()} disabled={approve.isPending}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
            {open && (
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <XCircle className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
            {canDelete && (
              <Button variant="outline" className="text-destructive" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="mr-2 h-4 w-4" /> Delete
              </Button>
            )}
          </div>
        }
      />
      {booking.status === 'Tentative' && open && mine && (
        <p className="text-sm text-muted-foreground">You booked this, so somebody else must approve it.</p>
      )}

      <Card>
        <CardHeader><CardTitle>Booking</CardTitle></CardHeader>
        <CardContent className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <Detail label="Status"><StatusBadge status={spaced(booking.status)} /></Detail>
          <Detail label="Booked on">{when(booking.bookingDate)}</Detail>
          <Detail label="From">{when(booking.startDateTime)}</Detail>
          <Detail label="To">{when(booking.endDateTime)}</Detail>
          <Detail label="Linked event">{booking.eventName || '—'}</Detail>
          <Detail label="Approved by">
            {booking.approvedByName ? `${booking.approvedByName} · ${when(booking.approvalDate)}` : '—'}
          </Detail>
          {booking.isCancelled && (
            <Detail label="Cancelled">
              {when(booking.cancellationDate)} — {booking.cancellationReason}
            </Detail>
          )}
        </CardContent>
      </Card>

      {open ? (
        <form onSubmit={onSubmit}>
          <Card>
            <CardHeader>
              <CardTitle>Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-xs text-muted-foreground">
                To move this to a different room, cancel it and book again — that is the only way the
                availability check runs against the new room.
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
        </form>
      ) : (
        <Card>
          <CardHeader><CardTitle>Details</CardTitle></CardHeader>
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
            <DialogDescription>The slot is released for someone else.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="reason">Reason</Label>
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

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete this booking?"
        description={`${booking.bookingNumber} will be removed from the register. Cancel it instead to keep it on record.`}
        confirmText="Delete"
        variant="destructive"
        onConfirm={remove}
      />
    </div>
  );
}
