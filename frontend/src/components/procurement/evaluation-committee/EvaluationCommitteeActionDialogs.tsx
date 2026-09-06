'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
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
import { Textarea } from '@/components/ui/textarea';
import {
  createEvaluationIdempotencyKey,
  validateCoiDeclaration,
  validateCommitteeBinding,
  validateRecallRequest,
} from '@/lib/procurement-evaluation-committee';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { procurementEvaluationCommitteeService as service } from '@/services/procurement-evaluation-committee.service';
import type {
  BindProcurementEvaluationCommitteeRequest,
  ProcurementEvaluationAppointment,
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeOptions,
  ProcurementEvaluationCommitteeReadiness,
  ProcurementEvaluationMeeting,
  ProcurementEvaluationPhase,
  ProcurementEvaluationScoreRecall,
  ProcurementEvaluationScoreSheet,
} from '@/types/procurement-evaluation-committee';

export type EvaluationCommitteeDialogAction =
  | { type: 'bind' }
  | { type: 'activate' }
  | { type: 'retireDraft' }
  | {
      type: 'appointment';
      member: ProcurementEvaluationAppointment;
      accept: boolean;
    }
  | { type: 'coi'; member: ProcurementEvaluationAppointment }
  | { type: 'meeting' }
  | {
      type: 'attendance';
      meeting: ProcurementEvaluationMeeting;
      member: ProcurementEvaluationAppointment;
    }
  | { type: 'quorum'; meeting: ProcurementEvaluationMeeting }
  | { type: 'recall'; scoreSheet: ProcurementEvaluationScoreSheet }
  | {
      type: 'recall-decision';
      recall: ProcurementEvaluationScoreRecall;
      approve: boolean;
    };

