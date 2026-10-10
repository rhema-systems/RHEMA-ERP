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
| MPOS-0308 | AR/GL/liquidity/audit/reconciliation tests | In progress - focused service coverage proves the dedicated invoice/payment posting routes, sale-line revenue account/UOM, split-tender amounts and exact invoice allocations, till-liquidity destination, unique canonical payment links, mutation replay identity, receipt reprint audit idempotency, completed-tender reconciliation, maker/checker close controls, and canonical deposit proposal mapping. SQLite relational failure injection proves rollback of partial owner/receipt writes, tracker cleanup, exact retry repair, and separately durable replayable business rejection. The eleven-control read-only certification pack is committed; authorized SQL Server/UAT accounting certification remains |

## Phase 4 - POS

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0401 | Catalogue/service search and cache contract | In progress - online search plus a permission-protected resumable change feed return sale-ready upserts and tombstones for item and assigned-warehouse quantity changes; the scoped SQLite catalogue cache warms online and provides local network-failure search; physical Android persistence evidence remains |
| MPOS-0402 | Camera/manual/keyboard-wedge scanner boundary | In progress - camera permission and barcode modal, normalized scan contract, manual/Enter-triggered catalogue input compatible with keyboard wedges, and a disposable test adapter are implemented; physical camera/wedge evidence and the Phase 7 ZCS adapter remain |
| MPOS-0403 | Cart, pricing preview, tax display, discounts | In progress - mobile cart quantity controls, server-calculated subtotal/discount/tax/total, dynamic `MobilePOS.Discount.Apply` authorization, permission-gated line discount entry, and preview/final-sale server enforcement are implemented; offline preview remains |
| MPOS-0404 | Tender UI and metadata rules | In progress - mobile checkout supports multiple till-mapped online tenders, amount reconciliation, required references, retry-stable mutation identity, and a searchable selector for active GL-mapped same-currency bank accounts within the operator's Finance scope; visible-device acceptance remains |
| MPOS-0405 | Atomic split-tender orchestration | In progress - the mobile checkout submits one command and one retry-stable mutation ID; the public endpoint composes the canonical invoice, posting, multiple allocated canonical payments, and source envelopes atomically; SQL failure-injection and live accounting reconciliation evidence remain |
| MPOS-0406 | Receipt screen, persistence, reprint audit | In progress - checkout loads and renders the canonical receipt; permission-gated reprints append a tenant-scoped idempotent central audit event, return a numbered `REPRINT` copy, and never repost Finance documents; visible-device receipt/reprint acceptance remains |
| MPOS-0407 | PDF/system/digital printing fallback | In progress - the receipt screen opens Android system printing, renders and retains a safe PDF in app document storage, and opens the native share sheet; Android device acceptance and vendor-specific Z92S printing remain |

## Phase 5 - offline

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0501 | SQLite schema and migrations | In progress - versioned Expo SQLite migrations through version 3 create scoped catalogue, approved-customer, tender, bootstrap/configuration, receipt, cursor, and guarded outbox tables with WAL, foreign keys, and no tokens or signing secrets; Android install/upgrade persistence evidence remains |
| MPOS-0502 | Scoped catalogue/customer/config cache | In progress - catalogue and approved-customer change feeds are isolated by tenant/user/device/store/till, cursor-resumable, transactionally applied, tombstone-aware, and locally searchable; bootstrap and current till payment-method projections are replaced transactionally; physical Android persistence and broader server pull evidence remain |
| MPOS-0503 | Transactional outbox | In progress - SQLite persists scope-bound draft/pending commands, canonical payload hashes, exclusive dispatch claims, attempt metadata, terminal server results, and guarded legal state transitions; the checkout queues retryable transport failures under the original mutation identity; visible device restart evidence remains |
| MPOS-0504 | Signed offline-grant enforcement | In progress - every cash-sale push revalidates signature, persisted status, occurrence window, revocation epoch, assignment, snapshot hash, signed command/tender/discount permissions, Finance permissions, per-sale/count/aggregate limits, and cashier session; relational concurrency and live revocation evidence remain |
| MPOS-0505 | Idempotent sync push/pull | In progress - single-command `/sync/push` validates the canonical SQLite payload hash and signed scope before using the existing mutation, invoice, posting, and payment boundary; exact replays remain idempotent and existing cursor feeds provide pull; full device disconnect/reconnect evidence remains |
| MPOS-0506 | Retry/rejection/conflict state machine | In progress - the dispatcher maps explicit server Synced/Rejected/Conflict decisions, bounded retry of ambiguous transport failures, interrupted claims, secure-grant mismatch, and incomplete results into governed outbox states; the Sync & exceptions screen shows pending and terminal work without automatically reposting review cases; physical-device evidence remains |
| MPOS-0507 | Restart and APK-upgrade preservation | In progress - versioned additive migrations and durable SQLite command/cache storage are implemented; signed grants are retained by immutable grant ID in SecureStore for queued commands after expiry/logout while the active-session pointer is cleared; install, process-kill, restart, and signed APK upgrade preservation evidence remains |

