'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  DEFAULT_PROJECT_CURRENCY,
  formatProjectMoney,
  loadProjectCurrencyContext,
  type ProjectCurrencyReference,
} from '@/lib/project-currency';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { format } from 'date-fns';
import { Eye, Filter, Plus, RefreshCw, Settings, Search } from 'lucide-react';
import {
  ProjectDashboardDto,
  ProjectDto,
  ProjectMilestoneTrackerReportItemDto,
  ProjectPortfolioDto,
  ProjectProgramDto,
  ProjectTaskAgingReportItemDto,
  ProjectTypeDto,
  ProjectWorkflowApprovalQueueItemDto,
  projectService,
} from '@/services/projectService';
import { toast } from 'sonner';

const emptyDashboard: ProjectDashboardDto = {
  totalProjects: 0,
  draftProjects: 0,
  activeProjects: 0,
  pendingApprovalProjects: 0,
  completedProjects: 0,
  overdueTasks: 0,
  dueMilestonesThisMonth: 0,
  overdueMilestones: 0,
  openRisks: 0,
  openIssues: 0,
  totalEstimatedBudget: 0,
  totalApprovedBudget: 0,
  totalActualCost: 0,
  atRiskProjects: [],
};

export default function ProjectsPage() {
  const router = useRouter();
  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [types, setTypes] = useState<ProjectTypeDto[]>([]);
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [dashboard, setDashboard] = useState<ProjectDashboardDto>(emptyDashboard);
  const [taskAging, setTaskAging] = useState<ProjectTaskAgingReportItemDto[]>([]);
  const [milestones, setMilestones] = useState<ProjectMilestoneTrackerReportItemDto[]>([]);
  const [workflowQueue, setWorkflowQueue] = useState<ProjectWorkflowApprovalQueueItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [projectTypeId, setProjectTypeId] = useState('all');
  const [portfolioId, setPortfolioId] = useState('all');
  const [programId, setProgramId] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [baseCurrency, setBaseCurrency] = useState<ProjectCurrencyReference>(DEFAULT_PROJECT_CURRENCY);

  const loadData = async () => {
    try {
      setLoading(true);
      const [projectResult, dashboardResult, typeResult, portfolioResult, currencyContext] = await Promise.all([
        projectService.getProjects({
          page,
          pageSize: 20,
          search: search || undefined,
          status: status === 'all' ? undefined : status,
          projectTypeId: projectTypeId === 'all' ? undefined : projectTypeId,
          portfolioId: portfolioId === 'all' ? undefined : portfolioId,
          programId: programId === 'all' ? undefined : programId,
        }),
        projectService.getDashboard(),
        projectService.getProjectTypes(),
        projectService.getPortfolios(),
        loadProjectCurrencyContext(),
      ]);
      const [taskAgingResult, milestoneResult, workflowQueueResult] = await Promise.all([
        projectService.getTaskAgingReport(undefined, 5),
        projectService.getMilestoneTrackerReport(undefined, 5),
        projectService.getWorkflowApprovalQueueReport(5),
      ]);
      setProjects(projectResult.items);
      setTotalPages(projectResult.totalPages);
      setDashboard(dashboardResult);
      setTypes(typeResult);
      setPortfolios(portfolioResult);
      setBaseCurrency(currencyContext.baseCurrency);
      setTaskAging(taskAgingResult);
      setMilestones(milestoneResult);
      setWorkflowQueue(workflowQueueResult);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load projects');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [page, status, projectTypeId, portfolioId, programId]);

  useEffect(() => {
    const loadPrograms = async () => {
      try {
        setPrograms(await projectService.getPrograms(portfolioId === 'all' ? undefined : portfolioId));
      } catch (error: any) {
        toast.error(error.message || 'Failed to load programs');
      }
    };

    loadPrograms();
  }, [portfolioId]);

  const statusBadge = (value: string) => {
    const tone =
      value === 'Planned' || value === 'InProgress' ? 'default' :
      value === 'PendingApproval' ? 'secondary' :
      value === 'Completed' || value === 'Closed' ? 'outline' :
      value === 'Cancelled' ? 'destructive' :
      'outline';
    return <Badge variant={tone}>{value}</Badge>;
  };

  const formatMoney = (value: number, currency?: string | null) => formatProjectMoney(value, currency, baseCurrency.code);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Projects</h1>
          <p className="text-muted-foreground">Central register for project initiation, planning, and governance.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href="/development/project-approvals">Approvals</Link>
          </Button>
          <Button variant="outline" onClick={() => router.push('/administration/project-management')}>
            <Settings className="mr-2 h-4 w-4" />
            Setup
          </Button>
          <Button onClick={() => router.push('/development/projects/new')}>
            <Plus className="mr-2 h-4 w-4" />
            New Project
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Total Projects</CardDescription><CardTitle>{dashboard.totalProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Active</CardDescription><CardTitle>{dashboard.activeProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Approval</CardDescription><CardTitle>{dashboard.pendingApprovalProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Approval Queue</CardDescription><CardTitle>{workflowQueue.filter((item) => item.status === 'PendingApproval').length}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Milestones Due</CardTitle>
            <CardDescription>{dashboard.dueMilestonesThisMonth} due this month</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {milestones.length === 0 ? (
              <div className="text-sm text-muted-foreground">No milestones to track.</div>
            ) : (
              milestones.map((item) => (
                <div key={item.milestoneId} className="rounded-md border p-3">
                  <div className="font-medium">{item.milestoneTitle}</div>
                  <div className="text-sm text-muted-foreground">{item.projectCode} | {format(new Date(item.targetDate), 'MMM dd, yyyy')}</div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Task Aging</CardTitle>
            <CardDescription>Overdue task items</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {taskAging.length === 0 ? (
              <div className="text-sm text-muted-foreground">No overdue tasks.</div>
            ) : (
              taskAging.map((item) => (
                <div key={item.workItemId} className="rounded-md border p-3">
                  <div className="font-medium">{item.workItemTitle}</div>
                  <div className="text-sm text-muted-foreground">{item.projectCode} | {item.daysOverdue} days overdue</div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>At Risk Projects</CardTitle>
            <CardDescription>Projects requiring attention</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {dashboard.atRiskProjects?.length ? (
              dashboard.atRiskProjects.map((project) => (
                <div key={project.id} className="rounded-md border p-3">
                  <div className="font-medium">{project.projectCode}</div>
                  <div className="text-sm text-muted-foreground">{project.title}</div>
                </div>
              ))
            ) : (
              <div className="text-sm text-muted-foreground">No at-risk projects.</div>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Approval Queue</CardTitle>
            <CardDescription>Workflow items waiting for governance action</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {workflowQueue.length === 0 ? (
              <div className="text-sm text-muted-foreground">No approval items are pending.</div>
            ) : (
              workflowQueue.map((item) => (
                <div key={`${item.entityType}-${item.entityId}`} className="rounded-lg border p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">{item.itemTitle}</div>
                      <div className="text-sm text-muted-foreground">{item.projectCode} | {item.projectTitle}</div>
                    </div>
                    <Badge variant={item.status === 'PendingApproval' ? 'secondary' : 'outline'}>{item.queueStage}</Badge>
                  </div>
                  <div className="mt-2 text-sm text-muted-foreground">
                    {item.entityType.replace('Project', '').replace(/([A-Z])/g, ' $1').trim()} | {item.daysPending} day(s)
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><Filter className="h-4 w-4" />Filters</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-6">
          <div className="flex gap-2">
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by code or title" onKeyDown={(e) => e.key === 'Enter' && loadData()} />
            <Button variant="outline" size="icon" onClick={() => { setPage(1); loadData(); }}>
              <Search className="h-4 w-4" />
            </Button>
          </div>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger><SelectValue placeholder="All statuses" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {['Draft', 'PendingApproval', 'Planned', 'InProgress', 'OnHold', 'Completed', 'Closed', 'Cancelled', 'Archived'].map((item) => (
                <SelectItem key={item} value={item}>{item}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={projectTypeId} onValueChange={setProjectTypeId}>
            <SelectTrigger><SelectValue placeholder="All types" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All types</SelectItem>
              {types.map((item) => (
                <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={portfolioId} onValueChange={(value) => { setPortfolioId(value); setProgramId('all'); }}>
            <SelectTrigger><SelectValue placeholder="All portfolios" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All portfolios</SelectItem>
              {portfolios.map((item) => (
                <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={programId} onValueChange={setProgramId}>
            <SelectTrigger><SelectValue placeholder="All programs" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All programs</SelectItem>
              {programs.map((item) => (
                <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button variant="outline" onClick={loadData}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Project Register</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${projects.length} projects in current view`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading projects...</div>
          ) : projects.length === 0 ? (
            <div className="py-10 text-center text-muted-foreground">No projects found.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Project</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Budget</TableHead>
                  <TableHead>Progress</TableHead>
                  <TableHead>Dates</TableHead>
                  <TableHead>Workflow</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {projects.map((project) => (
                  <TableRow key={project.id}>
                    <TableCell>
                      <div className="font-medium">{project.projectCode}</div>
                      <div className="text-sm text-muted-foreground">{project.title}</div>
                      {(project.portfolioName || project.programName) && (
                        <div className="text-xs text-muted-foreground">{[project.portfolioName, project.programName].filter(Boolean).join(' / ')}</div>
                      )}
                    </TableCell>
                    <TableCell>{statusBadge(project.status)}</TableCell>
                    <TableCell>{project.projectTypeName || 'General'}</TableCell>
                    <TableCell>{project.estimatedBudget ? formatMoney(project.estimatedBudget) : 'N/A'}</TableCell>
                    <TableCell>{project.progressPercent}%</TableCell>
                    <TableCell>
                      <div className="text-sm">
                        {project.startDate ? format(new Date(project.startDate), 'MMM dd, yyyy') : 'No start'}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {project.targetEndDate ? `Target ${format(new Date(project.targetEndDate), 'MMM dd, yyyy')}` : 'No target end'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="text-sm text-muted-foreground">{project.currentWorkflowStepName || '-'}</div>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Button variant="ghost" size="sm" onClick={() => router.push(`/development/projects/${project.id}`)}>
                          <Eye className="h-4 w-4" />
                        </Button>
                        <WorkflowApprovalActions
                          entityType="Project"
                          entityId={project.id}
                          entityLabel="Project"
                          entityNumber={project.projectCode}
                          status={project.status}
                          currentStepName={project.currentWorkflowStepName}
                          canSubmit={project.status === 'Draft'}
                          canApproveReject={project.status === 'PendingApproval'}
                          onSubmit={() => projectService.submitProject(project.id)}
                          onApprove={(comments) => projectService.approveProject(project.id, comments)}
                          onReject={(comments) => projectService.rejectProject(project.id, comments || 'Rejected', comments)}
                          onAfterAction={async () => loadData()}
                          onOpenWorkflows={() => router.push('/administration/workflow')}
                          size="icon"
                          iconOnly
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}

          {totalPages > 1 && (
            <div className="mt-4 flex items-center justify-between">
              <div className="text-sm text-muted-foreground">Page {page} of {totalPages}</div>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page === 1}>Previous</Button>
                <Button variant="outline" size="sm" onClick={() => setPage((value) => Math.min(totalPages, value + 1))} disabled={page === totalPages}>Next</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
