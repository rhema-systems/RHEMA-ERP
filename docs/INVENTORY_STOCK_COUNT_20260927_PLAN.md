# Stock count requirements 5–7: implementation plan

Status: source analysis complete; implementation and acceptance pending. This document supplements `INVENTORY_CONTROLS_20260926_PLAN.md`. Transfer migration/build acceptance remains the active backend work; do not concurrently change the shared inventory entity model or migration snapshot.

## Verified existing behavior

| Area | Existing owner and confirmed gap |
| --- | --- |
| Count aggregate | `InventoryEnhancedEntities.cs`: `PhysicalCount` has a single user `CountedById` and a single `StockAdjustmentId`. There is no Employee committee or independent addendum aggregate. |
| Counter access | `PhysicalCountService.Review.cs` checks only `count.CountedById`. `PhysicalCountService.cs` sets that user when starting and calculates reviewer exclusions from existing actors. Every new assigned counter must be included in these checks and self-approval exclusions. |
| Recount | `PhysicalCountService.Controlled.cs`, `RecordRecountAsync`, writes the new value back into `CountedQuantity` and variance. It requires a different actor from the original counter/initiator. This is incompatible with preserving an immutable original sheet and returning selected lines to assigned counters. Preserve checker separation while replacing this mutation with an independently reviewed addendum. |
| Submission/review | `SubmitReviewedCoreAsync` and review logic clear existing recount flags. These paths must preserve selected exclusions rather than silently resetting them. |
| Adjustment | `EnsureStockAdjustmentSubmittedAsync` includes every counted nonzero variance and binds one adjustment to the whole count. It must operate on approved, eligible sheet lines and protect the root-line adjustment claim. |
| Freeze | Posting sets the whole count's `FreezeReleasedAtUtc`. A passed original sheet must not release protection required by outstanding recount sheets. |
| Import lineage | XLSX import uses `PhysicalCountSheetReader`, controlled record methods and retained DMS evidence. `PhysicalCountSheetLineage` reads the latest count-record action. Legacy JSON import also exists and writes quantities directly; it must enforce the same new counter/sheet rules. |
| Notification | `NotificationTopicPublisher` uses tenant topics and recipient rules; it may return without delivery if a topic is missing or disabled. Reuse the queued framework with an explicit configured counter-recipient path and durable event identity. Calling Publish alone is not evidence of email/in-app delivery. |

## Proposed additive model and ownership

1. Add tenant-scoped `PhysicalCountCounter` relationships to existing Employee records. Retain assignment actor/time/reason, active period and linked-user identity. Keep legacy `CountedById` for historical reads. Do not infer a user from an ambiguous email address. Employees without a usable linked application identity need an explicit notification/access outcome.
2. Add independent original/addendum sheet records with root count, parent/original sheet, attempt, lifecycle status, row version, submission actor/time, workflow/adjustment binding and DMS version lineage.
3. Add immutable submitted sheet-line snapshots referencing the original root count line and predecessor sheet line. Retain system quantity, counted quantity, cost, bin/tracking identity, defective quantity/remark and investigation reason. Editable recount drafts contain only flagged lines.
4. Add an adjustment claim keyed by tenant and original root line. Link it to the final approved sheet line and governed adjustment. Enforce uniqueness transactionally and at SQL level; denied original values and repeated addenda cannot post the same difference twice. Define reversal behavior through the existing adjustment reversal owner rather than deleting claims/history.
5. Keep legacy submitted/posted counts readable without regenerating prior journals or making historical snapshots appear newly approved. Specify deterministic compatibility behavior before migration; do not rewrite legacy recount history as if it were a new immutable sheet.

The backend count owner must coordinate edits to `InventoryEnhancedEntities.cs`, count entities/configuration, DTOs, repository loading, model snapshot, migrations and SQL lifecycle guards. Frontend and API contract implementation should begin after this owner confirms the typed sheet contract. Do not add only a UI filter over the current adjustment query.

## Implementation sequence

### Committee counters

- Add count-scoped employee search under the existing inventory count/manage capability. Return only the fields needed to select a counter; do not grant general HR access.
- Allow authorized counter assignment/removal before the count begins, with row-version validation and immutable assignment audit. Display assigned employees on details.
- Centralize the assigned-member check across start, manual recording, batch recording, JSON import, XLSX import, review and submission. Keep tenant, warehouse/location, actor and existing permission checks.
- Extend checker self-approval exclusions to every participating assigned counter. Preserve historical actor exclusions.
- Queue ready/assignment/recount notifications with stable event keys inside the same transaction. Include count/sheet/warehouse/schedule, assigner, access link and relevant investigation items/reasons. Verify both duplicate prevention and delivery failure visibility.

