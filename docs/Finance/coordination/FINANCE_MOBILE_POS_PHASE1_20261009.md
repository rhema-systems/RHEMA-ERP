# Finance mobile POS architecture and foundation workstream - 2026-10-09

## Objective and scope

Audit the committed Flash ERP Android mobile application and the current RHEMA ERP Finance, AR, payment, till, security, tenant, location, and offline capabilities. Produce an implementation-ready architecture and phased delivery plan for the RHEMA Field POS and Revenue Collection mobile application. The plan must incorporate the management policies recorded in `C:\Users\USER\Desktop\Mobile app.docx`, including per-store default walk-in customers.

The architecture and contract mapping are complete. Phase 2 now contains an isolated Expo application, server-side Mobile POS governance foundation, signed offline-grant issuance, secure client grant handling, and an HQ administration page. The Phase 3 read slice adds governed approved-customer search, automatic store default-customer resolution, and canonical outstanding-invoice lookup. The Phase 3 Finance-route milestone adds distinct compiled invoice and customer-payment producer routes, external-producer contracts, module-lock identity, and canonical AR guard/settlement support. The transaction foundation adds persisted sale, line, tender, and mutation-receipt source envelopes plus server-side idempotent command execution. The online-sale milestone composes one validated invoice and one allocated canonical CustomerPayment per split tender inside that transaction boundary. The checkout milestone adds governed catalogue search, canonical server preview, a mobile cart and split-tender flow that calls the atomic command, a canonical receipt projection, permission-gated idempotent reprint auditing, and the Phase 4 camera/manual/keyboard-wedge scanner boundary. Phase 5 now has versioned scoped SQLite persistence, resumable reference feeds, a guarded transactional outbox, signed offline-command enforcement, one-command idempotent push, and a client retry/rejection/conflict dispatcher. Phase 6 adds canonical till-session opening, reconciliation, cash declaration, pending-sync controls, maker/checker day-end review, HQ reporting, and governed Finance bank-deposit proposals. Phase 7 now contains the ZCS printer/scanner capability boundary, Expo/Kotlin bridge, externally supplied SDK packaging control, and mobile checkout integration. This work does not authorize an alternate accounting ledger, direct database synchronization, production deployment, or device enrollment in a live environment.

## Branch and worktree

- Branch: `codex/mobile-pos-phase1`
- Worktree: `.worktrees/mobile-pos-phase1`
- Exact starting commit: `9044371f533ee3fac77472d74aecfe006e8c6872`
- Starting ref: `origin/master`
- Current integrated master baseline: `7b4a23a71f6e3d3c2f58950d69fd99cfedce8f0b`
- Current implementation checkpoint: `7c172f8f47f8a90ddc1d7a6cd7dfa8246843f3b7`
- Pull request: not created

## Source baselines

- RHEMA ERP target: `rhema-systems/RHEMA-ERP`, `origin/master` at `9044371f533ee3fac77472d74aecfe006e8c6872`.
- Flash ERP reference: local checkout `D:\DEVELOPMENTS\FLASH_DEVS\FLASH-ERP`; audit committed source only because that checkout contains unrelated uncommitted work.
- Management policy source: `C:\Users\USER\Desktop\Mobile app.docx`, last modified 2026-10-09.
- Original mobile requirements source: `C:\Users\USER\.codex\attachments\80e13311-5a40-4b43-85c7-0aea41a29e8a\Pasted text.txt`.

## Management decisions incorporated

- One cashier or collector owns each till session; multiple users do not share a session.
- Each user opens a separate shift. Only one active session may hold a physical till at a time.
- Opening float, offline limits, provisional documents, and pending-sync day-end behavior are tenant configurable.
- Till closure is permission controlled; shortage and excess approval belongs to an HQ approval process.
- Each sales store maps to one active default walk-in customer that is an approved Business Partner with an active Customer role and AR profile.
- The user's active store automatically supplies the walk-in customer. The operator may replace it with an approved customer found through search.
- Mobile customer creation is prohibited online and offline.
- Offline cash sales, cash receipts, cached customer and catalogue lookup, provisional receipts, partial payments, and privileged returns/reversals are allowed subject to tenant policy.
- Electronic tenders may be recorded offline only after completion outside the app, with the external authorization reference captured. Integrated provider processing is online.
- Offline documents receive a unique local reference and are assigned authoritative RHEMA numbers after synchronization.

## Current RHEMA evidence

