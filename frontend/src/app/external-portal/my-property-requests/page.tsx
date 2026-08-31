'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import React from 'react';
import {
  Archive,
  ArrowLeft,
  Building2,
  CalendarDays,
  CheckCircle2,
  ClipboardList,
  Download,
  Eye,
  FileSignature,
  Loader2,
  Send,
  Upload,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  externalEstateServicesService,
  type ExternalEstateRequestDocument,
  type ExternalEstateServiceRequest,
} from '@/services/external-estate-services.service';

const PROPERTY_LISTING_SOURCE = 'External Portal - Estate Listings';
const REQUESTS_PER_PAGE = 10;

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? 'Not recorded'
    : date.toLocaleDateString();
}

function requestTypeLabel(request: ExternalEstateServiceRequest) {
  if (request.title.toLowerCase().startsWith('purchase bid')) {
    return 'Purchase bid';
  }

  if (
    request.title.toLowerCase().startsWith('lease request') ||
    request.title.toLowerCase().startsWith('rental request')
  ) {
    return 'Rental request';
  }

  return 'Property request';
}

function isRentalRequest(request: ExternalEstateServiceRequest) {
  const requestType = fieldValue(request, 'requestType').toLowerCase();
  return (
    requestType.includes('rent') ||
    requestType.includes('lease') ||
    request.title.toLowerCase().startsWith('lease request') ||
    request.title.toLowerCase().startsWith('rental request')
  );
}

function fieldValue(
  request: ExternalEstateServiceRequest,
  key: string
): string {
  return request.fieldValues?.[key] || '';
}

function isApprovedStatus(value: string) {
  const normalized = value.trim().toLowerCase();
  return normalized === 'approved' || normalized.startsWith('approved ');
}

function isApprovedForCustomerAction(request: ExternalEstateServiceRequest) {
  return (
    !isListingUnavailable(request) &&
    isApprovedStatus(fieldValue(request, 'decisionStatus')) &&
    Boolean(fieldValue(request, 'generatedAgreementReference')) &&
    (!isRentalRequest(request) || Boolean(fieldValue(request, 'moveInDate'))) &&
    ['pending', ''].includes(
      fieldValue(request, 'customerAcceptanceStatus').toLowerCase()
    )
  );
}

function canUploadSignedAgreement(request: ExternalEstateServiceRequest) {
  return (
    !isListingUnavailable(request) &&
    isApprovedStatus(fieldValue(request, 'decisionStatus')) &&
    fieldValue(request, 'customerAcceptanceStatus').toLowerCase() ===
      'accepted' &&
    Boolean(fieldValue(request, 'generatedAgreementReference')) &&
    (!isRentalRequest(request) || Boolean(fieldValue(request, 'moveInDate'))) &&
    !fieldValue(request, 'signedAgreementReference')
  );
}

function canDownloadGeneratedAgreement(request: ExternalEstateServiceRequest) {
  return (
    !isListingUnavailable(request) &&
    isApprovedStatus(fieldValue(request, 'decisionStatus')) &&
    Boolean(fieldValue(request, 'generatedAgreementReference'))
  );
}

function isFullyExecuted(request: ExternalEstateServiceRequest) {
  return (
    fieldValue(request, 'agreementExecutionStatus').toLowerCase() ===
      'fully executed' ||
    Boolean(fieldValue(request, 'finalSignedAgreementReference'))
  );
}

function workflowStatusLabel(request: ExternalEstateServiceRequest) {
  return (
    fieldValue(request, 'applicationStatus') ||
    fieldValue(request, 'customerAcceptanceStatus') ||
    request.status
  );
}

function isListingUnavailableStatus(value: string) {
  const normalized = value.trim().toLowerCase();
  return (
    normalized.includes('sold out') ||
    normalized.includes('rented out') ||
    normalized.includes('listing unavailable')
  );
}

function listingOutcomeMessage(request: ExternalEstateServiceRequest) {
  const explicitMessage = fieldValue(request, 'listingOutcomeMessage');
  if (explicitMessage) return explicitMessage;

  const status = workflowStatusLabel(request);
  if (status.toLowerCase().includes('sold out')) {
    return 'This listing has been sold because another customer accepted the approved purchase request.';
  }

  if (status.toLowerCase().includes('rented out')) {
    return 'This listing has been rented out because another customer accepted the approved rental request.';
  }

  return '';
}

