# TDC Procurement and Inventory Implementation Handover

Last updated: 2026-08-01

Document status: `TDC-0401` through `TDC-0409`, `TDC-0501` through `TDC-0509`, and `TDC-0601` are verified `Done`; Phase 5 PR #23 review corrections are pushed through `a706f449`; representative Phase 5 `E2E-018`, Phase 4 `E2E-003`, and Phase 6 `E2E-024` remain explicit open acceptance gates; `TDC-0602` is the next eligible implementation task and remains `Not started`

## Handover Objective

This document transfers the TDC Procurement and Inventory implementation from completed requirement and gap analysis into controlled delivery. The next agent should be able to open this repository, verify the live state, and continue the next tracker slice without repeating the analysis or inventing a parallel architecture.

The governing delivery ledger is `docs/tdc-procurement-inventory-gap-implementation-tracker.md`. It currently contains 93 roadmap tasks across phases 0 through 9 and 27 mandatory end-to-end acceptance scenarios. Phase 0 (`TDC-0001` through `TDC-0007`), Phase 1 (`TDC-0101` through `TDC-0108`), Phase 2 (`TDC-0201` through `TDC-0212`), and Phase 3 (`TDC-0301` through `TDC-0309`) are verified `Done`. Later roadmap tasks and end-to-end scenarios retain the status recorded in the tracker and must change only as verified implementation work progresses.

## Immediate Starting Decision

The accepted Phase 0 through Phase 3 stack remains the merged baseline commit `61658141`. PR #20 merged `TDC-0401` through `TDC-0403`; PR #22 merged the remaining verified Phase 4 implementation to `master` at `d332f469` from exact verified head `2bf5b132`. `TDC-0401` through `TDC-0409` remain verified `Done`. Phase 4 implementation is complete, while representative `E2E-003` remains an explicit open UAT acceptance gate. `TDC-0501` through `TDC-0509` are now verified `Done` locally on branch `agent/tdc-0501-receipt-source-control`: tenant-safe governed source/capacity, inspection/quality hold, independently approved acceptance and disposition, supplier acknowledgement/dispute, return/replacement, closure, stock/AP eligibility, exact nine-action PO-creator-versus-receipt-actor SOD, mandatory server-derived invoice three-way matching, centralized AP payment readiness, invoice-processor-versus-payment-approver SOD, the complete AP-006 three-way-match exception lifecycle/report, controlled posted AP invoice/payment reversal, unified commitment-to-GL/retention/milestone reconciliation, and the DEC-013-driven GRN/MRN register lifecycle are enforced through existing shared controls. Finance remains authoritative for allocation, supplier advances, exact-invoice payment batches, posting, reversal, settlement and AP-control reconciliation through `IVendorPaymentService`, `IFinancePostingEngine`, and the shared Finance read models. Receipt source/capacity, inspection/acceptance, central numbering and central DMS remain authoritative for GRN/MRN generation; TDC-0509 composes them without another receipt, inspection, document repository or inventory path. The configured user-secret database `.\SQL2017` / `RhemaERP` is current at `20260801013000_TDC0509ReceiptDocumentLifecycle`: 200 history rows, three enabled receipt-document triggers, trusted constraints, one reconciliation view, active TDC-GRN/TDC-MRN central templates and zero receipt-document rows. All Phase 5 implementation tasks are complete; representative `E2E-018` remains an explicit phase-acceptance gate.

Phase 2 sourcing through award is complete. `TDC-0201` derives one tenant-safe sourcing case from a current immutable PR sourcing release and enforces that current case at RFQ/tender entry. `TDC-0202` makes the method recommendation server-derived from the exact current policy and protects any override with eligible exception, capability, independent workflow, evidence, actors, and SOD. `TDC-0203` completes the RFQ vertical flow. `TDC-0204` completes NCT/ICT from approved advertisement and controlled paid/free document issue through sealed/late submissions, signed public opening, separate technical/financial evaluation, exact authority/PPA workflow, approved award, executed-contract reference, bidder acceptance, immutable SQL records, and `DEC-001` through `DEC-014` control-event lineage. `TDC-0205` completes Restricted Tendering and Single Source. `TDC-0206` completes tenant-safe prequalification and deterministic qualified-list eligibility. `TDC-0207` completes reusable controlled tender-document families, immutable approved versions, exact Tender/RFQ binding, issuance/receipt, addenda/acknowledgements, and controlled extensions. `TDC-0208` completes the reusable tenant-safe evaluation-committee control: exact composition and appointments, acceptance/COI evidence, signed attendance and remote/hybrid evidence, server-derived quorum, scorer eligibility, immutable signed score attempts, independently approved recall, exact RFQ/NCT/legacy projection parity, eight-table SQL protection, all fourteen decision events, and dedicated Tender/RFQ history-first controls. `TDC-0209` completes the reusable tenant-safe recommendation-approval and award-readiness decision across RFQ, formal Tender, Exceptional Sourcing, and legacy tender-award boundaries, including exact current evaluation/score, supplier/prequalification, verification/due-diligence, recommendation, authority/workflow, evidence, actor, policy/method, integrity, immutable SQL/audit, API, and shared history-first UI controls. `TDC-0210` completes the reusable evaluator-versus-award-approver hard stop across those four boundaries, deriving exact direct and committee score-attempt lineage, allowing only independently approved non-sole outcomes, recording enriched allowed/denied control events, and exposing fail-closed current-actor status in the shared readiness UI without adding schema. `TDC-0211` completes the reusable successful/unsuccessful bidder-communication and tender-security register across formal Tender, RFQ, and Exceptional Sourcing, with server-derived award/readiness/supplier/subject lineage, immutable approved letter versions, dispatch/delivery/acknowledgement, standstill/appeal, security release/return, shared workflow/evidence/notification control events, ten-table SQL protection, and dedicated internal/external views. `TDC-0212` completes the configurable manual/API-ready GHANEPS publication and award exchange across formal Tender, RFQ, and Exceptional Sourcing, including exact evidenced Published/effective `DEC-009` and mapping lineage, immutable payload/attempt/failure/retry/acknowledgement/reconciliation history, SQL protection, role-neutral API, and dedicated shared Tender/RFQ history controls.

`TDC-0301` supplies the category-aware registration evidence-pack baseline, `TDC-0302` supplies the governed free/paid application token and Finance lifecycle, and `TDC-0303` through `TDC-0308` supply eligibility, due diligence, AVL, risk/performance, and supplier-master controls. `TDC-0309` completes verified contact, restricted applicant session/UI, shared controlled-document storage and scanning, terminal token/session closure, permission-complete review and approval, approval-based identity/role activation, one-time credentials, forced password change, resend/retry, and audit. `TDC-0401` through `TDC-0409` complete governed frameworks, purchase orders, contracts and Works closeout. `TDC-0501` through `TDC-0509` complete governed receipt, inspection/SOD, AP matching/readiness/SOD/exception/reversal/reconciliation, and DEC-013 GRN/MRN controls while preserving the existing Finance, DMS, workflow, evidence and inventory transaction owners. `TDC-0601` completes authoritative tenant-scoped InventoryItem primary/alternate/QR plus ItemUnitOfMeasure barcode persistence, uniqueness, lookup, import/export, ordinary edit and maker-checker application, dedicated shared control, audit and SQL protection. `TDC-0602` is next eligible but remains `Not started`; Phase 4 `E2E-003`, Phase 5 `E2E-018`, and Phase 6 `E2E-024` remain open.

## Authoritative Inputs And Precedence

Use the following sources in this order:

1. `C:\Users\micha\Downloads\Requirement Documents\TDC SRS Procurement ERP.docx` is the minimum client contractual baseline.
2. `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Procurement\Procurement ERP.docx` supplies additional operating detail.
3. `docs/tdc-procurement-inventory-gap-implementation-tracker.md` is the live implementation ledger and requirement traceability record.
4. `docs/procurement-planning-tasklist.md` records procurement-planning capabilities already delivered. Do not recreate or reopen those capabilities unless current code verification proves a regression or a tracker gap.
5. `docs/enterprise-workflow-module-integration-guide.md` governs reuse of the shared workflow platform.
6. The live source code and database migrations decide what currently exists. Reverify code before changing a tracker baseline classification.

If the SRS and questionnaire differ, preserve the SRS requirement and record the questionnaire detail as configuration or an extension. Do not silently remove statutory or client requirements.

## Verified Repository Snapshot

This snapshot was refreshed on 2026-07-31 after PR #22 merged at `d332f46905cc9e64a0d7c59f8d4a8c4d07517eae`. The local Phase 5 content baseline is its exact verified PR head `2bf5b1323b3aae0106cf540c0240cb2a862e34a5`; local HTTPS credentials must be refreshed before the next remote publish, but the connected GitHub integration confirms the merge. `TDC-0501` through `TDC-0509` are verified complete in the preserved dirty worktree; `TDC-0601` is next eligible while Phase 5 `E2E-018` remains open.

| Item | Verified state |
| --- | --- |
| Repository root | `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2` |
| Branch | `agent/tdc-0501-receipt-source-control`; local start tree `2bf5b132`; merged Phase 4 `master` commit `d332f469` verified through GitHub |
| Baseline commit | `d332f46905cc9e64a0d7c59f8d4a8c4d07517eae` (`Complete remaining Phase 4 procurement controls (#22)`) |
| .NET SDK | `8.0.206` from `global.json` |
| Frontend | Next.js `15.5.3`, React `19.1.0`, TypeScript 5, React Query 5, Vitest 3, Lucide icons, shared Radix/shadcn controls |
| Backend build | Final TDC-0501 through TDC-0509 Core/Data/API and focused test-project builds pass with zero errors; the final bounded API build retains eight repository-baseline warnings only. TDC-0509 adds the full model snapshot and migration `20260801013000_TDC0509ReceiptDocumentLifecycle`; Data Release/model validation and the final API compile pass. |
| Focused Core/API integration tests | TDC-0501 passes `22/22`; TDC-0502 inspection rules/migration tests pass `23/23`; TDC-0503 SOD rules/services pass `26/26` plus migration `3/3`; TDC-0504 matching rules/migration pass `15/15`; TDC-0505 readiness rules/migration pass `15/15`; TDC-0506 SOD rules/service/migration pass `7/7`; TDC-0507 AP-006 rules/migration tests pass `7/7`. TDC-0508 controlled reversal/reconciliation tests pass `5/5`, and the combined invoice/payment/reconciliation/settlement/export regression passes `64/64`. TDC-0509 receipt-document rules/migration tests pass `14/14`, controller mappings pass `5/5`, and frontend client tests pass `4/4`. |
| Focused API evidence | TDC-0502 through TDC-0508 retain their recorded API evidence. TDC-0509 adds tenant-safe permission-protected overview/ensure/reconcile/sign/issue/cancel/download routes. Real configured-database smoke proves anonymous `401`, authenticated overview `200`, effective profile ID/version, GRN-and-MRN applicability, six control checks, exactly `DEC-001` through `DEC-014`, missing receipt `404`, and structured `422 RCV_DOCUMENT_TEMPLATE_MISSING` for the deliberately obsolete acceptance template instead of 500, with zero register residue. |
| Targeted frontend verification | Earlier Phase 5 frontend baselines remain verified. TDC-0509 client tests pass `4/4` and scoped ESLint is clean. Playwright Chromium logs in through the real frontend/API, renders the dedicated receipt `GRN / MRN` tab, effective profile version, source/inspection/document/evidence/DMS checks, all fourteen decisions, ensure/reconcile states and immutable audit-history empty state, and passes `1/1` with a clean 1440x1100 visual review. |
| Full test-project boundary | The broader Core test-project source remains aligned with the already-changed Projects constructor; no Projects runtime behavior changed. The TDC-0508 test project rebuild succeeds. Repository-wide frontend TypeScript retains attributed unrelated baseline errors outside all TDC-0508 files. |
| Test database | User-secret `.\SQL2017` / `RhemaERP` is current at 200 migration-history rows with `20260801013000_TDC0509ReceiptDocumentLifecycle` latest. Three receipt-document triggers are enabled; all checks/foreign keys are trusted; active TDC-GRN/TDC-MRN metadata and generation templates, the reconciliation view, and zero receipt-document rows are verified. Rollback-only protected-delete and forged-snapshot probes fail with `51670` and `51671`. Existing Finance, Procurement, Projects, receipt, inspection and inventory records remain authoritative. |

### Dirty Worktree Protection

The remaining local dirty entries include unrelated pre-existing user/team root deletions and two architecture images; they remain deliberately excluded from the completed `TDC-0501` through `TDC-0509` slices. The verified Phase 5 changes are limited to receipt-source/capacity, inspection/closure, exact receipt SOD, mandatory invoice matching, AP payment readiness, invoice-processor-versus-payment-approver SOD, the bounded AP-006 exception lifecycle/report, Finance-owned controlled AP reversal, the read-only AP-005 cross-module reconciliation surface, and the configuration-derived GRN/MRN register plus their minimum supporting API/shared-control surfaces. Unrelated inventory behavior remains preserved. All pre-existing entries remain preserved; ignored browser screenshot evidence remains available while no test data or owned process remains.

Treat every current entry as accepted user/team work. Do not reset, revert, delete, stash, or broadly reformat it merely to obtain a clean tree. Before `TDC-0601`, reverify status and diff, preserve `TDC-0401` through `TDC-0509`, and keep any commit narrowly scoped to the verified implementation.

### TDC-0207 Acceptance Summary

`TDC-0207` is `Done`. The authoritative code and evidence anchors are recorded in the tracker. The accepted slice includes:

- One tenant-safe reusable template/version lifecycle with exact policy/configuration/workflow/evidence/checksum/effective/supersession lineage, soft-delete-safe clone numbering, immutable Published/Retired history, and future-effective replacement behavior.
- One exact Tender/RFQ register with supplier-validated paid/free issuance and receipts, recipient/channel/actor evidence, append-only issue history, addenda and acknowledgement, authorized submission-deadline extension, and permitted post-closing bid-validity extension while validity remains active.
- Explicit authorization, external ownership isolation, independent approval/SOD, shared workflow/evidence/notification/control-event reuse, concurrency/idempotency, legacy NCT/ICT issue delegation, and unchanged PR/PO/receiving/inventory behavior.
- Migration `20260723184003_AddProcurementTenderDocumentControls`, seven protected tables/triggers, successful rollback-only SQL hard-stop probes, repeat database apply, no pending EF model changes, and zero acceptance residue.
- 209/209 Core procurement, 123/123 procurement API, 100/100 frontend, focused 7/7 Core, 6/6 API, and 8/8 frontend tests; zero-error builds; targeted lint and TDC-owned TypeScript clean; authenticated API and real-browser/visual smoke complete.

### TDC-0209 Acceptance Summary

