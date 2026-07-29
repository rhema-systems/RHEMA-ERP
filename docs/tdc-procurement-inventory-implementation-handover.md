# TDC Procurement and Inventory Implementation Handover

Last updated: 2026-07-29

Document status: `TDC-0401` through `TDC-0403` verified `Done`; `TDC-0404` next eligible; Phase 4 in progress

## Handover Objective

This document transfers the TDC Procurement and Inventory implementation from completed requirement and gap analysis into controlled delivery. The next agent should be able to open this repository, verify the live state, and continue the next tracker slice without repeating the analysis or inventing a parallel architecture.

The governing delivery ledger is `docs/tdc-procurement-inventory-gap-implementation-tracker.md`. It currently contains 93 roadmap tasks across phases 0 through 9 and 27 mandatory end-to-end acceptance scenarios. Phase 0 (`TDC-0001` through `TDC-0007`), Phase 1 (`TDC-0101` through `TDC-0108`), Phase 2 (`TDC-0201` through `TDC-0212`), and Phase 3 (`TDC-0301` through `TDC-0309`) are verified `Done`. Later roadmap tasks and end-to-end scenarios retain the status recorded in the tracker and must change only as verified implementation work progresses.

## Immediate Starting Decision

The accepted Phase 0 through Phase 3 stack remains the merged baseline commit `61658141` on branch `agent/tdc-0401-framework-agreements`. `TDC-0401` through `TDC-0403` are verified `Done` in the preserved working tree. The framework family/version and governed-price lifecycle composes one dedicated tenant-safe framework call-off aggregate and linked PO with server-derived effective agreement, eligible supplier, currency, immutable price/UOM, approved requisition demand, actor authority and threshold, exact shared PO workflow, approval-time atomic commitment, separate issue, release on approved cancellation, notifications, evidence, authorization, SOD, concurrency, and `DEC-001` through `DEC-014` control-event audit. The ordinary-PO lifecycle now also requires one exact tenant-safe approved RFQ award, tender award/contract, or approved-exception source; persists immutable source, requisition, sourcing-case, readiness, supplier, and integrity lineage; and revalidates that source before update, submission, approval, and lifecycle progression. Framework orders remain confined to their dedicated call-off route, existing POs are protected historical migrations that cannot re-enter approval, and manual, PR-handoff, plan, forged-source, or generic-framework bypasses fail closed. At the user's explicit direction, the configured user-secret database `.\SQL2017` / `RhemaERP` was advanced through the complete earlier migration backlog after correcting five malformed Finance migrations and now includes `20260729153000_TDC0403MandatoryPurchaseOrderSources`: 178 history rows, repeat apply current, and no pending migration/model change. `TDC-0404` is next eligible but remains `Not started`; do not broaden the completed source-lineage slice retroactively.

Phase 2 sourcing through award is complete. `TDC-0201` derives one tenant-safe sourcing case from a current immutable PR sourcing release and enforces that current case at RFQ/tender entry. `TDC-0202` makes the method recommendation server-derived from the exact current policy and protects any override with eligible exception, capability, independent workflow, evidence, actors, and SOD. `TDC-0203` completes the RFQ vertical flow. `TDC-0204` completes NCT/ICT from approved advertisement and controlled paid/free document issue through sealed/late submissions, signed public opening, separate technical/financial evaluation, exact authority/PPA workflow, approved award, executed-contract reference, bidder acceptance, immutable SQL records, and `DEC-001` through `DEC-014` control-event lineage. `TDC-0205` completes Restricted Tendering and Single Source. `TDC-0206` completes tenant-safe prequalification and deterministic qualified-list eligibility. `TDC-0207` completes reusable controlled tender-document families, immutable approved versions, exact Tender/RFQ binding, issuance/receipt, addenda/acknowledgements, and controlled extensions. `TDC-0208` completes the reusable tenant-safe evaluation-committee control: exact composition and appointments, acceptance/COI evidence, signed attendance and remote/hybrid evidence, server-derived quorum, scorer eligibility, immutable signed score attempts, independently approved recall, exact RFQ/NCT/legacy projection parity, eight-table SQL protection, all fourteen decision events, and dedicated Tender/RFQ history-first controls. `TDC-0209` completes the reusable tenant-safe recommendation-approval and award-readiness decision across RFQ, formal Tender, Exceptional Sourcing, and legacy tender-award boundaries, including exact current evaluation/score, supplier/prequalification, verification/due-diligence, recommendation, authority/workflow, evidence, actor, policy/method, integrity, immutable SQL/audit, API, and shared history-first UI controls. `TDC-0210` completes the reusable evaluator-versus-award-approver hard stop across those four boundaries, deriving exact direct and committee score-attempt lineage, allowing only independently approved non-sole outcomes, recording enriched allowed/denied control events, and exposing fail-closed current-actor status in the shared readiness UI without adding schema. `TDC-0211` completes the reusable successful/unsuccessful bidder-communication and tender-security register across formal Tender, RFQ, and Exceptional Sourcing, with server-derived award/readiness/supplier/subject lineage, immutable approved letter versions, dispatch/delivery/acknowledgement, standstill/appeal, security release/return, shared workflow/evidence/notification control events, ten-table SQL protection, and dedicated internal/external views. `TDC-0212` completes the configurable manual/API-ready GHANEPS publication and award exchange across formal Tender, RFQ, and Exceptional Sourcing, including exact evidenced Published/effective `DEC-009` and mapping lineage, immutable payload/attempt/failure/retry/acknowledgement/reconciliation history, SQL protection, role-neutral API, and dedicated shared Tender/RFQ history controls.

