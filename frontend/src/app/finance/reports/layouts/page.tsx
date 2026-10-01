'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    AlertCircle,
    Archive,
    BookOpenCheck,
    CheckCircle2,
    ChevronRight,
    ClipboardCheck,
    Copy,
    Download,
    Eye,
    FileJson,
    FileSpreadsheet,
    History,
    Import,
    Loader2,
    RefreshCw,
    Save,
    ShieldCheck,
    Upload,
    XCircle,
    Trash2,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import {
    Breadcrumb,
    BreadcrumbItem,
    BreadcrumbLink,
    BreadcrumbList,
    BreadcrumbPage,
    BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { financialStatementLayoutDataService } from '@/services/finance/financial-statement-layout-data.service';
import { resolveFinancialStatementLayoutPermissions } from './permissions';
import { StatementLayoutDraftEditor } from '@/components/finance/statement-layout-draft-editor';
import type {
    AccountingBook,
    AccountClassification,
    FinancialStatementLayoutAuditEventDto,
    FinancialStatementLayoutDto,
    FinancialStatementLayoutExecutionDto,
    FinancialStatementLayoutImportDefinitionDto,
    FinancialStatementLayoutImportPreviewDto,
    FinancialStatementLayoutReadinessDto,
    FinancialStatementLayoutSummaryDto,
    FinancialStatementLayoutValidationIssueDto,
    FinancialStatementLayoutValidationResultDto,
    FinancialStatementLayoutVersionDto,
    FinancialStatementRowDto,
    FinancialStatementRowInputDto,
    FinancialStatementType,
    LegacyFinancialStatementLayoutMigrationRequestDto,
} from '@/types/finance';

const today = new Date().toISOString().slice(0, 10);
const yearStart = `${new Date().getFullYear()}-01-01`;

function errorMessage(error: unknown, fallback: string) {
    const validation = validationFromError(error);
    if (validation) return validation.issues.filter(issue => issue.severity === 'Error').map(issue => `${issue.rowCode ? `${issue.rowCode}: ` : ''}${issue.message}`).join(' ') || fallback;
    if (error instanceof Error && error.message) return error.message;
    return fallback;
}

function validationFromError(error: unknown): FinancialStatementLayoutValidationResultDto | null {
    if (!error || typeof error !== 'object' || !('response' in error)) return null;
    const response = error.response as Partial<FinancialStatementLayoutValidationResultDto> | undefined;
    return response && Array.isArray(response.issues) ? response as FinancialStatementLayoutValidationResultDto : null;
}

function formatDate(value?: string) {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat('en-GH', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
    }).format(date);
}

function formatDateTime(value?: string) {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat('en-GH', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    }).format(date);
}

function statementLabel(value: FinancialStatementType) {
    return value === 'BalanceSheet' ? 'Balance Sheet' : 'Income Statement';
}

function statusVariant(status: FinancialStatementLayoutVersionDto['status']) {
    if (status === 'Published') return 'default' as const;
    if (status === 'Draft' || status === 'Submitted') return 'secondary' as const;
    return 'outline' as const;
}

function mappingLabel(mapping: FinancialStatementRowDto['mappings'][number]) {
    if (mapping.mappingType === 'Classification') {
        return `${mapping.accountClassificationCode || 'Unknown classification'}${mapping.accountClassificationName ? ` — ${mapping.accountClassificationName}` : ''}${mapping.includeClassificationDescendants ? ' (including descendants)' : ''}`;
    }
    if (mapping.accountNumber) return `${mapping.accountNumber}${mapping.accountName ? ` — ${mapping.accountName}` : ''}`;
    return [mapping.fromAccountNumber, mapping.toAccountNumber].filter(Boolean).join(' → ') || 'Hierarchy node';
}

function toRowInput(row: FinancialStatementRowDto): FinancialStatementRowInputDto {
    return {
        rowCode: row.rowCode,
        parentRowCode: row.parentRowCode,
        label: row.label,
        rowType: row.rowType,
        displayOrder: row.displayOrder,
        formula: row.formula,
        signMultiplier: row.signMultiplier,
        isVisible: row.isVisible,
        suppressIfZero: row.suppressIfZero,
        showAccountDetails: row.showAccountDetails,
        isBold: row.isBold,
        isItalic: row.isItalic,
        isUnderlined: row.isUnderlined,
        indentLevel: row.indentLevel,
        mappings: row.mappings.map((mapping) => ({
            mappingType: mapping.mappingType,
            accountId: mapping.accountId,
            accountNumber: mapping.accountNumber,
            fromAccountNumber: mapping.fromAccountNumber,
            toAccountNumber: mapping.toAccountNumber,
            accountClassificationId: mapping.accountClassificationId,
            accountClassificationCode: mapping.accountClassificationCode,
            includeClassificationDescendants: mapping.includeClassificationDescendants,
        })),
    };
}

function issueIcon(issue: FinancialStatementLayoutValidationIssueDto) {
    if (issue.severity === 'Error') return <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />;
    if (issue.severity === 'Warning') return <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />;
    return <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-blue-600" />;
}

