'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertCircle, ArrowLeft, Landmark, Loader2, Plus, WalletCards } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { BankingSetupStatus, DepositPolicy, LiquidityAccountType } from '@/types/cash-management';
import type { Account } from '@/types/finance';

const labels: Record<LiquidityAccountType, string> = {
    Bank: 'Bank Account',
    UndepositedCash: 'Undeposited Cash',
    ChequesAwaitingDeposit: 'Cheques Awaiting Deposit',
    MobileMoneyClearing: 'Mobile Money Clearing',
    CardSettlementClearing: 'Card Settlement Clearing',
    CashTill: 'Cash Till',
    OtherSettlementClearing: 'Other Settlement Clearing',
};

const defaultCodes: Partial<Record<LiquidityAccountType, string>> = {
    UndepositedCash: 'UNDEP-CASH',
    ChequesAwaitingDeposit: 'CHQ-CLEAR',
    MobileMoneyClearing: 'MOMO-CLEAR',
    CardSettlementClearing: 'CARD-CLEAR',
};

export default function LiquidityAccountsPage() {
    const [setup, setSetup] = useState<BankingSetupStatus | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [mappings, setMappings] = useState<Record<string, string>>({});
    const [policy, setPolicy] = useState<DepositPolicy>('DepositIntact');
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);

    const load = async () => {
        setLoading(true);
        try {
            const status = await cashManagementDataService.getBankingSetup();
            setSetup(status);
            if (status.requiresProvisioningWizard) {
                const chart = await financeDataService.getAccounts({ status: 'Active' });
                setAccounts(chart.filter(account => account.allowDirectPosting || account.isPostingAllowed === true));
            }
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not load liquidity accounts.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void load();
    }, []);

    const allMapped = useMemo(
        () => setup?.missingAccountTypes.every(type => Boolean(mappings[type])) ?? false,
        [mappings, setup],
    );

    const completeSetup = async () => {
        if (!setup || !allMapped) return;
        setSaving(true);
        try {
            await cashManagementDataService.completeBankingSetup({
                depositPolicy: policy,
                requirePrimaryEvidence: true,
                accounts: setup.missingAccountTypes.map(type => ({
                    accountType: type,
                    code: `${defaultCodes[type] ?? type.toUpperCase()}-${setup.baseCurrency}`,
                    name: `${labels[type]} (${setup.baseCurrency})`,
                    currency: setup.baseCurrency,
                    glAccountId: mappings[type],
                })),
            });
            toast.success('Banking & Settlement setup completed.');
            await load();
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Setup could not be completed.');
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return <div className="flex min-h-[50vh] items-center justify-center"><Loader2 className="h-7 w-7 animate-spin" /></div>;
    }

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                    <Button variant="ghost" size="icon" asChild><Link href="/finance/cash"><ArrowLeft className="h-4 w-4" /></Link></Button>
                    <div>
                        <h1 className="text-3xl font-bold">Liquidity Accounts</h1>
                        <p className="text-muted-foreground">Bank, till, and settlement locations with their own GL control accounts.</p>
                    </div>
                </div>
                {setup?.isConfigured && <Button asChild><Link href="/finance/cash/liquidity-accounts/new"><Plus className="mr-2 h-4 w-4" />New account</Link></Button>}
            </div>

            {setup?.requiresProvisioningWizard ? (
                <Card className="border-amber-300">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><AlertCircle className="h-5 w-5 text-amber-600" />Banking setup required</CardTitle>
                        <CardDescription>
                            This tenant uses its existing {setup.coaType} chart of accounts. Map each operational
                            holding location to an established GL account; the system will not silently add accounts to your COA.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-5">
                        {setup.missingAccountTypes.map(type => (
                            <div key={type} className="grid gap-2 md:grid-cols-[260px_1fr] md:items-center">
                                <div>
                                    <Label>{labels[type]}</Label>
                                    <p className="text-xs text-muted-foreground">{defaultCodes[type]} · {setup.baseCurrency}</p>
                                </div>
                                <Select value={mappings[type] ?? ''} onValueChange={value => setMappings(current => ({ ...current, [type]: value }))}>
                                    <SelectTrigger><SelectValue placeholder="Select an existing GL control account" /></SelectTrigger>
                                    <SelectContent>
                                        {accounts.map(account => (
                                            <SelectItem key={account.id} value={account.id}>
                                                {account.accountNumber} — {account.accountName}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                            </div>
                        ))}
                        <div className="grid gap-2 md:grid-cols-[260px_1fr] md:items-center">
                            <div>
                                <Label>Deposit policy</Label>
                                <p className="text-xs text-muted-foreground">Can approved expenses reduce the amount banked?</p>
                            </div>
                            <Select value={policy} onValueChange={value => setPolicy(value as DepositPolicy)}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="DepositIntact">Deposit intact — no deductions</SelectItem>
                                    <SelectItem value="ControlledNetBanking">Controlled net banking</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="rounded-md bg-muted p-3 text-sm">
                            Deposit slips and bank-return advice are required before workflow submission. The Chief Accountant is the default approver.
                        </div>
                        <Button onClick={completeSetup} disabled={!allMapped || saving}>
                            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Complete setup
                        </Button>
                    </CardContent>
                </Card>
            ) : (
                <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {setup?.accounts.map(account => (
                        <Card key={account.id}>
                            <CardHeader className="pb-3">
                                <div className="flex items-start justify-between gap-3">
                                    <div className="flex gap-3">
                                        {account.accountType === 'Bank' ? <Landmark className="mt-1 h-5 w-5" /> : <WalletCards className="mt-1 h-5 w-5" />}
                                        <div><CardTitle className="text-lg">{account.name}</CardTitle><CardDescription>{account.code}</CardDescription></div>
                                    </div>
                                    <Badge variant={account.isActive ? 'default' : 'secondary'}>{account.isActive ? 'Active' : 'Inactive'}</Badge>
                                </div>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <div className="text-2xl font-semibold">{account.currency} {account.currentBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</div>
                                <div className="grid grid-cols-2 gap-3 text-sm">
                                    <div><p className="text-muted-foreground">Available</p><p>{account.currency} {account.availableToSettle.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p></div>
                                    <div><p className="text-muted-foreground">Open items</p><p>{account.openEntryCount}</p></div>
                                </div>
                                <div className="border-t pt-3 text-sm">
                                    <p className="text-muted-foreground">GL control account</p>
                                    <p>{account.glAccountNumber} — {account.glAccountName}</p>
                                </div>
                            </CardContent>
                        </Card>
                    ))}
                </div>
            )}
        </div>
    );
}
