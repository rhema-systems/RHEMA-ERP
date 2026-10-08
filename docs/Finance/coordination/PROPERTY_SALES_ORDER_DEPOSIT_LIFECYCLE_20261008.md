# Property Sales Order Deposit Lifecycle Ledger

## Objective

Audit and incrementally align the existing Property Listing -> Enquiry -> Opportunity -> Quote -> approved Customer -> Sales Order -> Deposit -> Finance -> Customer Detailed Ledger lifecycle. Reuse the existing HR Identification Type master, CRM, Sales, Business Partner, approval, Finance posting, and reporting infrastructure. Preserve historical prospect deposits and all tenant, authorization, audit, and accounting controls.

## Scope and authorization boundaries

- Authorized source: user attachment reviewed on 2026-10-08.
- Audit-first delivery is mandatory. Do not create parallel customer, quote, sales-order, approval, identification-type, or accounting implementations.
- New property-sale deposits must move to an actual Sales Order only after the approved-customer gate is satisfied.
- Historical prospect deposits must remain readable, auditable, reversible where already supported, and financially unchanged.
- Identification numbers are sensitive. Do not place full values in URLs, application logs, analytics, or descriptive audit text.
- Unrelated worktrees, branches, generated artifacts, and UAT observations are excluded.
- No production/VPS database change or deployment is authorized by this ledger entry alone.

## Git state

- Branch: `codex/property-sales-order-deposit`
- Worktree: `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\.worktrees\property-sales-order-deposit`
- Exact base: `dd6fb224a1eaa7512087ed3bf0dd767d490c0f12`
- Base description: merge of PR #383 on `origin/master`.
- Latest integrated upstream: `2f5ccdda0aea7a4ac0d3b37e779036975ae90cdd`, merge of PR #384 (`Fix AR profile replacement and credit authority`).
- Upstream integration commit: `c37d59c71876a915e9cbbf45c74cf552d80f39a2`.
- Existing older worktree `codex/public-property-enquiry-sales-crm` was preserved because it is 289 commits behind current master and contains 12 divergent commits.

## Current phase

`Audit in progress`

No runtime implementation has started. The repository model, APIs, UI, workflows, Finance posting, ledger projection, migrations, permissions, tenant boundaries, and existing tests are being mapped first.

## Required 29-point audit deliverable

