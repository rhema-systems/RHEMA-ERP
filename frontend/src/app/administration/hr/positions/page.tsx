'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Pencil, Trash2, Briefcase } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { employeePositionService } from '@/services/hr/employee-position.service';
import type { EmployeePosition } from '@/types/hr/position';

export default function EmployeePositionsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  // Round 4, lane O — 'technician' narrows the list to the posts whose holders Maintenance may assign work to.
  const [roleFilter, setRoleFilter] = useState<'all' | 'technician'>('all');
  const [deleteTarget, setDeleteTarget] = useState<EmployeePosition | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
  });

  const positions = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = (data ?? []).filter((p) => roleFilter === 'all' || p.isTechnicianRole);
    if (!term) return all;
    return all.filter(
      (p) =>
        p.title.toLowerCase().includes(term) ||
        p.code.toLowerCase().includes(term) ||
        p.organizationUnitName.toLowerCase().includes(term),
    );
  }, [data, search, roleFilter]);
  const filtered = search.trim() !== '' || roleFilter !== 'all';

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await employeePositionService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employee-positions'] });
      toast({ title: 'Deleted', description: `"${deleteTarget.title}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete position.',
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
        title="Job Positions"
        description="Define the positions employees can hold, within your organization units."
        actions={
          <Button onClick={() => router.push('/administration/hr/positions/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Position
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Positions</CardTitle>
            <div className="flex items-center gap-2">
              <Select value={roleFilter} onValueChange={(v) => setRoleFilter(v as 'all' | 'technician')}>
                <SelectTrigger className="w-44" aria-label="Role filter">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All positions</SelectItem>
                  <SelectItem value="technician">Technician roles</SelectItem>
                </SelectContent>
              </Select>
              <div className="relative w-64">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search positions…"
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
                  <TableHead>Title</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Reports To</TableHead>
                  <TableHead className="text-center">Headcount</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[120px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[40px] mx-auto" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : positions.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Briefcase}
                        title={filtered ? 'No matching positions' : 'No positions yet'}
                        description={
                          filtered
                            ? roleFilter === 'technician' && !search.trim()
                              ? 'No position is marked as a technician role yet. Open one and switch on "Technician role".'
                              : 'Try a different search term or filter.'
                            : 'Create your first job position.'
                        }
                        action={
                          !filtered ? (
                            <Button size="sm" onClick={() => router.push('/administration/hr/positions/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Position
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  positions.map((position) => (
                    <TableRow
                      key={position.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/positions/${position.id}/edit`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="flex items-center gap-2 font-medium">
                            {position.title}
                            {position.isTechnicianRole && (
                              <Badge variant="secondary" className="text-[11px] font-medium">
                                Technician role
                              </Badge>
                            )}
                          </span>
                          {position.code && (
                            <span className="text-xs text-muted-foreground">{position.code}</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {position.organizationUnitName || '—'}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {position.reportsToPositionTitle ?? '—'}
                      </TableCell>
                      <TableCell className="text-center">{position.expectedHeadcount}</TableCell>
                      <TableCell>
                        <StatusBadge active={position.isActive} />
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
                                router.push(`/administration/hr/positions/${position.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(position);
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
        title="Delete position"
        description={
          deleteTarget
            ? `Are you sure you want to delete "${deleteTarget.title}"? This cannot be undone.`
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