- `CashierTillSession` already records cashier ownership, opening float, custody totals, count evidence, variance, submission, review, closure, and correction lineage.
- `CashierTillService.OpenSessionAsync` prevents a second open or pending-review session on the same CashTill liquidity account.
- RHEMA customer invoices require `BusinessPartnerId`, `BusinessPartnerRoleId`, and a governed AR profile snapshot. The per-store default customer policy therefore fits the existing invoice boundary and supersedes an anonymous-customer document proposal.
- `PaymentService.CreateAsync` is already a serializable accounting command covering the source payment, optional allocations, Finance dimensions, liquidity evidence, and central posting. It supports partial and multi-invoice allocation for one tender.
- A `CustomerPayment` has one configured payment method. Split tender therefore requires multiple canonical payments grouped and committed by one server-side Mobile POS sale command; independent client HTTP calls are not atomic enough.
- `PaymentMethod` is tenant scoped and supplies active state, type, reference/bank requirements, and a default GL mapping. Mobile eligibility needs an additive store/policy mapping rather than hardcoded tender names.
- `LiquidityAccount`, `LiquidityAccountEntry`, `CashierTillSession`, and `BankDepositBatch` already form the correct custody and settlement chain. Mobile adds store/till/session links and sync evidence; it does not add another cash ledger.
- HR `Location` already supplies operating-site hierarchy, address, geography, and coordinates. Inventory `Warehouse` remains stock storage. A Mobile POS store should reference these masters rather than repurpose either one as the POS policy aggregate.
- The Finance dimension framework already freezes certified source-document assignments. Mobile invoice, receipt, and deposit producers require dedicated certified routes.
- RHEMA's permission handler enforces active user, tenant membership/expiry, and dynamic role permissions. Mobile permissions must be catalogue entries assigned to roles; role names remain unrestricted.
- `/api/Device/trust` returns HTTP 501 because no persisted device trust store exists. Mobile financial use therefore requires a new enrolled/approved/revoked device model.
- RHEMA's current web manifest and mobile-optimized pages do not provide the required native Android hardware, offline authorization, outbox, or POS accounting client.

## Architecture deliverables

- `docs/Mobile/RHEMA_MOBILE_POS_ARCHITECTURE_AND_IMPLEMENTATION_PLAN.md` covers the required A-U audit and design sections.
- `docs/Mobile/RHEMA_MOBILE_POS_IMPLEMENTATION_TRACKER.md` records decisions, phase tasks, exit evidence, and external dependencies.
- Phase 1 conclusion: existing RHEMA Finance services are reusable and authoritative; the principal new work is the Mobile POS orchestration/admin layer, native client, bounded offline sync, persisted device enrollment, and hardware adapters.

## Commits

- `fb707967413` - Phase 1 architecture report, implementation tracker, and initial coordination ledger.
- `6df8433f81d` - Phase 1 documentation checkpoint before Phase 2 implementation.
- `6d981a51448` - Phase 2 Mobile POS application, governance foundation, HQ administration UI, migration, and focused tests.
- `cd98aa003c4` - Phase 2 client contract hardening, reproducible mobile validation, and store/device service integration tests.
- `52eda8a8f2b` - Signed offline-grant issuance, permission/policy filtering, secure mobile grant lifecycle, dashboard controls, and focused acceptance tests.
- `a3bf60b22b9` - Phase 3 approved-customer/default resolution, canonical outstanding-invoice reads, mobile balance UI, authorization, tests, and tracker updates.
- `457fa545294` - Phase 3 compiled Mobile POS invoice/payment Finance routes, external contracts, module-lock identity, canonical AR guards, tests, and coordination evidence.
- `c8c9c8e1869` - Phase 3 persisted sale/line/tender source envelopes, server-side mutation idempotency execution, migration, tests, and tracker evidence.
- `f4d64bf0abf` - Phase 3 public online sale command, canonical invoice/posting and split-payment allocation orchestration, validation, authorization, tests, and tracker evidence.
- `939e0190b38` - Phase 4 governed catalogue search, canonical sale preview, native cart and split-tender checkout, API/client tests, and tracker evidence.
- `31399b06bd9` - Canonical Mobile POS receipt projection, assigned-store access boundary, permission-gated idempotent reprint audit, mobile receipt UI, and focused tests.
- `e8967f404f4` - Safe receipt HTML renderer, retained PDF output, Android system print and native sharing fallback, tests, and Expo dependencies.
- `c4fd481ab8f` - Camera/manual/keyboard-wedge scanning boundary, barcode normalization contract, catalogue integration, tests, and Expo Camera dependency.
- `201506dd3d1` - Dynamic Mobile POS discount permission, preview/final-sale enforcement, permission-gated mobile line entry, and focused tests.
- `a8978d2664f` - Governed bank-account lookup, searchable tender destination selection, canonical validation, Finance scope enforcement, and focused tests.
- `00e2d2d8aa3` - Permission-protected catalogue change feed, scoped Expo SQLite migration/cache, durable cursor resume, network fallback, and focused tests.
- `161c2b29ec3` - Approved-customer change feed, customer tombstones/search fallback, bootstrap/payment-method configuration cache, shared scoped change cursor, and focused tests.
- `1b534f646e8` - Version-3 durable Mobile POS outbox, canonical payload hashing, guarded state transitions, retries, recovery, and secret exclusion.
- `63bbcd9c68a` - Signed and persisted offline-grant validation with device/scope/policy/tender/limit/session enforcement and focused tests.
- `8a8cc43adf4` - Idempotent offline cash-sale sync endpoint, canonical Finance orchestration, client dispatcher, explicit terminal decisions, and focused tests.
- `250a9d6c059` - Offline synchronization coordination checkpoint.
- `1c1f6ff5af0` - Mobile offline operator sync, retained-grant lifecycle, and safe response-loss replay.
- `f157ad02fe8` - Canonical Mobile POS till-session workflow, mobile UI, authorization, and focused tests.
- `ad635847819` - Server-derived Mobile POS till reconciliation and client presentation.
- `1a3d0ad98ec` - Cash declaration, pending-sync finalization controls, HQ day-end workflow/reporting, and canonical bank-deposit proposal integration.
- `7c172f8f47f` - Z92S printer/scanner capability contracts, Expo/Kotlin bridge, controlled external SDK packaging, sale-screen integration, and focused tests.

## Phase 2 application foundation

