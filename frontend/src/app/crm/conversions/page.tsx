'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { crmService, type CrmConversionsDto } from '@/services/crmService';
import { formatCrmAmount, formatCrmCurrencyTotals, formatCrmMissingCurrencyCount } from '../crmMoney';
import { BarChart3, BriefcaseBusiness, FileText, RefreshCw, TrendingUp, Workflow } from 'lucide-react';
import { toast } from 'sonner';

const MONTH_OPTIONS = [3, 6, 12];
const OPPORTUNITY_TYPE_OPTIONS = ['New Business', 'Existing Customer', 'Renewal', 'Upsell'];

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatPercent = (value: number) => `${value.toFixed(1)}%`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getCoverageVariant = (status: string): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (status === 'Project Live' || status === 'Closed Won') {
    return 'default';
  }

  if (status === 'Contract Ready' || status === 'Quoted') {
    return 'secondary';
  }

  if (status === 'Closed Lost') {
    return 'outline';
  }

  return 'destructive';
};

const getLeakageVariant = (entityType: string, status: string): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (entityType === 'Lead') {
    return 'secondary';
  }

  if (status === 'Closed Lost') {
    return 'outline';
  }

  return 'destructive';
};

const buildChainLabel = (stages: string[]) => stages.join(' -> ');

const resolveLeakHref = (entityType: string, entityId: string) =>
  entityType === 'Lead'
    ? `/crm/leads?leadId=${entityId}`
    : `/crm/opportunities?opportunityId=${entityId}`;

