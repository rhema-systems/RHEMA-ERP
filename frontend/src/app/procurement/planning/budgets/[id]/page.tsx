'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Progress } from '@/components/ui/progress';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ArrowLeft, Edit, FilePenLine, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import {
  procurementBudgetService,
  type ProcurementBudgetDetailDto,
  type ProcurementBudgetRevisionDto,
} from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import {
  WorkflowApprovalActions,
  WorkflowTabContent,
  WorkflowTabTrigger,
  useWorkflowRecord,
} from '@/components/workflow';

function BudgetRevisionActions({
  revision,
  budgetCode,
  onChanged,
}: {
  revision: ProcurementBudgetRevisionDto;
  budgetCode: string;
  onChanged: () => Promise<void>;
}) {
  const router = useRouter();
  const isPending = ['pending', 'pendingapproval', 'pending approval']
    .includes((revision.status || '').trim().toLowerCase());
  const workflow = useWorkflowRecord({
    entityType: 'ProcurementBudgetRevision',
    entityId: revision.id,
    entityLabel: 'Procurement Budget Revision',
    entityNumber: `${budgetCode}/R${revision.revisionNumber}`,
    status: revision.status,
    canSubmit: false,
    canApproveReject: isPending,
    enabled: isPending,
    commands: {
      approve: async ({ comments }) => {
        await procurementBudgetService.approveRevision(revision.id, comments || undefined);
      },
      reject: async ({ comments }) => {
        await procurementBudgetService.rejectRevision(revision.id, comments);
      },
      afterAction: onChanged,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  if (!isPending) return null;
  return <WorkflowApprovalActions {...workflow.actionProps} size="sm" showStepBadge />;
}

export default function ProcurementBudgetDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const [budget, setBudget] = useState<ProcurementBudgetDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [revisionDialogOpen, setRevisionDialogOpen] = useState(false);
  const [revisionAmount, setRevisionAmount] = useState('');
  const [revisionReason, setRevisionReason] = useState('');
  const [revisionSubmitting, setRevisionSubmitting] = useState(false);

  useEffect(() => {
    if (id) loadBudget();
  }, [id]);

  const loadBudget = async () => {
    try {
      setLoading(true);
      const data = await procurementBudgetService.getBudgetById(id);
      setBudget(data);
    } catch (error) {
      console.error('Error loading budget:', error);
      toast.error('Failed to load budget details');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmitForApproval = async () => {
    await procurementBudgetService.submitBudget(id);
  };

  const openRevisionDialog = () => {
    if (!budget) return;
    setRevisionAmount(budget.allocatedAmount.toString());
    setRevisionReason('');
    setRevisionDialogOpen(true);
  };

  const handleCreateRevision = async () => {
    if (!budget) return;
    const newAmount = Number(revisionAmount);
    if (!Number.isFinite(newAmount) || newAmount <= 0) {
      toast.error('Enter a revised amount greater than zero');
      return;
    }
    if (newAmount === budget.allocatedAmount) {
      toast.error('The revised amount must differ from the current approved amount');
      return;
    }
    if (revisionReason.trim().length < 3) {
      toast.error('Enter a meaningful reason for the revision');
      return;
    }

    try {
      setRevisionSubmitting(true);
      await procurementBudgetService.createRevision(id, {
        revisionType: newAmount > budget.allocatedAmount ? 'Increase' : 'Decrease',
        newAmount,
        reason: revisionReason.trim(),
      });
      setRevisionDialogOpen(false);
      toast.success('Budget revision submitted for independent approval');
      await loadBudget();
    } catch (error: any) {
      toast.error(error?.message || 'Failed to create budget revision');
    } finally {
      setRevisionSubmitting(false);
    }
  };

  const workflow = useWorkflowRecord({
    entityType: 'ProcurementBudget',
    entityId: id,
    entityLabel: 'Procurement Budget',
    entityNumber: budget?.budgetCode,
    status: budget?.status || '',
    canSubmit: budget?.status === 'Draft',
    canApproveReject: budget?.status === 'Submitted' || budget?.status === 'UnderReview',
    enabled: Boolean(id && budget),
    commands: {
      submit: handleSubmitForApproval,
      approve: async ({ comments }) => {
        await procurementBudgetService.approveBudget(id, {
          isApproved: true,
          comments: comments || undefined,
        });
      },
      reject: async ({ comments }) => {
        await procurementBudgetService.approveBudget(id, {
          isApproved: false,
          comments: comments || undefined,
        });
      },
      afterAction: loadBudget,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Submitted': { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
      'UnderReview': { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
      'Approved': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Rejected': { variant: 'destructive', className: '' },
      'Active': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Frozen': { variant: 'outline', className: 'bg-blue-100 text-blue-800' },
      'Closed': { variant: 'outline', className: 'bg-purple-100 text-purple-800' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(amount);
  };

  if (loading) {
    return <div className="flex items-center justify-center h-96"><Loader2 className="h-8 w-8 animate-spin" /></div>;
  }
  if (!budget) {
    return <div className="text-center py-8 text-gray-500">Budget not found</div>;
  }

  const hasPendingRevision = (budget.revisions || []).some((revision) =>
    ['pending', 'pendingapproval', 'pending approval']
      .includes((revision.status || '').trim().toLowerCase()));
  const canRevise = ['approved', 'active'].includes((budget.status || '').trim().toLowerCase());
  const categoryAllocationTotal = (budget.allocations || [])
    .reduce((total, allocation) => total + allocation.allocatedAmount, 0);
  const minimumVisibleExposure = Math.max(
    budget.utilizedAmount + budget.committedAmount,
    categoryAllocationTotal,
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}><ArrowLeft className="h-5 w-5" /></Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold tracking-tight">{budget.budgetCode}</h1>
              {getStatusBadge(budget.status)}
            </div>
            <p className="text-muted-foreground">{budget.title}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {canRevise && (
            <Button
              variant="outline"
              onClick={openRevisionDialog}
              disabled={hasPendingRevision}
              className="gap-2"
              title={hasPendingRevision ? 'Complete the pending revision first' : 'Request a governed budget amount revision'}
            >
              <FilePenLine className="h-4 w-4" />Revise Budget
            </Button>
          )}
          {budget.status === 'Draft' && (
            <Button variant="outline" onClick={() => router.push(`/procurement/planning/budgets/${id}/edit`)} className="gap-2">
              <Edit className="h-4 w-4" />Edit
            </Button>
          )}
          <WorkflowApprovalActions {...workflow.actionProps} size="default" showStepBadge />
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Allocated</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold">{formatCurrency(budget.allocatedAmount, budget.currency)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Utilized</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold text-orange-600">{formatCurrency(budget.utilizedAmount, budget.currency)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Committed</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold text-blue-600">{formatCurrency(budget.committedAmount, budget.currency)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Remaining</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold text-green-600">{formatCurrency(budget.remainingAmount, budget.currency)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Utilization</CardTitle></CardHeader>
          <CardContent>
            <div className="flex items-center gap-2">
              <Progress value={budget.utilizationPercent} className="w-20" />
              <span className="text-2xl font-bold">{budget.utilizationPercent.toFixed(0)}%</span>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="details" className="w-full">
        <TabsList><TabsTrigger value="details">Details</TabsTrigger><TabsTrigger value="allocations">Allocations</TabsTrigger><TabsTrigger value="revisions">Revisions</TabsTrigger><WorkflowTabTrigger /></TabsList>
        
        <TabsContent value="details" className="space-y-4">
          <Card>
            <CardHeader><CardTitle>Budget Information</CardTitle></CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div><span className="text-sm text-muted-foreground">Department</span><p className="font-medium">{budget.departmentName || 'N/A'}</p></div>
                <div><span className="text-sm text-muted-foreground">Fiscal Year</span><p className="font-medium">{budget.fiscalYear}</p></div>
                <div><span className="text-sm text-muted-foreground">Control Level</span><p className="font-medium">{budget.controlLevel}</p></div>
                <div><span className="text-sm text-muted-foreground">Warning Threshold</span><p className="font-medium">{budget.warningThresholdPercent}%</p></div>
                <div><span className="text-sm text-muted-foreground">Effective Date</span><p className="font-medium">{budget.effectiveDate ? format(new Date(budget.effectiveDate), 'dd MMM yyyy') : 'N/A'}</p></div>
                <div><span className="text-sm text-muted-foreground">Expiry Date</span><p className="font-medium">{budget.expiryDate ? format(new Date(budget.expiryDate), 'dd MMM yyyy') : 'N/A'}</p></div>
              </div>
              {budget.notes && <div className="mt-4"><span className="text-sm text-muted-foreground">Notes</span><p className="mt-1">{budget.notes}</p></div>}
            </CardContent>
          </Card>
        </TabsContent>
        
        <TabsContent value="allocations">
          <Card>
            <CardHeader><CardTitle>Budget Allocations</CardTitle><CardDescription>Budget allocated by category</CardDescription></CardHeader>
            <CardContent>
              {budget.allocations && budget.allocations.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Category</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead>Allocated</TableHead>
                      <TableHead>Utilized</TableHead>
                      <TableHead>Remaining</TableHead>
                      <TableHead>Utilization</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {budget.allocations.map((alloc) => (
                      <TableRow key={alloc.id}>
                        <TableCell className="font-medium">{alloc.categoryName}</TableCell>
                        <TableCell>{alloc.categoryDescription || '-'}</TableCell>
                        <TableCell>{formatCurrency(alloc.allocatedAmount, budget.currency)}</TableCell>
                        <TableCell>{formatCurrency(alloc.utilizedAmount, budget.currency)}</TableCell>
                        <TableCell>{formatCurrency(alloc.remainingAmount, budget.currency)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Progress value={alloc.utilizationPercent} className="w-16" />
                            <span className="text-sm">{alloc.utilizationPercent.toFixed(0)}%</span>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <div className="text-center py-8 text-gray-500">No allocations defined for this budget</div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="revisions">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-4">
                <div><CardTitle>Budget Revisions</CardTitle><CardDescription>Governed history of requested and approved amount changes</CardDescription></div>
                {canRevise && (
                  <Button size="sm" onClick={openRevisionDialog} disabled={hasPendingRevision} className="gap-2">
                    <FilePenLine className="h-4 w-4" />New Revision
                  </Button>
                )}
              </div>
            </CardHeader>
            <CardContent className="overflow-x-auto">
              {budget.revisions && budget.revisions.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Rev #</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Previous Amount</TableHead>
                      <TableHead>New Amount</TableHead>
                      <TableHead>Change</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>Requested By</TableHead>
                      <TableHead>Approved By</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {budget.revisions.map((rev) => (
                      <TableRow key={rev.id}>
                        <TableCell className="font-medium">{rev.revisionNumber}</TableCell>
                        <TableCell>{rev.revisionType}</TableCell>
                        <TableCell>{formatCurrency(rev.previousAmount, budget.currency)}</TableCell>
                        <TableCell>{formatCurrency(rev.newAmount, budget.currency)}</TableCell>
                        <TableCell className={rev.changeAmount >= 0 ? 'text-green-600' : 'text-red-600'}>
                          {rev.changeAmount >= 0 ? '+' : ''}{formatCurrency(rev.changeAmount, budget.currency)}
                        </TableCell>
                        <TableCell>
                          <Badge variant={rev.status === 'Approved' ? 'default' : rev.status === 'Rejected' ? 'destructive' : 'secondary'}>
                            {rev.status}
                          </Badge>
                        </TableCell>
                        <TableCell className="min-w-64 whitespace-normal">{rev.reason || '-'}</TableCell>
                        <TableCell>{rev.requestedByName || '-'}</TableCell>
                        <TableCell>{rev.approvedByName || '-'}</TableCell>
                        <TableCell>{format(new Date(rev.createdAt), 'dd MMM yyyy')}</TableCell>
                        <TableCell className="text-right">
                          <BudgetRevisionActions
                            revision={rev}
                            budgetCode={budget.budgetCode}
                            onChanged={loadBudget}
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <div className="text-center py-8 text-gray-500">No revisions for this budget</div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          {...workflow.actionProps}
          entityType="ProcurementBudget"
          showActions
        />
      </Tabs>

      <Dialog open={revisionDialogOpen} onOpenChange={setRevisionDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Revise Approved Budget</DialogTitle>
            <DialogDescription>
              The current approved value remains effective until this revision completes the configured workflow.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4 rounded-md border p-3 text-sm">
              <div><span className="text-muted-foreground">Current approved amount</span><p className="font-semibold">{formatCurrency(budget.allocatedAmount, budget.currency)}</p></div>
              <div><span className="text-muted-foreground">Visible minimum exposure</span><p className="font-semibold">{formatCurrency(minimumVisibleExposure, budget.currency)}</p></div>
              <div><span className="text-muted-foreground">Utilized + committed</span><p>{formatCurrency(budget.utilizedAmount + budget.committedAmount, budget.currency)}</p></div>
              <div><span className="text-muted-foreground">Category allocations</span><p>{formatCurrency(categoryAllocationTotal, budget.currency)}</p></div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="revised-budget-amount">New approved amount ({budget.currency})</Label>
              <Input
                id="revised-budget-amount"
                type="number"
                min="0.01"
                step="0.01"
                value={revisionAmount}
                onChange={(event) => setRevisionAmount(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="budget-revision-reason">Reason</Label>
              <Textarea
                id="budget-revision-reason"
                value={revisionReason}
                onChange={(event) => setRevisionReason(event.target.value)}
                placeholder="Explain why the approved budget must change"
                maxLength={2000}
                rows={4}
              />
            </div>
            <p className="text-xs text-muted-foreground">
              The server also checks linked procurement-plan exposure before accepting a decrease.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRevisionDialogOpen(false)} disabled={revisionSubmitting}>Cancel</Button>
            <Button onClick={handleCreateRevision} disabled={revisionSubmitting}>
              {revisionSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Submit Revision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}