- Added `apps/mobile`, an Expo 54/React Native Android application with TEST/UAT/PRODUCTION profiles, production HTTPS enforcement, SecureStore-backed tokens and installation identity, typed API handling, login/MFA/tenant switching, device enrollment, bootstrap, dashboard, account switching, logout, and sanitized crash handling.
- Added dynamic `MobilePOS.*` permissions and permission policies without fixed role names.
- Added tenant-scoped Mobile POS stores, mandatory approved default walk-in customer/Customer-role mapping, Finance dimension defaults, tills, allowed payment methods, effective user-store assignments, persisted devices and assignment history, offline policies, and offline-grant foundation.
- Added administration/runtime APIs under `/api/administration/mobile-pos/v1` and `/api/mobile-pos/v1`.
- Added server-issued, signed offline grants requiring `MobilePOS.Offline.Use`, `MobilePOS.Till.Operate`, an open cashier session, an active store policy, and at least one policy-permitted operation. Grant capabilities are filtered through current dynamic permissions; offline tenders, amount/count limits, the approved default walk-in customer, policy version, and assignment bindings are snapshotted and hashed.
- Added Android SecureStore persistence for the current opaque grant and readable policy snapshot. The client invalidates it on expiry, logout, tenant change, or any tenant/user/device/store/till/session/policy/customer/revocation-epoch mismatch and exposes request/renewal state on the dashboard.
- Added a permission-gated HQ page at `/administration/mobile-pos` for stores, searchable walk-in customers, tills/tenders, offline policies, device approval/revocation, and searchable user-store assignment.
- Tenant selection now returns and persists a refresh token scoped to the selected tenant, preventing a subsequent refresh from reverting or failing against the original tenant context.

## Phase 3 Finance read slice

- Added dynamic `MobilePOS.Customer.View` authorization for customer search and outstanding-invoice access without restricting role names.
- Added a central eligible-customer query requiring tenant scope, active/effective Customer role, active and non-blacklisted Business Partner, approved/active registration, approved Business Partner status, and an effective approved AR profile.
- Blank customer search resolves only the active store's mapped default walk-in customer. A typed search returns matching approved transaction-ready customers and marks the store default.
- Added a device/store/till-scoped mobile Customers and balances screen. Operators may search approved customers and inspect their outstanding invoices; users without the permission receive an explicit disabled state.
- Outstanding invoices delegate to the existing canonical `IPaymentService` query after the selected customer and role pass the Mobile POS eligibility boundary.

## Phase 3 Finance producer routes

- Added the compiled `MobilePosCustomerInvoice` and `MobilePosCustomerPayment` Finance dimension routes with distinct external-producer contracts and source document types.
- Added a `MOBILEPOS` Finance module-lock identity that resolves to the tenant's Sales module without coupling authorization to a fixed role name.
- Extended the canonical AR invoice creation/approval boundary to accept the Mobile POS invoice route.
- Extended the canonical AR customer-payment boundary to accept the Mobile POS payment route, use its trusted destination line, and derive settlement allocation dimensions from the paired Mobile POS invoice route.
- This route milestone established route identity and canonical service acceptance only. The transaction foundation below now supplies sale/tender persistence and server idempotency; invoice/payment command orchestration and live producer-readiness certification remain outstanding.

## Phase 3 transaction and idempotency foundation

- Added tenant-scoped Mobile POS sale, immutable commercial line, and tender source envelopes with device, store, till, cashier-session, operator, customer, offline-grant, canonical invoice, canonical customer-payment, and split-tender sequence links.
- Added a persisted mutation receipt keyed by tenant, device, and client mutation ID. The server serializes and hashes the command type, schema version, and command payload rather than trusting a client-supplied fingerprint.
- Exact completed requests replay the stored result without invoking the handler again. Explicit business rejections are persisted and replayed. Reuse of a mutation key with a different command, schema, or payload is rejected as a conflict.
- Relational execution uses the provider execution strategy, a serializable database transaction, and the existing transaction-lock boundary so the future invoice/payment handler can participate in the same atomic unit of work.
- This milestone supplies the persistence and execution prerequisite. The online sale command below now uses it; relational concurrency, failure-injection, and live SQL/API evidence remain outstanding.

## Phase 3 online sale orchestration

- Added `POST /api/mobile-pos/v1/sales` behind the dynamic Mobile POS till/invoice/payment and canonical Finance invoice/payment permissions. No role name is hardcoded.
- The command revalidates the current device, effective store assignment, active store and till, open cashier session, customer eligibility, store/customer currency, item state, current sale price, revenue account, warehouse requirement, tender mapping, and external-reference policy inside the mutation transaction.
- A blank customer selection resolves to the store's approved default walk-in Business Partner and Customer role. An explicit approved customer remains supported.
- The command creates and posts the invoice through the canonical `IInvoiceService` Mobile POS producer. The server compares the canonical subtotal, tax, discount, and total with the client-confirmed values before any payment is created.
- Every split tender creates one canonical `CustomerPayment` through `IPaymentService` and allocates it directly to the invoice. Cash defaults to the till liquidity account; non-cash destination metadata remains subject to the canonical payment service.
- The Mobile POS sale, immutable lines, tender-to-payment links, canonical invoice link, and completed mutation result are persisted only after all canonical Finance calls succeed. Exact retries replay the stored result without repeating invoice or payment commands; explicit rejections are also replayed.
- This is the backend online-write slice. The canonical receipt projection described below now consumes its immutable source snapshot; SQL failure injection and live accounting reconciliation remain outstanding.

