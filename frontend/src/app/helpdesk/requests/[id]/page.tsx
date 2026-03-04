'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, CheckCircle2, RefreshCw, XCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { fileUploadService } from '@/services/file-upload.service';
import { ehcServiceCatalogService, type EhcServiceRequestStatus } from '@/services/ehcServiceCatalogService';

const statusBadgeClassName = (s: EhcServiceRequestStatus) => {
  switch (s) {
    case 'Approved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Rejected':
      return 'bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90';
    case 'PendingApproval':
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
    case 'Fulfilled':
      return 'bg-purple-600 text-white hover:bg-purple-600/90 dark:bg-purple-500 dark:hover:bg-purple-500/90';
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

export default function HelpdeskServiceRequestDetailPage() {
  const params = useParams();
  const qc = useQueryClient();
  const id = String(params?.id || '');

  const [notes, setNotes] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [internalAttachment, setInternalAttachment] = useState(false);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'service-catalog', 'request', id],
    queryFn: () => ehcServiceCatalogService.getRequestInternal(id),
    enabled: Boolean(id),
  });

  const def = useMemo(() => (data?.formDefinitionJson ? safeJsonParse(data.formDefinitionJson) : null), [data?.formDefinitionJson]);
  const formData = useMemo(() => (data?.formDataJson ? safeJsonParse(data.formDataJson) : null), [data?.formDataJson]);
  const fields: Array<{ key: string; label?: string }> = Array.isArray(def?.fields) ? def.fields : [];

  const approve = useMutation({
    mutationFn: () => ehcServiceCatalogService.approveInternal(id, notes || null),
    onSuccess: async () => {
      setNotes('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'request', id] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'requests'] });
    },
  });

  const reject = useMutation({
    mutationFn: () => ehcServiceCatalogService.rejectInternal(id, rejectReason),
    onSuccess: async () => {
      setRejectReason('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'request', id] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'requests'] });
    },
  });

  const fulfill = useMutation({
    mutationFn: () => ehcServiceCatalogService.fulfillInternal(id, notes || null),
    onSuccess: async () => {
      setNotes('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'request', id] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'requests'] });
    },
  });

  const close = useMutation({
    mutationFn: () => ehcServiceCatalogService.closeInternal(id, notes || null),
    onSuccess: async () => {
      setNotes('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'request', id] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'requests'] });
    },
  });

  const upload = useMutation({
    mutationFn: async () => {
      for (const f of files) {
        const uploaded = await fileUploadService.uploadSingleFile(f, 'ehc-service-request');
        await ehcServiceCatalogService.addAttachmentInternal(id, {
          filePath: uploaded.filePath,
          fileName: uploaded.originalFileName || uploaded.fileName,
          contentType: uploaded.contentType,
          fileSize: uploaded.fileSize,
          isInternal: internalAttachment,
        });
      }
    },
    onSuccess: async () => {
      setFiles([]);
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'service-catalog', 'request', id] });
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-3xl font-bold">{data ? data.requestNumber : 'Service Request'}</h1>
            {data ? <Badge className={statusBadgeClassName(data.status)}>{data.status}</Badge> : null}
          </div>
          {data ? <p className="text-slate-600 mt-1">{data.requestTypeName}</p> : null}
        </div>
        <div className="flex items-center gap-2">
          <Link href="/helpdesk/requests">
            <Button variant="outline">
              <ArrowLeft className="w-4 h-4 mr-2" /> Back
            </Button>
          </Link>
          <Button variant="outline" onClick={() => refetch()}>
            <RefreshCw className="w-4 h-4 mr-2" /> Refresh
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Overview</CardTitle>
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

          {data ? (
            <div className="space-y-2">
              <div className="font-medium">Actions</div>
              <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional notes/comments" rows={3} />
              <div className="flex flex-wrap items-center gap-2">
                <Button onClick={() => approve.mutate()} disabled={approve.isPending || !data}>
                  <CheckCircle2 className="w-4 h-4 mr-2" /> Approve
                </Button>
                <Button variant="destructive" onClick={() => reject.mutate()} disabled={reject.isPending || !data || !rejectReason.trim()}>
                  <XCircle className="w-4 h-4 mr-2" /> Reject
                </Button>
                <Input value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} placeholder="Rejection reason (required)" />
                <Button variant="outline" onClick={() => fulfill.mutate()} disabled={fulfill.isPending || !data}>
                  Mark Fulfilled
                </Button>
                <Button variant="outline" onClick={() => close.mutate()} disabled={close.isPending || !data}>
                  Close
                </Button>
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Form Data</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {data ? (
            fields.length ? (
              fields.map((f) => (
                <div key={f.key} className="rounded-md border bg-white p-3">
                  <div className="text-xs text-slate-500">{f.label || f.key}</div>
                  <div className="text-sm whitespace-pre-wrap">{formData && f.key in formData ? String(formData[f.key]) : ''}</div>
                </div>
              ))
            ) : (
              <pre className="text-xs whitespace-pre-wrap">{JSON.stringify(formData || {}, null, 2)}</pre>
            )
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Attachments</CardTitle>
          <CardDescription>Upload and manage request files.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Input type="file" multiple onChange={(e) => setFiles(Array.from(e.target.files || []))} />
            <div className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={internalAttachment} onChange={(e) => setInternalAttachment(e.target.checked)} />
              <span>Internal-only</span>
            </div>
            <Button variant="outline" onClick={() => upload.mutate()} disabled={upload.isPending || !files.length}>
              Upload {files.length ? `(${files.length})` : ''}
            </Button>
          </div>

          {data?.attachments?.length ? (
            <div className="space-y-2">
              {data.attachments.map((a: any) => (
                <div key={a.id} className="flex items-center justify-between rounded-md border bg-white p-3">
                  <div className="text-sm">
                    <div className="font-medium flex items-center gap-2">
                      <span>{a.fileName}</span>
                      {a.isInternal ? <Badge className="bg-slate-600 text-white">Internal</Badge> : null}
                    </div>
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
          ) : (
            <div className="text-sm text-slate-600">No attachments.</div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
