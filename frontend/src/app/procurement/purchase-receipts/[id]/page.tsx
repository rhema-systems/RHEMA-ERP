'use client';
import { LandedCostInvoiceLink } from '@/components/procurement/LandedCostInvoiceLink';
import { LandedCostSupplierSummary } from '@/components/procurement/LandedCostSupplierSummary';
import { LandedCostSupplierInvoices } from '@/components/procurement/LandedCostSupplierInvoices';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
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
  FileText,
  Calendar,
  User,
  Building2,
  Package,
  TruckIcon,
  Printer,
  AlertCircle,
  Loader2,
  CheckCircle,
  XCircle,
  MapPin,
  Clock,
  AlertTriangle
} from 'lucide-react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseOrderReceiptDto
} from '@/services/purchasingService';
import { inventoryManagementService, LandedCostDetailDto, LandedCostDto } from '@/services/inventoryManagementService';
import { format } from 'date-fns';
import Link from 'next/link';
import { ReceiptInspectionControl } from '@/components/procurement/ReceiptInspectionControl';
import { ReceiptDocumentControl } from '@/components/procurement/ReceiptDocumentControl';
import { ReceiptSourceEvidenceControl } from '@/components/procurement/ReceiptSourceEvidenceControl';
import { PurchaseOrderSodControl } from '@/components/procurement/PurchaseOrderSodControl';
import { ReceiptLandedCostEntry } from '@/components/procurement/ReceiptLandedCostEntry';
import { PurchaseReceiptDistribution } from '@/components/procurement/PurchaseReceiptDistribution';

const GRNStatuses = [
  { value: 'Accepted', label: 'Accepted', color: 'border-green-200 bg-green-100 text-green-800', icon: CheckCircle },
  { value: 'Pending', label: 'Pending', color: 'bg-yellow-100 text-yellow-800', icon: Clock },
  { value: 'Pending Inspection', label: 'Pending Inspection', color: 'bg-yellow-100 text-yellow-800', icon: AlertTriangle },
  { value: 'Inspection In Progress', label: 'Inspection In Progress', color: 'bg-blue-100 text-blue-800', icon: Clock },
  { value: 'Approved', label: 'Approved', color: 'bg-green-100 text-green-800', icon: CheckCircle },
  { value: 'Partially Approved', label: 'Partially Approved', color: 'bg-orange-100 text-orange-800', icon: AlertTriangle },
  { value: 'Rejected', label: 'Rejected', color: 'bg-red-100 text-red-800', icon: XCircle },
  { value: 'Completed', label: 'Completed', color: 'bg-purple-100 text-purple-800', icon: CheckCircle }
];

