'use client';

import React, { useEffect, useRef, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { useToast } from '@/components/ui/use-toast';
import { TrendingUp, Plus, Edit, Upload, Filter, LineChart, Download, AlertCircle, FileText, Loader2 } from 'lucide-react';
import { financeService } from '@/services/finance.service';
import { buildExchangeRateTemplateCsv, formatImportFileSize, parseExchangeRateImportFile } from '@/lib/finance/exchange-rate-import';
import type { ExchangeRate, ExchangeRateQuoteSide, ExchangeRateType } from '@/types/finance';
import Link from 'next/link';

const MOCK_CURRENCIES = ['GHS', 'USD', 'EUR', 'GBP'];
const EXCHANGE_RATE_TYPE_OPTIONS: Array<{ value: ExchangeRateType; label: string }> = [
    { value: 'Daily', label: 'Daily' },
    { value: 'Average', label: 'Average' },
    { value: 'MonthEnd', label: 'Month End' },
    { value: 'QuarterEnd', label: 'Quarter End' },
    { value: 'YearEnd', label: 'Year End' },
    { value: 'Budget', label: 'Budget' },
    { value: 'Fixed', label: 'Fixed' },
    { value: 'Spot', label: 'Spot' },
];
const ENTRY_RATE_TYPE_OPTIONS = EXCHANGE_RATE_TYPE_OPTIONS.filter(
    option => option.value !== 'Budget' && option.value !== 'Spot'
);
const QUOTE_SIDE_OPTIONS: Array<{ value: ExchangeRateQuoteSide; label: string }> = [
    { value: 'Mid', label: 'Mid / Reference' },
    { value: 'Buying', label: 'Buying (bank buys foreign currency)' },
    { value: 'Selling', label: 'Selling (bank sells foreign currency)' },
];

const formatRateType = (rateType: ExchangeRateType | string) =>
    EXCHANGE_RATE_TYPE_OPTIONS.find((option) => option.value === rateType)?.label ?? rateType;

export default function ExchangeRatesPage() {
    const { toast } = useToast();
    const bulkFileInputRef = useRef<HTMLInputElement | null>(null);
    // Exchange rates are accounting evidence. Never replace an unavailable API response with
    // sample values: a plausible-looking mock rate can be frozen into a real posting.
    const [rates, setRates] = useState<ExchangeRate[]>([]);
    const [isLoadingRates, setIsLoadingRates] = useState(true);
    const [rateLoadError, setRateLoadError] = useState<string | null>(null);
    const [rateReloadToken, setRateReloadToken] = useState(0);
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [isBulkUploadOpen, setIsBulkUploadOpen] = useState(false);
    const [bulkUploadFile, setBulkUploadFile] = useState<File | null>(null);
    const [bulkUploadErrors, setBulkUploadErrors] = useState<string[]>([]);
    const [isBulkUploading, setIsBulkUploading] = useState(false);
    const [isBulkFileDragOver, setIsBulkFileDragOver] = useState(false);
    const [editingRate, setEditingRate] = useState<ExchangeRate | null>(null);
    const [filters, setFilters] = useState({
        fromCurrency: 'all',
        toCurrency: 'all',
        rateType: 'all',
        quoteSide: 'all',
    });
    const [formData, setFormData] = useState({
        baseCurrencyCode: 'GHS',
        targetCurrencyCode: 'USD',
        rate: '',
        rateType: 'Daily' as ExchangeRateType,
        quoteSide: 'Mid' as ExchangeRateQuoteSide,
        effectiveDate: new Date().toISOString().split('T')[0],
        rateSource: '',
    });

    useEffect(() => {
        let isMounted = true;
        setIsLoadingRates(true);
        setRateLoadError(null);

        financeService.getExchangeRates()
            .then((apiRates) => {
                if (isMounted) {
                    setRates(apiRates);
                    setIsLoadingRates(false);
                }
            })
            .catch((error) => {
                console.error('Failed to load exchange rates', error);
                if (isMounted) {
                    setRates([]);
                    setRateLoadError(error instanceof Error ? error.message : 'The exchange-rate register could not be loaded.');
                    setIsLoadingRates(false);
                }
            });

        return () => {
            isMounted = false;
        };
    }, [rateReloadToken]);

    const filteredRates = rates.filter((rate) => {
        if (filters.fromCurrency !== 'all' && rate.baseCurrencyCode !== filters.fromCurrency) return false;
        if (filters.toCurrency !== 'all' && rate.targetCurrencyCode !== filters.toCurrency) return false;
        if (filters.rateType !== 'all' && rate.rateType !== filters.rateType) return false;
        if (filters.quoteSide !== 'all' && rate.quoteSide !== filters.quoteSide) return false;
        return true;
    });

    const handleCreate = async () => {
        try {
            const newRate = await financeService.createExchangeRate({
            baseCurrencyCode: formData.baseCurrencyCode,
            targetCurrencyCode: formData.targetCurrencyCode,
            rate: parseFloat(formData.rate),
            effectiveDate: new Date(formData.effectiveDate).toISOString(),
            rateType: formData.rateType,
            quoteSide: formData.quoteSide,
            rateSource: formData.rateSource,
            isActive: true,
            });
            setRates(current => [newRate, ...current]);
            setIsCreateDialogOpen(false);
            resetForm();
        } catch (error) {
            toast({ title: 'Unable to create rate', description: error instanceof Error ? error.message : 'Please review the rate details.', variant: 'destructive' });
        }
    };

    const handleUpdate = async () => {
        if (!editingRate) return;
        try {
            const updated = await financeService.updateExchangeRate(editingRate.id, {
                baseCurrencyCode: formData.baseCurrencyCode,
                targetCurrencyCode: formData.targetCurrencyCode,
                rate: parseFloat(formData.rate),
                effectiveDate: new Date(formData.effectiveDate).toISOString(),
                rateType: formData.rateType,
                quoteSide: formData.quoteSide,
                rateSource: formData.rateSource || editingRate.rateSource,
                isActive: editingRate.isActive,
            });
            setRates(current => current.map(rate => rate.id === updated.id ? updated : rate));
            setEditingRate(null);
            resetForm();
        } catch (error) {
            toast({ title: 'Unable to update rate', description: error instanceof Error ? error.message : 'Please review the rate details.', variant: 'destructive' });
        }
    };

    const resetForm = () => {
        setFormData({
            baseCurrencyCode: 'GHS',
            targetCurrencyCode: 'USD',
            rate: '',
            effectiveDate: new Date().toISOString().split('T')[0],
            rateType: 'Daily',
            quoteSide: 'Mid',
            rateSource: '',
        });
    };

    const openEditDialog = (rate: ExchangeRate) => {
        setEditingRate(rate);
        setFormData({
            baseCurrencyCode: rate.baseCurrencyCode,
            targetCurrencyCode: rate.targetCurrencyCode,
            rate: rate.rate.toString(),
            effectiveDate: rate.effectiveDate.split('T')[0],
            rateType: rate.rateType,
            quoteSide: rate.quoteSide || 'Mid',
            rateSource: rate.rateSource || '',
        });
    };

    const resetBulkUploadState = () => {
        setBulkUploadFile(null);
        setBulkUploadErrors([]);
        setIsBulkFileDragOver(false);
        if (bulkFileInputRef.current) {
            bulkFileInputRef.current.value = '';
        }
    };

    const handleBulkDialogOpenChange = (open: boolean) => {
        setIsBulkUploadOpen(open);
        if (!open) {
            resetBulkUploadState();
        }
    };

    const handleBulkFileSelected = (file?: File) => {
        if (!file) {
            return;
        }

        setBulkUploadFile(file);
        setBulkUploadErrors([]);
    };

    const handleBulkUpload = async () => {
        if (!bulkUploadFile) {
            toast({
                title: 'Select a file',
                description: 'Choose a CSV or XLSX exchange-rate file before uploading.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setIsBulkUploading(true);
            setBulkUploadErrors([]);

            const parsedImport = await parseExchangeRateImportFile(bulkUploadFile);
            if (parsedImport.errors.length > 0) {
                setBulkUploadErrors(parsedImport.errors);
                toast({
                    title: 'Upload validation failed',
                    description: `${parsedImport.errors.length} issue(s) need correction before import.`,
                    variant: 'destructive',
                });
                return;
            }

            const uploadedRates = await financeService.bulkUploadExchangeRates(parsedImport.rows);
            setRates((currentRates) => {
                const uploadedIds = new Set(uploadedRates.map((rate) => rate.id));
                return [
                    ...uploadedRates,
                    ...currentRates.filter((rate) => !uploadedIds.has(rate.id)),
                ];
            });

            const skippedCount = parsedImport.rows.length - uploadedRates.length;
            if (skippedCount > 0) {
                setBulkUploadErrors([
                    `${uploadedRates.length} of ${parsedImport.rows.length} row(s) were imported. ${skippedCount} row(s) were rejected by server-side finance validation, usually because a matching rate already exists or overlaps an existing rate window.`,
                ]);
                toast({
                    title: 'Import partially completed',
                    description: `${uploadedRates.length} of ${parsedImport.rows.length} exchange-rate row(s) were imported.`,
                    variant: 'destructive',
                });
                return;
            }

            toast({
                title: 'Exchange rates imported',
                description: `${uploadedRates.length} exchange-rate row(s) uploaded successfully.`,
            });
            setIsBulkUploadOpen(false);
            resetBulkUploadState();
        } catch (error: any) {
            const message = error?.message || 'Unable to upload exchange rates.';
            setBulkUploadErrors([message]);
            toast({
                title: 'Upload failed',
                description: message,
                variant: 'destructive',
            });
        } finally {
            setIsBulkUploading(false);
        }
    };

    const handleDownloadTemplate = () => {
        const templateDate = new Date().toISOString().split('T')[0];
        const csv = buildExchangeRateTemplateCsv(templateDate);

        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `exchange-rate-upload-template-${templateDate}.csv`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <TrendingUp className="h-8 w-8" />
                        Exchange Rates
                    </h1>
                    <p className="text-muted-foreground">
                        Manage currency exchange rates for multi-currency transactions
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button asChild variant="outline" className="mr-2">
                        <Link href="/finance/exchange-rates/trends">
                            <LineChart className="mr-2 h-4 w-4" />
                            Trend Analysis
                        </Link>
                    </Button>
                    <Dialog open={isBulkUploadOpen} onOpenChange={handleBulkDialogOpenChange}>
                        <DialogTrigger asChild>
                            <Button variant="outline" disabled={isLoadingRates || Boolean(rateLoadError)}>
                                <Upload className="mr-2 h-4 w-4" />
                                Bulk Upload
                            </Button>
                        </DialogTrigger>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Bulk Upload Exchange Rates</DialogTitle>
                                <DialogDescription>
                                    Upload multiple exchange rates from a CSV or Excel file
                                </DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                                <input
                                    ref={bulkFileInputRef}
                                    type="file"
                                    accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                                    className="hidden"
                                    onChange={(event) => handleBulkFileSelected(event.target.files?.[0])}
                                />
                                <div
                                    role="button"
                                    tabIndex={0}
                                    className={`rounded-lg border-2 border-dashed p-8 text-center transition-colors ${isBulkFileDragOver ? 'border-primary bg-primary/5' : ''}`}
                                    onClick={() => bulkFileInputRef.current?.click()}
                                    onKeyDown={(event) => {
                                        if (event.key === 'Enter' || event.key === ' ') {
                                            event.preventDefault();
                                            bulkFileInputRef.current?.click();
                                        }
                                    }}
                                    onDragOver={(event) => {
                                        event.preventDefault();
                                        setIsBulkFileDragOver(true);
                                    }}
                                    onDragLeave={() => setIsBulkFileDragOver(false)}
                                    onDrop={(event) => {
                                        event.preventDefault();
                                        setIsBulkFileDragOver(false);
                                        handleBulkFileSelected(event.dataTransfer.files?.[0]);
                                    }}
                                >
                                    {bulkUploadFile ? (
                                        <div className="space-y-2">
                                            <FileText className="mx-auto h-12 w-12 text-primary" />
                                            <p className="text-sm font-medium">{bulkUploadFile.name}</p>
                                            <p className="text-xs text-muted-foreground">{formatImportFileSize(bulkUploadFile)}</p>
                                        </div>
                                    ) : (
                                        <>
                                            <Upload className="mx-auto h-12 w-12 text-muted-foreground" />
                                            <p className="mt-2 text-sm text-muted-foreground">
                                                Drag and drop your file here, or click to browse
                                            </p>
                                            <p className="text-xs text-muted-foreground mt-1">
                                                Supported formats: CSV, XLSX
                                            </p>
                                        </>
                                    )}
                                    <Button
                                        type="button"
                                        variant="outline"
                                        className="mt-4"
                                        onClick={(event) => {
                                            event.stopPropagation();
                                            bulkFileInputRef.current?.click();
                                        }}
                                        disabled={isBulkUploading}
                                    >
                                        Select File
                                    </Button>
                                </div>
                                <div className="flex flex-col gap-3 rounded-lg border bg-muted/30 p-3 sm:flex-row sm:items-center sm:justify-between">
                                    <div>
                                        <p className="text-sm font-medium">Need the correct format?</p>
                                        <p className="text-xs text-muted-foreground">
                                            Start from the CSV template with required and optional fields.
                                        </p>
                                    </div>
                                    <Button type="button" variant="outline" onClick={handleDownloadTemplate}>
                                        <Download className="mr-2 h-4 w-4" />
                                        Download Template
                                    </Button>
                                </div>
                                <div className="text-sm text-muted-foreground">
                                    <p className="font-semibold mb-2">Required columns:</p>
                                    <ul className="list-disc list-inside space-y-1">
                                        <li>baseCurrencyCode (e.g., GHS)</li>
                                        <li>targetCurrencyCode (e.g., USD)</li>
                                        <li>rate (e.g., 12.5000)</li>
                                        <li>effectiveDate (YYYY-MM-DD)</li>
                                        <li>rateType (Daily/Average/MonthEnd/QuarterEnd/YearEnd/Fixed)</li>
                                        <li>rateSource (e.g., Manual, BankFeed, Bank of Ghana)</li>
                                    </ul>
                                </div>
                                {bulkUploadErrors.length > 0 && (
                                    <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
                                        <div className="flex items-start gap-2 font-medium">
                                            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                                            <span>Upload checks found issues</span>
                                        </div>
                                        <ul className="mt-2 list-disc space-y-1 pl-5">
                                            {bulkUploadErrors.slice(0, 8).map((error, index) => (
                                                <li key={`${error}-${index}`}>{error}</li>
                                            ))}
                                        </ul>
                                        {bulkUploadErrors.length > 8 && (
                                            <p className="mt-2">Showing first 8 of {bulkUploadErrors.length} issue(s).</p>
                                        )}
                                    </div>
                                )}
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => handleBulkDialogOpenChange(false)} disabled={isBulkUploading}>
                                    Cancel
                                </Button>
                                <Button onClick={handleBulkUpload} disabled={!bulkUploadFile || isBulkUploading}>
                                    {isBulkUploading ? (
                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    ) : (
                                        <Upload className="mr-2 h-4 w-4" />
                                    )}
                                    Upload Rates
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                    <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                        <DialogTrigger asChild>
                            <Button
                                disabled={isLoadingRates || Boolean(rateLoadError)}
                                onClick={() => { resetForm(); setEditingRate(null); }}
                            >
                                <Plus className="mr-2 h-4 w-4" />
                                Add Rate
                            </Button>
                        </DialogTrigger>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Add Exchange Rate</DialogTitle>
                                <DialogDescription>
                                    Create a new exchange rate entry
                                </DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label htmlFor="baseCurrencyCode">Base Currency</Label>
                                        <Select
                                            value={formData.baseCurrencyCode}
                                            onValueChange={(value) => setFormData({ ...formData, baseCurrencyCode: value })}
                                        >
                                            <SelectTrigger id="baseCurrencyCode">
                                                <SelectValue placeholder="Select currency" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="GHS">GHS - Ghanaian Cedi</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="targetCurrencyCode">Target Currency</Label>
                                        <Select
                                            value={formData.targetCurrencyCode}
                                            onValueChange={(value) =>
                                                setFormData({ ...formData, targetCurrencyCode: value })
                                            }
                                        >
                                            <SelectTrigger id="targetCurrencyCode">
                                                <SelectValue placeholder="Select currency" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="USD">USD - US Dollar</SelectItem>
                                                <SelectItem value="EUR">EUR - Euro</SelectItem>
                                                <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="rate">Exchange Rate</Label>
                                    <Input
                                        id="rate"
                                        type="number"
                                        step="0.0001"
                                        placeholder="12.5000"
                                        value={formData.rate}
                                        onChange={(e) => setFormData({ ...formData, rate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="effectiveDate">Effective Date</Label>
                                    <Input
                                        id="effectiveDate"
                                        type="date"
                                        value={formData.effectiveDate}
                                        onChange={(e) => setFormData({ ...formData, effectiveDate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="rateType">Rate Type</Label>
                                    <Select
                                        value={formData.rateType}
                                        onValueChange={(value: ExchangeRateType) => setFormData({ ...formData, rateType: value })}
                                    >
                                        <SelectTrigger id="rateType">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {ENTRY_RATE_TYPE_OPTIONS.map((option) => (
                                                <SelectItem key={option.value} value={option.value}>
                                                    {option.label}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">
                                        Daily and closing rates are operational. Average and Fixed are advanced; Budget and Spot are hidden until supported workflows exist.
                                    </p>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="quoteSide">Quote Side</Label>
                                    <Select
                                        value={formData.quoteSide}
                                        onValueChange={(value: ExchangeRateQuoteSide) => setFormData({ ...formData, quoteSide: value })}
                                    >
                                        <SelectTrigger id="quoteSide"><SelectValue /></SelectTrigger>
                                        <SelectContent>
                                            {QUOTE_SIDE_OPTIONS.map(option => (
                                                <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">Buying and selling are defined from the bank/provider perspective.</p>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="rateSource">Source</Label>
                                    <Input
                                        id="rateSource"
                                        placeholder="e.g. Bank of Ghana"
                                        value={formData.rateSource}
                                        onChange={(e) => setFormData({ ...formData, rateSource: e.target.value })}
                                    />
                                </div>
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                                    Cancel
                                </Button>
                                <Button onClick={handleCreate}>Create Rate</Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                </div>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Exchange Rates</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {rateLoadError && (
                <div role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-destructive">
                    <div className="flex items-start justify-between gap-4">
                        <div className="flex items-start gap-2">
                            <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" />
                            <div>
                                <p className="font-semibold">Exchange-rate register unavailable</p>
                                <p className="text-sm">
                                    {rateLoadError} No sample rates are displayed because only server-retained, approved evidence may be used for Finance transactions.
                                </p>
                            </div>
                        </div>
                        <Button
                            type="button"
                            variant="outline"
                            onClick={() => setRateReloadToken((current) => current + 1)}
                        >
                            Retry
                        </Button>
                    </div>
                </div>
            )}

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <Filter className="h-5 w-5" />
                        Filters
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
                        <div className="space-y-2">
                            <Label htmlFor="filterFrom">Base Currency</Label>
                            <Select
                                value={filters.fromCurrency}
                                onValueChange={(value) => setFilters({ ...filters, fromCurrency: value })}
                            >
                                <SelectTrigger id="filterFrom">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Currencies</SelectItem>
                                    {MOCK_CURRENCIES.map((curr) => (
                                        <SelectItem key={curr} value={curr}>{curr}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="filterQuoteSide">Quote Side</Label>
                            <Select
                                value={filters.quoteSide}
                                onValueChange={(value) => setFilters({ ...filters, quoteSide: value })}
                            >
                                <SelectTrigger id="filterQuoteSide"><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Quote Sides</SelectItem>
                                    {QUOTE_SIDE_OPTIONS.map(option => (
                                        <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="filterTo">Target Currency</Label>
                            <Select
                                value={filters.toCurrency}
                                onValueChange={(value) => setFilters({ ...filters, toCurrency: value })}
                            >
                                <SelectTrigger id="filterTo">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Currencies</SelectItem>
                                    {MOCK_CURRENCIES.map((curr) => (
                                        <SelectItem key={curr} value={curr}>{curr}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="filterType">Rate Type</Label>
                            <Select
                                value={filters.rateType}
                                onValueChange={(value) => setFilters({ ...filters, rateType: value })}
                            >
                                <SelectTrigger id="filterType">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Types</SelectItem>
                                    {EXCHANGE_RATE_TYPE_OPTIONS.map((option) => (
                                        <SelectItem key={option.value} value={option.value}>
                                            {option.label}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Exchange Rates Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Exchange Rates ({filteredRates.length})</CardTitle>
                    <CardDescription>
                        Historical and current exchange rates
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <table className="w-full">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-4 text-left font-medium">Currency Pair</th>
                                    <th className="p-4 text-right font-medium">Rate</th>
                                    <th className="p-4 text-left font-medium">Effective Date</th>
                                    <th className="p-4 text-left font-medium">Type</th>
                                    <th className="p-4 text-left font-medium">Quote Side</th>
                                    <th className="p-4 text-left font-medium">Source</th>
                                    <th className="p-4 text-right font-medium">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {isLoadingRates && (
                                    <tr>
                                        <td colSpan={7} className="p-8 text-center text-muted-foreground">
                                            <Loader2 className="mr-2 inline h-4 w-4 animate-spin" />
                                            Loading retained exchange rates…
                                        </td>
                                    </tr>
                                )}
                                {!isLoadingRates && !rateLoadError && filteredRates.length === 0 && (
                                    <tr>
                                        <td colSpan={7} className="p-8 text-center text-muted-foreground">
                                            No exchange rates match the selected filters.
                                        </td>
                                    </tr>
                                )}
                                {!isLoadingRates && !rateLoadError && filteredRates.map((rate) => (
                                    <tr key={rate.id} className="border-b hover:bg-muted/50">
                                        <td className="p-4 font-mono font-semibold">{rate.baseCurrencyCode}/{rate.targetCurrencyCode}</td>
                                        <td className="p-4 text-right font-mono">{rate.rate.toFixed(4)}</td>
                                        <td className="p-4">{formatDate(rate.effectiveDate)}</td>
                                        <td className="p-4">
                                            <Badge variant={rate.rateType === 'Daily' ? 'default' : 'secondary'}>
                                                {formatRateType(rate.rateType)}
                                            </Badge>
                                        </td>
                                        <td className="p-4">
                                            <Badge variant="outline">{rate.quoteSide || 'Mid'}</Badge>
                                        </td>
                                        <td className="p-4 text-sm text-muted-foreground">{rate.rateSource}</td>
                                        <td className="p-4 text-right">
                                            <Dialog>
                                                <DialogTrigger asChild>
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => openEditDialog(rate)}
                                                    >
                                                        <Edit className="h-4 w-4" />
                                                    </Button>
                                                </DialogTrigger>
                                                <DialogContent>
                                                    <DialogHeader>
                                                        <DialogTitle>Edit Exchange Rate</DialogTitle>
                                                        <DialogDescription>
                                                            Update exchange rate details
                                                        </DialogDescription>
                                                    </DialogHeader>
                                                    <div className="space-y-4 py-4">
                                                        <div className="grid grid-cols-2 gap-4">
                                                            <div className="space-y-2">
                                                                <Label>Base Currency</Label>
                                                                <Input value={formData.baseCurrencyCode} disabled />
                                                            </div>
                                                            <div className="space-y-2">
                                                                <Label>Target Currency</Label>
                                                                <Input value={formData.targetCurrencyCode} disabled />
                                                            </div>
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-rate">Exchange Rate</Label>
                                                            <Input
                                                                id="edit-rate"
                                                                type="number"
                                                                step="0.0001"
                                                                value={formData.rate}
                                                                onChange={(e) => setFormData({ ...formData, rate: e.target.value })}
                                                            />
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-effectiveDate">Effective Date</Label>
                                                            <Input
                                                                id="edit-effectiveDate"
                                                                type="date"
                                                                value={formData.effectiveDate}
                                                                onChange={(e) => setFormData({ ...formData, effectiveDate: e.target.value })}
                                                            />
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-rateType">Rate Type</Label>
                                                            <Select
                                                                value={formData.rateType}
                                                                onValueChange={(value: ExchangeRateType) => setFormData({ ...formData, rateType: value })}
                                                            >
                                                                <SelectTrigger id="edit-rateType">
                                                                    <SelectValue />
                                                                </SelectTrigger>
                                                                <SelectContent>
                                                                    {!ENTRY_RATE_TYPE_OPTIONS.some(option => option.value === formData.rateType) && (
                                                                        <SelectItem value={formData.rateType} disabled>
                                                                            {formatRateType(formData.rateType)} (legacy)
                                                                        </SelectItem>
                                                                    )}
                                                                    {ENTRY_RATE_TYPE_OPTIONS.map((option) => (
                                                                        <SelectItem key={option.value} value={option.value}>
                                                                            {option.label}
                                                                        </SelectItem>
                                                                    ))}
                                                                </SelectContent>
                                                            </Select>
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-quoteSide">Quote Side</Label>
                                                            <Select
                                                                value={formData.quoteSide}
                                                                onValueChange={(value: ExchangeRateQuoteSide) => setFormData({ ...formData, quoteSide: value })}
                                                            >
                                                                <SelectTrigger id="edit-quoteSide"><SelectValue /></SelectTrigger>
                                                                <SelectContent>
                                                                    {QUOTE_SIDE_OPTIONS.map(option => (
                                                                        <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                                                                    ))}
                                                                </SelectContent>
                                                            </Select>
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-source">Source</Label>
                                                            <Input
                                                                id="edit-source"
                                                                value={formData.rateSource}
                                                                onChange={(e) => setFormData({ ...formData, rateSource: e.target.value })}
                                                            />
                                                        </div>
                                                    </div>
                                                    <DialogFooter>
                                                        <Button variant="outline" onClick={() => setEditingRate(null)}>
                                                            Cancel
                                                        </Button>
                                                        <Button onClick={handleUpdate}>Update Rate</Button>
                                                    </DialogFooter>
                                                </DialogContent>
                                            </Dialog>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
