'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
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
  ShoppingCart,
  Printer,
  Download,
  AlertCircle,
  Loader2,
  Edit
} from 'lucide-react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseRequisitionDetailDto,
  ApprovalDto
} from '@/services/purchasingService';
import { format } from 'date-fns';
import Link from 'next/link';

const PRStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800', icon: FileText },
  { value: 'Submitted', label: 'Submitted', color: 'bg-blue-100 text-blue-800', icon: Clock },
  { value: 'Pending Approval', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800', icon: Clock },
  { value: 'Approved', label: 'Approved', color: 'bg-green-100 text-green-800', icon: CheckCircle },
  { value: 'Rejected', label: 'Rejected', color: 'bg-red-100 text-red-800', icon: XCircle },
  { value: 'Ordered', label: 'Ordered', color: 'bg-purple-100 text-purple-800', icon: ShoppingCart }
];

const PRPriorities = [
  { value: 'Low', label: 'Low', color: 'bg-gray-100 text-gray-600' },
  { value: 'Normal', label: 'Normal', color: 'bg-blue-100 text-blue-600' },
  { value: 'High', label: 'High', color: 'bg-orange-100 text-orange-600' },
  { value: 'Urgent', label: 'Urgent', color: 'bg-red-100 text-red-600' }
];

export default function PurchaseRequisitionDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;
  
  const [requisition, setRequisition] = useState<PurchaseRequisitionDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  

  const fetchRequisition = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await purchasingService.getPurchaseRequisitionById(id);
      setRequisition(data);
    } catch (err: any) {
      console.error('Error fetching purchase requisition:', err);
      setError('Failed to load purchase requisition');
      toast.error('Failed to load purchase requisition');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (id) {
      fetchRequisition();
    }
  }, [id]);

  const getStatusBadge = (status: string) => {
    const statusConfig = PRStatuses.find(s => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge className={statusConfig?.color || 'bg-gray-100'}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig?.label || status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const priorityConfig = PRPriorities.find(p => p.value === priority);
    return (
      <Badge variant="outline" className={priorityConfig?.color || 'bg-gray-100'}>
        {priorityConfig?.label || priority}
      </Badge>
    );
  };

  // Submit/approve/reject UX is centralized in <WorkflowApprovalActions />.

  const handleConvertToPO = async () => {
    try {
      const poData = await purchasingService.convertToPurchaseOrder(id);
      toast.success('Redirecting to create purchase order...');
      // Navigate to PO creation page with pre-filled data
      router.push(`/procurement/purchase-orders/new?fromRequisition=${id}`);
    } catch (error: any) {
      console.error('Error converting to PO:', error);
      toast.error(error.message || 'Failed to convert to purchase order');
    }
  };

  const handleCreateRfq = async () => {
    try {
      const result = await purchasingService.createRfqFromPurchaseRequisition(id);
      toast.success(`RFQ created (${result.rfqNumber}). Redirecting...`);
      router.push(`/procurement/rfqs/${result.rfqId}/edit?fromRequisitionId=${id}`);
    } catch (error: any) {
      console.error('Error creating RFQ:', error);
      toast.error(error.message || 'Failed to create RFQ');
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error || !requisition) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-requisitions')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
        </div>
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <AlertCircle className="h-5 w-5 text-red-600" />
              <p className="text-red-900">{error || 'Purchase requisition not found'}</p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const canEdit = requisition.status === 'Draft';
  const canSubmit = requisition.status === 'Draft';
  const canApprove = requisition.status === 'Pending Approval' || requisition.status === 'Submitted';
  const canConvertToPO = requisition.status === 'Approved';
  const canCreateRfq = requisition.status === 'Approved';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-requisitions')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold">{requisition.requisitionNumber}</h1>
              {getStatusBadge(requisition.status)}
              {getPriorityBadge(requisition.priority)}
              {(requisition.status === 'Pending Approval' || requisition.status === 'Submitted') && requisition.currentWorkflowStepName && (
                <Badge variant="outline" className="text-muted-foreground">
                  Step: {requisition.currentWorkflowStepName}
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">Purchase Requisition Details</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          {canEdit && (
            <Link href={`/procurement/purchase-requisitions/${id}/edit`}>
              <Button variant="outline">
                <Edit className="h-4 w-4 mr-2" />
                Edit
              </Button>
            </Link>
          )}
          
          <WorkflowApprovalActions
            entityType="PurchaseRequisition"
            entityId={id}
            entityLabel="Purchase Requisition"
            entityNumber={requisition.requisitionNumber}
            status={requisition.status}
            currentStepName={requisition.currentWorkflowStepName}
            loadWorkflowSummary
            canSubmit={canSubmit}
            canApproveReject={canApprove}
            onSubmit={async () => {
              await purchasingService.submitPurchaseRequisition(id);
            }}
            onApprove={async (comments) => {
              await purchasingService.approvePurchaseRequisition(id, {
                approved: true,
                comments: comments || undefined,
              });
            }}
            onReject={async (comments) => {
              await purchasingService.approvePurchaseRequisition(id, {
                approved: false,
                comments: comments || undefined,
                rejectionReason: comments || undefined,
              });
            }}
            onAfterAction={fetchRequisition}
            onOpenWorkflows={() => router.push('/administration/workflow')}
          />
          
          {canConvertToPO && (
            <Button onClick={handleConvertToPO}>
              <ShoppingCart className="h-4 w-4 mr-2" />
              Create Purchase Order
            </Button>
          )}

          {canCreateRfq && (
            <Button variant="outline" onClick={handleCreateRfq}>
              <FileText className="h-4 w-4 mr-2" />
              Create RFQ
            </Button>
          )}
          
          <Button variant="outline">
            <Printer className="h-4 w-4 mr-2" />
            Print
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
            <BreadcrumbLink href="/procurement/purchase-requisitions">Purchase Requisitions</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>{requisition.requisitionNumber}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Rejection Notice */}
      {requisition.status === 'Rejected' && requisition.rejectionReason && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <XCircle className="h-5 w-5 text-red-600 mt-0.5" />
              <div>
                <p className="font-medium text-red-900">Requisition Rejected</p>
                <p className="text-sm text-red-700 mt-1">{requisition.rejectionReason}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Main Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="items">Items ({requisition.itemCount})</TabsTrigger>
          <TabsTrigger value="approval">Approval History</TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-6">
          {/* Basic Information */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <FileText className="h-5 w-5" />
                Basic Information
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <Label className="text-muted-foreground">Requisition Number</Label>
                  <p className="font-medium mt-1">{requisition.requisitionNumber}</p>
                </div>
                
                <div>
                  <Label className="text-muted-foreground flex items-center gap-2">
                    <Calendar className="h-4 w-4" />
                    Requisition Date
                  </Label>
                  <p className="font-medium mt-1">
                    {format(new Date(requisition.requisitionDate), 'MMM dd, yyyy')}
                  </p>
                </div>
                
                <div>
                  <Label className="text-muted-foreground flex items-center gap-2">
                    <User className="h-4 w-4" />
                    Requested By
                  </Label>
                  <p className="font-medium mt-1">{requisition.requestedByName}</p>
                </div>
                
                {requisition.requiredDate && (
                  <div>
                    <Label className="text-muted-foreground">Required Date</Label>
                    <p className="font-medium mt-1">
                      {format(new Date(requisition.requiredDate), 'MMM dd, yyyy')}
                    </p>
                  </div>
                )}
                
                <div>
                  <Label className="text-muted-foreground">Priority</Label>
                  <div className="mt-1">{getPriorityBadge(requisition.priority)}</div>
                </div>
                
                <div>
                  <Label className="text-muted-foreground">Status</Label>
                  <div className="mt-1">{getStatusBadge(requisition.status)}</div>
                </div>
                
                {requisition.department && (
                  <div>
                    <Label className="text-muted-foreground flex items-center gap-2">
                      <Building2 className="h-4 w-4" />
                      Department
                    </Label>
                    <p className="font-medium mt-1">{requisition.department}</p>
                  </div>
                )}
                
                {requisition.costCenter && (
                  <div>
                    <Label className="text-muted-foreground">Cost Center</Label>
                    <p className="font-medium mt-1">{requisition.costCenter}</p>
                  </div>
                )}
              </div>
              
              {requisition.justification && (
                <>
                  <Separator className="my-6" />
                  <div>
                    <Label className="text-muted-foreground">Justification</Label>
                    <p className="mt-2 text-sm whitespace-pre-wrap">{requisition.justification}</p>
                  </div>
                </>
              )}
              
              {requisition.notes && (
                <>
                  <Separator className="my-6" />
                  <div>
                    <Label className="text-muted-foreground">Notes</Label>
                    <p className="mt-2 text-sm whitespace-pre-wrap">{requisition.notes}</p>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

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
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">Total Items:</span>
                  <span className="font-medium">{requisition.itemCount}</span>
                </div>
                <Separator />
                <div className="flex justify-between items-center">
                  <span className="text-lg font-semibold">Total Amount:</span>
                  <span className="text-2xl font-bold text-primary">
                    ${requisition.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Approval Information */}
          {(requisition.approvedByName || requisition.status === 'Rejected') && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  {requisition.status === 'Approved' ? (
                    <CheckCircle className="h-5 w-5 text-green-600" />
                  ) : (
                    <XCircle className="h-5 w-5 text-red-600" />
                  )}
                  Approval Information
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {requisition.approvedByName && (
                    <div>
                      <Label className="text-muted-foreground">
                        {requisition.status === 'Approved' ? 'Approved By' : 'Reviewed By'}
                      </Label>
                      <p className="font-medium mt-1">{requisition.approvedByName}</p>
                    </div>
                  )}
                  
                  {requisition.approvedAt && (
                    <div>
                      <Label className="text-muted-foreground">
                        {requisition.status === 'Approved' ? 'Approved At' : 'Reviewed At'}
                      </Label>
                      <p className="font-medium mt-1">
                        {format(new Date(requisition.approvedAt), 'MMM dd, yyyy HH:mm')}
                      </p>
                    </div>
                  )}
                  
                  {requisition.rejectionReason && (
                    <div className="md:col-span-2">
                      <Label className="text-muted-foreground">Rejection Reason</Label>
                      <p className="mt-2 text-sm text-red-700 whitespace-pre-wrap">
                        {requisition.rejectionReason}
                      </p>
                    </div>
                  )}
                </div>
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
                Requisition Items
              </CardTitle>
              <CardDescription>
                {requisition.itemCount} item(s) in this requisition
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="border rounded-lg overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[50px]">#</TableHead>
                      <TableHead>Item Code</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead className="text-right">Quantity</TableHead>
                      <TableHead>UOM</TableHead>
                      <TableHead className="text-right">Est. Unit Price</TableHead>
                      <TableHead className="text-right">Line Total</TableHead>
                      <TableHead>Preferred Supplier</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {requisition.items.map((item, index) => (
                      <TableRow key={item.id}>
                        <TableCell className="font-medium">{index + 1}</TableCell>
                        <TableCell>
                          <div className="font-medium">{item.itemCode || 'N/A'}</div>
                          <div className="text-sm text-muted-foreground">{item.itemName || 'Custom'}</div>
                        </TableCell>
                        <TableCell className="max-w-[300px]">
                          <div className="font-medium">{item.itemDescription}</div>
                          {item.specifications && (
                            <div className="text-xs text-muted-foreground mt-1">
                              Specs: {item.specifications}
                            </div>
                          )}
                          {item.notes && (
                            <div className="text-xs text-muted-foreground mt-1">
                              Notes: {item.notes}
                            </div>
                          )}
                          {item.requiredDate && (
                            <div className="text-xs text-muted-foreground mt-1">
                              Required: {format(new Date(item.requiredDate), 'MMM dd, yyyy')}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-right">{item.quantity}</TableCell>
                        <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                        <TableCell className="text-right">
                          ${item.estimatedUnitPrice.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          ${item.lineTotal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </TableCell>
                        <TableCell>
                          <div className="text-sm">{item.preferredSupplierName || '-'}</div>
                        </TableCell>
                        <TableCell>
                          {item.purchaseOrderNumber ? (
                            <Link href={`/procurement/purchase-orders/${item.purchaseOrderId}`}>
                              <Badge className="bg-purple-100 text-purple-800 hover:bg-purple-200">
                                PO: {item.purchaseOrderNumber}
                              </Badge>
                            </Link>
                          ) : (
                            <Badge variant="outline">{item.status}</Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              
              {/* Items Total */}
              <div className="flex justify-end mt-6">
                <Card className="w-full md:w-96">
                  <CardContent className="pt-6">
                    <div className="space-y-2">
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Total Items:</span>
                        <span className="font-medium">{requisition.itemCount}</span>
                      </div>
                      <Separator />
                      <div className="flex justify-between">
                        <span className="font-semibold">Total Amount:</span>
                        <span className="text-xl font-bold text-primary">
                          ${requisition.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Approval History Tab */}
        <TabsContent value="approval" className="space-y-6">
          <WorkflowApprovalHistoryPanel entityType="PurchaseRequisition" entityId={id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
