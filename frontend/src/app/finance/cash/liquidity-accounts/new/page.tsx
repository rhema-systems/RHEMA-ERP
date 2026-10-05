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
import type { Account, Currency } from '@/types/finance';

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
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [supportedCurrencyCodes, setSupportedCurrencyCodes] = useState<string[]>([]);
    const [loadingCurrencyLinks, setLoadingCurrencyLinks] = useState(false);
    const [saving, setSaving] = useState(false);
    const [form, setForm] = useState({
        code: '',
        name: '',
        accountType: 'CashTill' as LiquidityAccountType,
        currency: '',
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
            financeDataService.getCurrencies({ isActive: true }),
        ]).then(([chart, bankAccounts, activeCurrencies]) => {
            setAccounts(chart.filter(account => account.allowDirectPosting || account.isPostingAllowed === true));
            setBanks(bankAccounts);
            setCurrencies(activeCurrencies);
        }).catch(error => toast.error(error instanceof Error ? error.message : 'Could not load setup data.'));
    }, []);

    useEffect(() => {
        let cancelled = false;
        setLoadingCurrencyLinks(false);

        if (form.accountType === 'Bank') {
            const bank = banks.find(item => item.id === form.bankAccountId);
            const bankCurrency = bank?.currency.trim().toUpperCase() ?? '';
            setSupportedCurrencyCodes(bankCurrency ? [bankCurrency] : []);
            setForm(current => current.currency === bankCurrency ? current : { ...current, currency: bankCurrency });
            return () => { cancelled = true; };
        }

        const account = accounts.find(item => item.id === form.glAccountId);
        if (!account) {
            setSupportedCurrencyCodes([]);
            setForm(current => current.currency ? { ...current, currency: '' } : current);
            return () => { cancelled = true; };
        }

        const primaryCurrency = account.currencyCode.trim().toUpperCase();
        if (!account.isMultiCurrency) {
            setSupportedCurrencyCodes([primaryCurrency]);
            setForm(current => current.currency === primaryCurrency ? current : { ...current, currency: primaryCurrency });
            return () => { cancelled = true; };
        }

        setLoadingCurrencyLinks(true);
        void financeDataService.getAccountCurrencyLinks(account.id).then(links => {
            if (cancelled) return;
            const now = Date.now();
            const codes = [
                primaryCurrency,
                ...links.filter(link => link.isActive
                    && (!link.effectiveDate || new Date(link.effectiveDate).getTime() <= now)
                    && (!link.effectiveEndDate || new Date(link.effectiveEndDate).getTime() > now))
                    .map(link => link.linkedCurrencyCode.trim().toUpperCase()),
            ].filter((code, index, values) => code && values.indexOf(code) === index);
            setSupportedCurrencyCodes(codes);
            setForm(current => codes.includes(current.currency)
                ? current
                : { ...current, currency: primaryCurrency });
        }).catch(error => {
            if (cancelled) return;
            setSupportedCurrencyCodes([primaryCurrency]);
            setForm(current => ({ ...current, currency: primaryCurrency }));
            toast.error(error instanceof Error ? error.message : 'Could not load GL account currencies.');
        }).finally(() => {
            if (!cancelled) setLoadingCurrencyLinks(false);
        });

        return () => { cancelled = true; };
    }, [accounts, banks, form.accountType, form.bankAccountId, form.glAccountId]);

    const eligibleCurrencies = currencies.filter(currency =>
        supportedCurrencyCodes.includes(currency.currencyCode.trim().toUpperCase()));

    const changeAccountType = (accountType: LiquidityAccountType) => {
        setForm(current => ({ ...current, accountType, bankAccountId: '', glAccountId: '', currency: '' }));
    };

    const changeBankAccount = (bankAccountId: string) => {
        const bank = banks.find(item => item.id === bankAccountId);
        setForm(current => ({
            ...current,
            bankAccountId,
            glAccountId: bank?.glAccountId ?? '',
            currency: bank?.currency.trim().toUpperCase() ?? '',
        }));
    };

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
                                <Select value={form.accountType} onValueChange={value => changeAccountType(value as LiquidityAccountType)}>
                                    <SelectTrigger><SelectValue /></SelectTrigger>
                                    <SelectContent>{types.map(type => <SelectItem key={type} value={type}>{type.replace(/([A-Z])/g, ' $1').trim()}</SelectItem>)}</SelectContent>
                                </Select>
                            </div>
                        </div>
                        {form.accountType === 'Bank' && (
                            <div className="space-y-2">
                                <Label>Bank account master</Label>
                                <Select required value={form.bankAccountId} onValueChange={changeBankAccount}>
                                    <SelectTrigger><SelectValue placeholder="Select bank account" /></SelectTrigger>
                                    <SelectContent>{banks.map(bank => <SelectItem key={bank.id} value={bank.id}>{bank.bankName} — {bank.accountName}</SelectItem>)}</SelectContent>
                                </Select>
                            </div>
                        )}
                        <div className="space-y-2">
                            <Label>GL control account</Label>
                            <Select
                                required
                                disabled={form.accountType === 'Bank'}
                                value={form.glAccountId}
                                onValueChange={glAccountId => setForm(current => ({ ...current, glAccountId, currency: '' }))}
                            >
                                <SelectTrigger><SelectValue placeholder={form.accountType === 'Bank' ? 'Derived from bank account master' : 'Select GL account'} /></SelectTrigger>
                                <SelectContent>{accounts.map(account => <SelectItem key={account.id} value={account.id}>{account.accountNumber} — {account.accountName}</SelectItem>)}</SelectContent>
                            </Select>
                            {form.accountType === 'Bank' && <p className="text-xs text-muted-foreground">Derived from the selected bank account master.</p>}
                        </div>
                        <div className="space-y-2">
                            <Label>Currency</Label>
                            <Select
                                required
                                disabled={!form.glAccountId || loadingCurrencyLinks || form.accountType === 'Bank'}
                                value={form.currency}
                                onValueChange={currency => setForm({ ...form, currency })}
                            >
                                <SelectTrigger>
                                    <SelectValue placeholder={loadingCurrencyLinks ? 'Loading supported currencies…' : 'Select GL account first'} />
                                </SelectTrigger>
                                <SelectContent>
                                    {eligibleCurrencies.map(currency => (
                                        <SelectItem key={currency.id} value={currency.currencyCode}>
                                            {currency.currencyCode} — {currency.currencyName}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                            <p className="text-xs text-muted-foreground">
                                {form.accountType === 'Bank'
                                    ? 'Derived from the selected bank account master.'
                                    : 'Limited to currencies supported by the selected GL control account.'}
                            </p>
                        </div>
                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2"><Label>Provider (optional)</Label><Input value={form.providerName} onChange={e => setForm({ ...form, providerName: e.target.value })} /></div>
                            <div className="space-y-2"><Label>Provider reference</Label><Input value={form.providerAccountReference} onChange={e => setForm({ ...form, providerAccountReference: e.target.value })} /></div>
                        </div>
                        <div className="space-y-2"><Label>Notes</Label><Textarea value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} /></div>
                        <div className="flex justify-end gap-2"><Button type="button" variant="outline" asChild><Link href="/finance/cash/liquidity-accounts">Cancel</Link></Button><Button disabled={saving || !form.glAccountId || !form.currency}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Create</Button></div>
                    </form>
                </CardContent>
            </Card>
        </div>
    );
}
