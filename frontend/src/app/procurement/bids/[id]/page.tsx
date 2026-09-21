'use client';

import React, { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
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
  FileText,
  Package,
  Upload,
  Award,
  MessageSquare,
  Clock,
  XCircle,
  CheckCircle2,
  CheckCircle,
  Download,
  DollarSign,
  AlertCircle,
  RefreshCw,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';
import * as tenderBidService from '@/services/tenderBidService';
import {
  evaluationTemplateService,
  type EvaluationTemplate,
} from '@/services/evaluationTemplateService';
import {
  type TenderBidDetailDto,
  type TenderPaymentDto,
} from '@/services/tenderBidService';
import { format } from 'date-fns';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { QuantitySurveyTenderBoqVettingPanel } from '@/components/quantity-survey/QuantitySurveyTenderBoqVettingPanel';
import { isQsOptionalFeatureEnabled } from '@/lib/quantity-survey-architecture-scope';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { tenderService, type TenderDetailDto } from '@/services/tenderService';
import { getSupportingDocuments, getSupportingDocumentRequirements } from '@/lib/procurement-bid-documents';
import {
  getTenderEvaluationRoute,
  type TenderEvaluationRoute,
} from '@/lib/procurement-tender-evaluation-route';

function BidDocumentRequirements({ bid, tender }: { bid: TenderBidDetailDto; tender: TenderDetailDto | null }) {
  const requirements = getSupportingDocumentRequirements(tender);
  if (requirements === null) {
    return <Alert className="mb-4"><AlertTitle>Document requirements unavailable</AlertTitle><AlertDescription>Uploaded files are listed below, but completeness cannot be checked until the tender requirements load correctly.</AlertDescription></Alert>;
  }

  if (!requirements.length) return <p className="mb-4 text-sm text-muted-foreground">No supporting-document requirements were configured for this tender. Technical and commercial proposals are listed under Proposals.</p>;
  return (
    <section aria-label="Document requirements" className="mb-6 space-y-2">
      <h3 className="font-medium">Tender supporting-document requirements</h3>
      <p className="text-sm text-muted-foreground">{requirements.filter((item) => item.isRequired).length} required · {requirements.filter((item) => !item.isRequired).length} optional. Upload presence does not confirm document validity.</p>
      <ul className="divide-y rounded-lg border">
        {requirements.map((requirement, index) => {
          const count = bid.documents?.filter((document) => document.documentType === requirement.documentType).length ?? 0;
          const withheld = bid.isFinancialProposalSealed;
          const status = count ? `Uploaded (${count})` : withheld ? 'Not available in this phase' : requirement.isRequired ? 'Missing' : 'Not supplied';
          return (
            <li key={`${requirement.documentType}-${index}`} className="flex items-center justify-between gap-4 p-3 text-sm">
              <span>{requirement.documentName}<span className="ml-2 text-muted-foreground">{requirement.isRequired ? 'Required' : 'Optional'}</span></span>
              <Badge variant={!count && !withheld && requirement.isRequired ? 'destructive' : 'secondary'}>{status}</Badge>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

// Interface for criteria scores stored in evaluationCriteriaJson
interface CriteriaScore {
  criterionId: string;
  criterionName: string;
  criterionCode: string;
  score: number;
  weight: number;
  maxScore: number;
  weightedScore: number;
}

export const TENDER_PAYMENT_VERIFY_PERMISSION =
  'procurement.tender.payment.verify';

type PaymentDecision = {
  payment: TenderPaymentDto;
  isApproved: boolean;
};

interface TenderPaymentVerificationPanelProps {
  bidId: string;
  payments: TenderPaymentDto[];
  canVerifyPayment: boolean;
  onRefresh: (updatedPayment: TenderPaymentDto) => Promise<void>;
}

const paymentStatusPresentation = (status: string) => {
  const normalized = status.trim().toLowerCase();
  if (normalized === 'pending') {
    return {
      label: 'Pending verification',
      className: 'border-amber-200 bg-amber-100 text-amber-800',
    };
  }
  if (normalized === 'verified') {
    return {
      label: 'Verified',
      className: 'border-green-200 bg-green-100 text-green-800',
    };
  }
  if (normalized === 'rejected') {
    return {
      label: 'Rejected',
      className: 'border-red-200 bg-red-100 text-red-800',
    };
  }
  return {
    label: status || 'Unknown',
    className: 'border-gray-200 bg-gray-100 text-gray-800',
  };
};

const formatPaymentDate = (dateString?: string) => {
  if (!dateString) return 'Not recorded';
  try {
    return format(new Date(dateString), 'PPP p');
  } catch {
    return dateString;
  }
};

const formatPaymentAmount = (amount: number, currency: string) => {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency || 'GHS',
      currencyDisplay: 'code',
    }).format(amount);
  } catch {
    return `${currency || 'GHS'} ${amount.toLocaleString(undefined, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;
  }
};

export function TenderPaymentVerificationPanel({
  bidId,
  payments,
  canVerifyPayment,
  onRefresh,
}: TenderPaymentVerificationPanelProps) {
  const [decision, setDecision] = useState<PaymentDecision | null>(null);
  const [notes, setNotes] = useState('');
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [verifying, setVerifying] = useState(false);
  const isFinanceRecovery = Boolean(
    decision?.isApproved &&
      decision.payment.status.trim().toLowerCase() === 'verified' &&
      !decision.payment.journalEntryId
  );

  const openDecision = (payment: TenderPaymentDto, isApproved: boolean) => {
    setDecision({ payment, isApproved });
    setNotes('');
    setDecisionError(null);
  };

  const closeDecision = () => {
    if (verifying) return;
    setDecision(null);
    setNotes('');
    setDecisionError(null);
  };

  const confirmDecision = async () => {
    if (!decision) return false;

    const trimmedNotes = notes.trim();
    if (!decision.isApproved && !trimmedNotes) {
      setDecisionError('A rejection reason is required.');
      return false;
    }

    try {
      setVerifying(true);
      setDecisionError(null);
      const updatedPayment = await tenderBidService.verifyBidPayment(
        bidId,
        decision.payment.id,
        {
          isApproved: decision.isApproved,
          notes: trimmedNotes || undefined,
        }
      );

      let refreshFailed = false;
      try {
        await onRefresh(updatedPayment);
      } catch (refreshError) {
        refreshFailed = true;
        console.error('Error refreshing tender fee payments:', refreshError);
      }

      const action = isFinanceRecovery
        ? 'posted to Finance'
        : decision.isApproved
          ? 'approved'
          : 'rejected';
      toast.success(`Tender fee payment ${action}.`);
      if (refreshFailed) {
        toast.error(
          'The decision was saved, but the payment list could not be refreshed. Reload this page to confirm the latest status.'
        );
      }
      setDecision(null);
      setNotes('');
      setDecisionError(null);
      return true;
    } catch (error) {
      console.error('Error verifying tender fee payment:', error);
      const message =
        error instanceof Error
          ? error.message
          : 'Failed to update the tender fee payment.';
      setDecisionError(message);
      toast.error(message);
      return false;
    } finally {
      setVerifying(false);
    }
  };

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>Tender Fee Payments</CardTitle>
          <CardDescription>
            Review the supplier&apos;s recorded tender fee payments and their
            verification status.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {payments.length === 0 ? (
            <div className="rounded-lg border border-dashed py-10 text-center text-sm text-gray-500">
              No tender fee payments have been recorded for this bid.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Payment reference</TableHead>
                    <TableHead>Amount</TableHead>
                    <TableHead>Method</TableHead>
                    <TableHead>Payment date</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Finance posting</TableHead>
                    {canVerifyPayment && (
                      <TableHead className="text-right">Actions</TableHead>
                    )}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {payments.map((payment) => {
                    const status = paymentStatusPresentation(payment.status);
                    const isPending =
                      payment.status.trim().toLowerCase() === 'pending';
                    const isVerifiedWithoutPosting =
                      payment.status.trim().toLowerCase() === 'verified' &&
                      !payment.journalEntryId;
                    return (
                      <TableRow key={payment.id}>
                        <TableCell>
                          <p className="font-medium">
                            {payment.paymentReference || 'Not provided'}
                          </p>
                          {payment.transactionId && (
                            <p className="text-xs text-gray-500">
                              Transaction: {payment.transactionId}
                            </p>
                          )}
                        </TableCell>
                        <TableCell className="font-medium">
                          {formatPaymentAmount(
                            payment.amount,
                            payment.currency
                          )}
                        </TableCell>
                        <TableCell>{payment.paymentMethod || '—'}</TableCell>
                        <TableCell>
                          {formatPaymentDate(payment.paymentDate)}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className={status.className}>
                            {status.label}
                          </Badge>
                          {payment.verifiedDate && (
                            <p className="mt-1 text-xs text-gray-500">
                              {payment.verifiedByName
                                ? `By ${payment.verifiedByName} · `
                                : ''}
                              {formatPaymentDate(payment.verifiedDate)}
                            </p>
                          )}
                        </TableCell>
                        <TableCell>
                          {payment.journalEntryId ? (
                            <div className="space-y-1">
                              <Badge
                                variant="outline"
                                className="border-green-200 bg-green-100 text-green-800"
                              >
                                Posted
                              </Badge>
                              <a
                                href={`/finance/journal-entries/${payment.journalEntryId}`}
                                className="block text-xs font-medium text-primary hover:underline"
                              >
                                View journal entry
                              </a>
                              {payment.postedAtUtc && (
                                <p className="text-xs text-gray-500">
                                  {formatPaymentDate(payment.postedAtUtc)}
                                </p>
                              )}
                            </div>
                          ) : payment.status.trim().toLowerCase() ===
                            'verified' ? (
                            <Badge
                              variant="outline"
                              className="border-amber-200 bg-amber-100 text-amber-800"
                            >
                              Posting pending
                            </Badge>
                          ) : (
                            <span className="text-gray-400">—</span>
                          )}
                        </TableCell>
                        {canVerifyPayment && (
                          <TableCell className="text-right">
                            {isPending ? (
                              <div className="flex justify-end gap-2">
                                <Button
                                  size="sm"
                                  onClick={() => openDecision(payment, true)}
                                  aria-label={`Approve payment ${payment.paymentReference}`}
                                >
                                  <CheckCircle2 className="mr-2 h-4 w-4" />
                                  Approve
                                </Button>
                                <Button
                                  size="sm"
                                  variant="destructive"
                                  onClick={() => openDecision(payment, false)}
                                  aria-label={`Reject payment ${payment.paymentReference}`}
                                >
                                  <XCircle className="mr-2 h-4 w-4" />
                                  Reject
                                </Button>
                              </div>
                            ) : isVerifiedWithoutPosting ? (
                              <Button
                                size="sm"
                                onClick={() => openDecision(payment, true)}
                                aria-label={`Post payment ${payment.paymentReference} to Finance`}
                              >
                                <RefreshCw className="mr-2 h-4 w-4" />
                                Post to Finance
                              </Button>
                            ) : (
                              <span className="text-sm text-gray-500">
                                Decision complete
                              </span>
                            )}
                          </TableCell>
                        )}
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={Boolean(decision)}
        onOpenChange={(open) => {
          if (!open) closeDecision();
        }}
        title={
          isFinanceRecovery
            ? 'Post tender fee payment to Finance'
            : decision?.isApproved
              ? 'Approve tender fee payment'
              : 'Reject tender fee payment'
        }
        description={
          decision
            ? `${isFinanceRecovery ? 'Create the missing Finance journal for' : decision.isApproved ? 'Approve' : 'Reject'} payment ${decision.payment.paymentReference || 'without a reference'} for ${formatPaymentAmount(decision.payment.amount, decision.payment.currency)}.`
            : undefined
        }
        confirmText={
          isFinanceRecovery
            ? 'Post to Finance'
            : decision?.isApproved
              ? 'Approve payment'
              : 'Reject payment'
        }
        cancelText="Cancel"
        variant={decision?.isApproved ? 'default' : 'destructive'}
        onConfirm={confirmDecision}
        isLoading={verifying}
      >
        <div className="space-y-3">
          <div className="space-y-2">
            <Label htmlFor="payment-decision-notes">
              Decision notes{decision?.isApproved ? ' (optional)' : ''}
            </Label>
            <Textarea
              id="payment-decision-notes"
              value={notes}
              onChange={(event) => {
                setNotes(event.target.value);
                if (decisionError) setDecisionError(null);
              }}
              placeholder={
                isFinanceRecovery
                  ? 'Add recovery notes for the audit record'
                  : decision?.isApproved
                    ? 'Add verification notes for the audit record'
                    : 'Enter the reason this payment is being rejected'
              }
              disabled={verifying}
            />
            {!decision?.isApproved && (
              <p className="text-xs text-gray-500">
                Required when rejecting a payment.
              </p>
            )}
          </div>
          {decisionError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Payment decision not completed</AlertTitle>
              <AlertDescription>{decisionError}</AlertDescription>
            </Alert>
          )}
        </div>
      </ConfirmationDialog>
    </>
  );
}

export default function BidDetailPage() {
  const { hasPermission } = useAuth();
  const params = useParams();
  const router = useRouter();
  const bidId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');

  const [bid, setBid] = useState<TenderBidDetailDto | null>(null);
  const [payments, setPayments] = useState<TenderPaymentDto[]>([]);
  const [template, setTemplate] = useState<EvaluationTemplate | null>(null);
  const [sourceTender, setSourceTender] = useState<TenderDetailDto | null>(
    null
  );
  const [evaluationRouteError, setEvaluationRouteError] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [opening, setOpening] = useState(false);
  const [showOpenDialog, setShowOpenDialog] = useState(false);
  const [activeTab, setActiveTab] = useState('overview');
  const canVetTenderBoq = isQsOptionalFeatureEnabled('tender-exchange') && hasPermission('quantity-survey.transactions.approve');
  const canVerifyTenderPayment = hasPermission(
    TENDER_PAYMENT_VERIFY_PERMISSION
  );
  const canAdministerTender = hasPermission('procurement.tender.administer');

  const refreshPayments = async (updatedPayment: TenderPaymentDto) => {
    setPayments((current) =>
      current.map((payment) =>
        payment.id === updatedPayment.id ? updatedPayment : payment
      )
    );
    const paymentsData = await tenderBidService.getBidPayments(bidId);
    setPayments(paymentsData);
  };

  useEffect(() => {
    const requestedTab = new URLSearchParams(window.location.search).get('tab');
    if (
      requestedTab &&
      [
        'overview',
        'items',
        'proposals',
        'documents',
        'payments',
        'qs-boq',
        'evaluation',
        'interviews',
      ].includes(requestedTab)
    ) {
      setActiveTab(requestedTab);
    }
  }, []);

  useEffect(() => {
    if (bidId) {
      loadBidDetails();
    }
  }, [bidId]);

  useEffect(() => {
    if (activeTab !== 'evaluation' || !sourceTender) return;
    const route = getTenderEvaluationRoute(sourceTender, bidId);
    if (route.mode === 'controlled') router.replace(route.evaluationHref);
  }, [activeTab, bidId, router, sourceTender]);

  const loadBidDetails = async () => {
    try {
      setLoading(true);
      const data = await tenderBidService.getBidById(bidId);
      setBid(data);

      try {
        setEvaluationRouteError(undefined);
        setSourceTender(await tenderService.getTenderById(data.tenderId));
      } catch (tenderError) {
        setSourceTender(null);
        setEvaluationRouteError(
          getProcurementProblemMessage(
            tenderError,
            'The tender evaluation route could not be determined.'
          )
        );
      }

      // Load evaluation template if assigned
      if (data.evaluationTemplateId) {
        try {
          const templateData = await evaluationTemplateService.getById(
            data.evaluationTemplateId
          );
          setTemplate(templateData);
        } catch (templateError) {
          console.error('Error loading evaluation template:', templateError);
        }
      }

      // Load payments
      const paymentsData = await tenderBidService.getBidPayments(bidId);
      setPayments(paymentsData);
    } catch (error) {
      console.error('Error loading bid details:', error);
      toast.error('Failed to load bid details');
    } finally {
      setLoading(false);
    }
  };

  const handleOpenBid = () => {
    if (!canAdministerTender) {
      toast.error('Tender administration permission is required to open bids.');
      return;
    }
    setShowOpenDialog(true);
  };

  const confirmOpenBid = async () => {
    if (!bid) return;
    if (!canAdministerTender) {
      toast.error('Tender administration permission is required to open bids.');
      return false;
    }

    try {
      setOpening(true);
      const updatedBid = await tenderBidService.openBid(bidId);
      setBid(updatedBid);
      setShowOpenDialog(false);
      toast.success('Bid marked as opened');
    } catch (error) {
      console.error('Error opening bid:', error);
      toast.error(getProcurementProblemMessage(error, 'Failed to open bid'));
      return false;
    } finally {
      setOpening(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<
      string,
      {
        variant: 'default' | 'secondary' | 'destructive' | 'outline';
        className: string;
      }
    > = {
      Submitted: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      Opened: {
        variant: 'default',
        className: 'bg-purple-100 text-purple-800',
      },
      UnderEvaluation: {
        variant: 'default',
        className: 'bg-yellow-100 text-yellow-800',
      },
      Accepted: {
        variant: 'default',
        className: 'bg-green-100 text-green-800',
      },
      Rejected: {
        variant: 'destructive',
        className: 'bg-red-100 text-red-800',
      },
      Withdrawn: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
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

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading bid details...</p>
        </div>
      </div>
    );
  }

  if (!bid) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Bid not found</p>
          <Button
            onClick={() => router.push('/procurement/bids')}
            className="mt-4"
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Bids
          </Button>
        </div>
      </div>
    );
  }

  const openBidConfirmation = (
    <ConfirmationDialog
      open={showOpenDialog && canAdministerTender}
      onOpenChange={setShowOpenDialog}
      title="Mark Bid as Opened"
      description="Are you sure you want to mark this bid as opened? The supplier will be notified that their bid has been opened."
      confirmText="Mark as Opened"
      cancelText="Cancel"
      variant="default"
      onConfirm={confirmOpenBid}
      isLoading={opening}
    />
  );

  if (bid.isSealed) {
    const openingTime = sourceTender?.openingDate ? Date.parse(sourceTender.openingDate) : NaN;
    const closingTime = sourceTender?.submissionDeadline ? Date.parse(sourceTender.submissionDeadline) : NaN;
    const canRequestOpening = canAdministerTender && bid.status === 'Submitted' &&
      sourceTender?.status === 'Closed' && sourceTender.usesControlledTenderLifecycle === false &&
      closingTime <= Date.now() && openingTime <= Date.now();
    return (
      <div className="container mx-auto py-6 space-y-6">
        <Button variant="outline" onClick={() => router.push(`/procurement/tenders/${bid.tenderId}`)}>
          <ArrowLeft className="h-4 w-4 mr-2" />Return to tender
        </Button>
        <Card>
          <CardHeader><CardTitle>Bid sealed</CardTitle></CardHeader>
          <CardContent>
            <p className="font-mono mb-2">{bid.bidNumber}</p>
            <p>Prices, proposals and attachments remain confidential until formal bid opening. Complete the committee, closing-time and quorum prerequisites from the tender process flow.</p>
            {canRequestOpening && (
              <Button className="mt-4" onClick={handleOpenBid} disabled={opening}>Open bid</Button>
            )}
          </CardContent>
        </Card>
        {openBidConfirmation}
      </div>
    );
  }

  const supportingDocuments = getSupportingDocuments(bid.documents);
  const evaluationRoute: TenderEvaluationRoute | undefined = sourceTender
    ? getTenderEvaluationRoute(sourceTender, bidId)
    : undefined;
  const handleTabChange = (value: string) => {
    if (value === 'evaluation' && evaluationRoute?.mode === 'controlled') {
      router.push(evaluationRoute.evaluationHref);
      return;
    }
    setActiveTab(value);
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            onClick={() =>
              router.push(
                `/procurement/bids?tenderId=${encodeURIComponent(bid.tenderId)}`
              )
            }
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{bid.businessPartnerName}</h1>
            <p className="text-gray-500">Bid #{bid.bidNumber}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {bid.status === 'Submitted' && canAdministerTender && (
            <Button onClick={handleOpenBid} disabled={opening}>
              <CheckCircle className="h-4 w-4 mr-2" />
              {opening ? 'Opening...' : 'Mark as Opened'}
            </Button>
          )}
          {getStatusBadge(bid.status)}
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Total Bid Amount
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">
              {bid.isFinancialProposalSealed ? 'Sealed' : formatCurrency(bid.totalBidAmount, bid.currency)}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Total Score
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">
              {bid.totalScore !== undefined && bid.totalScore !== null
                ? `${bid.totalScore.toFixed(2)}%`
                : 'Not Evaluated'}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Rank
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">
              {bid.rank ? `#${bid.rank}` : 'N/A'}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Compliance
            </CardTitle>
          </CardHeader>
          <CardContent>
            {bid.isCompliant ? (
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-6 w-6 text-green-500" />
                <span className="text-lg font-semibold text-green-700">
                  Compliant
                </span>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <XCircle className="h-6 w-6 text-red-500" />
                <span className="text-lg font-semibold text-red-700">
                  Non-Compliant
                </span>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        className="space-y-4"
      >
        <TabsList
          className={`grid w-full ${canVetTenderBoq ? 'grid-cols-8' : 'grid-cols-7'}`}
        >
          <TabsTrigger value="overview">
            <FileText className="h-4 w-4 mr-2" />
            Overview
          </TabsTrigger>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Lots (
            {new Set(bid.items?.map((item) => item.lotCode).filter(Boolean))
              .size || 0}
            )
          </TabsTrigger>
          <TabsTrigger value="proposals">
            <FileText className="h-4 w-4 mr-2" />
            Proposals
          </TabsTrigger>
          <TabsTrigger value="documents">
            <Upload className="h-4 w-4 mr-2" />
            Documents ({supportingDocuments.length})
          </TabsTrigger>
          <TabsTrigger value="payments">
            <DollarSign className="h-4 w-4 mr-2" />
            Payments ({payments.length})
          </TabsTrigger>
          {canVetTenderBoq && (
            <TabsTrigger value="qs-boq">
              <FileText className="h-4 w-4 mr-2" />
              QS BoQ
            </TabsTrigger>
          )}
          <TabsTrigger value="evaluation">
            <Award className="h-4 w-4 mr-2" />
            Evaluation ({bid.evaluations?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="interviews">
            <MessageSquare className="h-4 w-4 mr-2" />
            Interviews ({bid.interviews?.length || 0})
          </TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Bid Information</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <p className="text-sm text-gray-500">Bid Number</p>
                <p className="font-semibold">{bid.bidNumber}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Tender</p>
                <p className="font-semibold">{bid.tenderTitle}</p>
                <p className="text-sm text-gray-500">{bid.tenderNumber}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Business Partner</p>
                <p className="font-semibold">{bid.businessPartnerName}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Status</p>
                {getStatusBadge(bid.status)}
              </div>
              <div>
                <p className="text-sm text-gray-500">Total Bid Amount</p>
                <p className="font-semibold">
                  {bid.isFinancialProposalSealed ? 'Sealed' : formatCurrency(bid.totalBidAmount, bid.currency)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Submitted Date</p>
                <p className="font-semibold">{formatDate(bid.submittedDate)}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Delivery Days</p>
                <p className="font-semibold">
                  {bid.deliveryDays ? `${bid.deliveryDays} days` : 'N/A'}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Compliance Status</p>
                {bid.isCompliant ? (
                  <Badge
                    variant="default"
                    className="bg-green-100 text-green-800"
                  >
                    Compliant
                  </Badge>
                ) : (
                  <Badge variant="destructive">Non-Compliant</Badge>
                )}
              </div>
              <div>
                <p className="text-sm text-gray-500">Lots Bidded</p>
                <div className="flex flex-wrap gap-1 mt-1">
                  {[
                    ...new Set(
                      bid.items?.map((item) => item.lotCode).filter(Boolean)
                    ),
                  ].map((lotCode) => (
                    <Badge
                      key={lotCode}
                      variant="outline"
                      className="font-mono"
                    >
                      {lotCode}
                    </Badge>
                  ))}
                  {(!bid.items || bid.items.length === 0) && (
                    <span className="text-gray-500">None</span>
                  )}
                </div>
              </div>
              {bid.paymentTerms && (
                <div className="md:col-span-2">
                  <p className="text-sm text-gray-500">Payment Terms</p>
                  <p className="mt-1">{bid.paymentTerms}</p>
                </div>
              )}
              {bid.warrantyTerms && (
                <div className="md:col-span-2">
                  <p className="text-sm text-gray-500">Warranty Terms</p>
                  <p className="mt-1">{bid.warrantyTerms}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {bid.technicalProposal && (
            <Card>
              <CardHeader>
                <CardTitle>Technical Proposal</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap">{bid.technicalProposal}</p>
              </CardContent>
            </Card>
          )}

          {bid.commercialProposal && (
            <Card>
              <CardHeader>
                <CardTitle>Commercial Proposal</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap">{bid.commercialProposal}</p>
              </CardContent>
            </Card>
          )}

          {!bid.isCompliant && bid.nonComplianceReasons && (
            <Card className="border-red-200 bg-red-50">
              <CardHeader>
                <CardTitle className="text-red-800">
                  Non-Compliance Reasons
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-red-700">{bid.nonComplianceReasons}</p>
              </CardContent>
            </Card>
          )}

          {/* Evaluation Scores */}
          {template && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  Evaluation Scores
                  <Badge variant="outline">{template.templateName}</Badge>
                </CardTitle>
                <CardDescription>
                  Scoring Method: {template.scoringMethod} | Passing Score:{' '}
                  {template.passingScore}%
                  {bid.evaluations &&
                    bid.evaluations.filter((e) => e.status === 'Submitted')
                      .length > 0 && (
                      <span className="ml-2">
                        |{' '}
                        {
                          bid.evaluations.filter(
                            (e) => e.status === 'Submitted'
                          ).length
                        }{' '}
                        evaluator(s)
                      </span>
                    )}
                </CardDescription>
              </CardHeader>
              <CardContent>
                {(() => {
                  // Calculate average scores from all submitted evaluations
                  const submittedEvaluations =
                    bid.evaluations?.filter(
                      (e) =>
                        e.status === 'Submitted' && e.evaluationCriteriaJson
                    ) || [];
                  const aggregatedScores: Record<
                    string,
                    {
                      totalScore: number;
                      count: number;
                      maxScore: number;
                      weight: number;
                    }
                  > = {};

                  // Parse all evaluation criteria and aggregate
                  submittedEvaluations.forEach((evaluation) => {
                    try {
                      const scores: CriteriaScore[] = JSON.parse(
                        evaluation.evaluationCriteriaJson || '[]'
                      );
                      scores.forEach((score) => {
                        if (!aggregatedScores[score.criterionId]) {
                          aggregatedScores[score.criterionId] = {
                            totalScore: 0,
                            count: 0,
                            maxScore: score.maxScore,
                            weight: score.weight,
                          };
                        }
                        aggregatedScores[score.criterionId].totalScore +=
                          score.score;
                        aggregatedScores[score.criterionId].count += 1;
                      });
                    } catch (e) {
                      console.error('Error parsing evaluation criteria:', e);
                    }
                  });

                  const hasScores = Object.keys(aggregatedScores).length > 0;
                  const colors = [
                    'text-blue-600',
                    'text-green-600',
                    'text-orange-600',
                    'text-purple-600',
                    'text-red-600',
                    'text-cyan-600',
                    'text-pink-600',
                    'text-indigo-600',
                    'text-teal-600',
                  ];

                  return (
                    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
                      {template.criteria
                        ?.sort((a, b) => a.displayOrder - b.displayOrder)
                        .map((criterion, index) => {
                          const colorClass = colors[index % colors.length];
                          const scoreData =
                            aggregatedScores[criterion.evaluationCriterionId];
                          const avgScore = scoreData
                            ? scoreData.totalScore / scoreData.count
                            : null;

                          return (
                            <div
                              key={criterion.evaluationCriterionId}
                              className="p-3 bg-gray-50 rounded-lg"
                            >
                              <div className="flex items-center justify-between mb-1">
                                <p className="text-sm font-medium text-gray-700">
                                  {criterion.criterionName}
                                </p>
                                {criterion.isMandatory && (
                                  <Badge
                                    variant="destructive"
                                    className="text-xs"
                                  >
                                    Required
                                  </Badge>
                                )}
                              </div>
                              {avgScore !== null ? (
                                <div className="flex items-baseline gap-1 mb-1">
                                  <span
                                    className={`text-xl font-bold ${colorClass}`}
                                  >
                                    {avgScore.toFixed(1)}
                                  </span>
                                  <span className="text-sm text-gray-400">
                                    / {criterion.maxScore}
                                  </span>
                                </div>
                              ) : (
                                <p className="text-sm text-gray-400 mb-1">
                                  Not scored
                                </p>
                              )}
                              <div className="flex items-center justify-between">
                                <span className="text-xs text-gray-500">
                                  Weight: {criterion.weight}%
                                </span>
                                {avgScore !== null && (
                                  <span className="text-xs text-gray-500">
                                    {(
                                      (avgScore / criterion.maxScore) *
                                      criterion.weight
                                    ).toFixed(1)}
                                    % weighted
                                  </span>
                                )}
                              </div>
                            </div>
                          );
                        })}
                    </div>
                  );
                })()}
                {bid.totalScore !== undefined && bid.totalScore !== null && (
                  <div className="mt-4 pt-4 border-t flex items-center justify-between">
                    <span className="text-sm font-medium">Total Score</span>
                    <div className="flex items-center gap-2">
                      <span
                        className={`text-2xl font-bold ${
                          bid.totalScore >= template.passingScore
                            ? 'text-green-600'
                            : 'text-red-600'
                        }`}
                      >
                        {bid.totalScore.toFixed(2)}%
                      </span>
                      {bid.totalScore >= template.passingScore ? (
                        <Badge variant="default" className="bg-green-600">
                          Pass
                        </Badge>
                      ) : (
                        <Badge variant="destructive">Fail</Badge>
                      )}
                    </div>
                  </div>
                )}
                {bid.evaluatedByName && (
                  <div className="mt-4 pt-4 border-t">
                    <p className="text-sm text-gray-500">
                      Evaluated by{' '}
                      <span className="font-semibold">
                        {bid.evaluatedByName}
                      </span>{' '}
                      on {formatDate(bid.evaluatedDate)}
                    </p>
                    {bid.evaluationNotes && (
                      <p className="mt-2 text-sm">{bid.evaluationNotes}</p>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Fallback: Show message if no template but bid has been evaluated */}
          {!template && bid.evaluationTemplateId && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <AlertCircle className="h-5 w-5 text-yellow-500" />
                  Evaluation Template
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-yellow-700">
                  This tender has an evaluation template assigned but it could
                  not be loaded.
                </p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Lots Tab */}
        <TabsContent value="items" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Bid Lots</CardTitle>
              <CardDescription>
                {new Set(bid.items?.map((item) => item.lotCode).filter(Boolean))
                  .size || 0}{' '}
                lot(s), {bid.items?.length || 0} item(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!bid.items || bid.items.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No lots in this bid
                </p>
              ) : (
                <>
                  {/* Group items by lot */}
                  {(() => {
                    const lotGroups = bid.items.reduce(
                      (acc, item) => {
                        const lotCode = item.lotCode || 'Unassigned';
                        if (!acc[lotCode]) {
                          acc[lotCode] = [];
                        }
                        acc[lotCode].push(item);
                        return acc;
                      },
                      {} as Record<string, typeof bid.items>
                    );

                    return Object.entries(lotGroups).map(([lotCode, items]) => (
                      <div
                        key={lotCode}
                        className="mb-6 border rounded-lg overflow-hidden"
                      >
                        {/* Lot Header */}
                        <div className="bg-gray-50 px-4 py-3 border-b flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <Badge variant="outline" className="font-mono">
                              {lotCode}
                            </Badge>
                            <span className="text-sm text-gray-600">
                              {items.length} item(s)
                            </span>
                          </div>
                          <div className="font-bold text-green-600">
                            {formatCurrency(
                              items.reduce(
                                (sum, item) => sum + item.totalPrice,
                                0
                              ),
                              bid.currency
                            )}
                          </div>
                        </div>
                        {/* Lot Items */}
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>#</TableHead>
                              <TableHead>Item Description</TableHead>
                              <TableHead>Requested Qty</TableHead>
                              <TableHead>Offered Qty</TableHead>
                              <TableHead>Unit Price</TableHead>
                              <TableHead>Total Price</TableHead>
                              <TableHead>Brand/Model</TableHead>
                              <TableHead>Delivery Days</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {items.map((item, index) => (
                              <TableRow key={item.id}>
                                <TableCell>{index + 1}</TableCell>
                                <TableCell>
                                  <div>
                                    <p className="font-medium">
                                      {item.tenderItemDescription}
                                    </p>
                                    {item.specifications && (
                                      <p className="text-sm text-gray-500 whitespace-pre-wrap">
                                        {item.specifications}
                                      </p>
                                    )}
                                  </div>
                                </TableCell>
                                <TableCell>
                                  {item.requestedQuantity}
                                  {item.unitOfMeasure && (
                                    <span className="text-gray-500 text-sm ml-1">
                                      {item.unitOfMeasure}
                                    </span>
                                  )}
                                </TableCell>
                                <TableCell className="font-semibold">
                                  {item.offeredQuantity}
                                  {item.unitOfMeasure && (
                                    <span className="text-gray-500 text-sm ml-1">
                                      {item.unitOfMeasure}
                                    </span>
                                  )}
                                </TableCell>
                                <TableCell>
                                  {formatCurrency(item.unitPrice, bid.currency)}
                                </TableCell>
                                <TableCell className="font-semibold">
                                  {formatCurrency(
                                    item.totalPrice,
                                    bid.currency
                                  )}
                                </TableCell>
                                <TableCell>
                                  {item.brand || item.model
                                    ? `${item.brand || ''} ${item.model || ''}`.trim()
                                    : '-'}
                                </TableCell>
                                <TableCell>
                                  {item.deliveryDays
                                    ? `${item.deliveryDays} days`
                                    : '-'}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                    ));
                  })()}

                  <div className="flex justify-end">
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 min-w-64 max-w-md">
                      <div className="flex items-center justify-between gap-4">
                        <span className="text-lg font-medium whitespace-nowrap">
                          Total Bid Amount:
                        </span>
                        <span className="text-2xl font-bold text-blue-600 break-all text-right">
                          {bid.isFinancialProposalSealed ? 'Sealed' : formatCurrency(bid.totalBidAmount, bid.currency)}
                        </span>
                      </div>
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Proposals Tab */}
        <TabsContent value="proposals" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Technical Proposal</CardTitle>
            </CardHeader>
            <CardContent>
              {bid.status === 'Submitted' ? (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-yellow-800">
                    <Clock className="h-5 w-5" />
                    <span className="font-medium">
                      Documents will be available after the bid is opened
                    </span>
                  </div>
                </div>
              ) : bid.documents?.find(
                  (doc) => doc.documentType === 'TechnicalProposal'
                ) ? (
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle className="h-5 w-5 text-green-600" />
                    <span className="font-medium text-green-700">
                      Technical Proposal Uploaded
                    </span>
                  </div>
                  {(() => {
                    const doc = bid.documents.find(
                      (doc) => doc.documentType === 'TechnicalProposal'
                    );
                    return doc ? (
                      <div className="bg-gray-50 border rounded-lg p-4">
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <p className="font-medium">{doc.documentName}</p>
                            <p className="text-sm text-gray-500 font-mono">
                              {doc.fileName || doc.documentName}
                            </p>
                            <p className="text-sm text-gray-500 mt-1">
                              Uploaded:{' '}
                              {formatDate(doc.uploadedAt || doc.uploadedDate)} •
                              Size: {((doc.fileSize ?? 0) / 1024).toFixed(2)} KB
                            </p>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() =>
                              tenderBidService.downloadBidDocument(
                                bid.id,
                                doc.id,
                                doc.documentName
                              )
                            }
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    ) : null;
                  })()}
                </div>
              ) : bid.technicalProposal ? (
                <div className="bg-gray-50 border rounded-lg p-4">
                  <p className="whitespace-pre-wrap">{bid.technicalProposal}</p>
                </div>
              ) : (
                <p className="text-center py-8 text-gray-500">
                  No technical proposal provided
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Commercial Proposal</CardTitle>
            </CardHeader>
            <CardContent>
              {bid.status === 'Submitted' ? (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-yellow-800">
                    <Clock className="h-5 w-5" />
                    <span className="font-medium">
                      Documents will be available after the bid is opened
                    </span>
                  </div>
                </div>
              ) : bid.documents?.find(
                  (doc) => doc.documentType === 'CommercialProposal'
                ) ? (
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle className="h-5 w-5 text-green-600" />
                    <span className="font-medium text-green-700">
                      Commercial Proposal Uploaded
                    </span>
                  </div>
                  {(() => {
                    const doc = bid.documents.find(
                      (doc) => doc.documentType === 'CommercialProposal'
                    );
                    return doc ? (
                      <div className="bg-gray-50 border rounded-lg p-4">
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <p className="font-medium">{doc.documentName}</p>
                            <p className="text-sm text-gray-500 font-mono">
                              {doc.fileName || doc.documentName}
                            </p>
                            <p className="text-sm text-gray-500 mt-1">
                              Uploaded:{' '}
                              {formatDate(doc.uploadedAt || doc.uploadedDate)} •
                              Size: {((doc.fileSize ?? 0) / 1024).toFixed(2)} KB
                            </p>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() =>
                              tenderBidService.downloadBidDocument(
                                bid.id,
                                doc.id,
                                doc.documentName
                              )
                            }
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    ) : null;
                  })()}
                </div>
              ) : bid.commercialProposal ? (
                <div className="bg-gray-50 border rounded-lg p-4">
                  <p className="whitespace-pre-wrap">
                    {bid.commercialProposal}
                  </p>
                </div>
              ) : (
                <p className="text-center py-8 text-gray-500">
                  No commercial proposal provided
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {canVetTenderBoq && (
          <TabsContent value="qs-boq" className="space-y-4">
            <QuantitySurveyTenderBoqVettingPanel tenderBidId={bidId} />
          </TabsContent>
        )}

        {/* Documents Tab */}
        <TabsContent value="documents" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Supporting Documents</CardTitle>
              <CardDescription>
                {supportingDocuments.length} supporting file(s) uploaded. Technical and commercial proposals are shown separately.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {bid.status !== 'Submitted' && <BidDocumentRequirements bid={bid} tender={sourceTender} />}
              {bid.status === 'Submitted' ? (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-yellow-800">
                    <Clock className="h-5 w-5" />
                    <span className="font-medium">
                      Documents will be available after the bid is opened
                    </span>
                  </div>
                </div>
              ) : !supportingDocuments.length ? (
                <p className="text-center py-8 text-gray-500">
                  No supporting files uploaded
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Document Type</TableHead>
                      <TableHead>Document Name</TableHead>
                      <TableHead>File Size</TableHead>
                      <TableHead>Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {supportingDocuments.map((doc) => (
                        <TableRow key={doc.id}>
                          <TableCell>
                            <Badge variant="outline">{doc.documentType}</Badge>
                          </TableCell>
                          <TableCell>{doc.documentName}</TableCell>
                          <TableCell>
                            {((doc.fileSize ?? 0) / 1024).toFixed(2)} KB
                          </TableCell>
                          <TableCell>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() =>
                                tenderBidService.downloadBidDocument(
                                  bid.id,
                                  doc.id,
                                  doc.documentName
                                )
                              }
                            >
                              <Download className="h-4 w-4 mr-2" />
                              Download
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

        {/* Tender Fee Payments Tab */}
        <TabsContent value="payments" className="space-y-4">
          <TenderPaymentVerificationPanel
            bidId={bidId}
            payments={payments}
            canVerifyPayment={canVerifyTenderPayment}
            onRefresh={refreshPayments}
          />
        </TabsContent>

        {/* Evaluation Tab */}
        <TabsContent value="evaluation" className="space-y-4">
          {evaluationRoute?.mode === 'controlled' ? (
            <Card className="border-blue-200 bg-blue-50/40">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <ShieldCheck className="h-5 w-5 text-blue-700" />
                  Controlled committee evaluation
                </CardTitle>
                <CardDescription>
                  Open the signed tender evaluation workspace to score this bid.
                  All bids remain side by side in one controlled committee
                  record so the comparison and audit history stay complete.
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  onClick={() => router.push(evaluationRoute.committeeHref)}
                >
                  <Users className="mr-2 h-4 w-4" />
                  Committee controls
                </Button>
                <Button
                  onClick={() => router.push(evaluationRoute.evaluationHref)}
                >
                  <ShieldCheck className="mr-2 h-4 w-4" />
                  {evaluationRoute.evaluationLabel}
                </Button>
              </CardContent>
            </Card>
          ) : evaluationRoute?.mode === 'legacy' ? (
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Evaluations</CardTitle>
                    <CardDescription>
                      {bid.evaluations?.length || 0} evaluation(s)
                    </CardDescription>
                  </div>
                  <Button
                    onClick={() =>
                      router.push(
                        `/procurement/evaluations/create?bidId=${bidId}`
                      )
                    }
                    className="gap-2"
                  >
                    <Award className="h-4 w-4" />
                    Create Evaluation
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                {!bid.evaluations || bid.evaluations.length === 0 ? (
                  <div className="text-center py-8">
                    <Award className="h-12 w-12 mx-auto mb-4 text-gray-300" />
                    <p className="text-gray-500 mb-4">No evaluations yet</p>
                    <Button
                      onClick={() =>
                        router.push(
                          `/procurement/evaluations/create?bidId=${bidId}`
                        )
                      }
                      variant="outline"
                    >
                      <Award className="h-4 w-4 mr-2" />
                      Create First Evaluation
                    </Button>
                  </div>
                ) : (
                  <div className="space-y-2">
                    {bid.evaluations.map((evaluation) => (
                      <Card key={evaluation.id} className="border">
                        <CardHeader className="pb-2 pt-3 px-4">
                          <div className="flex items-center justify-between gap-2">
                            <div className="flex-1 min-w-0">
                              <CardTitle className="text-sm font-semibold">
                                {evaluation.evaluatorName ||
                                  'Unknown Evaluator'}
                              </CardTitle>
                              <CardDescription className="text-xs">
                                {formatDate(evaluation.evaluationDate)}
                              </CardDescription>
                            </div>
                            <div className="flex items-center gap-1 flex-shrink-0">
                              <Badge
                                variant={
                                  evaluation.status === 'Submitted'
                                    ? 'default'
                                    : 'outline'
                                }
                                className={`text-xs ${evaluation.status === 'Submitted' ? 'bg-green-600' : ''}`}
                              >
                                {evaluation.status === 'Submitted'
                                  ? 'Completed'
                                  : evaluation.status}
                              </Badge>
                              <Button
                                variant="ghost"
                                size="sm"
                                className="h-7 px-2 text-xs"
                                onClick={() =>
                                  router.push(
                                    `/procurement/evaluations/${evaluation.id}`
                                  )
                                }
                              >
                                View
                              </Button>
                            </div>
                          </div>
                        </CardHeader>
                        <CardContent className="pt-2 pb-3 px-4 space-y-2">
                          {/* Dynamic Criteria Scores */}
                          {evaluation.evaluationCriteriaJson ? (
                            (() => {
                              try {
                                const criteriaScores: CriteriaScore[] =
                                  JSON.parse(evaluation.evaluationCriteriaJson);
                                const colors = [
                                  'text-blue-600',
                                  'text-green-600',
                                  'text-orange-600',
                                  'text-purple-600',
                                  'text-red-600',
                                  'text-cyan-600',
                                  'text-pink-600',
                                  'text-indigo-600',
                                  'text-teal-600',
                                ];
                                return (
                                  <div className="grid grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2">
                                    {criteriaScores.map((criteria, index) => (
                                      <div key={criteria.criterionId}>
                                        <p className="text-xs text-gray-500">
                                          {criteria.criterionName}
                                        </p>
                                        <div className="flex items-baseline gap-1">
                                          <p
                                            className={`text-sm font-bold ${colors[index % colors.length]}`}
                                          >
                                            {criteria.score}
                                          </p>
                                          <span className="text-xs text-gray-400">
                                            / {criteria.maxScore}
                                          </span>
                                        </div>
                                        <p className="text-xs text-gray-400">
                                          Weighted:{' '}
                                          {criteria.weightedScore.toFixed(1)}%
                                        </p>
                                      </div>
                                    ))}
                                    <div className="border-l pl-2">
                                      <p className="text-xs text-gray-500 font-medium">
                                        Total
                                      </p>
                                      <p className="text-sm font-bold text-indigo-600">
                                        {evaluation.totalScore !== undefined &&
                                        evaluation.totalScore !== null
                                          ? `${evaluation.totalScore.toFixed(1)}%`
                                          : criteriaScores
                                              .reduce(
                                                (sum, c) =>
                                                  sum + c.weightedScore,
                                                0
                                              )
                                              .toFixed(1) + '%'}
                                      </p>
                                    </div>
                                  </div>
                                );
                              } catch {
                                return (
                                  <p className="text-xs text-gray-500">
                                    Could not parse evaluation criteria
                                  </p>
                                );
                              }
                            })()
                          ) : (
                            <p className="text-xs text-gray-500">
                              No detailed criteria scores available
                            </p>
                          )}
                          {(evaluation.technicalComments ||
                            evaluation.commercialComments ||
                            evaluation.recommendation) && (
                            <div className="text-xs space-y-1 pt-1 border-t">
                              {evaluation.technicalComments && (
                                <div>
                                  <p className="font-medium text-gray-600">
                                    Technical:
                                  </p>
                                  <p className="text-gray-700 line-clamp-2">
                                    {evaluation.technicalComments}
                                  </p>
                                </div>
                              )}
                              {evaluation.commercialComments && (
                                <div>
                                  <p className="font-medium text-gray-600">
                                    Commercial:
                                  </p>
                                  <p className="text-gray-700 line-clamp-2">
                                    {evaluation.commercialComments}
                                  </p>
                                </div>
                              )}
                              {evaluation.recommendation && (
                                <div className="bg-blue-50 p-2 rounded">
                                  <p className="font-medium text-blue-900">
                                    Recommendation:
                                  </p>
                                  <p className="text-blue-800 line-clamp-2">
                                    {evaluation.recommendation}
                                  </p>
                                </div>
                              )}
                            </div>
                          )}
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          ) : (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Evaluation route unavailable</AlertTitle>
              <AlertDescription className="space-y-3">
                <p>
                  {evaluationRouteError ??
                    'The tender evaluation route could not be determined.'}
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    router.push(`/procurement/tenders/${bid.tenderId}`)
                  }
                >
                  Return to tender
                </Button>
              </AlertDescription>
            </Alert>
          )}
        </TabsContent>

        {/* Interviews Tab */}
        <TabsContent value="interviews" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Interviews</CardTitle>
              <CardDescription>
                {bid.interviews?.length || 0} interview(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!bid.interviews || bid.interviews.length === 0 ? (
                <p className="text-center py-8 text-gray-500">
                  No interviews scheduled
                </p>
              ) : (
                <div className="space-y-4">
                  {bid.interviews.map((interview) => (
                    <Card key={interview.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div>
                            <CardTitle className="text-base">
                              Interview
                            </CardTitle>
                            <CardDescription>
                              {formatDate(interview.interviewDate)}
                            </CardDescription>
                          </div>
                          <Badge
                            variant={
                              interview.status === 'Completed'
                                ? 'default'
                                : 'outline'
                            }
                          >
                            {interview.status}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-2">
                        {interview.location && (
                          <div>
                            <p className="text-sm text-gray-500">Location</p>
                            <p className="font-medium">{interview.location}</p>
                          </div>
                        )}
                        {interview.interviewerNames && (
                          <div>
                            <p className="text-sm text-gray-500">
                              Interviewers
                            </p>
                            <p className="font-medium">
                              {interview.interviewerNames}
                            </p>
                          </div>
                        )}
                        {interview.notes && (
                          <div>
                            <p className="text-sm text-gray-500">Notes</p>
                            <p className="text-sm mt-1">{interview.notes}</p>
                          </div>
                        )}
                        {interview.outcome && (
                          <div className="bg-blue-50 p-3 rounded-lg">
                            <p className="text-sm font-medium text-blue-900">
                              Outcome:
                            </p>
                            <p className="text-sm mt-1 text-blue-800">
                              {interview.outcome}
                            </p>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Open Bid Confirmation Dialog */}
      {openBidConfirmation}
    </div>
  );
}
