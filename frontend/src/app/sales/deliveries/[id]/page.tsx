'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger,
} from '@/components/ui/dialog';
import {
  Truck, ArrowLeft, Package, PackageCheck, XCircle, AlertTriangle, Send
} from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type DeliveryNoteDetailDto } from '@/services/salesOrderService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Draft: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
  Packed: { variant: 'secondary', className: 'bg-blue-100 text-blue-800' },
  Shipped: { variant: 'default', className: 'bg-indigo-100 text-indigo-800' },
  Delivered: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Cancelled: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
};

export default function DeliveryNoteDetailPage() {
  const params = useParams();
  const router = useRouter();
  const deliveryId = params.id as string;

  const [delivery, setDelivery] = useState<DeliveryNoteDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);

  // Ship dialog
  const [shipDialogOpen, setShipDialogOpen] = useState(false);
  const [carrierName, setCarrierName] = useState('');
  const [trackingNumber, setTrackingNumber] = useState('');

  // Confirm dialog
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [receivedByName, setReceivedByName] = useState('');
  const [confirmNotes, setConfirmNotes] = useState('');

  // Cancel dialog
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  useEffect(() => {
    if (deliveryId) loadDelivery();
  }, [deliveryId]);

  const loadDelivery = async () => {
    try {
      setLoading(true);
      const data = await salesOrderService.getDeliveryNoteById(deliveryId);
      setDelivery(data);
    } catch (error) {
      console.error('Error loading delivery note:', error);
      toast.error('Failed to load delivery note');
    } finally {
      setLoading(false);
    }
  };

  const handleAction = async (action: () => Promise<DeliveryNoteDetailDto>, successMessage: string) => {
    try {
      setActionLoading(true);
      const updated = await action();
      setDelivery(updated);
      toast.success(successMessage);
    } catch (error: any) {
      toast.error(error.message || 'Action failed');
    } finally {
      setActionLoading(false);
    }
  };

  const handlePack = () => handleAction(
    () => salesOrderService.markAsPacked(deliveryId),
    'Delivery marked as packed'
  );

  const handleShip = () => handleAction(
    () => salesOrderService.markAsShipped(deliveryId, carrierName || undefined, trackingNumber || undefined),
    'Delivery marked as shipped'
  ).then(() => setShipDialogOpen(false));

  const handleConfirm = () => handleAction(
    () => salesOrderService.confirmDelivery(deliveryId, {
      receivedByName: receivedByName || undefined,
      notes: confirmNotes || undefined,
    }),
    'Delivery confirmed'
  ).then(() => setConfirmDialogOpen(false));

  const handleCancel = () => handleAction(
    () => salesOrderService.cancelDeliveryNote(deliveryId, cancelReason || undefined),
    'Delivery note cancelled'
  ).then(() => setCancelDialogOpen(false));

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try { return format(new Date(dateString), 'dd MMM yyyy HH:mm'); } catch { return dateString; }
  };

  const getStatusBadge = (status: string) => {
    const c = STATUS_CONFIG[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-16">
          <Truck className="h-12 w-12 animate-pulse mx-auto mb-4 text-green-500" />
          <p className="text-gray-500">Loading delivery note...</p>
        </div>
      </div>
    );
  }

  if (!delivery) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-16">
          <AlertTriangle className="h-12 w-12 mx-auto mb-4 text-yellow-500" />
          <p className="text-gray-500">Delivery note not found</p>
          <Button variant="outline" className="mt-4" onClick={() => router.push('/sales/deliveries')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Back to Deliveries
          </Button>
        </div>
      </div>
    );
  }

  const canPack = delivery.status === 'Draft';
  const canShip = delivery.status === 'Packed';
  const canConfirm = delivery.status === 'Shipped';
  const canCancel = ['Draft', 'Packed'].includes(delivery.status);

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/deliveries')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <Truck className="h-6 w-6 text-green-600" />
              {delivery.deliveryNumber}
            </h1>
            <div className="flex items-center gap-2 mt-1">
              {getStatusBadge(delivery.status)}
              <span className="text-sm text-gray-500">•</span>
              <span className="text-sm text-gray-500">{delivery.customerName}</span>
              <span className="text-sm text-gray-500">•</span>
              <button className="text-sm text-blue-600 hover:underline" onClick={() => router.push(`/sales/orders/${delivery.salesOrderId}`)}>
                SO: {delivery.salesOrderNumber}
              </button>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-2">
          {canPack && (
            <Button onClick={handlePack} disabled={actionLoading}>
              <Package className="h-4 w-4 mr-2" />Mark Packed
            </Button>
          )}
          {canShip && (
            <Dialog open={shipDialogOpen} onOpenChange={setShipDialogOpen}>
              <DialogTrigger asChild>
                <Button><Send className="h-4 w-4 mr-2" />Ship</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Ship Delivery</DialogTitle>
                  <DialogDescription>Enter carrier and tracking details.</DialogDescription>
                </DialogHeader>
                <div className="space-y-3">
                  <div><Label>Carrier Name</Label><Input placeholder="e.g., DHL, FedEx" value={carrierName} onChange={(e) => setCarrierName(e.target.value)} /></div>
                  <div><Label>Tracking Number</Label><Input placeholder="Tracking number" value={trackingNumber} onChange={(e) => setTrackingNumber(e.target.value)} /></div>
                </div>
                <DialogFooter>
                  <Button onClick={handleShip} disabled={actionLoading}>Confirm Shipment</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          )}
          {canConfirm && (
            <Dialog open={confirmDialogOpen} onOpenChange={setConfirmDialogOpen}>
              <DialogTrigger asChild>
                <Button variant="default"><PackageCheck className="h-4 w-4 mr-2" />Confirm Delivery</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Confirm Delivery</DialogTitle>
                  <DialogDescription>Confirm that the customer has received the goods.</DialogDescription>
                </DialogHeader>
                <div className="space-y-3">
                  <div><Label>Received By</Label><Input placeholder="Name of receiver" value={receivedByName} onChange={(e) => setReceivedByName(e.target.value)} /></div>
                  <div><Label>Notes</Label><Textarea placeholder="Delivery notes" value={confirmNotes} onChange={(e) => setConfirmNotes(e.target.value)} /></div>
                </div>
                <DialogFooter>
                  <Button onClick={handleConfirm} disabled={actionLoading}>Confirm</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          )}
          {canCancel && (
            <Dialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
              <DialogTrigger asChild>
                <Button variant="destructive"><XCircle className="h-4 w-4 mr-2" />Cancel</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Cancel Delivery Note</DialogTitle>
                  <DialogDescription>This action cannot be undone.</DialogDescription>
                </DialogHeader>
                <Textarea placeholder="Cancellation reason..." value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
                <DialogFooter>
                  <Button variant="destructive" onClick={handleCancel} disabled={actionLoading}>Confirm Cancel</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          )}
        </div>
      </div>

      {/* Delivery Details */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader><CardTitle>Delivery Information</CardTitle></CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div>
                <p className="text-sm text-gray-500">Carrier</p>
                <p className="font-medium">{delivery.carrierName || '-'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Tracking Number</p>
                <p className="font-medium">{delivery.trackingNumber || '-'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Received By</p>
                <p className="font-medium">{delivery.receivedByName || '-'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Shipping Address</p>
                <p className="font-medium whitespace-pre-line">{delivery.shippingAddress || '-'}</p>
              </div>
              {delivery.notes && (
                <div className="col-span-2">
                  <p className="text-sm text-gray-500">Notes</p>
                  <p className="text-sm">{delivery.notes}</p>
                </div>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-lg">Timeline</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <div className="flex justify-between text-sm"><span className="text-gray-500">Created</span><span>{formatDate(delivery.createdAt)}</span></div>
            {delivery.createdByName && <div className="flex justify-between text-sm"><span className="text-gray-500">Created By</span><span>{delivery.createdByName}</span></div>}
            {delivery.packedDate && <div className="flex justify-between text-sm"><span className="text-gray-500">Packed</span><span>{formatDate(delivery.packedDate)}</span></div>}
            {delivery.shippedDate && <div className="flex justify-between text-sm"><span className="text-gray-500">Shipped</span><span>{formatDate(delivery.shippedDate)}</span></div>}
            {delivery.deliveredDate && <div className="flex justify-between text-sm"><span className="text-gray-500">Delivered</span><span className="text-green-600 font-medium">{formatDate(delivery.deliveredDate)}</span></div>}
          </CardContent>
        </Card>
      </div>

      {/* Line Items */}
      <Card>
        <CardHeader>
          <CardTitle>Line Items</CardTitle>
          <CardDescription>{delivery.lines.length} item(s)</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Item</TableHead>
                <TableHead>Code</TableHead>
                <TableHead className="text-right">Qty to Deliver</TableHead>
                <TableHead>UoM</TableHead>
                <TableHead className="text-right">Actually Delivered</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {delivery.lines.map((line) => (
                <TableRow key={line.id}>
                  <TableCell className="font-medium">{line.itemName}</TableCell>
                  <TableCell className="text-gray-500">{line.itemCode || '-'}</TableCell>
                  <TableCell className="text-right">{line.quantity}</TableCell>
                  <TableCell>{line.unitOfMeasure}</TableCell>
                  <TableCell className="text-right">
                    <span className={line.deliveredQuantity >= line.quantity ? 'text-green-600 font-medium' : 'text-orange-600 font-medium'}>
                      {line.deliveredQuantity}
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
