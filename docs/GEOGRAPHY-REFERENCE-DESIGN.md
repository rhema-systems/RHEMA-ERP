# Administrative Geography — Reference Data Design

> A tenant-configurable model for **administrative divisions** — Region, State, Province,
> Municipal, Metropolitan, District, Town, Locality, Ward — usable by every module that stores an
> address, not just HR.
>
> **Status:** design agreed, not built. **Created:** 2026-09-03.
> **Decisions taken by the user on 2026-09-03** are recorded in §1 and are not open for
> re-litigation in the build: shared reference module, multi-country from day one, additive
> migration keeping the existing free-text columns as a display snapshot.

---

## 0. Why this exists

Today the same idea is spelled four different ways across the codebase, all of them free text:

| Where | Columns | File |
| --- | --- | --- |
| Employee | `State`, `City` | `HREntities.cs:106` |
| Estate managed asset | `Region`, `District`, `Town` | `EstateManagedAsset.cs:60-62` |
| Company profile | `Region`, `City` | `CompanyProfile.cs:80-83` |
| Consultant client, medical facilities, travel, payroll | `Region` / `City` | various |
| Sales, Procurement, Inventory | `State`, `City` | various |

None of them can answer "how many staff live in the Tema Metropolitan area", none of them survive a
spelling variant, and none of them survive a boundary change. There is no shared list, so every
module invents its own.

## 1. Decisions

| # | Decision | Taken |
| --- | --- | --- |
| D-1 | Lives as **shared reference data**, not inside HR. Routes under `/api/reference/geo/*`; admin screens under **Administration → Reference Data → Geography**. HR is the first consumer, not the owner. | 2026-09-03 |
| D-2 | **Multi-country from day one.** The division scheme is per-country data, not code. Ghana is seeded as the first scheme. | 2026-09-03 |
| D-3 | **Additive migration.** Consumers gain a nullable `GeoAreaId`; existing free-text columns stay and are written back as a resolved display snapshot. Nothing is dropped. | 2026-09-03 |
| D-4 | Geography is a **separate tree from `Location`**. See §3. | 2026-09-03 |

## 2. The pattern being reused

The codebase already expresses configurable-depth hierarchies twice, with the same three-part
idiom:

```
OrganizationStructure -> OrganizationLevel (LevelNumber) -> OrganizationUnit (ParentId, Path)
LocationStructure     -> LocationLevel     (LevelNumber) -> Location         (ParentLocationId, Path)
```

Geography is the **third instance of the same idiom**, deliberately — so that a developer who has
read `OrganizationStructureEntities.cs` already knows how this one works.

```
GeoScheme (per Country) -> GeoLevel (LevelNumber) -> GeoArea (ParentAreaId, Path)
```

## 3. Why geography is NOT the `Location` tree

`Location` answers **"which of our sites?"** — an office, a depot, a clock-in station. It is
referenced by roughly thirty foreign keys across HR, SHE, Attendance and Finance: incident sites,
asset custody, attendance devices, company schedules, staff requisitions, geofence zones.

`GeoArea` answers **"where is this on the map of the country?"** — a fact about the world that is
true whether or not the company operates there.

If regions were seeded as `Location` rows, "Greater Accra Region" would appear in the incident-site
picker and the attendance-device station picker. The two trees stay separate, and `Location` gains
a `GeoAreaId` (§5) so a site can say which district it sits in.

## 4. The model

### `GeoScheme : TenantEntity`

One per country per tenant. Ghana is `Region -> Metropolitan/Municipal/District -> Town/Locality`;
Nigeria is `State -> LGA -> Ward`; the UK is `Country -> County -> District -> Parish`. Making the
scheme data is what removes the schema change when a second country arrives.

| Field | Notes |
| --- | --- |
| `CountryId` | FK → existing `Country` (`HREntities.cs:2335`) |
| `Name`, `Code` | "Ghana Administrative Divisions", `GH-ADMIN` |
| `IsDefault` | one default scheme per country |
| `IsActive` | |

### `GeoLevel : TenantEntity`

| Field | Notes |
| --- | --- |
| `SchemeId` | FK → `GeoScheme` |
| `Name` | **This is the field label the UI prints.** "Region", "District", "Town" |
| `Code`, `Description` | |
| `LevelNumber` | 1 = broadest, mirroring `LocationLevel.LevelNumber` |
| `IsRequiredInAddress` | drives client-side validation on the address widget |
| `AllowsAddressAssignment` | whether an address may terminate at this tier |
| `IsActive` | |

`Name` doing double duty as the label is the mechanism by which one React component serves every
country. A Ghanaian employee's form reads Region / District / Town; a Nigerian one reads State /
LGA / Ward; no frontend code differs.

