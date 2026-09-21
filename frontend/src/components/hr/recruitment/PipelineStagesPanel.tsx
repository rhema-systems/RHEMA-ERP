'use client';

import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { recruitmentPipelineService } from '@/services/hr/recruitment-pipeline.service';
import {
  PIPELINE_STAGE_TYPES,
  type RecruitmentPipelineStage,
  type RecruitmentPipelineStageType,
} from '@/types/hr/recruitment-pipeline';

const stageSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(500).optional().nullable(),
  order: z.coerce.number().int().min(1, 'Order starts at 1'),
  stageType: z.string().min(1, 'Stage type is required'),
  isActive: z.boolean(),
  isRequired: z.boolean(),
  isFinalStage: z.boolean(),
  canSkip: z.boolean(),
  canRepeat: z.boolean(),
  maxAttempts: z.coerce.number().int().min(1).optional().nullable(),
  defaultTimeToCompleteDays: z.coerce.number().int().min(0).optional().nullable(),
  instructions: z.string().max(2000).optional().nullable(),
});

type StageForm = z.infer<typeof stageSchema>;

const emptyStage = (nextOrder: number): StageForm => ({
  name: '',
  description: null,
  order: nextOrder,
  stageType: 'ApplicationReview',
  isActive: true,
  isRequired: true,
  isFinalStage: false,
  canSkip: false,
  canRepeat: false,
  maxAttempts: null,
  defaultTimeToCompleteDays: null,
  instructions: null,
});

/**
 * The stages of one pipeline, in order.
 *
 * ⚠ **These are transition rules, not labels.** The server refuses a move backwards (or sideways)
 * into a stage whose `canRepeat` is false, and refuses any move into a stage that has hit
 * `maxAttempts`. Editing them changes the rules every in-flight application on this pipeline is
 * being moved under, which is why the pipeline list offers Duplicate.
 *
 * Reordering is a dedicated endpoint rather than an edit of each row: `order` decides what counts as
 * forward, so the server takes the whole new sequence at once.
 */
