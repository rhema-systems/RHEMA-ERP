'use client';

import { Fragment, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ChevronDown, ChevronRight, Plus, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { UnitAccount } from '@/types/unit-accounts';

function AccountRows({ accounts, level, expanded, toggle }: { accounts: UnitAccount[]; level: number; expanded: Set<string>; toggle: (id: string) => void }) {
    return <>{accounts.map((account) => {
        const hasChildren = Boolean(account.children?.length);
        const open = expanded.has(account.id);
        return <Fragment key={account.id}><tr className="border-b"><td className="p-3"><div className="flex items-center" style={{ paddingLeft: `${level * 20}px` }}>{hasChildren ? <button type="button" className="mr-1" onClick={() => toggle(account.id)}>{open ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}</button> : <span className="mr-1 w-4" />}<Link className="font-mono hover:underline" href={`/finance/unit-accounts/${account.id}`}>{account.accountNumber}</Link></div></td><td className="p-3">{account.name}</td><td className="p-3">{account.unitTypeName ?? account.unitType?.name}</td><td className="p-3 text-right tabular-nums">{account.currentBalance?.toLocaleString() ?? '0'}</td><td className="p-3"><Badge variant={account.isActive ? 'default' : 'secondary'}>{account.isPostingAccount ? 'Posting' : 'Summary'} · {account.isActive ? 'Active' : 'Inactive'}</Badge></td></tr>{hasChildren && open && <AccountRows accounts={account.children ?? []} level={level + 1} expanded={expanded} toggle={toggle} />}</Fragment>;
    })}</>;
}

export default function UnitAccountsPage() {
    const [roots, setRoots] = useState<UnitAccount[]>([]);
    const [expanded, setExpanded] = useState<Set<string>>(new Set());
    const [search, setSearch] = useState('');
    const [error, setError] = useState<string | null>(null);
    useEffect(() => {
        void unitAccountsDataService.getUnitAccounts().then((loaded) => {
            const hydrated = loaded.map((account) => ({ ...account, children: [] as UnitAccount[] }));
            const byId = new Map(hydrated.map((account) => [account.id, account]));
            const nextRoots: UnitAccount[] = [];
            hydrated.forEach((account) => {
                const parent = account.parentAccountId ? byId.get(account.parentAccountId) : undefined;
                if (parent) parent.children = [...(parent.children ?? []), account]; else nextRoots.push(account);
            });
            setRoots(nextRoots); setError(null);
        }).catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Unable to load unit accounts.'));
    }, []);
    const visible = useMemo(() => {
        if (!search.trim()) return roots;
        const term = search.toLowerCase();
        const matchTree = (account: UnitAccount): UnitAccount | null => {
            const children = (account.children ?? []).map(matchTree).filter((child): child is UnitAccount => child !== null);
            return `${account.accountNumber} ${account.name}`.toLowerCase().includes(term) || children.length ? { ...account, children } : null;
        };
        return roots.map(matchTree).filter((account): account is UnitAccount => account !== null);
    }, [roots, search]);
    const toggle = (id: string) => setExpanded((current) => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; });

    return <div className="space-y-6"><div className="flex items-center justify-between"><div><h1 className="text-3xl font-bold">Unit Accounts</h1><p className="text-muted-foreground">Hierarchical non-financial account balances</p></div><Button asChild><Link href="/finance/unit-accounts/new"><Plus className="mr-2 h-4 w-4" />New account</Link></Button></div>
        {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>}
        <Card><CardHeader><CardTitle>Account hierarchy</CardTitle></CardHeader><CardContent className="space-y-4"><div className="relative max-w-md"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search number or name" /></div><div className="overflow-x-auto rounded-md border"><table className="w-full text-sm"><thead><tr className="border-b bg-muted/50"><th className="p-3 text-left">Account</th><th className="p-3 text-left">Name</th><th className="p-3 text-left">Unit</th><th className="p-3 text-right">Balance</th><th className="p-3 text-left">Status</th></tr></thead><tbody><AccountRows accounts={visible} level={0} expanded={expanded} toggle={toggle} />{visible.length === 0 && <tr><td colSpan={5} className="p-8 text-center text-muted-foreground">No unit accounts found.</td></tr>}</tbody></table></div></CardContent></Card>
    </div>;
}
