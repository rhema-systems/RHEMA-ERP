'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
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
  DollarSign,
  CheckCircle,
  XCircle,
  Clock,
  Send,
  TruckIcon,
  Printer,
  Download,
  AlertCircle,
  Loader2,
  Edit,
  AlertTriangle,
  MapPin,
  CreditCard
} from 'lucide-react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseOrderDetailDto,
  PurchaseOrderLandedCostPlanDto,
  ApprovalDto
} from '@/services/purchasingService';
import { format } from 'date-fns';
import Link from 'next/link';

const LANDED_COST_TYPES: Array<{ value: number; label: string }> = [
  { value: 1, label: 'Freight / Shipping' },
  { value: 2, label: 'Customs Duty' },
  { value: 3, label: 'Insurance' },
  { value: 4, label: 'Handling' },
  { value: 5, label: 'Brokerage' },
  { value: 6, label: 'Storage / Warehousing' },
  { value: 7, label: 'Other' },
];

const getLandedCostTypeLabel = (costType: number) =>
  LANDED_COST_TYPES.find(t => t.value === costType)?.label || 'Other';

const POStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800', icon: FileText },
  { value: 'Pending Approval', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800', icon: Clock },
  { value: 'Approved', label: 'Approved', color: 'bg-green-100 text-green-800', icon: CheckCircle },
  { value: 'Sent', label: 'Sent', color: 'bg-blue-100 text-blue-800', icon: Send },
  { value: 'Acknowledged', label: 'Acknowledged', color: 'bg-indigo-100 text-indigo-800', icon: CheckCircle },
  { value: 'Partially Received', label: 'Partially Received', color: 'bg-purple-100 text-purple-800', icon: Package },
  { value: 'Received', label: 'Received', color: 'bg-teal-100 text-teal-800', icon: TruckIcon },
  { value: 'Cancelled', label: 'Cancelled', color: 'bg-red-100 text-red-800', icon: XCircle }
];

