'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
import {
  WorkflowApprovalActions,
  WorkflowTabContent,
  WorkflowTabTrigger,
  useWorkflowRecord,
} from '@/components/workflow';
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
  Edit,
  ShieldCheck,
} from 'lucide-react';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseRequisitionDetailDto,
  PurchaseRequisitionBudgetControlHistoryDto,
  PurchaseRequisitionBudgetReadinessDto,
  PurchaseRequisitionAuthorityReadinessDto,
  PurchaseRequisitionAuthorityRouteHistoryDto,
  PurchaseRequisitionLinkageHistoryDto,
  PurchaseRequisitionSubmissionControlHistoryDto,
  PurchaseRequisitionSubmissionReadinessDto,
  PurchaseRequisitionSourcingReadinessDto,
  PurchaseRequisitionSourcingReleaseDto,
} from '@/services/purchasingService';
import { getBudgetControlPresentation } from '@/lib/procurement-requisition-budget';
import { getAuthorityControlPresentation } from '@/lib/procurement-requisition-authority';
import { getSubmissionControlPresentation } from '@/lib/procurement-requisition-submission';
import { getSourcingReleasePresentation } from '@/lib/procurement-requisition-sourcing';
import { PurchaseRequisitionSourcingReleaseControl } from '@/components/procurement/PurchaseRequisitionSourcingReleaseControl';
import { PurchaseRequisitionDocuments } from '@/components/procurement/PurchaseRequisitionDocuments';
import { format } from 'date-fns';
import Link from 'next/link';

const PRStatuses = [
  {
    value: 'Draft',
    label: 'Draft',
    color: 'bg-gray-100 text-gray-800',
    icon: FileText,
  },
  {
    value: 'Submitted',
    label: 'Submitted',
    color: 'bg-blue-100 text-blue-800',
    icon: Clock,
  },
  {
    value: 'Pending Approval',
    label: 'Pending Approval',
    color: 'bg-yellow-100 text-yellow-800',
    icon: Clock,
  },
  {
    value: 'Approved',
    label: 'Approved',
    color: 'bg-green-100 text-green-800',
    icon: CheckCircle,
  },
  {
    value: 'Rejected',
    label: 'Rejected',
    color: 'bg-red-100 text-red-800',
    icon: XCircle,
  },
  {
    value: 'Ordered',
    label: 'Ordered',
    color: 'bg-purple-100 text-purple-800',
    icon: ShoppingCart,
  },
];

const PRPriorities = [
  { value: 'Low', label: 'Low', color: 'bg-gray-100 text-gray-600' },
  { value: 'Normal', label: 'Normal', color: 'bg-blue-100 text-blue-600' },
  { value: 'High', label: 'High', color: 'bg-orange-100 text-orange-600' },
  { value: 'Urgent', label: 'Urgent', color: 'bg-red-100 text-red-600' },
];

