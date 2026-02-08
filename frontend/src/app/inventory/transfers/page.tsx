'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { formatPendingApprovers, useWorkflowEntitySummaries } from '@/hooks/useWorkflowEntitySummaries';
import {
  Plus, Search, Eye, ArrowRight, Truck, Package, CheckCircle,
  Clock, XCircle, Send, Download, Pencil, FileText, Undo2
} from 'lucide-react';
import {
  inventoryManagementService,
  InventoryTransferDto, WarehouseDto
} from '@/services/inventoryManagementService';
import { TransferDialog } from '@/components/inventory/TransferDialog';
import { ShipTransferDialog } from '@/components/inventory/ShipTransferDialog';
import { ReceiveTransferDialog } from '@/components/inventory/ReceiveTransferDialog';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';

const TransferStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 'Submitted', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800' },
  { value: 'Approved', label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 'Picked', label: 'Picked', color: 'bg-indigo-100 text-indigo-800' },
  { value: 'InTransit', label: 'In Transit', color: 'bg-purple-100 text-purple-800' },
  { value: 'Received', label: 'Received', color: 'bg-green-100 text-green-800' },
  { value: 'Completed', label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 'Cancelled', label: 'Cancelled', color: 'bg-red-100 text-red-800' },
  { value: 'Rejected', label: 'Rejected', color: 'bg-red-100 text-red-800' }
];

