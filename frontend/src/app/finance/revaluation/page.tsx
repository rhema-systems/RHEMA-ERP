'use client';

import { useEffect, useMemo, useState } from 'react';
import { ArrowRight, CheckCircle, Loader2, RefreshCw, RotateCcw, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { financeService } from '@/services/finance.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Currency, CurrencyRevaluationPreviewDto, FinanceSettings, JournalEntry } from '@/types/finance';

const emptyGuid = '00000000-0000-0000-0000-000000000000';

function messageFrom(error: unknown, fallback: string) {
    return error instanceof Error && error.message ? error.message : fallback;
}

export default function CurrencyRevaluationPage() {
    const { toast } = useToast();
    const [step, setStep] = useState<1 | 2 | 3>(1);
    const [busy, setBusy] = useState(false);
    const [loadingSetup, setLoadingSetup] = useState(true);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [preview, setPreview] = useState<CurrencyRevaluationPreviewDto | null>(null);
    const [postedJournal, setPostedJournal] = useState<JournalEntry | null>(null);
    const [parameters, setParameters] = useState({
        revaluationDate: new Date().toISOString().slice(0, 10),
        currencyCode: 'all',
        revaluationType: 'Month-End',
    });

    useEffect(() => {
        let active = true;
        void Promise.all([
            financeDataService.getFinanceSettings(),
            financeDataService.getCurrencies({ isActive: true }),
        ])
            .then(([financeSettings, activeCurrencies]) => {
                if (!active) return;
                setSettings(financeSettings);
                setCurrencies(activeCurrencies.filter(currency => !currency.isBaseCurrency));
            })
            .catch(error => toast({
                title: 'Revaluation setup could not be loaded',
                description: messageFrom(error, 'Refresh the page and verify your Finance permissions.'),
                variant: 'destructive',
            }))
            .finally(() => { if (active) setLoadingSetup(false); });
        return () => { active = false; };
    }, [toast]);

    const request = useMemo(() => ({
        revaluationDate: parameters.revaluationDate,
        revaluationType: parameters.revaluationType,
        currencyCode: parameters.currencyCode === 'all' ? undefined : parameters.currencyCode,
        unrealizedGainLossAccountId: settings?.unrealizedFxGainAccountId || settings?.unrealizedGainLossAccountId || emptyGuid,
    }), [parameters, settings]);

    const configurationReady = Boolean(
        (settings?.unrealizedFxGainAccountId || settings?.unrealizedGainLossAccountId)
        && settings?.unrealizedFxLossAccountId,
    );

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
        try {
            setBusy(true);
            const journal = await financeService.runRevaluation({ ...request, previewOnly: false });
            setPostedJournal(journal);
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

    const reset = () => {
        setPreview(null);
        setPostedJournal(null);
        setStep(1);
    };

    const formatMoney = (amount: number, currency = settings?.baseCurrency || 'GHS') =>
        new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(amount);

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

            {step === 1 ? <Card className="mx-auto max-w-4xl">
                <CardHeader>
                    <CardTitle>Revaluation parameters</CardTitle>
                    <CardDescription>The preview reads current posted AR, AP and foreign-bank exposures. It does not create accounting entries.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                    {!loadingSetup && !configurationReady ? <Alert variant="destructive">
                        <TriangleAlert className="h-4 w-4" /><AlertTitle>FX gain/loss configuration incomplete</AlertTitle>
                        <AlertDescription>Configure separate unrealized FX gain and loss accounts in Finance Settings before posting.</AlertDescription>
                    </Alert> : null}
                    <div className="grid gap-4 md:grid-cols-3">
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
                    <div className="flex justify-end"><Button onClick={handlePreview} disabled={busy || loadingSetup || !parameters.revaluationDate}>
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
                    <CardHeader><CardTitle>Live revaluation preview</CardTitle><CardDescription>{preview.batchNumber} · As at {new Date(preview.revaluationDate).toLocaleDateString()}</CardDescription></CardHeader>
                    <CardContent className="space-y-5">
                        {preview.lines.length === 0 ? <Alert><CheckCircle className="h-4 w-4" /><AlertTitle>No adjustment required</AlertTitle><AlertDescription>No eligible posted foreign-currency exposure produced a gain or loss at the selected closing rate.</AlertDescription></Alert> :
                            <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm">
                                <thead className="bg-muted/50"><tr><th className="p-3 text-left">Account</th><th className="p-3 text-left">Source</th><th className="p-3 text-left">Currency</th><th className="p-3 text-right">Foreign balance</th><th className="p-3 text-right">Carrying value</th><th className="p-3 text-right">Previous rate</th><th className="p-3 text-right">Closing rate</th><th className="p-3 text-right">Revalued value</th><th className="p-3 text-right">Adjustment</th></tr></thead>
                                <tbody>{preview.lines.map(line => <tr key={`${line.accountId}-${line.transactionCurrency}-${line.sourceModule}`} className="border-t">
                                    <td className="p-3"><div className="font-mono font-semibold">{line.accountNumber}</div><div className="text-xs text-muted-foreground">{line.accountName}</div></td>
                                    <td className="p-3"><Badge variant="outline">{line.sourceModule}</Badge></td><td className="p-3 font-medium">{line.transactionCurrency}</td>
                                    <td className="p-3 text-right font-mono">{line.foreignCurrencyBalance.toLocaleString()}</td><td className="p-3 text-right font-mono">{line.carryingFunctionalAmount.toLocaleString()}</td>
                                    <td className="p-3 text-right font-mono">{line.previousRate.toFixed(6)}</td><td className="p-3 text-right font-mono">{line.closingExchangeRate.toFixed(6)}</td><td className="p-3 text-right font-mono">{line.revaluedFunctionalAmount.toLocaleString()}</td>
                                    <td className="p-3 text-right"><span className={line.gainLossType === 'Gain' ? 'font-semibold text-green-600' : 'font-semibold text-red-600'}>{formatMoney(Math.abs(line.gainLossAmount), line.functionalCurrencyCode)}</span><Badge variant="outline" className="ml-2">{line.gainLossType}</Badge></td>
                                </tr>)}</tbody>
                            </table></div>}
                        <div className="flex justify-between"><Button variant="outline" onClick={() => setStep(1)}>Back</Button><Button onClick={handlePost} disabled={busy || preview.lines.length === 0 || !configurationReady}>{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}Post revaluation journal</Button></div>
                    </CardContent>
                </Card>
            </div> : null}

            {step === 3 && postedJournal ? <Card className="mx-auto max-w-xl text-center"><CardContent className="space-y-4 py-10">
                <CheckCircle className="mx-auto h-16 w-16 text-green-500" /><h2 className="text-2xl font-bold">Revaluation posted</h2>
                <p className="text-muted-foreground">The live posting engine created journal <strong>{postedJournal.journalEntryNumber}</strong>.</p>
                <div className="grid grid-cols-2 gap-3 rounded-md border p-4 text-left"><div><div className="text-xs text-muted-foreground">Posting status</div><div className="font-semibold">{postedJournal.postingStatus}</div></div><div><div className="text-xs text-muted-foreground">Journal total</div><div className="font-semibold">{formatMoney(postedJournal.totalDebitAmount)}</div></div></div>
                <div className="flex justify-center gap-3 pt-2"><Button variant="outline" onClick={reset}><RotateCcw className="mr-2 h-4 w-4" />Start another</Button><Button asChild><a href={`/finance/journal-entries/${postedJournal.id}`}>View journal entry</a></Button></div>
            </CardContent></Card> : null}
        </div>
    );
}
