'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardPen, Loader2, Printer } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import type {
  CandidateTestQuestion,
  PaperAnswerPayload,
  RecruitmentTestSitting,
} from '@/types/hr/recruitment-tests';

interface DraftAnswer {
  selected: string[];
  numeric: string;
  transcript: string;
  points: string;
  comment: string;
}

const emptyDraft = (): DraftAnswer => ({ selected: [], numeric: '', transcript: '', points: '', comment: '' });

/** A datetime-local value for "now", in the browser's own time zone. */
const localNow = () => {
  const now = new Date();
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  return now.toISOString().slice(0, 16);
};

/**
 * Records a script sat on the printed paper (round 4, lane E6).
 *
 * ⚠ **The questions come from the CANDIDATE projection** — the same no-answers view a candidate is
 * served. Whoever types in a script should be recording what the candidate ticked, not looking at
 * the key while they do it; a form that showed the right answers beside each question invites a
 * "correction" of the script on the way in.
 *
 * ⚠ **Closed questions take what was ticked, never a mark.** The server marks them against the key
 * by exactly the rules an online sitting gets. Only the written answers carry a mark, and every one
 * of them must — the script has been marked by hand before it is entered.
 *
 * ⚠ **Every choice question uses tick boxes, including the one-answer kind.** On paper a candidate
 * can tick two boxes on a question that wanted one. Record what they did; the key marks it wrong.
 */
