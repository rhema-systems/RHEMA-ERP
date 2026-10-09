'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import React from 'react';
import { ArrowLeft, CalendarDays, ClipboardList, Loader2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  externalEstateServicesService,
  type ExternalEstateServiceRequest,
} from '@/services/external-estate-services.service';

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleDateString();
}

function fieldValue(request: ExternalEstateServiceRequest, key: string) {
  return request.fieldValues?.[key] || '';
}

const PRIMARY_FIELD_GROUPS: Array<{ label: string; keys: string[] }> = [
  { label: 'Request type', keys: ['procedureType', 'requestType', 'category'] },
  { label: 'Requester name', keys: ['requester', 'applicantName', 'customerName'] },
  { label: 'Customer account', keys: ['customerBusinessPartnerReference', 'customerAccountReference', 'sourceReference'] },
  { label: 'Requester contact', keys: ['contactReference', 'contact', 'phoneNumber', 'emailAddress'] },
  { label: 'Property / unit / plot', keys: ['propertyUnit', 'propertyNumber', 'plotNumber', 'unitNumber', 'housePlotShopNumber'] },
  { label: 'Location', keys: ['location', 'locationDetail', 'propertyLocation'] },
  { label: 'Issue type', keys: ['issueType', 'serviceType', 'requestCategory'] },
  { label: 'Reported urgency', keys: ['reportedPriority', 'customerReportedUrgency'] },
  { label: 'Priority', keys: ['priority'] },
  { label: 'Service impact', keys: ['serviceImpact', 'impactLevel'] },
  { label: 'Target date', keys: ['targetDate', 'expectedCompletionDate', 'requiredDate'] },
  { label: 'Preferred visit date', keys: ['preferredVisitDate', 'visitDate'] },
  { label: 'Access instructions', keys: ['accessInstructions', 'accessNotes'] },
  { label: 'Description', keys: ['issueDescription', 'description', 'notes', 'requestDescription'] },
];

const HIDDEN_FIELD_KEYS = new Set(
  [
    'referenceNumber',
    'sourceLabel',
    'sourceSystem',
    'sourceWorkspace',
    'sourceReference',
    'sourceRecordReference',
    'sourceDepartment',
    'portalRecipientIdentity',
    'customerBusinessPartnerId',
    'customerAccountReference',
    'originatingProcedureCaseId',
    'originatingEntityType',
    'originatingTransactionReference',
    'maintenanceExecutionComplete',
    'maintenanceExecutionStatus',
  ].map((key) => key.toLowerCase())
);

function displayFieldValue(value?: string | null) {
  return value && value.trim().length > 0 ? value : 'Not recorded';
}

function formatFieldLabel(key: string) {
  const spaced = key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  return spaced ? spaced.charAt(0).toUpperCase() + spaced.slice(1) : key;
}

function detailRows(request: ExternalEstateServiceRequest) {
  const usedFieldKeys = new Set<string>();
  const rows: Array<[string, string]> = [
    ['Reference number', request.referenceNumber || request.title],
  ];

  PRIMARY_FIELD_GROUPS.forEach((group) => {
    const matchedKey = group.keys.find((key) => fieldValue(request, key));
    if (matchedKey) {
      group.keys.forEach((key) => usedFieldKeys.add(key.toLowerCase()));
      rows.push([group.label, displayFieldValue(fieldValue(request, matchedKey))]);
      return;
    }

    if (group.label === 'Request type') {
      rows.push([group.label, request.title]);
    } else if (group.label === 'Requester name') {
      rows.push([group.label, displayFieldValue(request.applicantName)]);
    }
  });

  Object.entries(request.fieldValues ?? {}).forEach(([key, value]) => {
    const normalizedKey = key.toLowerCase();
    if (
      usedFieldKeys.has(normalizedKey) ||
      HIDDEN_FIELD_KEYS.has(normalizedKey) ||
      !value ||
      value.trim().length === 0
    ) {
      return;
    }

    rows.push([formatFieldLabel(key), value]);
  });

  return rows;
}

export default function EstateServiceRequestDetailPage() {
  const params = useParams<{ id: string }>();
  const [request, setRequest] = React.useState<ExternalEstateServiceRequest | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      setError(null);
      try {
        const data = await externalEstateServicesService.getRequest(params.id);
        if (mounted) setRequest(data);
      } catch {
        if (mounted) setError('Could not load this Estate service request.');
      } finally {
        if (mounted) setIsLoading(false);
      }
    };

    void load();
    return () => {
      mounted = false;
    };
  }, [params.id]);

  return (
    <div className="space-y-6">
      <Button asChild variant="outline">
        <Link href="/external-portal/estate-services">
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to Estate Services
        </Link>
      </Button>

      {isLoading ? (
        <Card className="border-slate-700 bg-slate-950 text-slate-100 shadow-sm">
          <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-slate-300">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading request
          </CardContent>
        </Card>
      ) : null}

      {error ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      {request ? (
        <Card className="border-slate-700 bg-slate-950 text-slate-100 shadow-sm">
          <CardHeader>
            <CardTitle className="flex flex-col gap-3 text-base sm:flex-row sm:items-start sm:justify-between">
              <span className="flex items-center gap-2">
                <ClipboardList className="h-5 w-5 text-blue-600" />
                {request.referenceNumber || request.title}
              </span>
              <Badge variant="secondary" className="w-fit">
                {request.status}
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-3 rounded-md border border-slate-700 bg-slate-900 p-4 text-sm sm:grid-cols-3">
              <div>
                <div className="text-xs text-slate-400">Current stage</div>
                <div className="mt-1 font-medium text-white">
                  {request.currentStageName || 'Not recorded'}
                </div>
              </div>
              <div>
                <div className="text-xs text-slate-400">Assigned to</div>
                <div className="mt-1 font-medium text-white">
                  {request.currentAssignedRole || 'Awaiting assignment'}
                </div>
              </div>
              <div>
                <div className="flex items-center gap-1 text-xs text-slate-400">
                  <CalendarDays className="h-3.5 w-3.5" />
                  Submitted
                </div>
                <div className="mt-1 font-medium text-white">
                  {formatDate(request.createdAt)}
                </div>
              </div>
            </div>

            <div className="grid gap-3 text-sm md:grid-cols-2">
              {detailRows(request).map(([label, value], index) => (
                <div key={`${label}-${index}`} className="rounded-md border border-slate-700 bg-slate-900 p-3">
                  <div className="text-xs font-medium uppercase text-slate-400">
                    {label}
                  </div>
                  <div className="mt-1 whitespace-pre-wrap text-slate-100">
                    {value}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
