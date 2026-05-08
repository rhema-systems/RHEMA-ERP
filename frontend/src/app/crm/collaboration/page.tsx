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
  type CrmCollaborationDetailDto,
  type CrmCollaborationListItemDto,
  type CrmCollaborationSignalDto,
  type PagedResult,
} from '@/services/crmService';
import { ArrowRight, RefreshCw, Search, ShieldCheck, Users, Workflow } from 'lucide-react';
import { toast } from 'sonner';

const COLLABORATION_CATEGORY_OPTIONS = ['Connected', 'Partial', 'Watch', 'Blocked'];
const PARTNER_TYPE_OPTIONS = ['Customer', 'Both', 'Vendor', 'Prospect'];

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const getSignalVariant = (signal: CrmCollaborationSignalDto): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (signal.scoreImpact <= -10 || signal.severity === 'critical') {
    return 'destructive';
  }

  if (signal.scoreImpact < 0) {
    return 'outline';
  }

  if (signal.scoreImpact > 0) {
    return 'secondary';
  }

  return 'default';
};

export default function CrmCollaborationPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const requestedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const initialCollaborationCategory = searchParams.get('collaborationCategory') || 'all';
  const initialPartnerType = searchParams.get('partnerType') || 'all';
  const initialEnablementOnly = searchParams.get('enablementOnly') === 'true';
  const initialPendingOnboardingOnly = searchParams.get('pendingOnboardingOnly') === 'true';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [collaborationCategory, setCollaborationCategory] = useState(initialCollaborationCategory);
  const [partnerType, setPartnerType] = useState(initialPartnerType);
  const [enablementOnly, setEnablementOnly] = useState(initialEnablementOnly);
  const [pendingOnboardingOnly, setPendingOnboardingOnly] = useState(initialPendingOnboardingOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmCollaborationListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedBusinessPartnerId, setSelectedBusinessPartnerId] = useState(requestedBusinessPartnerId);
  const [selectedDetail, setSelectedDetail] = useState<CrmCollaborationDetailDto | null>(null);

  const loadCollaboration = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getCollaboration({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        collaborationCategory: collaborationCategory === 'all' ? undefined : collaborationCategory,
        enablementOnly,
        pendingOnboardingOnly,
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
      toast.error(getMessage(error, 'Failed to load CRM collaboration'));
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
      setSelectedDetail(await crmService.getCollaborationDetail(businessPartnerId, 8));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM collaboration detail'));
      setSelectedDetail(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadCollaboration(page);
  }, [page, collaborationCategory, partnerType, enablementOnly, pendingOnboardingOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadCollaboration(1);
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
        hint: 'Accounts in the current collaboration workspace',
        icon: Workflow,
      },
      {
        label: 'Enablement Gaps',
        value: items.filter((item) => item.requiresEnablement).length.toLocaleString(),
        hint: 'Accounts that need onboarding or access activation',
        icon: ShieldCheck,
      },
      {
        label: 'Active Portal Users',
        value: items.reduce((sum, item) => sum + item.activePortalUserCount, 0).toLocaleString(),
        hint: 'External users active on this page',
        icon: Users,
      },
      {
        label: 'External Projects',
        value: items.reduce((sum, item) => sum + item.portalProjectCount + item.collaborationProjectCount, 0).toLocaleString(),
        hint: 'Projects exposing portal or collaboration access',
        icon: Workflow,
      },
    ];
  }, [result]);

  const selectedSummary = result?.items.find((item) => item.businessPartnerId === selectedBusinessPartnerId);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Collaboration</h1>
          <p className="text-muted-foreground">
            External onboarding, portal access, tender assignment, and project collaboration view built on existing ERP partner records.
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
            <Link href="/crm/tenders">Tenders</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadCollaboration(page)}>
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

      {(collaborationCategory !== 'all' || partnerType !== 'all' || enablementOnly || pendingOnboardingOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This collaboration workspace is focused on a narrower enablement watchlist.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {collaborationCategory !== 'all' ? <Badge variant="secondary">Category: {collaborationCategory}</Badge> : null}
            {partnerType !== 'all' ? <Badge variant="secondary">Partner Type: {partnerType}</Badge> : null}
            {enablementOnly ? <Badge variant="secondary">Enablement only</Badge> : null}
            {pendingOnboardingOnly ? <Badge variant="secondary">Pending onboarding</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/collaboration">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search account identity, collaboration band, and onboarding state.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-[1.4fr_0.9fr_0.9fr]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search account, application number, territory, or status"
              />
            </div>

            <Select value={collaborationCategory} onValueChange={(value) => {
              setPage(1);
              setCollaborationCategory(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Collaboration category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All collaboration states</SelectItem>
                {COLLABORATION_CATEGORY_OPTIONS.map((option) => (
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
                <div className="text-sm font-medium">Enablement Gaps</div>
                <div className="text-xs text-muted-foreground">Only show accounts missing the collaboration basics they need.</div>
              </div>
              <Switch checked={enablementOnly} onCheckedChange={(checked) => {
                setPage(1);
                setEnablementOnly(checked);
              }} />
            </div>

            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">Pending Onboarding</div>
                <div className="text-xs text-muted-foreground">Focus on registrations that are draft, pending, under review, or rejected.</div>
              </div>
              <Switch checked={pendingOnboardingOnly} onCheckedChange={(checked) => {
                setPage(1);
                setPendingOnboardingOnly(checked);
              }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Collaboration Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} accounts matched` : 'Loading CRM collaboration'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM collaboration...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM accounts matched the current collaboration filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Account</TableHead>
                    <TableHead>Collaboration</TableHead>
                    <TableHead>Onboarding</TableHead>
                    <TableHead>Users</TableHead>
                    <TableHead>Delivery</TableHead>
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
                          <Badge variant={item.requiresEnablement ? 'destructive' : 'outline'}>{item.collaborationCategory}</Badge>
                          <Badge variant="secondary">{formatScore(item.collaborationScore)}</Badge>
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          Health {formatScore(item.healthScore)} | {item.healthCategory}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.latestApplicationNumber || 'No application'}</div>
                        <div className="text-xs text-muted-foreground">
                          {item.latestRegistrationLifecycleStatus || 'No registration status'}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.activePortalUserCount} active | {item.adminUserCount} admins</div>
                        <div className="text-xs text-muted-foreground">
                          {item.portalUserCount} total users | {item.tenderAssignmentCount} assignments
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{item.portalProjectCount} portal | {item.collaborationProjectCount} collaboration</div>
                        <div className="text-xs text-muted-foreground">
                          {item.activeContractCount} contracts | {item.openOpportunityCount} opps
                        </div>
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
            <CardTitle>Collaboration Preview</CardTitle>
            <CardDescription>
              {selectedSummary ? `${selectedSummary.partnerName} | ${selectedSummary.partnerCode}` : 'Select an account from the register'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading collaboration preview...</div> : null}
            {!detailLoading && !selectedDetail ? (
              <div className="py-16 text-center text-muted-foreground">Select an account to inspect its CRM collaboration detail.</div>
            ) : null}

            {!detailLoading && selectedDetail ? (
              <>
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Collaboration</div>
                      <div className="mt-2 text-3xl font-semibold">{formatScore(selectedDetail.collaborationScore)}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {selectedDetail.collaborationCategory} | Onboarding {selectedDetail.latestRegistrationLifecycleStatus || 'Not started'}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={selectedDetail.requiresEnablement ? 'destructive' : 'outline'}>
                        {selectedDetail.requiresEnablement ? 'Enablement Needed' : 'Connected'}
                      </Badge>
                      {selectedDetail.latestRegistrationLifecycleStatus && selectedDetail.latestRegistrationLifecycleStatus !== 'Approved' ? (
                        <Badge variant="secondary">{selectedDetail.latestRegistrationLifecycleStatus}</Badge>
                      ) : null}
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
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Latest Application</div>
                    <div className="mt-2 font-medium">{selectedDetail.latestApplicationNumber || 'No registration'}</div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      Submitted {formatDate(selectedDetail.latestRegistrationSubmittedDate)} | Approved {formatDate(selectedDetail.latestRegistrationApprovedDate)}
                    </div>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Top Signals</div>
                  {!selectedDetail.signals.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No collaboration signals are available yet.</div>
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
                    <Workflow className="h-4 w-4 text-muted-foreground" />
                    Collaboration Footprint
                  </div>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-center justify-between gap-3">
                      <span>Portal users</span>
                      <span>{selectedDetail.activePortalUserCount} active | {selectedDetail.portalUserCount} total</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Tenders</span>
                      <span>{selectedDetail.assignedTenderCount} tenders | {selectedDetail.tenderAssignmentCount} assignments</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>External projects</span>
                      <span>{selectedDetail.portalProjectCount} portal | {selectedDetail.collaborationProjectCount} collaboration</span>
                    </div>
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Registrations</div>
                  {!selectedDetail.registrations.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No external onboarding registrations are linked to this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.registrations.map((registration) => (
                        <div key={registration.registrationId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">{registration.applicationNumber}</div>
                              <div className="text-muted-foreground">Created {formatDate(registration.createdAt)}</div>
                            </div>
                            <Badge variant={registration.status === 'Approved' ? 'secondary' : registration.status === 'Rejected' ? 'destructive' : 'outline'}>
                              {registration.status}
                            </Badge>
                          </div>
                          <div className="mt-2 text-muted-foreground">
                            {registration.documentCount} docs | {registration.verifiedDocumentCount} verified | {registration.rejectedDocumentCount} rejected
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="grid gap-4 lg:grid-cols-2">
                  <div className="space-y-3">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Portal Users</div>
                    {!selectedDetail.portalUsers.length ? (
                      <div className="rounded-lg border p-4 text-sm text-muted-foreground">No portal users are linked to this account.</div>
                    ) : (
                      <div className="space-y-2">
                        {selectedDetail.portalUsers.map((user) => (
                          <div key={user.portalUserId} className="rounded-lg border p-3 text-sm">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">{user.fullName}</div>
                                <div className="text-muted-foreground">{user.email || user.userName}</div>
                              </div>
                              <div className="flex flex-wrap gap-2">
                                <Badge variant={user.isActive ? 'secondary' : 'outline'}>{user.isActive ? 'Active' : 'Inactive'}</Badge>
                                <Badge variant="outline">{user.role}</Badge>
                              </div>
                            </div>
                            <div className="mt-2 text-muted-foreground">Granted {formatDate(user.grantedAt)}</div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  <div className="space-y-3">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Tender Assignments</div>
                    {!selectedDetail.tenderAssignments.length ? (
                      <div className="rounded-lg border p-4 text-sm text-muted-foreground">No tender assignments are linked to this account.</div>
                    ) : (
                      <div className="space-y-2">
                        {selectedDetail.tenderAssignments.map((assignment) => (
                          <div key={assignment.assignmentId} className="rounded-lg border p-3 text-sm">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">{assignment.tenderNumber || 'Tender assignment'}</div>
                                <div className="text-muted-foreground">{assignment.tenderTitle || 'No tender title'}</div>
                              </div>
                              <Badge variant="outline">{assignment.assignmentType}</Badge>
                            </div>
                            <div className="mt-2 text-muted-foreground">
                              {assignment.assignedToUserName || 'No named assignee'} | {formatDate(assignment.assignedAt)}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">External Projects</div>
                  {!selectedDetail.projects.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">No external portal or collaboration projects are linked to this account.</div>
                  ) : (
                    <div className="space-y-2">
                      {selectedDetail.projects.map((project) => (
                        <div key={project.projectId} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <div className="font-medium">{project.title}</div>
                              <div className="text-muted-foreground">{project.projectCode} | {project.status}</div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              {project.externalPortalAccessEnabled ? <Badge variant="secondary">Portal</Badge> : null}
                              {project.externalCollaborationEnabled ? <Badge variant="outline">Collaboration</Badge> : null}
                            </div>
                          </div>
                          <div className="mt-2 text-muted-foreground">Target end {formatDate(project.targetEndDate)}</div>
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
                    <Link href={`/crm/contacts?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Contacts
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/tenders?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Tenders
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/readiness?businessPartnerId=${selectedDetail.businessPartnerId}`}>
                      Readiness
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
