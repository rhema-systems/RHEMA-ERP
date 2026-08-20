'use client';

/**
 * The exit interview — what the leaver said on the way out.
 *
 * ⚠ Not an FRD requirement; added at the client's request. Two things about this form are load-
 * bearing rather than cosmetic:
 *
 * 1. **A rating left alone is not a zero.** Every rating is nullable and runs 1–5, and "not asked"
 *    is a real answer that the averages depend on. So the scale offers a "not asked" position and
 *    the form sends `null`, never 0.
 * 2. **Declining clears everything.** Marking the interview declined wipes every answer on the
 *    server, deliberately, so a part-filled form cannot leave ratings behind to be averaged as
 *    though somebody had really said them. The form warns before doing it.
 */

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, MessageSquareOff, Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { separationService } from '@/services/hr/separation.service';
import {
  EXIT_INTERVIEW_REASONS,
  type ExitInterviewReason,
  type RecordExitInterviewPayload,
} from '@/types/hr/separation';

const NOT_ASKED = 'not-asked';

/** The four things the interview scores, kept in one place so the form and the read view agree. */
const RATINGS = [
  { key: 'overallExperienceRating', label: 'Overall experience' },
  { key: 'managementRating', label: 'Management and supervision' },
  { key: 'payAndBenefitsRating', label: 'Pay and benefits' },
  { key: 'careerDevelopmentRating', label: 'Career development' },
] as const;

type RatingKey = (typeof RATINGS)[number]['key'];

const YES_NO = [
  { key: 'wouldRecommendEmployer', label: 'Would recommend TDC as an employer' },
  { key: 'wouldConsiderReturning', label: 'Would consider returning in future' },
] as const;

type YesNoKey = (typeof YES_NO)[number]['key'];

interface FormState {
  conductedOn: string;
  conductedByName: string;
  primaryReason: string;
  primaryReasonDetail: string;
  ratings: Record<RatingKey, string>;
  yesNo: Record<YesNoKey, string>;
  whatWorkedWell: string;
  whatShouldChange: string;
  additionalComments: string;
}

const EMPTY: FormState = {
  conductedOn: '',
  conductedByName: '',
  primaryReason: '',
  primaryReasonDetail: '',
  ratings: {
    overallExperienceRating: NOT_ASKED,
    managementRating: NOT_ASKED,
    payAndBenefitsRating: NOT_ASKED,
    careerDevelopmentRating: NOT_ASKED,
  },
  yesNo: { wouldRecommendEmployer: NOT_ASKED, wouldConsiderReturning: NOT_ASKED },
  whatWorkedWell: '',
  whatShouldChange: '',
  additionalComments: '',
};

interface Props {
  separationId: string;
  /** Approved or later. Before that there is no exit to interview anybody about. */
  canRecord: boolean;
}