### `GeoArea : TenantEntity`

| Field | Notes |
| --- | --- |
| `SchemeId`, `GeoLevelId` | |
| `ParentAreaId` | null at the top tier |
| `Name` | |
| `Code` | **the official statutory code** (e.g. GSS district code), not an invented one — this is what makes re-seeding idempotent |
| `Path` | materialised, matching the `Location.Path` convention |
| `Latitude`, `Longitude` | centroid, for map display |
| `Shape` / `PolygonCoordinatesJson` | optional boundary, reusing the exact storage shape of `GeofenceZone` (`StaffAttendanceEntities.cs:1201-1206`) and the Leaflet picker already built in the geofence slice |
| `EffectiveFrom`, `EffectiveTo` | see §4.1 |
| `SupersededByGeoAreaId` | see §4.1 |
| `IsActive` | |

### `GeoAreaAlias : TenantEntity`

`GeoAreaId`, `Alias`, `AliasKind` (`FormerName` | `Spelling` | `Abbreviation` | `Vernacular`).
Feeds type-ahead and — more importantly — the import resolver, so a spreadsheet that still says
"Brong Ahafo" or an alternate spelling resolves instead of failing the row.

### 4.1 Effective dating is not optional

Ghana went from 10 regions to 16 in 2019. Districts split most election cycles. The rule:

> **Never rename a `GeoArea` to reflect a boundary change.** End-date the old row, create the new
> one, and point `SupersededByGeoAreaId` at the successor.

Consequence: an employee record created in 2018 still resolves to the region that existed in 2018,
and "headcount by region **as at** a date" stays answerable. A system that renames in place
silently rewrites history, which is the specific failure this design exists to avoid.

## 5. How consumers use it — the one-FK rule

Every entity with an address gets **one nullable `GeoAreaId`**, pointing at the *lowest* tier it
actually knows. **Not** `RegionId` + `DistrictId` + `TownId`.

Roll-up to any tier comes from `Path` / ancestor walk. This is the property that makes the model
flexible in the way that matters: **adding a fourth tier later changes zero columns on zero
consumer tables.** A three-FK design would require a migration on every consumer the day a tier is
added, which is how these systems ossify.

Per D-3, the existing text columns stay. On save the service writes the resolved ancestor names
back into them (`Employee.State` ← region name, `Employee.City` ← town name), so:

- ported rows, external integrations and reports that read the strings keep working;
- the strings become a *snapshot*, explicitly display-only, never the source of truth;
- backfill is a name-match pass that can run repeatedly and leave unmatched rows alone.

## 6. Consumption order

| Phase | Work | Notes |
| --- | --- | --- |
| **1** | ✅ **complete 2026-09-03** — entities, DbContext config, DTOs, service, 24 endpoints, admin screens, migration, Ghana seed pack | see §6.1 and §6.2 |
| **2** | ✅ **complete 2026-09-03** — `Employee.GeoAreaId`, snapshot write-back, shared `<AddressFields>`, Employee form wired, migration applied, 25 assertions green | see §6.4 |
| **3** | ✅ **complete 2026-09-04** — employee import resolves Region / City to a `GeoAreaId`. **Backfill dropped: it had no input** | see §6.5 |
| **4** | ✅ **complete 2026-09-04** — `Location`, `CompanyProfile`, `HealthcareFacility` + their probes + three screens. **Travel deliberately excluded** | see §6.6 |
| **5** | Offer to other modules | Estate's `Region`/`District`/`Town` triple is the obvious first external taker |

### 6.1 What phase 1 landed (2026-09-03)

| Layer | File |
| --- | --- |
| Entities | `src/ErpSystem.Core/Entities/Reference/GeographyEntities.cs` |
| Enum | `src/ErpSystem.Core/Enums/ReferenceGeographyEnums.cs` |
| EF config | `src/ErpSystem.Data/ApplicationDbContext.cs` — DbSets in a `Shared Reference Data` region above the HR region, `ConfigureReferenceModule` beside the other `Configure*` methods |
| DTOs | `src/ErpSystem.Core/DTOs/Reference/GeographyDTOs.cs` |
| Service | `src/ErpSystem.Core/Services/Reference/GeographyService.cs` |
| Controller | `src/ErpSystem.Api/Controllers/Reference/GeographyController.cs` — 24 endpoints under `/api/reference/geo` |
| Permissions | `src/ErpSystem.Shared/ReferenceDataPermissions.cs` + policies + seeding + role grants |
| Frontend | `types/reference/geography.ts`, `services/reference/geography.service.ts`, three screens under `app/administration/reference/geography/` |
| Nav | sidebar **Administration → Reference Data → Geography**; route gate in the administration layout |

