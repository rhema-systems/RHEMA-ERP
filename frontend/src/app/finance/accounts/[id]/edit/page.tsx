'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Save, Loader2, Check, ChevronsUpDown } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { AccountType, AccountStatus, Account, CashFlowClassification, UpdateAccountDto } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Trash2 } from 'lucide-react';

// IFRS/IAS/GAAP Standard Account Types
const DETAILED_ACCOUNT_TYPES: { group: string; types: { label: string; value: string; type: AccountType }[] }[] = [
    {
        group: 'Assets',
        types: [
            { label: 'Cash and Cash Equivalents', value: 'Cash and Cash Equivalents', type: 'Asset' },
            { label: 'Short-Term Investments', value: 'Short-Term Investments', type: 'Asset' },
            { label: 'Accounts Receivable (Trade Debtors)', value: 'Accounts Receivable', type: 'Asset' },
            { label: 'Inventory (Stock)', value: 'Inventory', type: 'Asset' },
            { label: 'Prepaid Expenses', value: 'Prepaid Expenses', type: 'Asset' },
            { label: 'Property, Plant & Equipment (PPE)', value: 'Property, Plant & Equipment', type: 'Asset' },
            { label: 'Intangible Assets', value: 'Intangible Assets', type: 'Asset' },
            { label: 'Goodwill', value: 'Goodwill', type: 'Asset' },
            { label: 'Long-Term Investments', value: 'Long-Term Investments', type: 'Asset' },
            { label: 'Deferred Tax Assets', value: 'Deferred Tax Assets', type: 'Asset' },
            { label: 'Other Assets', value: 'Other Assets', type: 'Asset' },
        ]
    },
    {
        group: 'Liabilities',
        types: [
            { label: 'Accounts Payable (Trade Creditors)', value: 'Accounts Payable', type: 'Liability' },
            { label: 'Accrued Liabilities', value: 'Accrued Liabilities', type: 'Liability' },
            { label: 'Short-Term Debt / Bank Overdrafts', value: 'Short-Term Debt', type: 'Liability' },
            { label: 'Deferred Revenue (Unearned Income)', value: 'Deferred Revenue', type: 'Liability' },
            { label: 'Tax Payable', value: 'Tax Payable', type: 'Liability' },
            { label: 'Long-Term Debt', value: 'Long-Term Debt', type: 'Liability' },
            { label: 'Lease Liabilities', value: 'Lease Liabilities', type: 'Liability' },
            { label: 'Provisions', value: 'Provisions', type: 'Liability' },
            { label: 'Deferred Tax Liabilities', value: 'Deferred Tax Liabilities', type: 'Liability' },
            { label: 'Other Liabilities', value: 'Other Liabilities', type: 'Liability' },
        ]
    },
    {
        group: 'Equity',
        types: [
            { label: 'Share Capital (Common/Preferred)', value: 'Share Capital', type: 'Equity' },
            { label: 'Retained Earnings', value: 'Retained Earnings', type: 'Equity' },
            { label: 'Additional Paid-In Capital', value: 'Additional Paid-In Capital', type: 'Equity' },
            { label: 'Revaluation Surplus', value: 'Revaluation Surplus', type: 'Equity' },
            { label: 'Other Comprehensive Income', value: 'Other Comprehensive Income', type: 'Equity' },
            { label: 'Dividends Declared', value: 'Dividends Declared', type: 'Equity' },
        ]
    },
    {
        group: 'Revenue',
        types: [
            { label: 'Operating Revenue (Sales)', value: 'Operating Revenue', type: 'Revenue' },
            { label: 'Service Revenue', value: 'Service Revenue', type: 'Revenue' },
            { label: 'Interest Income', value: 'Interest Income', type: 'Revenue' },
            { label: 'Dividend Income', value: 'Dividend Income', type: 'Revenue' },
            { label: 'Rental Income', value: 'Rental Income', type: 'Revenue' },
            { label: 'Other Income', value: 'Other Income', type: 'Revenue' },
        ]
    },
    {
        group: 'Expenses',
        types: [
            { label: 'Cost of Goods Sold (COGS)', value: 'Cost of Goods Sold', type: 'Expense' },
            { label: 'Selling, General & Admin (SG&A)', value: 'Operating Expense', type: 'Expense' },
            { label: 'Personnel / Payroll Expenses', value: 'Personnel Expense', type: 'Expense' },
            { label: 'Rent & Utilities', value: 'Rent and Utilities', type: 'Expense' },
            { label: 'Depreciation & Amortization', value: 'Depreciation and Amortization', type: 'Expense' },
            { label: 'Finance Costs (Interest Expense)', value: 'Interest Expense', type: 'Expense' },
            { label: 'Income Tax Expense', value: 'Tax Expense', type: 'Expense' },
            { label: 'Other Expenses', value: 'Other Expenses', type: 'Expense' },
        ]
    }
];