## Phase 4 online checkout

- Added `GET /api/mobile-pos/v1/catalogue/search` behind dynamic till and invoice permissions. It reuses the current device/store assignment, returns only active sale-ready items, preserves current price/UOM/tax-group data, and applies warehouse availability for stock items.
- Added `POST /api/mobile-pos/v1/sales/preview` behind the Mobile POS and canonical AR invoice permissions. It resolves the store default or selected approved customer, validates currency and items, applies governed line discounts, and delegates tax calculation to `ITaxCalculationEngine` without persisting a sale.
- Added the native mobile sale route with approved-customer override, governed catalogue search, cart quantity controls, canonical server preview, multiple till-mapped tenders, required-reference checks, exact tender-total reconciliation, and atomic completion.
- Completion derives commercial fields from the latest server preview and retains one client mutation ID/local reference across network retries. A changed cart, customer, preview, or tender set invalidates that pending identity.
- The success state displays the canonical invoice and payment identifiers returned by the server and then loads the canonical receipt projection described below. Printer output remains separate work.
- Bank-required payment methods now load a searchable list of active, non-deleted, GL-mapped accounts in the store currency and the current user's Finance operating scope. The mobile client receives the account name, bank, currency, and a masked number only.
- Completion revalidates the selected account inside the sale transaction before invoice creation. It rejects a missing account, an account on a method that does not permit one, conflicting bank/liquidity destinations, tenant/currency/status/GL failures, and accounts outside the operator's current Finance scope.
- The validated bank account is passed to the existing canonical `IPaymentService`; no Mobile POS-specific bank ledger or direct posting path was added.

## Phase 3 canonical receipt and Phase 4 receipt/reprint slice

- Added `GET /api/mobile-pos/v1/receipts/{saleId}` behind dynamic `MobilePOS.Access`. The service revalidates the current device and effective assignment, limits the receipt to the assigned store, and accepts only a completed sale with a canonical invoice and completed canonical payment links for every tender.
- The receipt is projected from the immutable Mobile POS sale, line, and tender snapshots together with canonical invoice and CustomerPayment identifiers. It includes tenant, store, location, till, session, device, cashier, customer, lines, totals, split tenders, safe external references, and a verification reference without exposing bank, liquidity-account, or internal posting details.
- Added `POST /api/mobile-pos/v1/receipts/{saleId}/reprint-events` behind both `MobilePOS.Access` and `MobilePOS.Receipt.Reprint`. It appends a tenant-scoped central `AuditLog` event keyed by device and client event ID. Exact retries return the same audit event and copy number.
- Reprinting never calls the invoice or payment services and therefore cannot create, repost, or reallocate Finance documents. No new migration was required because the existing immutable sale snapshot and append-only idempotent audit store satisfy this slice.
- The mobile success screen renders the canonical receipt, split-tender payment numbers, and a numbered `REPRINT` mark. The reprint action is hidden without the dynamic permission and explains that another copy only records audit evidence.
- Added a safe HTML receipt renderer that escapes business data and preserves the original or numbered `REPRINT` mark. Android system printing uses the rendered canonical receipt.
- Added PDF generation into the app's document-backed `receipts` directory and native PDF sharing. The filesystem-safe filename distinguishes the original from each audited reprint copy. This fallback does not depend on the Z92S vendor SDK.

## Phase 4 scanner boundary and Phase 7 SDK intake

- Added an Expo Camera barcode modal with explicit permission handling and EAN, UPC, Code 128, Code 93, Code 39, ITF-14, Codabar, Data Matrix, and QR formats.
- Added one normalized scanner contract for camera, manual/keyboard-wedge, later vendor, and deterministic test sources. Catalogue lookup reuses the existing search path instead of creating a second product-resolution path.
- Manual and keyboard-wedge entry remain available through the catalogue input and Enter/search action when camera permission is denied or hardware is unavailable.
- Management supplied `SmartPos_1.8.1_R231213_SDK.zip`; its SHA-256 is `8A5A259076030F17DFA629F413968B67CEBA55B02EA8B2018BF67F1B0C26AF66` and it contains 160 entries.
- The package documents `DriverManager`/`Sys` initialization, built-in `Printer` operations, and direct `HQrsanner` scanning for the Z90/Z91/Z92/Z100 family. Its SmartPos JAR and ARM64/ARMv7 JNI hashes are recorded in the architecture report and tracker.
- No licence, notice, or redistribution terms were detected. The vendor demo targets Android 26 while RHEMA targets Android 35. The native adapter remains scheduled for Phase 7 after Phase 4 through Phase 6; signed distribution and device claims require written redistribution approval and physical Z92S Android 14 evidence.
- Added `MobilePOS.Discount.Apply` to the dynamic permission catalogue. Positive line discounts are rejected by both server preview and final sale orchestration unless the current user's assigned role has that permission; no sales role name is hardcoded.
- The mobile line discount control is only rendered for authorized users. Every discount edit invalidates the earlier server preview and tender state, requiring canonical Finance recalculation before checkout.

## Phase 4 catalogue cache and Phase 5 SQLite/reference-cache foundation

