'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  AlertCircle,
  ArrowLeft,
  Landmark,
  Loader2,
  Plus,
  Save,
  ShieldCheck,
  WalletCards,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import {
  propertyEnquiryService,
  type ProspectDepositRequirementType,
} from '@/services/propertyEnquiryService';
import {
  salesSetupService,
  type SalesSaleableSourceDto,
} from '@/services/salesSetupService';
import type {
  BankAccount,
  BankingSetupStatus,
  DepositPolicy,
  LiquidityAccount,
  LiquidityAccountType,
} from '@/types/cash-management';
import type { Account } from '@/types/finance';

const labels: Record<LiquidityAccountType, string> = {
  Bank: 'Bank Account',
  UndepositedCash: 'Undeposited Cash',
  ChequesAwaitingDeposit: 'Cheques Awaiting Deposit',
  MobileMoneyClearing: 'Mobile Money Clearing',
  CardSettlementClearing: 'Card Settlement Clearing',
  CashTill: 'Cash Till',
  OtherSettlementClearing: 'Other Settlement Clearing',
};

const defaultCodes: Partial<Record<LiquidityAccountType, string>> = {
  UndepositedCash: 'UNDEP-CASH',
  ChequesAwaitingDeposit: 'CHQ-CLEAR',
  MobileMoneyClearing: 'MOMO-CLEAR',
  CardSettlementClearing: 'CARD-CLEAR',
};

type ProspectDepositPolicyForm = {
  salesSaleableSourceId: string;
  requirementType: ProspectDepositRequirementType;
  fixedAmount: string;
  percentage: string;
  depositLiabilityAccountId: string;
  cashDestinationType: 'Bank' | 'Liquidity';
  defaultBankAccountId: string;
  defaultLiquidityAccountId: string;
  isActive: boolean;
};

const emptyProspectDepositPolicy = (
  salesSaleableSourceId = ''
): ProspectDepositPolicyForm => ({
  salesSaleableSourceId,
  requirementType: 'Full',
  fixedAmount: '',
  percentage: '',
  depositLiabilityAccountId: '',
  cashDestinationType: 'Liquidity',
  defaultBankAccountId: '',
  defaultLiquidityAccountId: '',
  isActive: true,
});

