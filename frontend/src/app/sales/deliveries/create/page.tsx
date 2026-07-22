'use client';

import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Checkbox } from '@/components/ui/checkbox';
import { Truck, ArrowLeft, Save, Search, Package, PackageCheck } from 'lucide-react';
import { toast } from 'sonner';
import {
  salesOrderService,
  type CreateDeliveryNoteDto,
  type SalesOrderLineDto,
  type SalesOrderDetailDto,
} from '@/services/salesOrderService';
import { apiService } from '@/services/api.service';

interface DeliverableLine extends SalesOrderLineDto {
  selected: boolean;
  deliverQty: number;
}

export default function CreateDeliveryNotePage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const preselectedOrderId = searchParams.get('orderId');

  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(false);

  // Order selection
  const [orderSearch, setOrderSearch] = useState('');
  const [orderResults, setOrderResults] = useState<any[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<SalesOrderDetailDto | null>(null);
  const [salesOrderId, setSalesOrderId] = useState(preselectedOrderId || '');

  // Delivery details
  const [shippingAddress, setShippingAddress] = useState('');
  const [notes, setNotes] = useState('');

  // Lines
  const [deliverableLines, setDeliverableLines] = useState<DeliverableLine[]>([]);

  useEffect(() => {
    if (preselectedOrderId) {
      loadOrder(preselectedOrderId);
    }
  }, [preselectedOrderId]);

  const searchOrders = async (query: string) => {
    setOrderSearch(query);
    if (query.length < 2) { setOrderResults([]); return; }
    try {
      const results = await salesOrderService.getSalesOrders(1, 10, query, 'Confirmed');
      setOrderResults(results.items || []);
    } catch {
      setOrderResults([]);
    }
  };

  const loadOrder = async (orderId: string) => {
    try {
      setLoading(true);
      const order = await salesOrderService.getSalesOrderById(orderId);
      setSelectedOrder(order);
      setSalesOrderId(order.id);
      setOrderSearch(order.orderNumber);
      setOrderResults([]);
      setShippingAddress(order.shippingAddress || '');

      // Load deliverable lines
      try {
        const lines = await salesOrderService.getDeliverableLines(orderId);
        setDeliverableLines(lines.map(l => ({
          ...l,
          selected: true,
          deliverQty: l.remainingQuantity,
        })));
      } catch {
        // Fallback: use order lines with remaining qty
        setDeliverableLines(order.lines
          .filter(l => l.remainingQuantity > 0)
          .map(l => ({
            ...l,
            selected: true,
            deliverQty: l.remainingQuantity,
          }))
        );
      }
    } catch (error) {
      toast.error('Failed to load sales order');
    } finally {
      setLoading(false);
    }
  };

  const selectOrder = (order: any) => {
    loadOrder(order.id);
  };

  const toggleLine = (lineId: string, checked: boolean) => {
    setDeliverableLines(lines =>
      lines.map(l => l.id === lineId ? { ...l, selected: checked } : l)
    );
  };

  const updateDeliverQty = (lineId: string, qty: number) => {
    setDeliverableLines(lines =>
      lines.map(l => l.id === lineId ? { ...l, deliverQty: Math.min(qty, l.remainingQuantity) } : l)
    );
  };

  const selectedLines = deliverableLines.filter(l => l.selected && l.deliverQty > 0);

  const handleSave = async () => {
    if (!salesOrderId) { toast.error('Please select a sales order'); return; }
    if (selectedLines.length === 0) { toast.error('Please select at least one line to deliver'); return; }

    const dto: CreateDeliveryNoteDto = {
      salesOrderId,
      shippingAddress: shippingAddress || undefined,
      notes: notes || undefined,
      lines: selectedLines.map(l => ({
        salesOrderLineId: l.id,
        quantity: l.deliverQty,
      })),
    };

    try {
      setSaving(true);
      const result = await salesOrderService.createDeliveryNote(dto);
      toast.success(`Delivery Note ${result.deliveryNumber} created`);
      router.push(`/sales/deliveries/${result.id}`);
    } catch (error: any) {
      toast.error(error.message || 'Failed to create delivery note');
    } finally {
      setSaving(false);
    }
  };

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
              New Delivery Note
            </h1>
            <p className="text-gray-500">Create a delivery note from a confirmed sales order</p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={saving || selectedLines.length === 0}>
          <Save className="h-4 w-4 mr-2" />{saving ? 'Creating...' : 'Create Delivery Note'}
        </Button>
      </div>

      {/* Sales Order Selection */}
      <Card>
        <CardHeader><CardTitle>Sales Order</CardTitle></CardHeader>
        <CardContent>
          <div className="space-y-3">
            {!preselectedOrderId && (
              <div className="relative">
                <Label>Search Confirmed Sales Orders</Label>
                <div className="flex gap-2">
                  <Input
                    placeholder="Type order number or customer name..."
                    value={orderSearch}
                    onChange={(e) => searchOrders(e.target.value)}
                  />
                  <Button variant="outline" onClick={() => searchOrders(orderSearch)}>
                    <Search className="h-4 w-4" />
                  </Button>
                </div>
                {orderResults.length > 0 && (
                  <div className="absolute z-50 w-full mt-1 bg-white border rounded-md shadow-lg max-h-48 overflow-y-auto">
                    {orderResults.map((o: any) => (
                      <button
                        key={o.id}
                        className="w-full text-left px-4 py-2 hover:bg-gray-100 border-b last:border-0"
                        onClick={() => selectOrder(o)}
                      >
                        <p className="font-medium font-mono">{o.orderNumber}</p>
                        <p className="text-xs text-gray-500">{o.customerName} • GHS {o.totalAmount?.toLocaleString()}</p>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}

            {selectedOrder && (
              <div className="p-3 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="font-medium font-mono">{selectedOrder.orderNumber}</p>
                    <p className="text-sm text-gray-600">{selectedOrder.customerName}</p>
                  </div>
                  <div className="text-right">
                    <p className="font-semibold">GHS {selectedOrder.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
                    <p className="text-xs text-gray-500">Delivery: {selectedOrder.deliveryProgress}% complete</p>
                  </div>
                </div>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Shipping Details */}
      <Card>
        <CardHeader><CardTitle>Shipping Details</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label>Shipping Address</Label>
              <Textarea
                placeholder="Shipping address"
                value={shippingAddress}
                onChange={(e) => setShippingAddress(e.target.value)}
                rows={3}
              />
            </div>
            <div>
              <Label>Notes</Label>
              <Textarea
                placeholder="Delivery notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={3}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Deliverable Lines */}
      {deliverableLines.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Items to Deliver</CardTitle>
            <CardDescription>
              Select items and quantities to include in this delivery.
              {selectedLines.length > 0 && ` (${selectedLines.length} items selected)`}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="text-center py-8">
                <Package className="h-10 w-10 animate-pulse mx-auto mb-3 text-green-500" />
                <p className="text-gray-500">Loading deliverable items...</p>
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10">Select</TableHead>
                    <TableHead>Item</TableHead>
                    <TableHead className="text-right">Ordered</TableHead>
                    <TableHead className="text-right">Already Delivered</TableHead>
                    <TableHead className="text-right">Remaining</TableHead>
                    <TableHead className="w-32">Qty to Deliver</TableHead>
                    <TableHead>UoM</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {deliverableLines.map((line) => (
                    <TableRow key={line.id} className={!line.selected ? 'opacity-50' : ''}>
                      <TableCell>
                        <Checkbox
                          checked={line.selected}
                          onCheckedChange={(checked) => toggleLine(line.id, checked === true)}
                        />
                      </TableCell>
                      <TableCell>
                        <div>
                          <p className="font-medium">{line.itemName}</p>
                          {line.itemCode && <p className="text-xs text-gray-500">{line.itemCode}</p>}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">{line.quantity}</TableCell>
                      <TableCell className="text-right">{line.deliveredQuantity}</TableCell>
                      <TableCell className="text-right font-medium text-orange-600">{line.remainingQuantity}</TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          min={0}
                          max={line.remainingQuantity}
                          value={line.deliverQty}
                          onChange={(e) => updateDeliverQty(line.id, parseFloat(e.target.value) || 0)}
                          disabled={!line.selected}
                          className="w-24"
                        />
                      </TableCell>
                      <TableCell>{line.unitOfMeasure}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      {deliverableLines.length === 0 && selectedOrder && !loading && (
        <Card>
          <CardContent className="text-center py-8">
            <PackageCheck className="h-10 w-10 mx-auto mb-3 text-green-500" />
            <p className="text-gray-500">All items have been fully delivered for this order.</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
