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
  type CrmContractDetailDto,
  type CrmContractListItemDto,
  type PagedResult,
} from '@/services/crmService';
import { Clock3, FileCheck, RefreshCw, Search, ShieldCheck, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';

const CONTRACT_STATUS_OPTIONS = ['Draft', 'PendingSignature', 'Active', 'Completed', 'Suspended', 'Terminated'];

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

export default function CrmContractsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const requestedContractId = searchParams.get('contractId') || '';
  const initialExpiringOnly = searchParams.get('expiringOnly') === 'true';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [status, setStatus] = useState(searchParams.get('status') || 'all');
  const [expiringOnly, setExpiringOnly] = useState(initialExpiringOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmContractListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedContractId, setSelectedContractId] = useState(requestedContractId);
  const [selectedContract, setSelectedContract] = useState<CrmContractDetailDto | null>(null);

  const loadContracts = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getContracts({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        expiringOnly,
      });

      setResult(data);

      if (requestedContractId && requestedPage === 1) {
        setSelectedContractId(requestedContractId);
        return;
      }

      if (selectedContractId && data.items.some((item) => item.contractId === selectedContractId)) {
        return;
      }

      setSelectedContractId(data.items[0]?.contractId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM contracts'));
    } finally {
      setLoading(false);
    }
  };

  const loadContractDetail = async (contractId: string) => {
    if (!contractId) {
      setSelectedContract(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedContract(await crmService.getContract(contractId));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM contract'));
      setSelectedContract(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadContracts(page);
  }, [page, status, scopedBusinessPartnerId, expiringOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadContracts(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadContractDetail(selectedContractId);
  }, [selectedContractId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Contracts',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Filtered commercial commitments',
        icon: FileCheck,
      },
      {
        label: 'Active',
        value: items.filter((item) => item.isActive).length.toLocaleString(),
        hint: 'Currently live or signing',
        icon: ShieldCheck,
      },
      {
        label: 'Expiring Soon',
        value: items.filter((item) => item.isExpiringSoon).length.toLocaleString(),
        hint: 'Inside the 90-day renewal window',
        icon: Clock3,
      },
      {
        label: 'Page Value',
        value: formatMoney(items.reduce((sum, item) => sum + item.contractValue, 0), items[0]?.currency || 'USD'),
        hint: 'Visible contract value only',
        icon: TrendingUp,
      },
    ];
  }, [result]);

  const scopedAccountName = selectedContract?.businessPartnerId === scopedBusinessPartnerId
    ? selectedContract.businessPartnerName
    : result?.items.find((item) => item.businessPartnerId === scopedBusinessPartnerId)?.businessPartnerName;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Contracts</h1>
          <p className="text-muted-foreground">
            Commercial commitments and renewal exposure surfaced in CRM from the live procurement contract record.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/renewals">Renewals</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/projects">Open Projects</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadContracts(page)}>
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

      {(scopedBusinessPartnerId || expiringOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This contract workspace is filtered to the current CRM account or renewal queue.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account: {scopedAccountName || scopedBusinessPartnerId}</Badge> : null}
            {expiringOnly ? <Badge variant="secondary">Renewal window only</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/contracts">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search contract number, title, account, or term metadata.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-[1.4fr_0.8fr_0.8fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search contract, account, type, or payment terms"
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
              {CONTRACT_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="flex items-center justify-between rounded-lg border px-4 py-2">
            <div>
              <div className="text-sm font-medium">Renewal Queue</div>
              <div className="text-xs text-muted-foreground">Show expiring contracts only</div>
            </div>
            <Switch checked={expiringOnly} onCheckedChange={(checked) => {
              setPage(1);
              setExpiringOnly(checked);
            }} />
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Contract Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} contracts matched` : 'Loading contract records'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM contracts...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM contracts matched the current filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Contract</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>End Date</TableHead>
                    <TableHead className="text-right">Value</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((contract) => (
                    <TableRow
                      key={contract.contractId}
                      className={selectedContractId === contract.contractId ? 'bg-muted/40' : ''}
                      onClick={() => setSelectedContractId(contract.contractId)}
                    >
                      <TableCell>
                        <div className="font-medium">{contract.contractTitle}</div>
                        <div className="text-xs text-muted-foreground">
                          {contract.contractNumber}
                          {contract.businessPartnerName ? ` | ${contract.businessPartnerName}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={contract.isExpiringSoon ? 'destructive' : 'outline'}>{contract.status}</Badge>
                          {contract.isActive ? <Badge variant="secondary">Active</Badge> : null}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(contract.endDate)}</TableCell>
                      <TableCell className="text-right">
                        <div>{formatMoney(contract.contractValue, contract.currency)}</div>
                        <div className="text-xs text-muted-foreground">{contract.activeProjectCount} active projects</div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Contract Detail</CardTitle>
            <CardDescription>Commercial and renewal context for the selected contract.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading contract detail...</div> : null}
            {!detailLoading && !selectedContract ? (
              <div className="py-16 text-center text-muted-foreground">Select a contract to inspect its CRM delivery and renewal context.</div>
            ) : null}
            {!detailLoading && selectedContract ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">{selectedContract.contractTitle}</div>
                    <div className="text-sm text-muted-foreground">
                      {selectedContract.contractNumber}
                      {selectedContract.businessPartnerName ? ` | ${selectedContract.businessPartnerName}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={selectedContract.isExpiringSoon ? 'destructive' : 'outline'}>{selectedContract.status}</Badge>
                    {selectedContract.isActive ? <Badge variant="secondary">Active</Badge> : null}
                  </div>
                </div>

                <div className="grid gap-3 md:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Schedule</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Start: {formatDate(selectedContract.startDate)}</div>
                      <div>Signed: {formatDate(selectedContract.signedDate)}</div>
                      <div>End: {formatDate(selectedContract.endDate)}</div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Commercial</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Value: {formatMoney(selectedContract.contractValue, selectedContract.currency)}</div>
                      <div>Type: {selectedContract.contractType}</div>
                      <div>Payment terms: {selectedContract.paymentTerms || 'Not set'}</div>
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Delivery Link</div>
                  <div className="mt-2 space-y-1 text-sm">
                    <div>Related projects: {selectedContract.projectCount}</div>
                    <div>Active projects: {selectedContract.activeProjectCount}</div>
                    <div>Tender reference: {selectedContract.tenderId}</div>
                  </div>
                </div>

                {selectedContract.scopeOfWork || selectedContract.deliverables || selectedContract.notes ? (
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Contract Notes</div>
                    <div className="mt-2 space-y-2 text-sm text-muted-foreground">
                      {selectedContract.scopeOfWork ? <p>{selectedContract.scopeOfWork}</p> : null}
                      {selectedContract.deliverables ? <p>{selectedContract.deliverables}</p> : null}
                      {selectedContract.notes ? <p>{selectedContract.notes}</p> : null}
                    </div>
                  </div>
                ) : null}

                <div className="flex flex-wrap gap-2">
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/crm/accounts/${selectedContract.businessPartnerId}`}>Open Account</Link>
                  </Button>
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/crm/projects?contractId=${selectedContract.contractId}`}>Open Projects</Link>
                  </Button>
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/procurement/contracts/${selectedContract.contractId}`}>Open Source Contract</Link>
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
