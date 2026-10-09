# RHEMA Field POS and Revenue Collection mobile architecture

**Status:** Phase 1 architecture gate complete

**Date:** 2026-10-09

**Target baseline:** `rhema-systems/RHEMA-ERP` `9044371f533ee3fac77472d74aecfe006e8c6872`

**Reference baseline:** `flashyjunior/Flash-ERP` `526acb8f46e5f003b493f1807d8cbd0372a88dda`

## 1. Purpose and governing principles

This document defines the implementation boundary for an Android Field POS and Revenue Collection application that uses RHEMA ERP as its accounting, customer, security, tenant, numbering, audit, and settlement authority.

The design follows these rules:

1. Mobile transactions create normal RHEMA invoices, customer payments, allocations, liquidity entries, posting events, journals, and banking records.
2. The mobile app does not own a second accounting ledger or reproduce tax and posting rules.
3. A server-side mobile command layer coordinates existing Finance services atomically and idempotently.
4. Tenant, store, till, device, user, and permission scope are validated on every command.
5. Offline capability is bounded by a signed, expiring server authorization and tenant-configured limits.
6. Final invoice and receipt numbers come from RHEMA numbering. Offline references are provisional and remain traceable after synchronization.
7. Role names are never hardcoded. Access comes from permissions assigned through the existing dynamic role model.

## 2. Confirmed management policy

The following decisions are approved inputs to implementation:

- Each cashier or collector opens a separate till session. Users do not share the same session.
- A physical till can have only one `Open` or `PendingReview` custody session at a time.
- Opening float is configurable.
- Till closure is permission controlled.
- Shortage and excess require an HQ approval workflow.
- A tenant may allow a day-end submission with pending synchronization, but final closure and settlement remain blocked until the server has reconciled the pending work.
- Sales stores map to company/location and cost-centre context. Transactions retain store, till, device, user, and Finance dimension evidence.
- Each sales store has exactly one active default walk-in customer mapping.
- The mapped walk-in customer must be an approved Business Partner with an active Customer role and effective AR profile.
- The app automatically selects that default customer for walk-in sales. The operator may search and choose another approved customer.
- Customer creation is unavailable in the mobile app, online and offline.
- Approved offline operations are cash sale, cash receipt, cached customer and product lookup, provisional receipt, partial payment, and permission-controlled return/reversal.
- The default offline authorization window is eight hours and is tenant configurable.
- Offline amount, count, age, and aggregate exposure limits are configurable. No thresholds are embedded in code.
- Any configured tender may be made available offline by policy. Electronic payments recorded offline require an external authorization/reference already obtained outside the app.
- Direct provider processing for Mobile Money or card is online only.
- Offline documents use a unique local reference and receive an authoritative RHEMA number after synchronization.
- The app supports both immediate item/service sales and collection against existing outstanding invoices.

## 3. Decision summary

| Area | Decision |
| --- | --- |
| Mobile framework | Expo/React Native in a new `apps/mobile` workspace, with native prebuild capability for printer/scanner adapters |
| Android policy | Framework compatibility may extend lower, but supported production baseline is Android 10/API 29; primary certification is Android 14 on ZCS Z92S |
| Accounting | Existing RHEMA AR and Finance services remain authoritative |
| Immediate POS sale | Server command creates and posts the invoice, creates one receipt per tender, allocates tenders, and returns the receipt projection in one serializable transaction |
| Walk-in identity | Store-specific approved default Business Partner, automatically selected |
| Named customer | Search and choose from approved, active customers; no mobile creation |
| Split tender | Multiple normal `CustomerPayment` records grouped by one mobile sale, never one fake payment method |
| Till | Existing `LiquidityAccount` of type `CashTill` plus `CashierTillSession`, linked through a Mobile POS store/till configuration |
| Store | New Mobile POS store configuration references existing HR `Location`, optional Inventory `Warehouse`, Finance dimensions, default customer, currency, and till(s) |
| Day-end | Existing till custody and bank deposit services are extended with mobile sync/tender evidence; day-end remains till-based |
| Offline | SQLite catalogue/cache plus transactional outbox; signed, expiring offline grant; server idempotency registry |
| Printing | `ReceiptPrinter` abstraction with ZCS, Bluetooth ESC/POS, Android print, PDF/share, and digital fallbacks |
| Security | Existing JWT/MFA/session/tenant contract plus persisted mobile device enrollment and revocation |
| Server URL | Managed environment profiles; production uses HTTPS only and does not permit arbitrary operator-defined endpoints |

## A. Flash mobile technology stack

The committed Flash mobile reference uses:

