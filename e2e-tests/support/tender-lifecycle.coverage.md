# Real tender lifecycle coverage ledger

This ledger maps `tests/tender-lifecycle.real.spec.ts` to the authoritative lifecycle IDs in `docs/testing/TENDER_PLAYWRIGHT_LIFECYCLE_MATRIX.md`. It distinguishes browser automation from fixture preparation and external SQL verification. Test discovery, compilation, or an opt-out skip is not acceptance evidence.

Status meanings:

- **Automated**: the serial Playwright suite performs the browser/API transition and asserts its visible result.
- **Fixture-assisted**: the suite verifies or consumes a deliberately prepared lifecycle state, but does not create every prerequisite through the browser.
- **Pending**: the current suite does not execute the complete positive and negative matrix gate.
- **Known product gap**: the matrix names behaviour that the current application does not expose consistently enough for a passing acceptance test.

| Gate | Current status | Playwright evidence | Work still required before full matrix acceptance |
| --- | --- | --- | --- |
| TND-001 | Fixture-assisted | `00A` opens an approved PR and enters the Tender flow. | External verifier must prove readiness, effective budget/authority lineage, release-history uniqueness, and all negative readiness codes. |
| TND-002 | Automated | `00A` uses the visible Tender action and checks the source PR is loaded. | Add explicit RFQ/direct-PO method and stale-readiness negative branches. |
| TND-003 | Automated + SQL pending | `00A` creates one draft and records its generated ID/number. | SQL verifier must prove exactly one release, locked case, source request, and tender; add concurrent-create retry. |
| TND-004 | Partial | `00A`-`00B` save/reopen template, dates, PR lines, and visible lots. | Automate invitations, fee accounts, documents, item-to-lot persistence, invalid template/supplier/document cases, and SQL row comparison. |
| TND-005 | Pending | None. | Create/delete a disposable draft and prove the immutable release/case remains while submitted/published/repeated deletes are controlled. |
| TND-006 | Automated positive | `00C` submits as maker and approves in a separate approver context. | Add maker/self-approval, unassigned approver, rejection/reason, repeat-decision, and workflow SQL assertions. |
| TND-007 | Fixture-assisted | Committee states are supplied for `03`-`05`; active evaluator access is exercised. | Automate evaluator selection, template binding, composition rules, duplicates, other-tenant user denial, and committee-row SQL checks. |
| TND-008 | Fixture-assisted | `05` consumes an active/effective committee. | Automate appointment responses, conflict declarations, activation evidence, negative readiness branches, and SQL assertions. |
| TND-009 | Conditional or explicitly Pending | `00D` publishes only when `prePublication.publication.prerequisitesPrepared=true`; otherwise it records `Pending` before mutation. Labels support both statutory advertisement and controlled solicitation methods. | An authoritative preparation step must attach invitations, controlled documents and method-specific publication controls to the browser-created tender before setting the fixture flag. |
| TND-009A | Automated positive | `05D` creates a published-tender revision and verifies it after refresh. | Add every invalid revision/deadline/tenant branch and SQL sequence/notification assertions. |
| TND-010 | Known product gap | Supplier pages are exercised after publication, but Viewed transition is not asserted. | Application must invoke the existing view-log behaviour; then assert `Invited -> Viewed`, `ViewedDate`, `TenderViewLogs`, and blocked-supplier isolation. |
| TND-011 | Automated positive | `01` selects association scope, accepts any declaration, initiates, and records the bid ID. | Add SelectedUsers membership persistence and invalid user/declaration/duplicate/other-partner negatives. |
| TND-012 | Automated browser, SQL pending | `01` records payment evidence and `02` verifies it as the officer. | External verifier must prove one payment, posting event, balanced journal, and working journal link; add reject/wrong account/currency/self-verify/retry branches. |
| TND-013 | Automated positive | `01` saves and reopens lot/item price, quantity, delivery, brand/model, proposals, and configured documents. | Add invalid lot, zero values, missing/unsafe document, non-owner, foreign-tenant, and exact SQL comparisons. |
| TND-014 | Partial | `01` submits a fresh Supplier A bid. The separate evaluation fixture supplies two API-authoritatively prepared submitted bids, and `05A` verifies Supplier B ownership/visibility. | Automate a fresh Supplier B submission branch; add deadline, declaration/document/payment admission, ownership, second-submit, invitation-history, and total SQL negatives. |
| TND-015 | Known product gap | Not automated as a positive updated submission. | Current service keeps submitted bids immutable. Align UI wording, then test stable controlled denial and immutable values, or implement versioned resubmission before testing it. |
| TND-016 | Pending | None. | Add third disposable bid withdrawal, cutoff/owner/reason/retry negatives, and exclusion from ranking. |
| TND-017 | Automated opening, fixture-assisted clock | `02` proves the new future-deadline bid cannot open; guarded preparation retains two sealed receipts on the elapsed NCT control, and `04A` formally opens both through the signed controlled-opening action. The SQL verifier checks both bid timestamps and the immutable opening snapshot/hash. | Automate the preceding governed clock transition; add late-bid, pending-fee, repeated-open and other timing negatives. |
| TND-018 | Fixture-assisted | Active committee fixture includes the meeting/quorum state needed by evaluation. | Automate meeting creation, attendance and quorum finalization plus chair/count/duplicate/expiry negatives and SQL checks. |
| TND-019 | Automated positive | `03`-`05` create/reload evaluation drafts under absent, draft, and active committee states. | Add wrong-tenant, unopened/inadmissible, duplicate-draft, and submitted-edit negatives with exact SQL score checks. |
| TND-020 | Automated controlled stages | `05` and `05B` use distinct committee members to sign one current locked Technical sheet and one current locked Financial sheet; `05E` proves a repeated technical mutation is rejected without stage regression. | Add recall/replacement and incomplete/stale/cross-tenant score-sheet negatives. |
| TND-021 | Automated recommendation | `05B` records both evaluated amounts, gives Supplier A the higher score, selects that exact bid, and retains the signed recommendation. | Add report export, tie, wrong-winner, calculation comparison, and excluded-bid negatives. |
| TND-022 | Automated positive | `05C` uses the Head of Procurement to submit the exact locked award workflow after the signed phase records are current. `08` re-evaluates the authoritative award-readiness boundary before award. | Add stale expectation, changed supplier, incomplete evidence, committee/quorum and source negative branches. |
| TND-023 | Automated controlled award | `07` uses a distinct ETC member to decide the workflow; `08` records the exact recommended bid only after a current Ready decision. | Add wrong bid/supplier/amount, duplicate award, missing readiness/statutory evidence, and supplier-notification assertions. |
| TND-024 | Automated positive | `05C` Head-of-Procurement submission and `07` ETC decision prove separate assigned actors; the SQL verifier checks the two retained user IDs differ. | Add rejection, unassigned/tenant/ineligibility/repeat negatives and bidder winner/loser notification. |
| TND-025A | Statutory contract record automated; contract-module conversion pending | `09` records the executed-contract reference/evidence on the independently approved statutory control; `10` records bidder acceptance. | Create and independently activate the full procurement `Contract` entity, prove Finance commitment uniqueness, retry and negative branches. |
| TND-025B | Automated draft conversion | `10A` converts a distinct independently approved fixture award through the real TenderAward-to-PO API, captures the returned PO ID, and proves a repeated conversion cannot create another PO. SQL verifies the exact award/source snapshot, recovered PR/release/case/readiness lineage, line presence, and one-time consumption. | Add submit/independent PO approval plus shortfall and SOD negative branches. |
| TND-026 | Pending | None; `05D` is a tender revision, not a contract amendment. | Automate contract amendment request, independent approve/reject, value/time changes, budget adjustment, retry, and tenant/SOD negatives. |
| TND-027 | Pending | Tender revision is covered, but cancellation/termination/delete controls are not. | Add award cancellation, contract termination and eligible draft-delete branches; retain explicit expected failure if tender cancellation remains unavailable. |
| TND-028 | Automated representative isolation | `06` asserts unauthorized bid/evaluation denial. `06A` uses an authorized Procurement Officer against a known tender in a separately seeded tenant and requires controlled denial for both read and revision mutation. SQL proves the foreign tender remains Draft with zero revisions. | Extend the same foreign-tenant probes to documents/downloads, payment, readiness, approvals, conversions, and other amendment families. |
| TND-029 | PO export only | `11` downloads a non-empty generated PO PDF and monitors browser errors. | Add PR, tender, bid/evaluation report, award, contract exports; parse content and test unauthorized/cross-tenant export/audit evidence. |

## Result and verification boundary

The suite writes browser-created IDs atomically to `TENDER_E2E_RESULT`. Its final `status` can become `Passed` only when no checkpoint is `Pending`; later evidence-only checks such as PO PDF export cannot overwrite an earlier pending gate. The acceptance runner must use those exact IDs for read-consistent SQL assertions; it must never infer records from newest-row queries.

A release claim requires all of the following, not merely the current 24-test discovery count:

1. Every automated test executes and passes against the fresh fixture with no unexpected procurement HTTP failure or browser runtime error.
2. The acceptance runner verifies tenant, status, lineage, actor, timestamp, presence, uniqueness, postings, and commitments for the IDs in `TENDER_E2E_RESULT`.
3. Every row above marked Partial, Fixture-assisted, Pending, or Known product gap is closed or explicitly accepted as a documented manual/product-gap exception.
4. The same built candidate then passes focused backend/frontend regression and guarded VPS smoke.