export function PipelineStagesPanel({ pipelineId, canEdit }: { pipelineId: string; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editing, setEditing] = useState<RecruitmentPipelineStage | null>(null);
  const [adding, setAdding] = useState(false);
  const [deleting, setDeleting] = useState<RecruitmentPipelineStage | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'pipeline-stages', pipelineId],
    queryFn: () => recruitmentPipelineService.getStages(pipelineId),
    enabled: !!pipelineId,
  });

  const stages = useMemo(
    () => [...(data ?? [])].sort((a, b) => a.order - b.order),
    [data],
  );

  // `as any` on the resolver is the codebase idiom wherever a schema uses `z.coerce`: the coerced
  // fields make zod's *input* type `unknown` where `z.infer` gives `number`, so the two generics
  // disagree. Same treatment as LeaveTypeForm and the other coercing forms.
  const form = useForm<StageForm>({
    resolver: zodResolver(stageSchema) as any,
    defaultValues: emptyStage(1),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pipeline-stages', pipelineId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-pipelines'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-pipeline', pipelineId] });
  };

  const openAdd = () => {
    form.reset(emptyStage((stages.at(-1)?.order ?? 0) + 1));
    setAdding(true);
  };

  const openEdit = (stage: RecruitmentPipelineStage) => {
    form.reset({
      name: stage.name,
      description: stage.description ?? null,
      order: stage.order,
      stageType: stage.stageType,
      isActive: stage.isActive,
      isRequired: stage.isRequired,
      isFinalStage: stage.isFinalStage,
      canSkip: stage.canSkip,
      canRepeat: stage.canRepeat,
      maxAttempts: stage.maxAttempts ?? null,
      defaultTimeToCompleteDays: stage.defaultTimeToCompleteDays ?? null,
      instructions: stage.instructions ?? null,
    });
    setEditing(stage);
  };

  const save = useMutation({
    mutationFn: async (values: StageForm) => {
      const payload = {
        ...values,
        stageType: values.stageType as RecruitmentPipelineStageType,
        description: values.description || null,
        instructions: values.instructions || null,
        // D-13: derived on the server too; sent consistently so old readers of the payload agree.
        isRequired: !values.canSkip,
        maxAttempts: values.canRepeat ? values.maxAttempts ?? null : null,
        defaultTimeToCompleteDays: values.defaultTimeToCompleteDays ?? null,
      };
      if (editing) {
        return recruitmentPipelineService.updateStage(editing.id, { ...payload, id: editing.id });
      }
      return recruitmentPipelineService.addStage(pipelineId, payload);
    },
    onSuccess: async () => {
      await refresh();
      setAdding(false);
      setEditing(null);
      toast({ title: 'Stage saved' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (stageId: string) => recruitmentPipelineService.deleteStage(stageId),
    onSuccess: async () => {
      await refresh();
      setDeleting(null);
      toast({ title: 'Stage removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove', description: e?.message, variant: 'destructive' }),
  });

  /** Swaps a stage with its neighbour and sends the whole resulting sequence. */
  const reorder = useMutation({
    mutationFn: (next: RecruitmentPipelineStage[]) =>
      recruitmentPipelineService.reorderStages(
        pipelineId,
        next.map((s, i) => ({ stageId: s.id, newOrder: i + 1 })),
      ),
    onSuccess: refresh,
    onError: (e: any) => toast({ title: 'Could not reorder', description: e?.message, variant: 'destructive' }),
  });

  const move = (index: number, delta: number) => {
    const next = [...stages];
    const target = index + delta;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    reorder.mutate(next);
  };

  const dialogOpen = adding || !!editing;

  return (
    <>
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : stages.length === 0 ? (
            <EmptyState
              title="No stages yet"
              description="Applications cannot be moved through a pipeline with no stages."
              action={canEdit ? <Button onClick={openAdd}>Add the first stage</Button> : undefined}
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-16">#</TableHead>
                  <TableHead>Stage</TableHead>
                  <TableHead className="w-44">Type</TableHead>
                  <TableHead className="w-56">Rules</TableHead>
                  <TableHead className="w-24">Status</TableHead>
                  {canEdit && <TableHead className="w-40 text-right">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {stages.map((s, i) => (
                  <TableRow key={s.id}>
                    <TableCell className="tabular-nums text-muted-foreground">{s.order}</TableCell>
                    <TableCell>
                      <div className="font-medium">{s.name}</div>
                      {s.description && (
                        <div className="text-xs text-muted-foreground">{s.description}</div>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={humanizeEnum(s.stageType)} />
                    </TableCell>
                    <TableCell className="space-x-1 text-xs text-muted-foreground">
                      {s.isFinalStage && <span className="font-medium text-foreground">Final</span>}
                      {s.canSkip ? <span>Can be skipped</span> : <span>Required — cannot be skipped</span>}
                      {s.canRepeat ? <span>· Repeatable</span> : null}
                      {s.maxAttempts ? <span>· Max {s.maxAttempts}</span> : null}
                    </TableCell>
                    <TableCell>
                      <StatusBadge active={s.isActive} />
                    </TableCell>
                    {canEdit && (
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-0.5">
                          <Button
                            variant="ghost"
                            size="icon"
                            disabled={i === 0 || reorder.isPending}
                            onClick={() => move(i, -1)}
                            aria-label="Move earlier"
                          >
                            <ArrowUp className="h-3.5 w-3.5" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            disabled={i === stages.length - 1 || reorder.isPending}
                            onClick={() => move(i, 1)}
                            aria-label="Move later"
                          >
                            <ArrowDown className="h-3.5 w-3.5" />
                          </Button>
                          <Button variant="ghost" size="icon" onClick={() => openEdit(s)} aria-label="Edit">
                            <Pencil className="h-3.5 w-3.5" />
                          </Button>
                          <Button variant="ghost" size="icon" onClick={() => setDeleting(s)} aria-label="Remove">
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {canEdit && stages.length > 0 && (
        <Button variant="outline" onClick={openAdd}>
          <Plus className="mr-2 h-4 w-4" />
          Add stage
        </Button>
      )}

      <Dialog
        open={dialogOpen}
        onOpenChange={(o) => {
          if (!o) {
            setAdding(false);
            setEditing(null);
          }
        }}
      >
        <DialogContent className="sm:max-w-[620px]">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit stage' : 'Add stage'}</DialogTitle>
            <DialogDescription>
              The stage type decides what status an application takes when it arrives here. The rules
              below are enforced on every move — a non-repeatable stage cannot be re-entered, and a
              stage at its attempt limit refuses new arrivals.
            </DialogDescription>
          </DialogHeader>

          <form
            id="pipeline-stage-form"
            onSubmit={form.handleSubmit((values) => save.mutate(values))}
            className="space-y-4"
          >
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <SelectField
                form={form}
                name="stageType"
                label="Stage type"
                required
                options={PIPELINE_STAGE_TYPES.map((t) => ({ value: t, label: humanizeEnum(t) }))}
              />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" />
            <FieldRow>
              <NumberField form={form} name="order" label="Order" required />
              <NumberField
                form={form}
                name="defaultTimeToCompleteDays"
                label="Target days in stage"
              />
            </FieldRow>
            <FieldRow>
              <SwitchField form={form} name="isActive" label="Active" />
              <SwitchField form={form} name="isFinalStage" label="Final stage" />
            </FieldRow>
            {/* Round 3, lane G (D-13): ONE switch. A stage is required exactly when it cannot be
                skipped — the server derives `isRequired` and enforces both: a non-skippable stage
                cannot be jumped over, and the final stage refuses an application that never
                entered a required one. "Max entries" only means something when re-entry is allowed. */}
            <FieldRow>
              <SwitchField
                form={form}
                name="canSkip"
                label="Can be skipped"
                description={form.watch('canSkip') ? 'Optional — an application may move past it.' : 'Required — every application must pass through it.'}
              />
              <SwitchField form={form} name="canRepeat" label="Can be re-entered" />
            </FieldRow>
            {form.watch('canRepeat') && (
              <FieldRow>
                <NumberField form={form} name="maxAttempts" label="Max entries" />
                <div />
              </FieldRow>
            )}
            <TextareaField form={form} name="instructions" label="Instructions for reviewers" />
          </form>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setAdding(false);
                setEditing(null);
              }}
            >
              Cancel
            </Button>
            <Button type="submit" form="pipeline-stage-form" disabled={save.isPending}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Remove "${deleting?.name}"?`}
        description="Applications that have passed through this stage keep their history, but no one can be moved into it again."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (deleting) await remove.mutateAsync(deleting.id);
          return true;
        }}
      />
    </>
  );
}