- Expo SDK 54.0.33.
- React Native 0.81.5 and React 19.1.0.
- Expo Router 6 with TypeScript.
- `expo-camera` for scanning.
- `expo-secure-store` for secrets and small secure values.
- `expo-sqlite` for local durable state.
- `expo-file-system`, `expo-print`, and `expo-sharing` for receipt output.
- `expo-local-authentication` for biometric app unlock.
- EAS preview APK and production AAB profiles.
- Explicit Metro singleton resolution to prevent duplicate React and React Native runtimes in the monorepo.

RHEMA should use the same general toolchain after running `expo install --check` against the versions selected when Phase 2 begins. Version numbers must be aligned at implementation time rather than copied indefinitely from the reference.

The Flash setting `usesCleartextTraffic: true` is a lesson, not a production default. RHEMA production must require HTTPS. A narrowly controlled development build may opt into local HTTP through an environment-specific config plugin.

## B. Flash stable mobile architecture

The useful reference patterns are:

- An Expo Router shell with protected navigation after session hydration.
- A mobile API adapter that distinguishes online, offline, and automatic modes.
- Secure token storage separated from SQLite business data.
- SQLite tables for master-data cache and durable outbox.
- Outbox states such as `Pending`, `Syncing`, `Synced`, and `Failed`.
- A validated session snapshot before navigation reports login success.
- File-backed receipt persistence rather than placing large receipt data in SecureStore.
- Camera scanning with manual search fallback.
- A permissions/route access map.
- A safe diagnostics surface that excludes credentials and tokens.
- Automated browser-level flow checks for fast feedback, followed by native-device certification.

RHEMA will reuse these patterns while replacing Flash endpoint contracts and business objects with RHEMA-specific contracts.

## C. Flash defects and controls to carry forward

| Reference failure | Required RHEMA control |
| --- | --- |
| Runtime import of `ErrorUtils` crashed release startup | Read guarded runtime globals only; install a root error boundary without Node-only APIs |
| Expo packages did not match the SDK | Use `npx expo install` and enforce `expo install --check` |
| Two React copies entered the production bundle | Add Metro singleton resolution and a release regression check |
| Login accepted a token although the session profile was invalid or unsaved | Login succeeds only after token, validated profile, tenant, store/till bootstrap, and secure persistence succeed |
| MFA challenge was treated as a dead end | Implement RHEMA's actual MFA verification flow before protected navigation |
| Cached operator/session returned after logout | Clear local access first; generation-guard late responses; use deletion tombstones during the process lifetime |
| HTTP rejection was queued as an offline write | Queue only transport failures or explicit offline mode; retain server rejections as terminal/actionable failures |
| Malformed analytics crashed the dashboard | Validate and normalize every dashboard DTO |
| Receipt hooks were conditional | Keep hooks unconditional and test loading, missing, and loaded receipt states |
| Receipt payload exceeded SecureStore capacity | Store only secrets in SecureStore; use file/SQLite receipt cache |
| Missing fixtures caused long UI-gate stalls | Return deterministic fixture errors quickly and fail on unmatched requests |
| Browser tests were mistaken for device certification | Keep separate Expo Web, emulator, ordinary Android, and Z92S gates |
| Update guidance suggested uninstalling | Sign consistently and migrate local storage in place so outbox and config survive upgrades |

## D. RHEMA authentication architecture

RHEMA already supplies the correct authentication authority through `AuthController`:

- Username/password login with optional tenant code.
- JWT access tokens and refresh tokens.
- User sessions with server-side termination and logout.
- Tenant selection and tenant-scoped token issuance.
- MFA challenge response through `RequiresTwoFactor` and a temporary two-factor token.
- Active-user enforcement.
- Active `UserTenant` membership and expiry checks.
- Concurrent session policy and security settings.

Mobile implementation requirements:

1. Model every login state explicitly: unauthenticated, primary credentials accepted, MFA required, tenant selection required, bootstrap required, authenticated, offline-authorized, revoked.
2. Store access/refresh tokens only in Android Keystore-backed secure storage.
3. Validate the `/auth/me` or equivalent session snapshot before saving the session.
4. Clear local access immediately on logout; remote logout completes afterward.
5. Refresh tokens through RHEMA's actual endpoint and treat revoked/expired sessions as authentication failures, never connectivity failures.
6. Bind the mobile bootstrap and offline grant to tenant, user, device, store, till, and session.
7. Require environment identity from the server and display Production, UAT, or Test on login, dashboard, and account.

## E. RHEMA Finance AR invoice architecture

`InvoiceService` and `/api/ar/invoices` already enforce the customer and accounting boundary:

