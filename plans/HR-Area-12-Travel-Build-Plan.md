# HR Area 12 — Staff Travel: Build Plan

**Created 2026-08-17.** Area 12 of 27. Tier B, third of eight.

---

## 1. How to use this document

Read §2 and §3 before touching anything. §3 is measured, not inferred — every claim in it came
from a live call against the running API on 2026-08-17, and the commands that produced it are in
`D:\Rhema\TDC ERPS\dev-harness\hr-travel\`.

§3 records the area **as found**, before slice 0. It is deliberately not rewritten as slices land —
the findings register (§6) and the slice log (§8) carry current state. Two entries in §3 have been
corrected in place where they were *wrong when written*, not merely superseded; both say so.

The single most important thing in this document **was** F-01: the central entity of the area
could not be created, read in detail, or updated, and never had been. ✅ Fixed in slice 0, which
is the first time the area ever executed.

---

## 2. Status at a glance

| | |
|---|---|
| Backend surface | **8 controllers, 220 endpoints, 34 entities, 33 DbSets** |
| Backend wiring | ✅ complete — 8/8 services and 33/33 repositories DI-registered |
| Reads | ⚠ 31/33 parameter-free GETs return 200; **30 of the 31 returned empty** pre-slice-0 |
| Writes | ✅ **alive** — slice 0; create→read→list→submit green, 54 assertions |
| Detail read | ✅ **alive** — `AsSplitQuery`, slice 0 |
| Gating | ✅ **`HR.Travel.*`** — slice 0; seed verified, HR is not an administrator |
| Tenant scoping | ⚠ repositories unscoped, **services compensate** — no leak; see §3.6 (corrected) |
| Approvals | ✅ **on the workflow engine** — slice 2; bespoke chain retired |
| Cross-module | ❌ **isolated** — duplicates 3 masters, 0 GL/AP/budget links, no events, no reminders (§7) |
| Finance scope here | **master data only** — `Currency` + `ExchangeRate`; GL/AP deferred to the post-module sweep (D-4) |
| Frontend | **0 files** |
| SRS coverage | **none** — see §5 D-1 |

---

## 3. Ground truth — measured 2026-08-17

API running on `http://localhost:5000`, healthy. All figures below are from live calls.

### 3.1 The surface

Route base `api/staff-travel/*`, eight controllers:

| Controller | Endpoints | Route base |
|---|---:|---|
| `StaffTravelComplianceController` | 53 | `/compliance` |
| `StaffTravelFinanceController` | 41 | `/finance` |
| `StaffTravelRequestsController` | 35 | `/requests` |
| `StaffTravelBookingsController` | 25 | `/bookings` |
| `StaffTravelPoliciesController` | 25 | `/policies` |
| `StaffTravelApprovalsController` | 18 | `/approvals` |
| `StaffTravelItinerariesController` | 16 | `/itineraries` |
| `StaffTravelConfigurationController` | 7 | `/configuration` |
| **Total** | **220** | (110 GET, 33 of them parameter-free) |

34 entities in `StaffTravelEntities.cs` (1,577 lines), 3,372 lines of DTO, 3,362 lines of service.

### 3.2 What runs

`smoke-reads.mjs` — every parameter-free GET, as admin:

- **31 → 200**, of which **30 return `array(0)` / `paged(0/0)` / `null`**
- **2 → 400**, both `configuration/exchange-rates/{latest,on-date}`, missing required query params
- The one non-empty response is `requests/dashboard`, which returns a counters object of zeroes

So the read pipeline compiles and executes. It proves nothing about behaviour — there is no data.

### 3.3 What does not run

`smoke-spine.mjs` — create a travel request as a properly employee-linked actor:

```
CREATE  500
```

Root cause, from `logs/erp-api-20260817.log`:

```
SqlException 8618: The query processor could not produce a query plan because a worktable is
required, and its minimum row size exceeds the maximum allowable of 8060 bytes.
  at StaffTravelRequestRepository.GetWithFullDetailsAsync   StaffTravelRequestRepositories.cs:29
  at StaffTravelRequestService.CreateAsync                  StaffTravelRequestService.cs:321
  at StaffTravelRequestsController.Create                   StaffTravelRequestsController.cs:93
```

This is **the same 8060 defect that made area 8 dead**, in the same shape: the row is written,
then the post-write reload blows up before it can be returned. See F-01.

### 3.4 Data state

Zero rows in every travel table. No travel request has ever been created — which follows from
§3.3, since the only create path 500s.

### 3.5 Gating

Every one of the 8 controllers is exactly `[Authorize]` at class level — no policy, no role, no
ownership check. No `HR.Travel.*` permission exists anywhere in the seed. Confirmed by reading
each controller and by grepping the policy registration.

Measured consequence, `smoke-spine.mjs` as an actor holding **only** the `Employee` role: every
management and finance read listed in that script returns 200. That includes
`finance/claims`, `finance/advances`, `finance/claims/unpaid-approved`,
`compliance/documents` (passport and visa records), `requests/all` and `requests/dashboard`.

### 3.6 Tenant scoping — ⚠ **corrected 2026-08-17, read this carefully**

**The repository layer is unscoped, but the service layer compensates, so there is no
cross-tenant leak.** An earlier draft of this document claimed there was. That was wrong, and the
correction matters because it changes slice 0's scope substantially.

What is true:

- **0 of 29** repository read methods take a `tenantId` or filter on `TenantId`.
- The global filter does **not** save them. `ApplicationDbContext.ApplyGlobalFilters` applies
  `e => !e.IsDeleted && e.TenantId == tenantId` **only when `_tenantId.HasValue`**, and the
  constructor that would populate it from `ICurrentUserProvider` is commented out at
  `ApplicationDbContext.cs:60` — *"TEMPORARILY DISABLED - was causing hangs during login"*. In
  the normal DI path `_tenantId` is null, so **the tenant query filter is inert codebase-wide**.
  This is the read-side face of the documented tenancy gap; the write-side stamp at line 8297 is
  disabled by the same condition. **This fact is worth carrying to every remaining area.**

What is **not** true, and was asserted in error:

- Travel reads are **not** cross-tenant. Every travel service read is tenant-scoped — either
  directly (`if (entity.TenantId != tenantId) throw`) or through one of **33 `GetOwned*`
  ownership helpers, all 33 of which check `TenantId`** (verified individually). A by-id read of
  another tenant's row raises "not found". **Genuinely unchecked reads: 0.**

The residue that is real, and small:

1. **5 reads load every tenant's rows into memory and then filter** —
   `StaffTravelRequestService` (all requests, group travel), `StaffTravelPolicyService`
   (policies, vendors), `StaffTravelApprovalService` (templates). Correct, but it does not scale
   and it is trivially fixable.
2. **Defence in depth.** The scoping lives only in the service layer, so any future consumer of
   these repositories inherits no protection. Worth threading properly when a slice touches a
   repository anyway — but not worth 29 signature changes on its own.

⚠ **Method note.** This correction, and two others in §6.1, came from naive regex pattern-matching
that did not survive reading the source. Nested generics break `Task<[^>]*>`, filtered includes
break `.Include(x => x.Nav)`, and a read that looks unscoped may delegate to a helper that scopes
it. **In this codebase, confirm every statically-derived finding by reading the method before
recording it as a defect.**

### 3.7 Approvals

The area ships its own approval chain — `StaffTravelApprovalWorkflowTemplate`,
`...WorkflowStep`, `...ApprovalInstance`, `...ApprovalDecision` — and references no workflow
engine interface anywhere (`grep -n "IWorkflow|SimpleWorkflow|WorkflowService"` across the eight
travel services returns nothing). This is the same bespoke-chain shape area 8 retired onto the
engine in its slice 2.

---

## 4. Environment & harness

### 4.1 Running API

`http://localhost:5000`. **Do not run `dotnet build`** — stop the `ErpSystem.Api` process first
and hand the build to the user; it locks its own output DLLs.

⚠ **Staging needs the JWT signing key passed in, or every request 400s.** `JwtSettings:SecretKey`
is empty in `appsettings.json`, so `AddErpSystemJwtAuthentication` throws
`IDX10703: key length is zero` on **every** request — including `/health`, and including login.
The failure presents as `400 "Invalid argument provided."` on every endpoint, which reads like a
bad payload rather than a missing configuration, and the log line that explains it is buried
under a secondary FK-547 error from the exception-persisting middleware. Start it like this:

```bash
ASPNETCORE_ENVIRONMENT=Staging \
JwtSettings__SecretKey='<any 32+ char key, distinct from PortalSecretKey>' \
  dotnet bin/Debug/net8.0/ErpSystem.Api.dll
```

Staging rather than Development because the dev exception page masks status codes, which makes
every `rejects()` assertion meaningless.

### 4.2 Harness