function isListingUnavailable(request: ExternalEstateServiceRequest) {
  return (
    Boolean(listingOutcomeMessage(request)) ||
    isListingUnavailableStatus(request.status) ||
    isListingUnavailableStatus(request.currentStageName)
  );
}

function canUploadIntakeDocuments(request: ExternalEstateServiceRequest) {
  const firstInternalStageRoutedForward =
    request.customerIntakeUploadClosed || request.currentStageIndex > 0;
  return !firstInternalStageRoutedForward && !isListingUnavailable(request);
}

interface PropertyRequestsViewProps {
  selectedRequestId?: string;
}

export function PropertyRequestsView({
  selectedRequestId,
}: PropertyRequestsViewProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [requests, setRequests] = React.useState<
    ExternalEstateServiceRequest[]
  >([]);
  const [requestPage, setRequestPage] = React.useState(1);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [actionNotes, setActionNotes] = React.useState<Record<string, string>>(
    {}
  );
  const [uploadNotes, setUploadNotes] = React.useState<Record<string, string>>(
    {}
  );
  const [clarificationResponses, setClarificationResponses] = React.useState<
    Record<string, string>
  >({});
  const [selectedFiles, setSelectedFiles] = React.useState<
    Record<string, File | null>
  >({});
  const [intakeFiles, setIntakeFiles] = React.useState<
    Record<string, File | null>
  >({});
  const [previewRequest, setPreviewRequest] =
    React.useState<ExternalEstateServiceRequest | null>(null);

  const downloadGeneratedAgreement = async (
    request: ExternalEstateServiceRequest
  ) => {
    setIsSaving(true);
    setError(null);
    try {
      const blob =
        await externalEstateServicesService.downloadGeneratedAgreement(
          request.id
        );
      if (!blob.type.toLowerCase().includes('pdf')) {
        throw new Error('The agreement download was not returned as a PDF.');
      }
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${
        request.referenceNumber ||
        fieldValue(request, 'applicationReference') ||
        'agreement'
      }-agreement.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(
        downloadError instanceof Error
          ? downloadError.message
          : 'Could not download the agreement.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const replaceRequest = (updated: ExternalEstateServiceRequest) => {
    setRequests((current) =>
      current.map((request) => (request.id === updated.id ? updated : request))
    );
  };

  const submitDecision = async (
    request: ExternalEstateServiceRequest,
    decision: 'Accept' | 'Reject'
  ) => {
    setIsSaving(true);
    setError(null);
    try {
      const updated =
        await externalEstateServicesService.submitPropertyRequestDecision(
          request.id,
          {
            decision,
            notes: actionNotes[request.id]?.trim() || null,
          }
        );
      replaceRequest(updated);
      setActionNotes((current) => ({ ...current, [request.id]: '' }));
    } catch (decisionError) {
      setError(
        decisionError instanceof Error
          ? decisionError.message
          : 'Could not update your customer decision.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const submitClarification = async (
    request: ExternalEstateServiceRequest
  ) => {
    const responseText = clarificationResponses[request.id]?.trim();
    if (!responseText) {
      setError('Enter your clarification response before submitting.');
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated =
        await externalEstateServicesService.submitClarificationResponse(
          request.id,
          responseText
        );
      replaceRequest(updated);
      setClarificationResponses((current) => ({
        ...current,
        [request.id]: '',
      }));
      toast({
        title: 'Clarification submitted',
        description: 'Your response was returned to the current review stage.',
        variant: 'success',
      });
    } catch (clarificationError) {
      setError(
        clarificationError instanceof Error
          ? clarificationError.message
          : 'Could not submit your clarification response.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const uploadSignedAgreement = async (
    request: ExternalEstateServiceRequest
  ) => {
    const file = selectedFiles[request.id];
    if (!file) {
      setError('Select the signed agreement document before uploading.');
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      const updated = await externalEstateServicesService.uploadSignedAgreement(
        request.id,
        file,
        uploadNotes[request.id]?.trim() || null
      );
      replaceRequest(updated);
      setSelectedFiles((current) => ({ ...current, [request.id]: null }));
      setUploadNotes((current) => ({ ...current, [request.id]: '' }));
    } catch (uploadError) {
      setError(
        uploadError instanceof Error
          ? uploadError.message
          : 'Could not upload the signed agreement.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const uploadIntakeDocument = async (
    request: ExternalEstateServiceRequest,
    document: ExternalEstateRequestDocument
  ) => {
    const file = intakeFiles[document.id];
    if (!file) return;

    setIsSaving(true);
    setError(null);
    try {
      await externalEstateServicesService.uploadCustomerIntakeDocument(
        request.id,
        document.id,
        file
      );
      const submitted = await externalEstateServicesService.getMyRequests();
      setRequests(
        submitted.filter(
          (item) => item.sourceDepartment === PROPERTY_LISTING_SOURCE
        )
      );
      setIntakeFiles((current) => ({ ...current, [document.id]: null }));
      toast({
        title: 'Document uploaded',
        description: `${document.name} was attached to the property request.`,
        variant: 'success',
      });
    } catch (uploadError) {
      setError(
        uploadError instanceof Error
          ? uploadError.message
          : 'Could not upload the supporting document.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const submitted = await externalEstateServicesService.getMyRequests();
        if (mounted) {
          setRequests(
            submitted.filter(
              (request) => request.sourceDepartment === PROPERTY_LISTING_SOURCE
            )
          );
        }
      } catch {
        if (mounted) {
          setError('Could not load your property bids and rental requests.');
        }
      } finally {
        if (mounted) setIsLoading(false);
      }
    };

    void load();
    return () => {
      mounted = false;
    };
  }, []);

  const visibleRequests = selectedRequestId
    ? requests.filter((request) => request.id === selectedRequestId)
    : requests;
  const requestTotalPages = Math.max(
    1,
    Math.ceil(visibleRequests.length / REQUESTS_PER_PAGE)
  );
  const pagedVisibleRequests = selectedRequestId
    ? visibleRequests
    : visibleRequests.slice(
        (requestPage - 1) * REQUESTS_PER_PAGE,
        requestPage * REQUESTS_PER_PAGE
      );

  React.useEffect(() => {
    setRequestPage((current) => Math.min(current, requestTotalPages));
  }, [requestTotalPages]);

  const goBack = () => {
    if (window.history.length > 1) {
      router.back();
      return;
    }
    router.push('/external-portal/my-properties');
  };

  return (
    <>
      <CentralDocumentViewerDialog
        open={Boolean(previewRequest)}
        onOpenChange={(open) => {
          if (!open) setPreviewRequest(null);
        }}
        enableAnnotations={false}
        file={
          previewRequest
            ? {
                title: `${
                  previewRequest.referenceNumber || 'Property request'
                } ${isFullyExecuted(previewRequest) ? 'final signed agreement' : 'agreement'}`,
                fileName: `${
                  previewRequest.referenceNumber || 'agreement'
                }-agreement.pdf`,
                renditionPath: `/api/estate/external/requests/${encodeURIComponent(
                  previewRequest.id
                )}/agreement`,
                contentType: 'application/pdf',
                sourceLabel: isFullyExecuted(previewRequest)
                  ? 'Final signed property agreement'
                  : 'Approved property agreement',
              }
            : null
        }
      />
      <div className="space-y-6">
        {selectedRequestId ? (
          <Button
            variant="outline"
            className="border-slate-300 bg-white text-slate-950 shadow-sm hover:bg-slate-100 hover:text-slate-950"
            onClick={goBack}
          >
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to My Properties
          </Button>
        ) : null}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">
              {selectedRequestId ? 'Property Request Details' : 'My Property Requests'}
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Track purchase bids and rental requests submitted from Property
              Listings.
            </p>
          </div>
          <Button asChild>
            <Link href="/external-portal/property-listings">
              <Building2 className="mr-2 h-4 w-4" />
              Browse properties
            </Link>
          </Button>
        </div>

        {error ? (
          <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            {error}
          </div>
        ) : null}

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between gap-3 text-base">
              <span className="flex items-center gap-2">
                <ClipboardList className="h-5 w-5 text-blue-600" />
                {selectedRequestId ? 'Selected request' : 'Submitted requests'}
              </span>
              <Badge variant="outline">
                {isLoading ? 'Loading' : visibleRequests.length}
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {isLoading ? (
              <div className="flex items-center justify-center gap-2 py-12 text-sm text-slate-500">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading property requests
              </div>
            ) : null}

            {!isLoading && visibleRequests.length === 0 ? (
              <div className="rounded-md border border-dashed p-10 text-center text-sm text-slate-500">
                {selectedRequestId
                  ? 'This property request was not found on your customer account.'
                  : 'You have not submitted a purchase bid or rental request yet.'}
              </div>
            ) : null}

            {pagedVisibleRequests.map((request) => (
              <div
                key={request.id}
                className="rounded-md border bg-white p-4 text-sm"
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="font-medium text-slate-900">
                      {request.referenceNumber || request.title}
                    </div>
                    <div className="mt-1 text-slate-600">{request.title}</div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant="outline">{requestTypeLabel(request)}</Badge>
                    <Badge variant="secondary">
                      {workflowStatusLabel(request)}
                    </Badge>
                  </div>
                </div>
                <div className="mt-4 grid gap-2 text-xs text-slate-600 sm:grid-cols-3">
                  <div>
                    <span className="text-slate-400">Current stage</span>
                    <div className="mt-1 font-medium text-slate-700">
                      {request.currentStageName}
                    </div>
                  </div>
                  <div>
                    <span className="text-slate-400">Assigned to</span>
                    <div className="mt-1 font-medium text-slate-700">
                      {request.currentAssignedRole || 'Awaiting assignment'}
                    </div>
                  </div>
                  <div>
                    <span className="text-slate-400">Decision</span>
                    <div className="mt-1 font-medium text-slate-700">
                      {fieldValue(request, 'decisionStatus') ||
                        'Pending review'}
                    </div>
                  </div>
                  <div>
                    <span className="text-slate-400">Customer response</span>
                    <div className="mt-1 font-medium text-slate-700">
                      {fieldValue(request, 'customerAcceptanceStatus') ||
                        'Pending'}
                    </div>
                  </div>
                  <div>
                    <span className="text-slate-400">Agreement</span>
                    <div className="mt-1 font-medium text-slate-700">
                      {fieldValue(request, 'finalSignedAgreementReference') ||
                        fieldValue(request, 'signedAgreementReference') ||
                        fieldValue(request, 'generatedAgreementReference') ||
                        'Not ready'}
                    </div>
                  </div>
                  {isRentalRequest(request) ? (
                    <div>
                      <span className="text-slate-400">
                        Agreement move-in / billing start
                      </span>
                      <div className="mt-1 font-medium text-slate-700">
                        {isFullyExecuted(request)
                          ? fieldValue(request, 'billingStartDate') ||
                            fieldValue(request, 'moveInDate') ||
                            'Pending'
                          : fieldValue(request, 'moveInDate')
                            ? `${fieldValue(request, 'moveInDate')} (planned)`
                            : 'Pending'}
                      </div>
                    </div>
                  ) : null}
                  {isRentalRequest(request) && fieldValue(request, 'actualPossessionDate') ? (
                    <div>
                      <span className="text-slate-400">Actual possession</span>
                      <div className="mt-1 font-medium text-slate-700">
                        {fieldValue(request, 'actualPossessionDate')}
                      </div>
                    </div>
                  ) : null}
                  <div>
                    <span className="flex items-center gap-1 text-slate-400">
                      <CalendarDays className="h-3.5 w-3.5" /> Submitted
                    </span>
                    <div className="mt-1 font-medium text-slate-700">
                      {formatDate(request.createdAt)}
                    </div>
                  </div>
                </div>

                {listingOutcomeMessage(request) ? (
                  <div className="mt-4 rounded-md border border-amber-200 bg-amber-50 p-4">
                    <div className="flex items-center gap-2 font-medium text-amber-900">
                      <Archive className="h-4 w-4" />
                      Listing unavailable
                    </div>
                    <p className="mt-1 text-sm text-amber-800">
                      {listingOutcomeMessage(request)} This request has been
                      archived.
                    </p>
                  </div>
                ) : null}

                {selectedRequestId && request.documents?.length ? (
                  <div className="mt-4 rounded-md border border-slate-200 bg-slate-50 p-4">
                    <div className="flex items-center gap-2 font-medium text-slate-900">
                      <Upload className="h-4 w-4" />
                      Supporting documents
                    </div>
                    <p className="mt-1 text-sm text-slate-600">
                      {canUploadIntakeDocuments(request)
                        ? 'Upload any required application document that is still missing.'
                        : 'Document upload closes once the first internal stage is routed forward.'}
                    </p>
                    <div className="mt-3 space-y-3">
                      {request.documents.map((document) => (
                        <div
                          key={document.id}
                          className="rounded-md border bg-white p-3"
                        >
                          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                            <div className="min-w-0">
                              <p className="text-sm font-medium text-slate-900">
                                {document.name}{document.isMandatory ? ' *' : ''}
                              </p>
                              <p className="mt-1 text-xs text-slate-500">
                                {document.fileName || 'File not attached'}
                              </p>
                            </div>
                            {document.fileName ? (
                              <Badge variant="secondary">Uploaded</Badge>
                            ) : !canUploadIntakeDocuments(request) ? (
                              <Badge variant="outline">Closed</Badge>
                            ) : (
                              <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row">
                                <Input
                                  type="file"
                                  accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"
                                  className="bg-white sm:w-72"
                                  onChange={(event) =>
                                    setIntakeFiles((current) => ({
                                      ...current,
                                      [document.id]: event.target.files?.[0] || null,
                                    }))
                                  }
                                />
                                <Button
                                  type="button"
                                  disabled={isSaving || !intakeFiles[document.id]}
                                  onClick={() =>
                                    void uploadIntakeDocument(request, document)
                                  }
                                >
                                  <Upload className="mr-2 h-4 w-4" />
                                  Upload
                                </Button>
                              </div>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : null}

                {request.status.toLowerCase() === 'clarification required' ? (
                  <div className="mt-4 rounded-md border border-amber-200 bg-amber-50 p-4">
                    <div className="flex items-center gap-2 font-medium text-amber-900">
                      <ClipboardList className="h-4 w-4" />
                      Clarification required
                    </div>
                    <p className="mt-1 text-sm text-amber-800">
                      {fieldValue(request, 'clarificationReason') ||
                        'The review team needs additional information.'}
                    </p>
                    <Textarea
                      className="mt-3 bg-white"
                      value={clarificationResponses[request.id] || ''}
                      onChange={(event) =>
                        setClarificationResponses((current) => ({
                          ...current,
                          [request.id]: event.target.value,
                        }))
                      }
                      placeholder="Enter your clarification response"
                    />
                    <Button
                      type="button"
                      className="mt-3"
                      disabled={
                        isSaving ||
                        !clarificationResponses[request.id]?.trim()
                      }
                      onClick={() => void submitClarification(request)}
                    >
                      <Send className="mr-2 h-4 w-4" />
                      Submit clarification
                    </Button>
                  </div>
                ) : null}

                {isApprovedForCustomerAction(request) ? (
                  <div className="mt-4 rounded-md border border-emerald-200 bg-emerald-50 p-4">
                    <div className="flex items-center gap-2 font-medium text-emerald-900">
                      <CheckCircle2 className="h-4 w-4" />
                      Approved - customer response required
                    </div>
                    <p className="mt-1 text-sm text-emerald-800">
                      Accept to continue agreement signing, or reject if you no
                      longer want to proceed.
                    </p>
                    <Textarea
                      className="mt-3 bg-white"
                      value={actionNotes[request.id] || ''}
                      onChange={(event) =>
                        setActionNotes((current) => ({
                          ...current,
                          [request.id]: event.target.value,
                        }))
                      }
                      placeholder="Optional response notes"
                    />
                    <div className="mt-3 flex flex-wrap gap-2">
                      {canDownloadGeneratedAgreement(request) ? (
                        <>
                          <Button
                            type="button"
                            variant="outline"
                            disabled={isSaving}
                            onClick={() => setPreviewRequest(request)}
                          >
                            <Eye className="mr-2 h-4 w-4" />
                            View Agreement
                          </Button>
                          <Button
                            type="button"
                            variant="outline"
                            disabled={isSaving}
                            onClick={() =>
                              void downloadGeneratedAgreement(request)
                            }
                          >
                            <Download className="mr-2 h-4 w-4" />
                            Download PDF
                          </Button>
                        </>
                      ) : null}
                      <Button
                        type="button"
                        disabled={isSaving}
                        onClick={() => void submitDecision(request, 'Accept')}
                      >
                        <CheckCircle2 className="mr-2 h-4 w-4" />
                        Accept
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        disabled={isSaving}
                        onClick={() => void submitDecision(request, 'Reject')}
                      >
                        <XCircle className="mr-2 h-4 w-4" />
                        Reject
                      </Button>
                    </div>
                  </div>
                ) : null}

                {canUploadSignedAgreement(request) ? (
                  <div className="mt-4 rounded-md border border-blue-200 bg-blue-50 p-4">
                    <div className="flex items-center gap-2 font-medium text-blue-900">
                      <FileSignature className="h-4 w-4" />
                      Upload signed agreement
                    </div>
                    <p className="mt-1 text-sm text-blue-800">
                      Upload the signed property agreement after reviewing and
                      signing the generated agreement.
                    </p>
                    {canDownloadGeneratedAgreement(request) ? (
                      <div className="mt-3 flex flex-wrap gap-2">
                        <Button
                          type="button"
                          variant="outline"
                          disabled={isSaving}
                          onClick={() => setPreviewRequest(request)}
                        >
                          <Eye className="mr-2 h-4 w-4" />
                          View Agreement
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          disabled={isSaving}
                          onClick={() =>
                            void downloadGeneratedAgreement(request)
                          }
                        >
                          <Download className="mr-2 h-4 w-4" />
                          Download PDF
                        </Button>
                      </div>
                    ) : null}
                    <div className="mt-3 grid gap-3 md:grid-cols-[1fr_1fr_auto]">
                      <Input
                        type="file"
                        className="bg-white"
                        onChange={(event) =>
                          setSelectedFiles((current) => ({
                            ...current,
                            [request.id]: event.target.files?.[0] || null,
                          }))
                        }
                      />
                      <Input
                        className="bg-white"
                        value={uploadNotes[request.id] || ''}
                        onChange={(event) =>
                          setUploadNotes((current) => ({
                            ...current,
                            [request.id]: event.target.value,
                          }))
                        }
                        placeholder="Optional upload notes"
                      />
                      <Button
                        type="button"
                        disabled={isSaving || !selectedFiles[request.id]}
                        onClick={() => void uploadSignedAgreement(request)}
                      >
                        <Upload className="mr-2 h-4 w-4" />
                        Upload
                      </Button>
                    </div>
                  </div>
                ) : null}

                {isFullyExecuted(request) ? (
                  <div className="mt-4 rounded-md border border-emerald-200 bg-emerald-50 p-4">
                    <div className="flex items-center gap-2 font-medium text-emerald-900">
                      <CheckCircle2 className="h-4 w-4" />
                      Final signed agreement
                    </div>
                    <p className="mt-1 text-sm text-emerald-800">
                      Internal approval and digital signature are complete. This
                      PDF is your final agreement.
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        disabled={isSaving}
                        onClick={() => setPreviewRequest(request)}
                      >
                        <Eye className="mr-2 h-4 w-4" />
                        View final agreement
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        disabled={isSaving}
                        onClick={() => void downloadGeneratedAgreement(request)}
                      >
                        <Download className="mr-2 h-4 w-4" />
                        Download final PDF
                      </Button>
                    </div>
                  </div>
                ) : null}
              </div>
            ))}
            {!selectedRequestId && visibleRequests.length > REQUESTS_PER_PAGE ? (
              <Pagination
                currentPage={requestPage}
                totalPages={requestTotalPages}
                totalItems={visibleRequests.length}
                pageSize={REQUESTS_PER_PAGE}
                onPageChange={setRequestPage}
              />
            ) : null}
          </CardContent>
        </Card>
      </div>
    </>
  );
}

export default function MyPropertyRequestsPage() {
  return <PropertyRequestsView />;
}