`TDC-0301` supplies the category-aware registration evidence-pack baseline, `TDC-0302` supplies the governed free/paid application token and Finance lifecycle, and `TDC-0303` through `TDC-0308` supply eligibility, due diligence, AVL, risk/performance, and supplier-master controls. `TDC-0309` completes verified contact, restricted applicant session/UI, shared controlled-document storage and scanning, terminal token/session closure, permission-complete review and approval, approval-based identity/role activation, one-time credentials, forced password change, resend/retry, and audit. `TDC-0401` completes the separate framework-agreement and governed-price register, `TDC-0402` completes its call-off PO, balance, approval, and issue control, and `TDC-0403` completes mandatory immutable approved-source lineage for ordinary POs. `TDC-0404` and later compliance, receiving, and inventory runtime tasks remain untouched.

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

This snapshot was refreshed on 2026-07-29 at `TDC-0403` closure. The accepted Phase-0 through Phase-3 baseline is merged at `61658141345162a47e03ea12fa402a60ae1d5c07`; the verified `TDC-0401` through `TDC-0403` implementation and the explicit five-file Finance migration repair are present as preserved uncommitted work on `agent/tdc-0401-framework-agreements`.

| Item | Verified state |
| --- | --- |
| Repository root | `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2` |
| Branch | `agent/tdc-0401-framework-agreements`; verified `TDC-0401` through `TDC-0403` and the user-authorized Finance migration repair are uncommitted |
| Baseline commit | `61658141345162a47e03ea12fa402a60ae1d5c07` (`Complete procurement supplier controls through Phase 3 (#16)`) |
| .NET SDK | `8.0.206` from `global.json` |
| Frontend | Next.js `15.5.3`, React `19.1.0`, TypeScript 5, React Query 5, Vitest 3, Lucide icons, shared Radix/shadcn controls |
| Backend build | Final TDC-focused Core, Data, and API Release outputs pass with zero errors; retained warnings are repository baseline only. |
| Focused Core tests | Framework-agreement source/eligibility/decision/concurrency suite remains 7/7, call-off server-owned lifecycle-action rules pass 9/9, and ordinary-PO source-lineage rules pass 18/18. |
| Focused API tests | Framework-agreement authorized/DMS suite remains 4/4 and call-off InternalOnly route/structured `403`/`404`/`409`/`422`/decision-correlation suite passes 3/3. |
| Targeted frontend verification | Framework agreement and call-off helper/client suites pass 12/12, ordinary-PO source service tests pass 2/2, targeted ESLint is clean, and the successful optimized build includes the dedicated framework routes plus source-first `/procurement/purchase-orders/new` at 11.2 kB/203 kB. |
| Full test-project boundary | The TDC-focused Core/API build switches compile and pass. The full Core test project retains the unrelated baseline `ProjectServiceTests` constructor mismatch for `IEstateManagedAssetService`; no TDC-0401 source or test is implicated. |
| Test database | User-secret `.\SQL2017` / `RhemaERP` has 178 migration-history rows with `20260729153000_TDC0403MandatoryPurchaseOrderSources` latest, repeat apply current, and no pending EF model change. TDC-0403 adds nine source columns, four indexes, three trusted foreign keys, one trusted check, and two enabled triggers; historical backfill is valid and five rollback-only source hard stops pass with zero residue. Protected totals remain `PurchaseOrders=1`, `PurchaseOrderReceipts=1`, `StockMovements=0`, `InventoryMovements=0`, and call-offs `0`. |

### Dirty Worktree Protection

The worktree is intentionally not clean. It contains the verified `TDC-0401` through `TDC-0403` domain/API/persistence/frontend/tests/docs plus the user-authorized corrections to five pre-existing malformed Finance migration files. `TDC-0402` adds dedicated framework-call-off guards and `TDC-0403` adds mandatory ordinary-PO source controls to the generic PO controller; neither task changes receiving, stock, or inventory runtime behavior. All pre-existing entries were preserved; generated smoke artifacts remain ignored.

