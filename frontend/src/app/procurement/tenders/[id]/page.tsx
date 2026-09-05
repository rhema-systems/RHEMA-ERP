'use client';

import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
  Edit,
  FileText,
  Package,
  Upload,
  DollarSign,
  Users,
  Award,
  MessageSquare,
  Clock,
  CheckCircle2,
  XCircle,
  Send,
  Download,
  ClipboardList,
  MailCheck,
} from 'lucide-react';
import { toast } from 'sonner';
import * as tenderService from '@/services/tenderService';
import { type TenderDetailDto } from '@/services/tenderService';
import { format } from 'date-fns';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  WorkflowApprovalActions,
  useWorkflowRecord,
} from '@/components/workflow';
import {
  WorkflowTabContent,
  WorkflowTabTrigger,
} from '@/components/workflow/WorkflowRecordTab';
import { TenderAward } from '@/components/procurement/tenders/TenderAward';
import { AnswerClarificationDialog } from '@/components/procurement/tenders/AnswerClarificationDialog';
import TenderEvaluators from '@/components/procurement/tenders/TenderEvaluators';
import QCBSEvaluationPanel from '@/components/procurement/tenders/QCBSEvaluationPanel';
import { AwardVerificationResults } from '@/components/procurement/tenders/AwardVerificationResults';
import { Calculator, Shield } from 'lucide-react';
import { getTenderPublicationNotificationMessage, getTenderPublicationPresentation } from '@/lib/procurement-tender-publication';
import { getTenderEvaluationRoute } from '@/lib/procurement-tender-evaluation-route';
import { getTenderScheduleError } from '@/lib/tender-schedule';
import { TenderHeaderControlActions } from '@/components/procurement/tenders/TenderHeaderControlActions';
import { TenderRevisionsPanel } from '@/components/procurement/tenders/TenderRevisionsPanel';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

const tenderDetailTabs = [
  'overview',
  'items',
  'proposals',
  'documents',
  'fees',
  'invitations',
  'clarifications',
  'revisions',
  'evaluators',
  'qcbs',
  'bids',
  'award',
  'verification',
  'approvals',
];

