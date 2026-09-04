'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Edit, Trash2, DollarSign, Loader2, Plus, Link2, X, RefreshCcw, AlertTriangle } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { Account, AccountType, AccountStatus, AccountCurrencyLink, AccountBookCurrencyPolicy, Currency, AddCurrencyLinkDto, ExchangeRateQuoteSide, UpdateCurrencyLinkRatePolicyDto } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { AccountTransactionsInquiry } from '@/components/finance/accounts/account-transactions-inquiry';

export default function AccountDetailPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = React.use(params);
    const router = useRouter();
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canOverrideFxPolicy = hasPermission('Finance.FX.Policy.Override');
    const canApproveFxPolicy = hasPermission('Finance.FX.Policy.Approve');

    const [account, setAccount] = useState<Account | null>(null);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [currencyLinks, setCurrencyLinks] = useState<AccountCurrencyLink[]>([]);
    const [revaluationPolicies, setRevaluationPolicies] = useState<AccountBookCurrencyPolicy[]>([]);
    const [loading, setLoading] = useState(true);
    const [addDialogOpen, setAddDialogOpen] = useState(false);
    const [addingLink, setAddingLink] = useState(false);
    const [removingLinkId, setRemovingLinkId] = useState<string | null>(null);
    const [editingLink, setEditingLink] = useState<AccountCurrencyLink | null>(null);
    const [savingRatePolicy, setSavingRatePolicy] = useState(false);
    const [savingRevaluationPolicy, setSavingRevaluationPolicy] = useState(false);
    const [policyForm, setPolicyForm] = useState<UpdateCurrencyLinkRatePolicyDto | null>(null);
    const [selectedPolicyBookId, setSelectedPolicyBookId] = useState('');
    const [policyChoice, setPolicyChoice] = useState<'inherit' | 'include' | 'exclude'>('inherit');
    const [policyReason, setPolicyReason] = useState('');
    const [confirmNonstandard, setConfirmNonstandard] = useState(false);
    const [decisionPolicy, setDecisionPolicy] = useState<AccountBookCurrencyPolicy | null>(null);
    const [decisionAction, setDecisionAction] = useState<'approve' | 'reject'>('approve');
    const [decisionReason, setDecisionReason] = useState('');
    const [decidingPolicy, setDecidingPolicy] = useState(false);

    // New link form data
    const [newLink, setNewLink] = useState<Partial<AddCurrencyLinkDto>>({
        linkedCurrencyCode: '',
        revaluationFrequency: 'Monthly',
        transactionRateType: 'Daily',
        transactionQuoteSide: 'Mid',
        revaluationRateType: 'Month-End',
        revaluationQuoteSide: 'Mid',
        notes: '',
    });

    const loadAccountData = React.useCallback(async (showPageLoader = true) => {
        try {
            if (showPageLoader) setLoading(true);

            const [accountData, currenciesData] = await Promise.all([
                financeDataService.getAccountById(id),
                financeDataService.getCurrencies(),
            ]);

            setAccount(accountData);
            setCurrencies(currenciesData);

            if (accountData.isMultiCurrency) {
                const [links, policies] = await Promise.all([
                    financeDataService.getAccountCurrencyLinks(id),
                    financeDataService.getAccountBookCurrencyPolicies(id),
                ]);
                setCurrencyLinks(links);
                setRevaluationPolicies(policies);
            }
        } catch (error) {
            console.error('Error loading account:', error);
            toast({ title: 'Refresh failed', description: 'Authoritative account policy state could not be refreshed.', variant: 'destructive' });
        } finally {
            if (showPageLoader) setLoading(false);
        }
    }, [id, toast]);

    useEffect(() => {
        void loadAccountData();
    }, [loadAccountData]);

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
            setRevaluationPolicies(await financeDataService.getAccountBookCurrencyPolicies(id));

            toast({
                title: 'Success',
                description: `Currency link for ${newLink.linkedCurrencyCode} added successfully`,
            });

            setAddDialogOpen(false);
            setNewLink({
                linkedCurrencyCode: '',
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
        const availablePolicies = revaluationPolicies.filter(policy => policy.accountCurrencyLinkId === link.id);
        const selected = availablePolicies[0];
        setEditingLink(link);
        setPolicyForm({
            revaluationFrequency: link.revaluationFrequency,
            transactionRateType: link.transactionRateType,
            transactionQuoteSide: link.transactionQuoteSide || 'Mid',
            revaluationRateType: link.revaluationRateType,
            revaluationQuoteSide: link.revaluationQuoteSide || 'Mid',
            notes: link.notes,
        });
        setSelectedPolicyBookId(selected?.accountingBookId || '');
        setPolicyChoice(selected?.revaluationOverride == null ? 'inherit' : selected.revaluationOverride ? 'include' : 'exclude');
        setPolicyReason('');
        setConfirmNonstandard(false);
    };

    const selectedPolicy = revaluationPolicies.find(policy =>
        policy.accountCurrencyLinkId === editingLink?.id && policy.accountingBookId === selectedPolicyBookId);

    const selectPolicyBook = (bookId: string) => {
        const selected = revaluationPolicies.find(policy =>
            policy.accountCurrencyLinkId === editingLink?.id && policy.accountingBookId === bookId);
        setSelectedPolicyBookId(bookId);
        setPolicyChoice(selected?.revaluationOverride == null ? 'inherit' : selected.revaluationOverride ? 'include' : 'exclude');
        setPolicyReason('');
        setConfirmNonstandard(false);
    };

    const handleSaveRatePolicy = async () => {
        if (!editingLink || !policyForm) return;
        try {
            setSavingRatePolicy(true);
            await financeDataService.updateAccountCurrencyLinkRatePolicy(id, editingLink.linkedCurrencyCode, policyForm);
            toast({
                title: 'Rate settings saved',
                description: `${editingLink.linkedCurrencyCode} transaction and closing-rate settings were updated independently.`,
            });
        } catch (error: any) {
            toast({ title: 'Rate settings not saved', description: error?.message || 'The rate-policy update failed. Revaluation treatment was not changed by this action.', variant: 'destructive' });
        } finally {
            setSavingRatePolicy(false);
            await loadAccountData(false);
        }
    };

    const handleSaveRevaluationPolicy = async () => {
        if (!editingLink || !selectedPolicy) return;
        if (policyReason.trim().length < 5) {
            toast({ title: 'Reason required', description: 'Enter at least five characters explaining this policy decision.', variant: 'destructive' });
            return;
        }
        const nonstandardRequest = policyChoice === 'include' && ['Equity', 'Revenue', 'Expense'].includes(selectedPolicy.coreAccountType);
        if (nonstandardRequest && !confirmNonstandard) {
            toast({ title: 'Explicit confirmation required', description: 'Confirm the prominent non-standard revaluation warning before continuing.', variant: 'destructive' });
            return;
        }
        try {
            setSavingRevaluationPolicy(true);
            const savedPolicy = await financeDataService.saveAccountBookCurrencyPolicy(id, editingLink.id, selectedPolicy.accountingBookId, {
                revaluationOverride: policyChoice === 'inherit' ? null : policyChoice === 'include',
                reason: policyReason.trim(),
                confirmNonstandardInclusion: confirmNonstandard,
                rowVersion: selectedPolicy.rowVersion,
            });
            toast({
                title: savedPolicy.lifecycleStatus === 'PendingApproval' ? 'Approval requested' : 'Policy saved',
                description: savedPolicy.lifecycleStatus === 'PendingApproval'
                    ? 'The non-standard inclusion is not effective until a different authorized user approves it.'
                    : `${editingLink.linkedCurrencyCode} revaluation treatment was updated for ${savedPolicy.accountingBookCode}; rate settings were not changed.`,
            });
        } catch (error: any) {
            toast({ title: 'Revaluation treatment not saved', description: error?.message || 'The governed override failed. Rate settings were not changed by this action.', variant: 'destructive' });
        } finally {
            setSavingRevaluationPolicy(false);
            await loadAccountData(false);
        }
    };

    const openPolicyDecision = (policy: AccountBookCurrencyPolicy, action: 'approve' | 'reject') => {
        setDecisionPolicy(policy);
        setDecisionAction(action);
        setDecisionReason('');
    };

    const handlePolicyDecision = async () => {
        if (!decisionPolicy?.id || !decisionPolicy.rowVersion || decisionReason.trim().length < 5) {
            toast({ title: 'Decision reason required', description: 'Enter at least five characters and refresh if concurrency evidence is unavailable.', variant: 'destructive' });
            return;
        }
        try {
            setDecidingPolicy(true);
            const updated = await financeDataService.decideAccountBookCurrencyPolicy(
                id,
                decisionPolicy.id,
                decisionAction,
                { reason: decisionReason.trim(), rowVersion: decisionPolicy.rowVersion },
            );
            setRevaluationPolicies(current => current.map(policy => policy.id === updated.id ? updated : policy));
            setDecisionPolicy(null);
            toast({ title: decisionAction === 'approve' ? 'Approval recorded' : 'Request rejected', description: `${updated.accountingBookCode}/${updated.currencyCode} policy is ${updated.lifecycleStatus}.` });
        } catch (error: any) {
            toast({ title: 'Decision failed', description: error?.message || 'The policy may have changed. Refresh and try again.', variant: 'destructive' });
        } finally {
            setDecidingPolicy(false);
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

                                                <Alert>
                                                    <AlertTriangle className="h-4 w-4" />
                                                    <AlertDescription>
                                                        Revaluation inclusion is configured after linking, separately for each accounting book. The account classification supplies the inherited default.
                                                    </AlertDescription>
                                                </Alert>
                                                <div className="space-y-2">
                                                    <Label>Revaluation Frequency</Label>
                                                    <Select value={newLink.revaluationFrequency} onValueChange={(v) => setNewLink({ ...newLink, revaluationFrequency: v })}>
                                                        <SelectTrigger><SelectValue /></SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="Monthly">Monthly</SelectItem>
                                                            <SelectItem value="Quarterly">Quarterly</SelectItem>
                                                            <SelectItem value="Annually">Annually</SelectItem>
                                                            <SelectItem value="AdHoc">Ad hoc</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                </div>

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
                                                            <Badge variant="outline"><RefreshCcw className="h-3 w-3 mr-1" />{link.revaluationFrequency}</Badge>
                                                            {revaluationPolicies.some(policy => policy.accountCurrencyLinkId === link.id && policy.isNonstandardInclusion) && (
                                                                <Badge className="border-amber-500 bg-amber-100 text-amber-900">Non-standard revaluation policy</Badge>
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
                                                        <div className="mt-3 space-y-2">
                                                            {revaluationPolicies.filter(policy => policy.accountCurrencyLinkId === link.id).map(policy => (
                                                                <div key={policy.accountingBookId} className={policy.isNonstandardInclusion ? 'rounded border border-amber-400 bg-amber-50 p-2' : 'rounded border p-2'}>
                                                                    <div className="flex flex-wrap items-center gap-2">
                                                                        <strong>{policy.accountingBookCode}</strong>
                                                                        <Badge variant={policy.effectiveRevaluationRequired ? 'default' : 'secondary'}>
                                                                            {policy.effectiveRevaluationRequired ? 'Included' : 'Excluded'}
                                                                        </Badge>
                                                                        <Badge variant="outline">{policy.effectiveSource === 'Classification' ? 'Inherited' : 'Override'}</Badge>
                                                                        {policy.lifecycleStatus === 'PendingApproval' && <Badge className="bg-amber-600">Pending approval</Badge>}
                                                                    </div>
                                                                    <p className="mt-1 text-muted-foreground">{policy.accountClassificationCode} — {policy.accountClassificationName}; default {policy.classificationDefault}</p>
                                                                    {policy.warning && <p className="mt-1 font-semibold text-amber-900">{policy.warning}</p>}
                                                                    {policy.lifecycleStatus === 'PendingApproval' && policy.pendingReason && (
                                                                        <p className="mt-1 text-sm text-amber-950">Request reason: {policy.pendingReason}</p>
                                                                    )}
                                                                    {policy.lifecycleStatus === 'PendingApproval' && canApproveFxPolicy && policy.id && (
                                                                        <div className="mt-2 flex gap-2">
                                                                            <Button size="sm" onClick={() => openPolicyDecision(policy, 'approve')}>Review and approve</Button>
                                                                            <Button size="sm" variant="outline" onClick={() => openPolicyDecision(policy, 'reject')}>Reject</Button>
                                                                        </div>
                                                                    )}
                                                                </div>
                                                            ))}
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
                                                            disabled={!link.isActive || !canOverrideFxPolicy}
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
                                <DialogTitle>Edit {editingLink?.linkedCurrencyCode} Book and Rate Policy</DialogTitle>
                                <DialogDescription>
                                    These defaults apply to foreign-currency GL posting. AR/AP document policy remains authoritative for customer and supplier subledgers.
                                </DialogDescription>
                            </DialogHeader>
                            {policyForm && (
                                <div className="space-y-4 py-4">
                                    <div className="rounded-md border p-4 space-y-3">
                                        <div className="space-y-2">
                                            <Label>Accounting book</Label>
                                            <Select value={selectedPolicyBookId} onValueChange={selectPolicyBook}>
                                                <SelectTrigger><SelectValue placeholder="Select accounting book" /></SelectTrigger>
                                                <SelectContent>
                                                    {revaluationPolicies.filter(policy => policy.accountCurrencyLinkId === editingLink?.id).map(policy => (
                                                        <SelectItem key={policy.accountingBookId} value={policy.accountingBookId}>{policy.accountingBookCode} — {policy.accountingBookName}</SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        {selectedPolicy && <>
                                            <div className="rounded bg-muted p-3 text-sm">
                                                <strong>{selectedPolicy.accountClassificationCode} — {selectedPolicy.accountClassificationName}</strong>
                                                <div>Classification default: {selectedPolicy.classificationDefault}; current effective result: {selectedPolicy.effectiveRevaluationRequired ? 'Include' : 'Exclude'} ({selectedPolicy.effectiveSource}).</div>
                                            </div>
                                            <div className="space-y-2">
                                                <Label>Closing revaluation treatment</Label>
                                                <Select value={policyChoice} onValueChange={(value: 'inherit' | 'include' | 'exclude') => { setPolicyChoice(value); setConfirmNonstandard(false); }}>
                                                    <SelectTrigger><SelectValue /></SelectTrigger>
                                                    <SelectContent>
                                                        <SelectItem value="inherit">Inherit classification default</SelectItem>
                                                        <SelectItem value="include">Include in closing revaluation</SelectItem>
                                                        <SelectItem value="exclude">Exclude from closing revaluation</SelectItem>
                                                    </SelectContent>
                                                </Select>
                                            </div>
                                            {policyChoice === 'include' && ['Equity', 'Revenue', 'Expense'].includes(selectedPolicy.coreAccountType) && (
                                                <Alert className="border-amber-500 bg-amber-50 text-amber-950">
                                                    <AlertTriangle className="h-5 w-5" />
                                                    <AlertDescription className="font-semibold">
                                                        NON-STANDARD REVALUATION POLICY. This {selectedPolicy.coreAccountType} account will enter closing revaluation. A different authorized user must approve before it becomes effective.
                                                        <label className="mt-3 flex items-center gap-2 font-normal"><input type="checkbox" checked={confirmNonstandard} onChange={event => setConfirmNonstandard(event.target.checked)} />I understand and explicitly confirm this request.</label>
                                                    </AlertDescription>
                                                </Alert>
                                            )}
                                            <div className="space-y-2">
                                                <Label>Mandatory reason</Label>
                                                <Input value={policyReason} onChange={event => setPolicyReason(event.target.value)} placeholder="Explain why this book/currency treatment is required" />
                                            </div>
                                            {selectedPolicy.lifecycleStatus === 'PendingApproval' && <Alert className="border-amber-500"><AlertTriangle className="h-4 w-4" /><AlertDescription>Pending independent approval. The current effective treatment remains unchanged.</AlertDescription></Alert>}
                                            <div className="flex justify-end">
                                                <Button onClick={handleSaveRevaluationPolicy} disabled={savingRevaluationPolicy || !canOverrideFxPolicy || selectedPolicy.lifecycleStatus === 'PendingApproval'}>
                                                    {savingRevaluationPolicy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                                    Save revaluation treatment
                                                </Button>
                                            </div>
                                        </>}
                                        <div className="space-y-2">
                                            <Label>Revaluation Frequency</Label>
                                            <Select value={policyForm.revaluationFrequency} onValueChange={(value) => setPolicyForm({ ...policyForm, revaluationFrequency: value })}>
                                                <SelectTrigger><SelectValue /></SelectTrigger>
                                                <SelectContent><SelectItem value="Monthly">Monthly</SelectItem><SelectItem value="Quarterly">Quarterly</SelectItem><SelectItem value="Annually">Annually</SelectItem><SelectItem value="AdHoc">Ad hoc</SelectItem></SelectContent>
                                            </Select>
                                        </div>
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
                                    <div className="flex justify-end">
                                        <Button variant="secondary" onClick={handleSaveRatePolicy} disabled={savingRatePolicy || !canOverrideFxPolicy}>
                                            {savingRatePolicy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                            Save rate settings
                                        </Button>
                                    </div>
                                </div>
                            )}
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setEditingLink(null)}>Close</Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>

                    <Dialog open={Boolean(decisionPolicy)} onOpenChange={(open) => !open && setDecisionPolicy(null)}>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>{decisionAction === 'approve' ? 'Approve' : 'Reject'} non-standard revaluation policy</DialogTitle>
                                <DialogDescription>
                                    {decisionPolicy?.accountingBookCode}/{decisionPolicy?.currencyCode} — {decisionPolicy?.accountClassificationCode}. The requester cannot decide their own request.
                                </DialogDescription>
                            </DialogHeader>
                            <Alert className="border-amber-500 bg-amber-50 text-amber-950">
                                <AlertTriangle className="h-5 w-5" />
                                <AlertDescription className="font-semibold">
                                    This {decisionPolicy?.coreAccountType} account will be included in closing revaluation if the approval workflow completes.
                                </AlertDescription>
                            </Alert>
                            <div className="space-y-2">
                                <Label>Mandatory independent decision reason</Label>
                                <Input value={decisionReason} onChange={event => setDecisionReason(event.target.value)} placeholder="Record the finance review rationale" />
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setDecisionPolicy(null)}>Cancel</Button>
                                <Button
                                    variant={decisionAction === 'reject' ? 'destructive' : 'default'}
                                    onClick={handlePolicyDecision}
                                    disabled={decidingPolicy || decisionReason.trim().length < 5 || !decisionPolicy?.rowVersion}
                                >
                                    {decidingPolicy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    {decisionAction === 'approve' ? 'Approve request' : 'Reject request'}
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>

                    <AccountTransactionsInquiry accountId={id} accountBooks={account.accountingBooks} />
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
