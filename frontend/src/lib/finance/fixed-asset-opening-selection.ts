import type { FixedAssetOpeningBalanceCandidate } from '@/types/finance';

export type BulkSelectionState = boolean | 'indeterminate';

export function getEligibleFixedAssetBookValueIds(
  candidates: FixedAssetOpeningBalanceCandidate[],
  bookClassification: string
) {
  return candidates
    .filter(
      (candidate) =>
        candidate.bookClassification === bookClassification &&
        !candidate.openingPostedToGl
    )
    .map((candidate) => candidate.fixedAssetBookValueId);
}

export function reconcileFixedAssetSelection(
  selectedIds: string[],
  eligibleIds: string[]
) {
  const eligible = new Set(eligibleIds);
  const reconciled = selectedIds.filter((id) => eligible.has(id));

  return reconciled.length === selectedIds.length &&
    reconciled.every((id, index) => id === selectedIds[index])
    ? selectedIds
    : reconciled;
}

export function getFixedAssetBulkSelectionState(
  selectedIds: string[],
  eligibleIds: string[]
): BulkSelectionState {
  if (eligibleIds.length === 0) return false;

  const selected = new Set(selectedIds);
  const selectedEligibleCount = eligibleIds.filter((id) => selected.has(id)).length;

  if (selectedEligibleCount === 0) return false;
  return selectedEligibleCount === eligibleIds.length ? true : 'indeterminate';
}

export function toggleAllEligibleFixedAssets(
  selectedIds: string[],
  eligibleIds: string[]
) {
  return getFixedAssetBulkSelectionState(selectedIds, eligibleIds) === true
    ? []
    : [...eligibleIds];
}
