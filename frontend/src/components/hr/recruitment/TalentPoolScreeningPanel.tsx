'use client';

/**
 * Round 4, lane B — screening the talent pool by criteria that mean something, then acting on the
 * answer without leaving the screen.
 *
 * Until this existed, the pool could only be ranked by a blind 40/30/20 rubric over experience,
 * work mode and availability, and there was **no path at all** from a pool member to an application
 * or an interview: an interviewee row needs an application id, and nothing created one. A recruiter
 * could find exactly the right person and then had to start again by hand.
 *
 * ⚠ The score here is the vacancy's OWN shortlisting criteria, run through the same engine that
 * scores applications — not a second rubric that resembles it. It is shown out of 100 and beside
 * the fit score, never merged into it (decision D-7): the two answer different questions.
 *
 * ⚠ A null score is not a zero. Zero means "measured, and missed everything"; null means the
 * criteria asked questions this record cannot answer. They are rendered differently on purpose.
 */

import { Fragment, useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarPlus,
  ChevronDown,
  ChevronRight,
  ListChecks,
  Loader2,
  Plus,
  Search,
  Send,
  X,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { GatedPhoto } from '@/components/hr/common/PhotoDialog';
import { AddressFields } from '@/components/reference/AddressFields';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { certificationService } from '@/services/hr/certification.service';
import { jobInterviewService } from '@/services/hr/interviews.service';
import { languageService } from '@/services/hr/language.service';
import { qualificationService, referenceDimensionService } from '@/services/hr/lookup.service';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import { skillService } from '@/services/hr/skill.service';
import { talentPoolService } from '@/services/hr/talent-pool.service';
import { geographyService } from '@/services/reference/geography.service';
import { GENDERS } from '@/types/hr/recruitment-pipeline';
import type {
  ShortlistingComparisonOperator,
  ShortlistingCriteriaType,
} from '@/types/hr/recruitment';
import type {
  AdHocScreeningCriterion,
  TalentPoolBulkResult,
  TalentPoolFilter,
  TalentPoolScreenResult,
  TalentPoolScreenRow,
} from '@/types/hr/talent-pool';

/** Radix Select refuses an empty value, so "nothing chosen" needs a sentinel. */
const NONE = '__none__';

type Mode = 'vacancy' | 'adhoc';

/** A value picked into an ad-hoc criterion: a catalogue row, or typed text. */
type PickedValue = { referenceId: string | null; label: string };

const blankDraft = (): AdHocScreeningCriterion => ({
  criteriaName: '',
  type: 'Skill',
  isMandatory: false,
  // ⚠ Exact, matching the server's default. VacancyCriteriaPanel learned this the hard way: a
  // panel defaulting to Contains against a service defaulting to Exact scores one way and
  // displays another.
  matchStrategy: 'Exact',
  matchMode: 'AnyMatched',
  weight: 1,
  comparisonOperator: null,
  values: [],
});

/** The score cell. Null, 0 and a real number are three different statements. */
function ScoreCell({ row }: { row: TalentPoolScreenRow }) {
  if (row.criteriaScore === null) {
    return (
      <span className="text-xs text-muted-foreground" title="The criteria asked questions this record cannot answer.">
        Not measurable
      </span>
    );
  }
  return (
    <span className="tabular-nums font-medium">
      {row.criteriaScore}
      <span className="text-xs font-normal text-muted-foreground"> / {row.criteriaScoreMax}</span>
    </span>
  );
}

export function TalentPoolScreeningPanel({
  canManage,
  /** The pool filter the recruiter is already looking at, so "screen these people" means these people. */
  poolFilter,
}: {
  canManage: boolean;
  poolFilter?: TalentPoolFilter;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [mode, setMode] = useState<Mode>('vacancy');
  const [vacancyId, setVacancyId] = useState('');
  const [includeNonMatching, setIncludeNonMatching] = useState(true);
  const [useCurrentFilter, setUseCurrentFilter] = useState(true);
  const [topN, setTopN] = useState(50);

  const [criteria, setCriteria] = useState<AdHocScreeningCriterion[]>([]);
  const [draftOpen, setDraftOpen] = useState(false);

  const [result, setResult] = useState<TalentPoolScreenResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const [actionNotes, setActionNotes] = useState('');
  const [sendEmail, setSendEmail] = useState(true);
  const [interviewId, setInterviewId] = useState('');
  const [bulkResult, setBulkResult] = useState<TalentPoolBulkResult | null>(null);

  const vacancies = useQuery({
    queryKey: ['hr', 'vacancies', 'active'],
    queryFn: () => jobVacancyService.getActive(),
  });

  // Only the interviews of the vacancy just screened: booking is against an application, and an
  // application belongs to one vacancy. Offering every interview in the tenant would be offering
  // a list where all but a handful refuse.
  const interviews = useQuery({
    queryKey: ['hr', 'interviews', 'by-vacancy', result?.vacancyId],
    queryFn: () => jobInterviewService.getByVacancy(result!.vacancyId!),
    enabled: !!result?.vacancyId,
  });

  const screen = useMutation({
    mutationFn: () => {
      const request = {
        filter: useCurrentFilter ? poolFilter : undefined,
        topN,
        includeNonMatching,
        criteria: mode === 'adhoc' ? criteria : undefined,
      };
      return mode === 'vacancy'
        ? talentPoolService.screenAgainstVacancy(vacancyId, request)
        : talentPoolService.screenAdHoc(request);
    },
    onSuccess: (data) => {
      setResult(data);
      setError(null);
      setSelected(new Set());
      setBulkResult(null);
    },
    onError: (e: any) => {
      // The refusal text is the answer here, not a toast to be dismissed — "this vacancy has no
      // criteria yet" is a sentence the recruiter has to act on.
      setResult(null);
      setError(e?.message ?? 'The screen could not be run.');
    },
  });

  const afterBulk = async (data: TalentPoolBulkResult, verb: string) => {
    setBulkResult(data);
    setSelected(new Set());
    await queryClient.invalidateQueries({ queryKey: ['hr', 'talent-pool'] });
    if (result && mode === 'vacancy' && vacancyId) {
      // Re-screen so "already applied" is true for the people just invited, rather than offering
      // to invite them again on the next click.
      screen.mutate();
    }
    toast({
      title: `${data.succeeded} candidate${data.succeeded === 1 ? '' : 's'} ${verb}`,
      description: data.skipped > 0 ? `${data.skipped} skipped — reasons below the table.` : undefined,
    });
  };

  const invite = useMutation({
    mutationFn: () =>
      talentPoolService.inviteToApply({
        jobVacancyId: result?.vacancyId ?? vacancyId,
        candidateIds: [...selected],
        notes: actionNotes.trim() || null,
        sendEmail,
      }),
    onSuccess: (data) => afterBulk(data, 'invited to apply'),
    onError: (e: any) =>
      toast({ title: 'Nobody was invited', description: e?.message, variant: 'destructive' }),
  });

  const book = useMutation({
    mutationFn: () =>
      talentPoolService.bookForInterview({
        jobInterviewId: interviewId,
        candidateIds: [...selected],
        notes: actionNotes.trim() || null,
      }),
    onSuccess: (data) => afterBulk(data, 'booked for interview'),
    onError: (e: any) =>
      toast({ title: 'Nobody was booked', description: e?.message, variant: 'destructive' }),
  });

  const rows = result?.rows ?? [];
  const allSelected = rows.length > 0 && rows.every((r) => selected.has(r.candidateId));
  const canRun = mode === 'vacancy' ? !!vacancyId : criteria.length > 0;

  const toggle = (id: string, on: boolean) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Screen the pool</CardTitle>
          <CardDescription>
            Score pooled candidates against a vacancy&apos;s own shortlisting criteria — the same engine
            that scores applications — then invite the ones worth approaching.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1.5">
              <Label>Screen by</Label>
              <Select value={mode} onValueChange={(v) => { setMode(v as Mode); setResult(null); setError(null); }}>
                <SelectTrigger className="w-52">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="vacancy">A vacancy&apos;s criteria</SelectItem>
                  <SelectItem value="adhoc">Criteria I type now</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {mode === 'vacancy' && (
              <div className="min-w-64 flex-1 space-y-1.5">
                <Label>Vacancy</Label>
                <Select value={vacancyId || NONE} onValueChange={(v) => setVacancyId(v === NONE ? '' : v)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose a vacancy" />
                  </SelectTrigger>
                  <SelectContent>
                    {(vacancies.data ?? []).length === 0 && (
                      <SelectItem value={NONE} disabled>
                        No active vacancies
                      </SelectItem>
                    )}
                    {(vacancies.data ?? []).map((v) => (
                      <SelectItem key={v.id} value={v.id}>
                        {v.jobTitle} · {v.vacancyNumber}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="screen-topn">Rows</Label>
              <Input
                id="screen-topn"
                type="number"
                min={1}
                max={500}
                className="w-24"
                value={topN}
                onChange={(e) => setTopN(Math.max(1, Math.min(500, Number(e.target.value) || 50)))}
              />
            </div>

            <Button disabled={!canRun || screen.isPending} onClick={() => screen.mutate()}>
              {screen.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Search className="mr-2 h-4 w-4" />}
              Run screen
            </Button>
          </div>

          <div className="flex flex-wrap gap-4 text-sm">
            <label className="flex items-center gap-2">
              <Checkbox checked={includeNonMatching} onCheckedChange={(v) => setIncludeNonMatching(v === true)} />
              Show candidates who miss a mandatory criterion
            </label>
            <label className="flex items-center gap-2">
              <Checkbox checked={useCurrentFilter} onCheckedChange={(v) => setUseCurrentFilter(v === true)} />
              Only the candidates the Pool tab is filtered to
            </label>
          </div>

          {mode === 'adhoc' && (
            <AdHocCriteriaEditor
              criteria={criteria}
              onChange={setCriteria}
              open={draftOpen}
              setOpen={setDraftOpen}
            />
          )}

          {error && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      {result && (
        <>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm">
                {result.jobTitle
                  ? `${result.jobTitle} · ${result.vacancyNumber}`
                  : 'Ad-hoc criteria'}
              </CardTitle>
              <CardDescription>
                {result.screenedCount} pooled candidate{result.screenedCount === 1 ? '' : 's'} screened ·{' '}
                {result.scoredCount} could be scored · {result.qualifiedCount} met every mandatory criterion
              </CardDescription>
            </CardHeader>
            <CardContent className="flex flex-wrap gap-2">
              {result.criteria.map((c, i) => (
                <Badge key={c.criteriaId ?? `${c.criteriaName}-${i}`} variant={c.isMandatory ? 'default' : 'secondary'}>
                  {c.criteriaName} · weight {c.weight}
                  {c.isMandatory ? ' · mandatory' : ''}
                  {c.acceptedValues ? ` · ${c.acceptedValues}` : ''}
                </Badge>
              ))}
            </CardContent>
          </Card>

          {canManage && selected.size > 0 && (
            <Card>
              <CardContent className="flex flex-wrap items-end gap-3 pt-6">
                <div className="text-sm font-medium">{selected.size} selected</div>

                <div className="min-w-44 flex-1 space-y-1.5">
                  <Label htmlFor="screen-notes">Note</Label>
                  <Input
                    id="screen-notes"
                    placeholder="Why you are approaching them — appears in the invitation"
                    value={actionNotes}
                    onChange={(e) => setActionNotes(e.target.value)}
                    maxLength={2000}
                  />
                </div>

                {result.vacancyId ? (
                  <>
                    <label className="flex h-10 items-center gap-2 text-sm">
                      <Checkbox checked={sendEmail} onCheckedChange={(v) => setSendEmail(v === true)} />
                      Email them
                    </label>
                    <Button disabled={invite.isPending} onClick={() => invite.mutate()}>
                      {invite.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                      Invite to apply
                    </Button>

                    <div className="space-y-1.5">
                      <Label>Interview</Label>
                      <Select value={interviewId || NONE} onValueChange={(v) => setInterviewId(v === NONE ? '' : v)}>
                        <SelectTrigger className="w-56">
                          <SelectValue placeholder="Choose a session" />
                        </SelectTrigger>
                        <SelectContent>
                          {(interviews.data ?? []).length === 0 && (
                            <SelectItem value={NONE} disabled>
                              This vacancy has no interviews
                            </SelectItem>
                          )}
                          {(interviews.data ?? []).map((i) => (
                            <SelectItem key={i.id} value={i.id}>
                              {i.interviewNumber} · {formatDate(i.scheduledDate)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <Button variant="outline" disabled={!interviewId || book.isPending} onClick={() => book.mutate()}>
                      {book.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CalendarPlus className="mr-2 h-4 w-4" />}
                      Book for interview
                    </Button>
                  </>
                ) : (
                  // An ad-hoc screen is not about any one vacancy, so there is nothing to invite
                  // them TO. Saying so beats a button that would have to ask which vacancy anyway.
                  <span className="text-sm text-muted-foreground">
                    Screen against a vacancy to invite these candidates to apply.
                  </span>
                )}

                <Button variant="ghost" onClick={() => setSelected(new Set())}>
                  Clear
                </Button>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardContent className="p-0">
              {rows.length === 0 ? (
                <div className="py-12">
                  <EmptyState
                    icon={ListChecks}
                    title="Nobody came back"
                    description={
                      includeNonMatching
                        ? 'No pooled candidate matched the filter. Widen the pool filter and screen again.'
                        : 'Nobody met every mandatory criterion. Tick "Show candidates who miss a mandatory criterion" to see the near misses.'
                    }
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      {canManage && (
                        <TableHead className="w-10">
                          <Checkbox
                            checked={allSelected}
                            aria-label="Select everyone on this screen"
                            onCheckedChange={(v) =>
                              setSelected((prev) => {
                                const next = new Set(prev);
                                rows.forEach((r) => (v === true ? next.add(r.candidateId) : next.delete(r.candidateId)));
                                return next;
                              })
                            }
                          />
                        </TableHead>
                      )}
                      <TableHead className="w-8" />
                      <TableHead>Candidate</TableHead>
                      <TableHead>Where</TableHead>
                      <TableHead className="w-24">Experience</TableHead>
                      <TableHead className="w-28">Available</TableHead>
                      <TableHead className="w-28 text-right">Criteria score</TableHead>
                      <TableHead className="w-36">Verdict</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((r) => (
                      <Fragment key={r.candidateId}>
                        <TableRow>
                          {canManage && (
                            <TableCell>
                              <Checkbox
                                checked={selected.has(r.candidateId)}
                                aria-label={`Select ${r.candidateName}`}
                                onCheckedChange={(v) => toggle(r.candidateId, v === true)}
                              />
                            </TableCell>
                          )}
                          <TableCell>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-6 w-6"
                              aria-label={expanded.has(r.candidateId) ? 'Hide the breakdown' : 'Show the breakdown'}
                              onClick={() =>
                                setExpanded((prev) => {
                                  const next = new Set(prev);
                                  if (next.has(r.candidateId)) next.delete(r.candidateId);
                                  else next.add(r.candidateId);
                                  return next;
                                })
                              }
                            >
                              {expanded.has(r.candidateId) ? (
                                <ChevronDown className="h-4 w-4" />
                              ) : (
                                <ChevronRight className="h-4 w-4" />
                              )}
                            </Button>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-3">
                              <GatedPhoto
                                endpoint={jobCandidateService.photoUrl(r.candidateId)}
                                enabled={r.hasPhoto}
                                alt={r.candidateName}
                                className="h-8 w-8"
                              />
                              <div>
                                <Link
                                  href={`/hr/recruitment/candidates/${r.candidateId}`}
                                  className="font-medium hover:underline"
                                >
                                  {r.candidateName}
                                </Link>
                                <div className="text-xs text-muted-foreground">
                                  {r.headline || r.candidateNumber}
                                </div>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">{r.city || '—'}</TableCell>
                          <TableCell className="text-sm">
                            {r.totalYearsExperience != null ? `${r.totalYearsExperience} yrs` : '—'}
                          </TableCell>
                          <TableCell className="text-sm">
                            {r.availableFrom ? formatDate(r.availableFrom) : '—'}
                          </TableCell>
                          <TableCell className="text-right">
                            <ScoreCell row={r} />
                          </TableCell>
                          <TableCell>
                            {r.alreadyApplied ? (
                              <Badge variant="secondary">Already applied</Badge>
                            ) : !r.allMandatoryPassed ? (
                              <Badge variant="destructive">Missed a mandatory</Badge>
                            ) : r.criteriaScore === null ? (
                              <Badge variant="outline">Nothing measurable</Badge>
                            ) : (
                              <Badge>Meets the mandatories</Badge>
                            )}
                          </TableCell>
                        </TableRow>
                        {expanded.has(r.candidateId) && (
                          <TableRow className="bg-muted/30">
                            <TableCell colSpan={canManage ? 8 : 7} className="py-3">
                              <div className="space-y-1.5 text-xs">
                                {r.breakdown.length === 0 ? (
                                  <span className="text-muted-foreground">No criteria were evaluated.</span>
                                ) : (
                                  r.breakdown.map((b, i) => (
                                    <div key={`${b.criteriaId}-${i}`} className="flex flex-wrap items-baseline gap-2">
                                      <span className="font-medium">{b.criteriaName}</span>
                                      {b.isMandatory && <Badge variant="outline" className="text-[10px]">mandatory</Badge>}
                                      {/* ⚠ Check autoEvaluated BEFORE passed. A criterion the
                                          engine left out reports passed: true and contributes
                                          nothing — reading the tick alone calls it a pass. */}
                                      <span
                                        className={
                                          b.autoEvaluated === false
                                            ? 'text-muted-foreground'
                                            : b.passed
                                              ? 'text-emerald-600 dark:text-emerald-400'
                                              : 'text-destructive'
                                        }
                                      >
                                        {b.autoEvaluated === false ? 'not scored' : b.passed ? 'met' : 'missed'}
                                      </span>
                                      <span className="text-muted-foreground">
                                        weight {b.weight} · raw {Number(b.rawScore).toFixed(2)}
                                      </span>
                                      {b.notes && <span className="text-muted-foreground">— {b.notes}</span>}
                                    </div>
                                  ))
                                )}
                              </div>
                            </TableCell>
                          </TableRow>
                        )}
                      </Fragment>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {bulkResult && bulkResult.results.some((r) => !r.success) && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">Skipped, and why</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-1 text-sm text-muted-foreground">
                  {bulkResult.results
                    .filter((r) => !r.success)
                    .map((r) => (
                      <li key={r.candidateId ?? r.applicationId}>{r.message ?? 'skipped'}</li>
                    ))}
                </ul>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
//  The ad-hoc criteria editor
// ═══════════════════════════════════════════════════════════════════════════════

/**
 * ⚠ The server validates these through the SAME resolver a saved vacancy criterion goes through,
 * so this editor does not need to duplicate the rules and deliberately does not try: a mandatory
 * Gender, a numeric criterion with no bound and a list criterion with no values all come back as a
 * refusal naming the problem. What it does do is read the shape table from the server, so the
 * controls offered match what the engine will accept.
 */
function AdHocCriteriaEditor({
  criteria,
  onChange,
  open,
  setOpen,
}: {
  criteria: AdHocScreeningCriterion[];
  onChange: (next: AdHocScreeningCriterion[]) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
}) {
  const [draft, setDraft] = useState<AdHocScreeningCriterion>(blankDraft());
  const [picked, setPicked] = useState<PickedValue[]>([]);
  const [areaCountryId, setAreaCountryId] = useState('');
  const [areaId, setAreaId] = useState('');

  const shapes = useQuery({
    queryKey: ['hr', 'vacancy-criteria', 'shapes'],
    queryFn: () => jobVacancyService.getCriteriaShapes(),
  });
  const shape = shapes.data?.find((s) => s.type === draft.type);
  const valueKind = shape?.valueKind ?? null;

  const skills = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
    enabled: open && valueKind === 'Skill',
  });
  const qualifications = useQuery({
    queryKey: ['hr', 'qualifications', 'active'],
    queryFn: () => qualificationService.getActive(),
    enabled: open && valueKind === 'Qualification',
  });
  const certifications = useQuery({
    queryKey: ['hr', 'certifications', 'active'],
    queryFn: () => certificationService.getAll({ activeOnly: true }),
    enabled: open && valueKind === 'Certification',
  });
  const languages = useQuery({
    queryKey: ['hr', 'languages', 'active'],
    queryFn: () => languageService.getActive(),
    enabled: open && valueKind === 'Language',
  });
  // Round 4, lane Q: the rungs an "Education level" criterion picks its minimum from — the same
  // cache entry as the vacancy criteria panel's.
  const levels = useQuery({
    queryKey: ['hr', 'qualification-levels', 'active'],
    queryFn: () => referenceDimensionService.getQualificationLevels(true),
    enabled: open && valueKind === 'QualificationLevel',
  });
  const ladder = useMemo(
    () => [...(levels.data ?? [])].sort((a, b) => a.rank - b.rank || a.name.localeCompare(b.name)),
    [levels.data],
  );

  const catalogue: { id: string; name: string }[] = useMemo(() => {
    switch (valueKind) {
      case 'Skill':
        return (skills.data ?? []).map((s) => ({ id: s.id, name: s.name }));
      case 'Qualification':
        return (qualifications.data ?? []).map((q) => ({ id: q.id, name: q.name }));
      case 'Certification':
        return (certifications.data ?? []).map((c) => ({ id: c.id, name: c.name }));
      case 'Language':
        return (languages.data ?? []).map((l) => ({ id: l.id, name: l.name }));
      default:
        return [];
    }
  }, [valueKind, skills.data, qualifications.data, certifications.data, languages.data]);

  // The chosen area's name for the badge — AddressFields emits an id only, and a criterion that
  // read "a1b2c3…" would be unreadable the moment it was added.
  const areaAncestors = useQuery({
    queryKey: ['reference', 'geo', 'ancestors', areaId],
    queryFn: () => geographyService.getAncestors(areaId),
    enabled: !!areaId,
  });

  const reset = () => {
    setDraft(blankDraft());
    setPicked([]);
    setAreaId('');
    setAreaCountryId('');
  };

  const addPicked = (value: PickedValue) => {
    const label = value.label.trim();
    if (!label && !value.referenceId) return;
    setPicked((rows) =>
      rows.some((r) =>
        value.referenceId ? r.referenceId === value.referenceId : r.label.toLowerCase() === label.toLowerCase(),
      )
        ? rows
        : [...rows, { referenceId: value.referenceId, label }],
    );
  };

  const commit = () => {
    const isNumeric = !!shape?.isNumeric;
    onChange([
      ...criteria,
      {
        ...draft,
        criteriaName: draft.criteriaName.trim() || (shape?.label ?? draft.type),
        values: isNumeric ? [] : picked.map((v) => ({ referenceId: v.referenceId, label: v.referenceId ? null : v.label })),
        minValue: isNumeric ? draft.minValue ?? null : null,
        maxValue: isNumeric ? draft.maxValue ?? null : null,
        isMandatory: shape?.allowsMandatory === false ? false : draft.isMandatory,
        weight: Number.isFinite(draft.weight) && draft.weight > 0 ? draft.weight : 1,
        comparisonOperator: (shape?.operators?.length ?? 0) > 0 ? draft.comparisonOperator ?? null : null,
      },
    ]);
    reset();
    setOpen(false);
  };

  return (
    <div className="space-y-3 rounded-md border p-3">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">Criteria ({criteria.length})</span>
        <Button size="sm" variant="outline" onClick={() => { reset(); setOpen(true); }}>
          <Plus className="mr-2 h-4 w-4" />
          Add a criterion
        </Button>
      </div>

      {criteria.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Nothing to screen by yet. Add at least one criterion — with none, every candidate scores the
          same and the answer is just the pool list.
        </p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {criteria.map((c, i) => (
            <Badge key={`${c.criteriaName}-${i}`} variant={c.isMandatory ? 'default' : 'secondary'} className="gap-1">
              {c.criteriaName} · weight {c.weight}
              {(c.values ?? []).length > 0 ? ` · ${(c.values ?? []).map((v) => v.label ?? '…').join(', ')}` : ''}
              {c.minValue != null || c.maxValue != null ? ` · ${c.minValue ?? ''}–${c.maxValue ?? ''}` : ''}
              <button
                type="button"
                aria-label={`Remove ${c.criteriaName}`}
                onClick={() => onChange(criteria.filter((_, j) => j !== i))}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
        </div>
      )}

      <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) reset(); }}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Add a criterion</DialogTitle>
            <DialogDescription>{shape?.hint ?? 'Choose what this criterion measures.'}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>Measures</Label>
                <Select
                  value={draft.type}
                  onValueChange={(v) => {
                    setDraft((d) => ({ ...d, type: v as ShortlistingCriteriaType, comparisonOperator: null }));
                    setPicked([]);
                    setAreaId('');
                  }}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(shapes.data ?? [])
                      .filter((s) => s.isAutoEvaluated)
                      .map((s) => (
                        <SelectItem key={s.type} value={s.type}>
                          {s.label}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="adhoc-name">Name</Label>
                <Input
                  id="adhoc-name"
                  placeholder={shape?.label ?? 'What to call it'}
                  value={draft.criteriaName}
                  onChange={(e) => setDraft((d) => ({ ...d, criteriaName: e.target.value }))}
                  maxLength={100}
                />
              </div>
            </div>

            {shape?.isNumeric ? (
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="space-y-1.5">
                  <Label htmlFor="adhoc-min">Minimum</Label>
                  <Input
                    id="adhoc-min"
                    type="number"
                    value={draft.minValue ?? ''}
                    onChange={(e) =>
                      setDraft((d) => ({ ...d, minValue: e.target.value === '' ? null : Number(e.target.value) }))
                    }
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="adhoc-max">Maximum</Label>
                  <Input
                    id="adhoc-max"
                    type="number"
                    value={draft.maxValue ?? ''}
                    onChange={(e) =>
                      setDraft((d) => ({ ...d, maxValue: e.target.value === '' ? null : Number(e.target.value) }))
                    }
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Compared as</Label>
                  <Select
                    value={draft.comparisonOperator ?? NONE}
                    onValueChange={(v) =>
                      setDraft((d) => ({
                        ...d,
                        comparisonOperator: v === NONE ? null : (v as ShortlistingComparisonOperator),
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Default" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NONE}>Default</SelectItem>
                      {(shape?.operators ?? []).map((o) => (
                        <SelectItem key={o} value={o}>
                          {humanizeEnum(o)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            ) : valueKind === 'GeoArea' ? (
              <div className="space-y-2">
                <Label>Accepted areas</Label>
                <AddressFields
                  countryId={areaCountryId}
                  onCountryChange={(v) => { setAreaCountryId(v); setAreaId(''); }}
                  geoAreaId={areaId}
                  onGeoAreaChange={setAreaId}
                />
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={!areaId}
                  onClick={() => {
                    const chain = areaAncestors.data ?? [];
                    const own = chain[chain.length - 1];
                    if (!own) return;
                    addPicked({ referenceId: own.id, label: own.name });
                    setAreaId('');
                  }}
                >
                  <Plus className="mr-2 h-4 w-4" />
                  Add this area
                </Button>
                <p className="text-xs text-muted-foreground">
                  An area matches candidates recorded in it and anywhere beneath it.
                </p>
              </div>
            ) : valueKind === 'QualificationLevel' ? (
              // Round 4, lane Q: ONE value, the minimum rung. A new pick replaces the old one.
              <div className="space-y-1.5">
                <Label>At least</Label>
                <Select
                  value={picked[0]?.referenceId ?? NONE}
                  onValueChange={(v) => {
                    const rung = ladder.find((l) => l.id === v);
                    setPicked(rung ? [{ referenceId: rung.id, label: rung.name }] : []);
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={levels.isLoading ? 'Loading the ladder…' : 'The minimum level'} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>Choose the minimum level</SelectItem>
                    {ladder.map((l) => (
                      <SelectItem key={l.id} value={l.id}>
                        {l.name} · rank {l.rank}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  A candidate passes with any qualification at this level or higher. Levels ranked alike
                  count as equivalent; below it, or with no level on file, is a miss.
                </p>
              </div>
            ) : valueKind === 'Gender' ? (
              <div className="space-y-1.5">
                <Label>Accepted</Label>
                <Select value={NONE} onValueChange={(v) => v !== NONE && addPicked({ referenceId: null, label: v })}>
                  <SelectTrigger>
                    <SelectValue placeholder="Add a gender" />
                  </SelectTrigger>
                  <SelectContent>
                    {GENDERS.map((g) => (
                      <SelectItem key={g} value={g}>
                        {humanizeEnum(g)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : (
              <div className="space-y-1.5">
                <Label>Accepted values</Label>
                <Select value={NONE} onValueChange={(v) => {
                  const row = catalogue.find((c) => c.id === v);
                  if (row) addPicked({ referenceId: row.id, label: row.name });
                }}>
                  <SelectTrigger>
                    <SelectValue placeholder={`Add a ${(shape?.label ?? 'value').toLowerCase()}`} />
                  </SelectTrigger>
                  <SelectContent>
                    {catalogue.length === 0 && (
                      <SelectItem value={NONE} disabled>
                        Nothing in the catalogue
                      </SelectItem>
                    )}
                    {catalogue.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            {picked.length > 0 && valueKind !== 'QualificationLevel' && (
              <div className="flex flex-wrap gap-2">
                {picked.map((v, i) => (
                  <Badge key={`${v.label}-${i}`} variant="secondary" className="gap-1">
                    {v.label}
                    <button
                      type="button"
                      aria-label={`Remove ${v.label}`}
                      onClick={() => setPicked((rows) => rows.filter((_, j) => j !== i))}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            )}

            <div className="flex flex-wrap items-end gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="adhoc-weight">Weight</Label>
                <Input
                  id="adhoc-weight"
                  type="number"
                  min={1}
                  max={100}
                  className="w-24"
                  value={draft.weight}
                  onChange={(e) => setDraft((d) => ({ ...d, weight: Number(e.target.value) || 1 }))}
                />
              </div>
              <label className="flex h-10 items-center gap-2 text-sm">
                <Checkbox
                  checked={draft.isMandatory}
                  disabled={shape?.allowsMandatory === false}
                  onCheckedChange={(v) => setDraft((d) => ({ ...d, isMandatory: v === true }))}
                />
                Mandatory
              </label>
              {shape?.allowsMandatory === false && shape.mandatoryRefusal && (
                <span className="text-xs text-muted-foreground">{shape.mandatoryRefusal}</span>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => { setOpen(false); reset(); }}>
              Cancel
            </Button>
            <Button onClick={commit}>Add</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
