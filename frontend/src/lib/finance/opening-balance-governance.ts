import type {
  GovernedOpeningBalanceOptions,
  GovernedOpeningBalanceOptionsRequest,
  OpeningBalanceBatch,
} from '@/types/finance';

export interface OpeningStockLocationOption {
  id: string;
  code: string;
  name: string;
}

export interface OpeningStockWarehouseOption {
  id: string;
  code: string;
  name: string;
  locations: OpeningStockLocationOption[];
}

export interface OpeningStockItemOption {
  id: string;
  itemCode: string;
  name: string;
  unitOfMeasure: string;
  isSerialTracked: boolean;
  isLotTracked: boolean;
  isBatchTracked: boolean;
}

export interface OpeningStockOptions {
  isReady: boolean;
  blockers: string[];
  retrievedAtUtc: string;
  warehouses: OpeningStockWarehouseOption[];
  items: OpeningStockItemOption[];
}

export interface CreateOpeningStockItemDto {
  inventoryItemId: string;
  locationId: string;
  quantity: number;
  unitCost: number;
  serialNumber?: string;
  lotNumber?: string;
  batchNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  reason?: string;
  notes?: string;
}

export interface CreateOpeningStockAdjustmentDto {
  warehouseId: string;
  openingDate: string;
  bookClassification: string;
  sourceScheduleReference: string;
  description: string;
  items: CreateOpeningStockItemDto[];
}

export interface GovernedInventoryOpeningResult {
  id: string;
  adjustmentNumber: string;
  status: string;
  reference?: string;
  rowVersion: string;
  financePostingEventId?: string;
  financeJournalEntryId?: string;
}

const tenantKey = (tenantCode: string | null) => tenantCode ?? 'missing-tenant';

export const openingBalanceQueryKeys = {
  accounts: (tenantCode: string | null) =>
    ['finance', 'opening-balances', tenantKey(tenantCode), 'accounts'] as const,
  periods: (tenantCode: string | null) =>
    ['finance', 'opening-balances', tenantKey(tenantCode), 'periods'] as const,
  books: (tenantCode: string | null) =>
    ['finance', 'opening-balances', tenantKey(tenantCode), 'books'] as const,
  settings: (tenantCode: string | null) =>
    ['finance', 'opening-balances', tenantKey(tenantCode), 'settings'] as const,
  diagnostics: (tenantCode: string | null) =>
    [
      'finance',
      'opening-balances',
      tenantKey(tenantCode),
      'diagnostics',
    ] as const,
  batches: (tenantCode: string | null) =>
    ['finance', 'opening-balances', tenantKey(tenantCode), 'batches'] as const,
  subledgerReadiness: (tenantCode: string | null) =>
    [
      'finance',
      'opening-balances',
      tenantKey(tenantCode),
      'subledger-readiness',
    ] as const,
  specializedOptions: (tenantCode: string | null) =>
    [
      'finance',
      'opening-balances',
      tenantKey(tenantCode),
      'specialized-options',
    ] as const,
  governedOptions: (
    tenantCode: string | null,
    request: GovernedOpeningBalanceOptionsRequest
  ) =>
    [
      'finance',
      'opening-balances',
      tenantKey(tenantCode),
      'governed-options',
      request.openingDate || 'missing-opening-date',
      request.fiscalPeriodId || 'missing-fiscal-period',
      request.bookClassification || 'missing-book-classification',
    ] as const,
  openingStockOptions: (tenantCode: string | null) =>
    [
      'finance',
      'opening-balances',
      tenantKey(tenantCode),
      'opening-stock-options',
    ] as const,
};

export function hasCompleteGovernedOpeningHeader(
  request: GovernedOpeningBalanceOptionsRequest
) {
  return Boolean(
    request.openingDate.trim() &&
      request.fiscalPeriodId.trim() &&
      request.bookClassification.trim()
  );
}

export function canPostOpeningBalanceBatch(status?: string | null) {
  return status === 'Approved' || status === 'PostingFailed';
}

export function isOpeningBalanceBatchImmutable(
  batch: OpeningBalanceBatch | null | undefined
) {
  return Boolean(batch?.isSystemGenerated || batch?.isEditable === false);
}

export function getBankOpeningBlockers(
  options: GovernedOpeningBalanceOptions | undefined,
  bankAccountId: string,
  loading = false
) {
  const selectedBank = options?.bankAccounts.find(
    (option) => option.id === bankAccountId
  );
  return [
    ...(selectedBank?.blockers ?? []),
    ...(options?.migrationClearingAccount?.blockers ?? []),
    ...(!loading && options && !options.migrationClearingAccount
      ? [
          'Migration Clearing Account is not available for the governed bank opening.',
        ]
      : []),
  ];
}

export function canLoadOpeningBalanceQueries(input: {
  authLoading: boolean;
  tenantLoading: boolean;
  tenantCode: string | null;
  canView: boolean;
}) {
  return (
    !input.authLoading &&
    !input.tenantLoading &&
    Boolean(input.tenantCode) &&
    input.canView
  );
}
