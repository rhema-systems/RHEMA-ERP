# Accounting-book V2 producer migration

## Purpose and integration rule

Finance Phase 1A separates two concepts that the V1 name `BookClassification` obscured:

- `AccountingBookCode` selects the single canonical ledger/reporting book for a posting.
- `AccountClassificationId` is detailed Finance master data attached to each account/book assignment.

External producers submit the first value and never submit the second. Finance resolves and validates
the detailed classification from `AccountAccountingBook` for every posting line.

Phase 2 clarification: classification codes, IDs, names, hierarchy nodes and `SystemRole` are all
Finance-owned configuration. Procurement, Inventory, Sales and HR producers must not copy their own
category captions into `AccountCategory`/`AccountSubCategory`, infer a Finance classification from a
display string, or submit a raw classification ID. Where a producer creates a GL account, it must use
the Finance provisioning boundary with stable account intent/code; Finance creates the canonical-book
mappings and resolves its reviewed stable classification codes. Producer-owner conversion remains a
coordinated stacked-integration task and does not authorize direct edits to Finance tables.

V1 remains available only as a deprecated Finance-owned compatibility boundary while module owners
adopt V2. The coordinated final cutover must leave no permanent dual contract:

1. Integrate the Finance V2 DTOs, adapter overloads, evidence vectors and enforcement.
2. Integrate Finance-owned producer conversions.
3. Integrate Procurement, Inventory, Sales and HR/Payroll owner changes in the agreed window.
4. Run the repository V1-consumer and contract gates.
5. Remove V1, increment the final contract version and reset/reseed local development databases.

Do not merge a producer conversion before its accounts have enabled mappings for the concrete book;
the central engine intentionally fails closed instead of creating a mapping.

## Contract replacement

### Phase 5 account-identity boundary

Producer owners must treat `IFinanceAccountProvisioningService` as authoritative for the physical GL account number. Supply the reviewed natural account code and account intent; Finance resolves the tenant COMPANY value, validates the exact active COMPANY/NATURAL_ACCOUNT structure, and composes the identifier. Do not copy tenant-specific segment GUIDs, create `AccountSegmentValue` rows, or place department/project/estate/contract/funding/activity values inside a GL number. Those values use the existing Finance transaction-dimension contract and routes. Existing V1 posting compatibility is unaffected.

| V1 | V2 |
| --- | --- |
| `FinancePostingRequestDto` | `FinancePostingRequestV2Dto` |
| `BookClassification` | `AccountingBookCode` |
| `FinanceExternalPostingEnvelopeDto` | `FinanceExternalPostingEnvelopeV2Dto` |
| established V1 evidence canonicalization | explicit `RHEMA-FIN-EXTERNAL-POSTING\|2.0` evidence domain |

Example request fragment:

```json
{
  "accountingBookCode": "IFRS",
  "functionalCurrencyCode": "GHS",
  "lines": [
    { "accountId": "00000000-0000-0000-0000-000000000001", "debitAmount": 100 },
    { "accountId": "00000000-0000-0000-0000-000000000002", "creditAmount": 100 }
  ]
}
```

`bookClassification` and `accountClassificationId` are absent. V2 canonical evidence includes the
normalized uppercase book code and cannot be validated with a V1 hash. Producers must regenerate
evidence after constructing the complete V2 envelope and verify their output against the Finance
golden canonical string and SHA-256 vector in
`ExternalFinanceDimensionAdapterContractTests`.

## Book resolution and runtime failures

The V2 single-book boundary normalizes the code and requires exactly one non-deleted, tenant-owned
`AccountingBook`. It rejects unknown, inactive and non-posting books. Each distinct line account must
be tenant-owned, active under existing posting rules, and have an enabled `AccountAccountingBook`
row for that book. A migrated mapping must point to an active, compatible posting classification.

`ALL_ACTIVE_BOOKS` is an orchestration selection, not a book. A source workflow must resolve the
concrete active books and submit one independently idempotent V2 request per book. The Finance engine
will always reject the pseudo-code.

Before integration, each owner must test:

- V2 serialization contains `accountingBookCode` and no `bookClassification`;
- its evidence matches the published V2 vector algorithm;
- an unknown, inactive or non-posting book is rejected;
- an account without an enabled mapping is rejected;
- tenant-crossing book/account identifiers are rejected without existence disclosure;
- retries remain idempotent per concrete book;
- `AccountClassificationId` is never accepted from source data.

## Procurement owner

Stage B1 owner conversion status:

- `src/ErpSystem.Core/Services/Procurement/ProcurementSupplierOnboardingTokenService.cs`
  - `BuildPostingRequest` now produces V2 with a concrete Finance-resolved book code.
- `src/ErpSystem.Core/Services/Procurement/TenderBidService.cs`
  - `BuildTenderFeePostingRequest` now produces V2 with the same resolver contract.

`FinanceSettings.SubledgerPostingMode` is a transitional single-book setting. The Finance-owned resolver
maps only `IFRS`, `Local`/`LOCAL_STATUTORY`, and `Management`/`MANAGEMENT`; absent settings retain the
historical IFRS choice, while blank, unknown, and pseudo-book values fail before posting. Procurement does
not enumerate books or decide multi-book applicability.

Finance-account provisioning status:

- `src/ErpSystem.Data/Seeders/ProcurementSupplierOnboardingTestSeeder.cs`
  - now uses `IFinanceAccountProvisioningService` and performs no direct `Account`, segment, classification,
    or account-book mapping writes;
  - Finance may adopt only the three exact development-seeder identities (`1040`, `4930`, `2210`) when they
    retain the original seeder provenance and contain no segment or book evidence. IDs are preserved;
    ambiguous, wrong-type, or partially configured rows fail closed.

Stage B1 evidence scans:

- `rg -n --glob '*.cs' 'new\s+FinancePostingRequestDto|FinancePostingRequestDto\s+\w+\s*=' src/ErpSystem.Core/Services/Procurement src/ErpSystem.Data/Seeders/ProcurementSupplierOnboardingTestSeeder.cs`
  returns no active Procurement V1 constructor;
- `rg -n --glob '*.cs' 'new\s+Account\b|\.Accounts\.(Add|AddAsync|AddRange|AddRangeAsync|Update|Remove)|AccountCategory|AccountSubCategory' src/ErpSystem.Data/Seeders/ProcurementSupplierOnboardingTestSeeder.cs`
  returns no direct Finance-account writer or display-category authority.

This is an owner-scoped result only. Inventory and Sales V1 producers and Finance's temporary V1
compatibility boundary remain, so the repository-wide no-active-V1 gate has not passed.

## Inventory owner

Current V1/single-book producer:

- `src/ErpSystem.Api/Services/Inventory/InventoryDisposalService.cs`
  - posting request created in the disposal posting path near `PostAsync`.

Current book-selection consumers requiring owner review:

- `src/ErpSystem.Core/Services/Inventory/StockAdjustmentService.cs`
  - opening-stock and adjustment book snapshots, including the `ALL_ACTIVE_BOOKS` selection.
- `src/ErpSystem.Core/DTOs/Inventory/InventoryDTOs.cs`
- `src/ErpSystem.Core/Entities/Inventory/InventoryEntities.cs`

Required owner change:

- change disposal posting to V2 with a concrete `AccountingBookCode`;
- keep `ALL_ACTIVE_BOOKS` only as an Inventory workflow choice;
- resolve that choice into the tenant's concrete active posting books before calling Finance;
- emit one V2 request with a stable, book-qualified idempotency key per resolved book;
- add tests proving no pseudo-book reaches `IFinancePostingEngine`, partial retries do not duplicate a
  successful book, and every inventory account is mapped in every selected book.

Do not interpret an Inventory item/category label as a Finance classification.

## Sales owner

Current V1 producer:

- `src/ErpSystem.Core/Services/Sales/ReturnOrderService.cs`
  - `BuildSalesCreditNotePostingRequestAsync`;
  - the credit-note reversal posting block that currently rebuilds a request from original evidence.

Required owner change:

- create ordinary credit-note postings with `FinancePostingRequestV2Dto.AccountingBookCode`;
- use the Finance trusted reversal entry point for exact reversal instead of supplying reconstructed
  accounts/amounts as authoritative client input;
- add credit-note post/retry/reversal tests and book-mapping denial tests;
- verify the original V2 book code remains visible in immutable Finance posting evidence.

## HR/Payroll owner

No active `FinancePostingRequestDto` use was found in the Phase 1A base. The material dependency is
direct Finance master-data mutation:

- `src/ErpSystem.Api/Services/HR/PayrollService.cs`
  - payroll account seed/provisioning block near the account creation and
    `AccountCategory`/`AccountSubCategory` assignments.

Required owner change:

- stop inserting or editing Finance `Account` rows directly;
- call `IFinanceAccountProvisioningService` with a stable payroll account intent/code;
- let Finance create the required book mappings and select classifications from the reviewed
  manifest/policy;
- add tests proving repeated provisioning is idempotent, wrong-type account conflicts fail closed,
  and no display-text classification inference occurs.

Payroll journal economics do not otherwise change. Local database reset will invalidate generated
account IDs, so HR configuration must resolve the provisioned accounts again by the published stable
Finance key after reseed.

## Database and reset consequences

Phase 1A keeps the classification FK nullable only for migration. The readiness endpoint reports
accounts without enabled books, unclassified mappings, unavailable books, invalid classifications and
invalid tenant lineage. The reset gate cannot pass while any blocker remains.

After all owners have migrated and both existing-database and empty-database migration/reseed tests
pass, developers recreate their own local database at the coordinated integration commit. No GUID from
an old local database is portable. Configuration must resolve books by tenant code + book code,
classifications by book code + classification code, and accounts by stable account code.

The old `IsIFRSClassified`, `IsBaseClassified` and `IsLocalClassified` columns remain temporarily because
active cross-module/reporting consumers still exist. New Finance account writes do not synchronize
them. Removal requires a repository gate proving those consumers have moved to `AccountAccountingBook`.

## Generated SQL snapshots

`src/ErpSystem.Api/full_database.sql`, `src/ErpSystem.Data/schema.sql`, and
`src/ErpSystem.Data/verify_current_schema.sql` have no discovered build/startup deployment authority and
remain non-authoritative reference artifacts. Phase 1A does not edit them. After schema stabilization,
the Finance integration owner must either:

- regenerate an agreed deployment artifact from the reviewed migration chain with
  `dotnet ef migrations script --idempotent`, record the exact command/tool version and replace all
  three references consistently; or
- formally retire and remove them in a separately reviewed change after confirming no deployment
  owner consumes them.

Active `Program.cs` accounting-book repair SQL has been removed from the startup/repair path; the EF
migration and deterministic Finance seeder now own this schema.

## Final owner sign-off

For each module, record the named owner, commit, tests and agreed integration window in the Finance
coordination ledger. V1 removal is allowed only after a repository scan and contract tests prove no
active producer remains. Temporary coexistence is for stacked integration branches only, not the final
target architecture.

## Phase 3 reporting note

Classification-driven financial-statement layouts and immutable publication
snapshots are Finance-owned reporting configuration. External posting producers
continue to submit only the concrete V2 `AccountingBookCode`; they must never
submit a classification, report row, layout, or publication-snapshot identifier.
Phase 3 therefore adds no producer implementation change. The existing owner
actions and coordinated V1 removal gate above remain unchanged.
