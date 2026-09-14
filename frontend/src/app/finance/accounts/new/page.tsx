'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, ArrowLeft, Save, Loader2, Eye, Layers, Wand2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { AccountType, AccountStatus, CashFlowClassification, SegmentStructure, SegmentLookupValue, FinanceSettings, AccountBookAssignmentInput } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { AccountBookAssignments } from '@/components/finance/accounts/account-book-assignments';
import { composeAccountIdentityPreview } from '@/components/finance/accounts/account-identity-preview';
import { getSegmentAccess } from '@/components/finance/segments/segment-access';
import { useAuth } from '@/hooks/use-auth';

interface SegmentValue {
    segmentId: string;
    segmentName: string;
    value: string;
    isValid: boolean;
}

export default function NewAccountPage() {
    const router = useRouter();
    const { toast } = useToast();
    const { hasPermission, isLoading: authLoading } = useAuth();
    const { canRead, canManage } = getSegmentAccess(hasPermission);

    // Settings and loading state
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [segments, setSegments] = useState<SegmentStructure[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [reloadToken, setReloadToken] = useState(0);
    const [saving, setSaving] = useState(false);

    // Segment values for segmented COA
    const [segmentValues, setSegmentValues] = useState<Record<string, string>>({});
    const [segmentErrors, setSegmentErrors] = useState<Record<string, string>>({});
    const [accountingBooks, setAccountingBooks] = useState<AccountBookAssignmentInput[]>([]);

    // Form data
    const [formData, setFormData] = useState({
        accountCode: '',
        accountNumber: '',
        accountName: '',
        accountType: 'Asset' as AccountType,
        accountSubCategory: '', // Initial empty state
        cashFlowClassification: '' as CashFlowClassification | '',
        description: '',
        currencyCode: 'GHS',
        isMultiCurrency: false,
        allowDirectPosting: true,
        isControlAccount: false,
        budgetTrackingEnabled: false,
        status: 'Active' as AccountStatus,
    });
    // Load settings and segments
    useEffect(() => {
        const loadData = async () => {
            if (authLoading || !canRead) {
                setLoading(false);
                return;
            }
            try {
                setLoading(true);
                setLoadError(null);
                const [settingsData, segmentsData] = await Promise.all([
                    financeDataService.getFinanceSettings(),
                    financeDataService.getSegmentStructures(),
                ]);
                const activeSegments = segmentsData
                    .filter(segment => segment.isActive && (segment.lifecycleStatus === 'Active' || segment.lifecycleStatus === 'Frozen'))
                    .sort((a, b) => a.segmentPosition - b.segmentPosition);
                setSettings(settingsData);
                setSegments(activeSegments);

                // Initialize segment values
                const initialValues: Record<string, string> = {};
                activeSegments.forEach(seg => {
                    initialValues[seg.id] = '';
                });
                setSegmentValues(initialValues);
            } catch (error) {
                console.error('Error loading data:', error);
                setLoadError(error instanceof Error ? error.message : 'Failed to load settings and active account-number segments.');
                toast({
                    title: 'Error',
                    description: 'Failed to load settings and segments',
                    variant: 'destructive',
                });
            } finally {
                setLoading(false);
            }
        };
        loadData();
    }, [authLoading, canRead, toast, reloadToken]);

    // Generate account code from segment values
    // Note: We ALWAYS use segmented accounts now
    const generatedIdentity = useMemo(() => composeAccountIdentityPreview(
        segments, segmentValues, settings?.accountSeparator || '-'),
    [settings?.accountSeparator, segments, segmentValues]);

    // Update account code when segments change
    useEffect(() => {
        setFormData(prev => ({
            ...prev,
            accountCode: generatedIdentity.naturalAccountCode,
            accountNumber: generatedIdentity.accountNumber,
        }));
    }, [generatedIdentity]);

    const updateSegmentValue = (segmentId: string, value: string) => {
        setSegmentValues(prev => ({
            ...prev,
            [segmentId]: value,
        }));
        setSegmentErrors(prev => {
            if (!prev[segmentId]) return prev;
            const next = { ...prev };
            delete next[segmentId];
            return next;
        });
    };

    const validateSegments = () => {
        const sortedSegments = [...segments].sort((a, b) => a.segmentPosition - b.segmentPosition);
        const nextErrors: Record<string, string> = {};

        for (const seg of sortedSegments) {
            const segmentError = validateSegment(seg, segmentValues[seg.id] || '');
            if (segmentError) {
                nextErrors[seg.id] = segmentError;
            }
        }

        setSegmentErrors(nextErrors);
        return { sortedSegments, nextErrors };
    };

    const validateSegment = (seg: SegmentStructure, rawValue: string): string | undefined => {
        const value = rawValue.trim();
        const isRequired = seg.isRequired;

        if (isRequired && !value) {
            return `${seg.segmentName} is required.`;
        }

        if (!value) {
            return undefined;
        }

        if (value.length !== seg.segmentLength) {
            return `${seg.segmentName} must be exactly ${seg.segmentLength} characters.`;
        }

        if (seg.dataType?.toLowerCase() === 'numeric' && !/^\d+$/.test(value)) {
            return `${seg.segmentName} must contain only numbers.`;
        }

        if (seg.lookupTableRequired) {
            const isAllowed = (seg.lookupValues || []).some(
                lv => lv.isActive && lv.segmentValue === value
            );
            if (!isAllowed) {
                return `${seg.segmentName} must be selected from allowed lookup values.`;
            }
        }

        return undefined;
    };

    const validateSegmentOnBlur = (seg: SegmentStructure) => {
        const error = validateSegment(seg, segmentValues[seg.id] || '');
        setSegmentErrors(prev => {
            const next = { ...prev };
            if (error) {
                next[seg.id] = error;
            } else {
                delete next[seg.id];
            }
            return next;
        });
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        try {
            const { sortedSegments, nextErrors } = validateSegments();
            if (Object.keys(nextErrors).length > 0) {
                throw new Error(Object.values(nextErrors)[0]);
            }

            const enabledBooks = accountingBooks.filter(book => book.isEnabled);
            if (enabledBooks.length === 0 || enabledBooks.some(book => !book.accountClassificationId)) {
                toast({ title: 'Accounting-book assignment required', description: 'Enable at least one book and select a compatible classification for every enabled book.', variant: 'destructive' });
                return;
            }
            setSaving(true);

            // Build segment values array for segmented accounts
            // Note: We ALWAYS use segmented accounts - Standard COA is no longer supported
            const segmentValueInputs = sortedSegments
                .map(seg => ({
                    segmentStructureId: seg.id,
                    segmentPosition: seg.segmentPosition,
                    segmentValue: (segmentValues[seg.id] || '').trim(),
                }))
                .filter(seg => seg.segmentValue.length > 0);

            await financeDataService.createAccount({
                accountCode: formData.accountCode,
                accountNumber: formData.accountNumber,
                accountName: formData.accountName,
                accountType: formData.accountType,
                accountCategory: undefined,
                accountSubCategory: undefined,
                cashFlowClassification: formData.cashFlowClassification || null,
                currencyCode: formData.currencyCode,
                isMultiCurrency: formData.isMultiCurrency,
                isIFRSClassified: false,
                isPostingAllowed: formData.allowDirectPosting,
                isControlAccount: formData.isControlAccount,
                budgetTrackingEnabled: formData.budgetTrackingEnabled,
                status: formData.status,
                isSegmented: true,  // Always segmented
                segmentValues: segmentValueInputs,
                accountingBooks,
            });

            toast({
                title: 'Success',
                description: 'Account created successfully',
            });

            router.push('/finance/accounts');
        } catch (error: any) {
            console.error('Error creating account:', error);
            toast({
                title: 'Error',
                description: error?.message || 'Failed to create account',
                variant: 'destructive',
            });
        } finally {
            setSaving(false);
        }
    };

    const renderSegmentInput = (segment: SegmentStructure) => {
        const value = segmentValues[segment.id] || '';
        const lookupValues = segment.lookupValues || [];

        if (segment.lookupTableRequired && lookupValues.length > 0) {
            // Dropdown only - segment requires lookup values
            return (
                <Select
                    value={value}
                    onValueChange={(v) => updateSegmentValue(segment.id, v)}
                >
                    <SelectTrigger onBlur={() => validateSegmentOnBlur(segment)}>
                        <SelectValue placeholder={`Select ${segment.segmentName}...`} />
                    </SelectTrigger>
                    <SelectContent>
                        {lookupValues
                            .filter(lv => lv.isActive)
                            .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0))
                            .map((lv) => (
                                <SelectItem key={lv.id} value={lv.segmentValue}>
                                    <span className="font-mono">{lv.segmentValue}</span>
                                    <span className="ml-2 text-muted-foreground">- {lv.description}</span>
                                </SelectItem>
                            ))
                        }
                    </SelectContent>
                </Select>
            );
        } else if (!segment.lookupTableRequired && lookupValues.length > 0) {
            // Combo - can select from dropdown OR type custom value
            return (
                <div className="space-y-2">
                    <div className="flex gap-2">
                        <div className="flex-1">
                            <Input
                                placeholder={`Enter ${segment.segmentName} value or select below...`}
                                value={value}
                                onChange={(e) => updateSegmentValue(segment.id, e.target.value)}
                                onBlur={() => validateSegmentOnBlur(segment)}
                                maxLength={segment.segmentLength}
                                className="font-mono"
                            />
                        </div>
                    </div>
                    <div className="flex flex-wrap gap-1">
                        {lookupValues
                            .filter(lv => lv.isActive)
                            .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0))
                            .slice(0, 8)
                            .map((lv) => (
                                <Button
                                    key={lv.id}
                                    type="button"
                                    variant={value === lv.segmentValue ? 'default' : 'outline'}
                                    size="sm"
                                    onClick={() => updateSegmentValue(segment.id, lv.segmentValue)}
                                    className="text-xs"
                                >
                                    <span className="font-mono">{lv.segmentValue}</span>
                                    <span className="ml-1 text-muted-foreground">({lv.description})</span>
                                </Button>
                            ))
                        }
                    </div>
                </div>
            );
        } else {
            // Text input only - no lookup values
            return (
                <Input
                    placeholder={`Enter ${segment.segmentName} value...`}
                    value={value}
                    onChange={(e) => updateSegmentValue(segment.id, e.target.value)}
                    onBlur={() => validateSegmentOnBlur(segment)}
                    maxLength={segment.segmentLength}
                    className="font-mono"
                />
            );
        }
    };

    if (authLoading || loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!canRead || !canManage) {
        return <Alert variant="destructive"><AlertDescription>Finance.Read and Finance.ChartOfAccounts.Configure are required to create a GL account.</AlertDescription></Alert>;
    }

    if (loadError) {
        return <Alert variant="destructive"><AlertDescription>{loadError}<Button className="ml-3" size="sm" variant="outline" onClick={() => setReloadToken(value => value + 1)}>Retry</Button></AlertDescription></Alert>;
    }

    if (segments.length === 0) {
        return <Alert variant="destructive"><AlertDescription>No active account-number structure is available. Configure and activate the required Finance segments before creating an account.</AlertDescription></Alert>;
    }

    // Note: Standard COA is no longer supported - we ALWAYS use segmented accounts
    const isSegmented = true;

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Plus className="h-8 w-8" />
                        New Segmented Account
                    </h1>
                    <p className="text-muted-foreground">
                        Create a new GL account using segment structure
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
                        <BreadcrumbPage>New Account</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Form */}
            <form onSubmit={handleSubmit}>
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                    {/* Main Form */}
                    <div className="lg:col-span-2 space-y-6">
                        {/* Segmented COA - Segment Selection */}
                        {isSegmented && (
                            <Card className="border-blue-200 bg-blue-50/50">
                                <CardHeader>
                                    <CardTitle className="flex items-center gap-2">
                                        <Layers className="h-5 w-5 text-blue-600" />
                                        Segment Values
                                    </CardTitle>
                                    <CardDescription>
                                        Select or enter values for each segment to build the account code
                                    </CardDescription>
                                </CardHeader>
                                <CardContent className="space-y-4">
                                    {/* Segment Inputs */}
                                    {segments.map((segment) => (
                                        <div key={segment.id} className="space-y-2">
                                            <div className="flex items-center gap-2">
                                                <Label className="font-medium">
                                                    {segment.segmentName}
                                                    <span className="text-red-500">*</span>
                                                </Label>
                                                <Badge variant="outline" className="text-xs">
                                                    {segment.lookupTableRequired ? 'Dropdown' : 'Text/Dropdown'}
                                                </Badge>
                                                <span className="text-xs text-muted-foreground">
                                                    ({segment.segmentLength} chars)
                                                </span>
                                            </div>
                                            {renderSegmentInput(segment)}
                                            {segmentErrors[segment.id] && (
                                                <p className="text-sm text-red-600">{segmentErrors[segment.id]}</p>
                                            )}
                                        </div>
                                    ))}

                                    {/* Preview */}
                                    <Alert className="bg-gradient-to-r from-blue-100 to-indigo-100 border-blue-300">
                                        <Eye className="h-4 w-4" />
                                        <AlertDescription>
                                            <div className="flex items-center justify-between">
                                                <span className="font-medium">Generated Account Number:</span>
                                                <span className="font-mono text-lg font-bold text-blue-700">
                                                    {generatedIdentity.accountNumber}
                                                </span>
                                            </div>
                                        </AlertDescription>
                                    </Alert>
                                </CardContent>
                            </Card>
                        )}

                        {/* Basic Information */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Basic Information</CardTitle>
                                <CardDescription>Account identification and classification</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label htmlFor="accountCode">
                                            Account Code *
                                            {isSegmented && (
                                                <Badge variant="secondary" className="ml-2 text-xs">
                                                    <Wand2 className="h-3 w-3 mr-1" />
                                                    Auto-generated
                                                </Badge>
                                            )}
                                        </Label>
                                        <Input
                                            id="accountCode"
                                            placeholder={isSegmented ? "100-200-1000" : "1000"}
                                            value={formData.accountCode}
                                            onChange={(e) => setFormData({ ...formData, accountCode: e.target.value })}
                                            disabled={isSegmented}
                                            required
                                            className="font-mono"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="accountNumber">
                                            Account Number *
                                            {isSegmented && (
                                                <Badge variant="secondary" className="ml-2 text-xs">
                                                    <Wand2 className="h-3 w-3 mr-1" />
                                                    Auto-generated
                                                </Badge>
                                            )}
                                        </Label>
                                        <Input
                                            id="accountNumber"
                                            placeholder={isSegmented ? "100-200-1000" : "1000"}
                                            value={formData.accountNumber}
                                            onChange={(e) => setFormData({ ...formData, accountNumber: e.target.value })}
                                            disabled={isSegmented}
                                            required
                                            className="font-mono"
                                        />
                                    </div>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="accountName">Account Name *</Label>
                                    <Input
                                        id="accountName"
                                        placeholder="Cash and Cash Equivalents"
                                        value={formData.accountName}
                                        onChange={(e) => setFormData({ ...formData, accountName: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="accountType">Core Account Type *</Label>
                                    <Select value={formData.accountType} onValueChange={(value) => {
                                        setFormData({ ...formData, accountType: value as AccountType, accountSubCategory: '' });
                                        setAccountingBooks(current => current.map(mapping => ({ ...mapping, accountClassificationId: null })));
                                    }}>
                                        <SelectTrigger id="accountType"><SelectValue /></SelectTrigger>
                                        <SelectContent>{(['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'] as AccountType[]).map(type => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">Choose the book-specific classification below. Options are loaded from Finance configuration.</p>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="description">Description</Label>
                                    <Textarea
                                        id="description"
                                        placeholder="Optional account description"
                                        value={formData.description}
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
                                {formData.isMultiCurrency && (
                                    <div className="bg-blue-50 p-3 rounded text-sm">
                                        <p className="font-semibold mb-1">Multi-Currency Enabled</p>
                                        <p>This account can accept transactions in multiple currencies. Currency links can be configured after creation.</p>
                                    </div>
                                )}
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
                                                ? ''
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

                        {/* Accounting-book assignments */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Accounting Books</CardTitle>
                                <CardDescription>Enable each reporting book this account belongs to, then select its detailed classification.</CardDescription>
                            </CardHeader>
                            <CardContent>
                                <AccountBookAssignments accountType={formData.accountType} value={accountingBooks} onChange={setAccountingBooks} />
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
                                    </SelectContent>
                                </Select>
                            </CardContent>
                        </Card>

                        {/* Actions */}
                        <div className="space-y-2">
                            <Button type="submit" className="w-full" disabled={saving}>
                                {saving ? (
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                ) : (
                                    <Save className="mr-2 h-4 w-4" />
                                )}
                                Create Account
                            </Button>
                            <Button type="button" variant="outline" className="w-full" onClick={() => router.back()}>
                                Cancel
                            </Button>
                        </div>
                    </div>
                </div>
            </form>
        </div>
    );
}
