'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { financeDataService } from '@/services/finance/finance-data.service';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { FiscalPeriod, FiscalYear } from '@/types/finance';
import type { UnitAccount } from '@/types/unit-accounts';

export default function NewUnitBudgetPage() {
    const router = useRouter();
    const [accounts, setAccounts] = useState<UnitAccount[]>([]);
    const [years, setYears] = useState<FiscalYear[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [form, setForm] = useState({ unitAccountId: '', fiscalYearId: '', fiscalPeriodId: '', budgetQuantity: '', notes: '', budgetVersion: 'Original' });
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        void Promise.all([unitAccountsDataService.getUnitAccounts({ isActive: true, isPostingAccount: true }), financeDataService.getFiscalYears(), financeDataService.getFiscalPeriods()])
            .then(([loadedAccounts, loadedYears, loadedPeriods]) => { setAccounts(loadedAccounts); setYears(loadedYears); setPeriods(loadedPeriods); })
            .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Unable to load budget options.'));
    }, []);
    const availablePeriods = useMemo(() => periods.filter((period) => period.fiscalYearId === form.fiscalYearId), [periods, form.fiscalYearId]);

    const submit = async (event: React.FormEvent) => {
        event.preventDefault(); setBusy(true); setError(null);
        try {
            const created = await unitAccountsDataService.createBudget({ ...form, budgetQuantity: Number(form.budgetQuantity), notes: form.notes.trim() || undefined, budgetVersion: form.budgetVersion.trim() || 'Original' });
            router.push(`/finance/unit-budgets/${created.id}`);
        } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create unit budget.'); }
        finally { setBusy(false); }
    };

    return <div className="space-y-6"><div><h1 className="text-3xl font-bold">New Unit Budget</h1><p className="text-muted-foreground">Set a quantity target for one fiscal period.</p></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        <form onSubmit={submit}><Card><CardHeader><CardTitle>Budget details</CardTitle></CardHeader><CardContent className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2"><Label>Posting unit account</Label><Select required value={form.unitAccountId} onValueChange={(unitAccountId) => setForm({ ...form, unitAccountId })}><SelectTrigger><SelectValue placeholder="Select account" /></SelectTrigger><SelectContent>{accounts.map((account) => <SelectItem key={account.id} value={account.id}>{account.accountNumber} — {account.name}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-2"><Label>Fiscal year</Label><Select required value={form.fiscalYearId} onValueChange={(fiscalYearId) => setForm({ ...form, fiscalYearId, fiscalPeriodId: '' })}><SelectTrigger><SelectValue placeholder="Select year" /></SelectTrigger><SelectContent>{years.map((year) => <SelectItem key={year.id} value={year.id}>{year.fiscalYearName}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-2"><Label>Fiscal period</Label><Select required value={form.fiscalPeriodId} onValueChange={(fiscalPeriodId) => setForm({ ...form, fiscalPeriodId })}><SelectTrigger><SelectValue placeholder="Select period" /></SelectTrigger><SelectContent>{availablePeriods.map((period) => <SelectItem key={period.id} value={period.id}>{period.periodName}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-2"><Label>Budget quantity</Label><Input required type="number" step="any" value={form.budgetQuantity} onChange={(event) => setForm({ ...form, budgetQuantity: event.target.value })} /></div>
                <div className="space-y-2"><Label>Version</Label><Input required maxLength={50} value={form.budgetVersion} onChange={(event) => setForm({ ...form, budgetVersion: event.target.value })} /></div>
            </div>
            <div className="space-y-2"><Label>Notes</Label><Textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></div>
            <div className="flex justify-end gap-2"><Button asChild type="button" variant="outline"><Link href="/finance/unit-budgets">Cancel</Link></Button><Button type="submit" disabled={busy}><Save className="mr-2 h-4 w-4" />Create</Button></div>
        </CardContent></Card></form>
    </div>;
}
