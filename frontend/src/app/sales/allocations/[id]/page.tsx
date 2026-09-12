'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { format } from 'date-fns';
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRightLeft,
  ArrowUpRight,
  Ban,
  CheckCircle2,
  Clock,
  FileText,
  MapPinned,
  RotateCcw,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowApprovalActions, WorkflowApprovalHistoryPanel, useWorkflowRecord } from '@/components/workflow';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import {
  salesAllocationService,
  type SalesAllocationDto,
} from '@/services/salesAllocationService';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Reserved: { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
  PendingApproval: { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
  Approved: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
  Allocated: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Sold: { variant: 'default', className: 'bg-emerald-100 text-emerald-800' },
  Leased: { variant: 'default', className: 'bg-cyan-100 text-cyan-800' },
  Released: { variant: 'outline', className: 'bg-slate-100 text-slate-700' },
  Cancelled: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
  Expired: { variant: 'outline', className: 'bg-zinc-100 text-zinc-700' },
  Rejected: { variant: 'destructive', className: 'bg-red-50 text-red-700' },
};

const ACTIVE_STATUSES = new Set(['Reserved', 'PendingApproval', 'Approved', 'Allocated', 'Sold', 'Leased']);

const formatDate = (value?: string, withTime = false) => {
  if (!value) return '-';
  try { return format(new Date(value), withTime ? 'dd MMM yyyy HH:mm' : 'dd MMM yyyy'); } catch { return value; }
};

const formatAmount = (amount?: number, currency?: string) => {
  if (amount === undefined || amount === null) return '-';
  return `${currency || 'GHS'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
};

const formatLabel = (value?: string) => value ? value.replace(/([A-Z])/g, ' $1').trim() : '-';

export default function SalesAllocationDetailPage() {
  const params = useParams();
  const router = useRouter();
  const allocationId = params.id as string;

  const [allocation, setAllocation] = useState<SalesAllocationDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [nextStatus, setNextStatus] = useState('');
  const [statusNotes, setStatusNotes] = useState('');
  const [reservedUntil, setReservedUntil] = useState('');
  const [agreedValue, setAgreedValue] = useState('');
  const [transferDialogOpen, setTransferDialogOpen] = useState(false);
  const [businessPartners, setBusinessPartners] = useState<BusinessPartnerDto[]>([]);
  const [loadingPartners, setLoadingPartners] = useState(false);
  const [transferBusinessPartnerId, setTransferBusinessPartnerId] = useState('');
  const [transferCustomerName, setTransferCustomerName] = useState('');
  const [transferNotes, setTransferNotes] = useState('');
  const [clearLinkedDocuments, setClearLinkedDocuments] = useState(true);

  useEffect(() => {
    if (allocationId) {
      void loadAllocation();
    }
  }, [allocationId]);

  const loadAllocation = async () => {
    try {
      setLoading(true);
      const data = await salesAllocationService.getAllocationById(allocationId);
      setAllocation(data);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load sales allocation');
    } finally {
      setLoading(false);
    }
  };

  const openStatusDialog = (status: string) => {
    setNextStatus(status);
    setStatusNotes('');
    setReservedUntil(allocation?.reservedUntil ? allocation.reservedUntil.split('T')[0] : '');
    setAgreedValue(allocation?.agreedValue?.toString() || '');
    setStatusDialogOpen(true);
  };

  const loadBusinessPartners = async () => {
    try {
      setLoadingPartners(true);
      const partners = await businessPartnerService
        .getAllPartnersForDropdown()
        .catch(() => businessPartnerService.getActivePartners());
      setBusinessPartners(partners.sort((left, right) => left.partnerName.localeCompare(right.partnerName)));
    } catch {
      setBusinessPartners([]);
    } finally {
      setLoadingPartners(false);
    }
  };

  const openTransferDialog = () => {
    setTransferBusinessPartnerId('');
    setTransferCustomerName('');
    setTransferNotes('');
    setClearLinkedDocuments(true);
    setTransferDialogOpen(true);
    if (businessPartners.length === 0) {
      void loadBusinessPartners();
    }
  };

  const handleStatusUpdate = async () => {
    if (!allocation || !nextStatus) return;

    const requiresReason = ['Released', 'Cancelled', 'Expired', 'Rejected'].includes(nextStatus);
    if (requiresReason && !statusNotes.trim()) {
      toast.error('Please provide a reason.');
      return;
    }

    try {
      setActionLoading(true);
      const updated = await salesAllocationService.updateAllocationStatus(allocation.id, {
        status: nextStatus,
        agreedValue: agreedValue ? Number(agreedValue) : undefined,
        reservedUntil: reservedUntil || undefined,
        notes: statusNotes || undefined,
        releaseReason: requiresReason ? statusNotes : undefined,
      });
      setAllocation(updated);
      toast.success(`Allocation marked as ${formatLabel(nextStatus)}`);
      setStatusDialogOpen(false);
    } catch (error: any) {
      toast.error(error.message || 'Failed to update allocation status');
    } finally {
      setActionLoading(false);
    }
  };

  const handleTransferAllocation = async () => {
    if (!allocation) return;
    if (!transferBusinessPartnerId && !transferCustomerName.trim()) {
      toast.error('Select a customer or enter a transfer recipient name.');
      return;
    }

    try {
      setActionLoading(true);
      const updated = await salesAllocationService.transferAllocation(allocation.id, {
        businessPartnerId: transferBusinessPartnerId || undefined,
        customerName: transferCustomerName.trim() || undefined,
        clearLinkedDocuments,
        notes: transferNotes.trim() || undefined,
      });
      setAllocation(updated);
      toast.success('Allocation transferred');
      setTransferDialogOpen(false);
    } catch (error: any) {
      toast.error(error.message || 'Failed to transfer allocation');
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const config = STATUS_CONFIG[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={config.variant} className={config.className}>{formatLabel(status)}</Badge>;
  };

  const workflow = useWorkflowRecord({
    entityType: 'SalesAllocation',
    entityId: allocationId,
    entityLabel: 'Sales Allocation',
    entityNumber: allocation?.sourceItemCode || allocation?.sourceItemName,
    status: allocation?.status ?? '',
    canSubmit: allocation?.status === 'Reserved',
    canApproveReject: allocation?.status === 'PendingApproval',
    enabled: Boolean(allocation),
    commands: {
      submit: async () => setAllocation(await salesAllocationService.submitForApproval(allocationId)),
      approve: async ({ comments }) => setAllocation(await salesAllocationService.processApproval(allocationId, { isApproved: true, comments })),
      reject: async ({ comments }) => setAllocation(await salesAllocationService.processApproval(allocationId, {
        isApproved: false,
        comments,
        rejectionReason: comments,
      })),
      afterAction: loadAllocation,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="py-16 text-center text-gray-500">
          <MapPinned className="mx-auto mb-4 h-12 w-12 animate-pulse text-emerald-500" />
          Loading allocation...
        </div>
      </div>
    );
  }

  if (!allocation) {
    return (
      <div className="container mx-auto py-6">
        <div className="py-16 text-center text-gray-500">
          <AlertTriangle className="mx-auto mb-4 h-12 w-12 text-yellow-500" />
          Allocation not found
          <div className="mt-4">
            <Button variant="outline" onClick={() => router.push('/sales/allocations')}>
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to Allocations
            </Button>
          </div>
        </div>
      </div>
    );
  }

  const isActive = ACTIVE_STATUSES.has(allocation.status);
  // Reserved allocations use Submit, which centrally chooses direct completion or approval.
  const canMarkAllocated = allocation.status === 'Approved' && workflow.visibility.known && !workflow.visibility.active;
  const canRelease = isActive && !['Sold', 'Leased'].includes(allocation.status);
  const canCancel = isActive && !['Sold', 'Leased'].includes(allocation.status);
  const canTransfer = isActive && allocation.status !== 'PendingApproval';
  const entityNumber = allocation.sourceItemCode || allocation.sourceItemName;

  return (
    <div className="container mx-auto space-y-6 py-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/allocations')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="flex items-center gap-2 text-2xl font-bold">
              <MapPinned className="h-6 w-6 text-emerald-600" />
              {allocation.sourceItemName}
            </h1>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-gray-500">
              {getStatusBadge(allocation.status)}
              <span>{allocation.sourceCode}</span>
              <span>•</span>
              <span>{formatLabel(allocation.allocationType)}</span>
            </div>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <WorkflowApprovalActions
            {...workflow.actionProps}
            showStepBadge
          />
          {canMarkAllocated ? (
            <Button variant="outline" onClick={() => openStatusDialog('Allocated')}>
              <CheckCircle2 className="mr-2 h-4 w-4" />
              Mark Allocated
            </Button>
          ) : null}
          {canTransfer ? (
            <Button variant="outline" onClick={openTransferDialog}>
              <ArrowRightLeft className="mr-2 h-4 w-4" />
              Transfer
            </Button>
          ) : null}
          {canRelease ? (
            <Button variant="outline" onClick={() => openStatusDialog('Released')}>
              <RotateCcw className="mr-2 h-4 w-4" />
              Release
            </Button>
          ) : null}
          {canCancel ? (
            <Button variant="destructive" onClick={() => openStatusDialog('Cancelled')}>
              <XCircle className="mr-2 h-4 w-4" />
              Cancel
            </Button>
          ) : null}
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Status</CardTitle></CardHeader>
          <CardContent>{getStatusBadge(allocation.status)}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Customer</CardTitle></CardHeader>
          <CardContent><p className="truncate text-lg font-semibold">{allocation.customerName || '-'}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Agreed Value</CardTitle></CardHeader>
          <CardContent><p className="text-lg font-semibold text-blue-600">{formatAmount(allocation.agreedValue, allocation.currency)}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Reserved Until</CardTitle></CardHeader>
          <CardContent><p className="text-lg font-semibold">{formatDate(allocation.reservedUntil)}</p></CardContent>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
        <Card>
          <CardHeader>
            <CardTitle>Allocation Details</CardTitle>
            <CardDescription>Source item, customer, value, and linked commercial document.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Saleable Source</p>
                <p className="font-medium">{allocation.sourceCode}</p>
                <p className="text-sm text-gray-500">{formatLabel(allocation.sourceType)} • {allocation.adapterKey}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Source Item</p>
                <p className="font-medium">{allocation.sourceItemName}</p>
                <p className="text-sm text-gray-500">{allocation.sourceItemCode || allocation.sourceItemId}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Allocation Type</p>
                <p className="font-medium">{formatLabel(allocation.allocationType)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Item Type</p>
                <p className="font-medium">{formatLabel(allocation.sourceItemType)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Estimated Value</p>
                <p className="font-medium">{formatAmount(allocation.estimatedValue, allocation.currency)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Effective Date</p>
                <p className="font-medium">{formatDate(allocation.effectiveDate)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Released Date</p>
                <p className="font-medium">{formatDate(allocation.releasedDate)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Created</p>
                <p className="font-medium">{formatDate(allocation.createdAt, true)}</p>
              </div>
            </div>

            <Separator />

            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Sales Order</p>
                {allocation.salesOrderId ? (
                  <Button variant="link" className="h-auto p-0 text-blue-600" onClick={() => router.push(`/sales/orders/${allocation.salesOrderId}`)}>
                    {allocation.salesOrderNumber || 'Open Sales Order'}
                    <ArrowUpRight className="ml-1 h-3 w-3" />
                  </Button>
                ) : (
                  <p className="font-medium">-</p>
                )}
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500">Sales Agreement</p>
                {allocation.salesAgreementId ? (
                  <Button variant="link" className="h-auto p-0 text-blue-600" onClick={() => router.push(`/sales/agreements/${allocation.salesAgreementId}`)}>
                    {allocation.salesAgreementTitle || 'Open Sales Agreement'}
                    <ArrowUpRight className="ml-1 h-3 w-3" />
                  </Button>
                ) : (
                  <p className="font-medium">-</p>
                )}
              </div>
            </div>

            {allocation.notes ? (
              <>
                <Separator />
                <div>
                  <p className="text-xs uppercase tracking-wide text-gray-500">Notes</p>
                  <p className="mt-1 whitespace-pre-wrap text-sm text-gray-700">{allocation.notes}</p>
                </div>
              </>
            ) : null}

            {allocation.releaseReason ? (
              <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                <p className="font-medium">Release Reason</p>
                <p className="mt-1">{allocation.releaseReason}</p>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5" />
              Ledger History
            </CardTitle>
            <CardDescription>{allocation.history.length} event(s)</CardDescription>
          </CardHeader>
          <CardContent>
            {allocation.history.length === 0 ? (
              <p className="text-sm text-gray-500">No allocation history has been recorded yet.</p>
            ) : (
              <div className="space-y-3">
                {allocation.history.map((entry) => (
                  <div key={entry.id} className="rounded-lg border bg-slate-50 p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-medium">{formatLabel(entry.action)}</p>
                        <div className="mt-1 flex items-center gap-2">
                          {entry.fromStatus ? getStatusBadge(entry.fromStatus) : <Badge variant="outline">New</Badge>}
                          <span className="text-gray-400">→</span>
                          {getStatusBadge(entry.toStatus)}
                        </div>
                      </div>
                      <p className="text-right text-xs text-gray-500">{formatDate(entry.performedAt, true)}</p>
                    </div>
                    {entry.notes ? <p className="mt-2 text-sm text-gray-600">{entry.notes}</p> : null}
                    {entry.performedByName ? <p className="mt-2 text-xs text-gray-500">By {entry.performedByName}</p> : null}
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {workflow.visibility.showTab && (
        <WorkflowApprovalHistoryPanel
          {...workflow.actionProps}
          showActions={false}
        />
      )}

      <Dialog open={transferDialogOpen} onOpenChange={setTransferDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Transfer Allocation</DialogTitle>
            <DialogDescription>
              Move this allocation to another customer or prospect and keep the transfer trail in the ledger history.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Customer Account</Label>
              <Select
                value={transferBusinessPartnerId || 'manual'}
                onValueChange={(value) => {
                  if (value === 'manual') {
                    setTransferBusinessPartnerId('');
                    return;
                  }

                  const partner = businessPartners.find((item) => item.id === value);
                  setTransferBusinessPartnerId(value);
                  setTransferCustomerName(partner?.partnerName || '');
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={loadingPartners ? 'Loading customers...' : 'Select customer account'} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="manual">Free-text recipient</SelectItem>
                  {businessPartners.map((partner) => (
                    <SelectItem key={partner.id} value={partner.id}>
                      {[partner.partnerName, partner.partnerCode, partner.partnerType].filter(Boolean).join(' | ')}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Transfer Recipient</Label>
              <Input
                value={transferCustomerName}
                onChange={(event) => setTransferCustomerName(event.target.value)}
                placeholder="Customer or prospect name"
              />
            </div>
            <label className="flex items-start gap-3 rounded-md border bg-slate-50 p-3 text-sm">
              <input
                type="checkbox"
                className="mt-1 h-4 w-4"
                checked={clearLinkedDocuments}
                onChange={(event) => setClearLinkedDocuments(event.target.checked)}
              />
              <span>
                <span className="block font-medium">Clear linked sales documents</span>
                <span className="text-gray-500">
                  Recommended when the existing order or agreement belongs to the previous customer.
                </span>
              </span>
            </label>
            <div>
              <Label>Transfer Notes</Label>
              <Textarea
                value={transferNotes}
                onChange={(event) => setTransferNotes(event.target.value)}
                placeholder="Reason, approval reference, or transfer context..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTransferDialogOpen(false)}>Close</Button>
            <Button onClick={handleTransferAllocation} disabled={actionLoading || loadingPartners}>
              <ArrowRightLeft className="mr-2 h-4 w-4" />
              Transfer
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={statusDialogOpen} onOpenChange={setStatusDialogOpen}>
        <DialogTrigger asChild>
          <span className="hidden" />
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Mark Allocation As {formatLabel(nextStatus)}</DialogTitle>
            <DialogDescription>
              Update the allocation ledger and record the status change in history.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <Label>Reserved Until</Label>
                <Input type="date" value={reservedUntil} onChange={(event) => setReservedUntil(event.target.value)} />
              </div>
              <div>
                <Label>Agreed Value</Label>
                <Input
                  type="number"
                  min={0}
                  step={0.01}
                  value={agreedValue}
                  onChange={(event) => setAgreedValue(event.target.value)}
                />
              </div>
            </div>
            <div>
              <Label>{['Released', 'Cancelled', 'Expired', 'Rejected'].includes(nextStatus) ? 'Reason *' : 'Notes'}</Label>
              <Textarea
                placeholder="Add status change notes..."
                value={statusNotes}
                onChange={(event) => setStatusNotes(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStatusDialogOpen(false)}>Close</Button>
            <Button
              variant={['Released', 'Cancelled', 'Expired', 'Rejected'].includes(nextStatus) ? 'destructive' : 'default'}
              onClick={handleStatusUpdate}
              disabled={actionLoading}
            >
              {nextStatus === 'Cancelled' ? <Ban className="mr-2 h-4 w-4" /> : <FileText className="mr-2 h-4 w-4" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
