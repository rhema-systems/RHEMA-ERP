# RHEMA Field POS implementation tracker

**Branch:** `codex/mobile-pos-phase1`

**Phase 1 base:** `9044371f533ee3fac77472d74aecfe006e8c6872`

**Architecture:** [RHEMA_MOBILE_POS_ARCHITECTURE_AND_IMPLEMENTATION_PLAN.md](RHEMA_MOBILE_POS_ARCHITECTURE_AND_IMPLEMENTATION_PLAN.md)

## Status legend

- `Complete`: implementation and required evidence are present.
- `In progress`: active work exists but the acceptance gate is incomplete.
- `Ready`: architecture is settled and work may begin.
- `Blocked`: an external dependency prevents meaningful completion.
- `Planned`: not yet started.

## Decision register

| ID | Decision | Status |
| --- | --- | --- |
| MPOS-DEC-001 | RHEMA Finance remains the only accounting authority | Approved |
| MPOS-DEC-002 | Use Expo/React Native with native prebuild capability | Approved |
| MPOS-DEC-003 | Production support baseline Android 10/API 29; certify Z92S Android 14 | Approved |
| MPOS-DEC-004 | One physical till has one active/pending-review cashier session | Approved |
| MPOS-DEC-005 | Each store has one approved default walk-in Business Partner/customer mapping | Approved |
| MPOS-DEC-006 | Operators may select another approved customer; mobile customer creation is prohibited | Approved |
| MPOS-DEC-007 | Split tender creates multiple canonical CustomerPayments in one server transaction | Approved |
| MPOS-DEC-008 | Offline default authorization window is eight hours and configurable | Approved |
| MPOS-DEC-009 | Electronic tender offline is record-only with existing external authorization evidence | Approved |
| MPOS-DEC-010 | Offline numbers are provisional; RHEMA issues canonical numbers after sync | Approved |
| MPOS-DEC-011 | Day-end is till/session based; user is audit context | Approved |
| MPOS-DEC-012 | Pending-sync day-end may be submitted by policy but cannot be finalized while ambiguous | Approved |
| MPOS-DEC-013 | Production network transport is HTTPS only | Approved |
| MPOS-DEC-014 | Dynamic permissions are assigned to roles; role names are never hardcoded | Approved |

## Phase 1 - architecture and audit

| Task | Status | Evidence |
| --- | --- | --- |
| Audit Flash mobile source and build configuration | Complete | Architecture sections A-B |
| Audit Flash incident/fix history | Complete | Architecture section C |
| Map RHEMA authentication, tenant, MFA, and sessions | Complete | Architecture section D |
| Map AR invoice lifecycle and posting | Complete | Architecture section E |
| Map customer payment, allocation, reversal, and split tender fit | Complete | Architecture section F |
| Map configured payment methods | Complete | Architecture section G |
| Map liquidity, till custody, and banking settlement | Complete | Architecture section H |
| Map permissions and device-management gaps | Complete | Architecture sections I and Q |
| Map current mobile/PWA reuse boundary | Complete | Architecture section J |
| Define mobile, store, till, device, day-end, sync, and printer architecture | Complete | Architecture sections K-P |
| Define migrations, APIs, tests, and risks | Complete | Architecture sections R-U |
| Record management policy and default-customer decision | Complete | Architecture sections 2-3 |
| Update Finance coordination ledger | Complete | `docs/Finance/coordination/FINANCE_MOBILE_POS_PHASE1_20261009.md` |

**Phase 1 exit:** Complete when the documentation commit is recorded in the coordination ledger.

## Phase 2 - foundation

