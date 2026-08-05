'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
    ArrowLeft,
    CheckCircle2,
    ClipboardCheck,
    Loader2,
    RefreshCw,
    RotateCcw,
    Send,
    WalletCards,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { CashierTillSession, LiquidityAccount } from '@/types/cash-management';

// Ghana's current note and common coin denominations. Quantities remain editable, and the server
// recalculates every line/total; this list is only a cashier-friendly input aid, not accounting data.
const denominations = [200, 100, 50, 20, 10, 5, 2, 1, 0.5, 0.2, 0.1, 0.05];

const money = (value: number, currency = 'GHS') =>
    `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const errorMessage = (error: unknown, fallback: string) =>
    error instanceof Error ? error.message : fallback;

export default function CashierTillSessionsPage() {
    const { hasPermission } = useAuth();
    const canOperate = hasPermission('Finance.CashTills.Operate');
    const canReview = hasPermission('Finance.CashTills.Closures.Review');
    const canReopen = hasPermission('Finance.CashTills.Sessions.Reopen');

    const [sessions, setSessions] = useState<CashierTillSession[]>([]);
    const [tills, setTills] = useState<LiquidityAccount[]>([]);
    const [selected, setSelected] = useState<CashierTillSession | null>(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [tillId, setTillId] = useState('');
    const [businessDate, setBusinessDate] = useState(new Date().toISOString().slice(0, 10));
    const [openingFloat, setOpeningFloat] = useState('0');
    const [openingNotes, setOpeningNotes] = useState('');
    const [openingEvidence, setOpeningEvidence] = useState<File | null>(null);
    const [closingEvidence, setClosingEvidence] = useState<File | null>(null);
    const [counts, setCounts] = useState<Record<string, number>>({});
    const [varianceReason, setVarianceReason] = useState('');
    const [reviewComments, setReviewComments] = useState('');
    const [correctionReason, setCorrectionReason] = useState('');

    const load = useCallback(async (preferredId?: string) => {
        const [sessionRows, accountRows] = await Promise.all([
            cashManagementDataService.getCashierTillSessions(),
            cashManagementDataService.getLiquidityAccounts(true),
        ]);
        setSessions(sessionRows);
        const cashTills = accountRows.filter(account => account.accountType === 'CashTill');
        setTills(cashTills);
        setTillId(current => current || cashTills[0]?.id || '');
        const detailId = preferredId || selected?.id;
        if (detailId) {
            const detail = await cashManagementDataService.getCashierTillSession(detailId);
            setSelected(detail);
        }
    }, [selected?.id]);

    useEffect(() => {
        void load()
            .catch(error => toast.error(errorMessage(error, 'Could not load cashier till sessions.')))
            .finally(() => setLoading(false));
    }, [load]);

    const countedAmount = useMemo(
        () => denominations.reduce((sum, denomination) =>
            sum + denomination * Math.max(0, counts[String(denomination)] || 0), 0),
        [counts],
    );
    const liveVariance = selected ? countedAmount - selected.expectedClosingAmount : 0;
    const pendingCount = sessions.filter(item => item.status === 'PendingReview').length;
    const openExpected = sessions
        .filter(item => item.status === 'Open')
        .reduce((sum, item) => sum + item.expectedClosingAmount, 0);

    const run = async (operation: () => Promise<CashierTillSession>, success: string) => {
        setBusy(true);
        try {
            const result = await operation();
            setSelected(result);
            await load(result.id);
            toast.success(success);
            return result;
        } catch (error) {
            toast.error(errorMessage(error, 'The till operation could not be completed.'));
            return null;
        } finally {
            setBusy(false);
        }
    };

    const openSession = async () => {
        if (!tillId) return toast.error('Select a CashTill liquidity account.');
        const amount = Number(openingFloat);
        if (!Number.isFinite(amount) || amount < 0) return toast.error('Enter a valid opening float.');
        setBusy(true);
        try {
            const openingEvidenceFileId = openingEvidence
                ? await cashManagementDataService.uploadBankingEvidence(openingEvidence)
                : undefined;
            const result = await cashManagementDataService.openCashierTillSession({
                liquidityAccountId: tillId,
                businessDate,
                openingFloatAmount: amount,
                openingNotes: openingNotes.trim() || undefined,
                openingEvidenceFileId,
            });
            setOpeningFloat('0');
            setOpeningNotes('');
            setOpeningEvidence(null);
            setSelected(result);
            await load(result.id);
            toast.success(`Till session ${result.sessionNumber} opened.`);
        } catch (error) {
            toast.error(errorMessage(error, 'Could not open the till.'));
        } finally {
            setBusy(false);
        }
    };

    const submitCount = async () => {
        if (!selected) return;
        setBusy(true);
        try {
            const closingEvidenceFileId = closingEvidence
                ? await cashManagementDataService.uploadBankingEvidence(closingEvidence)
                : undefined;
            const result = await cashManagementDataService.submitCashierTillCount(selected.id, {
                countLines: denominations.map(denomination => ({
                    denomination,
                    quantity: Math.max(0, counts[String(denomination)] || 0),
                })),
                varianceReason: varianceReason.trim() || undefined,
                closingEvidenceFileId,
                rowVersion: selected.rowVersion,
            });
            setCounts({});
            setVarianceReason('');
            setClosingEvidence(null);
            setSelected(result);
            await load(result.id);
            toast.success('Denomination count submitted for independent review.');
        } catch (error) {
            toast.error(errorMessage(error, 'Could not submit the till count.'));
        } finally {
            setBusy(false);
        }
    };

    const selectSession = async (id: string) => {
        setBusy(true);
        try {
            setSelected(await cashManagementDataService.getCashierTillSession(id));
            setCounts({});
            setVarianceReason('');
            setReviewComments('');
            setCorrectionReason('');
        } catch (error) {
            toast.error(errorMessage(error, 'Could not load the till session.'));
        } finally {
            setBusy(false);
        }
    };

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                    <Button variant="ghost" size="icon" asChild>
                        <Link href="/finance/cash"><ArrowLeft className="h-4 w-4" /></Link>
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold">Cashier Till Sessions</h1>
                        <p className="text-muted-foreground">
                            Control physical custody, denomination counts, variances, and independent closure.
                        </p>
                    </div>
                </div>
                <Button variant="outline" onClick={() => void load()} disabled={busy}>
                    <RefreshCw className="mr-2 h-4 w-4" />Refresh
                </Button>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Open tills</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{sessions.filter(item => item.status === 'Open').length}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Live expected cash</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{money(openExpected)}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Awaiting review</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{pendingCount}</CardContent></Card>
            </div>

            {canOperate && (
                <Card>
                    <CardHeader><CardTitle>Open cashier custody</CardTitle><CardDescription>Opening float is a physical-count baseline and does not create a GL entry.</CardDescription></CardHeader>
                    <CardContent className="grid gap-4 md:grid-cols-5">
                        <div className="space-y-2 md:col-span-2"><Label>Cash till</Label><Select value={tillId} onValueChange={setTillId}><SelectTrigger><SelectValue placeholder="Select till" /></SelectTrigger><SelectContent>{tills.map(till => <SelectItem key={till.id} value={till.id}>{till.code} — {till.name}</SelectItem>)}</SelectContent></Select></div>
                        <div className="space-y-2"><Label>Business date</Label><Input type="date" value={businessDate} onChange={event => setBusinessDate(event.target.value)} /></div>
                        <div className="space-y-2"><Label>Opening float (GHS)</Label><Input type="number" min="0" step="0.01" value={openingFloat} onChange={event => setOpeningFloat(event.target.value)} /></div>
                        <div className="space-y-2"><Label>Opening evidence</Label><Input type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={event => setOpeningEvidence(event.target.files?.[0] || null)} /></div>
                        <div className="space-y-2 md:col-span-4"><Label>Opening notes</Label><Input value={openingNotes} onChange={event => setOpeningNotes(event.target.value)} placeholder="Float source, handover reference, or custody note" /></div>
                        <div className="flex items-end"><Button className="w-full" disabled={busy || !tillId} onClick={() => void openSession()}>{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <WalletCards className="mr-2 h-4 w-4" />}Open till</Button></div>
                    </CardContent>
                </Card>
            )}

            <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(420px,1.25fr)]">
                <Card>
                    <CardHeader><CardTitle>Session register</CardTitle><CardDescription>Up to the latest 500 tenant sessions.</CardDescription></CardHeader>
                    <CardContent>
                        {loading ? <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin" /></div> : sessions.length === 0 ? (
                            <div className="py-12 text-center text-muted-foreground"><WalletCards className="mx-auto mb-3 h-9 w-9" />No till sessions found.</div>
                        ) : (
                            <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="border-b text-left text-muted-foreground"><tr><th className="p-3">Session</th><th className="p-3">Till / cashier</th><th className="p-3 text-right">Expected</th><th className="p-3 text-right">Variance</th><th className="p-3">Status</th></tr></thead><tbody>{sessions.map(item => (
                                <tr key={item.id} onClick={() => void selectSession(item.id)} className={`cursor-pointer border-b hover:bg-muted/40 ${selected?.id === item.id ? 'bg-muted/60' : ''}`}>
                                    <td className="p-3"><div className="font-medium text-primary">{item.sessionNumber}</div><div className="text-xs text-muted-foreground">{new Date(item.businessDate).toLocaleDateString()}</div></td>
                                    <td className="p-3"><div>{item.tillName}</div><div className="text-xs text-muted-foreground">{item.cashierName}</div></td>
                                    <td className="p-3 text-right">{money(item.expectedClosingAmount, item.currency)}</td>
                                    <td className={`p-3 text-right ${item.varianceAmount < 0 ? 'text-red-600' : item.varianceAmount > 0 ? 'text-amber-600' : ''}`}>{item.status === 'Open' ? '—' : money(item.varianceAmount, item.currency)}</td>
                                    <td className="p-3"><Badge variant={item.status === 'Closed' ? 'default' : item.status === 'PendingReview' ? 'secondary' : 'outline'}>{item.status}</Badge></td>
                                </tr>
                            ))}</tbody></table></div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader><CardTitle>{selected ? selected.sessionNumber : 'Session detail'}</CardTitle><CardDescription>{selected ? `${selected.tillCode} — ${selected.tillName}` : 'Select a session from the register.'}</CardDescription></CardHeader>
                    <CardContent className="space-y-5">
                        {!selected ? <div className="py-16 text-center text-muted-foreground"><ClipboardCheck className="mx-auto mb-3 h-9 w-9" />No session selected.</div> : (
                            <>
                                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                                    <div className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">Opening float</div><div className="font-semibold">{money(selected.openingFloatAmount, selected.currency)}</div></div>
                                    <div className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">Transactions</div><div className="font-semibold">{money(selected.transactionMovementAmount, selected.currency)}</div></div>
                                    <div className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">Posted deposits</div><div className="font-semibold">{money(selected.depositedAmount, selected.currency)}</div></div>
                                    <div className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">Expected cash</div><div className="font-semibold">{money(selected.expectedClosingAmount, selected.currency)}</div></div>
                                </div>
                                <div className="text-sm text-muted-foreground">Custody entries: {selected.custodyEntryCount} · Opened {new Date(selected.openedAt).toLocaleString()} by {selected.cashierName}</div>

                                {selected.status === 'Open' && canOperate && (
                                    <div className="space-y-4 rounded-lg border p-4">
                                        <div><h3 className="font-semibold">Denomination count</h3><p className="text-sm text-muted-foreground">The server calculates counted cash and freezes activity at submission.</p></div>
                                        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">{denominations.map(denomination => (
                                            <div key={denomination} className="space-y-1"><Label className="text-xs">GHS {denomination.toFixed(2)}</Label><Input type="number" min="0" step="1" value={counts[String(denomination)] || 0} onChange={event => setCounts(current => ({ ...current, [String(denomination)]: Math.max(0, Number(event.target.value) || 0) }))} /></div>
                                        ))}</div>
                                        <div className="grid gap-3 sm:grid-cols-3"><div className="rounded-lg bg-muted p-3"><div className="text-xs text-muted-foreground">Counted</div><div className="font-semibold">{money(countedAmount, selected.currency)}</div></div><div className="rounded-lg bg-muted p-3"><div className="text-xs text-muted-foreground">Expected</div><div className="font-semibold">{money(selected.expectedClosingAmount, selected.currency)}</div></div><div className="rounded-lg bg-muted p-3"><div className="text-xs text-muted-foreground">Live variance</div><div className={`font-semibold ${liveVariance < 0 ? 'text-red-600' : liveVariance > 0 ? 'text-amber-600' : ''}`}>{money(liveVariance, selected.currency)}</div></div></div>
                                        <div className="space-y-2"><Label>Variance reason {Math.abs(liveVariance) >= 0.01 ? '(required)' : ''}</Label><Textarea value={varianceReason} onChange={event => setVarianceReason(event.target.value)} placeholder="Explain every surplus or shortage." /></div>
                                        <div className="space-y-2"><Label>Closing count evidence</Label><Input type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={event => setClosingEvidence(event.target.files?.[0] || null)} /></div>
                                        <Button disabled={busy} onClick={() => void submitCount()}>{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}Submit count</Button>
                                    </div>
                                )}

                                {selected.status === 'PendingReview' && (
                                    <div className="space-y-4 rounded-lg border p-4">
                                        <div className="grid gap-3 sm:grid-cols-3"><div><div className="text-xs text-muted-foreground">Counted</div><div className="font-semibold">{money(selected.countedClosingAmount, selected.currency)}</div></div><div><div className="text-xs text-muted-foreground">Variance</div><div className="font-semibold">{money(selected.varianceAmount, selected.currency)}</div></div><div><div className="text-xs text-muted-foreground">Threshold</div><div className="font-semibold">{money(selected.varianceApprovalThresholdAmount, selected.currency)}</div></div></div>
                                        {selected.varianceReason && <div className="rounded bg-muted p-3 text-sm"><strong>Cashier explanation:</strong> {selected.varianceReason}</div>}
                                        {canReview ? <><div className="space-y-2"><Label>Reviewer comments</Label><Textarea value={reviewComments} onChange={event => setReviewComments(event.target.value)} placeholder="Record evidence checked and disposition." /></div><div className="flex flex-wrap gap-2"><Button disabled={busy} onClick={() => void run(() => cashManagementDataService.approveCashierTillClosure(selected.id, reviewComments, selected.rowVersion), 'Till closure approved.')}><CheckCircle2 className="mr-2 h-4 w-4" />Approve closure</Button><Button variant="outline" disabled={busy || !reviewComments.trim()} onClick={() => void run(() => cashManagementDataService.returnCashierTillForRecount(selected.id, reviewComments, selected.rowVersion), 'Till returned for recount.')}><RotateCcw className="mr-2 h-4 w-4" />Return for recount</Button></div></> : <p className="text-sm text-muted-foreground">A user with till-closure review permission must complete this session.</p>}
                                    </div>
                                )}

                                {selected.status === 'Closed' && (
                                    <div className="space-y-4 rounded-lg border p-4"><div className="flex items-center gap-2 font-semibold text-emerald-700"><CheckCircle2 className="h-5 w-5" />Independently closed</div><div className="text-sm">Counted {money(selected.countedClosingAmount, selected.currency)} · variance {money(selected.varianceAmount, selected.currency)}</div>{selected.reviewComments && <div className="rounded bg-muted p-3 text-sm"><strong>Review:</strong> {selected.reviewComments}</div>}{canReopen && <><div className="space-y-2"><Label>Correction reason (minimum 20 characters)</Label><Textarea value={correctionReason} onChange={event => setCorrectionReason(event.target.value)} placeholder="Explain why a linked correction session is required." /></div><Button variant="outline" disabled={busy || correctionReason.trim().length < 20} onClick={() => void run(() => cashManagementDataService.reopenCashierTillAsCorrection(selected.id, correctionReason, selected.rowVersion), 'Linked correction session opened.')}><RotateCcw className="mr-2 h-4 w-4" />Open correction session</Button></>}</div>
                                )}

                                {selected.custodyEntries.length > 0 && <div className="space-y-2"><h3 className="font-semibold">Canonical custody activity</h3><div className="max-h-64 overflow-auto rounded border"><table className="w-full text-xs"><thead className="sticky top-0 bg-background text-left"><tr><th className="p-2">Recorded</th><th className="p-2">Entry / source</th><th className="p-2 text-right">Movement</th></tr></thead><tbody>{selected.custodyEntries.map(entry => <tr key={entry.id} className="border-t"><td className="p-2">{new Date(entry.recordedAt).toLocaleString()}</td><td className="p-2"><div>{entry.entryNumber}</div><div className="text-muted-foreground">{entry.sourceDocumentType} · {entry.referenceNumber || entry.sourceDocumentId}</div></td><td className={`p-2 text-right ${entry.signedAmount < 0 ? 'text-red-600' : 'text-emerald-700'}`}>{money(entry.signedAmount, selected.currency)}</td></tr>)}</tbody></table></div></div>}
                            </>
                        )}
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
