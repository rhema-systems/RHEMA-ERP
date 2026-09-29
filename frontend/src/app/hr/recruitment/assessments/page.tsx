'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarClock,
  ClipboardCheck,
  ClipboardList,
  ClipboardPen,
  Loader2,
  Mail,
  Plus,
  Printer,
  RotateCcw,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import {
  RECRUITMENT_SITTING_MODE_LABELS,
  RECRUITMENT_SITTING_STATUS_LABELS,
  type RecruitmentSittingStatus,
} from '@/types/hr/recruitment-tests';

const STATUS_VARIANT: Record<RecruitmentSittingStatus, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  NotStarted: 'outline',
  InProgress: 'secondary',
  AwaitingMarking: 'default',
  Marked: 'default',
  Expired: 'destructive',
  Cancelled: 'outline',
};

const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—';

/**
 * Where a test meets a vacancy: who has been asked to sit what, and what has come back to mark.
 *
 * ⚠ The marking queue is the half that matters operationally. A sitting with written answers stays
 * in it until somebody reads them and finalises — and finalising is the step that writes the result
 * into the applicant's test ledger and re-scores the application, so a paper marked but not
 * finalised has not influenced anybody's shortlist position.
 */
export default function RecruitmentAssessmentsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [assignOpen, setAssignOpen] = useState(false);
  const [testId, setTestId] = useState('');
  const [vacancyId, setVacancyId] = useState('');
  const [opensAt, setOpensAt] = useState('');
  const [closesAt, setClosesAt] = useState('');
  const [isRequired, setIsRequired] = useState(true);

  const [extraFor, setExtraFor] = useState<string | null>(null);
  const [extraReason, setExtraReason] = useState('');

  const [statusFilter, setStatusFilter] = useState<'all' | RecruitmentSittingStatus>('AwaitingMarking');

  const testList = useQuery({
    queryKey: ['hr', 'recruitment-tests', 'active'],
    queryFn: () => tests.getTests(true),
  });

  const vacancies = useQuery({
    queryKey: ['hr', 'job-vacancies', 'active'],
    queryFn: () => jobVacancyService.getActive(),
  });

  const assignments = useQuery({
    queryKey: ['hr', 'recruitment-test-assignments'],
    queryFn: () => tests.getAssignments(),
  });

  const sittings = useQuery({
    queryKey: ['hr', 'recruitment-test-sittings', statusFilter],
    queryFn: () =>
      tests.getSittings(statusFilter === 'all' ? {} : { status: statusFilter }),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-assignments'] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-test-sittings'] });
  };

  const assign = useMutation({
    mutationFn: () =>
      tests.assign({
        recruitmentTestId: testId,
        jobVacancyId: vacancyId,
        jobApplicationId: null,
        opensAt: opensAt ? new Date(opensAt).toISOString() : null,
        closesAt: closesAt ? new Date(closesAt).toISOString() : null,
        isRequired,
      }),
    onSuccess: () => {
      setAssignOpen(false);
      setTestId('');
      setVacancyId('');
      setOpensAt('');
      setClosesAt('');
      invalidate();
      toast({
        title: 'Test assigned',
        description: 'Nobody has been told yet — use Invite to send it out.',
      });
    },
    onError: (error: any) =>
      toast({ title: 'Could not assign the test', description: error?.message, variant: 'destructive' }),
  });

  const invite = useMutation({
    mutationFn: (id: string) => tests.invite(id),
    onSuccess: (sent) => {
      invalidate();
      toast({
        title: sent === 0 ? 'Nothing was sent' : `${sent} invitation(s) sent`,
        description:
          sent === 0
            ? 'Every candidate this assignment reaches is missing an email address.'
            : 'A vacancy-wide test reaches the applications that are still live.',
      });
    },
    onError: (error: any) =>
      toast({ title: 'Could not send the invitations', description: error?.message, variant: 'destructive' }),
  });

  const withdraw = useMutation({
    mutationFn: (id: string) => tests.deleteAssignment(id),
    onSuccess: () => {
      invalidate();
      toast({ title: 'Assignment withdrawn' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not withdraw the assignment',
        description: error?.message ?? 'Candidates have already sat it — close the window instead.',
        variant: 'destructive',
      }),
  });

  const grantExtra = useMutation({
    mutationFn: () =>
      tests.grantExtraAttempt({
        assignmentId: extraFor!,
        extraAttempts: 1,
        reason: extraReason.trim(),
      }),
    onSuccess: () => {
      setExtraFor(null);
      setExtraReason('');
      invalidate();
      toast({ title: 'Extra attempt granted' });
    },
    onError: (error: any) =>
      toast({ title: 'Could not grant the attempt', description: error?.message, variant: 'destructive' }),
  });

  const assignmentRows = assignments.data ?? [];
  const sittingRows = sittings.data ?? [];

  const awaitingCount = useMemo(
    () => sittingRows.filter((s) => s.status === 'AwaitingMarking').length,
    [sittingRows],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Assessments"
        description="Tests put in front of candidates, and the scripts waiting to be marked."
        backHref="/hr/recruitment"
        actions={
          <Button onClick={() => setAssignOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Assign a test
          </Button>
        }
      />

      <Tabs defaultValue="marking">
        <TabsList>
          <TabsTrigger value="marking">
            Marking queue
            {awaitingCount > 0 && (
              <Badge variant="secondary" className="ml-2">
                {awaitingCount}
              </Badge>
            )}
          </TabsTrigger>
          <TabsTrigger value="assignments">Assignments</TabsTrigger>
        </TabsList>

        <TabsContent value="marking" className="mt-4 space-y-4">
          <div className="flex items-center gap-2">
            <Label className="text-sm">Show</Label>
            <Select
              value={statusFilter}
              onValueChange={(value) => setStatusFilter(value as 'all' | RecruitmentSittingStatus)}
            >
              <SelectTrigger className="w-[220px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="AwaitingMarking">Awaiting marking</SelectItem>
                <SelectItem value="Marked">Marked</SelectItem>
                <SelectItem value="Expired">Time expired</SelectItem>
                <SelectItem value="InProgress">In progress</SelectItem>
                <SelectItem value="all">Everything</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <Card>
            <CardContent className="p-0">
              {sittings.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : sittingRows.length === 0 ? (
                <EmptyState
                  icon={ClipboardCheck}
                  title="Nothing here"
                  description="Scripts appear as candidates submit them. A paper of closed questions only is marked and finalised on submission; one with written answers waits here."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Candidate</TableHead>
                      <TableHead>Application</TableHead>
                      <TableHead>Test</TableHead>
                      <TableHead>Submitted</TableHead>
                      <TableHead className="text-right">Score</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[100px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sittingRows.map((sitting) => (
                      <TableRow key={sitting.id}>
                        <TableCell className="font-medium">{sitting.candidateName}</TableCell>
                        <TableCell className="font-mono text-xs">{sitting.applicationNumber}</TableCell>
                        <TableCell>
                          {sitting.testName}
                          {sitting.attemptNumber > 1 && (
                            <span className="ml-1 text-xs text-muted-foreground">
                              (attempt {sitting.attemptNumber})
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="text-sm">{formatDateTime(sitting.submittedAt)}</TableCell>
                        <TableCell className="text-right">
                          {/* ⚠ Blank, not 0%, while anything is still awaiting a human. A score
                              that is missing the essay is not the score. */}
                          {sitting.status === 'Marked' && sitting.scorePercent != null
                            ? `${sitting.scorePercent}%`
                            : '—'}
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap items-center gap-1">
                            <Badge variant={STATUS_VARIANT[sitting.status]}>
                              {RECRUITMENT_SITTING_STATUS_LABELS[sitting.status]}
                            </Badge>
                            {/* ⚠ How it was sat is evidence in its own right — say so on the row. */}
                            {sitting.mode === 'Paper' && (
                              <Badge variant="outline">{RECRUITMENT_SITTING_MODE_LABELS.Paper}</Badge>
                            )}
                            {sitting.status === 'Marked' && !sitting.jobApplicantTestResultId && (
                              <Badge variant="outline">Not in the ledger</Badge>
                            )}
                            {sitting.passed === true && <Badge variant="default">Passed</Badge>}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="outline" size="sm" asChild>
                            <Link href={`/hr/recruitment/assessments/${sitting.id}`}>Open</Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="assignments" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {assignments.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : assignmentRows.length === 0 ? (
                <EmptyState
                  icon={ClipboardList}
                  title="No tests assigned"
                  description="Assign an active test to a vacancy, then invite the candidates."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Test</TableHead>
                      <TableHead>Given to</TableHead>
                      <TableHead>Window</TableHead>
                      <TableHead className="text-right">Sat</TableHead>
                      <TableHead>Invited</TableHead>
                      <TableHead className="w-[230px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {assignmentRows.map((assignment) => (
                      <TableRow key={assignment.id}>
                        <TableCell>
                          <p className="font-medium">{assignment.testName}</p>
                          <p className="font-mono text-xs text-muted-foreground">
                            {assignment.testCode}
                          </p>
                        </TableCell>
                        <TableCell className="text-sm">
                          {assignment.jobVacancyId ? (
                            <>
                              <p>{assignment.jobTitle}</p>
                              <p className="font-mono text-xs text-muted-foreground">
                                {assignment.vacancyNumber}
                              </p>
                            </>
                          ) : (
                            <>
                              <p>{assignment.candidateName}</p>
                              <p className="font-mono text-xs text-muted-foreground">
                                {assignment.applicationNumber}
                              </p>
                            </>
                          )}
                        </TableCell>
                        <TableCell className="text-sm">
                          <span className="inline-flex items-center gap-1">
                            <CalendarClock className="h-3 w-3" />
                            {assignment.opensAt || assignment.closesAt
                              ? `${formatDateTime(assignment.opensAt)} → ${formatDateTime(assignment.closesAt)}`
                              : 'Open, no closing date'}
                          </span>
                          {assignment.extraAttemptsGranted > 0 && (
                            <p className="text-xs text-muted-foreground">
                              +{assignment.extraAttemptsGranted} extra attempt(s) granted
                            </p>
                          )}
                        </TableCell>
                        <TableCell className="text-right">{assignment.sittingCount}</TableCell>
                        <TableCell className="text-sm">
                          {assignment.invitedAt ? formatDateTime(assignment.invitedAt) : 'Not yet'}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center justify-end gap-1">
                            <Button variant="ghost" size="icon" title="Print named papers for a sitting session" asChild>
                              <Link
                                href={`/hr/recruitment/assessments/paper?testId=${assignment.recruitmentTestId}&variant=QuestionPaper&assignmentId=${assignment.id}`}
                              >
                                <Printer className="h-4 w-4" />
                              </Link>
                            </Button>
                            <Button variant="ghost" size="icon" title="Record a script sat on paper" asChild>
                              <Link
                                href={`/hr/recruitment/assessments/record?assignmentId=${assignment.id}&testId=${assignment.recruitmentTestId}`}
                              >
                                <ClipboardPen className="h-4 w-4" />
                              </Link>
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              title="Send the invitations"
                              disabled={invite.isPending}
                              onClick={() => invite.mutate(assignment.id)}
                            >
                              <Mail className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              title="Grant an extra attempt"
                              onClick={() => {
                                setExtraFor(assignment.id);
                                setExtraReason('');
                              }}
                            >
                              <RotateCcw className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              title="Withdraw the assignment"
                              disabled={withdraw.isPending}
                              onClick={() => {
                                if (window.confirm('Withdraw this assignment?')) {
                                  withdraw.mutate(assignment.id);
                                }
                              }}
                            >
                              <Trash2 className="h-4 w-4 text-destructive" />
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
        </TabsContent>
      </Tabs>

      {/* ── assign ───────────────────────────────────────────────────────────── */}
      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assign a test to a vacancy</DialogTitle>
            <DialogDescription>
              Every candidate whose application is still live will be able to sit it. Assigning does
              not tell anybody — invite them afterwards.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Test</Label>
              <Select value={testId} onValueChange={setTestId}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose an active test" />
                </SelectTrigger>
                <SelectContent>
                  {(testList.data ?? []).map((test) => (
                    <SelectItem key={test.id} value={test.id}>
                      {test.name} ({test.questionCount} questions)
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {(testList.data ?? []).length === 0 && (
                <p className="text-xs text-muted-foreground">
                  No active tests. A paper has to be activated before it can be assigned.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label>Vacancy</Label>
              <Select value={vacancyId} onValueChange={setVacancyId}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a vacancy" />
                </SelectTrigger>
                <SelectContent>
                  {(vacancies.data ?? []).map((vacancy) => (
                    <SelectItem key={vacancy.id} value={vacancy.id}>
                      {vacancy.jobTitle} — {vacancy.vacancyNumber}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="opensAt">Opens</Label>
                <Input
                  id="opensAt"
                  type="datetime-local"
                  value={opensAt}
                  onChange={(event) => setOpensAt(event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="closesAt">Closes</Label>
                <Input
                  id="closesAt"
                  type="datetime-local"
                  value={closesAt}
                  onChange={(event) => setClosesAt(event.target.value)}
                />
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              A candidate who starts shortly before the closing time gets the time that is left, not
              the paper&apos;s full duration.
            </p>

            <div className="flex items-center justify-between gap-4 rounded-lg border p-3">
              <div>
                <Label htmlFor="isRequired">Required</Label>
                <p className="text-xs text-muted-foreground">
                  An optional test still counts towards the score if it is taken.
                </p>
              </div>
              <Switch id="isRequired" checked={isRequired} onCheckedChange={setIsRequired} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!testId || !vacancyId || assign.isPending} onClick={() => assign.mutate()}>
              {assign.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── extra attempt ────────────────────────────────────────────────────── */}
      <Dialog open={!!extraFor} onOpenChange={(open) => !open && setExtraFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Grant an extra attempt</DialogTitle>
            <DialogDescription>
              One more attempt for everybody on this assignment. The reason is kept on the record.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="extraReason">Reason</Label>
            <Textarea
              id="extraReason"
              rows={3}
              value={extraReason}
              onChange={(event) => setExtraReason(event.target.value)}
              placeholder="The paper was interrupted by a power cut at the test centre."
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setExtraFor(null)}>
              Cancel
            </Button>
            <Button
              disabled={extraReason.trim().length === 0 || grantExtra.isPending}
              onClick={() => grantExtra.mutate()}
            >
              {grantExtra.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Grant
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
