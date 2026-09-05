'use client';

import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import { ORIENTATION_QUESTION_TYPE_OPTIONS } from '@/types/hr/orientation';
import type { OrientationAssessmentQuestion } from '@/types/hr/orientation';

const optionSchema = z.object({
  optionText: z.string().min(1, 'Option text is required').max(500),
  isCorrect: z.boolean(),
  displayOrder: z.coerce.number().min(0).max(999),
});

const questionSchema = z
  .object({
    questionText: z.string().min(1, 'The question is required').max(2000),
    questionType: z.enum(['SingleChoice', 'MultiSelect', 'TrueFalse', 'FreeText']),
    points: z.coerce.number().min(0).max(1000),
    explanation: z.string().max(2000).optional().or(z.literal('')),
    sequenceOrder: z.coerce.number().min(0).max(9999),
    isActive: z.boolean(),
    options: z.array(optionSchema),
  })
  // Free text is stored but never auto-graded, so it is the one type that carries no options. Every
  // other type is marked by comparing the selected set against the correct set — a paper with no
  // correct answer marked can never be passed, and the server would accept it silently.
  .superRefine((v, ctx) => {
    if (v.questionType === 'FreeText') return;

    if (v.options.length < 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Give this question at least two options.',
        path: ['options'],
      });
      return;
    }

    const correct = v.options.filter((o) => o.isCorrect).length;
    if (correct === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Mark at least one option as correct, or nobody can pass this question.',
        path: ['options'],
      });
    }
    if (correct > 1 && v.questionType !== 'MultiSelect') {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message:
          'Only a multi-select question can have more than one correct option. Change the type or unmark the extras.',
        path: ['options'],
      });
    }
  });

type QuestionForm = z.infer<typeof questionSchema>;

const emptyQuestion: QuestionForm = {
  questionText: '',
  questionType: 'SingleChoice',
  points: 1,
  explanation: '',
  sequenceOrder: 0,
  isActive: true,
  options: [
    { optionText: '', isCorrect: true, displayOrder: 0 },
    { optionText: '', isCorrect: false, displayOrder: 1 },
  ],
};

const typeLabel = (v: string) =>
  ORIENTATION_QUESTION_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

/**
 * The option rows for one question.
 *
 * Lives in its own component because it needs `useFieldArray`, and `renderFields` is a callback
 * rather than a component body — calling a hook there would break the rules of hooks.
 *
 * ⚠ The update endpoint treats `options` as the whole set: whatever is on screen when this saves is
 * what the question ends up with, and anything removed here is deleted server-side. That is why the
 * dialog is always seeded from the full existing set rather than from the changes.
 */
function OptionsEditor({ form }: { form: UseFormReturn<QuestionForm> }) {
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'options' });
  const questionType = form.watch('questionType');
  const message = (form.formState.errors as any)?.options?.message as string | undefined;

  if (questionType === 'FreeText') {
    return (
      <div className="text-muted-foreground rounded-md border border-dashed p-3 text-sm">
        Free-text answers are recorded against the attempt but are not auto-graded, so this question
        carries no options and no marks towards the score.
      </div>
    );
  }

  const single = questionType !== 'MultiSelect';

  /**
   * Marking a correct option on a single-answer question clears the others. The server marks by
   * comparing the whole selected set against the whole correct set, so two correct options on a
   * single-choice question make it unanswerable rather than lenient.
   */
  const setCorrect = (index: number, next: boolean) => {
    if (single && next) {
      fields.forEach((_f, i) =>
        form.setValue(`options.${i}.isCorrect`, i === index, { shouldValidate: true }),
      );
      return;
    }
    form.setValue(`options.${index}.isCorrect`, next, { shouldValidate: true });
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <Label>Options</Label>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() =>
            append({ optionText: '', isCorrect: false, displayOrder: fields.length })
          }
        >
          <Plus className="mr-2 h-4 w-4" />
          Add option
        </Button>
      </div>

      <div className="space-y-2">
        {fields.map((field, index) => (
          <div key={field.id} className="flex items-center gap-2 rounded-md border p-2">
            <span className="text-muted-foreground w-5 shrink-0 text-center font-mono text-xs">
              {index + 1}
            </span>
            <Input
              placeholder="Option text"
              className="flex-1"
              {...form.register(`options.${index}.optionText` as const)}
            />
            <div className="flex shrink-0 items-center gap-2">
              <Switch
                checked={!!form.watch(`options.${index}.isCorrect`)}
                onCheckedChange={(v) => setCorrect(index, v)}
                aria-label={`Option ${index + 1} is correct`}
              />
              <span className="text-muted-foreground w-14 text-xs">
                {form.watch(`options.${index}.isCorrect`) ? 'Correct' : 'Wrong'}
              </span>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8"
                disabled={fields.length <= 2}
                onClick={() => remove(index)}
              >
                <Trash2 className="h-4 w-4" />
                <span className="sr-only">Remove option</span>
              </Button>
            </div>
          </div>
        ))}
      </div>

      {message && <p className="text-sm text-red-500">{message}</p>}
      <p className="text-muted-foreground text-xs">
        Saving replaces the whole option set — an option removed here is deleted.
      </p>
    </div>
  );
}

