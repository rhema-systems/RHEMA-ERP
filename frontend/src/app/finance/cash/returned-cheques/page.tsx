'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ArrowLeft, Loader2, Plus, RotateCcw } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
    SettlementDimensionEvidence,
    SourceDocumentDimensionEvidence,
    SourceDocumentDimensionPanel,
} from '@/components/finance/dimensions/source-document-dimension-panel';
import {
    toFinancePostingDimensionValues,
    toFinanceSourceDimensionFormState,
} from '@/lib/finance/source-document-dimensions';
import { arService } from '@/services/ar-service';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { CustomerPayment } from '@/types/ar';
import type { BankDeposit, ReturnedChequeCase } from '@/types/cash-management';
import type { FinanceSettings } from '@/types/finance';

type ReturnedChequeChargeTreatment = 'CustomerRecoverable' | 'BankChargeExpense' | 'Split';

function createInitialForm(chargeTreatment: ReturnedChequeChargeTreatment) {
    return {
        customerPaymentId: '',
        bankDepositBatchId: '',
        bankAccountId: '',
        returnDate: new Date().toISOString().slice(0, 10),
        bankReference: '',
        returnReason: '',
        bankChargeAmount: 0,
        chargeTreatment,
        customerRecoverableChargeAmount: 0,
        expenseChargeAmount: 0,
        drawerBank: '',
        notes: '',
    };
}