`TDC-0209` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One reusable append-only tenant/source/sequence award-readiness decision with exact current evaluation/locked-score, recommendation, supplier/prequalification, verification/due-diligence, authority/workflow, evidence, actor/roles, policy/method, prerequisite, timeline, source-integrity, and `DEC-001` through `DEC-014` control-event lineage.
- Four server-enforced irreversible award gates covering RFQ handoff, formal Tender award, Exceptional Sourcing award, and legacy `TenderAward`; each re-evaluates current server state and asserts exact recommended subjects/suppliers before accepting `RecordAward`.
- A tenant-safe latest/history/evaluate API with structured errors and no public decision-record endpoint, plus shared dedicated Tender/RFQ history-first pages whose actions require both permission and the current server-provided allowed action.
- Migration `20260724020403_AddProcurementAwardReadinessControls`, append-only/source-parity SQL errors `51400` through `51409`, verified apply/rollback/reapply/repeat-latest, selected direct negative probes, and zero fixture residue.
- 239/239 Core procurement, 149/149 procurement API, and 120/120 frontend regressions; focused 36/36 Core, 12/12 API, and 8/8 frontend; zero-error builds; clean targeted lint and TDC-owned TypeScript; authenticated API and real-browser/visual smoke with exact process cleanup.

### TDC-0210 Acceptance Summary

`TDC-0210` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One trusted server-derived evaluator-versus-award-approver decision across RFQ, formal Tender, Exceptional Sourcing, and legacy award paths, including direct evaluator projections and exact committee control, appointment, subject, retained/recalled score-attempt, method-rule, current-readiness/hash, authority/workflow approval-actor, policy/rule, actor/roles, and correlation lineage.
- Same-source evaluator denial before any readiness or award mutation when no distinct recorded non-evaluator approval actor exists; exact independent approval prevents over-blocking. Missing, duplicate, foreign, mismatched, or non-contiguous lineage fails closed.
- Reuse of the existing SOD/AuditLog/control-event architecture with enriched allowed/denied events. Sole-actor relaxation inputs are internal-only and ignored by public JSON binding, so the generic SOD API cannot manufacture an independent actor.
- Authenticated tenant-safe `/api/procurement/award-readiness/sod-status`, structured four-boundary outcomes, and a shared Tender/RFQ/Exceptional SOD card that renders before the first readiness decision and fail-closes Evaluate for loading/error/incomplete/blocked state or missing approval permission.
- No new migration: EF reports no model drift, the database remains current through `20260724020403_AddProcurementAwardReadinessControls`, and live evidence shows zero readiness residue, zero disabled procurement triggers, and zero untrusted procurement foreign keys.
- 252/252 Core procurement, 166/166 procurement API, and 123/123 frontend regressions; focused 29/29 Core, 55/55 API, 5/5 boundary, 2/2 legacy, and 11/11 frontend; zero-warning/zero-error solution build; successful optimized Next build; clean targeted lint and zero TDC-0210 TypeScript diagnostic matches; authenticated API/browser/visual smoke and exact process cleanup.

### TDC-0211 Acceptance Summary

`TDC-0211` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One tenant-safe register spanning formal Tender, RFQ, and Exceptional Sourcing with server-derived successful/unsuccessful recipients and exact current award/readiness/supplier/bid-or-quote lineage.
- Approved immutable letter versions with template/content checksums; dispatch channel/reference/time/actor; delivery and acknowledgement; standstill and appeal dates/outcomes; and tender-security instrument, release, and return history.
- Shared workflow, evidence, notification, access/SOD, and `DEC-001` through `DEC-014` control-event reuse; structured role-neutral API; cross-source resource binding; fail-closed external supplier scope; and dedicated shared internal/external UI.
- Migration `20260724134047_AddProcurementBidderCommunicationsAndSecurityReturns`, ten protected tables and triggers, successful exact rollback/reapply/repeat-latest, ten append-only probes, RFQ/Tender source-mapping probe, trusted foreign keys, no model drift, and zero acceptance residue.
- 264/264 Core procurement, 182/182 procurement API, and 134/134 frontend regressions; focused 12/12 Core, 16/16 API, and 11/11 frontend; zero-error targeted builds; successful optimized Next build; clean targeted lint and zero TDC-0211 TypeScript diagnostic matches; bounded API/browser/visual smoke and exact process cleanup.

### TDC-0212 Acceptance Summary

`TDC-0212` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One configurable tenant-safe manual/API-ready exchange across formal Tender, RFQ, and Exceptional Sourcing publication/award events, deriving exact source lifecycle/reference and one evidenced Published/effective `DEC-009` decision with a unique applicable 17-property mapping.
- Immutable source/configuration/mapping snapshots and SHA-256 lineage, versioned export/import payloads, typed safe downloads, submission success/failure/retry, acknowledgement, reconciliation/mismatch resolution, authorization/SOD, concurrency, idempotency, shared evidence/notification, and `DEC-001` through `DEC-014` control-event reuse.
- Migration `20260724175059_AddProcurementGhanepsExchangeControls`, five protected tables/triggers/checks, exact rollback/reapply, 26 rollback-only SQL probes passing both before and after the cycle, trusted foreign keys, no model drift, and zero acceptance residue.
- 307/307 Core procurement, 209/209 procurement API, and 161/161 frontend regressions; focused 43/43 Core, 26/26 API, and 27/27 frontend; zero-error builds; successful optimized Next build; clean targeted lint and zero GHANEPS TypeScript diagnostic matches; bounded API/browser/visual smoke with three reviewed screenshots and exact process cleanup.

### TDC-0301 Acceptance Summary

`TDC-0301` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One tenant-safe, versioned and effective-dated supplier-registration evidence-pack family for Goods, Works, and Services, with Draft/Pending Approval/Published/Retired lifecycle, same-family future-replacement continuity, category-aware mandatory documents, classifications, validity rules, approval workflow, Works-classification hard stop, and exact Published/effective resolution.
- Immutable registration-to-pack binding and server-derived applicant readiness; explicit manage/review/approve permissions; independent authorization/SOD; row-version concurrency; idempotency; shared workflow, evidence, document, notification, configuration-profile, and `DEC-001` through `DEC-014` control-event reuse.
- Authenticated tenant-safe API and dedicated shared-control administration register/draft editor with lifecycle, loading, empty, error, and controlled-unavailable states, plus external registration category, requirements, document metadata, readiness, and status presentation. Token payment, restricted applicant sessions, and approved-account activation remain outside this slice.
- Migration `20260724203636_AddProcurementSupplierEvidencePacks`, three enabled SQL protection triggers, guards `51700` through `51713`, 16/16 rollback-only valid-path and hard-stop probes, trusted foreign keys, no model drift, and zero acceptance residue.
- Focused 5/5 Core, 2/2 API, and 5/5 frontend automation; zero-error backend build; clean targeted ESLint; successful optimized Next build; bounded authenticated API smoke with zero 5xx; real-browser smoke over 24 API requests with zero page/server/console errors; three visually reviewed screenshots; and exact process/port cleanup.

### TDC-0302 Acceptance Summary

`TDC-0302` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One tenant-safe application-bound token aggregate with immutable payment and exemption histories, explicit one-generation rotation, row-version concurrency, correlation idempotency, integrity hashes, and no time-based expiry.
- Exact evidenced Published/effective `DEC-007` resolution for free/paid mode, fee/tax/accounts, allowed payment methods, configured shared receipt numbering, workflow exemption, refund and renewal rules; unverified, missing, ambiguous, stale, or foreign configuration fails closed.
- Shared Finance posting-engine integration, posting-ready payment-method validation, idempotent receipt generation, independent reconciliation, shared workflow/evidence/notification/SOD/control-event reuse, and terminal token expiry only after the linked registration persists Approved or Rejected.
- Authenticated tenant-safe `/api/procurement/supplier-onboarding-tokens` summary/search/detail/options and controlled action routes plus the dedicated `/administration/procurement/supplier-onboarding-tokens` history-first shared-control UI. Restricted applicant authentication/UI, email/SMS credential delivery, and supplier-account activation remain `TDC-0309`.
- Migration `20260725111657_AddProcurementSupplierOnboardingTokens`, three protected tables/triggers, successful exact rollback/reapply, 17/17 valid-path and hard-stop probes, trusted foreign keys, no model drift, and zero acceptance residue.
- Focused 9/9 Core, 2/2 API, and 8/8 frontend automation; zero-error Core/Data/API builds; successful exact-source optimized Next build; bounded authenticated API smoke with structured negative outcomes and zero 5xx; real-browser smoke with zero page/server/console errors; two visually reviewed screenshots; and exact process/port cleanup.

### TDC-0303 Acceptance Summary

`TDC-0303` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One tenant-safe centralized eligibility decision enforced before RFQ/tender invitation, formal/exceptional/RFQ award, contract creation, manual PO, and `Blanket` framework call-off.
- Exact current supplier approval/registration/active/blacklist state, categories, license/performance inputs, governed registration/evidence-pack readiness, qualified-list/prequalification lineage, and evidenced Published/effective `DEC-011` AVL lineage. Missing production AVL policy remains an explicit release-configuration gate rather than an invented value.
- Deterministic SHA-256 decision hash, structured findings/outcomes, correlation/hash idempotency, and shared immutable `DEC-001` through `DEC-014` control-event audit without recreating workflow/evidence controls or changing receiving/inventory runtime.
- Authenticated internal-only tenant-safe eligibility/boundary API and a dedicated permission-gated administration route with controlled loading/empty/error states, supplier/status/evidence/AVL/hash/findings visibility, and no supplier-approval or transaction mutation action.
- Focused 5/5 Core, 2/2 API, and 1/1 frontend automation; zero-error Core/API outputs; clean targeted ESLint; successful optimized Next build; no pending EF model changes; protected SQL totals unchanged; API smoke with zero 5xx and structured hard stops; real-browser smoke with zero page/server/console errors; one visually reviewed screenshot; and exact process/port cleanup.

### TDC-0305 Acceptance Summary

`TDC-0305` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One formal tenant-safe annual AVL register with soft-delete-safe versions, exact effective/expiry dates, Draft/shared-workflow/independent approval or rejection, effective publication, future replacement without an interim policy gap, scheduled expiry/retirement, and immutable publication snapshots.
- Supplier membership derived through the accepted centralized eligibility decision while intentionally skipping only self-referential formal-AVL membership; exact registration/evidence/prequalification, approved-clear due diligence, `DEC-011` policy/profile/value, and supplier lineage remain retained and revalidated.
- Evidenced suspension/reinstatement and expiry history, row-version concurrency, correlation idempotency, manage/approve authorization, SOD, notifications, and immutable `DEC-001` through `DEC-014` control events. Current formal AVL membership is now part of central invitation/award/contract/manual-PO/framework-call-off eligibility when the production policy is available.
- Authenticated tenant-safe `/api/procurement/supplier-avl` summary/history/detail/current/options and lifecycle routes plus the dedicated `/administration/procurement/supplier-avl` history-first shared-control UI with controlled loading/empty/error/release-gate states.
- Migration `20260726174311_AddProcurementSupplierAvlLifecycle`, four protected tables/triggers, exact rollback/reapply, six lifecycle/immutability SQL hard stops, a valid SQL suspend/revalidate/reinstate path with two history rows, schema protection, trusted foreign keys, no model drift, zero acceptance residue, and unchanged protected PR/PO/receiving/inventory counts.
- Focused 5/5 Core, 2/2 API, and 1/1 frontend automation; zero-error Core/Data/API builds; clean targeted ESLint; successful optimized Next build; bounded authenticated API smoke with zero 5xx/mutations; real-browser smoke with 14 API requests and zero page/server/console errors; one visually reviewed screenshot; and exact process/port cleanup.

### TDC-0306 Acceptance Summary

`TDC-0306` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One tenant-safe immutable supplier-risk assessment using exact approved/evidenced Published/effective `DEC-011` risk dimensions whose unique weights total 100 percent, contiguous 0-100 risk bands, exposure window, supplier/category and single-source limits, and typed eligibility action; invalid or absent production values fail closed as a visible release gate.
- Server-derived currency-separated awarded spend, supplier and category concentration, single-source dependency, formal AVL, approved-clear due diligence, centralized eligibility, dimension contributions, risk band/action, exact source snapshots, and deterministic SHA-256 lineage without inventing defaults.
- Controlled Open/Escalated/Resolved alerts with current-tenant shared workflow/evidence validation, independent actors/SOD, authorization, concurrency, idempotency, notifications, and immutable `DEC-001` through `DEC-014` events. Specifically mapped award evaluation enforces the configured hard-stop action; non-award evaluation remains advisory.
- Authenticated tenant-safe `/api/procurement/supplier-risk` summary/history/detail/current/options and lifecycle routes plus the dedicated `/administration/procurement/supplier-risk` history-first UI and shared supplier-eligibility risk card.
- Migration `20260726224147_AddProcurementSupplierRiskAndConcentration`, two protected tables/triggers, exact rollback/reapply, valid Open-to-Escalated-to-Resolved lifecycle, append-only rejection `51300`, invalid-transition rejection `51313`, trusted foreign keys, no model drift, zero acceptance residue, and unchanged protected PR/PO/receiving/inventory totals.
- Focused/adjacent 20/20 Core, 2/2 API, and 8/8 frontend automation; zero-error Core/Data/API builds; clean targeted ESLint; successful optimized Next build; bounded authenticated API smoke with zero 5xx/mutations; real-browser smoke with 15 API requests and zero page/server/console errors; one visually reviewed screenshot; and exact process/port cleanup. Repository-wide TypeScript retains only the documented unrelated baseline diagnostics and no TDC-0306 file error.

### TDC-0307 Acceptance Summary

`TDC-0307` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One tenant-safe append-only supplier-performance scorecard using an exact approved/evidenced Published/effective `DEC-011` whose seven unique dimension weights total 100 percent and whose bands, performance window, minimum coverage, response target, minimum acceptable score, and typed eligibility action are fully validated; absent or invalid production values fail closed as a visible release gate.
- Server-derived promised-versus-actual receipt delivery, GRN inspection/acceptance/rejection quality, source-estimate-to-PO price, RFQ and quality-response timing, complaint resolution, and contract-completion observations. Missing dimensions remain unavailable, available evidence is coverage-normalized, and exact policy/supplier/risk/source snapshots plus deterministic SHA-256 hashes make every score explainable.
- Central supplier eligibility and supplier-risk composition, authorization, correlation idempotency, shared notifications, immutable `DEC-001` through `DEC-014` events, authenticated tenant-safe `/api/procurement/supplier-performance-scorecards` summary/history/detail/current/options/calculate routes, external denial, and the dedicated `/administration/procurement/supplier-performance` history-first UI plus eligibility performance card. No client-entered score path or new parallel workflow/evidence control was introduced.
- Migration `20260727005601_AddProcurementSupplierPerformanceScorecards`, one protected table and enabled trigger, exact rollback/reapply, append-only rejection `51900`, tenant/policy/risk-lineage rejection `51901`, consistency rejection `51902`, trusted foreign keys, no model drift, zero acceptance residue, and unchanged protected PR/PO/receiving/inventory totals.
- Focused/adjacent 25/25 Core, 6/6 API, and 9/9 frontend automation; zero-error Core/Data/API builds; clean targeted ESLint; successful optimized Next build; bounded authenticated API smoke with zero 5xx/mutations; real-browser smoke with 13 API requests and zero page/server/console errors; one visually reviewed screenshot; and exact process/port cleanup. Repository-wide TypeScript retains only documented unrelated baseline diagnostics and no TDC-0307 file error.