## Phase 6 - till and day-end

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0601 | Mobile till-session open/current views | In progress - the authenticated device/store/till bootstrap now opens and loads the operator's canonical Finance cashier session, derives the store-local business date, enforces both dynamic Mobile POS and Finance till permissions, and exposes a mobile opening/current-session screen; live API, database, and Android acceptance remain |
| MPOS-0602 | Server reconciliation by till/session/tender | In progress - the server derives completed, offline, pending, and rejected sale counts plus subtotal/tax/discount, sales-versus-tender balance, incomplete tenders, canonical payment counts, and payment-method groupings for the operator's assigned Finance till session; live SQL/accounting evidence remains |
| MPOS-0603 | Cash declaration and count evidence | In progress - the mobile close flow captures Ghana denomination counts, variance reason/evidence, and delegates the authoritative count to `ICashierTillService.SubmitCountAsync`; live device and SQL evidence remain |
| MPOS-0604 | Variance HQ workflow and segregation of duties | In progress - the HQ Mobile POS day-end queue uses the canonical Finance approval/return actions, blocks the cashier from resolving their own sync exception, and synchronizes returned/finalized evidence states; authenticated browser and live workflow evidence remain |
| MPOS-0605 | Pending-sync submit/finalization control | In progress - pending mutation IDs are normalized, hashed, persisted with the snapshotted policy, and rechecked against completed mutation receipts before the shared Finance transaction can finalize; an independent HQ resolution is required for unresolved work; relational concurrency and live device evidence remain |
| MPOS-0606 | Bank deposit proposal integration | In progress - only a finalized and closed till session can propose its unallocated custody entries through the canonical Finance banking service; the linkage is retry recoverable through an immutable source marker and retains the Finance deposit lifecycle; live Finance approval/posting evidence remains |
| MPOS-0607 | HQ till/mobile reports | In progress - the HQ administration page exposes current review work and a historical till-close report with close, sync, variance and deposit states plus CSV export; authenticated browser and live database evidence remain |

## Phase 7 - hardware

| ID | Work item | Status | Dependency |
| --- | --- | --- | --- |
| MPOS-0701 | Printer/scanner capability interfaces and fake adapters | In progress | Receipt-printer capability/result contract, deterministic fake printer, normalized scanner/test adapter, runtime hardware detection, and focused tests are implemented; physical adapter evidence remains |
| MPOS-0702 | ZCS built-in printer adapter | In progress | Local Expo/Kotlin bridge adapts SmartPos initialization/status/text/start calls; canonical 32-column receipt rendering, device capability heartbeat, configured checkout routing, audited external-SDK packaging, real artifact hash validation, and Android prebuild/autolink evidence pass; written redistribution approval, native Gradle build, and physical print acceptance remain |
| MPOS-0703 | ZCS hardware scanner adapter | In progress | Local Expo/Kotlin bridge adapts `HQrsanner` power/trigger/stop calls and the sale screen captures the vendor keyboard-wedge result through the normalized catalogue path; focused lifecycle tests and Android prebuild/autolink evidence pass; native Gradle build and physical trigger/decode acceptance remain |
| MPOS-0704 | Bluetooth ESC/POS adapter | Blocked | Management/vendor must confirm the supported printer models and protocol behavior before an adapter and certification matrix can be selected |
| MPOS-0705 | Z92S physical certification | Blocked | Signed build and physical Z92S |

## Phase 8 - hardening and release