const formatMoney = (amount: number, currency?: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(amount);

export default function PurchaseRequisitionDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');

  const [requisition, setRequisition] =
    useState<PurchaseRequisitionDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [linkageHistory, setLinkageHistory] = useState<
    PurchaseRequisitionLinkageHistoryDto[]
  >([]);
  const [submissionReadiness, setSubmissionReadiness] =
    useState<PurchaseRequisitionSubmissionReadinessDto>();
  const [submissionHistory, setSubmissionHistory] = useState<
    PurchaseRequisitionSubmissionControlHistoryDto[]
  >([]);
  const [submissionReadinessLoading, setSubmissionReadinessLoading] =
    useState(true);
  const [budgetReadiness, setBudgetReadiness] =
    useState<PurchaseRequisitionBudgetReadinessDto>();
  const [budgetHistory, setBudgetHistory] = useState<
    PurchaseRequisitionBudgetControlHistoryDto[]
  >([]);
  const [budgetReadinessLoading, setBudgetReadinessLoading] = useState(true);
  const [authorityReadiness, setAuthorityReadiness] =
    useState<PurchaseRequisitionAuthorityReadinessDto>();
  const [authorityHistory, setAuthorityHistory] = useState<
    PurchaseRequisitionAuthorityRouteHistoryDto[]
  >([]);
  const [authorityReadinessLoading, setAuthorityReadinessLoading] =
    useState(true);
  const [sourcingReadiness, setSourcingReadiness] =
    useState<PurchaseRequisitionSourcingReadinessDto>();
  const [sourcingHistory, setSourcingHistory] = useState<
    PurchaseRequisitionSourcingReleaseDto[]
  >([]);
  const [sourcingReadinessLoading, setSourcingReadinessLoading] = useState(true);
  const [releasingForSourcing, setReleasingForSourcing] = useState(false);
  const [exporting, setExporting] = useState(false);

  const fetchRequisition = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await purchasingService.getPurchaseRequisitionById(id);
      setRequisition(data);
      setSubmissionReadinessLoading(true);
      try {
        setSubmissionReadiness(
          await purchasingService.getPurchaseRequisitionSubmissionReadiness(id)
        );
      } catch (readinessError) {
        console.warn(
          'Purchase requisition submission readiness is unavailable:',
          readinessError
        );
        setSubmissionReadiness(undefined);
      } finally {
        setSubmissionReadinessLoading(false);
      }
      setBudgetReadinessLoading(true);
      try {
        setBudgetReadiness(
          await purchasingService.getPurchaseRequisitionBudgetReadiness(id)
        );
      } catch (readinessError) {
        console.warn(
          'Purchase requisition budget readiness is unavailable:',
          readinessError
        );
        setBudgetReadiness(undefined);
      } finally {
        setBudgetReadinessLoading(false);
      }
      try {
        setBudgetHistory(
          await purchasingService.getPurchaseRequisitionBudgetControlHistory(id)
        );
      } catch (historyError) {
        console.warn(
          'Purchase requisition budget-control history is unavailable:',
          historyError
        );
        setBudgetHistory([]);
      }
      setAuthorityReadinessLoading(true);
      try {
        setAuthorityReadiness(
          await purchasingService.getPurchaseRequisitionAuthorityReadiness(id)
        );
      } catch (readinessError) {
        console.warn(
          'Purchase requisition authority readiness is unavailable:',
          readinessError
        );
        setAuthorityReadiness(undefined);
      } finally {
        setAuthorityReadinessLoading(false);
      }
      try {
        setAuthorityHistory(
          await purchasingService.getPurchaseRequisitionAuthorityRouteHistory(
            id
          )
        );
      } catch (historyError) {
        console.warn(
          'Purchase requisition authority-route history is unavailable:',
          historyError
        );
        setAuthorityHistory([]);
      }
      setSourcingReadinessLoading(true);
      try {
        const [readiness, history] = await Promise.all([
          purchasingService.getPurchaseRequisitionSourcingReadiness(id),
          purchasingService.getPurchaseRequisitionSourcingReleaseHistory(id),
        ]);
        setSourcingReadiness(readiness);
        setSourcingHistory(history);
      } catch (sourcingError) {
        console.warn('Purchase requisition sourcing release is unavailable:', sourcingError);
        setSourcingReadiness(undefined);
        setSourcingHistory([]);
      } finally {
        setSourcingReadinessLoading(false);
      }
      try {
        setSubmissionHistory(
          await purchasingService.getPurchaseRequisitionSubmissionControlHistory(
            id
          )
        );
      } catch (historyError) {
        console.warn(
          'Purchase requisition submission-control history is unavailable:',
          historyError
        );
        setSubmissionHistory([]);
      }
      try {
        setLinkageHistory(
          await purchasingService.getPurchaseRequisitionLinkageHistory(id)
        );
      } catch (historyError) {
        console.warn(
          'Purchase requisition linkage history is unavailable:',
          historyError
        );
        setLinkageHistory([]);
      }
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
    const statusConfig = PRStatuses.find((s) => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge className={statusConfig?.color || 'bg-gray-100'}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig?.label || status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const priorityConfig = PRPriorities.find((p) => p.value === priority);
    return (
      <Badge
        variant="outline"
        className={priorityConfig?.color || 'bg-gray-100'}
      >
        {priorityConfig?.label || priority}
      </Badge>
    );
  };

  const submissionPresentation = getSubmissionControlPresentation(
    submissionReadiness,
    submissionReadinessLoading
  );
  const budgetPresentation = getBudgetControlPresentation(
    budgetReadiness,
    budgetReadinessLoading
  );
  const authorityPresentation = getAuthorityControlPresentation(
    authorityReadiness,
    authorityReadinessLoading
  );
  const sourcingPresentation = getSourcingReleasePresentation(
    sourcingReadiness,
    sourcingReadinessLoading
  );

  const workflow = useWorkflowRecord({
    entityType: 'PurchaseRequisition',
    entityId: id,
    entityLabel: 'Purchase Requisition',
    entityNumber: requisition?.requisitionNumber,
    status: requisition?.status || '',
    currentStepName: requisition?.currentWorkflowStepName,
    canSubmit:
      requisition?.status === 'Draft' &&
      submissionReadiness?.canSubmit === true &&
      budgetReadiness?.canReserve === true,
    canApproveReject:
      requisition?.status === 'Pending Approval' ||
      requisition?.status === 'Submitted',
    enabled: Boolean(id && requisition),
    commands: {
      submit: () => purchasingService.submitPurchaseRequisition(id),
      approve: ({ comments }) =>
        purchasingService.approvePurchaseRequisition(id, {
          approved: true,
          comments: comments || undefined,
        }),
      reject: ({ comments }) =>
        purchasingService.approvePurchaseRequisition(id, {
          approved: false,
          comments: comments || undefined,
          rejectionReason: comments || undefined,
        }),
      afterAction: fetchRequisition,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

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
      const result =
        await purchasingService.createRfqFromPurchaseRequisition(id);
      toast.success(`RFQ created (${result.rfqNumber}). Redirecting...`);
      router.push(
        `/procurement/rfqs/${result.rfqId}/edit?fromRequisitionId=${id}`
      );
    } catch (error: any) {
      console.error('Error creating RFQ:', error);
      toast.error(error.message || 'Failed to create RFQ');
    }
  };

  const handleCreateTender = async () => {
    try {
      router.push(`/procurement/tenders/new?fromRequisitionId=${id}`);
    } catch (error: any) {
      console.error('Error navigating to tender creation:', error);
      toast.error(error.message || 'Failed to start Tender process');
    }
  };

  const handleReleaseForSourcing = async (reason: string) => {
    try {
      setReleasingForSourcing(true);
      const release = await purchasingService.releasePurchaseRequisitionForSourcing(id, reason);
      toast.success(`Sourcing release ${release.releaseReference} recorded`);
      const [readiness, history] = await Promise.all([
        purchasingService.getPurchaseRequisitionSourcingReadiness(id),
        purchasingService.getPurchaseRequisitionSourcingReleaseHistory(id),
      ]);
      setSourcingReadiness(readiness);
      setSourcingHistory(history);
    } catch (releaseError: any) {
      toast.error(releaseError.message || 'Failed to release requisition for sourcing');
      throw releaseError;
    } finally {
      setReleasingForSourcing(false);
    }
  };

  const handleExport = async () => {
    try {
      setExporting(true);
      const blob = await purchasingService.exportPurchaseRequisitionLinkage(id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${requisition?.requisitionNumber || 'purchase-requisition'}-linkage.json`;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      toast.success('Structured requisition-linkage export downloaded');
      setLinkageHistory(
        await purchasingService.getPurchaseRequisitionLinkageHistory(id)
      );
    } catch (exportError: any) {
      toast.error(
        exportError.message || 'Failed to export requisition linkage'
      );
    } finally {
      setExporting(false);
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
          <Button
            variant="outline"
            onClick={() => router.push('/procurement/purchase-requisitions')}
          >
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
        </div>
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <AlertCircle className="h-5 w-5 text-red-600" />
              <p className="text-red-900">
                {error || 'Purchase requisition not found'}
              </p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const canEdit = requisition.status === 'Draft';
  const canConvertToPO = requisition.status === 'Approved';
  const canCreateRfq = requisition.status === 'Approved' && sourcingPresentation.canEnterSourcing;
  const canCreateTender = requisition.status === 'Approved' && sourcingPresentation.canEnterSourcing;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="outline"
            onClick={() => router.push('/procurement/purchase-requisitions')}
          >
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold">
                {requisition.requisitionNumber}
              </h1>
              {getStatusBadge(requisition.status)}
              {getPriorityBadge(requisition.priority)}
              {(requisition.status === 'Pending Approval' ||
                requisition.status === 'Submitted') &&
                requisition.currentWorkflowStepName && (
                  <Badge variant="outline" className="text-muted-foreground">
                    Step: {requisition.currentWorkflowStepName}
                  </Badge>
                )}
            </div>
            <p className="text-muted-foreground mt-1">
              Purchase Requisition Details
            </p>
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

          <WorkflowApprovalActions {...workflow.actionProps} />

          {canCreateTender && (
            <Button variant="outline" onClick={handleCreateTender}>
              <FileText className="h-4 w-4 mr-2" />
              Create Tender
            </Button>
          )}

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
            <BreadcrumbLink href="/procurement/purchase-requisitions">
              Purchase Requisitions
            </BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>{requisition.requisitionNumber}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <Card
        className={
          submissionPresentation.tone === 'ready'
            ? 'border-emerald-200 bg-emerald-50/50'
            : submissionPresentation.tone === 'blocked'
              ? 'border-amber-200 bg-amber-50/50'
              : ''
        }
      >
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle className="flex items-center gap-2">
              {submissionPresentation.tone === 'ready' ? (
                <CheckCircle className="h-5 w-5 text-emerald-700" />
              ) : submissionPresentation.tone === 'blocked' ? (
                <AlertCircle className="h-5 w-5 text-amber-700" />
              ) : (
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              )}
              Submission control
            </CardTitle>
            <CardDescription className="mt-1">
              Required requisition details are checked before the configured
              approval workflow starts. APP exchange is shown for traceability
              and does not block submission.
            </CardDescription>
          </div>
          <Badge variant="outline">{submissionPresentation.basisLabel}</Badge>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="font-medium">{submissionPresentation.title}</p>
            <p className="mt-1 text-sm text-muted-foreground">
              {submissionReadiness?.message ||
                'The tenant-safe submission control is being evaluated.'}
            </p>
            {submissionReadiness?.decisionCode && (
              <p className="mt-2 font-mono text-xs text-muted-foreground">
                Decision: {submissionReadiness.decisionCode}
              </p>
            )}
          </div>

          {submissionReadiness?.appSubmissionNumber && (
            <div className="grid grid-cols-1 gap-3 rounded-lg border bg-background/70 p-4 text-sm md:grid-cols-3">
              <div>
                <span className="text-muted-foreground">APP attempt:</span>{' '}
                {submissionReadiness.appSubmissionNumber}/A
                {submissionReadiness.appSubmissionAttemptNumber}
              </div>
              <div>
                <span className="text-muted-foreground">Status:</span>{' '}
                {submissionReadiness.appSubmissionStatus}
              </div>
              <div>
                <span className="text-muted-foreground">Acknowledgement:</span>{' '}
                {submissionReadiness.appAcknowledgementReference ||
                  'Not recorded'}
              </div>
            </div>
          )}

          {submissionReadiness?.approvedExceptionRuleId && (
            <div className="grid grid-cols-1 gap-3 rounded-lg border bg-background/70 p-4 text-sm md:grid-cols-3">
              <div>
                <span className="text-muted-foreground">Exception rule:</span>{' '}
                {submissionReadiness.approvedExceptionRuleCode}
              </div>
              <div>
                <span className="text-muted-foreground">Approval:</span>{' '}
                {submissionReadiness.exceptionApprovalReference ||
                  'Not recorded'}
              </div>
              <div>
                <span className="text-muted-foreground">Evidence:</span>{' '}
                {submissionReadiness.exceptionEvidenceReference ||
                  'Not recorded'}
              </div>
            </div>
          )}

          {submissionReadiness &&
            submissionReadiness.requiredActions.length > 0 && (
              <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
                <p className="text-sm font-medium text-amber-950">
                  Required before submission
                </p>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-900">
                  {submissionReadiness.requiredActions.map((action) => (
                    <li key={action}>{action}</li>
                  ))}
                </ul>
              </div>
            )}

          {canEdit && (
            <div className="flex flex-wrap gap-2">
              <Link href={`/procurement/purchase-requisitions/${id}/edit`}>
                <Button variant="outline" size="sm">
                  <Edit className="mr-2 h-4 w-4" />
                  Edit governance linkage
                </Button>
              </Link>
              <Link href="/procurement/planning/app-submissions">
                <Button variant="outline" size="sm">
                  <ShieldCheck className="mr-2 h-4 w-4" />
                  Open APP register
                </Button>
              </Link>
            </div>
          )}
        </CardContent>
      </Card>

      <Card
        className={
          budgetPresentation.tone === 'ready'
            ? 'border-emerald-200 bg-emerald-50/50'
            : budgetPresentation.tone === 'blocked'
              ? 'border-amber-200 bg-amber-50/50'
              : ''
        }
      >
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle className="flex items-center gap-2">
              {budgetPresentation.tone === 'ready' ? (
                <CheckCircle className="h-5 w-5 text-emerald-700" />
              ) : budgetPresentation.tone === 'blocked' ? (
                <AlertCircle className="h-5 w-5 text-amber-700" />
              ) : (
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              )}
              Finance budget control
            </CardTitle>
            <CardDescription className="mt-1">
              Current approved budget availability is checked before submission.
              The commitment is created when an approved purchase order or
              contract is issued.
            </CardDescription>
          </div>
          <Badge variant="outline">{budgetPresentation.basisLabel}</Badge>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="font-medium">{budgetPresentation.title}</p>
            <p className="mt-1 text-sm text-muted-foreground">
              {budgetReadiness?.message ||
                'Current Finance availability and commitment state are being evaluated.'}
            </p>
            {budgetReadiness?.decisionCode && (
              <p className="mt-2 font-mono text-xs text-muted-foreground">
                Decision: {budgetReadiness.decisionCode}
              </p>
            )}
          </div>

          {budgetReadiness?.budgetId && (
            <div className="grid grid-cols-2 gap-3 rounded-lg border bg-background/70 p-4 text-sm md:grid-cols-3 xl:grid-cols-6">
              <div>
                <span className="block text-muted-foreground">Budget</span>
                <span className="font-medium">
                  {budgetReadiness.budgetCode}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Requested</span>
                <span className="font-medium">
                  {formatMoney(
                    budgetReadiness.requestedAmount,
                    budgetReadiness.currency
                  )}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Allocated</span>
                <span className="font-medium">
                  {formatMoney(
                    budgetReadiness.allocatedAmount,
                    budgetReadiness.currency
                  )}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Utilized</span>
                <span className="font-medium">
                  {formatMoney(
                    budgetReadiness.utilizedAmount,
                    budgetReadiness.currency
                  )}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Committed</span>
                <span className="font-medium">
                  {formatMoney(
                    budgetReadiness.committedAmount,
                    budgetReadiness.currency
                  )}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Available</span>
                <span className="font-medium">
                  {formatMoney(
                    budgetReadiness.availableAmount,
                    budgetReadiness.currency
                  )}
                </span>
              </div>
            </div>
          )}

          {budgetReadiness?.commitmentReference && (
            <div className="grid grid-cols-1 gap-3 rounded-lg border border-emerald-200 bg-emerald-50/70 p-4 text-sm md:grid-cols-3">
              <div>
                <span className="text-muted-foreground">Commitment:</span>{' '}
                {budgetReadiness.commitmentReference}
              </div>
              <div>
                <span className="text-muted-foreground">Status:</span>{' '}
                {budgetReadiness.commitmentStatus}
              </div>
              <div>
                <span className="text-muted-foreground">
                  Reservation sequence:
                </span>{' '}
                {budgetReadiness.reservationSequence}
              </div>
            </div>
          )}

          {budgetReadiness?.isOverride && (
            <div className="grid grid-cols-1 gap-3 rounded-lg border bg-background/70 p-4 text-sm md:grid-cols-3">
              <div>
                <span className="text-muted-foreground">Override rule:</span>{' '}
                {budgetReadiness.overrideRuleCode}
              </div>
              <div>
                <span className="text-muted-foreground">Approval:</span>{' '}
                {budgetReadiness.overrideApprovalReference}
              </div>
              <div>
                <span className="text-muted-foreground">Evidence:</span>{' '}
                {budgetReadiness.overrideEvidenceReference}
              </div>
            </div>
          )}

          {budgetReadiness && budgetReadiness.requiredActions.length > 0 && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
              <p className="text-sm font-medium text-amber-950">
                Finance action required
              </p>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-900">
                {budgetReadiness.requiredActions.map((action) => (
                  <li key={action}>{action}</li>
                ))}
              </ul>
            </div>
          )}
        </CardContent>
      </Card>

      <Card
        className={
          authorityPresentation.tone === 'ready'
            ? 'border-emerald-200 bg-emerald-50/50'
            : authorityPresentation.tone === 'blocked'
              ? 'border-amber-200 bg-amber-50/50'
              : ''
        }
      >
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle className="flex items-center gap-2">
              {authorityPresentation.tone === 'ready' ? (
                <ShieldCheck className="h-5 w-5 text-emerald-700" />
              ) : authorityPresentation.tone === 'blocked' ? (
                <AlertCircle className="h-5 w-5 text-amber-700" />
              ) : (
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              )}
              Approval authority route
            </CardTitle>
            <CardDescription className="mt-1">
              Optional policy-routing guidance is shown for administrators. PR
              submission uses the configured Purchase Requisition workflow.
            </CardDescription>
          </div>
          <Badge variant="outline">{authorityPresentation.basisLabel}</Badge>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="font-medium">{authorityPresentation.title}</p>
            <p className="mt-1 text-sm text-muted-foreground">
              {authorityReadiness?.message ||
                'The effective approval authority matrix is being evaluated.'}
            </p>
            {authorityReadiness?.decisionCode && (
              <p className="mt-2 font-mono text-xs text-muted-foreground">
                Decision: {authorityReadiness.decisionCode}
              </p>
            )}
          </div>

          {(authorityReadiness?.policySetId ||
            authorityReadiness?.workflowDefinitionId) && (
            <div className="grid grid-cols-1 gap-3 rounded-lg border bg-background/70 p-4 text-sm md:grid-cols-2 xl:grid-cols-4">
              <div>
                <span className="block text-muted-foreground">Policy</span>
                <span className="font-medium">
                  {authorityReadiness.policyCode}/v
                  {authorityReadiness.policyVersion}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Workflow</span>
                <span className="font-medium">
                  {authorityReadiness.workflowName} v
                  {authorityReadiness.workflowVersion}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">Context</span>
                <span className="font-medium">
                  {authorityReadiness.category} ·{' '}
                  {formatMoney(
                    authorityReadiness.amount,
                    authorityReadiness.currencyCode
                  )}
                </span>
              </div>
              <div>
                <span className="block text-muted-foreground">
                  Current stage
                </span>
                <span className="font-medium">
                  {authorityReadiness.currentWorkflowStage || 'Not started'}
                  {authorityReadiness.currentWorkflowStageStatus
                    ? ` · ${authorityReadiness.currentWorkflowStageStatus}`
                    : ''}
                </span>
              </div>
            </div>
          )}

          {authorityReadiness?.routeReference && (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50/70 p-4 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  <span className="text-muted-foreground">
                    Immutable route:
                  </span>{' '}
                  <span className="font-medium">
                    {authorityReadiness.routeReference}
                  </span>
                </span>
                <Badge variant="outline">
                  Attempt {authorityReadiness.attemptNumber}
                </Badge>
              </div>
              {authorityReadiness.integrityHash && (
                <p className="mt-2 break-all font-mono text-[11px] text-muted-foreground">
                  Integrity: {authorityReadiness.integrityHash}
                </p>
              )}
            </div>
          )}

          {authorityReadiness && authorityReadiness.steps.length > 0 && (
            <div className="space-y-3">
              {authorityReadiness.steps.map((step) => (
                <div
                  key={`${step.sequence}-${step.ruleId}`}
                  className="grid grid-cols-1 gap-3 rounded-lg border bg-background/70 p-4 text-sm md:grid-cols-[auto_1fr_auto] md:items-center"
                >
                  <Badge className="w-fit">{step.sequence}</Badge>
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{step.authorityName}</span>
                      <Badge variant="outline">{step.authorityRole}</Badge>
                      {step.isObserver ? (
                        <Badge variant="secondary">Observer</Badge>
                      ) : (
                        <Badge variant="secondary">Quorum {step.quorum}</Badge>
                      )}
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {step.workflowStepName} · {step.ruleCode} ·{' '}
                      {step.sourceDecisionKey} · {step.rulePolicyCode}/v
                      {step.rulePolicyVersion}
                    </p>
                    {step.escalationAuthority && (
                      <p className="mt-1 text-xs text-muted-foreground">
                        Escalates to {step.escalationAuthority}
                      </p>
                    )}
                  </div>
                  <div className="text-xs text-muted-foreground md:text-right">
                    {step.lowerInclusive ? '[' : '('}
                    {formatMoney(step.lowerBound, step.currencyCode)} —{' '}
                    {step.upperBound == null
                      ? 'No upper limit'
                      : formatMoney(step.upperBound, step.currencyCode)}
                    {step.upperInclusive ? ']' : ')'}
                  </div>
                </div>
              ))}
            </div>
          )}

          {authorityReadiness &&
            authorityReadiness.requiredActions.length > 0 && (
              <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
                <p className="text-sm font-medium text-amber-950">
                  Configuration action required
                </p>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-900">
                  {authorityReadiness.requiredActions.map((action) => (
                    <li key={action}>{action}</li>
                  ))}
                </ul>
              </div>
            )}

          {authorityPresentation.tone === 'blocked' && (
            <div className="flex flex-wrap gap-2">
              <Link href="/administration/procurement/policy-sets">
                <Button variant="outline" size="sm">
                  <ShieldCheck className="mr-2 h-4 w-4" />
                  Open executable policies
                </Button>
              </Link>
              <Link href="/administration/workflow">
                <Button variant="outline" size="sm">
                  <Clock className="mr-2 h-4 w-4" />
                  Open shared workflows
                </Button>
              </Link>
            </div>
          )}
        </CardContent>
      </Card>

      <PurchaseRequisitionSourcingReleaseControl
        readiness={sourcingReadiness}
        history={sourcingHistory}
        loading={sourcingReadinessLoading}
        releasing={releasingForSourcing}
        onRelease={handleReleaseForSourcing}
      />

      {/* Rejection Notice */}
      {requisition.status === 'Rejected' && requisition.rejectionReason && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <XCircle className="h-5 w-5 text-red-600 mt-0.5" />
              <div>
                <p className="font-medium text-red-900">Requisition Rejected</p>
                <p className="text-sm text-red-700 mt-1">
                  {requisition.rejectionReason}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Main Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="items">
            Items ({requisition.itemCount})
          </TabsTrigger>
          <TabsTrigger value="documents">Documents</TabsTrigger>
          <TabsTrigger value="linkage">Planning &amp; Governance</TabsTrigger>
          <WorkflowTabTrigger value="approval" />
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
                  <Label className="text-muted-foreground">
                    Requisition Number
                  </Label>
                  <p className="font-medium mt-1">
                    {requisition.requisitionNumber}
                  </p>
                </div>

                <div>
                  <Label className="text-muted-foreground flex items-center gap-2">
                    <Calendar className="h-4 w-4" />
                    Requisition Date
                  </Label>
                  <p className="font-medium mt-1">
                    {format(
                      new Date(requisition.requisitionDate),
                      'MMM dd, yyyy'
                    )}
                  </p>
                </div>

                <div>
                  <Label className="text-muted-foreground flex items-center gap-2">
                    <User className="h-4 w-4" />
                    Requested By
                  </Label>
                  <p className="font-medium mt-1">
                    {requisition.requestedByName}
                  </p>
                </div>

                {requisition.requiredDate && (
                  <div>
                    <Label className="text-muted-foreground">
                      Required Date
                    </Label>
                    <p className="font-medium mt-1">
                      {format(
                        new Date(requisition.requiredDate),
                        'MMM dd, yyyy'
                      )}
                    </p>
                  </div>
                )}

                <div>
                  <Label className="text-muted-foreground">Priority</Label>
                  <div className="mt-1">
                    {getPriorityBadge(requisition.priority)}
                  </div>
                </div>

                <div>
                  <Label className="text-muted-foreground">Status</Label>
                  <div className="mt-1">
                    {getStatusBadge(requisition.status)}
                  </div>
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
                    <Label className="text-muted-foreground">
                      Justification
                    </Label>
                    <p className="mt-2 text-sm whitespace-pre-wrap">
                      {requisition.justification}
                    </p>
                  </div>
                </>
              )}

              {requisition.notes && (
                <>
                  <Separator className="my-6" />
                  <div>
                    <Label className="text-muted-foreground">Notes</Label>
                    <p className="mt-2 text-sm whitespace-pre-wrap">
                      {requisition.notes}
                    </p>
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
                    $
                    {requisition.totalAmount.toLocaleString('en-US', {
                      minimumFractionDigits: 2,
                      maximumFractionDigits: 2,
                    })}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Approval Information */}
          {(requisition.approvedByName ||
            requisition.status === 'Rejected') && (
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
                        {requisition.status === 'Approved'
                          ? 'Approved By'
                          : 'Reviewed By'}
                      </Label>
                      <p className="font-medium mt-1">
                        {requisition.approvedByName}
                      </p>
                    </div>
                  )}

                  {requisition.approvedAt && (
                    <div>
                      <Label className="text-muted-foreground">
                        {requisition.status === 'Approved'
                          ? 'Approved At'
                          : 'Reviewed At'}
                      </Label>
                      <p className="font-medium mt-1">
                        {format(
                          new Date(requisition.approvedAt),
                          'MMM dd, yyyy HH:mm'
                        )}
                      </p>
                    </div>
                  )}

                  {requisition.rejectionReason && (
                    <div className="md:col-span-2">
                      <Label className="text-muted-foreground">
                        Rejection Reason
                      </Label>
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
                      <TableHead className="text-right">
                        Est. Unit Price
                      </TableHead>
                      <TableHead className="text-right">Line Total</TableHead>
                      <TableHead>Preferred Supplier</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {requisition.items.map((item, index) => (
                      <TableRow key={item.id}>
                        <TableCell className="font-medium">
                          {index + 1}
                        </TableCell>
                        <TableCell>
                          <div className="font-medium">
                            {item.itemCode || 'N/A'}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            {item.itemName || 'Custom'}
                          </div>
                        </TableCell>
                        <TableCell className="max-w-[300px]">
                          <div className="font-medium">
                            {item.itemDescription}
                          </div>
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
                              Required:{' '}
                              {format(
                                new Date(item.requiredDate),
                                'MMM dd, yyyy'
                              )}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          {item.quantity}
                        </TableCell>
                        <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                        <TableCell className="text-right">
                          $
                          {item.estimatedUnitPrice.toLocaleString('en-US', {
                            minimumFractionDigits: 2,
                            maximumFractionDigits: 2,
                          })}
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          $
                          {item.lineTotal.toLocaleString('en-US', {
                            minimumFractionDigits: 2,
                            maximumFractionDigits: 2,
                          })}
                        </TableCell>
                        <TableCell>
                          <div className="text-sm">
                            {item.preferredSupplierName || '-'}
                          </div>
                        </TableCell>
                        <TableCell>
                          {item.purchaseOrderNumber ? (
                            <Link
                              href={`/procurement/purchase-orders/${item.purchaseOrderId}`}
                            >
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
                        <span className="text-muted-foreground">
                          Total Items:
                        </span>
                        <span className="font-medium">
                          {requisition.itemCount}
                        </span>
                      </div>
                      <Separator />
                      <div className="flex justify-between">
                        <span className="font-semibold">Total Amount:</span>
                        <span className="text-xl font-bold text-primary">
                          $
                          {requisition.totalAmount.toLocaleString('en-US', {
                            minimumFractionDigits: 2,
                            maximumFractionDigits: 2,
                          })}
                        </span>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="documents" className="space-y-6">
          <PurchaseRequisitionDocuments
            requisitionId={id}
            requisitionStatus={requisition.status}
            editable={canEdit}
          />
        </TabsContent>

        <TabsContent value="linkage" className="space-y-6">
          <Card>
            <CardHeader className="flex flex-row items-start justify-between gap-4">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <ShieldCheck className="h-5 w-5" /> Planning and Governance
                  Linkage
                </CardTitle>
                <CardDescription>
                  Drafts may remain incomplete while being prepared. Submission
                  requires an acknowledged APP plan-item linkage or a traceable
                  approved exception.
                </CardDescription>
              </div>
              <Button
                variant="outline"
                onClick={handleExport}
                disabled={exporting}
              >
                {exporting ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Download className="mr-2 h-4 w-4" />
                )}
                Export JSON
              </Button>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 gap-x-8 gap-y-5 md:grid-cols-2 xl:grid-cols-3">
                <div>
                  <Label className="text-muted-foreground">Plan item</Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.sourcePlanNumber
                      ? `${requisition.linkage.sourcePlanNumber} — ${requisition.linkage.sourcePlanItemDescription}`
                      : 'Not linked'}
                  </p>
                </div>
                <div>
                  <Label className="text-muted-foreground">Budget</Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.budgetCode || 'Not linked'}
                  </p>
                </div>
                <div>
                  <Label className="text-muted-foreground">Category</Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.procurementCategory || 'Not selected'}
                  </p>
                </div>
                <div>
                  <Label className="text-muted-foreground">Cost centre</Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.costCenter || 'Not linked'}
                  </p>
                </div>
                <div>
                  <Label className="text-muted-foreground">Project</Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.projectCode
                      ? `${requisition.linkage.projectCode} — ${requisition.linkage.projectName}`
                      : 'Not linked'}
                  </p>
                </div>
                <div>
                  <Label className="text-muted-foreground">Request type</Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.requisitionType}
                  </p>
                </div>
                <div className="xl:col-span-2">
                  <Label className="text-muted-foreground">
                    Specification / TOR template
                  </Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.specificationTemplateCode
                      ? `${requisition.linkage.specificationTemplateCode}/v${requisition.linkage.specificationTemplateVersion} — ${requisition.linkage.specificationTemplateName}`
                      : 'Not linked'}
                  </p>
                </div>
                <div>
                  <Label className="text-muted-foreground">
                    Linkage revision
                  </Label>
                  <p className="mt-1 font-medium">
                    {requisition.linkage.revision}
                  </p>
                </div>
              </div>

              {requisition.linkage.approvedExceptionRuleId && (
                <div className="mt-6 rounded-lg border border-amber-200 bg-amber-50/50 p-4">
                  <p className="font-medium text-amber-950">
                    Approved exception
                  </p>
                  <div className="mt-3 grid grid-cols-1 gap-4 text-sm md:grid-cols-2">
                    <div>
                      <span className="text-muted-foreground">Rule:</span>{' '}
                      {requisition.linkage.approvedExceptionRuleCode} —{' '}
                      {requisition.linkage.approvedExceptionName}
                    </div>
                    <div>
                      <span className="text-muted-foreground">
                        Approval reference:
                      </span>{' '}
                      {requisition.linkage.exceptionApprovalReference}
                    </div>
                    <div>
                      <span className="text-muted-foreground">
                        Approved by:
                      </span>{' '}
                      {requisition.linkage.exceptionApprovedByName ||
                        'Shared workflow record'}
                    </div>
                    <div>
                      <span className="text-muted-foreground">
                        Approved at:
                      </span>{' '}
                      {requisition.linkage.exceptionApprovedAtUtc
                        ? format(
                            new Date(
                              requisition.linkage.exceptionApprovedAtUtc
                            ),
                            'MMM dd, yyyy HH:mm'
                          )
                        : '-'}
                    </div>
                    {requisition.linkage.exceptionEvidenceReference && (
                      <div className="md:col-span-2">
                        <span className="text-muted-foreground">Evidence:</span>{' '}
                        {requisition.linkage.exceptionEvidenceReference}
                      </div>
                    )}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Immutable linkage history</CardTitle>
              <CardDescription>
                Created, updated, and export events from the shared procurement
                control-event ledger.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {linkageHistory.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No linkage history is available.
                </p>
              ) : (
                <div className="space-y-3">
                  {linkageHistory.map((event) => (
                    <div key={event.id} className="rounded-lg border p-4">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <Badge variant="outline">{event.action}</Badge>
                          <span className="font-medium">{event.actorName}</span>
                        </div>
                        <span className="text-xs text-muted-foreground">
                          {format(
                            new Date(event.occurredAtUtc),
                            'MMM dd, yyyy HH:mm'
                          )}
                        </span>
                      </div>
                      {event.reason && (
                        <p className="mt-2 text-sm text-muted-foreground">
                          {event.reason}
                        </p>
                      )}
                      <p className="mt-2 break-all font-mono text-[11px] text-muted-foreground">
                        Integrity: {event.integrityHash}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Immutable submission-control history</CardTitle>
              <CardDescription>
                Every allowed or blocked submission attempt is retained in the
                shared procurement control-event ledger.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {submissionHistory.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No submission attempts have been evaluated.
                </p>
              ) : (
                <div className="space-y-3">
                  {submissionHistory.map((event) => (
                    <div key={event.id} className="rounded-lg border p-4">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <Badge
                            variant={
                              event.result === 'Allowed'
                                ? 'default'
                                : 'destructive'
                            }
                          >
                            {event.result}
                          </Badge>
                          <span className="font-medium">{event.action}</span>
                          <Badge variant="outline">{event.ruleCode}</Badge>
                        </div>
                        <span className="text-xs text-muted-foreground">
                          {format(
                            new Date(event.occurredAtUtc),
                            'MMM dd, yyyy HH:mm'
                          )}
                        </span>
                      </div>
                      <p className="mt-2 text-sm text-muted-foreground">
                        {event.reason || 'Submission control evaluated.'}
                      </p>
                      <p className="mt-2 text-xs text-muted-foreground">
                        Actor: {event.actorName}
                      </p>
                      <p className="mt-2 break-all font-mono text-[11px] text-muted-foreground">
                        Integrity: {event.integrityHash}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Immutable budget-control history</CardTitle>
              <CardDescription>
                Blocked checks, idempotent retries, reservations, overrides, and
                releases remain in the shared control-event ledger.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {budgetHistory.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No budget-control decisions have been recorded.
                </p>
              ) : (
                <div className="space-y-3">
                  {budgetHistory.map((event) => (
                    <div key={event.id} className="rounded-lg border p-4">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <Badge
                            variant={
                              event.result === 'Denied'
                                ? 'destructive'
                                : 'default'
                            }
                          >
                            {event.result}
                          </Badge>
                          <span className="font-medium">{event.action}</span>
                          {event.ruleCode && (
                            <Badge variant="outline">{event.ruleCode}</Badge>
                          )}
                        </div>
                        <span className="text-xs text-muted-foreground">
                          {format(
                            new Date(event.occurredAtUtc),
                            'MMM dd, yyyy HH:mm'
                          )}
                        </span>
                      </div>
                      <p className="mt-2 text-sm text-muted-foreground">
                        {event.reason || 'Budget control evaluated.'}
                      </p>
                      <p className="mt-2 text-xs text-muted-foreground">
                        Actor: {event.actorName}
                      </p>
                      <p className="mt-2 break-all font-mono text-[11px] text-muted-foreground">
                        Integrity: {event.integrityHash}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Immutable authority-route history</CardTitle>
              <CardDescription>
                Every submitted attempt retains its exact policy, authority
                rules, workflow version, route stages, actor, correlation, and
                integrity hash.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {authorityHistory.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No authority route has been captured.
                </p>
              ) : (
                <div className="space-y-4">
                  {authorityHistory.map((route) => (
                    <div key={route.id} className="rounded-lg border p-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge>{route.routeReference}</Badge>
                            <Badge variant="outline">
                              Attempt {route.attemptNumber}
                            </Badge>
                            <span className="font-medium">
                              {route.policyCode}/v{route.policyVersion}
                            </span>
                          </div>
                          <p className="mt-2 text-sm text-muted-foreground">
                            {route.workflowName} v{route.workflowVersion} ·{' '}
                            {route.category} ·{' '}
                            {formatMoney(route.amount, route.currencyCode)}
                          </p>
                        </div>
                        <div className="text-right text-xs text-muted-foreground">
                          <p>
                            {format(
                              new Date(route.capturedAtUtc),
                              'MMM dd, yyyy HH:mm'
                            )}
                          </p>
                          <p>{route.capturedByName}</p>
                        </div>
                      </div>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {route.steps.map((step) => (
                          <Badge
                            key={`${route.id}-${step.sequence}`}
                            variant="secondary"
                          >
                            {step.sequence}. {step.authorityName}
                            {step.isObserver
                              ? ' · observer'
                              : ` · quorum ${step.quorum}`}
                          </Badge>
                        ))}
                      </div>
                      <p className="mt-3 break-all font-mono text-[11px] text-muted-foreground">
                        Integrity: {route.integrityHash}
                      </p>
                      <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                        Correlation: {route.correlationId}
                      </p>
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
          {...workflow.actionProps}
        />
      </Tabs>
    </div>
  );
}