### TDC-0308 Acceptance Summary

`TDC-0308` is `Done`. The authoritative code and exact evidence are recorded in the tracker. The accepted slice includes:

- One supplier-specific composition of the accepted `TDC-0007` platform across profile, bank, tax, beneficial ownership, category assignment, and compliance/status families. Persisted enum compatibility is retained, the registry now exposes twelve total resources and exactly six supplier resources, and no parallel workflow/evidence/change-request implementation was introduced.
- Exact typed field whitelists and authoritative current-tenant adapters: ownership JSON requires unique named owners whose percentages total exactly 100 percent; parent, verification date, active category, compliance-period, blacklist-evidence, registration/approval/active/blacklist, and risk inputs are validated server-side. Category replacement is atomic on the tracked supplier aggregate with deterministic primary assignment.
- The existing Draft/submit/revalidate/independent approve-or-reject/effective apply/cancel, row-version, maker/checker SOD, current-state hash revalidation, immutable before/proposed/applied snapshots and SHA-256 hashes, shared workflow/evidence, and direct-mutation decision remain the one lifecycle. Events retain `DEC-001` through `DEC-014`; lifecycle notifications are supplier-scoped for supplier resources and remain generic for other protected masters.
- Direct business-partner create/update/delete checks all six supplier families, and suspend/activate checks the compliance family. The authenticated tenant-safe `/api/procurement/master-data-changes` API is reused. `/administration/procurement/supplier-master-changes` provides a dedicated typed history-first shared-control UI, six-family metrics, immutable lineage, lifecycle actions, and controlled absent-policy state without raw JSON or client-derived scores.
- Migration `20260727101510_AddProcurementSupplierMasterChangeRollout` adds six controlled supplier columns and three SQL checks. Exact rollback/reapply, invalid ownership/compliance/blacklist probes, trusted relationships, enabled master-data triggers, no model drift, and unchanged PR/PO/receiving/inventory totals pass.
- Focused 12/12 Core, 7/7 API, and 12/12 frontend automation; zero-error Core/Data/API builds; clean targeted ESLint; successful optimized Next build; authenticated read-only API smoke; real-browser smoke with 15 API requests and zero page/browser-observed HTTP 5xx/console errors; one visually reviewed 1440×1100 screenshot; and exact process/port cleanup. Repository-wide TypeScript retains only documented unrelated baseline diagnostics and no TDC-0308 file error. The inherited FinanceSettings startup-seeder missing-column diagnostic already present during TDC-0307 remains outside this slice and did not affect TDC-0308 responses.

### TDC-0309 Final Verification Summary

`TDC-0309` is verified `Done`. The implementation and passing evidence include:

- Verified email or SMS applicant entry; one application-bound free/paid `TDC-0302` token; repeated restricted sessions while the application is active; payment-only restriction where applicable; and applicant-only application, document, submission, payment, status, and history access. Approved/Rejected applications close the token and sessions, and later token attempts are denied and audited.
- Approval provisions only configured identity/supplier roles, links the provisioned account as both the business partner's primary portal owner and active `BusinessPartnerUser`, sends a cryptographic one-time password through the verified channel, applies a configurable seven-day default expiry, and blocks every authenticated route except password change/logout until successful activation. Linkage is idempotently repaired on replay without replacing registration/approval audit actors. Resend and activation retry are controlled; legacy account-first mutations and admin review operations are internal-only.
- Tenant-safe API, separate applicant JWT middleware, shared registration/token/payment/Finance/evidence/workflow/notification/control-event reuse, all `DEC-001` through `DEC-014` lineage, and no plaintext token/password logging. Dedicated public applicant, restricted portal, temporary-password, and administration shared-control routes cover loading, empty, error, payment, terminal, and status states.
- Applicant uploads now compose `IControlledFileUploadService`, checksum retention, and fail-closed virus scanning before registration in the central DMS repository. Procurement stores central document/version identifiers and a logical compatibility URI while physical storage paths remain private and are redacted from API DTOs. Registration evidence must link an active, same-tenant, permitted-scan `FileUploadRecord` and matching active DMS record/version lineage; service checks and SQL trigger `TR_BusinessPartnerRegistrationDocuments_ControlledFileGuard` reject legacy controlled-file bypasses.
- Review, approve, reject, summary/history, resend, provision, and activation-retry paths enforce the accepted supplier permissions, tenant scope, and server-derived actor. External/unauthenticated, cross-tenant, generic-role-only, client-forged actor, and post-terminal attempts are denied.
- `Active` is the canonical approved operational registration state; the shared business-partner lifecycle policy also recognizes legacy `Approved` rows. Supplier validation, award readiness, tender-document portal ownership, bidder communications, and active/preferred partner queries use the same rule.
- Focused automation passes 23/23 Core lifecycle/authorization/document-control tests, 23/23 API upload/security/controller/password-activation tests, and 3/3 frontend client tests. These execute email and SMS contact, duplicate/repeated-session behavior, application/document/payment/submission, approval and rejection terminals, role/identity provisioning, notification failure/retry, credential activation, tenant isolation, and hard stops.
- Migration `20260727134547_HardenProcurementSupplierApplicantEvidence` applies, rolls back exactly to `20260727113854_AddProcurementSupplierApplicantAccess`, and reapplies. Valid same-tenant clean evidence passes; infected and cross-tenant evidence fail with SQL `51839`; both required indexes and the trigger are enabled; EF reports no pending model changes; acceptance residue is zero.
- The zero-error API build, focused frontend automation, previously verified real-browser applicant/admin surfaces, and controller-level HTTP behavior together cover the shared UI/API boundary. Exact owned build/test/EF processes are stopped and ports `5000`, `3000`, and `3015` are closed.

### TDC-0401 Acceptance Summary

`TDC-0401` is verified `Done`. The implementation and passing evidence include:

- One tenant-safe framework-agreement family/version lifecycle with exact Ready award source and centralized Contract-boundary supplier eligibility; governed category, item-price, call-off-authority, ceiling, currency, validity, document, extension, and status lineage; soft-delete-safe version allocation; immutable hashes/snapshots; future-effective replacement continuity; and no `TDC-0402` ceiling deduction.
- Explicit manage/approve/read authorization, external-user denial, server-derived actors, independent maker/checker decisions, shared workflow/evidence/notification/control-event reuse, correlation idempotency, stale row-version rejection, and atomic domain-plus-`DEC-001` through `DEC-014` audit commits.
- Central-DMS registration of only same-tenant clean controlled uploads, private non-executable downloads, durable compensation on failure, and no second file store, workflow, evidence, or audit implementation.
- Authenticated `/api/procurement/framework-agreements` history/detail/summary/options/lifecycle/document/extension surface and dedicated `/procurement/framework-agreements` history-first UI with loading, empty, error, validation, status, timeline, document, extension, and explicit `TDC-0402` boundary states.
- Migration `20260729084856_TDC0401FrameworkAgreements` with six tables, six triggers, six checks, 36 indexes, 24 trusted foreign keys, and 11 passing SQL hard stops with zero residue. At explicit user direction, five malformed Finance migration artifacts were repaired and the entire 23-migration backlog was applied to user-secret `.\SQL2017` / `RhemaERP`; history is 176 rows and repeat EF update is current.
- Focused Core `7/7`, API `4/4`, and frontend `6/6` tests; clean focused builds/lint; successful optimized Next and Release API builds; tenant-safe API smoke with no 5xx or mutation; real Chrome smoke with 16 API requests and zero page/server/console errors; visually reviewed 1440×1100 capture; and closed ports `5000`/`3016`.

### TDC-0402 Acceptance Summary

`TDC-0402` is verified `Done`. The implementation and passing evidence include:

- One tenant-safe framework call-off aggregate creates one linked `FrameworkCallOff` PO from an exact currently effective Published agreement and approved requisition. The server derives the supplier, currency, immutable item price/UOM, source demand allocation, required-date validity, current actor authority, threshold, effective extension end, and available ceiling; callers can supply only source selections, quantity, delivery data, notes, and retained evidence.
- Centralized `SupplierEligibilityBoundary.FrameworkCallOff`, internal read/manage/approve authorization, role/user/organizational-unit/permission authority matching, row-version concurrency, serializable transactions, correlation idempotency, immutable SHA-256 snapshots, shared notifications/evidence, and atomic `DEC-001` through `DEC-014` control events are composed without recreating workflow or evidence controls.
- The exact active Published `TDC Purchase Order Approval` workflow is required. Submission revalidates current server state, final shared-workflow approval posts one immutable commitment and deducts availability atomically, issue is a separate controlled transition/movement, and cancellation of an approved unissued call-off releases the commitment. Approved and rejected call-off outcomes cannot contradict the shared workflow.
- Generic PO create rejects `OrderType=FrameworkCallOff`, and generic PO update/status/approve/submit reject an already linked call-off. SQL independently protects source/commercial/authority lineage, exact price/demand/PO lines, cross-call-off allocation, lifecycle/workflow outcome, balance reconciliation, append-only movements, and linked PO/status parity. Ordinary PO, receiving, and inventory behavior remains outside this task.
- Authenticated `/api/procurement/framework-call-offs` summary/history/detail/options/create/submit/decision/issue/cancel/expiry surface returns structured `403`/`404`/`409`/`422` outcomes. `/procurement/framework-call-offs` is a dedicated permission-aware history-first shared control with summary/balance cards, loading/empty/error/readiness states, server-owned allowed actions, controlled create and lifecycle dialogs, immutable line/price/source/workflow/hash detail, and movement history.
- Migration `20260729124416_TDC0402FrameworkCallOffs` adds four tables and six enabled triggers. User-secret SQL2017 `RhemaERP` has 177 migration rows, repeat apply is current, EF reports no pending model change, target constraints are trusted, and zero probe residue remains. Rollback-only SQL verifies a valid Draft and complete approval/commitment/separate-issue lifecycle plus immutable call-off line, linked PO commercial, linked PO line, append-only movement, ledger reconciliation, and demand-allocation hard stops.
- Focused Core `9/9`, API `3/3`, and frontend `12/12` tests pass; targeted ESLint is clean; final Core/Data/API focused builds and the optimized Next build pass with the dedicated route at 13.3 kB/180 kB. Repository TypeScript attribution contains only the recorded unrelated supplier-AVL icon, evidence-pack pagination/testing-library, session-timeout, and duplicate API/CRM diagnostics; no TDC-0402 file is implicated.
- Configured-database API smoke rejects anonymous access, authenticates the `DEFAULT` administrator, returns tenant-safe summary/history/options, exposes the intentionally absent production PO workflow and framework data as a fail-closed readiness state, returns structured missing/source-less/generic-bypass failures, records no 5xx and no mutation, and processes zero expiry alerts. Real Chrome renders the register and controlled-create dialog, observes 13 API requests, and records zero page/server/console errors. The 1440×1100 screenshot was reviewed at original resolution with no clipping or overlap; owned API/Next processes stopped and ports `5000`/`3016` are closed.

### TDC-0403 Acceptance Summary

`TDC-0403` is verified `Done`. The implementation and passing evidence include:

- One central tenant-safe source service permits exact RFQ award, tender award/contract, or approved-exception sources for ordinary POs and keeps framework orders on the dedicated call-off route. It resolves the authoritative approved requisition release, sourcing case, award/readiness/contract or exception, and supplier rather than trusting client lineage.
- RFQ awards persist their final `Awarded` status and authoritative award lines before the immutable PO-source snapshot is resolved, so immediate and later revalidation hash the same final source. One-time RFQ, tender, and approved-exception orders must exactly match the authoritative item, UOM, quantity, price, line value, currency, and total; contract orders may be partial, but the cumulative non-deleted/non-cancelled/non-rejected POs cannot exceed any approved line or the contract value. The generic create/update, plan conversion, RFQ award, and tender-award paths all compose this central commercial validation and reservation.
- RFQ, tender, and approved-exception awards are consumed once per tenant/source/supplier under a transaction-scoped application lock, and the soft-delete-inclusive service lookup matches an unfiltered unique database index. Contract orders use one tenant/contract transaction lock before cumulative capacity is read and reserved. A concurrent, repeated, soft-delete-reuse, or over-capacity attempt therefore fails closed and rolls the PO back, while revalidation permits only the PO that already owns its reservation.
- Generic/manual creation, legacy PR handoff, procurement-plan conversion, RFQ award, tender award, update, submit, approve, and status transitions compose the same source control. The PO persists an immutable source/requisition/sourcing-case/readiness/supplier snapshot and SHA-256 integrity hash and revalidates current authority before progression.
- Existing workflow, evidence, supplier eligibility, notification, authorization, concurrency, and `DEC-001` through `DEC-014` control-event services are reused. Denied validation and successful binding/revalidation are audited; no parallel workflow, evidence, or document controls were created.
- `GET /api/procurement/purchase-orders/source-options`, source fields on PO list/detail responses, and source-first `/procurement/purchase-orders/new` expose tenant-safe fail-closed readiness. The UI disables create actions without an approved source and directs framework procurement to its dedicated route.
- Migrations `20260729153000_TDC0403MandatoryPurchaseOrderSources`, `20260729171500_TDC0403SourceCapacityEnforcement`, and corrective `20260729210000_TDC0403FrameworkLineageFailClosed` are applied to user-secret `.\SQL2017` / `RhemaERP`; history is 180 rows and model-diff operations are zero. Nine source columns, the source indexes/foreign keys/check/triggers, a valid historical backfill, and the exact unique one-time-source index are verified; duplicate one-time groups are zero and transaction-owned application locking succeeds. The correction also rejects an active contract before its start or after its end and makes the framework call-off trigger fail closed when PO/agreement/readiness/sourcing joins are absent or mismatched.
- SQL hard stops reject historical lifecycle re-entry (`51204`), source mutation (`51203`), source-less insertion (`547`), new historical source (`51201`), forged/non-authoritative source (`547`), and mismatched-but-valid framework sourcing lineage (`51212`) with zero residue. Existing historical POs cannot enter a new approval lifecycle.
- Focused Core source tests pass 33/33, including explicit one-time classification, exact item/UOM/line-set/quantity/price/total, currency, partial-contract, and cumulative line/value excess hard stops; frontend source tests pass 2/2, targeted ESLint and diff checks are clean, and focused Core/Data/API plus optimized Next builds pass. API smoke returns expected `401`/`200`/`422`/`409` outcomes with no mutation or 5xx. Real Chrome observes 14 API requests, the mandatory source and fail-closed states, disabled source-less create actions, and zero page/server/console errors.
- Protected totals remain `PurchaseOrders=1`, `PurchaseOrderReceipts=1`, `StockMovements=0`, and `InventoryMovements=0`. Owned verification listeners are stopped and ports `5000`/`3016` are closed; receiving and inventory runtime behavior remains unchanged.

### TDC-0404 Acceptance Summary

`TDC-0404` is verified `Done`. The implementation and passing evidence include:

