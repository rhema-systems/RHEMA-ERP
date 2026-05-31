'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Switch } from '@/components/ui/switch';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Settings, Lock, AlertTriangle, Save, DollarSign, Layers, Check, ChevronsUpDown } from 'lucide-react';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings, UpdateFinanceSettingsDto, Account } from '@/types/finance';
import { useToast } from '@/hooks/use-toast';
import Link from 'next/link';
import { cn } from '@/lib/utils';

interface AccountPickerProps {
    id: string;
    value?: string;
    placeholder: string;
    disabled?: boolean;
    accounts: Account[];
    onChange: (value?: string) => void;
}

function AccountPicker({ id, value, placeholder, disabled, accounts, onChange }: AccountPickerProps) {
    const [open, setOpen] = useState(false);
    const selected = accounts.find(a => a.id === value);

    return (
        <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
                <Button
                    id={id}
                    type="button"
                    variant="outline"
                    role="combobox"
                    aria-expanded={open}
                    disabled={disabled}
                    className="w-full justify-between font-normal"
                >
                    <span className="truncate">
                        {selected ? `${selected.accountCode} - ${selected.accountName}` : 'None'}
                    </span>
                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[420px] p-0" align="start">
                <Command>
                    <CommandInput placeholder={placeholder} />
                    <CommandList>
                        <CommandEmpty>No account found.</CommandEmpty>
                        <CommandGroup>
                            <CommandItem
                                value="none"
                                onSelect={() => {
                                    onChange(undefined);
                                    setOpen(false);
                                }}
                            >
                                <Check className={cn("mr-2 h-4 w-4", !value ? "opacity-100" : "opacity-0")} />
                                None
                            </CommandItem>
                            {accounts.map(account => (
                                <CommandItem
                                    key={account.id}
                                    value={`${account.accountCode} ${account.accountName}`}
                                    onSelect={() => {
                                        onChange(account.id);
                                        setOpen(false);
                                    }}
                                >
                                    <Check className={cn("mr-2 h-4 w-4", value === account.id ? "opacity-100" : "opacity-0")} />
                                    <span className="truncate">{account.accountCode} - {account.accountName}</span>
                                </CommandItem>
                            ))}
                        </CommandGroup>
                    </CommandList>
                </Command>
            </PopoverContent>
        </Popover>
    );
}