**Authorization as built.** Reads are `InternalOnly` with no permission — every module's address form
lists regions, and gating that would break a dropdown for anyone outside the reference-data desk.
Writes need `Reference.Geography.Write` (held by administrators and the HR role); deletes need
`Reference.Geography.Admin` (administrators only). Deletion separates because it is the one act a
later correction cannot undo; end-dating is the reversible alternative and sits with Write.

**Migration:** `20260903223232_AddAdministrativeGeography`, listed in `FastBuildMigrationMetadata`.
Purely additive — four new tables, nothing altered — so it carries no data halt-guards, but its
`CreateTable` calls were rewritten as guarded SQL because `rebuild-db` builds from the EF model and
a rebuilt database already has the tables. Model snapshot diff is 451 insertions, 0 deletions, and
no shadow FK column.

### 6.2 The Ghana seed pack (2026-09-03)

`src/ErpSystem.Data/Seeders/GhanaGeographySeeder.cs`, wired into `HrSeedOrchestrator` immediately
after Countries (it resolves Ghana) and run by:

```
dotnet run --project src/ErpSystem.Api seed-hr-all
```

Idempotent on `Code` within the scheme, so a re-run inserts nothing and a partial run completes
itself. The orchestrator's probe asks for the `GH-ADMIN` scheme specifically, not "any scheme
exists" — the job-architecture trap.

**Four tiers, decided 2026-09-03:** Region → District → Town → Community. Three would have been the
textbook Ghanaian hierarchy, but TDC's own sub-office addresses *are* Tema community numbers
(Community 2, 24, 26 in `TdcLocationSeeder`), and a tier present in the addresses but absent from
the model pushes those straight back into free text.

**⚠ What is authoritative and what is not.** This matters more than the row count:

| Tier | Seeded | Confidence |
| --- | --- | --- |
| Region | **All 16**, plus dissolved Brong Ahafo | **Authoritative** — ISO 3166-2:GH codes |
| District | Greater Accra's 29 MMDAs + Ho Municipal | **Names authoritative; codes derived** — the assemblies' own acronyms (TMA, AMA, KKMA…) namespaced under the region, *not* GSS district codes |
| Town | 7 — TDC's operating footprint | TDC-specific, not a national list |
| Community | 6 — the Tema communities TDC occupies | 3 carry a **provisional placement** flag in their own `Notes` |

**≈232 MMDAs outside Greater Accra are deliberately absent.** They are omitted rather than guessed:
an invented code poisons the idempotency key this whole design rests on, and it is the same
discipline that left TDC's site addresses null rather than inventing them. Load them through
Administration → Reference Data → Geography, or extend the seeder's `Districts` array when the GSS
list arrives. **If TDC supplies official GSS codes, reconcile by UPDATING the existing codes in
place — never re-seed under new ones**, or every district doubles.

**Brong Ahafo is seeded on purpose**, end-dated 2019-02-11 and superseded by Bono, with
"Brong-Ahafo" and "B/A" as aliases. It is what makes a pre-2019 TDC record resolve to the region
that existed when it was written, and an old spreadsheet import succeed. Bono is named successor
because it kept the capital; a record belonging to the Bono East or Ahafo portion has to be
re-stated by someone who knows which, and the area's `Notes` says so.

Coordinates are reused **verbatim** from `TdcLocationSeeder` — the pins already vetted — and are
left null everywhere else rather than approximated.

### 6.3 Verified 2026-09-03

**Seed run** (`seed-hr-all`): 60 areas + 15 aliases created — 17 regions, 30 districts, 7 towns,
6 communities. Re-run reported `[skip] … already seeded` with counts unchanged, so the orchestrator
probe holds.

**Database integrity**, all zero: rows whose `Path` does not end in their own id; children whose
`Path` is not `parentPath + / + own id`; roots with a malformed `Path`; non-region rows with no
parent; children at a tier not deeper than their parent.

**Harness**: `D:\Rhema\TDC ERPS\dev-harness\reference-geography` — **42 assertions, 0 failures**
against the API in Staging. Covers the by-country lookup, the four-tier cascade, ancestor roll-up,
alias resolution, the tree, and the refusals. Two of its assertions guard the pieces most likely to
be regressed into: a dissolved region must be **withheld from the cascade but findable by
`resolve`**, and a country with no scheme must answer **204, not 404**.

### 6.4 Phase 2 — Employee on the tree (2026-09-03)

