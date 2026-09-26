'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Switch } from '@/components/ui/switch';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Settings, Lock, AlertTriangle, Save, DollarSign, Layers, Check, ChevronsUpDown, Banknote, ShieldCheck, Undo2, Tags, BookOpen } from 'lucide-react';
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
    allowClear?: boolean;
    accounts: Account[];
    onChange: (value?: string) => void;
}

function AccountPicker({ id, value, placeholder, disabled, allowClear = true, accounts, onChange }: AccountPickerProps) {
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
                            {allowClear && <CommandItem
                                value="none"
                                onSelect={() => {
                                    onChange(undefined);
                                    setOpen(false);
                                }}
                            >
                                <Check className={cn("mr-2 h-4 w-4", !value ? "opacity-100" : "opacity-0")} />
                                None
                            </CommandItem>}
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
        whtStatutoryYearStartMonth: 1,
        whtStatutoryYearStartDay: 1,
        retainedEarningsAccountId: undefined,
        unrealizedGainLossAccountId: undefined,
        unrealizedFxGainAccountId: undefined,
        unrealizedFxLossAccountId: undefined,
        realizedGainLossAccountId: undefined,
        realizedFxGainAccountId: undefined,
        realizedFxLossAccountId: undefined,
        suspenseAccountId: undefined,
        controlAccountArId: undefined,
        controlAccountApId: undefined,
        controlAccountInventoryId: undefined,
        returnToVendorClearingAccountId: undefined,
        purchaseReturnVarianceAccountId: undefined,
        controlAccountPayrollId: undefined,
        controlAccountTaxId: undefined,
        controlAccountGRVAccrualId: undefined,
        discountAllowedAccountId: undefined,
        discountReceivedAccountId: undefined,
        migrationClearingAccountId: undefined,
        bankDepositPolicy: 'DepositIntact',
        requireBankDepositPrimaryEvidence: true,
        autoPostBankDepositAfterApproval: true,
        bankStatementMatchDateToleranceDays: 3,
        chequeClearingPeriodDays: 3,
        defaultReturnedChequeChargeTreatment: 'CustomerRecoverable',
        directionalExchangeRatePolicyEnabled: false,
        defaultTransactionQuoteSide: 'Mid',
        arInvoiceQuoteSide: 'Mid',
        arSettlementQuoteSide: 'Buying',
        apInvoiceQuoteSide: 'Mid',
        apSettlementQuoteSide: 'Selling',
        closingQuoteSide: 'Mid',
        requireExchangeRateOverrideApproval: true,
        // TDC default: corrections post in the current open period and require a substantive
        // explanation. Scope enforcement remains off until administrators have created grants.
        reversalDatePolicy: 'CurrentOpenPeriod',
        minimumReversalReasonLength: 20,
        enforceFinanceAccessScopes: false,
        requireDepreciationBeforePeriodClose: true,
        apInvoicePriceTolerancePercent: 1,
        apInvoiceQuantityTolerancePercent: 1,
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
                whtStatutoryYearStartMonth: data.whtStatutoryYearStartMonth ?? 1,
                whtStatutoryYearStartDay: data.whtStatutoryYearStartDay ?? 1,
                retainedEarningsAccountId: data.retainedEarningsAccountId,
                unrealizedGainLossAccountId: data.unrealizedGainLossAccountId,
                unrealizedFxGainAccountId: data.unrealizedFxGainAccountId,
                unrealizedFxLossAccountId: data.unrealizedFxLossAccountId,
                realizedGainLossAccountId: data.realizedGainLossAccountId,
                realizedFxGainAccountId: data.realizedFxGainAccountId,
                realizedFxLossAccountId: data.realizedFxLossAccountId,
                suspenseAccountId: data.suspenseAccountId,
                controlAccountArId: data.controlAccountArId,
                controlAccountApId: data.controlAccountApId,
                controlAccountInventoryId: data.controlAccountInventoryId,
                returnToVendorClearingAccountId: data.returnToVendorClearingAccountId,
                purchaseReturnVarianceAccountId: data.purchaseReturnVarianceAccountId,
                controlAccountPayrollId: data.controlAccountPayrollId,
                controlAccountTaxId: data.controlAccountTaxId,
                controlAccountGRVAccrualId: data.controlAccountGRVAccrualId,
                discountAllowedAccountId: data.discountAllowedAccountId,
                discountReceivedAccountId: data.discountReceivedAccountId,
                migrationClearingAccountId: data.migrationClearingAccountId,
                bankDepositPolicy: data.bankDepositPolicy ?? 'DepositIntact',
                requireBankDepositPrimaryEvidence: data.requireBankDepositPrimaryEvidence ?? true,
                autoPostBankDepositAfterApproval: data.autoPostBankDepositAfterApproval ?? true,
                maximumDepositDeductionAmount: data.maximumDepositDeductionAmount,
                maximumDepositDeductionPercentage: data.maximumDepositDeductionPercentage,
                bankStatementMatchDateToleranceDays: data.bankStatementMatchDateToleranceDays ?? 3,
                chequeClearingPeriodDays: data.chequeClearingPeriodDays ?? 3,
                returnedChequeBankChargeAccountId: data.returnedChequeBankChargeAccountId,
                defaultReturnedChequeChargeTreatment: data.defaultReturnedChequeChargeTreatment ?? 'CustomerRecoverable',
                directionalExchangeRatePolicyEnabled: data.directionalExchangeRatePolicyEnabled ?? false,
                defaultTransactionQuoteSide: data.defaultTransactionQuoteSide ?? 'Mid',
                arInvoiceQuoteSide: data.arInvoiceQuoteSide ?? 'Mid',
                arSettlementQuoteSide: data.arSettlementQuoteSide ?? 'Buying',
                apInvoiceQuoteSide: data.apInvoiceQuoteSide ?? 'Mid',
                apSettlementQuoteSide: data.apSettlementQuoteSide ?? 'Selling',
                closingQuoteSide: data.closingQuoteSide ?? 'Mid',
                requireExchangeRateOverrideApproval: true,
                reversalDatePolicy: data.reversalDatePolicy ?? 'CurrentOpenPeriod',
                minimumReversalReasonLength: data.minimumReversalReasonLength ?? 20,
                enforceFinanceAccessScopes: data.enforceFinanceAccessScopes ?? false,
                requireDepreciationBeforePeriodClose: data.requireDepreciationBeforePeriodClose ?? true,
                apInvoicePriceTolerancePercent: data.apInvoicePriceTolerancePercent ?? 1,
                apInvoiceQuantityTolerancePercent: data.apInvoiceQuantityTolerancePercent ?? 1,
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
        const tolerances = [
            formData.apInvoicePriceTolerancePercent,
            formData.apInvoiceQuantityTolerancePercent,
        ];
        if (tolerances.some(value => value == null || !Number.isFinite(value) || value < 0 || value > 100)) {
            toast({
                title: 'Invalid matching tolerance',
                description: 'AP invoice matching tolerances must be between 0 and 100 percent.',
                variant: 'destructive',
            });
            return;
        }
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
                        <Link href="/finance/settings/account-classifications">
                            <Button variant="outline" size="sm">
                                <Tags className="mr-2 h-4 w-4" />
                                Account Classifications
                            </Button>
                        </Link>
                        <Link href="/finance/settings/accounting-books">
                            <Button variant="outline" size="sm">
                                <BookOpen className="mr-2 h-4 w-4" />
                                Accounting Books
                            </Button>
                        </Link>
                        <Link href="/finance/accounts">
                            <Button variant="outline" size="sm">
                                <DollarSign className="mr-2 h-4 w-4" />
                                Chart of Accounts
                            </Button>
                        </Link>
                        <Link href="/finance/cash/liquidity-accounts">
                            <Button variant="outline" size="sm">
                                <Banknote className="mr-2 h-4 w-4" />
                                Banking Setup
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

            <Card>
                <CardHeader>
                    <CardTitle>Withholding Tax Statutory Year</CardTitle>
                    <CardDescription>
                        Sets the annual boundary used for supplier contract/category threshold accumulation.
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-2">
                            <Label htmlFor="whtYearStartMonth">Start month</Label>
                            <Input
                                id="whtYearStartMonth"
                                type="number"
                                min={1}
                                max={12}
                                value={formData.whtStatutoryYearStartMonth ?? 1}
                                onChange={event => setFormData({ ...formData, whtStatutoryYearStartMonth: Number(event.target.value) })}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="whtYearStartDay">Start day</Label>
                            <Input
                                id="whtYearStartDay"
                                type="number"
                                min={1}
                                max={31}
                                value={formData.whtStatutoryYearStartDay ?? 1}
                                onChange={event => setFormData({ ...formData, whtStatutoryYearStartDay: Number(event.target.value) })}
                            />
                        </div>
                    </div>
                    <p className="mt-3 text-sm text-muted-foreground">
                        January 1 is the default. Finance validates that the selected month/day is a real calendar date.
                    </p>
                </CardContent>
            </Card>

            <Card>
                <CardHeader>
                    <CardTitle>Exchange Rate Policy</CardTitle>
                    <CardDescription>
                        Buying and selling are defined from the bank/provider perspective. Mid is the accounting reference rate.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                    <div className="flex items-center justify-between rounded-md border p-4">
                        <div>
                            <Label htmlFor="directionalRates">Enforce directional rates</Label>
                            <p className="text-sm text-muted-foreground">
                                Saving is blocked until every active foreign currency has approved Daily rates for all configured transaction quote sides.
                            </p>
                        </div>
                        <Switch
                            id="directionalRates"
                            checked={formData.directionalExchangeRatePolicyEnabled ?? false}
                            onCheckedChange={checked => setFormData({ ...formData, directionalExchangeRatePolicyEnabled: checked })}
                        />
                    </div>
                    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                        {([
                            ['defaultTransactionQuoteSide', 'Manual GL transactions'],
                            ['arInvoiceQuoteSide', 'AR invoice recognition'],
                            ['arSettlementQuoteSide', 'AR cash settlement'],
                            ['apInvoiceQuoteSide', 'AP invoice recognition'],
                            ['apSettlementQuoteSide', 'AP cash settlement'],
                            ['closingQuoteSide', 'Period-end revaluation'],
                        ] as const).map(([field, label]) => (
                            <div className="space-y-2" key={field}>
                                <Label>{label}</Label>
                                <Select
                                    value={formData[field] ?? 'Mid'}
                                    disabled={!formData.directionalExchangeRatePolicyEnabled}
                                    onValueChange={value => setFormData({ ...formData, [field]: value as UpdateFinanceSettingsDto[typeof field] })}
                                >
                                    <SelectTrigger><SelectValue /></SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Mid">Mid / Reference</SelectItem>
                                        <SelectItem value="Buying">Buying (bank buys FX)</SelectItem>
                                        <SelectItem value="Selling">Selling (bank sells FX)</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        ))}
                    </div>
                    <Alert>
                        <Lock className="h-4 w-4" />
                        <AlertTitle>Override control is mandatory</AlertTitle>
                        <AlertDescription>
                            A document-level rate override requires an authorised approver and a recorded reason. One approved override is applied consistently to all balancing lines.
                        </AlertDescription>
                    </Alert>
                </CardContent>
            </Card>

            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2"><Undo2 className="h-5 w-5" /> Posting Correction & Access Controls</CardTitle>
                    <CardDescription>
                        Tenant policy for correcting posted Finance documents and restricting operational data by bank account.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                    <div className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-2">
                            <Label>Reversal date policy</Label>
                            <Select
                                value={formData.reversalDatePolicy ?? 'CurrentOpenPeriod'}
                                onValueChange={value => setFormData({ ...formData, reversalDatePolicy: value as UpdateFinanceSettingsDto['reversalDatePolicy'] })}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="CurrentOpenPeriod">Current open period</SelectItem>
                                    <SelectItem value="OriginalDocumentPeriodIfOpen">Original period, only if still open</SelectItem>
                                </SelectContent>
                            </Select>
                            <p className="text-xs text-muted-foreground">
                                Closed history is never reopened implicitly; corrections fall forward to the latest open period.
                            </p>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="minimumReversalReasonLength">Minimum reversal reason length</Label>
                            <Input
                                id="minimumReversalReasonLength"
                                type="number"
                                min={10}
                                max={500}
                                value={formData.minimumReversalReasonLength ?? 20}
                                onChange={event => setFormData({ ...formData, minimumReversalReasonLength: Number(event.target.value) })}
                            />
                            <p className="text-xs text-muted-foreground">Applies to posted-document corrections and is enforced by the API.</p>
                        </div>
                    </div>

                    <div className="flex flex-col gap-4 rounded-md border p-4 md:flex-row md:items-center md:justify-between">
                        <div>
                            <Label htmlFor="enforceFinanceAccessScopes">Enforce Finance access scopes</Label>
                            <p className="max-w-2xl text-sm text-muted-foreground">
                                When enabled, Finance users only see and operate on bank accounts covered by an effective grant. Tenant administrators retain recovery access.
                            </p>
                        </div>
                        <div className="flex items-center gap-3">
                            <Link href="/administration/finance/access-scopes">
                                <Button type="button" variant="outline" size="sm"><ShieldCheck className="mr-2 h-4 w-4" /> Manage Grants</Button>
                            </Link>
                            <Switch
                                id="enforceFinanceAccessScopes"
                                checked={formData.enforceFinanceAccessScopes ?? false}
                                onCheckedChange={checked => setFormData({ ...formData, enforceFinanceAccessScopes: checked })}
                            />
                        </div>
                    </div>
                    <div className="flex items-center justify-between gap-4 rounded-md border p-4">
                        <div>
                            <Label htmlFor="requireDepreciationBeforePeriodClose">Require depreciation before period close</Label>
                            <p className="max-w-2xl text-sm text-muted-foreground">
                                TDC default: due or failed fixed-asset depreciation blocks close. Disable only under an approved Finance policy exception.
                            </p>
                        </div>
                        <Switch
                            id="requireDepreciationBeforePeriodClose"
                            checked={formData.requireDepreciationBeforePeriodClose ?? true}
                            onCheckedChange={checked => setFormData({ ...formData, requireDepreciationBeforePeriodClose: checked })}
                        />
                    </div>
                    <Alert variant="default">
                        <AlertTriangle className="h-4 w-4" />
                        <AlertTitle>Staged activation</AlertTitle>
                        <AlertDescription>
                            Create and review at least one active grant before enabling enforcement. The backend blocks activation when no grant exists.
                        </AlertDescription>
                    </Alert>
                </CardContent>
            </Card>

            <Card>
                <CardHeader>
                    <CardTitle>Banking & Settlement Controls</CardTitle>
                    <CardDescription>
                        Tenant policy for net banking, deposit evidence, reconciliation tolerances, and returned cheques.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                    <div className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-2">
                            <Label>Deposit policy</Label>
                            <Select
                                value={formData.bankDepositPolicy ?? 'DepositIntact'}
                                onValueChange={(value) => setFormData({ ...formData, bankDepositPolicy: value as UpdateFinanceSettingsDto['bankDepositPolicy'] })}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="DepositIntact">Deposit intact — no deductions</SelectItem>
                                    <SelectItem value="ControlledNetBanking">Controlled net banking</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Returned-cheque charge default</Label>
                            <Select
                                value={formData.defaultReturnedChequeChargeTreatment ?? 'CustomerRecoverable'}
                                onValueChange={(value) => setFormData({ ...formData, defaultReturnedChequeChargeTreatment: value as UpdateFinanceSettingsDto['defaultReturnedChequeChargeTreatment'] })}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="CustomerRecoverable">Recover from customer</SelectItem>
                                    <SelectItem value="BankChargeExpense">Bank charge expense</SelectItem>
                                    <SelectItem value="Split">Split at case capture</SelectItem>
                                </SelectContent>
                            </Select>
                            <p className="text-xs text-muted-foreground">Initial treatment on a new returned-cheque case. Users may change it at case capture.</p>
                        </div>
                    </div>
                    <div className="grid gap-4 md:grid-cols-2">
                        <div className="flex items-center justify-between rounded-md border p-3">
                            <div><Label htmlFor="bankEvidence">Require primary bank evidence</Label><p className="text-xs text-muted-foreground">Deposit slip or bank advice before submission.</p></div>
                            <Switch id="bankEvidence" checked={formData.requireBankDepositPrimaryEvidence ?? true} onCheckedChange={checked => setFormData({ ...formData, requireBankDepositPrimaryEvidence: checked })} />
                        </div>
                        <div className="flex items-center justify-between rounded-md border p-3">
                            <div><Label htmlFor="bankAutoPost">Post after final approval</Label><p className="text-xs text-muted-foreground">Chief Accountant approval creates the bank transaction.</p></div>
                            <Switch id="bankAutoPost" checked={formData.autoPostBankDepositAfterApproval ?? true} onCheckedChange={checked => setFormData({ ...formData, autoPostBankDepositAfterApproval: checked })} />
                        </div>
                    </div>
                    {formData.bankDepositPolicy === 'ControlledNetBanking' && (
                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2"><Label>Maximum deduction amount</Label><Input type="number" min={0} step="0.01" value={formData.maximumDepositDeductionAmount ?? ''} onChange={event => setFormData({ ...formData, maximumDepositDeductionAmount: event.target.value ? Number(event.target.value) : undefined })} /></div>
                            <div className="space-y-2"><Label>Maximum deduction percentage</Label><Input type="number" min={0} max={100} step="0.01" value={formData.maximumDepositDeductionPercentage ?? ''} onChange={event => setFormData({ ...formData, maximumDepositDeductionPercentage: event.target.value ? Number(event.target.value) : undefined })} /></div>
                        </div>
                    )}
                    <div className="grid gap-4 md:grid-cols-3">
                        <div className="space-y-2"><Label>Statement date tolerance (calendar days)</Label><Input type="number" min={0} max={30} value={formData.bankStatementMatchDateToleranceDays ?? 3} onChange={event => setFormData({ ...formData, bankStatementMatchDateToleranceDays: Number(event.target.value) })} /><p className="text-xs text-muted-foreground">Limits auto-match candidates by book-to-statement date difference. Manual review can still match an exception.</p></div>
                        <div className="space-y-2"><Label>Cheque clearing period (calendar days)</Label><Input type="number" min={0} max={90} value={formData.chequeClearingPeriodDays ?? 3} onChange={event => setFormData({ ...formData, chequeClearingPeriodDays: Number(event.target.value) })} /><p className="text-xs text-muted-foreground">Shows an advisory expected clearing date for cheque receipts; it does not post or settle them automatically.</p></div>
                        <div className="space-y-2"><Label>Returned-cheque bank charge GL</Label><AccountPicker id="returnedChequeBankCharge" value={formData.returnedChequeBankChargeAccountId} placeholder="Search expense accounts..." accounts={accounts.filter(account => account.accountType === 'Expense')} onChange={value => setFormData({ ...formData, returnedChequeBankChargeAccountId: value })} /></div>
                    </div>
                    <Alert>
                        <AlertTriangle className="h-4 w-4" />
                        <AlertTitle>Liquidity master data</AlertTitle>
                        <AlertDescription>
                            Holding accounts are maintained separately so they do not become fake bank accounts or appear in bank reconciliation.
                            <Link className="ml-1 font-medium underline" href="/finance/cash/liquidity-accounts">Open Banking Setup</Link>
                        </AlertDescription>
                    </Alert>
                </CardContent>
            </Card>

            {/* AP matching is configured alongside the Finance-owned settlement controls because
                these tolerances determine whether a supplier invoice can enter the payment cycle. */}
            <Card>
                <CardHeader>
                    <CardTitle>Accounts Payable Matching Control</CardTitle>
                    <CardDescription>
                        Tenant-level tolerances used by the mandatory PO, invoice, and independently accepted receipt comparison.
                    </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-2">
                    <div className="space-y-2">
                        <Label htmlFor="apInvoicePriceTolerancePercent">Unit price tolerance (%)</Label>
                        <Input
                            id="apInvoicePriceTolerancePercent"
                            type="number"
                            min={0}
                            max={100}
                            step="0.01"
                            value={formData.apInvoicePriceTolerancePercent ?? 1}
                            onChange={(event) => setFormData({
                                ...formData,
                                apInvoicePriceTolerancePercent: Number(event.target.value),
                            })}
                        />
                        <p className="text-sm text-muted-foreground">
                            Maximum unit-price variance from the governed purchase-order line.
                        </p>
                    </div>
                    <div className="space-y-2">
                        <Label htmlFor="apInvoiceQuantityTolerancePercent">Cumulative quantity tolerance (%)</Label>
                        <Input
                            id="apInvoiceQuantityTolerancePercent"
                            type="number"
                            min={0}
                            max={100}
                            step="0.01"
                            value={formData.apInvoiceQuantityTolerancePercent ?? 1}
                            onChange={(event) => setFormData({
                                ...formData,
                                apInvoiceQuantityTolerancePercent: Number(event.target.value),
                            })}
                        />
                        <p className="text-sm text-muted-foreground">
                            Maximum cumulative invoice quantity above independently accepted receipt quantity.
                        </p>
                    </div>
                </CardContent>
            </Card>

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

                    <div className="space-y-4 rounded-lg border p-4">
                        <div>
                            <h3 className="font-semibold">Foreign Exchange Gain/Loss Accounts</h3>
                            <p className="text-sm text-muted-foreground">Separate gain and loss mappings are enforced by revaluation and settlement posting. These mappings remain configurable after transactions exist and every change is audited.</p>
                        </div>
                        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                            <div className="space-y-2">
                                <Label htmlFor="unrealizedFxGain">Unrealized FX Gain</Label>
                                <AccountPicker id="unrealizedFxGain" value={formData.unrealizedFxGainAccountId} placeholder="Search revenue accounts..." accounts={accounts.filter(a => a.accountType === 'Revenue')} onChange={(value) => setFormData({ ...formData, unrealizedFxGainAccountId: value })} />
                                <p className="text-xs text-muted-foreground">Credit side of favorable closing revaluation movements.</p>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="unrealizedFxLoss">Unrealized FX Loss</Label>
                                <AccountPicker id="unrealizedFxLoss" value={formData.unrealizedFxLossAccountId} placeholder="Search expense accounts..." accounts={accounts.filter(a => a.accountType === 'Expense')} onChange={(value) => setFormData({ ...formData, unrealizedFxLossAccountId: value })} />
                                <p className="text-xs text-muted-foreground">Debit side of adverse closing revaluation movements.</p>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="realizedFxGain">Realized FX Gain</Label>
                                <AccountPicker id="realizedFxGain" value={formData.realizedFxGainAccountId} placeholder="Search revenue accounts..." accounts={accounts.filter(a => a.accountType === 'Revenue')} onChange={(value) => setFormData({ ...formData, realizedFxGainAccountId: value })} />
                                <p className="text-xs text-muted-foreground">Used when invoices, receipts, payments and other FX items settle favorably.</p>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="realizedFxLoss">Realized FX Loss</Label>
                                <AccountPicker id="realizedFxLoss" value={formData.realizedFxLossAccountId} placeholder="Search expense accounts..." accounts={accounts.filter(a => a.accountType === 'Expense')} onChange={(value) => setFormData({ ...formData, realizedFxLossAccountId: value })} />
                                <p className="text-xs text-muted-foreground">Used when invoices, receipts, payments and other FX items settle adversely.</p>
                            </div>
                        </div>
                        {(formData.unrealizedGainLossAccountId || formData.realizedGainLossAccountId) && (
                            <Alert>
                                <AlertTriangle className="h-4 w-4" />
                                <AlertTitle>Legacy combined mappings retained</AlertTitle>
                                <AlertDescription>The old combined gain/loss mappings remain stored for compatibility. New controlled postings use the separate mappings above.</AlertDescription>
                            </Alert>
                        )}
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
                            <Label htmlFor="returnToVendorClearingAccount">Supplier Returns Clearing</Label>
                            <AccountPicker
                                id="returnToVendorClearingAccount"
                                value={formData.returnToVendorClearingAccountId}
                                placeholder="Search asset accounts..."
                                disabled={saving}
                                allowClear={false}
                                accounts={accounts.filter(a => a.accountType === 'Asset' && a.status === 'Active' &&
                                    a.allowDirectPosting && !a.isControlAccount && a.id !== formData.controlAccountInventoryId)}
                                onChange={(value) => setFormData({ ...formData, returnToVendorClearingAccountId: value })}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="purchaseReturnVarianceAccount">Purchase Return Cost Variance</Label>
                            <AccountPicker
                                id="purchaseReturnVarianceAccount"
                                value={formData.purchaseReturnVarianceAccountId}
                                placeholder="Search expense accounts..."
                                disabled={saving}
                                allowClear={false}
                                accounts={accounts.filter(a => a.accountType === 'Expense' && a.status === 'Active' &&
                                    a.allowDirectPosting && !a.isControlAccount && a.id !== formData.controlAccountInventoryId)}
                                onChange={(value) => setFormData({ ...formData, purchaseReturnVarianceAccountId: value })}
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

                        <div className="space-y-2">
                            <Label htmlFor="controlAccountGrvAccrual">GRV Accrual Control</Label>
                            <AccountPicker
                                id="controlAccountGrvAccrual"
                                value={formData.controlAccountGRVAccrualId}
                                placeholder="Search GRV accrual accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Liability')}
                                onChange={(value) => setFormData({ ...formData, controlAccountGRVAccrualId: value })}
                            />
                            <p className="text-sm text-muted-foreground">
                                Credited when finance GRVs are posted, then cleared when the AP invoice is approved.
                            </p>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="discountAllowedAccount">Sales Discounts Allowed</Label>
                            <AccountPicker
                                id="discountAllowedAccount"
                                value={formData.discountAllowedAccountId}
                                placeholder="Search discount allowed accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Revenue' || a.accountType === 'Expense')}
                                onChange={(value) => setFormData({ ...formData, discountAllowedAccountId: value })}
                            />
                            <p className="text-sm text-muted-foreground">
                                Debited when AR/customer discounts are allowed.
                            </p>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="discountReceivedAccount">Purchase Discounts Received</Label>
                            <AccountPicker
                                id="discountReceivedAccount"
                                value={formData.discountReceivedAccountId}
                                placeholder="Search discount received accounts..."
                                disabled={settings?.transactionsExist}
                                accounts={accounts.filter(a => a.accountType === 'Revenue' || a.accountType === 'Expense')}
                                onChange={(value) => setFormData({ ...formData, discountReceivedAccountId: value })}
                            />
                            <p className="text-sm text-muted-foreground">
                                Credited when AP/supplier discounts are received or taken.
                            </p>
                        </div>
                    </div>


                </CardContent>
            </Card >
        </div >
    );
}