/**
 * The programme's question paper.
 *
 * ⚠ This is the authoring view: `isCorrect` is populated and `explanation` is present. It is HR-only
 * server-side, and must never be reused on a participant screen — the player reads the same paper
 * through `employeeOrientationService.getAssessment`, which strips the key.
 */
export function ProgramQuestionsPanel({ programId }: { programId: string }) {
  return (
    <ResourceCollectionTab<OrientationAssessmentQuestion, QuestionForm>
      parentId={programId}
      title="questions"
      singular="question"
      queryKey={['hr', 'orientation-programs', programId, 'questions']}
      invalidateKeys={[['hr', 'orientation-programs', programId]]}
      dialogClassName="sm:max-w-[680px]"
      dialogHint="Marked by comparing the selected options against the correct ones."
      emptyDescription="No questions yet. A programme that requires an assessment needs at least one."
      list={() => orientationProgramService.getQuestions(programId)}
      create={(id, values) =>
        orientationProgramService.addQuestion(id, {
          programId: id,
          ...values,
          explanation: values.explanation || null,
          options: values.questionType === 'FreeText' ? [] : values.options,
        })
      }
      update={(_p, questionId, values) =>
        orientationProgramService.updateQuestion(questionId, {
          id: questionId,
          ...values,
          explanation: values.explanation || null,
          options: values.questionType === 'FreeText' ? [] : values.options,
        })
      }
      remove={(_p, questionId) => orientationProgramService.removeQuestion(questionId)}
      getId={(q) => q.id}
      columns={[
        { header: '#', cell: (q) => q.sequenceOrder, className: 'w-[60px]' },
        {
          header: 'Question',
          cell: (q) => (
            <div>
              <span className="font-medium">{q.questionText}</span>
              {q.questionType !== 'FreeText' && (
                <div className="text-muted-foreground mt-0.5 text-xs">
                  {q.options.length} options ·{' '}
                  {q.options.filter((o) => o.isCorrect).length} correct
                </div>
              )}
            </div>
          ),
        },
        { header: 'Type', cell: (q) => typeLabel(q.questionType) },
        {
          header: 'Points',
          cell: (q) =>
            q.questionType === 'FreeText' ? (
              <span className="text-muted-foreground">Not graded</span>
            ) : (
              <Badge variant="outline">{q.points}</Badge>
            ),
        },
        { header: 'Status', cell: (q) => <StatusBadge active={q.isActive} /> },
      ]}
      schema={questionSchema as any}
      emptyForm={emptyQuestion}
      toForm={(q) => ({
        questionText: q.questionText,
        questionType: q.questionType,
        points: q.points,
        explanation: q.explanation ?? '',
        sequenceOrder: q.sequenceOrder,
        isActive: q.isActive,
        // Seeded from the full existing set — this payload replaces it wholesale on save.
        options:
          q.options.length > 0
            ? [...q.options]
                .sort((a, b) => a.displayOrder - b.displayOrder)
                .map((o) => ({
                  optionText: o.optionText,
                  isCorrect: o.isCorrect,
                  displayOrder: o.displayOrder,
                }))
            : emptyQuestion.options,
      })}
      renderFields={(form) => (
        <>
          <TextareaField form={form} name="questionText" label="Question" rows={2} />
          <FieldRow>
            <SelectField
              form={form}
              name="questionType"
              label="Type"
              required
              options={ORIENTATION_QUESTION_TYPE_OPTIONS}
            />
            <NumberField form={form} name="points" label="Points" required />
          </FieldRow>
          <OptionsEditor form={form as UseFormReturn<QuestionForm>} />
          <TextareaField
            form={form}
            name="explanation"
            label="Explanation"
            rows={2}
            placeholder="Shown to the participant once their attempt has been graded."
          />
          <FieldRow>
            <NumberField form={form} name="sequenceOrder" label="Sequence" required />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive questions stay on past attempts but leave the paper."
            />
          </FieldRow>
        </>
      )}
    />
  );
}
