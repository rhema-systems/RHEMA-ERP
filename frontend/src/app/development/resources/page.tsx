'use client';

import { Fragment, useEffect, useMemo, useState } from 'react';
import { format } from 'date-fns';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  CreateProjectResourceAllocationDto,
  ProjectDetailDto,
  ProjectLookupDto,
  ProjectResourceCapacityReportItemDto,
  ProjectResourceCapacityRecommendationDto,
  ProjectResourceLookupDto,
  ProjectResourceOptimizationSuggestionDto,
  SubstituteProjectResourceAllocationDto,
  projectService,
} from '@/services/projectService';
import { toast } from 'sonner';

const emptyForm: CreateProjectResourceAllocationDto = {
  userId: '',
  allocationRole: 'Team Member',
  allocationType: 'Hours',
  allocationValue: 40,
  plannedHours: 40,
  startDate: new Date().toISOString().slice(0, 10),
  endDate: new Date().toISOString().slice(0, 10),
  bookingType: 'Soft',
  status: 'Requested',
  notes: '',
};

const emptySubstitution: SubstituteProjectResourceAllocationDto = {
  replacementUserId: '',
  fullReplacement: true,
  approveReplacement: true,
  reason: '',
};

const flattenWorkItems = (items: ProjectDetailDto['workItems'] = []): NonNullable<ProjectDetailDto['workItems']> =>
  items.flatMap((item) => [item, ...(item.children || [])]);

