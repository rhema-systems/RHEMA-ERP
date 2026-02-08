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
  Clock, XCircle, Pencil, ArrowUpCircle, FileCheck, Package
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

export default function InventoryReceiptsPage() {
  const { toast } = useToast();
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

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await stockAdjustmentService.getAll();
      setReceipts(data);
    } catch (err: unknown) {
      console.error('Error fetching data:', err);
      setError('Failed to load inventory receipts');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
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

  const handleApproveClick = (id: string) => {
    setActionReceiptId(id);
    setShowApproveDialog(true);
  };

  const confirmApprove = async () => {
    if (!actionReceiptId) return;
    try {
      setActionLoading(true);
      await stockAdjustmentService.approve(actionReceiptId);
      toast({ title: 'Success', description: 'Receipt approved successfully' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to approve receipt', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowApproveDialog(false);
      setActionReceiptId(null);
    }
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
      toast({ title: 'Success', description: 'Receipt posted to inventory successfully' });
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
    setShowCancelDialog(true);
  };

  const confirmCancel = async () => {
    if (!actionReceiptId) return;
    try {
      setActionLoading(true);
      await stockAdjustmentService.cancel(actionReceiptId);
      toast({ title: 'Success', description: 'Receipt cancelled successfully' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to cancel receipt', variant: 'destructive' });
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
  const approvedCount = receipts.filter((r: StockAdjustmentDto) => r.status === 'Approved').length;
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
          <h1 className="text-3xl font-bold tracking-tight">Inventory Receipts</h1>
          <p className="text-muted-foreground">Receive inventory into your warehouses</p>
        </div>
        <Button onClick={openCreateDialog}><Plus className="mr-2 h-4 w-4" />New Receipt</Button>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Inventory Receipts</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{receipts.length}</p><p className="text-sm text-muted-foreground">Total Receipts</p></div>
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
            <div><p className="text-2xl font-bold text-blue-600">{approvedCount}</p><p className="text-sm text-muted-foreground">Approved</p></div>
            <CheckCircle className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-green-600">${totalReceiptValue.toFixed(2)}</p><p className="text-sm text-muted-foreground">Total Received Value</p></div>
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
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Posted">Posted</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
            <Select value={reasonFilter} onValueChange={setReasonFilter}>
              <SelectTrigger><SelectValue placeholder="Receipt Reason" /></SelectTrigger>
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
        <CardHeader><CardTitle>Inventory Receipts</CardTitle><CardDescription>List of all inventory receipts</CardDescription></CardHeader>
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
                    <th className="text-left py-3 px-2">Receipt #</th>
                    <th className="text-left py-3 px-2">Date</th>
                    <th className="text-left py-3 px-2">Receipt Reason</th>
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
                        +${Math.abs(receipt.totalAdjustmentValue).toFixed(2)}
                      </td>
                      <td className="py-3 px-2">{getStatusBadge(receipt.status)}</td>
                      <td className="py-3 px-2">
                        <div className="flex gap-1">
                          <Button variant="ghost" size="sm" onClick={() => openViewDialog(receipt)} title="View"><Eye className="h-4 w-4" /></Button>
                          {receipt.status === 'Draft' && (
                            <>
                              <Button variant="ghost" size="sm" onClick={() => openEditDialog(receipt)} title="Edit"><Pencil className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-blue-600" onClick={() => handleApproveClick(receipt.id)} title="Approve"><CheckCircle className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-green-600" onClick={() => handlePostClick(receipt.id)} title="Post to Inventory"><FileCheck className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleDeleteClick(receipt.id)} title="Delete"><XCircle className="h-4 w-4" /></Button>
                            </>
                          )}
                          {receipt.status === 'Approved' && (
                            <>
                              <Button variant="ghost" size="sm" className="text-green-600" onClick={() => handlePostClick(receipt.id)} title="Post to Inventory"><FileCheck className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleCancelClick(receipt.id)} title="Cancel"><XCircle className="h-4 w-4" /></Button>
                            </>
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
      />

      {/* Approve Confirmation */}
      <ConfirmationDialog
        open={showApproveDialog}
        onOpenChange={setShowApproveDialog}
        title="Approve Receipt"
        description="Are you sure you want to approve this inventory receipt?"
        confirmText="Approve"
        onConfirm={confirmApprove}
        isLoading={actionLoading}
      />

      {/* Post Confirmation */}
      <ConfirmationDialog
        open={showPostDialog}
        onOpenChange={setShowPostDialog}
        title="Post Receipt"
        description="Are you sure you want to post this receipt? This will add the items to inventory and cannot be undone."
        confirmText="Post to Inventory"
        onConfirm={confirmPost}
        isLoading={actionLoading}
      />

      {/* Cancel Confirmation */}
      <ConfirmationDialog
        open={showCancelDialog}
        onOpenChange={setShowCancelDialog}
        title="Cancel Receipt"
        description="Are you sure you want to cancel this receipt?"
        confirmText="Cancel Receipt"
        variant="destructive"
        onConfirm={confirmCancel}
        isLoading={actionLoading}
      />

      {/* Delete Confirmation */}
      <ConfirmationDialog
        open={showDeleteDialog}
        onOpenChange={setShowDeleteDialog}
        title="Delete Receipt"
        description="Are you sure you want to delete this receipt? This action cannot be undone."
        confirmText="Delete"
        variant="destructive"
        onConfirm={confirmDelete}
        isLoading={actionLoading}
      />
    </div>
  );
}
