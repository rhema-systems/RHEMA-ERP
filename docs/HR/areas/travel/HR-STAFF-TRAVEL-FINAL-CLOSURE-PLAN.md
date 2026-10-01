# HR Staff Travel — final closure plan

**What this is:** the single tracking document for the final, end-to-end closure of the Staff Travel
module — requests and their approval, itineraries, the four kinds of booking, budgets, advances,
expense claims and payment, the travel policy and its caps, compliance (passports, visas, risk
assessments, destination alerts, insurance, health), the portal, the reminder sweep, notifications,
and the seams with Finance, Fleet, attendance, leave and separation. It supersedes the to-do side of
`HR-STAFF-TRAVEL-SYSTEM-GUIDE.md` § 19 (T-1…T-58); the guide stays the screen-by-screen reference.

**How it was made (2026-10-01).** Three passes, each re-reading the code rather than the earlier
documents:
1. **First review** — every travel service, controller, entity, DTO, mapper and repository, the
   policy guard, the budget rollup, the reminder engine and its host, the workflow adapter and the
   no-definition fallback, the notification publisher, the currency bridge, the permission map and
   the HR workflow seeder, plus three sweeps (backend field and enum census, frontend wiring,
   notifications and seams). Findings A, B, C, D, E, F (§ 3a). Decisions D-1…D-6.
2. **Second review** — an independent re-check of every High finding in source (all held, seven
   corrections, § 3c) and a search for what the first pass missed: findings O-1…O-18 (§ 3b), the guide
   items the first plan had dropped (§ 3d). Decisions D-7…D-10.
3. **Fleet review** — the seam with Fleet Management, which travel already calls but does not really
   use: findings FX-1…FX-9 (§ 3e). Decisions D-11, D-12.

**Scope decision, 2026-10-01:** close all of it — the user asked for this to be the ultimate and
final review of the module, with every discovered issue fixed. The user accepted the plan and asked
for development to be held until they say go.

**START HERE:**
1. § 1 is settled — twelve decisions, all taken with the user on 2026-10-01. Decision numbers in this
   document are **travel-closure-local**; they are not the closure ledger's D-01…D-40.
2. **Lane 0 first** (§ 4): it makes the travel harness runnable on UAT (it cannot mint actors there as
   written), records the baseline of the sixteen existing suites, and fixes the frontend fictions that
   fail in front of a user today. Lane 0 needs no schema.
3. **Then migration batch 1** (§ 5): the user scaffolds it, it is rewritten as guarded SQL, the user
   builds, the data part is dry-run on UAT and applied on the user's go. Lanes 1–9 build on it.
4. Then lanes **1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10** in that order (§ 2). Source-check each lane
   against this document before building it — line numbers are as of HEAD `bad482a8d`.