export default function CrmConversionsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';

  const [months, setMonths] = useState(Number(searchParams.get('months') || 6));
  const [opportunityType, setOpportunityType] = useState(searchParams.get('opportunityType') || 'all');
  const [conversions, setConversions] = useState<CrmConversionsDto | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      setLoading(true);
      setConversions(await crmService.getConversions({
        months,
        opportunityType: opportunityType === 'all' ? undefined : opportunityType,
        businessPartnerId: scopedBusinessPartnerId || undefined,
      }));
    } catch (error: unknown) {
      setConversions(null);
      toast.error(getMessage(error, 'Failed to load CRM conversions'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [months, opportunityType, scopedBusinessPartnerId]);

  const summary = useMemo(() => {
    if (!conversions) {
      return [];
    }

    return [
      {
        label: 'Lead -> Opportunity',
        value: formatPercent(conversions.leadToOpportunityRate),
        hint: `${conversions.leadWithOpportunityCount} of ${conversions.leadCount} leads progressed`,
        icon: TrendingUp,
      },
      {
        label: 'Opportunity -> Quote',
        value: formatPercent(conversions.opportunityToQuoteRate),
        hint: `${conversions.quotedOpportunityCount} of ${conversions.opportunityCount} opportunities quoted`,
        icon: FileText,
      },
      {
        label: 'Opportunity -> Contract',
        value: formatPercent(conversions.opportunityToContractRate),
        hint: `${conversions.contractBackedOpportunityCount} account-linked contract paths in scope`,
        icon: Workflow,
      },
      {
        label: 'Contract -> Project',
        value: formatPercent(conversions.opportunityToProjectRate),
        hint: `${conversions.projectBackedOpportunityCount} opportunities already show delivery coverage`,
        icon: BriefcaseBusiness,
      },
    ];
  }, [conversions]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Conversions</h1>
          <p className="text-muted-foreground">
            Follow lead-to-delivery progression across opportunities, quotes, contracts, and projects without duplicating ERP ownership.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/forecast">Forecast</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">Reports</Link>
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
            <CardDescription>The conversions register is focused on a narrower CRM slice than the default module-wide view.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account scoped</Badge> : null}
            {opportunityType !== 'all' ? <Badge variant="secondary">Type: {opportunityType}</Badge> : null}
            {months !== 6 ? <Badge variant="secondary">{months}-month lookback</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/conversions">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Conversion Filters</CardTitle>
          <CardDescription>Focus the chain on a lookback period or a specific commercial motion.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <Select value={String(months)} onValueChange={(value) => setMonths(Number(value))}>
            <SelectTrigger>
              <SelectValue placeholder="Lookback period" />
            </SelectTrigger>
            <SelectContent>
              {MONTH_OPTIONS.map((option) => (
                <SelectItem key={option} value={String(option)}>
                  Last {option} months
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

      {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM conversions...</div> : null}

      {!loading && conversions ? (
        <>
          <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
            <Card>
              <CardHeader>
                <CardTitle>Conversion Funnel</CardTitle>
                <CardDescription>Stage-by-stage entity counts and value across the current CRM conversion window.</CardDescription>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Stage</TableHead>
                      <TableHead>Entities</TableHead>
                      <TableHead>Related Opportunities</TableHead>
                      <TableHead className="text-right">Value</TableHead>
                      <TableHead className="text-right">Rate</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {conversions.funnel.map((stage) => (
                      <TableRow key={stage.stage}>
                        <TableCell className="font-medium">{stage.stage}</TableCell>
                        <TableCell>{stage.entityCount}</TableCell>
                        <TableCell>{stage.relatedOpportunityCount}</TableCell>
                        <TableCell className="text-right">
                          <div>{formatCrmCurrencyTotals(stage.valuesByCurrency)}</div>
                          {formatCrmMissingCurrencyCount(stage.unspecifiedCurrencyCount) ? (
                            <div className="text-xs text-muted-foreground">{formatCrmMissingCurrencyCount(stage.unspecifiedCurrencyCount)}</div>
                          ) : null}
                        </TableCell>
                        <TableCell className="text-right">{formatPercent(stage.conversionRate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Leakage Watchlist</CardTitle>
                <CardDescription>Leads and opportunities that are stalling before the next conversion step lands.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="rounded-lg border p-4">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Weighted Pipeline</div>
                  <div className="mt-2 break-words text-2xl font-semibold">{formatCrmCurrencyTotals(conversions.weightedPipelineValuesByCurrency)}</div>
                  <div className="mt-1 text-xs text-muted-foreground">
                    {formatCrmCurrencyTotals(conversions.totalOpportunityValuesByCurrency)} total opportunity value in the current chain window
                  </div>
                  {formatCrmMissingCurrencyCount(conversions.opportunitiesWithoutCurrencyCount) ? (
                    <div className="mt-1 text-xs text-muted-foreground">{formatCrmMissingCurrencyCount(conversions.opportunitiesWithoutCurrencyCount)}</div>
                  ) : null}
                </div>

                {!conversions.leakage.length ? (
                  <div className="py-10 text-center text-muted-foreground">No leaks are currently flagged in the active conversion window.</div>
                ) : (
                  conversions.leakage.slice(0, 8).map((item) => (
                    <div key={`${item.entityType}-${item.entityId}`} className="rounded-lg border p-4">
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <div>
                          <div className="font-medium">{item.name}</div>
                          <div className="text-xs text-muted-foreground">
                            {item.businessPartnerName || item.entityType} | {item.status}
                          </div>
                        </div>
                        <Badge variant={getLeakageVariant(item.entityType, item.status)}>
                          {item.leakageStage}
                        </Badge>
                      </div>
                      <div className="mt-3 text-sm text-muted-foreground">{item.leakageReason}</div>
                      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                        <div>
                          {item.amount !== undefined ? formatCrmAmount(item.amount, item.currency) : 'No amount recorded'}
                          {item.referenceDate ? ` | ${formatDate(item.referenceDate)}` : ''}
                        </div>
                        <Link href={resolveLeakHref(item.entityType, item.entityId)} className="font-medium text-foreground hover:underline">
                          Open record
                        </Link>
                      </div>
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Conversion Journeys</CardTitle>
              <CardDescription>Opportunity-by-opportunity view of the chain from lead through quote and into contract/project coverage.</CardDescription>
            </CardHeader>
            <CardContent>
              {!conversions.journeys.length ? (
                <div className="py-12 text-center text-muted-foreground">No opportunity journeys are inside the current conversion window.</div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Opportunity</TableHead>
                      <TableHead>Chain</TableHead>
                      <TableHead>Coverage</TableHead>
                      <TableHead className="text-right">Expected Close</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {conversions.journeys.map((journey) => (
                      <TableRow key={journey.opportunityId}>
                        <TableCell>
                          <div className="font-medium">
                            <Link href={`/crm/opportunities?opportunityId=${journey.opportunityId}`} className="hover:underline">
                              {journey.opportunityName}
                            </Link>
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {journey.businessPartnerName || journey.leadName || journey.opportunityType}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="font-medium">{buildChainLabel(journey.chain.nodes.map((node) => node.stage))}</div>
                          <div className="text-xs text-muted-foreground">
                            {journey.quoteCount} quotes | {journey.contractCount} contracts | {journey.projectCount} projects
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={getCoverageVariant(journey.coverageStatus)}>{journey.coverageStatus}</Badge>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {journey.leakageReason || `${journey.stage} | ${journey.opportunityType}`}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatDate(journey.expectedCloseDate)}</div>
                          <div className="text-xs text-muted-foreground">
                            {formatCrmAmount(journey.weightedValue, journey.currency)} weighted
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Open Leakage Register</CardTitle>
              <CardDescription>Full list of stalled conversion items that still need CRM follow-through.</CardDescription>
            </CardHeader>
            <CardContent>
              {!conversions.leakage.length ? (
                <div className="py-12 text-center text-muted-foreground">No leakage items are open right now.</div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Record</TableHead>
                      <TableHead>Leak</TableHead>
                      <TableHead className="text-right">Reference</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {conversions.leakage.map((item) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <div className="font-medium">
                            <Link href={resolveLeakHref(item.entityType, item.entityId)} className="hover:underline">
                              {item.name}
                            </Link>
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {item.entityType} | {item.businessPartnerName || item.status}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={getLeakageVariant(item.entityType, item.status)}>{item.leakageStage}</Badge>
                          <div className="mt-1 text-xs text-muted-foreground">{item.leakageReason}</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{item.referenceDate ? formatDate(item.referenceDate) : 'None'}</div>
                          <div className="text-xs text-muted-foreground">
                            {item.amount !== undefined ? formatCrmAmount(item.amount, item.currency) : 'No amount'}
                          </div>
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

      {!loading && !conversions ? (
        <div className="rounded-lg border border-dashed py-16 text-center text-muted-foreground">
          CRM conversions could not be loaded.
        </div>
      ) : null}
    </div>
  );
}
