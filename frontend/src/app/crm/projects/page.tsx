'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  crmService,
  type CrmProjectDetailDto,
  type CrmProjectListItemDto,
  type PagedResult,
} from '@/services/crmService';
import { Activity, Briefcase, Clock3, RefreshCw, Search, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';

const PROJECT_STATUS_OPTIONS = ['Draft', 'PendingApproval', 'Approved', 'Planned', 'InProgress', 'OnHold', 'Completed'];

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatPercent = (value: number) => `${value.toFixed(0)}%`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

export default function CrmProjectsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const scopedContractId = searchParams.get('contractId') || '';
  const requestedProjectId = searchParams.get('projectId') || '';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [status, setStatus] = useState(searchParams.get('status') || 'all');
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmProjectListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedProjectId, setSelectedProjectId] = useState(requestedProjectId);
  const [selectedProject, setSelectedProject] = useState<CrmProjectDetailDto | null>(null);

  const loadProjects = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getProjects({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        contractId: scopedContractId || undefined,
      });

      setResult(data);

      if (requestedProjectId && requestedPage === 1) {
        setSelectedProjectId(requestedProjectId);
        return;
      }

      if (selectedProjectId && data.items.some((item) => item.projectId === selectedProjectId)) {
        return;
      }

      setSelectedProjectId(data.items[0]?.projectId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM projects'));
    } finally {
      setLoading(false);
    }
  };

  const loadProjectDetail = async (projectId: string) => {
    if (!projectId) {
      setSelectedProject(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedProject(await crmService.getProject(projectId));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM project'));
      setSelectedProject(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadProjects(page);
  }, [page, status, scopedBusinessPartnerId, scopedContractId]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadProjects(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadProjectDetail(selectedProjectId);
  }, [selectedProjectId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Projects',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Filtered delivery records',
        icon: Briefcase,
      },
      {
        label: 'Active Delivery',
        value: items.filter((item) => !['Completed', 'Closed', 'Cancelled', 'Archived'].includes(item.status)).length.toLocaleString(),
        hint: 'Open project execution on this page',
        icon: Activity,
      },
      {
        label: 'Overdue',
        value: items.filter((item) => item.isOverdue).length.toLocaleString(),
        hint: 'Target end date already passed',
        icon: Clock3,
      },
      {
        label: 'Page Value',
        value: formatMoney(items.reduce((sum, item) => sum + item.value, 0)),
        hint: 'Approved or estimated budget total',
        icon: TrendingUp,
      },
    ];
  }, [result]);

  const scopedAccountName = selectedProject?.resolvedBusinessPartnerId === scopedBusinessPartnerId
    ? selectedProject.businessPartnerName
    : result?.items.find((item) => item.resolvedBusinessPartnerId === scopedBusinessPartnerId)?.businessPartnerName;
  const scopedContractName = selectedProject?.contractId === scopedContractId
    ? selectedProject.contractNumber || selectedProject.contractTitle
    : result?.items.find((item) => item.contractId === scopedContractId)?.contractNumber;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Projects</h1>
          <p className="text-muted-foreground">
            Delivery execution in CRM, sourced directly from project records already linked to the account and contract spine.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/contracts">Open Contracts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadProjects(page)}>
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

      {(scopedBusinessPartnerId || scopedContractId) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This project workspace was opened from another CRM drill-in and is filtered to that context.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account: {scopedAccountName || scopedBusinessPartnerId}</Badge> : null}
            {scopedContractId ? <Badge variant="secondary">Contract: {scopedContractName || scopedContractId}</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/projects">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search project code, title, account, or linked contract context.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1.6fr_0.8fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search project, account, contract, or status notes"
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
              {PROJECT_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Project Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} projects matched` : 'Loading project records'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM projects...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM projects matched the current filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Project</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Target End</TableHead>
                    <TableHead className="text-right">Value</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((project) => (
                    <TableRow
                      key={project.projectId}
                      className={selectedProjectId === project.projectId ? 'bg-muted/40' : ''}
                      onClick={() => setSelectedProjectId(project.projectId)}
                    >
                      <TableCell>
                        <div className="font-medium">{project.title}</div>
                        <div className="text-xs text-muted-foreground">
                          {project.projectCode}
                          {project.businessPartnerName ? ` | ${project.businessPartnerName}` : ''}
                          {project.contractNumber ? ` | ${project.contractNumber}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={project.isOverdue ? 'destructive' : 'outline'}>{project.status}</Badge>
                          {project.isLinkedToActiveContract ? <Badge variant="secondary">Active Contract</Badge> : null}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(project.targetEndDate)}</TableCell>
                      <TableCell className="text-right">
                        <div>{formatMoney(project.value)}</div>
                        <div className="text-xs text-muted-foreground">{formatPercent(project.progressPercent)}</div>
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
            <CardTitle>Project Detail</CardTitle>
            <CardDescription>CRM delivery context for the selected project.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading project detail...</div> : null}
            {!detailLoading && !selectedProject ? (
              <div className="py-16 text-center text-muted-foreground">Select a project to inspect its delivery context.</div>
            ) : null}
            {!detailLoading && selectedProject ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">{selectedProject.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {selectedProject.projectCode}
                      {selectedProject.businessPartnerName ? ` | ${selectedProject.businessPartnerName}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={selectedProject.isOverdue ? 'destructive' : 'outline'}>{selectedProject.status}</Badge>
                    {selectedProject.isLinkedToActiveContract ? <Badge variant="secondary">Contract-backed</Badge> : null}
                    {selectedProject.externalCollaborationEnabled ? <Badge variant="secondary">External Collaboration</Badge> : null}
                  </div>
                </div>

                <div>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">Progress</span>
                    <span className="font-medium">{formatPercent(selectedProject.progressPercent)}</span>
                  </div>
                  <Progress value={selectedProject.progressPercent} className="mt-2" />
                </div>

                <div className="grid gap-3 md:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Schedule</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Start: {formatDate(selectedProject.startDate)}</div>
                      <div>Target end: {formatDate(selectedProject.targetEndDate)}</div>
                      <div>Actual end: {formatDate(selectedProject.actualEndDate)}</div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Budget</div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>Value: {formatMoney(selectedProject.value)}</div>
                      <div>Approved: {formatMoney(selectedProject.approvedBudget || 0)}</div>
                      <div>Actual cost: {formatMoney(selectedProject.actualCost || 0)}</div>
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Relationship Context</div>
                  <div className="mt-2 space-y-1 text-sm">
                    <div>Account: {selectedProject.businessPartnerName || 'None'}</div>
                    <div>Contract: {selectedProject.contractNumber || selectedProject.contractTitle || 'None'}</div>
                    <div>Budget status: {selectedProject.budgetStatus}</div>
                    <div>Methodology: {selectedProject.methodology}</div>
                  </div>
                </div>

                {selectedProject.summary || selectedProject.objectives || selectedProject.statusRemarks ? (
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Delivery Notes</div>
                    <div className="mt-2 space-y-2 text-sm text-muted-foreground">
                      {selectedProject.summary ? <p>{selectedProject.summary}</p> : null}
                      {selectedProject.objectives ? <p>{selectedProject.objectives}</p> : null}
                      {selectedProject.statusRemarks ? <p>{selectedProject.statusRemarks}</p> : null}
                    </div>
                  </div>
                ) : null}

                <div className="flex flex-wrap gap-2">
                  {selectedProject.resolvedBusinessPartnerId ? (
                    <Button asChild variant="outline" size="sm">
                      <Link href={`/crm/accounts/${selectedProject.resolvedBusinessPartnerId}`}>Open Account</Link>
                    </Button>
                  ) : null}
                  {selectedProject.contractId ? (
                    <Button asChild variant="outline" size="sm">
                      <Link href={`/crm/contracts?contractId=${selectedProject.contractId}`}>Open Contract</Link>
                    </Button>
                  ) : null}
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/development/projects/${selectedProject.projectId}`}>Open Source Project</Link>
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
