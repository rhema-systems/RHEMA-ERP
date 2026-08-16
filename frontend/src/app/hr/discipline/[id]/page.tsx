'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Loader2, Send, Play, Gavel, Lock, PauseCircle, RotateCcw, XCircle, AlertTriangle, UserX, CalendarClock, Scale,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { disciplineService, disciplineLookupService, disciplineAppealService } from '@/services/hr/discipline.service';
import { useToast } from '@/hooks/use-toast';
import {
  TERMINAL_CASE_STATUSES, APPEAL_OUTCOME_OPTIONS,
  type DisciplinaryCase, type DisciplineAppealOutcomeType,
} from '@/types/hr/discipline';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );
}

/**
 * A disciplinary case.
 *
 * Slice 1 owns the case header: the whole graph is readable here and the lifecycle transitions run
 * from here, but the sub-entities (investigation, hearing, sanctions, appeal) are READ-ONLY for now.
 * Their editors arrive with their own slices, along with the rules that govern them — an editable
 * sanction with no authority rule behind it (FR-HR-080) would be worse than none.
 */
export default function DisciplineCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [decisionOpen, setDecisionOpen] = useState(false);
  const [closeOpen, setCloseOpen] = useState(false);
  const [waiveOpen, setWaiveOpen] = useState(false);
  const [waiveReason, setWaiveReason] = useState('');
  const [appealHearingOpen, setAppealHearingOpen] = useState(false);
  const [appealHearingDate, setAppealHearingDate] = useState('');
  const [appealHearingVenue, setAppealHearingVenue] = useState('');
  const [appealOutcomeOpen, setAppealOutcomeOpen] = useState(false);
  const [appealOutcome, setAppealOutcome] = useState<DisciplineAppealOutcomeType | ''>('');
  const [appealOutcomeNotes, setAppealOutcomeNotes] = useState('');
  const [actionTypeId, setActionTypeId] = useState('');
  const [actionDetails, setActionDetails] = useState('');
  const [decisionRationale, setDecisionRationale] = useState('');
  const [closureNotes, setClosureNotes] = useState('');

  const { data: c, isLoading, isError } = useQuery({
    queryKey: ['hr', 'discipline', 'case', id],
    queryFn: () => disciplineService.getById(id),
  });

  const { data: actionTypes } = useQuery({
    queryKey: ['hr', 'discipline', 'action-types', 'active'],
    queryFn: () => disciplineLookupService.getActiveActionTypes(),
    enabled: decisionOpen,
  });

  const { data: clock } = useQuery({
    queryKey: ['hr', 'discipline', 'process-clock', id],
    queryFn: () => disciplineService.getProcessClock(id),
    enabled: !!c,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline'] });

  // The decision segment of the case runs on the generic workflow engine. The screen never sets a
  // status itself — it refetches and lets the adapter decide, which is the whole contract.
  const workflow = useWorkflowRecord({
    entityType: 'StaffDisciplinaryAction',
    entityId: id,
    entityLabel: 'Disciplinary Decision',
    entityNumber: c?.caseNumber,
    status: c?.status ?? 'Draft',
    // Proposing has its own dialog below, because it carries the sanction and the rationale.
    canSubmit: false,
    canApproveReject: c?.status === 'AwaitingDecision',
    enabled: !!c,
    commands: {
      approve: (ctx) => disciplineService.approveDecision(id, ctx.comments || null),
      reject: (ctx) => disciplineService.rejectDecision(id, ctx.comments || 'Refused'),
      afterAction: async () => { refresh(); },
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  // Each lifecycle mutation surfaces the server's own refusal text rather than a generic failure.
  // The services answer 422 with the rule — "Only Draft cases can be submitted" — and that message
  // is the whole point of the business-rules filter slice 0 added.
  const submitMutation = useMutation({
    mutationFn: () => disciplineService.submit(id),
    onSuccess: () => { toast({ title: 'Case submitted' }); refresh(); },
    onError: (e: Error) => toast({ title: 'Could not submit', description: e.message, variant: 'destructive' }),
  });

  const startReviewMutation = useMutation({
    mutationFn: () => disciplineService.startReview(id),
    onSuccess: () => { toast({ title: 'Case moved to under review' }); refresh(); },
    onError: (e: Error) => toast({ title: 'Could not start review', description: e.message, variant: 'destructive' }),
  });

  const decisionMutation = useMutation({
    mutationFn: () => disciplineService.recordDecision(id, {
      caseId: id,
      actionTypeId,
      actionDetails: actionDetails || null,
      decisionRationale: decisionRationale || null,
      decisionDate: new Date().toISOString(),
    }),
    onSuccess: () => {
      toast({ title: 'Decision recorded' });
      setDecisionOpen(false);
      setActionTypeId('');
      setActionDetails('');
      setDecisionRationale('');
      refresh();
    },
    onError: (e: Error) => toast({ title: 'Could not record the decision', description: e.message, variant: 'destructive' }),
  });

  const closeMutation = useMutation({
    mutationFn: () => disciplineService.close(id, {
      caseId: id,
      closedDate: new Date().toISOString(),
      closureNotes: closureNotes || null,
    }),
    onSuccess: () => {
      toast({ title: 'Case closed' });
      setCloseOpen(false);
      setClosureNotes('');
      refresh();
    },
    onError: (e: Error) => toast({ title: 'Could not close the case', description: e.message, variant: 'destructive' }),
  });

  const holdMutation = useMutation({
    mutationFn: () => disciplineService.hold(id),
    onSuccess: () => { toast({ title: 'Case put on hold' }); refresh(); },
    onError: (e: Error) => toast({ title: 'Could not hold', description: e.message, variant: 'destructive' }),
  });

  const reactivateMutation = useMutation({
    mutationFn: () => disciplineService.reactivate(id),
    onSuccess: () => { toast({ title: 'Case reactivated' }); refresh(); },
    onError: (e: Error) => toast({ title: 'Could not reactivate', description: e.message, variant: 'destructive' }),
  });

  const dismissMutation = useMutation({
    mutationFn: () => disciplineService.dismiss(id),
    onSuccess: () => { toast({ title: 'Case dismissed' }); refresh(); },
    onError: (e: Error) => toast({ title: 'Could not dismiss', description: e.message, variant: 'destructive' }),
  });

  const waiveMutation = useMutation({
    mutationFn: () => disciplineService.waiveQueryOpportunity(id, waiveReason),
    onSuccess: () => {
      toast({
        title: 'Recorded on the case',
        description: 'The reason is now part of the disciplinary record and will be visible on it.',
      });
      setWaiveOpen(false);
      setWaiveReason('');
      refresh();
    },
    onError: (e: Error) => toast({ title: 'Could not record it', description: e.message, variant: 'destructive' }),
  });

  const appealHearingMutation = useMutation({
    mutationFn: () => disciplineAppealService.scheduleHearing(id, {
      caseId: id,
      hearingDate: new Date(appealHearingDate).toISOString(),
      hearingVenue: appealHearingVenue || null,
    }),
    onSuccess: () => {
      toast({ title: 'Appeal hearing scheduled' });
      setAppealHearingOpen(false);
      setAppealHearingDate('');
      setAppealHearingVenue('');
      refresh();
    },
    onError: (e: Error) => toast({ title: 'Could not schedule it', description: e.message, variant: 'destructive' }),
  });

  const appealOutcomeMutation = useMutation({
    mutationFn: () => disciplineAppealService.recordOutcome(id, {
      caseId: id,
      appealOutcome: appealOutcome as DisciplineAppealOutcomeType,
      appealOutcomeNotes: appealOutcomeNotes || null,
      appealOutcomeDate: new Date().toISOString(),
    }),
    onSuccess: () => {
      toast({ title: 'Appeal outcome recorded' });
      setAppealOutcomeOpen(false);
      setAppealOutcome('');
      setAppealOutcomeNotes('');
      refresh();
    },
    onError: (e: Error) => toast({ title: 'Could not record it', description: e.message, variant: 'destructive' }),
  });

  const recallMutation = useMutation({
    mutationFn: () => disciplineService.recallDecision(id),
    onSuccess: () => {
      toast({
        title: 'Decision recalled',
        description: 'The case has returned to review and the proposed sanction has been cleared.',
      });
      refresh();
    },
    onError: (e: Error) => toast({ title: 'Could not recall', description: e.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !c) {
    return (
      <div className="p-6">
        <EmptyState
          title="Case not available"
          description="It may have been deleted, or it belongs to an employee whose record you cannot see."
        />
      </div>
    );
  }

  const isTerminal = TERMINAL_CASE_STATUSES.includes(c.status);
  const detail = c as DisciplinaryCase;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${detail.caseNumber} — ${detail.employeeName}`}
        description={`${detail.offenseName} · incident ${fmtDate(detail.incidentDate)}`}
        backHref="/hr/discipline"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={detail.severityName} />
            <StatusBadge status={detail.statusName} />
          </div>
        }
      />

      {/* FR-HR-177 and FR-HR-178. Shown as a statement of fact, not as an obstacle: a missed
          deadline already happened and blocking the next step cannot undo it, so nothing here
          disables anything. The wording is the server's, so the screen and any report say the same
          thing about the same case. */}
      {clock && clock.advisories.length > 0 && (
        <Card className={clock.queryBreached || clock.investigationBreached ? 'border-destructive' : undefined}>
          <CardContent className="space-y-2 p-4">
            <div className="flex items-center gap-2 text-sm font-medium">
              <AlertTriangle
                className={`h-4 w-4 ${clock.queryBreached || clock.investigationBreached ? 'text-destructive' : 'text-muted-foreground'}`}
              />
              Process deadlines
            </div>
            <ul className="space-y-1 text-sm text-muted-foreground">
              {clock.advisories.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
            <p className="text-xs text-muted-foreground">
              Written query due {new Date(clock.queryDueAt).toLocaleString()}
              {clock.queryIssuedAt ? ` · issued ${new Date(clock.queryIssuedAt).toLocaleString()}` : ' · not yet issued'}
              {clock.investigationDueAt ? ` · investigation due ${fmtDate(clock.investigationDueAt)}` : ''}
            </p>
          </CardContent>
        </Card>
      )}

      {/* The confirming officer's actions, when the caller is one. The engine decides whether they
          are — this component asks it, and renders nothing when they are not. */}
      <WorkflowApprovalActions {...workflow.actionProps} />

      {/* Lifecycle. Every button is offered unconditionally except where the status makes it
          meaningless — the server owns the rules and answers 422 with the reason, which the toast
          shows verbatim. Hiding buttons on a guess would put a second, disagreeing copy of the rules
          in the client. */}
      {!isTerminal && (
        <Card>
          <CardContent className="flex flex-wrap gap-2 p-4">
            {detail.status === 'AwaitingDecision' && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => recallMutation.mutate()}
                disabled={recallMutation.isPending}
              >
                <RotateCcw className="mr-2 h-4 w-4" /> Recall decision
              </Button>
            )}
            {detail.status === 'Draft' && (
              <Button size="sm" onClick={() => submitMutation.mutate()} disabled={submitMutation.isPending}>
                <Send className="mr-2 h-4 w-4" /> Submit
              </Button>
            )}
            {detail.status === 'Reported' && (
              <Button size="sm" onClick={() => startReviewMutation.mutate()} disabled={startReviewMutation.isPending}>
                <Play className="mr-2 h-4 w-4" /> Start review
              </Button>
            )}
            {/* The one place a button IS disabled on client-side knowledge — because the server has
                already told us it will refuse, and why. The tooltip carries the server's own words,
                so the two cannot drift. Everything else here stays enabled and lets the 422 explain. */}
            <Button
              size="sm"
              variant="outline"
              onClick={() => setDecisionOpen(true)}
              disabled={clock ? !clock.canProposeDecision : false}
              title={clock?.decisionBlockedReason ?? undefined}
            >
              <Gavel className="mr-2 h-4 w-4" /> Propose decision
            </Button>
            {clock && !clock.canProposeDecision && (
              <Button size="sm" variant="ghost" onClick={() => setWaiveOpen(true)}>
                <UserX className="mr-2 h-4 w-4" /> Employee cannot be heard
              </Button>
            )}
            <Button size="sm" variant="outline" onClick={() => setCloseOpen(true)}>
              <Lock className="mr-2 h-4 w-4" /> Close
            </Button>
            {detail.status === 'OnHold' ? (
              <Button size="sm" variant="outline" onClick={() => reactivateMutation.mutate()}>
                <RotateCcw className="mr-2 h-4 w-4" /> Reactivate
              </Button>
            ) : (
              <Button size="sm" variant="outline" onClick={() => holdMutation.mutate()}>
                <PauseCircle className="mr-2 h-4 w-4" /> Put on hold
              </Button>
            )}
            <Button size="sm" variant="ghost" onClick={() => dismissMutation.mutate()}>
              <XCircle className="mr-2 h-4 w-4" /> Dismiss
            </Button>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="process">Investigation &amp; hearing</TabsTrigger>
          <TabsTrigger value="sanctions">Sanctions</TabsTrigger>
          <TabsTrigger value="record">Case file</TabsTrigger>
          <TabsTrigger value="appeal">Appeal</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader><CardTitle>The allegation</CardTitle></CardHeader>
            <CardContent className="grid gap-5 md:grid-cols-3">
              <Field label="Employee" value={`${detail.employeeName}${detail.employeeNumber ? ` (${detail.employeeNumber})` : ''}`} />
              <Field label="Offence" value={detail.offenseName} />
              <Field label="Severity" value={<StatusBadge status={detail.severityName} />} />
              <Field label="Incident date" value={fmtDate(detail.incidentDate)} />
              <Field label="Reported" value={fmtDate(detail.reportedDate)} />
              <Field label="Reported by" value={detail.reportedByName} />
              <Field label="Reported to" value={detail.reportedToName} />
              <Field label="Investigation required" value={detail.requiresInvestigation ? 'Yes' : 'No'} />
              <Field label="Hearing required" value={detail.hearingRequired ? 'Yes' : 'No'} />
              <div className="md:col-span-3">
                <Field label="What happened" value={<p className="whitespace-pre-wrap">{detail.incidentDescription}</p>} />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Decision</CardTitle></CardHeader>
            <CardContent className="grid gap-5 md:grid-cols-3">
              <Field label="Action" value={detail.actionTypeName} />
              <Field label="Decided" value={fmtDate(detail.decisionDate)} />
              <Field label="Decided by" value={detail.decisionByName} />
              <div className="md:col-span-3">
                <Field label="Rationale" value={detail.decisionRationale} />
              </div>
              <Field label="Closed" value={fmtDate(detail.closedDate)} />
              <Field label="Closed by" value={detail.closedByName} />
              <div className="md:col-span-3">
                <Field label="Closure notes" value={detail.closureNotes} />
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="process" className="space-y-4 pt-4">
          <Card>
            <CardHeader><CardTitle>Investigation</CardTitle></CardHeader>
            <CardContent>
              {detail.investigation ? (
                <div className="grid gap-5 md:grid-cols-3">
                  <Field label="Investigator" value={detail.investigation.investigatorName} />
                  <Field label="Started" value={fmtDate(detail.investigation.investigationStartDate)} />
                  <Field label="Completed" value={fmtDate(detail.investigation.investigationEndDate)} />
                  <div className="md:col-span-3">
                    <Field label="Findings" value={detail.investigation.investigationFindings} />
                  </div>
                  <div className="md:col-span-3">
                    <Field label="Evidence collected" value={detail.investigation.evidenceCollected} />
                  </div>
                </div>
              ) : (
                <EmptyState title="No investigation" description="None has been opened for this case." />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Hearing</CardTitle></CardHeader>
            <CardContent>
              {detail.hearing ? (
                <div className="grid gap-5 md:grid-cols-3">
                  <Field label="Date" value={fmtDate(detail.hearing.hearingDate)} />
                  <Field label="Venue" value={detail.hearing.hearingVenue} />
                  <Field label="Hearing officer" value={detail.hearing.hearingOfficerName} />
                  <Field label="Employee's representative" value={detail.hearing.representativeEmployeeName} />
                  <div className="md:col-span-3">
                    <Field label="Notes" value={detail.hearing.hearingNotes} />
                  </div>
                </div>
              ) : (
                <EmptyState title="No hearing" description="None has been scheduled for this case." />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Procedure steps</CardTitle></CardHeader>
            <CardContent className="p-0">
              {detail.actionSteps.length === 0 ? (
                <EmptyState title="No steps" description="The offence's procedure has not been initialised for this case." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Step</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>Completed</TableHead>
                      <TableHead>By</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {detail.actionSteps.map((s) => (
                      <TableRow key={s.id}>
                        <TableCell>{s.stepName ?? '—'}</TableCell>
                        <TableCell>{fmtDate(s.dueDate)}</TableCell>
                        <TableCell>{fmtDate(s.completedDate)}</TableCell>
                        <TableCell>{s.actionedByName ?? '—'}</TableCell>
                        <TableCell><StatusBadge status={s.statusName} /></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="sanctions" className="space-y-4 pt-4">
          <Card>
            <CardHeader><CardTitle>Warning</CardTitle></CardHeader>
            <CardContent>
              {detail.warning ? (
                <div className="grid gap-5 md:grid-cols-3">
                  <Field label="Type" value={<StatusBadge status={detail.warning.warningTypeName} />} />
                  <Field label="Expires" value={fmtDate(detail.warning.warningExpiryDate)} />
                  <Field label="Letter reference" value={detail.warning.warningLetterReference} />
                </div>
              ) : (
                <EmptyState title="No warning" description="No warning has been issued on this case." />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Suspension</CardTitle></CardHeader>
            <CardContent>
              {detail.suspension ? (
                <div className="grid gap-5 md:grid-cols-3">
                  <Field label="From" value={fmtDate(detail.suspension.suspensionStartDate)} />
                  <Field label="To" value={fmtDate(detail.suspension.suspensionEndDate)} />
                  <Field
                    label="Pay"
                    value={detail.suspension.suspensionWithPay ? 'With pay' : 'Without pay'}
                  />
                  <Field label="Days" value={detail.suspension.suspensionDays ?? '—'} />
                </div>
              ) : (
                <EmptyState title="No suspension" description="No suspension has been imposed on this case." />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Fine</CardTitle></CardHeader>
            <CardContent>
              {detail.fine ? (
                <div className="grid gap-5 md:grid-cols-4">
                  <Field label="Amount" value={money(detail.fine.fineAmount)} />
                  <Field label="Paid" value={money(detail.fine.finePaidAmount)} />
                  <Field label="Due" value={fmtDate(detail.fine.fineDueDate)} />
                  <Field
                    label="Status"
                    value={detail.fine.finePaymentStatusName
                      ? <StatusBadge status={detail.fine.finePaymentStatusName} />
                      : '—'}
                  />
                  <Field label="Outstanding" value={money(detail.fine.outstandingBalance)} />
                  <p className="text-xs text-muted-foreground md:col-span-4">
                    Recovery of a fine is payroll&apos;s to run. This records what was imposed and what
                    has been paid; it does not deduct anything.
                  </p>
                </div>
              ) : (
                <EmptyState title="No fine" description="No fine has been imposed on this case." />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Reduction in rank</CardTitle></CardHeader>
            <CardContent className="p-0">
              {detail.linkedDemotions.length === 0 ? (
                <div className="p-6">
                  <EmptyState
                    title="No reduction in rank"
                    description="No demotion cites this case."
                  />
                  <p className="mt-3 text-xs text-muted-foreground">
                    A reduction in rank is recorded as a staff movement, not here — moving someone
                    down a grade means moving them to a different post, with its own unit and salary,
                    and the movement is what carries that and applies it to their record. Raise it
                    under Staff Movements and cite this case as the reason; it will then appear here.
                  </p>
                </div>
              ) : (
                <>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Movement</TableHead>
                        <TableHead>New post</TableHead>
                        <TableHead>Grades down</TableHead>
                        <TableHead>Effective</TableHead>
                        <TableHead>Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {detail.linkedDemotions.map((d) => (
                        <TableRow key={d.demotionId}>
                          <TableCell className="font-medium">
                            <Link href={`/hr/movements/${d.movementId}`} className="hover:underline">
                              {d.movementNumber || 'Movement'}
                            </Link>
                          </TableCell>
                          <TableCell>{d.newPositionTitle || '—'}</TableCell>
                          <TableCell>{d.gradeLevelDecrease}</TableCell>
                          <TableCell>{fmtDate(d.effectiveDate)}</TableCell>
                          <TableCell><StatusBadge status={d.movementStatus} /></TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                  <p className="p-4 text-xs text-muted-foreground">
                    The movement owns its own approval route and is what changes the employee&apos;s
                    record. This is a link to it, not a second copy.
                  </p>
                </>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Termination</CardTitle></CardHeader>
            <CardContent>
              {detail.termination ? (
                <div className="grid gap-5 md:grid-cols-3">
                  <Field label="Type" value={detail.termination.typeName} />
                  <Field label="Eligible for rehire" value={detail.termination.isEligibleForRehire ? 'Yes' : 'No'} />
                  <Field label="Final pay processed" value={detail.termination.finalPaycheckProcessed ? 'Yes' : 'No'} />
                  <p className="text-xs text-muted-foreground md:col-span-3">
                    Clearance, entitlement computation and the exit process itself belong to the
                    separation module, which is not built yet. What is recorded here is the decision.
                  </p>
                </div>
              ) : (
                <EmptyState title="No termination" description="This case has not resulted in a termination." />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="record" className="space-y-4 pt-4">
          <Card>
            <CardHeader><CardTitle>Witnesses</CardTitle></CardHeader>
            <CardContent className="p-0">
              {detail.witnesses.length === 0 ? (
                <EmptyState title="No witnesses" description="None have been recorded." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Statement</TableHead>
                      <TableHead>Dated</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {detail.witnesses.map((w) => (
                      <TableRow key={w.id}>
                        <TableCell>{w.name}</TableCell>
                        <TableCell>{w.isEmployee ? 'Employee' : 'External'}</TableCell>
                        <TableCell>{w.hasStatement ? 'On file' : 'Outstanding'}</TableCell>
                        <TableCell>{fmtDate(w.statementDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Documents</CardTitle></CardHeader>
            <CardContent className="p-0">
              {detail.documents.length === 0 ? (
                <EmptyState title="No documents" description="Nothing has been attached to this case." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Document</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead>Uploaded</TableHead>
                      <TableHead>By</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {detail.documents.map((d) => (
                      <TableRow key={d.id}>
                        <TableCell>{d.fileName}</TableCell>
                        <TableCell>{d.categoryName}</TableCell>
                        <TableCell>{fmtDate(d.uploadDate)}</TableCell>
                        <TableCell>{d.uploadedByName || '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Notices issued</CardTitle></CardHeader>
            <CardContent className="p-0">
              {detail.notifications.length === 0 ? (
                <EmptyState title="No notices" description="Nothing has been issued to the employee on this case." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Sent</TableHead>
                      <TableHead>Acknowledged</TableHead>
                      <TableHead>Follow-up</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {detail.notifications.map((n) => (
                      <TableRow key={n.id}>
                        <TableCell>{n.notificationTypeName}</TableCell>
                        <TableCell>{fmtDate(n.sentDate)}</TableCell>
                        <TableCell>
                          {n.isAcknowledged
                            ? 'Acknowledged'
                            : <span className="text-muted-foreground">Not yet</span>}
                        </TableCell>
                        <TableCell>{n.isFollowupSent ? 'Follow-up sent' : '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Case notes</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              {detail.notes.length === 0 ? (
                <EmptyState title="No notes" description="Nothing has been recorded against this case." />
              ) : (
                detail.notes.map((n) => (
                  <div key={n.id} className="rounded-md border p-3">
                    <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                      <span>{n.createdByEmployeeName || 'Unknown'}</span>
                      <span>·</span>
                      <span>{fmtDate(n.noteDate)}</span>
                      {n.isConfidential && <StatusBadge status="Confidential" />}
                    </div>
                    {/* An excerpt, not the whole note — the case-file read returns summaries. */}
                    <p className="whitespace-pre-wrap text-sm">{n.noteExcerpt}</p>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="appeal" className="space-y-4 pt-4">
          <Card>
            <CardHeader><CardTitle>Appeal</CardTitle></CardHeader>
            <CardContent>
              {detail.appeal ? (
                <div className="grid gap-5 md:grid-cols-3">
                  <Field label="Filed" value={fmtDate(detail.appeal.filedDate)} />
                  <Field label="Status" value={<StatusBadge status={detail.appeal.appealStatusName} />} />
                  <Field label="Appeal officer" value={detail.appeal.appealOfficerName} />
                  <Field label="Hearing" value={fmtDate(detail.appeal.hearingDate)} />
                  <Field label="Outcome" value={detail.appeal.appealOutcomeName
                    ? <StatusBadge status={detail.appeal.appealOutcomeName} /> : '—'} />
                  <Field label="Decided by" value={detail.appeal.appealOutcomeByName} />
                  <div className="md:col-span-3">
                    <Field label="Grounds of appeal" value={<p className="whitespace-pre-wrap">{detail.appeal.reason}</p>} />
                  </div>
                  <div className="md:col-span-3">
                    <Field label="Outcome notes" value={detail.appeal.appealOutcomeNotes} />
                  </div>
                </div>
              ) : (
                <EmptyState
                  title="No appeal"
                  description="The employee has not appealed. Only they can file one — an appeal is their own act, so it cannot be filed on their behalf."
                />
              )}
            </CardContent>
          </Card>

          {/* FR-HR-180's two windows, stated plainly. The filing one is enforced, so it says when it
              closes; the decision one is advisory, so it says when it is due and reports lateness
              without preventing anything. */}
          {clock?.decisionMade && (
            <Card>
              <CardContent className="space-y-2 p-4 text-sm">
                <div className="font-medium">Appeal windows</div>
                <p className="text-muted-foreground">
                  {clock.appealFiled
                    ? <>Appeal filed {fmtDate(clock.appealFiledAt)}. A decision on it is due {fmtDate(clock.appealDecisionDueAt)}.</>
                    : clock.appealFilingWindowOpen
                      ? <>The employee has until {fmtDate(clock.appealFilingClosesAt)} to appeal — five working days from the decision.</>
                      : <>The window to appeal closed on {fmtDate(clock.appealFilingClosesAt)}.</>}
                </p>
                {clock.appealDecisionBreached && (
                  <p className="text-destructive">
                    The ten-working-day limit for deciding this appeal passed{' '}
                    {clock.appealDecisionWorkingDaysLate} working day(s) ago. It can still be decided —
                    this is a record of the delay, not a block.
                  </p>
                )}
              </CardContent>
            </Card>
          )}

          {/* HR's two actions on an appeal. Filing is absent by design: it is the appellant's own act
              and the server refuses it from HR, so offering the button would only produce a 403. */}
          {detail.appeal && !isTerminal && (
            <Card>
              <CardContent className="flex flex-wrap gap-2 p-4">
                <Button size="sm" variant="outline" onClick={() => setAppealHearingOpen(true)}>
                  <CalendarClock className="mr-2 h-4 w-4" /> Schedule appeal hearing
                </Button>
                <Button size="sm" variant="outline" onClick={() => setAppealOutcomeOpen(true)}>
                  <Scale className="mr-2 h-4 w-4" /> Record appeal outcome
                </Button>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="StaffDisciplinaryAction"
          entityId={id}
          entityLabel="Disciplinary Decision"
          entityNumber={detail.caseNumber}
          status={detail.status}
          workflowSummary={workflow.summary}
          canApproveReject={detail.status === 'AwaitingDecision'}
          onAfterAction={async () => {
            refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      {/* Record decision */}
      <Dialog open={decisionOpen} onOpenChange={setDecisionOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Propose the decision</DialogTitle>
            <DialogDescription>
              You are recorded as the deciding officer. This does not finalise the sanction — the
              case goes to a confirming officer, and becomes a decision once they confirm it.
              Who that is depends on the authority the chosen action requires.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Action *</Label>
              <Select value={actionTypeId} onValueChange={setActionTypeId}>
                <SelectTrigger><SelectValue placeholder="Choose the sanction" /></SelectTrigger>
                <SelectContent>
                  {(actionTypes ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.code ? `${t.code} — ${t.name}` : t.name}
                      {t.minimumAuthorityName ? ` · ${t.minimumAuthorityName} authority` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Details</Label>
              <Textarea rows={2} value={actionDetails} onChange={(e) => setActionDetails(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Rationale</Label>
              <Textarea
                rows={4}
                placeholder="Why this outcome, on this evidence."
                value={decisionRationale}
                onChange={(e) => setDecisionRationale(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDecisionOpen(false)}>Cancel</Button>
            <Button
              onClick={() => decisionMutation.mutate()}
              disabled={!actionTypeId || decisionMutation.isPending}
            >
              {decisionMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record decision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* The recorded override on the natural-justice gate. Deliberately not a confirm-and-forget
          dialog: it states what is being set aside, requires a reason of real length, and says the
          reason goes on the record. Making it costless would turn the gate into something people
          click past. */}
      <Dialog open={waiveOpen} onOpenChange={setWaiveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record that the employee cannot be heard</DialogTitle>
            <DialogDescription>
              A decision is normally refused until the employee has been issued a written query and
              given a chance to answer it. Use this only where that chance genuinely cannot be given
              — they have absconded, are detained, or refuse service. The reason becomes part of the
              disciplinary record.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Why the opportunity could not be given *</Label>
            <Textarea
              rows={4}
              placeholder="Set out what was attempted and when — dates, addresses tried, who was contacted."
              value={waiveReason}
              onChange={(e) => setWaiveReason(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              At least 10 characters. This is the answer to &ldquo;why was this employee sanctioned
              without being heard?&rdquo;, so write it for someone reading the file later.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setWaiveOpen(false)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={() => waiveMutation.mutate()}
              disabled={waiveReason.trim().length < 10 || waiveMutation.isPending}
            >
              {waiveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record and proceed
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Schedule the appeal hearing */}
      <Dialog open={appealHearingOpen} onOpenChange={setAppealHearingOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Schedule the appeal hearing</DialogTitle>
            <DialogDescription>
              FR-HR-180 tracks an appeal to a decision within ten working days of filing.
              {clock?.appealDecisionDueAt
                ? ` This one is due by ${fmtDate(clock.appealDecisionDueAt)}.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Date and time *</Label>
              <Input
                type="datetime-local"
                value={appealHearingDate}
                onChange={(e) => setAppealHearingDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Venue</Label>
              <Input value={appealHearingVenue} onChange={(e) => setAppealHearingVenue(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAppealHearingOpen(false)}>Cancel</Button>
            <Button
              onClick={() => appealHearingMutation.mutate()}
              disabled={!appealHearingDate || appealHearingMutation.isPending}
            >
              {appealHearingMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Schedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Record the appeal outcome */}
      <Dialog open={appealOutcomeOpen} onOpenChange={setAppealOutcomeOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record the appeal outcome</DialogTitle>
            <DialogDescription>
              You are recorded as the deciding officer. Say enough that the employee can understand
              the reasoning — this is the answer to the case they put.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Outcome *</Label>
              <Select
                value={appealOutcome}
                onValueChange={(v) => setAppealOutcome(v as DisciplineAppealOutcomeType)}
              >
                <SelectTrigger><SelectValue placeholder="Choose the outcome" /></SelectTrigger>
                <SelectContent>
                  {APPEAL_OUTCOME_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {appealOutcome && (
                <p className="text-xs text-muted-foreground">
                  {APPEAL_OUTCOME_OPTIONS.find((o) => o.value === appealOutcome)?.hint}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label>Reasons</Label>
              <Textarea
                rows={4}
                value={appealOutcomeNotes}
                onChange={(e) => setAppealOutcomeNotes(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAppealOutcomeOpen(false)}>Cancel</Button>
            <Button
              onClick={() => appealOutcomeMutation.mutate()}
              disabled={!appealOutcome || appealOutcomeMutation.isPending}
            >
              {appealOutcomeMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record outcome
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Close */}
      <Dialog open={closeOpen} onOpenChange={setCloseOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close this case</DialogTitle>
            <DialogDescription>
              A closed case cannot be edited afterwards.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Closure notes</Label>
            <Textarea rows={4} value={closureNotes} onChange={(e) => setClosureNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloseOpen(false)}>Cancel</Button>
            <Button onClick={() => closeMutation.mutate()} disabled={closeMutation.isPending}>
              {closeMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Close case
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