- Invoice creation resolves a Business Partner Customer role and effective AR profile.
- Lines support product/service identity, quantity, unit price, tax group/treatment, discount, currency, inventory warehouse/location, and Finance dimension input.
- Currency and exchange-rate evidence are resolved by the backend.
- Credit limits and customer balances are validated by the backend.
- Invoice numbers come from the document-numbering service.
- Posting uses the central Finance posting engine and source-document dimension routes.
- Voids/reversals use governed Finance actions.

The mobile app must send business input and render the server calculation preview. It must never reproduce Ghana taxes, exchange rates, account selection, or journal construction locally.

For immediate POS sale, the server mobile command should use a dedicated producer route such as `MobilePosCustomerInvoice`. That route must resolve certified store/cost-centre dimensions and call the same invoice service/posting engine. It must not call the public controller internally.

## F. RHEMA AR payment and receipt architecture

`PaymentService` and `/api/ar/payments` already provide:

- Customer and AR profile resolution.
- Tenant-configured payment method resolution.
- Cash/bank/liquidity destination resolution.
- Reference enforcement.
- Allocation against one or multiple outstanding invoices.
- Partial allocation.
- Currency and realized-FX handling.
- Source-document dimension capture and freezing.
- Central posting.
- Controlled reversal, clearing, bounce, and trace evidence.
- A serializable transaction covering source row, allocations, operational liquidity evidence, and posting output.

The existing `PaymentCreateDto` represents one payment method per `CustomerPayment`. Split tender therefore maps to several `CustomerPayment` records, each with its own amount, method, destination, and evidence, allocated to the same invoice or invoice set.

The existing service transaction is atomic for one receipt. It is not sufficient by itself for a multi-receipt POS checkout. Phase 3 will add a server orchestration service that owns the outer serializable transaction and invokes composable invoice/payment cores so all tenders succeed or all roll back. The public mobile endpoint must not loop over independent HTTP calls.

## G. RHEMA payment method architecture

The tenant-scoped `PaymentMethod` master supplies:

- Code, name, type, active state.
- Whether a bank account is required.
- Whether an external reference is required.
- Default GL account mapping.

The catalogue includes standard seeded examples, but the mobile app must load active configured records rather than hardcode tender names.

Mobile-specific eligibility belongs in an additive mapping/policy because existing payment methods do not express every mobile constraint. The mapping must include:

- Store/till availability.
- Online/offline availability.
- External-reference rules.
- Change-giving behavior.
- Provider integration identifier, without storing provider secrets in the client.
- Display order and optional icon key.
- Destination liquidity/bank account override where Finance permits it.

No PAN or CVV is accepted or stored. Card and Mobile Money provider integrations store safe provider reference, authorization outcome, and tokenized evidence only.

## H. RHEMA cash and banking settlement architecture

RHEMA already has the principal controls:

- `LiquidityAccount` represents CashTill, bank, and other controlled liquidity channels.
- `LiquidityAccountEntry` is the operational subledger entry used for cash custody and settlement.
- `CashierTillSession` records till, cashier, business date, opening float, count, expected amount, variance, evidence, review, close, cancellation, and correction lineage.
- `CashierTillService.OpenSessionAsync` uses a serializable transaction and blocks another active custody session for the same physical till.
- Only the cashier owning the session can perform owner actions.
- Closure and correction permissions already exist.
- `BankDepositBatch` and `BankingSettlementService` govern deposit preparation, approval, posting, and confirmation.
- Posted deposits, rather than draft allocations, reduce expected physical till cash.

Required extension:

- Link a configured Mobile POS till to one CashTill liquidity account.
- Link every mobile sale/receipt to its `CashierTillSession`.
- Capture all tender totals and pending-sync exposure for session reconciliation.
- Generate a bank deposit proposal from eligible cash custody entries after approved till close; retain the existing banking workflow.
- Keep day-end grouped by till/session. Operator remains audit detail.

## I. RHEMA security, session, and device architecture

RHEMA's permission handler already verifies:

- Authenticated user.
- Active user.
- Current tenant claim.
- Active and unexpired tenant membership.
- Dynamic role-permission assignments.
- SuperAdmin bypass under the established platform rule.

The current device surface manages user sessions, but it is not a persisted mobile enrollment system:

- `/api/Device/my-devices` and session termination are useful session views.
- `/api/Device/trust` returns HTTP 501 because no trust store exists.
- Suspicious activity currently returns an empty placeholder.

Phase 2 must introduce tenant-scoped mobile device enrollment with pending approval, activation, suspension, revocation, reassignment history, heartbeat, app/OS version, and last-sync evidence. A device can request enrollment but cannot approve itself or create a trusted till.

