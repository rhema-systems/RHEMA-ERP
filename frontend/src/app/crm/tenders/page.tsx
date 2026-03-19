'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  crmService,
  type CrmTenderDetailDto,
  type CrmTenderListItemDto,
  type PagedResult,
} from '@/services/crmService';
import { CalendarClock, Gavel, RefreshCw, Search, ShieldCheck, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';

const ENTITY_TYPE_OPTIONS = ['Invitation', 'Bid', 'Award'];
const STATUS_OPTIONS = ['Invited', 'Viewed', 'Submitted', 'Declined', 'Opened', 'UnderEvaluation', 'Accepted', 'Rejected', 'Awarded', 'ContractSigned', 'Cancelled'];

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getTenderSourceHref = (entityType: string, entityId: string, tenderId: string) => {
  switch (entityType) {
    case 'Bid':
      return `/procurement/bids/${entityId}`;
    case 'Award':
      return `/procurement/awards/${entityId}`;
    default:
      return `/procurement/tenders/${tenderId}`;
  }
};

export default function CrmTendersPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const scopedTenderId = searchParams.get('tenderId') || '';
  const requestedEntityType = searchParams.get('entityType') || '';
  const requestedEntityId = searchParams.get('entityId') || '';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [entityType, setEntityType] = useState(searchParams.get('filterEntityType') || 'all');
  const [status, setStatus] = useState(searchParams.get('status') || 'all');
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmTenderListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedEntityKey, setSelectedEntityKey] = useState(
    requestedEntityType && requestedEntityId ? `${requestedEntityType}:${requestedEntityId}` : '',
  );
  const [selectedTender, setSelectedTender] = useState<CrmTenderDetailDto | null>(null);

  const loadTenders = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getTenders({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        entityType: entityType === 'all' ? undefined : entityType,
        status: status === 'all' ? undefined : status,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        tenderId: scopedTenderId || undefined,
      });

      setResult(data);

      if (requestedEntityType && requestedEntityId && requestedPage === 1) {
        setSelectedEntityKey(`${requestedEntityType}:${requestedEntityId}`);
        return;
      }

      if (selectedEntityKey && data.items.some((item) => `${item.entityType}:${item.entityId}` === selectedEntityKey)) {
        return;
      }

      const first = data.items[0];
      setSelectedEntityKey(first ? `${first.entityType}:${first.entityId}` : '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM tenders'));
    } finally {
      setLoading(false);
    }
  };

  const loadTenderDetail = async (selection: string) => {
    if (!selection) {
      setSelectedTender(null);
      return;
    }

    const [selectedEntityTypeValue, selectedEntityId] = selection.split(':');
    if (!selectedEntityTypeValue || !selectedEntityId) {
      setSelectedTender(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedTender(await crmService.getTender(selectedEntityTypeValue, selectedEntityId));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM tender detail'));
      setSelectedTender(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadTenders(page);
  }, [page, entityType, status, scopedBusinessPartnerId, scopedTenderId]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadTenders(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadTenderDetail(selectedEntityKey);
  }, [selectedEntityKey]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Tender Records',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Invitation, bid, and award records in scope',
        icon: Gavel,
      },
      {
        label: 'Awards',
        value: items.filter((item) => item.entityType === 'Award').length.toLocaleString(),
        hint: 'Award records on this page',
        icon: ShieldCheck,
      },
      {
        label: 'Closing Soon',
        value: items.filter((item) => item.isClosingSoon).length.toLocaleString(),
        hint: 'Tenders near submission deadline',
        icon: CalendarClock,
      },
      {
        label: 'Page Value',
        value: formatMoney(items.reduce((sum, item) => sum + item.amount, 0), items[0]?.currency || 'USD'),
        hint: 'Estimated, bid, or awarded value',
        icon: TrendingUp,
      },
    ];
  }, [result]);

  const scopedAccountName = selectedTender?.businessPartnerId === scopedBusinessPartnerId
    ? selectedTender.businessPartnerName
    : result?.items.find((item) => item.businessPartnerId === scopedBusinessPartnerId)?.businessPartnerName;
  const scopedTenderName = selectedTender?.tenderId === scopedTenderId
    ? selectedTender.tenderNumber
    : result?.items.find((item) => item.tenderId === scopedTenderId)?.tenderNumber;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Tenders</h1>
          <p className="text-muted-foreground">
            Procurement invitations, bids, and awards surfaced inside CRM without duplicating tender ownership.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/contracts">Contracts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadTenders(page)}>
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

      {(scopedBusinessPartnerId || scopedTenderId) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This tender workspace was opened from another CRM drill-in and is filtered to that context.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account: {scopedAccountName || scopedBusinessPartnerId}</Badge> : null}
            {scopedTenderId ? <Badge variant="secondary">Tender: {scopedTenderName || scopedTenderId}</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/tenders">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search by tender number, title, account, record reference, or procurement status.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1.4fr_0.8fr_0.8fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search tender, bid number, account, or contract"
            />
          </div>

          <Select value={entityType} onValueChange={(value) => {
            setPage(1);
            setEntityType(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by entity" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All entity types</SelectItem>
              {ENTITY_TYPE_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={status} onValueChange={(value) => {
            setPage(1);
            setStatus(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {STATUS_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Tender Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} tender records matched` : 'Loading tender records'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM tenders...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM tender records matched the current filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tender</TableHead>
                    <TableHead>Entity</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((item) => {
                    const selectionKey = `${item.entityType}:${item.entityId}`;

                    return (
                      <TableRow
                        key={selectionKey}
                        className={selectedEntityKey === selectionKey ? 'bg-muted/40' : ''}
                        onClick={() => setSelectedEntityKey(selectionKey)}
                      >
                        <TableCell>
                          <div className="font-medium">{item.tenderTitle}</div>
                          <div className="text-xs text-muted-foreground">
                            {item.tenderNumber}
                            {item.businessPartnerName ? ` | ${item.businessPartnerName}` : ''}
                            {item.referenceNumber && item.referenceNumber !== item.tenderNumber ? ` | ${item.referenceNumber}` : ''}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap gap-2">
                            <Badge variant="outline">{item.entityType}</Badge>
                            {item.relatedContractNumber ? <Badge variant="secondary">{item.relatedContractNumber}</Badge> : null}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap gap-2">
                            <Badge variant={item.entityType === 'Award' ? 'default' : 'outline'}>{item.status}</Badge>
                            {item.isClosingSoon ? <Badge variant="secondary">Closing Soon</Badge> : null}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatMoney(item.amount, item.currency)}</div>
                          <div className="text-xs text-muted-foreground">{formatDate(item.createdAt)}</div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Tender Detail</CardTitle>
            <CardDescription>CRM context and procurement drill-down for the selected tender record.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading tender detail...</div> : null}
            {!detailLoading && !selectedTender ? (
              <div className="py-16 text-center text-muted-foreground">Select a tender record to inspect its CRM context.</div>
            ) : null}
            {!detailLoading && selectedTender ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">{selectedTender.tenderTitle}</div>
                    <div className="text-sm text-muted-foreground">
                      {selectedTender.tenderNumber}
                      {selectedTender.businessPartnerName ? ` | ${selectedTender.businessPartnerName}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={selectedTender.entityType === 'Award' ? 'default' : 'outline'}>{selectedTender.entityType}</Badge>
                    <Badge variant={selectedTender.isClosingSoon ? 'secondary' : 'outline'}>{selectedTender.status}</Badge>
                  </div>
                </div>

                <div className="grid gap-3 md:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Tender Context</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Tender type: {selectedTender.tenderType || 'Unknown'}</div>
                      <div>Tender status: {selectedTender.tenderStatus || 'Unknown'}</div>
                      <div>Publish date: {formatDate(selectedTender.publishDate)}</div>
                      <div>Submission deadline: {formatDate(selectedTender.submissionDeadline)}</div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Commercial Context</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Reference: {selectedTender.referenceNumber}</div>
                      <div>Amount: {formatMoney(selectedTender.amount, selectedTender.currency)}</div>
                      <div>Award date: {formatDate(selectedTender.awardDate)}</div>
                      <div>Related contract: {selectedTender.relatedContractNumber || 'None'}</div>
                    </div>
                  </div>
                </div>

                {selectedTender.entityType === 'Invitation' ? (
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Invitation Activity</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Invited: {formatDate(selectedTender.invitedDate)}</div>
                      <div>Viewed: {formatDate(selectedTender.viewedDate)}</div>
                      <div>Responded: {formatDate(selectedTender.responseDate)}</div>
                      <div>Decline reason: {selectedTender.declineReason || 'None'}</div>
                    </div>
                  </div>
                ) : null}

                {selectedTender.entityType === 'Bid' ? (
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Bid Detail</div>
                    <div className="mt-2 grid gap-2 text-sm sm:grid-cols-2">
                      <div>Submitted: {formatDate(selectedTender.submittedDate)}</div>
                      <div>Score: {selectedTender.totalScore?.toFixed(2) || 'None'}</div>
                      <div>Rank: {selectedTender.rank ?? 'None'}</div>
                      <div>Delivery days: {selectedTender.deliveryDays ?? 'None'}</div>
                      <div>Payment terms: {selectedTender.paymentTerms || 'None'}</div>
                      <div>Warranty terms: {selectedTender.warrantyTerms || 'None'}</div>
                      <div>Compliant: {selectedTender.isCompliant === undefined ? 'Unknown' : selectedTender.isCompliant ? 'Yes' : 'No'}</div>
                      <div>Non-compliance: {selectedTender.nonComplianceReasons || 'None'}</div>
                    </div>
                  </div>
                ) : null}

                {selectedTender.entityType === 'Award' ? (
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Award Detail</div>
                    <div className="mt-2 grid gap-2 text-sm sm:grid-cols-2">
                      <div>Original bid: {formatMoney(selectedTender.originalBidAmount || 0, selectedTender.currency)}</div>
                      <div>Negotiated: {selectedTender.isNegotiated ? 'Yes' : 'No'}</div>
                      <div>Purchase order: {selectedTender.purchaseOrderId || 'None'}</div>
                      <div>Award justification: {selectedTender.awardJustification || 'None'}</div>
                    </div>
                  </div>
                ) : null}

                {selectedTender.relationshipNote || selectedTender.notes ? (
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">CRM Notes</div>
                    <div className="mt-2 space-y-2 text-sm text-muted-foreground">
                      {selectedTender.relationshipNote ? <p>{selectedTender.relationshipNote}</p> : null}
                      {selectedTender.notes ? <p>{selectedTender.notes}</p> : null}
                    </div>
                  </div>
                ) : null}

                <div className="flex flex-wrap gap-2">
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/crm/accounts/${selectedTender.businessPartnerId}`}>Open Account</Link>
                  </Button>
                  {selectedTender.relatedContractId ? (
                    <Button asChild variant="outline" size="sm">
                      <Link href={`/crm/contracts?contractId=${selectedTender.relatedContractId}`}>Open Contract</Link>
                    </Button>
                  ) : null}
                  <Button asChild variant="outline" size="sm">
                    <Link href={getTenderSourceHref(selectedTender.entityType, selectedTender.entityId, selectedTender.tenderId)}>
                      Open Source Record
                    </Link>
                  </Button>
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/procurement/tenders/${selectedTender.tenderId}`}>Open Tender</Link>
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
