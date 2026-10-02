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
for development to be held until they say go. **They said go on 2026-10-01, skipping the old suites'
baseline (D-13); lane 0 was built the same day.**

**START HERE:**
1. § 1 is settled — thirteen decisions, all taken with the user on 2026-10-01. Decision numbers in
   this document are **travel-closure-local**; they are not the closure ledger's D-01…D-40.
2. **Lane 0** (§ 4) — **COMPLETE 2026-10-01**, committed `0b8cdf124` (`run-final-truth.mjs` 112/112
   twice on UAT). The old slice suites are not run on UAT at all (D-13). Starting the API on UAT: auto
   mode refuses `start-api-uat.ps1` unless this project's local settings allow it (the user added that
   rule on 2026-10-01); before every start, check UAT for pending migrations and ask the user first if
   one would be applied.
3. **Migration batch 1** (§ 5) — **APPLIED to UAT 2026-10-02**, committed `d426f4ed3`
   (`20261002000637_TravelClosureBatch1`, guarded SQL; 33/33 on a scratch copy of UAT first; verified on
   UAT; truth suite 112/112 twice after). Lanes 1–9 build on it.
4. **Lane 1** (§ 4) — **COMPLETE 2026-10-02**, in three slices: **1a** the request's write rules
   (committed `e1d050da2`), **1b** the lifecycle verbs (committed `8fadfd31e`), **1c** comments, the
   traveller's privacy and groups — built and proven (`run-final-lifecycle.mjs` 255/255 twice; the truth
   suite 117/117 twice) and staged for the user's commit.
5. **Next: lane 2** (the approval ladder and the approver's door, D-7), then lanes **3 → 4 → 5 → 6 → 7 →
   8 → 9 → 10** in that order (§ 2). Source-check each lane against this document before building it —
   line numbers are as of HEAD `bad482a8d`.

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
| **D-13** | Run the sixteen slice suites on UAT as lane 0's baseline? (asked at the go, 2026-10-01) | **No — skip the baseline.** The slice suites were written for a throwaway database: they hang their actors off the first position in the tenant (real staff's), mint `HR` and `TenantAdmin` logins they never switch off, approve fixture policies in the tenant and mostly delete nothing. The lane-0 truth suite is the baseline; each lane re-proves, with its own clean-up, what the slice suites covered in its area, and retires them by name in this document. |

**Standing assumptions (not re-asked):** the closure ledger's D-29 holds — the policy rule register
stays read-only and the policy-exception flow stays withheld until rule enforcement exists; Finance
posting stays as the posting sweep left it (no rule = Unposted, enabled under HR Settings → Finance
posting); the generic workflow inbox's desync is cross-module defect #15 and is recorded, not fixed.

---

## 2. Lane status

| Lane | What | Schema | Suite | Status |
|---|---|---|---|---|
| **0** | Harness on UAT, truth and fiction | none | `run-final-truth.mjs` | ✅ complete 2026-10-01 — 112/112 twice on UAT; committed `0b8cdf124` |
| **M1** | Migration batch 1 | the whole batch | `m1/test-cycle.sh` (session scratchpad) | ✅ applied to UAT 2026-10-02 — 33/33 on a scratch copy first, verified on UAT, truth suite 112/112 twice after; committed `d426f4ed3` |
| **1** | Request lifecycle | batch 1 | `run-final-lifecycle.mjs` | ✅ complete 2026-10-02 — 1a `e1d050da2`, 1b `8fadfd31e`, 1c 255/255 twice and truth 117/117 twice, staged |
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
  InProgress too. → lane 1 — **fixed in slice 1a, 2026-10-02**
- **A2 H** `CancelAsync` (l.713) never cancels the engine instance (leave and movements call
  `CancelWorkflowAsync`), so a cancelled Submitted trip leaves a live approval task; cancelling an
  Approved trip leaves confirmed bookings counted as committed, a disbursed advance outstanding and a
  Fleet trip live. → lanes 1, 6 — **lane 1's half fixed in slice 1b, 2026-10-02** (the approval task is
  cancelled with the trip; an approved trip with advance cash out cannot be cancelled); the bookings and
  the Fleet trip are lanes 5 and 6
- **A3 M** `ReturnedForRevision`, `InProgress` and `Closed` have no writer (D-6). → lanes 1, 8 —
  **ReturnedForRevision and Closed have writers since slice 1b, 2026-10-02**; InProgress is the sweep's
  (lane 8)
- **A4 M** `SubmitAsync` checks nothing but status: cost 0, return before departure (the mapper
  clamps the duration to 0), past dates, no itinerary, the policy; `SubmittedAt` comes from the DTO's
  default. → lane 1 — **fixed in slice 1a, 2026-10-02** (an itinerary is not required: a trip is often
  approved before its itinerary is drawn up)
- **A5 M** The mapper (`StaffTravelMappingExtensions.cs` l.111–172) takes `IsInternational`,
  `ApprovedBudget` (on a plain update), `PolicyId` (read by nothing), `GroupTravelId`,
  `ParentRequestId`, `OrganizationUnitId` and `EmployeeId` from the payload with no tenant check. The
  policy guard's own comment says *IsInternational is derived from the two countries* — it is not;
  declaring a domestic trip international buys the international caps. → lane 1 — **fixed in slice 1a,
  2026-10-02**, except `GroupTravelId` on an edit, which slice 1c moves to the group endpoints