## J. Existing RHEMA mobile, PWA, and offline components

The current web manifest and PWA infrastructure are oriented to general/maintenance use. RHEMA also has project mobile pages, inventory mobile scanning, and mobile workflow inbox/actions. They provide UI and API conventions but do not form an offline, native Field POS accounting client.

The Field POS therefore needs a dedicated `apps/mobile` client. Shared types may be generated or maintained from contracts, but the app must not import Next.js runtime code or web React dependencies.

## K. Proposed `apps/mobile` architecture

```text
apps/mobile
  app/                       Expo Router screens and protected route groups
  components/                POS-native UI components
  features/
    auth/                    login, MFA, tenant selection, session
    bootstrap/               device/store/till/policy configuration
    dashboard/
    customers/
    catalogue/
    invoices/
    collections/
    checkout/
    receipts/
    tills/
    sync/
    diagnostics/
  infrastructure/
    api/                     typed RHEMA client and ProblemDetails handling
    secure-storage/          token and installation-secret adapter
    database/                SQLite schema and migrations
    outbox/                  transactional outbox and retry state machine
    connectivity/
    logging/                 sanitized diagnostics
    printing/                printer interfaces and adapters
    scanning/                camera, keyboard wedge, vendor boundary
  contracts/                versioned mobile API DTOs
  tests/
  plugins/                   native/config plugins when required
```

Key implementation rules:

- Feature code calls typed use cases, not raw `fetch`.
- Server ProblemDetails retain `detail`, `code`, and correlation ID in the UI/diagnostics.
- SQLite writes business state and outbox messages in one local transaction.
- Secure storage contains secrets and small device credentials only.
- All cached rows include tenant, device/store scope, server version, fetched time, and expiry.
- Navigation becomes available only after authentication and bootstrap state validate.

## L. Proposed store, till, and device model

### `MobilePosStore`

- `TenantId`
- `Code`, `Name`, `Status`
- `CompanyProfileId` where the tenant uses a company profile
- `LocationId` referencing the existing HR operating location
- `WarehouseId` and optional default warehouse location for stock sales
- `CurrencyCode`, `TimeZoneId`
- `DefaultWalkInBusinessPartnerId`
- `DefaultWalkInBusinessPartnerRoleId`
- configured Finance dimension assignments, including cost centre where required
- default receipt profile and offline policy
- created/updated audit fields and row version

The service validates that the default customer is tenant-owned, active, approved, has an active Customer role, and has an effective AR profile. Deactivation or expiry of that customer blocks new walk-in sales until the store mapping is corrected.

### `MobilePosTill`

- tenant and store
- unique till number and name
- CashTill `LiquidityAccountId`
- status: Draft, Active, Suspended, Retired
- receipt profile
- allowed payment-method mappings
- last activation/heartbeat
- row version

Till identity belongs to the collection station, not the operator.

### `MobilePosUserStoreAssignment`

- tenant, user, store
- effective from/to
- active state and audit reason
- filtered uniqueness so a user has at most one current active sales-store assignment

### `MobilePosDevice`

- tenant and server-generated device ID
- application-generated installation identifier hash
- public key/attestation metadata where available
- platform, model, OS and app version
- status: Pending, Active, Suspended, Revoked, Retired
- assigned store and till
- printer/scanner capability profile
- enrolled/approved/revoked by and timestamps
- last seen, last sync, last IP summary where policy permits
- row version

### `MobilePosDeviceAssignmentHistory`

Immutable device/store/till assignment history with reason and author.

### `MobilePosSale`

- tenant, store, till, till session, device, operator
- client mutation ID and local reference
- selected customer and whether it was the store default
- canonical invoice ID/number
- totals and currency snapshots
- status and sync metadata
- offline grant/policy snapshot IDs

### `MobilePosTender`

- sale/collection group
- sequence, payment method, amount, reference/evidence
- destination liquidity/bank account
- canonical customer-payment ID/number
- provider status/reference where used
- offline-recorded flag

This is an orchestration/audit envelope. It never replaces the canonical Invoice or CustomerPayment.

## M. Proposed till session and day-end model

Reuse `CashierTillSession` for custody. Add mobile linkage and projections rather than a second till-close ledger.

Flow:

