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
  type CrmRiskDetailDto,
  type CrmRiskListItemDto,
  type CrmRiskSignalDto,
  type PagedResult,
} from '@/services/crmService';
import { AlertTriangle, ArrowRight, BarChart3, RefreshCw, Search, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';

const RISK_CATEGORY_OPTIONS = ['Low', 'Guarded', 'Elevated', 'Critical'];
const PARTNER_TYPE_OPTIONS = ['Customer', 'Both', 'Vendor', 'Prospect'];

const formatMoney = (value?: number, currency: string = 'USD') =>
  typeof value === 'number'
    ? new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value)
    : 'None';

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getSignalVariant = (signal: CrmRiskSignalDto): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (signal.scoreImpact >= 10 || signal.severity === 'critical') {
    return 'destructive';
  }

  if (signal.scoreImpact > 0) {
    return 'outline';
  }

  if (signal.scoreImpact < 0) {
    return 'secondary';
  }

  return 'default';
};

export default function CrmRiskPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const requestedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const initialRiskCategory = searchParams.get('riskCategory') || 'all';
  const initialPartnerType = searchParams.get('partnerType') || 'all';
  const initialEscalationOnly = searchParams.get('escalationOnly') === 'true';
  const initialOpenIncidentOnly = searchParams.get('openIncidentOnly') === 'true';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [riskCategory, setRiskCategory] = useState(initialRiskCategory);
  const [partnerType, setPartnerType] = useState(initialPartnerType);
  const [escalationOnly, setEscalationOnly] = useState(initialEscalationOnly);
  const [openIncidentOnly, setOpenIncidentOnly] = useState(initialOpenIncidentOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmRiskListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedBusinessPartnerId, setSelectedBusinessPartnerId] = useState(requestedBusinessPartnerId);
  const [selectedDetail, setSelectedDetail] = useState<CrmRiskDetailDto | null>(null);

  const loadRisk = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getRisk({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        riskCategory: riskCategory === 'all' ? undefined : riskCategory,
        escalationOnly,
        openIncidentOnly,
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
      toast.error(getMessage(error, 'Failed to load CRM account risk'));
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
      setSelectedDetail(await crmService.getRiskDetail(businessPartnerId, 8));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM account risk detail'));
      setSelectedDetail(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadRisk(page);
  }, [page, riskCategory, partnerType, escalationOnly, openIncidentOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadRisk(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadDetail(selectedBusinessPartnerId);
  }, [selectedBusinessPartnerId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Accounts',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Accounts in the current CRM risk register',
        icon: ShieldCheck,
      },
      {
        label: 'Escalations',
        value: items.filter((item) => item.requiresEscalation).length.toLocaleString(),
        hint: 'Accounts that need active intervention',
        icon: AlertTriangle,
      },
      {
        label: 'Open Incidents',
        value: items.reduce((sum, item) => sum + item.openIncidentCount, 0).toLocaleString(),
        hint: 'Open quality issues on this page',
        icon: AlertTriangle,
      },
      {
        label: 'Pending Appeals',
        value: items.reduce((sum, item) => sum + item.pendingAppealCount, 0).toLocaleString(),
        hint: 'Blacklist appeals still under review',
        icon: BarChart3,
      },
    ];
  }, [result]);

  const selectedSummary = result?.items.find((item) => item.businessPartnerId === selectedBusinessPartnerId);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Risk</h1>
          <p className="text-muted-foreground">
            Account risk workspace built from BusinessPartner risk fields, partner performance metrics, quality incidents, and blacklist appeals.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/accounts">Accounts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/readiness">Readiness</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">Reports</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadRisk(page)}>
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

      {(riskCategory !== 'all' || partnerType !== 'all' || escalationOnly || openIncidentOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This risk workspace is focused on a narrower escalation watchlist.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {riskCategory !== 'all' ? <Badge variant="secondary">Category: {riskCategory}</Badge> : null}
            {partnerType !== 'all' ? <Badge variant="secondary">Partner Type: {partnerType}</Badge> : null}
            {escalationOnly ? <Badge variant="secondary">Escalations only</Badge> : null}
            {openIncidentOnly ? <Badge variant="secondary">Open incidents only</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/risk">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search account identity, risk band, and operational risk exposure.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-[1.4fr_0.9fr_0.9fr]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search account, territory, risk level, or metric grade"
              />
            </div>

            <Select value={riskCategory} onValueChange={(value) => {
              setPage(1);
              setRiskCategory(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Risk category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All risk states</SelectItem>
                {RISK_CATEGORY_OPTIONS.map((option) => (
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
                <div className="text-sm font-medium">Escalation Queue</div>
                <div className="text-xs text-muted-foreground">Only show accounts that need immediate CRM escalation.</div>
              </div>
              <Switch checked={escalationOnly} onCheckedChange={(checked) => {
                setPage(1);
                setEscalationOnly(checked);
              }} />
            </div>

            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">Open Incident Exposure</div>
                <div className="text-xs text-muted-foreground">Focus on accounts with active quality incident backlog.</div>
              </div>
              <Switch checked={openIncidentOnly} onCheckedChange={(checked) => {
                setPage(1);
                setOpenIncidentOnly(checked);
              }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Risk Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} accounts matched` : 'Loading CRM account risk'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM account risk...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM accounts matched the current risk filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Account</TableHead>
                    <TableHead>Risk</TableHead>
                    <TableHead>Incidents</TableHead>
                    <TableHead>Performance</TableHead>
                    <TableHead>Exposure</TableHead>
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
                          <Badge variant={item.requiresEscalation ? 'destructive' : 'outline'}>{item.riskCategory}</Badge>
                          <Badge variant="secondary">{formatScore(item.riskScore)}</Badge>
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          {item.riskLevel || 'No master risk'}
                          {item.isBlacklisted ? ' | Blacklisted' : ''}
                          {item.isOnCreditHold ? ' | Credit hold' : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.openIncidentCount} open | {item.criticalIncidentCount} severe</div>
                        <div className="text-xs text-muted-foreground">
                          {item.pendingAppealCount} appeals | {item.openReviewFollowUpCount} review actions
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.latestMetricPeriod || 'No metric snapshot'}</div>
                        <div className="text-xs text-muted-foreground">
                          {typeof item.latestMetricScore === 'number' ? formatScore(item.latestMetricScore) : 'No score'}
                          {item.latestMetricGrade ? ` | ${item.latestMetricGrade}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.openOpportunityCount} opps | {item.activeContractCount} contracts</div>
                        <div className="text-xs text-muted-foreground">{item.activeProjectCount} active projects</div>
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
            <CardTitle>Risk Preview</CardTitle>
            <CardDescription>
              {selectedSummary ? `${selectedSummary.partnerName} | ${selectedSummary.partnerCode}` : 'Select an account from the register'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading risk preview...</div> : null}
            {!detailLoading && !selectedDetail ? (
              <div className="py-16 text-center text-muted-foreground">Select an account to inspect its CRM risk detail.</div>
            ) : null}

            {!detailLoading && selectedDetail ? (
              <>
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Risk</div>
                      <div className="mt-2 text-3xl font-semibold">{formatScore(selectedDetail.riskScore)}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {selectedDetail.riskCategory} | Health {formatScore(selectedDetail.healthScore)} | {selectedDetail.healthCategory}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={selectedDetail.requiresEscalation ? 'destructive' : 'outline'}>
                        {selectedDetail.requiresEscalation ? 'Escalate' : 'Monitored'}
                      </Badge>
                      {selectedDetail.isBlacklisted ? <Badge variant="destructive">Blacklisted</Badge> : null}
                      {selectedDetail.isOnCreditHold ? <Badge variant="destructive">Credit Hold</Badge> : null}
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
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Latest Performance Snapshot</div>
                    <div className="mt-2 font-medium">{selectedDetail.latestMetricPeriod || 'No metric snapshot'}</div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {typeof selectedDetail.latestMetricScore === 'number' ? formatScore(selectedDetail.latestMetricScore) : 'No score'}
                      {selectedDetail.latestMetricGrade ? ` | ${selectedDetail.latestMetricGrade}` : ''}
                    </div>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Top Signals</div>
                  {!selectedDetail.signals.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No risk signals are available yet.</div>
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

                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 font-medium">
                    <BarChart3 className="h-4 w-4 text-muted-foreground" />
                    Exposure Summary
                  </div>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-center justify-between gap-3">
                      <span>Pipeline</span>
                      <span>{selectedDetail.openOpportunityCount} opportunities</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Delivery</span>
                      <span>{selectedDetail.activeProjectCount} projects | {selectedDetail.activeContractCount} contracts</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Balance</span>
                      <span>{formatMoney(selectedDetail.outstandingBalance)}</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Credit Limit</span>
                      <span>{formatMoney(selectedDetail.creditLimit)}</span>
                    </div>
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Incidents</div>
                  {!selectedDetail.incidents.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No quality incidents are on file for this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.incidents.map((incident) => (
                        <div key={incident.incidentId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">{incident.incidentNumber}</div>
                              <div className="text-muted-foreground">{incident.incidentType} | {formatDate(incident.incidentDate)}</div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <Badge variant={incident.severity === 'Critical' || incident.severity === 'High' ? 'destructive' : 'outline'}>
                                {incident.severity}
                              </Badge>
                              <Badge variant={incident.status === 'Closed' || incident.status === 'Resolved' ? 'secondary' : 'outline'}>
                                {incident.status}
                              </Badge>
                            </div>
                          </div>
                          <div className="mt-2 text-muted-foreground">{incident.description}</div>
                          <div className="mt-2 text-muted-foreground">
                            Impact {formatMoney(incident.financialImpact)} | Supplier response {formatDate(incident.supplierResponseDate)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="grid gap-4 lg:grid-cols-2">
                  <div className="space-y-3">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Performance Reviews</div>
                    {!selectedDetail.reviews.length ? (
                      <div className="rounded-lg border p-4 text-sm text-muted-foreground">No performance reviews are on file yet.</div>
                    ) : (
                      <div className="space-y-2">
                        {selectedDetail.reviews.map((review) => (
                          <div key={review.reviewId} className="rounded-lg border p-3 text-sm">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">{review.reviewNumber}</div>
                                <div className="text-muted-foreground">{review.reviewPeriod} | {formatDate(review.reviewDate)}</div>
                              </div>
                              <div className="flex flex-wrap gap-2">
                                <Badge variant={review.requiresFollowUp ? 'destructive' : 'secondary'}>
                                  {review.requiresFollowUp ? 'Follow-up' : 'Closed loop'}
                                </Badge>
                                <Badge variant="outline">{review.status}</Badge>
                              </div>
                            </div>
                            <div className="mt-2 text-muted-foreground">
                              {review.overallScore.toFixed(1)}/5
                              {review.overallGrade ? ` | ${review.overallGrade}` : ''}
                              {review.followUpDate ? ` | Follow-up ${formatDate(review.followUpDate)}` : ''}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  <div className="space-y-3">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Blacklist Appeals</div>
                    {!selectedDetail.appeals.length ? (
                      <div className="rounded-lg border p-4 text-sm text-muted-foreground">No blacklist appeals are on file for this account.</div>
                    ) : (
                      <div className="space-y-2">
                        {selectedDetail.appeals.map((appeal) => (
                          <div key={appeal.appealId} className="rounded-lg border p-3 text-sm">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">{appeal.appealNumber}</div>
                                <div className="text-muted-foreground">{formatDate(appeal.appealDate)}</div>
                              </div>
                              <Badge variant={appeal.status === 'Approved' ? 'secondary' : appeal.status === 'Rejected' ? 'destructive' : 'outline'}>
                                {appeal.status}
                              </Badge>
                            </div>
                            <div className="mt-2 text-muted-foreground">
                              Reviewed {formatDate(appeal.reviewedDate)} | Approved {formatDate(appeal.approvedDate)}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Performance Snapshots</div>
                  {!selectedDetail.performanceMetrics.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No periodic performance metrics are on file for this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.performanceMetrics.map((metric) => (
                        <div key={metric.metricId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">{metric.metricPeriod}</div>
                              <div className="text-muted-foreground">Calculated {formatDate(metric.calculatedAt)}</div>
                            </div>
                            <Badge variant={metric.overallPerformanceScore >= 75 ? 'secondary' : metric.overallPerformanceScore < 60 ? 'destructive' : 'outline'}>
                              {formatScore(metric.overallPerformanceScore)}
                            </Badge>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            Compliance {formatScore(metric.complianceScore)} | Quality {formatScore(metric.qualityAcceptanceRate)} | On time {formatScore(metric.onTimeDeliveryRate)}
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
                    <Link href={`/crm/readiness?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Readiness
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/contracts?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Contracts
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/tenders?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Tenders
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