- One tenant-safe ordered decision evaluates approved source, supplier eligibility, approved budget, budget commitment/exposure, evaluation evidence, award approval, award SOD, applicable GHANEPS award exchange, required contract, and contract signatures from current server state. Conditional contract/signature checks are explicitly marked not required rather than silently omitted.
- `GET /api/PurchaseOrders/{id}/compliance-readiness?action=Preview` is authenticated, tenant scoped, read authorized, side-effect free, and returns the exact ten checks plus `DEC-001` through `DEC-014`. Submit and positive Approve enforce their existing create/approve authorization; missing, forbidden, and blocked outcomes are structured.
- Explicit PO submit/approval, matching generic status transitions, and framework-call-off submit/final-positive-decision/auto-approval paths compose the same service. Workflow assignment is checked before a positive approval result is recorded. Rejection and later receiving/fulfilment/inventory transitions retain their previous behavior.
- Each enforced result records one immutable `PO-003`/`TDC-0404` shared control event with all fourteen decisions, evidence references, actor, tenant, correlation, and allowed/blocked notification. Existing workflow, evidence, supplier, budget, source, award, GHANEPS, notification, and audit controls are reused rather than recreated.
- `PurchaseOrderComplianceGate` is one shared component on PO detail and framework call-off pages. It renders loading, error/retry, ready, blocked, and not-required states and exact references; only forward Submit and positive Approve/commit actions are disabled while rejection and unrelated actions remain available.
- Focused Core rules pass 20/20, service enforcement/audit/tenant isolation pass 2/2, current GHANEPS projection passes 2/2, and adjacent frontend suites pass 10/10. Targeted ESLint and diff checks are clean; Core/API Release and optimized Next builds pass with zero errors. No TDC-0404 file appears in the repository's retained unrelated TypeScript diagnostics.
- No schema change was required. EF reports no pending model changes, repeat apply against user-secret `.\SQL2017` / `RhemaERP` is current at 179 migrations, and protected totals remain `PurchaseOrders=1`, `PurchaseOrderReceipts=1`, `StockMovements=0`, and `InventoryMovements=0`.
- API smoke verifies `401`, responsibility-based `403`, tenant-safe `200` Preview with ten checks/fourteen decisions, missing `404`, progression `403`, eight fail-closed blockers, no mutation, and no 5xx. Real Chrome observes 15 API requests, exact ten labels and two not-required states, and zero page/server/console errors; both 1440×1100 captures were visually reviewed. Owned listeners are stopped and ports `5000`/`3016` are closed.

### TDC-0405 Acceptance Summary

`TDC-0405` is verified `Done`. The implementation and passing evidence include:

- One central tenant-safe PO SOD service composes the existing `SOD-INITIATOR-APPROVER` and `SOD-PO-CREATOR-RECEIVER` guard, responsibility access control, workflow outcomes, TDC-0404 compliance gate, notifications, SOD attempted-bypass audit, and immutable control-event writer. No parallel workflow, evidence, audit, policy, or SOD store was created.
- Ordinary/manual, plan, RFQ, tender, and framework creation derive the authoritative source requester, PO requester, and immutable creator on the server. Client requester and receiver identities cannot override the actor context. Positive PO/call-off approval prohibits the source requester, PO requester, PO creator, and framework creator; the creator cannot confirm the primary receipt. Rejection remains available.
- Tender-award `AutoApprove`, generic PATCH to `Approved`, and ordinary/framework workflow outcomes that approve during submission return structured `PO_AUTOMATIC_APPROVAL_PROHIBITED` or `PO_DIRECT_APPROVAL_PROHIBITED` before business mutation. Transaction-owned workflow/call-off/PO/balance changes roll back before the durable SOD denial is recorded.
- `GET /api/PurchaseOrders/{id}/sod-readiness` is authenticated, tenant scoped, read authorized, and side-effect free. It returns the ordered independent-approval and independent-receipt checks, participant lineage, exact required control references, and `DEC-001` through `DEC-014`. Enforced outcomes record `PO-004` or `RCV-004`, rule version `TDC-0405`, actor/policy/participant/correlation lineage, and the existing allowed/blocked notification topics.
- `PurchaseOrderSodControl` is one dedicated shared component on PO detail and framework call-offs. It renders loading, error/retry, allowed, and restricted states separately from the compliance gate; only positive approval/commit and primary receipt actions consume its result, while rejection stays available.
- Focused SOD rules pass 8/8, central lifecycle tests pass 6/6, adjacent tender-award tests pass 2/2, PO API tests pass 2/2, framework API mappings pass 4/4, and five frontend suites pass 12/12. Targeted ESLint and diff checks are clean; Core, Data, final API Release, and optimized Next builds pass with zero errors.
- Migration `20260730020000_TDC0405PurchaseOrderSodHardStops` is applied to user-secret `.\SQL2017` / `RhemaERP`; history is 181, repeat apply is current, and EF reports no pending model changes. Both triggers are enabled. Rollback-only SQL returns `51260` for requester/creator approval and `51261` for creator receipt confirmation, restores the earlier source trigger, and leaves zero residue.
- API smoke verifies `401`, independent `200` with exactly two allowed checks/fourteen decisions, missing `404`, requester/direct-bypass `409`, and creator/spoofed-receiver `403`; it observes durable TDC-0405 control/audit evidence, zero mutation, and zero 5xx. Real Chrome observes 16 API requests, the dedicated two-control SOD surface and separate compliance gate, and zero page/server/console errors. Full-page and focused captures were visually reviewed. Temporary policy/responsibility/PO fixture values were restored exactly, protected totals remain `1/1/0/0/0`, owned processes are stopped, and ports `5000`/`3016`/`3017` are closed.

### TDC-0406 Acceptance Summary

`TDC-0406` is verified `Done`. The implementation and passing evidence include:

- One tenant-safe amendment aggregate captures an immutable current-PO snapshot, typed proposed revision, explicit field/item diffs, before/proposed/diff hashes, reason, scope, evidence, source/requisition/sourcing/readiness/supplier lineage, revision sequence, row-version concurrency, and idempotency. Historical/received POs, linked framework call-offs, stale state, foreign tenants, invalid commercial/source changes, and concurrent duplicate actions fail before mutation.
- Draft submission reuses the exact shared PO workflow and evidence controls. An independent positive decision revalidates the authoritative source, supplier, compliance gate, PO SOD, workflow outcome, current baseline hash, and proposed totals; applies the new PO revision and source/commercial snapshot atomically; and records the exact budget-commitment delta. Rejection does not mutate the PO, and no parallel workflow, evidence, supplier, notification, audit, or policy store was created.
- Signed redispatch requires channel, destination, dispatch/document reference, organization-signature evidence, dispatch evidence, and optional shared workflow evidence. The external endpoint resolves only the authenticated user's approved linked supplier, exposes only its dispatched revisions, and records one idempotent receipt/acceptance/dispute acknowledgement with immutable actor, evidence, dispatch, revision, and integrity lineage.
- Every request/submission/decision/application/dispatch/acknowledgement outcome records the existing immutable procurement control event with the correct `PO-005` or `PO-006` rule, `TDC-0406` version, exact `DEC-001` through `DEC-014`, actor/tenant/correlation, source/supplier/workflow/evidence/commitment references, and existing scoped notification topics. The authenticated internal API uses registered procurement-read and manage/approve permissions; the supplier API cannot accept a client-selected tenant or supplier.
- The PO detail has one dedicated history-first Amendments tab with loading/error/retry/empty/blocked/editor/diff/commitment/dispatch/status states. The separate external-portal route exposes signed revisions and acknowledgement controls under the linked supplier identity. Framework-call-off immutability and broader receiving/inventory behavior remain unchanged.
- Focused Core rules pass 22/22, service lifecycle/authorization/tenant/concurrency/idempotency tests pass 7/7, migration tests pass 3/3, API controller tests pass 3/3, and frontend service-contract tests pass 3/3. The complete automated happy path proves revision creation, shared-workflow reapproval, a `+10 GHS` commitment delta, compliance/SOD enforcement, signed dispatch, linked-supplier acknowledgement, final revision/status, and exact `PO-005`/`PO-006` all-fourteen-decision events. Targeted ESLint/diff checks, final API Release, and optimized Next builds pass.
- Migration `20260730032601_TDC0406PurchaseOrderAmendments` is applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to 183. Rollback/reapply/repeat apply succeeds, EF reports no pending model changes, all four tables and four protection triggers exist, the approved-source bridge marker exists, related foreign keys are trusted, and residue is zero. Rollback-only SQL rejects invalid draft, physical deletion, unreconciled commitment, unsigned/unapplied dispatch, and direct approved-source mutation with `51301`, `51300`, `51311`, `51321`, and `51203`.
- API smoke verifies anonymous `401`, authenticated tenant-safe history/overview `200`, unavailable create state on the retained received PO, missing `404`, internal denial on the supplier-only route `403`, exactly fourteen decision keys, no mutation, and no 5xx. Real Chrome renders the internal controlled-amendment/fail-closed workspace and the dedicated supplier route, observes 42 API calls including both amendment endpoints, confirms linked-supplier-only isolation, and reports zero page/server/console errors. Captures were visually reviewed, the reversible supplier link was removed, amendment residue is zero, and owned ports `5000`/`3018` are closed.

### TDC-0407 Acceptance Summary

`TDC-0407` is verified `Done`. The implementation and passing evidence include:

- One tenant-safe activation aggregate captures exact configuration profile/version, all fourteen decisions, authority policy/rule/workflow, award/latest Ready decision and hashes, GHANEPS mapping, performance-security requirement, immutable contract/readiness snapshots, evidence ledger, row-version/idempotency, actors, decisions, status, and integrity hash. Submission rechecks the current contract/open-request/evidence state inside one serializable transaction.
- Legal, Internal Audit, statutory authority, signed-contract, configured `DEC-004`/`DEC-008`, and conditional performance-security requirements accept only exact current verified shared-workflow or malware-clean central-DMS evidence. Unknown evidence keys return a structured validation failure; stale, deleted, expired, foreign, unsafe, or broken DMS lineage fails closed at final activation.
- Positive approval requires an actor independent from the contract creator and activation submitter and the exact shared-workflow outcome. Final activation reloads and revalidates contract, row version, configuration, award/readiness, authority/workflow, GHANEPS, signatures, performance security, and evidence inside the activation transaction before atomically activating the contract, recording the award state, activation history, and immutable `CON-006`/`TDC-0407` event. The legacy direct activation service and SQL route are blocked.
- Authenticated internal read/manage/approve capabilities, tenant isolation, structured `403/404/409/422` API outcomes, shared notifications, exact `DEC-001` through `DEC-014`, central controlled upload/DMS storage, and one dedicated contract-detail Approval & activation tab are verified without recreating workflow/evidence controls.
- Focused activation rules pass 22/22, activation migration tests 4/4, recovered TDC-0406 migration regression 3/3, API controller tests 2/2, frontend client tests 3/3, targeted ESLint and TypeScript, final API Release, and the optimized Next build pass.
- Migration `20260730132714_TDC0407ContractActivationGate` is applied to user-secret `.\SQL2017` / `RhemaERP`; 186 history rows are current, repeat apply and no-model-diff checks pass, four TDC-0407 triggers are enabled, two exact activation-context markers exist, and activation/evidence/contract/document residue remains zero.
- API smoke proves anonymous denial, authenticated tenant-safe history, structured missing overview/submit/decision/activate outcomes, no mutation, zero 5xx, and cleanup. Real Chrome proves the dedicated fail-closed activation surface, latest-readiness blocker, central-DMS evidence, and all fourteen decision badges with zero page/server/console errors; the 1440×1100 capture was visually reviewed and ports `5000`/`3019` are closed.

### TDC-0408 Acceptance Summary

`TDC-0408` is verified `Done`. The tenant-safe derived read layer composes active contract, milestone, linked PO/receipt, AP invoice/payment, project retention certificate, governed supplier scorecard/risk, activation/configuration/policy/workflow, access control, notification, and immutable control-event data without introducing schema or mutating those sources. Currency-separated spend, payment, retention, milestones, SLA/KPI, delay, expiry/renewal, and supplier risk are visible in the dedicated portfolio and contract-detail Operations surfaces.

Automated delay/payment/retention/expiry/performance/risk/currency prompts are explainable, daily idempotent, audited with `DEC-001` through `DEC-014`, and explicitly require the existing independent contract/Finance path. The read model never interprets unstructured clauses into a monetary value, auto-posts a penalty, releases retention/funds, or changes PR/PO/receiving/inventory behavior. Focused backend/frontend automation, zero-error Core/API Release builds, completed optimized Next artifact, user-secret database repeat apply/no model drift, live API smoke, real browser/visual smoke, and process cleanup pass.

### TDC-0409 Acceptance Summary

`TDC-0409` is verified `Done`. One tenant-safe immutable Works action/evidence ledger controls initial and final takeover, defect rectification, warranty/performance-security/retention release, dispute opening/resolution, termination, final account, and closeout. Submission and positive decision compose the effective configuration and all fourteen decisions, statutory authority and exact shared workflow, current Projects/Contract source state, current malware-clean central-DMS/shared-workflow evidence, independent approval, final revalidation, row-version/idempotency, notification, access/SOD, immutable hashes/history, and `CON-005`/`TDC-0409` control events. Monetary instructions never auto-post Finance.

Focused rules pass 29/29, direct lifecycle service tests 5/5, migration tests 3/3, controller tests 2/2, and frontend client tests 3/3. Core/Data/API Release, targeted lint/TypeScript, optimized Next, no-model-drift, configured database apply/repeat/schema checks, API smoke, browser/visual smoke, and exact process cleanup pass. Migration `20260730171500_TDC0409WorksCloseoutControl` is latest at 188 history rows; two tables, three enabled triggers, nine trusted checks, and zero action/evidence residue are verified. PR/PO/receiving/stock/inventory behavior is unchanged.

Every Phase 4 implementation task is now `Done`. Phase 4 must not be declared fully accepted until representative `E2E-003` Works-procurement UAT is completed and recorded; that release/UAT gate is distinct from the verified `TDC-0409` implementation. All nine bounded Phase 5 tasks, `TDC-0501` through `TDC-0509`, are verified `Done` locally. Phase 5 must not be declared fully accepted until representative `E2E-018` is completed and recorded; `TDC-0601` is the next eligible implementation task.

### TDC-0501 Acceptance Summary

`TDC-0501` is verified `Done` locally. The implementation and passing evidence include:

