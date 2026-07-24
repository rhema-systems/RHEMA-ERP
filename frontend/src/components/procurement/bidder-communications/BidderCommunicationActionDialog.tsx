'use client';

import React from 'react';
import { useEffect, useState, type ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { createBidderCommunicationIdempotencyKey } from '@/lib/procurement-bidder-communication';
import { procurementBidderCommunicationService as service } from '@/services/procurement-bidder-communication.service';
import type { ProcurementAwardReadinessDecision } from '@/types/procurement-award-readiness';
import type {
  ProcurementBidderAppeal,
  ProcurementBidderCommunicationOverview,
  ProcurementBidderCommunicationRecipient,
  ProcurementBidderCommunicationSourceType,
  ProcurementBidderLetterDispatch,
  ProcurementBidderLetterVersion,
  ProcurementTenderSecurityActionType,
  ProcurementTenderSecurityInstrument,
} from '@/types/procurement-bidder-communication';

export type BidderCommunicationAction =
  | { type: 'initialize' }
  | {
      type: 'approve-letter';
      recipient: ProcurementBidderCommunicationRecipient;
    }
  | {
      type: 'dispatch';
      recipient: ProcurementBidderCommunicationRecipient;
      letter: ProcurementBidderLetterVersion;
    }
  | { type: 'delivery'; dispatch: ProcurementBidderLetterDispatch }
  | { type: 'acknowledge'; dispatch: ProcurementBidderLetterDispatch }
  | {
      type: 'file-appeal';
      recipient: ProcurementBidderCommunicationRecipient;
    }
  | { type: 'resolve-appeal'; appeal: ProcurementBidderAppeal }
  | {
      type: 'register-security';
      recipient: ProcurementBidderCommunicationRecipient;
    }
  | {
      type: 'security-action';
      security: ProcurementTenderSecurityInstrument;
      securityAction: ProcurementTenderSecurityActionType;
    };

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';

export function BidderCommunicationActionDialog({
  sourceType,
  sourceId,
  overview,
  awardReadiness,
  action,
  external,
  onOpenChange,
  onChanged,
}: {
  sourceType: ProcurementBidderCommunicationSourceType;
  sourceId: string;
  overview?: ProcurementBidderCommunicationOverview;
  awardReadiness?: ProcurementAwardReadinessDecision;
  action?: BidderCommunicationAction;
  external: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged: () => Promise<void>;
}) {
  const [reference, setReference] = useState('');
  const [secondaryReference, setSecondaryReference] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [reason, setReason] = useState('');
  const [channel, setChannel] = useState('SupplierPortal');
  const [outcome, setOutcome] = useState('');
  const [occurredAtUtc, setOccurredAtUtc] = useState('');
  const [standstillEndsAtUtc, setStandstillEndsAtUtc] = useState('');
  const [appealWindowEndsAtUtc, setAppealWindowEndsAtUtc] = useState('');
  const [templateVersionId, setTemplateVersionId] = useState('');
  const [checksum, setChecksum] = useState('');
  const [workflowInstanceId, setWorkflowInstanceId] = useState('');
  const [instrumentType, setInstrumentType] = useState('BidBond');
  const [issuer, setIssuer] = useState('');
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('');
  const [issuedAtUtc, setIssuedAtUtc] = useState('');
  const [expiresAtUtc, setExpiresAtUtc] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    setReference('');
    setSecondaryReference('');
    setEvidenceReference('');
    setReason('');
    setChannel('SupplierPortal');
    setOutcome('');
    setOccurredAtUtc('');
    setStandstillEndsAtUtc('');
    setAppealWindowEndsAtUtc('');
    setTemplateVersionId('');
    setChecksum('');
    setWorkflowInstanceId('');
    setInstrumentType('BidBond');
    setIssuer('');
    setAmount('');
    setCurrency('');
    setIssuedAtUtc('');
    setExpiresAtUtc('');
  }, [action]);

  const metadata = actionMetadata(action);
  const validation = validate({
    action,
    overview,
    awardReadiness,
    reference,
    secondaryReference,
    evidenceReference,
    reason,
    outcome,
    occurredAtUtc,
    standstillEndsAtUtc,
    appealWindowEndsAtUtc,
    templateVersionId,
    checksum,
    workflowInstanceId,
    instrumentType,
    issuer,
    amount,
    currency,
    issuedAtUtc,
    expiresAtUtc,
  });

  const submit = async () => {
    if (!action || validation) return;
    setSubmitting(true);
    try {
      const idempotencyKey = createBidderCommunicationIdempotencyKey(
        action.type
      );
      if (action.type === 'initialize' && awardReadiness) {
        await service.initialize(sourceType, sourceId, {
          sourceType,
          sourceId,
          standstillEndsAtUtc: new Date(standstillEndsAtUtc).toISOString(),
          appealWindowEndsAtUtc: new Date(
            appealWindowEndsAtUtc
          ).toISOString(),
          standstillAuthorityReference: reference,
          expectedAwardReadinessDecisionId: awardReadiness.id,
          expectedAwardReadinessIntegrityHash: awardReadiness.integrityHash,
          idempotencyKey,
        });
      } else if (action.type === 'approve-letter' && overview) {
        await service.approveLetter(
          sourceType,
          sourceId,
          action.recipient.id,
          {
            templateVersionId,
            contentReference: reference,
            contentChecksumSha256: checksum,
            workflowInstanceId,
            approvalReference: secondaryReference,
            approvalEvidenceReference: evidenceReference,
            expectedRegisterIntegrityHash: overview.integrityHash,
            idempotencyKey,
          }
        );
      } else if (action.type === 'dispatch') {
        await service.dispatchLetter(
          sourceType,
          sourceId,
          action.letter.id,
          {
            channel: channel as
              | 'Email'
              | 'Sms'
              | 'SupplierPortal'
              | 'PhysicalDelivery'
              | 'Other',
            destination: secondaryReference,
            dispatchReference: reference,
            dispatchEvidenceReference: evidenceReference,
            idempotencyKey,
          }
        );
      } else if (action.type === 'delivery') {
        await service.recordDelivery(
          sourceType,
          sourceId,
          action.dispatch.id,
          {
            outcome: outcome as 'Sent' | 'Delivered' | 'Failed' | 'Returned',
            occurredAtUtc: new Date(occurredAtUtc).toISOString(),
            providerReference: reference,
            detail: reason || undefined,
            evidenceReference,
            idempotencyKey,
          }
        );
      } else if (action.type === 'acknowledge') {
        await service.acknowledge(
          sourceType,
          sourceId,
          action.dispatch.id,
          {
            outcome: outcome as 'Received' | 'Accepted' | 'Disputed',
            acknowledgementChannel: secondaryReference,
            acknowledgementReference: reference,
            evidenceReference,
            idempotencyKey,
          },
          external
        );
      } else if (action.type === 'file-appeal') {
        await service.fileAppeal(
          sourceType,
          sourceId,
          action.recipient.id,
          {
            grounds: reason,
            evidenceReference,
            idempotencyKey,
          },
          external
        );
      } else if (action.type === 'resolve-appeal') {
        await service.resolveAppeal(
          sourceType,
          sourceId,
          action.appeal.id,
          {
            outcome: outcome as 'Upheld' | 'Dismissed' | 'Withdrawn',
            reason,
            workflowInstanceId,
            decisionReference: reference,
            evidenceReference,
            idempotencyKey,
          }
        );
      } else if (action.type === 'register-security') {
        const lineageId = action.recipient.bidOrQuoteIds[0];
        await service.registerSecurity(
          sourceType,
          sourceId,
          action.recipient.id,
          {
            tenderBidId:
              sourceType === 'RequestForQuotation' ? undefined : lineageId,
            requestForQuotationQuoteId:
              sourceType === 'RequestForQuotation' ? lineageId : undefined,
            instrumentType: instrumentType as
              | 'BidBond'
              | 'BankGuarantee'
              | 'InsuranceBond'
              | 'CashDeposit'
              | 'Other',
            instrumentReference: reference,
            issuerName: issuer,
            amount: Number(amount),
            currencyCode: currency.toUpperCase(),
            issuedAtUtc: new Date(issuedAtUtc).toISOString(),
            expiresAtUtc: new Date(expiresAtUtc).toISOString(),
            evidenceReference,
            idempotencyKey,
          }
        );
      } else if (action.type === 'security-action') {
        await service.actOnSecurity(
          sourceType,
          sourceId,
          action.security.id,
          {
            actionType: action.securityAction,
            workflowInstanceId,
            actionReference: reference,
            reason,
            evidenceReference,
            expectedLatestActionIntegrityHash:
              action.security.actions.at(-1)?.integrityHash,
            idempotencyKey,
          }
        );
      }
      toast.success(metadata.success);
      onOpenChange(false);
      await onChanged();
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={Boolean(action)} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{metadata.title}</DialogTitle>
          <DialogDescription>{metadata.description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          {action?.type === 'initialize' && (
            <>
              <ReferenceField
                label="Standstill authority reference *"
                value={reference}
                onChange={setReference}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <DateField
                  label="Standstill ends *"
                  value={standstillEndsAtUtc}
                  onChange={setStandstillEndsAtUtc}
                />
                <DateField
                  label="Appeal window ends *"
                  value={appealWindowEndsAtUtc}
                  onChange={setAppealWindowEndsAtUtc}
                />
              </div>
              <p className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
                Recipients come only from award-readiness decision #
                {awardReadiness?.decisionSequence ?? '—'} and integrity hash{' '}
                {awardReadiness?.integrityHash ?? '—'}.
              </p>
            </>
          )}

          {action?.type === 'approve-letter' && (
            <>
              <ReferenceField
                label="Approved template version ID *"
                value={templateVersionId}
                onChange={setTemplateVersionId}
              />
              <ReferenceField
                label="Approved content reference *"
                value={reference}
                onChange={setReference}
              />
              <ReferenceField
                label="Content SHA-256 checksum *"
                value={checksum}
                onChange={setChecksum}
              />
              <ReferenceField
                label="Completed workflow instance ID *"
                value={workflowInstanceId}
                onChange={setWorkflowInstanceId}
              />
              <ReferenceField
                label="Approval reference *"
                value={secondaryReference}
                onChange={setSecondaryReference}
              />
            </>
          )}

          {action?.type === 'dispatch' && (
            <>
              <Field label="Dispatch channel *">
                <Select value={channel} onValueChange={setChannel}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {[
                      'Email',
                      'Sms',
                      'SupplierPortal',
                      'PhysicalDelivery',
                      'Other',
                    ].map((value) => (
                      <SelectItem key={value} value={value}>
                        {display(value)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <ReferenceField
                label="Destination *"
                value={secondaryReference}
                onChange={setSecondaryReference}
              />
              <ReferenceField
                label="Dispatch reference *"
                value={reference}
                onChange={setReference}
              />
            </>
          )}

          {action?.type === 'delivery' && (
            <>
              <OutcomeField
                value={outcome}
                onChange={setOutcome}
                values={['Sent', 'Delivered', 'Failed', 'Returned']}
              />
              <DateField
                label="Occurred at *"
                value={occurredAtUtc}
                onChange={setOccurredAtUtc}
              />
              <ReferenceField
                label="Provider reference *"
                value={reference}
                onChange={setReference}
              />
              <TextField
                label="Provider detail"
                value={reason}
                onChange={setReason}
              />
            </>
          )}

          {action?.type === 'acknowledge' && (
            <>
              <OutcomeField
                value={outcome}
                onChange={setOutcome}
                values={['Received', 'Accepted', 'Disputed']}
              />
              <ReferenceField
                label="Acknowledgement channel *"
                value={secondaryReference}
                onChange={setSecondaryReference}
              />
              <ReferenceField
                label="Acknowledgement reference *"
                value={reference}
                onChange={setReference}
              />
            </>
          )}

          {action?.type === 'file-appeal' && (
            <TextField
              label="Appeal grounds *"
              value={reason}
              onChange={setReason}
            />
          )}

          {action?.type === 'resolve-appeal' && (
            <>
              <OutcomeField
                value={outcome}
                onChange={setOutcome}
                values={['Upheld', 'Dismissed', 'Withdrawn']}
              />
              <ReferenceField
                label="Completed workflow instance ID *"
                value={workflowInstanceId}
                onChange={setWorkflowInstanceId}
              />
              <ReferenceField
                label="Decision reference *"
                value={reference}
                onChange={setReference}
              />
              <TextField
                label="Decision reason *"
                value={reason}
                onChange={setReason}
              />
            </>
          )}

          {action?.type === 'register-security' && (
            <>
              <Field label="Instrument type *">
                <Select
                  value={instrumentType}
                  onValueChange={setInstrumentType}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {[
                      'BidBond',
                      'BankGuarantee',
                      'InsuranceBond',
                      'CashDeposit',
                      'Other',
                    ].map((value) => (
                      <SelectItem key={value} value={value}>
                        {display(value)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <ReferenceField
                label="Instrument reference *"
                value={reference}
                onChange={setReference}
              />
              <ReferenceField
                label="Issuer name *"
                value={issuer}
                onChange={setIssuer}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <ReferenceField
                  label="Amount *"
                  value={amount}
                  onChange={setAmount}
                  type="number"
                />
                <ReferenceField
                  label="Currency code *"
                  value={currency}
                  onChange={setCurrency}
                />
                <DateField
                  label="Issued at *"
                  value={issuedAtUtc}
                  onChange={setIssuedAtUtc}
                />
                <DateField
                  label="Expires at *"
                  value={expiresAtUtc}
                  onChange={setExpiresAtUtc}
                />
              </div>
            </>
          )}

          {action?.type === 'security-action' && (
            <>
              <ReferenceField
                label="Completed workflow instance ID *"
                value={workflowInstanceId}
                onChange={setWorkflowInstanceId}
              />
              <ReferenceField
                label={`${display(action.securityAction)} reference *`}
                value={reference}
                onChange={setReference}
              />
              <TextField
                label="Action reason *"
                value={reason}
                onChange={setReason}
              />
            </>
          )}

          {action && action.type !== 'initialize' && (
            <ReferenceField
              label="Evidence reference *"
              value={evidenceReference}
              onChange={setEvidenceReference}
            />
          )}

          {validation && (
            <p className="text-sm text-destructive">{validation}</p>
          )}
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button
            type="button"
            onClick={() => void submit()}
            disabled={Boolean(validation) || submitting}
          >
            {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {metadata.submit}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function actionMetadata(action?: BidderCommunicationAction) {
  if (!action)
    return {
      title: 'Bidder communication action',
      description: 'Complete the controlled action.',
      submit: 'Submit',
      success: 'Action completed.',
    };
  const values = {
    initialize: [
      'Initialize bidder communication register',
      'Assert statutory dates and the exact current award-readiness decision; recipients remain server-derived.',
      'Initialize register',
      'Bidder communication register initialized.',
    ],
    'approve-letter': [
      `Approve ${action.type === 'approve-letter' ? action.recipient.outcome.toLowerCase() : ''} letter`,
      'Retain a new immutable approved version from an exact controlled template and completed workflow.',
      'Approve immutable version',
      'Approved letter version retained.',
    ],
    dispatch: [
      'Dispatch approved letter',
      'Dispatch this exact approved version and retain destination, channel, reference, actor, time, and evidence.',
      'Record dispatch',
      'Letter dispatch retained.',
    ],
    delivery: [
      'Record delivery outcome',
      'Append the provider result to the exact immutable dispatch.',
      'Record delivery',
      'Delivery event retained.',
    ],
    acknowledge: [
      'Acknowledge communication',
      'Acknowledge the exact supplier-visible dispatch.',
      'Submit acknowledgement',
      'Acknowledgement retained.',
    ],
    'file-appeal': [
      'File appeal',
      'File supplier grounds and evidence within the server-controlled appeal window.',
      'File appeal',
      'Appeal retained.',
    ],
    'resolve-appeal': [
      'Resolve appeal',
      'Retain an authorized shared-workflow decision without changing the filed appeal.',
      'Resolve appeal',
      'Appeal decision retained.',
    ],
    'register-security': [
      'Register tender security',
      'Bind an evidence-backed instrument to the exact server-derived bid or quote lineage.',
      'Register security',
      'Tender security registered.',
    ],
    'security-action': [
      `${action.type === 'security-action' ? display(action.securityAction) : ''} tender security`,
      'The server rechecks standstill, appeals, award outcome, workflow, and prior action integrity.',
      action.type === 'security-action'
        ? `${display(action.securityAction)} security`
        : 'Submit',
      'Tender security action retained.',
    ],
  }[action.type];
  return {
    title: values[0],
    description: values[1],
    submit: values[2],
    success: values[3],
  };
}

function validate(input: {
  action?: BidderCommunicationAction;
  overview?: ProcurementBidderCommunicationOverview;
  awardReadiness?: ProcurementAwardReadinessDecision;
  reference: string;
  secondaryReference: string;
  evidenceReference: string;
  reason: string;
  outcome: string;
  occurredAtUtc: string;
  standstillEndsAtUtc: string;
  appealWindowEndsAtUtc: string;
  templateVersionId: string;
  checksum: string;
  workflowInstanceId: string;
  instrumentType: string;
  issuer: string;
  amount: string;
  currency: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
}) {
  const { action } = input;
  if (!action) return undefined;
  if (
    action.type !== 'initialize' &&
    !input.evidenceReference.trim()
  )
    return 'Evidence reference is required.';
  if (action.type === 'initialize') {
    if (!input.awardReadiness)
      return 'A current award-readiness decision is required.';
    if (!input.reference.trim())
      return 'Standstill authority reference is required.';
    if (!input.standstillEndsAtUtc || !input.appealWindowEndsAtUtc)
      return 'Standstill and appeal-window end dates are required.';
  }
  if (action.type === 'approve-letter') {
    if (!input.overview) return 'The current register is required.';
    if (!input.templateVersionId.trim())
      return 'Approved template version ID is required.';
    if (!input.reference.trim()) return 'Content reference is required.';
    if (!/^[a-f0-9]{64}$/i.test(input.checksum))
      return 'A 64-character SHA-256 content checksum is required.';
    if (!input.workflowInstanceId.trim())
      return 'Completed workflow instance ID is required.';
    if (!input.secondaryReference.trim())
      return 'Approval reference is required.';
  }
  if (action.type === 'dispatch') {
    if (!input.secondaryReference.trim()) return 'Destination is required.';
    if (!input.reference.trim()) return 'Dispatch reference is required.';
  }
  if (action.type === 'delivery') {
    if (!input.outcome) return 'Delivery outcome is required.';
    if (!input.occurredAtUtc) return 'Delivery time is required.';
    if (!input.reference.trim()) return 'Provider reference is required.';
  }
  if (action.type === 'acknowledge') {
    if (!input.outcome) return 'Acknowledgement outcome is required.';
    if (!input.secondaryReference.trim())
      return 'Acknowledgement channel is required.';
    if (!input.reference.trim())
      return 'Acknowledgement reference is required.';
  }
  if (action.type === 'file-appeal' && input.reason.trim().length < 5)
    return 'Appeal grounds must contain at least five characters.';
  if (action.type === 'resolve-appeal') {
    if (!input.outcome) return 'Appeal outcome is required.';
    if (!input.workflowInstanceId.trim())
      return 'Completed workflow instance ID is required.';
    if (!input.reference.trim()) return 'Decision reference is required.';
    if (input.reason.trim().length < 5)
      return 'Decision reason must contain at least five characters.';
  }
  if (action.type === 'register-security') {
    if (action.recipient.bidOrQuoteIds.length === 0)
      return 'Exact bid or quote lineage is required.';
    if (!input.instrumentType) return 'Instrument type is required.';
    if (!input.reference.trim()) return 'Instrument reference is required.';
    if (!input.issuer.trim()) return 'Issuer name is required.';
    if (!input.amount || Number(input.amount) <= 0)
      return 'A positive amount is required.';
    if (input.currency.trim().length !== 3)
      return 'A three-character currency code is required.';
    if (!input.issuedAtUtc || !input.expiresAtUtc)
      return 'Issue and expiry dates are required.';
  }
  if (action.type === 'security-action') {
    if (!input.workflowInstanceId.trim())
      return 'Completed workflow instance ID is required.';
    if (!input.reference.trim()) return 'Action reference is required.';
    if (input.reason.trim().length < 5)
      return 'Action reason must contain at least five characters.';
  }
  return undefined;
}

function display(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="grid gap-1.5 text-sm">
      <span className="font-medium">{label}</span>
      {children}
    </label>
  );
}

function ReferenceField({
  label,
  value,
  onChange,
  type = 'text',
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'number';
}) {
  return (
    <Field label={label}>
      <Input
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </Field>
  );
}

function DateField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <Field label={label}>
      <Input
        type="datetime-local"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </Field>
  );
}

function TextField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <Field label={label}>
      <Textarea
        value={value}
        onChange={(event) => onChange(event.target.value)}
        rows={4}
      />
    </Field>
  );
}

function OutcomeField({
  value,
  onChange,
  values,
}: {
  value: string;
  onChange: (value: string) => void;
  values: string[];
}) {
  return (
    <Field label="Outcome *">
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select outcome" />
        </SelectTrigger>
        <SelectContent>
          {values.map((item) => (
            <SelectItem key={item} value={item}>
              {display(item)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}
