'use client';

import {
  CalendarClock,
  FileCheck2,
  History,
  ReceiptText,
  ShieldAlert,
  Users,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  hasAnyTenderDocumentAction,
  pendingMandatoryAcknowledgements,
  procurementMethodLabel,
  tenderDocumentChangeStatusLabel,
  tenderDocumentChangeTypeLabel,
} from '@/lib/procurement-tender-document';
import type {
  ProcurementTenderDocumentAcknowledgementOutcome,
  ProcurementTenderDocumentChange,
  ProcurementTenderDocumentReadiness,
  ProcurementTenderDocumentRegister as Register,
} from '@/types/procurement-tender-document';

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

const formatMoney = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);

export function TenderDocumentRegister({
  readiness,
  register,
  external = false,
  canApprove = false,
  onDecision,
  onAcknowledge,
}: {
  readiness: ProcurementTenderDocumentReadiness;
  register?: Register;
  external?: boolean;
  canApprove?: boolean;
  onDecision?: (
    change: ProcurementTenderDocumentChange,
    action: 'Approve' | 'Reject'
  ) => void;
  onAcknowledge?: (
    target: { issuanceId?: string; changeRecipientId?: string },
    outcome: ProcurementTenderDocumentAcknowledgementOutcome
  ) => void;
}) {
  const blockedReasons = register?.blockedReasons ?? readiness.blockedReasons;

  return (
    <div className="space-y-6">
      <div className="grid gap-3 md:grid-cols-4">
        <Summary
          label="Source"
          value={`${readiness.sourceType === 'Tender' ? 'Tender' : 'RFQ'} · ${readiness.sourceReference}`}
        />
        <Summary
          label="Method"
          value={
            readiness.method
              ? procurementMethodLabel[readiness.method] ?? readiness.method
              : 'Not resolved'
          }
        />
        <Summary
          label="Issued / pending acknowledgement"
          value={`${readiness.issuanceCount} / ${readiness.pendingAcknowledgementCount}`}
        />
        <Summary
          label="Readiness"
          value={readiness.ready ? 'Ready' : 'Blocked'}
        />
      </div>

      {blockedReasons.length > 0 && (
        <Alert>
          <ShieldAlert className="h-4 w-4" />
          <AlertTitle>Controlled readiness is blocked</AlertTitle>
          <AlertDescription>
            <ul className="list-disc space-y-1 pl-4">
              {blockedReasons.map((reason) => (
                <li key={reason}>{reason}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {!register ? (
        <Card>
          <CardContent className="flex items-center gap-3 p-6 text-sm">
            <FileCheck2 className="h-5 w-5 text-muted-foreground" />
            No controlled register is bound yet. An authorized officer must
            select the exact Published/effective version and freeze issue terms
            before document dispatch.
          </CardContent>
        </Card>
      ) : (
        <>
          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Exact document and policy lineage</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <Line
                  label="Effective document"
                  value={register.effectiveTemplateReference}
                />
                <Line
                  label="Method rule"
                  value={`${register.methodRuleCode} · ${register.methodRuleId}`}
                />
                <Line
                  label="Policy"
                  value={`${register.policySetCode} · v${register.policySetVersion}`}
                />
                <Line
                  label="Source configuration"
                  value={register.sourceConfigurationProfileId}
                />
                <Line
                  label="Register integrity"
                  value={`${register.integrityHash.slice(0, 18)}…`}
                />
                <Line label="Correlation" value={register.correlationId} />
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>Effective controlled dates and fee</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <Line
                  label="Original submission deadline"
                  value={formatDate(register.originalSubmissionDeadlineUtc)}
                />
                <Line
                  label="Effective submission deadline"
                  value={formatDate(register.effectiveSubmissionDeadlineUtc)}
                />
                <Line
                  label="Original bid validity"
                  value={formatDate(register.originalBidValidityUntilUtc)}
                />
                <Line
                  label="Effective bid validity"
                  value={formatDate(register.effectiveBidValidityUntilUtc)}
                />
                <Line
                  label="Issue terms"
                  value={
                    register.feeMode === 'Free'
                      ? `Free · ${register.currencyCode}`
                      : formatMoney(register.feeAmount, register.currencyCode)
                  }
                />
              </CardContent>
            </Card>
          </div>

          <Card data-testid="tender-document-issuance-history">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <ReceiptText className="h-5 w-5" /> Immutable issue and receipt
                history
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Recipient</TableHead>
                      <TableHead>Exact version</TableHead>
                      <TableHead>Fee / receipt</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>Acknowledgement</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {register.issuances.map((issue) => (
                        <TableRow key={issue.id}>
                          <TableCell>
                            <div className="font-medium">
                              {issue.recipientName}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {issue.recipientEmail ||
                                issue.recipientPhone ||
                                issue.recipientKey}
                            </div>
                          </TableCell>
                          <TableCell>{issue.templateReference}</TableCell>
                          <TableCell>
                            <div>
                              {issue.feeMode === 'Free'
                                ? 'Free'
                                : formatMoney(
                                    issue.amountPaid,
                                    issue.currencyCode
                                  )}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {issue.receiptNumber}
                              {!external && issue.paymentReference
                                ? ` · ${issue.paymentReference}`
                                : ''}
                            </div>
                          </TableCell>
                          <TableCell>
                            {formatDate(issue.issuedAtUtc)}
                            <div className="text-xs text-muted-foreground">
                              {issue.issueChannel}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {issue.correlationId}
                            </div>
                          </TableCell>
                          <TableCell>
                            {issue.acknowledgement ? (
                              <Badge
                                variant={
                                  issue.acknowledgement.outcome ===
                                  'Acknowledged'
                                    ? 'default'
                                    : 'destructive'
                                }
                              >
                                {issue.acknowledgement.outcome}
                              </Badge>
                            ) : (
                              <Badge variant="outline">Pending</Badge>
                            )}
                            {issue.acknowledgement && (
                              <div className="mt-1 text-xs text-muted-foreground">
                                {issue.acknowledgement.acknowledgementReference}
                              </div>
                            )}
                          </TableCell>
                        </TableRow>
                    ))}
                    {register.issuances.length === 0 && (
                      <TableRow>
                        <TableCell
                          colSpan={5}
                          className="py-8 text-center text-muted-foreground"
                        >
                          No issue or sale records exist.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          <Card data-testid="tender-document-change-history">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CalendarClock className="h-5 w-5" /> Addenda and extension
                history
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {register.changes.map((change) => {
                const pending = pendingMandatoryAcknowledgements(change);
                const canApproveChange =
                  canApprove &&
                  change.status === 'PendingApproval' &&
                  hasAnyTenderDocumentAction(change.allowedActions, [
                    'Approve',
                    'ApproveChange',
                    'DecideChange',
                  ]);
                const canRejectChange =
                  canApprove &&
                  change.status === 'PendingApproval' &&
                  hasAnyTenderDocumentAction(change.allowedActions, [
                    'Reject',
                    'RejectChange',
                    'DecideChange',
                  ]);
                return (
                  <div
                    key={change.id}
                    className="space-y-3 rounded-lg border p-4"
                  >
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="font-medium">
                          #{change.sequence} ·{' '}
                          {tenderDocumentChangeTypeLabel[change.changeType]}
                        </p>
                        <p className="text-sm text-muted-foreground">
                          {change.reason}
                        </p>
                      </div>
                      <div className="flex items-center gap-2">
                        <Badge
                          variant={
                            change.status === 'Approved'
                              ? 'default'
                              : change.status === 'Rejected'
                                ? 'destructive'
                                : 'secondary'
                          }
                        >
                          {tenderDocumentChangeStatusLabel[change.status]}
                        </Badge>
                        {pending > 0 && (
                          <Badge variant="outline">
                            {pending} acknowledgement
                            {pending === 1 ? '' : 's'} pending
                          </Badge>
                        )}
                      </div>
                    </div>

                    <div className="grid gap-2 text-sm md:grid-cols-3">
                      <Line
                        label="Requested"
                        value={formatDate(change.requestedAtUtc)}
                      />
                      <Line
                        label="Workflow"
                        value={
                          change.workflowInstanceId ??
                          change.workflowDefinitionId
                        }
                      />
                      <Line
                        label="Approval"
                        value={change.approvalReference}
                      />
                      <Line label="Correlation" value={change.correlationId} />
                      <Line label="Evidence" value={change.evidenceReference} />
                      <Line
                        label="Integrity"
                        value={`${change.integrityHash.slice(0, 18)}…`}
                      />
                      {change.newTemplateVersionId && (
                        <Line
                          label="New approved version"
                          value={change.newTemplateVersionId}
                        />
                      )}
                      {change.previousValueUtc && (
                        <Line
                          label="Previous value"
                          value={formatDate(change.previousValueUtc)}
                        />
                      )}
                      {change.newValueUtc && (
                        <Line
                          label="New value"
                          value={formatDate(change.newValueUtc)}
                        />
                      )}
                    </div>

                    {change.blockedReasons.length > 0 && (
                      <Alert>
                        <ShieldAlert className="h-4 w-4" />
                        <AlertDescription>
                          {change.blockedReasons.join(' · ')}
                        </AlertDescription>
                      </Alert>
                    )}

                    {change.recipients.length > 0 && (
                      <div className="overflow-x-auto rounded border">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Recipient</TableHead>
                              <TableHead>Dispatch</TableHead>
                              <TableHead>Response</TableHead>
                              <TableHead />
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {change.recipients.map((recipient) => {
                              const canRespond =
                                external &&
                                !recipient.acknowledgement &&
                                hasAnyTenderDocumentAction(
                                  register.allowedActions,
                                  [
                                    'Acknowledge',
                                    'AcknowledgeChange',
                                    'DeclineChange',
                                  ]
                                );
                              return (
                                <TableRow key={recipient.id}>
                                  <TableCell>{recipient.recipientName}</TableCell>
                                  <TableCell>
                                    {recipient.dispatchChannel} ·{' '}
                                    {recipient.dispatchReference}
                                    <div className="text-xs text-muted-foreground">
                                      {formatDate(recipient.dispatchedAtUtc)}
                                    </div>
                                  </TableCell>
                                  <TableCell>
                                    {recipient.acknowledgement ? (
                                      <Badge
                                        variant={
                                          recipient.acknowledgement.outcome ===
                                          'Acknowledged'
                                            ? 'default'
                                            : 'destructive'
                                        }
                                      >
                                        {recipient.acknowledgement.outcome}
                                      </Badge>
                                    ) : (
                                      <Badge variant="outline">Pending</Badge>
                                    )}
                                    {recipient.acknowledgement && (
                                      <div className="mt-1 text-xs text-muted-foreground">
                                        {
                                          recipient.acknowledgement
                                            .acknowledgementReference
                                        }
                                      </div>
                                    )}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {canRespond && onAcknowledge && (
                                      <div className="flex justify-end gap-2">
                                        <Button
                                          size="sm"
                                          onClick={() =>
                                            onAcknowledge(
                                              {
                                                changeRecipientId: recipient.id,
                                              },
                                              'Acknowledged'
                                            )
                                          }
                                        >
                                          Acknowledge
                                        </Button>
                                        <Button
                                          size="sm"
                                          variant="outline"
                                          onClick={() =>
                                            onAcknowledge(
                                              {
                                                changeRecipientId: recipient.id,
                                              },
                                              'Declined'
                                            )
                                          }
                                        >
                                          Decline
                                        </Button>
                                      </div>
                                    )}
                                  </TableCell>
                                </TableRow>
                              );
                            })}
                          </TableBody>
                        </Table>
                      </div>
                    )}

                    {(canApproveChange || canRejectChange) &&
                      onDecision && (
                        <div className="flex gap-2">
                          {canApproveChange && (
                            <Button
                              size="sm"
                              onClick={() => onDecision(change, 'Approve')}
                            >
                              Approve exact workflow outcome
                            </Button>
                          )}
                          {canRejectChange && (
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => onDecision(change, 'Reject')}
                            >
                              Reject
                            </Button>
                          )}
                        </div>
                      )}
                  </div>
                );
              })}
              {register.changes.length === 0 && (
                <div className="flex items-center gap-3 p-4 text-sm text-muted-foreground">
                  <History className="h-5 w-5" /> No addenda or extension
                  records exist.
                </div>
              )}
            </CardContent>
          </Card>

          <Alert>
            <Users className="h-4 w-4" />
            <AlertTitle>
              {external
                ? 'Recipient-safe projection'
                : 'Append-only retained history'}
            </AlertTitle>
            <AlertDescription>
              {external
                ? 'Only records owned by the authenticated business partner are returned. Other recipients and payment references are never displayed.'
                : 'Issued versions, fee receipts, addenda, extensions, workflow outcomes, dispatches, acknowledgements and integrity hashes remain available without edit or delete actions.'}
            </AlertDescription>
          </Alert>
        </>
      )}
    </div>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs uppercase text-muted-foreground">{label}</p>
        <p className="mt-1 break-words font-medium">{value}</p>
      </CardContent>
    </Card>
  );
}

function Line({ label, value }: { label: string; value?: string }) {
  return (
    <div>
      <span className="text-muted-foreground">{label}: </span>
      <span className="break-all">{value || '—'}</span>
    </div>
  );
}