- One central tenant-safe receipt-source control composes the existing immutable approved ordinary/framework PO source and revalidates current source authority, receivable PO status, exact PO line/item/UOM, cumulative prior PO-receipt and standalone-GRN quantities, configured tolerance, and remaining capacity. Historical source-less POs remain visibly fail closed and cannot re-enter a new receipt lifecycle without remediation.
- PO receipt and GRN creation/revalidation, GRN inventory posting, inventory valuation, and landed-cost lineage use the same control. Server-owned snapshots preserve source, capacity, validation time, SHA-256 hashes, row version, correlation, and idempotency. Serializable execution-strategy transactions, warehouse-scoped access, shared notifications, and immutable `RCV-001`/`TDC-0501` events with `DEC-001` through `DEC-014` are reused; no parallel workflow, evidence, DMS, or audit control was created.
- `GET /api/PurchaseOrders/{id}/receipt-source-readiness` plus structured receipt/GRN problem responses are authenticated and tenant safe. The receive route uses one dedicated `ReceiptSourceControlCard` for authoritative source/capacity, loading/error/fail-closed status, exact line quantities, and all fourteen decision badges; client receipt submission cannot override server readiness or remaining quantity.
- Migration `20260731123000_TDC0501ReceiptSourceControl` is applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `193`. Five filtered unique indexes and six enabled triggers independently protect source/lineage, capacity, idempotency, and purchase-receipt inventory posting. Live rollback-only SQL probes pass `4/4` for missing governed source (`51102`), receipt-line mutation (`51111`), direct invoice inventory receipt (`51142`), and direct invoice stock receipt (`51152`).
- Focused service/rules/migration tests pass `22/22`; frontend client/component tests pass `5/5`; Core/Data/API builds, targeted ESLint, `git diff --check`, and the optimized Next build pass. API smoke has no 5xx or configured-data mutation. Real Chrome renders the dedicated control and historical-source fail-closed state with zero page/server/console errors; the visual capture is clean. Temporary responsibility/warehouse fixtures are restored, ports `5000`/`3021` are closed, and no owned background process remains.
- Broader receipt SOD, matching/payment, GRN/MRN documents, and unrelated inventory behavior remain later tasks. Representative Phase 4 `E2E-003` remains explicitly open.

### TDC-0502 Acceptance Summary

`TDC-0502` is verified `Done` locally. The implementation and passing evidence include:

- One tenant-safe inspection aggregate records immutable case, exact receipt-line quantities, controlled evidence references and append-only actions for quality hold/quarantine, accepted/rejected/pending disposition, formal rejection note, linked-supplier acknowledgement/dispute, return/replacement progression, replacement receipt and closure.
- Initialization and every positive transition compose the exact current `TDC-0501` governed source/capacity, Published configuration decisions, current receipt/PO/GRN state, shared workflow approval, malware-clean central-DMS/shared-workflow evidence, independent actor/SOD, authorization, concurrency/idempotency, notifications, hashes and immutable `RCV-002`/`TDC-0502` control events with `DEC-001` through `DEC-014`. Missing production configuration fails closed; no parallel workflow, evidence, DMS or audit control was created.
- Approved acceptance is the only path that projects exact accepted quantities to PO/GRN/stock, records supplier-performance observations and releases AP eligibility. Legacy direct GRN mutation is closed, already-posted linked GRNs remain idempotent, and SQL independently rejects direct acceptance or purchase-stock posting without the exact inspection context.
- Authenticated tenant-safe overview, initialize, save, submit, independent decision, supplier acknowledgement, resolution and close routes are exposed beneath `/api/PurchaseOrderReceipts/.../inspection-control`; the supplier queue is restricted to the exact linked supplier identity. One shared `ReceiptInspectionControl` powers the internal receipt Quality Inspection tab and `/external-portal/receipt-inspections` with loading/error/retry, action eligibility, quantities, workflow/evidence and all fourteen decision badges.
- Migration `20260731140000_TDC0502ReceiptInspectionClosure` is applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `194`. Four tables, 11 trusted checks, 13 trusted foreign keys, 16 secondary indexes and nine enabled triggers protect tenant/source parity, immutable history, exact transitions and acceptance/stock context. The rollback-only `51541` direct-acceptance probe passes and leaves all trigger state and data unchanged.
- Focused Core tests pass `23/23`, API tests `2/2`, and combined TDC-0501/TDC-0502 frontend tests `9/9`; Core/Data/API builds, scoped ESLint, optimized Next, configured database apply/schema checks, API smoke and real-browser/visual smoke pass. Cases, inventory movements and stock movements remain `0/0/0`; temporary smoke evidence is reviewed then removed, ports `5000`/`5092`/`3022` are closed, and no owned process remains.

### TDC-0503 Acceptance Summary

`TDC-0503` is verified `Done` locally. The implementation and passing evidence include:

- One typed nine-action extension of the existing tenant-safe `SOD-PO-CREATOR-RECEIVER` boundary protects PO-receipt and GRN creation, inspection initialize/save/submit, positive inspection approval, replacement-receipt confirmation, quality-hold closure, and GRN stock posting. It reuses server-owned PO creator lineage, warehouse responsibility, notifications, immutable audit, `RCV-004`/`TDC-0503`, and `DEC-001` through `DEC-014`; no parallel SOD, workflow, evidence, DMS, or audit control was created.
- Rejection, supplier acknowledgement/dispute, return, and cancellation routes remain available. Structured tenant-safe `403` mappings cover receipt-inspection and GRN boundaries, while the receipt-only shared control appears on PO receiving and the Quality Inspection tab with all nine actions and fourteen decisions.
- Migration `20260731153000_TDC0503ReceiptSodClosure` is applied and repeat-applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `195`. Four enabled SQL triggers protect GRN, positive inspection actions, purchase inventory movements, and GRN stock movements; rollback-only error `51561 RCV_GRN_SOD_BLOCKED` passes with exact restoration and zero residue.
- Focused SOD rules/services pass `26/26`, migration tests `3/3`, structured API tests `4/4`, and frontend tests `3/3`; zero-error Core/Data/API builds, scoped ESLint, optimized Next, configured-database schema checks, tenant-safe API smoke, and real-browser/visual smoke pass. Temporary fixtures and smoke artifacts are removed, ports `5000`/`3023` are closed, and no owned background process remains.

### TDC-0504 Acceptance Summary

`TDC-0504` is verified `Done` locally. The implementation and passing evidence include:

- PO-linked non-opening invoices are forced to three-way matching. Create/update invalidates stale lineage; submit and approve re-evaluate inside a tenant-scoped serializable transaction and application lock. Non-PO and controlled opening-balance invoices retain their existing paths.
- The evaluator uses exact tenant/supplier/currency/PO-line price, latest independently approved AP-eligible inspection quantities, and cumulative prior committed invoice quantities so one accepted receipt cannot be invoiced repeatedly. Separate Finance price and quantity tolerances are projected directly from the two required columns, normalized to the configured 0-100 range, and included in the immutable snapshot.
- Every decision binds the effective Published `TDC-PROCUREMENT` profile, complete `DEC-001` through `DEC-014`, SHA-256 source snapshot, exact evaluation time and immutable `AP-002` / `TDC-0504` control event. A variance is approval-ready only when a current independent `AP-006` / `TDC-0507` event has completed workflow, evidence, SOD, exact invoice/PO/snapshot and future expiry. TDC-0507 remains owner of exception request/approval/reporting; no workflow/evidence control was recreated.
- Authenticated read-only readiness and permission-protected re-evaluation APIs return tenant-safe structured outcomes; submit/approve return structured `422` hard stops. One dedicated `InvoiceThreeWayMatchControl` renders loading/error/retry, readiness, tolerances, configuration, checks, variances, exception boundary and fourteen decisions. PO-linked list actions route to review, and detail Submit/Approve remain disabled until the server reports ready.
- Migration `20260731170000_TDC0504MandatoryThreeWayMatch` is applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `196`. Eight invoice/settings columns, two trusted control-event foreign keys and two enabled triggers protect matching lineage, status transition, snapshot freshness, exact AP-002 evidence, the narrow AP-006 contract and non-draft line immutability. Rollback-only direct status mutation is rejected with `51601 AP_THREE_WAY_MATCH_APPROVAL_BLOCKED`.
- Focused rules pass `11/11`, migration tests `4/4`, API controller contracts `4/4`, and the shared frontend control `1/1`; scoped ESLint, `git diff --check`, and zero-error Core/Data/API builds pass. The broader Core test-project source build retains an unrelated Projects constructor mismatch, so the supported focused test switch supplies the exact TDC evidence without changing Projects.
- Authenticated API smoke returns real required/blocked readiness `200`, `1%/1%` tolerances, six checks and exactly fourteen decisions without audit mutation. Real Chrome uses the actual readiness API, displays the mandatory hard stop and disabled submission, and confirms the fail-closed missing-Published-configuration state. The disposable supplier/invoice/line fixture and control-event residue are zero; every owned process stops and ports `5000`/`3023`/`3024` are closed.

### TDC-0505 Acceptance Summary

`TDC-0505` is verified `Done` locally. The implementation and passing evidence include:

- One centralized tenant-safe `AP-003` payment-readiness evaluator composes payable invoice state/balance, the current persisted allowed `AP-002` / `TDC-0504` match event and exact snapshot, independently approved AP-eligible GRN inspection, and only the strict current independently approved `AP-006` / `TDC-0507` exception result. Missing matching/audit services or stale/missing state fail closed.
- Payment creation with allocations is atomic; direct allocation and supplier-advance application lock and revalidate inside serializable transactions; posting revalidates active allocation lineage. Payment batches persist an immutable exact invoice set, revalidate at selection, approval and processing, never discover additional supplier invoices dynamically, and mark failed generated payments terminally Failed.
- Existing Finance Read/Process/Approve permissions and the shared batch workflow/SOD remain authoritative. `TDC-0506` still owns the invoice-processor-versus-payment-approver identity conflict, and `TDC-0507` still owns exception request/approval/evidence/reporting; neither later task was implemented early and no parallel workflow/evidence/audit control was created.
- Every allowed or denied evaluation records immutable shared `AP-003` / `TDC-0505` control-event lineage. Positive allocations and batch selections persist the exact event, SHA-256 snapshot, evaluation time, effective configuration and `DEC-001` through `DEC-014` decision register.
- Authenticated tenant-safe readiness and payment/batch APIs use explicit Finance policies and structured `404`/`422` hard stops. One dedicated `VendorPaymentReadinessControl` on the payment-create route renders loading/error/fail-closed state, ready/blocked counts, reasons/checks, later-task boundaries and fourteen decisions; blocked or missing readiness cannot be selected, prefilled or auto-allocated.
- Migration `20260731193000_TDC0505PaymentReadiness` is applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `197`. Allocation lineage, the exact `PaymentBatchInvoice` table, seven trusted named foreign keys, three checks and four enabled triggers protect allocation, exact selection, batch transitions and selection immutability. Rollback-only direct SQL is rejected with `51621` and `51623`, with zero residue.
- Focused Core readiness/migration tests pass `15/15`, API controller/authorization tests `4/4`, and frontend control tests `1/1`; the model snapshot, Core/Data/API compilation, scoped ESLint and `git diff --check` pass with zero errors. The repository-wide TypeScript check retains only attributed unrelated baseline errors outside all TDC-0505 files.
- API smoke proves anonymous `401`, ready and blocked tenant-safe `200` outcomes, missing `404`, the outstanding-invoice readiness projection, applicable server checks and fourteen decisions without 5xx. Real Chrome uses the actual frontend and API, renders the AP-003 control, invoice badge, later-task boundary and every decision with zero page/server/console errors; the visual capture is clean. Exact fixtures and business/control-event residue are zero, all owned workers stop, and ports `5000`/`3023`/`3024`/`3025` are closed.
- TDC-0506's later real batch smoke exposed a TDC-0505 partial-aggregate flush in the existing Finance batch creator. The batch/payment/item/exact-selection/AP-003 graph is now fully constructed before it is attached and saved; the dedicated regression passes `1/1`, and a real exact-invoice batch subsequently completed both shared Finance approvals. This is an in-place `VendorPaymentService` correction, not a parallel Finance path.
- PR, PO, receiving, stock and inventory runtime behavior remains unchanged. Representative Phase 4 `E2E-003` remains explicitly open.

### TDC-0506 Acceptance Summary

`TDC-0506` is verified `Done` locally. The implementation and passing evidence include:

- One tenant-safe `AP-004` control protects both manual VendorPayment authorization and exact-invoice PaymentBatch approval. Invoice processor identity is derived only from server-owned `VendorInvoice.SubmittedById`; the payment approver is the authenticated actor completing the existing shared Finance approval workflow. A same-user conflict fails before state mutation.
- The implementation extends the existing `IVendorPaymentService` / `VendorPaymentService`, `FinanceApprovalsController`, `IFinancePostingEngine`, Finance permission policies, shared workflow definitions/queue, required `SOD-INVOICE-PROCESSOR-PAYMENT` registry/guard, notifications and immutable procurement control events. It does not create a parallel payment, posting, approval, workflow, evidence, audit or SOD subsystem.
- Manual and batch authorization preserve the final independent actor through downstream processing/posting. Allowed and denied results retain exact invoice, processor, approver, workflow, effective policy/configuration, snapshot and `DEC-001` through `DEC-014` lineage. Tenant-safe readiness APIs and one dedicated `InvoicePaymentSodControl` expose that state without accepting client-supplied actor identity.
- Migration `20260731213000_TDC0506InvoiceProcessorPaymentSod` is applied once to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `198`. Enabled SQL triggers independently reject forged same-actor batch and manual-payment authorization with `51641` and `51642`. Active Published VendorPayment and PaymentBatch workflow version 2 definitions contain only Finance Manager then Financial Controller approval stages; obsolete system-seeded variants are Retired.
- Focused Core SOD rules/service/migration tests pass `7/7`; API SOD/readiness/batch-regression/workflow-seeding tests pass `12/12`; frontend TDC-0506 plus retained TDC-0505 controls pass `2/2`; scoped ESLint, `git diff --check`, and the final API Debug build pass with zero errors and eight retained baseline warnings.
- Real API smoke blocks the AP officer processor, permits Finance Manager first approval and Financial Controller final approval, authorizes a manual payment, and approves one immutable exact-invoice batch. Real Chrome uses the actual login, frontend and API to render both `SOD_ALLOWED` and `AP_PAYMENT_SOD_CONFLICT` views with exactly fourteen decisions and zero page/server/console errors; the visual capture is clean.
- All supplier, invoice, payment, batch, workflow and control-event fixtures are removed; the original policy is restored; protection triggers are enabled; temporary browser artifacts are deleted; and ports `5000`/`3023`/`3024`/`3025`/`3026`/`9226` are closed. PR, PO, receiving and inventory runtime behavior remains unchanged.

### TDC-0507 Acceptance Summary

`TDC-0507` is verified `Done` locally. The implementation and passing evidence include:

- One tenant-safe `AP-006` producer lifecycle owns request, two-stage independent Finance approval/rejection, requester/admin cancellation, expiry, corrective-action completion and immutable audit/report extraction. It binds the exact invoice, PO, current TDC-0504 matching snapshot, variance/tolerance, root cause, justification, corrective plan/owner/due date, effective Published configuration and complete `DEC-001` through `DEC-014` register.
- Serializable/idempotent mutation, row-version concurrency, server-derived actors and tenant, two distinct approvers independent from both requester and invoice processor, shared workflow state, and final source/evidence revalidation fail closed. Controlled evidence remains in central DMS or shared workflow evidence; final approval verifies exact entity linkage, document/version/path/size/scan hash and clean malware state.
- The existing `VendorInvoiceService` matcher and centralized TDC-0505 payment-readiness evaluator consume the approved AP-006 event. Finance remains authoritative for direct allocation, supplier-advance application, exact-invoice payment batches and posting through `IVendorPaymentService` / `VendorPaymentService` and `IFinancePostingEngine`; TDC-0507 contains no allocate, pay, authorize-payment or post action and introduces no parallel Finance path.
- Existing Finance View/Manage/Approve permissions, shared approval workflow, notifications, AP report service and immutable procurement control events are extended in place. One dedicated `InvoiceMatchExceptionControl` on invoice detail exposes loading/error/retry, request evidence, decision/cancel, expiry, corrective closure, history and fourteen decisions; AP reports expose tenant-safe extraction/export.
- Migration `20260731233000_TDC0507InvoiceMatchExceptions` is applied to user-secret `.\SQL2017` / `RhemaERP`, advancing history to `199`. Four tables, 17 indexes, four enabled triggers, zero untrusted checks/foreign keys, exact Finance Manager then Financial Controller workflow and zero exception residue are verified. EF reports no pending model change. The failed first apply rolled back cleanly; separating each SQL Server trigger into its own migration operation fixed the batch rule.
- Focused Core rules/migration tests pass `7/7`; API route/authorization/success/conflict contracts pass `7/7`; workflow seeding passes `1/1`; frontend control passes `1/1`; targeted ESLint and zero-error Core/Data/API builds pass. The broader Core test fixture was aligned to its already-changed Projects constructor without changing Projects runtime behavior. Repository-wide TypeScript retains only attributed unrelated baseline errors outside TDC-0507.
- Authenticated API smoke proves AP-006 reporting `200`, tenant-safe missing invoice `404 / AP_INVOICE_NOT_FOUND`, no allocation/posting projection and zero persisted exception fixture. Playwright Chromium logs in through the real frontend/API, renders the dedicated eligible-variance/evidence/dual-approval surface, and verifies no Allocate payment action (`1/1`, 6.6 seconds). Swagger/auth/TDC-0507 routes pass; unrelated health dependencies retain their baseline `503`. Every owned API/Next/browser process stops and ports `3000`/`5000` are closed.
- PR, PO, receiving, stock, inventory, payment-allocation and posting runtime behavior remains unchanged. Representative Phase 4 `E2E-003` remains explicitly open.

### TDC-0508 Acceptance Summary

`TDC-0508` is verified `Done` locally. The implementation and passing evidence include:

- Existing Finance ownership is preserved. `VendorInvoiceService` and `VendorPaymentService` now execute posted voids through the central `IFinancePostingEngine` under tenant-scoped serializable locks with stable idempotency, balanced journals, mandatory reasons, immutable success/failure audit and recovery-safe retries. Active invoice settlement blocks invoice reversal, while cleared/reconciled payments remain blocked until removed from bank reconciliation.
- Payment reversal restores exact invoice balances and produces append-only negative allocation history while reversing the authoritative payment, FX and supplier-advance-application posting events. The shared settlement read model excludes allocations whose payment is Voided or Failed, preventing voided payments from remaining settled. No alternate allocation, supplier-advance, ledger or posting path was introduced.
- One read-only AP-005 report composes authoritative Procurement commitments/POs/accepted receipts, Finance invoices/payments/allocations/posting events/journals/reversals/AP-control reconciliation, and Projects contracts/milestones/payment certificates/retention. Results are tenant-safe, purchase-order and as-of-date filterable, currency-separated, explainable, exportable as culture-stable CSV, and bind `AP-005`, `TDC-0508`, and exactly `DEC-001` through `DEC-014`.
- `GET /api/ap/reports/procurement-reconciliation` requires `RunFinanceReports`; `/export` requires `ExportFinanceReports`. Both use tenant-safe missing-resource behavior and immutable generated/exported audit events. One dedicated AP Reports shared control supplies loading/error/retry, filters, totals, exception rows, decisions and export, with no allocate, pay or post action.
- TDC-0508 changes no entity/configuration schema. User-secret `.\SQL2017` / `RhemaERP` remains current at 199 migrations through `20260731233000_TDC0507InvoiceMatchExceptions`; repeat apply reports current and SQL probes report zero untrusted checks/foreign keys. The first live report exposed an unrelated full `FinanceSettings` materialization against absent teammate-owned columns, so the existing AP-control query was safely narrowed to only its required AP/AR account IDs rather than adding Finance schema.
- The focused controlled-reversal/reconciliation set passes `5/5`; the combined invoice/payment/reconciliation/settlement/export regression passes `64/64`; final API and test-project builds have zero errors. Frontend service/component tests pass `3/3`, scoped ESLint is clean, and repository-wide TypeScript has zero TDC-0508 matches while retaining unrelated baseline errors.
- Authenticated configured-database smoke proves anonymous `401`, report/export `200`, AP-control variance zero, exactly fourteen decisions, missing-PO `404`, generated/exported audit records, and no 5xx. The configured demo PO is transparently flagged `COMMITMENT_MISSING`. Playwright Chromium uses the real login/frontend/API and passes `1/1` with a clean 1440x1100 visual review. All owned API/Next/browser workers stop and ports `3000`/`5000` are closed.
- PR, PO, receiving, inventory, workflow, evidence, allocation, supplier-advance, retention and general posting runtime behavior remains authoritative and otherwise unchanged. Representative Phase 4 `E2E-003` remains explicitly open.

### TDC-0509 Acceptance Summary

`TDC-0509` is verified `Done` locally. The implementation and passing evidence include:

- DEC-013 now has typed optional GRN/MRN number-format and central-template overrides, distinct combined-document validation, `{TYPE}` support, signature-role uniqueness and coherent applicability/coexistence rules. One receipt-document rules layer owns required-kind, evidence, inspection, transition and sequential-issuance checks.
- One tenant-safe aggregate persists each GRN/MRN register entry, configured signatures and append-only actions with exact receipt, PO, effective configuration profile/version, DEC-013 value, all fourteen decision keys, source snapshot/hash, correlation, actors, row version and central-DMS record/version lineage. Standalone creation uses EF's configured retry strategy and serializable transaction; caller-owned PO-receipt/GRN transactions remain atomic.
- Existing receipt source/capacity, independent inspection/acceptance and evidence, receipt SOD, document numbering, central-DMS generation templates/storage/records/versions, notification and immutable control-event services are composed in place. PO receipt and Inventory GRN creation initialize the same register; the legacy GRN PDF route delegates to it. No parallel receipt, inspection, workflow, evidence, DMS, numbering or inventory implementation was created.
- Authenticated `/api/ProcurementReceiptDocuments` overview/ensure/reconcile/sign/issue/cancel/download routes use explicit tenant predicates, external-user denial, capability authorization, row-version concurrency and structured `403`/`404`/`409`/`422` outcomes. Issuance requires approved/closed inspection, configured evidence/signatures and GRN-before-MRN ordering, then creates the PDF in central DMS. Cancellation retains DMS/history and reconciliation explains pending, issued, exception or cancelled state.
- Migration `20260801013000_TDC0509ReceiptDocumentLifecycle` adds three protected tables, three enabled append-only/lineage/lifecycle triggers, trusted checks and foreign keys, tenant TDC-GRN/TDC-MRN metadata/generation templates and `vw_ProcurementReceiptDocumentReconciliation`. Existing receipts are surfaced by the view without guessed historical documents. Direct deletion and forged DEC-013 lineage are rejected with `51670` and `51671`.
- The receipt-detail `GRN / MRN` tab mounts one dedicated `ReceiptDocumentControl` with loading/error/retry/empty states, source/inspection/document/evidence/DMS checks, effective profile version, `DEC-001` through `DEC-014`, ensure/reconcile, sign/issue/download/cancel actions, central-DMS lineage and immutable audit history. The API/service client tests pass `4/4` and scoped ESLint is clean.
- Focused Core rules/migration tests pass `14/14`; API controller tests pass `5/5`; the final bounded API build passes with zero errors and eight retained baseline warnings. Data Release/model validation and the previously completed no-pending-model check pass. User-secret `.\SQL2017` / `RhemaERP` is current at 200 migrations with three enabled triggers, zero untrusted checks/foreign keys, two active metadata templates, two active generation templates, one reconciliation row and zero receipt-document rows.
- Real API smoke proves anonymous `401`, administrator tenant login, overview `200`, exact profile ID/version, GRN-and-MRN applicability, six checks, fourteen decisions and missing receipt `404`. The obsolete acceptance template produces structured `422 RCV_DOCUMENT_TEMPLATE_MISSING` rather than 500 and persists no document. Playwright Chromium uses the real login/frontend/API and passes `1/1`; the reviewed 1440x1100 screenshot shows the complete shared control and immutable audit-history empty state. The temporary profile is restored exactly, all owned API/Next/build/browser processes stop, ports `3000`/`5000` are closed, and no shared compiler remains.

Every Phase 4 implementation task plus `TDC-0501` through `TDC-0509` are now `Done`. Phase 5 implementation is complete, while representative Phase 5 `E2E-018` and Phase 4 `E2E-003` remain explicit acceptance gates.

### TDC-0601 Acceptance Summary

`TDC-0601` is verified `Done` locally. The accepted boundary and evidence are:

- InventoryItem now owns normalized primary, alternate and QR identifiers; ItemUnitOfMeasure remains the UOM-barcode owner. One registered `IInventoryItemIdentifierService` supplies explicit tenant-scoped normalization, cross-slot/cross-table validation and primary/alternate/QR/UOM resolution. Ordinary InventoryItem create/update, repository search and the existing master-data maker-checker application path reuse this authority.
- Authenticated `InternalOnly` routes under `/api/inventory/item-identifiers` expose resolve, item UOMs, item/UOM identifier mutation, and spreadsheet-safe CSV import/export. Mutations require a real `tenant_id`, compose the existing direct-mutation maker-checker guard, write AuditLog rows atomically, validate the complete import before its serializable transaction, and return structured `409 INVENTORY_IDENTIFIER_DUPLICATE` conflicts.
- One dedicated `/inventory/item-identifiers` shared control provides item search, authenticated lookup, primary/alternate/QR maintenance, UOM barcode/conversion maintenance, CSV import/export and sidebar access. It deliberately contains no label designer, printing, camera/handheld scanner, offline queue or inventory transaction scan path; those remain owned by `TDC-0602` and `E2E-024`.
- Migration `20260801150000_TDC0601InventoryItemIdentifiers` is applied to user-secret SQL2017 `RhemaERP` as history row `207`. It adds the item identifier columns, tenant-scoped filtered uniqueness, normalization checks and enabled cross-slot/cross-table triggers. EF trigger metadata disables incompatible SQL Server OUTPUT writes. Both triggers are enabled, duplicate groups are zero, and rollback-only cross-slot bypass fails with `51662` and zero residue.
- Focused Core automation passes `8/8`, focused frontend client contracts pass `3/3`, changed frontend sources pass isolated TypeScript transpilation, and the final fast Debug API build has zero errors with retained baseline warnings. Repository-wide TypeScript/scoped ESLint retain only previously attributed unrelated diagnostics/startup delay outside the identifier files.
- Real authenticated API smoke proves export `200`, missing lookup `404`, normalized primary/alternate/QR and UOM persistence/resolution, duplicate `409`, CSV round-trip, audit persistence, and exact data restoration. Real headless Edge renders the actual route with `10/10` expected controls across `22` API responses and zero API/console errors. Disposable UOM/browser artifacts are removed and owned ports are closed.
- No workflow/evidence subsystem, barcode registry, scanner, label engine, PR/PO/receiving or stock-posting behavior was created or changed. `TDC-0602` is the next eligible task but remains `Not started`; Phase 4 `E2E-003`, Phase 5 `E2E-018`, Phase 6 `E2E-024`, and full Phase 6 acceptance remain open.

## Delivery Contract

A tracker task is `Done` only when all applicable parts of the tracker Delivery Rule are present and verified:

- Backend model and enforced business rules.
- Database migration and required backfill or tenant seeding.
- Tenant-safe API endpoints and authorization.
- Frontend route, controls, validation, loading/empty/error states, and status visibility.
- Shared workflow, evidence, notification, audit, and reporting behavior where required.
- Happy-path, hard-stop, authorization, tenant-isolation, concurrency, and direct-API tests.
- Migration applied to the test database and schema checked.
- Browser/API smoke verification.
- Tracker status, coverage matrix, and Verification Log updated with real evidence.

Do not mark a task complete because an entity, field, endpoint, or screen exists. Do not leave backend-only or UI-only slices described as complete.

## Engineering Guardrails

- Reuse existing module boundaries, repositories, `IUnitOfWork`, `ICurrentUserProvider`, tenant entities, service registration, API response conventions, shared controls, and workflow services.
- Keep the existing `ProcurementSettings` behavior backward compatible. Do not convert that mutable singleton directly into the new policy/versioning model in the first slice.
- Use typed DTOs and `System.Text.Json` serialization/validation for configurable decision payloads. Do not parse policy values with string splitting or ad hoc JSON traversal.
- Keep executable policy rules relational in `TDC-0002`; the `TDC-0001` decision register may store typed, schema-versioned decision snapshots and approval evidence without becoming the runtime rules engine.
- Enforce tenant isolation in queries and unique indexes. Never trust a tenant ID from the request body.
- Published and retired configuration versions are immutable. Changes are made by cloning a new draft.
- Use row-version concurrency for profile and editable-decision updates.
- Use the shared file-storage, evidence-validation, workflow, notification, and audit infrastructure. Do not create a second blob store, workflow engine, approval dialog, or checklist implementation.
- Never use JavaScript `prompt`, `confirm`, or `alert`. Use shared dialogs and audited confirmation actions.
- Prefer dedicated, history-first administration routes over adding more sections to the already large Purchase Order Settings page.
- Keep UI styling consistent with the current application: compact operational layouts, Lucide icons, responsive tables, proper select controls, toggles for booleans, and no nested cards.
- Every transaction hard stop must be enforced in the service/API layer and covered by direct-API tests. Hiding a button is not enforcement.
- Do not mix unrelated refactoring into a procurement slice.

## Existing Architecture To Reuse

### Backend Anchors

| Concern | Existing code anchor and instruction |
| --- | --- |
| Tenant/audit base | `src/ErpSystem.Core/Entities/BaseEntity.cs`; new persisted procurement configuration records derive from `TenantEntity`. |
| Current settings | `src/ErpSystem.Core/Entities/Procurement/ProcurementSettings.cs`, DTOs, service, repository, controller, and frontend service. Preserve this compatibility surface. |
| DI registration | `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`; repository registrations are grouped near line 275 and services near line 803. |
| EF context | `src/ErpSystem.Data/ApplicationDbContext.cs`; add DbSets and explicit model configuration/indexes consistently. |
| Version lifecycle | `WorkflowDefinition`, `WorkflowApprovalPolicySet`, and `WorkflowDefinitionServiceAdapter` demonstrate Draft/Published/Retired, clone, publish, retirement, and immutable history. Reuse the behavior, not workflow-specific entity coupling. |
| Lifecycle tests | `tests/ErpSystem.Core.Tests/Services/Workflow/WorkflowDefinitionLifecyclePolicyTests.cs` is the focused policy-test pattern. |
| Concurrency | Finance fixed-asset lease/capital-project entities demonstrate `[Timestamp] byte[] RowVersion`. |
| API tests | `tests/ErpSystem.Api.Tests/Controllers/Procurement/BusinessPartnersControllerContactRouteTests.cs` demonstrates the current `WebApplicationFactory` procurement route pattern. Extend it with real authorization-negative coverage instead of only permissive test handlers. |
| Shared workflow | `IWorkflowIntegrationService`, workflow status adapters, `WorkflowApprovalActions`, and `useWorkflowRecord` are mandatory integration points when approval execution is added. |
| Evidence/storage | `WorkflowEvidenceService`, `FileUploadController`, and `IFileStorageService` own evidence validation/storage behavior. Store references and business linkage, not duplicate file content. |
| Completed PR sourcing boundary | `ProcurementRequisitionSourcingReleaseService`, `ProcurementRequisitionSourcingReleaseStore`, the sourcing routes in `PurchaseRequisitionsController`, direct RFQ/tender/plan service guards, and `PurchaseRequisitionSourcingReleaseControl` are the verified `TDC-0107` boundary. Calendar work must not weaken, duplicate, or couple scheduling state into this release aggregate. |

