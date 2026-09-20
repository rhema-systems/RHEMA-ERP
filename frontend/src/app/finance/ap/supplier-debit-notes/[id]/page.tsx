'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import {
  ArrowLeft,
  Ban,
  Check,
  Pencil,
  FileCheck2,
  Loader2,
  RotateCcw,
  Send,
  X,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { formatCurrency } from '@/lib/utils';
import { accountsPayableService } from '@/services/accountsPayableService';
import type { SupplierDebitNoteStatus } from '@/types/ap';
import { SourceDocumentDimensionEvidence } from '@/components/finance/dimensions/source-document-dimension-panel';

function statusBadge(status: SupplierDebitNoteStatus, approvalRequired = true) {
  if (status === 'Posted')
    return <Badge className="bg-emerald-600">Posted</Badge>;
  if (status === 'Approved')
    return <Badge className="bg-blue-600">{approvalRequired ? 'Approved' : 'Ready to post'}</Badge>;
  if (status === 'PendingApproval')
    return <Badge className="bg-amber-600">Pending approval</Badge>;
  if (status === 'Rejected' || status === 'Cancelled')
    return <Badge variant="destructive">{status}</Badge>;
  if (status === 'Reversed') return <Badge variant="outline">Reversed</Badge>;
  return <Badge variant="secondary">Draft</Badge>;
}

