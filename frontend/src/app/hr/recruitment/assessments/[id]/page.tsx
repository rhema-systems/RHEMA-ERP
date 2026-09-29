'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, ShieldCheck, XCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import {
  RECRUITMENT_QUESTION_TYPE_LABELS,
  RECRUITMENT_SITTING_MODE_LABELS,
  RECRUITMENT_SITTING_STATUS_LABELS,
  type SittingAnswer,
} from '@/types/hr/recruitment-tests';

const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—';

/**
 * One script, with its marks.
 *
 * ⚠ Only the written answers can be marked here. The closed questions were settled when the
 * candidate submitted, against the key that was on the paper at the time — letting a marker
 * overwrite one would mean the script no longer agrees with the marking key, and the server
 * refuses it.
 *
 * ⚠ **Finalise is the step that counts.** Awarding marks stores them; finalising writes the result
 * into the applicant's test ledger and re-scores the application, which is what applies the
 * vacancy's test weighting. A script marked but not finalised has changed nobody's shortlist.
 */
export default function SittingMarkingPage() {
  const params = useParams();
  const sittingId = params?.id as string;
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [marks, setMarks] = useState<Record<string, string>>({});
  const [comments, setComments] = useState<Record<string, string>>({});
  const [markerNotes, setMarkerNotes] = useState('');

  const sittingQuery = useQuery({
    queryKey: ['hr', 'recruitment-test-sitting', sittingId],
    queryFn: () => tests.getSitting(sittingId),
    enabled: !!sittingId,
  });

  const sitting = sittingQuery.data;

  useEffect(() => {
    if (!sitting) return;
    const nextMarks: Record<string, string> = {};
    const nextComments: Record<string, string> = {};
    for (const answer of sitting.answers) {
      if (answer.questionType !== 'FreeText') continue;
      nextMarks[answer.id] = answer.isManuallyMarked ? String(answer.pointsAwarded) : '';
      nextComments[answer.id] = answer.markerComment ?? '';
    }
    setMarks(nextMarks);
    setComments(nextComments);
    setMarkerNotes(sitting.markerNotes ?? '');
  }, [sitting]);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-sitting', sittingId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-sittings'] });
  };

  const markAnswer = useMutation({
    mutationFn: (answer: SittingAnswer) =>
      tests.markAnswer({
        answerId: answer.id,
        pointsAwarded: Number(marks[answer.id] ?? 0) || 0,
        markerComment: comments[answer.id]?.trim() || null,
      }),
    onSuccess: () => {
      invalidate();
      toast({ title: 'Mark recorded' });
    },
    onError: (error: any) =>
      toast({ title: 'Could not record the mark', description: error?.message, variant: 'destructive' }),
  });

  const finalise = useMutation({
    mutationFn: () => tests.finalise({ sittingId, markerNotes: markerNotes.trim() || null }),
    onSuccess: () => {
      invalidate();
      toast({
        title: 'Result finalised',
        description: "It is now in the candidate's test record and their application has been re-scored.",
      });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not finalise',
        description: error?.message ?? 'Every written answer needs a mark first.',
        variant: 'destructive',
      }),
  });

  if (sittingQuery.isLoading || !sitting) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const writtenAnswers = sitting.answers.filter((a) => a.questionType === 'FreeText');
  const closedAnswers = sitting.answers.filter((a) => a.questionType !== 'FreeText');
  const unmarked = writtenAnswers.filter((a) => !a.isManuallyMarked).length;
  const finalised = !!sitting.jobApplicantTestResultId;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={sitting.candidateName}
        description={`${sitting.testName} · ${sitting.applicationNumber} · attempt ${sitting.attemptNumber}`}
        backHref="/hr/recruitment/assessments"
        actions={
          <div className="flex items-center gap-2">
            <Badge variant="outline">{RECRUITMENT_SITTING_MODE_LABELS[sitting.mode] ?? sitting.mode}</Badge>
            <Badge variant={sitting.status === 'Expired' ? 'destructive' : 'default'}>
              {RECRUITMENT_SITTING_STATUS_LABELS[sitting.status]}
            </Badge>
          </div>
        }
      />

      {sitting.status === 'Expired' && (
        <Alert variant="destructive">
          <AlertTitle>The time allowed ran out</AlertTitle>
          <AlertDescription>
            This script was marked on the answers saved before the deadline. Questions the candidate
            never reached score nothing — which is not the same as having answered them wrongly.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Closed questions</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">
              {sitting.autoScore ?? 0}
              <span className="text-base font-normal text-muted-foreground">
                {' '}
                / {sitting.totalPoints ?? 0}
              </span>
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Written answers</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">
              {sitting.manualScore != null ? sitting.manualScore : '—'}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Result</CardDescription>
          </CardHeader>
          <CardContent>
            {/* ⚠ Only once finalised. Before that the figure is incomplete by construction. */}
            <p className="text-2xl font-semibold">
              {finalised && sitting.scorePercent != null ? `${sitting.scorePercent}%` : '—'}
            </p>
            {finalised && sitting.passed != null && (
              <Badge variant={sitting.passed ? 'default' : 'secondary'} className="mt-1">
                {sitting.passed ? 'Passed' : 'Below the pass mark'}
              </Badge>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Submitted</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm">{formatDateTime(sitting.submittedAt)}</p>
            <p className="text-xs text-muted-foreground">Started {formatDateTime(sitting.startedAt)}</p>
          </CardContent>
        </Card>
      </div>

      {writtenAnswers.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Written answers</CardTitle>
            <CardDescription>
              {unmarked === 0
                ? 'All marked.'
                : `${unmarked} still to mark. The result cannot be finalised until every one has a mark.`}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            {writtenAnswers.map((answer) => (
              <div key={answer.id} className="space-y-3 rounded-lg border p-4">
                <div>
                  <p className="font-medium">{answer.questionText}</p>
                  <p className="text-xs text-muted-foreground">
                    Worth {answer.questionPoints} mark{answer.questionPoints === 1 ? '' : 's'}
                  </p>
                </div>

                <div className="rounded-md bg-muted p-3 text-sm whitespace-pre-wrap">
                  {answer.freeTextAnswer?.trim() || (
                    <span className="text-muted-foreground italic">No answer was given.</span>
                  )}
                </div>

                <div className="grid gap-3 sm:grid-cols-[120px_minmax(0,1fr)_auto] sm:items-end">
                  <div className="space-y-1">
                    <Label htmlFor={`mark-${answer.id}`}>Marks</Label>
                    <Input
                      id={`mark-${answer.id}`}
                      type="number"
                      min={0}
                      max={answer.questionPoints}
                      step="0.5"
                      disabled={finalised}
                      value={marks[answer.id] ?? ''}
                      onChange={(event) =>
                        setMarks((current) => ({ ...current, [answer.id]: event.target.value }))
                      }
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor={`comment-${answer.id}`}>Comment</Label>
                    <Input
                      id={`comment-${answer.id}`}
                      disabled={finalised}
                      value={comments[answer.id] ?? ''}
                      onChange={(event) =>
                        setComments((current) => ({ ...current, [answer.id]: event.target.value }))
                      }
                    />
                  </div>
                  <Button
                    variant="outline"
                    disabled={finalised || markAnswer.isPending || (marks[answer.id] ?? '') === ''}
                    onClick={() => markAnswer.mutate(answer)}
                  >
                    {markAnswer.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Record
                  </Button>
                </div>

                {answer.isManuallyMarked && (
                  <p className="text-xs text-muted-foreground">
                    Marked: {answer.pointsAwarded} of {answer.questionPoints}
                  </p>
                )}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Closed questions</CardTitle>
          <CardDescription>
            Marked when the candidate submitted, against the key on the paper at the time. They
            cannot be changed here.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {closedAnswers.length === 0 ? (
            <p className="text-sm text-muted-foreground">This paper has no closed questions.</p>
          ) : (
            closedAnswers.map((answer) => (
              <div key={answer.id} className="flex items-start gap-3 rounded-lg border p-3">
                {answer.isCorrect ? (
                  <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-green-600" />
                ) : (
                  <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                )}
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{answer.questionText}</p>
                  <p className="text-xs text-muted-foreground">
                    {RECRUITMENT_QUESTION_TYPE_LABELS[answer.questionType]} &middot;{' '}
                    {answer.selectedOptionText ?? answer.numericAnswer ?? 'No answer given'}
                  </p>
                </div>
                <span className="shrink-0 text-sm">
                  {answer.pointsAwarded} / {answer.questionPoints}
                </span>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldCheck className="h-4 w-4" />
            Finalise
          </CardTitle>
          <CardDescription>
            Writes the result into the candidate&apos;s test record and re-scores their application,
            applying the vacancy&apos;s test weighting. Until this is done the mark has changed
            nobody&apos;s shortlist position.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="markerNotes">Notes</Label>
            <Textarea
              id="markerNotes"
              rows={2}
              disabled={finalised}
              value={markerNotes}
              onChange={(event) => setMarkerNotes(event.target.value)}
            />
          </div>

          {finalised ? (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertTitle>Finalised {formatDateTime(sitting.markedAt)}</AlertTitle>
              <AlertDescription>
                The result is in the candidate&apos;s test record and the application has been
                re-scored.
              </AlertDescription>
            </Alert>
          ) : (
            <Button disabled={unmarked > 0 || finalise.isPending} onClick={() => finalise.mutate()}>
              {finalise.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Finalise the result
            </Button>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