### Selective investigation and addenda

- Add an authorized per-sheet-line flag/reason action with idempotency key and row versions. Retain the submitted original values.
- Original passed lines continue through the same configured approval workflow. Flagged lines remain linked, hidden by default in the passed grid, and available through a recount toggle.
- Counter recount entry exposes only flagged lines. Submission creates a new addendum snapshot rather than changing the original sheet; repeat for subsequent attempts.
- Submit a separate governed StockAdjustment for each eligible sheet. Preserve workflow-required snapshots and optional-workflow behavior; never substitute a fresh workflow setting to bypass an approval cycle already entered.
- Under the count transaction lock, claim eligible root lines before posting and reject conflicting claims. Original rejected values cannot enter the adjustment builder. Include zero-variance resolved lines when determining completion, even though they do not produce adjustment lines.
- Keep the root freeze while unresolved original/addendum lines remain. Explicitly handle cancellation, rejected addenda, adjustment rejection/reversal and all-lines-recount cases.

### Defective quantity

- Add `DefectiveQuantity` and remark to manual record DTOs, detail grid, XLSX template/export, reader, import preview, legacy JSON import and immutable action/sheet snapshots.
- Validate nonnegative quantity no greater than the physical count with existing quantity precision. Preserve blank/backward-compatible template behavior.
- Keep variance exactly `CountedQuantity - SystemQuantity`; defective quantity does not change physical quantity, adjustment quantity, valuation or GL. Defect disposal/reclassification remains a separate authorized transaction.
- Preserve blind-count behavior and existing bin/lot/serial lineage validation in every import/export surface.

## Required acceptance evidence

- Multiple Employee counters, draft removal/audit, authorized member edits, nonmember denial, cross-tenant denial and checker separation.
- Assignment/recount email and in-app notification queue evidence, replay deduplication and explicit handling of employees without a linked user/email.
- Mixed passed/recount original sheet posts only passed differences; original values remain unchanged. Addendum #1 and #2 repeat configured approval and post each resolved root line once.
- Concurrent original/addendum and duplicate-post attempts prove SQL claim uniqueness and transaction locking. Preserve freeze until final unresolved sheet is closed.
- Defective quantity example 100 system / 100 physical / 5 defective yields zero variance and no adjustment, across manual, XLSX and JSON routes. Invalid negative/over-physical values fail consistently.
- Historical count reads, preexisting DMS evidence, blind count, tracking scopes, workflow-present/absent, cancellation/reversal and existing approval tests continue to pass.
- Normal analyzer build, focused API/frontend tests, isolated upgrade/fresh-install migration checks, SQL concurrency tests and visible lifecycle checks are required before marking these requirements complete.


## Agreed implementation refinement: reuse child physical counts (requirements 6–7)

The root task approved reusing `PhysicalCount` for independently approved addenda. Each child owns the existing count lifecycle and one governed StockAdjustment; this avoids a second approval engine. The original/root count remains the navigation context. This refines the proposed separate-sheet model above without changing the immutable observation, lineage or one-adjustment-per-root-line requirements.

Implemented source contract (acceptance status below):

- Add nullable `RootPhysicalCountId` and `ParentPhysicalCountId` plus `RecountAttempt` to a child count. Root/legacy records retain null references and attempt zero. Child count lines reference the original root line and predecessor line; their system quantity, cost, bin and tracking snapshots come from the retained root observation, never a new on-hand query.
- `POST /inventory/physical-counts/{id}/recounts` accepts the current count row version, idempotency key, comment and selected `{physicalCountItemId,itemRowVersion,reason}` rows. It creates a draft recount workspace containing only those lines and records the decision against the immutable original observations. Submission of that workspace produces the independently submitted addendum via the existing count submission API.
- Addendum details include `rootPhysicalCountId`, `parentPhysicalCountId`, `recountAttempt` and sibling sheet summaries for a sheet selector within the original count context. A child remains a real count record with existing authorization/row-version/workflow controls; register presentation must not misrepresent it as an unrelated count.
- Manual `RecordCountItemDto`, batch and JSON imports, XLSX reader/export/preview, item DTO and audit snapshots gain `defectiveQuantity` and `defectiveNotes`. Blank historical fields map to zero/null. Both values are persisted per sheet; quantities validate at existing four-decimal precision and defect quantity is bounded by physical quantity. Variance still equals physical minus system.
- Recount marking before original submission partitions the existing adjustment builder into passed versus flagged lines. Once an observation is submitted it cannot return to editable original values. Existing investigation/rejection entry points must create a new recount workspace instead of calling the current overwrite path. A submitted/posted adjustment must never be edited in place to remove lines; any later repartition requires the existing governed retirement/reapproval path.
- A unique `(TenantId, RootPhysicalCountItemId)` posting claim binds the final sheet line, original quantity basis and StockAdjustment item. Resolved zero-variance lines also need a terminal claim to close the investigation without inventing a stock movement. Reversals retain the claim and original history; a new stock count is a separate root.
- Root/count locks must serialize original and all descendants under the same root key. A second active recount workspace for the same root line is rejected, and replay returns the existing child.
- Recount children hold only their selected line scope. Their freeze must become effective when the workspace is created, including while still Draft, before the parent may release its own freeze. The existing SQL freeze predicates omit Draft and Posted, so simply preserving a root timestamp is insufficient. Parent posting must leave outstanding child scope protected, and passed lines must not be adjusted twice.
- Copy the active Employee committee through validated current Employee/User links; retain all predecessor actors in checker exclusions. Assignment/recount notices use the existing queue and include root count, addendum attempt, item identities and reasons.

