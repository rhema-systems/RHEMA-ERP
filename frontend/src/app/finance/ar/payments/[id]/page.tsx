'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, BookOpen, Loader2, Undo2, WalletCards } from 'lucide-react';
import { format } from 'date-fns';
import { arService } from '@/services/ar-service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { DOCUMENT_TYPES } from '@/services/document-output.service';
import { ControlledDocumentIssueActions } from '@/components/finance/ControlledDocumentIssueActions';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';

export default function CustomerReceiptDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    // The API remains authoritative. This UI check keeps ordinary receipt operators from seeing
    // a high-risk action that requires the dedicated Finance reversal permission.
    const canReverse = hasPermission('Finance.AR.Payments.Reverse');
    const [reverseDialogOpen, setReverseDialogOpen] = useState(false);
    const [isReversing, setIsReversing] = useState(false);
    const [reversalReason, setReversalReason] = useState('');
    const [reversalDate, setReversalDate] = useState('');

    const { data: payment, isLoading, refetch } = useQuery({
        queryKey: ['customer-receipt', id],
        queryFn: () => arService.getPayment(id),
    });
    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeDataService.getFinanceSettings(),
        // Surface the tenant rule before submission while the server still enforces it.
        staleTime: 5 * 60 * 1000,
    });
    const minimumReasonLength = financeSettings?.minimumReversalReasonLength ?? 20;
    const { data: trace, isLoading: traceLoading, refetch: refetchTrace } = useQuery({
        queryKey: ['customer-receipt-trace', id],
        queryFn: () => arService.getPaymentTrace(id),
        enabled: Boolean(payment?.journalEntryId),
    });

    const handleReverse = async () => {
        if (!payment) return;
        setIsReversing(true);
        try {
            await arService.reversePayment(payment.id, {
                reason: reversalReason.trim(),
                reversalDate: reversalDate || undefined,
            });
            toast({
                title: 'Receipt reversed',
                description: 'Linked compensating ledger, allocation, and bank/liquidity entries were created.',
            });
            setReverseDialogOpen(false);
            setReversalReason('');
            setReversalDate('');
            await Promise.all([refetch(), refetchTrace()]);
        } catch (error: any) {
            toast({
                title: 'Reversal failed',
                description: error?.message || 'Unable to reverse this customer receipt.',
                variant: 'destructive',
            });
        } finally {
            setIsReversing(false);
        }
    };

    if (isLoading) {
        return (
            <div className="space-y-6 p-8 max-w-[1100px] mx-auto">
                <Skeleton className="h-10 w-80" />
                <Skeleton className="h-72 w-full" />
                <Skeleton className="h-64 w-full" />
            </div>
        );
    }

    if (!payment) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">Customer receipt not found</h2>
                <Button variant="link" onClick={() => router.push('/finance/ar/receipts')}>Return to receipts</Button>
            </div>
        );
    }

    return (
        <div className="space-y-8 p-8 max-w-[1100px] mx-auto">
            <div className="flex flex-wrap items-center justify-between gap-4 no-print">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ar/receipts')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div className="flex items-center gap-2">
                        <h1 className="text-2xl font-bold tracking-tight">Receipt {payment.paymentNumber}</h1>
                        <Badge className={payment.status === 'Reversed' ? 'bg-amber-600' : payment.status === 'Posted' ? 'bg-green-600' : ''}>
                            {payment.status}
                        </Badge>
                    </div>
                </div>
                <div className="flex gap-2">
                    {canReverse && payment.status === 'Posted' && payment.journalEntryId && !payment.reversalJournalEntryId && (
                        <Button variant="destructive" size="sm" onClick={() => setReverseDialogOpen(true)}>
                            <Undo2 className="mr-2 h-4 w-4" /> Reverse Receipt
                        </Button>
                    )}
                    {payment.journalEntryId && !payment.isCreditNote && ['Posted', 'Cleared', 'Bounced', 'Reversed'].includes(payment.status) && (
                        <ControlledDocumentIssueActions
                            documentType={DOCUMENT_TYPES.financeArCustomerReceipt}
                            entityId={payment.id}
                            documentLabel="Official Receipt"
                            issuance={payment.receiptIssuance}
                            onIssued={() => refetch()}
                        />
                    )}
                </div>
            </div>

            <Card className="print:shadow-none">
                <CardHeader className="border-b">
                    <CardTitle>{payment.customerName}</CardTitle>
                    <p className="text-sm text-muted-foreground">Customer ID: {payment.customerId}</p>
                </CardHeader>
                <CardContent className="space-y-8 pt-6">
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
                        <Field label="Amount" value={formatCurrency(payment.totalAmount, payment.currencyCode)} prominent />
                        <Field label="Receipt date" value={format(new Date(payment.paymentDate), 'MMM dd, yyyy')} />
                        <Field label="Method" value={payment.paymentMethodName || payment.paymentMethod} />
                        <Field label="Destination" value={payment.bankAccountName || payment.liquidityAccountName || 'Tenant default'} />
                        <Field label="Allocated" value={formatCurrency(payment.allocatedAmount, payment.currencyCode)} />
                        <Field label="Unallocated" value={formatCurrency(payment.unallocatedAmount, payment.currencyCode)} />
                        <Field label="Transaction reference" value={payment.transactionReference || payment.paymentReference || payment.referenceNumber || '-'} />
                        <Field label="Original journal" value={payment.journalEntryId || '-'} mono />
                    </div>

                    <div className="border-t pt-6">
                        <h3 className="mb-3 font-semibold">Invoice allocations</h3>
                        <div className="overflow-x-auto rounded-md border">
                            <table className="w-full text-sm">
                                <thead className="bg-muted/50 text-left text-xs uppercase text-muted-foreground">
                                    <tr><th className="p-3">Invoice</th><th className="p-3">Date</th><th className="p-3 text-right">Discount</th><th className="p-3 text-right">Applied</th></tr>
                                </thead>
                                <tbody>
                                    {payment.allocations?.map(allocation => (
                                        <tr key={allocation.id} className={`border-t ${allocation.isReversal ? 'bg-amber-50/60' : ''}`}>
                                            <td className="p-3 font-medium">{allocation.invoiceNumber} {allocation.isReversal && <Badge variant="outline" className="ml-2">Reversal</Badge>}</td>
                                            <td className="p-3">{format(new Date(allocation.allocationDate), 'MMM dd, yyyy')}</td>
                                            <td className="p-3 text-right">{allocation.discountAmount ? formatCurrency(allocation.discountAmount, allocation.invoiceCurrencyCode || payment.currencyCode) : '-'}</td>
                                            <td className="p-3 text-right">
                                                <div>{formatCurrency(allocation.allocatedAmount, allocation.invoiceCurrencyCode || payment.currencyCode)}</div>
                                                {allocation.isCrossCurrency && (
                                                    <div className="text-xs text-muted-foreground">
                                                        from {formatCurrency(allocation.paymentCurrencyAmount, allocation.paymentCurrencyCode || payment.currencyCode)}
                                                    </div>
                                                )}
                                            </td>
                                        </tr>
                                    ))}
                                    {!payment.allocations?.length && <tr><td colSpan={4} className="p-4 text-center text-muted-foreground">No invoice allocations.</td></tr>}
                                </tbody>
                            </table>
                        </div>
                    </div>

                    {payment.reversalJournalEntryId && (
                        <div className="rounded-md border border-amber-300 bg-amber-50 p-4 text-sm">
                            <div className="flex items-center gap-2 font-semibold text-amber-900"><Undo2 className="h-4 w-4" /> Controlled receipt reversal</div>
                            <div className="mt-3 grid gap-3 md:grid-cols-2">
                                <div><span className="text-muted-foreground">Date:</span> {payment.reversalDate ? format(new Date(payment.reversalDate), 'MMM dd, yyyy') : '-'}</div>
                                <div><span className="text-muted-foreground">Journal:</span> <span className="font-mono text-xs">{payment.reversalJournalEntryId}</span></div>
                                <div className="md:col-span-2"><span className="text-muted-foreground">Reason:</span> {payment.reversalReason}</div>
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>

            {payment.journalEntryId && (
                <Card className="no-print">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><BookOpen className="h-5 w-5" /> Source-to-ledger trace</CardTitle>
                        <p className="text-sm text-muted-foreground">Immutable ledger, operational-subledger, and audit evidence for this receipt.</p>
                    </CardHeader>
                    <CardContent className="space-y-6">
                        {traceLoading && <Skeleton className="h-40 w-full" />}
                        {trace?.postings.map(posting => (
                            <div key={posting.postingEventId} className="rounded-md border">
                                <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/40 p-4">
                                    <div><Badge variant={posting.postingAction.startsWith('Reverse') ? 'outline' : 'secondary'}>{posting.postingAction}</Badge><span className="ml-2 font-medium">{posting.journalEntryNumber || posting.journalEntryId}</span></div>
                                    <div className="text-sm">{format(new Date(posting.postingDate), 'MMM dd, yyyy')} · {formatCurrency(posting.totalDebitAmount, posting.functionalCurrencyCode)}</div>
                                </div>
                                <div className="overflow-x-auto">
                                    <table className="w-full text-sm">
                                        <thead className="text-left text-xs uppercase text-muted-foreground"><tr><th className="p-3">Account</th><th className="p-3">Description</th><th className="p-3 text-right">Debit</th><th className="p-3 text-right">Credit</th></tr></thead>
                                        <tbody>{posting.lines.map(line => <tr key={line.transactionId} className="border-t"><td className="p-3"><span className="font-mono">{line.accountNumber}</span><br /><span className="text-xs text-muted-foreground">{line.accountName}</span></td><td className="p-3">{line.description}</td><td className="p-3 text-right">{line.debitAmount ? formatCurrency(line.debitAmount, posting.functionalCurrencyCode) : '-'}</td><td className="p-3 text-right">{line.creditAmount ? formatCurrency(line.creditAmount, posting.functionalCurrencyCode) : '-'}</td></tr>)}</tbody>
                                    </table>
                                </div>
                            </div>
                        ))}

                        {trace && trace.operationalEntries.length > 0 && (
                            <div>
                                <h3 className="mb-3 flex items-center gap-2 font-semibold"><WalletCards className="h-4 w-4" /> Bank and holding-account evidence</h3>
                                <div className="grid gap-3 md:grid-cols-2">{trace.operationalEntries.map(entry => <div key={entry.recordId} className="rounded-md border p-3 text-sm"><div className="flex justify-between gap-3"><span className="font-medium">{entry.recordType}</span><Badge variant="outline">{entry.status}</Badge></div><div className="mt-2 text-muted-foreground">{entry.reference} · {format(new Date(entry.recordDate), 'MMM dd, yyyy')}</div><div className="mt-1 font-semibold">{formatCurrency(entry.amount, entry.currencyCode)}</div>{entry.isReconciled && <p className="mt-2 text-xs text-amber-700">Consumed by reconciliation or settlement</p>}</div>)}</div>
                            </div>
                        )}

                        {trace && trace.auditEvents.length > 0 && (
                            <div><h3 className="mb-3 font-semibold">Audit history</h3><div className="space-y-2">{trace.auditEvents.map(event => <div key={event.auditLogId} className="flex flex-wrap justify-between gap-2 rounded-md border p-3 text-sm"><div><span className="font-medium">{event.eventType}</span><span className="ml-2 text-muted-foreground">by {event.username}</span></div><span className="text-muted-foreground">{format(new Date(event.timestamp), 'MMM dd, yyyy HH:mm')}</span></div>)}</div></div>
                        )}
                    </CardContent>
                </Card>
            )}

            <Dialog open={reverseDialogOpen} onOpenChange={setReverseDialogOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Reverse posted customer receipt</DialogTitle>
                        <DialogDescription>
                            Finance will retain the original receipt and post linked compensating entries. Reconciled or deposited receipts must first be removed from their banking workflow.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-4 py-2">
                        <div className="space-y-2"><Label htmlFor="reversalReason">Reason</Label><Textarea id="reversalReason" rows={5} value={reversalReason} onChange={event => setReversalReason(event.target.value)} placeholder="Describe the error, evidence reviewed, and why reversal is required." /><p className="text-xs text-muted-foreground">Tenant policy requires at least {minimumReasonLength} characters.</p></div>
                        <div className="space-y-2"><Label htmlFor="reversalDate">Preferred reversal date (optional)</Label><Input id="reversalDate" type="date" value={reversalDate} onChange={event => setReversalDate(event.target.value)} /><p className="text-xs text-muted-foreground">Leave blank to use the valid date selected by the Finance reversal-period policy.</p></div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setReverseDialogOpen(false)} disabled={isReversing}>Cancel</Button>
                        <Button variant="destructive" onClick={handleReverse} disabled={isReversing || reversalReason.trim().length < minimumReasonLength}>{isReversing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Post Reversal</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}

function Field({ label, value, prominent = false, mono = false }: { label: string; value: string; prominent?: boolean; mono?: boolean }) {
    return <div><p className="mb-1 text-xs font-bold uppercase text-muted-foreground">{label}</p><p className={`${prominent ? 'text-2xl font-bold' : 'font-medium'} ${mono ? 'break-all font-mono text-xs' : ''}`}>{value}</p></div>;
}
