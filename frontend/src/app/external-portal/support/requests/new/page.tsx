'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ehcServiceCatalogService, type EhcServiceRequestType } from '@/services/ehcServiceCatalogService';
import { settingsService } from '@/services/settings';
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

export default function NewExternalServiceRequestPage() {
  const router = useRouter();
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const [requestTypeId, setRequestTypeId] = useState<string>('');
  const [title, setTitle] = useState<string>('');
  const [formData, setFormData] = useState<Record<string, any>>({});
  const [files, setFiles] = useState<File[]>([]);

  const { data: securitySettings } = useQuery({
    queryKey: ['security', 'public'],
    queryFn: () => settingsService.getPublicSecuritySettings(),
  });

  const { data: types, isLoading: typesLoading, error: typesError, refetch: refetchTypes } = useQuery({
    queryKey: ['ehc', 'external', 'service-catalog', 'request-types'],
    queryFn: () => ehcServiceCatalogService.listRequestTypesExternal(),
  });

  const selectedType: EhcServiceRequestType | undefined = useMemo(() => (types || []).find((t) => t.id === requestTypeId), [types, requestTypeId]);
  const fields = useMemo(() => (selectedType ? parseFields(selectedType.formDefinitionJson) : []), [selectedType]);

  const captchaRequired = Boolean(securitySettings?.captchaEnabled);
  const canSubmit = Boolean(selectedType) && (!captchaRequired || Boolean(captchaToken));

  const mutation = useMutation({
    mutationFn: async () => {
      const formDataJson = JSON.stringify(formData || {});
      const created = await ehcServiceCatalogService.createRequestExternal({
        requestTypeId,
        title: title.trim() || null,
        formDataJson,
        captchaToken: captchaToken ?? undefined,
      });

      if (files.length) {
        for (const f of files) {
          const uploaded = await fileUploadService.uploadSingleFile(f, 'ehc-service-request');
          if (uploaded.filePath) {
            await ehcServiceCatalogService.addAttachmentExternal(created.id, {
              filePath: uploaded.filePath,
              fileName: uploaded.originalFileName || uploaded.fileName,
              contentType: uploaded.contentType,
              fileSize: uploaded.fileSize,
            });
          }
        }
      }

      return created;
    },
    onSuccess: (created) => {
      router.push(`/support/requests/${created.id}`);
    },
  });

  const setValue = (key: string, value: any) => setFormData((d) => ({ ...d, [key]: value }));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">New Service Request</h1>
        <p className="text-slate-600 mt-1">Choose a request type and complete the form.</p>
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
                <option value="">{typesLoading ? 'Loading…' : 'Select type'}</option>
                {(types || []).map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
              {typesError ? (
                <div className="text-sm text-red-600">
                  Failed to load request types.{' '}
                  <button className="underline" onClick={() => refetchTypes()}>
                    Retry
                  </button>
                </div>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label>Title (optional)</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Short summary" />
            </div>
          </div>

          {selectedType ? (
            <div className="space-y-4 rounded-lg border bg-slate-50/80 p-4">
              {fields.length ? (
                fields.map((f) => {
                  const label = f.label || f.key;
                  const value = formData[f.key];
                  if (f.type === 'textarea') {
                    return (
                      <div key={f.key} className="space-y-2">
                        <Label>
                          {label} {f.required ? <span className="text-red-600">*</span> : null}
                        </Label>
                        <Textarea value={value || ''} onChange={(e) => setValue(f.key, e.target.value)} rows={5} />
                      </div>
                    );
                  }
                  if (f.type === 'select') {
                    return (
                      <div key={f.key} className="space-y-2">
                        <Label>
                          {label} {f.required ? <span className="text-red-600">*</span> : null}
                        </Label>
                        <select
                          className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                          value={value || ''}
                          onChange={(e) => setValue(f.key, e.target.value)}
                        >
                          <option value="">Select…</option>
                          {(f.options || []).map((o) => (
                            <option key={o.value} value={o.value}>
                              {o.label}
                            </option>
                          ))}
                        </select>
                      </div>
                    );
                  }
                  if (f.type === 'checkbox') {
                    return (
                      <div key={f.key} className="flex items-center gap-2">
                        <input type="checkbox" checked={Boolean(value)} onChange={(e) => setValue(f.key, e.target.checked)} />
                        <Label>
                          {label} {f.required ? <span className="text-red-600">*</span> : null}
                        </Label>
                      </div>
                    );
                  }
                  if (f.type === 'number') {
                    return (
                      <div key={f.key} className="space-y-2">
                        <Label>
                          {label} {f.required ? <span className="text-red-600">*</span> : null}
                        </Label>
                        <Input
                          type="number"
                          value={value ?? ''}
                          onChange={(e) => setValue(f.key, e.target.value === '' ? null : Number(e.target.value))}
                        />
                      </div>
                    );
                  }
                  if (f.type === 'date') {
                    return (
                      <div key={f.key} className="space-y-2">
                        <Label>
                          {label} {f.required ? <span className="text-red-600">*</span> : null}
                        </Label>
                        <Input type="date" value={value || ''} onChange={(e) => setValue(f.key, e.target.value)} />
                      </div>
                    );
                  }
                  return (
                    <div key={f.key} className="space-y-2">
                      <Label>
                        {label} {f.required ? <span className="text-red-600">*</span> : null}
                      </Label>
                      <Input value={value || ''} onChange={(e) => setValue(f.key, e.target.value)} />
                    </div>
                  );
                })
              ) : (
                <div className="text-sm text-slate-600">This request type has no configured fields.</div>
              )}

              {securitySettings?.captchaEnabled ? (
                <div className="space-y-2">
                  <Label>Verification</Label>
                  {securitySettings.captchaProvider === 'recaptcha' && securitySettings.recaptchaSiteKey ? (
                    <ReCAPTCHA sitekey={securitySettings.recaptchaSiteKey} onChange={(t) => setCaptchaToken(t)} />
                  ) : (
                    <div className="text-sm text-slate-600">CAPTCHA is enabled but not configured for this portal. Please contact support.</div>
                  )}
                </div>
              ) : null}

              <div className="space-y-2">
                <Label>Attachments (optional)</Label>
                <Input type="file" multiple onChange={(e) => setFiles(Array.from(e.target.files || []))} />
                {files.length ? <div className="text-xs text-slate-600">{files.length} file(s) selected</div> : null}
              </div>
            </div>
          ) : (
            <div className="text-sm text-slate-600">Select a request type to continue.</div>
          )}

          <div className="flex items-center gap-2 rounded-lg border bg-white p-4">
            <Button onClick={() => mutation.mutate()} disabled={mutation.isPending || !canSubmit}>
              {mutation.isPending ? 'Submitting…' : 'Submit'}
            </Button>
            <Button variant="outline" onClick={() => router.push('/support/requests')}>
              Cancel
            </Button>
            {mutation.isError ? <div className="text-sm text-red-600 ml-2">Failed to submit. Please try again.</div> : null}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
