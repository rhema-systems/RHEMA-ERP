'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Settings, Lock, AlertTriangle, Save, DollarSign } from 'lucide-react';
import { financeService } from '@/services/finance.service';
import type { FinanceSettings, UpdateFinanceSettingsDto, Account } from '@/types/finance';
import { useToast } from '@/hooks/use-toast';

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
        loadAccounts();
    }, []);

    const loadSettings = async () => {
        try {
            setLoading(true);
            const data = await financeService.getSettings();
            setSettings(data);
            setFormData({
                coaType: data.coaType,
                baseCurrency: data.baseCurrency,
                retainedEarningsAccountId: data.retainedEarningsAccountId,
                unrealizedGainLossAccountId: data.unrealizedGainLossAccountId,
                realizedGainLossAccountId: data.realizedGainLossAccountId,
                suspenseAccountId: data.suspenseAccountId,
            });

            // Check if COA type can be changed
            const canChange = await financeService.canChangeCOAType();
            setCanChangeCOA(canChange);
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

    const loadAccounts = async () => {
        try {
            const result = await financeService.getAccounts({ pageSize: 1000 });
            setAccounts(result.items);
        } catch (error) {
            console.error('Error loading accounts:', error);
        }
    };

    const handleSave = async () => {
        try {
            setSaving(true);
            const updated = await financeService.updateSettings(formData);
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
                            onValueChange={(value) => setFormData({ ...formData, coaType: value as 'Standard' | 'Segmented' })}
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
                            value={formData.retainedEarningsAccountId || ''}
                            onValueChange={(value) => setFormData({ ...formData, retainedEarningsAccountId: value || undefined })}
                        >
                            <SelectTrigger id="retainedEarnings">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="">None</SelectItem>
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
                            value={formData.unrealizedGainLossAccountId || ''}
                            onValueChange={(value) => setFormData({ ...formData, unrealizedGainLossAccountId: value || undefined })}
                        >
                            <SelectTrigger id="unrealizedGainLoss">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="">None</SelectItem>
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
                            value={formData.realizedGainLossAccountId || ''}
                            onValueChange={(value) => setFormData({ ...formData, realizedGainLossAccountId: value || undefined })}
                        >
                            <SelectTrigger id="realizedGainLoss">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="">None</SelectItem>
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
                            value={formData.suspenseAccountId || ''}
                            onValueChange={(value) => setFormData({ ...formData, suspenseAccountId: value || undefined })}
                        >
                            <SelectTrigger id="suspense">
                                <SelectValue placeholder="Select account" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="">None</SelectItem>
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
