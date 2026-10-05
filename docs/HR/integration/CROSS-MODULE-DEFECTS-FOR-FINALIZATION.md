# Cross-Module Defects Found During HR Work — For Finalization

**Opened 2026-08-17.**

Defects found in **other teams' modules** while building HR. They are recorded rather than fixed,
because the module is not HR's to change — the same rule that governs payroll. Each entry states
what is broken, what was proven, what it blocks, and what a fix needs.

**Owner action required.** Nothing here is speculative: every item was reproduced against the
running API on the reference database, and where a fix was trialled the result is recorded.

---

---

## Re-verification against master `f71d6917` (2026-08-28, merge #8)

Every entry below was re-checked against the merged tree after pulling 187 master commits
(PRs #63–#121). Method: for each defect, establish whether master touched the implicated file at
all (`git diff 0c1c2e5c origin/master`), then read the code where it did. “Untouched” means the
file carrying the defect received no change in this range.

| # | Defect | Status after merge #8 | Evidence |
|---|---|---|---|
| 1 | Procurement `SuppliersController` dead | **RESOLVED 2026-09-22** | `GET /api/Suppliers?page=1&pageSize=1` answers **200** where it answered 400; verified directly with an admin token and by `hr-jobarch/run-r7`, whose assertion pinned the 400 and began failing because the defect is gone |
| 2 | Finance currency conversion inverted | **RESOLVED** | PR #99 `finance-fx-seed-contract` transposed the seed (`Rate=12.5`, `InverseRate=0.08`) and documented the contract; both directions re-derived against `CurrencyService.ConvertAsync` |
| 3 | Workflow conditional routing never routes | **Open** | `WorkflowDefinitionServiceAdapter:686` still `JsonSerializer.Serialize`s the condition; `WorkflowConditionEvaluator` untouched, still parses it as an expression |
| 4 | Inventory frontend type errors | **Partly resolved** | duplicate `isStockingUnit` gone (1 declaration); a full re-count is impossible while #20 aborts the compiler |
| 5 | `GetActiveByBusinessPartnerIdAsync` throws | **Open** | `BusinessPartnerUserRepository.cs` untouched |
| 6 | `CreatedBy` holds an id, not a name | **Open** | all cited sites untouched |
| 7 | Email template designer unusable | **Open** | `EmailTemplate*` untouched |
| 8 | Fixed-asset → Maintenance link unsettable | **Open** | service and DTOs carry `MaintenanceAssetId`, but neither `register/new` nor `register/[id]/edit` renders a control — the value is initialised and submitted, never entered |
| 9 | Projects maintenance follow-through throws | **Open** | `ProjectService.MaintenanceFollowThrough.cs` untouched; still no system seed for the three lookups |
| 10 | Maintenance number collisions | **Open** | `AssetAdmission`/`AssetDischarge` untouched |
| 11 | External-user allowlist exposure | **Open** | `ExternalUserAccessMiddleware` untouched; `/api/procurement` still a wholesale prefix |
| 12 | Payroll payslip snapshots (FYI) | n/a | informational entry |
| 13 | Payroll profile create never worked | **Open** | `PayrollService.cs` untouched |
| 14 | Workflow pending feeds die mid-stream | **Open** | no `ReferenceHandler`/`IgnoreCycles` anywhere; master's only `WorkflowController` change is an unrelated new endpoint |
| 15 | Generic approval strands the entity | **Partly mitigated, Finance only** | master added a `SupplierDebitNote` guard returning 409 `FINANCE_DOMAIN_APPROVAL_REQUIRED` pointing at the domain route. The generic hole is unchanged and **no HR entity is protected** — but this is now the precedent pattern for protecting one. *(2026-10-04: staff travel and separation named in § 15 — what a trip and a separation lose when approved there.)* |
| 16 | Notification feed planting | **Open upstream** | `NotificationsController` untouched (HR gated its own path in slice 11) |
| 17 | Fixed-asset approval links to a dead route | **Open** | `ActionUrl` still `/finance/fixed-assets/register/{id}`; `register/[id]/` still contains only `edit/` |
| 18 | Payroll loans readable by anyone | **Open** | `PayrollController.cs` untouched |
| 19 | Helpdesk tickets readable by any employee | **Open** | `EhcInternalTicketsController` untouched |
| 20 | Shared reporting crashes `tsc` | **Open (new)** | introduced by this range |
| 21 | Supplier registration claims a "verified email address" and verifies nothing | **Open (new, 2026-09-21)** | found during HR round 4, § below |

### 21 — Procurement supplier registration does not verify the applicant's email

**Raised for the procurement owner. Not HR's to fix.**

`BusinessPartnerRegistrationService.SubmitAsync` refuses submission with:

> *"A verified email address or phone number is required for submission"*

The check behind that message tests only that the field is **non-empty and syntactically valid**.
A grep of the whole procurement area finds **no OTP, no confirmation token and no ownership check of
any kind**. Supplier portal credentials and a one-time temporary password are later emailed to what
the code itself calls *"the verified application contact"* — an address nobody proved ownership of.

**What contains the risk:** a supplier application is reviewed by a person and its documents must
each be verified or rejected before approval, so no account is provisioned on the say-so of the form
alone. The exposure is a typo'd or deliberately supplied third-party address receiving live supplier
credentials, and a message that asserts a check the code does not perform.

**Two suggestions, one in each direction:**

- Correct the wording, or build the check. HR's `auth/candidate/activate` (round 4) is a worked
  example of the second: a single-use, expiring, user-bound Identity token, anonymous because the
  account cannot yet sign in, refusing any account outside the expected role, with a resend endpoint
  that returns one neutral message so it cannot be used to enumerate accounts.
- ⚠ **HR should borrow procurement's token design.** `ProcurementSupplierOnboardingToken` stores the
  token **SHA-256 hashed at rest** with only the last four characters in clear, plus `Generation`,
  `Status`, issued/activated/expired timestamps and an `ExpiryReason`. HR's offer-response and
  interview-confirmation tokens are stored in clear and carry none of that audit trail. Theirs is
  the better model and the gap is ours.

### Not a numbered defect, but fixed by this range

**The workflow engine’s single-step auto-approve trap is gone.** `WorkflowEngine` (`:1293`) used to
complete an end step before considering approval semantics, so a one-step approval definition
silently auto-approved — the trap recorded in the HR workflow-integration notes. Master reordered
it so approval is handled first: *“A valid one-step approval workflow is necessarily both the start
and end step; it must create and process its approval before the workflow completes.”* Any HR area
that avoided single-step definitions for this reason can stop working around it, after re-testing.

## 1. Procurement — `SuppliersController` is entirely non-functional

> **✅ RESOLVED 2026-09-22.** `GET /api/Suppliers?page=1&pageSize=1` now answers **200**. Found by
> `hr-jobarch/run-r7`, whose assertion asserted the 400 — so the suite started failing precisely
> *because* Procurement fixed it. ⚠ An assertion that pins somebody else’s defect inverts on the
> day they repair it: it then reports a regression where there is an improvement. The assertion has
> been rewritten to assert the correct behaviour, with this note as the record of why it changed.
>
> The account below is kept as written, because it is the evidence the fix was needed.

**Severity: blocking.** Found 2026-08-17 while retiring HR travel's duplicate vendor master onto
Procurement's `Supplier` (area 12, slice 3).

### What is broken

Two repositories the controller depends on are **never DI-registered**:

| dependency | registered | implementation |
|---|---|---|
| `ISupplierRepository` | ✅ | `SupplierRepository` |
| `ISupplierContactRepository` | ❌ | `SupplierContactRepository` — exists, ctor takes only `ApplicationDbContext` |
| `ISupplierItemCatalogRepository` | ❌ | `SupplierItemCatalogRepository` — exists, same ctor |
| `IProcurementMasterDataChangeService` | ✅ | — |

DI cannot activate the controller, so **every endpoint on it answers 400** with
`Unable to resolve service for type 'ISupplierContactRepository'`. It reports only the first
missing dependency, which is why the second is easy to miss.

All three implementations sit in the same file
(`src/ErpSystem.Data/Repositories/Procurement/SupplierRepositories.cs`), next to the one that *is*
registered. There is no assembly scanning — every repository is registered by hand in
`ServiceCollectionExtensions.cs` (~line 251) — so these two were simply skipped in the list.

`SuppliersController` is the **only** consumer of both interfaces, so the blast radius is that one
controller and adding the registrations cannot disturb anything currently working.

### Cause, found 2026-09-14 — the original diagnosis below was wrong

It is **not** the paging path. `SuppliersController` takes three repositories in its constructor and
**only one of them was registered**:

```
System.InvalidOperationException: Unable to resolve service for type
'ErpSystem.Core.Interfaces.Procurement.ISupplierContactRepository'
while attempting to activate 'ErpSystem.Api.Controllers.Procurement.SuppliersController'.
   at Microsoft.AspNetCore.Mvc.Controllers.ControllerFactoryProvider...CreateController
```

The exception is thrown during **controller activation**, before any action method runs — which is
why every endpoint fails identically and why the list query's shape made no difference.
`ServiceCollectionExtensions` registered `ISupplierRepository` but neither
`ISupplierContactRepository` nor `ISupplierItemCatalogRepository`, although both interfaces and both
implementations (`SupplierContactRepository`, `SupplierItemCatalogRepository` in
`SupplierRepositories.cs`) already existed.

**Both registrations were added** beside the existing one, with a comment pointing here. That is a
two-line change in Procurement's registration block and nothing else in the module was touched —
noted openly rather than left as a surprise diff, because a dead controller blocked HR's
pre-employment check providers (round 3 lane G) from being registered through the real door.

⚠ Once the controller constructs, the ORIGINAL suspicion below is still worth checking: the list
path may *also* have a paging/ordering fault that was simply never reachable.

### What was proven

The two registrations were added temporarily, the solution rebuilt, and the controller exercised.
**Registration is necessary but not sufficient.**

Reads work correctly once registered:

```
200  GET /api/Suppliers                 1 row      ← and it contained the seeded supplier
200  GET /api/Suppliers?isActive=true   1 row
200  GET /api/Suppliers/{id}            object
200  GET /api/Suppliers/{id}/contacts   0 rows
```

**The create is a dead path:**

```
POST /api/Suppliers  →  201 Created, empty body
Suppliers in database: 1 (unchanged)
```

`CreateSupplier` → `CreateSupplierAsync` → `GenericRepository.AddAsync`, which only calls
`_dbSet.AddAsync(entity)` and **never `SaveChangesAsync`**. The controller has no unit-of-work
commit either, so the entity is tracked, never written, and the request ends. The 201 comes from
`CreatedAtAction`; its body is empty because the follow-up read cannot find a row that was never
saved. This is the `AddAsync`-with-no-commit dead-path shape.

Two further problems in the same handler, which a fix should take together:

- **`TenantId` is never set** on the new `Supplier`. The DbContext's auto-stamp is inert — its
  tenant-resolving constructor is commented out at `ApplicationDbContext.cs:60` — so even once the
  save happens the row would violate the tenant FK.
- The response is `CreatedAtAction(...)` with a **null body** rather than the created DTO.

`POST /api/Suppliers/{id}/contacts` also returned 400 on a plausible payload; not investigated,
because the module is not ours.

**The trial changes were reverted.** `ServiceCollectionExtensions.cs` is back to its committed
state — nothing in Procurement has been modified.

### What it blocks

HR area 12 slice 3 retired travel's duplicate vendor master: six `VendorId` FKs (flights, hotels,
ground transport, car rentals, visa applications, insurance) now point at `Suppliers`. That is the
right design — an airline paid through travel and the same airline paid through procurement must
not be two records — but **travel's vendor selection cannot work until this controller does**, and
no supplier can be onboarded through the API at all while the create is dead.

The HR travel harness works around it by seeding a supplier directly in SQL
(`dev-harness/hr-travel/fixtures.json`), which tests HR's side of the seam and deliberately does
not depend on Procurement's endpoint.

### What a fix needs

1. Register both repositories beside `ISupplierRepository` in `ServiceCollectionExtensions.cs`.
2. Commit the create — `SaveChangesAsync`, via the repository or a unit of work.
3. Set `TenantId` explicitly on the new `Supplier`.
4. Return the created DTO in the response body.
5. Then exercise every remaining endpoint on the controller: **it has never once activated**, so
   nothing on it has ever executed and none of it is proven. Expect more of the same shapes.

### ⚠ A correction worth carrying

While this was undiagnosed, the 400 on `POST /api/Suppliers` was attributed to the staged
master-data change guard (`GuardDirectMutationAsync`), and it was reported that the travel desk
could no longer add vendors because Procurement gates supplier creation. **That was wrong.** The
400 was the DI failure, identical to every other endpoint. The guard exists but returned *allowed*
and is not blocking anything. Any decision taken on the basis of "supplier creation is gated"
should be revisited.

---

## 2. Finance — currency conversion is inverted

**Severity: blocking for any multi-currency amount, anywhere in the system.** Found 2026-08-17
while retiring HR travel's duplicate exchange-rate table onto Finance's (area 12, slice 6).

### What is broken

`GET /api/finance/currencies/convert` returns the reciprocal of the correct answer. Measured
against the running API:

```
1 USD -> GHS = 0.08     (should be ~12.5)
1 GHS -> USD = 12.5     (should be ~0.08)
1 EUR -> GHS = 0.076    (should be ~13.2)
```

### Why

Two halves of Finance disagree about what `ExchangeRate.Rate` means, and the seed data follows the
opposite convention to the code that reads it.

- **`ExchangeRate.cs` documentation** — *"1 unit of TargetCurrency = ExchangeRate units of
  BaseCurrency… Example: 1 USD = 15.25 GHS, BaseCurrencyCode = GHS, TargetCurrencyCode = USD,
  ExchangeRate = 15.25"*. So `Rate` is **target → base**.
- **`CurrencyService.ConvertAsync`** agrees with that documentation: when the row is
  `(base = to, target = from)` it returns `amount * rate.Rate`.
- **`FinanceDataSeeder.cs` (~line 284)** writes the opposite, and its own comment says so:

```csharp
// GHS to USD
BaseCurrencyCode = "GHS",
TargetCurrencyCode = "USD",
Rate = 0.08m,            // author's intent: 1 GHS = 0.08 USD  (base -> target)
InverseRate = 12.5m,     // 1 / 0.08
```

`Rate` and `InverseRate` are transposed relative to the code that consumes them. Every conversion
in the system is therefore out by the reciprocal — a factor of ~156 for USD/GHS.

### What it blocks

Any multi-currency figure the system computes. It surfaced in HR travel because slice 6 made
expense-claim lines take their rate from Finance instead of from the caller: a 100 USD hotel bill
is currently valued at **8 GHS instead of ~1,250**.

### The HR decision taken, and why

**HR travel delegates to `CurrencyService.ConvertAsync` and inherits the error deliberately.**
Reading `InverseRate` directly would make travel numerically right today and put it in open
disagreement with every other module — two truths about the same trip, which is worse than one
shared, fixable error, and is precisely the divergence slice 6 existed to remove. The reasoning is
recorded in `StaffTravelCurrencyBridge.GetRateToBaseAsync`, and the travel harness asserts
*agreement with Finance* rather than any fixed number, so it stays correct once this is fixed.

**Fixing Finance fixes travel with no change on the HR side.**

### What a fix needs

1. Decide which convention is canonical — the documentation and `ConvertAsync` already agree with
   each other, so the seeder is the odd one out and the smaller change.
2. If the documentation stands: swap `Rate` and `InverseRate` in `FinanceDataSeeder` for all three
   seeded pairs, and correct any existing rows.
3. Check every other writer of `ExchangeRate` for the same transposition — the seeder is unlikely
   to be the only place the ambiguity was resolved the wrong way.