export default function LiquidityAccountsPage() {
  const [setup, setSetup] = useState<BankingSetupStatus | null>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [liabilityAccounts, setLiabilityAccounts] = useState<Account[]>([]);
  const [saleableSources, setSaleableSources] = useState<
    SalesSaleableSourceDto[]
  >([]);
  const [bankAccounts, setBankAccounts] = useState<BankAccount[]>([]);
  const [liquidityAccounts, setLiquidityAccounts] = useState<
    LiquidityAccount[]
  >([]);
  const [mappings, setMappings] = useState<Record<string, string>>({});
  const [policy, setPolicy] = useState<DepositPolicy>('DepositIntact');
  const [prospectPolicy, setProspectPolicy] =
    useState<ProspectDepositPolicyForm>(() => emptyProspectDepositPolicy());
  const [prospectPolicyLoading, setProspectPolicyLoading] = useState(false);
  const [prospectPolicySaving, setProspectPolicySaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const [status, sources, chart, banks, liquidity] = await Promise.all([
        cashManagementDataService.getBankingSetup(),
        salesSetupService.getSaleableSources(false),
        financeDataService.getAccounts({ status: 'Active' }),
        cashManagementDataService.getActiveBankAccounts(),
        cashManagementDataService.getLiquidityAccounts(true),
      ]);
      setSetup(status);
      const postingAccounts = chart.filter(
        (account) =>
          account.allowDirectPosting || account.isPostingAllowed === true
      );
      setAccounts(postingAccounts);
      setLiabilityAccounts(
        postingAccounts.filter(
          (account) =>
            account.accountType === 'Liability' && !account.isControlAccount
        )
      );
      setSaleableSources(sources.filter((source) => source.isActive));
      setBankAccounts(
        banks.filter((bank) => bank.isActive && Boolean(bank.glAccountId))
      );
      setLiquidityAccounts(liquidity.filter((account) => account.isActive));
      setProspectPolicy((current) =>
        current.salesSaleableSourceId
          ? current
          : emptyProspectDepositPolicy(
              sources.find(
                (source) =>
                  source.adapterKey.toLowerCase() === 'land-management'
              )?.id ??
                sources.find((source) => source.isActive)?.id ??
                ''
            )
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Could not load liquidity accounts.'
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  useEffect(() => {
    const sourceId = prospectPolicy.salesSaleableSourceId;
    if (!sourceId) return;
    let current = true;
    setProspectPolicyLoading(true);
    void propertyEnquiryService
      .getDepositPolicy(sourceId)
      .then((saved) => {
        if (!current) return;
        if (!saved) {
          setProspectPolicy(emptyProspectDepositPolicy(sourceId));
          return;
        }
        setProspectPolicy({
          salesSaleableSourceId: sourceId,
          requirementType: saved.requirementType,
          fixedAmount: saved.fixedAmount?.toString() ?? '',
          percentage: saved.percentage?.toString() ?? '',
          depositLiabilityAccountId: saved.depositLiabilityAccountId,
          cashDestinationType: saved.defaultBankAccountId
            ? 'Bank'
            : 'Liquidity',
          defaultBankAccountId: saved.defaultBankAccountId ?? '',
          defaultLiquidityAccountId: saved.defaultLiquidityAccountId ?? '',
          isActive: saved.isActive,
        });
      })
      .catch((error) => {
        if (current)
          toast.error(
            error instanceof Error
              ? error.message
              : 'Could not load the prospect deposit policy.'
          );
      })
      .finally(() => {
        if (current) setProspectPolicyLoading(false);
      });
    return () => {
      current = false;
    };
  }, [prospectPolicy.salesSaleableSourceId]);

  const allMapped = useMemo(
    () =>
      setup?.missingAccountTypes.every((type) => Boolean(mappings[type])) ??
      false,
    [mappings, setup]
  );

  const selectedSaleableSource = useMemo(
    () =>
      saleableSources.find(
        (source) => source.id === prospectPolicy.salesSaleableSourceId
      ),
    [prospectPolicy.salesSaleableSourceId, saleableSources]
  );
  const prospectCurrency = (
    selectedSaleableSource?.defaultCurrency ||
    setup?.baseCurrency ||
    ''
  ).toUpperCase();
  const eligibleBankAccounts = useMemo(
    () =>
      bankAccounts.filter(
        (account) => account.currency.toUpperCase() === prospectCurrency
      ),
    [bankAccounts, prospectCurrency]
  );
  const eligibleLiquidityAccounts = useMemo(
    () =>
      liquidityAccounts.filter(
        (account) => account.currency.toUpperCase() === prospectCurrency
      ),
    [liquidityAccounts, prospectCurrency]
  );

  const completeSetup = async () => {
    if (!setup || !allMapped) return;
    setSaving(true);
    try {
      await cashManagementDataService.completeBankingSetup({
        depositPolicy: policy,
        requirePrimaryEvidence: true,
        accounts: setup.missingAccountTypes.map((type) => ({
          accountType: type,
          code: `${defaultCodes[type] ?? type.toUpperCase()}-${setup.baseCurrency}`,
          name: `${labels[type]} (${setup.baseCurrency})`,
          currency: setup.baseCurrency,
          glAccountId: mappings[type],
        })),
      });
      toast.success('Banking & Settlement setup completed.');
      await load();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Setup could not be completed.'
      );
    } finally {
      setSaving(false);
    }
  };

  const saveProspectDepositPolicy = async () => {
    if (!prospectPolicy.salesSaleableSourceId) {
      toast.error('Select a saleable source.');
      return;
    }
    if (!prospectPolicy.depositLiabilityAccountId) {
      toast.error('Select the prospect deposit liability account.');
      return;
    }
    const fixedAmount = Number(prospectPolicy.fixedAmount);
    const percentage = Number(prospectPolicy.percentage);
    if (
      prospectPolicy.requirementType === 'Fixed' &&
      (!Number.isFinite(fixedAmount) || fixedAmount <= 0)
    ) {
      toast.error('Enter a positive fixed deposit amount.');
      return;
    }
    if (
      prospectPolicy.requirementType === 'Percentage' &&
      (!Number.isFinite(percentage) || percentage <= 0 || percentage > 100)
    ) {
      toast.error('Enter a percentage greater than zero and no more than 100.');
      return;
    }
    const bankId =
      prospectPolicy.cashDestinationType === 'Bank'
        ? prospectPolicy.defaultBankAccountId
        : '';
    const liquidityId =
      prospectPolicy.cashDestinationType === 'Liquidity'
        ? prospectPolicy.defaultLiquidityAccountId
        : '';
    if (Boolean(bankId) === Boolean(liquidityId)) {
      toast.error('Select exactly one bank or liquidity account.');
      return;
    }

    setProspectPolicySaving(true);
    try {
      await propertyEnquiryService.upsertDepositPolicy({
        salesSaleableSourceId: prospectPolicy.salesSaleableSourceId,
        requirementType: prospectPolicy.requirementType,
        fixedAmount:
          prospectPolicy.requirementType === 'Fixed' ? fixedAmount : null,
        percentage:
          prospectPolicy.requirementType === 'Percentage' ? percentage : null,
        depositLiabilityAccountId: prospectPolicy.depositLiabilityAccountId,
        defaultBankAccountId: bankId || null,
        defaultLiquidityAccountId: liquidityId || null,
        isActive: prospectPolicy.isActive,
      });
      toast.success('Property prospect deposit policy saved.');
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Could not save the prospect deposit policy.'
      );
    } finally {
      setProspectPolicySaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/finance/cash">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Liquidity Accounts</h1>
            <p className="text-muted-foreground">
              Bank, till, and settlement locations with their own GL control
              accounts.
            </p>
          </div>
        </div>
        {setup?.isConfigured && (
          <Button asChild>
            <Link href="/finance/cash/liquidity-accounts/new">
              <Plus className="mr-2 h-4 w-4" />
              New account
            </Link>
          </Button>
        )}
      </div>

      {setup?.requiresProvisioningWizard ? (
        <Card className="border-amber-300">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-amber-600" />
              Banking setup required
            </CardTitle>
            <CardDescription>
              This tenant uses its existing {setup.coaType} chart of accounts.
              Map each operational holding location to an established GL
              account; the system will not silently add accounts to your COA.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            {setup.missingAccountTypes.map((type) => (
              <div
                key={type}
                className="grid gap-2 md:grid-cols-[260px_1fr] md:items-center"
              >
                <div>
                  <Label>{labels[type]}</Label>
                  <p className="text-xs text-muted-foreground">
                    {defaultCodes[type]} · {setup.baseCurrency}
                  </p>
                </div>
                <Select
                  value={mappings[type] ?? ''}
                  onValueChange={(value) =>
                    setMappings((current) => ({ ...current, [type]: value }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select an existing GL control account" />
                  </SelectTrigger>
                  <SelectContent>
                    {accounts.map((account) => (
                      <SelectItem key={account.id} value={account.id}>
                        {account.accountNumber} — {account.accountName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ))}
            <div className="grid gap-2 md:grid-cols-[260px_1fr] md:items-center">
              <div>
                <Label>Deposit policy</Label>
                <p className="text-xs text-muted-foreground">
                  Can approved expenses reduce the amount banked?
                </p>
              </div>
              <Select
                value={policy}
                onValueChange={(value) => setPolicy(value as DepositPolicy)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="DepositIntact">
                    Deposit intact — no deductions
                  </SelectItem>
                  <SelectItem value="ControlledNetBanking">
                    Controlled net banking
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="rounded-md bg-muted p-3 text-sm">
              Deposit slips and bank-return advice are required before workflow
              submission. The Chief Accountant is the default approver.
            </div>
            <Button onClick={completeSetup} disabled={!allMapped || saving}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Complete setup
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {setup?.accounts.map((account) => (
            <Card key={account.id}>
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex gap-3">
                    {account.accountType === 'Bank' ? (
                      <Landmark className="mt-1 h-5 w-5" />
                    ) : (
                      <WalletCards className="mt-1 h-5 w-5" />
                    )}
                    <div>
                      <CardTitle className="text-lg">{account.name}</CardTitle>
                      <CardDescription>{account.code}</CardDescription>
                    </div>
                  </div>
                  <Badge variant={account.isActive ? 'default' : 'secondary'}>
                    {account.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="text-2xl font-semibold">
                  {account.currency}{' '}
                  {account.currentBalance.toLocaleString(undefined, {
                    minimumFractionDigits: 2,
                  })}
                </div>
                <div className="grid grid-cols-2 gap-3 text-sm">
                  <div>
                    <p className="text-muted-foreground">Available</p>
                    <p>
                      {account.currency}{' '}
                      {account.availableToSettle.toLocaleString(undefined, {
                        minimumFractionDigits: 2,
                      })}
                    </p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">Open items</p>
                    <p>{account.openEntryCount}</p>
                  </div>
                </div>
                <div className="border-t pt-3 text-sm">
                  <p className="text-muted-foreground">GL control account</p>
                  <p>
                    {account.glAccountNumber} — {account.glAccountName}
                  </p>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5" />
            Property Prospect Deposit Policy
          </CardTitle>
          <CardDescription>
            Set the payment threshold and existing Finance accounts used before
            a public property prospect can be registered as a customer.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {saleableSources.length === 0 ? (
            <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
              No active saleable source is available. Configure one in{' '}
              <Link
                className="font-medium underline"
                href="/administration/sales"
              >
                Sales setup
              </Link>{' '}
              first.
            </div>
          ) : (
            <>
              <div className="grid gap-4 lg:grid-cols-2">
                <div className="space-y-2">
                  <Label>Saleable source</Label>
                  <Select
                    value={prospectPolicy.salesSaleableSourceId}
                    onValueChange={(salesSaleableSourceId) =>
                      setProspectPolicy(
                        emptyProspectDepositPolicy(salesSaleableSourceId)
                      )
                    }
                    disabled={prospectPolicyLoading || prospectPolicySaving}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select source" />
                    </SelectTrigger>
                    <SelectContent>
                      {saleableSources.map((source) => (
                        <SelectItem key={source.id} value={source.id}>
                          {source.code} — {source.displayName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {selectedSaleableSource && (
                    <p className="text-xs text-muted-foreground">
                      {selectedSaleableSource.adapterKey} · {prospectCurrency}
                    </p>
                  )}
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <Label htmlFor="prospect-policy-active">Active</Label>
                    <p className="text-xs text-muted-foreground">
                      Use this source policy for new qualifications and
                      deposits.
                    </p>
                  </div>
                  <Switch
                    id="prospect-policy-active"
                    checked={prospectPolicy.isActive}
                    onCheckedChange={(isActive) =>
                      setProspectPolicy((current) => ({ ...current, isActive }))
                    }
                    disabled={prospectPolicyLoading || prospectPolicySaving}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Required payment</Label>
                  <Select
                    value={prospectPolicy.requirementType}
                    onValueChange={(requirementType) =>
                      setProspectPolicy((current) => ({
                        ...current,
                        requirementType:
                          requirementType as ProspectDepositRequirementType,
                      }))
                    }
                    disabled={prospectPolicyLoading || prospectPolicySaving}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Fixed">Fixed amount</SelectItem>
                      <SelectItem value="Percentage">
                        Percentage of agreed price
                      </SelectItem>
                      <SelectItem value="Full">Full agreed price</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                {prospectPolicy.requirementType === 'Fixed' && (
                  <div className="space-y-2">
                    <Label htmlFor="prospect-fixed-amount">
                      Fixed amount ({prospectCurrency})
                    </Label>
                    <Input
                      id="prospect-fixed-amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={prospectPolicy.fixedAmount}
                      onChange={(event) =>
                        setProspectPolicy((current) => ({
                          ...current,
                          fixedAmount: event.target.value,
                        }))
                      }
                    />
                  </div>
                )}
                {prospectPolicy.requirementType === 'Percentage' && (
                  <div className="space-y-2">
                    <Label htmlFor="prospect-percentage">Percentage</Label>
                    <Input
                      id="prospect-percentage"
                      type="number"
                      min="0.0001"
                      max="100"
                      step="0.0001"
                      value={prospectPolicy.percentage}
                      onChange={(event) =>
                        setProspectPolicy((current) => ({
                          ...current,
                          percentage: event.target.value,
                        }))
                      }
                    />
                  </div>
                )}
                <div className="space-y-2 lg:col-span-2">
                  <Label>Prospect deposit liability account</Label>
                  <Select
                    value={prospectPolicy.depositLiabilityAccountId}
                    onValueChange={(depositLiabilityAccountId) =>
                      setProspectPolicy((current) => ({
                        ...current,
                        depositLiabilityAccountId,
                      }))
                    }
                    disabled={prospectPolicyLoading || prospectPolicySaving}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select active direct-posting liability" />
                    </SelectTrigger>
                    <SelectContent>
                      {liabilityAccounts.map((account) => (
                        <SelectItem key={account.id} value={account.id}>
                          {account.accountNumber} — {account.accountName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {liabilityAccounts.length === 0 && (
                    <p className="text-xs text-amber-700">
                      No active, direct-posting, non-control liability account
                      is configured.
                    </p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label>Cash destination type</Label>
                  <Select
                    value={prospectPolicy.cashDestinationType}
                    onValueChange={(cashDestinationType) =>
                      setProspectPolicy((current) => ({
                        ...current,
                        cashDestinationType: cashDestinationType as
                          | 'Bank'
                          | 'Liquidity',
                        defaultBankAccountId:
                          cashDestinationType === 'Bank'
                            ? current.defaultBankAccountId
                            : '',
                        defaultLiquidityAccountId:
                          cashDestinationType === 'Liquidity'
                            ? current.defaultLiquidityAccountId
                            : '',
                      }))
                    }
                    disabled={prospectPolicyLoading || prospectPolicySaving}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Bank">Bank account</SelectItem>
                      <SelectItem value="Liquidity">
                        Liquidity account
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>
                    {prospectPolicy.cashDestinationType === 'Bank'
                      ? 'Bank account'
                      : 'Liquidity account'}
                  </Label>
                  {prospectPolicy.cashDestinationType === 'Bank' ? (
                    <Select
                      value={prospectPolicy.defaultBankAccountId}
                      onValueChange={(defaultBankAccountId) =>
                        setProspectPolicy((current) => ({
                          ...current,
                          defaultBankAccountId,
                        }))
                      }
                      disabled={prospectPolicyLoading || prospectPolicySaving}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={`Select ${prospectCurrency} bank account`}
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {eligibleBankAccounts.map((account) => (
                          <SelectItem key={account.id} value={account.id}>
                            {account.bankName} — {account.accountName} (
                            {account.accountNumber})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    <Select
                      value={prospectPolicy.defaultLiquidityAccountId}
                      onValueChange={(defaultLiquidityAccountId) =>
                        setProspectPolicy((current) => ({
                          ...current,
                          defaultLiquidityAccountId,
                        }))
                      }
                      disabled={prospectPolicyLoading || prospectPolicySaving}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={`Select ${prospectCurrency} liquidity account`}
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {eligibleLiquidityAccounts.map((account) => (
                          <SelectItem key={account.id} value={account.id}>
                            {account.code} — {account.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                  {(prospectPolicy.cashDestinationType === 'Bank'
                    ? eligibleBankAccounts
                    : eligibleLiquidityAccounts
                  ).length === 0 && (
                    <p className="text-xs text-amber-700">
                      No active {prospectCurrency}{' '}
                      {prospectPolicy.cashDestinationType.toLowerCase()} account
                      with Finance mapping is available.
                    </p>
                  )}
                </div>
              </div>
              <div className="flex justify-end">
                <Button
                  onClick={saveProspectDepositPolicy}
                  disabled={prospectPolicyLoading || prospectPolicySaving}
                >
                  {prospectPolicySaving ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="mr-2 h-4 w-4" />
                  )}
                  Save prospect deposit policy
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
