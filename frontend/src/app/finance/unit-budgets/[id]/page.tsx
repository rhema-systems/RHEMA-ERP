'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { Save, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { BudgetVariance, UnitAccountBudget } from '@/types/unit-accounts';

export default function UnitBudgetDetailPage() {
    const id = useParams<{ id: string }>().id;
    const router = useRouter();
    const [budget, setBudget] = useState<UnitAccountBudget | null>(null);
    const [variance, setVariance] = useState<BudgetVariance | null>(null);
    const [form, setForm] = useState({ budgetQuantity: '', notes: '', isActive: true });
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;
        void unitAccountsDataService.getUnitAccountBudgetById(id).then(async (loaded) => {
            if (cancelled) return;
            setBudget(loaded);
            setForm({ budgetQuantity: String(loaded.budgetQuantity), notes: loaded.notes ?? '', isActive: loaded.isActive });
            const rows = await unitAccountsDataService.getBudgetVariances(loaded.fiscalPeriodId);
            if (!cancelled) setVariance(rows.find((row) => row.unitAccountId === loaded.unitAccountId) ?? null);
        }).catch((reason: unknown) => { if (!cancelled) setError(reason instanceof Error ? reason.message : 'Unable to load unit budget.'); });
        return () => { cancelled = true; };
    }, [id]);

    const save = async (event: React.FormEvent) => {
        event.preventDefault(); setBusy(true); setError(null);
        try { await unitAccountsDataService.updateBudget(id, { budgetQuantity: Number(form.budgetQuantity), notes: form.notes.trim() || undefined, isActive: form.isActive }); router.push('/finance/unit-budgets'); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to update unit budget.'); }
        finally { setBusy(false); }
    };
    const remove = async () => {
        if (!window.confirm('Delete this unit budget?')) return;
        setBusy(true);
        try { await unitAccountsDataService.deleteBudget(id); router.push('/finance/unit-budgets'); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to delete unit budget.'); setBusy(false); }
    };

    if (!budget && !error) return <p className="p-6 text-muted-foreground">Loading unit budget…</p>;
    return <div className="space-y-6">
        <div className="flex items-center justify-between"><div><h1 className="text-3xl font-bold">{budget?.accountNumber ?? 'Unit Budget'}</h1><p className="text-muted-foreground">{budget?.accountName} · {budget?.periodName} · {budget?.budgetVersion}</p></div><Button variant="destructive" disabled={busy} onClick={() => void remove()}><Trash2 className="mr-2 h-4 w-4" />Delete</Button></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        {variance && <Card><CardHeader><CardTitle>Period variance</CardTitle></CardHeader><CardContent className="grid gap-4 md:grid-cols-4"><div><p className="text-sm text-muted-foreground">Budget</p><p className="text-xl font-semibold">{variance.budgetQuantity.toLocaleString()}</p></div><div><p className="text-sm text-muted-foreground">Activity</p><p className="text-xl font-semibold">{variance.actualQuantity.toLocaleString()}</p></div><div><p className="text-sm text-muted-foreground">Variance</p><p className="text-xl font-semibold">{variance.variance.toLocaleString()}</p></div><div><Badge variant={variance.isFavorable ? 'default' : 'destructive'}>{variance.isFavorable ? 'Favorable' : 'Unfavorable'}</Badge></div></CardContent></Card>}
        <form onSubmit={save}><Card><CardHeader><CardTitle>Budget settings</CardTitle></CardHeader><CardContent className="space-y-5"><div className="grid gap-4 md:grid-cols-2"><div className="space-y-2"><Label>Budget quantity ({budget?.unitTypeCode})</Label><Input required type="number" step="any" value={form.budgetQuantity} onChange={(event) => setForm({ ...form, budgetQuantity: event.target.value })} /></div><div className="flex items-center gap-3 pt-7"><Switch checked={form.isActive} onCheckedChange={(isActive) => setForm({ ...form, isActive })} /><Label>{form.isActive ? 'Active' : 'Inactive'}</Label></div></div><div className="space-y-2"><Label>Notes</Label><Textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></div><div className="flex justify-end gap-2"><Button asChild type="button" variant="outline"><Link href="/finance/unit-budgets">Cancel</Link></Button><Button type="submit" disabled={busy}><Save className="mr-2 h-4 w-4" />Save</Button></div></CardContent></Card></form>
    </div>;
}