- **A6 H** `GET /me/requests/{id}` returns every comment — `IsVisibleToTraveller` is read by nothing
  on the server, only the client filters — plus the budget, advances, claims and policy exceptions.
  → lane 1 — **fixed in slice 1c, 2026-10-02** (comments the desk did not share, at any depth, and policy
  exceptions are dropped on the server; the traveller's own budget, advances and claims stay — lane 7
  builds the portal's views of them)
- **A7 M** The desk's *Add comment* sends `commentType: 'General'`, which is not a C# member, so it
  **always 400s** (`hr/travel/[id]/page.tsx:137`); `isVisibleToTraveller` is hard-coded true (T-27).
  → lane 0 — **fixed 2026-10-01** (a Comment / Internal note toggle)
- **A8 M** The request form offers `Negotiation` and `Extreme` (T-3, both 400) and hides `Emergency`,
  five purposes, `Critical`, `Prohibited` and `System`; its replace payload drops `approvedBudget`,
  `policyId` and `groupTravelId`, so editing a group participant silently removes them from the
  group; a self-service request gets no organisation unit although a comment says it does. → lanes 0, 1
  — **lane 0's half fixed 2026-10-01** (options from the enums; the edit sends the three back; the
  comment corrected); **lane 1's half fixed 2026-10-02** (the unit is the server's, slice 1a; the three
  fields are off the edit, the group link last, slice 1c)
- **A9 M** The attachment upload offers six types, five of them not C# members — 400 unless *Other*
  (`TravelAttachmentsPanel.tsx:33`); its delete button renders for HR and 403s. → lane 0 — **fixed
  2026-10-01**
- **A10 L** Comment edit and delete check no author; the desk path takes `CancelledAt` from the body;
  the request has no approver column (only `UpdatedBy`); the request-number generator is not atomic
  (recorded). → lane 1 — **`CancelledAt` and the approver fixed in slice 1b, the comment author check in
  slice 1c, 2026-10-02**; the request-number generator stays recorded (§ 6)
- **A11 M** Group travel: `MaxParticipants` is not enforced, the group's status is whatever the PUT
  says, dates and destination are not pushed to participants, an existing request cannot be linked,
  and the add-traveller dialog hard-codes purpose, risk, `requiresVisa: false` and `GHS`. → lane 1 —
  **fixed in slice 1c, 2026-10-02** (the currency half in lane 0)
- **A12 M** No recall verb in the service or controller although `HrWorkflowFallbackAuthority` says
  *use all four or none*; the generic recall button works only while a definition is published.
  → lane 1 — **fixed in slice 1b, 2026-10-02**

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
  them as rules (D-1). → lanes 1, 3, 4 (lane 0 made the copy true meanwhile: a banner says they are
  not enforced, and the rules banner no longer counts the budgets among the caps)
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
  summary DTO, so *Number* and *Fee* always read "—". → lane 7 — **the create DTO and the list
  brought forward and fixed in lane 0** (2026-10-01); the edit dialog stays in lane 7
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
  *Superseded*; the policy form pushes to its own URL after save. → lane 0 — **fixed 2026-10-01**
- **F5 L** Five enums with no reference outside their own file (`TravelApproverType`,
  `TravelApprovalDecision`, `TravelApprovalInstanceStatus`, `TravelAllowanceType`, `TravelVendorType`);
  the `StaffTravelCurrencyBridge` alias is due for retirement. → lane 0 — **fixed 2026-10-01**

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
  cover its departments, and a requester can pick a unit with a laxer policy. → lanes 1, 4 — **lane 1's
  half fixed in slice 1a, 2026-10-02** (the request carries the traveller's own unit); the ancestry is
  lane 4's
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
  (`[id]/edit/page.tsx:51`); no amendment exists. → lane 1 (D-9) — **fixed in slice 1b, 2026-10-02**
  (Request change)
- **O-11 M — lifecycle edges:** cancel is allowed while InProgress; Complete before the trip starts; a
  Submitted trip whose departure passes is never escalated. → lanes 1, 8 — **lane 1's half fixed in
  slice 1b, 2026-10-02** (no cancel or change under way; no completion before the start); the
  escalation is lane 8's
- **O-12 M — travel is invisible to attendance.** `StaffAttendanceStatus.OnDuty` exists and the
  attendance dashboard counts it as present, but nothing writes it; leave posts its days through
  `LeaveAttendancePostingService` and travel posts nothing. → lane 9
- **O-13 M — no conflict checks.** Overlapping trips for one traveller and trips over approved leave
  are accepted; training's `NomineeAvailabilityService` and recruitment read travel as a conflict, but
  travel reads nothing back. → lanes 1, 9 — **lane 1's half fixed in slice 1a, 2026-10-02** (an
  overlapping trip is refused at submission, approved leave is a warning); leave's side is lane 9's
- **O-14 M — leavers:** separation reads only outstanding advances (`SeparationService.cs:2312`); a
  leaver's open trips, bookings and undisbursed advances are not flagged; a request can be raised for
  an inactive employee. → lanes 1, 9 — **lane 1's half fixed in slice 1a, 2026-10-02** (create and
  submit refuse a traveller who has left; so does adding them to a group); separation's side is lane 9's
- **O-15 M — duty-of-care deletes:** risk assessments (acknowledged or not), verified passports, issued
  alerts and current itineraries delete with no status guard — reachable by every HR officer under
  D-3. → lanes 5, 7
- **O-16 M — international trips:** no check that insurance covers the trip dates; no check of the
  passport's validity against the return date (T-26). → lane 7