| Layer | Change |
| --- | --- |
| Entity | `Employee.GeoAreaId` + nav; `State`/`City` re-documented as **display snapshots** |
| EF | FK `Restrict`, explicit `.WithMany()`, index `IX_Employee_Tenant_GeoArea` |
| Service | `IGeographyService.GetAddressSnapshotAsync` + `EmployeeService.ApplyGeoAreaSnapshotAsync` on both write paths |
| DTOs | `GeoAreaId` on detail/create/update; `ClearGeoArea` on update |
| Frontend | `components/reference/AddressFields.tsx`; `EmployeeForm` address block replaced; `countryId` now settable at all |

**The tree wins over the text.** When `GeoAreaId` is set the service rewrites `State` and `City`
from it, on create *before the insert* and on update *after `Apply`*. A caller sending both its own
spelling and an area does not get to keep the spelling — otherwise the two drift and nobody can say
which is right, which is the condition this module exists to end. A **null** area leaves both
columns untouched: most of the register predates the tree and the free text is the only address
those rows have.

**⚠ `ClearGeoArea` exists because a nullable id cannot mean two things.** Every optional field on
`UpdateEmployeeDto` reads `null` as "not supplied" (the same limitation `CountryId` has). Without
an explicit flag, a user who emptied the region picker would watch the save succeed and change
nothing — a silent no-op, the exact defect shape this module keeps finding in ported code.

**⚠ No `GeoAreaName` on the DTO, deliberately.** It would be null on every read whose query did not
`Include` the navigation, and the Include depth would have to be right in a dozen places. Lists
print `State`/`City` — which is what the snapshot is *for* — and the edit form resolves the display
chain from `GeoAreaId` through the ancestors endpoint it must call anyway to re-open its cascade.

**Two gaps this closed on the way past:** the employee form had no country selector at all, so
`Employee.CountryId` could never be set from the UI despite existing on the entity and both DTOs;
and `State` was free text with no relationship to anything.

**⚠ Deleting an area in use is refused by the SERVICE, not the foreign key.** Geography deletes are
soft, so no constraint is ever consulted — an area with an employee living in it deleted cleanly,
disappeared from every read, and took the address with it while leaving a dangling id. Found by the
phase-2 harness on 2026-09-03. Each consumer now registers an `IGeoAreaConsumer` probe
(`EmployeeGeoAreaConsumer` is the first) and `DeleteAreaAsync` asks them all. **A module that gains
a `GeoAreaId` and does not register a probe gets no protection whatsoever** — phase 4 must add one
per consumer, not just the column.

**⚠ The legacy `api/Employees` controller bypasses `EmployeeService`** — AutoMapper writes straight
to the entity, so it skips staff numbering, payroll validation and the snapshot write-back.
`GeoAreaId` is therefore `Ignore()`d in both mapping profiles: the link is settable only through
`api/hr/Employees`, which goes through the service. The wider problem — two employee write paths,
one skipping every service rule — is pre-existing and out of this slice's scope.

**Verified 2026-09-03:** migration applied; `run-phase2.mjs` in the geography harness —
**25 assertions, 0 failures**, on top of phase 1's 42. The screens have not been browser-walked.

### 6.5 Phase 3 — the import places addresses (2026-09-04)

**⚠ The backfill was dropped, not deferred.** Measured before building it: **0 of 1605 employees**
have any `State` or `City` text, and the single CompanyProfile has none either. A backfill pass
would have processed zero rows. Only 5 Locations carry city text, and those belong to phase 4. The
import is therefore the *only* route by which geography enters the register, so that is where the
work went. Should real address text ever arrive in bulk, `GeographyService.ResolveAsync` is the
resolver a backfill would loop over.

The template's `Region` and `City/Town` columns already existed and were copied as inert text. They
now resolve to a `GeoAreaId`, and `EmployeeService` rewrites the text from the tree. Two reference
sheets ship in the template: **Regions**, and **Cities and Towns** with the region each sits in.

**⚠ Everything about geography WARNS; nothing errors.** Only Greater Accra and Ho are seeded — some
232 MMDAs are deliberately absent (§6.2) — so an unplaceable address must never block a row, or the
import would reject most real Ghanaian addresses. The text is kept, the row imports, the link is a
bonus.

**⚠ An unseeded tree means total silence.** With no scheme, the columns behave exactly as they did
before geography existed. Otherwise a missing reference table would produce a wall of warnings on
every existing import — a regression dressed as validation.

**⚠ Ambiguity is reported, never guessed.** `GeoAreaLookup` exists instead of reusing `LookupTable`
precisely because the latter keeps the first item registered under a key and drops the rest. Place
names repeat across regions; picking whichever loaded first would file someone in the wrong half of
the country silently.

