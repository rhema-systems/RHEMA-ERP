'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import {
  ArrowLeft,
  Ban,
  Check,
  Edit,
  FileCheck2,
  Loader2,
  RotateCcw,
  Send,
  X,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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

function statusBadge(status: SupplierDebitNoteStatus) {
  if (status === 'Posted')
    return <Badge className="bg-emerald-600">Posted</Badge>;
  if (status === 'Approved')
    return <Badge className="bg-blue-600">Approved</Badge>;
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
    try {
      await action();
      toast({
        title: `${label} completed`,
        description: 'The supplier debit-note register has been refreshed.',
      });
      await refetch();
    } catch (actionError) {
      toast({
        title: `${label} failed`,
        description:
          actionError instanceof Error
            ? actionError.message
            : 'The action could not be completed.',
        variant: 'destructive',
      });
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
              {statusBadge(note.statusName)}
            </div>
            <p className="text-muted-foreground">{note.vendorName}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {canManage && ['Draft', 'Rejected'].includes(note.statusName) && (
            <Button
              variant="outline"
              onClick={() =>
                router.push(`/finance/ap/supplier-debit-notes/${id}/edit`)
              }
            >
              <Edit className="mr-2 h-4 w-4" />
              Edit
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
              Submit
            </Button>
          )}
          {canApprove && note.statusName === 'PendingApproval' && (
            <Button
              disabled={busy}
              onClick={() =>
                run('Approval', () =>
                  accountsPayableService.decideSupplierDebitNote(
                    id,
                    true,
                    window.prompt('Approval comments (optional)') ?? undefined
                  )
                )
              }
            >
              <Check className="mr-2 h-4 w-4" />
              Approve
            </Button>
          )}
          {canApprove && note.statusName === 'PendingApproval' && (
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() => {
                const reason = window.prompt('Rejection reason');
                if (reason?.trim())
                  void run('Rejection', () =>
                    accountsPayableService.decideSupplierDebitNote(
                      id,
                      false,
                      reason
                    )
                  );
              }}
            >
              <X className="mr-2 h-4 w-4" />
              Reject
            </Button>
          )}
          {canPost && note.statusName === 'Approved' && (
            <Button
              disabled={busy}
              onClick={() =>
                run('Posting', () =>
                  accountsPayableService.postSupplierDebitNote(id)
                )
              }
            >
              <FileCheck2 className="mr-2 h-4 w-4" />
              Post
            </Button>
          )}
          {canManage && ['Draft', 'Rejected'].includes(note.statusName) && (
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => {
                const reason = window.prompt('Cancellation reason');
                if (reason?.trim())
                  void run('Cancellation', () =>
                    accountsPayableService.cancelSupplierDebitNote(id, reason)
                  );
              }}
            >
              <Ban className="mr-2 h-4 w-4" />
              Cancel
            </Button>
          )}
          {canReverse &&
            note.statusName === 'Posted' &&
            note.appliedAmount === 0 && (
              <Button
                variant="destructive"
                disabled={busy}
                onClick={() => {
                  const reason = window.prompt('Reversal reason');
                  if (reason?.trim())
                    void run('Reversal', () =>
                      accountsPayableService.reverseSupplierDebitNote(
                        id,
                        reason
                      )
                    );
                }}
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

      <Alert>
        <AlertTitle>Controlled lifecycle</AlertTitle>
        <AlertDescription>
          Maker saves and submits; an independent reviewer approves; a
          purpose-authorised poster creates the AP/GL entry. Applications are
          made against a specific supplier invoice through a vendor payment and
          are shown below.
        </AlertDescription>
      </Alert>

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
                Approved rate snapshot
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
            Create applications from the relevant vendor payment. Reversal rows
            preserve history and neutralise their linked original application.
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
                {note.applications.length === 0 && (
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
            <div>
              <span className="text-muted-foreground">Workflow:</span>{' '}
              <span className="font-mono">
                {note.workflowInstanceId ?? '-'}
              </span>
            </div>
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
    </div>
  );
}
