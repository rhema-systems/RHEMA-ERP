'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ClipboardList } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ehcServiceCatalogService, type EhcServiceRequestStatus } from '@/services/ehcServiceCatalogService';

const statusBadgeClassName = (s: EhcServiceRequestStatus) => {
  switch (s) {
    case 'Approved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Rejected':
      return 'bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90';
    case 'PendingApproval':
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
    default:
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
  }
};

function safeJsonParse(s: string): any {
  try {
    return JSON.parse(s);
  } catch {
    return null;
  }
}

export default function ExternalServiceRequestDetailPage() {
  const params = useParams();
  const id = String(params?.id || '');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'external', 'service-catalog', 'requests', id],
    queryFn: () => ehcServiceCatalogService.getMyRequestExternal(id),
    enabled: Boolean(id),
  });

  const formDef = useMemo(() => (data?.formDefinitionJson ? safeJsonParse(data.formDefinitionJson) : null), [data?.formDefinitionJson]);
  const formData = useMemo(() => (data?.formDataJson ? safeJsonParse(data.formDataJson) : null), [data?.formDataJson]);
  const fields: Array<{ key: string; label?: string; type?: string }> = Array.isArray(formDef?.fields) ? formDef.fields : [];

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <ClipboardList className="w-6 h-6" />
            <h1 className="text-3xl font-bold">{data ? data.requestNumber : 'Service Request'}</h1>
            {data ? <Badge className={statusBadgeClassName(data.status)}>{data.status}</Badge> : null}
          </div>
          {data ? <p className="text-slate-600 mt-1">{data.requestTypeName}</p> : null}
        </div>
        <div className="flex items-center gap-2">
          <Link href="/support/requests">
            <Button variant="outline">
              <ArrowLeft className="w-4 h-4 mr-2" /> Back
            </Button>
          </Link>
          <Button variant="outline" onClick={() => refetch()}>
            Refresh
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
          <CardDescription>{isLoading ? 'Loading…' : data?.title ? data.title : ' '}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {error ? <div className="text-sm text-red-600">Failed to load.</div> : null}
          {!data && !isLoading ? <div className="text-sm text-slate-600">Not found.</div> : null}

          {data ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1">
                <div className="text-xs text-slate-500">Submitted</div>
                <div className="text-sm">{data.submittedAtUtc ? new Date(data.submittedAtUtc).toLocaleString() : '-'}</div>
              </div>
              <div className="space-y-1">
                <div className="text-xs text-slate-500">Workflow</div>
                <div className="text-sm">{data.workflowInstanceId || '-'}</div>
              </div>
              {data.rejectionReason ? (
                <div className="sm:col-span-2 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                  <div className="font-medium">Rejection reason</div>
                  <div>{data.rejectionReason}</div>
                </div>
              ) : null}
            </div>
          ) : null}

          {data?.attachments?.length ? (
            <div className="space-y-2">
              <div className="font-medium">Attachments</div>
              <div className="space-y-2">
                {data.attachments.map((a: any) => (
                  <div key={a.id} className="flex items-center justify-between rounded-md border bg-white p-3">
                    <div className="text-sm">
                      <div className="font-medium">{a.fileName}</div>
                      <div className="text-xs text-slate-500">{a.contentType || 'file'} · {a.fileSize || 0} bytes</div>
                    </div>
                    {a.publicUrl ? (
                      <a className="text-sm underline" href={a.publicUrl} target="_blank" rel="noreferrer">
                        Download
                      </a>
                    ) : null}
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {data ? (
            <div className="space-y-2">
              <div className="font-medium">Form data</div>
              {fields.length ? (
                <div className="space-y-2">
                  {fields.map((f) => (
                    <div key={f.key} className="rounded-md border bg-white p-3">
                      <div className="text-xs text-slate-500">{f.label || f.key}</div>
                      <div className="text-sm whitespace-pre-wrap">{formData && f.key in formData ? String(formData[f.key]) : ''}</div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="rounded-md border bg-white p-3 text-sm text-slate-600">
                  {formData ? <pre className="text-xs whitespace-pre-wrap">{JSON.stringify(formData, null, 2)}</pre> : 'No form data.'}
                </div>
              )}
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}
