import { apiService } from './api.service';

export type ReceiptDistributionPurpose =
  | 'Inventory'
  | 'AccruedPurchases'
  | 'PurchasePriceVariance';

export interface PurchaseReceiptDistributionLine {
  lineId?: string;
  inventoryItemId?: string;
  itemCode?: string;
  itemName?: string;
  purpose?: ReceiptDistributionPurpose;
  accountId: string;
  accountCode: string;
  accountName: string;
  type: string;
  source: string;
  debit: number;
  credit: number;
}

export interface PurchaseReceiptDistributionGroup {
  inventoryItemId: string;
  purpose: ReceiptDistributionPurpose;
  itemCode: string;
  itemName: string;
  totalDebit: number;
  totalCredit: number;
  defaultAccountId: string;
}

export interface PurchaseReceiptDistributionAccount {
  id: string;
  accountCode: string;
  accountNumber?: string;
  accountName: string;
  accountType?: string | number;
  allowDirectPosting?: boolean;
  isControlAccount?: boolean;
}

export interface PurchaseReceiptDistribution {
  status: 'Proposed' | 'Posted';
  currency: string;
  basis: string;
  journalEntryId?: string;
  journalEntryNumber?: string;
  canEdit?: boolean;
  version?: string;
  basisVersion?: string;
  hasOverrides?: boolean;
  needsReview?: boolean;
  editBlockReason?: string;
  groups?: PurchaseReceiptDistributionGroup[];
  totalDebit: number;
  totalCredit: number;
  lines: PurchaseReceiptDistributionLine[];
}

export interface SavePurchaseReceiptDistribution {
  version: string;
  basisVersion: string;
  lines: Array<{
    lineId: string;
    inventoryItemId: string;
    purpose: ReceiptDistributionPurpose;
    accountId: string;
    debit: number;
    credit: number;
  }>;
}

export const purchaseReceiptDistributionService = {
  get(receiptId: string) {
    return apiService.get<PurchaseReceiptDistribution>(
      `/PurchaseOrderReceipts/${receiptId}/distribution`
    );
  },
  accounts(receiptId: string) {
    return apiService.get<PurchaseReceiptDistributionAccount[]>(
      `/PurchaseOrderReceipts/${receiptId}/distribution/accounts`
    );
  },
  save(receiptId: string, input: SavePurchaseReceiptDistribution) {
    return apiService.put<PurchaseReceiptDistribution>(
      `/PurchaseOrderReceipts/${receiptId}/distribution`,
      input
    );
  },
  reset(
    receiptId: string,
    input: Pick<SavePurchaseReceiptDistribution, 'version' | 'basisVersion'>
  ) {
    return apiService.post<PurchaseReceiptDistribution>(
      `/PurchaseOrderReceipts/${receiptId}/distribution/reset`,
      input
    );
  },
};
