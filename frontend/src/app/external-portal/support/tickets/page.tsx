'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Plus, Ticket } from 'lucide-react';
import { useCallback, useMemo, useRef, useState } from 'react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useSignalR } from '@/hooks/useSignalR';
import { useToast } from '@/hooks/use-toast';
import { ehcTicketService } from '@/services/ehcTicketService';
import type { EhcMyTicketsFilters, EhcTicketCategoryTree, EhcTicketPriority, EhcTicketSource, EhcTicketStatus, EhcTicketType } from '@/services/ehcTicketService';

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

const statuses: Array<{ label: string; value: EhcTicketStatus | '' }> = [
  { label: 'All', value: '' },
  { label: 'New', value: 'New' },
  { label: 'Acknowledged', value: 'Acknowledged' },
  { label: 'In Progress', value: 'InProgress' },
  { label: 'Pending (User)', value: 'PendingUser' },
  { label: 'Pending (3rd Party)', value: 'PendingThirdParty' },
  { label: 'Resolved', value: 'Resolved' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Reopened', value: 'Reopened' },
];

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

export default function ExternalPortalSupportTicketsPage() {
  const router = useRouter();
  const qc = useQueryClient();
  const { toast } = useToast();
  const seenNotificationIdsRef = useRef<Set<string>>(new Set());

  const [q, setQ] = useState('');
  const [status, setStatus] = useState<EhcTicketStatus | ''>('');
  const [ticketType, setTicketType] = useState<EhcTicketType | ''>('');
  const [priority, setPriority] = useState<EhcTicketPriority | ''>('');
  const [source, setSource] = useState<EhcTicketSource | ''>('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [createdFrom, setCreatedFrom] = useState<string>('');
  const [createdTo, setCreatedTo] = useState<string>('');

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'categories', 'external'],
    queryFn: () => ehcTicketService.listCategories(),
  });

  const allCategoryOptions = useMemo(() => flattenCategoryTree(categories || []), [categories]);
  const visibleCategoryOptions = useMemo(() => {
    return allCategoryOptions
      .filter((o) => !ticketType || !o.appliesToType || o.appliesToType === ticketType)
      .map((o) => ({
        ...o,
        label: `${'— '.repeat(o.depth)}${o.label}`,
        value: o.subcategoryId || o.categoryId,
      }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [allCategoryOptions, ticketType]);

  const filters: EhcMyTicketsFilters = useMemo(() => {
    const term = q.trim();
    return {
      q: term ? term : null,
      status: status || null,
      ticketType: ticketType || null,
      priority: priority || null,
      source: source || null,
      categoryId: categoryId || null,
      createdFrom: createdFrom || null,
      createdTo: createdTo || null,
    };
  }, [q, status, ticketType, priority, source, categoryId, createdFrom, createdTo]);

  const { data, isLoading, error } = useQuery({
    queryKey: ['ehc', 'myTickets', filters],
    queryFn: () => ehcTicketService.listMyTickets(1, 25, filters),
  });

  const handleRealtimeNotification = useCallback(
    (n: any) => {
      const meta = n?.metadata ?? n?.Metadata ?? null;
      const entityType = meta?.EntityType ?? meta?.entityType ?? null;

      const actionUrl = (n?.actionUrl ?? n?.ActionUrl ?? '') as string;
      const looksLikeTicketUrl = typeof actionUrl === 'string' && actionUrl.toLowerCase().includes('/tickets/');

      const isTicket = typeof entityType === 'string' && entityType.toLowerCase() === 'ehcticket';
      if (!isTicket && !looksLikeTicketUrl) return;

      const id = (n?.id ?? n?.Id ?? null) as string | null;
      if (id) {
        if (seenNotificationIdsRef.current.has(id)) {
          qc.invalidateQueries({ queryKey: ['ehc', 'myTickets'] });
          return;
        }
        seenNotificationIdsRef.current.add(id);
        if (seenNotificationIdsRef.current.size > 200) {
          seenNotificationIdsRef.current.clear();
          seenNotificationIdsRef.current.add(id);
        }
      }

      toast({ title: 'Ticket update', description: n?.title || n?.Title || 'Your ticket was updated.', variant: 'success' });
      qc.invalidateQueries({ queryKey: ['ehc', 'myTickets'] });
    },
    [qc, toast]
  );

  useSignalR({
    autoConnect: true,
    onNotification: handleRealtimeNotification,
  });

  const formatCreatedAt = (iso: string) => {
    const dt = new Date(iso);
    if (Number.isNaN(dt.getTime())) return iso;
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(dt);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Ticket className="h-7 w-7" />
            My Tickets
          </h1>
          <p className="text-slate-600 mt-1">Track status updates and respond to agents.</p>
        </div>
        <Button onClick={() => router.push('/support/tickets/new')}>
          <Plus className="h-4 w-4 mr-2" />
          Create
        </Button>
      </div>

      <Card className="bg-white">
        <CardHeader className="p-4 pb-2 bg-gradient-to-r from-slate-50 to-white">
          <CardTitle>Filters</CardTitle>
          <CardDescription className="text-xs">Filter by status, type, priority, channel, category, and date.</CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-7 gap-3 p-4 pt-0">
          <div className="space-y-1 xl:col-span-2">
            <Label className="text-xs text-slate-600">Search</Label>
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Ticket # or subject..." />
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Status</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={status} onChange={(e) => setStatus(e.target.value as any)}>
              {statuses.map((s) => (
                <option key={s.label} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Type</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={ticketType} onChange={(e) => setTicketType(e.target.value as any)}>
              <option value="">All</option>
              <option value="Enquiry">Enquiry</option>
              <option value="Complaint">Complaint</option>
              <option value="Helpdesk">Helpdesk</option>
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Priority</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={priority} onChange={(e) => setPriority(e.target.value as any)}>
              <option value="">All</option>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
              <option value="Critical">Critical</option>
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Channel</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={source} onChange={(e) => setSource(e.target.value as any)}>
              <option value="">All</option>
              <option value="Web">Website</option>
              <option value="Mobile">Mobile App</option>
              <option value="Email">Email</option>
              <option value="PhoneCall">Phone Call</option>
              <option value="Sms">SMS</option>
              <option value="WhatsApp">WhatsApp</option>
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Category</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
              <option value="">All</option>
              {visibleCategoryOptions.map((c) => (
                <option key={`${c.categoryId}:${c.subcategoryId ?? ''}:${c.label}`} value={c.value}>
                  {c.label}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">From</Label>
            <input className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" type="date" value={createdFrom} onChange={(e) => setCreatedFrom(e.target.value)} />
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">To</Label>
            <input className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" type="date" value={createdTo} onChange={(e) => setCreatedTo(e.target.value)} />
          </div>

          <div className="flex items-end gap-2">
            <Button
              variant="outline"
              onClick={() => {
                setQ('');
                setStatus('');
                setTicketType('');
                setPriority('');
                setSource('');
                setCategoryId('');
                setCreatedFrom('');
                setCreatedTo('');
              }}
            >
              Clear
            </Button>
          </div>
        </CardContent>
      </Card>

      {isLoading ? (
        <Card>
          <CardContent className="p-6 text-slate-600">Loading...</CardContent>
        </Card>
      ) : error ? (
        <Card>
          <CardHeader>
            <CardTitle>Error</CardTitle>
            <CardDescription>Failed to load tickets. Please sign in again.</CardDescription>
          </CardHeader>
          <CardContent className="flex gap-2">
            <Button onClick={() => router.push('/login')}>Sign in</Button>
            <Button variant="outline" onClick={() => router.push('/support')}>
              Back
            </Button>
          </CardContent>
        </Card>
      ) : !data || data.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>No tickets yet</CardTitle>
            <CardDescription>Create your first enquiry/complaint/helpdesk request.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button onClick={() => router.push('/support/tickets/new')}>Create Ticket</Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4">
          {data.map((t) => (
            <Card key={t.id} className={`border-l-4 ${statusAccentClassName(t.status)} hover:shadow-sm transition-shadow bg-white`}>
              <CardContent className="p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <div className="font-semibold text-slate-900">{t.ticketNumber}</div>
                    <Badge className={statusBadgeClassName(t.status)}>{t.status}</Badge>
                    <Badge className={priorityBadgeClassName(t.priority)}>{t.priority}</Badge>
                  </div>
                  <div className="text-sm text-slate-700 mt-1 truncate">{t.subject || `${t.ticketType} • ${t.priority}`}</div>
                  <div className="text-xs text-slate-500 mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
                    <span>{formatCreatedAt(t.createdAt)}</span>
                    <span>•</span>
                    <span>{sourceLabel(t.source)}</span>
                    {t.categoryName ? (
                      <>
                        <span>•</span>
                        <span>{t.categoryName}</span>
                      </>
                    ) : null}
                  </div>
                </div>

                <Button variant="outline" size="icon" asChild className="shrink-0" aria-label="View ticket">
                  <Link href={`/support/tickets/${t.id}`}>
                    <Eye className="h-4 w-4" />
                    <span className="sr-only">View</span>
                  </Link>
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
