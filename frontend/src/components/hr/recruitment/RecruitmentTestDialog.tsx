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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import type { JobApplicantTestType, RecruitmentTest } from '@/types/hr/recruitment-tests';

const schema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(2000).optional(),
  instructions: z.string().max(4000).optional(),
  testType: z.enum(['Written', 'Practical']),
  // Blank is "untimed"/"no pass mark" — a real state, not a missing value, so these are strings in
  // the form and become null on the way out rather than 0.
  durationMinutes: z.string().optional(),
  passMarkPercent: z.string().optional(),
  maxAttempts: z.string(),
  shuffleQuestions: z.boolean(),
  shuffleOptions: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const toNumberOrNull = (value?: string) => {
  if (value === undefined || value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

/** Creates or edits the paper's header. The questions are added on the builder page. */
export function RecruitmentTestDialog({
  open,
  onOpenChange,
  test,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  test: RecruitmentTest | null;
  onSaved: (saved: RecruitmentTest) => void;
}) {
  const { toast } = useToast();
  const isEdit = !!test;

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '',
      description: '',
      instructions: '',
      testType: 'Written',
      durationMinutes: '',
      passMarkPercent: '',
      maxAttempts: '1',
      shuffleQuestions: false,
      shuffleOptions: false,
    },
  });

  useEffect(() => {
    if (!open) return;
    form.reset({
      name: test?.name ?? '',
      description: test?.description ?? '',
      instructions: test?.instructions ?? '',
      testType: (test?.testType ?? 'Written') as JobApplicantTestType,
      durationMinutes: test?.durationMinutes != null ? String(test.durationMinutes) : '',
      passMarkPercent: test?.passMarkPercent != null ? String(test.passMarkPercent) : '',
      maxAttempts: String(test?.maxAttempts ?? 1),
      shuffleQuestions: test?.shuffleQuestions ?? false,
      shuffleOptions: test?.shuffleOptions ?? false,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, test]);

  const save = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = {
        name: values.name.trim(),
        description: values.description?.trim() || null,
        instructions: values.instructions?.trim() || null,
        testType: values.testType,
        durationMinutes: toNumberOrNull(values.durationMinutes),
        passMarkPercent: toNumberOrNull(values.passMarkPercent),
        maxAttempts: Number(values.maxAttempts) || 1,
        shuffleQuestions: values.shuffleQuestions,
        shuffleOptions: values.shuffleOptions,
      };
      return isEdit
        ? tests.updateTest({ ...payload, id: test!.id })
        : tests.createTest(payload);
    },
    onSuccess: (saved) => {
      toast({ title: isEdit ? 'Test updated' : 'Test created' });
      onSaved(saved);
      onOpenChange(false);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not save the test',
        description: error?.message ?? 'Please check the details and try again.',
        variant: 'destructive',
      }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit test' : 'New test'}</DialogTitle>
          <DialogDescription>
            The paper&apos;s settings. Questions are added next, and the paper has to be activated
            before it can be assigned to anybody.
          </DialogDescription>
        </DialogHeader>

        <form
          className="space-y-4"
          onSubmit={form.handleSubmit((values) => save.mutate(values))}
        >
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" {...form.register('name')} placeholder="Numerical reasoning" />
            {form.formState.errors.name && (
              <p className="text-sm text-destructive">{form.formState.errors.name.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" rows={2} {...form.register('description')} />
            <p className="text-xs text-muted-foreground">
              Internal — shown to HR and on the candidate&apos;s assessment list.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="instructions">Instructions to the candidate</Label>
            <Textarea
              id="instructions"
              rows={3}
              {...form.register('instructions')}
              placeholder="Answer every question. You may use a calculator."
            />
            <p className="text-xs text-muted-foreground">
              Shown above the first question, and repeated in the invitation email.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Type</Label>
              <Select
                value={form.watch('testType')}
                onValueChange={(value) => form.setValue('testType', value as JobApplicantTestType)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Written">Written</SelectItem>
                  <SelectItem value="Practical">Practical</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="maxAttempts">Attempts allowed</Label>
              <Input id="maxAttempts" type="number" min={1} max={10} {...form.register('maxAttempts')} />
              <p className="text-xs text-muted-foreground">
                One more can be granted to an individual candidate later, with a reason.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="durationMinutes">Time limit (minutes)</Label>
              <Input
                id="durationMinutes"
                type="number"
                min={1}
                max={600}
                placeholder="Leave blank for untimed"
                {...form.register('durationMinutes')}
              />
              <p className="text-xs text-muted-foreground">
                The clock starts when the candidate opens the paper and keeps running if they close
                the page.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="passMarkPercent">Pass mark (%)</Label>
              <Input
                id="passMarkPercent"
                type="number"
                min={0}
                max={100}
                placeholder="Leave blank for no pass or fail"
                {...form.register('passMarkPercent')}
              />
            </div>
          </div>

          <div className="space-y-3 rounded-lg border p-4">
            <div className="flex items-center justify-between gap-4">
              <div>
                <Label htmlFor="shuffleQuestions">Shuffle the questions</Label>
                <p className="text-xs text-muted-foreground">
                  Each paper keeps one stable order, so a refresh does not reshuffle it underneath
                  the candidate.
                </p>
              </div>
              <Switch
                id="shuffleQuestions"
                checked={form.watch('shuffleQuestions')}
                onCheckedChange={(checked) => form.setValue('shuffleQuestions', checked)}
              />
            </div>
            <div className="flex items-center justify-between gap-4">
              <Label htmlFor="shuffleOptions">Shuffle the choices within a question</Label>
              <Switch
                id="shuffleOptions"
                checked={form.watch('shuffleOptions')}
                onCheckedChange={(checked) => form.setValue('shuffleOptions', checked)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create test'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