export default function SupplierDebitNoteDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const [workingAction, setWorkingAction] = useState('');
  const [confirmation, setConfirmation] = useState<'Approve' | 'Reject' | 'Post' | 'Cancel' | 'Reverse' | 'Edit details' | null>(null);
  const [creditReference, setCreditReference] = useState('');
  const [creditDate, setCreditDate] = useState('');
  const [headerVersion, setHeaderVersion] = useState('');
  const [actionComment, setActionComment] = useState('');
  const [actionError, setActionError] = useState('');
  const {
    data: note,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['supplier-debit-note', id],
    queryFn: () => accountsPayableService.getSupplierDebitNote(id),
  });

  const run = async (label: string, action: () => Promise<unknown>) => {
    setWorkingAction(label);
    setActionError('');
    try {
      await action();
      toast({
        title: `${label} completed`,
        description: 'The supplier debit-note register has been refreshed.',
      });
      await refetch();
      return true;
    } catch (failure) {
      const data = (failure as { response?: { data?: { detail?: string; message?: string; code?: string } } })?.response?.data;
      const message = (data?.detail || data?.message || (failure instanceof Error ? failure.message : 'The action could not be completed.')) + (data?.code ? ` (${data.code})` : '');
      setActionError(message);
      toast({
        title: `${label} failed`,
        description: message,
        variant: 'destructive',
      });
      return false;
    } finally {
      setWorkingAction('');
    }
  };

  if (isLoading)
    return (
      <div className="mx-auto max-w-[1200px] space-y-4 p-8">
        <Skeleton className="h-12 w-1/2" />
        <Skeleton className="h-80 w-full" />
      </div>
    );
  if (!note || error)
    return (
      <div className="p-8">
        <p className="text-destructive">
          {error instanceof Error
            ? error.message
            : 'Supplier debit note was not found.'}
        </p>
        <Button
          variant="link"
          onClick={() => router.push('/finance/ap/supplier-debit-notes')}
        >
          Return to register
        </Button>
      </div>
    );

  const canManage = hasPermission('Finance.AP.SupplierDebitNotes.Manage');
  const canSubmit = hasPermission('Finance.AP.SupplierDebitNotes.Submit');
  const canApprove = hasPermission('Finance.AP.SupplierDebitNotes.Approve');
  const canPost = hasPermission('Finance.AP.SupplierDebitNotes.Post');
  const canReverse = hasPermission('Finance.AP.SupplierDebitNotes.Reverse');
  const busy = Boolean(workingAction);
  const approvalRequired = note.approvalRequired !== false;
  const inventoryCredit = !!note.inventoryPurchaseReturnId;
  const reasonRequired = confirmation === 'Reject' || confirmation === 'Cancel' || confirmation === 'Reverse';
  const ask = (action: NonNullable<typeof confirmation>) => {
    setActionComment(action === 'Edit details' ? note.reason || '' : ''); setActionError(''); setConfirmation(action);
    if (action === 'Edit details') {
      setCreditReference(note.supplierCreditNoteReference || ''); setCreditDate(note.debitNoteDate.slice(0, 10)); setHeaderVersion(note.rowVersion);
    }
  };
  const confirmAction = async () => {
    const comment = actionComment.trim();
    if (busy || (reasonRequired && !comment)) return false;
    switch (confirmation) {
      case 'Approve': return run('Approval', () => accountsPayableService.decideSupplierDebitNote(id, true, comment || undefined));
      case 'Reject': return run('Rejection', () => accountsPayableService.decideSupplierDebitNote(id, false, comment));
      case 'Post': return run('Posting', () => accountsPayableService.postSupplierDebitNote(id));
      case 'Cancel': return run('Cancellation', () => accountsPayableService.cancelSupplierDebitNote(id, comment));
      case 'Reverse': return run('Reversal', () => accountsPayableService.reverseSupplierDebitNote(id, comment));
      case 'Edit details':
        if (!creditReference.trim() || !creditDate || !headerVersion) return false;
        return run('Save', () => accountsPayableService.updateInventoryReturnCreditHeader(id, {
          supplierCreditNoteReference: creditReference.trim(), creditDate, reason: comment || undefined, rowVersion: headerVersion,
        }));
      default: return false;
    }
  };

  return (
    <div className="mx-auto max-w-[1200px] space-y-6 p-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <Button
            variant="outline"
            size="icon"
            onClick={() => router.push('/finance/ap/supplier-debit-notes')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold">{note.debitNoteNumber}</h1>
              {statusBadge(note.statusName, approvalRequired)}
            </div>
            <p className="text-muted-foreground">{note.vendorName}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {canManage && inventoryCredit && ['Draft', 'Rejected'].includes(note.statusName) && <Button variant="outline" size="icon" aria-label="Edit credit details" title="Edit credit details" disabled={busy} onClick={() => ask('Edit details')}><Pencil className="h-4 w-4" /></Button>}
          {canManage && !inventoryCredit && ['Draft', 'Rejected'].includes(note.statusName) && (
            <Button
              variant="outline"
              size="icon"
              aria-label="Edit"
              title="Edit"
              onClick={() =>
                router.push(`/finance/ap/supplier-debit-notes/${id}/edit`)
              }
            >
              <Pencil className="h-4 w-4" />
            </Button>
          )}
          {canSubmit && note.statusName === 'Draft' && (
            <Button
              disabled={busy}
              onClick={() =>
                run('Submission', () =>
                  accountsPayableService.submitSupplierDebitNote(id)
                )
              }
            >
              <Send className="mr-2 h-4 w-4" />
              {approvalRequired ? 'Submit for approval' : 'Continue'}
            </Button>
          )}
          {approvalRequired && canApprove && note.statusName === 'PendingApproval' && (
            <Button
              disabled={busy}
              onClick={() => ask('Approve')}
            >
              <Check className="mr-2 h-4 w-4" />
              Approve
            </Button>
          )}
          {approvalRequired && canApprove && note.statusName === 'PendingApproval' && (
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() => ask('Reject')}
            >
              <X className="mr-2 h-4 w-4" />
              Reject
            </Button>
          )}
          {canPost && note.statusName === 'Approved' && (
            <Button
              disabled={busy}
              onClick={() => ask('Post')}
            >
              <FileCheck2 className="mr-2 h-4 w-4" />
              Post
            </Button>
          )}
          {canManage && !inventoryCredit && ['Draft', 'Rejected'].includes(note.statusName) && (
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => ask('Cancel')}
            >
              <Ban className="mr-2 h-4 w-4" />
              Cancel
            </Button>
          )}
          {canReverse && !inventoryCredit &&
            note.statusName === 'Posted' &&
            note.appliedAmount === 0 && (
              <Button
                variant="destructive"
                disabled={busy}
                onClick={() => ask('Reverse')}
              >
                <RotateCcw className="mr-2 h-4 w-4" />
                Reverse
              </Button>
            )}
          {busy && (
            <Button variant="ghost" disabled>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              {workingAction}
            </Button>
          )}
        </div>
      </div>

      {actionError && !confirmation && <p role="alert" className="text-sm text-destructive">{actionError}</p>}
      <Alert>
        <AlertTitle>{inventoryCredit ? 'Credit against original invoice' : 'Supplier credit'}</AlertTitle>
        <AlertDescription>
          {inventoryCredit ? 'Post records the supplier credit and applies it to the original invoice. It does not move stock again.' : 'Post records the AP/GL credit. Apply it to an invoice through a vendor payment.'}
          {approvalRequired ? ' Approval is required before posting.' : ''}
        </AlertDescription>
      </Alert>

      <div className="no-print">
        <SourceDocumentDimensionEvidence evidence={note.financeDimensions} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Commercial credit</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-4">
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Supplier reference
              </p>
              <p className="mt-1 font-medium">
                {note.supplierCreditNoteReference ?? 'Not supplied'}
              </p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Debit-note date
              </p>
              <p className="mt-1 font-medium">
                {format(new Date(note.debitNoteDate), 'dd MMM yyyy')}
              </p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Original invoice
              </p>
              {note.originalVendorInvoiceId ? (
                <Button
                  variant="link"
                  className="h-auto p-0"
                  onClick={() =>
                    router.push(
                      `/finance/ap/invoices/${note.originalVendorInvoiceId}`
                    )
                  }
                >
                  {note.originalVendorInvoiceNumber}
                </Button>
              ) : (
                <p className="mt-1 font-medium">Standalone</p>
              )}
            </div>
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Exchange rate
              </p>
              <p className="mt-1 font-medium">
                {note.currencyCode} @ {note.exchangeRate.toFixed(6)}
              </p>
            </div>
            <div className="md:col-span-2">
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Reason
              </p>
              <p className="mt-1">{note.reason}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Application status
              </p>
              <p className="mt-1 font-medium">{note.applicationStatus}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Remaining credit
              </p>
              <p className="mt-1 text-lg font-bold">
                {formatCurrency(note.remainingAmount, note.currencyCode)}
              </p>
            </div>
          </div>
          {note.notes && (
            <>
              <Separator />
              <div>
                <p className="text-xs font-semibold uppercase text-muted-foreground">
                  Notes
                </p>
                <p className="mt-1 whitespace-pre-wrap">{note.notes}</p>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Lines and accounting source</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Description</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead className="text-right">Quantity</TableHead>
                  <TableHead className="text-right">Unit price</TableHead>
                  <TableHead className="text-right">Discount</TableHead>
                  <TableHead className="text-right">Tax</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {note.lineItems.map((line) => (
                  <TableRow key={line.id}>
                    <TableCell>{line.description}</TableCell>
                    <TableCell>
                      {line.originalVendorInvoiceLineItemId
                        ? 'Original invoice line'
                        : 'Standalone GL coding'}
                    </TableCell>
                    <TableCell className="text-right">
                      {line.quantity}
                    </TableCell>
                    <TableCell className="text-right">
                      {formatCurrency(line.unitPrice, note.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right">
                      {formatCurrency(line.discountAmount, note.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right">
                      {formatCurrency(line.taxAmount, note.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right font-medium">
                      {formatCurrency(line.lineTotal, note.currencyCode)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          <div className="ml-auto mt-5 grid max-w-sm gap-2 text-sm">
            <div className="flex justify-between">
              <span>Net amount</span>
              <strong>
                {formatCurrency(note.subTotal, note.currencyCode)}
              </strong>
            </div>
            <div className="flex justify-between">
              <span>Tax reversal</span>
              <strong>
                {formatCurrency(note.taxAmount, note.currencyCode)}
              </strong>
            </div>
            <div className="flex justify-between border-t pt-2 text-base">
              <span>Total</span>
              <strong>
                {formatCurrency(note.totalAmount, note.currencyCode)}
              </strong>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Invoice applications</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="mb-4 text-sm text-muted-foreground">
            {inventoryCredit ? 'This credit is applied directly to the original invoice when posted; no payment is created.' : 'Applications are recorded through vendor payments.'}
          </p>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Payment</TableHead>
                  <TableHead>Invoice</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead>State</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {inventoryCredit && note.directInvoiceAppliedAt && <TableRow>
                  <TableCell>Credit only</TableCell>
                  <TableCell><Button variant="link" className="h-auto p-0" onClick={() => router.push(`/finance/ap/invoices/${note.originalVendorInvoiceId}`)}>{note.originalVendorInvoiceNumber}</Button></TableCell>
                  <TableCell>{format(new Date(note.directInvoiceAppliedAt), 'dd MMM yyyy')}</TableCell>
                  <TableCell className="text-right">{formatCurrency(note.directInvoiceAppliedAmount || 0, note.currencyCode)}</TableCell>
                  <TableCell><Badge className="bg-emerald-600">Applied</Badge></TableCell>
                </TableRow>}
                {note.applications.length === 0 && !note.directInvoiceAppliedAt && (
                  <TableRow>
                    <TableCell
                      colSpan={5}
                      className="h-24 text-center text-muted-foreground"
                    >
                      No applications have been recorded.
                    </TableCell>
                  </TableRow>
                )}
                {note.applications.map((application) => (
                  <TableRow
                    key={application.id}
                    className={application.isReversal ? 'bg-amber-50/60' : ''}
                  >
                    <TableCell>
                      <Button
                        variant="link"
                        className="h-auto p-0"
                        onClick={() =>
                          router.push(
                            `/finance/ap/payments/${application.vendorPaymentId}`
                          )
                        }
                      >
                        {application.paymentNumber}
                      </Button>
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="link"
                        className="h-auto p-0"
                        onClick={() =>
                          router.push(
                            `/finance/ap/invoices/${application.vendorInvoiceId}`
                          )
                        }
                      >
                        {application.invoiceNumber}
                      </Button>
                    </TableCell>
                    <TableCell>
                      {format(
                        new Date(application.applicationDate),
                        'dd MMM yyyy'
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      {formatCurrency(
                        application.applicationAmount,
                        application.currencyCode
                      )}
                    </TableCell>
                    <TableCell>
                      {application.isReversal ? (
                        <Badge variant="outline">Reversal</Badge>
                      ) : application.appliedAt ? (
                        <Badge className="bg-emerald-600">
                          Posted with payment
                        </Badge>
                      ) : (
                        <Badge variant="secondary">
                          Reserved in draft payment
                        </Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {(note.journalEntryId ||
        note.reversalJournalEntryId ||
        note.workflowInstanceId) && (
        <Card>
          <CardHeader>
            <CardTitle>Control references</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3 text-sm md:grid-cols-2">
            {approvalRequired && note.workflowInstanceId && <div>
              <span className="text-muted-foreground">Workflow:</span>{' '}
              <span className="font-mono">
                {note.workflowInstanceId ?? '-'}
              </span>
            </div>}
            <div>
              <span className="text-muted-foreground">Posting event:</span>{' '}
              <span className="font-mono">{note.postingEventId ?? '-'}</span>
            </div>
            {note.journalEntryId && (
              <div>
                <span className="text-muted-foreground">Journal:</span>{' '}
                <Button
                  variant="link"
                  className="h-auto p-0 font-mono"
                  onClick={() =>
                    router.push(
                      `/finance/journal-entries/${note.journalEntryId}`
                    )
                  }
                >
                  {note.journalEntryId}
                </Button>
              </div>
            )}
            {note.reversalJournalEntryId && (
              <div>
                <span className="text-muted-foreground">Reversal journal:</span>{' '}
                <Button
                  variant="link"
                  className="h-auto p-0 font-mono"
                  onClick={() =>
                    router.push(
                      `/finance/journal-entries/${note.reversalJournalEntryId}`
                    )
                  }
                >
                  {note.reversalJournalEntryId}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
      <ConfirmationDialog open={!!confirmation} onOpenChange={open => { if (!open && !busy) setConfirmation(null); }}
        title={`${confirmation || 'Confirm'} supplier credit`} confirmText={confirmation === 'Edit details' ? 'Save' : confirmation || 'Confirm'}
        description={confirmation === 'Post' ? inventoryCredit ? `Post ${formatCurrency(note.totalAmount, note.currencyCode)} and apply the credit to invoice ${note.originalVendorInvoiceNumber}? Stock will not be changed again.` : `Post this ${formatCurrency(note.totalAmount, note.currencyCode)} supplier credit to AP and GL?` : `${confirmation || 'Confirm'} ${note.debitNoteNumber}.`}
        variant={reasonRequired ? 'destructive' : 'default'} isLoading={busy} confirmDisabled={(reasonRequired && !actionComment.trim()) || (confirmation === 'Edit details' && (!creditReference.trim() || !creditDate || !headerVersion))}
        onConfirm={confirmAction}>
        {confirmation === 'Edit details' && <div className="mb-4 grid gap-3"><div className="space-y-1"><Label htmlFor="credit-header-reference">Supplier credit reference</Label><Input id="credit-header-reference" value={creditReference} maxLength={100} disabled={busy} onChange={event => setCreditReference(event.target.value)} /></div><div className="space-y-1"><Label htmlFor="credit-header-date">Credit date</Label><Input id="credit-header-date" type="date" value={creditDate} disabled={busy} onChange={event => setCreditDate(event.target.value)} /></div></div>}
        {confirmation !== 'Post' && <div className="space-y-2"><Label htmlFor="credit-action-comment">{reasonRequired ? 'Reason' : 'Comments (optional)'}</Label><Textarea id="credit-action-comment" value={actionComment} disabled={busy} onChange={event => setActionComment(event.target.value)} /></div>}
        {actionError && <p role="alert" className="mt-2 text-sm text-destructive">{actionError}</p>}
      </ConfirmationDialog>
    </div>
  );
}
