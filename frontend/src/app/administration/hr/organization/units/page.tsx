'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Plus,
  MoreHorizontal,
  Pencil,
  Trash2,
  Building2,
  ArrowRightLeft,
  UserCog,
} from 'lucide-react';
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
import {
  ChangeUnitHeadDialog,
  MoveUnitDialog,
} from '@/components/hr/organization/UnitRestructureDialogs';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationUnit } from '@/types/hr/organization';

const PAGE_SIZE = 20;

export default function OrganizationUnitsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [page, setPage] = useState(1);
  const [deleteTarget, setDeleteTarget] = useState<OrganizationUnit | null>(null);
  const [deleting, setDeleting] = useState(false);
  // ⚠ A restructure and a change of leadership are separate acts on separate effective-dated
  // series of the change log, so they are separate dialogs — the edit form can perform both but
  // only carries one reason box between them.
  const [moveTarget, setMoveTarget] = useState<OrganizationUnit | null>(null);
  const [headTarget, setHeadTarget] = useState<OrganizationUnit | null>(null);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'organization-units'] });

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'organization-units', page, PAGE_SIZE],
    queryFn: () => organizationUnitService.getPaged(page, PAGE_SIZE),
  });

  const units = data?.items ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await organizationUnitService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-units'] });
      toast({ title: 'Deleted', description: `"${deleteTarget.name}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete organization unit.',
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
        title="Organization Units"
        description="Manage the nodes of your organization hierarchy and how they roll up."
        actions={
          <Button onClick={() => router.push('/administration/hr/organization/units/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Unit
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Units</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Level</TableHead>
                  <TableHead>Parent</TableHead>
                  <TableHead>Head</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : units.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Building2}
                        title="No organization units yet"
                        description="Create your first unit — start with the root, then add children."
                        action={
                          <Button size="sm" onClick={() => router.push('/administration/hr/organization/units/new')}>
                            <Plus className="mr-2 h-4 w-4" /> New Unit
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  units.map((unit) => (
                    <TableRow
                      key={unit.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/organization/units/${unit.id}/edit`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{unit.name}</span>
                          {unit.code && (
                            <span className="text-xs text-muted-foreground">{unit.code}</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{unit.levelName ?? '—'}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {unit.parentUnitName ?? '—'}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {unit.headEmployeeName ?? '—'}
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={unit.isActive} />
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
                                router.push(`/administration/hr/organization/units/${unit.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                setHeadTarget(unit);
                              }}
                            >
                              <UserCog className="mr-2 h-4 w-4" />
                              {unit.headEmployeeId ? 'Change head' : 'Appoint head'}
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                setMoveTarget(unit);
                              }}
                            >
                              <ArrowRightLeft className="mr-2 h-4 w-4" /> Move unit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(unit);
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
        title="Delete organization unit"
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

      {moveTarget && (
        <MoveUnitDialog
          unit={moveTarget}
          open
          onOpenChange={(open) => !open && setMoveTarget(null)}
          onDone={refresh}
        />
      )}

      {headTarget && (
        <ChangeUnitHeadDialog
          unit={headTarget}
          open
          onOpenChange={(open) => !open && setHeadTarget(null)}
          onDone={refresh}
        />
      )}
    </div>
  );
}
