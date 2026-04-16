import { useState } from 'react';
import { CalendarDays, Flag, PencilLine, PlayCircle, Plus, Save, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type { CreateProjectPhaseDto, ProjectPhaseDto, ProjectPhaseGateEvaluationDto } from '@/services/projectService';

const PHASE_STATUS_OPTIONS = [
  { value: 'NotStarted', label: 'Not Started' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Blocked', label: 'Blocked' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Waived', label: 'Waived' },
  { value: 'Cancelled', label: 'Cancelled' },
];

type FlattenedPhase = ProjectPhaseDto & { depth: number };

type ProjectPhasesTabProps = {
  phases: ProjectPhaseDto[];
  phaseGateEvaluations: ProjectPhaseGateEvaluationDto[];
  phaseDraft: CreateProjectPhaseDto;
  editingPhaseId: string | null;
  onPhaseDraftChange: (draft: CreateProjectPhaseDto) => void;
  onSavePhase: () => Promise<boolean> | boolean;
  onDeletePhase: (phaseId: string) => void;
  onEditPhase: (phase: ProjectPhaseDto) => void;
  onAdvancePhase: (phase: ProjectPhaseDto, evaluation?: ProjectPhaseGateEvaluationDto) => void;
  onCancelEdit: () => void;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): FlattenedPhase[] =>
  phases.flatMap((phase) => [{ ...phase, depth }, ...flattenPhases(phase.children ?? [], depth + 1)]);

const formatDate = (value?: string) => {
  if (!value) return 'Not set';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? 'Not set' : parsed.toLocaleDateString();
};

const formatWeight = (value?: number) => `${Number(value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;

const getAdvanceLabel = (phaseStatus: string, blockingFailureCount: number) => {
  if (phaseStatus === 'InProgress') {
    return blockingFailureCount > 0 ? 'Override' : 'Complete';
  }

  return 'Start';
};

export function ProjectPhasesTab({
  phases,
  phaseGateEvaluations,
  phaseDraft,
  editingPhaseId,
  onPhaseDraftChange,
  onSavePhase,
  onDeletePhase,
  onEditPhase,
  onAdvancePhase,
  onCancelEdit,
}: ProjectPhasesTabProps) {
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const flattened = flattenPhases(phases);
  const completedCount = flattened.filter((phase) => phase.status === 'Completed').length;
  const stageGateCount = flattened.filter((phase) => phase.isStageGateRequired).length;
  const evaluationMap = new Map(phaseGateEvaluations.map((evaluation) => [evaluation.projectPhaseId, evaluation]));
  const readyGateCount = phaseGateEvaluations.filter((evaluation) => evaluation.isStageGateRequired && evaluation.isReady).length;
  const blockedGateCount = phaseGateEvaluations.filter((evaluation) => evaluation.blockingFailureCount > 0).length;
  const gateSetupCount = phaseGateEvaluations.filter((evaluation) => evaluation.isStageGateRequired && !evaluation.hasConfiguredRules).length;

  const handleSaveAddPhase = async () => {
    const saved = await Promise.resolve(onSavePhase());
    if (saved) {
      setIsAddDialogOpen(false);
    }
  };

  const renderPhaseForm = () => (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="phase-name">Phase Name</Label>
          <Input
            id="phase-name"
            value={phaseDraft.name ?? ''}
            onChange={(event) => onPhaseDraftChange({ ...phaseDraft, name: event.target.value })}
            placeholder="Detailed Design"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="phase-code">Code</Label>
          <Input
            id="phase-code"
            value={phaseDraft.code ?? ''}
            onChange={(event) => onPhaseDraftChange({ ...phaseDraft, code: event.target.value })}
            placeholder="DD"
          />
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="phase-description">Description</Label>
        <Textarea
          id="phase-description"
          value={phaseDraft.description ?? ''}
          onChange={(event) => onPhaseDraftChange({ ...phaseDraft, description: event.target.value })}
          placeholder="Describe what must be completed before the next stage can begin."
          rows={4}
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label>Status</Label>
          <Select
            value={phaseDraft.status ?? 'NotStarted'}
            onValueChange={(value) => onPhaseDraftChange({ ...phaseDraft, status: value })}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PHASE_STATUS_OPTIONS.map((status) => (
                <SelectItem key={status.value} value={status.value}>
                  {status.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="phase-sort-order">Sort Order</Label>
          <Input
            id="phase-sort-order"
            type="number"
            min={0}
            value={phaseDraft.sortOrder ?? 0}
            onChange={(event) =>
              onPhaseDraftChange({
                ...phaseDraft,
                sortOrder: Number.isFinite(event.target.valueAsNumber) ? event.target.valueAsNumber : 0,
              })
            }
          />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="phase-start-date">Planned Start</Label>
          <Input
            id="phase-start-date"
            type="date"
            value={phaseDraft.plannedStartDate?.slice(0, 10) ?? ''}
            onChange={(event) => onPhaseDraftChange({ ...phaseDraft, plannedStartDate: event.target.value || undefined })}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="phase-end-date">Planned End</Label>
          <Input
            id="phase-end-date"
            type="date"
            value={phaseDraft.plannedEndDate?.slice(0, 10) ?? ''}
            onChange={(event) => onPhaseDraftChange({ ...phaseDraft, plannedEndDate: event.target.value || undefined })}
          />
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="phase-completion-weight">Phase Weight (%)</Label>
        <Input
          id="phase-completion-weight"
          type="number"
          value={phaseDraft.completionWeightPercent ?? 0}
          readOnly
          className="bg-slate-50 text-slate-700"
        />
        <div className="text-xs text-slate-500">
          This value is inherited from the selected project template. Update the template if this phase needs a different weight.
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
          <Checkbox
            checked={phaseDraft.isOptional ?? false}
            onCheckedChange={(checked) => onPhaseDraftChange({ ...phaseDraft, isOptional: Boolean(checked) })}
          />
          <span>
            <span className="block font-medium text-slate-900">Optional Phase</span>
            <span className="block text-slate-500">Mark phases that may not apply on every project.</span>
          </span>
        </label>
        <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
          <Checkbox
            checked={phaseDraft.isStageGateRequired ?? false}
            onCheckedChange={(checked) => onPhaseDraftChange({ ...phaseDraft, isStageGateRequired: Boolean(checked) })}
          />
          <span>
            <span className="block font-medium text-slate-900">Stage Gate Required</span>
            <span className="block text-slate-500">Require a formal review before the next phase can proceed.</span>
          </span>
        </label>
      </div>
    </div>
  );

  return (
    <div className="space-y-6">
      <Card className="border-slate-200/70 shadow-sm">
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="space-y-1">
              <CardTitle className="text-base">Project Phases</CardTitle>
              <CardDescription>
                Manage the real construction lifecycle here so feasibility, design, approvals, procurement, and handover are visible.
              </CardDescription>
            </div>
            <Button onClick={() => setIsAddDialogOpen(true)} className="gap-2">
              <Plus className="h-4 w-4" />
              Add Phase
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
            <div className="rounded-xl border border-slate-200 bg-slate-50/80 px-4 py-3">
              <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Total Phases</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{flattened.length}</div>
            </div>
            <div className="rounded-xl border border-emerald-200 bg-emerald-50/80 px-4 py-3">
              <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-emerald-700">Completed</div>
              <div className="mt-2 text-2xl font-semibold text-emerald-900">{completedCount}</div>
            </div>
            <div className="rounded-xl border border-amber-200 bg-amber-50/80 px-4 py-3">
              <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">Stage Gates</div>
              <div className="mt-2 text-2xl font-semibold text-amber-900">{stageGateCount}</div>
            </div>
            <div className="rounded-xl border border-emerald-200 bg-emerald-50/80 px-4 py-3">
              <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-emerald-700">Gate Ready</div>
              <div className="mt-2 text-2xl font-semibold text-emerald-900">{readyGateCount}</div>
            </div>
            <div className="rounded-xl border border-rose-200 bg-rose-50/80 px-4 py-3">
              <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-rose-700">Blocked</div>
              <div className="mt-2 text-2xl font-semibold text-rose-900">{blockedGateCount}</div>
              {gateSetupCount > 0 ? (
                <div className="mt-1 text-[11px] text-rose-700">{gateSetupCount} need gate setup</div>
              ) : null}
            </div>
          </div>

          {flattened.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 px-4 py-6 text-sm text-slate-600">
              No project phases are configured yet. Add the first lifecycle phase to make the construction flow visible in the workspace.
            </div>
          ) : (
            <ScrollArea className="h-[420px] rounded-xl border border-slate-200">
              <div className="divide-y divide-slate-200">
                {flattened.map((phase) => {
                  const evaluation = evaluationMap.get(phase.id);
                  const failedRequirements = evaluation?.requirementResults.filter((result) => result.isBlocking && !result.isSatisfied) ?? [];
                  const passedRequirements = evaluation?.requirementResults.filter((result) => result.isSatisfied) ?? [];
                  const canAdvance = !['Completed', 'Waived', 'Cancelled'].includes(phase.status);
                  const advanceLabel = getAdvanceLabel(phase.status, failedRequirements.length);

                  return (
                  <div key={phase.id} className="flex flex-col gap-2 px-3 py-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span className="text-sm font-semibold text-slate-900" style={{ marginLeft: `${phase.depth * 18}px` }}>
                          {phase.name}
                        </span>
                        {phase.code ? (
                          <Badge variant="outline" className="border-slate-300 px-1.5 py-0 text-[10px] uppercase tracking-[0.14em]">
                            {phase.code}
                          </Badge>
                        ) : null}
                        <Badge variant="secondary" className="px-1.5 py-0 text-[10px]">
                          {phase.status}
                        </Badge>
                        <Badge variant="outline" className="border-blue-200 bg-blue-50 px-1.5 py-0 text-[10px] text-blue-700">
                          Weight {formatWeight(phase.completionWeightPercent)}
                        </Badge>
                        {phase.isOptional ? (
                          <Badge variant="outline" className="border-slate-300 px-1.5 py-0 text-[10px]">
                            Optional
                          </Badge>
                        ) : null}
                        {phase.isStageGateRequired ? (
                          <Badge className="bg-amber-100 px-1.5 py-0 text-[10px] text-amber-900 hover:bg-amber-100">
                            Stage Gate
                          </Badge>
                        ) : null}
                        {phase.isStageGateRequired && evaluation?.isReady ? (
                          <Badge className="bg-emerald-100 px-1.5 py-0 text-[10px] text-emerald-900 hover:bg-emerald-100">
                            Ready
                          </Badge>
                        ) : null}
                        {phase.isStageGateRequired && evaluation && !evaluation.hasConfiguredRules ? (
                          <Badge className="bg-slate-100 px-1.5 py-0 text-[10px] text-slate-800 hover:bg-slate-100">
                            Needs Gate Setup
                          </Badge>
                        ) : null}
                        {evaluation && evaluation.blockingFailureCount > 0 ? (
                          <Badge className="bg-rose-100 px-1.5 py-0 text-[10px] text-rose-900 hover:bg-rose-100">
                            {evaluation.blockingFailureCount} Blocked
                          </Badge>
                        ) : null}
                      </div>
                      {phase.description ? (
                        <p className="mt-1.5 text-sm text-slate-600" style={{ marginLeft: `${phase.depth * 18}px` }}>
                          {phase.description}
                        </p>
                      ) : null}
                      <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-slate-500" style={{ marginLeft: `${phase.depth * 18}px` }}>
                        <span className="inline-flex items-center gap-1">
                          <CalendarDays className="h-3.5 w-3.5" />
                          {formatDate(phase.plannedStartDate)} - {formatDate(phase.plannedEndDate)}
                        </span>
                        <span className="inline-flex items-center gap-1">
                          <Flag className="h-3.5 w-3.5" />
                          Order {phase.sortOrder}
                        </span>
                      </div>
                      {evaluation ? (
                        <div className="mt-2 space-y-2" style={{ marginLeft: `${phase.depth * 18}px` }}>
                          {failedRequirements.length > 0 ? (
                            <div className="space-y-1 rounded-xl border border-rose-200 bg-rose-50/80 px-3 py-2">
                              <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-rose-700">Blocking Requirements</div>
                              {failedRequirements.map((result) => (
                                <div key={result.stageGateRuleId} className="text-xs text-rose-900">
                                  {result.ruleName}: {result.message || `${result.actualCount} recorded`}
                                </div>
                              ))}
                            </div>
                          ) : null}
                          {failedRequirements.length === 0 && passedRequirements.length > 0 ? (
                            <div className="rounded-xl border border-emerald-200 bg-emerald-50/70 px-3 py-2 text-xs text-emerald-900">
                              Gate checks satisfied for this phase.
                            </div>
                          ) : null}
                        </div>
                      ) : null}
                    </div>
                    <div className="flex shrink-0 items-center gap-1.5 self-start">
                      {canAdvance ? (
                        <Button size="sm" onClick={() => onAdvancePhase(phase, evaluation)} className="h-8 gap-1.5 px-2.5">
                          <PlayCircle className="h-4 w-4" />
                          {advanceLabel}
                        </Button>
                      ) : null}
                      <Button
                        variant="outline"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => onEditPhase(phase)}
                        aria-label={`Edit ${phase.name}`}
                        title={`Edit ${phase.name}`}
                      >
                        <PencilLine className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => onDeletePhase(phase.id)}
                        aria-label={`Delete ${phase.name}`}
                        title={`Delete ${phase.name}`}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                  );
                })}
              </div>
            </ScrollArea>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={isAddDialogOpen}
        onOpenChange={(open) => {
          setIsAddDialogOpen(open);
          if (!open) {
            onCancelEdit();
          }
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add Phase</DialogTitle>
            <DialogDescription>
              Set up lifecycle stages the way this project actually runs. Phase completion weights stay controlled from the template and are shown here as inherited values.
            </DialogDescription>
          </DialogHeader>

          {renderPhaseForm()}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setIsAddDialogOpen(false);
                onCancelEdit();
              }}
            >
              Cancel
            </Button>
            <Button onClick={handleSaveAddPhase} className="gap-2">
              <Plus className="h-4 w-4" />
              Add Phase
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(editingPhaseId)} onOpenChange={(open) => !open && onCancelEdit()}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Phase</DialogTitle>
            <DialogDescription>
              Update schedule and status details here. Phase completion weights remain template-controlled and read-only in the project.
            </DialogDescription>
          </DialogHeader>

          {renderPhaseForm()}

          <DialogFooter>
            <Button variant="outline" onClick={onCancelEdit}>
              Cancel
            </Button>
            <Button onClick={onSavePhase} className="gap-2">
              <Save className="h-4 w-4" />
              Save Phase
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
