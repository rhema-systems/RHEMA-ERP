'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { Download, Eye, FileSpreadsheet, Layers3, Loader2, Plus, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { journalBatchDataService } from '@/services/finance/journal-batch-data.service';
import type { JournalBatchListItem } from '@/types/journal-batches';

const money = (value: number, currency: string) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(value);

export default function JournalBatchesPage() {
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canCreate = hasPermission('Finance.JournalBatches.Create');
    const canImport = hasPermission('Finance.JournalBatches.Import');
    const [items, setItems] = useState<JournalBatchListItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [approvalStatus, setApprovalStatus] = useState('all');

    const load = useCallback(async () => {
        try {
            setLoading(true);
            const result = await journalBatchDataService.getBatches({
                search: search || undefined,
                approvalStatus: approvalStatus === 'all' ? undefined : approvalStatus,
                pageSize: 200,
            });
            setItems(result.items);
        } catch (error: any) {
            toast({ title: 'Load failed', description: error.message, variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    }, [approvalStatus, search, toast]);

    useEffect(() => {
        const timer = window.setTimeout(load, 250);
        return () => window.clearTimeout(timer);
    }, [load]);

    const downloadTemplate = async () => {
        try {
            await journalBatchDataService.downloadTemplate();
        } catch (error: any) {
            toast({ title: 'Download failed', description: error.message, variant: 'destructive' });
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
                        <Layers3 className="h-8 w-8" /> Journal Batches
                    </h1>
                    <p className="text-muted-foreground">Control totals, configured approvals, partial posting, and full-batch reversal.</p>
                </div>
                <div className="flex flex-wrap gap-2">
                    {canImport && <Button variant="outline" onClick={downloadTemplate}><Download className="mr-2 h-4 w-4" />Template</Button>}
                    {canImport && <Button variant="outline" asChild><Link href="/finance/journal-batches/import"><FileSpreadsheet className="mr-2 h-4 w-4" />Import</Link></Button>}
                    {canCreate && <Button asChild><Link href="/finance/journal-batches/new"><Plus className="mr-2 h-4 w-4" />New Batch</Link></Button>}
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Journal Batches</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardHeader>
                    <CardTitle>Batch register</CardTitle>
                    <CardDescription>Search or filter by batch readiness.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="grid gap-3 md:grid-cols-[1fr_240px]">
                        <div className="relative">
                            <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                            <Label className="sr-only" htmlFor="journal-batch-search">Search journal batches</Label>
                            <Input id="journal-batch-search" className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Batch number or description" />
                        </div>
                        <div>
                            <Label className="sr-only" htmlFor="journal-batch-approval-status">Batch readiness</Label>
                            <Select value={approvalStatus} onValueChange={setApprovalStatus}>
                                <SelectTrigger id="journal-batch-approval-status"><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All readiness statuses</SelectItem>
                                    <SelectItem value="Draft">Draft</SelectItem>
                                    <SelectItem value="ReadyToPost">Ready to post</SelectItem>
                                    <SelectItem value="PendingApproval">Pending approval</SelectItem>
                                    <SelectItem value="PartiallyApproved">Partially approved</SelectItem>
                                    <SelectItem value="Approved">Approved</SelectItem>
                                    <SelectItem value="Rejected">Rejected</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>

                    {loading ? (
                        <div className="flex justify-center py-12"><Loader2 className="h-7 w-7 animate-spin" /></div>
                    ) : (
                        <div className="overflow-x-auto rounded-md border">
                            <table className="w-full text-sm">
                                <thead className="bg-muted/50">
                                    <tr>
                                        <th className="p-3 text-left">Batch</th>
                                        <th className="p-3 text-left">Period</th>
                                        <th className="p-3 text-right">Expected</th>
                                        <th className="p-3 text-right">Actual / variance</th>
                                        <th className="p-3 text-center">Entries</th>
                                        <th className="p-3 text-left">Status</th>
                                        <th className="p-3 text-right">Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {items.map((batch) => (
                                        <tr key={batch.id} className="border-t">
                                            <td className="p-3">
                                                <Link className="font-mono font-semibold text-primary hover:underline" href={`/finance/journal-batches/${batch.id}`}>{batch.batchNumber}</Link>
                                                <div className="max-w-sm truncate text-muted-foreground">{batch.description}</div>
                                            </td>
                                            <td className="p-3">
                                                {batch.fiscalPeriodName || '—'}
                                                <div className="text-xs text-muted-foreground">
                                                    {batch.bookClassification} — {batch.accountingBookName} ({batch.accountingBookType})
                                                </div>
                                            </td>
                                            <td className="p-3 text-right">{money(batch.expectedDebitTotal, batch.controlCurrencyCode)}</td>
                                            <td className="p-3 text-right">
                                                {money(batch.actualDebitTotal, batch.controlCurrencyCode)}
                                                <div className={batch.variance === 0 ? 'text-xs text-emerald-600' : 'text-xs text-destructive'}>
                                                    variance {money(batch.variance, batch.controlCurrencyCode)}
                                                </div>
                                            </td>
                                            <td className="p-3 text-center">{batch.postedEntryCount}/{batch.entryCount} posted</td>
                                            <td className="p-3"><Badge variant={batch.approvalStatus === 'Rejected' ? 'destructive' : 'outline'}>{batch.displayStatus}</Badge></td>
                                            <td className="p-3 text-right"><Button variant="ghost" size="sm" asChild><Link href={`/finance/journal-batches/${batch.id}`}><Eye className="mr-2 h-4 w-4" />Open</Link></Button></td>
                                        </tr>
                                    ))}
                                    {items.length === 0 && <tr><td className="p-8 text-center text-muted-foreground" colSpan={7}>No journal batches found.</td></tr>}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