`D:\Rhema\TDC ERPS\dev-harness\hr-travel\` (outside the repo, per convention).

| File | Purpose |
|---|---|
| `api.mjs` | copied from `hr-medical`; `login`/`get`/`post`/`rejects`/`ok`/`eq`/`summary` |
| `setup.mjs` | mints employee-linked actors — `mintHrActor`, `mintPlainActor` |
| `smoke-reads.mjs` | every parameter-free GET, status + response shape |
| `smoke-create.mjs` | the create payload, as admin — shows the employee-link refusal |
| `smoke-spine.mjs` | create → read → submit as a plain employee, then the gating probes |
| `probe-detail.mjs` | detail reads with an arbitrary id — isolates the plan error from the data |

### 4.3 Actors

Two, per the standard rule. `admin` is SuperAdmin **and not employee-linked**, so it can neither
test a gate nor drive a self-service path — both facts are load-bearing here.

- `mintHrActor` → role `HR`
- `mintPlainActor` → role `Employee` only; the actor that proves the area is ungated

### 4.4 Payload traps already paid for

- `CurrencyCode` is `[Required]` on the create DTO and is easy to miss — the 400 names it.
- The country lookup is **`/api/Country`**, not `/api/countries`; the latter 404s.
- Create is refused with 400 *"Your user account is not linked to an employee record"* when the
  caller is not employee-linked. Same shape area 11 removed from the medical claim path.

### 4.5 Enum values (extracted, save a lookup)

| Enum | Values |
|---|---|
| `StaffTravelType` | 1 Domestic · 2 International · 3 CrossBorder · 4 Regional |
| `StaffTravelPurpose` | 1 BusinessDevelopment · 2 ClientMeeting · 3 Conference · 4 Training … |
| `StaffTravelPriority` | 1 Routine · 2 Urgent · 3 Emergency |
| `StaffTravelRequestStatus` | 1 Draft · 2 Submitted · 3 Approved · 4 Rejected … |
| `TravelInitiatorRole` | 1 Employee · 2 Manager · 3 HrAdmin · 4 TravelDesk |

---

## 5. Decisions taken — do not relitigate

### D-1. The full suite gets built, despite carrying no SRS requirement

The signed SRS (`TDC_ERPS_HR_Payroll_SHE_Environment_Requirements_Specification_v0.3`,
FR-HR-001 → FR-HR-185) contains **no staff-travel requirement**. Its single occurrence of the
word "travel" describes a National Service allowance paid against a GL account. There is no
requirement for itineraries, per diem, visas, advances or bookings.

The only travel ask anywhere in TDC's corpus is in the team's internal *Consolidated Modules
Document*, under **Company Schedule**: a "Travel Itinerary & Travel Request Form", "employees or
managers create travel itineraries for approval", and travel-cost reporting.

This was put to the user on 2026-08-17 with the measurement above and three narrower options.
**The user chose to build the full ported suite anyway.** Recorded here so the question is not
reopened each slice. The consequence to plan around: this area is scoped by the ported backend,
not by the SRS, so "is it in the requirements?" is not a usable test for what to build here.

### D-2. Fix F-01 before any UI work

Non-negotiable ordering, not a preference. Three of the area's most important endpoints are dead
through one method. Every screen that shows a travel request depends on it.

### D-3. Gating lands in this area, not in the W3 permissions sweep

Same reasoning area 11 used for the fallback fix. 220 endpoints across 8 controllers with no
gate is not a sweep-sized job deferred to later; it is this area's slice 0, and the surface is
only worth walking once.

### D-4. Finance integration is split — master data now, accounting later

**Decided by the user 2026-08-17.**

**In scope for area 12:** the Finance **master-data reads** — `Currency` and `ExchangeRate`
(§7.2). These are cheap, they are the things that silently diverge if left alone, and every day
travel keeps its own rate table is a day the two can disagree.

**Deferred to a single comprehensive Finance-integration sweep after the whole HR module is
complete:** GL posting, AP artifacts, advances as receivables, budget consumption — everything in
§7.4.

**Why this is the right cut, not just the cheap one.** GL posting is one accounting design, not
twenty-seven. Travel claims, medical claims, training costs, awards and final settlements all
post, and if each area invents its own treatment while the module is mid-build, the sweep becomes
a reconciliation of twenty-seven inconsistent decisions instead of one design applied
consistently. Master data is the opposite case: it diverges by sitting still, so it is fixed now.

**The obligation this creates:** every area from here on records its money-touching points in
`docs/HR-FINANCE-INTEGRATION-BACKLOG.md` as it is built. The deferral is only safe if the sweep
starts with a complete worklist rather than a re-survey. Areas already closed (2, 4, 11 in
particular) need a back-fill pass into that register before the sweep begins.

### D-5. A booking above the travel policy's cap is refused, and only an admin may authorise one

**Decided by the user 2026-08-17**, on finding that the booking policy gate gated nothing (slice 8).

**The rule:** the server resolves the cap from the applicable `StaffTravelPolicy` and refuses a
breach with 422 naming it. The `*ExceptionApproved` flag asks for authority to proceed and is
honoured only for a caller holding **`HR.Travel.Admin`** — which HR deliberately does not hold — so
a travel clerk with Write cannot approve their own breach. 403 otherwise.

**Rejected:** routing every breach through a `StaffTravelPolicyException` record decided by the
existing approval path. Stronger audit trail, but that entity keys on `PolicyRuleId` — a free-form
rule list that does **not** line up with the cap fields on the policy — so it would have required
inventing a rule-code convention mapping `MaxFlightClassInternational` to a `RuleCode`. Also
rejected: recording the breach without refusing it (visibility, no control).

**Consequence, and the thing to watch:** this makes `HR.Travel.Admin` a *financial* authority, not
just a destructive one. It already gated deletes; it now also gates spending above policy. If TDC
wants those separated, that is a new permission rather than a change to this rule.

---

## 6. Findings register

| id | finding | severity | evidence |
|---|---|---|---|
| **F-01** | Travel-request create, detail read and update are all dead — SQL 8618 | ✅ **fixed slice 0** | §3.3, reproduced |
| **F-02** | All 8 controllers ungated; a plain employee reads claims, advances, passports | ✅ **fixed slice 0** | §3.5, reproduced |
| **F-03** | ~~0/29 repository reads tenant-scoped~~ → **corrected**: services scope every read; residue is 5 load-all-then-filter reads + no defence in depth | ~~high~~ **medium** | §3.6 |
| **F-04** | Approvals are a bespoke chain, off the workflow engine | ✅ **fixed slice 2** | §3.7 |
| **F-05** | Create requires the *caller* to be employee-linked | ✅ **fixed slice 0** (33 sites) | §4.4 |
| **F-06** | Uneven `.Include` coverage across sibling reads | medium | 55 in requests repo, 0 in configuration |
| **F-07** | Travel duplicates Finance currency/exchange-rate and Procurement supplier masters | ✅ **CLOSED** — supplier slice 3, currency/FX slice 6 | §7.2 |
| **F-08** | Travel moves money with **no** GL / AP / budget artifact whatsoever | **high** ⏸ deferred (D-4) | §7.4 |
| **F-09** | Travel alert notifications are never sent; `NotificationSentAt` is set by the caller | ✅ **fixed slice 5** | §7.5 |
| **F-10** | No travel reminder sweep, though three other HR areas have one | ✅ **fixed slice 5a** | §7.5 |
| **F-11** | Company-vehicle transport bypasses Fleet, which already models trips | ✅ **fixed slice 3** | §7.3 |
| **F-12** | **44 write methods return the unreloaded entity** — blank nav names on every create/update response | ✅ **CLOSED slice 5** — audit 0 findings | §6.2 |
| **F-13** | **39 reads go through the generic repository**, which has no includes, feeding nav-dependent DTOs | ✅ **CLOSED slice 5** — audit 0 findings | §6.2 |
| **F-14** | Sibling reads include unevenly; 4 bespoke reads miss a nav their DTO declares | ✅ **fixed slice 0** (request repo); other repos unaudited | §6.1 |
| **F-15** | `RecordDecisionAsync` is AddAsync-then-UpdateAsync (dead-path shape 3) | medium | §6.1 |
| **F-16** | **`InitiatedById` on the desk create is caller-declared** — travel could be filed under a colleague's name | ✅ **fixed slice 7** | found by the form; the browser has no employee id |
| **F-17** | **The booking policy gate gates nothing** — service has 0 references to any policy field; caps and exception flags all caller-supplied | ✅ **fixed slice 8** (D-5) | §8 slice 8, reproduced |
| **F-18** | `GetApplicablePoliciesAsync` **ignores its `staffLevelId`** (a senior-management policy applied to everyone) and is unscoped by tenant | ✅ **fixed slice 8** | read while wiring F-17 |
| **F-19** | Derived money is caller-declared: hotel nights + total, car-rental total, segment duration | ✅ **fixed slice 8** | §8 slice 8 |
| **F-20** | `BookedAt` / `CancelledAt` are caller-declared (F-09's fiction shape, on bookings) | ✅ **fixed slice 8** | §8 slice 8 |
| **F-21** | Both itinerary reads omit their legs' country includes, which the leg repo's own reads have | ✅ **fixed slice 8** | found by `run-slice8-ui.mjs` |

### 6.1 Defect sweep — what has and has not been done

**The static sweep is complete (2026-08-17). The behavioural sweep is not, and cannot be yet.**
That split matters, so it is stated rather than implied.

**Static sweep — run against all 8 services, 8 repositories and the mapper:**

| shape | source | result |
|---|---|---|
| `AddAsync` then `UpdateAsync` in one method | dead-path shape 3 | **1** — `StaffTravelApprovalService.RecordDecisionAsync` |
| Write returns unreloaded entity (stale navs) | list-read shape 2 | **44** — see F-12 |
| Discarded DTO fields | list-read shape 12 | **0** ✅ clean |
| Unpaired-navigation shadow FKs (`%Id1`) | `ef-unpaired-navigation-shadow-fks` | **0** ✅ clean, verified against the live schema (33 travel tables) |
| Reads with no includes behind nav-dependent DTOs | list-read shapes 1/15 | **39 generic + 4 bespoke** — F-13, F-14 |
| Tenant scoping | area-9 shape | 0/29 — already F-03 |

**F-12 detail.** 44 of the area's create/update methods map the entity they just wrote without
reloading it, so every write response returns null for `employeeName`, `vendorName`,
`countryName` and every other navigation-derived field. `StaffTravelBookingService` and
`StaffTravelComplianceService` are the worst affected. The three request-service writes are the
exception — they do reload, which is exactly why they hit F-01.

**F-13 detail.** Only 110 of the area's reads use a bespoke repository method; **39 go through
`GenericRepository.GetByIdAsync` / `GetAllAsync`, which apply no includes at all.** Those feed
DTOs that declare navigation-derived fields — e.g. `_vendorRepository.GetAllAsync` feeds
`StaffTravelVendorDto`, which resolves its country name from `Country`, so the vendor list will
render every country blank.

**F-14 detail.** The bespoke methods are mostly well-included. Real gaps:
`GetWithLinesAsync` misses `FinanceReviewedBy` and `StaffTravelRequest`; `GetWithDecisionsAsync`
misses `StaffTravelRequest`; `GetActiveInstanceForRequestAsync` includes `Decisions` but not
`Approver`/`OriginalApprover` while its sibling `GetWithDecisionsAsync` does (shape 15, uneven
siblings); `GetEffectiveRateAsync` returns a per-diem rate with no `Country` or `StaffLevel`,
both of which its DTO resolves.

⚠ **Two false positives worth recording.** A first pass flagged `GetWithLegsAsync` and
`GetWithSegmentsAsync` as methods that don't do what their names say. They were wrong — both use
a *filtered* include (`.Include(i => i.Legs.OrderBy(...))`) that a naive
`.Include(x => x.Nav)` grep does not match. Any include audit in this codebase must handle
ordered and filtered includes or it will invent defects.

### 6.2 Content audit ✅ **run 2026-08-17, after slice 0** — `audit-content.mjs`

The behavioural pass §6.1 said was blocked. With the spine alive it builds a 16-entity fixture
across the area, then reads it back and asserts what the responses **contain**.

```
CONTENT AUDIT — 14 field(s) resolved, 30 finding(s) across 21 endpoints
```

**F-12 CONFIRMED — every write response tested returns blank navigation names. 14 of 14.**

| create | blank on the response |
|---|---|
| comment | `authorName` |
| itinerary | `requestNumber` |
| flight booking | `vendorName` |
| hotel booking | `countryName`, `vendorName` |
| ground transport | `vendorName` |
| budget | `approvedByName` |
| advance | `employeeName`, `requestNumber` |
| expense claim | `requestNumber` |
| per-diem rate | `countryName` |
| vendor | `countryName` |
| travel document | `employeeName`, `issuingCountryName` |
| visa application | `employeeName`, `destinationCountryName` |
| risk assessment | `destinationCountryName`, `assessedByName` |
| alert | `countryName` |

The travel request is the only create that resolves its names — because slice 0 fixed it. Every
other create in the area returns a payload the UI cannot render a name from.

**F-13 CONFIRMED — every by-id read through `GenericRepository` returns blank names. 7 of 7:**
vendor, advance, travel document, per-diem rate, risk assessment, visa application, hotel booking.

**List reads are clean.** The bespoke repository reads carry their includes, and the ones slice 0
fixed stay fixed. So the defect is precisely on **by-id reads and write responses**, which is
where a detail screen gets its data — the worst possible place for it and the least visible from
a list.

⚠ **`employeeName` comes back `""`, not `null`.** The mapper concatenates first and last name off
a null navigation, so the field is *present and empty* rather than absent. A UI binding renders a
blank cell rather than falling back, and a `!= null` check passes. Assert non-blank, never
non-null.

⚠ **Two findings in the first run were the audit's own error, not defects** —
`hotels by request` and `vendors (all)` were asserted for `countryName`, which the *summary* DTOs
do not declare at all (it lives on the full DTO). Checked the DTO before recording. This is the
third time in this area a statically-plausible finding has evaporated on inspection; see the
method note in §3.6.

**Also not defects, recorded so they are not re-investigated:** four creates 400'd on the first
run purely from wrong payloads — the comment field is `Body` not `commentText`, the approval
template's is `Name` not `templateName`, `visaType` is a **string** not an enum ordinal, and
`BudgetYear` carries a `[Range(2000,2100)]` with no default so omitting it fails validation.

### 6.3 What this means for the slices

F-12 and F-13 are **whole-area shapes**, not a list of endpoints to patch in one sweep. Each
slice fixes them for the endpoints it touches, and re-runs `audit-content.mjs` to confirm its
own rows have gone from the report. The audit is the area's regression net: it should shrink
slice by slice and read **0 findings** when area 12 closes.

Do not treat §6.1 plus this audit as the complete defect set either. On every prior area the
behavioural pass found defects that neither static analysis nor a content check can see — wrong
numbers, unsatisfiable gates, silently skipped rows. Those surface when a slice exercises the
*rules*, not the shapes.

### F-01 — The travel request cannot be created, read or updated ❌ *reproduced*

`StaffTravelRequestRepository.GetWithFullDetailsAsync` chains **25 `.Include` + 17
`.ThenInclude`**, several three levels deep
(`Itineraries → Legs → Activities`, `ApprovalInstances → Decisions → Approver`). SQL Server
cannot build a query plan for the resulting worktable and throws 8618 at *plan compilation* —
which is why it fails on an empty table with a non-existent id, not just on real data
(`probe-detail.mjs` → 500 for a random GUID, while the other six detail reads 400 normally).

It is called from three places in `StaffTravelRequestService`: line 103 (`GetByIdAsync`),
line 321 (`CreateAsync` reload), line 341 (`UpdateAsync` reload). All three are dead.

It is the **only** method in the eight travel repositories at this scale — no other exceeds 8
includes — so this is one fix, not a sweep.

**Fix:** `.AsSplitQuery()`, the pattern already used in 9 HR repository files including
`StaffMovementRepositories.cs`, where area 8's identical 8060 defect was fixed. Add the tenant
parameter in the same edit (F-03) — the method signature is changing regardless.

---

## 7. Cross-module integration register

**Swept 2026-08-17.** The standing rule for this module: **do not keep a parallel HR copy of
something another module already owns.** Read the canonical record; only add to the owning module
if the thing genuinely does not exist there.

Everything below was verified against live endpoints or by reading the entity. Items are grouped
by what has to happen to them, and each carries the slice that owns it so nothing is lost.

### 7.1 The one-line summary

**Zero** references to `GLAccount`, cost centre, `ProjectId`, `Payroll`, `BudgetEntry` or
`SupplierId` exist across all 34 travel entities. Travel disburses advances and pays expense
claims with **no accounting artifact of any kind**. That is the largest integration gap in the
area, and it is not a UI concern — it is a correctness concern about money.

### 7.2 Duplicates — retire the HR copy, read the canonical

| HR copy | Canonical owner | Status | Slice |
|---|---|---|---|
| `StaffTravelCurrencyExchangeRate` (6 fields) | Finance `ExchangeRate` | **live, seeded** — `/api/finance/exchange-rates`, 3 rows | 6 |
| `CurrencyCode` `char(3)` ×11, no FK | Finance `Currency` | **live, seeded** — `/api/finance/currencies`, 4 rows | 6 |
| `StaffTravelVendor` | Procurement `Supplier` | exists (`SupplierController`) | 3 |

**Exchange rates.** Finance's `ExchangeRate` carries rate *types* (Daily / Average / MonthEnd /
YearEnd / Budget / Fixed), quote side (Mid / Buying / Selling), an approval status, variance
thresholds, provenance, usage counts, and immutability once used in a transaction. Live data is
sourced "Bank of Ghana". Travel's copy has `FromCurrency`, `ToCurrency`, `Rate`, `RateDate`,
`RateSource`, `IsOfficial` — and no notion of which rate type a per-diem conversion should use.
Retire `StaffTravelCurrencyExchangeRate` and the 3 endpoints on
`StaffTravelConfigurationController` that serve it; read `/api/finance/exchange-rates` instead.
This also makes travel spend auditable against the same rates Finance reports on — today the two
could silently disagree.

**Currency.** Finance's `Currency` carries `DecimalPlaces`, `CurrencySymbol`, `SymbolPosition`,
`RoundingMethod`, `RoundingPrecision`, `ThousandsSeparator`. Travel stores a bare 3-char string in
11 places, so **it cannot format a foreign-currency amount correctly** — every travel screen
showing money in a non-GHS currency will render it wrong. FK the code to `Currency`.

**Vendors.** `StaffTravelVendor` is a second supplier master — `VendorCode`, `VendorName`,
contact, `AccountNumber`, contract dates, `IsPreferred`, `Rating`, `PaymentTerms` as free text.
Procurement's `Supplier` is canonical, `SupplierPerformanceMetric` already models rating, and
`PaymentTerm` is a canonical entity rather than a string. An airline paid through travel and the
same airline paid through procurement must not be two records.

### 7.3 Overlap — bridge, do not duplicate

**Fleet already has trips.** `api/maintenance/fleet/trips` and `FleetTrip` model exactly what a
company-vehicle journey is: vehicle, requester, **`DriverEmployeeId` (already an HR Employee
FK)**, purpose, origin, destination, planned start/end, expected hours and mileage — plus
`FleetTripDestination` for predefined routes and `FleetCost` for actual cost.

Travel's `StaffTravelGroundTransport` has `GroundTransportType.CompanyVehicle = 6`. When that
value is chosen, the trip must **reserve a fleet vehicle through Fleet**, not record free text in
HR — otherwise two people are promised the same vehicle and neither system knows. External modes
(Taxi, Rideshare, Bus, Train, Metro, Private Car Hire) stay travel-owned, with the vendor as a
`Supplier`. Same for `StaffTravelCarRentalBooking`, which is genuinely external.

### 7.4 Money — must reach Finance ⏸ **DEFERRED, by decision (D-4)**

**Do not build any of this in area 12.** It is recorded here so the later sweep inherits a
worklist instead of re-discovering it. See D-4 for the reasoning and
`docs/HR-FINANCE-INTEGRATION-BACKLOG.md` for the module-wide register.

None of these have a Finance artifact today.

1. **`StaffTravelExpenseClaim`** — carries `PaymentMethod` (a travel-private enum),
   `PaymentReference` (free text) and `PaidAt`. Money is marked paid inside HR with no GL posting
   and no AP document. Finance owns `PaymentMethodController` and the vendor-invoice/payment
   machinery. Note `FinanceReviewedById` is an **`Employee` FK** — the finance review is recorded
   as an HR fact, invisible to Finance.
2. **`StaffTravelAdvance`** — disbursed with no GL entry. An outstanding advance is an **employee
   receivable**: `UnsettledAmount` is a balance-sheet figure currently living only in HR, so it
   appears in no trial balance and no ageing.
3. **`StaffTravelBudget`** — a per-trip envelope (flight / accommodation / per-diem / transport /
   misc) with no link to `BudgetEntry`, `UnitBudget`, a GL account or a cost centre. The
   per-trip breakdown is legitimately travel-owned; what is missing is that it must **consume
   from** the department's finance budget rather than float free.

Also for the later sweep, not now: reimbursement may belong on **payroll** rather than a direct
payment. Payroll is another developer's module — integrate read-only, never modify.

**What area 12 must still do so the later sweep is cheap:** do not invent a parallel posting
mechanism, a private payment-status machine, or an HR-side "ledger" to fill the gap. Leave the
existing `PaymentMethod` / `PaymentReference` / `PaidAt` fields as the plain records they are.
Anything built now to paper over the missing GL link becomes something the sweep has to unpick.

### 7.5 Platform services not wired at all

| Service | Canonical mechanism | Travel today | Slice |
|---|---|---|---|
| Workflow engine | `IWorkflowIntegrationService` + `IWorkflowStatusAdapterRegistry` + entity-type seed | bespoke 4-table chain | 2 |
| Events / notifications | `IAppEventBus` + `EntityActivityEvent` | **nothing** | 1 |
| Reminder sweep | `DisciplineReminderService` / `SheReminderService` / `StaffMovementReminderService` | **does not exist** | 5 |
| Document upload | the controlled upload gate (as area 11 slice 3a) | raw attachment rows | 1 |

⚠ **`StaffTravelAlertNotification.NotificationSentAt` records a fiction.** The only writer is the
mapper, `NotificationSentAt = dto.NotificationSentAt ?? DateTime.UtcNow` — the *caller* asserts a
notification was sent. Nothing anywhere sends one. A travel security alert for a destination
country reaches nobody, while the table says it was delivered. This is
[[hr-dead-path-defects]] shape 8 with a safety consequence.

⚠ **There is no travel reminder sweep**, while three other HR areas have one — and travel is full
of things that expire: passports and visas (`documents/expiring`, `visa-applications/expiring`),
overdue advance settlements (`advances/overdue-settlements`), and upcoming departures. Those
endpoints exist and return data to nobody. Follow the area-9 pattern, including publishing only
**after** the claim commits.

### 7.6 Already correct — leave alone

- `Country` FK — already the shared canonical lookup, used properly throughout travel.
- `Employee` FKs — HR core, correct.
- Holiday calendar — `/api/holiday-calendars` is live but has **0 rows**; already open question 3
  for TDC. Travel date maths will inherit that gap, not create it.

---

## 8. The slices

Sequencing rule: **nothing renders until the spine runs**. Slice 0 makes the area executable and
safe; slices 1–3 make it correct; 4+ build the screens.

### Slice 0 — Resurrect the spine, gate the area ✅ **green 2026-08-17, 54 assertions**

The one slice that cannot be reordered. `run-slice0.mjs`, 54/54.

**The area executed for the first time.** Create → detail read → list → submit all work.

Delivered: `.AsSplitQuery()` + tenant on `GetWithFullDetailsAsync` (F-01); `HR.Travel.Read/Write/
Admin` seeded and applied across all 8 controllers — class-level Read, 80 Write gates, 30 Admin,
no bare `[Authorize]` left (F-02); 33 actor blocks moved onto the audit context (F-05); the
`api/staff-travel/me` self-service surface; the 5 load-all-then-filter reads (F-03 residue).

Seed verified live: `HR` holds Read+Write and **not** Admin; SuperAdmin/TenantAdmin hold all
three. So the gate is enforced by the database permission, not merely by the role fallback.

⚠ **The one failing assertion was worth more than the other 53.** `list read: employeeName` came
back `""`, which pulled the uneven-siblings thread: **nine repository reads feed
`StaffTravelRequestSummaryDto` and four were missing an include** — `GetByEmployeeIdAsync` and
`GetByEmployeeAndStatusAsync` had no `Employee`, `GetByGroupTravelIdAsync` no
`DestinationCountry`, `GetChildRequestsAsync` neither. `GetPagedAsync` had none either, and it is
the primary list endpoint. `GetByEmployeeIdAsync` powers the self-service list, so every
employee's own travel list would have shown a blank name.

⚠ **And the fix for F-03 introduced two of them.** Converting `GetAllAsync()` to
`GetQueryable()` removed the repository's includes along with the load-everything behaviour. A
status-code harness would have shipped it: all five reads returned 200 with the right rows and
the wrong contents. The harness now asserts resolved names on **five** list reads plus the
self-service list rather than one; had it done so originally it would have caught all four
sibling defects in the first run.

**Left deliberately for later slices:** two actor holes of the area-9 shape —
`RecordDecisionAsync` takes the approver from the DTO, and comment `AuthorId` comes from the
payload, so a caller can declare who acted. Slices 2 and 1 respectively.

1. **F-01** — `.AsSplitQuery()` on `GetWithFullDetailsAsync`, plus a tenant parameter on that one
   method. Prove create → read-back → update → submit end-to-end. This is the first time the area
   will ever have executed.
2. **F-02** — `HR.Travel.Read` / `.Write` / `.Admin` seeded and applied across all 8 controllers,
   with the role→permission fallback map extended the way area 11 slice 1 did it.
3. **Self-service** — gating an area whose core use case is "employees or managers create travel
   requests" locks employees out unless a self-service surface exists. Follow the area-11 slice-3
   pattern: a separate `api/staff-travel/me` controller, no employee id in any route or query,
   every id-addressed operation through one ownership helper, someone else's record a **404 not
   403** so ids cannot be enumerated, and no privileged operation routed there at all.
4. **F-05** — drop the caller-must-be-employee-linked requirement from the HR create path,
   keeping an explicit `employeeId`.
5. **F-03 residue** — fix the 5 load-all-then-filter reads (§3.6). Not the 29-signature rewrite
   the earlier draft called for; that was based on a finding since corrected.

Harness: `run-slice0.mjs`. Assert the spine *works* (not that it returns 200), and assert the
plain actor is now refused everywhere it succeeded in §3.5.

**Then, before slice 1: the content audit.** With the spine alive there is finally data to read,
so run the area-11 style pass — every read asserted for non-blank resolved names, every write
response checked for stale navigations (F-12), every generic-repo read checked for empty
navigations (F-13). That audit is what converts §6.1's static list into the real defect set, and
its output should be appended to the findings register before any screen work starts.

### Slice 1 — Requests, groups and the request lifecycle ✅ **green 2026-08-17, 37 assertions**

`run-slice1.mjs`, 37/37. Slice 0 still 54/54. Content audit 30 → 29 findings.

**The lifecycle guards were already correct** — submit/approve/reject/cancel/complete/delete all
check status properly. Surveyed before building, so the slice went where the defects actually
were and the guards are now just a regression net.

Delivered:

1. **Two actor holes closed.** `AuthorId` on a comment and `UploadedById` on an attachment were
   client-supplied — any caller could post under a colleague's name or attribute an upload to
   someone else. Both are stamped from the token and **removed from the create DTOs** so they
   cannot be mistaken for inputs. `UploadedById` was `[Required]`, so a client was obliged to
   declare who uploaded, and could name anyone.
2. **Parent-FK guards.** Comment and attachment creates never validated
   `StaffTravelRequestId`; a comment could be hung off any request id, including another
   tenant's. Both now resolve through `GetOwnedRequestAsync`.
3. **F-12 for these endpoints** — comments, attachments and group travel reload through new
   by-id repository reads carrying their includes.
4. **Lifecycle events** on all five transitions via `IAppEventBus`, published **after** commit.
   Topics are seeded first: publishing to an unseeded topic delivers to nobody while every table
   says it fired, which is F-09 exactly. Templates carry request number, route and dates but
   **not the purpose** — a notification reaches more people than the record does.

⚠ **The area had no error contract, and this is the slice that found it.** Five refusals were all
*correct* and all mute: `400 "The operation is not valid for the current state of the object."`,
swallowing the service's own message. Added `StaffTravelBusinessRulesAttribute` (404/422/403,
each keeping its message) across all 9 controllers. **This belonged in slice 0** — areas 8 and 9
both did "gating *and the error contract*" together.

⚠ **Slice 0 introduced a regression, caught here.** Its census covered *method parameters* — 65
audit against 3 approver — but **two actors travel inside a DTO** and were never censused, and
both land on Employee FKs:

- `CancelAsync`: `CancelledById` fed **both** `entity.CancelledById` (Employee FK) **and**
  `UpdatedBy` (audit). One field, two different identifiers, so whichever id was supplied was
  wrong for one of them. Now split.
- `AddGroupParticipantsAsync`: wrote `createdByUserId` straight into `InitiatedById`.

Then the check that should have run in slice 0: **all 16 Employee FKs in the area swept for
user-id contamination.** Those two were the only ones. The harness now asserts the *stored* id is
an employee — the only way this is visible, since both ids are Guids and every call returns 200
either way.

### Slice 1a — Attachments onto the controlled-upload gate ✅ **green 2026-08-17, 18 assertions**

`run-slice1a.mjs`, 18/18. Regression after: slice 0 54/54, slice 1 32/32.

The endpoint took a caller-supplied `FileUrl` and wrote it to the row, so anyone with travel write
access could point an attachment at arbitrary bytes on disk — including another tenant's. Same
path-injection sink area 11 fixed twice (exam documents, claim receipts), and a travel attachment
is typically a **passport scan or a visa letter**, so squarely in scope.

`POST .../attachments` is multipart now and runs through `HrAttachmentUpload` — the existing helper
built for exactly this shape, where the row carries a required `UploadedById` Employee FK. Scanned,
DMS-registered, stored outside the web root; the row keeps the three DMS ids and an empty
`FileUrl`. `GET .../attachments/{id}/download` reads it back. Entitlement is checked **before**
anything is stored — neither the gate nor the DMS performs that check.

`hr-staff-travel-attachments` is in `SystemCleanScanRequired`, so no tenant policy can permit an
unscanned travel upload. `FileName`, `FileSizeBytes` and `MimeType` are also no longer
caller-supplied — they are read off the stored document, so a client cannot misdeclare a file.

Migration `20260817130709_AddStaffTravelAttachmentControlledUpload`: three nullable columns,
scaffolded then rewritten as guarded SQL per repo convention, `Down` guarded symmetrically.
**Applied and verified on the dev database.**

⚠ **One deliberate departure from the area-11 precedent: no index.** That migration added a
composite `(TenantId, FileUploadRecordId)` and dropped the standalone `TenantId` one, mirroring
`EmployeeMedicalExamDocument`. Here **nothing queries attachments by `FileUploadRecordId`** —
`HrDocumentDownload` resolves `FileUploadRecords` through its own primary key — so a composite
index would carry write cost for no read. Copying the precedent without checking the queries would
have been cargo-culting.

⚠ **Needs `node clamd-stub.mjs` running alongside the API**, since the category requires a clean
scan. Without a scanner every upload 422s. Slices 0 and 1 do not need it.

⚠ **Slice 1a broke slice 1's harness, correctly.** The attachment contract changed from JSON to
multipart, so slice 1's §2 died on a 415. Rewritten rather than deleted: slice 1a owns the upload
assertions, and what belongs in slice 1 is the parent guard it added plus proof the old
caller-supplied-path contract is genuinely unreachable. **When a slice changes a contract, the
earlier slice's harness is the first place to look for what that change actually cost.**

**Integration (§7.5):** attachments onto the controlled-upload gate, as area 11 slice 3a did for
medical receipts. Wire `IAppEventBus` / `EntityActivityEvent` for the lifecycle transitions —
this is the area's first real notification, and everything downstream depends on the seam.

### Slice 2 — Approvals onto the workflow engine ✅ **green 2026-08-17, 19 assertions**

`run-slice2.mjs`, 19/19. Regression: slice 0 53/53, slice 1 32/32, slice 1a 18/18. **122 total.**

The 4-step recipe, seventh application, held unchanged:

1. **Entity type** `StaffTravelRequest` in the catalog, seeded via
   `POST api/Workflow/entity-types/seed`, verified as `STAFF_TRAVEL_REQUEST`.
2. **Status adapter** `HrStaffTravelWorkflowStatusAdapters.cs`, auto-discovered. Following the
   requisition precedent, **checked the enum before adding to it** — `Submitted` already means
   "out for approval", so no enum change.
3. **Routing context** in `SimpleWorkflowService`: cost, currency, international, visa, risk,
   priority, trip length. The currency travels *with* the amount deliberately — a threshold rule
   comparing bare numbers across currencies is wrong.
4. **Display resolver** giving route and dates, because that is what decides whether an approver
   must act today. Purpose omitted, same reasoning as the slice-1 templates.

**The actor hole is closed by construction, not patched.** `RecordDecisionAsync` took the approver
from the request body; that code path no longer exists. The engine resolves the approver from the
authenticated user against the published definition, and `CanUserApproveAsync` gates approve and
reject alike.

**The bespoke chain is retired.** `StaffTravelApprovalsController` (18 endpoints) removed. Evidence
it was safe: **0 steps, 0 instances, 0 decisions** on the reference database — it had never
executed; the only rows were 8 templates the content audit's own fixture created. Nothing in the
frontend referenced it.

⚠ **The four entities and their tables are deliberately left in place.** Dropping them is a
destructive migration for no benefit while they are empty. The service and repositories are now
dead code behind a removed surface — flagged rather than swept into this slice. **Owed: a cleanup
migration when the area closes.**

`ApprovalInstances → Decisions → Approver` came out of the full-details query: the live approval
state is the workflow record's now. Down to **24 includes + 15 ThenIncludes**.

⚠ **Two field-name traps, one of each kind.** The step order field is `Order`, not `stepOrder` —
that one fails loudly ("Step orders must be sequential starting from 1"), which is the *lucky*
case. The approver rule's role field is `Role`, not `roleName` — that one fails **silently**: the
definition publishes, looks correct, and can never be approved. The harness asserts
`pendingApprovers` is non-empty for exactly that reason.

⚠ **Retiring the controller broke two harnesses, correctly.** Slice 0 asserted a plain employee
got 403 on `/approvals/templates` (now 404), and the content audit created a template through it.
Both were fixed by pointing at reality rather than by loosening the assertion. **When a slice
retires a surface, the earlier harnesses are where you find out what depended on it.**

### Slice 3 — Itineraries, bookings, and both cross-module retirements ✅ **green 2026-08-17, 30 assertions**

`run-slice3.mjs`, 30/30. Regression: **151 assertions across five slices, 0 failures.** Content
audit 29 → 27.

**Five missing parent guards.** The four top-level booking creates and the itinerary create took
`StaffTravelRequestId` straight from the payload and never checked it — a flight could be booked
against any request id, including another tenant's. Neither service could even *see* the request;
both needed `IStaffTravelRequestRepository` injected. `AddSegment` and `AddLeg` already guarded
their immediate parents.

**Seven F-12 reloads** via four new tenant-scoped by-id reads. `UpdateFlightAsync` already
reloaded correctly and was left alone.

**Vendor master retired onto Procurement (§7.2).** Six `VendorId` FKs — flights, hotels, ground,
car rentals, visa applications, insurance — repointed at `Suppliers`; `VendorName` resolves from
`Supplier.Name`; the travel-side vendor CRUD is gone. Safe because it was measured first: 12
travel vendors (all harness fixtures), **0 suppliers, 0 bookings referencing a vendor**.

**Fleet bridge (§7.3).** `StaffTravelGroundTransport` gains `FleetTripId`. A `CompanyVehicle` leg
now reserves a real vehicle through `IFleetTripService`; a company-vehicle leg with **no** vehicle
is refused rather than silently recorded as a note. Every external mode stays travel-owned.

⚠ **Fleet's exception vocabulary was leaking into travel's contract.** `FleetTripService` throws
`ArgumentException` for *validation* failures ("Selected asset is not a vehicle"). Travel's error
contract reads `ArgumentException` as "not found" and answered **404**, so a wrong vehicle told the
caller their *travel request* was missing. Now translated at the seam to 422 "The vehicle could not
be reserved: …". **A module boundary must not import another module's exception semantics** — and
this only surfaced because the bridge was exercised for real.

⚠ **Procurement's `SuppliersController` is dead, and has always been.** **Two** repositories are
never DI-registered — `ISupplierContactRepository` and `ISupplierItemCatalogRepository` — so DI
cannot activate the controller and **every endpoint on it answers 400**. DI reports only the first,
which is why the second is easy to miss. Pre-existing and unrelated to this work.

Registration was trialled and **is necessary but not sufficient**: the reads then work correctly,
but `POST /api/Suppliers` returns **201 Created with an empty body and writes nothing** —
`GenericRepository.AddAsync` never calls `SaveChangesAsync`, and the handler also never sets
`TenantId`. The trial was **reverted**; nothing in Procurement is modified.

Full reproduction, evidence and fix list: `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` §1.
**Travel's vendor selection cannot work until that controller does.**

⚠ **CORRECTION.** An earlier draft of this section said supplier creation is gated behind a staged
master-data change, and that the travel desk therefore cannot add vendors. **That was wrong** — the
400 was the DI failure, identical to every other endpoint on the controller; the
`GuardDirectMutationAsync` check returned *allowed* and blocks nothing. The one operational
consequence that IS real: `Suppliers` is empty, so travel has no selectable vendors until suppliers
are onboarded — and they cannot be onboarded through the API while the create is dead.

⚠ **Retiring the vendor endpoints broke slice 0's harness again**, exactly as slice 2 did — it
gated a `/policies/vendors` read that no longer exists. Same fix, same lesson: **the earlier
harnesses are where you learn what a retirement actually cost.**

**Harness fixtures:** `Suppliers` and `MaintenanceAssets` were both empty, so a supplier and a
vehicle are seeded directly in SQL (`fixtures.json` carries the supplier id). The vehicle needs its
*category* to have `AssetType = 'Vehicle'` — Fleet checks the category, not `IsFleetAsset` — and
`Status = 0` (Active); `1` is Inactive and is refused.

### Slice 4 — Finance ✅ **green 2026-08-17, 29 assertions, first run**

`run-slice4.mjs`, 29/29. Regression: **180 assertions across six slices, 0 failures.** Content
audit 27 → 25.

The area-7 lesson governed this slice: a blank cell announces itself, a **wrong total renders
confidently**. Every figure is asserted against a fixture of known quantities — advance 3,000,
lines of 1,200 GHS @1.0 and 100 USD @12.5, approved down to 2,200 — with the advance deliberately
**larger** than the claim, because partial settlement is the case a naive implementation gets
wrong by over-recovering or going negative.

**Three money defects found, all of the render-confidently kind:**

1. ⚠ **The advance was never settled — employees were paid twice.**
   `StaffTravelExpenseClaim.AdvanceDeducted` and `StaffTravelAdvance.SettledAmount` were
   **read-only fields with no writer anywhere in the solution**. Draw a 3,000 advance, claim
   4,000, and you were paid the full 4,000 because `AdvanceDeducted` was 0. Meanwhile the
   advance's `UnsettledAmount` never moved, so it sat on `advances/overdue-settlements`
   **permanently** — the register said the money was outstanding while the traveller had
   effectively had it twice. Settlement now runs on the pay path, capped at the outstanding
   balance so it cannot over-recover, moving the advance to `PartiallySettled`/`FullySettled`.
   The harness asserts the invariant `settled + unsettled = approved`.

2. ⚠ **`NetPayable` had two formulas.** `CreateClaim` computed it from *claimed*;
   `RecomputeClaimTotals` from *approved*. One field, two meanings depending on which path last
   touched it — the area-7 "two screens disagree about one figure" shape. Both now route through
   one helper: approved once anything has been reviewed, claimed before that (otherwise a fresh
   claim reads *minus the advance*).

3. ⚠ **`AmountBaseCurrency` was caller-declared**, and `TotalClaimed` sums it — so a caller could
   claim 100 USD at rate 12.5 and declare the base amount to be anything. Now derived server-side
   as `AmountOriginal × ExchangeRate`, 2dp. **The rate is still caller-supplied**; sourcing it
   from Finance's `ExchangeRate` is slice 6.

**Four more actor holes closed** — `FinanceReviewedById`, `ReviewedById`, `ApprovedById`,
`DisbursedById`, all Employee FKs taken from the request body, so a caller could record that
someone else approved or disbursed money. Stamped from the token, removed from the DTOs.

**Three parent guards** added to budget, claim and advance creates.

**Integration: none, per D-4.** No GL posting was added; the accounting side stays registered as
items 12.1–12.3 in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`, and the settlement code says so in
its own remarks so a later reader does not mistake the travel-side arithmetic for the accounting.