1. Authenticated, enrolled user opens a session on the device's assigned till.
2. Server confirms the user's active store assignment, till/device status, permissions, business date, currency, and opening-float policy.
3. Every mobile financial command carries the till session ID.
4. Session reconciliation reports canonical invoice/receipt counts, tender totals, reversals/refunds, cash custody movements, posted deposits, pending/failed sync exposure, and exceptions.
5. Cashier freezes the activity cutoff and submits declared cash.
6. Variance is calculated, never silently posted as a balancing adjustment.
7. Within-policy zero/tolerated variance may follow configured approval rules. Short/excess routes to an HQ reviewer with segregation of duties.
8. A tenant may submit while transactions remain locally pending. Final closure, bank-deposit proposal, and immutable totals wait until all in-window messages are reconciled or an explicit exception workflow resolves them.
9. Approved closure can propose a `BankDepositBatch`; existing submit, approve, post, and confirm steps remain authoritative.

## N. Proposed offline and synchronization architecture

### Offline authorization

The server issues a signed offline grant containing:

- grant ID/version
- tenant, user, device, store, till, and active till-session IDs
- issued/expiry times, defaulting to no more than eight hours under current policy
- allowed command types
- per-transaction, aggregate, count, and age limits
- allowed payment-method IDs and evidence rules
- catalogue/customer/policy version watermarks
- revocation epoch

The Phase 2 implementation uses a versioned compact HMAC-SHA256 token whose key is purpose-derived from `MobilePos:OfflineGrantSigningKey` (or the existing deployment JWT secret when no dedicated key is configured). The readable policy snapshot is returned separately, and its SHA-256 hash is included in the signed token. The signing key never leaves the API. The Android client keeps the current token and snapshot in Keystore-backed SecureStore, rejects a cached grant when its tenant, user, device, store, till, session, policy, default walk-in customer, revocation epoch, or expiry no longer matches bootstrap, and clears it on sign-out or tenant change. Synchronization must still verify the signature and the persisted grant status before consuming any queued command.

The app refuses new offline work after expiry or local limit exhaustion. It may still display and synchronize existing work.

### Local database

SQLite contains:

- schema/version metadata
- store/till/bootstrap snapshot
- approved customer search subset
- product/service catalogue and tax display projection
- payment-method policy
- receipt projection/cache
- outbox, attempts, results, and sanitized errors
- sync cursors and server tombstones

PII is minimized. Tokens and signing secrets never enter SQLite. SQLCipher feasibility is evaluated during Phase 2 prebuild work; its adoption must be proven compatible with Expo SDK and required native hardware modules.

### Outbox state machine

`DraftLocal -> Pending -> Syncing -> Synced`

Terminal/action states include `Rejected`, `Conflict`, and `ManualReview`. Network failure returns to `Pending` with bounded exponential retry. An HTTP business or authorization rejection is recorded and shown; it is not relabeled as offline success.

Every message includes client mutation ID, tenant, device, store, till, session, local reference, type, schema version, timestamps, offline grant ID, payload hash, attempt count, and server result.

### Idempotency

The backend stores a tenant-scoped `MobileMutationReceipt` keyed by device and client mutation ID. It records request hash, status, canonical results, and error outcome. Repeating an identical completed request returns the same result. Reusing a key with a different hash is rejected and audited.

### Ordering and conflicts

- Till open precedes work for that session.
- Customer/catalogue rows use server version or ETag watermarks.
- A sale payload snapshots price/tax inputs, but the server recalculates and rejects or requests operator confirmation when policy disallows drift.
- A return/reversal references the canonical original and rechecks current authority on sync.
- Day-end finalization waits for all messages at or before the cutoff.

## O. Proposed receipt and printer architecture

```text
ReceiptRenderer
  -> canonical ReceiptDocument
ReceiptPrinter
  -> ZcsBuiltInPrinterAdapter
  -> BluetoothEscPosPrinterAdapter
  -> AndroidPrintAdapter
  -> PdfShareAdapter
  -> DigitalReceiptAdapter
```

`ReceiptDocument` is generated from canonical server results or a clearly marked provisional offline projection. It includes tenant, store/location, till, session, cashier, customer, lines, taxes, discounts, total, tender breakdown, references, and QR/reference code. Sensitive account metadata is excluded.

Print failure does not reverse or duplicate a successful financial transaction. Reprint reads the existing receipt and writes an audit event; it never posts again. The rendered copy is marked `REPRINT` under policy.

## P. ZCS Z92S integration requirements

The initial repository audit found no ZCS SDK, AIDL definition, service contract, intent documentation, or signed vendor library, so the implementation must never fabricate a device contract. On 2026-10-09 management supplied `SmartPos_1.8.1_R231213_SDK.zip` (SHA-256 `8A5A259076030F17DFA629F413968B67CEBA55B02EA8B2018BF67F1B0C26AF66`) for later Phase 7 integration.

