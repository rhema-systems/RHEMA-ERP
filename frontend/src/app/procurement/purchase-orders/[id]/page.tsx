'use client';

import React, { useState, useEffect, useRef } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { WorkflowApprovalActions, WorkflowTabContent, WorkflowTabTrigger, useWorkflowRecord } from '@/components/workflow';
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
  FilePenLine,
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
  ProcurementPurchaseOrderComplianceDto,
  ProcurementPurchaseOrderSodReadinessDto,
  ApprovalDto
} from '@/services/purchasingService';
import { PurchaseOrderComplianceGate } from '@/components/procurement/PurchaseOrderComplianceGate';
import { PurchaseOrderSodControl } from '@/components/procurement/PurchaseOrderSodControl';
import { PurchaseOrderBudgetCommitment } from '@/components/procurement/PurchaseOrderBudgetCommitment';
import { PurchaseOrderAmendmentWorkspace } from '@/components/procurement/PurchaseOrderAmendmentWorkspace';
import { format } from 'date-fns';
import Link from 'next/link';
import { formatProcurementMoney } from '@/lib/procurement-currency';
import { useAuth } from '@/hooks/use-auth';
import { resolvePurchaseOrderActionAccess } from '@/lib/purchase-order-actions';
import { exportProcurementDocumentPdf, printProcurementDocument } from '@/lib/procurement-document-output';
import {
  getPurchaseOrderStatusPresentation,
  isPurchaseOrderStatus,
  PurchaseOrderStatusKey,
} from '@/lib/purchase-order-status';

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

const POStatusIcons: Partial<
  Record<PurchaseOrderStatusKey, React.ComponentType<{ className?: string }>>
> = {
  Draft: FileText,
  Submitted: Clock,
  'Pending Approval': Clock,
  Approved: CheckCircle,
  Rejected: XCircle,
  Sent,
  Acknowledged: CheckCircle,
  'Partially Received': Package,
  Received: TruckIcon,
  Cancelled: XCircle,
  Closed: CheckCircle,
};