- **O-17 L — portal:** the traveller cannot reply to a desk comment or download their own attachment;
  a desk comment notifies nobody. → lanes 7, 8
- **O-18 L** — deleting a group leaves its participants linked to it; no status history for requests,
  claims or advances beyond the workflow tab; lookups by number and the effective per-diem read take
  the first row across tenants (single tenant today). → § 6 — **the group half fixed in slice 1c,
  2026-10-02** (deleting a group takes its travellers off it); the rest stays in § 6
- **O-19 H — the people who use the travel screens could pick no currency on any of them** (found
  while building lane 0). Every travel currency dropdown — the request form, the four booking dialogs,
  the visa fee, insurance, the budget, the advance, the claim and its lines, the group's add-traveller
  dialog — read `GET /api/finance/currencies`, which `FinancePermissionPolicyMap` puts behind a Finance
  permission: HR officers and travellers get a 403 and an empty list, so none of those could be entered
  from the screens. `api/hr/currencies` (InternalOnly) exists for exactly this; area 13 met the same wall.
  → lane 0 — **fixed 2026-10-01**; the truth suite asserts both answers

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
   (`claims/new/page.tsx:168`); it is recovered on payment. (The copy was corrected in lane 0.)

⚠ **For the leave owner, not travel scope:** `LeaveService.EnsureMayDecideAsync` (l.1000) asks only the
engine, whose stage 1 is the Manager *role*, so any Manager-role user appears able to decide any
leave at stage 1 — the line-authority rule exists (l.3571) but is not applied to the decision.
Travel applies the line rule in its service (lane 2) rather than copying leave's approve path.

### 3d. The guide's T-findings — where each one now lives

| Status | Findings |
|---|---|
| **Fixed upstream — correct the prose only** | T-5 and T-37's conversion half (Finance FX fixed 2026-09-10, PR #99), T-8's supplier read (2026-09-22), T-43 (alert body), T-53 (the sweep has been hosted daily since 2026-08-17 — the guide was wrong when written), T-58 (claims and advances post since 2026-09-20), T-44's per-trip button (it exists) |
| **Kept by decision** | T-4 and T-49 (D-29), T-6 (recovery on payment — the copy was fixed in lane 0), T-51 (informational) |
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
  office has nothing to approve and nothing is held. → lane 6 (lane 0 made the text true: *a draft
  trip in Fleet; the vehicle is not held*)
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

### Lane 0 — Harness on UAT, truth and fiction (no schema)

**Status 2026-10-01: COMPLETE.** `run-final-truth.mjs` passed 112/112 twice on UAT (runs 631771
and 649145) after one harness fix (below); staged for the user's commit. No migration was pending on
UAT — its history already held every migration in the repo — so starting the API changed no schema.

**The harness.** The slice suites' `setup.mjs` was not repaired: it is retired from UAT with the slice
suites (D-13), and the closure has its own fixture.
- [x] `final-api.mjs` (performance style — no throw on a non-2xx; `observe()` records a known-open
  finding without counting it) and `final-setup.mjs`: the fixture's own unit and position under the live
  root, employees created with `employeeNumber: ''`, the tenant from `/api/auth/me`, every minted login
  tracked and switched off.
- [x] The teardown, in a `finally`: every row the run made is soft-deleted by its request and group ids
  (claim lines, flight segments, itinerary legs and activities included), then checked — nothing left
  live. **Claim and advance numbers are renamed `…~E2E<stamp>` before the soft delete**: under B9 a
  deleted claim makes every later claim on the tenant collide, so the teardown also checks that the next
  claim and advance numbers are free. (Read-only on 2026-10-01: UAT held one live claim and one live
  advance, and both next numbers were free.)
- [x] `hr-travel/README.md`: the environment, `start-api-uat.ps1`, the clamd stub, the suites and their
  counts, the teardown rule, and why the slice suites must not run on UAT.
- [—] *A supplier from the tenant* — lane 0 books nothing through a vendor; the first suite that does
  (lane 4, `PreferredVendorMandatory`) reads an active one through `api/hr/suppliers`.
- [—] *`workflow-definition.mjs`* — retired with the slice suites; lane 2's suite asserts on the seeded
  definition.
- [—] *Run the sixteen existing suites on UAT* — **skipped by D-13.**
- [x] **Found on the first UAT run (stamp 418116), fixed.** Every feature check passed, but the
  teardown renamed the two claims the suite leaves on one number into each other: the unique index
  refused, SQL Server carried on with the rest of the batch, and one deleted claim kept
  `EXP-2026-00002`, so every new claim on UAT would have collided. The rename now takes the row's id,
  the teardown runs as one transaction under `XACT_ABORT`, a failure reports SQL Server's own
  message, and `teardown-run.mjs <stamp> [--apply]` recovers a run whose teardown did not finish —
  it renamed the stranded claim, and both next numbers were checked free afterwards.

**Frontend truth (A7, A8, A9, F4, F5).**
- [x] Every travel union written from its C# enum. `travel-enums.ts` holds one label map per request
  enum (labels from `[Description]`) and every option list is generated from a map;
  `travel-enums.test.ts` holds the unions to the C# enums (36 compared) and the maps to their members
  and descriptions. One `TravelRiskLevel` export.
- [x] The composer: a Comment / Internal note toggle sets the type and the visibility. Attachment types
  from the enum. The attachment delete button only for `HR.Travel.Admin` (`useTravelAccess`).
