'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { DoorOpen, MoreHorizontal, Pencil, Plus, PowerOff, Search, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { roomToForm, toRoomPayload } from '@/components/hr/company-schedule/RoomForm';
import {
  RoomRetireDialog,
  roomErrorText,
  type RoomRetireIntent,
} from '@/components/hr/company-schedule/RoomRetireDialog';
import { meetingRoomService } from '@/services/hr/company-schedule.service';
import type { MeetingRoom } from '@/types/hr/company-schedule';

const facilities = (r: MeetingRoom) =>
  [
    r.hasProjector && 'Projector',
    r.hasWhiteboard && 'Whiteboard',
    r.hasVideoConference && 'VC',
    r.hasAudioSystem && 'Audio',
    r.hasAirConditioning && 'A/C',
  ].filter(Boolean) as string[];

export default function MeetingRoomsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // ⚠ Lane 3a (C-33): Delete is HR.Company.Admin server-side, and the HR role does not hold it — the item was offered
  // to the people it refuses, and the refusal's toast blamed bookings. Hidden here; a 403 still says what it is.
  const { hasPermission } = useAuth();
  const canDelete = hasPermission('HR.Company.Admin');
  const [search, setSearch] = useState('');
  // D-18 (lane 3a): deactivating lists the bookings still to come and offers to cancel them; deleting is possible only
  // with no booking on record, and otherwise offers to deactivate instead.
  const [retire, setRetire] = useState<{ room: MeetingRoom; intent: RoomRetireIntent } | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'company-schedule', 'rooms'],
    queryFn: () => meetingRoomService.getAll(),
  });

  const rooms = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (data ?? [])
      .filter(
        (r) =>
          !term ||
          r.roomName.toLowerCase().includes(term) ||
          r.roomCode.toLowerCase().includes(term) ||
          (r.locationName ?? '').toLowerCase().includes(term) ||
          r.location.toLowerCase().includes(term),
      )
      .sort((a, b) => a.roomName.localeCompare(b.roomName));
  }, [data, search]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule'] });

  const deactivate = async (cancelFutureBookings: boolean) => {
    if (!retire) return;
    const { room } = retire;
    try {
      await meetingRoomService.update(room.id, {
        id: room.id,
        ...toRoomPayload(roomToForm(room)),
        isActive: false,
        cancelFutureBookings,
      });
      await refresh();
      toast({
        title: 'Room deactivated',
        description: cancelFutureBookings
          ? `"${room.roomName}" can no longer be booked; its bookings still to come were cancelled and their bookers told.`
          : `"${room.roomName}" can no longer be booked.`,
      });
      setRetire(null);
    } catch (error: any) {
      toast({ title: 'Could not deactivate the room', description: roomErrorText(error, 'change'), variant: 'destructive' });
    }
  };

  const handleDelete = async () => {
    if (!retire) return;
    const { room } = retire;
    try {
      await meetingRoomService.remove(room.id);
      await refresh();
      toast({ title: 'Room removed', description: `"${room.roomName}" is no longer listed.` });
      setRetire(null);
    } catch (error: any) {
      toast({ title: 'Could not remove the room', description: roomErrorText(error, 'delete'), variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Meeting rooms"
        description="The rooms people can book, and the rules for booking them."
        backHref="/administration/hr/company-schedule"
        actions={
          <Button onClick={() => router.push('/administration/hr/company-schedule/rooms/new')}>
            <Plus className="mr-2 h-4 w-4" /> New room
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Rooms</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search rooms…"
                className="pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Room</TableHead>
                  <TableHead>Site</TableHead>
                  <TableHead>Where</TableHead>
                  <TableHead>Seats</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Facilities</TableHead>
                  <TableHead>Bookable</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(10)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rooms.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={10}>
                      <EmptyState
                        icon={DoorOpen}
                        title={data?.length ? 'No matching rooms' : 'No meeting rooms yet'}
                        description={
                          data?.length
                            ? 'Try a different search.'
                            : 'Add the rooms people can book. Each one belongs to a site.'
                        }
                        action={
                          !data?.length ? (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/company-schedule/rooms/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New room
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rooms.map((r) => (
                    <TableRow key={r.id} className="hover:bg-muted/50">
                      <TableCell className="font-mono text-xs">{r.roomCode || '—'}</TableCell>
                      <TableCell className="font-medium">{r.roomName}</TableCell>
                      <TableCell>{r.locationName || '—'}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {[r.building, r.floor, r.location].filter(Boolean).join(' · ')}
                      </TableCell>
                      <TableCell>{r.capacity}</TableCell>
                      <TableCell>{r.type}</TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {facilities(r).length === 0 ? (
                            <span className="text-muted-foreground">—</span>
                          ) : (
                            facilities(r).map((f) => (
                              <Badge key={f} variant="secondary" className="text-[10px]">{f}</Badge>
                            ))
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        {r.isBookable ? (r.requiresApproval ? 'With approval' : 'Yes') : 'No'}
                      </TableCell>
                      <TableCell><StatusBadge active={r.isActive} /></TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon">
                              <MoreHorizontal className="h-4 w-4" />
                              <span className="sr-only">Actions</span>
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onClick={() =>
                                router.push(`/administration/hr/company-schedule/rooms/${r.id}/edit`)
                              }
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            {r.isActive && (
                              <DropdownMenuItem onClick={() => setRetire({ room: r, intent: 'deactivate' })}>
                                <PowerOff className="mr-2 h-4 w-4" /> Deactivate
                              </DropdownMenuItem>
                            )}
                            {canDelete && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive"
                                  onClick={() => setRetire({ room: r, intent: 'delete' })}
                                >
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

      <RoomRetireDialog
        room={retire?.room ?? null}
        intent={retire?.intent ?? null}
        onClose={() => setRetire(null)}
        deactivate={deactivate}
        remove={canDelete ? handleDelete : undefined}
      />
    </div>
  );
}
