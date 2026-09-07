'use client';

import React, { useEffect, useRef, useState } from 'react';
import { CalendarClock, FilePlus2, Loader2, ReceiptText } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
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
import { Textarea } from '@/components/ui/textarea';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { calculateBidValidity } from '@/lib/procurement-bid-validity';
import { TenderDocumentRecipientSelector } from './TenderDocumentRecipientSelector';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  hasAnyTenderDocumentAction,
  suggestedTenderDocumentSelection,
  tenderDocumentChangeTypeLabel,
  validateTenderDocumentChange,
  validateTenderDocumentIssue,
} from '@/lib/procurement-tender-document';
import { procurementTenderDocumentService as service } from '@/services/procurement-tender-document.service';
import type {
  AcknowledgeProcurementTenderDocumentChangeRequest,
  DecideProcurementTenderDocumentChangeRequest,
  ProcurementTenderDocumentAcknowledgementOutcome,
  ProcurementTenderDocumentChange,
  ProcurementTenderDocumentChangeType,
  ProcurementTenderDocumentReadiness,
  ProcurementTenderDocumentRegister,
  ProcurementTenderDocumentTemplateListItem,
  ProcurementTenderDocumentWorkflowOption,
} from '@/types/procurement-tender-document';

const localInput = (value?: string) => {
  if (!value) return '';
  const date = new Date(value);
  const shifted = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
};

