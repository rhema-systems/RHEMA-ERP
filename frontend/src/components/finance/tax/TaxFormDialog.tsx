'use client';

import { useEffect, useMemo, useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { taxDataService } from '@/services/finance/tax-data.service';
import { TaxApplicability, TaxCalculationMethod, TaxCategory, type CreateTaxDto, type Tax, type UpdateTaxDto } from '@/types/tax';
import type { Account } from '@/types/finance';
import { cn } from '@/lib/utils';
import { useToast } from '@/hooks/use-toast';
import { getTaxAccountRequirements } from '@/lib/finance/tax-account-requirements';

interface TaxFormDialogProps {
    open: boolean;
    tax?: Tax | null;
    accounts: Account[];
    accountsLoading?: boolean;
    onOpenChange: (open: boolean) => void;
    onSaved: (tax: Tax) => void;
}

interface TaxAccountPickerProps {
    id: string;
    value?: string | null;
    label: string;
    placeholder: string;
    accounts: Account[];
    disabled?: boolean;
    onChange: (value: string | null) => void;
}

const defaultFormData: CreateTaxDto = {
    code: '',
    name: '',
    rate: 0,
    calculationMethod: TaxCalculationMethod.Simple,
    category: TaxCategory.Standard,
    applicability: TaxApplicability.Both,
    isInputTaxDeductible: false,
    isActive: true,
    thresholdAmount: null,
    taxPayableAccountId: null,
    taxReceivableAccountId: null,
    effectiveFrom: new Date().toISOString().slice(0, 10),
};

function formatAccount(account?: Account) {
    if (!account) return 'No account';
    return `${account.accountNumber || account.accountCode} - ${account.accountName}`;
}

function TaxAccountPicker({ id, value, label, placeholder, accounts, disabled, onChange }: TaxAccountPickerProps) {
    const [open, setOpen] = useState(false);
    const selected = accounts.find(account => account.id === value);

    return (
        <div className="space-y-2">
            <Label htmlFor={id}>{label}</Label>
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
                        <span className="truncate">{formatAccount(selected)}</span>
                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                    </Button>
                </PopoverTrigger>
                <PopoverContent className="w-[460px] p-0" align="start">
                    <Command>
                        <CommandInput placeholder={placeholder} />
                        <CommandList>
                            <CommandEmpty>No account found.</CommandEmpty>
                            <CommandGroup>
                                <CommandItem
                                    value="no account"
                                    onSelect={() => {
                                        onChange(null);
                                        setOpen(false);
                                    }}
                                >
                                    <Check className={cn('mr-2 h-4 w-4', !value ? 'opacity-100' : 'opacity-0')} />
                                    No account
                                </CommandItem>
                                {accounts.map(account => (
                                    <CommandItem
                                        key={account.id}
                                        value={`${account.accountNumber} ${account.accountCode} ${account.accountName}`}
                                        onSelect={() => {
                                            onChange(account.id);
                                            setOpen(false);
                                        }}
                                    >
                                        <Check className={cn('mr-2 h-4 w-4', value === account.id ? 'opacity-100' : 'opacity-0')} />
                                        <span className="truncate">{formatAccount(account)}</span>
                                    </CommandItem>
                                ))}
                            </CommandGroup>
                        </CommandList>
                    </Command>
                </PopoverContent>
            </Popover>
        </div>
    );
}

export function TaxFormDialog({ open, tax, accounts, accountsLoading = false, onOpenChange, onSaved }: TaxFormDialogProps) {
    const [formData, setFormData] = useState<CreateTaxDto>(defaultFormData);
    const [isSaving, setIsSaving] = useState(false);
    const [changeReason, setChangeReason] = useState('');
    const { toast } = useToast();
    const isEditing = Boolean(tax);
    const accountRequirements = getTaxAccountRequirements(
        formData.applicability,
        formData.category,
        Boolean(formData.isInputTaxDeductible),
    );

    const liabilityAccounts = useMemo(
        () => accounts.filter(account => account.accountType === 'Liability' && account.status === 'Active'),
        [accounts]
    );
    const assetAccounts = useMemo(
        () => accounts.filter(account => account.accountType === 'Asset' && account.status === 'Active'),
        [accounts]
    );

    useEffect(() => {
        if (!open) return;

        if (tax) {
            setFormData({
                code: tax.code,
                name: tax.name,
                description: tax.description ?? '',
                rate: tax.rate,
                calculationMethod: tax.calculationMethod ?? TaxCalculationMethod.Simple,
                category: tax.category,
                applicability: tax.applicability,
                isInputTaxDeductible: Boolean(tax.isInputTaxDeductible),
                isActive: tax.isActive,
                thresholdAmount: tax.thresholdAmount ?? null,
                taxPayableAccountId: tax.taxPayableAccountId ?? null,
                taxReceivableAccountId: tax.taxReceivableAccountId ?? null,
                effectiveFrom: tax.effectiveFrom?.slice(0, 10),
            });
            setChangeReason('');
            return;
        }

        setFormData(defaultFormData);
    }, [open, tax]);

    const handleSave = async () => {
        if (accountRequirements.payableRequired && !formData.taxPayableAccountId) {
            toast({ title: 'Tax payable account required', description: accountRequirements.guidance, variant: 'destructive' });
            return;
        }
        if (accountRequirements.receivableRequired && !formData.taxReceivableAccountId) {
            toast({ title: 'Tax receivable account required', description: accountRequirements.guidance, variant: 'destructive' });
            return;
        }
        setIsSaving(true);
        try {
            const payload = {
                ...formData,
                description: formData.description?.trim() || undefined,
                thresholdAmount: formData.thresholdAmount ?? null,
                taxPayableAccountId: formData.taxPayableAccountId || null,
                taxReceivableAccountId: formData.taxReceivableAccountId || null,
            };

            const saved = tax
                ? await taxDataService.updateTax(tax.id, {
                    ...payload,
                    clearTaxPayableAccount: Boolean(tax.taxPayableAccountId && !formData.taxPayableAccountId),
                    clearTaxReceivableAccount: Boolean(tax.taxReceivableAccountId && !formData.taxReceivableAccountId),
                    changeReason: changeReason.trim() || undefined,
                } as UpdateTaxDto)
                : await taxDataService.createTax(payload);

            onSaved(saved);
            onOpenChange(false);
        } catch (error) {
            console.error('Failed to save tax:', error);
            const description = error instanceof Error
                ? error.message
                : 'The tax configuration could not be saved. Please try again.';
            toast({
                title: 'Unable to save tax',
                description,
                variant: 'destructive',
            });
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="flex max-h-[calc(100vh-2rem)] max-w-3xl flex-col overflow-hidden">
                <DialogHeader className="shrink-0">
                    <DialogTitle>{isEditing ? 'Edit Tax' : 'Add Tax'}</DialogTitle>
                    <DialogDescription>
                        Configure tax behavior and GL mappings used by posting and statutory tax reports.
                    </DialogDescription>
                </DialogHeader>

                <div className="grid min-h-0 flex-1 gap-4 overflow-y-auto py-4 pr-2">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div className="space-y-2">
                            <Label htmlFor="tax-code">Tax Code *</Label>
                            <Input
                                id="tax-code"
                                value={formData.code}
                                disabled={isEditing}
                                onChange={(event) => setFormData({ ...formData, code: event.target.value.toUpperCase() })}
                                placeholder="VAT, NHIL, GETFUND"
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="tax-name">Tax Name *</Label>
                            <Input
                                id="tax-name"
                                value={formData.name}
                                onChange={(event) => setFormData({ ...formData, name: event.target.value })}
                                placeholder="Value Added Tax"
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="tax-effective-from">Rate Effective Date *</Label>
                            <Input
                                id="tax-effective-from"
                                type="date"
                                value={formData.effectiveFrom?.slice(0, 10) || ''}
                                onChange={(event) => setFormData({ ...formData, effectiveFrom: event.target.value })}
                            />
                        </div>
                    </div>

                    {isEditing && (
                        <div className="space-y-2">
                            <Label htmlFor="tax-change-reason">Change Reason</Label>
                            <Input
                                id="tax-change-reason"
                                value={changeReason}
                                onChange={(event) => setChangeReason(event.target.value)}
                                placeholder="Regulatory change, GL remapping, threshold revision…"
                            />
                            <p className="text-xs text-muted-foreground">
                                Saved with the immutable configuration version for audit.
                            </p>
                        </div>
                    )}

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div className="space-y-2">
                            <Label htmlFor="tax-rate">Tax Rate (%) *</Label>
                            <Input
                                id="tax-rate"
                                type="number"
                                step="0.01"
                                value={formData.rate}
                                onChange={(event) => setFormData({ ...formData, rate: Number(event.target.value) })}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="tax-threshold">Threshold Amount</Label>
                            <Input
                                id="tax-threshold"
                                type="number"
                                value={formData.thresholdAmount ?? ''}
                                onChange={(event) => setFormData({ ...formData, thresholdAmount: event.target.value ? Number(event.target.value) : null })}
                                placeholder="Optional"
                            />
                        </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div className="space-y-2">
                            <Label htmlFor="tax-category">Category *</Label>
                            <Select
                                value={formData.category}
                                onValueChange={(value) => setFormData({ ...formData, category: value as TaxCategory })}
                            >
                                <SelectTrigger id="tax-category">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value={TaxCategory.Standard}>Standard</SelectItem>
                                    <SelectItem value={TaxCategory.Withholding}>Withholding</SelectItem>
                                    <SelectItem value={TaxCategory.VatWithholding}>VAT Withholding</SelectItem>
                                    <SelectItem value={TaxCategory.Levy}>Levy</SelectItem>
                                    <SelectItem value={TaxCategory.Exempt}>Exempt</SelectItem>
                                    <SelectItem value={TaxCategory.ZeroRated}>Zero-rated</SelectItem>
                                    <SelectItem value={TaxCategory.OutOfScope}>Out of scope</SelectItem>
                                    <SelectItem value={TaxCategory.ReverseCharge}>Reverse charge / import VAT</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="tax-applicability">Applicability *</Label>
                            <Select
                                value={formData.applicability}
                                onValueChange={(value) => setFormData({ ...formData, applicability: value as TaxApplicability })}
                            >
                                <SelectTrigger id="tax-applicability">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value={TaxApplicability.Sales}>Sales Only</SelectItem>
                                    <SelectItem value={TaxApplicability.Purchases}>Purchases Only</SelectItem>
                                    <SelectItem value={TaxApplicability.Both}>Both</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 rounded-md border p-4">
                        <TaxAccountPicker
                            id="tax-payable-account"
                            label={`Tax Payable Account${accountRequirements.payableRequired ? ' *' : ''}`}
                            value={formData.taxPayableAccountId}
                            placeholder="Search liability accounts..."
                            accounts={liabilityAccounts}
                            disabled={accountsLoading}
                            onChange={(value) => setFormData({ ...formData, taxPayableAccountId: value })}
                        />
                        <TaxAccountPicker
                            id="tax-receivable-account"
                            label={`Tax Receivable Account${accountRequirements.receivableRequired ? ' *' : ''}`}
                            value={formData.taxReceivableAccountId}
                            placeholder="Search asset accounts..."
                            accounts={assetAccounts}
                            disabled={accountsLoading}
                            onChange={(value) => setFormData({ ...formData, taxReceivableAccountId: value })}
                        />
                        <p className="md:col-span-2 text-xs text-muted-foreground" role="note">
                            {accountRequirements.guidance}
                        </p>
                    </div>

                    <div className="flex flex-col gap-3 rounded-md border p-4">
                        <div className="flex items-center justify-between gap-4">
                            <Label htmlFor="tax-deductible">Recoverable input tax</Label>
                            <Switch
                                id="tax-deductible"
                                checked={Boolean(formData.isInputTaxDeductible)}
                                onCheckedChange={(checked) => setFormData({ ...formData, isInputTaxDeductible: checked })}
                            />
                        </div>
                        <div className="flex items-center justify-between gap-4">
                            <Label htmlFor="tax-active">Active</Label>
                            <Switch
                                id="tax-active"
                                checked={Boolean(formData.isActive)}
                                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                            />
                        </div>
                    </div>
                </div>

                <DialogFooter className="shrink-0 border-t pt-4">
                    <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isSaving}>
                        Cancel
                    </Button>
                    <Button onClick={handleSave} disabled={isSaving || accountsLoading}>
                        {isSaving ? 'Saving...' : isEditing ? 'Update Tax' : 'Create Tax'}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