### Frontend Anchors

| Concern | Existing code anchor and instruction |
| --- | --- |
| Current page | `frontend/src/app/administration/procurement/purchase-order-settings/page.tsx` is a compatibility/settings page, not the new policy-profile workbench. |
| Current service | `frontend/src/services/procurementSettingsService.ts` uses a legacy raw-fetch pattern. For new work, follow the shared authenticated API wrapper used by `workflow-api.service.ts` and nearby modern services. |

Shared administration anchors:

- Workflow administration: `frontend/src/app/administration/workflow/page.tsx` shows lifecycle/history administration patterns, but it currently has uncommitted team changes. Read its diff before reuse or edits.
- Shared controls: use existing `Button`, `Dialog`, `AlertDialog`, `Tabs`, `Select`, `Switch`, `Input`, `Textarea`, table/data-grid, badges, toast, loading, and empty-state components.
- Navigation: add the new route to the existing Administration/Procurement navigation group in `frontend/src/components/layout/sidebar.tsx` only after verifying the current group structure.

## Dependency-Safe Delivery Sequence

### Foundation Through Award

| Delivery wave | Tracker tasks | Outcome before the next wave |
| --- | --- | --- |
| A. Configuration lifecycle | `TDC-0001` | Typed decision register, draft/publish/retire lifecycle, evidence references, validation, history, admin UI, tests, and migration. No transaction behavior changes. |
| B. Executable policy model | `TDC-0002`, then `TDC-0003` | Relational methods/thresholds/authorities/evidence/exception/SOD rules plus one explainable central decision service. |
| C. Cross-cutting controls | `TDC-0006`, `TDC-0004`, `TDC-0005`, `TDC-0007` | Immutable control decisions, SOD hard stops, roles/workflows, and maker-checker reusable across later slices. |
| D. Planning and PR vertical flow | `TDC-0101` through `TDC-0108` | APP, specifications, budget, requisition, exception, workflow, and commitment gates work end to end without reopening delivered planning features. |
| E. Sourcing through award | `TDC-0201` through `TDC-0212` | Method selection, tendering, evaluation, committees, exceptions, award, and statutory evidence. |

### Supplier Through Deployment

| Delivery wave | Tracker tasks | Outcome before the next wave |
| --- | --- | --- |
| F. Supplier controls | `TDC-0301` through `TDC-0309` | Evidence packs, token-gated portal/registration, fees, due diligence, AVL, risk, performance, sanctions, controlled master changes, and approval-based account activation. |
| G. Frameworks/contracts/POs | `TDC-0401` through `TDC-0409` | Commitments, call-offs, amendments, dispatch, acknowledgements, and contract controls. |
| H. Receipt through payment | `TDC-0501` through `TDC-0509` | Receipt inspection, GRN/MRN, rejection, landed cost, three-way matching, exception approval, payment, and GL. |
| I. Inventory control | `TDC-0601` through `TDC-0616` | Stores, traceability, reservation, issue/return/transfer, counts, valuation, replenishment, barcode/mobile, disposal, and item master. |
| J. Assurance and deployment | Phases 7 through 9 | Reports, archive, integrations, migration, data quality, training, NFRs, cutover, and success measures. |

Do not combine multiple waves into one unreviewable change. Within a wave, deliver one coherent vertical slice at a time and keep the tracker current.

## Portfolio Continuity After Procurement

Procurement and Inventory is the first tracker-driven implementation programme. Complete this tracker fully before moving the active delivery programme to another module. Completion means all applicable roadmap tasks, end-to-end scenarios, integrations, migrations, permissions, UI, reports, UAT evidence, and release gates satisfy the tracker Delivery Rule; reaching the end of a phase or completing only P0 items is not enough.

After Procurement and Inventory is fully completed and certified, repeat the same controlled method using each module's own tracker as its authoritative delivery ledger:

- `docs/tdc-fleet-management-gap-implementation-tracker.md`.
- `docs/tdc-sales-marketing-crm-gap-implementation-tracker.md`.
- `docs/tdc-quantity-survey-gap-implementation-tracker.md`.
- `docs/tdc-civil-engineering-gap-implementation-tracker.md`.

For every later module, create a fresh module-specific implementation handover from the then-current repository state. Reuse shared platform capabilities such as workflow, evidence, notifications, audit, documents, identity, Finance, and master data, but never copy Procurement task IDs, statuses, policy rules, or assumptions into another module. Do not run two tracker programmes as one blended implementation unless the user explicitly approves a cross-module dependency slice.

## Historical First Slice Specification: TDC-0001

The following specification is retained as delivery history for the completed configuration foundation. It is not the next implementation instruction; current work resumes at `TDC-0301` under the live tracker row and Delivery Contract.

### Slice Name

Procurement Configuration Profile Lifecycle and Decision Register

### Slice Intent

Create the versioned, tenant-scoped administration foundation for `DEC-001` through `DEC-014`. TDC values may remain pending while development proceeds, but only approved, validated values with required evidence can be published. The slice records configuration decisions; it does not yet enforce them on PR, PO, receipt, payment, or stock transactions.

### Required Domain Model

Use names sympathetic to the repository, but preserve these responsibilities:

| Record | Required responsibility |
| --- | --- |
| `ProcurementConfigurationProfile` | Tenant, stable profile key, name, version, Draft/Published/Retired lifecycle, effective period, change summary, publication/retirement actors and dates, default flag, and row version. |
| `ProcurementConfigurationDecision` | Profile link, one allowed `DEC-*` key, schema version, owner, status, typed value JSON, decision/effective dates, approval/evidence state, source lineage, notes, and row version. |
| `ProcurementConfigurationEvidenceLink` | Decision/profile link to shared uploaded/evidence metadata, evidence type, file/reference identifier, checksum/reference metadata where available, uploader, and date. Do not duplicate file bytes. |
| `ProcurementConfigurationRevision` | Append-only before/after snapshot, actor, action, timestamp, correlation ID, source decision/profile, and reason. |

Use explicit unique indexes at minimum for profile family/version per tenant, a single published active profile per family/tenant, and one decision key per profile. Use check/validation rules for dates and lifecycle state. Tenant ID comes from the authenticated context.

### Decision Schema Registry

Implement an explicit registry for all 14 keys. Each entry must define its display metadata, owner group, schema version, typed request/value DTO, validator, and publication requirements.

| Decision | Minimum typed value shape |
| --- | --- |
| `DEC-001` | Procurement category/service class, method, currency, lower/upper bounds, inclusivity, effective period, and statutory reference. |
| `DEC-002` | Authority level, currency, bounds/inclusivity, escalation authority, category applicability, and effective period. |
| `DEC-003` | Transaction/entity type, policy selector, workflow definition reference, applicability conditions, and effective period. |
| `DEC-004` | Authority/committee/observer role, quorum, evidence, amount/category conditions, sequence/group, and escalation. |
| `DEC-005` | Petty threshold, waiver eligibility, justification/evidence, approver, expiry, and effective period. |
| `DEC-006` | Restricted/single-source prerequisite, approval authority, mandatory evidence checklist, filing reference, and expiry. |
| `DEC-007` | Fee type, amount/currency, tax, payment channel, receipt format, exemption/refund/renewal rules, and effective period. |
| `DEC-008` | Document type, allowed signature mode, signatory role/order, verification/evidence rules, and effective period. |
| `DEC-009` | GHANEPS profile, file/template/reference mapping, frequency, owner, acknowledgement/reconciliation rule, and effective period. |
| `DEC-010` | Negative-stock default, emergency-override eligibility, permission/workflow/evidence, duration, and audit requirement. |
| `DEC-011` | AVL review frequency, risk dimensions/bands, concentration limit, minimum score, resulting eligibility action, and effective period. |
| `DEC-012` | Cutover date, dual-run period, data owner, acceptance signatories, release status, and evidence. This is a deployment gate, not a runtime policy. |
| `DEC-013` | Receipt document type, GRN/MRN applicability, coexistence rule, number format, template, signature/evidence requirements, and effective period. |
| `DEC-014` | Workload scenario, availability/response target, backup/RPO/RTO, authentication, monitoring, usability/accessibility target, and acceptance method. |

Do not turn the registry into executable threshold routing yet. `TDC-0002` owns normalized runtime policy entities; `TDC-0003` owns rule resolution.

### Lifecycle Invariants

1. Only Draft profiles and Draft/Proposed decisions are editable.
2. Published and Retired profiles and their decision snapshots are immutable.
3. Publishing validates every required decision in the profile, typed payloads, owners, decision/effective dates, evidence, date overlaps, and version uniqueness.
4. Publishing and retirement of the previously published version occur atomically.
5. Clone-to-draft creates the next version, preserves lineage and values, and resets approvals for decisions that require renewed approval.
6. Runtime queries must eventually resolve only Published, effective records; first-slice APIs may expose this query but no transaction service may consume it yet.
7. Deleting Published/Retired history is prohibited. Draft deletion is allowed only when no workflow/evidence dependency prevents it and must be audited.
8. Row-version mismatch returns a conflict response, not last-write-wins behavior.
9. Cross-tenant IDs return not found/forbidden without leaking record existence.

### Backend Deliverables

- Add the new entities and enums under `src/ErpSystem.Core/Entities/Procurement` and `src/ErpSystem.Core/Enums` or the existing procurement enum location.
- Add typed DTOs and request contracts under `src/ErpSystem.Core/DTOs/Procurement`.
- Add lifecycle policy, decision registry/validators, service interface, and service implementation under the established Procurement namespaces.
- Add repositories only where repository-specific queries are needed; use existing generic repository conventions otherwise.
- Register repositories/services in `ServiceCollectionExtensions.cs`.
- Add DbSets and explicit EF configuration/indexes in `ApplicationDbContext` or the repository's established model-configuration location.
- Add a tenant-scoped controller under `src/ErpSystem.Api/Controllers/Procurement`.
- Add append-only audit/revision writes in the same unit-of-work operation as state changes.
- Use shared file/evidence services for uploads/references and reject unsupported or unsafe evidence through existing policy.

### API Contract

Use REST names consistent with the repository. The following capability set is required even if final route spelling changes:

| Method and route | Behavior |
| --- | --- |
| `GET /api/procurement/configuration-profiles` | Tenant-scoped paged list with lifecycle/search filters and decision completeness summary. |
| `GET /api/procurement/configuration-profiles/{id}` | Full profile, decision values, evidence summaries, validation state, and history summary. |
| `POST /api/procurement/configuration-profiles` | Create a Draft and seed the allowed `DEC-*` decision rows idempotently. |
| `PUT /api/procurement/configuration-profiles/{id}` | Update Draft metadata with row-version concurrency. |
| `PUT /api/procurement/configuration-profiles/{id}/decisions/{decisionKey}` | Validate and save one typed Draft decision. |
| `POST /api/procurement/configuration-profiles/{id}/validate` | Return structured errors/warnings by decision key without publishing. |
| `POST /api/procurement/configuration-profiles/{id}/publish` | Authorize, validate, atomically publish, retire prior version, and audit. |
| `POST /api/procurement/configuration-profiles/{id}/clone-draft` | Create the next Draft version with lineage and approval reset rules. |
| `GET /api/procurement/configuration-profiles/{id}/history` | Return immutable revision/activity history. |
| Evidence routes | Link/unlink shared evidence only while Draft, with authorization, malware/file-policy validation, and audit. |

Return structured validation problems. Do not catch every exception and flatten it into an untyped 500 string. Never combine `[AllowAnonymous]` and `[Authorize]`; the existing settings check endpoints contain that contradictory pattern and must not be copied.

### Authorization And Tenant Rules

- Begin with the narrowest existing safe administration boundary, normally `SuperAdmin` and `TenantAdmin`, while `TDC-0005` introduces approved TDC procurement permissions.
- Add the authenticated user's tenant and user IDs server-side.
- Read/list/detail APIs are tenant-scoped; create/update/publish/clone/evidence actions require explicit authorization.
- Publish must require a separate privileged action from ordinary draft editing.
- Add negative controller/service tests for unauthenticated, unauthorized role, cross-tenant ID, and direct publish attempts.

### Frontend Deliverables

Create a dedicated administration workspace rather than extending the current Purchase Order Settings page:

- List route: `/administration/procurement/policy-profiles`.
- Detail/editor route: `/administration/procurement/policy-profiles/[id]`.
- History-first list with search, lifecycle filter, version/effective status, completeness, updated actor/date, and row actions.
- Detail tabs for Overview, Decisions, Validation, Evidence, and History.
- Decision forms rendered from an explicit typed registry, using selects for enumerations/owners, date controls for effective periods, amount/currency controls, toggles for booleans, and shared evidence upload controls.
- Shared `Dialog` for create/clone and shared `AlertDialog` for publish/retire/delete confirmations. No browser prompts.
- Clear loading, empty, error, validation, conflict, immutable, and success states.
- Compact responsive design consistent with the current administration pages; avoid cards inside cards and avoid one giant form.
- Add navigation only after route and authorization are working.
- Use the shared authenticated API service rather than duplicating local-storage token/header code.

### Migration And Seed Plan

1. Generate one focused EF Core migration after the model is stable.
2. Inspect the generated migration and SQL for only the intended profile/decision/evidence/revision tables, indexes, foreign keys, row versions, and seed/backfill behavior.
3. Seed the 14 decision definitions in code through an idempotent registry; do not store display metadata as duplicated tenant rows unless required.
4. Create one Draft TDC profile per existing tenant through an idempotent backfill/seeder. Do not publish guessed values.
5. Ensure newly created tenants receive the same Draft initialization.
6. Apply the migration to the configured test `RhemaERP` database only after reviewing the active connection source and keeping secrets out of logs.
7. Verify migration history, table/index presence, tenant counts, profile version uniqueness, and that no existing procurement settings or transaction rows changed.

### Required Tests For TDC-0001

