'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  crmService,
  type CrmQuoteDetailDto,
  type CrmQuoteListItemDto,
  type PagedResult,
} from '@/services/crmService';
import { salesOrderService } from '@/services/salesOrderService';
import { SalesHandoffActions } from '../components/SalesHandoffActions';
import { CalendarClock, FileText, Loader2, RefreshCw, Search, ShieldCheck, ShoppingCart, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';

const QUOTE_STATUS_OPTIONS = ['Draft', 'Sent', 'Accepted', 'Rejected', 'Expired'];

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const resolveChainHref = (entityType: string, entityId: string) => {
  switch (entityType) {
    case 'Lead':
      return `/crm/leads?leadId=${entityId}`;
    case 'Opportunity':
      return `/crm/opportunities?opportunityId=${entityId}`;
    case 'Quote':
      return `/crm/quotes?quoteId=${entityId}`;
    case 'SalesOrder':
      return `/sales/orders/${entityId}`;
    case 'Contract':
      return `/procurement/contracts/${entityId}`;
    case 'Project':
      return `/development/projects/${entityId}`;
    default:
      return null;
  }
};

export default function CrmQuotesPage() {
  const router = useRouter();
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const scopedOpportunityId = searchParams.get('opportunityId') || '';
  const scopedLeadId = searchParams.get('leadId') || '';
  const requestedQuoteId = searchParams.get('quoteId') || '';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [status, setStatus] = useState(searchParams.get('status') || 'all');
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmQuoteListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedQuoteId, setSelectedQuoteId] = useState(requestedQuoteId);
  const [selectedQuote, setSelectedQuote] = useState<CrmQuoteDetailDto | null>(null);
  const requestedIdRef = useRef(requestedQuoteId);
  requestedIdRef.current = requestedQuoteId;
  const detailRequest = useRef(0);

  useEffect(() => {
    if (requestedQuoteId) {
      setSelectedQuoteId(requestedQuoteId);
      setPage(1);
    }
  }, [requestedQuoteId]);

  const [convertingQuoteId, setConvertingQuoteId] = useState<string | null>(null);

  const loadQuotes = async (requestedPage: number = page) => {
    const requestedIdAtLoad = requestedIdRef.current;
    try {
      setLoading(true);
      const data = await crmService.getQuotes({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        opportunityId: scopedOpportunityId || undefined,
        leadId: scopedLeadId || undefined,
      });

      if (requestedIdAtLoad !== requestedIdRef.current) return;
      setResult(data);

      if (requestedQuoteId && requestedPage === 1) {
        setSelectedQuoteId(requestedQuoteId);
        return;
      }

      if (selectedQuoteId && data.items.some((item) => item.quoteId === selectedQuoteId)) {
        return;
      }

      setSelectedQuoteId(data.items[0]?.quoteId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM quotes'));
    } finally {
      setLoading(false);
    }
  };

  const loadQuoteDetail = async (quoteId: string) => {
    const request = ++detailRequest.current;
    setSelectedQuote(null);
    if (!quoteId) {
      setDetailLoading(false);
      return;
    }

    try {
      setDetailLoading(true);
      const detail = await crmService.getQuote(quoteId);
      if (request === detailRequest.current) setSelectedQuote(detail);
    } catch (error: unknown) {
      if (request !== detailRequest.current) return;
      toast.error(getMessage(error, 'Failed to load CRM quote detail'));
      setSelectedQuote(null);
    } finally {
      if (request === detailRequest.current) setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadQuotes(page);
  }, [page, status, scopedBusinessPartnerId, scopedOpportunityId, scopedLeadId]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadQuotes(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadQuoteDetail(selectedQuoteId);
    return () => { detailRequest.current++; };
  }, [selectedQuoteId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Quotes',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Filtered quote records',
        icon: FileText,
      },
      {
        label: 'Quoted Value',
        value: formatMoney(items.reduce((sum, item) => sum + item.value, 0)),
        hint: 'Current page only',
        icon: TrendingUp,
      },
      {
        label: 'Expiring Soon',
        value: items.filter((item) => item.isExpiringSoon).length.toLocaleString(),
        hint: 'Quotes nearing validity cut-off',
        icon: CalendarClock,
      },
      {
        label: 'Accepted',
        value: items.filter((item) => item.isAccepted).length.toLocaleString(),
        hint: 'Accepted quotes on this page',
        icon: ShieldCheck,
      },
    ];
  }, [result]);

  const scopedAccountName = selectedQuote?.businessPartnerId === scopedBusinessPartnerId
    ? selectedQuote.businessPartnerName
    : result?.items.find((item) => item.businessPartnerId === scopedBusinessPartnerId)?.businessPartnerName;
  const scopedOpportunityName = selectedQuote?.opportunityId === scopedOpportunityId
    ? selectedQuote.opportunityName
    : result?.items.find((item) => item.opportunityId === scopedOpportunityId)?.opportunityName;
  const scopedLeadName = selectedQuote?.leadId === scopedLeadId
    ? selectedQuote.leadName
    : result?.items.find((item) => item.leadId === scopedLeadId)?.leadName;
  const selectedQuoteSalesOrderNode = selectedQuote?.conversionChain.nodes.find((node) => node.entityType === 'SalesOrder');

  const handleConvertSelectedQuote = async () => {
    if (!selectedQuote) {
      return;
    }

    if (selectedQuoteSalesOrderNode) {
      router.push(`/sales/orders/${selectedQuoteSalesOrderNode.entityId}`);
      return;
    }

    if (!selectedQuote.isAccepted) {
      toast.error('Only accepted quotes can be converted to Sales Orders.');
      return;
    }

    if (!selectedQuote.businessPartnerId) {
      toast.error('Link this quote to a BusinessPartner account before converting it.');
      return;
    }

    try {
      setConvertingQuoteId(selectedQuote.quoteId);
      const order = await salesOrderService.convertQuoteToSalesOrder(selectedQuote.quoteId);
      toast.success(`Sales Order ${order.orderNumber || order.id} is ready`);
      router.push(`/sales/orders/${order.id}`);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to convert quote to Sales Order'));
    } finally {
      setConvertingQuoteId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Quotes</h1>
          <p className="text-muted-foreground">
            Drill into quote activity without duplicating the sales and commercial records already owned by the ERP.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadQuotes(page)}>
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

      {(scopedBusinessPartnerId || scopedOpportunityId || scopedLeadId) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>
              This quote workspace was opened from another CRM drill-in and is filtered to that context.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account: {scopedAccountName || scopedBusinessPartnerId}</Badge> : null}
            {scopedOpportunityId ? <Badge variant="secondary">Opportunity: {scopedOpportunityName || scopedOpportunityId}</Badge> : null}
            {scopedLeadId ? <Badge variant="secondary">Lead: {scopedLeadName || scopedLeadId}</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/quotes">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search quotes by customer context, opportunity context, or quote metadata.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1.6fr_0.8fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search quote, document, account, opportunity, or lead"
            />
          </div>

          <Select value={status} onValueChange={(value) => {
            setPage(1);
            setStatus(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {QUOTE_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Quote Queue</CardTitle>
            <CardDescription>{result ? `${result.totalCount} quotes matched` : 'Loading quote records'}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading quotes...</div> : null}

            {!loading && !(result?.items.length) ? (
              <div className="py-16 text-center text-muted-foreground">No CRM quotes matched the current filters.</div>
            ) : null}

            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Quote</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Valid Until</TableHead>
                    <TableHead className="text-right">Value</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((quote) => (
                    <TableRow
                      key={quote.quoteId}
                      className={selectedQuoteId === quote.quoteId ? 'bg-muted/40' : ''}
                      onClick={() => setSelectedQuoteId(quote.quoteId)}
                    >
                      <TableCell>
                        <div className="font-medium">{quote.quoteName}</div>
                        <div className="text-xs text-muted-foreground">
                          {quote.businessPartnerName || 'No account'}{quote.opportunityName ? ` | ${quote.opportunityName}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={quote.isAccepted ? 'default' : 'outline'}>{quote.quoteStatus}</Badge>
                          {quote.isExpiringSoon ? <Badge variant="secondary">Expiring Soon</Badge> : null}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(quote.validUntil)}</TableCell>
                      <TableCell className="text-right">
                        <div>{formatMoney(quote.value, quote.currency)}</div>
                        <div className="text-xs text-muted-foreground">{quote.documentNumber}</div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : null}

            <div className="flex items-center justify-between gap-3">
              <div className="text-sm text-muted-foreground">
                Page {result?.page || 1} of {Math.max(result?.totalPages || 1, 1)}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  disabled={!result || result.page <= 1 || loading}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  disabled={!result || result.page >= result.totalPages || loading}
                  onClick={() => setPage((current) => current + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Quote Detail</CardTitle>
            <CardDescription>Commercial drill-down, line items, and conversion chain for the selected quote.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading quote detail...</div> : null}

            {!detailLoading && !selectedQuote ? (
              <div className="py-16 text-center text-muted-foreground">Select a quote to inspect the proposal chain.</div>
            ) : null}

            {!detailLoading && selectedQuote ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">{selectedQuote.quoteName}</div>
                    <div className="text-sm text-muted-foreground">
                      {selectedQuote.businessPartnerName || 'No account'}{selectedQuote.opportunityName ? ` | ${selectedQuote.opportunityName}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={selectedQuote.isAccepted ? 'default' : 'outline'}>{selectedQuote.quoteStatus}</Badge>
                    {selectedQuote.isExpiringSoon ? <Badge variant="secondary">Expiring Soon</Badge> : null}
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  {selectedQuote.businessPartnerId ? (
                    <Button asChild variant="outline">
                      <Link href={`/crm/accounts/${selectedQuote.businessPartnerId}`}>Open Account</Link>
                    </Button>
                  ) : null}
                  <Button asChild variant="outline">
                    <Link href={`/crm/opportunities?opportunityId=${selectedQuote.opportunityId}`}>Open Opportunity</Link>
                  </Button>
                  <Button
                    onClick={handleConvertSelectedQuote}
                    disabled={convertingQuoteId === selectedQuote.quoteId || (!selectedQuote.isAccepted && !selectedQuoteSalesOrderNode)}
                  >
                    {convertingQuoteId === selectedQuote.quoteId ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <ShoppingCart className="mr-2 h-4 w-4" />
                    )}
                    {selectedQuoteSalesOrderNode ? 'Open Sales Order' : 'Convert to Sales Order'}
                  </Button>
                  <SalesHandoffActions
                    context={{
                      businessPartnerId: selectedQuote.businessPartnerId,
                      businessPartnerName: selectedQuote.businessPartnerName,
                      leadId: selectedQuote.leadId,
                      leadName: selectedQuote.leadName,
                      opportunityId: selectedQuote.opportunityId,
                      opportunityName: selectedQuote.opportunityName,
                      quoteId: selectedQuote.quoteId,
                      quoteName: selectedQuote.quoteName,
                      currency: selectedQuote.currency,
                      estimatedValue: selectedQuote.value,
                      contextLabel: 'Quote',
                    }}
                    showSalesOrder={false}
                  />
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Commercial</div>
                    <div className="mt-2 space-y-2">
                      <div>{formatMoney(selectedQuote.value, selectedQuote.currency)}</div>
                      <div>Valid Until: {formatDate(selectedQuote.validUntil)}</div>
                      <div>Lead: {selectedQuote.leadName || 'No linked lead'}</div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Document</div>
                    <div className="mt-2 space-y-2">
                      <div>{selectedQuote.documentNumber}</div>
                      <div>Document Date: {formatDate(selectedQuote.documentDate)}</div>
                      <div>Sent: {formatDate(selectedQuote.sentDate)} | Accepted: {formatDate(selectedQuote.acceptedDate)}</div>
                    </div>
                  </div>
                </div>

                {selectedQuote.proposal ? (
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Proposal Summary</div>
                    <div className="text-muted-foreground">{selectedQuote.proposal}</div>
                  </div>
                ) : null}

                <div className="space-y-3">
                  <div className="font-medium">Line Items</div>
                  {!selectedQuote.lineItems.length ? (
                    <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
                      No line items were captured for this quote.
                    </div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Description</TableHead>
                          <TableHead>Qty</TableHead>
                          <TableHead>Unit Price</TableHead>
                          <TableHead className="text-right">Line Total</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {selectedQuote.lineItems.map((line, index) => (
                          <TableRow key={`${line.description}-${index}`}>
                            <TableCell>
                              <div className="font-medium">{line.description}</div>
                              <div className="text-xs text-muted-foreground">
                                {[line.productCode, line.unit].filter(Boolean).join(' | ') || 'General item'}
                              </div>
                            </TableCell>
                            <TableCell>{line.quantity}</TableCell>
                            <TableCell>{formatMoney(line.unitPrice, selectedQuote.currency)}</TableCell>
                            <TableCell className="text-right">{formatMoney(line.lineTotal, selectedQuote.currency)}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </div>

                <div className="space-y-3">
                  <div className="font-medium">Conversion Chain</div>
                  {!selectedQuote.conversionChain.nodes.length ? (
                    <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
                      No conversion chain has been assembled yet.
                    </div>
                  ) : (
                    selectedQuote.conversionChain.nodes.map((node) => {
                      const href = resolveChainHref(node.entityType, node.entityId);

                      return (
                        <div key={`${node.entityType}-${node.entityId}`} className="rounded-lg border p-4">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                                {node.stage} | {node.relationshipType}
                              </div>
                              <div className="mt-1 font-medium">
                                {href ? <Link href={href} className="hover:underline">{node.title}</Link> : node.title}
                              </div>
                              <div className="text-sm text-muted-foreground">
                                {node.status}{node.referenceCode ? ` | ${node.referenceCode}` : ''}
                              </div>
                            </div>
                            {node.amount !== undefined ? (
                              <div className="text-right text-sm">
                                <div className="font-medium">{formatMoney(node.amount, node.currency || selectedQuote.currency)}</div>
                                <div className="text-muted-foreground">{formatDate(node.referenceDate)}</div>
                              </div>
                            ) : null}
                          </div>
                          {node.relationshipNote ? (
                            <div className="mt-2 text-sm text-muted-foreground">{node.relationshipNote}</div>
                          ) : null}
                        </div>
                      );
                    })
                  )}
                </div>
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
