'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  ArrowDown,
  ArrowUp,
  CheckCircle2,
  ClipboardCheck,
  Loader2,
  GraduationCap,
  MessageSquare,
  Plus,
  Star,
  Target,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { successionCandidateService } from '@/services/hr/succession.service';
import { DevelopmentPanel } from '@/components/hr/succession/DevelopmentPanel';
import type {
  SuccessionCandidate,
  SuccessionPlan,
} from '@/types/hr/succession';

const CANDIDATE_TYPES = ['Internal', 'External', 'Emergency'] as const;
const READINESS = [
  'ReadyNow',
  'ReadyIn12Months',
  'ReadyIn24Months',
  'ReadyIn36PlusMonths',
  'NotReady',
] as const;
const RETENTION = ['HighRisk', 'MediumRisk', 'LowRisk', 'Secure'] as const;
const PERFORMANCE = [
  'Outstanding',
  'ExceedsExpectations',
  'MeetsExpectations',
  'BelowExpectations',
  'Unsatisfactory',
] as const;
const POTENTIAL = ['HighPotential', 'MediumPotential', 'LowPotential'] as const;
const DISPOSITIONS = ['Support', 'Neutral', 'Oppose'] as const;

const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');
const options = (values: readonly string[]) =>
  values.map((v) => ({ value: v, label: spaced(v) }));
const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);