**Retired areas explain themselves but are never resolved to.** A sheet saying "Brong-Ahafo" is told
it was replaced by Bono, and the row imports unplaced. Brong Ahafo became three regions and only a
person knows which — placing them all in Bono would be a wrong answer that looks right.

**Verified 2026-09-04** — `run-phase3.mjs`, **31 assertions**. Full suite green:
74 + 52 (import) + 42 + 25 + 31 (geography) = **224, 0 failures**. The committed register showed the
design in five rows: resolved-and-rewritten; retired kept as text; ambiguous kept as text;
unknown city kept as text but still placed at its region; region-only placement.

**⚠ A defect this found in phase-3's own code:** the loader filtered areas on `IsActive`, and a
dissolved area is inactive *by definition* — so no retired area ever loaded and the successor
message could not fire. The query now decides what is *known*; the live/retired split decides what
can be *resolved to*.

### 6.6 Phase 4 — the company's own places (2026-09-04)

`Location` (sites), `CompanyProfile` (the registered address) and `HealthcareFacility` each gained a
`GeoAreaId`, an `IGeoAreaConsumer` probe, service write-back and a screen wired to
`<AddressFields>`. Migration `20260904005351_AddGeoAreaToLocationCompanyAndFacility`.

**⚠ The rule that decided the scope, and it is not "everything with a City column":**

> A `GeoAreaId` belongs on a record whose address is a property of a **place**. It does not belong
> on a record that merely **mentions** a city.

Sites, the company's seat and hospitals are places. `StaffTravelHotelBooking`,
`StaffTravelPerDiemRate` and `StaffTravelAlert` mention cities — usually foreign ones the Ghana
scheme cannot hold — and keying a per-diem rate by area is a rate-model change, not an address.
Columns that would stay permanently null are a false promise of coverage.
`MedicalInsuranceProvider` was left for the same reason: nothing reads its address geographically.

**⚠ Four consumers now means four probes.** The foreign keys protect nothing on their own: these
deletes are soft, so no constraint is consulted. `GeoAreaConsumers.cs` carries all three new probes
beside the employee one, and its header states the rule for whoever adds the fifth.

**A gap closed on the way past:** the healthcare-facility form had no country field at all, so
`HealthcareFacility.CountryId` could not be set from the UI — the same gap the employee form had.
`<AddressFields>` brings a country picker with it, so wiring it closed both.

**Verified 2026-09-04** — `run-phase4.mjs`, **15 assertions**: all three write-backs rewrite City
from the tree over deliberately-wrong text, the profile rewrites Region too, and the delete refusal
names all three consumers by count. Phase 1, 2 and 4 green together (42 + 25 + 15).

**⚠ A bug this found in phase 2's code:** the refusal message derived its singular by trimming an
"s", producing "1 healthcare facilitie". `IGeoAreaConsumer` now states the singular rather than
computing it — English pluralisation cannot be derived, and a message telling someone their data is
in use is the last place to guess.

## 7. Conventions this build must honour

- `TenantEntity` throughout; **stamp `TenantId` explicitly on create** and tenant-filter every read
  (the ported-HR tenancy gap).
- New tables must be regenerated into the EF model snapshot. (⚠ Superseded 2026-09-19: the
  `FastBuildMigrationMetadata` listing no longer exists — discovery metadata is generated. Do not
  re-create that file; see `docs/LOCAL-FAST-EF-BUILD.md`.)
- Unique indexes are filtered on `IsDeleted` where soft delete applies — a soft-deleted area must
  not hold its `Code` hostage.
- Seed data keyed on official `Code`, so re-running the seeder is a no-op rather than a duplicate.

## 8. Open questions

1. ~~**Depth for Ghana's first scheme**~~ — ✅ **answered 2026-09-03: four tiers**, Region →
   District → Town → Community. See §6.2.
2. **Boundary polygons.** ✅ Columns landed in phase 1 and are empty, as recommended — the storage
   and the Leaflet picker exist, the shapefiles do not.
3. **The ≈232 missing MMDAs** — does TDC have the Ghana Statistical Service district list with its
   official codes? That is the one input that would make the district tier national rather than
   Greater-Accra-only. Until then, districts outside Greater Accra and Ho must be added by hand.
4. **Three provisional community placements** need TDC to confirm: whether Community 24 and
   Community 26 fall under Tema Metropolitan or Tema West, and whether Sebrepor sits under
   Kpone-Katamanso. Flagged in each area's own `Notes`; re-parenting is a screen action, not a code
   change.
5. **Does Estate want to be phase 4 rather than phase 5?** Their `Region`/`District`/`Town` triple is
   the most acute case and they are on the same tenant.
