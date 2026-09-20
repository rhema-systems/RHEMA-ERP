'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ArrowRight, CheckCircle, Loader2, RefreshCw, RotateCcw, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { financeService } from '@/services/finance.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBook, Currency, CurrencyRevaluationPostingResultDto, CurrencyRevaluationPreviewDto, FinanceSettings, FxRevaluationBatchSummaryDto } from '@/types/finance';
import { getFxRevaluationAccess } from './access';

const emptyGuid = '00000000-0000-0000-0000-000000000000';

function messageFrom(error: unknown, fallback: string) {
    return error instanceof Error && error.message ? error.message : fallback;
}

export default function CurrencyRevaluationPage() {
    const { toast } = useToast();
    const { hasPermission, isLoading: authLoading, error: authError } = useAuth();
    const { canRead, canRun } = getFxRevaluationAccess(hasPermission);
    const [step, setStep] = useState<1 | 2 | 3>(1);
    const [busy, setBusy] = useState(false);
    const [loadingSetup, setLoadingSetup] = useState(true);
    const [setupError, setSetupError] = useState<string | null>(null);
    const [setupReloadKey, setSetupReloadKey] = useState(0);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [accountingBooks, setAccountingBooks] = useState<AccountingBook[]>([]);
    const [preview, setPreview] = useState<CurrencyRevaluationPreviewDto | null>(null);
    const [postedJournal, setPostedJournal] = useState<CurrencyRevaluationPostingResultDto | null>(null);
    const [history, setHistory] = useState<FxRevaluationBatchSummaryDto[]>([]);
    const [historyLoading, setHistoryLoading] = useState(true);
    const [historyError, setHistoryError] = useState<string | null>(null);
    const [reversingBatch, setReversingBatch] = useState<FxRevaluationBatchSummaryDto | null>(null);
    const [reversalDate, setReversalDate] = useState(new Date().toISOString().slice(0, 10));
    const [reversalReason, setReversalReason] = useState('');
    const [parameters, setParameters] = useState({
        revaluationDate: new Date().toISOString().slice(0, 10),
        currencyCode: 'all',
        revaluationType: 'Month-End',
        accountingBookCode: '',
    });

    useEffect(() => {
        if (authLoading) return;
        if (!canRead) {
            setLoadingSetup(false);
            return;
        }
        let active = true;
        setLoadingSetup(true);
        setSetupError(null);
        void Promise.all([
            financeDataService.getFinanceSettings(),
            financeDataService.getCurrencies({ isActive: true }),
            financeDataService.getAccountingBooks(false),
        ])
            .then(([financeSettings, activeCurrencies, books]) => {
                if (!active) return;
                setSettings(financeSettings);
                setCurrencies(activeCurrencies.filter(currency => !currency.isBaseCurrency));
                const postingBooks = books.filter(book => book.isActive && book.allowsPosting);
                setAccountingBooks(postingBooks);
                setParameters(current => ({
                    ...current,
                    accountingBookCode: current.accountingBookCode || postingBooks.find(book => book.isDefault)?.code || '',
                }));
            })
            .catch(error => {
                if (!active) return;
                setSetupError(messageFrom(error, 'Refresh the page and verify your Finance permissions.'));
            })
            .finally(() => { if (active) setLoadingSetup(false); });
        return () => { active = false; };
    }, [authLoading, canRead, setupReloadKey]);

    const request = useMemo(() => ({
        revaluationDate: parameters.revaluationDate,
        revaluationType: parameters.revaluationType,
        accountingBookCode: parameters.accountingBookCode,
        currencyCode: parameters.currencyCode === 'all' ? undefined : parameters.currencyCode,
        unrealizedGainLossAccountId: settings?.unrealizedFxGainAccountId || settings?.unrealizedGainLossAccountId || emptyGuid,
    }), [parameters, settings]);

    const configurationReady = Boolean(
        (settings?.unrealizedFxGainAccountId || settings?.unrealizedGainLossAccountId)
        && settings?.unrealizedFxLossAccountId,
    );

    const loadHistory = useCallback(async () => {
        if (!canRead) return;
        const year = new Date().getFullYear();
        setHistoryLoading(true);
        setHistoryError(null);
        try {
            setHistory(await financeService.getRevaluationHistory(`${year}-01-01`, `${year}-12-31`));
        } catch (error) {
            setHistoryError(messageFrom(error, 'Revaluation history could not be loaded.'));
        } finally {
            setHistoryLoading(false);
        }
    }, [canRead]);

    useEffect(() => {
        if (!authLoading && canRead) void loadHistory();
    }, [authLoading, canRead, loadHistory]);

    const handlePreview = async () => {
        try {
            setBusy(true);
            setPreview(await financeService.previewRevaluation(request));
            setPostedJournal(null);
            setStep(2);
        } catch (error) {
            toast({
                title: 'Live revaluation preview failed',
                description: messageFrom(error, 'Verify the closing rate, open period and FX account configuration.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    const handlePost = async () => {
        if (!canRun) return;
        try {
            setBusy(true);
            const journal = await financeService.runRevaluation({
                ...request,
                previewOnly: false,
                expectedPreviewFingerprint: preview?.previewFingerprint,
            });
            setPostedJournal(journal);
            await loadHistory();
            setStep(3);
            toast({ title: 'FX revaluation posted', description: journal.journalEntryNumber });
        } catch (error) {
            toast({
                title: 'Revaluation posting failed',
                description: messageFrom(error, 'The posting engine rejected the revaluation.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    const handleReverse = async () => {
        if (!canRun || !reversingBatch || reversalReason.trim().length < 5) return;
        try {
            setBusy(true);
            await financeService.reverseRevaluation(reversingBatch.id, reversalDate, reversalReason.trim());
            await loadHistory();
            setReversingBatch(null);
            setReversalReason('');
            toast({ title: 'Revaluation reversed', description: `${reversingBatch.batchNumber} now has a linked reversal journal.` });
        } catch (error) {
            toast({ title: 'Reversal failed', description: messageFrom(error, 'The reversal posting was rejected.'), variant: 'destructive' });
        } finally {
            setBusy(false);
        }
    };

    const reset = () => {
        setPreview(null);
        setPostedJournal(null);
        setStep(1);
    };

    const formatMoney = (amount: number, currency = settings?.baseCurrency || 'GHS') =>
        new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(amount);

    if (authLoading) {
        return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-8 w-8 animate-spin" /><span className="ml-3">Checking Finance access…</span></div>;
    }

    if (authError) {
        return <Alert variant="destructive"><TriangleAlert className="h-4 w-4" /><AlertTitle>Finance access could not be checked</AlertTitle><AlertDescription>Refresh the page to retry authentication and permission loading.</AlertDescription></Alert>;
    }

    if (!canRead) {
        return <Alert variant="destructive"><TriangleAlert className="h-4 w-4" /><AlertTitle>Permission denied</AlertTitle><AlertDescription>Finance.Read is required to view revaluation previews and history.</AlertDescription></Alert>;
    }

    return (
        <div className="space-y-6">
            <div>
                <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight"><RefreshCw className="h-8 w-8" />Currency Revaluation</h1>
                <p className="text-muted-foreground">Preview live posted FX exposures and post the controlled unrealized gain/loss journal.</p>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Currency Revaluation</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="mx-auto flex max-w-2xl items-center justify-center gap-4">
                {(['Parameters', 'Live preview', 'Posted'] as const).map((label, index) => {
                    const number = (index + 1) as 1 | 2 | 3;
                    return <div key={label} className="contents">
                        {index > 0 ? <div className={`h-0.5 w-14 ${step >= number ? 'bg-primary' : 'bg-muted'}`} /> : null}
                        <div className={`flex items-center gap-2 ${step >= number ? 'font-semibold text-primary' : 'text-muted-foreground'}`}>
                            <span className={`flex h-8 w-8 items-center justify-center rounded-full border-2 ${step >= number ? 'border-primary bg-primary text-primary-foreground' : ''}`}>{number}</span>
                            <span className="hidden sm:inline">{label}</span>
                        </div>
                    </div>;
                })}
            </div>

            {setupError ? <Alert variant="destructive">
                <TriangleAlert className="h-4 w-4" /><AlertTitle>Revaluation setup could not be loaded</AlertTitle>
                <AlertDescription className="flex items-center justify-between gap-4"><span>{setupError}</span><Button variant="outline" size="sm" onClick={() => setSetupReloadKey(value => value + 1)}>Retry setup</Button></AlertDescription>
            </Alert> : null}

            {step === 1 ? <Card className="mx-auto max-w-4xl">
                <CardHeader>
                    <CardTitle>Revaluation parameters</CardTitle>
                    <CardDescription>The preview reads posted monetary account/currency exposures selected by policy. It does not create accounting entries.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                    {!loadingSetup && !configurationReady ? <Alert variant="destructive">
                        <TriangleAlert className="h-4 w-4" /><AlertTitle>FX gain/loss configuration incomplete</AlertTitle>
                        <AlertDescription>Configure separate unrealized FX gain and loss accounts in Finance Settings before posting.</AlertDescription>
                    </Alert> : null}
                    <div className="grid gap-4 md:grid-cols-4">
                        <div className="space-y-2">
                            <Label>Accounting book</Label>
                            <Select value={parameters.accountingBookCode} onValueChange={value => setParameters(current => ({ ...current, accountingBookCode: value }))}>
                                <SelectTrigger><SelectValue placeholder="Select exact book" /></SelectTrigger>
                                <SelectContent>{accountingBooks.map(book => <SelectItem key={book.id} value={book.code}>{book.code} — {book.name}</SelectItem>)}</SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="revaluation-date">Revaluation date</Label>
                            <Input id="revaluation-date" type="date" value={parameters.revaluationDate} onChange={event => setParameters(current => ({ ...current, revaluationDate: event.target.value }))} />
                        </div>
                        <div className="space-y-2">
                            <Label>Closing rate type</Label>
                            <Select value={parameters.revaluationType} onValueChange={value => setParameters(current => ({ ...current, revaluationType: value }))}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent><SelectItem value="Month-End">Month End</SelectItem><SelectItem value="Quarter-End">Quarter End</SelectItem><SelectItem value="Year-End">Year End</SelectItem></SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Currency scope</Label>
                            <Select value={parameters.currencyCode} onValueChange={value => setParameters(current => ({ ...current, currencyCode: value }))}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All foreign currencies</SelectItem>
                                    {currencies.map(currency => <SelectItem key={currency.id} value={currency.currencyCode}>{currency.currencyCode} — {currency.currencyName}</SelectItem>)}
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                    <Alert><TriangleAlert className="h-4 w-4" /><AlertTitle>Live accounting data</AlertTitle><AlertDescription>Only posted transactions dated on or before the selected date are included. A matching approved closing rate and an open fiscal period are required.</AlertDescription></Alert>
                    <div className="flex justify-end"><Button onClick={handlePreview} disabled={busy || loadingSetup || Boolean(setupError) || !parameters.revaluationDate || !parameters.accountingBookCode}>
                        {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}Preview live exposures{!busy ? <ArrowRight className="ml-2 h-4 w-4" /> : null}
                    </Button></div>
                </CardContent>
            </Card> : null}

            {step === 2 && preview ? <div className="space-y-5">
                <div className="grid gap-3 sm:grid-cols-4">
                    <Card><CardHeader className="pb-2"><CardDescription>Exposures</CardDescription><CardTitle>{preview.exposureCount}</CardTitle></CardHeader></Card>
                    <Card><CardHeader className="pb-2"><CardDescription>Total gains</CardDescription><CardTitle className="text-green-600">{formatMoney(preview.totalGainAmount, preview.functionalCurrencyCode)}</CardTitle></CardHeader></Card>
                    <Card><CardHeader className="pb-2"><CardDescription>Total losses</CardDescription><CardTitle className="text-red-600">{formatMoney(preview.totalLossAmount, preview.functionalCurrencyCode)}</CardTitle></CardHeader></Card>
                    <Card><CardHeader className="pb-2"><CardDescription>Net gain/(loss)</CardDescription><CardTitle>{formatMoney(preview.netGainLossAmount, preview.functionalCurrencyCode)}</CardTitle></CardHeader></Card>
                </div>
                <Card>
                    <CardHeader><CardTitle>Live revaluation preview</CardTitle><CardDescription>{preview.batchNumber} · {preview.accountingBookCode} — {preview.accountingBookName} · As at {new Date(preview.revaluationDate).toLocaleDateString()}</CardDescription></CardHeader>
                    <CardContent className="space-y-5">
                        {preview.lines.length === 0 ? <Alert><CheckCircle className="h-4 w-4" /><AlertTitle>No adjustment required</AlertTitle><AlertDescription>No eligible posted foreign-currency exposure produced a gain or loss at the selected closing rate.</AlertDescription></Alert> :
                            <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm">
                                <thead className="bg-muted/50"><tr><th className="p-3 text-left">Account</th><th className="p-3 text-left">Source</th><th className="p-3 text-left">Currency</th><th className="p-3 text-left">Policy</th><th className="p-3 text-right">Foreign balance</th><th className="p-3 text-right">Carrying value</th><th className="p-3 text-right">Previous rate</th><th className="p-3 text-right">Closing rate</th><th className="p-3 text-right">Revalued value</th><th className="p-3 text-right">Adjustment</th></tr></thead>
                                <tbody>{preview.lines.map(line => <tr key={`${line.accountId}-${line.transactionCurrency}-${line.sourceModule}`} className={line.hasGovernanceWarning ? 'border-t bg-amber-50' : 'border-t'}>
                                    <td className="p-3"><div className="font-mono font-semibold">{line.accountNumber}</div><div className="text-xs text-muted-foreground">{line.accountName}</div></td>
                                    <td className="p-3"><Badge variant="outline">{line.sourceModule}</Badge></td><td className="p-3 font-medium">{line.transactionCurrency}</td>
                                    <td className="p-3"><div className="whitespace-nowrap">{line.accountClassificationCode} · {line.effectivePolicySource}</div><div className="text-xs text-muted-foreground whitespace-nowrap">{line.rateType} / {line.quoteSide}</div>{line.hasGovernanceWarning && <div className="mt-1 max-w-xs font-semibold text-amber-900">{line.governanceWarning || 'Non-standard revaluation policy'}</div>}</td>
                                    <td className="p-3 text-right font-mono">{line.foreignCurrencyBalance.toLocaleString()}</td><td className="p-3 text-right font-mono">{line.carryingFunctionalAmount.toLocaleString()}</td>
                                    <td className="p-3 text-right font-mono">{line.previousRate.toFixed(6)}</td><td className="p-3 text-right font-mono">{line.closingExchangeRate.toFixed(6)}</td><td className="p-3 text-right font-mono">{line.revaluedFunctionalAmount.toLocaleString()}</td>
                                    <td className="p-3 text-right"><span className={line.gainLossType === 'Gain' ? 'font-semibold text-green-600' : 'font-semibold text-red-600'}>{formatMoney(Math.abs(line.gainLossAmount), line.functionalCurrencyCode)}</span><Badge variant="outline" className="ml-2">{line.gainLossType}</Badge></td>
                                </tr>)}</tbody>
                            </table></div>}
                        <div className="flex justify-between"><Button variant="outline" onClick={() => setStep(1)}>Back</Button>{canRun ? <Button onClick={handlePost} disabled={busy || preview.lines.length === 0 || !configurationReady}>{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}Post revaluation journal</Button> : <p className="self-center text-sm text-muted-foreground">Finance.FX.Revaluation.Run is required to post.</p>}</div>
                    </CardContent>
                </Card>
            </div> : null}

            {step === 3 && postedJournal ? <Card className="mx-auto max-w-xl text-center"><CardContent className="space-y-4 py-10">
                <CheckCircle className="mx-auto h-16 w-16 text-green-500" /><h2 className="text-2xl font-bold">Revaluation posted</h2>
                <p className="text-muted-foreground">The live posting engine created journal <strong>{postedJournal.journalEntryNumber}</strong>.</p>
                <div className="grid grid-cols-2 gap-3 rounded-md border p-4 text-left"><div><div className="text-xs text-muted-foreground">Posting status</div><div className="font-semibold">{postedJournal.postingStatus}</div></div><div><div className="text-xs text-muted-foreground">Journal total</div><div className="font-semibold">{formatMoney(postedJournal.totalDebitAmount)}</div></div></div>
                <div className="flex justify-center gap-3 pt-2"><Button variant="outline" onClick={reset}><RotateCcw className="mr-2 h-4 w-4" />Start another</Button><Button asChild><a href={`/finance/journal-entries/${postedJournal.id}`}>View journal entry</a></Button></div>
            </CardContent></Card> : null}

            <Card>
                <CardHeader><CardTitle>Revaluation history</CardTitle><CardDescription>Posted batches for the current calendar year, including their journals and reversals.</CardDescription></CardHeader>
                <CardContent>
                    {historyLoading ? <div className="flex items-center justify-center py-6 text-sm text-muted-foreground"><Loader2 className="mr-2 h-4 w-4 animate-spin" />Loading revaluation history…</div> :
                    historyError ? <Alert variant="destructive"><TriangleAlert className="h-4 w-4" /><AlertTitle>Revaluation history could not be loaded</AlertTitle><AlertDescription className="flex items-center justify-between gap-4"><span>{historyError}</span><Button variant="outline" size="sm" onClick={() => void loadHistory()}>Retry history</Button></AlertDescription></Alert> :
                    history.length === 0 ? <p className="py-6 text-center text-sm text-muted-foreground">No revaluation batches found this year.</p> :
                        <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm">
                            <thead className="bg-muted/50"><tr><th className="p-3 text-left">Batch</th><th className="p-3 text-left">Date</th><th className="p-3 text-left">Currencies</th><th className="p-3 text-right">Exposures</th><th className="p-3 text-right">Net gain/(loss)</th><th className="p-3 text-left">Status</th><th className="p-3 text-right">Actions</th></tr></thead>
                            <tbody>{history.map(batch => <tr key={batch.id} className="border-t">
                                <td className="p-3 font-mono font-semibold">{batch.batchNumber}</td>
                                <td className="p-3">{new Date(batch.revaluationDate).toLocaleDateString()}</td>
                                <td className="p-3">{batch.currencies.join(', ')}</td>
                                <td className="p-3 text-right">{batch.exposureCount}{batch.nonstandardPolicyCount > 0 && <Badge className="ml-2 border-amber-500 bg-amber-100 text-amber-900">{batch.nonstandardPolicyCount} non-standard</Badge>}</td>
                                <td className="p-3 text-right font-mono">{formatMoney(batch.netGainLossAmount, batch.functionalCurrencyCode)}</td>
                                <td className="p-3"><Badge variant={batch.status === 'Reversed' ? 'secondary' : 'default'}>{batch.status}</Badge></td>
                                <td className="p-3"><div className="flex justify-end gap-2">
                                    {batch.journalEntryId ? <Button size="sm" variant="outline" asChild><a href={`/finance/journal-entries/${batch.journalEntryId}`}>{batch.journalEntryNumber || 'Journal'}</a></Button> : null}
                                    {batch.reversalJournalEntryId ? <Button size="sm" variant="outline" asChild><a href={`/finance/journal-entries/${batch.reversalJournalEntryId}`}>Reversal</a></Button> : null}
                                    {canRun && batch.status === 'Posted' ? <Button size="sm" variant="destructive" onClick={() => setReversingBatch(batch)}>Reverse</Button> : null}
                                </div></td>
                            </tr>)}</tbody>
                        </table></div>}
                </CardContent>
            </Card>

            <Dialog open={canRun && Boolean(reversingBatch)} onOpenChange={open => { if (!open) setReversingBatch(null); }}>
                <DialogContent>
                    <DialogHeader><DialogTitle>Reverse {reversingBatch?.batchNumber}</DialogTitle><DialogDescription>A balanced reversal journal will be posted through the Finance posting engine. The original journal remains in the audit trail.</DialogDescription></DialogHeader>
                    <div className="space-y-4 py-3">
                        <div className="space-y-2"><Label htmlFor="reversal-date">Reversal date</Label><Input id="reversal-date" type="date" value={reversalDate} onChange={event => setReversalDate(event.target.value)} /></div>
                        <div className="space-y-2"><Label htmlFor="reversal-reason">Reason</Label><Input id="reversal-reason" value={reversalReason} onChange={event => setReversalReason(event.target.value)} placeholder="Why is this revaluation being reversed?" /></div>
                    </div>
                    <DialogFooter><Button variant="outline" onClick={() => setReversingBatch(null)}>Cancel</Button><Button variant="destructive" onClick={handleReverse} disabled={busy || !reversalDate || reversalReason.trim().length < 5}>{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}Post reversal</Button></DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
