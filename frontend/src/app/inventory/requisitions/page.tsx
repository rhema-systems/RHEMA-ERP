'use client';
import { GlobalSearchRecordOpener } from '@/components/global-search/GlobalSearchRecordOpener';

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
  Plus, Search, Eye, ClipboardList, Package, CheckCircle,
  Clock, XCircle, Pencil, Building2
} from 'lucide-react';
import {
  inventoryRequisitionService,
  InventoryRequisitionDto, RequisitionStatusMap
} from '@/services/inventoryRequisitionService';
import { inventoryManagementService, WarehouseDto } from '@/services/inventoryManagementService';
import { RequisitionDialog } from '@/components/inventory/RequisitionDialog';
import { IssueRequisitionDialog } from '@/components/inventory/IssueRequisitionDialog';
import { ReturnRequisitionDialog } from '@/components/inventory/ReturnRequisitionDialog';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { canIssueRequisition } from '@/lib/inventory-requisition-access';
import { format } from 'date-fns';

const RequisitionStatuses = [
  { value: 1, label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 2, label: 'Submitted', color: 'bg-yellow-100 text-yellow-800' },
  { value: 3, label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 4, label: 'In Progress', color: 'bg-indigo-100 text-indigo-800' },
  { value: 5, label: 'Partially Issued', color: 'bg-purple-100 text-purple-800' },
  { value: 6, label: 'Issued', color: 'bg-green-100 text-green-800' },
  { value: 7, label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 8, label: 'Cancelled', color: 'bg-red-100 text-red-800' },
  { value: 9, label: 'Rejected', color: 'bg-red-100 text-red-800' }
];

// Helper to normalize status to number (API may return string or number)
const normalizeStatus = (status: number | string): number => {
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    'Draft': 1, 'Submitted': 2, 'Approved': 3, 'InProgress': 4,
    'PartiallyIssued': 5, 'Issued': 6, 'Completed': 7, 'Cancelled': 8, 'Rejected': 9
  };
  return statusMap[status] || 0;
};