The supplied package contains the SmartPos 1.8.1 JAR, ARM64 and ARMv7 `libSmartPosJni.so` libraries, English/Chinese guides, and a demonstration application. The documented Z90/Z91/Z92/Z100 flow initializes `DriverManager`/`Sys`, exposes printer status/text/bitmap/QR/start APIs through `Printer`, and exposes direct hardware scanning through `HQrsanner`. The relevant artifact hashes are:

- SmartPos JAR: `3A65BF1A26D59730C014D79BA7B5AA8744BAB6E0B5275A2C055B996F4A7277E9`.
- ARM64 JNI library: `DE5CBF76EAFC3D0FBF1767BD0E8007E6217A2C81A6FDACE02FBD511A1D9FF9BF`.
- ARMv7 JNI library: `7D8821DD051F83072023744019F1EDD86AB7CF0B0EAE2D7FD674DF7ED844A090`.

SDK receipt does not move the hardware adapter ahead of the tracker. Phase 4 through Phase 6 remain the active sequence; the Kotlin/Expo native bridge belongs to MPOS-0702/0703. The package contains no detected licence, notice, or redistribution terms, its demonstration app targets Android 26 while RHEMA targets Android 35, and no physical Z92S evidence is available. Distribution and production claims therefore remain gated by written redistribution approval and Z92S Android 14 certification.

Required from ZCS or the device supplier:

1. Printer SDK/AAR/JAR or AIDL/service/intent contract for the exact Z92S Android 14 firmware.
2. Sample application and licensing/redistrubution terms.
3. Printer status, paper, temperature, cutter, alignment, barcode/QR, and image APIs.
4. Scan-engine intent/broadcast/SDK documentation if a hardware scanner is fitted.
5. Supported ABI, min/target SDK, ProGuard/R8 rules, and lifecycle guidance.
6. A physical Z92S development/certification unit.

The Phase 4 scanner boundary supplies normalized camera/manual/keyboard-wedge input and a disposable test adapter. When Phase 7 begins, a Kotlin native module and Expo config plugin can adapt the supplied `Printer` and `HQrsanner` APIs without changing checkout or receipt use cases.

## Q. Permissions

The final names will be registered in the shared catalogue and assigned through dynamic roles. No role-name checks are added.

Proposed permissions:

| Permission | Purpose |
| --- | --- |
| `MobilePOS.Access` | Enter the mobile POS application |
| `MobilePOS.Store.View` | Read assigned store configuration |
| `MobilePOS.Store.Manage` | Administer stores, customer mapping, dimensions, and policies |
| `MobilePOS.Device.Enroll` | Request enrollment for the current device |
| `MobilePOS.Device.Approve` | Approve, map, suspend, or revoke devices |
| `MobilePOS.Till.Operate` | Use assigned till and open own session |
| `MobilePOS.Till.Close` | Submit own till count/day-end |
| `MobilePOS.Till.Review` | Review variance and approve/return close |
| `MobilePOS.Till.Correct` | Open governed correction sessions |
| `MobilePOS.Invoice.Create` | Create mobile invoices/sales |
| `MobilePOS.Invoice.Post` | Authorize immediate mobile posting where policy allows |
| `MobilePOS.Payment.Collect` | Create and allocate customer receipts |
| `MobilePOS.Discount.Apply` | Apply a line discount during Mobile POS checkout |
| `MobilePOS.Receipt.Reprint` | Reprint existing receipt |
| `MobilePOS.Return.Create` | Initiate a return/credit workflow |
| `MobilePOS.Reversal.Create` | Initiate a controlled reversal |
| `MobilePOS.Offline.Use` | Operate within a server-issued offline grant |
| `MobilePOS.Sync.Resolve` | Review and resolve sync conflicts/exceptions |
| `MobilePOS.Reports.View` | View HQ mobile POS reports |

Each command also requires the relevant existing Finance permission, or a narrowly defined server composition policy that maps the mobile permission to the underlying Finance operation. Mobile permissions do not bypass Finance data scope, period, workflow, or posting rules.

## R. Database migrations

Planned additive migrations:

1. Mobile POS permission catalogue and route policies.
2. `MobilePosStores` plus store dimension/default-customer configuration.
3. `MobilePosTills` and payment-method mappings.
4. Effective-dated `MobilePosUserStoreAssignments`.
5. `MobilePosDevices` and immutable assignment history.
6. `MobileOfflinePolicies` and versioned/snapshotted grants.
7. `MobilePosSales`, `MobilePosTenders`, and collection grouping/linkage.
8. `MobileMutationReceipts` for server idempotency.
9. Receipt/print audit and heartbeat/sync evidence where existing audit storage is insufficient.
10. Optional fields/relations on `CashierTillSession` only where a separate link entity cannot express the requirement cleanly.
11. New certified Finance dimension producer routes for Mobile POS invoice, receipt, and bank-deposit sources.

