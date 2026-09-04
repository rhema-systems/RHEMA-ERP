'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { FileSpreadsheet, Plus, Save, Trash2, X } from 'lucide-react';
import { toast } from 'sonner';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { financeDataService } from '@/services/finance/finance-data.service';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import { useDocumentSequence } from '@/hooks/use-document-sequence';
import { FinanceDocumentTypes } from '@/types/document-numbering';
import type { FiscalPeriod } from '@/types/finance';
import type { UnitAccount } from '@/types/unit-accounts';
import { appendJournalLine, createFreshUnitJournalLine } from '@/lib/finance/journal-line-addition';

interface EntryLine {
    id: string;
    unitAccountId: string;
    quantity: string;
    description: string;
}

function inputDate(value?: string) {
    if (!value) return '';
    return value.includes('T') ? value.split('T')[0] : value.slice(0, 10);
}

function isOpenPeriod(period: FiscalPeriod) {
    const status = period.status ?? period.periodStatus;
    return (period.isOpen ?? status === 'Open') && !period.isClosed && !period.isLocked;
}

function isDateInPeriod(date: string, period?: FiscalPeriod) {
    if (!date || !period) return false;
    return date >= inputDate(period.startDate) && date <= inputDate(period.endDate);
}

function defaultDateForPeriod(period: FiscalPeriod, currentDate: string) {
    return isDateInPeriod(currentDate, period) ? currentDate : inputDate(period.endDate);
}

