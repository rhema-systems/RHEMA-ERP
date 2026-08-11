'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
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
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { interviewQuestionBankService as bank } from '@/services/hr/interviews.service';
import type { InterviewQuestionType, InterviewQuestionTypeSummary } from '@/types/hr/interviews';

const schema = z.object({
  typeName: z.string().min(1, 'A name is required').max(50),
  code: z.string().max(50).optional(),
  description: z.string().max(500).optional(),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

export function QuestionTypeDialog({
  open,
  onOpenChange,
  questionType,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  questionType: InterviewQuestionTypeSummary | null;
  onSaved: (saved: InterviewQuestionType) => void;
}) {
  const { toast } = useToast();
  const isEdit = !!questionType;

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { typeName: '', code: '', description: '', isActive: true },
  });

  useEffect(() => {
    if (!open) return;
    form.reset({
      typeName: questionType?.typeName ?? '',
      code: questionType?.code ?? '',
      description: questionType?.description ?? '',
      isActive: questionType?.isActive ?? true,
    });
  }, [open, questionType, form]);

  const save = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = {
        typeName: values.typeName,
        code: values.code?.trim() ? values.code : null,
        description: values.description?.trim() ? values.description : null,
        isActive: values.isActive,
      };
      const editingId = questionType?.id;
      return editingId
        ? bank.updateType(editingId, { ...payload, id: editingId })
        : bank.createType(payload);
    },
    onSuccess: (saved) => {
      toast({ title: isEdit ? 'Question type updated' : 'Question type created' });
      onSaved(saved);
      onOpenChange(false);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not save the question type',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit question type' : 'New question type'}</DialogTitle>
          <DialogDescription>
            A section of the interview. Presets and per-interview question plans are built by naming
            a type and how many of its questions the panel must ask.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <div className="space-y-2">
            <Label htmlFor="typeName">
              Name<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Input id="typeName" placeholder="Competency" {...form.register('typeName')} />
            {form.formState.errors.typeName && (
              <p className="text-sm text-red-500">{form.formState.errors.typeName.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="code">Code</Label>
            <Input id="code" placeholder="COMP" {...form.register('code')} />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" rows={3} {...form.register('description')} />
          </div>

          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="isActive">Active</Label>
              <p className="text-sm text-muted-foreground">
                Inactive types stay listed here but are not offered when building a preset or drawing
                questions.
              </p>
            </div>
            <Switch
              id="isActive"
              checked={form.watch('isActive')}
              onCheckedChange={(checked) => form.setValue('isActive', checked)}
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
