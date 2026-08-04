'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, MoreHorizontal, Pencil, Trash2, CalendarClock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
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
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { workScheduleService } from '@/services/hr/attendance-setup.service';
import { formatTime } from '@/lib/hr/attendance-format';
import type { WorkScheduleSummary } from '@/types/hr/attendance';

export default function WorkSchedulesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [deleteTarget, setDeleteTarget] = useState<WorkScheduleSummary | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'work-schedules'],
    queryFn: () => workScheduleService.getAll(),
  });

  const schedules = data ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await workScheduleService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'work-schedules'] });
      toast({ title: 'Deleted', description: `"${deleteTarget.scheduleName}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete work schedule.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Work Schedules"
        description="Standard hours, working days, breaks and overtime rules that attendance is measured against."
        backHref="/administration/hr/attendance"
        actions={
          <Button onClick={() => router.push('/administration/hr/attendance/work-schedules/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Schedule
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Schedules</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Hours</TableHead>
                  <TableHead className="text-right">Per day</TableHead>
                  <TableHead className="text-right">Per week</TableHead>
                  <TableHead className="text-right">Shifts</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : schedules.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={CalendarClock}
                        title="No work schedules yet"
                        description="Create a schedule before assigning employees or recording attendance."
                        action={
                          <Button
                            size="sm"
                            onClick={() =>
                              router.push('/administration/hr/attendance/work-schedules/new')
                            }
                          >
                            <Plus className="mr-2 h-4 w-4" /> New Schedule
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  schedules.map((s) => (
                    <TableRow
                      key={s.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() =>
                        router.push(`/administration/hr/attendance/work-schedules/${s.id}/edit`)
                      }
                    >
                      <TableCell className="font-medium">
                        <div className="flex items-center gap-2">
                          {s.scheduleName}
                          {s.isDefault && <Badge variant="outline">Default</Badge>}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{s.type}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {formatTime(s.standardStartTime)} – {formatTime(s.standardEndTime)}
                      </TableCell>
                      <TableCell className="text-right">{s.standardHoursPerDay}</TableCell>
                      <TableCell className="text-right">{s.standardHoursPerWeek}</TableCell>
                      <TableCell className="text-right">{s.shiftCount}</TableCell>
                      <TableCell>
                        <StatusBadge active={s.isActive} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(
                                  `/administration/hr/attendance/work-schedules/${s.id}/edit`,
                                );
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(s);
                              }}
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
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete work schedule"
        description={
          deleteTarget
            ? `Delete "${deleteTarget.scheduleName}"? Employees assigned to it will lose their schedule.`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDelete}
      />
    </div>
  );
}