1. **Current Identification Type data model.** `IdentificationType` is a tenant-scoped HR entity with Name, Code, Description, IssuingAuthorityName, optional IssuingCountryId, HasExpiryDate, ExpiryNotificationLeadDays and IsActive. It has a restricted issuing-country relationship and an employee-card collection. There is no module availability relationship and no regex/length validation metadata for identification numbers.
2. **Current Identification Type edit/save behavior.** The edit page loads the record and countries, and its single form calls the normal update endpoint. The service updates only core type fields and enforces tenant-scoped Code uniqueness. There is no autosave or secondary configuration action today.
3. **Current HR consumers.** Employee identification cards, employee import, hiring, expiry reminders and Fleet driver projections consume `IdentificationTypeId`. The additive module mapping must not change those queries or require HR mappings for existing HR use.
4. **Current TenantModule data model.** `TenantModule` uses `BaseEntity`, TenantId, ModuleName, Description, Status, enabled/disabled dates and JSON Configuration. The database uniquely indexes `(TenantId, ModuleName)`.
5. **Current stable module identifier.** The durable relational identifier is `TenantModule.Id`. `ModuleName` is the current seed/reconciliation key because TenantModule has no Code field. New mappings will store `TenantModuleId`; only the server will resolve the Estate row using a shared Estate module constant. The browser will neither persist names nor filter by display text.
6. **Existing many-to-many mapping patterns.** Tenant-scoped mapping entities use two foreign keys, TenantId, audit/base fields and a composite tenant-aware unique index. `IdentificationTypeModule` will follow that explicit entity pattern so changes can be audited and soft deleted consistently.
7. **Existing permissions for Identification Type admin.** The controller is `InternalOnly`; create/update/activate/deactivate use `HR.Employee.Write`, while delete uses `HR.Employee.Admin`. Module exposure is configuration with cross-module impact, so mapping read/write will remain internal and mapping writes will use the existing Employee Admin policy. Core saves remain on Employee Write.
8. **Current Property Listing -> Enquiry flow.** Public listings use OTP-verified email/phone, CAPTCHA/rate limits, server-side listing/tenant resolution, duplicate-submission controls, and an immutable `EhcPropertyListingContextDto` JSON snapshot. Authenticated portal enquiries resolve linked Business Partners server-side. Both routes create the canonical EHC ticket/workflow.
9. **Current enquiry identification fields.** Neither public/authenticated request contract, ticket entity nor property context currently has IdentificationTypeId/IdentificationNumber. The new fields must be structured and server validated; the snapshot may retain type display data for historical display, but the relationship must be a foreign key rather than name-only data.
10. **Current Enquiry -> Opportunity flow.** `PropertyEnquiryProspectService` qualifies the ticket, creates or reconciles one tenant-owned opportunity, carries listing price/currency, optionally reserves the property and writes ticket/prospect audit lineage. Existing-customer enquiries skip creating a redundant lead.
11. **Current Opportunity -> Quote capability.** Generic Quote CRUD exists and CustomerId is nullable, so a quote can represent a prospect. Opportunities currently expose only View Quotes plus generic Sales handoff; there is no property-aware create-quote action or authoritative property-line prefill. Generic conversion requires an accepted quote.
12. **Current Quote printing capability.** No Sales Quote PDF/print document builder or controller action exists. The CRM page shows quote detail only. A printable quote must be added through the existing document-output/download conventions.
13. **Current Opportunity/customer linkage.** Opportunity.CustomerId is the canonical BusinessPartner.Id compatibility column. The property prospect and ticket also retain opportunity/customer lineage. Linking an existing customer already requires an active approved Customer role and tenant match.
14. **Current customer creation + approval workflow.** Property enquiry customer creation calls the existing Business Partner service, applies duplicate matching by contact, marks the prospect `CustomerPendingApproval`, and finalization checks active `ApprovalStatus == Approved`. The current UI incorrectly gates customer creation/finalization on the prospect-deposit threshold and must be reordered.
15. **Current Opportunity/Quote -> Sales Order flow.** Quote conversion already rejects non-accepted quotes, maps quote lines, and returns an existing order when the same QuoteId was converted earlier. It currently fails when Quote.CustomerId is null and does not independently require an approved customer. Direct Sales Order creation also lacks the active/approved Customer-role gate.
16. **Current property Sales Order prefill/locking.** The property enquiry UI resolves the canonical saleable source, passes an active reservation, locks the source item, uses Each/EA, and preserves OpportunityId/property reference. The server enforces allocation status, customer and opportunity consistency. Existing order discovery prefers OpportunityId and falls back to property reference for legacy records.
17. **Current enquiry deposit process.** New receipts are currently captured before customer conversion against `ProspectDepositReceipt`; clearing and reversal are separate permissioned operations. The property enquiry page actively exposes the form and uses the cleared threshold to unlock customer creation.
18. **Current prospect deposit accounting.** Clearing posts Dr bank/liquidity and Cr prospect-deposit liability exactly once through the Finance posting engine. Customer conversion creates a deterministic CustomerPayment advance and reclassifies the liability without debiting cash again. Historical reversal, journal and transfer lineage is durable and must remain unchanged.
19. **Current payment method model.** Tenant-scoped `PaymentMethod` has a stable Id, Type, active flag, RequiresBankAccount, RequiresReference and default GL account. AR `PaymentService` resolves and validates the configured method and posting destination server-side.
20. **Current Cheque/Bank Deposit metadata model.** CustomerPayment already stores PaymentMethodId, BankAccountId/LiquidityAccountId, CheckNumber, ChequeDrawerBank and TransactionReference. BankAccount supplies the configured bank/account identity. A small Sales Order payment lineage entity is still required to retain SalesOrderId and the structured external bank/account values requested for cheque/bank-deposit reference formatting.
21. **Current Customer Detailed Ledger Reference source.** AR report projection currently uses `CustomerPayment.TransactionReference ?? CheckNumber`; its older statement projection uses PaymentNumber. It does not format Cash/Cheque/Bank Deposit references according to the requirement.
22. **Current Customer Detailed Ledger Description source.** The detailed ledger uses `payment.Notes ?? "Customer payment - {PaymentMethod}"`. It does not resolve property/unit lineage.
23. **Current property/document lineage available to ledger.** Listing -> EHC ticket/context -> prospect -> opportunity -> allocation -> Sales Order exists; Sales Order -> invoice and PaymentAllocation -> CustomerPayment exist. An unallocated Sales Order deposit has no direct SalesOrderId today, so the report cannot obtain property lineage without a new indexed relation.
24. **Historical compatibility concerns.** Existing prospect deposits and journals must stay readable/reversible and must never be rewritten. Optional context additions must deserialize old JSON. Existing HR identification types stay usable by HR even with no mappings. Existing non-property CustomerPayments and ledger text retain their fallbacks.
25. **Exact gaps versus requirement.** Missing: module mapping/schema/API/UI/audit; Estate module seed; public filtered lookup; enquiry identification persistence/validation/masking; property-aware quote create/idempotency/print; approved-customer server gate; reordered customer flow; new Sales Order deposit command/UI; structured deposit lineage; exact ledger reference/description; and negative/concurrency coverage.
26. **Files expected to change.** HR entity/DTO/interface/service/controller/form/edit page/lookup service; TenantModule constants/seeding; EHC request/context/ticket configuration/controller/dialog/service types; Quote/Sales Order APIs/services/UI/document output; CustomerPayment-related Sales Order deposit lineage, AR reporting and property-enquiry UI; DbContext/configuration/migration; focused backend/frontend tests.
27. **Migration decision.** A forward EF migration is required for `IdentificationTypeModule`, enquiry IdentificationTypeId/IdentificationNumber, and Sales Order deposit/payment lineage. Historical migrations will not be edited. The default Estate TenantModule will use a deterministic seed Id; Estate mappings themselves will not be guessed or bulk seeded.
28. **Test plan.** Add mapping isolation/authorization/separate-save/concurrency tests; public Estate lookup and enquiry validation tests; quote pre-customer/idempotency/print tests; Business Partner approval and API-bypass Sales Order tests; new versus legacy deposit tests; Finance exactly-once/no-revenue/reversal tests; exact ledger reference/property description/query-shape tests; frontend interaction tests; build/typecheck/migration verification and visible browser acceptance.
29. **Implementation slices.** (1) protection tests and audit; (2) module mapping; (3) Estate lookup and identification persistence; (4) property quote/print; (5) approved-customer conversion; (6) Sales Order deposit and legacy read-only behavior; (7) structured tender metadata; (8) Finance advance posting; (9) ledger reference; (10) property description; (11) migration/legacy verification; (12) concurrency/security; (13) end-to-end acceptance.

