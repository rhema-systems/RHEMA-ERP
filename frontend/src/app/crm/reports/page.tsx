'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { crmService, type CrmReportingDto } from '@/services/crmService';
import { Activity, AlertTriangle, BarChart3, RefreshCw, ShieldCheck, TrendingUp, Users } from 'lucide-react';
import { toast } from 'sonner';

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatPercent = (value: number) => `${value.toFixed(1)}%`;
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

export default function CrmReportsPage() {
  const [reporting, setReporting] = useState<CrmReportingDto | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      setLoading(true);
      setReporting(await crmService.getReporting(10));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM reporting'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const summary = useMemo(() => {
    if (!reporting) {
      return [];
    }

    return [
      {
        label: 'Lead Conversion',
        value: formatPercent(reporting.leadConversionRate),
        hint: `${reporting.convertedLeadCount} converted of ${reporting.totalLeadCount}`,
        icon: Users,
      },
      {
        label: 'Weighted Pipeline',
        value: formatMoney(reporting.weightedPipelineValue),
        hint: `${reporting.openOpportunityCount} open opportunities`,
        icon: TrendingUp,
      },
      {
        label: 'Quote Acceptance',
        value: formatPercent(reporting.quoteAcceptanceRate),
        hint: `${reporting.acceptedQuoteCount} accepted quotes`,
        icon: ShieldCheck,
      },
      {
        label: 'Avg Health',
        value: formatScore(reporting.averageAccountHealthScore),
        hint: `${reporting.atRiskAccountCount} accounts at risk`,
        icon: Activity,
      },
    ];
  }, [reporting]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Reporting</h1>
          <p className="text-muted-foreground">
            Pipeline, quote conversion, and account health reporting built directly from the ERP-native CRM slice.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/forecast">Forecast</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/conversions">Conversions</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/renewals">Renewals</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/quotes">
              <BarChart3 className="mr-2 h-4 w-4" />
              Quote Drill-Down
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

      {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM reporting...</div> : null}

      {!loading && reporting ? (
        <>
          <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
            <Card>
              <CardHeader>
                <CardTitle>Pipeline by Stage</CardTitle>
                <CardDescription>Open opportunity and quote density across each current pipeline stage.</CardDescription>
              </CardHeader>
              <CardContent>
                {!reporting.pipelineByStage.length ? (
                  <div className="py-12 text-center text-muted-foreground">No pipeline stages are available.</div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Stage</TableHead>
                        <TableHead>Opportunities</TableHead>
                        <TableHead>Quotes</TableHead>
                        <TableHead className="text-right">Weighted Value</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {reporting.pipelineByStage.map((stage) => (
                        <TableRow key={stage.stage}>
                          <TableCell className="font-medium">{stage.stage}</TableCell>
                          <TableCell>{stage.opportunityCount}</TableCell>
                          <TableCell>{stage.quoteCount}</TableCell>
                          <TableCell className="text-right">
                            <div>{formatMoney(stage.weightedValue)}</div>
                            <div className="text-xs text-muted-foreground">{formatMoney(stage.totalValue)} total</div>
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
                <CardTitle>Health Distribution</CardTitle>
                <CardDescription>Account posture derived from live risk, follow-up, delivery, and commercial signals.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {!reporting.accountHealth.length ? (
                  <div className="py-12 text-center text-muted-foreground">No account health records are available.</div>
                ) : (
                  reporting.accountHealth.map((account) => (
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
                            {account.performanceRating ? ` | ${account.performanceRating.toFixed(1)} rating` : ''}
                          </div>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={account.isAtRisk ? 'destructive' : 'outline'}>{account.healthCategory}</Badge>
                          <Badge variant="secondary">{formatScore(account.healthScore)}</Badge>
                        </div>
                      </div>
                      <Progress value={account.healthScore} className="mt-3" />
                      <div className="mt-3 grid gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                        <div>{account.openOpportunityCount} open opportunities</div>
                        <div>{account.activeProjectCount} projects | {account.activeContractCount} contracts</div>
                        <div>{formatMoney(account.openOpportunityValue)} pipeline</div>
                        <div>{account.hasOpenFollowUp ? 'Follow-up required' : 'No urgent follow-up'}</div>
                      </div>
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Closing Opportunities</CardTitle>
                <CardDescription>Deals expected to close within the near-term reporting window.</CardDescription>
              </CardHeader>
              <CardContent>
                {!reporting.closingOpportunities.length ? (
                  <div className="py-12 text-center text-muted-foreground">No near-term closing opportunities were found.</div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Opportunity</TableHead>
                        <TableHead>Stage</TableHead>
                        <TableHead>Close</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {reporting.closingOpportunities.map((opportunity) => (
                        <TableRow key={opportunity.opportunityId}>
                          <TableCell>
                            <div className="font-medium">
                              <Link href={`/crm/opportunities?opportunityId=${opportunity.opportunityId}`} className="hover:underline">
                                {opportunity.name}
                              </Link>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {opportunity.businessPartnerName || opportunity.leadName || opportunity.leadSource}
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge variant={opportunity.stage === 'Closed Won' ? 'default' : 'outline'}>
                              {opportunity.stage}
                            </Badge>
                          </TableCell>
                          <TableCell>{formatDate(opportunity.expectedCloseDate)}</TableCell>
                          <TableCell className="text-right">
                            <div>{formatMoney(opportunity.amount, opportunity.currency)}</div>
                            <div className="text-xs text-muted-foreground">{opportunity.probability}% probability</div>
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
                <CardTitle>Expiring Contracts</CardTitle>
                <CardDescription>Commercial commitments that will need renewal or intervention soon.</CardDescription>
              </CardHeader>
              <CardContent>
                {!reporting.expiringContracts.length ? (
                  <div className="py-12 text-center text-muted-foreground">No expiring contracts were found in the reporting window.</div>
                ) : (
                  <div className="space-y-3">
                    {reporting.expiringContracts.map((contract) => (
                      <div key={contract.contractId} className="rounded-lg border p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <div className="font-medium">
                              <Link href={`/procurement/contracts/${contract.contractId}`} className="hover:underline">
                                {contract.contractTitle}
                              </Link>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {contract.contractNumber} | {contract.relationshipType}
                            </div>
                          </div>
                          <Badge variant="destructive">
                            <AlertTriangle className="mr-1 h-3 w-3" />
                            Expiring
                          </Badge>
                        </div>
                        <div className="mt-3 grid gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                          <div>Status: {contract.status}</div>
                          <div>End Date: {formatDate(contract.endDate)}</div>
                          <div>Value: {formatMoney(contract.contractValue)}</div>
                          <div>Tender Link: {contract.tenderId}</div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </>
      ) : null}
    </div>
  );
}