function ReturnedChequeDimensionEditor({
    item,
    onSaved,
}: {
    item: ReturnedChequeCase;
    onSaved: () => Promise<void>;
}) {
    const initial = toFinanceSourceDimensionFormState(item.financeDimensions);
    const [defaultValues, setDefaultValues] = useState(initial.defaultValues);
    const [lineValues, setLineValues] = useState(initial.lineValues);
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);
    const [saving, setSaving] = useState(false);
    const editable = item.status === 'Draft' || item.status === 'Returned';
    const lines = item.financeDimensions?.lines.map((line, index) => ({
        id: line.sourceLineId,
        accountId: line.accountId,
        accountLabel: index === 0
            ? `Bank · ${item.bankAccountName}`
            : index === 1
                ? `Customer balance · ${item.customerName}`
                : 'Bank-charge expense',
    })) ?? [];

    useEffect(() => {
        const hydrated = toFinanceSourceDimensionFormState(item.financeDimensions);
        setDefaultValues(hydrated.defaultValues);
        setLineValues(hydrated.lineValues);
        setApplyDefaultToAll(false);
    }, [item.financeDimensions]);

    if (!editable) return <SourceDocumentDimensionEvidence evidence={item.financeDimensions} />;

    const save = async () => {
        setSaving(true);
        try {
            await cashManagementDataService.updateReturnedChequeDimensions(item.id, {
                defaultDimensions: toFinancePostingDimensionValues(defaultValues),
                lines: lines.map(line => ({
                    sourceLineId: line.id,
                    accountId: line.accountId,
                    dimensions: toFinancePostingDimensionValues(lineValues[line.id] || {}),
                })),
                applyDefaultToEligibleLines: applyDefaultToAll,
            });
            toast.success('Returned-cheque Finance coding saved.');
            await onSaved();
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not save Finance coding.');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="space-y-3">
            <SourceDocumentDimensionPanel
                context={{
                    sourceModule: 'CASHBANK',
                    sourceDocumentType: 'ReturnedChequeCase',
                    postingAction: 'Post',
                    sourceRoute: 'finance.cash.returned-cheques',
                    contractVersion: '1.0',
                }}
                effectiveDate={item.returnDate.slice(0, 10)}
                lines={lines}
                defaultValues={defaultValues}
                lineValues={lineValues}
                onDefaultValuesChange={(values) => {
                    setDefaultValues(values);
                    setApplyDefaultToAll(false);
                }}
                onLineValuesChange={setLineValues}
                onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                certificationState={item.financeDimensions?.certificationState}
                disabled={saving}
            />
            <div className="flex justify-end">
                <Button size="sm" disabled={saving || lines.length === 0} onClick={() => void save()}>
                    {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Save Finance coding
                </Button>
            </div>
        </div>
    );
}

export default function ReturnedChequesPage() {
    const [items, setItems] = useState<ReturnedChequeCase[]>([]);
    const [payments, setPayments] = useState<CustomerPayment[]>([]);
    const [postedDeposits, setPostedDeposits] = useState<BankDeposit[]>([]);
    const [showForm, setShowForm] = useState(false);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [evidence, setEvidence] = useState<File | null>(null);
    const [defaultChargeTreatment, setDefaultChargeTreatment] = useState<ReturnedChequeChargeTreatment>('CustomerRecoverable');
    const [financeSettings, setFinanceSettings] = useState<FinanceSettings | null>(null);
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);
    const [form, setForm] = useState(() => createInitialForm('CustomerRecoverable'));

    const load = async () => {
        try {
            const [cases, receiptPage, deposits, financeSettings] = await Promise.all([
                cashManagementDataService.getReturnedCheques(),
                arService.getPayments({ pageSize: 500, status: 'Posted' }),
                cashManagementDataService.getBankDeposits('Posted'),
                financeDataService.getFinanceSettings(),
            ]);
            const configuredTreatment = financeSettings.defaultReturnedChequeChargeTreatment ?? 'CustomerRecoverable';
            setFinanceSettings(financeSettings);
            setDefaultChargeTreatment(configuredTreatment);
            setForm(current => showForm ? current : { ...current, chargeTreatment: configuredTreatment });
            setItems(cases);
            const returnedPaymentIds = new Set(cases
                .filter(item => item.status !== 'Rejected')
                .map(item => item.customerPaymentId));
            setPayments(receiptPage.items.filter(payment =>
                /cheque|check/i.test(payment.paymentMethodName ?? payment.paymentMethod)
                && !returnedPaymentIds.has(payment.id)
                && deposits.some(deposit => deposit.allocations.some(allocation =>
                    allocation.sourceDocumentType === 'CustomerPayment'
                    && allocation.sourceDocumentId === payment.id)),
            ));
            setPostedDeposits(deposits);
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not load returned cheques.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { void load(); }, []);

    const toggleForm = () => {
        if (showForm) {
            setShowForm(false);
            return;
        }

        setForm(createInitialForm(defaultChargeTreatment));
        setEvidence(null);
        setDefaultDimensionValues({});
        setLineDimensionValues({});
        setApplyDefaultToAll(false);
        setShowForm(true);
    };

    const selectedPayment = useMemo(
        () => payments.find(payment => payment.id === form.customerPaymentId),
        [form.customerPaymentId, payments],
    );
    const selectedDeposit = useMemo(
        () => postedDeposits.find(deposit => deposit.allocations.some(allocation =>
            allocation.sourceDocumentType === 'CustomerPayment'
            && allocation.sourceDocumentId === form.customerPaymentId)),
        [form.customerPaymentId, postedDeposits],
    );

    useEffect(() => {
        if (!selectedDeposit) return;
        setForm(current => ({
            ...current,
            bankDepositBatchId: selectedDeposit.id,
            bankAccountId: selectedDeposit.bankAccountId,
            drawerBank: current.drawerBank || selectedPayment?.chequeDrawerBank || '',
        }));
    }, [selectedDeposit, selectedPayment?.chequeDrawerBank]);

    const expenseChargeAmount = form.chargeTreatment === 'BankChargeExpense'
        ? form.bankChargeAmount
        : form.chargeTreatment === 'Split'
            ? form.expenseChargeAmount
            : 0;
    const dimensionLines = useMemo(() => [
        ...(selectedDeposit?.bankGLAccountId ? [{
            id: selectedDeposit.bankAccountId,
            accountId: selectedDeposit.bankGLAccountId,
            accountLabel: `Bank debit · ${selectedDeposit.bankAccountName}`,
        }] : []),
        ...(financeSettings?.controlAccountArId ? [{
            id: financeSettings.controlAccountArId,
            accountId: financeSettings.controlAccountArId,
            accountLabel: 'Customer balance / recoverable charge',
        }] : []),
        ...(expenseChargeAmount > 0 && financeSettings?.returnedChequeBankChargeAccountId ? [{
            id: financeSettings.returnedChequeBankChargeAccountId,
            accountId: financeSettings.returnedChequeBankChargeAccountId,
            accountLabel: 'Bank-charge expense',
        }] : []),
    ], [expenseChargeAmount, financeSettings, selectedDeposit]);

    const submit = async (event: FormEvent) => {
        event.preventDefault();
        if (!evidence) {
            toast.error('Attach the bank return advice before submission.');
            return;
        }
        setSaving(true);
        try {
            const item = await cashManagementDataService.createReturnedCheque({
                ...form,
                customerRecoverableChargeAmount: form.chargeTreatment === 'Split' ? form.customerRecoverableChargeAmount : undefined,
                expenseChargeAmount: form.chargeTreatment === 'Split' ? form.expenseChargeAmount : undefined,
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: dimensionLines.map(line => ({
                        sourceLineId: line.id,
                        accountId: line.accountId,
                        dimensions: toFinancePostingDimensionValues(lineDimensionValues[line.id] || {}),
                    })),
                    applyDefaultToEligibleLines: applyDefaultToAll,
                },
            });
            const fileId = await cashManagementDataService.uploadBankingEvidence(evidence);
            await cashManagementDataService.linkReturnedChequeAttachment(item.id, fileId);
            await cashManagementDataService.submitReturnedCheque(item.id);
            toast.success('Returned cheque submitted to the Chief Accountant.');
            setShowForm(false);
            await load();
        } catch (error) {
            toast.error(error instanceof Error ? error.message : 'Could not submit returned cheque.');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                    <Button variant="ghost" size="icon" asChild><Link href="/finance/cash"><ArrowLeft className="h-4 w-4" /></Link></Button>
                    <div><h1 className="text-3xl font-bold">Returned Cheques</h1><p className="text-muted-foreground">Reopen AR and record bank debits for deposited customer cheques.</p></div>
                </div>
                <Button disabled={loading} onClick={toggleForm}><Plus className="mr-2 h-4 w-4" />Record returned cheque</Button>
            </div>
            {showForm && (
                <Card>
                    <CardHeader><CardTitle>Bank return advice</CardTitle><CardDescription>The original deposit remains in history; approval creates the bank debit and reopens the customer balance.</CardDescription></CardHeader>
                    <CardContent>
                        <form onSubmit={submit} className="space-y-5">
                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2"><Label>Cheque receipt</Label><Select required value={form.customerPaymentId} onValueChange={customerPaymentId => setForm({ ...form, customerPaymentId })}><SelectTrigger><SelectValue placeholder="Select posted cheque receipt" /></SelectTrigger><SelectContent>{payments.map(payment => <SelectItem key={payment.id} value={payment.id}>{payment.paymentNumber} — {payment.customerName} — {payment.currencyCode} {payment.totalAmount.toLocaleString()}</SelectItem>)}</SelectContent></Select></div>
                                <div className="space-y-2"><Label>Deposited to</Label><Input disabled value={selectedDeposit ? `${selectedDeposit.bankAccountName} — ${selectedDeposit.depositNumber}` : ''} /><p className="text-xs text-muted-foreground">Derived from the posted deposit; the bank cannot be changed.</p></div>
                                <div className="space-y-2"><Label>Return date</Label><Input required type="date" value={form.returnDate} onChange={event => setForm({ ...form, returnDate: event.target.value })} /></div>
                                <div className="space-y-2"><Label>Bank reference</Label><Input required value={form.bankReference} onChange={event => setForm({ ...form, bankReference: event.target.value })} /></div>
                                <div className="space-y-2"><Label>Drawer bank</Label><Input value={form.drawerBank} onChange={event => setForm({ ...form, drawerBank: event.target.value })} /></div>
                                <div className="space-y-2"><Label>Returned amount</Label><Input disabled value={selectedPayment ? `${selectedPayment.currencyCode} ${selectedPayment.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}` : ''} /></div>
                                <div className="space-y-2"><Label>Bank charge</Label><Input min={0} step="0.01" type="number" value={form.bankChargeAmount} onChange={event => setForm({ ...form, bankChargeAmount: Number(event.target.value) })} /></div>
                                <div className="space-y-2"><Label>Charge treatment</Label><Select value={form.chargeTreatment} onValueChange={chargeTreatment => setForm({ ...form, chargeTreatment: chargeTreatment as typeof form.chargeTreatment })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="CustomerRecoverable">Recover from customer</SelectItem><SelectItem value="BankChargeExpense">Bank charge expense</SelectItem><SelectItem value="Split">Split</SelectItem></SelectContent></Select><p className="text-xs text-muted-foreground">Defaults from Finance Settings; you can change it for this case.</p></div>
                                {form.chargeTreatment === 'Split' && <><div className="space-y-2"><Label>Customer portion</Label><Input type="number" min={0} step="0.01" value={form.customerRecoverableChargeAmount} onChange={event => setForm({ ...form, customerRecoverableChargeAmount: Number(event.target.value) })} /></div><div className="space-y-2"><Label>Expense portion</Label><Input type="number" min={0} step="0.01" value={form.expenseChargeAmount} onChange={event => setForm({ ...form, expenseChargeAmount: Number(event.target.value) })} /></div></>}
                                <div className="space-y-2 md:col-span-2"><Label>Return reason</Label><Textarea required value={form.returnReason} onChange={event => setForm({ ...form, returnReason: event.target.value })} /></div>
                                <div className="space-y-2"><Label>Bank return advice (PDF/image)</Label><Input required type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={event => setEvidence(event.target.files?.[0] ?? null)} /></div>
                                <div className="space-y-2"><Label>Notes</Label><Input value={form.notes} onChange={event => setForm({ ...form, notes: event.target.value })} /></div>
                            </div>
                            <div className="space-y-3 border-t pt-5">
                                <div>
                                    <h3 className="font-medium">Finance coding dimensions</h3>
                                    <p className="text-sm text-muted-foreground">
                                        The original receipt supplies principal and discount combinations. Code the bank and any charge lines independently.
                                    </p>
                                </div>
                                <SourceDocumentDimensionPanel
                                    context={{
                                        sourceModule: 'CASHBANK',
                                        sourceDocumentType: 'ReturnedChequeCase',
                                        postingAction: 'Post',
                                        sourceRoute: 'finance.cash.returned-cheques',
                                        contractVersion: '1.0',
                                    }}
                                    effectiveDate={form.returnDate}
                                    lines={dimensionLines}
                                    defaultValues={defaultDimensionValues}
                                    lineValues={lineDimensionValues}
                                    onDefaultValuesChange={(values) => {
                                        setDefaultDimensionValues(values);
                                        setApplyDefaultToAll(false);
                                    }}
                                    onLineValuesChange={setLineDimensionValues}
                                    onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                                    disabled={saving}
                                />
                            </div>
                            <div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={() => setShowForm(false)}>Cancel</Button><Button disabled={saving}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Create & submit</Button></div>
                        </form>
                    </CardContent>
                </Card>
            )}
            <Card>
                <CardHeader><CardTitle>Returned-cheque register</CardTitle></CardHeader>
                <CardContent>
                    {loading ? <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin" /></div> : items.length === 0 ? <div className="py-12 text-center text-muted-foreground"><RotateCcw className="mx-auto mb-3 h-9 w-9" />No returned cheques recorded.</div> : (
                        <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="border-b text-left text-muted-foreground"><tr><th className="p-3">Case</th><th className="p-3">Customer / cheque</th><th className="p-3">Bank reference</th><th className="p-3">Return date</th><th className="p-3 text-right">Bank debit</th><th className="p-3">Status</th><th className="p-3"></th></tr></thead><tbody>{items.map(item => <tr key={item.id} className="border-b"><td className="p-3 font-medium">{item.caseNumber}</td><td className="p-3"><div>{item.customerName}</div><div className="text-xs text-muted-foreground">{item.chequeNumber} · {item.paymentNumber}</div></td><td className="p-3"><div>{item.bankAccountName}</div><div className="text-xs text-muted-foreground">{item.bankReference}</div></td><td className="p-3">{new Date(item.returnDate).toLocaleDateString()}</td><td className="p-3 text-right">{(item.returnedAmount + item.bankChargeAmount).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td><td className="p-3"><Badge variant={item.status === 'Posted' ? 'default' : item.status === 'Rejected' ? 'destructive' : 'secondary'}>{item.status}</Badge></td><td className="p-3">{item.status === 'Submitted' && <Button size="sm" onClick={() => void cashManagementDataService.approveReturnedCheque(item.id).then(() => load()).catch(error => toast.error(error instanceof Error ? error.message : 'Approval failed.'))}>Approve</Button>}</td></tr>)}</tbody></table></div>
                    )}
                    {items.length > 0 && (
                        <div className="mt-5 space-y-3 border-t pt-5">
                            <h3 className="font-medium">Internal dimension evidence</h3>
                            {items.map(item => (
                                <details key={`dimensions-${item.id}`} className="rounded-md border p-3">
                                    <summary className="cursor-pointer text-sm font-medium">
                                        {item.caseNumber} · {item.customerName}
                                    </summary>
                                    <div className="mt-3 space-y-3">
                                        <ReturnedChequeDimensionEditor item={item} onSaved={load} />
                                        <SettlementDimensionEvidence evidence={item.settlementDimensionEvidence} />
                                    </div>
                                </details>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