const READINESS_TONE: Record<string, string> = {
  ReadyNow: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  ReadyIn12Months: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  ReadyIn24Months: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  ReadyIn36PlusMonths: 'bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-200',
  NotReady: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

const RETENTION_TONE: Record<string, string> = {
  HighRisk: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
  MediumRisk: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  LowRisk: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Secure: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
};

const nominateSchema = z.object({
  employeeId: z.string().min(1, 'Choose the employee to nominate'),
  type: z.enum(CANDIDATE_TYPES),
  rank: z.coerce.number().int().min(1),
  currentReadiness: z.enum(READINESS),
  monthsToReady: z.coerce.number().int().min(0).max(600).optional(),
  isEmergencyOnly: z.boolean(),
  latestPerformanceRating: z.enum(PERFORMANCE).optional().or(z.literal('')),
  potentialRating: z.enum(POTENTIAL).optional().or(z.literal('')),
  retentionRisk: z.enum(RETENTION),
  strengths: z.string().max(2000).optional(),
  developmentGaps: z.string().max(2000).optional(),
  yearsInCurrentRole: z.coerce.number().int().min(0).max(80),
  yearsWithCompany: z.coerce.number().int().min(0).max(80),
  hasRelevantExperience: z.boolean(),
  willingToRelocate: z.boolean(),
  availableForPromotion: z.boolean(),
  riskMitigationPlan: z.string().max(2000).optional(),
});

type NominateValues = z.input<typeof nominateSchema>;

/**
 * The successors on a plan: nominating, ranking, assessing, recommending, selecting, and the
 * feedback panel each candidate collects.
 *
 * ⚠ **The order matters and the UI has to show it.** Selecting a candidate is refused unless they
 * have been assessed *and* recommended — so the Select button explains its own precondition rather
 * than failing with a bare 400. Assessment carries no assessor field: the assessor is the signed-in
 * user, which is the fix for the hole this slice closed.
 */
export function CandidatesPanel({ plan }: { plan: SuccessionPlan }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [nominating, setNominating] = useState(false);
  const [assessing, setAssessing] = useState<SuccessionCandidate | null>(null);
  const [assessNotes, setAssessNotes] = useState('');
  const [recommend, setRecommend] = useState(true);
  const [developmentFor, setDevelopmentFor] = useState<SuccessionCandidate | null>(null);
  const [feedbackFor, setFeedbackFor] = useState<SuccessionCandidate | null>(null);
  const [feedbackNote, setFeedbackNote] = useState('');
  const [disposition, setDisposition] = useState<'Support' | 'Neutral' | 'Oppose'>('Support');

  const { data: candidates, isLoading } = useQuery({
    queryKey: ['succession-candidates', plan.id],
    queryFn: () => successionCandidateService.getByPlan(plan.id),
  });

  const rows = [...(candidates ?? [])].sort((a, b) => a.rank - b.rank);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['succession-candidates', plan.id] });
    await queryClient.invalidateQueries({ queryKey: ['succession-plans', plan.id] });
  };

  const fail = (fallback: string) => (error: any) => {
    const status = error?.response?.status;
    toast({
      variant: 'destructive',
      title: status === 403 ? 'That is not yours to do' : fallback,
      description: error?.response?.data?.detail ?? error?.message,
    });
  };

  const form = useForm<NominateValues>({
    resolver: zodResolver(nominateSchema),
    defaultValues: {
      employeeId: '',
      type: 'Internal',
      rank: rows.length + 1,
      currentReadiness: 'ReadyIn12Months',
      monthsToReady: undefined,
      isEmergencyOnly: false,
      latestPerformanceRating: '',
      potentialRating: '',
      retentionRisk: 'MediumRisk',
      strengths: '',
      developmentGaps: '',
      yearsInCurrentRole: 0,
      yearsWithCompany: 0,
      hasRelevantExperience: false,
      willingToRelocate: false,
      availableForPromotion: true,
      riskMitigationPlan: '',
    },
  });

  const nominate = useMutation({
    mutationFn: (values: NominateValues) =>
      successionCandidateService.create({
        successionPlanId: plan.id,
        employeeId: values.employeeId,
        type: values.type,
        rank: Number(values.rank),
        currentReadiness: values.currentReadiness,
        monthsToReady:
          values.monthsToReady === undefined || values.monthsToReady === null
            ? null
            : Number(values.monthsToReady),
        isEmergencyOnly: values.isEmergencyOnly,
        latestPerformanceRating: (orNull(values.latestPerformanceRating) as any) ?? null,
        potentialRating: (orNull(values.potentialRating) as any) ?? null,
        strengths: orNull(values.strengths),
        developmentGaps: orNull(values.developmentGaps),
        yearsInCurrentRole: Number(values.yearsInCurrentRole),
        yearsWithCompany: Number(values.yearsWithCompany),
        hasRelevantExperience: values.hasRelevantExperience,
        willingToRelocate: values.willingToRelocate,
        availableForPromotion: values.availableForPromotion,
        retentionRisk: values.retentionRisk,
        riskMitigationPlan: orNull(values.riskMitigationPlan),
      }),
    onSuccess: async () => {
      await refresh();
      setNominating(false);
      form.reset();
      toast({ title: 'Candidate nominated' });
    },
    onError: fail('Could not nominate this candidate'),
  });

  const assess = useMutation({
    mutationFn: (candidate: SuccessionCandidate) =>
      successionCandidateService.assess(candidate.id, {
        candidateId: candidate.id,
        assessmentNotes: orNull(assessNotes),
        isRecommended: recommend,
        recommendationNotes: recommend ? orNull(assessNotes) : null,
      }),
    onSuccess: async () => {
      await refresh();
      setAssessing(null);
      setAssessNotes('');
      toast({ title: 'Assessment recorded' });
    },
    onError: fail('Could not record the assessment'),
  });

  const select = useMutation({
    mutationFn: (candidate: SuccessionCandidate) =>
      successionCandidateService.select(candidate.id),
    onSuccess: async () => {
      await refresh();
      toast({
        title: 'Candidate selected',
        description: 'Any previously selected candidate on this plan has been stood down.',
      });
    },
    onError: fail('Could not select this candidate'),
  });

  const reRank = useMutation({
    mutationFn: (updates: { id: string; rank: number }[]) =>
      successionCandidateService.bulkUpdateRanks(updates),
    onSuccess: refresh,
    onError: fail('Could not reorder the candidates'),
  });

  const addFeedback = useMutation({
    mutationFn: (candidate: SuccessionCandidate) =>
      successionCandidateService.addFeedback(candidate.id, {
        note: feedbackNote,
        disposition,
      }),
    onSuccess: async () => {
      await refresh();
      setFeedbackFor(null);
      setFeedbackNote('');
      toast({ title: 'Feedback recorded' });
    },
    onError: fail('Could not record the feedback'),
  });

  /** Swap this candidate's rank with its neighbour — one PATCH carrying both rows. */
  const move = (index: number, direction: -1 | 1) => {
    const target = rows[index + direction];
    if (!target) return;
    reRank.mutate([
      { id: rows[index].id, rank: target.rank },
      { id: target.id, rank: rows[index].rank },
    ]);
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button onClick={() => { form.setValue('rank', rows.length + 1); setNominating(true); }}>
          <Plus className="mr-2 h-4 w-4" />
          Nominate a successor
        </Button>
      </div>

      <Card>
        <CardContent className="p-0">
          {rows.length === 0 ? (
            <EmptyState
              icon={Users}
              title="No successors identified"
              description="A plan with nobody on it records the risk but does not reduce it."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-24">Rank</TableHead>
                  <TableHead>Candidate</TableHead>
                  <TableHead>Readiness</TableHead>
                  <TableHead>Performance</TableHead>
                  <TableHead>Potential</TableHead>
                  <TableHead>Retention</TableHead>
                  <TableHead>Feedback</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((c, i) => (
                  <TableRow key={c.id}>
                    <TableCell>
                      <div className="flex items-center gap-1">
                        <span className="w-4">{c.rank}</span>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-6 w-6"
                          disabled={i === 0 || reRank.isPending}
                          onClick={() => move(i, -1)}
                        >
                          <ArrowUp className="h-3 w-3" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-6 w-6"
                          disabled={i === rows.length - 1 || reRank.isPending}
                          onClick={() => move(i, 1)}
                        >
                          <ArrowDown className="h-3 w-3" />
                        </Button>
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2 font-medium">
                        {c.employeeName}
                        {c.isSelected && (
                          <Badge className="gap-1">
                            <Star className="h-3 w-3" />
                            Selected
                          </Badge>
                        )}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {c.employeeNumber}
                        {c.employeePosition && ` · ${c.employeePosition}`}
                        {c.isEmergencyOnly && ' · emergency only'}
                        {c.type !== 'Internal' && ` · ${c.type}`}
                      </div>
                      {c.isRecommended && (
                        <div className="mt-1 text-xs text-emerald-700 dark:text-emerald-400">
                          Recommended{c.recommendedByName ? ` by ${c.recommendedByName}` : ''}
                        </div>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className={READINESS_TONE[c.currentReadiness] ?? ''}>
                        {spaced(c.currentReadiness)}
                      </Badge>
                      {c.monthsToReady != null && (
                        <div className="mt-0.5 text-xs text-muted-foreground">
                          {c.monthsToReady} months
                        </div>
                      )}
                    </TableCell>
                    <TableCell>{spaced(c.latestPerformanceRating)}</TableCell>
                    <TableCell>{spaced(c.potentialRating)}</TableCell>
                    <TableCell>
                      <Badge variant="outline" className={RETENTION_TONE[c.retentionRisk] ?? ''}>
                        {spaced(c.retentionRisk)}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {c.feedback.length === 0 ? (
                        <span className="text-xs text-muted-foreground">None</span>
                      ) : (
                        <span className="text-sm">
                          {c.feedback.filter((f) => f.disposition === 'Support').length} for /{' '}
                          {c.feedback.filter((f) => f.disposition === 'Oppose').length} against
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setAssessing(c);
                            setAssessNotes(c.assessmentNotes ?? '');
                            setRecommend(c.isRecommended);
                          }}
                        >
                          <ClipboardCheck className="mr-1 h-3.5 w-3.5" />
                          Assess
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setDevelopmentFor(c)}>
                          <GraduationCap className="mr-1 h-3.5 w-3.5" />
                          Development
                          {c.developmentActivities.length > 0 && (
                            <span className="ml-1 text-xs text-muted-foreground">
                              ({c.developmentActivities.length})
                            </span>
                          )}
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setFeedbackFor(c)}>
                          <MessageSquare className="mr-1 h-3.5 w-3.5" />
                          Feedback
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={!c.isRecommended || c.isSelected || select.isPending}
                          // The server refuses an unrecommended candidate. Saying so up front beats
                          // a bare 400 after the click.
                          title={
                            c.isSelected
                              ? 'Already selected'
                              : c.isRecommended
                                ? 'Name this candidate as the intended successor'
                                : 'A candidate must be assessed and recommended before they can be selected'
                          }
                          onClick={() => select.mutate(c)}
                        >
                          <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
                          Select
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {rows.some((c) => c.competencyGaps.length > 0) && (
        <Card>
          <CardContent className="space-y-3 p-4">
            <h4 className="flex items-center gap-2 text-sm font-medium">
              <Target className="h-4 w-4" />
              Competency gaps
            </h4>
            {rows
              .filter((c) => c.competencyGaps.length > 0)
              .map((c) => (
                <div key={c.id} className="text-sm">
                  <div className="font-medium">{c.employeeName}</div>
                  <ul className="ml-4 list-disc text-muted-foreground">
                    {c.competencyGaps.map((g) => (
                      <li key={g.id}>
                        {g.competencyName}: at {g.currentLevel}, needs {g.requiredLevel}
                        {/* gapSize is computed server-side; do not recompute it here. */}
                        {g.gapSize > 0 && ` (short by ${g.gapSize})`}
                        {g.addressed && ' — addressed'}
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
          </CardContent>
        </Card>
      )}

      {/* ── Nominate ─────────────────────────────────────────────────────────── */}
      <Dialog open={nominating} onOpenChange={setNominating}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Nominate a successor</DialogTitle>
            <DialogDescription>
              Nominating records that someone is a candidate. It does not recommend them — that is a
              separate assessment, and only a recommended candidate can be selected.
            </DialogDescription>
          </DialogHeader>

          <form
            className="space-y-4"
            onSubmit={form.handleSubmit((v) => nominate.mutate(v))}
          >
            <FieldRow>
              <EmployeePickerField form={form} name="employeeId" label="Employee" required />
              <SelectField form={form} name="type" label="Candidate type" required options={options(CANDIDATE_TYPES)} />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="currentReadiness"
                label="Readiness"
                required
                options={options(READINESS)}
              />
              <NumberField form={form} name="monthsToReady" label="Months to ready" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="latestPerformanceRating"
                label="Latest performance rating"
                options={options(PERFORMANCE)}
                allowEmpty
                emptyLabel="Not rated"
              />
              <SelectField
                form={form}
                name="potentialRating"
                label="Potential"
                options={options(POTENTIAL)}
                allowEmpty
                emptyLabel="Not rated"
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="retentionRisk"
                label="Retention risk"
                required
                options={options(RETENTION)}
              />
              <NumberField form={form} name="rank" label="Rank" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="yearsInCurrentRole" label="Years in current role" required />
              <NumberField form={form} name="yearsWithCompany" label="Years with the company" required />
            </FieldRow>
            <div className="grid gap-3 sm:grid-cols-2">
              <SwitchField form={form} name="hasRelevantExperience" label="Has relevant experience" />
              <SwitchField form={form} name="willingToRelocate" label="Willing to relocate" />
              <SwitchField form={form} name="availableForPromotion" label="Available for promotion" />
              <SwitchField form={form} name="isEmergencyOnly" label="Emergency cover only" />
            </div>
            <TextareaField form={form} name="strengths" label="Strengths" />
            <TextareaField form={form} name="developmentGaps" label="Development gaps" />
            <TextareaField form={form} name="riskMitigationPlan" label="Retention plan" />

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setNominating(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={nominate.isPending}>
                {nominate.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Nominate
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Assess ───────────────────────────────────────────────────────────── */}
      <Dialog open={!!assessing} onOpenChange={(open) => !open && setAssessing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assess {assessing?.employeeName}</DialogTitle>
            <DialogDescription>
              Recorded against your sign-in — there is no field to name a different assessor, by
              design. Recommending a candidate is what allows them to be selected.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <Textarea
              value={assessNotes}
              onChange={(e) => setAssessNotes(e.target.value)}
              placeholder="What the assessment found."
              rows={4}
            />
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={recommend}
                onChange={(e) => setRecommend(e.target.checked)}
                className="h-4 w-4"
              />
              Recommend this candidate
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssessing(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => assessing && assess.mutate(assessing)}
              disabled={assess.isPending}
            >
              {assess.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record assessment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Development ──────────────────────────────────────────────────────── */}
      <Dialog open={!!developmentFor} onOpenChange={(open) => !open && setDevelopmentFor(null)}>
        <DialogContent className="max-h-[85vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Development plan — {developmentFor?.employeeName}</DialogTitle>
            <DialogDescription>
              The work that turns a candidate into a successor, and the milestones it is tracked
              against.
            </DialogDescription>
          </DialogHeader>
          {developmentFor && <DevelopmentPanel candidate={developmentFor} />}
        </DialogContent>
      </Dialog>

      {/* ── Feedback ─────────────────────────────────────────────────────────── */}
      <Dialog open={!!feedbackFor} onOpenChange={(open) => !open && setFeedbackFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Feedback on {feedbackFor?.employeeName}</DialogTitle>
            <DialogDescription>
              Recorded against your sign-in. Existing feedback is listed below.
            </DialogDescription>
          </DialogHeader>

          {feedbackFor && feedbackFor.feedback.length > 0 && (
            <div className="max-h-48 space-y-2 overflow-y-auto rounded-md border p-3">
              {feedbackFor.feedback.map((f) => (
                <div key={f.id} className="text-sm">
                  <span className="font-medium">{f.reviewerName}</span>{' '}
                  <Badge variant="outline">{f.disposition}</Badge>
                  <p className="text-muted-foreground">{f.note}</p>
                </div>
              ))}
            </div>
          )}

          <div className="space-y-3">
            <div className="flex gap-2">
              {DISPOSITIONS.map((d) => (
                <Button
                  key={d}
                  type="button"
                  size="sm"
                  variant={disposition === d ? 'default' : 'outline'}
                  onClick={() => setDisposition(d)}
                >
                  {d}
                </Button>
              ))}
            </div>
            <Textarea
              value={feedbackNote}
              onChange={(e) => setFeedbackNote(e.target.value)}
              placeholder="Your view on this candidate."
              rows={3}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setFeedbackFor(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => feedbackFor && addFeedback.mutate(feedbackFor)}
              disabled={addFeedback.isPending || feedbackNote.trim() === ''}
            >
              {addFeedback.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record feedback
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
