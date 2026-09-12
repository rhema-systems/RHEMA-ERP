# Physical-count scope — 11 September 2026

This records the scope-only rollout. The subsequent [warehouse default-bin update](WAREHOUSE_DEFAULT_BINS_20260911.md) supersedes the unresolved-location behaviour and adds a schema migration; the original verification evidence below is retained.

## Behaviour

- New Count explicitly offers Warehouse-wide or Selected location. Changing warehouse or scope clears the location. Only active locations belonging to the selected warehouse are offered.
- Warehouse-wide snapshots one row per item/location across the warehouse. Selected location snapshots only assignments and balances in that location. The Excel export uses those saved rows; system quantities remain hidden on the blind count sheet.
- Draft Add item uses location-specific assigned stock, including zero balances. The same item may appear in different locations but not twice at the same location. Server-side snapshots ignore caller-supplied system quantities.
- Existing valuation rules determine location-level snapshot cost. The normal controlled adjustment and independent approval flow remain unchanged.
- Warehouse stock without a saved location is retained as an unresolved draft line, not silently omitted or allocated to a guessed bin. Start/submission reject unresolved locations. Negative/inconsistent warehouse-versus-location balances are rejected before any count is inserted.
- Existing started count snapshots are not rewritten. In particular, rehearsal PC-20260910-0001 remains UnderReview with its original seven lines and saved quantities; it still needs a properly scoped replacement once stock locations are resolved.
- Old item-code-only imports reject repeated SKUs across bins. The current upload matches item plus location.
- Control history is displayed newest first without changing stored audit order.
- Walkthrough B20 updated in Markdown and HTML.

## Verification

- 32 backend scope/lifecycle tests passed, including invalid warehouse/location, residual preservation, duplicate pairs, baseline override rejection, multi-location export, old import ambiguity and independent approval lifecycle.
- 40 frontend page tests, 6 draft-item dialog tests and 17 count-sheet tests passed (63 total).
- Focused TypeScript check passed.
- Production frontend build passed.
- API build passed with 27 existing warnings and no errors.
- Tested Core SHA256: `5226F85A2BFD8F8FAA16E399A63FD8F5DD50DABAED46B7BCA1DFA4F146355969`.

## Deployment scope

Rehearsal only: frontend 3002 and API 5002, verified listening with main UAT 3000/5000 stopped. Main UAT data and the existing rehearsal count are preserved. This change introduces no schema migration; existing LocationId and item/location fields are reused.

## Live browser and database checks

- Saved warehouse-wide verification draft PC-20260911-0001 through the visible rehearsal UI: 9 items, including 3 located rows and 6 unresolved location rows. The details warning explains that unresolved stock locations must be addressed before starting.
- Saved selected-location verification draft PC-20260911-0002 through the visible rehearsal UI: Project Demo Warehouse, LOC-001, Full Count. Create remained disabled until a location was chosen. The saved Items tab contains only PVC Pipe 50mm, Barcode Device Kit and Wireless Keyboard, all LOC-001.
- Read-only SQL confirms location-only system baselines of 36, 41 and 0 respectively. Both verification counts remain Draft, all lines uncounted, with no StartedDate or StockAdjustmentId. Neither count was started, submitted, approved or posted.
- Original PC-20260910-0001 remains UnderReview with seven active lines, unchanged saved quantities, no stock adjustment and its existing missing locations. Two previously removed lines remain soft-deleted. This update does not retroactively repair its saved snapshot.
- Opened the original Control history read-only and verified descending sequence 14, 13, 12 through 1.
- Follow-up display request: replace the three hidden-quantity placeholders with `xxx`, preserving blind-count checks. Shared grid covers dialog and full-page views.
