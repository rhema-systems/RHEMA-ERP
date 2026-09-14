export type ReceiptMissingItemDecision =
  | 'existing'
  | 'pending'
  | 'create'
  | 'skip';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export function formatReceiptQuantitySummary(
  orderedQuantity: number,
  previouslyReceived: number,
  maximumReceivable: number,
): string {
  const remaining = Number(Math.max(0, orderedQuantity - previouslyReceived).toFixed(4));
  const summary = `Ordered: ${orderedQuantity} • Previously Received: ${previouslyReceived} • Remaining: ${remaining}`;
  return maximumReceivable > remaining
    ? `${summary} • Max. incl. tolerance: ${maximumReceivable}`
    : summary;
}

export function hasControlledInventoryItem(value?: string | null): boolean {
  const normalized = (value || '').trim().toLowerCase();
  return Boolean(normalized && normalized !== EMPTY_GUID);
}

export function buildReceiptItemCode(
  orderNumber: string,
  itemName: string
): string {
  const itemPart = itemName
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 50) || 'ITEM';
  const poPart = orderNumber
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 35);
  return `RCV-${poPart}-${itemPart}`.slice(0, 100);
}

export function findNextPendingReceiptItemIndex(
  items: ReadonlyArray<{ missingItemDecision: ReceiptMissingItemDecision }>,
  completedIndex = -1
): number | null {
  const nextIndex = items.findIndex(
    (item, index) =>
      index !== completedIndex && item.missingItemDecision === 'pending'
  );
  return nextIndex >= 0 ? nextIndex : null;
}

