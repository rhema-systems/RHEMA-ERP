'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
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
import { format } from 'date-fns';
import Link from 'next/link';

const GRNStatuses = [
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
  const id = params.id as string;
  
  const [receipt, setReceipt] = useState<PurchaseOrderReceiptDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('details');

  const handlePrint = async () => {
    try {
      const blob = await purchasingService.getPurchaseReceiptGrnPdf(id);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating GRN PDF:', err);
      toast.error('Failed to generate GRN PDF');
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

  useEffect(() => {
    if (id) {
      fetchReceipt();
    }
  }, [id]);

  const getStatusBadge = (status: string) => {
    const statusConfig = GRNStatuses.find(s => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge className={statusConfig?.color || 'bg-gray-100'}>
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
                <Badge variant="outline" className="bg-yellow-50">
                  <AlertTriangle className="h-3 w-3 mr-1" />
                  Inspection Required
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">Goods Receipt Note Details</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2 print:hidden">
          <Button variant="outline" onClick={handlePrint}>
            <Printer className="h-4 w-4 mr-2" />
            Print GRN
          </Button>
        </div>
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
        <TabsList className="print:hidden">
          <TabsTrigger value="details">Receipt Details</TabsTrigger>
          <TabsTrigger value="items">Items Received</TabsTrigger>
          {receipt.requiresInspection && (
            <TabsTrigger value="inspection">Quality Inspection</TabsTrigger>
          )}
        </TabsList>

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

        {/* Quality Inspection Tab */}
        {receipt.requiresInspection && (
          <TabsContent value="inspection" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <AlertTriangle className="h-5 w-5" />
                  Quality Inspection
                </CardTitle>
                <CardDescription>
                  Quality inspection details and results
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {receipt.inspectedByName && (
                    <div>
                      <Label className="text-muted-foreground">Inspector</Label>
                      <p className="font-medium mt-1">{receipt.inspectedByName}</p>
                    </div>
                  )}
                  
                  {receipt.inspectionDate && (
                    <div>
                      <Label className="text-muted-foreground">Inspection Date</Label>
                      <p className="font-medium mt-1">
                        {format(new Date(receipt.inspectionDate), 'MMM dd, yyyy HH:mm')}
                      </p>
                    </div>
                  )}
                  
                  {receipt.inspectionResult && (
                    <div>
                      <Label className="text-muted-foreground">Inspection Result</Label>
                      <div className="mt-1">
                        <Badge className={
                          receipt.inspectionResult === 'Passed' ? 'bg-green-100 text-green-800' :
                          receipt.inspectionResult === 'Failed' ? 'bg-red-100 text-red-800' :
                          'bg-yellow-100 text-yellow-800'
                        }>
                          {receipt.inspectionResult}
                        </Badge>
                      </div>
                    </div>
                  )}
                </div>
                
                {receipt.inspectionNotes && (
                  <>
                    <Separator />
                    <div>
                      <Label className="text-muted-foreground">Inspection Notes</Label>
                      <p className="mt-2 text-sm whitespace-pre-wrap">{receipt.inspectionNotes}</p>
                    </div>
                  </>
                )}
                
                {/* Item-level quality status */}
                {receipt.items && receipt.items.length > 0 && (
                  <>
                    <Separator />
                    <div>
                      <Label className="text-muted-foreground mb-3 block">Item Quality Status</Label>
                      <div className="space-y-2">
                        {receipt.items.map((item, index) => (
                          <div key={item.id} className="flex items-center justify-between p-3 border rounded-lg">
                            <div>
                              <p className="font-medium">{item.itemCode} - {item.itemName}</p>
                              {item.qualityNotes && (
                                <p className="text-xs text-muted-foreground mt-1">{item.qualityNotes}</p>
                              )}
                            </div>
                            <div className="text-right">
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
                              <div className="text-xs text-muted-foreground mt-1">
                                Accepted: {item.acceptedQuantity} / Rejected: {item.rejectedQuantity}
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
}
