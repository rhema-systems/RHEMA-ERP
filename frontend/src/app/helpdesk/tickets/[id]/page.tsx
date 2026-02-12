'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ExternalLink, Paperclip, Send, Ticket } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';
import { ehcInternalTicketService, type EhcAllowedTicketTransition } from '@/services/ehcInternalTicketService';
import { useSignalR } from '@/hooks/useSignalR';
import type { EhcTicketAttachment, EhcTicketStatus } from '@/services/ehcTicketService';
import { fileUploadService } from '@/services/fileUploadService';

const toDisplayUrl = (attachment: EhcTicketAttachment) => {
  const backendBaseUrl = (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api').replace('/api', '');
  const raw = attachment.publicUrl || attachment.filePath;
  if (!raw) return null;
  if (raw.startsWith('/uploads')) return `${backendBaseUrl}${raw}`;
  return raw;
};

const relatedResolveSupportedTypes = new Set([
  'Asset',
  'Vehicle',
  'WorkOrder',
  'PurchaseOrder',
  'PurchaseRequisition',
  'Tender',
  'RFQ',
]);

export default function HelpdeskTicketDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const ticketId = params?.id;
  const qc = useQueryClient();
  const { toast } = useToast();

  const [nowTs, setNowTs] = useState(() => Date.now());
  const [assignedDepartmentId, setAssignedDepartmentId] = useState<string>('');
  const [assignedToUserId, setAssignedToUserId] = useState<string>('');
  const [targetStatus, setTargetStatus] = useState<EhcTicketStatus>('InProgress');
  const [workflowTransitionId, setWorkflowTransitionId] = useState<string | null>(null);
  const [workflowTransitionName, setWorkflowTransitionName] = useState<string | null>(null);
  const [transitionNotes, setTransitionNotes] = useState('');
  const [internalComment, setInternalComment] = useState('');
  const [externalReply, setExternalReply] = useState('');
  const [attachmentFile, setAttachmentFile] = useState<File | null>(null);
  const [attachmentInternalOnly, setAttachmentInternalOnly] = useState(false);

  const getErrorMessage = (err: unknown) => (err instanceof Error ? err.message : 'Something went wrong');

  const { data: ticket, isLoading, error } = useQuery({
    queryKey: ['ehc', 'internal', 'ticket', ticketId],
    queryFn: () => ehcInternalTicketService.getTicket(ticketId),
    enabled: Boolean(ticketId),
  });

  const { data: allowedTransitions } = useQuery({
    queryKey: ['ehc', 'internal', 'ticket', ticketId, 'allowed-transitions'],
    queryFn: () => ehcInternalTicketService.getAllowedTransitions(ticketId),
    enabled: Boolean(ticketId),
  });

  const { data: departments } = useQuery({
    queryKey: ['ehc', 'internal', 'departments'],
    queryFn: () => ehcInternalTicketService.listDepartments(),
  });

  const { data: myDepartment } = useQuery({
    queryKey: ['ehc', 'internal', 'my-department'],
    queryFn: () => ehcInternalTicketService.getMyDepartment(),
  });

  const { data: agents } = useQuery({
    queryKey: ['ehc', 'internal', 'agents'],
    queryFn: () => ehcInternalTicketService.listAgents(),
  });

  useEffect(() => {
    const id = setInterval(() => setNowTs(Date.now()), 30_000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (!ticket) return;

    setAssignedDepartmentId((prev) => prev || ticket.assignedDepartmentId || myDepartment?.id || '');
    setAssignedToUserId((prev) => prev || ticket.assignedToUserId || '');
  }, [ticket, myDepartment?.id]);

  const allowedTransitionOptions = useMemo(() => {
    const items = (allowedTransitions || []) as EhcAllowedTicketTransition[];
    return items
      .filter((t) => t && t.targetStatus)
      .map((t) => ({
        value: t.transitionId ? String(t.transitionId) : `status:${t.targetStatus}`,
        label: t.transitionName ? `${t.transitionName} → ${t.targetStatus}` : String(t.targetStatus),
        transitionId: t.transitionId || null,
        transitionName: t.transitionName || null,
        transitionDescription: t.transitionDescription || null,
        targetStatus: t.targetStatus,
      }));
  }, [allowedTransitions]);

  const selectedTransitionMeta = useMemo(() => {
    if (!allowedTransitionOptions.length) return null;

    if (workflowTransitionId) {
      return allowedTransitionOptions.find((o) => o.transitionId && String(o.transitionId) === String(workflowTransitionId)) || null;
    }

    return allowedTransitionOptions.find((o) => o.transitionId == null && o.targetStatus === targetStatus) || null;
  }, [allowedTransitionOptions, targetStatus, workflowTransitionId]);

  useEffect(() => {
    if (!allowedTransitionOptions.length) return;
    const first = allowedTransitionOptions[0];
    setWorkflowTransitionId(first.transitionId || null);
    setWorkflowTransitionName(first.transitionName || null);
    setTargetStatus(first.targetStatus);
  }, [allowedTransitionOptions]);

  const handleRealtimeNotification = useCallback(
    (n: any) => {
      if (!ticketId) return;

      const entityType = (n?.entityType ?? n?.EntityType ?? null) as string | null;
      const entityId = (n?.entityId ?? n?.EntityId ?? null) as string | null;
      const isTicket =
        typeof entityType === 'string' &&
        entityType.toLowerCase() === 'ehcticket' &&
        typeof entityId === 'string' &&
        entityId.toLowerCase() === String(ticketId).toLowerCase();

      const actionUrl = (n?.actionUrl ?? n?.ActionUrl ?? '') as string;
      const urlMatch = actionUrl && actionUrl.includes(String(ticketId));

      if (!isTicket && !urlMatch) return;

      toast({ title: 'Ticket updated', description: n?.title || n?.Title || 'An update was received.', variant: 'success' });
      qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'allowed-transitions'] });
    },
    [qc, ticketId, toast]
  );

  useSignalR({
    autoConnect: true,
    onNotification: handleRealtimeNotification,
  });

  const assign = useMutation({
    mutationFn: async () => {
      if (!assignedToUserId) throw new Error('Assignee required');
      await ehcInternalTicketService.assignTicket(ticketId, assignedToUserId, assignedDepartmentId || null);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'allowed-transitions'] });
      toast({ title: 'Saved', description: 'Assignment updated.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const transition = useMutation({
    mutationFn: async () => {
      await ehcInternalTicketService.transitionTicket(ticketId, targetStatus, transitionNotes || null, workflowTransitionId, workflowTransitionName);
    },
    onSuccess: async () => {
      setTransitionNotes('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'allowed-transitions'] });
      toast({ title: 'Saved', description: 'Status updated.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const addComment = useMutation({
    mutationFn: async () => {
      const body = internalComment.trim();
      if (!body) throw new Error('Comment required');
      return ehcInternalTicketService.addInternalComment(ticketId, body);
    },
    onSuccess: async () => {
      setInternalComment('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      toast({ title: 'Saved', description: 'Internal note added.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const messages = useMemo(() => ticket?.messages ?? [], [ticket?.messages]);
  const statusHistory = useMemo(() => ticket?.statusHistory ?? [], [ticket?.statusHistory]);
  const attachments = useMemo(() => ticket?.attachments ?? [], [ticket?.attachments]);
  const dateTimeFormatter = useMemo(() => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }), []);
  const formatDateTime = (iso: string | null | undefined) => {
    if (!iso) return '—';
    const dt = new Date(iso);
    if (Number.isNaN(dt.getTime())) return String(iso);
    return dateTimeFormatter.format(dt);
  };

  const now = useMemo(() => new Date(nowTs), [nowTs]);

  const formatCountdown = (ms: number) => {
    const abs = Math.abs(ms);
    const totalMinutes = Math.round(abs / 60000);
    if (totalMinutes < 60) return `${totalMinutes}m`;
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return minutes ? `${hours}h ${minutes}m` : `${hours}h`;
  };

  const firstResponse = useMemo(() => {
    const dueIso = ticket?.firstResponseDueAt || null;
    const doneIso = ticket?.firstRespondedAt || null;
    if (!dueIso) return { badge: 'bg-slate-600 text-white', label: 'Not set', due: '—', detail: 'No SLA template applied.' };

    const due = new Date(dueIso);
    const dueText = formatDateTime(dueIso);
    if (doneIso) return { badge: 'bg-green-600 text-white', label: 'Met', due: dueText, detail: `First responded at ${formatDateTime(doneIso)}.` };

    const diff = due.getTime() - now.getTime();
    if (diff <= 0) return { badge: 'bg-red-600 text-white', label: 'Overdue', due: dueText, detail: `Overdue by ${formatCountdown(diff)}.` };
    const minsLeft = diff / 60000;
    if (minsLeft <= 15) return { badge: 'bg-amber-500 text-white', label: 'Due soon', due: dueText, detail: `Due in ${formatCountdown(diff)}.` };
    return { badge: 'bg-blue-600 text-white', label: 'On track', due: dueText, detail: `Due in ${formatCountdown(diff)}.` };
  }, [ticket?.firstResponseDueAt, ticket?.firstRespondedAt, now, dateTimeFormatter]);

  const resolution = useMemo(() => {
    const dueIso = ticket?.resolutionDueAt || null;
    const doneIso = ticket?.resolvedAt || null;
    if (!dueIso) return { badge: 'bg-slate-600 text-white', label: 'Not set', due: '—', detail: 'No SLA template applied.' };

    const due = new Date(dueIso);
    const dueText = formatDateTime(dueIso);
    if (doneIso) return { badge: 'bg-green-600 text-white', label: 'Met', due: dueText, detail: `Resolved at ${formatDateTime(doneIso)}.` };

    const diff = due.getTime() - now.getTime();
    if (diff <= 0) return { badge: 'bg-red-600 text-white', label: 'Overdue', due: dueText, detail: `Overdue by ${formatCountdown(diff)}.` };
    const minsLeft = diff / 60000;
    if (minsLeft <= 60) return { badge: 'bg-amber-500 text-white', label: 'Due soon', due: dueText, detail: `Due in ${formatCountdown(diff)}.` };
    return { badge: 'bg-blue-600 text-white', label: 'On track', due: dueText, detail: `Due in ${formatCountdown(diff)}.` };
  }, [ticket?.resolutionDueAt, ticket?.resolvedAt, now, dateTimeFormatter]);

  const auditEvents = useMemo(() => {
    if (!ticket) return [];

    const humanizeEventType = (eventType: string) =>
      (eventType || 'Event')
        .replace(/[_\.]+/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();

    if (ticket.auditTrail?.length) {
      return [...ticket.auditTrail]
        .filter((x) => !!x.createdAt)
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .map((e) => {
          const title = (e.title || '').trim() || humanizeEventType(e.eventType);
          const bits = [
            e.actorName ? `By ${e.actorName}` : null,
            e.body ? e.body : null,
            e.isInternal ? 'Internal' : null,
          ].filter(Boolean) as string[];

          return {
            id: `audit:${e.id}`,
            at: e.createdAt,
            title,
            detail: bits.join(' • ') || undefined,
            tone: e.isInternal ? 'warning' : 'neutral',
          };
        });
    }

    const ev: Array<{ id: string; at: string; title: string; detail?: string; tone?: 'neutral' | 'info' | 'warning' }> = [];

    ev.push({
      id: `created:${ticket.id}`,
      at: ticket.createdAt,
      title: 'Ticket created',
      detail: ticket.createdBy ? `By ${ticket.createdBy}` : undefined,
      tone: 'info',
    });

    for (const h of statusHistory) {
      ev.push({
        id: `status:${h.id}`,
        at: h.createdAt,
        title: h.fromStatus ? `Status: ${h.fromStatus} → ${h.toStatus}` : `Status: ${h.toStatus}`,
        detail: [h.changedByName ? `By ${h.changedByName}` : null, h.notes ? `Notes: ${h.notes}` : null].filter(Boolean).join(' • ') || undefined,
        tone: h.toStatus === 'Resolved' || h.toStatus === 'Closed' ? 'info' : 'neutral',
      });
    }

    for (const m of messages) {
      ev.push({
        id: `msg:${m.id}`,
        at: m.createdAt,
        title: m.isInternal ? 'Internal note added' : 'Message sent',
        detail: [m.authorName ? `By ${m.authorName}` : null, m.body ? `"${m.body.slice(0, 80)}${m.body.length > 80 ? '…' : ''}"` : null].filter(Boolean).join(' • ') || undefined,
        tone: m.isInternal ? 'warning' : 'neutral',
      });
    }

    for (const a of attachments) {
      ev.push({
        id: `att:${a.id}`,
        at: a.createdAt,
        title: a.isInternal ? 'Internal attachment uploaded' : 'Attachment uploaded',
        detail: [a.createdBy ? `By ${a.createdBy}` : null, a.fileName].filter(Boolean).join(' • ') || undefined,
        tone: a.isInternal ? 'warning' : 'neutral',
      });
    }

    return ev
      .filter((x) => !!x.at)
      .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime());
  }, [ticket, statusHistory, messages, attachments]);

  const addAgentMessage = useMutation({
    mutationFn: async () => {
      const body = externalReply.trim();
      if (!body) throw new Error('Message is required');
      return ehcInternalTicketService.addMessage(ticketId, body);
    },
    onSuccess: async () => {
      setExternalReply('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      toast({ title: 'Sent', description: 'Reply sent to requester.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const addAttachment = useMutation({
    mutationFn: async () => {
      if (!attachmentFile) throw new Error('File is required');
      const uploaded = await fileUploadService.uploadFile(attachmentFile, 'ehc-ticket', ticketId);
      if (!uploaded.filePath) throw new Error('Upload did not return filePath');

      return ehcInternalTicketService.addAttachment(ticketId, {
        filePath: uploaded.filePath,
        fileName: uploaded.originalName || uploaded.filename,
        contentType: uploaded.mimeType,
        fileSize: uploaded.size,
        isInternal: attachmentInternalOnly,
      });
    },
    onSuccess: async () => {
      setAttachmentFile(null);
      setAttachmentInternalOnly(false);
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      toast({ title: 'Uploaded', description: 'Attachment added.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const resolveRelated = useMutation({
    mutationFn: async () => {
      const type = (ticket?.relatedEntityType || '').trim();
      const reference = (ticket?.relatedEntityReference || '').trim();
      if (!type || !reference) throw new Error('No related entity to open');
      if (!relatedResolveSupportedTypes.has(type)) throw new Error(`Related entity type '${type}' is not supported yet.`);
      return ehcInternalTicketService.resolveRelatedEntity(type, reference);
    },
    onSuccess: (res) => {
      if (res?.exists && res.openUrl) {
        window.open(res.openUrl, '_blank', 'noopener,noreferrer');
        return;
      }
      toast({ title: 'Not found', description: 'Could not resolve the related record for this reference.', variant: 'destructive' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const canOpenRelated =
    Boolean(ticket?.relatedEntityType) &&
    Boolean(ticket?.relatedEntityReference) &&
    relatedResolveSupportedTypes.has((ticket?.relatedEntityType || '').trim());

  if (isLoading) {
    return (
      <Card>
        <CardContent className="p-6 text-slate-600">Loading...</CardContent>
      </Card>
    );
  }

  if (error || !ticket) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Error</CardTitle>
          <CardDescription>Failed to load ticket.</CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline" onClick={() => router.push('/helpdesk/tickets')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <Button variant="outline" onClick={() => router.push('/helpdesk/tickets')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <h1 className="text-3xl font-bold flex items-center gap-2 mt-4">
            <Ticket className="h-7 w-7" />
            {ticket.ticketNumber}
          </h1>
          <p className="text-slate-600 mt-1">
            Status: {ticket.status} • {ticket.ticketType} • {ticket.priority}
          </p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
          <CardDescription>Requester-facing information.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
            <div>
              <div className="text-slate-500">Submitted by</div>
              <div className="font-medium text-slate-900">{ticket.requesterName || '—'}</div>
              {ticket.requesterAuthenticationProvider ? (
                <div className="text-xs text-slate-500">{ticket.requesterAuthenticationProvider === 'Local' ? 'External Portal' : 'Internal ERP'}</div>
              ) : null}
            </div>
            <div>
              <div className="text-slate-500">Requester email</div>
              <div className="font-medium text-slate-900">{ticket.requesterEmail || '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Category</div>
              <div className="font-medium text-slate-900">{ticket.categoryName || '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Subcategory</div>
              <div className="font-medium text-slate-900">{ticket.subcategoryName || '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Source</div>
              <div className="font-medium text-slate-900">{ticket.source === 'Web' ? 'Website' : ticket.source}</div>
            </div>
            <div>
              <div className="text-slate-500">Related Entity</div>
              <div className="font-medium text-slate-900">{ticket.relatedEntityType || '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Reference</div>
              <div className="font-medium text-slate-900 flex items-center gap-2 flex-wrap">
                <span>{ticket.relatedEntityReference || '—'}</span>
                {canOpenRelated ? (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => resolveRelated.mutate()}
                    disabled={resolveRelated.isPending}
                    title="Open related record"
                  >
                    <ExternalLink className="h-4 w-4 mr-2" />
                    {resolveRelated.isPending ? 'Opening…' : 'Open'}
                  </Button>
                ) : null}
              </div>
            </div>
            <div>
              <div className="text-slate-500">Created</div>
              <div className="font-medium text-slate-900">{formatDateTime(ticket.createdAt)}</div>
              <div className="text-xs text-slate-500">{ticket.createdBy || '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Last updated</div>
              <div className="font-medium text-slate-900">{formatDateTime(ticket.updatedAt)}</div>
              <div className="text-xs text-slate-500">{ticket.updatedBy || '—'}</div>
            </div>
          </div>

          <div className="text-sm text-slate-900 whitespace-pre-wrap">{ticket.description}</div>

          {attachments.length ? (
            <div className="space-y-2 pt-2">
              <div className="font-medium text-slate-900 flex items-center gap-2">
                <Paperclip className="h-4 w-4" />
                Attachments
              </div>
              <div className="space-y-1">
                {attachments.map((a) => {
                  const url = toDisplayUrl(a);
                  return (
                    <div key={a.id} className="text-sm">
                      <span className={`mr-2 text-[11px] px-2 py-0.5 rounded-full border ${a.isInternal ? 'bg-amber-50 text-amber-800' : 'bg-blue-50 text-blue-700'}`}>
                        {a.isInternal ? 'Internal' : 'External'}
                      </span>
                      {url ? (
                        <a className="text-blue-700 hover:underline" href={url} target="_blank" rel="noreferrer">
                          {a.fileName}
                        </a>
                      ) : (
                        <span>{a.fileName}</span>
                      )}
                      <span className="text-slate-500 ml-2">({Math.round((a.fileSize / 1024) * 10) / 10} KB)</span>
                      <span className="text-slate-500 ml-2">• {formatDateTime(a.createdAt)}</span>
                      {a.createdBy ? <span className="text-slate-500 ml-2">• {a.createdBy}</span> : null}
                    </div>
                  );
                })}
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Tracking</CardTitle>
          <CardDescription>SLA timers and audit trail for accountability.</CardDescription>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="sla">
            <TabsList>
              <TabsTrigger value="sla">SLA</TabsTrigger>
              <TabsTrigger value="audit">Audit trail</TabsTrigger>
            </TabsList>

            <TabsContent value="sla" className="space-y-4">
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <div className="rounded-lg border bg-white p-4">
                  <div className="flex items-center justify-between gap-2">
                    <div className="font-medium text-slate-900">First response</div>
                    <Badge className={firstResponse.badge}>{firstResponse.label}</Badge>
                  </div>
                  <div className="mt-2 text-sm text-slate-700">
                    <div>
                      <span className="text-slate-500">Due:</span> <span className="font-medium">{firstResponse.due}</span>
                    </div>
                    <div className="text-slate-600 mt-1">{firstResponse.detail}</div>
                  </div>
                </div>

                <div className="rounded-lg border bg-white p-4">
                  <div className="flex items-center justify-between gap-2">
                    <div className="font-medium text-slate-900">Resolution</div>
                    <Badge className={resolution.badge}>{resolution.label}</Badge>
                  </div>
                  <div className="mt-2 text-sm text-slate-700">
                    <div>
                      <span className="text-slate-500">Due:</span> <span className="font-medium">{resolution.due}</span>
                    </div>
                    <div className="text-slate-600 mt-1">{resolution.detail}</div>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
                <div>
                  <div className="text-slate-500">First responded at</div>
                  <div className="font-medium text-slate-900">{formatDateTime(ticket.firstRespondedAt)}</div>
                </div>
                <div>
                  <div className="text-slate-500">Resolved at</div>
                  <div className="font-medium text-slate-900">{formatDateTime(ticket.resolvedAt)}</div>
                </div>
              </div>
            </TabsContent>

            <TabsContent value="audit" className="space-y-2">
              {auditEvents.length ? (
                <div className="space-y-2">
                  {auditEvents.map((e) => (
                    <div key={e.id} className="rounded-md border bg-white p-3">
                      <div className="flex items-center justify-between gap-3">
                        <div className="font-medium text-slate-900">{e.title}</div>
                        <div className="text-xs text-slate-500 whitespace-nowrap">{formatDateTime(e.at)}</div>
                      </div>
                      {e.detail ? <div className="mt-1 text-sm text-slate-700 whitespace-pre-wrap">{e.detail}</div> : null}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-sm text-slate-600">No audit events yet.</div>
              )}
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Assignment</CardTitle>
          <CardDescription>Set department and agent for accountability.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Department</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={assignedDepartmentId}
                onChange={(e) => setAssignedDepartmentId(e.target.value)}
              >
                <option value="">(optional)</option>
                {(departments || []).map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label>Assignee</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={assignedToUserId}
                onChange={(e) => setAssignedToUserId(e.target.value)}
              >
                <option value="">Select agent</option>
                {(agents || []).map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <Button onClick={() => assign.mutate()} disabled={assign.isPending || !assignedToUserId}>
            {assign.isPending ? 'Saving...' : 'Assign'}
          </Button>

          {assign.isError ? <div className="text-sm text-red-600">Failed to assign.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Status</CardTitle>
          <CardDescription>Status changes are enforced by the workflow engine. Only allowed next transitions are shown.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Tabs defaultValue="transition">
            <TabsList>
              <TabsTrigger value="transition">Transition</TabsTrigger>
              <TabsTrigger value="timeline">Timeline</TabsTrigger>
            </TabsList>

            <TabsContent value="transition" className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Next action</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={workflowTransitionId ? workflowTransitionId : `status:${targetStatus}`}
                    onChange={(e) => {
                      const v = e.target.value;
                      if (v.startsWith('status:')) {
                        const s = v.replace('status:', '') as EhcTicketStatus;
                        setWorkflowTransitionId(null);
                        setWorkflowTransitionName(null);
                        setTargetStatus(s);
                        return;
                      }

                      const match = allowedTransitionOptions.find((o) => o.transitionId && String(o.transitionId) === v);
                      setWorkflowTransitionId(v);
                      setWorkflowTransitionName(match?.transitionName || null);
                      setTargetStatus(match?.targetStatus || targetStatus);
                    }}
                    disabled={!allowedTransitionOptions.length}
                  >
                    {allowedTransitionOptions.length ? (
                      allowedTransitionOptions.map((o) => (
                        <option key={o.value} value={o.value}>
                          {o.label}
                        </option>
                      ))
                    ) : (
                      <option value={targetStatus}>No transitions available</option>
                    )}
                  </select>
                  {allowedTransitionOptions.length ? (
                    <div className="space-y-1 text-xs text-slate-500">
                      <div>
                        {selectedTransitionMeta?.transitionName
                          ? `Action: ${selectedTransitionMeta.transitionName}`
                          : workflowTransitionName
                            ? `Action: ${workflowTransitionName}`
                            : 'Next status is driven by the workflow definition.'}
                      </div>
                      {selectedTransitionMeta?.transitionDescription ? <div className="text-slate-600">{selectedTransitionMeta.transitionDescription}</div> : null}
                    </div>
                  ) : (
                    <div className="text-xs text-slate-500">This ticket has no workflow transitions available right now.</div>
                  )}
                </div>
                <div className="space-y-2">
                  <Label>Notes</Label>
                  <Textarea value={transitionNotes} onChange={(e) => setTransitionNotes(e.target.value)} rows={3} placeholder="Optional notes" />
                </div>
              </div>

              <Button onClick={() => transition.mutate()} disabled={transition.isPending || !allowedTransitionOptions.length}>
                {transition.isPending ? 'Updating...' : 'Transition'}
              </Button>

              {transition.isError ? <div className="text-sm text-red-600">Failed to transition.</div> : null}
            </TabsContent>

            <TabsContent value="timeline" className="space-y-2">
              {statusHistory.length ? (
                <div className="space-y-2">
                  {statusHistory.map((h) => (
                    <div key={h.id} className="text-sm">
                      <div className="font-medium text-slate-900">{h.toStatus}</div>
                      <div className="text-slate-600">
                        {formatDateTime(h.createdAt)}
                        {h.changedByName ? ` • ${h.changedByName}` : ''}
                        {h.notes ? ` • ${h.notes}` : ''}
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-sm text-slate-600">No status history yet.</div>
              )}
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Internal notes</CardTitle>
          <CardDescription>Internal comments are hidden from external users.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          <Textarea value={internalComment} onChange={(e) => setInternalComment(e.target.value)} rows={4} placeholder="Add internal comment..." />
          <Button onClick={() => addComment.mutate()} disabled={addComment.isPending || internalComment.trim().length === 0}>
            <Send className="h-4 w-4 mr-2" />
            {addComment.isPending ? 'Saving...' : 'Add comment'}
          </Button>
          {addComment.isError ? <div className="text-sm text-red-600">Failed to add comment.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Conversation</CardTitle>
          <CardDescription>Full thread (internal + external messages). External users only see external messages.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {messages.length ? (
            <div className="space-y-4">
              {messages.map((m) => (
                <div key={m.id} className={`rounded-md border p-4 ${m.isInternal ? 'bg-amber-50' : 'bg-white'}`}>
                  <div className="text-xs text-slate-500">
                    {m.isInternal ? 'Internal' : 'External'} • {m.authorName || 'User'} • {formatDateTime(m.createdAt)}
                  </div>
                  <div className="mt-2 text-sm whitespace-pre-wrap text-slate-900">{m.body}</div>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-sm text-slate-600">No messages yet.</div>
          )}

          <div className="rounded-md border bg-white p-4 space-y-2">
            <Label>Reply to requester (external)</Label>
            <Textarea value={externalReply} onChange={(e) => setExternalReply(e.target.value)} rows={4} placeholder="Type your response..." />
            <Button onClick={() => addAgentMessage.mutate()} disabled={addAgentMessage.isPending || externalReply.trim().length === 0}>
              <Send className="h-4 w-4 mr-2" />
              {addAgentMessage.isPending ? 'Sending...' : 'Send reply'}
            </Button>
            {addAgentMessage.isError ? <div className="text-sm text-red-600">Failed to send message.</div> : null}
          </div>

          <div className="rounded-md border bg-white p-4 space-y-2">
            <Label>Upload attachment</Label>
            <Input type="file" onChange={(e) => setAttachmentFile(e.target.files?.[0] || null)} />
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={attachmentInternalOnly} onChange={(e) => setAttachmentInternalOnly(e.target.checked)} />
              Internal only (hidden from requester)
            </label>
            <Button variant="outline" onClick={() => addAttachment.mutate()} disabled={addAttachment.isPending || !attachmentFile}>
              {addAttachment.isPending ? 'Uploading...' : 'Upload'}
            </Button>
            {addAttachment.isError ? <div className="text-sm text-red-600">Upload failed.</div> : null}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
