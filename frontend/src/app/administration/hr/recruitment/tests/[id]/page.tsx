'use client';

import { useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Eye,
  FileQuestion,
  Loader2,
  Lock,
  Pencil,
  Plus,
  Trash2,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import {
  AUTO_MARKED_QUESTION_TYPES,
  RECRUITMENT_QUESTION_TYPE_LABELS,
  type RecruitmentTestQuestion,
} from '@/types/hr/recruitment-tests';
import { RecruitmentQuestionDialog } from '@/components/hr/recruitment/RecruitmentQuestionDialog';
import { RecruitmentTestPreview } from '@/components/hr/recruitment/RecruitmentTestPreview';

/**
 * The paper builder: sections, questions, choices, and the preview that proves the candidate
 * cannot see the answers.
 *
 * ⚠ Once anybody has sat the paper every control here goes read-only. That is the server's rule,
 * not the screen's — the screen only has to stop offering a button that will be refused.
 */
export default function RecruitmentTestBuilderPage() {
  const params = useParams();
  const testId = params?.id as string;
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [questionDialogOpen, setQuestionDialogOpen] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<RecruitmentTestQuestion | null>(null);
  const [newSectionName, setNewSectionName] = useState('');

  const testQuery = useQuery({
    queryKey: ['hr', 'recruitment-test', testId],
    queryFn: () => tests.getTest(testId),
    enabled: !!testId,
  });

  const test = testQuery.data;
  const locked = test?.hasSittings ?? false;

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test', testId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-tests'] });
  };

  const setActive = useMutation({
    mutationFn: (active: boolean) => (active ? tests.activate(testId) : tests.retire(testId)),
    onSuccess: (_data, active) => {
      invalidate();
      toast({ title: active ? 'Test activated' : 'Test retired' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not change the test',
        // ⚠ The server's message names the question that cannot be marked. Show it verbatim —
        // replacing it with "activation failed" throws away the only useful part.
        description: error?.message ?? 'The paper could not be activated.',
        variant: 'destructive',
      }),
  });

  const addSection = useMutation({
    mutationFn: () =>
      tests.addSection({
        recruitmentTestId: testId,
        name: newSectionName.trim(),
        description: null,
        displayOrder: (test?.sections.length ?? 0) + 1,
      }),
    onSuccess: () => {
      setNewSectionName('');
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not add the section', description: error?.message, variant: 'destructive' }),
  });

  const removeSection = useMutation({
    mutationFn: (id: string) => tests.deleteSection(id),
    onSuccess: () => {
      invalidate();
      toast({
        title: 'Section removed',
        description: 'Its questions are still on the paper, without a section.',
      });
    },
    onError: (error: any) =>
      toast({ title: 'Could not remove the section', description: error?.message, variant: 'destructive' }),
  });

  const removeQuestion = useMutation({
    mutationFn: (id: string) => tests.deleteQuestion(id),
    onSuccess: () => {
      invalidate();
      toast({ title: 'Question removed' });
    },
    onError: (error: any) =>
      toast({ title: 'Could not remove the question', description: error?.message, variant: 'destructive' }),
  });

  const writtenCount = useMemo(
    () => (test?.questions ?? []).filter((q) => !AUTO_MARKED_QUESTION_TYPES.includes(q.questionType)).length,
    [test],
  );

  if (testQuery.isLoading || !test) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={test.name}
        description={`${test.testCode} · ${test.questionCount} question(s) · ${test.totalPoints} mark(s)`}
        backHref="/administration/hr/recruitment/tests"
        actions={
          <div className="flex items-center gap-2">
            <Badge variant={test.isActive ? 'default' : 'secondary'}>
              {test.isActive ? 'Active' : 'Draft'}
            </Badge>
            <Button
              variant={test.isActive ? 'outline' : 'default'}
              disabled={setActive.isPending}
              onClick={() => setActive.mutate(!test.isActive)}
            >
              {setActive.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {test.isActive ? 'Retire' : 'Activate'}
            </Button>
          </div>
        }
      />

      {locked && (
        <Alert>
          <Lock className="h-4 w-4" />
          <AlertTitle>This paper has been sat</AlertTitle>
          <AlertDescription>
            It can no longer be changed. Editing a question after it has been answered would rewrite
            what somebody was marked on. To change the paper, retire it and build a new one.
          </AlertDescription>
        </Alert>
      )}

      {!test.isActive && !locked && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Not yet active</AlertTitle>
          <AlertDescription>
            A draft paper cannot be assigned to anybody. Activating it checks every question can
            actually be marked — a choice question with no correct answer, or a numeric one with no
            expected value, is refused and named.
          </AlertDescription>
        </Alert>
      )}

      {writtenCount > 0 && (
        <Alert>
          <FileQuestion className="h-4 w-4" />
          <AlertTitle>
            {writtenCount} written answer{writtenCount === 1 ? '' : 's'} on this paper
          </AlertTitle>
          <AlertDescription>
            Written answers are never marked by the system. Each sitting will wait in the marking
            queue until somebody has read them and the result is finalised.
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="questions">
        <TabsList>
          <TabsTrigger value="questions">Questions</TabsTrigger>
          <TabsTrigger value="sections">Sections</TabsTrigger>
          <TabsTrigger value="preview">
            <Eye className="mr-2 h-4 w-4" />
            Candidate preview
          </TabsTrigger>
        </TabsList>

        <TabsContent value="questions" className="mt-4 space-y-4">
          <div className="flex justify-end">
            <Button
              disabled={locked}
              onClick={() => {
                setEditingQuestion(null);
                setQuestionDialogOpen(true);
              }}
            >
              <Plus className="mr-2 h-4 w-4" />
              Add question
            </Button>
          </div>

          {test.questions.length === 0 ? (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  icon={FileQuestion}
                  title="No questions yet"
                  description="A paper with no questions cannot be activated — it could only ever score zero."
                />
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-3">
              {test.questions.map((question, index) => (
                <Card key={question.id}>
                  <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0 pb-3">
                    <div className="min-w-0">
                      <CardTitle className="text-base">
                        {index + 1}. {question.questionText}
                      </CardTitle>
                      <CardDescription className="mt-1 flex flex-wrap items-center gap-2">
                        <Badge variant="outline">
                          {RECRUITMENT_QUESTION_TYPE_LABELS[question.questionType]}
                        </Badge>
                        <span>
                          {question.points} mark{question.points === 1 ? '' : 's'}
                        </span>
                        {question.sectionName && <span>&middot; {question.sectionName}</span>}
                      </CardDescription>
                    </div>
                    <div className="flex shrink-0 items-center gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={locked}
                        onClick={() => {
                          setEditingQuestion(question);
                          setQuestionDialogOpen(true);
                        }}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={locked}
                        onClick={() => {
                          if (window.confirm('Remove this question from the paper?')) {
                            removeQuestion.mutate(question.id);
                          }
                        }}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </CardHeader>
                  {question.options.length > 0 && (
                    <CardContent className="pt-0">
                      <ul className="space-y-1 text-sm">
                        {question.options.map((option) => (
                          <li key={option.id} className="flex items-center gap-2">
                            {option.isCorrect ? (
                              <CheckCircle2 className="h-4 w-4 text-green-600 shrink-0" />
                            ) : (
                              <span className="h-4 w-4 shrink-0 rounded-full border" />
                            )}
                            <span className={option.isCorrect ? 'font-medium' : ''}>
                              {option.optionText}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </CardContent>
                  )}
                  {question.questionType === 'Numeric' && question.expectedAnswer && (
                    <CardContent className="pt-0 text-sm">
                      <span className="text-muted-foreground">Expected answer: </span>
                      <span className="font-medium">{question.expectedAnswer}</span>
                    </CardContent>
                  )}
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="sections" className="mt-4 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Sections</CardTitle>
              <CardDescription>
                Optional headings — &ldquo;Numerical reasoning&rdquo;, &ldquo;Situational
                judgement&rdquo;. Removing one leaves its questions on the paper, ungrouped.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex gap-2">
                <Input
                  value={newSectionName}
                  disabled={locked}
                  placeholder="Section name"
                  onChange={(event) => setNewSectionName(event.target.value)}
                />
                <Button
                  disabled={locked || addSection.isPending || newSectionName.trim().length === 0}
                  onClick={() => addSection.mutate()}
                >
                  {addSection.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Add
                </Button>
              </div>

              {test.sections.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No sections. A twenty-question aptitude paper does not need any.
                </p>
              ) : (
                <ul className="divide-y rounded-lg border">
                  {test.sections.map((section) => (
                    <li key={section.id} className="flex items-center justify-between gap-4 p-3">
                      <div>
                        <p className="font-medium">{section.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {test.questions.filter((q) => q.recruitmentTestSectionId === section.id).length}{' '}
                          question(s)
                        </p>
                      </div>
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={locked}
                        onClick={() => removeSection.mutate(section.id)}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="preview" className="mt-4">
          <RecruitmentTestPreview testId={testId} />
        </TabsContent>
      </Tabs>

      {test && (
        <RecruitmentQuestionDialog
          open={questionDialogOpen}
          onOpenChange={setQuestionDialogOpen}
          test={test}
          question={editingQuestion}
          onSaved={invalidate}
        />
      )}
    </div>
  );
}