### Slice 5 — Policies and compliance ✅ **green 2026-08-17, 26 assertions**

`run-slice5.mjs`, 26/26. Regression: **206 assertions across seven slices, 0 failures.**

⚠ **F-09 is fixed — the alert now actually reaches somebody.** `NotificationSentAt` was stamped
`dto.NotificationSentAt ?? DateTime.UtcNow`, so the **caller asserted delivery** and nothing
anywhere sent anything: a destination-security alert reached nobody while the table said it had
been delivered. The row is now created with a null stamp, the event is published, and only then is
the stamp written — so a failed publish leaves the row reading *undelivered*, which is the truth
and is retryable. A record that falsely claims delivery is not.

The topic has **two** recipients, deliberately: **Role HR** (the desk, who may have to move a
booking) and **`EmailFromData`** pointing at the traveller's address carried in the event data —
because an alert about the country you fly to next week is useless if it only lands in somebody's
queue. The platform has no "employee" recipient kind, which is why the address travels in the data;
checked rather than assumed.

**Six parent guards** across the compliance creates, each guarding what its DTO actually
references — document (employee), visa application (request + employee), risk assessment,
insurance, alert notification (request + alert). Visa requirements, alerts and health requirements
are genuinely global and take none.

**Two more actor holes** — `VerifiedById` (who checked a passport) and `ApprovedById` on a policy
exception (who granted an exception to travel policy). Both Employee FKs read from the body.