export default function RecordPaperSittingPage() {
  const searchParams = useSearchParams();
  const assignmentId = searchParams.get('assignmentId') ?? '';
  const testId = searchParams.get('testId') ?? '';
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [applicationId, setApplicationId] = useState(searchParams.get('applicationId') ?? '');
  const [satOn, setSatOn] = useState(localNow());
  const [venue, setVenue] = useState('');
  const [invigilatorId, setInvigilatorId] = useState<string | null>(null);
  const [markerNotes, setMarkerNotes] = useState('');
  const [answers, setAnswers] = useState<Record<string, DraftAnswer>>({});
  const [recorded, setRecorded] = useState<RecruitmentTestSitting | null>(null);

  const assignment = useQuery({
    queryKey: ['hr', 'recruitment-test-assignments', 'for-test', testId],
    queryFn: () => tests.getAssignments({ testId }),
    enabled: testId !== '',
    select: (rows) => rows.find((row) => row.id === assignmentId) ?? null,
  });

  const paper = useQuery({
    queryKey: ['hr', 'recruitment-test-preview', testId],
    queryFn: () => tests.preview(testId),
    enabled: testId !== '',
  });

  const candidates = useQuery({
    queryKey: ['hr', 'recruitment-test-assignment-candidates', assignmentId],
    queryFn: () => tests.getAssignmentCandidates(assignmentId),
    enabled: assignmentId !== '',
  });

  const questions: CandidateTestQuestion[] = paper.data?.questions ?? [];
  const written = questions.filter((q) => q.questionType === 'FreeText');

  useEffect(() => {
    if (!paper.data) return;
    const seeded: Record<string, DraftAnswer> = {};
    for (const q of paper.data.questions) seeded[q.id] = emptyDraft();
    setAnswers(seeded);
  }, [paper.data]);

  const update = (questionId: string, next: Partial<DraftAnswer>) =>
    setAnswers((current) => ({ ...current, [questionId]: { ...(current[questionId] ?? emptyDraft()), ...next } }));

  const chosen = candidates.data?.find((c) => c.jobApplicationId === applicationId) ?? null;

  const missingMarks = useMemo(
    () =>
      written.filter((q) => {
        const draft = answers[q.id];
        if (!draft || draft.points.trim() === '') return true;
        const value = Number(draft.points);
        return !Number.isFinite(value) || value < 0 || value > q.points;
      }).length,
    [written, answers],
  );

  const record = useMutation({
    mutationFn: () => {
      const payload: PaperAnswerPayload[] = questions
        .map((q): PaperAnswerPayload => {
          const draft = answers[q.id] ?? emptyDraft();
          if (q.questionType === 'FreeText') {
            return {
              questionId: q.id,
              selectedOptionIds: [],
              freeTextAnswer: draft.transcript.trim() || null,
              pointsAwarded: Number(draft.points),
              markerComment: draft.comment.trim() || null,
            };
          }
          if (q.questionType === 'Numeric') {
            return { questionId: q.id, selectedOptionIds: [], numericAnswer: draft.numeric.trim() || null };
          }
          return { questionId: q.id, selectedOptionIds: draft.selected };
        })
        // A closed question left blank on the script is sent as nothing: it is marked as
        // unanswered, and still counts towards the total.
        .filter(
          (a) => a.pointsAwarded !== undefined || a.selectedOptionIds.length > 0 || !!a.numericAnswer,
        );

      return tests.recordPaperSitting({
        assignmentId,
        jobApplicationId: applicationId,
        // ⚠ Converted to UTC here, so the server receives an instant rather than a wall-clock time
        // it would have to guess the zone of.
        satOn: new Date(satOn).toISOString(),
        venue: venue.trim() || null,
        invigilatedById: invigilatorId,
        markerNotes: markerNotes.trim() || null,
        answers: payload,
      });
    },
    onSuccess: (sitting) => {
      setRecorded(sitting);
      queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-assignment-candidates', assignmentId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-sittings'] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-assignments'] });
      toast({ title: 'Paper sitting recorded', description: 'Marked, finalised, and the application re-scored.' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not record the sitting',
        description: error?.message ?? 'Please check the entry and try again.',
        variant: 'destructive',
      }),
  });

  const reset = () => {
    setRecorded(null);
    setApplicationId('');
    setMarkerNotes('');
    const seeded: Record<string, DraftAnswer> = {};
    for (const q of questions) seeded[q.id] = emptyDraft();
    setAnswers(seeded);
  };

  if (!assignmentId || !testId) {
    return (
      <div className="p-6">
        <Alert variant="destructive">
          <AlertTitle>No assignment chosen</AlertTitle>
          <AlertDescription>
            Open this from an assignment on the Assessments page — a paper sitting is recorded against
            the assignment the candidate sat.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  if (paper.isLoading || candidates.isLoading || assignment.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (recorded) {
    return (
      <div className="mx-auto max-w-2xl space-y-6 p-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-green-600" />
              Recorded and finalised
            </CardTitle>
            <CardDescription>
              {recorded.candidateName} · {recorded.applicationNumber} · attempt {recorded.attemptNumber}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-3xl font-semibold">{recorded.scorePercent ?? 0}%</p>
            <p className="text-sm text-muted-foreground">
              {recorded.finalScore ?? 0} of {recorded.totalPoints ?? 0} marks
              {recorded.passed === true && ' — a pass.'}
              {recorded.passed === false && ' — below the pass mark.'}
            </p>
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline">
                <Link href={`/hr/recruitment/assessments/${recorded.id}`}>Open the script</Link>
              </Button>
              <Button onClick={reset}>Record another</Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6 pb-28">
      <PageHeader
        title="Record a paper sitting"
        description={
          assignment.data
            ? `${assignment.data.testName} · ${assignment.data.jobTitle ?? assignment.data.candidateName ?? ''}`
            : undefined
        }
        backHref="/hr/recruitment/assessments"
        actions={
          <Button variant="outline" asChild>
            <Link href={`/hr/recruitment/assessments/paper?testId=${testId}&variant=MarkingKey`}>
              <Printer className="mr-2 h-4 w-4" />
              Marking key
            </Link>
          </Button>
        }
      />

      <Alert>
        <ClipboardPen className="h-4 w-4" />
        <AlertTitle>Enter what the candidate ticked, not what they scored</AlertTitle>
        <AlertDescription>
          The closed questions are marked by the system against the key, by the same rules an online
          paper gets. Only the written answers take a mark — every one of them, 0 where nothing was
          written. The key is deliberately not shown here.
        </AlertDescription>
      </Alert>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The sitting</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <Label>Candidate</Label>
            <Select value={applicationId} onValueChange={setApplicationId}>
              <SelectTrigger>
                <SelectValue placeholder="Choose the candidate whose script this is" />
              </SelectTrigger>
              <SelectContent>
                {(candidates.data ?? []).map((c) => (
                  <SelectItem key={c.jobApplicationId} value={c.jobApplicationId} disabled={!c.canRecordPaperSitting}>
                    {c.candidateName} — {c.applicationNumber}
                    {c.canRecordPaperSitting ? '' : ' (cannot record)'}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {chosen && !chosen.canRecordPaperSitting && (
              <p className="text-sm text-destructive">{chosen.blockedReason}</p>
            )}
            {chosen?.canRecordPaperSitting && (
              <p className="text-xs text-muted-foreground">
                Attempt {chosen.attemptsUsed + 1} of {chosen.attemptsAllowed}.
              </p>
            )}
            {(candidates.data ?? []).length === 0 && (
              <p className="text-sm text-muted-foreground">
                This assignment reaches nobody — a vacancy-wide test reaches live applications only.
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="satOn">Sat on</Label>
            <Input id="satOn" type="datetime-local" value={satOn} onChange={(e) => setSatOn(e.target.value)} />
            <p className="text-xs text-muted-foreground">Checked against the test&apos;s window — the day it was sat, not today.</p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="venue">Venue</Label>
            <Input id="venue" value={venue} onChange={(e) => setVenue(e.target.value)} placeholder="Head Office, Conference Room B" />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label>Invigilator</Label>
            <EmployeePicker value={invigilatorId} onChange={(id) => setInvigilatorId(id)} placeholder="Who supervised the sitting (optional)" />
          </div>
        </CardContent>
      </Card>

      <div className="space-y-3">
        {questions.map((q, index) => {
          const draft = answers[q.id] ?? emptyDraft();
          const isWritten = q.questionType === 'FreeText';
          const pointsValue = Number(draft.points);
          const pointsBad =
            isWritten && draft.points.trim() !== '' && (!Number.isFinite(pointsValue) || pointsValue < 0 || pointsValue > q.points);

          return (
            <Card key={q.id}>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-normal">
                  <span className="font-semibold">{index + 1}.</span> {q.questionText}
                </CardTitle>
                <CardDescription className="flex flex-wrap items-center gap-2">
                  <span>
                    {q.points} mark{q.points === 1 ? '' : 's'}
                  </span>
                  {q.sectionName && <span>· {q.sectionName}</span>}
                  {isWritten && <Badge variant="outline">Marked by hand</Badge>}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {q.options.length > 0 ? (
                  <div className="space-y-2">
                    <p className="text-xs text-muted-foreground">Tick what the candidate ticked — nothing if they left it blank.</p>
                    {q.options.map((option) => (
                      <label key={option.id} className="flex items-center gap-3 text-sm">
                        <Checkbox
                          checked={draft.selected.includes(option.id)}
                          onCheckedChange={(checked) =>
                            update(q.id, {
                              selected:
                                checked === true
                                  ? [...draft.selected, option.id]
                                  : draft.selected.filter((id) => id !== option.id),
                            })
                          }
                        />
                        {option.optionText}
                      </label>
                    ))}
                  </div>
                ) : q.questionType === 'Numeric' ? (
                  <div className="space-y-1">
                    <Label className="text-xs text-muted-foreground">What the candidate wrote</Label>
                    <Input
                      className="max-w-[220px]"
                      value={draft.numeric}
                      onChange={(e) => update(q.id, { numeric: e.target.value })}
                      placeholder="Leave empty if blank"
                    />
                  </div>
                ) : (
                  <div className="grid gap-3 md:grid-cols-[140px_minmax(0,1fr)]">
                    <div className="space-y-1">
                      <Label className="text-xs">Marks (0 – {q.points})</Label>
                      <Input
                        type="number"
                        min={0}
                        max={q.points}
                        step="0.5"
                        value={draft.points}
                        onChange={(e) => update(q.id, { points: e.target.value })}
                      />
                      {pointsBad && <p className="text-xs text-destructive">Between 0 and {q.points}.</p>}
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs">Marker&apos;s comment</Label>
                      <Input value={draft.comment} onChange={(e) => update(q.id, { comment: e.target.value })} />
                    </div>
                    <div className="space-y-1 md:col-span-2">
                      <Label className="text-xs text-muted-foreground">Transcript of the answer (optional — the script is the record)</Label>
                      <Textarea rows={2} value={draft.transcript} onChange={(e) => update(q.id, { transcript: e.target.value })} />
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Marker&apos;s notes</CardTitle>
          <CardDescription>Kept on the result in the candidate&apos;s test record.</CardDescription>
        </CardHeader>
        <CardContent>
          <Textarea rows={2} value={markerNotes} onChange={(e) => setMarkerNotes(e.target.value)} />
        </CardContent>
      </Card>

      <div className="fixed inset-x-0 bottom-0 border-t bg-background/95 p-4 backdrop-blur">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-muted-foreground">
            {written.length > 0
              ? missingMarks > 0
                ? `${missingMarks} written answer${missingMarks === 1 ? '' : 's'} still need a mark.`
                : 'Every written answer has a mark.'
              : 'Every question on this paper is marked by the key.'}
          </p>
          <Button
            disabled={
              !applicationId || !chosen?.canRecordPaperSitting || missingMarks > 0 || record.isPending || questions.length === 0
            }
            onClick={() => record.mutate()}
          >
            {record.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Record and finalise
          </Button>
        </div>
      </div>
    </div>
  );
}
