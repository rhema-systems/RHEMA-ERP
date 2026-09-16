'use client';

import { ChangeEvent, useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft, BadgeCheck, CheckCircle2, FileText, Landmark, Loader2, RotateCcw, Send, Upload, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    SettlementDimensionEvidence,
    SourceDocumentDimensionEvidence,
    SourceDocumentDimensionPanel,
} from '@/components/finance/dimensions/source-document-dimension-panel';
import {
    toFinancePostingDimensionValues,
    toFinanceSourceDimensionFormState,
} from '@/lib/finance/source-document-dimensions';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { BankDeposit } from '@/types/cash-management';

export default function BankDepositDetailPage() {
    const params = useParams<{ id: string }>();
    const [deposit, setDeposit] = useState<BankDeposit | null>(null);
    const [loading, setLoading] = useState(true);
    const [working, setWorking] = useState(false);
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);
    const [confirmationEvidenceName, setConfirmationEvidenceName] = useState('');
    const [confirmationForm, setConfirmationForm] = useState({
        bankConfirmationReference: '',
        bankConfirmationDate: new Date().toISOString().slice(0, 10),
        confirmationEvidenceFileId: '',
        notes: '',
    });

    const acceptDeposit = useCallback((value: BankDeposit) => {
        setDeposit(value);
        const dimensionForm = toFinanceSourceDimensionFormState(value.financeDimensions);
        setDefaultDimensionValues(dimensionForm.defaultValues);
        setLineDimensionValues(dimensionForm.lineValues);
        setApplyDefaultToAll(false);
    }, []);

    const load = useCallback(async () => {
        try {
            acceptDeposit(await cashManagementDataService.getBankDeposit(params.id));
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not load deposit.');
        } finally {
            setLoading(false);
        }
    }, [acceptDeposit, params.id]);

    useEffect(() => { void load(); }, [load]);

    const run = async (operation: () => Promise<BankDeposit>, message: string) => {
        setWorking(true);
        try {
            acceptDeposit(await operation());
            toast.success(message);
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'The action failed.');
        } finally {
            setWorking(false);
        }
    };

    const upload = async (event: ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file || !deposit) return;
        setWorking(true);
        try {
            const fileId = await cashManagementDataService.uploadBankingEvidence(file);
            acceptDeposit(await cashManagementDataService.linkBankDepositAttachment(deposit.id, fileId));
            toast.success('Deposit slip attached.');
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not attach evidence.');
        } finally {
            event.target.value = '';
            setWorking(false);
        }
    };

    const uploadConfirmationEvidence = async (event: ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file) return;
        setWorking(true);
        try {
            const fileId = await cashManagementDataService.uploadBankingEvidence(file);
            setConfirmationForm(current => ({ ...current, confirmationEvidenceFileId: fileId }));
            setConfirmationEvidenceName(file.name);
            toast.success('Bank confirmation evidence is ready to record.');
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not upload confirmation evidence.');
        } finally {
            event.target.value = '';
            setWorking(false);
        }
    };

    const confirmDeposit = async () => {
        if (!deposit || !confirmationForm.bankConfirmationReference.trim()) {
            toast.error('Enter the bank confirmation reference.');
            return;
        }
        await run(
            () => cashManagementDataService.confirmBankDeposit(deposit.id, {
                bankConfirmationReference: confirmationForm.bankConfirmationReference.trim(),
                bankConfirmationDate: confirmationForm.bankConfirmationDate,
                confirmationEvidenceFileId: confirmationForm.confirmationEvidenceFileId || undefined,
                notes: confirmationForm.notes.trim() || undefined,
                rowVersion: deposit.rowVersion,
            }),
            'Bank acknowledgement recorded.',
        );
    };

    const dimensionLines = useMemo(() => deposit ? [
        {
            id: deposit.id,
            accountId: deposit.bankGLAccountId,
            accountLabel: `Bank · ${deposit.bankAccountName}`,
        },
        ...deposit.allocations.map(item => ({
            id: item.id,
            accountId: item.glAccountId,
            accountLabel: `${item.entryNumber} · ${item.liquidityAccountName}`,
        })),
    ] : [], [deposit]);
    const financeDimensionInput = useMemo(() => ({
        defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
        lines: dimensionLines.flatMap(line => line.accountId ? [{
            sourceLineId: line.id,
            accountId: line.accountId,
            dimensions: toFinancePostingDimensionValues(lineDimensionValues[line.id] || {}),
        }] : []),
        applyDefaultToEligibleLines: applyDefaultToAll,
    }), [applyDefaultToAll, defaultDimensionValues, dimensionLines, lineDimensionValues]);

    const saveDimensions = async () => {
        if (!deposit) return;
        await run(
            () => cashManagementDataService.updateBankDepositDimensions(deposit.id, financeDimensionInput),
            'Finance coding dimensions saved.',
        );
    };

    if (loading || !deposit) {
        return <div className="flex min-h-[50vh] items-center justify-center"><Loader2 className="h-7 w-7 animate-spin" /></div>;
    }

    const editable = deposit.status === 'Draft' || deposit.status === 'Returned';
    const submitted = deposit.status === 'Submitted';

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                    <Button variant="ghost" size="icon" asChild><Link href="/finance/cash/deposits"><ArrowLeft className="h-4 w-4" /></Link></Button>
                    <div><div className="flex items-center gap-2"><h1 className="text-3xl font-bold">{deposit.depositNumber}</h1><Badge>{deposit.status}</Badge></div><p className="text-muted-foreground">{deposit.bankAccountName} · {deposit.depositReference}</p></div>
                </div>
                <div className="flex flex-wrap gap-2">
                    {editable && (
                        <>
                            <Button variant="outline" asChild><label className="cursor-pointer"><FileText className="mr-2 h-4 w-4" />Attach deposit slip<input className="hidden" type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={upload} /></label></Button>
                            <Button disabled={working || !deposit.attachments.some(item => item.isPrimaryEvidence)} onClick={() => void run(async () => {
                                await cashManagementDataService.updateBankDepositDimensions(deposit.id, financeDimensionInput);
                                return cashManagementDataService.submitBankDeposit(deposit.id);
                            }, 'Deposit submitted to the Chief Accountant.')}><Send className="mr-2 h-4 w-4" />Submit</Button>
                        </>
                    )}
                    {submitted && (
                        <>
                            <Button disabled={working} onClick={() => void run(() => cashManagementDataService.approveBankDeposit(deposit.id), 'Deposit approved and posted.')}><CheckCircle2 className="mr-2 h-4 w-4" />Approve</Button>
                            <Button variant="outline" disabled={working} onClick={() => { const comments = window.prompt('What needs to be corrected?'); if (comments) void run(() => cashManagementDataService.returnBankDeposit(deposit.id, comments), 'Deposit returned for changes.'); }}><RotateCcw className="mr-2 h-4 w-4" />Return</Button>
                            <Button variant="destructive" disabled={working} onClick={() => { const reason = window.prompt('Rejection reason'); if (reason) void run(() => cashManagementDataService.rejectBankDeposit(deposit.id, reason), 'Deposit rejected.'); }}><XCircle className="mr-2 h-4 w-4" />Reject</Button>
                        </>
                    )}
                    {deposit.status === 'Approved' && (
                        <Button disabled={working} onClick={() => void run(() => cashManagementDataService.postBankDeposit(deposit.id), 'Deposit posted to the bank control account.')}><Landmark className="mr-2 h-4 w-4" />Post deposit</Button>
                    )}
                </div>
            </div>
            <div className="grid gap-4 md:grid-cols-3">
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Selected receipts</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{deposit.currency} {deposit.totalReceipts.toLocaleString(undefined, { minimumFractionDigits: 2 })}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Approved deductions</CardTitle></CardHeader><CardContent className="text-2xl font-semibold text-red-600">{deposit.currency} {deposit.totalDeductions.toLocaleString(undefined, { minimumFractionDigits: 2 })}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Net on deposit slip</CardTitle></CardHeader><CardContent className="text-2xl font-bold">{deposit.currency} {deposit.netAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</CardContent></Card>
            </div>
            <Card className="no-print">
                <CardHeader>
                    <CardTitle>Finance coding dimensions</CardTitle>
                    <CardDescription>
                        Stable settlement-line identities retain their coding through draft edits. Submitted evidence is immutable.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    {editable ? (
                        <>
                            <SourceDocumentDimensionPanel
                                context={{
                                    sourceModule: 'CASHBANK',
                                    sourceDocumentType: 'BankDepositBatch',
                                    postingAction: 'Post',
                                    sourceRoute: 'finance.cash.bank-deposits',
                                    contractVersion: '1.0',
                                }}
                                effectiveDate={deposit.depositDate.slice(0, 10)}
                                lines={dimensionLines}
                                defaultValues={defaultDimensionValues}
                                lineValues={lineDimensionValues}
                                onDefaultValuesChange={(values) => {
                                    setDefaultDimensionValues(values);
                                    setApplyDefaultToAll(false);
                                }}
                                onLineValuesChange={setLineDimensionValues}
                                onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                                certificationState={deposit.financeDimensions?.certificationState}
                                disabled={working}
                            />
                            <div className="flex justify-end">
                                <Button type="button" disabled={working} onClick={() => void saveDimensions()}>
                                    Save Finance coding
                                </Button>
                            </div>
                        </>
                    ) : (
                        <SourceDocumentDimensionEvidence evidence={deposit.financeDimensions} />
                    )}
                    <SettlementDimensionEvidence evidence={deposit.settlementDimensionEvidence} />
                </CardContent>
            </Card>
            <div className="grid gap-6 xl:grid-cols-[2fr_1fr]">
                <Card>
                    <CardHeader><CardTitle>Settlement lines</CardTitle><CardDescription>Partial allocations are reserved while this batch remains open.</CardDescription></CardHeader>
                    <CardContent className="overflow-x-auto">
                        <table className="w-full text-sm">
                            <thead className="border-b text-left text-muted-foreground"><tr><th className="p-3">Source</th><th className="p-3">Counterparty</th><th className="p-3">Holding account</th><th className="p-3">Kind</th><th className="p-3 text-right">Amount</th></tr></thead>
                            <tbody>{deposit.allocations.map(item => <tr key={item.id} className="border-b"><td className="p-3"><div className="font-medium">{item.entryNumber}</div><div className="text-xs text-muted-foreground">{item.referenceNumber}</div></td><td className="p-3">{item.counterpartyName ?? item.description}</td><td className="p-3">{item.liquidityAccountName}</td><td className={`p-3 ${item.allocationType === 'Deduction' ? 'text-red-600' : 'text-green-700'}`}>{item.allocationType}</td><td className="p-3 text-right">{deposit.currency} {item.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td></tr>)}</tbody>
                        </table>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader><CardTitle>Evidence & posting</CardTitle><CardDescription>Evidence is frozen once submitted.</CardDescription></CardHeader>
                    <CardContent className="space-y-4">
                        {deposit.attachments.length === 0 ? <p className="text-sm text-muted-foreground">No evidence attached.</p> : deposit.attachments.map(item => <a key={item.id} className="flex items-center gap-2 rounded-md border p-3 text-sm hover:bg-muted" href={item.fileUrl} target="_blank" rel="noreferrer"><FileText className="h-4 w-4" /><span className="flex-1 truncate">{item.fileName}</span>{item.isPrimaryEvidence && <Badge variant="secondary">Primary</Badge>}</a>)}
                        <div className="space-y-2 border-t pt-4 text-sm">
                            <div className="flex justify-between"><span className="text-muted-foreground">Deposit date</span><span>{new Date(deposit.depositDate).toLocaleDateString()}</span></div>
                            <div className="flex justify-between"><span className="text-muted-foreground">Policy</span><span>{deposit.policySnapshot}</span></div>
                            <div className="flex justify-between"><span className="text-muted-foreground">Submitted</span><span>{deposit.submittedAt ? new Date(deposit.submittedAt).toLocaleString() : '—'}</span></div>
                            <div className="flex justify-between"><span className="text-muted-foreground">Posted</span><span>{deposit.postedAt ? new Date(deposit.postedAt).toLocaleString() : '—'}</span></div>
                            <div className="flex justify-between"><span className="text-muted-foreground">Bank confirmation</span><Badge variant={deposit.confirmationStatus === 'Confirmed' ? 'default' : 'secondary'}>{deposit.confirmationStatus}</Badge></div>
                            <div className="flex justify-between"><span className="text-muted-foreground">Reconciliation</span><span>{deposit.reconciliationStatus ?? (deposit.status === 'Posted' ? 'Unreconciled' : 'Not eligible')}</span></div>
                            {deposit.bankConfirmationReference && <div className="flex justify-between gap-4"><span className="text-muted-foreground">Bank reference</span><span className="text-right font-medium">{deposit.bankConfirmationReference}</span></div>}
                            {deposit.bankConfirmationDate && <div className="flex justify-between"><span className="text-muted-foreground">Confirmed date</span><span>{new Date(deposit.bankConfirmationDate).toLocaleDateString()}</span></div>}
                            {deposit.bankConfirmationEvidence && <a className="flex items-center gap-2 rounded-md border p-3 hover:bg-muted" href={deposit.bankConfirmationEvidence.fileUrl} target="_blank" rel="noreferrer"><BadgeCheck className="h-4 w-4" /><span className="flex-1 truncate">{deposit.bankConfirmationEvidence.fileName}</span></a>}
                            {deposit.journalEntryId && <Button className="w-full" variant="outline" asChild><Link href={`/finance/journal-entries/${deposit.journalEntryId}`}>View posting journal</Link></Button>}
                            {deposit.status === 'Posted' && <Button className="w-full" variant="outline" asChild><Link href={`/finance/cash/reconciliation?account=${deposit.bankAccountId}`}>Open bank reconciliation</Link></Button>}
                        </div>
                    </CardContent>
                </Card>
            </div>
            {deposit.status === 'Posted' && deposit.confirmationStatus === 'Pending' && (
                <Card>
                    <CardHeader><CardTitle>Record bank acknowledgement</CardTitle><CardDescription>Capture the bank-issued reference after the approved deposit has been accepted. This does not create another accounting entry.</CardDescription></CardHeader>
                    <CardContent className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-2"><Label htmlFor="bank-confirmation-reference">Bank confirmation reference</Label><Input id="bank-confirmation-reference" maxLength={100} value={confirmationForm.bankConfirmationReference} onChange={event => setConfirmationForm(current => ({ ...current, bankConfirmationReference: event.target.value }))} placeholder="Stamped slip / bank advice reference" /></div>
                        <div className="space-y-2"><Label htmlFor="bank-confirmation-date">Confirmation date</Label><Input id="bank-confirmation-date" type="date" min={deposit.depositDate.slice(0, 10)} max={new Date().toISOString().slice(0, 10)} value={confirmationForm.bankConfirmationDate} onChange={event => setConfirmationForm(current => ({ ...current, bankConfirmationDate: event.target.value }))} /></div>
                        <div className="space-y-2 md:col-span-2"><Label>Confirmation evidence (optional)</Label><Button className="w-full justify-start" variant="outline" asChild><label className="cursor-pointer"><Upload className="mr-2 h-4 w-4" />{confirmationEvidenceName || 'Upload bank-stamped slip or advice'}<input className="hidden" type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={uploadConfirmationEvidence} /></label></Button></div>
                        <div className="space-y-2 md:col-span-2"><Label htmlFor="bank-confirmation-notes">Notes</Label><Textarea id="bank-confirmation-notes" maxLength={1000} value={confirmationForm.notes} onChange={event => setConfirmationForm(current => ({ ...current, notes: event.target.value }))} placeholder="Optional confirmation context" /></div>
                        <div className="md:col-span-2"><Button disabled={working || !confirmationForm.bankConfirmationReference.trim() || !confirmationForm.bankConfirmationDate} onClick={() => void confirmDeposit()}><BadgeCheck className="mr-2 h-4 w-4" />Record bank confirmation</Button></div>
                    </CardContent>
                </Card>
            )}
        </div>
    );
}
