'use client';

import type { Dispatch, SetStateAction } from 'react';
import { Save } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  type CreateProjectWorkItemDto,
  type ProjectGovernanceSummaryDto,
  type ProjectWorkItemDto,
} from '@/services/projectService';
import type { User } from '@/types';

type PlannerEditableWorkItem = ProjectWorkItemDto & { depth: number; outline: string };

type ProjectGanttPlannerEditorProps = {
  editingWorkItemId: string | null;
  governanceSummary: ProjectGovernanceSummaryDto | null;
  plannerEditingItem: PlannerEditableWorkItem | null;
  resetWorkEditor: () => void;
  saveWorkEditor: () => void;
  work: CreateProjectWorkItemDto;
  setWork: Dispatch<SetStateAction<CreateProjectWorkItemDto>>;
  taskStatusOptions: string[];
  formatCatalogLabel: (value: string) => string;
  activeUsers: User[];
  formatUserLabel: (user: User) => string;
  scheduleReasonRequired: boolean;
};

export function ProjectGanttPlannerEditor({
  editingWorkItemId,
  governanceSummary,
  plannerEditingItem,
  resetWorkEditor,
  saveWorkEditor,
  work,
  setWork,
  taskStatusOptions,
  formatCatalogLabel,
  activeUsers,
  formatUserLabel,
  scheduleReasonRequired,
}: ProjectGanttPlannerEditorProps) {
  if (!editingWorkItemId) {
    return null;
  }

  return (
    <div className="border-b bg-muted/20 px-6 py-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="text-sm font-semibold">Planner Editor</div>
            {governanceSummary?.hasLockedBaseline ? <Badge variant="secondary">Baseline locked</Badge> : null}
          </div>
          <div className="text-xs text-muted-foreground">
            {plannerEditingItem ? `${plannerEditingItem.outline} · ${plannerEditingItem.title}` : 'Editing selected work item'}
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={resetWorkEditor}>
            Cancel
          </Button>
          <Button size="sm" onClick={saveWorkEditor}>
            <Save className="mr-2 h-4 w-4" />
            Save Changes
          </Button>
        </div>
      </div>
      <div className="grid gap-3 md:grid-cols-6 xl:grid-cols-8">
        <div className="grid gap-2 md:col-span-2">
          <Label>Title</Label>
          <Input value={work.title} onChange={(e) => setWork((p) => ({ ...p, title: e.target.value }))} />
        </div>
        <div className="grid gap-2">
          <Label>Status</Label>
          <Select value={work.status || taskStatusOptions[0]} onValueChange={(value) => setWork((p) => ({ ...p, status: value }))}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {taskStatusOptions.map((item) => (
                <SelectItem key={item} value={item}>
                  {formatCatalogLabel(item)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2">
          <Label>Owner</Label>
          <Select value={work.assignedToUserId || 'none'} onValueChange={(value) => setWork((p) => ({ ...p, assignedToUserId: value === 'none' ? undefined : value }))}>
            <SelectTrigger>
              <SelectValue placeholder="Select owner" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">Unassigned</SelectItem>
              {activeUsers.map((user) => (
                <SelectItem key={user.id} value={user.id}>
                  {formatUserLabel(user)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2">
          <Label>Start</Label>
          <Input type="date" value={work.plannedStartDate ? String(work.plannedStartDate).slice(0, 10) : ''} onChange={(e) => setWork((p) => ({ ...p, plannedStartDate: e.target.value || undefined }))} />
          {governanceSummary?.hasLockedBaseline ? <div className="text-[11px] text-muted-foreground">Changing this date requires a reason.</div> : null}
        </div>
        <div className="grid gap-2">
          <Label>End</Label>
          <Input type="date" value={work.plannedEndDate ? String(work.plannedEndDate).slice(0, 10) : ''} onChange={(e) => setWork((p) => ({ ...p, plannedEndDate: e.target.value || undefined }))} />
          {governanceSummary?.hasLockedBaseline ? <div className="text-[11px] text-muted-foreground">Changing this date requires a reason.</div> : null}
        </div>
        <div className="grid gap-2">
          <Label>% Complete</Label>
          <Input type="number" value={work.percentComplete ?? 0} onChange={(e) => setWork((p) => ({ ...p, percentComplete: Number(e.target.value || '0') }))} />
        </div>
        <div className="grid gap-2">
          <Label>Track</Label>
          <Input type="number" value={work.effortEstimateHours ?? 0} onChange={(e) => setWork((p) => ({ ...p, effortEstimateHours: Number(e.target.value || '0') }))} />
        </div>
      </div>
      {scheduleReasonRequired ? (
        <div className="mt-3 grid gap-2">
          <Label>Schedule Change Reason</Label>
          <Textarea
            rows={2}
            placeholder="Explain why the planned dates changed."
            value={work.scheduleChangeReason || ''}
            onChange={(e) => setWork((p) => ({ ...p, scheduleChangeReason: e.target.value }))}
          />
          <div className="text-xs text-amber-700">A locked baseline exists for this project, so date changes must include a reason.</div>
        </div>
      ) : null}
    </div>
  );
}