export function TenderDocumentActionDialogs({
  readiness,
  register,
  approvedTemplates,
  workflows,
  canManage,
  onChanged,
}: {
  readiness: ProcurementTenderDocumentReadiness;
  register?: ProcurementTenderDocumentRegister;
  approvedTemplates: ProcurementTenderDocumentTemplateListItem[];
  workflows: ProcurementTenderDocumentWorkflowOption[];
  canManage: boolean;
  onChanged: () => Promise<void>;
}) {
  const [bindOpen, setBindOpen] = useState(false);
  const [bindSchedule, setBindSchedule] = useState(false);
  const [bindDeadline, setBindDeadline] = useState('');
  const [bindOpening, setBindOpening] = useState('');
  const [bindWorkflow, setBindWorkflow] = useState('');
  const [bindReason, setBindReason] = useState('');
  const [bindEvidence, setBindEvidence] = useState('');
  const [bindError, setBindError] = useState('');
  const [bindRecorded, setBindRecorded] = useState(false);
  const [issueOpen, setIssueOpen] = useState(false);
  const [recipientMode, setRecipientMode] = useState<
    'saved' | 'new' | 'configured'
  >('saved');
  const [issueError, setIssueError] = useState('');
  const [issueRefreshWarning, setIssueRefreshWarning] = useState('');
  const [loadingRecipientContacts, setLoadingRecipientContacts] =
    useState(false);
  const [recipientContactNotice, setRecipientContactNotice] = useState('');
  const contactRequest = useRef(0);
  const contactEdits = useRef({ email: false, phone: false });
  const [changeOpen, setChangeOpen] = useState(false);
  const [changeError, setChangeError] = useState('');
  const [busy, setBusy] = useState(false);
  const [templateVersionId, setTemplateVersionId] = useState('');
  const [submissionDeadline, setSubmissionDeadline] = useState('');
  const [openingAt, setOpeningAt] = useState('');
  const [validityDays, setValidityDays] = useState<number | undefined>();
  const [validityReference, setValidityReference] = useState('');
  const savedValidityDays = readiness.bidValidityPeriodDays;
  const bidValidity = calculateBidValidity(
    bindSchedule ? bindDeadline : readiness.effectiveSubmissionDeadlineUtc,
    savedValidityDays ?? validityDays
  );
  const [feeMode, setFeeMode] = useState<'Free' | 'Paid'>('Free');
  const [feeAmount, setFeeAmount] = useState(0);
  const [currencyCode, setCurrencyCode] = useState('GHS');
  const [issue, setIssue] = useState({
    businessPartnerId: '',
    recipientName: '',
    recipientEmail: '',
    recipientPhone: '',
    amountPaid: 0,
    paymentReference: '',
    receiptNumber: '',
    issueChannel: 'ExternalPortal',
    evidenceReference: '',
  });
  const [changeType, setChangeType] =
    useState<ProcurementTenderDocumentChangeType>('Addendum');
  const [newTemplateVersionId, setNewTemplateVersionId] = useState('');
  const [newValueUtc, setNewValueUtc] = useState('');
  const [newOpeningUtc, setNewOpeningUtc] = useState('');
  const [requiresAcknowledgement, setRequiresAcknowledgement] = useState(true);
  const [workflowDefinitionId, setWorkflowDefinitionId] = useState('');
  const [reason, setReason] = useState('');
  const [changeEvidence, setChangeEvidence] = useState('');

  function updateIssueField<Key extends keyof typeof issue>(
    field: Key,
    value: (typeof issue)[Key]
  ) {
    // Merge with the latest state: a contact lookup may settle in the same input batch.
    setIssue((current) => ({ ...current, [field]: value }));
  }

  useEffect(
    () => () => {
      contactRequest.current += 1;
    },
    []
  );

  useEffect(() => {
    setSubmissionDeadline(localInput(readiness.effectiveSubmissionDeadlineUtc));
    setOpeningAt(localInput(readiness.openingScheduledAtUtc));
    setFeeMode(readiness.feeMode ?? 'Free');
    setFeeAmount(readiness.feeAmount ?? 0);
    setCurrencyCode(readiness.currencyCode ?? 'GHS');
  }, [readiness]);

  useEffect(() => {
    // A transcription belongs to this exact source/document, not another tender or version.
    setValidityDays(undefined);
    setValidityReference('');
  }, [readiness.sourceId, templateVersionId]);

  useEffect(() => {
    if (!register) return;
    setIssue((current) => ({
      ...current,
      amountPaid: register.feeMode === 'Paid' ? register.feeAmount : 0,
    }));
  }, [register]);

  useEffect(() => {
    setTemplateVersionId((current) =>
      suggestedTenderDocumentSelection(
        current,
        readiness.effectiveTemplateVersionId,
        approvedTemplates
      )
    );
  }, [readiness.effectiveTemplateVersionId, approvedTemplates]);

  const run = async (
    action: () => Promise<unknown>,
    success: string,
    onError?: (message: string) => void
  ) => {
    try {
      setBusy(true);
      await action();
      toast.success(success);
      await onChanged();
      return true;
    } catch (error) {
      const message = getProcurementProblemMessage(
        error,
        'Controlled action failed'
      );
      toast.error(message);
      onError?.(message);
      return false;
    } finally {
      setBusy(false);
    }
  };

  const bind = async () => {
    if (busy || bindRecorded) return;
    setBindError('');
    const missing: string[] = [];
    if (!templateVersionId) missing.push('Approved effective version');
    if (!submissionDeadline) missing.push('Original submission deadline');
    if (!currencyCode.trim()) missing.push('Currency');
    if (
      !Number.isSafeInteger(savedValidityDays ?? validityDays) ||
      (savedValidityDays ?? validityDays ?? 0) <= 0
    )
      missing.push('Bid validity period (calendar days)');
    if (!savedValidityDays && !validityReference.trim())
      missing.push('Approved validity document/clause reference');
    if (bindSchedule && !bindDeadline) missing.push('New submission deadline');
    if (bindSchedule && !bindOpening) missing.push('New opening scheduled');
    if (bindSchedule && !bindWorkflow)
      missing.push('Schedule approval workflow');
    if (bindSchedule && !bindReason.trim())
      missing.push('Reason for new dates');
    if (bindSchedule && !bindEvidence.trim())
      missing.push('Schedule evidence reference');
    if (missing.length) {
      setBindError(`Complete: ${missing.join('; ')}.`);
      return;
    }
    if (!bidValidity) {
      setBindError(
        'The submission date and validity period must produce a valid expiry date.'
      );
      return;
    }
    if (feeMode === 'Paid' && feeAmount <= 0) {
      toast.error('Paid document binding requires a positive fee.');
      return;
    }
    const originalDeadline = new Date(
      readiness.effectiveSubmissionDeadlineUtc ?? ''
    );
    const proposedDeadline = new Date(bindDeadline);
    const proposedOpening = new Date(bindOpening);
    const validity = new Date(bidValidity);
    if (
      bindSchedule &&
      (!canBindSchedule ||
        !bindWorkflow ||
        !bindReason.trim() ||
        !bindEvidence.trim() ||
        !Number.isFinite(proposedDeadline.getTime()) ||
        !Number.isFinite(proposedOpening.getTime()) ||
        proposedDeadline.getTime() <= Date.now() ||
        proposedDeadline <= originalDeadline ||
        proposedOpening <= proposedDeadline ||
        (readiness.openingScheduledAtUtc &&
          proposedOpening <= new Date(readiness.openingScheduledAtUtc)) ||
        proposedDeadline >= validity)
    ) {
      setBindError(
        'Choose a future submission deadline later than the original and before bid-validity expiry, a later opening after submission, and complete the approval workflow, reason and evidence reference.'
      );
      return;
    }
    if (!bindSchedule && originalDeadline.getTime() <= Date.now()) {
      setBindError(
        'The deadline has elapsed. Request new dates for approval before binding.'
      );
      return;
    }
    try {
      setBusy(true);
      await service.bind({
        sourceType: readiness.sourceType,
        sourceId: readiness.sourceId,
        templateVersionId,
        submissionDeadlineUtc: originalDeadline.toISOString(),
        openingScheduledAtUtc: readiness.openingScheduledAtUtc,
        bidValidityUntilUtc: bidValidity,
        bidValidityPeriodDays: savedValidityDays ?? validityDays,
        bidValidityTermsReference: savedValidityDays
          ? undefined
          : validityReference.trim(),
        feeMode,
        feeAmount: feeMode === 'Free' ? 0 : feeAmount,
        currencyCode: currencyCode.toUpperCase(),
        scheduleChange: bindSchedule
          ? {
              submissionDeadlineUtc: proposedDeadline.toISOString(),
              openingScheduledAtUtc: proposedOpening.toISOString(),
              workflowDefinitionId: bindWorkflow,
              reason: bindReason.trim(),
              evidenceReference: bindEvidence.trim(),
            }
          : undefined,
      });
      setBindRecorded(true);
      toast.success(
        bindSchedule
          ? 'Document bound; schedule change awaiting approval. Tender dates have not changed.'
          : 'Exact approved tender-document version bound'
      );
      try {
        await onChanged();
        setBindOpen(false);
      } catch {
        setBindError(
          'The binding was saved, but the register could not refresh. Refresh the register; do not submit again.'
        );
      }
    } catch (error) {
      const message = getProcurementProblemMessage(
        error,
        'Document binding failed'
      );
      setBindError(message);
      toast.error(message);
    } finally {
      setBusy(false);
    }
  };

  const selectRecipient = async (partner?: BusinessPartnerDto) => {
    const request = ++contactRequest.current;
    contactEdits.current = { email: false, phone: false };
    setIssueError('');
    setRecipientContactNotice('');
    setLoadingRecipientContacts(false);
    setIssue((current) => ({
      ...current,
      businessPartnerId: partner?.id ?? '',
      recipientName: partner?.partnerName ?? '',
      recipientEmail: partner?.email?.trim() ?? '',
      recipientPhone: partner?.phone?.trim() ?? '',
    }));
    if (!partner || (partner.email?.trim() && partner.phone?.trim())) return;
    setLoadingRecipientContacts(true);
    try {
      const contacts = await businessPartnerService.getPartnerContacts(
        partner.id
      );
      if (request !== contactRequest.current) return;
      const primary = contacts
        .filter((contact) => contact.isActive !== false && contact.isPrimary)
        .sort((left, right) => left.id.localeCompare(right.id))[0];
      if (!primary) return;
      setIssue((current) =>
        request !== contactRequest.current ||
        current.businessPartnerId !== partner.id
          ? current
          : {
              ...current,
              recipientEmail:
                !contactEdits.current.email && !current.recipientEmail.trim()
                  ? (primary.email?.trim() ?? '')
                  : current.recipientEmail,
              recipientPhone:
                !contactEdits.current.phone && !current.recipientPhone.trim()
                  ? primary.phone?.trim() || primary.mobile?.trim() || ''
                  : current.recipientPhone,
            }
      );
    } catch {
      if (request === contactRequest.current) {
        setRecipientContactNotice(
          'Saved contact details could not be loaded. Enter the recipient contact details below.'
        );
      }
    } finally {
      if (request === contactRequest.current)
        setLoadingRecipientContacts(false);
    }
  };

  const refreshIssuedRegister = async () => {
    try {
      await onChanged();
      setIssueRefreshWarning('');
    } catch (error) {
      const message = `Document issue was recorded, but the register could not be refreshed. ${getProcurementProblemMessage(error, 'Retry refreshing the register.')}`;
      setIssueRefreshWarning(message);
      toast.warning(message);
    }
  };

  const issueDocument = async () => {
    if (!register || busy || loadingRecipientContacts || issueRefreshWarning)
      return;
    setIssueError('');
    if (requiresPublicationForIssue && register.isSourcePublished !== true) {
      setIssueError(
        'Publish the procurement before issuing documents to suppliers.'
      );
      return;
    }
    if (recipientMode === 'saved' && !issue.businessPartnerId) {
      setIssueError('Select a saved supplier from the search results.');
      return;
    }
    if (recipientMode === 'new' && register.allowsNewRecipient !== true) {
      setIssueError('This procurement requires a saved supplier recipient.');
      return;
    }
    if (
      recipientMode === 'configured' &&
      !register.allowedExternalRecipientEmails?.includes(issue.recipientEmail)
    ) {
      setIssueError(
        'Select an external recipient already configured on this procurement.'
      );
      return;
    }
    if (
      recipientMode === 'new' &&
      !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(issue.recipientEmail.trim())
    ) {
      setIssueError('Enter a valid email for the new interested supplier.');
      return;
    }
    const request = {
      sourceType: register.sourceType,
      sourceId: register.sourceId,
      businessPartnerId: issue.businessPartnerId || undefined,
      recipientName: issue.recipientName,
      recipientEmail: issue.recipientEmail || undefined,
      recipientPhone: issue.recipientPhone || undefined,
      amountPaid: issue.amountPaid,
      paymentReference: issue.paymentReference || undefined,
      receiptNumber: issue.receiptNumber,
      issueChannel: issue.issueChannel,
      evidenceReference: issue.evidenceReference,
      registerRowVersion: register.rowVersion,
    };
    const validation = validateTenderDocumentIssue(
      request,
      register.feeMode,
      register.feeAmount
    );
    if (validation) {
      toast.error(validation);
      setIssueError(validation);
      return;
    }
    setBusy(true);
    try {
      await service.issue(request);
    } catch (error) {
      const message = getProcurementProblemMessage(
        error,
        'Document issue failed.'
      );
      setIssueError(message);
      toast.error(message);
      setBusy(false);
      return;
    }
    try {
      // Saving and reloading are different outcomes: never offer to re-save a
      // successful issue just because the subsequent read failed.
      setIssueOpen(false);
      setRecipientMode('saved');
      setIssue({
        businessPartnerId: '',
        recipientName: '',
        recipientEmail: '',
        recipientPhone: '',
        amountPaid: register.feeMode === 'Paid' ? register.feeAmount : 0,
        paymentReference: '',
        receiptNumber: '',
        issueChannel: 'ExternalPortal',
        evidenceReference: '',
      });
      toast.success('Immutable document issue recorded');
      await refreshIssuedRegister();
    } finally {
      setBusy(false);
    }
  };

  const createChange = async () => {
    if (!register || busy) return;
    setChangeError('');
    if (
      changeType === 'UnpublishedScheduleReschedule' &&
      (register.isSourcePublished !== false ||
        !hasAnyTenderDocumentAction(register.allowedActions, [
          'RescheduleUnpublished',
        ]))
    ) {
      setChangeError(
        'This tender no longer allows pre-publication rescheduling. Refresh the register.'
      );
      return;
    }
    const deadline = new Date(newValueUtc);
    const nextOpening = new Date(newOpeningUtc);
    const request = {
      sourceType: register.sourceType,
      sourceId: register.sourceId,
      changeType,
      newTemplateVersionId:
        changeType === 'Addendum'
          ? newTemplateVersionId || undefined
          : undefined,
      newValueUtc:
        changeType === 'Addendum' ||
        !newValueUtc ||
        Number.isNaN(deadline.getTime())
          ? undefined
          : deadline.toISOString(),
      newOpeningScheduledAtUtc:
        changeType === 'UnpublishedScheduleReschedule' &&
        newOpeningUtc &&
        !Number.isNaN(nextOpening.getTime())
          ? nextOpening.toISOString()
          : undefined,
      requiresAcknowledgement,
      reason,
      workflowDefinitionId,
      evidenceReference: changeEvidence,
      registerRowVersion: register.rowVersion,
    };
    const validation = validateTenderDocumentChange(request, register);
    if (validation) {
      toast.error(validation);
      setChangeError(validation);
      return;
    }
    const completed = await run(
      () => service.createChange(request),
      `${tenderDocumentChangeTypeLabel[changeType]} submitted for approval`,
      setChangeError
    );
    if (completed) {
      setChangeOpen(false);
      setReason('');
      setChangeEvidence('');
      setNewValueUtc('');
      setNewOpeningUtc('');
      setNewTemplateVersionId('');
    }
  };

  const bindAllowed =
    canManage &&
    !readiness.hasRegister &&
    hasAnyTenderDocumentAction(readiness.allowedActions, [
      'Bind',
      'BindVersion',
      'BindRegister',
    ]);
  const requiresPublicationForIssue =
    register?.sourceType === 'Tender' &&
    register.method !== 'RequestForQuotation';
  const issueAllowed =
    canManage &&
    Boolean(register) &&
    (!requiresPublicationForIssue || register?.isSourcePublished === true) &&
    hasAnyTenderDocumentAction(register?.allowedActions, [
      'Issue',
      'IssueDocument',
    ]);
  const changeAllowed =
    canManage &&
    Boolean(register) &&
    !register?.changes?.some((change) => change.status === 'PendingApproval') &&
    hasAnyTenderDocumentAction(register?.allowedActions, [
      'CreateChange',
      'CreateAddendum',
      'ExtendSubmissionDeadline',
      'ExtendBidValidity',
    ]);
  const rescheduleAllowed =
    canManage &&
    register?.sourceType === 'Tender' &&
    register.isSourcePublished === false &&
    hasAnyTenderDocumentAction(register?.allowedActions, [
      'RescheduleUnpublished',
    ]);
  const rescheduling = changeType === 'UnpublishedScheduleReschedule';
  const canBindSchedule =
    canManage &&
    hasAnyTenderDocumentAction(readiness.allowedActions, [
      'BindWithScheduleChange',
    ]);
  const effectiveTemplate = approvedTemplates.find(
    (template) => template.id === register?.effectiveTemplateVersionId
  );

  return (
    <>
      <div className="flex flex-wrap gap-2">
        {bindAllowed && (
          <Button
            onClick={() => {
              setBindError('');
              setBindRecorded(false);
              setBindSchedule(
                canBindSchedule &&
                  new Date(
                    readiness.effectiveSubmissionDeadlineUtc ?? ''
                  ).getTime() <= Date.now()
              );
              setBindOpen(true);
            }}
          >
            <FilePlus2 className="mr-2 h-4 w-4" /> Bind approved version
          </Button>
        )}
        {issueAllowed && (
          <Button
            disabled={busy || Boolean(issueRefreshWarning)}
            onClick={() => setIssueOpen(true)}
          >
            <ReceiptText className="mr-2 h-4 w-4" /> Issue document
          </Button>
        )}
        {rescheduleAllowed && (
          <Button
            variant="outline"
            disabled={busy}
            onClick={() => {
              if (!rescheduling) {
                setNewValueUtc(
                  localInput(register?.effectiveSubmissionDeadlineUtc)
                );
                setNewOpeningUtc(localInput(register?.openingScheduledAtUtc));
              }
              setChangeType('UnpublishedScheduleReschedule');
              setRequiresAcknowledgement(false);
              setChangeError('');
              setChangeOpen(true);
            }}
          >
            <CalendarClock className="mr-2 h-4 w-4" /> Reschedule before
            publication
          </Button>
        )}
        {changeAllowed && !rescheduleAllowed && (
          <Button
            variant="outline"
            onClick={() => {
              setChangeType('Addendum');
              setRequiresAcknowledgement(true);
              setChangeError('');
              setChangeOpen(true);
            }}
          >
            <CalendarClock className="mr-2 h-4 w-4" /> Governed change
          </Button>
        )}
      </div>

      {requiresPublicationForIssue && register?.isSourcePublished === false && (
        <p className="mt-3 text-sm text-muted-foreground">
          Prepare and approve documents here. Publish the tender before issuing
          documents to suppliers.
        </p>
      )}

      {issueRefreshWarning && (
        <Alert className="mt-3">
          <AlertDescription className="space-y-2">
            <p>{issueRefreshWarning}</p>
            <Button
              variant="outline"
              disabled={busy}
              onClick={async () => {
                setBusy(true);
                try {
                  await refreshIssuedRegister();
                } finally {
                  setBusy(false);
                }
              }}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Refresh register
            </Button>
          </AlertDescription>
        </Alert>
      )}

      <Dialog
        open={bindOpen}
        onOpenChange={(open) => {
          if (!busy && !bindRecorded) setBindOpen(open);
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Bind exact approved document version</DialogTitle>
            <DialogDescription>
              The binding freezes policy, method, content checksum, deadline,
              bid validity, and paid-or-free issue terms for this source.
              Original dates are retained. Eligible unpublished tenders can
              request new dates here; they take effect only after approval.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="md:col-span-2">
              <Field label="Approved effective version">
                <Select
                  value={templateVersionId}
                  onValueChange={setTemplateVersionId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select approved version" />
                  </SelectTrigger>
                  <SelectContent>
                    {approvedTemplates.map((template) => (
                      <SelectItem key={template.id} value={template.id}>
                        {template.templateCode} · v{template.version} ·{' '}
                        {template.documentTypeCode}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            </div>
            <Field label="Original submission deadline">
              <Input
                aria-label="Original submission deadline"
                type="datetime-local"
                value={submissionDeadline}
                readOnly
              />
            </Field>
            <Field label="Original opening scheduled">
              <Input
                aria-label="Original opening scheduled"
                type="datetime-local"
                value={openingAt}
                readOnly
              />
            </Field>
            <Field label="Bid validity period (calendar days)">
              <Input
                aria-label="Bid validity period (calendar days)"
                type="number"
                min={1}
                step={1}
                readOnly={
                  savedValidityDays !== undefined && savedValidityDays !== null
                }
                value={savedValidityDays ?? validityDays ?? ''}
                onChange={(event) =>
                  setValidityDays(
                    event.target.value ? Number(event.target.value) : undefined
                  )
                }
              />
              <p className="mt-1 text-xs text-muted-foreground">
                {savedValidityDays
                  ? 'From the saved tender terms. Change terms only through tender preparation and approval.'
                  : 'Not recorded on this source. Copy the period stated in the approved document; do not choose a default.'}
              </p>
            </Field>
            {!savedValidityDays && (
              <Field label="Approved validity document/clause reference">
                <Input
                  aria-label="Approved validity document/clause reference"
                  value={validityReference}
                  maxLength={500}
                  onChange={(event) => setValidityReference(event.target.value)}
                />
              </Field>
            )}
            <Field label="Bid valid until (calculated)">
              <Input
                aria-label="Bid valid until (calculated)"
                readOnly
                value={
                  bidValidity
                    ? new Date(bidValidity).toLocaleString()
                    : 'Enter the period and submission date'
                }
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Calculated from {bindSchedule ? 'the proposed' : 'the original'}{' '}
                submission deadline. Not the opening or delivery date.
              </p>
            </Field>
            <Field label="Fee mode">
              <Select
                value={feeMode}
                onValueChange={(value) => {
                  const mode = value as 'Free' | 'Paid';
                  setFeeMode(mode);
                  if (mode === 'Free') setFeeAmount(0);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Free">Free</SelectItem>
                  <SelectItem value="Paid">Paid</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Fee amount">
              <Input
                type="number"
                min={0}
                disabled={feeMode === 'Free'}
                value={feeAmount}
                onChange={(event) => setFeeAmount(Number(event.target.value))}
              />
            </Field>
            <Field label="Currency">
              <Input
                maxLength={3}
                value={currencyCode}
                onChange={(event) => setCurrencyCode(event.target.value)}
              />
            </Field>
          </div>
          {canBindSchedule && (
            <div className="space-y-3 rounded-lg border p-4">
              <Label className="flex items-center gap-2">
                <Checkbox
                  checked={bindSchedule}
                  disabled={busy || bindRecorded}
                  onCheckedChange={(value) => setBindSchedule(value === true)}
                />
                Request new dates for approval
              </Label>
              {bindSchedule && (
                <>
                  <p className="text-sm text-muted-foreground">
                    The original dates remain unchanged until the configured
                    independent approval is completed. The tender is not
                    published by this action.
                  </p>
                  <div className="grid gap-3 md:grid-cols-2">
                    <Field label="New submission deadline">
                      <Input
                        aria-label="New submission deadline"
                        type="datetime-local"
                        value={bindDeadline}
                        disabled={busy || bindRecorded}
                        onChange={(event) =>
                          setBindDeadline(event.target.value)
                        }
                      />
                    </Field>
                    <Field label="New opening scheduled">
                      <Input
                        aria-label="New opening scheduled"
                        type="datetime-local"
                        value={bindOpening}
                        disabled={busy || bindRecorded}
                        onChange={(event) => setBindOpening(event.target.value)}
                      />
                    </Field>
                    <Field label="Schedule approval workflow">
                      <Select
                        value={bindWorkflow}
                        disabled={busy || bindRecorded}
                        onValueChange={setBindWorkflow}
                      >
                        <SelectTrigger aria-label="Schedule approval workflow">
                          <SelectValue placeholder="Select Published workflow" />
                        </SelectTrigger>
                        <SelectContent>
                          {workflows.map((workflow) => (
                            <SelectItem key={workflow.id} value={workflow.id}>
                              {workflow.name} · v{workflow.version}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </Field>
                    <Field label="Schedule evidence reference">
                      <Input
                        aria-label="Schedule evidence reference"
                        value={bindEvidence}
                        disabled={busy || bindRecorded}
                        onChange={(event) =>
                          setBindEvidence(event.target.value)
                        }
                      />
                    </Field>
                    <div className="md:col-span-2">
                      <Field label="Reason for new dates">
                        <Textarea
                          aria-label="Reason for new dates"
                          value={bindReason}
                          disabled={busy || bindRecorded}
                          onChange={(event) =>
                            setBindReason(event.target.value)
                          }
                        />
                      </Field>
                    </div>
                  </div>
                </>
              )}
            </div>
          )}
          {bindError && (
            <Alert variant="destructive">
              <AlertDescription>{bindError}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={busy || bindRecorded}
              onClick={() => setBindOpen(false)}
            >
              Cancel
            </Button>
            {bindRecorded ? (
              <Button
                disabled={busy}
                onClick={async () => {
                  setBusy(true);
                  try {
                    await onChanged();
                    setBindRecorded(false);
                    setBindOpen(false);
                  } catch {
                    setBindError(
                      'The binding was saved. Refresh is still unavailable; do not submit again.'
                    );
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                Refresh register
              </Button>
            ) : (
              <Button disabled={busy} onClick={() => void bind()}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {bindSchedule
                  ? 'Bind and request schedule approval'
                  : 'Bind immutable version'}
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={issueOpen}
        onOpenChange={(open) => {
          if (!busy) setIssueOpen(open);
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Record controlled issue or sale</DialogTitle>
            <DialogDescription>
              Record who receives this document version. This is not a bid,
              invitation restriction, or supplier approval.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-3 md:col-span-2">
              {(register?.allowsNewRecipient === true ||
                Boolean(register?.allowedExternalRecipientEmails?.length)) && (
                <Field label="Recipient">
                  <Select
                    value={recipientMode}
                    disabled={busy}
                    onValueChange={(mode) => {
                      setRecipientMode(mode as 'saved' | 'new' | 'configured');
                      contactRequest.current += 1;
                      contactEdits.current = { email: false, phone: false };
                      setLoadingRecipientContacts(false);
                      setRecipientContactNotice('');
                      setIssueError('');
                      setIssue((current) => ({
                        ...current,
                        businessPartnerId: '',
                        recipientName: '',
                        recipientEmail: '',
                        recipientPhone: '',
                      }));
                    }}
                  >
                    <SelectTrigger aria-label="Recipient type">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="saved">Saved supplier</SelectItem>
                      {register?.allowsNewRecipient === true && (
                        <SelectItem value="new">
                          New interested supplier
                        </SelectItem>
                      )}
                      {Boolean(
                        register?.allowedExternalRecipientEmails?.length
                      ) && (
                        <SelectItem value="configured">
                          Configured external recipient
                        </SelectItem>
                      )}
                    </SelectContent>
                  </Select>
                </Field>
              )}
              {recipientMode === 'saved' ? (
                <TenderDocumentRecipientSelector
                  value={issue.businessPartnerId}
                  name={issue.recipientName}
                  disabled={busy}
                  onSelect={(partner) => void selectRecipient(partner)}
                />
              ) : recipientMode === 'configured' ? (
                <Field label="Configured external recipient">
                  <Select
                    value={issue.recipientEmail}
                    disabled={busy}
                    onValueChange={(email) => {
                      setIssueError('');
                      setIssue((current) => ({
                        ...current,
                        recipientEmail: email,
                      }));
                    }}
                  >
                    <SelectTrigger aria-label="Configured external recipient">
                      <SelectValue placeholder="Select a configured recipient" />
                    </SelectTrigger>
                    <SelectContent>
                      {register?.allowedExternalRecipientEmails?.map(
                        (email) => (
                          <SelectItem key={email} value={email}>
                            {email}
                          </SelectItem>
                        )
                      )}
                    </SelectContent>
                  </Select>
                </Field>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Use a saved record if one exists. This records document
                  receipt only; it does not create an approved vendor or waive
                  GHANEPS registration or tender eligibility.
                </p>
              )}
              {loadingRecipientContacts && (
                <p
                  role="status"
                  className="flex items-center gap-2 text-sm text-muted-foreground"
                >
                  <Loader2 className="h-4 w-4 animate-spin" /> Loading saved
                  primary contact…
                </p>
              )}
              {recipientContactNotice && (
                <p role="alert" className="text-sm text-amber-800">
                  {recipientContactNotice}
                </p>
              )}
            </div>
            <Field label="Recipient name">
              <Input
                aria-label="Recipient name"
                readOnly={recipientMode === 'saved'}
                disabled={busy}
                value={issue.recipientName}
                onChange={(event) =>
                  updateIssueField('recipientName', event.target.value)
                }
              />
            </Field>
            <Field label="Recipient email">
              <Input
                type="email"
                aria-label="Recipient email"
                required={recipientMode === 'new'}
                readOnly={recipientMode === 'configured'}
                disabled={busy}
                value={issue.recipientEmail}
                onChange={(event) => {
                  contactEdits.current.email = true;
                  updateIssueField('recipientEmail', event.target.value);
                }}
              />
            </Field>
            <Field label="Recipient phone">
              <Input
                aria-label="Recipient phone"
                disabled={busy}
                value={issue.recipientPhone}
                onChange={(event) => {
                  contactEdits.current.phone = true;
                  updateIssueField('recipientPhone', event.target.value);
                }}
              />
            </Field>
            <Field label="Amount paid">
              <Input
                aria-label="Amount paid"
                type="number"
                min={0}
                disabled={busy || register?.feeMode === 'Free'}
                value={issue.amountPaid}
                onChange={(event) =>
                  updateIssueField('amountPaid', Number(event.target.value))
                }
              />
            </Field>
            <Field label="Payment reference">
              <Input
                aria-label="Payment reference"
                disabled={busy || register?.feeMode === 'Free'}
                value={issue.paymentReference}
                onChange={(event) =>
                  updateIssueField('paymentReference', event.target.value)
                }
              />
            </Field>
            <Field label="Receipt number">
              <Input
                aria-label="Receipt number"
                disabled={busy}
                value={issue.receiptNumber}
                onChange={(event) =>
                  updateIssueField('receiptNumber', event.target.value)
                }
              />
            </Field>
            <Field label="Issue channel">
              <Select
                value={issue.issueChannel}
                disabled={busy}
                onValueChange={(issueChannel) =>
                  updateIssueField('issueChannel', issueChannel)
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ExternalPortal">
                    External portal
                  </SelectItem>
                  <SelectItem value="Email">Email</SelectItem>
                  <SelectItem value="PhysicalCollection">
                    Physical collection
                  </SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <div className="md:col-span-2">
              <Field label="Shared issue/payment evidence reference">
                <Input
                  aria-label="Shared issue/payment evidence reference"
                  disabled={busy}
                  value={issue.evidenceReference}
                  onChange={(event) =>
                    updateIssueField('evidenceReference', event.target.value)
                  }
                />
              </Field>
            </div>
          </div>
          {issueError && (
            <Alert variant="destructive">
              <AlertDescription>{issueError}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => setIssueOpen(false)}
            >
              Cancel
            </Button>
            <Button
              disabled={busy || loadingRecipientContacts}
              onClick={() => void issueDocument()}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record immutable issue
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={changeOpen}
        onOpenChange={(open) => {
          if (!busy) setChangeOpen(open);
        }}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {rescheduling
                ? 'Reschedule unpublished tender'
                : 'Create governed document change'}
            </DialogTitle>
            <DialogDescription>
              {rescheduling
                ? 'Submit both new dates for independent approval. The original schedule is retained; the tender stays unpublished and the dates change only after approval.'
                : 'Approval, evidence, affected recipients, dispatch and mandatory acknowledgements remain part of the immutable register.'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            {!rescheduling && (
              <Field label="Change type">
                <Select
                  value={changeType}
                  onValueChange={(value) =>
                    setChangeType(value as ProcurementTenderDocumentChangeType)
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(
                      Object.keys(
                        tenderDocumentChangeTypeLabel
                      ) as ProcurementTenderDocumentChangeType[]
                    )
                      .filter(
                        (type) => type !== 'UnpublishedScheduleReschedule'
                      )
                      .map((type) => (
                        <SelectItem key={type} value={type}>
                          {tenderDocumentChangeTypeLabel[type]}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            <Field label="Exact Published workflow">
              <Select
                value={workflowDefinitionId}
                disabled={busy}
                onValueChange={setWorkflowDefinitionId}
              >
                <SelectTrigger aria-label="Approval workflow">
                  <SelectValue placeholder="Select workflow" />
                </SelectTrigger>
                <SelectContent>
                  {workflows.map((workflow) => (
                    <SelectItem key={workflow.id} value={workflow.id}>
                      {workflow.name} · v{workflow.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            {changeType === 'Addendum' ? (
              <div className="md:col-span-2">
                <Field label="Exact approved replacement document version">
                  <Select
                    value={newTemplateVersionId}
                    onValueChange={setNewTemplateVersionId}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select approved addendum version" />
                    </SelectTrigger>
                    <SelectContent>
                      {approvedTemplates
                        .filter(
                          (template) =>
                            template.id !==
                              register?.effectiveTemplateVersionId &&
                            (!effectiveTemplate ||
                              template.templateKey ===
                                effectiveTemplate.templateKey)
                        )
                        .map((template) => (
                          <SelectItem key={template.id} value={template.id}>
                            {template.templateCode} · v{template.version}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </Field>
              </div>
            ) : (
              <Field
                label={
                  changeType === 'SubmissionDeadlineExtension' || rescheduling
                    ? 'New submission deadline'
                    : 'New bid-validity date'
                }
              >
                <Input
                  aria-label={
                    rescheduling ? 'New submission deadline' : undefined
                  }
                  type="datetime-local"
                  disabled={busy}
                  value={newValueUtc}
                  onChange={(event) => setNewValueUtc(event.target.value)}
                />
              </Field>
            )}
            {rescheduling && (
              <Field label="New opening time">
                <Input
                  type="datetime-local"
                  aria-label="New opening time"
                  disabled={busy}
                  value={newOpeningUtc}
                  onChange={(event) => setNewOpeningUtc(event.target.value)}
                />
              </Field>
            )}
            {!rescheduling && (
              <label className="flex items-center gap-2 rounded border p-3 text-sm">
                <Checkbox
                  checked={requiresAcknowledgement}
                  onCheckedChange={(checked) =>
                    setRequiresAcknowledgement(Boolean(checked))
                  }
                />
                Mandatory recipient acknowledgement
              </label>
            )}
            <div className="md:col-span-2">
              <Field label="Governed reason">
                <Textarea
                  aria-label="Governed reason"
                  disabled={busy}
                  rows={3}
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </Field>
            </div>
            <div className="md:col-span-2">
              <Field label="Shared approval evidence reference">
                <Input
                  aria-label="Shared approval evidence reference"
                  disabled={busy}
                  value={changeEvidence}
                  onChange={(event) => setChangeEvidence(event.target.value)}
                />
              </Field>
            </div>
          </div>
          {changeError && (
            <Alert variant="destructive">
              <AlertDescription>{changeError}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => setChangeOpen(false)}
            >
              Cancel
            </Button>
            <Button disabled={busy} onClick={() => void createChange()}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Submit governed change
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

export function TenderDocumentDecisionDialog({
  change,
  action,
  open,
  onOpenChange,
  onChanged,
}: {
  change?: ProcurementTenderDocumentChange;
  action: 'Approve' | 'Reject';
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged: () => Promise<void>;
}) {
  const [busy, setBusy] = useState(false);
  const [approvalReference, setApprovalReference] = useState('');
  const [comments, setComments] = useState('');
  const [dispatchChannel, setDispatchChannel] = useState('ExternalPortal');
  const [dispatchReference, setDispatchReference] = useState('');
  const [dispatchEvidenceReference, setDispatchEvidenceReference] =
    useState('');
  const dispatchRequired =
    action === 'Approve' &&
    change?.changeType !== 'UnpublishedScheduleReschedule';

  const decide = async () => {
    if (!change || !approvalReference.trim()) return;
    const request: DecideProcurementTenderDocumentChangeRequest = {
      action,
      approvalReference,
      comments: comments || undefined,
      dispatchChannel: dispatchRequired
        ? dispatchChannel || undefined
        : undefined,
      dispatchReference: dispatchRequired
        ? dispatchReference || undefined
        : undefined,
      dispatchEvidenceReference: dispatchRequired
        ? dispatchEvidenceReference || undefined
        : undefined,
      rowVersion: change.rowVersion,
    };
    try {
      setBusy(true);
      await service.decideChange(change.id, request);
      toast.success(
        action === 'Approve'
          ? 'Governed change approved and dispatched'
          : 'Governed change rejected'
      );
      await onChanged();
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Decision failed');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {action}{' '}
            {change
              ? tenderDocumentChangeTypeLabel[change.changeType]
              : 'change'}
          </DialogTitle>
          <DialogDescription>
            The service verifies the exact workflow outcome, capability and SOD
            before changing the immutable register.
          </DialogDescription>
        </DialogHeader>
        <Field label="Approval or rejection reference">
          <Input
            value={approvalReference}
            onChange={(event) => setApprovalReference(event.target.value)}
          />
        </Field>
        <Field label="Comments">
          <Textarea
            value={comments}
            onChange={(event) => setComments(event.target.value)}
          />
        </Field>
        {dispatchRequired && (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Dispatch channel">
              <Input
                value={dispatchChannel}
                onChange={(event) => setDispatchChannel(event.target.value)}
              />
            </Field>
            <Field label="Dispatch reference">
              <Input
                value={dispatchReference}
                onChange={(event) => setDispatchReference(event.target.value)}
              />
            </Field>
            <div className="md:col-span-2">
              <Field label="Dispatch evidence reference">
                <Input
                  value={dispatchEvidenceReference}
                  onChange={(event) =>
                    setDispatchEvidenceReference(event.target.value)
                  }
                />
              </Field>
            </div>
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            variant={action === 'Reject' ? 'destructive' : 'default'}
            disabled={busy || !approvalReference.trim()}
            onClick={() => void decide()}
          >
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Confirm {action.toLowerCase()}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function TenderDocumentAcknowledgementDialog({
  open,
  onOpenChange,
  issuanceId,
  changeRecipientId,
  outcome,
  onChanged,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  issuanceId?: string;
  changeRecipientId?: string;
  outcome: ProcurementTenderDocumentAcknowledgementOutcome;
  onChanged: () => Promise<void>;
}) {
  const [busy, setBusy] = useState(false);
  const [channel, setChannel] = useState('ExternalPortal');
  const [reference, setReference] = useState('');
  const [evidence, setEvidence] = useState('');

  const acknowledge = async () => {
    const request: AcknowledgeProcurementTenderDocumentChangeRequest = {
      issuanceId,
      changeRecipientId,
      outcome,
      acknowledgementChannel: channel,
      acknowledgementReference: reference,
      evidenceReference: evidence,
    };
    try {
      setBusy(true);
      await service.acknowledge(request);
      toast.success(
        outcome === 'Acknowledged'
          ? 'Document receipt acknowledged'
          : 'Document change declined with evidence'
      );
      await onChanged();
      onOpenChange(false);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Acknowledgement failed'
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {outcome === 'Acknowledged'
              ? 'Acknowledge receipt'
              : 'Decline change'}
          </DialogTitle>
          <DialogDescription>
            This response is tied to the authenticated recipient and retained in
            the immutable register.
          </DialogDescription>
        </DialogHeader>
        <Field label="Acknowledgement channel">
          <Input
            value={channel}
            onChange={(event) => setChannel(event.target.value)}
          />
        </Field>
        <Field label="Acknowledgement reference">
          <Input
            value={reference}
            onChange={(event) => setReference(event.target.value)}
          />
        </Field>
        <Field label="Evidence reference">
          <Input
            value={evidence}
            onChange={(event) => setEvidence(event.target.value)}
          />
        </Field>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            variant={outcome === 'Declined' ? 'destructive' : 'default'}
            disabled={
              busy || !channel.trim() || !reference.trim() || !evidence.trim()
            }
            onClick={() => void acknowledge()}
          >
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Record response
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
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
