'use client';

import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  CreateProjectInterdependencyDto,
  ProjectDependencyWatchReportItemDto,
  ProjectInterdependencyDto,
  ProjectLookupDto,
  ProjectPortfolioDto,
  ProjectProgramDto,
  ProjectResourceLookupDto,
  projectService,
} from '@/services/projectService';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

const emptyForm: CreateProjectInterdependencyDto = {
  sourceProjectId: '',
  targetProjectId: '',
  dependencyType: 'Schedule',
  status: 'Open',
  impactLevel: 'Medium',
  title: '',
  description: '',
  mitigationPlan: '',
};

export default function ProjectDependenciesPage() {
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [resources, setResources] = useState<ProjectResourceLookupDto[]>([]);
  const [watchItems, setWatchItems] = useState<ProjectDependencyWatchReportItemDto[]>([]);
  const [dependencies, setDependencies] = useState<ProjectInterdependencyDto[]>([]);
  const [selectedPortfolioId, setSelectedPortfolioId] = useState('all');
  const [selectedProgramId, setSelectedProgramId] = useState('all');
  const [selectedProjectId, setSelectedProjectId] = useState('all');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<CreateProjectInterdependencyDto>(emptyForm);

  const activePortfolioId = selectedPortfolioId === 'all' ? undefined : selectedPortfolioId;
  const activeProgramId = selectedProgramId === 'all' ? undefined : selectedProgramId;
  const activeProjectId = selectedProjectId === 'all' ? undefined : selectedProjectId;

  const loadLookups = async (portfolioId?: string, programId?: string) => {
    const [portfolioResult, programResult, projectResult, resourceResult] = await Promise.all([
      projectService.getPortfolios(),
      projectService.getPrograms(portfolioId),
      projectService.lookupProjects(undefined, undefined, undefined, portfolioId, programId),
      projectService.lookupResources(undefined, 100),
    ]);

    setPortfolios(portfolioResult);
    setPrograms(programResult);
    setProjects(projectResult);
    setResources(resourceResult);
  };

  const loadData = async () => {
    try {
      setLoading(true);
      await loadLookups(activePortfolioId, activeProgramId);
      const [watchResult, dependencyResult] = await Promise.all([
        projectService.getDependencyWatchReport(activePortfolioId, activeProgramId, 50),
        projectService.getInterdependencies(activeProjectId, activePortfolioId, activeProgramId),
      ]);

      setWatchItems(watchResult);
      setDependencies(dependencyResult);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project dependencies');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [selectedPortfolioId, selectedProgramId, selectedProjectId]);

  useEffect(() => {
    if (selectedProgramId !== 'all' && !programs.some((item) => item.id === selectedProgramId)) {
      setSelectedProgramId('all');
    }
  }, [programs, selectedProgramId]);

  useEffect(() => {
    if (selectedProjectId !== 'all' && !projects.some((item) => item.id === selectedProjectId)) {
      setSelectedProjectId('all');
    }
  }, [projects, selectedProjectId]);

  const filteredProjects = useMemo(
    () => projects.filter((item) => item.id !== form.sourceProjectId),
    [projects, form.sourceProjectId],
  );

  const saveDependency = async () => {
    try {
      setSaving(true);
      await projectService.createInterdependency(form);
      setForm(emptyForm);
      await loadData();
      toast.success('Dependency saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save dependency');
    } finally {
      setSaving(false);
    }
  };

  const deleteDependency = async (id: string) => {
    if (!window.confirm('Delete this dependency?')) {
      return;
    }

    try {
      await projectService.deleteInterdependency(id);
      await loadData();
      toast.success('Dependency deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete dependency');
    }
  };

  const alertCount = watchItems.filter((item) => item.coordinationState !== 'Resolved').length;
  const overdueCount = watchItems.filter((item) => item.coordinationState === 'Overdue').length;
  const criticalCount = watchItems.filter((item) => item.impactLevel === 'Critical').length;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-3xl font-bold tracking-tight">Project Dependencies</h1>
          <p className="text-muted-foreground">Track cross-project coordination items across portfolios and programs.</p>
        </div>
        <div className="grid w-full gap-3 md:max-w-4xl md:grid-cols-3">
          <Select value={selectedPortfolioId} onValueChange={setSelectedPortfolioId}>
            <SelectTrigger><SelectValue placeholder="All portfolios" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All portfolios</SelectItem>
              {portfolios.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={selectedProgramId} onValueChange={setSelectedProgramId}>
            <SelectTrigger><SelectValue placeholder="All programs" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All programs</SelectItem>
              {programs.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={selectedProjectId} onValueChange={setSelectedProjectId}>
            <SelectTrigger><SelectValue placeholder="All projects" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All projects</SelectItem>
              {projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Logged Dependencies</CardDescription><CardTitle>{dependencies.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Watch Items</CardDescription><CardTitle>{alertCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Overdue</CardDescription><CardTitle>{overdueCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Critical Impact</CardDescription><CardTitle>{criticalCount}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
        <Card>
          <CardHeader>
            <CardTitle>Dependency Watch</CardTitle>
            <CardDescription>Items that need immediate coordination across projects.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading dependency watch...</div> : null}
            {!loading && watchItems.length === 0 ? <div className="text-sm text-muted-foreground">No dependency watch items are open.</div> : null}
            {watchItems.map((item) => (
              <div key={item.interdependencyId} className="rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.sourceProjectCode} | {item.targetProjectCode}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Badge variant={item.coordinationState === 'Overdue' ? 'destructive' : item.coordinationState === 'Watch' ? 'secondary' : 'outline'}>
                      {item.coordinationState}
                    </Badge>
                    <Badge variant="outline">{item.impactLevel}</Badge>
                  </div>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                  <div>Type: {item.dependencyType}</div>
                  <div>Status: {item.status}</div>
                  <div>Portfolio: {item.portfolioName || 'None'}</div>
                  <div>Due: {item.dueDate ? new Date(item.dueDate).toLocaleDateString() : 'Open'}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Log Dependency</CardTitle>
            <CardDescription>Register a dependency for portfolio and program oversight.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Source Project</Label>
                <Select value={form.sourceProjectId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, sourceProjectId: value === 'none' ? '' : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Select project</SelectItem>
                    {projects.map((item) => <SelectItem key={`source-${item.id}`} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Target Project</Label>
                <Select value={form.targetProjectId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, targetProjectId: value === 'none' ? '' : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Select project</SelectItem>
                    {filteredProjects.map((item) => <SelectItem key={`target-${item.id}`} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Dependency Type</Label>
                <Select value={form.dependencyType} onValueChange={(value) => setForm((prev) => ({ ...prev, dependencyType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Schedule">Schedule</SelectItem>
                    <SelectItem value="Resource">Resource</SelectItem>
                    <SelectItem value="Commercial">Commercial</SelectItem>
                    <SelectItem value="Technical">Technical</SelectItem>
                    <SelectItem value="Procurement">Procurement</SelectItem>
                    <SelectItem value="Reporting">Reporting</SelectItem>
                    <SelectItem value="LessonsLearned">Lessons Learned</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Impact Level</Label>
                <Select value={form.impactLevel} onValueChange={(value) => setForm((prev) => ({ ...prev, impactLevel: value }))}>
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
                <Select value={form.status} onValueChange={(value) => setForm((prev) => ({ ...prev, status: value }))}>
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
                <Label>Owner</Label>
                <Select value={form.ownerId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, ownerId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No owner</SelectItem>
                    {resources.map((item) => <SelectItem key={item.id} value={item.id}>{item.displayName}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2 md:col-span-2">
                <Label>Due Date</Label>
                <Input type="date" value={form.dueDate || ''} onChange={(e) => setForm((prev) => ({ ...prev, dueDate: e.target.value || undefined }))} />
              </div>
            </div>
            <div className="grid gap-2">
              <Label>Title</Label>
              <Input value={form.title} onChange={(e) => setForm((prev) => ({ ...prev, title: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Description</Label>
              <Textarea rows={3} value={form.description || ''} onChange={(e) => setForm((prev) => ({ ...prev, description: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Mitigation Plan</Label>
              <Textarea rows={3} value={form.mitigationPlan || ''} onChange={(e) => setForm((prev) => ({ ...prev, mitigationPlan: e.target.value }))} />
            </div>
            <Button onClick={saveDependency} disabled={saving || !form.sourceProjectId || !form.targetProjectId || !form.title.trim()}>
              {saving ? 'Saving...' : 'Save Dependency'}
            </Button>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Dependency Register</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${dependencies.length} dependency records`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {dependencies.map((item) => (
            <div key={item.id} className="rounded-lg border p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold">{item.title}</span>
                    <Badge variant="outline">{item.dependencyType}</Badge>
                    <Badge>{item.status}</Badge>
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {item.sourceProjectCode} | {item.sourceProjectTitle}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {item.targetProjectCode} | {item.targetProjectTitle}
                  </div>
                  <div className="mt-2 text-sm text-muted-foreground">
                    Impact {item.impactLevel}
                    {item.dueDate ? ` | Due ${new Date(item.dueDate).toLocaleDateString()}` : ''}
                    {item.description ? ` | ${item.description}` : ''}
                  </div>
                </div>
                <Button variant="ghost" size="icon" onClick={() => deleteDependency(item.id)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
          {!dependencies.length && !loading ? <div className="text-sm text-muted-foreground">No dependencies have been logged for the current filter.</div> : null}
        </CardContent>
      </Card>
    </div>
  );
}
