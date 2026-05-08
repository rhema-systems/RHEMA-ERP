import { useMemo, type Dispatch, type SetStateAction } from 'react';
import { Maximize2, Pencil, Plus, Save, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type {
  CreateProjectMilestoneDto,
  CreateProjectResourceAllocationDto,
  CreateProjectTaskDependencyDto,
  CreateProjectWorkItemDto,
  ProjectDetailDto,
  ProjectWorkItemDto,
} from '@/services/projectService';

type FlatWorkItem = ProjectWorkItemDto & { depth: number; outline: string };
type IdentifiedRecord = { id: string };
type TaskView = 'tree' | 'kanban' | 'timeline';
type TimelineBounds = { min: number; max: number; totalDays: number } | null;
type GanttSummary = { scheduledItems: number; spanDays: number; overdueItems: number; phases: number } | null;
type FlatProjectPhase = ProjectDetailDto['phases'][number] & { depth: number };

type ProjectPlanTabProps = {
  project: ProjectDetailDto;
  flat: FlatWorkItem[];
  editingWorkItemId: string | null;
  work: CreateProjectWorkItemDto;
  setWork: Dispatch<SetStateAction<CreateProjectWorkItemDto>>;
  taskStatusOptions: string[];
  taskPriorityOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  saveWorkEditor: () => Promise<void> | void;
  resetWorkEditor: () => void;
  scheduleReasonRequired: boolean;
  taskView: TaskView;
  setTaskView: Dispatch<SetStateAction<TaskView>>;
  taskBoardStatuses: string[];
  taskBoardItems: ProjectWorkItemDto[];
  timelineBounds: TimelineBounds;
  ganttSummary: GanttSummary;
  workItemTitles: Map<string, string>;
  formatDateLabel: (value?: string) => string;
  onOpenGantt: () => void;
  onBeginEditWorkItem: (item: ProjectWorkItemDto, options?: { keepCurrentView?: boolean }) => void;
  onDeleteWorkItem: (workItemId: string) => Promise<void> | void;
  dependency: CreateProjectTaskDependencyDto;
  setDependency: Dispatch<SetStateAction<CreateProjectTaskDependencyDto>>;
  boolValue: (value?: boolean | null) => string;
  onAddTaskDependency: () => Promise<void> | void;
  onDeleteTaskDependency: (dependencyId: string) => Promise<void> | void;
  milestone: CreateProjectMilestoneDto;
  setMilestone: Dispatch<SetStateAction<CreateProjectMilestoneDto>>;
  onAddMilestone: () => Promise<void> | void;
  onDeleteMilestone: (milestoneId: string) => Promise<void> | void;
  resource: CreateProjectResourceAllocationDto;
  setResource: Dispatch<SetStateAction<CreateProjectResourceAllocationDto>>;
  activeUsers: IdentifiedRecord[];
  formatUserLabel: (user: IdentifiedRecord) => string;
  resourceRoleOptions: string[];
  resourceRoutingPolicies: string[];
  getResolvedUserLabel: (userId?: string, displayName?: string, fallback?: string) => string;
  onAddResourceAllocation: () => Promise<void> | void;
  onApproveResourceAllocation: (resourceAllocationId: string) => Promise<void> | void;
  onDeleteResourceAllocation: (resourceAllocationId: string) => Promise<void> | void;
};

const parseTagList = (value: string) => value.split(',').map((item) => item.trim()).filter(Boolean);
const flattenProjectPhases = (phases: ProjectDetailDto['phases'], depth = 0): FlatProjectPhase[] =>
  phases.flatMap((phase) => [{ ...phase, depth }, ...flattenProjectPhases(phase.children || [], depth + 1)]);
const formatWeight = (value?: number) => `${Number(value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;
const formatWorkComponentContext = (item: Pick<ProjectWorkItemDto, 'projectPhaseName' | 'projectPackageName'>) =>
  [item.projectPhaseName, item.projectPackageName].filter(Boolean).join(' / ');

function WorkTree({
  items,
  onDelete,
  onEdit,
  editingId,
  depth = 0,
}: {
  items: ProjectWorkItemDto[];
  onDelete: (id: string) => void;
  onEdit: (item: ProjectWorkItemDto) => void;
  editingId?: string | null;
  depth?: number;
}) {
  return (
    <>
      {items.map((item) => (
        <div key={item.id} className="space-y-2">
          <div
            className={`flex items-center justify-between rounded-lg border p-4 ${editingId === item.id ? 'border-primary bg-primary/5' : ''}`}
            style={{ marginLeft: depth * 16 }}
          >
            <div>
              <div className="font-medium">{item.title}</div>
              <div className="text-xs text-muted-foreground">{item.nodeType} | {item.status} | {item.percentComplete}%</div>
              {formatWorkComponentContext(item) ? <div className="text-xs text-muted-foreground">{formatWorkComponentContext(item)}</div> : null}
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="sm" onClick={() => onEdit(item)}><Pencil className="h-4 w-4" /></Button>
              <Button variant="ghost" size="sm" onClick={() => onDelete(item.id)}><Trash2 className="h-4 w-4" /></Button>
            </div>
          </div>
          {!!item.children?.length ? <WorkTree items={item.children} onDelete={onDelete} onEdit={onEdit} editingId={editingId} depth={depth + 1} /> : null}
        </div>
      ))}
    </>
  );
}

export function ProjectPlanTab({
  project,
  flat,
  editingWorkItemId,
  work,
  setWork,
  taskStatusOptions,
  taskPriorityOptions,
  formatCatalogLabel,
  saveWorkEditor,
  resetWorkEditor,
  scheduleReasonRequired,
  taskView,
  setTaskView,
  taskBoardStatuses,
  taskBoardItems,
  timelineBounds,
  ganttSummary,
  workItemTitles,
  formatDateLabel,
  onOpenGantt,
  onBeginEditWorkItem,
  onDeleteWorkItem,
  dependency,
  setDependency,
  boolValue,
  onAddTaskDependency,
  onDeleteTaskDependency,
  milestone,
  setMilestone,
  onAddMilestone,
  onDeleteMilestone,
  resource,
  setResource,
  activeUsers,
  formatUserLabel,
  resourceRoleOptions,
  resourceRoutingPolicies,
  getResolvedUserLabel,
  onAddResourceAllocation,
  onApproveResourceAllocation,
  onDeleteResourceAllocation,
}: ProjectPlanTabProps) {
  const projectPhases = useMemo(() => flattenProjectPhases(project.phases), [project.phases]);
  const milestonePhaseIds = milestone.projectPhaseIds ?? [];
  const assignedPhaseMilestones = useMemo(
    () => new Map(project.milestones.flatMap((item) => item.phases.map((phase) => [phase.projectPhaseId, item.title] as const))),
    [project.milestones],
  );
  const selectedMilestoneWeight = useMemo(
    () => milestonePhaseIds.reduce((sum, phaseId) => sum + (projectPhases.find((phase) => phase.id === phaseId)?.completionWeightPercent ?? 0), 0),
    [milestonePhaseIds, projectPhases],
  );
  const workComponentOptions = useMemo(
    () =>
      [...project.packages].sort((left, right) =>
        (left.projectPhaseName || '').localeCompare(right.projectPhaseName || '')
        || (left.code || left.name).localeCompare(right.code || right.name),
      ),
    [project.packages],
  );

  const toggleMilestonePhase = (phaseId: string, checked: boolean) => {
    setMilestone((current) => {
      const currentIds = current.projectPhaseIds ?? [];
      const nextIds = checked
        ? Array.from(new Set([...currentIds, phaseId]))
        : currentIds.filter((item) => item !== phaseId);

      return {
        ...current,
        projectPhaseIds: projectPhases.map((phase) => phase.id).filter((id) => nextIds.includes(id)),
      };
    });
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>Work Items</CardTitle>
            {editingWorkItemId ? <Badge>Editing existing item</Badge> : null}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2"><Label>Parent</Label><Select value={work.parentId || 'root'} onValueChange={(value) => setWork((current) => ({ ...current, parentId: value === 'root' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="root">Top level</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{`${' '.repeat(item.depth * 2)}${item.title}`}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Node Type</Label><Select value={work.nodeType} onValueChange={(value) => setWork((current) => ({ ...current, nodeType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Phase', 'Workstream', 'Task', 'Subtask', 'ChecklistItem'].map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Status</Label><Select value={work.status || taskStatusOptions[0]} onValueChange={(value) => setWork((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{taskStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>% Complete</Label><Input type="number" value={work.percentComplete ?? 0} onChange={(event) => setWork((current) => ({ ...current, percentComplete: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2 md:col-span-3"><Label>Title</Label><Input value={work.title} onChange={(event) => setWork((current) => ({ ...current, title: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Priority</Label><Select value={work.priority || 'none'} onValueChange={(value) => setWork((current) => ({ ...current, priority: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select priority" /></SelectTrigger><SelectContent><SelectItem value="none">No priority</SelectItem>{taskPriorityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-2"><Label>Work Component</Label><Select value={work.projectPackageId || 'none'} onValueChange={(value) => setWork((current) => ({ ...current, projectPackageId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Optional work component" /></SelectTrigger><SelectContent><SelectItem value="none">No work component</SelectItem>{workComponentOptions.map((projectPackage) => <SelectItem key={projectPackage.id} value={projectPackage.id}>{projectPackage.projectPhaseName ? `${projectPackage.projectPhaseName} / ${projectPackage.code ? `${projectPackage.code} - ` : ''}${projectPackage.name}` : projectPackage.code ? `${projectPackage.code} - ${projectPackage.name}` : projectPackage.name}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Planned Start</Label><Input type="date" value={work.plannedStartDate ? String(work.plannedStartDate).slice(0, 10) : ''} onChange={(event) => setWork((current) => ({ ...current, plannedStartDate: event.target.value || undefined }))} /></div>
            <div className="grid gap-2"><Label>Planned End</Label><Input type="date" value={work.plannedEndDate ? String(work.plannedEndDate).slice(0, 10) : ''} onChange={(event) => setWork((current) => ({ ...current, plannedEndDate: event.target.value || undefined }))} /></div>
            <div className="flex items-end gap-2">
              {editingWorkItemId ? <Button variant="outline" onClick={resetWorkEditor}>Cancel</Button> : null}
              <Button onClick={saveWorkEditor}>
                {editingWorkItemId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}
                {editingWorkItemId ? 'Save Changes' : 'Add'}
              </Button>
            </div>
          </div>
          {scheduleReasonRequired ? (
            <div className="grid gap-2 rounded-lg border border-amber-200 bg-amber-50/70 p-4">
              <Label>Schedule Change Reason</Label>
              <Textarea
                rows={2}
                placeholder="Explain why the planned dates changed."
                value={work.scheduleChangeReason || ''}
                onChange={(event) => setWork((current) => ({ ...current, scheduleChangeReason: event.target.value }))}
              />
              <div className="text-xs text-amber-700">
                A locked baseline exists for this project, so date changes must include a reason.
              </div>
            </div>
          ) : null}
          <div className="flex flex-wrap gap-2">
            <Button variant={taskView === 'tree' ? 'default' : 'outline'} size="sm" onClick={() => setTaskView('tree')}>List</Button>
            <Button variant={taskView === 'kanban' ? 'default' : 'outline'} size="sm" onClick={() => setTaskView('kanban')}>Kanban</Button>
            <Button variant={taskView === 'timeline' ? 'default' : 'outline'} size="sm" onClick={() => setTaskView('timeline')}>Gantt</Button>
          </div>
          {taskView === 'tree' ? (
            <WorkTree
              items={project.workItems}
              editingId={editingWorkItemId}
              onEdit={onBeginEditWorkItem}
              onDelete={(workItemId) => { void onDeleteWorkItem(workItemId); }}
            />
          ) : null}
          {taskView === 'kanban' ? (
            <div className="grid gap-4 xl:grid-cols-4">
              {taskBoardStatuses.map((status) => {
                const items = taskBoardItems.filter((item) => item.status === status);
                return (
                  <div key={status} className="rounded-lg border bg-muted/20 p-3">
                    <div className="mb-3 flex items-center justify-between">
                      <div className="font-medium">{formatCatalogLabel(status)}</div>
                      <Badge variant="outline">{items.length}</Badge>
                    </div>
                    <div className="space-y-3">
                      {items.length === 0 ? <div className="text-sm text-muted-foreground">No items</div> : null}
                      {items.map((item) => (
                        <div key={item.id} className={`rounded-lg border bg-background p-4 ${editingWorkItemId === item.id ? 'border-primary bg-primary/5' : ''}`}>
                          <div className="flex items-start justify-between gap-2">
                          <div className="font-medium">{item.title}</div>
                          <Button variant="ghost" size="sm" onClick={() => onBeginEditWorkItem(item)}>
                            <Pencil className="h-4 w-4" />
                          </Button>
                        </div>
                        <div className="text-xs text-muted-foreground">{item.nodeType} | {item.percentComplete}% complete</div>
                        {formatWorkComponentContext(item) ? <div className="mt-1 text-xs text-muted-foreground">{formatWorkComponentContext(item)}</div> : null}
                        {(item.plannedStartDate || item.plannedEndDate) ? <div className="mt-2 text-xs text-muted-foreground">{formatDateLabel(item.plannedStartDate)} - {formatDateLabel(item.plannedEndDate)}</div> : null}
                      </div>
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>
          ) : null}
          {taskView === 'timeline' ? (
            timelineBounds ? (
              <div className="rounded-lg border bg-muted/10 p-6">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div className="space-y-2">
                    <div className="text-lg font-semibold">Gantt Planner</div>
                    <div className="text-sm text-muted-foreground">
                      Open the project plan in a larger workspace for the full WBS grid, chart, and export actions.
                    </div>
                    <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
                      {ganttSummary ? (
                        <>
                          <Badge variant="outline">{ganttSummary.scheduledItems} scheduled items</Badge>
                          <Badge variant="outline">{ganttSummary.spanDays} day span</Badge>
                          <Badge variant="outline">{ganttSummary.phases} phases</Badge>
                          <Badge variant={ganttSummary.overdueItems > 0 ? 'destructive' : 'secondary'}>{ganttSummary.overdueItems} overdue</Badge>
                        </>
                      ) : null}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button onClick={onOpenGantt}>
                      <Maximize2 className="mr-2 h-4 w-4" />
                      Open Gantt
                    </Button>
                  </div>
                </div>
              </div>
            ) : (
              <div className="text-sm text-muted-foreground">Add planned start and end dates to work items to build the Gantt view.</div>
            )
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Task Dependencies</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2"><Label>Predecessor</Label><Select value={dependency.predecessorWorkItemId || 'none'} onValueChange={(value) => setDependency((current) => ({ ...current, predecessorWorkItemId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select work item</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Successor</Label><Select value={dependency.successorWorkItemId || 'none'} onValueChange={(value) => setDependency((current) => ({ ...current, successorWorkItemId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select work item</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Type</Label><Select value={dependency.dependencyType || 'FS'} onValueChange={(value) => setDependency((current) => ({ ...current, dependencyType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['FS', 'SS', 'FF', 'SF'].map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Lag Days</Label><Input type="number" value={dependency.lagDays ?? 0} onChange={(event) => setDependency((current) => ({ ...current, lagDays: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Enforced</Label><Select value={boolValue(dependency.isEnforced)} onValueChange={(value) => setDependency((current) => ({ ...current, isEnforced: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="true">Enforced</SelectItem><SelectItem value="false">Warning only</SelectItem></SelectContent></Select></div>
            <div className="flex items-end"><Button disabled={!dependency.predecessorWorkItemId || !dependency.successorWorkItemId || dependency.predecessorWorkItemId === dependency.successorWorkItemId} onClick={onAddTaskDependency}><Plus className="mr-2 h-4 w-4" />Add Dependency</Button></div>
          </div>
          <div className="space-y-3">
            {project.taskDependencies.length === 0 ? <div className="text-sm text-muted-foreground">No task dependencies are configured.</div> : null}
            {project.taskDependencies.map((item) => <div key={item.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{workItemTitles.get(item.predecessorWorkItemId) || item.predecessorWorkItemId}{' -> '}{workItemTitles.get(item.successorWorkItemId) || item.successorWorkItemId}</div><div className="text-sm text-muted-foreground">{item.dependencyType} | lag {item.lagDays} day(s) | {item.isEnforced ? 'Enforced' : 'Warning only'}</div></div><Button variant="ghost" size="sm" onClick={() => onDeleteTaskDependency(item.id)}><Trash2 className="h-4 w-4" /></Button></div>)}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Milestones</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={milestone.title} onChange={(event) => setMilestone((current) => ({ ...current, title: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Target Date</Label><Input type="date" value={milestone.targetDate ? String(milestone.targetDate).slice(0, 10) : ''} onChange={(event) => setMilestone((current) => ({ ...current, targetDate: event.target.value }))} /></div>
            <div className="flex items-end"><Button onClick={onAddMilestone}><Plus className="mr-2 h-4 w-4" />Add</Button></div>
          </div>
          {projectPhases.length > 0 ? (
            <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <div className="text-sm font-semibold text-slate-900">Milestone Deliverable Phases</div>
                  <div className="text-xs text-slate-500">Select the phases this milestone will represent. Each phase can only belong to one milestone.</div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline">{milestonePhaseIds.length} selected</Badge>
                  <Badge variant="outline" className="border-blue-200 bg-blue-50 text-blue-700">Total Weight {formatWeight(selectedMilestoneWeight)}</Badge>
                </div>
              </div>
              <div className="mt-4 grid gap-2 xl:grid-cols-2">
                {projectPhases.map((phase) => {
                  const isChecked = milestonePhaseIds.includes(phase.id);
                  const assignedMilestone = assignedPhaseMilestones.get(phase.id);
                  const isDisabled = Boolean(assignedMilestone) && !isChecked;

                  return (
                    <label key={phase.id} className={`rounded-lg border px-4 py-3 text-sm ${isDisabled ? 'border-amber-200 bg-amber-50/70' : 'border-slate-200 bg-white'}`}>
                      <div className="flex items-start gap-3">
                        <Checkbox
                          checked={isChecked}
                          disabled={isDisabled}
                          onCheckedChange={(checked) => toggleMilestonePhase(phase.id, Boolean(checked))}
                        />
                        <div className="min-w-0 flex-1" style={{ paddingLeft: `${phase.depth * 0.75}rem` }}>
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-medium text-slate-900">{phase.name}</span>
                            {phase.code ? <Badge variant="outline">{phase.code}</Badge> : null}
                            <Badge variant="outline" className="border-blue-200 bg-blue-50 text-blue-700">
                              {formatWeight(phase.completionWeightPercent)}
                            </Badge>
                          </div>
                          <div className="mt-1 text-xs text-slate-500">
                            {phase.plannedStartDate || phase.plannedEndDate
                              ? `${formatDateLabel(phase.plannedStartDate)} - ${formatDateLabel(phase.plannedEndDate)}`
                              : 'No phase schedule dates set yet'}
                          </div>
                          {assignedMilestone && !isChecked ? (
                            <div className="mt-1 text-xs text-amber-700">Already assigned to milestone {assignedMilestone}.</div>
                          ) : null}
                        </div>
                      </div>
                    </label>
                  );
                })}
              </div>
            </div>
          ) : (
            <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
              Add project phases first to define milestone deliverables from phase checklists.
            </div>
          )}
          {project.milestones.map((item) => (
            <div key={item.id} className="flex items-start justify-between rounded-lg border p-4">
              <div className="space-y-2">
                <div className="font-medium">{item.title}</div>
                <div className="text-sm text-muted-foreground">{formatDateLabel(item.targetDate)} | {item.status}</div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline" className="border-blue-200 bg-blue-50 text-blue-700">
                    Weight {formatWeight(item.totalWeightPercent)}
                  </Badge>
                  {item.phases.map((phase) => (
                    <Badge key={`${item.id}-${phase.projectPhaseId}`} variant="secondary">
                      {phase.phaseCode ? `${phase.phaseCode} - ${phase.phaseName}` : phase.phaseName}
                    </Badge>
                  ))}
                  {item.phases.length === 0 ? <span className="text-xs text-muted-foreground">No phases linked</span> : null}
                </div>
              </div>
              <Button variant="ghost" size="sm" onClick={() => onDeleteMilestone(item.id)}><Trash2 className="h-4 w-4" /></Button>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Resource Allocations</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2">
              <Label>User</Label>
              <Select value={resource.userId || 'none'} onValueChange={(value) => setResource((current) => ({ ...current, userId: value === 'none' ? '' : value }))}>
                <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No user</SelectItem>
                  {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2"><Label>Role</Label><Select value={resource.allocationRole} onValueChange={(value) => setResource((current) => ({ ...current, allocationRole: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{resourceRoleOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Type</Label><Select value={resource.allocationType} onValueChange={(value) => setResource((current) => ({ ...current, allocationType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Hours', 'Percent'].map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Value</Label><Input type="number" value={resource.allocationValue} onChange={(event) => setResource((current) => ({ ...current, allocationValue: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Work Item</Label><Select value={resource.workItemId || 'none'} onValueChange={(value) => setResource((current) => ({ ...current, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Project level</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Start Date</Label><Input type="date" value={String(resource.startDate).slice(0, 10)} onChange={(event) => setResource((current) => ({ ...current, startDate: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>End Date</Label><Input type="date" value={String(resource.endDate).slice(0, 10)} onChange={(event) => setResource((current) => ({ ...current, endDate: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Routing Policy</Label><Select value={resource.routingPolicy || resourceRoutingPolicies[0]} onValueChange={(value) => setResource((current) => ({ ...current, routingPolicy: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{resourceRoutingPolicies.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-2"><Label>Required Skills</Label><Textarea rows={2} value={(resource.requiredSkills || []).join(', ')} onChange={(event) => setResource((current) => ({ ...current, requiredSkills: parseTagList(event.target.value) }))} placeholder="e.g. Electrical Design, Primavera P6" /></div>
            <div className="grid gap-2 md:col-span-2"><Label>Required Certifications</Label><Textarea rows={2} value={(resource.requiredCertifications || []).join(', ')} onChange={(event) => setResource((current) => ({ ...current, requiredCertifications: parseTagList(event.target.value) }))} placeholder="e.g. PMP, OSHA 30" /></div>
            <div className="flex items-end"><Button onClick={onAddResourceAllocation}><Plus className="mr-2 h-4 w-4" />Add Allocation</Button></div>
          </div>
          {project.resourceAllocations.map((item) => <div key={item.id} className="rounded-lg border p-4"><div className="flex items-start justify-between gap-3"><div><div className="font-medium">{getResolvedUserLabel(item.userId, item.userDisplayName)} | {item.allocationRole}</div><div className="text-sm text-muted-foreground">{formatDateLabel(item.startDate)} - {formatDateLabel(item.endDate)} | {item.allocationValue} {item.allocationType} | {item.capacityUtilizationPercent}% capacity | {item.qualificationMatchPercent}% fit</div><div className="mt-2 flex flex-wrap gap-2"><Badge variant="outline">{formatCatalogLabel(item.routingPolicy)}</Badge><Badge variant={item.qualificationRisk === 'Critical' ? 'destructive' : item.qualificationRisk === 'High' ? 'secondary' : 'outline'}>{item.qualificationRisk}</Badge>{item.hasConflict ? <Badge variant="secondary">Capacity conflict</Badge> : null}{item.requiredSkills.map((skill) => <Badge key={`${item.id}-skill-${skill}`} variant="outline">{skill}</Badge>)}{item.requiredCertifications.map((certification) => <Badge key={`${item.id}-cert-${certification}`} variant="outline">Cert: {certification}</Badge>)}</div>{item.missingSkills.length ? <div className="mt-2 text-sm text-amber-700">Missing skills: {item.missingSkills.join(', ')}</div> : null}{item.missingCertifications.length ? <div className="mt-1 text-sm text-red-700">Missing certifications: {item.missingCertifications.join(', ')}</div> : null}{item.routingRecommendation ? <div className="mt-2 text-sm text-muted-foreground">{item.routingRecommendation}</div> : null}{item.recommendedUserDisplayName ? <div className="mt-1 text-sm text-muted-foreground">Suggested backup: {item.recommendedUserDisplayName}</div> : null}</div><div className="flex gap-2">{item.status !== 'Approved' ? <Button variant="outline" size="sm" onClick={() => onApproveResourceAllocation(item.id)}>Approve</Button> : null}<Button variant="ghost" size="sm" onClick={() => onDeleteResourceAllocation(item.id)}><Trash2 className="h-4 w-4" /></Button></div></div></div>)}
        </CardContent>
      </Card>
    </div>
  );
}
