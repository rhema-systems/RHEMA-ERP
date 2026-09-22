'use client';

import { useQuery } from '@tanstack/react-query';
import { Eye, Loader2, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import { RECRUITMENT_QUESTION_TYPE_LABELS } from '@/types/hr/recruitment-tests';

/**
 * The paper as the candidate is served it.
 *
 * ⚠ This is NOT a re-rendering of the authoring data with the answers hidden. It calls the
 * preview endpoint, which returns the candidate projection itself — the same types, from the same
 * code path, as a real sitting. A preview built from the authoring payload would prove nothing:
 * hiding a field in a component is not the same as the server never sending it.
 */
export function RecruitmentTestPreview({ testId }: { testId: string }) {
  const preview = useQuery({
    queryKey: ['hr', 'recruitment-test-preview', testId],
    queryFn: () => tests.preview(testId),
    enabled: !!testId,
  });

  if (preview.isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const paper = preview.data;
  if (!paper) return null;

  return (
    <div className="space-y-4">
      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>This is the actual payload a candidate receives</AlertTitle>
        <AlertDescription>
          It comes from the same projection a real sitting is served, so what is not on this page is
          not sent to the candidate&apos;s browser at all — not the correct choices, not the
          expected answers, not the explanations.
        </AlertDescription>
      </Alert>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Eye className="h-4 w-4" />
            {paper.testName}
          </CardTitle>
          <CardDescription>
            {paper.questions.length} question(s)
            {paper.durationMinutes ? ` · ${paper.durationMinutes} minutes` : ' · untimed'}
          </CardDescription>
        </CardHeader>
        {paper.instructions && (
          <CardContent>
            <div className="rounded-lg bg-muted p-4 text-sm whitespace-pre-wrap">
              {paper.instructions}
            </div>
          </CardContent>
        )}
      </Card>

      {paper.questions.map((question, index) => (
        <Card key={question.id}>
          <CardHeader className="pb-3">
            <CardTitle className="text-base font-normal">
              <span className="font-semibold">{index + 1}.</span> {question.questionText}
            </CardTitle>
            <CardDescription className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">
                {RECRUITMENT_QUESTION_TYPE_LABELS[question.questionType]}
              </Badge>
              <span>
                {question.points} mark{question.points === 1 ? '' : 's'}
              </span>
              {question.sectionName && <span>&middot; {question.sectionName}</span>}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {question.options.length > 0 ? (
              <ul className="space-y-2">
                {question.options.map((option) => (
                  <li key={option.id} className="flex items-center gap-2 text-sm">
                    <span className="h-4 w-4 shrink-0 rounded-full border" />
                    {option.optionText}
                  </li>
                ))}
              </ul>
            ) : question.questionType === 'Numeric' ? (
              <div className="h-9 w-40 rounded-md border bg-muted/40" />
            ) : (
              <div className="h-24 rounded-md border bg-muted/40" />
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