- [x] The request form's payloads are built by `travel-request-payload.ts` (unit-tested): an edit sends
  `approvedBudget`, `policyId` and `groupTravelId` back as they are; `isInternational` is derived from
  the two countries; an empty id is sent as `null`.
- [x] `TravelQueryError` on every travel read (26 screens, panels and forms), so a 403 or a 500 no
  longer reads as "nothing yet"; `fmtTravelMoney` replaces ten formatters that fell back to `'GHS'` —
  no currency is invented; error toasts read `e.message`.
- [x] Backend: the stale FX and GL comments; `StaffTravelCurrencyBridge` retired onto
  `HrCurrencyBridge`; the five dead enums deleted; `StaffTravelBusinessRulesAttribute` maps
  `DbUpdateException` to 409 with a fixed sentence (the database's own text is logged, never returned).

**Found while building lane 0, fixed in it.**
- [x] **O-19** (§ 3b): all seven currency reads moved to `api/hr/currencies` through `CurrencyField` and
  `CurrencyPicker`. The claim line no longer starts in GHS (its default was read while the claim was
  still loading); the group's add-traveller dialog no longer defaults to GHS and requires a currency.
- [x] **E3, brought forward from lane 7.** The visa create DTO accepts the status, the number and the
  three dates it used to drop (an omitted status is still Not Started; a status number the enum does not
  have → 422). The visa summary carries the approval date, the fee, its currency and the number masked
  to its last four — O-7's rule, for visas. The client types the list reads as the summary. Lane 7 keeps
  the edit dialog, the passport side of O-7 and the tenant-scoped requirement lookup.
- [x] Strings that promised what the code does not do: the request form's visa and health switches
  (they record a need; nothing checks it); FX-1's "reserves the vehicle"; "Open the booking to see
  both" (no screen opens a booking); the alert dialog's "travellers see it on their trip" (a traveller
  sees only what is sent to them); the policy form's eight unenforced caps (a banner says so, and the two
  switches say *should*); the rules banner's "and the budgets"; the policy badges (*Not in force* for a
  withdrawn or superseded version — the record cannot tell which — *Approved — in force from …* for a
  future one, *Expired* for an ended one); the reminder horizons (one reminder, then escalation only
  after the date passes) and their audience (the HR role, in the app only); the travel reminders and
  travel policies nav descriptions; the HR hub's travel card (no per diem); "Raise an amendment instead"
  (there is none); the dashboard's *High risk* tile (it counts High, Critical and Prohibited); the portal
  alert panel's "compliance tab" (the portal has none); the claim page's "recovered when the claim is
  approved" (on payment) and "pay refuses anything not Approved" (it takes Partially approved too).
- [x] The policy edit form stayed open after a save: it pushed to its own page's URL (F4). It now
  closes through an `onSaved` callback.
- [x] The risk-assessment acknowledgement button shows only to the traveller — for everyone else it
  could only answer 403. A traveller without desk access still has no door (E1, lane 7).
- [x] The register honours `?employeeId=` from the employee record (with *Show every traveller*), and
  its type filter offers all eleven types.

Suite `run-final-truth.mjs` — **112 assertions with the clamd stub running; 112/112 twice on UAT,
2026-10-01**: § 1 the currency doors;
§ 2 every request-enum member round-trips (31), the old forms' fiction values are refused, the
traveller's own create and edit; § 3 an edit keeps a group participant in the group; § 4 comment types;
§ 5 a visa keeps its status, number and dates, and the list masks the number; § 6 all seven attachment
types, the old ones refused, delete for Admin only; § 7 the money and booking payloads, and the claim
number collision answering 409; § 8 the register's employee filter; the teardown's five checks. Four
known-open findings are observed, not counted: A5, A6, O-5 and the link an edit that omits it drops.
*Since migration batch 1 (2026-10-02):* § 7 asserts that a deleted claim no longer blocks the next one,
observes the reused number (B9, lane 3) as a fifth finding, and proves the 409 on the policy version
index instead of the claim number.

Checks run 2026-10-01: `frontend/tsconfig.travel-lane0.json` type-checks clean; `travel-enums.test.ts`
(9) and `travel-request-payload.test.ts` (6) pass; `hr-setup-nav.test.ts` passes. Four layout tests
fail outside travel (the HR sidebar keep-open list's leave and company-schedule leaves, procurement's
sidebar, the header and the dashboard layout) — none touches a file of this lane. The API log of the
UAT runs holds two kinds of error besides the deliberate claim collision (one a run, answered 409):
payroll's profile insert failing its payment-method key at every fixture employee create
(cross-module defect #23 — the employee still saves), and one HR/Identity reconciliation failure for
a demo user (*Sequence contains more than one element*, `HrIdentityReconciliationService`) — neither
is travel's; the second is recorded here for the HR identity owner.

### Lane 1 — Request lifecycle (A1–A6, A10–A12, D-6, D-9, O-5, O-10, O-11, O-13, O-14, T-16, T-17)

- [x] **Edit** only in Draft and ReturnedForRevision, on the desk and `/me`. The update DTO loses
  `ApprovedBudget`, `PolicyId` and `GroupTravelId` (group membership moves to the group endpoints).
  *Slice 1a; `GroupTravelId` stays on the edit until slice 1c gives the group its link endpoint, so the
  form still sends it back.*
- [x] **Server-derived facts:** `IsInternational` from the two countries on create and update; the
  organisation unit from the traveller's employee record; every FK tenant-validated (employee, unit,
  group, parent). Create refuses an inactive or separated traveller. *Slice 1a.*
