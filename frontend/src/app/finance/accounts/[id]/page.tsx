'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Edit, Trash2, DollarSign, Loader2, Plus, Link2, X, RefreshCcw, AlertTriangle } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { Account, AccountType, AccountStatus, AccountCurrencyLink, Currency, AddCurrencyLinkDto, ExchangeRateQuoteSide, UpdateCurrencyLinkRatePolicyDto } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';

export default function AccountDetailPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = React.use(params);
    const router = useRouter();
    const { toast } = useToast();

    const [account, setAccount] = useState<Account | null>(null);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [currencyLinks, setCurrencyLinks] = useState<AccountCurrencyLink[]>([]);
    const [loading, setLoading] = useState(true);
    const [addDialogOpen, setAddDialogOpen] = useState(false);
    const [addingLink, setAddingLink] = useState(false);
    const [removingLinkId, setRemovingLinkId] = useState<string | null>(null);
    const [editingLink, setEditingLink] = useState<AccountCurrencyLink | null>(null);
    const [savingPolicy, setSavingPolicy] = useState(false);
    const [policyForm, setPolicyForm] = useState<UpdateCurrencyLinkRatePolicyDto | null>(null);

    // New link form data
    const [newLink, setNewLink] = useState<Partial<AddCurrencyLinkDto>>({
        linkedCurrencyCode: '',
        revaluationRequired: true,
        revaluationFrequency: 'Monthly',
        transactionRateType: 'Daily',
        transactionQuoteSide: 'Mid',
        revaluationRateType: 'Month-End',
        revaluationQuoteSide: 'Mid',
        notes: '',
    });

    // Load account data
    useEffect(() => {
        const loadData = async () => {
            try {
                setLoading(true);

                // Load account, currencies, and currency links in parallel
                const [accountData, currenciesData] = await Promise.all([
                    financeDataService.getAccountById(id),
                    financeDataService.getCurrencies(),
                ]);

                setAccount(accountData);
                setCurrencies(currenciesData);

                // Load currency links if multi-currency enabled
                if (accountData.isMultiCurrency) {
                    const links = await financeDataService.getAccountCurrencyLinks(id);
                    setCurrencyLinks(links);
                }
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
        loadData();
    }, [id, toast]);

    const getAccountTypeBadge = (type: AccountType) => {
        const colors: Record<AccountType, string> = {
            Asset: 'bg-blue-500',
            Liability: 'bg-red-500',
            Equity: 'bg-purple-500',
            Revenue: 'bg-green-500',
            Expense: 'bg-orange-500',
        };
        return <Badge className={colors[type]}>{type}</Badge>;
    };

    const getStatusBadge = (status: AccountStatus) => {
        const variants: Record<AccountStatus, 'default' | 'secondary' | 'destructive'> = {
            Active: 'default',
            Inactive: 'secondary',
            Closed: 'destructive',
        };
        return <Badge variant={variants[status]}>{status}</Badge>;
    };

    const formatCurrency = (amount: number, currencyCode: string = 'GHS') => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: currencyCode,
            minimumFractionDigits: 2,
        }).format(amount);
    };

    const getAvailableCurrencies = () => {
        const linkedCodes = currencyLinks.map(l => l.linkedCurrencyCode);
        return currencies.filter(c =>
            c.currencyCode !== account?.currencyCode && // Not the primary currency
            !linkedCodes.includes(c.currencyCode) && // Not already linked
            c.isActive // Must be active
        );
    };

    const handleAddCurrencyLink = async () => {
        if (!newLink.linkedCurrencyCode) {
            toast({
                title: 'Validation Error',
                description: 'Please select a currency',
                variant: 'destructive',
            });
            return;
        }

        try {
            setAddingLink(true);

            const addedLink = await financeDataService.addAccountCurrencyLink(id, newLink as AddCurrencyLinkDto);
            setCurrencyLinks([...currencyLinks, addedLink]);

            toast({
                title: 'Success',
                description: `Currency link for ${newLink.linkedCurrencyCode} added successfully`,
            });

            setAddDialogOpen(false);
            setNewLink({
                linkedCurrencyCode: '',
                revaluationRequired: true,
                revaluationFrequency: 'Monthly',
                transactionRateType: 'Daily',
                transactionQuoteSide: 'Mid',
                revaluationRateType: 'Month-End',
                revaluationQuoteSide: 'Mid',
                notes: '',
            });
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error?.message || 'Failed to add currency link',
                variant: 'destructive',
            });
        } finally {
            setAddingLink(false);
        }
    };

    const handleRemoveCurrencyLink = async (link: AccountCurrencyLink) => {
        try {
            setRemovingLinkId(link.id);

            await financeDataService.removeAccountCurrencyLink(id, link.linkedCurrencyCode);
            setCurrencyLinks(currencyLinks.filter(l => l.id !== link.id));

            toast({
                title: 'Success',
                description: `Currency link for ${link.linkedCurrencyCode} removed`,
            });
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error?.message || 'Failed to remove currency link',
                variant: 'destructive',
            });
        } finally {
            setRemovingLinkId(null);
        }
    };

    const openRatePolicyDialog = (link: AccountCurrencyLink) => {
        setEditingLink(link);
        setPolicyForm({
            revaluationRequired: link.revaluationRequired,
            revaluationFrequency: link.revaluationFrequency,
            transactionRateType: link.transactionRateType,
            transactionQuoteSide: link.transactionQuoteSide || 'Mid',
            revaluationRateType: link.revaluationRateType,
            revaluationQuoteSide: link.revaluationQuoteSide || 'Mid',
            notes: link.notes,
        });
    };

    const handleSaveRatePolicy = async () => {
        if (!editingLink || !policyForm) return;
        try {
            setSavingPolicy(true);
            const updated = await financeDataService.updateAccountCurrencyLinkRatePolicy(
                id,
                editingLink.linkedCurrencyCode,
                policyForm
            );
            setCurrencyLinks(current => current.map(link => link.id === updated.id ? updated : link));
            setEditingLink(null);
            setPolicyForm(null);
            toast({ title: 'Success', description: `${updated.linkedCurrencyCode} rate policy updated` });
        } catch (error: any) {
            toast({ title: 'Error', description: error?.message || 'Failed to update rate policy', variant: 'destructive' });
        } finally {
            setSavingPolicy(false);
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!account) {
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
                        {account.accountCode} - {account.accountName}
                    </h1>
                    <p className="text-muted-foreground">
                        Account details and transaction history
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Back
                    </Button>
                    <Button onClick={() => router.push(`/finance/accounts/${id}/edit`)}>
                        <Edit className="mr-2 h-4 w-4" />
                        Edit
                    </Button>
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
                        <BreadcrumbLink href="/finance/accounts">Chart of Accounts</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{account.accountCode}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Main Content */}
                <div className="lg:col-span-2 space-y-6">
                    {/* Account Summary */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Account Information</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Code</p>
                                    <p className="font-mono font-semibold">{account.accountCode}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Number</p>
                                    <p className="font-mono font-semibold">{account.accountNumber}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Name</p>
                                    <p className="font-semibold">{account.accountName}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Type</p>
                                    <div className="mt-1">{getAccountTypeBadge(account.accountType)}</div>
                                </div>
                                {account.description && (
                                    <div className="col-span-2">
                                        <p className="text-sm text-muted-foreground">Description</p>
                                        <p>{account.description}</p>
                                    </div>
                                )}
                            </div>
                        </CardContent>
                    </Card>

                    {/* Balance */}
                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <DollarSign className="h-5 w-5" />
                                Current Balance
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-3xl font-bold font-mono">
                                {account.currentBalance !== undefined ? formatCurrency(account.currentBalance, account.currencyCode) : 'N/A'}
                            </div>
                            <p className="text-sm text-muted-foreground mt-1">
                                As of {new Date().toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}
                            </p>
                        </CardContent>
                    </Card>

                    {/* Currency Links Management - Only show if multi-currency is enabled */}
                    {account.isMultiCurrency && (
                        <Card className="border-blue-200">
                            <CardHeader className="bg-gradient-to-r from-blue-50 to-indigo-50 rounded-t-lg">
                                <div className="flex items-center justify-between">
                                    <div>
                                        <CardTitle className="flex items-center gap-2">
                                            <Link2 className="h-5 w-5 text-blue-600" />
                                            Currency Links
                                        </CardTitle>
                                        <CardDescription>
                                            Manage currencies linked to this multi-currency account
                                        </CardDescription>
                                    </div>
                                    <Dialog open={addDialogOpen} onOpenChange={setAddDialogOpen}>
                                        <DialogTrigger asChild>
                                            <Button size="sm" disabled={getAvailableCurrencies().length === 0}>
                                                <Plus className="mr-2 h-4 w-4" />
                                                Add Currency
                                            </Button>
                                        </DialogTrigger>
                                        <DialogContent>
                                            <DialogHeader>
                                                <DialogTitle>Add Currency Link</DialogTitle>
                                                <DialogDescription>
                                                    Link an additional currency to this account for multi-currency transactions
                                                </DialogDescription>
                                            </DialogHeader>
                                            <div className="space-y-4 py-4">
                                                <div className="space-y-2">
                                                    <Label>Currency *</Label>
                                                    <Select
                                                        value={newLink.linkedCurrencyCode}
                                                        onValueChange={(v) => setNewLink({ ...newLink, linkedCurrencyCode: v })}
                                                    >
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="Select currency..." />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            {getAvailableCurrencies().map((c) => (
                                                                <SelectItem key={c.id} value={c.currencyCode}>
                                                                    {c.currencyCode} - {c.currencyName}
                                                                </SelectItem>
                                                            ))}
                                                        </SelectContent>
                                                    </Select>
                                                </div>

                                                <div className="flex items-center space-x-2">
                                                    <Checkbox
                                                        id="revaluationRequired"
                                                        checked={newLink.revaluationRequired}
                                                        onCheckedChange={(checked) =>
                                                            setNewLink({ ...newLink, revaluationRequired: checked as boolean })
                                                        }
                                                    />
                                                    <Label htmlFor="revaluationRequired" className="cursor-pointer">
                                                        Monetary item — include in closing revaluation
                                                    </Label>
                                                </div>
                                                <p className="text-xs text-muted-foreground">
                                                    Enable for cash, receivables, payables, loans and other balances settled in a fixed amount of this currency. Leave off for non-monetary items such as historical-cost inventory, fixed assets and prepayments.
                                                </p>

                                                {newLink.revaluationRequired && (
                                                    <div className="space-y-2">
                                                        <Label>Revaluation Frequency</Label>
                                                        <Select
                                                            value={newLink.revaluationFrequency}
                                                            onValueChange={(v) => setNewLink({ ...newLink, revaluationFrequency: v })}
                                                        >
                                                            <SelectTrigger>
                                                                <SelectValue />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="Monthly">Monthly</SelectItem>
                                                                <SelectItem value="Quarterly">Quarterly</SelectItem>
                                                                <SelectItem value="Annually">Annually</SelectItem>
                                                                <SelectItem value="AdHoc">Ad hoc</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </div>
                                                )}

                                                <div className="grid grid-cols-2 gap-4">
                                                    <div className="space-y-2">
                                                        <Label>Transaction Rate Type</Label>
                                                        <Select
                                                            value={newLink.transactionRateType}
                                                            onValueChange={(v) => setNewLink({ ...newLink, transactionRateType: v })}
                                                        >
                                                            <SelectTrigger>
                                                                <SelectValue />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="Daily">Daily</SelectItem>
                                                                <SelectItem value="Fixed">Fixed contractual</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </div>
                                                    <div className="space-y-2">
                                                        <Label>Revaluation Rate Type</Label>
                                                        <Select
                                                            value={newLink.revaluationRateType}
                                                            onValueChange={(v) => setNewLink({ ...newLink, revaluationRateType: v })}
                                                        >
                                                            <SelectTrigger>
                                                                <SelectValue />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="Month-End">Month End</SelectItem>
                                                                <SelectItem value="Quarter-End">Quarter End</SelectItem>
                                                                <SelectItem value="Year-End">Year End</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </div>
                                                </div>

                                                <div className="grid grid-cols-2 gap-4">
                                                    <div className="space-y-2">
                                                        <Label>Transaction Quote Side</Label>
                                                        <Select
                                                            value={newLink.transactionQuoteSide || 'Mid'}
                                                            onValueChange={(v: ExchangeRateQuoteSide) => setNewLink({ ...newLink, transactionQuoteSide: v })}
                                                        >
                                                            <SelectTrigger><SelectValue /></SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="Mid">Mid / Reference</SelectItem>
                                                                <SelectItem value="Buying">Buying (bank buys FX)</SelectItem>
                                                                <SelectItem value="Selling">Selling (bank sells FX)</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </div>
                                                    <div className="space-y-2">
                                                        <Label>Revaluation Quote Side</Label>
                                                        <Select
                                                            value={newLink.revaluationQuoteSide || 'Mid'}
                                                            onValueChange={(v: ExchangeRateQuoteSide) => setNewLink({ ...newLink, revaluationQuoteSide: v })}
                                                        >
                                                            <SelectTrigger><SelectValue /></SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="Mid">Mid / Reference</SelectItem>
                                                                <SelectItem value="Buying">Buying (bank buys FX)</SelectItem>
                                                                <SelectItem value="Selling">Selling (bank sells FX)</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </div>
                                                </div>

                                                <div className="space-y-2">
                                                    <Label>Notes</Label>
                                                    <Input
                                                        placeholder="Optional notes..."
                                                        value={newLink.notes || ''}
                                                        onChange={(e) => setNewLink({ ...newLink, notes: e.target.value })}
                                                    />
                                                </div>
                                            </div>
                                            <DialogFooter>
                                                <Button variant="outline" onClick={() => setAddDialogOpen(false)}>
                                                    Cancel
                                                </Button>
                                                <Button onClick={handleAddCurrencyLink} disabled={addingLink}>
                                                    {addingLink ? (
                                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                    ) : (
                                                        <Plus className="mr-2 h-4 w-4" />
                                                    )}
                                                    Add Link
                                                </Button>
                                            </DialogFooter>
                                        </DialogContent>
                                    </Dialog>
                                </div>
                            </CardHeader>
                            <CardContent className="pt-4">
                                {currencyLinks.length === 0 ? (
                                    <div className="text-center py-8 text-muted-foreground">
                                        <Link2 className="h-12 w-12 mx-auto mb-4 opacity-20" />
                                        <p>No additional currencies linked</p>
                                        <p className="text-sm mt-1">Click "Add Currency" to link foreign currencies to this account</p>
                                    </div>
                                ) : (
                                    <div className="space-y-4">
                                        {currencyLinks.map((link) => (
                                            <div
                                                key={link.id}
                                                className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
                                            >
                                                <div className="flex items-start justify-between">
                                                    <div className="space-y-1">
                                                        <div className="flex items-center gap-2">
                                                            <span className="font-mono font-bold text-lg">
                                                                {link.linkedCurrencyCode}
                                                            </span>
                                                            <Badge variant={link.isActive ? 'default' : 'secondary'}>
                                                                {link.isActive ? 'Active' : 'Inactive'}
                                                            </Badge>
                                                            {link.revaluationRequired && (
                                                                <Badge variant="outline" className="text-blue-600">
                                                                    <RefreshCcw className="h-3 w-3 mr-1" />
                                                                    {link.revaluationFrequency}
                                                                </Badge>
                                                            )}
                                                            {!link.revaluationRequired && (
                                                                <Badge variant="secondary">Non-monetary / excluded</Badge>
                                                            )}
                                                        </div>
                                                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm mt-2">
                                                            <div>
                                                                <p className="text-muted-foreground">Foreign Balance</p>
                                                                <p className="font-mono font-semibold">
                                                                    {formatCurrency(link.foreignCurrencyBalance, link.linkedCurrencyCode)}
                                                                </p>
                                                            </div>
                                                            <div>
                                                                <p className="text-muted-foreground">Base Balance</p>
                                                                <p className="font-mono font-semibold">
                                                                    {formatCurrency(link.baseCurrencyBalance, account.currencyCode)}
                                                                </p>
                                                            </div>
                                                            <div>
                                                                <p className="text-muted-foreground">Current Rate</p>
                                                                <p className="font-mono">
                                                                    {link.currentExchangeRate?.toFixed(4) || 'N/A'}
                                                                </p>
                                                            </div>
                                                            <div>
                                                                <p className="text-muted-foreground">Last Revaluation</p>
                                                                <p className="text-xs">
                                                                    {link.lastRevaluationDate
                                                                        ? new Date(link.lastRevaluationDate).toLocaleDateString()
                                                                        : 'Never'
                                                                    }
                                                                </p>
                                                            </div>
                                                        </div>
                                                        <div className="mt-3 flex flex-wrap gap-2 text-xs">
                                                            <Badge variant="outline">
                                                                Transactions: {link.transactionRateType} / {link.transactionQuoteSide || 'Mid'}
                                                            </Badge>
                                                            <Badge variant="outline">
                                                                Revaluation: {link.revaluationRateType} / {link.revaluationQuoteSide || 'Mid'}
                                                            </Badge>
                                                        </div>
                                                        {link.notes && (
                                                            <p className="text-xs text-muted-foreground mt-2">
                                                                {link.notes}
                                                            </p>
                                                        )}
                                                    </div>
                                                    <div className="flex items-center">
                                                        <Button
                                                            variant="ghost"
                                                            size="sm"
                                                            onClick={() => openRatePolicyDialog(link)}
                                                            disabled={!link.isActive}
                                                            aria-label={`Edit ${link.linkedCurrencyCode} rate policy`}
                                                        >
                                                            <Edit className="h-4 w-4" />
                                                        </Button>
                                                        <Button
                                                            variant="ghost"
                                                            size="sm"
                                                            className="text-destructive hover:text-destructive"
                                                            onClick={() => handleRemoveCurrencyLink(link)}
                                                            disabled={removingLinkId === link.id}
                                                        >
                                                            {removingLinkId === link.id ? (
                                                                <Loader2 className="h-4 w-4 animate-spin" />
                                                            ) : (
                                                                <X className="h-4 w-4" />
                                                            )}
                                                        </Button>
                                                    </div>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </CardContent>
                            {currencyLinks.length > 0 && (
                                <CardFooter className="bg-muted/30 text-sm text-muted-foreground">
                                    <AlertTriangle className="h-4 w-4 mr-2" />
                                    Currency links with transaction history cannot be deleted, only inactivated
                                </CardFooter>
                            )}
                        </Card>
                    )}

                    <Dialog open={Boolean(editingLink)} onOpenChange={(open) => {
                        if (!open) {
                            setEditingLink(null);
                            setPolicyForm(null);
                        }
                    }}>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Edit {editingLink?.linkedCurrencyCode} Rate Policy</DialogTitle>
                                <DialogDescription>
                                    These defaults apply to foreign-currency GL posting. AR/AP document policy remains authoritative for customer and supplier subledgers.
                                </DialogDescription>
                            </DialogHeader>
                            {policyForm && (
                                <div className="space-y-4 py-4">
                                    <div className="rounded-md border p-4 space-y-3">
                                        <div className="flex items-start space-x-3">
                                            <Checkbox
                                                id="editRevaluationRequired"
                                                checked={policyForm.revaluationRequired}
                                                onCheckedChange={(checked) => setPolicyForm({ ...policyForm, revaluationRequired: checked as boolean })}
                                            />
                                            <div className="space-y-1">
                                                <Label htmlFor="editRevaluationRequired" className="cursor-pointer">Monetary item — include in closing revaluation</Label>
                                                <p className="text-xs text-muted-foreground">The posting engine enforces this setting for this GL account and currency. Unmarked links remain available for foreign-currency posting but are excluded from revaluation.</p>
                                            </div>
                                        </div>
                                        {policyForm.revaluationRequired && (
                                            <div className="space-y-2">
                                                <Label>Revaluation Frequency</Label>
                                                <Select value={policyForm.revaluationFrequency} onValueChange={(value) => setPolicyForm({ ...policyForm, revaluationFrequency: value })}>
                                                    <SelectTrigger><SelectValue /></SelectTrigger>
                                                    <SelectContent>
                                                        <SelectItem value="Monthly">Monthly</SelectItem>
                                                        <SelectItem value="Quarterly">Quarterly</SelectItem>
                                                        <SelectItem value="Annually">Annually</SelectItem>
                                                        <SelectItem value="AdHoc">Ad hoc</SelectItem>
                                                    </SelectContent>
                                                </Select>
                                            </div>
                                        )}
                                    </div>
                                    <div className="grid grid-cols-2 gap-4">
                                        <div className="space-y-2">
                                            <Label>Transaction Rate Type</Label>
                                            <Select value={policyForm.transactionRateType} onValueChange={(value) => setPolicyForm({ ...policyForm, transactionRateType: value })}>
                                                <SelectTrigger><SelectValue /></SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="Daily">Daily</SelectItem>
                                                    <SelectItem value="Fixed">Fixed contractual</SelectItem>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Transaction Quote Side</Label>
                                            <Select value={policyForm.transactionQuoteSide} onValueChange={(value: ExchangeRateQuoteSide) => setPolicyForm({ ...policyForm, transactionQuoteSide: value })}>
                                                <SelectTrigger><SelectValue /></SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="Mid">Mid / Reference</SelectItem>
                                                    <SelectItem value="Buying">Buying (bank buys FX)</SelectItem>
                                                    <SelectItem value="Selling">Selling (bank sells FX)</SelectItem>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Revaluation Rate Type</Label>
                                            <Select value={policyForm.revaluationRateType} onValueChange={(value) => setPolicyForm({ ...policyForm, revaluationRateType: value })}>
                                                <SelectTrigger><SelectValue /></SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="Month-End">Month End</SelectItem>
                                                    <SelectItem value="Quarter-End">Quarter End</SelectItem>
                                                    <SelectItem value="Year-End">Year End</SelectItem>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Revaluation Quote Side</Label>
                                            <Select value={policyForm.revaluationQuoteSide} onValueChange={(value: ExchangeRateQuoteSide) => setPolicyForm({ ...policyForm, revaluationQuoteSide: value })}>
                                                <SelectTrigger><SelectValue /></SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="Mid">Mid / Reference</SelectItem>
                                                    <SelectItem value="Buying">Buying (bank buys FX)</SelectItem>
                                                    <SelectItem value="Selling">Selling (bank sells FX)</SelectItem>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Notes</Label>
                                        <Input value={policyForm.notes || ''} onChange={(event) => setPolicyForm({ ...policyForm, notes: event.target.value })} />
                                    </div>
                                </div>
                            )}
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setEditingLink(null)}>Cancel</Button>
                                <Button onClick={handleSaveRatePolicy} disabled={savingPolicy}>
                                    {savingPolicy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Save Policy
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>

                    {/* Transaction History Placeholder */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Recent Transactions</CardTitle>
                            <CardDescription>Last 10 transactions for this account</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="text-center py-8 text-muted-foreground">
                                <p>No transactions to display</p>
                                <p className="text-sm mt-2">Transaction history will appear here once the backend is connected</p>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Sidebar */}
                <div className="space-y-6">
                    {/* Status */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Status</CardTitle>
                        </CardHeader>
                        <CardContent>
                            {getStatusBadge(account.status)}
                        </CardContent>
                    </Card>

                    {/* Currency Settings */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Currency</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <div>
                                <p className="text-sm text-muted-foreground">Primary Currency</p>
                                <p className="font-mono font-semibold">{account.currencyCode}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Multi-Currency</p>
                                <Badge variant={account.isMultiCurrency ? 'default' : 'secondary'}>
                                    {account.isMultiCurrency ? 'Enabled' : 'Disabled'}
                                </Badge>
                            </div>
                            {account.isMultiCurrency && (
                                <div>
                                    <p className="text-sm text-muted-foreground">Linked Currencies</p>
                                    <p className="font-semibold">{currencyLinks.length}</p>
                                </div>
                            )}
                        </CardContent>
                    </Card>

                    {/* Features */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Features</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Direct Posting</span>
                                <Badge variant={account.allowDirectPosting ? 'default' : 'secondary'}>
                                    {account.allowDirectPosting ? 'Allowed' : 'Not Allowed'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Control Account</span>
                                <Badge variant={account.isControlAccount ? 'default' : 'secondary'}>
                                    {account.isControlAccount ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Budget Tracking</span>
                                <Badge variant={account.budgetTrackingEnabled ? 'default' : 'secondary'}>
                                    {account.budgetTrackingEnabled ? 'Enabled' : 'Disabled'}
                                </Badge>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Classification */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Classification</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <div className="flex items-center justify-between">
                                <span className="text-sm">IFRS</span>
                                <Badge variant={account.isIFRSClassified ? 'default' : 'secondary'}>
                                    {account.isIFRSClassified ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Base</span>
                                <Badge variant={account.isBaseClassified ? 'default' : 'secondary'}>
                                    {account.isBaseClassified ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Local</span>
                                <Badge variant={account.isLocalClassified ? 'default' : 'secondary'}>
                                    {account.isLocalClassified ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Actions */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Actions</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <Button variant="outline" className="w-full" disabled>
                                <Trash2 className="mr-2 h-4 w-4" />
                                Delete Account
                            </Button>
                            <p className="text-xs text-muted-foreground">
                                Accounts with transactions cannot be deleted
                            </p>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
