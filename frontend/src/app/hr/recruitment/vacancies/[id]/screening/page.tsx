'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, EyeOff, Loader2, Mail, Sparkles } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { GatedPhoto } from '@/components/hr/common/PhotoDialog';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ShortlistApprovalCard } from '@/components/hr/recruitment/ShortlistApprovalCard';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { jobApplicationService, jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import type { BulkOperationResult, EeoStage } from '@/types/hr/recruitment-pipeline';

function EeoRow({ label, stage }: { label: string; stage: EeoStage }) {
  return (
    <TableRow>
      <TableCell className="font-medium">{label}</TableCell>
      <TableCell className="text-right tabular-nums">{stage.total}</TableCell>
      <TableCell className="text-right tabular-nums">
        {stage.male}
        {stage.malePercent != null && (
          <span className="ml-1 text-xs text-muted-foreground">({stage.malePercent}%)</span>
        )}
      </TableCell>
      <TableCell className="text-right tabular-nums">
        {stage.female}
        {stage.femalePercent != null && (
          <span className="ml-1 text-xs text-muted-foreground">({stage.femalePercent}%)</span>
        )}
      </TableCell>
      <TableCell className="text-right tabular-nums">{stage.other}</TableCell>
      <TableCell className="text-right tabular-nums">{stage.preferNotToSay}</TableCell>
      <TableCell className="text-right tabular-nums">{stage.internalCandidateCount}</TableCell>
    </TableRow>
  );
}

/**
 * Screening and shortlisting for one vacancy — everything that turns a pile of applications into a
 * shortlist somebody signs off.
 *
 * Five tabs rather than five screens, because they are one job: you score, you compare, you decide,
 * you send it up. Blind screening and the EEO report sit alongside deliberately — they are the two
 * checks on the decision the other tabs are making.
 */
