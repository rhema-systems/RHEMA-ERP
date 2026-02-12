'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useTenant } from '@/contexts/TenantContext';
import { ehcTicketService, type CreateEhcTicketRequest, type EhcTicketCategoryTree, type EhcTicketPriority, type EhcTicketSource, type EhcTicketType } from '@/services/ehcTicketService';
import { settingsService } from '@/services/settings';
import { fileUploadService } from '@/services/fileUploadService';

const channelOptions: Array<{ label: string; value: EhcTicketSource }> = [
  { label: 'Website', value: 'Web' },
  { label: 'Mobile App', value: 'Mobile' },
  { label: 'Email', value: 'Email' },
  { label: 'Phone Call', value: 'PhoneCall' },
  { label: 'SMS', value: 'Sms' },
  { label: 'WhatsApp', value: 'WhatsApp' },
];

export default function NewExternalPortalSupportTicketPage() {
  const router = useRouter();
  const { currentTenantCode } = useTenant();
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [relatedValidation, setRelatedValidation] = useState<{ state: 'idle' | 'checking' | 'valid' | 'invalid'; message?: string }>({
    state: 'idle',
  });
  const [form, setForm] = useState<CreateEhcTicketRequest>({
    ticketType: 'Helpdesk',
    priority: 'Medium',
    subject: '',
    description: '',
    source: 'Web',
    categoryId: undefined,
    subcategoryId: undefined,
    relatedEntityType: undefined,
    relatedEntityReference: undefined,
  });

  const { data: securitySettings } = useQuery({
    queryKey: ['security', 'public'],
    queryFn: () => settingsService.getPublicSecuritySettings(),
  });

  const { data: categories, isLoading: categoriesLoading, error: categoriesError, refetch: refetchCategories } = useQuery({
    queryKey: ['ehc', 'categories', currentTenantCode],
    queryFn: () => ehcTicketService.listCategories(),
  });

  const mutation = useMutation({
    mutationFn: async () => {
      const ticket = await ehcTicketService.createTicket({
        ...form,
        captchaToken: captchaToken ?? undefined,
      });

      if (files.length) {
        for (const f of files) {
          const uploaded = await fileUploadService.uploadFile(f, 'ehc-ticket', ticket.id);
          if (uploaded.filePath) {
            await ehcTicketService.addAttachment(ticket.id, {
              filePath: uploaded.filePath,
              fileName: uploaded.originalName || uploaded.filename,
              contentType: uploaded.mimeType,
              fileSize: uploaded.size,
            });
          }
        }
      }

      return ticket;
    },
    onSuccess: () => {
      router.push('/support/tickets');
    },
  });

  const validateRelated = useMutation({
    mutationFn: async () => {
      const type = (form.relatedEntityType || '').trim();
      const reference = (form.relatedEntityReference || '').trim();
      if (!type || !reference) throw new Error('Select type and enter reference');
      return ehcTicketService.validateRelatedEntity(type, reference);
    },
    onMutate: () => setRelatedValidation({ state: 'checking' }),
    onSuccess: (res) => {
      if (res?.exists) {
        setRelatedValidation({ state: 'valid', message: 'Reference validated.' });
        if (res.reference && res.reference !== form.relatedEntityReference) {
          setForm((f) => ({ ...f, relatedEntityReference: res.reference }));
        }
      } else {
        setRelatedValidation({ state: 'invalid', message: 'Reference not found (validation currently supports Asset/Vehicle only).' });
      }
    },
    onError: (err) => {
      setRelatedValidation({ state: 'invalid', message: err instanceof Error ? err.message : 'Validation failed' });
    },
  });

  const captchaRequired = Boolean(securitySettings?.captchaEnabled);
  const canSubmit = Boolean(form.categoryId) && form.description.trim().length > 0 && (!captchaRequired || Boolean(captchaToken));

  const flattenCategoryTree = (nodes: EhcTicketCategoryTree[], rootId: string | null = null, depth = 0) => {
    const out: Array<{ label: string; categoryId: string; subcategoryId?: string; appliesToType?: EhcTicketType | null; depth: number }> = [];
    for (const n of nodes || []) {
      const effectiveRootId = rootId ?? n.id;
      out.push({
        label: n.name,
        categoryId: effectiveRootId,
        subcategoryId: rootId ? n.id : undefined,
        appliesToType: n.appliesToType,
        depth,
      });
      if (n.subcategories?.length) {
        out.push(...flattenCategoryTree(n.subcategories, effectiveRootId, depth + 1));
      }
    }
    return out;
  };

  const allCategoryOptions = flattenCategoryTree(categories || []);
  const visibleCategoryOptions = allCategoryOptions
    .filter((o) => !o.appliesToType || o.appliesToType === form.ticketType)
    .map((o) => ({
      ...o,
      label: `${'— '.repeat(o.depth)}${o.label}`,
    }));

  return (
    <div className="max-w-6xl mx-auto space-y-6">
      <Card className="border-l-4 border-l-sky-500 shadow-sm bg-white">
        <CardHeader className="bg-gradient-to-r from-sky-50 to-white">
          <CardTitle>Create Ticket</CardTitle>
          <CardDescription>Provide details so we can route and respond quickly.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {categoriesError ? (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800 flex items-center justify-between gap-3">
              <div>Failed to load categories for your tenant. Please refresh and try again.</div>
              <Button variant="outline" size="sm" onClick={() => refetchCategories()}>
                Reload
              </Button>
            </div>
          ) : categoriesLoading ? (
            <div className="rounded-md border bg-slate-50 p-3 text-sm text-slate-700">Loading categories…</div>
          ) : null}

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <div className="space-y-4 rounded-lg border bg-slate-50/80 p-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Type</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.ticketType}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        ticketType: e.target.value as EhcTicketType,
                        categoryId: undefined,
                        subcategoryId: undefined,
                      }))
                    }
                  >
                    <option value="Enquiry">Enquiry</option>
                    <option value="Complaint">Complaint</option>
                    <option value="Helpdesk">Helpdesk</option>
                  </select>
                </div>
                <div className="space-y-2">
                  <Label>Priority</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.priority}
                    onChange={(e) => setForm((f) => ({ ...f, priority: e.target.value as EhcTicketPriority }))}
                  >
                    <option value="Low">Low</option>
                    <option value="Medium">Medium</option>
                    <option value="High">High</option>
                    <option value="Critical">Critical</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Channel</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.source || 'Web'}
                    onChange={(e) => setForm((f) => ({ ...f, source: e.target.value as EhcTicketSource }))}
                  >
                    {channelOptions.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="space-y-2">
                  <Label>Category</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.subcategoryId || form.categoryId || ''}
                    onChange={(e) =>
                      setForm((f) => {
                        const value = e.target.value || '';
                        if (!value) return { ...f, categoryId: undefined, subcategoryId: undefined };

                        const selected = visibleCategoryOptions.find((o) => (o.subcategoryId || o.categoryId) === value);
                        if (!selected) return { ...f, categoryId: value, subcategoryId: undefined };

                        return {
                          ...f,
                          categoryId: selected.categoryId,
                          subcategoryId: selected.subcategoryId,
                        };
                      })
                    }
                  >
                    <option value="">Select category</option>
                    {visibleCategoryOptions.map((o) => (
                      <option key={`${o.categoryId}:${o.subcategoryId ?? ''}:${o.label}`} value={o.subcategoryId || o.categoryId}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="space-y-2">
                <Label>Subject</Label>
                <Input
                  value={form.subject || ''}
                  onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))}
                  placeholder="Short summary"
                />
              </div>

              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  placeholder="Describe the issue / enquiry"
                  rows={10}
                />
              </div>
            </div>

            <div className="space-y-4 rounded-lg border bg-slate-50/80 p-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Related Entity Type</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.relatedEntityType || ''}
                    onChange={(e) => setForm((f) => ({ ...f, relatedEntityType: e.target.value || undefined }))}
                  >
                    <option value="">None</option>
                    <option value="Asset">Asset</option>
                    <option value="Vehicle">Vehicle</option>
                    <option value="Other">Other</option>
                  </select>
                </div>
                <div className="space-y-2">
                  <Label>Related Reference</Label>
                  <Input
                    value={form.relatedEntityReference || ''}
                    onChange={(e) => setForm((f) => ({ ...f, relatedEntityReference: e.target.value || undefined }))}
                    placeholder={form.relatedEntityType === 'Vehicle' ? 'e.g. GR-123-24' : 'Enter reference'}
                  />
                </div>
              </div>

              {form.relatedEntityType && form.relatedEntityReference ? (
                <div className="flex items-center gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => validateRelated.mutate()}
                    disabled={validateRelated.isPending || !['Asset', 'Vehicle'].includes(form.relatedEntityType)}
                    title={['Asset', 'Vehicle'].includes(form.relatedEntityType) ? 'Validate reference' : 'Validation only available for Asset/Vehicle'}
                  >
                    {validateRelated.isPending ? 'Validating…' : 'Validate'}
                  </Button>
                  {relatedValidation.state === 'valid' ? <Badge className="bg-green-600 text-white">Valid</Badge> : null}
                  {relatedValidation.state === 'invalid' ? <Badge className="bg-red-600 text-white">Not found</Badge> : null}
                  {relatedValidation.message ? <div className="text-xs text-slate-600">{relatedValidation.message}</div> : null}
                </div>
              ) : (
                <div className="text-xs text-slate-500">Tip: Validation is available for Asset/Vehicle references.</div>
              )}

              <div className="space-y-2">
                <Label>Attachments</Label>
                <Input type="file" multiple onChange={(e) => setFiles(Array.from(e.target.files || []))} />
                {files.length ? <div className="text-xs text-slate-600">{files.length} file(s) selected</div> : null}
              </div>

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
            </div>
          </div>

          <div className="flex items-center gap-2 rounded-lg border bg-white p-4">
            <Button onClick={() => mutation.mutate()} disabled={mutation.isPending || !canSubmit}>
              {mutation.isPending ? 'Submitting...' : 'Submit'}
            </Button>
            <Button variant="outline" onClick={() => router.push('/support/tickets')}>
              Cancel
            </Button>
            {mutation.isError ? <div className="text-sm text-red-600 ml-2">Failed to submit. Please try again.</div> : null}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