- Added the permission-protected `GET /api/mobile-pos/v1/catalogue/changes` contract. Its opaque cursor fixes a server snapshot and page offset; the feed returns sale-ready upserts plus tombstones for inactive, deleted, or no-longer-postable items.
- Change selection includes both Inventory item timestamps and the assigned store warehouse's quantity timestamps so offline availability is refreshed as stock moves.
- Added the version-1 Expo SQLite database with WAL, foreign keys, explicit migration history, scoped catalogue/customer/payment-method/receipt/sync tables, and the durable outbox shape required by MPOS-0503. JWTs, refresh tokens, offline grant tokens, and signing secrets are excluded.
- Local cache scope binds tenant, user, device, store, and till. Each page is applied transactionally; an interrupted page cursor is retained, and the completed watermark advances only after the final page.
- The app warms the catalogue after bootstrap for authorized users and synchronizes it before retaining a new offline grant. A network-level catalogue search failure falls back to the scoped local cache and shows the operator that saved data is being used.
- Added a permission-protected approved-customer change feed using the same tenant/store-bound opaque cursor model. Customer-role, Business Partner, and approved AR-profile timestamps feed each snapshot; records that no longer satisfy the canonical Mobile POS customer-eligibility query are returned as tombstones.
- The client transactionally caches approved customer projections, bootstrap configuration, and the current till's payment-method policy under the same tenant/user/device/store/till scope. Approved-customer search falls back only on a network-level failure and clearly labels cached results.

## Phase 5 outbox, grant consumption, and synchronization

- Added a version-3 scope-bound SQLite outbox with durable draft/pending work, canonical JSON/SHA-256 hashes, exclusive dispatch claims, attempt timestamps, bounded retry, interrupted-claim recovery, explicit terminal results, and database triggers that reject illegal lifecycle transitions.
- Sensitive fields including access/refresh tokens, offline grant tokens, authorization headers, passwords, and signing secrets are rejected from outbox payloads. The dispatcher loads the signed grant from Android SecureStore only while sending.
- Added server-side offline grant consumption. Every command revalidates token signature, persisted grant status, recorded occurrence window, current user/tenant, device revocation epoch and assignment, policy snapshot hash, command schema, snapshotted tender rules, per-transaction/count/aggregate limits, and cashier-session ownership.
- Offline cash-sale grants now require the canonical Finance create/post/receive permissions as well as the Mobile POS permissions. Discount authority is snapshotted when the grant is issued rather than inferred from a later role state.
- Added `POST /api/mobile-pos/v1/sync/push` behind dynamic offline/till permissions. The server recomputes the canonical device payload hash, validates envelope identity and signed scope, and submits the sale through the same idempotent invoice, post, split-payment, and allocation transaction used online.
- Offline sale evidence records the grant ID, exact policy snapshot hash, device occurrence time, synchronization time, and offline flag on every tender. A valid recorded command may synchronize while its till session is pending independent review; final day-end control remains Phase 6.
- Added a client dispatcher that maps `Synced`, `Rejected`, and `Conflict` decisions to durable outbox states. Ambiguous network/401/timeout/rate-limit/server failures remain safely retryable under the same mutation ID, while grant-scope mismatch and incomplete responses require manual review.
- Added the mobile Sync & exceptions screen, queue counters, explicit sync action, terminal-decision detail, and dashboard navigation. Retryable checkout transport failures are queued under the same mutation identity and command type, preventing a response-loss retry from creating a second canonical sale.
- Replaced the single current-grant record with an encrypted grant-ID vault plus a separate current pointer. Logout, tenant change, and expiry clear active authorization while retained queued work can still load the exact historical signed grant that authorized its recorded occurrence; focused tests cover retention and deletion.

## Migrations and application state

- Migration created: `20261009054618_AddMobilePosFoundation`.
- Migration tables: stores, store dimension defaults, tills, till payment methods, user-store assignments, devices, device assignment histories, offline policies, and offline grants.
- Migration created: `20261009114817_AddMobilePosTransactionFoundation`.
- Transaction migration tables: Mobile POS sales, sale lines, tenders, and mutation receipts, including tenant/device idempotency identities and canonical Finance-document links.
- Migration created: `20261009235832_AddMobilePosTillCloseControl`.
- Till-close migration adds the policy-snapshotted Mobile POS close submission, pending-mutation integrity evidence, sync-exception resolution, finalization state, and tenant/session uniqueness controls.
- Migration created: `20261010003147_LinkMobilePosTillCloseBankDeposit`.
- Deposit-link migration adds the optional canonical Finance bank-deposit link and proposal audit fields to the till-close submission.
- No Mobile POS migration was applied to a database.
- No production deployment, service restart, live device enrollment, or provider call was performed.

## Phase 6 till and day-end implementation

