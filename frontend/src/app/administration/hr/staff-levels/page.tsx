'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, MoreHorizontal, Pencil, Trash2, Power, PowerOff, Layers } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import { staffLevelService } from '@/services/hr/staff-level.service';
import type { StaffLevelListItem } from '@/types/hr/staff-level';

export default function StaffLevelsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [deleteTarget, setDeleteTarget] = useState<StaffLevelListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'staff-levels'],
    queryFn: () => staffLevelService.getAll(),
  });

  const levels = data ?? [];

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'staff-levels'] });

  const toggleActive = async (level: StaffLevelListItem) => {
    try {
      if (level.isActive) await staffLevelService.deactivate(level.id);
      else await staffLevelService.activate(level.id);
      await invalidate();
      toast({ title: 'Done', description: `"${level.name}" ${level.isActive ? 'deactivated' : 'activated'}.` });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Action failed.', variant: 'destructive' });
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await staffLevelService.remove(deleteTarget.id);
      await invalidate();
      toast({ title: 'Deleted', description: `"${deleteTarget.name}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete staff level.',
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
        title="Staff Levels"
        description="Ranked classifications used to grade employees and positions."
        actions={
          <Button onClick={() => router.push('/administration/hr/staff-levels/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Staff Level
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Staff Levels</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[80px] text-center">Rank</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[40px] mx-auto" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[80px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : levels.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Layers}
                        title="No staff levels yet"
                        description="Create your first staff level classification."
                        action={
                          <Button size="sm" onClick={() => router.push('/administration/hr/staff-levels/new')}>
                            <Plus className="mr-2 h-4 w-4" /> New Staff Level
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  levels.map((level) => (
                    <TableRow
                      key={level.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/staff-levels/${level.id}/edit`)}
                    >
                      <TableCell className="text-center font-medium">{level.rank}</TableCell>
                      <TableCell className="font-medium">{level.name}</TableCell>
                      <TableCell className="text-muted-foreground">{level.code || '—'}</TableCell>
                      <TableCell>
                        <StatusBadge active={level.isActive} />
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
                                router.push(`/administration/hr/staff-levels/${level.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                toggleActive(level);
                              }}
                            >
                              {level.isActive ? (
                                <><PowerOff className="mr-2 h-4 w-4" /> Deactivate</>
                              ) : (
                                <><Power className="mr-2 h-4 w-4" /> Activate</>
                              )}
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(level);
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
        title="Delete staff level"
        description={
          deleteTarget
            ? `Are you sure you want to delete "${deleteTarget.name}"? This cannot be undone.`
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
