'use client';

import React, { useMemo, useState } from 'react';
import Link from 'next/link';
import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Download,
  ExternalLink,
  Landmark,
  Loader2,
  Plus,
  Scale,
  ShieldCheck,
  Trash2,
  Upload,
  Warehouse,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import { Textarea } from '@/components/ui/textarea';
import { SearchableOptionPicker } from '@/components/finance/accounting-books/searchable-option-picker';
import type {
  CreateBankAccountOpeningBalanceDto,
  CreateResidualGlEquityOpeningBalanceDto,
  GovernedOpeningBalanceOptions,
} from '@/types/finance';
import {
  getBankOpeningBlockers,
  type CreateOpeningStockAdjustmentDto,
  type GovernedInventoryOpeningResult,
  type OpeningStockOptions,
} from '@/lib/finance/opening-balance-governance';
import {
  buildInventoryOpeningTemplateCsv,
  parseInventoryOpeningImportFile,
} from '@/lib/finance/inventory-opening-import';

type GovernedBusyAction = 'bank' | 'inventory' | 'residual' | null;

interface GovernedOpeningSourcesProps {
  openingDate: string;
  openingDateMin?: string;
  openingDateMax?: string;
  fiscalPeriodId: string;
  fiscalPeriodCode?: string;
  bookClassification: string;
  periodOptions: Array<{
    id: string;
    periodCode: string;
    periodName: string;
  }>;
  bookOptions: Array<{
    code: string;
    name: string;
  }>;
  headerLocked?: boolean;
  financeOptions?: GovernedOpeningBalanceOptions;
  openingStockOptions?: OpeningStockOptions;
  financeOptionsLoading?: boolean;
  openingStockOptionsLoading?: boolean;
  canPrepareFinance: boolean;
  canPrepareInventory: boolean;
  busyAction: GovernedBusyAction;
  onPrepareBank: (dto: CreateBankAccountOpeningBalanceDto) => Promise<void>;
  onPrepareResidual: (
    dto: CreateResidualGlEquityOpeningBalanceDto
  ) => Promise<void>;
  onPrepareInventory: (
    dto: CreateOpeningStockAdjustmentDto
  ) => Promise<GovernedInventoryOpeningResult>;
  onOpeningDateChange: (value: string) => void;
  onFiscalPeriodChange: (value: string) => void;
  onBookClassificationChange: (value: string) => void;
}

type InventoryLineDraft = {
  key: string;
  inventoryItemId: string;
  locationId: string;
  quantity: string;
  unitCost: string;
  serialNumber: string;
  lotNumber: string;
  batchNumber: string;
  manufactureDate: string;
  expiryDate: string;
  notes: string;
};

const INVENTORY_LINES_PER_PAGE = 50;

function newInventoryLine(): InventoryLineDraft {
  return {
    key: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
    inventoryItemId: '',
    locationId: '',
    quantity: '',
    unitCost: '',
    serialNumber: '',
    lotNumber: '',
    batchNumber: '',
    manufactureDate: '',
    expiryDate: '',
    notes: '',
  };
}

function positiveNumber(value: string) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? number : 0;
}

