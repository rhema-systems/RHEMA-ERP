'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Save, Trash2, PlugZap, Activity } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { ehcAdminService, type EhcInboundEmailChannelAdmin, type EhcInboundMessagingChannelAdmin, type UpsertEhcInboundEmailChannelAdmin, type UpsertEhcInboundMessagingChannelAdmin } from '@/services/ehcAdminService';
import type { EhcTicketPriority, EhcTicketType } from '@/services/ehcTicketService';

export default function HelpdeskChannelsAdminPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const { data, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'channels', 'email-inbound'],
    queryFn: () => ehcAdminService.listInboundEmailChannels(),
  });

  const { data: msgData, isLoading: msgLoading, error: msgError } = useQuery({
    queryKey: ['ehc', 'admin', 'channels', 'messaging-inbound'],
    queryFn: () => ehcAdminService.listInboundMessagingChannels(),
  });

  const flatCategories = useMemo(() => {
    const items = categories || [];
    const byParent: Record<string, typeof items> = {};
    for (const c of items) {
      const p = c.parentCategoryId || 'root';
      byParent[p] = byParent[p] || [];
      byParent[p].push(c);
    }
    Object.keys(byParent).forEach((k) => byParent[k].sort((a, b) => a.name.localeCompare(b.name)));

    const out: Array<{ id: string; label: string }> = [];
    const walk = (parent: string, depth: number) => {
      for (const c of byParent[parent] || []) {
        out.push({ id: c.id, label: `${'—'.repeat(depth)}${depth ? ' ' : ''}${c.name}` });
        walk(c.id, depth + 1);
      }
    };
    walk('root', 0);
    return out;
  }, [categories]);

  const [editing, setEditing] = useState<EhcInboundEmailChannelAdmin | null>(null);
  const [form, setForm] = useState<UpsertEhcInboundEmailChannelAdmin>({
    mailboxAddress: '',
    folderName: 'Inbox',
    isEnabled: true,
    requireKnownSender: true,
    autoProvisionUnknownSenders: false,
    defaultCategoryId: '',
    defaultTicketType: 'Helpdesk',
    defaultPriority: 'Medium',
    useGraphWebhook: false,
  });

  const [editingMsg, setEditingMsg] = useState<EhcInboundMessagingChannelAdmin | null>(null);
  const [msgForm, setMsgForm] = useState<UpsertEhcInboundMessagingChannelAdmin>({
    provider: 'Twilio',
    source: 'Sms',
    toAddress: '',
    isEnabled: true,
    requireKnownSender: true,
    autoProvisionUnknownSenders: false,
    defaultCategoryId: '',
    defaultTicketType: 'Helpdesk',
    defaultPriority: 'Medium',
  } as any);

  const save = useMutation({
    mutationFn: async () => {
      const payload: UpsertEhcInboundEmailChannelAdmin = {
        mailboxAddress: form.mailboxAddress.trim(),
        folderName: 'Inbox',
        isEnabled: Boolean(form.isEnabled),
        requireKnownSender: Boolean(form.requireKnownSender),
        autoProvisionUnknownSenders: Boolean(form.autoProvisionUnknownSenders),
        defaultCategoryId: form.defaultCategoryId || null,
        defaultTicketType: (form.defaultTicketType as any) || 'Helpdesk',
        defaultPriority: (form.defaultPriority as any) || 'Medium',
        useGraphWebhook: Boolean(form.useGraphWebhook),
      };
      if (editing) return ehcAdminService.updateInboundEmailChannel(editing.id, payload);
      return ehcAdminService.createInboundEmailChannel(payload);
    },
    onSuccess: async () => {
      setEditing(null);
      setForm({
        mailboxAddress: '',
        folderName: 'Inbox',
        isEnabled: true,
        requireKnownSender: true,
        autoProvisionUnknownSenders: false,
        defaultCategoryId: '',
        defaultTicketType: 'Helpdesk',
        defaultPriority: 'Medium',
        useGraphWebhook: false,
      });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'channels', 'email-inbound'] });
    },
  });

  const del = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteInboundEmailChannel(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'channels', 'email-inbound'] });
    },
  });

  const saveMsg = useMutation({
    mutationFn: async () => {
      const payload: UpsertEhcInboundMessagingChannelAdmin = {
        provider: (msgForm.provider as any) || 'Twilio',
        source: (msgForm.source as any) || 'Sms',
        toAddress: msgForm.toAddress.trim(),
        isEnabled: Boolean(msgForm.isEnabled),
        requireKnownSender: Boolean(msgForm.requireKnownSender),
        autoProvisionUnknownSenders: Boolean(msgForm.autoProvisionUnknownSenders),
        defaultCategoryId: (msgForm.defaultCategoryId as any) || null,
        defaultTicketType: (msgForm.defaultTicketType as any) || 'Helpdesk',
        defaultPriority: (msgForm.defaultPriority as any) || 'Medium',
      };
      if (editingMsg) return ehcAdminService.updateInboundMessagingChannel(editingMsg.id, payload);
      return ehcAdminService.createInboundMessagingChannel(payload);
    },
    onSuccess: async () => {
      setEditingMsg(null);
      setMsgForm({
        provider: 'Twilio',
        source: 'Sms',
        toAddress: '',
        isEnabled: true,
        requireKnownSender: true,
        autoProvisionUnknownSenders: false,
        defaultCategoryId: '',
        defaultTicketType: 'Helpdesk',
        defaultPriority: 'Medium',
      } as any);
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'channels', 'messaging-inbound'] });
    },
  });

  const delMsg = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteInboundMessagingChannel(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'channels', 'messaging-inbound'] });
    },
  });

  const startEdit = (c: EhcInboundEmailChannelAdmin) => {
    setEditing(c);
    setForm({
      mailboxAddress: c.mailboxAddress,
      folderName: c.folderName,
      isEnabled: c.isEnabled,
      requireKnownSender: c.requireKnownSender,
      autoProvisionUnknownSenders: c.autoProvisionUnknownSenders,
      defaultCategoryId: c.defaultCategoryId || '',
      defaultTicketType: (c.defaultTicketType as any) || 'Helpdesk',
      defaultPriority: (c.defaultPriority as any) || 'Medium',
      useGraphWebhook: Boolean(c.useGraphWebhook),
    });
  };

  const startEditMsg = (c: EhcInboundMessagingChannelAdmin) => {
    setEditingMsg(c);
    setMsgForm({
      provider: (c.provider as any) || 'Twilio',
      source: (c.source as any) || 'Sms',
      toAddress: c.toAddress,
      isEnabled: c.isEnabled,
      requireKnownSender: c.requireKnownSender,
      autoProvisionUnknownSenders: c.autoProvisionUnknownSenders,
      defaultCategoryId: c.defaultCategoryId || '',
      defaultTicketType: (c.defaultTicketType as any) || 'Helpdesk',
      defaultPriority: (c.defaultPriority as any) || 'Medium',
    } as any);
  };

  const test = useMutation({
    mutationFn: (id: string) => ehcAdminService.testInboundEmailChannel(id),
    onSuccess: (data, id) => {
      toast({ title: 'Mailbox test succeeded', description: `Inbox reachable for channel ${id}.` });
    },
    onError: (e: any) => {
      toast({ title: 'Mailbox test failed', description: e?.message || 'Failed to test mailbox.', variant: 'destructive' as any });
    },
  });

  const subscribe = useMutation({
    mutationFn: (id: string) => ehcAdminService.subscribeInboundEmailWebhook(id),
    onSuccess: async () => {
      toast({ title: 'Webhook subscribed', description: 'Graph subscription created/updated.' });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'channels', 'email-inbound'] });
    },
    onError: (e: any) => {
      toast({ title: 'Webhook subscription failed', description: e?.message || 'Failed to subscribe.', variant: 'destructive' as any });
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Support Channels</h1>
        <p className="text-slate-600 mt-1">Configure inbound email (Microsoft 365) for email-to-ticket.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Edit inbound email channel' : 'Add inbound email channel'}</CardTitle>
          <CardDescription>Graph polling uses the global `Microsoft365InboundEmail` settings in the API configuration.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Mailbox address</Label>
              <Input value={form.mailboxAddress} onChange={(e) => setForm((f) => ({ ...f, mailboxAddress: e.target.value }))} placeholder="support@yourdomain.com" />
            </div>
            <div className="space-y-2">
              <Label>Enabled</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.isEnabled ? 'true' : 'false'}
                onChange={(e) => setForm((f) => ({ ...f, isEnabled: e.target.value === 'true' }))}
              >
                <option value="true">Enabled</option>
                <option value="false">Disabled</option>
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Default ticket type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.defaultTicketType}
                onChange={(e) => setForm((f) => ({ ...f, defaultTicketType: e.target.value as EhcTicketType }))}
              >
                <option value="Enquiry">Enquiry</option>
                <option value="Complaint">Complaint</option>
                <option value="Helpdesk">Helpdesk</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Default priority</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.defaultPriority}
                onChange={(e) => setForm((f) => ({ ...f, defaultPriority: e.target.value as EhcTicketPriority }))}
              >
                <option value="Low">Low</option>
                <option value="Medium">Medium</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>Default category</Label>
            <select
              className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={form.defaultCategoryId || ''}
              onChange={(e) => setForm((f) => ({ ...f, defaultCategoryId: e.target.value || '' }))}
            >
              <option value="">Select category…</option>
              {flatCategories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.label}
                </option>
              ))}
            </select>
            <div className="text-xs text-slate-500">Inbound email requires a default category so tickets can be created consistently.</div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Require known sender</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.requireKnownSender ? 'true' : 'false'}
                onChange={(e) => setForm((f) => ({ ...f, requireKnownSender: e.target.value === 'true' }))}
              >
                <option value="true">Yes (recommended)</option>
                <option value="false">No</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Auto-provision unknown senders</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.autoProvisionUnknownSenders ? 'true' : 'false'}
                onChange={(e) => setForm((f) => ({ ...f, autoProvisionUnknownSenders: e.target.value === 'true' }))}
              >
                <option value="false">No (recommended)</option>
                <option value="true">Yes</option>
              </select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>Webhook mode (optional)</Label>
            <select
              className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={form.useGraphWebhook ? 'true' : 'false'}
              onChange={(e) => setForm((f) => ({ ...f, useGraphWebhook: e.target.value === 'true' }))}
            >
              <option value="false">Delta polling</option>
              <option value="true">Graph webhook + delta (recommended when public URL exists)</option>
            </select>
            <div className="text-xs text-slate-500">Webhook subscriptions require `Microsoft365InboundEmail.WebhookBaseUrl` on the API.</div>
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.mailboxAddress.trim() || !form.defaultCategoryId}>
              <Save className="w-4 h-4 mr-2" /> {save.isPending ? 'Saving…' : editing ? 'Save' : 'Add'}
            </Button>
            {editing ? (
              <Button variant="outline" onClick={() => setEditing(null)}>
                Cancel
              </Button>
            ) : null}
          </div>
          {save.isError ? <div className="text-sm text-red-600">Failed to save.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Inbound email channels</CardTitle>
          <CardDescription>{isLoading ? 'Loading…' : `${(data || []).length} item(s)`}</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? (
            <div className="text-sm text-red-600">Failed to load.</div>
          ) : (
            <div className="space-y-2">
              {(data || []).map((c) => (
                <div key={c.id} className="flex items-center justify-between rounded-md border p-3">
                  <div className="space-y-1">
                    <div className="font-medium">{c.mailboxAddress}</div>
                    <div className="text-xs text-slate-500">
                      {c.isEnabled ? 'Enabled' : 'Disabled'} · {c.defaultTicketType} · {c.defaultPriority}
                    </div>
                    {c.useGraphWebhook ? (
                      <div className="text-xs text-slate-500">
                        Webhook: {c.graphSubscriptionId ? 'Subscribed' : 'Not subscribed'}
                        {c.graphSubscriptionExpiresAtUtc ? ` · Expires: ${new Date(c.graphSubscriptionExpiresAtUtc).toLocaleString()}` : ''}
                      </div>
                    ) : (
                      <div className="text-xs text-slate-500">Mode: Delta polling</div>
                    )}
                    {c.lastSyncedAtUtc ? <div className="text-xs text-slate-500">Last sync: {new Date(c.lastSyncedAtUtc).toLocaleString()}</div> : null}
                    {c.lastAttemptAtUtc ? <div className="text-xs text-slate-500">Last attempt: {new Date(c.lastAttemptAtUtc).toLocaleString()}</div> : null}
                    {c.lastSuccessAtUtc ? <div className="text-xs text-slate-500">Last success: {new Date(c.lastSuccessAtUtc).toLocaleString()}</div> : null}
                    {typeof c.lastProcessedMessageCount === 'number' ? <div className="text-xs text-slate-500">Last processed: {c.lastProcessedMessageCount}</div> : null}
                    {typeof c.consecutiveFailureCount === 'number' && c.consecutiveFailureCount > 0 ? (
                      <div className="text-xs text-red-600">Consecutive failures: {c.consecutiveFailureCount}</div>
                    ) : null}
                    {c.lastError ? <div className="text-xs text-red-600">Last error: {c.lastError}</div> : null}
                  </div>
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={() => test.mutate(c.id)} disabled={test.isPending}>
                      <Activity className="w-4 h-4 mr-1" /> Test
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => subscribe.mutate(c.id)}
                      disabled={subscribe.isPending || !c.useGraphWebhook}
                      title={!c.useGraphWebhook ? 'Enable webhook mode to subscribe' : 'Create/renew Graph subscription'}
                    >
                      <PlugZap className="w-4 h-4 mr-1" /> Subscribe
                    </Button>
                    <Button variant="outline" size="sm" onClick={() => startEdit(c)}>
                      <Pencil className="w-4 h-4 mr-1" /> Edit
                    </Button>
                    <Button variant="destructive" size="sm" onClick={() => del.mutate(c.id)} disabled={del.isPending}>
                      <Trash2 className="w-4 h-4 mr-1" /> Delete
                    </Button>
                  </div>
                </div>
              ))}
              {!data?.length ? <div className="text-sm text-slate-600">No channels configured.</div> : null}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{editingMsg ? 'Edit inbound messaging channel' : 'Add inbound messaging channel'}</CardTitle>
          <CardDescription>Configure SMS/WhatsApp inbound routing (Twilio webhook).</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Source</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={msgForm.source}
                onChange={(e) => setMsgForm((f) => ({ ...f, source: e.target.value as any }))}
              >
                <option value="Sms">SMS</option>
                <option value="WhatsApp">WhatsApp</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>To number</Label>
              <Input value={msgForm.toAddress} onChange={(e) => setMsgForm((f) => ({ ...f, toAddress: e.target.value }))} placeholder="e.g. +15551234567" />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Default ticket type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={msgForm.defaultTicketType}
                onChange={(e) => setMsgForm((f) => ({ ...f, defaultTicketType: e.target.value as EhcTicketType }))}
              >
                <option value="Enquiry">Enquiry</option>
                <option value="Complaint">Complaint</option>
                <option value="Helpdesk">Helpdesk</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Default priority</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={msgForm.defaultPriority}
                onChange={(e) => setMsgForm((f) => ({ ...f, defaultPriority: e.target.value as EhcTicketPriority }))}
              >
                <option value="Low">Low</option>
                <option value="Medium">Medium</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>Default category</Label>
            <select
              className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={msgForm.defaultCategoryId || ''}
              onChange={(e) => setMsgForm((f) => ({ ...f, defaultCategoryId: e.target.value || '' }))}
            >
              <option value="">Select category…</option>
              {flatCategories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.label}
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Require known sender</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={msgForm.requireKnownSender ? 'true' : 'false'}
                onChange={(e) => setMsgForm((f) => ({ ...f, requireKnownSender: e.target.value === 'true' }))}
              >
                <option value="true">Yes (recommended)</option>
                <option value="false">No</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Auto-provision unknown senders</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={msgForm.autoProvisionUnknownSenders ? 'true' : 'false'}
                onChange={(e) => setMsgForm((f) => ({ ...f, autoProvisionUnknownSenders: e.target.value === 'true' }))}
              >
                <option value="false">No (recommended)</option>
                <option value="true">Yes</option>
              </select>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => saveMsg.mutate()} disabled={saveMsg.isPending || !msgForm.toAddress.trim() || !msgForm.defaultCategoryId}>
              <Save className="w-4 h-4 mr-2" /> {saveMsg.isPending ? 'Saving…' : editingMsg ? 'Save' : 'Add'}
            </Button>
            {editingMsg ? (
              <Button variant="outline" onClick={() => setEditingMsg(null)}>
                Cancel
              </Button>
            ) : null}
          </div>
          {saveMsg.isError ? <div className="text-sm text-red-600">Failed to save.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Inbound messaging channels</CardTitle>
          <CardDescription>{msgLoading ? 'Loading…' : `${(msgData || []).length} item(s)`}</CardDescription>
        </CardHeader>
        <CardContent>
          {msgError ? (
            <div className="text-sm text-red-600">Failed to load.</div>
          ) : (
            <div className="space-y-2">
              {(msgData || []).map((c) => (
                <div key={c.id} className="flex items-center justify-between rounded-md border p-3">
                  <div className="space-y-1">
                    <div className="font-medium">{c.source} · {c.toAddress}</div>
                    <div className="text-xs text-slate-500">
                      {c.isEnabled ? 'Enabled' : 'Disabled'} · {c.defaultTicketType} · {c.defaultPriority}
                    </div>
                    <div className="text-xs text-slate-500">Provider: {c.provider}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={() => startEditMsg(c)}>
                      <Pencil className="w-4 h-4 mr-1" /> Edit
                    </Button>
                    <Button variant="destructive" size="sm" onClick={() => delMsg.mutate(c.id)} disabled={delMsg.isPending}>
                      <Trash2 className="w-4 h-4 mr-1" /> Delete
                    </Button>
                  </div>
                </div>
              ))}
              {!msgData?.length ? <div className="text-sm text-slate-600">No messaging channels configured.</div> : null}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
