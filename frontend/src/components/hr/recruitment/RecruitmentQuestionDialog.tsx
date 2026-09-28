'use client';

import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Check, Loader2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import {
  CHOICE_QUESTION_TYPES,
  RECRUITMENT_QUESTION_TYPES,
  RECRUITMENT_QUESTION_TYPE_LABELS,
  type RecruitmentQuestionType,
  type RecruitmentTest,
  type RecruitmentTestQuestion,
} from '@/types/hr/recruitment-tests';

interface DraftOption {
  optionText: string;
  isCorrect: boolean;
}

const TRUE_FALSE_OPTIONS: DraftOption[] = [
  { optionText: 'True', isCorrect: true },
  { optionText: 'False', isCorrect: false },
];

/**
 * Writes one question.
 *
 * ⚠ The rules below are mirrored on the SERVER, which refuses a question it could never mark and
 * says which one. They are repeated here so the author is told before they have typed the whole
 * thing, not instead — a client-side check is a courtesy, never the guard.
 */
export function RecruitmentQuestionDialog({
  open,
  onOpenChange,
  test,
  question,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  test: RecruitmentTest;
  question: RecruitmentTestQuestion | null;
  onSaved: () => void;
}) {
  const { toast } = useToast();
  const isEdit = !!question;

  const [questionText, setQuestionText] = useState('');
  const [questionType, setQuestionType] = useState<RecruitmentQuestionType>('SingleChoice');
  const [points, setPoints] = useState('1');
  const [sectionId, setSectionId] = useState<string>('none');
  const [expectedAnswer, setExpectedAnswer] = useState('');
  const [explanation, setExplanation] = useState('');
  const [options, setOptions] = useState<DraftOption[]>([
    { optionText: '', isCorrect: true },
    { optionText: '', isCorrect: false },
  ]);

  useEffect(() => {
    if (!open) return;
    setQuestionText(question?.questionText ?? '');
    setQuestionType(question?.questionType ?? 'SingleChoice');
    setPoints(String(question?.points ?? 1));
    setSectionId(question?.recruitmentTestSectionId ?? 'none');
    setExpectedAnswer(question?.expectedAnswer ?? '');
    setExplanation(question?.explanation ?? '');
    setOptions(
      question?.options?.length
        ? question.options.map((o) => ({ optionText: o.optionText, isCorrect: o.isCorrect }))
        : [
            { optionText: '', isCorrect: true },
            { optionText: '', isCorrect: false },
          ],
    );
  }, [open, question]);

  const isChoice = CHOICE_QUESTION_TYPES.includes(questionType);
  const isTrueFalse = questionType === 'TrueFalse';
  const effectiveOptions = isTrueFalse
    ? options.length === 2
      ? options
      : TRUE_FALSE_OPTIONS
    : options;

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        recruitmentTestId: test.id,
        recruitmentTestSectionId: sectionId === 'none' ? null : sectionId,
        questionText: questionText.trim(),
        questionType,
        points: Number(points) || 0,
        expectedAnswer: expectedAnswer.trim() || null,
        explanation: explanation.trim() || null,
        displayOrder: question?.displayOrder ?? 0,
        // ⚠ The whole set travels, always. The server treats it as a replace-set, so sending only
        // the changed options would delete the rest.
        options: isChoice
          ? effectiveOptions
              .filter((o) => o.optionText.trim().length > 0)
              .map((o, index) => ({
                optionText: o.optionText.trim(),
                isCorrect: o.isCorrect,
                displayOrder: index + 1,
              }))
          : [],
      };

      return isEdit
        ? tests.updateQuestion({ ...payload, id: question!.id })
        : tests.addQuestion(payload);
    },
    onSuccess: () => {
      toast({ title: isEdit ? 'Question updated' : 'Question added' });
      onSaved();
      onOpenChange(false);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not save the question',
        description: error?.message ?? 'Please check the question and try again.',
        variant: 'destructive',
      }),
  });

  const setCorrect = (index: number, checked: boolean) => {
    setOptions((current) =>
      current.map((option, i) => {
        if (questionType === 'MultiSelect') {
          return i === index ? { ...option, isCorrect: checked } : option;
        }
        // Single choice and true/false: exactly one correct, so ticking one unticks the rest.
        return { ...option, isCorrect: i === index ? checked : false };
      }),
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit question' : 'Add question'}</DialogTitle>
          <DialogDescription>
            {RECRUITMENT_QUESTION_TYPE_LABELS[questionType]} &middot; {test.name}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="questionText">Question</Label>
            <Textarea
              id="questionText"
              rows={3}
              value={questionText}
              onChange={(event) => setQuestionText(event.target.value)}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label>Type</Label>
              <Select
                value={questionType}
                onValueChange={(value) => setQuestionType(value as RecruitmentQuestionType)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {RECRUITMENT_QUESTION_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {RECRUITMENT_QUESTION_TYPE_LABELS[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="points">Marks</Label>
              <Input
                id="points"
                type="number"
                min={0}
                step="0.5"
                value={points}
                onChange={(event) => setPoints(event.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label>Section</Label>
              <Select value={sectionId} onValueChange={setSectionId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No section</SelectItem>
                  {test.sections.map((section) => (
                    <SelectItem key={section.id} value={section.id}>
                      {section.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {questionType === 'Numeric' && (
            <div className="space-y-2">
              <Label htmlFor="expectedAnswer">Expected answer</Label>
              <Input
                id="expectedAnswer"
                value={expectedAnswer}
                onChange={(event) => setExpectedAnswer(event.target.value)}
                placeholder="42"
              />
              <p className="text-xs text-muted-foreground">
                Compared as a number, so 7 and 7.0 are the same answer. Without one, nothing the
                candidate types could be right — the server refuses the question.
              </p>
            </div>
          )}

          {questionType === 'FreeText' && (
            <div className="space-y-2">
              <Label htmlFor="markingNote">Marking note</Label>
              <Textarea
                id="markingNote"
                rows={2}
                value={expectedAnswer}
                onChange={(event) => setExpectedAnswer(event.target.value)}
                placeholder="What a good answer covers."
              />
              <p className="text-xs text-muted-foreground">
                For the marker only — never shown to the candidate. A written answer is never marked
                by the system; it waits for a person.
              </p>
            </div>
          )}

          {isChoice && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label>Choices</Label>
                {!isTrueFalse && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      setOptions((current) => [...current, { optionText: '', isCorrect: false }])
                    }
                  >
                    <Plus className="mr-2 h-3 w-3" />
                    Add choice
                  </Button>
                )}
              </div>

              <p className="text-xs text-muted-foreground">
                {questionType === 'MultiSelect'
                  ? 'Tick every correct choice. A multiple-answer question is marked on getting all of them and no wrong one — there is no partial credit.'
                  : 'Tick the one correct choice.'}
              </p>

              <div className="space-y-2">
                {effectiveOptions.map((option, index) => (
                  <div key={index} className="flex items-center gap-2">
                    <Checkbox
                      checked={option.isCorrect}
                      onCheckedChange={(checked) => setCorrect(index, checked === true)}
                      aria-label={`Choice ${index + 1} is correct`}
                    />
                    <Input
                      value={option.optionText}
                      readOnly={isTrueFalse}
                      onChange={(event) =>
                        setOptions((current) =>
                          current.map((o, i) =>
                            i === index ? { ...o, optionText: event.target.value } : o,
                          ),
                        )
                      }
                      placeholder={`Choice ${index + 1}`}
                    />
                    {!isTrueFalse && effectiveOptions.length > 2 && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        onClick={() =>
                          setOptions((current) => current.filter((_, i) => i !== index))
                        }
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    )}
                  </div>
                ))}
              </div>

              {isTrueFalse && options.length !== 2 && (
                <p className="text-xs text-muted-foreground">
                  A true/false question always has these two choices.
                </p>
              )}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="explanation">Explanation</Label>
            <Textarea
              id="explanation"
              rows={2}
              value={explanation}
              onChange={(event) => setExplanation(event.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Kept with the marking key. Not served during the sitting.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            type="button"
            disabled={save.isPending || questionText.trim().length === 0}
            onClick={() => save.mutate()}
          >
            {save.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Check className="mr-2 h-4 w-4" />
            )}
            {isEdit ? 'Save question' : 'Add question'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
