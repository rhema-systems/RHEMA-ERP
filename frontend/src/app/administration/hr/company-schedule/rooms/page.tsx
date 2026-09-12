'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { DoorOpen, MoreHorizontal, Pencil, Plus, Search, Trash2 } from 'lucide-react';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
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
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<MeetingRoom | null>(null);

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

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    try {
      await meetingRoomService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'rooms'] });
      toast({ title: 'Room removed', description: `"${deleteTarget.roomName}" is no longer listed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Could not remove the room',
        description: error?.response?.data?.detail ?? error?.message ?? 'It may have bookings against it.',
        variant: 'destructive',
      });
      return false;
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
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive"
                              onClick={() => setDeleteTarget(r)}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Delete
                            </DropdownMenuItem>
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

      <ConfirmationDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete this room?"
        description={`"${deleteTarget?.roomName}" will be removed. Deactivate it instead if it has booking history worth keeping.`}
        confirmText="Delete"
        variant="destructive"
        onConfirm={handleDelete}
      />
    </div>
  );
}