### 🎯 F-12 and F-13 are now CLOSED for the whole area

```
CONTENT AUDIT — 41 field(s) resolved, 0 finding(s)
```

The audit went 25 → 12 → 6 → **0** across this slice. Six compliance repositories, plus advance,
per-diem and hotel, gained tenant-scoped `GetWithDetailsAsync` reads; every create, update and
by-id read whose DTO resolves a navigation now goes through one.

⚠ **Three of the twelve were the audit's own fault, not the code's** — it still exercised the
vendor endpoints retired in slice 3, asserted `vendorName` on bookings where it had never *set* a
vendor, and asserted an approver on an unapproved budget. Reporting those as defects would have
been wrong. **That is the fourth plausible-looking finding in this area to evaporate on
inspection** (see the method note in §3.6): the audit is now both stricter and more honest.

### Slice 5a — the travel reminder sweep ✅ **green 2026-08-17, 20 assertions, first run**

`run-slice5a.mjs`, 20/20. Regression: **226 assertions across eight slices, 0 failures.** Audit
still 0.

Travel was full of dates that mattered and **nothing watched any of them**. The endpoints already
existed — `compliance/documents/expiring`, `compliance/visa-applications/expiring`,
`finance/advances/overdue-settlements`, `requests/upcoming` — returning their rows to nobody. Three
other HR areas had a sweep; travel did not, which is how an expired passport sits unnoticed until
somebody is turned away at a gate.