export default function PurchaseOrderDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  
  const [order, setOrder] = useState<PurchaseOrderDetailDto | null>(null);
  const [landedCostPlan, setLandedCostPlan] = useState<PurchaseOrderLandedCostPlanDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  
  // Submit/approve/reject UX is centralized in <WorkflowApprovalActions />.

  const fetchOrder = async () => {
    try {
      setLoading(true);
      setError(null);
      const [data, plan] = await Promise.all([
        purchasingService.getPurchaseOrderById(id),
        purchasingService.getPurchaseOrderLandedCostPlan(id)
      ]);
      setOrder(data);
      setLandedCostPlan(plan);
    } catch (err: any) {
      console.error('Error fetching purchase order:', err);
      setError('Failed to load purchase order');
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

  const getStatusBadge = (status: string) => {
    const statusConfig = POStatuses.find(s => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge className={statusConfig?.color || 'bg-gray-100'}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig?.label || status}
      </Badge>
    );
  };

  // (see header actions below)

  const calculateReceiptProgress = (orderedQty: number, receivedQty: number) => {
    if (orderedQty === 0) return 0;
    return Math.min((receivedQty / orderedQty) * 100, 100);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error || !order) {
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
              <p className="text-red-900">{error || 'Purchase order not found'}</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const canEdit = order.status === 'Draft';
  const canSubmit = order.status === 'Draft';
  const canApprove = order.status === 'Pending Approval';
  const canReceive = order.status === 'Approved' || order.status === 'Sent' || 
                     order.status === 'Acknowledged' || order.status === 'Partially Received';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-orders')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold">{order.orderNumber}</h1>
              {getStatusBadge(order.status)}
              {(order.status === 'Pending Approval' || order.status === 'Submitted') && order.currentWorkflowStepName && (
                <Badge variant="outline" className="text-xs">
                  Step: {order.currentWorkflowStepName}
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">Purchase Order Details</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          {canEdit && (
            <Link href={`/procurement/purchase-orders/${id}/edit`}>
              <Button variant="outline">
                <Edit className="h-4 w-4 mr-2" />
                Edit
              </Button>
            </Link>
          )}
          
          <WorkflowApprovalActions
            entityType="PurchaseOrder"
            entityId={id}
            entityLabel="Purchase Order"
            entityNumber={order.orderNumber}
            status={order.status}
            currentStepName={order.currentWorkflowStepName}
            loadWorkflowSummary
            canSubmit={canSubmit}
            canApproveReject={canApprove}
            onSubmit={async () => {
              await purchasingService.submitPurchaseOrder(id);
            }}
            onApprove={async (comments) => {
              await purchasingService.approvePurchaseOrder(id, {
                approved: true,
                comments: comments || undefined,
              });
            }}
            onReject={async (comments) => {
              await purchasingService.approvePurchaseOrder(id, {
                approved: false,
                comments: comments || undefined,
                rejectionReason: comments || undefined,
              });
            }}
            onAfterAction={fetchOrder}
            onOpenWorkflows={() => router.push('/administration/workflow')}
          />
          
          {canReceive && (
            <Link href={`/procurement/purchase-orders/${id}/receive`}>
              <Button>
                <Package className="h-4 w-4 mr-2" />
                Receive Goods
              </Button>
            </Link>
          )}
          
          <Button variant="outline">
            <Printer className="h-4 w-4 mr-2" />
            Print
          </Button>
          
          <Button variant="outline">
            <Download className="h-4 w-4 mr-2" />
            Export PDF
          </Button>
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
            <BreadcrumbPage>{order.orderNumber}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Main Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="items">Items ({order.itemCount})</TabsTrigger>
          <TabsTrigger value="receipts">Receipts ({order.receipts?.length || 0})</TabsTrigger>
          <WorkflowTabTrigger value="approval" />
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-6">
          {/* Order Information */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Left Column */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <FileText className="h-5 w-5" />
                  Order Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <Label className="text-muted-foreground">Order Number</Label>
                  <p className="font-medium mt-1">{order.orderNumber}</p>
                </div>
                
                <div>
                  <Label className="text-muted-foreground flex items-center gap-2">
                    <Calendar className="h-4 w-4" />
                    Order Date
                  </Label>
                  <p className="font-medium mt-1">
                    {format(new Date(order.orderDate), 'MMM dd, yyyy')}
                  </p>
                </div>
                
                {order.requiredDate && (
                  <div>
                    <Label className="text-muted-foreground">Required Date</Label>
                    <p className="font-medium mt-1">
                      {format(new Date(order.requiredDate), 'MMM dd, yyyy')}
                    </p>
                  </div>
                )}
                
                {order.promisedDate && (
                  <div>
                    <Label className="text-muted-foreground">Promised Date</Label>
                    <p className="font-medium mt-1">
                      {format(new Date(order.promisedDate), 'MMM dd, yyyy')}
                    </p>
                  </div>
                )}
                
                {order.receivedDate && (
                  <div>
                    <Label className="text-muted-foreground">Received Date</Label>
                    <p className="font-medium mt-1">
                      {format(new Date(order.receivedDate), 'MMM dd, yyyy')}
                    </p>
                  </div>
                )}
                
                <div>
                  <Label className="text-muted-foreground">Status</Label>
                  <div className="mt-1">{getStatusBadge(order.status)}</div>
                </div>
                
                {order.requestedByName && (
                  <div>
                    <Label className="text-muted-foreground flex items-center gap-2">
                      <User className="h-4 w-4" />
                      Requested By
                    </Label>
                    <p className="font-medium mt-1">{order.requestedByName}</p>
                  </div>
                )}
                
                {order.referenceNumber && (
                  <div>
                    <Label className="text-muted-foreground">Reference Number</Label>
                    <p className="font-medium mt-1">{order.referenceNumber}</p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Right Column */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Building2 className="h-5 w-5" />
                  Supplier Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <Label className="text-muted-foreground">Supplier Name</Label>
                  <Link href={`/procurement/business-partners/${order.supplierId}`}>
                    <p className="font-medium mt-1 text-blue-600 hover:underline">
                      {order.supplierName}
                    </p>
                  </Link>
                </div>
                
                {order.supplierPhone && (
                  <div>
                    <Label className="text-muted-foreground">Phone</Label>
                    <p className="font-medium mt-1">{order.supplierPhone}</p>
                  </div>
                )}
                
                {order.supplierEmail && (
                  <div>
                    <Label className="text-muted-foreground">Email</Label>
                    <p className="font-medium mt-1">{order.supplierEmail}</p>
                  </div>
                )}
                
                {order.supplierAddress && (
                  <div>
                    <Label className="text-muted-foreground flex items-center gap-2">
                      <MapPin className="h-4 w-4" />
                      Address
                    </Label>
                    <p className="text-sm mt-1">{order.supplierAddress}</p>
                  </div>
                )}
                
                {order.paymentTerms && (
                  <div>
                    <Label className="text-muted-foreground flex items-center gap-2">
                      <CreditCard className="h-4 w-4" />
                      Payment Terms
                    </Label>
                    <p className="font-medium mt-1">{order.paymentTerms}</p>
                  </div>
                )}
                
                {order.shippingTerms && (
                  <div>
                    <Label className="text-muted-foreground">Shipping Terms</Label>
                    <p className="font-medium mt-1">{order.shippingTerms}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Delivery Information */}
          {(order.deliveryAddress || order.deliveryInstructions) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <TruckIcon className="h-5 w-5" />
                  Delivery Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {order.deliveryAddress && (
                  <div>
                    <Label className="text-muted-foreground">Delivery Address</Label>
                    <p className="mt-1 text-sm">{order.deliveryAddress}</p>
                  </div>
                )}
                
                {order.deliveryInstructions && (
                  <div>
                    <Label className="text-muted-foreground">Delivery Instructions</Label>
                    <p className="mt-1 text-sm whitespace-pre-wrap">{order.deliveryInstructions}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Financial Summary */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <DollarSign className="h-5 w-5" />
                Financial Summary
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Subtotal:</span>
                  <span className="font-medium">
                    ${order.subTotal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Tax:</span>
                  <span className="font-medium">
                    ${order.taxAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Shipping:</span>
                  <span className="font-medium">
                    ${order.shippingCost.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Miscellaneous:</span>
                  <span className="font-medium">
                    ${(order.miscellaneousCost || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Total Additional Cost:</span>
                  <span className="font-medium">
                    ${(order.totalAdditionalCost || (order.shippingCost + (order.miscellaneousCost || 0))).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Discount:</span>
                  <span className="font-medium text-green-600">
                    -${order.discountAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>

                <Separator />

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Allocation Method:</span>
                  <span className="font-medium">
                    {order.costAllocationMethod === 'GLExpense' ? 'Post to GL expense' : 'Spread to item cost'}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Spread Basis:</span>
                  <span className="font-medium">{order.costApportionmentBasis || 'Value'}</span>
                </div>

                {order.costAllocationMethod === 'GLExpense' && (
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">GL Expense Account:</span>
                    <span className="font-medium">{order.expenseGLAccount || '-'}</span>
                  </div>
                )}

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Costs Allocated:</span>
                  <span className="font-medium">{order.costsAllocated ? 'Yes' : 'No'}</span>
                </div>
                
                <Separator />
                
                <div className="flex justify-between items-center">
                  <span className="text-lg font-semibold">Total Amount:</span>
                  <span className="text-2xl font-bold text-primary">
                    ${order.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Planned Landed Costs (carried to GRN) */}
          {landedCostPlan && (landedCostPlan.items?.length || 0) > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <TruckIcon className="h-5 w-5" />
                  Planned Landed Costs (carried to GRN)
                </CardTitle>
                <CardDescription>
                  Total planned ({(landedCostPlan.currency || 'USD').toUpperCase()}):{' '}
                  {(landedCostPlan.totalPlannedCost || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {landedCostPlan.notes && (
                  <div>
                    <Label className="text-muted-foreground">Notes</Label>
                    <p className="mt-1 text-sm whitespace-pre-wrap">{landedCostPlan.notes}</p>
                  </div>
                )}

                <div className="border rounded-lg overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="min-w-[180px]">Type</TableHead>
                        <TableHead>Description</TableHead>
                        <TableHead className="min-w-[220px]">Service Supplier</TableHead>
                        <TableHead className="min-w-[140px]">Allocation</TableHead>
                        <TableHead className="min-w-[120px] text-right">Amount</TableHead>
                        <TableHead className="min-w-[90px]">Curr</TableHead>
                        <TableHead className="min-w-[100px] text-right">Rate</TableHead>
                        <TableHead className="min-w-[150px] text-right">In Plan Curr</TableHead>
                        <TableHead className="min-w-[140px]">Ref</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {landedCostPlan.items.map((i) => (
                        <TableRow key={i.id}>
                          <TableCell className="font-medium">{getLandedCostTypeLabel(i.costType)}</TableCell>
                          <TableCell className="max-w-[420px] truncate" title={i.description}>
                            {i.description}
                          </TableCell>
                          <TableCell>{i.supplierName || '-'}</TableCell>
                          <TableCell>{i.allocationMethod}</TableCell>
                          <TableCell className="text-right">
                            {i.amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </TableCell>
                          <TableCell>{(i.currency || landedCostPlan.currency || 'USD').toUpperCase()}</TableCell>
                          <TableCell className="text-right">
                            {(i.exchangeRate || 1).toLocaleString('en-US', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}
                          </TableCell>
                          <TableCell className="text-right font-medium">
                            {(i.amountInPlanCurrency || (i.amount * (i.exchangeRate || 1))).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </TableCell>
                          <TableCell>{i.referenceNumber || '-'}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Terms & Notes */}
          {(order.terms || order.notes) && (
            <Card>
              <CardHeader>
                <CardTitle>Additional Information</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {order.terms && (
                  <div>
                    <Label className="text-muted-foreground">Terms & Conditions</Label>
                    <p className="mt-2 text-sm whitespace-pre-wrap">{order.terms}</p>
                  </div>
                )}
                
                {order.notes && (
                  <>
                    {order.terms && <Separator />}
                    <div>
                      <Label className="text-muted-foreground">Notes</Label>
                      <p className="mt-2 text-sm whitespace-pre-wrap">{order.notes}</p>
                    </div>
                  </>
                )}
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Items Tab */}
        <TabsContent value="items" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Package className="h-5 w-5" />
                Order Items
              </CardTitle>
              <CardDescription>
                {order.itemCount} item(s) in this purchase order
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="border rounded-lg overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[50px]">#</TableHead>
                      <TableHead>Item Code</TableHead>
                      <TableHead>Item Name</TableHead>
                      <TableHead>Warehouse</TableHead>
                      <TableHead className="text-right">Ordered</TableHead>
                      <TableHead>UOM</TableHead>
                      <TableHead className="text-right">Received</TableHead>
                      <TableHead className="text-right">Remaining</TableHead>
                      <TableHead className="text-right">Unit Price</TableHead>
                      <TableHead className="text-right">Line Total</TableHead>
                      <TableHead>Progress</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {order.items.map((item, index) => {
                      const progress = calculateReceiptProgress(item.orderedQuantity, item.receivedQuantity);
                      
                      return (
                        <TableRow key={item.id}>
                          <TableCell className="font-medium">{index + 1}</TableCell>
                          <TableCell className="font-medium">{item.itemCode}</TableCell>
                          <TableCell>
                            <div>{item.itemName}</div>
                            {item.itemDescription && (
                              <div className="text-xs text-muted-foreground mt-1">
                                {item.itemDescription}
                              </div>
                            )}
                            {item.expectedDeliveryDate && (
                              <div className="text-xs text-muted-foreground mt-1">
                                Expected: {format(new Date(item.expectedDeliveryDate), 'MMM dd, yyyy')}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>{item.warehouseName || '-'}</TableCell>
                          <TableCell className="text-right">{item.orderedQuantity}</TableCell>
                          <TableCell>{item.unitOfMeasure || 'EA'}</TableCell>
                          <TableCell className="text-right text-green-600 font-medium">
                            {item.receivedQuantity}
                          </TableCell>
                          <TableCell className="text-right">
                            {item.remainingQuantity > 0 ? (
                              <span className="text-orange-600 font-medium">{item.remainingQuantity}</span>
                            ) : (
                              <span className="text-green-600">0</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            ${item.unitPrice.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </TableCell>
                          <TableCell className="text-right font-medium">
                            ${item.lineTotal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </TableCell>
                          <TableCell>
                            <div className="space-y-1">
                              <Progress value={progress} className="h-2" />
                              <p className="text-xs text-muted-foreground text-center">
                                {progress.toFixed(0)}%
                              </p>
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Receipts Tab */}
        <TabsContent value="receipts" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <TruckIcon className="h-5 w-5" />
                Goods Receipt Notes (GRN)
              </CardTitle>
              <CardDescription>
                {order.receipts?.length || 0} receipt(s) for this purchase order
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!order.receipts || order.receipts.length === 0 ? (
                <div className="text-center py-12 border-2 border-dashed rounded-lg">
                  <TruckIcon className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
                  <p className="text-muted-foreground mb-4">No receipts recorded yet</p>
                  {canReceive && (
                    <Link href={`/procurement/purchase-orders/${id}/receive`}>
                      <Button>
                        <Package className="h-4 w-4 mr-2" />
                        Receive Goods
                      </Button>
                    </Link>
                  )}
                </div>
              ) : (
                <div className="space-y-3">
                  {order.receipts.map((receipt) => (
                    <div
                      key={receipt.id}
                      className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-4">
                          <div className="w-12 h-12 rounded-lg bg-teal-100 flex items-center justify-center">
                            <TruckIcon className="h-6 w-6 text-teal-600" />
                          </div>
                          <div>
                            <div className="flex items-center space-x-2">
                              <h3 className="font-semibold">{receipt.receiptNumber}</h3>
                              <Badge className={
                                receipt.status === 'Completed' ? 'bg-green-100 text-green-800' :
                                receipt.status === 'Pending' ? 'bg-yellow-100 text-yellow-800' :
                                'bg-gray-100 text-gray-800'
                              }>
                                {receipt.status}
                              </Badge>
                            </div>
                            <p className="text-sm text-muted-foreground">
                              Date: {format(new Date(receipt.receiptDate), 'MMM dd, yyyy')} •
                              Received by: {receipt.receivedByName || 'N/A'}
                              {receipt.deliveryNote && ` • DN: ${receipt.deliveryNote}`}
                            </p>
                            {receipt.requiresInspection && (
                              <Badge variant="outline" className="mt-1">
                                <AlertCircle className="h-3 w-3 mr-1" />
                                Requires Inspection
                              </Badge>
                            )}
                          </div>
                        </div>
                        
                        <Link href={`/procurement/purchase-receipts/${receipt.id}`}>
                          <Button size="sm" variant="outline">
                            View Details
                          </Button>
                        </Link>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="approval"
          className="space-y-6"
          entityType="PurchaseOrder"
          entityId={id}
          entityLabel="Purchase Order"
          entityNumber={order.orderNumber}
          status={order.status}
          currentStepName={order.currentWorkflowStepName}
          canSubmit={canSubmit}
          canApproveReject={canApprove}
          onSubmit={async () => {
            await purchasingService.submitPurchaseOrder(id);
          }}
          onApprove={async (comments) => {
            await purchasingService.approvePurchaseOrder(id, {
              approved: true,
              comments: comments || undefined,
            });
          }}
          onReject={async (comments) => {
            await purchasingService.approvePurchaseOrder(id, {
              approved: false,
              comments: comments || undefined,
              rejectionReason: comments || undefined,
            });
          }}
          onAfterAction={fetchOrder}
          onOpenWorkflows={() => router.push('/administration/workflow')}
        />
      </Tabs>
    </div>
  );
}
