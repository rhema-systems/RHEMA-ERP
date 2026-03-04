'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ehcServiceCatalogService, type EhcServiceRequestType } from '@/services/ehcServiceCatalogService';
import { fileUploadService } from '@/services/file-upload.service';

type FormField =
  | { key: string; label?: string; type: 'text' | 'textarea' | 'number' | 'checkbox' | 'date'; required?: boolean }
  | { key: string; label?: string; type: 'select'; required?: boolean; options?: Array<{ label: string; value: string }> };

function parseFields(defJson: string): FormField[] {
  try {
    const def = JSON.parse(defJson);
    const fields = Array.isArray(def?.fields) ? def.fields : [];
    return fields
      .filter((f: any) => f && typeof f.key === 'string' && typeof f.type === 'string')
      .map((f: any) => ({
        key: String(f.key),
        label: typeof f.label === 'string' ? f.label : f.key,
        type: f.type,
        required: Boolean(f.required),
        options: Array.isArray(f.options) ? f.options : undefined,
      }));
  } catch {
    return [];
  }
}

export default function NewHelpdeskServiceRequestPage() {
  const router = useRouter();
  const [requestTypeId, setRequestTypeId] = useState<string>('');
  const [title, setTitle] = useState<string>('');
  const [formData, setFormData] = useState<Record<string, any>>({});
  const [files, setFiles] = useState<File[]>([]);

  const { data: types, isLoading: typesLoading, error: typesError, refetch: refetchTypes } = useQuery({
    queryKey: ['ehc', 'internal', 'service-catalog', 'request-types'],
    queryFn: () => ehcServiceCatalogService.listRequestTypesInternal(),
  });

  const selectedType: EhcServiceRequestType | undefined = useMemo(() => (types || []).find((t) => t.id === requestTypeId), [types, requestTypeId]);
  const fields = useMemo(() => (selectedType ? parseFields(selectedType.formDefinitionJson) : []), [selectedType]);

  const mutation = useMutation({
    mutationFn: async () => {
      const formDataJson = JSON.stringify(formData || {});
      const created = await ehcServiceCatalogService.createRequestInternal({
        requestTypeId,
        title: title.trim() || null,
        formDataJson,
      });

      if (files.length) {
        for (const f of files) {
          const uploaded = await fileUploadService.uploadSingleFile(f, 'ehc-service-request');
          if (uploaded.filePath) {
            await ehcServiceCatalogService.addAttachmentInternal(created.id, {
              filePath: uploaded.filePath,
              fileName: uploaded.originalFileName || uploaded.fileName,
              contentType: uploaded.contentType,
              fileSize: uploaded.fileSize,
              isInternal: false,
            });
          }
        }
      }

      return created;
    },
    onSuccess: (created) => {
      router.push(`/helpdesk/requests/${created.id}`);
    },
  });

  const setValue = (key: string, value: any) => setFormData((d) => ({ ...d, [key]: value }));

  const canSubmit = Boolean(selectedType) && !mutation.isPending;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">New Service Request</h1>
          <p className="text-slate-600 mt-1">Submit a service catalog request (internal staff).</p>
        </div>
        <Button variant="outline" onClick={() => router.push('/helpdesk/requests')}>
          Back to requests
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Request</CardTitle>
          <CardDescription>Fields and approval steps depend on the selected request type.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Request type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={requestTypeId}
                onChange={(e) => {
                  setRequestTypeId(e.target.value);
                  setFormData({});
                }}
                disabled={typesLoading}
              >
                <option value="">Select a request type…</option>
                {(types || []).map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
              {typesError ? (
                <div className="text-sm text-red-600">
                  Failed to load request types.{' '}
                  <button className="underline" type="button" onClick={() => refetchTypes()}>
                    Retry
                  </button>
                </div>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label>Title (optional)</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Short title" />
            </div>
          </div>

          {selectedType ? (
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Form</CardTitle>
                <CardDescription>{selectedType.description || 'Complete the form fields below.'}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {fields.length ? (
                  fields.map((f) => (
                    <div key={f.key} className="space-y-2">
                      <Label>
                        {f.label || f.key}
                        {f.required ? <span className="text-red-600"> *</span> : null}
                      </Label>

                      {f.type === 'textarea' ? (
                        <Textarea value={String(formData?.[f.key] ?? '')} onChange={(e) => setValue(f.key, e.target.value)} />
                      ) : f.type === 'select' ? (
                        <select
                          className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                          value={String(formData?.[f.key] ?? '')}
                          onChange={(e) => setValue(f.key, e.target.value)}
                        >
                          <option value="">Select…</option>
                          {(f.options || []).map((o) => (
                            <option key={o.value} value={o.value}>
                              {o.label}
                            </option>
                          ))}
                        </select>
                      ) : f.type === 'checkbox' ? (
                        <div className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            className="h-4 w-4"
                            checked={Boolean(formData?.[f.key])}
                            onChange={(e) => setValue(f.key, e.target.checked)}
                          />
                          <span className="text-sm text-slate-600">Yes</span>
                        </div>
                      ) : (
                        <Input
                          type={f.type === 'number' ? 'number' : f.type === 'date' ? 'date' : 'text'}
                          value={String(formData?.[f.key] ?? '')}
                          onChange={(e) => setValue(f.key, e.target.value)}
                        />
                      )}
                    </div>
                  ))
                ) : (
                  <div className="text-sm text-slate-600">No form fields configured for this request type.</div>
                )}

                <div className="space-y-2 pt-2">
                  <Label>Attachments (optional)</Label>
                  <input
                    type="file"
                    multiple
                    onChange={(e) => setFiles(Array.from(e.target.files || []))}
                    className="block w-full text-sm"
                  />
                  {files.length ? <div className="text-xs text-slate-600">{files.length} file(s) selected</div> : null}
                </div>
              </CardContent>
            </Card>
          ) : null}

          {mutation.error ? <div className="text-sm text-red-600">Failed to submit request.</div> : null}

          <div className="flex items-center justify-end gap-2">
            <Button variant="outline" onClick={() => router.push('/helpdesk/requests')}>
              Cancel
            </Button>
            <Button disabled={!canSubmit} onClick={() => mutation.mutate()}>
              {mutation.isPending ? 'Submitting…' : 'Submit request'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

