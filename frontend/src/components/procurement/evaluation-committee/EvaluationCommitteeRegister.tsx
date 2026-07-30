'use client';

import React from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  FileLock2,
  History,
  MonitorUp,
  ShieldCheck,
  UserCheck,
  Users,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import {
  hasAnyEvaluationCommitteeAction,
} from '@/lib/procurement-evaluation-committee';
import type {
  ProcurementEvaluationAppointment,
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeReadiness,
  ProcurementEvaluationMeeting,
  ProcurementEvaluationPhase,
  ProcurementEvaluationScorerEligibility,
  ProcurementEvaluationScoreRecall,
  ProcurementEvaluationScoreSheet,
} from '@/types/procurement-evaluation-committee';

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const roleLabel = (value: string) =>
  value.replace(/([a-z])([A-Z])/g, '$1 $2');

const statusVariant = (
  status: string
): 'default' | 'destructive' | 'outline' | 'secondary' => {
  if (
    ['Active', 'Accepted', 'NoConflict', 'QuorumConfirmed', 'Locked', 'Approved'].includes(
      status
    )
  )
    return 'default';
  if (
    ['Declined', 'Withdrawn', 'ConflictDeclared', 'QuorumFailed', 'Rejected'].includes(
      status
    )
  )
    return 'destructive';
  if (['Draft', 'Pending', 'PendingApproval'].includes(status)) return 'secondary';
  return 'outline';
};

const serverAllows = (actions: string[] | undefined, aliases: string[]) =>
  hasAnyEvaluationCommitteeAction(actions, aliases);

export interface EvaluationCommitteeRegisterProps {
  readiness: ProcurementEvaluationCommitteeReadiness;
  control?: ProcurementEvaluationCommitteeControl;
  scorerEligibility: Partial<
    Record<ProcurementEvaluationPhase, ProcurementEvaluationScorerEligibility>
  >;
  currentUserId?: string;
  canAdminister: boolean;
  canEvaluate: boolean;
  canApprove: boolean;
  onBind: () => void;
  onActivate: () => void;
  onAppointment: (
    member: ProcurementEvaluationAppointment,
    accept: boolean
  ) => void;
  onDeclareCoi: (member: ProcurementEvaluationAppointment) => void;
  onCreateMeeting: () => void;
  onSignAttendance: (
    meeting: ProcurementEvaluationMeeting,
    member: ProcurementEvaluationAppointment
  ) => void;
  onConfirmQuorum: (meeting: ProcurementEvaluationMeeting) => void;
  onRequestRecall: (scoreSheet: ProcurementEvaluationScoreSheet) => void;
  onDecideRecall: (
    recall: ProcurementEvaluationScoreRecall,
    approve: boolean
  ) => void;
}