- [x] **Submit preconditions:** cost > 0; return ≥ departure; departure ≥ today (the desk may override
  with a reason); a currency Finance holds; `MaxSingleTripBudget` of the applicable approved policy
  (D-1) — the 422 names the cap; a trip overlapping another Submitted/Approved/InProgress trip of the
  same traveller is refused; an overlap with approved leave is a warning; `SubmittedAt` from the clock.
  *Slice 1a.*
- [x] **The request form shows the policy that will apply and its caps** (a read that resolves the
  guard for a traveller and a date — T-16). *Slice 1a.*

**Slice 1a as built (2026-10-02)** — `StaffTravelRequestService` (create, update, submit, group
participants, the new `GetPolicyPreviewAsync`), the DTOs, the mapper, `StaffTravelPolicyGuard`
(`ResolveForAsync`; the caps record carries the single-trip limit, the policy's currency and its
version), `HrCurrencyBridge` (`GetBaseCurrencyCodeAsync`, `ConvertBetweenAsync`), both controllers;
on the frontend the types, the payload builder, the service, a `TravelPolicyPreview` card on the
request form (the desk's unit picker is gone), the late-submission dialog on the desk's request page,
the portal's "departure has passed" note, and both edit pages' lock rule.
- *The facts.* The unit is the employee's own, or their position's when the employee row has none (29
  of 4,398 UAT staff; none where the two disagree). Create and submit re-derive it, so a draft written
  before lane 1 is checked on the facts.
- *Who has left* is a record switched off or a status of Inactive, Terminated or Retired — what
  separation, termination and deactivation write. Probation, leave and suspension are statuses of
  someone still employed. Adding participants to a group checks every one first, so one leaver refuses
  the call instead of leaving half a group behind.
- *A past departure* is the desk's to submit, with a reason kept as an internal note in the
  submitter's name (so the caller must be employee-linked); the traveller cannot submit it from the
  portal. *Trips may meet on a travel day* — back from one and off on the next the same day — but not
  run over each other, and two cannot leave on the same day. Only Submitted, Approved and In-progress
  trips count.
- *The single-trip limit* applies only from an approved policy (UAT's only real policy is still an
  unapproved draft, so nothing binds there yet). A policy written before batch 1 has no currency and is
  read in the base; a trip in another currency is compared at Finance's rate and the 422 shows the
  converted figure. The policy checked is recorded on the request (`PolicyId` finally has a writer).
- *Submission answers* with the status, the policy and any warnings (approved leave over the days); the
  portal's submit answered 204 before, so a warning had nowhere to go.
- *An itinerary is not required* to submit (finding A4 listed it): trips are often approved before the
  itinerary is drawn up.
- *The demo pack* (`dev-harness/hr-demo-smoke/scenarios/080-travel.mjs`) broke on two new rules and is
  fixed in this slice: the Sebrepor trip (taken twelve working days before the build) is submitted with
  a late reason, and the Lagos request is linked to its group — and its health clearance set — while
  still a Draft, before the submit loop (the edit after submission is now refused). The hand-made policy
  links are gone: a request records the policy it was checked against, and the demo's policy stays an
  unapproved draft until lane 4 (D-3) and lane 10 have hr.head approve it. Proven at the next demo
  rebuild (lane 10); UAT's demo trips are already built and are not re-run.

**Suite** `run-final-lifecycle.mjs` (slice 1a: 103 assertions) — §1 the facts, desk and portal; §2 what
a create refuses; §3 the edit lock through Submitted, Approved and Rejected, desk and portal; §4
submission — cost, the past departure, overlaps, the limit in GHS and in USD, the policy recorded, the
leave warning, the clock, no approval task left by a refusal; §5 the preview, desk and portal; §6 group
participants. Its fixture adds a TenantAdmin login (to approve the run's unit-scoped policy — HR cannot
until lane 4), an employee deactivated through the API, and one approved leave planted for its own
traveller and deleted after. Every request it submitted is decided before the teardown, so no
approval task is left in a real approver's inbox, and the teardown retires the run's notifications
(each run writes about 230, mostly the engine's *Approval Required* to the Manager, HR and TenantAdmin
role holders; emails dead-letter on UAT, which has no mail server). **103/103 twice on UAT, 2026-10-02**
(stamps 138908, 184961; and 308438 after a date-format fix in the suite — the server writes September
as "Sept"). Regression: `run-final-truth.mjs` **114/114 twice** (stamps 207913, 223419) — O-5 and A5 now
asserted, three findings still observed (the group link on an edit and A6 for slice 1c, B9 for lane 3).
Four runs wrote 44 requests and 466 notifications to UAT and left none live, no open workflow instance,
no fixture leave, policy or active login.
- [x] **Cancel:** cancels the engine instance while Submitted; refused once InProgress; from Approved
  it requires no disbursed advance with money outstanding (the 422 names it) and cancels pending and
  confirmed bookings and undispatched fleet trips (lanes 5, 6). *Slice 1b; the bookings and the Fleet
  trip remain lanes 5 and 6.*
- [x] **D-6 writers.** `ReturnForRevisionAsync` (the approve gate) on leave's suggest-changes shape
  (`LeaveService.cs:1163-1177`): `HasActiveApprovalWorkflowAsync` → `CancelWorkflowAsync` → check
  `.Success` → set the status directly (the engine has no *returned* outcome), stamping
  `ReturnedAt/ById/Reason`. `CloseAsync` (HR), `ClosedAt/ById`; the sweep closes too (lane 8) when every
  claim is Paid or Rejected and every advance FullySettled, refunded or written off, or when no claim
  was filed by `TravelEndDate + ExpenseSubmissionDays`. Nothing is booked, advanced or claimed on a
  Closed trip. Complete only on or after `TravelStartDate`. *Slice 1b; the sweep's close is lane 8's.*
- [x] **D-9 Request change** (traveller, desk or approver; Approved only): returns the trip to
  ReturnedForRevision with a reason (`ChangeRequestedAt/ById`, `ChangeReason`); bookings, advances and
  claims stay linked; resubmission re-enters the two-stage ladder. The edit page's "raise an amendment"
  text becomes this button. *Slice 1b — the traveller and the desk; the approver gets their door in
  lane 2.*
- [x] **Recall** — service, `/recall` and `/me/recall`: the requester-only check in the service first
  (the helper does not enforce it), then `HrWorkflowFallbackAuthority.RecallAsync`, then
  `ApplyRecallOutcome` whatever the helper returns. This completes "all four or none". *Slice 1b.*
- [x] **`ApprovedById`** (new column) is the final approver's employee id, stamped in `ApproveAsync`; an
  instance completed through the generic inbox never passes the service, so it stays null there
  (recorded under #15). *Slice 1b.*
- [x] Comment edit and delete by their author (or Admin); `CancelledAt` from the clock; the `/me`
  detail filters comments by `IsVisibleToTraveller` on the server and drops policy exceptions.
  *`CancelledAt` in slice 1b; the rest in slice 1c.*

**Slice 1b as built (2026-10-02)** — `StaffTravelRequestService` (`CancelAsync`, `MarkCompletedAsync`,
`ApproveAsync`'s stamp, and the new `ReturnForRevisionAsync`, `RequestChangeAsync`, `RecallAsync`,
`CloseAsync`), a new `StaffTravelRequestGuards` (a closed trip takes nothing more — used by the four
booking creates and the budget, claim and advance creates), the read DTO's lifecycle fields and the
repository's includes for them, four desk routes (`return`, `request-change`, `recall`, `close`) and two
portal routes (`recall`, `request-change`); on the frontend a shared `TravelReasonDialog`, a
`TravelLifecycleNotes` card, the desk page's Return for revision, Request change, Close and its recall
wired into the shared workflow actions (the generic recall never told the request), the portal's Recall
and Request change, and both edit pages' Request change button.
- *Cancel* asks the engine whether the record has a live instance (not whether a definition is
  published: a request submitted before one was has no instance) and cancels it first; a refusal stops
  the cancel. A rejected request is refused too — its rejection reason lives in `CancellationReason`,
  which a cancel would overwrite.
- *Return for revision* runs the approve gate (the engine's assignee, or the approve tier with no
  definition), never the traveller, before it cancels the approval. *Recall* allows the traveller or
  whoever raised the request; with an instance the engine also insists on the login that submitted it, so
  a trip the desk submitted is the desk's to recall — the traveller is told to ask them.
- *Request change* keeps the approval's stamps as the record of what is being changed until the next
  approval replaces them; return, request change and recall all clear `SubmittedAt` and the recorded
  policy, which the next submission sets again.
- *Close* needs a completed trip, every claim paid or rejected, and every advance settled, written off,
  rejected or cancelled — until lane 3 gives advances reject and cancel verbs, an unpaid requested advance
  is deleted (Admin) before closing.
- *Not yet:* none of the new verbs notifies anyone — who hears of each is lane 8's (D-4).

**Suite** `run-final-lifecycle.mjs` gains §7–§11 (193 assertions in all): §7 cancel — the approval task
withdrawn on the desk and the portal, the clock's time, refused for a rejected request, for an approved
trip with a paid-out advance (named, with its amount), and under way; §8 return for revision — refused
for a draft, without a reason and for a plain employee, the task withdrawn, edited and resubmitted into
a new approval, an officer refused on their own trip; §9 request change — the approver recorded, the
advance kept, edited, resubmitted and approved again, from the portal too and nobody else's; §10 recall
— the traveller's own, the desk's submission refused to the traveller, an outsider refused, the
raiser's recall; §11 complete and close — not before the start, not while a claim or an advance is open
(each named), closed, and then no advance, claim, budget or booking. "Under way" is set on the run's own
trip in SQL (lane 8 writes InProgress). The suite pays out one advance, which writes an unposted row to
HR's Finance posting register (UAT has no posting rules, so no journal); the teardown retires the run's
register rows with its notifications. **193/193 twice on UAT, 2026-10-02** (stamps 651140, 780011).
Regression: `run-final-truth.mjs` **114/114 twice** (stamps 849263, 860396), the same three observed.
Four runs wrote 70 requests, 8 advances, 2 register rows and 1,474 notifications, and left none live —
no open workflow instance, fixture leave, policy or active login. The API log's only errors were the
known payroll defect #23, the truth suite's deliberate duplicate policy, the known demo-user identity
reconciliation, and one failure of the platform's notification clean-up job under the suite's load
(it deleted nothing and recovered).
- [x] **Groups:** `MaxParticipants` enforced; a change of the group's dates or destination propagates
  to Draft participants; `POST groups/{id}/requests/{requestId}` links an existing Draft request; the
  group's status moves by verb (open, close, cancel), not by PUT; deleting a group detaches its
  participants; the add-traveller dialog takes purpose, risk, visa and currency. *Slice 1c.*

**Slice 1c as built (2026-10-02)** — `StaffTravelRequestService` (comment author rule; group create and
edit checks, propagation, the open / close / cancel verbs, `LinkGroupParticipantAsync`, the delete that
detaches, capacity on add), the mapper (`SeatsTaken`, `ToTravellerView`, no group link from either
request DTO, no status from the group edit), the group repository reads (each traveller's destination
country; the by-status list counts places), both controllers (comment routes with the Admin test, four
group routes, the traveller's view on `/me`); on the frontend the group page rebuilt around the verbs,
with a link dialog, the fuller add-traveller dialog and a "differs from the group" mark, and the payload
builder without the group link.
- *A place* is held by every linked trip that is not cancelled or rejected — the count used to include
  them, so a cancelled traveller held a place for ever, and could not be added again.
- *A new destination or new dates* go to every trip still a draft or returned for revision — the two
  states the requester may edit (lane 1's lock). A submitted or approved trip keeps what its approver is
  deciding or decided; the page marks it. The PUT ignores any status it is sent.
- *Linking* takes only a draft or returned request of a traveller not already on the group, while the
  group takes travellers (Planning or Open) and has room; the trip takes the group's destination and
  dates (and so is international if the group goes abroad). A trip on another group must come off it
  first.
- *Cancelling a group* is refused while any traveller's trip is going ahead (draft, submitted,
  approved, returned or under way) — the error names them. Each trip has its own approval and money and
  its own cancel rules, so the group does not cancel them for the desk.
- *The group link is off both request DTOs* (create and edit): a payload that names a group is ignored,
  not believed and not refused. *A group's lead* must be an employee still employed, its destination a
  country the organisation holds, its dates in order.
- *The traveller's view* drops comments the desk did not share at every depth of replies, and the
  policy exceptions; their own budget, advances and claims stay (lane 7 builds the portal's views of
  them).
- *Comments* are edited and deleted by their author or a travel administrator (`HR.Travel.Admin`,
  evaluated against the same policy as the routes); deleting was administrators only, so an officer
  could not take back their own comment, while any officer could rewrite a colleague's.
- *The demo pack* links the Lagos request through the new route (`080-travel.mjs`).
- *InProgress and Completed* group statuses still have no writer; they belong with lane 8's sweep, which
  can derive them from the travellers' trips (recorded there).

**Suite** `run-final-lifecycle.mjs` gains §12–§14 (255 assertions in all; its fixture adds a second HR
officer): §12 a comment edited and deleted by its author and an administrator, refused to a colleague;
§13 the traveller's read without internal notes at any depth and without the policy exception the desk
reads (a real rule and exception planted on the run's policy); §14 groups — a leaver lead and backwards
dates refused, the PUT's status ignored, the limit on add, link and edit, a link that aligns the trip,
links refused twice and for a submitted request, propagation to drafts and not to a submitted trip, the
request edit leaving the link alone, open/close/reopen, cancel refused with live trips (named) and then
done, a cancelled trip freeing its place, a cancelled group refusing edits, delete detaching travellers
while their trips carry on. The first run (stamp 339381) failed one 1a assertion slice 1c had made stale
— a create naming a group is now accepted on no group, not refused — and, because the suite expected a
refusal, it had not tracked the request, which stayed live until `teardown-run.mjs 339381 --apply`; the
suite now asserts the new behaviour and tracks every create, refusal expected or not. Then **255/255
twice on UAT, 2026-10-02** (stamps 517575, 600995). Regression: `run-final-truth.mjs` **117/117 twice**
(stamps 702525, 713508) — A8's group link and A6 now asserted; only B9 still observed (lane 3). Five runs
wrote 120 requests, 11 groups and 2,715 notifications and left none live — no rule, exception, comment,
register row, open workflow instance, fixture leave, policy or active login.

*A teardown safety net, and the lesson it taught.* Both suites' teardowns now also take in everything
their own fixture travellers own, found by the run's stamp (`includeDiscovered`), so a create a suite did
not record is still decided and torn down. Its first two runs (stamps 133298, 231515) passed every
feature check and then failed the teardown: SQL Server prints GUIDs in upper case and the API in lower,
so the merged set held both forms of one id, the teardown's id table refused the duplicate, and the
atomic teardown rolled everything back. Both were recovered with `teardown-run.mjs <stamp> --apply` (no
approval left open, nothing live after); ids are now lower-cased on discovery and de-duplicated without
regard to case before any SQL. Then both suites passed twice more on the final harness (lifecycle
335140, 435255 — 255/255; truth 416045, 519338 — 117/117), and UAT was checked to hold no live travel
fixture row from any run. The API log's only errors
were the known ones, and the platform's notification clean-up job failing twice more under the suite's
load (a bulk `UPDATE` over `Notifications` after 4–6 s, then "Deleted 0") — not travel's; recorded for the
platform owner.

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
  (`TravelAdvanceStatus.Rejected = 8`, `Cancelled = 9`); `UnsettledAmount` 0 until disbursed — and the
  data step moved here from batch 1: zero it on existing Requested and Approved advances in the same
  slice as the disbursement code that sets it; the
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
  Fleet's dispatch, lane 6); Completed → Closed when settled (lane 1's rule); advance → Overdue. *Added
  by lane 1, slice 1c:* a group's **InProgress** and **Completed** are still written by nothing — the
  sweep derives them from its travellers' trips (in progress once any is under way; completed once every
  place is completed or closed).
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
from the two countries for live requests; a claim's `CurrencyCode` set to the tenant's base where it
differs; a policy's `CurrencyCode` set to the tenant's base; a reminder dispatch's new `PublishedAt` set
to the time it was written (the sweep treated those rows as sent — lane 8 must not send them again).

⚠ **Moved to lane 3: zeroing `UnsettledAmount` on Requested and Approved advances** (2026-10-02, while
writing the migration). Today's disbursement does not recompute the amount — only approval sets it — so
an Approved advance zeroed by this batch would be disbursed with nothing outstanding, and the claim that
should recover it would recover nothing. It ships in lane 3 with the code that sets the amount at
disbursement.

**As built (2026-10-02):** `20261002000637_TravelClosureBatch1` — the scaffold rewritten as guarded SQL
(74 batches up, 76 down). EF's scaffold also drops `IX_StaffTravelPolicies_TenantId`, because the new
policy index starts with the tenant and covers its foreign key. Down refuses while an advance records a
refund, a booking carries an exception state or a claim records a voided payment, and it refuses to
restore the unfiltered number indexes while a deleted row shares a number. Proven on a scratch copy of
UAT (`m1/test-cycle.sh`, 33/33): Up, Up again (no rows changed, same schema), Down (UAT's schema exactly),
Up again; the two corrections on planted rows; a duplicate live policy version refused and rolled back,
a deleted duplicate not; the three Down refusals rolled back. Against UAT it adds 38 columns, 17
indexes, 14 foreign keys and 3 defaults, and removes the three indexes it replaces. On UAT the data part
fills one policy's currency (GHS) and one dispatch's published time; the corrections touch nothing.

**Applied to UAT 2026-10-02, on the user's go.** Restore point: `ErpSystemDB_UAT_before_travelb1.bak`
(COPY_ONLY, verified), taken just before. The API's startup applied it — history 108 rows, newest
`20261002000637_TravelClosureBatch1` — and it was verified in SQL, not from the log: every post-Up check
held, and UAT's schema fingerprint equals the scratch copy's after Up. The truth suite then passed
112/112 twice. Its claim section had to change, because the batch did what it was meant to: a deleted
claim no longer blocks the next one, so the old 409 became a 201. The suite now asserts that, records the
reused number as an observation for lane 3, and shows the 409 mapping on the new policy version index —
two drafts claiming one version, which lane 4 ends by assigning the number on the server.

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
database); lane 0's truth suite is the first UAT run (D-13 skipped the old suites' baseline). The demo database holds the four demo trips of
`080-travel.mjs` and `081-travel-logistics.mjs`, with the 2026 policy unapproved.

---

## 9. Log

- **2026-10-01** — First review (findings A–F) and decisions D-1…D-6. Second review: every High
  finding re-read and held, seven corrections, findings O-1…O-18, decisions D-7…D-10. Fleet review:
  findings FX-1…FX-9, decisions D-11 and D-12. Plan accepted by the user; development held until the
  user says go. This document written and staged the same day.
- **2026-10-01, later** — The user said go, skipping the baseline (D-13). Lane 0 built and staged: the
  frontend truth (A7, A8, A9, F4, F5); O-19 found and fixed; E3's create and list brought forward from
  lane 7; the strings that promised more than the code does; the backend tidy-ups and the 409; the new
  fixture, truth suite and harness README. Scoped type-check and unit tests clean. The suite has not
  run: it waits for the user's build and the go to start the API on UAT.
- **2026-10-02** — Migration batch 1 written: the model changes, the user's scaffold, the guarded-SQL
  rewrite, and 33/33 on a scratch copy of UAT. The `UnsettledAmount` data step moved to lane 3 (§ 5).
- **2026-10-02, later** — **Migration batch 1 applied to UAT** on the user's go, after a verified COPY_ONLY
  backup; verified in SQL; the truth suite's claim section updated for the filtered index and the 409 moved
  to the policy version index; 112/112 twice. Staged. Next: lane 1.
- **2026-10-01, night** — **Lane 0 complete.** The build succeeded; no migration was pending on UAT;
  the user allowed `start-api-uat.ps1` under auto mode. The first run passed every feature check but
  its teardown failed (the claim-rename collision in lane 0's checklist) — fixed, and the stranded
  claim renamed with `teardown-run.mjs`. Then 112/112 twice. Restaged for the user. Next: migration
  batch 1 (§ 5).
- **2026-10-02** — The user committed lane 0 (`0b8cdf124`) and migration batch 1 (`d426f4ed3`).
- **2026-10-02, later** — **Lane 1, slice 1a built and proven** (the request's write rules: the edit
  lock, the server's facts, what create and submission refuse, the policy preview). The build
  succeeded; no migration pending on UAT; `run-final-lifecycle.mjs` 103/103 twice (and once more after
  a harness date fix), the truth suite 114/114 twice. Staged. Next: slice 1b.
- **2026-10-02, later** — The user committed slice 1a (`e1d050da2`). **Slice 1b built and proven** (the
  lifecycle verbs): the build succeeded; no migration pending on UAT; `run-final-lifecycle.mjs` 193/193
  twice, the truth suite 114/114 twice. Staged. Next: slice 1c.
- **2026-10-02, later** — The user committed slice 1b (`8fadfd31e`). **Slice 1c built and proven — LANE 1
  COMPLETE** (comments by their author, the traveller's view, groups by verb with a binding limit, links
  and propagation): the build succeeded; no migration pending on UAT; one stale 1a assertion and the
  untracked request it left were fixed; `run-final-lifecycle.mjs` 255/255 twice, the truth suite 117/117
  twice. Staged. Next: lane 2.
