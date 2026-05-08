'use client';

import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Users } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import { getHelpdeskScopeConfig } from '@/lib/helpdesk-scope';
import { ehcInternalTicketService, type EhcAgentReplyProfile } from '@/services/ehcInternalTicketService';

export default function HelpdeskSupportPage() {
  const searchParams = useSearchParams();
  const qc = useQueryClient();
  const { toast } = useToast();
  const scopeParam = searchParams?.get('scope');
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(scopeParam), [scopeParam]);

  const { data, isLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'agent-profile', 'reply'],
    queryFn: () => ehcInternalTicketService.getMyReplyProfile(),
  });

  const [form, setForm] = useState<EhcAgentReplyProfile>({
    signature: '',
    isSignatureEnabled: true,
    appendSignatureToReplies: true,
  });

  useEffect(() => {
    if (!data) return;
    setForm({
      signature: data.signature ?? '',
      isSignatureEnabled: Boolean(data.isSignatureEnabled),
      appendSignatureToReplies: Boolean(data.appendSignatureToReplies),
    });
  }, [data?.signature, data?.isSignatureEnabled, data?.appendSignatureToReplies]);

  const save = useMutation({
    mutationFn: async () => {
      return ehcInternalTicketService.updateMyReplyProfile(form);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'agent-profile', 'reply'] });
      toast({ title: 'Saved', description: 'Your reply profile was updated.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed to save', variant: 'destructive' });
    },
  });

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <div>
        <h1 className="text-3xl font-bold flex items-center gap-2">
          <Users className="h-7 w-7" />
          {scopeParam ? `${scopeConfig.moduleLabel} Customer Support` : 'Customer Support'}
        </h1>
        <p className="text-slate-600 mt-1">
          {scopeParam ? `Agent tools for the ${scopeConfig.listTitle.toLowerCase()} branch.` : 'Agent tools and preferences for communicating with requesters.'}
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Reply signature</CardTitle>
          <CardDescription>Automatically append a signature to requester-facing replies.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {isLoading ? <div className="text-sm text-slate-600">Loading…</div> : null}

          <div className="flex items-center justify-between gap-4 rounded-md border bg-white p-3">
            <div>
              <div className="font-medium text-slate-900">Enable signature</div>
              <div className="text-xs text-slate-500">If disabled, nothing is appended to replies.</div>
            </div>
            <Switch checked={form.isSignatureEnabled} onCheckedChange={(v) => setForm((f) => ({ ...f, isSignatureEnabled: v }))} />
          </div>

          <div className="flex items-center justify-between gap-4 rounded-md border bg-white p-3">
            <div>
              <div className="font-medium text-slate-900">Append to replies</div>
              <div className="text-xs text-slate-500">Adds the signature when you reply to requesters.</div>
            </div>
            <Switch
              checked={form.appendSignatureToReplies}
              onCheckedChange={(v) => setForm((f) => ({ ...f, appendSignatureToReplies: v }))}
              disabled={!form.isSignatureEnabled}
            />
          </div>

          <div className="space-y-2">
            <Label>Signature text</Label>
            <Textarea
              value={form.signature ?? ''}
              onChange={(e) => setForm((f) => ({ ...f, signature: e.target.value }))}
              rows={6}
              placeholder="Regards,\nSupport Team"
              disabled={!form.isSignatureEnabled}
            />
            <div className="text-xs text-slate-500">Template variables supported in replies: {`{{TicketNumber}}`}, {`{{RequesterName}}`}, {`{{PortalUrl}}`}.</div>
          </div>

          <div className="flex justify-end">
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
