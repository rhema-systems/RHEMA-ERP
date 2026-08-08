'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, MoreHorizontal, Pencil, Trash2, Layers } from 'lucide-react';
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
import { Badge } from '@/components/ui/badge';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import type { OrganizationLevel } from '@/types/hr/organization';

const PAGE_SIZE = 20;

export default function OrganizationLevelsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [page, setPage] = useState(1);
  const [deleteTarget, setDeleteTarget] = useState<OrganizationLevel | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'organization-levels', page, PAGE_SIZE],
    queryFn: () => organizationLevelService.getPaged(page, PAGE_SIZE),
  });

  const levels = data?.items ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await organizationLevelService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-levels'] });
      toast({ title: 'Deleted', description: `"${deleteTarget.name}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete organization level.',
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
        title="Organization Levels"
        description="Configure the tiers of your organization hierarchy (e.g. Division, Department, Unit)."
        actions={
          <Button onClick={() => router.push('/administration/hr/organization/levels/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Level
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Levels</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead className="text-center">Level No.</TableHead>
                  <TableHead>Structure</TableHead>
                  <TableHead>Rules</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[40px] mx-auto" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[160px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : levels.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Layers}
                        title="No organization levels yet"
                        description="Create your first level to start building the hierarchy."
                        action={
                          <Button size="sm" onClick={() => router.push('/administration/hr/organization/levels/new')}>
                            <Plus className="mr-2 h-4 w-4" /> New Level
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
                      onClick={() => router.push(`/administration/hr/organization/levels/${level.id}/edit`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{level.name}</span>
                          {level.code && (
                            <span className="text-xs text-muted-foreground">{level.code}</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-center">
                        <div className="flex items-center justify-center gap-1">
                          {level.levelNumber}
                          {level.isRootLevel && (
                            <Badge variant="outline" className="text-[10px]">Root</Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {level.structureName ?? '—'}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {level.requiresHead && (
                            <Badge variant="secondary" className="text-[10px]">Requires head</Badge>
                          )}
                          {level.allowsDirectEmployees && (
                            <Badge variant="secondary" className="text-[10px]">Direct employees</Badge>
                          )}
                          {level.isLocked && (
                            <Badge variant="outline" className="text-[10px]">Locked</Badge>
                          )}
                        </div>
                      </TableCell>
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
                                router.push(`/administration/hr/organization/levels/${level.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              disabled={level.isLocked}
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

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-end space-x-2 py-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPrevious}
              >
                Previous
              </Button>
              <div className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNext}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete organization level"
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