export default function PurchaseReceiptDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  
  const [receipt, setReceipt] = useState<PurchaseOrderReceiptDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('details');

  const [landedCosts, setLandedCosts] = useState<LandedCostDto[]>([]);
  const [selectedLandedCostId, setSelectedLandedCostId] = useState<string>('');
  const [landedCostDetail, setLandedCostDetail] = useState<LandedCostDetailDto | null>(null);
  const [landedCostLoading, setLandedCostLoading] = useState(false);
  const [landedCostAllocating, setLandedCostAllocating] = useState(false);
  const [landedCostPosting, setLandedCostPosting] = useState(false);
  const inspectionCompleted = Boolean(
    receipt?.requiresInspection &&
    receipt.inspectionDate &&
    receipt.inspectionResult &&
    receipt.inspectionResult.toLowerCase() !== 'pending'
  );

  const getLandedCostTypeLabel = (costType: string | number): string => {
    const value = typeof costType === 'string' ? costType : String(costType);
    const normalized = value.trim();
    switch (normalized) {
      case 'Freight':
      case '1':
        return 'Freight / Shipping';
      case 'CustomsDuty':
      case '2':
        return 'Customs Duty';
      case 'Insurance':
      case '3':
        return 'Insurance';
      case 'Handling':
      case '4':
        return 'Handling';
      case 'Brokerage':
      case '5':
        return 'Brokerage';
      case 'Storage':
      case '6':
        return 'Storage / Warehousing';
      case 'Other':
      case '7':
      default:
        return 'Other';
    }
  };

  const fetchReceipt = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await purchasingService.getPurchaseReceiptById(id);
      setReceipt(data);
    } catch (err: any) {
      console.error('Error fetching purchase receipt:', err);
      setError('Failed to load purchase receipt');
      toast.error('Failed to load purchase receipt');
    } finally {
      setLoading(false);
    }
  };

  const fetchLandedCosts = async (ensure: boolean) => {
    try {
      setLandedCostLoading(true);
      const list = await inventoryManagementService.getLandedCostsByGrn(id, ensure);
      setLandedCosts(list || []);

      // Select the currently selected voucher if it still exists; otherwise pick the first.
      setSelectedLandedCostId((prev) => {
        const keep = prev && (list || []).some((l) => l.id === prev);
        return keep ? prev : (list?.[0]?.id || '');
      });

      if (!list || list.length === 0) {
        setLandedCostDetail(null);
      }
    } catch (err: any) {
      console.error('Error fetching landed cost:', err);
      toast.error(err?.message || 'Failed to load landed cost voucher');
      // Keep existing state on fetch failures (avoid wiping a just-created voucher from the UI).
    } finally {
      setLandedCostLoading(false);
    }
  };

  const initializeLandedCostFromPo = async () => {
    try {
      setLandedCostLoading(true);
      console.log('[LandedCost] Initialize from PO', { receiptId: id });
      const created = await inventoryManagementService.initializeLandedCostFromPo(id);
      console.log('[LandedCost] Initialize response', created);

      // Optimistically reflect the created/updated voucher immediately (so the dropdown is populated even if
      // the subsequent list refresh is delayed/failed).
      if (created?.id) {
        setLandedCosts((prev) => {
          const existingIndex = prev.findIndex((x) => x.id === created.id);
          if (existingIndex >= 0) {
            const next = [...prev];
            next[existingIndex] = created;
            return next;
          }
          return [created, ...prev];
        });
        setSelectedLandedCostId(created.id);
        try {
          const detail = await inventoryManagementService.getLandedCostById(created.id);
          setLandedCostDetail(detail);
        } catch {
          // ignore detail fetch errors here; list refresh below will handle UI sync
        }
      }

      await fetchLandedCosts(false);

      // Ensure the created voucher remains selectable even if the refresh returns empty/failed.
      if (created?.id) {
        setLandedCosts((prev) => (prev.some((x) => x.id === created.id) ? prev : [created, ...prev]));
        setSelectedLandedCostId((prev) => (prev ? prev : created.id));
      }
      toast.success(created?.landedCostNumber
        ? `Landed cost voucher ${created.landedCostNumber} created`
        : 'Landed cost voucher created from PO planned costs');
    } catch (err: any) {
      console.error('Error initializing landed cost:', err);
      toast.error(err?.message || 'Failed to initialize landed cost voucher');
    } finally {
      setLandedCostLoading(false);
    }
  };

  const allocateLandedCost = async () => {
    if (!selectedLandedCostId) return;
    try {
      setLandedCostAllocating(true);
      await inventoryManagementService.allocateLandedCost(selectedLandedCostId);
      await fetchLandedCosts(false);
      try {
        const detail = await inventoryManagementService.getLandedCostById(selectedLandedCostId);
        setLandedCostDetail(detail);
      } catch {
        // ignore; user can re-select voucher to reload
      }
      toast.success('Landed costs allocated');
    } catch (err: any) {
      console.error('Error allocating landed cost:', err);
      toast.error(err?.message || 'Failed to allocate landed costs');
    } finally {
      setLandedCostAllocating(false);
    }
  };

  useEffect(() => {
    if (id) {
      fetchReceipt();
    }
  }, [id]);

  useEffect(() => {
    if (activeTab !== 'landed-cost') return;
    if (!id) return;
    fetchLandedCosts(false);
  }, [activeTab, id]);

  useEffect(() => {
    if (activeTab !== 'landed-cost') return;
    if (!selectedLandedCostId) {
      setLandedCostDetail(null);
      return;
    }

    let cancelled = false;

    (async () => {
      try {
        setLandedCostLoading(true);
        const detail = await inventoryManagementService.getLandedCostById(selectedLandedCostId);
        if (cancelled) return;
        setLandedCostDetail(detail);
      } catch (err: any) {
        if (cancelled) return;
        console.error('Error fetching landed cost detail:', err);
        toast.error(err?.message || 'Failed to load landed cost voucher details');
        setLandedCostDetail(null);
      } finally {
        if (cancelled) return;
        setLandedCostLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [activeTab, selectedLandedCostId]);

  const getStatusBadge = (status: string) => {
    const statusConfig = GRNStatuses.find(s => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge variant="outline" className={statusConfig?.color || 'border-gray-200 bg-gray-100 text-gray-800'}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig?.label || status}
      </Badge>
    );
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error || !receipt) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-receipts')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
        </div>
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <AlertCircle className="h-5 w-5 text-red-600" />
              <p className="text-red-900">{error || 'Purchase receipt not found'}</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-receipts')} className="print:hidden">
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold">{receipt.receiptNumber}</h1>
              {getStatusBadge(receipt.status)}
              {receipt.requiresInspection && (
                <Badge
                  variant="outline"
                  className={inspectionCompleted
                    ? 'border-green-200 bg-green-50 text-green-800'
                    : 'bg-yellow-50'}
                >
                  {inspectionCompleted
                    ? <CheckCircle className="h-3 w-3 mr-1" />
                    : <AlertTriangle className="h-3 w-3 mr-1" />}
                  {inspectionCompleted ? 'Inspection Complete' : 'Inspection Required'}
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">Goods Receipt Note Details</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2 print:hidden"><Button variant="outline" onClick={() => setActiveTab('documents')}><Printer className="h-4 w-4 mr-2" />GRN register</Button></div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb className="print:hidden">
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
            <BreadcrumbLink href="/procurement/purchase-receipts">Purchase Receipts</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>{receipt.receiptNumber}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Main Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <div className="flex flex-wrap items-center justify-between gap-2 print:hidden">
        <TabsList>
          <TabsTrigger value="details">Receipt Details</TabsTrigger>
          <TabsTrigger value="items">Items Received</TabsTrigger>
          <TabsTrigger value="landed-cost">Landed Cost</TabsTrigger>
          <TabsTrigger value="documents">GRN</TabsTrigger>
          {receipt.requiresInspection && (
            <TabsTrigger value="inspection">Quality Inspection</TabsTrigger>
          )}
        </TabsList>
        <PurchaseReceiptDistribution receiptId={id} />
        </div>

        {/* Receipt Details Tab */}
        <TabsContent value="details" className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Receipt Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <FileText className="h-5 w-5" />
                  Receipt Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <Label className="text-muted-foreground">Receipt Number</Label>
                  <p className="font-medium mt-1">{receipt.receiptNumber}</p>
                </div>
                
                <div>
                  <Label className="text-muted-foreground flex items-center gap-2">
                    <Calendar className="h-4 w-4" />
                    Receipt Date
                  </Label>
                  <p className="font-medium mt-1">
                    {format(new Date(receipt.receiptDate), 'MMM dd, yyyy')}
                  </p>
                </div>
                
                <div>
                  <Label className="text-muted-foreground">Status</Label>
                  <div className="mt-1">{getStatusBadge(receipt.status)}</div>
                </div>
                
                {receipt.receivedByName && (
                  <div>
                    <Label className="text-muted-foreground flex items-center gap-2">
                      <User className="h-4 w-4" />
                      Received By
                    </Label>
                    <p className="font-medium mt-1">{receipt.receivedByName}</p>
                  </div>
                )}
                
                {receipt.inspectedByName && (
                  <div>
                    <Label className="text-muted-foreground">Inspected By</Label>
                    <p className="font-medium mt-1">{receipt.inspectedByName}</p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Purchase Order & Supplier */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Building2 className="h-5 w-5" />
                  Purchase Order & Supplier
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {receipt.purchaseOrderNumber && (
                  <div>
                    <Label className="text-muted-foreground">Purchase Order</Label>
                    <Link href={`/procurement/purchase-orders/${receipt.purchaseOrderId}`}>
                      <p className="font-medium mt-1 text-blue-600 hover:underline">
                        {receipt.purchaseOrderNumber}
                      </p>
                    </Link>
                  </div>
                )}
                
                {receipt.supplierName && (
                  <div>
                    <Label className="text-muted-foreground">Supplier</Label>
                    <p className="font-medium mt-1">{receipt.supplierName}</p>
                  </div>
                )}
                
                {receipt.deliveryNote && (
                  <div>
                    <Label className="text-muted-foreground">Delivery Note</Label>
                    <p className="font-medium mt-1">{receipt.deliveryNote}</p>
                  </div>
                )}
                
                {receipt.carrierName && (
                  <div>
                    <Label className="text-muted-foreground flex items-center gap-2">
                      <TruckIcon className="h-4 w-4" />
                      Carrier
                    </Label>
                    <p className="font-medium mt-1">{receipt.carrierName}</p>
                  </div>
                )}
                
                {receipt.trackingNumber && (
                  <div>
                    <Label className="text-muted-foreground">Tracking Number</Label>
                    <p className="font-medium mt-1">{receipt.trackingNumber}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Notes */}
          {receipt.notes && (
            <Card>
              <CardHeader>
                <CardTitle>Notes</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm whitespace-pre-wrap">{receipt.notes}</p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Items Received Tab */}
        <TabsContent value="items" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Package className="h-5 w-5" />
                Items Received
              </CardTitle>
              <CardDescription>
                Items included in this goods receipt note
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!receipt.items || receipt.items.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No items in this receipt
                </div>
              ) : (
                <div className="border rounded-lg overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[50px]">#</TableHead>
                        <TableHead>Item Code</TableHead>
                        <TableHead>Item Name</TableHead>
                        <TableHead>UOM</TableHead>
                        <TableHead className="text-right">Received</TableHead>
                        <TableHead className="text-right">Accepted</TableHead>
                        <TableHead className="text-right">Rejected</TableHead>
                        <TableHead>Quality</TableHead>
                        <TableHead>Location</TableHead>
                        <TableHead>Tracking</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {receipt.items.map((item, index) => (
                        <TableRow key={item.id}>
                          <TableCell className="font-medium">{index + 1}</TableCell>
                          <TableCell className="font-medium">{item.itemCode}</TableCell>
                          <TableCell>
                            <div>{item.itemName}</div>
                            {item.notes && (
                              <div className="text-xs text-muted-foreground mt-1">
                                {item.notes}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                          <TableCell className="text-right">{item.receivedQuantity}</TableCell>
                          <TableCell className="text-right">
                            <span className="text-green-600 font-medium">{item.acceptedQuantity}</span>
                          </TableCell>
                          <TableCell className="text-right">
                            {item.rejectedQuantity > 0 ? (
                              <div>
                                <span className="text-red-600 font-medium">{item.rejectedQuantity}</span>
                                {item.rejectionReason && (
                                  <div className="text-xs text-red-600 mt-1">
                                    {item.rejectionReason}
                                  </div>
                                )}
                              </div>
                            ) : (
                              <span className="text-muted-foreground">0</span>
                            )}
                          </TableCell>
                          <TableCell>
                            {item.qualityStatus && (
                              <Badge variant="outline" className={
                                item.qualityStatus === 'Passed' ? 'bg-green-50 text-green-700' :
                                item.qualityStatus === 'Failed' ? 'bg-red-50 text-red-700' :
                                item.qualityStatus === 'Conditional' ? 'bg-yellow-50 text-yellow-700' :
                                'bg-gray-50 text-gray-700'
                              }>
                                {item.qualityStatus}
                              </Badge>
                            )}
                            {item.qualityNotes && (
                              <div className="text-xs text-muted-foreground mt-1">
                                {item.qualityNotes}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>
                            <div className="text-sm">
                              {item.warehouseCode || item.warehouseName
                                ? `${item.warehouseCode ?? ''}${item.warehouseCode && item.warehouseName ? ' - ' : ''}${item.warehouseName ?? ''}`.trim()
                                : '-'}
                            </div>
                            {item.locationCode && (
                              <div className="text-xs text-muted-foreground mt-1">
                                Bin: {item.locationCode}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>
                            <div className="text-xs space-y-1">
                              {item.serialNumber && (
                                <div>S/N: {item.serialNumber}</div>
                              )}
                              {item.lotNumber && (
                                <div>Lot: {item.lotNumber}</div>
                              )}
                              {item.expirationDate && (
                                <div>Exp: {format(new Date(item.expirationDate), 'MMM dd, yyyy')}</div>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Landed Cost Tab */}
        <TabsContent value="landed-cost" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <MapPin className="h-5 w-5" />
                Landed Cost
              </CardTitle>
              <CardDescription>
                Additional costs to allocate across received items for this GRN
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div className="space-y-2 w-full md:max-w-[420px]">
                  <Label>Landed Cost Voucher</Label>
                  <Select
                    value={selectedLandedCostId}
                    onValueChange={async (value) => {
                      setSelectedLandedCostId(value);
                      if (!value) {
                        setLandedCostDetail(null);
                        return;
                      }
                      try {
                        setLandedCostLoading(true);
                        const detail = await inventoryManagementService.getLandedCostById(value);
                        setLandedCostDetail(detail);
                      } catch (err: any) {
                        console.error('Error fetching landed cost detail:', err);
                        toast.error(err?.message || 'Failed to load landed cost voucher');
                      } finally {
                        setLandedCostLoading(false);
                      }
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={landedCostLoading ? 'Loading...' : 'Select voucher'} />
                    </SelectTrigger>
                    <SelectContent>
                      {landedCosts.map((lc) => (
                        <SelectItem key={lc.id} value={lc.id}>
                          {lc.landedCostNumber} ({lc.status})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    Receipt costs apply only to received stock lines. PO estimates can be copied optionally.
                  </p>
                </div>

                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    onClick={() => fetchLandedCosts(false)}
                    disabled={landedCostLoading || landedCostAllocating || landedCostPosting}
                  >
                    {landedCostLoading && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                    Refresh
                  </Button>
                  <Button
                    onClick={allocateLandedCost}
                    disabled={!selectedLandedCostId || landedCostLoading || landedCostAllocating || landedCostPosting || Boolean(landedCostDetail?.status && landedCostDetail.status !== 'Draft')}
                  >
                    {landedCostAllocating && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                    Allocate
                  </Button>
                  {landedCostDetail && <LandedCostSupplierInvoices voucher={landedCostDetail}
                    disabled={landedCostLoading || landedCostAllocating || landedCostPosting}
                    onBusyChange={setLandedCostPosting} onCreated={() => {
                      void fetchLandedCosts(false);
                      void inventoryManagementService.getLandedCostById(landedCostDetail.id).then(setLandedCostDetail)
                        .catch(() => toast.error('Posting response received. Refresh to load the latest voucher and invoice links.'));
                    }} />}
                </div>
              </div>

              <ReceiptLandedCostEntry receipt={receipt} selected={landedCostDetail}
                disabled={landedCostLoading || landedCostAllocating || landedCostPosting}
                onSaved={voucher => {
                  setLandedCostDetail(null);
                  setLandedCosts(previous => [voucher, ...previous.filter(c => c.id !== voucher.id)]);
                  setSelectedLandedCostId(voucher.id);
                  void inventoryManagementService.getLandedCostById(voucher.id).then(setLandedCostDetail)
                    .catch(() => toast.error('Costs saved. Refresh to load the voucher details.'));
                }} />
              {landedCostLoading ? (
                <div className="flex items-center justify-center py-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : landedCosts.length === 0 ? (
                <div className="border rounded-lg p-6 text-center space-y-3">
                  <p className="text-sm text-muted-foreground">
                    No landed cost voucher exists for this GRN yet.
                  </p>
                  <Button onClick={initializeLandedCostFromPo} disabled={landedCostLoading}>
                    Copy PO estimates (optional)
                  </Button>
                </div>
              ) : !landedCostDetail ? (
                <div className="border rounded-lg p-6 text-center text-sm text-muted-foreground">
                  Select a voucher to view details.
                </div>
              ) : (
                <>
                  <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                    <Card>
                      <CardHeader className="py-4">
                        <CardTitle className="text-sm">Voucher</CardTitle>
                      </CardHeader>
                      <CardContent className="pt-0">
                        <div className="font-medium">{landedCostDetail.landedCostNumber}</div>
                        <div className="text-xs text-muted-foreground">{landedCostDetail.createdAtFormatted}</div>
                      </CardContent>
                    </Card>
                    <Card>
                      <CardHeader className="py-4">
                        <CardTitle className="text-sm">Status</CardTitle>
                      </CardHeader>
                      <CardContent className="pt-0">
                        <Badge variant="outline">{landedCostDetail.status}</Badge>
                      </CardContent>
                    </Card>
                    <Card>
                      <CardHeader className="py-4">
                        <CardTitle className="text-sm">Total</CardTitle>
                      </CardHeader>
                      <CardContent className="pt-0">
                        <div className="font-medium">
                          {new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(landedCostDetail.totalCostAmount)} {landedCostDetail.currency}
                        </div>
                      </CardContent>
                    </Card>
                    <Card>
                      <CardHeader className="py-4">
                        <CardTitle className="text-sm">Allocated</CardTitle>
                      </CardHeader>
                      <CardContent className="pt-0">
                        <div className="font-medium">
                          {new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(landedCostDetail.allocatedAmount)} {landedCostDetail.currency}
                        </div>
                      </CardContent>
                    </Card>
                  </div>

                  {/* Cost Lines */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-base">Cost Lines</CardTitle>
                      <CardDescription>Supplier services like freight, insurance, handling, duty, etc.</CardDescription>
                    </CardHeader>
                    <CardContent>
                      {!landedCostDetail.costItems || landedCostDetail.costItems.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                          No cost lines
                        </div>
                      ) : (
                        <div className="border rounded-lg overflow-hidden">
                          <Table>
                            <TableHeader>
                              <TableRow>
                                <TableHead>Type</TableHead>
                                <TableHead>Supplier</TableHead>
                                <TableHead>Description</TableHead>
                                <TableHead className="text-right">Amount</TableHead>
                                <TableHead>Currency</TableHead>
                                <TableHead>Method</TableHead>
                              </TableRow>
                            </TableHeader>
                            <TableBody>
                              {landedCostDetail.costItems.map((ci) => (
                                <TableRow key={ci.id}>
                                  <TableCell className="font-medium">{getLandedCostTypeLabel(ci.costType)}
                                    <div className="text-xs font-normal text-muted-foreground">{ci.purchaseOrderItemId ? receipt.items?.find(i => i.purchaseOrderItemId === ci.purchaseOrderItemId)?.itemName || 'Specific received item' : 'All received stock items'}</div>
                                  </TableCell>
                                  <TableCell>{ci.supplierName || '-'}</TableCell>
                                  <TableCell>{ci.description}<div className="mt-2 text-xs"><LandedCostInvoiceLink voucherId={landedCostDetail.id} item={ci}
                                    onChanged={() => { void inventoryManagementService.getLandedCostById(landedCostDetail.id).then(setLandedCostDetail); }} /></div></TableCell>
                                  <TableCell className="text-right">
                                    {new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(ci.amount)}
                                  </TableCell>
                                  <TableCell>{ci.currency}</TableCell>
                                  <TableCell>{ci.allocationMethod}</TableCell>
                                </TableRow>
                              ))}
                            </TableBody>
                          </Table>
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  <LandedCostSupplierSummary lines={landedCostDetail.costItems} />

                  {/* Allocations */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-base">Allocations</CardTitle>
                      <CardDescription>Allocated landed cost per received item</CardDescription>
                    </CardHeader>
                    <CardContent>
                      {!landedCostDetail.allocations || landedCostDetail.allocations.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                          No allocations yet. Click Allocate to distribute the cost lines.
                        </div>
                      ) : (
                        <div className="border rounded-lg overflow-hidden">
                          <Table>
                            <TableHeader>
                              <TableRow>
                                <TableHead>Item</TableHead>
                                <TableHead className="text-right">Allocated</TableHead>
                                <TableHead className="text-right">%</TableHead>
                                <TableHead className="text-right">New Unit Cost</TableHead>
                              </TableRow>
                            </TableHeader>
                            <TableBody>
                              {landedCostDetail.allocations.map((a) => (
                                <TableRow key={a.id}>
                                  <TableCell>
                                    <div className="font-medium">{a.itemCode}</div>
                                    <div className="text-xs text-muted-foreground">{a.itemName}</div>
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(a.allocatedAmount)}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(a.allocationPercent)}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {new Intl.NumberFormat(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 4 }).format(a.newUnitCost)}
                                  </TableCell>
                                </TableRow>
                              ))}
                            </TableBody>
                          </Table>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Quality Inspection Tab */}
        {receipt.requiresInspection && (
          <TabsContent value="inspection" className="space-y-6">
            <PurchaseOrderSodControl
              purchaseOrderId={receipt.purchaseOrderId}
              status={receipt.status}
              scope="receipt"
            />
            <ReceiptInspectionControl receiptId={id} onChanged={() => void fetchReceipt()} />
          </TabsContent>
        )}
        <TabsContent value="documents" className="space-y-6">
          <ReceiptSourceEvidenceControl receiptId={id} />
          <ReceiptDocumentControl receiptId={id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
