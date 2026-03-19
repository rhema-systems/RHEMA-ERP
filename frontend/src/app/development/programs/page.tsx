'use client';

import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  CreateProjectInterdependencyDto,
  CreateProjectProgramDto,
  ProjectDependencyWatchReportItemDto,
  ProjectInterdependencyDto,
  ProjectLookupDto,
  ProjectPortfolioDto,
  ProjectProgramDto,
  ProjectProgramSummaryReportItemDto,
  projectService,
} from '@/services/projectService';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

const emptyForm: CreateProjectProgramDto = {
  code: '',
  name: '',
  description: '',
  status: 'Active',
};

const emptyDependencyForm: CreateProjectInterdependencyDto = {
  sourceProjectId: '',
  targetProjectId: '',
  dependencyType: 'Schedule',
  status: 'Open',
  impactLevel: 'Medium',
  title: '',
  description: '',
  mitigationPlan: '',
};

export default function ProjectProgramsPage() {
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [summary, setSummary] = useState<ProjectProgramSummaryReportItemDto[]>([]);
  const [projectOptions, setProjectOptions] = useState<ProjectLookupDto[]>([]);
  const [dependencyWatch, setDependencyWatch] = useState<ProjectDependencyWatchReportItemDto[]>([]);
  const [interdependencies, setInterdependencies] = useState<ProjectInterdependencyDto[]>([]);
  const [selectedPortfolioId, setSelectedPortfolioId] = useState<string>('all');
  const [form, setForm] = useState<CreateProjectProgramDto>(emptyForm);
  const [dependencyForm, setDependencyForm] = useState<CreateProjectInterdependencyDto>(emptyDependencyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [savingDependency, setSavingDependency] = useState(false);

  const load = async (portfolioId?: string) => {
    try {
      setLoading(true);
      const [portfolioResult, programResult, summaryResult, projectResult, dependencyResult, interdependencyResult] = await Promise.all([
        projectService.getPortfolios(),
        projectService.getPrograms(portfolioId),
        projectService.getProgramSummaryReport(portfolioId, 12),
        projectService.lookupProjects(undefined, undefined, undefined, portfolioId),
        projectService.getDependencyWatchReport(portfolioId, undefined, 12),
        projectService.getInterdependencies(undefined, portfolioId),
      ]);
      setPortfolios(portfolioResult);
      setPrograms(programResult);
      setSummary(summaryResult);
      setProjectOptions(projectResult);
      setDependencyWatch(dependencyResult);
      setInterdependencies(interdependencyResult);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load programs');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    load(selectedPortfolioId === 'all' ? undefined : selectedPortfolioId);
  }, [selectedPortfolioId]);

  const save = async () => {
    try {
      setSaving(true);
      if (editingId) {
        await projectService.updateProgram(editingId, form);
      } else {
        await projectService.createProgram(form);
      }
      setEditingId(null);
      setForm(emptyForm);
      await load(selectedPortfolioId === 'all' ? undefined : selectedPortfolioId);
      toast.success('Program saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save program');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id: string) => {
    if (!window.confirm('Delete this program?')) {
      return;
    }

    try {
      await projectService.deleteProgram(id);
      if (editingId === id) {
        setEditingId(null);
        setForm(emptyForm);
      }
      await load(selectedPortfolioId === 'all' ? undefined : selectedPortfolioId);
      toast.success('Program deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete program');
    }
  };

  const saveDependency = async () => {
    try {
      setSavingDependency(true);
      await projectService.createInterdependency(dependencyForm);
      setDependencyForm(emptyDependencyForm);
      await load(selectedPortfolioId === 'all' ? undefined : selectedPortfolioId);
      toast.success('Dependency saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save dependency');
    } finally {
      setSavingDependency(false);
    }
  };

  const removeDependency = async (id: string) => {
    if (!window.confirm('Delete this dependency?')) {
      return;
    }

    try {
      await projectService.deleteInterdependency(id);
      await load(selectedPortfolioId === 'all' ? undefined : selectedPortfolioId);
      toast.success('Dependency deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete dependency');
    }
  };

  const totalProjects = summary.reduce((sum, item) => sum + item.projectCount, 0);
  const totalBudget = summary.reduce((sum, item) => sum + item.totalEstimatedBudget, 0);
  const totalActualCost = summary.reduce((sum, item) => sum + item.totalActualCost, 0);
  const averageProgress = summary.length > 0
    ? summary.reduce((sum, item) => sum + item.averageProgressPercent, 0) / summary.length
    : 0;
  const activeDependencies = dependencyWatch.filter((item) => item.coordinationState !== 'Resolved').length;
  const getProjectLabel = (projectId?: string) => {
    const match = projectOptions.find((item) => item.id === projectId);
    return match ? `${match.projectCode} | ${match.title}` : projectId || 'Unknown project';
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Programs</h1>
          <p className="text-muted-foreground">Manage program structures and grouped delivery status.</p>
        </div>
        <div className="w-full max-w-xs">
          <Select value={selectedPortfolioId} onValueChange={setSelectedPortfolioId}>
            <SelectTrigger><SelectValue placeholder="Filter by portfolio" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All portfolios</SelectItem>
              {portfolios.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
        <Card><CardHeader className="pb-2"><CardDescription>Programs</CardDescription><CardTitle>{programs.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Projects in Programs</CardDescription><CardTitle>{totalProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Total Program Budget</CardDescription><CardTitle>{totalBudget.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Total Actual Cost</CardDescription><CardTitle>{totalActualCost.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Average Progress</CardDescription><CardTitle>{averageProgress.toFixed(2)}%</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Dependency Watch</CardDescription><CardTitle>{activeDependencies}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Program Summary</CardTitle>
            <CardDescription>Delivery and budget totals by program.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {summary.map((item) => (
              <div key={item.programId} className="rounded-lg border p-4">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.programName}</div>
                    <div className="text-sm text-muted-foreground">{item.portfolioName || 'No portfolio'} | {item.programCode}</div>
                  </div>
                  <Badge variant="outline">{item.projectCount} projects</Badge>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                  <div>Active: {item.activeProjectCount}</div>
                  <div>Budget: {item.totalEstimatedBudget.toLocaleString()}</div>
                  <div>Actual: {item.totalActualCost.toLocaleString()}</div>
                  <div>Avg progress: {item.averageProgressPercent.toFixed(2)}%</div>
                </div>
              </div>
            ))}
            {!summary.length && !loading && <div className="text-sm text-muted-foreground">No program data available.</div>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{editingId ? 'Edit Program' : 'New Program'}</CardTitle>
            <CardDescription>Maintain program records.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-2">
              <Label>Portfolio</Label>
              <Select value={form.portfolioId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, portfolioId: value === 'none' ? undefined : value }))}>
                <SelectTrigger><SelectValue placeholder="No portfolio" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No portfolio</SelectItem>
                  {portfolios.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Code</Label>
              <Input value={form.code} onChange={(e) => setForm((prev) => ({ ...prev, code: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))} />
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
              <Button onClick={save} disabled={saving || !form.code || !form.name}>{saving ? 'Saving...' : 'Save Program'}</Button>
              {editingId && <Button variant="outline" onClick={() => { setEditingId(null); setForm(emptyForm); }}>Cancel</Button>}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
        <Card>
          <CardHeader>
            <CardTitle>Dependency Watch</CardTitle>
            <CardDescription>Inter-project dependencies that need program-level coordination.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {dependencyWatch.map((item) => (
              <div key={item.interdependencyId} className="rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {/* NOTE: The '>' in '->' must be escaped in JSX as {'->'}  */}
                      {item.sourceProjectCode} {'->'}  {item.targetProjectCode}
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
            {!dependencyWatch.length && !loading ? <div className="text-sm text-muted-foreground">No program dependencies recorded.</div> : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Log Dependency</CardTitle>
            <CardDescription>Register cross-project dependencies for program oversight.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Source Project</Label>
                <Select value={dependencyForm.sourceProjectId || 'none'} onValueChange={(value) => setDependencyForm((prev) => ({ ...prev, sourceProjectId: value === 'none' ? '' : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Select project</SelectItem>
                    {projectOptions.map((item) => <SelectItem key={`source-${item.id}`} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Target Project</Label>
                <Select value={dependencyForm.targetProjectId || 'none'} onValueChange={(value) => setDependencyForm((prev) => ({ ...prev, targetProjectId: value === 'none' ? '' : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Select project</SelectItem>
                    {projectOptions.filter((item) => item.id !== dependencyForm.sourceProjectId).map((item) => <SelectItem key={`target-${item.id}`} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Dependency Type</Label>
                <Select value={dependencyForm.dependencyType} onValueChange={(value) => setDependencyForm((prev) => ({ ...prev, dependencyType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Schedule">Schedule</SelectItem>
                    <SelectItem value="Resource">Resource</SelectItem>
                    <SelectItem value="Commercial">Commercial</SelectItem>
                    <SelectItem value="Technical">Technical</SelectItem>
                    <SelectItem value="Procurement">Procurement</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Impact Level</Label>
                <Select value={dependencyForm.impactLevel} onValueChange={(value) => setDependencyForm((prev) => ({ ...prev, impactLevel: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Low">Low</SelectItem>
                    <SelectItem value="Medium">Medium</SelectItem>
                    <SelectItem value="High">High</SelectItem>
                    <SelectItem value="Critical">Critical</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={dependencyForm.status} onValueChange={(value) => setDependencyForm((prev) => ({ ...prev, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Open">Open</SelectItem>
                    <SelectItem value="Monitoring">Monitoring</SelectItem>
                    <SelectItem value="Blocked">Blocked</SelectItem>
                    <SelectItem value="Resolved">Resolved</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Due Date</Label>
                <Input type="date" value={dependencyForm.dueDate || ''} onChange={(e) => setDependencyForm((prev) => ({ ...prev, dueDate: e.target.value || undefined }))} />
              </div>
            </div>
            <div className="grid gap-2">
              <Label>Title</Label>
              <Input value={dependencyForm.title} onChange={(e) => setDependencyForm((prev) => ({ ...prev, title: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Description</Label>
              <Textarea rows={3} value={dependencyForm.description || ''} onChange={(e) => setDependencyForm((prev) => ({ ...prev, description: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Mitigation Plan</Label>
              <Textarea rows={3} value={dependencyForm.mitigationPlan || ''} onChange={(e) => setDependencyForm((prev) => ({ ...prev, mitigationPlan: e.target.value }))} />
            </div>
            <Button onClick={saveDependency} disabled={savingDependency || !dependencyForm.sourceProjectId || !dependencyForm.targetProjectId || !dependencyForm.title.trim()}>
              {savingDependency ? 'Saving...' : 'Save Dependency'}
            </Button>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Dependency Register</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${interdependencies.length} logged dependencies`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {interdependencies.map((item) => (
            <div key={item.id} className="rounded-lg border p-4">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-semibold">{item.title}</span>
                    <Badge variant="outline">{item.dependencyType}</Badge>
                    <Badge>{item.status}</Badge>
                  </div>
                  {/* NOTE: The '>' in '->' must be escaped in JSX as {'->'}  */}
                  <div className="mt-1 text-sm text-muted-foreground">{getProjectLabel(item.sourceProjectId)} {'->'}  {getProjectLabel(item.targetProjectId)}</div>
                  <div className="mt-2 text-sm text-muted-foreground">
                    Impact {item.impactLevel}{item.dueDate ? ` | Due ${new Date(item.dueDate).toLocaleDateString()}` : ''}{item.description ? ` | ${item.description}` : ''}
                  </div>
                </div>
                <Button variant="ghost" size="icon" onClick={() => removeDependency(item.id)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
          {!interdependencies.length && !loading ? <div className="text-sm text-muted-foreground">No dependencies logged yet.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Program Register</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${programs.length} programs`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {programs.map((item) => (
            <div key={item.id} className="rounded-lg border p-4">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-semibold">{item.name}</span>
                    <Badge variant="outline">{item.code}</Badge>
                    <Badge>{item.status}</Badge>
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">{item.portfolioName || 'No portfolio'}</div>
                  <div className="mt-2 text-sm text-muted-foreground">
                    {item.projectCount} projects | Active {item.activeProjectCount} | Actual cost {item.totalActualCost.toLocaleString()} | Budget cap {(item.budgetCap ?? 0).toLocaleString()}
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
                        portfolioId: item.portfolioId,
                        status: item.status,
                        programManagerId: item.programManagerId,
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
          {!programs.length && !loading && <div className="text-sm text-muted-foreground">No programs created yet.</div>}
        </CardContent>
      </Card>
    </div>
  );
}
