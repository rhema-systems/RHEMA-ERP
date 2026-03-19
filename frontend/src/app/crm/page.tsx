'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { crmService, type CrmOverviewDto } from '@/services/crmService';
import { Activity, AlertTriangle, ArrowRight, BarChart3, FileText, Mail, Megaphone, MessageSquare, RefreshCw, ShieldCheck, TrendingUp, Workflow } from 'lucide-react';
import { toast } from 'sonner';

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

export default function CrmPage() {
  const [overview, setOverview] = useState<CrmOverviewDto | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      setLoading(true);
      setOverview(await crmService.getOverview(10));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM overview'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const summary = useMemo(() => {
    if (!overview) {
      return null;
    }

    return [
      { label: 'Open Pipeline', value: overview.openOpportunityCount.toLocaleString(), hint: formatMoney(overview.openOpportunityValue) },
      { label: 'Weighted Pipeline', value: formatMoney(overview.weightedPipelineValue), hint: `${overview.qualifiedLeadCount} qualified leads` },
      { label: 'Active Quotes', value: overview.activeQuoteCount.toLocaleString(), hint: formatMoney(overview.activeQuoteValue) },
      { label: 'Active Accounts', value: overview.activeAccountCount.toLocaleString(), hint: `${overview.atRiskAccountCount} at risk` },
      { label: 'Avg Health', value: formatScore(overview.averageAccountHealthScore), hint: 'Across CRM-linked accounts' },
      { label: 'Follow-Ups', value: overview.leadsNeedingFollowUpCount.toLocaleString(), hint: `${overview.expiringContractCount} contracts expiring` },
    ];
  }, [overview]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM</h1>
          <p className="text-muted-foreground">ERP-native customer relationship overview across pipeline, accounts, and delivery commitments.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/accounts">Manage Accounts</Link>
          </Button>
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
            <Link href="/crm/leads">Manage Leads</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/campaigns">
              <Megaphone className="mr-2 h-4 w-4" />
              Campaigns
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/opportunities">Manage Opportunities</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/renewals">Renewals</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/activities">
              <Activity className="mr-2 h-4 w-4" />
              Activities
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/quotes">
              <FileText className="mr-2 h-4 w-4" />
              Quotes
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/projects">Projects</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/contracts">Contracts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/tenders">Tenders</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/forecast">
              <TrendingUp className="mr-2 h-4 w-4" />
              Forecast
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/conversions">Conversions</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">
              <BarChart3 className="mr-2 h-4 w-4" />
              Reports
            </Link>
          </Button>
          <Button variant="outline" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
        {(summary ?? []).map((item) => (
          <Card key={item.label}>
            <CardHeader className="pb-2">
              <CardDescription>{item.label}</CardDescription>
              <CardTitle>{item.value}</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-xs text-muted-foreground">{item.hint}</CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Top Opportunities</CardTitle>
            <CardDescription>Open deals ranked by weighted value.</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading opportunities...</div> : null}
            {!loading && !overview?.opportunities.length ? <div className="py-10 text-center text-muted-foreground">No open opportunities available.</div> : null}
            {!loading && overview?.opportunities.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Opportunity</TableHead>
                    <TableHead>Stage</TableHead>
                    <TableHead>Expected Close</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {overview.opportunities.map((item) => (
                    <TableRow key={item.opportunityId}>
                      <TableCell>
                        <div className="font-medium">
                          <Link href={`/crm/opportunities?opportunityId=${item.opportunityId}`} className="hover:underline">
                            {item.name}
                          </Link>
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {item.businessPartnerName || item.leadName || item.leadSource} | {item.opportunityType}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant={item.probability >= 70 ? 'default' : 'secondary'}>{item.stage}</Badge>
                      </TableCell>
                      <TableCell>{formatDate(item.expectedCloseDate)}</TableCell>
                      <TableCell className="text-right">
                        <div>{formatMoney(item.amount, item.currency)}</div>
                        <div className="text-xs text-muted-foreground">{item.probability}% | {formatMoney(item.weightedValue, item.currency)}</div>
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
            <CardTitle>Follow-Up Queue</CardTitle>
            <CardDescription>Leads and activities that need attention soon.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading follow-ups...</div> : null}
            {!loading && !overview?.followUps.length ? <div className="py-10 text-center text-muted-foreground">No near-term follow-ups are due.</div> : null}
            {!loading && overview?.followUps.map((item) => (
              <div key={`${item.entityType}-${item.entityId}`} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.title}</div>
                    <div className="text-sm text-muted-foreground">{item.entityType} | {item.context || 'General'}</div>
                  </div>
                  <Badge variant="secondary">{item.status}</Badge>
                </div>
                <div className="mt-2 text-sm text-muted-foreground">Due {formatDate(item.dueDate)}</div>
                {item.entityType === 'Lead' ? (
                  <Button asChild variant="link" className="mt-2 h-auto px-0">
                    <Link href={`/crm/leads?leadId=${item.entityId}&followUpOnly=true`}>
                      Open lead
                      <ArrowRight className="ml-1 h-3 w-3" />
                    </Link>
                  </Button>
                ) : null}
                {item.entityType === 'Activity' ? (
                  <Button asChild variant="link" className="mt-2 h-auto px-0">
                    <Link href={`/crm/activities?activityId=${item.entityId}&followUpOnly=true`}>
                      Open activity
                      <ArrowRight className="ml-1 h-3 w-3" />
                    </Link>
                  </Button>
                ) : null}
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card id="account-360">
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle>Account 360</CardTitle>
              <CardDescription>Customer and partner relationships ranked by active commercial and delivery footprint.</CardDescription>
            </div>
            <Button asChild variant="outline" size="sm">
              <Link href="/crm/accounts">Open Account Workspace</Link>
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? <div className="py-10 text-center text-muted-foreground">Loading account overview...</div> : null}
          {!loading && !overview?.accounts.length ? <div className="py-10 text-center text-muted-foreground">No CRM-linked accounts are available yet.</div> : null}
          {!loading && overview?.accounts.length ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Account</TableHead>
                  <TableHead>Health</TableHead>
                  <TableHead>Projects</TableHead>
                  <TableHead>Contracts</TableHead>
                  <TableHead>Tenders</TableHead>
                  <TableHead>Next Milestone</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {overview.accounts.map((account) => (
                  <TableRow key={account.businessPartnerId}>
                    <TableCell>
                      <div className="font-medium">
                        <Link href={`/crm/accounts/${account.businessPartnerId}`} className="hover:underline">
                          {account.partnerName}
                        </Link>
                      </div>
                      <div className="text-xs text-muted-foreground">{account.partnerCode} | {account.partnerType}{account.salesTerritory ? ` | ${account.salesTerritory}` : ''}</div>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-2">
                        <Badge variant={account.isAtRisk ? 'destructive' : 'outline'}>
                          {account.healthCategory}
                        </Badge>
                        <Badge variant="secondary">{formatScore(account.healthScore)}</Badge>
                        {account.hasOpenFollowUp ? <Badge variant="secondary">Follow-Up</Badge> : null}
                      </div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        {account.riskLevel || 'No risk level'}
                        {account.performanceRating ? ` | ${account.performanceRating.toFixed(1)} rating` : ''}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{account.activeProjectCount}/{account.totalProjectCount} active</div>
                      <div className="text-xs text-muted-foreground">
                        {account.activeProjectCount} projects | {formatMoney(account.projectValue)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{account.activeContractCount}/{account.totalContractCount} active</div>
                      <div className="text-xs text-muted-foreground">
                        {account.activeContractCount} contracts | {formatMoney(account.contractValue)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{account.tenderAwardCount} awards | {account.tenderBidCount} bids</div>
                      <div className="text-xs text-muted-foreground">{formatMoney(account.tenderAwardedValue)}</div>
                    </TableCell>
                    <TableCell>{formatDate(account.nextMilestoneDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}