| ID | Work item | Status | Acceptance evidence |
| --- | --- | --- | --- |
| MPOS-0201 | Create isolated `apps/mobile` Expo workspace | Complete | Expo 54 workspace; typecheck passed; Expo Doctor 18/18; Android export passed |
| MPOS-0202 | Metro singleton/runtime protection | Complete | Metro pins React/React Native to the app root; Expo Doctor and Android bundle export passed |
| MPOS-0203 | Root crash boundary and sanitized diagnostics | In progress | Root boundary, compact reference-only diagnostic view, and deterministic support-reference test implemented; visible fault injection evidence remains |
| MPOS-0204 | Environment/server profile with HTTPS production policy | Complete | TEST/UAT/PRODUCTION profiles, production HTTPS rejection, origin-only validation, and six focused tests pass |
| MPOS-0205 | Typed API client and ProblemDetails model | Complete | Typed client, ProblemDetails/correlation mapping, sanitized non-JSON handling, single-flight refresh, retry-once 401, and five focused contract tests pass |
| MPOS-0206 | Secure token/install-secret storage | In progress | SecureStore-backed native session, install secret, and signed offline grant implemented; automatic expiry/context invalidation tests pass; physical Android keystore evidence remains |
| MPOS-0207 | Login, MFA, tenant select, refresh, logout state machine | In progress | Real auth APIs implemented; tenant switch now returns a tenant-scoped refresh token; visible device lifecycle remains |
| MPOS-0208 | Mobile POS permission catalogue and policies | Complete | Six focused authorization/model tests pass; controller actions use dynamic `MobilePOS.*` policies without role names |
| MPOS-0209 | Store/default-customer/dimension model | In progress | Migration, approved-customer validation, dimension defaults, tenant indexes, and service integration cases pass; authorized SQL migration/application evidence remains |
| MPOS-0210 | Till and tender mappings | In progress | CashTill and payment-method mappings with uniqueness checks and admin UI implemented; database mapping cases remain |
| MPOS-0211 | Effective user-store assignment | In progress | Effective assignment command and service test close the prior current assignment; relational concurrency evidence remains |
| MPOS-0212 | Persisted device enrollment, approval, revoke, heartbeat | In progress | APIs, assignment history, revocation epoch, grant revocation, admin UI, and service lifecycle test pass; live remote-disable flow remains |
| MPOS-0213 | Offline policy and grant model | In progress | Signed issuance requires offline/till permissions and an open cashier session; grant binds tenant/user/device/store/till/session/policy/revocation epoch, snapshots filtered command/tender/limit policy, supersedes prior grants, and has backend/client tests; sync-side signature/status/aggregate consumption enforcement remains MPOS-0504 |
| MPOS-0214 | Bootstrap, dashboard shell, account, diagnostics | In progress | Dashboard and account surfaces use real bootstrap/auth values; authenticated visible-device evidence remains |
| MPOS-0215 | HQ Mobile POS administration UI | In progress | Permission-gated route, navigation, stores/tills/policies/devices/assignments UI, typecheck, lint, and 27 route/access tests pass; browser verification remains |

## Phase 3 - core Finance

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0301 | Certified Mobile POS Finance dimension routes | In progress - distinct compiled invoice and customer-payment routes, external producer contracts, Mobile POS module-lock identity, AR invoice/payment route guards, approval support, settlement dimension mapping, and focused contract tests are implemented; live database producer-readiness evidence remains |
| MPOS-0302 | Approved-customer search and default customer resolution | In progress - tenant/store/device-scoped API and mobile screen implemented; eligible customers require an effective Customer role, approved/active registration, approved Business Partner status, and an effective approved AR profile; blank search resolves only the mapped store default; service tests pass; live SQL/API/device evidence remains |
| MPOS-0303 | Outstanding-invoice lookup | In progress - selected eligible customers use the canonical `IPaymentService` outstanding-invoice query through a `MobilePOS.Customer.View` protected endpoint; mobile balance view and focused tests pass; live AR/API evidence remains |
| MPOS-0304 | Invoice preview/create/lifecycle adapter | In progress - the governed preview endpoint resolves current item prices, approved/default customer, currency precision, discount policy, and canonical tax engine totals; the mobile checkout renders that preview and the online sale endpoint revalidates it before canonical create/post; live Finance evidence remains |
| MPOS-0305 | Customer receipt and allocation adapter | In progress - each online tender creates one canonical `CustomerPayment` through `IPaymentService`, allocates it directly to the sale invoice, defaults cash to the till liquidity account, and enforces till tender/reference rules; collection flows and live Finance evidence remain |
| MPOS-0306 | Server idempotency registry | In progress - server-computed command fingerprints, tenant/device/client-mutation uniqueness, atomic relational transaction locking, exact completed/rejected sale replay, mismatch conflicts, and focused tests implemented; live SQL concurrency evidence remains |
| MPOS-0307 | Canonical receipt projection | In progress - a store-scoped runtime endpoint projects an immutable receipt from the completed Mobile POS sale snapshot plus canonical invoice/payment identifiers, including lines, totals, customer, cashier, till, and split tenders; live SQL/API/device evidence remains |
| MPOS-0308 | AR/GL/liquidity/audit/reconciliation tests | Planned |

