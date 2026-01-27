'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Progress } from '@/components/ui/progress';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ArrowLeft, Edit, CheckCircle, DollarSign, TrendingUp, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { procurementBudgetService, type ProcurementBudgetDetailDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';

export default function ProcurementBudgetDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;
  const [budget, setBudget] = useState<ProcurementBudgetDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [approving, setApproving] = useState(false);

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

  const handleApprove = async () => {
    if (!confirm('Are you sure you want to approve this budget? This action will make it active.')) return;
    try {
      setApproving(true);
      await procurementBudgetService.approveBudget(id);
      toast.success('Budget approved successfully');
      loadBudget();
    } catch (error) {
      console.error('Error approving budget:', error);
      toast.error('Failed to approve budget');
    } finally {
      setApproving(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
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
          {budget.status === 'Draft' && (
            <>
              <Button variant="outline" onClick={() => router.push(`/procurement/planning/budgets/${id}/edit`)} className="gap-2">
                <Edit className="h-4 w-4" />Edit
              </Button>
              <Button onClick={handleApprove} disabled={approving} className="gap-2">
                <CheckCircle className="h-4 w-4" />{approving ? 'Approving...' : 'Approve'}
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Allocated</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold">{formatCurrency(budget.allocatedAmount, budget.currency)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Utilized</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold text-orange-600">{formatCurrency(budget.utilizedAmount, budget.currency)}</div></CardContent>
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
        <TabsList><TabsTrigger value="details">Details</TabsTrigger><TabsTrigger value="allocations">Allocations</TabsTrigger><TabsTrigger value="revisions">Revisions</TabsTrigger></TabsList>
        
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
            <CardHeader><CardTitle>Budget Revisions</CardTitle><CardDescription>History of budget changes</CardDescription></CardHeader>
            <CardContent>
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
                      <TableHead>Date</TableHead>
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
                        <TableCell>{format(new Date(rev.createdAt), 'dd MMM yyyy')}</TableCell>
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
      </Tabs>
    </div>
  );
}

