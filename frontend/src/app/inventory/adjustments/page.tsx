'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Plus, Search, Eye, ClipboardList, CheckCircle,
  Clock, XCircle, Pencil, ArrowUpCircle, FileCheck, Send, RotateCcw
} from 'lucide-react';
import {
  stockAdjustmentService,
  StockAdjustmentDto,
  StockAdjustmentReasonCodes,
  StockAdjustmentStatusColors
} from '@/services/stockAdjustmentService';
import { InventoryReceiptDialog } from '@/components/inventory/InventoryReceiptDialog';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';
import { currencyService } from '@/services/financeCommonService';
import { formatInventoryMoney, normalizeInventoryCurrency } from '@/lib/inventory-currency';
import { useAuth } from '@/hooks/use-auth';
import { canDecideInventoryRecord, canPostInventoryRecord } from '@/lib/inventory-approval-actions';

export default function StockAdjustmentsPage() {
  const { toast } = useToast();
  const { user, hasPermission } = useAuth();
  const [receipts, setReceipts] = useState<StockAdjustmentDto[]>([]);
  const [filteredReceipts, setFilteredReceipts] = useState<StockAdjustmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [reasonFilter, setReasonFilter] = useState('all');
  const [selectedReceipt, setSelectedReceipt] = useState<StockAdjustmentDto | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit' | 'view'>('create');

  // Confirmation dialog states
  const [showApproveDialog, setShowApproveDialog] = useState(false);
  const [showPostDialog, setShowPostDialog] = useState(false);
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [actionReceiptId, setActionReceiptId] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [decisionApproved, setDecisionApproved] = useState(true);
  const [actionComment, setActionComment] = useState('');
  const [currencyCode, setCurrencyCode] = useState('GHS');

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await stockAdjustmentService.getAll();
      setReceipts(data);
    } catch (err: unknown) {
      console.error('Error fetching data:', err);
      setError('Failed to load controlled stock adjustments');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  useEffect(() => {
    let cancelled = false;

    // Inventory adjustment values post in the tenant's functional currency. Resolve that
    // canonical currency instead of embedding a symbol in this Inventory-owned screen.
    currencyService.getBaseCurrency()
      .then((currency) => {
        if (!cancelled) setCurrencyCode(normalizeInventoryCurrency(currency?.code));
      })
      .catch(() => {
        if (!cancelled) setCurrencyCode(normalizeInventoryCurrency());
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let filtered = receipts;
    if (searchTerm) {
      filtered = filtered.filter((r: StockAdjustmentDto) =>
        r.adjustmentNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    if (statusFilter !== 'all') {
      filtered = filtered.filter((r: StockAdjustmentDto) => r.status === statusFilter);
    }
    if (reasonFilter !== 'all') {
      filtered = filtered.filter((r: StockAdjustmentDto) => r.reasonCode === reasonFilter);
    }
    setFilteredReceipts(filtered);
  }, [searchTerm, statusFilter, reasonFilter, receipts]);

  const openCreateDialog = () => {
    setSelectedReceipt(null);
    setDialogMode('create');
    setDialogOpen(true);
  };

  const openEditDialog = (receipt: StockAdjustmentDto) => {
    setSelectedReceipt(receipt);
    setDialogMode('edit');
    setDialogOpen(true);
  };

  const openViewDialog = (receipt: StockAdjustmentDto) => {
    setSelectedReceipt(receipt);
    setDialogMode('view');
    setDialogOpen(true);
  };

  const handleApproveClick = (id: string, approved: boolean) => {
    setActionReceiptId(id);
    setDecisionApproved(approved);
    setActionComment('');
    setShowApproveDialog(true);
  };

  const confirmApprove = async () => {
    if (!actionReceiptId) return;
    if (!actionComment.trim()) return false;
    try {
      setActionLoading(true);
      await stockAdjustmentService.decide(actionReceiptId, decisionApproved, actionComment.trim());
      toast({ title: 'Control decision recorded', description: `Adjustment ${decisionApproved ? 'approved' : 'rejected'} successfully.` });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Action blocked', description: 'The controlled decision could not be recorded.', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowApproveDialog(false);
      setActionReceiptId(null);
    }
  };

  const handleSubmit = async (id: string) => {
    try {
      setActionLoading(true);
      const result = await stockAdjustmentService.submit(id);
      toast({ title: 'Submitted', description: result.approvalRequired === false
        ? 'The adjustment is ready for Post; stock and Finance are unchanged.'
        : 'The adjustment is pending independent approval; stock and Finance are unchanged.' });
      await fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Submission blocked', description: 'The adjustment could not enter the configured workflow.', variant: 'destructive' });
    } finally { setActionLoading(false); }
  };

  const handlePostClick = (id: string) => {
    setActionReceiptId(id);
    setShowPostDialog(true);
  };

  const confirmPost = async () => {
    if (!actionReceiptId) return;
    try {
      setActionLoading(true);
      await stockAdjustmentService.post(actionReceiptId);
      toast({ title: 'Posted', description: 'The adjustment was posted atomically to inventory and Finance.' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to post receipt', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowPostDialog(false);
      setActionReceiptId(null);
    }
  };

  const handleCancelClick = (id: string) => {
    setActionReceiptId(id);
    setActionComment('');
    setShowCancelDialog(true);
  };

  const confirmCancel = async () => {
    if (!actionReceiptId) return;
    try {
      setActionLoading(true);
      if (!actionComment.trim()) return false;
      await stockAdjustmentService.reverse(actionReceiptId, actionComment.trim());
      toast({ title: 'Reversed', description: 'Inventory and Finance reversal entries were posted atomically.' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Reversal blocked', description: 'The posted adjustment could not be reversed.', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowCancelDialog(false);
      setActionReceiptId(null);
    }
  };

  const handleDeleteClick = (id: string) => {
    setActionReceiptId(id);
    setShowDeleteDialog(true);
  };

  const confirmDelete = async () => {
    if (!actionReceiptId) return;
    try {
      setActionLoading(true);
      await stockAdjustmentService.delete(actionReceiptId);
      toast({ title: 'Success', description: 'Receipt deleted successfully' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to delete receipt', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowDeleteDialog(false);
      setActionReceiptId(null);
    }
  };

  const getStatusBadge = (status: string) => {
    const colorClass = StockAdjustmentStatusColors[status] || 'bg-gray-100 text-gray-800';
    return <Badge className={colorClass}>{status}</Badge>;
  };

  const getReasonLabel = (reasonCode: string) => {
    return StockAdjustmentReasonCodes[reasonCode as keyof typeof StockAdjustmentReasonCodes] || reasonCode;
  };

  const draftCount = receipts.filter((r: StockAdjustmentDto) => r.status === 'Draft').length;
  const approvedCount = receipts.filter((r: StockAdjustmentDto) => r.status === 'PendingApproval').length;
  const postedCount = receipts.filter((r: StockAdjustmentDto) => r.status === 'Posted').length;

  // Calculate total receipt value (always positive)
  const totalReceiptValue = receipts
    .filter((r: StockAdjustmentDto) => r.status === 'Posted')
    .reduce((sum: number, r: StockAdjustmentDto) => sum + Math.abs(r.totalAdjustmentValue), 0);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Controlled Stock Adjustments</h1>
          <p className="text-muted-foreground">Stock adjustments with exact locations, supporting evidence, and Finance posting</p>
        </div>
        <Button onClick={openCreateDialog}><Plus className="mr-2 h-4 w-4" />New Adjustment</Button>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Stock Adjustments</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{receipts.length}</p><p className="text-sm text-muted-foreground">Total Adjustments</p></div>
            <ClipboardList className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-gray-600">{draftCount}</p><p className="text-sm text-muted-foreground">Draft</p></div>
            <Clock className="h-8 w-8 text-gray-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-amber-600">{approvedCount}</p><p className="text-sm text-muted-foreground">Pending Approval</p></div>
            <CheckCircle className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-green-600">{formatInventoryMoney(totalReceiptValue, currencyCode)}</p><p className="text-sm text-muted-foreground">Posted Absolute Value</p></div>
            <ArrowUpCircle className="h-8 w-8 text-green-500" />
          </div>
        </CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search receipts..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="ReadyToPost">Ready to Post</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Posted">Posted</SelectItem>
                <SelectItem value="Reversed">Reversed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
            <Select value={reasonFilter} onValueChange={setReasonFilter}>
              <SelectTrigger><SelectValue placeholder="Adjustment Reason" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Reasons</SelectItem>
                {Object.entries(StockAdjustmentReasonCodes).map(([code, label]) => (
                  <SelectItem key={code} value={code}>{label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Receipts Table */}
      <Card>
        <CardHeader><CardTitle>Adjustment register</CardTitle><CardDescription>Immutable lifecycle and posting status for controlled inventory adjustments</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div></div>
          ) : error ? (
            <div className="text-center py-8 text-red-500">{error}</div>
          ) : filteredReceipts.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">No receipts found</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b">
                    <th className="text-left py-3 px-2">Adjustment #</th>
                    <th className="text-left py-3 px-2">Date</th>
                    <th className="text-left py-3 px-2">Reason</th>
                    <th className="text-left py-3 px-2">Description</th>
                    <th className="text-right py-3 px-2">Items</th>
                    <th className="text-right py-3 px-2">Value</th>
                    <th className="text-left py-3 px-2">Status</th>
                    <th className="text-left py-3 px-2">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredReceipts.map((receipt: StockAdjustmentDto) => (
                    <tr key={receipt.id} className="border-b hover:bg-muted/50">
                      <td className="py-3 px-2 font-medium">{receipt.adjustmentNumber}</td>
                      <td className="py-3 px-2">{format(new Date(receipt.adjustmentDate), 'dd/MM/yyyy')}</td>
                      <td className="py-3 px-2">{getReasonLabel(receipt.reasonCode)}</td>
                      <td className="py-3 px-2 max-w-[200px] truncate">{receipt.description || '-'}</td>
                      <td className="py-3 px-2 text-right">{receipt.itemCount}</td>
                      <td className="py-3 px-2 text-right font-medium text-green-600">
                        +{formatInventoryMoney(Math.abs(receipt.totalAdjustmentValue), currencyCode)}
                      </td>
                      <td className="py-3 px-2">{getStatusBadge(receipt.status)}</td>
                      <td className="py-3 px-2">
                        <div className="flex gap-1">
                          <Button variant="ghost" size="sm" onClick={() => openViewDialog(receipt)} title="View"><Eye className="h-4 w-4" /></Button>
                          {receipt.status === 'Draft' && (
                            <>
                              <Button variant="ghost" size="sm" onClick={() => openEditDialog(receipt)} title="Edit"><Pencil className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-blue-600" disabled={actionLoading || !hasPermission('procurement.inventory.adjust.request')} onClick={() => void handleSubmit(receipt.id)} title="Submit"><Send className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleDeleteClick(receipt.id)} title="Delete"><XCircle className="h-4 w-4" /></Button>
                            </>
                          )}
                          {canDecideInventoryRecord(receipt, user?.id, hasPermission('procurement.inventory.adjust.approve')) && (
                            <>
                              <Button variant="ghost" size="sm" className="text-green-600" onClick={() => handleApproveClick(receipt.id, true)} title="Approve"><CheckCircle className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleApproveClick(receipt.id, false)} title="Reject"><XCircle className="h-4 w-4" /></Button>
                            </>
                          )}
                          {canPostInventoryRecord(receipt, user?.id, hasPermission('procurement.inventory.adjust.approve')) && (
                            <Button variant="ghost" size="sm" className="text-green-600" onClick={() => handlePostClick(receipt.id)} title="Post to Inventory and Finance"><FileCheck className="h-4 w-4" /></Button>
                          )}
                          {receipt.status === 'Posted' && (
                            <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleCancelClick(receipt.id)} title="Reverse inventory and Finance"><RotateCcw className="h-4 w-4" /></Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Dialog */}
      <InventoryReceiptDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        mode={dialogMode}
        receiptId={selectedReceipt?.id}
        onSuccess={fetchData}
        currencyCode={currencyCode}
      />

      {/* Approve Confirmation */}
      <ConfirmationDialog
        open={showApproveDialog}
        onOpenChange={setShowApproveDialog}
        title={decisionApproved ? 'Approve stock adjustment' : 'Reject stock adjustment'}
        description="This decision is recorded against the shared workflow and segregation-of-duties controls."
        confirmText={decisionApproved ? 'Approve' : 'Reject'}
        variant={decisionApproved ? 'default' : 'destructive'}
        onConfirm={confirmApprove}
        isLoading={actionLoading}
        confirmDisabled={!actionComment.trim()}
      ><Input value={actionComment} onChange={(event) => setActionComment(event.target.value)} placeholder="Required decision comment" /></ConfirmationDialog>

      {/* Post Confirmation */}
      <ConfirmationDialog
        open={showPostDialog}
        onOpenChange={setShowPostDialog}
        title="Post adjustment"
        description="This atomically changes stock and creates the balanced Finance posting."
        confirmText="Post Inventory and Finance"
        onConfirm={confirmPost}
        isLoading={actionLoading}
      />

      {/* Cancel Confirmation */}
      <ConfirmationDialog
        open={showCancelDialog}
        onOpenChange={setShowCancelDialog}
        title="Reverse posted adjustment"
        description="This creates compensating inventory movements and a Finance reversal; the original record remains immutable."
        confirmText="Reverse"
        variant="destructive"
        onConfirm={confirmCancel}
        isLoading={actionLoading}
        confirmDisabled={!actionComment.trim()}
      ><Input value={actionComment} onChange={(event) => setActionComment(event.target.value)} placeholder="Required reversal reason" /></ConfirmationDialog>

      {/* Delete Confirmation */}
      <ConfirmationDialog
        open={showDeleteDialog}
        onOpenChange={setShowDeleteDialog}
        title="Delete Draft Adjustment"
        description="Only an unsubmitted Draft can be deleted."
        confirmText="Delete"
        variant="destructive"
        onConfirm={confirmDelete}
        isLoading={actionLoading}
      />
    </div>
  );
}