## Phase 4 - POS

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0401 | Catalogue/service search and cache contract | In progress - the assigned store/device/till-scoped online search returns active sale-ready items with current price, UOM, tax group, and warehouse availability; offline change feed and device cache remain |
| MPOS-0402 | Camera/manual/keyboard-wedge scanner boundary | In progress - camera permission and barcode modal, normalized scan contract, manual/Enter-triggered catalogue input compatible with keyboard wedges, and a disposable test adapter are implemented; physical camera/wedge evidence and the Phase 7 ZCS adapter remain |
| MPOS-0403 | Cart, pricing preview, tax display, discounts | In progress - mobile cart quantity controls, server-calculated subtotal/discount/tax/total, dynamic `MobilePOS.Discount.Apply` authorization, permission-gated line discount entry, and preview/final-sale server enforcement are implemented; offline preview remains |
| MPOS-0404 | Tender UI and metadata rules | In progress - mobile checkout supports multiple till-mapped online tenders, amount reconciliation, required references, retry-stable mutation identity, and a searchable selector for active GL-mapped same-currency bank accounts within the operator's Finance scope; visible-device acceptance remains |
| MPOS-0405 | Atomic split-tender orchestration | In progress - the mobile checkout submits one command and one retry-stable mutation ID; the public endpoint composes the canonical invoice, posting, multiple allocated canonical payments, and source envelopes atomically; SQL failure-injection and live accounting reconciliation evidence remain |
| MPOS-0406 | Receipt screen, persistence, reprint audit | In progress - checkout loads and renders the canonical receipt; permission-gated reprints append a tenant-scoped idempotent central audit event, return a numbered `REPRINT` copy, and never repost Finance documents; visible-device receipt/reprint acceptance remains |
| MPOS-0407 | PDF/system/digital printing fallback | In progress - the receipt screen opens Android system printing, renders and retains a safe PDF in app document storage, and opens the native share sheet; Android device acceptance and vendor-specific Z92S printing remain |

## Phase 5 - offline

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0501 | SQLite schema and migrations | Planned |
| MPOS-0502 | Scoped catalogue/customer/config cache | Planned |
| MPOS-0503 | Transactional outbox | Planned |
| MPOS-0504 | Signed offline-grant enforcement | Planned |
| MPOS-0505 | Idempotent sync push/pull | Planned |
| MPOS-0506 | Retry/rejection/conflict state machine | Planned |
| MPOS-0507 | Restart and APK-upgrade preservation | Planned |

## Phase 6 - till and day-end

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0601 | Mobile till-session open/current views | Planned |
| MPOS-0602 | Server reconciliation by till/session/tender | Planned |
| MPOS-0603 | Cash declaration and count evidence | Planned |
| MPOS-0604 | Variance HQ workflow and segregation of duties | Planned |
| MPOS-0605 | Pending-sync submit/finalization control | Planned |
| MPOS-0606 | Bank deposit proposal integration | Planned |
| MPOS-0607 | HQ till/mobile reports | Planned |

## Phase 7 - hardware

| ID | Work item | Status | Dependency |
| --- | --- | --- | --- |
| MPOS-0701 | Printer/scanner capability interfaces and fake adapters | In progress | Scanner normalization/test adapter exists; printer capability interface remains |
| MPOS-0702 | ZCS built-in printer adapter | Ready | SDK supplied and audited; scheduled after Phase 6; redistribution approval and physical test unit still required for acceptance |
| MPOS-0703 | ZCS hardware scanner adapter | Ready | `HQrsanner` contract supplied and audited; scheduled after Phase 6; physical test unit still required for acceptance |
| MPOS-0704 | Bluetooth ESC/POS adapter | Planned | Supported printer models |
| MPOS-0705 | Z92S physical certification | Blocked | Signed build and physical Z92S |

## Phase 8 - hardening and release

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0801 | Complete `acceptance:mobile-pos` gate | Planned |
| MPOS-0802 | Mobile threat review and penetration checks | Planned |
| MPOS-0803 | Accounting reconciliation certification | Planned |
| MPOS-0804 | Provider sandbox certification | Blocked pending provider selection |
| MPOS-0805 | Performance, battery, network, and recovery tests | Planned |
| MPOS-0806 | Signing, build, deployment, upgrade, and rollback runbook | Planned |
| MPOS-0807 | Production readiness and operational support handoff | Planned |

## External dependency register

| Dependency | Needed by | Owner/status |
| --- | --- | --- |
| ZCS Z92S printer SDK/service contract | MPOS-0702 | Supplied 2026-10-09 as SmartPos 1.8.1 and hash-audited; no licence/redistribution terms detected; exact Android 14 firmware behavior remains unverified |
| ZCS hardware scanner contract | MPOS-0703 | Supplied 2026-10-09 through `HQrsanner`; physical Z92S behavior remains unverified |
| Physical Z92S certification unit | MPOS-0705 | Awaiting availability |
| Mobile Money/card provider contract and sandbox | MPOS-0804 | Awaiting management/provider selection |
| Android signing identity and secure CI credentials | Phase 8 | Must be prepared before release |

## Evidence rules

No item becomes `Complete` from source changes alone. The evidence column must link or describe the relevant migration check, focused tests, API result, SQL/reconciliation evidence, browser route verification, Android/native result, and physical hardware result required by that item.

Production deployment, provider enrollment, and device activation are separate release actions and are not implied by implementation completion.
