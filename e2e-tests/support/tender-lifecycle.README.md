# Real tender lifecycle Playwright prerequisites

`tests/tender-lifecycle.real.spec.ts` is a destructive, serial acceptance suite. It uses the real browser application, real bearer-token authentication, real procurement APIs, and a disposable SQL Server fixture. It contains no procurement route interception or mocked API response.

## Required environment

Set:

- `TENDER_E2E_RUN=1`
- `TENDER_E2E_FIXTURE=<absolute or e2e-tests-relative fixture JSON path>`
- `TENDER_E2E_RESULT=<absolute or e2e-tests-relative non-secret result JSON path>`
- `E2E_BASE_URL=<running frontend URL>`

For each actor `PROCUREMENT_OFFICER`, `SUPPLIER`, `SUPPLIER_B`, `EVALUATOR`, `EVALUATOR_B`, `APPROVER`, `ETC_APPROVER`, and `UNAUTHORIZED`, set either:

- `TENDER_E2E_<ACTOR>_STORAGE_STATE=<real, current Playwright storage-state path>`; or
- `TENDER_E2E_<ACTOR>_USERNAME` and `TENDER_E2E_<ACTOR>_PASSWORD`.

`TENDER_E2E_<ACTOR>_TENANT_CODE` may override the fixture tenant for an actor. Credentials are read only from the process environment. They must never be placed in the fixture, repository, command output, or test attachment. When CAPTCHA is enabled, generate real storage states interactively; the suite intentionally does not bypass CAPTCHA.

## Disposable SQL fixture contract

The fixture producer must start from fresh records and write JSON matching `tender-lifecycle.fixture.example.json`. Its output must establish these exact states:

1. `supplierLifecycle` is an approved and published tender linked to its locked sourcing case. Supplier A is invited, approved, active, eligible for the governed category, and has valid required evidence. It has one selectable lot containing the named item, a positive mandatory tender fee awaiting supplier evidence, a future-valid submission window, and no precreated Supplier A bid. This branch ends at submitted bid plus verified payment and must reject premature opening without changing the bid.
2. `committee.absent` is a different tender with an eligible opened bid and assigned evaluator, but no committee control record.
3. `committee.draft` is a different tender with an eligible opened bid and assigned evaluator. Its committee exists only in Draft and must not satisfy final score locking.
4. `committee.active` and `competition` identify a separate source tender with two distinct `Submitted` bids owned by Supplier A and Supplier B. Guarded preparation first obtains and links the real release/case, then creates the exact advertised NCT control, document-issue entries, and sealed receipts in the disposable database. The browser owns public opening and every later transition. Its active committee has current membership, signed attendance/evidence, quorum, conflict declarations, and distinct technical and financial evaluators.
5. `prePublication` identifies a separate approved two-line PR with no tender/release/case yet. Its effective policy must select the configured tender type and its independent tender workflow approver must be the `APPROVER` actor. Set `prePublication.publication.prerequisitesPrepared=true` only when an external authoritative step has attached the browser-created tender's invitations, controlled document register, committee/publication evidence, and any method-specific controls. Otherwise `00D` records publication as `Pending`; it never attempts a predictably blocked publish.
6. `competition.firstSupplierBidId` and `competition.secondSupplierBidId` are the two submitted, not-yet-opened bids on the separate closed evaluation tender. Both contain deterministic prices, Supplier A is the expected winner, and neither has an evaluation or award.
7. `handoff.alternateApprovedAwardId` and `handoff.alternateMode` identify a separate independently approved disposable award used only for the opposite conversion route. The guarded runner requires both values; Playwright captures the real conversion response ID and SQL proves the source was consumed exactly once.
8. The procurement officer can administer the tender, verify tender payments, open sealed bids, and submit award recommendations, but is not the independent award approver.
9. `EVALUATOR` signs the technical evaluation and `EVALUATOR_B` independently signs the financial recommendation; neither can submit or decide the authority workflow.
10. `APPROVER` is the Head of Procurement who submits the recommendation to the exact authority workflow. `ETC_APPROVER` is the independent Entity Tender Committee member who makes the positive decision.
11. The unauthorized actor is an active tenant user with none of the tender administration, evaluation, award, contract, or PO privileges.
12. The selected handoff actor has both `procurement.purchase-order.create` and `procurement.contract.manage` for the two independent award branches.

The fixture must use distinct natural keys per `runId`, and its cleanup must target only records created for that run.

The suite writes browser-created IDs after every successful transition to `TENDER_E2E_RESULT` using schema version 1. The external SQL verifier must consume that file; it must not infer IDs from fixed GUIDs, display numbers, or newest-row queries. The result is marked `Passed` only after the opposite handoff, foreign-tenant read/mutation isolation, and final refresh checks succeed.

## Execution and evidence

From `e2e-tests`:

```powershell
npx playwright test tests/tender-lifecycle.real.spec.ts --project=chromium --reporter=list
```

The suite runs ordered gates covering actor isolation; approved-PR tender creation, draft persistence, independent approval, and conditional publication; Supplier A payment and bid persistence/submission with premature opening denied; a separate two-supplier controlled NCT competition; public opening; technical and financial evaluation; Head-of-Procurement submission; independent ETC decision; current award readiness; award, executed-contract and bidder-acceptance records; real alternate award-to-PO conversion; foreign-tenant read/mutation isolation; amendment/retry controls; and unauthorized API nonmutation. Use `--list` to obtain the authoritative gate count for the candidate. Every test attaches captured procurement HTTP failures and browser runtime errors. Unsupported prerequisite branches record `Pending` and keep the result `InProgress`; unexpected failures still stop the serial transition chain.

This suite being discoverable or skipped is not acceptance evidence. Acceptance requires every listed test to execute and pass against the disposable SQL fixture with no unexpected procurement HTTP failure or browser runtime error.