- Added the first till-session mobile boundary. It resolves the authenticated device, effective store assignment, physical till, liquidity account, and current cashier before delegating session opening and live custody calculation to the existing `ICashierTillService`.
- The mobile runtime can now load the assigned operator's current Finance till session or open one with an opening float, note, and optional governed evidence file. The business date is calculated in the configured store time zone from server UTC rather than trusted device time.
- Both endpoints require the dynamic `MobilePOS.Till.Operate` permission and the canonical `Finance.CashTills.Operate` permission. The service rejects a Finance session returned for any liquidity account other than the assigned Mobile POS till.
- Added the mobile till screen and dashboard link with current session status, opening float, expected cash, transaction movements, deposits, count, variance, and denomination evidence. The app does not calculate or maintain a second custody balance.
- Added a server-derived session reconciliation for the current operator and assigned till. It groups completed tenders by configured payment method, identifies offline tenders, counts linked canonical CustomerPayments, reports pending/rejected/incomplete exceptions, and explicitly compares completed sales with completed tender totals. The mobile screen displays this explanation alongside the Finance custody balance.
- Added the device day-end declaration using the canonical Finance denomination count, variance reason/evidence, store/till/device scope, and the device's retained pending mutation IDs. Pending IDs are normalized, deduplicated, deterministically hashed, and persisted with the applicable pending-sync policy snapshot.
- Added a shared Finance finalization guard. It verifies the retained pending evidence, requires completed mutation receipts or an independent HQ exception resolution, stages finalization in the same DbContext transaction as Finance till approval, and records a return-for-recount state when Finance returns the session.
- Added an HQ day-end queue with permission-gated approval, return, and sync-exception controls. Maker/checker enforcement prevents the cashier from resolving or approving their own close.
- Added a historical till-close report and CSV export showing cashier/session, close state, sync evidence, variance, and canonical bank-deposit status.
- Added bank-deposit proposal creation only after the Mobile POS evidence is finalized and the Finance till session is closed. Eligible unallocated custody entries are delegated to the canonical `IBankingSettlementService`; retries recover the same Finance deposit through an immutable source marker and the existing destination-account/reference uniqueness control.

## Phase 7 Z92S hardware integration

- Added a local Expo Android module with a Kotlin reflection bridge for the audited SmartPos 1.8.1 `DriverManager`, `Sys`, `Printer`, and `HQrsanner` contracts. Reflection allows normal portable builds to compile without committing or redistributing proprietary vendor files.
- Added a controlled Expo config plugin. A Z92S prebuild must explicitly set `RHEMA_ZCS_ENABLED=true` and `RHEMA_ZCS_SDK_DIR`; the plugin rejects missing or hash-mismatched JAR/JNI artifacts, copies only the audited ARM64/ARMv7 files into the ignored generated Android project, and adds the app Gradle dependency.
- Added receipt-printer capability/result contracts, a deterministic fake adapter, compact canonical 32-column receipt text, and a Z92S printer adapter that checks initialization and printer status before sending the canonical receipt to the built-in printer. Android system print/PDF/share remain the portable fallback.
- Added a Z92S scanner adapter that owns SDK initialization, power, trigger, stop, and power-off lifecycle. It follows the vendor demo's keyboard-wedge behavior by focusing the catalogue input and converting the terminating input into the existing normalized vendor scan and catalogue lookup path. Camera scanning remains available.
- Hardware capability detection now reports `zcs-smartpos` through device enrollment and heartbeat when the packaged SDK exposes each capability, while ordinary builds report `system-print` and `camera-manual`. The account screen shows the active adapter keys.
- No SmartPos JAR or JNI binary is tracked. Written redistribution approval, a Java/Android native build environment, a signed APK, and a physical Android 14 Z92S remain required for acceptance.

## Verification evidence