function ValidationPanel({
    validation,
    emptyMessage = 'No validation findings.',
}: {
    validation?: FinancialStatementLayoutValidationResultDto | null;
    emptyMessage?: string;
}) {
    if (!validation) return null;
    return (
        <div className="space-y-2 rounded-md border p-3">
            <div className="flex items-center justify-between">
                <div className="font-medium">Validation findings</div>
                <Badge variant={validation.isValid ? 'secondary' : 'destructive'}>
                    {validation.isValid ? 'Valid' : 'Blocked'}
                </Badge>
            </div>
            {validation.issues.length === 0 ? (
                <p className="text-sm text-muted-foreground">{emptyMessage}</p>
            ) : (
                <div className="max-h-48 space-y-2 overflow-y-auto">
                    {validation.issues.map((issue, index) => (
                        <div key={`${issue.code}-${issue.rowCode || index}`} className="flex gap-2 text-sm">
                            {issueIcon(issue)}
                            <div>
                                <span className="font-medium">{issue.code}</span>
                                {issue.rowCode ? <span className="text-muted-foreground"> · {issue.rowCode}</span> : null}
                                <div className="text-muted-foreground">{issue.message}</div>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

function ImportPreviewPanel({ preview }: { preview: FinancialStatementLayoutImportPreviewDto }) {
    return (
        <div className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Operation</div>
                    <div className="font-medium">{preview.willCreateLayout ? 'Create new layout' : 'Update draft'}</div>
                </div>
                <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Rows</div>
                    <div className="text-lg font-semibold">{preview.rowCount.toLocaleString()}</div>
                </div>
                <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Mappings</div>
                    <div className="text-lg font-semibold">{preview.mappingCount.toLocaleString()}</div>
                </div>
            </div>
            <div className="rounded-md bg-muted/40 p-3 text-sm">
                <div className="font-medium">{preview.definition.code} — {preview.definition.name}</div>
                <div className="text-muted-foreground">
                    {statementLabel(preview.definition.statementType)} · Definition hash {preview.definitionHash.slice(0, 12)}…
                </div>
            </div>
            <ValidationPanel validation={preview.validation} />
        </div>
    );
}

function ImportLayoutDialog({
    open,
    onOpenChange,
    onImported,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    onImported: (layoutId: string) => void;
}) {
    const { toast } = useToast();
    const [mode, setMode] = useState<'workbook' | 'json'>('workbook');
    const [file, setFile] = useState<File | null>(null);
    const [jsonText, setJsonText] = useState('');
    const [preview, setPreview] = useState<FinancialStatementLayoutImportPreviewDto | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (!open) {
            setFile(null);
            setJsonText('');
            setPreview(null);
            setMode('workbook');
        }
    }, [open]);

    const runPreview = async () => {
        try {
            setBusy(true);
            if (mode === 'workbook') {
                if (!file) throw new Error('Select a completed .xlsx layout workbook.');
                if (file.size > 5 * 1024 * 1024) throw new Error('The workbook must not exceed 5 MB.');
                setPreview(await financialStatementLayoutDataService.previewWorkbook(file));
            } else {
                if (!jsonText.trim()) throw new Error('Paste a JSON layout definition.');
                const definition = JSON.parse(jsonText) as FinancialStatementLayoutImportDefinitionDto;
                setPreview(await financialStatementLayoutDataService.previewJson(definition));
            }
        } catch (error) {
            setPreview(null);
            toast({
                title: 'Preview failed',
                description: errorMessage(error, 'The layout definition could not be previewed.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    const commit = async () => {
        if (!preview || !preview.validation.isValid) return;
        try {
            setBusy(true);
            let result;
            if (mode === 'workbook') {
                if (!file) throw new Error('Select the same workbook that was previewed.');
                result = await financialStatementLayoutDataService.commitWorkbook(file, preview.definitionHash);
            } else {
                result = await financialStatementLayoutDataService.commitJson(preview);
            }
            toast({
                title: 'Draft imported',
                description: `${result.layout.code} version ${result.draftVersionNumber} is ready for finance review.`,
            });
            onOpenChange(false);
            onImported(result.layoutId);
        } catch (error) {
            toast({
                title: 'Import failed',
                description: errorMessage(error, 'The validated definition could not be committed. Preview it again.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="grid max-h-[92vh] max-w-4xl grid-rows-[auto_minmax(0,1fr)_auto] overflow-hidden p-0">
                <DialogHeader className="px-6 pt-6">
                    <DialogTitle>Import a financial statement layout</DialogTitle>
                    <DialogDescription>
                        Imports create or replace Draft content only. Preview and validation are mandatory.
                    </DialogDescription>
                </DialogHeader>
                <ScrollArea className="px-6">
                    <Tabs value={mode} onValueChange={(value) => {
                        setMode(value as 'workbook' | 'json');
                        setPreview(null);
                    }}>
                        <TabsList className="grid w-full grid-cols-2">
                            <TabsTrigger value="workbook"><FileSpreadsheet className="mr-2 h-4 w-4" />Excel workbook</TabsTrigger>
                            <TabsTrigger value="json"><FileJson className="mr-2 h-4 w-4" />JSON definition</TabsTrigger>
                        </TabsList>
                        <TabsContent value="workbook" className="space-y-4 py-4">
                            <Alert>
                                <ShieldCheck className="h-4 w-4" />
                                <AlertTitle>Controlled workbook</AlertTitle>
                                <AlertDescription>
                                    Use the downloaded template. Macros, formulas and external links are rejected.
                                </AlertDescription>
                            </Alert>
                            <div className="space-y-2">
                                <Label htmlFor="layout-workbook">Completed template</Label>
                                <Input
                                    id="layout-workbook"
                                    type="file"
                                    accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                                    onChange={(event) => {
                                        setFile(event.target.files?.[0] || null);
                                        setPreview(null);
                                    }}
                                />
                                {file ? <p className="text-xs text-muted-foreground">{file.name} · {(file.size / 1024).toFixed(1)} KB</p> : null}
                            </div>
                        </TabsContent>
                        <TabsContent value="json" className="space-y-4 py-4">
                            <Alert>
                                <FileJson className="h-4 w-4" />
                                <AlertTitle>API-compatible JSON</AlertTitle>
                                <AlertDescription>
                                    Paste a version 1 import definition. The server normalises it before returning the commit hash.
                                </AlertDescription>
                            </Alert>
                            <div className="space-y-2">
                                <Label htmlFor="layout-json">Layout definition</Label>
                                <Textarea
                                    id="layout-json"
                                    className="min-h-64 font-mono text-xs"
                                    value={jsonText}
                                    onChange={(event) => {
                                        setJsonText(event.target.value);
                                        setPreview(null);
                                    }}
                                    placeholder={'{\n  "templateVersion": "1",\n  "code": "BS-STATUTORY",\n  ...\n}'}
                                />
                            </div>
                        </TabsContent>
                    </Tabs>
                    {preview ? <div className="pb-5"><ImportPreviewPanel preview={preview} /></div> : null}
                </ScrollArea>
                <DialogFooter className="border-t px-6 py-4">
                    <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                    <Button variant="secondary" onClick={runPreview} disabled={busy}>
                        {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ClipboardCheck className="mr-2 h-4 w-4" />}
                        Preview and validate
                    </Button>
                    <Button onClick={commit} disabled={busy || !preview?.validation.isValid}>
                        {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
                        Commit Draft
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function LegacyMigrationDialog({
    open,
    onOpenChange,
    books,
    onImported,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    books: AccountingBook[];
    onImported: (layoutId: string) => void;
}) {
    const { toast } = useToast();
    const [request, setRequest] = useState<LegacyFinancialStatementLayoutMigrationRequestDto>({
        code: '',
        name: '',
        statementType: 'BalanceSheet',
        accountingBookId: '',
        isDefault: false,
        notes: 'Generated from legacy financial-statement line-item mappings for finance review.',
    });
    const [preview, setPreview] = useState<FinancialStatementLayoutImportPreviewDto | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (open) {
            setRequest((current) => ({
                ...current,
                accountingBookId: current.accountingBookId || books.find((book) => book.isDefault)?.id || books[0]?.id || '',
            }));
        } else {
            setPreview(null);
        }
    }, [books, open]);

    const update = <K extends keyof LegacyFinancialStatementLayoutMigrationRequestDto>(
        key: K,
        value: LegacyFinancialStatementLayoutMigrationRequestDto[K],
    ) => {
        setRequest((current) => ({ ...current, [key]: value }));
        setPreview(null);
    };

    const runPreview = async () => {
        if (!request.code.trim() || !request.name.trim() || !request.accountingBookId) {
            toast({ title: 'Required fields missing', description: 'Enter a code, name and accounting book.', variant: 'destructive' });
            return;
        }
        try {
            setBusy(true);
            setPreview(await financialStatementLayoutDataService.previewLegacyMigration({
                ...request,
                code: request.code.trim().toUpperCase(),
                name: request.name.trim(),
            }));
        } catch (error) {
            setPreview(null);
            toast({
                title: 'Migration preview failed',
                description: errorMessage(error, 'Legacy mappings could not be analysed.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    const commit = async () => {
        if (!preview?.validation.isValid) return;
        try {
            setBusy(true);
            const result = await financialStatementLayoutDataService.commitLegacyMigration(
                {
                    ...request,
                    code: request.code.trim().toUpperCase(),
                    name: request.name.trim(),
                },
                preview.definitionHash,
            );
            toast({
                title: 'Migration Draft created',
                description: `${result.layout.code} contains ${preview.rowCount} reviewable rows.`,
            });
            onOpenChange(false);
            onImported(result.layoutId);
        } catch (error) {
            toast({
                title: 'Migration failed',
                description: errorMessage(error, 'The migration Draft could not be created. Preview it again.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="grid max-h-[92vh] max-w-3xl grid-rows-[auto_minmax(0,1fr)_auto] overflow-hidden p-0">
                <DialogHeader className="px-6 pt-6">
                    <DialogTitle>Generate Draft from legacy mappings</DialogTitle>
                    <DialogDescription>
                        Existing account line-item labels will be grouped alphabetically because they have no stored report sequence.
                    </DialogDescription>
                </DialogHeader>
                <ScrollArea className="px-6">
                    <div className="grid gap-4 py-4 sm:grid-cols-2">
                        <div className="space-y-2">
                            <Label htmlFor="legacy-code">Layout code</Label>
                            <Input id="legacy-code" value={request.code} onChange={(event) => update('code', event.target.value.toUpperCase())} placeholder="BS-LEGACY-IFRS" />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="legacy-name">Layout name</Label>
                            <Input id="legacy-name" value={request.name} onChange={(event) => update('name', event.target.value)} placeholder="IFRS Balance Sheet" />
                        </div>
                        <div className="space-y-2">
                            <Label>Statement</Label>
                            <Select value={request.statementType} onValueChange={(value) => update('statementType', value as FinancialStatementType)}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="BalanceSheet">Balance Sheet</SelectItem>
                                    <SelectItem value="IncomeStatement">Income Statement</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Accounting book</Label>
                            <Select value={request.accountingBookId} onValueChange={(value) => update('accountingBookId', value)}>
                                <SelectTrigger><SelectValue placeholder="Select a book" /></SelectTrigger>
                                <SelectContent>
                                    {books.map((book) => <SelectItem key={book.id} value={book.id}>{book.code} — {book.name}</SelectItem>)}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="legacy-from">Effective from</Label>
                            <Input id="legacy-from" type="date" value={request.effectiveFrom || ''} onChange={(event) => update('effectiveFrom', event.target.value || undefined)} />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="legacy-to">Effective to</Label>
                            <Input id="legacy-to" type="date" value={request.effectiveTo || ''} onChange={(event) => update('effectiveTo', event.target.value || undefined)} />
                        </div>
                        <div className="space-y-2 sm:col-span-2">
                            <Label htmlFor="legacy-description">Description</Label>
                            <Textarea id="legacy-description" value={request.description || ''} onChange={(event) => update('description', event.target.value || undefined)} />
                        </div>
                        <div className="space-y-2 sm:col-span-2">
                            <Label htmlFor="legacy-notes">Review notes</Label>
                            <Textarea id="legacy-notes" value={request.notes || ''} onChange={(event) => update('notes', event.target.value || undefined)} />
                        </div>
                    </div>
                    {preview ? <div className="pb-5"><ImportPreviewPanel preview={preview} /></div> : null}
                </ScrollArea>
                <DialogFooter className="border-t px-6 py-4">
                    <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                    <Button variant="secondary" onClick={runPreview} disabled={busy}>
                        {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ClipboardCheck className="mr-2 h-4 w-4" />}
                        Preview migration
                    </Button>
                    <Button onClick={commit} disabled={busy || !preview?.validation.isValid}>
                        {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Import className="mr-2 h-4 w-4" />}
                        Create Draft
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function ExecutionPreviewDialog({
    open,
    onOpenChange,
    version,
    statementType,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    version: FinancialStatementLayoutVersionDto | null;
    statementType: FinancialStatementType;
}) {
    const { toast } = useToast();
    const [periodStart, setPeriodStart] = useState(yearStart);
    const [periodEnd, setPeriodEnd] = useState(today);
    const [result, setResult] = useState<FinancialStatementLayoutExecutionDto | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (!open) setResult(null);
    }, [open]);

    const run = async () => {
        if (!version) return;
        try {
            setBusy(true);
            setResult(await financialStatementLayoutDataService.previewVersion(version.id, {
                periodStart: statementType === 'IncomeStatement' ? periodStart : undefined,
                periodEnd,
                includeAccountDetails: true,
                includeHiddenRows: true,
                accountIds: [],
                segmentFilters: [],
            }));
        } catch (error) {
            toast({
                title: 'Execution preview failed',
                description: errorMessage(error, 'The selected version could not be executed.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="grid max-h-[92vh] max-w-5xl grid-rows-[auto_minmax(0,1fr)_auto] overflow-hidden p-0">
                <DialogHeader className="px-6 pt-6">
                    <DialogTitle>Version {version?.versionNumber} execution preview</DialogTitle>
                    <DialogDescription>Run the Draft against posted GL balances and review reconciliation coverage.</DialogDescription>
                </DialogHeader>
                <ScrollArea className="px-6">
                    <div className="space-y-5 py-4">
                        <div className="grid gap-3 sm:grid-cols-3">
                            {statementType === 'IncomeStatement' ? (
                                <div className="space-y-2">
                                    <Label htmlFor="preview-start">Period start</Label>
                                    <Input id="preview-start" type="date" value={periodStart} onChange={(event) => setPeriodStart(event.target.value)} />
                                </div>
                            ) : <div />}
                            <div className="space-y-2">
                                <Label htmlFor="preview-end">{statementType === 'BalanceSheet' ? 'As at date' : 'Period end'}</Label>
                                <Input id="preview-end" type="date" value={periodEnd} onChange={(event) => setPeriodEnd(event.target.value)} />
                            </div>
                            <div className="flex items-end">
                                <Button className="w-full" onClick={run} disabled={busy || !periodEnd}>
                                    {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Eye className="mr-2 h-4 w-4" />}
                                    Run preview
                                </Button>
                            </div>
                        </div>
                        {result ? (
                            <>
                                <div className="grid gap-3 sm:grid-cols-4">
                                    <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Coverage</div><div className="text-xl font-semibold">{result.reconciliation.accountCoveragePercent.toFixed(1)}%</div></div>
                                    <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Mapped accounts</div><div className="text-xl font-semibold">{result.reconciliation.mappedAccountCount}</div></div>
                                    <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Unmapped accounts</div><div className="text-xl font-semibold">{result.reconciliation.unmappedAccountCount}</div></div>
                                    <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Unmapped non-zero</div><div className="text-xl font-semibold">{result.reconciliation.unmappedNonZeroAccountCount}</div></div>
                                </div>
                                {result.reconciliation.unmappedNonZeroAccountCount > 0 ? (
                                    <Alert variant="destructive">
                                        <AlertCircle className="h-4 w-4" />
                                        <AlertTitle>Reconciliation requires attention</AlertTitle>
                                        <AlertDescription>
                                            {result.reconciliation.unmappedNonZeroAccountCount} non-zero accounts are not represented in this layout.
                                        </AlertDescription>
                                    </Alert>
                                ) : null}
                                <div className="overflow-x-auto rounded-md border">
                                    <table className="w-full text-sm">
                                        <thead className="bg-muted/50">
                                            <tr><th className="p-2 text-left">Row</th><th className="p-2 text-left">Label</th><th className="p-2 text-right">Amount</th></tr>
                                        </thead>
                                        <tbody>
                                            {result.rows.filter((row) => row.isDisplayed).map((row) => (
                                                <tr key={row.rowId} className="border-t">
                                                    <td className="p-2 font-mono text-xs">{row.rowCode}</td>
                                                    <td className="p-2" style={{ paddingLeft: `${0.5 + row.indentLevel * 1.25}rem` }}>
                                                        <span className={row.isBold ? 'font-semibold' : ''}>{row.label}</span>
                                                    </td>
                                                    <td className="p-2 text-right font-mono">{row.amount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            </>
                        ) : null}
                    </div>
                </ScrollArea>
                <DialogFooter className="border-t px-6 py-4">
                    <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function LayoutDetailDialog({
    layoutId,
    open,
    onOpenChange,
    canManage,
    canRun,
    onChanged,
}: {
    layoutId: string | null;
    open: boolean;
    onOpenChange: (open: boolean) => void;
    canManage: boolean;
    canRun: boolean;
    onChanged: () => void;
}) {
    const { toast } = useToast();
    const [layout, setLayout] = useState<FinancialStatementLayoutDto | null>(null);
    const [audit, setAudit] = useState<FinancialStatementLayoutAuditEventDto[]>([]);
    const [classifications, setClassifications] = useState<AccountClassification[]>([]);
    const [classificationSelections, setClassificationSelections] = useState<Record<string, string>>({});
    const [designing, setDesigning] = useState(false);
    const [selectedVersionId, setSelectedVersionId] = useState('');
    const [validation, setValidation] = useState<FinancialStatementLayoutValidationResultDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [busy, setBusy] = useState(false);
    const [previewOpen, setPreviewOpen] = useState(false);
    const [discardOpen, setDiscardOpen] = useState(false);
    const [discardReason, setDiscardReason] = useState('');
    const [confirmation, setConfirmation] = useState<{
        title: string;
        description: string;
        action: () => Promise<void>;
    } | null>(null);
    const [edit, setEdit] = useState({ name: '', description: '', isDefault: false, isActive: true });
    const [clone, setClone] = useState({ code: '', name: '' });

    const load = useCallback(async () => {
        if (!layoutId) return;
        try {
            setLoading(true);
            const detail = await financialStatementLayoutDataService.getLayout(layoutId);
            const [auditEvents, availableClassifications] = await Promise.all([
                financialStatementLayoutDataService.getAuditTrail(layoutId).catch(() => []),
                financeDataService.getAccountClassifications(detail.accountingBookId),
            ]);
            setLayout(detail);
            setAudit(auditEvents);
            setClassifications(availableClassifications.filter((item) => item.status === 'Active'));
            setClone({ code: `${detail.code}_COPY`.slice(0, 50), name: `${detail.name} Copy` });
            setEdit({
                name: detail.name,
                description: detail.description || '',
                isDefault: detail.isDefault,
                isActive: detail.isActive,
            });
            const draft = detail.versions.find((version) => version.status === 'Draft');
            const latest = [...detail.versions].sort((a, b) => b.versionNumber - a.versionNumber)[0];
            setSelectedVersionId((current) =>
                detail.versions.some((version) => version.id === current)
                    ? current
                    : draft?.id || latest?.id || '',
            );
        } catch (error) {
            toast({
                title: 'Layout unavailable',
                description: errorMessage(error, 'The layout could not be loaded.'),
                variant: 'destructive',
            });
            onOpenChange(false);
        } finally {
            setLoading(false);
        }
    }, [layoutId, onOpenChange, toast]);

    useEffect(() => {
        if (open) void load();
        if (!open) {
            setLayout(null);
            setAudit([]);
            setClassifications([]);
            setValidation(null);
        }
    }, [load, open]);

    const selectedVersion = useMemo(
        () => layout?.versions.find((version) => version.id === selectedVersionId) || null,
        [layout, selectedVersionId],
    );

    useEffect(() => { setValidation(null); setDesigning(false); }, [selectedVersionId]);

    const runAction = async (action: () => Promise<void>, success: string) => {
        try {
            setBusy(true);
            await action();
            toast({ title: success });
            await load();
            onChanged();
            return true;
        } catch (error) {
            setValidation(validationFromError(error));
            toast({
                title: 'Action failed',
                description: errorMessage(error, 'The layout changed or the action was rejected. Refresh and retry.'),
                variant: 'destructive',
            });
            return false;
        } finally {
            setBusy(false);
        }
    };

    const saveMetadata = async () => {
        if (!layout || !edit.name.trim()) return;
        await runAction(async () => {
            const updated = await financialStatementLayoutDataService.updateLayout(layout.id, {
                name: edit.name.trim(),
                description: edit.description.trim() || undefined,
                isDefault: edit.isDefault,
                isActive: edit.isActive,
                expectedRevision: layout.revision,
            });
            setLayout(updated);
        }, 'Layout settings saved');
    };

    const discardUnusedDraft = async () => {
        if (!layout || discardReason.trim().length < 5) return;
        try {
            setBusy(true);
            await financialStatementLayoutDataService.discardUnusedDraft(
                layout.id,
                layout.revision,
                discardReason.trim(),
            );
            toast({ title: 'Unused Draft layout discarded' });
            setDiscardOpen(false);
            setDiscardReason('');
            onOpenChange(false);
            onChanged();
        } catch (error) {
            toast({
                title: 'Layout was not discarded',
                description: errorMessage(error, 'The layout is no longer eligible. Refresh and review its lifecycle.'),
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    const requestMetadataSave = () => {
        if (!layout) return;
        if ((!layout.isDefault && edit.isDefault) || (layout.isActive && !edit.isActive)) {
            setConfirmation({
                title: !layout.isDefault && edit.isDefault ? 'Make this the default layout?' : 'Deactivate this layout?',
                description: !layout.isDefault && edit.isDefault
                    ? 'It will replace the current default for this statement and accounting book. Existing published versions remain in the audit trail.'
                    : 'Inactive layouts cannot receive new Draft versions and will be excluded from normal report selection.',
                action: saveMetadata,
            });
            return;
        }
        void saveMetadata();
    };

    const createDraft = async () => {
        if (!layout) return;
        if (layout.versions.some((version) => version.status === 'Draft' || version.status === 'Submitted')) {
            toast({ title: 'Version already in progress', description: 'Complete or reject the existing Draft/Submitted version before creating another.', variant: 'destructive' });
            return;
        }
        const source = [...layout.versions].sort((a, b) => b.versionNumber - a.versionNumber)[0];
        await runAction(
            () => financialStatementLayoutDataService.createDraftVersion(layout.id, {
                sourceVersionId: source?.id,
                notes: source ? `Created from version ${source.versionNumber} for controlled review.` : 'New controlled Draft.',
            }).then(() => undefined),
            'New Draft version created',
        );
    };

    const validate = async () => {
        if (!selectedVersion) return;
        try {
            setBusy(true);
            setValidation(await financialStatementLayoutDataService.validateVersion(selectedVersion.id));
        } catch (error) {
            toast({ title: 'Validation failed', description: errorMessage(error, 'The version could not be validated.'), variant: 'destructive' });
        } finally {
            setBusy(false);
        }
    };

    const requestSubmission = () => {
        if (!selectedVersion || selectedVersion.status !== 'Draft') return;
        setConfirmation({
            title: `Submit version ${selectedVersion.versionNumber} for approval?`,
            description: 'Submission freezes this Draft and sends it to the Finance Approval Workbench. A different authorised Finance user must approve it before it becomes available to reports.',
            action: async () => {
                await runAction(
                    () => financialStatementLayoutDataService.submitVersion(
                        selectedVersion.id,
                        selectedVersion.revision,
                    ).then(() => undefined),
                    `Version ${selectedVersion.versionNumber} submitted for independent approval`,
                );
            },
        });
    };

    const exportVersion = async (format: 'json' | 'xlsx') => {
        if (!layout || !selectedVersion) return;
        try {
            setBusy(true);
            const baseName = `${layout.code}-v${selectedVersion.versionNumber}`;
            if (format === 'json') await financialStatementLayoutDataService.downloadExportJson(selectedVersion.id, `${baseName}.json`);
            else await financialStatementLayoutDataService.downloadExportWorkbook(selectedVersion.id, `${baseName}.xlsx`);
        } catch (error) {
            toast({ title: 'Export failed', description: errorMessage(error, 'The version could not be exported.'), variant: 'destructive' });
        } finally {
            setBusy(false);
        }
    };

    const cloneStandard = async () => {
        if (!layout || !clone.code.trim() || !clone.name.trim()) return;
        await runAction(async () => {
            const created = await financialStatementLayoutDataService.cloneLayout(layout.id, {
                code: clone.code.trim(),
                name: clone.name.trim(),
                accountingBookId: layout.accountingBookId,
            });
            onOpenChange(false);
            onChanged();
            toast({ title: 'Editable draft cloned', description: `${created.code} is ready for controlled editing.` });
        }, 'Protected standard cloned');
    };

    const replaceRows = async (rows: FinancialStatementRowInputDto[]) => {
        if (!selectedVersion || selectedVersion.status !== 'Draft') return;
        await runAction(
            () => financialStatementLayoutDataService.replaceDraftRows(selectedVersion.id, selectedVersion.revision, rows).then(() => undefined),
            'Draft mappings updated',
        );
    };

    const addClassificationMapping = async (rowId: string) => {
        if (!selectedVersion) return;
        const classificationId = classificationSelections[rowId];
        const classification = classifications.find((item) => item.id === classificationId);
        if (!classification) return;
        const rows = selectedVersion.rows.map(toRowInput);
        const row = rows.find((item) => selectedVersion.rows.find((source) => source.id === rowId)?.rowCode === item.rowCode);
        if (!row) return;
        if (row.mappings.some((mapping) => mapping.mappingType === 'Classification' && mapping.accountClassificationId === classification.id)) {
            toast({ title: 'Mapping already exists', description: `${classification.code} is already mapped to this row.`, variant: 'destructive' });
            return;
        }
        row.mappings.push({
            mappingType: 'Classification',
            accountClassificationId: classification.id,
            accountClassificationCode: classification.code,
            includeClassificationDescendants: true,
        });
        await replaceRows(rows);
    };

    const removeMapping = async (rowId: string, mappingId: string) => {
        if (!selectedVersion) return;
        const sourceRow = selectedVersion.rows.find((row) => row.id === rowId);
        if (sourceRow?.rowType === 'Account' && sourceRow.mappings.length === 1) {
            toast({
                title: 'Replacement mapping required',
                description: 'An account row must retain at least one mapping. Add the replacement mapping first, then remove the old mapping.',
                variant: 'destructive',
            });
            return;
        }
        const rows = selectedVersion.rows.map((source) => ({
            ...toRowInput(source),
            mappings: source.id === rowId
                ? source.mappings.filter((mapping) => mapping.id !== mappingId).map((mapping) => toRowInput({ ...source, mappings: [mapping] }).mappings[0])
                : toRowInput(source).mappings,
        }));
        await replaceRows(rows);
    };

    return (
        <>
            <Dialog open={open} onOpenChange={value => {
                if (!value && designing) {
                    setConfirmation({ title: 'Discard unsaved design changes?', description: 'The saved Draft is unchanged. Close only if you want to discard your local row edits.', action: async () => { setDesigning(false); onOpenChange(false); } });
                } else onOpenChange(value);
            }}>
                <DialogContent className="grid h-[94vh] max-w-6xl grid-rows-[auto_minmax(0,1fr)] overflow-hidden p-0">
                    <DialogHeader className="border-b px-6 py-5">
                        <DialogTitle className="flex flex-wrap items-center gap-2">
                            {layout?.code || 'Financial statement layout'}
                            {layout?.isDefault ? <Badge>Default</Badge> : null}
                            {layout?.isProtectedStandard ? <Badge variant="secondary">Protected standard · clone only</Badge> : null}
                            {layout && !layout.isActive ? <Badge variant="outline">Inactive</Badge> : null}
                        </DialogTitle>
                        <DialogDescription>
                            {layout ? `${statementLabel(layout.statementType)} · ${layout.accountingBookCode} — ${layout.accountingBookName}` : 'Loading layout details…'}
                        </DialogDescription>
                    </DialogHeader>
                    {loading || !layout ? (
                        <div className="flex items-center justify-center"><Loader2 className="h-8 w-8 animate-spin" /></div>
                    ) : (
                        <Tabs defaultValue="versions" className="grid min-h-0 grid-rows-[auto_minmax(0,1fr)]">
                            <div className="border-b px-6">
                                <TabsList className="h-12 bg-transparent">
                                    <TabsTrigger value="versions">Versions and rows</TabsTrigger>
                                    <TabsTrigger value="settings" disabled={designing}>Layout settings</TabsTrigger>
                                    <TabsTrigger value="audit" disabled={designing}>Audit trail</TabsTrigger>
                                </TabsList>
                            </div>
                            <TabsContent value="versions" className="m-0 min-h-0">
                                <ScrollArea className="h-full px-6">
                                    <div className="space-y-5 py-5">
                                        <div className="flex flex-wrap items-end justify-between gap-3">
                                            <div className="min-w-64 space-y-2">
                                                <Label>Version under review</Label>
                                                <Select value={selectedVersionId} onValueChange={setSelectedVersionId} disabled={designing || busy}>
                                                    <SelectTrigger><SelectValue /></SelectTrigger>
                                                    <SelectContent>
                                                        {[...layout.versions].sort((a, b) => b.versionNumber - a.versionNumber).map((version) => (
                                                            <SelectItem key={version.id} value={version.id}>
                                                                Version {version.versionNumber} — {version.status}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                            </div>
                                            <fieldset disabled={designing} className="flex flex-wrap gap-2">
                                                {canManage && !layout.isProtectedStandard ? <Button variant="outline" onClick={createDraft} disabled={busy || !layout.isActive || layout.versions.some((version) => version.status === 'Draft' || version.status === 'Submitted')}><Archive className="mr-2 h-4 w-4" />Create next Draft</Button> : null}
                                                {canManage && !layout.isProtectedStandard ? <Button variant="outline" onClick={validate} disabled={busy || !selectedVersion}><ClipboardCheck className="mr-2 h-4 w-4" />Validate</Button> : null}
                                                {canRun && !layout.isProtectedStandard ? <Button variant="outline" onClick={() => setPreviewOpen(true)} disabled={!selectedVersion}><Eye className="mr-2 h-4 w-4" />Run preview</Button> : null}
                                                {selectedVersion ? <Button variant="outline" onClick={() => void exportVersion('json')} disabled={busy}><FileJson className="mr-2 h-4 w-4" />JSON</Button> : null}
                                                {selectedVersion ? <Button variant="outline" onClick={() => void exportVersion('xlsx')} disabled={busy}><FileSpreadsheet className="mr-2 h-4 w-4" />Excel</Button> : null}
                                                {canManage && !layout.isProtectedStandard && selectedVersion?.status === 'Draft' ? <Button onClick={requestSubmission} disabled={busy}><ShieldCheck className="mr-2 h-4 w-4" />Submit for approval</Button> : null}
                                            </fieldset>
                                        </div>

                                        {selectedVersion ? (
                                            <div className="grid gap-3 sm:grid-cols-4">
                                                <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Status</div><Badge className="mt-1" variant={statusVariant(selectedVersion.status)}>{selectedVersion.status}</Badge></div>
                                                <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Effective from</div><div className="font-medium">{formatDate(selectedVersion.effectiveFrom)}</div></div>
                                                <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Effective to</div><div className="font-medium">{formatDate(selectedVersion.effectiveTo)}</div></div>
                                                <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">Rows / mappings</div><div className="font-medium">{selectedVersion.rows.length} / {selectedVersion.rows.reduce((sum, row) => sum + row.mappings.length, 0)}</div></div>
                                            </div>
                                        ) : null}

                                        {selectedVersion?.status !== 'Draft' && selectedVersion?.resolutionFingerprint ? (
                                            <Alert>
                                                <ShieldCheck className="h-4 w-4" />
                                                <AlertTitle>Immutable publication snapshot</AlertTitle>
                                                <AlertDescription>
                                                    {selectedVersion.publicationAccountCount.toLocaleString()} frozen account memberships · book {selectedVersion.publishedAccountingBookCode} · resolution {selectedVersion.resolutionFingerprint.slice(0, 12)}… · hierarchy {selectedVersion.hierarchyFingerprint?.slice(0, 12)}…
                                                </AlertDescription>
                                            </Alert>
                                        ) : null}

                                        {selectedVersion?.status === 'Submitted' ? (
                                            <Alert>
                                                <ClipboardCheck className="h-4 w-4" />
                                                <AlertTitle>Awaiting independent Finance approval</AlertTitle>
                                                <AlertDescription>
                                                    Submitted by {selectedVersion.submittedByName || 'an authorised preparer'} on {formatDate(selectedVersion.submittedAt)}. The maker cannot approve or reject this version; an eligible checker must decide it in the Finance Approval Workbench.
                                                </AlertDescription>
                                            </Alert>
                                        ) : null}

                                        {layout.isProtectedStandard && canManage ? (
                                            <Card>
                                                <CardHeader><CardTitle className="text-base">Clone into an editable tenant draft</CardTitle><CardDescription>The protected standard remains unchanged.</CardDescription></CardHeader>
                                                <CardContent className="grid gap-3 md:grid-cols-[1fr_2fr_auto]">
                                                    <Input aria-label="Clone layout code" value={clone.code} maxLength={50} onChange={(event) => setClone((current) => ({ ...current, code: event.target.value }))} />
                                                    <Input aria-label="Clone layout name" value={clone.name} maxLength={150} onChange={(event) => setClone((current) => ({ ...current, name: event.target.value }))} />
                                                    <Button onClick={() => void cloneStandard()} disabled={busy || !clone.code.trim() || !clone.name.trim()}><Copy className="mr-2 h-4 w-4" />Clone</Button>
                                                </CardContent>
                                            </Card>
                                        ) : null}

                                        <ValidationPanel validation={validation} />

                                        {selectedVersion ? (
                                            <Card>
                                                <CardHeader>
                                                    <CardTitle className="text-base">Sequenced row definition</CardTitle>
                                                    <CardDescription>Design Draft rows here, or use a controlled import. Submitted and published versions remain read-only.</CardDescription>
                                                    {canManage && !layout.isProtectedStandard && selectedVersion.status === 'Draft' && !designing && <Button variant="outline" disabled={busy} onClick={() => setDesigning(true)}>Design draft rows</Button>}
                                                </CardHeader>
                                                <CardContent className="p-0">
                                                    {designing && <StatementLayoutDraftEditor key={selectedVersion.id} initialRows={selectedVersion.rows.map(toRowInput)} classifications={classifications} busy={busy} onCancel={() => setDesigning(false)} onSave={async rows => {
                                                        const saved = await runAction(() => financialStatementLayoutDataService.replaceDraftRows(selectedVersion.id, selectedVersion.revision, rows).then(() => undefined), 'Draft design saved');
                                                        if (saved) { setDesigning(false); setValidation(null); }
                                                        return saved;
                                                    }} />}
                                                    <div className={designing ? 'hidden' : 'overflow-x-auto'}>
                                                        <table className="w-full text-sm">
                                                            <thead className="bg-muted/50">
                                                                <tr>
                                                                    <th className="p-3 text-left">Order</th>
                                                                    <th className="p-3 text-left">Code and label</th>
                                                                    <th className="p-3 text-left">Type</th>
                                                                    <th className="p-3 text-left">Mapping / formula</th>
                                                                    <th className="p-3 text-center">Display</th>
                                                                </tr>
                                                            </thead>
                                                            <tbody>
                                                                {[...selectedVersion.rows].sort((a, b) => a.displayOrder - b.displayOrder).map((row) => (
                                                                    <tr key={row.id} className="border-t align-top">
                                                                        <td className="p-3 font-mono">{row.displayOrder}</td>
                                                                        <td className="p-3">
                                                                            <div style={{ paddingLeft: `${row.indentLevel * 1.1}rem` }} className={row.isBold ? 'font-semibold' : ''}>{row.label}</div>
                                                                            <div className="font-mono text-xs text-muted-foreground">{row.rowCode}{row.parentRowCode ? ` ← ${row.parentRowCode}` : ''}</div>
                                                                        </td>
                                                                        <td className="p-3"><Badge variant="outline">{row.rowType}</Badge></td>
                                                                        <td className="max-w-md p-3">
                                                                            {row.formula ? <code className="rounded bg-muted px-1 py-0.5 text-xs">{row.formula}</code> : null}
                                                                            {row.mappings.map((mapping) => (
                                                                                <div key={mapping.id} className="flex items-center gap-1 text-xs text-muted-foreground">
                                                                                    <span>{mapping.mappingType}: {mappingLabel(mapping)}</span>
                                                                                    {canManage && !layout.isProtectedStandard && selectedVersion.status === 'Draft' ? (
                                                                                        <Button variant="ghost" size="icon" className="h-6 w-6" aria-label={`Remove ${mapping.mappingType} mapping`} onClick={() => void removeMapping(row.id, mapping.id)}><Trash2 className="h-3 w-3" /></Button>
                                                                                    ) : null}
                                                                                </div>
                                                                            ))}
                                                                            {canManage && !layout.isProtectedStandard && selectedVersion.status === 'Draft' && row.rowType === 'Account' ? (
                                                                                <div className="mt-2 flex gap-1">
                                                                                    <Select value={classificationSelections[row.id] || ''} onValueChange={(value) => setClassificationSelections((current) => ({ ...current, [row.id]: value }))}>
                                                                                        <SelectTrigger className="h-8 min-w-56"><SelectValue placeholder="Add classification mapping" /></SelectTrigger>
                                                                                        <SelectContent>{classifications.map((item) => <SelectItem key={item.id} value={item.id}>{item.code} — {item.name}</SelectItem>)}</SelectContent>
                                                                                    </Select>
                                                                                    <Button variant="outline" size="sm" disabled={!classificationSelections[row.id] || busy} onClick={() => void addClassificationMapping(row.id)}>Add</Button>
                                                                                </div>
                                                                            ) : null}
                                                                            {!row.formula && row.mappings.length === 0 ? <span className="text-xs text-muted-foreground">—</span> : null}
                                                                        </td>
                                                                        <td className="p-3 text-center text-xs">{row.isVisible ? 'Visible' : 'Hidden'}{row.suppressIfZero ? ' · zero suppressed' : ''}</td>
                                                                    </tr>
                                                                ))}
                                                            </tbody>
                                                        </table>
                                                    </div>
                                                </CardContent>
                                            </Card>
                                        ) : null}
                                    </div>
                                </ScrollArea>
                            </TabsContent>
                            <TabsContent value="settings" className="m-0 min-h-0">
                                <ScrollArea className="h-full px-6">
                                    <div className="mx-auto max-w-2xl space-y-5 py-6">
                                        <Alert>
                                            <BookOpenCheck className="h-4 w-4" />
                                            <AlertTitle>Layout-level controls</AlertTitle>
                                            <AlertDescription>Default and active status apply to the whole layout. Row content remains version-controlled.</AlertDescription>
                                        </Alert>
                                        <div className="space-y-2">
                                            <Label htmlFor="layout-name">Name</Label>
                                            <Input id="layout-name" value={edit.name} disabled={!canManage || layout.isProtectedStandard} onChange={(event) => setEdit((current) => ({ ...current, name: event.target.value }))} />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="layout-description">Description</Label>
                                            <Textarea id="layout-description" value={edit.description} disabled={!canManage || layout.isProtectedStandard} onChange={(event) => setEdit((current) => ({ ...current, description: event.target.value }))} />
                                        </div>
                                        <div className="flex items-center justify-between rounded-md border p-4">
                                            <div><div className="font-medium">Default report layout</div><div className="text-sm text-muted-foreground">Used when a report requests the default published layout.</div></div>
                                            <Switch
                                                checked={edit.isDefault}
                                                disabled={!canManage || layout.isProtectedStandard || !layout.versions.some((version) => version.status === 'Published')}
                                                onCheckedChange={(checked) => setEdit((current) => ({ ...current, isDefault: checked }))}
                                            />
                                        </div>
                                        <div className="flex items-center justify-between rounded-md border p-4">
                                            <div><div className="font-medium">Active</div><div className="text-sm text-muted-foreground">Inactive layouts remain available in the audit history only.</div></div>
                                            <Switch checked={edit.isActive} disabled={!canManage || layout.isProtectedStandard} onCheckedChange={(checked) => setEdit((current) => ({ ...current, isActive: checked, isDefault: checked ? current.isDefault : false }))} />
                                        </div>
                                        {canManage && !layout.isProtectedStandard ? <Button onClick={requestMetadataSave} disabled={busy || !edit.name.trim()}><Save className="mr-2 h-4 w-4" />Save settings</Button> : null}
                                        {canManage
                                            && !layout.isProtectedStandard
                                            && !layout.isDefault
                                            && layout.versions.length > 0
                                            && layout.versions.every((version) => version.status === 'Draft') ? (
                                                <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4">
                                                    <div className="font-medium">Discard an unused Draft</div>
                                                    <p className="mt-1 text-sm text-muted-foreground">
                                                        Available only because this tenant-owned layout has never been published, is not default, and contains Draft versions only. The server rechecks every condition and records an audit tombstone.
                                                    </p>
                                                    <Button variant="destructive" className="mt-3" onClick={() => setDiscardOpen(true)} disabled={busy}>
                                                        <Trash2 className="mr-2 h-4 w-4" />Discard unused Draft
                                                    </Button>
                                                </div>
                                            ) : null}
                                    </div>
                                </ScrollArea>
                            </TabsContent>
                            <TabsContent value="audit" className="m-0 min-h-0">
                                <ScrollArea className="h-full px-6">
                                    <div className="space-y-3 py-6">
                                        {audit.length === 0 ? (
                                            <div className="rounded-md border border-dashed p-10 text-center text-muted-foreground">No layout audit events were returned.</div>
                                        ) : audit.map((event) => (
                                            <div key={event.id} className="rounded-md border p-4">
                                                <div className="flex flex-wrap items-start justify-between gap-2">
                                                    <div>
                                                        <div className="font-medium">{event.eventType.replace('Finance.Reporting.', '')}</div>
                                                        <div className="text-sm text-muted-foreground">{event.username} · {formatDateTime(event.timestamp)}</div>
                                                    </div>
                                                    <History className="h-4 w-4 text-muted-foreground" />
                                                </div>
                                                {event.detailsJson ? (
                                                    <details className="mt-3">
                                                        <summary className="cursor-pointer text-xs font-medium text-muted-foreground">Recorded details</summary>
                                                        <pre className="mt-2 max-h-48 overflow-auto rounded bg-muted p-3 text-xs">{event.detailsJson}</pre>
                                                    </details>
                                                ) : null}
                                            </div>
                                        ))}
                                    </div>
                                </ScrollArea>
                            </TabsContent>
                        </Tabs>
                    )}
                </DialogContent>
            </Dialog>
            <ExecutionPreviewDialog open={previewOpen} onOpenChange={setPreviewOpen} version={selectedVersion} statementType={layout?.statementType || 'BalanceSheet'} />
            <Dialog open={discardOpen} onOpenChange={setDiscardOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Discard unused Draft layout?</DialogTitle>
                        <DialogDescription>
                            This permanently removes the unused definition while retaining a durable Finance audit tombstone. Published, default, protected, referenced, or historically used layouts are rejected by the server.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-2">
                        <Label htmlFor="discard-layout-reason">Reason</Label>
                        <Textarea
                            id="discard-layout-reason"
                            value={discardReason}
                            maxLength={500}
                            placeholder="Explain why this unused Draft should be discarded."
                            onChange={(event) => setDiscardReason(event.target.value)}
                        />
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setDiscardOpen(false)} disabled={busy}>Cancel</Button>
                        <Button variant="destructive" onClick={() => void discardUnusedDraft()} disabled={busy || discardReason.trim().length < 5}>
                            {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Trash2 className="mr-2 h-4 w-4" />}
                            Discard Draft
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
            <AlertDialog open={!!confirmation} onOpenChange={(value) => { if (!value) setConfirmation(null); }}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>{confirmation?.title}</AlertDialogTitle>
                        <AlertDialogDescription>{confirmation?.description}</AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                            disabled={busy}
                            onClick={() => {
                                const action = confirmation?.action;
                                setConfirmation(null);
                                if (action) void action();
                            }}
                        >
                            Confirm
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </>
    );
}

export default function FinancialStatementLayoutsPage() {
    const { toast } = useToast();
    const { hasPermission, isLoading: authLoading } = useAuth();
    const { canRead, canManage, canRun } =
        resolveFinancialStatementLayoutPermissions(hasPermission);
    const [layouts, setLayouts] = useState<FinancialStatementLayoutSummaryDto[]>([]);
    const [books, setBooks] = useState<AccountingBook[]>([]);
    const [statementType, setStatementType] = useState<'all' | FinancialStatementType>('all');
    const [bookId, setBookId] = useState('all');
    const [includeInactive, setIncludeInactive] = useState(false);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [importOpen, setImportOpen] = useState(false);
    const [migrationOpen, setMigrationOpen] = useState(false);
    const [selectedLayoutId, setSelectedLayoutId] = useState<string | null>(null);
    const [readiness, setReadiness] = useState<FinancialStatementLayoutReadinessDto | null>(null);
    const [initializationOpen, setInitializationOpen] = useState(false);
    const [initializing, setInitializing] = useState(false);

    const load = useCallback(async () => {
        if (!canRead) {
            setLoading(false);
            return;
        }
        try {
            setLoading(true);
            setLoadError(null);
            const [layoutRows, accountingBooks, readinessResult] = await Promise.all([
                financialStatementLayoutDataService.getLayouts({
                    statementType: statementType === 'all' ? undefined : statementType,
                    accountingBookId: bookId === 'all' ? undefined : bookId,
                    includeInactive,
                }),
                financeDataService.getAccountingBooks(true),
                financialStatementLayoutDataService.getInitializationReadiness(),
            ]);
            setLayouts(layoutRows);
            setBooks(accountingBooks);
            setReadiness(readinessResult);
        } catch (error) {
            const message = errorMessage(error, 'Check your finance permissions and try again.');
            setLoadError(message);
            toast({
                title: 'Layouts could not be loaded',
                description: message,
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    }, [bookId, canRead, includeInactive, statementType, toast]);

    useEffect(() => {
        if (!authLoading) void load();
    }, [authLoading, load]);

    const downloadTemplate = async () => {
        try {
            await financialStatementLayoutDataService.downloadImportTemplate();
        } catch (error) {
            toast({ title: 'Download failed', description: errorMessage(error, 'The import template is unavailable.'), variant: 'destructive' });
        }
    };

    const imported = (layoutId: string) => {
        void load();
        setSelectedLayoutId(layoutId);
    };

    const initializeFromStandards = async () => {
        try {
            setInitializing(true);
            const result = await financialStatementLayoutDataService.initializeFromStandards();
            setReadiness(result.readiness);
            setInitializationOpen(false);
            toast({
                title: result.createdCount > 0
                    ? `${result.createdCount} Draft layout${result.createdCount === 1 ? '' : 's'} created`
                    : 'Tenant layouts already initialized',
                description: 'Review validation, publish each approved version, then mark one published Balance Sheet and Income Statement layout as default per book.',
            });
            await load();
        } catch (error) {
            toast({
                title: 'Initialization failed',
                description: errorMessage(error, 'Protected standards could not be initialized.'),
                variant: 'destructive',
            });
        } finally {
            setInitializing(false);
        }
    };

    const publishedCount = layouts.filter((layout) => layout.publishedVersionNumber).length;
    const draftCount = layouts.filter((layout) => layout.latestVersionNumber !== layout.publishedVersionNumber).length;

    if (authLoading) {
        return <div className="flex min-h-64 items-center justify-center"><Loader2 className="h-8 w-8 animate-spin" /></div>;
    }

    if (!canRead) {
        return (
            <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>Access restricted</AlertTitle>
                <AlertDescription>You need Finance.Read to browse financial-statement layouts. Action permissions do not grant read access.</AlertDescription>
            </Alert>
        );
    }

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
                        <BookOpenCheck className="h-8 w-8" />
                        Financial Statement Layouts
                    </h1>
                    <p className="text-muted-foreground">Import, validate, version and publish sequenced Balance Sheet and Income Statement definitions.</p>
                </div>
                {canManage ? (
                    <div className="flex flex-wrap gap-2">
                        <Button variant="outline" onClick={() => setInitializationOpen(true)}><Copy className="mr-2 h-4 w-4" />Initialize from standards</Button>
                        <Button variant="outline" onClick={downloadTemplate}><Download className="mr-2 h-4 w-4" />Excel template</Button>
                        <Button variant="outline" onClick={() => setMigrationOpen(true)}><Import className="mr-2 h-4 w-4" />Migrate legacy mappings</Button>
                        <Button onClick={() => setImportOpen(true)}><Upload className="mr-2 h-4 w-4" />Import layout</Button>
                    </div>
                ) : null}
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/reports">Financial Reports</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Statement Layouts</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {readiness && !readiness.isReady ? (
                <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Financial-statement layouts are not fully ready</AlertTitle>
                    <AlertDescription>
                        {readiness.books.filter((book) => book.missingRequirements.length > 0).map((book) => (
                            <div key={book.accountingBookId}>
                                <span className="font-medium">{book.accountingBookCode}</span>: {book.missingRequirements.join(' and ')} required.
                            </div>
                        ))}
                    </AlertDescription>
                </Alert>
            ) : null}

            <div className="grid gap-3 sm:grid-cols-3">
                <Card><CardHeader className="pb-2"><CardDescription>Visible layouts</CardDescription><CardTitle className="text-2xl">{layouts.length}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>With published version</CardDescription><CardTitle className="text-2xl">{publishedCount}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>Drafts requiring review</CardDescription><CardTitle className="text-2xl">{draftCount}</CardTitle></CardHeader></Card>
            </div>

            <Card>
                <CardHeader>
                    <div className="flex flex-wrap items-start justify-between gap-3">
                        <div><CardTitle>Layout register</CardTitle><CardDescription>Select a layout to inspect its versions, rows, validation and audit trail.</CardDescription></div>
                        <Button variant="ghost" size="sm" onClick={load} disabled={loading}><RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />Refresh</Button>
                    </div>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="grid gap-3 md:grid-cols-[220px_1fr_auto]">
                        <Select value={statementType} onValueChange={(value) => setStatementType(value as 'all' | FinancialStatementType)}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All statements</SelectItem>
                                <SelectItem value="BalanceSheet">Balance Sheet</SelectItem>
                                <SelectItem value="IncomeStatement">Income Statement</SelectItem>
                            </SelectContent>
                        </Select>
                        <Select value={bookId} onValueChange={setBookId}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All accounting books</SelectItem>
                                {books.map((book) => <SelectItem key={book.id} value={book.id}>{book.code} — {book.name}</SelectItem>)}
                            </SelectContent>
                        </Select>
                        <div className="flex items-center gap-2 rounded-md border px-3">
                            <Switch id="include-inactive" checked={includeInactive} onCheckedChange={setIncludeInactive} />
                            <Label htmlFor="include-inactive" className="whitespace-nowrap">Include inactive</Label>
                        </div>
                    </div>

                    {loadError ? (
                        <Alert variant="destructive">
                            <AlertCircle className="h-4 w-4" />
                            <AlertTitle>Layout register unavailable</AlertTitle>
                            <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
                                <span>{loadError}</span>
                                <Button variant="outline" size="sm" onClick={() => void load()}>Retry</Button>
                            </AlertDescription>
                        </Alert>
                    ) : loading ? (
                        <div className="flex justify-center py-14"><Loader2 className="h-7 w-7 animate-spin" /></div>
                    ) : layouts.length === 0 ? (
                        <div className="rounded-md border border-dashed py-14 text-center">
                            <BookOpenCheck className="mx-auto mb-3 h-8 w-8 text-muted-foreground" />
                            <div className="font-medium">No layouts match these filters</div>
                            <div className="text-sm text-muted-foreground">{canManage ? 'Import a workbook or generate a Draft from legacy mappings.' : 'Ask an authorised finance user to prepare a layout.'}</div>
                        </div>
                    ) : (
                        <div className="overflow-x-auto rounded-md border">
                            <table className="w-full text-sm">
                                <thead className="bg-muted/50">
                                    <tr>
                                        <th className="p-3 text-left">Code and name</th>
                                        <th className="p-3 text-left">Statement</th>
                                        <th className="p-3 text-left">Accounting book</th>
                                        <th className="p-3 text-center">Latest</th>
                                        <th className="p-3 text-center">Published</th>
                                        <th className="p-3 text-left">Controls</th>
                                        <th className="p-3 text-right">Open</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {layouts.map((layout) => (
                                        <tr key={layout.id} className="border-t hover:bg-muted/30">
                                            <td className="p-3"><div className="font-mono font-semibold">{layout.code}</div><div>{layout.name}</div></td>
                                            <td className="p-3">{statementLabel(layout.statementType)}</td>
                                            <td className="p-3"><div className="font-medium">{layout.accountingBookCode}</div><div className="text-xs text-muted-foreground">{layout.accountingBookName}</div></td>
                                            <td className="p-3 text-center">v{layout.latestVersionNumber}</td>
                                            <td className="p-3 text-center">{layout.publishedVersionNumber ? `v${layout.publishedVersionNumber}` : '—'}</td>
                                            <td className="p-3">
                                                <div className="flex flex-wrap gap-1">
                                                    {layout.isDefault ? <Badge>Default</Badge> : null}
                                                    {layout.isProtectedStandard ? <Badge variant="secondary">Protected standard</Badge> : null}
                                                    {!layout.isActive ? <Badge variant="outline">Inactive</Badge> : null}
                                                    {layout.latestVersionNumber !== layout.publishedVersionNumber ? <Badge variant="secondary">Draft</Badge> : null}
                                                </div>
                                            </td>
                                            <td className="p-3 text-right">
                                                <Button variant="ghost" size="sm" onClick={() => setSelectedLayoutId(layout.id)}>
                                                    Review <ChevronRight className="ml-1 h-4 w-4" />
                                                </Button>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>

            <ImportLayoutDialog open={importOpen} onOpenChange={setImportOpen} onImported={imported} />
            <LegacyMigrationDialog open={migrationOpen} onOpenChange={setMigrationOpen} books={books.filter((book) => book.isActive)} onImported={imported} />
            <LayoutDetailDialog
                layoutId={selectedLayoutId}
                open={!!selectedLayoutId}
                onOpenChange={(value) => { if (!value) setSelectedLayoutId(null); }}
                canManage={canManage}
                canRun={canRun}
                onChanged={() => void load()}
            />
            <Dialog open={initializationOpen} onOpenChange={setInitializationOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Initialize tenant layouts from protected standards</DialogTitle>
                        <DialogDescription>
                            Creates only missing tenant-owned Draft clones for each active posting book. Protected standards remain immutable and non-default. Existing clones are retained, and every resulting Draft is validated.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-2 rounded-md border p-4 text-sm">
                        <div className="font-medium">Controlled next steps</div>
                        <ol className="list-decimal space-y-1 pl-5 text-muted-foreground">
                            <li>Review each generated Balance Sheet and Income Statement Draft.</li>
                            <li>Resolve validation findings and obtain authorised publication.</li>
                            <li>Set one published layout of each statement type as the book default.</li>
                        </ol>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setInitializationOpen(false)} disabled={initializing}>Cancel</Button>
                        <Button onClick={() => void initializeFromStandards()} disabled={initializing}>
                            {initializing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Copy className="mr-2 h-4 w-4" />}
                            Create missing Drafts
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