export default function InventoryRequisitionsPage() {
  const { toast } = useToast();
  const { user, hasPermission } = useAuth();
  const hasIssuePermission = hasPermission('procurement.inventory.issue');
  const router = useRouter();
  const [requisitions, setRequisitions] = useState<InventoryRequisitionDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [filteredRequisitions, setFilteredRequisitions] = useState<InventoryRequisitionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedRequisition, setSelectedRequisition] = useState<InventoryRequisitionDto | null>(null);

  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'InventoryRequisition',
    filteredRequisitions.map((r) => r.id),
    filteredRequisitions.length > 0
  );

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit' | 'view'>('create');

  // Issue dialog state
  const [issueDialogOpen, setIssueDialogOpen] = useState(false);
  const [issueRequisitionId, setIssueRequisitionId] = useState<string | null>(null);
  const [returnDialogOpen, setReturnDialogOpen] = useState(false);
  const [returnRequisitionId, setReturnRequisitionId] = useState<string | null>(null);

  // Confirmation dialog states
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [actionRequisitionId, setActionRequisitionId] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [requisitionData, warehouseData] = await Promise.all([
        inventoryRequisitionService.getAll(),
        inventoryManagementService.getWarehouses()
      ]);
      setRequisitions(requisitionData);
      setWarehouses(warehouseData);
    } catch (err: unknown) {
      console.error('Error fetching data:', err);
      setError('Failed to load requisitions');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  useEffect(() => {
    let filtered = requisitions;
    if (searchTerm) {
      filtered = filtered.filter(r =>
        r.requisitionNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.departmentName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.warehouseName?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    if (statusFilter !== 'all') {
      filtered = filtered.filter(r => normalizeStatus(r.status) === parseInt(statusFilter));
    }
    setFilteredRequisitions(filtered);
  }, [searchTerm, statusFilter, requisitions]);

  const openCreateDialog = () => {
    setSelectedRequisition(null);
    setDialogMode('create');
    setDialogOpen(true);
  };

  const openEditDialog = (requisition: InventoryRequisitionDto) => {
    setSelectedRequisition(requisition);
    setDialogMode('edit');
    setDialogOpen(true);
  };

  const openViewDialog = (requisition: InventoryRequisitionDto) => {
    setSelectedRequisition(requisition);
    setDialogMode('view');
    setDialogOpen(true);
  };

  const handleIssue = (id: string) => {
    setIssueRequisitionId(id);
    setIssueDialogOpen(true);
  };

  const handleReturn = (id: string) => {
    setReturnRequisitionId(id);
    setReturnDialogOpen(true);
  };

  const handleCancelClick = (id: string) => {
    setActionRequisitionId(id);
    setCancelReason('');
    setShowCancelDialog(true);
  };

  const confirmCancel = async () => {
    if (!actionRequisitionId || !cancelReason.trim()) return;
    try {
      setActionLoading(true);
      await inventoryRequisitionService.cancel(actionRequisitionId, cancelReason);
      toast({ title: 'Success', description: 'Requisition cancelled successfully' });
      fetchData();
    } catch (err) {
      console.error('Error:', err);
      toast({ title: 'Error', description: 'Failed to cancel requisition', variant: 'destructive' });
    } finally {
      setActionLoading(false);
      setShowCancelDialog(false);
      setActionRequisitionId(null);
      setCancelReason('');
    }
  };

  const getStatusBadge = (status: number | string) => {
    const numStatus = normalizeStatus(status);
    const s = RequisitionStatuses.find(st => st.value === numStatus);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || RequisitionStatusMap[numStatus] || status}</Badge>;
  };

  const pendingApprovalCount = requisitions.filter(r => normalizeStatus(r.status) === 2).length;
  const pendingIssueCount = requisitions.filter(r => {
    const s = normalizeStatus(r.status);
    return s === 3 || s === 4;
  }).length;
  const completedCount = requisitions.filter(r => normalizeStatus(r.status) === 7).length;

  return (
    <div className="space-y-6">
      <GlobalSearchRecordOpener load={id => inventoryRequisitionService.getById(id)} onOpen={openViewDialog} />
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inventory Requisitions</h1>
          <p className="text-muted-foreground">Manage department inventory issue requests</p>
        </div>
        <Button onClick={openCreateDialog}><Plus className="mr-2 h-4 w-4" />New Requisition</Button>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Requisitions</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{requisitions.length}</p><p className="text-sm text-muted-foreground">Total Requisitions</p></div>
            <ClipboardList className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-yellow-600">{pendingApprovalCount}</p><p className="text-sm text-muted-foreground">Pending Approval</p></div>
            <Clock className="h-8 w-8 text-yellow-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-purple-600">{pendingIssueCount}</p><p className="text-sm text-muted-foreground">Pending Issue</p></div>
            <Package className="h-8 w-8 text-purple-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-green-600">{completedCount}</p><p className="text-sm text-muted-foreground">Completed</p></div>
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
              <Input placeholder="Search requisitions..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                {RequisitionStatuses.map(s => <SelectItem key={s.value} value={s.value.toString()}>{s.label}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Requisitions Table */}
      <Card>
        <CardHeader><CardTitle>Requisitions</CardTitle><CardDescription>List of all inventory requisitions</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div></div>
          ) : error ? (
            <div className="text-center py-8 text-red-500">{error}</div>
          ) : filteredRequisitions.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">No requisitions found</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b">
                    <th className="text-left py-3 px-2">Requisition #</th>
                    <th className="text-left py-3 px-2">Department</th>
                    <th className="text-left py-3 px-2">Warehouse</th>
                    <th className="text-left py-3 px-2">Request Date</th>
                    <th className="text-left py-3 px-2">Required Date</th>
                    <th className="text-left py-3 px-2">Items</th>
                    <th className="text-left py-3 px-2">Status</th>
                    <th className="text-left py-3 px-2">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRequisitions.map((req) => {
                    const status = normalizeStatus(req.status);
                    const canReturn = hasIssuePermission && (status === 5 || status === 6 || status === 7);
                    const canIssue = canIssueRequisition(req, user?.id, hasIssuePermission);
                    const summary = workflowSummariesById[req.id];
                    const stepName = summary?.currentStepName || req.currentWorkflowStepName;
                    const pending = formatPendingApprovers(summary?.pendingApprovers || []);
                    return (
                      <tr key={req.id} className="border-b hover:bg-muted/50">
                        <td className="py-3 px-2">
                          <div className="flex items-center gap-2">
                            <span className="font-medium">{req.requisitionNumber}</span>
                            {status === 2 && stepName && (
                              <Badge variant="outline" className="text-xs">
                                Step: {stepName}
                              </Badge>
                            )}
                            {status === 2 && pending.short && (
                              <Badge variant="outline" className="text-xs" title={pending.full}>
                                Pending with: {pending.short}
                              </Badge>
                            )}
                          </div>
                        </td>
                        <td className="py-3 px-2"><div className="flex items-center gap-1"><Building2 className="h-4 w-4 text-muted-foreground" />{req.departmentName || '-'}</div></td>
                        <td className="py-3 px-2">{req.warehouseName}</td>
                        <td className="py-3 px-2">{req.requestDateFormatted || format(new Date(req.requestDate), 'dd/MM/yyyy')}</td>
                        <td className="py-3 px-2">{req.requiredDateFormatted || (req.requiredDate ? format(new Date(req.requiredDate), 'dd/MM/yyyy') : '-')}</td>
                        <td className="py-3 px-2">{req.totalItems}</td>
                        <td className="py-3 px-2">{getStatusBadge(req.status)}</td>
                        <td className="py-3 px-2">
                          <div className="flex gap-1">
                            <Button variant="ghost" size="sm" onClick={() => openViewDialog(req)} title="View"><Eye className="h-4 w-4" /></Button>
                            {status === 1 && (
                              <>
                                <Button variant="ghost" size="sm" onClick={() => openEditDialog(req)} title="Edit"><Pencil className="h-4 w-4" /></Button>
                              </>
                            )}
                            {(status === 1 || status === 2) && (
                              <WorkflowApprovalActions
                                entityType="InventoryRequisition"
                                entityId={req.id}
                                entityLabel="Inventory Requisition"
                                entityNumber={req.requisitionNumber}
                                status={RequisitionStatusMap[status] || 'Draft'}
                                currentStepName={stepName}
                                workflowSummary={summary}
                                canSubmit={status === 1}
                                canApproveReject={status === 2}
                                onSubmit={async () => {
                                  try {
                                    await inventoryRequisitionService.submit(req.id);
                                  } catch (err: any) {
                                    const msg =
                                      err?.response?.data?.error ||
                                      err?.response?.data?.message ||
                                      err?.response?.data ||
                                      err?.message ||
                                      'Failed to submit requisition for approval';
                                    throw new Error(typeof msg === 'string' ? msg : 'Failed to submit requisition for approval');
                                  }
                                }}
                                onApprove={async (comments) => {
                                  try {
                                    await inventoryRequisitionService.approve(req.id, comments || undefined);
                                  } catch (err: any) {
                                    const msg =
                                      err?.response?.data?.error ||
                                      err?.response?.data?.message ||
                                      err?.response?.data ||
                                      err?.message ||
                                      'Failed to approve requisition';
                                    throw new Error(typeof msg === 'string' ? msg : 'Failed to approve requisition');
                                  }
                                }}
                                onReject={async (comments) => {
                                  try {
                                    await inventoryRequisitionService.reject(req.id, comments);
                                  } catch (err: any) {
                                    const msg =
                                      err?.response?.data?.error ||
                                      err?.response?.data?.message ||
                                      err?.response?.data ||
                                      err?.message ||
                                      'Failed to reject requisition';
                                    throw new Error(typeof msg === 'string' ? msg : 'Failed to reject requisition');
                                  }
                                }}
                                onAfterAction={fetchData}
                                onOpenWorkflows={() => router.push('/administration/workflow')}
                                size="icon"
                                iconOnly
                                className="flex"
                              />
                            )}
                            {canIssue && (
                              <Button variant="ghost" size="sm" className="text-purple-600" onClick={() => handleIssue(req.id)} title="Issue Items"><Package className="h-4 w-4" /></Button>
                            )}
                            {(status === 5 || status === 6 || status === 7) && (
                              <Button variant="ghost" size="sm" onClick={() => handleIssue(req.id)} title="Issue vouchers"><ClipboardList className="h-4 w-4" /></Button>
                            )}
                            {canReturn && (
                              <Button variant="ghost" size="sm" className="text-amber-600" onClick={() => handleReturn(req.id)} title="Return Items"><CheckCircle className="h-4 w-4" /></Button>
                            )}
                            {(status === 1 || status === 2) && (
                              <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleCancelClick(req.id)} title="Cancel"><XCircle className="h-4 w-4" /></Button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Dialogs */}
      <RequisitionDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        mode={dialogMode}
        requisitionId={selectedRequisition?.id}
        warehouses={warehouses}
        onSuccess={fetchData}
      />

      <IssueRequisitionDialog
        open={issueDialogOpen}
        onOpenChange={setIssueDialogOpen}
        requisitionId={issueRequisitionId}
        onSuccess={fetchData}
      />

      <ReturnRequisitionDialog
        open={returnDialogOpen}
        onOpenChange={setReturnDialogOpen}
        requisitionId={returnRequisitionId}
        onSuccess={fetchData}
      />

      {/* Cancel Confirmation */}
      <ConfirmationDialog
        open={showCancelDialog}
        onOpenChange={setShowCancelDialog}
        title="Cancel Requisition"
        description="Please provide a reason for cancellation:"
        confirmText="Cancel Requisition"
        variant="destructive"
        onConfirm={confirmCancel}
        isLoading={actionLoading}
      >
        <Input placeholder="Cancellation reason" value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
      </ConfirmationDialog>
    </div>
  );
}