export default function DevelopmentResourcesPage() {
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [resourceDirectory, setResourceDirectory] = useState<ProjectResourceLookupDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('all');
  const [selectedResourceId, setSelectedResourceId] = useState<string>('all');
  const [startDate, setStartDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [endDate, setEndDate] = useState<string>(new Date(Date.now() + 1000 * 60 * 60 * 24 * 30).toISOString().slice(0, 10));
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [capacity, setCapacity] = useState<ProjectResourceCapacityReportItemDto[]>([]);
  const [recommendations, setRecommendations] = useState<ProjectResourceCapacityRecommendationDto[]>([]);
  const [optimizationSuggestions, setOptimizationSuggestions] = useState<ProjectResourceOptimizationSuggestionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<CreateProjectResourceAllocationDto>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [substituteForId, setSubstituteForId] = useState<string | null>(null);
  const [substitution, setSubstitution] = useState<SubstituteProjectResourceAllocationDto>(emptySubstitution);
  const projectWorkItems = useMemo(() => flattenWorkItems(project?.workItems), [project?.workItems]);

  const load = async (projectId?: string, resourceId?: string) => {
    try {
      setLoading(true);
      const resolvedResourceId = resourceId && resourceId !== 'all'
        ? resourceId
        : selectedResourceId !== 'all'
          ? selectedResourceId
          : undefined;
      const [projectItems, resourceItems, capacityItems, recommendationItems, optimizationItems] = await Promise.all([
        projectService.lookupProjects(),
        projectService.lookupResources(undefined, 100),
        projectService.getResourceCapacityReport(startDate, endDate, resolvedResourceId),
        projectService.getResourceCapacityRecommendations(startDate, endDate, resolvedResourceId),
        projectService.getResourceOptimizationSuggestions(startDate, endDate),
      ]);

      setProjects(projectItems);
      setResourceDirectory(resourceItems);
      setCapacity(capacityItems);
      setRecommendations(recommendationItems);
      setOptimizationSuggestions(optimizationItems);
      setSelectedResourceId(resolvedResourceId || 'all');

      const resolvedProjectId = projectId === 'all'
        ? undefined
        : projectId && projectId !== 'all'
          ? projectId
          : selectedProjectId !== 'all'
            ? selectedProjectId
            : projectItems[0]?.id;

      if (resolvedProjectId) {
        setSelectedProjectId(resolvedProjectId);
        setProject(await projectService.getProjectById(resolvedProjectId));
      } else {
        setSelectedProjectId('all');
        setProject(null);
      }
    } catch (error: any) {
      toast.error(error.message || 'Failed to load resource planning');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const refreshProject = async (projectId: string) => {
    setSelectedProjectId(projectId);
    if (projectId === 'all') {
      setProject(null);
      return;
    }

    try {
      setProject(await projectService.getProjectById(projectId));
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project resources');
    }
  };

  const saveAllocation = async () => {
    if (!project) {
      toast.error('Select a project first');
      return;
    }

    try {
      setSaving(true);
      await projectService.addResourceAllocation(project.id, {
        ...form,
        workItemId: form.workItemId || undefined,
      });
      setForm(emptyForm);
      await load(project.id, selectedResourceId);
      toast.success('Resource allocation saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save resource allocation');
    } finally {
      setSaving(false);
    }
  };

  const approveAllocation = async (allocationId: string) => {
    try {
      await projectService.approveResourceAllocation(allocationId);
      if (project) {
        await load(project.id, selectedResourceId);
      }
      toast.success('Allocation approved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to approve allocation');
    }
  };

  const deleteAllocation = async (allocationId: string) => {
    if (!window.confirm('Delete this allocation?')) {
      return;
    }

    try {
      await projectService.deleteResourceAllocation(allocationId);
      if (project) {
        await load(project.id, selectedResourceId);
      }
      toast.success('Allocation deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete allocation');
    }
  };

  const openSubstitution = (allocationId: string) => {
    if (!project) return;
    const item = project.resourceAllocations.find((x) => x.id === allocationId);
    if (!item) return;
    const suggestion = optimizationSuggestions.find((x) => x.userId === item.userId && x.affectedAllocationIds.includes(item.id));
    setSubstituteForId(allocationId);
    setSubstitution({
      replacementUserId: suggestion?.suggestedReplacementUserId || '',
      workItemId: item.workItemId,
      transferAllocationValue: item.allocationValue,
      transferPlannedHours: item.plannedHours,
      startDate: String(item.startDate).slice(0, 10),
      endDate: String(item.endDate).slice(0, 10),
      fullReplacement: true,
      approveReplacement: item.status === 'Approved',
      bookingType: item.bookingType,
      reason: suggestion?.recommendation || '',
    });
  };

  const applySubstitution = async () => {
    if (!substituteForId) return;
    try {
      await projectService.substituteResourceAllocation(substituteForId, substitution);
      if (project) {
        await load(project.id, selectedResourceId);
      }
      setSubstituteForId(null);
      setSubstitution(emptySubstitution);
      toast.success('Resource substitution applied');
    } catch (error: any) {
      toast.error(error.message || 'Failed to substitute allocation');
    }
  };

  const summary = useMemo(() => {
    const total = capacity.length;
    const conflictCount = capacity.filter((item) => item.conflictCount > 0).length;
    const averageUtilization = total
      ? Math.round(capacity.reduce((sum, item) => sum + item.capacityUtilizationPercent, 0) / total)
      : 0;
    const overAllocated = capacity.filter((item) => item.capacityUtilizationPercent > 100).length;
    const leaveImpacted = capacity.filter((item) => item.leaveRequestCount > 0).length;
    const qualificationWatch = capacity.filter((item) => item.qualificationRisk !== 'Healthy').length;

    return { total, conflictCount, averageUtilization, overAllocated, leaveImpacted, qualificationWatch };
  }, [capacity]);

  const resourceLabels = useMemo(() => {
    const labels = new Map<string, string>();
    resourceDirectory.forEach((item) => labels.set(item.id, item.displayName));
    capacity.forEach((item) => labels.set(item.userId, item.userDisplayName || item.userId));
    recommendations.forEach((item) => {
      labels.set(item.userId, item.userDisplayName || labels.get(item.userId) || item.userId);
      if (item.suggestedReplacementUserId) {
        labels.set(
          item.suggestedReplacementUserId,
          item.suggestedReplacementUserDisplayName || labels.get(item.suggestedReplacementUserId) || item.suggestedReplacementUserId,
        );
      }
    });
    optimizationSuggestions.forEach((item) => {
      labels.set(item.userId, item.userDisplayName || labels.get(item.userId) || item.userId);
      if (item.suggestedReplacementUserId) {
        labels.set(
          item.suggestedReplacementUserId,
          item.suggestedReplacementUserDisplayName || labels.get(item.suggestedReplacementUserId) || item.suggestedReplacementUserId,
        );
      }
    });
    return labels;
  }, [capacity, optimizationSuggestions, recommendations, resourceDirectory]);

  const resourceUserOptions = useMemo(() => {
    const ids = new Set<string>();
    capacity.forEach((item) => ids.add(item.userId));
    recommendations.forEach((item) => {
      ids.add(item.userId);
      if (item.suggestedReplacementUserId) ids.add(item.suggestedReplacementUserId);
    });
    optimizationSuggestions.forEach((item) => {
      ids.add(item.userId);
      if (item.suggestedReplacementUserId) ids.add(item.suggestedReplacementUserId);
    });
    project?.resourceAllocations.forEach((item) => ids.add(item.userId));
    return Array.from(ids)
      .sort((left, right) => (resourceLabels.get(left) || left).localeCompare(resourceLabels.get(right) || right))
      .map((id) => ({ id, label: resourceLabels.get(id) || id }));
  }, [capacity, optimizationSuggestions, project?.resourceAllocations, recommendations, resourceLabels]);

  const getResourceLabel = (userId: string, displayName?: string) => displayName || resourceLabels.get(userId) || userId;
  const getWorkItemLabel = (workItemId?: string) => {
    if (!workItemId) {
      return 'Project level';
    }

    const match = projectWorkItems.find((item) => item.id === workItemId);
    return match?.title || workItemId;
  };

  const criticalRecommendations = recommendations.filter((item) => item.severity === 'Critical').length;
  const highRecommendations = recommendations.filter((item) => item.severity === 'High').length;
  const spareCapacity = capacity.filter((item) => item.capacityUtilizationPercent < 80).length;
  const getQualificationBadgeVariant = (risk: string) => {
    if (risk === 'Critical') return 'destructive' as const;
    if (risk === 'Watch') return 'default' as const;
    if (risk === 'Limited') return 'secondary' as const;
    return 'outline' as const;
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Resource Planning</h1>
        <p className="text-muted-foreground">Approve project allocations, watch conflicts, and track utilization by resource.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Planning Window</CardTitle>
          <CardDescription>Filter capacity results by period or resource.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 lg:grid-cols-[0.9fr_0.9fr_1.2fr_0.6fr]">
          <div className="grid gap-2">
            <Label>Start Date</Label>
            <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </div>
          <div className="grid gap-2">
            <Label>End Date</Label>
            <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
          </div>
          <div className="grid gap-2">
            <Label>Focus Resource</Label>
              <Select value={selectedResourceId} onValueChange={setSelectedResourceId}>
                <SelectTrigger><SelectValue placeholder="All resources" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All resources</SelectItem>
                  {resourceUserOptions.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          <div className="flex items-end">
            <Button className="w-full" onClick={() => load(selectedProjectId, selectedResourceId)}>Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-9">
        <Card><CardHeader className="pb-2"><CardDescription>Resources Planned</CardDescription><CardTitle>{summary.total}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Conflicts</CardDescription><CardTitle>{summary.conflictCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Avg Utilization</CardDescription><CardTitle>{summary.averageUtilization}%</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Over Allocated</CardDescription><CardTitle>{summary.overAllocated}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Leave Impacted</CardDescription><CardTitle>{summary.leaveImpacted}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Qualification Watch</CardDescription><CardTitle>{summary.qualificationWatch}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Critical Actions</CardDescription><CardTitle>{criticalRecommendations}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>High Priority Actions</CardDescription><CardTitle>{highRecommendations}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Spare Capacity</CardDescription><CardTitle>{spareCapacity}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Capacity Recommendations</CardTitle>
          <CardDescription>Recommended actions based on capacity results.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading recommendations...</div>
          ) : !recommendations.length ? (
            <div className="py-10 text-center text-muted-foreground">No capacity recommendations right now.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Utilization</TableHead>
                  <TableHead>Effective Capacity</TableHead>
                  <TableHead>Leave</TableHead>
                  <TableHead>Conflicts</TableHead>
                  <TableHead>Suggested Reduction</TableHead>
                  <TableHead>Qualifications</TableHead>
                  <TableHead>Recommendation</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {recommendations.map((item) => (
                  <TableRow key={item.userId}>
                    <TableCell>
                      <div className="font-medium">{getResourceLabel(item.userId, item.userDisplayName)}</div>
                      <div className="text-xs text-muted-foreground">{item.projectCodes.join(', ') || 'Unassigned'}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.severity === 'Critical' ? 'destructive' : item.severity === 'High' ? 'default' : 'outline'}>
                        {item.severity}
                      </Badge>
                    </TableCell>
                    <TableCell>{item.capacityUtilizationPercent.toLocaleString()}%</TableCell>
                    <TableCell>{item.effectiveCapacityHours.toLocaleString()} hrs</TableCell>
                    <TableCell>{item.leaveRequestCount ? `${item.approvedLeaveDays.toLocaleString()} days` : 'Clear'}</TableCell>
                    <TableCell>{item.conflictCount}</TableCell>
                    <TableCell>{item.suggestedReductionHours.toLocaleString()} hrs</TableCell>
                    <TableCell>
                      <div className="space-y-1 text-xs">
                        <Badge variant={getQualificationBadgeVariant(item.qualificationRisk)}>
                          {item.qualificationRisk}
                        </Badge>
                        <div className="text-muted-foreground">
                          {item.verifiedSkillCount} verified, {item.certifiedSkillCount} certified
                        </div>
                        {(item.expiringCertificationCount > 0 || item.expiredCertificationCount > 0) ? (
                          <div className="text-muted-foreground">
                            {item.expiredCertificationCount} expired, {item.expiringCertificationCount} expiring
                          </div>
                        ) : null}
                      </div>
                    </TableCell>
                    <TableCell className="max-w-md text-sm text-muted-foreground">
                      <div>{item.recommendation}</div>
                      {item.suggestedReplacementUserId ? (
                        <div className="mt-1 text-xs">
                          Suggested substitute: {getResourceLabel(item.suggestedReplacementUserId, item.suggestedReplacementUserDisplayName)}
                        </div>
                      ) : null}
                      {item.matchedSkills?.length ? <div className="mt-1 text-xs">Skills: {item.matchedSkills.join(', ')}</div> : null}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Optimization Suggestions</CardTitle>
          <CardDescription>Suggested substitutions for overloaded or conflicting resources.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading optimization suggestions...</div>
          ) : !optimizationSuggestions.length ? (
            <div className="py-10 text-center text-muted-foreground">No substitution suggestions available right now.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User</TableHead>
                  <TableHead>Suggested Substitute</TableHead>
                  <TableHead>Skills</TableHead>
                  <TableHead>Coverage</TableHead>
                  <TableHead>Recommendation</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {optimizationSuggestions.map((item) => (
                  <TableRow key={item.userId}>
                    <TableCell>{getResourceLabel(item.userId, item.userDisplayName)}</TableCell>
                    <TableCell>{item.suggestedReplacementUserId ? getResourceLabel(item.suggestedReplacementUserId, item.suggestedReplacementUserDisplayName) : 'No match'}</TableCell>
                    <TableCell>
                      <div>{item.matchedSkills.join(', ') || 'No verified skills'}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.matchedSkillCount} shared, {item.matchedCertifiedSkillCount} certified
                      </div>
                    </TableCell>
                    <TableCell>
                      {item.suggestedReplacementUserId ? (
                        <div className="space-y-1 text-xs">
                          <Badge variant={getQualificationBadgeVariant(item.replacementQualificationRisk)}>
                            {item.replacementQualificationRisk}
                          </Badge>
                          <div className="text-muted-foreground">
                            {item.replacementVerifiedSkillCount} verified, {item.replacementCertifiedSkillCount} certified
                          </div>
                          {(item.replacementExpiringCertificationCount > 0 || item.replacementExpiredCertificationCount > 0) ? (
                            <div className="text-muted-foreground">
                              {item.replacementExpiredCertificationCount} expired, {item.replacementExpiringCertificationCount} expiring
                            </div>
                          ) : null}
                        </div>
                      ) : (
                        <span className="text-muted-foreground">No qualified substitute</span>
                      )}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{item.recommendation}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Capacity Watchlist</CardTitle>
            <CardDescription>{loading ? 'Loading...' : 'Utilization across tracked project resources'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-10 text-center text-muted-foreground">Loading resource capacity...</div>
            ) : !capacity.length ? (
              <div className="py-10 text-center text-muted-foreground">No resource capacity data available.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>User</TableHead>
                    <TableHead>Allocations</TableHead>
                    <TableHead>Hours</TableHead>
                  <TableHead>Available</TableHead>
                  <TableHead>Leave</TableHead>
                  <TableHead>Utilization</TableHead>
                  <TableHead>Conflicts</TableHead>
                  <TableHead>Qualifications</TableHead>
                </TableRow>
              </TableHeader>
                <TableBody>
                  {capacity.map((item) => (
                    <TableRow key={item.userId}>
                      <TableCell>
                        <div className="font-medium">{getResourceLabel(item.userId, item.userDisplayName)}</div>
                      </TableCell>
                      <TableCell>{item.allocationCount}</TableCell>
                      <TableCell>{item.totalAllocatedHours.toLocaleString()}</TableCell>
                      <TableCell>{item.effectiveCapacityHours.toLocaleString()} hrs</TableCell>
                      <TableCell>{item.leaveRequestCount ? `${item.approvedLeaveDays.toLocaleString()} days` : 'Clear'}</TableCell>
                      <TableCell>
                        <Badge variant={item.capacityUtilizationPercent > 100 ? 'destructive' : 'outline'}>
                          {item.capacityUtilizationPercent.toLocaleString()}%
                        </Badge>
                      </TableCell>
                      <TableCell>{item.conflictCount}</TableCell>
                      <TableCell>
                        <div className="space-y-1 text-xs">
                          <Badge variant={getQualificationBadgeVariant(item.qualificationRisk)}>
                            {item.qualificationRisk}
                          </Badge>
                          <div className="text-muted-foreground">
                            {item.verifiedSkillCount} verified, {item.certifiedSkillCount} certified
                          </div>
                          {(item.expiringCertificationCount > 0 || item.expiredCertificationCount > 0) ? (
                            <div className="text-muted-foreground">
                              {item.expiredCertificationCount} expired, {item.expiringCertificationCount} expiring
                            </div>
                          ) : null}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Allocate Resource</CardTitle>
            <CardDescription>Create a soft or hard booking against a project or task.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-2">
              <Label>Project</Label>
              <Select value={selectedProjectId} onValueChange={(value) => refreshProject(value)}>
                <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Select project</SelectItem>
                  {projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Resource</Label>
              <Select value={form.userId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, userId: value === 'none' ? '' : value }))}>
                <SelectTrigger><SelectValue placeholder="Select resource" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Select resource</SelectItem>
                  {resourceDirectory.map((item) => <SelectItem key={item.id} value={item.id}>{item.displayName}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Role</Label>
                <Input value={form.allocationRole} onChange={(e) => setForm((prev) => ({ ...prev, allocationRole: e.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Work Item</Label>
                <Select value={form.workItemId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, workItemId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Optional task" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Project level</SelectItem>
                    {project?.workItems.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Allocation Type</Label>
                <Select value={form.allocationType} onValueChange={(value) => setForm((prev) => ({ ...prev, allocationType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Hours">Hours</SelectItem>
                    <SelectItem value="Percent">Percent</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Booking</Label>
                <Select value={form.bookingType} onValueChange={(value) => setForm((prev) => ({ ...prev, bookingType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Soft">Soft</SelectItem>
                    <SelectItem value="Hard">Hard</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Allocation Value</Label>
                <Input type="number" value={form.allocationValue} onChange={(e) => setForm((prev) => ({ ...prev, allocationValue: Number(e.target.value || '0') }))} />
              </div>
              <div className="grid gap-2">
                <Label>Planned Hours</Label>
                <Input type="number" value={form.plannedHours ?? ''} onChange={(e) => setForm((prev) => ({ ...prev, plannedHours: e.target.value ? Number(e.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Start Date</Label>
                <Input type="date" value={String(form.startDate).slice(0, 10)} onChange={(e) => setForm((prev) => ({ ...prev, startDate: e.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>End Date</Label>
                <Input type="date" value={String(form.endDate).slice(0, 10)} onChange={(e) => setForm((prev) => ({ ...prev, endDate: e.target.value }))} />
              </div>
            </div>
            <div className="grid gap-2">
              <Label>Notes</Label>
              <Input value={form.notes || ''} onChange={(e) => setForm((prev) => ({ ...prev, notes: e.target.value }))} />
            </div>
            <Button onClick={saveAllocation} disabled={saving || !project || !form.userId}>
              {saving ? 'Saving...' : 'Save Allocation'}
            </Button>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Project Allocations</CardTitle>
          <CardDescription>{project ? `${project.projectCode} | ${project.title}` : 'Select a project to view allocations'}</CardDescription>
        </CardHeader>
        <CardContent>
          {!project ? (
            <div className="py-10 text-center text-muted-foreground">Select a project to manage resource allocations.</div>
          ) : !project.resourceAllocations.length ? (
            <div className="py-10 text-center text-muted-foreground">No resource allocations created yet.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Dates</TableHead>
                  <TableHead>Load</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {project.resourceAllocations.map((item) => (
                  <Fragment key={item.id}>
                  <TableRow>
                    <TableCell>
                      <div className="font-medium">{getResourceLabel(item.userId)}</div>
                      <div className="text-xs text-muted-foreground">{getWorkItemLabel(item.workItemId)}</div>
                    </TableCell>
                    <TableCell>{item.allocationRole}</TableCell>
                    <TableCell>
                      {format(new Date(item.startDate), 'MMM dd, yyyy')} to {format(new Date(item.endDate), 'MMM dd, yyyy')}
                    </TableCell>
                    <TableCell>
                      <div>{item.allocationValue.toLocaleString()} {item.allocationType}</div>
                      <div className="text-xs text-muted-foreground">{item.capacityUtilizationPercent.toLocaleString()}% capacity</div>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Badge variant={item.hasConflict ? 'destructive' : 'outline'}>{item.status}</Badge>
                        {item.hasConflict && <Badge variant="destructive">Conflict</Badge>}
                        {item.replacementAllocationId ? <Badge variant="secondary">Has replacement</Badge> : null}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-2">
                        {item.status !== 'Approved' && <Button variant="outline" size="sm" onClick={() => approveAllocation(item.id)}>Approve</Button>}
                        {item.canSubstitute && <Button variant="outline" size="sm" onClick={() => openSubstitution(item.id)}>Substitute</Button>}
                        <Button variant="ghost" size="sm" onClick={() => deleteAllocation(item.id)}>Delete</Button>
                      </div>
                    </TableCell>
                  </TableRow>
                {substituteForId === item.id ? (
                  <TableRow key={`${item.id}-substitution`}>
                    <TableCell colSpan={6}>
                      <div className="grid gap-3 rounded-md border p-4 md:grid-cols-3">
                        <div className="grid gap-2">
                          <Label>Replacement User</Label>
                          <Select value={substitution.replacementUserId || 'none'} onValueChange={(value) => setSubstitution((prev) => ({ ...prev, replacementUserId: value === 'none' ? '' : value }))}>
                            <SelectTrigger><SelectValue placeholder="Select replacement" /></SelectTrigger>
                            <SelectContent>
                              <SelectItem value="none">Select replacement</SelectItem>
                              {resourceUserOptions.filter((x) => x.id !== item.userId).map((x) => <SelectItem key={x.id} value={x.id}>{x.label}</SelectItem>)}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="grid gap-2">
                          <Label>Transfer Value</Label>
                          <Input type="number" value={substitution.transferAllocationValue ?? item.allocationValue} onChange={(e) => setSubstitution((prev) => ({ ...prev, transferAllocationValue: Number(e.target.value || '0') }))} />
                        </div>
                        <div className="grid gap-2">
                          <Label>Transfer Hours</Label>
                          <Input type="number" value={substitution.transferPlannedHours ?? item.plannedHours ?? ''} onChange={(e) => setSubstitution((prev) => ({ ...prev, transferPlannedHours: e.target.value ? Number(e.target.value) : undefined }))} />
                        </div>
                        <div className="grid gap-2">
                          <Label>Start Date</Label>
                          <Input type="date" value={substitution.startDate || String(item.startDate).slice(0, 10)} onChange={(e) => setSubstitution((prev) => ({ ...prev, startDate: e.target.value }))} />
                        </div>
                        <div className="grid gap-2">
                          <Label>End Date</Label>
                          <Input type="date" value={substitution.endDate || String(item.endDate).slice(0, 10)} onChange={(e) => setSubstitution((prev) => ({ ...prev, endDate: e.target.value }))} />
                        </div>
                        <div className="grid gap-2">
                          <Label>Replacement Mode</Label>
                          <Select value={substitution.fullReplacement ? 'full' : 'partial'} onValueChange={(value) => setSubstitution((prev) => ({ ...prev, fullReplacement: value === 'full' }))}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                              <SelectItem value="full">Full replacement</SelectItem>
                              <SelectItem value="partial">Partial rebalance</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="grid gap-2 md:col-span-2">
                          <Label>Reason</Label>
                          <Input value={substitution.reason || ''} onChange={(e) => setSubstitution((prev) => ({ ...prev, reason: e.target.value }))} />
                        </div>
                        <div className="grid gap-2">
                          <Label>Replacement Status</Label>
                          <Select value={substitution.approveReplacement ? 'approved' : 'requested'} onValueChange={(value) => setSubstitution((prev) => ({ ...prev, approveReplacement: value === 'approved' }))}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                              <SelectItem value="approved">Approve now</SelectItem>
                              <SelectItem value="requested">Requested</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="flex items-end gap-2 md:col-span-3">
                          <Button onClick={applySubstitution} disabled={!substitution.replacementUserId || !substitution.reason?.trim()}>Apply Substitution</Button>
                          <Button variant="ghost" onClick={() => { setSubstituteForId(null); setSubstitution(emptySubstitution); }}>Cancel</Button>
                        </div>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : null}
                  </Fragment>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
