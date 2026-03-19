'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  crmService,
  type CrmAccountOverviewDto,
  type CrmActivityListItemDto,
  type CrmContractListItemDto,
  type CrmOpportunityListItemDto,
} from '@/services/crmService';
import { AlertTriangle, RefreshCw, Search, ShieldCheck, Siren, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';

type RenewalWatchlistItem = {
  contract: CrmContractListItemDto;
  account?: CrmAccountOverviewDto;
  relatedOpportunities: CrmOpportunityListItemDto[];
  relatedActivities: CrmActivityListItemDto[];
};

const CLOSED_STAGES = new Set(['Closed Won', 'Closed Lost']);

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatPercent = (value: number) => `${value.toFixed(0)}%`;
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

export default function CrmRenewalsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [missingPipelineOnly, setMissingPipelineOnly] = useState(searchParams.get('missingPipelineOnly') === 'true');
  const [atRiskOnly, setAtRiskOnly] = useState(searchParams.get('atRiskOnly') === 'true');

  const [loading, setLoading] = useState(true);
  const [contracts, setContracts] = useState<CrmContractListItemDto[]>([]);
  const [accounts, setAccounts] = useState<CrmAccountOverviewDto[]>([]);
  const [renewalOpportunities, setRenewalOpportunities] = useState<CrmOpportunityListItemDto[]>([]);
  const [activities, setActivities] = useState<CrmActivityListItemDto[]>([]);

  const load = async () => {
    try {
      setLoading(true);
      const [contractResult, accountResult, opportunityResult, activityResult] = await Promise.all([
        crmService.getContracts({ page: 1, pageSize: 50, expiringOnly: true }),
        crmService.getAccounts({ page: 1, pageSize: 50 }),
        crmService.getOpportunities({ page: 1, pageSize: 50, opportunityType: 'Renewal' }),
        crmService.getActivities({ page: 1, pageSize: 50, followUpOnly: true }),
      ]);

      setContracts(contractResult.items);
      setAccounts(accountResult.items);
      setRenewalOpportunities(opportunityResult.items.filter((item) => !CLOSED_STAGES.has(item.stage)));
      setActivities(activityResult.items);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM renewals'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const watchlist = useMemo(() => {
    const accountLookup = new Map(accounts.map((account) => [account.businessPartnerId, account]));
    const activitiesByAccountId = new Map<string, CrmActivityListItemDto[]>();

    activities.forEach((activity) => {
      if (!activity.businessPartnerId) {
        return;
      }

      const scopedActivities = activitiesByAccountId.get(activity.businessPartnerId) || [];
      scopedActivities.push(activity);
      activitiesByAccountId.set(activity.businessPartnerId, scopedActivities);
    });

    const mapped = contracts.map((contract) => {
      const relatedOpportunities = renewalOpportunities
        .filter((opportunity) => opportunity.businessPartnerId === contract.businessPartnerId)
        .sort((left, right) => new Date(left.expectedCloseDate).getTime() - new Date(right.expectedCloseDate).getTime());
      const relatedActivities = activitiesByAccountId.get(contract.businessPartnerId) || [];

      return {
        contract,
        account: accountLookup.get(contract.businessPartnerId),
        relatedOpportunities,
        relatedActivities,
      } satisfies RenewalWatchlistItem;
    });

    return mapped
      .filter((item) => {
        if (!search.trim()) {
          return true;
        }

        const query = search.trim().toLowerCase();
        return [
          item.contract.contractNumber,
          item.contract.contractTitle,
          item.contract.businessPartnerName,
          item.account?.partnerName,
          item.relatedOpportunities[0]?.name,
        ]
          .filter((value): value is string => typeof value === 'string' && value.length > 0)
          .some((value) => value.toLowerCase().includes(query));
      })
      .filter((item) => !missingPipelineOnly || item.relatedOpportunities.length === 0)
      .filter((item) => !atRiskOnly || item.account?.isAtRisk)
      .sort((left, right) => new Date(left.contract.endDate || '').getTime() - new Date(right.contract.endDate || '').getTime());
  }, [accounts, activities, contracts, renewalOpportunities, search, missingPipelineOnly, atRiskOnly]);

  const coverageRate = useMemo(() => {
    if (!watchlist.length) {
      return 0;
    }

    return (watchlist.filter((item) => item.relatedOpportunities.length > 0).length / watchlist.length) * 100;
  }, [watchlist]);

  const relevantActivities = useMemo(() => watchlist
    .flatMap((item) => item.relatedActivities)
    .sort((left, right) => {
      const leftTime = new Date(left.dueDate || left.activityDate).getTime();
      const rightTime = new Date(right.dueDate || right.activityDate).getTime();
      return leftTime - rightTime;
    }), [watchlist]);

  const atRiskAccounts = useMemo(() => {
    const uniqueAccounts = new Map<string, CrmAccountOverviewDto>();
    watchlist.forEach((item) => {
      if (item.account?.isAtRisk) {
        uniqueAccounts.set(item.account.businessPartnerId, item.account);
      }
    });

    return Array.from(uniqueAccounts.values())
      .sort((left, right) => right.healthScore - left.healthScore);
  }, [watchlist]);

  const metrics = useMemo(() => [
    {
      label: 'Renewal Exposure',
      value: formatMoney(watchlist.reduce((sum, item) => sum + item.contract.contractValue, 0)),
      hint: `${watchlist.length} expiring contracts in scope`,
      icon: TrendingUp,
    },
    {
      label: 'Coverage Rate',
      value: formatPercent(coverageRate),
      hint: `${watchlist.filter((item) => item.relatedOpportunities.length > 0).length} contracts already have a renewal deal`,
      icon: ShieldCheck,
    },
    {
      label: 'Coverage Gaps',
      value: watchlist.filter((item) => item.relatedOpportunities.length === 0).length.toLocaleString(),
      hint: 'Expiring contracts without an open renewal opportunity',
      icon: Siren,
    },
    {
      label: 'At-Risk Accounts',
      value: atRiskAccounts.length.toLocaleString(),
      hint: `${relevantActivities.length} relevant follow-up items in queue`,
      icon: AlertTriangle,
    },
  ], [atRiskAccounts.length, coverageRate, relevantActivities.length, watchlist]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Renewals</h1>
          <p className="text-muted-foreground">
            Retention workspace for expiring contracts, renewal deals, at-risk accounts, and follow-up pressure.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/contracts?expiringOnly=true">Open Contracts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/opportunities?opportunityType=Renewal">Renewal Pipeline</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">Reports</Link>
          </Button>
          <Button variant="outline" onClick={() => void load()}>
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

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Focus the renewal watchlist on gaps, risk, or a specific contract/account.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-[1.5fr_0.9fr_0.9fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search contract, account, or renewal opportunity"
            />
          </div>

          <div className="flex items-center justify-between rounded-lg border px-4 py-2">
            <div>
              <div className="text-sm font-medium">Missing Pipeline</div>
              <div className="text-xs text-muted-foreground">Only show uncovered renewals</div>
            </div>
            <Switch checked={missingPipelineOnly} onCheckedChange={setMissingPipelineOnly} />
          </div>

          <div className="flex items-center justify-between rounded-lg border px-4 py-2">
            <div>
              <div className="text-sm font-medium">At-Risk Only</div>
              <div className="text-xs text-muted-foreground">Focus on deteriorating accounts</div>
            </div>
            <Switch checked={atRiskOnly} onCheckedChange={setAtRiskOnly} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Renewal Watchlist</CardTitle>
          <CardDescription>Contract-by-contract coverage view that highlights renewal gaps before they become churn risk.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM renewals...</div> : null}
          {!loading && !watchlist.length ? (
            <div className="py-16 text-center text-muted-foreground">No renewal items matched the current filters.</div>
          ) : null}
          {!loading && watchlist.length ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Contract</TableHead>
                  <TableHead>Renewal Coverage</TableHead>
                  <TableHead>Account Health</TableHead>
                  <TableHead>Follow-Up</TableHead>
                  <TableHead>End Date</TableHead>
                  <TableHead className="text-right">Value</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {watchlist.map((item) => {
                  const primaryOpportunity = item.relatedOpportunities[0];

                  return (
                    <TableRow key={item.contract.contractId}>
                      <TableCell>
                        <div className="font-medium">
                          <Link href={`/crm/contracts?contractId=${item.contract.contractId}&businessPartnerId=${item.contract.businessPartnerId}`} className="hover:underline">
                            {item.contract.contractTitle}
                          </Link>
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {item.contract.contractNumber}
                          {item.contract.businessPartnerName ? ` | ${item.contract.businessPartnerName}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        {primaryOpportunity ? (
                          <div className="space-y-1">
                            <Badge>{item.relatedOpportunities.length} linked</Badge>
                            <div className="text-xs text-muted-foreground">
                              <Link
                                href={`/crm/opportunities?businessPartnerId=${item.contract.businessPartnerId}&opportunityType=Renewal&opportunityId=${primaryOpportunity.opportunityId}`}
                                className="hover:underline"
                              >
                                {primaryOpportunity.name}
                              </Link>
                            </div>
                          </div>
                        ) : (
                          <div className="space-y-1">
                            <Badge variant="destructive">Gap</Badge>
                            <div className="text-xs text-muted-foreground">
                              <Link
                                href={`/crm/opportunities?businessPartnerId=${item.contract.businessPartnerId}&opportunityType=Renewal&new=1`}
                                className="hover:underline"
                              >
                                Create renewal opportunity
                              </Link>
                            </div>
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        {item.account ? (
                          <div className="space-y-1">
                            <div className="flex flex-wrap gap-2">
                              <Badge variant={item.account.isAtRisk ? 'destructive' : 'outline'}>
                                {item.account.healthCategory}
                              </Badge>
                              <Badge variant="secondary">{formatScore(item.account.healthScore)}</Badge>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              <Link href={`/crm/accounts/${item.account.businessPartnerId}`} className="hover:underline">
                                Open account
                              </Link>
                            </div>
                          </div>
                        ) : (
                          <span className="text-sm text-muted-foreground">No account summary</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={item.relatedActivities.length > 0 ? 'secondary' : 'outline'}>
                            {item.relatedActivities.length} items
                          </Badge>
                        </div>
                        {item.relatedActivities[0] ? (
                          <div className="mt-1 text-xs text-muted-foreground">
                            <Link href={`/crm/activities?activityId=${item.relatedActivities[0].activityId}`} className="hover:underline">
                              {item.relatedActivities[0].subject}
                            </Link>
                          </div>
                        ) : null}
                      </TableCell>
                      <TableCell>{formatDate(item.contract.endDate)}</TableCell>
                      <TableCell className="text-right">{formatMoney(item.contract.contractValue, item.contract.currency)}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          ) : null}
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Renewal Pipeline</CardTitle>
            <CardDescription>Open renewal opportunities already in flight.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {!renewalOpportunities.length ? (
              <div className="py-10 text-center text-muted-foreground">No open renewal opportunities are active right now.</div>
            ) : (
              renewalOpportunities.slice(0, 8).map((opportunity) => (
                <div key={opportunity.opportunityId} className="rounded-lg border p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">
                        <Link href={`/crm/opportunities?opportunityId=${opportunity.opportunityId}&opportunityType=Renewal`} className="hover:underline">
                          {opportunity.name}
                        </Link>
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {opportunity.businessPartnerName || 'Unassigned account'} | {opportunity.stage}
                      </div>
                    </div>
                    <Badge variant={opportunity.probability >= 70 ? 'default' : 'secondary'}>
                      {opportunity.probability}%
                    </Badge>
                  </div>
                  <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
                    <span>Close {formatDate(opportunity.expectedCloseDate)}</span>
                    <span>{formatMoney(opportunity.amount, opportunity.currency)}</span>
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>At-Risk Accounts</CardTitle>
            <CardDescription>Accounts in the renewal window that already show health deterioration.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {!atRiskAccounts.length ? (
              <div className="py-10 text-center text-muted-foreground">No at-risk renewal accounts are currently in scope.</div>
            ) : (
              atRiskAccounts.slice(0, 8).map((account) => (
                <div key={account.businessPartnerId} className="rounded-lg border p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">
                        <Link href={`/crm/accounts/${account.businessPartnerId}`} className="hover:underline">
                          {account.partnerName}
                        </Link>
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {account.partnerCode}
                        {account.riskLevel ? ` | ${account.riskLevel} risk` : ''}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant="destructive">{account.healthCategory}</Badge>
                      <Badge variant="secondary">{formatScore(account.healthScore)}</Badge>
                    </div>
                  </div>
                  <div className="mt-3 text-sm text-muted-foreground">
                    {account.openOpportunityCount} opps | {account.activeContractCount} contracts | {account.activeQuoteCount} quotes
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Renewal Follow-Ups</CardTitle>
            <CardDescription>Activities already queued against accounts inside the renewal watchlist.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {!relevantActivities.length ? (
              <div className="py-10 text-center text-muted-foreground">No renewal follow-up items are currently queued.</div>
            ) : (
              relevantActivities.slice(0, 8).map((activity) => (
                <div key={activity.activityId} className="rounded-lg border p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">
                        <Link href={`/crm/activities?activityId=${activity.activityId}`} className="hover:underline">
                          {activity.subject}
                        </Link>
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {activity.businessPartnerName || 'General CRM context'} | {activity.activityStatus}
                      </div>
                    </div>
                    <Badge variant={activity.isOverdue ? 'destructive' : 'secondary'}>
                      {activity.isOverdue ? 'Overdue' : activity.activityType}
                    </Badge>
                  </div>
                  <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
                    <span>Due {formatDate(activity.dueDate || activity.activityDate)}</span>
                    <span>{activity.priority === 1 ? 'High priority' : `Priority ${activity.priority}`}</span>
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