export default function FinanceSettingsPage() {
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const { toast } = useToast();

    // Form state
    const [formData, setFormData] = useState<UpdateFinanceSettingsDto>({
        coaType: 'Segmented', // Default to Segmented as enforced by backend
        baseCurrency: 'GHS',
        retainedEarningsAccountId: undefined,
        unrealizedGainLossAccountId: undefined,
        realizedGainLossAccountId: undefined,
        suspenseAccountId: undefined,
        controlAccountArId: undefined,
        controlAccountApId: undefined,
        controlAccountInventoryId: undefined,
        controlAccountPayrollId: undefined,
        controlAccountTaxId: undefined,
        migrationClearingAccountId: undefined,
        openingBalanceAutoRoutingEnabled: true,
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
                controlAccountArId: data.controlAccountArId,
                controlAccountApId: data.controlAccountApId,
                controlAccountInventoryId: data.controlAccountInventoryId,
                controlAccountPayrollId: data.controlAccountPayrollId,
                controlAccountTaxId: data.controlAccountTaxId,
                migrationClearingAccountId: data.migrationClearingAccountId,
                openingBalanceAutoRoutingEnabled: data.openingBalanceAutoRoutingEnabled ?? true,
            });

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



            {/* Account Format Configuration */}
            <Card>
                <CardHeader>
                    <CardTitle>Account Code Formatting</CardTitle>
                    <CardDescription>
                        Configure how account codes are displayed
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="space-y-2">
                        <Label htmlFor="accountSeparator">Account Separator</Label>
                        <Select
                            value={formData.accountSeparator || '-'}
                            onValueChange={(value) => setFormData({ ...formData, accountSeparator: value })}
                        >
                            <SelectTrigger id="accountSeparator" className="w-[180px]">
                                <SelectValue placeholder="Select separator" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="-">Dash ( - )</SelectItem>
                                <SelectItem value=".">Dot ( . )</SelectItem>
                                <SelectItem value="/">Slash ( / )</SelectItem>
                                <SelectItem value="_">Underscore ( _ )</SelectItem>
                            </SelectContent>
                        </Select>
                        <p className="text-sm text-muted-foreground">
                            Character used to separate segments (e.g. 100-200-300 or 100.200.300)
                        </p>
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
            </Card >

            {/* Default Accounts */}
            < Card >
                <CardHeader>
                    <CardTitle>Default Accounts</CardTitle>
                    <CardDescription>
                        Configure default GL accounts for system operations
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="space-y-2">
                        <Label htmlFor="retainedEarnings">Retained Earnings Account</Label>
                        <AccountPicker
                            id="retainedEarnings"
                            value={formData.retainedEarningsAccountId}
                            placeholder="Search equity accounts..."
                            disabled={settings?.transactionsExist}
                            accounts={accounts.filter(a => a.accountType === 'Equity')}
                            onChange={(value) => setFormData({ ...formData, retainedEarningsAccountId: value })}
                        />
                        <p className="text-sm text-muted-foreground">
                            Used for year-end closing entries
                        </p>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="unrealizedGainLoss">Unrealized Gain/Loss Account</Label>
                        <AccountPicker
                            id="unrealizedGainLoss"
                            value={formData.unrealizedGainLossAccountId}
                            placeholder="Search revenue/expense accounts..."
                            disabled={settings?.transactionsExist}
                            accounts={accounts.filter(a => a.accountType === 'Revenue' || a.accountType === 'Expense')}
                            onChange={(value) => setFormData({ ...formData, unrealizedGainLossAccountId: value })}
                        />
                        <p className="text-sm text-muted-foreground">
                            Used for currency revaluation entries
                        </p>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="realizedGainLoss">Realized Gain/Loss Account</Label>
                        <AccountPicker
                            id="realizedGainLoss"
                            value={formData.realizedGainLossAccountId}
                            placeholder="Search revenue/expense accounts..."
                            disabled={settings?.transactionsExist}
                            accounts={accounts.filter(a => a.accountType === 'Revenue' || a.accountType === 'Expense')}
                            onChange={(value) => setFormData({ ...formData, realizedGainLossAccountId: value })}
                        />
                        <p className="text-sm text-muted-foreground">
                            Used for settled foreign currency transactions
                        </p>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="migrationClearingAccount">Migration Clearing Account</Label>
                        <AccountPicker
                            id="migrationClearingAccount"
                            value={formData.migrationClearingAccountId}
                            placeholder="Search clearing/suspense accounts..."
                            disabled={settings?.transactionsExist}
                            accounts={accounts}
                            onChange={(value) => setFormData({ ...formData, migrationClearingAccountId: value })}
                        />
                        <p className="text-sm text-muted-foreground">
                            Used as the offset account for opening-balance migration postings.
                        </p>
                    </div>
                    <div className="rounded-md border p-3">
                        <div className="flex items-center justify-between">
                            <div className="space-y-1">
                                <Label htmlFor="openingBalanceAutoRouting">Opening Balance Auto-Routing</Label>
                                <p className="text-sm text-muted-foreground">
                                    When enabled, Opening Balance postings in GL/Subledger auto-route balancing/offset lines to Migration Clearing Account.
                                </p>
                            </div>
                            <Switch
                                id="openingBalanceAutoRouting"
                                checked={formData.openingBalanceAutoRoutingEnabled ?? true}
                                onCheckedChange={(checked) =>
                                    setFormData({ ...formData, openingBalanceAutoRoutingEnabled: checked })
                                }
                            />
                        </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-4 border-t">
                        <div className="space-y-2">
                            <Label htmlFor="controlAccountAr">Accounts Receivable (AR) Control</Label>
                            <AccountPicker
                                id="controlAccountAr"
                                value={formData.controlAccountArId}
                                placeholder="Search AR control accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Asset')}
                                onChange={(value) => setFormData({ ...formData, controlAccountArId: value })}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="controlAccountAp">Accounts Payable (AP) Control</Label>
                            <AccountPicker
                                id="controlAccountAp"
                                value={formData.controlAccountApId}
                                placeholder="Search AP control accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Liability')}
                                onChange={(value) => setFormData({ ...formData, controlAccountApId: value })}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="controlAccountInventory">Inventory Control</Label>
                            <AccountPicker
                                id="controlAccountInventory"
                                value={formData.controlAccountInventoryId}
                                placeholder="Search inventory control accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Asset')}
                                onChange={(value) => setFormData({ ...formData, controlAccountInventoryId: value })}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="controlAccountPayroll">Payroll/Salaries Payable</Label>
                            <AccountPicker
                                id="controlAccountPayroll"
                                value={formData.controlAccountPayrollId}
                                placeholder="Search payroll control accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Liability')}
                                onChange={(value) => setFormData({ ...formData, controlAccountPayrollId: value })}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="controlAccountTax">Tax/VAT Control</Label>
                            <AccountPicker
                                id="controlAccountTax"
                                value={formData.controlAccountTaxId}
                                placeholder="Search tax control accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Liability')}
                                onChange={(value) => setFormData({ ...formData, controlAccountTaxId: value })}
                            />
                        </div>
                    </div>


                </CardContent>
            </Card >
        </div >
    );
}
