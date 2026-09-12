'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Trash2, Send, CalendarRange, Eye } from 'lucide-react';
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
import { trainingPlanService } from '@/services/hr/training-plan.service';
import type { TrainingPlanSummary } from '@/types/hr/training';

export default function TrainingPlansPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<TrainingPlanSummary | null>(null);
  const [submitTarget, setSubmitTarget] = useState<TrainingPlanSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'plans'],
    queryFn: () => trainingPlanService.getAll(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (p) =>
        p.planNumber.toLowerCase().includes(term) ||
        (p.organizationUnitName ?? '').toLowerCase().includes(term),
    );
  }, [data, search]);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'plans'] });

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setBusy(true);
    try {
      await trainingPlanService.remove(deleteTarget.id);
      await invalidate();
      toast({ title: 'Deleted', description: `Plan "${deleteTarget.planNumber}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to delete plan.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const handleSubmit = async () => {
    if (!submitTarget) return false;
    setBusy(true);
    try {
      await trainingPlanService.submit(submitTarget.id);
      await invalidate();
      toast({ title: 'Submitted', description: `Plan "${submitTarget.planNumber}" was submitted for approval.` });
      setSubmitTarget(null);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to submit plan.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Plans"
        description="Annual or quarterly training plans by organization scope, with items and budget lines."
        backHref="/administration/hr/training"
        actions={
          <Button onClick={() => router.push('/administration/hr/training/plans/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Plan
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Plans</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search by plan number or unit…"
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
                  <TableHead>Plan number</TableHead>
                  <TableHead>Year</TableHead>
                  <TableHead>Organization unit</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Items</TableHead>
                  <TableHead>Completion</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-[100px]" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={CalendarRange}
                        title={search ? 'No matching plans' : 'No training plans yet'}
                        description={search ? 'Try a different search.' : 'Create your first training plan.'}
                        action={
                          !search ? (
                            <Button size="sm" onClick={() => router.push('/administration/hr/training/plans/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Plan
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((p) => (
                    <TableRow
                      key={p.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/training/plans/${p.id}`)}
                    >
                      <TableCell className="font-medium">{p.planNumber}</TableCell>
                      <TableCell>{p.year}</TableCell>
                      <TableCell className="text-muted-foreground">{p.organizationUnitName || 'Company-wide'}</TableCell>
                      <TableCell><StatusBadge status={p.status} /></TableCell>
                      <TableCell>{p.completedItemsCount} / {p.totalItemsCount}</TableCell>
                      <TableCell>
                        {p.totalItemsCount > 0
                          ? `${Math.round((p.completedItemsCount / p.totalItemsCount) * 100)}%`
                          : '—'}
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" className="h-8 w-8 p-0" onClick={(e) => e.stopPropagation()}>
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            {/* Always present: Submit and Delete are Draft-only, so without this the
                                menu on an approved or completed plan opened completely empty. */}
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/administration/hr/training/plans/${p.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            {p.status === 'Draft' && (
                              <>
                                <DropdownMenuItem
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setSubmitTarget(p);
                                  }}
                                >
                                  <Send className="mr-2 h-4 w-4" /> Submit for approval
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setDeleteTarget(p);
                                  }}
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

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete plan"
        description={
          deleteTarget ? `Are you sure you want to delete "${deleteTarget.planNumber}"? This cannot be undone.` : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={handleDelete}
      />

      <ConfirmationDialog
        open={submitTarget !== null}
        onOpenChange={(open) => !open && setSubmitTarget(null)}
        title="Submit plan for approval?"
        description={submitTarget ? `"${submitTarget.planNumber}" will move to Pending Approval.` : ''}
        confirmText="Submit"
        isLoading={busy}
        onConfirm={handleSubmit}
      />
    </div>
  );
}
