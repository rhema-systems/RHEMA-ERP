'use client';

import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  ProjectDependencyWatchReportItemDto,
  CreateProjectPortfolioDto,
  ProjectPortfolioDto,
  ProjectPortfolioPrioritizationReportItemDto,
  ProjectStrategicInitiativeReportItemDto,
  ProjectPortfolioSummaryReportItemDto,
  projectService,
} from '@/services/projectService';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

const emptyForm: CreateProjectPortfolioDto = {
  code: '',
  name: '',
  description: '',
  status: 'Active',
  strategicObjective: '',
};

export default function ProjectPortfoliosPage() {
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [summary, setSummary] = useState<ProjectPortfolioSummaryReportItemDto[]>([]);
  const [prioritization, setPrioritization] = useState<ProjectPortfolioPrioritizationReportItemDto[]>([]);
  const [strategicInitiatives, setStrategicInitiatives] = useState<ProjectStrategicInitiativeReportItemDto[]>([]);
  const [dependencyWatch, setDependencyWatch] = useState<ProjectDependencyWatchReportItemDto[]>([]);
  const [form, setForm] = useState<CreateProjectPortfolioDto>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    try {
      setLoading(true);
      const [portfolioResult, summaryResult, prioritizationResult, strategicResult, dependencyResult] = await Promise.all([
        projectService.getPortfolios(),
        projectService.getPortfolioSummaryReport(8),
        projectService.getPortfolioPrioritizationReport(undefined, 20),
        projectService.getStrategicInitiativeReport(undefined, 8),
        projectService.getDependencyWatchReport(undefined, undefined, 8),
      ]);
      setPortfolios(portfolioResult);
      setSummary(summaryResult);
      setPrioritization(prioritizationResult);
      setStrategicInitiatives(strategicResult);
      setDependencyWatch(dependencyResult);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load portfolios');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const save = async () => {
    try {
      setSaving(true);
      if (editingId) {
        await projectService.updatePortfolio(editingId, form);
      } else {
        await projectService.createPortfolio(form);
      }
      setEditingId(null);
      setForm(emptyForm);
      await load();
      toast.success('Portfolio saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save portfolio');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id: string) => {
    if (!window.confirm('Delete this portfolio?')) {
      return;
    }

    try {
      await projectService.deletePortfolio(id);
      if (editingId === id) {
        setEditingId(null);
        setForm(emptyForm);
      }
      await load();
      toast.success('Portfolio deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete portfolio');
    }
  };

  const totalProjects = summary.reduce((sum, item) => sum + item.projectCount, 0);
  const totalBudget = summary.reduce((sum, item) => sum + item.totalEstimatedBudget, 0);
  const totalActualCost = summary.reduce((sum, item) => sum + item.totalActualCost, 0);
  const highRiskProjects = summary.reduce((sum, item) => sum + item.highRiskProjectCount, 0);
  const dependencyAlerts = dependencyWatch.filter((item) => item.coordinationState !== 'Resolved').length;
  const topPortfolios = [...summary].sort((a, b) => b.totalEstimatedBudget - a.totalEstimatedBudget).slice(0, 3);
  const prioritizationBoard = [...prioritization].slice(0, 6);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Portfolios</h1>
        <p className="text-muted-foreground">Manage portfolio structures, budget totals, and delivery status.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
        <Card><CardHeader className="pb-2"><CardDescription>Portfolios</CardDescription><CardTitle>{portfolios.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Projects in Portfolios</CardDescription><CardTitle>{totalProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Total Portfolio Budget</CardDescription><CardTitle>{totalBudget.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Total Actual Cost</CardDescription><CardTitle>{totalActualCost.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>High-Risk Projects</CardDescription><CardTitle>{highRiskProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Dependency Watch</CardDescription><CardTitle>{dependencyAlerts}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Portfolio Summary</CardTitle>
            <CardDescription>Budget, risk, and delivery totals by portfolio.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {topPortfolios.length > 0 ? (
              <div className="grid gap-3 md:grid-cols-3">
                {topPortfolios.map((item) => (
                  <div key={`highlight-${item.portfolioId}`} className="rounded-lg border bg-slate-50 p-4 dark:bg-slate-900/40">
                    <div className="font-semibold">{item.portfolioName}</div>
                    <div className="mt-1 text-sm text-muted-foreground">{item.portfolioCode}</div>
                    <div className="mt-3 text-2xl font-semibold">{item.totalEstimatedBudget.toLocaleString()}</div>
                    <div className="mt-1 text-sm text-muted-foreground">Estimated budget</div>
                  </div>
                ))}
              </div>
            ) : null}
            {summary.map((item) => (
              <div key={item.portfolioId} className="rounded-lg border p-4">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.portfolioName}</div>
                    <div className="text-sm text-muted-foreground">{item.portfolioCode}</div>
                  </div>
                  <Badge variant="outline">{item.projectCount} projects</Badge>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                  <div>Programs: {item.programCount}</div>
                  <div>Active: {item.activeProjectCount}</div>
                  <div>Budget: {item.totalEstimatedBudget.toLocaleString()}</div>
                  <div>Actual: {item.totalActualCost.toLocaleString()}</div>
                  <div>High risk: {item.highRiskProjectCount}</div>
                </div>
              </div>
            ))}
            {!summary.length && !loading && <div className="text-sm text-muted-foreground">No portfolio data available.</div>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{editingId ? 'Edit Portfolio' : 'New Portfolio'}</CardTitle>
            <CardDescription>Maintain portfolio records.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-2">
              <Label>Code</Label>
              <Input value={form.code} onChange={(e) => setForm((prev) => ({ ...prev, code: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Strategic Objective</Label>
              <Input value={form.strategicObjective || ''} onChange={(e) => setForm((prev) => ({ ...prev, strategicObjective: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Description</Label>
              <Textarea rows={4} value={form.description || ''} onChange={(e) => setForm((prev) => ({ ...prev, description: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Budget Cap</Label>
              <Input type="number" value={form.budgetCap ?? ''} onChange={(e) => setForm((prev) => ({ ...prev, budgetCap: e.target.value ? Number(e.target.value) : undefined }))} />
            </div>
            <div className="flex gap-2">
              <Button onClick={save} disabled={saving || !form.code || !form.name}>{saving ? 'Saving...' : 'Save Portfolio'}</Button>
              {editingId && <Button variant="outline" onClick={() => { setEditingId(null); setForm(emptyForm); }}>Cancel</Button>}
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Prioritization Board</CardTitle>
          <CardDescription>Projects requiring the most portfolio attention across the current delivery landscape.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {prioritizationBoard.map((item) => (
            <div key={item.projectId} className="rounded-lg border p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="font-semibold">{item.projectTitle}</div>
                  <div className="text-sm text-muted-foreground">{item.projectCode} | {item.portfolioName || 'No portfolio'}{item.programName ? ` | ${item.programName}` : ''}</div>
                </div>
                <div className="flex gap-2">
                  <Badge variant={item.priorityBand === 'Stabilize' ? 'destructive' : item.priorityBand === 'Focus' ? 'secondary' : 'outline'}>{item.priorityBand}</Badge>
                  <Badge variant="outline">Score {item.priorityScore}</Badge>
                </div>
              </div>
              <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                <div>Status: {item.status}</div>
                <div>Health: {item.healthStatus}</div>
                <div>Open risks/issues: {item.openRiskCount}/{item.openIssueCount}</div>
                <div>Overdue milestones: {item.overdueMilestoneCount}</div>
              </div>
            </div>
          ))}
          {!prioritizationBoard.length && !loading ? <div className="text-sm text-muted-foreground">No prioritization data available.</div> : null}
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
        <Card>
          <CardHeader>
            <CardTitle>Strategic Initiatives</CardTitle>
            <CardDescription>Delivery performance grouped by strategic alignment.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {strategicInitiatives.map((item) => (
              <div key={item.initiative} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.initiative}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.portfolioNames.join(', ') || 'No portfolio'}{item.programNames.length ? ` | ${item.programNames.join(', ')}` : ''}
                    </div>
                  </div>
                  <Badge variant="outline">{item.projectCount} projects</Badge>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                  <div>Active: {item.activeProjectCount}</div>
                  <div>At risk: {item.atRiskProjectCount}</div>
                  <div>Budget: {item.totalEstimatedBudget.toLocaleString()}</div>
                  <div>Progress: {item.averageProgressPercent.toFixed(2)}%</div>
                </div>
              </div>
            ))}
            {!strategicInitiatives.length && !loading ? <div className="text-sm text-muted-foreground">No strategic initiatives identified yet.</div> : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Dependency Watch</CardTitle>
            <CardDescription>Cross-project coordination items impacting portfolio delivery.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {dependencyWatch.map((item) => (
              <div key={item.interdependencyId} className="rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.sourceProjectCode} -> {item.targetProjectCode}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Badge variant={item.coordinationState === 'Overdue' ? 'destructive' : item.coordinationState === 'Watch' ? 'secondary' : 'outline'}>
                      {item.coordinationState}
                    </Badge>
                    <Badge variant="outline">{item.impactLevel}</Badge>
                  </div>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-3">
                  <div>Type: {item.dependencyType}</div>
                  <div>Status: {item.status}</div>
                  <div>Due: {item.dueDate ? new Date(item.dueDate).toLocaleDateString() : 'Open'}</div>
                </div>
              </div>
            ))}
            {!dependencyWatch.length && !loading ? <div className="text-sm text-muted-foreground">No cross-project dependencies recorded.</div> : null}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Portfolio Register</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${portfolios.length} portfolios`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {portfolios.map((item) => (
            <div key={item.id} className="rounded-lg border p-4">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-semibold">{item.name}</span>
                    <Badge variant="outline">{item.code}</Badge>
                    <Badge>{item.status}</Badge>
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">{item.description || 'No description'}</div>
                  <div className="mt-2 text-sm text-muted-foreground">
                    {item.programCount} programs | {item.projectCount} projects | Actual cost {item.totalActualCost.toLocaleString()} | Budget cap {(item.budgetCap ?? 0).toLocaleString()}
                  </div>
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setEditingId(item.id);
                      setForm({
                        code: item.code,
                        name: item.name,
                        description: item.description,
                        status: item.status,
                        strategicObjective: item.strategicObjective,
                        ownerId: item.ownerId,
                        sponsorId: item.sponsorId,
                        startDate: item.startDate,
                        targetEndDate: item.targetEndDate,
                        budgetCap: item.budgetCap,
                      });
                    }}
                  >
                    Edit
                  </Button>
                  <Button variant="ghost" size="icon" onClick={() => remove(item.id)}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </div>
          ))}
          {!portfolios.length && !loading && <div className="text-sm text-muted-foreground">No portfolios created yet.</div>}
        </CardContent>
      </Card>
    </div>
  );
}
