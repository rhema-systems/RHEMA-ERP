'use client';

import { useMemo, useState } from 'react';
import { Check, ChevronsUpDown, Download, FileSpreadsheet, FileText, Loader2, Play, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatCurrency, cn } from '@/lib/utils';
import type {
    DetailedLedgerReport,
    LedgerPartnerOption,
} from '@/components/finance/PartnerDetailedLedgerReport';

interface StatementReportParams {
    fromDate: string;
    toDate: string;
    partnerIds: string[];
    showPartnerCurrency: boolean;
}

interface PartnerStatementReportProps {
    title: string;
    description: string;
    partnerLabel: string;
    partnerPluralLabel: string;
    currencyToggleLabel: string;
    exportFilePrefix: string;
    partners: LedgerPartnerOption[];
    partnersLoading?: boolean;
    loadReport: (params: StatementReportParams) => Promise<DetailedLedgerReport>;
    downloadCsv: (params: StatementReportParams) => Promise<Blob>;
    downloadPdf?: (params: StatementReportParams) => Promise<void>;
    downloadXlsx?: (params: StatementReportParams) => Promise<void>;
    canExport?: boolean;
}

const isoDate = (date: Date) => date.toISOString().slice(0, 10);

const firstDayOfMonth = () => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
};

const saveBlob = (blob: Blob, fileName: string) => {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');

    try {
        link.href = url;
        link.download = fileName;
        link.click();
    } finally {
        window.URL.revokeObjectURL(url);
    }
};

const formatReportAmount = (amount: number, currencyCode: string) => {
    if (currencyCode === 'Supplier Currency' || currencyCode === 'Customer Currency') {
        return amount.toLocaleString(undefined, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        });
    }

    return formatCurrency(amount, currencyCode);
};

