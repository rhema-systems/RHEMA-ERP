'use client';

import { useEffect } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertTriangle, Loader2, Plus, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  interviewQuestionBankService as bank,
  interviewQuestionPresetService as presets,
} from '@/services/hr/interviews.service';
import type { InterviewQuestionPreset } from '@/types/hr/interviews';

const schema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(1000).optional(),
  isActive: z.boolean(),
  items: z
    .array(
      z.object({
        id: z.string().optional(),
        questionTypeId: z.string().min(1, 'Choose a question type'),
        requiredQuestionCount: z.coerce.number().int().min(1).max(50),
        allowedPoolSize: z.coerce.number().int().min(1).max(50),
      }),
    )
    .min(1, 'A preset needs at least one section'),
});

// `z.coerce.number()` takes `unknown` in and `number` out — the same input/output split the other
// HR forms use (see WorkScheduleForm).
type FormValues = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

export function QuestionPresetDialog({
  open,
  onOpenChange,
  preset,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  preset: InterviewQuestionPreset | null;
  onSaved: () => void;
}) {
  const { toast } = useToast();
  const isEdit = !!preset;

  const types = useQuery({
    queryKey: ['hr', 'interview-question-types'],
    queryFn: () => bank.getTypes(),
    enabled: open,
  });
  // A deactivated type stays on an existing preset but must not be offered for a new section.
  const activeTypes = (types.data ?? []).filter((t) => t.isActive);

  const form = useForm<FormValues, any, FormOutput>({
    resolver: zodResolver(schema) as any,
    defaultValues: { name: '', description: '', isActive: true, items: [] },
  });

  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'items' });

  useEffect(() => {
    if (!open) return;
    form.reset({
      name: preset?.name ?? '',
      description: preset?.description ?? '',
      isActive: preset?.isActive ?? true,
      items:
        preset?.items
          ?.slice()
          .sort((a, b) => a.displayOrder - b.displayOrder)
          .map((item) => ({
            id: item.id,
            questionTypeId: item.questionTypeId,
            requiredQuestionCount: item.requiredQuestionCount,
            allowedPoolSize: item.allowedPoolSize,
          })) ?? [],
    });
  }, [open, preset, form]);

  const watchedItems = form.watch('items');
  const duplicateType = watchedItems.some(
    (item, i) => item.questionTypeId && watchedItems.findIndex((o) => o.questionTypeId === item.questionTypeId) !== i,
  );
  const poolBelowRequired = watchedItems.some(
    (item) => Number(item.allowedPoolSize) < Number(item.requiredQuestionCount),
  );

  const save = useMutation({
    mutationFn: (values: FormOutput) => {
      // ⚠ Replace-set: `items` is the whole list. Anything dropped here is deleted server-side,
      // which is why the editor always loads the existing set first.
      const payload = {
        name: values.name,
        description: values.description?.trim() ? values.description : null,
        isActive: values.isActive,
        items: values.items.map((item, index) => ({
          ...(item.id ? { id: item.id } : {}),
          questionTypeId: item.questionTypeId,
          requiredQuestionCount: item.requiredQuestionCount,
          allowedPoolSize: item.allowedPoolSize,
          displayOrder: index + 1,
        })),
      };
      const editingId = preset?.id;
      return editingId ? presets.update(editingId, { ...payload, id: editingId }) : presets.create(payload);
    },
    onSuccess: () => {
      toast({ title: isEdit ? 'Preset updated' : 'Preset created' });
      onSaved();
      onOpenChange(false);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not save the preset',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit preset' : 'New preset'}</DialogTitle>
          <DialogDescription>
            Each section names a question type, how many questions the panel must score, and how many
            are drawn into their pool to choose from.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <div className="space-y-2">
            <Label htmlFor="name">
              Name<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Input id="name" placeholder="Technical panel — engineering" {...form.register('name')} />
            {form.formState.errors.name && (
              <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="presetDescription">Description</Label>
            <Textarea id="presetDescription" rows={2} {...form.register('description')} />
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <Label>Sections</Label>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => append({ questionTypeId: '', requiredQuestionCount: 2, allowedPoolSize: 5 })}
              >
                <Plus className="mr-1.5 h-4 w-4" />
                Add section
              </Button>
            </div>

            {fields.length === 0 && (
              <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                No sections yet. A preset needs at least one.
              </p>
            )}

            {fields.map((field, index) => (
              <div key={field.id} className="grid grid-cols-[minmax(0,1fr)_110px_110px_40px] items-end gap-2">
                <div className="space-y-1.5">
                  {index === 0 && <Label className="text-xs text-muted-foreground">Question type</Label>}
                  <Select
                    value={form.watch(`items.${index}.questionTypeId`)}
                    onValueChange={(v) => form.setValue(`items.${index}.questionTypeId`, v, { shouldValidate: true })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Choose a type" />
                    </SelectTrigger>
                    <SelectContent>
                      {activeTypes.map((type) => (
                        <SelectItem key={type.id} value={type.id}>
                          {type.typeName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  {index === 0 && <Label className="text-xs text-muted-foreground">Must score</Label>}
                  <Input type="number" min={1} {...form.register(`items.${index}.requiredQuestionCount`)} />
                </div>
                <div className="space-y-1.5">
                  {index === 0 && <Label className="text-xs text-muted-foreground">Pool size</Label>}
                  <Input type="number" min={1} {...form.register(`items.${index}.allowedPoolSize`)} />
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Remove section"
                  onClick={() => remove(index)}
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            ))}

            {form.formState.errors.items?.message && (
              <p className="text-sm text-red-500">{form.formState.errors.items.message}</p>
            )}

            {duplicateType && (
              <Alert variant="destructive">
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  A question type can only appear once — two sections of the same type would draw two
                  plans for one part of the interview.
                </AlertDescription>
              </Alert>
            )}

            {poolBelowRequired && (
              <Alert>
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  A pool smaller than the number that must be scored leaves the panel unable to sign
                  off their scorecards.
                </AlertDescription>
              </Alert>
            )}
          </div>

          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="presetActive">Active</Label>
              <p className="text-sm text-muted-foreground">
                Inactive presets are not offered when scheduling.
              </p>
            </div>
            <Switch
              id="presetActive"
              checked={form.watch('isActive')}
              onCheckedChange={(checked) => form.setValue('isActive', checked)}
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending || duplicateType}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create preset'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
