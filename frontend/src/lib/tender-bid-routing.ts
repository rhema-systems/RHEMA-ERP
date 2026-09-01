export type SupplierTenderBidRoute = {
  id: string;
  status?: string | null;
};

export const getSupplierTenderBidPath = (
  tenderId: string,
  bid: SupplierTenderBidRoute
): string =>
  bid.status?.trim().toLowerCase() === 'draft'
    ? `/external-portal/tenders/${tenderId}/initiate-bid`
    : `/external-portal/my-bids/${bid.id}`;
