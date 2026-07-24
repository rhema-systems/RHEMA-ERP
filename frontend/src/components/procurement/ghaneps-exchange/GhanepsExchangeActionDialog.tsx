'use client';

import React, {
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
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
import {
  createGhanepsIdempotencyKey,
  ghanepsContentTypeMetadata,
  readGhanepsContentFile,
  validateGhanepsContent,
} from '@/lib/procurement-ghaneps-exchange';
import { procurementGhanepsExchangeService as service } from '@/services/procurement-ghaneps-exchange.service';
import type {
  ProcurementGhanepsAttemptOutcome,
  ProcurementGhanepsContentType,
  ProcurementGhanepsExchangeAcknowledgement,
  ProcurementGhanepsExchangeAttempt,
  ProcurementGhanepsExchangeEvent,
  ProcurementGhanepsExchangeMappingOption,
  ProcurementGhanepsExchangePayload,
  ProcurementGhanepsSourceType,
} from '@/types/procurement-ghaneps-exchange';

export type GhanepsExchangeAction =
  | {
      type: 'prepare-export';
      mapping: ProcurementGhanepsExchangeMappingOption;
    }
  | {
      type: 'record-import';
      mapping: ProcurementGhanepsExchangeMappingOption;
    }
  | {
      type: 'record-attempt';
      event: ProcurementGhanepsExchangeEvent;
      payload: ProcurementGhanepsExchangePayload;
    }
  | {
      type: 'retry';
      event: ProcurementGhanepsExchangeEvent;
      payload: ProcurementGhanepsExchangePayload;
      priorAttempt: ProcurementGhanepsExchangeAttempt;
      rejectedAcknowledgement?: ProcurementGhanepsExchangeAcknowledgement;
      requiresCorrectedPayload: boolean;
    }
  | {
      type: 'acknowledge';
      event: ProcurementGhanepsExchangeEvent;
    }
  | {
      type: 'reconcile';
      event: ProcurementGhanepsExchangeEvent;
      resolveExistingMismatch: boolean;
    };

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';

export function GhanepsExchangeActionDialog({
  sourceType,
  sourceId,
  sourceReference,
  action,
  onOpenChange,
  onChanged,
}: {
  sourceType: ProcurementGhanepsSourceType;
  sourceId: string;
  sourceReference: string;
  action?: GhanepsExchangeAction;
  onOpenChange: (open: boolean) => void;
  onChanged: () => Promise<void>;
}) {
  const [reference, setReference] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [payloadContent, setPayloadContent] = useState('');
  const [fileName, setFileName] = useState('');
  const [checksum, setChecksum] = useState('');
  const [transportReference, setTransportReference] = useState('');
  const [externalStatusCode, setExternalStatusCode] = useState('');
  const [outcome, setOutcome] = useState('');
  const [failureCode, setFailureCode] = useState('');
  const [reason, setReason] = useState('');
  const [fileReadError, setFileReadError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const idempotencyKeyRef = useRef('');
  const actionType = action?.type;
  const actionIdentity = action
    ? 'event' in action
      ? `${action.type}:${action.event.id}`
      : `${action.type}:${action.mapping.mappingKey}`
    : '';
  const isSourcePayloadAction =
    actionType === 'prepare-export' || actionType === 'record-import';
  const configuredContentType =
    action?.type === 'prepare-export' || action?.type === 'record-import'
      ? action.mapping.payloadContentType
      : action?.type === 'retry'
        ? action.event.payloadContentType
        : action?.type === 'acknowledge'
          ? action.event.acknowledgementContentType
          : undefined;

  useEffect(() => {
    idempotencyKeyRef.current = actionType
      ? createGhanepsIdempotencyKey(actionType)
      : '';
    setReference(
      isSourcePayloadAction ? sourceReference : ''
    );
    setEvidenceReference('');
    setPayloadContent('');
    setFileName('');
    setChecksum('');
    setTransportReference('');
    setExternalStatusCode('');
    setOutcome('');
    setFailureCode('');
    setReason('');
    setFileReadError('');
  }, [
    actionIdentity,
    actionType,
    isSourcePayloadAction,
    sourceReference,
  ]);

  const updatePayloadContent = (value: string) => {
    setPayloadContent(value);
    setFileReadError('');
  };

  const loadContentFile = async (file?: File) => {
    if (!file || !configuredContentType) return;
    setFileReadError('');
    try {
      const content = await readGhanepsContentFile(
        file,
        configuredContentType
      );
      setPayloadContent(content);
      setFileName(file.name);
      setChecksum('');
    } catch (error) {
      setFileReadError(errorMessage(error));
    }
  };

  const metadata = actionMetadata(action);
  const validation =
    fileReadError ||
    validate({
      action,
      reference,
      evidenceReference,
      payloadContent,
      configuredContentType,
      checksum,
      transportReference,
      outcome,
      failureCode,
      reason,
    });

  const submit = async () => {
    if (!action || validation) return;
    setSubmitting(true);
    try {
      const idempotencyKey =
        idempotencyKeyRef.current ||
        createGhanepsIdempotencyKey(action.type);
      idempotencyKeyRef.current = idempotencyKey;
      if (
        action.type === 'prepare-export' ||
        action.type === 'record-import'
      ) {
        const request = {
          sourceType,
          sourceId,
          eventFamily: action.mapping.eventFamily,
          mappingKey: action.mapping.mappingKey,
          eventReference: sourceReference,
          payloadContent,
          fileName: fileName || undefined,
          expectedPayloadChecksumSha256: checksum || undefined,
          evidenceReference: evidenceReference || undefined,
          idempotencyKey,
        };
        if (action.type === 'prepare-export')
          await service.prepareExport(sourceType, sourceId, request);
        else
          await service.recordImport(sourceType, sourceId, {
            ...request,
            transportReference,
          });
      } else if (action.type === 'record-attempt') {
        await service.recordAttempt(
          sourceType,
          sourceId,
          action.event.id,
          {
            payloadId: action.payload.id,
            outcome: outcome as ProcurementGhanepsAttemptOutcome,
            transportReference: transportReference || undefined,
            failureCode:
              outcome === 'Failed' ? failureCode || undefined : undefined,
            failureMessage:
              outcome === 'Failed' ? reason || undefined : undefined,
            evidenceReference: evidenceReference || undefined,
            idempotencyKey,
            expectedRowVersion: action.event.rowVersion,
          }
        );
      } else if (action.type === 'retry') {
        await service.retry(sourceType, sourceId, action.event.id, {
          payloadId: action.payload.id,
          outcome: outcome as ProcurementGhanepsAttemptOutcome,
          transportReference: transportReference || undefined,
          failureCode:
            outcome === 'Failed' ? failureCode || undefined : undefined,
          failureMessage:
            outcome === 'Failed' ? reason || undefined : undefined,
          replacementPayloadContent:
            payloadContent.trim() ? payloadContent : undefined,
          fileName: fileName || undefined,
          expectedPayloadChecksumSha256: checksum || undefined,
          evidenceReference: evidenceReference || undefined,
          idempotencyKey,
          expectedRowVersion: action.event.rowVersion,
        });
      } else if (action.type === 'acknowledge') {
        await service.recordAcknowledgement(
          sourceType,
          sourceId,
          action.event.id,
          {
            outcome: outcome as 'Accepted' | 'Rejected',
            acknowledgementReference: reference,
            externalStatusCode: externalStatusCode || undefined,
            acknowledgementContent: payloadContent,
            expectedAcknowledgementChecksumSha256:
              checksum || undefined,
            evidenceReference,
            idempotencyKey,
            expectedRowVersion: action.event.rowVersion,
          }
        );
      } else {
        await service.reconcile(
          sourceType,
          sourceId,
          action.event.id,
          {
            resolveExistingMismatch: action.resolveExistingMismatch,
            actualReference: reference,
            actualChecksumSha256: checksum,
            notes: reason || undefined,
            evidenceReference,
            idempotencyKey,
            expectedRowVersion: action.event.rowVersion,
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

  const mapping =
    action?.type === 'prepare-export' || action?.type === 'record-import'
      ? action.mapping
      : undefined;
  const event =
    action && 'event' in action ? action.event : undefined;
  const acknowledgementAttempt =
    action?.type === 'acknowledge'
      ? action.event.attempts
          .filter((attempt) => attempt.outcome === 'Succeeded')
          .at(-1)
      : undefined;

  return (
    <Dialog open={Boolean(action)} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{metadata.title}</DialogTitle>
          <DialogDescription>{metadata.description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          {mapping && (
            <>
              <ReadOnlyLine
                label="Effective DEC-009 mapping"
                value={`${mapping.mappingKey} · ${mapping.eventFamily} · ${mapping.direction}`}
              />
              <ReadOnlyLine
                label="External contract"
                value={`${mapping.externalEventCode} · ${mapping.templateReference} · ${mapping.schemaReference} · ${mapping.payloadVersion}`}
              />
              <ReadOnlyLine
                label="Configured representations"
                value={`Payload ${mapping.payloadContentType} · acknowledgement ${mapping.acknowledgementContentType}`}
              />
            </>
          )}
          {event && (
            <>
              <ReadOnlyLine
                label="Exact exchange event"
                value={`${event.eventFamily} · ${event.eventReference} · ${event.status}`}
              />
              <ReadOnlyHash
                label="Event / source integrity"
                value={`${event.integrityHash} / ${event.sourceIntegrityHash}`}
              />
            </>
          )}

          {(action?.type === 'prepare-export' ||
            action?.type === 'record-import') && (
            <>
              <ReadOnlyLine
                label={`${mapping?.referenceField ?? 'Event'} (exact source reference)`}
                value={sourceReference}
              />
              {action.type === 'record-import' && (
                <Field label="Transport reference *">
                  <Input
                    value={transportReference}
                    onChange={(event) =>
                      setTransportReference(event.target.value)
                    }
                  />
                </Field>
              )}
              <ContentFields
                content={payloadContent}
                setContent={updatePayloadContent}
                contentType={action.mapping.payloadContentType}
                subject="Payload"
                required
                fileName={fileName}
                setFileName={setFileName}
                checksum={checksum}
                setChecksum={setChecksum}
                onLoadFile={loadContentFile}
                fileReadError={fileReadError}
              />
            </>
          )}

          {(action?.type === 'record-attempt' ||
            action?.type === 'retry') && (
            <>
              <ReadOnlyLine
                label="Payload version"
                value={`v${action.payload.version} · ${action.payload.direction} · ${action.payload.payloadChecksumSha256}`}
              />
              {action.type === 'retry' && (
                <ReadOnlyLine
                  label={
                    action.requiresCorrectedPayload
                      ? 'Rejected acknowledgement retry lineage'
                      : 'Failed attempt retained'
                  }
                  value={
                    action.requiresCorrectedPayload
                      ? `Attempt #${action.priorAttempt.attemptNumber} · rejected acknowledgement ${action.rejectedAcknowledgement?.acknowledgementReference ?? '—'}`
                      : `Attempt #${action.priorAttempt.attemptNumber} · ${action.priorAttempt.failureCode ?? 'Failed'} · ${action.priorAttempt.failureMessage ?? 'No detail'}`
                  }
                />
              )}
              <AttemptFields
                outcome={outcome}
                setOutcome={setOutcome}
                transportReference={transportReference}
                setTransportReference={setTransportReference}
                failureCode={failureCode}
                setFailureCode={setFailureCode}
                reason={reason}
                setReason={setReason}
              />
              {action.type === 'retry' && (
                <div className="rounded-md border p-3">
                  <p className="mb-3 text-sm font-medium">
                    {action.requiresCorrectedPayload
                      ? 'Corrected replacement payload *'
                      : 'Optional replacement payload'}
                  </p>
                  <ContentFields
                    content={payloadContent}
                    setContent={updatePayloadContent}
                    contentType={action.event.payloadContentType}
                    subject="Replacement payload"
                    required={action.requiresCorrectedPayload}
                    fileName={fileName}
                    setFileName={setFileName}
                    checksum={checksum}
                    setChecksum={setChecksum}
                    onLoadFile={loadContentFile}
                    fileReadError={fileReadError}
                  />
                </div>
              )}
            </>
          )}

          {action?.type === 'acknowledge' && (
            <>
              <ReadOnlyLine
                label="Latest successful attempt / payload"
                value={
                  acknowledgementAttempt
                    ? `Attempt #${acknowledgementAttempt.attemptNumber} · ${acknowledgementAttempt.id} / ${acknowledgementAttempt.payloadId}`
                    : 'No successful attempt is available'
                }
              />
              <OutcomeField
                label="Acknowledgement outcome *"
                value={outcome}
                onChange={setOutcome}
                values={['Accepted', 'Rejected']}
              />
              <Field label="Acknowledgement reference *">
                <Input
                  value={reference}
                  onChange={(event) => setReference(event.target.value)}
                />
              </Field>
              <Field label="External status code">
                <Input
                  value={externalStatusCode}
                  onChange={(event) =>
                    setExternalStatusCode(event.target.value)
                  }
                />
              </Field>
              <ContentFields
                content={payloadContent}
                setContent={updatePayloadContent}
                contentType={action.event.acknowledgementContentType}
                subject="Acknowledgement"
                required
                checksum={checksum}
                setChecksum={setChecksum}
                onLoadFile={loadContentFile}
                fileReadError={fileReadError}
              />
            </>
          )}

          {action?.type === 'reconcile' && (
            <>
              {action.resolveExistingMismatch && (
                <ReadOnlyLine
                  label="Reconciliation transition"
                  value="Resolve the latest retained mismatch"
                />
              )}
              <Field label="Actual external reference *">
                <Input
                  value={reference}
                  onChange={(event) => setReference(event.target.value)}
                />
              </Field>
              <Field label="Actual payload SHA-256 *">
                <Input
                  value={checksum}
                  onChange={(event) => setChecksum(event.target.value)}
                  maxLength={64}
                />
              </Field>
              <Field label="Reconciliation notes">
                <Textarea
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  rows={4}
                />
              </Field>
            </>
          )}

          {action && (
            <Field
              label={`Shared evidence reference${
                action.type === 'acknowledge' ||
                action.type === 'reconcile'
                  ? ' *'
                  : ''
              }`}
            >
              <Input
                value={evidenceReference}
                onChange={(event) => setEvidenceReference(event.target.value)}
              />
            </Field>
          )}

          {validation && validation !== fileReadError && (
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

function ContentFields({
  content,
  setContent,
  contentType,
  subject,
  required,
  fileName,
  setFileName,
  checksum,
  setChecksum,
  onLoadFile,
  fileReadError,
}: {
  content: string;
  setContent: (value: string) => void;
  contentType: ProcurementGhanepsContentType;
  subject: string;
  required: boolean;
  fileName?: string;
  setFileName?: (value: string) => void;
  checksum: string;
  setChecksum: (value: string) => void;
  onLoadFile?: (file?: File) => Promise<void>;
  fileReadError?: string;
}) {
  const contentMetadata = ghanepsContentTypeMetadata(contentType);
  return (
    <>
      <ReadOnlyLine
        label={`${subject} representation (DEC-009 controlled)`}
        value={`${contentMetadata.label} · ${contentType}`}
      />
      {onLoadFile && (
        <Field label={`Load ${contentMetadata.label} file`}>
          <Input
            type="file"
            accept={contentMetadata.accept}
            onChange={(event) => {
              const file = event.currentTarget.files?.[0];
              event.currentTarget.value = '';
              void onLoadFile(file);
            }}
          />
          <span className="text-xs text-muted-foreground">
            Loads up to 5,000,000 UTF-8 bytes into the editable content field.
            Submission remains manual and server-validated.
          </span>
          {fileReadError && (
            <span className="text-xs text-destructive">{fileReadError}</span>
          )}
        </Field>
      )}
      <Field
        label={`${subject} content (${contentMetadata.label})${required ? ' *' : ''}`}
      >
        <Textarea
          value={content}
          onChange={(event) => setContent(event.target.value)}
          rows={8}
          className="font-mono text-xs"
        />
      </Field>
      {setFileName && (
        <Field label="File name">
          <Input
            value={fileName ?? ''}
            onChange={(event) => setFileName(event.target.value)}
          />
        </Field>
      )}
      <Field label={`Expected ${subject.toLowerCase()} SHA-256`}>
        <Input
          value={checksum}
          onChange={(event) => setChecksum(event.target.value)}
          maxLength={64}
        />
      </Field>
    </>
  );
}

function AttemptFields({
  outcome,
  setOutcome,
  transportReference,
  setTransportReference,
  failureCode,
  setFailureCode,
  reason,
  setReason,
}: {
  outcome: string;
  setOutcome: (value: string) => void;
  transportReference: string;
  setTransportReference: (value: string) => void;
  failureCode: string;
  setFailureCode: (value: string) => void;
  reason: string;
  setReason: (value: string) => void;
}) {
  return (
    <>
      <OutcomeField
        label="Attempt outcome *"
        value={outcome}
        onChange={setOutcome}
        values={['Succeeded', 'Failed']}
      />
      <Field label="Transport reference">
        <Input
          value={transportReference}
          onChange={(event) => setTransportReference(event.target.value)}
        />
      </Field>
      {outcome === 'Failed' && (
        <>
          <Field label="Failure code *">
            <Input
              value={failureCode}
              onChange={(event) => setFailureCode(event.target.value)}
            />
          </Field>
          <Field label="Failure message *">
            <Textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              rows={4}
            />
          </Field>
        </>
      )}
    </>
  );
}

function actionMetadata(action?: GhanepsExchangeAction) {
  switch (action?.type) {
    case 'prepare-export':
      return {
        title: 'Prepare immutable GHANEPS export',
        description:
          'Retain the exact payload against the current source and effective DEC-009 mapping. The server validates source and checksum lineage.',
        submit: 'Prepare export',
        success: 'GHANEPS export event retained.',
      };
    case 'record-import':
      return {
        title: 'Record controlled GHANEPS import',
        description:
          'Retain the external payload, checksum, and transport reference against the server-validated source and DEC-009 mapping.',
        submit: 'Record import',
        success: 'GHANEPS import event retained.',
      };
    case 'record-attempt':
      return {
        title: 'Record GHANEPS transport attempt',
        description:
          'Append a transport outcome to the exact immutable payload version.',
        submit: 'Record attempt',
        success: 'GHANEPS transport attempt retained.',
      };
    case 'retry':
      return {
        title: 'Retry failed GHANEPS exchange',
        description:
          'Append a retry from the exact failed event/payload lineage. Prior attempts remain immutable.',
        submit: 'Record retry',
        success: 'GHANEPS retry retained.',
      };
    case 'acknowledge':
      return {
        title: 'Record GHANEPS acknowledgement',
        description:
          'Append the external acknowledgement payload and checksum to the exact exchange event.',
        submit: 'Record acknowledgement',
        success: 'GHANEPS acknowledgement retained.',
      };
    case 'reconcile':
      return action.resolveExistingMismatch
        ? {
            title: 'Resolve GHANEPS reconciliation mismatch',
            description:
              'Independently resolve the latest retained mismatch without changing the original reconciliation record.',
            submit: 'Resolve mismatch',
            success: 'GHANEPS reconciliation mismatch resolved.',
          }
        : {
            title: 'Reconcile GHANEPS exchange',
            description:
              'Compare the actual external reference/checksum with the exact retained payload and append the result.',
            submit: 'Record reconciliation',
            success: 'GHANEPS reconciliation retained.',
          };
    default:
      return {
        title: 'GHANEPS exchange action',
        description: 'Complete the controlled append-only action.',
        submit: 'Submit',
        success: 'GHANEPS exchange action retained.',
      };
  }
}

function validate(input: {
  action?: GhanepsExchangeAction;
  reference: string;
  evidenceReference: string;
  payloadContent: string;
  configuredContentType?: ProcurementGhanepsContentType;
  checksum: string;
  transportReference: string;
  outcome: string;
  failureCode: string;
  reason: string;
}) {
  if (!input.action) return undefined;
  if (
    input.action.type === 'retry' &&
    input.action.requiresCorrectedPayload &&
    !input.payloadContent.trim()
  )
    return 'A corrected replacement payload is required after a rejected acknowledgement.';
  if (
    (input.action.type === 'acknowledge' ||
      input.action.type === 'reconcile') &&
    !input.evidenceReference.trim()
  )
    return 'Shared evidence reference is required.';
  if (
    input.action.type === 'prepare-export' ||
    input.action.type === 'record-import'
  ) {
    if (input.action.type === 'record-import' && !input.transportReference.trim())
      return 'Transport reference is required.';
    if (!input.configuredContentType)
      return 'The DEC-009 payload representation is unavailable.';
    const contentValidation = validateGhanepsContent(
      input.payloadContent,
      input.configuredContentType,
      'Payload content'
    );
    if (contentValidation) return contentValidation;
    if (input.checksum && !isHash(input.checksum))
      return 'Expected payload checksum must be a 64-character SHA-256 value.';
  }
  if (
    input.action.type === 'record-attempt' ||
    input.action.type === 'retry'
  ) {
    if (!input.outcome) return 'Attempt outcome is required.';
    if (
      input.outcome === 'Succeeded' &&
      !input.transportReference.trim()
    )
      return 'A successful attempt requires a transport reference.';
    if (input.outcome === 'Failed') {
      if (!input.failureCode.trim()) return 'Failure code is required.';
      if (!input.reason.trim()) return 'Failure message is required.';
    }
    if (
      input.action.type === 'retry' &&
      input.payloadContent.trim()
    ) {
      if (!input.configuredContentType)
        return 'The DEC-009 payload representation is unavailable.';
      const contentValidation = validateGhanepsContent(
        input.payloadContent,
        input.configuredContentType,
        'Replacement payload content'
      );
      if (contentValidation) return contentValidation;
    }
    if (input.checksum && !isHash(input.checksum))
      return 'Expected payload checksum must be a 64-character SHA-256 value.';
    if (
      input.action.type === 'retry' &&
      input.checksum &&
      !input.payloadContent.trim()
    )
      return 'An expected replacement checksum requires replacement payload content.';
  }
  if (input.action.type === 'acknowledge') {
    if (!input.outcome) return 'Acknowledgement outcome is required.';
    if (!input.reference.trim())
      return 'Acknowledgement reference is required.';
    if (!input.configuredContentType)
      return 'The DEC-009 acknowledgement representation is unavailable.';
    const contentValidation = validateGhanepsContent(
      input.payloadContent,
      input.configuredContentType,
      'Acknowledgement content'
    );
    if (contentValidation) return contentValidation;
    if (input.checksum && !isHash(input.checksum))
      return 'Expected acknowledgement checksum must be a 64-character SHA-256 value.';
  }
  if (input.action.type === 'reconcile') {
    if (!input.reference.trim()) return 'Actual reference is required.';
    if (!isHash(input.checksum))
      return 'Actual checksum must be a 64-character SHA-256 value.';
  }
  return undefined;
}

const isHash = (value: string) => /^[a-f0-9]{64}$/i.test(value);

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="grid gap-1.5 text-sm">
      <span className="font-medium">{label}</span>
      {children}
    </label>
  );
}

function OutcomeField({
  label,
  value,
  onChange,
  values,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  values: string[];
}) {
  return (
    <Field label={label}>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select outcome" />
        </SelectTrigger>
        <SelectContent>
          {values.map((item) => (
            <SelectItem key={item} value={item}>
              {item}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}

function ReadOnlyLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border bg-muted/20 p-3 text-sm">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 font-medium">{value}</p>
    </div>
  );
}

function ReadOnlyHash({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border bg-muted/20 p-3 text-sm">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 break-all font-mono text-xs">{value}</p>
    </div>
  );
}
