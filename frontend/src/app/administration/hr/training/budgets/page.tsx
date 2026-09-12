'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Trash2, CheckCircle2, Coins, Eye } from 'lucide-react';
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
import { trainingBudgetService } from '@/services/hr/training-budget.service';
import type { TrainingBudgetSummary } from '@/types/hr/training';

export default function TrainingBudgetsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<TrainingBudgetSummary | null>(null);
  const [approveTarget, setApproveTarget] = useState<TrainingBudgetSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'budgets'],
    queryFn: () => trainingBudgetService.getAll(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (b) =>
        b.budgetCode.toLowerCase().includes(term) || (b.organizationUnitName ?? '').toLowerCase().includes(term),
    );
  }, [data, search]);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'budgets'] });

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setBusy(true);
    try {
      await trainingBudgetService.remove(deleteTarget.id);
      await invalidate();
      toast({ title: 'Deleted', description: `Budget "${deleteTarget.budgetCode}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to delete budget.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const handleApprove = async () => {
    if (!approveTarget) return false;
    setBusy(true);
    try {
      await trainingBudgetService.approve(approveTarget.id);
      await invalidate();
      toast({ title: 'Approved', description: `Budget "${approveTarget.budgetCode}" was approved.` });
      setApproveTarget(null);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to approve budget.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Budgets"
        description="Allocated training spend by year/quarter and organization scope, with spend transactions."
        backHref="/administration/hr/training"
        actions={
          <Button onClick={() => router.push('/administration/hr/training/budgets/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Budget
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Budgets</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search by code or unit…"
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
                  <TableHead>Budget code</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Organization unit</TableHead>
                  <TableHead>Allocated</TableHead>
                  <TableHead>Spent</TableHead>
                  <TableHead>Remaining</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-[100px]" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={Coins}
                        title={search ? 'No matching budgets' : 'No training budgets yet'}
                        description={search ? 'Try a different search.' : 'Create your first training budget.'}
                        action={
                          !search ? (
                            <Button size="sm" onClick={() => router.push('/administration/hr/training/budgets/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Budget
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((b) => (
                    <TableRow
                      key={b.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/training/budgets/${b.id}`)}
                    >
                      <TableCell className="font-medium">{b.budgetCode}</TableCell>
                      <TableCell>{b.periodDescription}</TableCell>
                      <TableCell className="text-muted-foreground">{b.organizationUnitName || 'Company-wide'}</TableCell>
                      <TableCell>{b.currency} {b.allocatedAmount.toLocaleString()}</TableCell>
                      <TableCell>{b.currency} {b.spentAmount.toLocaleString()}</TableCell>
                      <TableCell>{b.currency} {b.remainingAmount.toLocaleString()}</TableCell>
                      <TableCell><StatusBadge status={b.status} /></TableCell>
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
                            {/* Always present: Approve and Delete are Draft-only, so without this the
                                menu on an approved or closed budget opened completely empty. */}
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/administration/hr/training/budgets/${b.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            {b.status === 'Draft' && (
                              <>
                                <DropdownMenuItem
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setApproveTarget(b);
                                  }}
                                >
                                  <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setDeleteTarget(b);
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
        title="Delete budget"
        description={
          deleteTarget ? `Are you sure you want to delete "${deleteTarget.budgetCode}"? This cannot be undone.` : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={handleDelete}
      />

      <ConfirmationDialog
        open={approveTarget !== null}
        onOpenChange={(open) => !open && setApproveTarget(null)}
        title="Approve budget?"
        description={approveTarget ? `"${approveTarget.budgetCode}" will be marked Approved.` : ''}
        confirmText="Approve"
        isLoading={busy}
        onConfirm={handleApprove}
      />
    </div>
  );
}