**House rules** (from the HR programme, not repeated in each lane): the user runs builds — never
`dotnet build`; stop `ErpSystem.Api` by command line before the user builds; migrations are scaffolded
by the user and rewritten as guarded SQL; **stage, the user commits** — never `git commit`; `hrdev` is
never pushed; the harness lives at `D:\Rhema\TDC ERPS\dev-harness\hr-travel\`, outside the repo, and
runs against UAT the way the performance closure does (Staging, the JWT key, the UAT connection
string, the API started with `dev-harness\hr-performance\tools\start-api-uat.ps1`, two HR officers,
the clamd stub for uploads); **UAT is the demo database — no fixture may resolve to real staff**;
every suite tears down in a `finally` and switches off the logins it minted; never `python -`;
PowerShell bulk edits mangle UTF-8; a demo-pack scenario a lane's new rule breaks is fixed in that
lane's slice.

---

## 1. Decisions (all taken with the user, 2026-10-01)

| # | Question | Decision |
|---|---|---|
| **D-1** | Eight policy caps have an editor and no reader (C1) | **Enforce the enforceable, drop the rest.** `MaxSingleTripBudget` at request submit; `ReceiptRequiredAbove` and `ExpenseSubmissionDays` at claim submit; `AdvanceBookingDaysFlight/Hotel` at booking create; `PreferredVendorMandatory` — a booking needs a Supplier. `RequiresCheapestFare` and `MaxAnnualTravelBudget` leave the form and the DTOs (the columns stay). |
| **D-2** | The money chain sits on one Write permission with no self-check (B2) | **Self-check plus two-person pay.** Nobody reviews, approves, disburses or pays their own claim or advance; whoever approved a claim or advance cannot be the one who pays or disburses it. Stays on `HR.Travel.Write`. |
| **D-3** | The admin tier needs an employee link that no admin login has (T-1, T-2) | **Grant `HR.Travel.Admin` to the HR role.** The narrowing is done in code: author ≠ approver on policies (lane 4), D-2 on money, D-8 on breaches, status guards on every delete (lane 7). The reminders screen opens to HR. |
| **D-4** | No travel notification reaches the traveller in the app (F1, F2) | **Full delivery on leave's pattern.** One topic per event and audience, in-app and email, recipients by `UserFromEmployeeIdData`, `UsersFromData` and `Role` (`LeaveReminderService.cs:379-531` is the model). |
| **D-5** | 95 of 194 client methods have no screen | **Build the doors that make existing features true** — booking edit/cancel/status and flight segments, a travel-documents register, portal claim filing with receipt upload, portal risk-assessment acknowledgement, portal view of advances/claims/itinerary/bookings/visas, the desk's overdue-settlements queue, visa-application edit. No per-diem screen; no policy-exception screen. |
| **D-6** | `ReturnedForRevision`, `InProgress` and `Closed` have no writer (A3, T-7) | **Give all three real writers.** The approver returns a request for revision; the nightly sweep moves Approved → InProgress on departure; Completed → Closed when every claim is paid or rejected and every advance settled (sweep, plus an HR Close verb). |
| **D-7** | A line manager cannot open or approve a travel request (O-1) | **Two stages, like leave.** Stage 1 the traveller's line authority — their supervisor, or the head of their unit or any unit above it — enforced in the travel service, not just by the engine's role; stage 2 HR. A read door and an approvals queue for the approver. A traveller with no line authority who has a login goes to HR at stage 1. |
| **D-8** | With HR holding Admin, the booker can authorise their own breach (O-3) | **A second person authorises.** A booking above a cap is saved awaiting authorisation and cannot be confirmed or ticketed until a different Admin holder authorises it; authoriser and time are recorded; a breach register lists every over-cap booking. The `AdvanceBookingDays` override follows the same rule. |
| **D-9** | Nothing can change a trip after approval (O-10) | **Send it back for re-approval.** A *Request change* verb returns an Approved trip to ReturnedForRevision; it is edited and approved again; bookings, advances and claims stay linked. Not allowed once the trip is InProgress. |
| **D-10** | "Paid by payroll offset" pays nobody (O-6) | **Hide payroll offset** from the pay dialog and refuse it in the API until payroll can receive travel claims; existing rows keep their value; a hand-off is recorded for the payroll owner. |
| **D-11** | Which Fleet integrations (FX-1…FX-8) | **All four:** core reservation and sync; fuel on claims; drivers as travellers; incidents and trip signals (with the traveller's assigned official car as the default vehicle). Lane 6. |
| **D-12** | Who fixes Fleet's own gaps — planned-window conflicts, driver leave, no seeded fleet-trip approval (FX-2, FX-7, FX-9) | **Hand them to the Fleet owner; travel guards its own side meanwhile.** Travel's checks refuse overlapping vehicles and unavailable drivers for travel bookings. This closure does not change Fleet's code. |

**Standing assumptions (not re-asked):** the closure ledger's D-29 holds — the policy rule register
stays read-only and the policy-exception flow stays withheld until rule enforcement exists; Finance
posting stays as the posting sweep left it (no rule = Unposted, enabled under HR Settings → Finance
posting); the generic workflow inbox's desync is cross-module defect #15 and is recorded, not fixed.

---

## 2. Lane status

| Lane | What | Schema | Suite | Status |
|---|---|---|---|---|
| **0** | Harness on UAT, baseline, truth and fiction | none | `run-final-truth.mjs` | ☐ not started |
| **M1** | Migration batch 1 | the whole batch | — | ☐ not started |
| **1** | Request lifecycle | batch 1 | `run-final-lifecycle.mjs` | ☐ |
| **2** | The approval ladder and the approver's door | batch 1 | `run-final-approvals.mjs` | ☐ |
| **3** | The money chain | batch 1 | `run-final-money.mjs` | ☐ |
| **4** | Policy and authority | batch 1 | `run-final-policy.mjs` | ☐ |
| **5** | Bookings and itinerary | batch 1 | `run-final-bookings.mjs` | ☐ |
| **6** | Fleet | batch 1 | `run-final-fleet.mjs` | ☐ |
| **7** | Compliance and the portal | batch 1 | `run-final-compliance.mjs`, `run-final-portal.mjs` | ☐ |
| **8** | Notifications and the sweep | batch 1 | `run-final-reminders.mjs` | ☐ |
| **9** | Cross-module touchpoints | none | `run-final-touchpoints.mjs` | ☐ |
| **10** | Docs, demo pack, harness, hand-offs | none | the full regression | ☐ |

A lane is done when its suite is green **twice** on UAT, the travel regression holds its count, this
document, the guide and the memory carry the new state, and the slice is staged for the user.

---

## 3. Findings (verified in source on 2026-10-01, HEAD `bad482a8d`)

Severity: **H** — money, authority or data wrong · **M** — does not do what it says · **L** — tidy.
Each finding names the lane that owns it.

### 3a. First review

**A. Request lifecycle** (`StaffTravelRequestService.cs`, the workflow adapter, the mapper, `/me`)
- **A1 H** `UpdateAsync` (l.539) refuses only Approved/Completed/Cancelled/Closed, so a **Submitted**
  request can be rewritten while the approver decides — on the desk and through `/me`; Rejected and
  InProgress too. → lane 1
- **A2 H** `CancelAsync` (l.713) never cancels the engine instance (leave and movements call
  `CancelWorkflowAsync`), so a cancelled Submitted trip leaves a live approval task; cancelling an
  Approved trip leaves confirmed bookings counted as committed, a disbursed advance outstanding and a
  Fleet trip live. → lanes 1, 6
- **A3 M** `ReturnedForRevision`, `InProgress` and `Closed` have no writer (D-6). → lanes 1, 8
- **A4 M** `SubmitAsync` checks nothing but status: cost 0, return before departure (the mapper
  clamps the duration to 0), past dates, no itinerary, the policy; `SubmittedAt` comes from the DTO's
  default. → lane 1
- **A5 M** The mapper (`StaffTravelMappingExtensions.cs` l.111–172) takes `IsInternational`,
  `ApprovedBudget` (on a plain update), `PolicyId` (read by nothing), `GroupTravelId`,
  `ParentRequestId`, `OrganizationUnitId` and `EmployeeId` from the payload with no tenant check. The
  policy guard's own comment says *IsInternational is derived from the two countries* — it is not;
  declaring a domestic trip international buys the international caps. → lane 1
- **A6 H** `GET /me/requests/{id}` returns every comment — `IsVisibleToTraveller` is read by nothing
  on the server, only the client filters — plus the budget, advances, claims and policy exceptions.
  → lane 1
- **A7 M** The desk's *Add comment* sends `commentType: 'General'`, which is not a C# member, so it
  **always 400s** (`hr/travel/[id]/page.tsx:137`); `isVisibleToTraveller` is hard-coded true (T-27).
  → lane 0
- **A8 M** The request form offers `Negotiation` and `Extreme` (T-3, both 400) and hides `Emergency`,
  five purposes, `Critical`, `Prohibited` and `System`; its replace payload drops `approvedBudget`,
  `policyId` and `groupTravelId`, so editing a group participant silently removes them from the
  group; a self-service request gets no organisation unit although a comment says it does. → lanes 0, 1
- **A9 M** The attachment upload offers six types, five of them not C# members — 400 unless *Other*
  (`TravelAttachmentsPanel.tsx:33`); its delete button renders for HR and 403s. → lane 0
- **A10 L** Comment edit and delete check no author; the desk path takes `CancelledAt` from the body;
  the request has no approver column (only `UpdatedBy`); the request-number generator is not atomic
  (recorded). → lane 1
- **A11 M** Group travel: `MaxParticipants` is not enforced, the group's status is whatever the PUT
  says, dates and destination are not pushed to participants, an existing request cannot be linked,
  and the add-traveller dialog hard-codes purpose, risk, `requiresVisa: false` and `GHS`. → lane 1
- **A12 M** No recall verb in the service or controller although `HrWorkflowFallbackAuthority` says
  *use all four or none*; the generic recall button works only while a definition is published.
  → lane 1

**B. The money chain** (`StaffTravelFinanceService.cs`, its controller and repositories)
- **B1 H** `ReviewClaimAsync` (l.361) has **no from-status guard and no to-status validation**: Draft →
  Approved or Paid, Paid → Approved → paid again — the advance is recovered twice and `PaidAt`
  overwritten, while the Finance journal is deduplicated, so the ledger and travel drift apart.
  → lane 3
- **B2 H** No segregation of duties on money: review, pay, approve-advance and disburse are all
  Write with no self-check; a claimant can approve and pay their own claim (D-2). → lane 3
- **B3 H** `TravelAdvanceId` and the claim's and advance's `EmployeeId` are not validated — a claim can
  settle another employee's advance or be filed for someone who is not the traveller;
  `ReceiptAttachmentId` and `PerDiemRateId` are unchecked; claims and advances are accepted on Draft,
  Rejected and Cancelled requests, and `PayClaimAsync` pays on a cancelled trip. → lane 3
- **B4 H** Approving a claim with no reviewed lines pays `TotalClaimed` (l.809) while the GL posts
  nothing (`HrFinancePostingCommandFactory.cs:101, 128`); `ReviewClaimLineAsync` does not cap
  `AmountApproved + AmountRejected` at the line's amount. → lane 3
- **B5 H** Lines sent **with** the claim on create are never valued (`CreateClaimAsync` skips
  `ApplyBaseCurrencyAmountAsync`): rate 0, base amount 0, currency unchecked. → lane 3
- **B6 M** Lines can be added, edited, reviewed and deleted on Submitted, Approved and Paid claims —
  the only guard is the posting register, inert without a posting rule; `UpdateClaimAsync` allows
  PartiallyApproved, Submitted and UnderReview and can re-point the advance and the currency. → lane 3
- **B7 M** The *awaiting payment* read excludes PartiallyApproved, which `PayClaimAsync` accepts.
  → lane 3
- **B8 M** Advance: `ApprovedAmount` is uncapped; there is no reject or cancel verb; `Overdue` and
  `WrittenOff` are set by nothing; `UnsettledAmount = RequestedAmount` on create, so a merely
  requested advance reads as outstanding (and, with a deadline, overdue) in the endpoints; the approve
  dialog says *or less* and nothing enforces it. → lane 3
- **B9 M** Claim and advance numbers come from `CountByYearAsync` — live rows under the global
  soft-delete filter (`ApplicationDbContext.cs:9931`), across tenants — against an **unfiltered**
  unique index, so deleting a Draft claim or a Requested advance makes the next number collide (500).
  The request-number fix was never applied here. → lane 3
- **B10 M** The budget rollup's *Actual* counts paid claims only — a disbursed advance is cash out
  and is counted nowhere; cancellation fees are dropped; Pending, OnHold and NoShow bookings count as
  committed; the budget's `ApprovedById/At` have no writer; `ApprovedTotal` and the lines are
  caller-set. → lane 3
- **B11 M** Claim totals are in base currency, but the claim carries its own `CurrencyCode` that
  nothing reconciles, and an advance in another currency is deducted unconverted. → lane 3
- **B12 M** `HrCurrencyBridge.GetRateToBaseAsync` checks that a rate exists for the expense date, then
  converts with `ConvertAsync`, which uses **today's** rate (`CurrencyService.cs:397`) — the comment
  "valued at the rate for the expense date" is false. → lane 3
- **B13 L** Per-diem rates have no screen and no reader on claims (`IsPerDiem`, `PerDiemRateId`,
  `PolicyLimit` are decorations) and no overlap check. Deferred by D-5 (§ 6).
- **B14 L** Employee ids written into `UpdatedBy` (l.379, 487, 638, 664); pay records no actor; DTO
  fields the server ignores (`PaidAt`, `ReviewedAt`, `ApprovedById`, `SubmittedById`). → lane 3

**C. Policy** (`StaffTravelPolicy`, the guard, the service, the forms)
- **C1 M** Eight caps are read by nothing — `AdvanceBookingDaysFlight/Hotel`, `RequiresCheapestFare`,
  `PreferredVendorMandatory`, `MaxSingleTripBudget`, `MaxAnnualTravelBudget`, `ReceiptRequiredAbove`,
  `ExpenseSubmissionDays` — while `TravelPolicyForm.tsx:237-282` and `PolicyRulesPanel.tsx:55` present
  them as rules (D-1). → lanes 1, 3, 4
- **C2 M** `[Required]` on the non-nullable cabin-class enums is a no-op: an omitted class is stored as
  0 and, once the policy is approved, **every flight exceeds the cap**. → lane 4
- **C3 M** The hotel cap has no currency (T-9); `VersionNumber` is caller-declared and not unique
  (T-50); no check that `EffectiveFrom ≤ EffectiveTo` or that the level band is in order; a policy's
  author may approve it. → lane 4
- **C4 M** Deciding a policy exception needs only Write (authorising a booking breach needs Admin)
  and takes `Status` and `DecidedAt` from the body — the flow is withheld, align it anyway. → lane 4
- **C5 M** `RiskLevel.Prohibited` prohibits nothing (T-46); an alert's severity blocks nothing (T-45).
  → lanes 4, 5
- **C6 L** The shared `SelectField` clears a value that arrives before its async options
  (`fields.tsx:336-338`); on the policy form a cleared staff-level band silently widens the policy.
  → lane 4

**D. Bookings and itinerary** (`StaffTravelBookingService.cs`, `StaffTravelItineraryService.cs`, panels)
- **D1 M** Bookings are accepted on any request status; their dates are not checked against the trip;
  delete has no status guard; `Status` and `VendorId` are free on update; every booking status is
  caller-set with no transitions. → lane 5
- **D2 M** A company-vehicle leg creates a Fleet trip; edit, delete and cancel never touch it.
  → lane 6
- **D3 M** No screen can edit, cancel or change the status of a booking, add a flight segment, edit or
  delete an itinerary, leg or activity, or link a leg to its booking (`travel-bookings.service.ts`: 26
  of 40 methods uncalled) — *Committed* can never fall from the UI. → lane 5
- **D4 M** The booking dialogs never offer `vendorId` (Suppliers now load), hotel `starRating`, the
  ground leg's driver or actual cost; the vehicle is a free-text GUID box. → lanes 5, 6
- **D5 L** The mappers copy derived fields (`BookedAt`, `CancelledAt`, `NumberOfNights`, `TotalCost`,
  `PolicyAllowedClass`, `ClassExceptionApproved`) from the DTO before the service overwrites them; a
  car rental's `BookedAt` is stamped on update only; the itinerary's status, `FinalizedAt` and day
  totals are caller-set; `Superseded` is never set. → lane 5

**E. Compliance and the portal** (`StaffTravelComplianceService.cs`, panels, `/me`)
- **E1 H** No traveller can acknowledge a risk assessment: the desk route needs Write, the service
  accepts only the traveller, `/me` has no route, and the desk button 403s (T-23/T-55, finish plan
  9.19). → lane 7
- **E2 M** No screen creates a travel document (`createDocument` has no caller), so the passport-based
  visa lookup never resolves for UI data and the expiry reminders have no UI-fed rows — while the
  panel tells users to "record their passport under travel documents first". → lane 7
- **E3 M** A visa application is frozen at creation: the dialog collects status, number and dates
  that the create DTO drops (`StaffTravelDTOs.cs:2638`); the update has no caller; the list uses the
  summary DTO, so *Number* and *Fee* always read "—". → lane 7
- **E4 M** `RequiresVisa` gates nothing (T-24/T-42); health requirements are never checked (T-25);
  an expiring passport blocks nothing (T-26); `GetRequirementAsync` is not tenant-scoped, so the
  duplicate guard and the read-back can pick another tenant's row. → lanes 5, 7
- **E5 M** Updating a risk assessment re-opens the actor hole (`AssessedById` from the payload,
  `StaffTravelMappingExtensions.cs:1946`); editing a document keeps `IsVerified` (l.1724); an
  acknowledgement is not reset when the risk level rises. → lane 7
- **E6 M** The alert topic reaches the traveller **by email only**, never in the app;
  `CreateAlertAsync` notifies nobody (T-44); *Notify* sends an `employeeId` the DTO ignores. → lanes 7, 8
- **E7 M** The portal has no claims, no receipts, no attachments, no advances, itinerary, bookings,
  visas or risk; `MyTravelAlertsPanel` points travellers at "your trip's compliance tab", which `/me`
  does not have. → lane 7

**F. Notifications, reminders, seams**
- **F1 M** The five lifecycle topics have one recipient — the HR role, in-app. Nothing tells anyone
  about advances, claims, bookings, exception decisions or briefings awaiting acknowledgement.
  (Correction 2 in § 3c: the engine's own notices do reach a self-submitting traveller.) → lane 8
- **F2 M** The reminder sweep has four kinds, all to the HR role in-app; a document gets one rung at
  90 days and nothing until it expires; there is no *visa missing*, *claim overdue* or *approval
  waiting*; a delivery that fails is still logged as sent; its reads are Admin-gated (T-52); the nav
  says "trip approvals falling due", a kind that does not exist. → lane 8
- **F3 L** Cross-module, recorded: the generic inbox `approvals/{id}/process` applies no travel status
  (#15); `SendOverdueStepRemindersAsync` has no caller; Procurement's `POST /api/Suppliers` is behind
  its staged master-data guard (correction 3). → lane 10
- **F4 L** No travel query handles `isError`, so a 403 or 500 renders as "nothing yet"; option arrays
  duplicate the TS unions (how the drift happened); `'GHS'` is a fallback in twelve places; stale
  comments (no GL posting, advance recovered on approval, pay only Approved); the HR hub card promises
  per diem; *Open in Travel* passes an `employeeId` the register ignores; a withdrawn policy shows
  *Superseded*; the policy form pushes to its own URL after save. → lane 0
- **F5 L** Five enums with no reference outside their own file (`TravelApproverType`,
  `TravelApprovalDecision`, `TravelApprovalInstanceStatus`, `TravelAllowanceType`, `TravelVendorType`);
  the `StaffTravelCurrencyBridge` alias is due for retirement. → lane 0

### 3b. Found by the second review

- **O-1 H — a line manager cannot approve travel.** The seeded definition names Manager, but the
  approve route is `HR.Travel.Write`, the detail page `HR.Travel.Read`, and Manager holds neither
  (`HrPermissions.cs:862-892`); `/me/inbox` deep-links the manager to `/hr/travel/{id}`, which answers
  "does not exist". Leave met this exact defect and fixed it (`LeavesController.cs:120-173`
  `CanReadRequestAsync`; `/hr/leave/approvals`; `LeaveService.IsLineAuthorityAsync` l.3571). Travel has
  no line-authority narrowing either, and its definition is the one-step *Manager or HR, one
  signature* shape leave abandoned on 2026-09-17 (`DatabaseSeedingService.cs:590-600`). → lane 2 (D-7)
- **O-2 H — T-57 is still a money leak.** A claim filed without the advance link is paid in full while
  the advance stays outstanding; the filing page offers *No advance* even when one is outstanding.
  → lane 3
- **O-3 H — a breach's authoriser is never recorded, and D-3 lets the booker authorise.** Flight and
  hotel bookings carry only a flag and a reason (`StaffTravelEntities.cs:417-420, 556-559`); with HR
  holding Admin, the clerk who books over the cap ticks their own exception. → lane 4 (D-8)
- **O-4 H — policy supersession is date-blind.** Approving a version effective next year stands the
  sitting one down today (`StaffTravelPolicyService.cs:214-230`), and resolution requires
  `IsCurrentVersion` (`StaffTravelPolicyRepositories.cs:68`), so every trip before the new date
  resolves to no policy and no cap. → lane 4
- **O-5 M — policy scope can be chosen.** The guard resolves on `request.OrganizationUnitId`, a payload
  field (null on `/me`), with an exact match and no ancestry (l.71) — a directorate's policy does not
  cover its departments, and a requester can pick a unit with a laxer policy. → lanes 1, 4
- **O-6 M — "Paid by payroll offset" pays nobody.** `PayrollOffset` has no reader outside the posting
  factory, which posts nothing for it; the claim reads Paid and the employee is not paid. → lane 3 (D-10)
- **O-7 M — passport and visa numbers are plain text** despite *encrypted at rest* and *encrypted*
  (`StaffTravelEntities.cs:1139, 1215`; no converter anywhere in the model), and every document and
  visa DTO returns the full number to any travel reader. → lane 7
- **O-8 M — advances:** no verb records unused cash handed back; `SettlementDeadline` is optional with
  no default, so the overdue sweep never sees such an advance; no limit on several advances or their
  total against the approved budget; a typed "0" approves GHS 0. → lane 3
- **O-9 M — *Approved budget* is the estimate copied.** The approve action sends no amount
  (`hr/travel/[id]/page.tsx:104`), so the field records no decision (T-10); `request.ApprovedBudget` and
  `budget.ApprovedTotal` are two unlinked "approved" figures, and neither caps bookings or claims.
  → lanes 2, 3
- **O-10 M — no change path after approval.** The edit page says "Raise an amendment instead"
  (`[id]/edit/page.tsx:51`); no amendment exists. → lane 1 (D-9)
- **O-11 M — lifecycle edges:** cancel is allowed while InProgress; Complete before the trip starts; a
  Submitted trip whose departure passes is never escalated. → lanes 1, 8
- **O-12 M — travel is invisible to attendance.** `StaffAttendanceStatus.OnDuty` exists and the
  attendance dashboard counts it as present, but nothing writes it; leave posts its days through
  `LeaveAttendancePostingService` and travel posts nothing. → lane 9
- **O-13 M — no conflict checks.** Overlapping trips for one traveller and trips over approved leave
  are accepted; training's `NomineeAvailabilityService` and recruitment read travel as a conflict, but
  travel reads nothing back. → lanes 1, 9
- **O-14 M — leavers:** separation reads only outstanding advances (`SeparationService.cs:2312`); a
  leaver's open trips, bookings and undisbursed advances are not flagged; a request can be raised for
  an inactive employee. → lanes 1, 9
- **O-15 M — duty-of-care deletes:** risk assessments (acknowledged or not), verified passports, issued
  alerts and current itineraries delete with no status guard — reachable by every HR officer under
  D-3. → lanes 5, 7
- **O-16 M — international trips:** no check that insurance covers the trip dates; no check of the
  passport's validity against the return date (T-26). → lane 7
- **O-17 L — portal:** the traveller cannot reply to a desk comment or download their own attachment;
  a desk comment notifies nobody. → lanes 7, 8
- **O-18 L** — deleting a group leaves its participants linked to it; no status history for requests,
  claims or advances beyond the workflow tab; lookups by number and the effective per-diem read take
  the first row across tenants (single tenant today). → § 6

### 3c. Corrections the second review made to the first

1. The "dead error path" in F4 is harmless: `apiService` already puts the server's message in
   `e.message` (`api.service.ts:329-349`).
2. *The traveller is never told* overstated F1: the engine's own Submitted/Rejected/Completed notices go
   to the initiating user, who is the traveller on a self-service submission (not on a desk one).
3. A harness supplier cannot be created through `POST /api/Suppliers` — it sits behind
   `GuardDirectMutationAsync("LegacySupplier.Create")` (`SuppliersController.cs:197`); read an existing
   active supplier instead.
4. The guide's T-28 (*cancel has no server guard*) is wrong — Cancelled, Completed and Closed are
   refused. The real gaps are A2 and O-11.
5. D-3's safety net needed D-8 and the delete guards of O-15; it was incomplete as first written.
6. Approver notifications (lane 8) are useless until the approver can open the request (lane 2).
7. The claim-filing page says the advance is recovered "when the claim is approved"
   (`claims/new/page.tsx:168`); it is recovered on payment.

⚠ **For the leave owner, not travel scope:** `LeaveService.EnsureMayDecideAsync` (l.1000) asks only the
engine, whose stage 1 is the Manager *role*, so any Manager-role user appears able to decide any
leave at stage 1 — the line-authority rule exists (l.3571) but is not applied to the decision.
Travel applies the line rule in its service (lane 2) rather than copying leave's approve path.

### 3d. The guide's T-findings — where each one now lives

| Status | Findings |
|---|---|
| **Fixed upstream — correct the prose only** | T-5 and T-37's conversion half (Finance FX fixed 2026-09-10, PR #99), T-8's supplier read (2026-09-22), T-43 (alert body), T-53 (the sweep has been hosted daily since 2026-08-17 — the guide was wrong when written), T-58 (claims and advances post since 2026-09-20), T-44's per-trip button (it exists) |
| **Kept by decision** | T-4 and T-49 (D-29), T-6 (recovery on payment — the copy is fixed in lane 3), T-51 (informational) |
| **Lane 0** | T-3, T-12, T-15, T-27, T-29 (with lane 1) |
| **Lane 1** | T-7 (with lane 8), T-16, T-17, T-28 (corrected), T-30, T-31, T-32 |
| **Lane 2** | T-10 |
| **Lane 3** | T-21, T-22, T-35, T-36, T-37, T-38, T-39, T-57 |
| **Lane 4** | T-1 and T-2 (D-3), T-9, T-46, T-50, T-52 |
| **Lane 5** | T-19, T-24/T-42 (ticketing half), T-45 (a warning) |
| **Lane 6** | T-8's vehicle half |
| **Lane 7** | T-23/T-55, T-24/T-42 (derived half), T-25, T-26, T-40, T-44, T-54, T-56 |
| **Deferred (§ 6)** | T-11, T-13, T-14, T-18, T-20, T-33, T-34, T-41, T-47, T-48 |

### 3e. Fleet (the seam travel already calls)

**What exists.** A company-vehicle ground leg calls `IFleetTripService.CreateTripAsync`
(`StaffTravelBookingService.cs:462-497`) and keeps the trip id as a bare Guid. Fleet's own service
interfaces (`IFleetServices.cs`) already offer everything a deeper seam needs — trip read, update,
submit and cancel; compliance items blocking a vehicle as at a date; costs per trip; fuel transactions;
vehicle assignments; incidents — so travel integrates without changing Fleet's code, the way it creates
the trip today.

- **FX-1** "This reserves the vehicle in Fleet" (`TravelBookingsPanel.tsx:445, 471`) is false: the
  fleet trip is created **Draft** (`FleetTripService.cs:180`) and never submitted, so the transport
  office has nothing to approve and nothing is held. → lane 6
- **FX-2** Fleet's only conflict check looks at **Dispatched** trips (`FleetTripService.cs:885-899`), so
  two legs — or a leg and a fleet booking — can hold one vehicle or driver for the same days. → lane 6
  (travel side), Fleet hand-off
- **FX-3** The leg keeps only the trip id: vehicle, plate, driver and fleet status never show on the
  travel record; the update DTO cannot change vehicle or driver; edit, delete, a type change away from
  CompanyVehicle and a request cancel leave the fleet trip live (D2). → lane 6
- **FX-4** The vehicle is a raw asset GUID because Fleet's reads need `MaintenanceRead`
  (`FleetTripsController.cs:10`), which HR lacks; the screen says "a vehicle picker arrives with the
  Fleet integration" (`TravelBookingsPanel.tsx:472`). → lane 6
- **FX-5** Vehicle compliance (insurance, roadworthiness) is checked only at dispatch
  (`FleetTripService.cs:484`): a vehicle whose insurance lapses before the trip can be booked. → lane 6
- **FX-6** Fleet records fuel and cost per trip (`FleetFuelTransaction.FleetTripId`,
  `FleetCostEntry.FleetTripId`, `IFleetCostService.GetPagedForTripAsync`), but travel's budget takes the
  leg's hand-typed cost, and a Fuel claim line is never compared with Fleet's fuel log — the same fuel
  can be paid twice. → lane 6
- **FX-7** A driver is checked for a licence only — not for approved leave or another trip (Fleet's own
  tracker, DRV-006) — and a driver who goes out of station gets no travel record (allowances,
  attendance, duty of care). → lane 6, Fleet hand-off
- **FX-8** `FleetIncident.FleetTripId` exists; an accident on a staff trip never reaches the travel
  request or its approver. → lane 6
- **FX-9 (Fleet's own)** No fleet-trip approval definition is seeded (a catalogue entry only), and
  Fleet's submit does not use HR's no-definition guard, so a submitted fleet trip auto-approves; with
  `RequirePredefinedFleetTripDestinationOnDispatch` on, a travel-created trip cannot be submitted at
  all. → Fleet hand-off (lane 10); lane 6 handles the destination setting on its side

---

## 4. Lanes

### Lane 0 — Harness on UAT, baseline, truth and fiction (no schema)

**The harness first.** `dev-harness\hr-travel\setup.mjs` cannot mint actors on UAT as written:
- [ ] send `employeeNumber: ''` — the staff-number rule auto-generates and refuses a supplied number
  (copy `hr-performance/setup.mjs:49`);
- [ ] resolve the tenant from `/api/auth/me`, not the hard-coded `…0001`;
- [ ] pick the first position that has an `organizationUnitId`;
- [ ] track minted user ids and switch them off in a `finally` (performance's `mintedUserIds`);
- [ ] replace the SQL-seeded supplier in `fixtures.json` with an existing active supplier read from
  the tenant (correction 3);
- [ ] `workflow-definition.mjs` must not publish a second `StaffTravelRequest` definition beside the
  seeded one — read the seeded definition and assert on it;
- [ ] `clamd-stub.mjs` for every upload suite; write the missing `hr-travel/README.md` (environment,
  suites, counts, teardown rule);
- [ ] run the sixteen existing suites on UAT and record the baseline — list the stale assertions, do
  not bend a suite to pass.

**Frontend truth (A7, A8, A9, F4, F5).**
- [ ] Every travel union written from its C# enum (`frontend/src/types/hr/travel*.ts`) and the option
  arrays derived from them — `TravelRequestForm.tsx`, `TravelAttachmentsPanel.tsx`, the register's type
  filter; one `TravelRiskLevel` export.
- [ ] Comment type `Comment` / `InternalNote` with an internal-note toggle in the composer; attachment
  types from the enum; the attachment delete button only for Admin.
- [ ] The request form sends `policyId`, `groupTravelId` and `approvedBudget` back on edit until lane 1
  takes them out of the update DTO.
- [ ] `isError` and one shared error state on every travel query; stale comments and strings (GL, FX,
  pay statuses, the per-diem card, the nav descriptions, the portal's "compliance tab").
- [ ] Backend: stale FX comments; retire `StaffTravelCurrencyBridge` onto `HrCurrencyBridge`; delete the
  five dead enums; `StaffTravelBusinessRulesAttribute` maps `DbUpdateException` to 409 with a sentence.

Suite `run-final-truth.mjs`: a UI-payload probe per screen; every enum member round-trips.

### Lane 1 — Request lifecycle (A1–A6, A10–A12, D-6, D-9, O-5, O-10, O-11, O-13, O-14, T-16, T-17)

- [ ] **Edit** only in Draft and ReturnedForRevision, on the desk and `/me`. The update DTO loses
  `ApprovedBudget`, `PolicyId` and `GroupTravelId` (group membership moves to the group endpoints).
- [ ] **Server-derived facts:** `IsInternational` from the two countries on create and update; the
  organisation unit from the traveller's employee record; every FK tenant-validated (employee, unit,
  group, parent). Create refuses an inactive or separated traveller.
- [ ] **Submit preconditions:** cost > 0; return ≥ departure; departure ≥ today (the desk may override
  with a reason); a currency Finance holds; `MaxSingleTripBudget` of the applicable approved policy
  (D-1) — the 422 names the cap; a trip overlapping another Submitted/Approved/InProgress trip of the
  same traveller is refused; an overlap with approved leave is a warning; `SubmittedAt` from the clock.
- [ ] **The request form shows the policy that will apply and its caps** (a read that resolves the
  guard for a traveller and a date — T-16).
- [ ] **Cancel:** cancels the engine instance while Submitted; refused once InProgress; from Approved
  it requires no disbursed advance with money outstanding (the 422 names it) and cancels pending and
  confirmed bookings and undispatched fleet trips (lanes 5, 6).
- [ ] **D-6 writers.** `ReturnForRevisionAsync` (the approve gate) on leave's suggest-changes shape
  (`LeaveService.cs:1163-1177`): `HasActiveApprovalWorkflowAsync` → `CancelWorkflowAsync` → check
  `.Success` → set the status directly (the engine has no *returned* outcome), stamping
  `ReturnedAt/ById/Reason`. `CloseAsync` (HR), `ClosedAt/ById`; the sweep closes too (lane 8) when every
  claim is Paid or Rejected and every advance FullySettled, refunded or written off, or when no claim
  was filed by `TravelEndDate + ExpenseSubmissionDays`. Nothing is booked, advanced or claimed on a
  Closed trip. Complete only on or after `TravelStartDate`.
- [ ] **D-9 Request change** (traveller, desk or approver; Approved only): returns the trip to
  ReturnedForRevision with a reason (`ChangeRequestedAt/ById`, `ChangeReason`); bookings, advances and
  claims stay linked; resubmission re-enters the two-stage ladder. The edit page's "raise an amendment"
  text becomes this button.
- [ ] **Recall** — service, `/recall` and `/me/recall`: the requester-only check in the service first
  (the helper does not enforce it), then `HrWorkflowFallbackAuthority.RecallAsync`, then
  `ApplyRecallOutcome` whatever the helper returns. This completes "all four or none".
- [ ] **`ApprovedById`** (new column) is the final approver's employee id, stamped in `ApproveAsync`; an
  instance completed through the generic inbox never passes the service, so it stays null there
  (recorded under #15).
- [ ] Comment edit and delete by their author (or Admin); `CancelledAt` from the clock; the `/me`
  detail filters comments by `IsVisibleToTraveller` on the server and drops policy exceptions.
- [ ] **Groups:** `MaxParticipants` enforced; a change of the group's dates or destination propagates
  to Draft participants; `POST groups/{id}/requests/{requestId}` links an existing Draft request; the
  group's status moves by verb (open, close, cancel), not by PUT; deleting a group detaches its
  participants; the add-traveller dialog takes purpose, risk, visa and currency.

### Lane 2 — The approval ladder and the approver's door (D-7, O-1, O-9, T-10)

- [ ] **Seeder:** STAFF_TRAVEL_REQUEST leaves the one-step list and is seeded through
  `EnsureSequentialWorkflowDefinitionSeededAsync` as leave is — stage 1 Manager + TenantAdmin, stage 2
  HR + TenantAdmin — deactivating the superseded one-step row so no tenant holds two active
  definitions. `PreventInitiatorApproval` stays false; the traveller check is the control.
- [ ] **Service:** before the engine, approve, reject and return refuse a stage-1 decision by anyone
  who is not the traveller's line authority — leave's `IsLineAuthorityAsync` extracted into a shared HR
  helper, not the leave service injected — unless the traveller has no line authority with a login,
  when HR decides stage 1 and the record says why.
- [ ] **Controller:** approve, reject and return move from `TravelWritePolicy` to `InternalOnly` with
  the service's checks (leave's shape, `LeavesController.cs:847-858`); a read door — Read OR line
  authority OR engine assignee (`LeavesController.cs:138-154`) — on the request, its comments and its
  attachments; `GET requests/my-approvals` asks the engine (`LeavesController.cs:759`).
- [ ] **Frontend:** a `/hr/travel/approvals` queue, added to `sidebar-hr-gates.test.ts`'s KEEP_OPEN list
  with its reason; the detail page renders for an approver with no travel permission (desk-only tabs
  hidden), so `/me/inbox`'s link opens; the approve dialog takes an approved budget, prefilled with
  the estimate.

Suite `run-final-approvals.mjs`: the traveller's manager approves stage 1; an unrelated manager is
refused; HR approves stage 2; a traveller with no manager goes to HR; the inbox link opens.

### Lane 3 — The money chain (B1–B12, B14, D-2, D-10, O-2, O-6, O-8, O-9, T-21, T-22, T-35–T-39, T-57)

- [ ] **Claim state machine.** Submit: Draft or Returned → Submitted; the request Approved, InProgress
  or Completed; at least one line; every line above `ReceiptRequiredAbove` carries a receipt
  attachment; filed within `ExpenseSubmissionDays` of `TravelEndDate` or the 422 names the date.
  Review: only from Submitted or UnderReview, only to UnderReview, Approved, PartiallyApproved,
  Rejected or Returned; Approved versus PartiallyApproved is computed from the lines; nothing approved →
  "review the lines first". Pay: only Approved or PartiallyApproved with `TotalApproved > 0`;
  `PaidById` stamped; Paid is terminal. Lines are frozen from Submitted except when Returned.
- [ ] **D-2:** reviewer and payer ≠ the claim's employee; payer ≠ the reviewer; advance approver ≠ the
  employee; disburser ≠ the employee and ≠ the approver — a 403 with the sentence.
- [ ] **Parents (B3):** the advance must belong to the same request and employee; the claim's and
  advance's employee is the request's traveller (server-set); a receipt is an attachment of the same
  request; claims and advances only on Approved, InProgress or Completed requests.
- [ ] **O-2 (T-57):** pay refused while the traveller holds a disbursed advance on the same request
  that the claim does not name, unless the payer records a waiver reason; the filing page preselects
  the outstanding advance, and its copy says recovery happens on payment.
- [ ] **Valuation (B5, B11, B12):** inline lines valued; the claim's currency server-set to base;
  `GetRateToBaseAsync` returns the dated row's rate (direction per PR #99's contract, asserted against
  `GET /api/finance/exchange-rates/current/USD`); an advance in another currency converted before it
  is deducted.
- [ ] **Advances:** `0 < ApprovedAmount ≤ RequestedAmount`; the total approved on a request ≤ its
  approved budget; no new advance while the traveller has an Overdue one; reject and cancel verbs
  (`TravelAdvanceStatus.Rejected = 8`, `Cancelled = 9`); `UnsettledAmount` 0 until disbursed; the
  outstanding and overdue reads filter status; `SettlementDeadline` defaults to `TravelEndDate +
  ExpenseSubmissionDays` (or 30 days); `Overdue` set by the sweep; `WrittenOff` by an Admin verb with a
  reason; **`RecordRefundAsync`** (amount ≤ unsettled, reference, actor ≠ traveller) settles unused
  cash handed back and posts through the same adapter as a new posting event (the Finance owner told).
- [ ] ⚠ Flip the posting retry guard at `HrFinancePostingAdminService.cs:655` from the negative list
  to `is not (Disbursed or PartiallySettled or FullySettled or Overdue)`, or a rejected advance could
  be re-posted; regenerate the `TravelAdvanceStatus` TS union.
- [ ] **Numbers (B9):** max-based, tenant-scoped generators for claims and advances (the shape of
  `GenerateRequestNumberAsync`), with filtered unique indexes (§ 5).
- [ ] **Budget (B10, O-9, T-22):** Actual = paid claims' `NetPayable` + disbursed advances; Committed
  excludes NoShow and adds cancellation fees; the budget's lines sum to `ApprovedTotal`, which defaults
  to and may not exceed the request's approved budget; the budget's currency is the request's;
  `budgets/{id}/approve` on `TravelAdminPolicy` stamps `ApprovedById/At` (approver ≠ traveller),
  shown on the card; the Finance tab shows committed and actual against the approved budget and flags
  an overrun (whether an overrun refuses is a TDC question, § 6).
- [ ] **D-10:** payroll offset leaves the pay dialog and is refused by the API for new payments.
- [ ] **T-39:** an Admin *void payment* (a second person, a reason) reverses the payment and the advance
  settlement it made, and asks the posting register to reverse `TravelClaimPaid`. No code in
  `Services/HR/Finance` calls back into a claim after a Finance reversal, so a reversed posting row
  stays retryable as it is.
- [ ] B7: *awaiting payment* includes PartiallyApproved. B14: user ids in `UpdatedBy`; the ignored DTO
  fields removed.
- [ ] **Frontend:** a line review takes an amount and a typed reason; the advance approve dialog
  prefills and caps; the overdue-settlements queue (D-5); the posting card's wording.

Suite `run-final-money.mjs`: every H above as a two-actor assertion; pay twice → 422; review a Paid
claim → 422; a claim naming another employee's advance → 404; a zero-line approval → 422; inline lines
valued; a back-dated line valued at its date's rate; a deleted claim does not collide; a reversed
`TravelClaimPaid` leaves the claim Paid and a retry re-posts the same amount.

### Lane 4 — Policy and authority (C1–C6, D-1, D-3, D-8, O-3, O-4, O-5, T-1, T-2, T-9, T-46, T-50, T-52)

- [ ] **D-3 in four steps:** add `AdministerTravel` to `HrStaffGrants` (`HrPermissions.cs:687`;
  precedent `AdministerRecruitment`, l.693); the seeder's grant loop is add-only and runs every
  startup, so restarting the UAT API converges existing tenants (`RoleRevocations` is not touched);
  the suite asserts the `hr` actor passes an Admin route, and `mintTravelAdminActor` is retired;
  update the remarks that describe the travel tier (`HrPermissions.cs` ~l.280, 584, 611;
  `StaffTravelMeController.cs:186`). The UI's `canAdmin` drops its `HR_ADMIN_ROLES` fallback.
- [ ] **D-8:** flight and hotel bookings gain `ExceptionState` (None, Pending, Authorised, Refused),
  `ExceptionRequestedById` and `ExceptionAuthorisedById/At`. An over-cap booking is saved Pending and
  cannot be Confirmed or Ticketed until a **different** Admin holder authorises it; the
  `AdvanceBookingDays` override takes the same path; a breach register under Staff Travel lists every
  over-cap booking.
- [ ] **D-1:** `AdvanceBookingDays*` at flight and hotel create; `PreferredVendorMandatory` → a Supplier
  is required (picker on the four booking dialogs, tenant-validated); `RequiresCheapestFare` and
  `MaxAnnualTravelBudget` leave the DTOs and the form; the form and `PolicyRulesPanel` say exactly what
  binds, and the rules panel keeps its read-only, non-binding banner (D-29).
- [ ] **C2, C3:** cabin classes validated as enum members; a policy `CurrencyCode` (the hotel cap
  compared in it, a booking in another currency converted through the bridge); `VersionNumber`
  server-assigned (max + 1 per policy name) with a filtered unique index; `EffectiveFrom ≤ EffectiveTo`;
  the level band in rank order; author ≠ approver.
- [ ] **O-4:** approving a future-dated version sets the sitting version's `EffectiveTo` to the day
  before and keeps both in force for their own windows; resolution picks by date.
- [ ] **O-5:** the guard resolves the unit from the traveller, not the request, and walks the unit's
  ancestry (`UnitAncestryAsync`), most specific first.
- [ ] **C4:** deciding an exception → Admin, `DecidedAt` from the clock, decider ≠ requester (still no
  screen). **C5:** `RiskLevel.Prohibited` refuses submit; `Critical` needs an acknowledged risk
  assessment before departure.
- [ ] **C6:** one shared line in `SelectField.onValueChange` (`fields.tsx:336-338`): `if (next === '')
  return;` before `form.setValue`. Safe: the only legitimate clear is `NONE_VALUE → ''`, and no HR
  option array declares `value: ''` (750 uses in 208 files). Scoped type-check afterwards.

Suite `run-final-policy.mjs` (and re-run `run-slice12-policy-authoring.mjs`).

### Lane 5 — Bookings and itinerary (D1, D3–D5, E4, O-15, T-19, T-24/T-42, T-45)

- [ ] Bookings only on Approved or InProgress requests; booking dates inside the trip (± 1 day); status
  by verb — confirm, ticket, cancel (with fee), complete — not by PUT; status verbs never re-run the
  cap check (cancelling an authorised over-cap booking must work); delete only while Pending; the
  mappers stop copying derived fields; a car rental's `BookedAt` on create.
- [ ] A flight cannot be Ticketed while the request requires a visa and no visa application is
  Approved or NotRequired.
- [ ] An active Critical or Emergency alert for the destination shows on the approval and booking
  screens (a warning; a block is a TDC question).
- [ ] Itinerary: delete only a non-current version; `Superseded` set when a version is replaced.
- [ ] **Doors (D-5):** edit, cancel and status on every booking row; flight segments; itinerary, leg and
  activity edit and delete; the leg ↔ booking link; the vendor picker, star rating, actual cost.

Suite `run-final-bookings.mjs` (and re-run `run-slice8*.mjs`).

### Lane 6 — Fleet (D-11, D-12, D2, D4, FX-1…FX-9)

- [ ] **Pickers through travel's own controller** (HR needs no Maintenance permission): active fleet
  vehicles with plate, current assignment, the compliance items blocking them on the trip dates
  (`IFleetComplianceService.GetDispatchBlockingItemsAsync(vehicle, date)`) and their planned overlaps
  (Draft, Submitted, Approved or Dispatched fleet trips in the window); drivers with a valid licence,
  flagged when on approved leave or another trip. The traveller's active Primary fleet assignment
  preselects its vehicle and driver.
- [ ] **A real reservation:** on an Approved request the leg's fleet trip is submitted to Fleet's
  approval (`SubmitForApprovalAsync`); travel refuses a vehicle or driver with an overlapping planned
  trip (a read-only check on Fleet's trips) and a vehicle whose critical compliance item expires before
  the return; when Fleet requires a predefined destination, the leg offers Fleet's destination
  templates.
- [ ] **Kept in step:** the leg's status is read from the fleet trip (Submitted → Pending, Approved →
  Confirmed, Dispatched → in use, Completed → Completed, Rejected or Cancelled → Cancelled, with
  Fleet's reason shown); leg edits go through `UpdateTripAsync` while Fleet allows it, otherwise cancel
  and rebook; leg delete, a type change, a request cancel and *Request change* cancel undispatched fleet
  trips. **No copies of Fleet's facts** — vehicle, driver and status are read from the fleet trip.
- [ ] **Shown on the leg:** vehicle, plate, driver, fleet status, dispatch and return times, distance
  from the start and end mileage. Fleet's costs for the trip feed the budget rollup for that leg.
- [ ] **Fuel on claims:** claim lines gain `FleetTripId`, `FuelQuantity` and `FleetFuelTransactionId`;
  a Fuel line on a request with a company-vehicle leg names one of its fleet trips; on **payment**
  travel records it through `IFleetFuelService.CreateAsync` (vehicle, trip, quantity, cost, merchant,
  receipt) and keeps the id; a voided payment deletes it; a Fuel line on a date Fleet already holds fuel
  for that trip asks for a reason; the rollup counts a claim-created fuel cost once, as the paid claim.
- [ ] **Drivers as travellers:** when a leg keeps the driver away overnight (drop-off on a later date,
  or a destination outside the origin city), the desk is asked to raise the driver's own request — a
  Draft linked to the trip through its group record (created if none exists, the traveller as lead),
  same dates and destination, purpose "driver — company vehicle for TR-…" — which goes through the same
  two-stage approval, so allowances, attendance and duty-of-care alerts cover the driver. The leg keeps
  `DriverTravelRequestId`; cancelling the leg or the trip cancels the driver's request.
- [ ] **Incidents and signals:** the sweep reads fleet incidents carrying a fleet trip of an open
  travel request (read-only), shows them on the request's Compliance tab and tells the travel desk and
  the traveller's line authority once per incident; Fleet's dispatch of the outbound leg moves an
  Approved trip to InProgress ahead of the date rule; Fleet's completion of the return leg tells the
  desk to mark the trip completed.

Suite `run-final-fleet.mjs` — a fixture vehicle with a verified-licence driver (created and retired by
the suite if Fleet allows; real vehicles are read, never written): the fleet trip reaches Fleet's queue
as Submitted; a second leg on the same vehicle and days is refused; a vehicle insured only until before
the return is refused; a request cancel cancels the fleet trip; Fleet's rejection shows on the leg; a
paid fuel line lands in Fleet's fuel log once and the budget counts it once; the driver's request is
raised, approved and cancelled with the leg; a fleet incident reaches the desk once.

### Lane 7 — Compliance and the portal (E1–E7, D-5, O-7, O-15, O-16, O-17, T-23–T-26, T-40, T-44, T-54–T-56)

- [ ] **E1:** `/me/risk-assessments/{id}/acknowledge` and a portal screen; the desk button becomes a
  read-only state.
- [ ] **E2:** a travel-documents register (`/hr/travel/documents`: list, create, edit, verify, delete,
  an expiring filter) and `/me/travel/documents` for the traveller's own passport; `IsVerified` reset
  on edit; one primary document per type per employee.
- [ ] **E3:** the visa-application create DTO takes status, number, dates and fee; the edit dialog
  wired; the list typed from the summary DTO, with a detail read for the drawer; `GetRequirementAsync`
  tenant-scoped.
- [ ] **E4:** `RequiresVisa` derived from the visa-requirement register when the traveller's passport
  country is known (an override needs a note); health requirements shown on the request with a
  *cleared* tick each (no block); a requirement not verified for twelve months shows as stale (T-40).
- [ ] **E5:** a risk update ignores `AssessedById`; an acknowledgement is reset when the risk level
  rises.
- [ ] **E6:** `CreateAlertAsync` fans out one notification per Approved or InProgress request to the
  country within the alert's window (the button's path), and the traveller gets it in the app.
- [ ] **O-7:** passport and visa numbers masked (last four) in every list and summary read and on the
  request's screens; the full number only on the document's own detail; the false *encrypted* comments
  corrected; column encryption recorded as a platform item (§ 6).
- [ ] **O-15:** delete refused for an acknowledged risk assessment, a verified document, an alert that
  has notifications (deactivate it instead).
- [ ] **O-16:** an international trip with no insurance spanning its dates gets a warning at submit and
  refused ticketing; a primary passport expiring within six months of the return gets a warning (both
  windows to be confirmed by TDC).
- [ ] **Portal (D-5, E7, O-17):** `/me/claims` — create, lines, receipt upload through the controlled
  gate, submit, status; the traveller's advances, itinerary, bookings, visas and risk assessment;
  `/me` attachment upload and download; a reply to a desk comment. Upload precedent
  `MyProfileController.cs:138-173` (entitlement first — the traveller's own request or 404 through
  `GetOwnActiveRequestAsync` — then `HrAttachmentUpload.ExecuteAsync` with
  `ControlledFileUploadCategories.HrStaffTravelAttachments`); `StaffTravelMeController` gains
  `IHrControlledDocumentService` and a logger. `MyTravelAlertsPanel` stops pointing at a tab `/me`
  does not have.

Suites `run-final-compliance.mjs` and `run-final-portal.mjs` — every portal check runs as a plain
employee (HR passes its own guards); the upload category requires a clean scan, so both need the
clamd stub.

### Lane 8 — Notifications and the sweep (D-4, D-6, F1, F2, E6, O-11, O-17)

- [ ] **Topics per event and audience** (leave's shape). Traveller: submitted (acknowledgement),
  approved, rejected, returned, change requested, cancelled by the desk, advance approved, advance
  disbursed, claim returned, claim approved, claim paid, alert issued, briefing to acknowledge, desk
  comment, document expiring, visa expiring, trip departing. Approver (`UsersFromData` from the
  engine's pending approvers): request waiting — the link opens through lane 2's door. HR and the
  travel desk: claim submitted, advance requested, settlement overdue, traveller comment, approval
  waiting with nobody else to ask, fleet incident (lane 6). Traveller and approver topics send email.
- [ ] **Legacy topics** deactivated as `LeaveReminderService.cs:468-499` does — a `LegacyTopicKeys`
  array; `IsActive = false` and a replacement description on `IsSystem` rows nothing publishes to any
  more: `StaffTravelReminder.DueSoon.Internal`, `StaffTravelReminder.Overdue.Internal`, and the five
  `StaffTravelRequest.{Activity}.Internal` keys unless the HR-desk audience keeps them.
- [ ] **Sweep kinds added:** visa missing (an approved trip within 14 days that requires a visa and has
  none approved); claim overdue (`ExpenseSubmissionDays` passed, no claim); approval waiting (a
  Submitted trip waiting more than N days, escalating to HR when departure is three days away or
  past); briefing unacknowledged;
  passport rungs at 90, 30 and 7 days. **D-6 transitions:** Approved → InProgress on departure (or on
  Fleet's dispatch, lane 6); Completed → Closed when settled (lane 1's rule); advance → Overdue.
- [ ] A dispatch row records `PublishedAt` only when the bus call returned without throwing — the bus
  swallows handler errors, so delivery is proved by counting the notification rows written per
  audience, never by reading the log.
- [ ] The reminders page reads for HR (D-3); the nav description says what the sweep chases.

Suite `run-final-reminders.mjs` (the SMTP-sink pattern; assert the rows written per audience) and
re-run `run-slice5a.mjs`.

### Lane 9 — Cross-module touchpoints (O-12, O-13, O-14, D-10)

- [ ] **Attendance:** a `TravelAttendancePostingService` on the model of
  `LeaveAttendancePostingService` — an approved trip posts its working days as `OnDuty` (the enum value
  that has never had a writer); *Request change* and cancel reverse or re-post; an existing punch is
  never overwritten.
- [ ] **Leave:** a leave submission warns when the employee has an approved trip on those days, through
  leave's existing warning path.
- [ ] **Separation:** the clearance lists a leaver's open trips, bookings and undisbursed advances (it
  lists outstanding advances already); the separation's approval cancels Draft and Submitted trips.
- [ ] **Payroll:** the D-10 hand-off; the payroll-offset row in the cross-module defects document.

Suite `run-final-touchpoints.mjs`: attendance rows written and reversed; the leave warning; the
separation lists.

### Lane 10 — Docs, demo pack, harness, hand-offs

- [ ] `HR-STAFF-TRAVEL-SYSTEM-GUIDE.md`: rewrite the six rules above chapter 1 (T-1 and T-2 fall with
  D-3, T-3 is fixed, T-5 is stale, T-6 stays) and every chapter a lane changed; § 19 points here.
- [ ] `docs/HR/README.md`, `HR-FINISH-PLAN.md` (lane 10's travel sweep, lane 9's travel-policy door,
  row 9.19, D-29), `HR-FINANCE-INTEGRATION-BACKLOG.md` (the FX note, the refund event).
- [ ] `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`: #15 lists travel among the unprotected entities; the
  Fleet hand-offs (planned-window conflicts, driver leave and trip availability at dispatch — offer HR's
  availability read — a seeded fleet-trip approval definition with the no-definition guard, an
  incidents-by-trip read); the payroll-offset row.
- [ ] The demo pack (`dev-harness\hr-demo-smoke\scenarios\080-travel.mjs`, `081-travel-logistics.mjs`):
  `hr.head` approves the policy (D-3), the London trip's over-cap hotel is authorised by a second
  officer (D-8), a claim is filed with a receipt; the verify-tables gate covers the new rows.
- [ ] The travel harness README with its regression count; each of the sixteen old suites re-pointed
  or retired where a rule changed, naming the lane that changed it.
- [ ] Memory: the two travel memories updated; the closure memory records each lane's state.

---

## 5. Migration batch 1

**House rules** (the performance closure's § 6): every statement guarded (`IF COL_LENGTH(...) IS NULL`,
`IF NOT EXISTS (SELECT 1 FROM sys.indexes …)`); data operations as raw SQL; a new non-nullable column
carries its default in the `ADD`; UAT is built from empty and then seeded, so a backfill may touch no
rows there and must also be safe on a populated database; the model snapshot is regenerated, never
hand-merged; the data part is dry-run on the target first and applied on the user's go.

**Columns** (all nullable unless stated):
- `StaffTravelRequests`: `ApprovedById`, `ReturnedAt`, `ReturnedById`, `ReturnReason`, `ClosedAt`,
  `ClosedById`, `ChangeRequestedAt`, `ChangeRequestedById`, `ChangeReason`.
- `StaffTravelExpenseClaims`: `PaidById`, `AdvanceWaiverReason`, `PaymentVoidedAt`, `PaymentVoidedById`,
  `PaymentVoidReason`.
- `StaffTravelExpenseClaimLines`: `FleetTripId`, `FuelQuantity` (decimal(10,2)), `FleetFuelTransactionId`.
- `StaffTravelAdvances`: `RejectedAt`, `RejectedById`, `RejectionReason`, `WrittenOffAt`,
  `WrittenOffById`, `WriteOffReason`, `RefundedAmount` (decimal(14,2), **default 0 in the `ADD`**),
  `RefundedAt`, `RefundedById`, `RefundReference`.
- `StaffTravelFlightBookings` and `StaffTravelHotelBookings`: `ExceptionState` (int, **default 0 =
  None in the `ADD`**), `ExceptionRequestedById`, `ExceptionAuthorisedById`, `ExceptionAuthorisedAt`.
- `StaffTravelGroundTransports`: `DriverTravelRequestId`.
- `StaffTravelPolicies`: `CurrencyCode` char(3) — nullable, because the base currency is per tenant
  and no literal default fits; backfilled per tenant from the tenant's base currency, and the guard
  falls back to base when it is null.
- `StaffTravelReminderDispatchLogs`: `PublishedAt`.
- Enum members, int-stored, no schema: `TravelAdvanceStatus.Rejected = 8`, `Cancelled = 9`; a new
  `TravelBookingExceptionState`.

**Indexes:** the claim-number and advance-number indexes **already exist unfiltered**
(`ApplicationDbContext.HR.cs:14765`, `:14778`), so this is a replacement — a guarded `DROP INDEX`, then
`CREATE UNIQUE INDEX … WHERE [IsDeleted] = 0`, the model shape of `:967` / `:2781`
(`.HasFilter("[IsDeleted] = 0")`). A new filtered unique index on `StaffTravelPolicies (TenantId,
PolicyName, VersionNumber)`.

**Data** (raw SQL; counted read-only first, applied on the user's go): `IsInternational` recomputed
from the two countries for live requests; `UnsettledAmount` zeroed for Requested and Approved advances;
a claim's `CurrencyCode` set to the tenant's base where it differs; a policy's `CurrencyCode` set to
the tenant's base.

Attendance posting needs no schema — it writes the same `StaffDailyAttendance` rows leave's posting
does, with a reason. Vehicle and driver are never copied onto travel rows — they are read from the
fleet trip.

---

## 6. Out of scope — recorded, not built in this closure

| Item | Why it waits |
|---|---|
| Register and claims-queue paging, search and filters; the two exports (T-11, T-13, T-14, T-33, T-34) | Recommended as the **first follow-up**; no rule depends on them |
| Ground-transport and car-rental caps (T-18) | Needs new policy fields and TDC's numbers |
| Per-category budget enforcement (T-20) | Product decision; the total is enforced (lane 3) |
| Reverse visa view (T-41); dashboard date range and actual spend (T-47, T-48) | Reporting, not control |
| Per-diem screen and per-diem claim lines (B13) | D-5 |
| Policy-exception screen and rule enforcement | D-29 |
| A status-history timeline for requests, claims and advances; cross-tenant first-row lookups by number (O-18) | Single tenant today; the workflow tab covers approval history |
| Generic inbox desync (#15); Procurement's supplier create (#1) | Other teams' modules |
| Budget consumption against Finance (backlog 12.4, 12.5) | Needs the budget-commitment conversation with Finance |
| Paying claims through payroll (O-6) | Payroll is another developer's module — D-10 hides the option until it can receive them |
| Column encryption of passport and visa numbers (O-7) | No encryption mechanism exists in the model; masking ships in lane 7 |
| Concurrency tokens on travel entities | A platform item |
| **TDC questions:** reminder windows (90/14/90 days); the insurance and passport windows of O-16; whether an approved-budget overrun refuses or only warns (O-9); whether a Critical or Emergency alert blocks booking (T-45) | Recorded in `HR-OPEN-QUESTIONS-FOR-TDC.md` by lane 10 |

---

## 7. Verification

- Each lane's suite green **twice** on UAT, then the full travel regression (the sixteen existing
  suites as lane 0 re-pointed them, plus the new ones), its count recorded in the harness README.
- **Actors:** an HR officer; a second HR officer (D-2, D-8); a plain employee (every portal check); the
  traveller's own line manager **and** an unrelated manager (D-7 — one must succeed, one must be
  refused); `admin` only to mint fixtures.
- **Second-review proofs:** a future-dated policy approval leaves today's trips capped (O-4); a
  self-service request cannot choose a laxer unit's policy (O-5); a claim with an unlinked outstanding
  advance is refused at pay (O-2); a refunded advance settles and drops off the overdue sweep (O-8);
  payroll offset is refused (D-10); an approved trip writes `OnDuty` days and a cancel removes them
  (O-12); every list read masks the passport number (O-7).
- **Manual walk** on the demo database after the demo-pack change: the guide's 25-minute short path,
  plus the new end-to-end path — an employee raises a trip in the portal → their manager approves →
  HR approves → the desk books (a company vehicle reaches Fleet's queue) → the employee files a claim
  with a receipt → one HR officer reviews and a second pays → the advance is recovered → the sweep
  closes the trip.
- **Frontend:** a scoped `tsconfig` type-check of the travel files (the full type-check crashes) and a
  UI-payload probe for every form a lane changes.

---

## 8. Status at HEAD `bad482a8d` (before any code of this plan)

No lane has started. The travel harness's last green runs predate UAT (August 2026, the dev
database); lane 0 records the first UAT baseline. The demo database holds the four demo trips of
`080-travel.mjs` and `081-travel-logistics.mjs`, with the 2026 policy unapproved.

---

## 9. Log

- **2026-10-01** — First review (findings A–F) and decisions D-1…D-6. Second review: every High
  finding re-read and held, seven corrections, findings O-1…O-18, decisions D-7…D-10. Fleet review:
  findings FX-1…FX-9, decisions D-11 and D-12. Plan accepted by the user; development held until the
  user says go. This document written and staged the same day.
