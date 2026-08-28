'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { journalBatchDataService } from '@/services/finance/journal-batch-data.service';
import type { Currency, FiscalPeriod } from '@/types/finance';

export default function NewJournalBatchPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [saving, setSaving] = useState(false);
    const [form, setForm] = useState({
        description: '',
        fiscalPeriodId: '',
        bookClassification: 'IFRS',
        controlCurrencyCode: '',
        expectedDebitTotal: '',
        expectedJournalCount: '',
        notes: '',
    });

    useEffect(() => {
        // Journal-batch totals are functional-currency controls, even when member journals
        // retain foreign-currency evidence. Load the governed catalogue so the UI can expose
        // only the configured active base currency instead of accepting a free-text ISO code
        // that JournalBatchService must reject later.
        Promise.all([
            financeDataService.getFiscalPeriods(),
            financeDataService.getCurrencies({ isActive: true }),
        ])
            .then(([periodData, currencyData]) => {
                setPeriods(periodData);
                const baseCurrency = currencyData.find((currency) => currency.isBaseCurrency);
                setCurrencies(baseCurrency ? [baseCurrency] : []);
                setForm((current) => ({
                    ...current,
                    controlCurrencyCode: baseCurrency?.currencyCode ?? '',
                }));
            })
            .catch((error) => toast({
                title: 'Batch prerequisites unavailable',
                description: error.message,
                variant: 'destructive',
            }));
    }, [toast]);

    const openPeriods = useMemo(
        () => periods.filter((period) => period.periodStatus === 'Open' || period.status === 'Open' || period.isOpen),
        [periods],
    );

    const create = async () => {
        if (!form.description.trim() || !form.fiscalPeriodId || !form.controlCurrencyCode || Number(form.expectedDebitTotal) <= 0) {
            toast({ title: 'Complete required fields', description: 'Description, open period, control currency, and expected total are required.', variant: 'destructive' });
            return;
        }
        try {
            setSaving(true);
            const created = await journalBatchDataService.createBatch({
                description: form.description.trim(),
                fiscalPeriodId: form.fiscalPeriodId,
                bookClassification: form.bookClassification,
                controlCurrencyCode: form.controlCurrencyCode.toUpperCase(),
                expectedDebitTotal: Number(form.expectedDebitTotal),
                expectedJournalCount: form.expectedJournalCount ? Number(form.expectedJournalCount) : undefined,
                notes: form.notes || undefined,
            });
            router.push(`/finance/journal-batches/${created.id}`);
        } catch (error: any) {
            toast({ title: 'Create failed', description: error.message, variant: 'destructive' });
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="mx-auto max-w-4xl space-y-6">
            <div className="flex items-center gap-3">
                <Button variant="ghost" size="icon" asChild><Link href="/finance/journal-batches" aria-label="Back to journal batches"><ArrowLeft className="h-4 w-4" /></Link></Button>
                <div><h1 className="text-3xl font-bold">New journal batch</h1><p className="text-muted-foreground">Define the independent control total before entering journals.</p></div>
            </div>
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/journal-batches">Journal Batches</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>New</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>
            <Card>
                <CardHeader><CardTitle>Batch controls</CardTitle><CardDescription>The expected debit total and optional journal count are checked again at submission.</CardDescription></CardHeader>
                <CardContent className="grid gap-5 md:grid-cols-2">
                    <div className="space-y-2 md:col-span-2"><Label htmlFor="batch-description">Description</Label><Input id="batch-description" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
                    <div className="space-y-2">
                        <Label htmlFor="batch-fiscal-period">Fiscal period</Label>
                        <Select value={form.fiscalPeriodId} onValueChange={(value) => setForm({ ...form, fiscalPeriodId: value })}>
                            <SelectTrigger id="batch-fiscal-period"><SelectValue placeholder="Select an open period" /></SelectTrigger>
                            <SelectContent>{openPeriods.map((period) => <SelectItem key={period.id} value={period.id}>{period.periodName}</SelectItem>)}</SelectContent>
                        </Select>
                    </div>
                    <div className="space-y-2"><Label htmlFor="batch-accounting-book">Accounting book</Label><Select value={form.bookClassification} onValueChange={(value) => setForm({ ...form, bookClassification: value })}><SelectTrigger id="batch-accounting-book"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="IFRS">IFRS</SelectItem><SelectItem value="LOCAL_STATUTORY">Local statutory</SelectItem><SelectItem value="MANAGEMENT">Management</SelectItem></SelectContent></Select></div>
                    <div className="space-y-2"><Label htmlFor="batch-expected-total">Expected debit total</Label><Input id="batch-expected-total" type="number" min="0.01" step="0.01" value={form.expectedDebitTotal} onChange={(e) => setForm({ ...form, expectedDebitTotal: e.target.value })} /></div>
                    <div className="space-y-2"><Label htmlFor="batch-expected-count">Expected journal count (optional)</Label><Input id="batch-expected-count" type="number" min="1" value={form.expectedJournalCount} onChange={(e) => setForm({ ...form, expectedJournalCount: e.target.value })} /></div>
                    <div className="space-y-2">
                        <Label htmlFor="batch-control-currency">Control currency</Label>
                        <Select value={form.controlCurrencyCode} onValueChange={(value) => setForm({ ...form, controlCurrencyCode: value })}>
                            <SelectTrigger id="batch-control-currency">
                                <SelectValue placeholder={currencies.length ? 'Select the base currency' : 'No active base currency configured'} />
                            </SelectTrigger>
                            <SelectContent>
                                {currencies.map((currency) => (
                                    <SelectItem key={currency.id} value={currency.currencyCode}>
                                        {currency.currencyCode} — {currency.currencyName}{currency.isBaseCurrency ? ' (base)' : ''}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                        <p className="text-xs text-muted-foreground">
                            Batch control totals use the tenant base currency; member journals retain any foreign-currency evidence.
                        </p>
                    </div>
                    <div className="space-y-2 md:col-span-2"><Label htmlFor="batch-notes">Notes</Label><Textarea id="batch-notes" value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></div>
                    <div className="flex justify-end md:col-span-2"><Button onClick={create} disabled={saving}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Create batch</Button></div>
                </CardContent>
            </Card>
        </div>
    );
}