export function PartnerStatementReport({
    title,
    description,
    partnerLabel,
    partnerPluralLabel,
    currencyToggleLabel,
    exportFilePrefix,
    partners,
    partnersLoading = false,
    loadReport,
    downloadCsv,
    downloadPdf,
    downloadXlsx,
    canExport = true,
}: PartnerStatementReportProps) {
    const [fromDate, setFromDate] = useState(() => isoDate(firstDayOfMonth()));
    const [toDate, setToDate] = useState(() => isoDate(new Date()));
    const [selectedPartnerIds, setSelectedPartnerIds] = useState<string[]>([]);
    const [showPartnerCurrency, setShowPartnerCurrency] = useState(false);
    const [report, setReport] = useState<DetailedLedgerReport | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [exportingFormat, setExportingFormat] = useState<'csv' | 'pdf' | 'xlsx' | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [partnerPickerOpen, setPartnerPickerOpen] = useState(false);

    const selectedPartners = useMemo(
        () => partners.filter((partner) => selectedPartnerIds.includes(partner.id)),
        [partners, selectedPartnerIds]
    );

    const selectedLabel = selectedPartners.length === 0
        ? `All ${partnerPluralLabel}`
        : selectedPartners.length === 1
            ? selectedPartners[0].name
            : `${selectedPartners.length} ${partnerPluralLabel}`;

    const reportParams = (): StatementReportParams => ({
        fromDate,
        toDate,
        partnerIds: selectedPartnerIds,
        showPartnerCurrency,
    });

    const togglePartner = (partnerId: string) => {
        setSelectedPartnerIds((current) =>
            current.includes(partnerId)
                ? current.filter((id) => id !== partnerId)
                : [...current, partnerId]
        );
    };

    const runReport = async () => {
        setIsLoading(true);
        setError(null);
        try {
            setReport(await loadReport(reportParams()));
        } catch (err) {
            console.error(`Failed to load ${title}`, err);
            setError(`Failed to generate ${title.toLowerCase()}.`);
        } finally {
            setIsLoading(false);
        }
    };

    const exportCsv = async () => {
        setExportingFormat('csv');
        setError(null);
        try {
            const blob = await downloadCsv(reportParams());
            saveBlob(blob, `${exportFilePrefix}-${fromDate}-${toDate}.csv`);
        } catch (err) {
            console.error(`Failed to export ${title}`, err);
            setError(`Failed to export ${title.toLowerCase()}.`);
        } finally {
            setExportingFormat(null);
        }
    };

    const exportControlledDocument = async (
        formatName: 'PDF' | 'XLSX',
        download: (params: StatementReportParams) => Promise<void>
    ) => {
        const format = formatName.toLowerCase() as 'pdf' | 'xlsx';
        setExportingFormat(format);
        setError(null);
        try {
            await download(reportParams());
        } catch (err) {
            console.error(`Failed to export ${title} as ${formatName}`, err);
            setError(`Failed to export ${title.toLowerCase()} as ${formatName}.`);
        } finally {
            setExportingFormat(null);
        }
    };

    return (
        <div className="space-y-6">
            <Card>
                <CardHeader>
                    <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                        <div>
                            <CardTitle>{title}</CardTitle>
                            <CardDescription>{description}</CardDescription>
                        </div>
                        {canExport && (
                            <div className="flex flex-wrap gap-2">
                                <Button
                                    variant="outline"
                                    onClick={exportCsv}
                                    disabled={exportingFormat !== null || partnersLoading}
                                >
                                    {exportingFormat === 'csv' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                                    CSV
                                </Button>
                                {downloadPdf && (
                                    <Button
                                        variant="outline"
                                        onClick={() => exportControlledDocument('PDF', downloadPdf)}
                                        disabled={exportingFormat !== null || partnersLoading}
                                    >
                                        {exportingFormat === 'pdf' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileText className="mr-2 h-4 w-4" />}
                                        PDF
                                    </Button>
                                )}
                                {downloadXlsx && (
                                    <Button
                                        variant="outline"
                                        onClick={() => exportControlledDocument('XLSX', downloadXlsx)}
                                        disabled={exportingFormat !== null || partnersLoading}
                                    >
                                        {exportingFormat === 'xlsx' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileSpreadsheet className="mr-2 h-4 w-4" />}
                                        Excel
                                    </Button>
                                )}
                            </div>
                        )}
                    </div>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="grid gap-4 lg:grid-cols-[180px_180px_minmax(260px,1fr)_auto] lg:items-end">
                        <div className="space-y-2">
                            <Label htmlFor={`${exportFilePrefix}-from-date`}>Start Date</Label>
                            <Input
                                id={`${exportFilePrefix}-from-date`}
                                type="date"
                                value={fromDate}
                                onChange={(event) => setFromDate(event.target.value)}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor={`${exportFilePrefix}-to-date`}>End Date</Label>
                            <Input
                                id={`${exportFilePrefix}-to-date`}
                                type="date"
                                value={toDate}
                                onChange={(event) => setToDate(event.target.value)}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label>{partnerPluralLabel}</Label>
                            <Popover open={partnerPickerOpen} onOpenChange={setPartnerPickerOpen}>
                                <PopoverTrigger asChild>
                                    <Button variant="outline" role="combobox" className="w-full justify-between">
                                        <span className="truncate">{selectedLabel}</span>
                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-[360px] p-0" align="start">
                                    <Command>
                                        <CommandInput placeholder={`Search ${partnerPluralLabel.toLowerCase()}...`} />
                                        <CommandList>
                                            <CommandEmpty>No {partnerPluralLabel.toLowerCase()} found.</CommandEmpty>
                                            <CommandGroup>
                                                <CommandItem
                                                    value={`all ${partnerPluralLabel}`}
                                                    onSelect={() => setSelectedPartnerIds([])}
                                                >
                                                    <Check className={cn('mr-2 h-4 w-4', selectedPartnerIds.length === 0 ? 'opacity-100' : 'opacity-0')} />
                                                    All {partnerPluralLabel}
                                                </CommandItem>
                                                {partners.map((partner) => (
                                                    <CommandItem
                                                        key={partner.id}
                                                        value={`${partner.code} ${partner.name}`}
                                                        onSelect={() => togglePartner(partner.id)}
                                                    >
                                                        <Check className={cn('mr-2 h-4 w-4', selectedPartnerIds.includes(partner.id) ? 'opacity-100' : 'opacity-0')} />
                                                        <span className="truncate">{partner.name}</span>
                                                        <span className="ml-auto text-xs text-muted-foreground">{partner.code}</span>
                                                    </CommandItem>
                                                ))}
                                            </CommandGroup>
                                        </CommandList>
                                    </Command>
                                </PopoverContent>
                            </Popover>
                        </div>
                        <Button onClick={runReport} disabled={isLoading || partnersLoading} className="min-w-[140px]">
                            {isLoading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Play className="mr-2 h-4 w-4" />}
                            Run Report
                        </Button>
                    </div>

                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <label className="flex items-center gap-2 text-sm">
                            <Checkbox
                                checked={showPartnerCurrency}
                                onCheckedChange={(checked) => setShowPartnerCurrency(checked === true)}
                            />
                            {currencyToggleLabel}
                        </label>
                        {selectedPartners.length > 0 && (
                            <Button variant="ghost" size="sm" onClick={() => setSelectedPartnerIds([])}>
                                <X className="mr-2 h-4 w-4" />
                                Clear selection
                            </Button>
                        )}
                    </div>
                </CardContent>
            </Card>

            {error && (
                <div className="rounded-md border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
                    {error}
                </div>
            )}

            {report && (
                <div className="space-y-6">
                    {report.warnings.length > 0 && (
                        <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                            {report.warnings.map((warning) => (
                                <div key={warning}>{warning}</div>
                            ))}
                        </div>
                    )}

                    <div className="grid gap-4 md:grid-cols-4">
                        <div className="rounded-md border bg-background p-4">
                            <div className="text-sm text-muted-foreground">Opening Balance</div>
                            <div className="mt-2 text-2xl font-bold">{formatReportAmount(report.totalOpeningBalance, report.currencyCode)}</div>
                        </div>
                        <div className="rounded-md border bg-background p-4">
                            <div className="text-sm text-muted-foreground">Debits</div>
                            <div className="mt-2 text-2xl font-bold">{formatReportAmount(report.totalDebits, report.currencyCode)}</div>
                        </div>
                        <div className="rounded-md border bg-background p-4">
                            <div className="text-sm text-muted-foreground">Credits</div>
                            <div className="mt-2 text-2xl font-bold">{formatReportAmount(report.totalCredits, report.currencyCode)}</div>
                        </div>
                        <div className="rounded-md border bg-primary/5 p-4">
                            <div className="text-sm text-primary">Closing Balance</div>
                            <div className="mt-2 text-2xl font-bold text-primary">{formatReportAmount(report.totalClosingBalance, report.currencyCode)}</div>
                        </div>
                    </div>

                    {report.accounts.length === 0 ? (
                        <div className="rounded-md border bg-background py-16 text-center text-muted-foreground">
                            No statement activity found for the selected parameters.
                        </div>
                    ) : (
                        report.accounts.map((account) => (
                            <div key={account.id} className="rounded-md border bg-background">
                                <div className="flex flex-col gap-3 border-b p-5 lg:flex-row lg:items-center lg:justify-between">
                                    <div>
                                        <h3 className="text-lg font-semibold">{account.name}</h3>
                                        <p className="text-sm text-muted-foreground">
                                            {account.code} - {account.currencyCode}
                                        </p>
                                    </div>
                                    <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm lg:grid-cols-4">
                                        <div>
                                            <span className="text-muted-foreground">Opening</span>
                                            <div className="font-semibold">{formatCurrency(account.openingBalance, account.currencyCode)}</div>
                                        </div>
                                        <div>
                                            <span className="text-muted-foreground">Debits</span>
                                            <div className="font-semibold">{formatCurrency(account.totalDebits, account.currencyCode)}</div>
                                        </div>
                                        <div>
                                            <span className="text-muted-foreground">Credits</span>
                                            <div className="font-semibold">{formatCurrency(account.totalCredits, account.currencyCode)}</div>
                                        </div>
                                        <div>
                                            <span className="text-muted-foreground">Closing</span>
                                            <div className="font-semibold">{formatCurrency(account.closingBalance, account.currencyCode)}</div>
                                        </div>
                                    </div>
                                </div>
                                <div className="overflow-x-auto">
                                    <Table>
                                        <TableHeader>
                                            <TableRow>
                                                <TableHead className="min-w-[110px]">Date</TableHead>
                                                <TableHead className="min-w-[150px]">Type</TableHead>
                                                <TableHead className="min-w-[140px]">Document</TableHead>
                                                <TableHead className="min-w-[140px]">Reference</TableHead>
                                                <TableHead className="min-w-[240px]">Description</TableHead>
                                                <TableHead>Curr.</TableHead>
                                                <TableHead className="text-right">Debit</TableHead>
                                                <TableHead className="text-right">Credit</TableHead>
                                                <TableHead className="text-right">Balance</TableHead>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            <TableRow className="bg-muted/40">
                                                <TableCell>{report.fromDate}</TableCell>
                                                <TableCell className="font-medium">Opening Balance</TableCell>
                                                <TableCell />
                                                <TableCell />
                                                <TableCell />
                                                <TableCell>{account.currencyCode}</TableCell>
                                                <TableCell />
                                                <TableCell />
                                                <TableCell className="text-right font-semibold">{formatCurrency(account.openingBalance, account.currencyCode)}</TableCell>
                                            </TableRow>
                                            {account.lines.map((line, index) => (
                                                <TableRow key={`${line.sourceDocumentId}-${line.transactionType}-${index}`}>
                                                    <TableCell>{line.transactionDate.slice(0, 10)}</TableCell>
                                                    <TableCell>{line.transactionType}</TableCell>
                                                    <TableCell className="font-medium">{line.documentNumber}</TableCell>
                                                    <TableCell>{line.reference || '-'}</TableCell>
                                                    <TableCell>{line.description}</TableCell>
                                                    <TableCell>{line.transactionCurrencyCode}</TableCell>
                                                    <TableCell className="text-right">{line.debit ? formatCurrency(line.debit, account.currencyCode) : '-'}</TableCell>
                                                    <TableCell className="text-right">{line.credit ? formatCurrency(line.credit, account.currencyCode) : '-'}</TableCell>
                                                    <TableCell className="text-right font-medium">{formatCurrency(line.runningBalance, account.currencyCode)}</TableCell>
                                                </TableRow>
                                            ))}
                                            <TableRow className="bg-muted/40">
                                                <TableCell>{report.toDate}</TableCell>
                                                <TableCell className="font-medium">Closing Balance</TableCell>
                                                <TableCell />
                                                <TableCell />
                                                <TableCell />
                                                <TableCell>{account.currencyCode}</TableCell>
                                                <TableCell className="text-right font-semibold">{formatCurrency(account.totalDebits, account.currencyCode)}</TableCell>
                                                <TableCell className="text-right font-semibold">{formatCurrency(account.totalCredits, account.currencyCode)}</TableCell>
                                                <TableCell className="text-right font-semibold">{formatCurrency(account.closingBalance, account.currencyCode)}</TableCell>
                                            </TableRow>
                                        </TableBody>
                                    </Table>
                                </div>
                            </div>
                        ))
                    )}
                </div>
            )}
        </div>
    );
}