Treat every current entry as accepted user/team work. Do not reset, revert, delete, stash, or broadly reformat it merely to obtain a clean tree. Before `TDC-0404`, reverify status and diff, preserve `TDC-0401` through `TDC-0403`, and keep any commit narrowly scoped to the verified implementation and explicit migration repair.

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
- Generic/manual creation, legacy PR handoff, procurement-plan conversion, RFQ award, tender award, update, submit, approve, and status transitions compose the same source control. The PO persists an immutable source/requisition/sourcing-case/readiness/supplier snapshot and SHA-256 integrity hash and revalidates current authority before progression.
- Existing workflow, evidence, supplier eligibility, notification, authorization, concurrency, and `DEC-001` through `DEC-014` control-event services are reused. Denied validation and successful binding/revalidation are audited; no parallel workflow, evidence, or document controls were created.
- `GET /api/procurement/purchase-orders/source-options`, source fields on PO list/detail responses, and source-first `/procurement/purchase-orders/new` expose tenant-safe fail-closed readiness. The UI disables create actions without an approved source and directs framework procurement to its dedicated route.
- Migration `20260729153000_TDC0403MandatoryPurchaseOrderSources` is applied to user-secret `.\SQL2017` / `RhemaERP`; history is 178 rows, repeat apply is current, and EF reports no pending model change. Nine source columns, four indexes, three trusted foreign keys, one trusted check, two enabled triggers, and a valid historical backfill are verified.
- SQL hard stops reject historical lifecycle re-entry (`51204`), source mutation (`51203`), source-less insertion (`547`), new historical source (`51201`), and forged/non-authoritative source (`547`) with zero residue. Existing historical POs cannot enter a new approval lifecycle.
- Focused Core tests pass 18/18, frontend source tests pass 2/2, targeted ESLint and diff checks are clean, and focused Core/Data/API plus optimized Next builds pass. API smoke returns expected `401`/`200`/`422`/`409` outcomes with no mutation or 5xx. Real Chrome observes 14 API requests, the mandatory source and fail-closed states, disabled source-less create actions, zero page/server/console errors, and a visually clean 1440x1100 capture.
- Protected totals remain `PurchaseOrders=1`, `PurchaseOrderReceipts=1`, `StockMovements=0`, and `InventoryMovements=0`. Owned verification listeners are stopped and ports `5000`/`3016` are closed; receiving and inventory runtime behavior remains unchanged.

Phase 4 is not complete. `TDC-0404` is the next eligible task and must add the PO pre-submit/pre-approve compliance gate without reopening the verified source-lineage or framework-call-off controls or changing receiving/inventory behavior prematurely.

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

> Continue the TDC Procurement and Inventory implementation using `docs/tdc-procurement-inventory-implementation-handover.md` and `docs/tdc-procurement-inventory-gap-implementation-tracker.md` as the active delivery documents. Start with `TDC-0404` only. Reverify branch `agent/tdc-0401-framework-agreements`, baseline `61658141`, the preserved dirty worktree, verified `TDC-0401` through `TDC-0403` implementation and evidence, and user-secret `.\SQL2017` / `RhemaERP` at 178 migration-history rows with `20260729153000_TDC0403MandatoryPurchaseOrderSources` latest and no pending migration/model change; preserve all existing team changes. Add the PO pre-submit and pre-approve compliance gate by composing the existing supplier, budget, commitment, immutable source, evaluation, award, GHANEPS, contract, signature, workflow/evidence/notification/audit, authorization, concurrency, and SOD controls. Do not rewrite the verified framework/source lifecycles, recreate shared controls, begin `TDC-0405`, or change receiving/inventory behavior; do not mark `TDC-0404` `Done` until every applicable acceptance gate passes.

## Handover Completion Signal

The next agent should consider this handover successfully picked up when it has:

- Reverified repository and worktree state.
- Confirmed the tracker and existing planning tracker were read.
- Confirmed `TDC-0401` through `TDC-0403` are complete, Phase 4 remains in progress, and named `TDC-0404` as the only next eligible task.
- Reported how the PO pre-submit/pre-approve compliance gate will compose the verified supplier, budget, commitment, source, evaluation, award, GHANEPS, contract, signature, workflow, evidence, Finance, authorization, SOD, and audit controls before editing.
- Started the `TDC-0404` boundary map without rewriting `TDC-0401` through `TDC-0403`, touching unrelated dirty files, beginning `TDC-0405`, recreating workflow/evidence controls, weakening completed supplier/source/award controls, or changing receiving/inventory behavior.
