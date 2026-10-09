# Finance mobile POS architecture and foundation workstream - 2026-10-09

## Objective and scope

Audit the committed Flash ERP Android mobile application and the current RHEMA ERP Finance, AR, payment, till, security, tenant, location, and offline capabilities. Produce an implementation-ready architecture and phased delivery plan for the RHEMA Field POS and Revenue Collection mobile application. The plan must incorporate the management policies recorded in `C:\Users\USER\Desktop\Mobile app.docx`, including per-store default walk-in customers.

The architecture and contract mapping are complete. Phase 2 now contains an isolated Expo application, server-side Mobile POS governance foundation, and an HQ administration page. It does not authorize an alternate accounting ledger, direct database synchronization, production deployment, or device enrollment in a live environment.

## Branch and worktree

- Branch: `codex/mobile-pos-phase1`
- Worktree: `.worktrees/mobile-pos-phase1`
- Exact starting commit: `9044371f533ee3fac77472d74aecfe006e8c6872`
- Starting ref: `origin/master`
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
- Phase 2 foundation commit: pending at the time of this ledger update.

## Phase 2 application foundation

- Added `apps/mobile`, an Expo 54/React Native Android application with TEST/UAT/PRODUCTION profiles, production HTTPS enforcement, SecureStore-backed tokens and installation identity, typed API handling, login/MFA/tenant switching, device enrollment, bootstrap, dashboard, account switching, logout, and sanitized crash handling.
- Added dynamic `MobilePOS.*` permissions and permission policies without fixed role names.
- Added tenant-scoped Mobile POS stores, mandatory approved default walk-in customer/Customer-role mapping, Finance dimension defaults, tills, allowed payment methods, effective user-store assignments, persisted devices and assignment history, offline policies, and offline-grant foundation.
- Added administration/runtime APIs under `/api/administration/mobile-pos/v1` and `/api/mobile-pos/v1`.
- Added a permission-gated HQ page at `/administration/mobile-pos` for stores, searchable walk-in customers, tills/tenders, offline policies, device approval/revocation, and searchable user-store assignment.
- Tenant selection now returns and persists a refresh token scoped to the selected tenant, preventing a subsequent refresh from reverting or failing against the original tenant context.

## Migrations and application state

- Migration created: `20261009054618_AddMobilePosFoundation`.
- Migration tables: stores, store dimension defaults, tills, till payment methods, user-store assignments, devices, device assignment histories, offline policies, and offline grants.
- Migration was not applied to a database.
- No production deployment, service restart, live device enrollment, or provider call was performed.

## Verification evidence

- Git worktree created cleanly from the exact `origin/master` baseline.
- Root checkout and unrelated worktrees were left unchanged.
- Management responses were extracted with their question context; all highlighted responses were captured.
- Flash committed source and incident documents were inspected at `526acb8f46e5f003b493f1807d8cbd0372a88dda`; unrelated dirty Flash working-tree files were not read as implementation evidence or modified.
- RHEMA invoice, payment, payment-method, till, liquidity, banking, numbering, Finance-dimension, authorization, tenant, location, warehouse, device, and PWA boundaries were traced in the current source.
- All requested A-U architecture sections, management decisions, phase gates, data contracts, API proposals, migration plan, test plan, and blockers are present in the Phase 1 report.
- Full migration-aware API build passed with 0 errors; the build preserved 89 EF models and compiled the new migration metadata.
- Idempotent SQL generation from `20261008192833_AddPropertySalesOrderDepositLifecycle` to `20261009054618_AddMobilePosFoundation` passed. The 28,333-byte script contains nine `CREATE TABLE` statements and the expected stores, tills, devices, policies, and migration-history marker.
- Six focused `MobilePosFoundationTests` passed, covering unique permission catalogue entries, database-backed permission policies without role names, runtime/admin controller policy coverage, tenant-scoped unique model indexes, and required walk-in customer foreign keys.
- Frontend TypeScript check passed. Focused ESLint passed for the Mobile POS page/service and changed route/navigation/auth files.
- Frontend administration/access tests passed: 27 tests across the route guard, Settings registry, and navigation access helper.
- Frontend production build passed. The generated app manifest contains `/administration/mobile-pos/page`.
- Expo application TypeScript check passed; Expo Doctor passed 18/18 checks; Android bundle export passed.

## Known failures and constraints

- The Flash ERP reference checkout is intentionally dirty. Read committed files through Git object paths and do not clean, reset, stash, or commit that checkout.
- ZCS Z92S printer/scanner SDK artifacts and provider credentials have not yet been supplied.
- Native Android keystore behavior, visible device enrollment/remote-disable flow, and physical Z92S behavior require an Android device and later acceptance stages.
- Offline grant signing/issuance and Finance transaction orchestration are intentionally not claimed by the Phase 2 foundation.
- The repository currently reports pre-existing ImageSharp package advisories and compiler warnings; the verified Mobile POS builds completed with zero errors.

## Remaining work

Complete the remaining Phase 2 acceptance evidence: focused API/service integration cases, environment and ProblemDetails contract tests, physical Android secure-storage/auth/enrollment flow, visible HQ browser verification after applying the migration in an authorized test database, and offline-grant signing/issuance. Then begin the Phase 3 canonical Finance orchestration. ZCS native adapter completion remains dependent on vendor artifacts and a physical certification unit.

## Authorization boundaries

The user authorized proceeding with the mobile implementation plan and Phase 1 architecture work. Read-only audits, documentation, tests, and isolated branch commits are authorized. Production deployment, live database migration, provider enrollment, payment capture, device enrollment, and physical Z92S certification require their relevant implementation stage and evidence.