export default function EditAccountPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = React.use(params);
    const router = useRouter();
    const { toast } = useToast();

    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [formData, setFormData] = useState<Account | null>(null);
    const [transactionError, setTransactionError] = useState<string | null>(null);

    // Combobox state
    const [openAccountType, setOpenAccountType] = useState(false);

    // Load account data
    useEffect(() => {
        const loadAccount = async () => {
            try {
                setLoading(true);
                const accountData = await financeDataService.getAccountById(id);
                setFormData({
                    ...accountData,
                    allowDirectPosting: accountData.allowDirectPosting ?? accountData.isPostingAllowed ?? true,
                    isBaseClassified: accountData.isBaseClassified ?? accountData.isBaseFrameworkClassified ?? accountData.isManagementClassified ?? true,
                    isLocalClassified: accountData.isLocalClassified ?? accountData.isLocalFrameworkClassified ?? true,
                    budgetTrackingEnabled: accountData.budgetTrackingEnabled ?? false,
                });
            } catch (error) {
                console.error('Error loading account:', error);
                toast({
                    title: 'Error',
                    description: 'Failed to load account details',
                    variant: 'destructive',
                });
            } finally {
                setLoading(false);
            }
        };
        loadAccount();
    }, [id, toast]);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!formData) return;

        try {
            setSaving(true);
            setTransactionError(null);

            const selectedClassification = formData.accountSubCategory || formData.accountCategory;
            const updateDto: UpdateAccountDto = {
                id: formData.id,
                accountCode: formData.accountCode,
                accountNumber: formData.accountNumber,
                accountName: formData.accountName,
                accountType: formData.accountType,
                accountCategory: selectedClassification,
                accountSubCategory: selectedClassification,
                cashFlowClassification: formData.cashFlowClassification || null,
                currencyCode: formData.currencyCode,
                isMultiCurrency: formData.isMultiCurrency,
                isIFRSClassified: formData.isIFRSClassified,
                isManagementClassified: formData.isBaseClassified ?? formData.isBaseFrameworkClassified ?? true,
                isBaseFrameworkClassified: formData.isBaseClassified ?? formData.isBaseFrameworkClassified ?? true,
                isLocalFrameworkClassified: formData.isLocalClassified ?? formData.isLocalFrameworkClassified ?? true,
                isPostingAllowed: formData.allowDirectPosting ?? formData.isPostingAllowed ?? true,
                isControlAccount: formData.isControlAccount,
                budgetTrackingEnabled: formData.budgetTrackingEnabled,
                status: formData.status,
                isSegmented: formData.isSegmented,
            };

            await financeDataService.updateAccount(id, updateDto);

            toast({
                title: 'Success',
                description: 'Account updated successfully',
            });

            router.push('/finance/accounts');
        } catch (error: any) {
            console.error('Error updating account:', error);
            const errorMessage = error.body?.error || error.message || 'Failed to update account';

            // specific check for the transaction validation error we added
            if (errorMessage.includes("transactions already exist")) {
                setTransactionError("Cannot change Account Classification because this account has existing transactions. Please reverse them first to verify.");
            }

            toast({
                title: 'Error',
                description: errorMessage,
                variant: 'destructive',
            });
        } finally {
            setSaving(false);
        }
    };


    const handleDelete = async () => {
        try {
            setSaving(true);
            await financeDataService.deleteAccount(id);
            toast({
                title: 'Success',
                description: 'Account deleted successfully',
            });
            router.push('/finance/accounts');
        } catch (error: any) {
            console.error('Error deleting account:', error);
            const errorMessage = error.body?.error || error.message || 'Failed to delete account';
            toast({
                title: 'Error',
                description: errorMessage,
                variant: 'destructive',
            });
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!formData) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <p className="text-muted-foreground">Account not found</p>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        Edit Account: {formData.accountCode}
                    </h1>
                    <p className="text-muted-foreground">
                        Update account details
                    </p>
                </div>
                <Button variant="outline" onClick={() => router.back()}>
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    Back
                </Button>
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
                        <BreadcrumbLink href="/finance/accounts">Chart of Accounts</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href={`/finance/accounts/${id}`}>{formData.accountCode}</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Edit</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {transactionError && (
                <Alert variant="destructive">
                    <AlertDescription>{transactionError}</AlertDescription>
                </Alert>
            )}

            {/* Form */}
            <form onSubmit={handleSubmit}>
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                    {/* Main Form */}
                    <div className="lg:col-span-2 space-y-6">
                        {/* Basic Information */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Basic Information</CardTitle>
                                <CardDescription>Account identification and classification</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label htmlFor="accountCode">Account Code *</Label>
                                        <Input
                                            id="accountCode"
                                            value={formData.accountCode}
                                            onChange={(e) => setFormData({ ...formData, accountCode: e.target.value })}
                                            required
                                            disabled // Code usually invariant
                                        />
                                        <p className="text-xs text-muted-foreground">Account code cannot be changed</p>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="accountNumber">Account Number *</Label>
                                        <Input
                                            id="accountNumber"
                                            value={formData.accountNumber}
                                            onChange={(e) => setFormData({ ...formData, accountNumber: e.target.value })}
                                            required
                                            disabled // Number derived from segments, usually invariant in edit unless re-segmenting
                                        />
                                        <p className="text-xs text-muted-foreground">Account number cannot be changed directly</p>
                                    </div>
                                </div>

                                {/* Segment Details (Read Only) */}
                                {formData.isSegmented && formData.segmentValues && formData.segmentValues.length > 0 && (
                                    <div className="space-y-3 p-3 bg-muted/50 rounded-md border">
                                        <Label className="text-xs font-semibold uppercase text-muted-foreground">Account Segments</Label>
                                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                                            {formData.segmentValues
                                                .sort((a, b) => a.segmentPosition - b.segmentPosition)
                                                .map((seg) => (
                                                    <div key={seg.id} className="space-y-1">
                                                        <Label className="text-xs text-muted-foreground">
                                                            Segment {seg.segmentPosition}
                                                        </Label>
                                                        <div className="font-medium font-mono bg-background border px-2 py-1 rounded">
                                                            {seg.segmentValue}
                                                        </div>
                                                        {seg.segmentDescription && (
                                                            <p className="text-[10px] text-muted-foreground truncate" title={seg.segmentDescription}>
                                                                {seg.segmentDescription}
                                                            </p>
                                                        )}
                                                    </div>
                                                ))}
                                        </div>
                                        <p className="text-[10px] text-muted-foreground">
                                            Segments define the structure of the account code. To change these, create a new account.
                                        </p>
                                    </div>
                                )}

                                <div className="space-y-2">
                                    <Label htmlFor="accountName">Account Name *</Label>
                                    <Input
                                        id="accountName"
                                        value={formData.accountName}
                                        onChange={(e) => setFormData({ ...formData, accountName: e.target.value })}
                                        required
                                    />
                                </div>

                                {/* IFRS Classification */}
                                <div className="space-y-2">
                                    <Label htmlFor="accountType">Account Classification (IFRS/GAAP)</Label>
                                    <Popover open={openAccountType} onOpenChange={setOpenAccountType}>
                                        <PopoverTrigger asChild>
                                            <Button
                                                variant="outline"
                                                role="combobox"
                                                aria-expanded={openAccountType}
                                                className="w-full justify-between font-normal"
                                            >
                                                {/* Logic: Try to find label by value. If not found, show value. If no value, show placeholder */}
                                                {formData.accountSubCategory
                                                    ? DETAILED_ACCOUNT_TYPES.flatMap(g => g.types).find(t => t.value === formData.accountSubCategory)?.label || formData.accountSubCategory
                                                    : "Select detailed account type..."}
                                                <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                            </Button>
                                        </PopoverTrigger>
                                        <PopoverContent className="w-[400px] p-0" align="start">
                                            <Command>
                                                <CommandInput placeholder="Search account types..." />
                                                <CommandList className="max-h-[300px] overflow-y-auto">
                                                    <CommandEmpty>No account type found.</CommandEmpty>
                                                    {DETAILED_ACCOUNT_TYPES.map((group) => (
                                                        <CommandGroup key={group.group} heading={group.group}>
                                                            {group.types.map((type) => (
                                                                <CommandItem
                                                                    key={type.value}
                                                                    value={type.label}
                                                                    onSelect={() => {
                                                                        setFormData({
                                                                            ...formData,
                                                                            accountType: type.type,
                                                                            accountSubCategory: type.value,
                                                                        });
                                                                        setOpenAccountType(false);
                                                                    }}
                                                                >
                                                                    <Check
                                                                        className={cn(
                                                                            "mr-2 h-4 w-4",
                                                                            formData.accountSubCategory === type.value ? "opacity-100" : "opacity-0"
                                                                        )}
                                                                    />
                                                                    {type.label}
                                                                </CommandItem>
                                                            ))}
                                                        </CommandGroup>
                                                    ))}
                                                </CommandList>
                                            </Command>
                                        </PopoverContent>
                                    </Popover>
                                    <div className="text-xs text-muted-foreground mt-1 flex items-center gap-2">
                                        <span>Core Type: <Badge variant="outline">{formData.accountType}</Badge></span>
                                        {!formData.accountSubCategory && (
                                            <span className="text-amber-600 flex items-center gap-1">
                                                (Not Classified)
                                            </span>
                                        )}
                                    </div>
                                </div>

                                <div className="space-y-2">
                                    <Label htmlFor="description">Description</Label>
                                    <Textarea
                                        id="description"
                                        value={formData.description || ''}
                                        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                        rows={3}
                                    />
                                </div>
                            </CardContent>
                        </Card>

                        {/* Currency Settings */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Currency Settings</CardTitle>
                                <CardDescription>Configure currency handling for this account</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="space-y-2">
                                    <Label htmlFor="currencyCode">Primary Currency *</Label>
                                    <Select
                                        value={formData.currencyCode}
                                        onValueChange={(value) => setFormData({ ...formData, currencyCode: value })}
                                        disabled
                                    >
                                        <SelectTrigger id="currencyCode">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                            <SelectItem value="USD">USD - US Dollar</SelectItem>
                                            <SelectItem value="EUR">EUR - Euro</SelectItem>
                                            <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                        </SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">Currency cannot be changed after creation</p>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isMultiCurrency"
                                        checked={formData.isMultiCurrency}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isMultiCurrency: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isMultiCurrency" className="cursor-pointer">
                                        Enable multi-currency transactions
                                    </Label>
                                </div>
                            </CardContent>
                        </Card>
                    </div>

                    {/* Sidebar */}
                    <div className="space-y-6">
                        {/* Account Features */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Account Features</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <div className="space-y-2 border-b pb-4">
                                    <Label htmlFor="cashFlowClassification">Cash-flow statement section</Label>
                                    <Select
                                        value={formData.cashFlowClassification || 'Unclassified'}
                                        onValueChange={(value) => setFormData({
                                            ...formData,
                                            cashFlowClassification: value === 'Unclassified'
                                                ? null
                                                : value as CashFlowClassification,
                                        })}
                                    >
                                        <SelectTrigger id="cashFlowClassification">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Unclassified">Not classified</SelectItem>
                                            <SelectItem value="Operating">Operating</SelectItem>
                                            <SelectItem value="Investing">Investing</SelectItem>
                                            <SelectItem value="Financing">Financing</SelectItem>
                                        </SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">
                                        Required when this account is the non-cash counterpart of a cash or bank posting.
                                    </p>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="allowDirectPosting"
                                        checked={formData.allowDirectPosting}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, allowDirectPosting: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="allowDirectPosting" className="cursor-pointer text-sm">
                                        Allow direct posting
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isControlAccount"
                                        checked={formData.isControlAccount}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isControlAccount: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isControlAccount" className="cursor-pointer text-sm">
                                        Control account
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="budgetTrackingEnabled"
                                        checked={formData.budgetTrackingEnabled}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, budgetTrackingEnabled: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="budgetTrackingEnabled" className="cursor-pointer text-sm">
                                        Enable budget tracking
                                    </Label>
                                </div>
                            </CardContent>
                        </Card>

                        {/* Classification Flags */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Reporting Flags</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isIFRSClassified"
                                        checked={formData.isIFRSClassified}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isIFRSClassified: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isIFRSClassified" className="cursor-pointer text-sm">
                                        IFRS Classification
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isBaseClassified"
                                        checked={formData.isBaseClassified}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isBaseClassified: checked as boolean })
                                        } // Renamed prop usage check
                                    />
                                    <Label htmlFor="isBaseClassified" className="cursor-pointer text-sm">
                                        Base Classification
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isLocalClassified"
                                        checked={formData.isLocalClassified}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isLocalClassified: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isLocalClassified" className="cursor-pointer text-sm">
                                        Local Classification
                                    </Label>
                                </div>
                            </CardContent>
                        </Card>

                        {/* Status */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Status</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <Select
                                    value={formData.status}
                                    onValueChange={(value: AccountStatus) => setFormData({ ...formData, status: value })}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Active">Active</SelectItem>
                                        <SelectItem value="Inactive">Inactive</SelectItem>
                                        <SelectItem value="Closed">Closed</SelectItem>
                                    </SelectContent>
                                </Select>
                            </CardContent>
                        </Card>

                        {/* Actions */}
                        <div className="space-y-2">
                            <Button type="submit" className="w-full" disabled={saving}>
                                {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                                Save Changes
                            </Button>
                            <Button type="button" variant="outline" className="w-full" onClick={() => router.back()}>
                                Cancel
                            </Button>
                        </div>

                        {/* Delete Action */}
                        <Card className="border-red-200">
                            <CardHeader className="pb-3">
                                <CardTitle className="text-red-600 text-base">Danger Zone</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <AlertDialog>
                                    <AlertDialogTrigger asChild>
                                        <Button variant="destructive" className="w-full" type="button" disabled={saving}>
                                            <Trash2 className="mr-2 h-4 w-4" />
                                            Delete Account
                                        </Button>
                                    </AlertDialogTrigger>
                                    <AlertDialogContent>
                                        <AlertDialogHeader>
                                            <AlertDialogTitle>Are you absolutely sure?</AlertDialogTitle>
                                            <AlertDialogDescription>
                                                This action cannot be undone. This will permanently delete the account
                                                and remove it from the system. If the account has existing transactions,
                                                deletion will be blocked.
                                            </AlertDialogDescription>
                                        </AlertDialogHeader>
                                        <AlertDialogFooter>
                                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                                            <AlertDialogAction onClick={handleDelete} className="bg-red-600 hover:bg-red-700">
                                                Delete
                                            </AlertDialogAction>
                                        </AlertDialogFooter>
                                    </AlertDialogContent>
                                </AlertDialog>
                            </CardContent>
                        </Card>
                    </div>
                </div>
            </form>
        </div>
    );
}
