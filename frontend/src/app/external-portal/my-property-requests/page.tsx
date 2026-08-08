'use client';

import Link from 'next/link';
import React from 'react';
import {
  Building2,
  CalendarDays,
  CheckCircle2,
  ClipboardList,
  FileSignature,
  Loader2,
  Upload,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  externalEstateServicesService,
  type ExternalEstateServiceRequest,
} from '@/services/external-estate-services.service';

const PROPERTY_LISTING_SOURCE = 'External Portal - Estate Listings';

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleDateString();
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

function fieldValue(
  request: ExternalEstateServiceRequest,
  key: string
): string {
  return request.fieldValues?.[key] || '';
}

function isApprovedForCustomerAction(request: ExternalEstateServiceRequest) {
  return (
    fieldValue(request, 'decisionStatus').toLowerCase() === 'approved' &&
    ['pending', ''].includes(
      fieldValue(request, 'customerAcceptanceStatus').toLowerCase()
    )
  );
}

function canUploadSignedAgreement(request: ExternalEstateServiceRequest) {
  return (
    fieldValue(request, 'decisionStatus').toLowerCase() === 'approved' &&
    fieldValue(request, 'customerAcceptanceStatus').toLowerCase() ===
      'accepted' &&
    !fieldValue(request, 'signedAgreementReference')
  );
}

function workflowStatusLabel(request: ExternalEstateServiceRequest) {
  return (
    fieldValue(request, 'applicationStatus') ||
    fieldValue(request, 'customerAcceptanceStatus') ||
    request.status
  );
}

export default function MyPropertyRequestsPage() {
  const [requests, setRequests] = React.useState<ExternalEstateServiceRequest[]>(
    []
  );
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [actionNotes, setActionNotes] = React.useState<Record<string, string>>(
    {}
  );
  const [uploadNotes, setUploadNotes] = React.useState<Record<string, string>>(
    {}
  );
  const [selectedFiles, setSelectedFiles] = React.useState<
    Record<string, File | null>
  >({});

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
    } catch {
      setError('Could not update your customer decision.');
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
    } catch {
      setError('Could not upload the signed agreement.');
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

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">
            My Property Requests
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
              Submitted requests
            </span>
            <Badge variant="outline">
              {isLoading ? 'Loading' : requests.length}
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

          {!isLoading && requests.length === 0 ? (
            <div className="rounded-md border border-dashed p-10 text-center text-sm text-slate-500">
              You have not submitted a purchase bid or rental request yet.
            </div>
          ) : null}

          {requests.map((request) => (
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
                    {fieldValue(request, 'decisionStatus') || 'Pending review'}
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
                    {fieldValue(request, 'signedAgreementReference') ||
                      fieldValue(request, 'generatedAgreementReference') ||
                      'Not ready'}
                  </div>
                </div>
                <div>
                  <span className="flex items-center gap-1 text-slate-400">
                    <CalendarDays className="h-3.5 w-3.5" /> Submitted
                  </span>
                  <div className="mt-1 font-medium text-slate-700">
                    {formatDate(request.createdAt)}
                  </div>
                </div>
              </div>

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
                    Upload the signed lease/tenancy agreement after reviewing
                    and signing the generated agreement.
                  </p>
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
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