export function EvaluationCommitteeRegister({
  readiness,
  control,
  scorerEligibility,
  currentUserId,
  canAdminister,
  canEvaluate,
  canApprove,
  onBind,
  onActivate,
  onAppointment,
  onDeclareCoi,
  onCreateMeeting,
  onSignAttendance,
  onConfirmQuorum,
  onRequestRecall,
  onDecideRecall,
}: EvaluationCommitteeRegisterProps) {
  const currentMember = control?.members.find(
    (member) => member.userId === currentUserId
  );
  const canBind =
    canAdminister &&
    serverAllows(readiness.allowedActions, [
      'Bind',
      'BindCommittee',
      'CreateCommitteeControl',
    ]);
  const canActivate =
    canAdminister &&
    Boolean(control) &&
    serverAllows(control?.allowedActions, ['Activate', 'ActivateCommittee']);
  const canCreateMeeting =
    canAdminister &&
    Boolean(control) &&
    serverAllows(control?.allowedActions, ['CreateMeeting', 'RecordMeeting']);

  return (
    <div className="space-y-6" data-testid="evaluation-committee-register">
      <ReadinessOverview readiness={readiness} control={control} />

      {readiness.blockedReasons.length > 0 && (
        <Alert className="border-amber-500/40 bg-amber-500/5">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Evaluation controls are not ready</AlertTitle>
          <AlertDescription>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              {readiness.blockedReasons.map((reason) => (
                <li key={reason}>{reason}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {!control ? (
        <Card className="border-dashed" data-testid="committee-empty-state">
          <CardContent className="flex min-h-64 flex-col items-center justify-center p-8 text-center">
            <Users className="mb-4 h-10 w-10 text-muted-foreground" />
            <h2 className="text-lg font-semibold">
              No source-specific committee has been constituted
            </h2>
            <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
              Bind this procurement source to an exact active Evaluation Committee
              snapshot. The reusable committee remains administered in Access &amp;
              Committees; this record retains the source-specific appointment,
              declaration, quorum, and score history.
            </p>
            {canBind && (
              <Button className="mt-5" onClick={onBind}>
                <Users className="mr-2 h-4 w-4" />
                Constitute evaluation committee
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader>
              <div className="flex flex-col justify-between gap-3 md:flex-row md:items-start">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle>{control.committeeName}</CardTitle>
                    <Badge variant="outline">{control.committeeCode}</Badge>
                    <Badge variant={statusVariant(control.status)}>
                      {control.status}
                    </Badge>
                    <Badge variant="outline">Snapshot v{control.version}</Badge>
                  </div>
                  <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
                    {control.purpose}
                  </p>
                </div>
                {canActivate && (
                  <Button onClick={onActivate}>
                    <ShieldCheck className="mr-2 h-4 w-4" />
                    Activate exact composition
                  </Button>
                )}
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <Lineage
                  label="Policy"
                  value={`${control.policyCode} · v${control.policyVersion}`}
                />
                <Lineage
                  label="Configuration"
                  value={
                    control.configurationProfileCode
                      ? `${control.configurationProfileCode} · v${control.configurationProfileVersion}`
                      : 'Policy-owned configuration'
                  }
                />
                <Lineage label="Method rule" value={control.methodRuleCode} />
                <Lineage
                  label="Effective period"
                  value={`${formatDate(control.effectiveFromUtc)} → ${formatDate(control.effectiveToUtc)}`}
                />
              </div>
              <p className="mt-4 break-all font-mono text-[11px] text-muted-foreground">
                Composition hash: {control.compositionIntegrityHash}
              </p>
            </CardContent>
          </Card>

          <CompositionSection
            control={control}
            currentMember={currentMember}
            canEvaluate={canEvaluate}
            onAppointment={onAppointment}
            onDeclareCoi={onDeclareCoi}
          />

          <MeetingSection
            control={control}
            currentMember={currentMember}
            canAdminister={canAdminister}
            canCreateMeeting={canCreateMeeting}
            canEvaluate={canEvaluate}
            onCreateMeeting={onCreateMeeting}
            onSignAttendance={onSignAttendance}
            onConfirmQuorum={onConfirmQuorum}
          />

          <ScoreSection
            control={control}
            scorerEligibility={scorerEligibility}
            currentUserId={currentUserId}
            canEvaluate={canEvaluate}
            canApprove={canApprove}
            onRequestRecall={onRequestRecall}
            onDecideRecall={onDecideRecall}
          />

          <TimelineSection control={control} />
        </>
      )}
    </div>
  );
}

function ReadinessOverview({
  readiness,
  control,
}: {
  readiness: ProcurementEvaluationCommitteeReadiness;
  control?: ProcurementEvaluationCommitteeControl;
}) {
  const cards = [
    {
      label: 'Composition',
      value: readiness.compositionReady ? 'Ready' : 'Blocked',
      ok: readiness.compositionReady,
      detail: control
        ? `${control.members.length} retained appointments`
        : 'Exact committee snapshot',
    },
    {
      label: 'Acceptance',
      value: readiness.appointmentsReady ? 'Complete' : 'Pending',
      ok: readiness.appointmentsReady,
      detail: 'Member appointment responses',
    },
    {
      label: 'COI declarations',
      value: readiness.declarationsReady ? 'Current' : 'Incomplete',
      ok: readiness.declarationsReady,
      detail: 'Signed, versioned declarations',
    },
    {
      label: 'Quorum',
      value: `${readiness.signedVotingAttendanceCount}/${readiness.requiredQuorum}`,
      ok: readiness.quorumMet,
      detail: readiness.quorumMet
        ? 'Chair and Secretary present'
        : `${readiness.eligibleVotingMemberCount} eligible voters`,
    },
  ];
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map((card) => (
        <Card key={card.label}>
          <CardContent className="pt-5">
            <div className="flex items-center justify-between gap-2">
              <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {card.label}
              </p>
              {card.ok ? (
                <CheckCircle2 className="h-4 w-4 text-emerald-600" />
              ) : (
                <AlertTriangle className="h-4 w-4 text-amber-600" />
              )}
            </div>
            <p className="mt-2 text-2xl font-semibold">{card.value}</p>
            <p className="mt-1 text-xs text-muted-foreground">{card.detail}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function CompositionSection({
  control,
  currentMember,
  canEvaluate,
  onAppointment,
  onDeclareCoi,
}: {
  control: ProcurementEvaluationCommitteeControl;
  currentMember?: ProcurementEvaluationAppointment;
  canEvaluate: boolean;
  onAppointment: (
    member: ProcurementEvaluationAppointment,
    accept: boolean
  ) => void;
  onDeclareCoi: (member: ProcurementEvaluationAppointment) => void;
}) {
  return (
    <Card data-testid="committee-composition-history">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <UserCheck className="h-5 w-5" />
          Composition, acceptance, and COI history
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        <div>
          <div className="mb-2 flex items-center justify-between gap-3">
            <p className="text-sm font-medium">Required composition</p>
            <Badge variant={control.compositionReady ? 'default' : 'destructive'}>
              {control.requiredRoles.filter((role) => role.isMet).length}/
              {control.requiredRoles.length} roles met
            </Badge>
          </div>
          <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
            {control.requiredRoles.map((role) => (
              <div key={role.id} className="rounded-md border p-3 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-medium">{roleLabel(role.memberKind)}</p>
                  <Badge variant={role.isMet ? 'default' : 'destructive'}>
                    {role.matchedCount}/{role.minimumCount}
                  </Badge>
                </div>
                <p className="mt-1 text-xs text-muted-foreground">
                  {role.roleName} · {role.isVoting ? 'Voting' : 'Non-voting'}
                  {role.isRequiredForQuorum ? ' · Required for quorum' : ''}
                </p>
              </div>
            ))}
          </div>
        </div>

        <Separator />

        <div className="overflow-x-auto">
          <table className="w-full min-w-[920px] text-sm">
            <thead className="border-b text-left text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2">Member</th>
                <th className="px-3 py-2">Role</th>
                <th className="px-3 py-2">Appointment</th>
                <th className="px-3 py-2">Current COI</th>
                <th className="px-3 py-2">Scorer eligibility</th>
                <th className="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {control.members.map((member) => {
                const isSelf = member.id === currentMember?.id;
                return (
                  <tr key={member.id}>
                    <td className="px-3 py-3">
                      <p className="font-medium">{member.userDisplayName}</p>
                      <p className="text-xs text-muted-foreground">
                        Effective {formatDate(member.effectiveFromUtc)}
                      </p>
                    </td>
                    <td className="px-3 py-3">
                      <p>{roleLabel(member.memberKind)}</p>
                      <p className="text-xs text-muted-foreground">
                        {member.roleName} · {member.isVoting ? 'Voting' : 'Non-voting'}
                      </p>
                    </td>
                    <td className="px-3 py-3">
                      <Badge variant={statusVariant(member.status)}>
                        {member.status}
                      </Badge>
                      {member.acceptedAtUtc && (
                        <p className="mt-1 text-xs text-muted-foreground">
                          {formatDate(member.acceptedAtUtc)}
                        </p>
                      )}
                    </td>
                    <td className="px-3 py-3">
                      {member.currentDeclaration ? (
                        <>
                          <Badge
                            variant={statusVariant(
                              member.currentDeclaration.outcome
                            )}
                          >
                            {roleLabel(member.currentDeclaration.outcome)}
                          </Badge>
                          <p className="mt-1 text-xs text-muted-foreground">
                            v{member.currentDeclaration.version} · valid to{' '}
                            {formatDate(member.currentDeclaration.validToUtc)}
                          </p>
                        </>
                      ) : (
                        <Badge variant="destructive">Missing</Badge>
                      )}
                    </td>
                    <td className="px-3 py-3">
                      <Badge
                        variant={member.eligibleToScore ? 'default' : 'destructive'}
                      >
                        {member.eligibleToScore ? 'Eligible' : 'Blocked'}
                      </Badge>
                      {member.blockedReasons.length > 0 && (
                        <p className="mt-1 max-w-xs text-xs text-muted-foreground">
                          {member.blockedReasons.join(' · ')}
                        </p>
                      )}
                    </td>
                    <td className="px-3 py-3">
                      <div className="flex justify-end gap-2">
                        {canEvaluate &&
                          isSelf &&
                          member.status === 'Pending' &&
                          serverAllows(control.allowedActions, [
                            'respondToAppointment',
                          ]) && (
                          <>
                            <Button
                              size="sm"
                              onClick={() => onAppointment(member, true)}
                            >
                              Accept
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => onAppointment(member, false)}
                            >
                              Decline
                            </Button>
                          </>
                        )}
                        {canEvaluate &&
                          isSelf &&
                          member.status === 'Accepted' &&
                          serverAllows(control.allowedActions, [
                            'submitConflictDeclaration',
                          ]) && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => onDeclareCoi(member)}
                            >
                              {member.currentDeclaration
                                ? 'Renew declaration'
                                : 'Declare COI'}
                            </Button>
                          )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}

function MeetingSection({
  control,
  currentMember,
  canAdminister,
  canCreateMeeting,
  canEvaluate,
  onCreateMeeting,
  onSignAttendance,
  onConfirmQuorum,
}: {
  control: ProcurementEvaluationCommitteeControl;
  currentMember?: ProcurementEvaluationAppointment;
  canAdminister: boolean;
  canCreateMeeting: boolean;
  canEvaluate: boolean;
  onCreateMeeting: () => void;
  onSignAttendance: (
    meeting: ProcurementEvaluationMeeting,
    member: ProcurementEvaluationAppointment
  ) => void;
  onConfirmQuorum: (meeting: ProcurementEvaluationMeeting) => void;
}) {
  return (
    <Card data-testid="committee-meeting-history">
      <CardHeader>
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <CardTitle className="flex items-center gap-2">
            <MonitorUp className="h-5 w-5" />
            Signed attendance and quorum history
          </CardTitle>
          {canCreateMeeting && (
            <Button onClick={onCreateMeeting}>
              <CalendarClock className="mr-2 h-4 w-4" />
              Schedule controlled meeting
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {control.meetings.length === 0 ? (
          <EmptyState
            icon={CalendarClock}
            title="No controlled meeting has been recorded"
            detail="Create the technical, financial, or combined meeting, then collect signed attendance before the server confirms quorum."
          />
        ) : (
          [...control.meetings]
            .sort((a, b) => b.sequence - a.sequence)
            .map((meeting) => {
              const selfAttendance = meeting.attendance.find(
                (item) => item.appointmentId === currentMember?.id
              );
              const canSign =
                canEvaluate &&
                Boolean(currentMember) &&
                currentMember?.status === 'Accepted' &&
                !selfAttendance?.signedAtUtc &&
                meeting.status === 'Draft' &&
                serverAllows(control.allowedActions, ['signAttendance']);
              const canConfirm =
                canAdminister &&
                meeting.status === 'Draft' &&
                meeting.signedVotingAttendanceCount >= control.requiredQuorum &&
                serverAllows(control.allowedActions, ['confirmQuorum']);
              return (
                <div key={meeting.id} className="rounded-lg border p-4">
                  <div className="flex flex-col justify-between gap-3 md:flex-row">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-semibold">
                          Meeting {meeting.sequence} · {meeting.phase}
                        </p>
                        <Badge variant={statusVariant(meeting.status)}>
                          {roleLabel(meeting.status)}
                        </Badge>
                        <Badge variant="outline">{meeting.meetingMode}</Badge>
                      </div>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {formatDate(meeting.scheduledAtUtc)} ·{' '}
                        {meeting.meetingChannel}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        Meeting evidence: {meeting.evidenceReference}
                        {meeting.remoteMeetingEvidenceReference
                          ? ` · Remote evidence: ${meeting.remoteMeetingEvidenceReference}`
                          : ''}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {canSign && currentMember && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => onSignAttendance(meeting, currentMember)}
                        >
                          Sign attendance
                        </Button>
                      )}
                      {canConfirm && (
                        <Button
                          size="sm"
                          onClick={() => onConfirmQuorum(meeting)}
                        >
                          Confirm server quorum
                        </Button>
                      )}
                    </div>
                  </div>
                  <div className="mt-4 grid gap-3 sm:grid-cols-3">
                    <QuorumSignal
                      label="Eligible voting attendance"
                      value={`${meeting.signedVotingAttendanceCount}/${control.requiredQuorum}`}
                      ok={
                        meeting.signedVotingAttendanceCount >=
                        control.requiredQuorum
                      }
                    />
                    <QuorumSignal
                      label="Required Chair"
                      value={meeting.chairPresent ? 'Present' : 'Missing'}
                      ok={meeting.chairPresent}
                    />
                    <QuorumSignal
                      label="Required Secretary"
                      value={meeting.secretaryPresent ? 'Present' : 'Missing'}
                      ok={meeting.secretaryPresent}
                    />
                  </div>
                  <div className="mt-4 overflow-x-auto rounded-md border">
                    <table className="w-full min-w-[680px] text-sm">
                      <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                        <tr>
                          <th className="px-3 py-2">Member</th>
                          <th className="px-3 py-2">Role</th>
                          <th className="px-3 py-2">Attendance</th>
                          <th className="px-3 py-2">Signature</th>
                          <th className="px-3 py-2">Eligibility at signing</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y">
                        {meeting.attendance.map((attendance) => (
                          <tr key={attendance.id}>
                            <td className="px-3 py-2 font-medium">
                              {attendance.userDisplayName}
                            </td>
                            <td className="px-3 py-2">
                              {roleLabel(attendance.memberKind)}
                            </td>
                            <td className="px-3 py-2">
                              {attendance.isPresent ? 'Present' : 'Absent'}
                            </td>
                            <td className="px-3 py-2">
                              {attendance.signatureReference ?? 'Unsigned'}
                            </td>
                            <td className="px-3 py-2">
                              <Badge
                                variant={
                                  attendance.wasEligibleAtSignature
                                    ? 'default'
                                    : 'destructive'
                                }
                              >
                                {attendance.wasEligibleAtSignature
                                  ? 'Eligible'
                                  : 'Ineligible'}
                              </Badge>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <p className="mt-3 break-all font-mono text-[11px] text-muted-foreground">
                    Quorum hash: {meeting.quorumIntegrityHash}
                  </p>
                </div>
              );
            })
        )}
      </CardContent>
    </Card>
  );
}

function ScoreSection({
  control,
  scorerEligibility,
  currentUserId,
  canEvaluate,
  canApprove,
  onRequestRecall,
  onDecideRecall,
}: {
  control: ProcurementEvaluationCommitteeControl;
  scorerEligibility: Partial<
    Record<ProcurementEvaluationPhase, ProcurementEvaluationScorerEligibility>
  >;
  currentUserId?: string;
  canEvaluate: boolean;
  canApprove: boolean;
  onRequestRecall: (scoreSheet: ProcurementEvaluationScoreSheet) => void;
  onDecideRecall: (
    recall: ProcurementEvaluationScoreRecall,
    approve: boolean
  ) => void;
}) {
  return (
    <Card data-testid="committee-score-lock-history">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileLock2 className="h-5 w-5" />
          Scorer eligibility, immutable score sheets, and controlled recall
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 md:grid-cols-3">
          {(['Technical', 'Financial', 'Combined'] as const).map((phase) => {
            const eligibility = scorerEligibility[phase];
            return (
              <div key={phase} className="rounded-md border p-4">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-medium">{phase}</p>
                  <Badge
                    variant={eligibility?.allowed ? 'default' : 'destructive'}
                  >
                    {eligibility?.allowed ? 'Eligible' : 'Blocked'}
                  </Badge>
                </div>
                <p className="mt-2 text-xs text-muted-foreground">
                  {eligibility
                    ? eligibility.allowed
                      ? `Authorized attempt ${eligibility.authorizedAttempt}; quorum meeting resolved.`
                      : eligibility.blockedReasons.join(' · ')
                    : 'Authoritative scorer eligibility is loading.'}
                </p>
                {canEvaluate && eligibility?.allowed && (
                  <Button asChild size="sm" className="mt-3 w-full">
                    <Link
                      href={
                        control.sourceType === 'Tender'
                          ? `/procurement/tenders/${control.sourceId}`
                          : `/procurement/rfqs/${control.sourceId}/controls`
                      }
                    >
                      Open evaluation workspace
                    </Link>
                  </Button>
                )}
              </div>
            );
          })}
        </div>

        {control.scoreSheets.length === 0 ? (
          <EmptyState
            icon={FileLock2}
            title="No immutable score sheet has been submitted"
            detail="Complete scoring in the existing evaluation workspace. Authoritative submission atomically retains the exact signed snapshot here and locks it against update or delete; this register never accepts caller-entered score JSON."
          />
        ) : (
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full min-w-[980px] text-sm">
              <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-3 py-2">Subject</th>
                  <th className="px-3 py-2">Evaluator</th>
                  <th className="px-3 py-2">Phase / attempt</th>
                  <th className="px-3 py-2">Lock state</th>
                  <th className="px-3 py-2">Signed evidence</th>
                  <th className="px-3 py-2 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {[...control.scoreSheets]
                  .sort(
                    (a, b) =>
                      new Date(b.submittedAtUtc).getTime() -
                      new Date(a.submittedAtUtc).getTime()
                  )
                  .map((sheet) => (
                    <tr key={sheet.id}>
                      <td className="px-3 py-3">
                        <p className="font-medium">{sheet.scoreSubjectType}</p>
                        <p className="font-mono text-xs text-muted-foreground">
                          {sheet.scoreSubjectId}
                        </p>
                      </td>
                      <td className="px-3 py-3">
                        <p>{sheet.submittedByName}</p>
                        <p className="text-xs text-muted-foreground">
                          {formatDate(sheet.submittedAtUtc)}
                        </p>
                      </td>
                      <td className="px-3 py-3">
                        {sheet.phase} · attempt {sheet.attempt}
                      </td>
                      <td className="px-3 py-3">
                        <Badge variant={statusVariant(sheet.status)}>
                          {sheet.status}
                        </Badge>
                      </td>
                      <td className="px-3 py-3">
                        <p>{sheet.signatureReference}</p>
                        <p className="text-xs text-muted-foreground">
                          {sheet.evidenceReference}
                        </p>
                      </td>
                      <td className="px-3 py-3 text-right">
                        {sheet.status === 'Locked' &&
                          canEvaluate &&
                          sheet.submittedByUserId === currentUserId &&
                          serverAllows(control.allowedActions, [
                            'requestRecall',
                          ]) && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => onRequestRecall(sheet)}
                          >
                            Request controlled recall
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}

        {control.recalls.length > 0 && (
          <div>
            <h3 className="mb-3 font-medium">Recall decision history</h3>
            <div className="space-y-3">
              {[...control.recalls]
                .sort(
                  (a, b) =>
                    new Date(b.requestedAtUtc).getTime() -
                    new Date(a.requestedAtUtc).getTime()
                )
                .map((recall) => (
                  <div
                    key={recall.id}
                    className="grid gap-3 rounded-md border p-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-center"
                  >
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge variant={statusVariant(recall.status)}>
                          {roleLabel(recall.status)}
                        </Badge>
                        <p className="font-medium">{recall.requestedByName}</p>
                        <span className="text-xs text-muted-foreground">
                          {formatDate(recall.requestedAtUtc)}
                        </span>
                      </div>
                      <p className="mt-2 text-sm">{recall.reason}</p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {recall.evidenceReference}
                        {recall.decisionReference
                          ? ` · ${recall.decisionReference}`
                          : ''}
                        {recall.decisionEvidenceReference
                          ? ` · ${recall.decisionEvidenceReference}`
                          : ''}
                        {recall.authorizedNewAttempt
                          ? ` · New attempt ${recall.authorizedNewAttempt}`
                          : ''}
                      </p>
                    </div>
                    {recall.status === 'PendingApproval' &&
                      canApprove &&
                      serverAllows(control.allowedActions, ['decideRecall']) && (
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          onClick={() => onDecideRecall(recall, true)}
                        >
                          Approve recall
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => onDecideRecall(recall, false)}
                        >
                          Reject
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function TimelineSection({
  control,
}: {
  control: ProcurementEvaluationCommitteeControl;
}) {
  return (
    <Card data-testid="committee-immutable-timeline">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <History className="h-5 w-5" />
          Immutable committee timeline
        </CardTitle>
      </CardHeader>
      <CardContent>
        {control.timeline.length === 0 ? (
          <EmptyState
            icon={History}
            title="No lifecycle events have been retained"
            detail="Committee actions will appear here with actor, outcome, reference, and timestamp."
          />
        ) : (
          <div className="relative ml-2 border-l">
            {[...control.timeline]
              .sort(
                (a, b) =>
                  new Date(b.occurredAtUtc).getTime() -
                  new Date(a.occurredAtUtc).getTime()
              )
              .map((entry, index) => (
                <div key={`${entry.occurredAtUtc}-${entry.action}-${index}`} className="relative pb-6 pl-6 last:pb-0">
                  <span className="absolute -left-1.5 top-1.5 h-3 w-3 rounded-full border bg-background" />
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-medium">{entry.action}</p>
                    <Badge variant="outline">{entry.outcome}</Badge>
                    <span className="text-xs text-muted-foreground">
                      {formatDate(entry.occurredAtUtc)}
                    </span>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {entry.actorName}
                    {entry.reference ? ` · ${entry.reference}` : ''}
                  </p>
                </div>
              ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function Lineage({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 break-words text-sm font-medium">{value}</p>
    </div>
  );
}

function QuorumSignal({
  label,
  value,
  ok,
}: {
  label: string;
  value: string;
  ok: boolean;
}) {
  return (
    <div className="rounded-md bg-muted/40 p-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs text-muted-foreground">{label}</p>
        {ok ? (
          <CheckCircle2 className="h-4 w-4 text-emerald-600" />
        ) : (
          <AlertTriangle className="h-4 w-4 text-amber-600" />
        )}
      </div>
      <p className="mt-1 font-medium">{value}</p>
    </div>
  );
}

function EmptyState({
  icon: Icon,
  title,
  detail,
}: {
  icon: typeof Users;
  title: string;
  detail: string;
}) {
  return (
    <div className="flex min-h-40 flex-col items-center justify-center rounded-md border border-dashed p-6 text-center">
      <Icon className="mb-3 h-8 w-8 text-muted-foreground" />
      <p className="font-medium">{title}</p>
      <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{detail}</p>
    </div>
  );
}
