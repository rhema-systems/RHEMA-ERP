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
import { LinkedErCasesPanel } from '@/components/hr/employee-relations/LinkedErCasesPanel';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { CaseProcessPanel } from '@/components/hr/discipline/CaseProcessPanel';
import { SanctionsPanel } from '@/components/hr/discipline/SanctionsPanel';
import { ActionStepsPanel } from '@/components/hr/discipline/ActionStepsPanel';
import { LegalReviewsPanel } from '@/components/hr/discipline/LegalReviewsPanel';
import { CorrectiveActionPanel } from '@/components/hr/discipline/CorrectiveActionPanel';
import { WitnessesPanel } from '@/components/hr/discipline/WitnessesPanel';
import { CaseDocumentsPanel } from '@/components/hr/discipline/CaseDocumentsPanel';
import { CaseNotesPanel } from '@/components/hr/discipline/CaseNotesPanel';
import { NotificationsPanel } from '@/components/hr/discipline/NotificationsPanel';
import { disciplineService, disciplineLookupService, disciplineAppealService } from '@/services/hr/discipline.service';
import { useAuth } from '@/hooks/use-auth';
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
 * Slice 1 owned the case header: the whole graph is readable here and the lifecycle transitions run
 * from here, while the sub-entities stayed READ-ONLY until the rules that govern them existed — an
 * editable sanction with no authority rule behind it (FR-HR-080) would be worse than none.
 *
 * The procedure steps, the legal reviews and the corrective action plan are PROCEDURAL: nothing
 * about who may record them turns on FR-HR-080, because none of them imposes a penalty. The
 * investigation and hearing are recordable for the same reason — they were never blocked on
 * anything, and had simply had no editors built.
 *
 * ⚠ **The sanctions became editable on 2026-08-31 (`SanctionsPanel`), and the block that held them
 * since slice 1 was half wrong.** It read: no actor reaches the authority rule, and a warning can be
 * recorded against an undecided case. Re-run properly:
 *
 *  · **The first half was FALSE.** It rested on two probe assertions expecting a **401** where a
 *    permission refusal is a 403 — they had passed against tokens that had gone stale, proving
 *    nothing. The rule worked; what it lacked was any way for a head of department to reach it,
 *    because headship is DATA (`OrganizationUnits.HeadEmployeeId`) and no policy can express it.
 *    Authority is now resolved from the organisation and the decision route is ungated.
 *  · **The second half was TRUE and understated.** Not just a warning — warning, suspension AND
 *    fine were all accepted with no decision behind them; only termination was guarded. All four now
 *    share one guard, and it refuses a merely PROPOSED decision too.
 *
 * `probe-authority-gate.mjs` (33) and `probe-sanctions.mjs` (30) hold both, and now prove the gate
 * WORKS rather than that it does not.
 */
export default function DisciplineCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  /**
   * What the signed-in user may do to the case's procedural records.
   *
   * Two tiers because the API has two: create, update and the transitions sit on
   * `HR.Policy.DisciplineWrite`, while **delete sits on `HR.Policy.DisciplineAdmin`** — legal
   * reviews, corrective action plans and their items can all be recorded by an HR officer and
   * erased only by an administrator. The panels hide the remove affordance rather than offering an
   * action the API refuses.
   *
   * The role check beside each permission mirrors `HrPermissions.RoleGrants`: permissions resolve
   * from the database, so on a tenant provisioned before the seeder ran a user holds none, and the
   * server's own fallback is what keeps them working. Gating on the permission alone would black
   * out the screen for exactly those tenants.
   */
  const canWriteDiscipline =
    hasAnyPermission(['HR.Discipline.Write', 'HR.Discipline.Admin']) || hasAnyRole(HR_ROLES);
  const canAdminDiscipline =
    hasAnyPermission(['HR.Discipline.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

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
    recallPrompt: 'reason',
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

      {/* Lane 6: the reverse ER link. HR-desk screen only — the read is ER-permission gated. */}
      <LinkedErCasesPanel source="DisciplinaryCase" recordId={id} />

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
          <CaseProcessPanel
            caseId={id}
            detail={detail}
            canWrite={canWriteDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />

          <ActionStepsPanel
            caseId={id}
            canWrite={canWriteDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />

          {/*
            The corrective action plan lives beside the procedure rather than under Sanctions on
            purpose: it is what the employee agrees to DO, not a penalty imposed on them. Filing it
            with the warnings and fines would misread the record.
          */}
          <CorrectiveActionPanel
            caseId={id}
            employeeId={detail.employeeId}
            employeeName={detail.employeeName}
            canWrite={canWriteDiscipline}
            canDelete={canAdminDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />

          <LegalReviewsPanel
            caseId={id}
            canWrite={canWriteDiscipline}
            canDelete={canAdminDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />
        </TabsContent>

        <TabsContent value="sanctions" className="space-y-4 pt-4">
          {/*
            ⚠ Editable since 2026-08-31. This tab was read-only from slice 1 under the note that a
            sanction must wait "until the issuing-authority rule is in place" — see the block
            comment at the top of this file for what turned out to be true and what did not.
          */}
          <SanctionsPanel detail={detail} />

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
        </TabsContent>

        <TabsContent value="record" className="space-y-4 pt-4">
          {/*
            The case file, in the order an investigator builds one: who was asked, what was put to
            them formally, what was collected, and what HR thought about it.
          */}
          <WitnessesPanel
            caseId={id}
            canWrite={canWriteDiscipline}
            canDelete={canAdminDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />

          <NotificationsPanel
            caseId={id}
            canWrite={canWriteDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />

          <CaseDocumentsPanel
            caseId={id}
            appealId={detail.appeal?.id ?? null}
            canWrite={canWriteDiscipline}
            canDelete={canAdminDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />

          <CaseNotesPanel
            caseId={id}
            canWrite={canWriteDiscipline}
            canDelete={canAdminDiscipline}
            onChanged={() => queryClient.invalidateQueries({ queryKey: ['hr', 'discipline', 'case', id] })}
          />
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
