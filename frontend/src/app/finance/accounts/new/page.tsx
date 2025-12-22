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
import type { AccountType, AccountStatus, SegmentStructure, SegmentLookupValue, FinanceSettings } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useDemoMode } from '@/contexts/demo-mode-context';
import { useToast } from '@/hooks/use-toast';

interface SegmentValue {
    segmentId: string;
    segmentName: string;
    value: string;
    isValid: boolean;
}

export default function NewAccountPage() {
    const router = useRouter();
    const { toast } = useToast();
    const demoContext = useDemoMode();
    const isDemo = demoContext.isDemoMode('finance');

    // Settings and loading state
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [segments, setSegments] = useState<SegmentStructure[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);

    // Segment values for segmented COA
    const [segmentValues, setSegmentValues] = useState<Record<string, string>>({});
    const [useManualEntry, setUseManualEntry] = useState(false);

    // Form data
    const [formData, setFormData] = useState({
        accountCode: '',
        accountNumber: '',
        accountName: '',
        accountType: 'Asset' as AccountType,
        description: '',
        currencyCode: 'GHS',
        isMultiCurrency: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: true,
        isControlAccount: false,
        budgetTrackingEnabled: false,
        status: 'Active' as AccountStatus,
    });

    // Load settings and segments
    useEffect(() => {
        const loadData = async () => {
            try {
                setLoading(true);
                const [settingsData, segmentsData] = await Promise.all([
                    financeDataService.getFinanceSettings(),
                    financeDataService.getSegmentStructures(),
                ]);
                setSettings(settingsData);
                setSegments(segmentsData.sort((a, b) => a.segmentPosition - b.segmentPosition));

                // Initialize segment values
                const initialValues: Record<string, string> = {};
                segmentsData.forEach(seg => {
                    initialValues[seg.id] = '';
                });
                setSegmentValues(initialValues);
            } catch (error) {
                console.error('Error loading data:', error);
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
    }, [toast]);

    // Generate account code from segment values
    const generatedAccountCode = useMemo(() => {
        if (!settings || settings.coaType !== 'Segmented' || useManualEntry) return '';

        const parts: string[] = [];
        const sortedSegments = [...segments].sort((a, b) => a.segmentPosition - b.segmentPosition);

        for (const segment of sortedSegments) {
            const value = segmentValues[segment.id] || '';
            if (value) {
                parts.push(value.padStart(segment.segmentLength, '0'));
            } else {
                parts.push(''.padStart(segment.segmentLength, '0'));
            }
        }

        // Join with the separator from the first segment
        return parts.join('-');
    }, [settings, segments, segmentValues, useManualEntry]);

    // Update account code when segments change (for segmented COA)
    useEffect(() => {
        if (settings?.coaType === 'Segmented' && !useManualEntry && generatedAccountCode) {
            setFormData(prev => ({
                ...prev,
                accountCode: generatedAccountCode,
                accountNumber: generatedAccountCode,
            }));
        }
    }, [generatedAccountCode, settings?.coaType, useManualEntry]);

    const updateSegmentValue = (segmentId: string, value: string) => {
        setSegmentValues(prev => ({
            ...prev,
            [segmentId]: value,
        }));
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        try {
            setSaving(true);

            // Build segment values array for segmented accounts
            const segmentValueInputs = settings?.coaType === 'Segmented'
                ? segments.map(seg => ({
                    segmentPosition: seg.segmentPosition,
                    segmentValue: segmentValues[seg.id] || '',
                }))
                : undefined;

            await financeDataService.createAccount({
                ...formData,
                isSegmented: settings?.coaType === 'Segmented',
                segmentValues: segmentValueInputs,
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
                    <SelectTrigger>
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
                    maxLength={segment.segmentLength}
                    className="font-mono"
                />
            );
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    const isSegmented = settings?.coaType === 'Segmented';

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Plus className="h-8 w-8" />
                        New Account
                        <Badge variant="outline" className="ml-2">
                            {isSegmented ? 'Segmented COA' : 'Standard COA'}
                        </Badge>
                    </h1>
                    <p className="text-muted-foreground">
                        Create a new GL account
                    </p>
                    {isDemo && (
                        <p className="text-sm text-orange-600 mt-1">
                            ⚠️ DEMO FRONTEND UI
                        </p>
                    )}
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
                                    {/* Manual Entry Toggle */}
                                    <div className="flex items-center space-x-2">
                                        <Checkbox
                                            id="useManualEntry"
                                            checked={useManualEntry}
                                            onCheckedChange={(checked) => setUseManualEntry(checked as boolean)}
                                        />
                                        <Label htmlFor="useManualEntry" className="cursor-pointer text-sm">
                                            Enter account code manually instead of building from segments
                                        </Label>
                                    </div>

                                    {!useManualEntry && (
                                        <>
                                            {/* Segment Inputs */}
                                            {segments.map((segment) => (
                                                <div key={segment.id} className="space-y-2">
                                                    <div className="flex items-center gap-2">
                                                        <Label className="font-medium">
                                                            {segment.segmentName}
                                                            {segment.isMandatory && <span className="text-red-500">*</span>}
                                                        </Label>
                                                        <Badge variant="outline" className="text-xs">
                                                            {segment.lookupTableRequired ? 'Dropdown' : 'Text/Dropdown'}
                                                        </Badge>
                                                        <span className="text-xs text-muted-foreground">
                                                            ({segment.segmentLength} chars)
                                                        </span>
                                                    </div>
                                                    {renderSegmentInput(segment)}
                                                </div>
                                            ))}

                                            {/* Preview */}
                                            <Alert className="bg-gradient-to-r from-blue-100 to-indigo-100 border-blue-300">
                                                <Eye className="h-4 w-4" />
                                                <AlertDescription>
                                                    <div className="flex items-center justify-between">
                                                        <span className="font-medium">Generated Account Code:</span>
                                                        <span className="font-mono text-lg font-bold text-blue-700">
                                                            {generatedAccountCode || '---/---/----'}
                                                        </span>
                                                    </div>
                                                </AlertDescription>
                                            </Alert>
                                        </>
                                    )}
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
                                            {isSegmented && !useManualEntry && (
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
                                            disabled={isSegmented && !useManualEntry}
                                            required
                                            className="font-mono"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="accountNumber">
                                            Account Number *
                                            {isSegmented && !useManualEntry && (
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
                                            disabled={isSegmented && !useManualEntry}
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
                                    <Label htmlFor="accountType">Account Type *</Label>
                                    <Select
                                        value={formData.accountType}
                                        onValueChange={(value: AccountType) => setFormData({ ...formData, accountType: value })}
                                    >
                                        <SelectTrigger id="accountType">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Asset">Asset</SelectItem>
                                            <SelectItem value="Liability">Liability</SelectItem>
                                            <SelectItem value="Equity">Equity</SelectItem>
                                            <SelectItem value="Revenue">Revenue</SelectItem>
                                            <SelectItem value="Expense">Expense</SelectItem>
                                        </SelectContent>
                                    </Select>
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

                        {/* Classification */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Classification</CardTitle>
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
                                        }
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
