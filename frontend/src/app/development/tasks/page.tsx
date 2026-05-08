'use client';

import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ProjectDetailDto, ProjectLookupDto, ProjectScheduleAnalysisDto, ProjectTaskDependencyDto, ProjectWorkItemDto, projectService } from '@/services/projectService';
import { toast } from 'sonner';

type TaskDraft = {
  status: string;
  priority: string;
  assignedToUserId: string;
  percentComplete: number;
  plannedStartDate: string;
  plannedEndDate: string;
  scheduleChangeReason: string;
};

const quickTaskInit = {
  title: '',
  assignedToUserId: '',
  plannedStartDate: '',
  plannedEndDate: '',
};

const boardStatuses = ['New', 'Assigned', 'In Progress', 'Pending Review', 'Blocked', 'Completed'];

const flatten = (items: ProjectWorkItemDto[], depth = 0): Array<ProjectWorkItemDto & { depth: number }> =>
  items.flatMap((item) => [{ ...item, depth }, ...flatten(item.children || [], depth + 1)]);

const toDateInput = (value?: string) => (value ? String(value).slice(0, 10) : '');

export default function DevelopmentTasksPage() {
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('all');
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [analysis, setAnalysis] = useState<ProjectScheduleAnalysisDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState<'list' | 'kanban'>('kanban');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [search, setSearch] = useState('');
  const [drafts, setDrafts] = useState<Record<string, TaskDraft>>({});
  const [quickTask, setQuickTask] = useState(quickTaskInit);

  const load = async (projectId?: string) => {
    try {
      setLoading(true);
      const projectItems = await projectService.lookupProjects();
      setProjects(projectItems);

      const resolvedProjectId = projectId && projectId !== 'all'
        ? projectId
        : selectedProjectId !== 'all'
          ? selectedProjectId
          : projectItems[0]?.id;

      if (!resolvedProjectId) {
        setSelectedProjectId('all');
        setProject(null);
        setAnalysis(null);
        setDrafts({});
        return;
      }

      const [detail, schedule] = await Promise.all([
        projectService.getProjectById(resolvedProjectId),
        projectService.analyzeSchedule(resolvedProjectId),
      ]);

      setSelectedProjectId(resolvedProjectId);
      setProject(detail);
      setAnalysis(schedule);
      setDrafts(Object.fromEntries(
        flatten(detail.workItems)
          .filter((item) => item.nodeType === 'Task' || item.nodeType === 'Subtask')
          .map((item) => [item.id, {
            status: item.status,
            priority: item.priority || 'Normal',
            assignedToUserId: item.assignedToUserId || '',
            percentComplete: Number(item.percentComplete || 0),
            plannedStartDate: toDateInput(item.plannedStartDate),
            plannedEndDate: toDateInput(item.plannedEndDate),
            scheduleChangeReason: '',
          }]),
      ));
    } catch (error: any) {
      toast.error(error.message || 'Failed to load task board');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const tasks = useMemo(() => {
    const items = flatten(project?.workItems || [])
      .filter((item) => item.nodeType === 'Task' || item.nodeType === 'Subtask');
    return items.filter((item) => {
      if (statusFilter !== 'all' && item.status !== statusFilter) return false;
      if (!search.trim()) return true;
      const query = search.trim().toLowerCase();
      return item.title.toLowerCase().includes(query) || (item.description || '').toLowerCase().includes(query);
    });
  }, [project?.workItems, search, statusFilter]);

  const dependencyBySuccessor = useMemo(() => {
    const map = new Map<string, ProjectTaskDependencyDto[]>();
    for (const dependency of project?.taskDependencies || []) {
      const current = map.get(dependency.successorWorkItemId) || [];
      current.push(dependency);
      map.set(dependency.successorWorkItemId, current);
    }
    return map;
  }, [project?.taskDependencies]);

  const updateDraft = (workItemId: string, patch: Partial<TaskDraft>) => {
    setDrafts((current) => ({
      ...current,
      [workItemId]: {
        ...current[workItemId],
        ...patch,
      },
    }));
  };

  const saveTask = async (item: ProjectWorkItemDto) => {
    const draft = drafts[item.id];
    if (!draft) return;

    try {
      await projectService.updateWorkItem(item.id, {
        parentId: item.parentId,
        nodeType: item.nodeType,
        title: item.title,
        description: item.description,
        status: draft.status,
        priority: draft.priority,
        assignedToUserId: draft.assignedToUserId || undefined,
        plannedStartDate: draft.plannedStartDate || undefined,
        plannedEndDate: draft.plannedEndDate || undefined,
        actualStartDate: item.actualStartDate,
        actualEndDate: draft.percentComplete >= 100 ? item.actualEndDate || new Date().toISOString() : item.actualEndDate,
        percentComplete: draft.percentComplete,
        isRollupEnabled: item.isRollupEnabled,
        effortEstimateHours: item.effortEstimateHours,
        actualEffortHours: item.actualEffortHours,
        scheduleChangeReason: draft.scheduleChangeReason || undefined,
      });
      await load(project?.id);
      toast.success('Task updated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to update task');
    }
  };

  const addQuickTask = async () => {
    if (!project || !quickTask.title.trim()) return;

    try {
      await projectService.addWorkItem(project.id, {
        nodeType: 'Task',
        title: quickTask.title.trim(),
        status: 'New',
        priority: 'Normal',
        assignedToUserId: quickTask.assignedToUserId || undefined,
        plannedStartDate: quickTask.plannedStartDate || undefined,
        plannedEndDate: quickTask.plannedEndDate || undefined,
        percentComplete: 0,
        isRollupEnabled: true,
      });
      setQuickTask(quickTaskInit);
      await load(project.id);
      toast.success('Task added');
    } catch (error: any) {
      toast.error(error.message || 'Failed to add task');
    }
  };

  const renderTaskCard = (item: ProjectWorkItemDto & { depth: number }) => {
    const draft = drafts[item.id];
    const dependencies = dependencyBySuccessor.get(item.id) || [];
    const hasViolation = analysis?.violations.some((violation) => violation.workItemId === item.id);

    return (
      <div key={item.id} className="rounded-lg border bg-background p-4 shadow-sm">
        <div className="flex items-start justify-between gap-3">
          <div>
            <div className="font-semibold" style={{ paddingLeft: item.depth * 10 }}>{item.title}</div>
            <div className="text-sm text-muted-foreground">
              {item.nodeType} | owner {draft?.assignedToUserId || 'unassigned'} | {dependencies.length} predecessor{dependencies.length === 1 ? '' : 's'}
            </div>
          </div>
          <div className="flex gap-2">
            <Badge variant={hasViolation ? 'destructive' : 'outline'}>{draft?.status || item.status}</Badge>
            <Badge variant="outline">{draft?.percentComplete ?? item.percentComplete}%</Badge>
          </div>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-3">
          <div className="grid gap-2">
            <Label>Status</Label>
            <Select value={draft?.status || item.status} onValueChange={(value) => updateDraft(item.id, { status: value })}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{boardStatuses.map((status) => <SelectItem key={status} value={status}>{status}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Priority</Label>
            <Select value={draft?.priority || item.priority || 'Normal'} onValueChange={(value) => updateDraft(item.id, { priority: value })}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{['Low', 'Normal', 'High', 'Critical'].map((priority) => <SelectItem key={priority} value={priority}>{priority}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Progress %</Label>
            <Input type="number" min={0} max={100} value={draft?.percentComplete ?? item.percentComplete} onChange={(e) => updateDraft(item.id, { percentComplete: Math.max(0, Math.min(100, Number(e.target.value || '0'))) })} />
          </div>
          <div className="grid gap-2">
            <Label>Assigned User Id</Label>
            <Input value={draft?.assignedToUserId || ''} onChange={(e) => updateDraft(item.id, { assignedToUserId: e.target.value })} />
          </div>
          <div className="grid gap-2">
            <Label>Planned Start</Label>
            <Input type="date" value={draft?.plannedStartDate || ''} onChange={(e) => updateDraft(item.id, { plannedStartDate: e.target.value })} />
          </div>
          <div className="grid gap-2">
            <Label>Planned End</Label>
            <Input type="date" value={draft?.plannedEndDate || ''} onChange={(e) => updateDraft(item.id, { plannedEndDate: e.target.value })} />
          </div>
          <div className="grid gap-2 md:col-span-3">
            <Label>Replan Reason</Label>
            <Input placeholder="Required if you move a scheduled task after locking a baseline" value={draft?.scheduleChangeReason || ''} onChange={(e) => updateDraft(item.id, { scheduleChangeReason: e.target.value })} />
          </div>
        </div>

        {(dependencies.length > 0 || hasViolation) && (
          <div className="mt-4 space-y-2 text-sm">
            {dependencies.map((dependency) => (
              <div key={dependency.id} className="rounded-md border border-dashed px-3 py-2 text-muted-foreground">
                {dependency.dependencyType} predecessor: {project?.workItems ? dependency.predecessorWorkItemId : dependency.predecessorWorkItemId}
              </div>
            ))}
            {analysis?.violations.filter((violation) => violation.workItemId === item.id).map((violation, index) => (
              <div key={`${item.id}-violation-${index}`} className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-red-700">
                {violation.message}
              </div>
            ))}
          </div>
        )}

        <div className="mt-4 flex justify-end">
          <Button onClick={() => saveTask(item)}>Save Task</Button>
        </div>
      </div>
    );
  };

  const kanbanColumns = useMemo(() => boardStatuses.map((status) => ({
    status,
    items: tasks.filter((item) => (drafts[item.id]?.status || item.status) === status),
  })), [drafts, tasks]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Task Board</h1>
        <p className="text-muted-foreground">Operational list and Kanban execution workspace for live project work.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Board Controls</CardTitle>
          <CardDescription>Filter a project, inspect dependency pressure, and update task execution inline.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr_0.8fr_0.8fr]">
          <div className="grid gap-2">
            <Label>Project</Label>
            <Select value={selectedProjectId} onValueChange={(value) => load(value)}>
              <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Select a project</SelectItem>
                {projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Status Filter</Label>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {boardStatuses.map((status) => <SelectItem key={status} value={status}>{status}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Search</Label>
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Find task title or note" />
          </div>
          <div className="grid gap-2">
            <Label>View</Label>
            <Tabs value={view} onValueChange={(value) => setView(value as 'list' | 'kanban')}>
              <TabsList className="grid w-full grid-cols-2">
                <TabsTrigger value="kanban">Kanban</TabsTrigger>
                <TabsTrigger value="list">List</TabsTrigger>
              </TabsList>
            </Tabs>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[0.82fr_1.18fr]">
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Schedule Health</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-1">
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Critical Path Tasks</div>
                <div className="text-2xl font-semibold">{analysis?.criticalPathTaskCount ?? 0}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Dependency Violations</div>
                <div className="text-2xl font-semibold">{analysis?.violations.length ?? 0}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Forecast Finish</div>
                <div className="text-lg font-semibold">{analysis?.forecastFinishDate ? new Date(analysis.forecastFinishDate).toLocaleDateString() : 'N/A'}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Slack Days</div>
                <div className="text-2xl font-semibold">{analysis?.totalSlackDays ?? 0}</div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Quick Task</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid gap-2">
                <Label>Title</Label>
                <Input value={quickTask.title} onChange={(e) => setQuickTask((current) => ({ ...current, title: e.target.value }))} />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <div className="grid gap-2">
                  <Label>Assigned User Id</Label>
                  <Input value={quickTask.assignedToUserId} onChange={(e) => setQuickTask((current) => ({ ...current, assignedToUserId: e.target.value }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Start Date</Label>
                  <Input type="date" value={quickTask.plannedStartDate} onChange={(e) => setQuickTask((current) => ({ ...current, plannedStartDate: e.target.value }))} />
                </div>
              </div>
              <div className="grid gap-2">
                <Label>End Date</Label>
                <Input type="date" value={quickTask.plannedEndDate} onChange={(e) => setQuickTask((current) => ({ ...current, plannedEndDate: e.target.value }))} />
              </div>
              <Button className="w-full" disabled={!project || !quickTask.title.trim()} onClick={addQuickTask}>Add Task</Button>
            </CardContent>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>{view === 'kanban' ? 'Kanban Board' : 'Task List'}</CardTitle>
            <CardDescription>
              {loading ? 'Loading task board...' : project ? `${project.projectCode} | ${project.title}` : 'Choose a project'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-10 text-center text-muted-foreground">Loading tasks...</div>
            ) : !project ? (
              <div className="py-10 text-center text-muted-foreground">Select a project to manage execution work.</div>
            ) : !tasks.length ? (
              <div className="py-10 text-center text-muted-foreground">No tasks match the current filters.</div>
            ) : view === 'list' ? (
              <div className="space-y-4">{tasks.map(renderTaskCard)}</div>
            ) : (
              <div className="grid gap-4 xl:grid-cols-3 2xl:grid-cols-6">
                {kanbanColumns.map((column) => (
                  <div key={column.status} className="rounded-lg border bg-muted/30 p-3">
                    <div className="mb-3 flex items-center justify-between">
                      <div className="font-semibold">{column.status}</div>
                      <Badge variant="outline">{column.items.length}</Badge>
                    </div>
                    <div className="space-y-3">
                      {column.items.length === 0 ? (
                        <div className="rounded-md border border-dashed bg-background px-3 py-6 text-center text-sm text-muted-foreground">No tasks</div>
                      ) : (
                        column.items.map(renderTaskCard)
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
