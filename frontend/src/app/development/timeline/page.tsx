'use client';

import { useEffect, useMemo, useState } from 'react';
import { format } from 'date-fns';
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
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ProjectBaselineComparisonDto, ProjectDetailDto, ProjectLookupDto, ProjectScheduleAnalysisDto, ProjectWorkItemDto, projectService } from '@/services/projectService';
import { toast } from 'sonner';

type TimelineDraft = {
  plannedStartDate: string;
  plannedEndDate: string;
  scheduleChangeReason: string;
};

const flatten = (items: ProjectWorkItemDto[], depth = 0): Array<ProjectWorkItemDto & { depth: number }> =>
  items.flatMap((item) => [{ ...item, depth }, ...flatten(item.children || [], depth + 1)]);

const toDateInput = (value?: string) => (value ? String(value).slice(0, 10) : '');

export default function DevelopmentTimelinePage() {
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('all');
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [analysis, setAnalysis] = useState<ProjectScheduleAnalysisDto | null>(null);
  const [baselineComparison, setBaselineComparison] = useState<ProjectBaselineComparisonDto | null>(null);
  const [selectedBaselineId, setSelectedBaselineId] = useState<string>('none');
  const [baselineName, setBaselineName] = useState('');
  const [loading, setLoading] = useState(true);
  const [drafts, setDrafts] = useState<Record<string, TimelineDraft>>({});
  const [baseCurrency, setBaseCurrency] = useState<ProjectCurrencyReference>(DEFAULT_PROJECT_CURRENCY);

  const load = async (projectId?: string) => {
    try {
      setLoading(true);
      const [projectItems, currencyContext] = await Promise.all([
        projectService.lookupProjects(),
        loadProjectCurrencyContext(),
      ]);
      setProjects(projectItems);
      setBaseCurrency(currencyContext.baseCurrency);

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
          .filter((item) => item.plannedStartDate || item.plannedEndDate)
          .map((item) => [item.id, {
            plannedStartDate: toDateInput(item.plannedStartDate),
            plannedEndDate: toDateInput(item.plannedEndDate),
            scheduleChangeReason: '',
          }]),
      ));
      setSelectedBaselineId('none');
      setBaselineComparison(null);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load timeline workspace');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const timelineItems = useMemo(() => {
    if (!project) return [];
    return flatten(project.workItems)
      .filter((item) => item.plannedStartDate || item.plannedEndDate)
      .map((item) => ({
        ...item,
        startDate: item.plannedStartDate ? new Date(item.plannedStartDate) : item.plannedEndDate ? new Date(item.plannedEndDate) : null,
        endDate: item.plannedEndDate ? new Date(item.plannedEndDate) : item.plannedStartDate ? new Date(item.plannedStartDate) : null,
      }))
      .filter((item): item is ProjectWorkItemDto & { depth: number; startDate: Date; endDate: Date } => Boolean(item.startDate && item.endDate))
      .sort((a, b) => a.startDate.getTime() - b.startDate.getTime());
  }, [project]);

  const range = useMemo(() => {
    if (!timelineItems.length) return null;
    const starts = timelineItems.map((item) => item.startDate?.getTime()).filter((value): value is number => typeof value === 'number');
    const ends = timelineItems.map((item) => item.endDate?.getTime()).filter((value): value is number => typeof value === 'number');
    if (!starts.length || !ends.length) return null;
    const min = new Date(Math.min(...starts));
    const max = new Date(Math.max(...ends));
    const totalDays = Math.max(1, Math.round((max.getTime() - min.getTime()) / 86400000) + 1);
    return { min, max, totalDays };
  }, [timelineItems]);

  const updateDraft = (workItemId: string, patch: Partial<TimelineDraft>) => {
    setDrafts((current) => ({
      ...current,
      [workItemId]: {
        ...current[workItemId],
        ...patch,
      },
    }));
  };

  const formatMoney = (value: number, currency?: string | null) => formatProjectMoney(value, currency, baseCurrency.code);

  const saveDates = async (item: ProjectWorkItemDto) => {
    const draft = drafts[item.id];
    if (!draft) return;

    try {
      await projectService.updateWorkItem(item.id, {
        parentId: item.parentId,
        nodeType: item.nodeType,
        title: item.title,
        description: item.description,
        status: item.status,
        priority: item.priority,
        assignedToUserId: item.assignedToUserId,
        plannedStartDate: draft.plannedStartDate || undefined,
        plannedEndDate: draft.plannedEndDate || undefined,
        actualStartDate: item.actualStartDate,
        actualEndDate: item.actualEndDate,
        percentComplete: item.percentComplete,
        isRollupEnabled: item.isRollupEnabled,
        effortEstimateHours: item.effortEstimateHours,
        actualEffortHours: item.actualEffortHours,
        scheduleChangeReason: draft.scheduleChangeReason || undefined,
      });
      await load(project?.id);
      toast.success('Schedule updated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to update schedule');
    }
  };

  const recalculate = async () => {
    if (!project) return;
    try {
      const schedule = await projectService.recalculateSchedule(project.id);
      setAnalysis(schedule);
      await load(project.id);
      toast.success(schedule.recalculatedItemCount > 0 ? `Recalculated ${schedule.recalculatedItemCount} task(s)` : 'Schedule already aligned to dependencies');
    } catch (error: any) {
      toast.error(error.message || 'Failed to recalculate schedule');
    }
  };

  const compareBaseline = async (baselineId: string) => {
    setSelectedBaselineId(baselineId);
    if (baselineId === 'none') {
      setBaselineComparison(null);
      return;
    }

    try {
      setBaselineComparison(await projectService.compareBaseline(baselineId));
    } catch (error: any) {
      toast.error(error.message || 'Failed to compare baseline');
    }
  };

  const createBaseline = async () => {
    if (!project || !baselineName.trim()) return;
    try {
      await projectService.createBaseline(project.id, { name: baselineName.trim() });
      setBaselineName('');
      await load(project.id);
      toast.success('Baseline captured');
    } catch (error: any) {
      toast.error(error.message || 'Failed to create baseline');
    }
  };

  const barMetrics = (item: typeof timelineItems[number]) => {
    if (!range || !item.startDate || !item.endDate) return { left: '0%', width: '100%' };
    const startOffset = Math.max(0, Math.round((item.startDate.getTime() - range.min.getTime()) / 86400000));
    const duration = Math.max(1, Math.round((item.endDate.getTime() - item.startDate.getTime()) / 86400000) + 1);
    return {
      left: `${(startOffset / range.totalDays) * 100}%`,
      width: `${Math.max((duration / range.totalDays) * 100, 2)}%`,
    };
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Timeline Workspace</h1>
        <p className="text-muted-foreground">Dependency-aware planning, baseline comparison, and Gantt-style schedule visibility.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Schedule Controls</CardTitle>
          <CardDescription>Choose a project, recalculate successor dates, and inspect baseline variance. Once a baseline is locked, date changes require an explicit replan reason.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr_0.9fr_0.8fr]">
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
            <Label>Baseline</Label>
            <Select value={selectedBaselineId} onValueChange={compareBaseline}>
              <SelectTrigger><SelectValue placeholder="Compare baseline" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No comparison</SelectItem>
                {project?.baselines.map((baseline) => <SelectItem key={baseline.id} value={baseline.id}>{baseline.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>New Baseline</Label>
            <Input value={baselineName} onChange={(e) => setBaselineName(e.target.value)} placeholder="Baseline name" />
          </div>
          <div className="flex items-end gap-2">
            <Button variant="outline" disabled={!project} onClick={recalculate}>Recalculate</Button>
            <Button disabled={!project || !baselineName.trim()} onClick={createBaseline}>Capture</Button>
          </div>
        </CardContent>
      </Card>

      {project?.hasLockedBaseline ? (
        <Card>
          <CardContent className="grid gap-4 p-4 md:grid-cols-4">
            <div>
              <div className="text-sm text-muted-foreground">Active Baseline</div>
              <div className="font-semibold">{project.activeBaselineName || 'Locked baseline'}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground">Captured</div>
              <div className="font-semibold">{project.activeBaselineCreatedOn ? format(new Date(project.activeBaselineCreatedOn), 'MMM dd, yyyy HH:mm') : 'N/A'}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground">Off-Baseline Tasks</div>
              <div className="font-semibold">{timelineItems.filter((item) => item.isOffBaseline).length}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground">Current Drift</div>
              <div className="font-semibold">{baselineComparison ? `${baselineComparison.scheduleVarianceDays} days` : 'Select baseline to compare'}</div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Schedule Analysis</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2 xl:grid-cols-1">
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Dependencies</div>
                <div className="text-2xl font-semibold">{analysis?.dependencyCount ?? 0}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Critical Path</div>
                <div className="text-2xl font-semibold">{analysis?.criticalPathTaskCount ?? 0}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Forecast Finish</div>
                <div className="text-lg font-semibold">{analysis?.forecastFinishDate ? format(new Date(analysis.forecastFinishDate), 'MMM dd, yyyy') : 'N/A'}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Circular Paths</div>
                <div className="text-2xl font-semibold">{analysis?.hasCircularDependencies ? 'Yes' : 'No'}</div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Dependency Violations</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {!analysis?.violations.length ? (
                <div className="text-sm text-muted-foreground">No enforced dependency violations detected.</div>
              ) : (
                analysis.violations.map((violation, index) => (
                  <div key={`${violation.workItemId}-${index}`} className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                    <div className="font-medium">{violation.workItemTitle}</div>
                    <div>{violation.message}</div>
                    {violation.expectedDate ? <div className="mt-1 text-xs">Expected not earlier than {format(new Date(violation.expectedDate), 'MMM dd, yyyy')}</div> : null}
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          {baselineComparison && (
            <Card>
              <CardHeader>
                <CardTitle>Baseline Comparison</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-1">
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Baseline</div>
                  <div className="font-semibold">{baselineComparison.baselineName}</div>
                  {baselineComparison.baselineCreatedOn ? <div className="text-xs text-muted-foreground">{format(new Date(baselineComparison.baselineCreatedOn), 'MMM dd, yyyy HH:mm')}</div> : null}
                </div>
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Schedule Variance</div>
                  <div className="text-2xl font-semibold">{baselineComparison.scheduleVarianceDays} days</div>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Budget Variance</div>
                  <div className="text-2xl font-semibold">{formatMoney(baselineComparison.budgetVariance)}</div>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Changed Work Items</div>
                  <div className="text-2xl font-semibold">{baselineComparison.changedWorkItemCount}</div>
                </div>
              </CardContent>
              <CardContent className="space-y-4">
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-sm text-muted-foreground">Baseline Finish</div>
                    <div className="font-semibold">{baselineComparison.baselineFinishDate ? format(new Date(baselineComparison.baselineFinishDate), 'MMM dd, yyyy') : 'N/A'}</div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-sm text-muted-foreground">Current Finish</div>
                    <div className="font-semibold">{baselineComparison.currentFinishDate ? format(new Date(baselineComparison.currentFinishDate), 'MMM dd, yyyy') : 'N/A'}</div>
                  </div>
                </div>
                <div className="space-y-3">
                  <div className="font-medium">Changed Work Items</div>
                  {!baselineComparison.workItemChanges.length ? (
                    <div className="text-sm text-muted-foreground">No work-item drift from the selected baseline.</div>
                  ) : (
                    baselineComparison.workItemChanges.slice(0, 8).map((change) => (
                      <div key={change.workItemId} className="rounded-md border p-3 text-sm">
                        <div className="font-medium">{change.workItemTitle}</div>
                        <div className="text-muted-foreground">
                          {change.baselinePlannedStartDate ? format(new Date(change.baselinePlannedStartDate), 'MMM dd, yyyy') : 'N/A'} to {change.baselinePlannedEndDate ? format(new Date(change.baselinePlannedEndDate), 'MMM dd, yyyy') : 'N/A'}
                          {' -> '}
                          {change.currentPlannedStartDate ? format(new Date(change.currentPlannedStartDate), 'MMM dd, yyyy') : 'N/A'} to {change.currentPlannedEndDate ? format(new Date(change.currentPlannedEndDate), 'MMM dd, yyyy') : 'N/A'}
                          {` | variance ${change.scheduleVarianceDays} day(s)`}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </CardContent>
            </Card>
          )}
        </div>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Gantt View</CardTitle>
              <CardDescription>{loading ? 'Loading timeline...' : project ? `${project.projectCode} | ${project.title}` : 'Choose a project'}</CardDescription>
            </CardHeader>
            <CardContent>
              {!range || !timelineItems.length ? (
                <div className="py-10 text-center text-muted-foreground">No scheduled work items are available for this project.</div>
              ) : (
                <div className="space-y-3">
                  <div className="rounded-lg border bg-muted/20 p-4">
                    <div className="mb-4 flex items-center justify-between text-sm text-muted-foreground">
                      <span>{format(range.min, 'MMM dd, yyyy')}</span>
                      <span>{range.totalDays} days</span>
                      <span>{format(range.max, 'MMM dd, yyyy')}</span>
                    </div>
                    <div className="space-y-3">
                      {timelineItems.map((item) => {
                        const critical = analysis?.criticalPathWorkItemIds.includes(item.id);
                        const bar = barMetrics(item);
                        return (
                          <div key={item.id} className="grid gap-2 md:grid-cols-[240px_1fr] md:items-center">
                            <div>
                              <div className="font-medium" style={{ paddingLeft: item.depth * 10 }}>{item.title}</div>
                              <div className="text-xs text-muted-foreground">{item.nodeType} | {item.status}{item.isOffBaseline ? ` | off baseline by ${item.baselineVarianceDays} day(s)` : ''}</div>
                            </div>
                            <div className="relative h-8 rounded-md bg-muted">
                              <div
                                className={`absolute top-1.5 h-5 rounded-md ${critical ? 'bg-red-500' : 'bg-sky-600'}`}
                                style={{ left: bar.left, width: bar.width }}
                              />
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Plan Adjustments</CardTitle>
              <CardDescription>Update planned dates with explicit reasons once a baseline exists.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {!timelineItems.length ? (
                <div className="text-sm text-muted-foreground">No dated tasks are available for schedule editing.</div>
              ) : (
                timelineItems.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="mb-3 flex items-center justify-between gap-3">
                      <div>
                        <div className="font-semibold">{item.title}</div>
                        <div className="text-sm text-muted-foreground">
                          {item.nodeType}
                          {item.baselinePlannedStartDate || item.baselinePlannedEndDate ? ` | baseline ${item.baselinePlannedStartDate ? format(new Date(item.baselinePlannedStartDate), 'MMM dd') : 'N/A'} - ${item.baselinePlannedEndDate ? format(new Date(item.baselinePlannedEndDate), 'MMM dd, yyyy') : 'N/A'}` : ''}
                        </div>
                      </div>
                      <div className="flex gap-2">
                        {item.isOffBaseline ? <Badge variant="secondary">Off baseline</Badge> : null}
                        <Badge variant={analysis?.criticalPathWorkItemIds.includes(item.id) ? 'destructive' : 'outline'}>
                          {analysis?.criticalPathWorkItemIds.includes(item.id) ? 'Critical Path' : item.status}
                        </Badge>
                      </div>
                    </div>
                    <div className="grid gap-3 md:grid-cols-3">
                      <div className="grid gap-2">
                        <Label>Planned Start</Label>
                        <Input type="date" value={drafts[item.id]?.plannedStartDate || ''} onChange={(e) => updateDraft(item.id, { plannedStartDate: e.target.value })} />
                      </div>
                      <div className="grid gap-2">
                        <Label>Planned End</Label>
                        <Input type="date" value={drafts[item.id]?.plannedEndDate || ''} onChange={(e) => updateDraft(item.id, { plannedEndDate: e.target.value })} />
                      </div>
                      <div className="grid gap-2">
                        <Label>Replan Reason</Label>
                        <Input value={drafts[item.id]?.scheduleChangeReason || ''} onChange={(e) => updateDraft(item.id, { scheduleChangeReason: e.target.value })} placeholder="Recovery, approval, dependency update..." />
                      </div>
                    </div>
                    <div className="mt-3 flex justify-end">
                      <Button onClick={() => saveDates(item)}>Save Dates</Button>
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