Migration controls:

- Tenant keys and filtered uniqueness are explicit.
- Foreign keys prevent cross-tenant references at service and, where practical, database boundaries.
- Current active default customer and user-store assignment uniqueness are enforced.
- No destructive replacement of existing AR, banking, or till data.
- Migration, model snapshot, SQL deployment, and rollback evidence are required before integration.

## S. API endpoints

All endpoints are versioned, tenant scoped, permission protected, return ProblemDetails, and accept cancellation tokens.

### Mobile runtime

- `POST /api/mobile-pos/v1/devices/enrollment-requests`
- `GET /api/mobile-pos/v1/bootstrap`
- `POST /api/mobile-pos/v1/offline-grants`
- `GET /api/mobile-pos/v1/catalogue/changes`
- `GET /api/mobile-pos/v1/customers/search`
- `GET /api/mobile-pos/v1/customers/{id}/outstanding-invoices`
- `GET /api/mobile-pos/v1/payment-methods`
- `GET /api/mobile-pos/v1/dashboard`
- `POST /api/mobile-pos/v1/till-sessions/open`
- `GET /api/mobile-pos/v1/till-sessions/current`
- `POST /api/mobile-pos/v1/sales/preview`
- `POST /api/mobile-pos/v1/sales`
- `POST /api/mobile-pos/v1/collections`
- `POST /api/mobile-pos/v1/sync/push`
- `GET /api/mobile-pos/v1/sync/pull`
- `POST /api/mobile-pos/v1/till-sessions/{id}/submit-count`
- `GET /api/mobile-pos/v1/till-sessions/{id}/reconciliation`
- `GET /api/mobile-pos/v1/receipts/{id}`
- `POST /api/mobile-pos/v1/receipts/{id}/reprint-events`
- `POST /api/mobile-pos/v1/heartbeat`

### HQ administration

- CRUD/query endpoints for stores, tills, store customer mapping, tender mapping, receipt profiles, offline policies, user-store assignments, and devices.
- Explicit approve/suspend/revoke/reassign device actions.
- Till close review/return/correction actions that call the existing Finance till service.
- Sync-exception and reconciliation reports.

Existing `/api/ar/invoices`, `/api/ar/payments`, `/api/finance/payment-methods`, `/api/finance/cashier-tills`, and `/api/finance/banking` remain the canonical web/Finance surfaces. The mobile orchestration layer calls their services, not their controllers.

## T. Release and test plan

### Automated gate: `acceptance:mobile-pos`

- TypeScript and lint.
- `expo install --check`.
- Expo doctor and Android config validation.
- Production Android bundle/export.
- Single React/runtime check.
- Auth, MFA, refresh, tenant, logout, and revoked-session tests.
- Device enrollment and remote-disable tests.
- Store/default-customer validity tests.
- Invoice preview/create/post contract tests.
- Single and split-tender atomicity tests.
- Payment allocation, partial payment, currency, reference, and reversal tests.
- Till open/ownership/count/review tests.
- Offline grant expiry and all configured limit tests.
- Outbox durability, retry, rejection, replay, ordering, and conflict tests.
- Receipt hook, persistence, reprint, and adapter tests.
- Scanner permission/repeated-scan/resume tests.
- API tenant-escape, permission, idempotency, and ProblemDetails tests.
- Accounting reconciliation assertions across AR, GL, liquidity, till, and banking.

### UI and native gates

- Expo Web/Playwright flows for fast deterministic coverage.
- Android emulator smoke on API 29 and current target.
- Ordinary Android device checks on representative Android 10, 12, 13, and 14+ devices where available.
- ZCS Z92S Android 14 certification covering install, upgrade, printer, scanner, camera, Wi-Fi, 4G, suspend/resume, reboot, low battery, offline work, sync, day-end, and preserved outbox.
- Provider sandbox certification for integrated electronic payments.

### Upgrade gate

Install the new APK over the prior signed build and prove that tokens follow security policy while device enrollment, till configuration, catalogue cache, receipts, database migrations, and unsynced outbox survive correctly. Normal release guidance must not require uninstalling the app.

## U. Risks, dependencies, and blockers

