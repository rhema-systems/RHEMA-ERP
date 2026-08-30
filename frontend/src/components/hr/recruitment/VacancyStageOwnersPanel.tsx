'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, Pencil, SkipForward, Trash2, UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';
import { recruitmentPipelineService } from '@/services/hr/recruitment-pipeline.service';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import type { VacancyStageAssignment } from '@/types/hr/recruitment';

interface AssignmentDraft {
  pipelineStageId: string;
  stageName: string;
  /** Set when editing an existing assignment (PUT); absent = new assignment (upsert POST). */
  assignmentId?: string;
  assignedToId: string;
  assignedToName?: string | null;
  dueDate: string;
  escalationEnabled: boolean;
  escalationDaysAfterDue: string;
  escalateToId: string;
  escalateToName?: string | null;
}

/**
 * Who owns each pipeline stage of THIS vacancy — assign, reassign, mark done or skipped.
 *
 * ⚠ Not the application board (`/pipeline`), which moves applications between stages. This
 * panel answers "whose desk is shortlisting on, and is it overdue", per vacancy.
 *
 * Complete and skip are deliberately shown to non-HR too: the server allows exactly the
 * assignee, the assigner or HR, per record — a hiring manager completes their own stage here.
 */
export function VacancyStageOwnersPanel({
  vacancyId,
  pipelineId,
  canManage,
}: {
  vacancyId: string;
  pipelineId?: string | null;
  canManage: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [draft, setDraft] = useState<AssignmentDraft | null>(null);
  const [closing, setClosing] = useState<{ id: string; mode: 'complete' | 'skip'; stageName: string } | null>(null);
  const [closingNotes, setClosingNotes] = useState('');

  const stages = useQuery({
    queryKey: ['hr', 'pipeline-stages', pipelineId],
    queryFn: () => recruitmentPipelineService.getStages(pipelineId ?? ''),
    enabled: !!pipelineId,
  });

  const assignments = useQuery({
    queryKey: ['hr', 'vacancy-stage-assignments', vacancyId],
    queryFn: () => jobVacancyService.getStageAssignments(vacancyId),
    enabled: !!vacancyId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-stage-assignments', vacancyId] });

  const byStage = useMemo(() => {
    const map = new Map<string, VacancyStageAssignment>();
    for (const a of assignments.data ?? []) map.set(a.pipelineStageId, a);
    return map;
  }, [assignments.data]);

  const save = useMutation({
    mutationFn: (d: AssignmentDraft) => {
      const payload = {
        assignedToId: d.assignedToId,
        dueDate: d.dueDate || null,
        escalationEnabled: d.escalationEnabled,
        escalationDaysAfterDue: d.escalationEnabled && d.escalationDaysAfterDue !== ''
          ? Number(d.escalationDaysAfterDue)
          : null,
        escalateToId: d.escalationEnabled && d.escalateToId ? d.escalateToId : null,
      };
      return d.assignmentId
        ? jobVacancyService.updateStageAssignment(d.assignmentId, payload)
        : jobVacancyService.upsertStageAssignment(vacancyId, d.pipelineStageId, payload);
    },
    onSuccess: async () => {
      await refresh();
      setDraft(null);
      toast({ title: 'Stage owner saved' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not save the assignment', description: e?.message, variant: 'destructive' }),
  });

  const close = useMutation({
    mutationFn: ({ id, mode }: { id: string; mode: 'complete' | 'skip' }) =>
      mode === 'complete'
        ? jobVacancyService.completeStageAssignment(id, closingNotes.trim() || null)
        : jobVacancyService.skipStageAssignment(id, closingNotes.trim() || null),
    onSuccess: async (_r, v) => {
      await refresh();
      setClosing(null);
      setClosingNotes('');
      toast({ title: v.mode === 'complete' ? 'Stage marked complete' : 'Stage skipped' });
    },
    onError: (e: any) =>
      toast({
        title: 'Not allowed',
        description: e?.message ?? 'Only the assignee, the assigner or HR can close a stage.',
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => jobVacancyService.deleteStageAssignment(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Assignment removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  if (!pipelineId) {
    return (
      <EmptyState
        title="No pipeline on this vacancy"
        description="Stage owners hang off the pipeline; assign a pipeline to the vacancy first."
      />
    );
  }

  const openAssign = (stageId: string, stageName: string, existing?: VacancyStageAssignment) =>
    setDraft({
      pipelineStageId: stageId,
      stageName,
      assignmentId: existing?.id,
      assignedToId: existing?.assignedToId ?? '',
      assignedToName: existing?.assignedToName ?? null,
      dueDate: existing?.dueDate ? existing.dueDate.slice(0, 10) : '',
      escalationEnabled: existing?.escalationEnabled ?? false,
      escalationDaysAfterDue: existing?.escalationDaysAfterDue != null ? String(existing.escalationDaysAfterDue) : '',
      escalateToId: existing?.escalateToId ?? '',
      escalateToName: existing?.escalateToName ?? null,
    });

  const loading = stages.isLoading || assignments.isLoading;
  const orderedStages = [...(stages.data ?? [])].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Stage owners</CardTitle>
        <CardDescription>
          Who is responsible for each stage of this vacancy&apos;s pipeline, and by when.
        </CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        {loading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : orderedStages.length === 0 ? (
          <div className="py-10">
            <EmptyState title="The pipeline has no stages" description="Add stages to the pipeline first." />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Stage</TableHead>
                <TableHead>Owner</TableHead>
                <TableHead className="w-[120px]">Due</TableHead>
                <TableHead className="w-[130px]">Status</TableHead>
                <TableHead className="w-[190px]">Closed</TableHead>
                <TableHead className="w-[150px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {orderedStages.map((stage) => {
                const a = byStage.get(stage.id);
                const open = a && (a.status === 'NotStarted' || a.status === 'InProgress' || a.status === 'Overdue' || a.status === 'Escalated');
                return (
                  <TableRow key={stage.id}>
                    <TableCell>
                      <div className="font-medium">{stage.name}</div>
                    </TableCell>
                    <TableCell>
                      {a ? (
                        <div>
                          <div className="text-sm">{a.assignedToName}</div>
                          <div className="text-xs text-muted-foreground">
                            by {a.assignedByName} · {formatDate(a.assignedAt)}
                          </div>
                        </div>
                      ) : (
                        <span className="text-sm text-muted-foreground">Unassigned</span>
                      )}
                    </TableCell>
                    <TableCell className="text-sm">{a?.dueDate ? formatDate(a.dueDate) : '—'}</TableCell>
                    <TableCell>{a ? <StatusBadge status={a.status} /> : '—'}</TableCell>
                    <TableCell className="text-sm">
                      {a?.completedAt ? (
                        <div>
                          {formatDateTime(a.completedAt)}
                          {a.completedByName && (
                            <div className="text-xs text-muted-foreground">by {a.completedByName}</div>
                          )}
                        </div>
                      ) : a?.completionNotes && a.status === 'Skipped' ? (
                        <span className="text-xs text-muted-foreground">{a.completionNotes}</span>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-0.5">
                        {open && (
                          <>
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Complete ${stage.name}`}
                              title="Mark complete"
                              onClick={() => a && setClosing({ id: a.id, mode: 'complete', stageName: stage.name })}
                            >
                              <CheckCircle2 className="h-4 w-4 text-green-600" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Skip ${stage.name}`}
                              title="Skip"
                              onClick={() => a && setClosing({ id: a.id, mode: 'skip', stageName: stage.name })}
                            >
                              <SkipForward className="h-4 w-4" />
                            </Button>
                          </>
                        )}
                        {canManage && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={a ? `Reassign ${stage.name}` : `Assign ${stage.name}`}
                            title={a ? 'Edit assignment' : 'Assign an owner'}
                            onClick={() => openAssign(stage.id, stage.name, a)}
                          >
                            {a ? <Pencil className="h-4 w-4" /> : <UserPlus className="h-4 w-4" />}
                          </Button>
                        )}
                        {canManage && a && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove the ${stage.name} assignment`}
                            title="Remove assignment"
                            onClick={() => remove.mutate(a.id)}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>

      {/* assign / reassign */}
      <Dialog open={!!draft} onOpenChange={(o) => !o && setDraft(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{draft?.assignmentId ? 'Edit stage owner' : 'Assign stage owner'}</DialogTitle>
            <DialogDescription>
              {draft?.stageName} — the owner (or whoever assigned them, or HR) can mark it done or skipped.
            </DialogDescription>
          </DialogHeader>
          {draft && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>
                  Owner <span className="text-red-500">*</span>
                </Label>
                <EmployeePicker
                  value={draft.assignedToId || null}
                  initialLabel={draft.assignedToName ?? null}
                  onChange={(id) => setDraft((d) => (d ? { ...d, assignedToId: id ?? '' } : d))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="stage-due">Due date</Label>
                <Input
                  id="stage-due"
                  type="date"
                  value={draft.dueDate}
                  onChange={(e) => setDraft((d) => (d ? { ...d, dueDate: e.target.value } : d))}
                />
              </div>
              <div className="flex items-center gap-2">
                <Checkbox
                  id="stage-escalate"
                  checked={draft.escalationEnabled}
                  onCheckedChange={(v) =>
                    setDraft((d) => (d ? { ...d, escalationEnabled: v === true } : d))
                  }
                />
                <Label htmlFor="stage-escalate">Escalate when overdue</Label>
              </div>
              {draft.escalationEnabled && (
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label htmlFor="stage-esc-days">Days after due</Label>
                    <Input
                      id="stage-esc-days"
                      type="number"
                      min={1}
                      value={draft.escalationDaysAfterDue}
                      onChange={(e) =>
                        setDraft((d) => (d ? { ...d, escalationDaysAfterDue: e.target.value } : d))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Escalate to</Label>
                    <EmployeePicker
                      value={draft.escalateToId || null}
                      initialLabel={draft.escalateToName ?? null}
                      onChange={(id) => setDraft((d) => (d ? { ...d, escalateToId: id ?? '' } : d))}
                    />
                  </div>
                </div>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDraft(null)}>
              Cancel
            </Button>
            <Button
              disabled={!draft?.assignedToId || save.isPending}
              onClick={() => draft && save.mutate(draft)}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* complete / skip */}
      <Dialog
        open={!!closing}
        onOpenChange={(o) => {
          if (!o) {
            setClosing(null);
            setClosingNotes('');
          }
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {closing?.mode === 'complete' ? 'Mark stage complete' : 'Skip stage'}
            </DialogTitle>
            <DialogDescription>{closing?.stageName}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="stage-close-notes">
              {closing?.mode === 'complete' ? 'Completion notes' : 'Why is it being skipped?'}
            </Label>
            <Textarea
              id="stage-close-notes"
              value={closingNotes}
              onChange={(e) => setClosingNotes(e.target.value)}
              maxLength={2000}
            />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setClosing(null);
                setClosingNotes('');
              }}
            >
              Cancel
            </Button>
            <Button
              disabled={close.isPending}
              onClick={() => closing && close.mutate({ id: closing.id, mode: closing.mode })}
            >
              {close.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {closing?.mode === 'complete' ? 'Complete' : 'Skip'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
