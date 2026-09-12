'use client';

import Link from 'next/link';
import React from 'react';
import { useSearchParams } from 'next/navigation';
import {
  AlertTriangle,
  Banknote,
  CalendarClock,
  FileText,
  Landmark,
  Loader2,
  Plus,
  Receipt,
  RefreshCw,
  TrendingUp,
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import { usePaginatedItems } from '@/hooks/use-paginated-items';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import {
  estateGroundRentService,
  type GroundRentAccount,
  type GroundRentAssetOption,
  type GroundRentCharge,
  type GroundRentOptions,
  type RecordGroundRentReceipt,
  type UpsertGroundRentAccount,
} from '@/services/estate-ground-rent.service';

const today = () => new Date().toISOString().slice(0, 10);

const dateInput = (value?: string) => (value ? value.slice(0, 10) : '');

function formatDate(value?: string) {
  if (!value) return 'Not scheduled';
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function formatMoney(value: number, currency = 'GHS') {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value || 0);
}

function statusVariant(status: string) {
  if (status === 'Paid') return 'default' as const;
  if (status === 'Overdue') return 'destructive' as const;
  if (status === 'PartiallyPaid') return 'secondary' as const;
  return 'outline' as const;
}

const emptyForm = (): UpsertGroundRentAccount => ({
  estateManagedAssetId: '',
  customerBusinessPartnerId: '',
  paymentFrequency: 'Annual',
  calculationMethod: 'ApprovedAssessment',
  annualAmount: null,
  ratePerAcre: null,
  currencyCode: 'GHS',
  nextDueDate: today(),
  paymentTermsDays: 30,
  reviewFrequencyMonths: 12,
  nextReviewDate: '',
  escalationMethod: 'None',
  escalationValue: 0,
  gracePeriodDays: 30,
  penaltyMethod: 'None',
  penaltyValue: 0,
  penaltyCapAmount: null,
  groundRentIncomeAccountId: '',
  autoPostInvoices: false,
  status: 'Active',
  notes: '',
});

interface ReceiptDialogState {
  account: GroundRentAccount;
  charge: GroundRentCharge;
  target: 'Base' | 'Penalty';
}

export function GroundRentAdministrationWorkspace() {
  const searchParams = useSearchParams();
  const requestedAssetId = searchParams.get('assetId')?.trim() || '';
  const appliedRequestedAssetRef = React.useRef('');
  const [accounts, setAccounts] = React.useState<GroundRentAccount[]>([]);
  const [options, setOptions] = React.useState<GroundRentOptions>({
    assets: [],
    incomeAccounts: [],
  });
  const [selectedAccountId, setSelectedAccountId] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [actionKey, setActionKey] = React.useState('');
  const [setupOpen, setSetupOpen] = React.useState(false);
  const [assessmentAssetId, setAssessmentAssetId] = React.useState('');
  const [assessmentRate, setAssessmentRate] = React.useState('');
  const [assessmentCurrency, setAssessmentCurrency] = React.useState('GHS');
  const [setupForm, setSetupForm] =
    React.useState<UpsertGroundRentAccount>(emptyForm);
  const [reviewAccount, setReviewAccount] =
    React.useState<GroundRentAccount | null>(null);
  const [reviewMethod, setReviewMethod] = React.useState('Percentage');
  const [reviewValue, setReviewValue] = React.useState('');
  const [reviewDate, setReviewDate] = React.useState(today());
  const [reviewNotes, setReviewNotes] = React.useState('');
  const [receiptDialog, setReceiptDialog] =
    React.useState<ReceiptDialogState | null>(null);
  const accountPages = usePaginatedItems(accounts, 10);
  const [receiptForm, setReceiptForm] = React.useState<RecordGroundRentReceipt>(
    {
      target: 'Base',
      paymentDate: today(),
      amount: 0,
      paymentMethod: 'Cash',
      transactionReference: '',
      currencyCode: 'GHS',
      exchangeRate: 1,
      notes: '',
    }
  );

  const loadWorkspace = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const [loadedAccounts, loadedOptions] = await Promise.all([
        estateGroundRentService.getAccounts(),
        estateGroundRentService.getOptions(),
      ]);
      setAccounts(loadedAccounts);
      setOptions(loadedOptions);
      const requestedAccount = requestedAssetId
        ? loadedAccounts.find(
            (account) => account.estateManagedAssetId === requestedAssetId
          )
        : undefined;

      setSelectedAccountId((current) => {
        if (requestedAccount) return requestedAccount.id;
        return loadedAccounts.some((account) => account.id === current)
          ? current
          : loadedAccounts[0]?.id || '';
      });

      if (
        requestedAssetId &&
        !requestedAccount &&
        appliedRequestedAssetRef.current !== requestedAssetId
      ) {
        const requestedAsset = loadedOptions.assets.find(
          (asset) => asset.id === requestedAssetId
        );
        if (requestedAsset) {
          appliedRequestedAssetRef.current = requestedAssetId;
          setSetupForm({
            ...emptyForm(),
            estateManagedAssetId: requestedAsset.id,
            customerBusinessPartnerId:
              requestedAsset.customerBusinessPartnerId || '',
            currencyCode: requestedAsset.currencyCode || 'GHS',
            annualAmount: requestedAsset.approvedAnnualGroundRent ?? null,
            ratePerAcre: requestedAsset.approvedRatePerAcre ?? null,
          });
          setSetupOpen(true);
        }
      }
    } catch (error: unknown) {
      setLoadError(
        error instanceof Error
          ? error.message
          : 'Unable to load Ground Rent Administration.'
      );
    } finally {
      setIsLoading(false);
    }
  }, [requestedAssetId]);

  React.useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const selectedAccount =
    accounts.find((account) => account.id === selectedAccountId) || null;
  const selectedAsset = options.assets.find(
    (asset) => asset.id === setupForm.estateManagedAssetId
  );
  const assessmentAsset = options.assets.find(
    (asset) => asset.id === assessmentAssetId
  );
  const assessmentAmount =
    assessmentAsset?.areaAcres && Number(assessmentRate) > 0
      ? Math.ceil(assessmentAsset.areaAcres * Number(assessmentRate))
      : null;

  const totals = React.useMemo(
    () => ({
      annual: accounts.reduce(
        (sum, account) =>
          account.status === 'Active' ? sum + account.annualAmount : sum,
        0
      ),
      outstanding: accounts.reduce(
        (sum, account) => sum + account.outstandingAmount,
        0
      ),
      arrears: accounts.reduce(
        (sum, account) => sum + account.arrearsAmount,
        0
      ),
      reviewsDue: accounts.filter(
        (account) =>
          account.nextReviewDate &&
          new Date(account.nextReviewDate) <= new Date()
      ).length,
    }),
    [accounts]
  );

  const replaceAccount = (account: GroundRentAccount) => {
    setAccounts((current) => {
      const exists = current.some((item) => item.id === account.id);
      return exists
        ? current.map((item) => (item.id === account.id ? account : item))
        : [account, ...current];
    });
    setSelectedAccountId(account.id);
  };

  const openNewSetup = () => {
    setSetupForm(emptyForm());
    setSetupOpen(true);
  };

  const openEditSetup = (account: GroundRentAccount) => {
    setSetupForm({
      estateManagedAssetId: account.estateManagedAssetId,
      customerBusinessPartnerId: account.customerBusinessPartnerId,
      paymentFrequency: account.paymentFrequency,
      calculationMethod: account.calculationMethod,
      annualAmount: account.annualAmount,
      ratePerAcre: account.ratePerAcre ?? null,
      currencyCode: account.currencyCode,
      nextDueDate: dateInput(account.nextDueDate),
      paymentTermsDays: account.paymentTermsDays,
      reviewFrequencyMonths: account.reviewFrequencyMonths,
      nextReviewDate: dateInput(account.nextReviewDate),
      escalationMethod: account.escalationMethod,
      escalationValue: account.escalationValue,
      gracePeriodDays: account.gracePeriodDays,
      penaltyMethod: account.penaltyMethod,
      penaltyValue: account.penaltyValue,
      penaltyCapAmount: account.penaltyCapAmount ?? null,
      groundRentIncomeAccountId: account.groundRentIncomeAccountId,
      autoPostInvoices: account.autoPostInvoices,
      status: account.status,
      notes: account.notes || '',
    });
    setSetupOpen(true);
  };

  const selectAsset = (assetId: string) => {
    const asset = options.assets.find((item) => item.id === assetId);
    setSetupForm((current) => ({
      ...current,
      estateManagedAssetId: assetId,
      customerBusinessPartnerId: asset?.customerBusinessPartnerId || '',
      currencyCode: asset?.currencyCode || current.currencyCode,
      annualAmount: asset?.approvedAnnualGroundRent ?? current.annualAmount,
      ratePerAcre: asset?.approvedRatePerAcre ?? current.ratePerAcre,
    }));
  };

  const saveSetup = async () => {
    if (
      !setupForm.estateManagedAssetId ||
      !setupForm.customerBusinessPartnerId ||
      !setupForm.groundRentIncomeAccountId
    ) {
      toast.error(
        'Select a property with a linked customer and a ground-rent income account.'
      );
      return;
    }

    setActionKey('save-account');
    try {
      const saved = await estateGroundRentService.saveAccount({
        ...setupForm,
        nextReviewDate: setupForm.nextReviewDate || null,
        notes: setupForm.notes?.trim() || null,
      });
      replaceAccount(saved);
      setSetupOpen(false);
      toast.success('Ground-rent schedule saved.');
      void loadWorkspace();
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to save the ground-rent schedule.'
      );
    } finally {
      setActionKey('');
    }
  };

  const saveAssessment = async () => {
    if (!assessmentAssetId || Number(assessmentRate) <= 0) {
      toast.error('Select a land parcel and enter the approved rate per acre.');
      return;
    }

    setActionKey('save-assessment');
    try {
      await estateGroundRentService.assessAsset({
        estateManagedAssetId: assessmentAssetId,
        ratePerAcre: Number(assessmentRate),
        currencyCode: assessmentCurrency,
      });
      toast.success('Pre-listing ground-rent assessment saved.');
      await loadWorkspace();
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to save the pre-listing ground-rent assessment.'
      );
    } finally {
      setActionKey('');
    }
  };

  const generateInvoice = async (account: GroundRentAccount) => {
    setActionKey(`invoice-${account.id}`);
    try {
      const result = await estateGroundRentService.generateInvoice(
        account.id,
        false
      );
      replaceAccount(result.account);
      toast.success(result.message);
    } catch (error: unknown) {
      toast.error(
        error instanceof Error ? error.message : 'Unable to generate invoice.'
      );
    } finally {
      setActionKey('');
    }
  };

  const assessPenalty = async (charge: GroundRentCharge) => {
    setActionKey(`penalty-${charge.id}`);
    try {
      const result = await estateGroundRentService.assessPenalty(charge.id);
      replaceAccount(result.account);
      toast.success(result.message);
    } catch (error: unknown) {
      toast.error(
        error instanceof Error ? error.message : 'Unable to assess penalty.'
      );
    } finally {
      setActionKey('');
    }
  };

  const postInvoice = async (
    charge: GroundRentCharge,
    target: 'Base' | 'Penalty'
  ) => {
    setActionKey(`post-${target}-${charge.id}`);
    try {
      const result = await estateGroundRentService.postInvoice(
        charge.id,
        target
      );
      replaceAccount(result.account);
      toast.success(result.message);
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to post the Finance AR invoice.'
      );
    } finally {
      setActionKey('');
    }
  };

  const openReview = (account: GroundRentAccount) => {
    setReviewAccount(account);
    setReviewMethod(
      account.escalationMethod === 'None'
        ? 'Percentage'
        : account.escalationMethod
    );
    setReviewValue(
      account.escalationValue > 0 ? String(account.escalationValue) : ''
    );
    setReviewDate(today());
    setReviewNotes('');
  };

  const applyReview = async () => {
    if (!reviewAccount || Number(reviewValue) <= 0) {
      toast.error('Enter an escalation value greater than zero.');
      return;
    }
    setActionKey(`review-${reviewAccount.id}`);
    try {
      const result = await estateGroundRentService.applyReview(
        reviewAccount.id,
        {
          effectiveDate: reviewDate,
          escalationMethod: reviewMethod,
          escalationValue: Number(reviewValue),
          notes: reviewNotes.trim() || undefined,
        }
      );
      replaceAccount(result.account);
      setReviewAccount(null);
      toast.success(result.message);
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to apply the ground-rent review.'
      );
    } finally {
      setActionKey('');
    }
  };

  const openReceipt = (
    account: GroundRentAccount,
    charge: GroundRentCharge,
    target: 'Base' | 'Penalty'
  ) => {
    const penaltyOutstanding = Math.max(
      0,
      charge.penaltyAmount - Math.max(0, charge.paidAmount - charge.baseAmount)
    );
    const amount =
      target === 'Penalty'
        ? penaltyOutstanding || charge.penaltyAmount
        : Math.min(charge.baseAmount, charge.outstandingAmount);
    setReceiptDialog({ account, charge, target });
    setReceiptForm({
      target,
      paymentDate: today(),
      amount,
      paymentMethod: 'Cash',
      transactionReference: '',
      currencyCode: account.currencyCode,
      exchangeRate: 1,
      notes: '',
    });
  };

  const recordReceipt = async () => {
    if (!receiptDialog || receiptForm.amount <= 0) {
      toast.error('Enter a receipt amount greater than zero.');
      return;
    }
    setActionKey(`receipt-${receiptDialog.charge.id}`);
    try {
      const result = await estateGroundRentService.recordReceipt(
        receiptDialog.charge.id,
        receiptForm
      );
      replaceAccount(result.account);
      setReceiptDialog(null);
      toast.success(result.message);
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to record the Finance AR receipt.'
      );
    } finally {
      setActionKey('');
    }
  };

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Active schedules</CardDescription>
            <CardTitle>
              {accounts.filter((a) => a.status === 'Active').length}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Annual ground rent</CardDescription>
            <CardTitle>{formatMoney(totals.annual)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Outstanding / arrears</CardDescription>
            <CardTitle>{formatMoney(totals.outstanding)}</CardTitle>
            <div className="text-xs text-destructive">
              {formatMoney(totals.arrears)} overdue
            </div>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Reviews due</CardDescription>
            <CardTitle>{totals.reviewsDue}</CardTitle>
          </CardHeader>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <TrendingUp className="h-5 w-5 text-primary" />
            Pre-listing Ground Rent Assessment
          </CardTitle>
          <CardDescription>
            Calculate and approve the annual ground rent before publishing an
            unallocated land parcel to the customer portal.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 lg:grid-cols-[2fr_1fr_1fr_auto] lg:items-end">
          <div className="space-y-2">
            <Label>Land parcel</Label>
            <Select
              value={assessmentAssetId}
              onValueChange={(value) => {
                const asset = options.assets.find((item) => item.id === value);
                setAssessmentAssetId(value);
                setAssessmentRate(
                  asset?.approvedRatePerAcre == null
                    ? ''
                    : String(asset.approvedRatePerAcre)
                );
                setAssessmentCurrency(asset?.currencyCode || 'GHS');
              }}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select a land parcel" />
              </SelectTrigger>
              <SelectContent>
                {options.assets.map((asset) => (
                  <SelectItem key={asset.id} value={asset.id}>
                    {asset.assetCode} - {asset.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Approved rate per acre</Label>
            <Input
              type="number"
              min="0.01"
              step="0.01"
              value={assessmentRate}
              onChange={(event) => setAssessmentRate(event.target.value)}
            />
          </div>
          <div className="rounded-md border bg-muted/30 p-3">
            <div className="text-xs text-muted-foreground">
              Annual ground rent
            </div>
            <div className="mt-1 font-semibold">
              {assessmentAmount == null
                ? 'Select parcel and rate'
                : formatMoney(assessmentAmount, assessmentCurrency)}
            </div>
            <div className="text-xs text-muted-foreground">
              {assessmentAsset?.areaAcres
                ? `${assessmentAsset.areaAcres.toFixed(4)} acres`
                : 'Parcel area required'}
            </div>
          </div>
          <Button
            type="button"
            onClick={() => void saveAssessment()}
            disabled={
              actionKey === 'save-assessment' || assessmentAmount == null
            }
          >
            {actionKey === 'save-assessment' ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Banknote className="mr-2 h-4 w-4" />
            )}
            Save Assessment
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Landmark className="h-5 w-5 text-primary" />
                Ground Rent Accounts
              </CardTitle>
              <CardDescription className="mt-2 max-w-4xl">
                Configure payment frequency, due dates, approved calculation
                basis, rent reviews, escalation, arrears penalties, Finance AR
                invoices, GL postings, and allocated receipts.
              </CardDescription>
            </div>
            <div className="flex gap-2">
              <Button
                type="button"
                size="icon"
                variant="outline"
                aria-label="Refresh ground-rent accounts"
                disabled={isLoading}
                onClick={() => void loadWorkspace()}
              >
                <RefreshCw className="h-4 w-4" />
              </Button>
              <Button type="button" onClick={openNewSetup}>
                <Plus className="mr-2 h-4 w-4" />
                Set up ground rent
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loadError ? (
            <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
              {loadError}
            </div>
          ) : null}

          {isLoading ? (
            <div className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading ground-rent accounts
            </div>
          ) : null}

          {!isLoading && accounts.length === 0 ? (
            <div className="rounded-md border border-dashed p-10 text-center">
              <Banknote className="mx-auto h-8 w-8 text-muted-foreground" />
              <div className="mt-3 font-medium">
                No ground-rent schedules have been configured
              </div>
              <p className="mt-1 text-sm text-muted-foreground">
                Start from a property or unit that already has a linked Finance
                AR customer.
              </p>
            </div>
          ) : null}

          {!isLoading && accounts.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Property / customer</TableHead>
                    <TableHead>Frequency</TableHead>
                      <TableHead>Annual / period</TableHead>
                      <TableHead>Billing starts</TableHead>
                      <TableHead>Next due</TableHead>
                    <TableHead>Next review</TableHead>
                    <TableHead>Outstanding</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {accountPages.items.map((account) => (
                    <TableRow
                      key={account.id}
                      className={
                        selectedAccountId === account.id ? 'bg-muted/30' : ''
                      }
                    >
                      <TableCell>
                        <button
                          type="button"
                          className="text-left"
                          onClick={() => setSelectedAccountId(account.id)}
                        >
                          <div className="font-medium">{account.assetName}</div>
                          <div className="text-xs text-muted-foreground">
                            {account.assetCode} · {account.customerName}
                          </div>
                        </button>
                      </TableCell>
                      <TableCell>{account.paymentFrequency}</TableCell>
                      <TableCell>
                        <div>
                          {formatMoney(
                            account.annualAmount,
                            account.currencyCode
                          )}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {formatMoney(
                            account.amountPerPeriod,
                            account.currencyCode
                          )}{' '}
                          per period
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{formatDate(account.billingStartDate)}</div>
                        <div className="text-xs text-muted-foreground">
                          {account.billingStartSource}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(account.nextDueDate)}</TableCell>
                      <TableCell>
                        {formatDate(account.nextReviewDate)}
                      </TableCell>
                      <TableCell>
                        <div>
                          {formatMoney(
                            account.outstandingAmount,
                            account.currencyCode
                          )}
                        </div>
                        {account.arrearsAmount > 0 ? (
                          <div className="text-xs text-destructive">
                            {formatMoney(
                              account.arrearsAmount,
                              account.currencyCode
                            )}{' '}
                            arrears
                          </div>
                        ) : null}
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{account.status}</Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex justify-end gap-2">
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            onClick={() => openEditSetup(account)}
                          >
                            Configure
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            disabled={
                              Boolean(actionKey) ||
                              !account.canGenerateInvoice
                            }
                            title={
                              account.canGenerateInvoice
                                ? undefined
                                : account.invoiceHoldReason
                            }
                            onClick={() => void generateInvoice(account)}
                          >
                            {actionKey === `invoice-${account.id}` ? (
                              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            ) : (
                              <FileText className="mr-2 h-4 w-4" />
                            )}
                            Generate invoice
                          </Button>
                          {!account.canGenerateInvoice &&
                          account.invoiceHoldReason ? (
                            <div className="max-w-48 text-right text-xs text-muted-foreground">
                              {account.invoiceHoldReason}
                            </div>
                          ) : null}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {accounts.length > accountPages.pageSize ? <Pagination currentPage={accountPages.currentPage} totalPages={accountPages.totalPages} totalItems={accountPages.totalItems} pageSize={accountPages.pageSize} onPageChange={accountPages.setCurrentPage} /> : null}
        </CardContent>
      </Card>

      {selectedAccount ? (
        <div className="grid gap-6 xl:grid-cols-[2fr_1fr]">
          <Card>
            <CardHeader>
              <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div>
                  <CardTitle>Charge, invoice and receipt history</CardTitle>
                  <CardDescription className="mt-2">
                    {selectedAccount.assetCode} · {selectedAccount.customerName}
                  </CardDescription>
                </div>
                <Badge variant="secondary">
                  {selectedAccount.charges.length} periods
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              {selectedAccount.charges.length === 0 ? (
                <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
                  No ground-rent invoices have been generated yet.
                </div>
              ) : (
                <div className="overflow-x-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Period / due</TableHead>
                        <TableHead>Invoice</TableHead>
                        <TableHead>Amount</TableHead>
                        <TableHead>Paid / outstanding</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="text-right">
                          Finance actions
                        </TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selectedAccount.charges.map((charge) => (
                        <TableRow key={charge.id}>
                          <TableCell>
                            <div>
                              {formatDate(charge.periodStart)} –{' '}
                              {formatDate(charge.periodEnd)}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              Due {formatDate(charge.dueDate)}
                            </div>
                          </TableCell>
                          <TableCell>
                            {charge.financeInvoiceId ? (
                              <Link
                                className="text-primary hover:underline"
                                href={`/finance/ar/invoices/${charge.financeInvoiceId}`}
                              >
                                {charge.financeInvoiceNumber}
                              </Link>
                            ) : (
                              'Not generated'
                            )}
                            {charge.penaltyInvoiceId ? (
                              <div className="text-xs text-destructive">
                                Penalty:{' '}
                                <Link
                                  className="hover:underline"
                                  href={`/finance/ar/invoices/${charge.penaltyInvoiceId}`}
                                >
                                  {charge.penaltyInvoiceNumber}
                                </Link>
                              </div>
                            ) : null}
                          </TableCell>
                          <TableCell>
                            <div>
                              {formatMoney(
                                charge.baseAmount,
                                selectedAccount.currencyCode
                              )}
                            </div>
                            {charge.penaltyAmount > 0 ? (
                              <div className="text-xs text-destructive">
                                +{' '}
                                {formatMoney(
                                  charge.penaltyAmount,
                                  selectedAccount.currencyCode
                                )}{' '}
                                penalty
                              </div>
                            ) : null}
                          </TableCell>
                          <TableCell>
                            <div>
                              {formatMoney(
                                charge.paidAmount,
                                selectedAccount.currencyCode
                              )}{' '}
                              paid
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {formatMoney(
                                charge.outstandingAmount,
                                selectedAccount.currencyCode
                              )}{' '}
                              outstanding
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge variant={statusVariant(charge.status)}>
                              {charge.status}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            <div className="flex flex-wrap justify-end gap-2">
                              {!charge.financeJournalEntryId ? (
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="outline"
                                  disabled={Boolean(actionKey)}
                                  onClick={() =>
                                    void postInvoice(charge, 'Base')
                                  }
                                >
                                  Post invoice
                                </Button>
                              ) : null}
                              {charge.outstandingAmount > 0 ? (
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="outline"
                                  onClick={() =>
                                    openReceipt(selectedAccount, charge, 'Base')
                                  }
                                >
                                  <Receipt className="mr-2 h-4 w-4" />
                                  Receipt
                                </Button>
                              ) : null}
                              {charge.status === 'Overdue' &&
                              !charge.penaltyInvoiceId &&
                              selectedAccount.penaltyMethod !== 'None' ? (
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="destructive"
                                  disabled={Boolean(actionKey)}
                                  onClick={() => void assessPenalty(charge)}
                                >
                                  <AlertTriangle className="mr-2 h-4 w-4" />
                                  Assess penalty
                                </Button>
                              ) : null}
                              {charge.penaltyInvoiceId &&
                              !charge.penaltyJournalEntryId ? (
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="outline"
                                  disabled={Boolean(actionKey)}
                                  onClick={() =>
                                    void postInvoice(charge, 'Penalty')
                                  }
                                >
                                  Post penalty
                                </Button>
                              ) : null}
                              {charge.penaltyInvoiceId &&
                              charge.penaltyAmount > 0 ? (
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="outline"
                                  onClick={() =>
                                    openReceipt(
                                      selectedAccount,
                                      charge,
                                      'Penalty'
                                    )
                                  }
                                >
                                  Penalty receipt
                                </Button>
                              ) : null}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>

          <div className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <TrendingUp className="h-5 w-5 text-primary" />
                  Rent review
                </CardTitle>
                <CardDescription>
                  {selectedAccount.escalationMethod === 'None'
                    ? 'No automatic escalation rule configured.'
                    : `${selectedAccount.escalationMethod}: ${selectedAccount.escalationValue}`}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="rounded-md border p-3 text-sm">
                  <div className="text-muted-foreground">Next review</div>
                  <div className="font-medium">
                    {formatDate(selectedAccount.nextReviewDate)}
                  </div>
                </div>
                <Button
                  type="button"
                  className="w-full"
                  onClick={() => openReview(selectedAccount)}
                >
                  Apply rent review
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <CalendarClock className="h-5 w-5 text-primary" />
                  Billing rules
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <div>
                  <div className="text-muted-foreground">Calculation</div>
                  <div className="font-medium">
                    {selectedAccount.calculationMethod}
                  </div>
                </div>
                <div>
                  <div className="text-muted-foreground">Payment terms</div>
                  <div className="font-medium">
                    {selectedAccount.paymentTermsDays} days
                  </div>
                </div>
                <div>
                  <div className="text-muted-foreground">Penalty</div>
                  <div className="font-medium">
                    {selectedAccount.penaltyMethod === 'None'
                      ? 'None'
                      : `${selectedAccount.penaltyMethod} · ${selectedAccount.penaltyValue} after ${selectedAccount.gracePeriodDays} days`}
                  </div>
                </div>
                <div>
                  <div className="text-muted-foreground">GL income account</div>
                  <div className="font-medium">
                    {selectedAccount.groundRentIncomeAccount}
                  </div>
                </div>
                <Badge variant="outline">
                  {selectedAccount.autoPostInvoices
                    ? 'Invoices auto-post'
                    : 'Manual Finance posting'}
                </Badge>
              </CardContent>
            </Card>
          </div>
        </div>
      ) : null}

      <Dialog open={setupOpen} onOpenChange={setSetupOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Ground rent schedule</DialogTitle>
            <DialogDescription>
              Define the Estate billing rule. Finance AR remains the source for
              invoices, receipts, allocations, balances, and journal postings.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-6 py-2 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Property / unit</Label>
              <Select
                value={setupForm.estateManagedAssetId}
                onValueChange={selectAsset}
              >
                <SelectTrigger aria-label="Ground rent property or unit">
                  <SelectValue placeholder="Select a property with a linked customer" />
                </SelectTrigger>
                <SelectContent>
                  {options.assets
                    .filter((asset) => asset.customerBusinessPartnerId)
                    .map((asset) => (
                    <SelectItem key={asset.id} value={asset.id}>
                      {asset.assetCode} · {asset.name}
                      {asset.customerName ? ` · ${asset.customerName}` : ''}
                    </SelectItem>
                    ))}
                </SelectContent>
              </Select>
              {selectedAsset ? (
                <div className="grid gap-2 rounded-md border bg-muted/20 p-3 text-sm sm:grid-cols-3">
                  <div>
                    <span className="text-muted-foreground">Customer:</span>{' '}
                    {selectedAsset.customerName || 'Not linked'}
                  </div>
                  <div>
                    <span className="text-muted-foreground">
                      Approved annual:
                    </span>{' '}
                    {selectedAsset.approvedAnnualGroundRent == null
                      ? 'Not assessed'
                      : formatMoney(
                          selectedAsset.approvedAnnualGroundRent,
                          selectedAsset.currencyCode
                        )}
                  </div>
                  <div>
                    <span className="text-muted-foreground">Area:</span>{' '}
                    {selectedAsset.areaAcres == null
                      ? 'Not recorded'
                      : `${selectedAsset.areaAcres.toFixed(4)} acres`}
                  </div>
                </div>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label>Payment frequency</Label>
              <Select
                value={setupForm.paymentFrequency}
                onValueChange={(value) =>
                  setSetupForm((current) => ({
                    ...current,
                    paymentFrequency: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent payment frequency">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Annual">Annual</SelectItem>
                  <SelectItem value="SemiAnnual">Semi-annual</SelectItem>
                  <SelectItem value="Quarterly">Quarterly</SelectItem>
                  <SelectItem value="Monthly">Monthly</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Calculation method</Label>
              <Select
                value={setupForm.calculationMethod}
                onValueChange={(value) =>
                  setSetupForm((current) => ({
                    ...current,
                    calculationMethod: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent calculation method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ApprovedAssessment">
                    Approved Estate assessment
                  </SelectItem>
                  <SelectItem value="FixedAnnualAmount">
                    Fixed annual amount
                  </SelectItem>
                  <SelectItem value="RatePerAcre">Rate per acre</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {setupForm.calculationMethod === 'FixedAnnualAmount' ? (
              <div className="space-y-2">
                <Label htmlFor="ground-rent-annual">Annual amount</Label>
                <Input
                  id="ground-rent-annual"
                  type="number"
                  min={0.01}
                  step={0.01}
                  value={setupForm.annualAmount ?? ''}
                  onChange={(event) =>
                    setSetupForm((current) => ({
                      ...current,
                      annualAmount:
                        event.target.value === ''
                          ? null
                          : Number(event.target.value),
                    }))
                  }
                />
              </div>
            ) : null}

            {setupForm.calculationMethod === 'RatePerAcre' ? (
              <div className="space-y-2">
                <Label htmlFor="ground-rent-rate">Rate per acre</Label>
                <Input
                  id="ground-rent-rate"
                  type="number"
                  min={0.01}
                  step={0.01}
                  value={setupForm.ratePerAcre ?? ''}
                  onChange={(event) =>
                    setSetupForm((current) => ({
                      ...current,
                      ratePerAcre:
                        event.target.value === ''
                          ? null
                          : Number(event.target.value),
                    }))
                  }
                />
              </div>
            ) : null}

            <div className="space-y-2">
              <Label htmlFor="ground-rent-next-due">Next due date</Label>
              <Input
                id="ground-rent-next-due"
                type="date"
                value={setupForm.nextDueDate}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    nextDueDate: event.target.value,
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-terms">Payment terms (days)</Label>
              <Input
                id="ground-rent-terms"
                type="number"
                min={0}
                max={365}
                value={setupForm.paymentTermsDays}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    paymentTermsDays: Number(event.target.value),
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label>Ground-rent income account</Label>
              <Select
                value={setupForm.groundRentIncomeAccountId}
                onValueChange={(value) =>
                  setSetupForm((current) => ({
                    ...current,
                    groundRentIncomeAccountId: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent income account">
                  <SelectValue placeholder="Select an active revenue account" />
                </SelectTrigger>
                <SelectContent>
                  {options.incomeAccounts.map((account) => (
                    <SelectItem key={account.id} value={account.id}>
                      {account.accountNumber} · {account.accountName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Status</Label>
              <Select
                value={setupForm.status}
                onValueChange={(value) =>
                  setSetupForm((current) => ({
                    ...current,
                    status: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent account status">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Held">Held</SelectItem>
                  <SelectItem value="Closed">Closed</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-review-frequency">
                Review every (months)
              </Label>
              <Input
                id="ground-rent-review-frequency"
                type="number"
                min={1}
                max={120}
                value={setupForm.reviewFrequencyMonths}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    reviewFrequencyMonths: Number(event.target.value),
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-next-review">Next review date</Label>
              <Input
                id="ground-rent-next-review"
                type="date"
                value={setupForm.nextReviewDate || ''}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    nextReviewDate: event.target.value,
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label>Escalation method</Label>
              <Select
                value={setupForm.escalationMethod}
                onValueChange={(value) =>
                  setSetupForm((current) => ({
                    ...current,
                    escalationMethod: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent escalation method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="None">No escalation</SelectItem>
                  <SelectItem value="Percentage">Percentage</SelectItem>
                  <SelectItem value="FixedAmount">Fixed amount</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-escalation-value">
                Escalation value
              </Label>
              <Input
                id="ground-rent-escalation-value"
                type="number"
                min={0}
                step={0.01}
                disabled={setupForm.escalationMethod === 'None'}
                value={setupForm.escalationValue}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    escalationValue: Number(event.target.value),
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label>Arrears penalty</Label>
              <Select
                value={setupForm.penaltyMethod}
                onValueChange={(value) =>
                  setSetupForm((current) => ({
                    ...current,
                    penaltyMethod: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent penalty method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="None">No penalty</SelectItem>
                  <SelectItem value="PercentageOfOutstanding">
                    Percentage of outstanding
                  </SelectItem>
                  <SelectItem value="FixedAmount">Fixed amount</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-penalty-value">Penalty value</Label>
              <Input
                id="ground-rent-penalty-value"
                type="number"
                min={0}
                step={0.01}
                disabled={setupForm.penaltyMethod === 'None'}
                value={setupForm.penaltyValue}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    penaltyValue: Number(event.target.value),
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-grace">Grace period (days)</Label>
              <Input
                id="ground-rent-grace"
                type="number"
                min={0}
                max={365}
                value={setupForm.gracePeriodDays}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    gracePeriodDays: Number(event.target.value),
                  }))
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ground-rent-penalty-cap">
                Penalty cap (optional)
              </Label>
              <Input
                id="ground-rent-penalty-cap"
                type="number"
                min={0}
                step={0.01}
                value={setupForm.penaltyCapAmount ?? ''}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    penaltyCapAmount:
                      event.target.value === ''
                        ? null
                        : Number(event.target.value),
                  }))
                }
              />
            </div>

            <div className="flex items-center gap-3 rounded-md border p-3 md:col-span-2">
              <input
                id="ground-rent-auto-post"
                type="checkbox"
                className="h-4 w-4"
                checked={setupForm.autoPostInvoices}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    autoPostInvoices: event.target.checked,
                  }))
                }
              />
              <div>
                <Label htmlFor="ground-rent-auto-post">
                  Automatically post generated invoices to Finance GL
                </Label>
                <p className="text-xs text-muted-foreground">
                  Leave disabled when Finance must review draft invoices before
                  posting.
                </p>
              </div>
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="ground-rent-notes">Notes</Label>
              <Textarea
                id="ground-rent-notes"
                maxLength={1000}
                value={setupForm.notes || ''}
                onChange={(event) =>
                  setSetupForm((current) => ({
                    ...current,
                    notes: event.target.value,
                  }))
                }
              />
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setSetupOpen(false)}
            >
              Cancel
            </Button>
            <Button
              type="button"
              disabled={actionKey === 'save-account'}
              onClick={() => void saveSetup()}
            >
              {actionKey === 'save-account' ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              Save schedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(reviewAccount)}
        onOpenChange={(open) => !open && setReviewAccount(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Apply ground-rent review</DialogTitle>
            <DialogDescription>
              Create an auditable review and update the annual ground rent for{' '}
              {reviewAccount?.assetCode}.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-2">
              <Label>Escalation method</Label>
              <Select value={reviewMethod} onValueChange={setReviewMethod}>
                <SelectTrigger aria-label="Review escalation method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Percentage">Percentage</SelectItem>
                  <SelectItem value="FixedAmount">Fixed amount</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="review-value">Escalation value</Label>
              <Input
                id="review-value"
                type="number"
                min={0.01}
                step={0.01}
                value={reviewValue}
                onChange={(event) => setReviewValue(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="review-effective-date">Effective date</Label>
              <Input
                id="review-effective-date"
                type="date"
                value={reviewDate}
                onChange={(event) => setReviewDate(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="review-notes">Approval / review note</Label>
              <Textarea
                id="review-notes"
                value={reviewNotes}
                onChange={(event) => setReviewNotes(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setReviewAccount(null)}
            >
              Cancel
            </Button>
            <Button type="button" onClick={() => void applyReview()}>
              Apply review
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(receiptDialog)}
        onOpenChange={(open) => !open && setReceiptDialog(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record Finance AR receipt</DialogTitle>
            <DialogDescription>
              The receipt will be posted and allocated to the selected{' '}
              {receiptDialog?.target.toLowerCase()} invoice.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="receipt-date">Payment date</Label>
              <Input
                id="receipt-date"
                type="date"
                value={receiptForm.paymentDate}
                onChange={(event) =>
                  setReceiptForm((current) => ({
                    ...current,
                    paymentDate: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="receipt-amount">Amount</Label>
              <Input
                id="receipt-amount"
                type="number"
                min={0.01}
                step={0.01}
                value={receiptForm.amount}
                onChange={(event) =>
                  setReceiptForm((current) => ({
                    ...current,
                    amount: Number(event.target.value),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Payment method</Label>
              <Select
                value={receiptForm.paymentMethod}
                onValueChange={(value) =>
                  setReceiptForm((current) => ({
                    ...current,
                    paymentMethod: value,
                  }))
                }
              >
                <SelectTrigger aria-label="Ground rent receipt payment method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Cash">Cash</SelectItem>
                  <SelectItem value="Bank Transfer">Bank transfer</SelectItem>
                  <SelectItem value="Cheque">Cheque</SelectItem>
                  <SelectItem value="Mobile Money">Mobile money</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="receipt-reference">
                Transaction / receipt reference
              </Label>
              <Input
                id="receipt-reference"
                value={receiptForm.transactionReference || ''}
                onChange={(event) =>
                  setReceiptForm((current) => ({
                    ...current,
                    transactionReference: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="receipt-notes">Notes</Label>
              <Textarea
                id="receipt-notes"
                value={receiptForm.notes || ''}
                onChange={(event) =>
                  setReceiptForm((current) => ({
                    ...current,
                    notes: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setReceiptDialog(null)}
            >
              Cancel
            </Button>
            <Button type="button" onClick={() => void recordReceipt()}>
              Record and allocate receipt
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
