# Civil and Quantity Survey search coverage

Updated 2026-09-27. This is a coverage ledger, not a claim that all ERP records are searchable.

## Registered sources

Eleven sources are registered in `frontend/src/lib/global-search-civil-qs-sources.ts`.

| Record | Owner endpoint (GET, prefix `/api`) | Search contract | Exact destination |
|---|---|---|---|
| Civil direct task | `/civil-engineering/direct-tasks/search` | search, take; array | Read-only owner Get by id |
| QS measurement | `/quantity-survey/measurements/search` | search, take; array | Read-only owner Get by id |
| QS saved valuation worksheet | `/quantity-survey/valuation-worksheets/search` | search, take; array | Owner Get by **projectInterimValuationId**, not worksheet id |
| QS variation | `/quantity-survey/variations/search` | search, take; array | Read-only owner Get by id |
| QS payment certificate | `/quantity-survey/payment-certificates/search` | search, take; array | Read-only owner Get by id |
| QS catalogue entry | `/quantity-survey/catalogues/search` | search, take; array | Read-only owner Get by id |
| QS rate-library item | `/quantity-survey/rate-library` | search, page, pageSize; items | Read-only owner Get by id, including rate rows |
| QS price-index import | `/quantity-survey/price-index-imports` | search, page, pageSize; items | Read-only owner Get by id, including values |
| QS escalation formula | `/quantity-survey/escalation-formulas` | search, page, pageSize; items | Read-only owner Get by id, including components |
| QS configuration profile | `/quantity-survey/configuration-profiles` | search, page, pageSize; items | Existing configuration profile `/[id]` page |
| Civil configuration profile | `/civil-engineering/configuration-profiles` | search, page, pageSize; items | Existing configuration profile `/[id]` page |

The five existing paged endpoints filter search on the server before pagination. They retain owner tenant/deleted and permission guards. **Escalation formulas inherit an owner limitation:** accessible projects are discovered through `LookupProjectsAsync(take: 5000)`, so projects beyond that cap may be absent despite being individually readable. Rate-library search retains the owner's default active-item filter. Catalogue search retains existing owner full-list materialization before filtering/taking; it does not filter a previously paged client result.

The new shared result page uses GET only. Opening the existing profile pages performs owner GET, schema/lookup GET and permission-gated audit GET. No workflow refresh, validation, approval or mutation endpoint is invoked by search selection.

## Prepared but not applied

`tmp/qs-search-extension/qs-four-owner-search.patch` prepares dayworks, contract claims, subcontracts and final accounts. It requires Core interface and owner API changes, so it remains excluded from current compiled coverage. The patch has been rebased onto the eleven-source frontend and passes `git apply --check`. Final-account reads preserve the owner's linked-Works-contract validation and reconciliation; invalid historical linkage fails closed.

## Other QS contracts requiring owner work

| Record | Existing GET contract | Missing for complete search |
|---|---|---|
| Joint measurements | `/quantity-survey/joint-measurements`, optional project/status and Page/PageSize; exact `/{id}` | List DTO has **no Search**. Add server predicate before paging. |
| Escalation calculations | `/quantity-survey/escalation-calculations`, optional project/formula/status and Page/PageSize; exact `/{id}` | List DTO has **no Search**. |
| Escalation disputes | `/quantity-survey/escalation-disputes`, optional project/calculation/status and Page/PageSize; exact `/{id}` | List DTO has **no Search**. |
| Material reconciliations | `/quantity-survey/material-reconciliations?projectId=...`; exact `/{id}` | Global authorized matching, bounded result limit. |
| Advance recoveries | `/quantity-survey/advance-recoveries?projectId=...` | Global bounded matching and exact agreement read. |
| Subcontract charges | `/quantity-survey/subcontract-charges?projectId=...&subcontractId=...` | Global bounded matching and exact charge read. |
| Contract commercial terms | `/quantity-survey/contract-commercial-terms/{contractId}` | Searchable parent contract discovery plus selected terms/version identity. |
| Design revision impacts | `/quantity-survey/design-revision-impacts?projectId=...`; exact `/{id}` | Global bounded matching. |
| Tender BoQ submissions | `/quantity-survey/tender-bids/{tenderBidId}/boq-submissions` | Authorized bid-scoped source discovery; do not substitute external-portal GET. |
| Project BoQ versions/lines | Existing project BoQ owner, not spreadsheet operations | Trace exact version/line owner and bounded search before registration. |
| Index families | `/quantity-survey/escalation-formulas/index-families` | Existing list is a candidate, but exact index-family Get is absent from this controller. |

Remeasurement tests/models exist; no corresponding API controller class/route was found in the inspected API source. Establish its reachable owner before registering it. Do not use ignored `search` query parameters plus client first-page filtering for the endpoints above.

## Other Civil contracts requiring owner work

All routes below have prefix `/api/projects/civil-engineering/` unless noted.

- `design-cases`: projectId list and exact Get exist; needs bounded cross-project owner search.
- `rfis`, `site-instructions`, `quality-tests`, `inspection-controls`, `supervision`, `weekly-supervision-reports`, `ipc-endorsements`, `extension-of-time-controls`: projectId lists; no bounded text-search contract.
- `maintenance-intakes`, `maintenance-assessments`, `maintenance-costing-handoffs`, `maintenance-execution-links`, `maintenance-completion-controls`, `development-approval-files`: owner lists accept no search/page arguments. Do not fetch unrestricted lists for every keystroke.
- `development-approval-files/{fileId}/handoffs` and `development-approval-files/{fileId}/engineering-reviews`: parent-scoped lists need authorized source discovery and exact selected-child opening.
- Permitting HoD decisions and complaint-resolution controllers expose process actions; establish their parent read owner instead of calling a process action.
- `/api/projects/{projectId}/civil-engineering/migration-batches`: project-scoped list, no bounded text search.
- Design reconnaissance, information requests, protected documents, planning/GIS evidence and audit histories are children of their authorized parent records. Separate providers require a real standalone read/search contract.

## Verification

Focused frontend run: `global-search-civil-qs-sources.test.ts` (3) plus `search-result/page.test.tsx` (7), **10/10 passed**. It covers exact identity, three specialist child-row types, denied/invalid reads, and no mutation calls. No backend edits/builds were performed for the five latest adapters. Live authenticated verification remains with the root task while API readiness is diagnosed.