- Git worktree created cleanly from the exact `origin/master` baseline.
- Root checkout and unrelated worktrees were left unchanged.
- Management responses were extracted with their question context; all highlighted responses were captured.
- Flash committed source and incident documents were inspected at `526acb8f46e5f003b493f1807d8cbd0372a88dda`; unrelated dirty Flash working-tree files were not read as implementation evidence or modified.
- RHEMA invoice, payment, payment-method, till, liquidity, banking, numbering, Finance-dimension, authorization, tenant, location, warehouse, device, and PWA boundaries were traced in the current source.
- All requested A-U architecture sections, management decisions, phase gates, data contracts, API proposals, migration plan, test plan, and blockers are present in the Phase 1 report.
- Full migration-aware API build passed with 0 errors; the build preserved 89 EF models and compiled the new migration metadata.
- The Phase 3 read-slice API rebuild passed with 0 errors after the customer eligibility, read service, controller, and permission changes.
- The Core and API Release builds passed with 0 errors after adding the Mobile POS Finance producer routes and AR integration guards.
- The migration-aware API Release build passed with 0 errors after the transaction foundation; it preserved 90 full EF models and compiled 9,047 distinct statements across two lookup contexts.
- Idempotent SQL generation from `20261008192833_AddPropertySalesOrderDepositLifecycle` to `20261009054618_AddMobilePosFoundation` passed. The 28,333-byte script contains nine `CREATE TABLE` statements and the expected stores, tills, devices, policies, and migration-history marker.
- Eighteen focused Mobile POS API tests passed. Coverage includes authorization/model contracts, customer eligibility/default resolution, canonical outstanding-invoice delegation and ineligible-customer rejection, store/customer persistence, assignment replacement, device lifecycle, the offline-and-till permission pair, signed grant issuance and supersession, permission-filtered commands, tender filtering, bounded snapshots, token round-trip, tamper rejection, and expiry rejection.
- Fifty-two focused Finance-route and Mobile POS API tests passed together, covering the new external contracts, module-lock mapping, invoice approval route, existing Mobile POS foundation, and customer/read behavior.
- Twenty-two focused Mobile POS API tests passed after the transaction foundation. The new coverage proves exact success replay, changed-payload conflict without handler re-execution, persisted explicit rejection replay, and the sale/line/tender/mutation uniqueness contracts.
- Twenty-five focused Mobile POS API tests passed after the online sale composition. The added cases prove successful default-walk-in sale orchestration with two canonical tenders, exact replay without repeating Finance commands, canonical-total drift rejection before payment, persisted rejection replay, and all seven endpoint authorization policies.
- Twenty-eight focused Mobile POS API tests passed after catalogue and preview were added. The added coverage proves case-insensitive sale-ready catalogue search, store default-customer preview, governed discount calculation, canonical tax-engine delegation, and endpoint permission policies.
- The Core and API Release builds passed with zero errors for the online sale DTO, service, controller, and dependency registration. Existing repository warnings and ImageSharp advisories remained warnings.
- Idempotent SQL generation from `20261009054618_AddMobilePosFoundation` to `20261009114817_AddMobilePosTransactionFoundation` passed. The generated script contains exactly the four expected transaction tables and the new migration-history marker.
- Frontend TypeScript check passed. Focused ESLint passed for the Mobile POS page/service and changed route/navigation/auth files.
- Frontend administration/access tests passed: 27 tests across the route guard, Settings registry, and navigation access helper.
- Frontend production build passed. The generated app manifest contains `/administration/mobile-pos/page`.
- Expo application TypeScript check passed. Twenty-eight client tests passed across environment policy, sanitized support references, ProblemDetails and non-JSON response handling, concurrent-401 single-flight refresh behavior, customer/outstanding-invoice request encoding, catalogue/preview/completion contracts, split-tender calculation/completion mapping, offline-grant API shape, expiry, assignment/policy/default-customer/revocation binding, and remaining-time calculation.
- Thirty-two focused Mobile POS API tests passed after the canonical receipt and audited-reprint slice. Added coverage proves the complete receipt projection, split-tender payment identifiers, cross-store denial, idempotent reprint audit, unchanged canonical sale state, and dynamic access/reprint authorization.
- The Expo TypeScript check and all 30 mobile tests passed after the receipt API client, receipt/reprint UI, safe printable renderer, and PDF fallback were added.
- The API Release compilation completed with zero errors for the receipt service/controller/DTO/DI changes. Existing repository warnings and ImageSharp advisories remained warnings.
- Expo Doctor is installed as a reproducible development dependency and passed 18/18 checks. Android bundle export passed again after adding the supported Expo file-system, print, and sharing modules, generating the Hermes bundle and metadata under the ignored `apps/mobile/dist/android` output.
- The Phase 4 scanner change passed the Expo TypeScript check and all 33 mobile tests, including normalization, empty/oversized scan rejection, and disposable adapter behavior. Expo Doctor passed 18/18 and the Android Hermes bundle export passed with the camera module included.
- The discount authorization slice passed the Release Mobile POS API test filter with 34/34 tests, including unauthorized preview and final-sale rejection before tax/invoice creation. The API, Core, Data, and test projects compiled with zero errors; existing repository warnings and ImageSharp advisories remained warnings. The Expo TypeScript check and all 33 mobile tests also passed.
- The governed bank-tender slice passed the Release Mobile POS API test filter with 24/24 tests after a fresh API/test build. Coverage includes Finance operating-scope filtering, active/GL-mapped/store-currency eligibility, masked account output, rejection before invoice creation when a required bank destination is missing, and propagation of an eligible account into the canonical payment command. The Expo TypeScript check and all 33 mobile tests passed, including the bank-account endpoint and completion request mapping.
- The catalogue/SQLite slice passed the Release Mobile POS API filter with 27/27 tests after a fresh build. New coverage proves offline/till/invoice authorization, upsert/tombstone projection, opaque multi-page continuation, and stable snapshot time. The Expo TypeScript check and all 39 mobile tests passed, including migration scope/token exclusion, resumable synchronization, cursor validation, search normalization, and API encoding. Expo Doctor passed 18/18 and the Android Hermes export completed with `expo-sqlite` bundled.
- The approved-customer/configuration cache slice passed the Release Mobile POS API filter with 29/29 tests after a fresh build. Added coverage proves dual offline/customer authorization, eligible customer upserts, ineligible-customer tombstones, and current canonical customer filtering. The Expo TypeScript check and all 40 mobile tests passed for the version-2 SQLite schema, customer/configuration cache, resumable customer cursor, and encoded client contract.
- The version-3 outbox passed the Expo TypeScript check and 45/45 mobile tests. Added coverage proves stable canonical payload hashing, secret rejection, legal lifecycle transitions, bounded retry, exclusive claims, interrupted-attempt recovery, and scope/identity uniqueness.
- Signed offline-grant consumption passed all 29 focused Mobile POS API tests after a fresh Release build, including expiry-at-sync with occurrence inside the signed window, revocation, policy-hash tamper, aggregate-limit, and tender rejection cases.
- Offline push and dispatch passed all 30 focused Mobile POS API tests after a fresh Release build plus the Expo TypeScript check and 50/50 mobile tests. Added coverage proves dynamic endpoint authorization, payload hash rejection before grant/Finance work, explicit idempotency conflict, canonical offline sale completion during pending review, persisted grant/policy/tender evidence, secure-grant-at-dispatch behavior, terminal state mapping, retry scheduling, and scope-mismatch manual review.
- The operator sync and historical-grant retention slice passed the Expo TypeScript check and all 53 mobile tests. Added coverage proves dispatcher lookup by queued grant ID, retained grant availability after the current authorization pointer is cleared, deliberate retained-grant deletion, and malformed grant-ID rejection.
- The fresh Release Mobile POS API build and focused test run passed 43/43 tests after unifying online and offline cash-sale mutation identity for safe response-loss replay. The build preserved 90 EF models, compiled 355,610 ordered migration statements and 9,047 distinct statements, and completed with existing repository warnings only.
- The Phase 6 till-session slice passed the Expo TypeScript check and all 54 mobile tests. A fresh Release API/test build passed 4/4 focused till-session tests covering dual permission enforcement, assigned-liquidity routing, store-local business date calculation, and cross-till rejection. Existing repository warnings and ImageSharp advisories remained warnings.
- The Phase 6 server-reconciliation slice passed the Expo TypeScript check and all 55 mobile tests. The fresh Release API/test build passed 6/6 focused tests, including exact completed-sale/tender totals, online/offline grouping, canonical-payment counts, rejected-sale exclusion, and sales-to-tender balance. Existing repository warnings and ImageSharp advisories remained warnings.
- The completed Phase 6 source passed a migration-aware Release build with zero errors after compiling 92 preserved EF models and 9,053 distinct statements. Existing repository warnings and ImageSharp advisories remained warnings.
- Idempotent SQL generation from `20261009114817_AddMobilePosTransactionFoundation` through `20261010003147_LinkMobilePosTillCloseBankDeposit` passed. The 14,621-byte script contains the till-close table, canonical bank-deposit link, and both migration-history markers.
- Twenty focused till/day-end tests passed. Coverage includes dual endpoint permissions, cash declaration policy, deterministic pending evidence, maker/checker controls, unresolved-sync blocking, completed-sync finalization, return-for-recount propagation, report/deposit permissions, canonical custody-entry proposal mapping, and deposit linkage.
- The Expo TypeScript check and all 56 mobile tests passed after the cash declaration and close-submission API/UI work. The HQ frontend TypeScript check also passed after the day-end queue, report, CSV export, and bank-deposit proposal controls.
- The supplied ZCS archive hash, entry count, JAR/JNI hashes, documented public printer/scanner APIs, ABIs, sample target, and absence of detected licence files were checked read-only. No vendor binary was added to source control.
- The Phase 7 source passed the Expo TypeScript check and all 68 mobile tests. New coverage includes compact receipt text, printer capability/fake behavior, ZCS printer submission, scanner power/trigger/stop lifecycle, vendor wedge normalization, runtime capability selection, device heartbeat reporting, plugin gating, and rejection of missing or hash-altered SDK files.
- Expo Doctor passed 18/18 and the Android Hermes export completed after adding the local module. An SDK-enabled clean Expo prebuild succeeded, autolinking resolved `com.rhemasystems.zcssmartpos.ZcsSmartPosModule`, the generated app Gradle file contained the controlled JAR dependency, and all three packaged artifacts matched the audited SHA-256 hashes.
- The verification host has no Java runtime or Android SDK, so native Gradle/Kotlin compilation was not claimed. That gate remains explicit rather than treating JavaScript export or Expo prebuild as a compiled APK.

