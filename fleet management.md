# Fleet Management (Maintenance Module)

## Scope (confirmed)
Build a **Fleet Management** sub-module under **Maintenance** that manages **vehicles only** end-to-end:
master data → operations (trip requests/dispatch) → compliance → fuel → defects → maintenance integration.

Integrations (must work with existing platform capabilities):
- Maintenance: Assets, Work Orders, Job Cards, schedules/checklists
- Workflow: approvals (trip approvals now)
- Notifications: topics-driven in-app + email
- Inventory: inventory issues/consumption (finance posting later)
- Vendors: later (external repairs/services)

## Phases

### Phase 1 (MVP)
1) Vehicle master + vehicle status (Up/Down/InRepair/Decommissioned)
2) Compliance tracking (expiry + critical flag) + dispatch blockers
3) Trip request + workflow approval + dispatch + check-in/out (odometer capture)
4) Notifications/topics for the above

### Phase 2
1) Fuel transactions (linked to vehicle + optional trip) + receipt attachment
2) Consumption KPIs (simple): km/l, l/100km, anomaly thresholds

### Phase 3+
1) Defects/incidents → convert to Work Order/Job Card (vehicle/asset linked)
2) Preventive maintenance plans (time/km/hours) → auto-create Work Orders
3) Vendor repairs, warranty, richer dashboards, finance posting

## Core features (Phase 1–2 detail)

### Vehicle master (vehicles only)
- Plate/VIN, make/model/year, class/type, fuel type, capacity
- Depot/location, assigned unit/department
- Odometer/hour-meter tracking (start with odometer; add hour-meter per vehicle later if needed)
- Attachments (documents/photos)
- Link to Maintenance Asset (recommended):
  - one vehicle ↔ one asset (`AssetId`) so Work Orders/Job Cards attach naturally

### Trips (request → approval → dispatch → check-in)
- Trip request fields: purpose, route, start/end date/time, expected distance, requested-by, driver, vehicle
- Workflow approval (existing workflow engine):
  - EntityType: `FleetTrip`
  - Status adapter: map workflow status → trip status
- Dispatch lifecycle:
  - Checkout: start time + start odometer + condition notes
  - Check-in: end time + end odometer + incidents/notes
- Blockers:
  - Block dispatch if vehicle status is Down/InRepair
  - Block dispatch if any **critical** compliance is expired

### Compliance
- Compliance items per vehicle: type (registration/insurance/roadworthy/etc), expiry date, isCritical, notes, attachments
- Due soon/overdue reminders via topics

### Fuel
- Fuel transactions: datetime, liters, unit cost (optional), odometer, driver, vendor/station (optional), receipt attachment
- KPIs:
  - km/l, l/100km (vehicle and driver rollups later)
  - anomaly flags (threshold-based initially)

### Inventory issues (no finance posting yet)
- Allow inventory issues to reference:
  - `VehicleId` (vehicle consumption)
  - `TripId` (trip consumption)
  - Work Orders/Job Cards (maintenance parts) as per existing flows
- Finance posting comes later; record quantities and let existing valuation logic apply.

## Data model (high level)
- `FleetVehicle` (links to `AssetId`)
- `FleetTrip`
- `FleetFuelTransaction`
- `FleetComplianceItem`
- `FleetDefect` (Phase 3)

## Events & notification topics (examples)
Published as `EntityType.Activity.Audience`, configured in Notifications UI.

- `FleetTrip.Submitted.Internal`
- `FleetTrip.Approved.Internal`
- `FleetTrip.Dispatched.Internal`
- `FleetTrip.Completed.Internal`
- `FleetVehicle.ComplianceDue.Internal`
- `FleetVehicle.ComplianceOverdue.Internal`
- `FleetVehicle.StatusChanged.Internal`

## Confirmed decisions
1) Drivers: use existing Users/Employees (no separate `FleetDriver` table in Phase 1).
2) Metering: support both odometer and hour-meter per vehicle from day 1.
3) “Due soon” policy: send reminders and (optionally) block dispatch within **N** days; **N** is configurable in Admin → Maintenance Settings.
