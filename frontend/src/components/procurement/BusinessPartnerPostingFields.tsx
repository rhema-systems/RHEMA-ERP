'use client';

import { useEffect, useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import {
  businessPartnerService,
  type BusinessPartnerPostingDefaults,
  type BusinessPartnerWithholdingTaxOption,
} from '@/services/businessPartnerService';
import type { PaymentTermListDto } from '@/services/financeCommonService';
import type { Account } from '@/types/finance';
import type { BankAccount } from '@/types/cash-management';
import type { TaxGroup } from '@/types/tax';
import { cn } from '@/lib/utils';
import { hasSupplierRole, hasContractorRole } from '@/lib/business-partner-roles';

export const emptyBusinessPartnerPostingDefaults =
  (): BusinessPartnerPostingDefaults => ({
    subjectToWithholdingDeduction: false,
    withholdingTaxRate: 0,
    cashAccountSource: 'Chequebook',
  });

export function useBusinessPartnerPostingCatalogues(partnerType = 'Supplier') {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [bankAccounts, setBankAccounts] = useState<BankAccount[]>([]);
  const [taxGroups, setTaxGroups] = useState<TaxGroup[]>([]);
  const [withholdingTaxes, setWithholdingTaxes] = useState<
    BusinessPartnerWithholdingTaxOption[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [unavailable, setUnavailable] = useState<string[]>([]);

  useEffect(() => {
    let current = true;
    setLoading(true);
    void businessPartnerService
      .getPostingOptions(partnerType)
      .then((options) => {
        if (!current) return;
        setAccounts(options.accounts);
        setBankAccounts(options.bankAccounts);
        setTaxGroups(options.taxGroups);
        setWithholdingTaxes(options.withholdingTaxes || []);
        setUnavailable([]);
      })
      .catch(() => {
        if (current) setUnavailable(['posting options']);
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [partnerType]);

  return {
    accounts,
    bankAccounts,
    taxGroups,
    withholdingTaxes,
    loading,
    unavailable,
  };
}

export function PartnerCatalogueNotice({
  unavailable,
}: {
  unavailable: string[];
}) {
  return unavailable.length ? (
    <p role="status" className="text-sm text-amber-700">
      Could not load {unavailable.join(', ')}. Saved selections are unchanged.
    </p>
  ) : null;
}

interface Option {
  value: string;
  label: string;
}

function PartnerDefaultPicker({
  id,
  label,
  value,
  options,
  onChange,
  disabled,
}: {
  id: string;
  label: string;
  value?: string | null;
  options: Option[];
  onChange: (value: string | null) => void;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const selected = options.find((option) => option.value === value);
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-label={label}
          aria-expanded={open}
          disabled={disabled}
          className="h-9 w-full min-w-0 justify-between font-normal"
        >
          <span className="truncate">
            {selected?.label ||
              (value ? 'Saved selection (unavailable)' : 'None')}
          </span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-[var(--radix-popover-trigger-width)] min-w-64 max-w-[calc(100vw-2rem)] p-0"
      >
        <Command>
          <CommandInput placeholder={`Search ${label.toLowerCase()}...`} />
          <CommandList>
            <CommandEmpty>No match found.</CommandEmpty>
            <CommandGroup>
              <CommandItem
                value="None"
                onSelect={() => {
                  onChange(null);
                  setOpen(false);
                }}
              >
                <Check
                  className={cn(
                    'mr-2 h-4 w-4',
                    value ? 'opacity-0' : 'opacity-100'
                  )}
                />
                None
              </CommandItem>
              {options.map((option) => (
                <CommandItem
                  key={option.value}
                  value={option.label}
                  onSelect={() => {
                    onChange(option.value);
                    setOpen(false);
                  }}
                >
                  <Check
                    className={cn(
                      'mr-2 h-4 w-4',
                      option.value === value ? 'opacity-100' : 'opacity-0'
                    )}
                  />
                  {option.label}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

interface PostingFieldsProps {
  value: BusinessPartnerPostingDefaults;
  onChange: (value: BusinessPartnerPostingDefaults) => void;
  disabled?: boolean;
}

export function PartnerTaxDefaultsFields({
  value,
  onChange,
  taxGroups,
  withholdingTaxes = [],
  partnerType = 'Supplier',
  disabled,
}: PostingFieldsProps & {
  taxGroups: TaxGroup[];
  withholdingTaxes?: BusinessPartnerWithholdingTaxOption[];
  partnerType?: string;
}) {
  const hasPayables = hasSupplierRole(partnerType) || hasContractorRole(partnerType);
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      {hasPayables && <><div className="flex min-h-9 items-center gap-3">
        <Switch
          id="subjectToWithholdingDeduction"
          checked={value.subjectToWithholdingDeduction}
          disabled={disabled}
          onCheckedChange={(checked) =>
            onChange({ ...value, subjectToWithholdingDeduction: checked })
          }
        />
        <Label htmlFor="subjectToWithholdingDeduction">
          Subject To Withholding Deduction
        </Label>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="withholdingTaxRate">WHT Rate (%)</Label>
        <Input
          id="withholdingTaxRate"
          type="number"
          min="0"
          max="100"
          step="0.0001"
          disabled={disabled || !value.subjectToWithholdingDeduction}
          value={value.withholdingTaxRate || ''}
          onChange={(event) =>
            onChange({
              ...value,
              withholdingTaxRate:
                event.target.value === '' ? 0 : Number(event.target.value),
            })
          }
        />
      </div></>}
      {hasPayables && value.subjectToWithholdingDeduction && (
        <div className="space-y-1.5">
          <Label htmlFor="defaultWithholdingTaxId">WHT Configuration</Label>
          <PartnerDefaultPicker
            id="defaultWithholdingTaxId"
            label="WHT Configuration"
            value={value.defaultWithholdingTaxId}
            disabled={disabled}
            options={withholdingTaxes.map((tax) => ({
              value: tax.id,
              label: `${tax.code} - ${tax.name} (${tax.rate}%)`,
            }))}
            onChange={(defaultWithholdingTaxId) => {
              const selected = withholdingTaxes.find(
                (tax) => tax.id === defaultWithholdingTaxId
              );
              onChange({
                ...value,
                defaultWithholdingTaxId,
                withholdingTaxRate: selected?.rate ?? value.withholdingTaxRate,
              });
            }}
          />
        </div>
      )}
      <div
        className={`space-y-1.5 ${hasPayables && value.subjectToWithholdingDeduction ? '' : 'md:col-span-2'}`}
      >
        <Label htmlFor="defaultTaxGroupId">Tax</Label>
        <PartnerDefaultPicker
          id="defaultTaxGroupId"
          label="Tax"
          value={value.defaultTaxGroupId}
          disabled={disabled}
          options={taxGroups.map((tax) => ({
            value: tax.id,
            label: `${tax.code} - ${tax.name}`,
          }))}
          onChange={(defaultTaxGroupId) =>
            onChange({ ...value, defaultTaxGroupId })
          }
        />
      </div>
    </div>
  );
}

export interface PartnerOptionValues {
  paymentTermId: string;
  taxNumber: string;
  creditLimit: string;
}

export function PartnerOptionsFields({
  value,
  onChange,
  options,
  onOptionsChange,
  paymentTerms,
  partnerType = 'Supplier',
  bankAccounts,
  disabled,
}: PostingFieldsProps & {
  options: PartnerOptionValues;
  onOptionsChange: (patch: Partial<PartnerOptionValues>) => void;
  paymentTerms: PaymentTermListDto[];
  partnerType?: string;
  bankAccounts: BankAccount[];
}) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      <div className="space-y-1.5">
        <Label htmlFor="paymentTermId">Payment Terms</Label>
        <PartnerDefaultPicker
          id="paymentTermId"
          label="Payment Terms"
          value={options.paymentTermId}
          disabled={disabled}
          options={paymentTerms
            .filter((term) => {
              const applicable = term.applicableTo?.toLowerCase();
              const target =
                partnerType === 'Customer'
                  ? 'customer'
                  : partnerType === 'Both'
                    ? 'all'
                    : 'supplier';
              return (
                !applicable ||
                applicable === 'all' ||
                applicable === target ||
                (target === 'supplier' && applicable === 'vendor') ||
                (target === 'customer' && applicable === 'client')
              );
            })
            .map((term) => ({
              value: term.id,
              label: `${term.code} - ${term.name}`,
            }))}
          onChange={(paymentTermId) =>
            onOptionsChange({ paymentTermId: paymentTermId || '' })
          }
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="taxNumber">TIN</Label>
        <Input
          id="taxNumber"
          value={options.taxNumber}
          disabled={disabled}
          onChange={(event) =>
            onOptionsChange({ taxNumber: event.target.value })
          }
        />
      </div>
      {(hasSupplierRole(partnerType) || hasContractorRole(partnerType)) && <div className="space-y-1.5">
        <Label htmlFor="defaultBankAccountId">ChequeBook ID</Label>
        <PartnerDefaultPicker
          id="defaultBankAccountId"
          label="ChequeBook ID"
          value={value.defaultBankAccountId}
          disabled={disabled}
          options={bankAccounts.map((bank) => ({
            value: bank.id,
            label: `${bank.accountNumber} - ${bank.accountName} (${bank.currency})`,
          }))}
          onChange={(defaultBankAccountId) =>
            onChange({ ...value, defaultBankAccountId })
          }
        />
      </div>}
      <div className="space-y-1.5">
        <Label htmlFor="creditLimit">Credit Limit</Label>
        <Input
          id="creditLimit"
          type="number"
          min="0"
          step="0.01"
          value={options.creditLimit}
          disabled={disabled}
          onChange={(event) =>
            onOptionsChange({ creditLimit: event.target.value })
          }
        />
      </div>
    </div>
  );
}

export const businessPartnerAccountFields = [
  ['defaultCashAccountId', 'Cash'],
  ['defaultApAccountId', 'Accounts Payable'],
  ['defaultTermsDiscountsTakenAccountId', 'Terms Discounts Taken'],
  ['defaultFinanceChargesAccountId', 'Finance Charges'],
  ['defaultExpenseAccountId', 'Purchases'],
  ['defaultMiscellaneousAccountId', 'Miscellaneous'],
  ['defaultFreightAccountId', 'Freight'],
  ['defaultTaxAccountId', 'Input tax fallback'],
  ['defaultWriteoffAccountId', 'Writeoffs'],
  ['defaultAccruedPurchasesAccountId', 'Accrued Purchases'],
  ['defaultPurchasePriceVarianceAccountId', 'Purchase Price Variance'],
] as const;

export type BusinessPartnerAccountKey =
  (typeof businessPartnerAccountFields)[number][0];

export function eligiblePartnerAccounts(
  accounts: Account[],
  key: BusinessPartnerAccountKey
) {
  return accounts.filter((account) => {
    if (account.status !== 'Active') return false;
    if (
      key === 'defaultApAccountId' ||
      key === 'defaultAccruedPurchasesAccountId'
    )
      return (
        account.accountType === 'Liability' &&
        (account.allowDirectPosting || account.isControlAccount)
      );
    if (key === 'defaultTaxAccountId')
      return (
        (account.accountType === 'Asset' || account.accountType === 'Liability') &&
        (account.allowDirectPosting || account.isControlAccount)
      );
    if (!account.allowDirectPosting || account.isControlAccount) return false;
    if (key === 'defaultCashAccountId') return account.accountType === 'Asset';
    if (key === 'defaultWriteoffAccountId')
      return account.accountType === 'Revenue' || account.accountType === 'Expense';
    if (key === 'defaultExpenseAccountId')
      return (
        account.accountType === 'Asset' || account.accountType === 'Expense'
      );
    if (key === 'defaultPurchasePriceVarianceAccountId')
      return account.accountType === 'Expense';
    return true;
  });
}

export function PartnerAccountsFields({
  value,
  onChange,
  accounts,
  bankAccounts,
  disabled,
}: PostingFieldsProps & { accounts: Account[]; bankAccounts: BankAccount[] }) {
  const chequeBook = bankAccounts.find(
    (bank) => bank.id === value.defaultBankAccountId
  );
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">Trade discounts reduce the invoice purchase amount. Payment discounts post when taken. Cash defaults select the payment bank when no bank is explicitly chosen; a creditor cash account must identify one active bank with that GL account.</p>
      <p className="text-sm text-muted-foreground">For Freight, Miscellaneous and Finance Charges, select the matching invoice line type with supplier defaults enabled. Review the account on the draft before approval. Capitalized freight continues through Landed Cost.</p>
      <p className="text-sm text-muted-foreground">Input tax fallback applies to recoverable purchase tax only when the tax rule has no receivable account. Enable supplier defaults on a new invoice to capture this account; tax rates, exemptions and nonrecoverable tax remain controlled by the tax rules.</p>
      <div className="flex flex-wrap items-center gap-4">
        <Label id="cashAccountSourceLabel">Use Cash Account From</Label>
        <RadioGroup
          aria-labelledby="cashAccountSourceLabel"
          className="flex gap-4"
          value={value.cashAccountSource}
          disabled={disabled}
          onValueChange={(cashAccountSource) =>
            onChange({
              ...value,
              cashAccountSource:
                cashAccountSource as BusinessPartnerPostingDefaults['cashAccountSource'],
            })
          }
        >
          <div className="flex items-center gap-2">
            <RadioGroupItem id="cashChequebook" value="Chequebook" />
            <Label htmlFor="cashChequebook">Chequebook</Label>
          </div>
          <div className="flex items-center gap-2">
            <RadioGroupItem id="cashCreditor" value="BusinessPartner" />
            <Label htmlFor="cashCreditor">Creditor</Label>
          </div>
        </RadioGroup>
      </div>
      <div className="overflow-x-auto rounded-lg border">
        <table className="w-full min-w-[480px] text-sm">
          <thead className="bg-muted/50">
            <tr>
              <th className="w-56 px-3 py-2 text-left font-medium">
                Posting Type
              </th>
              <th className="px-3 py-2 text-left font-medium">
                Account / Description
              </th>
            </tr>
          </thead>
          <tbody>
            {businessPartnerAccountFields.map(([key, label]) => (
              <tr key={key} className="border-t">
                <td className="px-3 py-1.5">
                  <Label htmlFor={key} className="font-normal">
                    {label}
                  </Label>
                </td>
                <td className="px-3 py-1.5">
                  {key === 'defaultCashAccountId' &&
                  value.cashAccountSource === 'Chequebook' ? (
                    <div
                      id={key}
                      className="min-h-9 py-2 text-muted-foreground"
                    >
                      {chequeBook?.glAccountNumber
                        ? `${chequeBook.glAccountNumber} - ${chequeBook.glAccountName || 'Cash'}`
                        : value.defaultBankAccountId
                          ? 'Chequebook cash account unavailable'
                          : 'Select ChequeBook ID in Options'}
                    </div>
                  ) : (
                    <PostingAccountPicker
                      id={key}
                      value={value[key]}
                      disabled={disabled}
                      accounts={eligiblePartnerAccounts(accounts, key)}
                      onChange={(accountId) =>
                        onChange({ ...value, [key]: accountId })
                      }
                    />
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
