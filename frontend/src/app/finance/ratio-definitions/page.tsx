'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Calculator, Plus, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { RatioDefinition } from '@/types/unit-accounts';

export default function RatioDefinitionsPage() {
    const [ratios, setRatios] = useState<RatioDefinition[]>([]);
    const [search, setSearch] = useState('');
    const [error, setError] = useState<string | null>(null);

    const load = async () => {
        try { setRatios(await unitAccountsDataService.getRatioDefinitions()); setError(null); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load ratio definitions.'); }
    };
    useEffect(() => { void load(); }, []);

    const visible = useMemo(() => ratios.filter((ratio) => `${ratio.code} ${ratio.name}`.toLowerCase().includes(search.toLowerCase())), [ratios, search]);
    const toggle = async (ratio: RatioDefinition) => {
        try { await unitAccountsDataService.setRatioDefinitionActive(ratio.id, !ratio.isActive); await load(); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to update ratio status.'); }
    };
    const remove = async (ratio: RatioDefinition) => {
        if (!window.confirm(`Delete ratio ${ratio.code}?`)) return;
        try { await unitAccountsDataService.deleteRatioDefinition(ratio.id); await load(); }
        catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to delete ratio.'); }
    };

    return <div className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-3xl font-bold">Ratio Definitions</h1><p className="text-muted-foreground">Financial and unit-account KPIs</p></div><div className="flex gap-2"><Button asChild variant="outline"><Link href="/finance/ratio-definitions/calculator"><Calculator className="mr-2 h-4 w-4" />Calculator</Link></Button><Button asChild><Link href="/finance/ratio-definitions/new"><Plus className="mr-2 h-4 w-4" />New ratio</Link></Button></div></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        <Card><CardHeader><CardTitle>Definitions</CardTitle></CardHeader><CardContent className="space-y-4">
            <div className="relative max-w-md"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search code or name" /></div>
            <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm"><thead><tr className="border-b bg-muted/50"><th className="p-3 text-left">Code</th><th className="p-3 text-left">Name</th><th className="p-3 text-left">Formula</th><th className="p-3 text-left">Status</th><th className="p-3 text-right">Actions</th></tr></thead><tbody>
                {visible.map((ratio) => <tr key={ratio.id} className="border-b"><td className="p-3 font-mono"><Link className="hover:underline" href={`/finance/ratio-definitions/${ratio.id}`}>{ratio.code}</Link></td><td className="p-3">{ratio.name}</td><td className="p-3 text-muted-foreground">{ratio.numeratorType} ÷ {ratio.denominatorType}</td><td className="p-3"><Badge variant={ratio.isActive ? 'default' : 'secondary'}>{ratio.isActive ? 'Active' : 'Inactive'}</Badge></td><td className="p-3 text-right"><Button size="sm" variant="ghost" onClick={() => void toggle(ratio)}>{ratio.isActive ? 'Deactivate' : 'Activate'}</Button><Button size="sm" variant="ghost" className="text-destructive" onClick={() => void remove(ratio)}>Delete</Button></td></tr>)}
                {visible.length === 0 && <tr><td colSpan={5} className="p-8 text-center text-muted-foreground">No ratio definitions found.</td></tr>}
            </tbody></table></div>
        </CardContent></Card>
    </div>;
}