Before source implementation, coordinate the next model/migration boundary with the transfer migration owner. Required new tests include partial original posting while a child is Draft, two concurrent descendant claims, repeated addendum attempts, failed adjustment rollback, zero-variance resolution, tenant/scope denials, and defects carried unchanged through every import path.


## Source checkpoint: counters, defects and child recounts (2026-09-27)

Implementation now exists in the continuation worktree; **migration, normal full build and live/SQL lifecycle acceptance remain pending**. The preceding design sections are retained to explain the architecture, not to imply those acceptance gates have passed.

- Committee assignment uses typed Employee/User relationships, retained assignment history, exact existing count capabilities/scopes and current linked-user revalidation. Notifications queue one inbox record and one email-only record; email delivery remains the worker's responsibility.
- Defective quantity/notes are separate persisted observations, included in manual, JSON and XLSX paths and audit payloads. Physical minus system still defines variance. New Excel templates append `Defective Qty` and `Defective Notes`; old four/five-column uploads remain accepted and preserve existing defects. Blank defect cells on a new template mean zero. JSON imports now require each line's `rowVersion` and `idempotencyKey` and use the same governed line recorder in one root transaction.
- Selective recounts create child PhysicalCount records with root/parent lineage and retained system quantity, cost, bin and lot/serial basis. The original observation is not overwritten. Original passed lines are the only lines included in its new adjustment. Child sheets repeat existing review, evidence, approval and posting paths.
- Unique root-line claims include zero-variance final resolutions and bind nonzero differences to their exact governed adjustment line. Root locks serialize descendants. Draft child sheets hold their selected stock scope before the original can finish posting.
- Submitted adjustments cannot be repartitioned in place. Use the existing investigation decision/retirement before selecting recount lines. Counts with unresolved recount obligations cannot be cancelled to release their freeze. Legacy counts without an Employee committee cannot create a child until handled through a governed replacement count; existing history is not rewritten into fabricated assignments.
- The register labels recount records and details provide an original/recount sheet selector. Selected original lines are hidden by default, available with `Show recount items`, and read-only. Creating a recount preserves failed selections/replay keys and carries line row versions.

Focused frontend evidence: transfer fractional quantities and defects suites had 59 passing cases plus one obsolete column-count expectation; the corrected upload/grid rerun passed all 16 cases. New recount panel/grid rerun passed all 14 cases. The existing review actions passed in the preceding run. Req7 narrow TypeScript passed with zero diagnostics; the initial req6 TypeScript pass also returned zero diagnostics. Backend tests for defects and count model lineage are written but not executed yet. Normal Core analyzer build passed with zero errors (513 existing warnings; `tmp/count-core-checkpoint-build.log`). Twelve embedded count SQL blocks parsed with ScriptDom160 without syntax errors. The isolated SQL fixture then passed 25 checks, including committee notification identity/history, immutable selected observations, draft-child stock freeze after parent posting, duplicate root-line resolution and actual two-session contention: the second session blocked and failed with SQL 2601, leaving one committed claim. Evidence: `tmp/count-guards-20260927_121409_a10c29d7.json`; reproducible script: `scripts/acceptance/Test-PhysicalCountRecountSqlGuards.ps1`. This fixture exercises the new guards and existing freeze guards on a minimal isolated schema; it is not a full migration or complete API lifecycle test. The normal full Data/API build, backend test execution, combined migration and visible lifecycle checks remain outstanding.