export default function VacancyScreeningPage() {
  const params = useParams();
  const vacancyId = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [autoOpen, setAutoOpen] = useState(false);
  // Round 3, lane K (R-5 fix 8): the vacancy's own threshold (`AutoShortlistMinScore`, set on the
  // vacancy form and never read by anything until now) is the dialog's starting value.
  const [minScore, setMinScore] = useState('70');
  const [minScoreSeeded, setMinScoreSeeded] = useState(false);
  const [requireMandatory, setRequireMandatory] = useState(true);
  const [autoNotes, setAutoNotes] = useState('');
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  const vacancy = useQuery({
    queryKey: ['hr', 'vacancies', vacancyId],
    queryFn: () => jobVacancyService.getById(vacancyId),
    enabled: !!vacancyId,
  });
  useEffect(() => {
    if (minScoreSeeded || !vacancy.data) return;
    if (vacancy.data.autoShortlistMinScore != null) setMinScore(String(vacancy.data.autoShortlistMinScore));
    setMinScoreSeeded(true);
  }, [vacancy.data, minScoreSeeded]);

  const summary = useQuery({
    queryKey: ['hr', 'shortlist-summary', vacancyId],
    queryFn: () => jobApplicationService.getShortlistSummary(vacancyId),
    enabled: !!vacancyId,
  });

  const applications = useQuery({
    queryKey: ['hr', 'applications', 'by-vacancy', vacancyId],
    queryFn: () => jobApplicationService.getByVacancy(vacancyId),
    enabled: !!vacancyId,
  });

  const comparison = useQuery({
    queryKey: ['hr', 'shortlist-comparison', vacancyId],
    queryFn: () => jobApplicationService.getComparison(vacancyId),
    enabled: !!vacancyId,
  });

  const sla = useQuery({
    queryKey: ['hr', 'shortlist-sla', vacancyId],
    queryFn: () => jobApplicationService.getSla(vacancyId),
    enabled: !!vacancyId,
  });

  const eeo = useQuery({
    queryKey: ['hr', 'eeo-report', vacancyId],
    queryFn: () => jobApplicationService.getEeoReport(vacancyId),
    enabled: !!vacancyId,
  });

  // 422 unless the vacancy has blind screening switched on — a refusal here is a setting, not a fault.
  const blind = useQuery({
    queryKey: ['hr', 'blind-applications', vacancyId],
    queryFn: () => jobApplicationService.getBlindApplications(vacancyId),
    enabled: !!vacancyId,
    retry: false,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'shortlist-summary', vacancyId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'applications', 'by-vacancy', vacancyId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'shortlist-comparison', vacancyId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'shortlist-sla', vacancyId] });
    setSelected(new Set());
  };

  const scoreAll = useMutation({
    mutationFn: () => jobApplicationService.scoreAll(vacancyId),
    onSuccess: async (r) => {
      await refresh();
      toast({ title: `Scored ${r.length} application${r.length === 1 ? '' : 's'}` });
    },
    onError: (e: any) => toast({ title: 'Could not score', description: e?.message, variant: 'destructive' }),
  });

  const bulkShortlist = useMutation({
    mutationFn: () => jobApplicationService.bulkShortlist(vacancyId, [...selected], null),
    onSuccess: async (r) => {
      await refresh();
      setBulkResult(r);
    },
    onError: (e: any) => toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const autoShortlist = useMutation({
    mutationFn: () =>
      jobApplicationService.autoShortlist(vacancyId, {
        minScore: Number(minScore),
        requireAllMandatoryPassed: requireMandatory,
        shortlistingNotes: autoNotes.trim() || null,
      }),
    onSuccess: async (r) => {
      await refresh();
      setAutoOpen(false);
      setBulkResult(r);
    },
    onError: (e: any) => toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const notify = useMutation({
    mutationFn: (kind: 'shortlist' | 'rejection') =>
      kind === 'shortlist'
        ? jobApplicationService.sendShortlistNotifications(vacancyId)
        : jobApplicationService.sendRejectionNotifications(vacancyId),
    onSuccess: (r) =>
      toast({
        title: `${r.succeeded} notification${r.succeeded === 1 ? '' : 's'} sent`,
        description: r.skipped > 0 ? `${r.skipped} skipped.` : 'Anyone already notified was skipped.',
      }),
    onError: (e: any) => toast({ title: 'Could not send', description: e?.message, variant: 'destructive' }),
  });

  const exportCsv = async () => {
    try {
      const blob = await jobApplicationService.exportShortlistCsv(vacancyId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `shortlist-${vacancy.data?.vacancyNumber ?? vacancyId}.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (e: any) {
      toast({ title: 'Could not export', description: e?.message, variant: 'destructive' });
    }
  };

  const s = summary.data;
  const matrix = comparison.data;
  const rows = applications.data ?? [];
  const selectable = rows.filter((r) => !r.isShortlisted && r.status !== 'Rejected' && r.status !== 'Withdrawn');
  const allSelected = selectable.length > 0 && selectable.every((r) => selected.has(r.id));

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Screening — ${vacancy.data?.jobTitle || vacancy.data?.vacancyNumber || ''}`}
        description="Score, compare, shortlist and send the shortlist for approval."
        backHref={`/hr/recruitment/vacancies/${vacancyId}`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link href={`/hr/recruitment/vacancies/${vacancyId}/pipeline`}>Pipeline</Link>
            </Button>
            <Button variant="outline" onClick={exportCsv}>
              <Download className="mr-2 h-4 w-4" />
              Export CSV
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Applications', value: s?.totalApplications ?? '—' },
          {
            label: 'Scored',
            value: s ? `${s.scored} / ${s.totalApplications}` : '—',
            hint: s?.staleScores ? `${s.staleScores} stale` : undefined,
            tone: s?.staleScores ? 'warning' : 'default',
          },
          { label: 'Shortlisted', value: s?.shortlisted ?? '—', hint: `${s?.waitlisted ?? 0} waitlisted` },
          {
            label: 'Average score',
            value: s?.averageScore != null ? s.averageScore.toFixed(1) : '—',
            hint:
              s?.highestScore != null
                ? `High ${s.highestScore.toFixed(1)} · Low ${s.lowestScore?.toFixed(1) ?? '—'}`
                : undefined,
          },
        ]}
      />

      {s?.isShortlistDeadlinePassed && (
        <Card className="border-destructive/50">
          <CardContent className="pt-6 text-sm">
            <strong>The shortlisting deadline has passed.</strong> Shortlisting is refused for this
            vacancy until HR extends the deadline. Un-shortlisting and rejecting still work.
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="shortlist">
        <TabsList className="flex-wrap">
          <TabsTrigger value="shortlist">Shortlist</TabsTrigger>
          <TabsTrigger value="comparison">Comparison</TabsTrigger>
          <TabsTrigger value="blind">Blind screening</TabsTrigger>
          <TabsTrigger value="eeo">Diversity</TabsTrigger>
          <TabsTrigger value="sla">SLA</TabsTrigger>
        </TabsList>

        <TabsContent value="shortlist" className="mt-4 space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" onClick={() => scoreAll.mutate()} disabled={scoreAll.isPending}>
              {scoreAll.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Score all
            </Button>
            <Button variant="outline" onClick={() => setAutoOpen(true)}>
              <Sparkles className="mr-2 h-4 w-4" />
              Auto-shortlist by score
            </Button>
            {selected.size > 0 && (
              <Button onClick={() => bulkShortlist.mutate()} disabled={bulkShortlist.isPending}>
                {bulkShortlist.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Shortlist {selected.size} selected
              </Button>
            )}
          </div>

          {bulkResult && (
            <Card>
              <CardContent className="space-y-1 pt-6 text-sm">
                <p>
                  <strong>{bulkResult.succeeded}</strong> shortlisted,{' '}
                  <strong>{bulkResult.skipped}</strong> skipped.
                </p>
                {bulkResult.results
                  .filter((r) => !r.success)
                  .slice(0, 8)
                  .map((r) => (
                    <p key={r.applicationId} className="text-xs text-muted-foreground">
                      {r.message}
                    </p>
                  ))}
              </CardContent>
            </Card>
          )}

          <Card>
            <CardContent className="p-0">
              {applications.isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : rows.length === 0 ? (
                <EmptyState title="No applications" description="Nobody has applied for this vacancy yet." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-10">
                        <Checkbox
                          checked={allSelected}
                          onCheckedChange={() =>
                            setSelected((prev) => {
                              const next = new Set(prev);
                              if (allSelected) selectable.forEach((r) => next.delete(r.id));
                              else selectable.forEach((r) => next.add(r.id));
                              return next;
                            })
                          }
                          aria-label="Select all eligible"
                        />
                      </TableHead>
                      <TableHead>Candidate</TableHead>
                      <TableHead className="w-32">Applied</TableHead>
                      <TableHead className="w-36">Status</TableHead>
                      <TableHead className="w-24 text-right">Score</TableHead>
                      <TableHead className="w-28 text-right">Panel</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {[...rows]
                      .sort((a, b) => (b.autoScore ?? -1) - (a.autoScore ?? -1))
                      .map((r) => {
                        const eligible = selectable.some((x) => x.id === r.id);
                        return (
                          <TableRow key={r.id}>
                            <TableCell>
                              <Checkbox
                                checked={selected.has(r.id)}
                                disabled={!eligible}
                                onCheckedChange={() =>
                                  setSelected((prev) => {
                                    const next = new Set(prev);
                                    if (next.has(r.id)) next.delete(r.id);
                                    else next.add(r.id);
                                    return next;
                                  })
                                }
                                aria-label={`Select ${r.candidateName}`}
                              />
                            </TableCell>
                            <TableCell>
                              {/* Round 4, lane B5 — a face on the screening list too. */}
                              <div className="flex items-center gap-3">
                                <GatedPhoto
                                  endpoint={jobCandidateService.photoUrl(r.jobCandidateId)}
                                  enabled={r.candidateHasPhoto}
                                  alt={r.candidateName}
                                  className="h-8 w-8"
                                />
                                <div>
                                  <Link
                                    href={`/hr/recruitment/applications/${r.id}`}
                                    className="font-medium hover:underline"
                                  >
                                    {r.candidateName || r.applicationNumber}
                                  </Link>
                                  <div className="text-xs text-muted-foreground">{r.candidateEmail}</div>
                                </div>
                              </div>
                            </TableCell>
                            <TableCell className="text-sm text-muted-foreground">
                              {formatDate(r.applicationDate)}
                            </TableCell>
                            <TableCell>
                              <StatusBadge status={r.statusName} />
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {r.autoScore != null ? (
                                <span className={r.scoreIsStale ? 'text-muted-foreground' : undefined}>
                                  {r.autoScore.toFixed(1)}
                                  {r.scoreIsStale && '*'}
                                </span>
                              ) : (
                                '—'
                              )}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {r.aggregatedReviewScore != null ? r.aggregatedReviewScore.toFixed(1) : '—'}
                            </TableCell>
                          </TableRow>
                        );
                      })}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <p className="text-xs text-muted-foreground">
            * marks a stale score — the application or the vacancy&rsquo;s criteria changed since it
            was scored. Auto-shortlisting skips stale scores entirely, so re-score first.
          </p>
        </TabsContent>

        <TabsContent value="comparison" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {comparison.isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !matrix || matrix.criteria.length === 0 ? (
                <EmptyState
                  title="No shortlisting criteria"
                  description="The comparison matrix scores candidates against the vacancy's shortlisting criteria. Add some to the vacancy and re-score to use this."
                />
              ) : matrix.candidates.length === 0 ? (
                <EmptyState
                  title="Nobody to compare"
                  description="With nothing shortlisted, the matrix has no rows. Shortlist some candidates first."
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="min-w-[200px]">Candidate</TableHead>
                        <TableHead className="w-24 text-right">Total</TableHead>
                        {matrix.criteria.map((c) => (
                          <TableHead key={c.id} className="min-w-[140px] text-right">
                            {c.criteriaName}
                            <div className="text-xs font-normal text-muted-foreground">
                              weight {c.weight}
                              {c.isMandatory && ' · mandatory'}
                            </div>
                          </TableHead>
                        ))}
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {matrix.candidates.map((row) => (
                        <TableRow key={row.applicationId}>
                          <TableCell>
                            <Link
                              href={`/hr/recruitment/applications/${row.applicationId}`}
                              className="font-medium hover:underline"
                            >
                              {row.candidateName}
                            </Link>
                            <div className="font-mono text-xs text-muted-foreground">
                              {row.applicationNumber}
                            </div>
                          </TableCell>
                          <TableCell className="text-right font-medium tabular-nums">
                            {row.totalScore != null ? row.totalScore.toFixed(1) : '—'}
                          </TableCell>
                          {matrix.criteria.map((c) => {
                            const cell = row.criterionScores.find((x) => x.criteriaId === c.id);
                            return (
                              <TableCell key={c.id} className="text-right tabular-nums">
                                <span className={cell?.passed ? undefined : 'text-destructive'}>
                                  {cell ? cell.weightedScore.toFixed(2) : '—'}
                                </span>
                              </TableCell>
                            );
                          })}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="blind" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {blind.isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : blind.isError ? (
                <EmptyState
                  icon={EyeOff}
                  title="Blind screening is off for this vacancy"
                  description="Switch it on in the vacancy's settings to review applications with names, gender, age and contact details withheld."
                />
              ) : (blind.data ?? []).length === 0 ? (
                <EmptyState icon={EyeOff} title="Nothing to review" description="No applications yet." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Reference</TableHead>
                      <TableHead className="w-32">Applied</TableHead>
                      <TableHead className="w-36">Status</TableHead>
                      <TableHead className="w-24 text-right">Experience</TableHead>
                      <TableHead className="w-24 text-right">Score</TableHead>
                      <TableHead className="w-28">Internal</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(blind.data ?? []).map((b) => (
                      <TableRow key={b.applicationId}>
                        <TableCell className="font-mono text-xs">{b.applicationNumber}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(b.applicationDate)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={b.statusName} />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {b.yearsOfExperience ?? '—'}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {b.autoScore != null ? b.autoScore.toFixed(1) : '—'}
                        </TableCell>
                        <TableCell>
                          {b.isInternalCandidate ? <Badge variant="secondary">Internal</Badge> : '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <p className="mt-3 text-xs text-muted-foreground">
            Names, gender, age, location and contact details are withheld server-side — they are not
            hidden in the browser.
          </p>
        </TabsContent>

        <TabsContent value="eeo" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {eeo.isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !eeo.data ? (
                <EmptyState title="No report" description="Nothing to report on yet." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Stage</TableHead>
                      <TableHead className="w-24 text-right">Total</TableHead>
                      <TableHead className="w-32 text-right">Male</TableHead>
                      <TableHead className="w-32 text-right">Female</TableHead>
                      <TableHead className="w-24 text-right">Other</TableHead>
                      <TableHead className="w-32 text-right">Not stated</TableHead>
                      <TableHead className="w-28 text-right">Internal</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    <EeoRow label="All applicants" stage={eeo.data.allApplicants} />
                    <EeoRow label="Shortlisted" stage={eeo.data.shortlisted} />
                    <EeoRow label="Rejected" stage={eeo.data.rejected} />
                    <EeoRow label="Hired" stage={eeo.data.hired} />
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <p className="mt-3 text-xs text-muted-foreground">
            Compare the shortlisted row against all applicants — a large shift between the two is
            what this report exists to surface.
          </p>
        </TabsContent>

        <TabsContent value="sla" className="mt-4">
          <Card>
            <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <div className="text-xs text-muted-foreground">Published</div>
                <div className="text-sm">
                  {sla.data?.publishDate ? formatDate(sla.data.publishDate) : '—'}
                </div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Shortlisting deadline</div>
                <div className="text-sm">
                  {sla.data?.shortlistingDeadline ? formatDate(sla.data.shortlistingDeadline) : '—'}
                </div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Shortlist completed</div>
                <div className="text-sm">
                  {sla.data?.shortlistCompletedAt ? formatDate(sla.data.shortlistCompletedAt) : 'Not yet'}
                </div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Time to shortlist</div>
                <div className="text-sm">
                  {sla.data?.timeToShortlistDays != null ? `${sla.data.timeToShortlistDays} days` : '—'}
                </div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Days remaining</div>
                <div className="text-sm">
                  {sla.data?.daysOverdue != null ? (
                    <span className="text-destructive">{sla.data.daysOverdue} days overdue</span>
                  ) : (
                    (sla.data?.daysRemaining ?? '—')
                  )}
                </div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Approval</div>
                <div className="text-sm">
                  <StatusBadge status={sla.data?.approvalStatusName ?? 'Not submitted'} />
                </div>
              </div>
            </CardContent>
          </Card>
          <p className="mt-3 text-xs text-muted-foreground">
            Time to shortlist is measured from the publish date and stamped when the shortlist is
            approved — not when the last candidate is shortlisted.
          </p>
        </TabsContent>
      </Tabs>

      {/* Round 3 (demo feedback): the approval and the notifications come AFTER the shortlist
          itself — you read and shape the list, then sign it off and tell people. */}
      <div className="grid gap-4 md:grid-cols-2">
        <ShortlistApprovalCard vacancyId={vacancyId} summary={s} onChanged={refresh} />

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Candidate notifications</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Emails everyone shortlisted or rejected who has not already been told. Safe to run
              again — anyone already notified is skipped.
            </p>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={notify.isPending} onClick={() => notify.mutate('shortlist')}>
                <Mail className="mr-2 h-3.5 w-3.5" />
                Tell the shortlisted
              </Button>
              <Button size="sm" variant="outline" disabled={notify.isPending} onClick={() => notify.mutate('rejection')}>
                <Mail className="mr-2 h-3.5 w-3.5" />
                Tell the rejected
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>

      <Dialog open={autoOpen} onOpenChange={(o) => !o && setAutoOpen(false)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Auto-shortlist by score</DialogTitle>
            <DialogDescription>
              Shortlists everyone at or above the threshold. Applications with a stale score are
              skipped, so run &ldquo;Score all&rdquo; first if anything is marked with an asterisk.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="auto-min-score">Minimum score (1–100)</Label>
              {vacancy.data?.autoShortlistMinScore != null && (
                <p className="text-xs text-muted-foreground">
                  The vacancy&apos;s own threshold is {vacancy.data.autoShortlistMinScore}. Applications on a
                  vacancy with no criteria carry no score and are never shortlisted this way.
                </p>
              )}
              <Input
                id="auto-min-score"
                type="number"
                min={1}
                max={100}
                value={minScore}
                onChange={(e) => setMinScore(e.target.value)}
              />
            </div>
            <div className="flex items-center justify-between rounded-md border p-3">
              <div className="space-y-0.5">
                <Label>Require all mandatory criteria</Label>
                <p className="text-xs text-muted-foreground">
                  Excludes anyone who failed a mandatory criterion, whatever their total.
                </p>
              </div>
              <Switch checked={requireMandatory} onCheckedChange={setRequireMandatory} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="auto-notes">Shortlisting notes</Label>
              <Textarea
                id="auto-notes"
                value={autoNotes}
                onChange={(e) => setAutoNotes(e.target.value)}
                rows={3}
                placeholder="Recorded against every application this shortlists"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAutoOpen(false)}>
              Cancel
            </Button>
            <Button disabled={autoShortlist.isPending} onClick={() => autoShortlist.mutate()}>
              {autoShortlist.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Shortlist
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