export default function PurchaseOrderDetailPage() {
  const router = useRouter();
  const { hasPermission } = useAuth();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  
  const [order, setOrder] = useState<PurchaseOrderDetailDto | null>(null);
  const [complianceReadiness, setComplianceReadiness] =
    useState<ProcurementPurchaseOrderComplianceDto | null>(null);
  const [sodReadiness, setSodReadiness] =
    useState<ProcurementPurchaseOrderSodReadinessDto | null>(null);
  const [landedCostPlan, setLandedCostPlan] = useState<PurchaseOrderLandedCostPlanDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [amendmentEditorRequest, setAmendmentEditorRequest] = useState(0);
  const [documentAction, setDocumentAction] = useState<'print' | 'pdf' | null>(null);
  const documentRef = useRef<HTMLDivElement>(null);
  
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
      const message = err?.message || 'Failed to load purchase order';
      setError(message);
      toast.error(message);
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
    const statusConfig = getPurchaseOrderStatusPresentation(status);
    const Icon = POStatusIcons[statusConfig.key] || FileText;
    return (
      <Badge variant="outline" className={statusConfig.badgeClass}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig.label}
      </Badge>
    );
  };

  const complianceIsCurrent =
    complianceReadiness?.purchaseOrderId === id;
  const complianceForwardBlocked =
    isPurchaseOrderStatus(order?.status, 'Draft', 'Pending Approval', 'Submitted') &&
    (!complianceIsCurrent || complianceReadiness?.isCompliant !== true);
  const complianceBlockedReason = complianceIsCurrent
    ? complianceReadiness?.blockedReasons[0] ||
      'All purchase-order compliance checks must pass before progressing.'
    : 'Wait for the purchase-order compliance check to finish.';
  const sodIsCurrent = sodReadiness?.purchaseOrderId === id;
  const sodApprovalBlocked =
    isPurchaseOrderStatus(order?.status, 'Pending Approval', 'Submitted') &&
    (!sodIsCurrent || sodReadiness?.canApprove !== true);
  const sodApprovalBlockedReason = sodIsCurrent
    ? sodReadiness?.checks.find((check) => check.key === 'approval')?.message ||
      'An independent actor must make the positive approval decision.'
    : 'Wait for the purchase-order role-separation check to finish.';
  const forwardActionsBlocked =
    complianceForwardBlocked || sodApprovalBlocked;
  const forwardActionsBlockedReason = complianceForwardBlocked
    ? complianceBlockedReason
    : sodApprovalBlockedReason;
  const sodReceiptAllowed =
    sodIsCurrent && sodReadiness?.canReceive === true;
  const sodReceiptBlockedReason = sodIsCurrent
    ? sodReadiness?.checks.find((check) => check.key === 'receipt')?.message ||
      'The PO creator cannot confirm its goods receipt.'
    : 'Wait for the purchase-order role-separation check to finish.';
  const actionAccess = resolvePurchaseOrderActionAccess(
    order?.status,
    hasPermission
  );

  const workflow = useWorkflowRecord({
    entityType: 'PurchaseOrder',
    entityId: id,
    entityLabel: 'Purchase Order',
    entityNumber: order?.orderNumber,
    status: order?.status || '',
    currentStepName: order?.currentWorkflowStepName,
    canSubmit: actionAccess.canSubmit,
    canApproveReject: actionAccess.canApproveReject,
    enabled: Boolean(id && order),
    commands: {
      submit: () => purchasingService.submitPurchaseOrder(id),
      approve: ({ comments }) => purchasingService.approvePurchaseOrder(id, {
        approved: true,
        comments: comments || undefined,
      }),
      reject: ({ comments }) => purchasingService.approvePurchaseOrder(id, {
        approved: false,
        comments: comments || undefined,
        rejectionReason: comments || undefined,
      }),
      afterAction: fetchOrder,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  // (see header actions below)

  const calculateReceiptProgress = (orderedQty: number, receivedQty: number) => {
    if (orderedQty === 0) return 0;
    return Math.min((receivedQty / orderedQty) * 100, 100);
  };

  const handlePrint = () => {
    if (!documentRef.current || !order) return;
    try {
      setDocumentAction('print');
      printProcurementDocument(documentRef.current, order.orderNumber);
      toast.success('Purchase order print view opened');
    } catch (printError: any) {
      toast.error(printError?.message || 'Failed to open the purchase order print view');
    } finally {
      setDocumentAction(null);
    }
  };

  const handleExportPdf = async () => {
    if (!documentRef.current || !order) return;
    try {
      setDocumentAction('pdf');
      await exportProcurementDocumentPdf(documentRef.current, order.orderNumber);
      toast.success('Purchase order PDF downloaded');
    } catch (exportError: any) {
      toast.error(exportError?.message || 'Failed to export the purchase order PDF');
    } finally {
      setDocumentAction(null);
    }
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

  const canEdit = actionAccess.canEdit;
  const canAmend = actionAccess.canAmend;
  const canReceive = isPurchaseOrderStatus(
    order.status,
    'Approved',
    'Sent',
    'Acknowledged',
    'Partially Received'
  );

  return (
    <div ref={documentRef} className="space-y-6">
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
              {isPurchaseOrderStatus(order.status, 'Pending Approval', 'Submitted') && order.currentWorkflowStepName && (
                <Badge variant="outline" className="text-xs">
                  Step: {order.currentWorkflowStepName}
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">Purchase Order Details</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2" data-document-exclude="true">
          {canEdit && (
            <Link href={`/procurement/purchase-orders/${id}/edit`}>
              <Button variant="outline">
                <Edit className="h-4 w-4 mr-2" />
                Edit
              </Button>
            </Link>
          )}
          {canAmend && (
            <Button
              variant="outline"
              title="Change this approved purchase order through a controlled amendment"
              onClick={() => {
                setActiveTab('amendments');
                setAmendmentEditorRequest((request) => request + 1);
              }}
            >
              <FilePenLine className="h-4 w-4 mr-2" />
              Amend PO
            </Button>
          )}
          
          <WorkflowApprovalActions
            {...workflow.actionProps}
            submitCopyMode="approval"
            forwardActionsDisabled={forwardActionsBlocked}
            forwardActionsDisabledReason={forwardActionsBlockedReason}
          />
          
          {canReceive && sodReceiptAllowed && (
            <Link href={`/procurement/purchase-orders/${id}/receive`}>
              <Button>
                <Package className="h-4 w-4 mr-2" />
                Receive Goods
              </Button>
            </Link>
          )}
          {canReceive && !sodReceiptAllowed && (
            <Button disabled title={sodReceiptBlockedReason}>
              <Package className="h-4 w-4 mr-2" />
              Receive Goods
            </Button>
          )}
          
          <Button variant="outline" onClick={handlePrint} disabled={documentAction !== null}>
            {documentAction === 'print' ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Printer className="h-4 w-4 mr-2" />}
            Print
          </Button>
          
          <Button variant="outline" onClick={() => void handleExportPdf()} disabled={documentAction !== null}>
            {documentAction === 'pdf' ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Download className="h-4 w-4 mr-2" />}
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
          <TabsTrigger value="amendments">Amendments</TabsTrigger>
          <WorkflowTabTrigger value="approval" />
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-6">
          <Card className={
            order.procurementSourceType === 'HistoricalMigration'
              ? 'border-amber-300 bg-amber-50/60'
              : 'border-emerald-300 bg-emerald-50/60'
          }>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CheckCircle className="h-5 w-5" />
                Approved Source Lineage
              </CardTitle>
              <CardDescription>
                Immutable requisition, sourcing case, and award-readiness trace.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 text-sm md:grid-cols-3">
              <div>
                <Label className="text-muted-foreground">Source</Label>
                <p className="font-medium">
                  {order.procurementSourceType || 'Missing'} ·{' '}
                  {order.procurementSourceReference || 'No reference'}
                </p>
              </div>
              <div>
                <Label className="text-muted-foreground">Requisition</Label>
                <p className="font-medium">
                  {order.sourceRequisitionNumber || order.sourceRequisitionId || 'Not retained'}
                </p>
              </div>
              <div>
                <Label className="text-muted-foreground">Last validated</Label>
                <p className="font-medium">
                  {order.sourceValidatedAtUtc
                    ? format(new Date(order.sourceValidatedAtUtc), 'MMM dd, yyyy HH:mm')
                    : 'Not validated'}
                </p>
              </div>
              {order.procurementSourceType === 'HistoricalMigration' && (
                <div className="md:col-span-3 rounded-md border border-amber-300 bg-amber-100 p-3 text-amber-950">
                  This retained historical PO cannot enter a new approval or issue
                  lifecycle until its governed source is remediated.
                </div>
              )}
              {order.sourcingCaseId && (
                <div className="md:col-span-3 break-all font-mono text-xs text-muted-foreground">
                  Case {order.sourcingCaseId} · Release {order.sourcingReleaseId} ·
                  Readiness {order.awardReadinessDecisionId} · Hash{' '}
                  {order.sourceIntegrityHash}
                </div>
              )}
            </CardContent>
          </Card>

          <PurchaseOrderComplianceGate
            purchaseOrderId={order.id}
            status={order.status}
            onReadinessChange={setComplianceReadiness}
          />
          <PurchaseOrderSodControl
            purchaseOrderId={order.id}
            status={order.status}
            onReadinessChange={setSodReadiness}
          />
          {order.budgetCommitment && (
            <PurchaseOrderBudgetCommitment
              commitment={order.budgetCommitment}
            />
          )}

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
                  <span className="text-muted-foreground">PO Currency:</span>
                  <span className="font-medium">{order.currency}</span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Subtotal:</span>
                  <span className="font-medium">
                    {formatProcurementMoney(order.subTotal, order.currency)}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Tax:</span>
                  <span className="font-medium">
                    {formatProcurementMoney(order.taxAmount, order.currency)}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Shipping:</span>
                  <span className="font-medium">
                    {formatProcurementMoney(order.shippingCost, order.currency)}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Miscellaneous:</span>
                  <span className="font-medium">
                    {formatProcurementMoney(order.miscellaneousCost || 0, order.currency)}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Total Additional Cost:</span>
                  <span className="font-medium">
                    {formatProcurementMoney(order.totalAdditionalCost || (order.shippingCost + (order.miscellaneousCost || 0)), order.currency)}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-muted-foreground">Discount:</span>
                  <span className="font-medium text-green-600">
                    -{formatProcurementMoney(order.discountAmount, order.currency)}
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
                    {formatProcurementMoney(order.totalAmount, order.currency)}
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
                  Total planned ({(landedCostPlan.currency || order.currency).toUpperCase()}):{' '}
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
                          <TableCell>{(i.currency || landedCostPlan.currency || order.currency).toUpperCase()}</TableCell>
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
                            {formatProcurementMoney(item.unitPrice, order.currency)}
                          </TableCell>
                          <TableCell className="text-right font-medium">
                            {formatProcurementMoney(item.lineTotal, order.currency)}
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
                  {canReceive && sodReceiptAllowed && (
                    <Link href={`/procurement/purchase-orders/${id}/receive`}>
                      <Button>
                        <Package className="h-4 w-4 mr-2" />
                        Receive Goods
                      </Button>
                    </Link>
                  )}
                  {canReceive && !sodReceiptAllowed && (
                    <Button disabled title={sodReceiptBlockedReason}>
                      <Package className="h-4 w-4 mr-2" />
                      Receive Goods
                    </Button>
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

        <TabsContent value="amendments" className="space-y-6">
          <PurchaseOrderAmendmentWorkspace
            order={order}
            onApplied={fetchOrder}
            editorRequestToken={amendmentEditorRequest}
          />
        </TabsContent>

        <WorkflowTabContent
          value="approval"
          className="space-y-6"
          {...workflow.actionProps}
        />
      </Tabs>
    </div>
  );
}
