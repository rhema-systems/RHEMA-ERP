'use client';

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { meetingRoomService } from '@/services/hr/company-schedule.service';
import type { RoomBookingSummary } from '@/types/hr/company-schedule';

/** What the person asked for: to deactivate the room, or to delete it. */
export type RoomRetireIntent = 'deactivate' | 'delete';

const when = (start: string, end: string) => {
  const s = new Date(start);
  const e = new Date(end);
  const day = s.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
  const time = (d: Date) => d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  return `${day}, ${time(s)}–${time(e)}`;
};

const plural = (n: number) => (n === 1 ? '1 booking' : `${n} bookings`);

/** A 403 says so; anything else is the server's own sentence. */
export const roomErrorText = (error: any, action: string) =>
  error?.status === 403
    ? `You do not have permission to ${action} rooms — it needs Company Admin.`
    : error?.message ?? 'Please try again.';

function FutureList({ bookings }: { bookings: RoomBookingSummary[] }) {
  return (
    <ul className="max-h-56 space-y-1 overflow-y-auto rounded-md border p-2 text-sm">
      {bookings.map((b) => (
        <li key={b.id} className="flex flex-wrap gap-x-2">
          <span className="font-mono text-xs">{b.bookingNumber}</span>
          <span>{when(b.startDateTime, b.endDateTime)}</span>
          <span className="text-muted-foreground">
            {b.bookedByName}
            {b.purpose ? ` — ${b.purpose}` : ''}
          </span>
        </li>
      ))}
    </ul>
  );
}

/**
 * Retiring a room (D-18, company-schedule lane 3a, the user's ruling): deactivating one lists its bookings still to
 * come and offers to cancel them; deleting one is possible only while it has no booking on record — otherwise it
 * offers to deactivate instead, so the room's history stays in the register.
 */
export function RoomRetireDialog({
  room,
  intent,
  onClose,
  deactivate,
  remove,
}: {
  room: { id: string; roomName: string } | null;
  intent: RoomRetireIntent | null;
  onClose: () => void;
  /** Saves the room switched off; `cancelFutureBookings` when the person agreed to cancel what is still to come. */
  deactivate: (cancelFutureBookings: boolean) => Promise<void>;
  remove?: () => Promise<void>;
}) {
  const open = !!room && !!intent;
  const [step, setStep] = useState<RoomRetireIntent | null>(intent);
  const [busy, setBusy] = useState(false);

  useEffect(() => setStep(intent), [intent, room?.id]);

  const { data, isLoading, error } = useQuery({
    queryKey: ['hr', 'company-schedule', 'rooms', room?.id, 'retirement'],
    queryFn: () => meetingRoomService.retirement(room?.id ?? ''),
    enabled: open,
    staleTime: 0,
  });

  const future = data?.futureBookings ?? [];
  const run = async (fn: () => Promise<void>) => {
    setBusy(true);
    try {
      await fn();
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && !busy && onClose()}>
      <DialogContent className="max-w-lg">
        {isLoading || !data ? (
          <div className="flex items-center gap-2 p-4 text-sm text-muted-foreground">
            {error ? 'Could not read the room’s bookings. Please try again.' : (
              <>
                <Loader2 className="h-4 w-4 animate-spin" /> Reading the room’s bookings…
              </>
            )}
          </div>
        ) : step === 'delete' && data.canDelete ? (
          <>
            <DialogHeader>
              <DialogTitle>Delete {data.roomName}?</DialogTitle>
              <DialogDescription>It has no bookings on record, so nothing is lost by deleting it.</DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline" onClick={onClose} disabled={busy}>Keep it</Button>
              <Button
                variant="destructive"
                disabled={busy || !remove}
                onClick={() => remove && run(remove)}
              >
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />} Delete
              </Button>
            </DialogFooter>
          </>
        ) : step === 'delete' ? (
          <>
            <DialogHeader>
              <DialogTitle>{data.roomName} cannot be deleted</DialogTitle>
              <DialogDescription>
                It has {plural(data.bookingsOnRecord)} on record, and the bookings register would lose them.
                {data.isActive ? ' Deactivate it instead: nobody can book it, and its history stays.' : ' It is already deactivated.'}
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline" onClick={onClose}>Close</Button>
              {data.isActive && <Button onClick={() => setStep('deactivate')}>Deactivate instead</Button>}
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>Deactivate {data.roomName}?</DialogTitle>
              <DialogDescription>
                {future.length === 0
                  ? 'Nobody will be able to book it. It has no bookings still to come.'
                  : `Nobody will be able to book it. It has ${plural(future.length)} still to come, which will be cancelled — the reason, that the room was taken out of use — and their bookers told:`}
              </DialogDescription>
            </DialogHeader>
            {future.length > 0 && <FutureList bookings={future} />}
            <DialogFooter>
              <Button variant="outline" onClick={onClose} disabled={busy}>Keep it in use</Button>
              <Button
                variant={future.length ? 'destructive' : 'default'}
                disabled={busy}
                onClick={() => run(() => deactivate(future.length > 0))}
              >
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {future.length ? `Cancel ${plural(future.length)} and deactivate` : 'Deactivate'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
