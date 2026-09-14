'use client';

import { PropertyEnquiryDetails } from '@/components/estate/PropertyEnquiryDetails';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Eye, ExternalLink, Keyboard, Link2, Paperclip, Plus, Send, Ticket, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';
import {
  buildScopedHelpdeskDetailPath,
  buildScopedHelpdeskQueuePath,
  buildScopedHelpdeskTicketsPath,
  getHelpdeskScopeConfig,
  inferHelpdeskScopeFromTicket,
} from '@/lib/helpdesk-scope';
import {
  ehcInternalTicketService,
  type EhcAllowedTicketTransition,
  type EhcProblemLookupItem,
  type EhcTicketLink,
  type EhcTicketLinkType,
  type EhcTicketLookupTicket,
} from '@/services/ehcInternalTicketService';
import { useSignalR } from '@/hooks/useSignalR';
import type { EhcTicketAttachment, EhcTicketStatus } from '@/services/ehcTicketService';
import { fileUploadService } from '@/services/fileUploadService';

const toDisplayUrl = (attachment: EhcTicketAttachment) => {
  const backendBaseUrl = (process.env.NEXT_PUBLIC_API_URL || '/api').replace('/api', '');
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
  const searchParams = useSearchParams();
  const params = useParams<{ id: string }>();
  const ticketId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
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
  const [rcaRootCauseId, setRcaRootCauseId] = useState<string>('');
  const [rcaRootCauseDetails, setRcaRootCauseDetails] = useState('');
  const [rcaResolutionSummary, setRcaResolutionSummary] = useState('');
  const [selectedCannedResponseId, setSelectedCannedResponseId] = useState<string>('');
  const [watchersOpen, setWatchersOpen] = useState(false);
  const [watcherSearch, setWatcherSearch] = useState('');
  const [linksOpen, setLinksOpen] = useState(false);
  const [linkSearch, setLinkSearch] = useState('');
  const [linkType, setLinkType] = useState<EhcTicketLinkType>('Related');
  const [linkReverseDirection, setLinkReverseDirection] = useState(false);
  const [linkNotes, setLinkNotes] = useState('');
  const [closeDuplicatesOpen, setCloseDuplicatesOpen] = useState(false);
  const [closeDuplicatesNotes, setCloseDuplicatesNotes] = useState('');
  const [previewOpen, setPreviewOpen] = useState(false);
  const [preview, setPreview] = useState<{ name: string; url: string; kind: 'image' | 'pdf' | 'other' } | null>(null);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);

  const goPrefixUntilRef = useRef<number>(0);

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

  const { data: isWatching } = useQuery({
    queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers', 'me'],
    queryFn: () => ehcInternalTicketService.getIsWatching(ticketId),
    enabled: Boolean(ticketId),
  });

  const scopeParam = searchParams?.get('scope');
  const effectiveScope = useMemo(
    () => scopeParam || (ticket ? inferHelpdeskScopeFromTicket(ticket.ticketType, ticket.source) : null),
    [scopeParam, ticket],
  );
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(effectiveScope), [effectiveScope]);

  const { data: watchers } = useQuery({
    queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers'],
    queryFn: () => ehcInternalTicketService.listWatchers(ticketId),
    enabled: Boolean(ticketId),
  });

  const { data: watcherSearchResults, isLoading: watcherSearchLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'users', watcherSearch],
    queryFn: () => ehcInternalTicketService.searchUsers(watcherSearch, 20),
    enabled: watcherSearch.trim().length >= 2,
  });

  const { data: links } = useQuery({
    queryKey: ['ehc', 'internal', 'ticket', ticketId, 'links'],
    queryFn: () => ehcInternalTicketService.listTicketLinks(ticketId),
    enabled: Boolean(ticketId),
  });

  const duplicateLinksToClose = useMemo(() => {
    const items = (links ?? []) as EhcTicketLink[];
    return items.filter((l) => l.linkType === 'DuplicateOf' && l.relationshipLabel === 'Duplicate ticket' && l.linkedStatus !== 'Closed');
  }, [links]);

  const { data: ticketProblems } = useQuery({
    queryKey: ['ehc', 'internal', 'ticket', ticketId, 'problems'],
    queryFn: () => ehcInternalTicketService.listTicketProblems(ticketId),
    enabled: Boolean(ticketId),
  });

  const { data: linkSearchResults, isLoading: linkSearchLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'tickets', linkSearch],
    queryFn: () => ehcInternalTicketService.searchTickets(linkSearch, 20),
    enabled: linkSearch.trim().length >= 2,
  });

  const isComplaint = ticket?.ticketType === 'Complaint';

  const { data: rootCauses } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'root-causes'],
    queryFn: () => ehcInternalTicketService.listRootCauses(),
    enabled: Boolean(isComplaint),
  });

  const { data: cannedResponses } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'canned-responses', ticket?.ticketType ?? null],
    queryFn: () => ehcInternalTicketService.listCannedResponses(ticket?.ticketType ?? null, null),
    enabled: Boolean(ticket?.id),
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

  useEffect(() => {
    if (!ticket || ticket.ticketType !== 'Complaint') return;
    setRcaRootCauseId(ticket.rootCauseId || '');
    setRcaRootCauseDetails(ticket.rootCauseDetails || '');
    setRcaResolutionSummary(ticket.resolutionSummary || '');
  }, [ticket?.id, ticket?.ticketType, ticket?.rootCauseId, ticket?.rootCauseDetails, ticket?.resolutionSummary]);

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

  useEffect(() => {
    const isTypingTarget = (t: EventTarget | null) => {
      const el = t as HTMLElement | null;
      if (!el) return false;
      if ((el as any).isContentEditable) return true;
      const tag = (el.tagName || '').toLowerCase();
      return tag === 'input' || tag === 'textarea' || tag === 'select';
    };

    const refresh = () => {
      if (!ticketId) return;
      void qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      void qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'allowed-transitions'] });
      void qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers'] });
      void qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'links'] });
      void qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'problems'] });
      toast({ title: 'Refreshed', description: 'Ticket reloaded.', variant: 'success' });
    };

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.defaultPrevented) return;
      if (isTypingTarget(e.target)) return;
      if (e.ctrlKey || e.metaKey || e.altKey) return;

      if (e.key === '?') {
        e.preventDefault();
        setShortcutsOpen(true);
        return;
      }

      if (e.key === 'r' || e.key === 'R') {
        e.preventDefault();
        refresh();
        return;
      }

      if (e.key === 'w' || e.key === 'W') {
        e.preventDefault();
        setWatchersOpen(true);
        return;
      }

      if (e.key === 'l' || e.key === 'L') {
        e.preventDefault();
        setLinksOpen(true);
        return;
      }

      if (e.key === 'i' || e.key === 'I') {
        e.preventDefault();
        const el = document.getElementById('ehc-internal-comment');
        (el as HTMLTextAreaElement | null)?.focus();
        return;
      }

      if (e.key === 'e' || e.key === 'E') {
        e.preventDefault();
        const el = document.getElementById('ehc-external-reply');
        (el as HTMLTextAreaElement | null)?.focus();
        return;
      }

      const now = Date.now();
      if (e.key === 'g' || e.key === 'G') {
        e.preventDefault();
        goPrefixUntilRef.current = now + 1000;
        return;
      }

      if (goPrefixUntilRef.current && now <= goPrefixUntilRef.current) {
        if (e.key === 'q' || e.key === 'Q') {
          e.preventDefault();
          goPrefixUntilRef.current = 0;
          router.push(buildScopedHelpdeskQueuePath(scopeConfig.scope));
          return;
        }
        if (e.key === 'p' || e.key === 'P') {
          e.preventDefault();
          goPrefixUntilRef.current = 0;
          router.push('/helpdesk/problems');
          return;
        }
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [qc, router, scopeConfig.scope, ticketId, toast]);

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

  const saveRca = useMutation({
    mutationFn: async () => {
      if (!ticketId) throw new Error('Ticket id is required');
      if (!isComplaint) throw new Error('RCA is only available for complaint tickets');

      return ehcInternalTicketService.updateRca(ticketId, {
        rootCauseId: rcaRootCauseId ? rcaRootCauseId : null,
        rootCauseDetails: (rcaRootCauseDetails || '').trim() || null,
        resolutionSummary: (rcaResolutionSummary || '').trim() || null,
      });
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      toast({ title: 'Saved', description: 'RCA updated.', variant: 'success' });
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
    if (name.match(/\\.(png|jpg|jpeg|gif|webp|bmp|svg)$/)) return 'image';
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

  const toggleWatch = useMutation({
    mutationFn: async () => {
      if (!ticketId) throw new Error('Ticket id is required');
      if (isWatching) return ehcInternalTicketService.unwatchMe(ticketId);
      return ehcInternalTicketService.watchMe(ticketId);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers', 'me'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers'] });
      toast({
        title: isWatching ? 'Unwatched' : 'Watching',
        description: isWatching ? 'You will no longer receive updates for this ticket.' : 'You will receive updates for this ticket.',
        variant: 'success',
      });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const addWatcher = useMutation({
    mutationFn: async (userId: string) => {
      if (!ticketId) throw new Error('Ticket id is required');
      await ehcInternalTicketService.addWatcher(ticketId, userId);
    },
    onSuccess: async () => {
      setWatcherSearch('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers'] });
      toast({ title: 'Saved', description: 'Watcher added.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const removeWatcher = useMutation({
    mutationFn: async (userId: string) => {
      if (!ticketId) throw new Error('Ticket id is required');
      await ehcInternalTicketService.removeWatcher(ticketId, userId);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'watchers'] });
      toast({ title: 'Saved', description: 'Watcher removed.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const createLink = useMutation({
    mutationFn: async (payload: { relatedTicketId: string; linkType: EhcTicketLinkType; notes?: string | null; reverseDirection?: boolean }) => {
      if (!ticketId) throw new Error('Ticket id is required');
      return ehcInternalTicketService.createTicketLink(ticketId, payload);
    },
    onSuccess: async () => {
      setLinksOpen(false);
      setLinkSearch('');
      setLinkNotes('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'links'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      toast({ title: 'Saved', description: 'Ticket link added.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const deleteLink = useMutation({
    mutationFn: async (linkId: string) => {
      if (!ticketId) throw new Error('Ticket id is required');
      await ehcInternalTicketService.deleteTicketLink(ticketId, linkId);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'links'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      toast({ title: 'Saved', description: 'Ticket link removed.', variant: 'success' });
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const closeDuplicates = useMutation({
    mutationFn: async () => {
      if (!ticketId) throw new Error('Ticket id is required');
      return ehcInternalTicketService.closeDuplicateTickets(ticketId, {
        targetStatus: 'Closed',
        notes: closeDuplicatesNotes.trim() || null,
      });
    },
    onSuccess: async (res) => {
      setCloseDuplicatesOpen(false);
      setCloseDuplicatesNotes('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'links'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'tickets'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'queue'] });

      const closed = res?.closedCount ?? 0;
      const failed = (res?.failed ?? []).length;
      if (failed) {
        toast({ title: 'Completed with errors', description: `Closed ${closed}. Failed ${failed}.`, variant: 'destructive' });
      } else {
        toast({ title: 'Done', description: `Closed ${closed} duplicate ticket(s).`, variant: 'success' });
      }
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
    },
  });

  const convertToProblem = useMutation({
    mutationFn: async () => {
      if (!ticketId) throw new Error('Ticket id is required');
      return ehcInternalTicketService.convertTicketToProblem(ticketId, {});
    },
    onSuccess: async (problem) => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'ticket', ticketId, 'problems'] });
      toast({ title: 'Converted', description: `Problem ${problem.problemNumber} created.`, variant: 'success' });
      router.push(`/helpdesk/problems/${problem.id}`);
    },
    onError: (err) => {
      toast({ title: 'Error', description: getErrorMessage(err), variant: 'destructive' });
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
          <CardDescription>Failed to load ticket.</CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline" onClick={() => router.push(buildScopedHelpdeskTicketsPath(scopeConfig.scope))}>
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
          <Button variant="outline" onClick={() => router.push(buildScopedHelpdeskTicketsPath(scopeConfig.scope))}>
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

        <div className="flex items-center gap-2 flex-wrap justify-end pt-1">
          <Button type="button" variant="outline" onClick={() => setShortcutsOpen(true)} title="Keyboard shortcuts">
            <Keyboard className="h-4 w-4 mr-2" />
            Shortcuts
          </Button>

          <Button
            type="button"
            variant={isWatching ? 'default' : 'outline'}
            onClick={() => toggleWatch.mutate()}
            disabled={toggleWatch.isPending}
            title={isWatching ? 'Unwatch ticket' : 'Watch ticket'}
          >
            <Eye className="h-4 w-4 mr-2" />
            {toggleWatch.isPending ? 'Saving…' : isWatching ? 'Watching' : 'Watch'}
          </Button>

          <Badge variant="outline" className="bg-white">
            Watchers: {(watchers ?? []).length}
          </Badge>

          <Button type="button" variant="outline" onClick={() => setWatchersOpen(true)}>
            Manage
          </Button>
        </div>
      </div>

      <Dialog open={watchersOpen} onOpenChange={setWatchersOpen}>
        <DialogContent className="sm:max-w-[720px]">
          <DialogHeader>
            <DialogTitle>Watchers</DialogTitle>
            <DialogDescription>Add colleagues to receive ticket update notifications without reassigning.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Add watcher</Label>
              <Input value={watcherSearch} onChange={(e) => setWatcherSearch(e.target.value)} placeholder="Search name or email (min 2 chars)..." />
              <div className="rounded-md border bg-white p-2 max-h-[280px] overflow-auto">
                {watcherSearch.trim().length < 2 ? (
                  <div className="text-sm text-slate-500 p-2">Type at least 2 characters to search.</div>
                ) : watcherSearchLoading ? (
                  <div className="text-sm text-slate-500 p-2">Searching…</div>
                ) : (watcherSearchResults ?? []).length ? (
                  <div className="space-y-1">
                    {(watcherSearchResults ?? []).map((u) => {
                      const already = (watchers ?? []).some((w) => String(w.id) === String(u.id));
                      return (
                        <div key={u.id} className="flex items-center justify-between gap-2 rounded-md border px-2 py-2">
                          <div className="min-w-0">
                            <div className="text-sm font-medium text-slate-900 truncate">{u.name}</div>
                            {u.email ? <div className="text-xs text-slate-500 truncate">{u.email}</div> : null}
                          </div>
                          <Button
                            size="sm"
                            variant={already ? 'secondary' : 'default'}
                            disabled={already || addWatcher.isPending}
                            onClick={() => addWatcher.mutate(u.id)}
                          >
                            {already ? 'Added' : addWatcher.isPending ? 'Adding…' : 'Add'}
                          </Button>
                        </div>
                      );
                    })}
                  </div>
                ) : (
                  <div className="text-sm text-slate-500 p-2">No matches.</div>
                )}
              </div>
              <div className="text-xs text-slate-500">Tip: use @email in notes/replies to mention users.</div>
            </div>

            <div className="space-y-2">
              <Label>Current watchers</Label>
              <div className="rounded-md border bg-white p-2 max-h-[360px] overflow-auto">
                {(watchers ?? []).length ? (
                  <div className="space-y-1">
                    {(watchers ?? []).map((u) => (
                      <div key={u.id} className="flex items-center justify-between gap-2 rounded-md border px-2 py-2">
                        <div className="min-w-0">
                          <div className="text-sm font-medium text-slate-900 truncate">{u.name || u.email || u.id}</div>
                          {u.email ? <div className="text-xs text-slate-500 truncate">{u.email}</div> : null}
                        </div>
                        <Button size="sm" variant="outline" disabled={removeWatcher.isPending} onClick={() => removeWatcher.mutate(u.id)}>
                          {removeWatcher.isPending ? 'Removing…' : 'Remove'}
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-sm text-slate-500 p-2">No watchers yet.</div>
                )}
              </div>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      <Dialog
        open={linksOpen}
        onOpenChange={(open) => {
          setLinksOpen(open);
          if (!open) {
            setLinkSearch('');
            setLinkNotes('');
            setLinkType('Related');
            setLinkReverseDirection(false);
          }
        }}
      >
        <DialogContent className="sm:max-w-[860px]">
          <DialogHeader>
            <DialogTitle>Linked tickets</DialogTitle>
            <DialogDescription>Create relationships (related, parent/child, duplicates) to keep cases connected.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <div className="space-y-3">
              <div className="space-y-2">
                <Label>Relationship</Label>
                <Select
                  value={
                    linkType === 'Related'
                      ? 'Related'
                      : linkType === 'ParentOf'
                        ? `ParentOf:${linkReverseDirection ? 'rev' : 'fwd'}`
                        : `DuplicateOf:${linkReverseDirection ? 'rev' : 'fwd'}`
                  }
                  onValueChange={(v) => {
                    if (v === 'Related') {
                      setLinkType('Related');
                      setLinkReverseDirection(false);
                      return;
                    }
                    const [t, dir] = String(v).split(':');
                    if (t === 'ParentOf' || t === 'DuplicateOf') {
                      setLinkType(t);
                      setLinkReverseDirection(dir === 'rev');
                    }
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select relationship..." />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Related">Related to</SelectItem>
                    <SelectItem value="ParentOf:fwd">This ticket is parent of…</SelectItem>
                    <SelectItem value="ParentOf:rev">This ticket is child of…</SelectItem>
                    <SelectItem value="DuplicateOf:fwd">This ticket is duplicate of…</SelectItem>
                    <SelectItem value="DuplicateOf:rev">This ticket has duplicate…</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Notes (optional)</Label>
                <Textarea value={linkNotes} onChange={(e) => setLinkNotes(e.target.value)} rows={3} placeholder="Why are these tickets linked?" />
              </div>

              <div className="space-y-2">
                <Label>Find ticket</Label>
                <Input
                  value={linkSearch}
                  onChange={(e) => setLinkSearch(e.target.value)}
                  placeholder="Search ticket number or subject (min 2 chars)..."
                />
                <div className="rounded-md border bg-white p-2 max-h-[320px] overflow-auto">
                  {linkSearch.trim().length < 2 ? (
                    <div className="text-sm text-slate-500 p-2">Type at least 2 characters to search.</div>
                  ) : linkSearchLoading ? (
                    <div className="text-sm text-slate-500 p-2">Searching…</div>
                  ) : (linkSearchResults ?? []).length ? (
                    <div className="space-y-1">
                      {(linkSearchResults ?? []).map((t: EhcTicketLookupTicket) => {
                        const already = (links ?? []).some((l: EhcTicketLink) => String(l.linkedTicketId) === String(t.id));
                        const isSelf = String(t.id) === String(ticketId);
                        const disabled = already || isSelf || createLink.isPending;
                        return (
                          <div key={t.id} className="flex items-center justify-between gap-2 rounded-md border px-2 py-2">
                            <div className="min-w-0">
                              <div className="text-sm font-medium text-slate-900 truncate">
                                {t.ticketNumber} {t.subject ? <span className="font-normal text-slate-600">• {t.subject}</span> : null}
                              </div>
                              <div className="text-xs text-slate-500 truncate">
                                {t.status} • {t.priority} • {formatDateTime(t.createdAt)}
                              </div>
                            </div>
                            <Button
                              size="sm"
                              variant={already ? 'secondary' : 'default'}
                              disabled={disabled}
                              onClick={() =>
                                createLink.mutate({
                                  relatedTicketId: t.id,
                                  linkType,
                                  reverseDirection: linkReverseDirection,
                                  notes: linkNotes || null,
                                })
                              }
                            >
                              {isSelf ? 'Current' : already ? 'Linked' : createLink.isPending ? 'Linking…' : 'Link'}
                            </Button>
                          </div>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="text-sm text-slate-500 p-2">No matches.</div>
                  )}
                </div>
              </div>
            </div>

            <div className="space-y-2">
              <Label>Existing links</Label>
              <div className="rounded-md border bg-white p-2 max-h-[560px] overflow-auto">
                {(links ?? []).length ? (
                  <div className="space-y-1">
                    {(links ?? []).map((l: EhcTicketLink) => (
                      <div key={l.id} className="flex items-center justify-between gap-2 rounded-md border px-2 py-2">
                        <div className="min-w-0">
                          <div className="text-sm font-medium text-slate-900 truncate">
                            {l.relationshipLabel} {l.linkedTicketNumber}
                          </div>
                          <div className="text-xs text-slate-500 truncate">
                            {l.linkedStatus} • {l.linkedPriority}
                            {l.linkedSubject ? <span> • {l.linkedSubject}</span> : null}
                          </div>
                        </div>
                        <div className="flex items-center gap-2">
                          <Button size="icon" variant="outline" title="Open" onClick={() => router.push(buildScopedHelpdeskDetailPath(scopeConfig.scope, l.linkedTicketId))}>
                            <ExternalLink className="h-4 w-4" />
                          </Button>
                          <Button
                            size="icon"
                            variant="outline"
                            title="Remove link"
                            disabled={deleteLink.isPending}
                            onClick={() => deleteLink.mutate(l.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-sm text-slate-500 p-2">No links yet.</div>
                )}
              </div>
            </div>
          </div>
        </DialogContent>
      </Dialog>

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

          <PropertyEnquiryDetails property={ticket.propertyListing} />
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
                  const kind = guessKind(a);
                  return (
                    <div key={a.id} className="text-sm flex items-center justify-between gap-3 rounded-md border bg-slate-50/60 px-3 py-2">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 min-w-0">
                          <span
                            className={`text-[11px] px-2 py-0.5 rounded-full border whitespace-nowrap ${
                              a.isInternal ? 'bg-amber-50 text-amber-800' : 'bg-blue-50 text-blue-700'
                            }`}
                          >
                            {a.isInternal ? 'Internal' : 'External'}
                          </span>
                          <div className="truncate">
                            {url ? (
                              <a className="text-blue-700 hover:underline" href={url} target="_blank" rel="noreferrer" title={a.fileName}>
                                {a.fileName}
                              </a>
                            ) : (
                              <span title={a.fileName}>{a.fileName}</span>
                            )}
                          </div>
                        </div>
                        <div className="text-xs text-slate-500 mt-0.5">
                          {formatBytes(a.fileSize)} • {formatDateTime(a.createdAt)}
                          {a.createdBy ? <span> • {a.createdBy}</span> : null}
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
                {isComplaint ? <TabsTrigger value="rca">RCA</TabsTrigger> : null}
                <TabsTrigger value="links">Linked tickets</TabsTrigger>
                <TabsTrigger value="problems">Problems</TabsTrigger>
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

            {isComplaint ? (
              <TabsContent value="rca" className="space-y-4">
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Root cause</Label>
                    <Select value={rcaRootCauseId || '__none'} onValueChange={(v) => setRcaRootCauseId(v === '__none' ? '' : v)}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select root cause..." />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="__none">None</SelectItem>
                        {(rootCauses ?? [])
                          .filter((x) => x?.isActive)
                          .sort((a, b) => String(a.code || '').localeCompare(String(b.code || '')))
                          .map((x) => (
                            <SelectItem key={x.id} value={x.id}>
                              {x.code} • {x.name}
                            </SelectItem>
                          ))}
                      </SelectContent>
                    </Select>
                    <div className="text-xs text-slate-500">Configure options in Admin → Helpdesk → Root Causes.</div>
                  </div>

                  <div className="space-y-2">
                    <Label>Resolution summary</Label>
                    <Textarea
                      value={rcaResolutionSummary}
                      onChange={(e) => setRcaResolutionSummary(e.target.value)}
                      placeholder="What was done to resolve the complaint?"
                      rows={4}
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Root cause details</Label>
                  <Textarea
                    value={rcaRootCauseDetails}
                    onChange={(e) => setRcaRootCauseDetails(e.target.value)}
                    placeholder="Additional analysis, contributing factors, corrective/preventive actions..."
                    rows={5}
                  />
                </div>

                <div className="flex justify-end">
                  <Button onClick={() => saveRca.mutate()} disabled={saveRca.isPending}>
                    {saveRca.isPending ? 'Saving…' : 'Save RCA'}
                  </Button>
                </div>
              </TabsContent>
            ) : null}

            <TabsContent value="links" className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <div className="text-sm text-slate-600">Link duplicates, parent/child tickets, or related cases.</div>
                <div className="flex items-center gap-2">
                  {duplicateLinksToClose.length ? (
                    <Button type="button" variant="outline" onClick={() => setCloseDuplicatesOpen(true)} disabled={closeDuplicates.isPending}>
                      Close duplicates ({duplicateLinksToClose.length})
                    </Button>
                  ) : null}
                  <Button type="button" onClick={() => setLinksOpen(true)}>
                    <Plus className="h-4 w-4 mr-2" />
                    Add link
                  </Button>
                </div>
              </div>

              <div className="rounded-lg border bg-white p-3">
                {(links ?? []).length ? (
                  <div className="space-y-2">
                    {(links ?? []).map((l) => {
                      const done = l.linkedStatus === 'Resolved' || l.linkedStatus === 'Closed';
                      const statusClass = done ? 'bg-green-600 text-white' : 'bg-blue-600 text-white';

                      const priorityClass =
                        l.linkedPriority === 'Critical'
                          ? 'bg-red-600 text-white'
                          : l.linkedPriority === 'High'
                            ? 'bg-orange-500 text-white'
                            : l.linkedPriority === 'Medium'
                              ? 'bg-yellow-500 text-slate-900'
                              : 'bg-slate-200 text-slate-900';

                      return (
                        <div key={l.id} className="rounded-md border bg-white p-3">
                          <div className="flex items-start justify-between gap-3">
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
                                  onClick={() => router.push(buildScopedHelpdeskDetailPath(scopeConfig.scope, l.linkedTicketId))}
                                  title="Open linked ticket"
                                >
                                  {l.linkedTicketNumber}
                                </button>
                                <Badge className={statusClass}>{l.linkedStatus}</Badge>
                                <Badge className={priorityClass}>{l.linkedPriority}</Badge>
                              </div>
                              {l.linkedSubject ? <div className="mt-1 text-sm text-slate-700 line-clamp-2">{l.linkedSubject}</div> : null}
                              <div className="mt-1 text-xs text-slate-500">
                                Created: {formatDateTime(l.linkedCreatedAt)} • Linked: {formatDateTime(l.createdAt)}
                                {l.createdBy ? <span> • By {l.createdBy}</span> : null}
                              </div>
                            </div>

                            <div className="flex items-center gap-2">
                              <Button
                                type="button"
                                size="icon"
                                variant="outline"
                                title="Open"
                                onClick={() => router.push(buildScopedHelpdeskDetailPath(scopeConfig.scope, l.linkedTicketId))}
                              >
                                <ExternalLink className="h-4 w-4" />
                              </Button>
                              <Button
                                type="button"
                                size="icon"
                                variant="outline"
                                title="Remove link"
                                disabled={deleteLink.isPending}
                                onClick={() => deleteLink.mutate(l.id)}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                ) : (
                  <div className="text-sm text-slate-600">No linked tickets yet.</div>
                )}
              </div>
            </TabsContent>

            <TabsContent value="problems" className="space-y-4">
              <div className="rounded-lg border bg-slate-50 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold text-slate-900">Problem management</div>
                    <div className="text-sm text-slate-700">Convert this ticket to a Problem record (RCA + CAPA).</div>
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => convertToProblem.mutate()}
                    disabled={convertToProblem.isPending || Boolean((ticketProblems ?? []).length)}
                    title={(ticketProblems ?? []).length ? 'This ticket is already linked to a problem.' : 'Convert ticket to problem'}
                  >
                    Convert to problem
                  </Button>
                </div>
                {(ticketProblems ?? []).length ? (
                  <div className="mt-2 text-xs text-slate-600">Linked problems: {(ticketProblems ?? []).length}</div>
                ) : (
                  <div className="mt-2 text-xs text-slate-600">No linked problems yet.</div>
                )}
              </div>

              {(ticketProblems ?? []).length ? (
                <div className="space-y-2">
                  {(ticketProblems ?? []).map((p: EhcProblemLookupItem) => {
                    const statusClass =
                      p.status === 'Resolved'
                        ? 'bg-green-600 text-white'
                        : p.status === 'Closed'
                          ? 'bg-slate-600 text-white'
                          : 'bg-blue-600 text-white';

                    const priorityClass =
                      p.priority === 'Critical'
                        ? 'bg-red-600 text-white'
                        : p.priority === 'High'
                          ? 'bg-orange-500 text-white'
                          : p.priority === 'Medium'
                            ? 'bg-yellow-500 text-slate-900'
                            : 'bg-slate-200 text-slate-900';

                    return (
                      <div key={p.id} className="rounded-md border bg-white p-3">
                        <div className="flex items-start justify-between gap-3">
                          <div className="min-w-0">
                            <div className="flex flex-wrap items-center gap-2">
                              <button
                                type="button"
                                className="text-sm font-semibold text-slate-900 hover:underline"
                                onClick={() => router.push(`/helpdesk/problems/${p.id}`)}
                                title="Open problem"
                              >
                                {p.problemNumber}
                              </button>
                              <Badge className={statusClass}>{p.status}</Badge>
                              <Badge className={priorityClass}>{p.priority}</Badge>
                            </div>
                            <div className="mt-1 text-sm text-slate-700 line-clamp-2">{p.title}</div>
                            <div className="mt-1 text-xs text-slate-500">Created: {formatDateTime(p.createdAt)}</div>
                          </div>

                          <Button
                            type="button"
                            size="icon"
                            variant="outline"
                            title="Open"
                            onClick={() => router.push(`/helpdesk/problems/${p.id}`)}
                          >
                            <ExternalLink className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : null}
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
          <Textarea id="ehc-internal-comment" value={internalComment} onChange={(e) => setInternalComment(e.target.value)} rows={4} placeholder="Add internal comment..." />
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
            {(cannedResponses ?? []).length ? (
              <div className="space-y-2">
                <Label className="text-xs text-slate-500">Canned responses</Label>
                <Select
                  value={selectedCannedResponseId || '__none'}
                  onValueChange={(v) => {
                    if (v === '__none') {
                      setSelectedCannedResponseId('');
                      return;
                    }

                    setSelectedCannedResponseId(v);
                    const selected = (cannedResponses ?? []).find((x) => x.id === v);
                    if (!selected) return;
                    setExternalReply((prev) => {
                      const body = (selected.body || '').trim();
                      if (!body) return prev;
                      return prev && prev.trim().length ? `${prev}\n\n${body}` : body;
                    });
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Insert canned response..." />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none">Select…</SelectItem>
                    {(cannedResponses ?? [])
                      .filter((x) => x?.isActive)
                      .sort((a, b) => String(a.code || '').localeCompare(String(b.code || '')))
                      .map((x) => (
                        <SelectItem key={x.id} value={x.id}>
                          {x.code} • {x.title}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}
            <Textarea id="ehc-external-reply" value={externalReply} onChange={(e) => setExternalReply(e.target.value)} rows={4} placeholder="Type your response..." />
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

      <Dialog open={closeDuplicatesOpen} onOpenChange={setCloseDuplicatesOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close duplicates</DialogTitle>
            <DialogDescription>
              Close linked duplicate tickets for <span className="font-medium">{ticket.ticketNumber}</span>.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="rounded-md border bg-slate-50 p-3 text-sm text-slate-700">
              This will close {duplicateLinksToClose.length.toLocaleString()} duplicate ticket(s) linked via “Duplicate ticket”.
            </div>

            {duplicateLinksToClose.length ? (
              <div className="max-h-40 overflow-auto rounded-md border bg-white p-2 text-sm">
                <div className="space-y-1">
                  {duplicateLinksToClose.map((l) => (
                    <div key={l.id} className="flex items-center justify-between gap-2">
                      <div className="min-w-0 truncate">
                        <span className="font-medium text-slate-900">{l.linkedTicketNumber}</span>
                        {l.linkedSubject ? <span className="text-slate-600"> • {l.linkedSubject}</span> : null}
                      </div>
                      <Badge variant="secondary">{l.linkedStatus}</Badge>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            <div className="space-y-2">
              <Label>Notes (optional)</Label>
              <Textarea value={closeDuplicatesNotes} onChange={(e) => setCloseDuplicatesNotes(e.target.value)} rows={3} placeholder="Extra notes to include in the close action..." />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setCloseDuplicatesOpen(false)} disabled={closeDuplicates.isPending}>
              Cancel
            </Button>
            <Button type="button" onClick={() => closeDuplicates.mutate()} disabled={closeDuplicates.isPending || duplicateLinksToClose.length === 0}>
              {closeDuplicates.isPending ? 'Closing…' : `Close ${duplicateLinksToClose.length} duplicate(s)`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={shortcutsOpen} onOpenChange={setShortcutsOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Keyboard shortcuts</DialogTitle>
            <DialogDescription>Shortcuts apply when you are not typing in a form field.</DialogDescription>
          </DialogHeader>

          <div className="space-y-2 text-sm">
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Refresh ticket</span>
              <Badge variant="secondary">R</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Manage watchers</span>
              <Badge variant="secondary">W</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Add link</span>
              <Badge variant="secondary">L</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Focus internal note</span>
              <Badge variant="secondary">I</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Focus external reply</span>
              <Badge variant="secondary">E</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Go to queue</span>
              <span className="flex items-center gap-2">
                <Badge variant="secondary">G</Badge>
                <Badge variant="secondary">Q</Badge>
              </span>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Go to problems</span>
              <span className="flex items-center gap-2">
                <Badge variant="secondary">G</Badge>
                <Badge variant="secondary">P</Badge>
              </span>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">This help</span>
              <Badge variant="secondary">?</Badge>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" onClick={() => setShortcutsOpen(false)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
