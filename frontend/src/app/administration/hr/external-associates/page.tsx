'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  MoreHorizontal,
  Pencil,
  Plus,
  Power,
  PowerOff,
  Search,
  Trash2,
  UserRoundCheck,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { externalAssociateService } from '@/services/hr/external-associate.service';
import type { ExternalAssociateSummary } from '@/types/hr/external-associate';

const PAGE_SIZE = 20;
type ActiveFilter = 'all' | 'active' | 'inactive';

/**
 * The external-associate register — eleven endpoints that, before slice 8, nothing had ever called.
 *
 * The filter row is server-side on purpose. `searchTerm` and `isActive` both go to `…/paged`, and
 * `isActive` did not exist until this slice: the register has an activate/deactivate pair and no
 * way to page the inactive half, so "show me the ones we have retired" was unanswerable.
 */
export default function ExternalAssociatesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('all');
  const [page, setPage] = useState(1);
  const [deleteTarget, setDeleteTarget] = useState<ExternalAssociateSummary | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  const isActive = useMemo(
    () => (activeFilter === 'all' ? undefined : activeFilter === 'active'),
    [activeFilter],
  );

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'external-associates', 'paged', page, search, activeFilter],
    queryFn: () =>
      externalAssociateService.getPaged({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        searchTerm: search.trim() || undefined,
        isActive,
      }),
  });

  const rows = data?.items ?? [];
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'external-associates'] });

  const setFilter = (next: ActiveFilter) => {
    setActiveFilter(next);
    setPage(1);
  };

  const toggleActive = async (row: ExternalAssociateSummary) => {
    setBusyId(row.id);
    try {
      if (row.isActive) {
        await externalAssociateService.deactivate(row.id);
        toast({
          title: 'Deactivated',
          description: `${row.fullName} will no longer appear in the interview panel picker.`,
        });
      } else {
        await externalAssociateService.activate(row.id);
        toast({ title: 'Activated', description: `${row.fullName} is back in the panel picker.` });
      }
      await invalidate();
    } catch (error) {
      toast({
        title: 'Could not change their status',
        description: (error as Error)?.message || 'The change was not saved.',
        variant: 'destructive',
      });
    } finally {
      setBusyId(null);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await externalAssociateService.remove(deleteTarget.id);
      await invalidate();
      toast({ title: 'Deleted', description: `${deleteTarget.fullName} was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error) {
      // The server refuses an associate who sits on an interview panel and names how many. That
      // sentence is more useful than anything this screen could invent, so it is what is shown.
      toast({
        title: 'Could not delete this associate',
        description: (error as Error)?.message || 'Failed to delete the associate.',
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
        title="External Associates"
        description="People who act for the organisation without an ERP login — interview panellists, technical assessors and advisers."
        actions={
          <Button onClick={() => router.push('/administration/hr/external-associates/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Associate
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Register</CardTitle>
          <CardDescription>
            The recruitment panel picker draws from the <strong>active</strong> associates here. An
            associate who has sat on a panel cannot be deleted — deactivate them instead, and the
            interviews keep their record.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative min-w-[260px] flex-1">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder="Search name, email, organisation or EXT number…"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
            </div>
            <Select value={activeFilter} onValueChange={(v) => setFilter(v as ActiveFilter)}>
              <SelectTrigger className="w-[180px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All associates</SelectItem>
                <SelectItem value="active">Active only</SelectItem>
                <SelectItem value="inactive">Inactive only</SelectItem>
              </SelectContent>
            </Select>
            <span className="text-sm text-muted-foreground">
              {data ? `${data.totalCount} matching` : ''}
            </span>
          </div>

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[120px]">Number</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Organisation</TableHead>
                  <TableHead>Contact</TableHead>
                  <TableHead className="w-[110px]">Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-full" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={UserRoundCheck}
                        title={
                          search || activeFilter !== 'all'
                            ? 'Nothing matches those filters'
                            : 'No external associates yet'
                        }
                        description={
                          search || activeFilter !== 'all'
                            ? 'Clear the search or the status filter to see the whole register.'
                            : 'Add the assessors, panellists and advisers you bring in from outside, so they can be put on an interview panel.'
                        }
                        action={
                          search || activeFilter !== 'all' ? undefined : (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/external-associates/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Associate
                            </Button>
                          )
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((row) => (
                    <TableRow
                      key={row.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/external-associates/${row.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{row.associateNumber}</TableCell>
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{row.fullName}</span>
                          {row.role && (
                            <span className="text-xs text-muted-foreground">{row.role}</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-sm">{row.companyName || '—'}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        <div className="flex flex-col">
                          <span className="text-xs">{row.email}</span>
                          <span className="text-xs">{row.phoneNumber}</span>
                        </div>
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={row.isActive} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              disabled={busyId === row.id}
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
                                router.push(`/administration/hr/external-associates/${row.id}`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Open
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                void toggleActive(row);
                              }}
                            >
                              {row.isActive ? (
                                <>
                                  <PowerOff className="mr-2 h-4 w-4" /> Deactivate
                                </>
                              ) : (
                                <>
                                  <Power className="mr-2 h-4 w-4" /> Activate
                                </>
                              )}
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(row);
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
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages}
              </span>
              <div className="space-x-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete associate"
        description={
          deleteTarget
            ? `Are you sure you want to delete ${deleteTarget.fullName}? If they sit on any interview panel this will be refused — deactivate them instead.`
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