| ID | Work item | Status |
| --- | --- | --- |
| MPOS-0801 | Complete `acceptance:mobile-pos` gate | Complete - the committed gate restores both lockfiles, rejects tracked mobile binaries, validates mobile and HQ TypeScript, runs mobile tests/Expo Doctor/Hermes export, performs a migration-aware API Release build, requires a nonempty all-passing focused API TRX, verifies idempotent SQL for all four Mobile POS migrations, and optionally hash-verifies the externally supplied Z92S SDK packaging. The fresh-dependency release-form run passed on 2026-10-10; the current checkpoint `7d5b4817ec5` passes 83 mobile tests, 65/65 focused API tests including relational rollback/retry recovery, all eight migration markers, and the production manifest/signing-guard contracts using the already restored lockfile dependencies. Native Android and proprietary SDK packaging remain separate external gates |
| MPOS-0802 | Mobile threat review and penetration checks | In progress - source threat model, per-request origin validation, remote cleartext rejection, production Android backup/cleartext controls, 74/74 mobile security/regression tests, production manifest prebuild evidence, and current npm/.NET advisory scans are complete; coordinated dependency remediation, MFA challenge-token change/acceptance, screenshot and pinning decisions, signed-APK analysis, authorized API penetration testing, and physical Z92S/root/TLS/revoke testing remain |
| MPOS-0803 | Accounting reconciliation certification | In progress - committed read-only SQL and guarded PowerShell wrapper reconcile completed sales/lines/tenders to canonical posted invoices, payments, allocations, mutation receipts, closed Finance till sessions, and deposit proposals with eleven fail-closed controls; authorized UAT execution with nonzero sales and Finance sample review remain |
| MPOS-0804 | Provider sandbox certification | Blocked pending provider selection |
| MPOS-0805 | Performance, battery, network, and recovery tests | In progress - a committed host rehearsal exercises 1,000-command canonicalization and dispatch, bounded retry, ambiguous response-loss replay under the original mutation identity, and stale in-flight claim recovery; the full mobile suite passes 78/78 and the evidence wrapper records local JSON/logs. Signed Z92S timing, real network interruption, process/device restart, APK upgrade preservation, queue drain through API/SQL, resource pressure, and eight-hour battery evidence remain |
| MPOS-0806 | Signing, build, deployment, upgrade, and rollback runbook | In progress - generated Android release tasks now fail closed without external signing enablement/keystore values, the signed-build script binds a clean commit to native Z92S acceptance evidence, HTTPS origin, version, certificate and artifact hashes, and the runbook defines staged install, in-place upgrade, stop/recovery, and higher-version-code last-good replacement. Secure signing identity/credentials, written SDK redistribution approval, signed APK/AAB output, physical Z92S install/upgrade, and rollback-rehearsal evidence remain |
| MPOS-0807 | Production readiness and operational support handoff | In progress - the mobile account screen can generate a privacy-limited support bundle with environment/device/assignment/adapter/grant timing and queue counts, Sync & exceptions preserves correlation references, and the handoff defines roles, daily controls, incident priorities, triage/recovery playbooks, monitoring cadence, and go-live gates. Named owners, approved SLA/RPO/RTO/alerts, production configuration, training, signed-device/provider/physical evidence, rehearsals, and operational sign-off remain |

## External dependency register

| Dependency | Needed by | Owner/status |
| --- | --- | --- |
| ZCS Z92S printer SDK/service contract | MPOS-0702 | Supplied 2026-10-09 as SmartPos 1.8.1 and hash-audited; no licence/redistribution terms detected; exact Android 14 firmware behavior remains unverified |
| ZCS hardware scanner contract | MPOS-0703 | Supplied 2026-10-09 through `HQrsanner`; physical Z92S behavior remains unverified |
| Physical Z92S certification unit | MPOS-0705 | Awaiting availability |
| Supported Bluetooth ESC/POS printer models and protocol contracts | MPOS-0704 | Awaiting management/vendor confirmation; implementation is blocked to avoid claiming compatibility against an unspecified device |
| Mobile Money/card provider contract and sandbox | MPOS-0804 | Awaiting management/provider selection |
| Android signing identity and secure CI credentials | Phase 8 | Must be prepared before release |

## Evidence rules

No item becomes `Complete` from source changes alone. The evidence column must link or describe the relevant migration check, focused tests, API result, SQL/reconciliation evidence, browser route verification, Android/native result, and physical hardware result required by that item.

Production deployment, provider enrollment, and device activation are separate release actions and are not implied by implementation completion.
