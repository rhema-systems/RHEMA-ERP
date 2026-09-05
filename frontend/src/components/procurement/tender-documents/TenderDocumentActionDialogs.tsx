'use client';

import { useEffect, useState } from 'react';
import { CalendarClock, FilePlus2, Loader2, ReceiptText } from 'lucide-react';
import { toast } from 'sonner';

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
  const [issueOpen, setIssueOpen] = useState(false);
  const [changeOpen, setChangeOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [templateVersionId, setTemplateVersionId] = useState('');
  const [submissionDeadline, setSubmissionDeadline] = useState('');
  const [openingAt, setOpeningAt] = useState('');
  const [bidValidity, setBidValidity] = useState('');
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
  const [requiresAcknowledgement, setRequiresAcknowledgement] = useState(true);
  const [workflowDefinitionId, setWorkflowDefinitionId] = useState('');
  const [reason, setReason] = useState('');
  const [changeEvidence, setChangeEvidence] = useState('');

  useEffect(() => {
    setSubmissionDeadline(
      localInput(readiness.effectiveSubmissionDeadlineUtc)
    );
    setBidValidity(localInput(readiness.effectiveBidValidityUntilUtc));
    setFeeMode(readiness.feeMode ?? 'Free');
    setFeeAmount(readiness.feeAmount ?? 0);
    setCurrencyCode(readiness.currencyCode ?? 'GHS');
  }, [readiness]);

  useEffect(() => {
    if (!register) return;
    setIssue((current) => ({
      ...current,
      amountPaid: register.feeMode === 'Paid' ? register.feeAmount : 0,
    }));
  }, [register]);

  useEffect(() => {
    setTemplateVersionId(current => suggestedTenderDocumentSelection(
      current, readiness.effectiveTemplateVersionId, approvedTemplates
    ));
  }, [readiness.effectiveTemplateVersionId, approvedTemplates]);

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      setBusy(true);
      await action();
      toast.success(success);
      await onChanged();
      return true;
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Controlled action failed'
      );
      return false;
    } finally {
      setBusy(false);
    }
  };

  const bind = async () => {
    if (
      !templateVersionId ||
      !submissionDeadline ||
      !bidValidity ||
      !currencyCode.trim()
    ) {
      toast.error(
        'Select an approved version and complete deadline, validity, and currency.'
      );
      return;
    }
    if (feeMode === 'Paid' && feeAmount <= 0) {
      toast.error('Paid document binding requires a positive fee.');
      return;
    }
    const completed = await run(
      () =>
        service.bind({
          sourceType: readiness.sourceType,
          sourceId: readiness.sourceId,
          templateVersionId,
          submissionDeadlineUtc: new Date(submissionDeadline).toISOString(),
          openingScheduledAtUtc: openingAt
            ? new Date(openingAt).toISOString()
            : undefined,
          bidValidityUntilUtc: new Date(bidValidity).toISOString(),
          feeMode,
          feeAmount: feeMode === 'Free' ? 0 : feeAmount,
          currencyCode: currencyCode.toUpperCase(),
        }),
      'Exact approved tender-document version bound'
    );
    if (completed) setBindOpen(false);
  };

  const issueDocument = async () => {
    if (!register) return;
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
      return;
    }
    const completed = await run(
      () => service.issue(request),
      'Immutable document issue recorded'
    );
    if (completed) {
      setIssueOpen(false);
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
    }
  };

  const createChange = async () => {
    if (!register) return;
    const request = {
      sourceType: register.sourceType,
      sourceId: register.sourceId,
      changeType,
      newTemplateVersionId:
        changeType === 'Addendum' ? newTemplateVersionId || undefined : undefined,
      newValueUtc:
        changeType === 'Addendum' || !newValueUtc
          ? undefined
          : new Date(newValueUtc).toISOString(),
      requiresAcknowledgement,
      reason,
      workflowDefinitionId,
      evidenceReference: changeEvidence,
      registerRowVersion: register.rowVersion,
    };
    const validation = validateTenderDocumentChange(request, register);
    if (validation) {
      toast.error(validation);
      return;
    }
    const completed = await run(
      () => service.createChange(request),
      `${tenderDocumentChangeTypeLabel[changeType]} submitted for approval`
    );
    if (completed) {
      setChangeOpen(false);
      setReason('');
      setChangeEvidence('');
      setNewValueUtc('');
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
  const issueAllowed =
    canManage &&
    Boolean(register) &&
    hasAnyTenderDocumentAction(register?.allowedActions, [
      'Issue',
      'IssueDocument',
    ]);
  const changeAllowed =
    canManage &&
    Boolean(register) &&
    hasAnyTenderDocumentAction(register?.allowedActions, [
      'CreateChange',
      'CreateAddendum',
      'ExtendSubmissionDeadline',
      'ExtendBidValidity',
    ]);
  const effectiveTemplate = approvedTemplates.find(
    (template) => template.id === register?.effectiveTemplateVersionId
  );

  return (
    <>
      <div className="flex flex-wrap gap-2">
        {bindAllowed && (
          <Button onClick={() => setBindOpen(true)}>
            <FilePlus2 className="mr-2 h-4 w-4" /> Bind approved version
          </Button>
        )}
        {issueAllowed && (
          <Button onClick={() => setIssueOpen(true)}>
            <ReceiptText className="mr-2 h-4 w-4" /> Issue document
          </Button>
        )}
        {changeAllowed && (
          <Button variant="outline" onClick={() => setChangeOpen(true)}>
            <CalendarClock className="mr-2 h-4 w-4" /> Governed change
          </Button>
        )}
      </div>

      <Dialog open={bindOpen} onOpenChange={setBindOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Bind exact approved document version</DialogTitle>
            <DialogDescription>
              The binding freezes policy, method, content checksum, deadline,
              bid validity, and paid-or-free issue terms for this source.
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
            <Field label="Submission deadline">
              <Input
                type="datetime-local"
                value={submissionDeadline}
                onChange={(event) => setSubmissionDeadline(event.target.value)}
              />
            </Field>
            <Field label="Opening scheduled">
              <Input
                type="datetime-local"
                value={openingAt}
                onChange={(event) => setOpeningAt(event.target.value)}
              />
            </Field>
            <Field label="Bid validity until">
              <Input
                type="datetime-local"
                value={bidValidity}
                onChange={(event) => setBidValidity(event.target.value)}
              />
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
          <DialogFooter>
            <Button variant="outline" onClick={() => setBindOpen(false)}>
              Cancel
            </Button>
            <Button disabled={busy} onClick={() => void bind()}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Bind immutable version
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={issueOpen} onOpenChange={setIssueOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Record controlled issue or sale</DialogTitle>
            <DialogDescription>
              One exact version, recipient, fee decision, payment/receipt,
              channel and evidence record will be retained immutably.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Business-partner ID">
              <Input
                value={issue.businessPartnerId}
                onChange={(event) =>
                  setIssue({ ...issue, businessPartnerId: event.target.value })
                }
              />
            </Field>
            <Field label="Recipient name">
              <Input
                value={issue.recipientName}
                onChange={(event) =>
                  setIssue({ ...issue, recipientName: event.target.value })
                }
              />
            </Field>
            <Field label="Recipient email">
              <Input
                type="email"
                value={issue.recipientEmail}
                onChange={(event) =>
                  setIssue({ ...issue, recipientEmail: event.target.value })
                }
              />
            </Field>
            <Field label="Recipient phone">
              <Input
                value={issue.recipientPhone}
                onChange={(event) =>
                  setIssue({ ...issue, recipientPhone: event.target.value })
                }
              />
            </Field>
            <Field label="Amount paid">
              <Input
                type="number"
                min={0}
                value={issue.amountPaid}
                onChange={(event) =>
                  setIssue({ ...issue, amountPaid: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Payment reference">
              <Input
                value={issue.paymentReference}
                onChange={(event) =>
                  setIssue({ ...issue, paymentReference: event.target.value })
                }
              />
            </Field>
            <Field label="Receipt number">
              <Input
                value={issue.receiptNumber}
                onChange={(event) =>
                  setIssue({ ...issue, receiptNumber: event.target.value })
                }
              />
            </Field>
            <Field label="Issue channel">
              <Select
                value={issue.issueChannel}
                onValueChange={(issueChannel) =>
                  setIssue({ ...issue, issueChannel })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ExternalPortal">External portal</SelectItem>
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
                  value={issue.evidenceReference}
                  onChange={(event) =>
                    setIssue({ ...issue, evidenceReference: event.target.value })
                  }
                />
              </Field>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIssueOpen(false)}>
              Cancel
            </Button>
            <Button disabled={busy} onClick={() => void issueDocument()}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record immutable issue
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={changeOpen} onOpenChange={setChangeOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Create governed document change</DialogTitle>
            <DialogDescription>
              Approval, evidence, affected recipients, dispatch and mandatory
              acknowledgements remain part of the immutable register.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
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
                  ).map((type) => (
                    <SelectItem key={type} value={type}>
                      {tenderDocumentChangeTypeLabel[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label="Exact Published workflow">
              <Select
                value={workflowDefinitionId}
                onValueChange={setWorkflowDefinitionId}
              >
                <SelectTrigger>
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
                  changeType === 'SubmissionDeadlineExtension'
                    ? 'New submission deadline'
                    : 'New bid-validity date'
                }
              >
                <Input
                  type="datetime-local"
                  value={newValueUtc}
                  onChange={(event) => setNewValueUtc(event.target.value)}
                />
              </Field>
            )}
            <label className="flex items-center gap-2 rounded border p-3 text-sm">
              <Checkbox
                checked={requiresAcknowledgement}
                onCheckedChange={(checked) =>
                  setRequiresAcknowledgement(Boolean(checked))
                }
              />
              Mandatory recipient acknowledgement
            </label>
            <div className="md:col-span-2">
              <Field label="Governed reason">
                <Textarea
                  rows={3}
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </Field>
            </div>
            <div className="md:col-span-2">
              <Field label="Shared approval evidence reference">
                <Input
                  value={changeEvidence}
                  onChange={(event) => setChangeEvidence(event.target.value)}
                />
              </Field>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setChangeOpen(false)}>
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
  const [dispatchEvidenceReference, setDispatchEvidenceReference] = useState('');

  const decide = async () => {
    if (!change || !approvalReference.trim()) return;
    const request: DecideProcurementTenderDocumentChangeRequest = {
      action,
      approvalReference,
      comments: comments || undefined,
      dispatchChannel:
        action === 'Approve' ? dispatchChannel || undefined : undefined,
      dispatchReference:
        action === 'Approve' ? dispatchReference || undefined : undefined,
      dispatchEvidenceReference:
        action === 'Approve'
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
            {action} {change ? tenderDocumentChangeTypeLabel[change.changeType] : 'change'}
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
        {action === 'Approve' && (
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
            disabled={
              busy || !approvalReference.trim()
            }
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
            {outcome === 'Acknowledged' ? 'Acknowledge receipt' : 'Decline change'}
          </DialogTitle>
          <DialogDescription>
            This response is tied to the authenticated recipient and retained
            in the immutable register.
          </DialogDescription>
        </DialogHeader>
        <Field label="Acknowledgement channel">
          <Input value={channel} onChange={(event) => setChannel(event.target.value)} />
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
