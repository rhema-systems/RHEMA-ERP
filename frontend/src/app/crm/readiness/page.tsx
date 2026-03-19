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
  type CrmReadinessDetailDto,
  type CrmReadinessListItemDto,
  type CrmReadinessSignalDto,
  type PagedResult,
} from '@/services/crmService';
import {
  AlertTriangle,
  ArrowRight,
  BarChart3,
  FileText,
  RefreshCw,
  Search,
  ShieldCheck,
} from 'lucide-react';
import { toast } from 'sonner';

const READINESS_CATEGORY_OPTIONS = ['Ready', 'Watch', 'Gap', 'Critical'];
const PARTNER_TYPE_OPTIONS = ['Customer', 'Both', 'Vendor', 'Prospect'];

const formatMoney = (value?: number, currency: string = 'USD') =>
  typeof value === 'number'
    ? new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value)
    : 'None';

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getSignalVariant = (signal: CrmReadinessSignalDto): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (signal.severity === 'critical') {
    return 'destructive';
  }

  if (signal.severity === 'positive') {
    return 'secondary';
  }

  return 'outline';
};

export default function CrmReadinessPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const requestedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const initialCategory = searchParams.get('readinessCategory') || 'all';
  const initialPartnerType = searchParams.get('partnerType') || 'all';
  const initialExpiringOnly = searchParams.get('expiringOnly') === 'true';
  const initialMissingFinancialsOnly = searchParams.get('missingFinancialsOnly') === 'true';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [readinessCategory, setReadinessCategory] = useState(initialCategory);
  const [partnerType, setPartnerType] = useState(initialPartnerType);
  const [expiringOnly, setExpiringOnly] = useState(initialExpiringOnly);
  const [missingFinancialsOnly, setMissingFinancialsOnly] = useState(initialMissingFinancialsOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmReadinessListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedBusinessPartnerId, setSelectedBusinessPartnerId] = useState(requestedBusinessPartnerId);
  const [selectedDetail, setSelectedDetail] = useState<CrmReadinessDetailDto | null>(null);

  const loadReadiness = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getReadiness({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        readinessCategory: readinessCategory === 'all' ? undefined : readinessCategory,
        expiringOnly,
        missingFinancialsOnly,
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
      toast.error(getMessage(error, 'Failed to load CRM readiness'));
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
      setSelectedDetail(await crmService.getReadinessDetail(businessPartnerId, 8));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM readiness detail'));
      setSelectedDetail(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadReadiness(page);
  }, [page, readinessCategory, partnerType, expiringOnly, missingFinancialsOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadReadiness(1);
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
        hint: 'CRM accounts under the current readiness filters',
        icon: ShieldCheck,
      },
      {
        label: 'Critical Gaps',
        value: items.filter((item) => item.hasCriticalGap).length.toLocaleString(),
        hint: 'Accounts missing key qualification evidence',
        icon: AlertTriangle,
      },
      {
        label: 'Expiring Items',
        value: items.reduce((sum, item) => sum + item.expiringDocumentCount + item.expiringLicenseCount, 0).toLocaleString(),
        hint: 'Documents and licenses nearing expiry on this page',
        icon: FileText,
      },
      {
        label: 'Missing Financials',
        value: items.filter((item) => item.financialRecordCount === 0).length.toLocaleString(),
        hint: 'Accounts without a financial history on file',
        icon: BarChart3,
      },
    ];
  }, [result]);

  const selectedSummary = result?.items.find((item) => item.businessPartnerId === selectedBusinessPartnerId);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Readiness</h1>
          <p className="text-muted-foreground">
            Qualification and compliance view built from the existing BusinessPartner document, license, and financial record backbone.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/accounts">Open Accounts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/risk">
              <AlertTriangle className="mr-2 h-4 w-4" />
              Risk
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">Reports</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadReadiness(page)}>
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

      {(readinessCategory !== 'all' || partnerType !== 'all' || expiringOnly || missingFinancialsOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This readiness workspace is focused on a narrower qualification watchlist.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {readinessCategory !== 'all' ? <Badge variant="secondary">Category: {readinessCategory}</Badge> : null}
            {partnerType !== 'all' ? <Badge variant="secondary">Partner Type: {partnerType}</Badge> : null}
            {expiringOnly ? <Badge variant="secondary">Expiring only</Badge> : null}
            {missingFinancialsOnly ? <Badge variant="secondary">Missing financials</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/readiness">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search account identity, readiness band, and qualification gaps.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-[1.4fr_0.9fr_0.9fr]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search account, territory, type, or credit rating"
              />
            </div>

            <Select value={readinessCategory} onValueChange={(value) => {
              setPage(1);
              setReadinessCategory(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Readiness category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All readiness states</SelectItem>
                {READINESS_CATEGORY_OPTIONS.map((option) => (
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
                <div className="text-sm font-medium">Expiring Evidence</div>
                <div className="text-xs text-muted-foreground">Only show accounts with expiring or expired documents and licenses.</div>
              </div>
              <Switch checked={expiringOnly} onCheckedChange={(checked) => {
                setPage(1);
                setExpiringOnly(checked);
              }} />
            </div>

            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">Missing Financials</div>
                <div className="text-xs text-muted-foreground">Focus on accounts without any financial history on file.</div>
              </div>
              <Switch checked={missingFinancialsOnly} onCheckedChange={(checked) => {
                setPage(1);
                setMissingFinancialsOnly(checked);
              }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Readiness Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} accounts matched` : 'Loading CRM readiness'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM readiness...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM accounts matched the current readiness filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Account</TableHead>
                    <TableHead>Readiness</TableHead>
                    <TableHead>Evidence</TableHead>
                    <TableHead>Financials</TableHead>
                    <TableHead>Commercial</TableHead>
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
                          <Badge variant={item.hasCriticalGap ? 'destructive' : 'outline'}>{item.readinessCategory}</Badge>
                          <Badge variant="secondary">{formatScore(item.readinessScore)}</Badge>
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          Health {formatScore(item.healthScore)} | {item.healthCategory}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.documentCount} docs | {item.licenseCount} licenses</div>
                        <div className="text-xs text-muted-foreground">
                          {item.expiredDocumentCount + item.expiredLicenseCount} expired | {item.expiringDocumentCount + item.expiringLicenseCount} expiring
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.financialRecordCount} records</div>
                        <div className="text-xs text-muted-foreground">
                          {item.latestFinancialYear || 'No year'} | {item.creditRating || 'No rating'}
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
            <CardTitle>Readiness Preview</CardTitle>
            <CardDescription>
              {selectedSummary ? `${selectedSummary.partnerName} | ${selectedSummary.partnerCode}` : 'Select an account from the register'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading readiness preview...</div> : null}
            {!detailLoading && !selectedDetail ? (
              <div className="py-16 text-center text-muted-foreground">Select an account to inspect its readiness detail.</div>
            ) : null}

            {!detailLoading && selectedDetail ? (
              <>
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Readiness</div>
                      <div className="mt-2 text-3xl font-semibold">{formatScore(selectedDetail.readinessScore)}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {selectedDetail.readinessCategory} | Next compliance {formatDate(selectedDetail.nextComplianceDate)}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={selectedDetail.hasCriticalGap ? 'destructive' : 'outline'}>
                        {selectedDetail.hasCriticalGap ? 'Critical gap' : 'In watch'}
                      </Badge>
                      <Badge variant="secondary">{selectedDetail.healthCategory}</Badge>
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
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Latest Financial</div>
                    <div className="mt-2 font-medium">{selectedDetail.latestFinancialYear || 'No financial year'}</div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {formatMoney(selectedDetail.latestAnnualRevenue)} | {selectedDetail.creditRating || 'No rating'}
                    </div>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Top Signals</div>
                  {!selectedDetail.signals.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No readiness signals are available yet.</div>
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
                    <FileText className="h-4 w-4 text-muted-foreground" />
                    Documents & Licenses
                  </div>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-center justify-between gap-3">
                      <span>Documents</span>
                      <span>{selectedDetail.documentCount} total | {selectedDetail.verifiedDocumentCount} verified</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Licenses</span>
                      <span>{selectedDetail.licenseCount} total | {selectedDetail.expiredLicenseCount} expired</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Financials</span>
                      <span>{selectedDetail.financialRecordCount} records</span>
                    </div>
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Documents</div>
                  {!selectedDetail.documents.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No documents are on file for this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.documents.map((document) => (
                        <div key={document.documentId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">{document.documentName}</div>
                              <div className="text-muted-foreground">{document.documentType}</div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              {document.isVerified ? <Badge variant="secondary">Verified</Badge> : <Badge variant="outline">Unverified</Badge>}
                              {document.isExpired ? <Badge variant="destructive">Expired</Badge> : null}
                              {document.isExpiringSoon ? <Badge variant="outline">Expiring</Badge> : null}
                            </div>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            Issue {formatDate(document.issueDate)} | Expiry {formatDate(document.expiryDate)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Licenses</div>
                  {!selectedDetail.licenses.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No licenses are on file for this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.licenses.map((license) => (
                        <div key={license.licenseId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">{license.licenseTypeName}</div>
                              <div className="text-muted-foreground">{license.licenseNumber}</div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <Badge variant={license.isExpired ? 'destructive' : 'outline'}>{license.status}</Badge>
                              {license.isExpiringSoon ? <Badge variant="outline">Expiring</Badge> : null}
                            </div>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            Authority {license.issuingAuthority || 'Unknown'} | Expiry {formatDate(license.expiryDate)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Financial History</div>
                  {!selectedDetail.financials.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No financial records are on file for this account.</div>
                  ) : (
                    <div className="grid gap-3">
                      {selectedDetail.financials.map((financial) => (
                        <div key={financial.financialId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div className="font-medium">{financial.financialYear}</div>
                            <div className="flex flex-wrap gap-2">
                              {financial.isAudited ? <Badge variant="secondary">Audited</Badge> : <Badge variant="outline">Unaudited</Badge>}
                              {financial.creditRating ? <Badge variant="outline">{financial.creditRating}</Badge> : null}
                            </div>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            Revenue {formatMoney(financial.annualRevenue)} | Profit {formatMoney(financial.netProfit)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button asChild>
                    <Link href={`/crm/accounts/${selectedDetail.businessPartnerId}`}>
                      Open Account
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/opportunities?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Pipeline
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/contacts?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Contacts
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