Four kinds swept: `TravelDocumentExpiring` and `VisaExpiring` (90-day horizon),
`AdvanceSettlementOverdue`, `TripDeparting` (14-day horizon, **approved trips only** — the harness
asserts a Draft trip is *not* announced, which a naive implementation gets wrong).

⚠ **The advance kind only works because of slice 4.** Before settlement existed, `UnsettledAmount`
never moved off its full value, so every disbursed advance would have appeared here for ever. The
sweep would have been noise generating noise.

Three things carried from area 9 rather than rediscovered:

- **Send-once by dedupe key** (item + kind + date + tier) enforced by a unique
  `(TenantId, DedupeKey)` index — claimed in the same `SaveChanges` that records the run.
  Moving a date produces fresh keys and re-arms the ladder, which is wanted: a corrected passport
  expiry genuinely is a new thing to chase.
- **Publish after commit.** An unpublished-but-claimed reminder is one missed notification;
  publishing first risks re-sending on every sweep, for ever.
- **A 90-day backlog floor.** Area 9's first live run queued 275, of which 242 were history.

The **`?asOf=` preview seam** was built in from the start rather than added after the fact — every
date here is server-stamped, so without it the harness could only assert whatever happens to be
true today. §5 uses it to prove the escalation ladder without waiting for time to pass.

