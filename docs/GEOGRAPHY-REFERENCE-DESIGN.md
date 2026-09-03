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
| **2** | Shared `<AddressFields>` component reading the scheme for the selected country; wire **Employee** first | retires the free-text `State` input; closes the deferred Country→Region cascade |
| **3** | Backfill pass + employee import catalogue gains Region / District / Town columns resolving by name-within-parent | unresolved values surface in the existing per-row diff rather than failing the import |
| **4** | `Location.GeoAreaId`, `CompanyProfile`, medical facilities, travel destinations | |
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

## 7. Conventions this build must honour

- `TenantEntity` throughout; **stamp `TenantId` explicitly on create** and tenant-filter every read
  (the ported-HR tenancy gap).
- New tables must be regenerated into the EF model snapshot; the migration must be listed in
  `FastBuildMigrationMetadata` or it is inert.
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
