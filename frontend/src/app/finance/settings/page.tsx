'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Settings, Lock, AlertTriangle, Save, DollarSign, Layers } from 'lucide-react';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings, UpdateFinanceSettingsDto, Account } from '@/types/finance';
import { useToast } from '@/hooks/use-toast';
import { FinanceDemoModeToggle } from '@/components/finance/finance-demo-mode-toggle';
import Link from 'next/link';

export default function FinanceSettingsPage() {
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [canChangeCOA, setCanChangeCOA] = useState(true);
    const { toast } = useToast();

    // Form state
    const [formData, setFormData] = useState<UpdateFinanceSettingsDto>({
        coaType: 'Standard',
        baseCurrency: 'GHS',
        retainedEarningsAccountId: undefined,
        unrealizedGainLossAccountId: undefined,
        realizedGainLossAccountId: undefined,
        suspenseAccountId: undefined,
    });

    useEffect(() => {
        loadSettings();
    }, []);

    // Reload accounts when COA type changes
    useEffect(() => {
        if (formData.coaType) {
            loadAccounts(formData.coaType);
        }
    }, [formData.coaType]);

    const loadSettings = async () => {
        try {
            setLoading(true);
            const data = await financeDataService.getFinanceSettings();
            setSettings(data);
            setFormData({
                coaType: data.coaType,
                baseCurrency: data.baseCurrency,
                retainedEarningsAccountId: data.retainedEarningsAccountId,
                unrealizedGainLossAccountId: data.unrealizedGainLossAccountId,
                realizedGainLossAccountId: data.realizedGainLossAccountId,
                suspenseAccountId: data.suspenseAccountId,
            });

            // In demo mode, COA type can always be changed
            setCanChangeCOA(true);

            // Load accounts for the current COA type
            await loadAccounts(data.coaType);
        } catch (error: any) {
            console.error('Error loading settings:', error);
            toast({
                title: 'Error',
                description: error?.message || 'Failed to load finance settings',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    const loadAccounts = async (coaType: 'Standard' | 'Segmented') => {
        try {
            const accountsData = await financeDataService.getAccounts({ coaType });
            setAccounts(accountsData);
        } catch (error) {
            console.error('Error loading accounts:', error);
        }
    };

    // Account ID mappings between Standard and Segmented COA
    const ACCOUNT_ID_MAP: Record<string, string> = {
        // Standard -> Segmented
        'acc-3100': 'seg-acc-retained',
        'acc-7100': 'seg-acc-unrealized',
        'acc-7200': 'seg-acc-realized',
        'acc-9999': 'seg-acc-suspense',
        // Segmented -> Standard
        'seg-acc-retained': 'acc-3100',
        'seg-acc-unrealized': 'acc-7100',
        'seg-acc-realized': 'acc-7200',
        'seg-acc-suspense': 'acc-9999',
    };

    const handleCOATypeChange = (newCoaType: 'Standard' | 'Segmented') => {
        // Map the account IDs to the new COA type equivalents
        const mapAccountId = (id: string | undefined): string | undefined => {
            if (!id) return undefined;
            return ACCOUNT_ID_MAP[id] || undefined;
        };

        setFormData({
            ...formData,
            coaType: newCoaType,
            retainedEarningsAccountId: mapAccountId(formData.retainedEarningsAccountId),
            unrealizedGainLossAccountId: mapAccountId(formData.unrealizedGainLossAccountId),
            realizedGainLossAccountId: mapAccountId(formData.realizedGainLossAccountId),
            suspenseAccountId: mapAccountId(formData.suspenseAccountId),
        });
    };

    const handleSave = async () => {
        try {
            setSaving(true);
            const updated = await financeDataService.updateFinanceSettings(formData);
            setSettings(updated);

            toast({
                title: 'Success',
                description: 'Finance settings updated successfully',
            });

            // Reload to get updated lock status
            await loadSettings();
        } catch (error: any) {
            console.error('Error saving settings:', error);
            toast({
                title: 'Error',
                description: error?.message || 'Failed to update settings',
                variant: 'destructive',
            });
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return (
            <div className="space-y-6">
                <div className="h-8 bg-gray-200 rounded w-64 animate-pulse" />
                <div className="h-96 bg-gray-100 rounded animate-pulse" />
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Settings className="h-8 w-8" />
                        Finance Settings
                    </h1>
                    <p className="text-muted-foreground">
                        Configure finance module settings and chart of accounts structure
                    </p>
                </div>
                <Button onClick={handleSave} disabled={saving}>
                    <Save className="mr-2 h-4 w-4" />
                    {saving ? 'Saving...' : 'Save Settings'}
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
                        <BreadcrumbPage>Settings</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Demo Mode Toggle - TEMPORARILY HIDDEN FOR DEMO
            <FinanceDemoModeToggle />
            */}

            {/* Quick Links */}
            <Card>
                <CardHeader className="pb-3">
                    <div className="flex items-center justify-between">
                        <CardTitle className="text-base">Quick Links</CardTitle>
                        <span className={`text-xs px-2 py-1 rounded-full ${formData.coaType === 'Segmented' ? 'bg-purple-100 text-purple-700' : 'bg-blue-100 text-blue-700'}`}>
                            {formData.coaType} COA
                        </span>
                    </div>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-wrap gap-2">
                        {formData.coaType === 'Segmented' && (
                            <Link href="/finance/settings/segments">
                                <Button variant="outline" size="sm">
                                    <Layers className="mr-2 h-4 w-4" />
                                    Configure Segments
                                </Button>
                            </Link>
                        )}
                        <Link href="/finance/accounts">
                            <Button variant="outline" size="sm">
                                <DollarSign className="mr-2 h-4 w-4" />
                                Chart of Accounts
                            </Button>
                        </Link>
                    </div>
                    {formData.coaType === 'Standard' && (
                        <p className="text-xs text-muted-foreground mt-2">
                            Segment configuration is only available for Segmented COA
                        </p>
                    )}
                </CardContent>
            </Card>

            {/* COA Lock Warning */}
            {settings?.coaConfigurationLocked && (
                <Alert variant="destructive">
                    <Lock className="h-4 w-4" />
                    <AlertTitle>COA Configuration Locked</AlertTitle>
                    <AlertDescription>
                        The Chart of Accounts type cannot be changed because accounts have already been created.
                        To change the COA type, you must delete all existing accounts first.
                    </AlertDescription>
                </Alert>
            )}

            {/* Chart of Accounts Configuration */}
            <Card>
                <CardHeader>
                    <CardTitle>Chart of Accounts Structure</CardTitle>
                    <CardDescription>
                        Select the type of chart of accounts structure for your organization.
                        This cannot be changed once accounts are created.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-6">
                    <div className="space-y-4">
                        <Label>COA Type</Label>
                        <RadioGroup
                            value={formData.coaType}
                            onValueChange={(value) => handleCOATypeChange(value as 'Standard' | 'Segmented')}
                            disabled={!canChangeCOA || settings?.coaConfigurationLocked}
                        >
                            <div className="flex items-center space-x-2 border rounded-lg p-4">
                                <RadioGroupItem value="Standard" id="standard" />
                                <Label htmlFor="standard" className="flex-1 cursor-pointer">
                                    <div className="font-semibold">Standard COA</div>
                                    <div className="text-sm text-muted-foreground">
                                        Simple hierarchical structure with main accounts and sub-accounts.
                                        Suitable for small to medium organizations.
                                    </div>
                                </Label>
                            </div>
                            <div className="flex items-center space-x-2 border rounded-lg p-4">
                                <RadioGroupItem value="Segmented" id="segmented" />
                                <Label htmlFor="segmented" className="flex-1 cursor-pointer">
                                    <div className="font-semibold">Segmented COA</div>
                                    <div className="text-sm text-muted-foreground">
                                        Advanced structure with up to 20 segments (e.g., Department, Cost Center, Project).
                                        Provides powerful reporting dimensions for large organizations.
                                    </div>
                                </Label>
                            </div>
                        </RadioGroup>
                        {!canChangeCOA && (
                            <Alert>
                                <AlertTriangle className="h-4 w-4" />
                                <AlertDescription>
                                    COA type is locked because {settings?.coaConfigurationLocked ? 'accounts exist' : 'configuration is locked'}.
                                </AlertDescription>
                            </Alert>
                        )}
                    </div>
                </CardContent>
            </Card>

            {/* Currency Configuration */}
            <Card>
                <CardHeader>
                    <CardTitle>Currency Settings</CardTitle>
                    <CardDescription>
                        Configure the base currency for your organization
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="space-y-2">
                        <Label htmlFor="baseCurrency">Base Currency</Label>
                        <Select
                            value={formData.baseCurrency}
                            onValueChange={(value) => setFormData({ ...formData, baseCurrency: value })}
                        >
                            <SelectTrigger id="baseCurrency">
                                <SelectValue placeholder="Select base currency" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                <SelectItem value="USD">USD - US Dollar</SelectItem>
                                <SelectItem value="EUR">EUR - Euro</SelectItem>
                                <SelectItem value="GBP">GBP - British Pound</SelectItem>
                            </SelectContent>
                        </Select>
                        <p className="text-sm text-muted-foreground">
                            The base currency is used for financial reporting and consolidation
                        </p>
                    </div>
                </CardContent>
            </Card>

            {/* Default Accounts */}
            <Card>
                <CardHeader>
                    <CardTitle>Default Accounts</CardTitle>
                    <CardDescription>
                        Configure default GL accounts for system operations
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="space-y-2">
                        <Label htmlFor="retainedEarnings">Retained Earnings Account</Label>
                        <Select
                            value={formData.retainedEarningsAccountId || '__none__'}
                            onValueChange={(value) => setFormData({ ...formData, retainedEarningsAccountId: value === '__none__' ? undefined : value })}
                        >
                            <SelectTrigger id="retainedEarnings">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="__none__">None</SelectItem>
                                {accounts
                                    .filter(a => a.accountType === 'Equity')
                                    .map(account => (
                                        <SelectItem key={account.id} value={account.id}>
                                            {account.accountCode} - {account.accountName}
                                        </SelectItem>
                                    ))}
                            </SelectContent>
                        </Select>
                        <p className="text-sm text-muted-foreground">
                            Used for year-end closing entries
                        </p>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="unrealizedGainLoss">Unrealized Gain/Loss Account</Label>
                        <Select
                            value={formData.unrealizedGainLossAccountId || '__none__'}
                            onValueChange={(value) => setFormData({ ...formData, unrealizedGainLossAccountId: value === '__none__' ? undefined : value })}
                        >
                            <SelectTrigger id="unrealizedGainLoss">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="__none__">None</SelectItem>
                                {accounts
                                    .filter(a => a.accountType === 'Revenue' || a.accountType === 'Expense')
                                    .map(account => (
                                        <SelectItem key={account.id} value={account.id}>
                                            {account.accountCode} - {account.accountName}
                                        </SelectItem>
                                    ))}
                            </SelectContent>
                        </Select>
                        <p className="text-sm text-muted-foreground">
                            Used for currency revaluation entries
                        </p>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="realizedGainLoss">Realized Gain/Loss Account</Label>
                        <Select
                            value={formData.realizedGainLossAccountId || '__none__'}
                            onValueChange={(value) => setFormData({ ...formData, realizedGainLossAccountId: value === '__none__' ? undefined : value })}
                        >
                            <SelectTrigger id="realizedGainLoss">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="__none__">None</SelectItem>
                                {accounts
                                    .filter(a => a.accountType === 'Revenue' || a.accountType === 'Expense')
                                    .map(account => (
                                        <SelectItem key={account.id} value={account.id}>
                                            {account.accountCode} - {account.accountName}
                                        </SelectItem>
                                    ))}
                            </SelectContent>
                        </Select>
                        <p className="text-sm text-muted-foreground">
                            Used for settled foreign currency transactions
                        </p>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="suspense">Suspense Account</Label>
                        <Select
                            value={formData.suspenseAccountId || '__none__'}
                            onValueChange={(value) => setFormData({ ...formData, suspenseAccountId: value === '__none__' ? undefined : value })}
                        >
                            <SelectTrigger id="suspense">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="__none__">None</SelectItem>
                                {accounts
                                    .filter(a => a.accountType === 'Asset' || a.accountType === 'Liability')
                                    .map(account => (
                                        <SelectItem key={account.id} value={account.id}>
                                            {account.accountCode} - {account.accountName}
                                        </SelectItem>
                                    ))}
                            </SelectContent>
                        </Select>
                        <p className="text-sm text-muted-foreground">
                            Used for unbalanced or unidentified transactions
                        </p>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
