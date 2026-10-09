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
| MPOS-0201 | Create isolated `apps/mobile` Expo workspace | Ready | Typecheck, Expo alignment, Android export |
| MPOS-0202 | Metro singleton/runtime protection | Ready | One React/React Native runtime gate |
| MPOS-0203 | Root crash boundary and sanitized diagnostics | Ready | Startup fault injection tests |
| MPOS-0204 | Environment/server profile with HTTPS production policy | Ready | Environment identity and TLS tests |
| MPOS-0205 | Typed API client and ProblemDetails model | Ready | Correlation/error contract tests |
| MPOS-0206 | Secure token/install-secret storage | Ready | Keystore-backed native tests |
| MPOS-0207 | Login, MFA, tenant select, refresh, logout state machine | Ready | Auth lifecycle tests and UI flow |
| MPOS-0208 | Mobile POS permission catalogue and policies | Ready | Dynamic role assignment tests |
| MPOS-0209 | Store/default-customer/dimension model | Ready | Migration plus validity/tenant tests |
| MPOS-0210 | Till and tender mappings | Ready | Unique till and mapping tests |
| MPOS-0211 | Effective user-store assignment | Ready | One-active-store concurrency tests |
| MPOS-0212 | Persisted device enrollment, approval, revoke, heartbeat | Ready | Enrollment and remote-disable tests |
| MPOS-0213 | Offline policy and grant model | Ready | Signature, expiry, limit tests |
| MPOS-0214 | Bootstrap, dashboard shell, account, diagnostics | Ready | Real API values; no mock dashboard data |
| MPOS-0215 | HQ Mobile POS administration UI | Ready | Browser route and permission verification |

## Phase 3 - core Finance

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0301 | Certified Mobile POS Finance dimension routes | Planned |
| MPOS-0302 | Approved-customer search and default customer resolution | Planned |
| MPOS-0303 | Outstanding-invoice lookup | Planned |
| MPOS-0304 | Invoice preview/create/lifecycle adapter | Planned |
| MPOS-0305 | Customer receipt and allocation adapter | Planned |
| MPOS-0306 | Server idempotency registry | Planned |
| MPOS-0307 | Canonical receipt projection | Planned |
| MPOS-0308 | AR/GL/liquidity/audit/reconciliation tests | Planned |

## Phase 4 - POS

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0401 | Catalogue/service search and cache contract | Planned |
| MPOS-0402 | Camera/manual/keyboard-wedge scanner boundary | Planned |
| MPOS-0403 | Cart, pricing preview, tax display, discounts | Planned |
| MPOS-0404 | Tender UI and metadata rules | Planned |
| MPOS-0405 | Atomic split-tender orchestration | Planned |
| MPOS-0406 | Receipt screen, persistence, reprint audit | Planned |
| MPOS-0407 | PDF/system/digital printing fallback | Planned |

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
| MPOS-0701 | Printer/scanner capability interfaces and fake adapters | Planned | None |
| MPOS-0702 | ZCS built-in printer adapter | Blocked | ZCS SDK/AIDL/intent artifact and test unit |
| MPOS-0703 | ZCS hardware scanner adapter | Blocked | ZCS scanner contract and test unit |
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
| ZCS Z92S printer SDK/service contract | MPOS-0702 | Awaiting supplier/vendor |
| ZCS hardware scanner contract | MPOS-0703 | Awaiting supplier/vendor |
| Physical Z92S certification unit | MPOS-0705 | Awaiting availability |
| Mobile Money/card provider contract and sandbox | MPOS-0804 | Awaiting management/provider selection |
| Android signing identity and secure CI credentials | Phase 8 | Must be prepared before release |

## Evidence rules

No item becomes `Complete` from source changes alone. The evidence column must link or describe the relevant migration check, focused tests, API result, SQL/reconciliation evidence, browser route verification, Android/native result, and physical hardware result required by that item.

Production deployment, provider enrollment, and device activation are separate release actions and are not implied by implementation completion.