**Gated on `HR.Travel.Admin`**, a deliberate step up from the rest of the area: the log lists
references across the whole tenant and forcing a sweep is administrative. The harness asserts that
**HR itself is refused**.

⚠ **`DocumentExpiryHorizonDays = 90` is an assumption, not TDC's number** — chosen as the shortest
notice that still allows a Ghanaian passport renewal, since a 30-day warning about a six-week
document is not a warning. Belongs in §9 with the other assumed windows.

### Slice 6 — Currency and rates retired onto Finance ✅ **green 2026-08-17, 25 assertions**

`run-slice6.mjs`, 25/25. Regression: **252 assertions across nine slices, 0 failures.** Audit 0.

`StaffTravelConfigurationController` is gone — all seven endpoints were duplicate-rate CRUD.
`StaffTravelCurrencyBridge` replaces it as a **read-only** window onto Finance: nothing in it
creates a currency or a rate, because a missing rate should be added in Finance, not invented by
travel, which is exactly what the retired table allowed.

**Currency codes validated on all 11 money-bearing creates** — request, four bookings, visa
application, insurance, claim, advance, budget, per-diem rate. A bare `char(3)` that nothing
checked meant a claim could be filed in "XYZ" and totted up.

⚠ **The visa fee currency is genuinely optional** (`string?`), unlike the other ten. Blanket
validation would have rejected a valid visa application with no fee recorded; the bridge takes an
`optional` flag, and only that site uses it. Enumerating the nullability beat pattern-matching on
the field name.

**The exchange rate now comes from Finance**, closing slice 4's residue: that slice stopped the
caller declaring the converted *amount*, this one stops them declaring the *rate*.

⚠ **No `CurrencyId` FK columns were added**, despite §7.2 hinting at it. Finance's uniqueness is
`(TenantId, Code)`, so a real FK would be composite across eleven travel tables — a large migration
that turns a currency re-code into a schema problem, for little beyond what validation gives. The
parallel *copy* was the rate table, and that is gone; a validated code is a reference, not a
duplicate.

### ⚠ Finance's currency conversion is inverted — found here, inherited deliberately

Measured live: `1 USD → GHS = 0.08` (should be ~12.5), `1 GHS → USD = 12.5`. `FinanceDataSeeder`
writes `Rate = 0.08` meaning "1 GHS = 0.08 USD", while both the `ExchangeRate` documentation and
`CurrencyService.ConvertAsync` read `Rate` as "1 Target = Rate Base". **Rate and InverseRate are
transposed relative to the code that consumes them**, so every conversion in the system is out by
the reciprocal.

**Travel delegates to `ConvertAsync` and inherits the error on purpose.** Reading `InverseRate`
directly would make travel right today and put it in open disagreement with every other module —
two truths about the same trip, which is worse than one shared, fixable error and is precisely the
divergence this slice existed to remove. Fixing Finance fixes travel with no change here. Full
reproduction: `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` §2.

The harness asserts **agreement with Finance** rather than any constant, so it stays correct once
Finance is fixed, and prints a warning naming the defect on every run.

⚠ **Slice 6 broke slice 4's harness, and the breakage was instructive.** Slice 4 asserted that a
line with `exchangeRate: 0` is refused — true then, obsolete now that the caller's rate is ignored.
The assertion stopped failing, so the line it posted was **created**, and the extra 50 USD silently
inflated every total below it. **A stale assertion is not merely noise: it can manufacture the
state it was meant to forbid.** Slice 4 now derives its expected totals from Finance's live rate
rather than a constant.

### Slices 7+ — UI
The W1 recipe again. Operational screens under `/hr/travel/...`; setup and reference under
`/administration/hr/travel/...`.

Verification basis for every UI slice: **`tsc --noEmit` + `eslint` + route resolution only.** There
is no browser-automation tool in this environment, so no screen here has been rendered. What that
does and does not prove is worth being honest about — it proves the calls type-check against the
service layer and the routes resolve; it proves nothing about layout, and nothing about a call
whose shape is right and whose *meaning* is wrong.

#### Slice 7 — Requests: register, self-service, forms, detail ✅ **green 2026-08-17, 11 assertions**

Eight routes and three shared pieces:

| route | |
|---|---|
| `/hr/travel` | the desk's register — 3 quick views, type filter |
| `/hr/travel/new`, `/hr/travel/[id]/edit` | desk create / amend |
| `/hr/travel/[id]` | detail — overview, comments, attachments, workflow |
| `/hr/travel/mine`, `/hr/travel/mine/new` | the employee's own |
| `/hr/travel/mine/[id]`, `.../edit` | self-service detail / amend |

`types/hr/travel.ts`, `services/hr/travel.service.ts`, `components/hr/travel/TravelRequestForm.tsx`
(one form, four routes) and `TravelAttachmentsPanel.tsx`.

