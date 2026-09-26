# Finance Integration Adapter Checklist

Use this checklist jointly with the producing-module owner. The adapter belongs at the boundary between the approved operational transaction and Finance; it must not absorb the producer's workflow or reproduce Finance accounting rules.

## 1. Agree ownership before coding

- Record a `FIN-INT-###` catalogue ID, contract owner and version.
- Name the exact producer status/event that authorises accounting. “Saved” or “updated” is not sufficient.
- Identify the immutable source ID, tenant ID and human-readable reference.
- State which module owns corrections and which Finance reversal/void operation compensates a posted event.
- Decide whether customer, supplier, asset, tax, cash/bank or inventory subledger evidence is required in addition to GL posting.

## 2. Define the request and outcome

- Use an existing Finance interface where one exists; do not introduce a second journal writer.
- Map producer values to Finance DTOs explicitly. Do not pass producer entities into Finance persistence.
- Resolve accounts from Finance configuration or validated source mappings; never hard-code account IDs/numbers.
- Include `OriginModuleCode`, `SourceDocumentType`, `SourceDocumentId`, `SourceDocumentTenantId`, `SourceDocumentReference`, posting date and functional currency.
- Generate a deterministic idempotency key from stable source identity, action and contract version.
- Persist Finance `PostingEventId`, `JournalEntryId` and document links as repairable back-references.

## 3. Protect accounting and tenancy

- Reject cross-tenant source, account, partner, tax, bank and asset references before posting.
- Prove debit equals credit in functional currency and preserve transaction-currency/rate evidence when applicable.
- Let Finance enforce fiscal periods, locks, control-account policy, approval separation and posting audit.
- For AR/AP, create/post/allocate through the Finance subledger service; a control-account GL line alone is incomplete.
- Define transaction behaviour so a failed Finance step cannot leave the source marked as successfully posted.

## 4. Make retries and failures operationally safe

- Test first call, identical retry, concurrent retry and changed-source retry.
- A duplicate must return the original Finance result or a clear conflict; it must not create another journal.
- Log the catalogue ID, source reference and correlation ID without exposing secrets or sensitive personal data.
- Return an actionable failure to the producer and retain enough state to retry after configuration or period correction.
- Add reconciliation/read-model evidence so support staff can trace source → subledger → journal → reversal.

## 5. Required consumer evidence

- Happy-path test captures the Finance request and uses `ShouldSatisfyPostingContract(...)`.
- Negative tests cover wrong tenant, missing configuration, invalid lifecycle status and closed/locked period where relevant.
- Retry test proves stable idempotency and a single accounting outcome.
- Failure test proves the producer does not advance to “posted” after Finance rejects or throws.
- AR/AP integrations prove aging/allocation/statement visibility, not just balanced GL lines.
- A real SQL Server test is added when migrations, constraints, concurrency or transaction semantics are involved.
- The focused test is added to `.github/workflows/finance-integration-gate.yml` and its expected TRX count is updated.

## 6. Review and rollout

- Producer owner reviews the source-event and user-workflow mapping.
- Finance owner reviews accounts, tax, currency, subledger, dates, reversal and reporting.
- Migration is rehearsed on a representative dry-run database when schema changes exist.
- UAT covers success, retry, correction, reconciliation and report drill-through using representative TDC data.
- Update the catalogue status only after the callable boundary and required evidence exist.
# Canonical posting identity registration

Before a module can participate in governed accounting-book selection, its producer must register the exact Finance-owned identity triplet—origin module, source document type, and posting action—in `FinancePostingIdentityCatalog`. Producer adapters must send those registered values verbatim; aliases or locally invented abbreviations are rejected. When communicating the Finance-module rollout to other development teams, include this registration step, the relevant canonical triplets, and contract tests proving their producer emits them unchanged.
