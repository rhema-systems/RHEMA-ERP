# Physical-count review and decisions — 11 September 2026

## Requested workflow

Count → Review variance → correct entries → Submit for approval → independent Stores, Finance and Audit decisions → Post.

- No new automatic recount flags or mandatory independent recount stage.
- Only the assigned counter can correct quantities in In Progress or Under Review. The original first count and every saved correction remain in immutable history.
- Submitted counts are locked. An investigation decision retires the unposted adjustment and blocks posting. The counter records findings, resumes review and resubmits through all approval stages.
- Legacy RecountRequired records are displayed as Awaiting review. Review variance transitions them without rewriting their saved quantities or old audit events.
- Existing file validation, current-sheet lineage, evidence, scope, row-version, idempotency, separation of duties, stock freeze and controlled Finance posting remain enforced.

## Administration

Administration → Inventory → Physical Count Decisions.

Administrators with `procurement.inventory.master-data.manage` can configure active decision codes and labels. Each decision has exactly one of two server-enforced effects:

| Effect | Consequence |
| --- | --- |
| Approve adjustment | Advances the current approval stage. Does not itself post inventory or GL. |
| Start investigation | Returns the count for investigation and blocks posting. |

At least one active choice for each effect is required. Configuration is tenant-scoped, revision-checked and audited in the existing SystemSettings/AuditLogs owners. Every recorded approval retains the selected decision and configuration revision. Changing configuration does not rewrite earlier approvals.

## Requirements checked

The original **TDC ERP Architecture and Design Document (002).docx**, section **16.1 Stores, Inventory, and Fixed Asset Design**, calls for stock-taking evidence, discrepancy records, reconciliation, approval and audit controls. Its extracted text did not specify automatic recount on every variance. The proposed configurable decision flow implements the user's request; it is not presented as an exact workflow prescribed by that source.

The two older detailed procurement Word documents referenced by the tracker were not found at their recorded locations, so their contents were not used as evidence. B20 of the customer walkthrough (Markdown and HTML) has been updated for this revised sequence.

## Verification

- API Release build: passed (existing repository warnings remain).
- Focused TypeScript check: passed, including the new admin page.
- Frontend: 25 review/page tests passed; the existing 23 count-sheet parsing/upload tests also passed in the preceding run.
- Core count lifecycle: 7 tests passed, including correction history, submission replay, independent approval and investigation.
- Shared stock-adjustment transaction: 2 tests passed. Both an idempotent submission replay and a rejected submission leave the enclosing count transaction open for its owner to commit or roll back.
- API/count validation/security: 41 tests passed.
- Real rehearsal SQL: 13 rollback-only cases passed for review transitions, immutable original counts, locked submitted quantities, investigation blocking and append-only history. Synthetic fixtures were rolled back; no user count was completed or posted by these tests.
- Production frontend build: passed. Initial temporary-folder dependency resolution was fixed with a node_modules junction beside the external .next output; the succeeding build completed normally.

## Database and runtime scope

Migration: `20260911210000_PhysicalCountReviewDecisions` updates existing count lifecycle, action and stock-freeze guards. It creates no new business tables and rewrites no count, stock or approval records.

A copy-only, checksum-verified rehearsal backup was created before deployment:
`C:\Program Files\Microsoft SQL Server\MSSQL15.SQL2017\MSSQL\Backup\RhemaERP_Rehearsal_before_count_review_20260911_2005.bak`.

The migration was applied to `RhemaERP_PO_Rehearsal_20260909` only. Rehearsal API port 5002 and production frontend port 3002 were restarted from the review build. SQL verification after deployment confirmed the user's count was still RecountRequired in stored legacy data, with seven counted lines, total counted quantity 438 and no stock adjustment linked. The UI translates that legacy state to Awaiting review; no count quantities or business actions were saved by deployment.

Live browser check as `manager`: opened the existing count and selected **Review variance** only. The real API/SQL transition succeeded: status **UnderReview**, one new ReviewStarted audit event, all seven old recount flags cleared, counted total still 438 and no linked adjustment. The Items tab exposes the saved quantities as editable fields alongside the protected original system snapshot; Submit for approval is available. No quantity save, submission, approval or posting was performed. The count is left open for the user's review.

Main UAT remains stopped and its database has not been migrated for this change. It will require the matching application build and migration before this feature is used there.

## Existing-count limitation

`PC-20260910-0001` currently has seven saved quantities totalling 438, but neither its header nor its item lines have exact locations. Review/correction can be demonstrated; a non-zero stock adjustment cannot use those location-less snapshots. Do not invent a bin or rewrite a started count's source snapshot. Resolve this separately with a location-scoped count before testing non-zero stock posting. No submission, approval or stock/GL posting is included in this change's live acceptance evidence.