export default function InventoryTransfersPage() {
  const { toast } = useToast();
  const router = useRouter();
  const [transfers, setTransfers] = useState<InventoryTransferDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [filteredTransfers, setFilteredTransfers] = useState<InventoryTransferDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedTransfer, setSelectedTransfer] = useState<InventoryTransferDto | null>(null);

  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'InventoryTransfer',
    filteredTransfers.map((t) => t.id),
    filteredTransfers.length > 0
  );

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit' | 'view'>('create');

  // Ship dialog state
  const [shipDialogOpen, setShipDialogOpen] = useState(false);
  const [shipTransferId, setShipTransferId] = useState<string | null>(null);

  // Receive dialog state
  const [receiveDialogOpen, setReceiveDialogOpen] = useState(false);
  const [receiveTransferId, setReceiveTransferId] = useState<string | null>(null);

  // Confirmation dialog states
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [showReverseShipmentDialog, setShowReverseShipmentDialog] = useState(false);
  const [actionTransferId, setActionTransferId] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [transferData, warehouseData] = await Promise.all([
        inventoryManagementService.getInventoryTransfers(),
        inventoryManagementService.getWarehouses()
      ]);
      setTransfers(transferData);
      setWarehouses(warehouseData);
    } catch (err: any) {
      console.error('Error fetching data:', err);
      setError('Failed to load transfers');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  useEffect(() => {
    let filtered = transfers;
    if (searchTerm) {
      filtered = filtered.filter(t =>
        t.transferNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
        t.sourceWarehouseName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        t.destinationWarehouseName?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    if (statusFilter !== 'all') filtered = filtered.filter(t => t.status === statusFilter);
    setFilteredTransfers(filtered);
  }, [searchTerm, statusFilter, transfers]);

  const openCreateDialog = () => {
    setSelectedTransfer(null);
    setDialogMode('create');
    setDialogOpen(true);
  };

  const openEditDialog = (transfer: InventoryTransferDto) => {
    setSelectedTransfer(transfer);
    setDialogMode('edit');
    setDialogOpen(true);
  };

  const openViewDialog = (transfer: InventoryTransferDto) => {
    setSelectedTransfer(transfer);
    setDialogMode('view');
    setDialogOpen(true);
  };

  const handleShip = (id: string) => {
    setShipTransferId(id);
    setShipDialogOpen(true);
  };

  const handleReceive = (id: string) => {
    setReceiveTransferId(id);
    setReceiveDialogOpen(true);
  };

  const handleCancelClick = (id: string) => {
    setActionTransferId(id);
    setShowCancelDialog(true);
  };

  const handleReverseShipmentClick = (id: string) => {
    setActionTransferId(id);
    setShowReverseShipmentDialog(true);
  };

  const confirmReverseShipment = async () => {
    if (!actionTransferId) return;
    try {
      setActionLoading(true);
      await inventoryManagementService.reverseShipment(actionTransferId, 'Shipment reversed by user');
      toast({ title: 'Success', description: 'Shipment reversed successfully. Quantities have been reinstated to the source warehouse.' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to reverse shipment', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowReverseShipmentDialog(false);
      setActionTransferId(null);
    }
  };

  const confirmCancel = async () => {
    if (!actionTransferId) return;
    try {
      setActionLoading(true);
      await inventoryManagementService.cancelTransfer(actionTransferId, 'Cancelled by user');
      toast({ title: 'Success', description: 'Transfer cancelled successfully' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to cancel transfer', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowCancelDialog(false);
      setActionTransferId(null);
    }
  };

  const getStatusBadge = (status: string) => {
    const s = TransferStatuses.find(st => st.value === status);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || status}</Badge>;
  };

  const getAllocationMethodLabel = (method?: string) => {
    if (method === 'GLExpense') return 'GL Expense';
    return 'Spread to Item Cost';
  };

  const handlePrintShipmentNote = async (id: string) => {
    try {
      const blob = await inventoryManagementService.getShipmentNotePdf(id);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating shipment note:', err);
      toast({ title: 'Error', description: 'Failed to generate shipment note', variant: 'destructive' });
    }
  };

  const handlePrintGRN = async (id: string) => {
    try {
      const blob = await inventoryManagementService.getGoodsReceivedNotePdf(id);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating GRN:', err);
      toast({ title: 'Error', description: 'Failed to generate GRN', variant: 'destructive' });
    }
  };

  const inTransitCount = transfers.filter(t => t.status === 'InTransit').length;
  const pendingCount = transfers.filter(t => t.status === 'Submitted' || t.status === 'Approved').length;

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inventory Transfers</h1>
          <p className="text-muted-foreground">Manage inter-warehouse and inter-bin stock transfers</p>
        </div>
        <Button onClick={openCreateDialog}><Plus className="mr-2 h-4 w-4" />New Transfer</Button>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Transfers</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{transfers.length}</p><p className="text-sm text-muted-foreground">Total Transfers</p></div>
            <Package className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-purple-600">{inTransitCount}</p><p className="text-sm text-muted-foreground">In Transit</p></div>
            <Truck className="h-8 w-8 text-purple-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-yellow-600">{pendingCount}</p><p className="text-sm text-muted-foreground">Pending</p></div>
            <Clock className="h-8 w-8 text-yellow-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-green-600">{transfers.filter(t => t.status === 'Completed').length}</p><p className="text-sm text-muted-foreground">Completed</p></div>
            <CheckCircle className="h-8 w-8 text-green-500" />
          </div>
        </CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search transfers..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                {TransferStatuses.map(s => <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Transfer List */}
      <Card>
        <CardHeader>
          <CardTitle>Inventory Transfers</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredTransfers.length} transfer(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredTransfers.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No transfers found.</div>
              ) : (
                filteredTransfers.map((transfer) => {
                  const summary = workflowSummariesById[transfer.id];
                  const stepName = summary?.currentStepName || transfer.currentWorkflowStepName;
                  const pending = formatPendingApprovers(summary?.pendingApprovers || []);

                  return (
                  <div key={transfer.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-purple-100 flex items-center justify-center">
                          <Truck className="h-6 w-6 text-purple-600" />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{transfer.transferNumber}</h3>
                            {getStatusBadge(transfer.status)}
                            {transfer.status === 'Submitted' && stepName && (
                              <Badge variant="outline" className="text-muted-foreground">
                                Step: {stepName}
                              </Badge>
                            )}
                            {transfer.status === 'Submitted' && pending.short && (
                              <Badge variant="outline" className="text-muted-foreground" title={pending.full}>
                                Pending with: {pending.short}
                              </Badge>
                            )}
                          </div>
                          <div className="flex items-center text-sm text-muted-foreground">
                            <span>{transfer.sourceWarehouseName}</span>
                            <ArrowRight className="h-4 w-4 mx-2" />
                            <span>{transfer.destinationWarehouseName}</span>
                          </div>
                          <p className="text-sm text-muted-foreground">
                            Requested: {transfer.requestedDate ? format(new Date(transfer.requestedDate), 'MMM dd, yyyy') : '-'} • Items: {transfer.totalItems || 0}
                          </p>
                          <div className="mt-1 flex flex-wrap items-center gap-2 text-xs">
                            <Badge variant="outline">
                              Addl. Cost: ${((transfer.totalAdditionalCost ?? ((transfer.shippingCost || 0) + (transfer.miscellaneousCost || 0))) || 0).toFixed(2)}
                            </Badge>
                            <Badge variant="outline">
                              Method: {getAllocationMethodLabel(transfer.costAllocationMethod)}
                            </Badge>
                            <Badge variant="outline">
                              Basis: {transfer.costApportionmentBasis || 'Value'}
                            </Badge>
                            <Badge className={transfer.costsAllocated ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-700'}>
                              {transfer.costsAllocated ? 'Allocated' : 'Not Allocated'}
                            </Badge>
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => openViewDialog(transfer)}><Eye className="h-4 w-4 mr-1" />View</Button>
                        {transfer.status === 'Draft' && (
                          <Button size="sm" variant="outline" onClick={() => openEditDialog(transfer)}><Pencil className="h-4 w-4 mr-1" />Edit</Button>
                        )}

                        <WorkflowApprovalActions
                          entityType="InventoryTransfer"
                          entityId={transfer.id}
                          entityLabel="Inventory Transfer"
                          entityNumber={transfer.transferNumber}
                          status={transfer.status}
                          currentStepName={stepName}
                          workflowSummary={summary}
                          canSubmit={transfer.status === 'Draft'}
                          canApproveReject={transfer.status === 'Submitted'}
                          onSubmit={async () => {
                            try {
                              await inventoryManagementService.submitTransferForApproval(transfer.id);
                            } catch (err: any) {
                              const msg =
                                err?.response?.data?.error ||
                                err?.response?.data ||
                                err?.message ||
                                'Failed to submit transfer for approval';
                              throw new Error(typeof msg === 'string' ? msg : 'Failed to submit transfer for approval');
                            }
                          }}
                          onApprove={async (comments) => {
                            try {
                              await inventoryManagementService.approveTransfer(transfer.id, comments || undefined);
                            } catch (err: any) {
                              const msg =
                                err?.response?.data?.error ||
                                err?.response?.data ||
                                err?.message ||
                                'Failed to approve transfer';
                              throw new Error(typeof msg === 'string' ? msg : 'Failed to approve transfer');
                            }
                          }}
                          onReject={async (comments) => {
                            try {
                              await inventoryManagementService.rejectTransfer(transfer.id, comments);
                            } catch (err: any) {
                              const msg =
                                err?.response?.data?.error ||
                                err?.response?.data ||
                                err?.message ||
                                'Failed to reject transfer';
                              throw new Error(typeof msg === 'string' ? msg : 'Failed to reject transfer');
                            }
                          }}
                          onAfterAction={fetchData}
                          onOpenWorkflows={() => router.push('/administration/workflow')}
                        />

                        {transfer.status === 'Approved' && <Button size="sm" variant="outline" onClick={() => handleShip(transfer.id)}><Send className="h-4 w-4 mr-1" />Ship</Button>}
                        {transfer.status === 'InTransit' && (
                          <>
                            <Button size="sm" variant="outline" onClick={() => handleReceive(transfer.id)}><Download className="h-4 w-4 mr-1" />Receive</Button>
                            <Button size="sm" variant="outline" className="text-orange-600" onClick={() => handleReverseShipmentClick(transfer.id)} title="Reverse Shipment"><Undo2 className="h-4 w-4 mr-1" />Reverse</Button>
                          </>
                        )}
                        {/* PDF Buttons - Show Shipment Note after shipping, GRN after receiving */}
                        {['InTransit', 'Received', 'Completed'].includes(transfer.status) && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => handlePrintShipmentNote(transfer.id)}
                            title="Print Shipment Note"
                          >
                            <FileText className="h-4 w-4" />
                          </Button>
                        )}
                        {['Received', 'Completed'].includes(transfer.status) && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => handlePrintGRN(transfer.id)}
                            title="Print GRN"
                          >
                            <FileText className="h-4 w-4 text-green-600" />
                          </Button>
                        )}
                        {['Draft', 'Submitted'].includes(transfer.status) && (
                          <Button size="sm" variant="outline" className="text-red-600" onClick={() => handleCancelClick(transfer.id)}><XCircle className="h-4 w-4" /></Button>
                        )}
                      </div>
                    </div>
                  </div>
                  );
                })
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Transfer Dialog - handles Create, Edit, View */}
      <TransferDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        transfer={selectedTransfer}
        mode={dialogMode}
        warehouses={warehouses}
        onSuccess={fetchData}
      />

      {/* Cancel Confirmation Dialog */}
      <ConfirmationDialog
        open={showCancelDialog}
        onOpenChange={setShowCancelDialog}
        title="Cancel Transfer"
        description="Are you sure you want to cancel this inventory transfer? This action cannot be undone."
        confirmText="Cancel Transfer"
        cancelText="Go Back"
        variant="destructive"
        onConfirm={confirmCancel}
        isLoading={actionLoading}
      />

      {/* Reverse Shipment Confirmation Dialog */}
      <ConfirmationDialog
        open={showReverseShipmentDialog}
        onOpenChange={setShowReverseShipmentDialog}
        title="Reverse Shipment"
        description="Are you sure you want to reverse this shipment? The shipped quantities will be reinstated to the source warehouse and the transfer will be cancelled."
        confirmText="Reverse Shipment"
        cancelText="Cancel"
        variant="default"
        onConfirm={confirmReverseShipment}
        isLoading={actionLoading}
      />

      {/* Ship Transfer Dialog */}
      <ShipTransferDialog
        open={shipDialogOpen}
        onOpenChange={setShipDialogOpen}
        transferId={shipTransferId}
        onSuccess={fetchData}
      />

      {/* Receive Transfer Dialog */}
      <ReceiveTransferDialog
        open={receiveDialogOpen}
        onOpenChange={setReceiveDialogOpen}
        transferId={receiveTransferId}
        onSuccess={fetchData}
      />
    </div>
  );
}
