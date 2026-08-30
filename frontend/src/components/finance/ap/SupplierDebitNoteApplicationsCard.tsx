'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { AlertCircle, CreditCard, Loader2, RotateCcw } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import {
  effectiveSupplierDebitNoteApplications,
  supplierDebitNoteApplicationLimit,
} from '@/lib/finance/supplier-debit-note';
import { formatCurrency } from '@/lib/utils';
import { accountsPayableService } from '@/services/accountsPayableService';
import type {
  OutstandingVendorInvoice,
  SupplierDebitNote,
  SupplierDebitNoteApplication,
  VendorPayment,
} from '@/types/ap';

interface SupplierDebitNoteApplicationsCardProps {
  payment: VendorPayment;
  canProcess: boolean;
  onChanged?: () => void | Promise<unknown>;
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message ? error.message : fallback;
}

function sameCurrency(left?: string, right?: string): boolean {
  return (
    (left ?? '').trim().toUpperCase() === (right ?? '').trim().toUpperCase()
  );
}

export function filterSupplierCreditApplicationCandidates(
  notes: SupplierDebitNote[],
  invoices: OutstandingVendorInvoice[],
  selectedNoteId = '',
  selectedInvoiceId = ''
) {
  const eligibleNotes = notes.filter(
    (note) => note.statusName === 'Posted' && Number(note.remainingAmount) > 0
  );
  const eligibleInvoices = invoices.filter(
    (invoice) => Number(invoice.balanceAmount) > 0
  );
  const selectedNote = eligibleNotes.find((note) => note.id === selectedNoteId);
  const selectedInvoice = eligibleInvoices.find(
    (invoice) => invoice.invoiceId === selectedInvoiceId
  );

  return {
    notes: selectedInvoice
      ? eligibleNotes.filter((note) =>
          sameCurrency(note.currencyCode, selectedInvoice.currencyCode)
        )
      : eligibleNotes,
    invoices: selectedNote
      ? eligibleInvoices.filter((invoice) =>
          sameCurrency(invoice.currencyCode, selectedNote.currencyCode)
        )
      : eligibleInvoices,
    selectedNote,
    selectedInvoice,
    currenciesCompatible:
      !selectedNote ||
      !selectedInvoice ||
      sameCurrency(selectedNote.currencyCode, selectedInvoice.currencyCode),
  };
}

