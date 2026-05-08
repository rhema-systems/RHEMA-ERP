'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  crmService,
  type CrmServiceDetailDto,
  type CrmServiceListItemDto,
  type CrmServiceSignalDto,
  type PagedResult,
} from '@/services/crmService';
import { AlertTriangle, ArrowRight, Clock3, MessageSquare, RefreshCw, Search, Star } from 'lucide-react';
import { toast } from 'sonner';

const SERVICE_CATEGORY_OPTIONS = ['Healthy', 'Monitor', 'Escalate', 'Critical'];
const PARTNER_TYPE_OPTIONS = ['Customer', 'Both', 'Vendor', 'Prospect'];

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const formatRating = (value?: number) => value ? `${value.toFixed(1)}/5` : 'No feedback';
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getSignalVariant = (signal: CrmServiceSignalDto): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (signal.scoreImpact <= -10 || signal.severity === 'critical') {
    return 'destructive';
  }

  if (signal.scoreImpact < 0) {
    return 'outline';
  }

  if (signal.scoreImpact > 0) {
    return 'secondary';
  }

  return 'default';
};

export default function CrmServicePage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const requestedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const initialServiceCategory = searchParams.get('serviceCategory') || 'all';
  const initialPartnerType = searchParams.get('partnerType') || 'all';
  const initialAttentionOnly = searchParams.get('attentionOnly') === 'true';
  const initialOverdueOnly = searchParams.get('overdueOnly') === 'true';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [serviceCategory, setServiceCategory] = useState(initialServiceCategory);
  const [partnerType, setPartnerType] = useState(initialPartnerType);
  const [attentionOnly, setAttentionOnly] = useState(initialAttentionOnly);
  const [overdueOnly, setOverdueOnly] = useState(initialOverdueOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmServiceListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedBusinessPartnerId, setSelectedBusinessPartnerId] = useState(requestedBusinessPartnerId);
  const [selectedDetail, setSelectedDetail] = useState<CrmServiceDetailDto | null>(null);

  const loadService = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getService({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        serviceCategory: serviceCategory === 'all' ? undefined : serviceCategory,
        attentionOnly,
        overdueOnly,
        partnerType: partnerType === 'all' ? undefined : partnerType,
      });

      setResult(data);

      if (requestedBusinessPartnerId && requestedPage === 1 && data.items.some((item) => item.businessPartnerId === requestedBusinessPartnerId)) {
        setSelectedBusinessPartnerId(requestedBusinessPartnerId);
        return;
      }

      if (selectedBusinessPartnerId && data.items.some((item) => item.businessPartnerId === selectedBusinessPartnerId)) {
        return;
      }

      setSelectedBusinessPartnerId(data.items[0]?.businessPartnerId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM service'));
    } finally {
      setLoading(false);
    }
  };

  const loadDetail = async (businessPartnerId: string) => {
    if (!businessPartnerId) {
      setSelectedDetail(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedDetail(await crmService.getServiceDetail(businessPartnerId, 8));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM service detail'));
      setSelectedDetail(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadService(page);
  }, [page, serviceCategory, partnerType, attentionOnly, overdueOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadService(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadDetail(selectedBusinessPartnerId);
  }, [selectedBusinessPartnerId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];
    const ratedItems = items.filter((item) => typeof item.averageFeedbackRating === 'number');
    const averageFeedback = ratedItems.length
      ? ratedItems.reduce((sum, item) => sum + (item.averageFeedbackRating || 0), 0) / ratedItems.length
      : 0;

    return [
      {
        label: 'Visible Accounts',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Accounts with service footprint in the current view',
        icon: MessageSquare,
      },
      {
        label: 'Needs Attention',
        value: items.filter((item) => item.requiresAttention).length.toLocaleString(),
        hint: 'Accounts with SLA, ticket, or problem pressure',
        icon: AlertTriangle,
      },
      {
        label: 'Open Tickets',
        value: items.reduce((sum, item) => sum + item.openTicketCount, 0).toLocaleString(),
        hint: `${items.reduce((sum, item) => sum + item.overdueTicketCount, 0)} overdue across this page`,
        icon: Clock3,
      },
      {
        label: 'Avg Feedback',
        value: ratedItems.length ? formatRating(averageFeedback) : 'No feedback',
        hint: `${items.reduce((sum, item) => sum + item.feedbackResponseCount, 0)} submitted ratings`,
        icon: Star,
      },
    ];
  }, [result]);

  const selectedSummary = result?.items.find((item) => item.businessPartnerId === selectedBusinessPartnerId);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Service</h1>
          <p className="text-muted-foreground">
            Customer service and support posture built on the existing EHC ticket and problem modules, mapped back to CRM accounts through portal users.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/accounts">Accounts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/collaboration">Collaboration</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/risk">Risk</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/helpdesk/tickets">Helpdesk</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadService(page)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {metrics.map((metric) => {
          const Icon = metric.icon;

          return (
            <Card key={metric.label}>
              <CardHeader className="pb-2">
                <CardDescription className="flex items-center gap-2">
                  <Icon className="h-4 w-4" />
                  {metric.label}
                </CardDescription>
                <CardTitle>{metric.value}</CardTitle>
              </CardHeader>
              <CardContent className="pt-0 text-xs text-muted-foreground">{metric.hint}</CardContent>
            </Card>
          );
        })}
      </div>

      {(serviceCategory !== 'all' || partnerType !== 'all' || attentionOnly || overdueOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This service workspace is focused on a narrower customer support watchlist.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {serviceCategory !== 'all' ? <Badge variant="secondary">Category: {serviceCategory}</Badge> : null}
            {partnerType !== 'all' ? <Badge variant="secondary">Partner Type: {partnerType}</Badge> : null}
            {attentionOnly ? <Badge variant="secondary">Attention only</Badge> : null}
            {overdueOnly ? <Badge variant="secondary">Overdue only</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/service">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search the account register by service band, support footprint, and SLA pressure.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-[1.4fr_0.9fr_0.9fr]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search account, territory, or service band"
              />
            </div>

            <Select value={serviceCategory} onValueChange={(value) => {
              setPage(1);
              setServiceCategory(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Service category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All service states</SelectItem>
                {SERVICE_CATEGORY_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={partnerType} onValueChange={(value) => {
              setPage(1);
              setPartnerType(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Partner type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All partner types</SelectItem>
                {PARTNER_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">Needs Attention</div>
                <div className="text-xs text-muted-foreground">Only show accounts with service or SLA pressure.</div>
              </div>
              <Switch checked={attentionOnly} onCheckedChange={(checked) => {
                setPage(1);
                setAttentionOnly(checked);
              }} />
            </div>

            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">Overdue / SLA Risk</div>
                <div className="text-xs text-muted-foreground">Focus on accounts with overdue tickets or breach risk.</div>
              </div>
              <Switch checked={overdueOnly} onCheckedChange={(checked) => {
                setPage(1);
                setOverdueOnly(checked);
              }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Service Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} accounts matched` : 'Loading CRM service footprint'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM service footprint...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM accounts matched the current service filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Account</TableHead>
                    <TableHead>Service</TableHead>
                    <TableHead>Tickets</TableHead>
                    <TableHead>Problems</TableHead>
                    <TableHead>Last Activity</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((item) => (
                    <TableRow
                      key={item.businessPartnerId}
                      className={`cursor-pointer ${selectedBusinessPartnerId === item.businessPartnerId ? 'bg-muted/40' : ''}`}
                      onClick={() => setSelectedBusinessPartnerId(item.businessPartnerId)}
                    >
                      <TableCell>
                        <div className="font-medium">{item.partnerName}</div>
                        <div className="text-xs text-muted-foreground">
                          {item.partnerCode} | {item.partnerType}
                          {item.salesTerritory ? ` | ${item.salesTerritory}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={item.requiresAttention ? 'destructive' : 'outline'}>{item.serviceCategory}</Badge>
                          <Badge variant="secondary">{formatScore(item.serviceScore)}</Badge>
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          {item.healthCategory} | {formatScore(item.healthScore)}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.openTicketCount} open | {item.ticketCount} total</div>
                        <div className="text-xs text-muted-foreground">
                          {item.overdueTicketCount} overdue | {formatRating(item.averageFeedbackRating)}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.openProblemCount} open | {item.linkedProblemCount} linked</div>
                        <div className="text-xs text-muted-foreground">
                          {item.complaintTicketCount} complaints | {item.resolvedTicketCount30Days} resolved / 30d
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{formatDate(item.lastTicketCreatedAt)}</div>
                        <div className="text-xs text-muted-foreground">Resolved {formatDate(item.lastResolvedAt)}</div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : null}

            {result?.totalCount && result.totalCount > result.pageSize ? (
              <div className="mt-4 flex items-center justify-between">
                <div className="text-sm text-muted-foreground">
                  Page {result.page} of {Math.max(1, Math.ceil(result.totalCount / result.pageSize))}
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    disabled={page <= 1 || loading}
                    onClick={() => setPage((current) => Math.max(1, current - 1))}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    disabled={!result || page >= Math.ceil(result.totalCount / result.pageSize) || loading}
                    onClick={() => setPage((current) => current + 1)}
                  >
                    Next
                  </Button>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Service Preview</CardTitle>
            <CardDescription>
              {selectedSummary ? `${selectedSummary.partnerName} | ${selectedSummary.partnerCode}` : 'Select an account from the register'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading service preview...</div> : null}
            {!detailLoading && !selectedDetail ? (
              <div className="py-16 text-center text-muted-foreground">Select an account to inspect its CRM service detail.</div>
            ) : null}

            {!detailLoading && selectedDetail ? (
              <>
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Service</div>
                      <div className="mt-2 text-3xl font-semibold">{formatScore(selectedDetail.serviceScore)}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {selectedDetail.serviceCategory} | {selectedDetail.openTicketCount} open tickets | {selectedDetail.openProblemCount} open problems
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={selectedDetail.requiresAttention ? 'destructive' : 'outline'}>
                        {selectedDetail.requiresAttention ? 'Attention Needed' : 'Stable'}
                      </Badge>
                      {selectedDetail.hasSlaBreachRisk ? <Badge variant="secondary">SLA Risk</Badge> : null}
                    </div>
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Primary Contact</div>
                    <div className="mt-2 font-medium">{selectedDetail.primaryContactName || 'No primary contact set'}</div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {selectedDetail.primaryEmail || selectedDetail.primaryPhone || 'No direct channel on file'}
                    </div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Customer Feedback</div>
                    <div className="mt-2 font-medium">{formatRating(selectedDetail.averageFeedbackRating)}</div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {selectedDetail.feedbackResponseCount} submitted ratings | Last resolved {formatDate(selectedDetail.lastResolvedAt)}
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 font-medium">
                    <MessageSquare className="h-4 w-4 text-muted-foreground" />
                    Service Footprint
                  </div>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-center justify-between gap-3">
                      <span>Portal users</span>
                      <span>{selectedDetail.activePortalUserCount} active | {selectedDetail.portalUserCount} total</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Ticket mix</span>
                      <span>{selectedDetail.helpdeskTicketCount} helpdesk | {selectedDetail.complaintTicketCount} complaints | {selectedDetail.enquiryTicketCount} enquiries</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Linked problems</span>
                      <span>{selectedDetail.openProblemCount} open | {selectedDetail.linkedProblemCount} total</span>
                    </div>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Top Signals</div>
                  {!selectedDetail.signals.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No service signals are available yet.</div>
                  ) : (
                    selectedDetail.signals.slice(0, 5).map((signal) => (
                      <div key={`${signal.label}-${signal.scoreImpact}`} className="flex items-center justify-between rounded-lg border p-3 text-sm">
                        <div className="font-medium">{signal.label}</div>
                        <Badge variant={getSignalVariant(signal)}>
                          {signal.scoreImpact > 0 ? `+${signal.scoreImpact}` : signal.scoreImpact}
                        </Badge>
                      </div>
                    ))
                  )}
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Recent Tickets</div>
                  {!selectedDetail.tickets.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No customer service tickets are linked to this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.tickets.map((ticket) => (
                        <div key={ticket.ticketId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">
                                <Link href={`/helpdesk/tickets/${ticket.ticketId}`} className="hover:underline">
                                  {ticket.ticketNumber}
                                </Link>
                              </div>
                              <div className="text-muted-foreground">
                                {ticket.subject || ticket.ticketType} | {ticket.priority} | {ticket.status}
                              </div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              {ticket.isComplaint ? <Badge variant="destructive">Complaint</Badge> : null}
                              {ticket.isOverdue ? <Badge variant="secondary">Overdue</Badge> : null}
                            </div>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            Created {formatDate(ticket.createdAt)} | Resolved {formatDate(ticket.resolvedAt)} | Feedback {ticket.feedbackRating ?? 'None'}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Linked Problems</div>
                  {!selectedDetail.problems.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No linked service problems are recorded for this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.problems.map((problem) => (
                        <div key={problem.problemId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">
                                <Link href={`/helpdesk/problems/${problem.problemId}`} className="hover:underline">
                                  {problem.problemNumber}
                                </Link>
                              </div>
                              <div className="text-muted-foreground">
                                {problem.title} | {problem.priority} | {problem.status}
                              </div>
                            </div>
                            <Badge variant={problem.status === 'Open' || problem.status === 'InProgress' ? 'destructive' : 'outline'}>
                              {problem.linkedTicketCount} linked tickets
                            </Badge>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            Opened {formatDate(problem.createdAt)} | Owner {problem.ownerName || 'Unassigned'}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button asChild>
                    <Link href={`/crm/accounts/${selectedDetail.businessPartnerId}`}>
                      Open Full Account
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/collaboration?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Collaboration
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/risk?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Risk
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href="/helpdesk/tickets">
                      Helpdesk Queue
                    </Link>
                  </Button>
                </div>
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