export function ExitInterviewTab({ separationId, canRecord }: Props) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<FormState>(EMPTY);
  const [declining, setDeclining] = useState(false);
  const [declinedReason, setDeclinedReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const { data: interview, isLoading } = useQuery({
    queryKey: ['separation-exit-interview', separationId],
    queryFn: () => separationService.getExitInterview(separationId),
    enabled: canRecord,
  });

  // Load an existing interview into the form so amending edits it rather than starting blank.
  useEffect(() => {
    if (!interview) return;
    setForm({
      conductedOn: interview.conductedOn ?? '',
      conductedByName: interview.conductedByName ?? '',
      primaryReason: interview.primaryReason ?? '',
      primaryReasonDetail: interview.primaryReasonDetail ?? '',
      ratings: {
        overallExperienceRating: interview.overallExperienceRating?.toString() ?? NOT_ASKED,
        managementRating: interview.managementRating?.toString() ?? NOT_ASKED,
        payAndBenefitsRating: interview.payAndBenefitsRating?.toString() ?? NOT_ASKED,
        careerDevelopmentRating: interview.careerDevelopmentRating?.toString() ?? NOT_ASKED,
      },
      yesNo: {
        wouldRecommendEmployer: interview.wouldRecommendEmployer == null
          ? NOT_ASKED : String(interview.wouldRecommendEmployer),
        wouldConsiderReturning: interview.wouldConsiderReturning == null
          ? NOT_ASKED : String(interview.wouldConsiderReturning),
      },
      whatWorkedWell: interview.whatWorkedWell ?? '',
      whatShouldChange: interview.whatShouldChange ?? '',
      additionalComments: interview.additionalComments ?? '',
    });
    setDeclinedReason(interview.declinedReason ?? '');
  }, [interview]);

  const save = useMutation({
    mutationFn: (payload: RecordExitInterviewPayload) =>
      separationService.recordExitInterview(separationId, payload),
    onSuccess: () => {
      setError(null);
      setDeclining(false);
      queryClient.invalidateQueries({ queryKey: ['separation-exit-interview', separationId] });
    },
    onError: (e: Error) => setError(e.message),
  });

  // ⚠ "Not asked" must leave the field out — sending 0 would be the worst possible score, and
  // sending false would be a real "no" that the averages would count.
  const rating = (key: RatingKey) =>
    form.ratings[key] === NOT_ASKED ? null : Number(form.ratings[key]);
  const yesNo = (key: YesNoKey) =>
    form.yesNo[key] === NOT_ASKED ? null : form.yesNo[key] === 'true';

  const submit = () => {
    save.mutate({
      wasDeclined: false,
      conductedOn: form.conductedOn || null,
      conductedByName: form.conductedByName.trim() || null,
      primaryReason: (form.primaryReason || null) as ExitInterviewReason | null,
      primaryReasonDetail: form.primaryReasonDetail.trim() || null,
      overallExperienceRating: rating('overallExperienceRating'),
      managementRating: rating('managementRating'),
      payAndBenefitsRating: rating('payAndBenefitsRating'),
      careerDevelopmentRating: rating('careerDevelopmentRating'),
      wouldRecommendEmployer: yesNo('wouldRecommendEmployer'),
      wouldConsiderReturning: yesNo('wouldConsiderReturning'),
      whatWorkedWell: form.whatWorkedWell.trim() || null,
      whatShouldChange: form.whatShouldChange.trim() || null,
      additionalComments: form.additionalComments.trim() || null,
    });
  };

  const decline = () => {
    save.mutate({ wasDeclined: true, declinedReason: declinedReason.trim() });
  };

  if (!canRecord) {
    return (
      <Alert>
        <AlertDescription>
          An exit interview can be recorded once the separation has been approved. Before that the
          exit may still not happen, and there is nobody to interview about it.
        </AlertDescription>
      </Alert>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {interview?.wasDeclined && (
        <Alert>
          <MessageSquareOff className="h-4 w-4" />
          <AlertDescription>
            <span className="font-medium">The interview was declined.</span>{' '}
            {interview.declinedReason}
            <span className="mt-1 block text-xs text-muted-foreground">
              Recorded by {interview.recordedByName ?? 'unknown'}. Completing the form below and
              saving replaces this with a conducted interview.
            </span>
          </AlertDescription>
        </Alert>
      )}

      {interview && !interview.wasDeclined && (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <Badge variant="secondary">Recorded</Badge>
          <span>
            Conducted {interview.conductedOn ?? '—'}
            {interview.conductedByName ? ` by ${interview.conductedByName}` : ''}
          </span>
          {interview.recordedByName && <span>· Entered by {interview.recordedByName}</span>}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The interview</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="conductedOn">Date conducted</Label>
              <Input
                id="conductedOn"
                type="date"
                value={form.conductedOn}
                onChange={(e) => setForm({ ...form, conductedOn: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="conductedByName">Conducted by</Label>
              {/* Free text: the interviewer is often somebody without an ERP account. */}
              <Input
                id="conductedByName"
                placeholder="Name and role"
                value={form.conductedByName}
                onChange={(e) => setForm({ ...form, conductedByName: e.target.value })}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Main reason for leaving, in the employee&apos;s own account</Label>
            <Select
              value={form.primaryReason}
              onValueChange={(v) => setForm({ ...form, primaryReason: v })}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select a reason" />
              </SelectTrigger>
              <SelectContent>
                {EXIT_INTERVIEW_REASONS.map((r) => (
                  <SelectItem key={r.value} value={r.value}>{r.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              This is separate from the termination reason on the separation itself. The
              organisation may record a resignation; this records whether it was the pay or the
              manager.
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="reasonDetail">In their words</Label>
            <Textarea
              id="reasonDetail"
              rows={2}
              value={form.primaryReasonDetail}
              onChange={(e) => setForm({ ...form, primaryReasonDetail: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Ratings</CardTitle>
          <p className="text-sm text-muted-foreground">
            1 is poor and 5 is excellent. Leave a question as &ldquo;not asked&rdquo; if it was not
            put to them — it is kept out of the averages rather than counted as a low score.
          </p>
        </CardHeader>
        <CardContent className="space-y-4">
          {RATINGS.map((r) => (
            <div key={r.key} className="grid items-center gap-2 sm:grid-cols-[1fr,200px]">
              <Label htmlFor={r.key}>{r.label}</Label>
              <Select
                value={form.ratings[r.key]}
                onValueChange={(v) =>
                  setForm({ ...form, ratings: { ...form.ratings, [r.key]: v } })}
              >
                <SelectTrigger id={r.key}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NOT_ASKED}>Not asked</SelectItem>
                  {[1, 2, 3, 4, 5].map((n) => (
                    <SelectItem key={n} value={String(n)}>{n}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ))}

          {YES_NO.map((q) => (
            <div key={q.key} className="grid items-center gap-2 sm:grid-cols-[1fr,200px]">
              <Label htmlFor={q.key}>{q.label}</Label>
              <Select
                value={form.yesNo[q.key]}
                onValueChange={(v) => setForm({ ...form, yesNo: { ...form.yesNo, [q.key]: v } })}
              >
                <SelectTrigger id={q.key}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NOT_ASKED}>Not asked</SelectItem>
                  <SelectItem value="true">Yes</SelectItem>
                  <SelectItem value="false">No</SelectItem>
                </SelectContent>
              </Select>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">In their own words</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="workedWell">What worked well</Label>
            <Textarea
              id="workedWell"
              rows={3}
              value={form.whatWorkedWell}
              onChange={(e) => setForm({ ...form, whatWorkedWell: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="shouldChange">What should change</Label>
            <Textarea
              id="shouldChange"
              rows={3}
              value={form.whatShouldChange}
              onChange={(e) => setForm({ ...form, whatShouldChange: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="comments">Anything else</Label>
            <Textarea
              id="comments"
              rows={2}
              value={form.additionalComments}
              onChange={(e) => setForm({ ...form, additionalComments: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={submit} disabled={save.isPending}>
          {save.isPending
            ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            : <Save className="mr-2 h-4 w-4" />}
          {interview && !interview.wasDeclined ? 'Save changes' : 'Record interview'}
        </Button>
        {!declining && !interview?.wasDeclined && (
          <Button variant="outline" onClick={() => setDeclining(true)} disabled={save.isPending}>
            <MessageSquareOff className="mr-2 h-4 w-4" />
            Record as declined
          </Button>
        )}
      </div>

      {declining && (
        <Card className="border-amber-300">
          <CardHeader>
            <CardTitle className="text-base">Record the interview as declined</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {/* ⚠ Not a cosmetic warning: the server really does clear the answers, and it should. */}
            <Alert>
              <AlertDescription>
                Anything already entered above will be cleared. That is deliberate — a part-finished
                form must not leave ratings behind to be averaged as though the interview had
                actually happened.
              </AlertDescription>
            </Alert>
            <div className="space-y-1.5">
              <Label htmlFor="declinedReason">Why the interview was not held</Label>
              <Textarea
                id="declinedReason"
                rows={2}
                value={declinedReason}
                onChange={(e) => setDeclinedReason(e.target.value)}
              />
            </div>
            <div className="flex gap-2">
              <Button
                variant="destructive"
                onClick={decline}
                disabled={save.isPending || !declinedReason.trim()}
              >
                Confirm declined
              </Button>
              <Button variant="ghost" onClick={() => setDeclining(false)}>Cancel</Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