export function SupplierDebitNoteApplicationsCard({
  payment,
  canProcess,
  onChanged,
}: SupplierDebitNoteApplicationsCardProps) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const canEdit =
    canProcess && payment.status === 'Draft' && !payment.paymentBatchId;
  const [selectedNoteId, setSelectedNoteId] = useState('');
  const [selectedInvoiceId, setSelectedInvoiceId] = useState('');
  const [amount, setAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [reversalTarget, setReversalTarget] =
    useState<SupplierDebitNoteApplication | null>(null);
  const [reversalReason, setReversalReason] = useState('');

  const applicationsQuery = useQuery({
    queryKey: ['supplier-debit-note-applications', payment.id],
    queryFn: () =>
      accountsPayableService.getSupplierDebitNoteApplications(payment.id),
  });

  const debitNotesQuery = useQuery({
    queryKey: [
      'supplier-debit-notes',
      'payment-application',
      payment.supplierId,
    ],
    queryFn: () =>
      accountsPayableService.getSupplierDebitNotes({
        supplierId: payment.supplierId,
        status: 'Posted',
      }),
    enabled: canEdit,
  });

  const invoicesQuery = useQuery({
    queryKey: ['outstanding-vendor-invoices', payment.supplierId],
    queryFn: () =>
      accountsPayableService.getOutstandingInvoices(payment.supplierId),
    enabled: canEdit,
  });

  const history = applicationsQuery.data ?? [];
  const effectiveApplications = useMemo(
    () => effectiveSupplierDebitNoteApplications(history),
    [history]
  );
  const reversedOriginalIds = useMemo(
    () =>
      new Set(
        history
          .filter((item) => item.isReversal && item.originalApplicationId)
          .map((item) => item.originalApplicationId as string)
      ),
    [history]
  );
  const effectiveCreditTotals = useMemo(
    () =>
      Array.from(
        effectiveApplications
          .reduce((totals, item) => {
            const currency = item.currencyCode.trim().toUpperCase();
            totals.set(
              currency,
              (totals.get(currency) ?? 0) + Number(item.applicationAmount)
            );
            return totals;
          }, new Map<string, number>())
          .entries()
      ).sort(([left], [right]) => left.localeCompare(right)),
    [effectiveApplications]
  );
  const candidates = useMemo(
    () =>
      filterSupplierCreditApplicationCandidates(
        debitNotesQuery.data ?? [],
        invoicesQuery.data ?? [],
        selectedNoteId,
        selectedInvoiceId
      ),
    [
      debitNotesQuery.data,
      invoicesQuery.data,
      selectedInvoiceId,
      selectedNoteId,
    ]
  );
  const availableNotes = candidates.notes;
  const availableInvoices = candidates.invoices;
  const selectedNote = candidates.selectedNote;
  const selectedInvoice = candidates.selectedInvoice;
  const applicationCurrency =
    selectedNote?.currencyCode ??
    selectedInvoice?.currencyCode ??
    payment.currencyCode;
  const applicationLimit = supplierDebitNoteApplicationLimit(
    Number(selectedInvoice?.balanceAmount ?? 0),
    Number(selectedNote?.remainingAmount ?? 0)
  );
  const numericAmount = Number(amount);
  const amountIsValid =
    candidates.currenciesCompatible &&
    Number.isFinite(numericAmount) &&
    numericAmount > 0 &&
    numericAmount <= applicationLimit;

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['supplier-debit-note-applications', payment.id],
      }),
      queryClient.invalidateQueries({ queryKey: ['supplier-debit-notes'] }),
      queryClient.invalidateQueries({
        queryKey: ['outstanding-vendor-invoices', payment.supplierId],
      }),
      queryClient.invalidateQueries({
        queryKey: ['vendor-payment', payment.id],
      }),
    ]);
    await onChanged?.();
  };

  const applyMutation = useMutation({
    mutationFn: () =>
      accountsPayableService.applySupplierDebitNotes(payment.id, [
        {
          supplierDebitNoteId: selectedNoteId,
          vendorInvoiceId: selectedInvoiceId,
          applicationAmount: numericAmount,
          notes: notes.trim() || undefined,
        },
      ]),
    onSuccess: async () => {
      toast({
        title: 'Supplier credit reserved',
        description:
          'The posted debit note is now linked to this draft payment and invoice.',
      });
      setSelectedNoteId('');
      setSelectedInvoiceId('');
      setAmount('');
      setNotes('');
      await refresh();
    },
    onError: (error) =>
      toast({
        title: 'Unable to reserve supplier credit',
        description: errorMessage(
          error,
          'The supplier debit-note application could not be saved.'
        ),
        variant: 'destructive',
      }),
  });

  const reverseMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      accountsPayableService.reverseSupplierDebitNoteApplication(id, reason),
    onSuccess: async () => {
      toast({
        title: 'Reservation released',
        description:
          'An immutable reversal row released the unposted supplier credit.',
      });
      setReversalTarget(null);
      setReversalReason('');
      await refresh();
    },
    onError: (error) =>
      toast({
        title: 'Unable to release reservation',
        description: errorMessage(
          error,
          'The supplier debit-note application could not be reversed.'
        ),
        variant: 'destructive',
      }),
  });

  const setSuggestedAmount = (noteId: string, invoiceId: string) => {
    const note = availableNotes.find((item) => item.id === noteId);
    const invoice = availableInvoices.find(
      (item) => item.invoiceId === invoiceId
    );
    const limit = supplierDebitNoteApplicationLimit(
      Number(invoice?.balanceAmount ?? 0),
      Number(note?.remainingAmount ?? 0)
    );
    setAmount(limit > 0 ? limit.toFixed(2) : '');
  };

  const loading =
    applicationsQuery.isLoading ||
    (canEdit && (debitNotesQuery.isLoading || invoicesQuery.isLoading));
  const loadError =
    applicationsQuery.error ?? debitNotesQuery.error ?? invoicesQuery.error;

  return (
    <>
      <Card className="no-print">
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle className="flex items-center gap-2">
                <CreditCard className="h-5 w-5" /> Supplier credit applications
              </CardTitle>
              <p className="mt-1 text-sm text-muted-foreground">
                Reserve posted supplier debit notes against this payment&apos;s
                invoices before authorization.
              </p>
            </div>
            <div className="text-right">
              <p className="text-xs font-semibold uppercase text-muted-foreground">
                Effective credit by currency
              </p>
              {effectiveCreditTotals.length > 0 ? (
                effectiveCreditTotals.map(([currency, total]) => (
                  <p key={currency} className="font-semibold">
                    {formatCurrency(total, currency)}
                  </p>
                ))
              ) : (
                <p className="font-semibold">None</p>
              )}
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          {/* Finance owns this commercial AP settlement evidence. It is not proof of a
              Procurement or Inventory physical-return event (FIN-INT-012/013 remain separate). */}
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>No duplicate journal</AlertTitle>
            <AlertDescription>
              The debit note already reduced AP when it was posted. This link
              reserves the credit for invoice settlement and remittance
              evidence; the payment posting finalizes the application.
            </AlertDescription>
          </Alert>

          {loading && (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          )}
          {loadError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Supplier credits unavailable</AlertTitle>
              <AlertDescription>
                {errorMessage(
                  loadError,
                  'Unable to load supplier debit-note applications.'
                )}
              </AlertDescription>
            </Alert>
          )}

          {!loading && canEdit && (
            <div className="space-y-4 rounded-md border bg-muted/20 p-4">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="supplierDebitNote">
                    Posted supplier debit note
                  </Label>
                  <Select
                    value={selectedNoteId || undefined}
                    onValueChange={(value) => {
                      setSelectedNoteId(value);
                      setSuggestedAmount(value, selectedInvoiceId);
                    }}
                  >
                    <SelectTrigger id="supplierDebitNote">
                      <SelectValue placeholder="Select a debit note" />
                    </SelectTrigger>
                    <SelectContent>
                      {availableNotes.map((note) => (
                        <SelectItem key={note.id} value={note.id}>
                          {note.debitNoteNumber} ·{' '}
                          {formatCurrency(
                            note.remainingAmount,
                            note.currencyCode
                          )}{' '}
                          remaining
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {availableNotes.length === 0 && (
                    <p className="text-xs text-muted-foreground">
                      No posted supplier debit notes with a remaining balance
                      match the selected invoice currency.
                    </p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="supplierDebitInvoice">
                    Outstanding invoice
                  </Label>
                  <Select
                    value={selectedInvoiceId || undefined}
                    onValueChange={(value) => {
                      setSelectedInvoiceId(value);
                      setSuggestedAmount(selectedNoteId, value);
                    }}
                  >
                    <SelectTrigger id="supplierDebitInvoice">
                      <SelectValue placeholder="Select an invoice" />
                    </SelectTrigger>
                    <SelectContent>
                      {availableInvoices.map((invoice) => (
                        <SelectItem
                          key={invoice.invoiceId}
                          value={invoice.invoiceId}
                        >
                          {invoice.invoiceNumber} ·{' '}
                          {formatCurrency(
                            invoice.balanceAmount,
                            invoice.currencyCode
                          )}{' '}
                          available
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {availableInvoices.length === 0 && (
                    <p className="text-xs text-muted-foreground">
                      No unreserved supplier invoice balance matches the
                      selected debit-note currency.
                    </p>
                  )}
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto] md:items-end">
                <div className="space-y-2">
                  <Label htmlFor="supplierDebitAmount">
                    Application amount
                  </Label>
                  <Input
                    id="supplierDebitAmount"
                    type="number"
                    min="0.01"
                    max={applicationLimit || undefined}
                    step="0.01"
                    value={amount}
                    onChange={(event) => setAmount(event.target.value)}
                    placeholder="0.00"
                  />
                  <p className="text-xs text-muted-foreground">
                    Maximum{' '}
                    {formatCurrency(applicationLimit, applicationCurrency)}
                  </p>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="supplierDebitNotes">
                    Settlement note (optional)
                  </Label>
                  <Textarea
                    id="supplierDebitNotes"
                    rows={2}
                    maxLength={500}
                    value={notes}
                    onChange={(event) => setNotes(event.target.value)}
                    placeholder="Credit-note reference or allocation context"
                  />
                </div>
                <Button
                  type="button"
                  disabled={
                    !selectedNote ||
                    !selectedInvoice ||
                    !amountIsValid ||
                    applyMutation.isPending
                  }
                  onClick={() => applyMutation.mutate()}
                >
                  {applyMutation.isPending && (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  )}
                  Reserve credit
                </Button>
              </div>
              <p className="text-xs text-muted-foreground">
                The amount is capped by the invoice&apos;s unreserved balance
                and the debit-note balance. It is entered in their shared{' '}
                {applicationCurrency} currency; the payment itself is{' '}
                {payment.currencyCode}. Supplier credit adds settlement value;
                it never consumes or inflates this payment&apos;s cash amount.
              </p>
            </div>
          )}

          {!canEdit && payment.status === 'Draft' && (
            <p className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
              Finance.AP.Payments.Process permission is required to reserve or
              release supplier credits.
            </p>
          )}
          {payment.status !== 'Draft' && (
            <p className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
              The supplier-credit set was frozen when this payment left Draft
              status.
            </p>
          )}

          {!loading && (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Debit note</TableHead>
                    <TableHead>Invoice</TableHead>
                    <TableHead>Date</TableHead>
                    <TableHead>State</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead className="text-right">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {history.map((application) => {
                    const released = reversedOriginalIds.has(application.id);
                    const applied = Boolean(
                      application.paymentPostingEventId ||
                        application.paymentJournalEntryId
                    );
                    return (
                      <TableRow
                        key={application.id}
                        className={
                          application.isReversal
                            ? 'text-muted-foreground'
                            : undefined
                        }
                      >
                        <TableCell>
                          <span className="font-medium">
                            {application.debitNoteNumber}
                          </span>
                          {application.supplierCreditNoteReference && (
                            <div className="text-xs text-muted-foreground">
                              Supplier ref:{' '}
                              {application.supplierCreditNoteReference}
                            </div>
                          )}
                        </TableCell>
                        <TableCell>{application.invoiceNumber}</TableCell>
                        <TableCell>
                          {format(
                            new Date(application.applicationDate),
                            'dd MMM yyyy'
                          )}
                        </TableCell>
                        <TableCell>
                          {application.isReversal ? (
                            <Badge variant="outline">Reversal</Badge>
                          ) : released ? (
                            <Badge variant="outline">Released</Badge>
                          ) : applied ? (
                            <Badge className="bg-emerald-600">Applied</Badge>
                          ) : (
                            <Badge variant="secondary">Reserved</Badge>
                          )}
                          {application.notes && (
                            <div className="mt-1 max-w-64 text-xs text-muted-foreground">
                              {application.notes}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          {formatCurrency(
                            application.applicationAmount,
                            application.currencyCode
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          {!application.isReversal &&
                            !released &&
                            !applied &&
                            canEdit && (
                              <Button
                                type="button"
                                size="sm"
                                variant="outline"
                                onClick={() => setReversalTarget(application)}
                              >
                                <RotateCcw className="mr-2 h-3.5 w-3.5" />{' '}
                                Release
                              </Button>
                            )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                  {history.length === 0 && (
                    <TableRow>
                      <TableCell
                        colSpan={6}
                        className="h-24 text-center text-muted-foreground"
                      >
                        No supplier debit notes are linked to this payment.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={Boolean(reversalTarget)}
        onOpenChange={(open) => {
          if (!open && !reverseMutation.isPending) {
            setReversalTarget(null);
            setReversalReason('');
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Release supplier-credit reservation</DialogTitle>
            <DialogDescription>
              This keeps the original row and adds an immutable reversal. It
              does not reverse the posted debit note.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="supplierDebitReversalReason">Reason</Label>
            <Textarea
              id="supplierDebitReversalReason"
              maxLength={1000}
              value={reversalReason}
              onChange={(event) => setReversalReason(event.target.value)}
              placeholder="Explain why this invoice-credit reservation is being released"
            />
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              disabled={reverseMutation.isPending}
              onClick={() => setReversalTarget(null)}
            >
              Keep reservation
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={
                !reversalTarget ||
                !reversalReason.trim() ||
                reverseMutation.isPending
              }
              onClick={() =>
                reversalTarget &&
                reverseMutation.mutate({
                  id: reversalTarget.id,
                  reason: reversalReason.trim(),
                })
              }
            >
              {reverseMutation.isPending && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Release reservation
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
