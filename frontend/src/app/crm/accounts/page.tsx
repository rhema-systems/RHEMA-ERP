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
import { formatCurrencyAmount as formatMoney } from '@/lib/currency';
import {
  crmService,
  type CrmAccountDetailDto,
  type CrmAccountOverviewDto,
  type PagedResult,
} from '@/services/crmService';
import { AlertTriangle, ArrowRight, Building2, Mail, MessageSquare, RefreshCw, Search, ShieldCheck, Target, TrendingUp, Users, Workflow } from 'lucide-react';
import { toast } from 'sonner';

const HEALTH_OPTIONS = ['Strong', 'Healthy', 'Watch', 'At Risk', 'Critical'];
const PARTNER_TYPE_OPTIONS = ['Customer', 'Both', 'Vendor', 'Prospect'];

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

export default function CrmAccountsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const requestedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const initialAtRiskOnly = searchParams.get('atRiskOnly') === 'true';
  const initialHealthCategory = searchParams.get('healthCategory') || 'all';
  const initialPartnerType = searchParams.get('partnerType') || 'all';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [healthCategory, setHealthCategory] = useState(initialHealthCategory);
  const [partnerType, setPartnerType] = useState(initialPartnerType);
  const [atRiskOnly, setAtRiskOnly] = useState(initialAtRiskOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmAccountOverviewDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedBusinessPartnerId, setSelectedBusinessPartnerId] = useState(requestedBusinessPartnerId);
  const [selectedAccount, setSelectedAccount] = useState<CrmAccountDetailDto | null>(null);

  const loadAccounts = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getAccounts({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        healthCategory: healthCategory === 'all' ? undefined : healthCategory,
        atRiskOnly,
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
      toast.error(getMessage(error, 'Failed to load CRM accounts'));
    } finally {
      setLoading(false);
    }
  };

  const loadAccountDetail = async (businessPartnerId: string) => {
    if (!businessPartnerId) {
      setSelectedAccount(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedAccount(await crmService.getAccountDetail(businessPartnerId, 8));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM account preview'));
      setSelectedAccount(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadAccounts(page);
  }, [page, healthCategory, partnerType, atRiskOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadAccounts(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadAccountDetail(selectedBusinessPartnerId);
  }, [selectedBusinessPartnerId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];
    const averageHealth = items.length
      ? items.reduce((sum, item) => sum + item.healthScore, 0) / items.length
      : 0;

    return [
      {
        label: 'Visible Accounts',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'CRM-relevant accounts under the current filters',
        icon: Users,
      },
      {
        label: 'At Risk',
        value: items.filter((item) => item.isAtRisk).length.toLocaleString(),
        hint: 'Accounts needing escalation or attention',
        icon: AlertTriangle,
      },
      {
        label: 'Visible Pipeline',
        value: formatMoney(items.reduce((sum, item) => sum + item.openOpportunityValue, 0)),
        hint: 'Open opportunity value in this page',
        icon: TrendingUp,
      },
      {
        label: 'Average Health',
        value: formatScore(averageHealth),
        hint: 'Only the accounts on this page',
        icon: Target,
      },
    ];
  }, [result]);

  const selectedSummary = result?.items.find((item) => item.businessPartnerId === selectedBusinessPartnerId);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Accounts</h1>
          <p className="text-muted-foreground">
            BusinessPartner-backed account workspace for commercial exposure, delivery footprint, and health monitoring.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/readiness">
              <ShieldCheck className="mr-2 h-4 w-4" />
              Readiness
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/risk">
              <AlertTriangle className="mr-2 h-4 w-4" />
              Risk
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/contacts">
              <Mail className="mr-2 h-4 w-4" />
              Contacts
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/collaboration">
              <Workflow className="mr-2 h-4 w-4" />
              Collaboration
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/service">
              <MessageSquare className="mr-2 h-4 w-4" />
              Service
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">Open Reports</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadAccounts(page)}>
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

      {(atRiskOnly || healthCategory !== 'all' || partnerType !== 'all') ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This account workspace is currently filtered to a narrower CRM watchlist.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {atRiskOnly ? <Badge variant="secondary">At-risk only</Badge> : null}
            {healthCategory !== 'all' ? <Badge variant="secondary">Health: {healthCategory}</Badge> : null}
            {partnerType !== 'all' ? <Badge variant="secondary">Partner Type: {partnerType}</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/accounts">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search account identity, type, territory, or health profile.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-[1.4fr_0.8fr_0.8fr_0.9fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search account, code, territory, or risk"
            />
          </div>

          <Select value={healthCategory} onValueChange={(value) => {
            setPage(1);
            setHealthCategory(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by health" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All health states</SelectItem>
              {HEALTH_OPTIONS.map((option) => (
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
              <SelectValue placeholder="Filter by partner type" />
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

          <div className="flex items-center justify-between rounded-lg border px-4 py-2">
            <div>
              <div className="text-sm font-medium">At-Risk Watchlist</div>
              <div className="text-xs text-muted-foreground">Only show accounts needing attention</div>
            </div>
            <Switch checked={atRiskOnly} onCheckedChange={(checked) => {
              setPage(1);
              setAtRiskOnly(checked);
            }} />
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Account Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} accounts matched` : 'Loading CRM accounts'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM accounts...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM accounts matched the current filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Account</TableHead>
                    <TableHead>Health</TableHead>
                    <TableHead>Pipeline</TableHead>
                    <TableHead>Delivery</TableHead>
                    <TableHead>Next Milestone</TableHead>
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
                          {item.partnerCode} | {item.partnerType}{item.salesTerritory ? ` | ${item.salesTerritory}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={item.isAtRisk ? 'destructive' : 'outline'}>{item.healthCategory}</Badge>
                          <Badge variant="secondary">{formatScore(item.healthScore)}</Badge>
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          {item.riskLevel || 'No risk level'}
                          {item.hasOpenFollowUp ? ' | Follow-up open' : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{formatMoney(item.openOpportunityValue)}</div>
                        <div className="text-xs text-muted-foreground">
                          {item.openOpportunityCount} opps | {item.activeQuoteCount} quotes
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.activeProjectCount} projects | {item.activeContractCount} contracts</div>
                        <div className="text-xs text-muted-foreground">
                          {formatMoney(item.projectValue + item.contractValue)}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(item.nextMilestoneDate)}</TableCell>
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
            <CardTitle>Account Preview</CardTitle>
            <CardDescription>
              {selectedSummary ? `${selectedSummary.partnerName} | ${selectedSummary.partnerCode}` : 'Select an account from the register'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading account preview...</div> : null}
            {!detailLoading && !selectedAccount ? (
              <div className="py-16 text-center text-muted-foreground">Select an account to inspect its CRM summary.</div>
            ) : null}

            {!detailLoading && selectedAccount ? (
              <>
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Health</div>
                      <div className="mt-2 text-3xl font-semibold">{formatScore(selectedAccount.healthScore)}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {selectedAccount.healthCategory}{selectedAccount.riskLevel ? ` | ${selectedAccount.riskLevel} risk` : ''}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={selectedAccount.isAtRisk ? 'destructive' : 'outline'}>
                        {selectedAccount.isAtRisk ? 'Escalate' : 'Stable'}
                      </Badge>
                      {selectedAccount.hasOpenFollowUp ? <Badge variant="secondary">Needs Attention</Badge> : null}
                    </div>
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Pipeline</div>
                    <div className="mt-2 text-xl font-semibold">
                      {formatMoney(selectedAccount.openOpportunityValue, selectedAccount.currency || 'USD')}
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {selectedAccount.openOpportunityCount} opps | {selectedAccount.activeQuoteCount} active quotes
                    </div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Delivery</div>
                    <div className="mt-2 text-xl font-semibold">
                      {selectedAccount.activeProjectCount} projects | {selectedAccount.activeContractCount} contracts
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {formatMoney(selectedAccount.projectValue + selectedAccount.contractValue, selectedAccount.currency || 'USD')}
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-medium">{selectedAccount.primaryContactName || 'Primary contact not set'}</div>
                      <div className="text-sm text-muted-foreground">
                        {selectedAccount.primaryEmail || selectedAccount.primaryPhone || 'No contact channel on file'}
                      </div>
                    </div>
                    <Badge variant="outline">{selectedAccount.contacts.length} contacts</Badge>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Top Signals</div>
                  {!selectedAccount.healthSignals.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No health signals are available for this account yet.</div>
                  ) : (
                    selectedAccount.healthSignals.slice(0, 4).map((signal) => (
                      <div key={`${signal.label}-${signal.scoreImpact}`} className="flex items-center justify-between rounded-lg border p-3 text-sm">
                        <div className="font-medium">{signal.label}</div>
                        <Badge variant={signal.scoreImpact < 0 ? 'destructive' : 'secondary'}>
                          {signal.scoreImpact > 0 ? `+${signal.scoreImpact}` : signal.scoreImpact}
                        </Badge>
                      </div>
                    ))
                  )}
                </div>

                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 font-medium">
                    <Building2 className="h-4 w-4 text-muted-foreground" />
                    Current Footprint
                  </div>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-center justify-between">
                      <span>Tenders</span>
                      <span>{selectedAccount.tenderAwardCount} awards | {selectedAccount.tenderBidCount} bids</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span>Related leads</span>
                      <span>{selectedAccount.relatedLeadCount}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span>Next milestone</span>
                      <span>{formatDate(selectedAccount.nextMilestoneDate)}</span>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button asChild>
                    <Link href={`/crm/accounts/${selectedAccount.businessPartnerId}`}>
                      Open Full Account
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/opportunities?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Open Pipeline
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/activities?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Activities
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/contacts?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Contacts
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/collaboration?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Collaboration
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/service?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Service
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/readiness?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Readiness
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/risk?businessPartnerId=${selectedAccount.businessPartnerId}`}>
                      Risk
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
