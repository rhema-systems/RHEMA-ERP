# Cross-Module Defects Found During HR Work — For Finalization

**Opened 2026-08-17.**

Defects found in **other teams' modules** while building HR. They are recorded rather than fixed,
because the module is not HR's to change — the same rule that governs payroll. Each entry states
what is broken, what was proven, what it blocks, and what a fix needs.

**Owner action required.** Nothing here is speculative: every item was reproduced against the
running API on the reference database, and where a fix was trialled the result is recorded.

---

## 1. Procurement — `SuppliersController` is entirely non-functional

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
4. Re-run `dev-harness/hr-travel/run-slice6.mjs`; it prints the effective rate and warns when it is
   implausible, so it will confirm the fix without needing an edit.

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

## How to use this file

Add an entry whenever HR work uncovers a defect in a module HR does not own. Keep the same shape:
what is broken, what was proven, what it blocks, what a fix needs. Resist fixing them inline — the
value here is that the owning team gets a reproducible report, not a surprise diff in their module.
