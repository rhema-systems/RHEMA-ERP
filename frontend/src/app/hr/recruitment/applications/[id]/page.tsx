'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Calculator, Loader2, Pencil, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { GatedPhoto } from '@/components/hr/common/PhotoDialog';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ApplicationDecisionBar } from '@/components/hr/recruitment/ApplicationDecisionBar';
import { ApplicationSourceDialog } from '@/components/hr/recruitment/ApplicationSourceDialog';
import { ApplicationReviewsPanel } from '@/components/hr/recruitment/ApplicationReviewsPanel';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobOfferService } from '@/services/hr/offers.service';
import { jobApplicationService, jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import {
  APPLICANT_TEST_TYPES,
  COMMUNICATION_DIRECTIONS,
  COMMUNICATION_TYPES,
  parseScoreBreakdown,
  TERMINAL_APPLICATION_STATUSES,
  type CommunicationDirection,
  type CommunicationType,
  type JobApplicantTestType,
} from '@/types/hr/recruitment-pipeline';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

export default function ApplicationDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [sourceOpen, setSourceOpen] = useState(false);
  const [addingTest, setAddingTest] = useState(false);
  const [testForm, setTestForm] = useState({
    testType: 'Written' as JobApplicantTestType,
    testName: '',
    testDate: '',
    venue: '',
    score: '',
    maxScore: '',
  });
  const [addingComm, setAddingComm] = useState(false);
  const [commForm, setCommForm] = useState({
    type: 'Email' as CommunicationType,
    direction: 'Outbound' as CommunicationDirection,
    subject: '',
    body: '',
  });

  const { data: a, isLoading, isError } = useQuery({
    queryKey: ['hr', 'application', id],
    queryFn: () => jobApplicationService.getById(id),
    enabled: !!id,
  });

  const history = useQuery({
    queryKey: ['hr', 'application-stage-history', id],
    queryFn: () => jobApplicationService.getStageHistory(id),
    enabled: !!id,
  });

  const decisions = useQuery({
    queryKey: ['hr', 'application-decision-log', id],
    queryFn: () => jobApplicationService.getDecisionLog(id),
    enabled: !!id,
  });

  const tests = useQuery({
    queryKey: ['hr', 'application-tests', id],
    queryFn: () => jobApplicationService.getTestResults(id),
    enabled: !!id,
  });

  const comms = useQuery({
    queryKey: ['hr', 'application-communications', id],
    queryFn: () => jobApplicationService.getCommunications(id),
    enabled: !!id,
  });

  // Raising an offer is not gated by application status server-side — only that the application
  // resolves to a vacancy and a position — so this is offered whenever nothing has been raised yet.
  const offer = useQuery({
    queryKey: ['hr', 'offer-for-application', id],
    queryFn: () => jobOfferService.getByApplication(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'application', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'application-stage-history', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'application-decision-log', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'applications'] });
  };

  const rescore = useMutation({
    mutationFn: () => jobApplicationService.score(id),
    onSuccess: async (result) => {
      await refresh();
      toast({
        title: `Scored ${result.autoScore.toFixed(1)}`,
        description: result.allMandatoryPassed
          ? 'All mandatory criteria passed.'
          : 'One or more mandatory criteria failed.',
      });
    },
    onError: (e: any) => toast({ title: 'Could not score', description: e?.message, variant: 'destructive' }),
  });

  const addTest = useMutation({
    mutationFn: () =>
      jobApplicationService.addTestResult(id, {
        testType: testForm.testType,
        testName: testForm.testName.trim(),
        testDate: testForm.testDate,
        venue: testForm.venue.trim() || null,
        score: testForm.score === '' ? null : Number(testForm.score),
        maxScore: testForm.maxScore === '' ? null : Number(testForm.maxScore),
        passed: null,
        remarks: null,
        invigilatedById: null,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'application-tests', id] });
      setAddingTest(false);
      setTestForm({ testType: 'Written', testName: '', testDate: '', venue: '', score: '', maxScore: '' });
      toast({ title: 'Test result recorded' });
    },
    onError: (e: any) => toast({ title: 'Could not record', description: e?.message, variant: 'destructive' }),
  });

  const addComm = useMutation({
    mutationFn: () =>
      jobApplicationService.addCommunication(id, {
        type: commForm.type,
        direction: commForm.direction,
        subject: commForm.subject.trim(),
        body: commForm.body.trim(),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'application-communications', id] });
      setAddingComm(false);
      setCommForm({ type: 'Email', direction: 'Outbound', subject: '', body: '' });
      toast({ title: 'Communication logged' });
    },
    onError: (e: any) => toast({ title: 'Could not log', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !a) {
    return <EmptyState title="Application not found" description="It may have been removed." />;
  }

  const breakdown = parseScoreBreakdown(a.autoScoreBreakdown);

  return (
    <div className="space-y-6">
      <PageHeader
        title={a.candidateName || a.applicationNumber}
        description={`${a.applicationNumber} · ${a.jobTitle || a.vacancyNumber}`}
        backHref="/hr/recruitment/applications"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <ApplicationSourceDialog
              open={sourceOpen}
              onOpenChange={setSourceOpen}
              application={{ id: a.id, jobVacancyId: a.jobVacancyId, source: a.source, jobPostingId: a.jobPostingId ?? null }}
            />
            <Button variant="outline" onClick={() => rescore.mutate()} disabled={rescore.isPending}>
              {rescore.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Calculator className="mr-2 h-4 w-4" />
              )}
              Re-score
            </Button>
            {offer.data ? (
              <Button asChild variant="outline">
                <Link href={`/hr/recruitment/offers/${offer.data.id}`}>
                  {offer.data.offerNumber} · {offer.data.offerStatusName}
                </Link>
              </Button>
            ) : (
              !TERMINAL_APPLICATION_STATUSES.includes(a.status) && (
                <Button asChild>
                  <Link href={`/hr/recruitment/offers/new?applicationId=${id}`}>
                    <Plus className="mr-2 h-4 w-4" />
                    Extend an offer
                  </Link>
                </Button>
              )
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Status', value: a.statusName },
          {
            label: 'Auto score',
            value: a.autoScore != null ? a.autoScore.toFixed(1) : '—',
            hint: a.scoreIsStale ? 'Stale — re-score before relying on it' : undefined,
            tone: a.scoreIsStale ? 'warning' : 'default',
          },
          {
            // G-9.5 (2026-09-15): "Panel score" was ambiguous in the one way that matters here.
            // This is AggregatedReviewScore — the average of finalised ShortlistReview rows, i.e.
            // the SHORTLISTING panel — and a recruiter reading an application after interviews have
            // run would reasonably take it for the interview panel's verdict. Two different panels,
            // two different decisions, one label. The hint below was accurate but never said which.
            label: 'Shortlisting panel score',
            value: a.aggregatedReviewScore != null ? a.aggregatedReviewScore.toFixed(2) : '—',
            hint: 'Finalized shortlisting reviews only — not the interview panel',
          },
          { label: 'Current stage', value: a.currentStageName ?? 'Not in a stage' },
        ]}
      />

      <ApplicationDecisionBar application={a} onChanged={refresh} />

      <Tabs defaultValue="overview">
        <TabsList className="flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="score">Score breakdown</TabsTrigger>
          <TabsTrigger value="stages">Stage history</TabsTrigger>
          <TabsTrigger value="reviews">Panel reviews</TabsTrigger>
          <TabsTrigger value="tests">Tests</TabsTrigger>
          <TabsTrigger value="communications">Communications</TabsTrigger>
          <TabsTrigger value="decisions">Decision log</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="mt-4 grid gap-4 md:grid-cols-2">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Application</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6">
              <InfoRow label="Applied" value={formatDate(a.applicationDate)} />
              <InfoRow
                label="Source"
                value={
                  <span className="inline-flex items-center gap-2">
                    {humanizeEnum(a.source)}
                    <button type="button" className="text-xs text-primary hover:underline" onClick={() => setSourceOpen(true)}>
                      <Pencil className="mr-1 inline h-3 w-3" />
                      Correct
                    </button>
                  </span>
                }
              />
              <InfoRow
                label="Advert"
                value={a.jobPostingId ? `${a.jobPostingChannel ? humanizeEnum(a.jobPostingChannel) : ''}${a.jobPostingTitle ? ` · ${a.jobPostingTitle}` : ''}`.trim() || 'Recorded' : 'None — not through an advert'}
              />
              <InfoRow label="Experience" value={a.yearsOfExperience != null ? `${a.yearsOfExperience} yrs` : null} />
              <InfoRow label="Available from" value={a.availableFrom ? formatDate(a.availableFrom) : null} />
              <InfoRow
                label="Internal candidate"
                value={a.isInternalCandidate ? <Badge variant="secondary">Internal</Badge> : 'No'}
              />
            </CardContent>
          </Card>

          <Card>
            {/* Round 4, lane B5. The photograph was built in round 3 (lane C2) and rendered on the
                CANDIDATE screens only, so a recruiter working an APPLICATION — where the decisions
                are actually made — never saw a face. Same gated route, same flag guarding against a
                request that would 404. */}
            <CardHeader className="flex-row items-center gap-3 space-y-0 pb-2">
              <GatedPhoto
                endpoint={jobCandidateService.photoUrl(a.jobCandidateId)}
                enabled={a.candidateHasPhoto}
                alt={a.candidateName}
                className="h-12 w-12"
              />
              <CardTitle className="text-base">Candidate</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6">
              <InfoRow
                label="Name"
                value={
                  <Link
                    href={`/hr/recruitment/candidates/${a.jobCandidateId}`}
                    className="text-primary hover:underline"
                  >
                    {a.candidateName || a.candidateNumber}
                  </Link>
                }
              />
              <InfoRow label="Number" value={a.candidateNumber} />
              <InfoRow label="Email" value={a.candidateEmail} />
              <InfoRow label="Phone" value={a.candidatePhone} />
            </CardContent>
          </Card>

          <Card className="md:col-span-2">
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Decision</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow
                label="Shortlisted"
                value={a.shortlistedDate ? `${formatDate(a.shortlistedDate)} by ${a.shortlistedByName ?? '—'}` : null}
              />
              <InfoRow
                label="Decision source"
                value={a.decisionSource ? humanizeEnum(a.decisionSource) : null}
              />
              <InfoRow label="Shortlisting notes" value={a.shortlistingNotes} />
              <InfoRow
                label="Waitlisted"
                value={a.waitlistedDate ? formatDate(a.waitlistedDate) : null}
              />
              <InfoRow label="Waitlist reason" value={a.waitlistReason} />
              <InfoRow
                label="Rejected"
                value={a.rejectedDate ? `${formatDate(a.rejectedDate)} by ${a.rejectedByName ?? '—'}` : null}
              />
              <InfoRow label="Rejection reason" value={a.rejectionReason} />
              <InfoRow label="Withdrawn" value={a.withdrawnDate ? formatDate(a.withdrawnDate) : null} />
              <InfoRow label="Withdrawal reason" value={a.withdrawalReason} />
            </CardContent>
          </Card>

          {a.coverLetter && (
            <Card className="md:col-span-2">
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Cover letter</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap text-sm">{a.coverLetter}</p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="score" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {breakdown.length === 0 ? (
                <EmptyState
                  title="Not scored yet"
                  description="Scoring compares the candidate against the vacancy's shortlisting criteria. Add criteria to the vacancy first if there are none."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Criterion</TableHead>
                      <TableHead className="w-28">Mandatory</TableHead>
                      <TableHead className="w-24 text-right">Weight</TableHead>
                      <TableHead className="w-24 text-right">Raw</TableHead>
                      <TableHead className="w-28 text-right">Weighted</TableHead>
                      <TableHead className="w-24">Result</TableHead>
                      <TableHead>Notes</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {breakdown.map((c) => (
                      <TableRow key={c.criteriaId}>
                        <TableCell className="font-medium">{c.criteriaName}</TableCell>
                        <TableCell>{c.isMandatory ? 'Yes' : '—'}</TableCell>
                        <TableCell className="text-right tabular-nums">{c.weight}</TableCell>
                        <TableCell className="text-right tabular-nums">{c.rawScore?.toFixed(1)}</TableCell>
                        <TableCell className="text-right tabular-nums">{c.weightedScore?.toFixed(2)}</TableCell>
                        <TableCell>
                          <Badge variant={c.passed ? 'default' : 'destructive'}>
                            {c.passed ? 'Passed' : 'Failed'}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">{c.notes ?? '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="stages" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {history.isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : (history.data ?? []).length === 0 ? (
                <EmptyState
                  title="Not in a pipeline yet"
                  description="An application enters the pipeline when it is placed in its first stage."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Stage</TableHead>
                      <TableHead className="w-40">Type</TableHead>
                      <TableHead className="w-44">Entered</TableHead>
                      <TableHead className="w-44">Left</TableHead>
                      <TableHead className="w-32">Reason</TableHead>
                      <TableHead>Moved by</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(history.data ?? []).map((h) => (
                      <TableRow key={h.id}>
                        <TableCell className="font-medium">
                          {h.stageName}
                          {h.isCurrent && (
                            <Badge variant="secondary" className="ml-2">
                              Current
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>{humanizeEnum(h.stageType)}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDateTime(h.enteredAt)}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {h.exitedAt ? formatDateTime(h.exitedAt) : '—'}
                        </TableCell>
                        <TableCell>
                          {h.exitReason ? <StatusBadge status={humanizeEnum(h.exitReason)} /> : '—'}
                        </TableCell>
                        <TableCell className="text-sm">{h.movedByName || '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="reviews" className="mt-4">
          <ApplicationReviewsPanel applicationId={id} />
        </TabsContent>

        <TabsContent value="tests" className="mt-4 space-y-4">
          <Card>
            <CardContent className="p-0">
              {(tests.data ?? []).length === 0 ? (
                <EmptyState title="No test results" description="Record written and practical test outcomes here." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Test</TableHead>
                      <TableHead className="w-28">Type</TableHead>
                      <TableHead className="w-32">Date</TableHead>
                      <TableHead className="w-32 text-right">Score</TableHead>
                      <TableHead className="w-24 text-right">%</TableHead>
                      <TableHead className="w-24">Result</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(tests.data ?? []).map((t) => (
                      <TableRow key={t.id}>
                        <TableCell className="font-medium">{t.testName}</TableCell>
                        <TableCell>{t.testTypeName}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">{formatDate(t.testDate)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {t.score != null ? `${t.score}${t.maxScore != null ? ` / ${t.maxScore}` : ''}` : '—'}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {t.scorePercentage != null ? `${t.scorePercentage.toFixed(1)}%` : '—'}
                        </TableCell>
                        <TableCell>
                          {t.passed == null ? (
                            '—'
                          ) : (
                            <Badge variant={t.passed ? 'default' : 'destructive'}>
                              {t.passed ? 'Passed' : 'Failed'}
                            </Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <Button variant="outline" onClick={() => setAddingTest(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Record test result
          </Button>
        </TabsContent>

        <TabsContent value="communications" className="mt-4 space-y-4">
          <Card>
            <CardContent className="p-0">
              {(comms.data ?? []).length === 0 ? (
                <EmptyState
                  title="No communications"
                  description="Shortlist and rejection decisions write their own entries here."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-40">When</TableHead>
                      <TableHead className="w-28">Type</TableHead>
                      <TableHead className="w-28">Direction</TableHead>
                      <TableHead>Subject</TableHead>
                      <TableHead>Sent by</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(comms.data ?? []).map((c) => (
                      <TableRow key={c.id}>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDateTime(c.sentAt)}
                        </TableCell>
                        <TableCell>{c.typeName}</TableCell>
                        <TableCell>{c.directionName}</TableCell>
                        <TableCell>
                          <div className="font-medium">{c.subject}</div>
                          <div className="text-xs text-muted-foreground">{c.body}</div>
                        </TableCell>
                        <TableCell className="text-sm">{c.sentByName ?? 'System'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <Button variant="outline" onClick={() => setAddingComm(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Log a communication
          </Button>
          <p className="text-xs text-muted-foreground">
            This records a communication — it does not send one. Candidate emails go out through the
            vacancy&rsquo;s shortlist and rejection notification actions.
          </p>
        </TabsContent>

        <TabsContent value="decisions" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {(decisions.data ?? []).length === 0 ? (
                <EmptyState
                  title="No decisions recorded"
                  description="Shortlisting, waitlisting and rejection each write an immutable entry here."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-44">When</TableHead>
                      <TableHead className="w-40">Decision</TableHead>
                      <TableHead className="w-24 text-right">Score</TableHead>
                      <TableHead className="w-28">Automatic</TableHead>
                      <TableHead>By</TableHead>
                      <TableHead>Notes</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(decisions.data ?? []).map((d) => (
                      <TableRow key={d.id}>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDateTime(d.decisionAt)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={humanizeEnum(d.decisionType)} />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {d.autoScoreAtDecision != null ? d.autoScoreAtDecision.toFixed(1) : '—'}
                        </TableCell>
                        <TableCell>{d.isAutoDecision ? 'Yes' : '—'}</TableCell>
                        <TableCell className="text-sm">{d.decisionByName ?? '—'}</TableCell>
                        <TableCell className="text-xs text-muted-foreground">{d.notes ?? '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog open={addingTest} onOpenChange={(o) => !o && setAddingTest(false)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Record test result</DialogTitle>
            <DialogDescription>
              The percentage is computed from the score and maximum. Name, type, date and venue cannot
              be amended afterwards — only the marks.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label>Type</Label>
                <Select
                  value={testForm.testType}
                  onValueChange={(v) => setTestForm((f) => ({ ...f, testType: v as JobApplicantTestType }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {APPLICANT_TEST_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {t}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Date</Label>
                <Input
                  type="date"
                  value={testForm.testDate}
                  onChange={(e) => setTestForm((f) => ({ ...f, testDate: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Test name</Label>
              <Input
                value={testForm.testName}
                onChange={(e) => setTestForm((f) => ({ ...f, testName: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Venue</Label>
              <Input
                value={testForm.venue}
                onChange={(e) => setTestForm((f) => ({ ...f, venue: e.target.value }))}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label>Score</Label>
                <Input
                  type="number"
                  value={testForm.score}
                  onChange={(e) => setTestForm((f) => ({ ...f, score: e.target.value }))}
                />
              </div>
              <div className="space-y-1.5">
                <Label>Out of</Label>
                <Input
                  type="number"
                  value={testForm.maxScore}
                  onChange={(e) => setTestForm((f) => ({ ...f, maxScore: e.target.value }))}
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddingTest(false)}>
              Cancel
            </Button>
            <Button
              disabled={!testForm.testName.trim() || !testForm.testDate || addTest.isPending}
              onClick={() => addTest.mutate()}
            >
              {addTest.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={addingComm} onOpenChange={(o) => !o && setAddingComm(false)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Log a communication</DialogTitle>
            <DialogDescription>
              Records something that already happened — a call, a letter, an email sent by hand.
              Nothing is sent from here.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label>Type</Label>
                <Select
                  value={commForm.type}
                  onValueChange={(v) => setCommForm((f) => ({ ...f, type: v as CommunicationType }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {COMMUNICATION_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {humanizeEnum(t)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Direction</Label>
                <Select
                  value={commForm.direction}
                  onValueChange={(v) =>
                    setCommForm((f) => ({ ...f, direction: v as CommunicationDirection }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {COMMUNICATION_DIRECTIONS.map((d) => (
                      <SelectItem key={d} value={d}>
                        {d}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Subject</Label>
              <Input
                value={commForm.subject}
                onChange={(e) => setCommForm((f) => ({ ...f, subject: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Body</Label>
              <Textarea
                value={commForm.body}
                onChange={(e) => setCommForm((f) => ({ ...f, body: e.target.value }))}
                rows={5}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddingComm(false)}>
              Cancel
            </Button>
            <Button
              disabled={!commForm.subject.trim() || !commForm.body.trim() || addComm.isPending}
              onClick={() => addComm.mutate()}
            >
              {addComm.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Log
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
