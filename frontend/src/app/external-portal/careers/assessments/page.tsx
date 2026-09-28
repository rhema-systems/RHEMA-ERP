'use client';

import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { CheckCircle2, ClipboardList, Clock, FileQuestion, Loader2, Play } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { candidateService } from '@/services/hr/careers.service';
import {
  RECRUITMENT_SITTING_STATUS_LABELS,
  type CandidateAssessmentSummary,
} from '@/types/hr/recruitment-tests';

const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : null;

/**
 * The candidate's assessments.
 *
 * ⚠ Every card that cannot be started says WHY, in the server's own words — not open yet, closed,
 * attempts used up. A disabled button with no reason is the commonest complaint in this
 * programme's screen walks, and the server computes the sentence precisely so this page does not
 * have to guess at it.
 */
export default function CandidateAssessmentsPage() {
  const router = useRouter();
  const { toast } = useToast();

  const list = useQuery({
    queryKey: ['candidate', 'assessments'],
    queryFn: () => candidateService.getAssessments(),
  });

  const start = useMutation({
    mutationFn: (assessment: CandidateAssessmentSummary) =>
      assessment.inProgressSittingId
        ? candidateService.getSitting(assessment.inProgressSittingId)
        : candidateService.startAssessment(assessment.assignmentId),
    onSuccess: (sitting) => {
      router.push(`/external-portal/careers/assessments/${sitting.sittingId}`);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not open the assessment',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  const rows = list.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">My assessments</h1>
        <p className="text-muted-foreground mt-2">
          Tests you have been asked to complete as part of an application.
        </p>
      </div>

      {list.isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : rows.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={ClipboardList}
              title="Nothing to complete"
              description="When an employer asks you to sit an assessment it will appear here, and you will get an email."
            />
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {rows.map((assessment) => {
            const resumable = !!assessment.inProgressSittingId;
            const opens = formatDateTime(assessment.opensAt);
            const closes = formatDateTime(assessment.closesAt);

            return (
              <Card key={`${assessment.assignmentId}-${assessment.jobApplicationId}`}>
                <CardHeader>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <CardTitle className="text-base">{assessment.testName}</CardTitle>
                      <CardDescription>
                        {assessment.jobTitle} &middot; {assessment.applicationNumber}
                      </CardDescription>
                    </div>
                    {assessment.isRequired ? (
                      <Badge variant="default">Required</Badge>
                    ) : (
                      <Badge variant="outline">Optional</Badge>
                    )}
                  </div>
                </CardHeader>

                <CardContent className="space-y-4">
                  {assessment.description && (
                    <p className="text-sm text-muted-foreground">{assessment.description}</p>
                  )}

                  <div className="flex flex-wrap gap-4 text-sm">
                    <span className="inline-flex items-center gap-1">
                      <FileQuestion className="h-4 w-4 text-muted-foreground" />
                      {assessment.questionCount} question{assessment.questionCount === 1 ? '' : 's'}
                    </span>
                    <span className="inline-flex items-center gap-1">
                      <Clock className="h-4 w-4 text-muted-foreground" />
                      {assessment.durationMinutes
                        ? `${assessment.durationMinutes} minutes`
                        : 'No time limit'}
                    </span>
                    <span className="text-muted-foreground">
                      Attempt {Math.min(assessment.attemptsUsed + (resumable ? 0 : 1), assessment.attemptsAllowed)}{' '}
                      of {assessment.attemptsAllowed}
                    </span>
                  </div>

                  {(opens || closes) && (
                    <p className="text-xs text-muted-foreground">
                      {opens && <>Opens {opens}. </>}
                      {closes && <>Closes {closes}.</>}
                    </p>
                  )}

                  {/* ⚠ Released only once the employer has finalised the result. */}
                  {assessment.releasedScorePercent != null && (
                    <div className="flex items-center gap-2 rounded-lg bg-muted p-3 text-sm">
                      <CheckCircle2 className="h-4 w-4 text-green-600" />
                      <span>
                        Your result: <strong>{assessment.releasedScorePercent}%</strong>
                        {assessment.passed === true && ' — you met the pass mark.'}
                      </span>
                    </div>
                  )}

                  {assessment.lastStatus === 'AwaitingMarking' && (
                    <p className="text-sm text-muted-foreground">
                      Submitted {formatDateTime(assessment.lastSubmittedAt)}. Your written answers are
                      being read by the recruitment team.
                    </p>
                  )}

                  {assessment.blockedReason ? (
                    <div className="rounded-lg border border-dashed p-3 text-sm text-muted-foreground">
                      {assessment.blockedReason}
                      {assessment.lastStatus && (
                        <Badge variant="outline" className="ml-2">
                          {RECRUITMENT_SITTING_STATUS_LABELS[assessment.lastStatus]}
                        </Badge>
                      )}
                    </div>
                  ) : (
                    <Button
                      className="w-full"
                      disabled={start.isPending}
                      onClick={() => start.mutate(assessment)}
                    >
                      {start.isPending ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Play className="mr-2 h-4 w-4" />
                      )}
                      {resumable ? 'Resume' : 'Start'}
                    </Button>
                  )}

                  {!assessment.blockedReason && !resumable && assessment.durationMinutes && (
                    <p className="text-xs text-muted-foreground">
                      The clock starts as soon as you begin and keeps running if you close the page.
                      Start only when you have {assessment.durationMinutes} minutes free.
                    </p>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