export default function TenderDetailPage() {
  const queryClient = useQueryClient();
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { hasPermission } = useAuth();
  const canAdministerTender = hasPermission('procurement.tender.administer');
  const tenderId = Array.isArray(params?.id)
    ? params.id[0]
    : (params?.id ?? '');

  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState(() => {
    const tab = searchParams.get('tab') ?? 'overview';
    return tenderDetailTabs.includes(tab) ? tab : 'overview';
  });
  const [showPublishDialog, setShowPublishDialog] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [publishData, setPublishData] = useState({
    submissionDeadline: '',
    openingDate: '',
    advertisementReference: '',
    publicationChannel: '',
    tenderDocumentReference: '',
    tenderDocumentVersion: '',
    documentFee: 0,
    advertisementEvidenceReference: '',
  });
  const [externalEmailInput, setExternalEmailInput] = useState('');
  const [externalRecipientEmails, setExternalRecipientEmails] = useState<
    string[]
  >([]);
  const [showAnswerDialog, setShowAnswerDialog] = useState(false);
  const [selectedClarification, setSelectedClarification] = useState<any>(null);

  const loadTenderDetails = async () => {
    try {
      setLoading(true);
      const data = await tenderService.getTenderById(tenderId);
      console.log('Internal Portal - Tender Data:', data);
      console.log(
        'Internal Portal - Required Documents:',
        data.requiredDocuments
      );
      setTender(data);
      queryClient.setQueryData(
        tenderService.tenderDetailQueryKey(tenderId),
        data
      );
    } catch (error) {
      console.error('Error loading tender details:', error);
      toast.error(
        getProcurementProblemMessage(error, 'Failed to load tender details')
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (tenderId) {
      loadTenderDetails();
    }
  }, [tenderId]);

  const requestedTab = searchParams.get('tab');
  useEffect(() => {
    setActiveTab(
      requestedTab && tenderDetailTabs.includes(requestedTab)
        ? requestedTab
        : 'overview'
    );
  }, [requestedTab]);

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<
      string,
      {
        variant: 'default' | 'secondary' | 'destructive' | 'outline';
        className: string;
      }
    > = {
      Draft: { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      Submitted: {
        variant: 'outline',
        className: 'bg-yellow-100 text-yellow-800',
      },
      Approved: {
        variant: 'default',
        className: 'bg-green-100 text-green-800',
      },
      Published: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      Closed: {
        variant: 'outline',
        className: 'bg-yellow-100 text-yellow-800',
      },
      Awarded: { variant: 'default', className: 'bg-green-100 text-green-800' },
      Cancelled: {
        variant: 'destructive',
        className: 'bg-red-100 text-red-800',
      },
      Rejected: {
        variant: 'destructive',
        className: 'bg-red-100 text-red-800',
      },
    };

    const config = statusConfig[status] || {
      variant: 'outline' as const,
      className: '',
    };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP p');
    } catch {
      return dateString;
    }
  };

  const formatCurrency = (amount?: number, currency?: string) => {
    if (amount === undefined || amount === null) return 'N/A';
    return `${currency || 'USD'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const getTenderTypeLabel = (type: string) => {
    const typeMap: Record<string, string> = {
      RFQ: 'Request for Quotation',
      RFP: 'Request for Proposal',
      ITB: 'Invitation to Bid',
      EOI: 'Expression of Interest',
    };
    return typeMap[type] || type;
  };

  const handlePublishClick = () => {
    // Pre-fill submission deadline if available
    const submissionDeadline = tender?.submissionDeadline;
    const openingDate = tender?.openingDate;
    if (submissionDeadline) {
      setPublishData((prev) => ({
        ...prev,
        submissionDeadline: new Date(submissionDeadline)
          .toISOString()
          .slice(0, 16),
        openingDate: openingDate
          ? new Date(openingDate).toISOString().slice(0, 16)
          : '',
      }));
    }
    setPublishError(null);
    setShowPublishDialog(true);
  };

  const addExternalRecipientEmail = () => {
    const email = externalEmailInput.trim();
    if (!email) return;
    if (!email.includes('@')) {
      toast.error('Please enter a valid email address');
      return;
    }
    setExternalRecipientEmails((prev) =>
      prev.some((e) => e.toLowerCase() === email.toLowerCase())
        ? prev
        : [...prev, email]
    );
    setExternalEmailInput('');
  };

  const removeExternalRecipientEmail = (email: string) => {
    setExternalRecipientEmails((prev) =>
      prev.filter((e) => e.toLowerCase() !== email.toLowerCase())
    );
  };

  const handlePublishConfirm = async () => {
    setPublishError(null);
    try {
      // Validate dates
      if (!publishData.submissionDeadline) {
        setPublishError('Submission deadline is required');
        toast.error('Submission deadline is required');
        return false;
      }

      const now = new Date();
      const submissionDeadline = new Date(publishData.submissionDeadline);

      if (submissionDeadline <= now) {
        setPublishError('Submission deadline must be in the future');
        toast.error('Submission deadline must be in the future');
        return false;
      }

      const scheduleError = getTenderScheduleError(
        publishData.submissionDeadline,
        publishData.openingDate
      );
      if (scheduleError) {
        setPublishError(scheduleError);
        toast.error(scheduleError);
        return false;
      }

      setPublishing(true);

      // Get invited business partner IDs from invitations
      const invitedBusinessPartnerIds =
        tender?.invitations?.map((inv) => inv.businessPartnerId) || [];

      await tenderService.publishTender(tenderId, {
        submissionDeadline: publishData.submissionDeadline,
        openingDate: publishData.openingDate || undefined,
        invitedBusinessPartnerIds,
        externalRecipientEmails,
        sendNotifications: true,
        advertisementReference: publishData.advertisementReference || undefined,
        publicationChannel: publishData.publicationChannel || undefined,
        tenderDocumentReference:
          publishData.tenderDocumentReference || undefined,
        tenderDocumentVersion: publishData.tenderDocumentVersion || undefined,
        documentFee: publishData.documentFee,
        advertisementEvidenceReference:
          publishData.advertisementEvidenceReference || undefined,
      });

      toast.success(
        'Tender published successfully! Notifications sent to invited suppliers.'
      );
      setShowPublishDialog(false);
      setExternalRecipientEmails([]);
      setExternalEmailInput('');

      // Reload tender details
      await loadTenderDetails();
    } catch (error: any) {
      console.error('Error publishing tender:', error);
      const message = getProcurementProblemMessage(
        error,
        'Failed to publish tender'
      );
      setPublishError(message);
      toast.error(message);
      return false;
    } finally {
      setPublishing(false);
    }
  };

  const workflow = useWorkflowRecord({
    entityType: 'Tender',
    entityId: tenderId,
    entityLabel: 'Tender',
    entityNumber: tender?.tenderNumber,
    status: tender?.status ?? '',
    currentStepName: tender?.currentWorkflowStepName,
    canSubmit: tender?.status === 'Draft',
    canApproveReject: tender?.status === 'Submitted',
    enabled: Boolean(tender),
    commands: {
      submit: async () => {
        await tenderService.submitTenderForApproval(tenderId);
      },
      approve: async ({ comments }) => {
        await tenderService.approveTender(tenderId, comments || undefined);
      },
      reject: async ({ comments }) => {
        await tenderService.rejectTender(tenderId, comments);
      },
      afterAction: loadTenderDetails,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading tender details...</p>
        </div>
      </div>
    );
  }

  if (!tender) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Tender not found</p>
          <Button
            onClick={() => router.push('/procurement/tenders')}
            className="mt-4"
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Tenders
          </Button>
        </div>
      </div>
    );
  }

  const publicationPresentation = getTenderPublicationPresentation(tender);
  const evaluationRoute = getTenderEvaluationRoute(tender);
  const isPublishedStage = ['Published', 'Closed', 'Awarded'].includes(
    tender.status
  );
  const hasSubmittedBids = tender.bidCount > 0;
  const showEvaluationStage = isPublishedStage && hasSubmittedBids;
  const showAwardStage = ['Closed', 'Awarded'].includes(tender.status);
  const visibleActiveTab =
    (!showAwardStage && ['award', 'verification'].includes(activeTab)) ||
    (!showEvaluationStage && ['evaluators', 'qcbs'].includes(activeTab)) ||
    (!isPublishedStage && activeTab === 'bids')
      ? 'overview'
      : activeTab;

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            onClick={() => router.push('/procurement/tenders')}
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{tender.title}</h1>
            <p className="text-gray-500">Tender #{tender.tenderNumber}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(tender.status)}
          {tender.status === 'Submitted' && tender.currentWorkflowStepName && (
            <Badge variant="outline" className="text-xs">
              Step: {tender.currentWorkflowStepName}
            </Badge>
          )}

          {tender.status === 'Draft' && canAdministerTender && (
            <Button
              variant="outline"
              onClick={() =>
                router.push(`/procurement/tenders/${tenderId}/edit`)
              }
            >
              <Edit className="h-4 w-4 mr-2" />
              Edit
            </Button>
          )}

          {((tender.status === 'Draft' && canAdministerTender) ||
            tender.status === 'Submitted') && (
            <WorkflowApprovalActions {...workflow.actionProps} showStepBadge />
          )}

          {tender.status === 'Approved' && canAdministerTender && (
            <Button onClick={handlePublishClick}>
              <Send className="h-4 w-4 mr-2" />
              Publish Tender
            </Button>
          )}
          {/* Document-register navigation lives in the process guide, not twice in the page chrome. */}
          <TenderHeaderControlActions
            tenderId={tenderId}
            tenderType={tender.tenderType}
            sourcingCaseId={tender.sourcingCaseId}
            sourcingMethod={tender.sourcingMethod}
            status={tender.status}
            bidCount={tender.bidCount}
          />
          {tender.tenderType !== 'RFQ' && tender.status === 'Awarded' && (
            <Button
              variant="outline"
              onClick={() =>
                router.push(
                  `/procurement/tenders/${tenderId}/bidder-communications`
                )
              }
            >
              <MailCheck className="h-4 w-4 mr-2" />
              Bidder Communications
            </Button>
          )}
          {canAdministerTender &&
            showEvaluationStage &&
            publicationPresentation.advancedControlLabel && (
              <Button
                variant="outline"
                onClick={() =>
                  router.push(`/procurement/tenders/${tenderId}/controls`)
                }
              >
                <Shield className="h-4 w-4 mr-2" />
                {publicationPresentation.advancedControlLabel}
              </Button>
            )}
          {['Approved', 'Published', 'Awarded'].includes(tender.status) &&
            publicationPresentation.exceptionalControlLabel && (
              <Button
                variant="outline"
                onClick={() =>
                  router.push(
                    `/procurement/tenders/${tenderId}/exception-controls`
                  )
                }
              >
                <Shield className="h-4 w-4 mr-2" />
                {publicationPresentation.exceptionalControlLabel}
              </Button>
            )}
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Tender Type
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{tender.tenderType}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Estimated Value
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">
              {formatCurrency(tender.estimatedValue, tender.currency)}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Bids Received
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{tender.bids?.length || 0}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Submission Deadline
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-lg font-semibold">
              {tender.submissionDeadline
                ? format(new Date(tender.submissionDeadline), 'PPP')
                : 'N/A'}
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs
        value={visibleActiveTab}
        onValueChange={setActiveTab}
        className="space-y-4"
      >
        <TabsList className="flex h-auto w-full flex-wrap justify-start">
          <TabsTrigger value="overview">
            <FileText className="h-4 w-4 mr-2" />
            Overview
          </TabsTrigger>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Lots ({tender.lots?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="proposals">
            <ClipboardList className="h-4 w-4 mr-2" />
            Proposals
          </TabsTrigger>
          <TabsTrigger value="documents">
            <Upload className="h-4 w-4 mr-2" />
            Documents ({tender.documents?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="fees">
            <DollarSign className="h-4 w-4 mr-2" />
            Fees ({tender.fees?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="invitations">
            <Users className="h-4 w-4 mr-2" />
            Invitations ({tender.invitations?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="clarifications">
            <MessageSquare className="h-4 w-4 mr-2" />
            Clarifications ({tender.clarifications?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="revisions">
            <FileText className="h-4 w-4 mr-2" />
            Amendments ({tender.revisions?.length || 0})
          </TabsTrigger>
          {showEvaluationStage && (
            <TabsTrigger value="evaluators">
              <Users className="h-4 w-4 mr-2" />
              {evaluationRoute.mode === 'controlled'
                ? 'Evaluation Committee'
                : `Evaluators (${tender.evaluators?.length || 0})`}
            </TabsTrigger>
          )}
          {showEvaluationStage && evaluationRoute.mode === 'legacy' && (
            <TabsTrigger value="qcbs" disabled={!tender.useQCBSEvaluation}>
              <Calculator className="h-4 w-4 mr-2" />
              QCBS
            </TabsTrigger>
          )}
          {isPublishedStage && (
            <TabsTrigger value="bids">
              <Award className="h-4 w-4 mr-2" />
              Bids ({tender.bids?.length || 0})
            </TabsTrigger>
          )}
          {showAwardStage && (
            <TabsTrigger value="award">
              <Award className="h-4 w-4 mr-2" />
              Award
            </TabsTrigger>
          )}
          {showAwardStage && (
            <TabsTrigger value="verification">
              <Shield className="h-4 w-4 mr-2" />
              Verification
            </TabsTrigger>
          )}
          <WorkflowTabTrigger value="approvals" />
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Basic Information</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <p className="text-sm text-gray-500">Tender Number</p>
                <p className="font-semibold">{tender.tenderNumber}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Tender Type</p>
                <p className="font-semibold">{tender.tenderType}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Status</p>
                <div className="flex items-center gap-2">
                  {getStatusBadge(tender.status)}
                  {tender.status === 'Submitted' &&
                    tender.currentWorkflowStepName && (
                      <Badge variant="outline" className="text-xs">
                        Step: {tender.currentWorkflowStepName}
                      </Badge>
                    )}
                </div>
              </div>
              <div>
                <p className="text-sm text-gray-500">Estimated Value</p>
                <p className="font-semibold">
                  {formatCurrency(tender.estimatedValue, tender.currency)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Publish Date</p>
                <p className="font-semibold">
                  {formatDate(tender.publishDate)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Submission Deadline</p>
                <p className="font-semibold">
                  {formatDate(tender.submissionDeadline)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Opening Date</p>
                <p className="font-semibold">
                  {formatDate(tender.openingDate)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">
                  Minimum Performance Rating
                </p>
                <p className="font-semibold">
                  {tender.minimumPerformanceRating
                    ? `${tender.minimumPerformanceRating}/5 ⭐`
                    : 'N/A'}
                </p>
              </div>
              {tender.description && (
                <div className="md:col-span-2">
                  <p className="text-sm text-gray-500">Description</p>
                  <p className="mt-1">{tender.description}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Evaluation Template</CardTitle>
              <CardDescription>
                {tender.evaluationTemplateName
                  ? `Using: ${tender.evaluationTemplateName}`
                  : 'No evaluation template assigned'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {tender.evaluationTemplateId ? (
                <div className="space-y-4">
                  <p className="text-sm text-muted-foreground">
                    Bids will be evaluated using the criteria defined in the
                    assigned template.
                  </p>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      router.push(
                        `/administration/procurement/evaluation-templates?highlight=${tender.evaluationTemplateId}`
                      )
                    }
                  >
                    View Template Details
                  </Button>
                </div>
              ) : (
                <div className="text-center py-4">
                  <p className="text-sm text-muted-foreground mb-2">
                    No evaluation template has been assigned to this tender.
                  </p>
                  {tender.status === 'Draft' && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        router.push(`/procurement/tenders/${tender.id}/edit`)
                      }
                    >
                      Assign Template
                    </Button>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Options</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <div className="flex items-center gap-2">
                {tender.requiresPrequalification ? (
                  <CheckCircle2 className="h-5 w-5 text-green-500" />
                ) : (
                  <XCircle className="h-5 w-5 text-gray-300" />
                )}
                <span>Requires Prequalification</span>
              </div>
              <div className="flex items-center gap-2">
                {tender.allowPartialBids ? (
                  <CheckCircle2 className="h-5 w-5 text-green-500" />
                ) : (
                  <XCircle className="h-5 w-5 text-gray-300" />
                )}
                <span>Allow Partial Bids</span>
              </div>
              <div className="flex items-center gap-2">
                {tender.useQCBSEvaluation ? (
                  <CheckCircle2 className="h-5 w-5 text-green-500" />
                ) : (
                  <XCircle className="h-5 w-5 text-gray-300" />
                )}
                <span>QCBS Evaluation</span>
                {tender.useQCBSEvaluation && (
                  <Badge variant="outline" className="ml-2">
                    Tech: {tender.technicalWeight}% | Fin:{' '}
                    {tender.financialWeight}%
                  </Badge>
                )}
              </div>
            </CardContent>
          </Card>

          {/* QCBS Configuration Card - only shown if enabled */}
          {tender.useQCBSEvaluation && (
            <Card className="border-blue-200 bg-blue-50/30">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Calculator className="h-5 w-5" />
                  QCBS Evaluation Configuration
                </CardTitle>
                <CardDescription>
                  Quality and Cost-Based Selection settings
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="p-4 bg-white rounded-lg border">
                    <p className="text-sm text-gray-500">Technical Weight</p>
                    <p className="text-2xl font-bold text-blue-600">
                      {tender.technicalWeight}%
                    </p>
                  </div>
                  <div className="p-4 bg-white rounded-lg border">
                    <p className="text-sm text-gray-500">Financial Weight</p>
                    <p className="text-2xl font-bold text-green-600">
                      {tender.financialWeight}%
                    </p>
                  </div>
                  <div className="p-4 bg-white rounded-lg border">
                    <p className="text-sm text-gray-500">
                      Minimum Technical Score
                    </p>
                    <p className="text-2xl font-bold text-orange-600">
                      {tender.minimumTechnicalScore}%
                    </p>
                  </div>
                </div>
                <div className="mt-4 p-3 bg-white rounded-lg border text-sm text-muted-foreground">
                  <strong>Formula:</strong> Combined Score = (Technical Score ×{' '}
                  {tender.technicalWeight}%) + (Financial Score ×{' '}
                  {tender.financialWeight}%)
                  <br />
                  Financial Score = (Lowest Bid / Bidder&apos;s Price) × 100
                </div>
              </CardContent>
            </Card>
          )}

          {tender.termsAndConditions && (
            <Card>
              <CardHeader>
                <CardTitle>Terms and Conditions</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap">
                  {tender.termsAndConditions}
                </p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Lots Tab */}
        <TabsContent value="items" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Tender Lots</CardTitle>
              <CardDescription>
                {tender.lots?.length || 0} lot(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!tender.lots || tender.lots.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No lots added yet
                </p>
              ) : (
                <div className="space-y-4">
                  {tender.lots.map((lot, lotIndex) => (
                    <div
                      key={lot.id}
                      className="border rounded-lg overflow-hidden"
                    >
                      {/* Lot Header */}
                      <div className="bg-gray-50 px-4 py-3 border-b">
                        <div className="flex justify-between items-start">
                          <div>
                            <h4 className="font-medium">
                              {lot.lotCode}: {lot.title}
                            </h4>
                            {lot.description && (
                              <p className="text-sm text-gray-500 mt-1">
                                {lot.description}
                              </p>
                            )}
                          </div>
                          <div className="text-right text-sm">
                            {lot.estimatedValue && (
                              <p className="font-medium">
                                {lot.currency || 'USD'}{' '}
                                {lot.estimatedValue.toLocaleString()}
                              </p>
                            )}
                            <p className="text-gray-500">
                              {lot.items?.length || 0} item(s)
                            </p>
                          </div>
                        </div>
                      </div>
                      {/* Lot Items */}
                      {lot.items && lot.items.length > 0 && (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead className="w-16">#</TableHead>
                              <TableHead>Item Code</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead>Quantity</TableHead>
                              <TableHead>Unit</TableHead>
                              <TableHead>Delivery Date</TableHead>
                              <TableHead>Location</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {lot.items.map((item) => (
                              <TableRow key={item.id}>
                                <TableCell>{item.lineNumber}</TableCell>
                                <TableCell>{item.itemCode || '-'}</TableCell>
                                <TableCell>
                                  <div>
                                    <p className="font-medium">
                                      {item.description}
                                    </p>
                                    {item.specifications && (
                                      <p className="text-sm text-gray-500">
                                        {item.specifications}
                                      </p>
                                    )}
                                  </div>
                                </TableCell>
                                <TableCell>{item.quantity}</TableCell>
                                <TableCell>
                                  {item.unitOfMeasure || '-'}
                                </TableCell>
                                <TableCell>
                                  {item.requiredDeliveryDate
                                    ? format(
                                        new Date(item.requiredDeliveryDate),
                                        'PP'
                                      )
                                    : '-'}
                                </TableCell>
                                <TableCell>
                                  {item.deliveryLocation || '-'}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Proposal Tab */}
        <TabsContent value="proposals" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Proposal Template</CardTitle>
              <CardDescription>
                Template for bidders to download and use when preparing their
                technical and commercial proposals
              </CardDescription>
            </CardHeader>
            <CardContent>
              {(() => {
                // Support both new 'ProposalTemplate' and legacy 'TechnicalProposalTemplate' for backward compatibility
                const proposalTemplate =
                  tender.documents?.find(
                    (doc) => doc.documentType === 'ProposalTemplate'
                  ) ||
                  tender.documents?.find(
                    (doc) => doc.documentType === 'TechnicalProposalTemplate'
                  );

                if (!proposalTemplate) {
                  return (
                    <p className="text-center py-8 text-gray-500">
                      No proposal template uploaded for this tender
                    </p>
                  );
                }

                return (
                  <div className="space-y-4">
                    {/* Proposal Template */}
                    <div className="border rounded-lg overflow-hidden">
                      <div className="p-4 bg-blue-50 border-b border-blue-200">
                        <div className="flex items-center gap-2">
                          <FileText className="h-5 w-5 text-blue-600" />
                          <span className="font-semibold text-blue-900">
                            Proposal Template
                          </span>
                          <Badge
                            variant="outline"
                            className="text-xs bg-green-50 text-green-700 border-green-200"
                          >
                            Uploaded
                          </Badge>
                        </div>
                        <p className="text-sm text-blue-700 mt-1">
                          Guide for bidders to prepare their technical and
                          commercial proposals
                        </p>
                      </div>
                      <div className="p-4 bg-white">
                        <div className="flex items-center justify-between p-3 border rounded-lg hover:bg-gray-50">
                          <div className="flex items-center gap-3">
                            <FileText className="h-5 w-5 text-blue-600" />
                            <div>
                              <p className="font-medium text-sm">
                                {proposalTemplate.documentName}
                              </p>
                              <p className="text-xs text-gray-500">
                                Uploaded{' '}
                                {proposalTemplate.uploadedAt ||
                                proposalTemplate.uploadedDate
                                  ? format(
                                      new Date(
                                        proposalTemplate.uploadedAt ||
                                          proposalTemplate.uploadedDate ||
                                          ''
                                      ),
                                      'PPp'
                                    )
                                  : ''}
                              </p>
                            </div>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() =>
                              window.open(
                                `${process.env.NEXT_PUBLIC_API_URL?.replace('/api', '')}${proposalTemplate.filePath}`,
                                '_blank'
                              )
                            }
                          >
                            <Download className="h-4 w-4 mr-1" />
                            Download
                          </Button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })()}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Document Requirements & Uploads</CardTitle>
              <CardDescription>
                {(() => {
                  try {
                    const requirements = tender.requiredDocuments
                      ? JSON.parse(tender.requiredDocuments)
                      : [];
                    const requiredCount = requirements.filter(
                      (r: any) => r.isRequired
                    ).length;
                    const optionalCount = requirements.length - requiredCount;
                    const uploadedCount = tender.documents?.length || 0;
                    return `${requirements.length} requirement(s) - ${requiredCount} required, ${optionalCount} optional • ${uploadedCount} file(s) uploaded`;
                  } catch {
                    return `${tender.documents?.length || 0} file(s) uploaded`;
                  }
                })()}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {(() => {
                try {
                  const requirements = tender.requiredDocuments
                    ? JSON.parse(tender.requiredDocuments)
                    : [];
                  const uploadedDocs = tender.documents || [];
                  const acceptanceDeclarationDoc = uploadedDocs.find(
                    (doc) => doc.documentType === 'AcceptanceDeclaration'
                  );

                  if (requirements.length === 0 && uploadedDocs.length === 0) {
                    return (
                      <p className="text-center py-8 text-gray-500">
                        No document requirements or uploads
                      </p>
                    );
                  }

                  return (
                    <div className="space-y-4">
                      {/* Acceptance Declaration Section */}
                      {tender.requiresAcceptanceDeclaration && (
                        <div className="border-2 border-purple-200 rounded-lg overflow-hidden bg-purple-50/30">
                          <div className="p-4 bg-purple-100 border-b border-purple-200">
                            <div className="flex items-start justify-between">
                              <div className="flex-1">
                                <div className="flex items-center gap-2">
                                  <FileText className="h-5 w-5 text-purple-600" />
                                  <p className="font-semibold text-purple-900">
                                    Supplier Acceptance Declaration
                                  </p>
                                  <Badge className="text-xs bg-purple-600">
                                    Required for Bidding
                                  </Badge>
                                  {acceptanceDeclarationDoc && (
                                    <Badge
                                      variant="outline"
                                      className="text-xs bg-green-50 text-green-700 border-green-200"
                                    >
                                      Uploaded
                                    </Badge>
                                  )}
                                </div>
                                <p className="text-sm text-purple-700 mt-1">
                                  Bidders must download, review, and accept this
                                  declaration before submitting their bid
                                </p>
                              </div>
                            </div>
                          </div>
                          {acceptanceDeclarationDoc ? (
                            <div className="p-4 bg-white">
                              <div className="flex items-center justify-between p-3 border rounded-lg hover:bg-gray-50">
                                <div className="flex items-center gap-3">
                                  <FileText className="h-5 w-5 text-purple-600" />
                                  <div>
                                    <p className="font-medium text-sm">
                                      {acceptanceDeclarationDoc.documentName}
                                    </p>
                                    <p className="text-xs text-gray-500">
                                      Uploaded:{' '}
                                      {formatDate(
                                        acceptanceDeclarationDoc.uploadedDate
                                      )}
                                      {acceptanceDeclarationDoc.fileSize &&
                                        ` • ${(acceptanceDeclarationDoc.fileSize / 1024 / 1024).toFixed(2)} MB`}
                                    </p>
                                  </div>
                                </div>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => {
                                    if (acceptanceDeclarationDoc) {
                                      tenderService.downloadTenderDocument(
                                        tenderId,
                                        acceptanceDeclarationDoc.id,
                                        acceptanceDeclarationDoc.documentName
                                      );
                                    }
                                  }}
                                >
                                  <Download className="h-4 w-4 mr-2" />
                                  Download
                                </Button>
                              </div>
                            </div>
                          ) : (
                            <div className="p-4 text-center text-sm text-amber-600 bg-amber-50/50 border-t border-amber-200">
                              ⚠️ No acceptance declaration document uploaded yet
                            </div>
                          )}
                        </div>
                      )}

                      {/* Show requirements with their uploaded files */}
                      {requirements.map((req: any, index: number) => {
                        // Find uploaded files matching this requirement
                        const matchingDocs = uploadedDocs.filter(
                          (doc) => doc.documentType === req.documentType
                        );

                        return (
                          <div
                            key={index}
                            className="border rounded-lg overflow-hidden"
                          >
                            {/* Requirement Header */}
                            <div className="p-4 bg-gray-50 border-b">
                              <div className="flex items-start justify-between">
                                <div className="flex-1">
                                  <div className="flex items-center gap-2">
                                    <p className="font-medium">
                                      {req.documentName}
                                    </p>
                                    <Badge
                                      variant={
                                        req.isRequired
                                          ? 'destructive'
                                          : 'secondary'
                                      }
                                      className="text-xs"
                                    >
                                      {req.isRequired ? 'Required' : 'Optional'}
                                    </Badge>
                                    {matchingDocs.length > 0 && (
                                      <Badge
                                        variant="outline"
                                        className="text-xs bg-green-50 text-green-700 border-green-200"
                                      >
                                        {matchingDocs.length} uploaded
                                      </Badge>
                                    )}
                                  </div>
                                  <p className="text-sm text-gray-600 mt-1">
                                    Type:{' '}
                                    <span className="font-medium">
                                      {req.documentType}
                                    </span>
                                  </p>
                                  {req.description && (
                                    <p className="text-sm text-gray-500 mt-1">
                                      {req.description}
                                    </p>
                                  )}
                                  <p className="text-xs text-gray-500 mt-2">
                                    Max Size: {req.maxFileSizeMB}MB • Allowed
                                    Types: {req.allowedFileTypes}
                                  </p>
                                </div>
                              </div>
                            </div>

                            {/* Uploaded Files for this Requirement */}
                            {matchingDocs.length > 0 ? (
                              <div className="p-4 space-y-2">
                                {matchingDocs.map((doc) => (
                                  <div
                                    key={doc.id}
                                    className="flex items-center justify-between p-3 bg-white border rounded-lg hover:bg-gray-50"
                                  >
                                    <div className="flex items-center gap-3">
                                      <FileText className="h-5 w-5 text-blue-600" />
                                      <div>
                                        <p className="font-medium text-sm">
                                          {doc.documentName}
                                        </p>
                                        <p className="text-xs text-gray-500">
                                          Uploaded:{' '}
                                          {formatDate(doc.uploadedDate)}
                                          {doc.fileSize &&
                                            ` • ${(doc.fileSize / 1024 / 1024).toFixed(2)} MB`}
                                        </p>
                                      </div>
                                    </div>
                                    <Button variant="ghost" size="sm">
                                      <Download className="h-4 w-4 mr-2" />
                                      Download
                                    </Button>
                                  </div>
                                ))}
                              </div>
                            ) : (
                              <div className="p-4 text-center text-sm text-gray-500 bg-gray-50/50">
                                No files uploaded for this requirement yet
                              </div>
                            )}
                          </div>
                        );
                      })}

                      {/* Show uploaded files that don't match any requirement */}
                      {(() => {
                        const unmatchedDocs = uploadedDocs.filter(
                          (doc) =>
                            doc.documentType !== 'AcceptanceDeclaration' && // Exclude acceptance declaration (shown separately)
                            doc.documentType !== 'ProposalTemplate' && // Exclude proposal templates (shown in Proposal tab)
                            doc.documentType !== 'TechnicalProposalTemplate' && // Legacy proposal template
                            doc.documentType !== 'CommercialProposalTemplate' && // Legacy proposal template
                            !requirements.some(
                              (req: any) =>
                                req.documentType === doc.documentType
                            )
                        );

                        if (unmatchedDocs.length > 0) {
                          return (
                            <div className="border rounded-lg overflow-hidden">
                              <div className="p-4 bg-gray-50 border-b">
                                <div className="flex items-center gap-2">
                                  <p className="font-medium">Other Documents</p>
                                  <Badge variant="outline" className="text-xs">
                                    {unmatchedDocs.length} file(s)
                                  </Badge>
                                </div>
                                <p className="text-sm text-gray-500 mt-1">
                                  Documents that don't match any specific
                                  requirement
                                </p>
                              </div>
                              <div className="p-4 space-y-2">
                                {unmatchedDocs.map((doc) => (
                                  <div
                                    key={doc.id}
                                    className="flex items-center justify-between p-3 bg-white border rounded-lg hover:bg-gray-50"
                                  >
                                    <div className="flex items-center gap-3">
                                      <FileText className="h-5 w-5 text-blue-600" />
                                      <div>
                                        <p className="font-medium text-sm">
                                          {doc.documentName}
                                        </p>
                                        <p className="text-xs text-gray-500">
                                          Type: {doc.documentType} • Uploaded:{' '}
                                          {formatDate(doc.uploadedDate)}
                                          {doc.fileSize &&
                                            ` • ${(doc.fileSize / 1024 / 1024).toFixed(2)} MB`}
                                        </p>
                                      </div>
                                    </div>
                                    <Button variant="ghost" size="sm">
                                      <Download className="h-4 w-4 mr-2" />
                                      Download
                                    </Button>
                                  </div>
                                ))}
                              </div>
                            </div>
                          );
                        }
                        return null;
                      })()}
                    </div>
                  );
                } catch (error) {
                  console.error('Error parsing document requirements:', error);
                  return (
                    <p className="text-center py-8 text-red-500">
                      Error loading documents
                    </p>
                  );
                }
              })()}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Fees Tab */}
        <TabsContent value="fees" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Tender Fees</CardTitle>
              <CardDescription>
                {tender.fees?.length || 0} fee(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!tender.fees || tender.fees.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No fees configured
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Fee Type</TableHead>
                      <TableHead>Amount</TableHead>
                      <TableHead>Payment Method</TableHead>
                      <TableHead>Due Date</TableHead>
                      <TableHead>Mandatory</TableHead>
                      <TableHead>Description</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tender.fees.map((fee) => (
                      <TableRow key={fee.id}>
                        <TableCell>
                          <Badge>{fee.feeType}</Badge>
                        </TableCell>
                        <TableCell className="font-semibold">
                          {formatCurrency(fee.amount, fee.currency)}
                        </TableCell>
                        <TableCell>{fee.paymentMethod || '-'}</TableCell>
                        <TableCell>
                          {fee.dueDate
                            ? format(new Date(fee.dueDate), 'PP')
                            : '-'}
                        </TableCell>
                        <TableCell>
                          {fee.isMandatory ? (
                            <Badge variant="destructive">Required</Badge>
                          ) : (
                            <Badge variant="outline">Optional</Badge>
                          )}
                        </TableCell>
                        <TableCell>{fee.description || '-'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Invitations Tab */}
        <TabsContent value="invitations" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Business Partner Invitations</CardTitle>
              <CardDescription>
                {tender.invitations?.length || 0} invitation(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!tender.invitations || tender.invitations.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No invitations sent yet
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Business Partner</TableHead>
                      <TableHead>Invited Date</TableHead>
                      <TableHead>Notification Sent</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tender.invitations.map((invitation) => (
                      <TableRow key={invitation.id}>
                        <TableCell className="font-medium">
                          {invitation.businessPartnerName}
                        </TableCell>
                        <TableCell>
                          {formatDate(invitation.invitedDate)}
                        </TableCell>
                        <TableCell>
                          {(invitation.notificationSent ?? false) ? (
                            <Badge
                              variant="default"
                              className="bg-green-100 text-green-800"
                            >
                              Sent
                            </Badge>
                          ) : (
                            <Badge variant="outline">Not Sent</Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">Invited</Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Clarifications Tab */}
        <TabsContent value="clarifications" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Clarifications & Questions</CardTitle>
              <CardDescription>
                {tender.clarifications?.length || 0} clarification(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!tender.clarifications || tender.clarifications.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No clarifications requested yet
                </p>
              ) : (
                <div className="space-y-4">
                  {tender.clarifications.map((clarification) => (
                    <Card key={clarification.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div>
                            <CardTitle className="text-base">
                              Question from{' '}
                              {clarification.businessPartnerName || 'Anonymous'}
                            </CardTitle>
                            <CardDescription>
                              {formatDate(clarification.questionDate)}
                            </CardDescription>
                          </div>
                          <Badge
                            variant={
                              clarification.status === 'Answered'
                                ? 'default'
                                : 'outline'
                            }
                          >
                            {clarification.status}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-3">
                        <div>
                          <p className="text-sm font-medium text-gray-500">
                            Question:
                          </p>
                          <p className="mt-1">{clarification.question}</p>
                        </div>
                        {clarification.status === 'Answered' &&
                          clarification.answer && (
                            <div className="bg-blue-50 p-3 rounded-lg">
                              <p className="text-sm font-medium text-blue-900">
                                Answer:
                              </p>
                              <p className="mt-1 text-blue-800">
                                {clarification.answer}
                              </p>
                              {clarification.answerDate && (
                                <p className="text-xs text-blue-600 mt-2">
                                  Answered on{' '}
                                  {formatDate(clarification.answerDate)} by{' '}
                                  {clarification.answeredByName || 'Staff'}
                                </p>
                              )}
                            </div>
                          )}
                        {clarification.status === 'Pending' &&
                          canAdministerTender && (
                            <Button
                              size="sm"
                              onClick={() => {
                                setSelectedClarification(clarification);
                                setShowAnswerDialog(true);
                              }}
                            >
                              <MessageSquare className="h-4 w-4 mr-2" />
                              Answer Question
                            </Button>
                          )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="revisions" className="space-y-4">
          <TenderRevisionsPanel
            tenderId={tenderId}
            tenderStatus={tender.status}
            currentSubmissionDeadline={tender.submissionDeadline}
            currentOpeningDate={tender.openingDate}
            revisions={tender.revisions || []}
            onChanged={loadTenderDetails}
          />
        </TabsContent>

        {/* Evaluators Tab */}
        <TabsContent value="evaluators" className="space-y-4">
          <Card>
            <CardContent className="flex flex-col justify-between gap-3 pt-6 sm:flex-row sm:items-center">
              <div>
                <p className="font-medium">
                  {evaluationRoute.mode === 'controlled'
                    ? 'Controlled tender evaluation'
                    : 'Source-specific committee controls'}
                </p>
                <p className="text-sm text-muted-foreground">
                  {evaluationRoute.mode === 'controlled'
                    ? 'This tender uses one signed committee lifecycle for appointment acceptance, COI, quorum, technical scoring, and the financial recommendation. Individual legacy evaluator assignments are not used.'
                    : 'Appointment acceptance, COI, signed quorum, scorer eligibility, immutable score locks, and controlled recall.'}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  onClick={() => router.push(evaluationRoute.committeeHref)}
                >
                  <Users className="mr-2 h-4 w-4" />
                  Committee controls
                </Button>
                {evaluationRoute.mode === 'controlled' && (
                  <Button
                    onClick={() => router.push(evaluationRoute.evaluationHref)}
                  >
                    <Shield className="mr-2 h-4 w-4" />
                    {evaluationRoute.evaluationLabel}
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
          {evaluationRoute.mode === 'legacy' && (
            <TenderEvaluators
              tenderId={tenderId}
              tenderStatus={tender.status}
              onEvaluatorsChanged={loadTenderDetails}
            />
          )}
        </TabsContent>

        {/* Bids Tab */}
        <TabsContent value="bids" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Submitted Bids</CardTitle>
              <CardDescription>
                {tender.bids?.length || 0} bid(s) received
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!tender.bids || tender.bids.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No bids submitted yet
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Bid Number</TableHead>
                      <TableHead>Business Partner</TableHead>
                      <TableHead>Total Amount</TableHead>
                      <TableHead>Submitted Date</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tender.bids.map((bid) => (
                      <TableRow key={bid.id}>
                        <TableCell className="font-mono">
                          {bid.bidNumber}
                        </TableCell>
                        <TableCell className="font-medium">
                          {bid.businessPartnerName}
                        </TableCell>
                        <TableCell className="font-semibold">
                          {bid.isSealed || bid.isFinancialProposalSealed ? 'Sealed' : formatCurrency(bid.totalBidAmount, bid.currency)}
                        </TableCell>
                        <TableCell>{formatDate(bid.submittedDate)}</TableCell>
                        <TableCell>
                          <Badge
                            variant={
                              bid.status === 'Submitted' ? 'default' : 'outline'
                            }
                          >
                            {bid.status}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                              router.push(`/procurement/bids/${bid.id}`)
                            }
                          >
                            View Details
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* QCBS Evaluation Tab */}
        {evaluationRoute.mode === 'legacy' && (
          <TabsContent value="qcbs" className="space-y-4">
            <QCBSEvaluationPanel
              tenderId={tenderId}
              useQCBSEvaluation={tender.useQCBSEvaluation}
              technicalWeight={tender.technicalWeight}
              financialWeight={tender.financialWeight}
              minimumTechnicalScore={tender.minimumTechnicalScore}
              tenderStatus={tender.status}
            />
          </TabsContent>
        )}

        {/* Award Tab */}
        <TabsContent value="award" className="space-y-4">
          <TenderAward
            tenderId={tenderId}
            tenderNumber={tender.tenderNumber}
            tenderTitle={tender.title}
            onAwardCreated={loadTenderDetails}
          />
        </TabsContent>

        {/* Verification Tab */}
        <TabsContent value="verification" className="space-y-4">
          <AwardVerificationResults tenderId={tenderId} />
        </TabsContent>

        {/* Approvals Tab */}
        <WorkflowTabContent value="approvals" {...workflow.actionProps} />
      </Tabs>

      {/* Publish Tender Confirmation Dialog */}
      <ConfirmationDialog
        open={showPublishDialog}
        onOpenChange={setShowPublishDialog}
        title="Publish Tender"
        maxWidth="900px"
        description={
          <div className="space-y-4">
            {publishError && (
              <Alert variant="destructive" role="alert">
                <AlertDescription>{publishError}</AlertDescription>
              </Alert>
            )}
            <div className="space-y-2">
              <p className="text-sm text-muted-foreground">
                Review the supplier audience and submission schedule before
                publishing. Notifications will be sent to the configured
                recipients.
              </p>
              <div className="rounded-lg border bg-blue-50 border-blue-200 p-3">
                <div className="flex gap-2">
                  <Shield className="h-5 w-5 text-blue-600 flex-shrink-0 mt-0.5" />
                  <p className="text-sm text-blue-800">
                    <strong>Supplier access:</strong>{' '}
                    {publicationPresentation.supplierAccessMessage}
                  </p>
                </div>
              </div>
            </div>

            {/* Tender Summary */}
            <div className="rounded-lg border bg-muted/50 p-4 space-y-3">
              <h4 className="font-semibold text-sm">Tender Summary</h4>
              <div className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">Title:</span>
                  <span className="font-medium">{tender?.title}</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">Type:</span>
                  <span className="font-medium">
                    {getTenderTypeLabel(tender?.tenderType || '')}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">
                    Estimated Value:
                  </span>
                  <span className="font-medium">
                    {formatCurrency(tender?.estimatedValue, tender?.currency)}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">Lots:</span>
                  <span className="font-medium">
                    {tender?.lots?.length || 0} lot(s)
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">
                    Document Requirements:
                  </span>
                  <span className="font-medium">
                    {(() => {
                      try {
                        const requirements = tender?.requiredDocuments
                          ? JSON.parse(tender.requiredDocuments)
                          : [];
                        return `${requirements.length} requirement(s)`;
                      } catch {
                        return '0 requirement(s)';
                      }
                    })()}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">Fees:</span>
                  <span className="font-medium">
                    {tender?.fees?.length || 0} fee(s)
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">
                    Invited Suppliers:
                  </span>
                  <span className="font-medium">
                    {tender?.invitations?.length || 0} supplier(s)
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-muted-foreground">
                    Submission Deadline:
                  </span>
                  <span className="font-medium">
                    {tender?.submissionDeadline
                      ? format(new Date(tender.submissionDeadline), 'PPP')
                      : 'Not set'}
                  </span>
                </div>
              </div>
            </div>

            <div className="rounded-lg border bg-white p-4 space-y-3">
              <h4 className="font-semibold text-sm">Submission schedule</h4>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-2">
                  <Label>Submission deadline</Label>
                  <Input
                    type="datetime-local"
                    value={publishData.submissionDeadline}
                    onChange={(e) =>
                      setPublishData((prev) => ({
                        ...prev,
                        submissionDeadline: e.target.value,
                      }))
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>
                    {publicationPresentation.requiresControlledPublication
                      ? 'Controlled bid opening'
                      : 'Bid opening (optional)'}
                  </Label>
                  <Input
                    type="datetime-local"
                    value={publishData.openingDate}
                    min={publishData.submissionDeadline || undefined}
                    onChange={(e) =>
                      setPublishData((prev) => ({
                        ...prev,
                        openingDate: e.target.value,
                      }))
                    }
                  />
                </div>
              </div>
            </div>

            {publicationPresentation.requiresControlledPublication && (
              <div className="rounded-lg border bg-white p-4 space-y-3">
                <h4 className="font-semibold text-sm">
                  {publicationPresentation.controlledPublicationHeading}
                </h4>
                <p className="text-xs text-muted-foreground">
                  Required by this tender&apos;s advanced immutable sourcing
                  case. Bind the approved publication, version, fee terms and
                  deadlines in the{' '}
                  <Button
                    type="button"
                    variant="link"
                    className="h-auto p-0 text-xs"
                    onClick={() =>
                      router.push(
                        `/procurement/tenders/${tenderId}/document-controls`
                      )
                    }
                  >
                    controlled document register
                  </Button>{' '}
                  before publication; the server validates the exact method,
                  version and authority lineage.
                </p>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-2">
                    <Label>
                      {publicationPresentation.publicationReferenceLabel}
                    </Label>
                    <Input
                      value={publishData.advertisementReference}
                      onChange={(e) =>
                        setPublishData((prev) => ({
                          ...prev,
                          advertisementReference: e.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Publication channel</Label>
                    <Input
                      value={publishData.publicationChannel}
                      onChange={(e) =>
                        setPublishData((prev) => ({
                          ...prev,
                          publicationChannel: e.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Approved document reference</Label>
                    <Input
                      value={publishData.tenderDocumentReference}
                      onChange={(e) =>
                        setPublishData((prev) => ({
                          ...prev,
                          tenderDocumentReference: e.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Document version</Label>
                    <Input
                      value={publishData.tenderDocumentVersion}
                      onChange={(e) =>
                        setPublishData((prev) => ({
                          ...prev,
                          tenderDocumentVersion: e.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Document fee (0 = free)</Label>
                    <Input
                      type="number"
                      min="0"
                      value={publishData.documentFee}
                      onChange={(e) =>
                        setPublishData((prev) => ({
                          ...prev,
                          documentFee: Number(e.target.value),
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>
                      {publicationPresentation.publicationEvidenceLabel}
                    </Label>
                    <Input
                      value={publishData.advertisementEvidenceReference}
                      onChange={(e) =>
                        setPublishData((prev) => ({
                          ...prev,
                          advertisementEvidenceReference: e.target.value,
                        }))
                      }
                    />
                  </div>
                </div>
              </div>
            )}

            {/* External/Public Recipients (RFQ) */}
            {tender?.tenderType === 'RFQ' && (
              <div className="rounded-lg border bg-white p-4 space-y-3">
                <h4 className="font-semibold text-sm">
                  Public Suppliers (Email Only)
                </h4>
                <p className="text-xs text-muted-foreground">
                  Add external email recipients to receive the RFQ PDF
                  attachment. Emails are sent individually (no shared BCC list).
                </p>

                <div className="space-y-2">
                  <Label>Additional Recipient Email</Label>
                  <div className="flex gap-2">
                    <Input
                      placeholder="supplier@example.com"
                      value={externalEmailInput}
                      onChange={(e) => setExternalEmailInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.preventDefault();
                          addExternalRecipientEmail();
                        }
                      }}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      onClick={addExternalRecipientEmail}
                    >
                      Add
                    </Button>
                  </div>
                </div>

                {externalRecipientEmails.length > 0 && (
                  <div className="flex flex-wrap gap-2">
                    {externalRecipientEmails.map((email) => (
                      <div
                        key={email}
                        className="flex items-center gap-2 px-2 py-1 rounded border bg-muted/40 text-xs"
                      >
                        <span>{email}</span>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="h-5 w-5"
                          onClick={() => removeExternalRecipientEmail(email)}
                        >
                          <XCircle className="h-3 w-3" />
                        </Button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {/* What happens next */}
            <div className="rounded-lg border bg-blue-50 border-blue-200 p-3">
              <div className="flex gap-2">
                <Send className="h-5 w-5 text-blue-600 flex-shrink-0 mt-0.5" />
                <div className="text-sm text-blue-800">
                  <p className="font-medium mb-1">What happens next?</p>
                  <ul className="space-y-1 list-disc list-inside text-xs">
                    <li>Tender status will change to "Published"</li>
                    <li>
                      {getTenderPublicationNotificationMessage(tender?.invitations?.length || 0, externalRecipientEmails.length)}
                    </li>
                    {(tender?.invitations?.length || 0) > 0 && <li>
                      In-app notifications will be created for invited suppliers
                    </li>}
                    <li>
                      Eligible suppliers can access the tender and submit bids
                      under the configured procurement route until the deadline
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        }
        confirmText="Publish Tender"
        cancelText="Cancel"
        variant="default"
        onConfirm={handlePublishConfirm}
        isLoading={publishing}
      />

      {/* Answer Clarification Dialog */}
      {selectedClarification && (
        <AnswerClarificationDialog
          open={showAnswerDialog}
          onOpenChange={setShowAnswerDialog}
          tenderId={tenderId}
          clarification={selectedClarification}
          onAnswered={loadTenderDetails}
        />
      )}
    </div>
  );
}