4. ~~Re-run `dev-harness/hr-travel/run-slice6.mjs`~~ — retired with the old travel suites (2026-10-04, travel closure
   D-59; it could no longer start on UAT). The rate travel uses is now checked by `run-final-truth.mjs` §1 and
   `run-final-money.mjs` (the expense date's rate, B12).

---

---

## 3. Workflow engine — a transition's `Condition` is stored as a JSON blob and evaluated as an expression, so **conditional routing never routes**

**Module:** Workflow (shared infrastructure) · **Found by:** HR area 9b slice 15 · **2026-08-21**

### What is broken

`CreateWorkflowTransitionDto.Condition` is a **`WorkflowConditionDto`** — an object with
`ConditionType`, `Expression`, `Variables`. When a definition is created,
`WorkflowDefinitionServiceAdapter.CreateStepsAndTransitionsAsync` persists it as:

```csharp
Condition = transitionDto.Condition != null
    ? JsonSerializer.Serialize(transitionDto.Condition, WorkflowJsonOptions)
    : null,
```

into `WorkflowTransition.Condition`, which is a **`string?`**.

`WorkflowEngine` then passes that column straight to the evaluator, in four places
(`AdvanceFromStepAsync`, `ExecuteTransitionAsync`, and two others):

```csharp
if (await EvaluateConditionAsync(transition.Condition, context))
```

But `WorkflowConditionEvaluator.EvaluateConditionAsync` expects a **bare expression** —
`isProcedural == true`. What it receives is the serialised DTO:

```json
{"conditionType":"Expression","expression":"isProcedural == true","variables":null,
 "logicalOperator":"And","childConditions":null}
```

The evaluator never sees the expression it was given. Nothing errors: the evaluator catches its own
exceptions and the engine logs nothing, so a definition with conditional routing looks correct in
the admin UI, publishes cleanly, reads back with the condition intact — and routes by priority
alone.

### What was proven

A definition with two branches out of `Draft` — one conditional on `isProcedural == true` going to
an HR approval step, one default going to a Managing Director approval step:

| conditional branch priority | record | expected | actual |
|---|---|---|---|
| 1 (below the default's 2) | `isProcedural = true` | HR approval | **Managing Director approval** |
| 10 (above the default's 0) | `isProcedural = false` | Managing Director approval | **HR approval** |

The branch taken tracks **priority only**; the condition changes nothing. Measured directly: the
record read back `isProcedural: false` while the engine reported `currentStep: "HR approval"` and
`pendingApprovers: [{approverRole: "HR"}]`.

⚠ Also observed while measuring, and possibly a second defect: the instance's `DataContext` came
back **empty** through `GET /api/Workflow/instances/{id}`, even though `SimpleWorkflowService`
builds a populated context and passes it to `StartWorkflowAsync`. If the context is genuinely not
being persisted onto the instance, then `MergeDataContext(instance, stepData)` has nothing to merge
on any later step and conditions would fail on a second count. Worth checking as part of the same
fix — it may be a projection omission in the read endpoint rather than a storage problem.

### What it blocks

**Any per-record routing, in every module.** Threshold rules are the main reason
`SimpleWorkflowService.BuildEntityContextAsync` exists at all — procurement's value bands, the
requisition's headcount and budget flags, HR's `isProcedural`. All of that context is assembled,
passed in, and then ignored at the moment it would matter.

For HR area 9b specifically: FR-HR-092 wants a procedural absence termination signed by HR and
every other exit by the Managing Director. That routing cannot be expressed. **The requirement is
still enforced** — `SeparationService.RequireDecisionAuthority` refuses on the record itself and
runs before the engine's own check — so no exit can be signed by the wrong authority. What is lost
is the *assignment*: both roles have to be listed as approvers on a single step, so an HR officer
sees exits in their queue that the service will refuse them.

### What a fix needs

1. Decide which side is canonical. Storing the DTO is the richer choice (it carries
   `ConditionType`, `Variables`, `ChildConditions`), so the smaller correct change is at the read
   side: deserialise before evaluating.
2. In `WorkflowEngine`, replace each `EvaluateConditionAsync(transition.Condition, context)` with a
   helper that deserialises the stored JSON to `WorkflowConditionDto` and evaluates
   `.Expression` — falling back to treating the string as a bare expression, since
   hand-authored and seeded definitions may hold one. There are **four** call sites; fixing one is
   worse than fixing none, because the behaviour would then differ by code path.
3. Make an unparseable condition **loud**. Today it silently means "false" in the evaluator and
   "no opinion" in the engine. A definition whose routing cannot be evaluated should log a warning
   naming the definition, the transition and the expression.
4. Note for whoever fixes it: `AdvanceFromStepAsync` selects
   `validTransitions.OrderByDescending(t => t.Priority).FirstOrDefault()` — **highest priority
   wins**, not lowest. That is not documented anywhere and reads as "1st, 2nd" to anyone authoring
   a definition. Once conditions work, a conditional branch must still outrank its default or it
   can never be taken.
5. Re-run `dev-harness/hr-separation/run-slice15.mjs` with the conditional definition restored
   (it is preserved in `workflow-definition.mjs` as `publishSeparationDefinition`'s
   `useConditionalRouting` option) — it asserts the routing in both directions.

---

## 4. Inventory — the frontend does not type-check (19 errors, `tsc --noEmit`)

**Found:** 2026-08-22, running `npx tsc --noEmit` as the verification step for HR area 14 slice 11.
Nothing in HR touches these files; they were already failing.

### What is broken

`npx tsc --noEmit` in `frontend/` reports **19 errors, all in Inventory**. Three distinct causes:

| Cause | Where | Errors |
|---|---|---|
| `isStockingUnit` declared **twice** on `ItemUnitOfMeasureDto`, with different optionality | `src/services/inventoryManagementService.ts:98-99` | 5 |
| Fields read that the DTO does not declare — `lotNumber`, `batchNumber`, `serialNumber` on a requisition item union, `isStockingUnit` on a UoM | `src/components/inventory/RequisitionDialog.tsx:760`, `src/app/inventory/item-identifiers/page.tsx:330` | 6 |
| A shorthand property with no value in scope, a bad `SetStateAction` cast, a possible-null deref, and four test mocks typed against the real axios signature | `project-reservations/page.tsx`, `ShipTransferDialog.tsx`, `inventoryManagementService.identifiers.test.ts` | 8 |

### Why it matters more than the count suggests

The duplicate `isStockingUnit` (TS2717) means **the two declarations disagree about whether the
field can be undefined**. Whichever one wins, half the call sites are typed against the other. This
is the frontend twin of the shape this repo keeps producing server-side: a field that exists, that
compiles, and that carries something other than what the reader believes.

The `lotNumber` / `batchNumber` / `serialNumber` errors are the more serious ones for a user:
`RequisitionDialog` **reads and renders** three fields the item type does not declare. Either the
DTO is missing them — in which case the dialog has been showing blanks — or the union is wrong. Both
readings are defects, and the compiler cannot tell them apart from the outside.

### What it blocks

Nothing in HR. It is recorded because it makes `tsc` a **useless gate for everyone**: a clean run is
impossible, so any new type error in any module lands in a wall of 19 pre-existing ones and is not
noticed. That is the real cost — not the 19, but the twentieth.

### What a fix needs

1. Delete one of the two `isStockingUnit` declarations. Decide first whether it is optional; the
   call sites will tell you.
2. For `lotNumber` / `batchNumber` / `serialNumber`: check a live requisition-item payload before
   changing either side. If the API sends them, add them to the DTO; if it does not, the dialog has
   been rendering empty cells and the fix is in the dialog. **Do not guess from the field names** —
   that is how the wrong half gets changed.
3. Type the axios mocks in `inventoryManagementService.identifiers.test.ts` as
   `vi.mocked(...)` / `jest.Mock` rather than against the real client signature.
4. Then keep it at zero. A gate that is never green is not a gate.

---

## 5. Procurement — `GetActiveByBusinessPartnerIdAsync` orders by an unmapped property, so it throws on every call

**Found:** 2026-08-22, sweeping the data layer after HR area 14's content audit found the identical
bug in two award repositories.

### What is broken

`src/ErpSystem.Data/Repositories/Procurement/BusinessPartnerUserRepository.cs:58`

```csharp
    .OrderBy(bpu => bpu.User!.FullName)
```

`ApplicationUser.FullName` is a **computed property** — `=> $"{FirstName} {LastName}"` — with no
column behind it. EF Core cannot translate it, so the query raises
`InvalidOperationException: The LINQ expression … could not be translated` **before any SQL runs**.

The method therefore returns a result **never**. Not sometimes, not for some tenants: every call
throws.

### What was proven

The identical construct in `AwardCommitteeMemberRepository` was ordered by `Employee.FullName` and
did exactly this — `committees/{id}/members` and `committees/{id}/members/active` answered 400 on
every request, and had never once returned a row. It was caught by asserting **content** rather than
status; a status-only check sees a 4xx and reads it as a refusal working correctly.

The Procurement instance is the same expression against the same kind of property. It has not been
executed here — HR has no fixtures for business-partner users — so it is reported rather than
claimed as reproduced.

### What it blocks

Nothing in HR. It blocks whatever screen lists the active users attached to a business partner.

### What a fix needs

1. Order by the mapped columns instead: `.OrderBy(bpu => bpu.User!.FirstName).ThenBy(bpu => bpu.User!.LastName)`.
   Same result, and it can run.
2. **Then look for the others.** A sweep of `src/ErpSystem.Data` for `FullName` inside `OrderBy` /
   `ThenBy` / `Where` found four uses: two in HR (now fixed), this one, and two that are safe
   because `EmployeeReferee.FullName` and `JobCandidateReferee.FullName` are real mapped columns
   rather than computed ones. **The name alone does not tell you which kind you have** — that is
   what makes this class of bug survive review.
3. Whatever test covers it must assert the **rows**, not the status code. This bug's whole signature
   is an endpoint that answers, consistently, with an error.

---

## 6. Platform-wide — `CreatedBy` is a display-name column and much of the codebase writes an id into it

Found by the areas 19–23 slice 11 content audit, 2026-08-23. **Not fixed** — the HR stores in that
bundle were corrected, but the pattern is codebase-wide and the convention is not HR's to change
unilaterally.

### What is broken

`BaseEntity` carries **`CreatedBy` (`string?`)** and **`CreatedById` (`Guid?`)** — a name and an id,
two columns for two different jobs. A screen renders `createdBy`. Across the solution, a large
number of writers put a **raw GUID into the name column** and leave the id column null.

Confirmed instances, by grep for `CreatedBy = <something>.ToString()` / `= userId`:

```
src/ErpSystem.Api/Controllers/ReportRoleAssignmentController.cs   3 sites
src/ErpSystem.Api/Services/Maintenance/AssetTypeService.cs        1 site
src/ErpSystem.Api/Services/Finance/MultiCurrency/ExchangeRateService.cs
                                    CreatedBy = rate.CreatedByUserId.ToString(), // TODO: Resolve username
src/ErpSystem.Core/Services/HR/ApplicationPipelineService.cs      1 site
src/ErpSystem.Core/Services/HR/Appraisal/AppraisalCycleService.cs 2 sites
```

⚠ The `// TODO: Resolve username` in `ExchangeRateService` is the tell: **this is a known shortcut,
taken repeatedly, and never revisited.**

### What was proven

Measured on live DEFAULT-tenant data:

```
ExternalAssociates   7 rows, CreatedBy set on 7 — every value is an EMPLOYEE id, not a name
FacilityServices     3 rows, CreatedBy set on 3 — one GUID
OrganizationUnitHistories   142 of 208 carry a real name ("admin", "t19v_…")
```

The external-associate values were resolved against `Users` and `Employees`: they match **employee
ids**. So the register's "added by" column, the moment a screen renders it, shows a GUID.

⚠ **There is no automatic stamp to fall back on.** `ApplicationDbContext.UpdateAuditableEntities`
sets `CreatedAt`, `UpdatedAt`, `TenantId` and the soft-delete flag, and stops. The author columns
are every service's own job — which is why the behaviour varies service by service rather than
being uniformly right or uniformly wrong.

⚠ **`UpdatedBy` is filled by nothing at all** in any of the ten stores areas 19–23 touch, including
the ones that fill `CreatedBy`. Every "last modified" in those modules can say when and not by whom.

### What it blocks

Any screen with an "added by" or "last modified by" column, in every module that took the shortcut.
It is not a crash and not a wrong number — it renders, and it renders a GUID at a human.

### What a fix needs

1. **Decide the convention once**, then hold it: `CreatedBy` is the display name,
   `CreatedById` is the id. Both, not one.
2. HR already has the rule as a named helper —
   `ErpSystem.Core.Services.HR.Extensions.AuditStampExtensions` (`StampCreated` / `StampUpdated`,
   with the `FullName`-then-`Username` fallback a service account needs). Promote it out of the HR
   namespace rather than writing a second one.
3. **Or make it automatic.** `UpdateAuditableEntities` already runs on every save and already reads
   ambient state; giving it an `ICurrentUserProvider` would close the whole class. That is a
   platform decision, which is exactly why it is recorded here instead of being taken in an HR slice.
4. Backfilling is optional and probably not worth it — the existing GUIDs are recoverable, but the
   rows that carry nothing are gone for good either way.

⚠ Whatever fixes it must assert on the **value**, not on the column being non-null. A GUID in the
name column is non-null, and that is the entire defect.

---

## 7. Email templates — the designer cannot create a template the renderer will ever use

### What is broken

`TemplatedEmailService.ResolveAsync` matches a stored template on **`Module` + `EventKey`**, falling
back to the module catalog's built-in default when it finds none. But `CreateEmailTemplateDto`
(`EmailTemplateController`) has **no `EventKey` field**, and `CreateTemplate` does not set one — so
every template created through the designer at `/administration/settings/email` is stored with a null
`EventKey` and can never be resolved for any event.

The row appears in the designer, saves cleanly, previews correctly, and is dead.

### What was proven

Read from the source while wiring area 16 slice 5 (the AST-5 asset responsibility document):

- `EmailTemplateController.CreateTemplate` constructs `new EmailTemplate { Name, Module, TableName,
  Subject, HtmlBody, PlainTextBody, SelectedFields, Description, Category }` — no `EventKey`.
- `TemplatedEmailService.ResolveAsync` filters
  `t.Module == module && t.EventKey == eventKey && t.IsActive`.
- **Editing is unaffected**, and that is the saving grace: `EmailTemplateService.UpdateTemplateAsync`
  loads the existing row and copies only the editable fields, so a seeded row keeps its `EventKey`
  through an edit and continues to resolve. Reword-a-shipped-document works; author-a-new-one does
  not.

### What it blocks

Any module shipping an editable document — recruitment's transactional emails, probation's FR-HR-032
confirmation letter, HR assets' responsibility-and-terms form — is editable **only** through a row
that the seeder wrote. A tenant who deletes one, or who tries to author a variant, cannot get back to
a working template through the UI.

It is also invisible: nothing errors. The event silently falls back to the built-in default, so the
tenant sees the old wording and reasonably concludes the editor is broken rather than the row.

⚠ Compounding it, `EmailTemplateCatalogSeeder` — which is what writes those rows — sits on
`HrSeedOrchestrator`'s deliberately-skipped list ("templates are not TDC-branded yet"). Until it is
enabled, *no* module's documents are listed in the designer at all.

### What a fix needs

1. Add `EventKey` to `CreateEmailTemplateDto` and set it in `CreateTemplate`. One field, one
   assignment.
2. Have the designer offer the **event catalogue** when creating a template, rather than free text:
   the descriptors already exist (`IEmailEventCatalog`, with per-event token palettes and sample
   values), and nothing exposes them over HTTP. The interface docs describe a "token-catalogue API
   that drives the authoring palette" — **that endpoint does not exist**. Adding it is what turns the
   token list from documentation into an authoring aid.
3. Consider refusing to save a template whose `Module` matches a catalog but whose `EventKey` matches
   no event in it. A template bound to nothing should not look saved.

---

## 8. Finance — the fixed-asset → Maintenance link is a field no screen can set

**Severity: feature never usable.** Found 2026-08-24 while deciding HR's own link into the
Maintenance register (area 16, slice 9).

### What is broken

`FixedAsset.MaintenanceAssetId` (`Entities/Finance/FixedAssets/FixedAsset.cs:117`) is a nullable FK
plus navigation, commented *"Link to the physical asset/equipment record in Operations module
(One-to-One relationship)"*. The service layer handles it correctly and completely:

| site | what it does |
|---|---|
| `FixedAssetService.cs:125` | copies it from the create DTO onto the entity |
| `FixedAssetService.cs:222` | copies it from the update DTO onto the entity |
| `FixedAssetService.cs:336` | returns it on the read DTO |
| `FixedAssetDtos.cs:112, 168, 191` | declared on the read, create and update DTOs |
| `frontend/src/types/fixed-assets.ts:83, 131` | typed on the frontend, both shapes |

And the **UI never renders an input for it**. In
`frontend/src/app/finance/fixed-assets/register/new/page.tsx` the field appears exactly twice —
initialised as `maintenanceAssetId: ''` in the form state (line 35) and forwarded as
`formData.maintenanceAssetId || undefined` in the submit (line 68). There is no control, no picker,
no lookup of `api/maintenance/assets` anywhere on the page. The edit page
(`register/[id]/edit/page.tsx:62, 97`) is the same: it *loads* the existing value into state and
sends it back, so it round-trips a value that nothing can ever put there in the first place.

Because the state is initialised to the empty string and `'' || undefined` is `undefined`, every
create posts `undefined` and every edit posts back whatever was already stored — which is always
null.

### What was proven

Measured against the reference database (`ErpSystemDB`), 2026-08-24:

```
FixedAssets                                        4
FixedAssets with MaintenanceAssetId IS NOT NULL     0
MaintenanceAssets                                   1   <- see below; not real data
```

Zero of four. The link has never been used, which is consistent with there being no way to use it
short of calling the API by hand.

⚠ **The single `MaintenanceAsset` is HR's own litter, so the true figure is zero.** It is
`A12V-VEH-1` / *"Toyota Hilux (area-12 harness)"*, in the only asset category on the database,
*"Vehicles (area-12 harness)"* — both left behind by HR area-12 (staff travel) work. Recorded here
so nobody reads that 1 as evidence that the Maintenance register holds anything. **These two rows
are HR's to clear at finalization**, and they are named here rather than deleted mid-slice because
they sit in another team's tables.

### What it blocks

1. **The integration the comment promises does not exist.** Nothing anywhere reads
   `FixedAsset.MaintenanceAssetId` except the DTO round-trip that wrote it — no join, no report, no
   screen showing a capitalised asset's service history, no screen showing a serviced asset's book
   value.
2. **It is being copied as a precedent.** HR area 16 slice 9 added `CompanyAsset.MaintenanceAssetId`
   on the strength of this field being "the house convention" for reaching the Maintenance register.
   The shape is right; the claim that it is established practice was not — it is one declaration
   that has never carried a value. Anyone citing it should know that.

### What a fix needs

1. An asset picker on the fixed-asset create and edit forms, sourced from `GET api/maintenance/assets`
   — the same control the Projects module already has in its Access tab
   (`ProjectAccessTab.tsx:112-124`), which is a working example to copy rather than design.
2. Decide whether the relationship is genuinely one-to-one as the comment says. Nothing enforces it:
   there is no unique index on `FixedAssets.MaintenanceAssetId`, so two fixed assets can name one
   maintenance asset today.
3. If the link is not wanted, remove the column and the three DTO fields rather than leaving a
   documented integration that has never run.

---

## 9. Projects — maintenance follow-through throws for every tenant, because its reference data is empty

**Severity: blocking, shipped feature.** Found 2026-08-24 while researching how modules push work
into the Maintenance module (area 16, slice 9b design).

### What is broken

`ProjectService.MaintenanceFollowThrough.cs` (432 lines) implements four user-facing actions that
raise work inside the Maintenance module from a project:

- `CreateJobCardFromCustomerVariationAsync`
- `CreateWorkOrderFromCustomerVariationAsync`
- `CreateJobCardFromDefectLiabilityCaseAsync`
- `CreateWorkOrderFromDefectLiabilityCaseAsync`

Each one resolves the Maintenance reference data it needs before calling
`_jobCardService.CreateJobCardAsync` / `_workOrderService.CreateWorkOrderAsync`. The resolvers
**select an existing row and throw when there is none** — they never create a default:

```csharp
// ResolveProjectMaintenanceTypeForActionAsync  (:244-268)
?? throw new InvalidOperationException("No active maintenance types are configured for this tenant.");

// ResolveProjectPriorityLevelForActionAsync    (:271-292)
?? throw new InvalidOperationException("No active maintenance priority levels are configured for this tenant.");

// ResolveProjectWorkOrderTypeForActionAsync    (:295-312)
?? throw new InvalidOperationException("No active work order types are configured for this tenant.");
```

`WorkOrder` requires all four FKs — `AssetId`, `WorkOrderTypeId`, `MaintenanceTypeId`,
`PriorityLevelId` (`MaintenanceEntities.cs:659-668`) — so none of these paths can degrade
gracefully. `JobCard` needs `AssetId`, `MaintenanceTypeId` and `PriorityLevelId` (`:973-980`), so the
job-card paths are blocked by two of the three empty lookups and the work-order paths by all three.

### What was proven

Measured against the reference database, 2026-08-24:

```
MaintenanceTypes             0
PriorityLevels               0
WorkOrderTypes               0
MaintenanceAssetCategories   1   <- "Vehicles (area-12 harness)", HR litter
MaintenanceAssets            1   <- "Toyota Hilux (area-12 harness)", HR litter
MaintenanceSchedules         0
WorkOrders                   0
AssetAdmissions              0
```

All three lookups the resolvers search are **empty**, so every one of the four actions reaches its
`throw` on this database. The two rows that are not zero are **HR's own test litter** from area-12,
not reference data — the Maintenance module's register is empty in every direction that matters. `GlobalExceptionHandlingMiddleware` discards `InvalidOperationException`
messages, so the user does not even receive the sentence that would tell them what to configure —
they get a generic failure.

### What it blocks

1. **Four shipped actions with buttons in front of them.** `ProjectCustomerVariationsTab.tsx:101`
   and `ProjectDefectsTab.tsx:109` gate the controls on
   `project.assetLinks.some(item => Boolean(item.maintenanceAssetId))` — i.e. on a *project asset
   link*, not on the reference data. Link a maintenance asset to a project and the buttons light up;
   pressing them fails.
2. **Every other module that wants to raise maintenance work.** HR area 16 slice 9b intends to push
   an asset for repair. Following this module's pattern — the correct instinct, since it is the only
   worked example in the codebase — inherits the same block. HR seeding another module's master data
   is not an acceptable workaround, so slice 9b is being built on `AssetAdmission` instead, which
   requires no reference data at all.
3. **The Maintenance module's own PM engine.** `MaintenanceTriggerEvaluationBackgroundService` sweeps
   every 30 minutes over `MaintenanceSchedule`; with 0 schedules and 0 maintenance types it has never
   had a row to evaluate.

### What a fix needs

1. **Seed the three lookups per tenant.** `MaintenanceType`, `PriorityLevel` and `WorkOrderType` are
   classification masters, not customer data — a system seed of the obvious set (Preventive /
   Corrective / Inspection; Low / Medium / High / Critical; Standard / Emergency) makes every one of
   these paths work immediately. Note that the resolvers already search by name for `"Corrective"`,
   `"Medium"`, `"High"` and `"Standard"`, so those exact names are what the code expects to find.
2. **Gate the UI on what actually blocks it.** The buttons should be disabled when the reference data
   is missing, not only when an asset link is absent, or the user is offered an action that cannot
   succeed.
3. **Let the refusal speak.** These are `InvalidOperationException`s, which the global middleware
   strips. The sentences are good ones — they name exactly what to configure — and the user never
   sees them. Six HR areas have now needed a module-specific exception type for this reason; it is
   worth raising as a platform decision rather than a seventh workaround.

---

## 10. Maintenance — admission and discharge numbers collide within one second, and both are uniquely indexed

**Severity: blocking, and it fails as a 500.** Found 2026-08-24 while building HR's push into the
Maintenance module (area 16, slice 9b).

### What is broken

Two reference numbers are generated from a **second-resolution timestamp** and both columns carry a
**unique index**:

| entity | generator | index |
|---|---|---|
| `AssetAdmission.AdmissionNumber` | `$"ADM-{DateTime.UtcNow:yyyyMMddHHmmss}"` (`AssetAdmissionService.cs:121`) | `IX_AssetAdmissions_AdmissionNumber`, unique |
| `AssetDischarge.DischargeNumber` | `$"DIS-{DateTime.UtcNow:yyyyMMddHHmmss}"` (`AssetDischargeService.cs:128`) | `IX_AssetDischarges_DischargeNumber`, unique |

So **any two admissions, or any two discharges, created in the same wall-clock second collide** and
the second one fails. Nothing serialises them, nothing retries, and there is no per-tenant or
per-asset component in the key — two different tenants admitting two different assets in the same
second is enough.

The failure surfaces as an unhandled `DbUpdateException`, so the caller receives a bare **500** with
`GlobalExceptionHandlingMiddleware`'s generic body. Nothing tells the user, or the calling module,
that the problem is a duplicate reference number.

### What was proven

Reduced to four calls against `POST api/maintenance/asset-admissions` on the running API,
2026-08-24, with two distinct maintenance assets so nothing else could be the cause:

```
#1  (t)          -> 201  ADM-20260824103724
#2  (t, same s)  -> 500
#3  (t+~0.3s)    -> 500        <- still inside the same second as #2
#4  (t+1.2s)     -> 201  ADM-20260824103725
```

Deterministic, not a race: the only variable is whether the clock has ticked over a second.

It also broke two consecutive runs of HR's slice-9b harness in two different places — the first on
`AssetDischarges` (completing a maintenance record discharges the admission), the second on
`AssetAdmissions` — which is what a timing-dependent collision looks like from the outside.

### What it blocks

1. **Any bulk or scripted use of admissions and discharges.** A "receive these five vehicles" action,
   an import, a migration, or any automated test suite will fail on the second row. Only
   hand-paced clicking is safe.
2. **HR's push (area 16 slice 9b) at machine speed.** HR's own code is correct — its duplicate guard
   refuses a second send for the *same* asset with a 409 before this module is reached — but two
   *different* HR assets sent within one second hit this. HR is shipping no workaround: the harness
   asserts the defect exists today and paces itself around it, so the assertion turns red the day
   this is fixed.
3. **Anything the Projects module eventually does here**, for the same reason.

### What a fix needs

1. Give both numbers a component that is unique within the second. The cheapest change that keeps
   them human-readable is a short random or sequential suffix —
   `$"ADM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}"` — which is
   the shape HR's own `MNT-`, `AST-` and `SUR-` numbers already use in `AssetsServices.cs` for
   exactly this reason. Both columns are `nvarchar(50)`, so there is room.
2. Or drop the uniqueness to `(TenantId, Number)` **and** add the suffix. Tenant-scoping alone does
   not fix it; the collision is within a tenant.
3. Either way, catch the duplicate-key failure and answer **409 with a sentence**, rather than a 500
   with a generic body. A reference-number clash is a state clash, not a server error.
4. ⚠ Check the other generators in the module before assuming these two are the only ones. The same
   `{PREFIX}-{yyyyMMddHHmmss}` pattern appears elsewhere; only these two were proven, because only
   these two are on HR's path.

---

## 11. Platform-wide — bare `[Authorize]` behind the external-user allowlist: one prefix is exposed TODAY (procurement, proven), the rest are one allowlist edit away

**Registered 2026-08-24 by the HR W3 permissions sweep; corrected the same day after the sweep's
harness surfaced `ExternalUserAccessMiddleware`. Part (a) is a live defect in procurement's
surface now; part (b) is the prerequisite HR area 26 (candidate portal) must not ship without.**

### The two layers, and what each actually covers

The platform already has a **deny-by-default allowlist** for external accounts:
`Middleware/ExternalUserAccessMiddleware.cs`, registered between `UseAuthentication` and
`UseAuthorization`. A token carrying the `ExternalUser` role is refused (403 "External users are
not permitted to access this resource") on every path except a curated prefix list — `/api/auth`,
`/api/tenant`, **`/api/procurement` (the whole prefix)**, `/api/notifications`, `/api/hubs`,
`/api/fileupload`, `/api/ehc/external`, `/api/projects/external`, `/api/estate/external`,
`/api/user/profile`, `/api/user/change-password`, `/health`, `/swagger`.

So a bare `[Authorize]` (which admits *any* authenticated user) is shielded from external
accounts **only by that middleware**, and the shield has two failure modes:

**(a) LIVE NOW — endpoints under an allowlisted prefix.** The middleware waves externals through
the whole prefix; a bare `[Authorize]` endpoint underneath has no second gate. **Proven
2026-08-24** with a freshly self-registered `ExternalUser`-role account:

| Probe | Result |
|---|---|
| `GET /api/procurement/business-partners` | **200** — the internal business-partner register, paged |
| `GET /api/procurement/partner-categories` | **200** |
| `GET /api/PurchaseOrders` (outside the prefix) | 403 (middleware) |

Procurement has **61 bare-`[Authorize]` controller classes** and the entire `/api/procurement`
prefix is allowlisted ("existing supplier/external portal features live under procurement" — the
comment allowlists far more than the portal features). Also bare under allowlisted prefixes:
`NotificationsController` (`/api/notifications`), `FileUploadController` (`/api/fileupload` —
possibly by design for portal uploads, but then the *endpoint* should say so), and
`TenantController` (`/api/tenant`). Each needs its owner's judgment: gate the endpoint, or narrow
the prefix.

**(b) LATENT — everything else, until area 26 widens the allowlist.** The candidate-portal
decision puts self-registering job candidates on the main JWT scheme as `ExternalUser`s, and
serving them will require **adding recruitment/candidate prefixes to this allowlist** — at which
moment every bare `[Authorize]` under a newly allowed prefix goes live to the public, exactly as
procurement's did. HR closed its own surface on 2026-08-24 precisely for that day: every bare
`[Authorize]` in `Controllers/HR` (1,834 actions across 156 files) is now
`[Authorize(Policy = "InternalOnly")]` (authenticated AND NOT `ExternalUser`,
`ServiceCollectionExtensions.cs` ~1207) — a second gate that holds even where the middleware is
told to stand aside. The rest of the API was measured the same day and **not** edited, because
the files belong to other teams:

| Module (Controllers/…) | Classes | Bare `[Authorize]` at class level | No class attribute |
|---|---|---|---|
| (root — shared platform: Documents, FileUpload, Reports, Notifications, …) | 39 | **28** | 2 |
| Procurement | 72 | **61** | 1 |
| Finance | 55 | **52** | 1 |
| Maintenance | 56 | **33** | 2 |
| Sales | 18 | **17** | 0 |
| Inventory | 24 | **11** | 0 |
| Estate | 10 | **9** | 0 |
| Pricing | 4 | 4 | 0 |
| Crm / DocumentManagement / Legal / Optimized / Planning / Procedures / Projects | 9 | 7 | 0 |
| Ehc (already largely on `InternalOnly`) | 30 | 0 | 23* |
| **Total non-HR** | **319** | **222** | **29** |

\* "No class attribute" is not proven-anonymous — most gate per-method (Ehc does) — but each one
needs its owner's eyes, because a method added without an attribute there is anonymous by default.

**One HR-adjacent exception remains open inside HR's own folder:**
`Controllers/HR/PayrollController.cs` (`api/hr/payroll`, 87 actions) is the payroll developer's
file and was deliberately left out of HR's sweep. Today the middleware shields it (`api/hr/*` is
not allowlisted); it becomes the single unswept surface behind `api/hr/*` the day the allowlist
is widened near it. The entire fix is one line:
`[Authorize]` → `[Authorize(Policy = "InternalOnly")]` at the class level.

### What it blocks

Part (a) blocks nothing — it is a **live exposure** in procurement's surface, reachable today by
anyone who completes the public business-partner self-registration. Part (b) blocks HR area 26:
the allowlist cannot be widened for candidates until the prefixes being widened — and anything
else opened with them — carry their own gates. HR's sweep protects HR only.

### What a fix needs

1. **Procurement (now):** audit what under `/api/procurement` external portal users actually need,
   gate the rest (role/policy per endpoint, or the one-line `InternalOnly` sweep per class), or
   narrow the middleware prefix to the portal-facing routes. The two proven-open endpoints above
   are the starting list, not the whole of it — 61 bare classes sit under the prefix.
2. **Platform (with area 26):** every module owner makes the same one-line-per-file change HR made
   (a scripted, line-exact, BOM/CRLF-preserving replace; HR's run is in the W3 plan,
   `plans/HR-W3-Permissions-Sweep-Build-Plan.md`), **or** the platform decides once: register
   `InternalOnly` as the authorization **FallbackPolicy** (catches attribute-less endpoints)
   and/or fold "not ExternalUser" into the default policy — preceded by an audit of every
   endpoint the external portal legitimately calls with bare `[Authorize]`, or it takes the
   portal down in one deploy. The middleware then remains what it is today — a good outer wall —
   instead of the only wall.
3. Keep the two layers keyed to the same fact (`ExternalUser` role) in sync deliberately: the
   middleware allowlist says where externals may go; endpoint gates say what they may do there.
   A prefix added to the allowlist without endpoint-level gates underneath is exactly how (a)
   happened.

---

## 12. Payroll — FYI, not a defect: HR now READS `PayrollPayslipSnapshots` (the portal's "My Payslips")

**Added 2026-08-26 (HR area 25 slice 10).** For the payroll owner's awareness — nothing here is
broken, and no payroll code was touched.

### What HR consumes

The employee self-service portal serves `GET /api/employee-portal/payslips` (+ `/{id}`) by
querying the **`PayrollPayslipSnapshots` table directly** (read-only, `AsNoTracking`, scoped to
the token's employee, joined to `PayrollRuns` for the period columns), and the portal home
aggregate shows the newest snapshot (by `PayPeriodTo`) as "latest payslip". The detail route
deserializes `SnapshotJson` into `PayrollPayslipDto` server-side. Per the agreed boundary
(payroll-ownership / D4 in the area-25 plan): HR never calls a payroll service, never writes,
never recomputes — the frozen snapshot is the single source, precisely so payroll's live-run
logic stays payroll's.

### What that makes worth knowing when payroll changes

1. **Schema/JSON shape is now a consumed contract.** `SnapshotJson` is serialized at
   `PayrollService.cs` (`JsonSerializer.Serialize(payslip)`, no options → **PascalCase**) from
   `PayrollPayslipDto`. Renaming its properties, or introducing a naming policy on that
   serialize call, changes what 7,000+ employees' payslip screens render. The columns HR reads:
   `EmployeeId`, `PayslipNumber`, `GeneratedAt`, `GrossIncome`, `NetIncome`, `TaxAmount`,
   `EmployeeContribution`, `SnapshotJson`, plus `PayrollRun.{RunNumber, PayPeriod,
   PayPeriodFrom, PayPeriodTo, CurrencyCode, IsSeparateBonusRun}`.
2. **Recalculating a run hard-deletes its snapshots** (`ClearRunDetailsAsync` →
   `ExecuteDeleteAsync`). That is payroll's prerogative — but it now means a payslip an
   employee saw yesterday can 404 today until snapshots are regenerated. The portal handles it
   gracefully (404 → friendly copy); if regeneration after recalculation ever becomes optional
   rather than habitual, employees will notice.
3. **`PayrollController` is still bare `[Authorize]`** (already recorded under #11's HR-adjacent
   exception) — unrelated to this read, but worth folding into the same one-line fix pass.

---

## 13. Payroll — creating an employee profile through the API has never worked (tracked-graph FK 547)

**Found 2026-08-26** while building the portal's payslip harness (HR area 25 slice 10).

### What is broken

`POST /api/hr/payroll/employee-profiles` **500s for every NEW profile**, with any payload —
explicit `paymentMethods` rows or none. Root cause is the codebase-wide tracked-graph trap
(`BaseEntity.Id = Guid.NewGuid()` at construction): `UpsertEmployeeProfileAsync`
(`PayrollService.cs:2358`) `Add()`s the new profile FIRST, then `UpsertPaymentMethods`
(`:13420`) attaches payment-method rows via navigation (`profile.PaymentMethods.Add`,
`profile.DefaultPaymentMethod = …`). Entities discovered by fixup with a pre-set key are
tracked **Modified**, so SaveChanges emits an `UPDATE PayrollPaymentMethods … WHERE Id=@p`
for a row that was never inserted, and the profile INSERT's `DefaultPaymentMethodId` FK
fails — `SqlException 547` on
`FK_PayrollEmployeeProfiles_PayrollPaymentMethods_DefaultPaymentMethodId`.

### What was proven

Measured live (Staging, 2026-08-26): two fresh employees, both payload shapes, 500 both
times with the FK 547 in the API log. And the corroborating fact: **the database held ZERO
rows in `PayrollEmployeeProfiles`** before HR's harness seeded its fixtures — the create
path has never once succeeded, so payroll has never been able to enrol an employee through
its own API/UI.

### What it blocks

The whole payroll module, practically: no profile → `calculate` silently includes nobody →
no payslips → no snapshots. HR's portal payslip surface (area 25 slice 10, entry #12 above)
reads snapshots and honestly shows "no payslips yet" tenant-wide until this is fixed.
(HR's harness seeds profiles by SQL to verify its own adapter — around the API, never
through payroll code.)

### What a fix needs

In `UpsertPaymentMethods` / `UpsertEmployeeComponents`: explicitly `_context.Add(...)` the
rows constructed there (or set `EntityState.Added`) instead of relying on navigation fixup —
the standard remedy for this trap elsewhere in the codebase. One place each; the edit-path
variant (adding new rows to an EXISTING profile) has the same bug and the same fix.

---

## 14. Workflow platform — the pending feeds die mid-stream the moment they have content

**Found 2026-08-26** while building the portal approvals inbox (HR area 25 slice 11).

### What is broken

`GET /api/Workflow/approvals/pending` and `GET /api/Workflow/tasks/pending` return the RAW
EF entity graphs (`WorkflowApproval`, `WorkflowStepInstance`) — no DTO. The graph is cyclic
(`WorkflowApproval.StepInstance ↔ WorkflowStepInstance.Approvals`, plus the instance →
definition → steps chain), so serialization throws AFTER the 200 status line has gone out:
the client sees **HTTP 200 with a connection that terminates mid-body**. The same shape
kills the RESPONSE of `POST /api/Workflow/approvals/{id}/process` (it returns
`{ success, data = approval }` — the entity again), so every process call reports a network
error to its caller **after the action has executed server-side**.

### What was proven

Measured live (Staging, 2026-08-26, probe-slice11): both GET feeds answer `HTTP 200` +
`BODY READ FAILED: terminated` whenever the caller has ≥1 pending row, and `[]` cleanly when
empty — the endpoints have effectively never returned content. The process POST terminated
the same way while the approval row WAS consumed (the mobile inbox count dropped 2 → 1).

The server log names it exactly:

```
System.Text.Json.JsonException: A possible object cycle was detected ... Path:
$.data.StepInstance.WorkflowInstance.WorkflowDefinition.Steps.WorkflowDefinition.Steps.
WorkflowDefinition.Steps. ... .DefinitionKey.
RequestPath: /api/Workflow/approvals/pending
```

followed by a second, misleading error as the global handler tries to turn it into a 500:
`System.InvalidOperationException: StatusCode cannot be set because the response has already
started` (`GlobalExceptionHandlingMiddleware.cs:247`). That pair — cycle exception, then
"response has already started" — is the signature to grep for; it hides the real fault and is
why the endpoints look healthy from the outside.

### What it blocks

Any consumer of a non-empty feed: the admin workflow dashboard's `pendingApprovals` stat
card silently loses its count (its `Promise.allSettled` swallows the failure), and nothing
can build on the two feeds. The HR portal inbox (area 25 slice 11) does NOT consume them —
it projects its own flat DTO — which is how this stayed invisible for so long: the one
working feed (`workflow/platform/mobile/inbox`) projects an anonymous shape.

### What a fix needs

Project DTOs instead of returning entities from the three actions (`approvals/pending`
~WorkflowController.cs:2327, `tasks/pending` :1937, and the `ProcessApproval` response
:2539). The flat projection the HR portal added (`EmployeePortalController.GetInbox`) shows
the shape; alternatively `ReferenceHandler.IgnoreCycles` masks the crash but still ships
unbounded graphs.

---

## 15. Workflow platform — a generic-inbox approval consumes the approval but strands the entity

**Found 2026-08-26**, same probe. The severe one.

### What is broken

Approving through the generic surfaces — `POST /api/Workflow/approvals/{id}/process`,
`POST /api/Workflow/steps/{id}/process`, or the mobile inbox's
`POST /api/workflow/platform/mobile/actions` — drives `WorkflowEngine.ProcessStepAsync`
ONLY. The module's `IWorkflowStatusAdapter` is **never applied**: adapters are invoked by
each module's own approve endpoint (e.g. `StaffMovementService.ApproveAsync` →
`_workflowIntegrationService.ProcessApprovalAsync` → `adapter.ApplyApprovalOutcome` → save).
The controller's only integration hook, `TryApplyPostApprovalIntegrationAsync`
(WorkflowController.cs:2552), handles ProcedureCases and ServiceRequests — nothing else.
So a generic-inbox approval consumes the `WorkflowApproval` row and advances the instance
while the business entity **stays `Submitted` forever**, with no pending approval left that
could ever move it.

### What was proven

Measured live: two staff movements approved/rejected via `approvals/{id}/process` (string
enum bound fine, the engine advanced, the approver's inbox emptied) — both movements still
`Submitted` on re-read, minutes later. The module's own `staff-movements/{id}/approve`
lands the same definition's outcome correctly (every prior area harness proves it).

### What it blocks

The existing `/workflow/inbox` page (backed by `mobile/actions`) is a strand-the-record
button for every module whose status lives behind an adapter — the approver believes they
approved; the requester sees a request stuck in Submitted with no approval pending. Every
module on the engine is affected. The HR portal inbox (slice 11) is READ-AND-NAVIGATE for
exactly this reason: rows deep-link to the record page, whose module commands do the whole
job.

**Staff travel, named (2026-10-04, travel closure lane 10).** A trip approved from the generic inbox stays
**Submitted**. It also skips everything travel's own approve does after the engine:
- the checks travel's approve makes before the engine — at stage 1 the traveller's line authority, and never the
  traveller themselves (D-7; the route leaves `PreventInitiatorApproval` off because the service is the control). By
  the route's design, then, an HR officer who is travelling could approve their own trip at the HR stage from the inbox
  (read from the code, not tried);
- the final approver's `ApprovedById`;
- the trip's working days on attendance;
- the traveller's and desk's notices.

An **employee separation** approved there stays PendingApproval and cancels none of the leaver's trips (travel closure
9c). Travel's own door is the desk's *Approvals* queue and the trip page, whose verbs carry the whole outcome.

### What a fix needs

The generic process endpoints must apply the entity's status adapter after a successful
engine step — the recall path already does exactly this
(`TryApplyRecallStatusAsync`, WorkflowController.cs:1728: registry lookup →
`ResolveWorkflowEntityForAdapterAsync` → apply → save); the approval/rejection outcome
needs the same treatment wherever `ProcessStepAsync` is driven generically (ProcessApproval,
ProcessStep, mobile/actions). Alternatively: derive the outcome from the instance status
after the step, as `SimpleWorkflowService`'s submit path does.

---

## 16. Platform notifications — any user could plant a notification in any user's feed (GATED in HR slice 11)

**Found and closed 2026-08-26** (HR area 25 slice 11). Recorded because the fix sits in a
platform controller HR does not own.

### What was broken

`POST /api/Notifications` sat behind the controller's plain `[Authorize]` and takes an
arbitrary `RecipientId`, title, message and `actionUrl`. Measured live: a plain Employee
token created a notification addressed to another user, 201. Combined with the engine's own
"Approval Required" rows, an employee could put an official-looking, arbitrary-link row into
anyone's bell — a phishing-shaped hole the self-service portal would have surfaced to every
employee.

### The fix (applied)

`[Authorize(Roles = "SuperAdmin,TenantAdmin")]` on the action — the same gate its `send-push`
sibling already carried. Safe because backend modules write through `INotificationService`
directly (not HTTP), and the only frontend wrapper (`notificationService.sendNotification`)
has zero callers. If the platform team ever wants user-created notifications, they need a
recipient-authorization rule, not the open door.

---

## 17. Finance — a fixed-asset approval links approvers to a route that does not exist

**Found 2026-08-26** by the area-25 slice-11 route-resolution sweep over every `ActionUrl`
`WorkflowEntityDisplayService` can emit (the portal inbox surfaces them, so they were checked
for the first time).

### What is broken

`WorkflowEntityDisplayService.cs:890` sets `ActionUrl = $"/finance/fixed-assets/register/{entityId}"`
for the `FixedAsset` entity type. That route has no page: `frontend/src/app/finance/fixed-assets/register/[id]/`
contains only `edit/`, so the detail URL 404s. Only `/finance/fixed-assets/register/{id}/edit` exists.

### What was proven

All 71 `ActionUrl` values in the display service were resolved against the Next.js app tree:
**70 resolve, this one does not** (the 25 HR ones are all clean — area 25 slice 7 fixed that
family). Sibling fixed-asset URLs on lines 940–960 (`verification/{id}`, `capital-projects/{id}`,
`leases/{id}`) resolve correctly, so this is a single missed route, not a pattern.

### What it blocks

Anyone approving a fixed-asset workflow item from a generic inbox (including HR's new portal
inbox, which links approvals to the record) lands on a 404 instead of the asset.

### What a fix needs

Either add `register/[id]/page.tsx` (the detail page the edit screen implies), or point the
ActionUrl at `/finance/fixed-assets/register/{id}/edit` — the owning team's call on whether a
read-only detail view is wanted.

---

## 18. Payroll — loan and salary-advance records are readable by any authenticated user

**Found 2026-08-26** while surveying which employee-self-service features have a backend
(HR area 25 slice 12).

### What is broken

`PayrollController` carries a bare `[Authorize]` at the class level (`:12`) with **no
per-action policy**, and its loan/advance reads take an employee identifier as a query
parameter with no self-scoping: `GET loans` (`:245`), `GET loans/repayments`, and
`GET salary-advances` (`:260`). Any authenticated user in the tenant — including a plain
Employee-role account — can therefore read any colleague's loan balances and salary advances
by supplying their employee number.

### What it blocks / why it matters

Nothing functionally; it is a disclosure. Loan and advance balances are among the most
sensitive rows in an HR system, and the self-service portal puts an authenticated session in
every employee's hands, which raises the practical exposure considerably.

### What a fix needs

Per-action policies on `PayrollController` matching the rest of the module's surfaces, plus
self-scoping (or a `/me` arm) for the reads an employee legitimately needs. HR deliberately
did **not** build a "my loans" portal surface for this reason — there is no self-scoped read
to ride, and `PayrollSalaryAdvance` has no requester/status/approval columns, so there is no
request lifecycle to expose either.

---

## 19. Helpdesk (Ehc) — the Employee role can read every internal ticket in the tenant

**Found 2026-08-26**, same survey.

### What is broken

`EhcInternalTicketsController` (`:13-20`) is `[Authorize(Policy = "InternalOnly")]` **plus a
role list that includes `Constants.Roles.Employee`**. Its `GET` (`:34-69`) is the desk queue —
it filters by status, priority and department, and carries **no requester filter**. So an
Employee-role caller can enumerate every internal ticket in the tenant, including tickets
raised about other people.

### What was proven

Read from the source; the requester-scoped path exists and is simply not used by this action:
`EhcTicketRepository` (`:62-67`, `:89-92`) filters on `RequesterUserId`, and
`EhcTicketService.GetMyTicketsAsync` (`:729`) is the self read — but it is only reachable
through `EhcExternalTicketsController` (`ExternalOnly`), which employees are excluded from.

### What a fix needs

Drop `Employee` from the role list on the list action (keep it on create), and add a
self-scoped `api/ehc/me/tickets` arm reusing `GetMyTicketsAsync` / `CreateInternalTicketAsync`.
That is also the cheapest route to an "Ask HR" surface in the employee portal — recorded here
rather than built, because the controller belongs to the helpdesk team.

---

## 20. Shared reporting — the frontend type-check **crashes the TypeScript compiler**, so no module can be type-checked

**Found 2026-08-28** while merging master (f71d6917) into hrdev (merge #8). Reproduced on
`origin/master` **standalone**, so this is not a merge artefact.

### What is broken

`npx tsc --noEmit` does not fail — it **aborts**:

```
Error: Debug Failure. No error for last overload signature
    at resolveJsxOpeningLikeElement (typescript/lib/_tsc.js:77292:12)
    at checkJsxSelfClosingElementDeferred (typescript/lib/_tsc.js:74140:5)
    at checkDeferredNodes (typescript/lib/_tsc.js:86617:27)
```

This is an internal compiler assertion, not a diagnostic. Because it throws mid-check, tsc emits
**zero** diagnostics and exits 1 — so the run looks like a hard failure with no error list, and
every genuine type error in every module is masked behind it.

### What was proven

A `--generateTrace` run names the file: checking begins on
`frontend/src/components/reports/StatutoryReportCataloguePage.tsx` and never completes. The
deferred node is a self-closing JSX element whose tag is a **variable**, not a literal —
`const Icon = item.icon;` then `<Icon className="h-4 w-4" />` (`:813-815`), where
`CatalogueItem.icon` is typed `LucideIcon` (`:83`).

Three runs, TypeScript 5.9.2, `strict: true`, `skipLibCheck: true`:

| Tree | Result |
|---|---|
| `backup/hrdev-premerge` (hrdev before the merge) | completes, exit 2, reports the known inventory errors (defect #4) |
| `origin/master` **alone** | **crashes**, exit 1, 0 diagnostics |
| the merge result | **crashes**, identically |

Ruled out: the incremental cache (removed, still crashes), `.next/types/**` (excluded, still
crashes), and any dependency drift — `package.json`, `package-lock.json` and `tsconfig.json` are
byte-identical across all three trees, same TypeScript 5.9.2 from the same `node_modules`.

The file was rewritten on master in this range (+983/-140), which is where the trigger entered;
`CatalogueMode` went from a two-member union to a much wider one and `CatalogueItem` became
exported. The precise minimal trigger has not been isolated — that belongs with the owning team.

### What it blocks

- **The whole repository's type-check gate.** Not one module: the compiler never finishes, so
  no module's errors are reported. Any CI step running `tsc --noEmit` is now uninformative.
- It supersedes defect #4 in practice — inventory's 19 errors are still there (they show on the
  pre-merge tree) but are now invisible.
- For HR specifically it removes one of the three legs of our UI verification method
  (`tsc` + `next lint` + route resolution), which is the only verification available with no
  browser automation in this environment.

### What a fix needs

Isolate the JSX element that trips the assertion in `StatutoryReportCataloguePage.tsx`.

**Do not repeat this experiment — it has been run and it fails.** The obvious suspect was the
dynamic-tag render `const Icon = item.icon;` then `<Icon className="h-4 w-4" />` (`:811-815`),
where `CatalogueItem.icon` is `LucideIcon`. Typing it explicitly
(`const Icon: LucideIcon = item.icon`) **does not stop the crash** — verified against the merged
tree with a clean cache. Excluding the file in `tsconfig` does not help either, because it is
still pulled into the program by its importers (`app/reports/page.tsx`, `ReportModuleNavigator`).
The untested candidates that remain are the six bare `<SelectValue />` renders
(`:935, 1007, 1025, 1045, 1063, 1084`).

Scope note: `next.config` sets `typescript.ignoreBuildErrors: true`, so this does **not** break
production builds. What it breaks is the `tsc --noEmit` gate — for every module at once, because
the compiler aborts before emitting any diagnostics.

---

## 21. Procurement / Shared reporting — three migrations drop constraints that a model-built database never had, so `database update` cannot complete

**What is broken.** Three migrations issue a raw `ALTER TABLE ... DROP CONSTRAINT` with no
existence guard:

| Migration | Constraint it drops unguarded |
| --- | --- |
| `20260722074659_AddProcurementRequisitionSourcingReleases` | `FK_RequestForQuotations_PurchaseRequisitions_SourcePurchaseRequisitionId` |
| `20260805100000_TDC0705ConfigurableReportTemplates` | `CK_ReportTemplates_Audience` |
| `20260812170000_ExtendControlledSourcingMethods` | `CK_ProcurementTenderControls_State`, `CK_ProcurementExceptionalSourcingControls_Core` |

**What was proven.** Applying the migration chain after a master→hrdev merge fails on:

```
Applying migration '20260812170000_ExtendControlledSourcingMethods'.
Msg 3728: 'CK_ProcurementTenderControls_State' is not a constraint.
Could not drop constraint. See previous errors.
```

`CK_ProcurementTenderControls_State` is created **only** inside `migrationBuilder.Sql` in
`20260723071908_AddProcurementTenderStatutoryControls`. It is not declared on the EF model —
`grep HasCheckConstraint` finds entries for HR and Quantity Survey, none for Procurement.

**The mechanism, confirmed on a live developer database.** `rebuild-db` (`Program.cs`) calls
`EnsureCreatedAsync()` — which builds tables from the EF model and therefore creates no check
constraints and no triggers — and then `StampCurrentModelMigrationsAsAppliedAsync`, which inserts
a `__EFMigrationsHistory` row for **every migration in the assembly without running any of them**.
The database is left claiming a complete chain while missing every object that only exists in
migration SQL.

Queried on the affected database:

| Check | Result |
| --- | --- |
| `20260723071908` in `__EFMigrationsHistory` | **1** — recorded as applied |
| `sys.check_constraints` on `ProcurementTenderControls` | **no rows** — never created |
| `OBJECT_ID('TR_ProcurementTenderControls_Lifecycle')` | **exists** |

The trigger surviving while the constraint does not is the tell, and it is not a contradiction:
the trigger is re-issued as `CREATE OR ALTER TRIGGER` by a later migration
(`20260723184003_AddProcurementTenderDocumentControls`), which is idempotent and succeeds against a
database that never had it. The check constraints use a bare `ADD CONSTRAINT` and get no such
second chance. **Idempotent DDL survived the stamping; non-idempotent DDL did not.**

**Why patching the one constraint is not enough.** The same migration's second statement rewrites
trigger `TR_ProcurementTenderControls_Lifecycle`, which is *also* created only in migration SQL and
is *also* absent from a model-built database. It is guarded — but the guard is a `THROW`:

```
IF @definition IS NULL ... THROW 51109, 'The controlled-tender lifecycle trigger is missing ...'
```

So recreating the check constraint by hand simply moves the failure one statement along, to 51109.
Every migration-SQL object this chain assumes is missing on such a database.

**What it blocks.** Any developer whose database was built with `rebuild-db` cannot bring it forward
with `dotnet ef database update` — the chain stops at the first unguarded drop. It also means these
migrations are not re-runnable, so a partially-applied chain cannot be resumed.

**What a fix needs.** Guard each drop the way HR's own migrations do — the shape is in
`20260828170021_RepointCompanyScheduleStationToLocation`:

```sql
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE [name] = N'CK_ProcurementTenderControls_State'
             AND [parent_object_id] = OBJECT_ID(N'[dbo].[ProcurementTenderControls]'))
    ALTER TABLE [dbo].[ProcurementTenderControls] DROP CONSTRAINT [CK_ProcurementTenderControls_State];
```

and make the trigger rewrite create-if-absent rather than `THROW`. The deeper question for the
owning team is whether Procurement's check constraints and triggers should be declared on the EF
model (`HasCheckConstraint`, as Quantity Survey does) so a model-built database gets them too. As
long as they live only in migration SQL, `rebuild-db` and `database update` produce two different
databases, and only one of them can run the chain.

---

## 22. Procurement / Projects / Quantity Survey — a soft-deleted document keeps its number, so deleting one request can break every later create

**What is broken.** Three tables outside HR carry a **unique index on their request number with no
`IsDeleted` filter**, while their delete is a soft delete:

| Table | Index |
|---|---|
| `ProcurementMasterDataChangeRequests` | `IX_ProcurementMasterDataChangeRequests_TenantId_RequestNumber` |
| `ProjectInvoiceRequests` | `IX_ProjectInvoiceRequests_TenantId_RequestNumber` |
| `QuantitySurveyJointMeasurementRequests` | `IX_QuantitySurveyJointMeasurementRequests_TenantId_RequestNumber` |

A soft-deleted row therefore goes on owning its number. Whether that is *live* depends on how each
module mints the next one: a generator that **counts live rows** will re-issue a number the index
still holds, and the insert then fails for good. A generator that reads a max, or uses the
platform's `INumberSequenceService`, is safe.

**What was proven.** The index shape is established, not guessed — from `sys.indexes` on
`ErpSystemDB`, filtering unique non-primary indexes over columns named `RequestNumber` and
friends. What was **not** checked is which of these three generators is count-based; that is the
owning teams' code and the one-line difference between latent and live.

**Proven live in HR, on the identical shape.** `StaffTravelRequests` had exactly this index and a
count-based generator. Deleting a single travel request dropped the live count to zero, the next
create re-minted `TR-2026-00001`, and it collided with the soft-deleted row — **every subsequent
travel request in that tenant failed with a 500 naming nothing**. Fixed in HR on 2026-08-30
(closure ledger D-27); five more HR tables are latent and tracked as D-28. The failure is
completely silent until someone deletes one document, which is why it survived a shipped area.

**What it blocks.** Nothing today. It blocks document creation permanently, per tenant, from the
first deletion onwards — and the error names neither the column nor the constraint, so it will be
diagnosed as "the module is broken" rather than as one stale row.

**What a fix needs.** For each of the three, read the generator first: if it counts, either take
the maximum already issued (including soft-deleted rows) or move it onto
`INumberSequenceService`, seeding the sequence from the table's current maximum so it keeps issuing
after the numbers already in the wild. Filtering the unique index on `IsDeleted = 0` is the other
option and needs a migration; HR has consistently chosen not to, because reusing a retired
document's number is worse than the collision it prevents.

---

## 23. Payroll — the employee-profile upsert cannot create a NEW profile (FK cycle on insert)

**What is broken.** `POST api/hr/payroll/employee-profiles` → `PayrollService.UpsertEmployeeProfileAsync`
fails with SQL error 547 for every employee payroll does not already know:
`FK_PayrollEmployeeProfiles_PayrollPaymentMethods_DefaultPaymentMethodId`. Updating an existing
profile works; creating one does not. So payroll's own Employee Profiles screen can only maintain
people who arrived through the legacy reconciliation import (`ImportLegacyEmployees…`, which
inserts the profile row without a default payment method).

**Why.** `UpsertPaymentMethods` (`PayrollService.cs` ≈13420–13512) adds the payment method to
`profile.PaymentMethods` AND sets `profile.DefaultPaymentMethod` to it in the same unit of work —
both branches, the "seed a default" one for an empty list and the "apply the supplied rows" one.
The two rows point at each other (`PayrollPaymentMethod.EmployeeProfileId` →
`PayrollEmployeeProfiles`, `PayrollEmployeeProfile.DefaultPaymentMethodId` → `PayrollPaymentMethods`),
keys are client-generated Guids, and EF inserts the profile first with the default already set, so
the profile insert references a method that does not exist yet.

**What was proven (2026-09-02, Staging, `ErpSystemDB`).** Two calls with a fresh employee: one with
`paymentMethods: []`, one with a single 100 % bank row. Both 500 with the same FK message in the
API log. The HR harness `dev-harness/hr-payroll-membership` reproduces it on every run.

**What it blocks.** The HR payroll-membership bridge (finish plan lane 3f): when HR marks an
employee "on payroll", HR enrols them through this upsert, create-only. Until this is fixed the
enrolment cannot go through payroll's code path. **HR's interim handling:** `PayrollMembershipService`
tries the upsert first and, on failure, inserts the minimal profile itself in two saves (profile +
salary basis + one default bank method, then the default pointer) and logs a warning naming this
entry. The fallback is deliberately the same shape the upsert would have produced; remove it once
the upsert creates profiles.

**Hand-off.** A self-contained report for the payroll owner — reproduction, mechanism, fix options
and what to tell HR afterwards — is `docs/HR/integration/handoffs/HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`, with a
runnable one-file repro at `…\TDC ERPS\dev-harness\hr-payroll-membership\repro-payroll-profile-create.mjs` (outside the repo)
(plain node, no dependencies; verified 2026-09-02, both variants 500).

**What a fix needs.** In `UpsertPaymentMethods`, do not assign `DefaultPaymentMethod` while the
profile is new — save the profile and its methods first, then set `DefaultPaymentMethodId` and
save again (or make the default a computed "lowest active SequenceNo" and drop the column). Then
delete the HR fallback.

---

## 24. Shared frontend auth — the `/auth/me` mapper dropped every field it did not name, so a linked user became "unlinked" after five minutes or a reload

**What is broken.** `authService.getCurrentUser()` (`frontend/src/services/auth.ts`) re-mapped the
`/auth/me` response into the frontend `User` object by copying a fixed list of twelve fields. Anything
the server sent that was not on the list was discarded: `employeeId`, `currentTenantId` /
`currentTenantCode` / `currentTenantName`, `accessibleTenants`, `authenticationProvider`,
`mustChangePassword`, `temporaryPasswordExpiresAtUtc`. The list dates from the initial commit and was
never extended as the backend grew.

**Why it hid.** The login response is put straight into the React Query cache with every field, and
the query's `staleTime` is five minutes. So for five minutes after signing in, or until the first page
reload or new tab, the user object is complete. After that the query refetches through the mapper and
the fields vanish. Any screen that reads them then misbehaves without an error: the self-service
portal (`/me`) showed its "your account isn't linked" page to linked staff; the tenant switcher lost
the user's tenant list; the must-change-password gate went quiet.

**What was proven (2026-09-02, UAT demo database).** `hr.head` is linked to `TDC/00009` in `Users`,
`/auth/me` returns `employeeId` for it, and the `/me` shell still rendered the unlinked notice once
the five-minute cache had expired. Thirteen frontend screens read `user.employeeId`.

**Fixed inline (exception to the rule below).** The change is a one-line `...userInfo` spread at the
top of the mapped object, keeping the explicit fields after it, plus `employeeId` added to the
`UserInfo` interface in `api.service.ts`. It was applied on the HR branch because the stakeholder demo
runs the self-service portal and there was no HR-side workaround. Owning team: review that the spread
is acceptable (the stored `localStorage.user` now carries the full server shape) and keep it.

---

## 25. Cross-module findings from the HR/SHE demo-dataset build (2026-09-04)

Found while filling every HR and SHE table in `ErpSystemDB_UAT` through the API as the demo
personas (34 scenario modules, ~4,000 writes). HR-owned defects from the same exercise are in
`docs/HR/programme/HR-FINISH-PLAN.md` § Lane 9; only the ones that belong to another module or to the platform
are here.

### What is broken

1. **Identity / platform — the admin-tier + employee-link contradiction.** Every `HR.*.Admin`
   approval door (`training-budgets/{id}/approve`, `training-plans/{id}/approve`,
   `staff-travel/policies/{id}/approve`, `probations/{id}/extend|confirm|terminate`,
   `talent-reviews/{id}/finalize`, `position-vacancies/reconcile`) is granted only to
   SuperAdmin/TenantAdmin by `RolePermissions`, and each handler also requires
   `CurrentUser.EmployeeId`. The only account in those roles (`admin`) has `Users.EmployeeId = NULL`,
   and the HR role holds none of the 22 `HR.*.Admin` permissions. Result: **nobody can act** — a
   travel policy is never in force (so `StaffTravelPolicyGuard` resolves `TravelPolicyCaps.None`
   and no booking cap is ever enforced), no probation can be extended or confirmed, no talent
   review finalised. Four independent area agents hit this wall on four modules.
2. **Payroll — `POST /hr/payroll/setup/grades` returns a bare 500 (`Payroll request failed.`)**
   when `gradeType` exceeds 15 characters (`PayrollGrades.GradeType` is `nvarchar(15)`); the
   truncation surfaces as an opaque 500 instead of a 400 naming the field. Same shape expected on
   every `nvarchar(5)`/`nvarchar(15)` column of that DTO.
3. **Finance workflow — HR asset transfers route to `Asset Transfer Approval`**, whose steps are
   Accounts Officer → Finance Manager → Financial Controller. Those users have no `EmployeeId`,
   which `AssetTransferService.ApproveAsync` requires, so `POST /Assets/transfers/{id}/approve`
   answers 403 for every login. An HR transfer between holders stops at Submitted for ever.
4. **Fleet / Maintenance coupling — `GroundTransportType.CompanyVehicle` requires a
   `VehicleAssetId` resolved against `MaintenanceAssets`.** HR's pool vehicles live in
   `CompanyAssets`, so a travel leg can never name the Corporation's own Hilux.
5. **Identity — `POST /api/User` without `tenantId` returns a bare 500** (`An error occurred while
   creating the user`); with `tenantId` it is 200. Should be a 400 naming the field.
6. **Auth — the login throttle surfaces as a 500** (`An error occurred during login`) for a user
   whose per-user window is saturated; a 429 follows only once the burst clears. `ApiPolicy` is
   100 requests/minute per caller and the Staging login limit is 10/minute per IP.
7. **Platform — cold-start reads of 35–55 s** on the first call of a shape after the API starts
   (`GET /api/job-candidates/all` 37.8 s for 6 rows; a candidate's qualifications 54.6 s). Later
   calls of the same shape are 17–50 ms. EF query compilation against a very large model.

### What was proven

Each item was reproduced at least twice against the current build on 2026-09-04 with the exact
payloads recorded in the area agents' reports (kept with the harness under
`dev-harness/hr-demo-smoke/out/`). Item 1 was reached independently from Training, Travel,
Probation and Succession.

### What it blocks

Item 1 blocks five runbook steps and every "approve" screen at Admin tier; the demo works around
it by seeding the affected tables directly (see `HrDemoSeedOrchestrator`'s second-pass steps).
Item 3 makes Book 2 §4 step 3 ("transfer between holders, approved") unreachable. Item 7 will make
the first click on Candidates look hung on stage — Book 0 tells the presenter to pre-open screens.

### What a fix needs

Item 1 is one decision, not seven fixes: either link a TenantAdmin login to an employee record, or
grant the `HR` role the `HR.*.Admin` tier for the doors HR actually operates. Items 2 and 5 are
validation before the write. Item 3 needs either an HR-side approval definition for HR asset
transfers or employee links on the Finance approvers. Item 4 needs a vehicle lookup that accepts
either register. Item 6 is a status-code mapping. Item 7 is a warm-up on startup or compiled
queries for the heaviest list shapes.


## 26. Procurement's supplier controller answers 400 to every caller — it cannot be constructed (2026-09-10, cause found 2026-09-14)

**Owner:** Procurement. **Severity:** the ENTIRE `/api/Suppliers` surface is dead — reads and
writes alike. **Status:** ⚠ the two missing registrations were added on 2026-09-14 (see "Cause,
found 2026-09-14"); the owning team should confirm that is the whole fix.

### What is broken

`GET /api/Suppliers?page=1&pageSize=3` (`SuppliersController.GetSuppliers`, `[Authorize]` only)
returns **400** `{"code":"INVALID_OPERATION","detail":"The operation is not valid for the current
state of the object."}` for the SuperAdmin `admin` login and for an HR user alike. The controller
delegates to `ISupplierRepository.GetSuppliersAsync(page, pageSize, search, status, supplierType,
isActive, isPreferred)`; the `InvalidOperationException` comes from inside that call and is
answered by the global middleware as a 400, so the endpoint cannot list suppliers at all.

### What was proven

Reproduced 2026-09-10 twice (admin, then a freshly minted HR user), API in Staging on the current
`hrdev` build; both variants of the query string (`pageSize` only, `page` + `pageSize`) fail the
same way. `dev-harness/hr-jobarch/run-r7.mjs` asserts the door is *not* usable so the day it is
fixed shows up as a failed assertion rather than a silent change.

### What it blocks

Any screen that needs a supplier picker through Procurement's own endpoint. HR's recruitment
costs (round 2b, R7) name a Procurement `Supplier` as the payee and read the master through a new
narrow projection, `GET api/hr/suppliers` (`HrSuppliersController`, over `ISupplierRepository`'s
queryable, tenant-filtered, id/code/name/active only) — the same shape and reason as
`HrCurrenciesController`. That door stays even after this is fixed.

### What a fix needs

Look at `SupplierRepository.GetSuppliersAsync`'s paging/ordering path for the invalid-operation
throw (a `Skip`/`Take` without an `OrderBy`, or a `Single` over several rows, are the usual
shapes). Nothing on the HR side needs to change when it is fixed.

## 27. Payroll — component exceptions: a removed row can never be re-added, and the bulk write has no role gate (2026-09-14)

**Owner:** Payroll. **Severity:** (b) blocks re-adding an allowance/deduction to a person once it
was removed — a 500 with no message the user can act on; (a) is an authorisation gap. HR is not
blocked: its Salary-tab card switches a row OFF instead of removing it, and reads through its own
door. **Status:** open. Found by round 3, lane X (`dev-harness/hr-payroll-membership/run-x.mjs`
B5/B6 diagnostics and `probe-component-exceptions.mjs`).

### What is broken

(a) `POST /api/hr/payroll/component-exceptions/bulk` (`PayrollController`, class-level `[Authorize]`
only) accepts any authenticated user: a plain-Employee login writes payroll exceptions and gets 200.
Every other payroll write on the controller shares the gate.

(b) `PayrollService.SaveEmployeeComponentExceptionsAsync` handles `isSelected:false` with
`_context.PayrollEmployeeComponents.Remove(entry)`, which the context's soft-delete turns into
`IsDeleted = 1`. The table's unique index
`IX_PayrollEmployeeComponents_TenantId_EmployeeProfileId_PayrollComponentId` is **not filtered on
`IsDeleted`**, so the next save that re-adds the same person + component inserts a second row and
SQL Server refuses it: `DbUpdateException → 500`.

### What was proven

Probe + harness on the dev tenant, 2026-09-14: save A and B on one component → save A alone → B
survives (per-employee-safe: the endpoint upserts only the lines it is sent) → deselect A → A's row
is soft-deleted (`IsDeleted = 1` rows visible in the table, index `has_filter = 0`) → re-add A →
**500** duplicate key. A plain-Employee token's bulk POST → **200**.

### What it blocks

Payroll's own allowances/deductions screen: removing a person from a component and later adding
them back is impossible until the soft-deleted row is purged by hand. HR's card avoids the path.

### What a fix needs

(b) Either filter the unique index on `[IsDeleted] = 0` (the relationship-type lesson) or have the
save reactivate the soft-deleted row instead of inserting. (a) Gate the payroll write routes on a
payroll permission policy, as HR's compensation routes are. HR's card needs no change for either.

## 28. Procurement's supplier CREATE is a silent no-op — 2xx, empty body, no row (2026-09-14)

**Owner:** Procurement. **Severity:** high — a caller cannot tell the write failed. **Status:** open.

### What is broken

`POST /api/Suppliers` with a complete, valid `CreateSupplierDto` returns **2xx with an empty
response body** and **writes nothing**. No error, no validation message, no id. A client that
follows the HTTP contract concludes the supplier was created and carries on against a row that does
not exist.

This is distinct from item 26 (the controller could not be constructed at all). Item 26 is fixed;
reads work now. **The write still does not.**

### What was proven

Reproduced 2026-09-14 against the current `hrdev` build, API in Staging on `ErpSystemDB_UAT`, as the
SuperAdmin `admin` login:

```
POST /api/Suppliers   { supplierCode: 'SUP-MED-001', name: '...', supplierType: 'Service Provider', ... }
  -> 2xx, body: ""
SELECT COUNT(*) FROM Suppliers   -> unchanged (3, the finance seeder's)
GET  /api/hr/suppliers           -> unchanged (the same 3)
```

`GET /api/Suppliers?page=1&pageSize=5` returns the three existing suppliers correctly, so the
controller, the repository read path and the tenant filter are all sound. Only the create is silent.

### What it blocks

HR's pre-employment check providers (round 3 lane G): `PreEmploymentCheckProviderServices` maps a
Procurement supplier to the checks it performs, and the demo needs three providers that do not exist
in the seeded supplier list. Because the door cannot create them,
`TdcDemoCheckProviderLinkSeeder.EnsureProvidersAsync` writes the three `Supplier` rows and their
mappings directly — flagged in that method, and to be **deleted in favour of the doors** once this
is fixed. Scenario 050 §21 already carries the restoration note.

### What a fix needs

Look at `SuppliersController.CreateSupplier` after the `GuardDirectMutationAsync` call: a missing
`SaveChanges`/`CommitAsync` on the unit of work, or an early return that skips the add, would both
produce exactly this. The empty body suggests the action is returning a result whose payload was
never populated. The mapping endpoint it blocks,
`POST /api/pre-employment-checks/providers`, is HR's and works.

## 29. Maintenance — its technician list decides "technician" by a unit's NAME, and reads a workload nothing writes (2026-09-23)

**Owner:** Maintenance. **Severity:** (a) the technicians screen disagrees with Maintenance's own
assignment gates, listing people the gates refuse; (b) every technician reads "available, 0%
loaded"; (c)–(g) dead or misleading code. **Status:** open — the hand-off from HR round 4, lane O.
**HR's side is done:** HR now maintains `Employee.CanBeAssignedToMaintenance` — the column
Maintenance's gates already read — from the position's **Technician role** flag, with a per-person
exception, and serves it at `GET api/hr/employees/technicians*`.

### What is broken

(a) **Two predicates for one question.** `TechnicianRepository` decides who is a technician by
`e.OrganizationUnit.Name.Contains("Maintenance")`. That covers `GetTechniciansAsync`,
`GetActiveTechniciansAsync`, `GetByDepartmentAsync`, `GetBySpecializationAsync`,
`GetByEmployeeIdAsync` and four more, nine sites in all. `TechnicianService` builds the
`/maintenance/technicians` screen from them. Maintenance's own gates read the COLUMN instead:
- `WorkOrderService` (assign);
- `WorkOrderLaborService` (log labour);
- `MaintenanceStaffScheduleService` (create and update);
- `QualityControlService.GetQualifiedInspectionOfficersAsync`.

So the screen lists people the gates refuse, and misses the technicians the gates accept: anyone in
a technician post outside a unit with that name.

(b) **Availability from a column nothing writes.** `TechnicianService.MapToDto(Employee)` computes
`IsAvailable = IsActive && CurrentWorkload < MaxWorkload`, and reports both figures from `Employee`.
`CurrentWorkload` is written by nothing, so it is always 0. Every technician therefore reads
available and 0% utilised, on that screen and in its utilisation figures.
`TechnicianSchedulingService` computes real utilisation from schedules, but the screen does not use it.

(c) **A sync that does nothing and reports success.** `TechnicianRepository.GetFromHRModuleAsync` is
`// TODO: Implement actual HR module integration`, returning an empty list. So
`SyncTechniciansFromHRAsync`, `GetTechnicianFromHRAsync` and
`POST api/maintenance/technical-skills/sync-from-hr` do nothing, and `GetAllTechniciansAsync` calls
the no-op sync on every read. There is nothing to sync: HR's record is read live.

(d) **Unreachable writes into HR's record.** `TechnicianService.CreateTechnicianAsync` and
`UpdateTechnicianAsync` write HR's `Employee` columns, and no controller calls them. The columns are
`CanBeAssignedToMaintenance`, `Specialization`, `CertificationLevel`, `ExperienceLevel`,
`MaxWorkload`, `Notes` and the names. Create also gates on the unit-name rule. If they were wired up,
HR would keep the column write as a **by-hand inclusion**: it sticks, and shows on HR's employee form
as set by hand. HR does not undo it on the next save.

(e) **Names for past records go through the wrong door.** `MaintenanceScheduleService` resolves an
assigned technician's NAME through HR's technician door (`IEmployeeService.GetTechnicianByIdAsync`),
which answers only for CURRENT technicians. Now that the answer follows the post, someone who moves
out of a technician post will read "Unknown Technician" on their past schedules. UAT holds no
schedules today.

(f) **The name is split by hand.** `TechnicianSchedulingService` splits HR's `FullName` on the first
space to get a first and last name. The door now carries `FirstName` and `LastName`.

(g) **A table with no reader.** The `Technicians` table (entity `Maintenance/Technician`) is written
by two seeders only and read by no service. `ITechnicianRepository` is `IGenericRepository<Employee>`.

### What was proven

Round 4 lane O's suite (`dev-harness/hr-jobarch/run-round4-o.mjs`, 101 ×2, UAT, 2026-09-23) set up
an employee in a fixture post that is NOT a technician role, inside the "Building Maintenance" unit:
- HR's door leaves them out, and the stored column stays false;
- `GET api/maintenance/technicians` still lists them, by unit name. The suite prints this as an
  observation and does not assert it, because it is Maintenance's to fix.

Measured on UAT before the lane:

| Measure | Count |
|---|---|
| employees with the column set | 0 of 2,089 |
| employees in a `MAINT` department | 0 |
| employees in a unit named like "Maintenance" | 1 |
| work orders | 0 |
| `Technicians` rows | 0 |
| `LastSyncDate` written | 0 |

### What it blocks

The Maintenance technicians screen cannot show the pool HR maintains, and its availability and
utilisation figures say "free".

Maintenance's shipped dropdowns are **not** blocked. The work-order, emergency, scheduled and
job-card screens call `maintenanceDataService.getTechnicians()`, which lane O repointed at HR's door.

⚠ **HR touched two Maintenance frontend files, and says so here.** The plan's O3 changed them
because, until they were moved, the flag changed nothing a user saw:
- `maintenanceDataService.getTechnicians()` now calls `/hr/employees/technicians`. It used the root
  `/employees/maintenance-available`, which is retired and returned each person's full record.
- `maintenanceApiService.getTechnicians()` is removed. It had no caller, and it fell back to
  `/employees?pageSize=1000`, offering every employee in the tenant as a technician.

### What a fix needs

- (a) Replace the unit-name predicate in `TechnicianRepository` with `CanBeAssignedToMaintenance`,
  the gates' own, or read HR's door.
- (b) Take availability and utilisation from `TechnicianSchedulingService`, and stop reading
  `Employee.CurrentWorkload`.
- (c) Delete the sync.
- (d) Delete `Create/UpdateTechnicianAsync`, or have HR's employee form be the only writer of those
  columns.
- (e) Resolve historical names through `IEmployeeService.GetEmployeeSummaryByIdAsync`.
- (f) Read the door's `FirstName` and `LastName`.
- (g) Retire the `Technicians` table.

None of these needs an HR change.

## 30. Global search — gating `POST api/hr/employees/paged` refused every name picker outside the HR desk (2026-09-27)

**Owner:** global search (master `b29cf068f`, "Add global search and restore partner account and
receipt controls"), plus **Payroll** for (b). **Severity:** (a) fixed on HR's side; (b) open.
**Found:** hrdev ← master merge #11.

### What is broken

Global search's employee record source reads `POST api/hr/employees/paged`, and the same commit put
`[Authorize(Policy = HrPermissions.EmployeeReadPolicy)]` on that endpoint. For a full `EmployeeDto`
read that is correct. But the endpoint was also the back end of every lean employee search, and the
permission catalogue promised that search stays open: `HrPermissions.ViewEmployees` reads *"the lean
directory reads (the shared name picker, org lookups) stay open to internal staff and do not need
this."*

- (a) HR's shared `EmployeePicker` (imported by 83 files) searched through it. Only the HR desk bundle
  (`HrStaffGrants`) holds `HR.Employee.Read`; `SafetyOfficerGrants`, `SheManagerGrants` and every
  line-manager login do not, so their pickers were refused.
- (b) `payrollService.searchEmployees` (`frontend/src/services/payrollService.ts`) still posts to the
  same endpoint, so a payroll user without `HR.Employee.Read` gets 403 from payroll's employee search.

### What was proven

Master's gate is the only change to `EmployeesController` in the range; the endpoint was ungated
at the merge base (`21ff99f3`), and all 10 `hr/safety` picker screens predate the gate.

### What it blocks

(b): payroll's employee search, for any payroll role without HR's employee-read permission.

### What a fix needs

- (a) **Done in HR.** The picker now searches `GET api/employee-portal/directory/lookup`: the
  directory's lean card (name, number, post, unit), any internal user, no employee link required, at
  least two characters, at most 25 rows. Master's gate on `/paged` is untouched.
- (b) Payroll: move `searchEmployees` to `directory/lookup`, or grant payroll roles
  `HR.Employee.Read` if payroll genuinely needs the full record. No HR change needed either way.

## 31. Platform — a `rebuild-db` database now lacks 500 guard triggers, and cannot migrate past master's QS guards (2026-09-27)

**Owner:** Finance (the disposable baseline) and the module owners whose guards live only in
migration SQL — Quantity Survey, Procurement, Inventory, Finance. **Severity:** a model-built database
silently runs without the platform's business-rule guards, and cannot be brought forward.
**Supersedes the scale, not the substance, of § 21.**

### What is broken

§ 21 found a few check constraints and triggers that exist only in migration SQL. Since master's
disposable baseline (`20260916132000`) the migration chain builds a complete database from empty,
and the guards have multiplied. Two scratch databases built from the same assembly at merge #11 —
one by `apply-migrations` on an empty database, one by `rebuild-db` — match exactly in tables (1,703),
columns, primary, unique and check constraints (848) and foreign keys (6,252), but:

| Object | Chain-built | `rebuild-db` |
| --- | --- | --- |
| Triggers | **500** | **0** |
| SQL scalar functions | 7 | 0 |
| Views | 1 | 0 |
| Default constraints | 185 | 120 |

### What was proven

`ErpSystemDB_UAT` (built by `rebuild-db` on 2026-09-20) applied 6 of master's 48 new migrations and
stopped at `20260920121000_AllowInternalQuantitySurveyValuations`:

```
The existing QS valuation guard is required before internal valuation alignment.
```

That migration, `20260920124000`, `20260920154000` and `20260925190000` read an existing trigger's
text and patch it (`REPLACE(@guard, N'CREATE TRIGGER', N'ALTER TRIGGER')` plus clause edits), so the
guard must exist at exactly the version the patch expects. Installing today's final-state trigger
would not satisfy them either.

### What it blocks

Every developer database built with `rebuild-db`, which until this merge was the documented reset in
HR's docs and `scripts/New-UatDatabase.ps1`. On such a database the API's startup `MigrateAsync`
throws, and even before that, the database enforced none of the 500 guards.

### What a fix needs

- Treat `rebuild-db` as scratch-only, or make it build through the chain (drop, create empty,
  `MigrateAsync`, seed) now that the chain can.
- **HR's side is done:** `New-UatDatabase.ps1` now drops, creates empty, runs `apply-migrations`
  then `seed-db`. Verified: the whole chain applies from empty in about 1.8 min (89 of 89).

## 32. Finance / Procurement / Inventory — nine migration-created unique indexes carry filters the model does not declare (2026-09-27)

**Owner:** Finance (Business Partner roles and profiles, primary-book designations, vendor-invoice
receipt allocations), Procurement / Inventory (landed costs, issue-voucher receipt lines).
**Severity:** low — a schema drift between chain-built and model-built databases; no correctness
change. **Found:** hrdev ← master merge #11, schema comparison of the two scratch databases in § 31.

### What is broken

These unique indexes are created by raw SQL with a filter, while the EF model declares them
unfiltered, so a chain-built database and a model-built one differ:

- `IX_AccountingBookPrimaryDesignations_TenantId_EffectiveFrom`
  (`20260920030047_AddGovernedPrimaryBookReplacementAuthority`)
- `IX_BusinessPartnerRoles_TenantId_BusinessPartnerId_RoleType`, the AP/AR profile-version and AP
  WHT-default indexes (`20260924032011_CanonicalBusinessPartnerFinanceProfiles`)
- `IX_LandedCostReceiptWeights_…`, `IX_LandedCostSupplierDocuments_…`
  (`20260924210000`, `20260924220000`)
- `IX_VendorInvoiceReceiptAllocations_TenantId_VendorInvoiceLineItemId`
  (`20260924230000_ProcurementAutoInvoiceReceipts`)
- `IX_InventoryIssueVoucherReceiptLines_…` (`20260927021852_InventoryIssueActualReceipts`)

Every filter reads `[col] IS NOT NULL AND …`, over key columns that are **all NOT NULL** (24 of 24),
so the filter excludes nothing.

### What it blocks

Nothing functionally. A filtered index cannot serve a parameterised lookup whose predicate the
optimiser cannot prove matches the filter, so these unique indexes may be skipped for exactly the
lookups they were built for.

### What a fix needs

Drop the redundant filters in a follow-up migration, or declare `HasFilter` on the model if they are
intentional. `has-pending-model-changes` will not surface this: the snapshot follows the model,
not the SQL.

## 33. Platform — the 90-day notification clean-up runs every 30 seconds, as a bulk UPDATE over the whole table (2026-09-30)

**Owner:** Platform (notifications — `NotificationDispatcherBackgroundService`,
`UnifiedNotificationService.CleanupExpiredNotificationsAsync`).
**Severity:** medium — no data is wrong, but it takes locks and query memory that every other
request competes for. **Found:** HR performance closure, slice E-d1's regression on UAT.

### What is broken

`ArchiveExpiredNotificationsAsync` runs on every dispatch cycle — `Notifications:DispatchIntervalSeconds`,
30 seconds by default — and ends by calling `CleanupExpiredNotificationsAsync`, which soft-deletes
notifications older than **90 days** with one `ExecuteUpdateAsync` over `Notifications`. A 90-day
rule needs a daily run, not one every 30 seconds.

### What was proven

- The API's log shows the clean-up **53 times in one hour**, each *"Deleted 0 expired notifications"*:
  UAT holds 151,116 notifications, none older than 90 days, so every run scans the table to change
  nothing.
- A read-only monitor of `sys.dm_exec_requests` during a harness run (2 s samples, 18:04–18:30):
  the clean-up's `UPDATE` **blocked notification inserts** (`LCK_M_IX`, up to several seconds, 23
  samples), and **waited in SQL Server's memory-grant queue itself** (`RESOURCE_SEMAPHORE`, 31
  samples); it failed once. Every request that raises a notification waits behind it.
- In the same window, heavy reads elsewhere waited for memory grants too, and HR's manager
  evaluation form timed out at 30 s five times — its own defect (HR's, recorded in the performance
  closure plan's § 5), made likelier by a table-wide `UPDATE` every half minute.

- **Seen again at slice E-d2a (2026-09-30, the same monitor over a 23-minute regression):** the
  clean-up had nothing to scan by then, but the dispatcher's own claim query (`SELECT TOP(@n) … WHERE
  … @maxRetryAttempts …`, running in parallel — `CXSYNC_PORT`) held a **range lock that blocked a
  notification insert for about 3 s** (`LCK_M_RIn_NL`); an HR template's submit-for-approval took 10 s
  in that window. It was the only blocking the monitor saw. UAT's unsent notifications are claimed
  200 at a time every 31 s, each failing at once for want of SMTP settings (8,110 failures in the
  run's 34-minute log).
- **Once the harness's rate-limit waits were lifted (the same evening), it was the slowest thing left:**
  every workflow submit that raises notifications took 11–13 s — five template submits and a PIP
  submit, 6 of 6, in a regression whose other calls answered in well under a second. The monitor had
  caught one such insert waiting on the claim query's range lock for about 10 s (`LCK_M_RIn_NL`).

### What it blocks

Nothing outright; it degrades every request that writes a notification, and adds to the memory
pressure that times out large reads.

### What a fix needs

Run the clean-up on its own daily schedule (or at most hourly), not inside the 30-second dispatch
loop; and index `Notifications (IsDeleted, CreatedAt)` so the `WHERE CreatedAt < @cutoff` finds its
rows without a scan. The claim query should not hold range locks over the table the rest of the
application inserts into (read committed with a claim `UPDATE … OUTPUT`, or `READPAST`).

## 34. Workflow designer — an approval stage's "person named by the record" approvers are invisible, and saving the route deletes them (2026-10-02)

**Owner:** Platform (workflow — `frontend/src/components/workflow/WorkflowDesigner.tsx`).
**Severity:** high for any route that relies on them: the route works until someone edits it in the
designer, and is then silently rewired. Today that is one route, **Staff Travel Approval**, since HR's
travel closure lane 2 (2026-10-02). **Found:** HR, checking whether the travel route would survive an
administrator's edit. **Evidence:** read in source on 2026-10-02; not yet reproduced in the browser
(steps below).

> **Status: the minimum fix is in — made by HR on 2026-10-02 (travel closure slice 2b), after the issue was
> raised with the workflow developer.** The designer now keeps, shows and saves back the approver rules it does not edit,
> and keeps the step's required role as their fallback. See *What HR changed* below. The fuller fix — an
> approval stage *assigned* to a person from the record in the designer — stays with the workflow owner.

### What the engine supports

An approval stage's approver rules (`approvalConfig.approverRules`) can be `Role`, `User`, `Dynamic`,
`RequestorManager` or `PreviousStepUser` (`WorkflowAssignmentType`). `WorkflowEngine.ResolveApproversAsync`
(`WorkflowEngine.cs:1561`) resolves each; a `Dynamic` rule reads a user id from the record's workflow
context under its `dynamicExpression` (l.1593), and when no rule resolves the engine falls back to the
step's `RequiredRole` (l.1455). The context is what the entity's block in
`SimpleWorkflowService.BuildEntityContextAsync` puts there.

Staff travel uses this: the *Line manager approval* stage carries two `Dynamic` rules
(`lineApproverUserId`, `lineApproverUserId2`), which the travel context fills with the logins of the
traveller's two nearest line authorities (`SimpleWorkflowService.cs:1932-1938`), and `RequiredRole = HR`
as the fallback. So the task, the *Approval Required* notification and the inbox row go to the
traveller's own supervisor or head of unit — not to every holder of the Manager role — and to HR only
when nobody in the traveller's line can sign in.

### What is broken

The designer understands only `Role` and `User` approver rules on an approval stage:

- **Loading** (`WorkflowDesigner.tsx:1433-1446`) keeps the `Role` and `User` rules and drops every other
  kind; when no `Role` rule is left, it shows the step's `RequiredRole` as the stage's approver role. The
  travel stage therefore shows **"HR"** as its approver — the fallback, not who is actually asked.
- **Saving** (from l.1623; `requiredRole` at l.1250) rebuilds `approverRules` from the roles and users it
  showed. The `Dynamic` rules are gone, and "HR" is written back as an explicit `Role` rule.
- Nothing warns, on load or on save.

Task steps are not affected — they already offer *Dynamic User From Context* (l.2866). The gap is approval
stages only.

### What happens when someone edits the travel route (steps to reproduce)

1. Workflow designer → *Staff Travel Approval* → new version → change nothing, or add a stage → publish.
2. Read the new version's *Line manager approval* step: `approverRules` is `[Role: HR]`; the two `Dynamic`
   rules are gone.
3. Submit a travel request: every HR officer gets the first-stage task and notification; the traveller's
   line manager gets nothing and can no longer decide it. HR's travel service then lets the travel desk
   decide the stage (its rule for "no line manager can"), so nothing errors — the route has quietly become
   HR-only.

### What a fix needs

**Minimum (enough for travel):** on load, keep every approver rule the designer does not edit, unchanged,
in the node's data; on save, write those rules back after the role and user rules; show them read-only on
the stage (for example *Named by the record: lineApproverUserId*); and do not present `RequiredRole` as an
approver role when the stage's rules are of another kind. A round-trip test — load a stage with a `Dynamic`
rule, save it, get the same rules back — and a check that `Role`/`User`-only routes save exactly as before.

**Fuller:** let an approval stage be assigned *Person from the record (context field)*, as a task step
already can, listing the context fields the entity's `BuildEntityContextAsync` block provides.

**HR's proposal** (as first recorded): HR makes the minimum change in its next travel slice (2b), with the
round-trip test, unless the workflow owner prefers to make it. Until then, do not publish a new version of
*Staff Travel Approval* from the designer. — *Done by HR; see below.*

### What HR changed (2026-10-02, travel closure slice 2b)

The minimum fix, and nothing else in the designer or the engine:

- **New `frontend/src/components/workflow/WorkflowDesigner.approvers.ts`** — the approval stage's approver
  mapping, moved out of the component into pure functions so it can be tested. It is part of the
  designer — named for it, and the designer's import and its load and save points say where it is:
  - `readApprovalStageApprovers(rules, requiredRole)` — **load.** Roles and users go to the designer's
    editors as before. Every other kind of rule is kept **exactly as stored** (`preservedApproverRules`).
    When there are such rules, the step's required role is kept as their fallback
    (`fallbackRequiredRole`) and is **no longer shown as an approver role**. A stage of roles and users
    loads exactly as before — including the old behaviour of showing the required role when no role
    rule is left.
  - `buildApprovalStageRules(...)` — **save.** Roles and users are built exactly as the designer always
    built them (same groups and priorities); the preserved rules follow, unchanged (their kind written
    as the enum number, as the designer writes its own).
  - `stageRequiredRole(...)` — a stage with preserved rules keeps the required role it was stored with;
    any other stage takes its first role, as before.
  - `approverSlotCount(...)` — the designer's "at least one approver" and minimum-approvals checks count
    preserved approvers (travel's line-manager stage has no role or user, and would otherwise fail
    validation and could not be saved at all).
  - `describePreservedApprover(rule)` — the words the designer shows for one.
- **`WorkflowDesigner.tsx`** — `buildNodeDataFromStep` reads the stage through the helper and keeps
  `preservedApproverRules` and `fallbackRequiredRole` on the node; `buildStepConfiguration` saves through
  `buildApprovalStageRules`; the step's `requiredRole` comes from `stageRequiredRole`; validation counts
  preserved approvers. The stage's properties panel shows them read-only under **Named by the record**
  ("Person named by the record (lineApproverUserId)"), with "When none of them can be found, the HR role
  is asked instead". **`nodes/ApprovalNode.tsx`** counts them on the canvas card.
- **Proof:** `WorkflowDesigner.approvers.test.ts`, 8 tests — the travel stage **exactly as
  `GET api/Workflow/definitions/{id}` returned it on UAT** round-trips unchanged, twice, with HR kept as
  its fallback; a stage of roles and users saves **exactly as the code before the fix did** (that code is
  copied into the test as the reference, for parallel and sequential stages); the old seed shape (no
  rules, a required role only) is unchanged; a mixed stage keeps both kinds; the kind is read whether the
  API sends its name or its number. All 47 workflow component tests pass; type-check and lint clean.
  Not yet walked in the browser.

**What it means now:** the *Staff Travel Approval* route can be opened, edited (a stage added, for
instance) and republished in the designer; its *Line manager approval* stage keeps its two named-approver
rules and its HR fallback.

**Still the workflow owner's:** letting an author *assign* an approval stage to a person from the record
in the designer (task steps already offer *Dynamic User From Context*), and #3 below.

### Related

- **#3 — conditional routing does not route** (still open): a stage cannot yet depend on the record, e.g.
  "the Managing Director only above GHS 50,000".
- **`RequestorManager` never resolves.** The engine reads `requestorManagerId` from the context
  (`WorkflowEngine.cs:1600`, `ProcedureCaseService.cs:6848`), and no entity's context supplies it, so a
  stage assigned to "the requestor's manager" has no approver. HR built its own line-manager lookup for
  travel for this reason (`HrLineAuthority`).

## 35. Finance — on a database seeded with the v2 accounting books, the V1 book resolver names a book that does not exist, so no HR (or Procurement) posting can land (2026-10-02)

**Owner:** Finance (`ErpSystem.Core/Finance/Integration/FinanceAccountingBookCodeResolver.cs`, the V1 compatibility
boundary of `docs/Finance/ACCOUNTING_BOOK_V2_PRODUCER_MIGRATION.md`), with the HR, Payroll and Procurement owners for
the V2 cut-over that document schedules. **Severity:** high the day any producer's posting rule is switched on — with
HR's strict adapter the HR action itself is refused, not merely left unposted. **Found:** HR's travel closure, lane 3
(D-17), proving travel's posted path on a scratch copy of UAT. **Evidence:** reproduced against the running API on that
copy, 2026-10-02.

### What is broken

`HrFinancePostingStore.GetTenantContextAsync` takes the book from Finance's own resolver,
`FinanceAccountingBookCodeResolver.ResolveLegacySingleBook(FinanceSettings.SubledgerPostingMode, …)`, which answers only
`IFRS`, `LOCAL_STATUTORY` or `MANAGEMENT` (and `IFRS` when there are no settings). UAT's `FinanceSettings` say
`SubledgerPostingMode = IFRS`. But UAT — seeded on Finance's book model v2 (`a5baaf88f`, `0e3b1580b`, 2026-09-21) —
has the books **`BASE`** (default, primary, active, posting), **`IFRS_ADJUSTMENTS`** and **`USD_PARALLEL`**, and no
`IFRS`. `FinancePostingEngine` (`FinancePostingEngine.cs:1330`) looks the book up by tenant and code, finds none,
records a `BOOK_UNAVAILABLE` denial and throws *"Accounting book is unavailable for this tenant."*

Reproduced: with travel's five rules switched on and every role mapped, disbursing an advance answered **422 —
"Finance did not accept 'Travel advance disbursed' for ADV-2026-00002: Accounting book is unavailable for this
tenant."**; the register row is `Failed`, and — the adapter being strict — the advance stayed Approved. A claim's
approval was refused the same way. Every HR event goes through the same store, so medical, benefits, awards, leave
encashment, separation and receivables are affected alike. Procurement's supplier-onboarding fee and tender fee
(`ProcurementSupplierOnboardingTokenService.cs:1535`, `TenderBidService.cs:2136`) call the same resolver.

HR's posting design (`HR-FINANCE-POSTING-DESIGN.md` § 5.1) and `dev-harness/hr-finance/prep-uat-finance-authority.sql`
were written when UAT had an `IFRS` book; the prep's `UPDATE … WHERE Code = 'IFRS'` now matches nothing. (UAT's three
books are already active and postable — that step of the prep is obsolete.)

### What it blocks

Switching on any HR posting rule on a database built today. UAT keeps no HR rule, so nothing fails there now — every
HR money event is recorded `Unposted`, as designed.

### What a fix needs

The V2 cut-over the migration note describes: producers submit a concrete `AccountingBookCode` that exists — for a v2
tenant, presumably its default primary book — rather than the V1 setting. Until then, either the resolver (or
`FinanceSettings`) must be able to name `BASE`, or a v2 seed must keep a book the resolver names. Not HR's to choose:
the book a subledger posts to is Finance's decision.

**How HR proved its own path meanwhile:** on the scratch copy only, the primary book `BASE` was renamed `IFRS`
(`l3c/d17/scratch-book.sql`); travel's posted path then ran 70/70 twice (`dev-harness/hr-travel/run-final-posting.mjs`)
and the copy was dropped.

## 36. Platform — the notification dispatcher writes back whole rows, undoing a soft delete made while its batch runs (2026-10-03)

**Owner:** Platform (notifications — `UnifiedNotificationService.ProcessPendingNotificationsAsync`, run by
`NotificationDispatcherBackgroundService`). **Severity:** low for users (a notification someone deleted can come back to
their list), but it makes any clean-up of `Notifications` unreliable while the dispatcher is working. **Found:** HR's
travel closure, lane 8, reading why notices of deleted harness trips were still live on UAT.

### What is broken

The dispatcher claims each due notification, loads it **tracked** (`_dbContext.Notifications.FirstOrDefaultAsync(n =>
n.Id == id && !n.IsDeleted)`), sends it, sets its status, and calls `Repository<Notification>().UpdateAsync(notification)`
— which marks every column modified — then saves the whole batch with one `SaveChangesAsync` at the end of the loop. A
row soft-deleted by anyone else between its load and that save is written back with `IsDeleted = 0` and `DeletedAt =
NULL`. The window is the batch's processing time, which on a database with no mail server is wide: each email fails
after several seconds (see #33 — 200 claimed every 31 s, each failing at once for want of SMTP settings).

### What was proven

On UAT, 2026-10-03: two trips of a harness run (TR-2026-03111 and -03112, policy suite run 343424) were soft-deleted at
02:09:14.867 with their notices; 45 of those notices were live again, each stamped `SentAt` between 02:09:14.967 and
02:09:15.0 — written by the dispatcher a tenth of a second after the delete. The other 21 notices of one trip, not in that
batch, stayed deleted. (The harness now deletes a run's notices again one dispatcher cycle later; the 45 were removed by
hand.)

### What it blocks

Nothing outright. Any module that removes notifications in bulk — a user clearing their list, a record's deletion
cascading to its notices, a test teardown — can be partly undone.

### What a fix needs

Save only what the dispatcher changed: set the status, attempt count and sent time on the tracked entity and let change
tracking write those columns (no `Update` on an already-tracked entity), or use one `ExecuteUpdateAsync … WHERE Id = @id
AND IsDeleted = 0` per outcome; and save per notification, not once per batch.

## 37. Payroll — no intake for a one-off amount owed to an employee, so a claim "paid through payroll" reaches nobody (2026-10-04)

**Owner:** Payroll. **Severity:** medium — a missing capability, not broken code; until it exists HR must not offer
payment through payroll. **Found:** HR's travel closure, finding O-6 (2026-10-01), decided as D-10 in lane 3; written up in
lane 9. **Full report:** `docs/HR/integration/handoffs/HANDOFF-PAYROLL-TRAVEL-CLAIMS.md`.

### What is broken

Two HR claim screens let an officer settle an approved claim through payroll — travel's **Payroll offset**
(`TravelPaymentMethod.PayrollOffset`) and medical's **Salary deduction** (`PaymentMethod.SalaryDeduction`). Both mark the
claim *Paid*; neither sends anything to payroll, because payroll has no intake for "pay this employee this one-off amount
in the next period". Bonus, back-pay and promotion arrears have their own policies; `PayrollEmployeeComponent` is a
standing per-employee setting; `PayrollImportBatch` reconciles legacy staff numbers. HR's Finance posting already leaves
the staff claims payable for payroll's journal to clear — the amount never reaches a payslip.

### What was proven

Read 2026-10-04: `TravelPaymentMethod.PayrollOffset` and `PaymentMethod.SalaryDeduction` have no reader outside HR's
posting factory, which posts nothing for them beyond the advance a travel claim set off. On UAT no claim has used either
method (the paid travel claims are bank transfers; one medical claim, by bank transfer).

### What it blocks

Paying staff claims through payroll. **Travel is protected:** since 2026-10-02 its pay dialog hides the option and the
API refuses it (D-10). **Medical is not:** its claim page still offers *Salary deduction* — HR will decide separately
whether to switch it off the same way.

### What a fix needs

First a decision from payroll (and TDC): should claims be paid through payroll at all? If not, nothing is built — travel's
refusal stays and medical's option is switched off. If so, a payroll intake for a one-off amount per employee, with a
reference back to the claim, a way to withdraw an item not yet paid, and the pay period that paid it reported back, so HR
marks the claim *Paid* only once it is. Currency and tax treatment are payroll's to rule. The hand-off lists exactly what
HR would send.

## 38. Fleet — a vehicle or driver can be double-booked, and a submitted fleet trip is approved by nobody (2026-10-04)

**Owner:** Fleet (`src/ErpSystem.Core/Services/Maintenance/Fleet/FleetTripService.cs`). **Severity:** medium — both
put trips and vehicles wrong on Fleet's own screens, whoever books them. **Found:** HR's travel closure, the Fleet review
(FX-2, FX-7, FX-9, 2026-10-01), handed off under D-12, D-27 and D-62. **Full report:**
`docs/HR/integration/handoffs/HANDOFF-FLEET-STAFF-TRAVEL.md`.

### What is broken

- **The clash check sees dispatched trips only, and no dates** (`EnsureNoDispatchedConflictAsync`, l.885-901). Planned
  trips for the same hours are all accepted. A vehicle that is out refuses every other trip for it, next month's too.
- **A driver is checked for a licence only** — not for approved leave, or another trip not yet dispatched.
- **No `FLEET_TRIP` approval route is seeded**, and `SubmitForApprovalAsync` (l.346) has no guard for a missing route.
  The engine then approves the submission at once, with nobody asked.

### What was proven

Read from the code, 2026-10-01 and again 2026-10-04 (the lines above).

Travel's own legs are protected on travel's side (`StaffTravelFleetService`):
- strict overlap against planned trips;
- compliance at drop-off;
- driver leave and other trips;
- submission only under a published route (D-27).

### What it blocks

Nothing in travel. In Fleet: double-booked vehicles and drivers, and fleet trips approved without an approver.

### What a fix needs

- Compare overlapping planned windows of live trips.
- Seed a route and refuse submission without one.

HR offers a read-only availability read (approved leave and staff travel) for the driver check. The hand-off also asks
for a Fleet-owned read of a trip's incidents: travel reads the `FleetIncidents` table directly today, because Fleet's
reads need `MaintenanceRead`.

## 39. Platform (identity) — the HR/Identity reconciliation sweep fails, every run, for anyone whose manager has two logins (2026-10-05)

**Owner:** Platform, identity (`src/ErpSystem.Api/Services/Identity/HrIdentityReconciliationService.cs`, run every 5
minutes by `HrIdentityReconciliationBackgroundService`; arrived with master's #31). **Severity:** low to medium. The
sweep goes on past a failure, but the person it fails on is never reconciled. Each failure is logged as an error,
which buries real errors in the API log. **Found:** HR's company-schedule final closure, lane 1d, reading the API log
after a harness run.

### What is broken

1. **One login per employee is assumed, and a second login breaks the people that employee manages.**
   `ReconcileCandidateAsync` looks up the login of the candidate's manager with `ResolveEligibleUserForEmployeeAsync`
   (l.1054). It reads the manager's active logins with `SingleOrDefaultAsync`. A manager with two active logins
   throws "Sequence contains more than one element", so each person reporting to them fails. The failure happens
   again in every run: nothing about the data changes between runs.
2. **A person removed while a run is under way is reported as a failure.** A run lists its candidates first (l.75–91:
   logins joined to employees not deleted). It then reconciles them one by one, re-reading each employee with
   `SingleAsync` (l.472). On UAT a run takes about five minutes for about 3,800 logins. An employee soft-deleted in
   that window throws "Sequence contains no elements". The person is then recorded as *Failed, eligible for retry*,
   when they are simply no longer a candidate.
3. **A run interrupted by a restart stays *Running* for ever.** The `catch` blocks only handle exceptions, so a run
   whose process stops never reaches a final state. The next 5-minute window starts a new run under a new key, so
   nothing is blocked. But the runs list keeps one "Running" row for each restart.

### What was proven

On UAT (`ErpSystemDB_UAT`), 2026-10-05, from `HrIdentityReconciliationItems` and `HrIdentityReconciliationRuns`.

**Item 1: 261 failures, all one login.** Every one is `property.manager` (employee TDC/00052, Danquah), from the
first run after the rebuild (2026-09-29 12:48) to the last (2026-10-05 01:15). Danquah's manager, Stephen Boateng
(TDC/00007), has two active logins: the demo personas `head.estate` and `authorised.signatory`. He is the only
employee on UAT with more than one active login.

**Item 2: 273 failures, one per login.** Every one is a harness login: 272 from the travel closure's suites (`e2e_tv_*`,
from 2026-10-03) and one from the company-schedule suite (`csv_finUK55S6H`, 2026-10-05 01:15:49). Each was caught
while its suite's teardown deleted the employee.

**Item 3: 57 of 268 runs never completed.** Each was left by an API stop: UAT's API is started and stopped around
each harness session.

### What it blocks

- **Reconciliation of anyone whose manager has a second login.** On UAT that is one person. In TDC's data it is
  every report of anyone given a second account, such as a persona or an admin login.
- **Reading the API log.** Every 5-minute run adds these errors beside the real ones.

### What a fix needs

- **Item 1:** decide whether an employee may have more than one active login.
  - If yes: resolve the manager's login by a stated rule, for example the most recently signed-in, or the one marked
    primary.
  - If no: refuse the second link where logins are linked to employees, and report the existing pairs for an
    administrator to resolve.
- **Item 2:** re-read with `SingleOrDefaultAsync`, and record a candidate whose employee or login has gone as
  *skipped* rather than *failed*.
- **Item 3:** on start, mark runs left *Running* by an earlier process as *Interrupted*, or give each run a lease
  that expires.

## 40. Platform (email) — since 2026-10-03 an email sent with nobody signed in finds no mail server: the careers activation email, the password reset and every background send (2026-10-05)

**Owner:** Platform, email settings (`src/ErpSystem.Core/Services/SettingsService.cs`, `GetEmailSettingsAsync`, as
changed by master `de8ad4fb2` "Fix public verification delivery and dashboard health routing", 2026-10-03; read by
`ProductionEmailService`). **Severity:** high.
- A candidate who registers on the careers site, on a server with no working SMS provider, can no longer activate the
  account: the lock-out round 4 closed is back.
- On a server with SMTP configured, every email HR sends from a background service fails.

**Found:** HR's company-schedule final closure, lane 2e-1, running `hr-templates/run-lane-n.mjs` (section J).

### What is broken

`GetEmailSettingsAsync` now takes the tenant from the signed-in user (`ICurrentUserService.TenantId`) and returns
nothing when there is none. Before `de8ad4fb2` it took the first settings row, "even for anonymous requests". With no
HTTP context, or no signed-in user, `CurrentUserService.TenantId` is null, so the lookup finds no mail server.

HR's senders name the tenant (`TemplatedEmailService.SendForTenantAsync(tenantId, …)`), but the tenant is used only to
choose the wording. The send goes through `IEmailService` → `ProductionEmailService.SendEmailAsync` → the lookup above.
So these sends fail:
- **the careers registration's activation email** (`AuthController.SendCandidateActivationEmailAsync`, anonymous) —
  proven below;
- **every send from a background service**: HR's hourly company-schedule reminders and RSVP chases, the other HR
  sweeps, and the notification dispatcher's emails — read from the code, not run;
- **the forgotten-password email** (`AuthController.ForgotPassword`, anonymous, through `IEmailService`) — read from
  the code, not run.

The same commit added `ITenantEmailSender` / `TenantEmailSender`, which takes the tenant explicitly. Only its own
public path (`EstateExternalDocumentsController`) uses it.

### What was proven

On UAT (`ErpSystemDB_UAT`), 2026-10-05, 11:00–11:01:
- `run-lane-n` configured a mail sink as the DEFAULT tenant's `EmailSettings` row and registered four careers
  candidates.
- Each registration logged "No email settings configured in database", then "Activation email could not be sent to
  candidate {id}".
- J2–J8 failed: 6 of the suite's 115. The suite scored 115/115 at round 4 (2026-09-23).
- Signed-in sends in the same window reached the sink: sections D–I, K and L passed.

### What it blocks

- **Careers self-registration** wherever SMS does not deliver. The activation email is the only other way to
  activate, as `AuthController`'s own comment on it explains.
- **Password reset** by email.
- **Every HR background email** on a server with SMTP: company-schedule reminders and chases, leave, travel and
  orientation sweeps. They report a failure and send nothing.

UAT shows no change, because it has no mail server at all.

### What a fix needs

- Send with the tenant the caller gives. For example, `TemplatedEmailService` could use `ITenantEmailSender` when a
  tenant is named, or `SendForTenantAsync` could set an ambient tenant that the settings lookup reads.
- An anonymous send that knows no tenant, such as the password reset, needs a stated rule: the seeded DEFAULT tenant,
  as `5d7e57f64` chose for the login page's public settings.
- HR's senders already name the tenant, so HR needs no change.

## 41. Platform (identity) — the careers sign-up texts a code to whatever number it is given, with no switch for test servers (2026-10-05)

**Owner:** Platform, identity (`AuthController.RegisterCandidate`, l.1912–1934; `TenantSmsSender`), with HR's own
harness. **Severity:** medium. This is a privacy and cost risk, not a broken function. **Found:** HR's
company-schedule final closure, lane 2e-1, reading the API log after `hr-templates/run-lane-n.mjs`.

### What is broken

Every careers registration sends a verification code by SMS to the request's phone number, which is required
(`RegisterRequest.PhoneNumber`). Nothing stops this on a test or staging server: there is no sandbox, no list of
allowed numbers, and no "log the code instead". So any automated registration texts the numbers it uses: a test
suite, a load test, or a demo script.

HR's `run-lane-n.mjs` registers with random real-format Ghana numbers: `+23320` and seven random digits (l.262).

### What was proven

On UAT, 2026-10-05, 11:00–11:01, the suite registered four candidates. For each, the log reads:
- "OTP created … channel=Sms";
- the Twilio fallback failed;
- "[SMS:mNotify] Sending to ***6527" (then ***6308, ***7832 and ***8225), and that attempt failed;
- "Failed to send phone verification OTP".

No phone was reached, only because UAT has no SMS credentials, either the tenant's own or the fallback's.

### What it blocks

Running any suite that registers candidates on a server where SMS credentials are configured.

### What a fix needs

- **Platform:** a guard for non-production servers. Either a sandbox mode that logs the code and sends nothing, or an
  allowlist of numbers outside production.
- **HR (owed):** until that guard exists, `run-lane-n.mjs` must not run where SMS credentials are configured. It
  should refuse to start there. The number cannot simply be left out, because the sign-up requires one.

## 42. Platform (identity) — `PUT /api/User` answers 500 for a login with no email, so such a login cannot be edited or switched off through the API (2026-10-05)

**Owner:** Platform, identity (`UserService.UpdateUserAsync` l.121, `UserController.UpdateUser`). **Severity:** low.
**Found:** HR's company-schedule final closure, lane 2e-1, switching off `run-lane-n`'s officer with no email after
the run.

### What is broken

Updating a login whose email is empty sends the empty email back, as the screen and the API echo what they read.
Identity's validation then refuses it ("Email '' is invalid"). The refusal is thrown as an exception and answered
**500**, not as a 400 or 422 that says why. Any change to such a login fails, including only switching it off.

### What was proven

On UAT, 2026-10-05, 11:05:28–11:05:40, three `PUT /api/User/5B3768C6-…` calls (login `r4n_hrn_028928`) answered 500.
The suite's own teardown made one call and HR's switch-off tool made two. Each logged "Failed to update user: Email ''
is invalid" at `UserService.UpdateUserAsync` l.121. The login was switched off by SQL instead.

The suite removes that login's email itself, by SQL, so it can test an officer with no address. A login can have no
email by other routes too, such as an import.

### What it blocks

Editing or switching off, through the API or the users screen, any login with no email.

### What a fix needs

- Treat an empty email as no email on update, if a login may have none; or require one everywhere, at creation and on
  import.
- Answer Identity's validation failures with a 400 that names the field.

## 43. Platform (email settings) — the SMTP password is written back to the database in plain text by any request that sends an email and then saves (2026-10-05)

**Owner:** Platform, email settings (`src/ErpSystem.Core/Services/SettingsService.cs`, `GetEmailSettingsAsync` l.61–94;
`src/ErpSystem.Api/Controllers/SettingsController.cs`, the email endpoints l.340–520). **Severity:** high — a
credential stored at rest in plain text, and shown in plain text. **Found:** HR's company-schedule final closure, lane
2e-2, reading the send path; then proved. Logged on the user's word (2026-10-05).

### What is broken

1. **Written back in plain text.** `GetEmailSettingsAsync` reads the tenant's `EmailSettings` row through the generic
   repository, which tracks it, then sets `SmtpPassword` to the decrypted value on that tracked entity. Its comment
   says "but don't modify the entity"; the code does. The mail sender (`ProductionEmailService`) calls it on every
   send, in the request's own unit of work. **Any `SaveChanges` later in the same request writes the decrypted
   password back to the table.** Nearly every module sends and then saves. For example, every company-schedule
   notice saves its in-app rows after its emails, and so do workflow notices and HR's reminders.
2. **Shown in plain text.** `GET /api/Settings/email` (TenantAdmin) answers `SmtpPassword` decrypted. Its own comment
   says "In production, don't return the password".
3. **Audited in plain text, permanently.** `POST` and `PUT /api/Settings/email` write an `AuditLogs` row:
   - `NewValues` is the request serialized, password included;
   - on an update, `OldValues` is the loaded entity, whose password is already decrypted.

   `AuditLogs` is append-only (trigger `TR_AuditLogs_AppendOnly`), so such a row cannot be removed afterwards.

### What was proven

On UAT (`ErpSystemDB_UAT`), 2026-10-05, with `dev-harness/hr-company-schedule/tools/probe-smtp-password-write.mjs`.
It uses a dummy password and a mail server on a closed port (127.0.0.1:2526), so no email went anywhere:
- The admin saved the settings through `POST /api/Settings/email`. The stored `SmtpPassword` was 64 characters of
  ciphertext, not the password.
- One `AuditLogs` row from that save carried the password in plain text.
- `GET /api/Settings/email` answered the password in plain text. That read saves nothing, and the stored value was
  unchanged after it.
- An HR officer then pressed "Send reminder now" on an event they organise: one email tried and refused, then one
  in-app notice saved.
- **Read again, the stored `SmtpPassword` was the password in plain text.**
- The settings row, the event, the notice, the login (switched off) and the employee were removed afterwards. The
  audit row could not be removed (append-only) and stays on UAT. It holds only the dummy password, for a server that
  does not exist.

Under #40 the background senders find no mail settings at all, so today only signed-in sends write the password
back. Fixing #40 without this would let every background sweep write it back too.

### What it blocks

Storing SMTP credentials safely. Anyone with read access to the database or a backup — or a TenantAdmin through the
screen — reads the mail account's password, after the first ordinary send on any server with mail configured.
Decryption keeps working, because the code falls back to "assume plain text" when decrypting fails, so nothing
visibly breaks.

### What a fix needs

- Return a detached copy from `GetEmailSettingsAsync`: read `AsNoTracking`, or decrypt into a new object. ⚠
  `UpdateEmailSettingsAsync` calls the same method and relies on the tracked row to save its update, so it needs its
  own tracked read.
- Re-encrypt any row already stored in plain text. One way: try to decrypt each row, and encrypt it if that fails.
- Never answer the password from `GET /api/Settings/email`; answer whether one is set.
- Leave the password out of the audit's `NewValues` and `OldValues`.

## How to use this file

Add an entry whenever HR work uncovers a defect in a module HR does not own. Keep the same shape:
what is broken, what was proven, what it blocks, what a fix needs. Resist fixing them inline — the
value here is that the owning team gets a reproducible report, not a surprise diff in their module.
