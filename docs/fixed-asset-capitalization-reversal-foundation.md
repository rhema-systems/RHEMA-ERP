# Fixed Asset Capitalization Reversal and Correction Foundation

## Outcome

Finance can now correct an incorrectly posted fixed-asset capitalization without editing or deleting posted cost. The system retains the original journal and posting event, requires an independent review, posts a linked compensating journal through the central Finance posting engine, and synchronizes the fixed-asset register and accounting books.

This resolves `FIN-LIM-0030` for the Finance-owned capitalization paths.

## Client requirement traceability

This slice was designed from both the TDC requirements traceability workbook and the Finance limitations register.

| TDC requirement | Requirement outcome | Delivered evidence |
|---|---|---|
| `FR-GL-008` | Authorised linked reversal of posted journals with reason, approval, and audit trail | Maker-checker request; minimum reason and impact evidence; permission-separated review; original/reversal journal and posting-event links; audit events |
| `FR-GL-010` | No direct editing of posted entries; use correction journals or approved reversal-and-repost | Existing posted journal remains immutable; central posting engine creates a compensating journal; corrected capitalization starts a new posting cycle |
| `FR-FA-001` | Fixed-asset register retains cost, life, depreciation and status | Register and all active accounting-book values are reduced through an evidenced reversal transaction |
| `FR-FA-002` | Assets can originate from invoices or manual capitalization with supporting linkage | Direct capitalization uses the maker-checker correction workflow; AP invoice void uses the AP-owned reversal journal and synchronizes the asset register |
| `FR-FA-010` | Asset register reconciles to GL by class and period | Original and reversal journal/event IDs are retained on the asset, accounting-book values, request record, and asset transaction history |

Cross-module acquisition interfaces remain outside this Finance-only slice. Procurement/stores integrations must call the canonical source-document workflow rather than create another Finance reversal route.

## Business intent

A posted capitalization has already changed both the general ledger and the asset register. Correcting only one side would cause the fixed-asset reconciliation to disagree with the ledger. Editing the original row would also destroy who posted it, which accounts were used, and what was known at the time.

The implemented control therefore treats correction as a new accounting event:

1. A permitted maker requests reversal, supplies the requested posting date, states the error, and records the expected impact.
2. A different permitted user approves or rejects the request with a review comment.
3. A permitted poster posts an approved request.
4. The central posting engine reverses the original journal and links both posting events.
5. The asset and active accounting books record zero current cost and net book value, while immutable history retains the original capitalization and the negative reversal movement.
6. A corrected capitalization can be posted as a new cycle without reusing the original posting engine idempotency key.

## Source ownership rule

The correction starts where the accounting source is owned:

| Capitalization source | Correction route | Journal result |
|---|---|---|
| Direct Finance fixed-asset capitalization | Fixed-asset capitalization correction workspace | One linked Finance reversal journal |
| Posted AP vendor invoice | Void/reverse the AP invoice | AP posts one reversal journal; the same journal/event synchronizes the asset register |
| Future procurement/stores source | Source module workflow | Source module must call the canonical Finance posting/reversal contract |

This boundary prevents parallel reversal journals for one economic event.

## Control rules

- Only the current, posted, unreversed direct capitalization can enter the direct correction workflow.
- The original posting event and journal IDs are mandatory.
- The request reason and impact assessment must be substantive.
- The requested reversal date must satisfy the shared Finance reversal-date and open-period policy.
- The requester cannot review their own request.
- Only a pending request can be reviewed; only an approved request can be posted.
- Later depreciation, valuation, GL transfer, disposal, or other value movements block the reversal. Those lifecycle entries must be corrected in reverse order first.
- Repeated posting returns the established reversal result rather than posting a second correction.
- Capitalizations owned by an AP/source document cannot use the direct fixed-asset route.
- Posted accounting fields remain protected by the existing fixed-asset update guard.

## Persisted evidence

`FixedAssetCapitalizationReversal` retains:

- the asset and tenant;
- original posting event and journal;
- reversal posting event and journal;
- request date, reason, and impact assessment;
- maker identity and timestamp;
- reviewer identity, decision, comment, and timestamp;
- posted/failed state and failure evidence.

The current reversal IDs and date are also exposed on `FixedAsset`, `FixedAssetBookValue`, and capitalizable `VendorInvoiceLineItem` records. Original capitalization IDs are not overwritten.

## Audit and access

Permissions:

- `Finance.FixedAssets.Capitalization.Reverse`
- `Finance.FixedAssets.Capitalization.Reversal.Approve`

Audit events cover request, approval, rejection, posting, failure, and blocked correction attempts. The UI hides actions a user is not permitted to perform, while the API remains the authoritative enforcement boundary.

## Operator workspace

The fixed-asset edit workspace contains a Capitalization correction control card that:

- explains why correction is a linked accounting event rather than an undo;
- shows maker, reviewer, decision, reason, impact, original journal, and reversal journal;
- exposes request, review/reject, and post actions according to permission and state;
- directs AP/source-owned assets back to the source document;
- refreshes register cost and status after posting.

## Robustness improvements discovered by regression testing

The new tests exposed two pre-existing tracker-boundary risks in the capitalization path:

1. Creating the fallback IFRS accounting book cleared the shared EF tracker and detached the asset immediately before register updates. The broad clear was removed.
2. The central posting engine may clear tracking when recovering from a concurrent/idempotent posting race. The fixed-asset service now rehydrates the asset—and, for reversal, its maker-checker request—after that boundary before applying register changes.

Both changes are documented inline because other Finance-module consumers rely on the same posting engine behavior.

## Verification evidence

Focused regression scenarios cover:

- independently approved direct reversal posts one linked journal and removes current register/book cost;
- requester cannot approve their own request;
- downstream asset accounting blocks an unsafe reversal;
- AP invoice void uses one AP-owned reversal journal and synchronizes the asset register without duplicate Finance posting.

The tests carry `FR-GL-008`, `FR-GL-010`, and `FIN-LIM-0030` traits so requirement evidence remains searchable.

## Demonstration wow factors

- Show the original journal ID before the correction and the linked reversal journal afterward—the history is preserved rather than rewritten.
- Sign in as the maker and demonstrate that self-approval is rejected; then approve as an independent reviewer.
- Show the impact assessment and reviewer comment alongside the journal lineage.
- Attempt reversal after a downstream value movement and demonstrate that Finance protects reconciliation by requiring reverse-order correction.
- Void a capitalizable AP invoice and show that AP and Fixed Assets share one reversal posting event rather than creating competing journals.
- Re-capitalize the corrected asset and show the new accounting cycle while the earlier cycle remains auditable.

## Remaining boundaries

- Revaluation/impairment reversal and correction remain governed by their dedicated limitation slices (`FIN-LIM-0035` and `FIN-LIM-0037`).
- This correction feature does not own Procurement receiving. Procurement-origin capitalization
  and its reversal lineage are now composed separately through FIN-INT-007 without moving receipt
  workflow logic into Finance.
- Richer reporting presentation can expose the same persisted lineage; the accounting and audit evidence is already available.
