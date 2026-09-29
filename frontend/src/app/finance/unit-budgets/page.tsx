'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Plus, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { financeDataService } from '@/services/finance/finance-data.service';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { FiscalPeriod } from '@/types/finance';
import type { BudgetVariance } from '@/types/unit-accounts';

export default function UnitBudgetsPage() {
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [periodId, setPeriodId] = useState('all');
    const [variances, setVariances] = useState<BudgetVariance[]>([]);
    const [search, setSearch] = useState('');
    const [error, setError] = useState<string | null>(null);

    useEffect(() => { void financeDataService.getFiscalPeriods().then(setPeriods).catch(() => setError('Unable to load fiscal periods.')); }, []);
    useEffect(() => {
        void unitAccountsDataService.getBudgetVariances(periodId === 'all' ? undefined : periodId)
            .then((rows) => { setVariances(rows); setError(null); })
            .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Unable to load budget variances.'));
    }, [periodId]);

    const visible = useMemo(() => variances.filter((row) => `${row.accountNumber} ${row.accountName}`.toLowerCase().includes(search.toLowerCase())), [variances, search]);
    return <div className="space-y-6">
        <div className="flex items-center justify-between"><div><h1 className="text-3xl font-bold">Unit Budgets</h1><p className="text-muted-foreground">Period activity compared with unit targets</p></div><Button asChild><Link href="/finance/unit-budgets/new"><Plus className="mr-2 h-4 w-4" />New budget</Link></Button></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        <Card><CardHeader><CardTitle>Variance analysis</CardTitle></CardHeader><CardContent className="space-y-4">
            <div className="flex flex-wrap gap-3"><div className="relative min-w-64 flex-1"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search accounts" /></div><Select value={periodId} onValueChange={setPeriodId}><SelectTrigger className="w-64"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All periods</SelectItem>{periods.map((period) => <SelectItem key={period.id} value={period.id}>{period.periodName}</SelectItem>)}</SelectContent></Select></div>
            <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm"><thead><tr className="border-b bg-muted/50"><th className="p-3 text-left">Account</th><th className="p-3 text-left">Period</th><th className="p-3 text-right">Budget</th><th className="p-3 text-right">Activity</th><th className="p-3 text-right">Variance</th><th className="p-3 text-left">Assessment</th></tr></thead><tbody>
                {visible.map((row) => <tr key={`${row.unitAccountId}-${row.periodName}`} className="border-b"><td className="p-3"><Link className="font-medium hover:underline" href={`/finance/unit-accounts/${row.unitAccountId}`}>{row.accountNumber} — {row.accountName}</Link><div className="text-xs text-muted-foreground">{row.unitTypeCode}</div></td><td className="p-3">{row.periodName}</td><td className="p-3 text-right tabular-nums">{row.budgetQuantity.toLocaleString()}</td><td className="p-3 text-right tabular-nums">{row.actualQuantity.toLocaleString()}</td><td className="p-3 text-right tabular-nums">{row.variance.toLocaleString()} ({row.variancePercent.toFixed(2)}%)</td><td className="p-3"><Badge variant={row.isFavorable ? 'default' : 'destructive'}>{row.isFavorable ? 'Favorable' : 'Unfavorable'}</Badge></td></tr>)}
                {visible.length === 0 && <tr><td colSpan={6} className="p-8 text-center text-muted-foreground">No unit budgets found.</td></tr>}
            </tbody></table></div>
        </CardContent></Card>
    </div>;
}
