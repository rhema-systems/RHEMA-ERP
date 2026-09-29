'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ArrowLeft, Loader2, Plus, ReceiptText } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { BankDeposit } from '@/types/cash-management';
import { useAuth } from '@/hooks/use-auth';

export default function BankDepositsPage() {
    const { hasPermission } = useAuth();
    const [items, setItems] = useState<BankDeposit[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');

    useEffect(() => {
        void cashManagementDataService.getBankDeposits()
            .then(setItems)
            .catch(error => toast.error(error instanceof Error ? error.message : 'Could not load deposits.'))
            .finally(() => setLoading(false));
    }, []);

    const filtered = useMemo(() => {
        const value = search.trim().toLowerCase();
        return value
            ? items.filter(item => [item.depositNumber, item.depositReference, item.bankAccountName, item.bankConfirmationReference].some(text => text?.toLowerCase().includes(value)))
            : items;
    }, [items, search]);

    const openTotal = items.filter(item => !['Posted', 'Cancelled', 'Rejected', 'Reversed'].includes(item.status))
        .reduce((sum, item) => sum + item.netAmount, 0);
    const awaiting = items.filter(item => item.status === 'Submitted').length;
    const awaitingConfirmation = items.filter(item => item.status === 'Posted' && item.confirmationStatus === 'Pending').length;
    const unreconciled = items.filter(item => item.status === 'Posted' && !item.isReconciled).length;

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                    <Button variant="ghost" size="icon" asChild><Link href="/finance/cash"><ArrowLeft className="h-4 w-4" /></Link></Button>
                    <div><h1 className="text-3xl font-bold">Banking Deposits</h1><p className="text-muted-foreground">Settle collected receipts and eligible payments into bank accounts.</p></div>
                </div>
                {hasPermission('Finance.Banking.Deposits.Create') && (
                    <Button asChild><Link href="/finance/cash/deposits/new"><Plus className="mr-2 h-4 w-4" />New deposit</Link></Button>
                )}
            </div>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Open deposits</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">GHS {openTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Awaiting approval</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{awaiting}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Awaiting bank confirmation</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{awaitingConfirmation}</CardContent></Card>
                <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Posted & unreconciled</CardTitle></CardHeader><CardContent className="text-2xl font-semibold">{unreconciled}</CardContent></Card>
            </div>
            <Card>
                <CardHeader className="flex-row items-center justify-between gap-3"><CardTitle>Deposit register</CardTitle><Input className="max-w-sm" placeholder="Search number, reference, bank…" value={search} onChange={event => setSearch(event.target.value)} /></CardHeader>
                <CardContent>
                    {loading ? <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin" /></div> : filtered.length === 0 ? (
                        <div className="py-12 text-center text-muted-foreground"><ReceiptText className="mx-auto mb-3 h-9 w-9" />No deposits found.</div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full text-sm">
                                <thead className="border-b text-left text-muted-foreground"><tr><th className="p-3">Deposit</th><th className="p-3">Bank / reference</th><th className="p-3">Date</th><th className="p-3 text-right">Receipts</th><th className="p-3 text-right">Deductions</th><th className="p-3 text-right">Net banked</th><th className="p-3">Lifecycle</th><th className="p-3">Bank confirmation</th><th className="p-3">Reconciliation</th></tr></thead>
                                <tbody>{filtered.map(item => (
                                    <tr key={item.id} className="border-b hover:bg-muted/40">
                                        <td className="p-3 font-medium"><Link className="text-primary hover:underline" href={`/finance/cash/deposits/${item.id}`}>{item.depositNumber}</Link></td>
                                        <td className="p-3"><div>{item.bankAccountName}</div><div className="text-xs text-muted-foreground">{item.depositReference}</div></td>
                                        <td className="p-3">{new Date(item.depositDate).toLocaleDateString()}</td>
                                        <td className="p-3 text-right">{item.currency} {item.totalReceipts.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                                        <td className="p-3 text-right text-red-600">{item.totalDeductions.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                                        <td className="p-3 text-right font-semibold">{item.currency} {item.netAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                                        <td className="p-3"><Badge variant={item.status === 'Posted' ? 'default' : item.status === 'Rejected' ? 'destructive' : 'secondary'}>{item.status}</Badge></td>
                                        <td className="p-3"><div><Badge variant={item.confirmationStatus === 'Confirmed' ? 'default' : 'secondary'}>{item.confirmationStatus}</Badge></div><div className="mt-1 text-xs text-muted-foreground">{item.bankConfirmationReference ?? 'No bank reference'}</div></td>
                                        <td className="p-3"><Badge variant={item.reconciliationStatus === 'Approved' ? 'default' : 'secondary'}>{item.reconciliationStatus ?? (item.status === 'Posted' ? 'Unreconciled' : 'Not eligible')}</Badge></td>
                                    </tr>
                                ))}</tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