| Test area | Minimum cases |
| --- | --- |
| Lifecycle policy | Draft editable; Published/Retired immutable; clone increments version; delete restrictions; published runtime eligibility. |
| Registry/validation | All `DEC-001` through `DEC-014` registered; unknown key rejected; schema version and typed payload validation; effective-date validation. |
| Publication | Pending/invalid/missing-evidence decisions block publish; valid profile publishes; previous version retires atomically; publish is idempotent or safely rejected. |
| Tenant isolation | List/detail/update/clone/publish cannot cross tenant; unique indexes include tenant. |
| Authorization | Anonymous and non-admin mutation attempts fail; direct API publish bypass fails. |
| Concurrency | Stale row version returns conflict and preserves the newer edit. |
| Audit/history | Create, edit, evidence link, validate, publish, retire, clone, rejected bypass, and delete actions record actor/result/correlation and before/after where applicable. |
| Seeding/migration | Existing tenant receives one Draft with all 14 keys; rerun creates no duplicates; existing procurement data remains unchanged. |
| Frontend | Service mapping and principal forms/components have focused Vitest coverage where repository patterns support it; targeted ESLint/type-check are clean. |

### TDC-0001 Definition Of Done

`TDC-0001` may be changed to `Done` only after all of the following are true:

- All 14 decision schemas are represented and validated.
- Draft/publish/retire/clone lifecycle works and Published/Retired data is immutable.
- Required owner/date/effective/evidence/publication protections are enforced server-side.
- Tenant-safe API and dedicated administration UI are complete.
- Migration and idempotent tenant initialization are applied and verified.
- Authorization, tenant-isolation, concurrency, lifecycle, audit, and bypass tests pass.
- Backend build, focused tests, targeted frontend lint/type-check, API smoke, and browser smoke pass.
- Existing procurement behavior is unchanged.
- Tracker task, coverage matrix, code anchors, and Verification Log contain exact evidence.

If any applicable item is incomplete, keep the active tracker task `In progress`; do not create a misleading `Done` state.

## Historical PR 16 Integrity Checkpoint

- Forward migration `20260728210856_EnforceSupplierApplicantIntegrity` is the current SQL2017 `RhemaERP` test-database checkpoint after `20260728192617_AddDurableFileStorageCleanup`.
- Shared controlled-file deletion refuses active supplier-registration evidence references in the service, and the enabled SQL trigger prevents direct/concurrent soft-delete bypass.
- Verified application creation commits registration, onboarding token, applicant access, and control-event lineage through one serializable caller-owned transaction; the shared token service joins that transaction.
- The filtered unique active-contact index covers access statuses `0`, `1`, `2`, and `5`, releases the contact only after `Activated` or `Rejected`, and fails migration when legacy duplicate active contacts exist.
- Focused evidence is applicant lifecycle `6/6`, token/payment lifecycle `10/10`, controlled upload/model `20/20`, zero-error Release API build, no pending EF model changes, enabled SQL gates, and rollback-only rejection probes with zero residue.
- Bearer-authenticated `/api` requests are network-only in the production service worker, the cache version invalidates preceding runtime data, and the API independently returns `private, no-store`, `no-cache`, and `Vary: Authorization` after downstream processing. Focused cache/applicant evidence passes frontend `7/7` and API `13/13`.
- Approval activation retry can recover the exact unbound partial Identity user left by a post-`CreateAsync` provisioning failure only when tenant, verified contact, applicant attributes, original approver/time, temporary-password state, configured-role subset, and absence of applicant/tenant/business-partner links all match. It reconciles missing roles and links, records `SupplierAccountProvisioningResumed`, and continues to reject unrelated or already-linked accounts. Focused lifecycle recovery passes `8/8`; lifecycle plus authorization passes `14/14`.
- Supplier compliance master changes now hydrate the selected tenant-safe supplier's authoritative lifecycle, blacklist, risk, and compliance state before editing and submit only fields explicitly changed by the operator. A notes-only change cannot silently reactivate, unblacklist, or reset the supplier, while deliberate nullable-field clearing remains supported.
- The shared controlled-upload boundary now registers a working fail-closed ClamAV provider by default, streams bytes through clamd `INSTREAM`, reports readiness through the existing health endpoint, and preserves a host-provided scanner registration. Development and production Compose keep clamd on the private application network with persistent signature data; no scanner port is published.
- Focused review-correction evidence is supplier patch automation `8/8`, ClamAV protocol/registration automation `4/4`, adjacent controlled-upload security `20/20`, zero-error Release compilation, targeted frontend ESLint and Prettier, and valid development/production Compose configuration. Repository TypeScript reports only recorded unrelated baseline errors and no changed-file diagnostic. No persisted model changed, so no migration or test-database apply is required.
- All four production API containers now mount the same `erp-uploads` volume at the default local-storage path, and the image creates that mount point before switching to its non-root user. Node-local absence is therefore authoritative within the single-host Compose topology instead of allowing one cleanup worker to finalize deletion while an object survives on another API node.
- A payment-only applicant session now receives structured `409 SUPPLIER_APPLICANT_PAYMENT_REQUIRED` before supplier-document lookup or mutation, so retained Draft or MoreInfoRequired evidence cannot be deleted until payment or exemption verification succeeds. Focused lifecycle and deployment-contract automation passes `10/10`, and production Compose validates.
- Latest `origin/master` DMS commit `792d8f28` is merged locally as unpushed commit `dd44dcce`. Supplier evidence now registers a clean controlled upload as a `CentralDocumentRecord`/`CentralDocumentVersion`, persists only the DMS identifiers plus a logical compatibility URI in procurement, validates exact tenant/uploader/source lineage, and deletes or opens content through the shared DMS repository-file boundary. Internal evidence access requires `procurement.supplier.review`; applicant access uses a dedicated `SupplierApplicantOnly` download bound to the exact active registration session. Legacy physical paths are redacted, and the old public `/uploads/supplier-registration-evidence` path is blocked before static-file handling.
- DMS manual uploads now use the same centralized controlled-upload and fail-closed malware-scanning boundary as supplier evidence. `document-management`, `central-dms`, and supplier-evidence content resolve beneath private `secure-file-storage`; all four production API services mount the shared `erp-secure-files` volume, and the API image prepares the mount point for its non-root runtime user.
- Migration `20260729022744_AddSupplierEvidenceDmsLinks` adds the nullable central record/version references, restrictive foreign keys, and tenant-aware indexes. EF reports no model drift. At this historical checkpoint, the exact migration was applied to disposable LocalDB `ErpSystemDmsMigrationTest_20cc1f4a`; two columns, two foreign keys, four indexes, and the history row were verified before removing the scratch database.
- Post-merge evidence is zero-error Core/Data/API/test-project compilation, focused API tests `45/45`, and focused Core document-control tests `5/5`. A full Core test-project build still has an unrelated incoming-`master` `ProjectServiceTests` compile failure caused by its unadapted `IEstateManagedAssetService` constructor dependency. No PR/PO/receiving/inventory runtime or shared workflow/evidence control changed.
- This checkpoint's migration-backlog restriction was superseded on 2026-07-29 by the user's explicit instruction to apply all 23 pending migrations. Five malformed Finance artifacts were corrected, the complete set was applied to configured `RhemaERP`, and repeat EF update now reports current; see the `TDC-0401` Verification Log for current evidence.

## Historical TDC-0210 Start Procedure

1. Re-run `git status --short --branch`, upstream/divergence checks, `git diff --check`, and the diff for every already modified file the slice may touch.
2. Read `SRC-007` through `SRC-010`, `TDC-0210`, `PROC-007`, `E2E-002`, `E2E-004`, `E2E-014`, the Delivery Rule, Tracker Maintenance, and completed `TDC-0204` through `TDC-0209` evidence before changing award behavior.
3. Preserve the pushed through-`TDC-0206` baseline, completed uncommitted `TDC-0207` through `TDC-0209`, and every team-owned dirty/untracked file; do not commit `full_database.sql` or unrelated module trackers.
4. Reverify the accepted 239 Core procurement, 149 API procurement-controller, and 120 frontend tests. Confirm `RhemaERP` remains current through `20260724020403_AddProcurementAwardReadinessControls`, readiness/committee guards are enabled/trusted, readiness decisions remain empty, and protected transaction counts have not drifted.
5. Change only `TDC-0210` from `Not started` to `In progress`; add a dated Verification Log kickoff entry with the exact evaluator-versus-award-approver boundary.
6. Inventory the existing required SOD registry/guard, award-readiness source/evaluation/committee/authority lineage, four award boundaries, control events, API, and shared frontend before adding behavior.
7. Derive direct and committee evaluator identities, retained/recalled attempts, exact source/method lineage, and actual authority/workflow approval actors server-side. Never accept an evaluator or independent-actor list from the client.
8. Deny and audit a same-source evaluator acting as sole award approver before any readiness or award mutation. Allow only when exact retained authority/workflow lineage contains a distinct non-evaluator approval actor.
9. Fail closed for missing, duplicate, foreign, mismatched, recalled-only/current-state-invalid, or non-contiguous evaluator/appointment/score lineage, incomplete policy, capability denial, tenant mismatch, and direct-route bypass.
10. Keep generic SOD behavior backward compatible and prevent any trusted sole-actor relaxation option from binding through the public SOD API.
11. Compose the rule through `EvaluateAsync` and `EnsureAwardReadyAsync` so RFQ, formal Tender, Exceptional Sourcing, and legacy award boundaries share one enforcement point without duplicating the four workflows.
12. Expose authenticated tenant-safe current-actor status and shared Tender/RFQ/Exceptional visibility; fail-close Evaluate during loading/error/incomplete/blocked status and require the exact approval permission.
13. Add domain/shared-guard/API/four-boundary/frontend automation, verify no migration is required, run test-database apply/model-drift checks, API/browser smoke, visual review, and exact process cleanup.
14. Review `git diff --check`, synchronize all changed coverage/traceability rows, code anchors, and exact Verification Log evidence, and mark `TDC-0210` `Done` only when every applicable Delivery Contract gate passes. Commit only when the user requests it; do not begin `TDC-0211` in the same slice.

## Historical TDC-0210 Verification Commands

Run from the repository root unless a command says otherwise:

```powershell
git status --short --branch
git diff --check
dotnet build ErpSystem.sln --no-restore
dotnet test tests/ErpSystem.Core.Tests/ErpSystem.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Procurement"
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Controllers.Procurement"
dotnet ef migrations script --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext
```

Run from `frontend`:

```powershell
npx eslint <all changed TDC-0210 TypeScript and TSX files>
npx tsc --noEmit 2>&1 | Select-String "<TDC-0210 award-readiness SOD file names>"
npm run test -- --run <focused award-readiness SOD tests>
npm run build
```

The full TypeScript check currently fails in unrelated baseline files. Each next slice must still run attribution, retain the baseline distinction, and prove there are no new errors in changed procurement files. Do not fix unrelated module diagnostics in a procurement slice.

## Known Traps And Required Responses

| Trap | Required response |
| --- | --- |
| Treating business decisions as code blockers | Build Draft configuration and typed schemas now; gate only publication/UAT of the affected rule. |
| Extending the mutable `ProcurementSettings` row | Add the versioned policy profile beside it; preserve compatibility until controlled migration is planned. |
| Copying broad/contradictory authorization | Use explicit authenticated administration actions; never combine `[AllowAnonymous]` and `[Authorize]`. |
| Enforcing UI-only restrictions | Put every hard stop in service/API code and add direct-API negative tests. |
| Building a new workflow/evidence implementation | Reuse the shared workflow, file storage, evidence policies, notifications, and audit history. |
| Adding module-specific upload or malware-scan code | Inject the Core `IControlledFileUploadService`, use `ControlledFileUploadCategories`, and register one host-level `IFileVirusScanService`; never write user-controlled documents directly from a module controller. |
| Adding a large all-in-one settings screen | Use dedicated list/detail routes and shared dialogs/controls. |
| Publishing seeded questionnaire values | Seed them only as clearly unapproved Draft suggestions; never activate them without TDC approval evidence. |
| Editing or deleting the dirty worktree | Preserve team changes and keep procurement commits scoped. |
| Marking a task Done after backend completion | Require UI, migration, authorization, tests, database apply, smoke evidence, and tracker update. |
| Expanding scope into planning features already delivered | Use `docs/procurement-planning-tasklist.md` and current code to reuse the existing plan/version/workflow/publish/report surfaces. |

## Tracker Update Protocol

- Start each slice by naming explicit `TDC-*` IDs in the tracker and implementation notes.
- Update a task to `In progress` when code work actually starts.
- Keep the Current Coverage and Gap Matrix aligned when behavior changes.
- Add exact migration name, API routes, UI routes, test commands/counts, database evidence, and browser/API smoke results to the Verification Log.
- Record approved configuration decisions with owner, date, effective date, values, and evidence; do not rewrite history.
- Do not mark a phase complete until all its tasks and applicable end-to-end scenarios meet the Delivery Rule.
- Preserve success-target measurement definitions; do not claim business outcomes before go-live measurement.

## Next-Agent Kickoff Prompt

Use this prompt in the new chat after opening the repository:

> Continue the TDC Procurement and Inventory implementation using `docs/tdc-procurement-inventory-implementation-handover.md` and `docs/tdc-procurement-inventory-gap-implementation-tracker.md` as the active delivery documents. Reverify branch `agent/tdc-phase-6`, the pushed Phase 5 PR #23 review correction `a706f449`, the verified local `TDC-0601` implementation, preserved team changes, and user-secret `.\SQL2017` / `RhemaERP` at 207 migration-history rows with `20260801150000_TDC0601InventoryItemIdentifiers` latest, both identifier triggers enabled, duplicate groups and smoke residue zero, passing focused Core/frontend automation, tenant-safe API and real-browser evidence, and closed owned ports. Start only `TDC-0602`; keep Phase 4 `E2E-003`, Phase 5 `E2E-018`, and Phase 6 `E2E-024` explicitly open until their complete acceptance evidence exists. Reuse the TDC-0601 identifier resolver, existing authentication/warehouse scope, inventory transaction services, audit, and synchronization infrastructure; do not add a parallel identifier registry, stock path, workflow or evidence control. Deliver label generation/printing plus authenticated handheld/mobile receipt/issue/return/transfer/count scanning, offline idempotent synchronization and reconciliation under the exact tracker acceptance boundary. Do not begin `TDC-0603` or mark Phase 6 complete in the same slice.

## Handover Completion Signal

The next agent should consider this handover successfully picked up when it has:

- Reverified repository/worktree state, current branch ancestry, configured database migration/trigger state, and closed owned ports without discarding team changes.
- Confirmed the tracker and planning references were read; `TDC-0401` through `TDC-0601` implementation remains accepted while Phase 4 `E2E-003`, Phase 5 `E2E-018`, and Phase 6 `E2E-024` remain open.
- Reverified TDC-0601 primary/alternate/QR/UOM persistence, resolver, uniqueness, ordinary edit, maker-checker, import/export, authorization, audit, API/UI and SQL boundaries before consuming them.
- Mapped existing label, scanner, mobile/offline sync, warehouse authorization, receipt/issue/return/transfer/count, inventory idempotency and audit paths and stated exactly what `TDC-0602` will reuse.
- Changed only `TDC-0602` to `In progress`; `TDC-0603`, unrelated inventory behavior, PR/PO/receiving controls, and shared workflow/evidence implementations remain untouched until separately selected.
