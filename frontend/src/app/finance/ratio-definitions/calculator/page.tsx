'use client';

import { useEffect, useState } from 'react';
import { Calculator } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { financeDataService } from '@/services/finance/finance-data.service';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { FiscalPeriod } from '@/types/finance';
import type { RatioCalculationResult, RatioDefinition } from '@/types/unit-accounts';

export default function RatioCalculatorPage() {
    const [ratios, setRatios] = useState<RatioDefinition[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [ratioId, setRatioId] = useState('');
    const [periodId, setPeriodId] = useState('');
    const [result, setResult] = useState<RatioCalculationResult | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        void Promise.all([unitAccountsDataService.getRatioDefinitions({ isActive: true }), financeDataService.getFiscalPeriods()])
            .then(([loadedRatios, loadedPeriods]) => { setRatios(loadedRatios); setPeriods(loadedPeriods); })
            .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Unable to load calculator options.'));
    }, []);

    const calculate = async () => {
        if (!ratioId || !periodId) return setError('Select both a ratio and a fiscal period.');
        setBusy(true); setError(null);
        try { setResult(await unitAccountsDataService.calculateRatio(ratioId, periodId)); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to calculate ratio.'); }
        finally { setBusy(false); }
    };

    return <div className="space-y-6"><div><h1 className="flex items-center gap-2 text-3xl font-bold"><Calculator className="h-7 w-7" />Ratio Calculator</h1><p className="text-muted-foreground">Calculate an active ratio from posted, book-scoped balances.</p></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        <Card><CardHeader><CardTitle>Calculation inputs</CardTitle></CardHeader><CardContent className="grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end">
            <div className="space-y-2"><Label>Ratio</Label><Select value={ratioId} onValueChange={setRatioId}><SelectTrigger><SelectValue placeholder="Select ratio" /></SelectTrigger><SelectContent>{ratios.map((ratio) => <SelectItem key={ratio.id} value={ratio.id}>{ratio.code} — {ratio.name}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Fiscal period</Label><Select value={periodId} onValueChange={setPeriodId}><SelectTrigger><SelectValue placeholder="Select period" /></SelectTrigger><SelectContent>{periods.map((period) => <SelectItem key={period.id} value={period.id}>{period.periodName}</SelectItem>)}</SelectContent></Select></div>
            <Button disabled={busy} onClick={() => void calculate()}>{busy ? 'Calculating…' : 'Calculate'}</Button>
        </CardContent></Card>
        {result && <Card><CardHeader><CardTitle>{result.ratioCode} — {result.ratioName}</CardTitle></CardHeader><CardContent className="grid gap-4 md:grid-cols-3"><div><p className="text-sm text-muted-foreground">Numerator</p><p className="text-2xl font-semibold">{result.numerator?.toLocaleString()}</p></div><div><p className="text-sm text-muted-foreground">Denominator</p><p className="text-2xl font-semibold">{result.denominator?.toLocaleString()}</p></div><div><p className="text-sm text-muted-foreground">Result</p><p className="text-2xl font-semibold">{result.formattedResult ?? result.result.toLocaleString()}</p></div></CardContent></Card>}
    </div>;
}
