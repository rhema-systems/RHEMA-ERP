'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type {
    BankAccount,
    BankingSetupStatus,
    LiquidityAccountEntry,
    PostedLiquidityPaymentCandidate,
} from '@/types/cash-management';

export default function NewBankDepositPage() {
    const router = useRouter();
    const [banks, setBanks] = useState<BankAccount[]>([]);
    const [entries, setEntries] = useState<LiquidityAccountEntry[]>([]);
    const [paymentCandidates, setPaymentCandidates] = useState<PostedLiquidityPaymentCandidate[]>([]);
    const [setup, setSetup] = useState<BankingSetupStatus | null>(null);
    const [selected, setSelected] = useState<Record<string, number>>({});
    const [search, setSearch] = useState('');
    const [saving, setSaving] = useState(false);
    const [importingPayment, setImportingPayment] = useState(false);
    const [paymentCandidateId, setPaymentCandidateId] = useState('');
    const [paymentEntryType, setPaymentEntryType] = useState<
        'CashExpense' | 'PettyCashReplenishment' | 'CustomerRefund' | 'OtherPayment'
    >('CashExpense');
    const [form, setForm] = useState({
        bankAccountId: '',
        depositDate: new Date().toISOString().slice(0, 10),
        depositReference: '',
        notes: '',
    });

    useEffect(() => {
        void Promise.all([
            cashManagementDataService.getActiveBankAccounts(),
            cashManagementDataService.getEligibleLiquidityEntries(),
            cashManagementDataService.getBankingSetup(),
            cashManagementDataService.getPostedPaymentCandidates(),
        ]).then(([bankAccounts, openEntries, bankingSetup, postedPayments]) => {
            setBanks(bankAccounts);
            setEntries(openEntries);
            setSetup(bankingSetup);
            setPaymentCandidates(postedPayments);
        }).catch(error => toast.error(error instanceof Error ? error.message : 'Could not load banking queue.'));
    }, []);

    const visibleEntries = useMemo(() => {
        const bank = banks.find(item => item.id === form.bankAccountId);
        const term = search.trim().toLowerCase();
        return entries.filter(entry =>
            (!bank || entry.currency === bank.currency) &&
            (!term || [entry.entryNumber, entry.counterpartyName, entry.referenceNumber, entry.description, entry.liquidityAccountName]
                .some(value => value?.toLowerCase().includes(term))),
        );
    }, [banks, entries, form.bankAccountId, search]);

    const totals = useMemo(() => {
        let receipts = 0;
        let deductions = 0;
        entries.forEach(entry => {
            const amount = selected[entry.id] ?? 0;
            if (entry.direction === 'Increase') receipts += amount;
            else deductions += amount;
        });
        return { receipts, deductions, net: receipts - deductions };
    }, [entries, selected]);

    const toggle = (entry: LiquidityAccountEntry, checked: boolean) => {
        setSelected(current => {
            const next = { ...current };
            if (checked) next[entry.id] = entry.remainingAmount;
            else delete next[entry.id];
            return next;
        });
    };

    const importPostedPayment = async () => {
        if (!paymentCandidateId) return;
        setImportingPayment(true);
        try {
            await cashManagementDataService.registerPostedPayment(paymentCandidateId, paymentEntryType);
            const [openEntries, postedPayments] = await Promise.all([
                cashManagementDataService.getEligibleLiquidityEntries(),
                cashManagementDataService.getPostedPaymentCandidates(),
            ]);
            setEntries(openEntries);
            setPaymentCandidates(postedPayments);
            setPaymentCandidateId('');
            toast.success('Posted payment added to the banking queue.');
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not add the posted payment.');
        } finally {
            setImportingPayment(false);
        }
    };

    const submit = async (event: FormEvent) => {
        event.preventDefault();
        if (totals.net <= 0) {
            toast.error('Select receipts with a positive net amount.');
            return;
        }
        setSaving(true);
        try {
            const deposit = await cashManagementDataService.createBankDeposit({
                ...form,
                allocations: entries.filter(entry => selected[entry.id] > 0).map(entry => ({
                    liquidityAccountEntryId: entry.id,
                    allocationType: entry.direction === 'Increase' ? 'Receipt' : 'Deduction',
                    amount: selected[entry.id],
                })),
            });
            toast.success('Draft deposit created. Attach the deposit slip before submission.');
            router.push(`/finance/cash/deposits/${deposit.id}`);
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not create deposit.');
        } finally {
            setSaving(false);
        }
    };

    if (setup?.requiresProvisioningWizard) {
        return (
            <div className="mx-auto max-w-2xl p-6">
                <Card><CardHeader><CardTitle>Complete Banking setup first</CardTitle><CardDescription>Map the tenant’s holding accounts before creating a deposit.</CardDescription></CardHeader><CardContent><Button asChild><Link href="/finance/cash/liquidity-accounts">Open setup wizard</Link></Button></CardContent></Card>
            </div>
        );
    }

    return (
        <form onSubmit={submit} className="space-y-6 p-6">
            <div className="flex items-start gap-3">
                <Button variant="ghost" size="icon" asChild><Link href="/finance/cash/deposits"><ArrowLeft className="h-4 w-4" /></Link></Button>
                <div><h1 className="text-3xl font-bold">New Bank Deposit</h1><p className="text-muted-foreground">Select posted receipts and eligible payments; partial settlement is supported.</p></div>
            </div>
            <Card>
                <CardHeader><CardTitle>Deposit header</CardTitle><CardDescription>One deposit represents one expected bank-statement line.</CardDescription></CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-2">
                    <div className="space-y-2"><Label>Destination bank</Label><Select required value={form.bankAccountId} onValueChange={bankAccountId => setForm({ ...form, bankAccountId })}><SelectTrigger><SelectValue placeholder="Select bank account" /></SelectTrigger><SelectContent>{banks.map(bank => <SelectItem key={bank.id} value={bank.id}>{bank.bankName} — {bank.accountName} ({bank.currency})</SelectItem>)}</SelectContent></Select></div>
                    <div className="space-y-2"><Label>Deposit date</Label><Input required type="date" value={form.depositDate} onChange={event => setForm({ ...form, depositDate: event.target.value })} /></div>
                    <div className="space-y-2"><Label>Deposit slip / bank reference</Label><Input required maxLength={100} value={form.depositReference} onChange={event => setForm({ ...form, depositReference: event.target.value })} /></div>
                    <div className="space-y-2"><Label>Policy</Label><Input disabled value={setup?.depositPolicy === 'ControlledNetBanking' ? 'Controlled net banking' : 'Deposit intact'} /></div>
                    <div className="space-y-2 md:col-span-2"><Label>Notes</Label><Textarea value={form.notes} onChange={event => setForm({ ...form, notes: event.target.value })} /></div>
                </CardContent>
            </Card>
            {setup?.depositPolicy === 'ControlledNetBanking' && (
                <Card>
                    <CardHeader>
                        <CardTitle>Add a posted payment</CardTitle>
                        <CardDescription>
                            Only posted GL credits to a mapped liquidity control account are eligible. This prevents unposted or free-form deductions from reducing a deposit.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="grid gap-4 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_auto]">
                        <div className="space-y-2">
                            <Label>Posted journal line</Label>
                            <Select value={paymentCandidateId || undefined} onValueChange={setPaymentCandidateId}>
                                <SelectTrigger><SelectValue placeholder="Select an eligible posted payment" /></SelectTrigger>
                                <SelectContent>
                                    {paymentCandidates.map(candidate => (
                                        <SelectItem key={candidate.accountTransactionId} value={candidate.accountTransactionId}>
                                            {candidate.journalEntryNumber} — {candidate.liquidityAccountName} — {candidate.currency} {candidate.amount.toFixed(2)}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Classification</Label>
                            <Select value={paymentEntryType} onValueChange={value => setPaymentEntryType(value as typeof paymentEntryType)}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="CashExpense">Cash expense</SelectItem>
                                    <SelectItem value="PettyCashReplenishment">Petty-cash replenishment</SelectItem>
                                    <SelectItem value="CustomerRefund">Customer refund</SelectItem>
                                    <SelectItem value="OtherPayment">Other payment</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <Button type="button" className="self-end" disabled={!paymentCandidateId || importingPayment} onClick={importPostedPayment}>
                            {importingPayment && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Add to queue
                        </Button>
                    </CardContent>
                </Card>
            )}
            <Card>
                <CardHeader>
                    <CardTitle>Banking queue</CardTitle>
                    <CardDescription>Receipt lines add to the deposit; eligible payment lines reduce it when tenant policy permits.</CardDescription>
                    <div className="relative max-w-lg"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" placeholder="Search receipt, customer, reference…" value={search} onChange={event => setSearch(event.target.value)} /></div>
                </CardHeader>
                <CardContent>
                    <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                            <thead className="border-b text-left text-muted-foreground"><tr><th className="w-12 p-3"></th><th className="p-3">Source</th><th className="p-3">Counterparty / reference</th><th className="p-3">Holding account</th><th className="p-3">Kind</th><th className="p-3 text-right">Available</th><th className="w-40 p-3 text-right">Allocate</th></tr></thead>
                            <tbody>{visibleEntries.map(entry => {
                                const checked = selected[entry.id] !== undefined;
                                const deductionDisabled = entry.direction === 'Decrease' && setup?.depositPolicy !== 'ControlledNetBanking';
                                const chequeMustRemainIntact = entry.entryType === 'CustomerReceipt'
                                    && setup?.accounts.some(account =>
                                        account.id === entry.liquidityAccountId
                                        && account.accountType === 'ChequesAwaitingDeposit');
                                return (
                                    <tr key={entry.id} className="border-b">
                                        <td className="p-3"><Checkbox checked={checked} disabled={deductionDisabled} onCheckedChange={value => toggle(entry, value === true)} /></td>
                                        <td className="p-3"><div className="font-medium">{entry.entryNumber}</div><div className="text-xs text-muted-foreground">{new Date(entry.entryDate).toLocaleDateString()}</div></td>
                                        <td className="p-3"><div>{entry.counterpartyName ?? entry.description}</div><div className="text-xs text-muted-foreground">{entry.referenceNumber}</div></td>
                                        <td className="p-3">{entry.liquidityAccountName}</td>
                                        <td className={`p-3 ${entry.direction === 'Decrease' ? 'text-red-600' : 'text-green-700'}`}>{entry.direction === 'Increase' ? 'Receipt' : 'Deduction'}</td>
                                        <td className="p-3 text-right">{entry.currency} {entry.remainingAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                                        <td className="p-3"><Input className="text-right" type="number" min={0.01} max={entry.remainingAmount} step="0.01" disabled={!checked || chequeMustRemainIntact} value={selected[entry.id] ?? ''} onChange={event => setSelected(current => ({ ...current, [entry.id]: Math.min(Number(event.target.value), entry.remainingAmount) }))} /></td>
                                    </tr>
                                );
                            })}</tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>
            <div className="sticky bottom-0 flex flex-wrap items-center justify-between gap-4 border bg-background p-4 shadow-sm">
                <div className="flex gap-6 text-sm">
                    <div><p className="text-muted-foreground">Receipts</p><p className="font-semibold text-green-700">GHS {totals.receipts.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p></div>
                    <div><p className="text-muted-foreground">Deductions</p><p className="font-semibold text-red-600">GHS {totals.deductions.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p></div>
                    <div><p className="text-muted-foreground">Net to bank</p><p className="text-xl font-bold">GHS {totals.net.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p></div>
                </div>
                <Button disabled={saving || !form.bankAccountId || !form.depositReference || totals.net <= 0}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save draft</Button>
            </div>
        </form>
    );
}
