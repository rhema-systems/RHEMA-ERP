# Hand-off to the Fleet module owner — four things only Fleet can settle, found by staff travel's integration

**Raised by:** HR module work, 2026-10-04 (staff travel final closure, lane 10; decisions D-12, D-27, D-62).
**Severity:** two are real gaps in Fleet on its own (a vehicle or driver double-booked; a fleet trip approved by
nobody), two are requests. Travel guards its own side, so nothing here breaks travel.
**Status:** open. Travel has changed no Fleet code and will not.

This is a self-contained report — nothing in it requires reading HR's plans or code.

HR follows one rule with every module it does not own: **report it, don't change it.** Staff travel books company
vehicles through Fleet. While integrating (2026-10-01 to 10-02), HR read Fleet's trip, compliance, cost, fuel,
assignment and incident code. These four findings are Fleet's to fix or to answer.

---

## What travel does with Fleet today

All of it goes through one HR class, `StaffTravelFleetService` (`src/ErpSystem.Core/Services/HR/`).

- When a staff trip's ground leg is a **company vehicle**, travel creates a fleet trip with `IFleetTripService.CreateTripAsync`
  (purpose *"Staff travel TR-…"*). It submits it with `SubmitForApprovalAsync` **only when Fleet has a published, active
  `FLEET_TRIP` approval route** (see § 3). Otherwise the trip stays a Draft, and the travel record says the vehicle is not
  held yet.
- Edits use `UpdateTripAsync` while Fleet allows them. Cancelling the leg or the trip cancels the fleet trip, but never
  a dispatched one.
- The leg's status, vehicle, plate, driver and times are read from the fleet trip, and its cost from your
  `FleetCostEntries`. A paid fuel expense on a travel claim goes into your fuel log, with its cost entry.
- Before it asks Fleet for anything, travel checks:
  - no vehicle or driver on another **planned** fleet trip (Draft, Submitted, Approved or Dispatched) over the same hours;
  - no critical compliance item blocking the vehicle at drop-off;
  - the driver is not on approved leave or driving another trip.

  These checks protect travel's bookings only, not the ones made in Fleet.

---

## 1. A vehicle or driver can be booked twice — Fleet's clash check sees dispatched trips only

**Where:** `FleetTripService.EnsureNoDispatchedConflictAsync` (`src/ErpSystem.Core/Services/Maintenance/Fleet/FleetTripService.cs:885-901`),
called on create, update and dispatch.

**What happens:** it refuses a vehicle or driver only if they are on a **Dispatched** trip, and it compares no dates.
That cuts both ways:
- two Draft, Submitted or Approved trips for the same hours are both accepted, and the clash surfaces only when the
  second is dispatched while the first vehicle is out;
- while a vehicle is out on one trip, a trip for it next month is refused.

**What a fix needs:** compare the **planned windows** of every trip that is not cancelled, rejected or completed, not
just the dispatched ones, and only where they overlap. Travel's own rule is strict overlap: one trip may end as the
next starts.

---

## 2. A driver is checked for a licence only — not for leave, or another trip

**Where:** `EnsureDriverLicenseValidAsync` (on create and update) is the only check made on a driver, besides § 1's
dispatched-trip clash. Your own tracker calls this DRV-006.

**What happens:** a driver on approved leave, or booked on another trip that day (one not yet dispatched), can be
assigned and dispatched.

**What HR can offer:** HR owns both facts — approved leave, and approved or under-way staff travel. **HR will build a
read-only availability read** for Fleet to call: given employees and a window, it returns who is unavailable and why
(*"on approved leave 12–16 Oct"*, *"travelling on TR-2026-00031"*). Travel's driver picker already makes this check
inside HR. Say which shape you want — one call per dispatch, or a batch for a picker — and whether Fleet should refuse
or only warn.

---

## 3. A submitted fleet trip is approved by nobody — no route is seeded, and submit has no guard

**Where:** `FleetTrip` is in the workflow catalogue (`WorkflowEntityTypeCatalogService.cs:13`), but no approval
definition is seeded for it. `FleetTripService.SubmitForApprovalAsync` (l.346) calls the workflow engine without first
checking that an active definition exists.

**What happens:** with no published definition, the engine completes the submission at once, so **the trip is
approved with nobody asked.** HR met the same engine behaviour in 26 of its own services and guards each one: it refuses
to submit when no route is published. A tenant whose setting `RequirePredefinedFleetTripDestinationOnDispatch` is on
also cannot submit a trip without one of Fleet's predefined destinations. Travel offers those destinations on its side.

**What a fix needs:**
- seed a `FLEET_TRIP` definition (e.g. the transport officer);
- refuse submission while none is published, as HR does.

Travel already submits only when a route is published (D-27), so a fix here changes nothing for travel's legs except
that they start being submitted.

---

## 4. A read of a trip's incidents that Fleet owns

**Where:** an accident on a staff trip should reach the travel record (the trip's Compliance tab, and the travel
desk's nightly sweep). Fleet's reads are gated by `MaintenanceRead`, which HR's users do not hold. So travel reads
`FleetIncidents` by `FleetTripId` straight from the table (`StaffTravelFleetService.cs:492-510`, and the sweep's signals
below it).

**What HR is asking for:** a small read in Fleet's service — incidents for a set of fleet trip ids (date, type,
severity, status, a short description) — callable without `MaintenanceRead`. Then travel stops reading your table, and
the shape stays yours. Nothing is broken meanwhile.

---

## What HR is asking for, in order

1. **§ 3 and § 1** — they put trips and vehicles wrong on Fleet's own screens today, whoever books them.
2. **§ 2** — say whether you want HR's availability read, and in what shape.
3. **§ 4** — when convenient.

Contact: the HR module owner. Related: `docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` (Fleet findings
FX-1…FX-9, decisions D-11, D-12, D-27, lane 6) and `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` #38.