**⚠ The slice found a backend actor hole, and it found it by trying to fill in a form field.**
`CreateStaffTravelRequestDto.InitiatedById` is an `Employee` FK recording who *raised* the request,
and it was caller-declared: any desk actor could file travel under a colleague's name. It surfaced
because the browser has **no employee id for the signed-in user** — the client `User` type carries
roles and tenants, not an employee link — so there was nothing honest to put in the field. That is
the general tell: **a value the client cannot know is a value the client should not be sending.**

It is the same shape slice 0 closed 33 times over, and it survived for the same reason
`CancelledById` and the group-participant initiator did — the slice-0 census counted *controller
parameters*, and this actor travels inside a DTO. Fixed in the controller
(`dto.InitiatedById = CurrentUser.EmployeeId ?? dto.EmployeeId`), `[Required]` dropped, and
`initiatedById` removed from the frontend create type so it cannot come back. Note the fallback: an
administrative account with no employee link records the request as self-initiated rather than being
refused over a field it was never able to supply. `run-slice7.mjs`, 11/11; all nine earlier slices
re-run unchanged (252).

**Two design calls worth keeping:**

- **`isInternational` is derived, not asked for.** It was a free boolean on the DTO, which let a
  request claim a domestic trip between two countries. There is no case where the answer is not
  already on the form, so the form computes it from the two country pickers.
- **The self-service detail is deliberately narrower than the desk's.** `api/staff-travel/me` has no
  comment, attachment or approval endpoint, and pointing the page at the desk routes to obtain them
  would 403 for the very employee the surface exists to serve. Desk comments marked
  `isVisibleToTraveller` arrive embedded on the record, so they render with no second call.

Currency is bound to `GET /api/finance/currencies` and countries to `/api/Country` — travel keeps
no list of either, per §7.

#### Slice 8 — Itineraries and bookings ✅ **green 2026-08-17, 59 + 26 assertions**

Two tabs on the request detail (`Itinerary`, `Bookings`), `TravelItineraryPanel` and
`TravelBookingsPanel`, plus `types/hr/travel-bookings.ts` and
`services/hr/travel-bookings.service.ts` over both controllers (~40 endpoints).

**⚠ THE HEADLINE: the booking policy gate gated nothing.** `StaffTravelBookingService` contained
**zero references to any policy field**, while every booking DTO carried `PolicyAllowedClass` /
`PolicyMaxRatePerNight` and a `ClassExceptionApproved` / `RateExceptionApproved` boolean — all four
caller-supplied. A caller could book First class, declare that the policy allowed First, tick their
own exception, and nothing refused it. The real caps were on `StaffTravelPolicy` the whole time
(`MaxFlightClassDomestic/International`, `MaxHotelRateDomestic/International`) and
`GetApplicablePoliciesAsync` already resolved the right policy; it was simply never called from a
booking path. **The area-5 `RequireCalibration` shape: the code reads as built precisely because so
much of it mentions the policy.** Ask which method actually *refuses* something.

**Decision D-5 (user, 2026-08-17): a breach is refused, and authorising one is `HR.Travel.Admin`.**
Rejected alternatives: routing breaches through `StaffTravelPolicyException` (its `PolicyRuleId`
keys to a free-form rule list that does not line up with the cap fields, so it needed a new
rule-code convention invented), and recording-without-refusing (visibility, no control).

Delivered as `StaffTravelPolicyGuard`, a focused collaborator beside `StaffTravelCurrencyBridge`:

1. **Caps are resolved, never declared.** From the policy in force for the traveller's staff level,
   org unit and departure date. Domestic vs international is the *request's* `IsInternational` — the
   trip already knows, and a booking does not get a second opinion.
2. **A breach is 422 naming the cap; asking for an exception without Admin is 403.** The controller
   evaluates `HR.Travel.Admin` through `IAuthorizationService` against the same policy object the
   `[Authorize]` attributes use, and passes the answer in — a service that inspects the caller's
   identity behind the controller's back is how these fields became spoofable. An approved exception
   must also record why.
3. **No policy configured means no cap.** A fresh tenant can still book travel. Refusing everything
   until someone writes a policy is how unmaintained reference data turned a rule into an obstacle
   in area 8.

**Two more defects found while wiring it.** `GetApplicablePoliciesAsync` **took `staffLevelId` and
never used it** — it filtered on unit and date, then merely *ordered* by whether a level band
happened to be set, so a policy written for senior management applied to everyone. It now compares
on `StaffLevel.Rank`. It was also unscoped by tenant, which mattered far more once bookings were
being refused against these caps: another tenant's policy would have decided what yours may book.

**Derived values stopped being the client's arithmetic:** hotel `NumberOfNights` and `TotalCost`,
car-rental `TotalCost`, segment `DurationMinutes`, and `BookedAt`/`CancelledAt` (stamped from status
— the F-09 fiction shape). ⚠ One ordering subtlety: the timestamps are captured **before** the
mapper runs, because `UpdateEntity` still copies them off the DTO, so reading them afterwards would
read the client's value back.

⚠ **Segment duration clamps at zero rather than refusing a negative.** Both datetimes are local to
their own airports, so an arrival before departure is a legitimate westward crossing, not an error;
only a duration computed from UTC would be meaningful. An honest zero beats a fabricated number, and
carrying the offsets is the real fix.

⚠ **The hotel rate cap compares numbers in whatever currency each side carries.** `StaffTravelPolicy`
stores no currency beside `MaxHotelRateDomestic/International`, so there is nothing to convert from.
Sound only while caps and bookings share a currency. Giving the policy a currency is the fix; it is a
schema change beyond this slice. Recorded rather than silently assumed.

**⚠ THE UI PROBE EARNED ITS KEEP — `run-slice8-ui.mjs`, and it found a defect no other check could.**
The screens send a different shape from the rest of the harness: enums as **strings** (`"Economy"`,
not `1`), datetimes as **local wall-clock strings** from `<input type="datetime-local">` (no zone, no
seconds), and `null` rather than `""` for an untouched optional datetime. `tsc` is satisfied either
way and there is no browser here. It caught: **`GET /itineraries/{id}` did not resolve its legs'
country names while `POST .../legs` did** — the leg repository's own reads had the two includes and
both itinerary reads did not. The timeline renders from the nested read, so every leg would have
printed a blank country and the row just added would look right only until a refresh. The
uneven-siblings shape from slice 0, one level down. Fixed on both reads via a shared `WithLegDetail`
with `AsSplitQuery`.

**A wrinkle worth knowing, not a defect:** a freshly stamped `DateTime.UtcNow` serialises with a `Z`
(Kind=Utc); the same value read back from SQL Server does not (`datetime2` carries no offset, so EF
returns Kind=Unspecified). So `...3576655Z` and `...3576655` are one moment written two ways,
depending only on whether the response was reloaded. The first draft of that assertion compared
strings and reported a defect that was not there. Consequence for any client:
`new Date("...3576655")` reads it as **local** time. Platform-wide by construction, not travel's.

**UI notes worth keeping:** the two "authorise above the cap" switches are shown to **everyone** and
answer 403 for a caller without Admin — hiding them would turn "you may not do this" into "this does
not exist", which is a worse thing to tell someone. Nights and hire-day totals are previewed with the
same arithmetic the server runs, labelled as previews, so a disagreement is visible rather than
silent (the [[hr-appraisal-scoring-model]] lesson). Itinerary versions are created alongside the
current one and **promoted as a separate act** — a trip gets re-planned and the desk needs what was
agreed before, not only what is agreed now.

#### Slice 9 — Finance ✅ **green 2026-08-17, 40 + 35 assertions**

Finance tab on the request (budget, advances, claims), the cross-trip claims register with its
awaiting-payment queue, and the claim detail with lines, review and payment.

