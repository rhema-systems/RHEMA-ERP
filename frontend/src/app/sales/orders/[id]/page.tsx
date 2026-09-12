'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger,
} from '@/components/ui/dialog';
import { WorkflowApprovalActions, WorkflowApprovalHistoryPanel, useWorkflowRecord } from '@/components/workflow';
import {
  ShoppingCart, ArrowLeft, CheckCircle, XCircle, Pause, Play,
  Lock, Truck, Clock, AlertTriangle, Building2, Home
} from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type SalesOrderDetailDto } from '@/services/salesOrderService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Draft: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
  PendingApproval: { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
  Approved: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
  Confirmed: { variant: 'default', className: 'bg-indigo-100 text-indigo-800' },
  InProgress: { variant: 'default', className: 'bg-cyan-100 text-cyan-800' },
  PartiallyDelivered: { variant: 'secondary', className: 'bg-orange-100 text-orange-800' },
  FullyDelivered: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Invoiced: { variant: 'default', className: 'bg-emerald-100 text-emerald-800' },
  OnHold: { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
  Cancelled: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
  Closed: { variant: 'outline', className: 'bg-slate-100 text-slate-700' },
  Rejected: { variant: 'destructive', className: 'bg-red-50 text-red-700' },
};

export default function SalesOrderDetailPage() {
  const params = useParams();
  const router = useRouter();
  const orderId = params.id as string;

  const [order, setOrder] = useState<SalesOrderDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);

  // Dialog states
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [holdDialogOpen, setHoldDialogOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [holdReason, setHoldReason] = useState('');

  useEffect(() => {
    if (orderId) loadOrder();
  }, [orderId]);

  const loadOrder = async () => {
    try {
      setLoading(true);
      const data = await salesOrderService.getSalesOrderById(orderId);
      setOrder(data);
    } catch (error) {
      console.error('Error loading sales order:', error);
      toast.error('Failed to load sales order');
    } finally {
      setLoading(false);
    }
  };

  const handleAction = async (action: () => Promise<SalesOrderDetailDto>, successMessage: string) => {
    try {
      setActionLoading(true);
      const updated = await action();
      setOrder(updated);
      toast.success(successMessage);
    } catch (error: any) {
      toast.error(error.message || 'Action failed');
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfirm = () => handleAction(
    () => salesOrderService.confirmSalesOrder(orderId),
    'Order confirmed'
  );

  const handleCancel = () => handleAction(
    () => salesOrderService.cancelSalesOrder(orderId, { reason: cancelReason }),
    'Order cancelled'
  ).then(() => setCancelDialogOpen(false));

  const handleHold = () => handleAction(
    () => salesOrderService.putOnHold(orderId, holdReason),
    'Order put on hold'
  ).then(() => setHoldDialogOpen(false));

  const handleRelease = () => handleAction(
    () => salesOrderService.releaseFromHold(orderId),
    'Order released from hold'
  );

  const handleClose = () => handleAction(
    () => salesOrderService.closeSalesOrder(orderId),
    'Order closed'
  );

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try { return format(new Date(dateString), 'dd MMM yyyy HH:mm'); } catch { return dateString; }
  };

  const formatCurrency = (amount: number) =>
    `${order?.currency || 'GHS'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  const formatLabel = (value?: string) => value ? value.replace(/([A-Z])/g, ' $1').trim() : '-';

  const getStatusBadge = (status: string) => {
    const c = STATUS_CONFIG[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status.replace(/([A-Z])/g, ' $1').trim()}</Badge>;
  };

  const workflow = useWorkflowRecord({
    entityType: 'SalesOrder',
    entityId: orderId,
    entityLabel: 'Sales Order',
    entityNumber: order?.orderNumber,
    status: order?.status ?? '',
    canSubmit: order?.status === 'Draft',
    canApproveReject: order?.status === 'PendingApproval',
    enabled: Boolean(order),
    commands: {
      submit: async () => setOrder(await salesOrderService.submitForApproval(orderId)),
      approve: async ({ comments }) => setOrder(await salesOrderService.processApproval(orderId, { isApproved: true, comments })),
      reject: async ({ comments }) => setOrder(await salesOrderService.processApproval(orderId, { isApproved: false, comments })),
      afterAction: loadOrder,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-16">
          <ShoppingCart className="h-12 w-12 animate-pulse mx-auto mb-4 text-blue-500" />
          <p className="text-gray-500">Loading sales order...</p>
        </div>
      </div>
    );
  }

  if (!order) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-16">
          <AlertTriangle className="h-12 w-12 mx-auto mb-4 text-yellow-500" />
          <p className="text-gray-500">Sales order not found</p>
          <Button variant="outline" className="mt-4" onClick={() => router.push('/sales/orders')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Back to Orders
          </Button>
        </div>
      </div>
    );
  }

  const canConfirm = order.status === 'Approved';
  const canCancel = ['Draft', 'PendingApproval', 'Approved', 'Confirmed'].includes(order.status);
  const canHold = ['Confirmed', 'InProgress'].includes(order.status);
  const canRelease = order.status === 'OnHold';
  const canClose = ['FullyDelivered', 'Invoiced'].includes(order.status);
  const canCreateDelivery = ['Confirmed', 'InProgress', 'PartiallyDelivered'].includes(order.status);

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/orders')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <ShoppingCart className="h-6 w-6 text-blue-600" />
              {order.orderNumber}
            </h1>
            <div className="flex items-center gap-2 mt-1">
              {getStatusBadge(order.status)}
              <span className="text-sm text-gray-500">•</span>
              <span className="text-sm text-gray-500">{order.customerName}</span>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-2 flex-wrap">
          <WorkflowApprovalActions
            {...workflow.actionProps}
            showStepBadge
          />
          {canConfirm && (
            <Button onClick={handleConfirm} disabled={actionLoading}>
              <CheckCircle className="h-4 w-4 mr-2" />Confirm Order
            </Button>
          )}
          {canCreateDelivery && (
            <Button variant="outline" onClick={() => router.push(`/sales/deliveries/create?orderId=${orderId}`)}>
              <Truck className="h-4 w-4 mr-2" />Create Delivery
            </Button>
          )}
          {canHold && (
            <Dialog open={holdDialogOpen} onOpenChange={setHoldDialogOpen}>
              <DialogTrigger asChild>
                <Button variant="outline"><Pause className="h-4 w-4 mr-2" />Hold</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Put Order On Hold</DialogTitle>
                  <DialogDescription>Provide a reason for holding this order.</DialogDescription>
                </DialogHeader>
                <Textarea placeholder="Hold reason..." value={holdReason} onChange={(e) => setHoldReason(e.target.value)} />
                <DialogFooter>
                  <Button onClick={handleHold} disabled={actionLoading}>Confirm Hold</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          )}
          {canRelease && (
            <Button variant="outline" onClick={handleRelease} disabled={actionLoading}>
              <Play className="h-4 w-4 mr-2" />Release Hold
            </Button>
          )}
          {canClose && (
            <Button variant="outline" onClick={handleClose} disabled={actionLoading}>
              <Lock className="h-4 w-4 mr-2" />Close Order
            </Button>
          )}
          {canCancel && (
            <Dialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
              <DialogTrigger asChild>
                <Button variant="destructive"><XCircle className="h-4 w-4 mr-2" />Cancel</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Cancel Sales Order</DialogTitle>
                  <DialogDescription>This action cannot be undone. Please provide a reason.</DialogDescription>
                </DialogHeader>
                <Textarea placeholder="Cancellation reason..." value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
                <DialogFooter>
                  <Button variant="destructive" onClick={handleCancel} disabled={actionLoading || !cancelReason}>
                    Confirm Cancellation
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          )}
        </div>
      </div>

      {order.projectUnitContext && (
        <Card className="border-emerald-200 bg-emerald-50/40">
          <CardContent className="flex flex-col gap-4 pt-6 lg:flex-row lg:items-start lg:justify-between">
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-sm font-medium text-emerald-700">
                <Building2 className="h-4 w-4" />
                Project-Linked Unit
              </div>
              <div>
                <p className="text-base font-semibold text-slate-900">
                  {order.projectUnitContext.projectCode}
                  {order.projectUnitContext.projectTitle ? ` • ${order.projectUnitContext.projectTitle}` : ''}
                </p>
                <p className="text-sm text-slate-600">
                  {order.projectUnitContext.projectUnitCode || order.projectUnitContext.projectUnitName}
                  {order.projectUnitContext.projectUnitCode && order.projectUnitContext.projectUnitName
                    ? ` • ${order.projectUnitContext.projectUnitName}`
                    : ''}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <Badge variant="outline">{formatLabel(order.projectUnitContext.projectUnitType)}</Badge>
                <Badge variant="outline">{formatLabel(order.projectUnitContext.projectUnitStatus)}</Badge>
                <Badge variant="secondary">{formatLabel(order.projectUnitContext.projectUnitCommercialStatus)}</Badge>
                <Badge variant="outline">{formatLabel(order.projectUnitContext.projectUnitHandoverStatus)}</Badge>
              </div>
              <div className="grid gap-3 text-sm text-slate-600 md:grid-cols-3">
                <div>
                  <p className="text-xs uppercase tracking-wide text-slate-500">Market Release</p>
                  <p className="font-medium text-slate-900">{order.projectUnitContext.isReleasedForMarket ? 'Released' : 'Not Released'}</p>
                </div>
                <div>
                  <p className="text-xs uppercase tracking-wide text-slate-500">Handover</p>
                  <p className="font-medium text-slate-900">{formatDate(order.projectUnitContext.handoverDate)}</p>
                </div>
                <div>
                  <p className="text-xs uppercase tracking-wide text-slate-500">Unit Name</p>
                  <p className="font-medium text-slate-900">{order.projectUnitContext.projectUnitName}</p>
                </div>
              </div>
            </div>
            <Button
              variant="outline"
              className="w-full lg:w-auto"
              onClick={() => {
                const projectId = order.projectUnitContext?.projectId;
                if (projectId) {
                  router.push(`/development/projects/${projectId}/units`);
                }
              }}
            >
              <Home className="mr-2 h-4 w-4" />
              Open Project Unit
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Order Details */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main Info */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Order Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div>
                <p className="text-sm text-gray-500">Order Date</p>
                <p className="font-medium">{formatDate(order.orderDate)}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Order Type</p>
                <p className="font-medium">{order.orderType || 'Standard'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Priority</p>
                <Badge variant={order.priority === 'High' || order.priority === 'Urgent' ? 'destructive' : 'outline'}>
                  {order.priority}
                </Badge>
              </div>
              <div>
                <p className="text-sm text-gray-500">Expected Delivery</p>
                <p className="font-medium">{formatDate(order.expectedDeliveryDate)}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Payment Terms</p>
                <p className="font-medium">{order.paymentTerms || '-'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Customer PO #</p>
                <p className="font-medium">{order.customerPoNumber || '-'}</p>
              </div>
              {order.propertyReference && (
                <div>
                  <p className="text-sm text-gray-500">Property Reference</p>
                  <p className="font-medium">{order.propertyReference}</p>
                </div>
              )}
              {order.propertyType && (
                <div>
                  <p className="text-sm text-gray-500">Property Type</p>
                  <p className="font-medium">{order.propertyType}</p>
                </div>
              )}
              {order.salesRepName && (
                <div>
                  <p className="text-sm text-gray-500">Sales Rep</p>
                  <p className="font-medium">{order.salesRepName}</p>
                </div>
              )}
            </div>

            {order.shippingAddress && (
              <>
                <Separator />
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <p className="text-sm text-gray-500">Shipping Address</p>
                    <p className="font-medium whitespace-pre-line">{order.shippingAddress}</p>
                  </div>
                  {order.billingAddress && (
                    <div>
                      <p className="text-sm text-gray-500">Billing Address</p>
                      <p className="font-medium whitespace-pre-line">{order.billingAddress}</p>
                    </div>
                  )}
                </div>
              </>
            )}

            {(order.notes || order.internalNotes) && (
              <>
                <Separator />
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {order.notes && (
                    <div>
                      <p className="text-sm text-gray-500">Notes</p>
                      <p className="text-sm">{order.notes}</p>
                    </div>
                  )}
                  {order.internalNotes && (
                    <div>
                      <p className="text-sm text-gray-500">Internal Notes</p>
                      <p className="text-sm italic">{order.internalNotes}</p>
                    </div>
                  )}
                </div>
              </>
            )}

            {(order.rejectionReason || order.cancellationReason || order.holdReason) && (
              <>
                <Separator />
                <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                  {order.rejectionReason && (
                    <div><p className="text-sm font-semibold text-red-700">Rejection Reason</p><p className="text-sm text-red-600">{order.rejectionReason}</p></div>
                  )}
                  {order.cancellationReason && (
                    <div><p className="text-sm font-semibold text-red-700">Cancellation Reason</p><p className="text-sm text-red-600">{order.cancellationReason}</p></div>
                  )}
                  {order.holdReason && (
                    <div><p className="text-sm font-semibold text-amber-700">Hold Reason</p><p className="text-sm text-amber-600">{order.holdReason}</p></div>
                  )}
                </div>
              </>
            )}
          </CardContent>
        </Card>

        {/* Summary & Delivery Progress */}
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Order Summary</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex justify-between"><span className="text-gray-500">Subtotal</span><span className="font-medium">{formatCurrency(order.subtotalAmount)}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Discount</span><span className="font-medium text-red-500">-{formatCurrency(order.discountAmount)}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Tax</span><span className="font-medium">{formatCurrency(order.taxAmount)}</span></div>
              <Separator />
              <div className="flex justify-between"><span className="font-bold text-lg">Total</span><span className="font-bold text-lg text-blue-600">{formatCurrency(order.totalAmount)}</span></div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Delivery Progress</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-3">
                <div className="flex-1 bg-gray-200 rounded-full h-3">
                  <div className="bg-green-500 h-3 rounded-full transition-all" style={{ width: `${Math.min(order.deliveryProgress, 100)}%` }} />
                </div>
                <span className="font-bold text-lg">{order.deliveryProgress}%</span>
              </div>
              {order.approvedByName && (
                <div className="mt-3 text-sm text-gray-500">
                  <p>Approved by: {order.approvedByName}</p>
                  <p>Approved: {formatDate(order.approvedDate)}</p>
                </div>
              )}
              {order.confirmedDate && (
                <p className="mt-1 text-sm text-gray-500">Confirmed: {formatDate(order.confirmedDate)}</p>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Order Lines */}
      <Card>
        <CardHeader>
          <CardTitle>Order Lines</CardTitle>
          <CardDescription>{order.lines.length} item(s)</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>#</TableHead>
                <TableHead>Item</TableHead>
                <TableHead className="text-right">Qty</TableHead>
                <TableHead>UoM</TableHead>
                <TableHead className="text-right">Unit Price</TableHead>
                <TableHead className="text-right">Disc %</TableHead>
                <TableHead className="text-right">Tax %</TableHead>
                <TableHead className="text-right">Line Total</TableHead>
                <TableHead className="text-right">Delivered</TableHead>
                <TableHead className="text-right">Remaining</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {order.lines.map((line) => (
                <TableRow key={line.id}>
                  <TableCell className="text-gray-500">{line.lineNumber}</TableCell>
                  <TableCell>
                    <div>
                      <p className="font-medium">{line.itemName}</p>
                      {line.itemCode && <p className="text-xs text-gray-500">{line.itemCode}</p>}
                      {line.description && <p className="text-xs text-gray-400">{line.description}</p>}
                    </div>
                  </TableCell>
                  <TableCell className="text-right">{line.quantity}</TableCell>
                  <TableCell>{line.unitOfMeasure}</TableCell>
                  <TableCell className="text-right">{formatCurrency(line.unitPrice)}</TableCell>
                  <TableCell className="text-right">{line.discountPercent}%</TableCell>
                  <TableCell className="text-right">{line.taxPercent}%</TableCell>
                  <TableCell className="text-right font-semibold">{formatCurrency(line.lineTotal)}</TableCell>
                  <TableCell className="text-right">
                    <span className={line.deliveredQuantity > 0 ? 'text-green-600 font-medium' : ''}>
                      {line.deliveredQuantity}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    <span className={line.remainingQuantity > 0 ? 'text-orange-600 font-medium' : 'text-green-600'}>
                      {line.remainingQuantity}
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {workflow.visibility.showTab && (
        <WorkflowApprovalHistoryPanel
          {...workflow.actionProps}
          showActions={false}
        />
      )}

      {/* Status History */}
      {order.statusHistory && order.statusHistory.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5" />
              Status History
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {order.statusHistory.map((entry) => (
                <div key={entry.id} className="flex items-start gap-3 p-3 bg-gray-50 rounded-lg">
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      {getStatusBadge(entry.fromStatus)}
                      <span className="text-gray-400">→</span>
                      {getStatusBadge(entry.toStatus)}
                    </div>
                    {entry.reason && <p className="text-sm text-gray-600 mt-1">{entry.reason}</p>}
                  </div>
                  <div className="text-right text-sm text-gray-500">
                    <p>{entry.changedByName}</p>
                    <p>{formatDate(entry.changedDate)}</p>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