const toLocalDateTime = (value?: string) => {
  const date = value ? new Date(value) : new Date();
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const toUtc = (value: string) => new Date(value).toISOString();

const defaultPurpose = (sourceReference: string) =>
  `Constitute and govern the evaluation committee for ${sourceReference}.`;

export function EvaluationCommitteeActionDialogs({
  action,
  onClose,
  onCompleted,
  readiness,
  control,
  options,
}: {
  action?: EvaluationCommitteeDialogAction;
  onClose: () => void;
  onCompleted: () => Promise<void>;
  readiness: ProcurementEvaluationCommitteeReadiness;
  control?: ProcurementEvaluationCommitteeControl;
  options?: ProcurementEvaluationCommitteeOptions;
}) {
  const [busy, setBusy] = useState(false);
  const [recallError, setRecallError] = useState<string>();
  const [committeeTemplateId, setCommitteeTemplateId] = useState('');
  const [purpose, setPurpose] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState(toLocalDateTime());
  const [effectiveTo, setEffectiveTo] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [signatureReference, setSignatureReference] = useState('');
  const [reason, setReason] = useState('');
  const [coiOutcome, setCoiOutcome] = useState<
    'NoConflict' | 'ConflictDeclared'
  >('NoConflict');
  const [declaration, setDeclaration] = useState('');
  const [conflictDetails, setConflictDetails] = useState('');
  const [validFrom, setValidFrom] = useState(toLocalDateTime());
  const [validTo, setValidTo] = useState('');
  const [phase, setPhase] = useState<ProcurementEvaluationPhase>('Technical');
  const [meetingMode, setMeetingMode] = useState('InPerson');
  const [meetingChannel, setMeetingChannel] = useState('');
  const [scheduledAt, setScheduledAt] = useState(toLocalDateTime());
  const [remoteEvidence, setRemoteEvidence] = useState('');
  const [workflowDefinitionId, setWorkflowDefinitionId] = useState('');
  const [decisionReference, setDecisionReference] = useState('');

  useEffect(() => {
    if (!action) return;
    setBusy(false);
    setRecallError(undefined);
    setEvidenceReference('');
    setSignatureReference('');
    setReason('');
    setConflictDetails('');
    setRemoteEvidence('');
    setDecisionReference('');
    setPurpose(defaultPurpose(readiness.sourceReference));
    setCommitteeTemplateId(
      options?.committees.find((item) => item.compositionReady)?.id ?? ''
    );
    setEffectiveFrom(toLocalDateTime());
    setEffectiveTo('');
    setCoiOutcome('NoConflict');
    setDeclaration(
      'I declare that the information provided is complete and accurate.'
    );
    setValidFrom(toLocalDateTime());
    setValidTo('');
    setPhase('Technical');
    setMeetingMode('InPerson');
    setMeetingChannel('');
    setScheduledAt(toLocalDateTime());
    setWorkflowDefinitionId('');
  }, [action, options, readiness.sourceReference, readiness.sourceType]);

  const selectedCommittee = options?.committees.find(
    (item) => item.id === committeeTemplateId
  );
  const bindRequest = useMemo<BindProcurementEvaluationCommitteeRequest>(
    () => ({
      sourceType: readiness.sourceType,
      sourceId: readiness.sourceId,
      committeeTemplateId,
      purpose,
      effectiveFromUtc: effectiveFrom ? toUtc(effectiveFrom) : effectiveFrom,
      effectiveToUtc: effectiveTo ? toUtc(effectiveTo) : undefined,
      requiredRoles: [],
      idempotencyKey: 'pending',
    }),
    [
      committeeTemplateId,
      effectiveFrom,
      effectiveTo,
      purpose,
      readiness.sourceId,
      readiness.sourceType,
    ]
  );

  const validation = useMemo(() => {
    if (!action) return undefined;
    if (action.type === 'bind') {
      const baseError = validateCommitteeBinding(bindRequest);
      if (baseError) return baseError;
      if (!selectedCommittee?.compositionReady)
        return (
          selectedCommittee?.issues[0] ??
          'Select a committee with a complete active composition.'
        );
    }
    if (action.type === 'appointment') {
      if (!action.accept && reason.trim().length < 5)
        return 'Provide the appointment-decline reason.';
    }
    if (action.type === 'retireDraft' && reason.trim().length < 10)
      return 'Provide a retirement reason of at least 10 characters.';
    if (action.type === 'coi') {
      return validateCoiDeclaration({
        outcome: coiOutcome,
        declaration,
        conflictDetails:
          coiOutcome === 'ConflictDeclared' ? conflictDetails : undefined,
        signatureReference,
        evidenceReference,
        validFromUtc: validFrom ? toUtc(validFrom) : validFrom,
        validToUtc: validTo ? toUtc(validTo) : undefined,
        appointmentRowVersion: action.member.rowVersion,
        idempotencyKey: 'pending',
      });
    }
    if (action.type === 'meeting') {
      if (!meetingChannel.trim())
        return 'Meeting channel or venue is required.';
      if (!scheduledAt || Number.isNaN(Date.parse(scheduledAt)))
        return 'Scheduled meeting time is invalid.';
      if (['Remote', 'Hybrid'].includes(meetingMode) && !remoteEvidence.trim())
        return 'Remote or hybrid meetings require a remote-session evidence reference.';
    }
    if (action.type === 'recall') {
      return validateRecallRequest({
        scoreSheetRowVersion: action.scoreSheet.rowVersion,
        reason,
        evidenceReference,
        workflowDefinitionId,
        idempotencyKey: 'pending',
      });
    }
    if (action.type === 'recall-decision') {
      if (!decisionReference.trim() || !evidenceReference.trim())
        return 'Decision and evidence references are required.';
    }
    return undefined;
  }, [
    action,
    bindRequest,
    coiOutcome,
    conflictDetails,
    control,
    declaration,
    decisionReference,
    evidenceReference,
    meetingChannel,
    meetingMode,
    reason,
    remoteEvidence,
    readiness.sourceId,
    readiness.sourceType,
    scheduledAt,
    selectedCommittee,
    signatureReference,
    validFrom,
    validTo,
    workflowDefinitionId,
  ]);

  const submit = async () => {
    if (!action || validation) return;
    if (action.type === 'recall' || action.type === 'recall-decision')
      setRecallError(undefined);
    setBusy(true);
    try {
      const key = createEvaluationIdempotencyKey(action.type);
      if (action.type === 'bind') {
        await service.bind({ ...bindRequest, idempotencyKey: key });
      } else if (action.type === 'activate' && control) {
        await service.activate(control.id, {
          rowVersion: control.rowVersion,
          evidenceReference: evidenceReference.trim() || undefined,
          idempotencyKey: key,
        });
      } else if (action.type === 'retireDraft' && control) {
        await service.retireDraft(control.id, {
          rowVersion: control.rowVersion,
          reason: reason.trim(),
          evidenceReference: evidenceReference.trim() || undefined,
          idempotencyKey: key,
        });
      } else if (action.type === 'appointment') {
        await service.respondToAppointment(action.member.id, {
          accept: action.accept,
          rowVersion: action.member.rowVersion,
          signatureReference: action.accept
            ? signatureReference.trim() || undefined
            : undefined,
          evidenceReference: evidenceReference.trim() || undefined,
          reason: action.accept ? undefined : reason,
          idempotencyKey: key,
        });
      } else if (action.type === 'coi') {
        await service.submitConflictDeclaration(action.member.id, {
          outcome: coiOutcome,
          declaration,
          conflictDetails:
            coiOutcome === 'ConflictDeclared' ? conflictDetails : undefined,
          signatureReference: signatureReference.trim() || undefined,
          evidenceReference: evidenceReference.trim() || undefined,
          validFromUtc: toUtc(validFrom),
          validToUtc: validTo ? toUtc(validTo) : undefined,
          appointmentRowVersion: action.member.rowVersion,
          idempotencyKey: key,
        });
      } else if (action.type === 'meeting' && control) {
        await service.createMeeting(control.id, {
          phase,
          meetingMode,
          meetingChannel,
          scheduledAtUtc: toUtc(scheduledAt),
          evidenceReference: evidenceReference.trim() || undefined,
          remoteMeetingEvidenceReference:
            meetingMode === 'InPerson' ? undefined : remoteEvidence,
          committeeRowVersion: control.rowVersion,
          idempotencyKey: key,
        });
      } else if (action.type === 'attendance') {
        await service.signAttendance(action.meeting.id, {
          isPresent: true,
          signatureReference: signatureReference.trim() || undefined,
          evidenceReference: evidenceReference.trim() || undefined,
          meetingRowVersion: action.meeting.rowVersion,
          appointmentRowVersion: action.member.rowVersion,
          idempotencyKey: key,
        });
      } else if (action.type === 'quorum') {
        await service.confirmQuorum(action.meeting.id, {
          rowVersion: action.meeting.rowVersion,
          evidenceReference: evidenceReference.trim() || undefined,
          remoteMeetingEvidenceReference: remoteEvidence || undefined,
          idempotencyKey: key,
        });
      } else if (action.type === 'recall') {
        await service.requestRecall(action.scoreSheet.id, {
          scoreSheetRowVersion: action.scoreSheet.rowVersion,
          reason,
          evidenceReference,
          workflowDefinitionId,
          idempotencyKey: key,
        });
      } else if (action.type === 'recall-decision') {
        await service.decideRecall(action.recall.id, {
          approve: action.approve,
          rowVersion: action.recall.rowVersion,
          decisionReference,
          evidenceReference,
          idempotencyKey: key,
        });
      }
      toast.success(successMessage(action));
      onClose();
      await onCompleted();
    } catch (error) {
      const message = getProcurementProblemMessage(
        error,
        'The controlled committee action failed.'
      );
      if (action.type === 'recall' || action.type === 'recall-decision')
        setRecallError(message);
      toast.error(message);
    } finally {
      setBusy(false);
    }
  };

  if (!action) return null;

  const metadata = dialogMetadata(action);
  return (
    <Dialog open onOpenChange={(open) => !open && !busy && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{metadata.title}</DialogTitle>
          <DialogDescription>{metadata.description}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {action.type === 'bind' && (
            <>
              <Field label="Active committee snapshot *">
                <Select
                  value={committeeTemplateId || 'none'}
                  onValueChange={(value) =>
                    setCommitteeTemplateId(value === 'none' ? '' : value)
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select committee" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Select committee</SelectItem>
                    {(options?.committees ?? []).map((committee) => (
                      <SelectItem
                        key={committee.id}
                        value={committee.id}
                        disabled={!committee.compositionReady}
                      >
                        {committee.code} · {committee.name} ·{' '}
                        {committee.activeMemberCount} members
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              {selectedCommittee?.issues.length ? (
                <Alert variant="destructive">
                  <AlertTitle>Committee template is not ready</AlertTitle>
                  <AlertDescription>
                    {selectedCommittee.issues.join(' · ')}
                  </AlertDescription>
                </Alert>
              ) : null}
              <Field label="Purpose *">
                <Textarea
                  rows={3}
                  value={purpose}
                  onChange={(event) => setPurpose(event.target.value)}
                />
              </Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Effective from *">
                  <Input
                    type="datetime-local"
                    value={effectiveFrom}
                    onChange={(event) => setEffectiveFrom(event.target.value)}
                  />
                </Field>
                <Field label="Effective to">
                  <Input
                    type="datetime-local"
                    value={effectiveTo}
                    onChange={(event) => setEffectiveTo(event.target.value)}
                  />
                </Field>
              </div>
              <Alert>
                <AlertTitle>Exact snapshot, shared administration</AlertTitle>
                <AlertDescription>
                  Membership is copied from the active committee managed in
                  Access &amp; Committees. Subsequent acceptance, declaration,
                  attendance, and scores are retained against this source and
                  version.
                </AlertDescription>
              </Alert>
            </>
          )}

          {action.type === 'activate' && (
            <EvidenceFields
              evidenceReference={evidenceReference}
              setEvidenceReference={setEvidenceReference}
              required={false}
            />
          )}

          {action.type === 'retireDraft' && (
            <>
              <Alert variant="destructive">
                <AlertTitle>Retire this unactivated draft?</AlertTitle>
                <AlertDescription>
                  The record and its audit history will remain available. This
                  action is rejected after activation or any appointment,
                  declaration, meeting, attendance, or scoring activity.
                </AlertDescription>
              </Alert>
              <Field label="Retirement reason *">
                <Textarea
                  rows={4}
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </Field>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
                required={false}
              />
            </>
          )}

          {action.type === 'appointment' && (
            <>
              <ReadOnly
                label="Appointment"
                value={`${action.member.userDisplayName} · ${action.member.memberKind}`}
              />
              {action.accept && (
                <Field label="External signature reference (optional)">
                  <Input
                    value={signatureReference}
                    onChange={(event) =>
                      setSignatureReference(event.target.value)
                    }
                  />
                </Field>
              )}
              {!action.accept && (
                <Field label="Decline reason *">
                  <Textarea
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </Field>
              )}
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
                required={false}
              />
            </>
          )}

          {action.type === 'coi' && (
            <>
              <Field label="Declaration outcome *">
                <Select
                  value={coiOutcome}
                  onValueChange={(value) =>
                    setCoiOutcome(value as typeof coiOutcome)
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="NoConflict">
                      No conflict declared
                    </SelectItem>
                    <SelectItem value="ConflictDeclared">
                      Conflict declared
                    </SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Signed declaration *">
                <Textarea
                  rows={4}
                  value={declaration}
                  onChange={(event) => setDeclaration(event.target.value)}
                />
              </Field>
              {coiOutcome === 'ConflictDeclared' && (
                <Field label="Conflict details *">
                  <Textarea
                    rows={3}
                    value={conflictDetails}
                    onChange={(event) => setConflictDetails(event.target.value)}
                  />
                </Field>
              )}
              <Field
                label={
                  coiOutcome === 'ConflictDeclared'
                    ? 'Signature reference *'
                    : 'External signature reference (optional)'
                }
              >
                <Input
                  value={signatureReference}
                  onChange={(event) =>
                    setSignatureReference(event.target.value)
                  }
                />
              </Field>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
                required={coiOutcome === 'ConflictDeclared'}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Valid from *">
                  <Input
                    type="datetime-local"
                    value={validFrom}
                    onChange={(event) => setValidFrom(event.target.value)}
                  />
                </Field>
                <Field label="Valid to">
                  <Input
                    type="datetime-local"
                    value={validTo}
                    onChange={(event) => setValidTo(event.target.value)}
                  />
                </Field>
              </div>
            </>
          )}

          {action.type === 'meeting' && (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Evaluation phase *">
                  <Select
                    value={phase}
                    onValueChange={(value) =>
                      setPhase(value as ProcurementEvaluationPhase)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Technical">Technical</SelectItem>
                      <SelectItem value="Financial">Financial</SelectItem>
                      <SelectItem value="Combined">Combined</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="Meeting mode *">
                  <Select value={meetingMode} onValueChange={setMeetingMode}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="InPerson">In person</SelectItem>
                      <SelectItem value="Remote">Remote</SelectItem>
                      <SelectItem value="Hybrid">Hybrid</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
              </div>
              <Field label="Venue / meeting channel *">
                <Input
                  value={meetingChannel}
                  onChange={(event) => setMeetingChannel(event.target.value)}
                  placeholder="Boardroom or controlled Teams/Zoom reference"
                />
              </Field>
              <Field label="Scheduled time *">
                <Input
                  type="datetime-local"
                  value={scheduledAt}
                  onChange={(event) => setScheduledAt(event.target.value)}
                />
              </Field>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
                required={false}
              />
              {meetingMode !== 'InPerson' && (
                <Field label="Remote-session evidence reference *">
                  <Input
                    value={remoteEvidence}
                    onChange={(event) => setRemoteEvidence(event.target.value)}
                    placeholder="Recording, attendance export, or secured meeting log"
                  />
                </Field>
              )}
            </>
          )}

          {action.type === 'attendance' && (
            <>
              <ReadOnly
                label="Member"
                value={`${action.member.userDisplayName} · ${action.member.memberKind}`}
              />
              <ReadOnly
                label="Meeting"
                value={`${action.meeting.phase} · ${action.meeting.meetingMode} · ${action.meeting.meetingChannel}`}
              />
              <Field label="External attendance signature reference (optional)">
                <Input
                  value={signatureReference}
                  onChange={(event) =>
                    setSignatureReference(event.target.value)
                  }
                />
              </Field>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
                required={false}
              />
            </>
          )}

          {action.type === 'quorum' && (
            <>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
                required={false}
              />
              {action.meeting.meetingMode !== 'InPerson' && (
                <Field label="Remote-session evidence reference">
                  <Input
                    value={remoteEvidence}
                    onChange={(event) => setRemoteEvidence(event.target.value)}
                  />
                </Field>
              )}
            </>
          )}

          {action.type === 'recall' && (
            <>
              <ReadOnly
                label="Locked attempt"
                value={`${action.scoreSheet.phase} · ${action.scoreSheet.scoreSubjectType} · attempt ${action.scoreSheet.attempt}`}
              />
              <Field label="Recall reason *">
                <Textarea
                  rows={4}
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </Field>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
              />
              <Field label="Independent recall workflow *">
                <Select
                  value={workflowDefinitionId || 'none'}
                  onValueChange={(value) =>
                    setWorkflowDefinitionId(value === 'none' ? '' : value)
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select workflow" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Select workflow</SelectItem>
                    {(options?.workflows ?? []).map((workflow) => (
                      <SelectItem key={workflow.id} value={workflow.id}>
                        {workflow.name} · v{workflow.version}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            </>
          )}

          {action.type === 'recall-decision' && (
            <>
              <ReadOnly label="Recall reason" value={action.recall.reason} />
              <Field label="Workflow decision reference *">
                <Input
                  value={decisionReference}
                  onChange={(event) => setDecisionReference(event.target.value)}
                />
              </Field>
              <EvidenceFields
                evidenceReference={evidenceReference}
                setEvidenceReference={setEvidenceReference}
              />
              <Alert variant={action.approve ? 'default' : 'destructive'}>
                <AlertTitle>
                  {action.approve ? 'Authorize a new attempt' : 'Reject recall'}
                </AlertTitle>
                <AlertDescription>
                  The locked score sheet remains immutable in both outcomes.
                  Approval recalls the prior attempt and authorizes exactly one
                  new attempt.
                </AlertDescription>
              </Alert>
            </>
          )}

          {validation && (
            <p className="text-sm text-destructive">{validation}</p>
          )}
          {recallError &&
            (action.type === 'recall' || action.type === 'recall-decision') && (
              <Alert variant="destructive">
                <AlertTitle>Recall action could not be confirmed</AlertTitle>
                <AlertDescription>{recallError}</AlertDescription>
              </Alert>
            )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button
            variant={
              action.type === 'retireDraft' ||
              (action.type === 'recall-decision' && !action.approve)
                ? 'destructive'
                : 'default'
            }
            onClick={() => void submit()}
            disabled={busy || Boolean(validation)}
          >
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {metadata.submit}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function dialogMetadata(action: EvaluationCommitteeDialogAction) {
  switch (action.type) {
    case 'bind':
      return {
        title: 'Constitute source-specific evaluation committee',
        description:
          'Bind an exact active committee composition to this tender or RFQ without changing the reusable master committee.',
        submit: 'Create immutable snapshot',
      };
    case 'activate':
      return {
        title: 'Activate committee composition',
        description:
          'Activation verifies the exact composition and required roles. Member acceptance, current COI declarations, signed attendance, and quorum are subsequent scorer-readiness gates.',
        submit: 'Activate committee',
      };
    case 'retireDraft':
      return {
        title: 'Retire unactivated committee draft',
        description:
          'Preserve the incorrect snapshot as retired so a corrected committee can be constituted for this source.',
        submit: 'Retire draft',
      };
    case 'appointment':
      return {
        title: action.accept ? 'Accept appointment' : 'Decline appointment',
        description:
          'Only the appointed member may respond. The signed response is retained in committee history.',
        submit: action.accept ? 'Accept appointment' : 'Decline appointment',
      };
    case 'coi':
      return {
        title: 'Submit conflict-of-interest declaration',
        description:
          'Every declaration is versioned and retained. Missing, conflicting, withdrawn, or expired declarations block scoring.',
        submit: 'Submit signed declaration',
      };
    case 'meeting':
      return {
        title: 'Schedule controlled evaluation meeting',
        description:
          'Remote and hybrid sessions require an auditable meeting reference and evidence.',
        submit: 'Create meeting',
      };
    case 'attendance':
      return {
        title: 'Sign meeting attendance',
        description:
          'The server records eligibility at signature time; attendance cannot grant scorer eligibility.',
        submit: 'Sign attendance',
      };
    case 'quorum':
      return {
        title: 'Confirm evaluation quorum',
        description: 'Confirm the recorded meeting attendance and quorum.',
        submit: 'Confirm Quorum',
      };
    case 'recall':
      return {
        title: 'Request controlled score recall',
        description:
          'Independent workflow approval is required. The submitted score sheet is never overwritten.',
        submit: 'Submit recall request',
      };
    case 'recall-decision':
      return {
        title: action.approve ? 'Approve controlled recall' : 'Reject recall',
        description:
          'The decision must match the independently completed shared workflow outcome.',
        submit: action.approve ? 'Approve recall' : 'Reject recall',
      };
  }
}

function successMessage(action: EvaluationCommitteeDialogAction) {
  switch (action.type) {
    case 'bind':
      return 'Evaluation committee snapshot created.';
    case 'activate':
      return 'Evaluation committee activated.';
    case 'retireDraft':
      return 'Draft committee retired. A corrected committee can now be constituted.';
    case 'appointment':
      return action.accept ? 'Appointment accepted.' : 'Appointment declined.';
    case 'coi':
      return 'Conflict-of-interest declaration retained.';
    case 'meeting':
      return 'Controlled evaluation meeting created.';
    case 'attendance':
      return 'Signed attendance retained.';
    case 'quorum':
      return 'Server quorum result retained.';
    case 'recall':
      return 'Controlled recall submitted for independent approval.';
    case 'recall-decision':
      return action.approve ? 'Recall approved.' : 'Recall rejected.';
  }
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}

function EvidenceFields({
  evidenceReference,
  setEvidenceReference,
  required = true,
}: {
  evidenceReference: string;
  setEvidenceReference: (value: string) => void;
  required?: boolean;
}) {
  return (
    <Field
      label={
        required
          ? 'Evidence reference *'
          : 'Supporting evidence reference (optional)'
      }
    >
      <Input
        value={evidenceReference}
        onChange={(event) => setEvidenceReference(event.target.value)}
        placeholder="Workflow evidence, signed file, minute, or controlled reference"
      />
    </Field>
  );
}

function ReadOnly({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border bg-muted/30 p-3">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 break-words text-sm">{value}</p>
    </div>
  );
}