| Risk/dependency | Status and mitigation |
| --- | --- |
| Z92S vendor printer/scanner contract unavailable | Hardware adapter implementation and physical certification are blocked; interfaces and fake adapter can proceed |
| Payment provider not selected | Generic provider boundary can proceed; online Mobile Money/card capture waits for provider contracts and credentials |
| Existing device trust endpoint is a placeholder | Phase 2 adds persisted enrollment before financial mobile access |
| Multi-tender atomicity spans multiple current receipt commands | Phase 3 adds a composed server transaction and failure-injection tests |
| Store/customer master can become invalid after offline grant issuance | Snapshot validity and grant expiry limit exposure; sync revalidates and can route to review |
| Electronic tender recorded offline may be false or duplicated | Require external authorization reference, permission, policy, idempotency, and HQ exception reporting |
| Offline revocation cannot reach a disconnected device instantly | Short signed authorization window, revocation epoch on next contact, and bounded value/count exposure |
| SQLCipher/native module compatibility unknown | Prototype during Phase 2 before committing the storage design |
| RHEMA frontend monorepo may hoist incompatible React | Metro singleton configuration and release check are mandatory |
| User/store/till ambiguity | Bootstrap blocks financial actions unless all active mappings resolve uniquely |
| Day-end with pending sync can produce unstable totals | Permit submission only; freeze/finalize after synchronization or an audited exception |
| New Finance producer routes require certification | Add readiness checks and prevent production posting until routes are certified |

## 4. Phased implementation and gates

### Phase 1 - Architecture and audit

Deliver this report, the implementation tracker, and the Finance coordination ledger. No application or database state changes.

**Exit gate:** A-U covered; management policies recorded; reuse/gap decisions explicit; branch and baselines recorded.

### Phase 2 - Foundation

- Create `apps/mobile` with aligned Expo/React Native dependencies.
- Implement error boundary, API client, ProblemDetails, environment profile, secure storage, and session state machine.
- Implement login, MFA, tenant selection, logout, refresh, bootstrap, dashboard shell, account, and diagnostics.
- Add persisted device/store/till/offline-policy server models, permission catalogue, HQ administration, enrollment, and heartbeat.
- Issue a signed, expiring offline grant only for an open cashier session, filtering commands and tenders through the current dynamic permissions and store policy.
- Add Android development and preview build configuration.

**Exit gate:** enrolled test device can authenticate with MFA, resolve one store/till/default customer, render real bootstrap/dashboard values, revoke safely, and pass Phase 2 gates.

### Phase 3 - Core Finance

- Approved-customer search and store default customer.
- Outstanding invoices.
- Invoice preview/create and governed lifecycle actions.
- Customer receipt, allocation, partial payment, canonical receipt.
- Server idempotency and Finance producer routes.

**Exit gate:** real test invoices/payments reconcile to AR, GL, liquidity, audit, dimensions, and numbering with no mobile-only balances.

### Phase 4 - POS

- Catalogue/service search, camera/manual scan, cart, discounts, tender entry, split tender, receipt and reprint.
- Atomic mobile sale orchestration.
- Printer abstraction and PDF/system fallback.

**Exit gate:** single and split-tender checkout is atomic and renders/prints one canonical receipt projection.

### Phase 5 - Offline

- SQLite migrations, scoped cache, outbox, grant enforcement, idempotent push/pull, conflict and exception UI.

**Exit gate:** forced disconnect/retry/restart/upgrade tests cannot duplicate or lose financial work.

### Phase 6 - Till and day-end

- Till session UI, tender reconciliation, cash declaration, variance workflow, pending-sync handling, bank deposit proposal, and HQ reports.

**Exit gate:** till-based totals reconcile through banking and all short/excess decisions are evidenced.

### Phase 7 - Hardware

- ZCS printer and scanner adapters using the audited SmartPos 1.8.1 contract.
- Keyboard-wedge/vendor scanner adapter.
- Bluetooth ESC/POS and native physical-device certification.

**Exit gate:** signed APK passes the Z92S and representative Android matrix.

### Phase 8 - Hardening and release

- Threat review, performance, reconciliation, release pipeline, signing, upgrade/rollback runbook, observability, and production readiness.

**Exit gate:** all automated, native, physical, security, accounting, deployment, and operational support evidence is approved.

## 5. Phase 1 conclusion

RHEMA has strong reusable Finance and security foundations. The principal new work is a governed mobile orchestration and administration layer, a native Android client, bounded offline authorization and synchronization, persisted device enrollment, and hardware adapters.

The store-specific default walk-in customer decision resolves the customer identity requirement cleanly: every sale remains attached to a valid RHEMA Business Partner and AR profile, while the operator can select another approved customer when required. No anonymous or orphan invoice path is needed.
