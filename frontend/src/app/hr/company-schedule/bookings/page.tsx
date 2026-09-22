'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarCheck, CheckCircle2, MoreHorizontal, Plus, Search, Trash2, XCircle } from 'lucide-react';
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { roomBookingService } from '@/services/hr/company-schedule.service';
import { BOOKING_STATUSES } from '@/types/hr/company-schedule';
import type { RoomBooking } from '@/types/hr/company-schedule';

const ALL = '__all__';
const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const when = (iso: string) => new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });

export default function RoomBookingsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // ⚠ Round 4, D7 (company-schedule defect C-1). Delete is gated on HR.Company.Admin server-side,
  // and the HR role holds Read, Write and Approve but NOT Admin — so this button was rendered, in
  // destructive red, for the very people it refuses. A screen that offers what it cannot do is
  // worse than one that offers less.
  //
  // ⚠ The button is hidden; the endpoint is NOT weakened. Whether HR may delete a company event is a
  // permission decision for TDC to make in role setup, not one to make by loosening a policy. The
  // two only have to agree about what is on offer.
  const { hasPermission } = useAuth();
  const canDelete = hasPermission('HR.Company.Admin');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ALL);
  const [cancelTarget, setCancelTarget] = useState<RoomBooking | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<RoomBooking | null>(null);

  const key = ['hr', 'company-schedule', 'bookings'];
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: () => roomBookingService.getAll() });

  const bookings = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (data ?? [])
      .filter((b) => (status === ALL ? true : b.status === status))
      .filter(
        (b) =>
          !term ||
          b.bookingNumber.toLowerCase().includes(term) ||
          b.roomName.toLowerCase().includes(term) ||
          b.purpose.toLowerCase().includes(term) ||
          b.bookedByName.toLowerCase().includes(term),
      )
      .sort((a, b) => b.startDateTime.localeCompare(a.startDateTime));
  }, [data, search, status]);

  const fail = (title: string) => (error: any) =>
    toast({
      title,
      description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const approve = async (b: RoomBooking) => {
    try {
      await roomBookingService.approve(b.id);
      await queryClient.invalidateQueries({ queryKey: key });
      toast({ title: 'Booking approved', description: `${b.bookingNumber} — ${b.roomName}.` });
    } catch (e) {
      fail('Could not approve the booking')(e);
    }
  };

  const doCancel = async () => {
    if (!cancelTarget) return false;
    try {
      await roomBookingService.cancel(cancelTarget.id, cancelReason.trim());
      await queryClient.invalidateQueries({ queryKey: key });
      toast({ title: 'Booking cancelled' });
      setCancelTarget(null);
      setCancelReason('');
      return true;
    } catch (e) {
      fail('Could not cancel the booking')(e);
      return false;
    }
  };

  const doDelete = async () => {
    if (!deleteTarget) return false;
    try {
      await roomBookingService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: key });
      toast({ title: 'Booking deleted' });
      setDeleteTarget(null);
      return true;
    } catch (e) {
      fail('Could not delete the booking')(e);
      return false;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Room bookings"
        description="Who has which room, when, and which bookings are still waiting on approval."
        backHref="/hr/company-schedule"
        actions={
          <Button onClick={() => router.push('/hr/company-schedule/bookings/new')}>
            <Plus className="mr-2 h-4 w-4" /> Book a room
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>Bookings</CardTitle>
            <div className="flex flex-wrap items-center gap-2">
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {BOOKING_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>{spaced(s)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <div className="relative w-64">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search bookings…"
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
                  <TableHead>Room</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Purpose</TableHead>
                  <TableHead>Seats</TableHead>
                  <TableHead>Booked by</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(9)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : bookings.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={9}>
                      <EmptyState
                        icon={CalendarCheck}
                        title={data?.length ? 'No matching bookings' : 'No bookings yet'}
                        description={
                          data?.length ? 'Try a different search or filter.' : 'Book a room to get started.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  bookings.map((b) => (
                    <TableRow
                      key={b.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/company-schedule/bookings/${b.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{b.bookingNumber}</TableCell>
                      <TableCell className="font-medium">{b.roomName}</TableCell>
                      <TableCell className="whitespace-nowrap">{when(b.startDateTime)}</TableCell>
                      <TableCell className="whitespace-nowrap">{when(b.endDateTime)}</TableCell>
                      <TableCell>{b.purpose}</TableCell>
                      <TableCell>{b.expectedAttendees}</TableCell>
                      <TableCell>{b.bookedByName}</TableCell>
                      <TableCell><StatusBadge status={spaced(b.status)} /></TableCell>
                      <TableCell onClick={(e) => e.stopPropagation()}>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon">
                              <MoreHorizontal className="h-4 w-4" />
                              <span className="sr-only">Actions</span>
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            {b.status === 'Tentative' && !b.isCancelled && (
                              <DropdownMenuItem onClick={() => approve(b)}>
                                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                              </DropdownMenuItem>
                            )}
                            {!b.isCancelled && b.status !== 'Completed' && (
                              <DropdownMenuItem onClick={() => setCancelTarget(b)}>
                                <XCircle className="mr-2 h-4 w-4" /> Cancel
                              </DropdownMenuItem>
                            )}
                            {canDelete && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem className="text-destructive" onClick={() => setDeleteTarget(b)}>
                                  <Trash2 className="mr-2 h-4 w-4" /> Delete
                                </DropdownMenuItem>
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={!!cancelTarget} onOpenChange={(open) => !open && setCancelTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this booking</DialogTitle>
            <DialogDescription>
              {cancelTarget?.roomName} — the slot is released for someone else.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="bookingCancelReason">Reason</Label>
            <Textarea
              id="bookingCancelReason"
              rows={3}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelTarget(null)}>Keep it</Button>
            <Button variant="destructive" disabled={!cancelReason.trim()} onClick={doCancel}>
              Cancel booking
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete this booking?"
        description="Cancelling keeps the record and the reason. Deleting removes it entirely."
        confirmText="Delete"
        variant="destructive"
        onConfirm={doDelete}
      />
    </div>
  );
}