**⚠ A travel budget's committed and actual spend had no writer but a client `PUT`.** `UpdateEntity`
assigned `TotalCommitted` and `TotalActual` straight off the DTO and derived `Variance` from them,
while the bookings and claims that constitute the spend sat unread on the same request. A
budget-versus-actual screen showed whatever was last typed — worse than showing nothing, because a
hand-entered actual looks exactly as authoritative as a computed one. Now `StaffTravelBudgetRollup`:
committed = non-cancelled bookings, actual = **paid** claims, variance = `ApprovedTotal − TotalActual`
(matching `JobAnalysisService`; the old formula was `TotalActual − TotalCommitted`, a different
quantity with an overspend's sign inverted relative to the rest of the codebase).

Two implementation notes worth keeping:
- **Recomputed on read, not only on write.** Bookings change constantly and the budget row is written
  rarely, so a write-time-only rollup is stale almost immediately — the same failure it replaces.
- **Onto the DTO, never the tracked entity.** Assigning to the entity on a read path would queue an
  `UPDATE` for whatever else in the request calls `SaveChangesAsync` — a read that silently writes.

⚠ **Committed and actual measure different routes and must never be summed.** A booking paid direct
to a vendor is committed and never becomes a claim, so neither figure contains the other. The screen
says so in words, because two progress bars invite being read as parts of one whole.

**Also fixed:** a plain `PUT` could set an advance's `ApprovedAmount`, bypassing the endpoint that
records who approved it — money approved with nobody on record, and `UnsettledAmount` computed off
it. Slice 4 closed the actor half of this and left the amount half. And four caller-declared
timestamps: `DisbursedAt`, `PaidAt`, `FinanceReviewedAt`, line `ReviewedAt` — slice 4 had removed the
*actor* from three of those and left the *moment*.

**⚠ TWO OF MY OWN TYPES WERE FICTION, and only a live call could tell.** The claim-review type was
written as `approve: boolean`; the API takes `newStatus`, because **reviewing sets the claim's status
outright and is NOT derived from its lines**. A screen built on that type ships an Approve button
that leaves claims stuck in `UnderReview` and unpayable, since `pay` refuses anything not `Approved`.
The dashboard type (slice 7) had `upcomingTrips` as a number when it is the list. Both compiled
perfectly. **A TypeScript type written from an endpoint's NAME rather than its DTO is fiction that
type-checks.**

⚠ **The advance is recovered on PAYMENT, not on approval** (`SettleLinkedAdvanceAsync` is called from
`PayClaimAsync`). So an approved claim still reads as fully payable, and the pay dialog must not
announce `netPayable` — the act itself changes it. It shows the anticipated split instead, computed
with the server's own `min(outstanding, approved)` rule and labelled as anticipated; the probe
asserts the two agree.

#### Slice 10 — Compliance and policy ✅ **green 2026-08-18, 47 assertions**

Compliance tab (risk assessment, visas, insurance, active destination alerts) and the travel policy
register.

**⚠ THE HEADLINE, and the lesson: slice 8 made two dormant weaknesses load-bearing.**
`StaffTravelPolicy.ApprovedById`/`ApprovedAt` were on the entity and the read DTO with **no writer
anywhere** — decoration, and harmless, right up until policy caps began refusing bookings. After
that, any `HR.Travel.Write` holder could author the rule constraining everyone's travel spending with
nothing recording who authorised it. Likewise `IsCurrentVersion` was caller-declared with no
uniqueness guard, so two policies covering one scope could both claim to be in force and
`StaffTravelPolicyGuard` picked between them by ordering alone. **Giving a dormant field teeth turns
its neighbours' weaknesses into real defects — audit the neighbourhood, not just the field.**

**Decision D-6 (user, 2026-08-18): a policy is a DRAFT until approved, and approving is
`HR.Travel.Admin`.** Rejected: enforcing regardless with approval as a mere audit trail; and deferring
the question to TDC. ⚠ **Behaviour change on deploy: every policy in every tenant is currently
unapproved, so travel caps do not bind until each is approved.** The register shows a draft count and
a per-row badge for exactly that reason — a list where a draft looks like a policy in force tells you
the organisation is protected when it is not.

Approving also **puts the policy in force**, superseding whichever policy covered the same scope:
approving a rule and then separately remembering to activate it is two chances to get it wrong. An
approved policy **cannot be edited** — raise a version — otherwise the caps could change without
anyone approving the change.

⚠ **`withdraw` was the verb the model was missing, and I created the gap by adding approval.** Once
approval put policies in force, the only ways one could stop binding were approving a replacement
with an *identical* scope or hard-deleting it. A policy approved in error capped everyone until
someone drafted a matching replacement. `POST policies/{id}/withdraw` (Admin) stands it down and
**leaves it approved** — approval is a fact about the past; what changes is whether it is in force.

**Acknowledge-with-no-actor, twice.** `AcknowledgeRiskAssessment` took **no actor at all** and is
Write-gated, so a travel clerk could set `EmployeeAcknowledged` on the employee's behalf — the record
then asserting that someone had read a security briefing about their destination when they had not.
Both acknowledgements now require the caller to *be* the person (traveller / addressee). The entity
has no `AcknowledgedById` column, so rather than add one the caller is required to be who the flag
already claimed. The risk **assessor** (`AssessedById`) was caller-declared too.

⚠ **Four sibling reads feed a policy DTO and only one had any includes.** `GetCurrentVersionsAsync`
and `GetApplicablePoliciesAsync` had none, so "which policies are in force" reported `RuleCount: 0`
for every policy since the port; `GetWithRulesAsync` had four includes but not `ApprovedBy`, so the
approve endpoint returned a response with a null approver — the field the call had just written.

⚠ **A permission an unlinked account holds is a permission it cannot exercise.** The `admin` login is
SuperAdmin and therefore holds `HR.Travel.Admin`, but approving stamps an Employee FK and `admin` is
not employee-linked. Three of this area's Admin writes are like this. If TDC's travel administrators
are unlinked service accounts, policy approval is unreachable for them — a seeding question, not a
code one.

#### Slice 11 — Dashboard and reminder admin ✅ **green 2026-08-18, 38 assertions**

`/hr/travel/dashboard` and `/administration/hr/travel/reminders`.

**⚠ The dashboard's two headline money figures added currencies together.** `TotalEstimatedCost`
summed every request's estimate regardless of the currency it was costed in, and the DTO carried no
currency at all — 5,000 GHS + 5,000 USD rendered as "10,000" of nothing, with no unit beside it.

**Deliberately not fixed by converting.** Travel does not invent a rate (slice 6), Finance's
conversion is currently inverted, and a converted headline would be *confidently wrong* rather than
*visibly incomplete*. `CostByCurrency` splits it; the screen shows one figure for one currency and a
breakdown otherwise, saying plainly why they are not added.

The rest audited clean: both spotlight reads carry their `Employee` and `DestinationCountry` includes
(slice 0's fix covering a fourth consumer). The reminder surface is `HR.Travel.Admin` **including the
reads** — the log names employees against their document expiry dates. ⚠ That means the travel desk
cannot see what the reminder engine is doing on its behalf; worth confirming with TDC.

#### Area close-out — the cleanup migration

Six tables retired across slices 2, 3 and 6 were left in place while they were empty. Removed on
close: the 4 `StaffTravelApproval*` entities, `StaffTravelVendor`, `StaffTravelCurrencyExchangeRate`,
with their services, repositories, DbSets, entity configurations, DI registrations, DTOs and mappers.

Verified before cutting rather than assumed: **no controller route reaches any of it**
(`IStaffTravelApprovalService` was registered in DI and injected nowhere — unreachable since slice 2),
and **no live entity navigates into the dead cluster** except `StaffTravelRequest.ApprovalInstances`,
whose removal matters because it was a FK into a table about to be dropped.

---

---

## 9. Open for TDC

Add to `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` when the area is closed enough to state it well:

**How is a travel expense reimbursed — through payroll, or as a direct payment?** This decides
where slice 4's payment leg posts, and it cannot be assumed. Payroll is another developer's
module (read-only from here), so if the answer is payroll, the integration is a hand-off rather
than something Area 12 builds. Ask before slice 4, not after.

**Who owns travel vendors once they are Procurement suppliers?** Retiring `StaffTravelVendor`
(§7.2) means an airline becomes a supplier record that Procurement's process governs — onboarding,
performance, payment terms. Confirm the travel desk is content to raise vendors through
Procurement rather than keep a private list.

**How much notice does the travel desk need of an expiring passport or visa?** The sweep chases
from **90 days** out (`DocumentExpiryHorizonDays`). That is our number, chosen as the shortest
notice that still allows a Ghanaian passport renewal — a 30-day warning about a document that takes
six weeks to replace is not a warning. Easy to change, but it is running in code today. The
departure announcement uses **14 days** and the first-sweep backlog floor is **90 days**.

**⚠ For Procurement's owner, not TDC:** `SuppliersController` is entirely non-functional — two
missing DI registrations stop it activating at all, and its create is a dead path that answers 201
and writes nothing. Reproduced, trialled and reverted; the full report is in
`docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` §1. **Travel's vendor selection cannot work until
it is fixed.**

**Does TDC operate staff travel in the ERP at all, and if so how much of it?** The system now
carries a full corporate travel suite — per-diem rate tables, visa tracking, travel insurance,
risk assessments, currency exchange rates — that no TDC requirement asks for. The question is not
whether to build it (§5 D-1 settles that) but which parts TDC will actually staff and maintain,
because unmaintained reference data is what made the establishment rule advisory in area 8 and
what blocks the org-authority model today.

---

## 10. Area 12 closed — 2026-08-18

**506 assertions across 16 harness files, all green. Content audit: 41 fields resolved, 0 findings.**
Frontend `tsc` clean (19 pre-existing inventory errors) and `eslint` clean throughout.

| slice | | assertions |
|---|---|---|
| 0 | spine (8060 fix), gating, self-service | 52 |
| 1 / 1a | requests, comments, groups, lifecycle / attachments | 32 / 18 |
| 2 | approvals onto the workflow engine | 19 |
| 3 | bookings, vendor→Supplier, Fleet bridge | 30 |
| 4 | finance: settlement, totals, actors | 30 |
| 5 / 5a | policies, compliance, F-09 / the reminder sweep | 26 / 20 |
| 6 | currency + FX retired onto Finance | 25 |
| 7 | **UI** requests + the initiator actor hole | 11 |
| 8 / 8-ui | **UI** itineraries, bookings + the policy gate | 59 / 26 |
| 9 / 9-ui | **UI** finance + the budget rollup | 40 / 35 |
| 10 | **UI** compliance + policy approval | 47 |
| 11 | **UI** dashboard + reminder admin | 38 |

### What this area taught, beyond travel

1. **Build the create form early — it is an actor audit.** Slice 7's `InitiatedById` and slice 10's
   `AssessedById` were both found by trying to bind a form field to a value the browser cannot know.
   *A value the client cannot know is a value the client should not be sending.*
2. **A UI-payload probe is not optional.** Screens send string enums, local wall-clock datetimes and
   `null`-vs-`""` that no other harness file exercises and `tsc` cannot check. It found F-21 and two
   fiction types of mine. **Copy `run-slice8-ui.mjs` into every future UI slice.**
3. **Giving a dormant field teeth turns its neighbours into defects.** Slice 8 made policy caps
   enforce; slice 10 then had to fix approval, versioning and withdrawal — none of which mattered the
   day before. Audit the neighbourhood, not the field.
4. **Ask which method actually REFUSES something.** The booking policy gate mentioned the policy
   everywhere and enforced nothing.
5. **Derived money must never be the client's arithmetic**, and a figure a call is about to change
   must not be announced before the call.
6. **Fixture determinism is a real cost of stateful harnesses.** Two "failures" were leftovers from
   previous runs. Fixtures now derive their scope from live state rather than taking `[0]`.

### Owed after this area

- **GL posting and the rest of Finance (D-4)** — `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`, 18 entries;
  areas 2, 4, 7, 10, 11 still need back-filling before the sweep starts.
- **Two blocked cross-module defects** — Procurement's dead `SuppliersController` (travel has no
  selectable vendors until it is fixed) and Finance's inverted conversion.
  `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.
- **TDC questions** — §9, plus: should the travel desk be able to see the reminder log (it is
  Admin-gated including reads), and are travel administrators employee-linked accounts?
