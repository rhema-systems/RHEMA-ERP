'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { ArrowLeft, BookOpen, Loader2, Undo2, WalletCards } from 'lucide-react';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { DOCUMENT_TYPES } from '@/services/document-output.service';
import { ControlledDocumentIssueActions } from '@/components/finance/ControlledDocumentIssueActions';
import { CashTransactionType } from '@/types/cash-management';
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
import { SourceDocumentDimensionEvidence } from '@/components/finance/dimensions/source-document-dimension-panel';
import { TransactionExchangeRateOverridePanel } from '@/components/finance/TransactionExchangeRateOverridePanel';

export default function CashTransactionDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    // The server repeats this permission and bank-scope check. Hiding the action here keeps a
    // cashier's screen aligned with the dedicated higher-risk correction responsibility.
    const hasReversalPermission = hasPermission('Finance.CashBank.Transactions.Reverse');
    const [dialogOpen, setDialogOpen] = useState(false);
    const [reason, setReason] = useState('');
    const [reversalDate, setReversalDate] = useState('');
    const [isReversing, setIsReversing] = useState(false);

    const { data: transaction, isLoading, refetch } = useQuery({
        queryKey: ['cash-transaction', id],
        queryFn: () => cashManagementDataService.getCashTransactionById(id),
    });
    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeDataService.getFinanceSettings(),
        staleTime: 5 * 60 * 1000,
    });
    const minimumReasonLength = financeSettings?.minimumReversalReasonLength ?? 20;
    const { data: trace, isLoading: traceLoading, refetch: refetchTrace } = useQuery({
        queryKey: ['cash-transaction-trace', id],
        queryFn: () => cashManagementDataService.getCashTransactionTrace(id),
        enabled: Boolean(transaction?.journalEntryId),
    });

    const canReverse = hasReversalPermission &&
        Boolean(transaction?.isPosted) &&
        !transaction?.isReversed &&
        !transaction?.reversalOfCashTransactionId &&
        !transaction?.isReconciled;

    const handleReverse = async () => {
        if (!transaction) return;
        setIsReversing(true);
        try {
            await cashManagementDataService.reverseCashTransaction(transaction.id, {
                reason: reason.trim(),
                reversalDate: reversalDate || undefined,
            });
            toast({
                title: 'Cash/bank transaction reversed',
                description: 'The linked compensating journal and bank transaction records were posted.',
            });
            setDialogOpen(false);
            setReason('');
            setReversalDate('');
            await Promise.all([refetch(), refetchTrace()]);
        } catch (error: any) {
            toast({
                title: 'Reversal failed',
                description: error?.message || 'Unable to reverse this cash/bank transaction.',
                variant: 'destructive',
            });
        } finally {
            setIsReversing(false);
        }
    };

    if (isLoading) {
        return <div className="mx-auto max-w-[1100px] space-y-6 p-8"><Skeleton className="h-10 w-80" /><Skeleton className="h-72 w-full" /><Skeleton className="h-64 w-full" /></div>;
    }
    if (!transaction) {
        return <div className="p-8 text-center"><h2 className="text-xl font-semibold">Cash transaction not found</h2><Button variant="link" onClick={() => router.push('/finance/cash/transactions')}>Return to cash transactions</Button></div>;
    }

    const status = transaction.isReversed
        ? 'Reversed'
        : transaction.reversalOfCashTransactionId
            ? 'Compensating entry'
            : transaction.isReconciled
                ? 'Reconciled'
                : transaction.approvalStatusName || (transaction.isPosted ? 'Posted' : 'Pending');

    return (
        <div className="mx-auto max-w-[1100px] space-y-8 p-8">
            <TransactionExchangeRateOverridePanel
                sourceDocumentType="CashTransaction"
                sourceDocumentId={transaction.id}
                transactionCurrencyCode={transaction.currency}
                governedExchangeRateId={transaction.exchangeRateId}
                governedRate={transaction.exchangeRate}
                canRequestForDocument={!transaction.isPosted && transaction.approvalStatusName === 'Captured'}
                ineligibleReason="Only a captured, unposted cash/bank transaction can request an override."
            />
            <div className="flex flex-wrap items-center justify-between gap-4 no-print">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/cash/transactions')}><ArrowLeft className="h-4 w-4" /></Button>
                    <div className="flex flex-wrap items-center gap-2">
                        <h1 className="text-2xl font-bold tracking-tight">{transaction.transactionNumber}</h1>
                        <Badge className={transaction.isReversed ? 'bg-amber-600' : transaction.isPosted ? 'bg-green-600' : ''}>{status}</Badge>
                    </div>
                </div>
                <div className="flex gap-2">
                    {canReverse && <Button variant="destructive" size="sm" onClick={() => setDialogOpen(true)}><Undo2 className="mr-2 h-4 w-4" /> Reverse Transaction</Button>}
                    {transaction.transactionType === CashTransactionType.Payment && transaction.isPosted && transaction.journalEntryId && !transaction.reversalOfCashTransactionId && (
                        <ControlledDocumentIssueActions
                            documentType={DOCUMENT_TYPES.financeCashBankPaymentSlip}
                            entityId={transaction.id}
                            documentLabel="Payment Slip"
                            issuance={transaction.paymentSlipIssuance}
                            onIssued={() => refetch()}
                        />
                    )}
                </div>
            </div>

            <Card className="print:shadow-none">
                <CardHeader className="border-b"><CardTitle>{transaction.transactionType}</CardTitle><p className="text-sm text-muted-foreground">Finance cash/bank operational record</p></CardHeader>
                <CardContent className="space-y-8 pt-6">
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
                        <Field label="Amount" value={formatCurrency(transaction.amount, transaction.currency)} prominent />
                        <Field label="Transaction date" value={format(new Date(transaction.transactionDate), 'MMM dd, yyyy')} />
                        <Field label="Bank account" value={transaction.bankAccountName || transaction.bankAccountId} />
                        <Field label="Destination account" value={transaction.toBankAccountName || '-'} />
                        <Field label="Payee / payer" value={transaction.payeeOrPayer || '-'} />
                        <Field label="Reference" value={transaction.referenceNumber || '-'} />
                        <Field label="GL account" value={transaction.glAccountNumber ? `${transaction.glAccountNumber} - ${transaction.glAccountName || ''}` : transaction.glAccountId || 'Transfer contra bank'} />
                        <Field label="Original journal" value={transaction.journalEntryId || '-'} mono />
                    </div>
                    {transaction.description && <div className="border-t pt-6"><p className="mb-1 text-xs font-bold uppercase text-muted-foreground">Description</p><p>{transaction.description}</p></div>}

                    {transaction.reversalJournalEntryId && (
                        <div className="rounded-md border border-amber-300 bg-amber-50 p-4 text-sm">
                            <div className="flex items-center gap-2 font-semibold text-amber-900"><Undo2 className="h-4 w-4" /> Controlled cash/bank reversal</div>
                            <div className="mt-3 grid gap-3 md:grid-cols-2">
                                <div><span className="text-muted-foreground">Date:</span> {transaction.reversalDate ? format(new Date(transaction.reversalDate), 'MMM dd, yyyy') : '-'}</div>
                                <div><span className="text-muted-foreground">Journal:</span> <span className="font-mono text-xs">{transaction.reversalJournalEntryId}</span></div>
                                <div className="md:col-span-2"><span className="text-muted-foreground">Reason:</span> {transaction.reversalReason}</div>
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>

            <div className="no-print">
                <SourceDocumentDimensionEvidence evidence={transaction.financeDimensions} />
            </div>

            {transaction.journalEntryId && (
                <Card className="no-print">
                    <CardHeader><CardTitle className="flex items-center gap-2"><BookOpen className="h-5 w-5" /> Source-to-ledger trace</CardTitle><p className="text-sm text-muted-foreground">Immutable operational rows, posting events, journal lines, and audit evidence.</p></CardHeader>
                    <CardContent className="space-y-6">
                        {traceLoading && <Skeleton className="h-40 w-full" />}
                        {trace?.postings.map(posting => (
                            <div key={posting.postingEventId} className="rounded-md border">
                                <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/40 p-4"><div><Badge variant={posting.postingAction.startsWith('Reverse') ? 'outline' : 'secondary'}>{posting.postingAction}</Badge><span className="ml-2 font-medium">{posting.journalEntryNumber || posting.journalEntryId}</span></div><div className="text-sm">{format(new Date(posting.postingDate), 'MMM dd, yyyy')} · {formatCurrency(posting.totalDebitAmount, posting.functionalCurrencyCode)}</div></div>
                                <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="text-left text-xs uppercase text-muted-foreground"><tr><th className="p-3">Account</th><th className="p-3">Description</th><th className="p-3 text-right">Debit</th><th className="p-3 text-right">Credit</th></tr></thead><tbody>{posting.lines.map(line => <tr key={line.transactionId} className="border-t"><td className="p-3"><span className="font-mono">{line.accountNumber}</span><br /><span className="text-xs text-muted-foreground">{line.accountName}</span></td><td className="p-3">{line.description}</td><td className="p-3 text-right">{line.debitAmount ? formatCurrency(line.debitAmount, posting.functionalCurrencyCode) : '-'}</td><td className="p-3 text-right">{line.creditAmount ? formatCurrency(line.creditAmount, posting.functionalCurrencyCode) : '-'}</td></tr>)}</tbody></table></div>
                            </div>
                        ))}

                        {trace && trace.relatedTransactions.length > 0 && (
                            <div><h3 className="mb-3 flex items-center gap-2 font-semibold"><WalletCards className="h-4 w-4" /> Related bank records</h3><div className="grid gap-3 md:grid-cols-2">{trace.relatedTransactions.map(item => <button type="button" key={item.id} onClick={() => router.push(`/finance/cash/transactions/${item.id}`)} className="rounded-md border p-3 text-left text-sm hover:bg-muted/50"><div className="flex justify-between gap-3"><span className="font-medium">{item.transactionNumber}</span><Badge variant="outline">{item.isReversed ? 'Reversed' : item.reversalOfCashTransactionId ? 'Correction' : item.transactionType}</Badge></div><div className="mt-2 text-muted-foreground">{item.bankAccountName} · {format(new Date(item.transactionDate), 'MMM dd, yyyy')}</div><div className="mt-1 font-semibold">{formatCurrency(item.amount, item.currency)}</div></button>)}</div></div>
                        )}

                        {trace && trace.auditEvents.length > 0 && <div><h3 className="mb-3 font-semibold">Audit history</h3><div className="space-y-2">{trace.auditEvents.map(event => <div key={event.auditLogId} className="flex flex-wrap justify-between gap-2 rounded-md border p-3 text-sm"><div><span className="font-medium">{event.eventType}</span><span className="ml-2 text-muted-foreground">by {event.username}</span></div><span className="text-muted-foreground">{format(new Date(event.timestamp), 'MMM dd, yyyy HH:mm')}</span></div>)}</div></div>}
                    </CardContent>
                </Card>
            )}

            <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
                <DialogContent>
                    <DialogHeader><DialogTitle>Reverse posted cash/bank transaction</DialogTitle><DialogDescription>The original transaction remains immutable. Finance will post an opposite journal and operational bank row; a transfer produces a complete opposite pair. Reconciled items must first be removed from reconciliation.</DialogDescription></DialogHeader>
                    <div className="space-y-4 py-2">
                        <div className="space-y-2"><Label htmlFor="cashReversalReason">Reason</Label><Textarea id="cashReversalReason" rows={5} value={reason} onChange={event => setReason(event.target.value)} placeholder="Describe the error, evidence reviewed, and why reversal is required." /><p className="text-xs text-muted-foreground">Tenant policy requires at least {minimumReasonLength} characters.</p></div>
                        <div className="space-y-2"><Label htmlFor="cashReversalDate">Preferred reversal date (optional)</Label><Input id="cashReversalDate" type="date" value={reversalDate} onChange={event => setReversalDate(event.target.value)} /><p className="text-xs text-muted-foreground">Leave blank to let the Finance policy select a valid date in the current open period.</p></div>
                    </div>
                    <DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)} disabled={isReversing}>Cancel</Button><Button variant="destructive" onClick={handleReverse} disabled={isReversing || reason.trim().length < minimumReasonLength}>{isReversing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Post Reversal</Button></DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}

function Field({ label, value, prominent = false, mono = false }: { label: string; value: string; prominent?: boolean; mono?: boolean }) {
    return <div><p className="mb-1 text-xs font-bold uppercase text-muted-foreground">{label}</p><p className={`${prominent ? 'text-2xl font-bold' : 'font-medium'} ${mono ? 'break-all font-mono text-xs' : ''}`}>{value}</p></div>;
}
