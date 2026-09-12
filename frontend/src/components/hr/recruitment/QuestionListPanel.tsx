'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, MessageSquareQuote, Pencil, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { interviewQuestionBankService as bank } from '@/services/hr/interviews.service';
import type { InterviewQuestion, InterviewQuestionTypeSummary } from '@/types/hr/interviews';

const schema = z
  .object({
    questionText: z.string().min(1, 'The question is required').max(500),
    weight: z.coerce.number().int().min(1, 'Weight must be at least 1').max(100),
    minScore: z.coerce.number().int().min(0).max(100),
    maxScore: z.coerce.number().int().min(1).max(100),
    isActive: z.boolean(),
  })
  .refine((v) => v.maxScore > v.minScore, {
    message: 'The top of the band must be above the bottom',
    path: ['maxScore'],
  });

// `z.coerce.number()` takes `unknown` in and `number` out, so the form's value type and its
// validated type differ — the split the other HR forms use (see WorkScheduleForm).
type FormValues = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

/**
 * The questions inside one type.
 *
 * The weight and the score band are shown together because they only make sense together: the
 * server scores an answer as `(raw / maxScore) × weight`, so a question marked out of 5 with weight
 * 20 contributes exactly as much as one marked out of 10 with weight 20. The panel's raw marks are
 * normalised before the weight is applied — which is what lets sections use different scales.
 */
export function QuestionListPanel({ type }: { type: InterviewQuestionTypeSummary }) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<InterviewQuestion | null>(null);

  const questions = useQuery({
    queryKey: ['hr', 'interview-questions', type.id],
    queryFn: () => bank.getQuestionsByType(type.id),
  });

  const rows = questions.data ?? [];
  const activeCount = rows.filter((q) => q.isActive).length;

  const form = useForm<FormValues, any, FormOutput>({
    resolver: zodResolver(schema) as any,
    defaultValues: { questionText: '', weight: 10, minScore: 1, maxScore: 10, isActive: true },
  });

  useEffect(() => {
    if (!dialogOpen) return;
    form.reset({
      questionText: editing?.questionText ?? '',
      weight: editing?.weight ?? 10,
      minScore: editing?.minScore ?? 1,
      maxScore: editing?.maxScore ?? 10,
      isActive: editing?.isActive ?? true,
    });
  }, [dialogOpen, editing, form]);

  const save = useMutation({
    mutationFn: (values: FormOutput) => {
      const payload = { ...values, questionTypeId: type.id };
      return editing
        ? bank.updateQuestion(editing.id, { ...payload, id: editing.id })
        : bank.createQuestion(payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-questions', type.id] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-question-types'] });
      toast({ title: editing ? 'Question updated' : 'Question added' });
      setDialogOpen(false);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not save the question',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => bank.deleteQuestion(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-questions', type.id] });
      toast({ title: 'Question deleted' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not delete the question',
        description: error?.message ?? 'A question already used on a scorecard cannot be removed.',
        variant: 'destructive',
      }),
  });

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
        <div>
          <CardTitle className="text-base">{type.typeName}</CardTitle>
          <CardDescription>
            {activeCount} active question{activeCount === 1 ? '' : 's'} available to draw from.
            A plan that asks for more than this will come up short.
          </CardDescription>
        </div>
        <Button
          size="sm"
          onClick={() => {
            setEditing(null);
            setDialogOpen(true);
          }}
        >
          <Plus className="mr-1.5 h-4 w-4" />
          Add question
        </Button>
      </CardHeader>
      <CardContent className="p-0">
        {questions.isLoading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-10">
            <EmptyState
              icon={MessageSquareQuote}
              title="No questions yet"
              description="Add the questions the panel will choose from for this section."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Question</TableHead>
                <TableHead className="w-[90px]">Weight</TableHead>
                <TableHead className="w-[110px]">Score band</TableHead>
                <TableHead className="w-[100px]">Status</TableHead>
                <TableHead className="w-[90px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((question) => (
                <TableRow key={question.id}>
                  <TableCell className="max-w-[420px]">{question.questionText}</TableCell>
                  <TableCell>{question.weight}</TableCell>
                  <TableCell>
                    {question.minScore}–{question.maxScore}
                  </TableCell>
                  <TableCell>
                    <StatusBadge active={question.isActive} />
                  </TableCell>
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label="Edit question"
                        onClick={() => {
                          setEditing(question);
                          setDialogOpen(true);
                        }}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label="Delete question"
                        onClick={() => remove.mutate(question.id)}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit question' : 'New question'}</DialogTitle>
            <DialogDescription>
              Scored answers are normalised before weighting — <code>(mark ÷ top of band) × weight</code> —
              so a question marked out of 5 and one marked out of 10 count equally at the same weight.
            </DialogDescription>
          </DialogHeader>

          <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
            <div className="space-y-2">
              <Label htmlFor="questionText">
                Question<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Textarea id="questionText" rows={3} {...form.register('questionText')} />
              {form.formState.errors.questionText && (
                <p className="text-sm text-red-500">{form.formState.errors.questionText.message}</p>
              )}
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-2">
                <Label htmlFor="weight">Weight</Label>
                <Input id="weight" type="number" min={1} {...form.register('weight')} />
                {form.formState.errors.weight && (
                  <p className="text-sm text-red-500">{form.formState.errors.weight.message}</p>
                )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="minScore">Lowest mark</Label>
                <Input id="minScore" type="number" min={0} {...form.register('minScore')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="maxScore">Highest mark</Label>
                <Input id="maxScore" type="number" min={1} {...form.register('maxScore')} />
                {form.formState.errors.maxScore && (
                  <p className="text-sm text-red-500">{form.formState.errors.maxScore.message}</p>
                )}
              </div>
            </div>
            <p className="text-sm text-muted-foreground">
              The panel is held to this band — a mark outside it is refused, so the scorecard cannot be
              inflated past the question&rsquo;s ceiling.
            </p>

            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label htmlFor="questionActive">Active</Label>
                <p className="text-sm text-muted-foreground">
                  Only active questions are drawn into a panel&rsquo;s pool.
                </p>
              </div>
              <Switch
                id="questionActive"
                checked={form.watch('isActive')}
                onCheckedChange={(checked) => form.setValue('isActive', checked)}
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={save.isPending}>
                {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save changes' : 'Add question'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