## Known failures and constraints

- The Flash ERP reference checkout is intentionally dirty. Read committed files through Git object paths and do not clean, reset, stash, or commit that checkout.
- The ZCS SmartPos 1.8.1 printer/scanner SDK is supplied and audited, but its package has no detected redistribution terms and its Android 14 behavior has not been certified on a physical Z92S. Phase 7 adapter source and the external-SDK build hook are implemented; vendor binaries remain outside source control and distributable builds remain gated by written approval.
- The current verification host has no Java runtime or Android SDK, so the generated Android project and Kotlin bridge have not passed a native Gradle build here.
- Native Android keystore behavior, visible device enrollment/remote-disable flow, and physical Z92S behavior require an Android device and later acceptance stages.
- Offline grant consumption and cash-sale Finance orchestration are implemented for schema version 1. Physical disconnect/reconnect, concurrent SQL Server aggregate consumption, and process/APK persistence remain unverified outside the automated in-memory and client test boundaries.
- The repository currently reports pre-existing ImageSharp package advisories and compiler warnings; the verified Mobile POS builds completed with zero errors.

## Remaining work

Complete the remaining Phase 2 acceptance evidence: physical Android secure-storage/auth/enrollment and remote-disable flow, visible HQ browser verification after applying the migrations in an authorized test database, and relational concurrency coverage for effective assignments. Complete live SQL/API/device evidence for the Phase 3 customer, invoice read, producer-route, online sale, catalogue, preview, receipt, and mobile checkout paths. Continue Phase 4 with SQL Server concurrency/failure-injection coverage, end-to-end Finance reconciliation evidence, and visible Android checkout/receipt acceptance. Finish Phase 5 with forced disconnect/retry/process-restart and signed APK upgrade preservation evidence. Apply and exercise the Phase 6 migrations in an authorized test database and capture authenticated browser/device/Finance workflow evidence. Complete the Phase 7 native Gradle build and signed Z92S printer/scanner certification after written SDK redistribution approval and access to an Android 14 Z92S unit; MPOS-0704 Bluetooth ESC/POS remains planned after supported printer models are confirmed.

## Authorization boundaries

The user authorized proceeding with the mobile implementation plan and Phase 1 architecture work. Read-only audits, documentation, tests, and isolated branch commits are authorized. Production deployment, live database migration, provider enrollment, payment capture, device enrollment, and physical Z92S certification require their relevant implementation stage and evidence.