### Accounting decision

- **Historical path:** retain the existing prospect receipt posting: debit bank/liquidity, credit prospect-deposit liability; later reclassify to a CustomerPayment advance without a second cash debit.
- **New path:** create a tenant-owned, approved-customer `CustomerPayment` advance linked to the actual Sales Order. Finance posts debit bank/liquidity and credit the configured customer-advance liability exactly once. No invoice, receivable settlement or revenue is created merely because the deposit was received. Allocation happens only after the normal Sales Order -> Invoice lifecycle produces and posts an invoice.
- The existing Finance posting engine, source-book authority, bank/liquidity access, configured payment method and idempotent posting keys remain authoritative.

## Commits

- `216a3bcaef1` - start this audit ledger.
- `c37d59c7187` - merge the latest `origin/master` after teammate PR #384.

## Migrations and application status

- Migration required: under audit; no migration created or applied.
- Local application: not changed or started for this workstream.
- VPS/test application: unchanged by this workstream.
- Database: no writes performed.

## Verification evidence

- Confirmed the isolated worktree starts clean from `origin/master` at the exact base above.
- Re-fetched and verified both GitHub and `git ls-remote` on 2026-10-08: PR #384 at `2f5ccdda0ae` is the current remote `master` head and is already integrated in this worktree.
- Inspected existing Sales/CRM tracker `docs/tdc-sales-marketing-crm-gap-implementation-tracker.md` to avoid duplicating prior requirements analysis.
- Confirmed the older property-enquiry worktree is divergent and unsuitable as the implementation base.

## Known failures and risks

- The attachment spans HR configuration, a public Estate form, CRM, Sales, Business Partner approval, Finance posting, and reporting. Each existing ownership boundary must be verified before schema or service changes.
- Existing prospect deposits may already be posted in production. Any prospective Sales Order deposit path must coexist with them without rewriting history.
- The canonical stable identity available on `TenantModule` is still under audit; frontend display-name matching is prohibited.

## Remaining work

1. Complete and record the 29-point audit requested by the user.
2. Identify any genuine business-rule blockers.
3. Add protection tests for existing behavior and agreed prospective boundaries.
4. Implement only the required controlled slices.
5. Apply and verify any forward migration against an explicit non-production database.
6. Run focused backend/frontend tests, migration checks, tenant/authorization tests, and visible browser acceptance.
7. Update this ledger before every handoff or stop while work remains incomplete.
