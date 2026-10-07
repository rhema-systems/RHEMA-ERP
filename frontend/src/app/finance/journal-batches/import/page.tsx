'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AlertCircle, CheckCircle2, Download, FileSpreadsheet, Loader2, Upload } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { formatJournalBatchMoney } from '@/lib/finance/journal-batch-money';
import { journalBatchDataService } from '@/services/finance/journal-batch-data.service';
import type { JournalBatchImportPreview } from '@/types/journal-batches';

export default function JournalBatchImportPage() {
    const router = useRouter();
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canImport = hasPermission('Finance.JournalBatches.Import');
    const [file, setFile] = useState<File | null>(null);
    const [preview, setPreview] = useState<JournalBatchImportPreview | null>(null);
    const [busy, setBusy] = useState<'preview' | 'commit' | null>(null);

    const previewFile = async () => {
        if (!file || !canImport) return;
        try {
            setBusy('preview');
            setPreview(await journalBatchDataService.previewImport(file));
        } catch (error: any) {
            toast({ title: 'Preview failed', description: error.message, variant: 'destructive' });
        } finally {
            setBusy(null);
        }
    };

    const commit = async () => {
        if (!preview?.isValid || !canImport) return;
        try {
            setBusy('commit');
            const batch = await journalBatchDataService.commitImport(preview.sessionId, preview.previewToken);
            router.push(`/finance/journal-batches/${batch.id}`);
        } catch (error: any) {
            toast({ title: 'Import failed', description: error.message, variant: 'destructive' });
        } finally {
            setBusy(null);
        }
    };

    return (
        <div className="mx-auto max-w-5xl space-y-6">
            <div>
                <h1 className="flex items-center gap-2 text-3xl font-bold"><FileSpreadsheet className="h-8 w-8" />Import journal batch</h1>
                <p className="text-muted-foreground">Preview is mandatory; invalid workbooks cannot be committed.</p>
            </div>
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/journal-batches">Journal Batches</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Import</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardHeader><CardTitle>Workbook</CardTitle><CardDescription>Use the protected .xlsx template so header and journal-line controls can be validated consistently.</CardDescription></CardHeader>
                <CardContent className="space-y-4">
                    <div className="flex flex-wrap gap-2">
                        {canImport && <Button variant="outline" onClick={() => journalBatchDataService.downloadTemplate()}><Download className="mr-2 h-4 w-4" />Download template</Button>}
                        <Label className="sr-only" htmlFor="journal-batch-workbook">Journal batch workbook</Label>
                        <Input id="journal-batch-workbook" className="max-w-xl" type="file" accept=".xlsx" onChange={(event) => { setFile(event.target.files?.[0] ?? null); setPreview(null); }} />
                        <Button onClick={previewFile} disabled={!file || busy !== null || !canImport}>{busy === 'preview' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}Preview</Button>
                    </div>

                    {preview && (
                        <>
                            <Alert variant={preview.isValid ? 'default' : 'destructive'}>
                                {preview.isValid ? <CheckCircle2 className="h-4 w-4" /> : <AlertCircle className="h-4 w-4" />}
                                <AlertTitle>{preview.isValid ? 'Workbook is ready to import' : 'Workbook requires correction'}</AlertTitle>
                                <AlertDescription>
                                    {preview.journalCount} journals, {preview.lineCount} lines. Expected {formatJournalBatchMoney(preview.expectedDebitTotal, preview.controlCurrencyCode)}; actual {formatJournalBatchMoney(preview.actualDebitTotal, preview.controlCurrencyCode)}.
                                </AlertDescription>
                            </Alert>

                            {preview.issues.length > 0 && (
                                <div className="overflow-x-auto rounded-md border">
                                    <table className="w-full text-sm">
                                        <thead className="bg-muted/50"><tr><th className="p-3 text-left">Location</th><th className="p-3 text-left">Code</th><th className="p-3 text-left">Issue</th></tr></thead>
                                        <tbody>{preview.issues.map((issue, index) => <tr className="border-t" key={`${issue.sheet}-${issue.row}-${issue.column}-${index}`}><td className="p-3">{issue.sheet}!{issue.column || ''}{issue.row}</td><td className="p-3 font-mono">{issue.code}</td><td className="p-3">{issue.message}</td></tr>)}</tbody>
                                    </table>
                                </div>
                            )}

                            <div className="flex justify-end gap-2">
                                {!preview.isValid && canImport && <Button variant="outline" onClick={() => journalBatchDataService.downloadImportErrors(preview.sessionId)}><Download className="mr-2 h-4 w-4" />Error workbook</Button>}
                                <Button onClick={commit} disabled={!preview.isValid || busy !== null || !canImport}>{busy === 'commit' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Commit import</Button>
                            </div>
                        </>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
