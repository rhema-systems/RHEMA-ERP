'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { Save, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';

export default function EditUnitTypePage() {
    const id = useParams<{ id: string }>().id;
    const router = useRouter();
    const [originalActive, setOriginalActive] = useState(true);
    const [form, setForm] = useState({ code: '', name: '', description: '', decimalPlaces: 2, isActive: true });
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;
        void unitAccountsDataService.getUnitTypeById(id).then((unitType) => {
            if (cancelled) return;
            setForm({ code: unitType.code, name: unitType.name, description: unitType.description ?? '', decimalPlaces: unitType.decimalPlaces, isActive: unitType.isActive });
            setOriginalActive(unitType.isActive);
        }).catch((reason: unknown) => {
            if (!cancelled) setError(reason instanceof Error ? reason.message : 'Unable to load unit type.');
        }).finally(() => { if (!cancelled) setLoading(false); });
        return () => { cancelled = true; };
    }, [id]);

    const save = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!form.name.trim()) return setError('Name is required.');
        setBusy(true); setError(null);
        try {
            await unitAccountsDataService.updateUnitType(id, { name: form.name.trim(), description: form.description.trim(), decimalPlaces: form.decimalPlaces });
            if (form.isActive !== originalActive) await unitAccountsDataService.setUnitTypeActive(id, form.isActive);
            router.push('/finance/unit-types');
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : 'Unable to save unit type.');
        } finally { setBusy(false); }
    };

    const remove = async () => {
        if (!window.confirm('Delete this unit type? The server will block deletion when accounts depend on it.')) return;
        setBusy(true);
        try { await unitAccountsDataService.deleteUnitType(id); router.push('/finance/unit-types'); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to delete unit type.'); setBusy(false); }
    };

    if (loading) return <p className="p-6 text-muted-foreground">Loading unit type…</p>;

    return <div className="space-y-6">
        <div className="flex items-center justify-between"><div><h1 className="text-3xl font-bold">Edit Unit Type</h1><p className="text-muted-foreground">{form.code}</p></div><Button variant="destructive" disabled={busy} onClick={() => void remove()}><Trash2 className="mr-2 h-4 w-4" />Delete</Button></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        <form onSubmit={save}><Card><CardHeader><CardTitle>Unit type details</CardTitle></CardHeader><CardContent className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2"><Label>Code</Label><Input value={form.code} disabled /></div>
                <div className="space-y-2"><Label>Name</Label><Input required maxLength={100} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></div>
                <div className="space-y-2"><Label>Decimal places</Label><Input type="number" min={0} max={6} value={form.decimalPlaces} onChange={(event) => setForm({ ...form, decimalPlaces: Number(event.target.value) })} /></div>
                <div className="flex items-center gap-3 pt-7"><Switch checked={form.isActive} onCheckedChange={(isActive) => setForm({ ...form, isActive })} /><Label>{form.isActive ? 'Active' : 'Inactive'}</Label></div>
            </div>
            <div className="space-y-2"><Label>Description</Label><Textarea maxLength={500} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></div>
            <div className="flex justify-end gap-2"><Button asChild type="button" variant="outline"><Link href="/finance/unit-types">Cancel</Link></Button><Button disabled={busy} type="submit"><Save className="mr-2 h-4 w-4" />Save</Button></div>
        </CardContent></Card></form>
    </div>;
}
