'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { crmService, type CrmForecastDto } from '@/services/crmService';
import { AlertTriangle, BarChart3, Megaphone, RefreshCw, Target, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';

const MONTH_OPTIONS = [3, 6, 12];
const OPPORTUNITY_TYPE_OPTIONS = ['New Business', 'Existing Customer', 'Renewal'];

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatPercent = (value: number) => `${value.toFixed(1)}%`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getCoverageVariant = (category: string): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (category === 'Critical' || category === 'Urgent') {
    return 'destructive';
  }

  if (category === 'Gap') {
    return 'outline';
  }

  if (category === 'Covered') {
    return 'default';
  }

  return 'secondary';
};

export default function CrmForecastPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';

  const [months, setMonths] = useState(Number(searchParams.get('months') || 6));
  const [opportunityType, setOpportunityType] = useState(searchParams.get('opportunityType') || 'all');
  const [forecast, setForecast] = useState<CrmForecastDto | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      setLoading(true);
      setForecast(await crmService.getForecast({
        months,
        opportunityType: opportunityType === 'all' ? undefined : opportunityType,
        businessPartnerId: scopedBusinessPartnerId || undefined,
      }));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM forecast'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [months, opportunityType, scopedBusinessPartnerId]);

  const summary = useMemo(() => {
    if (!forecast) {
      return [];
    }

    return [
      {
        label: 'Weighted Pipeline',
        value: formatMoney(forecast.weightedPipelineValue),
        hint: `${forecast.opportunityCount} opportunities across ${forecast.horizonMonths} months`,
        icon: TrendingUp,
      },
      {
        label: 'Commit View',
        value: formatMoney(forecast.commitValue),
        hint: `${formatPercent(forecast.averageProbability)} average win probability`,
        icon: Target,
      },
      {
        label: 'Campaign-Backed',
        value: formatMoney(forecast.campaignBackedWeightedValue),
        hint: 'Weighted value tied to active campaign-supported leads',
        icon: Megaphone,
      },
      {
        label: 'Renewal Gap',
        value: formatMoney(forecast.renewalGapValue),
        hint: `${formatMoney(forecast.renewalContractValue)} expiring contract value in view`,
        icon: AlertTriangle,
      },
    ];
  }, [forecast]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Forecast</h1>
          <p className="text-muted-foreground">
            Forward-looking pipeline, renewal coverage, and campaign-backed close outlook built from the existing CRM and ERP delivery graph.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/reports">Reports</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/conversions">Conversions</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/renewals">Renewals</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/opportunities">
              <BarChart3 className="mr-2 h-4 w-4" />
              Opportunities
            </Link>
          </Button>
          <Button variant="outline" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {summary.map((metric) => {
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

      {(scopedBusinessPartnerId || opportunityType !== 'all' || months !== 6) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>The forecast is focused on a narrower CRM slice than the default module-wide outlook.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account scoped</Badge> : null}
            {opportunityType !== 'all' ? <Badge variant="secondary">Type: {opportunityType}</Badge> : null}
            {months !== 6 ? <Badge variant="secondary">{months}-month horizon</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/forecast">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Forecast Filters</CardTitle>
          <CardDescription>Adjust the planning horizon and commercial motion in view.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <Select value={String(months)} onValueChange={(value) => setMonths(Number(value))}>
            <SelectTrigger>
              <SelectValue placeholder="Forecast horizon" />
            </SelectTrigger>
            <SelectContent>
              {MONTH_OPTIONS.map((option) => (
                <SelectItem key={option} value={String(option)}>
                  {option} months
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={opportunityType} onValueChange={setOpportunityType}>
            <SelectTrigger>
              <SelectValue placeholder="Opportunity type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All opportunity types</SelectItem>
              {OPPORTUNITY_TYPE_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM forecast...</div> : null}

      {!loading && forecast ? (
        <>
          <div className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
            <Card>
              <CardHeader>
                <CardTitle>Monthly Outlook</CardTitle>
                <CardDescription>Forecast buckets by month across commit, weighted, quoted, and renewal-covered value.</CardDescription>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Period</TableHead>
                      <TableHead>Opportunities</TableHead>
                      <TableHead className="text-right">Weighted</TableHead>
                      <TableHead className="text-right">Commit</TableHead>
                      <TableHead className="text-right">Renewals</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {forecast.buckets.map((bucket) => (
                      <TableRow key={bucket.periodStart}>
                        <TableCell>
                          <div className="font-medium">{bucket.periodLabel}</div>
                          <div className="text-xs text-muted-foreground">
                            {bucket.campaignBackedOpportunityCount} campaign-backed | {bucket.renewalOpportunityCount} renewal deals
                          </div>
                        </TableCell>
                        <TableCell>{bucket.opportunityCount}</TableCell>
                        <TableCell className="text-right">
                          <div>{formatMoney(bucket.weightedValue)}</div>
                          <div className="text-xs text-muted-foreground">{formatMoney(bucket.bestCaseValue)} best case</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatMoney(bucket.commitValue)}</div>
                          <div className="text-xs text-muted-foreground">{formatMoney(bucket.quoteCoverageValue)} quoted</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatMoney(bucket.renewalCoverageValue)}</div>
                          <div className="text-xs text-muted-foreground">{formatMoney(bucket.renewalContractValue)} expiring</div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>High-Confidence Deals</CardTitle>
                <CardDescription>Opportunities in the horizon with enough probability to influence the commit plan.</CardDescription>
              </CardHeader>
              <CardContent>
                {!forecast.highConfidenceDeals.length ? (
                  <div className="py-12 text-center text-muted-foreground">No high-confidence opportunities are in the current horizon.</div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Opportunity</TableHead>
                        <TableHead>Forecast</TableHead>
                        <TableHead className="text-right">Weighted</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {forecast.highConfidenceDeals.map((deal) => (
                        <TableRow key={deal.opportunityId}>
                          <TableCell>
                            <div className="font-medium">
                              <Link href={`/crm/opportunities?opportunityId=${deal.opportunityId}`} className="hover:underline">
                                {deal.name}
                              </Link>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {deal.businessPartnerName || deal.leadName || deal.opportunityType}
                              {deal.campaignContext ? ` | ${deal.campaignContext}` : ''}
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge variant={deal.forecastCategory === 'Commit' ? 'default' : 'secondary'}>
                              {deal.forecastCategory}
                            </Badge>
                            <div className="mt-1 text-xs text-muted-foreground">
                              {deal.stage} | {deal.quoteCount} quotes | {deal.campaignCount} campaigns
                            </div>
                          </TableCell>
                          <TableCell className="text-right">
                            <div>{formatMoney(deal.weightedValue, deal.currency)}</div>
                            <div className="text-xs text-muted-foreground">
                              {deal.probability}% | closes {formatDate(deal.expectedCloseDate)}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Renewal Coverage Watchlist</CardTitle>
              <CardDescription>Expiring contracts compared with renewal coverage already in the pipeline.</CardDescription>
            </CardHeader>
            <CardContent>
              {!forecast.renewalWatchlist.length ? (
                <div className="py-12 text-center text-muted-foreground">No expiring contracts were found inside the current horizon.</div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Contract</TableHead>
                      <TableHead>Coverage</TableHead>
                      <TableHead className="text-right">Gap</TableHead>
                      <TableHead className="text-right">Campaign Support</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {forecast.renewalWatchlist.map((renewal) => (
                      <TableRow key={renewal.contractId}>
                        <TableCell>
                          <div className="font-medium">
                            <Link href={`/crm/contracts?contractId=${renewal.contractId}`} className="hover:underline">
                              {renewal.contractTitle}
                            </Link>
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {renewal.contractNumber} | {renewal.businessPartnerName} | ends {formatDate(renewal.endDate)}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={getCoverageVariant(renewal.coverageCategory)}>{renewal.coverageCategory}</Badge>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {renewal.renewalOpportunityCount} renewal deals | {formatMoney(renewal.renewalWeightedValue)} weighted
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatMoney(renewal.coverageGapValue)}</div>
                          <div className="text-xs text-muted-foreground">{formatMoney(renewal.contractValue)} contract value</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{renewal.activeCampaignCount}</div>
                          <div className="text-xs text-muted-foreground">{formatMoney(renewal.renewalCommitValue)} commit coverage</div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </>
      ) : null}
    </div>
  );
}
