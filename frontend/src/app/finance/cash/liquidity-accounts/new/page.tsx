'use client';

import { FormEvent, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { BankAccount, LiquidityAccountType } from '@/types/cash-management';
import type { Account } from '@/types/finance';

const types: LiquidityAccountType[] = [
    'UndepositedCash',
    'ChequesAwaitingDeposit',
    'MobileMoneyClearing',
    'CardSettlementClearing',
    'CashTill',
    'OtherSettlementClearing',
    'Bank',
];

export default function NewLiquidityAccountPage() {
    const router = useRouter();
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [banks, setBanks] = useState<BankAccount[]>([]);
    const [saving, setSaving] = useState(false);
    const [form, setForm] = useState({
        code: '',
        name: '',
        accountType: 'CashTill' as LiquidityAccountType,
        currency: 'GHS',
        glAccountId: '',
        bankAccountId: '',
        providerName: '',
        providerAccountReference: '',
        notes: '',
    });

    useEffect(() => {
        void Promise.all([
            financeDataService.getAccounts({ status: 'Active' }),
            cashManagementDataService.getActiveBankAccounts(),
        ]).then(([chart, bankAccounts]) => {
            setAccounts(chart.filter(account => account.allowDirectPosting || account.isPostingAllowed === true));
            setBanks(bankAccounts);
        }).catch(error => toast.error(error instanceof Error ? error.message : 'Could not load setup data.'));
    }, []);

    const submit = async (event: FormEvent) => {
        event.preventDefault();
        setSaving(true);
        try {
            await cashManagementDataService.createLiquidityAccount({
                code: form.code,
                name: form.name,
                accountType: form.accountType,
                currency: form.currency.toUpperCase(),
                glAccountId: form.glAccountId,
                bankAccountId: form.accountType === 'Bank' ? form.bankAccountId : undefined,
                providerName: form.providerName || undefined,
                providerAccountReference: form.providerAccountReference || undefined,
                allowsNegativeBalance: false,
                allowsManualAllocations: form.accountType !== 'Bank',
                notes: form.notes || undefined,
            });
            toast.success('Liquidity account created.');
            router.push('/finance/cash/liquidity-accounts');
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not create liquidity account.');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="mx-auto max-w-3xl space-y-6 p-6">
            <div className="flex items-start gap-3">
                <Button variant="ghost" size="icon" asChild><Link href="/finance/cash/liquidity-accounts"><ArrowLeft className="h-4 w-4" /></Link></Button>
                <div><h1 className="text-3xl font-bold">New Liquidity Account</h1><p className="text-muted-foreground">Create a till, clearing location, or bank subtype.</p></div>
            </div>
            <Card>
                <CardHeader><CardTitle>Account mapping</CardTitle><CardDescription>Every location has a dedicated GL control account so unresolved settlements remain visible.</CardDescription></CardHeader>
                <CardContent>
                    <form onSubmit={submit} className="space-y-5">
                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2"><Label>Code</Label><Input required maxLength={30} value={form.code} onChange={e => setForm({ ...form, code: e.target.value.toUpperCase() })} /></div>
                            <div className="space-y-2"><Label>Name</Label><Input required value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></div>
                            <div className="space-y-2">
                                <Label>Type</Label>
                                <Select value={form.accountType} onValueChange={value => setForm({ ...form, accountType: value as LiquidityAccountType })}>
                                    <SelectTrigger><SelectValue /></SelectTrigger>
                                    <SelectContent>{types.map(type => <SelectItem key={type} value={type}>{type.replace(/([A-Z])/g, ' $1').trim()}</SelectItem>)}</SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2"><Label>Currency</Label><Input required maxLength={3} value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></div>
                        </div>
                        <div className="space-y-2">
                            <Label>GL control account</Label>
                            <Select required value={form.glAccountId} onValueChange={glAccountId => setForm({ ...form, glAccountId })}>
                                <SelectTrigger><SelectValue placeholder="Select GL account" /></SelectTrigger>
                                <SelectContent>{accounts.map(account => <SelectItem key={account.id} value={account.id}>{account.accountNumber} — {account.accountName}</SelectItem>)}</SelectContent>
                            </Select>
                        </div>
                        {form.accountType === 'Bank' && (
                            <div className="space-y-2">
                                <Label>Bank account master</Label>
                                <Select required value={form.bankAccountId} onValueChange={bankAccountId => setForm({ ...form, bankAccountId })}>
                                    <SelectTrigger><SelectValue placeholder="Select bank account" /></SelectTrigger>
                                    <SelectContent>{banks.map(bank => <SelectItem key={bank.id} value={bank.id}>{bank.bankName} — {bank.accountName}</SelectItem>)}</SelectContent>
                                </Select>
                            </div>
                        )}
                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2"><Label>Provider (optional)</Label><Input value={form.providerName} onChange={e => setForm({ ...form, providerName: e.target.value })} /></div>
                            <div className="space-y-2"><Label>Provider reference</Label><Input value={form.providerAccountReference} onChange={e => setForm({ ...form, providerAccountReference: e.target.value })} /></div>
                        </div>
                        <div className="space-y-2"><Label>Notes</Label><Textarea value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} /></div>
                        <div className="flex justify-end gap-2"><Button type="button" variant="outline" asChild><Link href="/finance/cash/liquidity-accounts">Cancel</Link></Button><Button disabled={saving || !form.glAccountId}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Create</Button></div>
                    </form>
                </CardContent>
            </Card>
        </div>
    );
}
