'use client';

import { PropertyEnquiryDetails } from '@/components/estate/PropertyEnquiryDetails';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ExternalLink, Eye, Link2, Paperclip, Send, Ticket } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';
import { useSignalR } from '@/hooks/useSignalR';
import { ehcTicketService, type EhcExternalTicketLink, type EhcTicketAttachment, type EhcTicketPriority, type EhcTicketSource, type EhcTicketStatus } from '@/services/ehcTicketService';
import { fileUploadService } from '@/services/fileUploadService';

const statusBadgeClassName = (s: EhcTicketStatus) => {
  switch (s) {
    case 'Resolved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Closed':
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
    case 'Reopened':
      return 'bg-purple-600 text-white hover:bg-purple-600/90 dark:bg-purple-500 dark:hover:bg-purple-500/90';
    default:
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
  }
};

const statusAccentClassName = (s: EhcTicketStatus) => {
  switch (s) {
    case 'Resolved':
      return 'border-l-green-500';
    case 'Closed':
      return 'border-l-slate-500';
    case 'Reopened':
      return 'border-l-purple-500';
    default:
      return 'border-l-blue-500';
  }
};

const statusHeaderClassName = (s: EhcTicketStatus) => {
  switch (s) {
    case 'Resolved':
      return 'bg-gradient-to-r from-green-50 to-white';
    case 'Closed':
      return 'bg-gradient-to-r from-slate-50 to-white';
    case 'Reopened':
      return 'bg-gradient-to-r from-purple-50 to-white';
    default:
      return 'bg-gradient-to-r from-sky-50 to-white';
  }
};

const priorityBadgeClassName = (p: EhcTicketPriority) => {
  switch (p) {
    case 'Critical':
      return 'bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90';
    case 'High':
      return 'bg-orange-600 text-white hover:bg-orange-600/90 dark:bg-orange-500 dark:hover:bg-orange-500/90';
    case 'Medium':
      return 'bg-yellow-400 text-slate-900 hover:bg-yellow-400/90 dark:bg-yellow-500 dark:text-slate-900 dark:hover:bg-yellow-500/90';
    case 'Low':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    default:
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
  }
};

const sourceLabel = (s: EhcTicketSource) => {
  switch (s) {
    case 'Web':
      return 'Website';
    case 'Mobile':
      return 'Mobile App';
    case 'PhoneCall':
      return 'Phone Call';
    case 'Sms':
      return 'SMS';
    default:
      return s;
  }
};

const toDisplayUrl = (attachment: EhcTicketAttachment) => {
  const backendBaseUrl = (process.env.NEXT_PUBLIC_API_URL || '/api').replace('/api', '');
  const raw = attachment.publicUrl || attachment.filePath;
  if (!raw) return null;
  if (raw.startsWith('/uploads')) return `${backendBaseUrl}${raw}`;
  return raw;
};

export default function ExternalPortalSupportTicketDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const ticketId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const qc = useQueryClient();
  const { toast } = useToast();

  const [nowTs, setNowTs] = useState(() => Date.now());
  const [messageBody, setMessageBody] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [feedbackRating, setFeedbackRating] = useState<number>(0);
  const [feedbackComment, setFeedbackComment] = useState('');
  const [previewOpen, setPreviewOpen] = useState(false);
  const [preview, setPreview] = useState<{ name: string; url: string; kind: 'image' | 'pdf' | 'other' } | null>(null);

  const { data: ticket, isLoading, error } = useQuery({
    queryKey: ['ehc', 'ticket', ticketId],
    queryFn: () => ehcTicketService.getMyTicket(ticketId),
    enabled: Boolean(ticketId),
  });

  const { data: links } = useQuery({
    queryKey: ['ehc', 'ticket', ticketId, 'links'],
    queryFn: () => ehcTicketService.listMyTicketLinks(ticketId),
    enabled: Boolean(ticketId),
  });

  useEffect(() => {
    if (!ticket) return;
    if (ticket.feedbackRating) {
      setFeedbackRating(ticket.feedbackRating);
      setFeedbackComment(ticket.feedbackComment || '');
    }
  }, [ticket?.id, ticket?.feedbackRating, ticket?.feedbackComment]);

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
      qc.invalidateQueries({ queryKey: ['ehc', 'ticket', ticketId] });
      qc.invalidateQueries({ queryKey: ['ehc', 'ticket', ticketId, 'links'] });
    },
    [qc, ticketId, toast]
  );

  useSignalR({
    autoConnect: true,
    onNotification: handleRealtimeNotification,
  });

  const canSubmitFeedback = useMemo(() => {
    if (!ticket) return false;
    if (ticket.status !== 'Resolved' && ticket.status !== 'Closed') return false;
    return !ticket.feedbackRating;
  }, [ticket?.status, ticket?.feedbackRating]);

  const submitFeedback = useMutation({
    mutationFn: async () => {
      if (!ticketId) throw new Error('Ticket id is required');
      if (feedbackRating < 1 || feedbackRating > 5) throw new Error('Select a rating from 1 to 5');
      return ehcTicketService.submitFeedback(ticketId, {
        rating: feedbackRating,
        comment: (feedbackComment || '').trim() || null,
      });
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'ticket', ticketId] });
      toast({ title: 'Thank you', description: 'Your feedback has been submitted.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to submit feedback', variant: 'destructive' });
    },
  });

  const statusHistory = useMemo(() => ticket?.statusHistory ?? [], [ticket?.statusHistory]);
  const attachments = useMemo(() => ticket?.attachments ?? [], [ticket?.attachments]);
  const messages = useMemo(() => ticket?.messages ?? [], [ticket?.messages]);

  useEffect(() => {
    const id = setInterval(() => setNowTs(Date.now()), 30_000);
    return () => clearInterval(id);
  }, []);

  const formatDateTime = (iso: string) => {
    const dt = new Date(iso);
    if (Number.isNaN(dt.getTime())) return iso;
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(dt);
  };

  const formatBytes = (bytes: number) => {
    if (!bytes || bytes < 0) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round((bytes / 1024) * 10) / 10} KB`;
    return `${Math.round((bytes / 1024 / 1024) * 10) / 10} MB`;
  };

  const guessKind = (a: EhcTicketAttachment): 'image' | 'pdf' | 'other' => {
    const ct = (a.contentType || '').toLowerCase();
    const name = (a.fileName || '').toLowerCase();
    if (ct.startsWith('image/')) return 'image';
    if (ct === 'application/pdf') return 'pdf';
    if (name.endsWith('.pdf')) return 'pdf';
    if (name.match(/\.(png|jpg|jpeg|gif|webp|bmp|svg)$/)) return 'image';
    return 'other';
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
  }, [ticket?.firstResponseDueAt, ticket?.firstRespondedAt, nowTs]);

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
  }, [ticket?.resolutionDueAt, ticket?.resolvedAt, nowTs]);

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
          const bits = [e.actorName ? `By ${e.actorName}` : null, e.body ? e.body : null].filter(Boolean) as string[];
          return {
            id: `audit:${e.id}`,
            at: e.createdAt,
            title,
            detail: bits.join(' • ') || undefined,
          };
        });
    }

    const ev: Array<{ id: string; at: string; title: string; detail?: string }> = [];

    ev.push({
      id: `created:${ticket.id}`,
      at: ticket.createdAt,
      title: 'Ticket created',
    });

    for (const h of statusHistory) {
      ev.push({
        id: `status:${h.id}`,
        at: h.createdAt,
        title: h.fromStatus ? `Status: ${h.fromStatus} → ${h.toStatus}` : `Status: ${h.toStatus}`,
        detail: h.notes || undefined,
      });
    }

    for (const m of messages) {
      ev.push({
        id: `msg:${m.id}`,
        at: m.createdAt,
        title: 'Message sent',
        detail: m.body ? `"${m.body.slice(0, 80)}${m.body.length > 80 ? '…' : ''}"` : undefined,
      });
    }

    for (const a of attachments) {
      ev.push({
        id: `att:${a.id}`,
        at: a.createdAt,
        title: 'Attachment uploaded',
        detail: a.fileName,
      });
    }

    return ev
      .filter((x) => !!x.at)
      .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime());
  }, [ticket, statusHistory, attachments, messages]);

  const addMessage = useMutation({
    mutationFn: async () => {
      const body = messageBody.trim();
      if (!body) throw new Error('Message is required');
      return ehcTicketService.addMessage(ticketId, body);
    },
    onSuccess: async () => {
      setMessageBody('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'ticket', ticketId] });
      toast({ title: 'Sent', description: 'Message sent.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed to send message', variant: 'destructive' });
    },
  });

  const addAttachment = useMutation({
    mutationFn: async () => {
      if (!file) throw new Error('File is required');
      const uploaded = await fileUploadService.uploadFile(file, 'ehc-ticket', ticketId);
      if (!uploaded.filePath) throw new Error('Upload did not return filePath');

      return ehcTicketService.addAttachment(ticketId, {
        filePath: uploaded.filePath,
        fileName: uploaded.originalName || uploaded.filename,
        contentType: uploaded.mimeType,
        fileSize: uploaded.size,
      });
    },
    onSuccess: async () => {
      setFile(null);
      await qc.invalidateQueries({ queryKey: ['ehc', 'ticket', ticketId] });
      toast({ title: 'Uploaded', description: 'Attachment uploaded.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: err instanceof Error ? err.message : 'Upload failed', variant: 'destructive' });
    },
  });

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
          <CardDescription>Failed to load ticket. Please sign in again.</CardDescription>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button onClick={() => router.push('/support/tickets')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <Button variant="outline" onClick={() => router.push('/login')}>
            Sign in
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <Button variant="outline" onClick={() => router.push('/support/tickets')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <h1 className="text-3xl font-bold flex items-center gap-2 mt-4">
            <Ticket className="h-7 w-7" />
            {ticket.ticketNumber}
          </h1>
          <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-slate-700">
            <Badge className={statusBadgeClassName(ticket.status)}>{ticket.status}</Badge>
            <Badge variant="outline">{ticket.ticketType}</Badge>
            <Badge className={priorityBadgeClassName(ticket.priority)}>{ticket.priority}</Badge>
            <Badge variant="outline">{sourceLabel(ticket.source)}</Badge>
          </div>
        </div>
        <Button onClick={() => router.push('/support/tickets/new')}>Create New</Button>
      </div>

      <Card className={`border-l-4 ${statusAccentClassName(ticket.status)} bg-white`}>
        <CardHeader className={statusHeaderClassName(ticket.status)}>
          <CardTitle>{ticket.subject || 'Details'}</CardTitle>
          <CardDescription>Created {formatDateTime(ticket.createdAt)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
            <Tabs defaultValue="details">
              <TabsList>
                <TabsTrigger value="details">Details</TabsTrigger>
                <TabsTrigger value="sla">SLA</TabsTrigger>
                <TabsTrigger value="links">Linked tickets</TabsTrigger>
                <TabsTrigger value="timeline">Timeline</TabsTrigger>
                <TabsTrigger value="audit">Audit</TabsTrigger>
                {ticket.status === 'Resolved' || ticket.status === 'Closed' ? <TabsTrigger value="feedback">Feedback</TabsTrigger> : null}
              </TabsList>

            <TabsContent value="details" className="space-y-4">
              <div className="rounded-lg border bg-slate-50/80 p-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
                  <div>
                    <div className="text-slate-500">Type</div>
                    <div className="font-medium text-slate-900">{ticket.ticketType}</div>
                  </div>
                  <div>
                    <div className="text-slate-500">Priority</div>
                    <div className="font-medium text-slate-900">{ticket.priority}</div>
                  </div>
                  <div>
                    <div className="text-slate-500">Channel</div>
                    <div className="font-medium text-slate-900">{sourceLabel(ticket.source)}</div>
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
                    <div className="text-slate-500">Related Entity</div>
                    <div className="font-medium text-slate-900">{ticket.relatedEntityType || '—'}</div>
                  </div>
                  <div>
                    <div className="text-slate-500">Reference</div>
                    <div className="font-medium text-slate-900">{ticket.relatedEntityReference || '—'}</div>
                  </div>
                </div>
              </div>

              <PropertyEnquiryDetails property={ticket.propertyListing} />
              <div className="rounded-lg border bg-white p-4 text-sm text-slate-900 whitespace-pre-wrap">{ticket.description}</div>

              {attachments.length ? (
                <div className="space-y-2 rounded-lg border bg-white p-4">
                  <div className="font-medium text-slate-900 flex items-center gap-2">
                    <Paperclip className="h-4 w-4" />
                    Attachments
                  </div>
                  <div className="space-y-1">
                    {attachments.map((a) => {
                      const url = toDisplayUrl(a);
                      const kind = guessKind(a);
                      return (
                        <div key={a.id} className="text-sm flex items-center justify-between gap-3 rounded-md border bg-slate-50/60 px-3 py-2">
                          <div className="min-w-0">
                            <div className="truncate">
                              {url ? (
                                <a className="text-blue-700 hover:underline" href={url} target="_blank" rel="noreferrer" title={a.fileName}>
                                  {a.fileName}
                                </a>
                              ) : (
                                <span title={a.fileName}>{a.fileName}</span>
                              )}
                            </div>
                            <div className="text-xs text-slate-500">
                              {formatBytes(a.fileSize)} • {formatDateTime(a.createdAt)}
                            </div>
                          </div>

                          <div className="flex items-center gap-2">
                            {url ? (
                              <Button
                                type="button"
                                size="icon"
                                variant="outline"
                                title="Preview"
                                onClick={() => {
                                  setPreview({ name: a.fileName, url, kind });
                                  setPreviewOpen(true);
                                }}
                              >
                                <Eye className="h-4 w-4" />
                              </Button>
                            ) : null}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ) : null}

              <div className="space-y-2 rounded-lg border bg-slate-50/80 p-4">
                <Label>Add attachment</Label>
                <Input type="file" onChange={(e) => setFile(e.target.files?.[0] || null)} />
                <Button variant="outline" onClick={() => addAttachment.mutate()} disabled={addAttachment.isPending || !file}>
                  {addAttachment.isPending ? 'Uploading...' : 'Upload'}
                </Button>
                {addAttachment.isError ? <div className="text-sm text-red-600">Upload failed.</div> : null}
              </div>
            </TabsContent>

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
                  <div className="font-medium text-slate-900">{ticket.firstRespondedAt ? formatDateTime(ticket.firstRespondedAt) : '—'}</div>
                </div>
                <div>
                  <div className="text-slate-500">Resolved at</div>
                  <div className="font-medium text-slate-900">{ticket.resolvedAt ? formatDateTime(ticket.resolvedAt) : '—'}</div>
                </div>
              </div>
            </TabsContent>

            <TabsContent value="links" className="space-y-4">
              <div className="rounded-lg border bg-slate-50/80 p-4">
                <div className="text-sm text-slate-700">Linked tickets help you track related or duplicate cases.</div>
              </div>

              <div className="rounded-lg border bg-white p-4">
                {(links ?? []).length ? (
                  <div className="space-y-2">
                    {(links ?? []).map((l: EhcExternalTicketLink) => (
                      <div key={l.id} className="rounded-md border bg-slate-50/60 px-3 py-2 flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge variant="outline" className="bg-white">
                              <span className="inline-flex items-center gap-1.5">
                                <Link2 className="h-3.5 w-3.5" />
                                {l.relationshipLabel}
                              </span>
                            </Badge>
                            <button
                              type="button"
                              className="text-sm font-semibold text-slate-900 hover:underline"
                              onClick={() => router.push(`/support/tickets/${l.linkedTicketId}`)}
                              title="Open linked ticket"
                            >
                              {l.linkedTicketNumber}
                            </button>
                            <Badge className={statusBadgeClassName(l.linkedStatus)}>{l.linkedStatus}</Badge>
                            <Badge className={priorityBadgeClassName(l.linkedPriority)}>{l.linkedPriority}</Badge>
                          </div>
                          {l.linkedSubject ? <div className="mt-1 text-sm text-slate-700 line-clamp-2">{l.linkedSubject}</div> : null}
                          <div className="mt-1 text-xs text-slate-500">
                            Created: {formatDateTime(l.linkedCreatedAt)} • Linked: {formatDateTime(l.createdAt)}
                          </div>
                        </div>

                        <Button
                          type="button"
                          size="icon"
                          variant="outline"
                          title="Open"
                          onClick={() => router.push(`/support/tickets/${l.linkedTicketId}`)}
                        >
                          <ExternalLink className="h-4 w-4" />
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-sm text-slate-600">No linked tickets.</div>
                )}
              </div>
            </TabsContent>

            <TabsContent value="timeline" className="space-y-2">
              {statusHistory.length ? (
                <div className="space-y-2">
                  {statusHistory.map((h) => (
                    <div key={h.id} className={`rounded-md border bg-white p-3 border-l-4 ${statusAccentClassName(h.toStatus)}`}>
                      <div className="flex items-center gap-2 flex-wrap">
                        <Badge className={statusBadgeClassName(h.toStatus)}>{h.toStatus}</Badge>
                        <div className="text-xs text-slate-500">{formatDateTime(h.createdAt)}</div>
                      </div>
                      {h.notes ? <div className="mt-2 text-sm text-slate-700 whitespace-pre-wrap">{h.notes}</div> : null}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-sm text-slate-600">No status history yet.</div>
              )}
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

            <TabsContent value="feedback" className="space-y-4">
              <div className="rounded-lg border bg-white p-4 space-y-3">
                <div className="font-medium text-slate-900">How was your experience?</div>
                <div className="text-sm text-slate-600">Rate our support for this ticket.</div>

                <div className="flex flex-wrap gap-2">
                  {[1, 2, 3, 4, 5].map((n) => (
                    <button
                      key={n}
                      type="button"
                      className={`h-9 w-9 rounded-md border text-sm font-medium ${
                        (ticket.feedbackRating || feedbackRating) >= n ? 'bg-yellow-400 text-slate-900 border-yellow-400' : 'bg-background hover:bg-muted'
                      } ${ticket.feedbackRating ? 'opacity-80 cursor-not-allowed' : ''}`}
                      onClick={() => (!ticket.feedbackRating ? setFeedbackRating(n) : null)}
                      disabled={Boolean(ticket.feedbackRating)}
                      aria-label={`Rate ${n}`}
                      title={`${n}/5`}
                    >
                      {n}
                    </button>
                  ))}
                </div>

                <div className="space-y-2">
                  <Label>Comment (optional)</Label>
                  <Textarea
                    value={ticket.feedbackRating ? ticket.feedbackComment || '' : feedbackComment}
                    onChange={(e) => (ticket.feedbackRating ? null : setFeedbackComment(e.target.value))}
                    placeholder="Tell us what went well or what we can improve..."
                    disabled={Boolean(ticket.feedbackRating)}
                    rows={4}
                  />
                </div>

                <div className="flex items-center justify-between gap-3">
                  <div className="text-xs text-slate-500">
                    {ticket.feedbackRating ? `Submitted ${ticket.feedbackSubmittedAt ? formatDateTime(ticket.feedbackSubmittedAt) : ''}` : 'You can submit feedback once.'}
                  </div>
                  <Button onClick={() => submitFeedback.mutate()} disabled={!canSubmitFeedback || submitFeedback.isPending}>
                    {submitFeedback.isPending ? 'Submitting…' : ticket.feedbackRating ? 'Submitted' : 'Submit feedback'}
                  </Button>
                </div>
              </div>
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      <Card className="bg-white">
        <CardHeader className="bg-gradient-to-r from-slate-50 to-white">
          <CardTitle>Messages</CardTitle>
          <CardDescription>Reply to the assigned agent. Internal notes are not visible here.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {messages.length ? (
            <div className="space-y-4">
              {messages.map((m) => (
                <div key={m.id} className="rounded-md border bg-white p-4 shadow-sm">
                  <div className="text-xs text-slate-500">
                    {m.authorName || 'User'} • {formatDateTime(m.createdAt)}
                  </div>
                  <div className="mt-2 text-sm whitespace-pre-wrap text-slate-900">{m.body}</div>
                  {m.attachments?.length ? (
                    <div className="mt-2 space-y-1">
                      {m.attachments.map((a) => {
                        const url = toDisplayUrl(a);
                        return (
                          <div key={a.id} className="text-sm">
                            {url ? (
                              <a className="text-blue-700 hover:underline" href={url} target="_blank" rel="noreferrer">
                                {a.fileName}
                              </a>
                            ) : (
                              <span>{a.fileName}</span>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  ) : null}
                </div>
              ))}
            </div>
          ) : (
            <div className="text-sm text-slate-600">No messages yet.</div>
          )}

          <div className="space-y-2">
            <Label>New message</Label>
            <Textarea value={messageBody} onChange={(e) => setMessageBody(e.target.value)} rows={4} placeholder="Type your response..." />
            <Button onClick={() => addMessage.mutate()} disabled={addMessage.isPending || messageBody.trim().length === 0}>
              <Send className="h-4 w-4 mr-2" />
              {addMessage.isPending ? 'Sending...' : 'Send'}
            </Button>
            {addMessage.isError ? <div className="text-sm text-red-600">Failed to send message.</div> : null}
          </div>
        </CardContent>
      </Card>

      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle className="truncate">{preview?.name || 'Attachment'}</DialogTitle>
            <DialogDescription>
              {preview?.url ? (
                <a className="text-blue-700 hover:underline" href={preview.url} target="_blank" rel="noreferrer">
                  Open in new tab
                </a>
              ) : null}
            </DialogDescription>
          </DialogHeader>

          {preview?.url ? (
            preview.kind === 'image' ? (
              <div className="rounded-md border bg-white p-2">
                <img src={preview.url} alt={preview.name} className="max-h-[70vh] w-full object-contain" />
              </div>
            ) : preview.kind === 'pdf' ? (
              <div className="rounded-md border bg-white overflow-hidden">
                <iframe title={preview.name} src={preview.url} className="w-full h-[70vh]" />
              </div>
            ) : (
              <div className="text-sm text-slate-600">
                Preview not available for this file type. Use “Open in new tab” to download/view.
              </div>
            )
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}
