'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import { FileSpreadsheet, Save, X, Plus, Trash2 } from 'lucide-react';
import Link from 'next/link';
import type { UnitAccount, UnitType } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_UNIT_TYPES: UnitType[] = [
    { id: 'ut-1', code: 'EMP', name: 'Employees', decimalPlaces: 0, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-2', code: 'SQFT', name: 'Square Footage', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-3', code: 'HRS', name: 'Hours', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
];

const MOCK_ACCOUNTS: UnitAccount[] = [
    { id: 'ua-2', accountNumber: 'U-1100', name: 'Operations Department', unitTypeId: 'ut-1', unitType: MOCK_UNIT_TYPES[0], accountLevel: 2, isPostingAccount: true, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ua-3', accountNumber: 'U-1200', name: 'Sales Department', unitTypeId: 'ut-1', unitType: MOCK_UNIT_TYPES[0], accountLevel: 2, isPostingAccount: true, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ua-4', accountNumber: 'U-1300', name: 'Admin Department', unitTypeId: 'ut-1', unitType: MOCK_UNIT_TYPES[0], accountLevel: 2, isPostingAccount: true, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ua-6', accountNumber: 'U-2100', name: 'Head Office', unitTypeId: 'ut-2', unitType: MOCK_UNIT_TYPES[1], accountLevel: 2, isPostingAccount: true, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ua-7', accountNumber: 'U-2200', name: 'Branch Offices', unitTypeId: 'ut-2', unitType: MOCK_UNIT_TYPES[1], accountLevel: 2, isPostingAccount: true, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ua-8', accountNumber: 'U-3000', name: 'Machine Hours', unitTypeId: 'ut-3', unitType: MOCK_UNIT_TYPES[2], accountLevel: 1, isPostingAccount: true, isActive: true, createdAt: '', createdBy: '' },
];

interface EntryLine {
    id: string;
    unitAccountId: string;
    quantity: string;
    description: string;
}

export default function NewUnitJournalEntryPage() {
    const router = useRouter();
    const [formData, setFormData] = useState({
        entryDate: new Date().toISOString().split('T')[0],
        description: '',
        sourceDocument: '',
    });
    const [lines, setLines] = useState<EntryLine[]>([
        { id: '1', unitAccountId: '', quantity: '', description: '' },
    ]);
    const [errors, setErrors] = useState<Record<string, string>>({});

    const addLine = () => {
        setLines([
            ...lines,
            { id: Date.now().toString(), unitAccountId: '', quantity: '', description: '' },
        ]);
    };

    const removeLine = (id: string) => {
        if (lines.length > 1) {
            setLines(lines.filter((line) => line.id !== id));
        }
    };

    const updateLine = (id: string, field: keyof EntryLine, value: string) => {
        setLines(lines.map((line) =>
            line.id === id ? { ...line, [field]: value } : line
        ));
    };

    const getAccountInfo = (accountId: string) => {
        return MOCK_ACCOUNTS.find((acc) => acc.id === accountId);
    };

    const getTotalQuantity = () => {
        return lines.reduce((sum, line) => sum + (parseFloat(line.quantity) || 0), 0);
    };

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.entryDate) {
            newErrors.entryDate = 'Entry date is required';
        }

        const hasValidLine = lines.some((line) => line.unitAccountId && line.quantity);
        if (!hasValidLine) {
            newErrors.lines = 'At least one line with account and quantity is required';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = (e: React.FormEvent, saveAsDraft: boolean = true) => {
        e.preventDefault();

        if (!validateForm()) {
            return;
        }

        const data = {
            ...formData,
            status: saveAsDraft ? 'Draft' : 'PendingApproval',
            lines: lines.filter((line) => line.unitAccountId && line.quantity).map((line) => ({
                unitAccountId: line.unitAccountId,
                quantity: parseFloat(line.quantity),
                description: line.description,
            })),
        };

        console.log('Creating unit journal entry:', data);
        alert(`DEMO MODE: Entry would be ${saveAsDraft ? 'saved as draft' : 'submitted for approval'}. Check console.`);
        router.push('/finance/unit-journal-entries');
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <FileSpreadsheet className="h-8 w-8" />
                    New Unit Journal Entry
                </h1>
                <p className="text-muted-foreground">
                    Record unit quantities to accounts
                </p>
                <p className="text-sm text-orange-600 mt-1">
                    ⚠️ DEMO FRONTEND UI
                </p>
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
                        <BreadcrumbLink href="/finance/unit-journal-entries">Unit Journal Entries</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>New</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <form onSubmit={(e) => handleSubmit(e, true)}>
                {/* Header Card */}
                <Card className="mb-6">
                    <CardHeader>
                        <CardTitle>Entry Header</CardTitle>
                        <CardDescription>
                            Basic information for the journal entry
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                            {/* Entry Date */}
                            <div className="space-y-2">
                                <Label htmlFor="entryDate">
                                    Entry Date <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="entryDate"
                                    type="date"
                                    value={formData.entryDate}
                                    onChange={(e) => setFormData({ ...formData, entryDate: e.target.value })}
                                    className={errors.entryDate ? 'border-destructive' : ''}
                                />
                                {errors.entryDate && (
                                    <p className="text-sm text-destructive">{errors.entryDate}</p>
                                )}
                            </div>

                            {/* Source Document */}
                            <div className="space-y-2">
                                <Label htmlFor="sourceDocument">Source Document</Label>
                                <Input
                                    id="sourceDocument"
                                    placeholder="e.g., HR Report #123"
                                    value={formData.sourceDocument}
                                    onChange={(e) => setFormData({ ...formData, sourceDocument: e.target.value })}
                                />
                            </div>

                            {/* Description */}
                            <div className="space-y-2 md:col-span-1">
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

                {/* Lines Card */}
                <Card className="mb-6">
                    <CardHeader>
                        <div className="flex justify-between items-center">
                            <div>
                                <CardTitle>Entry Lines</CardTitle>
                                <CardDescription>
                                    Add unit accounts and quantities
                                </CardDescription>
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
                                            <TableCell className="text-muted-foreground">
                                                {index + 1}
                                            </TableCell>
                                            <TableCell>
                                                <Select
                                                    value={line.unitAccountId}
                                                    onValueChange={(value) => updateLine(line.id, 'unitAccountId', value)}
                                                >
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select account..." />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {MOCK_ACCOUNTS.map((acc) => (
                                                            <SelectItem key={acc.id} value={acc.id}>
                                                                {acc.accountNumber} - {acc.name}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                            </TableCell>
                                            <TableCell className="text-muted-foreground">
                                                {accountInfo?.unitType?.name || '-'}
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

                        {/* Totals */}
                        <div className="flex justify-end mt-4 pt-4 border-t">
                            <div className="text-right">
                                <p className="text-sm text-muted-foreground">Total Quantity</p>
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

                {/* Actions */}
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
                                <Button type="submit" variant="secondary">
                                    <Save className="mr-2 h-4 w-4" />
                                    Save as Draft
                                </Button>
                                <Button
                                    type="button"
                                    onClick={(e) => handleSubmit(e, false)}
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