export default function NewUnitJournalEntryPage() {
    const router = useRouter();
    const entrySequence = useDocumentSequence('Finance', FinanceDocumentTypes.UnitJournalEntry);
    const today = new Date().toISOString().split('T')[0];

    const [formData, setFormData] = useState({
        entryDate: today,
        fiscalPeriodId: '',
        description: '',
        sourceDocument: '',
    });
    const [lines, setLines] = useState<EntryLine[]>([
        { id: '1', unitAccountId: '', quantity: '', description: '' },
    ]);
    const [accounts, setAccounts] = useState<UnitAccount[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [isLoadingLookups, setIsLoadingLookups] = useState(true);
    const [isSaving, setIsSaving] = useState(false);

    useEffect(() => {
        let isMounted = true;

        const loadLookups = async () => {
            try {
                setIsLoadingLookups(true);
                const [postingAccounts, fiscalPeriods] = await Promise.all([
                    unitAccountsDataService.getUnitAccounts({ isActive: true, isPostingAccount: true }),
                    financeDataService.getFiscalPeriods(),
                ]);
                const openPeriods = fiscalPeriods.filter(isOpenPeriod);

                if (!isMounted) return;

                setAccounts(postingAccounts);
                setPeriods(openPeriods);

                if (openPeriods.length > 0) {
                    const matchingPeriod = openPeriods.find((period) => isDateInPeriod(today, period)) ?? openPeriods[0];
                    setFormData((current) => ({
                        ...current,
                        fiscalPeriodId: matchingPeriod.id,
                        entryDate: defaultDateForPeriod(matchingPeriod, current.entryDate),
                    }));
                }
            } catch (error: any) {
                toast.error(error?.message || 'Failed to load unit journal entry lookups.');
            } finally {
                if (isMounted) {
                    setIsLoadingLookups(false);
                }
            }
        };

        loadLookups();

        return () => {
            isMounted = false;
        };
    }, [today]);

    const selectedPeriod = useMemo(
        () => periods.find((period) => period.id === formData.fiscalPeriodId),
        [periods, formData.fiscalPeriodId]
    );

    const addLine = () => {
        setLines((current) => appendJournalLine(
            current,
            description => createFreshUnitJournalLine(crypto.randomUUID(), description),
        ));
    };

    const removeLine = (id: string) => {
        setLines((current) => current.length > 1 ? current.filter((line) => line.id !== id) : current);
    };

    const updateLine = (id: string, field: keyof EntryLine, value: string) => {
        setLines((current) =>
            current.map((line) => line.id === id ? { ...line, [field]: value } : line)
        );
    };

    const getAccountInfo = (accountId: string) => {
        return accounts.find((acc) => acc.id === accountId);
    };

    const getTotalQuantity = () => {
        return lines.reduce((sum, line) => sum + (Number.parseFloat(line.quantity) || 0), 0);
    };

    const handlePeriodChange = (periodId: string) => {
        const period = periods.find((candidate) => candidate.id === periodId);
        setFormData((current) => ({
            ...current,
            fiscalPeriodId: periodId,
            entryDate: period ? defaultDateForPeriod(period, current.entryDate) : current.entryDate,
        }));
    };

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.fiscalPeriodId) {
            newErrors.fiscalPeriodId = 'Fiscal period is required';
        }

        if (!formData.entryDate) {
            newErrors.entryDate = 'Entry date is required';
        } else if (!isDateInPeriod(formData.entryDate, selectedPeriod)) {
            newErrors.entryDate = 'Entry date must fall within the selected fiscal period';
        }

        const validLines = lines.filter((line) => {
            const quantity = Number.parseFloat(line.quantity);
            return line.unitAccountId && Number.isFinite(quantity) && quantity !== 0;
        });

        if (validLines.length === 0) {
            newErrors.lines = 'At least one line with an account and non-zero quantity is required';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = async (e: React.FormEvent, submitForApproval = false) => {
        e.preventDefault();

        if (!validateForm()) return;

        try {
            setIsSaving(true);
            const created = await unitAccountsDataService.createUnitJournalEntry({
                entryDate: formData.entryDate,
                fiscalPeriodId: formData.fiscalPeriodId,
                description: formData.description.trim() || undefined,
                sourceDocument: formData.sourceDocument.trim() || undefined,
                lines: lines
                    .filter((line) => line.unitAccountId && Number.parseFloat(line.quantity))
                    .map((line) => ({
                        unitAccountId: line.unitAccountId,
                        quantity: Number.parseFloat(line.quantity),
                        description: line.description.trim() || undefined,
                    })),
            });

            if (submitForApproval) {
                await unitAccountsDataService.submitUnitJournalEntry(created.id);
                toast.success(`Unit journal entry ${created.entryNumber} submitted for approval.`);
            } else {
                toast.success(`Unit journal entry ${created.entryNumber} saved as draft.`);
            }

            router.push(`/finance/unit-journal-entries/${created.id}`);
        } catch (error: any) {
            toast.error(error?.message || 'Failed to save unit journal entry.');
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="space-y-6">
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <FileSpreadsheet className="h-8 w-8" />
                    New Unit Journal Entry
                </h1>
                <p className="text-muted-foreground">Record unit quantities to accounts</p>
            </div>

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
                        <BreadcrumbLink href="/finance/unit-journal-entries">Unit Journal Entries</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>New</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <form onSubmit={(e) => handleSubmit(e, false)}>
                <Card className="mb-6">
                    <CardHeader>
                        <CardTitle>Entry Header</CardTitle>
                        <CardDescription>Basic information for the journal entry</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                            <div className="space-y-2">
                                <Label htmlFor="entryId">Entry ID</Label>
                                <Input
                                    id="entryId"
                                    value={entrySequence.sampleNumber}
                                    disabled
                                    className="bg-muted font-mono"
                                />
                                <p className="text-xs text-muted-foreground">Assigned by the configured Unit Journal Entry sequence when saved.</p>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="fiscalPeriod">
                                    Fiscal Period <span className="text-destructive">*</span>
                                </Label>
                                <Select
                                    value={formData.fiscalPeriodId}
                                    onValueChange={handlePeriodChange}
                                    disabled={isLoadingLookups || periods.length === 0}
                                >
                                    <SelectTrigger id="fiscalPeriod" className={errors.fiscalPeriodId ? 'border-destructive' : ''}>
                                        <SelectValue placeholder="Select open period..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {periods.map((period) => (
                                            <SelectItem key={period.id} value={period.id}>
                                                {period.periodCode} - {period.periodName}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.fiscalPeriodId && (
                                    <p className="text-sm text-destructive">{errors.fiscalPeriodId}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="entryDate">
                                    Entry Date <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="entryDate"
                                    type="date"
                                    value={formData.entryDate}
                                    min={selectedPeriod ? inputDate(selectedPeriod.startDate) : undefined}
                                    max={selectedPeriod ? inputDate(selectedPeriod.endDate) : undefined}
                                    onChange={(e) => setFormData({ ...formData, entryDate: e.target.value })}
                                    className={errors.entryDate ? 'border-destructive' : ''}
                                />
                                {errors.entryDate && (
                                    <p className="text-sm text-destructive">{errors.entryDate}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="sourceDocument">Source Document</Label>
                                <Input
                                    id="sourceDocument"
                                    placeholder="e.g., HR Report #123"
                                    value={formData.sourceDocument}
                                    onChange={(e) => setFormData({ ...formData, sourceDocument: e.target.value })}
                                />
                            </div>

                            <div className="space-y-2 md:col-span-2">
                                <Label htmlFor="description">Description</Label>
                                <Input
                                    id="description"
                                    placeholder="Brief description..."
                                    value={formData.description}
                                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                />
                            </div>
                        </div>
                    </CardContent>
                </Card>

                <Card className="mb-6">
                    <CardHeader>
                        <div className="flex justify-between items-center">
                            <div>
                                <CardTitle>Entry Lines</CardTitle>
                                <CardDescription>Add unit accounts and quantities</CardDescription>
                            </div>
                            <Button type="button" variant="outline" onClick={addLine}>
                                <Plus className="mr-2 h-4 w-4" />
                                Add Line
                            </Button>
                        </div>
                    </CardHeader>
                    <CardContent>
                        {errors.lines && (
                            <p className="text-sm text-destructive mb-4">{errors.lines}</p>
                        )}
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead className="w-[50px]">#</TableHead>
                                    <TableHead>Unit Account</TableHead>
                                    <TableHead>Unit Type</TableHead>
                                    <TableHead className="w-[150px]">Quantity</TableHead>
                                    <TableHead>Description</TableHead>
                                    <TableHead className="w-[60px]"></TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {lines.map((line, index) => {
                                    const accountInfo = getAccountInfo(line.unitAccountId);
                                    return (
                                        <TableRow key={line.id}>
                                            <TableCell className="text-muted-foreground">{index + 1}</TableCell>
                                            <TableCell>
                                                <Select
                                                    value={line.unitAccountId}
                                                    onValueChange={(value) => updateLine(line.id, 'unitAccountId', value)}
                                                    disabled={isLoadingLookups || accounts.length === 0}
                                                >
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select account..." />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {accounts.map((acc) => (
                                                            <SelectItem key={acc.id} value={acc.id}>
                                                                {acc.accountNumber} - {acc.name}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                            </TableCell>
                                            <TableCell className="text-muted-foreground">
                                                {accountInfo?.unitTypeName || accountInfo?.unitType?.name || '-'}
                                            </TableCell>
                                            <TableCell>
                                                <Input
                                                    type="number"
                                                    step="any"
                                                    placeholder="0"
                                                    value={line.quantity}
                                                    onChange={(e) => updateLine(line.id, 'quantity', e.target.value)}
                                                    className="text-right"
                                                />
                                            </TableCell>
                                            <TableCell>
                                                <Input
                                                    placeholder="Line description..."
                                                    value={line.description}
                                                    onChange={(e) => updateLine(line.id, 'description', e.target.value)}
                                                />
                                            </TableCell>
                                            <TableCell>
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => removeLine(line.id)}
                                                    disabled={lines.length === 1}
                                                >
                                                    <Trash2 className="h-4 w-4 text-destructive" />
                                                </Button>
                                            </TableCell>
                                        </TableRow>
                                    );
                                })}
                            </TableBody>
                        </Table>

                        <div className="flex justify-end mt-4 pt-4 border-t">
                            <div className="text-right">
                                <p className="text-sm text-muted-foreground">Net Quantity</p>
                                <p className="text-2xl font-bold font-mono">
                                    {getTotalQuantity().toLocaleString(undefined, {
                                        minimumFractionDigits: 0,
                                        maximumFractionDigits: 2,
                                    })}
                                </p>
                            </div>
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-6">
                        <div className="flex justify-between items-center">
                            <Link href="/finance/unit-journal-entries">
                                <Button type="button" variant="outline">
                                    <X className="mr-2 h-4 w-4" />
                                    Cancel
                                </Button>
                            </Link>
                            <div className="flex gap-2">
                                <Button type="submit" variant="secondary" disabled={isSaving || isLoadingLookups}>
                                    <Save className="mr-2 h-4 w-4" />
                                    Save as Draft
                                </Button>
                                <Button
                                    type="button"
                                    onClick={(e) => handleSubmit(e, true)}
                                    disabled={isSaving || isLoadingLookups}
                                >
                                    Submit for Approval
                                </Button>
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