function ReadOnlyPostingLine({
  direction,
  account,
  amount,
  currencyCode,
}: {
  direction: string;
  account: string;
  amount?: number;
  currencyCode?: string;
}) {
  return (
    <div className="grid min-w-0 grid-cols-[72px_minmax(0,1fr)_auto] items-center gap-3 rounded-md border bg-muted/30 px-3 py-2 text-sm">
      <Badge variant="outline" className="justify-center">
        {direction}
      </Badge>
      <span className="min-w-0 break-words font-medium">{account}</span>
      {amount !== undefined && (
        <span className="tabular-nums">
          {currencyCode
            ? new Intl.NumberFormat(undefined, {
                style: 'currency',
                currency: currencyCode,
                currencyDisplay: 'code',
              }).format(amount)
            : amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
        </span>
      )}
    </div>
  );
}

function BlockerList({
  title,
  blockers,
}: {
  title: string;
  blockers: string[];
}) {
  if (blockers.length === 0) return null;
  return (
    <Alert variant="destructive">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <ul className="mt-1 list-disc space-y-1 pl-5">
          {blockers.map((blocker) => (
            <li key={blocker}>{blocker}</li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  );
}

export function GovernedOpeningSources({
  openingDate,
  openingDateMin,
  openingDateMax,
  fiscalPeriodId,
  fiscalPeriodCode,
  bookClassification,
  periodOptions,
  bookOptions,
  headerLocked = false,
  financeOptions,
  openingStockOptions,
  financeOptionsLoading = false,
  openingStockOptionsLoading = false,
  canPrepareFinance,
  canPrepareInventory,
  busyAction,
  onPrepareBank,
  onPrepareResidual,
  onPrepareInventory,
  onOpeningDateChange,
  onFiscalPeriodChange,
  onBookClassificationChange,
}: GovernedOpeningSourcesProps) {
  const [bankAccountId, setBankAccountId] = useState('');
  const [bankAmount, setBankAmount] = useState('');
  const [bankSourceReference, setBankSourceReference] = useState('');
  const [bankDescription, setBankDescription] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [inventorySourceReference, setInventorySourceReference] = useState('');
  const [inventoryDescription, setInventoryDescription] = useState('');
  const [inventoryLines, setInventoryLines] = useState<InventoryLineDraft[]>([
    newInventoryLine(),
  ]);
  const [inventoryPage, setInventoryPage] = useState(0);
  const [inventoryImportErrors, setInventoryImportErrors] = useState<string[]>([]);
  const [inventoryImportFileName, setInventoryImportFileName] = useState('');
  const [inventoryResult, setInventoryResult] =
    useState<GovernedInventoryOpeningResult | null>(null);
  const [accruedExpensesAccountId, setAccruedExpensesAccountId] = useState('');
  const [accruedExpensesAmount, setAccruedExpensesAmount] = useState('');
  const [shareCapitalAccountId, setShareCapitalAccountId] = useState('');
  const [shareCapitalAmount, setShareCapitalAmount] = useState('');
  const [retainedEarningsAmount, setRetainedEarningsAmount] = useState('');
  const [residualSourceReference, setResidualSourceReference] = useState('');
  const [residualDescription, setResidualDescription] = useState('');

  const headerReady = Boolean(
    openingDate && fiscalPeriodId && bookClassification
  );
  // Inventory makers intentionally have Finance.Read rather than the broader Finance opening-
  // balance preparation permission. They still need to select the dated accounting context for
  // Inventory-owned INITIAL_STOCK evidence, while the server remains authoritative for period,
  // book, source, workflow and posting validation.
  const headerControlsDisabled =
    headerLocked ||
    busyAction !== null ||
    (!canPrepareFinance && !canPrepareInventory);
  const selectedBank = financeOptions?.bankAccounts.find(
    (option) => option.id === bankAccountId
  );
  const bankNativeAmount = positiveNumber(bankAmount);
  const bankFunctionalAmount =
    bankNativeAmount * (selectedBank?.exchangeRate ?? 1);
  const selectedWarehouse = openingStockOptions?.warehouses.find(
    (option) => option.id === warehouseId
  );
  const inventoryTotal = useMemo(
    () =>
      inventoryLines.reduce(
        (sum, line) =>
          sum + positiveNumber(line.quantity) * positiveNumber(line.unitCost),
        0
      ),
    [inventoryLines]
  );
  const inventoryPageCount = Math.max(1, Math.ceil(inventoryLines.length / INVENTORY_LINES_PER_PAGE));
  const visibleInventoryLines = inventoryLines.slice(
    inventoryPage * INVENTORY_LINES_PER_PAGE,
    (inventoryPage + 1) * INVENTORY_LINES_PER_PAGE
  );
  const residualTotal =
    positiveNumber(accruedExpensesAmount) +
    positiveNumber(shareCapitalAmount) +
    positiveNumber(retainedEarningsAmount);

  const financeBlockers = financeOptions?.blockers ?? [];
  // Retained Earnings is irrelevant to a bank opening. Gate this source only on its canonical
  // bank master and the Migration Clearing account that the bank endpoint actually derives.
  const bankBlockers = getBankOpeningBlockers(
    financeOptions,
    bankAccountId,
    financeOptionsLoading
  );
  if (
    !financeOptionsLoading &&
    financeOptions &&
    financeOptions.bankAccounts.length === 0
  ) {
    bankBlockers.push(
      'No tenant bank account is available for governed opening preparation.'
    );
  }
  const residualBlockers = [
    ...financeBlockers,
    ...(financeOptions?.migrationClearingAccount?.blockers ?? []),
    ...(financeOptions?.retainedEarningsAccount?.blockers ?? []),
    ...(!financeOptionsLoading &&
    financeOptions &&
    financeOptions.accruedExpensesAccounts.length === 0
      ? ['No eligible accrued-expenses account is available.']
      : []),
    ...(!financeOptionsLoading &&
    financeOptions &&
    financeOptions.shareCapitalAccounts.length === 0
      ? ['No eligible share-capital account is available.']
      : []),
  ];
  const inventoryBlockers = openingStockOptions?.blockers ?? [];
  const validInventoryLines = inventoryLines.every((line) => {
    const item = openingStockOptions?.items.find(
      (option) => option.id === line.inventoryItemId
    );
    return (
      Boolean(item && line.locationId) &&
      positiveNumber(line.quantity) > 0 &&
      positiveNumber(line.unitCost) > 0 &&
      (!item?.isSerialTracked || Boolean(line.serialNumber.trim())) &&
      (!item?.isLotTracked || Boolean(line.lotNumber.trim())) &&
      (!item?.isBatchTracked || Boolean(line.batchNumber.trim()))
    );
  });

  const updateInventoryLine = (
    key: string,
    patch: Partial<InventoryLineDraft>
  ) => {
    setInventoryLines((current) =>
      current.map((line) => (line.key === key ? { ...line, ...patch } : line))
    );
  };

  const downloadInventoryTemplate = () => {
    const firstItemCode = openingStockOptions?.items[0]?.itemCode ?? 'ITEM-001';
    const firstLocationCode = selectedWarehouse?.locations[0]?.code ?? 'MAIN';
    const url = URL.createObjectURL(new Blob([
      buildInventoryOpeningTemplateCsv(firstItemCode, firstLocationCode),
    ], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'inventory-opening-template.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const importInventorySchedule = async (file?: File) => {
    if (!file || !selectedWarehouse) return;
    const result = await parseInventoryOpeningImportFile(
      file,
      openingStockOptions?.items ?? [],
      selectedWarehouse
    );
    setInventoryImportFileName(file.name);
    setInventoryImportErrors(result.errors);
    if (result.errors.length > 0) return;
    setInventoryLines(result.rows.map((row) => ({ ...newInventoryLine(), ...row })));
    if (!inventorySourceReference.trim()) {
      setInventorySourceReference(file.name.replace(/\.(csv|xlsx)$/i, '').slice(0, 50));
    }
    setInventoryPage(0);
  };

  const downloadInventoryImportErrors = () => {
    const content = ['error', ...inventoryImportErrors]
      .map((value) => `"${value.replace(/"/g, '""')}"`).join('\r\n');
    const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${inventoryImportFileName.replace(/\.(csv|xlsx)$/i, '') || 'inventory-opening'}-errors.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const prepareInventory = async () => {
    if (!selectedWarehouse) return;
    try {
      const result = await onPrepareInventory({
        warehouseId: selectedWarehouse.id,
        openingDate,
        bookClassification,
        sourceScheduleReference: inventorySourceReference.trim(),
        description: inventoryDescription.trim(),
        items: inventoryLines.map((line) => ({
          inventoryItemId: line.inventoryItemId,
          locationId: line.locationId,
          quantity: positiveNumber(line.quantity),
          unitCost: positiveNumber(line.unitCost),
          serialNumber: line.serialNumber.trim() || undefined,
          lotNumber: line.lotNumber.trim() || undefined,
          batchNumber: line.batchNumber.trim() || undefined,
          manufactureDate: line.manufactureDate || undefined,
          expiryDate: line.expiryDate || undefined,
          notes: line.notes.trim() || undefined,
        })),
      });
      setInventoryResult(result);
    } catch {
      // The page owns the destructive toast. Preserve the schedule so the operator can correct it.
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5" />
            Governed opening sources
          </CardTitle>
          <CardDescription>
            Select only tenant-scoped server options. The server derives
            protected accounts and posting directions; generated evidence is
            immutable.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-2">
            <Label htmlFor="governed-opening-date">Opening date</Label>
            <Input
              id="governed-opening-date"
              aria-label="Governed opening date"
              type="date"
              value={openingDate}
              min={openingDateMin}
              max={openingDateMax}
              onChange={(event) => onOpeningDateChange(event.target.value)}
              disabled={headerControlsDisabled}
            />
          </div>
          <div className="space-y-2">
            <Label>Fiscal period</Label>
            <Select
              value={fiscalPeriodId}
              onValueChange={onFiscalPeriodChange}
              disabled={headerControlsDisabled}
            >
              <SelectTrigger aria-label="Governed fiscal period">
                <SelectValue placeholder="Select period" />
              </SelectTrigger>
              <SelectContent>
                {periodOptions.map((period) => (
                  <SelectItem key={period.id} value={period.id}>
                    {period.periodCode} - {period.periodName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Book</Label>
            <Select
              value={bookClassification}
              onValueChange={onBookClassificationChange}
              disabled={headerControlsDisabled}
            >
              <SelectTrigger aria-label="Governed accounting book">
                <SelectValue placeholder="Select book" />
              </SelectTrigger>
              <SelectContent>
                {bookOptions.map((book) => (
                  <SelectItem key={book.code} value={book.code}>
                    {book.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {headerLocked && (
            <p className="text-xs text-muted-foreground sm:col-span-3">
              The governed header is locked while a saved GL batch is open.
            </p>
          )}
        </CardContent>
      </Card>

      {!headerReady && (
        <BlockerList
          title="Opening header required"
          blockers={[
            'Select an opening date, fiscal period and explicit accounting book above.',
          ]}
        />
      )}

      <div className="grid gap-6 xl:grid-cols-3">
        <Card className="min-w-0">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <Landmark className="h-5 w-5" />
              1. Bank opening
            </CardTitle>
            <CardDescription>
              Finance derives the mapped bank GL debit and Migration Clearing
              credit.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <BlockerList
              title="Bank opening is not ready"
              blockers={bankBlockers}
            />
            <div className="space-y-2">
              <Label>Bank account</Label>
              <Select
                value={bankAccountId}
                onValueChange={setBankAccountId}
                disabled={
                  !canPrepareFinance ||
                  financeOptionsLoading ||
                  busyAction !== null
                }
              >
                <SelectTrigger aria-label="Bank account">
                  <SelectValue
                    placeholder={
                      financeOptionsLoading
                        ? 'Loading server options...'
                        : 'Select eligible bank'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {(financeOptions?.bankAccounts ?? []).map((option) => (
                    <SelectItem
                      key={option.id}
                      value={option.id}
                      disabled={!option.isEligible}
                    >
                      {option.accountName} · {option.accountNumber} (
                      {option.currencyCode})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="bank-opening-amount">
                Opening amount
                {selectedBank ? ` (${selectedBank.currencyCode})` : ''}
              </Label>
              <Input
                id="bank-opening-amount"
                aria-label="Opening amount"
                type="number"
                min="0.01"
                step="0.01"
                value={bankAmount}
                onChange={(event) => setBankAmount(event.target.value)}
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="bank-source-reference">
                Source schedule reference
              </Label>
              <Input
                id="bank-source-reference"
                value={bankSourceReference}
                onChange={(event) => setBankSourceReference(event.target.value)}
                placeholder="Approved bank opening schedule"
                maxLength={50}
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="bank-description">Description</Label>
              <Textarea
                id="bank-description"
                value={bankDescription}
                onChange={(event) => setBankDescription(event.target.value)}
                rows={2}
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            {selectedBank && (
              <div className="space-y-2" aria-label="Derived bank posting">
                <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  Read-only derived posting
                </p>
                <ReadOnlyPostingLine
                  direction={selectedBank.postingDirection}
                  account={`${selectedBank.glAccountCode ?? 'Mapped GL'} — ${selectedBank.glAccountName ?? selectedBank.accountName}`}
                  amount={bankNativeAmount}
                  currencyCode={selectedBank.currencyCode}
                />
                <ReadOnlyPostingLine
                  direction="Credit"
                  account={`${financeOptions?.migrationClearingAccount?.accountCode ?? 'Configured'} — ${financeOptions?.migrationClearingAccount?.accountName ?? 'Migration Clearing'}`}
                  amount={bankFunctionalAmount}
                  currencyCode={financeOptions?.functionalCurrencyCode}
                />
                {selectedBank.exchangeRateId && (
                  <p className="text-xs text-muted-foreground">
                    Approved {selectedBank.exchangeRateType}{' '}
                    {selectedBank.exchangeRateQuoteSide} rate:{' '}
                    {selectedBank.exchangeRate.toLocaleString(undefined, {
                      maximumFractionDigits: 6,
                    })}{' '}
                    on {selectedBank.exchangeRateDate?.slice(0, 10)}
                    {selectedBank.exchangeRateSource
                      ? ` · ${selectedBank.exchangeRateSource}`
                      : ''}
                  </p>
                )}
              </div>
            )}
            <Button
              className="w-full"
              disabled={
                !canPrepareFinance ||
                !headerReady ||
                busyAction !== null ||
                !selectedBank?.isEligible ||
                bankBlockers.length > 0 ||
                !bankSourceReference.trim() ||
                positiveNumber(bankAmount) <= 0
              }
              onClick={() =>
                onPrepareBank({
                  sourceReference: bankSourceReference.trim(),
                  description: bankDescription.trim() || undefined,
                  openingDate,
                  fiscalPeriodId,
                  bookClassification,
                  bankAccountId,
                  amount: bankNativeAmount,
                  exchangeRateId: selectedBank?.exchangeRateId,
                })
              }
            >
              {busyAction === 'bank' && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Prepare immutable bank batch
            </Button>
          </CardContent>
        </Card>

        <Card className="min-w-0">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <Warehouse className="h-5 w-5" />
              2. Inventory opening evidence
            </CardTitle>
            <CardDescription>
              Inventory owns its masters, item/location evidence and workflow.
              This Finance workspace consumes only Inventory&apos;s typed
              readiness/create contract; Finance revalidates the derived posting
              when the approved adjustment posts.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {!canPrepareInventory && (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>
                  Inventory preparation permission required
                </AlertTitle>
                <AlertDescription>
                  An authorised Inventory adjustment maker must prepare this
                  source.
                </AlertDescription>
              </Alert>
            )}
            <BlockerList
              title="Inventory opening is not ready"
              blockers={inventoryBlockers}
            />
            {inventoryResult ? (
              <div className="space-y-4 rounded-md border bg-muted/20 p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-medium">
                    {inventoryResult.adjustmentNumber}
                  </span>
                  <Badge>{inventoryResult.status}</Badge>
                </div>
                <p className="text-sm text-muted-foreground">
                  The opening-stock evidence is server generated and immutable.
                  Continue its submit, independent decision and post lifecycle
                  in Inventory.
                </p>
                <Button variant="outline" asChild className="w-full">
                  <Link href="/inventory/adjustments">
                    Open Inventory adjustments{' '}
                    <ExternalLink className="ml-2 h-4 w-4" />
                  </Link>
                </Button>
              </div>
            ) : (
              <>
                <div className="space-y-2">
                  <Label>Warehouse</Label>
                  <Select
                    value={warehouseId}
                    onValueChange={(value) => {
                      setWarehouseId(value);
                      setInventoryImportErrors([]);
                      setInventoryImportFileName('');
                      setInventoryPage(0);
                      setInventoryLines((current) =>
                        current.map((line) => ({ ...line, locationId: '' }))
                      );
                    }}
                    disabled={
                      !canPrepareInventory ||
                      openingStockOptionsLoading ||
                      busyAction !== null
                    }
                  >
                    <SelectTrigger aria-label="Inventory opening warehouse">
                      <SelectValue
                        placeholder={
                          openingStockOptionsLoading
                            ? 'Loading server options...'
                            : 'Select eligible warehouse'
                        }
                      />
                    </SelectTrigger>
                    <SelectContent>
                      {(openingStockOptions?.warehouses ?? []).map((option) => (
                        <SelectItem key={option.id} value={option.id}>
                          {option.code} — {option.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="inventory-source-reference">
                    Source schedule reference
                  </Label>
                  <Input
                    id="inventory-source-reference"
                    value={inventorySourceReference}
                    onChange={(event) =>
                      setInventorySourceReference(event.target.value)
                    }
                    maxLength={50}
                    disabled={!canPrepareInventory || busyAction !== null}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="inventory-description">Description</Label>
                  <Textarea
                    id="inventory-description"
                    value={inventoryDescription}
                    onChange={(event) =>
                      setInventoryDescription(event.target.value)
                    }
                    rows={2}
                    maxLength={1000}
                    disabled={!canPrepareInventory || busyAction !== null}
                  />
                </div>
                <div className="space-y-3">
                  <div className="space-y-3 rounded-md border bg-muted/20 p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div>
                        <p className="text-sm font-medium">Bulk schedule import</p>
                        <p className="text-xs text-muted-foreground">Preflight CSV or XLSX rows against the selected warehouse and current eligible item catalog before replacing the draft.</p>
                      </div>
                      <Button type="button" variant="outline" size="sm" onClick={downloadInventoryTemplate} disabled={!selectedWarehouse}>
                        <Download className="mr-2 h-4 w-4" />Download template
                      </Button>
                    </div>
                    <Label htmlFor="inventory-opening-import" className="flex cursor-pointer items-center justify-center rounded-md border border-dashed px-3 py-4 text-sm font-medium hover:bg-muted">
                      <Upload className="mr-2 h-4 w-4" />Choose CSV or XLSX schedule
                    </Label>
                    <Input
                      id="inventory-opening-import"
                      aria-label="Import inventory opening schedule"
                      type="file"
                      accept=".csv,.xlsx"
                      className="sr-only"
                      disabled={!canPrepareInventory || !selectedWarehouse || busyAction !== null}
                      onChange={(event) => void importInventorySchedule(event.target.files?.[0])}
                    />
                    {inventoryImportFileName && (
                      <p className="text-xs text-muted-foreground">Preflight file: {inventoryImportFileName}</p>
                    )}
                    {inventoryImportErrors.length > 0 && (
                      <Alert variant="destructive">
                        <AlertCircle className="h-4 w-4" />
                        <AlertTitle>{inventoryImportErrors.length} import error{inventoryImportErrors.length === 1 ? '' : 's'} — draft not replaced</AlertTitle>
                        <AlertDescription>
                          <ul className="mt-1 max-h-48 list-disc space-y-1 overflow-auto pl-5">
                            {inventoryImportErrors.slice(0, 50).map((error, index) => <li key={`${index}-${error}`}>{error}</li>)}
                          </ul>
                          {inventoryImportErrors.length > 50 && <p className="mt-2">Showing the first 50 errors. Correct the source file and run preflight again.</p>}
                          <Button type="button" variant="outline" size="sm" className="mt-3" onClick={downloadInventoryImportErrors}>
                            <Download className="mr-2 h-4 w-4" />Download all errors
                          </Button>
                        </AlertDescription>
                      </Alert>
                    )}
                    {inventoryImportFileName && inventoryImportErrors.length === 0 && inventoryLines.length > 0 && (
                      <Alert>
                        <ShieldCheck className="h-4 w-4" />
                        <AlertTitle>Preflight passed</AlertTitle>
                        <AlertDescription>{inventoryLines.length.toLocaleString()} rows loaded. Review the paged preview and totals before preparation.</AlertDescription>
                      </Alert>
                    )}
                  </div>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="text-sm text-muted-foreground">
                      {inventoryLines.length.toLocaleString()} schedule line{inventoryLines.length === 1 ? '' : 's'} · showing at most {INVENTORY_LINES_PER_PAGE} per page
                    </p>
                    {inventoryPageCount > 1 && (
                      <div className="flex items-center gap-2">
                        <Button type="button" variant="outline" size="sm" aria-label="Previous inventory rows" disabled={inventoryPage === 0} onClick={() => setInventoryPage((page) => Math.max(0, page - 1))}><ChevronLeft className="h-4 w-4" /></Button>
                        <span className="text-sm">Page {inventoryPage + 1} of {inventoryPageCount}</span>
                        <Button type="button" variant="outline" size="sm" aria-label="Next inventory rows" disabled={inventoryPage >= inventoryPageCount - 1} onClick={() => setInventoryPage((page) => Math.min(inventoryPageCount - 1, page + 1))}><ChevronRight className="h-4 w-4" /></Button>
                      </div>
                    )}
                  </div>
                  {visibleInventoryLines.map((line, pageIndex) => {
                    const index = inventoryPage * INVENTORY_LINES_PER_PAGE + pageIndex;
                    const item = openingStockOptions?.items.find(
                      (option) => option.id === line.inventoryItemId
                    );
                    return (
                      <div
                        key={line.key}
                        className="space-y-3 rounded-md border p-3"
                      >
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-medium">
                            Schedule line {index + 1}
                          </span>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove inventory line ${index + 1}`}
                            disabled={
                              inventoryLines.length === 1 || busyAction !== null
                            }
                            onClick={() =>
                              setInventoryLines((current) =>
                                current.filter(
                                  (candidate) => candidate.key !== line.key
                                )
                              )
                            }
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                        <SearchableOptionPicker
                          label={`Inventory item ${index + 1}`}
                          value={line.inventoryItemId}
                          onChange={(value) =>
                            updateInventoryLine(line.key, {
                              inventoryItemId: value,
                              serialNumber: '',
                              lotNumber: '',
                              batchNumber: '',
                            })
                          }
                          options={(openingStockOptions?.items ?? []).map((option) => ({ value: option.id, label: `${option.itemCode} — ${option.name} (${option.unitOfMeasure})` }))}
                          placeholder="Select server item"
                          searchPlaceholder="Search item code or name..."
                          emptyMessage="No eligible stock item found."
                          disabled={!canPrepareInventory || busyAction !== null}
                        />
                        <SearchableOptionPicker
                          label={`Inventory location ${index + 1}`}
                          value={line.locationId}
                          onChange={(value) =>
                            updateInventoryLine(line.key, { locationId: value })
                          }
                          options={(selectedWarehouse?.locations ?? []).map((option) => ({ value: option.id, label: `${option.code} — ${option.name}` }))}
                          placeholder="Select warehouse location"
                          searchPlaceholder="Search location code or name..."
                          emptyMessage="No eligible warehouse location found."
                          disabled={
                            !canPrepareInventory ||
                            !selectedWarehouse ||
                            busyAction !== null
                          }
                        />
                        <div className="grid grid-cols-2 gap-3">
                          <div className="space-y-1">
                            <Label htmlFor={`inventory-quantity-${line.key}`}>
                              Quantity
                            </Label>
                            <Input
                              id={`inventory-quantity-${line.key}`}
                              type="number"
                              min="0.0001"
                              step="0.0001"
                              value={line.quantity}
                              onChange={(event) =>
                                updateInventoryLine(line.key, {
                                  quantity: event.target.value,
                                })
                              }
                              disabled={
                                !canPrepareInventory || busyAction !== null
                              }
                            />
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor={`inventory-cost-${line.key}`}>
                              Unit cost
                            </Label>
                            <Input
                              id={`inventory-cost-${line.key}`}
                              type="number"
                              min="0.0001"
                              step="0.0001"
                              value={line.unitCost}
                              onChange={(event) =>
                                updateInventoryLine(line.key, {
                                  unitCost: event.target.value,
                                })
                              }
                              disabled={
                                !canPrepareInventory || busyAction !== null
                              }
                            />
                          </div>
                        </div>
                        {item?.isSerialTracked && (
                          <div className="space-y-1">
                            <Label htmlFor={`inventory-serial-${line.key}`}>
                              Serial number
                            </Label>
                            <Input
                              id={`inventory-serial-${line.key}`}
                              value={line.serialNumber}
                              onChange={(event) =>
                                updateInventoryLine(line.key, {
                                  serialNumber: event.target.value,
                                })
                              }
                              disabled={busyAction !== null}
                            />
                          </div>
                        )}
                        {item?.isLotTracked && (
                          <div className="space-y-1">
                            <Label htmlFor={`inventory-lot-${line.key}`}>
                              Lot number
                            </Label>
                            <Input
                              id={`inventory-lot-${line.key}`}
                              value={line.lotNumber}
                              onChange={(event) =>
                                updateInventoryLine(line.key, {
                                  lotNumber: event.target.value,
                                })
                              }
                              disabled={busyAction !== null}
                            />
                          </div>
                        )}
                        {item?.isBatchTracked && (
                          <div className="space-y-1">
                            <Label htmlFor={`inventory-batch-${line.key}`}>
                              Batch number
                            </Label>
                            <Input
                              id={`inventory-batch-${line.key}`}
                              value={line.batchNumber}
                              onChange={(event) =>
                                updateInventoryLine(line.key, {
                                  batchNumber: event.target.value,
                                })
                              }
                              disabled={busyAction !== null}
                            />
                          </div>
                        )}
                      </div>
                    );
                  })}
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setInventoryLines((current) => {
                      setInventoryPage(Math.floor(current.length / INVENTORY_LINES_PER_PAGE));
                      return [...current, newInventoryLine()];
                    })}
                    disabled={!canPrepareInventory || busyAction !== null}
                  >
                    <Plus className="mr-2 h-4 w-4" />
                    Add schedule line
                  </Button>
                </div>
                <div
                  className="space-y-2"
                  aria-label="Derived inventory posting"
                >
                  <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    Read-only posting at approved Inventory post
                  </p>
                  <ReadOnlyPostingLine
                    direction="Debit"
                    account="Server-derived Inventory Control"
                    amount={inventoryTotal}
                  />
                  <ReadOnlyPostingLine
                    direction="Credit"
                    account="Server-derived Migration Clearing"
                    amount={inventoryTotal}
                  />
                  <p className="text-xs text-muted-foreground">
                    Account IDs and directions are not browser inputs. This page
                    never creates duplicate Inventory masters or treats
                    Inventory account IDs as browser authority; Finance derives
                    and revalidates the posting at post.
                  </p>
                </div>
                <Button
                  className="w-full"
                  disabled={
                    !canPrepareInventory ||
                    !headerReady ||
                    busyAction !== null ||
                    !openingStockOptions?.isReady ||
                    inventoryBlockers.length > 0 ||
                    !selectedWarehouse ||
                    !inventorySourceReference.trim() ||
                    !inventoryDescription.trim() ||
                    inventoryImportErrors.length > 0 ||
                    !validInventoryLines
                  }
                  onClick={prepareInventory}
                >
                  {busyAction === 'inventory' && (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  )}
                  Prepare immutable opening stock
                </Button>
              </>
            )}
          </CardContent>
        </Card>

        <Card className="min-w-0">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <Scale className="h-5 w-5" />
              3. Residual GL &amp; equity
            </CardTitle>
            <CardDescription>
              Select only safe residual accounts. Retained Earnings and the
              balancing Migration Clearing debit are server derived.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <BlockerList
              title="Residual opening is not ready"
              blockers={residualBlockers}
            />
            <div className="space-y-2">
              <Label>Accrued expenses account</Label>
              <Select
                value={accruedExpensesAccountId}
                onValueChange={setAccruedExpensesAccountId}
                disabled={
                  !canPrepareFinance ||
                  financeOptionsLoading ||
                  busyAction !== null
                }
              >
                <SelectTrigger aria-label="Accrued expenses account">
                  <SelectValue placeholder="Select server option" />
                </SelectTrigger>
                <SelectContent>
                  {(financeOptions?.accruedExpensesAccounts ?? []).map(
                    (option) => (
                      <SelectItem key={option.id} value={option.id}>
                        {option.accountCode} — {option.accountName}
                      </SelectItem>
                    )
                  )}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="accrued-expenses-amount">
                Accrued expenses credit
              </Label>
              <Input
                id="accrued-expenses-amount"
                type="number"
                min="0.01"
                step="0.01"
                value={accruedExpensesAmount}
                onChange={(event) =>
                  setAccruedExpensesAmount(event.target.value)
                }
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2">
              <Label>Share capital account</Label>
              <Select
                value={shareCapitalAccountId}
                onValueChange={setShareCapitalAccountId}
                disabled={
                  !canPrepareFinance ||
                  financeOptionsLoading ||
                  busyAction !== null
                }
              >
                <SelectTrigger aria-label="Share capital account">
                  <SelectValue placeholder="Select server option" />
                </SelectTrigger>
                <SelectContent>
                  {(financeOptions?.shareCapitalAccounts ?? []).map(
                    (option) => (
                      <SelectItem key={option.id} value={option.id}>
                        {option.accountCode} — {option.accountName}
                      </SelectItem>
                    )
                  )}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="share-capital-amount">Share capital credit</Label>
              <Input
                id="share-capital-amount"
                type="number"
                min="0.01"
                step="0.01"
                value={shareCapitalAmount}
                onChange={(event) => setShareCapitalAmount(event.target.value)}
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="retained-earnings-amount">
                Retained earnings credit
              </Label>
              <Input
                id="retained-earnings-amount"
                type="number"
                min="0.01"
                step="0.01"
                value={retainedEarningsAmount}
                onChange={(event) =>
                  setRetainedEarningsAmount(event.target.value)
                }
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="residual-source-reference">
                Source schedule reference
              </Label>
              <Input
                id="residual-source-reference"
                value={residualSourceReference}
                onChange={(event) =>
                  setResidualSourceReference(event.target.value)
                }
                maxLength={50}
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="residual-description">Description</Label>
              <Textarea
                id="residual-description"
                value={residualDescription}
                onChange={(event) => setResidualDescription(event.target.value)}
                rows={2}
                disabled={!canPrepareFinance || busyAction !== null}
              />
            </div>
            <div className="space-y-2" aria-label="Derived residual posting">
              <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                Read-only derived posting
              </p>
              {accruedExpensesAccountId && (
                <ReadOnlyPostingLine
                  direction="Credit"
                  account={
                    financeOptions?.accruedExpensesAccounts.find(
                      (option) => option.id === accruedExpensesAccountId
                    )?.accountName ?? 'Accrued Expenses'
                  }
                  amount={positiveNumber(accruedExpensesAmount)}
                />
              )}
              {shareCapitalAccountId && (
                <ReadOnlyPostingLine
                  direction="Credit"
                  account={
                    financeOptions?.shareCapitalAccounts.find(
                      (option) => option.id === shareCapitalAccountId
                    )?.accountName ?? 'Share Capital'
                  }
                  amount={positiveNumber(shareCapitalAmount)}
                />
              )}
              <ReadOnlyPostingLine
                direction={
                  financeOptions?.retainedEarningsAccount?.postingDirection ??
                  'Credit'
                }
                account={`${financeOptions?.retainedEarningsAccount?.accountCode ?? 'Configured'} — ${financeOptions?.retainedEarningsAccount?.accountName ?? 'Retained Earnings'}`}
                amount={positiveNumber(retainedEarningsAmount)}
              />
              <ReadOnlyPostingLine
                direction={
                  financeOptions?.migrationClearingAccount?.postingDirection ??
                  'Debit'
                }
                account={`${financeOptions?.migrationClearingAccount?.accountCode ?? 'Configured'} — ${financeOptions?.migrationClearingAccount?.accountName ?? 'Migration Clearing'}`}
                amount={residualTotal}
              />
            </div>
            <Button
              className="w-full"
              disabled={
                !canPrepareFinance ||
                !headerReady ||
                busyAction !== null ||
                residualBlockers.length > 0 ||
                !accruedExpensesAccountId ||
                !shareCapitalAccountId ||
                positiveNumber(accruedExpensesAmount) <= 0 ||
                positiveNumber(shareCapitalAmount) <= 0 ||
                positiveNumber(retainedEarningsAmount) <= 0 ||
                !residualSourceReference.trim()
              }
              onClick={() =>
                onPrepareResidual({
                  sourceReference: residualSourceReference.trim(),
                  description: residualDescription.trim() || undefined,
                  openingDate,
                  fiscalPeriodId,
                  bookClassification,
                  accruedExpensesAccountId,
                  accruedExpensesAmount: positiveNumber(accruedExpensesAmount),
                  shareCapitalAccountId,
                  shareCapitalAmount: positiveNumber(shareCapitalAmount),
                  retainedEarningsAmount: positiveNumber(
                    retainedEarningsAmount
                  ),
                })
              }
            >
              {busyAction === 'residual' && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Prepare immutable residual batch
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
