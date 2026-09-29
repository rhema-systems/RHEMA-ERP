export interface ArRevenueAccountOption {
  status?: string;
  accountType?: string;
  allowDirectPosting?: boolean;
  isControlAccount?: boolean;
}

/** Manual AR lines may post only to active, direct-posting revenue accounts. */
export function isEligibleManualArRevenueAccount(account: ArRevenueAccountOption): boolean {
  return account.status === 'Active'
    && account.accountType === 'Revenue'
    && account.allowDirectPosting === true
    && account.isControlAccount !== true;
}

/** Snapshots the header default onto a newly created line. Existing lines remain unchanged. */
export function taxGroupForNewArInvoiceLine(
  headerTaxGroupId: string | null | undefined,
  isOpeningBalance: boolean
): string {
  return isOpeningBalance ? 'none' : (headerTaxGroupId || 'none');
}
