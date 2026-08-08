'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Separator } from '@/components/ui/separator';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  ArrowLeft,
  Package,
  TruckIcon,
  Calendar,
  User,
  Building2,
  Loader2,
  AlertCircle,
  CheckCircle,
  XCircle,
  MapPin,
  FileText
} from 'lucide-react';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseOrderDetailDto,
  ProcurementPurchaseOrderSodReadinessDto,
  ProcurementReceiptSourceReadinessDto,
  ReceivePurchaseOrderDto,
  ReceivePurchaseOrderItemDto
} from '@/services/purchasingService';
import { inventoryManagementService, WarehouseDto, WarehouseLocationDto } from '@/services/inventoryManagementService';
import { ReceiptSourceControlCard } from '@/components/procurement/ReceiptSourceControlCard';
import { PurchaseOrderSodControl } from '@/components/procurement/PurchaseOrderSodControl';
import { format } from 'date-fns';

interface ReceiptItemFormData extends ReceivePurchaseOrderItemDto {
  itemCode: string;
  itemName: string;
  orderedQuantity: number;
  previouslyReceived: number;
  remainingQuantity: number;
  unitOfMeasure: string;
  itemUnitOfMeasureId?: string;
  poWarehouseId?: string;
  warehouseId?: string;
  warehouseName?: string;
}

const qualityStatusOptions = [
  { value: 'Passed', label: 'Passed', color: 'text-green-600' },
  { value: 'Failed', label: 'Failed', color: 'text-red-600' },
  { value: 'Conditional', label: 'Conditional', color: 'text-yellow-600' },
  { value: 'Pending', label: 'Pending', color: 'text-gray-600' }
];

export default function ReceivePurchaseOrderPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  
  const [order, setOrder] = useState<PurchaseOrderDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [receiptSourceReadiness, setReceiptSourceReadiness] =
    useState<ProcurementReceiptSourceReadinessDto | null>(null);
  const [sodReadiness, setSodReadiness] =
    useState<ProcurementPurchaseOrderSodReadinessDto | null>(null);
  const [receiptSourceLoading, setReceiptSourceLoading] = useState(true);
  const [receiptSourceError, setReceiptSourceError] = useState<string | null>(null);
  const [idempotencyKey] = useState(() =>
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `receipt-${Date.now()}-${Math.random().toString(16).slice(2)}`
  );
  
  // Receipt form data
  const [receiptDate, setReceiptDate] = useState(format(new Date(), 'yyyy-MM-dd'));
  const [deliveryNote, setDeliveryNote] = useState('');
  const [carrierName, setCarrierName] = useState('');
  const [trackingNumber, setTrackingNumber] = useState('');
  const [requiresInspection, setRequiresInspection] = useState(false);
  const [receiptNotes, setReceiptNotes] = useState('');
  
  // Items to receive
  const [receiptItems, setReceiptItems] = useState<ReceiptItemFormData[]>([]);
  
  // Reference data
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseLocationsByWarehouseId, setWarehouseLocationsByWarehouseId] = useState<Record<string, WarehouseLocationDto[]>>({});

  const normalizeGuid = (value?: string | null): string => (value || '').trim().toLowerCase();

  const unwrapWarehouses = (payload: unknown): WarehouseDto[] => {
    if (Array.isArray(payload)) {
      return payload as WarehouseDto[];
    }

    if (payload && typeof payload === 'object' && Array.isArray((payload as any).data)) {
      return (payload as any).data as WarehouseDto[];
    }

    return [];
  };

  const unwrapWarehouseLocations = (payload: unknown): WarehouseLocationDto[] => {
    if (Array.isArray(payload)) {
      return payload as WarehouseLocationDto[];
    }

    if (payload && typeof payload === 'object' && Array.isArray((payload as any).data)) {
      return (payload as any).data as WarehouseLocationDto[];
    }

    return [];
  };

  const getPendingQuantity = (item: ReceiptItemFormData): number => {
    const received = Number(item.receivedQuantity || 0);
    const accepted = Number(item.acceptedQuantity || 0);
    const rejected = Number(item.rejectedQuantity || 0);
    return Math.max(0, received - accepted - rejected);
  };

  const getFallbackWarehouseId = (): string => {
    const preferred = warehouses.find(w => w.isDefault) ?? (warehouses.length === 1 ? warehouses[0] : undefined);
    return normalizeGuid(preferred?.id);
  };

  const getEffectiveWarehouseId = (item: ReceiptItemFormData): string => {
    return normalizeGuid(item.warehouseId || item.poWarehouseId || order?.deliveryWarehouseId || getFallbackWarehouseId());
  };

  const loadReceiptSourceReadiness = async (
    purchaseOrderId = id
  ): Promise<ProcurementReceiptSourceReadinessDto | null> => {
    if (!purchaseOrderId) return null;
    try {
      setReceiptSourceLoading(true);
      setReceiptSourceError(null);
      const readiness =
        await purchasingService.getReceiptSourceReadiness(purchaseOrderId);
      setReceiptSourceReadiness(readiness);
      return readiness;
    } catch (error: any) {
      const message =
        error?.message ||
        'Receipt source could not be verified. Receiving is blocked until the control can be revalidated.';
      setReceiptSourceReadiness(null);
      setReceiptSourceError(message);
      return null;
    } finally {
      setReceiptSourceLoading(false);
    }
  };

  const fetchOrder = async () => {
    try {
      setLoading(true);
      const data = await purchasingService.getPurchaseOrderById(id);
      setOrder(data);
      const sourceReadiness = await loadReceiptSourceReadiness(data.id);
      const sourceLines = new Map(
        (sourceReadiness?.lines ?? []).map(line => [
          normalizeGuid(line.purchaseOrderItemId),
          line,
        ])
      );
      
      // Initialize receipt items from PO items
      const items: ReceiptItemFormData[] = data.items
        .map(item => {
          const governedLine = sourceLines.get(normalizeGuid(item.id));
          const remainingQuantity =
            governedLine?.remainingQuantity ?? item.remainingQuantity;
          return {
          purchaseOrderItemId: item.id,
          itemCode: item.itemCode,
          itemName: item.itemName,
          orderedQuantity: item.orderedQuantity,
          previouslyReceived:
            governedLine?.previouslyReceiptedQuantity ?? item.receivedQuantity,
          remainingQuantity,
          unitOfMeasure: item.unitOfMeasure,
          itemUnitOfMeasureId: item.itemUnitOfMeasureId,
          // Do not auto-populate receipt quantities; user will enter what was actually received.
          receivedQuantity: 0,
          acceptedQuantity: 0,
          rejectedQuantity: 0,
          locationId: '',
          poWarehouseId: normalizeGuid(item.warehouseId || data.deliveryWarehouseId),
          warehouseId: normalizeGuid(item.warehouseId || data.deliveryWarehouseId),
          warehouseName: item.warehouseName || 'Warehouse',
          serialNumber: '',
          lotNumber: '',
          expirationDate: '',
          notes: '',
          rejectionReason: '',
          qualityStatus: 'Passed',
          qualityNotes: ''
          };
        })
        .filter(item => item.remainingQuantity > 0);
      
      setReceiptItems(items);
      
      // Load all active warehouses so user can override PO-line warehouse at receiving.
      try {
        let allWarehouses = unwrapWarehouses(await inventoryManagementService.getWarehouses(true));
        if (allWarehouses.length === 0) {
          allWarehouses = unwrapWarehouses(await inventoryManagementService.getActiveWarehouses());
        }
        if (allWarehouses.length === 0) {
          allWarehouses = unwrapWarehouses(await inventoryManagementService.getAllWarehouses());
        }

        allWarehouses = allWarehouses
          .map(warehouse => ({ ...warehouse, id: normalizeGuid(warehouse.id) }))
          .filter(w => w.isActive);

        setWarehouses(allWarehouses);

        // Preload warehouse locations for any warehouses referenced by the receipt lines.
        const uniqueWarehouseIds = Array.from(
          new Set(items.map(i => normalizeGuid(i.warehouseId)).filter(Boolean))
        );

        const locationsMap: Record<string, WarehouseLocationDto[]> = {};
        await Promise.all(uniqueWarehouseIds.map(async (warehouseId) => {
          try {
            const locations = unwrapWarehouseLocations(await inventoryManagementService.getWarehouseLocations(warehouseId))
              .map(l => ({ ...l, id: normalizeGuid(l.id), warehouseId: normalizeGuid(l.warehouseId) }))
              .filter(l => l.isActive);
            locationsMap[warehouseId] = locations;
          } catch (err) {
            console.error(`Error loading warehouse locations for ${warehouseId}:`, err);
            locationsMap[warehouseId] = [];
          }
        }));
        setWarehouseLocationsByWarehouseId(locationsMap);
      } catch (err) {
        console.error('Error loading warehouses:', err);
        setWarehouses([]);
      }
    } catch (err: any) {
      console.error('Error fetching purchase order:', err);
      toast.error('Failed to load purchase order');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (id) {
      fetchOrder();
    }
  }, [id]);

  const handleReceivedQuantityChange = (index: number, value: number) => {
    const updatedItems = [...receiptItems];
    const item = updatedItems[index];
    
    // Ensure received quantity doesn't exceed remaining
    const oldReceived = Number(item.receivedQuantity || 0);
    const receivedQty = Math.min(value, item.remainingQuantity);
    item.receivedQuantity = receivedQty;
    
    // Keep accepted/rejected independent:
    // accepted < received does NOT imply rejected; it can mean "pending/unchecked".
    const rejectedQty = Math.max(0, Number(item.rejectedQuantity || 0));
    const acceptedQty = Math.max(0, Number(item.acceptedQuantity || 0));

    // If accepted was previously auto-aligned to received and nothing rejected, keep it aligned.
    if (rejectedQty === 0 && Math.abs(acceptedQty - oldReceived) < 0.000001) {
      item.acceptedQuantity = receivedQty;
    } else {
      item.acceptedQuantity = Math.min(acceptedQty, Math.max(0, receivedQty - rejectedQty));
    }

    item.rejectedQuantity = Math.min(rejectedQty, Math.max(0, receivedQty - Number(item.acceptedQuantity || 0)));
    
    setReceiptItems(updatedItems);
  };

  const ensureWarehouseLocationsLoaded = async (warehouseId?: string) => {
    const idToLoad = normalizeGuid(warehouseId);
    if (!idToLoad) return;
    if (warehouseLocationsByWarehouseId[idToLoad] && warehouseLocationsByWarehouseId[idToLoad].length > 0) return;

    try {
      const locations = unwrapWarehouseLocations(await inventoryManagementService.getWarehouseLocations(idToLoad))
        .map(l => ({ ...l, id: normalizeGuid(l.id), warehouseId: normalizeGuid(l.warehouseId) }))
        .filter(l => l.isActive);

      setWarehouseLocationsByWarehouseId(prev => ({ ...prev, [idToLoad]: locations }));
    } catch (err) {
      console.error('Error loading warehouse locations:', err);
      setWarehouseLocationsByWarehouseId(prev => ({ ...prev, [idToLoad]: [] }));
    }
  };

  const handleAcceptedQuantityChange = (index: number, value: number) => {
    const updatedItems = [...receiptItems];
    const item = updatedItems[index];
    
    const oldReceivedQty = Math.max(0, Number(item.receivedQuantity || 0));
    const oldAcceptedQty = Math.max(0, Number(item.acceptedQuantity || 0));
    let receivedQty = oldReceivedQty;
    let rejectedQty = Math.max(0, Number(item.rejectedQuantity || 0));

    // Accepted cannot exceed the current received quantity.
    // We'll optionally auto-adjust received below to support "short shipment" entry.
    let acceptedQty = Math.min(Math.max(0, value), receivedQty);

    // If we're not doing inspection and nothing is explicitly rejected, treat Accepted < Received as "not received"
    // (short-shipped / pending delivery), so the PO stays open for the remainder.
    if (!requiresInspection && rejectedQty === 0 && acceptedQty < receivedQty)
    {
      receivedQty = acceptedQty;
      item.receivedQuantity = receivedQty;
    }

    // If there are explicit rejections, ensure Received >= Accepted + Rejected (up to remaining quantity).
    if (rejectedQty > 0)
    {
      const minNeeded = acceptedQty + rejectedQty;
      if (minNeeded > receivedQty)
      {
        receivedQty = Math.min(item.remainingQuantity, minNeeded);
        item.receivedQuantity = receivedQty;
      }
    }

    // Enforce bounds after any received adjustments.
    acceptedQty = Math.min(acceptedQty, receivedQty);
    item.acceptedQuantity = acceptedQty;

    // If accepted was previously auto-aligned and user is typing, keep behavior stable.
    // (We still keep the hard constraint Accepted + Rejected <= Received.)
    if (Math.abs(oldAcceptedQty - oldReceivedQty) < 0.000001 && Math.abs(oldReceivedQty - receivedQty) < 0.000001)
    {
      // no-op; leave values as-is
    }

    // If user increases accepted beyond remaining after rejected, shrink rejected (explicit reject) to fit.
    if (acceptedQty + rejectedQty > receivedQty)
    {
      rejectedQty = Math.max(0, receivedQty - acceptedQty);
      item.rejectedQuantity = rejectedQty;
    }
    
    setReceiptItems(updatedItems);
  };

  const handleRejectedQuantityChange = (index: number, value: number) => {
    const updatedItems = [...receiptItems];
    const item = updatedItems[index];
    
    let receivedQty = Math.max(0, Number(item.receivedQuantity || 0));
    let acceptedQty = Math.max(0, Number(item.acceptedQuantity || 0));
    const desiredRejectedQty = Math.min(Math.max(0, value), item.remainingQuantity);

    // Ensure Received >= Accepted + Rejected (up to remaining quantity) so users can set rejected without
    // manually bumping "Received" when they've already set Accepted.
    const minNeeded = acceptedQty + desiredRejectedQty;
    if (minNeeded > receivedQty)
    {
      receivedQty = Math.min(item.remainingQuantity, minNeeded);
      item.receivedQuantity = receivedQty;
    }

    // If user increases rejected beyond remaining after accepted (and we cannot increase Received), shrink accepted to fit.
    if (acceptedQty + desiredRejectedQty > receivedQty)
    {
      acceptedQty = Math.max(0, receivedQty - desiredRejectedQty);
      item.acceptedQuantity = acceptedQty;
    }

    item.rejectedQuantity = Math.min(desiredRejectedQty, Math.max(0, receivedQty - acceptedQty));
    
    setReceiptItems(updatedItems);
  };

  const handleItemFieldChange = (index: number, field: keyof ReceiptItemFormData, value: any) => {
    const updatedItems = [...receiptItems];
    (updatedItems[index] as any)[field] = value;
    setReceiptItems(updatedItems);
  };

  const handleWarehouseChange = async (index: number, nextWarehouseIdRaw: string) => {
    const fallbackWarehouseId = getFallbackWarehouseId();
    const nextWarehouseId = nextWarehouseIdRaw === '__none__'
      ? normalizeGuid(receiptItems[index]?.poWarehouseId || order?.deliveryWarehouseId || fallbackWarehouseId)
      : normalizeGuid(nextWarehouseIdRaw);

    if (!nextWarehouseId) {
      toast.error('Please select a warehouse first');
      return;
    }

    const updatedItems = [...receiptItems];
    updatedItems[index].warehouseId = nextWarehouseId;
    // Reset location selection when warehouse changes.
    updatedItems[index].locationId = '';
    setReceiptItems(updatedItems);

    await ensureWarehouseLocationsLoaded(nextWarehouseId);
  };

  const handleCreateReceipt = async () => {
    if (
      receiptSourceLoading ||
      receiptSourceError ||
      receiptSourceReadiness?.canReceive !== true ||
      sodReadiness?.canReceive !== true
    ) {
      toast.error(
        receiptSourceError ||
        sodReadiness?.message ||
        receiptSourceReadiness?.message ||
        'The governed receipt source and independent receiver controls are not ready.'
      );
      return;
    }

    // Validation
    const itemsToReceive = receiptItems.filter(item => item.receivedQuantity > 0);
    
    if (itemsToReceive.length === 0) {
      toast.error('Please enter received quantities for at least one item');
      return;
    }

    // Enforce put-away location selection per receipt line
    const missingLocations = itemsToReceive.filter(item => !item.locationId || !item.locationId.trim());
    if (missingLocations.length > 0) {
      toast.error('Please select a storage location for each received line item');
      return;
    }
    
    // Check for rejection reasons
    const itemsWithRejections = itemsToReceive.filter(item => item.rejectedQuantity > 0);
    const missingRejectionReasons = itemsWithRejections.filter(item => !item.rejectionReason?.trim());
    
    if (missingRejectionReasons.length > 0) {
      toast.error('Please provide rejection reasons for all rejected items');
      return;
    }

    try {
      setSaving(true);
      
      const userStr = localStorage.getItem('user');
      const user = userStr ? JSON.parse(userStr) : null;
      const receivedById = user?.id || user?.userId;
      
      if (!receivedById) {
        toast.error('User not authenticated');
        return;
      }

      const receiptData: ReceivePurchaseOrderDto = {
        purchaseOrderId: id,
        deliveryNote: deliveryNote || undefined,
        carrierName: carrierName || undefined,
        trackingNumber: trackingNumber || undefined,
        receivedById,
        inspectedById: requiresInspection ? receivedById : undefined,
        notes: receiptNotes || undefined,
        requiresInspection,
        idempotencyKey,
        items: itemsToReceive.map(item => ({
          purchaseOrderItemId: item.purchaseOrderItemId,
          receivedQuantity: item.receivedQuantity,
          acceptedQuantity: item.acceptedQuantity,
          rejectedQuantity: item.rejectedQuantity,
          warehouseId: item.warehouseId || undefined,
          locationId: item.locationId || undefined,
          serialNumber: item.serialNumber || undefined,
          lotNumber: item.lotNumber || undefined,
          expirationDate: item.expirationDate || undefined,
          notes: item.notes || undefined,
          rejectionReason: item.rejectionReason || undefined,
          qualityStatus: item.qualityStatus || undefined,
          qualityNotes: item.qualityNotes || undefined
        }))
      };

      const result = await purchasingService.receivePurchaseOrder(id, receiptData);
      toast.success('Goods receipt created successfully');
      router.push(`/procurement/purchase-receipts/${result.id}`);
    } catch (error: any) {
      console.error('Error creating receipt:', error);
      toast.error(error.message || 'Failed to create receipt');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!order) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-orders')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
        </div>
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <AlertCircle className="h-5 w-5 text-red-600" />
              <p className="text-red-900">Purchase order not found</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const totalReceiving = receiptItems.reduce((sum, item) => sum + item.receivedQuantity, 0);
  const totalAccepted = receiptItems.reduce((sum, item) => sum + item.acceptedQuantity, 0);
  const totalRejected = receiptItems.reduce((sum, item) => sum + item.rejectedQuantity, 0);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push(`/procurement/purchase-orders/${id}`)}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to PO
          </Button>
          <div>
            <h1 className="text-2xl font-bold">Receive Goods</h1>
            <p className="text-muted-foreground mt-1">
              Create Goods Receipt Note for {order.orderNumber}
            </p>
          </div>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement">Procurement</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement/purchase-orders">Purchase Orders</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href={`/procurement/purchase-orders/${id}`}>{order.orderNumber}</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Receive</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <ReceiptSourceControlCard
        readiness={receiptSourceReadiness}
        loading={receiptSourceLoading}
        error={receiptSourceError}
        onRetry={() => {
          void loadReceiptSourceReadiness();
        }}
      />

      <PurchaseOrderSodControl
        purchaseOrderId={id}
        status={order.status}
        scope="receipt"
        onReadinessChange={setSodReadiness}
      />

      {/* PO Summary */}
      <Card>
        <CardHeader className="py-3">
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Purchase Order Summary
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-0 pb-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <Label className="text-muted-foreground">PO Number</Label>
              <p className="font-medium mt-1">{order.orderNumber}</p>
            </div>
            
            <div>
              <Label className="text-muted-foreground flex items-center gap-2">
                <Building2 className="h-4 w-4" />
                Supplier
              </Label>
              <p className="font-medium mt-1">{order.supplierName}</p>
            </div>
            
            <div>
              <Label className="text-muted-foreground">Total Amount</Label>
              <p className="font-medium mt-1">
                ${order.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Receipt Information */}
      <Card>
        <CardHeader className="py-3">
          <CardTitle className="flex items-center gap-2">
            <TruckIcon className="h-5 w-5" />
            Receipt Information
          </CardTitle>
          <CardDescription>Enter the delivery and receipt details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3 pt-0 pb-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="receiptDate" className="flex items-center gap-2">
                <Calendar className="h-4 w-4" />
                Receipt Date *
              </Label>
              <Input
                id="receiptDate"
                type="date"
                value={receiptDate}
                onChange={(e) => setReceiptDate(e.target.value)}
                className="h-9"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="deliveryNote">Delivery Note Number</Label>
              <Input
                id="deliveryNote"
                value={deliveryNote}
                onChange={(e) => setDeliveryNote(e.target.value)}
                placeholder="Supplier's delivery note number"
                className="h-9"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="carrierName">Carrier Name</Label>
              <Input
                id="carrierName"
                value={carrierName}
                onChange={(e) => setCarrierName(e.target.value)}
                placeholder="Delivery carrier/courier"
                className="h-9"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="trackingNumber">Tracking Number</Label>
              <Input
                id="trackingNumber"
                value={trackingNumber}
                onChange={(e) => setTrackingNumber(e.target.value)}
                placeholder="Shipment tracking number"
                className="h-9"
              />
            </div>
          </div>
          
          <Separator />
          
          <div className="flex items-center space-x-2">
            <Switch
              id="requiresInspection"
              checked={requiresInspection}
              onCheckedChange={setRequiresInspection}
            />
            <Label htmlFor="requiresInspection" className="cursor-pointer text-sm">
              Requires Quality Inspection
            </Label>
          </div>
                       
          <div className="space-y-2">
            <Label htmlFor="receiptNotes" className="text-sm">Receipt Notes</Label>
            <Textarea
              id="receiptNotes"
              value={receiptNotes}
              onChange={(e) => setReceiptNotes(e.target.value)}
              placeholder="Additional notes about this receipt..."
              rows={2}
            />
          </div>
        </CardContent>
      </Card>

      {/* Items to Receive */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Package className="h-5 w-5" />
            Items to Receive
          </CardTitle>
          <CardDescription>
            Enter received quantities and quality information for each item
          </CardDescription>
        </CardHeader>
        <CardContent>
          {receiptItems.length === 0 ? (
            <div className="text-center py-12 border-2 border-dashed rounded-lg">
              <CheckCircle className="h-12 w-12 mx-auto text-green-500 mb-4" />
              <p className="text-muted-foreground">All items have been fully received</p>
            </div>
          ) : (
            <div className="space-y-4">
              {receiptItems.map((item, index) => (
                <Card key={item.purchaseOrderItemId} className="border-2">
                  <CardHeader className="py-3">
                    <div className="flex items-center justify-between">
                      <div>
                        <CardTitle className="text-base">
                          {item.itemCode} - {item.itemName}
                        </CardTitle>
                        <CardDescription className="mt-0.5 text-xs">
                          UOM: {item.unitOfMeasure} •{' '}
                          Ordered: {item.orderedQuantity} • 
                          Previously Received: {item.previouslyReceived} • 
                          Remaining: {item.remainingQuantity}
                        </CardDescription>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-3 pt-3">
                    <div className="grid grid-cols-2 lg:grid-cols-7 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor={`received-${index}`} className="text-xs">Received ({item.unitOfMeasure}) *</Label>
                        <Input
                          id={`received-${index}`}
                          type="number"
                          min="0"
                          max={item.remainingQuantity}
                          step="0.01"
                          value={item.receivedQuantity}
                          onChange={(e) => handleReceivedQuantityChange(index, parseFloat(e.target.value) || 0)}
                          className="h-9"
                        />
                      </div>
                      
                      <div className="space-y-2">
                        <Label htmlFor={`accepted-${index}`} className="flex items-center gap-2 text-xs">
                          <CheckCircle className="h-4 w-4 text-green-600" />
                          Accepted ({item.unitOfMeasure})
                        </Label>
                        <Input
                          id={`accepted-${index}`}
                          type="number"
                          min="0"
                          max={item.receivedQuantity}
                          step="0.01"
                          value={item.acceptedQuantity}
                          onChange={(e) => handleAcceptedQuantityChange(index, parseFloat(e.target.value) || 0)}
                          className="h-9"
                        />
                      </div>
                      
                      <div className="space-y-2">
                        <Label htmlFor={`rejected-${index}`} className="flex items-center gap-2 text-xs">
                          <XCircle className="h-4 w-4 text-red-600" />
                          Rejected ({item.unitOfMeasure})
                        </Label>
                        <Input
                          id={`rejected-${index}`}
                          type="number"
                          min="0"
                          max={item.receivedQuantity}
                          step="0.01"
                          value={item.rejectedQuantity}
                          onChange={(e) => handleRejectedQuantityChange(index, parseFloat(e.target.value) || 0)}
                          className="h-9"
                        />
                      </div>

                      <div className="space-y-2">
                        <Label className="text-xs">Pending</Label>
                        <Input readOnly value={getPendingQuantity(item)} className="h-9 bg-muted" />
                      </div>

                      <div className="space-y-2">
                        <Label htmlFor={`warehouse-${index}`} className="text-xs">Warehouse</Label>
                        <Select
                          value={item.warehouseId || '__none__'}
                          onValueChange={(value) => handleWarehouseChange(index, value)}
                        >
                          <SelectTrigger className="h-9">
                            <SelectValue placeholder="Select warehouse" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Default From PO Item Warehouse</SelectItem>
                            {warehouses.map(warehouse => (
                              <SelectItem key={warehouse.id} value={warehouse.id}>
                                {(warehouse as WarehouseDto & { warehouseCode?: string }).warehouseCode ?? warehouse.code}
                                {' - '}
                                {(warehouse as WarehouseDto & { warehouseName?: string }).warehouseName ?? warehouse.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-2">
                        <Label htmlFor={`location-${index}`} className="text-xs">Storage Location *</Label>
                        <Select
                          value={item.locationId || '__none__'}
                          onOpenChange={(open) => {
                            if (!open) return;
                            const effectiveWarehouseId = getEffectiveWarehouseId(item);
                            if (!effectiveWarehouseId) return;

                            // If user never selected a warehouse on the PO and this line has no explicit warehouse,
                            // use the effective warehouse to load locations and keep the UX smooth.
                            if (!normalizeGuid(item.warehouseId)) {
                              handleItemFieldChange(index, 'warehouseId', effectiveWarehouseId);
                            }

                            ensureWarehouseLocationsLoaded(effectiveWarehouseId);
                          }}
                          onValueChange={(value) => {
                            const nextLocationId = value === '__none__' ? '' : normalizeGuid(value);
                            const effectiveWarehouseId = getEffectiveWarehouseId(item);
                            const updatedItems = [...receiptItems];
                            updatedItems[index].locationId = nextLocationId;
                            if (nextLocationId && !normalizeGuid(updatedItems[index].warehouseId) && effectiveWarehouseId) {
                              updatedItems[index].warehouseId = effectiveWarehouseId;
                            }
                            setReceiptItems(updatedItems);
                          }}
                          disabled={!getEffectiveWarehouseId(item)}
                        >
                          <SelectTrigger className="h-9">
                            <SelectValue placeholder={getEffectiveWarehouseId(item) ? "Select location" : "Select warehouse first"} />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select location…</SelectItem>
                            {(warehouseLocationsByWarehouseId[getEffectiveWarehouseId(item)] ?? []).map(location => (
                              <SelectItem key={location.id} value={location.id}>
                                {location.locationCode}
                                {location.name ? ` - ${location.name}` : ''}
                              </SelectItem>
                            ))}
                            {getEffectiveWarehouseId(item) &&
                              (warehouseLocationsByWarehouseId[getEffectiveWarehouseId(item)] ?? []).length === 0 && (
                              <SelectItem value="__no_locations__" disabled>
                                No locations found for this warehouse
                              </SelectItem>
                            )}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-2">
                        <Label htmlFor={`quality-${index}`} className="text-xs">Quality</Label>
                        <Select
                          value={item.qualityStatus}
                          onValueChange={(value) => handleItemFieldChange(index, 'qualityStatus', value)}
                        >
                          <SelectTrigger className="h-9">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {qualityStatusOptions.map(opt => (
                              <SelectItem key={opt.value} value={opt.value}>
                                <span className={opt.color}>{opt.label}</span>
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    </div>
                    
                    {item.rejectedQuantity > 0 && (
                      <div className="space-y-2">
                        <Label htmlFor={`rejection-reason-${index}`}>Rejection Reason *</Label>
                        <Textarea
                          id={`rejection-reason-${index}`}
                          value={item.rejectionReason}
                          onChange={(e) => handleItemFieldChange(index, 'rejectionReason', e.target.value)}
                          placeholder="Explain why items were rejected..."
                          rows={2}
                          className="border-red-200"
                        />
                      </div>
                    )}
                    
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor={`serial-${index}`} className="text-xs">Serial</Label>
                        <Input
                          id={`serial-${index}`}
                          value={item.serialNumber}
                          onChange={(e) => handleItemFieldChange(index, 'serialNumber', e.target.value)}
                          placeholder="Serial number (if tracked)"
                          className="h-9"
                        />
                      </div>
                      
                      <div className="space-y-2">
                        <Label htmlFor={`lot-${index}`} className="text-xs">Lot/Batch</Label>
                        <Input
                          id={`lot-${index}`}
                          value={item.lotNumber}
                          onChange={(e) => handleItemFieldChange(index, 'lotNumber', e.target.value)}
                          placeholder="Lot/batch number (if tracked)"
                          className="h-9"
                        />
                      </div>
                      
                      <div className="space-y-2">
                        <Label htmlFor={`expiry-${index}`} className="text-xs">Expiry</Label>
                        <Input
                          id={`expiry-${index}`}
                          type="date"
                          value={item.expirationDate}
                          onChange={(e) => handleItemFieldChange(index, 'expirationDate', e.target.value)}
                          className="h-9"
                        />
                      </div>
                    </div>
                    
                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor={`quality-notes-${index}`} className="text-xs">Quality Notes</Label>
                        <Textarea
                          id={`quality-notes-${index}`}
                          value={item.qualityNotes}
                          onChange={(e) => handleItemFieldChange(index, 'qualityNotes', e.target.value)}
                          placeholder="Quality inspection notes..."
                          rows={2}
                        />
                      </div>
                      
                      <div className="space-y-2">
                        <Label htmlFor={`item-notes-${index}`} className="text-xs">Item Notes</Label>
                        <Textarea
                          id={`item-notes-${index}`}
                          value={item.notes}
                          onChange={(e) => handleItemFieldChange(index, 'notes', e.target.value)}
                          placeholder="Additional notes for this item..."
                          rows={2}
                        />
                      </div>
                    </div>
                  </CardContent>
                </Card>
              ))}
              
              {/* Receipt Summary */}
              <Card className="border-blue-200 bg-blue-50">
                <CardContent className="pt-6">
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="text-center">
                      <p className="text-2xl font-bold text-blue-600">{totalReceiving}</p>
                      <p className="text-sm text-muted-foreground">Total Receiving</p>
                    </div>
                    <div className="text-center">
                      <p className="text-2xl font-bold text-green-600">{totalAccepted}</p>
                      <p className="text-sm text-muted-foreground">Accepted</p>
                    </div>
                    <div className="text-center">
                      <p className="text-2xl font-bold text-red-600">{totalRejected}</p>
                      <p className="text-sm text-muted-foreground">Rejected</p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Action Buttons */}
      <div className="flex justify-end gap-4">
        <Button 
          variant="outline" 
          onClick={() => router.push(`/procurement/purchase-orders/${id}`)}
        >
          Cancel
        </Button>
        <Button
          onClick={handleCreateReceipt}
          disabled={
            saving ||
            receiptSourceLoading ||
            receiptSourceError !== null ||
            receiptSourceReadiness?.canReceive !== true ||
            sodReadiness?.canReceive !== true ||
            receiptItems.length === 0 ||
            totalReceiving === 0
          }
        >
          {saving ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Creating Receipt...
            </>
          ) : (
            <>
              <Package className="w-4 h-4 mr-2" />
              Create Receipt
            </>
          )}
        </Button>
      </div>

      {/* Validation Warnings */}
      {totalReceiving === 0 && receiptItems.length > 0 && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div>
                <p className="font-medium text-yellow-900">No quantities entered</p>
                <p className="text-sm text-yellow-700">
                  Please enter received quantities for at least one item.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
